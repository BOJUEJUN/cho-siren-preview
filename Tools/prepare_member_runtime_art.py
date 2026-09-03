#!/usr/bin/env python3
"""Prepare transparent, runtime-sized member art from the audited source list.

The source PNG files are opened read-only. Visible pixels are never cropped. The
script removes only fully transparent outer canvas, then fits the result inside
a transparent target canvas with a configurable safety border and bottom
alignment.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from PIL import Image


PIPELINE_VERSION = "1.0.0"
# Audited source art includes several legitimate 100–145 MP transparent PNGs.
# Keep Pillow's bomb protection well above this known set while still rejecting
# unexpectedly huge inputs.
Image.MAX_IMAGE_PIXELS = 200_000_000
DEFAULT_SAMPLE_IDS = ("0002", "0005", "0032")
TARGETS = {
    "thumb": (256, 352),
    "portrait": (512, 704),
}

INVENTORY_ROW = re.compile(
    r"^\|\s*(?P<hero_id>\d{4})\s*\|\s*`(?P<filename>[^`]+)`\s*\|\s*"
    r"(?P<width>\d+)×(?P<height>\d+)\s*\|\s*(?P<risk>[^|]+?)\s*\|\s*"
    r"`(?P<source>[^`]+)`\s*\|\s*$"
)


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def relative_posix(path: Path, root: Path) -> str:
    try:
        return path.resolve().relative_to(root.resolve()).as_posix()
    except ValueError:
        return str(path.resolve())


def read_inventory(path: Path) -> list[dict[str, Any]]:
    records: list[dict[str, Any]] = []
    for line in path.read_text(encoding="utf-8-sig").splitlines():
        match = INVENTORY_ROW.match(line)
        if not match:
            continue
        item = match.groupdict()
        records.append(
            {
                "heroId": item["hero_id"],
                "stableId": f"hero-{item['hero_id']}",
                "filename": item["filename"],
                "risk": item["risk"].strip(),
                "inventoryDimensions": [int(item["width"]), int(item["height"])],
                "sourcePath": item["source"],
            }
        )

    ids = [record["heroId"] for record in records]
    if len(records) != 55:
        raise RuntimeError(f"Expected 55 audited heroes, found {len(records)} in {path}")
    if len(ids) != len(set(ids)):
        raise RuntimeError("The inventory contains duplicate hero IDs")
    return records


def save_png_atomic(image: Image.Image, destination: Path) -> None:
    destination.parent.mkdir(parents=True, exist_ok=True)
    temporary = destination.with_name(destination.name + ".tmp")
    image.save(temporary, format="PNG", compress_level=9, optimize=False)
    os.replace(temporary, destination)


def render_to_canvas(
    source: Image.Image,
    canvas_size: tuple[int, int],
    safety_border: float,
) -> tuple[Image.Image, dict[str, Any]]:
    rgba = source.convert("RGBA")
    alpha = rgba.getchannel("A")
    visible_bbox = alpha.getbbox()
    if visible_bbox is None:
        raise RuntimeError("Source image has no visible pixels")

    # Cropping the fully transparent outer canvas is lossless for visible art.
    visible = rgba.crop(visible_bbox)
    canvas_width, canvas_height = canvas_size
    border_x = max(1, round(canvas_width * safety_border))
    border_y = max(1, round(canvas_height * safety_border))
    available_width = canvas_width - (2 * border_x)
    available_height = canvas_height - (2 * border_y)
    scale = min(available_width / visible.width, available_height / visible.height)
    resized_width = max(1, round(visible.width * scale))
    resized_height = max(1, round(visible.height * scale))

    resized = visible.resize((resized_width, resized_height), Image.Resampling.LANCZOS)
    position_x = (canvas_width - resized_width) // 2
    # Bottom alignment keeps feet/skirts stable while retaining the safe edge.
    position_y = canvas_height - border_y - resized_height
    position_y = max(border_y, position_y)

    canvas = Image.new("RGBA", canvas_size, (0, 0, 0, 0))
    canvas.paste(resized, (position_x, position_y))
    rendered_bbox = canvas.getchannel("A").getbbox()
    if rendered_bbox is None:
        raise RuntimeError("Rendered image unexpectedly has no visible pixels")

    return canvas, {
        "sourceVisibleBounds": list(visible_bbox),
        "canvasDimensions": [canvas_width, canvas_height],
        "safetyBorderPixels": [border_x, border_y],
        "scale": round(scale, 8),
        "placement": [position_x, position_y, resized_width, resized_height],
        "renderedVisibleBounds": list(rendered_bbox),
    }


def validate_png(path: Path, expected_size: tuple[int, int], border: tuple[int, int]) -> dict[str, Any]:
    with Image.open(path) as image:
        image.load()
        if image.mode != "RGBA":
            raise RuntimeError(f"{path} is {image.mode}, expected RGBA")
        if image.size != expected_size:
            raise RuntimeError(f"{path} is {image.size}, expected {expected_size}")
        alpha = image.getchannel("A")
        alpha_min, alpha_max = alpha.getextrema()
        bbox = alpha.getbbox()
        if bbox is None or alpha_min != 0 or alpha_max == 0:
            raise RuntimeError(f"{path} does not retain a usable transparent alpha channel")
        left, top, right, bottom = bbox
        border_x, border_y = border
        if left < border_x - 2 or right > expected_size[0] - border_x + 2:
            raise RuntimeError(f"{path} violates the horizontal safety border: {bbox}")
        if top < border_y - 2 or bottom > expected_size[1] - border_y + 2:
            raise RuntimeError(f"{path} violates the vertical safety border: {bbox}")
        return {
            "mode": image.mode,
            "dimensions": list(image.size),
            "alphaExtrema": [alpha_min, alpha_max],
            "visibleBounds": list(bbox),
            "sha256": sha256_file(path),
            "bytes": path.stat().st_size,
        }


def parse_args() -> argparse.Namespace:
    script_path = Path(__file__).resolve()
    default_project = script_path.parent.parent
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project-root", type=Path, default=default_project)
    parser.add_argument("--inventory", type=Path)
    parser.add_argument("--output-root", type=Path)
    parser.add_argument("--manifest", type=Path)
    parser.add_argument("--source-manifest", type=Path)
    parser.add_argument("--ids", help="Comma-separated audited hero IDs")
    parser.add_argument("--sample", action="store_true", help="Process 0002, 0005 and 0032")
    parser.add_argument("--safety-border", type=float, default=0.05)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    project_root = args.project_root.resolve()
    inventory_path = (args.inventory or project_root / "Docs" / "hero-asset-inventory-50.md").resolve()
    output_root = (args.output_root or project_root / "Assets" / "Resources" / "Art" / "Members").resolve()
    manifest_path = (args.manifest or project_root / "Docs" / "member-runtime-art-manifest.json").resolve()
    source_manifest_path = (args.source_manifest or project_root / "Tools" / "member-runtime-art-sources.json").resolve()

    if not 0.0 < args.safety_border < 0.25:
        raise RuntimeError("--safety-border must be greater than 0 and less than 0.25")

    all_records = read_inventory(inventory_path)
    source_manifest = {
        "schemaVersion": 1,
        "inventory": relative_posix(inventory_path, project_root),
        "expectedHeroCount": 55,
        "records": all_records,
    }
    source_manifest_path.parent.mkdir(parents=True, exist_ok=True)
    source_manifest_path.write_text(
        json.dumps(source_manifest, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )

    requested_ids: set[str] | None = None
    if args.sample:
        requested_ids = set(DEFAULT_SAMPLE_IDS)
    if args.ids:
        explicit_ids = {value.strip() for value in args.ids.split(",") if value.strip()}
        requested_ids = explicit_ids if requested_ids is None else requested_ids | explicit_ids
    selected = [record for record in all_records if requested_ids is None or record["heroId"] in requested_ids]
    if requested_ids is not None:
        missing = requested_ids - {record["heroId"] for record in selected}
        if missing:
            raise RuntimeError(f"Unknown hero IDs: {', '.join(sorted(missing))}")

    source_hashes: set[str] = set()
    results: list[dict[str, Any]] = []
    for index, record in enumerate(selected, start=1):
        source_path = Path(record["sourcePath"])
        if not source_path.is_file():
            raise FileNotFoundError(source_path)
        source_hash = sha256_file(source_path)
        if source_hash in source_hashes:
            raise RuntimeError(f"Duplicate source art detected at {source_path}")
        source_hashes.add(source_hash)

        with Image.open(source_path) as source_image:
            source_image.load()
            if list(source_image.size) != record["inventoryDimensions"]:
                raise RuntimeError(
                    f"Inventory dimensions for {record['heroId']} are {record['inventoryDimensions']}, "
                    f"but the source is {list(source_image.size)}"
                )
            source_alpha = source_image.convert("RGBA").getchannel("A").getextrema()
            if source_alpha[0] != 0 or source_alpha[1] == 0:
                raise RuntimeError(f"Source {source_path} has no usable transparent alpha channel")

            outputs: dict[str, Any] = {}
            for output_name, dimensions in TARGETS.items():
                canvas, render_metadata = render_to_canvas(source_image, dimensions, args.safety_border)
                output_path = output_root / record["stableId"] / f"{output_name}.png"
                save_png_atomic(canvas, output_path)
                validation = validate_png(
                    output_path,
                    dimensions,
                    tuple(render_metadata["safetyBorderPixels"]),
                )
                outputs[output_name] = {
                    "path": relative_posix(output_path, project_root),
                    "resourcePath": f"Art/Members/{record['stableId']}/{output_name}",
                    **render_metadata,
                    **validation,
                }

        result = {
            **record,
            "sourceSha256": source_hash,
            "sourceBytes": source_path.stat().st_size,
            "outputs": outputs,
        }
        results.append(result)
        print(f"[{index:02d}/{len(selected):02d}] {record['stableId']} prepared", flush=True)

    manifest = {
        "schemaVersion": 1,
        "pipelineVersion": PIPELINE_VERSION,
        "generatedAtUtc": datetime.now(timezone.utc).isoformat(),
        "inventory": relative_posix(inventory_path, project_root),
        "outputRoot": relative_posix(output_root, project_root),
        "safetyBorderRatio": args.safety_border,
        "fitMode": "contain-no-visible-pixel-crop",
        "alignment": "bottom-center",
        "selectedHeroCount": len(results),
        "expectedFullHeroCount": 55,
        "records": results,
    }
    manifest_path.parent.mkdir(parents=True, exist_ok=True)
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    total_bytes = sum(
        output["bytes"]
        for record in results
        for output in record["outputs"].values()
    )
    print(f"Prepared {len(results)} distinct heroes / {len(results) * len(TARGETS)} PNG files")
    print(f"Output size: {total_bytes / (1024 * 1024):.2f} MiB")
    print(f"Manifest: {manifest_path}")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as error:
        print(f"ERROR: {error}", file=sys.stderr)
        raise
