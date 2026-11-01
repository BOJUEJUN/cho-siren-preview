# 战斗特效深度调研（2026-10-21）

> 只是调研，没有改任何代码、没有打包、没有发布。示意图在 `Docs/VFX-RESEARCH-2026-10-21/`，都是用代码画的**概念图**，不是游戏素材。
> 标注说明：**[来源]** = 网上资料直接说的；**[推断]** = 我根据资料和我们代码得出的结论。

---

## 0. 一句话结论

我们的特效"低级"，**主要不是图不够好**（GPT 画的 16 张特效图其实很漂亮），而是：

1. **形状太圆太软**：程序画的光点、光环都是高斯柔光，像"Unity 默认发光"，和 GPT 画的硬边二次元图风格打架；而且全是圆的、对称的，看不出"往哪打"。
2. **图是死的**：GPT 特效图只做"放大 + 淡出"，像贴纸，图里面没有任何变化（没有撕裂、溶解、换帧）。
3. **节奏太平均**：每一下都"慢慢变大慢慢变淡"，没有"第 0 帧最猛 → 停顿 → 快速散掉"的冲击曲线；尾巴拖到 0.8 秒，太长。
4. **配套反馈不够**：被打的角色没有白闪/形变，暴击/大招没有真正的"冲击帧"（黑白反转那一下），音效只有一层，伤害数字所有情况都差不多。

**最划算的方向**：保留现在的 uGUI 结构，不换渲染管线，做「**硬边赛璐璐（二次元动画）风 + P5 红黑白冲击帧 + 更狠的节奏**」。大部分只要改代码和写 2~3 个着色器，几乎不增加包体。

对比图：
- 现在（代码近似复刻）：`VFX-RESEARCH-2026-10-21/hit_current.png`
- 建议（概念）：`VFX-RESEARCH-2026-10-21/hit_proposed.png`
- 一张灰度图 + 色带换出 4 个种族颜色：`VFX-RESEARCH-2026-10-21/gradient_map.png`
- 伤害数字现在 vs P5 风：`VFX-RESEARCH-2026-10-21/damage_numbers.png`
- 现有 GPT 特效图总览：`VFX-RESEARCH-2026-10-21/current_ultfx_art.png`

---

## 1. 现状体检（读代码得出）

| 位置 | 现在怎么做 | 问题 [推断] |
|---|---|---|
| `BattleFx1021.cs` `BattleFxArt1021` | 程序生成 Dot/Ring/Spark/Star，全部是柔边（`Pow`、`Exp` 高斯） | 柔光圆点 = "廉价发光"；和硬边手绘图混用，风格不统一 |
| `ImpactBurst1021` | 核心白闪 0.14s、光晕 0.3s、光环 0.34s、火花 7/11/16 根，总寿命 0.8s | 圆形对称，只有一半火花顺着攻击方向；所有层亮度都差不多，没有"主角"；0.8s 偏长 |
| `UIAdditive.shader` | `Blend SrcAlpha One`，`_Boost 1.35` | 全加色叠在亮色立绘上会"洗白"成一团；缺一个暗色衬底层 |
| `ScreenFlash1021` | 先 0.05s 45% 暗色，再 0.25s 彩色闪 | 这不是真正的冲击帧，太温和；动画里的冲击帧是**整屏黑白反转 1~3 帧** |
| `BattleHitStop` | 普通 0.045s / 技能暴击 0.08s / 大招 0.12s | 数值方向对，普通略短；目标没有"被打时抖动"和白闪 |
| `ShakeStage` | 沿攻击方向阻尼振动，15Hz | 已经不错；可加"trauma 平方"曲线和一点点缩放冲击 |
| `BasicStrike1021` | 4 种族各一套程序动作 + GPT 单张命中图 | 动作想法很好；但弹道/拖尾用圆点拼，命中图只缩放淡出 |
| 狐火 `FoxFire` | 代码颜色是**橙色** (255,150,60) | 但 `charm-*.png` 图是**粉色爱心火** → 颜色对不上，需要少爷定 |
| 伤害数字 `AnimatePopup` | 旧 `Text` + 上白下彩渐变；暴击只是更大 + 歪 7° | 同一目标新数字直接顶掉旧数字；暴击不够"炸"；没有 P5 味道 |
| 音效 | 每个事件 1 个 wav（hit/crit/strike-xxx…） | 没有"挥动声 + 命中声 + 材质声"分层，也没有音高随机 |
| 性能 | 每次命中新建约 20~35 个 GameObject，最多 10 个并存 | 能跑，但每次 new/Destroy 有垃圾回收，WebGL 上建议做对象池 |

---

## 2. 大厂为什么看起来"高级"（研究问题 1）

