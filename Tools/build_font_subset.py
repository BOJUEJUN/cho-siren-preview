"""Build Assets/Resources/Fonts/NotoSansSC-Subset.otf from NotoSansSC-Regular.otf.

The character set is the union of
  * every non-ASCII character found in project source, data JSON, docs and the
    Codex staging folder (so all current UI copy is covered),
  * GB2312 level-1 common hanzi (rows B0-D7, 3755 characters),
  * printable ASCII, full-width punctuation / CJK symbols, and a few UI glyphs.

The original font is never modified or deleted. Requires fontTools (pyftsubset):
    python -m venv %TEMP%\\chosiren-fonttools-venv
    %TEMP%\\chosiren-fonttools-venv\\Scripts\\python -m pip install fonttools brotli
    %TEMP%\\chosiren-fonttools-venv\\Scripts\\python Tools\\build_font_subset.py
"""

from __future__ import annotations

import argparse
import glob
import json
import os
import secrets
import sys
from pathlib import Path

PROJECT_ROOT = Path(__file__).resolve().parents[1]
FONT_DIR = PROJECT_ROOT / "Assets" / "Resources" / "Fonts"
SOURCE_FONT = FONT_DIR / "NotoSansSC-Regular.otf"
OUTPUT_FONT = FONT_DIR / "NotoSansSC-Subset.otf"

SCAN_GLOBS = [
    str(PROJECT_ROOT / "Assets" / "Scripts" / "**" / "*.cs"),
    str(PROJECT_ROOT / "Assets" / "Resources" / "Data" / "**" / "*.json"),
    str(PROJECT_ROOT / "Docs" / "*.md"),
    os.path.join(os.environ.get("LOCALAPPDATA", ""), "Temp", "chosiren-stage", "**", "*.cs"),
    os.path.join(os.environ.get("LOCALAPPDATA", ""), "Temp", "chosiren-stage", "**", "*.json"),
]

EXTRA_SYMBOLS = "♪♫◇♡☆★×▶◀●○◆■□▲▼→←↑↓…—–·•‰℃°±÷≈≠≤≥∞√∑∏∈∵∴∠⊙※§¶†‡「」『』【】〈〉《》〔〕"


def gb2312_level1() -> set[str]:
    chars: set[str] = set()
    for row in range(0xB0, 0xD8):          # B0..D7
        for cell in range(0xA1, 0xFF):     # A1..FE
            if row == 0xD7 and cell > 0xF9:
                break
            try:
                chars.add(bytes([row, cell]).decode("gb2312"))
            except UnicodeDecodeError:
                pass
    return chars


def scan_project_chars() -> tuple[set[str], int]:
    chars: set[str] = set()
    files = 0
    for pattern in SCAN_GLOBS:
        for path in glob.glob(pattern, recursive=True):
            try:
                text = Path(path).read_text(encoding="utf-8", errors="ignore")
            except OSError:
                continue
            files += 1
            chars.update(ch for ch in text if ord(ch) > 0x7F and not ch.isspace())
    return chars, files


def build_charset() -> dict[str, object]:
    project_chars, scanned_files = scan_project_chars()
    gb = gb2312_level1()
    ascii_printable = {chr(c) for c in range(0x20, 0x7F)}
    fullwidth = {chr(c) for c in range(0xFF01, 0xFF5F)}          # ！ … ～
    cjk_punct = {chr(c) for c in range(0x3000, 0x3040)}          # 　、。〃 … 〿
    general_punct = {chr(c) for c in range(0x2010, 0x2028)} | {chr(c) for c in range(0x2030, 0x205F)}
    extra = set(EXTRA_SYMBOLS)

    charset = project_chars | gb | ascii_printable | fullwidth | cjk_punct | general_punct | extra
    charset = {c for c in charset if not (0xD800 <= ord(c) <= 0xDFFF)}  # drop stray surrogates
    return {
        "charset": charset,
        "scannedFiles": scanned_files,
        "projectNonAscii": len(project_chars),
        "gb2312Level1": len(gb),
        "projectNotInGb2312": len(project_chars - gb),
    }


def run_subset(charset: set[str], keep_hinting: bool) -> None:
    from fontTools import subset  # imported lazily so --dry-run works without fontTools

    unicodes_file = Path(os.environ.get("TEMP", ".")) / "chosiren-font-unicodes.txt"
    unicodes_file.write_text("\n".join(f"U+{ord(c):04X}" for c in sorted(charset)), encoding="utf-8")

    args = [
        str(SOURCE_FONT),
        f"--unicodes-file={unicodes_file}",
        f"--output-file={OUTPUT_FONT}",
        "--layout-features=*",
        "--glyph-names",
        "--name-IDs=*",
        "--name-legacy",
        "--notdef-outline",
        "--recommended-glyphs",
        "--no-recalc-bounds",
    ]
    if not keep_hinting:
        args.append("--no-hinting")
    subset.main(args)


def write_meta(source_meta: Path, target_meta: Path) -> str:
    if target_meta.exists():
        existing = target_meta.read_text(encoding="utf-8")
        for line in existing.splitlines():
            if line.startswith("guid: "):
                return line[6:].strip()
    text = source_meta.read_bytes().decode("utf-8")
    new_guid = secrets.token_hex(16)
    lines = text.split("\n")
    for i, line in enumerate(lines):
        if line.startswith("guid: "):
            lines[i] = f"guid: {new_guid}"
            break
    target_meta.write_bytes("\n".join(lines).encode("utf-8"))
    return new_guid


def count_cmap(path: Path) -> int:
    from fontTools.ttLib import TTFont

    with TTFont(str(path), lazy=True) as font:
        return len(font.getBestCmap())


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--dry-run", action="store_true", help="only report the character set")
    parser.add_argument("--keep-hinting", action="store_true", help="keep CFF hints (larger file)")
    args = parser.parse_args()

    if not SOURCE_FONT.exists():
        print(f"source font missing: {SOURCE_FONT}", file=sys.stderr)
        return 2

    info = build_charset()
    charset: set[str] = info.pop("charset")  # type: ignore[assignment]
    report: dict[str, object] = {
        "sourceFont": str(SOURCE_FONT),
        "sourceBytes": SOURCE_FONT.stat().st_size,
        "requestedCharacters": len(charset),
        **info,
    }

    if not args.dry_run:
        run_subset(charset, args.keep_hinting)
        report["outputFont"] = str(OUTPUT_FONT)
        report["outputBytes"] = OUTPUT_FONT.stat().st_size
        report["outputCmapCharacters"] = count_cmap(OUTPUT_FONT)
        report["sourceCmapCharacters"] = count_cmap(SOURCE_FONT)
        report["ratio"] = round(report["outputBytes"] / report["sourceBytes"], 4)  # type: ignore[operator]
        report["metaGuid"] = write_meta(SOURCE_FONT.with_suffix(".otf.meta"), OUTPUT_FONT.with_suffix(".otf.meta"))

    print(json.dumps(report, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    sys.exit(main())
