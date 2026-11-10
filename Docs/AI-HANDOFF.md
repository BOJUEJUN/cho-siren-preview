# CHO-SIREN 与 enzoban.com 交接（唯一现行交接，2026-11-08）

本文件取代以前所有 `AI-HANDOFF-*`、`AI-COLLAB-HANDOFF`、`WORKSHOP-PRIVATE-PREVIEW-*` 文档（已删除，需要时可从 git 历史查）。各次发版记录仍在 `Docs/FULL-RELEASE-*.md` / `RELEASE-*.md`。接手时先实查一遍状态，别照抄本文数字当结果。

## 0. 先读

- 用户：称呼「少爷」，中文，回复极短、口语、不讲术语；他说的不一定对，可以纠正，把事做好。
- 本仓库 `AGENTS.md` 的产品约定全部有效（版本只往前 0.9.x、选中=实心青色、底部导航中文、首页两位角色不换等）。
- 禁止 Devin；不启用 DeepSeek Harness；同一时间只一个 AI 改这个工程，同一 Unity 工程同时只跑一个 Editor 进程。
- **不碰 Karing / VPN / 代理 / 路由 / 网络设置。**
- 浏览器：自己开新标签页做事，**不要把少爷正在看的页面导航走**。
- 手机：覆盖安装、保留存档、**不自动打开游戏**。
- 秘密只在运行时从本机文件读，绝不打印、提交、贴进聊天/文档/日志（清单见第 6 节）。拉下来的 vercel env 文件用完即删。

## 1. 游戏现状（2026-11-08）

- **0.9.46 已全量发布**（记录 `Docs/FULL-RELEASE-2026-11-09-0946.md`：工坊只剩换装 + 炫彩、库存材料分类）。0.9.45：`Docs/FULL-RELEASE-2026-11-09-0945.md`。0.9.44：`Docs/FULL-RELEASE-2026-11-08-0944.md`。0.9.43：`Docs/FULL-RELEASE-2026-11-08-0943.md`。之前 0.9.42（`Docs/FULL-RELEASE-2026-11-08-0942.md`）：团队/选秀/饰品手机实测修改、按钮回弹手感、三页统一背景、工坊工具精简。再之前 0.9.41/0.9.40：赵金 11-07 六页新美术 + 会议纪要落地 + 200 级 + 角色体力 + 累计更新公告。明细 `Docs/ART-FEEDBACK-2026-11-07.md`、数值 `Docs/BALANCE-1107-LV200.md`。下一版 **0.9.47**。
- 全量发布范围（11-08 晚起）：PC 下载 + 更新清单、本机 Mac 应用、手机。**网页不再更新**（性能不够），Mac 安装包和安卓包也不发；清单里 Mac 字段留空，Mac 客户端不会被提示更新。
- PC 更新改成分块补丁：大文件切约 1MB 小块，客户端只下改动的块（`AppUpdate1022.cs` 的 `ChunkPlan`/`Cdc`，上传端 `Tools/publish_files.py --previous <上一版 windows-stage>`）。0.9.39 客户端还用旧整文件方式，所以本次仍开着整文件上传；**0.9.41 起加 `--no-legacy-whole`**，每次只传几十到一两百 MB。
- 上传默认走腾讯云全球加速（11-08 开通，`Tools/cos_upload.py`；`COS_ACCELERATE=0` 关闭）；64MB 以上分块并发上传（`COS_PART_WORKERS`）。实测约 5MB/s。
- PlayMode 118 条改版前老界面测试已隔离（`Docs/TEST-QUARANTINE-1108.md`），其余全过；EditMode 全过。