> 说明：米哈游、Shift Up（NIKKE）、Nexon（蔚蓝档案）**没有公开过战斗特效的官方讲座**，搜不到可靠的原话。下面的原则来自 Riot（英雄联盟）公开的特效规范、VFX Apprentice、RealtimeVFX 论坛点评、格斗游戏/樱井政博的"顿帧"资料、Vlambeer 的手感数字，以及 ATLUS 在 CEDEC 讲的女神异闻录 UI。这些是业界通用的规律，二次元手游也都遵守。

### 2.1 形状
- 形状是第一信息：一眼要看出**多大、往哪打** [来源 S3][S10]。弹道静止时也要能看出方向（尖头 + 拖尾）[S4]。
- 手绘形状、软硬边混合、不要叠太多互相矛盾的形状 [S6]；细碎噪点满屏 = "很吵" [S8]。
- 星铁/原神常见：高饱和 + **四角星闪光** + 每个角色自己的配色主题 [S14]。
- **我们要改**：圆点 → 顺着攻击方向的**三角碎片、刀光、拉长的尖刺**；完美圆环 → **断开的、由粗变细的冲击环**；命中点只放 1~2 个干净的四角星。

### 2.2 颜色和明暗
- 重点区域 = 最高对比、最高饱和、最多细节；"什么都重要 = 什么都不重要" [S5]。
- 大部分用中间亮度，纯白/纯黑留给焦点 [S6]；**一个主色 + 一个辅色** [S6]。
- 好的命中火花：**白 → 黄 → 橙 → 红** 的色阶 [S8]；白芯用加色，边缘用普通透明混合 [S8c][S8b]。
- 业界做法：特效图画成**灰度**，在着色器里用**色带（Gradient Map）**上色，一张图能出很多颜色 [S15]。
- **我们要改**：每个种族一条 4~5 段的**硬分段色带**（白芯 → 亮色 → 主色 → 深色描边）；亮色特效后面垫一层**暗色衬底**，在亮立绘上也看得清。

### 2.3 节奏（最关键）
- 结构：**蓄力 → 爆发 → 消散**。命中火花用"冲击型"：**第 0 帧就最强**，然后衰减 [S7]。
- 英雄联盟规范原话大意："觉得长，那就是长太多了" [S6]。
- 顿帧（Hit-stop）参考：街霸 5 轻/中/重 8/12/15 帧 [S12]；KOF13 轻 7 帧、重 11 帧 [S16]；日本学会研究最佳约 0.1~0.2 秒 [S17]；Nuclear Throne 连射每下 10~20ms [S11]。
- 樱井政博：攻守双方一起停；被打的一方**先大后小地抖**；越重停越久但有上限 [S18]。
- 两下命中间隔 < 约 8 帧（30fps）就难分辨 [S13]。

### 2.4 分层
- 一个技能一般 2~5 层动画 [S15]。Vlambeer 每一下：命中特效 + 命中声 + 材质声 + 敌人受击声 + 击退 3px/帧 + **1 帧白 + 2 帧受击** + 顿帧 [S11]。
- **建议给我们的 8 层**：① 目标白闪+压扁（1~2 帧）② 顺方向刀光/拖影 ③ 白芯四角星（2~3 帧）④ 断开冲击环 ⑤ 6~12 片定向碎片 ⑥ 低对比烟/墨残留 ⑦ 伤害数字 ⑧ 屏幕层（震动、缩放冲击、冲击帧）。

### 2.5 屏幕级技巧
- **震动用 trauma 模型**：每次命中加"创伤值"，震动 = trauma² × 最大幅度，用 Perlin 噪声，带一点旋转 [S21][S22]。
- **镜头踢**：朝攻击方向推几像素 [S11][S13]；星铁强化攻击时推近镜头、短暂隐藏 UI [S23]。
- **冲击帧**：1~3 帧整屏换成黑/白/反色 + 放射线 [S24]；绝区零制作人说参考街霸做"逐帧清晰反馈" [S25]。
- **色差**：没有后处理时，用偏移的红/蓝复制品假装 [推断]。

### 2.6 声音同步
- 声音必须和命中**同一帧**，分层：挥动声 + 受击喊声 + 材质命中声 [S13][S11]。同一种机制用同一种声音，玩家会学会 [S12]。

### 2.7 女神异闻录 5 的 UI 语言
- ATLUS CEDEC 2017：UI 从"辅助"升为"主角"；P5 主色**红**，副色压到最少；概念"Pop Punk"；菜单进一层就换布局和角度；调到"1 像素 1 帧" [S30]。
- 红黑白为主，选中项变蓝变大 [S31]；斜体文字框反复迭代才可读 [S32]。
- 总攻击（All-Out Attack）：红黑平面背景 + 敌人剪影 → 1 帧把 3D 换成同姿势 2D 立绘 → 泼墨背景 + 剪影爆开 → 暗色 2D 模糊横扫 + 拟声字 → 最后一闪清屏 [S33]。
- 暗喻幻想（GDC 2025）：切入和特效排好顺序让紧张感快速上升 [S34]。
- **P5 味道配方** [推断]：一切倾斜 5~15°；红黑白硬边锯齿板；"盖章式"出现（1.15→1.0，约 4 帧 + 1~2 帧白闪），不用淡入；暗部加网点。

