"""Mock-up frames for the VFX research page (concept only, not game assets)."""
import sys, math
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter

OUT = sys.argv[1]
S = 300
rng = np.random.default_rng(7)
yy, xx = np.mgrid[0:S, 0:S].astype(np.float32)
U = (xx - S / 2) / (S / 2)
V = (S / 2 - yy) / (S / 2)
R = np.sqrt(U * U + V * V)
A = np.arctan2(V, U)


def stage():
    """Dark purple stage with a placeholder enemy card."""
    g = np.clip(1 - R * .6, 0, 1)[..., None]
    img = np.array([22, 14, 38], np.float32) * (1 - g) + np.array([60, 34, 92], np.float32) * g
    card = (np.abs(U) < .32) & (np.abs(V + .05) < .45)
    img[card] = [92, 70, 128]
    edge = card & ~((np.abs(U) < .30) & (np.abs(V + .05) < .43))
    img[edge] = [200, 180, 230]
    return img / 255.0


def add(img, mask, color, k=1.0):
    return np.clip(img + mask[..., None] * np.array(color, np.float32) * k, 0, 1)


def over(img, mask, color):
    m = np.clip(mask, 0, 1)[..., None]
    return img * (1 - m) + np.array(color, np.float32) * m


def save(img, name):
    Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8)).save(f"{OUT}/{name}.png")


def ramp(x, stops):
    """Gradient map: stops = [(pos, (r,g,b)), ...] in 0..1."""
    x = np.clip(x, 0, 1)
    out = np.zeros(x.shape + (3,), np.float32)
    for (p0, c0), (p1, c1) in zip(stops, stops[1:]):
        m = (x >= p0) & (x <= p1)
        t = ((x - p0) / max(p1 - p0, 1e-5))[m][..., None]
        out[m] = np.array(c0) * (1 - t) + np.array(c1) * t
    return out


def noise(scale, seed):
    r = np.random.default_rng(seed)
    acc = np.zeros((S, S), np.float32); amp = 1; tot = 0
    for o in range(5):
        n = max(2, int(scale * 2 ** o))
        g = r.random((n + 1, n + 1)).astype(np.float32)
        im = Image.fromarray((g * 255).astype(np.uint8)).resize((S, S), Image.BICUBIC)
        acc += np.asarray(im, np.float32) / 255 * amp; tot += amp; amp *= .5
    return acc / tot


TINT = (1.0, .35, .55)  # pink
DEEP = (.45, .02, .25)

# ---------- current style (approximation of ImpactBurst1021) ----------
def current(t):
    img = stage()
    core = np.clip(1 - R / (.5 * .4 + .5 * .4 * min(t / .14, 1)), 0, 1) ** 2.2 * max(0, 1 - t / .14)
    glow = np.clip(1 - R / (.6 + .5 * min(t / .3, 1)), 0, 1) ** 2.2 * .9 * max(0, 1 - t / .3)
    p = min(t / .34, 1); rr = .2 + .9 * (1 - (1 - p) ** 3)
    ring = np.exp(-((R - rr) / .04) ** 2) * (1 - p) ** 1.4
    img = add(img, glow, TINT); img = add(img, ring, (1, .7, .8)); img = add(img, core, (1, 1, 1))
    for i in range(7):
        a = i * .9 + .3; d = .3 + .5 * (1 - (1 - min(t / .3, 1)) ** 3)
        cx, cy = math.cos(a) * d, math.sin(a) * d
        du, dv = U - cx, V - cy
        along = du * math.cos(a) + dv * math.sin(a); across = -du * math.sin(a) + dv * math.cos(a)
        sp = np.clip(1 - np.abs(along) / .12, 0, 1) * np.clip(1 - np.abs(across) / .02, 0, 1)
        img = add(img, sp * max(0, 1 - t / .36), (1, .8, .9))
    return img


# ---------- proposed anime / P5 hit ----------
def star_mask(r_in, r_out, spikes, seed, rot=0.0):
    r = np.random.default_rng(seed)
    jag = r.random(spikes) * .5 + .5
    sector = ((A + rot) / (2 * math.pi) + 1) * spikes
    i = np.floor(sector).astype(int) % spikes; f = sector - np.floor(sector)
    rad = r_in + (r_out * jag[i] - r_in) * (1 - np.abs(f * 2 - 1))
    return np.clip((rad - R) * 120, 0, 1)


