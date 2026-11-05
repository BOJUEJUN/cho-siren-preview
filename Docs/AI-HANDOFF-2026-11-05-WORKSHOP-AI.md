# 最新（2026-11-05 晚，Claude）— GPT 换装个人测试版、画图网站迁移

## 游戏 GPT 换装：只给少爷本人测试，不是公开上线
- 少爷在美国出差（OpenAI 支持地区），要求在自己手机上测 GPT Image 2.6 换装。国内玩家用不了 OpenAI，**公开版本仍走广州 RunningHub 服务，不要把 GPT 推给全体玩家**。
- 新服务：腾讯云 SCF `cho-siren-ai-dress-gpt`（ap-singapore），URL `https://1318245308-m2ju450cfv.ap-singapore.tencentscf.com`。源码 `Tools/ai-dress-gpt-fn/index.py`，部署 `Tools/ai-dress-gpt-fn/deploy_gpt_fn.py`（读 `~/.cho-siren-cos.json`、`~/.openai.key`、`~/.cho-siren-gpt-test.code`，不打印）。每个请求必须带 `X-Test-Code`；用 Responses API 后台模式 + `gpt-image-2.6-flare`、1024×1536、medium、绿幕。实测端到端约 31 秒出图。
- 客户端 `WorkshopAi1104.cs`：只有存在本地文件 `Assets/Resources/AiGptTestCode.txt`（已 gitignore）时才切到新加坡服务并带测试码；结果下载改为 `UnityWebRequest.Get` + `LoadImage`，兼容新加坡函数返回的 base64 文本。**打完测试包后已删掉该文件，公开构建不受影响**；以后打公开包前确认它不存在。
- 测试包：`Builds/iOS-device-0.9.36-gpt` → `Builds/iOS-DerivedData-device-0.9.36-gpt`，已覆盖安装到伯爵君 iPhone（版本仍显示 0.9.36，未卸载、未清档、未自动启动）。五条悟未动。日志 `Builds/qa-logs/gpt-test-1105/`。真机里点一次换装的实测还没做（要少爷自己在手机上试）。
- iPhone 描述文件 2026-11-06 19:34（北京时间）到期。回国后这台手机的 GPT 换装不该再用；要恢复正常版就重新装公开 0.9.36。
- OpenAI 钥匙在聊天里出现过，测完建议少爷去 OpenAI 后台换新钥匙后重新部署。

## 画图网站（另一个仓库 ~/XiaomiMiMoProjects/krea-studio-models）
- 已从 Netlify 迁到 Vercel 项目 `krea-studio`，域名 https://enzoban.com （腾讯云买的 enzoban.com，DNSPod 已加 A/CNAME）。历史 131 条和图片已搬到私有 Vercel Blob。
- 新模型：Nano Banana 2.1、Krea 3（Krea API，余额单独充值）、GPT Image 2.6 Flare/Sunburst；每个模型只显示支持的参数。代码已推到 GitHub main（19ab73c）。

---

# 最新交接更新 — 2026-11-05

以下更新优先于本文件其余较早记录。请先重新核查当前状态，不要重复发布或覆盖用户变更。

## 0.9.36 发布状态

- 网页 0.9.36 / R167 已上线，实际加载至标题画面；Windows、Mac、Android 下载入口和自动更新清单均已切换至 0.9.36。Android 使用可访问的 GitHub APK 地址。
- 用户 Mac 已安装 0.9.36，签名和 Dock 已检查，四份存档及相关文件校验值未变。伯爵君 iPhone 已覆盖安装 0.9.36，未卸载、清档或自动启动；五条悟 iPhone 未操作。
- 网站的 113 项本地检查通过；GitHub Quality checks 两次在 checkout 后取消，测试步骤未运行。不能把本地检查说成 CI 通过。源码分支同步状态应接手时重新核对。

## GPT Image 2.6 换装接入