---

## 3. 技术选项评估（研究问题 2）

| 方案 | 画面提升 | 制作成本 | WebGL/iOS 性能风险 | 包体 | 和 P5 风搭配 | 结论 |
|---|---|---|---|---|---|---|
| **自写 UI 着色器**（硬边阈值 + 色带 + 溶解 + 滚动噪声扭曲 + 极坐标环） | ★★★★★ | 中（2~3 个 shader） | 低~中（怕多层全屏叠加的填充率） | 几乎 0 | ★★★★★ | **首选** |
| **改节奏/反馈代码**（顿帧、白闪、冲击帧、数字、震动） | ★★★★★ | 低 | 极低 | 0 | ★★★★★ | **首选** |
| **序列帧（Flipbook）** 16 帧 4×4 | ★★★★ | 中（要出图） | GPU 低，内存/包体中 | 每张 2048² ASTC 4×4 约 4MB 内存；灰度+色带可省 | ★★★★ | 做 4~8 张关键的 |
| **UIParticle**（mob-sakai，MIT 免费，v4.14.1） | ★★★★ | 低（装包）+ 中（改效果） | 中（WebGL 主线程烘焙网格 + 过度绘制） | 几乎 0 | ★★★ | 推荐用于火星、拖尾、骰子光球吸附 |
| **UIEffect v5**（同作者，MIT） | ★★★ | 很低 | 低 | 小 | ★★★★ | 立绘/卡牌溶解、扫光现成可用 |
| **PrimeTween**（免费，零分配） | ★★★（手感） | 很低 | 极低 | 极小 | — | 推荐，写缓动曲线更快 |
| 预烘焙发光图（假 Bloom） | ★★★ | 低 | 低 | 小 | ★★★ | 推荐，代替真 Bloom |
| 低分辨率 RenderTexture + 模糊（只在大招开） | ★★★ | 中 | 中 | 0 | ★★ | 以后再说 |
| 切 URP 做 Bloom | ★★ | 很高 | 高 | 增加 shader | ★★ | **不推荐**：URP 后处理根本不作用于 Overlay 画布 |
| GrabPass 屏幕扭曲 | ★★★ | 低 | 高（手机 WebGL 整屏拷贝） | 0 | ★★ | 不推荐，用"只扭曲特效自己的贴图"代替 |
| Spine（$69/$379 每人） | ★★★★★（大招切入） | 高（新美术流程） | 低~中 | 中 | ★★★★★ | 以后做大招切入时再考虑 |
| Live2D（年收入 <1000 万日元免费） | ★★★★（立绘） | 高 | 中 | 中 | ★★★ | 不急 |
| 视频叠加 | ★★★★ | 低 | 高：Safari 不支持 WebM 透明，HEVC 透明进 WebGL 会丢 | 视频放包外 | ★★ | 只适合不透明的全屏切入 |
| Feel（$50） | ★★★ | 低 | 低 | 包很大要裁 | — | PrimeTween + 自己的小工具够用 |

要点：
- **URP 不是解药**：Unity 官方说 Overlay 画布不受后处理影响是"设计如此"；要用就得改成 Camera 画布 + 全量材质迁移，Bloom 又是 URP 最贵的效果 [T-urp]。
- **WebGL 贴图格式**：Unity 6 WebGL 可选 ETC2/ASTC/DXT；设备不支持就软件解压，吃内存。官方方案是"双构建"（桌面 DXT + 手机 ASTC，index.html 判断加载）[T-webgl]。iOS 上 ASTC 支持率约 99.98% [T-astc]。**特效遮罩和色带要设成非 sRGB（Linear）贴图、关掉 Mipmap。**
- **贴图尺寸**：特效用 2048 图集；4096 只给 1~2 个大招 [T-8k]。
- **少帧技巧**：在 shader 里混合相邻两帧（帧混合），或用运动向量图（再省 2~4 倍帧数）[T-mv]。
- **UIParticle 坑**：只接受 UI/Default 或自己写的 UI shader；不要和 ParticleSystem 挂在同一个物体上；自定义 shader 要加 Stencil 和 `_ClipRect` 才能被遮罩 [T-uip]。
- **包体预算建议**：现在 data ~82MB，离 100MiB 还剩约 18MB。**新特效贴图总共控制在 ≤ 8MB**（灰度 + 色带，一张图 4 个种族共用）。

