#!/bin/bash
# Build the iOS simulator app (arm64) into Builds/iOS-DerivedData/Build/Products/Release-iphonesimulator/CHO-SIREN.app.
#   bash Tools/ios-sim.sh [logdir]
set -u
cd "$(dirname "$0")/.."
LOG=${1:-Builds/qa-logs}; mkdir -p "$LOG"
/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath . -buildTarget iOS \
  -executeMethod ChoSiren.Editor.ChoSirenBuild.BuildIOS -iosSimulator -logFile "$LOG/ios.log" -quit >/dev/null 2>&1
grep -E "CHO_SIREN_BUILD_OK|error CS|BuildFailedException" "$LOG/ios.log" | head -3
cd Builds/iOS && xcodebuild -project Unity-iPhone.xcodeproj -scheme Unity-iPhone -configuration Release -sdk iphonesimulator \
  -destination 'generic/platform=iOS Simulator' -derivedDataPath ../iOS-DerivedData CODE_SIGNING_ALLOWED=NO ARCHS=arm64 \
  ONLY_ACTIVE_ARCH=NO build > "../../$LOG/xcb.log" 2>&1
echo xcode=$?; grep -m5 "error:" "../../$LOG/xcb.log"
