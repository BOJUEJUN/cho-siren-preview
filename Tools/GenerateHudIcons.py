"""Render the selected HUD icon set to antialiased transparent PNG files."""

from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter


PROJECT = Path(__file__).resolve().parents[1]
OUT = PROJECT / "Assets" / "Resources" / "Art" / "UI" / "HudIcons"
S = 16
CANVAS = 64 * S

WHITE = (255, 255, 255, 255)
CYAN = (53, 221, 255, 120)
PINK = (255, 90, 203, 108)


def pts(values, dx=0, dy=0):
    return [((x + dx) * S, (y + dy) * S) for x, y in values]


def layer():
    return Image.new("RGBA", (CANVAS, CANVAS), (0, 0, 0, 0))


def stroke(draw, values, color, width, *, closed=False, dx=0, dy=0, joint="curve"):
    points = pts(values, dx, dy)
    if closed:
        points.append(points[0])
    draw.line(points, fill=color, width=round(width * S), joint=joint)
    radius = width * S / 2
    for point in (points[0], points[-1]):
        draw.ellipse((point[0] - radius, point[1] - radius, point[0] + radius, point[1] + radius), fill=color)


def glow_composite(base, painter):
    for color, offset in ((CYAN, (-1.25, 1.25)), (PINK, (1.25, -1.25))):
        glow = layer()
        painter(ImageDraw.Draw(glow), color, 5.5, *offset)
        diffuse = glow.filter(ImageFilter.GaussianBlur(1.15 * S))
        base.alpha_composite(diffuse)
        base.alpha_composite(glow)
    core = layer()
    painter(ImageDraw.Draw(core), WHITE, 3.35, 0, 0)
    base.alpha_composite(core)


def draw_mail():
    base = layer()

    def painter(d, color, width, dx, dy):
        d.rounded_rectangle(((7 + dx) * S, (15 + dy) * S, (57 + dx) * S, (50 + dy) * S),
                            radius=6 * S, outline=color, width=round(width * S))
        stroke(d, [(10, 19), (30.1, 35.2), (32, 35.9), (33.9, 35.2), (54, 19)], color, width, dx=dx, dy=dy)
        stroke(d, [(9.5, 47), (24.2, 32.6)], color, width, dx=dx, dy=dy)
        stroke(d, [(54.5, 47), (39.8, 32.6)], color, width, dx=dx, dy=dy)

    glow_composite(base, painter)
    accent = ImageDraw.Draw(base)
    stroke(accent, [(32, 29.5), (32, 34.5)], (255, 115, 211, 242), 1.65)
    stroke(accent, [(29.5, 32), (34.5, 32)], (255, 115, 211, 242), 1.65)
    return base


def draw_music():
    base = layer()

    def painter(d, color, width, dx, dy):
        stroke(d, [(25, 15), (25, 46)], color, width, dx=dx, dy=dy)
        stroke(d, [(25, 17), (53, 10), (53, 39)], color, width, dx=dx, dy=dy)
        stroke(d, [(25, 25), (53, 18)], color, width, dx=dx, dy=dy)
        d.ellipse(((8 + dx) * S, (40 + dy) * S, (26 + dx) * S, (54 + dy) * S), outline=color, width=round(width * S))
        d.ellipse(((36 + dx) * S, (33 + dy) * S, (54 + dx) * S, (47 + dy) * S), outline=color, width=round(width * S))

    glow_composite(base, painter)
    d = ImageDraw.Draw(base)
    d.polygon(pts([(8.5, 20), (9.6, 22.8), (12.5, 23.9), (9.6, 25), (8.5, 27.9), (7.4, 25), (4.5, 23.9), (7.4, 22.8)]), fill=(255, 115, 211, 255))
    return base


GEAR = [(26, 7), (38, 7), (39.8, 13.8), (44.8, 16.7), (51.5, 14.8), (57.5, 25.2),
        (52.6, 30.1), (52.6, 35.9), (57.5, 40.8), (51.5, 51.2), (44.8, 49.3),
        (39.8, 52.2), (38, 59), (26, 59), (24.2, 52.2), (19.2, 49.3), (12.5, 51.2),
        (6.5, 40.8), (11.4, 35.9), (11.4, 30.1), (6.5, 25.2), (12.5, 14.8),
        (19.2, 16.7), (24.2, 13.8)]


def draw_settings():
    base = layer()

    def painter(d, color, width, dx, dy):
        stroke(d, GEAR, color, width, closed=True, dx=dx, dy=dy)
        d.ellipse(((21 + dx) * S, (22 + dy) * S, (43 + dx) * S, (44 + dy) * S), outline=color, width=round(width * S))

    glow_composite(base, painter)
    d = ImageDraw.Draw(base)
    arc_box = (23.5 * S, 24.5 * S, 40.5 * S, 41.5 * S)
    d.arc(arc_box, 185, 275, fill=(115, 234, 255, 242), width=round(1.7 * S))
    d.arc(arc_box, 5, 95, fill=(255, 115, 211, 242), width=round(1.7 * S))
    size = round(CANVAS * 0.84)
    scaled = base.resize((size, size), Image.Resampling.LANCZOS)
    result = layer()
    result.alpha_composite(scaled, ((CANVAS - size) // 2, (CANVAS - size) // 2))
    return result


def save(name, image):
    image.resize((256, 256), Image.Resampling.LANCZOS).save(OUT / f"{name}.png", optimize=True)


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    save("Mail", draw_mail())
    save("Music", draw_music())
    save("Settings", draw_settings())


if __name__ == "__main__":
    main()
