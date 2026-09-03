# CHO-SIREN Unity 多 AI 协作交接

更新时间：2026-09-03（Asia/Shanghai）
Unity：`6000.6.0f1 (f7f8ed4d1e24)`
设计基准：竖屏 `720 × 1536`
主场景：`Assets/Scenes/Main.unity`

新电脑接手先读：`Docs/GETTING_STARTED.md`、`Docs/CONTINUITY.md` 和本文的文件所有权、验收清单与当前事实。

新电脑取得源码的权威命令：

```powershell
git clone --branch master --single-branch https://github.com/BOJUEJUN/cho-siren-preview.git cho-siren-unity
```

克隆后可双击根目录 `打开CHO-SIREN编辑器.cmd`；它只接受 Unity `6000.6.0f1`。`Builds/Windows/开始游戏.cmd` 只运行成品。Windows 构建生成的 `Builds/Windows/打开Unity编辑器.cmd` 仅用于从本机构建目录返回完整源码工作区，单独分发时不具备编辑能力。

## 1. 当前事实，不要沿用旧状态

- 本目录现在是 Git 仓库，本地源码分支 `master` 跟踪远端源码分支 `origin/master`；2026-09-03 最终构建稳定性检查点从 `ec719a4` 开始。
- `origin` 已配置为 `https://github.com/BOJUEJUN/cho-siren-preview.git`。同仓库 `master` 保存完整 Unity 源码，`main` 只存 GitHub Pages 网页预览；不能把 Unity 源码推到 `main`。
- 当前工作树包含多人并行中的界面、音频、AI 美术、测试和文档改动；接手者先运行 `git status --short`，不得覆盖不属于自己的修改。
- `Builds/`、`Releases/`、`Library/`、`Logs/`、`TestResults/` 是本机产物且被忽略，不是“源码已交接”的证据。
- 历史文档里的 `126/126`、`5/5` 等数字只代表当时构建；当前 dirty worktree 必须重新跑全量测试，不能复制旧数字作为新验收结果。
- 当前 tracked worktree 和全部 Git 历史都没有 ≥50 MiB 单文件；Git LFS 客户端存在但没有跟踪对象/规则。最大 tracked 文件是 15.68 MiB 字体，不需要改写历史。

## 2. 当前产品方向与已落地约束

这是原生 Unity 竖屏偶像养成原型，不是 WebView。主要链路包括大厅、成员、团队、饰品、招募、任务、第一章冒险、剧情、骰子战斗、演出与本机存档。

当前必须保持：

1. 第一章完整显示 `1-1`～`1-10`，运行时 ID 为 `stage-1-1`～`stage-1-10`；十关必须顺序解锁并能真正进入战斗。
2. `stage-7-3`～`stage-7-6` 只能出现在旧存档迁移及其测试中，不能回到 UI、数据表或新存档。
3. 战斗使用五枚骰子的保留/重投、牌型倍率、能量重投和真实 `BattleSimulator` 结算，不得退化为假进度或静态展示。
4. 顶栏只保留邮件与设置两个操作；音乐切换只在设置内。顶部不得重新出现 Music、开/关小字、斜杠、无状态通知红点或 Accent 小方块。
5. 顶栏体力只显示 `当前值/上限`，例如 `108/120`；不得显示 `05:12` 等倒计时，也不得为倒计时保留空白占位。
6. 全部动态 Unity UI Button 使用统一 hover/press 反馈；不得破坏页面自己的选中颜色或基准缩放。
7. 所有玩家可见文案使用中文；`SSR/SR/R`、评级和必要的技术缩写例外。
8. 玩家存档位于本机 `PlayerPrefs`，不会随 Git 跨设备同步。

## 3. 当前视觉选择

