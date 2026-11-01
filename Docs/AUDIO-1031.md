# 2026-10-31 · 声音系统优化（子任务 audio）

赵金原话：“死了要有一两句台词，大招交互的 good、perfect 等等音效跟反馈，还有整体的打击感、反馈。音效不要用代码简单合成……要好的。”
用户确认：音效先用 Suno（RunningHub 国际站 Suno v6 Sounds），台词日语、字幕中文。

- 试听页（每个事件全部候选 + 入选标记 + 旧版对照 + 全部语音 take）：`Builds/qa-logs/zj-1031/audio/audition.html`
- 收费账本（每次调用的模型 / taskId / 报价 / 实收 / 输出 SHA）：`Docs/AUDIO-1031-ledger.json`
- 语音清单（每条台词的日语、中文字幕、声线、情绪、速度、音高、入选 take、SHA、时长、响度）：`Docs/AUDIO-1031-voice-manifest.json`
- 音效候选指标与入选：`Builds/qa-logs/zj-1031/audio/sfx-candidates.json`；音频 QA：`Builds/qa-logs/zj-1031/audio/qa-audio.json`

## 1. 费用

| 钱包 | 模型 | 次数 | 合计 |
|---|---|---|---|
| RunningHub 国际（USD） | `rhart-audio/suno-v6/sounds`（每次 2 个候选） | 43（含 1 次试点） | **0.86 USD** |
| RunningHub 国内（CNY） | `rhart-audio/text-to-audio/speech-2.8-hd`（MiniMax） | 125 成功 + 2 失败 | **≤ 1.06 CNY**（失败 2 条按报价计入，实际大概率未扣） |

合计约 **7.34 元人民币等值**（按 7.3 换算，仅用于和 60 元上限比较；两个钱包分别记账）。每次调用前都做了 price-preview，单次上限（Suno 0.05 USD、MiniMax 0.03 CNY）和批次上限（60 元）在 `Tools/audio1031/rh1031.py` 里强制检查，失败不自动重试；脚本被超时打断那一次，只按已知 taskId 收取结果，没有重新提交。
密钥只在进程内读取（国际：用户已有的 Oct17 交接 README 第 3 节；国内：`rh_readonly_probe.py` 的 env 路径），没有写入任何文件或日志（已用进程内比对扫描 Docs / Tools / qa-logs，0 命中）。

## 2. 哪些用了 Suno，哪些用了别的

| 内容 | 生成 / 来源 | 许可 |
|---|---|---|
| 全部战斗音效的**主体层**（14 个事件 × 3 take + 4 个判定音） | **Suno v6 Sounds**（one-shot，WAV），RunningHub 国际站 | 付费账户生成；RunningHub 条款下的商用归属见 SFX-RESEARCH §1，仍待少爷确认 |
| 低频 / 瞬态 / 点缀层 | **Sonniss GDC 2026**（344 Audio：Cinematic Fight 拳击、Bass Drops & Downers、Haunting Ambiences 金属/木击、Historical Weapons 刀剑/盾/矛击、Christmas 魔法铃、Elemental Palette 水/风/金属呼啸、Ghostly Presences 邪咒） | 免版税、可商用、免署名；不得单独转卖、不得训练 AI（`~/cho-siren-audio-src/sonniss-gdc2026/part1/License - GDC Game Audio.pdf`） |
| 种族材质点缀 | **lentikula Basic Spell Impacts**（Fire / Ice / Water / Lightning） | CC0 |
| 尾音 | pedalboard 房间混响（处理，不是合成声源） | — |
| 阵亡台词、敌人台词、大招播报 | **MiniMax speech-2.8-hd**（RunningHub 国内） | 同 10-19 配音 |

**没有任何振荡器 / 噪声合成层**（旧 10-23 版里的 sub_drop / crack / sparkle 合成层这次全部弃用，低频改用 Sonniss 真实 bass drop 录音）。豆包 Seed-Audio 没有用到：国际 Suno 账户可用（调用前余额 4.59 USD、pending 0）。

## 3. A 我方阵亡台词

- 4 位已有配音成员各 2 句（日语、短、有情绪），**沿用各自 10-19 B 版声线与音高**：
  星璃 `Japanese_DependableWoman` pitch +2；绯音 `Japanese_DecisivePrincess` -2；雾白 `Japanese_KindLady` +1；夜莺 `Japanese_CalmLady` -2。
  每句生成 2–6 个 take（sad / fearful / angry 情绪对比），按 **0.8–2.5 秒 + 离线 Whisper-small 识别准确度** 挑选，其余 take 都在试听页。

