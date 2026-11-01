#!/bin/bash
# Generate one image with the user's ChatGPT (bundled Codex CLI, built-in image_gen), then de-speckle it.
# Usage: gpt_gen.sh OUT.png "prompt" [--ref IMG]... [--transparent] [--raw] [--strength 1.0]
# Writes OUT.png (cleaned) and OUT.raw.png (as generated). No RunningHub / paid API involved.
set -euo pipefail
CODEX=/Applications/ChatGPT.app/Contents/Resources/codex-cli/bin/codex
HERE="$(cd "$(dirname "$0")" && pwd)"
OUT="$1"; PROMPT="$2"; shift 2
REFS=(); TRANSPARENT=false; CLEAN=true; STRENGTH=1.0
while [ $# -gt 0 ]; do
  case "$1" in
    --ref) REFS+=(--image "$2"); shift 2 ;;
    --transparent) TRANSPARENT=true; shift ;;
    --raw) CLEAN=false; shift ;;
    --strength) STRENGTH="$2"; shift 2 ;;
    *) echo "unknown arg $1" >&2; exit 2 ;;
  esac
done

# Anti-noise rendering block. GPT Image grain is triggered by busy micro-detail, reference images,
# long edit chains and "in the style of X" requests; describe clean surfaces concretely instead.
CLEAN_STYLE="Rendering: clean digital anime cel shading with smooth flat colour fills and soft, clean gradients; crisp, even lineart; matte, smooth surfaces. Keep micro-detail low and large areas simple and calm. Avoid film grain, noise, speckles, dithering, halftone dots, paper or canvas texture, glitter, sparkles, floating particles, dust, bokeh specks, rain streaks, lace, fishnet and other dense fine patterns. No text, letters, watermark, border or UI."

DIR="$(cd "$(dirname "$OUT")" && pwd)"; BASE="$(basename "$OUT" .png)"
LOG="$DIR/$BASE.log"
( cd "$DIR" && "$CODEX" exec --skip-git-repo-check -s read-only \
    -c model_reasoning_effort='"low"' -c approval_policy='"never"' ${REFS[@]+"${REFS[@]}"} -- \
    "用你自带的图片生成功能（image_gen）生成一张图片，调用 image_gen 时设置 transparent_background: $TRANSPARENT。不要搜索或额外引用其他图片。要求：$PROMPT $CLEAN_STYLE 生成后只回复图片保存的完整路径。" > "$LOG" 2>&1 ) || true
SRC="$(grep -oE "/[^ \"']*generated_images/[^ \"']*\.png" "$LOG" | tail -1 || true)"
if [ -z "$SRC" ] || [ ! -f "$SRC" ]; then
  grep -o "moderation_blocked[^\"]*" "$LOG" | head -1 >&2 || true
  echo "FAILED (see $LOG)" >&2; exit 1
fi
cp "$SRC" "$DIR/$BASE.raw.png"
if $CLEAN; then python3 "$HERE/gpt_clean.py" "$DIR/$BASE.raw.png" "$OUT" --strength "$STRENGTH"; else cp "$SRC" "$OUT"; fi
echo "$OUT"
