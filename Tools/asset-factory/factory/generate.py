"""Generators: ComfyUI (the 5090) and a mock used to test the loop without a GPU.

ComfyUI workflows are exported from the ComfyUI editor with "Save (API Format)" into factory/workflows/<name>.json.
Anywhere a string value in that JSON is exactly one of these placeholders, the factory fills it in:
  "{{prompt}}" "{{negative}}" "{{seed}}" "{{width}}" "{{height}}" "{{frames}}" "{{fps}}"
  "{{image}}"      first/reference image (uploaded to ComfyUI first)
  "{{last_image}}" optional last frame (first/last-frame video models; same as image for a seamless loop)
Numbers are written as numbers when the placeholder is the whole value.
"""
import copy
import json
import random
import shutil
import time
import uuid
from pathlib import Path

import httpx

PLACEHOLDERS = ("prompt", "negative", "seed", "width", "height", "frames", "fps", "image", "last_image", "speaker", "text", "out")
NUMERIC = {"seed", "width", "height", "frames", "fps"}


def fill(node, values):
    if isinstance(node, dict):
        return {k: fill(v, values) for k, v in node.items()}
    if isinstance(node, list):
        return [fill(v, values) for v in node]
    if isinstance(node, str) and node.startswith("{{") and node.endswith("}}"):
        key = node[2:-2]
        if key in values:
            v = values[key]
            return int(v) if key in NUMERIC and v is not None else v
    return node


class ComfyGenerator:
    def __init__(self, base_url, workflows_dir: Path, timeout=1800):
        self.base = base_url.rstrip("/")
        self.workflows = workflows_dir
        self.timeout = timeout
        self.client_id = uuid.uuid4().hex

    def busy(self):
        try:
            q = httpx.get(f"{self.base}/queue", timeout=10).json()
            return len(q.get("queue_running", [])) + len(q.get("queue_pending", []))
        except Exception:
            return -1

    def gpu(self):
        try:
            stats = httpx.get(f"{self.base}/system_stats", timeout=10).json()
            dev = (stats.get("devices") or [{}])[0]
            return {"name": dev.get("name"), "vram_total": dev.get("vram_total"), "vram_free": dev.get("vram_free")}
        except Exception as e:
            return {"error": str(e)}

    def _upload(self, path: Path):
        with open(path, "rb") as f:
            r = httpx.post(f"{self.base}/upload/image", files={"image": (path.name, f, "image/png")},
                           data={"overwrite": "true"}, timeout=120)
        r.raise_for_status()
        return r.json()["name"]

    def run(self, workflow: str, values: dict, out_dir: Path):
        graph = json.loads((self.workflows / f"{workflow}.json").read_text(encoding="utf-8"))
        values = dict(values)
        for key in ("image", "last_image"):
            if values.get(key):
                values[key] = self._upload(Path(values[key]))
        graph = fill(copy.deepcopy(graph), values)
        r = httpx.post(f"{self.base}/prompt", json={"prompt": graph, "client_id": self.client_id}, timeout=60)
        if r.status_code != 200:
            raise RuntimeError(f"ComfyUI refused the workflow: {r.text[:400]}")
        prompt_id = r.json()["prompt_id"]
        started = time.time()
        while time.time() - started < self.timeout:
            time.sleep(3)
            h = httpx.get(f"{self.base}/history/{prompt_id}", timeout=30).json()
            if prompt_id not in h:
                continue
            item = h[prompt_id]
            status = item.get("status", {})
            if status.get("status_str") == "error":
                raise RuntimeError("ComfyUI error: " + json.dumps(status.get("messages", []))[:600])
            files = []
            for node in item.get("outputs", {}).values():
                for kind in ("videos", "gifs", "images"):
                    for f in node.get(kind, []):
                        if f.get("type") == "temp":
                            continue
                        params = {"filename": f["filename"], "subfolder": f.get("subfolder", ""), "type": f.get("type", "output")}
                        data = httpx.get(f"{self.base}/view", params=params, timeout=300).content
                        dest = out_dir / f["filename"]
                        dest.write_bytes(data)
                        files.append(str(dest))
            if files or status.get("completed"):
                return files
        raise TimeoutError(f"ComfyUI did not finish within {self.timeout}s")