| 资源 | 日语 | 字幕 |
|---|---|---|
| xingli-defeat-01 / 02 | ごめん…歌、届かなかった… / みんな…あとは、お願い… | 抱歉……歌声没能传到…… / 大家……剩下的，拜托了…… |
| feiyin-defeat-01 / 02 | くっ…まだ、踊れるのに… / ちっ…ここまで、か… | 可恶……明明还能跳的…… / 啧……到此为止了吗…… |
| wubai-defeat-01 / 02 | ごめんね…守れなくて… / 泣かないで…平気、だから… | 对不起……没能守护你们…… / 别哭……我没事的…… |
| yeying-defeat-01 / 02 | ビート…途切れた… / くそっ…次は、外さない… | 节拍……断了…… / 该死……下次不会失手…… |

- 后期：裁头尾静音、只剪“…”处的静音段（不变速、不变调）、BS.1770 响度 -19.5 LUFS（与现有 -20 LUFS 战斗配音一致），44.1 kHz 单声道。时长 1.83–2.44 s。
- 接入：`TacticsBattlePanel.VoiceDefeat1031.cs`。我方单位的 Defeated 事件 → `down` 倒地音效 + `BattleVoiceDirector.Cue.Defeat`（新增，优先级高于小技能、**不打断队友大招台词**；与敌人“击杀”台词同帧时最多等 3.2 s 排在其后播放；团灭时最后一句不被结算截断）→ 实际开口时 `Spoke` 事件 → `VoiceSubtitle1031` 在该成员卡片正上方出撕纸风中文字幕（墨色底 + 品牌青边 + 名字签，跟随卡片、暂停冻结、ReduceMotion 只淡入淡出）。
  原项目没有战斗语音字幕组件，所以新建。共享文件 `TacticsBattlePanel.cs` 只加了 1 行调用。
- 两位新偶像（精灵/人鱼）暂无配音，按要求跳过（`BattleVoiceDirector` 只对四位成员发声）。

## 4. B 敌人配音（`Assets/Resources/Audio/Voice/Enemy/barks.json`）

schema 与任务给定一致：`{ id: { enter|taunt|lowhp|kill|death: [ {text: 中文字幕, clip: "Voice/Enemy/<id>-<类>-0N"} ] } }`。
数据里唯一的 Boss 是 `siren-queen`（魅声女王，1-5/1-10/2-5/2-10）；`boss-default` = 女王整套，`minion-default` = 从各小怪里挑的通用句。小怪每类 1–2 句、Boss 每类 2 句，共 58 条。

| 敌人 | 形象 | MiniMax 声线 | 速度 / 音高 | 后期 |
|---|---|---|---|---|
| neon-scout 赤血刺客 | 冷酷刺客 | English_ConfidentWoman | 1.00 / -2 | 干声 |
| noise-wraith 蚀音游魂 | 顽皮幽灵少女 | English_PlayfulGirl | 1.05 / +2 | 薄合唱 + 空灵混响 |
| pulse-guard 浊浪溺者 | 溺水低语 | English_Whispering_girl | 0.92–1.0 / -3 | 低通 + 水波合唱 + 暗房间 |
| velvet-hexer 毒蚀蛛娘 | 妖艳 | English_captivating_female1 | 1.00 / +1 | 小房间 |
| echo-drone 幻魅歌姬 | 优雅歌姬 | Japanese_GracefulMaiden | 0.95 / -1 | 厅堂混响 |
| static-golem 狂响幻蚀歌姬 | 狂气 | English_UpsetGirl | 1.12 / 0 | 轻失真 + 短回声 |
| siren-queen 魅声女王 | 冷艳女王 | Japanese_ColdQueen | 0.95 / -1 | 厅堂混响 |

- 声线先做了 15 条“选角试点”（每个敌人 2–3 个候选声线读同一句），用离线 ASR 排除读不准日语的声线（如 English_Deep-VoicedGentlewoman、English_AnimeCharacterFemale 直接失败；English_WhimsicalGirl 读“声”不稳，游魂整套换成 PlayfulGirl 重做）。
- 全部 58 条过了离线 Whisper：读错的 9 条改写台词后重生成（如“音量”→「ボリューム」、“魅声の女王”→「この私に」），一条 51 秒循环坏音已丢弃重做。
- 对话泡 UI 与播放归 enemy-stage 子任务，本任务只保证 json 与 clip 正确（EditMode 测试逐条 `Resources.Load` 校验）。

## 5. C 大招判定反馈

现有判定（`UltimateBeatChallenge1025`，单拍）：PERFECT ±0.10 s（+10%）、GOOD ±0.24 s（+5%）、MISS。
新增 **GREAT = GOOD 窗口里更准的一半（|偏差| ≤ 0.16 s）**，**只影响表现，加成仍是 +5%**；自动战斗的辅助点击（0.17 s）保持显示 GOOD。

