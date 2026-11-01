# AI 做 2D 战斗特效（VFX）工具调研 · 2026-10-21

> 目标：给 CHO-SIREN（Unity 6 / uGUI / Built-in / P5 风）做打击火花、斩击、火、水花、魔法爆发、大招特效的序列帧（sprite sheet / flipbook）。
> 我们已有：GPT 生图（透明底、4x4 黑底序列）、RunningHub 视频模型（Kling / Wan / Seedance / Nano Banana）抽帧、Wan-Alpha。
> 本文只找"别的办法"。价格都是网页上写的，可能会变；标【未核实】的是推测或来源互相矛盾。

## 一句话结论

1. **专门的 AI 特效序列帧工具，目前最像样的是 Ludo.ai 的 VFX Sprite Generator**：文字或关键帧 → 透明底序列帧，有 API / MCP / Unity 插件。
2. **最省钱的办法是"黑底 + 叠加混合（Additive）"**：我们现有的 GPT / Kling / Seedance 黑底素材**根本不用抠图**，用 Additive 材质直接叠上去，黑色自动消失。发光特效用"亮度当透明度"脚本也能免费转 PNG。
3. **抠发光/烟雾的新开源模型**：LTX-2.5 Alpha Gen（2026 新出，专门抠烟、火、水这类半透明东西）、MatAnyone 2。比普通抠图（BiRefNet、LightAI）更适合特效。
4. 像素风工具（PixelLab、Sorceress、Retro Diffusion）和电影特效工具（Higgsfield、Pika、Runway）**不适合我们**的动漫赛璐璐风。

## 排名表

| # | 工具 / 方法 | 做什么 | 价格 | 国内能不能用 | 适不适合我们 |
|---|---|---|---|---|---|
| 1 | **Ludo.ai VFX Sprite Generator** | 文字 → 特效序列帧，4–64 帧，32–384px，透明底；可给起始/中间/结束 3 张关键帧（可留空 = 从无到有再消失）；循环/一次性两种；导出 PNG 图集 + JSON + GIF + MP4；还能顺手生成音效 | 可免费试；Indie 约 $15/月（年付，每年 3000 积分）；Hydra 模型 3 积分/秒 → 一个 3 秒特效约 9 积分 ≈ $0.5；**API / MCP 只有 Pro（$35/月年付）才有** | 海外网站，需要梯子 + 外币卡【未核实】 | ★★★★ 最对口。风格要在提示词里写"anime cel-shaded"，需先试几张看能不能做出 P5 味 |
| 2 | **黑底 + Additive 叠加（技巧，免费）** | GPT 4x4 黑底序列、Kling/Seedance 黑底视频抽帧，直接用 Additive（加色）材质显示，黑 = 透明 | 免费（只花生图钱） | 能 | ★★★★★ 火花、光、火、魔法这类"发光"特效最佳。缺点：不能做深色/实心的东西（比如黑烟、墨水） |
| 3 | **亮度转透明脚本（unmult / 去黑底）** | 把黑底帧算成真透明 PNG：透明度 = 像素最亮那个通道，再把颜色除回去（去预乘），发光边不会发黑 | 免费（Python / ffmpeg 几行；AE 有免费插件 Crate's Unmult） | 能 | ★★★★ 当需要普通 Alpha 混合（不想用 Additive）时用 |
| 4 | **LTX-2.5 Alpha Gen（开源）** | 普通视频 → 每帧透明遮罩，专门说能处理烟、火、水、玻璃这类半透明 | 免费开源，但要大显卡（H100 级）；有 ComfyUI 工作流；许可证要看是否可商用【未核实】 | 模型在 HuggingFace；RunningHub 上有没有现成工作流【未核实，需上去搜】 | ★★★ 给 Kling/Seedance 出的非黑底视频抠图用 |
| 5 | **MatAnyone 2 / VideoMaMa（开源视频抠像）** | 视频抠像，输出透明前景 + 遮罩 | 免费；VideoMaMa 非商用许可 | 同上 | ★★ 更擅长抠人，特效发光边一般 |
| 6 | **ParticleFX Studio** | 浏览器里做 2D 粒子特效，632 个现成特效 + AI 助手按描述搭特效；导出 Unity 图集、PNG 序列、带 Alpha 的 WebM；有 MCP（Claude Code 能直接调用） | 免费号每天 3 次导出；付费 $7.99/月起（引擎数据文件要付费） | 海外网站【未核实】 | ★★★ 适合小火花、星星、光点这类"粒子"；手绘斩击/大招味道不够 |
| 7 | **Unity AI（Assistant + Sprite Generator 做 spritesheet）** | 一张图 → 先生成约 5 秒小视频 → 均匀取 16 帧拼 4x4；Unity 里直接生成动画 Clip | Personal 版 14 天试用 1000 积分，之后 $10/月；Pro 版自带 | Unity 6 已在中国大陆下架，全球版 AI 服务在国内能否用【未核实】，可能要梯子；国内版"团结引擎"有 AI Graph（腾讯混元） | ★★ 主要为角色转身/动作设计，不是特效专用；只有 16 帧 |
| 8 | **Sprixen** | 40 类现成特效可调色 + "AI 创建"：文字 → 8 帧 / 1 秒 / 256px 特效，自动去底；导出 Unity 图集、WebM | AI 生成 6 积分/个（积分价格未写明） | 海外【未核实】 | ★★ 帧数和分辨率太低，手机大招不够看 |
| 9 | **Scenario** | 图 → 视频（Seedance / Pixverse）→ 手动截帧拼表；有 "VFX Hits" 爆炸 LoRA | 约 $15/月起，有免费额度 | 海外 | ★★ 流程和我们 RunningHub 抽帧一样，没优势 |
| 10 | **MythicLemon AI Flipbook（UE 插件）** | 用 GPT 生图做 4x4/6x6/8x8 特效表 | 插件 + 自己的 OpenAI 钱（约 $0.04/张） | — | ✗ 只支持虚幻引擎。**但它的技巧值得抄**（见下面"技巧 B"） |
| 11 | PixelLab / Sorceress / Retro Diffusion | 像素风角色/特效序列 | $0.07–0.25/个动画 等 | 海外 | ✗ 只有像素风 |
| 12 | Higgsfield / Pika / Runway / Krea / Luma | 给实拍视频加爆炸、着火等电影特效 | 订阅 | 海外 | ✗ 输出的是带背景的视频，不是游戏素材 |
| 13 | EmberGen 2.0 / IlluGen（JangaFX，非 AI） | 实时模拟火、烟、爆炸，直接出 flipbook；IlluGen 偏风格化特效 | 付费软件 | 能买 | △ 不是 AI，要学软件；做"写实烟火"很强 |

