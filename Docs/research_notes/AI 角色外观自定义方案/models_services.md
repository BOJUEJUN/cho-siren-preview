# AI 图像编辑 / 换装模型与服务对比（截至 2026-11-03 调研）

> 场景：已有 2D 二次元全身立绘（紫黑/青/粉 朋克偶像风，64 角色，需 RGBA 透明输出，Web/iOS/Android/PC/Mac，国内+海外玩家），玩家输入或选择“让角色 X 穿上服装 Y”。
> 说明：本笔记大量数字来自第三方聚合/评测站（官方价格页未能直接抓取），已逐项标注来源与冲突。模型命名变化很快，部分名称（GPT Image 2.5、Nano Banana 2.1、FLUX 3）只在单一来源出现，需上线前核实。

## 1. 2026 年哪些指令/参考图编辑模型最适合二次元角色换装（身份保持、画风、多参考、分辨率、延迟、价格、开源/许可、国内可用性）

### Takeaway
2026 下半年闭源编辑第一梯队为 OpenAI GPT Image 2/2.5、Google Nano Banana Pro / 2 / 2.1、ByteDance Seedream 5.0 Pro、Alibaba Qwen-Image 3.0 Pro，几家 Elo 差距仅个位数到十几分，排名随榜单和日期变化；单张价格从约 $0.014（GPT Image 2.5 medium）/ $0.034（Nano Banana 2.1 1K）到 $0.134（Nano Banana Pro）不等。面向中国大陆可直连的只有国产（Seedream/即梦 via 火山方舟、Qwen via 阿里云百炼等）；可商用自托管的开源编辑模型主要是 Apache 2.0 的 Qwen-Image-Edit-2509/2511 和 FLUX.2 [klein] 4B。

### Cited Findings