每次判定（`BattleUltimateShow1020.BeatJudged` 新事件 → `TacticsBattlePanel.UltimateGrade1031.cs`）：
1. **中央大字** `Assets/Scripts/UI/UltimateGradeBurst1031.cs`：斜切撕纸色带（GOOD/GREAT 品牌青 #1CF2EC，PERFECT 金 + 粉、白边墨边、半调网点）从左扫入，现有手绘撕纸字（`UI/GradeStamps`）带墨色投影弹入；GREAT 没有手绘字，用游戏现有的 P5 剪报字母（青/金/墨）。
   PERFECT 最炸：2.6× 过冲弹出、金色辉光脉冲、白闪、16 道光芒旋出、星星闪烁、金色「!!」追加盖章、大招演出层 0.3 s 衰减震动。MISS：灰色小字下坠 + 抖一下，无闪光无光芒。
   ReduceMotion：只保留淡入淡出（无闪光、震动、光芒、弹跳）。暂停冻结并停播报员。原来环旁的小字、旧 hit/crit 提示音和结尾贴纸在有新反馈时让位，避免三重重复。
2. **判定音效** `Sfx/grade-perfect|great|good|miss`（Suno 主体 + Sonniss 魔法铃 / bass drop 分层，档位越高越华丽：perfect 1.8 s 闪光上扬+镲片+铃，great 1.1 s，good 0.7 s 叮，miss 0.4 s 闷响）。
3. **英文播报** `Voice/Announcer/ult-good|great|perfect`：MiniMax `English_Upbeat_Woman`（三种女声对比后选：基频最高、起伏最大、三档递进）；MISS 不播报。走 GameAudio 的语音通道（受音效开关/音量控制、会压低音乐），所有者独立，不会被战斗语音导演误停。

## 6. D 打击音效重做

文件名全部不变，直接替换内容（代码零改动即可生效），每个事件 3 个 take（`name`、`name-2`、`name-3`，GameAudio 轮换不重复）：
`hit, crit, hurt, block, shield, heal, ko, strike-charm, strike-demon, strike-mermaid, strike-bloodelf, ultimate`。
shield/heal 以前只有 1 个 take，现在也是 3 个。新增：`boss-hit`（打到魅声女王时叠加的加重层，1 行调用）、`down`（我方倒地）、`grade-*`（见上）。

流程（`Tools/audio1031/`）：
1. `gen_sfx.py`：每个事件 2 次 Suno 请求（= 4 个候选文件），较弱的 6 个事件又用更精确的提示词补 1 次；提示词全文在 `gen_sfx.py` 的 `PROMPTS` / `PROMPTS_R3` 和账本 payload 里。
2. `segment.py`：Suno 经常在一个 one-shot 文件里出多次击打，按能量起音切成单次声响，裁到起音点（0 ms），按客观指标打分：起音时间、频谱质心是否在该事件频段、峰均比（打击要有冲击）、800–6000 Hz 能量占比（手机扬声器听得到）、是否被截断 / 门限式骤停、后段是否反常变响、时长、削波。
3. `build_sfx1031.py`：每事件取最好的 5–6 个主体，按五层法叠 Sonniss/lentikula 层（①低频 bass drop ②Suno 主体 ③拳击/矛击瞬态 ④魔法铃/种族法术点缀 ⑤房间尾音），包络塑形（保持 + 指数衰减，把 AI 生成的“方块”变成冲击 + 尾巴）、去门限、轻压缩，BS.1770 **最大短时响度**归一、真峰值限幅 -1 dBTP（限幅增益衰减封顶 3 dB，不把瞬态压扁），裁头部静音（打击类入选 take 起音 0–16 ms），44.1 kHz 单声道 PCM16；同一事件 3 个 take 统一到同一短时响度；再按最终指标排名入选。
   种族特色：魅族 = 粉色火焰 + 糖果玻璃闪光；恶魔 = 撕裂爪击 + 邪咒 + 金属重击；人鱼 = 水花 + 水系法术；血精灵 = 金属刀光 + 冰晶。
