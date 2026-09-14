#!/usr/bin/env python3
"""Validate and prepare the four transparent Lobby Punk V2 UI assets.

This tool intentionally has no background-removal path.  It accepts only native,
8-bit, non-interlaced RGBA PNG masters and may only split a grid, crop pixels that
are already transparent, resize, and place art on a transparent canvas.

Pillow is the only third-party dependency.  The repository's existing art tools
already use it::

    python3 -m pip install Pillow
"""

from __future__ import annotations

import argparse
import json
import math
import shutil
import struct
import sys
import uuid
from collections import Counter
from dataclasses import dataclass
from pathlib import Path
from typing import Dict, Iterable, List, Optional, Sequence, Tuple

try:
    from PIL import Image, ImageDraw, ImageFont
except ImportError as exc:  # pragma: no cover - exercised by the actionable CLI error.
    raise SystemExit("Pillow is required: python3 -m pip install Pillow") from exc


PROJECT_ROOT = Path(__file__).resolve().parents[1]
DEFAULT_OUTPUT = PROJECT_ROOT / "Assets" / "Resources" / "Art" / "LobbyPunk"
PNG_SIGNATURE = b"\x89PNG\r\n\x1a\n"

ROLE_OUTPUTS = {
    "practice": "lobby-practice-punk-v2.png",
    "album": "lobby-album-punk-v2.png",
    "task": "lobby-task-punk-v2.png",
    "perform-cta": "lobby-perform-cta-punk-v2.png",
}
ROLE_ALIASES = {
    "practice-room": "practice",
    "album-production": "album",
    "tasks": "task",
    "cta": "perform-cta",
    "perform": "perform-cta",
    "live": "perform-cta",
}

CHINESE_FONT = Path("/System/Library/Fonts/STHeiti Medium.ttc")
ENGLISH_FONT = Path("/System/Library/Fonts/Supplemental/Impact.ttf")


class AssetRejected(ValueError):
    """Raised when a source is not a genuine transparent RGBA master."""


@dataclass(frozen=True)
class AlphaMetrics:
    width: int
    height: int
    transparent_fraction: float
    partial_fraction: float
    opaque_fraction: float
    alpha_bbox: Tuple[int, int, int, int]
    checkerboard_score: float

    def as_dict(self) -> Dict[str, object]:
        return {
            "size": [self.width, self.height],
            "transparentFraction": round(self.transparent_fraction, 6),
            "partialAlphaFraction": round(self.partial_fraction, 6),
            "opaqueFraction": round(self.opaque_fraction, 6),
            "alphaBounds": list(self.alpha_bbox),
            "checkerboardScore": round(self.checkerboard_score, 4),
        }


def canonical_role(value: str) -> str:
    role = value.strip().lower().replace("_", "-")
    role = ROLE_ALIASES.get(role, role)
    if role not in ROLE_OUTPUTS:
        allowed = ", ".join(ROLE_OUTPUTS)
        raise argparse.ArgumentTypeError(f"unknown role {value!r}; expected one of: {allowed}")
    return role


def parse_assignment(value: str) -> Tuple[str, Path]:
    if "=" not in value:
        raise argparse.ArgumentTypeError("expected ROLE=/absolute/path.png")
    raw_role, raw_path = value.split("=", 1)
    return canonical_role(raw_role), Path(raw_path).expanduser()


def parse_grid(value: str) -> Tuple[int, int]:
    try:
        columns, rows = (int(part) for part in value.lower().split("x", 1))
    except (TypeError, ValueError):
        raise argparse.ArgumentTypeError("grid must look like 2x2")
    if columns <= 0 or rows <= 0:
        raise argparse.ArgumentTypeError("grid dimensions must be positive")
    return columns, rows


def parse_canvas(value: str) -> Tuple[str, Tuple[int, int]]:
    if "=" not in value:
        raise argparse.ArgumentTypeError("expected ROLE=WIDTHxHEIGHT")
    raw_role, raw_size = value.split("=", 1)
    width, height = parse_grid(raw_size)
    return canonical_role(raw_role), (width, height)


