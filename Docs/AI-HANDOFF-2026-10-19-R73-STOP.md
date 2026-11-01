# CHO-SIREN 开发交接（R73c，用户要求停工）

更新时间：2026-10-19。用户要求“尽快弄完然后停下来，给可复制的 AI 交接文档，让其他 AI 继续开发”。本轮已结束生成、构建、测试与验收，旧目标将暂停，禁止自行恢复、重试或另开付费任务。

## 1. 先确认真实状态

- Unity 项目：/Users/bojuejun/cho-siren-unity
- Web 发布仓库：/Users/bojuejun/cho-siren-web
- 本轮全部证据：/Users/bojuejun/Documents/Codex/2026-10-09/kan/work/battle-feedback-20261019
- 正式线上仍为 **v0.3.81 / R72**：https://bojuejun.github.io/cho-siren-preview/
- 本地最终候选为 **v0.3.82 / R73c**，两个目标都构建成功；**尚未 commit / push / 上线**。不要只看版本号，r73b 与 r73c 都是0.3.82，必须看新二进制与回执 SHA。
- Web 仓库本轮未改，仍干净，HEAD 743cc5c3f3f0274b802f71dd5301cfdbac192cf5，分支 release/v0.3.81-r72。
- Unity 有大量其他 AI 的脏改动；不 reset/clean，不 git add -A，不批量提交。
- 先读 AGENTS.md、本文件、Docs/AI-HANDOFF-2026-10-19.md、Docs/WEBGL-RELEASE-SAFETY-2026-09-05.md。旧交接数字/状态可能过时，以新回执及人类最新指示为准。

## 2. 用户要求与已完成源码

用户希望极简、少字、合理动作和留白；不要满屏字/图标/抢眼装饰；保留用户原画和 PSD，不要擅自换首页两位角色。回复通俗简短，少爷不常在电脑，不要反复请求常规确认。永不主动使用 Devin。不要改 Karing 或系统安全设置。

- 战斗默认1倍 Pace .5（原 .7），表现与模拟时钟统一放慢。新队伍早期关卡模型审计约70–97游戏秒；之前旧高等级队伍打初章仍可能6–13秒，不能伪造敌人HP掩盖。root 曾在 r73b 实际网页打完1-3，用时81.0游戏秒；这不是最终c全场战斗验收。
- 重投是整场预算：普通2、精英3、Boss4、世界Boss5；跨波/切队长/重复Begin不能补满。保留血精灵队长原一次独立救援例外，不能无限循环。
- 战斗 PSD 82项，原L012绯音普攻冒出立绘已接；各原始bbox/比例/透明保留。HP/蓝条紧凑放立绘下，持续状态头顶横排、主动/大招提示按事件出现。移除技能名大黑板。
- 六骰开场有实际投掷过程，遮罩揭开后开始、六骰落地前不消耗战斗时钟。计分特效在骰子前层。
- 重投入口伸出/收回、原青色噪波动态、黄色充能光柱动态；蓝条内显示重投充能，队长加成文字不再被黑板挡住。
- 共用 ActionPlate 改用原矩形 ButtonSprite（退出战斗式），继续战斗等共用动作入口已统一，旧尖刺资源保留未删。
- 首页采用用户20:46明确选中的细蓝紫弧轨+低位真实圆盘；5个原有非均匀命中区/中文标签保留，点击短转60度/.68秒，非一直转。歌曲盘18度/秒，歌名不显示1/2，角色切换为简洁半透明箭头。
- 开始演出/挑战和返回大厅走新1.62秒圆盘/角色CG转场；不依赖旧KUMI参考录像。四人大招视频尚未提供，仍有原角色立绘回退。
- 修了遮罩期间重复挑战先扣体力后打不开战斗的问题；QA工具现在也等待真实遮罩退出，不能抢点。
- 地图/剧情/战斗不再漏出首页底盘；返回大厅恢复整组5导航。
- BGM/人声平滑让路与重复冷却、音效限流/优先级已有源码优化；静音原生验收不算听感通过。大厅只保留原两首《光的规则》《Chase the Glow》，不要接回Neon Moonwalk或中文唱歌候选；战斗暂保已允许的临时音乐，新03等仍候选。