4. 入选结果（最大短时响度 LUFS-M，同事件 3 个 take 一致）：hit -18.6、hurt -17.5、block -16.0、crit -15.4、strike-* -14.8~-16.1、boss-hit -14.6（低频更厚、更长，叠在 hit/crit 下）、ko -12.1、ultimate -11.7、shield -15.1、heal -15.0、down -16.9、grade-perfect -11.8、great -12.6、good -16.2、miss -20.2。全部真峰值 ≤ -1.0 dBTP，0 削波，0 头部静音；打击类起音 0–16 ms（grade-good/miss 约 20 ms，heal-3 是 0.29 s 渐起的治疗光）。
5. 候选全部保留：`Builds/qa-logs/zj-1031/audio/candidates/sfx/<事件>/cand-XX.wav`；替换前的旧版（10-23 版）存于 `previous-sfx/`，试听页可直接 A/B。想换：把某个 cand 复制成 `Assets/Resources/Audio/Sfx/<事件>[-2|-3].wav` 即可。

`BattleSfxVoicePolicy1019` 补了新名字的优先级（grade 90、down 70、boss-hit 52），否则默认 15 可能在混战中被丢。

## 7. 导入设置与包体

- 音效：Vorbis 质量 0.70、Decompress On Load、Preload（零延迟）。语音：Vorbis 0.50、Compressed In Memory、Preload。保留所有旧 GUID，normalize/forceToMono 关闭（文件已是单声道且已做响度，Unity normalize 会破坏事件间响度层级）。脚本：`Tools/audio1031/write_metas.py`。
- 新增 / 替换音频 115 个文件，Vorbis 估算约 **2.5 MB**（其中 32 个是替换原有同名文件，83 个为新增）；新增部分远低于 8 MB 预算。

## 8. 重建命令

```bash
cd ~/cho-siren-unity
PY=~/cho-siren-audio-src/venv/bin/python
# (收费，已完成；脚本会跳过已成功的条目) python3 Tools/audio1031/gen_sfx.py --runs 3
# (收费，已完成) python3 Tools/audio1031/gen_voice.py Tools/audio1031/plans/<plan>.json Builds/qa-logs/zj-1031/audio/raw/<dir>
$PY Tools/audio1031/segment.py          # 切分 + 打分 Suno 候选
$PY Tools/audio1031/build_sfx1031.py    # 分层、母带、入选并安装到 Assets/Resources/Audio/Sfx
$PY Tools/audio1031/build_voices.py process   # 语音后期 -> candidates/voice
#   离线 ASR：/Users/bojuejun/Documents/Codex/2026-10-15/new-chat/work/jay-voice-pilot-20261016/local-venv/bin/python \
#            Tools/audio1031/asr_check.py japanese <out.json> <wav>::<台词> ...
$PY Tools/audio1031/build_voices.py select    # 入选、安装、写 barks.json 与语音清单
$PY Tools/audio1031/write_metas.py            # 导入设置
$PY Tools/audio1031/qa_audio.py               # 解码 / 时长 / 响度 / 真峰值 / 削波 / 头部静音检查
python3 Tools/audio1031/build_audition.py     # 试听页
```

## 9. 自动化验证（2026-10-31）

- 编译通过；EditMode 668 项 667 过，唯一失败 `BattleMeeting929Tests.HighHandsAccelerateCaptainOnlyAndFractionalGainsAreRetained` 是 dice 子任务正在改的“六同 ×1.8”能量数值（他们自己的 after1/after2 日志同样失败），与音频无关。新增 `Audio1031Tests` 9 项全过（barks.json 全部 clip 可加载、阵亡台词 0.8–2.5 s、GREAT 分档、判定音/播报存在、替换音效 3 take 44.1 kHz）。
- PlayMode（WebGL 目标，过滤 BattleVoiceDirector / BattleChorus1028 / ManualUltimatePanel1019 / ContextMusic / BattleEnergyUltimate* / BattleSfx* / UltimateTimingInput / GlobalMusic + 新测试）：47 项 35 过 12 失败，**12 项失败与 0.9.15 基线完全相同，无新增失败**。新增 `BattleVoiceDirectorTests.DefeatLine…`、`UltimateGradeBurst1031Tests`×3、`Audio1031CaptureTests` 全过。
- 真实战斗画面截图：`Builds/qa-logs/zj-1031/audio/captures/`（grade-perfect/great/good/miss 及弹出帧、defeat-subtitle）。
- 注意：ManualUltimatePanel1019Tests 里真正走完整大招流程的几项在基线就失败，所以“判定回调 → 大字/音效/播报”在完整面板里的联动只有截图和代码路径检查，没有端到端自动化断言。

## 10. 验收层级（不能互相冒充）

技术指标通过（可解码、44.1 kHz、单声道、真峰值 ≤ -1 dBTP、0 削波、起音 ≤ 16 ms、台词 ASR 可识别）≠ 少爷试听认可 ≠ 战斗中音画对齐 ≠ 手机扬声器听感。本批次只验证到第一层和 Unity 自动化测试；**所有声音都还没有经过人耳试听验收**，请用试听页挑换。
