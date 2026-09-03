# CHO-SIREN 新电脑开发指南

本指南用于从源码远端取得一个全新的工作副本，并在 Windows 上完成首次打开、测试、构建与本地预览。跨电脑交接与 checkpoint 规则见 [CONTINUITY.md](CONTINUITY.md)。

## 1. 固定开发环境

- Unity Editor：`6000.6.0f1`
- Unity revision：`f7f8ed4d1e24`
- 渲染管线：URP `17.6.0`
- UI：uGUI `2.6.0`
- 测试框架：Unity Test Framework `1.8.0`
- 设计基准：竖屏 `720 × 1536`
- 主场景：`Assets/Scenes/Main.unity`

必须安装完全相同的 Unity Editor 版本。通过 Unity Hub 按需添加：

- Windows 开发：Windows Build Support（IL2CPP 仅在切换为 IL2CPP 时需要）
- Web 预览：WebGL Build Support
- Android：Android Build Support、SDK & NDK Tools、OpenJDK

不要用更高版本 Unity 自动升级项目，也不要首次打开就执行 `CHO-SIREN > Configure Project`；该菜单是会重建场景/构建设置的维护工具，不是初始化步骤。

## 2. 从 GitHub 获取源码

项目使用现有仓库 `https://github.com/BOJUEJUN/cho-siren-preview.git` 的两个独立分支保存不同交付物：

- `unity-source`：Unity 可编辑源码，是开发工作的唯一真相。
- `main`：GitHub Pages 的 WebGL 网页预览，只是可再生成的发布物。

2026-09-03 的本地审计副本已整理到 `unity-source`，并配置了上述 `origin`。使用 GitHub 换机前，必须先在仓库网页确认远端确实存在 `unity-source`；若远端尚未发布该分支，不得改为克隆 `main` 冒充源码，应使用本次交付的完整 Git bundle 恢复。

新电脑必须只克隆 `unity-source`，不要在源码工作目录切换到 `main`：

安装 Git 和 Git LFS 后，在新电脑执行：

```powershell
git lfs install
git clone --branch unity-source --single-branch https://github.com/BOJUEJUN/cho-siren-preview.git cho-siren-unity
Set-Location .\cho-siren-unity
git lfs pull
git status --short
```

当前提交历史没有 LFS 对象，`git lfs pull` 会是空操作；保留这一步可兼容以后加入的大型源媒体。克隆后必须能看到 `Assets/`、`Packages/`、`ProjectSettings/`、`Tools/` 和全部 `.meta` 文件。

若手头拿到的是 `CHO-SIREN-Unity-Source-0.3.0.bundle`，无需联网也可以恢复完整历史：

```powershell
git clone .\CHO-SIREN-Unity-Source-0.3.0.bundle cho-siren-unity
Set-Location .\cho-siren-unity
git switch unity-source
git status --short
```

bundle 是完整、可验证的 Git 仓库快照，不包含 `Library/`、构建缓存或玩家存档。恢复后如需与 GitHub 同步，再由仓库所有者确认目标远端并执行 `git remote add origin <SOURCE_REPOSITORY_URL>`。

运行只读环境检查：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File .\Tools\Check-DevelopmentEnvironment.ps1
```

Unity 不在常见 Hub 路径时显式传入：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File .\Tools\Check-DevelopmentEnvironment.ps1 `
  -UnityExe 'D:\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe'