用户要求使用其 OpenAI API 额度替换游戏现有生图服务，并要求手机也能用。当前只完成隔离的本地候选，**没有上线、没有真实调用、没有配置 OpenAI 凭据，也没有消耗 API 额度**。现网仍使用原来的生图服务；不要称其已经切换到 2.6。

候选位置（用户 Mac）：
- `/Users/bojuejun/Documents/Codex/2026-11-05/task/openai-dress-candidate/`
- `/Users/bojuejun/Documents/Codex/2026-11-05/task/openai-dress-candidate.zip`
- `/Users/bojuejun/Documents/Codex/2026-11-05/task/openai-dress-candidate.patch`
- `/Users/bojuejun/Documents/Codex/2026-11-05/task/openai-dress-candidate-artifacts.json`：文件哈希清单。使用前重新核对源码、包和补丁的哈希。

最终候选 Python 3.9 后台 99 项测试通过，客户端 48 项故障测试通过，C# 编译通过（7 条警告来自未修改文件）；补丁应用检查通过。这些是离线结果，不是真实云端或手机出图验收。候选包含任务恢复、重复请求控制、扣钻保护及试验准入，但真实网关认证、手机 UI、线上存储、断电原子性和跨设备结算仍未验证。

**地区前提：**用户明确表示玩家包含中国大陆。当前 OpenAI 服务条款与支持地区限制使其不能按这一范围统一提供 2.6；海外服务器或改变 IP 不能代替地区资格。参见 https://openai.com/policies/services-agreement/ 和 https://developers.openai.com/api/docs/supported-countries 。保留现网；在确定合规方案前不部署新服务或调用 API。下一步需要用户选择：验证支持大陆玩家的其他供应商，或单独为符合条件地区设计方案。现有供应商是否满足全部要求仍须核实。

后续还需真实验证云资源、认证、防滥用、密钥安全、费用上限、失败和回退；创建或配置持久凭据、开启收费资源及实测预算需对应授权。不要把密钥写入仓库、客户端、日志或交接文档；不要修改用户 Karing、VPN 或网络设置。

## 发布验证证据与实际范围

证据位于本机 `/Users/bojuejun/Documents/Codex/2026-11-05/task/`：
- `published-native.json`、`verified-native-artifacts.json`、`verified-latest-aliases.json`：0.9.36 下载地址、大小及前 64 KiB 抽样比对。Windows 安装包 734863398 字节、Mac DMG 812979981 字节、Android APK 652053990 字节；Windows/Mac 更新 ZIP 及各自 513 项文件清单已核对。不能据此声称 Windows/Android 已安装运行或所有大包完整下载验收。
- `installed-desktop.json`、`mac-0936-verification.json`：Mac 0.9.36、签名通过、Dock 保持原入口、4 份保存文件核对未变；未启动桌面游戏。旧 0.9.35 备份位于原项目 `Builds/qa-logs/full-release-0936/desktop-backups/`。
- `iphone-0936-install.json`、`iphone-0936-apps-after.json`：伯爵君 iPhone 覆盖安装成功，查询版本 0.9.36 / build 0。未卸载、清档或自动启动；未打开手机存档验证游戏内功能。五条悟设备未操作。证据文件可能含设备标识，交接正文不复制这些标识。
- 正式网页：`https://bojuejun.github.io/cho-siren-preview/`；清单：`https://cho-siren-1318245308.cos.ap-guangzhou.myqcloud.com/app-version.json`。已验证 Unity WebGL 标题画面，不等于本轮完成游戏内收费换装验收。
- Android 使用 `https://github.com/BOJUEJUN/cho-siren-preview/releases/download/v0.9.36/CHO-SIREN.apk`；此前 COS APK 地址返回 403，不能改回不可访问地址。
- 以上为已保存的发布实证；接手时只读复核当前进程、版本和线上状态，避免重复发布。历史签名有效期不代表现在仍可安装，应实际检查。

