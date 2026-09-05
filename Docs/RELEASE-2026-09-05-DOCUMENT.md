# 企划职业与实时战斗正式发布

验收时间：2026-09-05 17:04，Asia/Shanghai。

## 发布身份和边界

- 用户在16:00截止后明确单次授权“上线”；没有恢复持续优化目标。
- 源码：`2237c51316fe30e642670bc7116bce1d1c5ebc5e`，分支 `ui/battle-layout-20260905`。
- Pages：`a7d14c7078b98615f6ef6b9796035ff4ad33bbea`，main由 `6693a9911f137aad2ef0e2166e28cd43668bb62c` 快进，push退出0。
- 正式网址：https://bojuejun.github.io/cho-siren-preview/?v=a7d14c7 。旧网址参数不锁定旧包，刷新时加载器读取当前版本清单。
- 发布前已fetch检查；源码包含已接受的 `fa809db`、`721e6b4`、`a1197ea`、`6e0f1a2`，未从旧master重建。
- 仅推送Pages main。源码改动仍保留本地分支，不声称已远端同步。未纳入无关Live2D原型、`.agents`、辅助脚本或`.DS_Store`。

## 发布内容

- 四职业主唱、主舞、Rapper、门面；成员/编队/选秀/战斗资料同源，兼容旧DJ和支援输入，不清空旧成员、等级或货币。
- 含此前待发布的实时普攻/双技能、骰型持续增益、能量与整场重投、种族技能、成长训练和第一章节奏。
- 含伤害数字分栏及头像单一飘字；战斗顶部单排控制、骰子去方块底、清晰头像及分区保留。
- 不重做已确认的顶部HUD、底部导航、章节地图及双面试池；不恢复全屏按钮、SSR/SR/R抽卡界面。
- 本次没有新增实现企划中冲突的收费/招募评级/付费突破规则，也未擅自更换引擎。

## 构建与自动验证

- Unity `6000.6.0f1`，方法 `ChoSiren.Editor.ChoSirenBuild.BuildWebGL`。
- 新构建目录：`Builds/WebGL-Document-Release-20260905`，不是早期035a0ab候选。
- `Logs/build-document-release-20260905.log` 第4013行 `Build Finished, Result: Success.`；进程退出0，临时移动的HeroFrames已恢复，tracked源码构建前后干净。
- `TestResults/editmode-document-careers-v2-20260905.xml`：271/271（包括640场成长战斗回归）；`TestResults/playmode-document-careers-20260905.xml`：46/46。二者是同一源码测试，不把本次网页检查重复计算成新Unity测试。
- `node --test Tools/test-stage-webgl.mjs`：5/5；规范暂存工具dry-run/apply通过。
- Pages `npm run check`：文件散列、体积、竖屏canvas、视频、脚本语法及12/12加载器测试通过；`git diff --check`通过。

## 构建身份

- data：`2f364b30e7faf34727ef21fb714a42d4.data.unityweb`，74312414字节。
- framework：`64056aa04fb1e0face6166ade064d9b0.framework.js.unityweb`，78094字节。
- wasm：`beb81cdbf67ab6c5d4fb21a8cb62b8ce.wasm.unityweb`，9001164字节。
- loader：`ef287f73f971d6347d26adb18c28dbb7.loader.js`，41128字节。
- HTML SHA-256：`dfa7b691a5f73b410352f899da60502bf88592bc9c8303fcafda0bd36c956ce5`。
- 版本清单 SHA-256：`ad374ee164d555714dd30d6ce5ab2b4fee14986b10214a3688f8f3a6760aea2a`。
- 大厅视频未改，SHA-256 `aaa9231a0cf5c681d4fe1d142d22ade2e7f9c1b935f5e99914d7165791d0065a`。

## 实际浏览器验收

使用隔离Chrome上下文，不操作用户日常浏览器存档。内部新构建检查390×844，战斗另查320×568和720×1536，暂停/继续可用，伤害分栏；首关12.6秒三星胜利。截图位于本机 `Artifacts/qa-20260905/release-*.png`。

发布后重新访问正式GitHub Pages，断言HTTP响应的HTML散列与本地一致、版本清单current完全一致，并记录**实际浏览器请求的4个新构建文件全部HTTP 200**。不是仅凭git push成功或网址参数判断最新版。

实际点击并目视检查门面筛选、团队四职业、线上池、线下池、章节地图、战斗与胜利弹窗；官网首关12.0秒、无人倒下、三星结算。`pageerror`为空，校验进程退出0。正式截图 `Artifacts/qa-20260905/live-*-a7d14c7.png`；这些是本机验收证据，不是已上传的公共素材。

## 回退与未验范围

- `build-versions.json.previous`及Build目录保留6693a99完整4资源；仅移除更早的 `3321b5...data`、`2e7cf...framework`、`0e26de...wasm`、`6f99b3...loader`。可从Git历史恢复，不能递归清空Build目录。
- 若出现真实回归，应在确认没有其他新发布后，用可审查的恢复提交恢复6693a99的整套入口/清单与所需资源，重新校验后快进发布；禁止force push、reset --hard或只换单个wasm造成混包。
- GitHub接受了本次push，但对70.87MiB的data给出超过50MB建议值的警告；未超100MiB硬限制。大包及移动网络加载速度仍是已知风险，不因此宣称iPhone性能问题全部解决。
- 本次没有真实iPhone、Windows成品测试；浏览器窄视口不等于真机认证。完整经营/魅力收益、装备、突破、召唤等仍按企划对照记录列为欠项。