---

## 4. 前 10 项改动（按优先级）

| # | 改动 | 涉及文件 | 工作量 | 效果 | 包体 |
|---|---|---|---|---|---|
| 1 | **命中节奏重做**：第 0 帧最强、顿帧分档、目标白闪+压扁+抖动、总长缩到 ≤0.45s | `BattleFx1021.cs`、`TacticsBattlePanel.LandHit1021`、`UIFlash.shader` | 小（半天~1 天） | ★★★★★ | 0 |
| 2 | **硬边赛璐璐 shader**：阈值分段 + 种族色带 + 暗色衬底，替换柔光圆点 | 新 `UICelFx.shader` + `BattleFxArt1021` | 中（1~2 天） | ★★★★★ | <0.5MB |
| 3 | **形状改定向**：刀光拖影 + 三角碎片顺攻击方向 + 断开由粗变细的冲击环 + 四角星 | `ImpactBurst1021` | 小~中 | ★★★★ | 0 |
| 4 | **GPT 特效图"活"起来**：噪声溶解/侵蚀消散、出现时 1.3→1.0 盖章、少量 UV 扭曲 | `BasicStrike1021` + 新 shader 功能 | 小~中 | ★★★★ | 一张噪声图 |
| 5 | **真冲击帧**（暴击轻版、大招完整版）：黑白反转 + 目标剪影 + 红星 + 放射线 | 新 `ImpactFrame`（替换 `ScreenFlash1021`） | 中 | ★★★★★ | 0 |
| 6 | **伤害数字 P5 化**：逐字倾斜、红色错位影、暴击红色斜条、合并多段、弹出曲线 | `AnimatePopup` / `SpawnPopupAt` | 小~中 | ★★★★ | 字体可复用 |
| 7 | **音效分层**：挥动 + 命中 + 材质/种族点缀，音高 ±5% 随机，严格同帧 | `GameAudio` + 少量新 wav | 小 | ★★★★ | 约 0.5~1MB |
| 8 | **关键序列帧**：4 个种族命中（灰度 4×4，色带上色）+ 大招 1~2 张 | 新贴图 + 帧动画组件 | 中~大（出图） | ★★★★ | 4~8MB |
| 9 | **UIParticle**：火星、拖尾、骰子光球吸附、大招火焰 | 新包 + `DiceBonusFlight1020`、`UltReadyRing1020` | 中 | ★★★ | ~0 |
| 10 | **大招切入 P5 化**：红黑底 + 立绘滑入 + 拟声字 + 泼墨剪影 + 闪光清屏 | 大招 show 相关 | 中~大 | ★★★★★ | 看素材 |

建议顺序：**1 → 3 → 6 → 7**（纯代码，一两天就能感觉"变高级"）→ **2 → 4 → 5**（着色器）→ **8 → 9 → 10**（出图/装包）。

---

## 5. 逐个特效的规格（60fps，f = 帧，1f ≈ 16.7ms）

> 现在的游戏有"0.5 倍演出速度"设置，下面写的是**实际屏幕时间**；实现时要按演出速度换算。顿帧期间特效时钟也停（现在 `BattleHitStop.Delta` 已经这么做，保留）。

### 5.1 通用命中（普通 / 暴击 / 大招）

| 时间 | 普通 (power 0) | 暴击/技能 (power 1) | 大招 (power 2) |
|---|---|---|---|
| f0 | 目标白闪（UIFlash）2f；命中音；数字出现 | 同左 + 目标红蓝错位残影 6f | **冲击帧 2f**（见 5.5）+ 白闪 1f |
| 顿帧 | **4f**（0.067s） | **8f**（0.13s） | **12f**（0.2s） |
| 目标反应 | 压扁 0.9×1.1 → 回弹 6f；横向抖 3px 递减 | 抖 6px；后退 8px 回位 12f | 抖 10px；后退 14px |
| 白芯四角星 | f0 满大小 → f3 消失 | f0→f4 | f0→f6，大 1.5 倍 |
| 刀光/拖影 | f0~f3 画出，f4~f10 侵蚀消失 | 同左，加粗 | 两道交叉 |
| 碎片 | 5~7 片，f6 时飞完 70% 距离（指数缓出），f16 缩到 0 | 9~11 片 | 14~16 片 |
| 冲击环 | f1~f12，厚度 14% → 1%，有缺口 | 双环，第二环晚 4f | 双环 + 地面横向椭圆环 |
| 残留烟 | f6~f27 硬边侵蚀 | f6~f33 | f8~f40 |
| 屏幕 | 震 3px 6f | 震 7px 12f + 缩放 1.03（4f 进，8f 回） | trauma 0.8，震 11px 18f + 缩放 1.06 |
| 总长 | **≤ 0.45s** | ≤ 0.55s | ≤ 0.7s |