class MockGenerator:
    """Makes a short clip from the reference image (slow zoom + bob) so the queue, review and UI can be tested."""

    def busy(self):
        return 0

    def gpu(self):
        return {"name": "mock", "vram_total": 0, "vram_free": 0}

    def run(self, workflow: str, values: dict, out_dir: Path):
        import cv2
        import numpy as np
        src = values.get("image") if workflow != "audio" else None
        frames = int(values.get("frames") or 49)
        w, h = int(values.get("width") or 480), int(values.get("height") or 848)
        if src and Path(src).exists():
            img = cv2.imread(str(src), cv2.IMREAD_UNCHANGED)
            if img.shape[2] == 4:
                a = img[:, :, 3:4] / 255.0
                img = (img[:, :, :3] * a + 255 * (1 - a)).astype(np.uint8)
        else:
            img = np.full((h, w, 3), 255, np.uint8)
        # Fit inside the frame keeping her proportions (never stretch), centred on white.
        k = min(w / img.shape[1], h / img.shape[0])
        fitted = cv2.resize(img, (max(1, int(img.shape[1] * k)), max(1, int(img.shape[0] * k))), interpolation=cv2.INTER_AREA)
        canvas = np.full((h, w, 3), 255, np.uint8)
        y0, x0 = (h - fitted.shape[0]) // 2, (w - fitted.shape[1]) // 2
        canvas[y0:y0 + fitted.shape[0], x0:x0 + fitted.shape[1]] = fitted
        img = canvas
        if workflow == "audio":
            import wave
            rate, sec = 24000, 2.0
            t = np.arange(int(rate * sec)) / rate
            tone = (0.25 * np.sin(2 * np.pi * 440 * t) * np.exp(-t)).astype(np.float32)
            dest = out_dir / f"mock-{random.randint(0, 9999)}.wav"
            with wave.open(str(dest), "wb") as w:
                w.setnchannels(1); w.setsampwidth(2); w.setframerate(rate)
                w.writeframes((tone * 32767).astype(np.int16).tobytes())
            return [str(dest)]
        if workflow.endswith("image"):
            dest = out_dir / f"mock-{random.randint(0, 9999)}.png"
            cv2.imwrite(str(dest), img)
            return [str(dest)]
        dest = out_dir / f"mock-{random.randint(0, 9999)}.mp4"
        vw = cv2.VideoWriter(str(dest), cv2.VideoWriter_fourcc(*"mp4v"), int(values.get("fps") or 24), (w, h))
        for i in range(frames):
            t = i / max(1, frames - 1)
            s = 1 + .03 * np.sin(t * np.pi * 2)
            m = cv2.getRotationMatrix2D((w / 2, h / 2), 0, s)
            m[1, 2] += 6 * np.sin(t * np.pi * 2)
            vw.write(cv2.warpAffine(img, m, (w, h), borderValue=(255, 255, 255)))
        vw.release()
        time.sleep(.5)
        return [str(dest)]


class HttpGenerator:
    """Any local model service (voice, music, SFX, a custom upscaler…): POSTs a JSON body built from `request`
    (placeholders as in ComfyUI workflows, plus "{{image_b64}}" and "{{text}}") to `url`. The reply is either the file
    itself (audio/*, video/*, image/*) or JSON with "files": [paths on this machine] or "b64" + "ext"."""

    EXT = {"audio/wav": ".wav", "audio/x-wav": ".wav", "audio/mpeg": ".mp3", "audio/flac": ".flac", "audio/ogg": ".ogg",
           "video/mp4": ".mp4", "video/webm": ".webm", "image/png": ".png", "image/jpeg": ".jpg", "image/webp": ".webp"}

    def __init__(self, spec):
        self.spec = spec

    def busy(self):
        return 0

    def gpu(self):
        return {}

    def run(self, workflow, values, out_dir: Path):
        import base64
        values = dict(values)
        if values.get("image"):
            values["image_b64"] = base64.b64encode(Path(values["image"]).read_bytes()).decode()
        body = fill(copy.deepcopy(self.spec.get("request", {"prompt": "{{prompt}}"})), values)
        headers = {}
        if self.spec.get("api_key_env"):
            import os
            key = os.environ.get(self.spec["api_key_env"], "")
            if not key:
                raise RuntimeError(f"缺少环境变量 {self.spec['api_key_env']}（付费服务的钥匙只放环境变量）")
            headers[self.spec.get("auth_header", "Authorization")] = self.spec.get("auth_prefix", "Bearer ") + key
        r = httpx.post(self.spec["url"], json=body, headers=headers, timeout=self.spec.get("timeout", 1800))
        r.raise_for_status()
        poll = self.spec.get("poll")
        if poll:
            # Cloud task APIs: submit returns a task id; ask `poll.url` until the task is done, then download the result.
            task = _dig(r.json(), poll.get("task_field", "taskId"))
            started = time.time()
            while time.time() - started < self.spec.get("timeout", 1800):
                time.sleep(poll.get("every", 5))
                q = httpx.post(poll["url"], json={poll.get("task_param", "taskId"): task}, headers=headers, timeout=60).json()
                status = str(_dig(q, poll.get("status_field", "status")))
                if status in poll.get("fail_values", ["FAILED", "failed", "error"]):
                    raise RuntimeError("云端任务失败：" + json.dumps(q, ensure_ascii=False)[:300])
                if status in poll.get("done_values", ["SUCCESS", "succeeded", "done"]):
                    url = _dig(q, poll.get("result_field", "results.0.url"))
                    data = httpx.get(url, timeout=300).content
                    ext = Path(url.split("?")[0]).suffix or self.spec.get("ext", ".bin")
                    dest = out_dir / f"{self.spec.get('name', 'cloud')}-{uuid.uuid4().hex[:8]}{ext}"
                    dest.write_bytes(data)
                    return [str(dest)]
            raise TimeoutError("云端任务超时")
        kind = r.headers.get("content-type", "").split(";")[0]
        if kind in self.EXT:
            dest = out_dir / f"{self.spec.get('name', 'http')}-{uuid.uuid4().hex[:8]}{self.EXT[kind]}"
            dest.write_bytes(r.content)
            return [str(dest)]
        data = r.json()
        if data.get("files"):
            return [str(Path(f)) for f in data["files"]]
        if data.get("b64"):
            dest = out_dir / f"{self.spec.get('name', 'http')}-{uuid.uuid4().hex[:8]}{data.get('ext', '.bin')}"
            dest.write_bytes(base64.b64decode(data["b64"]))
            return [str(dest)]
        raise RuntimeError("服务没有返回文件：" + json.dumps(data)[:300])