## 前三推荐

**第 1 名：黑底 + Additive（继续用 GPT / Kling / Seedance，不买新工具）**
- 发光类（火花、斩光、火、魔法、大招光柱）占战斗特效大头，这类用 Additive 叠加，黑底直接消失，**不用抠图，零成本**。
- Unity uGUI 做法：给 Image 用一个"UI Additive"材质（Blend One One），序列帧用脚本按帧换 sprite。
- 要普通透明 PNG 时，跑一遍"亮度转透明 + 去预乘"脚本即可。
- 视频模型技巧：首帧和尾帧都给**纯黑图**，提示词写"在纯黑背景上，特效从无到有爆开再消失"，这样首尾干净，好做一次性特效；首尾给同一张图 = 循环特效（Kling 首尾帧官方支持）。

**第 2 名：Ludo.ai VFX Sprite Generator（专业对口，花点小钱试）**
- 唯一一个"专门为游戏特效设计"、直接出透明序列帧、支持关键帧控制、有 API/MCP 的工具。
- 建议先用免费试用做 3 个测试：斩击、打击火花、水花，看能不能做出赛璐璐/P5 感（黑红白、硬边）。
- 满意再开 Indie；要让 Claude 批量调用就得 Pro（$35/月年付）。

**第 3 名：LTX-2.5 Alpha Gen（给不是黑底的视频抠特效）**
- 当 Kling / Seedance 出的视频带背景、或要做黑烟/水花这种不能 Additive 的东西时，用它抠出带半透明的遮罩。
- 优先去 RunningHub 搜有没有现成工作流；没有的话可以用 Wan-Alpha 直接生成透明视频替代。

## 两个可以马上用的技巧

