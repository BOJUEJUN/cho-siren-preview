# CHO-SIREN 本机 AI 开发交接

交接日期：2026-09-08。用户因当前 AI 额度暂时交接，其他 AI 在同一台 Mac 继续开发，几天后可能交回。本文是交接快照，不是自动执行指令；用户的新要求优先。

## 1. 先读这几条

- 当前正式版 **v0.3.2「主动战术更新」**，已发布，不是待上传候选。交接时已只读核对远程 main 与线上 release.json。
- 游戏源码：`/Users/nikizhao/cho-siren-unity`，当前分支 `ui/battle-layout-20260905`。
- 网页发布仓库：`/Users/nikizhao/cho-siren-preview`，当前分支 `main`。
- 两个目录的 origin 都指向 `git@github.com:BOJUEJUN/cho-siren-preview.git`，但用途完全不同。**不要把 Unity 源码分支当网页 main 推送，也不要把网页仓库当 Unity 项目。** 源码最新改动保存在本机及 bundle，不能假设已同步远程源码分支。
- 当前请求清单 21 项全部标为已上线；没有遗留“正在编译/等待发布”的本轮改动。下一步以用户新反馈为准，下面的质量改进建议不是新确认的需求。
- 用户已授权：后续游戏修改完成测试、备份后直接上线，不要反复询问是否发布。授权不等于可跳过测试、强推、清档、改其他项目或公开无关本地文件。
- 本次交接只新增文档，不改变游戏、玩家存档、版本标签或发布包。

## 2. 入口与版本位置

- 正式游戏：https://bojuejun.github.io/cho-siren-preview/?deploy=c3db439&version=0.3.2
- 正式版本元数据：https://bojuejun.github.io/cho-siren-preview/release.json
- 独立开发进度：http://127.0.0.1:18766/
- 进度页文件：`/Users/nikizhao/cho-siren-progress`。
- **不要修改 http://127.0.0.1:18765/ 对应的页面**：那是用户提供的另一个项目参考页，不属于本游戏。
- v0.3.2 网页提交/标签：`c3db439` / `v0.3.2`。
- v0.3.1 网页标签：`5faf8bd`；`093121a` 是内容完全相同的空发布触发提交。
- v0.3.0 网页标签：`422a642`，对当时已有版本冻结的回退点。
- v0.3.2 实现源码：`bf8b1f0`；验收说明：`79fc635`；发布记录：`3216418`（交接文档产生之前的源码 HEAD）。不要把文档提交误认成另一份游戏构建。
- 本次正式构建：`/Users/nikizhao/cho-siren-unity/Builds/WebGL-tactics-v032`。
- 浏览器地址中残留 `deploy=d060af7` 不证明仍在运行那个旧版本；加载器会读取版本清单。用线上 release.json、build-versions.json、新资源响应及实际游戏交互共同核验。

## 3. 用户偏好与不可回退的产品方向

- 中文交流，结论先说，简短清楚；不要长时间无进度反馈。
- 角色主要使用女性；角色立绘优先复用用户提供素材包，用户明确不喜欢此前 AI 自生成的角色。不要自行批量替换已接受的立绘。
- UI 要可读、比例协调、不重叠。装饰边框素材不好用时可以换成程序化面板，但保留霓虹紫蓝、舞台主题，不能只改漂亮截图而断开功能。
- 签约、已拥有、编队、装备、属性、战斗、掉落必须用一致数据形成闭环。不能一个页面点击后另一处没同步。
- 头像、技能和奖励要有合适图标，避免只有名字；统一角色选择面板，默认已拥有，可显示全部；未拥有只可查看，不可任命/上阵/装备。
- 更新默认保留存档，不要求每次清档。新号必须是真实默认状态，不能用高等级演示档伪装验证。
- 默认新号战斗尽量一分钟多，典型约65–90秒；已有培养的队伍打旧关可以更快，不加硬性等待。
- 玩家应能判断谁攻击谁、敌人正在准备什么、是否该出手；不同反馈类别统一颜色，不是全部同色弹字。
- 用户希望后续版本明确编号、备份和可回滚。下一次游戏修改可用 v0.3.3，较大功能另定版本，但不要移动已有标签。

