"""Cut the turning record face (mascot + crystal) out of player-base.png into disc-spin.png (2026-11-12).

Centre (327.5, 300) and radius 203 were measured on player-base.png (the dark ring starts at ~204). Pixels under the
round 下一首 button (drawn on top) are replaced by the same ring a quarter turn round, so no button shows while it spins.
"""
import math
from PIL import Image

ART = "Assets/Resources/Art/Music1112/"
base = Image.open(ART + "player-base.png").convert("RGBA")
bp = base.load()
cx, cy, R = 327.5, 300.0, 203.0
rx, ry, rr = 134.5, 481.5, 108.0
S = int(2 * R) + 2
out = Image.new("RGBA", (S, S), (0, 0, 0, 0))
op = out.load()
ox, oy = cx - S / 2, cy - S / 2


def sample(x, y):
    xi, yi = int(round(x)), int(round(y))
    return bp[xi, yi] if 0 <= xi < base.width and 0 <= yi < base.height else (0, 0, 0, 0)


for j in range(S):
    for i in range(S):
        x, y = ox + i + .5, oy + j + .5
        d = math.hypot(x - cx, y - cy)
        if d > R + 1:
            continue
        if math.hypot(x - rx, y - ry) < rr:
            ang = math.atan2(y - cy, x - cx) + math.pi / 2
            c = sample(cx + d * math.cos(ang), cy + d * math.sin(ang))
        else:
            c = sample(x, y)
        op[i, j] = (c[0], c[1], c[2], int(c[3] * max(0.0, min(1.0, R + .5 - d))))
out.save(ART + "disc-spin.png")
print("disc-spin.png", out.size, "top-left", ox, oy)
