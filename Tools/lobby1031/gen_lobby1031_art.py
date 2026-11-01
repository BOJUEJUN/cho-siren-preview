#!/usr/bin/env python3
"""2026-10-31 lobby batch (赵金): derived art for the lobby polish.

1. ink-edge-strip-1031.png — the approved R02 brush (StreamingAssets/AlbumInkR02, the green ink behind
   「开始演出」) re-sampled into a 256x(48*32) strip atlas: for each of the 48 frames, the ragged top edge of
   the paint, de-trended so the brush's own bumps (and their frame-to-frame flow) can be wrapped around any
   button silhouette by UIInkEdge1031.shader. Row 0 of a frame = outside the paint, row 31 = solid paint.
   RGB = the brush grain (luminance), A = paint coverage.
2. notice-sticker-1031.png — the torn-paper pop notice sticker (refs/04 cyan torn band): teal paper with a
   torn white paper rim, a hard ink drop shadow, halftone dots and a slanted ink "!".

Run from the repo root:  python3 Tools/lobby1031/gen_lobby1031_art.py
"""
import math
import os
import random

import numpy as np
from PIL import Image, ImageDraw

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Resources", "Art", "Lobby1031")
SHEETS = os.path.join(ROOT, "Assets", "StreamingAssets", "AlbumInkR02")

FRAME_W, FRAME_H = 512, 480
STRIP_W, STRIP_ROWS, FRAMES = 256, 32, 48
X0, X1 = 16, 420          # the frame's ragged top edge (the right tail and the hard left cut are skipped)
REACH = 40.0              # frame pixels sampled above/below the de-trended edge


def load_frames():
    frames = []
    for s in range(4):
        sheet = np.asarray(Image.open(os.path.join(SHEETS, "ink-white-%d.png" % s)).convert("RGBA"), dtype=np.float32)
        for r in range(3):
            for c in range(4):
                frames.append(sheet[r * FRAME_H:(r + 1) * FRAME_H, c * FRAME_W:(c + 1) * FRAME_W])
    return frames


def bilinear(img, x, y):
    h, w = img.shape[:2]
    x = np.clip(x, 0, w - 1.001); y = np.clip(y, 0, h - 1.001)
    x0 = np.floor(x).astype(int); y0 = np.floor(y).astype(int)
    fx = (x - x0)[..., None]; fy = (y - y0)[..., None]
    a = img[y0, x0]; b = img[y0, x0 + 1]; c = img[y0 + 1, x0]; d = img[y0 + 1, x0 + 1]
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy


def ink_strip():
    frames = load_frames()
    tops = []
    for fr in frames:
        alpha = fr[:, :, 3]
        t = []
        for x in range(X0, X1):
            rows = np.where(alpha[:, x] > 128)[0]
            t.append(rows[0] if len(rows) else FRAME_H)
        tops.append(t)
    mean = np.asarray(tops, np.float32).mean(0)
    k = 81
    base = np.convolve(np.pad(mean, (k // 2, k // 2), mode="edge"), np.ones(k) / k, mode="valid")
    out = np.zeros((FRAMES * STRIP_ROWS, STRIP_W, 4), np.float32)
    xs = np.linspace(X0, X1 - 1, STRIP_W)
    bx = np.interp(xs, np.arange(X0, X1), base)
    for f, fr in enumerate(frames):
        rows = np.linspace(-REACH, REACH, STRIP_ROWS)
        gx = np.tile(xs, (STRIP_ROWS, 1))
        gy = bx[None, :] + rows[:, None]
        px = bilinear(fr, gx, gy)
        lum = px[..., :3].mean(-1)
        a = px[..., 3]
        # Grain: normalise the sheet's grey speckle to 0..255 around its paint mean.
        grain = np.clip((lum - 150.0) / (235.0 - 150.0), 0, 1) * 255
        grain = np.where(a > 8, grain, 255)
        block = out[f * STRIP_ROWS:(f + 1) * STRIP_ROWS]
        block[..., 0] = block[..., 1] = block[..., 2] = grain
        block[..., 3] = a
    img = Image.fromarray(np.clip(out, 0, 255).astype(np.uint8), "RGBA")
    img.save(os.path.join(OUT, "ink-edge-strip-1031.png"), optimize=True)


# ------------------------------------------------------------------ notice sticker
SS = 4                     # supersampling
SIZE = 128
TEAL = (30, 232, 232)
TEAL_DARK = (10, 178, 196)
DOTS = (0, 150, 168)
INK = (20, 9, 40)
SHADOW = (14, 6, 26)


def periodic_noise(rng, count, octaves):
    """Sum of random-phase sines over the perimeter: periodic, so the torn edge closes without a seam."""
    waves = []
    for freq, amp in octaves:
        for _ in range(3):
            waves.append((freq * rng.uniform(.8, 1.25), amp * rng.uniform(.5, 1.0), rng.uniform(0, 2 * math.pi)))
    out = []
    for i in range(count):
        t = 2 * math.pi * i / count
        out.append(sum(a * math.sin(round(f) * t + p) for f, a, p in waves))
    return out


def torn_polygon(cx, cy, hw, hh, tilt, rng, rough=1.0, squircle=7.0, count=420):
    """A tilted, slightly rectangular sheet with a torn (fibrous, uneven) rim: long gentle wobble,
    medium rips and fine paper fibre, plus a few sharp nicks."""
    noise = periodic_noise(rng, count, [(3, 1.4), (9, 1.1), (23, .8), (61, .55)])
    fibre = [rng.uniform(-.5, .5) for _ in range(count)]
    nicks = set(rng.sample(range(count), 7))
    pts = []
    for i in range(count):
        t = 2 * math.pi * i / count
        c, s = math.cos(t), math.sin(t)
        r = 1.0 / ((abs(c) / hw) ** squircle + (abs(s) / hh) ** squircle) ** (1 / squircle)
        r += (noise[i] + fibre[i]) * rough
        if i in nicks:
            r -= rng.uniform(2.0, 4.0) * rough
        rt = t + math.radians(tilt)
        pts.append((cx + r * math.cos(rt), cy + r * math.sin(rt)))
    return pts


def sticker():
    rng = random.Random(1031)
    n = SIZE * SS
    S = lambda pts: [(x * SS, y * SS) for x, y in pts]
    cx, cy, tilt = 60.0, 60.0, -8.0
    # The white paper shows unevenly (wider top-left, like the torn band in refs/04).
    paper = torn_polygon(cx - 1.5, cy - 1.5, 46.0, 41.0, tilt - 2.0, rng, rough=1.15)
    teal = torn_polygon(cx + .8, cy + .8, 40.0, 35.5, tilt, rng, rough=.8)

    layer = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    # Hard pop drop shadow (down-right), then the torn white paper and the teal sheet on top.
    d.polygon(S([(x + 4.5, y + 5.5) for x, y in paper]), fill=SHADOW + (235,))
    d.polygon(S(paper), fill=(255, 255, 255, 255))

    # Teal sheet with a diagonal light-to-deep gradient.
    grad = Image.new("RGBA", (n, n))
    g = np.zeros((n, n, 4), np.float32)
    yy, xx = np.mgrid[0:n, 0:n] / n
    k = np.clip((xx * .55 + yy * .75 - .25), 0, 1)[..., None]
    g[..., :3] = np.array(TEAL) * (1 - k * .55) + np.array(TEAL_DARK) * (k * .55)
    g[..., 3] = 255
    grad = Image.fromarray(g.astype(np.uint8), "RGBA")
    mask = Image.new("L", (n, n), 0)
    ImageDraw.Draw(mask).polygon(S(teal), fill=255)
    layer.paste(grad, (0, 0), mask)

    # Halftone: a 45° dot grid growing toward the lower right, clipped to the teal sheet.
    dots = Image.new("L", (n, n), 0)
    dd = ImageDraw.Draw(dots)
    pitch = 6.0
    for i in range(-30, 30):
        for j in range(-30, 30):
            u = (i + j * .5) * pitch
            v = j * pitch * .866
            x, y = cx + u * .707 - v * .707, cy + u * .707 + v * .707
            w = (x - cx) * .6 + (y - cy) * .8
            r = np.clip((w + 8) / 34.0, 0, 1) * 2.1
            if r < .35:
                continue
            dd.ellipse([(x - r) * SS, (y - r) * SS, (x + r) * SS, (y + r) * SS], fill=255)
    dots = Image.fromarray((np.asarray(dots, np.float32) * (np.asarray(mask, np.float32) / 255) * .55).astype(np.uint8))
    layer.paste(Image.new("RGBA", (n, n), DOTS + (255,)), (0, 0), dots)

    # Paper sheen: a soft lighter band across the upper-left corner.
    sheen = Image.new("L", (n, n), 0)
    ImageDraw.Draw(sheen).polygon(S([(18, 40), (44, 16), (52, 22), (24, 50)]), fill=70)
    sheen = Image.fromarray((np.asarray(sheen, np.float32) * (np.asarray(mask, np.float32) / 255)).astype(np.uint8))
    layer.paste(Image.new("RGBA", (n, n), (255, 255, 255, 255)), (0, 0), sheen)

    # Slanted pop "!": a tapered bar and a squared dot, white offset rim under the ink.
    def bang(ox, oy, grow):
        bar = [(55 - grow + ox, 32 - grow + oy), (71 + grow + ox, 32 - grow + oy),
               (63 + grow + ox, 66 + grow + oy), (54 - grow + ox, 66 + grow + oy)]
        dot = [(51 - grow + ox, 72 - grow + oy), (61 + grow + ox, 72 - grow + oy),
               (59 + grow + ox, 82 + grow + oy), (49 - grow + ox, 82 + grow + oy)]
        return bar, dot
    bar, dot = bang(1.6, 1.8, 2.2)
    d.polygon(S(bar), fill=(255, 255, 255, 255)); d.polygon(S(dot), fill=(255, 255, 255, 255))
    bar, dot = bang(0, 0, 0)
    d.polygon(S(bar), fill=INK + (255,)); d.polygon(S(dot), fill=INK + (255,))

    img = layer.resize((SIZE, SIZE), Image.LANCZOS)
    img.save(os.path.join(OUT, "notice-sticker-1031.png"), optimize=True)
    # The web title page (公告) shows the same sticker.
    img.save(os.path.join(ROOT, "Assets", "WebGLTemplates", "ChoSirenPortrait", "icons", "notice-sticker-1031.png"), optimize=True)


def dialogue_disc():
    """The lobby dialogue's spinning record: the project's own vfx-vinyl (CHO-SIREN label), at icon size."""
    src = Image.open(os.path.join(ROOT, "Assets", "Resources", "Art", "Reference038", "vfx-vinyl.png")).convert("RGBA")
    src = src.crop(src.getbbox())
    src.resize((160, 160), Image.LANCZOS).save(os.path.join(OUT, "dialogue-disc-1031.png"), optimize=True)


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    ink_strip()
    sticker()
    dialogue_disc()
    print("wrote", OUT)