颜色：每层都从种族色带取（白芯 → 亮色 → 主色 → 深色），**只有白芯和刀光用加色**，烟、碎片的暗边用普通混合。

### 5.2 种族普攻

| 种族 | 蓄力 | 飞行 | 命中 | 关键改法 |
|---|---|---|---|---|
| 魅魔/狐火 | 施法者身上 3 个小火苗依次点燃，每个 2f 闪，共 6f | 12~16f，三条弧线依次到达（间隔 4f），拖尾用**连续丝带**不用圆点 | 每个火苗小爆 2f，最后一个大爆 + 爱心碎片 | 颜色要和图统一（粉 or 橙，待定） |
| 恶魔 | 目标身上裂缝发光 6f（预告） | 无飞行 | 三道爪痕间隔 3f，每道 2f 画出 + 8f 侵蚀；溅出绿毒滴 | 黑边已有，保留；加紫→绿的副色 |
| 血精灵 | 血月刃在手边旋转出现 4f | 14f 旋转飞行，3 个残影间隔 2f | X 字斩：两条各 2f 画出，中间隔 3f；最后 1f 白 + 血滴碎片 | 残影透明度 60/35/15% |
| 人鱼 | 水珠聚集 6f | 10f 水柱（扭动） | 水花序列帧 16 帧 + 气泡上升 30f | **水体用普通混合不用加色**（加色看起来像光不像水），只有高光加色 |

### 5.3 敌人攻击
- 敌人统一用**一种敌对色**（现在是 `PanelKit.Pink`）+ 黑色 + 一点"故障"感（已有 glitch.wav），和我方 4 个种族色明显分开 [推断]。
- **蓄力预告**必须有：激光先 12f 汇聚线 + 枪口亮点；爪击先 6f 爪影；声波先 8f 圆环收缩。
- 激光：发射 3f 白芯粗光束 → 粗细脉动 → 10f 收细消失。爪击：3 道拖影间隔 2f。声波：环带红蓝错位。
- **我方被打**：屏幕边缘红色暗角闪 6f；卡牌**横向**抖（樱井：地面角色左右抖）；数字红色、向下飘。

### 5.4 伤害数字
- 弹出：0 → 1.3 → 1.0，约 6~8f（ease-out-back）；停 0.3s；上飘 + 淡出 0.3s。
- 普通：白字黑粗描边 + 种族色错位影，逐字随机歪 -6°~+6°。
- 暴击：放大 1.6 倍，红色倾斜条 + "CRITICAL!"/"暴击"，出现时 2f 白闪，加一个小四角星。
- 多段：同一目标 0.4s 内的伤害**合并成滚动总数**，每次加数都重新弹一下（现在是新数字顶掉旧数字）。
- 治疗：绿色、从小长大、向上慢飘（保持现在思路）。

### 5.5 冲击帧（大招完整版 / 暴击轻版）
- f0~f1：整屏变**白底**，所有角色/敌人变**纯黑剪影**（用立绘 alpha，UIFlash 设成黑色即可），命中点一颗**红色锯齿星** + 黑色放射线。
- f2：反过来——**黑底白剪影**（可选）。
- f3：恢复正常画面 + 白闪 1f，接普通命中层。
- 暴击轻版：只让**目标卡牌**做 2f 黑白反转，不动整屏。
- "减少动态效果"开关打开时：全部关闭，改成 4f 低强度白闪（照顾光敏玩家）。

### 5.6 大招就绪火焰（`UltReadyRing1020`）
- 现在：很多柔光图片粒子。建议：**一个四边形 + 火焰形状遮罩 × 向上滚动噪声 → 硬边阈值 → 狐火色带**，三段分明的二次元火（便宜很多）。
- 再加 2~3 片硬边火星（UIParticle 或现有池）。
- 刚就绪的那一刻：盖章式"READY"/"必杀"字 1.3→1.0（4f）+ 2f 白闪 + 一声短音。
- 呼吸节奏约 70 BPM（略快于心跳，有紧张感 [S7]）。

### 5.7 骰子
- 落地：每颗落地 1 个硬边尘土环（8f）+ 面上 2f 白闪；顿 2f。
- 计分：骰子上方点数"盖章"；组合名（如 顺子）用 P5 倾斜红条 + 白字，1.3→1.0 4f。
- 光球飞向队长（`DiceBonusFlight1020`）：改用 UIParticle 的吸附器 + 丝带拖尾；每颗到达时卡牌 1.12→1.0（6f）+ 叮声音高逐个升高（越连越爽）。

