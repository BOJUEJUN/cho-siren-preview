#!/usr/bin/env bash
# Builds the Apple WebKit variant of the transparent character clips.
#
# Safari and every iOS browser ignore the alpha channel of VP9 WebM, so each clip is
# re-packed side by side as [colour | 32 px black gap | alpha stored as luma] and
# encoded as 8-bit HEVC (hvc1) with AAC audio. character-moments.js recombines the
# two halves on the GPU before handing the frame to Unity. The colour planes are
# taken straight from the WebM decode, without an RGB round trip.
#
# Usage: scripts/build-packed-video.sh [clip ...]   (default: all three clips)
#        CRF=18 by default; lower is larger and closer to the WebM source.
set -euo pipefail
cd "$(dirname "$0")/../media"

gap=32   # must match PACKED_GAP in character-moments.js
crf="${CRF:-18}"
clips=("$@")
if [ ${#clips[@]} -eq 0 ]; then clips=(catalena-look catalena-whisper catalena-live); fi

for clip in "${clips[@]}"; do
  ffmpeg -hide_banner -v error -y -c:v libvpx-vp9 -i "$clip.webm" -filter_complex \
    "[0:v]format=yuva420p,split=2[c][a];[c]format=yuv420p,pad=iw+$gap:ih:0:0:black[cp];[a]alphaextract,format=yuv420p[am];[cp][am]hstack=inputs=2[v]" \
    -map "[v]" -map 0:a -c:v libx265 -preset slow -crf "$crf" -x265-params log-level=error \
    -tag:v hvc1 -pix_fmt yuv420p -color_range tv -c:a aac -b:a 160k -movflags +faststart \
    "$clip.packed.mp4"
  echo "$clip.packed.mp4 $(wc -c < "$clip.packed.mp4") bytes"
done
