# CHO-SIREN 资源工厂（5090 本地出图出视频 + AI 自动验收）

> 给 Codex：少爷的 5090 在你那边远程连着。代码已经写好在 `Tools/asset-factory/`，模拟模式（不用显卡）已在 Mac 上跑通：排队 → 生成 → 数值检查 → AI 审核 → 不过就带着反馈返工 → 过了等少爷在网页上点「采用」→ 导出。你要做的是在 5090 上装好模型、导出 ComfyUI 工作流、接上审核模型、试跑并调参。

## 1. 少爷要的东西（整理后）

- 一个**网页**，打开就能看到：显卡在做什么、利用率、每个资源的进度、每次生成的结果和 AI 的意见。
- **一直在出资源**：游戏里需要动起来的地方，用视频模型做；需要图的地方，用图像模型做。
- **AI 先验收**：每出一个，先过一遍硬指标（时长、循环接缝、背景能不能抠、动得够不够、有没有闪），再让视觉模型对照原画逐条打分。
  - 过了 → 标「AI 通过 · 待少爷验收」，然后马上做下一个；
  - 没过 → 用 AI 给的修改意见改提示词，**自动返工**，最多 6 次；还不行就先放一边做别的，不卡住整条线。
- **少爷最后拍板**：网页上「采用」就复制到 `export/`，「退回」可以写一句想怎么改，会加进提示词重做。
- **显卡别闲着**：两条工作线并行（一条在审核时，另一条的生成已经排进 ComfyUI）；队列空了自动补「还差几个版本」的任务；所有资源都够了才停。

## 2. 第一批要出的资源（`Tools/asset-factory/briefs.yaml`）

| 任务 | 用在哪 | 参考图 | 要求 |
|---|---|---|---|
| 大厅待机动画 `lobby-idle` | 大厅轮播里会动的角色（现在幻纱、瑶光、琥珀等还没有） | 成员立绘 `Art/Members/member-<id>.png` | 白底、6 秒左右、首尾同帧无缝循环、只做呼吸眨眼和小动作 |
| 大招动画 `ult-cutin` | 战斗放大招时的演出（SR / R 成员还没有） | 成员立绘 | 4–5 秒、镜头推进、灯光特效、结尾定格 |
| 种族转场 `transition` | 进关卡的转场页（四个种族，参考少爷给的转场 PSD） | `Tools/asset-factory/inputs/transitions/*.png`（已从 PSD 导出） | 1.5–2 秒、平涂剪影风、剪影要动起来 |
| 首领入场 `boss-entrance` | 1-5 琉娜、1-10 梅洛蒂、2-5 赛琳 等 | `Art/BattleUser/boss-*.png` | 3 秒、从特效中出现、仰拍、停在有压迫感的姿势 |
| 小怪入场 / 倒下 `minion-enter` | 战斗中途登场的小怪 | `Art/Enemies/<id>-ai-v1.png` | 1–2 秒、白底、跳入落地 |

加新东西只要改 `briefs.yaml`：加一个 target（比如新成员），或加一个新 brief，然后网页上点「重新读取任务」。

之后可以继续加：练习室循环、加载动画、剧情 CG（图像模型）、关卡背景、技能图标、Q 版表情、首领倒下动画、种族专属受击特效等。

## 2.5 不只是视频：5090 还能做的，以及「质量过关」的标准

少爷的要求：**不是能用就行，要看质量**。所以每类资源先做「对比试验」：同一批输入，新模型和现在在用的线上方案（RunningHub / MiniMax / Suno 等）各出一份，盲选，新模型赢了才进工厂。

