# CHO-SIREN 与 enzoban.com 交接（唯一现行交接，2026-11-07）

本文件取代以前所有 `AI-HANDOFF-*`、`AI-COLLAB-HANDOFF`、`WORKSHOP-PRIVATE-PREVIEW-*` 文档（已删除，需要时可从 git 历史查）。各次发版记录仍在 `Docs/FULL-RELEASE-*.md` / `RELEASE-*.md`。接手时先实查一遍状态，别照抄本文数字当结果。

## 0. 先读

- 用户：称呼「少爷」，中文，回复极短、口语、不讲术语；他说的不一定对，可以纠正，把事做好。
- 本仓库 `AGENTS.md` 的产品约定全部有效（版本只往前 0.9.x、选中=实心青色、底部导航中文、首页两位角色不换等）。
- 禁止 Devin；不启用 DeepSeek Harness；同一时间只一个 AI 改这个工程，同一 Unity 工程同时只跑一个 Editor 进程。
- **不碰 Karing / VPN / 代理 / 路由 / 网络设置。**
- 浏览器：自己开新标签页做事，**不要把少爷正在看的页面导航走**。
- 手机：覆盖安装、保留存档、**不自动打开游戏**。
- 秘密只在运行时从本机文件读，绝不打印、提交、贴进聊天/文档/日志（清单见第 6 节）。拉下来的 vercel env 文件用完即删。

## 1. 游戏现状（2026-11-07 晚）

- **0.9.40 测试版（未发布网页/下载）**：赵金 11-07 六页新美术 + 会议纪要落地 + 200 级 + 角色体力 + 累计更新公告。明细 `Docs/ART-FEEDBACK-2026-11-07.md`、数值 `Docs/BALANCE-1107-LV200.md`。`ProjectSettings` 已是 0.9.40 / Android 940；代码**未提交**。正式发 0.9.40 时按 0.9.39 流程（`Builds/qa-logs/full-release-0939/`）走，网页 release notes 用 `Assets/Resources/Data/changelog.json` 的 0.9.40 条目。
- PlayMode 全量 487 条里有 ~127 条是改动前就不过的老测试（基线对比 11-07：只这次新增的 9 条已全部修好）；EditMode 803 条全过。

| 渠道 | 版本 | 说明 |
|---|---|---|
| 网页 | 0.9.39 / R169 | https://bojuejun.github.io/cho-siren-preview/ ，Pages 仓库 `/Users/bojuejun/cho-siren-web` main `aefba0a` |
| Windows / Mac / Android 下载 + 更新清单 | 0.9.39 | 清单 `https://cho-siren-1318245308.cos.ap-guangzhou.myqcloud.com/app-version.json`；Android 用 GitHub Release 地址（COS 的 APK 地址实际下载 403） |
| 本机 `/Applications/幻域魅声.app` | **0.9.40 测试版** | 11-07 本地预览（未发布），Dock 入口保留 |
| 伯爵君 iPhone | **0.9.40 测试版** | 11-07 覆盖安装（无 GPT 测试码，存档保留）；包 `Builds/iOS-DerivedData-device-0.9.40/…/CHO-SIREN.app`，描述文件 11-13 到期 |
| 五条悟专用机 | **0.9.38 私人版** | 11-07 要装 0.9.40 测试版，手机锁屏（CoreDevice 4016/10003）没装成；解锁后用 `Builds/qa-logs/private-0940/install.sh 00008140-001154D23E93001C` |

- 源码分支 `release/music-0.3.5-20260909`（名字老，内容是最新），`ProjectSettings` 已是 0.9.39 / Android 939（11-07 全量发布，记录 `Docs/FULL-RELEASE-2026-11-07-0939.md`）。私人 0.9.38 代码 `8f09814`（工坊编辑页 + Suno 作曲），11-07 已推送到远端（仓库是公开的）。
- 工作树里有与本任务无关的老改动（`Docs/FULL-RELEASE-2026-11-02-0930.md`、`Packages/*`、`chosiren-font-unicodes.txt`、`ResourcesArchive/`、`chpl26g2`），保留，不要清理或顺手提交。
- 公开版 0.9.39 = 0.9.38 的工坊编辑页 + 作曲按钮隐藏（作曲只在带测试码的私人包显示）。下一次正式发版用 **0.9.40 或更高**，打公开包前**确认 `Assets/Resources/AiGptTestCode.txt` 不存在**（它是 gitignore 的私人开关，有它就会走 GPT 测试服务）。

### iPhone 签名