def _dig(data, path):
    """'a.b.0.c' → data['a']['b'][0]['c']"""
    for part in str(path).split("."):
        data = data[int(part)] if isinstance(data, list) else data.get(part)
    return data


class CommandGenerator:
    """Any command-line tool: `cmd` is a list; "{{out}}" becomes an output path in the attempt folder (with the
    backend's `ext`), other placeholders as above. Whatever new files appear in the folder are the outputs."""

    def __init__(self, spec):
        self.spec = spec

    def busy(self):
        return 0

    def gpu(self):
        return {}

    def run(self, workflow, values, out_dir: Path):
        import subprocess
        values = dict(values)
        values["out"] = str(out_dir / f"{self.spec.get('name', 'cmd')}-{uuid.uuid4().hex[:8]}{self.spec.get('ext', '.bin')}")
        before = set(out_dir.iterdir())
        cmd = [str(fill(part, values)) for part in self.spec["cmd"]]
        done = subprocess.run(cmd, cwd=self.spec.get("cwd"), capture_output=True, text=True, timeout=self.spec.get("timeout", 1800))
        if done.returncode != 0:
            raise RuntimeError(f"命令失败（{done.returncode}）：{(done.stderr or done.stdout)[-400:]}")
        return [str(p) for p in sorted(set(out_dir.iterdir()) - before)]


class Backends:
    """Named model backends from config.yaml `backends:`. A brief lists the backends that can make it; new models are
    added by adding a backend (and, for ComfyUI, its workflow JSON) — no code change. With `mock: true` every backend
    is the mock generator."""

    def __init__(self, cfg, root: Path):
        self.cfg = cfg
        self.root = root
        self.comfy = None if cfg.get("mock") else ComfyGenerator(cfg.get("comfy_url", "http://127.0.0.1:8188"), root / "workflows",
                                                                 cfg.get("timeout_seconds", 1800))
        self.mock = MockGenerator()
        self.specs = {name: dict(spec, name=name) for name, spec in (cfg.get("backends") or {}).items()}

    def names_for(self, brief, allowed=lambda name: True):
        """Backends that may make this brief now: listed on it, known, not switched off for poor results, and — for paid
        cloud backends — approved by 少爷 with budget left (the `allowed` check)."""
        names = brief.get("backends") or []
        if not names and brief.get("workflow"):
            names = ["comfy:" + brief["workflow"]]
        known = [n for n in names if n in self.specs or n.startswith("comfy:")] or ([] if self.specs else names)
        return [n for n in known if allowed(n)]

    def spec(self, name):
        return self.specs.get(name, {})

    def run(self, name, values, out_dir: Path, kind="video"):
        if self.cfg.get("mock"):
            return self.mock.run("image" if kind == "image" else "audio" if kind in ("voice", "sfx", "music") else "video", values, out_dir)
        if name.startswith("comfy:"):
            return self.comfy.run(name[6:], values, out_dir)
        spec = self.specs.get(name)
        if spec is None:
            raise RuntimeError(f"config.yaml 里没有这个后端：{name}")
        kind_ = spec.get("type", "comfy")
        if kind_ == "comfy":
            return self.comfy.run(spec["workflow"], values, out_dir)
        if kind_ in ("http", "cloud"):
            return HttpGenerator(spec).run(None, values, out_dir)
        if kind_ == "command":
            return CommandGenerator(spec).run(None, values, out_dir)
        raise RuntimeError(f"不认识的后端类型 {kind_}")

    def busy(self):
        return self.mock.busy() if self.comfy is None else self.comfy.busy()

    def gpu(self):
        return self.mock.gpu() if self.comfy is None else self.comfy.gpu()


def copy_inputs(paths, dest: Path):
    dest.mkdir(parents=True, exist_ok=True)
    out = []
    for p in paths:
        p = Path(p)
        if p.exists():
            t = dest / p.name
            if not t.exists():
                shutil.copy2(p, t)
            out.append(str(t))
    return out
