#!/bin/bash
# 改完代码后双击：重出 WebGL 包到 Builds/WebGL，然后刷新 http://127.0.0.1:58399 即可
cd "$(dirname "$0")"
"/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -projectPath "$PWD" \
  -executeMethod ChoSiren.Editor.ChoSirenBuild.BuildWebGL \
  -logFile "$PWD/Logs/build-webgl-preview.log" -quit
echo "构建完成 → 刷新 http://127.0.0.1:58399"