**技巧 A：GPT 4x4 帧不一致怎么办**
- 先画"关键帧 1 张"，再让 GPT 以它为参考出 4x4；或者只让 GPT 出 3 张关键帧（开始/最大/消失），中间帧用视频模型首尾帧补。
- 拼表后对每格按亮度中心重新对齐，可减少"抖动"。

**技巧 B（抄 MythicLemon 插件）**
- 调 GPT 图像"编辑"接口时，**附一张画好格子线的模板图 + 遮罩**，让模型只在格子里画，帧位置更稳。
- 切图时不要死按 4x4 切：先自动检测模型实际画了几格（它们有时画成 5x5）。

## 其他发现
- Unity AI 2026-05-04 开放公测；还有 Material Generator（文字 → 材质），但**没找到官方"AI 生成 Shader Graph / 粒子系统"功能**。程序化特效（Shader、粒子参数）现在最靠谱的还是让 Claude 直接写 C# / Shader 代码。
- 普通抠图（BiRefNet、LightAI 去背景）对发光特效效果差，边缘会发黑或丢掉光晕——这是行业通识，所以发光类优先 Additive / 亮度转透明。
- 国内能用的方案：RunningHub（Kling / Wan / Seedance / Wan-Alpha）、团结引擎 AI Graph。海外工具大多需要梯子和外币卡。

## 来源
- Ludo VFX Sprite Generator：https://ludo.ai/tools/vfx-sprite-generator
- Ludo 价格：https://ludo.ai/pricing
- Ludo Sprite Generator 文档：https://ludo.ai/docs/sprite-generator
- ParticleFX Studio：https://particlefx.studio/
- Sprixen：https://sprixen.com/animated-vtt-effects
- MythicLemon AI Flipbook（UE）：https://forums.unrealengine.com/t/mythiclemon-ai-flipbook-generator-niagara-vfx-spritesheets-from-text-prompts/2724414
- Unity AI spritesheet 文档：https://docs.unity3d.com/Packages/com.unity.ai.assistant@2.1/manual/Sprite/spritesheet-overview.html
- Unity AI 公测价格：https://blog.buildfastwithai.com/unity-ai-open-beta-guide-2026
- Unity AI Material Generator：https://unity.com/blog/unity-ai-material-generator
- Unity 6 中国下架：https://80.lv/articles/unity-6-is-taken-offline-in-china/
- 团结引擎 AI Graph：https://www.ithome.com/0/891/863.htm
- Scenario 序列帧教程：https://help.scenario.com/articles/9088582240-create-spritesheets-with-scenario
- Scenario VFX Hits LoRA：https://www.scenario.com/models/vfx-hits
- Scenario 价格：https://help.scenario.com/pricing-plans
- Sorceress（像素 VFX）：https://sorceress.games/blog/ai-sprite-generator-pixel-walk-cycles-idles-and-vfx
- Retro Diffusion API：https://github.com/Retro-Diffusion/api-examples
- PixelLab：https://pixellab.ai/
- Wan-Alpha：https://github.com/WeChatCV/Wan-Alpha
- Wan-Alpha ComfyUI 教程：https://docs.comfy.org/zh/tutorials/video/wan/wan-alpha
- LTX-2.5 Alpha Gen：https://huggingface.co/Lightricks/LTX-2.5-22b-IC-LoRA-Alpha-Gen
- MatAnyone 2：https://studio.aifilms.ai/blog/matanyone-2-video-matting
- VideoMaMa：https://github.com/cvlab-kaist/VideoMaMa
- TransPixeler（透明视频研究）：https://arxiv.org/abs/2501.03006v2
- Crate's Unmult（黑底转透明）：https://www.productioncrate.com/plugins/crates-unmult
- Unmult 原理：https://www.provideocoalition.com/unmult-with-and-without-a-plug-in/
- Kling 首尾帧：https://higgsfield.ai/blog/Kling-Start-End-Frames
- Higgsfield 特效预设：https://higgsfield.ai/blog/Higgsfield-Effects-Mix-Lets-You-Tell-Bigger-Stories
- JangaFX 2026 路线图（EmberGen 2.0 / IlluGen）：https://80.lv/articles/jangafx-reveals-2026-roadmap
- AI 2D 素材实测（帧间漂移问题）：https://www.summerengine.com/zh/blog/ai-2d-game-asset-generator
