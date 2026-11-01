# 2026-10-23 · 技能音效（SFX）调研与制作方案

> 只做调研：本次没有花钱、没有调用任何收费接口、没有读取任何 key。
> 要花钱的步骤都写成“待批准”，执行时沿用 `AUDIO-SAMPLES-2026-10-19.md` 的规矩：
> **先精确报价 → 查 pending → 一次只提交一个声音 → 保存 taskId → 只查已知 ID → 失败不自动重试 → 记收据。**

---

## 0. 一句话结论

- 现在“很难听”的根本原因：WebAudio 用噪声+振荡器现场合成，没有真实质感、没有分层。
- 推荐做法：**“分层拼装”**——每个技能音效 = 3~5 层短素材叠在一起（低频冲击 + 主体 + 瞬态咔哒 + 闪光点缀 + 尾音）。
- 素材来源三条腿：
  1. **免费 CC0 / 免版税库**（Kenney、Sonniss GDC、lentikula）→ 低频、瞬态、尾音这些“通用层”，零成本。
  2. **Suno v6 Sounds（RunningHub 国际 USD 钱包）**→ 一次出 2 个候选，适合短促 one-shot：嗖声、蓄力、重击、碎裂。
  3. **豆包 Seed-Audio 1.0（国内 CNY 钱包，很便宜）**→ 种族特色的“主体层”（粉心火、紫爪、水、血月），**一次只要一个声音**。
- 5090 本地免费备选：**Stable Audio 3 Small SFX**（可商用，年收入 < 100 万美元免费）。
- 视频配音模型（MMAudio / ThinkSound / AudioGen）**不能商用**，HunyuanVideo-Foley 许可**排除欧盟/英国/韩国**，只做参考。
- 预估总花费：**最多约 1.2 美元 + 0.6 元人民币**，先做“魅族一套”试点（约 0.25 美元 + 0.1 元）。

---

## 1. Suno v6 “Sounds”（RunningHub：`rhart-audio/suno-v6/sounds`）

**是什么**：Suno 的实验性功能，用文字生成音效、乐器采样、环境声。每次生成 **2 个候选**。
有两种类型：
- **One Shot**：一次性单个声音 → 官方说“纯音效一般选它”。我们的击打、嗖声、蓄力都用这个。
- **Loop**：无缝循环 → 官方说适合音乐元素/背景铺底。注意 Loop 可能带完整编曲（会变成音乐），燃烧循环要写清楚“no music”。
- 可选 BPM / 调性（我们一般不用，除非要和战斗 BGM 144BPM 对齐，比如大招切入的“咚”）。
- 官方提示词技巧：短、具体、用行业词（whoosh、glitch、impact、ambience），太长/太短可以直接写“2 second”。

**质量口碑**（第三方实测）：短促的离散冲击（关门、脚步、撞击）效果很好；**长而持续的声音（风、火持续燃烧）偏弱**，需要多次改提示词；部分结果“偏薄或有杂音”。→ 结论：**Suno 做短 one-shot，燃烧循环不要指望它**。

**价格**：Suno 官方 App 内 Sounds = 每次 2 credits（Pro/Premier）。**RunningHub 上的价格没有公开页面**，必须用 price-preview 精确报价。参考：上次 Suno v6 做歌（2 个候选）实收 0.08 USD，音效大概率不会更贵，下面预算按 **≤0.08 USD/次** 保守估。

**商用**：Suno 规定付费订阅期间生成的内容有商用权（但 2025 年后改口“一般不算你拥有版权”）；免费账号生成的只能非商用。**通过 RunningHub API 生成时，权利归属要看 RunningHub 自己的条款——未确认**，上线前请少爷看一眼 RunningHub 用户协议。

来源：
- Suno 官方帮助 https://help.suno.com/en/articles/10625537
- Suno v6 Create 指南 https://jackrighteous.com/blogs/guides-using-suno-ai-music-creation/inside-suno-v6-create-simple-advanced-sounds-guide
- 实测评论摘要 https://lilys.ai/en/notes/suno-ai-20260130/suno-insane-sounds-update
- 权利条款变化 https://www.musicinafrica.net/magazine/suno-adjusts-ai-music-ownership-terms-after-warner-music-partnership

## 2. 豆包 Seed-Audio 1.0（RunningHub：`bytedance/doubao-seed-audio-1.0`）