- 大厅：沉浸式霓虹舞台；角色是主视觉，入口使用轻量 AI 舞台装置，禁止恢复厚重大卡遮住角色。
- 招募：`Art/GachaAI/gacha-stage-bg-ai-v1-20260903` 为舞台背景；三个卡池使用对应 AI 徽记，十连使用晶体边框，概率/保底是轻玻璃信息层。
- 任务：使用 `Art/TaskAI` 的舞台任务视觉，保持任务状态与领取逻辑。
- 战斗：使用 `Art/BattleAI` 的 HUD/技能资产，并以 `Art/BattleUser` 中用户确认的王座 BOSS 和六面心形水晶骰为主视觉；真实骰子与回合状态必须可读。
- 饰品：C1“星环试衣舱”，运行时背景 `Art/AccessoryAI/accessory-calm-bg-ai-v2-20260903`；六槽沿星环、属性为窄玻璃读数。旧方案只作为 `Docs/DesignReferences/User/` 参考保留。
- 成员：高密度五列动态图鉴、响应式行数、分页贴底；任何卡框都必须是真 Alpha，绝不能显示烘焙棋盘格。
- HUD 图标：白色立体线条、青粉霓虹。音乐资源文件可保留作素材，但顶部代码不得引用它。

AI 生成资产及 `.meta` 在最终 checkpoint 前必须被 Git 跟踪。`Tools/Check-DevelopmentEnvironment.ps1` 会检查当前运行时关键资产是否存在并已跟踪。

## 4. 文件所有权规则

开始任务前先在交接消息中声明路径。以下高冲突文件同一时刻只允许一个写入者：

- `Assets/Scripts/ChoSirenApp.cs`
- `Assets/Scripts/GameModel.cs`
- `Assets/Scripts/Panels/*.cs` 中正在改的具体面板
- `Assets/Resources/Data/*.json`
- `Assets/Scenes/Main.unity`、Prefab、ScriptableObject
- `.gitignore`、`.gitattributes`、本文和连续开发文档

推荐边界：

- 系统规则：`Assets/Scripts/Systems/**` + 对应 EditMode 测试
- 独立面板：单个 `Assets/Scripts/Panels/<Panel>.cs` + 对应 PlayMode 测试
- 美术：明确的 `Assets/Resources/Art/<Feature>/**`，连同 `.meta`
- 验证：只运行测试/构建/截图，不顺带改产品逻辑
- 可移植性：Git 规则、Docs、Tools 环境检查，不碰游戏 Panel

只做小范围 patch，不整文件覆盖。共享工作树有其他修改时，不使用 `git checkout --`、`git reset --hard` 或清理命令。提交、tag、push 与部署由最终维护者统一完成。

## 5. 代码与数据权威位置

- 应用壳与大厅/成员/团队/饰品：`Assets/Scripts/ChoSirenApp.cs`
- 第一章地图：`Assets/Scripts/LevelMapPanel.cs`
- 新面板：`Assets/Scripts/Panels/`
- 骰子：`Assets/Scripts/Systems/Dice/`
- 战斗模拟：`Assets/Scripts/Systems/Tactics/`
- 数据：`Assets/Resources/Data/`
- 第一章战棋：`Assets/Resources/Data/tactics.json`
- 第一章剧情：`Assets/Resources/Data/Story/chapter-01.json`
- 用户确认的王座 BOSS 与六面心形水晶骰：`Assets/Resources/Art/BattleUser/`
- 用户副本图及去文字夜城底图：`Assets/Resources/Art/LevelMapUser/`
- 构建入口：`Assets/Editor/ChoSirenBuild.cs`
- Unity 安全启动：`Tools/Run-UnitySafe.ps1`
- Windows 双击编辑入口：根目录 `打开CHO-SIREN编辑器.cmd`
- WebGL 交付验证：`Tools/Test-WebGLDeliverable.ps1`

不要把 238 张 `HeroFrames` 当成 238 名成员；它们是一名角色的一段序列帧。第三方角色素材公开发布前必须确认授权与内容分级。

## 6. AI 修改前检查

1. `git status --short`，记录已有 dirty paths。
2. 阅读目标文件当前版本，不能只依赖旧对话摘要。
3. 用 `rg` 查调用方、测试、数据 ID 与资源路径。
4. 声明独占文件；发现冲突就停在只读审计，不抢写。
5. UI 任务先确认已选视觉图与实机截图，再修改布局。
6. 数据迁移必须保留兼容测试，不能直接删除旧存档字段。

## 7. 功能验收清单

