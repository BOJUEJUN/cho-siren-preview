#!/usr/bin/env python3
"""
2026-11-01 battle-fx (赵金: 伤害数字要像《天命之子》那么大、那么爽).

Pre-renders the big damage-number glyph atlas used by ChoSiren.BigDamageNumbers1101.

Font: Anton Regular (SIL Open Font License 1.1, https://github.com/googlefonts/AntonFont,
downloaded from https://github.com/google/fonts/tree/main/ofl/anton). Only the glyphs
0-9 , + and the word CRITICAL are rasterised; the licence travels with the atlas
(Assets/Resources/UI/DamageFont1101/OFL-Anton.txt).

Each glyph is baked in TWO aligned layers so the runtime can draw every outline of a number first
and every face on top (outlines of neighbouring digits merge instead of cutting into each other):
  back  = soft drop shadow + 3D extrusion + thick near-black outline + a coloured inner rim
  front = the face, white with a soft inner bevel
Both are grey-scale; the runtime tints them per vertex (face: 3-stop gradient, back: rim colour),
so one atlas serves the gold, critical and skill-blue variants.

Usage: python3 Tools/damage1101/make_damage_digits.py
"""
import json
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter
from scipy.ndimage import distance_transform_edt

ROOT = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(ROOT, "..", ".."))
FONT = os.path.join(ROOT, "fonts", "Anton-Regular.ttf")
OUT_DIR = os.path.join(REPO, "Assets", "Resources", "UI", "DamageFont1101")

SS = 3              # supersampling
CAP = 150           # final cap (digit) height in atlas pixels
SHEAR = .2          # italic lean
RIM = 4.5           # coloured inner rim width (final px)
OUTLINE = 12.5      # total outline radius (final px)
EXTRUDE = 10        # 3D extrusion depth (final px)
EXTRUDE_DIR = (.42, 1.0)
SHADOW_OFF = (6, 13)
SHADOW_BLUR = 6
TRACK = .9          # tighten the font's advance (heavy outlines overlap)
ATLAS = 1024


def smooth(dist_inside_radius):
    return np.clip(dist_inside_radius + .5, 0, 1)