def proposed(t, inverted=False):
    img = stage()
    if inverted:  # 1-2 frame impact frame: hard black/white, red accent
        img = np.ones_like(img) * .96
        card = (np.abs(U) < .32) & (np.abs(V + .05) < .45)
        img[card] = 0.02
        s = star_mask(.18, 1.1, 13, 3)
        img = over(img, s, (0.02, 0.02, 0.02))
        img = over(img, star_mask(.08, .55, 9, 5), (.9, .05, .15))
        # speed lines
        lines = (np.sin(A * 46) > .93) & (R > .55)
        img[lines] = 0.0
        return img
    # cel shock ring: thick -> thin, hard edges
    p = min(t / .25, 1); rr = .15 + .95 * (1 - (1 - p) ** 3); w = .14 * (1 - p) + .01
    ring = (np.abs(R - rr) < w).astype(np.float32) * (1 if p < 1 else 0)
    broken = noise(3, 11) > .35 + .4 * p
    img = over(img, ring * broken, (1, .82, .9))
    # smoke (2-tone cel) with dissolve
    if t > .06:
        q = min((t - .06) / .5, 1)
        n = noise(4, 21)
        field = np.clip(1 - R / (.35 + .45 * q), 0, 1) * (n * 1.2)
        thr = .35 + .5 * q
        smoke = (field > thr).astype(np.float32)
        light = (field > thr + .12).astype(np.float32)
        img = over(img, smoke * .9, DEEP); img = over(img, light * .9, (.85, .3, .5))
    # white-hot core with 3-band gradient map
    c = max(0.0, 1 - t / .16)
    if c > 0:
        base = np.clip(1 - R / (.25 + .5 * (1 - c)), 0, 1) * c
        flare = np.clip(1 - np.abs(V) / (.03 * c + .005), 0, 1) * np.clip(1 - np.abs(U) / 1.0, 0, 1) * c
        flare2 = np.clip(1 - np.abs(U) / (.02 * c + .004), 0, 1) * np.clip(1 - np.abs(V) / .7, 0, 1) * c
        f = np.maximum(base * 1.6, np.maximum(flare, flare2))
        band = np.where(f > .66, 1.0, np.where(f > .4, .66, np.where(f > .15, .33, 0)))
        col = ramp(band, [(0, (0, 0, 0)), (.33, DEEP), (.66, TINT), (1, (1, 1, 1))])
        img = over(img, (band > 0).astype(np.float32), (0, 0, 0)) + col * (band > 0)[..., None]
    # slash: tapered crescent with black outline
    if t < .2:
        k = t / .2
        ang = .5
        du = U * math.cos(ang) + V * math.sin(ang); dv = -U * math.sin(ang) + V * math.cos(ang)
        arc = dv - (du ** 2) * .5 + .05
        width = .06 * (1 - np.abs(du) / 1.05) * (1 - k)
        reveal = du < -1 + 2.4 * min(k * 3, 1)
        sl = (np.abs(arc) < width) & reveal & (np.abs(du) < 1.05)
        ol = (np.abs(arc) < width + .025) & reveal & (np.abs(du) < 1.05)
        img[ol] = (.05, 0, .05); img[sl] = (1, 1, 1)
    # triangle shards (sharp, tinted, black edge)
    r2 = np.random.default_rng(4)
    for i in range(9):
        a = r2.random() * 6.28; d = (.25 + .7 * (1 - (1 - min(t / .35, 1)) ** 2)) * (.7 + .5 * r2.random())
        size = (.07 + .05 * r2.random()) * max(0, 1 - t / .45)
        if size <= 0: continue
        cx, cy = math.cos(a) * d, math.sin(a) * d
        du, dv = U - cx, V - cy
        along = du * math.cos(a) + dv * math.sin(a); across = -du * math.sin(a) + dv * math.cos(a)
        tri = (along > -size) & (along < size * 1.6) & (np.abs(across) < (size * 1.6 - along) * .35)
        tri_o = (along > -size - .015) & (along < size * 1.6 + .02) & (np.abs(across) < (size * 1.6 - along) * .35 + .015)
        img[tri_o] = (.05, 0, .05); img[tri] = (1, .55, .7) if i % 3 else (1, 1, 1)
    return img


def strip(fn, times, name, labels=True, inv_first=False):
    frames = []
    for i, t in enumerate(times):
        im = fn(t, True) if (inv_first and i == 0) else fn(t)
        frames.append(Image.fromarray((np.clip(im, 0, 1) * 255).astype(np.uint8)))
    W = S * len(frames) + 8 * (len(frames) - 1)
    sheet = Image.new("RGB", (W, S + 34), (14, 10, 20))
    d = ImageDraw.Draw(sheet)
    font = ImageFont.truetype("/System/Library/Fonts/Supplemental/Arial Black.ttf", 18)
    for i, f in enumerate(frames):
        sheet.paste(f, (i * (S + 8), 0))
        lab = "IMPACT FRAME" if (inv_first and i == 0) else f"{int(round(times[i] * 1000))} ms"
        d.text((i * (S + 8) + 10, S + 6), lab, fill=(235, 225, 245), font=font)
    sheet.save(f"{OUT}/{name}.png")