**是什么**：字节 2026-06 发布的“统一音频模型”，一个提示词里能同时出对白、音乐、环境声、音效（偏“有声剧成品”）。
- 第三方资料：单次最长约 120 秒、文本最多约 1.5k 字；RunningHub 上只给 **5 秒或 10 秒**两档。
- 风险：它天生喜欢“讲故事”，**容易自己加人声、配乐或长静音**。上次 3 个音效塞一次请求，结果只出 2 组、中间 3.76 秒静音。
- 对策：**一次只描述一个声音**，写明“单个音效、无人声、无音乐、声音从第 0 秒开始”，选 5 秒档，拿回来自己裁剪。

**价格**：上次 5.9 秒实收 **0.024 元**，非常便宜，适合多抽几次。（第三方平台价格差异大：fal.ai 0.1875 美元/分钟，Picsart 约 0.025 美元/分钟，仅参考。）

**商用**：**没找到公开的商用条款**。需要以火山方舟服务协议 / RunningHub 条款为准——未确认。

**提示词写法**（中文或英文都行，建议中英各一句）：
```
单个游戏技能音效，粉色爱心火焰爆开：明亮的火焰呼啦一声+玻璃般清脆闪光，0.8秒内结束，干声无混响，无人声，无音乐，从第0秒开始。
Single game spell SFX: bright pink heart-shaped fireball bursting, airy flame whoomp with glassy sparkle, under 1 second, dry, no voice, no music.
```
来源：
- https://ai-bot.cn/doubao-seed-audio-1-0/
- https://mer.vin/2026/07/seed-audio-1-0-byteplus-single-prompt-audio-scene-generation-api/
- https://fal.ai/models/bytedance/seed-audio-1.0/llms.txt
- https://evolink.ai/blog/seed-audio-1-0-pricing-cost-planning-evolink

## 3. 其他生成器对比

| 工具 | 音效质量 | 价格 | 能否商用 | 5090 本地 |
|---|---|---|---|---|
| **ElevenLabs Sound Effects** | 业界公认最好的文字转音效之一；0.1–30 秒；有 Loop；“prompt influence”控制听话程度 | API 约 0.12 美元/分钟（指定时长时 40 credits/秒）；Starter 月费约 5–6 美元 | **付费方案可商用、免署名**；免费方案要署名 elevenlabs.io | 否（只有云） |
| **Stable Audio 3 Small SFX**（2026-05） | 专门做音效/短环境声，0.5B 参数，社区实测 8GB 显卡 1.7 秒出 12 秒 | 免费 | Stability 社区许可：**年收入 < 100 万美元可商用，输出归你** | **能，轻松** |
| Stable Audio Open 1.0 | 一般，偏素材感，最长 47 秒，44.1k 立体声 | 免费 | 同上社区许可 | 能 |
| HunyuanVideo-Foley | 看视频自动配音效，48kHz，质量好 | 免费 | 许可**不适用欧盟/英国/韩国**；月活>1亿要另谈 | 能（XXL 20GB 显存） |
| MMAudio | 看视频配音，8 秒，约 6GB 显存 | 免费 | 权重 **CC-BY-NC，不能商用** | 能 |
| ThinkSound | 视频/文字转音效 | 免费 | **仅研究/教育，禁止商用** | 能 |
| AudioGen（Meta） | 16kHz，音质低 | 免费 | **CC-BY-NC，不能商用** | 能 |

**建议**：
- ElevenLabs 质量最稳，但要少爷自己注册付费账号（我不能替你开账号），属于“备胎”：如果 Suno + Seed 试点不满意再考虑，一个月 5–6 美元足够做完全部音效。
- 5090 上装 **Stable Audio 3 Small SFX** 作为“免费无限抽卡”，专门出变体（同一个声音多出几版，避免听腻）。
- HunyuanVideo-Foley 可以把我们已有的 VFX 视频喂进去，**听一下“节奏点在哪”当参考**，但成品不进游戏包（地区许可风险）。

来源：
- ElevenLabs API 价格 https://elevenlabs.io/pricing/api
- ElevenLabs 音效文档 https://elevenlabs.io/docs/overview/capabilities/sound-effects
- ElevenLabs 商用 FAQ https://elevenlabs.io/sound-effects/commercial
- Stable Audio 3 发布 https://stability.ai/news-updates/meet-stable-audio-3-the-model-family-built-for-artistic-experimentation-with-open-weight-models
- Stable Audio 3 Small SFX 社区移植/实测 https://huggingface.co/thepatch/stable-audio-3-small-sfx-GGUF
- Stability 社区许可 https://stability.ai/license
- Stable Audio Open 1.0 https://huggingface.co/stabilityai/stable-audio-open-1.0
- HunyuanVideo-Foley https://github.com/Tencent-Hunyuan/HunyuanVideo-Foley （许可：仓库 LICENSE 第 1(l)、4、5(c) 条）
- MMAudio https://github.com/hkchengrex/MMAudio
- ThinkSound https://github.com/FunAudioLLM/ThinkSound
- AudioGen https://huggingface.co/facebook/audiogen-medium

