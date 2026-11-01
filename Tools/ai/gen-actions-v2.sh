#!/bin/bash
# Second batch of lobby performances, same key frame and rules as gen-actions.sh.
set -u
K=~/Downloads/cho-siren-ai/catalena-keyframe-green.png
OUT=~/Downloads/cho-siren-ai/actions-v2
RULES="镜头是锁死在三脚架上的静止镜头，取景、人物在画面中的大小和位置全程不变。动作结束时角色必须回到与首帧完全相同的姿势。服装、画风与首帧一致，脸部和手部不变形。背景始终是纯绿色 #00FF00，不要阴影。"
NEG="镜头推拉, 缩放, 变焦, 镜头晃动, 人物变大变小, 离开画面, 背景变化, 特效, 粒子, 文字, 水印, 脸部变形, 手部变形, 多余的手指, 换装"
NAMES=(catalena-wave catalena-heart catalena-sway)
MOVES=(
 "角色看向镜头开心地笑，抬起一只手在脸旁轻快地挥手打招呼，挥两三下后放下手，回到原来的姿势。"
 "角色害羞地笑着，双手在胸前比出一个爱心手势，轻轻歪头眨一下眼，然后放下双手，回到原来的姿势。"
 "角色闭上眼睛随着音乐节奏轻轻左右摇摆身体和头部，头发和身后的狐狸尾巴跟着摆动，最后睁开眼睛，回到原来的姿势。"
)
for i in 0 1 2; do
  dir="$OUT/${NAMES[$i]}"
  for try in 1 2 3 4 5 6 7 8; do
    ls "$dir"/*.mp4 >/dev/null 2>&1 && break
    echo "== ${NAMES[$i]} try $try"
    ~/cho-siren-unity/Tools/ai/gen-video.sh "$dir" 5 "$K" "$K" "${MOVES[$i]}$RULES" "$NEG" 2>&1 | tail -3
    ls "$dir"/*.mp4 >/dev/null 2>&1 || sleep 150
  done
done
ls -la "$OUT"/*/
