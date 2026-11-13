"""Split the 播放器.psd composite layer into the parts the game animates (2026-11-12)."""
import sys, os, math
import numpy as np, cv2
from PIL import Image, ImageDraw

src, out = sys.argv[1], sys.argv[2]
os.makedirs(out, exist_ok=True)
L = np.array(Image.open(src).convert('RGBA')).astype(np.float32)  # 1006x729 layer, y down
H, W = L.shape[:2]
rgb, a = L[:, :, :3], L[:, :, 3]
lum = rgb.mean(2)
mn = rgb.min(2)
yy, xx = np.mgrid[0:H, 0:W]

# stray faint smudge in the transparent margin (top-left): drop near-invisible alpha
a[a < 6] = 0
a[:75, :420] = 0  # faint stray brush mark above the record

DISC_C, DISC_R = (327.5, 300.0), 198.0
BTN_C, BTN_R = (134.5, 481.5), 105.0

# --- glyphs (white on dark): box, threshold on the min channel ---
ICONS = {
    'icon-round-next': (92, 432, 170, 530),
    'icon-loop': (278, 634, 326, 680),
    'icon-prev': (393, 614, 442, 659),
    'icon-play': (524, 577, 586, 650),
    'icon-next': (665, 572, 715, 615),
    'icon-shuffle': (781, 551, 833, 596),
}
TEXT_BOXES = [  # (box, kind) erased from the base
    ((585, 238, 805, 378), "white"),   # title
    ((580, 370, 712, 428), 'violet'),  # ARTIST / ALBUM
    ((824, 288, 908, 360), 'violet'),  # level bars + sparkles
    ((826, 422, 886, 450), 'white'),   # 03:29
    ((542, 461, 600, 491), 'white'),   # 01:06
]

def save(arr, name):
    Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8)).save(os.path.join(out, name + '.png'))

mask = np.zeros((H, W), np.uint8)
for name, (x0, y0, x1, y1) in ICONS.items():
    sub = mn[y0:y1, x0:x1]
    alpha = np.clip((sub - 95) / 140.0, 0, 1)
    glyph = np.zeros((y1 - y0, x1 - x0, 4), np.float32)
    glyph[:, :, :3] = 255
    glyph[:, :, 3] = alpha * 255
    ys, xs = np.where(alpha > .02)
    gx0, gx1, gy0, gy1 = xs.min(), xs.max() + 1, ys.min(), ys.max() + 1
    pad = 3
    gx0, gy0 = max(0, gx0 - pad), max(0, gy0 - pad)
    gx1, gy1 = min(x1 - x0, gx1 + pad), min(y1 - y0, gy1 + pad)
    save(glyph[gy0:gy1, gx0:gx1], name)
    print(name, 'centre', x0 + (gx0 + gx1) / 2, y0 + (gy0 + gy1) / 2, 'size', gx1 - gx0, gy1 - gy0)
    m = (alpha > .05).astype(np.uint8)
    mask[y0:y1, x0:x1] |= m

for (x0, y0, x1, y1), kind in TEXT_BOXES:
    sub = rgb[y0:y1, x0:x1]
    if kind == 'white':
        m = sub.mean(2) > 70
    else:
        m = (sub.max(2) > 70)
    mask[y0:y1, x0:x1] |= m.astype(np.uint8)

# progress wedge: everything violet inside the band polygon
band = Image.new('L', (W, H), 0)
ImageDraw.Draw(band).polygon([(546, 448), (690, 424), (720, 418), (892, 399), (892, 414), (546, 468)], fill=255)
band = np.array(band) > 0
violet = (rgb[:, :, 2] > 90) & (lum > 45)
mask |= (band & violet).astype(np.uint8)
mask = cv2.dilate(mask, np.ones((5, 5), np.uint8))

bgr = cv2.cvtColor(rgb.astype(np.uint8), cv2.COLOR_RGB2BGR)
clean = cv2.inpaint(bgr, mask * 255, 6, cv2.INPAINT_TELEA)
clean = cv2.cvtColor(clean, cv2.COLOR_BGR2RGB).astype(np.float32)
base = np.dstack([clean, a])
save(base, 'player-base')
Image.fromarray((mask * 255).astype(np.uint8)).save(os.path.join(out, '..', 'erase-mask-debug.png')) if False else None

# --- spinning record: circle around the spindle hole, the button's overlap refilled by rotation ---
def bilinear(img, x, y):
    return cv2.remap(img, x.astype(np.float32), y.astype(np.float32), cv2.INTER_LINEAR, borderMode=cv2.BORDER_CONSTANT)

