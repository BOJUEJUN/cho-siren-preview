# CHO-SIREN 本机 AI 开发交接

更新日期：2026-11-03。适用对象：在这台 Mac 上继续开发游戏的其他 AI。

**当前基线已推进到 0.9.32。完整 Unity 源码就在本机，不需要向用户重新索要源码或 API key。** 本文最初整理于30版，下面新增的0节覆盖旧版渠道、手机状态及界面规则；其余历史实现入口仍可用于定位。设备连接与线上状态会随时间变化。

## 0. 32版最新状态和覆盖项

- 代码为 `bfe529b`（32），包含 `b928506`（31）；Android code932，Unity仍6000.6.0f1。当前完整发布记录见 `Docs/FULL-RELEASE-2026-11-03-0932.md`。
- 31调整操作/选中态、顶部货币和档案、角色种族与专属技能名；32修复主操作按钮文字可读性。新的“青色实心表示已选中，操作按钮青色描边”规则优先于旧版样式描述。
- Web32/R164已上线，Android32已发布；Windows/macOS包、三latest别名及COS总更新清单均已32。本机桌面和Dock已32。
- 伯爵君、五条悟两台手机均已覆盖安装并分别查询到32，未启动、未清档；下文30版时的连接失败和五条悟待安装只是历史记录。
- 本次Wi-Fi例外已正确，无需改网络；iOS签名期限仍2026-11-06 11:34:38 UTC。
- Unity源码bfe529b已同步到远端原源码分支，未强推；Web32已实际进入大厅、地图和战斗，锁关点击/拨碟/挑战拦截、章节提示、换队和主按钮可读性通过。未验完整对局，详情见32版发布记录。
- Web原32/R164部署已成功；本次最终运行结果单独保存在QA，未为validation文字再发一次Pages，因此旧staged/pending字样不代表32未发布。
- 最新原生/设备证据在 `Builds/qa-logs/full-release-0932/`；Web在 `/Users/bojuejun/Documents/Codex/2026-10-09/kan/work/web-release-0932/`。

## 1. 接手后先做什么

1. 到 `/Users/bojuejun/cho-siren-unity`，读取当前 `AGENTS.md`，执行 `git status --short`、`git log -5 --oneline`。
2. 先读本文，再按任务读相关文件；不要从九、十月份旧交接重新开发已经完成的功能。
3. 本机有完整游戏源码、素材、构建、发布脚本和测试证据。聊天所在的 `kan` 目录、网页发布仓库、HTML 设计样机都不是完整游戏工程。
4. 保留所有已有脏改动。不要执行 `git reset --hard`、`git clean`、全量覆盖、全量 add/commit，也不要随意升级 Unity、Packages、SDK。
5. 用户给出具体任务后，只检查相关模块，改完做必要验证，随后交付。**本文不是要求再次全库审计、重跑全部构建或重新上传 30 版。**

可直接复制的开工命令：

```sh
cd /Users/bojuejun/cho-siren-unity
git status --short
git branch --show-current
git log -5 --oneline
```

### 旧文档中已失效的内容

- `AGENTS.md` 顶部仍写“latest 2026-10-20 / 0.3.87”和保留 `0.3.x`，这是旧描述；当前是 **0.9.32 / Android 932**，不能倒退版本。
- `Docs/AI-COLLAB-HANDOFF.md` 中五骰子、顶栏布局、饰品旧设计、全量测试要求等含更早历史，不能覆盖用户后来的六骰子、简化 UI 和最短交付要求。
- `Docs/AI-HANDOFF-2026-10-29-CONTINUE.md` 的 Web 0.9.9、手机未装成功是历史快照，已被下文覆盖。
- `Docs/WEB-REVIEW-1102-WORKING.md` 是 25 版 Web 先行验收记录，功能信息有用，里面“尚未全端发布”“原生保持24”等状态已过时。
- 网页仓库的 `HANDOFF.md` 仍写0.3.16；不能据此选版本、分支或发布包。
- `Docs/BALANCE-1101.md` 和部分旧充能注释不等于现行规则；应读 `ChargeOrder1101.cs`。
- 更早的 `install-bojuejun-final.json` 记录失败；最终成功证据是 `install-bojuejun-after-authenticated.json`，不要读错先后顺序。

## 2. 用户工作习惯和已经确定的方向

### 执行与沟通

- 日常回复尽量不超过30字，直接、通俗；状态只报告真实进展。
- 永远记得快：小改只查直接相关文件，复用现成脚本，独立任务安全并行，不顺手重构、重复验证或升级工具。
- 已授权的修改、必要检查和同一范围发布不要反复问确认。新付费任务、额外范围以用户当前授权为准，不把旧预算当无限额度。
- 不主动使用、推荐、修复或恢复 Devin；不自行启用 DeepSeek Harness。
- **同一个 Unity 工程同时只运行一个 Editor 测试/构建进程。** 可并行做只读调查、独立美术、上传和资料整理；不要多个 AI 同写共享文件。
- 手机安装保留存档，**装好即可，不自动启动游戏**。不要为排障卸载游戏、清配对或清存档。
- VPN/路由先只读定位，不能因为同 Wi-Fi 连不上就盲目切节点、断 VPN、改路由或要求用户反复操作。下文有已成功修过的具体问题。

### 视觉与产品约束

