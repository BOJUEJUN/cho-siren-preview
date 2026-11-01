# CHO-SIREN 当前交接 · v0.3.66 / R62

正式版本 **v0.3.66 / PSD-20261013-NATIVE-ALPHA-R62**，网页提交 `c7fbd1a25c76ad318432f09e877994c3fbd3de01`，备份分支 `backup/v0.3.66`。公开站已实际进入大厅、成员列表和雾白详情；PNG新立绘已生效，原有余额、等级、好感与队长状态正常显示。公开页面控制台错误为0。

[正式游戏](https://bojuejun.github.io/cho-siren-preview/?v=0.3.66) · [线上雾白截图](/Users/bojuejun/Documents/Codex/2026-10-09/kan/outputs/ui-unification-20261013/public-r62-wubai-final.jpg) · [模拟器截图](/Users/bojuejun/Documents/Codex/2026-10-09/kan/outputs/ui-unification-20261013/simulator-r62-wubai-final.png)

## 本次修复

1. 雾白全身立绘用内置 imagegen 重做原生透明背景，显式 `transparent_background: true`。清除发丝、颈部、手臂空隙等位置的白底，沿用人物、粉白配色、尾巴和持麦构图；生成式重绘有细节变化，原PNG留存，可恢复。没有用Python二次抠图。头像原件保留；一次头像生成被服务安全系统拒绝，没有继续尝试。
2. 新PNG为1069×1471 RGBA，alpha范围0–255，约65.51%像素全透明，主体alpha主要253。已在深色和青色背景、Unity运行图、桌面正式网页及实际iOS模拟器中检查。原始输出、精确提示词、失败记录、透明统计和对照图都在输出目录。
3. 实际iPhone18Pro/iOS27模拟器Safari发现详情页的快捷穿戴/全部解除被底部浏览器栏裁切。仅成员详情局部按宽、高缩放：最小设计高度1384、顶部lift50；工具栏引起视口变化时重新适配已有Page，保留同一成员、页签与回调。共享Canvas、其他页面坐标和战斗数值保持现有实现。
4. 已在真实模拟器点击快捷穿戴：雾白战力212→233、体力420→470、攻击32→35、防御10→11，三格装备出现；点击全部解除后恢复212/420与空槽。点击大厅成功返回，再打开四位固定Q版的手机练习室。

## 验证与边界

- 本次新增隔离PlayMode：1/1通过。用真实RectTransform世界坐标检查短视口的动作可见性，并验证开页后720×1268↔720×1536变化时，同一成员Modal与技能页签保留。
- R61基线保留：EditMode502/502、UI/地图PlayMode11/11和20张原生运行图。本次没有宣称重新跑完整基线；R62另有1张原生雾白运行图和实际浏览器/模拟器截图。
- 网页加载、角色生命周期与音频72/72；service-worker语法、正式WebGL交付物和网页版本/哈希校验通过。正式构建 `302153538` bytes，用时 `00:01:38.6709620`。生产公司名已恢复。
- 当前线上引擎文件、入口、release.json、build-versions.json和透明UI底板均完整SHA256匹配本地。大data文件用8份已检查字节范围与长度的HTTP206响应按序校验完整内容；其余当前文件HTTP200。上一版R61四件HEAD仍HTTP200且长度匹配。未把未发布候选包当上一版。
- 模拟器为实际iOS27 Safari运行，截图1206×2622、scale3；不是浏览器改尺寸，也不等于实体手机。实体手机、低端机持续帧率仍未验证。
- 后续Mac锁屏中止了原生界面操作，未尝试自动解锁或改系统设置。练习室“完成·返回”已确认完整可见；随后的点击被锁屏拦截。本次正式页验收继续通过后台浏览器完成。
- 正式release.json的 `publicRuntimeVerified` 为发布前快照false；发布后已验证true，以本报告和 `browser-qa-r62.json` 为准。

## 发布与恢复

- [Quality checks](https://github.com/BOJUEJUN/cho-siren-preview/actions/runs/38402067886)：成功。
- [Quality checks](https://github.com/BOJUEJUN/cho-siren-preview/actions/runs/38402018288)：成功。
- [pages build and deployment](https://github.com/BOJUEJUN/cho-siren-preview/actions/runs/38402016646)：成功。

| 当前引擎文件 | bytes | SHA256 |
| --- | ---: | --- |
| 34eeac273e3ab9689789d67d1f22ec99.data.unityweb | 86155726 | `34eeac273e3ab9689789d67d1f22ec998c9b3ca51de8d882fe81eced80623e0e` |
| 7012e16a1589eaff9f19a94c04ba88b7.framework.js.unityweb | 81722 | `7012e16a1589eaff9f19a94c04ba88b7b1bc457e0de52315d35f0e3d457dbfbd` |
| feae9de99e9e6ef1c6cd0fbbbc806fe3.wasm.unityweb | 9375264 | `feae9de99e9e6ef1c6cd0fbbbc806fe3cee460b46a97d8cb4f6c619dcae78f32` |
| c1b7b9eba0877e517731f7dd43cb7d14.loader.js | 41128 | `c1b7b9eba0877e517731f7dd43cb7d142f0cf42bb0a5cac2ea0e32edf6a40eb5` |

正式源码：`/Users/bojuejun/cho-siren-unity`。本轮只合入任务文件，保留原1048项未提交/未跟踪工作。原始立绘、R61代码/文档备份在 `/Users/bojuejun/Documents/Codex/2026-10-09/kan/work/ui-unification-20261013/before-r62`；初轮UI覆盖前备份在 `/Users/bojuejun/Documents/Codex/2026-10-09/kan/work/ui-unification-20261013/before-integration`。逐文件摘要在 `integration-manifest.json`。旧8086预览未覆盖，当前请以正式网址为准。

图集：`/Users/bojuejun/Documents/Codex/2026-10-09/kan/outputs/ui-unification-20261013/review.html`。精确生成调用：`/Users/bojuejun/Documents/Codex/2026-10-09/kan/outputs/ui-unification-20261013/wubai-imagegen-calls.json`；立绘PNG：`/Users/bojuejun/Documents/Codex/2026-10-09/kan/outputs/ui-unification-20261013/wubai-clean-v2.png`。只针对雾白立绘做此次生成，未恢复已取消的3D骰子任务。R61完整UI统一详情仍保留在原报告下方。下一版使用v0.3.67 / R63。