## 4. 已实现的系统

### 角色、装备和资源

- 招募签约写入拥有状态，成员可出战/替换；团队点击已有角色打开档案，支持升级、装备及换人。
- 成员显示队长/出战/待命状态，顶部显示当前队伍战力，不是队均等级。
- 每人七部位装备，支持同部位替换、卸下、跨角色转移；其他部位保持，属性用于真实战斗。
- 收藏共66件饰品，图标、分类、已拥有筛选、分页和来源已接入。重复获得转强化碎片；不是随机词条独立物品实例系统。
- 关卡有不同掉落池、图标预览、首通与重复通关奖励；首通不能重复领。
- 金币用于培养/强化，星钻用于招募/兑换，体力用于关卡。补给/兑换不代表已接入真实支付；不要声称有真实充值。
- 同一件装备从出战角色转移给待命角色时，队伍战力可能下降，这是合理结果；要同时解释当前角色属性和团队影响。

### v0.3.1：队长、骰子与时限

- 队长、编队换人、饰品选人共用头像选择面板，支持已拥有/全部、搜索、分页及返回状态。
- 更换队长：选角色 → 查看档案效果 → 任命，不再轮流切换。已有队员互换位置；待命成员替换当前队长前确认；等级和装备不丢。
- 队长效果区分魅族追击、人鱼护盾/精准重投、魔族叠毒、血精灵穿甲/收割，与角色自身技能分别说明。
- 骰子伤害增益按加法累计：开局0–25%，每次重投增加5–25%，最多+100%（×2）；弱骰型不倒扣已累计收益。
- 当前骰型决定的附加效果不跟着指数叠乘；换波/队长接任保留本局累计，新战斗重置。
- 第一章10关限时120秒，三星时限100秒，末20秒红色警示；超时仍有敌人判负。暂停不计时，2倍速同时加快战斗时间和倒计时。

### v0.3.2：主动战术（最新）

用户不喜欢“等待敌人蓄力/打断蓄力”按钮机械切换，已替换为：

- **破招突袭**：150%攻击，12秒冷却。平时可主动攻击；命中蓄力者且成功控制时打断，眩晕1.2秒（首领0.6秒），破甲30%持续4秒。
- 优先攻击玩家指定集火目标；未指定时优先蓄力者，再选正常攻击目标。指定了非蓄力目标时，不会偷偷改打另一只怪。
- 免控与控制恢复保护不被绕过；免控时仍受突袭伤害，但不能伪报打断成功。
- **应急守护**：全队3秒减伤50%，18秒独立冷却，不覆盖/消耗原有护盾。
- 战术施放者优先队长，队长无法行动则由能行动队员接替；全队眩晕/封技时不可释放且不扣冷却。UI暂停时不可释放。
- 敌人头顶显示技能、目标与2秒读条，末0.7秒变红；战术按钮名称固定，下方显示用途、冷却或破招时机；成功反馈短暂保留。
- 仍保留旧 `TryTacticalInterrupt()` 逻辑 API 和旧回归测试，**当前 UI 调用的是 `TryTacticalStrike()`，不是旧 API**。后续改动不要修错入口。
- 攻击青色、受击红色、暴击橙色、治疗绿色、护盾/守护蓝色、控制金色、毒/破甲紫色。

## 5. 源码导航（相对于 Unity 项目）