- 风格为现有紫黑、青色、少量粉色、锐角切面、撕纸拼贴、网点的偶像音乐游戏；不是随意一张赛博城市图，也不是满屏霓虹和装饰。
- 用户要求整体更简约、字少、层级清楚。统一的是同类元素的设计语言，不是把所有信息都画成同一个可点击框。
- 队伍名可编辑；队伍战力是读数。名字旁用小笔，闪电靠近战力数字，不能相隔太远；不要额外加硬矩形黑底。
- 左上角头像沿当前认可的 C 框方向，头像、姓名、等级、战力要像同一个功能区，字清楚、头像看得见，不能擅自大改顶栏。
- 关卡采用 **B 右侧半光碟**；保持正圆，右边贴实际游戏视口边缘，不能压扁或因安全区留出缝。当前暂用 G 光碟，用户仍认为可以更好。
- 光碟只显示部分较大关卡节点，可点选，也可鼠标/触屏跟手拨动、惯性吸附；背景能看见，节点不是全十关挤成一小圈。
- 每关有符合名称的独立背景，背景简洁、属于本游戏美术风格；不是同一图换字，也不是把花图整体压暗。
- 地图队伍栏采用 **A 大圆头像**。四人头像和名字紧凑，栏宽与下方关卡卡片约一致；换队按钮在同框右侧，双向切换箭头，不是刷新图标或孤零零漂在外面。
- 保留扫荡、章节奖励、章节任务、体力与真实解锁逻辑。章节入口应打开可扩展选择列表，不是点一次直接轮换章节。
- 锁章、锁关使用锁图标；锁关仍可拨到并查看，点击提示条件，不能挑战或扣体力。
- 角色详情左右切换用简约半透箭头，不要厚重按钮遮住属性；选秀支持左右滑动。
- 头像选择四列、面部聚焦、超高可上下滚动，关闭按钮固定；拖动滚动不能误选。
- 战斗底部角色默认胸像足够大，攻击时可放大出框；血量/蓝条不能被挤乱。
- 小技能特写用有设计的非规则切边、单侧错位亮边。禁止全身统一荧光描边、平直矩形底边；进入、保持、收回全程都要贴边裁切，不只改静止帧。
- 角色与剧情场景要直接生成自然完整画面，光影、透视一致，不接受把旧全身立绘硬贴到背景。
- UI/贴花必须有真实 RGBA Alpha，不把棋盘格当透明；保持原始比例，不用全局拉伸或虚假的锐化掩盖低分辨率源图。
- 骰子只给成型组合的参与骰显示沿边特效，特效位于骰面上层；不要大圆环或向上箭头光柱。
- 重投有库存才伸出来，没库存收回；显示“重投×N”，边缘噪波沿真实轮廓走，可略大一点，不能整块形状对不上。
- GOOD/GREAT/PERFECT 与游戏字形风格统一；PERFECT 不带两个感叹号。
- **练习室暂时不要改。** 用户最后说当前游戏版本挺好，团体一起升级的想法先搁置；旧设计预览不代表已批准合入。
- 饰品保持用户认可的左侧竖分类图标和原库存排版，只修问题，别再次大改布局。
- 首页保留用户原选两首日文歌曲；不要擅自加入中文歌、恢复 Neon Moonwalk 或显示2/2之类计数。新 BGM/语音先试听，按反馈迭代。

## 3. 工程、账号、分支与当前脏改动

GitHub账号：**BOJUEJUN**。

| 内容 | 本机位置 / 当前快照 |
|---|---|
| 完整 Unity 工程 | `/Users/bojuejun/cho-siren-unity` |
| Unity 源码分支 | `release/music-0.3.5-20260909`，跟踪 `origin/release/music-0.3.5-20260909`；分支名老，不代表版本仍0.3.5 |
| Unity 游戏提交 | `bfe529b`（32），包含 `b928506`（31）；文档提交另见git log。30版 `728d652`、29版 `a2d31ef`仅作历史定位 |
| Git remote | `https://github.com/BOJUEJUN/cho-siren-preview.git`；本交接针对本机现有 checkout，不保证任意旧远端分支都含最新素材 |
| WebGL/官网下载发布仓库 | `/Users/bojuejun/cho-siren-web` |
| Web 本地分支 | `release/v0.9.7-r142`，无 upstream；实际32/R164 |
| Web 游戏发布提交 | `e195877cb989c5b7e4ae8f56b3c2889856e8a62c`已在远端main；后续验证元数据提交另见git log |
| 辅助工作目录 | `/Users/bojuejun/Documents/Codex/2026-10-09/kan`，包含预览、素材流程和发布辅助脚本 |
| 最新独立设计样机源码 | `/Users/bojuejun/Documents/Codex/2026-10-09/kan/outputs/cho-map-polish-site-20261102`，HEAD `66efbeeee8c121846bb096d0587c473cbb9ddb6d` |
| 不要误用的旧样机 | `/Users/bojuejun/Documents/Codex/2026-10-09/kan/outputs/cho-mobile-design-preview-20261102` 是Sites第2版，不是最新第3版 |

开始写本文时 Unity 工作树已有以下改动；不得清理或顺手提交：

```text
 M AGENTS.md
 M Docs/FULL-RELEASE-2026-11-02-0930.md
 M Packages/manifest.json
 M Packages/packages-lock.json
 M chosiren-font-unicodes.txt
?? ResourcesArchive/
?? chpl26g2
```

本文新增为 `Docs/AI-HANDOFF-2026-11-03-CONTINUE.md`，不代表以上其他文件属于本次交接修改。Web工作树另有未跟踪 `releases/R159.json`，保留。

