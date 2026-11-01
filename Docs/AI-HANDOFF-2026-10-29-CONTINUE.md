# CHO-SIREN 开发交接 · 2026-10-29

## 先读：真实状态

用户要求本轮问题全部修完上线，后来在手机连接排障中说「先这样吧」，原目标已明确暂停。本次只整理交接，未恢复开发。用户把本文交给你并要求继续后，再推进下列待办。

**当前不是全端同步完成：**

| 范围 | 2026-10-29 本次重新查询的状态 |
|---|---|
| 公开网页游戏 | **0.9.9 / R144**，尚未发布本地0.9.10 |
| Android、Windows、Mac安装包及腾讯云更新清单 | **0.9.10 已发布** |
| 官网安卓下载入口 | 已修复并发布，使用 GitHub Releases APK |
| 本地网页源码、WebGL包 | **0.9.10 / R145**，已暂存、测试，**未提交推送** |
| 伯爵君 iPhone | **没有安装成功**；0.9.10真机包已构建签名 |
| 另一台「五条悟专用机」 | 之前通过Wi-Fi安装过0.9.9，但启动被系统信任限制拦住；不能算正常试玩验收 |

公开网页：https://bojuejun.github.io/cho-siren-preview/

官网：https://bojuejun.github.io/cho-siren-preview/site/

原生更新清单：https://cho-siren-1318245308.cos.ap-guangzhou.myqcloud.com/app-version.json

## 用户的工作要求

- 尽快交付，日常回复尽量不超过30字，通俗直接。
- 小改只查直接相关文件，复用现成脚本，必要测试通过即推进；不要全库审计、顺手重构、重复检查或升级工具。
- 已授权本轮修复和发布，不要反复问是否继续、是否构建、是否上线。
- 独立工作可并行，但**同一个Unity工程同时只能有一个Editor测试/构建进程**。
- 原饰品UI保留「左侧竖排图标分类＋五列库存」，只修问题，不再改版。
- 不主动使用Devin；不要改、重启、切换用户的VPN/TUN。
- 不删除用户存档、旧发布包和无关改动。不要把构建、测试、截图、公开可达、实际设备操作混为一谈。
- 本文与最新用户决定优先于旧交接中0.3.x版本、旧待办和旧布局描述。

## 工程位置与工具

- Unity工程：`/Users/bojuejun/cho-siren-unity`
- 网页发布仓库：`/Users/bojuejun/cho-siren-web`
- 当前聊天工作目录：`/Users/bojuejun/Documents/Codex/2026-10-09/kan`，**不是Unity工程**。运行命令时明确设置工作目录。
- Unity：`/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity`
- 可用Node：`/Applications/Unity/Hub/Editor/6000.6.0f1/PlaybackEngines/WebGLSupport/BuildTools/Emscripten/node/node`
- Android SDK：`/Applications/Unity/Hub/Editor/6000.6.0f1/PlaybackEngines/AndroidPlayer/SDK`
- Xcode：27.1；开发工具路径 `/Applications/Xcode.app/Contents/Developer`。
- Unity工程原来就有大量未提交/未跟踪文件，禁止 `reset --hard`、`clean` 或全量提交。
- 网页当前本地分支仍叫 `release/v0.9.7-r142`，但内容已经推进。已发布main最新提交为 `e997a91e13934b0ba339d39ef59d4442b250b10c`（安卓下载修复），其前一提交为0.9.9的 `580e40b99dbb236df3bc49951dc9247078cb572e`。

## 本轮0.9.10已实现的修改

### 文字与公告

1. `Assets/Resources/Fonts/NotoSansSC-Subset.otf.meta`：动态字体保留，`fontRenderingMode` 从0改为1，即 **HintedSmooth**，让小字笔画贴合像素网格。
2. `Assets/Scripts/ChoSirenApp.cs`：主Canvas `pixelPerfect=true`，保留720×1536参考布局，不全局扩大控件。
3. `Assets/Scripts/UI/NativeTitleScreen.cs`：公告、适龄提示、更新说明正文改为24号常规字重；长文可滚动，异步取回新公告后内容高度能更新；更新面板只斜背景，正文保持水平。
4. `Assets/WebGLTemplates/ChoSirenPortrait/index.html` 和网页仓库 `index.html`：HTML公告正文14→16 CSS像素；普通1x桌面窗口使用2x渲染。仍保留3.2M像素预算，因此特别大的窗口可能被预算限制到低于2x，不能声称任何尺寸都硬性2x。

