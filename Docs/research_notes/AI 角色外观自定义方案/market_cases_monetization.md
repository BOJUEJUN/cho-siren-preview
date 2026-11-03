# 市场案例：AI/深度 UGC 角色外观自定义的做法、玩家反应与变现（2024-2026）

> 调研日期：2026-11-03。每条事实后标注事实发生/报道日期。未找到可靠来源的内容一律放入「Gaps」，没有编造数字。
> 适用背景：Unity 女团养成游戏，计划以「AI 生成（例如“把这个角色换成 X 服装”）」为主、官方设计为辅的外观自定义功能。

## 问题 1：哪些已上线的游戏/应用做了 AI 外观或服装生成，具体怎么做？

### 要点
把 AI 外观生成做进成熟商业游戏的公开案例，几乎都把它限制在**沙盒或旁路场景**：逆水寒的「剧组模式」、蛋仔派对的地图工坊、Roblox 的创作者工具、inZOI 的纹理贴图。生成结果通常**不能直接当作可在主世界展示的正式时装**。可以随意穿出去的正式时装，仍然由官方设计，或者走「官方模板 + 玩家贴图/图案 DIY」的半自定义路线。

### 引用发现

**中国**
- **逆水寒手游（网易雷火）·剧组模式 AI 生成服饰**（报道日期 2026-03-16）：玩家在剧组模式的模型栏里输入描述（例如“黄色碎花长裙，不要人物建模，只要裙子”），系统生成 3D 物件，自动挂接到身体部位，并可微调位置和比例。这些衣服属于“剧组站特供版”，**不能穿到大世界里展示**；文章也承认精度比不上正式时装，只够拍片、做梗。玩家在小红书、抖音上把它当作“无限时装生成器”传播。—— [17173](https://news.17173.com/content/03162026/161653420.shtml)
- 剧组模式本身是 2024-02 发布的 AI 影片生成工具，官方称“让创作门槛接近于 0”，可以调整服装和妆造。—— [腾讯新闻](https://news.qq.com/rain/a/20240226A04G0N00)；[IT之家](https://www.ithome.com/0/752/061.htm)
- **逆水寒手游·AI 捏脸**：支持照片捏脸和文字捏脸，后来增加了交互式调整（打字或语音微调）；网易伏羲称照片捏脸项目 2018 年就已立项。2025-12 的 3.2.2 版本新增“声音捏脸”，用玩家音色生成面部参数。—— [腾讯新闻/IT之家 2025-12-03](https://news.qq.com/rain/a/20251203A083ZF00)；[小米游戏中心/网易伏羲分享](https://game.xiaomi.com/viewpoint/1393220466_1766644665311_100)
- **逆水寒手游·烫画 DIY（非 AI 的半自定义时装）**（2.2.2 版本，2024-11-22 上线）：玩家可以在胸口、背部、袖口、鞋面等位置贴图案，并能和染色叠加。首批支持的时装是「纯白心绪」（用绑玉获取）和「乱律音阶」。官方表示以后会开放玩家上传自定义烫画素材。—— [IT之家](https://game.ithome.com/archiver/811/219.htm)
- **逆水寒手游·自捏 AI 江湖友人**（2.1.3 版本，2024-08 宣布）：官方称这是“业界首个可以自捏 AI 智能生命体”的游戏，玩家可以自定义 AI NPC 的形象和人设。—— [IT之家](https://www.ithome.com/0/789/859.htm)；[新浪科技](https://finance.sina.com.cn/tech/roll/2024-08-20/doc-inckhwih5342686.shtml)
- **蛋仔派对（网易）·AI 模型生成**（2025-09 中旬上线，报道日期 2025-09-23）：玩家输入文字或上传图片就能生成 3D 模型，可直接用于乐园工坊，主要面向不会建模的地图创作者（工匠蛋）。底层是影眸科技（Hyper3D）的 Rodin，其中专为实时手游设计的 “Rodin Zero” 模式会控制面数；另有 BoundingBox、VoxelControl、PointCloudControl 等可控生成方式。报道没有说明是否覆盖时装或皮肤。—— [新浪科技](https://finance.sina.com.cn/tech/roll/2025-09-23/doc-infrnzqh8950930.shtml)
- 二手转述称，蛋仔官方在 2026 年全球数字经济大会上公布：注册用户超 7 亿，月活超 1 亿，UGC 开发者超 5000 万，累计地图超 2 亿张（未核对官方原文）。—— [知乎](https://zhuanlan.zhihu.com/p/2060800399246877683)
- **元梦之星（腾讯）**：游戏内置 AI 辅助地图编辑，可以自定义道具数值和外观、编辑角色形象；但**没有找到** AI 生成时装或皮肤的官方信息。—— [维基百科](https://zh.wikipedia.org/zh-hans/%E5%85%83%E6%A2%A6%E4%B9%8B%E6%98%9F)

**全球**
- **inZOI（Krafton，2025 年 EA 发售）**：Steam 页面写明 “AI-generated textures can be applied to character clothing and various items”，即玩家可以用文字或图片生成衣服纹理。Krafton 回应称模型是自研的，只用公司自有或无版权问题的数据训练；另有媒体称训练集是 2000 万张 CC 授权图片，两种说法存在冲突。—— [PCGamesN](https://www.pcgamesn.com/inzoi/generative-ai)；[esports.net](https://www.esports.net/news/gaming/inzoi-generative-ai/)
- **Roblox**：2025-03 开源 Cube 3D，以 beta 形式提供文本生成网格。2025-11 起开放 4D 创作早期访问，2026-02 进入公开测试，可生成可交互物体（模板为 Car-5 和 Body-1）。官方称用户已用 Cube 3D 生成超过 180 万个 3D 物体。—— [TechCrunch 2025-03-17](https://techcrunch.com/2025/03/17/roblox-releases-its-open-source-model-that-can-create-3d-objects-using-ai/)；[TechCrunch 2026-02-04](https://techcrunch.com/2026/02/04/robloxs-4d-creation-feature-is-now-available-in-open-beta/)
- Roblox 2024-06 的 4D 路线图明确提到：服装需要自动适配“非为其设计的身体”，Avatar Auto Setup 是早期一步。2025-09 开发者论坛发布 Photo-to-Avatar API alpha（自拍转可绑骨 avatar，不生成服装）。—— [Roblox Newsroom](https://about.roblox.com/newsroom/2024/06/robloxs-road-to-4d-generative-ai)；[DevForum](https://devforum.roblox.com/t/early-preview-alpha-release-of-photo-to-avatar-apis/3931624)
- 第三方网站 Nilo 称 Roblox 在 2026-09 开发者大会上宣布了 “Prompt to Avatar”（含 layered clothing），计划年底上线。**未能用官方来源证实**。—— [Nilo](https://nilo.io/articles/roblox-avatar-generator-ai)
- Roblox 平台上第三方体验 “AI Outfit Creator” 的“生成”只是从现有商城目录里组合单品，并不真正生成新衣服。—— [Nilo](https://nilo.io/articles/roblox-avatar-generator-ai)
- **ZEPETO（NAVER Z，韩国）**：2023-06 推出 AI 头像活动（上传自拍得到 4 种风格化 avatar）；2023-07-31 至 08-13 举办生成式 AI 创作活动“도전! 제페토 디자이너”，在 ZEPETO Studio 里用关键词或指令生成纹理并套到官方服装模板上；同期与 RECON Labs 合办 AI 纹理“Top Designer Contest”。**没有找到** 2025 年以后的新 AI 服装功能。—— [Asiae 2023-06](https://view.asiae.co.kr/en/article/2023060809060509573)；[byline.network 2023-07](https://byline.network/2023/07/31-162/)
- **AI 陪伴类（Talkie / 星野 / 猫箱）**：星野（MiniMax）被描述为“AI 陪聊 + 抽卡”，用卡牌机制提升变现。虎嗅（2026-10）称猫箱月活多数时间在 100-200 万，但单月活 iOS 收入效率约为星野、筑梦岛的 4 倍、即梦 AI 的 5 倍。—— [人人都是产品经理](https://www.woshipm.com/evaluating/6146340.html)；[虎嗅](https://www.huxiu.com/article/4898877.html)

### 推论
- “AI 生成可正式穿着的时装”在头部商业游戏里**还没有成熟先例**。逆水寒把 AI 服饰锁在剧组模式，ZEPETO 和 inZOI 只让 AI 生成**纹理/图案**再套到官方模板上，这两种隔离方式都值得借鉴：用官方 3D 版型（骨骼、物理、布料）做底，AI 只负责贴图或 2D 立绘层，这样质量和穿模风险可控。
- 对 2D 立绘为主的女团养成游戏，最接近的可行形态是：“官方服装版型 + AI 生成图案/配色/2D 换装立绘”，生成结果先放在拍照、相册、应援卡这类旁路场景，而不是直接进入正式时装池。

### 缺口
- 没有找到 Infinity Nikki（无限暖暖）、闪耀暖暖做 AI 外观生成或“玩家设计实装”的官方资料；搜索只看到设计工坊（官方图纸）相关内容。
- 没有找到偶像或 VTuber 题材游戏上线“AI 生成服装”的正式案例（日韩均无）。
- 没有找到原神千星奇域、PUBG 等的 AI 外观功能资料（本次未深挖）。

## 问题 2：玩家反应——兴奋、反感（AI 美术争议、质量、“slop”、画师担忧），以及对留存或营收的影响

### 要点
玩家对**自己动手的 AI 创作工具**（逆水寒剧组、蛋仔生成）大体当作玩具接受。但对**官方用 AI 制作付费外观或宣传美术**的反感非常强烈，在二次元、角色向和偶像向用户里尤其明显：典型反应是停氪、刷评、删评争议。角色向游戏的营收对“信任破裂”极度敏感。

### 引用发现
- **第五人格（网易）AI 海报风波**（2026-04）：04-11 发布「幸运儿」「先知」时装海报，玩家指出 AI 痕迹（四根手指、器物结构错误、鞋子变形）。玩家把 04-23（画家角色艾格·瓦尔登生日）定为“停氪日”，喊出“不道歉就停氪，不为 AI 买单”。另一个导火索是双标：官方同人比赛明令禁止 AI 参赛。报道发布时官方仍未公开回应，宣传账号被指大规模删评。—— [虎嗅 2026-04](https://www.huxiu.com/article/4853767.html)；[gamegeeker](https://gamegeeker.com/zh/news/identity-v-ai-poster-controversy)；[扬子晚报 2026-04-16](https://www.yzwb.net/news/wy/202604/t20260416_342668.html)
- 背景：网易在 2025 年财报中称 AI 原生管线已在美术、策划、编程、动画和 QA 大规模部署，部分环节“效能提升 300%”，这成为玩家质疑“降本增效”的依据。—— [虎嗅](https://www.huxiu.com/article/4853767.html)
- 17173 报道称，网易多款产品在 2026 年接连因 AI 内容引发争议，第五人格 6 月还出现“Coser 照片疑被盗用”事件。—— [17173 2026-06-08](https://news.17173.com/content/06082026/160322729.shtml)
- **Battlefield 6（EA）**（2025-12 至 2026-10）：付费 Windchill 礼包里的贴纸被指是 AI 生成，玩家原话 “I didn't pay for AI garbage”。EA 此前数月曾向玩家保证不会出现这类内容。2026-10 官方承认该批外观“未达到质量标准”，但没有承认使用了 AI。—— [GosuGamers](https://www.gosugamers.net/entertainment/news/77764-battlefield-6-players-slam-ea-over-suspected-ai-generated-cosmetic-i-didn-t-pay-for-ai-garbage)；[Massively OP 2026-10-21](https://massivelyop.com/2026/10/21/battlefield-6-admits-its-latest-cosmetics-missed-a-quality-bar-but-wont-fess-up-to-suspected-gen-ai-use/)；[80.lv](https://80.lv/articles/battlefield-6-gets-slammed-for-adding-ai-generated-cosmetics)
- **Call of Duty: Black Ops 7**（2025-11）：玩家指出名片疑似 AI 生成；Activision 在 Steam 页面披露“使用生成式 AI 工具协助开发部分游戏内资产”。—— [ingamenews](https://ingamenews.com/2025/11/black-ops-7-fans-decry-suspected-ai.html)
- **Crimson Desert（Pearl Abyss）**：开发方承认 AI 资产“无意中”留在正式版里，并补上了 Steam AI 披露；有报道称玩家以此为由申请退款被拒（仅单一来源）。—— [onemoregame.ph](https://onemoregame.ph/?p=145890)；[gamegeeker](https://gamegeeker.com/zh/news/crimson-desert-ai-refund-controversy-on-steam)
- **inZOI**：Krafton 澄清训练数据后，仍有部分玩家对 AI 纹理和 3D 打印功能感到不适。Steam 讨论里有人指出：AI 能生成的图案在常规服装自定义里却没有，等于把玩家推向 AI 工具。—— [PCGamesN](https://www.pcgamesn.com/inzoi/generative-ai)；[Steam 讨论](https://steamcommunity.com/app/2456740/discussions/0/599648251203886537/?ctp=6)
- **偶像/日系相关**：
  - 偶像大师 闪耀色彩（シャニマス）的ノクチル演唱会告知主视觉被指 AI 生成，海外粉丝反应尤其激烈；一条“检测到 OpenAI SynthID 水印”的检验帖浏览量超过 386 万，但检测工具可靠性存疑，报道时官方未承认。—— [coki.jp](https://coki.jp/?p=94045)
  - Juice=Juice 官方 X 用 AI 图片庆生，粉丝意见分化（“以为是粉丝发的，结果是官方”，也有人觉得“本人喜欢就行”）。—— [coki.jp](https://coki.jp/?p=92441)
  - 美少女游戏《あいりすミスティリア!》的节日立绘被误判为 AI 生成，运营不得不出面澄清（2024-10）。—— [Game*Spark](https://www.gamespark.jp/article/2024/10/25/146342.html)
  - 2023 年集英社因 AI 偶像写真集与真人女优相似而停售。—— [OECD AI Incidents](https://oecd.ai/en/incidents/2023-06-07-c3bb)
- **角色向游戏的营收脆弱性（非 AI，但同属“角色形象/设定”信任危机）**：恋与深空（叠纸）2026-06-22 公布新男主 Valko，8 天后取消。根据 Sensor Tower，6 月营收约 3800 万美元，7 月刚过 1000 万美元，降幅接近四分之三，跌到当月二次元抽卡榜第 16 名；因果关系没有被证实。—— [The Next Web](https://thenextweb.com/news/love-and-deepspace-valko-cancelled-revenue-collapse-74-percent)；[TechNode 2026-06-29](https://technode.com/2026/06/29/love-and-deepspaces-new-romanceable-character-draws-heavy-criticism-despite-paper-games-apology/)
- **正面反应**：逆水寒玩家自发把剧组 AI 服饰做成短视频传播，形成话题；但文章同时提到美术风格与预期有落差。—— [17173](https://news.17173.com/content/03162026/161653420.shtml)

### 推论
- 区分两类 AI 至关重要：**“玩家拿 AI 当工具自己玩”**的接受度明显高于**“官方用 AI 产出、玩家付钱买”**。女团养成用户与二次元、偶像粉圈高度重合，对“官方付费立绘或服装是 AI 做的”的容忍度很低；如果把官方付费时装改成 AI 产出，风险很高。
- 合理的做法是：官方付费时装保持人工设计并明确声明；AI 生成功能明确标注为“玩家创作/AI 生成”，与官方内容在视觉标识和获取渠道上做隔离。另外要避免“同人比赛禁 AI、官方却用 AI”这类规则不一致。
- 出现质疑时沉默或删评会放大损失（第五人格、Battlefield 6 都是例子），需要预先准备 AI 使用披露口径。

### 缺口
- 没有找到任何游戏公开 **AI 外观功能对留存或 DAU 的影响数据**。
- 没有找到逆水寒 AI 捏脸或剧组服饰的系统性玩家满意度数据（TapTap 或 NGA 评分拆分），只有媒体叙述。
- 第五人格事件对营收的实际影响没有找到量化数据。

## 问题 3：变现模式——免费次数、付费点数、服装券、官方时装抽卡 vs AI 时装、订阅；常见价位与成本封顶

### 要点
公开资料里，**没有哪款头部游戏直接对“AI 生成时装”按次收费**。常见做法是：AI 生成功能本身免费（用来拉活跃、做传播），变现点放在周边，例如延长时长的特质道具、官方图案素材、染色消耗品、会员。AI 陪伴类应用则用“订阅 + 抽卡/卡牌 + 内购”。平台型产品（Roblox）明确表示高成本实时 AI 会走订阅或按次付费。

### 引用发现
- **逆水寒剧组模式的付费点**（2025-12）：视频上传时长受“闲游特质·开机大吉”限制，没有该特质最多上传 30 秒，购买后可提升到 45 秒或 60 秒；特质在工坊商店或星梦商店购买。这是对 AI 功能做“增量能力付费”的实例。—— [腾讯新闻/IT之家](https://news.qq.com/rain/a/20251203A083ZF00)
- **逆水寒烫画 DIY 定价**（2024-11）：很多图案可通过活动或商城 0 元购获得，也可用绑玉购买；多数付费图案约 1-9 元（可用限定付费货币）；一次购买无限使用，但每次应用要消耗“轻容染”（消耗品）。承载时装「纯白心绪」用绑玉获取。—— [IT之家](https://game.ithome.com/archiver/811/219.htm)
- 逆水寒官方在内测前提出“不卖数值”的理念，营收依赖外观。—— [今日头条](https://www.toutiao.com/zixun/7489335451187300371/)
- **Roblox**（2026-04-30 Q1 财报电话会）：CEO Baszucki 表示实时渲染项目 “Roblox Reality” **不会免费**，会采用订阅或按次付费来抵消实时推理成本；CFO 称全年利润率下调中约四分之一来自新增 AI 投入和 DevEx 调整。（引述来自二手报道。）—— [HTX News 转述](https://www.htx.com/news/ai-inference-bills-soar-shopify-and-roblox-warn-savings-from-1EZMXLnf/)
- Roblox Q3 2025 股东信：AI 相关负载增长会稀释近期降本空间。—— [SEC 8-K](https://www.sec.gov/Archives/edgar/data/1315098/000131509825000326/ex991-q32025shareholderl.htm)
- **ZEPETO**：创作者通过出售虚拟服装赚 ZEM，2022 年报道称每卖出 5000 ZEM 创作者约得 106 美元；第三方应用页称会员包含每月 70 ZEM、作品优先审核和专属物品（未核实是否覆盖 AI）。—— [Asiae 2022-09](https://view.asiae.co.kr/en/article/2022091915001729836)；[mwm.ai](https://mwm.ai/>/apps/zepeto-avatar-connect-live/1350301428)
- **MiniMax（Talkie / 星野）**：2025 年 AI 原生产品营收 5310 万美元（2024 年为 2180 万美元，+143.4%），70% 以上来自海外；Talkie 主要靠订阅和角色相关内购，早期免费层由广告支撑；星野采用抽卡卡牌机制。—— [MiniMax FY2025 财报](https://www.minimax.io/news/minimax-global-announces-full-year-2025-financial-results)；[KrASIA](https://www.kr-asia.com/talkies-global-traction-puts-minimax-in-ais-top-tier-but-risks-loom)
- **猫箱**（2026-08 数据，虎嗅 2026-10）：月活 100-200 万，单月活 iOS 收入效率在同类中领先（约为星野的 4 倍）。—— [虎嗅](https://www.huxiu.com/article/4898877.html)
- **恋与深空的补偿模式**：取消新角色后，发放 30 天每日免费抽卡作为补偿。—— [The Next Web](https://thenextweb.com/news/love-and-deepspace-valko-cancelled-revenue-collapse-74-percent)

### 推论
- 对女团养成游戏，较稳妥的变现结构是：**AI 生成本身每天给少量免费次数，用于拉活跃和社媒传播 → 额外次数、高清导出、保存栏位、动态立绘等增量能力付费 → 官方人工设计时装继续作为抽卡或直售主力**。逆水寒“30 秒免费 / 付费到 60 秒”的模式可以直接借鉴为“基础分辨率或张数免费，高清、多张或动态付费”。
- 让 AI 生成的结果和官方时装在价格层级上互不替代，可以避免“AI 衣服挤占付费时装”和“花钱买 AI 货”这两类负面情绪。

### 缺口
- 没有找到任何头部游戏 **AI 生成时装的按次价格、每日免费次数上限** 的官方资料（逆水寒、蛋仔、inZOI 都没有公开）。
- 没有找到 Talkie、猫箱、星野的捏人或形象生成按次收费标准。
- 没有找到 AI 时装与官方抽卡时装营收对比的任何数据。

## 问题 4：单次生成的算力成本披露与防滥用（限流）

### 要点
没有游戏公司公开**单次外观生成成本**。可用的外部参考是商用图像 API 的公开报价：主流图像编辑或生成模型约为**每张 1024² 图 0.02-0.04 美元**。平台型公司（Roblox）只在公司层面披露“AI 推理成本侵蚀利润”，并倾向向用户转嫁成本。防滥用方面，公开信息集中在内容审核和训练数据合规，而不是具体的限流参数。

### 引用发现
- Gemini 2.5 Flash Image（Nano Banana）：Google 直连约 **0.039 美元/张**（1024×1024，约 1290 个输出 token，报价时间 2026-02）；Replicate 约 0.040 美元，Fal.ai 约 0.020 美元。—— [anotherwrapper](https://anotherwrapper.com/tools/llm-pricing/image-models/gemini-25-flash-image-nano-banana)；[pricepertoken](https://pricepertoken.com/image/model/google-gemini-2-5-flash-image)
- Seedream 5.0 Lite 第三方比价约 0.04 美元/张（Seedream 4.0 的价格未查到）。—— [anotherwrapper 比较页](https://anotherwrapper.com/tools/llm-pricing/image-models/compare/gemini-25-flash-image-nano-banana/seedream-50-lite)
- Roblox：2024-09 介绍混合云 AI 推理架构；二手报道称把部分推理从第三方云迁回自建机房后，特定负载效率提升 10 倍，平台推理调用达每秒 150 万次（后一数字未经 Roblox 一手来源核实）。—— [Roblox Newsroom 2024-09](https://about.roblox.com/newsroom/2024/09/running-ai-inference-at-scale-in-the-hybrid-cloud)；[Synthedia](https://synthedia.substack.com/p/roblox-is-deploying-its-own-generative)
- Roblox 把成本转嫁给用户的表态（订阅或按次付费），见问题 3。—— [HTX News 转述](https://www.htx.com/news/ai-inference-bills-soar-shopify-and-roblox-warn-savings-from-1EZMXLnf/)
- 蛋仔采用 Rodin Zero 低面数模式和可控生成（BoundingBox 等），从生成端限制资产复杂度，相当于控制了下游的渲染和存储成本。—— [新浪科技](https://finance.sina.com.cn/tech/roll/2025-09-23/doc-infrnzqh8950930.shtml)
- inZOI 用“自研模型 + 自有或无版权数据训练”作为合规卖点，回应玩家担忧。—— [PCGamesN](https://www.pcgamesn.com/inzoi/generative-ai)
- 偶像相关滥用风险：日本漫画家用 AI 对 STU48 成员照片做性化加工，被批评后道歉删帖。—— [news.allabout.co.jp](https://news.allabout.co.jp/articles/o/130016/)；[OECD AI Incidents（2023 AI 写真集事件）](https://oecd.ai/en/incidents/2023-06-07-c3bb)

### 推论
- 粗略估算（仅作推论）：按每张约 0.04 美元计，若每位活跃玩家每天免费生成 3 张，**每个 DAU 的日成本约 0.12 美元**（约 0.85 元人民币），对中小体量手游是明显负担。因此免费次数必须严格封顶（例如每日 1-3 次），超出部分用付费点数或会员覆盖，或者用自建、开源模型压低单价。
- 女团题材的特有风险是**性化、擦边、侵权（真人或他家 IP 角色）提示词**。需要提示词过滤、输出审核（尤其在公开分享前）、角色专属的安全约束（禁止的服装或姿态类别），并按账号和设备限频。

### 缺口
- 没有找到任何游戏厂商（网易、腾讯、米哈游、叠纸、Krafton、Roblox）披露的**单次生成成本**或**每日生成限额、限流参数**。
- 没有找到关于 AI 外观功能被滥用（生成违规内容）后游戏方处置的具体公开案例。

## 问题 5：分享与社交——服装分享、投票、比赛，把优秀 UGC 变成官方道具

### 要点
已经验证的社交闭环是：**生成或设计 → 站外短视频传播 → 官方比赛或活动 → 少数作品商品化（ZEPETO 创作者分成）**。“把玩家 AI 作品转成官方付费道具”在头部游戏里没有找到成熟先例；ZEPETO 的“AI 纹理 + 官方模板 + 设计师比赛”是最接近的形态。

### 引用发现
- ZEPETO 2023 年两场 AI 服装创作比赛（NAVER Z 官方活动，以及与 RECON Labs 合办的 Top Designer Contest），都在官方服装模板上用 AI 生成纹理；平台本身允许创作者售卖服装并获得分成。—— [byline.network](https://byline.network/2023/07/31-162/)；[ditoday](https://ditoday.com/?p=76628)；[Asiae 2022-09](https://view.asiae.co.kr/en/article/2022091915001729836)
- 逆水寒剧组 AI 服饰的传播主要靠玩家在小红书和抖音自发分享。—— [17173](https://news.17173.com/content/03162026/161653420.shtml)
- 逆水寒烫画官方计划开放玩家上传自定义素材（2024-11 表态）。—— [IT之家](https://game.ithome.com/archiver/811/219.htm)
- 第五人格同人比赛明确禁止 AI 参赛，与官方疑似使用 AI 形成双标，成为舆论引爆点。—— [gamegeeker](https://gamegeeker.com/zh/news/identity-v-ai-poster-controversy)
- 闪耀暖暖的“设计工坊”是官方图纸制作系统，不是玩家投稿通道。—— [小米游戏中心](https://game.xiaomi.com/viewpoint/1267481814_1592986835052_16)

### 推论
- 对女团养成游戏，可以做“AI 应援造型 / 舞台服提案”的站内相册 + 投票 + 主题赛季。获胜作品由官方美术**人工重绘**后实装，并署名获奖玩家；不要直接把 AI 原图上架售卖。这样既保留 UGC 的参与感，也避开“官方卖 AI 货”的反感。
- 比赛规则要与官方内容的 AI 政策一致，并明确区分“AI 生成赛道”和“手绘赛道”，避免出现第五人格那样的双标指控。

### 缺口
- 没有找到 Infinity Nikki / 闪耀暖暖“玩家设计大赛作品实装”的权威资料。
- 没有找到任何游戏公开站内 AI 外观分享或投票功能带来的参与度数据。