| 类别 | 用在哪 | 本地候选模型（以官方页为准） | 自动验收看什么 |
|---|---|---|---|
| 视频 | 待机、大招、转场、首领/小怪出入场、加载、练习室 | H3（FastH3 NVFP4）、Wan 2.2、LTX-2（出小样） | 上面第 6 节的数值检查 + AI 对照原画 |
| 图像 | 剧情 CG、关卡背景、卡面、技能/道具图标、换装底图、宣传图 | Qwen-Image / Qwen-Image-Edit、FLUX 系、二次元 SDXL/Illustrious + 项目 LoRA | 分辨率、透明边、AI 打分（构图、手、脸、和项目画风一致），同角色脸部相似度 |
| 角色配音 | 日语台词（新角色、敌人、大厅台词） | GPT-SoVITS / CosyVoice / Fish-Speech 等可做「同一声线」的语音模型 | Whisper 听写对照台词（错字率）、响度 -19.5 LUFS、无爆音、声线相似度（说话人向量） |
| 音效 | 打击、技能、UI、判定音 | Stable Audio Open、MMAudio 等 | 时长、峰值、响度、和现有音效库的风格对比 |
| 音乐 | 关卡/大厅 BGM、短循环 | ACE-Step、YuE 等开源音乐模型 | 循环接缝、响度、AI 听感评分；先和现在 Suno 出的歌盲比 |
| 后期 | 所有视频/图 | RIFE 插帧、SeedVR2 / Real-ESRGAN 超分、BiRefNet 抠图 | 超分前后清晰度、抠图边缘干净 |

做法：

- 每类先挑 1–2 个模型做 20 组对比，把胜率记到本文档末尾；胜率不到一半的不上线，继续用线上方案。
- 显卡排班：白天视频为主（最吃显卡），夜里插入图像和音频的批量任务，模型切换按批次做，减少反复加载。
- 声音和图片同样走「生成 → 自动检查 → AI 审核 → 返工 → 少爷确认」；`briefs.yaml` 增加 `kind: image / voice / sfx / music` 时，在 `generate.py` 加对应的生成器（ComfyUI 有音频节点的直接用工作流；没有的就写一个调用本地服务的生成器）。

## 2.6 工厂自己会做的事（不用少爷一条条说）

- **自己找缺口**：页面「AI 建议」会翻游戏仓库，列出还缺的资源（哪些成员没有大厅待机 / 大招动画、哪个首领没有入场、哪些关卡没有专属背景、哪些小怪登场没动作…），点「加入」或「全部加入」就排进任务。规则在 `factory/scanner.py`，游戏加了新种类的资源就加一条规则。
- **类别不写死**：`briefs.yaml` 里一类资源就是一个 brief（现在有视频、图像、日语配音、序列帧特效），`kind` 可以是 video / image / voice / sfx / music，新类别直接加。
- **模型不写死**：`config.yaml` 的 `backends` 登记模型——ComfyUI 工作流、本地 HTTP 服务（配音、音乐、音效）、命令行工具、云端接口都行。加新模型 = 加一条 backend，再写进 brief 的 `backends`。
- **跟着新模型走**：「模型 → 新模型雷达」每天看一次 Hugging Face 上热门的视频 / 图像 / 配音 / 音效模型，标出权重大小和 5090 能不能跑、许可证。值得试的，Codex 装好后加成 backend。
- **效果优先，不浪费算力**：每个模型在每类任务上只试 3 次；之后只有通过率最高（或差 10 个点以内）的才继续干活。一直不过的模型自动停用；连最好的模型都很少通过的任务先暂停，等更好的模型出来再开。
- **付费模型要少爷点头**：便宜但明显比本地好的云端模型登记成 `paid: true`（写价格），默认不用；少爷在「模型 → 付费模型」打开并设预算才会用，花到预算自动停。钥匙只放环境变量。
- **验收后自动合入游戏**：点「采用」后——大招视频直接放进游戏并登记（和已有的大招随机播）；关卡背景直接放到对应关卡；要改代码的（大厅新角色、转场、首领入场等）进 `game-inbox/`，由 Claude / Codex 接。`integrate_commit: true` 时自动在游戏仓库提交（不推送，发布照常走发布流程）。
- **声音也要审**：配音先用 Whisper 听写，和台词对不上就返工；再交给能听音频的模型（如 Qwen2.5-Omni）按标准打分；同时查时长、响度、爆音、空白。

