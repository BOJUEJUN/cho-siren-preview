# 2026-11-07 赵金六页更新 + 会议纪要落地

来源：11-07 06:09 快速会议智能纪要、企业微信聊天（赵金）、`~/Downloads` 的 6 张 PSD（主界面1)、装备2、练习室界面、任务、团队、选秀、战斗psd）和 3 段视频、进度条图。新素材都在 `Assets/Resources/Art/Ui1107/`（导入规则 `Assets/Editor/Ui1107ArtImporter.cs`）和 `Assets/StreamingAssets/Practice1107|Loading1107/`。

## 已做

| 项 | 做法 | 代码 |
|---|---|---|
| 200 级数值 | 见 `Docs/BALANCE-1107-LV200.md` | `GameModelBreakthrough1104.cs`、`BattleSimulator` |
| 大厅 | 只换背景（灰一点的新底），光碟和标志 PSD 里没变 | `Art/Reference038/home-layer-00.png` |
| 练习室 | PSD 摄像机画面：PSD 练习室底图 + 四主角抠像视频（唱歌 / 跳舞两段随机轮播、交叉淡入）+ REC 取景框和走动的时间码；点画面里的主角选人，成员卡左右箭头切换任意已拥有成员，选中的主角头上有 ▼；小猪趴在成员卡上 | `UI/PracticeVideoStage1107.cs`、`UI/StackedAlphaClips1107.cs` |
| 视频抠像 | 练习室两段用 Vision 前景遮罩（新工具 `Tools/lobbyvideo/matte_room.swift`）；加载视频白底用原 `matte.swift`，新增 `ENCLOSED_FROM_X`（保住白色小猪）和 `NO_PINGPONG`（跑步不能倒放） | |
| 加载页 | 开机画面和网页版素材加载等待：深色底 + 四主角和小猪奔跑循环 + 角色脚下粉色蝴蝶结进度条，跟真实进度走 | `UI/LoadingRun1107.cs`、`NativeTitleScreen.cs`、`ChoSirenApp.cs` |
| 库存 | 装备2.psd：标题、饰品/图鉴、稀有度排序、按部位分四行、每行四格，格子底色和下方色条表示品阶（灰/青/紫/粉，传说加金边）；下方 ‹ 1/3 › 翻页 | `ChoSirenAppInventory.cs` |
| 任务 | 统一背景（上深下浅）+ 左下角小猪；布局本来就和 PSD 一致 | `Panels/TaskBoardPanel.cs` |
| 团队 | 团队.psd：顶部队伍页签、队伍牌（点名字改名）、黑色战力条和 成员 n/4、四张紫色撕纸卡（头像、种族图标+定位、LV、种族·定位、战力、换人）、队长贴纸、更换队长 / 一键编队 | `ChoSirenAppTeam1107.cs` |
| 面试 | 选秀.psd：ID 卡叠放（头像在最前面那张）、名字、"年龄 职业 爱好"、SSR/SR、声乐/舞蹈/说唱/名气 + 等级字母、最强项红心、血型/生日、风险（原幸福感）、体力、月薪、面试 💎价格、线上/线下面试；‹ › 翻候选人；64 人的档案在 `Data/member-profiles.json` | `Panels/GachaPanel.Interview1107.cs`、`MemberProfile1107.cs` |
| 声波→声乐 | 全游戏统一改名 | |
| 十连 | 游戏里本来就没有十连入口（只剩旧代码没被调用），不用改 | |
| 战斗 | BOSS 放大 1/3（脚还站在成员卡上）；两只小怪允许更宽；大招落地时镜头先推近再快速拉远，然后震屏 | `TacticsBattlePanel.EnemyStage1031.cs`、`TacticsBattlePanel.UltimateCamera1107.cs` |
| 跳过动画 | 练习升级动画时右上角出现「跳过 ››」，点了直接出结果 | `Panels/MemberPracticePanel*.cs` |
| 左右切换按钮 | 统一成成员详情页那种白色粗箭头（`UI/SwitchArrow1107.cs`）：练习室、库存、面试、成员列表翻页 | |
| 背景渐变 | 库存、任务、团队、面试用 PSD 的上深下浅底图 | `Art/Ui1107/page-bg-board.jpg` |
| 角色体力 | 每人 120；每场战斗/每次扫荡上场成员 -8（首领关 -12），15 场打空；低于 36 疲劳（生命、攻击 ×0.9）；不够一场就不能开战，提示休息或换人；每 3 分钟回 1（6 小时满），团队卡上点体力条可花星光币补满（每点 10）。参照明日方舟干员心情：逼玩家轮换成员，不卡正常日常 | `GameModelMemberStamina1107.cs`、`ChoSirenAppTeam1107.cs` |
| 更新公告 | 列出玩家上次读过的版本之后的每一版改动（游戏内置 `Data/changelog.json`）；有更新可下时把新版本说明排在最前 | `UI/NativeTitleScreen.cs` |
| 生魂/残魂图标 | GPT 临时图 | `Art/Reward1101/*-1107.png` |

## 没做 / 等美术

- 生魂、残魂正式图标：现在是 GPT（gpt-image-2.6-flare）临时图，等赵金出图替换。
- 光碟旋转（大厅、队伍页）：要赵金拆好的光碟分层图（会议待办）。
- 小猪从遮罩跳出的动画（等赵金视频）。
- 四个种族标志的 LOGO 优化：少爷说后面再做。
- 战斗页：战斗psd.psd 和现有战斗布局一致（顶栏、敌人、成员卡、骰子），这次改了 BOSS/小怪大小和大招镜头。
