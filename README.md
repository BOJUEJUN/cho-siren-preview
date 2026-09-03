# CHO-SIREN 幻域魅声 — Unity

Unity 原生竖屏游戏项目，基于 CHO-SIREN 网页原型重构。当前包含大厅、角色待机动画、成员、编队、招募、养成、饰品、设置和本机存档，不是在 Unity 中嵌入网页。

## 开发环境

- Unity Editor：`6000.6.0f1`（Unity 6.6）
- 渲染管线：Universal Render Pipeline `17.6.0`
- 主场景：`Assets/Scenes/Main.unity`
- 目标方向：竖屏，参考分辨率 `720 × 1552`

请在 PC 和 Mac 上安装完全相同的 Editor 版本。按需从 Unity Hub 添加以下模块：

- Windows：Windows Build Support
- Web：WebGL Build Support
- Android：Android Build Support、SDK & NDK Tools、OpenJDK

## 获取并打开项目

Git 仓库中应保留这些内容：

- `Assets/`（必须连同所有 `.meta` 文件提交）
- `Packages/manifest.json` 和 `Packages/packages-lock.json`
- `ProjectSettings/`
- `Tools/`
- `.gitignore`、`.gitattributes` 和本 README

`Library/`、`Temp/`、`obj/`、`Logs/`、`UserSettings/`、`TestResults/` 与 `Builds/` 都是本机生成内容，已由 `.gitignore` 排除，不要手工加入 Git。

首次打开：

1. 在 Unity Hub 中选择 **Add project from disk**，指向本目录。
2. 使用 Unity `6000.6.0f1` 打开，等待本机重新生成 `Library/` 并完成资源导入。
3. 打开 `Assets/Scenes/Main.unity`。
4. 点击 Play 运行。

项目已经启用 **Visible Meta Files** 和 **Force Text**，方便 Windows 与 macOS 共同开发和审查场景差异。

> `CHO-SIREN > Configure Project` 是项目重建工具，会重新创建主场景并覆盖构建设置。正常克隆和日常开发不需要运行它；只有明确要重建基础场景时才使用。

## 当前可玩循环

1. 在大厅演出，消耗体力并获得金币或每日奖励。
2. 进入女团选秀，消耗星钻签约新成员。
3. 在成员页训练升级并调整编队。
4. 在饰品页装备加成，提升组合战力。
5. 进度通过 `PlayerPrefs` 保存在当前设备。

`PlayerPrefs` 存档不会随 Git 在 PC、Mac 或手机之间同步；若需要跨设备游戏进度，后续应接入账号与云存档。

## 构建

可在 Unity 中打开 **File > Build Profiles**，确认 `Assets/Scenes/Main.unity` 已启用，再选择目标平台构建。默认输出统一放在被 Git 忽略的 `Builds/`。

项目也提供三个批处理入口：

- `ChoSiren.Editor.ChoSirenBuild.BuildWindows`
- `ChoSiren.Editor.ChoSirenBuild.BuildWebGL`
- `ChoSiren.Editor.ChoSirenBuild.BuildAndroid`

### Windows PowerShell

在项目根目录运行；若 Unity Hub 安装路径不同，请替换 `$unityEditor`：

```powershell
$unityEditor = 'C:\Users\51908\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe'

& $unityEditor -batchmode -quit `
  -projectPath $PWD `
  -executeMethod ChoSiren.Editor.ChoSirenBuild.BuildWindows `
  -buildOutput "$PWD\Builds\Windows\CHO-SIREN.exe" `
  -logFile "$PWD\Logs\build-windows.log"
```

把 `BuildWindows` 替换为 `BuildWebGL` 或 `BuildAndroid`，并将输出分别改为 `Builds/WebGL` 或 `Builds/Android/CHO-SIREN.apk`，即可构建其他目标。

这台受企业 iOA 管理的电脑请优先使用 `Tools/Run-UnitySafe.ps1`。当前环境中 Unity 的 Mono HTTP 监听器在 iOA 数据保护模块已加载时会反复崩溃；该脚本只占用 Unity 可选的 Build Report REST 端口来绕开触发路径，不会关闭或修改 iOA：

```powershell
& .\Tools\Run-UnitySafe.ps1 -UnityArguments @(
  '-batchmode', '-accept-apiupdate', '-quit',
  '-executeMethod', 'ChoSiren.Editor.ChoSirenBuild.BuildWindows',
  '-buildOutput', "$PWD\Builds\Windows\CHO-SIREN.exe"
)
```

### macOS Terminal

在项目根目录运行；若 Unity Hub 安装路径不同，请替换 `UNITY_EDITOR`：

```bash
UNITY_EDITOR="/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity"

"$UNITY_EDITOR" -batchmode -quit \
  -projectPath "$PWD" \
  -executeMethod ChoSiren.Editor.ChoSirenBuild.BuildWebGL \
  -buildOutput "$PWD/Builds/WebGL" \
  -logFile "$PWD/Logs/build-webgl.log"
```

批处理返回非零退出码或日志中没有 `CHO_SIREN_BUILD_OK` 时，应视为构建失败。

## 测试

EditMode 测试位于 `Assets/Tests/EditMode/GameModelTests.cs`，覆盖资源、招募和自动编队的核心规则。

Windows PowerShell：

```powershell
& $unityEditor -batchmode `
  -projectPath $PWD `
  -runTests -testPlatform EditMode `
  -testResults "$PWD\TestResults\editmode.xml" `
  -logFile "$PWD\Logs\test-editmode.log"
```

macOS 将首行可执行文件替换为 `"$UNITY_EDITOR"`，其余参数相同即可。

## PC 与 Mac 协作

每次开始工作前：

```bash
git pull --rebase
```

换电脑前：

```bash
git status
git add -A
git commit -m "描述本次 Unity 修改"
git push
```

注意事项：

- 不要使用 iCloud、OneDrive 或其他网盘直接同步整个 Unity 工作目录；以 Git 远端为唯一源码真相。
- 不要在两台电脑上同时修改同一个场景、Prefab 或 ScriptableObject。并行工作时使用不同分支，完成后通过 PR 合并。
- 切换分支或执行大范围拉取前，先保存场景并退出 Play Mode；大量资源变化时最好关闭 Editor 后再切换。
- `Assets/Resources/Fonts/NotoSansSC-Regular.otf` 约 16 MB，目前可由普通 Git 管理。以后若增加大型音频、视频、PSD、FBX，先在两台机器安装 Git LFS，再统一添加 LFS 规则。
- Android keystore、签名证书、token 和 `.env` 文件已被忽略，必须通过本机安全存储或 CI Secret 分发。

## 资源准备工具

`Tools/prepare_assets.py` 可从同级的 `cho-siren-pages/assets` 重新生成 Unity 图片资源。正常开发无需运行，因为生成结果已经位于 `Assets/Resources/Art`。确需重建时，请先安装 Pillow，并确认网页资源仓库位于预期的同级目录：

```bash
python -m pip install Pillow
python Tools/prepare_assets.py
```