def _native_png_header(path: Path) -> Tuple[int, int]:
    try:
        header = path.read_bytes()[:33]
    except OSError as exc:
        raise AssetRejected(f"cannot read {path}: {exc}") from exc
    if len(header) < 33 or header[:8] != PNG_SIGNATURE or header[12:16] != b"IHDR":
        raise AssetRejected(f"{path}: source must be a PNG file")
    width, height, bit_depth, color_type, compression, filtering, interlace = struct.unpack(
        ">IIBBBBB", header[16:29]
    )
    if bit_depth != 8 or color_type != 6:
        raise AssetRejected(
            f"{path}: source must be native 8-bit RGBA PNG (IHDR color type 6); "
            f"found bit depth {bit_depth}, color type {color_type}"
        )
    if compression != 0 or filtering != 0 or interlace != 0:
        raise AssetRejected(f"{path}: only standard, non-interlaced RGBA PNG masters are accepted")
    if width <= 1 or height <= 1:
        raise AssetRejected(f"{path}: image is too small ({width}x{height})")
    return width, height


def open_native_rgba(path: Path) -> Image.Image:
    expected_size = _native_png_header(path)
    try:
        source = Image.open(path)
        source.load()
    except (OSError, ValueError) as exc:
        raise AssetRejected(f"{path}: invalid PNG: {exc}") from exc
    if source.format != "PNG" or source.mode != "RGBA":
        raise AssetRejected(f"{path}: Pillow decoded {source.format}/{source.mode}; native RGBA is required")
    if getattr(source, "n_frames", 1) != 1:
        raise AssetRejected(f"{path}: animated PNG masters are not accepted")
    if source.size != expected_size:
        raise AssetRejected(f"{path}: PNG header and decoded dimensions disagree")
    return source.copy()