## 3. 本轮重点：自然剧情CG

路径：/Users/bojuejun/Documents/Codex/2026-10-09/kan/work/story-natural-cg-20261019

- 已用内置 image_gen 整绘3幕：星璃单人排练 → 绯音推门双人入场 → 星璃独自后巷尾声。统一机位/光影、扶谱架/门框等真实互动，人物不再漂浮贴图。
- 3份941×1672原PNG逐字节保真进入 Assets/Resources/Art/Story/Scenes1019：
  chapter-01-rehearsal-solo、chapter-01-rehearsal-arrival、chapter-01-alley-outro。
- StoryPanel 对这3个 exact key 隐藏场内全身 Slot，保逻辑出场/表情/位置，左下当前说话者半身头像仍独立，右下对话；经纪人不借用偶像脸。
- 下载中/失败使用匹配空景+左下头像；过期CG/迟到空景不能盖新画面。完整CG上半保原色，只在下方阅读区加中性暗渐层。
- chapter-01 两分支/旗标/原文保留，绯音入场前明确切双人CG，尾声使用独立key，避免和stage-1-1演员组合混用。
- **其余20个关卡场景仍是无人环境+传统叠立绘，未完成自然人物整绘！** 不要把3幕示范写成全20场完成。下一步按实际对白/演员/动作逐场做，新增含人物CG必须同步明确注册，否则又会叠人偶。
- 构图/剧情表：work/story-streaming-20261019/INTEGRATED-SCENE-AUDIT.md。
- 提示词/原图SHA：prompts.json、manifest.json；实现证据 INTEGRATION.md、integration-receipt.json。
- Editor真实3图：runtime/chapter-01-{solo,arrival,outro}-720x1536.png。
- root已在最终c真实Chrome实际点过三幕、选择支、剧情→地图→大厅；浏览器console error为空，首页导航返回正常。截图见战斗证据目录 runtime/web/r73c-*.png。浏览器完整viewport为1728×817，实际竖屏画布约383×817；不要写成实测390×844。

## 4. 最终构建、测试与原生证据

以下路径均在 battle-feedback-20261019 下：

- 冻结 build-input-r73c.json：855个源码/设置/测试/验证器/包文件，构建和原生QA后仍全同。source SHA见两个 build-*-r73c-receipt.json。
- 原图 story-streaming-original-input-r73c.json：28张、28,786,121字节（20旧场景+3新CG+4透明头像+1转场CG），外置StreamingAssets按原字节/SHA读入，未重压/降像素。避免Unity展开纹理使Web data超限。
- candidate-web-r73c：Unity成功 + Node验证成功。data101,702,948B，小于100MiB但高于90MiB建议预算。build-web-r73c-receipt.json、web-deliverable-r73c.json。
- candidate-ios-r73c：Unity导出成功，ios-derived-r73c 的 arm64 Simulator Release **BUILD SUCCEEDED**。App为 ios-derived-r73c/Build/Products/Release-iphonesimulator/CHO-SIREN.app，bundle com.bojuejun.chosiren。
- 原生已安装新c并比对实际二进制SHA，title/lobby/story/tactics四页都运行成功，8PNG1206×2622逐张查看，12.74秒投骰录屏有两次转场/滚动中间态/六骰落地。
- 证据：FINAL-R73C-BUILD-NATIVE.md、native-runtime-r73c-receipt.json、runtime/native-r73c/。
- 原生输入为已有Unity按钮回调，非实体手机触控；原生返回流程未另做；保留状态静音，未主观听验。旧Prefs已原字节SHA恢复，QA已退出。
- 相关当前测试：新剧情Edit7+新Play6+旧剧情Play18；导航Play2；遮罩/投骰/编队Play26+相关Edit10，均通过无skip。
- 原全量Edit574中573过/1失败，后按exact fullname修复重验过；原Play109中106过/1失败/2显式环境skip，后失败重验过。这是聚合证据，**不是新全量全绿**。不要掩盖历史失败或两个录屏环境skip。
- web-stage-r73c 独立镜像已建，npm check **110/110**。38新增/7修改/0删除；保留全部历史Build。web-stage-r73c-receipt/ 是精确copy计划和哈希，真实Web仓库未写。
- 独立脚本 prepare-web-stage-r73c.py、PREPARE-WEB-STAGE-R73C.md；8项交付保护fixture通过。旧r73/r73b不覆盖。
- 下个AI若发布，先核当前源码/冻结/远端main仍对应基线，完成其需要的最终战斗暂停/继续/重投等运行回归，更新release验证状态与copy清单，再精确stage/commit。不要把当前Pending元数据原样发出，也不要先重建或沿用b的hash。
- 发布不用旧release.py/push.sh：前者会删历史Build，后者git add -A且推送非原子。应精确copy-only、检查暂存集合、保历史，核main后原子推main+backup/v0.3.82，再查公开元数据/入口四件/关键SHA。

