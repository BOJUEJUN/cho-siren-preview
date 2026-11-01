#!/bin/zsh
# Lobby idol clips at full quality (2026-11-01 video-hq, 用户: "最高清晰度，不要压缩画质").
# matte.swift does the cut-out (per-frame backdrop sample, Vision mask + white key, ping-pong loop) and pipes the
# stacked colour|alpha frames uncompressed into x264:
#   - source-native colour width (never upscaled), H.264 High profile, level 5.0 (the lowest level these frame sizes
#     allow: 2496x1664 = 16224 MB per frame; signalling 5.1 would only make stricter decoders refuse), yuv420p 8-bit, BT.709 tagged,
#     4 reference frames / 3 B-frames, so every platform's VideoPlayer (and browsers on WebGL) hardware-decodes it;
#   - CRF 12 (opaque face pixels vs the source MOV in coded YUV: PSNR >= 43 dB, SSIM >= 0.989 on every forward-pass
#     frame; see Builds/qa-logs/zj-1101b/video-hq/quality.jsonl), alpha half 5 QP finer (addroi qoffset -0.2 = -5 QP)
#     so compression neither softens nor shimmers the cut-out edge;
#   - faststart (moov first) so a browser can start the clip before the whole file has arrived.
# usage: FFMPEG=/path/to/ffmpeg(with libx264) Tools/lobbyvideo/export_hq.sh [clip ...]   (default: all eight)
set -e -o pipefail
cd "$(dirname "$0")/../.."
: ${FFMPEG:?set FFMPEG to an ffmpeg build with libx264}
: ${CRF:=12}
: ${ALPHA_QOFFSET:=-0.2}
SRC=${SRC:-$HOME/Downloads}
OUT=${OUT:-Assets/StreamingAssets/LobbyIdols}
typeset -A MOV
MOV=(elf-a f8663d7423450c8b8cf29ee97f0df1b5 elf-b 101afc5f83c17c9f6a7c34ffd980d3af
     mermaid-a d4548a559349a12bc39384e8ccd0482e mermaid-b 0822aadb997814c1c98acd4fec3012c0
     catalena-a 22da77388e65f9cede0834032430b90e catalena-b c68b407e511c77cd1cd74f5e882e3d99
     lilith-a 922e8407cd3056a7b8375a3e5f49f38b lilith-b f24fa22c75728f4d07a592cccb74ad0e)
clips=(elf-a elf-b mermaid-a mermaid-b catalena-a catalena-b lilith-a lilith-b)
(( $# )) && clips=($@)
MATTE=${TMPDIR:-/tmp}/lobby-matte
[[ $MATTE -nt Tools/lobbyvideo/matte.swift ]] || swiftc -O Tools/lobbyvideo/matte.swift -o $MATTE
for clip in $clips; do
  src=$SRC/${MOV[$clip]}.mov
  read W H FPS <<< "$($MATTE $src --probe)"
  echo "== $clip ${W}x${H} @ $FPS fps"
  $MATTE $src - $((W / 2)) - |
    $FFMPEG -hide_banner -loglevel error -y -f rawvideo -pix_fmt bgra -s ${W}x${H} -framerate $FPS -i - \
      -vf "scale=out_color_matrix=bt709:out_range=tv:flags=bicubic+accurate_rnd+full_chroma_int,format=yuv420p,addroi=x=iw/2:y=0:w=iw/2:h=ih:qoffset=$ALPHA_QOFFSET" \
      -c:v libx264 -preset veryslow -profile:v high -level:v 5.0 -crf $CRF \
      -x264-params "keyint=48:min-keyint=1:ref=4:bframes=3:colorprim=bt709:transfer=bt709:colormatrix=bt709:range=tv:vbv-maxrate=80000:vbv-bufsize=80000" \
      -colorspace bt709 -color_primaries bt709 -color_trc bt709 -color_range tv -movflags +faststart -an $OUT/$clip.mp4
  ls -l $OUT/$clip.mp4
done
