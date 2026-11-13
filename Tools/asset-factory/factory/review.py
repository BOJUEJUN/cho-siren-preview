"""Automatic review: hard numeric checks first (cheap, objective), then an AI reviewer that looks at a contact sheet
next to the reference art and answers with JSON. The AI is any OpenAI-compatible chat endpoint that takes images:
a local VLM on the 5090 (vLLM / Ollama / LM Studio serving Qwen-VL class models) or a cloud model.
"""
import base64
import json
import re
from pathlib import Path

import cv2
import numpy as np
import httpx


def _frames(path: Path, count=6):
    cap = cv2.VideoCapture(str(path))
    n = int(cap.get(cv2.CAP_PROP_FRAME_COUNT)) or 1
    fps = cap.get(cv2.CAP_PROP_FPS) or 24
    w, h = int(cap.get(3)), int(cap.get(4))
    picks = sorted(set(int(round(i)) for i in np.linspace(0, n - 1, count)))
    frames = []
    for k in picks:
        cap.set(cv2.CAP_PROP_POS_FRAMES, k)
        ok, f = cap.read()
        if ok:
            frames.append(f)
    # whole-clip stats on a small proxy
    cap.set(cv2.CAP_PROP_POS_FRAMES, 0)
    small, means = [], []
    while True:
        ok, f = cap.read()
        if not ok:
            break
        g = cv2.cvtColor(cv2.resize(f, (96, int(96 * h / max(1, w)))), cv2.COLOR_BGR2GRAY).astype(np.float32)
        small.append(g)
        means.append(float(g.mean()))
    cap.release()
    return {"frames": frames, "small": small, "means": means, "n": n, "fps": fps, "w": w, "h": h}


def numeric_checks(path: Path, spec: dict):
    """Returns (ok, details). spec keys come from the brief's `checks`."""
    path = Path(path)
    out, problems = {}, []
    if path.suffix.lower() in (".png", ".jpg", ".jpeg", ".webp"):
        img = cv2.imread(str(path), cv2.IMREAD_UNCHANGED)
        if img is None:
            return False, {"problems": ["图片打不开"]}
        out.update(w=img.shape[1], h=img.shape[0])
        frames = [img[:, :, :3]]
    else:
        info = _frames(path)
        if not info["frames"]:
            return False, {"problems": ["视频打不开"]}
        out.update(w=info["w"], h=info["h"], frames=info["n"], fps=round(info["fps"], 2),
                   seconds=round(info["n"] / max(1, info["fps"]), 2))
        frames = info["frames"]
        small = info["small"]
        if len(small) > 2:
            motion = float(np.mean([np.abs(small[i + 1] - small[i]).mean() for i in range(len(small) - 1)]))
            out["motion"] = round(motion, 2)
            lo, hi = spec.get("motion", [0.4, 18])
            if motion < lo:
                problems.append(f"几乎不动（运动量 {motion:.2f} < {lo}）")
            if motion > hi:
                problems.append(f"动得太乱（运动量 {motion:.2f} > {hi}）")
            flicker = float(np.std(np.diff(info["means"])))
            out["flicker"] = round(flicker, 2)
            if flicker > spec.get("flicker_max", 4.0):
                problems.append(f"亮度闪烁（{flicker:.2f}）")
            if spec.get("loop"):
                seam = float(np.abs(small[0] - small[-1]).mean())
                out["loop_seam"] = round(seam, 2)
                if seam > spec.get("loop_seam_max", 6.0):
                    problems.append(f"首尾接不上，循环会跳（差 {seam:.2f}）")
        sec = out["seconds"]
        if "seconds" in spec:
            lo, hi = spec["seconds"]
            if not (lo <= sec <= hi):
                problems.append(f"时长 {sec}s 不在 {lo}-{hi}s")
    if "min_height" in spec and out["h"] < spec["min_height"]:
        problems.append(f"分辨率太低（高 {out['h']} < {spec['min_height']}）")
    if "aspect" in spec:
        want = spec["aspect"][0] / spec["aspect"][1]
        got = out["w"] / max(1, out["h"])
        if abs(got - want) / want > .03:
            problems.append(f"画面比例 {got:.3f} 不是 {want:.3f}")
    if spec.get("clean_background"):
        # The border should be plain background (white or the brief's colour) so it can be cut out.
        bg = np.array(spec.get("background_bgr", [255, 255, 255]), np.float32)
        worst = 0.0
        for f in frames:
            border = np.concatenate([f[:6].reshape(-1, 3), f[-6:].reshape(-1, 3), f[:, :6].reshape(-1, 3), f[:, -6:].reshape(-1, 3)])
            off = float((np.abs(border.astype(np.float32) - bg).max(axis=1) > 40).mean())
            worst = max(worst, off)
        out["border_dirty"] = round(worst, 3)
        if worst > spec.get("border_dirty_max", .08):
            problems.append(f"背景不干净，抠不出来（边缘杂色 {worst:.0%}）")
    out["problems"] = problems
    return len(problems) == 0, out


