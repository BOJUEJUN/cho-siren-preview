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

# Parallel uploads (each prints its public URL into a file), then server-side copies for the *-latest
# names the official site links (seconds, no second upload).
UP=$(mktemp -d)
up() { python3 Tools/cos_upload.py "$1" "$2" > "$UP/$3" || { echo "upload failed: $1" > "$UP/$3.err"; }; }
up "$R/CHO-SIREN-$V.apk" "releases/CHO-SIREN-$V.apk" apk &
# COS's default domain rejects public APK GETs. Archive it there, but publish
# an installable GitHub release asset for the website and Android update link.
( python3 Tools/github_android.py "$R/CHO-SIREN-$V.apk" "$V" > "$UP/apk-public" || echo "Android public download failed" > "$UP/apk-public.err" ) &
up "$R/CHO-SIREN-Windows-$V.zip" "releases/CHO-SIREN-Windows-$V.zip" win &
up "$R/CHO-SIREN-Windows-Setup-$V.exe" "releases/CHO-SIREN-Windows-Setup-$V.exe" winsetup &
up "$R/CHO-SIREN-Mac-$V.dmg" "releases/CHO-SIREN-Mac-$V.dmg" mac &
up "$R/CHO-SIREN-Mac-update-$V.zip" "releases/CHO-SIREN-Mac-update-$V.zip" macup &
# Delta-update file lists (+ only the files COS does not have yet). Clients from 0.7.2 use these;
# older ones still take the whole zips above.
( python3 Tools/publish_files.py windows "Builds/Windows-$V" "$V" > "$UP/winfiles" || echo "windows file list failed" > "$UP/winfiles.err" ) &
( python3 Tools/publish_files.py mac "$R/mac-stage/dmg/幻域魅声.app" "$V" > "$UP/macfiles" || echo "mac file list failed" > "$UP/macfiles.err" ) &
wait
if ls "$UP"/*.err >/dev/null 2>&1; then cat "$UP"/*.err; exit 1; fi
APK=$(cat "$UP/apk-public"); WIN=$(cat "$UP/win"); WINSETUP=$(cat "$UP/winsetup"); MAC=$(cat "$UP/mac"); MACUP=$(cat "$UP/macup")
WINFILES=$(cat "$UP/winfiles"); MACFILES=$(cat "$UP/macfiles")
python3 Tools/cos_upload.py --copy "releases/CHO-SIREN-$V.apk" releases/CHO-SIREN-latest.apk >/dev/null
python3 Tools/cos_upload.py --copy "releases/CHO-SIREN-Windows-$V.zip" releases/CHO-SIREN-Windows-latest.zip >/dev/null
python3 Tools/cos_upload.py --copy "releases/CHO-SIREN-Windows-Setup-$V.exe" releases/CHO-SIREN-Windows-Setup-latest.exe >/dev/null
python3 Tools/cos_upload.py --copy "releases/CHO-SIREN-Mac-$V.dmg" releases/CHO-SIREN-Mac-latest.dmg >/dev/null
MANIFEST=$(mktemp -d)/app-version.json
python3 - "$MANIFEST" "$V" "$APK" "$WINSETUP" "$MAC" "$NOTES" "$MACUP" "$WIN" "$WINFILES" "$MACFILES" <<'EOF'
import json, sys, urllib.request
path, version, apk, winsetup, mac, notes, macup, win, winfiles, macfiles = sys.argv[1:11]
# Keep a changelog so a player several versions behind sees everything they are about to get.
changelog = []
try:
    old = json.load(urllib.request.urlopen("https://cho-siren-1318245308.cos.ap-guangzhou.myqcloud.com/app-version.json", timeout=15))
    changelog = old.get("changelog") or ([{"version": old["version"], "notes": old.get("notes", "")}] if old.get("version") else [])
except Exception:
    pass
changelog = [e for e in changelog if e.get("version") != version]
changelog.insert(0, {"version": version, "notes": notes})
# windowsUpdate / macUpdate: zips the in-game updater swaps in place (Windows zip = the game folder).
json.dump({"version": version, "android": apk, "windows": winsetup, "mac": mac,
           "windowsUpdate": win, "macUpdate": macup, "windowsFiles": winfiles, "macFiles": macfiles,
           "notes": notes, "changelog": changelog[:30]},
          open(path, "w"), ensure_ascii=False, indent=2)
EOF
python3 Tools/cos_upload.py "$MANIFEST" app-version.json >/dev/null
ls -la "$R"
echo "android $APK"; echo "windows $WINSETUP (zip $WIN)"; echo "mac $MAC"

# Storage: keep only the newest 3 versions of the installer packages.
python3 Tools/cos_cleanup.py 3

# Local disk: keep only the newest 3 Release-/Windows-/macOS- build folders.
python3 - <<'PY'
import os, re, shutil
root = "Builds"
for prefix in ("Release-", "Windows-", "macOS-"):
    found = sorted(((tuple(map(int, m.groups())), n) for n in os.listdir(root)
                    for m in [re.match(re.escape(prefix) + r"(\d+)\.(\d+)\.(\d+)$", n)] if m), reverse=True)
    for _, name in found[3:]:
        shutil.rmtree(os.path.join(root, name))
PY