已查到的直接原因：原生公告原先16逻辑像素，在470像素宽窗口里只有约10像素；更新正文17号且父面板旋转，进一步影响中文细笔画。**这与之前0.9.9的图片导入质量修复是两件事。**

不能声称所有画面都已4K：一些旧角色原图只有512×704，部分文字已烘焙进图片；字体设置不能补出原图没有的细节。Windows实体机的主观可读性仍待用户确认。

### 名字输入光标

`Assets/Scripts/ChoSirenAppPlayerContract1028.cs`：设置青色闪烁光标，0.85Hz；布局完成后按实际Canvas缩放计算宽度，至少覆盖2个物理像素、最低3个逻辑单位。没有仅靠更换图片假装修复。

### 新玩家序章

- `Assets/Resources/Data/Story/prologue.json`：每条路线约6句、1次选择，共约141字，保留世界观、角色招呼、杂音来袭及开战。
- 保留 `prologue-accept`、`prologue-bold` 标记。
- `Assets/Scripts/StorySceneDirector1028.cs`：按排练室背景与 `alarm` 标签选择原有完整CG `cg-prologue-l4` / `cg-prologue-l24`，不再依赖删减后变化的行号，避免错图或独立角色硬贴回场景。

### 安卓下载

腾讯COS默认域名的APK **真实GET会403 DownloadForbidden**，而HEAD可能200，之前用HEAD验收不充分。

现改为同仓库GitHub Releases下载，保留原APK签名：

- 官网固定最新入口：`https://github.com/BOJUEJUN/cho-siren-preview/releases/latest/download/CHO-SIREN.apk`
- 当前固定版本：`https://github.com/BOJUEJUN/cho-siren-preview/releases/download/v0.9.10/CHO-SIREN.apk`
- 官网源：网页仓库 `site/index.html`，两处入口已提交、发布。
- `Tools/github_android.py`：读取现有Git认证发布APK，校验大小、SHA256及匿名Range GET字节，**不输出凭据**。
- `Tools/release-installers.sh`：继续向COS归档APK，但app-version.json里的Android下载地址改用GitHub Releases；其他平台仍走COS。
- COS账号未开通CDN，现有密钥也不能管理DNS/自定义域名；本轮没有购买服务、域名或修改VPN。
- GitHub下载在不同网络的可达性需实际确认，不能把当前有效链接说成所有国内网络都保证高速。

详细记录：`Docs/ANDROID-DOWNLOAD-FIX-2026-10-28.md`。

## 已有验证与不能混淆的失败

证据目录：`/Users/bojuejun/cho-siren-unity/Builds/qa-logs/readability-0910/`

- `editmode.xml`：**19/19通过**，序章、玩家档案、CG路由及字体导入设置。
- `playmode-retry.xml`：**5/5通过**，长公告滚到底、更新正文水平、空名字/有字光标真实显隐、序章报警CG、跳过至选择支保持完整CG。
- `web-check.log`：资源/哈希检查 `success:true`，**113/113 Node测试通过**；包含1x桌面渲染密度回归。
- 原生Android、Windows、Mac、WebGL及iOS构建均成功。Android v2签名、最终Mac ad-hoc签名验证通过。
- 原生公告已实际查看470×1010渲染图，正文更大、水平且清楚：`native-notice-final.png` / `native-notice-final-motion.png`。
- `native-name-final.log` 与 `native-notice-final.log` 均有 `CHO_SIREN_SMOKE_CAPTURE_OK`。名字另有 `native-name-final-burst-00.png` 等闪烁序列，接手时可直接查看。它们使用真实Mac Player渲染、合成Unity按钮回调，**不是Windows实机或物理触控验收**。
- 本地网页0.9.10已加载到标题，实际打开新版HTML公告并点击开始，停在转场附近；**尚未把本地新版完整走到输入、短序章、首页做最后浏览器验收**。浏览器没有error日志，仅旧文件系统同步API弃用warn。

