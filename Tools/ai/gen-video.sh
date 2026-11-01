#!/bin/bash
# Generates one video through the CodeBuddy CLI's VideoGen tool (the user's own
# CodeBuddy/WorkBuddy account and credits), without opening the WorkBuddy app.
#
#   Tools/ai/gen-video.sh <out_dir> <seconds> <first_frame> <last_frame|-> "<prompt>" ["<negative prompt>"]
#
# Only ToolSearch, VideoGen and its runner are enabled, so skipping prompts is limited to them.
# Prints CodeBuddy's final answer, which names the saved file.
set -euo pipefail
out_dir="$1"; seconds="$2"; first="$3"; last="$4"; prompt="$5"; negative="${6:-}"
node_dir=/Applications/Unity/Hub/Editor/6000.6.0f1/PlaybackEngines/WebGLSupport/BuildTools/Emscripten/node
command -v node >/dev/null || export PATH="$node_dir:$PATH"
mkdir -p "$out_dir"
last_line=""
[ "$last" != "-" ] && last_line="- last_image: $last"
cd "$out_dir"
exec ~/.codebuddy-cli/bin/codebuddy -p --max-turns 6 --tools "ToolSearch,VideoGen,DeferExecuteTool" --permission-mode bypassPermissions <<PROMPT
先用 ToolSearch 加载 VideoGen（select:VideoGen），然后只调用一次 VideoGen，参数严格如下，不要改写提示词，不要做别的：
- prompt: $prompt
- negative_prompt: $negative
- image: $first
$last_line
- seconds: $seconds
- resolution: 1080P
- enable_audio: false
- output_dir: $out_dir
完成后只回复：保存的视频文件绝对路径、所用模型名、消耗积分（如果有）。
PROMPT