Unity、网页两个目录是同一远端仓库的不同用途分支：**不要把完整 Unity 源码直接推到 Pages 的 main，不要用网页目录替代游戏工程。**

## 4. 30版历史渠道快照

下表留作历史证据定位。**当前已是32，已完成的升级覆盖项读第0节和32版发布记录，不再以此表判断今天是否安装成功。**

| 渠道 | 截至本次交接的状态 | 证据与边界 |
|---|---|---|
| 正式游戏 Web | **0.9.30 / UI-20261102-AVATAR-MAP-R162 已上线** | 本次重新GET清单；Nov2真实浏览器运行通过，0 page/console/资源错误 |
| 宣传官网/下载页 | HTTP200，仍为独立 `/site/` | 官网200不等于游戏实际运行验收 |
| Windows / macOS / Android公开包 | **0.9.30 已发布** | 本次重新核对五个包的首64KB及总长；原发布有完整验真记录 |
| 桌面差分更新清单 | Windows/Mac各504文件，latest和app-version均30 | “小包更新”指已安装客户端的文件差分，完整安装器仍数百MB |
| 本机 `/Applications/幻域魅声.app` | **0.9.30**，严格codesign通过，原Dock入口保留 | 发布时原位替换、备份旧29，未自动启动游戏，未动存档 |
| 伯爵君 iPhone | **Nov2已安装并设备端查询到0.9.30** | 今天Nov3再次只读查询报4016，表示当前连接不可用，不表示昨天没安装或游戏被卸载 |
| 五条悟专用机 | 最新30安装尚未确认 | 先前安装/在线记录不能代替当前30的安装证明 |
| Windows/Android真机试玩 | 本轮没有新增实机画面/操作验收 | 构建、签名、包可下载不等于目标实机试玩通过 |
| 独立Sites设计预览 | 第3版部署成功，属于HTML样机 | 不是真实游戏；当前custom访问仅owner，其他AI/手机可能打不开 |

链接：

- 游戏：<https://bojuejun.github.io/cho-siren-preview/?v=0.9.30>
- 官网：<https://bojuejun.github.io/cho-siren-preview/site/>
- 原生更新清单：<https://cho-siren-1318245308.cos.ap-guangzhou.myqcloud.com/app-version.json>
- Android30：<https://github.com/BOJUEJUN/cho-siren-preview/releases/download/v0.9.30/CHO-SIREN.apk>
- 独立设计预览：<https://cho-design-preview-1102.good-slug-5392.chatgpt.site/>
- 历史动画实验：<https://bojuejun.github.io/cho-siren-preview/motion-lab/>

`www.chosiren.com` 本次HTTPS出现TLS EOF，当前绑定未核实；不要把它写成可正常访问的正式入口。旧EdgeOne记录也不是今天发布状态的证明。

### 最新证据应读这里

- 发布汇总：`/Users/bojuejun/cho-siren-unity/Docs/FULL-RELEASE-2026-11-02-0930.md`。
- 原生与测试：`/Users/bojuejun/cho-siren-unity/Builds/qa-logs/full-release-0930/`。
- Web：`/Users/bojuejun/Documents/Codex/2026-10-09/kan/work/web-release-0930/`。
- Pages job `41498909618` 的build/deploy成功；本地网页检查113项通过。
- **Quality job `41498910595` 在checkout阶段取消，未执行verify；不能写“所有CI全绿”。**
- 公网实际交互结果在 `public-runtime.json`；不要用早期 `github-actions.json` 的in_progress覆盖最终结果。

## 5. 当前已落地功能与代码入口

下面的文件均相对完整工程 `/Users/bojuejun/cho-siren-unity`。项目大量使用 partial class；先查调用方，再改对应分文件，避免整份重写主面板。

