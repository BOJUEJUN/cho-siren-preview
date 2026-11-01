#!/usr/bin/env python3
"""2026-11-01 reward batch (reward-ui): art for the one shared "获得奖励" presentation.

1. hud-diamond/gold/stamina-1101.png — the currency badges cut from the top-HUD pill art itself
   (Art/LobbyPunk/lobby-resource-*-punk-v2), so every reward/sign-in/result icon is literally the HUD icon:
   the gem/coin/heart with its spiky purple shard surround. The pill bar to the right of the badge is
   removed by mirroring the badge's own left silhouette over its right half.
2. reward-card-rim-1101.png — torn white paper card silhouette (tinted in-engine with the quality colour).
3. reward-card-face-1101.png — the ink card face that sits inside the rim: deep violet-to-ink gradient,
   a halftone corner and a paper sheen (neutral, so the rim/glow carry the quality colour).
4. reward-title-1101.png — the 「获得奖励」 band: brand-teal torn paper with a torn white rim, a hard ink
   drop shadow and halftone (the notice-sticker language at banner scale). Text is drawn in-engine.
5. reward-rays-1101.png — soft white light rays for the neon burst behind the cards (tinted, additive).

Run from the repo root:  python3 Tools/reward1101/gen_reward1101_art.py
"""
import math
import os
import random
import re
import uuid

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Resources", "Art", "Reward1101")
PILLS = os.path.join(ROOT, "Assets", "Resources", "Art", "LobbyPunk")
META_TEMPLATE = os.path.join(ROOT, "Assets", "Resources", "Art", "Lobby1031", "notice-sticker-1031.png.meta")

TEAL = (30, 232, 232)
TEAL_DARK = (10, 178, 196)
DOTS = (0, 150, 168)
INK = (14, 8, 20)
SHADOW = (14, 6, 26)
SS = 4


def write_meta(png_path):
    meta = png_path + ".meta"
    if os.path.exists(meta):
        return
    text = open(META_TEMPLATE, encoding="utf-8").read()
    text = re.sub(r"guid: [0-9a-f]+", "guid: " + uuid.uuid4().hex, text, count=1)
    text = re.sub(r"internalIDToNameTable:\n(  - .*\n(    .*\n)*)", "internalIDToNameTable: []\n", text)
    text = re.sub(r"    sprites:\n(    - .*\n(      .*\n)*)", "    sprites: []\n", text)
    open(meta, "w", encoding="utf-8").write(text)


def save(img, name):
    path = os.path.join(OUT, name)
    img.save(path, optimize=True)
    write_meta(path)


