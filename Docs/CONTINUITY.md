# CHO-SIREN 跨电脑连续开发规范

目标：任意一台新电脑只依赖源码 GitHub 仓库、声明的 Unity 版本和 Git LFS，就能恢复同一份可编译、可测试、可构建的项目。

## 当前审计快照

审计时间：2026-09-03（Asia/Shanghai）。

- Git 仓库：存在，当前分支 `master`。
- 当前提交：`c20e9c1 feature: improve dice evaluation and auto holds`。
- 当前实际状态：没有配置 `origin`，因此此刻另一台电脑还不能从 GitHub 取得这份源码；这是最终 checkpoint 前的硬阻断。
- 目标源码远端方案：`https://github.com/BOJUEJUN/cho-siren-preview.git` 的 `unity-source` 分支。最终维护者必须将同一 checkpoint push 到该分支并建立 upstream；不得用 Pages 的 `main` 代替。
- 已跟踪文件：约 1,036 个，工作树内合计约 192.64 MiB。
- 已跟踪最大单文件：`SourceAssets/Fonts/NotoSansSC-Regular.otf`，15.68 MiB；它保留为字体子集源文件，但不进入 Unity 构建。
- 当前工作树与全部 Git 历史：没有 ≥50 MiB blob，也没有 ≥100 MiB blob。
- Git LFS：客户端已安装，但当前没有 LFS 跟踪规则/对象。
- `Library/`、`Temp/`、`Logs/`、`Build/`、`Builds/`、`Releases/`、`TestResults/` 等本机构建内容已忽略。
- `Assets/StreamingAssets/Lobby/lobby-loop.mp4` 没有被忽略，必须与其 `.meta` 一起版本化。

这份快照只描述审计时刻；以 `git status`、`git remote -v`、`git lfs ls-files` 和 `Tools/Check-DevelopmentEnvironment.ps1` 的即时结果为准。

同一 GitHub 仓库的 `main` 只用于 GitHub Pages 网页预览，不是 Unity 源码分支。源码工作目录不得切换到 `main`，Pages 发布应使用另一份独立 clone。

审计时工作目录中存在下列 ≥50 MiB 文件，但它们全部位于已忽略的缓存、构建、发布暂存或 vendored 依赖目录，不应进入 Git/LFS：

- ≥100 MiB：`Releases/.../resources.assets.resS` 132.22 MiB、`Library/ArtifactDB` 128.00 MiB、`Library/DataStore/UDSData_56.bin` 112.13 MiB。
- 50–100 MiB：`Library/DataStore/UDSData_55.bin` 99.02 MiB、`Releases/*.zip` 94.11 MiB、`Tools/python-packages/.../ffmpeg*.exe` 83.58 MiB、Windows `resources.assets.resS` 72.96 MiB、WebGL/tmp data 52–57 MiB。

这些文件应通过 Unity 构建、发布打包或依赖安装重建；不能因为换电脑方便而用 `git add -f` 或 Git LFS 携带。

## 源码真相边界

必须提交并 push：

- `Assets/` 及所有对应 `.meta`
- `Packages/manifest.json`、`Packages/packages-lock.json`
- `ProjectSettings/`
- `Tools/`、`Docs/`
- `.gitignore`、`.gitattributes`、`README.md`

不得作为源码同步：

- `Library/`、`Temp/`、`Logs/`、`UserSettings/`
- `Build/`、`Builds/`、`Releases/`、`TestResults/`
- `Tools/python-packages/`、IDE 缓存、许可证、签名材料、token、`.env`
- `PlayerPrefs` 玩家存档

不要用 OneDrive、网盘或 U 盘覆盖整个 Unity 工作目录。Git 远端是源码唯一真相；Windows/WebGL 构建与 GitHub Pages 是可再生交付物。

## 素材来源与授权记录

- `Assets/Resources/Art/BattleUser/` 与 `Assets/Resources/Art/LevelMapUser/` 保存用户在本轮明确提供或确认的王座 BOSS、六面骰和第一章地图素材；原图与 `.meta` 必须一并保留。公开或商业发布前仍需由项目所有者确认其来源及授权范围。
- 名称以 `*AI` 结尾的美术目录保存本轮 AI 协作生成或处理的 UI/背景素材。Git 仓库尚不能替代完整的生成凭证；最终 checkpoint 应另行记录工具、生成日期、提示词/参考图来源和可用授权。
- `Assets/Resources/Art/Members/`、`HeroFrames` 与 `Assets/StreamingAssets/Lobby/lobby-loop.mp4` 属于既有项目/参考素材。公开发布前必须完成来源、肖像、音乐/视频及内容分级复核。
- `Builds/`、`Releases/` 和 Pages 内容是由源码生成的交付物，不是素材来源证明，也不能反向代替可编辑源文件。