def _checkerboard_score(image: Image.Image) -> float:
    """Return a conservative score for a baked, neutral two-colour checkerboard.

    RGB files and checkerboards reaching a corner are already rejected elsewhere.
    This second check catches the less common case where a fake checkerboard was
    pasted inside a transparent margin.  It intentionally looks only for two
    frequent, near-neutral colours with alternating spatial periodicity.
    """

    sample = image.copy()
    sample.thumbnail((160, 160), Image.Resampling.NEAREST)
    width, height = sample.size
    # Pillow 12.1 deprecated getdata(); keep compatibility with older Pillow used by
    # some Unity workstations while avoiding the warning on current environments.
    pixels = list(
        sample.get_flattened_data() if hasattr(sample, "get_flattened_data") else sample.getdata()
    )
    neutral_bins: Counter[Tuple[int, int, int]] = Counter()
    opaque_count = 0
    for red, green, blue, alpha in pixels:
        if alpha < 240:
            continue
        opaque_count += 1
        if max(red, green, blue) - min(red, green, blue) <= 14:
            neutral_bins[(red // 12, green // 12, blue // 12)] += 1
    if opaque_count < width * height * 0.2 or len(neutral_bins) < 2:
        return 0.0

    common = neutral_bins.most_common(6)
    best = 0.0
    for first_index in range(len(common)):
        for second_index in range(first_index + 1, len(common)):
            first_bin, first_count = common[first_index]
            second_bin, second_count = common[second_index]
            first = tuple(channel * 12 + 5.5 for channel in first_bin)
            second = tuple(channel * 12 + 5.5 for channel in second_bin)
            first_luma = sum(first) / 3.0
            second_luma = sum(second) / 3.0
            difference = abs(first_luma - second_luma)
            combined = (first_count + second_count) / float(width * height)
            if difference < 8.0 or difference > 96.0 or combined < 0.18:
                continue

            classes = [-1] * (width * height)
            classified = 0
            for index, (red, green, blue, alpha) in enumerate(pixels):
                if alpha < 240 or max(red, green, blue) - min(red, green, blue) > 18:
                    continue
                d0 = (red - first[0]) ** 2 + (green - first[1]) ** 2 + (blue - first[2]) ** 2
                d1 = (red - second[0]) ** 2 + (green - second[1]) ** 2 + (blue - second[2]) ** 2
                distance = min(d0, d1)
                if distance <= 24.0 ** 2 * 3:
                    classes[index] = 0 if d0 <= d1 else 1
                    classified += 1
            if classified < width * height * 0.16:
                continue

            for period in range(1, min(33, max(width, height) // 3 + 1)):
                opposite = same = comparisons = 0
                stride = max(1, period // 3)
                for y in range(0, height, stride):
                    row = y * width
                    for x in range(0, width, stride):
                        current = classes[row + x]
                        if current < 0:
                            continue
                        if x + period < width:
                            other = classes[row + x + period]
                            if other >= 0:
                                opposite += int(other != current)
                                comparisons += 1
                        if y + period < height:
                            other = classes[(y + period) * width + x]
                            if other >= 0:
                                opposite += int(other != current)
                                comparisons += 1
                        if x + 2 * period < width:
                            other = classes[row + x + 2 * period]
                            if other >= 0:
                                same += int(other == current)
                                comparisons += 1
                        if y + 2 * period < height:
                            other = classes[(y + 2 * period) * width + x]
                            if other >= 0:
                                same += int(other == current)
                                comparisons += 1
                if comparisons >= 100:
                    # Opposite/same pairs each account for roughly half the comparisons.
                    score = (opposite + same) / float(comparisons)
                    best = max(best, score * min(1.0, combined / 0.3))
    return best


def inspect_alpha(image: Image.Image, alpha_threshold: int) -> AlphaMetrics:
    alpha = image.getchannel("A")
    histogram = alpha.histogram()
    total = image.width * image.height
    mask = alpha.point(lambda value: 255 if value > alpha_threshold else 0)
    bbox = mask.getbbox()
    if bbox is None:
        raise AssetRejected("image has no visible pixels above the alpha threshold")
    return AlphaMetrics(
        width=image.width,
        height=image.height,
        transparent_fraction=histogram[0] / total,
        partial_fraction=sum(histogram[1:255]) / total,
        opaque_fraction=histogram[255] / total,
        alpha_bbox=bbox,
        checkerboard_score=_checkerboard_score(image),
    )


def validate_image(
    image: Image.Image,
    label: str,
    alpha_threshold: int,
    minimum_transparent: float,
    maximum_transparent: float,
    checkerboard_threshold: float,
) -> AlphaMetrics:
    corner_alpha = [
        image.getpixel((0, 0))[3],
        image.getpixel((image.width - 1, 0))[3],
        image.getpixel((0, image.height - 1))[3],
        image.getpixel((image.width - 1, image.height - 1))[3],
    ]
    if any(alpha != 0 for alpha in corner_alpha):
        raise AssetRejected(f"{label}: all four corner alpha values must be 0; found {corner_alpha}")
    metrics = inspect_alpha(image, alpha_threshold)
    if metrics.transparent_fraction < minimum_transparent:
        raise AssetRejected(
            f"{label}: only {metrics.transparent_fraction:.1%} fully transparent pixels; "
            f"minimum is {minimum_transparent:.1%}. This is likely a baked background."
        )
    if metrics.transparent_fraction > maximum_transparent:
        raise AssetRejected(
            f"{label}: {metrics.transparent_fraction:.1%} fully transparent pixels; "
            f"maximum is {maximum_transparent:.1%}. The artwork is likely empty or undersized."
        )
    if metrics.checkerboard_score >= checkerboard_threshold:
        raise AssetRejected(
            f"{label}: baked checkerboard pattern detected "
            f"(score {metrics.checkerboard_score:.3f} >= {checkerboard_threshold:.3f})"
        )
    return metrics


def padded_box(
    bbox: Tuple[int, int, int, int], width: int, height: int, padding: int
) -> Tuple[int, int, int, int]:
    left, top, right, bottom = bbox
    return (
        max(0, left - padding),
        max(0, top - padding),
        min(width, right + padding),
        min(height, bottom + padding),
    )


def resize_to_max_edge(image: Image.Image, max_edge: int) -> Image.Image:
    longest = max(image.size)
    if longest <= max_edge:
        return image
    scale = max_edge / float(longest)
    size = (max(1, round(image.width * scale)), max(1, round(image.height * scale)))
    return image.resize(size, Image.Resampling.LANCZOS)


def fit_on_canvas(image: Image.Image, size: Tuple[int, int], allow_upscale: bool) -> Image.Image:
    canvas_width, canvas_height = size
    scale = min(canvas_width / image.width, canvas_height / image.height)
    if not allow_upscale:
        scale = min(1.0, scale)
    fitted_size = (max(1, round(image.width * scale)), max(1, round(image.height * scale)))
    fitted = image if fitted_size == image.size else image.resize(fitted_size, Image.Resampling.LANCZOS)
    canvas = Image.new("RGBA", size, (0, 0, 0, 0))
    canvas.alpha_composite(fitted, ((canvas_width - fitted.width) // 2, (canvas_height - fitted.height) // 2))
    return canvas


def unity_meta(name: str, width: int, height: int) -> str:
    guid = uuid.uuid5(uuid.NAMESPACE_URL, "cho-siren:lobby-punk-v2:" + name).hex
    largest = max(width, height, 256)
    max_texture_size = min(8192, 2 ** math.ceil(math.log(largest, 2)))
    return f"""fileFormatVersion: 2
guid: {guid}
TextureImporter:
  serializedVersion: 13
  mipmaps:
    enableMipMap: 0
    sRGBTexture: 1
  isReadable: 0
  textureSettings:
    filterMode: 1
    wrapU: 1
    wrapV: 1
    wrapW: 1
  maxTextureSize: {max_texture_size}
  textureFormat: 1
  textureCompression: 0
  spriteMode: 1
  spritePixelsToUnits: 100
  spriteMeshType: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  alphaUsage: 1
  alphaIsTransparency: 1
  textureType: 8
  assetBundleName:
  assetBundleVariant:
"""


def split_sheet(image: Image.Image, columns: int, rows: int) -> List[Image.Image]:
    cells: List[Image.Image] = []
    for row in range(rows):
        for column in range(columns):
            box = (
                round(column * image.width / columns),
                round(row * image.height / rows),
                round((column + 1) * image.width / columns),
                round((row + 1) * image.height / rows),
            )
            cells.append(image.crop(box))
    return cells


def crop_on_transparent_canvas(
    source: Image.Image,
    box: Tuple[int, int, int, int],
    canvas_size: Tuple[int, int],
    margin: int = 20,
) -> Image.Image:
    """Crop authored pixels and fit them on a new transparent canvas.

    This deliberately preserves the source RGBA values.  It never derives alpha
    from colour and never removes a background.
    """
    crop = source.crop(box)
    available = (canvas_size[0] - margin * 2, canvas_size[1] - margin * 2)
    if available[0] <= 0 or available[1] <= 0:
        raise AssetRejected("transparent canvas margin leaves no drawable area")
    scale = min(available[0] / crop.width, available[1] / crop.height)
    size = (max(1, round(crop.width * scale)), max(1, round(crop.height * scale)))
    if size != crop.size:
        crop = crop.resize(size, Image.Resampling.LANCZOS)
    canvas = Image.new("RGBA", canvas_size, (0, 0, 0, 0))
    canvas.alpha_composite(crop, ((canvas.width - crop.width) // 2, (canvas.height - crop.height) // 2))
    return canvas


def fitted_font(path: Path, text: str, initial_size: int, max_width: int) -> ImageFont.FreeTypeFont:
    if not path.exists():
        raise AssetRejected(f"required local font is missing: {path}")
    size = initial_size
    while size >= 12:
        font = ImageFont.truetype(str(path), size=size)
        box = font.getbbox(text, stroke_width=max(1, size // 24))
        if box[2] - box[0] <= max_width:
            return font
        size -= 2
    raise AssetRejected(f"cannot fit text on authored card: {text}")


def draw_centered_text(
    image: Image.Image,
    text: str,
    center: Tuple[int, int],
    font_path: Path,
    size: int,
    max_width: int,
    fill: Tuple[int, int, int, int],
    stroke_fill: Tuple[int, int, int, int] = (20, 4, 45, 255),
) -> None:
    font = fitted_font(font_path, text, size, max_width)
    stroke_width = max(1, size // 24)
    ImageDraw.Draw(image).text(
        center,
        text,
        font=font,
        anchor="mm",
        align="center",
        fill=fill,
        stroke_width=stroke_width,
        stroke_fill=stroke_fill,
    )


def build_approved_entries(args: argparse.Namespace) -> Tuple[Dict[str, Image.Image], List[Dict[str, object]]]:
    required = {
        "entry sheet": args.entry_sheet,
        "CTA/navigation sheet": args.cta_nav_sheet,
        "album source": args.album_source,
    }
    missing = [label for label, path in required.items() if path is None]
    if missing:
        raise AssetRejected("--approved-entries requires: " + ", ".join(missing))

    masters: Dict[str, Image.Image] = {}
    reports: List[Dict[str, object]] = []
    for label, raw_path in required.items():
        path = raw_path.expanduser().resolve()
        image = open_native_rgba(path)
        metrics = inspect_alpha(image, 0)
        corners = [
            image.getpixel((0, 0))[3], image.getpixel((image.width - 1, 0))[3],
            image.getpixel((0, image.height - 1))[3],
            image.getpixel((image.width - 1, image.height - 1))[3],
        ]
        if max(corners) > 1:
            raise AssetRejected(f"{path}: source corners must be transparent; found {corners}")
        if metrics.transparent_fraction < 0.05:
            raise AssetRejected(f"{path}: source lacks native transparent area")
        if metrics.checkerboard_score >= args.checkerboard_threshold:
            raise AssetRejected(f"{path}: baked checkerboard pattern detected")
        masters[label] = image
        reports.append({"source": str(path), "corners": corners, **metrics.as_dict()})

    entry = masters["entry sheet"]
    cta_nav = masters["CTA/navigation sheet"]
    album = masters["album source"]

    prepared: Dict[str, Image.Image] = {
        "practice": crop_on_transparent_canvas(entry, (20, 0, 1085, 500), (1024, 640), 20),
        "task": crop_on_transparent_canvas(entry, (0, 930, 1080, 1330), (1024, 640), 20),
        "perform-cta": crop_on_transparent_canvas(cta_nav, (565, 80, 1024, 1230), (768, 1024), 24),
        "album": album.copy(),
    }

    draw_centered_text(prepared["practice"], "练习室", (365, 245), CHINESE_FONT, 88, 470, (248, 245, 255, 255))
    draw_centered_text(prepared["practice"], "PRACTICE ROOM", (365, 330), ENGLISH_FONT, 46, 500, (180, 92, 255, 255))
    draw_centered_text(prepared["task"], "任务", (340, 245), CHINESE_FONT, 92, 420, (248, 245, 255, 255))
    draw_centered_text(prepared["task"], "TASKS", (340, 335), ENGLISH_FONT, 52, 420, (180, 92, 255, 255))
    draw_centered_text(prepared["perform-cta"], "开始演出", (384, 330), CHINESE_FONT, 76, 520, (250, 247, 255, 255))
    draw_centered_text(prepared["perform-cta"], "START LIVE", (384, 420), ENGLISH_FONT, 52, 520, (221, 104, 255, 255))
    draw_centered_text(prepared["perform-cta"], "舞台已就绪", (384, 490), CHINESE_FONT, 34, 420, (255, 220, 249, 255))

    for role, image in prepared.items():
        metrics = validate_image(image, role, 0, 0.005, 0.98, args.checkerboard_threshold)
        if image.mode != "RGBA":
            raise AssetRejected(f"{role}: output mode is {image.mode}; RGBA required")
        reports.append({"outputRole": role, "corners": [0, 0, 0, 0], **metrics.as_dict()})
    return prepared, reports


def ensure_complete_roles(items: Iterable[str]) -> None:
    roles = list(items)
    duplicates = sorted(role for role, count in Counter(roles).items() if count > 1)
    missing = sorted(set(ROLE_OUTPUTS) - set(roles))
    extras = sorted(set(roles) - set(ROLE_OUTPUTS))
    if duplicates or missing or extras or len(roles) != len(ROLE_OUTPUTS):
        pieces = []
        if duplicates:
            pieces.append("duplicates=" + ",".join(duplicates))
        if missing:
            pieces.append("missing=" + ",".join(missing))
        if extras:
            pieces.append("extras=" + ",".join(extras))
        raise AssetRejected("exactly one source for every Lobby V2 role is required (" + "; ".join(pieces) + ")")


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description=(
            "Validate native RGBA Lobby masters and create the four LobbyPunk v2 PNGs. "
            "No background removal is performed."
        )
    )
    source_group = parser.add_mutually_exclusive_group(required=True)
    source_group.add_argument(
        "--source",
        action="append",
        type=parse_assignment,
        metavar="ROLE=PNG",
        help="one native RGBA PNG per role; repeat for practice, album, task, perform-cta",
    )
    source_group.add_argument("--sheet", type=Path, help="native RGBA PNG atlas to split")
    source_group.add_argument(
        "--approved-entries",
        action="store_true",
        help="build the four approved entrance assets from the named native-RGBA masters",
    )
    parser.add_argument("--entry-sheet", type=Path)
    parser.add_argument("--cta-nav-sheet", type=Path)
    parser.add_argument("--album-source", type=Path)
    parser.add_argument("--grid", type=parse_grid, default=(2, 2), metavar="COLSxROWS")
    parser.add_argument(
        "--order",
        default="practice,album,task,perform-cta",
        help="row-major roles for --sheet",
    )
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    parser.add_argument("--padding", type=int, default=24, help="transparent pixels kept around alpha bounds")
    parser.add_argument("--min-transparent", type=float, default=0.05)
    parser.add_argument("--max-transparent", type=float, default=0.95)
    parser.add_argument("--checkerboard-threshold", type=float, default=0.84)
    parser.add_argument("--max-edge", type=int, default=2048, help="downscale output longer edge; never upscales")
    parser.add_argument(
        "--canvas",
        action="append",
        type=parse_canvas,
        default=[],
        metavar="ROLE=WIDTHxHEIGHT",
        help="optionally center a role on an exact transparent canvas",
    )
    parser.add_argument("--allow-upscale", action="store_true", help="allow --canvas to enlarge artwork")
    parser.add_argument("--validate-only", action="store_true", help="perform every check but write nothing")
    parser.add_argument("--force", action="store_true", help="replace existing v2 PNG/meta files")
    parser.add_argument("--report", type=Path, help="optional JSON report path")
    return parser


def main(argv: Optional[Sequence[str]] = None) -> int:
    args = build_parser().parse_args(argv)
    if args.padding < 0:
        raise AssetRejected("--padding must be non-negative")
    if not 0 <= args.min_transparent < args.max_transparent <= 1:
        raise AssetRejected("transparent limits must satisfy 0 <= min < max <= 1")
    if not 0 < args.checkerboard_threshold <= 1:
        raise AssetRejected("--checkerboard-threshold must be in (0, 1]")
    if args.max_edge <= 0:
        raise AssetRejected("--max-edge must be positive")

    if args.approved_entries:
        prepared, audit = build_approved_entries(args)
        output_directory = args.output.expanduser().resolve()
        destinations = [
            path
            for role in prepared
            for path in (
                output_directory / ROLE_OUTPUTS[role],
                Path(str(output_directory / ROLE_OUTPUTS[role]) + ".meta"),
            )
        ]
        existing = [str(path) for path in destinations if path.exists()]
        if existing and not args.force and not args.validate_only:
            raise AssetRejected("refusing to replace existing outputs without --force: " + ", ".join(existing))
        if not args.validate_only:
            output_directory.mkdir(parents=True, exist_ok=True)
            for role, image in prepared.items():
                png = output_directory / ROLE_OUTPUTS[role]
                if role == "album":
                    shutil.copyfile(args.album_source.expanduser().resolve(), png)
                else:
                    image.save(png, "PNG", optimize=True, compress_level=7)
                Path(str(png) + ".meta").write_text(
                    unity_meta(png.name, image.width, image.height), encoding="utf-8"
                )
        report = {
            "ok": True,
            "operation": "validate-only" if args.validate_only else "write",
            "outputDirectory": str(output_directory),
            "assets": audit,
            "policy": "native RGBA only; transparent crop/resize and text composition only; no background removal",
        }
        encoded = json.dumps(report, ensure_ascii=False, indent=2)
        print(encoded)
        if args.report:
            report_path = args.report.expanduser().resolve()
            report_path.parent.mkdir(parents=True, exist_ok=True)
            report_path.write_text(encoded + "\n", encoding="utf-8")
        return 0

    canvases = dict(args.canvas)
    if len(canvases) != len(args.canvas):
        raise AssetRejected("each --canvas role may be specified only once")

    sources: Dict[str, Image.Image] = {}
    source_labels: Dict[str, str] = {}
    master_reports: List[Dict[str, object]] = []
    if args.source:
        ensure_complete_roles(role for role, _ in args.source)
        for role, path in args.source:
            image = open_native_rgba(path.resolve())
            sources[role] = image
            source_labels[role] = str(path.resolve())
    else:
        sheet_path = args.sheet.expanduser().resolve()
        sheet = open_native_rgba(sheet_path)
        sheet_metrics = validate_image(
            sheet,
            str(sheet_path),
            0,
            args.min_transparent,
            args.max_transparent,
            args.checkerboard_threshold,
        )
        master_reports.append({"source": str(sheet_path), **sheet_metrics.as_dict()})
        columns, rows = args.grid
        roles = [canonical_role(item) for item in args.order.split(",") if item.strip()]
        if len(roles) != columns * rows:
            raise AssetRejected(f"--order has {len(roles)} roles but --grid contains {columns * rows} cells")
        ensure_complete_roles(roles)
        for role, cell in zip(roles, split_sheet(sheet, columns, rows)):
            sources[role] = cell
            source_labels[role] = f"{sheet_path}#{role}"

    results: List[Dict[str, object]] = []
    prepared: Dict[str, Image.Image] = {}
    for role in ROLE_OUTPUTS:
        image = sources[role]
        metrics = validate_image(
            image,
            source_labels[role],
            0,
            args.min_transparent,
            args.max_transparent,
            args.checkerboard_threshold,
        )
        crop_box = padded_box(metrics.alpha_bbox, image.width, image.height, args.padding)
        output_image = image.crop(crop_box)
        output_image = resize_to_max_edge(output_image, args.max_edge)
        if role in canvases:
            output_image = fit_on_canvas(output_image, canvases[role], args.allow_upscale)
        prepared[role] = output_image
        results.append(
            {
                "role": role,
                "source": source_labels[role],
                "output": str((args.output / ROLE_OUTPUTS[role]).resolve()),
                "input": metrics.as_dict(),
                "cropBox": list(crop_box),
                "outputSize": list(output_image.size),
            }
        )

    output_directory = args.output.expanduser().resolve()
    if not args.validate_only:
        destinations = []
        for role, image in prepared.items():
            png = output_directory / ROLE_OUTPUTS[role]
            destinations.extend((png, Path(str(png) + ".meta")))
        existing = [str(path) for path in destinations if path.exists()]
        if existing and not args.force:
            raise AssetRejected("refusing to replace existing outputs without --force: " + ", ".join(existing))
        output_directory.mkdir(parents=True, exist_ok=True)
        for role, image in prepared.items():
            png = output_directory / ROLE_OUTPUTS[role]
            image.save(png, "PNG", optimize=True, compress_level=7)
            Path(str(png) + ".meta").write_text(
                unity_meta(png.name, image.width, image.height), encoding="utf-8"
            )

    report = {
        "ok": True,
        "operation": "validate-only" if args.validate_only else "write",
        "outputDirectory": str(output_directory),
        "masters": master_reports,
        "assets": results,
        "policy": "native RGBA only; transparent crop/resize/slice only; no background removal",
    }
    encoded = json.dumps(report, ensure_ascii=False, indent=2)
    print(encoded)
    if args.report:
        report_path = args.report.expanduser().resolve()
        report_path.parent.mkdir(parents=True, exist_ok=True)
        report_path.write_text(encoded + "\n", encoding="utf-8")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except AssetRejected as exc:
        print(json.dumps({"ok": False, "error": str(exc)}, ensure_ascii=False), file=sys.stderr)
        raise SystemExit(2)
