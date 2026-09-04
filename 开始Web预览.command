#!/bin/zsh
set -euo pipefail

project_dir="$(cd -- "$(dirname -- "$0")" && pwd)"
build_dir="$project_dir/Builds/WebGL"

pause_before_exit() {
  read -r "?按回车键关闭……"
}

if [[ ! -f "$build_dir/index.html" ]]; then
  echo "尚未找到 WebGL 构建：$build_dir/index.html"
  echo "请先完成 WebGL 构建后再运行。"
  pause_before_exit
  exit 1
fi

if ! command -v python3 >/dev/null 2>&1; then
  echo "未找到 Python 3，无法启动本地 Web 预览。"
  echo "请安装 Python 3，或在 Unity 中使用 Build And Run。"
  pause_before_exit
  exit 1
fi

preview_port=""
for candidate_port in {8080..8090}; do
  if ! lsof -nP -iTCP:"$candidate_port" -sTCP:LISTEN >/dev/null 2>&1; then
    preview_port="$candidate_port"
    break
  fi
done

if [[ -z "$preview_port" ]]; then
  echo "8080–8090 端口均被占用，无法启动预览。"
  pause_before_exit
  exit 1
fi

preview_url="http://127.0.0.1:$preview_port/"
echo "正在启动 CHO-SIREN WebGL 预览：$preview_url"
echo "关闭此终端窗口即可停止本地预览服务。"

python3 -m http.server "$preview_port" --bind 127.0.0.1 --directory "$build_dir" &
server_pid=$!
trap 'kill "$server_pid" >/dev/null 2>&1 || true' EXIT INT TERM

for _ in {1..20}; do
  if curl --silent --fail --max-time 1 "$preview_url" >/dev/null 2>&1; then
    break
  fi
  if ! kill -0 "$server_pid" >/dev/null 2>&1; then
    echo "本地预览服务启动失败。"
    pause_before_exit
    exit 1
  fi
  sleep 0.1
done

if ! curl --silent --fail --max-time 1 "$preview_url" >/dev/null 2>&1; then
  echo "本地预览服务未能及时响应。"
  pause_before_exit
  exit 1
fi

open "$preview_url"
wait "$server_pid"