- 11-06 少爷在 Xcode 登录了 Apple 账号，已用自动签名重签 0.9.38：`Builds/iOS-DerivedData-device-0.9.38/Build/Products/Release-iphoneos/CHO-SIREN.app`（工程 `Builds/iOS-device-0.9.38/Unity-iPhone.xcodeproj`）。日志 `Builds/qa-logs/resign-1106/`。
- 新描述文件含两台设备，**2026-11-13（周五）到期**，到期前要再重签安装，否则手机打不开。
- 重签：`xcodebuild -project <工程> -scheme Unity-iPhone -configuration Release -destination generic/platform=iOS -derivedDataPath Builds/iOS-DerivedData-device-0.9.38 -allowProvisioningUpdates DEVELOPMENT_TEAM=63K7K9XRZ2 CODE_SIGN_STYLE=Automatic build`。报 "No Accounts" 就请少爷在 Xcode → 设置 → Apple 账号登录。
- 安装 / 查版本（不加 launch）：
  - `xcrun devicectl --timeout 120 device install app --device <UDID> <app路径>`
  - `xcrun devicectl --timeout 20 device info apps --device <UDID> --filter "bundleIdentifier == 'com.bojuejun.chosiren'" --json-output <文件>`
- 设备 UDID：伯爵君 `00008140-000E706E2493C01C`；五条悟专用机 `00008140-001154D23E93001C`。Bundle `com.bojuejun.chosiren`，team `63K7K9XRZ2`。别装不带版本号的旧 `Builds/iOS-DerivedData-device/`。
- 历史上手机连不上（CoreDevice 4016）曾是 Wi-Fi 代理例外列表格式问题，证据在 `Builds/qa-logs/full-release-0930/`。再遇到先只读排查，不要改网络。

## 2. 创意工坊 / AI 功能

### AI 换装（公开版）

- 客户端 `Assets/Scripts/WorkshopAi1104.cs`（网络、恢复）、`WorkshopRequestError1105.cs`（错误分类）、界面 `ChoSirenApp.Workshop1104.cs`、`WorkshopPoster1105.cs`。
- 广州腾讯云函数 `cho-siren-ai-dress`（源码 `Tools/ai-dress-fn/index.py`）：RunningHub Nano Banana 2.1 经济版，失败退到 Seedream 4.7；每台设备每天 5 次、全体每天 300 次。
- 每次成品扣 10 星钻，成品到手才扣；充值通道仍关闭（`PaymentOpen1104 = false`）。
- 提交前先落盘记录，已知任务只查询不重发收费请求；下载 120 秒、校验图片完整。
- **不要随便运行 `Tools/ai-dress-fn/deploy_fn.py`**，它会改线上服务。

### GPT Image 2.6 换装（只给少爷自测）

- 少爷在美国出差时要求自测。OpenAI 不支持中国大陆，**公开版本不能用 GPT**，大陆玩家继续走广州 RunningHub。
- 新加坡函数 `cho-siren-ai-dress-gpt`（源码/部署 `Tools/ai-dress-gpt-fn/`），每次请求必须带测试码；返回的图已减半尺寸，手机下载小。
- 两台手机的 0.9.38 都带测试码，所以都会走 GPT。少爷回国后在大陆用不了这条；要恢复正常就装不带测试码的公开包。
- OpenAI key 少爷决定**不换**（11-06），别再催。

### 0.9.38 私人版新增（0.9.39 起公开，作曲除外）

- 编辑页半身/全身切换、转身查看；底部工具栏 + 参数抽屉；滤镜、背景、应援色、亮度/音高/律动。
- 作曲：`WorkshopMusic1105.cs` / `ChoSirenApp.WorkshopMusic1105.cs`，后端 `Tools/ai-dress-fn/music.py`（Suno，经广州函数，测试码保护），报价约 USD 0.08/次。只真实跑过一次 40 秒纯音乐；80 秒和日语女声没实测。
- 证据：`Builds/qa-logs/workshop-review-1105/`、`workshop-music-1105/`、`workshop-network-1105/`（后者私密，不要外传）。
- 手机上实际点换装、试听，以及少爷是否满意，都还没确认。

## 3. 游戏常用知识

- 关键代码入口：
  - 主入口 `ChoSirenApp.cs`；数据/存档 `GameModel*.cs`。
  - 战斗 `Panels/TacticsBattlePanel*.cs` 和 `Systems/Tactics/`。
  - 地图 `LevelMapPanel*.cs`；构建 `Assets/Editor/ChoSirenBuild.cs`。
  - 项目大量用 partial class，先查调用方。
- 战斗规则：六骰子；重投库存最多 3、每次己方行动充 34%。大招充能按 `AGENTS.md`（队长 17s，之后 20/25/30）。只有队长吃骰型充能加成。
- 存档 key `ChoSiren.Save.v2`（PlayerPrefs）；Mac 偏好 `~/Library/Preferences/com.bojuejun.chosiren.plist`，不要覆盖或删除。测试用隔离存档。
- 工具：
  - Unity `/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity`。
  - 测试 `-runTests -testPlatform EditMode|PlayMode -testFilter … -testResults … -logFile …`。测试不加 `-quit`，构建才加。
  - 只跑与改动相关的测试；UI 要在真实画面里看。
- 美术与界面方向：
  - 紫黑 + 青 + 少量粉、锐角切面、撕纸拼贴、网点；整体简约、字少。
  - 真透明 PNG，保持原比例。
  - 练习室暂时不改；首页保留少爷选的两首日文歌。
