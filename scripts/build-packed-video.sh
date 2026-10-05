#!/usr/bin/env bash
# Builds the Apple WebKit variants of the transparent character clips.
#
# Safari and every iOS browser ignore the alpha channel of VP9 WebM, so each clip is
# re-packed side by side as [colour | 32 px black gap | alpha stored as luma].
# character-moments.js recombines the two halves on the GPU before handing the frame
# to Unity. The colour planes are taken straight from the WebM decode, without an RGB
# round trip.
#
#   <clip>.packed.webm  VP9 + the source's Opus audio. Used wherever WebM plays
#                       (Safari 14.1+ on macOS, iOS 17.4+): the same WebKit player
#                       that already played the plain WebM clips on iPhone.
#   <clip>.packed.mp4   8-bit HEVC (hvc1) + AAC, for Safari without WebM and as the
#                       fallback when the WebM fails to load.
#
# Usage: scripts/build-packed-video.sh [clip ...]   (default: all three clips)
#        FORMATS="webm mp4" by default; CRF=18 for both (lower is larger and closer
#        to the WebM source).
set -euo pipefail
cd "$(dirname "$0")/../media"

gap=32   # must match PACKED_GAP in character-moments.js
crf="${CRF:-18}"
formats="${FORMATS:-webm mp4}"
clips=("$@")
if [ ${#clips[@]} -eq 0 ]; then clips=(catalena-look catalena-whisper catalena-live); fi
pack="[0:v]format=yuva420p,split=2[c][a];[c]format=yuv420p,pad=iw+$gap:ih:0:0:black[cp];[a]alphaextract,format=yuv420p[am];[cp][am]hstack=inputs=2[v]"

for clip in "${clips[@]}"; do
  for format in $formats; do
    case "$format" in
      webm) codec=(-c:v libvpx-vp9 -crf "$crf" -b:v 0 -deadline good -cpu-used 2 -row-mt 1 -tile-columns 2
                   -pix_fmt yuv420p -color_range tv -c:a copy -f webm) ;;
      mp4)  codec=(-c:v libx265 -preset slow -crf "$crf" -x265-params log-level=error -tag:v hvc1
                   -pix_fmt yuv420p -color_range tv -c:a aac -b:a 160k -movflags +faststart) ;;
      *) echo "unknown format: $format" >&2; exit 1 ;;
    esac
    ffmpeg -hide_banner -v error -y -c:v libvpx-vp9 -i "$clip.webm" -filter_complex "$pack" \
      -map "[v]" -map 0:a "${codec[@]}" "$clip.packed.$format"
    echo "$clip.packed.$format $(wc -c < "$clip.packed.$format") bytes"
  done
done