## 最终 GPT Image 候选与校验值

以下是已冻结、尚未应用到原项目的本地候选，SHA-256 已与实际文件复核：

| 文件（相对于本机任务目录） | SHA-256 |
| --- | --- |
| `openai-dress-candidate.zip` | `3749bf8f4e9fad74a221bef9f12089773c3c3bd8e6889cbbae93ae5f0fa4b51e` |
| `openai-dress-candidate.patch` | `f33dfa250842e791e0238ee5149f26fdb6b6d82ed458790c455e9276d67f5ffc` |
| `openai-dress-candidate/Tools/ai-dress-fn/build/function-candidate.zip` | `ee5197d1f03219109bf591cfc2d3d6c52d9c02acd647ea42df7b890ddb43441d` |

不要使用候选根目录 `build/backend-trial-reviewed.zip`，它是较早中间包。最终总包包含 30 个条目，已逐项核对哈希；未包含客户端 build DLL/PDB。补丁仅执行过 `git apply --check`，没有应用、提交或推送。

说明与测试证据（均相对于 `openai-dress-candidate/`）：
- `README.md`：接入、准入、存储、失败及回退说明。
- `Tools/ai-dress-fn/offline-test-results.txt`：Python 3.9.6 的 99 项后台测试通过；`build/function-candidate.manifest.json` 记录 index/runtime/transport/auth 四模块哈希。
- `client-tests/README.md`、`offline-client-results.txt`、`runtime-compile-results.txt`、`verification.json`：48 项离线故障测试及完整 ChoSiren.Runtime 编译通过，7 条警告来自未改原文件。verification.json 包含最终 4 个 C# 文件和测试 runner 的哈希。
- 后台测试实际执行 HMAC 验签，再进入模拟存储/worker；云端验签入口和真实票据签发交付尚未验收。客户端测试使用假存储及序列化适配，未访问真实 PlayerPrefs 或用户存档；不能证明断电或跨设备恰好一次结算。
- 候选客户端默认 Endpoint 为空、拒绝旧生产服务 host；使用短期试验码，但不保存真实密钥。已安装的 iPhone 0.9.36 没有新认证头，不能只替换后台便声称手机已接入。
- 候选包含试验期一次上限、原请求重放去重、收费结果未知时不自动重试、已知任务恢复、失败重试不重复扣钻及衣橱保存保护；尚未证明真实云端竞争写、权限、超时、下载和成图质量。

## 接续顺序及授权边界

1. 先读原工程 `/Users/bojuejun/cho-siren-unity/AGENTS.md`，核对 git 状态、现有进程及上述哈希。保留用户和其他任务改动；不要直接运行原工程 `Tools/ai-dress-fn/deploy_fn.py`，旧脚本会部署真实服务。候选中的同名脚本只有本地打包用途。
2. 先确定服务包含大陆玩家的可行方案。不得通过境外服务器、代理或改变 IP 绕过 OpenAI 地区限制；现有 RunningHub 是否满足全部条款亦未核实。若另做支持地区专用功能，须核实运营主体、账户和真实终端用户资格，并有效隔离不支持地区；候选尚无完整地区准入体系。
3. 云账户只读盘点位于 `tencent-readonly-inventory/RESULT.md`：已发现广州 SCF/COS，未发现可直接使用的境外 SCF/COS；CVM/Lighthouse 查询无权限，不能据此说境外服务器一定不存在。不要擅自新增资源、扩权或配置密钥。
4. 持久凭据创建/配置、私有存储及 worker 资源、IAM 权限、具体收费测试预算均需对应的用户明确确认。OpenAI key 只可在私有 worker，票据签名 secret 与其分离；不得进入客户端、仓库、日志、聊天或本文。赠额不是无限预算，也不保证用尽后自动停费。
5. 地区与授权满足后，先真实检查网关认证、防滥用、私桶匿名拒绝、create-only 竞争、并发/配额、超时、短链接、结果保留、供应商计费未知时的人工核对和回退，再做一个明确授权的手机请求。此前提出 US$0.25 赠额及 ¥1 云端增量作为小额验收预留，只是建议，用户未确认，亦非已生效的绝对费用上限。
6. 真机验收应确认请求到成图、绿幕抠图、下载、扣 10 星钻、失败恢复、手动保存、重进衣橱；一次成功不能声称全部平台通过。手机仅操作用户指定设备，保留存档、不自动启动游戏；五条悟仍不操作。不要更改 Karing、VPN 或网络设置。

