; CHO-SIREN 幻域魅声 Windows installer (2026-10-22), built on the Mac with Homebrew makensis:
;   makensis -DVERSION=0.6.9 -DSRC=<game folder> -DICON=<icon.ico> -DOUT=<setup.exe> Tools/windows-installer.nsi
; Installs per user into %LOCALAPPDATA%\Programs\幻域魅声 (no admin prompt), so the in-game updater can
; swap files there later. Desktop + Start-menu shortcuts and an uninstaller registered under HKCU.
; Saves live in the registry (Unity PlayerPrefs), so reinstalling or updating keeps progress.

Unicode true
SetCompressor /SOLID lzma
RequestExecutionLevel user

!include "MUI2.nsh"

!define APPNAME "幻域魅声"
!define EXE "CHO-SIREN.exe"
!define UNINSTKEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\CHO-SIREN"

Name "${APPNAME} CHO-SIREN"
OutFile "${OUT}"
InstallDir "$LOCALAPPDATA\Programs\${APPNAME}"
InstallDirRegKey HKCU "Software\CHO-SIREN-Installer" "InstallDir"
BrandingText "CHO-SIREN ${VERSION}"

VIProductVersion "${VERSION}.0"
VIAddVersionKey "ProductName" "${APPNAME} CHO-SIREN"
VIAddVersionKey "FileDescription" "${APPNAME} 安装程序"
VIAddVersionKey "FileVersion" "${VERSION}"
VIAddVersionKey "ProductVersion" "${VERSION}"
VIAddVersionKey "LegalCopyright" "CHO-SIREN"

!define MUI_ICON "${ICON}"
!define MUI_UNICON "${ICON}"
!define MUI_ABORTWARNING
!define MUI_FINISHPAGE_RUN "$INSTDIR\${EXE}"
!define MUI_FINISHPAGE_RUN_TEXT "立即开始游戏"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "SimpChinese"

Section "Install"
  SetOutPath "$INSTDIR"
  File /r /x "*BackUpThisFolder*" /x "*DoNotShip*" "${SRC}\*.*"
  WriteUninstaller "$INSTDIR\uninstall.exe"

  ; Shortcuts take the game's own embedded icon, so an in-game update that swaps the exe updates them too.
  CreateShortCut "$DESKTOP\${APPNAME}.lnk" "$INSTDIR\${EXE}" "" "$INSTDIR\${EXE}" 0
  CreateDirectory "$SMPROGRAMS\${APPNAME}"
  CreateShortCut "$SMPROGRAMS\${APPNAME}\${APPNAME}.lnk" "$INSTDIR\${EXE}" "" "$INSTDIR\${EXE}" 0
  CreateShortCut "$SMPROGRAMS\${APPNAME}\卸载 ${APPNAME}.lnk" "$INSTDIR\uninstall.exe"

  WriteRegStr HKCU "Software\CHO-SIREN-Installer" "InstallDir" "$INSTDIR"
  WriteRegStr HKCU "${UNINSTKEY}" "DisplayName" "${APPNAME} CHO-SIREN"
  WriteRegStr HKCU "${UNINSTKEY}" "DisplayVersion" "${VERSION}"
  WriteRegStr HKCU "${UNINSTKEY}" "Publisher" "CHO-SIREN"
  WriteRegStr HKCU "${UNINSTKEY}" "DisplayIcon" "$INSTDIR\icon.ico"
  WriteRegStr HKCU "${UNINSTKEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "${UNINSTKEY}" "UninstallString" '"$INSTDIR\uninstall.exe"'
  WriteRegDWORD HKCU "${UNINSTKEY}" "NoModify" 1
  WriteRegDWORD HKCU "${UNINSTKEY}" "NoRepair" 1
SectionEnd

Section "Uninstall"
  Delete "$DESKTOP\${APPNAME}.lnk"
  RMDir /r "$SMPROGRAMS\${APPNAME}"
  ; Remove only what the installer put there, then the folder if it is empty (never a whole folder
  ; the player may have pointed us at, e.g. D:\Games).
  RMDir /r "$INSTDIR\CHO-SIREN_Data"
  RMDir /r "$INSTDIR\MonoBleedingEdge"
  RMDir /r "$INSTDIR\D3D12"
  Delete "$INSTDIR\*.exe"
  Delete "$INSTDIR\*.dll"
  Delete "$INSTDIR\icon.ico"
  Delete "$INSTDIR\使用说明.txt"
  Delete "$INSTDIR\开始游戏.cmd"
  RMDir "$INSTDIR"
  DeleteRegKey HKCU "${UNINSTKEY}"
  DeleteRegKey HKCU "Software\CHO-SIREN-Installer"
SectionEnd
