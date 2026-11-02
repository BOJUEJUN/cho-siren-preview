# 2026-11-02 Web 验收版 0.9.25

本轮仅 Web；原生安装包、客户端更新清单、手机和 Dock 保持 0.9.24。练习室未改。

## 已实现
- B 侧边半光碟地图，采用用户暂选 G 光碟，保持正圆；每关独立场景，20 张 JPEG 按选中关卡切换。关卡进度、奖励、体力、队伍选择和章节切换沿用真实模型。
- 取消队伍战力/改名笔和头像姓名上的额外矩形黑底；头像对齐现有 C 框圆孔，文字留出空间。
- 大/小 Boss 原视频完整入场；入场期间战斗和充能不推进。
- 默认人物增大，攻击出框，血蓝条位置保留。四种技能立绘按实际屏幕边界贴边，入场、保持、收回均夹紧裁切边，手机与 PC 共用。
- 夜莺大招使用用户原视频全长，播放完成事件结束，已移除 4.4 秒截断。
- 得分骰子采用大招满能同类动态火焰；特效在骰面上方，非得分骰子不亮。
- 有效战斗时间充能：四位置 12/15/20/30 秒，暂停/入场/大招视频停表；只给队长骰子小加成，大顺子第一位约 11.54 秒。
- 换队长只换标记与加成，不换站位；横向拖动卡片换位，保留能量，使用新位置速率。释放大招仅消耗本人能量。
- 另生成 H/I/J 光碟候选并已展示，未选定；不替换 G。

## 验证
证据目录：`Builds/qa-logs/web-review-1102/`。
- EditMode 10/10；PlayMode 7/7；人物卡峰值与骰子视觉捕获通过。
- WebGL 最终构建成功：`build-final.log`；网页检查 113/113；最终资源哈希验证 `final-verification.json`。
- 独立浏览器测试存档，未改用户存档：真实 WebGL 地图背景切换、队伍页面、骰子光效和人物卡截图。
- 实际 WebGL 夜莺视频到 ended=5.184 秒（容器时长）；小 Boss ended=4.041667 秒；大 Boss ended=6.041667 秒；不是仅浏览器独立视频播放。
- 实测发现关卡编号被 Noto 行高裁掉，已补 verticalOverflow 并重建，`final-map.png` 已确认编号可见。
- 真实 WebGL 拖动：`drag-before-controlled.png` / `drag-after-controlled.png`，星璃从第一位拖到第二位，队长标记跟随，绯音移至第一位；不是仅数据单测。
- `final-team.png`：头像在圆框内、队伍改名笔与金色战力无硬黑底。
- 编译/自动测试不等于用户美术认可；后续以用户 Web 验收反馈为准。

## 发布状态
Pages 仓库 `/Users/bojuejun/cho-siren-web`，Web 提交 `cae6c29`，发布编号 `WEB-20261102-REVIEW-R159`。
GitHub Pages 部署已成功；公开 release.json 已确认 0.9.25 / R159；全新独立浏览器已加载本轮构建哈希并进入大厅，零 JS 错误。公开运行证据：public-runtime.json / public-initial-0925.png。
注意：GitHub Quality checks 在 checkout 阶段超时取消，未执行测试；本机同套 113 项检查已通过。Pages 的构建和部署均 success。
实际游戏链接：`https://bojuejun.github.io/cho-siren-preview/?v=0.9.25`。
旧哈希构建全部留存；只更换当前四个构建资源和新增本轮场景/视频。原生下载和更新元数据未动。

## 代码入口
- `LevelMapPanel.Vinyl1102.cs` / `StageSceneArt1102.cs`：地图。
- `ChargeOrder1101.cs` / `TacticsBattlePanel.Formation1102.cs`：位置充能与拖动。
- `BossEntrance1102.cs` / `TacticsBattlePanel.BossEntrance1102.cs`：Boss 视频。
- `BattleUltimateShow1020.cs` / `Resources/UltimateVideos/manifest.json`：完整大招。
- `BattleActiveCloseup1019.cs` / `TacticsBattlePanel.BattleFx1101.cs`：跨屏贴边。
- `DiceScoreFire1102.cs` / `DiceScoreAura.cs`：得分骰子光效。
- `BattleMemberAlbum929.cs` / `HeroCardLunge1031.cs`：人物放大出框。

## 保留事项
工作区已有 AGENTS.md、Packages、字体列表、ResourcesArchive 等其他 AI 的变动未清理。用户未授权这轮全端发布，先等 Web 验收。