## 以下为较早记录（仅保留背景）

下文中的“仍为 0.9.35”“未发布”“下一步发布 0.9.36”“临时提高限额”等均是历史描述，不是当前状态或新的操作授权。以本文顶部更新及接手时实查为准。

---

# AI handoff — 2026-11-05: 创意工坊 AI 换装 (in game) + 星钻 cost

Read `AGENTS.md` first (reply style: Chinese, call the user 少爷, very short, plain words). This continues
`Docs/AI-HANDOFF-2026-11-03-CONTINUE.md` and `Docs/FULL-RELEASE-2026-11-04-0935.md`.

## State in one paragraph

Released game is still **0.9.35**. Everything below is committed on branch `release/music-0.3.5-20260909` but
**not released**: 经纪人 account level (d386c5b), 创意工坊 replacing 专辑制作 (cc5778b … d654325), in-game AI 换装
through Tencent Cloud (d499f3a), 星钻 cost (f6e5ca8), and the uncommitted Banana 2.1 switch described below. The Mac
app `/Applications/幻域魅声.app` has everything up to f6e5ca8 (Seedream era); it needs one more update after the
last fix. The user asked to try on Mac first, then release 0.9.36 to all channels when they approve.

## How AI 换装 works

1. Game (`Assets/Scripts/WorkshopAi1104.cs`) PNG-encodes the member still (≤1024 px tall), POSTs
   `{device, member, prompt, image}` to `WorkshopAi1104.Endpoint`
   = `https://1318245308-d7n1q0c0wj.ap-guangzhou.tencentscf.com` `/dress`, then polls `GET /dress/<task>` every 2.5 s
   (180 s max), downloads the result, `KeyGreen` removes the green backdrop (border-connected flood fill + enclosed
   gaps + soft edge + despill), `FitToStill` scales/places the figure like the still so face close-ups still land.
2. Tencent Cloud SCF function `cho-siren-ai-dress` (ap-guangzhou, Python 3.9, source `Tools/ai-dress-fn/index.py`,
   stdlib only) holds the keys in env vars, enforces **5 per device per day and 300 per day for everyone** (counts in
   COS `ai-quota/<date>.json`), and calls RunningHub.
3. Model: **Nano Banana 2.1 economy** on the global site `https://www.runninghub.ai/openapi/v2/rhart-image-n-nb21/image-to-image`
   (body `prompt, imageUrls[dataURI], aspectRatio "9:16", resolution "2k"`; ~$0.03, ~40 s, 1536×2752, pure green
   backdrop). If the global site refuses at submit time it falls back to **Seedream 4.7** on the China site
   (`runninghub.cn/openapi/v2/seedream-v4.7/image-to-image`, ¥0.15, ~16 s). Task ids are prefixed `b:` / `s:` so the
   query goes to the right site. Banana 2.1 is NOT available on the China site (compliance, error 40310).
4. Player cost: **10 星钻 per finished outfit** (`GameModel.AiDressDiamondCost1105` in `GameModelWorkshop1104.cs`),
   charged only when the outfit arrives; not enough 星钻 → toast, nothing sent.
5. Style chips map to full outfit descriptions (`WorkshopAi1104.StylePrompts` / `PromptFor`) — short tags like
   「偶像演出服」 alone made the model keep the old outfit.