## 3. 5090 上要装的

> 版本和显存数据来自网上资料，装之前以官方模型页为准。商用前请确认每个权重的许可证（例如 H3 社区权重有地区和用途限制）。

1. **ComfyUI**（最新版，CUDA 12.8+ 的 PyTorch，5090 是 Blackwell 架构）。开 `--listen 127.0.0.1 --port 8188`。
2. **视频模型（主力）**：MiniMax **H3**（2026-08 开源权重，支持文生视频、首尾帧、参考图）。5090 优先用 FastVideo 的 **FastH3 Omni（NVFP4）**，或 FreeVideo 插件的 int8 版本。
   - 备选：**Wan 2.2 I2V**（FP8，支持首尾帧，画面稳），**LTX-2** 系列（快，适合先出小样）。
3. **图像模型**（静态资源、改图）：Qwen-Image / Qwen-Image-Edit 或 FLUX Kontext，二次元可加 Illustrious / SDXL 系 LoRA。
4. **后期**：RIFE 插帧（24→48 帧更顺）、SeedVR2 或 Real-ESRGAN 超分（720p→1080p+）、BiRefNet / RMBG-2.0 抠图（白底→透明）。
5. **审核模型**：Ollama 跑 `qwen2.5vl:7b`（约 6GB 显存，和视频模型一起放得下；放不下就用 CPU 跑或者改成云端模型，钥匙只放环境变量）。