**排行榜（Elo 不可跨榜比较）**
- 2026-10-26 LMArena 文生图快照（6.87M 票、82 模型）：GPT Image 2.5 Sunburst #1/1427，GPT Image 2.5 Flare #2/1401，GPT Image 2 (medium) #3/1381，MAI-Image-2.6 #4/1329，Nano Banana 2.1 #5/1325，Grok Imagine Image 2.0 #6/1317，Reve 2.1 #7/1300，Meta Muse Image #9/1269，Nano Banana 2 #11/1257，Qwen-Image 3.0 Pro #12/1254，Seedream 5.0 Pro #14/1250，Ideogram 4 #20/1200，FLUX.2 [max] #28/1161 — [Teamday, 2026-10-29](https://www.teamday.ai/blog/best-ai-image-models-2026)（注意这是文生图榜，不是编辑榜）
- 某图像编辑榜 9 月抓取：GPT Image 2 (high) 1257、Nano Banana 2 1251、Qwen-Image-3.0-Pro 1249、Nano Banana Pro 1247、Seedream 5.0 Pro 1247；FLUX.2 [max] 1202（#20）；Qwen Image Edit 2511 1165（#35）— [benchmarklist（Artificial Analysis 编辑榜镜像）](https://benchmarklist.com/arenas/artificial_analysis_image_editing/)、[Magic Hour 编辑榜](https://magichour.ai/model-leaderboard/image-editing)
- 10 月初另一编辑榜快照：Nano Banana Pro #1（1250），领先 Nano Banana 2 约 11 分，GPT Image 2 #3，FLUX.2 [flex] #6，Seedream 5.0 Lite #8 — [Magic Hour](https://magichour.ai/model-leaderboard/image-editing) / [Versely](https://www.versely.studio/best/best-image-editing-model)（聚合来源，相互不一致）
- 7 月前后 GPT Image 2 曾以 1255 短暂登顶 Artificial Analysis 编辑榜，超过 Nano Banana Pro 的 1247 — [tech-insider](https://tech-insider.org/best-ai-image-generator-2026/)
- MAI Image 2.7（Microsoft）2026-10-27 预览，声称 Arena 编辑榜 #3，暂无 API/价格 — [Teamday](https://www.teamday.ai/blog/best-ai-image-models-2026)

**Google Gemini 图像（Nano Banana 系列）**
- Nano Banana Pro（Gemini 3 Pro Image）：1K/2K ≈ $0.134/张，4K ≈ $0.24 — [simplemetrics](https://simplemetrics.xyz/how-much-per-image-is-nano-banana-and-nano-banana-pro/)、[myarchitectai](https://www.myarchitectai.com/blog/nano-banana-api-pricing)、[Teamday](https://www.teamday.ai/blog/best-ai-image-models-2026)
- Nano Banana 2（Gemini 3.1 Flash Image）：0.5K $0.045 / 1K $0.067 / 2K $0.101 / 4K $0.151；$60/1M 图像输出 token；Batch 约 5 折；2026-10-06 已标记 deprecated（未定关停日期）— [laozhang 定价指南](https://blog.laozhang.ai/en/posts/nano-banana-2-api-pricing-guide)、[Teamday](https://www.teamday.ai/blog/best-ai-image-models-2026)
- Nano Banana 2.1：2026-10-06 GA；1K $0.0336 / 2K $0.0504 / 4K $0.113，Batch 1K $0.0168；最多 14 张参考图；对话式编辑 — [Teamday](https://www.teamday.ai/blog/best-ai-image-models-2026)（单一来源，需核实）
- Nano Banana 2 Lite（Gemini 3.1 Flash Lite Image）约 $0.03–0.034/张 — [anotherwrapper](https://anotherwrapper.com/tools/llm-pricing/image-models/compare/gemini-3-pro-image/nano-banana-2-lite-gemini-31-flash-lite-image)
- 原版 Nano Banana（Gemini 2.5 Flash Image）$0.039/张，Batch $0.0195（旧型号）— [simplemetrics](https://simplemetrics.xyz/how-much-per-image-is-nano-banana-and-nano-banana-pro/)
- 延迟实测（含排队）：Nano Banana 2 平均 13 s — [Teamday 9/23 测试](https://www.teamday.ai/blog/best-ai-image-models-2026)
- 身份保持：Runware 指南称“身份在脸和发型而非服装”，换衣不破坏相貌；但跨场景仍会出现“换了一个人”的漂移，推荐多参考图/多角度角色设定图 — [Runware NB2 角色一致性](https://runware.ai/docs/models/google-nano-banana-2/guides/character-consistency)、[apipass](https://apipass.dev/blogs/master-character-consistency-nano-banana-2-api)
- WeShop 演示：先转二次元再换装，保持了原姿势与面部特征（营销来源）— [WeShop](https://www.weshop.ai/blog/?p=8885)
- 国内可用性：Google 论坛回复称 Gemini API 在中国不可用；第三方称 2026-04 检查时中国大陆、香港、澳门不在 AI Studio/Gemini API 支持地区列表，调用返回 400 FAILED_PRECONDITION “User location is not supported” — [Google AI 开发者论坛](https://discuss.ai.google.dev/t/gemini2-5-china/75434)、[yingtu.ai](https://yingtu.ai/zh/blog/gemini-api-region-availability)

**OpenAI gpt-image**
- GPT Image 2（2026-04 发布）：token 计费，输入 $8/1M、输出 $30/1M；1024² low $0.006 / medium $0.053 / high $0.211；1024×1536 high ≈ $0.165；Batch 5 折；参考图一律按最高质量处理，所以编辑比纯生成贵 — [apiyi](https://docs.apiyi.com/en/api-capabilities/gpt-image-2/overview)、[OpenAI 社区帖：输入图定价](https://community.openai.com/t/openai-must-document-the-input-image-pricing-of-gpt-image-2-so-i-did/1382940)
- GPT Image 2 **不支持** `background: "transparent"`（只有 auto/opaque），也**不再暴露** `input_fidelity`（传入报 400）；原生透明需退回 gpt-image-1.5 — [apiyi](https://docs.apiyi.com/en/api-capabilities/gpt-image-2/overview)、[picassoia](https://blog.picassoia.com/gpt-image-2-api-pricing-cost-per-image-parameters-and-transparent-pngs)、[fal gpt-image-1.5 edit](https://fal.ai/models/fal-ai/gpt-image-1.5/edit/llms.txt)
- GPT Image 2.5 Sunburst / Flare：2026-09-08 发布；$30/1M 输出 token，medium 1024² 实测约 $0.014；最多 4 张参考（Teamday 路由）+ mask；平均 15 s — [Teamday](https://www.teamday.ai/blog/best-ai-image-models-2026)（单一来源）

**Black Forest Labs FLUX**
- FLUX.2 按百万像素计费，首 MP：[klein] 4B ≈ $0.014、[klein] 9B ≈ $0.015、[pro] $0.03、[flex] $0.05、[max] $0.07；klein 4B 2MP 示例 $0.015 — [BFL 帮助中心](https://help.bfl.ai/articles/7686530342-flux-2-pricing-guide)、[BFL 定价文档](https://docs.eu.bfl.ai/quick_start/pricing)
- FLUX.1 Kontext [pro] $0.04/张、[max] $0.08/张（旧一代）— [puter](https://developer.puter.com/tutorials/flux-api-pricing/)
- 许可：FLUX.2 [klein] 4B 为 Apache 2.0；[klein] 9B 与 [dev] 为 FLUX Non-Commercial License（输出可商用，但在商业产品中部署模型需向 BFL 购买授权）— [invideo](https://invideo.io/blog/flux-ai-image-generator/)、[BFL 帮助中心](https://help.bfl.ai/articles/7986977817-what-are-the-costs-associated-with-using-your-models)
- FLUX 3 Image：2026-07 发布家族、10 月开放 API；原生 2K/4K；最多 10 张参考 + 框选编辑；商业权重许可，Dev 权重“即将推出”— [Teamday](https://www.teamday.ai/blog/best-ai-image-models-2026)、[layer3labs](https://www.layer3labs.io/guides/flux-3-image-pricing)（另一检索结果认为 FLUX 3 命名存疑，需以 bfl.ai 为准）
- FLUX.2 [max] 实测 $0.07、平均 28 s — [Teamday](https://www.teamday.ai/blog/best-ai-image-models-2026)

**Alibaba Qwen**
- Qwen-Image-Edit-2511（基于 2509）：Apache 2.0；改进单主体一致性；支持多图输入（pipeline 接收图片列表），含多人合照融合；示例均为照片 — [HF 模型卡](https://huggingface.co/qwen/qwen-image-edit-2511)
- 阿里云百炼官方 qwen-image-edit：北京地域 0.3 元/张，新加坡 0.330266 元/张（原价，不含限时优惠）— [阿里云帮助文档](https://help.aliyun.com/en/model-studio/model-qwen-image-edit)
- Model Studio edit-plus / edit / edit-max 约 $0.03 / $0.045 / $0.075（2026-08 第三方整理）— [invideo 定价汇总](https://invideo.io/blog/ai-image-model-pricing/)
- Qwen-Image 3.0 Pro：2026-07-21 发布、08-05 GA；$0.04 (1K) / $0.075 (2K)；独立编辑端点；12 种语言；fal 队列 40–70 s（平均 43 s）；未找到开源权重 — [Teamday](https://www.teamday.ai/blog/best-ai-image-models-2026)
- Qwen-Image-2.1（2026-09-20）：HF/ModelScope 发布但使用 Qwen Research License，商用需另行授权（与此前 Apache 2.0 系列不同）；有来源称并无权重仓库，存在冲突 — [datanorth](https://datanorth.ai/news/qwen-releases-qwen-image-2-1)；contradicted by [orcarouter](https://www.orcarouter.ai/blog/qwen-image-2-1)

**ByteDance Seedream / 即梦（火山方舟 / BytePlus）**
- Seedream 5.0 Pro：2026-07-08 上线火山方舟，1K 0.3 元/张、2K 0.6 元/张（作者未实测，基于文档）— [heyuan110](https://www.heyuan110.com/zh/posts/ai/2026-07-09-seedream-5-pro/)
- BytePlus/海外：≤2.36MP $0.045、更大 $0.09；首张参考图免费，额外每张 $0.003 — [BytePlus](https://www.byteplus.com/zh-CN/product/Seedream)、[Atlas Cloud](https://www.atlascloud.ai/blog/ai-updates/seedream-5-0-pro-price)；另一处报 1K $0.0675、2K/4K $0.135（冲突）— [Zalify](https://zalify.com/ai-models/seedream-5-0-pro)
- 参考图上限：Pro 最多 10 张（Atlas），Lite/Flash 最多 14 张 — [Atlas Cloud](https://www.atlascloud.ai/blog/ai-updates/seedream-5-0-pro-price)、[Higgsfield](https://higgsfield.ai/blog/best-platforms-seedream-5)、[Teamday](https://www.teamday.ai/blog/best-ai-image-models-2026)
- Seedream 5.0 Pro 支持框/箭头/草图空间编辑；5.0 Lite 约 $0.035、平均 27 s；5.0 Flash 约 $0.018（转售价）、1K/2K、2026-10 初发布 — [Teamday](https://www.teamday.ai/blog/best-ai-image-models-2026)

**其他**
- Midjourney V8.2（2026-07-24），编辑模型 08-27 上线：最多 4 张参考、局部重绘/扩图；**无公开 API**（订阅制）— [Teamday](https://www.teamday.ai/blog/best-ai-image-models-2026)
- Tencent：Hy Image 3.5 Preview（2026-09-22，最高 4K，多轮编辑、参考图生成，无独立排名）；HunyuanImage 3.0 开源权重 — [Teamday](https://www.teamday.ai/blog/best-ai-image-models-2026)
- Grok Imagine Image 2.0 $0.04、13 s；Meta Muse Image $0.01、19 s；Ideogram 4 开源 9.3B；Wan 2.7 Image（2026-04）内置编辑 — [Teamday](https://www.teamday.ai/blog/best-ai-image-models-2026)

### Inferences
- 对“保持身份+换装”，多参考图（角色立绘 + 服装参考）是关键能力：Nano Banana 2.1（14）、Seedream 5.0（10–14）、FLUX 3（10）> GPT Image 2/2.5（4）≈ Midjourney（4，且无 API，不适合游戏后端）。
- 国内+海外双区部署的现实方案：国内走火山方舟 Seedream 或阿里云百炼 Qwen-Image-Edit（0.3 元/张量级），海外走 Nano Banana 2.1 或 GPT Image 2.x；或统一自托管 Apache 2.0 的 Qwen-Image-Edit-2511 规避区域限制。
- 单张成本量级：便宜档 $0.01–0.04，中档 $0.045–0.07，高档 $0.13–0.24；对玩家触发的 UGC 功能，按每次 1–4 张候选计算，建议用 Flash/Lite/Batch 档或自托管。
- 所有官方示例几乎都基于照片；对“紫黑/青/粉朋克偶像”这种特定画风的二次元保真度没有公开基准，必须用项目自己的 64 角色做 A/B 盲测。

### Gaps
- 未能直接抓取 ai.google.dev / platform.openai.com / bfl.ai / 火山方舟 官方价格页，价格多为第三方转述；GPT Image 2.5、Nano Banana 2.1、FLUX 3 仅见单一来源。
- OpenAI API 在中国大陆的可用性本轮未检索到来源（据既往了解 OpenAI 自 2024-07 起限制中国大陆 API 访问，但未在本轮核实）。
- Kling（可灵）图像编辑 / Kolors（可图）在 2026 的版本、价格与二次元表现未查到可靠来源。
- 未找到任何针对二次元立绘换装的公开基准或身份一致性量化评测（如 ArcFace/CLIP-I 在 anime 上的分数）。
- 各家 ToS 对“玩家 UGC 生成、游戏内商用”的具体条款未逐一核对（BFL API 输出一般可商用；Qwen 2511 Apache 2.0 明确）。

## 2. 专用虚拟试穿模型（IDM-VTON、CatVTON、OOTDiffusion、Kolors VTON、FitDiT 等）能否用于二次元角色

### Takeaway
未找到任何证据显示 VTON 专用模型在二次元/插画角色上有效；它们的训练与输入假设均为真人照片（正面全身人像 + 平铺/模特图服装），对 anime 立绘属于分布外输入。通用指令编辑模型（Nano Banana、Seedream、Qwen-Edit）是更合适的路线。

### Cited Findings
- CatVTON 轻量：去掉额外图像编码器、ReferenceNet 与文本编码器，仅保留核心扩散模块，比 IDM-VTON 易部署；IDM-VTON 专注服装保真（logo、条纹、颜色不变形），但显存需求更高 — [Medium 2026 VTON 实践](https://medium.com/@anmjawad007/virtual-try-on-in-2026-how-catvton-and-idm-vton-work-what-i-learned-on-a-macbook-m1-pro-and-how-6428f460fcba)
- 输入假设：人物图为正对镜头的全身照，服装图为平铺/隐形模特/商品图 — [Medium 2026 VTON 实践](https://medium.com/@anmjawad007/virtual-try-on-in-2026-how-catvton-and-idm-vton-work-what-i-learned-on-a-macbook-m1-pro-and-how-6428f460fcba)、[RunComfy CatVTON 工作流](https://runcomfy.com/comfyui-workflows/catvton-advanced-ai-virtual-try-on)
- IDM-VTON 模型卡及 HF Demo 示例服装均为 VITON-HD 类照片数据 — [HF IDM-VTON](https://huggingface.co/tuananhsp2202/IDM-VTON)、[HF VTON Space 示例](https://huggingface.co/spaces/peterpeter8585/Virtual-Try-On/blob/main/example/cloth/09305_00.jpg)

### Inferences
- VTON 模型依赖人体解析（DensePose/OpenPose/human parsing），这些在二次元夸张比例、平涂上色上易失效；同时会把输出推向写实质感，造成画风漂移。
- 若要“服装 Y”可控，更现实的做法是：为每件服装准备二次元风格的服装参考图（或设定图），作为第二参考图喂给多参考编辑模型。

### Gaps
- 没有找到 CatVTON/IDM-VTON/OOTDiffusion/FitDiT/Kolors VTON 在 anime 上的定量或定性测试；需自行在 3–5 个角色上冒烟测试确认。

## 3. 透明背景：哪些模型原生输出 alpha；二次元抠图方案与发丝边缘质量

### Takeaway
2026 主流编辑模型（GPT Image 2/2.5、Nano Banana、Seedream、FLUX、Qwen-Edit）均未见原生透明输出；只有旧版 gpt-image-1/1.5 支持 `background: transparent`，以及开源的 Qwen-Image-Layered（Apache 2.0，图像→多 RGBA 图层）。实际管线应为“编辑 → 纯色背景 → anime 专用抠图”，首选 BiRefNet 的 anime 微调版 ToonOut（像素准确率 95.3% → 99.5%）。

### Cited Findings
- gpt-image-2 不支持透明背景，需 opaque 输出后自行抠图；gpt-image-1.5 支持 `background: "transparent"` — [apiyi](https://docs.apiyi.com/en/api-capabilities/gpt-image-2/overview)、[picassoia](https://blog.picassoia.com/gpt-image-2-api-pricing-cost-per-image-parameters-and-transparent-pngs)
- Teamday 对 20+ 个 2026 主流模型的整理中，没有任何一个标注透明背景支持 — [Teamday](https://www.teamday.ai/blog/best-ai-image-models-2026)
- Qwen-Image-Layered：把一张图分解为 3 或 8 个（可递归）RGBA 图层，RGBA-VAE + VLD-MMDiT 架构（arXiv 2512.15603），Apache 2.0；附带用 Qwen-Image-Edit 编辑图层的 Gradio 脚本；ComfyUI 工作流与 GGUF 量化可用；文生多 RGBA 能力有限；Replicate 4.28 万次运行 — [GitHub](https://github.com/QwenLM/Qwen-Image-Layered)、[Replicate](https://replicate.com/qwen/qwen-image-layered)
- ToonOut（arXiv 2509.06839）：在 1,228 张二次元图上微调 BiRefNet，anime 像素准确率 95.3% → 99.5%；作者指出即使 anime 专用模型结果也常不理想；权重公开 — [arXiv](https://arxiv.org/html/2509.06839v1)、[HF 权重](https://huggingface.co/joelseytre/toonout)
- rembg 的 isnet-anime 被推荐用于二次元角色，但输出有轻微雾化，可用 `-a`（alpha matting）+ 阈值缓解 — [GIGAZINE rembg 2026-05](https://gigazine.net/gsc_news/en/20260510-rembg/)
- u2net 系（含 anime 调优的 abg）在发丝、半透明边缘不如 BiRefNet；BiRefNet 的 anime 版更善于处理赛璐璐边缘、平涂与风格化发型（博客观点，非基准）— [HackerNoon](https://hackernoon.com/birefnet-toonout-anime-background-removal-with-995percent-accuracy)、[Replicate BiRefNet](https://replicate.com/sprited/birefnet/readme)
- BiRefNet 有 ComfyUI 官方去背教程节点 — [Comfy 文档](https://docs.comfy.org/tutorials/utility/remove-background-birefnet)

### Inferences
- 推荐管线：编辑时提示词锁定“纯色（如 #00FF00 或纯白）背景、保持原构图与尺寸” → ToonOut/BiRefNet 抠图 → 用原立绘的 alpha 作为参考做边缘对比 QA；朋克风的青/粉高饱和配色要避免选与之冲突的抠图底色（如勿用纯青/品红底）。
- 因项目规范要求“拒绝棋盘格/纯色伪透明”，必须在管线中校验真实 alpha 通道（例如检测 alpha 是否全 255、边缘半透明像素比例）。
- Qwen-Image-Layered 可作为“换装只替换服装图层”的探索方向，但未见 anime 测试。

### Gaps
- 没有找到 BiRefNet vs isnet-anime vs ToonOut 在发丝边缘（如 SAD/MSE/Grad 抠图指标）的直接对比。
- SAM 2/SAM 3、LayerDiffuse 在 2026 的 anime 抠图表现本轮未检索到来源。
- 未确认 Nano Banana / Seedream 是否有隐藏的透明输出参数。

## 4. 已知失败模式：面部漂移、手/身体结构改变、画风漂移、偶像服装 NSFW 误判、比例不一致

### Takeaway
面部/身份漂移仍是所有模型的已知问题，主流缓解是多参考图与角色设定图；NSFW 误判主要来自“输出侧图像安全过滤”（Gemini 的 IMAGE_SAFETY），调低文本安全阈值无效，偶像舞台装/泳装类请求需预期一定拒绝率。画风漂移与手部/比例问题没有找到 anime 专门的量化资料。

### Cited Findings
- 换场景后“模型给你换了个人”是常见漂移；开发者报告角色脸型在后续生成中变短等轻微漂移；建议多角度角色设定图作为参考 — [Runware](https://runware.ai/docs/models/google-nano-banana-2/guides/character-consistency)、[apipass](https://apipass.dev/blogs/master-character-consistency-nano-banana-2-api)
- Google 开发者论坛：Gemini 图像编辑的新安全过滤可能对真实人脸和擦边内容过度触发（“content is not permitted”）— [Google AI 开发者论坛](https://discuss.ai.google.dev/t/frustration-with-content-is-not-permitted-errors-in-gemini-2-0-flash-experimental-image-editing/72083/9)（2.0 Flash 时代，较旧）
- IMAGE_SAFETY / IMAGE_PROHIBITED_CONTENT 是对生成图的输出侧拦截，输入可通过但结果被拦；调整文本安全阈值通常无效；应检查 candidate finish reason — [yingtu.ai](https://yingtu.ai/en/blog/gemini-refusing-harmless-images)、[aifreeapi](https://www.aifreeapi.com/ru/posts/gemini-image-silent-failure-image-safety-fix.md)、[apiyi FAQ](https://docs.apiyi.com/ja/faq/gemini-image-input-blocked)
- 被拦截不一定代表请求涉黄；虚构角色也不能豁免露骨内容政策 — [nanobanana.org](https://nanobanana.org/posts/nano-banana-nsfw)（第三方）
- 良性任务被拦的常见原因：人脸编辑、人/宠物误分类、公众人物措辞、旧对话上下文 — [yingtu.ai](https://yingtu.ai/en/blog/gemini-refusing-harmless-images)
- IDM-VTON 着重解决早期扩散试穿“改坏服装”（logo 错、条纹糊、颜色偏）问题，说明服装细节保真是普遍难点 — [Medium](https://medium.com/@anmjawad007/virtual-try-on-in-2026-how-catvton-and-idm-vton-work-what-i-learned-on-a-macbook-m1-pro-and-how-6428f460fcba)

### Inferences
- 对 64 个偶像角色（部分可能外观年轻），短裙/露脐/泳装等偶像舞台装在海外闭源模型上可能被输出侧过滤拦截，且无法靠参数关闭；产品上应提供预设服装库 + 文本审核前置，并为拦截设计友好回退（换服装/退款点数）。
- 国内模型同样有内容审核（火山方舟有审核相关诊断能力），需在两区分别测拦截率。
- 失败模式量化建议：用原立绘 vs 生成图计算面部区域相似度、轮廓/身高比、手指数等自动 QA，低于阈值自动重试。

### Gaps
- 无 anime 立绘换装的手部/解剖结构、比例漂移、画风漂移的公开统计。
- 没找到 OpenAI、Seedream、Qwen 对偶像/泳装二次元角色的误判率数据。
- Gemini 2.x/3.x 的官方安全设置文档本轮未直接抓取。

## 5. 自托管 vs API 的单次生成成本（GPU 类型、每张秒数）

### Takeaway
自托管开源编辑模型（Qwen-Image-Edit-2511 + 4 步 Lightning LoRA）在 A100/H100 上单张约 3.5–5 s，GPU 成本约 $0.005–0.008/张（不含冷启动、闲置、利用率损失），比闭源 API（$0.014–0.134）便宜一个数量级，但需承担运维、冷启动 60–90 s、以及画质略低于闭源第一梯队（编辑榜 2511 为 1165 vs 头部 ~1250）。

### Cited Findings
- Qwen-Image（文生图）H100 优化版：1328×1328 约 3.5 s（2.5 it/s，“10× 加速”），按 H100 $5.49/h 计 Lightning 模式约 $0.005/张；冷启动 60–90 s — [Replicate smkrv/qwen-image](https://replicate.com/smkrv/qwen-image/readme)
- Qwen-Image-Edit 4 步 LoRA：1024×1024 在 A100 80GB 上 5.10 s，$0.00714/张 — [Replicate qwen-image-edit-fastest](https://replicate.com/usamaehsan/qwen-image-edit-fastest)
- Edit-2511 Lightning LoRA 声称 4 步比标准 40 步快约 10× — [HF Qwen-Image-Edit-2511-Lightning](https://huggingface.co/frankjoshua/Qwen-Image-Edit-2511-Lightning)
- 第三方托管 Qwen Edit 转售价：Atlas $0.02/张、302.AI $0.05/次；百炼官方 0.3 元/张 — [Atlas Cloud](https://www.atlascloud.ai/zh/models/atlascloud/qwen-image/edit)、[302.AI](https://302.ai/product/detail/302ai-qwen-image-edit)、[阿里云](https://help.aliyun.com/en/model-studio/model-qwen-image-edit)
- 对照 API：GPT Image 2.5 medium ≈ $0.014、Nano Banana 2.1 1K $0.0336（Batch $0.0168）、Seedream 5.0 Pro $0.045 / 0.3 元、FLUX.2 klein 4B $0.014、Nano Banana Pro $0.134 — [Teamday](https://www.teamday.ai/blog/best-ai-image-models-2026)、[BFL](https://help.bfl.ai/articles/7686530342-flux-2-pricing-guide)
- 成本导向排名中 Qwen Image Edit 2511 被评为最便宜选项 — [Lumenfall](https://lumenfall.ai/arena/image-editing)

### Inferences
- 估算：每 DAU 每日 3 次换装 × 2 张候选 = 6 张；1 万 DAU/日 = 6 万张。API 中档（$0.035）≈ $2,100/日；自托管 Qwen 2511 Lightning（$0.007）≈ $420/日 + 运维；需要约 6 万 × 5 s ≈ 83 GPU·h/日（≈ 4 张 H100/A100 常驻，峰值另计）。
- Apache 2.0 的 Qwen-Image-Edit-2511 / FLUX.2 klein 4B 是唯一可无额外授权商用自托管的选项；FLUX.2 dev/klein 9B 与 Qwen-Image-2.1 需额外商业授权。
- 自托管还可加 LoRA：为本项目 64 角色+画风训练风格/角色 LoRA，可显著缓解画风漂移（推断，需验证）。

### Gaps
- 无 Qwen-Image-Edit-2511 在 H100/4090/L40S 上的实测编辑基准（多参考图输入时的耗时与显存）。
- FLUX.2 klein 4B 自托管编辑速度、显存需求未找到。
- 国内 GPU 云（阿里云、火山引擎）H100/H20/4090 实时单价未查。