def contact_sheet(path: Path, reference: Path, dest: Path, count=6):
    """Reference art on the left, sampled frames to the right, one row, height 512."""
    path = Path(path)
    tiles = []
    if reference and Path(reference).exists():
        ref = cv2.imread(str(reference), cv2.IMREAD_UNCHANGED)
        if ref is not None and ref.ndim == 3 and ref.shape[2] == 4:
            a = ref[:, :, 3:4] / 255.0
            ref = (ref[:, :, :3] * a + 128 * (1 - a)).astype(np.uint8)
        if ref is not None:
            tiles.append(ref)
    if path.suffix.lower() in (".png", ".jpg", ".jpeg", ".webp"):
        tiles.append(cv2.imread(str(path)))
    else:
        tiles.extend(_frames(path, count)["frames"])
    h = 512
    row = [cv2.resize(t, (max(1, int(t.shape[1] * h / t.shape[0])), h)) for t in tiles if t is not None]
    sheet = np.hstack(row)
    if sheet.shape[1] > 3600:
        sheet = cv2.resize(sheet, (3600, int(sheet.shape[0] * 3600 / sheet.shape[1])))
    cv2.imwrite(str(dest), sheet, [cv2.IMWRITE_JPEG_QUALITY, 88])
    return dest


REVIEW_SYSTEM = (
    "你是手游美术资源的验收员。左边第一张是参考原画，后面是生成结果按时间顺序抽的帧。"
    "按给出的验收标准逐条检查，只回答一个 JSON 对象，不要多余文字："
    '{"pass": true/false, "score": 0-100, "problems": ["中文短句", ...], "fix_prompt": "给生成模型的修改后完整英文提示词"}'
)


def ai_review(cfg: dict, sheet: Path, rubric: str, prompt: str):
    """Returns a dict {pass, score, problems, fix_prompt} or None when no reviewer is configured."""
    rv = cfg.get("reviewer") or {}
    if not rv.get("base_url"):
        return None
    b64 = base64.b64encode(Path(sheet).read_bytes()).decode()
    body = {
        "model": rv.get("model", "qwen-vl"),
        "temperature": 0.2,
        "messages": [
            {"role": "system", "content": REVIEW_SYSTEM},
            {"role": "user", "content": [
                {"type": "text", "text": f"验收标准：\n{rubric}\n\n这次用的提示词：\n{prompt}"},
                {"type": "image_url", "image_url": {"url": f"data:image/jpeg;base64,{b64}"}},
            ]},
        ],
    }
    headers = {}
    if rv.get("api_key_env"):
        import os
        key = os.environ.get(rv["api_key_env"], "")
        if key:
            headers["Authorization"] = f"Bearer {key}"
    r = httpx.post(rv["base_url"].rstrip("/") + "/chat/completions", json=body, headers=headers, timeout=rv.get("timeout", 300))
    r.raise_for_status()
    text = r.json()["choices"][0]["message"]["content"]
    m = re.search(r"\{.*\}", text, re.S)
    if not m:
        return {"pass": False, "score": 0, "problems": ["验收模型没有返回 JSON"], "fix_prompt": prompt, "raw": text[:500]}
    try:
        data = json.loads(m.group(0))
    except json.JSONDecodeError:
        return {"pass": False, "score": 0, "problems": ["验收模型的 JSON 解析失败"], "fix_prompt": prompt, "raw": text[:500]}
    data.setdefault("problems", [])
    data.setdefault("fix_prompt", prompt)
    data["score"] = float(data.get("score", 0) or 0)
    data["pass"] = bool(data.get("pass")) and data["score"] >= rv.get("pass_score", 75)
    return data
