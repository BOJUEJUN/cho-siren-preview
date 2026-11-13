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

PLACEHOLDERS = ("prompt", "negative", "seed", "width", "height", "frames", "fps", "image", "last_image")
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
        src = values.get("image")
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


def make_generator(cfg, root: Path):
    if cfg.get("mock"):
        return MockGenerator()
    return ComfyGenerator(cfg["comfy_url"], root / "workflows", cfg.get("timeout_seconds", 1800))


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