| 模块 | 先读的文件 |
|---|---|
| 应用入口 / 主Canvas | `Assets/Scenes/Main.unity`、`Assets/Scripts/ChoSirenApp.cs` |
| 游戏数据、存档、角色进度 | `Assets/Scripts/GameModel.cs`、`GameModelProfile1028.cs`、`GameModelSaveTransfer1021.cs`、`Assets/Resources/Data/` |
| 大厅和顶部档案区 | `Assets/Scripts/ChoSirenAppLobbyPunk.cs`、`ChoSirenAppLobby1031.cs`、`ChoSirenApp.VisualA1101.cs` |
| 玩家头像统一解析 | `Assets/Scripts/UI/PlayerAvatar1102.cs`、`Assets/Scripts/ChoSirenAppAvatarPicker1101.cs`、`Assets/Resources/Data/portrait-faces.json` |
| 面部裁剪 | `Assets/Scripts/UI/PortraitFace1101.cs`，成员头像复用同一套Head framing |
| 团队预设 / 改名 | `Assets/Scripts/ChoSirenAppTeamPresets1028.cs`、`ChoSirenAppPlayerContract1028.cs` |
| 半碟关卡地图 | `Assets/Scripts/LevelMapPanel.cs`、`LevelMapPanel.Vinyl1102.cs`、`UI/VinylStageWheel1102.cs` |
| 关卡场景图 | `Assets/Scripts/UI/StageSceneArt1102.cs`；`Assets/StreamingAssets/LevelScenes1102/`；光碟 `Assets/Resources/Art/LevelMap1102/vinyl.png` |
| 角色详情 / 成员选择 | `Assets/Scripts/ChoSirenAppMemberDetail1101.cs`、`ChoSirenAppMemberPicker1101.cs`、`ChoSirenAppRosterVisuals.cs` |
| 饰品 / 库存 | `Assets/Scripts/ChoSirenAppInventory.cs`、`ChoSirenAppInventory1101.cs`、`ChoSirenAppReferenceEquipment038.cs` |
| 练习室 | `Assets/Scripts/ChoSirenAppTraining.cs`，现阶段不重做 |
| 选秀滑动与签约 | `Assets/Scripts/Panels/GachaPanel.cs`、`GachaPanel.AuditionSwipe1102.cs`、`Assets/Scripts/UI/AuditionSwipe1102.cs` |
| 战斗表现主面板 | `Assets/Scripts/Panels/TacticsBattlePanel.cs`及其各partial文件 |
| 战斗规则 / 结算 | `Assets/Scripts/Systems/Tactics/RealtimeBattle.cs`、`BattleSimulator.cs`、`ChargeOrder1101.cs`、`CaptainPassive1101.cs` |
| 骰子 / 重投 | `Assets/Scripts/Systems/Dice/DiceTurn.cs`、`DiceRules.cs`、`DiceEndless1101.cs`；`Panels/TacticsBattlePanel.Dice1031.cs`、`TacticsBattlePanel.DiceEndless1101.cs` |
| 编队跟手拖动 / 计时充能 | `Assets/Scripts/Panels/TacticsBattlePanel.Formation1102.cs` |
| Boss完整入场视频 | `Assets/Scripts/UI/BossEntrance1102.cs`、`Panels/TacticsBattlePanel.BossEntrance1102.cs` |
| 大招完整视频 / QTE | `Assets/Scripts/UI/BattleUltimateShow1020.cs`、`Panels/TacticsBattlePanel.UltimateGrade1031.cs`、`TacticsBattlePanel.Turn1020.cs`；`Assets/Resources/UltimateVideos/manifest.json` |
| 技能贴边特写 / 默认胸像放大 | `Assets/Scripts/UI/BattleActiveCloseup1019.cs`、`BattleMemberAlbum929.cs`、`HeroCardLunge1031.cs`；`Panels/TacticsBattlePanel.BattleFx1101.cs` |
| 成型骰子沿边特效 | `Assets/Scripts/UI/DiceScoreFire1102.cs`、`DiceScoreAura.cs`，调用在TacticsBattlePanel |
| 统一判定字形 | `Assets/Scripts/Panels/TacticsBattlePanel.UltimateGrade1031.cs`及相关UI组件，搜索GOOD/GREAT/PERFECT/MISS |
| 剧情场景 / 对话 | `Assets/Scripts/StorySceneDirector1028.cs`、`Assets/Resources/Data/Story/`，对话面板通过PlayerAvatar1102取制作人头像 |
| BGM / 录制音效 / 对白 | `Assets/Scripts/GameAudio.cs`、`GameAudio.Music1101.cs`、`GameAudio.UiSfx1102.cs`、`GameAudio.Dialogue1102.cs`、`UI/DialogueVoice1102.cs` |
| 原生自更新 | `Assets/Scripts/UI/AppUpdate1022.cs` |
| 构建入口 | `Assets/Editor/ChoSirenBuild.cs` |

### 29至30版最近修改

- A版大圆头像关卡栏490宽，头像88，换队双箭头收进右侧，有轻底签/分隔，不再孤立漂浮。
- 光碟可点击、跟手拖动、惯性吸附，显示部分大节点；贴实际视口边界。锁关可预览，挑战按钮与执行回调双重拦截，不扣体力。
- 章节弹窗可扩展，用锁图标、显示解锁条件，点击锁章不穿透。
- 雾白小技能贴脸图单独左移64/720设计单位裁掉用户指出的左侧部分，保留比例和贴边行为，其他成员不变。
- `PlayerAvatar1102.Resolve`：有效成员头像优先，否则保存的预设头像；首页、剧情制作人、档案、合约统一使用。
- 换预设头像清除旧成员头像ID；当前剧情句和打开中的档案也可即时刷新，普通剧情角色头像不受影响。
- 头像列表四列可滚动、固定关闭按钮；成员详情简约箭头；选秀左右滑动；左上信息放大；名字输入框背景也可重新点击聚焦。
- 签约券可用于签约；成功页档案在左、继续在右；界面仍保留签约、角色入库等真实逻辑。
- 录制音效替换简单嘟声；大厅与剧情缺失对白按当前文本补录并精确映射。

## 6. 现行战斗规则，避免按旧PPT/注释改错

### 六骰子与重投

- 新战斗固定六骰子，创建同一个 `DiceTurn` 并 `Begin()`；开场摇一次，不白送重投库存。
- `DiceEndless1101`：初始库存0，最多存3次，每次己方行动充34%；消费后可继续积攒，**不是整场只能3次**。旧历史说明里的限制不能直接恢复。
- 存满溢出丢弃；当前骰子攻击加成封顶+5%。UI有库存显示“重投×N”，没库存收回。
- `DiceRules`评估最强五骰子集并标记Participating；第六个不参与的杂骰不应有成型光环。
- `ShowDiceAuras`需`tier > 0 && Participating`才显示光效。旧五骰高点兼容分支仍保留轻色调；正常六骰新战斗不会产生HighPoint，不要据此误报当前必现问题。

### 位置、队长、充能与大招

| 位置 | 基础充能时间 | 速度比例 |
|---|---:|---:|
| 第1位 | 12秒 | 100% |
| 第2位 | 15秒 | 80% |
| 第3位 | 20秒 | 60% |
| 第4位 | 30秒 | 40% |