### 5.8 波次过场（`BattleWaveFracture1019`）
- 现在约 1.7 秒（0.5 倍速）。建议总长 ~1.1s：
  - f0~6 黑色楔形斜着砸进来（最后 1f 冲过头再弹回）
  - f6~10 红色条带
  - f10 "第二波"盖章 + 2f 白闪 + 震 4px
  - 停 24f
  - f40~52 卡片撕开成碎片飞走
  - 新敌人依次滑入：每个 18f（ease-out-back），间隔 4f
- 暗部加网点纹理。

### 5.9 结算画面（`BattleStickerResultView`）
- f0~10：红色斜切整屏擦入
- f6~18：MVP 立绘从侧面滑入（ease-out-expo），1f 换成定格姿势
- "VICTORY/胜利" 逐字盖章，每字间隔 3f，各自歪 ±8°
- 数字滚动 30f，配"嗒嗒"音；奖励依次掉落，间隔 5f，带弹跳
- 最后一闪清屏，按钮出现

---

## 6. 怎么出特效图（研究问题 4）

### 6.1 推荐流程
1. **形状类**（环、刀光遮罩、噪声、色带、火星）：**用 numpy 代码生成**。免费、绝对一致、可平铺。也可以用 CC0 的 Kenney 粒子包、Unity 免费 Particle Pack。
2. **主角单张**（一道刀光、一颗命中星）：**本地 GPT 生图**（免费），用下面模板 A/B；再用 PIL 做"去黑底"或硬边阈值。动画交给 Unity（缩放、侵蚀、极坐标）。
3. **有机序列帧**（爆炸、水花、烟）：**AI 视频 → 抽帧**。在纯黑背景上生成 5 秒视频 → Swift 抽帧 → 选 16 帧（起势/高潮/消散）→ numpy 去黑底、分 3 段色 → 拼 4×4、2048² 图集。
4. GPT 直接画 4×4 网格只适合简单对称的效果，**前后帧容易不一致**，要多试几次 [A1]。

### 6.2 去黑底公式（加色材质可以不做）
```python
a = rgb.max(-1)
a = np.clip((a - 0.04) / (1 - 0.04), 0, 1)   # 0.04 去掉视频压缩噪点
rgb_out = np.where(a[..., None] > 0, rgb / np.maximum(a[..., None], 1e-4), 0).clip(0, 1)
```

### 6.3 提示词模板
- **A 单张加色贴图（黑底）**："Single game VFX texture, [cyan-white crescent sword slash, thick at center, tapering to sharp points], 2D anime cel-shaded effect, hard-edged shapes with 3 flat color bands (white core, [cyan] mid, [deep blue] rim), centered, fills 80% of frame, isolated on PURE BLACK #000000 background, no character, no text, no vignette, square."
- **B 透明贴图（烟、暗色特效）**："…same…, transparent background, clean crisp alpha edges, no glow halo, flat cel-shaded anime smoke puff, 2 tones [lavender / dark purple]."
- **C 4×4 序列帧**（配合画好格子的遮罩图去编辑）："4×4 sprite sheet, 16 equal square cells, one frame per cell left-to-right top-to-bottom: frames 1–3 small spark onset, 4–7 expanding [orange-white starburst], 8–10 peak with 6 sharp spikes, 11–16 thinning into shards, last cell empty. Same center, same scale, same palette, anime cel-shaded, pure black background, no text."
- **D 视频（可灵/万相/Seedance）**："Locked-off static camera, pure black background, a single [anime-style blue water splash] erupts from the center, peaks, then dissipates completely, cel-shaded 2D anime effects animation, crisp shapes, no camera movement, ends on a fully black frame." 1:1，5 秒。
- **每个元素固定一套色号**，所有提示词和色带共用（例：火 白/#FFD24A/#FF6A1A/#8A1A0A；水 白/#8FE8FF/#2A9DF4/#0B3A8C）。先出一张"定稿帧"，后面都拿它当参考图 [A1]。

### 6.4 价格（只列价，没买）
| 项目 | 价格 | 来源日期 |
|---|---|---|
| 本地 GPT 生图 | 免费（已有） | — |
| Google 官方 Nano Banana 2.1 | 1K $0.0336 / 2K $0.0504 / 4K $0.113；批量 1K $0.0168 | 2026-10-21 读 [A2] |
| Google 官方 Nano Banana 2 | 1K $0.067 / 2K $0.101 / 4K $0.151 | 同上 |
| Google 官方 Nano Banana Pro | 1K/2K $0.134 / 4K $0.24 | 同上 |
| RunningHub nano-banana-2（.ai） | 标价 $0.067，折后 $0.027 | 2026-10-21 读 [A3] |
| RunningHub nano-banana-2-Lite（大概是 2.1） | 标价 $0.0336，折后 $0.01 | 同上 |
| RunningHub Nano Banana 2 第三方通道（.cn） | 1K ¥0.15（每天 5 张免费）/ 2K ¥0.2 / 4K ¥0.3 | 2026-01 博客 [A4]，可能已变 |
| RunningHub seedream-v5-pro | ¥0.3/张 | 2026-10-21 读 [A5] |
| RunningHub 可灵 3.0 | ¥0.6/秒（5 秒约 ¥3） | 同上 |
| RunningHub 万相 3.0 | ¥0.3/秒（5 秒约 ¥1.5） | 同上 |
| Seedance 2.5 | 480P ¥0.21/秒（促销）；720P ¥0.49/秒 | 2026-08 [A6] |
| RunningHub 会员 | 传闻 ¥33/¥79/¥114 每 31 天，来源互相矛盾 | 未核实 |
| EmberGen（专业流体特效软件） | 独立版约 $299.99 买断 | 第三方报价 [A7] |

