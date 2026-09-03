# CHO-SIREN Unity 多 AI 协作交接

更新时间：2026-09-03（Asia/Shanghai）  
项目根目录：`D:\AIGC\PPT\jcc-chroma-web\cho-siren-unity`  
Unity：`6000.6.0f1`，全球版 Unity Editor，项目为竖屏 `720 × 1536` 设计基准。

## 1. 先读这里

这是从网页原型迁移到 Unity 的竖屏偶像养成游戏原型。当前已有可运行的大厅、成员、团队、饰品、选秀、冒险关卡、演出和存档流程，但视觉还原、50+ 成员扩容和部分交互仍在迭代。

用户当前最高优先级：

1. 首页 UI 先做到清楚、整齐、接近旧网页预览，不遮挡、不重叠。
2. 成员必须使用本地素材包中的真实不同角色，目标至少 50 名；禁止复制同一立绘凑数量。
3. 成员页要支持 50+ 数据而不一次加载全部大图。
4. 顶部邮件、音乐、设置使用用户刚选定的白色立体线条、青粉霓虹图标。
5. 所有玩家可见 UI 只使用中文；`SSR`、`SR`、`R` 和战斗评级 `S/A/B/C` 例外。
6. Windows 版为主要交付；WebGL 只是朋友快速预览，稳定链接应继续使用 `https://bojuejun.github.io/cho-siren-preview/`。

旧网页视觉参考：`https://bojuejun.github.io/cho-siren-preview/`。  
Windows 构建：`Builds\Windows\CHO-SIREN.exe`（本轮已重建）。  
旧压缩包：`Releases\CHO-SIREN-Windows-Preview-0.3.0.zip`（本轮未动 Releases/Pages）。

## 2. 当前工作区状态

本目录本身不是 Git 仓库，修改前不要假设可以用 `git checkout` 回滚。`cho-siren-pages` 是另一个用于 GitHub Pages 的仓库。

### 已验证基线（2026-09-03 接线后）

- EditMode：**126/126** 通过。
- PlayMode UI：`UiInteractionSmokeTests` **5/5** 通过。
- Windows 构建：`Builds\Windows\CHO-SIREN.exe`；`CHO-SIREN_Data\resources.assets.resS` = **60486304** 字节（约 57.7 MiB），对比旧基线 **138645184**（约 132.2 MiB），约 **-56%**。
- 烟雾截图（720×1536，Player `-smokeCapture`/`-smokeScreen`）：`Builds\Smoke\wired-{lobby,gacha,taskboard,tactics,story}.png`（另有 `-motion` 副帧）。自检：无英文占位、无明显重叠；剧情页可见「背景资源缺失」（中文提示，缺 BG 资源）。
- 烟雾战棋入口：第一章关卡统一为 `Level-1-1`～`Level-1-4`；`SmokeCapture` 会动态选择当前已解锁关卡，再由 `StartChallenge` 进入 `TacticsBattlePanel`。
- 角色大厅动画：238 张透明序列帧，24 fps；这些帧属于同一个角色动画，不是 238 名英雄。

### 新增但尚未接入主界面的模块

- `Assets\Scripts\MemberCatalog.cs`
  - 50+ 清单验证、稳定 ID、确定性战力、v1 索引存档到 v2 稳定 ID 的迁移底座。
  - 对应 `Assets\Tests\EditMode\MemberCatalogTests.cs` 已单独运行：8/8 通过，结果在 `TestResults\member-catalog-tests.xml`（2026-09-03 10:25）。下一次全量 EditMode 仍应包含它。
- `Assets\Scripts\MemberRosterPagination.cs`
  - 筛选后分页、每页 9/12 人、三列网格、空结果和越界处理。
  - 对应 11 项 EditMode 测试已通过。

详细设计：

- `Docs\hero-asset-inventory-50.md`
- `Docs\member-catalog-50-plan.md`
- `Docs\member-roster-50-ui-plan.md`

## 3. 50+ 英雄素材的权威结论

素材包：

`D:\BaiduNetdiskDownload\3695 棕色尘埃2【202605】`

已完成只读盘点：

- 找到 55 个独立角色组、100 张可选立绘变体。
- 已选出 55 张不同的透明 PNG 主档，SHA-256 没有完全重复。
- 完整文件路径、尺寸和风险标记在 `Docs\hero-asset-inventory-50.md`。
- 55 张源图约 463.9 MiB，10 张超大图，不能原样全部拖进 Unity。