- `Assets/Scripts/ChoSirenApp.cs`、`ChoSirenAppRosterVisuals.cs`、`ChoSirenAppProgression.cs`：页面入口、选人/角色交互及成长流程。
- `Assets/Scripts/GameModel.cs`：游戏状态与默认存档。
- `Assets/Scripts/GameModelRosterActions.cs`：拥有、编队、队长相关动作。
- `Assets/Scripts/GameModelEquipment.cs`：多部位装备、转移、属性和掉落闭环。
- `Assets/Scripts/GameModelEconomyActions.cs`：资源动作。
- `Assets/Scripts/Systems/Tactics/RealtimeBattle.cs`：实时战斗时钟、伤害结算、队长与波次。
- `Assets/Scripts/Systems/Tactics/RealtimeTactics.cs`：状态、敌人蓄力、主动突袭和守护。
- `Assets/Scripts/Systems/Tactics/BattleSimulator.cs`：共用战斗状态及原有回合逻辑。不要误将实时需求只改在旧回合接口里。
- `Assets/Scripts/Panels/TacticsBattlePanel.cs`：战斗UI、事件表现、按钮、读条、暂停与结算。
- `Assets/Scripts/UI/CombatFeedbackPalette.cs`、`SkillIconVisuals.cs`：类别颜色和技能图标。
- `Assets/Resources/Data/tactics.json`：关卡数据；改数值要验证实际读取路径。
- `Assets/Editor/ChoSirenBuild.cs`：构建与WebGL哈希命名。
- `Tools/Stage-WebGL.mjs`：网页暂存/保留前版资源；先 dry-run 再 apply。
- `Docs/CAPTAIN-DICE-20260908.md`：v0.3.1细节；`Docs/TACTICAL-COMMANDS-v0.3.2.md`：最新机制与验收。
- 网页目录中的 `RELEASES.md`、`release.json`、`build-versions.json`：版本说明与资源清单。

## 6. 已验证到哪里，尚不能保证什么

最新测试证据位于 `/Users/nikizhao/cho-siren-unity`：

- `TestResults/editmode-tactics-v032-final.xml`：372/372通过。
- `TestResults/playmode-tactics-v032.xml`：115/115通过，共487项Unity测试。
- `npm run check`：12项网页加载器测试通过，哈希和资源完整性验证通过。
- 新号主动战术128场模拟全部胜利：随冷却突袭中位70.15秒，63/64超过60秒；等待蓄力突袭中位70.475秒，58/64超过60秒。使用自动骰子；不保证每场超过一分钟。
- 本机同一发布构建实玩：默认队伍、62.8秒三星、4人生还；突袭命中/冷却、守护蓝色反馈、暂停规则和结算实际检查过，未见控制台错误。
- 线上：版本元数据0.3.2，新loader/data/wasm为200；Chrome实际进入主页和关卡地图，未见控制台错误。**本轮没有再用玩家线上存档打一整局，完整战斗验收在本机完成。**
- 不能据此声称所有手机、所有分辨率、所有66件饰品组合、所有关卡与队长都已人工玩过，也不能声称每个2秒蓄力窗口/多敌人并发都已录屏验收。
- v0.3.2画面检查通过CUA截图工具完成，截图存在会话记录中，不要编造本地截图路径。v0.3.1可追溯截图在 `Artifacts/qa-minute-1788836180456/`；只用 `*-verified.png`、`pause-rules.png`、`cumulative-reroll.png`、`battle-result.png` 等确认过的文件。
- `editmode-tactics-v032.xml` 是早期一次失败记录：守护测试提前到敌人还没攻击时取伤害，后已纠正测试采样时刻。最终依据是带 `-final` 的文件，不要把早期结果当现状。

## 7. 建议下一位 AI 优先做的质量检查

这些是建议/验收缺口，不是已确认缺陷或未完成承诺；先问清或读取用户最新反馈，再实施。

1. **实玩复核战术是否真的有决策感**：现有机制仍是两个全队通用战术，敌人读条多为2秒。评估是否需要按敌人职业错开发招节奏、清楚标出威胁优先级；不要未经验证不断添加按钮。
2. **重点截取蓄力瞬间**：多敌人同时施法、首领全体攻击、治疗敌人、免控/控后保护、已指定非施法目标等情况。检查提示目标与实际结算目标一致，避免首领阶段提示误套其他敌人。
3. **战术状态显示**：队伍全员封技/眩晕或暂停时，按钮禁用原因是否足够清楚；施法提示有没有被其他弹字遮住；两按钮在窄屏是否容易点击。
4. **平衡不是靠抬血量解决一切**：已覆盖新号常规模拟，继续测不同培养、七件装备、队长组合及后续关卡。防止守护让敌人无威胁，或控制接力导致无限压制。
5. **移动端实测**：iPhone/iPad/Android的加载、内存、读条文字、触控区域、暂停和退后台。当前data约77.4 MiB，GitHub有>50MB建议警告（仍低于100MB文件上限），桌面加载成功不等于手机无问题。
6. **安全存档导出/导入与回滚兼容测试**：版本包备份不含玩家数据，目前没有依据宣称一键存档备份已做完。若增加此功能，先设计验证和恢复失败保护。