**估算**：如果用视频抽帧做 8~12 个序列帧（含重试约 15~20 条视频），可灵约 ¥45~60，万相约 ¥25~30。**需要少爷批准才花钱。**

---

## 7. 需要少爷决定的事

1. **整体风格**：A 硬边二次元赛璐璐 + P5 冲击帧（推荐）／B 保留柔光只修节奏／C 混合。
2. **冲击帧强度**：只大招整屏（推荐）／暴击也整屏／都不要。
3. **狐火颜色**：跟图走**粉色爱心火**（推荐，和魅魔立绘一致）／改图成橙色。
4. **装免费插件**：UIParticle（MIT）+ PrimeTween（免费）——可以／不要。
5. **花钱出序列帧**：用 RunningHub 视频（约 ¥30~60）／只用本地 GPT + 代码（免费，慢一点）。
6. **包体预算**：新特效贴图上限 8MB（推荐）／更多。
7. **大招切入**：先用现有立绘做 P5 切入（推荐）／以后买 Spine 做骨骼动画。

---

## 8. 参考链接

### 美术原则（S）
- [S3] https://www.surrenderat20.net/2015/06/red-post-collection-dev-blog-on-vfx.html
- [S4] https://www.leagueoflegends.com/en-us/news/dev/clarity-in-league/
- [S5] https://realtimevfx.com/t/anatomy-of-a-league-of-legends-missile-part-1-creating-areas-of-focus/11614
- [S6] https://www.vfxapprentice.com/blog/10-league-of-legends-vfx-design-tips
- [S7] https://www.vfxapprentice.com/blog/the-soul-of-effects-what-is-timing-in-vfx
- [S8] https://realtimevfx.com/t/stylised-explosive-and-fiery-hit-spark-looking-for-feedback/11286
- [S8b] https://realtimevfx.com/t/lessons-learned-i-remade-one-of-my-vfx-after-2-years/22808
- [S8c] https://openskillindex.com/skills/vibeforge1111-vibeship-spawner-skills-vfx-realtime （权威性较低）
- [S9] https://80.lv/articles/world-of-warcraft-vfx-overview-from-luis-aguas/
- [S10] https://www.vfxapprentice.com/blog/five-artistic-principles-gaming-vfx
- [S11] https://infovore.org/?p=5275 （Vlambeer "Art of Screenshake" 笔记）
- [S12] https://www.gcores.com/articles/167815
- [S13] https://www.gamedeveloper.com/audio/improving-the-combat-impact-of-action-games
- [S14] https://hackmd.io/@xiaorin/cgipipeline
- [S15] https://80.lv/articles/vfx-staples-shape-color-and-motion/
- [S16] https://srk.shib.live/w/The_King_of_Fighters_XIII/Character_States/Hitstun_Blockstun_state
- [S17] https://ken.ieice.org/ken/paper/20230315OCSt/eng/
- [S18] https://sourcegaming.info/2015/11/11/thoughts-on-hitstop-sakurais-famitsu-column-vol-490-1/
- [S19] https://www.bilibili.com/opus/935443031017914368
- [S20] https://www.gcores.com/articles/165341
- [S21] https://kidscancode.org/godot_recipes/3.x/2d/screen_shake/
- [S22] https://bevy.org/examples/camera/2d-screen-shake/
- [S23] https://www.gcores.com/articles/208724
- [S24] https://pantherdynamics.yolasite.com/panther-dynamics-blog/page/2 （权威性较低）
- [S25] https://www.3djuegos.com/juegos/zenless-zone-zero/noticias/one-piece-spider-man-han-convertido-tendencia-street-fighter-parte-su-adn-ahora-zenless-zone-zero-quiere-llevarlo-paso-alla
- [S26] https://docs.unity3d.com/Packages/com.unity.postprocessing@3.4/manual/Chromatic-Aberration.html
- [S27] https://bugnet.io/blog/how-to-build-a-damage-number-system
- [S30] https://www.famitsu.com/news/201711/13145540.html （ATLUS CEDEC+KYUSHU 2017）
- [S31] https://mechanicsofmagic.com/2022/04/23/visual-design-of-games-persona-5/
- [S32] https://kotaku.com/-metaphor-refantazio-persona-5-menu-ui-hashino-1851667088
- [S33] https://amara.org/videos/wIZbgZ3Jyy5D/en/3103539 （New Frame Plus：P5 总攻击）
- [S34] https://www.gcores.com/articles/196442 （ATLUS GDC 2025 暗喻幻想）
- GDC 2017 Applying artistic principles to VFX design：https://gamedeveloper.com/art/video-applying-artistic-principles-to-vfx-design-for-games