正确导入流程：

1. 以角色组编号为临时稳定键，先确定 55 个不同角色。
2. 为运行时制作副本：缩略图建议 `256 × 352`，详情图建议 `512 × 704`，保持 Alpha。
3. 等比完整显示并添加 4%–8% 透明安全边，禁止中心硬裁导致武器、耳朵、裙摆缺失。
4. 建议目录：`Assets\Resources\Art\Members\<stable-id>\thumb.png` 与 `portrait.png`。
5. 建议首版 54 名，以主唱/舞者/支援各 18 名平衡分布；第 55 名可作为候补或活动角色。
6. 不要直接把素材文件名显示给玩家；中文名、定位、稀有度需要策划表。

版权提示：素材来源是第三方游戏素材包。内部原型可以继续验证，但对外发布、收费或公开分发前必须确认授权和内容分级。

## 4. 顶部图标的用户选择

用户选定参考图：

`C:\Users\51908\AppData\Local\Temp\codex-clipboard-27f3d02d-43a0-4a6f-abdc-3bac720a1e00.png`

设计要求：

- 顺序：邮件、音乐、设置。
- 白色立体线条；左/下青色微光，右/上粉色微光。
- 图案本身简洁，三个图标的视觉重量、描边粗细和点击热区一致。
- 最终资源必须是真透明背景，不能把灰白棋盘格烘进 PNG。
- 顶部显示约 28–32 px，按钮点击热区保持约 `40 × 48`。
- 设置按钮右上角保留粉色通知点。

当前代码中邮件和设置由 `MinimalUiIconFactory` 生成，音乐仍是文字字形 `♫`。调用位置在 `ChoSirenApp.cs` 顶部栏构建代码约 190 行，按钮辅助方法约 896–940 行。

AI 已生成一张黑底、适合加法混合的参考条：

`C:\Users\51908\.codex\generated_images\01a05b24-9ab2-7ab2-a093-34347cbeacb5\exec-ccf96f26-5e52-4380-8a68-5a0e8d07acae.png`

它还没有导入项目。项目已有 `com.unity.modules.vectorgraphics`，优先建议把用户选定的三枚图标重绘为透明 SVG 或真正带 Alpha 的 PNG，再用 `AddSpriteIconButton` 接入。不要把黑底图直接用普通 Alpha 材质显示。

## 5. 首页目前需要验收的具体问题

文件：`Assets\Scripts\ChoSirenApp.cs`

- `每日任务` 与 `每日签到` 不得重叠，视觉间隔应与 `闪耀舞台计划` / `冒险剧本` 一致。
- `开始演出`、音符和 `舞台已就绪` 必须位于装饰框内的视觉中心。
- 角色动画保留素材原始动作，不要再额外叠加强硬上下浮动。
- 角色站位要靠下一些，但脚部不能进入底部导航栏；左右卡片不能遮住面部。
- 卡片图标必须绘制在卡片装饰层之上，文字也要保持足够对比度。
- 顶部角色名和 `等级 68` 必须清楚，不能缩成难以阅读的小字。
- 中间卡片层级、边距和圆角统一；按钮文字不能漂出按钮或装饰框。

建议验收截图尺寸：Windows 窗口内容区接近 `720 × 1536`，再补测 900p/1080p 高 DPI 缩放。

## 6. 50+ 成员页接线顺序

不要一次同时重写数据、存档、图片导入和 UI。按下面顺序做：

1. 先运行 `MemberCatalogTests`，修到通过。
2. 创建 `Assets\Resources\Data\member-catalog.json`，至少 50 条，所有 ID 和资源路径唯一。
3. 批量生成运行时缩略图/详情图；先导入 9–12 名做样本验收，再处理余下角色。
4. 把 `GameModel.Members` 从硬编码 9 项切到 `MemberCatalog`，保留原 9 名 ID 与旧顺序。
5. 启用 `ChoSiren.Save.v2` 的稳定 ID 存档迁移，旧 v1 至少保留两个公开版本，不要迁移后马上删。
6. 将 `MemberRosterPagination` 接入 `BuildMembers()`；默认一页 9 张卡，只创建当前页卡片。
7. 添加定位/稀有度筛选和上一页/下一页；切换筛选时页码重置为 0。
8. 缩略图按页异步加载；禁止 `Resources.LoadAll`，禁止打开页面时同步加载 50+ 张大图。
9. PlayMode 验证能翻到最后一页、打开第 54/55 名详情、快速翻页不串图。