disc_src = base.copy()
dx, dy = xx - DISC_C[0], yy - DISC_C[1]
r = np.hypot(dx, dy)
bd = np.hypot(xx - BTN_C[0], yy - BTN_C[1])
occ = (r < DISC_R + 2) & (bd < BTN_R + 8)
ang = math.radians(110)
sx = DISC_C[0] + dx * math.cos(ang) - dy * math.sin(ang)
sy = DISC_C[1] + dx * math.sin(ang) + dy * math.cos(ang)
rot = bilinear(base, sx, sy)
disc_src[occ] = rot[occ]
edge = np.clip(DISC_R - r + .5, 0, 1)
disc_src[:, :, 3] *= edge
x0, y0 = int(DISC_C[0] - DISC_R - 1), int(DISC_C[1] - DISC_R - 1)
size = int(2 * DISC_R + 2)
# centre the crop exactly on the spindle (x .5 offset → shift by half a pixel)
M = np.float32([[1, 0, -(DISC_C[0] - DISC_R - 1) + 0], [0, 1, -(DISC_C[1] - DISC_R - 1)]])
disc = cv2.warpAffine(disc_src, M, (size, size), flags=cv2.INTER_LINEAR)
save(disc, 'player-disc')
print('disc crop', DISC_C, DISC_R, size)

# --- round next button (icon erased) ---
btn = base.copy()
btn[:, :, 3] *= np.clip(BTN_R - bd + .5, 0, 1)
bs = int(2 * BTN_R + 2)
M = np.float32([[1, 0, -(BTN_C[0] - BTN_R - 1)], [0, 1, -(BTN_C[1] - BTN_R - 1)]])
save(cv2.warpAffine(btn, M, (bs, bs), flags=cv2.INTER_LINEAR), 'player-round')

# --- progress wedge (track + fill), drawn from the measured geometry at 4x ---
S = 4
bx0, bx1 = 553, 882
top = [(553, 452.5), (882, 405.5)]
bot = [(553, 462.5), (882, 410.5)]
by0, by1 = 403, 465
def wedge(col_l, col_r):
    w, h = (bx1 - bx0) * S, (by1 - by0) * S
    shape = Image.new('L', (w, h), 0)
    pts = [((x - bx0) * S, (y - by0) * S) for x, y in top + bot[::-1]]
    ImageDraw.Draw(shape).polygon(pts, fill=255)
    shape = shape.resize((bx1 - bx0, by1 - by0), Image.LANCZOS)
    t = np.linspace(0, 1, bx1 - bx0)[None, :, None]
    col = np.array(col_l[:3], np.float32) * (1 - t) + np.array(col_r[:3], np.float32) * t
    out_ = np.zeros((by1 - by0, bx1 - bx0, 4), np.float32)
    out_[:, :, :3] = col
    out_[:, :, 3] = np.array(shape, np.float32) * (col_l[3] / 255.0)
    return out_
save(wedge([176, 110, 255, 255], [150, 80, 255, 255]), 'bar-fill')
save(wedge([70, 30, 170, 235], [86, 44, 214, 235]), 'bar-track')
# bolt thumb from the original (violet-white on black): unpremultiply from black
bolt = rgb[421:448, 689:717]
alpha = np.clip(bolt.max(2) / 255.0 * 1.4 - .12, 0, 1)
col = np.clip(bolt / np.maximum(alpha[:, :, None], .05), 0, 255)
save(np.dstack([col, alpha * 255]), 'bar-bolt')
# level-bar segment (tiled): 4 px lit, 2 px gap
seg = np.zeros((6, 8, 4), np.float32); seg[:4, :, :] = 255
save(seg, 'eq-seg')
# pause glyph in the play glyph's style (two slanted white bars)
pw, ph = 60, 72
pz = Image.new('L', (pw * S, ph * S), 0); d = ImageDraw.Draw(pz)
for bx in (10, 36):
    d.polygon([((bx + 3) * S, 4 * S), ((bx + 17) * S, 4 * S), ((bx + 14) * S, 68 * S), (bx * S, 68 * S)], fill=255)
pz = np.array(pz.resize((pw, ph), Image.LANCZOS), np.float32)
pa = np.zeros((ph, pw, 4), np.float32); pa[:, :, :3] = 255; pa[:, :, 3] = pz
save(pa, 'icon-pause')
print('done', base.shape)

# --- light that orbits the record's rim while a song plays (the glass dome itself never turns) ---
RIM_C, RIM_R = (323.0, 309.0), 214.0
n = int(2 * (RIM_R + 16)) * S
img = np.zeros((n, n), np.float32)
c = n / 2
gy, gx = np.mgrid[0:n, 0:n]
rr = np.hypot(gx - c, gy - c) / S
th = (np.degrees(np.arctan2(-(gy - c), gx - c)) + 360) % 360  # 0 = right, counter-clockwise
radial = np.exp(-((rr - RIM_R) / 3.2) ** 2) + .35 * np.exp(-((rr - RIM_R) / 9.0) ** 2)
for head, strength in ((90.0, 1.0), (270.0, .55)):
    lag = (head - th) % 360          # tail trails behind a clockwise-moving head... drawn counter-clockwise
    tail = np.where(lag < 150, (1 - lag / 150) ** 2.2, 0)
    img += strength * tail
orbit = np.clip(radial * img, 0, 1)
orbit = cv2.resize(orbit, (n // S, n // S), interpolation=cv2.INTER_AREA)
o = np.zeros(orbit.shape + (4,), np.float32)
o[:, :, 0], o[:, :, 1], o[:, :, 2] = 235, 215, 255
o[:, :, 3] = orbit * 255
save(o, 'disc-orbit')
print('orbit', RIM_C, RIM_R, orbit.shape)