### 技术（T）
- [T-uip] UIParticle：https://github.com/mob-sakai/ParticleEffectForUGUI ，版本：https://github.com/mob-sakai/ParticleEffectForUGUI/releases
- UIEffect：https://github.com/mob-sakai/UIEffect ；SoftMaskForUGUI：https://github.com/mob-sakai/SoftMaskForUGUI
- [T-webgl] Unity 6 WebGL 贴图压缩：https://docs.unity3d.com/6000.0/Documentation/Manual/webgl-texture-compression.html ；WebGL 图形：https://docs.unity3d.com/6000.0/Documentation/Manual/webgl-graphics.html
- [T-astc] https://web3dsurvey.com/webgl2/extensions/WEBGL_compressed_texture_astc
- [T-8k] https://discourse.threejs.org/t/8k-basis-texture-not-loading-on-mobile-4k-works-fine/26425
- [T-mv] 运动向量序列帧：https://realtimevfx.com/t/ue4s-flip-book-motion-vector-material-function-works-well-image-fixed/2807 ，https://godotshaders.com/shader/particle-flipbook-flowmap-smoothing/
- [T-urp] Overlay 不受后处理：https://issuetracker.unity3d.com/issues/ui-is-not-affected-by-post-processing-stack-effects ；URP Bloom：https://docs.unity3d.com/6000.0/Documentation/Manual/urp/post-processing-bloom.html
- GrabPass 开销：https://docs.unity3d.com/6000.0/Documentation/Manual/SL-GrabPass.html
- 透明叠加填充率（B 站）：https://www.bilibili.com/opus/1087438625068548098 ；PowerVR 粒子建议：https://docs.imgtec.com/performance-guides/graphics-recommendations/html/topics/optimal-particle-rendering-on-powervr.html
- WebGL 视频：https://docs.unity3d.com/6000.0/Documentation/Manual/webgl-video.html ；Safari HEVC 透明问题：https://bugs.webkit.org/show_bug.cgi?id=273006
- Spine 价格：https://esotericsoftware.com/spine-purchase ；Live2D 许可：https://www.live2d.com/en/sdk/license/
- PrimeTween：https://github.com/KyryloKuzyk/PrimeTween ；LitMotion：https://github.com/annulusgames/LitMotion ；Feel：https://assetstore.unity.com/packages/tools/particles-effects/feel-183370

### 出图（A）
- [A1] https://gamelabstudio.co/blog/why-ai-cant-make-game-ready-spritesheets ；网格遮罩做法：https://www.fab.com/listings/695f3e5d-3aae-4ed8-9720-a4f65f9fe4ac ；4×4 分段指南：https://www.framesprite.com/guides/game-vfx-sprite-sheet-generator
- Unity 官方 AI 序列帧（视频→16 帧）：https://docs.unity3d.com/Packages/com.unity.ai.assistant@2.1/manual/Sprite/spritesheet-overview.html
- FramePacker（视频清理成序列帧）：https://sspai.com/post/114559
- 去黑底（unmult）：https://www.provideocoalition.com/unmult-with-and-without-a-plug-in/
- 硬边侵蚀：https://vfxdoc.readthedocs.io/en/latest/shaders/alpha-erosion/
- [A2] https://ai.google.dev/gemini-api/docs/pricing
- [A3] https://www.runninghub.ai/call-api
- [A4] https://www.cnblogs.com/flyingsir/p/19434459
- [A5] https://www.runninghub.cn/call-api
- [A6] https://www.csdn.net/article/2026-08-09/163607608
- [A7] https://www.cgchannel.com/2024/01/jangafx-releases-embergen-1-1/
- 可灵官方价格：https://www.klingai.com/document-api/pricing/base/video
- Kenney 粒子包（CC0）：https://kenney.nl/assets/particle-pack ；Unity Particle Pack：https://assetstore.unity.com/packages/vfx/particles/particle-pack-127325 ；免费特效贴图：https://80.lv/articles/download-over-35-completely-free-vfx-textures