| 渠道 | 版本 | 说明 |
|---|---|---|
| 网页（不再更新） | 0.9.41 / R171 | https://bojuejun.github.io/cho-siren-preview/ ，Pages 仓库 `/Users/bojuejun/cho-siren-web` main |
| 官网 | 已下线（11-08 少爷要求保密） | 原 `site/` 已从公开仓库删除，本机副本 `~/cho-siren-site-private/`；别再发布到公开地址 |
| Windows 下载 + 更新清单 | 0.9.46 | 清单 `https://cho-siren-1318245308.cos.ap-guangzhou.myqcloud.com/app-version.json`（mac 字段空；android 指 GitHub v0.9.40 APK） |
| Mac 下载 | 0.9.39（不再更新） | 官网已下线 |
| 本机 `/Applications/幻域魅声.app` | 0.9.46 | 11-09 替换，Dock 入口保留 |
| 伯爵君 iPhone | 0.9.46 | 11-09 覆盖安装（无 GPT 测试码，存档保留）；描述文件 11-13 到期 |
| 五条悟专用机 | 0.9.42 | 11-08 覆盖安装，同上 |

- 源码分支 `release/music-0.3.5-20260909`（名字老，内容是最新），`ProjectSettings` 是 0.9.46 / Android 946（11-09 全量发布）。私人 0.9.38 代码 `8f09814`（工坊编辑页 + Suno 作曲），11-07 已推送到远端（仓库是公开的）。
- 工作树里有与本任务无关的老改动（`Docs/FULL-RELEASE-2026-11-02-0930.md`、`Packages/*`、`chosiren-font-unicodes.txt`、`ResourcesArchive/`、`chpl26g2`），保留，不要清理或顺手提交。
- 公开版 0.9.39 = 0.9.38 的工坊编辑页 + 作曲按钮隐藏（作曲只在带测试码的私人包显示）。下一次正式发版用 **0.9.40 或更高**，打公开包前**确认 `Assets/Resources/AiGptTestCode.txt` 不存在**（它是 gitignore 的私人开关，有它就会走 GPT 测试服务）。

### iPhone 签名

- 少爷 11-06 在 Xcode 登录了 Apple 账号，现在每次发版都用自动签名打包。
- 最新包：`Builds/iOS-DerivedData-device-0.9.46/Build/Products/Release-iphoneos/CHO-SIREN.app`（工程 `Builds/iOS-device-0.9.46/Unity-iPhone.xcodeproj`）。
- 0.9.43 及更早的包已删除（11-09，硬盘满了）。
- 新描述文件含两台设备，**2026-11-13（周五）到期**，到期前要再重签安装，否则手机打不开。
- 重签：`xcodebuild -project <工程> -scheme Unity-iPhone -configuration Release -destination generic/platform=iOS -derivedDataPath Builds/iOS-DerivedData-device-<版本> -allowProvisioningUpdates DEVELOPMENT_TEAM=63K7K9XRZ2 CODE_SIGN_STYLE=Automatic build`。报 "No Accounts" 就请少爷在 Xcode → 设置 → Apple 账号登录。
- 安装 / 查版本（不加 launch）：
  - `xcrun devicectl --timeout 120 device install app --device <UDID> <app路径>`
  - `xcrun devicectl --timeout 20 device info apps --device <UDID> --filter "bundleIdentifier == 'com.bojuejun.chosiren'" --json-output <文件>`
- 设备 UDID：伯爵君 `00008140-000E706E2493C01C`；五条悟专用机 `00008140-001154D23E93001C`。Bundle `com.bojuejun.chosiren`，team `63K7K9XRZ2`。别装不带版本号的旧 `Builds/iOS-DerivedData-device/`。
- 历史上手机连不上（CoreDevice 4016）曾是 Wi-Fi 代理例外列表格式问题，证据在 `Builds/qa-logs/full-release-0930/`。再遇到先只读排查，不要改网络。

- 游戏歌单（11-09 少爷定）：光のルール、輝きを追いかけて、BLACK ROSE A/B，其他新歌先拿掉。`Tools/sync_liked_songs.py`（把选歌台的喜欢同步进游戏）已经从打包脚本里去掉了，少爷没让加新歌之前不要跑。

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

### 编辑页（0.9.46 起）

