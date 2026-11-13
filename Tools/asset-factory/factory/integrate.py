"""After 少爷 accepts a result (少爷: "我点击验收之后就会合入游戏"), put it into the game checkout.

Each brief says how in `integrate`:
  {type: ultimate}                     → StreamingAssets/UltimateVideos/<target>-ult-f<job>.mp4 + a manifest entry
                                          (several clips per member play at random; works without code changes)
  {type: file, path: "Assets/…/{id}.png"} → copied to that path (placeholders from the target), replacing the old file
  {type: inbox} (default)              → Tools/asset-factory/game-inbox/<brief>/ with a note; needs code (e.g. a new
                                          lobby idol) — Claude / Codex wire it up and clear the inbox
With `integrate_commit: true` in config.yaml every integration is committed on the current branch of the game checkout
(never pushed); releasing still goes through the normal release flow.
"""
import json
import shutil
import subprocess
import time
from pathlib import Path


def _clip_seconds(path: Path):
    try:
        import cv2
        cap = cv2.VideoCapture(str(path))
        n, fps = cap.get(cv2.CAP_PROP_FRAME_COUNT), cap.get(cv2.CAP_PROP_FPS) or 24
        cap.release()
        return round(n / fps, 4) if n else 4.0
    except Exception:
        return 4.0


def integrate(game: Path, root: Path, brief: dict, job: dict, src: Path, cfg: dict):
    spec = brief.get("integrate") or {"type": "inbox"}
    target = json.loads(job["params"])
    kind = spec.get("type", "inbox")
    changed = []
    if kind == "ultimate":
        folder = game / "Assets/StreamingAssets/UltimateVideos"
        name = f"{job['target']}-ult-f{job['id']}{src.suffix.lower()}"
        shutil.copy2(src, folder / name)
        changed.append(folder / name)
        manifest_path = game / "Assets/Resources/UltimateVideos/manifest.json"
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        manifest["entries"] = [e for e in manifest.get("entries", []) if e.get("path") != name]
        manifest["entries"].append({"characterId": target.get("id", job["target"]), "path": name, "approvedOriginalArt": True,
                                    "durationSeconds": _clip_seconds(src)})
        manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        changed.append(manifest_path)
        where = f"大招动画已加入（{name}），和这个角色已有的大招随机播放"
    elif kind == "file":
        dest = game / spec["path"].format(**target)
        dest.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(src, dest)
        changed.append(dest)
        where = f"已放到 {spec['path'].format(**target)}"
    else:
        inbox = root / "game-inbox" / brief["id"]
        inbox.mkdir(parents=True, exist_ok=True)
        dest = inbox / f"{job['target']}-job{job['id']}{src.suffix}"
        shutil.copy2(src, dest)
        (inbox / (dest.stem + ".json")).write_text(json.dumps(
            {"brief": brief["id"], "title": brief.get("title"), "target": target, "file": dest.name, "accepted": time.time(),
             "how": spec.get("how", "需要接代码：交给 Claude / Codex 按 Docs/ASSET-FACTORY-5090.md 第 7 节接入")},
            ensure_ascii=False, indent=1), encoding="utf-8")
        return {"where": "已进待接入清单（game-inbox），要改代码的资源由 Claude / Codex 接入", "files": [str(dest)], "committed": False}
    committed = False
    if cfg.get("integrate_commit"):
        try:
            rel = [str(p.relative_to(game)) for p in changed]
            subprocess.run(["git", "-C", str(game), "add", "--", *rel], check=True, capture_output=True)
            subprocess.run(["git", "-C", str(game), "commit", "-m", f"factory: {brief.get('title', brief['id'])} · {job['target']} (job {job['id']})",
                            "--", *rel], check=True, capture_output=True)
            committed = True
        except Exception as e:
            where += f"（提交失败：{e}）"
    return {"where": where, "files": [str(p) for p in changed], "committed": committed}