- 这是**有效战斗时间**。暂停、Boss入场、引导阻塞、大招视频等不涨能量；1至3倍战斗速度会相应推进。
- 队长是指挥身份，站位是另一个维度。`TrySwapCaptain`只换标记和加成，不把角色搬到第一格、不补能量；保留15秒队长切换冷却。
- `SwapFormationPositions1102`交换站位，已有能量保留；后续按新位置速率充能，卡片跟鼠标/触屏拖动。
- 只有队长享受骰型充能小加成：三条/葫芦/双三条/顺子/六连顺+4%；四条/五条/六同+5%；其他无加成。
- 六连顺且队长处于第一位时约 `12 / 1.04 = 11.538秒`；不是全队都11.5秒，也不是固定减去0.5秒后再重复叠加。
- `DiceMultiplierFor`：队长享完整骰子攻击加成；人鱼队友分享2/3，魔族/血精灵队友1/2，魅族不额外分享。种族被动和上限读 `CaptainPassive1101.cs`，不要在UI再乘一次。
- 手动模式下普攻/小技能继续自动执行；满能大招由点成员卡或上滑唱片排入下一行动间隙。
- 大招只消耗本人100能量，队友能量保留；当前每人每场最多3次大招。
- 用户给的夜莺视频要完整播放：等待`loopPointReached`；不因QTE结束或manifest四舍五入时长提前淡出。存在解码卡死的时长+8秒兜底；无素材、减少动态效果或准备超过2.5秒时走立绘回退。
- Boss大/小入场同样完整播放，入场时战斗和充能停止。

这些已有代码和历史自动验证，下一次相关改动仍要在用户手机1倍速下看节奏、拖动、能量和完整视频尾帧；不能只报源码存在。

## 7. 音乐、语音、素材与存档

### 语音与音效

- 语音主要为日语，中文字幕；沿用用户选定B版四角色声线，新增制作人成熟男声和低调女旁白。
- Nov2新增98段 MiniMax speech-2.8-hd：6段首页、92段剧情。当前首页13句、剧情150条say均有映射；这是文件和映射覆盖，不等于用户认可全部情绪演出。
- `Docs/DIALOGUE-VOICE-1102.md`、`Docs/DIALOGUE-VOICE-1102.json`记录本轮，manifest在 `Assets/Resources/Audio/Voice/dialogue-1102.json`。
- 首页音频在 `Assets/Resources/Audio/Voice/Home/`；剧情增量在 `Assets/StreamingAssets/Voice/Story/v1102-*.mp3`。
- 匹配脚本、索引、说话者和中文原文；改台词后要重做对应录音，不能错播旧句。动态玩家名字的日语录音自然称“プロデューサー”，不要现场系统朗读。
- 自动剧情等待录音加载与结束，跳过非对白指令预取，避免中文字幕读完就切掉日文尾音。
- 本轮只做响度规范：-17 LUFS、-1.5 dBTP、44.1k单声道MP3，不靠加速、变调或机械提示音伪装情绪。
- UI/拨碟采用现成录制Kenney CC0等素材；准确调用见 `GameAudio.UiSfx1102.cs`。不要恢复740Hz一类简单beep兜底。
- 既有生成工具在 `Tools/audio1031/`、`Tools/audio/`。`rh1031.py`通过本机既有私密来源运行时读取国/国际站key，包含报价预览和ledger；不要把其历史批次预算直接当新授权。
- 后续语音先做代表角色/情绪样本试听，认可后扩整批；用户反复指出“没有力量、情绪不足”，自动ASR可懂度不等于演出达标。

### 素材与场景

- `Assets/StreamingAssets/LevelScenes1102/`有20张关卡独立JPEG；`StageSceneArt1102.Show(stageId)`只加载所选关卡图，别把20张全部塞回启动Resources包。
- 当前光碟为 `Assets/Resources/Art/LevelMap1102/vinyl.png`，G方案暂用；其他候选只是实验，不要自动替换。
- 用户原始Boss素材曾在 `/Users/bojuejun/Downloads/大 BOSS.mp4`、`/Users/bojuejun/Downloads/小 Boss.mp4`；开发以项目内已批准的manifest和打包资源为准，不依赖微信临时目录永远存在。
- PSD和最新用户截图是美术依据；需要PSD还原时读取本机 `psd-ui-handoff` skill，忠实还原，不把旧参考当最新定稿。

### 存档

- 主存档key：`ChoSiren.Save.v2`；旧key：`ChoSiren.Save.v1`。迁移逻辑在 `GameModel.cs`，传输在 `GameModelSaveTransfer1021.cs`。
- 当前生产使用 `PlayerPrefsSaveStore`；模型支持注入隔离store做测试。QA请用独立测试存档，不改用户真实进度。
- 本机macOS实际偏好文件存在：`/Users/bojuejun/Library/Preferences/com.bojuejun.chosiren.plist`。不要直接覆盖或删除。
- iPhone、桌面、Web的本机存档不是因为用了同一个Git仓库就自动同步；Web浏览器证据也不是手机存档证据。
- `public-state.json`是自动验收用瑶光头像、双编队、锁关场景的测试数据，不是用户真实存档。

## 8. 必要测试和本机工具

工具：

```text
Unity：/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity
Unity版本：6000.6.0f1 (f7f8ed4d1e24)
Xcode：/Applications/Xcode.app/Contents/Developer
Android SDK：/Applications/Unity/Hub/Editor/6000.6.0f1/PlaybackEngines/AndroidPlayer/SDK
Node：/Users/bojuejun/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node
Playwright：同runtime的 dependencies/node/node_modules/playwright
```