## 4. 免费可商用素材库

| 素材库 | 许可 | 大小 | 适合做哪层 |
|---|---|---|---|
| **Kenney 音频包**（已有 4 包，另有 RPG Audio 50 个、Digital Audio、UI Audio） | **CC0**，商用免署名（不能用 Kenney 的 logo） | 每包几 MB | UI、瞬态咔哒、轻击 |
| **Sonniss GDC 游戏音效包** | 免版税、可商用、**免署名**、终身无限项目；**禁止拿去训练 AI；不能单独转卖音效文件** | 2015–2024 合计约 207GB；**GDC 2026 包 7.47GB+、347+ 文件** | 专业录音级：低频轰、爆炸、魔法、碎玻璃、尾音 |
| **lentikula Basic Spell Impacts**（itch.io） | **CC0** | 20 个，13MB，48k/24bit WAV（雷/火/冰/水各 5） | 火、水技能的主体参考层 |
| OpenGameArt 个别 CC0 火焰音效 | 逐个确认，部分是 CC-BY/GPL **不要混用** | 小 | 备用 |

- Sonniss 下载：去 https://gdc.sonniss.com/ 选“Primary download”5 个 ZIP（或官方种子），不用注册。**下载前要少爷点头**（文件大）。建议只下 GDC 2026 一包先挑。
- 每个用到的包都要把原 License 文件存进 `Docs/AUDIO-SAMPLES-EVIDENCE-*/licenses/`，和 10-19 的 Kenney 做法一样。

来源：
- Kenney 许可 https://kenney.nl/support ，音频包列表 https://kenney.nl/assets/category:Audio
- Sonniss 历年 https://sonniss.com/gameaudiogdc ，2026 包 https://gdc.sonniss.com/
- lentikula https://lentikula.itch.io/freecc0-basic-spell-impacts-sfx
- OpenGameArt 搜索结果 https://opengameart.org/content/cc0-sound-effects

## 5. 专业二次元技能音效是怎么做的（小白版）

### 5.1 五层结构（所有层在“命中帧”对齐）
| 层 | 作用 | 频段 | 时长 | 例子 |
|---|---|---|---|---|
| ① 低频 Sub/Boom | “胸口一震”的重量感 | 30–120Hz | 0.3–1.5s | 低音鼓、电影 boom |
| ② 主体 Body | 决定“这是火/水/爪” | 200Hz–3kHz | 0.3–1s | 火焰呼啦、水花、撕裂 |
| ③ 瞬态 Transient | 让耳朵“抓到命中点”的咔哒 | 2–8kHz | 0.02–0.1s | 拍手、军鼓头、玻璃 tick |
| ④ 点缀 Sweetener | 二次元味道：闪光、铃音 | 5–12kHz | 0.3–0.8s | 风铃、闪光 shimmer |
| ⑤ 尾音 Tail | 空间感，大招尾巴长 | 全频 | 0.5–2s | 混响、余震 |

拼装口诀：**先放主体当锚 → 低频垫在下面 → 瞬态和点缀小声放上面**；非低频层把 100Hz 以下切掉，避免糊；最后整体轻压缩 + 限幅。

### 5.2 时间线
- **蓄力 Riser**（0.6–1.0s，音高/音量往上爬）→ **命中前留 50–100ms 安静**（反差让冲击更响）→ **冲击** → **尾音**。
- 飞行物用 **Whoosh**（0.3–0.5s，和弹道同长）。
- **压低其他声音（Ducking）**：大招切入时 BGM 降 6dB 左右，0.6 秒后慢慢回来；角色语音响时技能音效降 3dB。

### 5.3 防听腻
- 每个高频事件（普攻、暴击）准备 **3–5 个变体**，随机选、不连续选同一个。
- 每次播放随机 **音高 0.95–1.05**、**音量 0.85–1.0**。只靠变调会有“花栗鼠感”，所以变体比变调重要。

### 5.4 响度和格式（手机）
- 整体混音目标：**-18 LUFS ±2**（Sony 手机推荐值），峰值 **≤ -1 dBTP**。单个音效文件峰值归一到约 -3 dBFS，留余量叠层。
- **Unity 导入设置**：
  - 短音效（<2 秒）：**Force To Mono + ADPCM + Decompress On Load**，44.1kHz（点缀层高频多，别降采样）。
  - 燃烧循环/大招切入（>2 秒）：**Vorbis（质量 70 左右）+ Compressed In Memory**；切入可保留立体声。
  - 只有 BGM 用 Streaming。