参考：[MiniMax H3 开源公告](https://www.minimax.io/news/minimax-h3-open-source)、[FastH3 Omni（5090 / NVFP4）](https://haoailab.com/blogs/fastvideo-omniref-update/)、[本地跑 H3 显存说明](https://www.mindstudio.ai/blog/minimax-h3-run-locally-guide)、[FreeVideo 8GB 方案](https://artrealmai.com/article/freevideo-minimax-h3-8gb-vram-local-video)、[开源视频模型对比](https://ltx.io/blog/open-source-video-generation-models-guide)。

## 4. 部署步骤（Codex 照做）

```bash
cd <CHO-SIREN 仓库>/Tools/asset-factory
python -m venv .venv && .venv/Scripts/activate      # Windows；Linux 用 source .venv/bin/activate
pip install -r requirements.txt
copy config.example.yaml config.yaml                # 改 comfy_url / reviewer / host
python -m factory.app                               # 打开 http://<5090 机器>:8790
```

1. 在 ComfyUI 里分别搭好三个工作流，用 **Export (API)** 存到 `Tools/asset-factory/workflows/`：
   - `i2v.json`：参考图 → 视频；
   - `i2v-firstlast.json`：首帧 + 尾帧 → 视频（用来做无缝循环）；
   - `t2i.json`：文生图 / 改图（以后静态资源用）。
   占位符写法见 `workflows/README.md`（`"{{prompt}}"`、`"{{image}}"`、`"{{seed}}"` 等）。
2. 先把 `config.yaml` 里的 `mock: true`，确认网页、队列都正常；再改成 `false` 接真模型。
3. 每种任务先手动跑 1 个，看时长、分辨率、显存、单条耗时，把 `briefs.yaml` 里的 `size / frames / checks` 调到合适。
4. 接上审核模型，挑 3 个明显好的、3 个明显坏的结果，看 AI 判得对不对；不对就调 `pass_score` 或改 rubric 的说法。
5. 让它整夜跑，第二天看利用率（网页顶部）和通过率，把数字记进这份文档末尾。

## 5. 让 5090 吃满的做法

- **两条工作线**（`config.yaml` 的 `workers: 2`）：一条在做数值检查和 AI 审核时，另一条的生成已经排在 ComfyUI 里，显卡不用等。
- **模型常驻**：ComfyUI 不要每次卸载模型；同一时间尽量连着跑同一种任务（同一工作流），少换模型。
- **先小样后精修**（建议你下一步加上）：先用快模型出 480p 小样过 AI 审核，过了再用主力模型出正式版。烂的方向早淘汰，显卡时间花在有希望的版本上。
- **夜里也跑**：关掉睡眠，开机自启 ComfyUI 和工厂（任务计划程序 / systemd）。重启后之前「生成中」的任务会自动回到队列。
- **利用率**每 5 秒采样一次 ComfyUI 队列，显示最近一小时显卡有活干的比例；长期低于 80% 就加 worker 或加任务。

## 6. 验收规则（代码里已实现）

1. **数值检查**（`factory/review.py`）：时长范围、首尾接缝（循环）、运动量（不动 / 太乱）、亮度闪烁、白底边缘是否干净（能不能抠）、分辨率和比例。
2. **AI 审核**：参考原画 + 6 帧拼成一张图，连同 brief 里的验收标准发给视觉模型，要求它回 JSON：`pass / score / problems / fix_prompt`。分数低于 `pass_score`（默认 75）也算不过。
3. **返工**：用 AI 给的 `fix_prompt` 和数值问题重写提示词、换种子再出；每个任务最多 6 次。一个资源连续 3 个任务都没过，就不再自动补，等人改 brief。
4. **少爷确认**：「AI 通过」的在网页「待我验收」页签里；「采用」导出到 `export/<任务>/<目标>-job<n>.mp4`。

## 7. 放进游戏

导出的文件不要直接提交进 git（太大）。同步到 Mac 后由 Claude / Codex 按类型导入：

| 类型 | 怎么进游戏 |
|---|---|
| 大厅待机 | Mac 上用 `Tools/lobbyvideo/matte.swift` 抠成「左彩色右透明」的 MP4 → `StreamingAssets/LobbyIdols/`，再在 `PsdHomeCharacterCarousel` / `PsdHomeIdolVideo1025` 加一位（做法见 0.9.55 加幻惑伶、凛音那次） |
| 大招 | 转 H.264 MP4 → `StreamingAssets/UltimateVideos/`，在 `Resources/UltimateVideos/manifest.json` 加一条（同一角色可以多条，随机播） |
| 转场 | → `StreamingAssets/Transitions/`，接到 `ChallengeVideoTransition` |
| 首领入场 | → `StreamingAssets/BossEntrance1102/`，在 `StageBoss1103` 打开 `EntranceVideo` |
| 小怪入场 | 抠图后接到敌人登场（现在是 `BattleEnter1112` 的淡入弹出，可以换成视频） |

## 8. 安全

- 网页默认只监听本机；要在局域网看，`host: 0.0.0.0` 并只在内网开端口，不要映射到公网。
- 不在 `config.yaml`、日志、仓库里放任何钥匙；云端审核用环境变量（`api_key_env`）。
- 参考图来自游戏仓库，只在本机处理；用云端审核模型时，原画会发给那家服务，先问少爷。

## 9. 现状

- 已完成：代码（`factory/store.py` 队列、`generate.py` ComfyUI 客户端和模拟器、`review.py` 检查和 AI 审核、`engine.py` 返工循环、`app.py` + `static/index.html` 网页）、5 类 brief、四张转场参考图。
- Mac 模拟模式实测：两条线并行、返工、导出都正常。
- 已加：自动找缺口（AI 建议）、可插拔模型后端、模型榜（按通过率挑模型）、新模型雷达、付费模型确认和预算、验收后自动合入游戏、音频检查（听写 + 音频审核）。Mac 模拟模式下全部跑通（找出 53 个缺口、模型轮换、付费开关）。
- 未完成（需要 5090）：真实模型和工作流、审核模型调准、实际利用率和通过率、先小样后精修。