保留了失败记录，不要改写为全绿：

- 首次编译因新测试用了旧版 `CanvasRenderer.GetMesh(mesh)` API失败，已改为当前 `GetMesh()`。
- `playmode.xml` 原整批17项为11通过、6失败。其中光标3逻辑单位在测试窗口不足1物理像素，已修并通过后续测试；另5项为旧Story布局期待（28号字/旧矩形高度），没有为让旧测试通过而重改现行剧情布局。上述5项并未在本轮全部修复或复跑，不能声称整类全过。
- 第一次 `native-name.log` 因首次升级自动公告阻挡标题退出而失败，其PNG不是有效名字预览；更新清单上线后重新运行公告、名字，最终 `*-final.log` 成功。不要引用前一次失败PNG当已验收名字框。

## 下一步优先做：把网页0.9.10发布收尾

**不必重做已经成功的构建，也不必重传已上线原生安装包。**

1. 看本地最终名字/公告PNG；继续实际浏览器核对名字框聚焦与闪烁、短序章和返回首页。之前本地HTTP服务为8098，目录是网页仓库；先检查是否仍在运行，别依赖旧tab ID或CUA变量。
2. 更新网页 `release.json` 的validation：目前仍是暂存脚本写的 `pending`。只记录实际完成证据，公开验收须等发布后再确认。
3. 网页仓库已准备的改动：`index.html`、`build-versions.json`、`package.json`、`release.json`、`scripts/test-loader.mjs` 和以下新Build四件。仅提交本轮文件，推送main及 `backup/v0.9.10`。

```text
Build/552a6fec9d25e30501427625d6d74b08.data.unityweb
Build/13ed719d529880f62a48d4edb1243039.framework.js.unityweb
Build/e4fd1a1da2b514d56b91acfcf931a535.wasm.unityweb
Build/0c970e782d6c0eb40221eab5823c4d9d.loader.js
```

4. 旧release备份为 `releases/R144.json`；检查是否被gitignore排除，确需随版发布时只对该文件显式添加。保留所有旧Build，不清理历史。
5. 等CI/Pages成功，核对公网release.json为 **0.9.10 / PSD-20261028-READABILITY-R145**，实际打开公告与游戏确认，不以本地结果冒充上线。
6. 写本轮正式发布记录，更新 `Docs/CLAUDE-CONTINUATION-2026-10-28.md` 顶部状态，明确手机安装是否完成。

新WebGL目录：`Builds/WebGL-0.9.10`。

已执行过的暂存脚本：`Builds/qa-logs/readability-0910/stage-web-0910.py`，旁边有receipt。**当前已经暂存，无需反复执行。**旧 `Tools/Stage-WebGL.mjs` 不理解当前retained历史结构，不能用它覆盖网页入口或删历史。

## 原生发布已经完成

- 成品：`Builds/Release-0.9.10/`，Windows/Mac原生目录分别 `Builds/Windows-0.9.10/`、`Builds/macOS-0.9.10/`。
- `native-package.log`、`native-publish.log` 记录构建/发布。
- 本轮仍使用文件差分更新：日志为Windows约**4.7MB新文件**、Mac约**4.8MB新文件**上传。完整安装器依然约数百MB，不能把安装包体积与老客户端本轮差分量混淆。
- 为保留旧包，本次分拆执行了 `Tools/release-package-0910.tmp.sh`、`Tools/release-upload-0910.tmp.sh`，没有执行正式脚本末尾cleanup。两个临时脚本还在，勿不看内容就重跑发布。
- `Tools/release-installers.sh` 末尾仍有COS及本地旧版本清理逻辑；下次发版继续按用户保留旧包要求处理，不直接运行全脚本然后清掉历史。
- `~/.cho-siren-cos.json`、Android签名配置和本机Git认证只能运行时读取，不复制到文档、仓库或输出。

## 伯爵君iPhone：当前阻塞与已有成品