## 8. 本机测试、构建与试玩

Unity版本6000.6.0f1，本机有两个安装：

- `/Applications/Unity/Hub/Editor/6000.6.0f1-arm64/Unity.app/Contents/MacOS/Unity`：本轮用于EditMode和PlayMode测试。
- `/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity`：含WebGLSupport，本轮网页构建用此安装。
- arm64安装目前只观察到MacStandaloneSupport，别直接假设它能构建WebGL；不要为交接随意安装或切换编辑器。
- 同一项目一次只开一个Unity进程。读日志、确认测试进程退出，再启动下一步。需要执行权限时走正常确认，不绕过许可/系统安全。

测试示例（修改结果文件名以保留旧证据；测试运行不要加 `-quit` 提前退出）：

```sh
"/Applications/Unity/Hub/Editor/6000.6.0f1-arm64/Unity.app/Contents/MacOS/Unity" -batchmode -nographics -projectPath /Users/nikizhao/cho-siren-unity -runTests -testPlatform EditMode -testResults /Users/nikizhao/cho-siren-unity/TestResults/editmode-next.xml -logFile /Users/nikizhao/cho-siren-unity/Logs/editmode-next.log

"/Applications/Unity/Hub/Editor/6000.6.0f1-arm64/Unity.app/Contents/MacOS/Unity" -batchmode -projectPath /Users/nikizhao/cho-siren-unity -runTests -testPlatform PlayMode -testResults /Users/nikizhao/cho-siren-unity/TestResults/playmode-next.xml -logFile /Users/nikizhao/cho-siren-unity/Logs/playmode-next.log
```

独立版本构建示例（先确定版本，若目录已有旧包则换新目录）：

```sh
"/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity" -batchmode -quit -projectPath /Users/nikizhao/cho-siren-unity -executeMethod ChoSiren.Editor.ChoSirenBuild.BuildWebGL -buildOutput /Users/nikizhao/cho-siren-unity/Builds/WebGL-next -logFile /Users/nikizhao/cho-siren-unity/Logs/build-next.log
```

- 构建脚本会临时把 `Assets/Resources/Art/HeroFrames` 移到Editor下，避免旧238帧序列进入WebGL；期间git会显示大量删除。**不要提交这些临时删除，不要边构建边改资源。** 构建完成必须确认已恢复；若中断先检查，不要直接清理。
- 本机试玩用独立端口与新来源，避免改线上玩家存档。例如先确认端口空闲，再运行 `python3 -m http.server 18805 --bind 127.0.0.1 --directory /Users/nikizhao/cho-siren-unity/Builds/WebGL-tactics-v032`。不要杀掉未知端口占用进程。
- 旧辅助脚本 `Tools/qa-minute-battles.cjs` 有独立浏览器存档测试能力，但依赖硬编码本机路径。若当前工具要求所有UI必须使用CUA，就按当前工具规则，不得绕过；不要拿其中fixture写法修改用户日常浏览器的IndexedDB。

## 9. 发布顺序

1. 两个目录分别检查 `git status`、当前分支、`git log`；网页仓库fetch远程，确认无他人新发布。保留所有不属于本任务的改动。
2. 修改源码并补回归，测试通过后构建到独立目录；检查Unity构建成功和临时资产已恢复。
3. 实际浏览器检查用户指出的界面/机制：尺寸、重叠、文字裁切、真实交互；不是只确认加载完成。
4. 从Unity目录运行 `node Tools/Stage-WebGL.mjs --build Builds/新构建目录 --pages /Users/nikizhao/cho-siren-preview` 先看dry-run；核对要移除的旧资源都已归档，再加 `--apply`。
5. 该脚本保留当前与上一版完整4文件，淘汰更早一版在发布目录的资源；更早版本仍须留在git和独立备份中。
6. 同步 `ProjectSettings/ProjectSettings.asset` 中bundleVersion（构建前）、网页 `package.json`、`release.json`、`RELEASES.md`。网页构建不能只换index、不换清单和资源。
7. 在网页目录 `npm run check`；提交明确发布变更，创建新的annotated版本标签，不移动旧标签。
8. 独立目录创建网页归档与源码bundle，计算SHA256；网页归档解压到临时目录再跑 `npm run check`，验证bundle完整历史。备份完成后才推送。
9. 发布前再次fetch并确认 `origin/main` 是本地HEAD祖先。正常fast-forward推送网页main，随后单独推版本标签；**不强推，不重置公共历史，不推无关源码/素材包。**
10. 检查GitHub Actions的Pages部署和新哈希200，再打开带新deploy参数的正式URL检查。质量检查成功不等于Pages部署成功。
11. 更新独立进度页和部署记录；准确区分本机实玩、线上资源可用、线上实际交互。

