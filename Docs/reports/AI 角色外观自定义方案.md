# 让 AI 换衣，但别让它换人

结论先行：2026 年的技术已经能把“给现有立绘换衣服”做成玩法，但只有在“锁定原立绘的脸、姿势和画布，只让 AI 改服装区域”时才可靠；“任意服装、发型、妆容 + 全部衍生素材 + 大招视频”同步更新，目前在成本、一致性和合规上都还不成立。可用的引擎是多参考图指令编辑模型。闭源第一梯队（GPT Image 2/2.5、Nano Banana Pro/2/2.1、Seedream 5.0 Pro、Qwen-Image 3.0 Pro）在编辑榜上只差个位数 Elo，因此选型要看三件事：能否在中国大陆调用、参考图上限、单价。国内可直连的只有字节火山方舟（Seedream）和阿里云百炼（Qwen），可无额外授权商用自托管的只有 Apache 2.0 的 Qwen-Image-Edit-2511 和 FLUX.2 [klein] 4B。主流模型都不原生输出透明背景，需要接一个二次元专用抠图模型。胸像和头像可以在保留原脸像素的前提下零成本重新裁切，技能特写可以逐张重新编辑。大招视频按 API 价每 10 秒约 1.5–5.7 美元，不适合按玩家实时生成。市场上还没有头部游戏让 AI 生成的服装成为可在主世界穿着的正式时装。玩家能接受“自己拿 AI 玩”，但对“官方卖 AI 货”反感强烈，偶像和二次元受众尤其如此。在中国上线前，这个功能必须先在属地网信办完成登记，并实现显式加隐式双重 AI 标识。在欧盟，新功能没有 Art. 50 的宽限期。推荐的 MVP 是：以官方服装版型为骨架，AI 在服装蒙版内改款、改色、改图案，只出静态立绘与自动裁切的头像，默认仅自己可见，不开放上传参考图，不做视频。

## 第一梯队模型差距不到 10 Elo，国内可用性和许可决定选型

