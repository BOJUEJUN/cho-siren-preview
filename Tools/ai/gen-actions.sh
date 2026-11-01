#!/bin/bash
# Generates the three lobby performances from the green-screen key frame, each starting
# and ending on it, retrying while the generation service is full.
set -u
K=~/Downloads/cho-siren-ai/catalena-keyframe-green.png
OUT=~/Downloads/cho-siren-ai/actions
RULES="镜头是锁死在三脚架上的静止镜头，取景、人物在画面中的大小和位置全程不变。动作结束时角色必须回到与首帧完全相同的姿势。服装、画风与首帧一致，脸部和手部不变形。背景始终是纯绿色 #00FF00，不要阴影。"
NEG="镜头推拉, 缩放, 变焦, 镜头晃动, 人物变大变小, 离开画面, 背景变化, 特效, 粒子, 文字, 水印, 脸部变形, 手部变形, 多余的手指, 换装"
declare -a NAMES=(catalena-look catalena-whisper catalena-live)
declare -a MOVES=(
 "角色先微微歪头看向镜头，抬起一只手把耳边的头发轻轻拨到耳后，露出温柔的微笑，然后慢慢放下手回到原来的姿势。"
 "角色身体稍稍前倾靠近镜头，一只手放在嘴边像在说悄悄话，俏皮地眨一下眼，然后收回手站直，回到原来的姿势。"
 "角色一只手扶住耳麦，对着头戴麦克风开心地唱歌，身体随节奏轻轻左右摇摆，唱完放下手，回到原来的姿势。"
)
for i in 0 1 2; do
  dir="$OUT/${NAMES[$i]}"
  for try in 1 2 3 4 5 6 7 8 9 10; do
    ls "$dir"/*.mp4 >/dev/null 2>&1 && break
    echo "== ${NAMES[$i]} try $try"
    ~/cho-siren-unity/Tools/ai/gen-video.sh "$dir" 5 "$K" "$K" "${MOVES[$i]}$RULES" "$NEG" 2>&1 | tail -4
    ls "$dir"/*.mp4 >/dev/null 2>&1 || sleep 180
  done
done
ls -la "$OUT"/*/