- 少爷 11-09 要求重新设计：底部只剩 **换装** 和 **炫彩** 两个宽按钮。
- 背景、转身、半身/全身都不要了，固定全身视角。
- 只能切换已签约成员，默认打开队长。
- 炫彩：
  - 7 种配色，表 `ChoSirenApp.WorkshopChromas1109`。
  - 着色器 `Resources/Shaders/UIChroma1109.shader` 负责静态立绘；`UIStackedAlpha` 里也加了同样的换色，给待机视频用。
  - 鲜艳的衣服、头发换成目标色，皮肤保持不变。
  - 存在 `WorkshopLook1104.Chroma`。
  - 截图：`Builds/qa-logs/topup-1104/workshop-chroma*.png`。
- 滤镜、应援色、亮度、音高、律动、背景、转身的代码都还在，只是不显示。测试会直接调用它们。

### 作曲（只在私人测试包显示）

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
  - 练习室（11-09）：舞台打光标出主唱；平时待机呼吸，开始练习才唱跳。
- 改完游戏后，跑 `bash Builds/qa-logs/phone-test-1108/mac-and-phone-0945.sh`（后台跑），会一起做三件事：
  - 重建并替换本机 Mac app；
  - 装到伯爵君手机；
  - 打印手机上装的版本。
- 库存（11-09）：
  - 底部导航第 4 个改名“库存”。
  - 新增“材料”分类：饰品页只显示持有的，图鉴页显示全部。
  - 材料按稀有度排序（声核 > 生魂 > 其他）。

### 正式发版流程（少爷说"发布"时）

1. 定新版本号（只往前），先确认没有别的 AI 在构建；只提交本次文件。
2. 复用 `Builds/qa-logs/full-release-0946/` 的脚本。
   - 复制整个目录，把 `0.9.46` 改成新版本号，`Release-0.9.45`（上一版，用来做补丁）改成 `Release-0.9.46`。
   - 然后跑 `all.sh`：先测试，再打 Windows / Mac / iOS 包，然后传 PC，最后装本机 Mac 和伯爵君手机。
   - 范围按 `AGENTS.md`：网页、dmg、APK 都不发。
   - 旧版整份文件必须保留，旧客户端才能自动更新（少爷要求，不能加 `--no-legacy-whole`）。
   - 上传一个一个传（并行反而更慢）。
   - 发版前先 `df -h` 看硬盘：一次构建大约要 15 GB。不够的话，**先问少爷**，再删 `Builds/` 里的旧版本目录。
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

- **2026-11-13 前**给伯爵君重新打包安装（描述文件到期）；五条悟那台只在少爷说要装时才装。
- 0.9.46 之后还没做：
  - 战斗镜头更夸张（放大后拉远一倍）。
  - 一屏放三个怪，镜头跟着攻击者平移。
- 后期玩法（通关两章后玩什么）：两轮提议少爷都觉得一般，等他定方向。
- 歌：
  - 另一个会话在用 RunningHub 跑 Suno 4.5 批量出歌。
  - 选歌台是本地 `localhost:8777`。
  - 少爷没让之前，不往游戏里加新歌。
- 手机上 GPT 换装、作曲的真实体验，还在等少爷反馈。
- 上面网站的可选清理。

## 6. 秘密文件位置（只读路径，不读出内容）

- 腾讯云：`~/.cho-siren-cos.json`。
- RunningHub 海外站：`~/.cho-siren-rh-ai.key`；国内站在网站仓库 `.env.local`。
- OpenAI：`~/.openai.key`；Krea：`~/.krea.key`。
- GPT 测试码：`~/.cho-siren-gpt-test.code`。
- 网站邀请码：`~/.krea-studio-invite.txt`；店主码：`~/.krea-studio-owner.txt`；搬图 token：`~/.enzoban-fetch.token`。
- Android 签名：`~/.cho-siren/android-signing.json`；GitHub 用系统钥匙串。
- 网站线上变量在 Vercel（`vercel env add NAME production --force --yes --sensitive --value …`）。