```

检查脚本不下载、不安装、不启动 Unity，也不修改仓库。`FAIL` 必须处理；`WARN` 至少要在交接记录中说明。

## 3. 首次打开

1. 关闭其他正在打开此项目的 Unity Editor。
2. 在 Unity Hub 选择 **Add project from disk**，选择 `cho-siren-unity` 根目录。
3. 使用 `6000.6.0f1` 打开并等待 Package Manager resolve、脚本编译与资源导入完成。
4. 首次生成 `Library/` 可能较慢，序列帧 Crunch 导入期间不要强制结束 Editor。
5. 打开 `Assets/Scenes/Main.unity`，确认 Console 为 0 error 后进入 Play Mode。
6. 首次烟雾检查确认第一章显示 `1-1`～`1-10`，十关依次锁定/解锁，且 `1-1` 能进入五骰真实战斗并正常返回或结算。

Windows 上也可使用安全启动器：

```powershell
.\打开CHO-SIREN编辑器.cmd
```

也可以在 PowerShell 中执行 `& .\Tools\Open-UnityEditorSafe.ps1`。根目录的 `打开CHO-SIREN编辑器.cmd` 会调用同一套安全定位逻辑，按项目声明版本检查 `UNITY_EDITOR`、当前用户的 Unity Hub 路径和 Program Files。非标准安装目录可直接从 Hub 打开，或调用 `Run-UnitySafe.ps1 -UnityExe <路径>`。不要并行启动两个 Editor 指向同一工作目录。

`Builds/Windows/开始游戏.cmd` 和 `CHO-SIREN.exe` 是运行成品，不会打开 Unity 编辑器。每次 Windows 构建还会在该目录生成 `打开Unity编辑器.cmd`；它只是返回上两级的源码根目录并启动 Editor，因此仅在当前完整源码工作区内有效。将 `Builds/Windows` 单独复制到其他电脑后，必须重新 clone `unity-source` 才能编辑。

## 4. 运行测试

测试输出写入已被 Git 忽略的 `TestResults/` 和 `Logs/`。

```powershell
$unityEditor = 'C:\Users\<USER>\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe'

$editMode = @(
  '-batchmode', '-accept-apiupdate',
  '-runTests', '-testPlatform', 'EditMode',
  '-testResults', "$PWD\TestResults\editmode.xml"
)
& .\Tools\Run-UnitySafe.ps1 -UnityExe $unityEditor -UnityArguments $editMode `
  -LogPath "$PWD\Logs\test-editmode.log"

$playMode = @(
  '-batchmode', '-accept-apiupdate',
  '-runTests', '-testPlatform', 'PlayMode',
  '-testResults', "$PWD\TestResults\playmode.xml"
)
& .\Tools\Run-UnitySafe.ps1 -UnityExe $unityEditor -UnityArguments $playMode `
  -LogPath "$PWD\Logs\test-playmode.log"
```

测试参数不要添加 `-quit`；Unity Test Runner 会在完成后退出。进程非零、XML 缺失、XML 中存在失败，或日志出现编译错误，都算失败。

## 5. Windows 构建

```powershell
$unityEditor = 'C:\Users\<USER>\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe'
$buildWindows = @(
  '-batchmode', '-accept-apiupdate', '-quit',
  '-executeMethod', 'ChoSiren.Editor.ChoSirenBuild.BuildWindows',
  '-buildOutput', "$PWD\Builds\Windows\CHO-SIREN.exe"
)
& .\Tools\Run-UnitySafe.ps1 -UnityExe $unityEditor -UnityArguments $buildWindows `
  -LogPath "$PWD\Logs\build-windows.log"
```

成功条件：进程返回 0、日志包含 `CHO_SIREN_BUILD_OK`，并且 `Builds/Windows/CHO-SIREN.exe` 可在真实 Windows Player 中启动。需要发包时再执行 `Tools/Package-WindowsPreview.ps1`；它会替换同版本 `Releases/` 内容，因此只在明确发布时运行。

构建完成后目录中应同时存在：

- `开始游戏.cmd`：启动最新 Windows 成品。
- `打开Unity编辑器.cmd`：回到本机源码根目录打开 Unity，仅源码工作区有效。

## 6. WebGL 构建、验证与预览

```powershell
$buildWebGl = @(
  '-batchmode', '-accept-apiupdate', '-quit',
  '-executeMethod', 'ChoSiren.Editor.ChoSirenBuild.BuildWebGL',
  '-buildOutput', "$PWD\Builds\WebGL"
)
& .\Tools\Run-UnitySafe.ps1 -UnityExe $unityEditor -UnityArguments $buildWebGl `
  -LogPath "$PWD\Logs\build-webgl.log"

& .\Tools\Test-WebGLDeliverable.ps1 -BuildPath "$PWD\Builds\WebGL"
python -m http.server 8080 --directory .\Builds\WebGL
```

浏览器打开 `http://localhost:8080/`，至少检查大厅、成员、招募、任务、第一章关卡、骰子战斗和返回链路。不要直接双击 `index.html`，浏览器的本地文件策略会破坏 WebGL/StreamingAssets 加载。

