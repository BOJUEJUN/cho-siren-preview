# CHO-SIREN 幻域魅声 — Unity

Unity 原生竖屏游戏项目，基于 CHO-SIREN 网页原型重构。当前包含大厅、角色待机动画、成员、编队、招募、养成、饰品、设置和本机存档，不是在 Unity 中嵌入网页。

## 开发环境

- Unity Editor：`6000.6.0f1`（Unity 6.6）
- 渲染管线：Universal Render Pipeline `17.6.0`
- 主场景：`Assets/Scenes/Main.unity`
- 目标方向：竖屏，参考分辨率 `720 × 1536`

新电脑首次接手请优先阅读：

- [`Docs/GETTING_STARTED.md`](Docs/GETTING_STARTED.md)：clone、环境、首次打开、测试、构建与预览
- [`Docs/CONTINUITY.md`](Docs/CONTINUITY.md)：跨电脑 checkpoint、回滚、Git LFS 与交接门禁
- [`Docs/AI-COLLAB-HANDOFF.md`](Docs/AI-COLLAB-HANDOFF.md)：当前产品事实、文件所有权及视觉/功能验收
- [`Docs/CHAPTER-01-PROGRESSION.md`](Docs/CHAPTER-01-PROGRESSION.md)：十关、难度、章节奖励和章节任务规范
- [`Docs/BATTLE-VFX-ANIMATION.md`](Docs/BATTLE-VFX-ANIMATION.md)：Boss 动画、战斗特效及分层升级路线

请在 PC 和 Mac 上安装完全相同的 Editor 版本。按需从 Unity Hub 添加以下模块：

- Windows：Windows Build Support
- Web：WebGL Build Support
- Android：Android Build Support、SDK & NDK Tools、OpenJDK

## 获取并打开项目

源码与网页预览共用同一个 GitHub 仓库，但使用彼此独立的分支：

- Unity 源码：`unity-source`
- GitHub Pages 网页预览：`main`

新电脑只克隆源码分支：

```powershell
git clone --branch unity-source --single-branch https://github.com/BOJUEJUN/cho-siren-preview.git cho-siren-unity
Set-Location .\cho-siren-unity
```

不要在 Unity 源码工作目录切换到 `main`；`main` 只存放可再生成的 WebGL 网页预览。

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

Windows 上最省事的方式是双击项目根目录的 **`打开CHO-SIREN编辑器.cmd`**。它会定位本项目声明的 Unity `6000.6.0f1` 并打开可编辑源码；若找不到对应 Editor，会明确报错，而不会用其他版本悄悄升级项目。

`Builds/Windows/开始游戏.cmd` 和 `CHO-SIREN.exe` 只用于运行成品，不能编辑项目。每次 Windows 构建还会生成 `Builds/Windows/打开Unity编辑器.cmd`，方便从构建目录返回源码工作区；它只有与完整源码目录保持当前相对位置时才有效，单独复制或发给别人后的成品包并不会因此变成可编辑项目。

项目已经启用 **Visible Meta Files** 和 **Force Text**，方便 Windows 与 macOS 共同开发和审查场景差异。

> `CHO-SIREN > Configure Project` 是项目重建工具，会重新创建主场景并覆盖构建设置。正常克隆和日常开发不需要运行它；只有明确要重建基础场景时才使用。

## 当前可玩循环

1. 从大厅进入第一章地图，按 `1-1`～`1-10` 顺序解锁真实副本，并选择简单、普通或困难。
2. 累积每关最佳星级，领取 10/20/30 星章节奖励和永久章节任务奖励。
3. 在副本中操作五枚骰子，保留/重投并用牌型倍率驱动真实回合战斗与三阶段 BOSS。
4. 在大厅演出，消耗体力并获得金币或每日奖励。
5. 进入女团选秀，消耗星钻签约新成员。
6. 在成员页训练升级、调整编队，并在饰品页装备加成。
7. 关卡星数、难度、奖励领取与养成进度通过 `PlayerPrefs` 保存在当前设备。

`PlayerPrefs` 存档不会随 Git 在 PC、Mac 或手机之间同步；若需要跨设备游戏进度，后续应接入账号与云存档。

## 构建

可在 Unity 中打开 **File > Build Profiles**，确认 `Assets/Scenes/Main.unity` 已启用，再选择目标平台构建。默认输出统一放在被 Git 忽略的 `Builds/`。

项目也提供三个批处理入口：

- `ChoSiren.Editor.ChoSirenBuild.BuildWindows`
- `ChoSiren.Editor.ChoSirenBuild.BuildWebGL`
- `ChoSiren.Editor.ChoSirenBuild.BuildAndroid`

WebGL 的稳定预览地址与独立 Pages 发布流程见 [`Docs/GETTING_STARTED.md`](Docs/GETTING_STARTED.md#6-webgl-构建验证与预览)；稳定预览不得由 dirty 工作树直接覆盖。

### Windows PowerShell

在项目根目录运行；若 Unity Hub 安装路径不同，请替换 `$unityEditor`：

```powershell
$unityEditor = 'C:\Users\<USER>\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe'

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

EditMode 测试位于 `Assets/Tests/EditMode/`（含 `Systems/`），覆盖存档迁移、经济、招募、剧情、骰子和战斗规则；PlayMode 测试位于 `Assets/Tests/PlayMode/`，覆盖主要 UI 点击链与布局回归。

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

- 日常源码提交只能进入 `unity-source`；不要把 `Assets/`、`Packages/` 或 `ProjectSettings/` 推到 Pages 的 `main`。

- 不要使用 iCloud、OneDrive 或其他网盘直接同步整个 Unity 工作目录；以 Git 远端为唯一源码真相。
- 不要在两台电脑上同时修改同一个场景、Prefab 或 ScriptableObject。并行工作时使用不同分支，完成后通过 PR 合并。
- 切换分支或执行大范围拉取前，先保存场景并退出 Play Mode；大量资源变化时最好关闭 Editor 后再切换。
- `SourceAssets/Fonts/NotoSansSC-Regular.otf` 约 16 MB，只用于重建字体子集，不会进入 Unity/WebGL；目前可由普通 Git 管理。以后若增加更大的音频、视频、PSD、FBX，先在两台机器安装 Git LFS，再统一添加 LFS 规则。
- Android keystore、签名证书、token 和 `.env` 文件已被忽略，必须通过本机安全存储或 CI Secret 分发。

## 资源准备工具

`Tools/prepare_assets.py` 可从同级的 `cho-siren-pages/assets` 重新生成 Unity 图片资源。正常开发无需运行，因为生成结果已经位于 `Assets/Resources/Art`。确需重建时，请先安装 Pillow，并确认网页资源仓库位于预期的同级目录：

```bash
python -m pip install Pillow
python Tools/prepare_assets.py
```

AI 图集的原始参考保存在 `Docs/DesignReferences/AI/`。运行时素材已提交，无需在新电脑重新生图；如需从保留的图集重新切出透明素材：

```powershell
& .\Tools\Process-AILevelMapAtlas.ps1 `
  -Source .\Docs\DesignReferences\AI\chapter-ui-atlas-generated-v1.png

& .\Tools\Process-AIBattleVfxAtlas.ps1 `
  -Source .\Docs\DesignReferences\AI\battle-vfx-atlas-generated-v1.png
```