目标是 **「伯爵君iPhone」iPhone16 Pro / iOS26.6.2**，不是「五条悟专用机」。

- 目标UDID：`00008140-000E706E2493C01C`。
- `devicectl` 记录为 `unavailable / paired`，重连4000、安装4016；开发者模式已开，用户也确认过，不要继续让他反复开。
- 两台手机确在同一Wi-Fi，目标的IPv4/IPv6及49152、62078端口都实测能连；**不能归因于不同Wi-Fi，也不能无证据归因于线坏、TUN或某把密钥损坏**。
- 新Device Hub的 `Pair Nearby Device` 从头无线配对要求iOS27+；目标是26.6.2，所以那个Waiting窗口不能作为可用重配办法。已有有效配对的26.x仍能无线连接，另一台能用不矛盾。
- 用户后来插了原装、以前用过的数据线，手机显示充电，但当时Mac的实时usbmuxd设备列表为空，USB系统报告只有控制器、没有手机；用户说没有「信任」提示。
- Mac「系统设置→隐私与安全→有线配件」是「新增配件时询问」。已查看、未改全局权限。让用户在两端解锁后重插并看Mac允许连接提示，最后用户说「先这样吧」，所以已停止排障。
- 未删除/重置伯爵君配对记录；未关闭安全限制；未改VPN或网络。根因没有确认，勿断言只是线坏。
- 一次重启过用户级CoreDeviceService，无效。不要继续重复同一失败命令或无限排查。

准备好的最新成品（不用重新从零构建）：

```text
Xcode工程：Builds/iOS-device/Unity-iPhone.xcodeproj
真机app：Builds/iOS-DerivedData-device/Build/Products/Release-iphoneos/CHO-SIREN.app
Bundle ID：com.bojuejun.chosiren
版本：0.9.10
```

该app使用免费开发签名，现有profile**到期时间为2026-10-30 08:58:38 UTC（北京时间16:58:38）**，本次重新读取确认。接手若已过期，应续签后装，不能把它当永久可用安装。TestFlight未配置、未购买开发者会员。

若用户明确继续手机安装并出现数据连接，可直接：

```sh
xcrun devicectl list devices
xcrun devicectl device install app --device 00008140-000E706E2493C01C /Users/bojuejun/cho-siren-unity/Builds/iOS-DerivedData-device/Build/Products/Release-iphoneos/CHO-SIREN.app
xcrun devicectl device process launch --device 00008140-000E706E2493C01C com.bojuejun.chosiren
```

安装成功后还要检查版本、启动、信任提示及实际画面，不能只凭安装命令成功说正常游玩。

另一台之前免费签名应用数达3个，用户明确允许卸载旧UI测试运行器 `ai.devin.bailian-r2probe.uitests.xctrunner`，该运行器已从五条悟手机卸载。**这不授权删除任何游戏本体或存档，也不授权调用Devin工具。**

进一步证据：`Builds/qa-logs/iphone-099/wireless-diagnosis-1028.md`、`Builds/qa-logs/readability-0910/iphone-device.json`、`iphone-install.json`、`ios-build.log`、`ios-xcode.log`。

Apple说明：https://developer.apple.com/documentation/xcode/managing-your-simulated-and-physical-devices-in-device-hub

## 避免重复做已经交付的0.9.9功能

五套可改名编队、成员筛选搜索、完整剧情CG路由、玩家对话头像同步、大厅唱片播放暂停、短大招合声、签名横线和关键原生纹理质量都已有实现。不要照旧交接的「仍未完成」表重复开发。

0.9.9详细记录：`Docs/RELEASE-2026-10-28-0.9.9.md`。本交接的0.9.10状态覆盖该文件中更早的手机/发布状态，但不覆盖它的功能事实与质量边界。

## 给接手AI的简短任务

先完成本地0.9.10网页的实际交互核对、发布和公网验证，使网页与已发布客户端一致；保留原UI和全部无关改动。手机仅在用户明确继续且出现可用连接后再装，不再让用户反复确认已授权工作。按事实报告完成与阻塞，快做、少说。