- 手机扬声器基本是单声道，**绝大多数音效做单声道**就够。

来源：
- 分层 https://morphic.com/resources/how-to/how-to-layer-sound-effects ，https://violetrecording.com/how-to-make-impact-sounds/ ，https://www.liortega.com/post/first-person-shooter-gun-sound-design
- 响度 https://audiokinetic.com/ko/blog/mastering-a-game-with-wwise-part1 ，https://designingsound.org/2012/07/30/video-games-and-loudness-standards-interview-with-sonys-garry-taylor/
- 变体 https://bugnet.io/blog/how-to-fix-unity-sfx-sounds-identical-every-play-no-variation
- Unity https://docs.unity3d.com/Manual/class-AudioClip.html ，https://docs.unity3d.com/Manual/AudioFiles-compression.html

---

## 6. 我们的具体方案

### 6.1 6 个技能音效 = “通用骨架 + 种族皮肤”
骨架（所有种族共用，Suno 做）+ 种族层（Seed-Audio 做主体/循环/点缀）+ 免费层（Sonniss/Kenney 做低频/瞬态/尾音）。

| # | 声音 | 时长 | 骨架层（谁做） | 种族层（谁做） |
|---|---|---|---|---|
| 1 | 蓄力 Riser | 0.8s | Suno：通用魔力上扬 | Seed：种族质感叠一层（可选） |
| 2 | 飞行 Whoosh | 0.4s | Suno：魔法弹嗖声 | — |
| 3 | 主魔法冲击 | 0.8s | Sonniss 低频 + Kenney 瞬态 | **Seed：种族主体** |
| 4 | 燃烧/持续循环 | 2–3s 循环 | — | **Seed：种族循环**（5 秒档，自己裁无缝） |
| 5 | 闪光铃音 | 0.6s | Suno：通用 shimmer | Seed：种族音色（可选） |
| 6 | 最终重击 | 1.5s | **Suno：电影级重击** + Sonniss 尾音 | Seed：种族主体复用 #3 加长 |

### 6.2 提示词（英文，短，游戏 SFX 风格）

**Suno v6 Sounds（One Shot，每次自带 2 候选）**
1. Riser：`anime magic charge-up riser, rising shimmering synth swell, 1 second, ends abruptly, no music`
2. Whoosh：`fast magic projectile whoosh, airy swish passing by, 0.5 second, no music`
3. Heavy hit：`massive anime finisher impact, deep sub boom with sharp crack, short metallic ring tail, 1.5 seconds`
4. Normal slash：`quick sword slash swish with light cloth whip, crisp, 0.3 second`
5. Crit hit：`critical hit impact, punchy thump plus bright glass crack and sparkle, 0.6 second`
6. Ult cut-in：`dramatic anime ultimate cut-in sting, reverse cymbal swell into big taiko hit and glitter burst, 2 seconds`
7. Break：`enemy armor shatter, crystal glass breaking into many shards, low boom underneath, 1 second`
8. Sparkle：`magical sparkle chime, tiny bells and glitter twinkle, bright, 0.6 second`

**Seed-Audio 1.0（5 秒档，一次一个，统一加后缀 `single sound effect only, starts at 0 s, dry, no voice, no music, silence after`）**

| 种族 | 主体冲击 #3 | 循环 #4 |
|---|---|---|
| 魅族 粉心火 | `bright pink heart fireball bursting, airy flame whoomp with candy-glass sparkle` | `soft magical pink flame crackling steadily, warm and airy, continuous` |
| 魔族 紫爪虚空 | `dark demon claw rip, three fast tearing swipes, purple void suction thump` | `low void energy hum with faint whispering wind, continuous drone` |
| 海灵族 水 | `cyan water spell splash, big wave crash with bubbly pop` | `gentle magical water swirl, bubbling stream, continuous` |
| 血精灵 血月 | `crimson crescent moon slash, sharp metallic swing then thick liquid splatter` | `ominous blood magic pulse, slow heartbeat thump with dark shimmer, continuous` |

每次请求后另附中文同义一句（Seed 中文理解更好）。种族点缀/蓄力（可选）用同样格式，第二期再做。

### 6.3 候选数量
- Suno：8 个声音 × 1 次请求（=2 候选）= **8 次**；不满意的最多再抽 1 次 → 上限 16 次。
- Seed：4 种族 × 2 声音 × 2 次 = **16 次**（每次 1 个声音）。
- 5090 Stable Audio 3 Small SFX：给普攻/暴击各补到 4–5 个变体（免费）。
- 免费库：Sonniss GDC 2026 挑低频 boom ×3、尾音 ×3、玻璃碎 ×2；Kenney 现有瞬态复用。