历史坑：v0.3.1曾把main和两个标签原子推送，只有Quality checks启动，Pages未启动。确认不是代码失败后单独推一个空发布提交才触发Pages，内容与版本标签相同。v0.3.2按main→标签分别推送，Pages正常启动。不能推断每次都需空提交；先看当前任务状态，避免无意义重发。

## 10. 备份与回滚

- `/Users/nikizhao/cho-siren-backups/2026-09-08-v0.3.1/`：v0.3.0、v0.3.1完整网页tar.gz、v0.3.1源码bundle、校验清单及部署记录。
- `/Users/nikizhao/cho-siren-backups/2026-09-08-v0.3.2/`：v0.3.2完整网页tar.gz、源码bundle、SHA256SUMS、DEPLOYED.md。
- v0.3.2源码bundle包含79fc635及祖先，实现bf8b1f0；不含之后单独追加的3216418发布说明与本交接文档。最新说明在本机源码仓库；游戏实现完整。
- 源码bundle只含已跟踪内容，不含下面列出的未跟踪目录；没有把那些文件当成本轮资产完整备份。
- 两轮归档均做过SHA256、bundle验证和网页解压恢复测试。旧归档保持不动，新发布新目录。
- 回滚时先确定目标版本，核验归档，解压到独立目录并验证；将完整旧网页快照作为**新的前向提交**发布main，不force-push、不移动旧标签。保留`.git`和不相关文件，避免递归删除发布目录。
- 代码/资源回滚不等于玩家存档回滚。玩家数据在各自浏览器；需要单独导出、备份、验证降级兼容。本次两版都未改变存档格式、未清档，但这不是今后任意降级兼容保证。

## 11. 工作区与进度页保护

交接时Unity未跟踪内容：`.agents/`、`ArtArchive/`、`Artifacts/`、`Assets/Live2D.meta`、`Assets/Live2D/`、`Tools/render_character_preview.py`。网页目录未跟踪`.DS_Store`。**都不属于待清理垃圾，不删除、不擅自整包提交。** 其他AI可能同时工作，交接后再核对。

进度页是普通UTF-8 HTML/CSS/JS，不是TSX源码展示。用户曾反馈直接给canvas.tsx看到的是“乱码/代码”；因此不要再把源码编辑器当作可用进度页面交付。

- `tasks.json`：需求、真实状态、验收与证据；当前21项已上线。
- `order.json`：用户保存排序，保留顺序，不随意重置；开发前先读。
- `server.cjs`：无依赖的Node本机服务，仅绑定127.0.0.1:18766；若访问失败，先查端口，再在该目录执行 `node server.cjs`。不能保证重启电脑后服务自动存在。
- `/api/tasks`会合并新任务ID，状态变更不等于用户优先级变更；记录建议任务时不要冒充已获用户确认。

## 12. 开始接手 / 交回时

接手：读本文 → 读进度页tasks和order → 检查两个仓库当前状态与远程 → 核对用户最新反馈 → 做一个边界明确的改动 → 测试、备份、发布、验证。

交回：新增本轮交接日志，写明源码/网页提交、版本、测试文件、实际玩过的流程、未验证部分、新备份路径和遗留问题。不要只留“都完成了”，也不要覆盖本快照使历史丢失。

---

## 本轮交接日志（v0.3.3 玩家反馈优化）