本地烟雾测试完成后，可用安全发布脚本同步到同级的独立 Pages 工作目录。先干跑审查计划，再执行；脚本只会更新新旧 `index.html` 明确涉及的 Unity 哈希构建文件、`.nojekyll` 和 `StreamingAssets`，不会镜像或删除 Pages 的工作流、脚本及其他站点文件：

```powershell
& .\Tools\Publish-WebGLToPages.ps1 `
  -BuildPath "$PWD\Builds\WebGL" `
  -PagesPath "$PWD\..\cho-siren-pages" `
  -DryRun

& .\Tools\Publish-WebGLToPages.ps1 `
  -BuildPath "$PWD\Builds\WebGL" `
  -PagesPath "$PWD\..\cho-siren-pages"

Push-Location "$PWD\..\cho-siren-pages"
npm run check
git diff --check
Pop-Location
```

脚本会在写入前再次运行 `Test-WebGLDeliverable.ps1`，拒绝缺文件、非哈希文件、单文件达到 GitHub 100 MiB 限制或包含额外构建文件的 WebGL 产物。`index.html` 最后写入；即使中途复制失败，也不会先删除旧页面仍在引用的构建文件。

线上稳定预览位于 `https://bojuejun.github.io/cho-siren-preview/`。GitHub 仓库的 `main` 是 Pages 分支，`unity-source` 是源码分支；两者应使用两个独立工作目录，绝不能在 dirty Unity 工作树中来回切换。发布顺序必须是：

1. 源码测试通过并 push，记录源 commit SHA 或 checkpoint tag。
2. WebGL 构建通过 `Test-WebGLDeliverable.ps1`，本地 HTTP 烟雾测试通过。
3. 在**单独克隆**且干净的 Pages `main` 工作目录中，把 `Builds/WebGL` 内容更新到其约定发布目录。
4. 提交信息写入源 commit SHA，经 diff/PR 审查后 push。
5. 等 GitHub Pages 部署成功，再验证稳定链接 `https://bojuejun.github.io/cho-siren-preview/`。

不得从未提交的工作树直接覆盖稳定预览，也不得把 `Builds/`、`Releases/` 提交到源码仓库。

## 7. 常见问题

- **只有粉色材质或 Missing Script**：通常是 LFS 对象未拉取、`.meta` 遗失、包尚未 resolve，或用了错误 Unity 版本。先运行环境检查，再看 Console 第一条错误。
- **Library 很大/换机是否复制**：不要复制；关闭 Unity 后删除本机 `Library/` 是可恢复操作，重新打开会重建。
- **项目锁/导入数据库损坏**：确认没有第二个 Editor 或批处理进程打开同一路径。
- **Unity 许可证失败**：先在 Unity Hub 登录并激活对应许可证；不要用重复并发批处理绕过。
- **WebGL 本地打不开**：必须通过 HTTP 服务访问；同时确认 `StreamingAssets/Lobby/lobby-loop.mp4` 已跟踪且实际存在。
- **画面比例错误**：权威基准是 `720 × 1536`，不是旧文档里的 `720 × 1552`。
- **新电脑没有存档**：玩家进度存于本机 `PlayerPrefs`，Git 只同步源码，不同步游戏存档。
- **双击 Windows 构建里的编辑器入口无反应**：`打开Unity编辑器.cmd` 依赖同一个源码工作区中的 `Tools/`、`Packages/` 和 `ProjectSettings/`；它不是随身编辑器。请按第 2 节重新 clone `unity-source`，再双击根目录的 `打开CHO-SIREN编辑器.cmd`。
- **中文脚本乱码**：Windows PowerShell 5.1 对无 BOM 中文脚本不稳定；新增 `.ps1` 尽量只用 ASCII 字面量。

## 8. 安全回到 checkpoint

不要用 `git reset --hard` 处理不确定的本地改动。先保存现场：

```powershell
git status --short
git stash push -u -m 'before-checkpoint-recovery'
git fetch --all --tags
git switch -c recovery/checkpoint-verify <CHECKPOINT_TAG_OR_SHA>
```

这会在 checkpoint 上创建新的检查分支，不移动或删除原分支。验证完成后可 `git switch <原分支>`；需要取回现场时执行 `git stash list`，确认条目后再 `git stash apply`。