`gh` 当前未安装；现有Git认证和Python GitHub API流程可用，不为发版顺手装一套工具。

本轮已完成的测试证据，不需要仅为交接重新运行：

- `Builds/qa-logs/full-release-0930/final-play.xml`：12/12。
- `avatar-edit.xml`、`profile-play.xml`：各1/1。
- 本地Web113项检查通过；真实公网地图/头像流程0错误。
- iOS、WebGL、Android、Windows、Mac最终构建日志含`CHO_SIREN_BUILD_OK`；iOS Xcode最终`BUILD SUCCEEDED`，严格codesign通过。
- 这些数字是历史结果，不得复制为下一次修改的测试结果。

按修改范围挑测试：

- 位置充能：`Assets/Tests/EditMode/FormationCharge1102Tests.cs`。
- 对白覆盖：`Assets/Tests/EditMode/DialogueVoice1102Tests.cs`。
- 玩家头像解析：`Assets/Tests/EditMode/PlayerProfile1028Tests.cs`。
- 拨碟：`Assets/Tests/PlayMode/VinylStageWheel1102Tests.cs`。
- 拖动编队：`Assets/Tests/PlayMode/FormationDrag1102Tests.cs`。
- 头像滚动：`Assets/Tests/PlayMode/AvatarPickerScroll1102Tests.cs`。
- 录制音效、选秀滑动：`RecordedUiSfx1102Tests.cs`、`AuditionSwipe1102Tests.cs`。
- 综合本轮小改：`ReviewPolish1102Tests.cs`、`WebReview1102Tests.cs`、`InventoryReview1028Tests.cs`。

示例，仅在确实修改该模块且没有其他Editor占用项目时运行：

```sh
cd /Users/bojuejun/cho-siren-unity
CHO_UNITY=/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity
CHO_QA=Builds/qa-logs/next-focused-check
mkdir -p "$CHO_QA"
"$CHO_UNITY" -batchmode -nographics -projectPath "$PWD" \
  -runTests -testPlatform EditMode \
  -testFilter ChoSiren.Tests.FormationCharge1102Tests \
  -testResults "$CHO_QA/edit.xml" -logFile "$CHO_QA/edit.log"
```

测试命令不加`-quit`，构建命令才用。PlayMode/视觉测试按现成脚本选择图形模式；不能一律`-nographics`后声称视觉验收。

UI任务要实际验证点击/触屏或鼠标拖动、窗口适配与可读性；测试和构建通过不能代替真实画面。小改通过必要验证即交付，不重复跑无关全量套件。

## 9. 后续发版必须复用的流程

当前30已经发布。只有用户新任务需要发版时才开展下述流程，别重新上传旧30。

### 9.1 通用顺序

1. 定好新版本，检查没有其他AI同时改版本或构建；同步ProjectSettings、Android code、游戏公告及实际发布notes。
2. 只提交本次源码/素材和`.meta`，保留无关dirty；从确定的源码状态构建。
3. 串行执行同工程Unity构建；原生打包、上传和独立Web检查可在依赖满足后并行。
4. 包和增量文件先传并验真，最后才提升latest和`app-version.json`，防止客户端拿到未完成包。
5. Web保留所有旧hash，发布main，等Pages成功，实际打开新构建验收。
6. 更新 `/Applications/幻域魅声.app`，保持原Dock入口；安装用户指定iPhone并查询版本，不自动打开。
7. 分渠道报告：源码、测试、构建、Web公网、原生下载、本机桌面、每台手机分别是什么状态；一个阻点不等于其他渠道没发布。

### 9.2 原生和桌面

现成最终脚本目录：`/Users/bojuejun/cho-siren-unity/Builds/qa-logs/full-release-0930/`。

| 脚本 | 用途与限制 |
|---|---|
| `verify-build-final.sh` | 头像补充测试、iOS/Web导出、调用原生构建；硬编码30，下一版复制并修改，别盲跑 |
| `build-native-final.py` | Android→Windows→Mac；SDK离线包装有finally还原 |
| `release-native-no-cleanup.sh` | `package`打安装器和更新包；拒绝覆盖已有成品，不清历史 |
| `upload-native-staggered.py` | GitHub Android优先，COS最多两任务；`--wait-package`等待真正成功的版本回执 |
| `finish-native.py` | 五公网包及两增量清单验真后才提升latest/app-version，有更高线上版本保护 |
| `verify-latest.py` | 验三条桌面latest别名 |
| `install-desktop.py` | 使用处理并签名后的mac-stage/dmg中app，备份旧版、原位替换、保留Dock；不启动 |

**不要直接执行 `Tools/release-installers.sh` 全脚本**：末尾含`rm -rf`、`cos_cleanup.py 3`和旧版本清理。用户要求保留历史，应沿无cleanup流程。

Android SDK在线`--list`曾卡17分钟；现成Python流程临时将单参数`--list`转`--list_installed`，结束恢复。不要为这个问题升级SDK、长期改SDK文件或改用户网络。

Android公网下载继续用GitHub Release资产；COS默认域名APK真实GET曾403 `DownloadForbidden`，HEAD可能200。COS APK只作归档，不能用HEAD200宣布下载可用。

### 9.3 WebGL