### 6.4 预算（需少爷批准后才执行）
| 项 | 次数 | 单价 | 小计 |
|---|---|---|---|
| Suno v6 Sounds | 8–16 | ≤0.08 USD（**待 price-preview 精确报价**） | **0.64–1.28 USD** |
| Seed-Audio 5s | 16 | ≈0.02–0.024 元 | **≈0.4 元**（上限算 0.6 元） |
| Sonniss / Kenney / lentikula / 本地 SA3 | — | 0 | 0 |
| **合计上限** | | | **≈1.3 USD + 0.6 CNY**（两个钱包分别记账，不换算相加） |

**建议分两步**：
- **第 1 步试点（魅族 + 通用骨架 3 个）**：Suno riser/whoosh/heavy hit 3 次 + Seed 魅族主体/循环 4 次 ≈ **0.24 USD + 0.1 元**。少爷试听满意再做第 2 步。
- 第 2 步：其余骨架 + 3 个种族。

### 6.5 拿到素材后的处理（全部本地免费）
1. 用 ffmpeg 裁掉开头静音、加 5ms 淡入 / 20–50ms 淡出，防爆音。
2. 单个文件峰值归一到 -3 dBFS，转 44.1k 单声道 WAV。
3. 循环：找过零点裁成 2–3 秒，首尾做 50–75ms 等功率交叉淡化（和 BGM 循环同法）。
4. 每个文件记 SHA、来源、许可、taskId 到新的 manifest.json。

### 6.6 WebAudio 预览页怎么叠
```js
// 一个技能 = 若干层，每层：buffer、延迟、音量、音高随机范围
const SKILL_CHARM = [
  { buf:'riser',      at:0.00, gain:0.7 },
  { buf:'whoosh',     at:0.80, gain:0.6 },
  { buf:'sub_boom',   at:1.20, gain:0.9 },   // 命中帧 1.20s
  { buf:'charm_body', at:1.20, gain:1.0 },
  { buf:'click',      at:1.20, gain:0.5 },
  { buf:'sparkle',    at:1.25, gain:0.4 },
  { buf:'charm_loop', at:1.30, gain:0.5, loop:true, stopAt:3.0 },
];
function playLayer(L, t0){
  const s = ctx.createBufferSource(); s.buffer = buffers[L.buf];
  s.playbackRate.value = 0.95 + Math.random()*0.10;      // 防听腻
  const g = ctx.createGain(); g.gain.value = L.gain*(0.85+Math.random()*0.15);
  s.connect(g).connect(sfxBus); s.loop = !!L.loop;
  s.start(t0 + L.at); if (L.stopAt) s.stop(t0 + L.stopAt);
}
// sfxBus -> DynamicsCompressor(阈值-12dB) -> master；大招时 bgmGain 0.6秒内降到 0.5 再回 1
```
- 命中帧时间从 VFX 时间线读（和 7 层 VFX 时间线同一份配置），音画就不会错位。
- 先替换现在的噪声合成，只保留合成器做“临时占位”开关。

### 6.7 Unity 里怎么接
- 做一个 ScriptableObject `SfxEvent`：`List<Layer>`，每层 = 片段池(3–5 个) + 延迟 + 音量 + 音高范围。播放时每层随机挑一个、不连续重复。
- AudioMixer 分组：`SFX`（技能/击打）、`Voice`、`BGM`；BGM 组加 **Duck Volume**（侧链接 Voice 和 Ult），或大招时切 Snapshot。
- 同一帧最多叠 ~8 个声音；普攻 50ms 内重复触发合并成一次，防爆音。
- 导入规则见 5.4；沿用已有 `.wav.meta` GUID 规则，不重建整个资源目录。
- 网页版和 iOS 模拟器同步更新（按惯例）。

### 6.8 验收层级（和 10-19 文档一致）
文件有效 ≠ 少爷试听认可 ≠ Unity 战斗里音画对齐 ≠ 真机手机扬声器听感。每层分别记录，不能互相冒充。

---

## 7. 待少爷决定
1. 批准第 1 步试点预算（≈0.24 USD + 0.1 元）？
2. 是否允许下载 Sonniss GDC 2026 包（7.47GB+，来自 gdc.sonniss.com 官方）？
3. 要不要在 5090 上装 Stable Audio 3 Small SFX 做免费变体？
4. RunningHub 生成内容的商用条款请少爷确认一次（Suno / Seed 两个都未找到公开条款）。
5. 如果试点仍不满意 → 考虑开 ElevenLabs 付费账号（需少爷本人注册）。
