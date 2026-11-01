#!/usr/bin/env python3
"""Quick look at the damage atlas, tinted the way BigDamageNumbers1101 tints it (no Unity needed)."""
import json
import os
import sys

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(ROOT, "..", ".."))
D = os.path.join(REPO, "Assets", "Resources", "UI", "DamageFont1101")
atlas = Image.open(os.path.join(D, "damage-digits.png")).convert("RGBA")
meta = json.load(open(os.path.join(D, "damage-digits.json")))
G = {g["c"]: g for g in meta["glyphs"]}

VARIANTS = {
    "gold": ((255, 250, 196), (255, 214, 40), (255, 112, 10), (150, 40, 0)),
    "crit": ((255, 255, 255), (255, 236, 90), (255, 70, 40), (190, 0, 60)),
    "skill": ((255, 255, 255), (150, 248, 255), (40, 140, 255), (20, 40, 150)),
}


def tint_back(cell, rim):
    a = np.asarray(cell).astype(np.float32) / 255
    out = a.copy()
    out[..., :3] = a[..., :3] * np.array(rim) / 255
    return Image.fromarray((out * 255).astype(np.uint8), "RGBA")


def tint_front(cell, g, top, mid, bot):
    a = np.asarray(cell).astype(np.float32) / 255
    h = a.shape[0]
    ys = np.arange(h)[:, None]
    t0, t1 = g["capTop"], g["baseline"]
    k = np.clip((ys - t0) / max(1, t1 - t0), 0, 1)
    top, mid, bot = [np.array(c, np.float32) / 255 for c in (top, mid, bot)]
    col = np.where(k < .5, top + (mid - top) * (k / .5), mid + (bot - mid) * ((k - .5) / .5))
    out = a.copy()
    out[..., :3] = a[..., :3] * col[:, None, :].reshape(h, 1, 3)
    return Image.fromarray((np.clip(out, 0, 1) * 255).astype(np.uint8), "RGBA")


def compose(text, variant, scale=1.0):
    top, mid, bot, rim = VARIANTS[variant]
    pen = 0
    places = []
    for c in ([text] if text in G else text):
        g = G[c]
        places.append((g, pen - g["originX"]))
        pen += g["advance"]
    minx = min(p for _, p in places)
    maxx = max(p + g["w"] for g, p in places)
    H = max(g["h"] for g, _ in places) + 20
    canvas = Image.new("RGBA", (int(maxx - minx) + 4, H + 40), (0, 0, 0, 0))
    base = 20 + max(g["baseline"] for g, _ in places)
    for g, p in places:
        back = atlas.crop((g["x"], g["y"], g["x"] + g["w"], g["y"] + g["h"]))
        canvas.alpha_composite(tint_back(back, rim), (int(p - minx), int(base - g["baseline"])))
    for g, p in places:
        front = atlas.crop((g["fx"], g["y"], g["fx"] + g["w"], g["y"] + g["h"]))
        canvas.alpha_composite(tint_front(front, g, top, mid, bot), (int(p - minx), int(base - g["baseline"])))
    if scale != 1:
        canvas = canvas.resize((int(canvas.width * scale), int(canvas.height * scale)), Image.BILINEAR)
    return canvas


if __name__ == "__main__":
    bg = Image.new("RGBA", (1100, 900), (70, 40, 110, 255))
    y = 10
    for text, v in (("15,009", "gold"), ("4,645", "crit"), ("3,460", "skill"), ("+128", "gold")):
        im = compose(text, v)
        bg.alpha_composite(im, (20, y))
        y += im.height - 30
    crit = compose("CRITICAL", "crit")
    bg.alpha_composite(crit, (620, 20))
    out = sys.argv[1] if len(sys.argv) > 1 else os.path.join(REPO, "Builds/qa-logs/zj-1101/battle-fx/tmp/digits-preview.png")
    bg.convert("RGB").save(out)
    print(out)