- 最新Unity原始包：`/Users/bojuejun/cho-siren-unity/Builds/WebGL-0.9.30`。
- 模板：`/Users/bojuejun/cho-siren-unity/Assets/WebGLTemplates/ChoSirenPortrait/`。
- 追加式staging：`/Users/bojuejun/Documents/Codex/2026-10-09/kan/work/web-release-0930/stage-web.py`，默认dry-run，`--apply`才写。
- 检查：同目录`check-web.cjs`；真实交互：`public-runtime.cjs`。
- 当前Build保留349个hash文件，manifest87组retained；不要清理旧345文件或历史R159。
- **不要用 `Tools/Stage-WebGL.mjs`**：该旧脚本只保留当前/上一组，会unlink其他Build文件。
- 下一版复制staging/check/runtime脚本到新QA目录；版本、releaseId、recordfile、previousReleaseRecord、上一原始包基线都要改，不是只改一个`--version`。
- 保留网页独立的DPR、公告字号等修复，不能直接拿Unity模板覆盖现有index；现成流程使用三方合并。
- Web本地分支名旧且无upstream，确认暂存diff后用明确的 `git push origin HEAD:main`，不要误推到同名旧分支。
- `Build`约6.7GB、`.git`约18GB；上次commit到公网验收约33分钟。耗时主要可能在大仓库Pages，不要盲目重复推送制造多个大任务。

### 9.4 凭据位置，只运行时读取

- COS：`/Users/bojuejun/.cho-siren-cos.json`，字段`bucket`、`region`、`secretId`、`secretKey`。
- Android签名：`/Users/bojuejun/.cho-siren/android-signing.json`。
- GitHub推送：现有Git `osxkeychain` credential helper和系统钥匙串；不要把token复制到文档、聊天、命令URL或日志。
- 音频生成：读 `Tools/audio1031/rh1031.py` 中的本机私密来源读取逻辑；这里只交路径，不交key值。
- Sites依赖当前Codex已连接账号；其他本机AI不一定拥有相同connector。拿不到Sites权限时可直接从本地样机目录继续，不需要重建游戏。

上述秘密文件、私密备份、签名材料都不进Git；不要打印它们的完整内容。

## 10. iPhone安装与已验证的网络修复

### 设备与产物

| 手机 | 标识 | 最后已确认事实 |
|---|---|---|
| 伯爵君iPhone | UDID `00008140-000E706E2493C01C`；CoreDevice `9550BD54-2A61-5AC8-92DE-5E7F7F889AC7` | Nov3已安装并设备端确认32，未启动 |
| 五条悟专用机 | UDID `00008140-001154D23E93001C` | Nov3已安装并设备端确认32，未启动 |

当前32签名app及对应Xcode工程：

```text
/Users/bojuejun/cho-siren-unity/Builds/iOS-DerivedData-device-0.9.32/Build/Products/Release-iphoneos/CHO-SIREN.app
/Users/bojuejun/cho-siren-unity/Builds/iOS-device-0.9.32/Unity-iPhone.xcodeproj
```

以下是**实际为30**的历史产物，仅用于旧记录比对，不要给用户装回30：

```text
/Users/bojuejun/cho-siren-unity/Builds/iOS-DerivedData-device-0.9.24/Build/Products/Release-iphoneos/CHO-SIREN.app
```

Xcode工程：`/Users/bojuejun/cho-siren-unity/Builds/iOS-device-0.9.24/Unity-iPhone.xcodeproj`。

Bundle ID：`com.bojuejun.chosiren`；签名team：`63K7K9XRZ2`。

**不要误装不带版本的旧 `Builds/iOS-DerivedData-device/...`，那个app此前检查仍为0.9.10。** 用Info.plist和codesign看实际版本，不靠目录名猜。

当前embedded.mobileprovision到期为 **2026-11-06 11:34:38 UTC**，含两设备。本机开发签名不是永久签名，过期后先处理续签，别把签名过期和Wi-Fi连接故障混为一谈。

历史Xcode成功命令在QA的 `xcodebuild-ios-final.log` 开头；有旧账户凭据warning但最终BUILD SUCCEEDED。不要只看到warning就从零重做。

### Nov2确实修成功的原因和方法

当时同网IPv4/IPv6和Bonjour配对端口能到，但CoreDevice仍4016。**全局`scutil --proxy`显示关闭并不完整**：

1. `State:/Network/Global/Proxies` 的 `__SCOPED__.en0` 仍开启HTTP/HTTPS/SOCKS，代理127.0.0.1:3067。
2. Wi-Fi的ExceptionsList只有一个元素，里面是15条用逗号连接的字符串；`*.local`等没有成为独立例外，Apple配对流量仍经代理并触发ARP不可用。
3. 备份后，仅把当前Wi-Fi现有条目拆成15个独立参数；未重启VPN、未切节点、未改路由、无需管理员验证。
4. 新日志出现authenticated，旧3067/ARP拒绝消失；设备available (paired)，随后安装30成功并查到版本。

只读入口：

```sh
networksetup -getproxybypassdomains Wi-Fi
scutil --proxy
scutil
# 进入scutil后，查看：
# show State:/Network/Global/Proxies
# quit
xcrun devicectl --timeout 10 list devices
```

正常例外是一行一条，不是一整行CSV。`scutil`要看`__SCOPED__`，不能只看顶层开关。**Nov3再次4016的原因尚未重新定位，不能直接断言还是同一原因。** 先读当前状态；若格式本来正确，不重复改规则或无休止重试安装。

历史修复与成功证据均在QA0930：