# ------------------------------------------------------------------ 1. HUD badges
def hud_badges():
    for f in ("diamond", "gold", "stamina"):
        a0 = Image.open(os.path.join(PILLS, "lobby-resource-%s-punk-v2.png" % f)).convert("RGBA")
        a = Image.new("RGBA", (a0.width + 128, a0.height + 128), (0, 0, 0, 0))
        a.paste(a0, (64, 64))
        arr = np.array(a).astype(float)
        sub = arr[:, :64 + 300]
        sat = sub[..., :3].max(-1) - sub[..., :3].min(-1)
        lum = sub[..., :3].mean(-1) * (sub[..., 3] / 255)
        ys, xs = np.where((lum > 150) & (sat > 60))
        cx = (np.percentile(xs, 3) + np.percentile(xs, 97)) / 2
        cy = (np.percentile(ys, 3) + np.percentile(ys, 97)) / 2
        S = 272
        box = (int(cx - S / 2), int(cy - S / 2), int(cx + S / 2), int(cy + S / 2))
        c = np.array(a.crop(box)).astype(float)
        al = c[..., 3].copy()
        mirrored = al[:, ::-1]
        xx = np.arange(S)[None, :].repeat(S, 0)
        w = np.clip((xx - S // 2 + 10) / 30.0, 0, 1)
        c[..., 3] = al * (1 - w) + np.minimum(al, mirrored) * w
        img = Image.fromarray(c.astype(np.uint8))
        img = img.crop(img.getbbox())
        side = max(img.size)
        sq = Image.new("RGBA", (side, side), (0, 0, 0, 0))
        sq.paste(img, ((side - img.width) // 2, (side - img.height) // 2))
        save(sq.resize((256, 256), Image.LANCZOS), "hud-%s-1101.png" % f)


# ------------------------------------------------------------------ torn paper helpers
def periodic_noise(rng, count, octaves):
    waves = []
    for freq, amp in octaves:
        for _ in range(3):
            waves.append((freq * rng.uniform(.8, 1.25), amp * rng.uniform(.5, 1.0), rng.uniform(0, 2 * math.pi)))
    out = []
    for i in range(count):
        t = 2 * math.pi * i / count
        out.append(sum(a * math.sin(round(f) * t + p) for f, a, p in waves))
    return out


def torn_polygon(cx, cy, hw, hh, tilt, rng, rough=1.0, squircle=9.0, count=720, nicks=9):
    noise = periodic_noise(rng, count, [(3, 1.4), (9, 1.1), (23, .8), (61, .55), (131, .35)])
    fibre = [rng.uniform(-.5, .5) for _ in range(count)]
    cut = set(rng.sample(range(count), nicks))
    pts = []
    for i in range(count):
        t = 2 * math.pi * i / count
        c, s = math.cos(t), math.sin(t)
        r = 1.0 / ((abs(c) / hw) ** squircle + (abs(s) / hh) ** squircle) ** (1 / squircle)
        r += (noise[i] + fibre[i]) * rough
        if i in cut:
            r -= rng.uniform(2.0, 4.5) * rough
        rt = t + math.radians(tilt)
        pts.append((cx + r * math.cos(rt), cy + r * math.sin(rt)))
    return pts


def poly_mask(n_w, n_h, pts):
    m = Image.new("L", (n_w * SS, n_h * SS), 0)
    ImageDraw.Draw(m).polygon([(x * SS, y * SS) for x, y in pts], fill=255)
    return m


def halftone(n_w, n_h, cx, cy, pitch, grow, dirx, diry, offset, rmax):
    dots = Image.new("L", (n_w * SS, n_h * SS), 0)
    dd = ImageDraw.Draw(dots)
    for i in range(-60, 60):
        for j in range(-60, 60):
            u = (i + j * .5) * pitch
            v = j * pitch * .866
            x, y = cx + u * .707 - v * .707, cy + u * .707 + v * .707
            if x < -10 or y < -10 or x > n_w + 10 or y > n_h + 10:
                continue
            w = (x - cx) * dirx + (y - cy) * diry
            r = np.clip((w + offset) / grow, 0, 1) * rmax
            if r < .35:
                continue
            dd.ellipse([(x - r) * SS, (y - r) * SS, (x + r) * SS, (y + r) * SS], fill=255)
    return dots


# ------------------------------------------------------------------ 2/3. card rim + face
CARD_W, CARD_H = 280, 340


def card():
    rng = random.Random(1101)
    cx, cy = CARD_W / 2, CARD_H / 2
    rim = torn_polygon(cx, cy, CARD_W / 2 - 6, CARD_H / 2 - 6, 0, rng, rough=1.6)
    rim_img = Image.new("RGBA", (CARD_W * SS, CARD_H * SS), (255, 255, 255, 0))
    rim_img.putalpha(poly_mask(CARD_W, CARD_H, rim))
    save(rim_img.resize((CARD_W, CARD_H), Image.LANCZOS), "reward-card-rim-1101.png")

    face = torn_polygon(cx + .5, cy + .5, CARD_W / 2 - 17, CARD_H / 2 - 17, 0, random.Random(7), rough=1.1)
    mask = poly_mask(CARD_W, CARD_H, face)
    n_w, n_h = CARD_W * SS, CARD_H * SS
    yy, xx = np.mgrid[0:n_h, 0:n_w]
    k = np.clip(yy / n_h * .9 + xx / n_w * .2, 0, 1)[..., None]
    top = np.array((52, 26, 92), np.float32)       # neon violet
    bottom = np.array((12, 7, 22), np.float32)     # ink
    g = top * (1 - k) + bottom * k
    # a soft light pool behind where the icon sits (upper middle)
    d = np.sqrt(((xx - n_w * .5) / (n_w * .42)) ** 2 + ((yy - n_h * .4) / (n_h * .34)) ** 2)
    pool = np.clip(1 - d, 0, 1)[..., None] ** 1.6
    g = g + pool * np.array((70, 40, 110), np.float32)
    arr = np.zeros((n_h, n_w, 4), np.float32)
    arr[..., :3] = np.clip(g, 0, 255)
    arr[..., 3] = np.asarray(mask, np.float32)
    layer = Image.fromarray(arr.astype(np.uint8), "RGBA")
    # halftone growing to the lower right, white at low alpha
    dots = halftone(CARD_W, CARD_H, cx, cy, 9.0, 120.0, .45, .9, -20, 2.6)
    dots = Image.fromarray((np.asarray(dots, np.float32) * (np.asarray(mask, np.float32) / 255) * .16).astype(np.uint8))
    layer.paste(Image.new("RGBA", layer.size, (255, 255, 255, 255)), (0, 0), dots)
    # diagonal paper sheen across the upper-left
    sheen = Image.new("L", layer.size, 0)
    ImageDraw.Draw(sheen).polygon([(p[0] * SS, p[1] * SS) for p in [(0, 90), (90, 0), (130, 0), (0, 130)]], fill=26)
    sheen = Image.fromarray((np.asarray(sheen, np.float32) * (np.asarray(mask, np.float32) / 255)).astype(np.uint8))
    layer.paste(Image.new("RGBA", layer.size, (255, 255, 255, 255)), (0, 0), sheen)
    save(layer.resize((CARD_W, CARD_H), Image.LANCZOS), "reward-card-face-1101.png")


# ------------------------------------------------------------------ 4. title band
def title():
    W, H = 640, 168
    rng = random.Random(2026)
    cx, cy = W / 2, H / 2
    paper = torn_polygon(cx - 2, cy - 2, W / 2 - 18, H / 2 - 22, -1.2, rng, rough=2.2, squircle=12.0, count=1200, nicks=14)
    teal = torn_polygon(cx + 2, cy + 1, W / 2 - 34, H / 2 - 36, 0, rng, rough=1.6, squircle=12.0, count=1200, nicks=10)
    n = (W * SS, H * SS)
    layer = Image.new("RGBA", n, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    d.polygon([((x + 9) * SS, (y + 10) * SS) for x, y in paper], fill=SHADOW + (235,))
    d.polygon([(x * SS, y * SS) for x, y in paper], fill=(255, 255, 255, 255))
    mask = poly_mask(W, H, teal)
    yy, xx = np.mgrid[0:n[1], 0:n[0]]
    k = np.clip(xx / n[0] * .7 + yy / n[1] * .5 - .15, 0, 1)[..., None]
    g = np.zeros((n[1], n[0], 4), np.float32)
    g[..., :3] = np.array(TEAL) * (1 - k * .5) + np.array(TEAL_DARK) * (k * .5)
    g[..., 3] = 255
    layer.paste(Image.fromarray(g.astype(np.uint8), "RGBA"), (0, 0), mask)
    dots = halftone(W, H, W * .5, H * .5, 8.0, 260.0, .9, .3, -40, 2.8)
    dots = Image.fromarray((np.asarray(dots, np.float32) * (np.asarray(mask, np.float32) / 255) * .5).astype(np.uint8))
    layer.paste(Image.new("RGBA", n, DOTS + (255,)), (0, 0), dots)
    sheen = Image.new("L", n, 0)
    ImageDraw.Draw(sheen).polygon([(p[0] * SS, p[1] * SS) for p in [(40, 120), (140, 30), (190, 30), (90, 120)]], fill=60)
    sheen = Image.fromarray((np.asarray(sheen, np.float32) * (np.asarray(mask, np.float32) / 255)).astype(np.uint8))
    layer.paste(Image.new("RGBA", n, (255, 255, 255, 255)), (0, 0), sheen)
    save(layer.resize((W, H), Image.LANCZOS), "reward-title-1101.png")


# ------------------------------------------------------------------ 5. rays
def rays():
    N = 512
    yy, xx = np.mgrid[0:N, 0:N].astype(np.float32)
    u, v = (xx - N / 2) / (N / 2), (yy - N / 2) / (N / 2)
    r = np.sqrt(u * u + v * v)
    ang = np.arctan2(v, u)
    rng = np.random.RandomState(11)
    beams = np.zeros_like(r)
    for i in range(18):
        a0 = i / 18 * 2 * np.pi + rng.uniform(-.08, .08)
        width = rng.uniform(.05, .11)
        da = np.angle(np.exp(1j * (ang - a0)))
        beams = np.maximum(beams, np.clip(1 - np.abs(da) / width, 0, 1) ** 1.5 * rng.uniform(.55, 1.0))
    fall = np.clip(1 - r, 0, 1) ** 1.3 * np.clip(r / .12, 0, 1)
    core = np.clip(1 - r / .45, 0, 1) ** 2 * .55
    a = np.clip(beams * fall + core, 0, 1)
    img = np.zeros((N, N, 4), np.uint8)
    img[..., :3] = 255
    img[..., 3] = (a * 255).astype(np.uint8)
    save(Image.fromarray(img, "RGBA"), "reward-rays-1101.png")


# ------------------------------------------------------------------ 6. ticket badges (签约券 / 服装券)
def ticket_badges():
    """The HUD badge language for the two tickets: the diamond badge's spiky purple shard surround with the gem
    cut out, and a tilted neon ticket (notched ends, perforation, star stamp, white rim, ink outline) in its place."""
    base = Image.open(os.path.join(OUT, "hud-diamond-1101.png")).convert("RGBA")
    N = base.width
    arr = np.array(base).astype(np.float32)
    yy, xx = np.mgrid[0:N, 0:N].astype(np.float32)
    hole = (((xx - N * .5) / (N * .34)) ** 2 + ((yy - N * .52) / (N * .27)) ** 2) < 1.0
    arr[..., 3] = np.where(hole, 0, arr[..., 3])
    # darken the remaining shards slightly so the ticket reads first
    arr[..., :3] *= .82
    shards = Image.fromarray(arr.astype(np.uint8), "RGBA")
    specs = {
        "recruit": ((255, 214, 92), (255, 92, 176), "star"),
        "costume": ((92, 240, 236), (150, 92, 255), "heart"),
    }
    for name, (c0, c1, mark) in specs.items():
        n = N * SS
        layer = Image.new("RGBA", (n, n), (0, 0, 0, 0))
        tw, th = N * .66, N * .40
        cx, cy = N * .5, N * .52

        def ticket(grow):
            pts = []
            hw, hh = tw / 2 + grow, th / 2 + grow
            notch = th * .16 - grow * .5
            steps = 24
            # top edge, right edge with a semicircle notch, bottom, left notch
            pts += [(-hw, -hh), (hw, -hh)]
            for i in range(steps + 1):
                a = -math.pi / 2 + math.pi * i / steps
                pts.append((hw - notch * math.cos(a), notch * math.sin(a)))
            pts += [(hw, hh), (-hw, hh)]
            for i in range(steps + 1):
                a = math.pi / 2 + math.pi * i / steps
                pts.append((-hw - notch * math.cos(a), notch * math.sin(a)))
            ang = math.radians(-14)
            return [((cx + x * math.cos(ang) - y * math.sin(ang)) * SS, (cy + x * math.sin(ang) + y * math.cos(ang)) * SS) for x, y in pts]

        # neon glow in the ticket's deep colour behind everything
        glow = Image.new("RGBA", (n, n), (0, 0, 0, 0))
        ImageDraw.Draw(glow).polygon(ticket(16), fill=tuple(c1) + (200,))
        layer.alpha_composite(glow.filter(ImageFilter.GaussianBlur(9 * SS)))
        d = ImageDraw.Draw(layer)
        d.polygon([(x + 5 * SS, y + 6 * SS) for x, y in ticket(7)], fill=SHADOW + (240,))
        d.polygon(ticket(7), fill=INK + (255,))
        d.polygon(ticket(4), fill=(255, 255, 255, 255))
        mask = Image.new("L", (n, n), 0)
        ImageDraw.Draw(mask).polygon(ticket(0), fill=255)
        gy, gx = np.mgrid[0:n, 0:n] / n
        k = np.clip(gx * .8 + gy * .4 - .2, 0, 1)[..., None]
        g = np.zeros((n, n, 4), np.float32)
        g[..., :3] = np.array(c0) * (1 - k) + np.array(c1) * k
        g[..., 3] = 255
        layer.paste(Image.fromarray(g.astype(np.uint8), "RGBA"), (0, 0), mask)
        # halftone + sheen inside the ticket
        dots = halftone(N, N, cx, cy, 7.0, 90.0, .6, .8, -10, 2.0)
        dots = Image.fromarray((np.asarray(dots, np.float32) * (np.asarray(mask, np.float32) / 255) * .28).astype(np.uint8))
        layer.paste(Image.new("RGBA", (n, n), (255, 255, 255, 255)), (0, 0), dots)
        # glossy finish: an inner ink line, a bright glare band across the top, a darker lower edge
        inner = Image.new("L", (n, n), 0)
        ImageDraw.Draw(inner).polygon(ticket(-4), fill=255)
        rim_line = np.clip(np.asarray(mask, np.float32) - np.asarray(inner, np.float32), 0, 255) * .35
        layer.paste(Image.new("RGBA", (n, n), INK + (255,)), (0, 0), Image.fromarray(rim_line.astype(np.uint8)))
        glare = Image.new("L", (n, n), 0)
        ang0 = math.radians(-14)
        gpts = [(-tw * .48, -th * .42), (tw * .48, -th * .42), (tw * .48, -th * .12), (-tw * .48, -th * .02)]
        ImageDraw.Draw(glare).polygon([((cx + x * math.cos(ang0) - y * math.sin(ang0)) * SS, (cy + x * math.sin(ang0) + y * math.cos(ang0)) * SS) for x, y in gpts], fill=90)
        glare = glare.filter(ImageFilter.GaussianBlur(2 * SS))
        glare = Image.fromarray((np.asarray(glare, np.float32) * (np.asarray(inner, np.float32) / 255)).astype(np.uint8))
        layer.paste(Image.new("RGBA", (n, n), (255, 255, 255, 255)), (0, 0), glare)
        # perforation line (stub on the right third)
        ang = math.radians(-14)
        px = tw * .2
        for i in range(-5, 6):
            y = i * th / 12.5
            x0, y0 = cx + px * math.cos(ang) - y * math.sin(ang), cy + px * math.sin(ang) + y * math.cos(ang)
            r = 2.3
            d.ellipse([(x0 - r) * SS, (y0 - r) * SS, (x0 + r) * SS, (y0 + r) * SS], fill=INK + (200,))
        # the stamp mark on the main part
        mx, my = cx - tw * .14, cy + tw * .035
        if mark == "star":
            pts = []
            for i in range(10):
                a = -math.pi / 2 + i * math.pi / 5 + ang
                r = (th * .3) if i % 2 == 0 else (th * .13)
                pts.append(((mx + r * math.cos(a)) * SS, (my + r * math.sin(a)) * SS))
        else:
            pts = []
            for i in range(80):
                t = 2 * math.pi * i / 80
                x = 16 * math.sin(t) ** 3
                y = -(13 * math.cos(t) - 5 * math.cos(2 * t) - 2 * math.cos(3 * t) - math.cos(4 * t))
                s = th * .3 / 17
                rx, ry = x * s, y * s
                pts.append(((mx + rx * math.cos(ang) - ry * math.sin(ang)) * SS, (my + rx * math.sin(ang) + ry * math.cos(ang)) * SS))
        d.polygon([(x + 2.5 * SS, y + 3 * SS) for x, y in pts], fill=INK + (255,))
        d.polygon(pts, fill=(255, 255, 255, 255))
        # two four-point sparkles, like the glints on the HUD gem
        for (sx, sy, sr) in ((cx + tw * .42, cy - th * .62, 13), (cx - tw * .5, cy + th * .38, 8)):
            spts = []
            for i in range(8):
                a = i * math.pi / 4
                r = sr if i % 2 == 0 else sr * .22
                spts.append(((sx + r * math.cos(a)) * SS, (sy + r * math.sin(a)) * SS))
            d.polygon(spts, fill=(255, 255, 255, 245))
        out = shards.copy()
        out.alpha_composite(layer.resize((N, N), Image.LANCZOS))
        save(out, "ticket-%s-1101.png" % name)


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    hud_badges()
    card()
    title()
    rays()
    ticket_badges()
    print("wrote", sorted(f for f in os.listdir(OUT) if f.endswith(".png")))