- [ ] Unity Console 0 error；没有 Missing Script / Missing Sprite / 粉色材质。
- [ ] 大厅五个底部导航都能进入并返回，弹窗不会留下透明拦截层。
- [ ] 顶栏只有邮件、设置；无 Music、Notice、Badge、Accent；设置内音乐切换仍真实控制音频。
- [ ] 体力只显示 `数字/数字`，资源组紧凑且不重叠。
- [ ] 动态按钮 hover 放大、按下回弹、离开恢复；移动端点击仍只触发一次。
- [ ] 第一章地图显示 1-1～1-10；锁定、解锁、体力消耗、结算星数和下一关推进有效。
- [ ] 骰子 Begin、保留、两次普通重投、100 能量全重投、牌型与成型骰高亮有效。
- [ ] 骰子倍率只应用一次，伤害/治疗/护盾结算正确；自动模式不会卡死。
- [ ] 暂停、胜利或失败后不能继续提交动作；回合和阶段边界显示正确。
- [ ] 招募单抽/十连、货币扣除、概率展示、保底累积和结果关闭有效。
- [ ] 任务领取不会重复发奖，状态刷新正确。
- [ ] 成员筛选、搜索、分页、详情、训练、编队保持有效。
- [ ] 饰品选择、装备、设置与战力变化保持有效。
- [ ] 旧 `stage-7-*` 存档迁移后合并到新 ID 且不重复星数。
- [ ] 清档二次确认有效；存档版本迁移后旧成员/进度不串位。

## 8. 视觉验收清单

- [ ] 720×1536、较矮窗口和高 DPI 下均无裁切、重叠、文字出框。
- [ ] 大厅角色脸部/身体主视觉不被入口、CTA、顶部 HUD 或底部导航遮挡。
- [ ] 首页、招募、任务、战斗、饰品使用已选 AI 资产，不是临时纯色矩形堆叠。
- [ ] 十连按钮文字不压晶体边框；卡池徽记可读但没有厚重底色。
- [ ] 战斗骰子、牌型、倍率、行动对象、血量、能量和回合均可辨认。
- [ ] 成员页保持高密度五列与贴底分页；透明卡框中心/四角没有棋盘底。
- [ ] 饰品角色对准星环，六槽围绕节点，右侧读数轻薄，底部图鉴贴舞台。
- [ ] 交互态不会导致布局跳动、父布局重排或选中缩放丢失。
- [ ] 所有玩家可见文字中文化，无调试路径、资源 ID 或英文占位。

## 9. 测试、构建与交接证据

最低门禁：全量 EditMode、全量 PlayMode、Windows Player 实机烟雾、WebGL 构建与 `Tools/Test-WebGLDeliverable.ps1`、视觉任务 720×1536 截图。

本机受管环境应通过 `Tools/Run-UnitySafe.ps1` 启动 Unity。不要并行打开同一项目，也不要在测试命令里添加 `-quit`。完整命令见 `GETTING_STARTED.md`。

每个 AI 结束时必须报告：

```text
修改文件：
行为变化：
测试/编译结果：
实机或截图结果：
未运行项及原因：
剩余风险：
是否新增未跟踪资产：
```

## 10. 最终 checkpoint 前门禁

- `Tools/Check-DevelopmentEnvironment.ps1` 没有 `FAIL`。
- `git diff --check` 通过。
- 所有运行时素材与 `.meta` 已跟踪，关键路径不被 `.gitignore` 命中。
- `git remote -v` 指向约定 GitHub 仓库，当前源码分支跟踪 `origin/master`，而非 Pages 的 `origin/main`。
- 当前测试证据来自同一源码 SHA。
- commit push 成功，并在另一目录做一次 fresh clone + `git lfs pull` + 环境检查。
- 重要版本创建并 push annotated checkpoint tag。
- 只有以上均通过后，才从同一 SHA 构建/部署 Windows 与 WebGL 预览。

## 11. 明确禁止

- 禁止把缓存、构建、压缩包、Python vendored packages 强制提交。
- 禁止对已有远端擅自运行 LFS 历史迁移、filter-repo 或 force-push。
- 禁止把稳定 Pages 链接更新到未提交、未测试的工作树。
- 禁止复制/镜像/轻微调色同一角色来凑成员数量。
- 禁止删除旧存档迁移兼容、用户原始素材或可恢复 checkpoint。
- 禁止用旧测试数字冒充当前验证。