```text
bojuejun-proxy-store-readonly.json
wifi-bypass-before-repair.json
wifi-bypass-repair-result.json
bojuejun-after-bypass-logs.json
install-bojuejun-after-authenticated.json
verified-bojuejun-after-network-repair.json
```

私密Karing备份也在该目录的 `karing-private-backup-20261102-100003/`，禁止公开内容。不需要为了手机安装把已有LAN/direct规则重复加一遍。

以后用户指定安装时，先核实际连接和包版本，再用现有签名app覆盖安装，不附加launch命令：

```sh
xcrun devicectl --timeout 60 device install app \
  --device 9550BD54-2A61-5AC8-92DE-5E7F7F889AC7 \
  /Users/bojuejun/cho-siren-unity/Builds/iOS-DerivedData-device-0.9.32/Build/Products/Release-iphoneos/CHO-SIREN.app

xcrun devicectl --timeout 20 device info apps \
  --device 9550BD54-2A61-5AC8-92DE-5E7F7F889AC7 \
  --filter "bundleIdentifier == 'com.bojuejun.chosiren'" \
  --json-output /tmp/cho-installed-apps.json
```

成功标准是安装返回成功且设备端查询到目标版本；不要求自动打开。若用户只要求某一台，不因另一台连接失败拖延已可交付的那台。

## 11. 待办、质量边界和建议接手顺序

### 仍未闭环的事项

1. **两台手机最新32已经安装闭环。** 伯爵君和五条悟均有安装与设备版本查询成功记录；以后安装新版本再检查连接，不能继续沿用早前4016失败结论。
2. **用户对32版实际试玩的反馈。** 重点看角色卡默认大小/攻击出框、贴边动画全阶段、1倍速节奏、锁关拨动、换队、头像一致性、拖动跟手、扫荡和章节任务保留。
3. **关卡光碟美术仍可提高。** 用户只是暂用G，没说最终满意。保留当前运行版，改进候选先给完整交互预览，不能又压扁、抢背景或只给一张无法感受交互的图。
4. **语音情绪与UI/拨碟音效听感。** 已补录覆盖、替换录制素材，但用户是否认可力量和情绪仍需真实试听；不能仅凭98文件和ASR就称成熟音效已获认可。
5. **文字/图片清晰度的真实设备体验。** 之前修过字体、DPR、纹理导入；低清源图和图内烘焙字仍受源分辨率限制。若再报告模糊，先定位具体原图尺寸/导入平台覆盖/Canvas采样/DPR/裁剪比例，别全局乱放大布局。
6. **正式域名或CI健康若被要求再做。** 当前GitHub入口可用；自定义域名TLS、Quality checkout取消是独立问题，不能为了小UI任务顺便重做网站托管。

这些是交接给下一位AI的候选工作，不代表本次已启动新的重构、付费生成或自动发布。练习室团体升级继续暂缓。

### 推荐的下一步

- 收到用户新反馈时，直接在上面的文件映射中找对应partial，先复现一个代表问题，修复并在目标运行时验证。
- 如任务是继续手机安装，使用已有32签名包和已知网络排查入口，不重建游戏、不先改VPN；本次两台32已成功，无需重复安装。
- 如任务是美术优化，先按现有风格做一个代表样本和真实交互预览，用户认可后扩整套；不要批量生成不合风格资产。
- 如任务是发版，使用新版本独立QA目录复用无清理脚本，分别验真Web、公开包、桌面和指定手机。

## 12. 本机交接与跨电脑迁移的区别

**本机AI直接读完整工程路径即可。** 不要向用户回复“只有网页，没有Unity源码”；本机这里有源码。

如果将来换另一台电脑，仅发送本文或网页仓库main不够。至少交付完整Unity工程中的 `Assets/`（连`.meta`）、`Packages/`、`ProjectSettings/`、`Docs/`、`Tools/`和所需本机辅助脚本；可排除 `Library/`、`Temp/`、`Logs/`、重建型缓存。`Builds/`及QA脚本通常被Git忽略，若接手需要复用发布/安装证据应另外传选定目录；私密凭据另行安全配置，不混入公开源码包。

不要沿用旧文档的任意clone分支就保证取得最新工程。迁移前核实目标分支实际commit、关键StreamingAssets/字体/视频/`.meta`是否齐全，或从本机完整工作区制作源码包；用户真实存档另备份，不混入QA测试存档。

## 13. 可直接复制给下一位本机AI的开工提示

```text
请接手本机 CHO-SIREN 游戏。先完整阅读：
/Users/bojuejun/cho-siren-unity/Docs/AI-HANDOFF-2026-11-03-CONTINUE.md

完整Unity工程：/Users/bojuejun/cho-siren-unity
网页发布仓库：/Users/bojuejun/cho-siren-web
当前游戏0.9.32，Unity6000.6.0f1；不要按旧0.3.x交接倒退版本。

先看git status和目标文件，保留所有无关脏改动及存档。
默认走最短交付路径，小改只查相关文件，必要验证通过即交付。
不要顺手重构、全库审计、重复构建、升级工具或清历史发布包。
同Unity工程只运行一个Editor测试/构建进程；独立工作可并行。
日常中文回复尽量不超过30字；不主动使用Devin。

按我随后给你的具体任务继续。若涉及发布，复用文档中的无cleanup流程，
分别核对Web、Windows/macOS/Android、原生更新清单、本机Dock和指定iPhone。
手机保留存档，安装后查询版本，不自动打开。
不要把测试、构建、公开下载、真机安装和玩家验收混为一谈。
```