长期更新版本应迁移 Addressables；50+ 原型可以先用分页后的 `Resources.LoadAsync`。

## 7. 多 AI 文件分工，避免互相覆盖

同一时刻只允许一个 AI 修改 `ChoSirenApp.cs`，因为这个文件集中构建了大部分界面。

### 当前已占用的并行任务（2026-09-03）

- 本 Codex 的素材任务：`Tools\Prepare-MemberRuntimeArt.ps1`、运行时成员图片目录和素材映射清单。
- 本 Codex 的图标任务：`Assets\Resources\Art\UI\HudIcons\` 与 `Docs\selected-hud-icons.md`。
- 本 Codex 的测试任务：新增独立大厅布局 PlayMode 测试文件。
- `MemberCatalog.cs`、`MemberRosterPagination.cs` 及其测试底座已经完成；外部 AI 不要重写这些文件，可直接接入。

外部 AI 当前最适合独占 `ChoSirenApp.cs` 完成首页或成员页接线，或者独占 `GameModel.cs` 完成 54/55 人目录与 v2 存档接线。开始前应在交接消息中声明选择哪一个文件；不要同时改两条线。

推荐分工：

- AI-A（数据/存档）：只改 `GameModel.cs`、`MemberCatalog.cs`、成员 JSON 和相关 EditMode 测试。
- AI-B（素材处理）：只新增批处理脚本和 `Assets\Resources\Art\Members\...` 运行时图片，不改 C# 主界面。
- AI-C（成员 UI）：独占 `ChoSirenApp.cs` 的 `BuildMembers()` 和成员详情区域，接入分页与筛选。
- AI-D（首页 UI/图标）：独占 `ChoSirenApp.cs` 的大厅和顶部栏；若 AI-C 正在编辑，先只制作图标资源，不碰主文件。
- AI-E（验证/构建）：不改产品代码，只跑测试、截图、Windows/WebGL 构建并记录结果。
- AI-F（系统层）：独占 `Assets\Scripts\Systems\**`、`Assets\Tests\EditMode\Systems\**`、`Assets\Resources\Data\{economy,gacha,tactics}.json` 与 `Data\Story\**`；见第 11 节。`Data\member-catalog.json` 仍归 AI-A。

交接前每个 AI 必须说明：修改了哪些文件、哪些测试通过、哪些还没测。禁止用覆盖整个文件的方式合并别人的工作。

## 8. Unity 测试与构建约束

Unity Editor：

`C:\Users\51908\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe`

安全启动脚本：

`Tools\Run-UnitySafe.ps1`

这个脚本会占用本机端口 38000，规避本机 iOA 注入导致的 Unity Build Report 服务崩溃。不要并行启动多个 Unity 进程打开同一项目；否则会出现项目锁、Library 冲突或测试结果不可信。

运行全部 EditMode（PowerShell）：

```powershell
$args = @(
  '-batchmode', '-accept-apiupdate',
  '-runTests', '-testPlatform', 'EditMode',
  '-testResults', 'D:\AIGC\PPT\jcc-chroma-web\cho-siren-unity\TestResults\editmode-handoff.xml'
)
& '.\Tools\Run-UnitySafe.ps1' -UnityArguments $args
```

运行大厅 PlayMode：

```powershell
$args = @(
  '-batchmode', '-accept-apiupdate',
  '-runTests', '-testPlatform', 'PlayMode',
  '-testFilter', 'ChoSiren.Tests.UiInteractionSmokeTests',
  '-testResults', 'D:\AIGC\PPT\jcc-chroma-web\cho-siren-unity\TestResults\ui-handoff.xml'
)
& '.\Tools\Run-UnitySafe.ps1' -UnityArguments $args
```

Unity Test Runner 会自己退出；测试参数中不要添加 `-quit`。

修改后最低门禁：

- 编译 0 error。
- EditMode 全通过。
- PlayMode 交互全通过。
- 截图确认竖屏布局、中文文案、图标层级和 CTA 对齐。
- Windows 实机打开检查，不只看 WebGL。
- 只有源码测试和本地构建通过后，才更新 GitHub Pages 稳定链接。

## 9. 完成定义

本轮可称为完成，至少要满足：

- 首页卡片无重叠，CTA 对框，顶部图标为用户选中的同一套设计。
- 角色动画流畅且没有人为硬上下浮动。
- 成员目录包含至少 50 个不同稳定 ID，使用至少 50 张不同本地立绘。
- 成员页分页/筛选有效，最后一页可访问，不一次加载全部大图。
- v1 存档迁移后，旧 9 名的等级、解锁和编队不串角色。
- 所有可点击入口有反馈，所有可见文字清楚且中文化。
- Windows 新构建、压缩包和 WebGL 稳定链接都来自同一通过测试的源码版本。

## 10. 2026-09-03 项目体检结论（静态审查，未启动 Unity）

已直接修复（不需要 Unity、不触碰 `ChoSirenApp.cs`）：

- `Tools\Package-WindowsPreview.ps1`：不再把 `CHO-SIREN_BackUpThisFolder_ButDontShipItWithYourGame` 打进发布包；`bundleVersion` 与 `-Version` 不一致时给出警告；文件补上 UTF-8 BOM——本机只有 Windows PowerShell 5.1，无 BOM 时脚本里的中文字面量会按 GBK 解码导致“字符串缺少终止符”解析失败。以后 `Tools\*.ps1` 只要含中文就必须带 BOM。
- `.gitignore`：新增 `Releases/`、`tmp/`、`.utmp/`、`Data/`、`Tools/python-packages/`（内含 87 MB 的 ffmpeg.exe，禁止入库）。

体积/性能事实（供后续优化决策）：

- `Assets\Resources\Art\HeroFrames`：238 张 720×720 PNG，源文件 131.7 MiB；Windows 构建里 `resources.assets.resS` 为 138.6 MiB，几乎全部是这套序列帧（Standalone 为 DXT5，每帧约 0.5 MB，常驻显存约 123 MB）。
- `Assets\Resources\Fonts`：15.7 MiB，`NotoSansSC-Regular` 整包随构建发布；WebGL 的 60 MB `.data` 中它占比第二。
- `Assets\Editor\HeroFrameImportProcessor.cs` 强制 `maxTextureSize = 1024`、`crunchedCompression = false`，而 `ChoSirenProjectSetup.ConfigureArt` 想给序列帧设 512；两处意图冲突，以 Postprocessor 为准。
- `Packages\manifest.json` 含 `com.unity.ai.assistant 2.19.0-pre.2`（预览包，`Unity.AI.MCP.Runtime.dll` 已进入玩家构建）、`ai.navigation`、`timeline`、`xr.legacyinputhelpers`、`2d.tilemap` 以及 terrain/cloth/vehicles/wind/xr/video 等大量未使用模块。
- `ProjectSettings.asset`：`bundleVersion: 0.1.0`，但发布包标注 0.3.0；`managedStrippingLevel` 为默认，Windows 用 Mono，`Managed\` 目录约 20 MB System.*.dll 大多未用。
- `Releases\CHO-SIREN-Windows-Preview-0.3.0\`（解压态暂存目录）与同名 zip 并存，重复约 250 MB；`Builds\Windows\预览说明.txt` 与打包脚本写入的 `使用说明.txt` 内容重叠。
- 项目根目录 `tmp\` 有 83.8 MiB 的截图/日志；`Library\` 2.5 GB 属正常。

建议按收益排序、需 Unity 验证后再做（每项都会改变构建产物，改完必须重跑 EditMode + PlayMode 并重新截图）：

1. 序列帧启用 Crunch（`crunchedCompression = true`，quality 50–70）：显存不变，磁盘/WebGL 下载约缩到 1/3–1/4。不改帧数、不改动作，符合“保留素材原始动作”的要求。
2. 字体子集化：把 `NotoSansSC-Regular` 裁成项目实际用到的字符（可用 `pyftsubset` 生成 GB2312 常用字或按文案扫描），预计 15.7 MiB → 1–2 MiB。
3. `manifest.json` 删除 `com.unity.ai.assistant`（发布产物里不应带预览期 AI 包）及确认不用的模块；`managedStrippingLevel` 提到 Medium。
4. 把 `bundleVersion` 改成 0.3.0（或让 `Package-WindowsPreview.ps1` 直接读取它），保证 `Application.version` 与发布包一致。
5. `ChoSirenApp.cs`（需拿到文件独占权）小项：`UpdateTopBar` 的 `/120` 改为 `GameModel.MaxStamina`；`AuditionCard` 中 `member.BasePower + Level * 135` 应改用 `model.PowerOf`；每张 `MiniCard` 都挂 `Mask`，首页 6 张卡即 6 组额外 stencil 批次，如无裁切需求可去掉。
6. 成员页扩到 50+ 后，`MemberGridCard` 里的 `Resources.Load<Sprite>` 必须改成分页后的 `Resources.LoadAsync`，见第 6 节第 8 条。

## 11. 2026-09-03 新增：面向“棕色尘埃 2 式”收集手游的系统层（AI-F）

用户已决定：战斗采用横版回合制战棋（BD2 式）；角色美术先用占位资产、后续替换；不再堆 50 个拆包角色，而是先把系统做扎实。本轮全部是**新增文件**，没有改动 `ChoSirenApp.cs`、`GameModel.cs` 或任何既有文件，也没有改 `manifest.json`。

新增目录 `Assets\Scripts\Systems\`（在 `ChoSiren.Runtime` 程序集内，纯 C# 部分零 Unity 依赖）：

| 子目录 | 内容 | 关键类型 |
|---|---|---|
| `Common` | 跨平台确定性随机（xorshift64*），供抽卡/战斗/掉落复现 | `IRandomSource`、`SeededRandom`、`ScriptedRandom` |
| `Economy` | 体力按时间回复、挂机产出（12 小时上限）、每日/每周任务（ISO 周） | `StaminaRegen`、`IdleIncome`、`TaskBoard`、`EconomyConfig`、`CurrencyIds` |
| `Gacha` | ‰ 概率、60 抽软保底、80 抽硬保底、50/50 + 大保底、十连保 SR、重复转碎片 | `GachaEngine`、`GachaBannerDefinition`、`GachaBannerState`、`DuplicateConverter` |
| `Tactics` | 双方 3×3 网格、速度回合、single/plus/row/column/all 范围、整数伤害公式、护盾/增减益、冷却、贪心 AI、自动战斗、星级、掉落 | `BattleSimulator`、`EnemyAi`、`DropResolver`、`TacticsManifest` |
| `Story` | 视觉小说脚本（say/choice/bg/bgm/sfx/show/hide/set/jump/jump-if/end）、标签跳转、跨章 flag | `StoryScript`、`StoryRunner`、`StoryFrame` |
| `Data` | 所有数据表的统一加载与校验（聚合全部错误而非只报第一个） | `GameDataRepository`、`GameData.Repository`、`IGameDataSource`、`IJsonReader` |
| `Presentation` | 大厅角色表现抽象，Spine 接入点 | `ICharacterStagePresenter`、`SpriteSequenceStagePresenter` |

新增数据表 `Assets\Resources\Data\`：`economy.json`（10 条日/周任务）、`gacha.json`（3 个卡池：星璃 UP 池、常驻池、服装池）、`tactics.json`（11 个技能、9 名成员单位 + 3 种敌人、第 01 章 1-1～1-4 四关）、`Story\chapter-01.json`（原创第一章，2 分支）。角色 ID 沿用现有 9 名成员，卡池角色与战斗单位做了交叉校验。

新增测试 `Assets\Tests\EditMode\Systems\`：6 个文件、52 个用例。其中 47 个纯 C# 用例已在 Unity 之外用 Unity 自带 Roslyn + Mono 编译并全部通过；`GameDataTablesTests`（5 个，依赖 JsonUtility）已用 Newtonsoft 等价校验通过：四关 40 级 SSR 满编自动战斗均 3 星胜利、1 级 R 双人均失败；第一章两条分支都能走到结尾；星璃池 10 万抽综合 SSR 3.48%。**接手 AI 请在 Unity 里跑一次全量 EditMode 确认。**

尚未接线（系统层数据已进 `GameModel`，面板已接完；见第 12 节）：

1. ~~`GameSave` / 体力挂机任务抽卡字段~~ — 已在 `GameModel` v2 存档落地。
2. ~~选秀 → `GachaPanel`~~ — 已接。
3. ~~关卡 → `TacticsBattlePanel`~~ — 已接；旧 `StoryBattlePanel` 可删。
4. ~~`StoryPanel`~~ — 已接第 01 章；缺背景资源。
5. Spine：安装 spine-unity 后做 `SpineStagePresenter`（仍待做）。

## 12. 2026-09-03 系统面板接线（已完成）

本轮把 `Assets\Scripts\Panels\` 接到大厅与冒险图，并让 `GameModel` 实现面板契约。

### 完成项

| 项 | 说明 |
|---|---|
| `GameModel : IGachaService, ITaskBoardService` | `Banners` / `BannerState` / `ItemDisplayName`；既有 `TryPull` / `TaskViews` / `TryClaimTask` / `Balance` / `ClaimableTaskCount` |
| 选秀导航 | `ShowScreen("audition")` → `GachaPanel.Open(safeRoot, model, model, …)`；旧 `BuildAudition` 仍保留但不入口 |
| 每日任务卡 | → `TaskBoardPanel.Open`；`ClaimableTaskCount>0` 时粉色圆点 |
| 闪耀舞台计划 | 主按钮领取挂机收益（`PreviewIdleIncome` 金币/星钻）；无可领时「暂无收益」 |
| 顶栏体力 | `/{StaminaCap}`；未满时附 `mm:ss`（`SecondsUntilNextStamina`），`Update` 整秒节流 |
| 冒险关卡 | `StartChallenge` → `StartStageBattle("stage-1-N")` + `TacticsBattlePanel`；结算 `SettleStageBattle` |
| 剧情入口 | LevelMap `StoryChapter-01` → `TryStartStory` + `StoryPanel`；结束 `CompleteStory` |
| 字体 | 5 处 `NotoSansSC-Subset`（保留 LegacyRuntime 回退）；`ChoSirenProjectSetup` bundleVersion `0.3.0` |
| 中文文案 | Gacha「UP」→「限定」；缺省背景改为「背景资源缺失」；未知道具不再露出英文 id |
| 测试 | `GameplayPanelTests` / `UiInteractionSmokeTests` 清 `SaveKey`+`LegacySaveKey`；新增抽卡/任务板/剧情/战棋点击链 |
| 门禁 | EditMode `TestResults\editmode-wired.xml` **126/126**；PlayMode `ui-wired.xml` **5/5** |
| 构建/体积 | Windows 已重建；`resources.assets.resS` **60486304** vs 旧 **138645184**（约 -56%） |
| 截图 | `Builds\Smoke\wired-{lobby,gacha,taskboard,tactics,story}.png` |

### 接线约定（给下一位）

- 面板宿主一律 `safeRoot`（或 LevelMap 的 `transform.parent`），返回回调回大厅 / LevelMap。
- 抽卡种子在 `GachaPanel` 内：`(ulong)DateTime.UtcNow.Ticks ^ 总抽数`。
- 战棋烟雾：已通关节点点「开始挑战」只出战报；`SmokeCapture` 的 `tactics` 根据 `LevelMapPanel.SelectedStage` 动态走当前可挑战的 `Level-1-*`。
- `Tools\Run-UnitySafe.ps1`：若 38000 已被占用会警告并继续；仍建议单实例跑 Unity。
- 未动：`Releases\`、GitHub Pages、`NotoSansSC-Regular.otf`（仍在 Resources，体积优化下一刀可移出）。

### 已知未完成 / 可跟进

- 剧情背景图尚未进包（UI 显示「背景资源缺失」）。
- 旧 `StoryBattlePanel` / `BuildAudition` 代码仍在，可后续删除。
- 成员 50+ 分页 UI、Spine 大厅替换、Addressables 仍按第 6 / 11 节推进。

## 13. 不要做的事情

- 不要把同一个角色动画的 238 帧当成 238 名成员。
- 不要复制、镜像或轻微调色同一立绘来凑 50 名。
- 不要把 463.9 MiB 原始大图全部直接放进 `Resources`。
- 不要随意重排旧 9 名数组后继续读取 v1 索引存档。
- 不要同时让两个 Unity Editor 打开同一项目。
- 不要在测试通过前覆盖 `Builds\Windows` 或部署线上稳定链接。
- 不要删除用户原素材、旧存档或旧可运行构建。
- 不要改 `Releases\` 或 Pages 稳定链接，除非用户明确要求。