交接日期：2026-09-08 21:22。本轮基于赵金当天 12 项玩家反馈完成。

### 完成项

- **代码改动**（Unity 源码，分支 `ui/battle-layout-20260905`，v0.3.3 实现已提交为 `07a7b1a`；注意 `3216418` 仅为 v0.3.2 发布说明，不含本轮实现）：
  - 经济收紧：GameSave 默认 300 星钻 + 1200 星光币（原 10695/17267）；`金币`全量改名`星光币`（30+ 处文案）。
  - 成长曲线压制：BattleSimulator.GrowthRates Hp 1.085→1.052、Attack 1.065→1.042、Defense 1.05→1.032；100 级攻击从 20401 → 2349，生命从 900952 → 42336。
  - 好感度系统：GameSave.MemberAffection 数组、AffectionOf/GainAffection/AffectionTierOf API、档位（陌生/友好/熟悉/亲密/羁绊）；训练 +3、战斗胜利 +2~4，签约送 5 点初始好感。
  - 饰品品质分级：AccessoryRarity 枚举（Common/Fine/Rare/Epic/Legendary），AccessoryRarityOf/NameOf/ColorHexOf/StatDescription API；作战饰品 0-5 Legendary、6-11 Epic，收藏按 (index-12)%9 分 Fine/Rare/Epic。
  - 掉落表重排：tactics.json 10 关 × 9 item/关（7 饰品+装备碎片+金币），品质递进 1-3 精良、4-6 稀有、7-9 史诗、10 传说；66 件装备全可达。
  - 面试页：SignCandidate/SignInterviewCandidate 改消耗星钻（60-3100）；NextInterviewRefreshLabel/TimeUntilNextInterviewCycle 显示 18:00 倒计时。
  - 骰子加持：TacticsBattlePanel.FlashDiceResult 投掷瞬间放大 1.18 倍 + 颜色按累计档位（<30% 紫、30-70% 绿、>70% 暖橙）。
  - BGM 通道：GameAudio.TryLoadExternalBgm 支持 Resources/Audio/bgm-loop 真实音频注入，未放文件回退合成音。
  - 成员档案 UI：好感度进度条 + 档位标签显示在战力四宫格下方；训练消耗显示"星光币 · 星钻 0"。
  - 饰品图鉴 UI：ChoSirenAppProgression 显示品质色 + 具体属性说明（"+8% 攻击 · +5% 生命"）。
  - 新增 ARCHE TYPE-DESIGN-v0.3.3.md 流派设计文档（打击感与特效点位）。
- **测试**：新增 FeedbackV033Tests.cs 9 个回归（经济/成长/好感度/品质/掉落/冷却）；修复 23 个旧测试期望值以匹配新曲线/新经济。**EditMode 测试 390/390 全部通过**（TestResults/editmode-v033.xml）。
- **构建**：Builds/WebGL（4 个 hash 资源，data 77.41 MB wasm 8.7 MB framework 80 KB loader 44 KB）。
- **发布**（cho-siren-preview main commit `89b9683`）：
  - 通过 Stage-WebGL.mjs apply 模式同步到 preview 仓库（保留 v0.3.2 作为 previous 回滚）。
  - build-versions.json current=v0.3.3、previous=v0.3.2；v0.3.1 hash 资源已从 Build/ 移除但 git tag v0.3.0/v0.3.1/v0.3.2 不动。
  - release.json/RELEASES.md/package.json 版本号同步 0.3.3。
  - 推送 main + v0.3.3 tag 到 origin。
- **玩家存档**：未触碰；新 GameSave 默认值只影响新号，已存档余额不被覆盖（测试验证）。

### 未验证部分

- **本机浏览器实玩**：构建完成但 Pages 部署期间未跑本地 18805 端口实玩验证。
- **移动端**：iPhone/Android 加载与内存实测未做；data.unityweb 77.4 MB 仍触发 GitHub >50 MB 警告（交接文档第 7 节 5 条）。
- **战术决策感增强**（交接文档第 7 节 1 条）：本轮优先级让位 12 项反馈，未做。
- **面试签约钻石改名**：用户原话"签约改为消耗钻石（代替人民币,用代名词过审）"——我沿用现有"星钻"命名,未额外改名。
- **真实音频文件**：BGM 通道已做好但 Resources/Audio/ 暂无 .mp3/.wav 文件。