## Secrets (never print, commit or paste them)

- China-site RunningHub key: `~/XiaomiMiMoProjects/krea-studio-models/.env.local` (`RUNNINGHUB_API_KEY`).
- Global-site RunningHub key: `~/.cho-siren-rh-ai.key` (the user pasted it in chat; suggest they rotate it later).
- Tencent credentials: `~/.cho-siren-cos.json` (also used for SCF; user granted `QcloudSCFFullAccess`).
- Deploy script: `Tools/ai-dress-fn/deploy_fn.py` (run with a venv that has `tencentcloud-sdk-python`) — zips `index.py`, Create/UpdateFunctionCode, then
  UpdateFunctionConfiguration with env `RUNNINGHUB_AI_KEY, RUNNINGHUB_API_KEY, COS_BUCKET, COS_REGION,
  COS_SECRET_ID, COS_SECRET_KEY`.
  The HTTP trigger (function URL, AuthType NONE) already exists; redeploying code keeps it.

## Last step in progress (do this next)

1. Uncommitted: `Tools/ai-dress-fn/index.py` (Banana 2.1 + fallback, already deployed and verified end to end:
   `b:` task done in 45 s) and `Assets/Scripts/WorkshopAi1104.cs` (HTTP refusals now show the service's message,
   e.g. 「今天的 AI 换装次数用完了」, instead of 「网络连接失败」; 429 sets `RemainingToday = 0`).
2. The last live in-game test (`play4`) failed with 「网络连接失败」 after 1 s. Most likely cause: the Unity test
   device already used its 5 tries today (earlier run reported 「今天还能换 0 次」), and the old client hid the 429.
   Verify after the fix: rerun the capture (it now shows the real message); to really test generation, wait for the
   next day (Beijing time) or temporarily raise `AI_DRESS_DAILY_LIMIT` env on the function, then restore 5.
3. Run tests (one Unity process at a time):
   - `Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testFilter WorkshopKey1104Tests ...`
     (set `WORKSHOP_KEY_SAMPLE=<png>` to also key a real sample; 1536×2752 keys in ~220 ms)
   - `HOMECENTER_PHASE=ai1105 WORKSHOP_AI_LIVE=1 Unity ... -testPlatform PlayMode -testFilter TopUp1104CaptureTests`
     (live step costs one model call; screenshots land in `Builds/qa-logs/topup-1104/workshop-ai-result*.png`)
4. Quit the game (`osascript -e 'tell application "幻域魅声" to quit'`), run
   `Builds/qa-logs/local-preview/update-desktop.sh`, commit only these files.
5. When the user approves: release **0.9.36** (never 0.3.x) to web / PC / macOS / Android / update manifest, replace
   the Mac app, install on 伯爵君's iPhone keeping saves (no auto-launch; 五条悟 on hold; iPhone signing profile
   expired 11-06 19:34 — the user must re-login in Xcode). Follow `Docs/FULL-RELEASE-2026-11-04-0935.md` and
   `Docs/WEBGL-RELEASE-SAFETY-2026-09-05.md`. WebGL: `WorkshopRecorder1104` mic is native only; AI works over HTTPS.

## Costs the user knows

Seedream ¥0.15/张, Banana 2.1 economy ~$0.03 (~¥0.2)/张, official $0.04 but ~100 s. Global balance was ~$7.7.
Worst case per day = 300 × $0.03 ≈ $9. Players pay 10 星钻 (=1 元 at the planned rate); payment channel is still
closed (`PaymentOpen1104 = false`).

## Other open items

- Domain enzoban.com / enzoban.cn: purchase waits for the real-name template review; confirm item and price with the
  user before ordering; the user pays by scanning.
- Run `Tools/cos_prune.py --apply` after releases.
- The user's Claude-in-Apple purchase failed; they were told to cancel in Apple settings and request a refund.
