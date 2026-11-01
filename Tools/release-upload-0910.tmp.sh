#!/bin/bash
set -euo pipefail
cd /Users/bojuejun/cho-siren-unity
NOTES="${1:-优化文字清晰度与公告，修复输入光标，精简开场剧情并修复安卓下载。}"
V=0.9.10
R=Builds/Release-$V
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

