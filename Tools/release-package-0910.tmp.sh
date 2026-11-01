#!/bin/bash
# Build the Android / Windows / Mac installers for the current bundleVersion, upload them to Tencent COS
# and publish app-version.json so installed clients see 「发现新版本」.
#   bash Tools/release-installers.sh "更新说明"
# Run from anywhere; never run while another Unity batch job is using the project.
set -euo pipefail
cd "$(dirname "$0")/.."
NOTES="${1:-新版本已发布。}"
U=/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity
V=$(grep -m1 "bundleVersion:" ProjectSettings/ProjectSettings.asset | awk '{print $2}')
R=Builds/Release-$V
mkdir -p "$R" Builds/qa-logs

build() { # target method output log
  "$U" -batchmode -nographics -quit -projectPath . -buildTarget "$1" -executeMethod "ChoSiren.Editor.ChoSirenBuild.$2" \
    -buildOutput "$PWD/$3" -logFile "Builds/qa-logs/$4" >/dev/null 2>&1 || true
  grep -q CHO_SIREN_BUILD_OK "Builds/qa-logs/$4" || { echo "build failed: $2 (see Builds/qa-logs/$4)"; exit 1; }
}

build Android BuildAndroid "$R/CHO-SIREN-$V.apk" "installer-android-$V.log"
rm -rf "Builds/Windows-$V"; build Win64 BuildWindows "Builds/Windows-$V/CHO-SIREN.exe" "installer-win-$V.log"
rm -rf "Builds/macOS-$V"; build OSXUniversal BuildMacOS "Builds/macOS-$V/CHO-SIREN.app" "installer-mac-$V.log"

# Rounded .ico: shipped inside the game folder too, so in-game updates refresh the shortcut icon.
ICO=$(mktemp -d)/icon.ico
python3 -c "import sys; from PIL import Image; Image.open('Assets/Art/AppIcon/app-icon-rounded.png').convert('RGBA').save(sys.argv[1], sizes=[(16,16),(24,24),(32,32),(48,48),(64,64),(128,128),(256,256)])" "$ICO"
cp "$ICO" "Builds/Windows-$V/icon.ico"

# Packaging runs as two parallel jobs (Windows: zip + NSIS setup; Mac: rounded icns + .dmg + update zip).
package_windows() {
  # Unity's support files (editor launcher, preview notes, 开始游戏.cmd) are not for players.
  rm -f "Builds/Windows-$V/预览说明.txt" "Builds/Windows-$V/打开Unity编辑器.cmd" "Builds/Windows-$V/开始游戏.cmd"
  sed "s/v0\.0\.0/v$V/" Tools/installer-notes/windows.txt > "Builds/Windows-$V/使用说明.txt"
  rm -f "$R/CHO-SIREN-Windows-$V.zip" "$R/CHO-SIREN-Windows-Setup-$V.exe"
  # The zip is the in-game update package: game files + 使用说明 + icon.ico.
  (cd "Builds/Windows-$V" && zip -qr "../../$R/CHO-SIREN-Windows-$V.zip" . -x "CHO-SIREN_BackUpThisFolder_ButDontShipItWithYourGame/*")
  # Installer (NSIS, per-user, desktop + Start-menu shortcuts, uninstaller).
  LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8 /opt/homebrew/bin/makensis -V2 -DVERSION="$V" -DSRC="$PWD/Builds/Windows-$V" -DICON="$ICO" \
    -DOUT="$PWD/$R/CHO-SIREN-Windows-Setup-$V.exe" Tools/windows-installer.nsi
}

package_mac() {
  # .dmg: rounded macOS icon, app named 幻域魅声, ad-hoc signed, drag-to-Applications layout.
  sed "s/v0\.0\.0/v$V/" Tools/installer-notes/mac.txt > "Builds/macOS-$V/使用说明.txt"
  # Kept under the release folder: publish_files.py reads the processed 幻域魅声.app from here.
  local W="$PWD/$R/mac-stage"; rm -rf "$W"; mkdir -p "$W"
  python3 - "$W" <<'EOF2'
import sys, os
from PIL import Image, ImageDraw, ImageFilter
w = sys.argv[1]
src = Image.open('Assets/Art/AppIcon/app-icon.png').convert('RGBA')
S, inner, r = 1024, 824, 185
canvas = Image.new('RGBA', (S, S), (0, 0, 0, 0))
shadow = Image.new('RGBA', (S, S), (0, 0, 0, 0))
ImageDraw.Draw(shadow).rounded_rectangle(((S - inner) // 2, (S - inner) // 2 + 12, (S + inner) // 2, (S + inner) // 2 + 12), r, fill=(0, 0, 0, 110))
canvas.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(14)))
mask = Image.new('L', (inner, inner), 0)
ImageDraw.Draw(mask).rounded_rectangle((0, 0, inner - 1, inner - 1), r, fill=255)
canvas.paste(src.resize((inner, inner), Image.LANCZOS), ((S - inner) // 2, (S - inner) // 2), mask)
d = os.path.join(w, 'icon.iconset'); os.makedirs(d)
for s in [16, 32, 128, 256, 512]:
    canvas.resize((s, s), Image.LANCZOS).save(f'{d}/icon_{s}x{s}.png')
    canvas.resize((s * 2, s * 2), Image.LANCZOS).save(f'{d}/icon_{s}x{s}@2x.png')
EOF2
  iconutil -c icns "$W/icon.iconset" -o "$W/PlayerIcon.icns"
  mkdir -p "$W/dmg"
  ditto "Builds/macOS-$V/CHO-SIREN.app" "$W/dmg/幻域魅声.app"
  cp "$W/PlayerIcon.icns" "$W/dmg/幻域魅声.app/Contents/Resources/PlayerIcon.icns"
  xattr -cr "$W/dmg/幻域魅声.app"
  codesign --force --deep -s - "$W/dmg/幻域魅声.app"
  ln -s /Applications "$W/dmg/应用程序"
  cp "Builds/macOS-$V/使用说明.txt" "$W/dmg/使用说明.txt"
  rm -f "$R/CHO-SIREN-Mac-$V.dmg" "$R/CHO-SIREN-Mac-update-$V.zip"
  hdiutil create -volname "幻域魅声" -srcfolder "$W/dmg" -ov -format UDZO -imagekey zlib-level=9 "$R/CHO-SIREN-Mac-$V.dmg" >/dev/null
  # In-game updater package for Mac: just the processed 幻域魅声.app.
  (cd "$W/dmg" && ditto -c -k --keepParent "幻域魅声.app" "$OLDPWD/$R/CHO-SIREN-Mac-update-$V.zip")
}

package_windows & WINJOB=$!
package_mac & MACJOB=$!
wait $WINJOB || { echo "Windows packaging failed"; exit 1; }
wait $MACJOB || { echo "Mac packaging failed"; exit 1; }