def render_layers(text, cap):
    """Returns (back RGBA, front RGBA, meta) at final resolution for a string drawn as one glyph run."""
    font_size = 10
    probe = ImageFont.truetype(FONT, 100)
    box = probe.getbbox("0")
    digit_h = box[3] - box[1]
    font_size = int(round(100 * cap * SS / digit_h))
    font = ImageFont.truetype(FONT, font_size)
    pad = int((OUTLINE + EXTRUDE + SHADOW_BLUR * 2 + max(SHADOW_OFF) + 6) * SS)
    shear_room = int(cap * SS * SHEAR) + 4
    width = int(font.getlength(text)) + pad * 2 + shear_room
    ascent, descent = font.getmetrics()
    height = ascent + descent + pad * 2
    img = Image.new("L", (width, height), 0)
    d = ImageDraw.Draw(img)
    x0, y0 = pad, pad
    d.text((x0, y0), text, font=font, fill=255)
    # Baseline and cap top in this canvas.
    zero = font.getbbox("0")
    baseline = y0 + zero[3]
    cap_top = y0 + zero[1]
    # Italic: shear around the baseline (tops lean right).
    img = img.transform(img.size, Image.AFFINE, (1, SHEAR, -SHEAR * baseline, 0, 1, 0), resample=Image.BICUBIC)
    m = np.asarray(img).astype(np.float32) / 255.0
    solid = m > .5
    outside = distance_transform_edt(~solid)
    inside = distance_transform_edt(solid)

    rim = smooth(RIM * SS - outside)
    outer = smooth(OUTLINE * SS - outside)
    # Extrusion: the outline silhouette swept down-right.
    ex = np.zeros_like(outer)
    dx, dy = EXTRUDE_DIR
    n = np.hypot(dx, dy)
    dx, dy = dx / n, dy / n
    steps = int(EXTRUDE * SS)
    for k in range(1, steps + 1):
        sx, sy = int(round(dx * k)), int(round(dy * k))
        shifted = np.zeros_like(outer)
        shifted[sy:, sx:] = outer[:outer.shape[0] - sy, :outer.shape[1] - sx]
        ex = np.maximum(ex, shifted)
    ex_edge = smooth(2.0 * SS - distance_transform_edt(ex < .5))  # a thin dark line around the extrusion
    body = np.maximum(outer, ex)
    shadow_img = Image.fromarray((body * 255).astype(np.uint8))
    shadow_img = shadow_img.transform(shadow_img.size, Image.AFFINE,
                                      (1, 0, -SHADOW_OFF[0] * SS, 0, 1, -SHADOW_OFF[1] * SS))
    shadow_img = shadow_img.filter(ImageFilter.GaussianBlur(SHADOW_BLUR * SS))
    shadow = np.asarray(shadow_img).astype(np.float32) / 255.0 * .62

    # Back layer, composited bottom to top as (lum, alpha).
    lum = np.zeros_like(m)
    alpha = np.zeros_like(m)

    def over(l, a):
        nonlocal lum, alpha
        out_a = a + alpha * (1 - a)
        safe = np.where(out_a > 1e-5, out_a, 1)
        lum = np.where(out_a > 1e-5, (l * a + lum * alpha * (1 - a)) / safe, 0)
        alpha = out_a

    over(np.zeros_like(m), shadow)
    over(np.full_like(m, .03), ex_edge)
    # Extrusion side: dark, slightly lighter near the letter so it reads as a bevelled block.
    ex_shade = .30 + .18 * np.clip(1 - outside / (OUTLINE * SS + steps), 0, 1)
    over(ex_shade, ex)
    over(np.full_like(m, .06), outer)
    over(np.full_like(m, 1.0), rim)

    # Front layer: the face with a soft inner bevel (darker along the lower-right inner edge).
    face_a = smooth(inside - .0) * 0 + np.clip(m * 1.0, 0, 1)
    band = np.clip(1 - inside / (5.0 * SS), 0, 1)
    gy, gx = np.gradient(inside)
    lower_right = np.clip((gx * -1 + gy * -1) * .7 + .5, 0, 1)  # inner edges facing down/right
    face_l = 1.0 - .22 * band * lower_right - .05 * band
    # A thin glossy rim highlight along the upper-left inner edge.
    upper_left = 1 - lower_right
    face_l = np.clip(face_l + .0 * upper_left, 0, 1)

    def to_img(l, a):
        rgba = np.zeros(m.shape + (4,), np.float32)
        rgba[..., 0] = rgba[..., 1] = rgba[..., 2] = l
        rgba[..., 3] = a
        # premultiply for an honest box downsample, then un-premultiply
        rgba[..., :3] *= rgba[..., 3:4]
        im = Image.fromarray((np.clip(rgba, 0, 1) * 255).astype(np.uint8), "RGBA")
        small = im.resize((im.width // SS, im.height // SS), Image.BOX)
        arr = np.asarray(small).astype(np.float32)
        a2 = arr[..., 3:4] / 255.0
        arr[..., :3] = np.where(a2 > 1e-4, arr[..., :3] / np.maximum(a2, 1e-4), 0)
        return Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGBA")

    back = to_img(lum, alpha)
    front = to_img(face_l, face_a)
    # Trim to the back layer's coverage (it contains everything).
    bbox = back.getchannel("A").point(lambda v: 255 if v > 2 else 0).getbbox()
    back = back.crop(bbox)
    front = front.crop(bbox)
    meta = {
        "originX": (x0 / SS) - bbox[0],                 # pen origin, px from the cell's left
        "baseline": (baseline / SS) - bbox[1],          # px from the cell's top
        "capTop": (cap_top / SS) - bbox[1],
        "advance": font.getlength(text) / SS * TRACK,
        "w": back.width, "h": back.height,
    }
    return back, front, meta


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    items = []
    for c in "0123456789,+":
        b, f, meta = render_layers(c, CAP)
        meta["c"] = c
        items.append((b, f, meta))
    b, f, meta = render_layers("CRITICAL", 58)
    meta["c"] = "CRITICAL"
    items.append((b, f, meta))

    # Shelf-pack back and front cells side by side (same size, so the runtime only needs one offset).
    atlas = Image.new("RGBA", (ATLAS, ATLAS), (0, 0, 0, 0))
    x = y = 2
    shelf = 0
    out = []
    for b, f, meta in items:
        w2 = b.width * 2 + 4
        if x + w2 > ATLAS:
            x = 2
            y += shelf + 2
            shelf = 0
        if y + b.height > ATLAS:
            raise SystemExit("atlas overflow at %s (y=%d)" % (meta["c"], y))
        atlas.paste(b, (x, y))
        atlas.paste(f, (x + b.width + 2, y))
        meta["x"], meta["y"] = x, y
        meta["fx"] = x + b.width + 2
        out.append(meta)
        x += w2 + 2
        shelf = max(shelf, b.height)
    # Soft round glow for criticals / the impact flash (bottom-right corner).
    g = 120
    yy, xx = np.mgrid[0:g, 0:g]
    r = np.hypot(xx - g / 2 + .5, yy - g / 2 + .5) / (g / 2)
    ga = np.clip(1 - r, 0, 1) ** 2.2
    glow = np.zeros((g, g, 4), np.uint8)
    glow[..., :3] = 255
    glow[..., 3] = (ga * 255).astype(np.uint8)
    gx0, gy0 = ATLAS - g - 2, ATLAS - g - 2
    if y + shelf + 2 > gy0 and x > gx0 - 2:
        raise SystemExit("glow overlaps glyphs")
    atlas.paste(Image.fromarray(glow, "RGBA"), (gx0, gy0))
    used_h = y + shelf + 2
    data = {"atlas": ATLAS, "cap": CAP, "glow": {"x": gx0, "y": gy0, "w": g, "h": g}, "glyphs": out}
    atlas.save(os.path.join(OUT_DIR, "damage-digits.png"), optimize=True)
    with open(os.path.join(OUT_DIR, "damage-digits.json"), "w") as fh:
        json.dump(data, fh, indent=1)
    print("atlas rows used:", used_h, "glyphs:", [(m["c"], m["w"], m["h"]) for m in out])


if __name__ == "__main__":
    main()