## 5. 结算与语音待选择

- 结算用户多次拒稿；最后要求参考P5但简单、保角色原色、胜利动作。最新两案是“彩色走台/演出落幕”，root实际打开看过，两案均未选、未接产品。
- 可打开 /Users/bojuejun/.codex/visualizations/2026/10/09/01a11f6e-6a02-7fb2-b043-2a611929a904/result-p5-minimal-options.html；源图 work/result-victory-motion-20261019/。不要接回拒绝的黑白巨头像/斜切战报。
- 原16条生产日文WAV保持原SHA。早先4声线B是既有基线；最新power试听只有“绯音B还行”获初步认可，不是四人新版全通过。
- 最新12句（每人2主动+1大招）已生成/真解码，仍是 **试听，未接游戏、未宣称演技通过**。
  路径 /Users/bojuejun/Documents/Codex/2026-10-09/kan/work/voice-next12-20261019/
  打开 voice-next12-preview.html（4角色tab、自含12段MP3，可离线打开）。
  读 HANDOFF.md、audition-manifest.json、cost-receipt.json；原保护40文件SHA未变。
- 本批12任务各提交1次，全部SUCCESS。本批实扣¥0.0521；语音/SFX累计¥0.4113/¥1，预算余¥0.5887（不是账户余额）；收尾实时在途0。所有生成/下载/轮询已结束。不要再次提交这些task。
- 新句时长0.79–2.21秒，少数主动超过原目标1.2秒，未裁字/机械加速。模型为speech-2.8-hd，四人原voice_id/pitch身份保留；接口不支持的导演文字未伪装成真实控制字段。效果必须用户耳听，不拿ASR/响度当通过。
- 研究/12句导演稿见 work/voice-power-auditions-20261019/{VOICE-INDUSTRY-RESEARCH.md,VOICE-NEXT-BATCH-AUDIT.md}。合格同事件音频再做shuffle-bag防连播；不要只为了“多语音”放过期喊声。

## 6. 接手顺序与停止边界

1. 先核git status、最新人类指示与本文件，再看新c包/截图/三幕CG，不重复生成已有资产。
2. 审视剩余20剧情自然CG和最终结算选择，再继续制作；四位大招视频等赵金提供，不能声称已完成。
3. 用户听选新12句，保声线身份补强情绪/变化、接真实事件池；研究音效遮蔽并听真实战斗混音，不盲加音量。
4. 需要上线时按第4节安全发布最终候选；源有变化才重建双端。任何新增收费遵守余预算并先查报价/在途。
5. 本AI及所有子代理已按用户要求停止，不会继续开发/轮询/消费。没有新自动化。只读预览服务如仍存在不代表后台开发；文件可独立使用。
