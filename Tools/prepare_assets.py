"""Convert the browser prototype's WebP art into Unity-native assets.

Source files are never modified. The generated PNG/JPEG copies live under
Assets/Resources so the same content is reliable on Windows, Web and Android.
"""

from __future__ import annotations

import json
from pathlib import Path

from PIL import Image


PROJECT = Path(__file__).resolve().parents[1]
SOURCE = PROJECT.parent / "cho-siren-pages" / "assets"
ART = PROJECT / "Assets" / "Resources" / "Art"


def save_rgba(source_name: str, destination: Path) -> None:
    destination.parent.mkdir(parents=True, exist_ok=True)
    with Image.open(SOURCE / source_name) as image:
        image.convert("RGBA").save(destination, "PNG", optimize=True, compress_level=7)


def save_background() -> None:
    destination = ART / "LobbyBackground.jpg"
    with Image.open(SOURCE / "background-portrait-hd.webp") as image:
        image = image.convert("RGB")
        # 1080 px is crisp on phone screens without carrying a 3104 px texture.
        target = (1080, round(image.height * 1080 / image.width))
        image.resize(target, Image.Resampling.LANCZOS).save(
            destination, "JPEG", quality=92, optimize=True, progressive=True
        )


def save_animation() -> None:
    source = SOURCE / "character-idle-mobile-lite.webp"
    output = ART / "HeroFrames"
    output.mkdir(parents=True, exist_ok=True)
    durations: list[int] = []

    with Image.open(source) as image:
        for index in range(image.n_frames):
            image.seek(index)
            frame = image.convert("RGBA")
            duration = int(image.info.get("duration") or 111)
            durations.append(duration)
            frame.save(
                output / f"hero_{index:03d}.png",
                "PNG",
                optimize=True,
                compress_level=7,
            )

    (ART / "hero-animation.json").write_text(
        json.dumps({"durationsMs": durations, "loop": True}, separators=(",", ":")),
        encoding="utf-8",
    )


def main() -> None:
    if not SOURCE.is_dir():
        raise SystemExit(f"Prototype asset folder not found: {SOURCE}")

    save_background()
    save_rgba("character-correct-display.webp", ART / "HeroFallback.png")
    save_rgba("profile-avatar.webp", ART / "ProfileAvatar.png")
    save_rgba("nav-icons-display.webp", ART / "UI" / "NavIcons.png")
    save_rgba("ui-emblems-display.webp", ART / "UI" / "Emblems.png")
    save_rgba("vfx-overlay-display.webp", ART / "UI" / "VfxOverlay.png")

    for source in sorted(SOURCE.glob("member-*.webp")):
        save_rgba(source.name, ART / "Members" / f"{source.stem}.png")

    save_animation()

    outputs = [path for path in ART.rglob("*") if path.is_file()]
    total = sum(path.stat().st_size for path in outputs)
    print(f"Prepared {len(outputs)} Unity art files ({total / 1024 / 1024:.1f} MiB)")


if __name__ == "__main__":
    main()
