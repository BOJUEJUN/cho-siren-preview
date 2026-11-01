#!/bin/bash
# Compatibility entry point for the independent Claude -> Codex -> Windows PC bridge.
# Usage: pc-run.sh "task" [empty-or-dispatcher-thread] [max-wait-seconds]
set -eu
if [ -n "${2:-}" ] && [ "$2" != "01a11ff1-81ab-70a0-90f2-c4f626289962" ]; then
  echo "请使用独立PC桥接；此入口不再向旧工作对话投递。" >&2
  exit 64
fi
exec /usr/bin/python3 /Users/bojuejun/.local/share/claude-pc-bridge/server.py run "$1" --wait "${3:-900}"
