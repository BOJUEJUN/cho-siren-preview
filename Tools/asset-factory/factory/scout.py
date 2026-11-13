"""Model radar (少爷: "今天发布了什么牛逼的模型能在 5090 跑也自己关注"). Once a day it asks the public Hugging Face API
for the trending models of every kind the factory makes, keeps the ones whose weights look like they fit a 32 GB
card, and lists the new ones on the page. Trying one = add a backend in config.yaml (+ its ComfyUI workflow) and put
it in a brief's `backends`; the engine then gives it trial attempts and keeps using it only if it passes more often
than the incumbent (see Engine.pick_backend)."""
import json
import threading
import time
from pathlib import Path

import httpx

KINDS = {
    "image-to-video": "视频", "text-to-video": "视频", "text-to-image": "图像", "image-to-image": "图像",
    "text-to-speech": "配音", "text-to-audio": "音效/音乐", "image-to-3d": "3D", "image-segmentation": "抠图",
}
API = "https://huggingface.co/api/models"


def _fits_5090(info):
    """Rough VRAM guess from the safetensors parameter count: BF16 ≈ 2 bytes/param; ≤ 30 GB fits, ≤ 60 GB fits with
    FP8, otherwise only with 4-bit or offload."""
    params = ((info.get("safetensors") or {}).get("total")) or 0
    if not params:
        return "未知", None
    gb = params * 2 / 1e9
    if gb <= 30:
        return "能直接跑", round(gb, 1)
    if gb <= 60:
        return "FP8 能跑", round(gb, 1)
    return "要 4bit/分块", round(gb, 1)


class Scout:
    def __init__(self, path: Path, interval_hours=24):
        self.path = path
        self.interval = interval_hours * 3600
        self.data = json.loads(path.read_text(encoding="utf-8")) if path.exists() else {"seen": {}, "checked": 0}

    def save(self):
        self.path.parent.mkdir(parents=True, exist_ok=True)
        self.path.write_text(json.dumps(self.data, ensure_ascii=False, indent=1), encoding="utf-8")

    def check(self, per_kind=15):
        found = []
        for tag, label in KINDS.items():
            try:
                r = httpx.get(API, params={"pipeline_tag": tag, "sort": "trendingScore", "limit": per_kind,
                                           "expand[]": ["safetensors", "likes", "downloads", "lastModified", "createdAt", "cardData"]},
                              timeout=30)
                r.raise_for_status()
                items = r.json()
            except Exception as e:
                found.append({"error": f"{tag}: {e}"})
                continue
            for m in items:
                mid = m.get("id") or m.get("modelId")
                if not mid:
                    continue
                fit, gb = _fits_5090(m)
                licence = ((m.get("cardData") or {}).get("license")) or "?"
                row = {"id": mid, "kind": label, "tag": tag, "likes": m.get("likes", 0), "downloads": m.get("downloads", 0),
                       "created": m.get("createdAt", ""), "updated": m.get("lastModified", ""), "fit": fit, "weights_gb": gb,
                       "license": licence, "url": f"https://huggingface.co/{mid}"}
                old = self.data["seen"].get(mid)
                row["first_seen"] = old["first_seen"] if old else time.time()
                row["status"] = old.get("status", "new") if old else "new"
                self.data["seen"][mid] = row
                if not old:
                    found.append(row)
        self.data["checked"] = time.time()
        self.save()
        return found

    def mark(self, mid, status):
        if mid in self.data["seen"]:
            self.data["seen"][mid]["status"] = status   # new | watching | trying | adopted | ignored
            self.save()

    def radar(self, limit=60):
        rows = [r for r in self.data["seen"].values() if r.get("status") != "ignored"]
        rows.sort(key=lambda r: (r.get("status") != "new", -r.get("first_seen", 0), -r.get("likes", 0)))
        return rows[:limit]

    def loop(self):
        while True:
            if time.time() - self.data.get("checked", 0) >= self.interval:
                try:
                    self.check()
                except Exception:
                    pass
            time.sleep(600)

    def start(self):
        threading.Thread(target=self.loop, daemon=True).start()