公开编辑榜的头部几乎打平。2026 年 9 月的一份编辑榜抓取显示，GPT Image 2 (high) 1257、Nano Banana 2 1251、Qwen-Image-3.0-Pro 1249、Nano Banana Pro 1247、Seedream 5.0 Pro 1247，开源的 Qwen Image Edit 2511 为 1165，排第 35 ([benchmarklist](https://benchmarklist.com/arenas/artificial_analysis_image_editing/))。10 月的另一份快照把 Nano Banana Pro 排在第一，排名随榜单和日期变化 ([Magic Hour](https://magichour.ai/model-leaderboard/image-editing))。更重要的是，这些榜单和官方示例几乎都基于照片。对“紫黑青粉朋克偶像”这种特定二次元画风，没有任何公开的保真度基准，所以排行榜只能用来圈出候选，最终选择必须用项目自己的角色盲测决定。

把对本项目真正重要的维度放在一起比较，差异就清楚了（价格多为第三方转述，标注“单一来源”的条目需在上线前核实）：

| 模型 / 服务 | 参考图上限 | 原生透明 | 速度 | 单价（约 1K） | 中国大陆 | 许可 / 部署 |
|---|---|---|---|---|---|---|
| Nano Banana 2.1（Gemini） | 14 | 否 | NB2 实测约 13 s | $0.0336，Batch $0.0168（单一来源） | 不可用 | 闭源 API |
| Nano Banana Pro | 14，可保持 5 个主体 | 否 | — | $0.134（4K $0.24） | 不可用 | 闭源 API |
| GPT Image 2 / 2.5 | 4 + mask | 否（仅 gpt-image-1.5 支持） | 约 15 s | 2.5 medium 约 $0.014；2 medium $0.053 | 本轮未核实 | 闭源 API |
| Seedream 5.0 Pro（火山方舟 / BytePlus） | 10（Lite/Flash 14） | 否 | Lite 约 27 s | 国内 0.3 元；海外 $0.045 | 可直连 | 闭源 API |
| Qwen-Image 3.0 Pro | — | 否 | fal 平均 43 s | $0.04 | 百炼 | 闭源，无开源权重 |
| Qwen-Image-Edit-2511 | 多图输入 | 否 | 4 步 LoRA 在 A100 上约 5 s | 自托管约 $0.007；百炼 0.3 元 | 可自托管或用百炼 | Apache 2.0 |
| FLUX.2 [klein] 4B / [max] | 多参考 | 否 | [max] 约 28 s | $0.014 / $0.07 | 本轮未核实 | klein 4B Apache 2.0；klein 9B、dev 商用需授权 |

表中数据来源：Nano Banana 与 GPT Image 2.5 的价格和延迟 ([Teamday](https://www.teamday.ai/blog/best-ai-image-models-2026))；Nano Banana Pro 的价格 ([simplemetrics](https://simplemetrics.xyz/how-much-per-image-is-nano-banana-and-nano-banana-pro/))；14 张参考、保持 5 个主体 ([Higgsfield](https://higgsfield.ai/creator-hub/help-center/ai-models/how-do-i-use-nano-banana))；GPT Image 2 定价与不支持透明 ([apiyi](https://docs.apiyi.com/en/api-capabilities/gpt-image-2/overview))；Seedream 国内价 ([heyuan110](https://www.heyuan110.com/zh/posts/ai/2026-07-09-seedream-5-pro/)) 与海外价 ([BytePlus](https://www.byteplus.com/zh-CN/product/Seedream))；百炼 qwen-image-edit 0.3 元/张 ([阿里云](https://help.aliyun.com/en/model-studio/model-qwen-image-edit))；Qwen-Edit 自托管 ([Replicate](https://replicate.com/usamaehsan/qwen-image-edit-fastest))；FLUX.2 定价 ([BFL](https://help.bfl.ai/articles/7686530342-flux-2-pricing-guide)) 与许可 ([BFL 帮助中心](https://help.bfl.ai/articles/7986977817-what-are-the-costs-associated-with-using-your-models))。

表里有三个直接影响架构的事实。第一，**Gemini API 不对中国大陆、香港、澳门开放**，调用会返回“User location is not supported” ([yingtu.ai](https://yingtu.ai/zh/blog/gemini-api-region-availability))，所以双区部署几乎是必然：国内走火山方舟或百炼，海外走 Nano Banana 或 GPT Image。第二，**模型更替极快**：Nano Banana 2 在 2026-10-06 已被标记为 deprecated ([Teamday](https://www.teamday.ai/blog/best-ai-image-models-2026))，Qwen-Image 2.1 换成了需要另行商用授权的 Qwen Research License ([datanorth](https://datanorth.ai/news/qwen-releases-qwen-image-2-1))。后端必须有一个模型路由层，不能把某个模型写死进玩法。第三，Midjourney V8.2 的编辑能力很强，但**没有公开 API** ([Teamday](https://www.teamday.ai/blog/best-ai-image-models-2026))，不能作为游戏后端。

专用虚拟试穿模型（IDM-VTON、CatVTON、OOTDiffusion 等）不适合本项目。它们假设输入是正对镜头的真人全身照，服装图是平铺或模特商品照 ([Medium](https://medium.com/@anmjawad007/virtual-try-on-in-2026-how-catvton-and-idm-vton-work-what-i-learned-on-a-macbook-m1-pro-and-how-6428f460fcba))。对夸张比例、平涂上色的二次元立绘属于分布外输入，还会把画风推向写实。本轮没有找到任何它们在动漫角色上可用的证据。正确的“服装 Y”输入方式，是给多参考编辑模型喂一张二次元风格的服装设定图。

透明背景是项目规范的硬约束：项目要求保留原生 RGBA，并拒绝把棋盘格或纯色背景当作透明。2026 年的主流编辑模型都不原生输出 alpha，GPT Image 2 只接受 auto 和 opaque 两种背景 ([picassoia](https://blog.picassoia.com/gpt-image-2-api-pricing-cost-per-image-parameters-and-transparent-pngs))。可行管线是“生成时锁定纯色底，再用二次元专用抠图”。首选 ToonOut：它在 1,228 张动漫图上微调 BiRefNet，把动漫像素准确率从 95.3% 提升到 99.5%，但作者也承认结果常不理想 ([arXiv 2509.06839](https://arxiv.org/html/2509.06839v1))。rembg 的 isnet-anime 可作备选，边缘有轻微雾化 ([GIGAZINE](https://gigazine.net/gsc_news/en/20260510-rembg/))。本项目的配色以青、粉为主，抠图底色要避开纯青和品红。抠图后还要自动校验：alpha 不能全部是 255，边缘半透明像素的比例要正常。

## 锁定画布与面部像素，比任何“一致性模型”都可靠

角色一致性的核心不是换哪个模型，而是如何约束同一个模型。一篇在 RTX 5090 上用 Qwen-Image 2.1 给同一张脸换 8 套衣服的实测给出了可复制的配方 ([ai-muninn](https://ai-muninn.com/en/blog/qwen-image-21-same-face-8-outfits-rtx-5090))。第一步，在提示词里逐项列出不变项（脸、痣、发型和发饰、姿势、背景、光照），然后只说一个改动。第二步，加一句“保持与 image1 完全相同的取景、裁切和机位距离”，否则外套和西装类改动会把半身变成全身。第三步，让潜空间画布尺寸与原图一致，**画布不一致时眼距偏差达 ±18.9%，修正后降到 ±3.9%**。第四步，服装描述里不要提鞋子，以免取景被拉宽。即便如此，**23 次编辑里仍有约 7 次需要重抽**，模型还会为了贴合衣服轻微改动姿势；在 40 步下，768×1024 约 5.5 s，1536×2048 约 22.5 s。FLUX Kontext 的指南给出了同样的方向：一次只改一个变量；姿势必须精确时，与其加长提示词，不如改用 ControlNet ([eastondev](https://eastondev.com/blog/en/posts/ai/20260821-comfyui-flux-kontext-character-consistency/))。

这个 30% 的重抽率对本项目意味着：光靠模型自己“保持身份”不够，因为胸像和头像依赖面部锚点裁切，脸只要漂移几个像素，裁切就会失效。推荐做法是**编辑后再做一次蒙版回贴**：用服装和身体蒙版只把改动区域合成回原立绘，脸、手和外轮廓保留原像素。这样，现有的面部锚点裁切框不用重新检测就能继续使用，AI 瑕疵也被限制在服装区域内。这是根据上述实测得出的工程推论，需在 Phase 0 验证。

逐角色 LoRA 是第二道防线，不宜作为起点。动漫角色 LoRA 的数据量不大，常见为 35–42 张 ([HF 数据集示例](https://huggingface.co/datasets/abbuibuibui/kaguya-Dataset))，FLUX 指南建议 15–30 张 ([ThinkDiffusion](https://learn.thinkdiffusion.com/make-your-character-style-lora-stand-out-easy-lora-training-with-fluxgym/))。但 64 个角色意味着要训练、版本化并部署 64 个模型，这只在自托管开源模型时才划算，闭源 API 也不接受 ControlNet 或 LoRA。LoRA 的真正价值在“换发型”这类大幅改动时稳定画风。IP-Adapter、InstantID、PuLID 这类 2023–2024 年的适配器主要面向写实人脸，在 2026 年的动漫场景中已基本被原生多参考编辑取代（基于既有知识，本轮未复核）。

“纸娃娃”分层是更长远的结构性方案。See-through（arXiv 2602.03749）是一个专门针对动漫的单图分层工具。它用约 9,102 个商业 Live2D 模型训练，能把一张立绘拆成 19 类身体部件，输出最多 23 层、遮挡区已补全的 PSD；在 RTX 4090 上每张约需 74 s 加 10 s 深度估计。作者和美术测试者都指出了它的不足：子层边界不稳定，细排线被抹平，发梢需要人工检查，纹理有“AI 感” ([arXiv](https://arxiv.org/html/2602.03749v1))。Apache 2.0 的 Qwen-Image-Layered 能把任意图像递归分解为可变数量的 RGBA 图层 ([GitHub](https://github.com/QwenLM/Qwen-Image-Layered))，但没有动漫测试。两者都只能做“自动 PSD 草稿”，不能一键产出生产级图层 ([lilting.ch](https://lilting.ch/en/articles/see-through-anime-layer-decomposition))。可行的用法是：用 See-through 为 64 张官方立绘各做一次分层，由美术清理成“身体、脸、头发、服装”的基础 PSD；之后每次 AI 换装都在完整人物上生成，再只取服装层回贴。服装不要脱离身体单独生成，否则领口、袖口和手部会对不上。

发型和妆容要和服装分开对待。换装可以做到脸部像素完全不变，但发型会在脸前后两侧都与脸重叠，妆容则直接改动面部像素。这两类编辑会让面部锚点裁切重新检测，也会把“身份漂移”和“低龄化或性化漂移”的风险直接引入脸部。因此它们应该晚于服装上线，并采用更严格的自动相似度校验。

## 静态衍生素材可以自动跟随，大招视频不行

四类素材的同步难度相差两个数量级。全身立绘是源头。**胸像和头像**几乎零成本：只要保持原画布和原脸像素，现有裁切框直接复用；换发型时需重跑面部检测，并检查发型轮廓在裁切边缘是否被截断。**技能特写**用同一个编辑模型重新生成：以现有特写为 image1、新全身立绘为 image2，指令为“套用 image2 的服装，其余不变”。每张都按图像模型的单价和秒级延迟计费，重抽率预计与立绘相近。

**大招视频**是成本和一致性的断点。按 2026-10 的聚合报价，Kling 4.0 在 720p 约 $0.157/s，1080p 约 $0.235/s；Seedance 2.5 在 BytePlus 约 $0.231–0.569/s ([toolcolumn](https://www.toolcolumn.com/learn/kling-4-vs-seedance-2-5))，不同平台对 Seedance 2.5 的报价从约 $0.134/s 到 $0.473/s 不等 ([Atlas Cloud](https://ask.atlascloud.ai/seedance-2-5-image-to-video-api-provider-pricing)、[fal.ai](https://fal.ai/models/bytedance/seedance-2.5/image-to-video))。一段 10 秒大招因此要 **1.5–5.7 美元**，还没算重抽。自托管的 Wan 2.2 TI2V-5B 在 RTX 4090 上生成 5 秒 720p 约需 9 分钟 ([evetech](https://evezone.evetech.co.za/daily-drop/wan-2-2-hits-local-rigs-720p-video-on-24gb-of-vram))；加 4 步 Lightning LoRA 后，640×640、81 帧可从约 524 s 降到约 138 s ([ComfyUI 文档](https://docs.comfy.org/tutorials/video/wan/wan2-2-fun-inp))，仍然远谈不上实时。多镜头视频里的身份和服装一致性，目前也没有任何量化数据。合理的分工是：官方服装的大招视频由团队用首帧（及尾帧）图生视频一次性制作并质检；玩家的 AI 服装则让大招视频与服装无关，只保留特效、剪影和不变的面部特写，或把新静态立绘做成 2.5D 视差镜头插入。

要让换装后的角色真正“动起来”，现有骨骼动画方案有天然限制。Spine 有成熟的运行时皮肤混搭 API，可以在代码中组合多套皮肤的附件，AttachmentTools 还能用运行时 Sprite 构造附件 ([spine-unity Mix and Match](https://en.esotericsoftware.com/spine-unity-mix-and-match)、[AttachmentTools](https://esotericsoftware.com/spine-unity-attachments-and-attachmenttools))。Live2D Cubism 5.1 能用 AI 读取 PSD，自动生成人形变形器和头发、饰品摆动 ([Live2D 官方](https://www.live2d.com/en/?p=4424))。但目前没有任何主流工具能把一张图变成完整的 .moc3。Bunraku（arXiv 2607.27348）只是研究原型 ([papers.cool](https://papers.cool/arxiv/2607.27348))，img2rig 明确不输出 Live2D 格式 ([GitHub](https://github.com/SSnemo/img2rig))。关键在于：固定骨骼只能接受与原部件网格一一对应的服装，从短裙换成长风衣这种轮廓变化需要新的网格和权重。因此，若将来引入动态立绘，应采用混合方案：动态场景只用少量与骨骼兼容的官方版型，AI 在版型蒙版内改色、改纹理、改款；轮廓自由的 AI 服装只用于静态立绘。

3D（VRoid/VRM 加卡通渲染）在结构上最适合模块化换装，因为一套网格自动覆盖所有姿势和镜头。VRoid Studio 约在 2025 年 11 月推出了换装 beta ([MoguraVR](https://www.moguravr.com/?p=259783))。但 3D 方案存在穿模问题和着色器绑定 ([BOOTH vear Dresser](https://booth.pm/ko/items/2667352))，还意味着 64 个角色全部重做，并放弃项目规范保护的手绘 2D 观感。只有当产品转向实时 3D 舞台演出时，才值得重新评估。

## 市场只验证了“沙盒 AI”，偶像受众对“官方卖 AI 货”零容忍

已上线案例的共同点是：AI 外观被隔离在旁路场景中。逆水寒手游的“剧组模式”能按文字描述生成 3D 服饰并自动挂到身体上，但这些衣服是“剧组站特供版”，**不能穿到大世界**，精度也不及正式时装；玩家把它当作“无限时装生成器”在小红书和抖音上传播 ([17173](https://news.17173.com/content/03162026/161653420.shtml))。逆水寒另有不依赖 AI 的“烫画 DIY”半自定义：在指定时装的胸口、背部等位置贴图案，付费图案多为 1–9 元，买断后每次使用还要消耗“轻容染” ([IT之家](https://game.ithome.com/archiver/811/219.htm))。inZOI 让 AI 只生成可贴到衣服上的纹理 ([PCGamesN](https://www.pcgamesn.com/inzoi/generative-ai))。ZEPETO 的 AI 服装比赛同样是在官方服装模板上生成纹理 ([byline.network](https://byline.network/2023/07/31-162/))。蛋仔派对的 AI 3D 生成服务于地图工坊 ([新浪科技](https://finance.sina.com.cn/tech/roll/2025-09-23/doc-infrnzqh8950930.shtml))。Roblox 用户已用 Cube 3D 生成超过 180 万个物体 ([TechCrunch](https://techcrunch.com/2025/03/17/roblox-releases-its-open-source-model-that-can-create-3d-objects-using-ai/))，但服装自动适配仍在路线图上。本轮**没有找到任何偶像、VTuber 或换装类头部游戏让 AI 服装成为正式可穿时装**，无限暖暖和闪耀暖暖也没有。本项目要做的，是市场上还没人验证过的形态。

玩家反馈有一条清晰的分界线。玩家把自己动手的 AI 工具当玩具接受，但对官方用 AI 产出付费内容的反感极其强烈。第五人格在 2026 年 4 月发布的时装海报被指有 AI 痕迹（四根手指、鞋子变形）。玩家发起“停氪日”，导火索之一是官方同人比赛禁止 AI 参赛，官方自己却疑似使用 AI，宣传账号还被指大规模删评 ([虎嗅](https://www.huxiu.com/article/4853767.html))。Battlefield 6 的付费贴纸被斥为“I didn't pay for AI garbage”，EA 最终只承认“未达质量标准” ([Massively OP](https://massivelyop.com/2026/10/21/battlefield-6-admits-its-latest-cosmetics-missed-a-quality-bar-but-wont-fess-up-to-suspected-gen-ai-use/))。偶像大师闪耀色彩一张演唱会主视觉被质疑是 AI 生成，相关检验帖浏览量超过 386 万 ([coki.jp](https://coki.jp/?p=94045))。角色向营收对信任破裂极度敏感：恋与深空在 2026 年 6 月公布并在 8 天后取消新男主，据 Sensor Tower，月营收从约 3800 万美元跌到刚过 1000 万美元 ([The Next Web](https://thenextweb.com/news/love-and-deepspace-valko-cancelled-revenue-collapse-74-percent))。这件事与 AI 无关，因果也未被证实，但它说明女团养成这类受众一旦觉得“被糊弄”，代价有多大。

由此得出两条产品原则。其一，AI 结果必须在视觉标识和获取渠道上与官方内容隔离，统一标为“玩家创作 / AI 生成”，官方付费时装坚持人工设计并公开声明。其二，规则口径必须一致：如果举办玩家造型比赛，就分设 AI 赛道和手绘赛道，获奖作品由官方美术人工重绘后实装并署名，而不是直接把 AI 原图上架售卖。这也意味着，用户设想的“AI 为核心、官方为辅”需要重新理解：AI 可以是玩法的核心引擎，但它的产出不能进入核心付费池。

变现上，公开资料里**没有头部游戏对单次 AI 换装直接收费**。逆水寒的做法是让 AI 功能免费，对“增量能力”收费：剧组视频免费上传上限 30 秒，购买特质后提升到 45 或 60 秒 ([IT之家/腾讯新闻](https://news.qq.com/rain/a/20251203A083ZF00))。Roblox CEO 表示实时 AI 渲染“不会免费”，会走订阅或按次付费，以覆盖推理成本 ([HTX 转述](https://www.htx.com/news/ai-inference-bills-soar-shopify-and-roblox-warn-savings-from-1EZMXLnf/))。AI 原生产品的变现已被验证：MiniMax 2025 年 AI 原生产品营收 5310 万美元，同比增长 143% ([MiniMax](https://www.minimax.io/news/minimax-global-announces-full-year-2025-financial-results))，但它靠的是订阅加抽卡卡牌，而不是按图收费。

## 每套 AI 造型约 0.03–1.2 元，视频会把成本放大百倍

下表按“每套被玩家采纳的造型”估算单位成本，这是本报告的推算，不是厂商披露。假设每次出 2 张候选，加上约 30% 重抽（依据上文 7/23 的实测），约合 2.6 次全身编辑；再加 1 张技能特写重编辑（含重抽约 1.3 次），合计约 4 次编辑调用。胸像和头像为裁切，成本为零；抠图假设自托管，成本忽略；内容审核单价本轮未查到，未计入。

| 路线 | 单次编辑价 | 每套造型约成本 | 说明 |
|---|---|---|---|
| 国内：Seedream 5.0 Pro 1K / 百炼 qwen-image-edit | 0.3 元 | 约 1.2 元 | 中国大陆唯一可直连的闭源路线 |
| 海外：Nano Banana 2.1 1K | $0.0336 | 约 $0.13 | Batch 可减半，但不适合交互 |
| 海外：GPT Image 2.5 medium | 约 $0.014 | 约 $0.06 | 只有 4 张参考；GPT Image 2 的输入参考图按最高质量计费 |
| 海外：Nano Banana Pro | $0.134 | 约 $0.54 | 只适合官方制作线 |
| 自托管 Qwen-Image-Edit-2511 + Lightning | 约 $0.007 | 约 $0.03 + 运维 | 冷启动 60–90 s，画质低于头部约 80 Elo |
| 大招视频 10 s（Kling 4 / Seedance 2.5） | $0.15–0.57/s | 每角色每套 $1.5–5.7 | 不建议对玩家开放 |

单价出处见第一节表格；GPT Image 2 参考图计费 ([OpenAI 社区](https://community.openai.com/t/openai-must-document-the-input-image-pricing-of-gpt-image-2-so-i-did/1382940))；自托管冷启动 ([Replicate](https://replicate.com/smkrv/qwen-image/readme))。

按这个口径推算：1 万 DAU，30% 玩家每天领取 1 次免费造型，即每天 3,000 套。全走国内 API 约 3,600 元/天（约 11 万元/月）。改为自托管，每天约需 3,000 × 4 × 5 s ≈ 17 个 GPU 小时，一张常驻 H100 按 $5.49/h 计约 $130/天。只看 GPU 账，日产量超过约 1,000 套时自托管开始更便宜，但还要加上工程运维人力，以及 Apache 2.0 开源模型约 80 Elo 的画质差距。因此合理的节奏是先用 API 验证需求，量上来之后把“改色、改图案”这类简单任务迁到自托管，复杂改款继续走闭源 API。免费次数必须严格封顶，每日 1–3 次。超出部分用付费点数或会员覆盖，付费点放在额外次数、高清导出、更多保存栏位和动态视差立绘上，绝不替代官方抽卡时装。

## 中国登记、双重标识和三大商店规则决定上线顺序

在中国，**接入已备案的大模型并不能让游戏免责**。网信办 2026-01-12 的公告明确：通过 API 调用已备案模型的应用或功能，由地方网信办开展登记，并须在显著位置公示所用模型名称及备案号或上线编号 ([澎湃](https://www.thepaper.cn/newsdetail_forward_32368980))。截至 2025 年底，全国已有 748 个生成式 AI 服务完成备案、435 个应用或功能完成登记，说明这条路走得通，但登记材料和时限需向所在省份的网信办确认。《生成式人工智能服务管理暂行办法》第 22 条把“通过可编程接口等方式提供服务”的组织也认定为提供者，因此游戏要承担一整套义务：第 9 条的网络信息内容生产者责任和用户服务协议、第 10 条的防未成年人沉迷、第 11 条的个人信息最小化、第 14 条的违法内容处置与上报、第 15 条的投诉通道 ([网信办原文](https://www.cac.gov.cn/2023-07/13/c_1690898327029107.htm))。2025-09-01 起施行的《人工智能生成合成内容标识办法》要求图片带显著的显式标识，下载和导出后仍须保留；文件元数据中要写入隐式标识，包含服务提供者名称或编码、内容编号等；如果允许玩家分享，游戏还要作为传播平台检测隐式标识；应用商店上架时会核验标识材料；若提供无标识输出，须留存日志至少 6 个月 ([网信办原文](https://www.cac.gov.cn/2025-03/14/c_1743654684782215.htm))。配套的强制性国家标准 GB 45438-2025 同日生效 ([PwC](https://www.pwccn.com/zh/tmt/method-identifying-synthetic-content-generated-ai-sep2025.pdf))。以下问题本轮未能确认：《深度合成管理规定》的实名要求原文、是否还需另做深度合成算法备案、新增 AIGC 系统是否触发版号变更。上线前必须分别向网信办和出版单位确认，其中版号问题不确定性最高。

海外商店和平台的要求与中国在方向上一致，细节不同。Apple 1.2 已把“AI content creation apps”明确纳入 UGC，要求过滤、举报、拉黑和公开联系方式；1.2.1(a) 要求创作类应用按已验证或自报的年龄限制超出分级的内容；5.1.2(i) 要求向第三方 AI 发送个人数据前取得明确同意 ([Apple 审核指南](https://developer.apple.com/app-store/review/guidelines/))。新的 13+/16+/18+ 分级问卷须在 2026-01-31 前作答 ([Apple Developer News](https://developer.apple.com/news/?id=ks775ehf))。Google Play 要求玩家能**在应用内**举报 AI 生成内容，并禁止以性满足为主要目的的生成式 AI 应用 ([Play Console 帮助](https://support.google.com/googleplay/android-developer/answer/13985936?hl=en)、[AI 内容政策](https://support.google.com/googleplay/android-developer/answer/14094294))。Steam 在 2026 年 1 月改写了 AI 披露问卷：实时生成内容须说明防止违法输出的护栏，且不允许实时生成成人性内容 ([tbreak](https://tbreak.com/steam-ai-disclosure-policy-update-developers/))。欧盟 AI Act 第 50 条自 2026-08-02 起适用，机器可读标记的宽限期延至 2026-12-02，**但只适用于此前已上市的系统，新功能没有宽限期** ([CSA](https://labs.cloudsecurityalliance.org/research/csa-research-note-eu-ai-act-article50-watermarking-deadline/))。好在一套实现可以同时满足中欧两边：可见的“AI 生成 / AI-generated”角标，加上元数据，C2PA 清单与国标字段能否共存于同一文件则需要实测。

内容安全的最大风险来自题材本身。偶像舞台装本来就处在“性感”边缘，海外闭源模型的拦截发生在输出侧。Gemini 的 IMAGE_SAFETY 会在输入通过后拦下生成结果，调低文本安全阈值通常无效 ([yingtu.ai](https://yingtu.ai/en/blog/gemini-refusing-harmless-images))，因此必须为“被拦截”设计友好回退，例如退还次数或推荐相近服装。防线应当分层：用结构化的服装构建器代替自由长文本；屏蔽性暗示、低龄、真人姓名和他家 IP 品牌词；在服务端注入固定的安全提示词，客户端永不直连模型；以原成人立绘为底只改服装区域，从结构上锁住年龄观感；输出再经独立审核模型检查。国内可用阿里云内容安全的 AIGC 场景检测，它还能识别“疑似侵权的元素” ([阿里云](https://www.alibabacloud.com/help/zh/content-moderation/latest/image-audit-enhanced-edition-detects-aigc-infringement))。分享默认关闭；公开分享走更严的阈值并做人工抽检。各家审核 API 的单价和延迟本轮未取得，需要询价。

版权上，北京互联网法院 2023 年认定，用户通过提示词、参数和筛选体现独创性时，可以成为 AI 图片的作者 ([澎湃](https://www.thepaper.cn/newsDetail_forward_25725671))；美国则维持人类作者要求，最高法院已于 2026-03-02 驳回 Thaler 案的调卷申请 ([SCOTUSblog](https://www.scotusblog.com/cases/case-files/thaler-v-perlmutter))。对游戏而言，更现实的风险是 AI 输出抄袭他家 IP、品牌或真人。用户协议应约定：角色及一切游戏内资产归运营方；玩家若对造型享有任何权利，须授予运营方全球、永久、免费、可再许可的使用权；平台不保证输出可受版权保护或独一无二。上传参考图是风险最高的入口，同时涉及他家 IP、真人肖像和个人信息，MVP 阶段应直接关闭。另需注意，国内模型服务海外玩家涉及跨境数据，本轮未研究，这也支持“国内模型服务国内、海外模型服务海外”的分区架构。

## 分三步落地：先改服装区域，再开放发型与分享，视频留给官方

**Phase 0：用 3–5 个角色做技术盲测，先回答“能不能用”。** 仓库所在环境已装有 LightAI（Nano Banana Pro/2、可灵、即梦）和火山方舟（Seedream/Seedance）的调用技能，可以低成本先跑闭源路线；再并行搭一套 ComfyUI + Qwen-Image-Edit-2511 作为对照。测试集应覆盖本项目最难的情况：青粉高饱和配色、朋克配饰、短裙露脐类舞台装、长发遮挡。用自动指标打分：回贴后面部像素差、眼距偏差、裁切框是否仍然有效、alpha 边缘半透明像素比例、重抽率、拦截率、P50 和 P95 延迟、单套成本；再加美术盲评画风保真度。这些数字目前没有任何公开基准，必须自测。

**Phase 1 MVP：“官方版型 + AI 改款”的静态换装。** 做什么：每个角色先由美术用 See-through 草稿清理出基础 PSD 和服装蒙版；玩家从官方服装版型库中选择一款，用结构化标签（颜色、材质、图案、风格）加短文本提出修改；服务端经模型路由层调用国内或海外模型，按固定画布生成，蒙版回贴，ToonOut 抠图，alpha 校验，输出审核，最后打上显式角标和隐式元数据；产出全身立绘，并用现有面部锚点框自动裁出胸像和头像，在成员详情、相册、应援卡等旁路场景展示。不做什么：首页角色继续只用官方原画，这是项目已有的规范；不开放上传参考图；不改发型和妆容（最多改发色）；不重新生成技能特写和大招视频（战斗内仍用官方素材，或只替换静态立绘）；不开放公开分享；不让 AI 结果进入付费池；不接 VTON 模型；不提供无标识导出；不改 Live2D、Spine 或 3D。中国区要先完成属地网信办登记和模型备案号公示，再开放 AI 功能。在拿到登记前，可以先在海外区或以纯官方版型的形态上线。

**Phase 2：在 Phase 1 指标达标后扩展自由度。** 依次开放：轮廓可变的自由改款；发型和妆容（需更严格的面部相似度阈值和重新裁切）；技能特写自动重编辑；默认私有、可选公开的造型相册，配套投票、举报和拉黑；付费点数和会员。按量把简单任务迁到自托管的 Apache 2.0 模型；必要时为画风漂移严重的角色补训 LoRA。

**Phase 3：把玩家创意回流到官方内容。** 举办分赛道的造型季赛，获奖方案由官方美术人工重绘，并配套制作大招视频后实装、署名。玩家 AI 服装在战斗演出中使用 2.5D 视差立绘。如果将来引入动态立绘，只为少量与骨骼兼容的官方版型做 Spine 皮肤，AI 在版型蒙版内改纹理。

贯穿三个阶段、需要持续验证的风险有五项：二次元朋克画风的保真度和 30% 级别的重抽率能否压到可接受水平；偶像舞台装在两区模型上的误拦截率；属地登记和版号的实际要求与时长；玩家是否会把“AI 生成”标签理解为“官方在偷工”，这需要上线前准备好 AI 使用披露口径，避免第五人格式的沉默和删评；免费次数封顶后，单个 DAU 的成本是否落在可承受范围。

## 结论

这项研究带来的最大认知转变是：真正的瓶颈不是“哪个模型最聪明”。头部模型已经难分高下，真正的瓶颈是**下游素材系统能否接住 AI 的不确定性**。一张不保留原脸像素的新立绘，会连带让头像裁切、特写和视频全部失效；而一张只改服装区域的新立绘，几乎可以免费复用整条现有素材链。所以，项目最值得提前投入的不是模型接入，而是为 64 个角色准备分层 PSD 和服装蒙版。这项资产与模型无关，模型每半年换一代，它依然有效。

第二个启示是，“AI 为核心”应该理解为 AI 是玩家的创作工具和社交话题，而不是官方内容的生产线。在偶像受众里，信任是核心资产，玩家花钱买的是“官方用心”。把 AI 关在“玩家创作”区，再用比赛加人工重绘的方式把优秀创意收编为官方内容，就能同时拿到 UGC 的传播力和人工美术的信任溢价。这条路目前还没有头部游戏走通，既是风险，也是先发机会。