## 离开旧电脑前

1. 退出 Play Mode，保存场景/Prefab，关闭 Unity。
2. 运行 `Tools/Check-DevelopmentEnvironment.ps1`，处理所有 `FAIL`。
3. 查看 `git status --short`；确认新增 Unity 资源都有同名 `.meta`。
4. 运行全量 EditMode、PlayMode；高风险视觉修改还要做 Windows Player 和 WebGL 烟雾测试。
5. 用 `git diff --check` 检查空白错误，用 `git diff --stat` 检查改动范围。
6. 提交源码；不要把被忽略的构建目录强行 `git add -f`。
7. 首次发布执行 `git push -u origin HEAD:unity-source`；之后可用普通 `git push`。在 GitHub 网页确认 `unity-source` 上的 commit 与大文件均可访问，绝不能把 Unity 源码推入 `main`。
8. 重要可交付点创建 annotated tag，例如：

```powershell
git tag -a checkpoint/20260903-playable-v1 -m 'Playable first chapter checkpoint'
git push origin checkpoint/20260903-playable-v1
```

checkpoint tag 必须指向已通过门禁且已 push 的 commit，不能指向仅存在本机的工作树。

## 到达新电脑后

1. 按 `GETTING_STARTED.md` 安装完全相同的 Unity `6000.6.0f1`。
2. 用下列命令只克隆源码分支，再执行 `git lfs pull`：

```powershell
git clone --branch unity-source --single-branch https://github.com/BOJUEJUN/cho-siren-preview.git cho-siren-unity
Set-Location .\cho-siren-unity
git lfs pull
```

3. 运行只读环境检查，确认 `origin`、关键素材、构建入口和包 JSON。
4. 双击根目录 `打开CHO-SIREN编辑器.cmd`，或从 Unity Hub 用 `6000.6.0f1` 打开；首次只等待自动导入，不运行重建工具。
5. Console 0 error 后跑 EditMode、PlayMode。
6. 在开始新修改前创建工作分支，避免直接把试验性修改堆到 checkpoint。

## 分支与多人/多 AI 协作

- 开工前记录 `git status --short` 和当前 SHA。
- 每个任务声明文件所有权；`ChoSirenApp.cs`、`GameModel.cs`、核心数据 JSON、场景/Prefab 同时只由一个任务写。
- 不覆盖整个文件来合并；使用小范围 patch，保留工作树中不属于自己的改动。
- 合并前至少交代：改动文件、行为变化、测试证据、未验证项、资产来源与授权风险。
- 构建、截图和部署任务不能悄悄修改产品代码。
- 只有维护者统一提交、tag、push；自动代理不得擅自改历史或 force-push。
- `unity-source` 承载源码，`main` 承载 Pages；即使它们共享一个远端 URL，也必须视作两套独立工作树和发布流程。

## 大文件与 Git LFS 策略

GitHub 普通 Git 的单文件硬限制是 100 MiB。项目采用更保守的门槛：

- `< 50 MiB`：可普通 Git 管理；对高频变更二进制仍要评估 LFS。
- `50–100 MiB`：提交前先建立窄范围 LFS 规则，例如只跟踪 `Assets/SourceVideo/*.mp4`，并在另一台电脑验证 clone。
- `>= 100 MiB`：不得普通提交；先压缩/拆分/外置或使用 LFS。
- 构建产物、缓存和可下载依赖永远不应因为“大”而进入 LFS；它们应被忽略并重建。

添加新规则的安全流程：

```powershell
git lfs install
git lfs track 'Assets/SourceVideo/*.mp4'
git add .gitattributes
git add Assets/SourceVideo
git lfs ls-files
```

不要对已有远端执行 `git lfs migrate import`、filter-repo 或 force-push，除非维护者明确批准、已备份、通知所有协作者重新 clone。当前历史没有 ≥50 MiB blob，不需要改写历史。

## Checkpoint 与回滚

建议 checkpoint 命名：`checkpoint/YYYYMMDD-short-description`。恢复时优先创建分支，不破坏当前现场：

```powershell
git fetch --all --tags
git status --short
git stash push -u -m 'before-checkpoint-recovery'
git switch -c recovery/<name> <checkpoint-tag-or-sha>
```

单文件回看先使用 `git show <checkpoint>:<path>`。只有确认要覆盖该文件时才使用 `git restore --source <checkpoint> -- <path>`。禁止把 `git reset --hard` 当作日常回滚手段。

## 每次交接的最小记录

```text
source commit/tag:
branch:
dirty paths:
Unity version:
changed files:
EditMode result:
PlayMode result:
Windows smoke result:
WebGL validation result:
known blockers:
next file owner:
```

如果其中任何测试没有运行，必须写“未运行”和原因，不能沿用旧交接文档的历史通过数冒充当前结果。
