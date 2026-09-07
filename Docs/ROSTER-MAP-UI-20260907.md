# 战斗单排成员与地图比例调整

源码 e720c34；Pages 147cbfd；回退资源保留 5ed4088。

- 战斗删除 AttackPlayerProxy 及出手时重复头像，保留真实成员卡。成员行位于骰子上方；双方攻击均指向实际立绘/头像，不按模拟器枚举顺序猜阵位。连线裁切范围不进入骰子台。
- 章节返回按钮由76×66缩为48×48，采用程序绘制细箭头。掉落图标从32增加到68，五项横排使用632宽独立区，名字字号从10增加到13。
- 关卡信息卡高度318，信息/掉落/消耗与挑战按钮独立排列；章节奖励与任务收为88高底栏。原城市、关卡环、挑战按钮和宝箱素材保留。
- 不改数值、奖励记录与存档，不清档。

## 验证

- EditMode 348/348：TestResults/editmode-roster-map-20260907.xml。
- PlayMode 111/111：TestResults/playmode-roster-map-20260907.xml。
- 测试后仅调整返回箭头旋转符号，最终WebGL真实浏览器已确认向左。
- WebGL成功：Builds/WebGL-roster-map-20260907；Logs/build-roster-map-20260907.log。
- Pages npm run check通过，12项加载器测试通过。
- 本机实际Chrome：Artifacts/qa-minute-1788784261922。地图、掉落详情、挑战入口、实际战斗、切换第二成员查看技能、暂停均通过；没有pageerror。
- 线上新loader于2026-09-07 12:35:32 UTC返回200。独立Chrome打开147cbfd，实际进入地图并开始战斗，放大的奖励图标和骰子上方单排成员均已生效，无pageerror。证据：Artifacts/qa-minute-1788784603607/live-map.png、live-single-roster.png。
- 仅移除更早 be7675d 的四个构建文件，Git历史可恢复；保留最近5ed4088整包用于回退。