- 改完游戏后：重建并替换本机 Mac app（`Builds/qa-logs/local-preview/update-desktop.sh`），退出游戏再换。

### 正式发版流程（少爷说"发布"时）

1. 定新版本号（只往前），先确认没有别的 AI 在构建；只提交本次文件。
2. 复用 `Builds/qa-logs/full-release-0939/`（含 `stage-web.py` 网页上架脚本；更早的 `full-release-0937/`、`0930/`）里的脚本，复制后改版本号再用。
   - 不要盲跑 `Tools/release-installers.sh`（末尾会删历史包）。
   - 不要用 `Tools/Stage-WebGL.mjs`（会删旧 Build 文件）。
3. 原生包先传、验真，最后才改 latest 和 `app-version.json`。
4. 网页：保留旧 hash 文件，推 `cho-siren-web` 的 main（`git push origin HEAD:main`），等 Pages 完成再实际打开验收。按 `Docs/WEBGL-RELEASE-SAFETY-2026-09-05.md`。
5. 替换本机 Mac app、保留 Dock；按少爷指定安装 iPhone（不自动打开）。
6. 分渠道报告真实状态；没完成的渠道不能说已上线。发版后可跑 `python3 Tools/cos_prune.py --apply`。

## 4. 画图网站 enzoban.com（仓库 `~/XiaomiMiMoProjects/krea-studio-models`）

- Next.js（新版，改代码前读 `node_modules/next/dist/docs/`），部署在 Vercel 项目 `krea-studio`。手动部署：`npx vercel deploy --prod --yes`，没接 GitHub 自动部署。
- 域名在腾讯云 DNSPod，A 记录 216.198.79.1。广州实测打开约 0.25 秒，大陆可流畅用；`*.vercel.app` 在大陆被挡，朋友用 enzoban.com。
- 当前分支 `feat/vercel-gpt`（线上就是它），11-07 已快进合到 `main`，两者一致。
- 模型：
  - RunningHub Krea2 工作流：棕色尘埃、小小英雄、LOL 2.36MP V1.1（默认）、LOL 2.36MP、CherryCHE、局内视角。
  - Nano Banana 2.1（RunningHub 海外站）、Krea 3（Krea API，余额单独充）、GPT Image 2.6 Flare / Sunburst（OpenAI）。
  - 详见仓库 `README.md`。
- 存储：图片和记录在腾讯云香港桶 `enzoban-studio-1318245308`（ap-hongkong，`COS_PREFIX=root`），代码 `lib/cos.ts`。RunningHub 结果经腾讯云"搬图"函数（`FETCH_FN_URL` / `FETCH_FN_TOKEN`）搬进 COS。
- 登录与安全（11-06）：
  - 名字 + 邀请码登录，会话 cookie 365 天。
  - 店主账号（enzoban、enzo）必须用单独的店主码（Vercel env `OWNER_CODE`），邀请码登不进店主号。11-07 少爷自己定了新店主码，线上和本机文件都已换，旧码失效。
  - 旧的密码登录已关闭；同一 IP 10 分钟输错 8 次锁定；不登录所有接口 401。
  - 已知弱点（少爷没要求修）：朋友之间知道名字+邀请码就能互登；要防就得给每个账号加密码。
- 每日上限：每位朋友每天默认 30 张（北京时间零点重置，删了也算，失败不算），店主不限。管理页"设置"里改，0 = 不限。代码 `lib/quota.ts`，接口 `app/api/admin`。
- 管理页 `/admin`：用户、今天用量、邀请码、上限。
- 待办（可选）：
  - 过几天删掉广州旧副本（桶 `cho-siren-1318245308` 前缀 `enzoban-studio/`），以及香港桶里的测试对象 img-zz-test / img-zz-sgtest / img-zz-hktest。
  - 过几天看一下 Vercel 用量。
  - 搬家时有 2 张图（img-2118549259229036546、一张 ref）没搬成功。

## 5. 未完成 / 提醒

- 2026-11-13 前给两台 iPhone 重签重装。
- 0.9.39 已全量发布（11-07）；伯爵君已装 0.9.39，五条悟专用机待解锁后装。
- 手机上 GPT 换装、作曲的真实体验等少爷反馈。
- 上面网站的可选清理。

## 6. 秘密文件位置（只读路径，不读出内容）

- 腾讯云：`~/.cho-siren-cos.json`。
- RunningHub 海外站：`~/.cho-siren-rh-ai.key`；国内站在网站仓库 `.env.local`。
- OpenAI：`~/.openai.key`；Krea：`~/.krea.key`。
- GPT 测试码：`~/.cho-siren-gpt-test.code`。
- 网站邀请码：`~/.krea-studio-invite.txt`；店主码：`~/.krea-studio-owner.txt`；搬图 token：`~/.enzoban-fetch.token`。
- Android 签名：`~/.cho-siren/android-signing.json`；GitHub 用系统钥匙串。
- 网站线上变量在 Vercel（`vercel env add NAME production --force --yes --sensitive --value …`）。