strip(lambda t: current(t), [0, .05, .12, .22, .4], "hit_current")
strip(proposed, [0, .03, .08, .18, .4], "hit_proposed", inv_first=True)

# ---------- gradient map demo: one grayscale fireball, 4 race ramps ----------
n = noise(6, 2)
Vv = V + .25
flame = np.clip(1 - np.sqrt(U * U * 1.6 + np.where(Vv > 0, Vv * Vv * .35, Vv * Vv * 2.2)) / .8, 0, 1)
fire = np.clip(flame * 1.7 - (1 - n) * 1.1 - (n.max() - n) * 0 , 0, 1)
ramps = {
    "fox": [(0, (0, 0, 0)), (.25, (.5, .05, .1)), (.5, (1, .35, .1)), (.75, (1, .78, .3)), (1, (1, 1, .95))],
    "demon": [(0, (0, 0, 0)), (.25, (.2, .02, .35)), (.5, (.6, .15, .95)), (.75, (.65, 1, .3)), (1, (1, 1, 1))],
    "blood": [(0, (0, 0, 0)), (.25, (.3, 0, .05)), (.5, (.95, .1, .25)), (.75, (1, .55, .6)), (1, (1, 1, 1))],
    "tide": [(0, (0, 0, 0)), (.25, (0, .12, .35)), (.5, (.1, .55, 1)), (.75, (.55, .95, 1)), (1, (1, 1, 1))],
}
tiles = [Image.fromarray((np.stack([fire] * 3, -1) * 255).astype(np.uint8))]
for k, st in ramps.items():
    poster = np.floor(fire * 5) / 4  # cel bands
    tiles.append(Image.fromarray((ramp(poster, st) * 255).astype(np.uint8)))
sheet = Image.new("RGB", (S * 5 + 32, S + 34), (14, 10, 20))
d = ImageDraw.Draw(sheet); font = ImageFont.truetype("/System/Library/Fonts/Supplemental/Arial Black.ttf", 18)
for i, (tile, lab) in enumerate(zip(tiles, ["GRAY SOURCE", "FOX", "DEMON", "BLOOD", "TIDE"])):
    sheet.paste(tile, (i * (S + 8), 0)); d.text((i * (S + 8) + 10, S + 6), lab, fill=(235, 225, 245), font=font)
sheet.save(f"{OUT}/gradient_map.png")


# ---------- damage numbers ----------
def numbers():
    W, H = 900, 300
    im = Image.new("RGB", (W, H), (34, 22, 52)); d = ImageDraw.Draw(im)
    f = ImageFont.truetype("/System/Library/Fonts/Supplemental/Arial Black.ttf", 64)
    fb = ImageFont.truetype("/System/Library/Fonts/Supplemental/Impact.ttf", 110)
    fs = ImageFont.truetype("/System/Library/Fonts/Supplemental/Arial Black.ttf", 22)
    d.text((60, 120), "1,284", font=f, fill=(255, 190, 210), stroke_width=3, stroke_fill=(20, 10, 30))
    d.text((60, 230), "NOW", font=fs, fill=(200, 190, 220))
    # proposed: tilted digits, red/black offset, white face, crit ribbon
    layer = Image.new("RGBA", (520, 300), (0, 0, 0, 0)); ld = ImageDraw.Draw(layer)
    ld.polygon([(20, 196), (470, 160), (478, 200), (28, 236)], fill=(220, 16, 40, 255))
    rib = Image.new("RGBA", (300, 40), (0, 0, 0, 0)); ImageDraw.Draw(rib).text((0, 4), "CRITICAL!", font=fs, fill=(255, 255, 255))
    layer.alpha_composite(rib.rotate(4.5, resample=Image.BICUBIC), (70, 184))
    x = 40
    for i, ch in enumerate("3,852"):
        g = Image.new("RGBA", (120, 150), (0, 0, 0, 0)); gd = ImageDraw.Draw(g)
        gd.text((12, 4), ch, font=fb, fill=(10, 4, 14), stroke_width=9, stroke_fill=(10, 4, 14))
        gd.text((4, -2), ch, font=fb, fill=(230, 20, 50), stroke_width=7, stroke_fill=(230, 20, 50))
        gd.text((4, -2), ch, font=fb, fill=(255, 255, 255), stroke_width=3, stroke_fill=(10, 4, 14))
        g = g.rotate([-8, 5, -3, 9, -6][i], resample=Image.BICUBIC, expand=False)
        layer.alpha_composite(g, (x, [10, 0, 16, 4, 12][i]))
        x += 40 if ch == "," else 70
    im.paste(layer, (360, 0), layer)
    d.text((380, 260), "PROPOSED (P5)", font=fs, fill=(200, 190, 220))
    im.save(f"{OUT}/damage_numbers.png")


numbers()
print("ok")