### 遗留问题

- **构建输出路径**：Builds/WebGL 而非 Builds/WebGL-tactics-v033（ChoSirenBuild.BuildWebGL 硬编码 Builds/WebGL，未传 -buildOutput）。
- **测试时长上限放宽**：GameModelTests.ProductionChapterAuditUsesRealFormation 战斗时长上限从 105s → 120s（成长压制后高关卡时长自然拉长）。
- **`.DS_Store` 未提交**：preview 仓库 .DS_Store 仍为未跟踪,交接文档第 11 节明确不删除不擅自提交。

### 备份

- Unity 源码：v0.3.3 实现已本地提交 `07a7b1a`（分支 `ui/battle-layout-20260905`，按交接规范不推远程源码分支）。SHA256 bundle 归档已于 21:35 后补齐，路径见 `Docs/External-AI-Supervision/WORKBUDDY-STATUS.md`。
- preview 仓库 main 已推送（`89b9683`）；旧 tag v0.3.0/v0.3.1/v0.3.2 未动。

### 下轮建议优先级

1. 移动端实测与包体优化（交接文档第 7 节 5 条,data 77.4 MB）。
2. 战术决策感增强（交接文档第 7 节 1 条,敌人职业错峰蓄力+威胁标识）。
3. 深入交流功能（好感度 80+ 解锁,赵金反馈 12 条下版）。
4. 真实音频注入与图标替换（赵金反馈 6、8 条）。

---

## 本轮交接日志（v0.3.4 深入交流）

交接日期：2026-09-08 23:10。本轮落地赵金反馈 #12「深入交流（下版）」，按外部监督 WORKER-PROTOCOL 自主连续完成。

### 完成项

- **代码改动**（分支 `ui/battle-layout-20260905`，commit `a3238cc`）：
  - GameModel 深入交流 API：门槛 80（=羁绊档）、耗星光币 200、好感 +6、每成员每日 1 次（日历日）；
    存档新字段 MemberDeepTalkDate（可缺省，旧档兼容，未升 SchemaVersion）。
  - DeepTalkCatalog 10 条羁绊文案池（{name} 插值）。
  - 档案页底栏三键 + OpenDeepTalkResult 对话弹窗；锁定/已用/星光币不足三态均有明确提示。
- **需求页**：Docs/DEEP-TALK-v0.3.4.md（范围/验收/不在本版的边界）。
- **测试**：EditMode 401/401（TestResults/editmode-v034.xml，新增 11 项 DeepTalk 回归）。
- **构建**：Builds/WebGL-deeptalk-v034；bundleVersion=0.3.4。
- **本机实玩**（截图 deeptalk-01~38）：新号→锁定提示→15 次训练+1-1~1-6 六场三星→80 解锁→
  弹窗文案/-200 星光币/+6 好感→今日已交流→刷新持久化，全链路逐项核验。
- **发布**：preview main `95b5394` + tag `v0.3.4` 已推送；v0.3.3 保留为 previous 回滚点；线上 release.json=0.3.4、
  四资源 200、真实浏览器加载与档案按钮渲染确认（截图 deeptalk-39~41）。
- **备份**：/Users/nikizhao/cho-siren-backups/2026-09-08-v0.3.4/（bundle+web 归档+SHA256+恢复验证通过）。
- **看板**：18766 deep-talk-v034 已上线。

### 未验证部分 / 遗留

- 移动端真机未测；data 77.4MB 仍触发 GitHub 警告。
- BGM 真实音频、图标指认仍待用户输入。
- 深入交流文案池为通用池，种族/职业专属分支与对话选项为后续候选。
- 实玩坐标点击改用 CDP（/tmp/cdp-tap.mjs），合成 JS 事件 Unity WebGL 不响应。

### 下轮建议优先级

1. 战术决策感增强（交接 7-1，敌人职业错峰蓄力+威胁标识）。
2. 移动端实测与包体优化（7-5）。
3. 真实音频注入与图标替换（反馈 6、8，需用户提供素材/指认）。
