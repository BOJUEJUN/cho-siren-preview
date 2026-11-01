# 当前版本 v0.3.66 / R62

雾白原生透明立绘、Safari详情高度适配与实际iOS模拟器验收已经完成。

请先读 [R62最新交接与证据](UI-NATIVE-ALPHA-2026-10-13.md)。以下R61内容作为上一版完整UI统一的历史验收，旧的“协议待确认”状态已完成；当前版本以上方R62文档为准。

---

# CHO-SIREN UI统一验收 · 2026-10-13

当前线上 **v0.3.65 / PSD-20261013-UNIFIED-UI-R61**，网页提交 `73152253f789f8b9622278feba426c463592029c`，备份分支 `backup/v0.3.65`。源码和本地真实网页已验收，GitHub两项发布流程成功，线上入口、清单、透明底板与四件引擎文件均逐字节匹配本地。公开站内验收等待用户确认首次试玩协议；没有代替用户接受协议。

[正式游戏](https://bojuejun.github.io/cho-siren-preview/?v=0.3.65) · [运行图集](/Users/bojuejun/Documents/Codex/2026-10-09/kan/outputs/ui-unification-20261013/review.html)

## 实际改动

- 当前可达的任务、团队、成员图鉴与详情、饰品及换装、选秀、地图子弹窗、剧情、练习、战斗、结算、公共设置/邮件/签到/成就/资源弹窗，以及开场网页外壳，采用同一套紫黑、白色撕纸边、水晶角与横排信息层级。主动作使用原生透明无字底板，文字、数量和状态仍由真实数据绘制。
- 保留最新大厅PSD人物、入口尺寸、动态墨迹、非均分底栏热区和已有四位新角色。皮肤装饰不拦截点击，视觉反馈不移动根热区；避免同一按钮叠两套动效。成员页去除重复标题。
- 练习室加入手机舞台与四位固定Q版主角。实际训练仍对应所选成员；结束后等级、费用和属性刷新。选秀、替换、升级、编队、领取等沿用现有模型与回调。
- 对白置于上方半透明圆框，加入纸边与唱片元素；转场统一单色圆形、当前人物和标题；唱片采用玻璃高光与透光效果。
- 战斗加入技能及被动独立图形、头顶生命条与数字、渐变无数值能量条、守护次数、星形飞行反馈与骰子轻浮动。继续保留原组合火焰、真实攻击力加成、半秒长按换队长和15秒冷却。
- 胜败结算使用竖向人物拼屏和滚动字幕。“输出MVP”按战斗记录的实际生命损失及护盾吸收计算；治疗单独记录，无输出不虚构MVP。星数、用时、倒下人数、奖励数量均为真实数据，长奖励可滚动。
- 修复训练后等级显示、Noto行高导致血量/星数/用时/数量消失、暂停时空Combo条、旧PSD人物映射和底板烘焙数值。规则及角色能力正文为22字号并可滚动到底。

## 会议事项边界

角色装备分类、替换升级、骰子火焰、攻击力加成、长按换队长等已有实现已保留；本轮补齐其可读性与统一表现。首页“专辑制作”目前实际打开成就纪录，没有声称新增专辑制作玩法。沿用现有原创BGM；本轮没有生成女团演唱歌曲或3D骰子，不恢复之前取消的3D任务。未修改战斗平衡、骰型倍率或存档结构。

## 验证

- EditMode：502/502通过。
- 当前UI路线与选秀地图PlayMode：11/11通过。20份PNG由Unity实际Camera.Render输出，拼图每格保留720×1536。真实换装与训练回调、暂停时钟、自然模拟胜利、结算只执行一次、所有返回路线均有断言。
- 网页加载/角色生命周期/音频：72/72；service-worker语法检查通过。
- 原静态校验器固定要求9张素材，已改为检查必需主页面键、所有唯一键、安全路径与内容哈希；当前67项流式素材全过，缺少主页面及重复键两个反例均正确拒绝。
- 最终Unity构建日志含 `CHO_SIREN_BUILD_OK`：302117812 bytes，data 86119979 bytes。data相较R60增加686790 bytes。保留正式R60完整四件作为上一版，未将中途候选包当兼容版。

真实网页操作：

- 430×932：大厅、任务、换装、练习室、剧情、地图、真实胜利结算与返回
- 390×844：大厅布局；320×568：设置及关于游戏，正文和关闭按钮可达
- 点击一颗骰子后只重投该骰，牌型与倍率更新；其余骰面保留
- 700ms长按夜莺卡：队长标签与守护按钮移到该卡
- 暂停后计时固定，规则滚动至末段，继续后恢复
- 最终构建真实胜利：88.6秒，0人倒下；结算奖励与MVP输出可读，返回地图成功
- 同一测试浏览器重载后保留等级、奖励与关卡进度

桌面浏览器的手机尺寸模拟不等于实体手机。实体手机、iOS Safari和低端机持续帧率未验收。

早期扩大PlayMode套件18/31通过，出现旧节点/批处理时序断言及本轮新取证启动时序失败。新取证时序已修复；旧断言未完整修复，不宣称整套PlayMode通过。原始日志保留在work目录。测试在隔离工程与公司名下运行并恢复偏好；不要在真实公司名下裸跑会清存档的历史fixture。

## 发布证据

- [Quality checks](https://github.com/BOJUEJUN/cho-siren-preview/actions/runs/38394785732)：success
- [pages build and deployment](https://github.com/BOJUEJUN/cho-siren-preview/actions/runs/38394784253)：success

两项均对应本次提交。`public-asset-verification.json`记录正式站入口、release.json、build-versions.json、透明底板与四件引擎文件的HTTP 200及完整SHA256一致。`release.json`的公开站内验收字段为发布前快照；发布后状态以本报告为准。

| 当前引擎文件 | bytes | SHA256 |
| --- | ---: | --- |
| 5cbebba90eb4bc2b6acaf32fa9cd12ad.framework.js.unityweb | 81722 | `5cbebba90eb4bc2b6acaf32fa9cd12adffbf0e3d6b40cc7f3a6af6160fecbfb1` |
| 5ff6dff4f753968599256c7b050fc335.data.unityweb | 86119979 | `5ff6dff4f753968599256c7b050fc335e659242b3d92a8b55aa21074c6a7f93a` |
| 773f82ee98fcb2c6aeb1213377448f99.loader.js | 41128 | `773f82ee98fcb2c6aeb1213377448f99bb546f0c0e74ea1a92ea095e12dd1cae` |
| 8b3a1bf4e0337a726b99354c7e1b346a.wasm.unityweb | 9375285 | `8b3a1bf4e0337a726b99354c7e1b346abe34c4c42238c681c1d21175115df177` |

## 素材与交接

内置imagegen生成1张无字底板、4张Q版人物；每次显式使用 `transparent_background: true`。PNG原生RGBA，alpha范围0到255，透明背景已检查，主体多数alpha接近253；没有把棋盘格当透明，也没有事后抠图。原图保留，Unity导入Q版512、底板1024、不生成mipmap。精确提示词与调用参数在 `imagegen-call-records.json`，alpha统计另存JSON。

正式源码：`/Users/bojuejun/cho-siren-unity`；网页：`/Users/bojuejun/cho-siren-web`。保留原1048项未提交/未跟踪工作，只合回本轮67项源代码、素材、模板及校验器文件；没有整体提交Unity工程。逐文件SHA256在 `integration-manifest.json`，覆盖前原件在 `/Users/bojuejun/Documents/Codex/2026-10-09/kan/work/ui-unification-20261013/before-integration`。最终构建位于 `/Users/bojuejun/Documents/Codex/2026-10-09/kan/work/ui-unification-20261013/project/Builds/WebGL`；旧8086预览未覆盖，以正式网址为新版。

图集与验收文件：`/Users/bojuejun/Documents/Codex/2026-10-09/kan/outputs/ui-unification-20261013`。隔离工程：`/Users/bojuejun/Documents/Codex/2026-10-09/kan/work/ui-unification-20261013/project`，正式公司名已恢复。公共站若仍在协议页，确认协议后继续核对大厅、任务及练习室并追加截图即可，无需重复生图或重建。下次新Unity发布使用v0.3.66 / R62。
