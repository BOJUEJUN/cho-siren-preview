using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Reflection;
using ChoSiren.Panels;
using ChoSiren.Systems;
using ChoSiren.Systems.Dice;
using ChoSiren.Systems.Tactics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;

namespace ChoSiren.Tests
{
    public sealed class TacticsBattlePanelDicePlayModeTests
    {
        [Test]
        public void FinalTwentySecondsAreRedAndPauseKeepsBattleTime()
        {
            GameObject root = new GameObject("Countdown Test", typeof(RectTransform));
            try
            {
                BattleSimulator battle = CreateBattle();
                battle.Stage.TimeLimitSeconds = 120;
                battle.EnableRealtime(new Dictionary<string, string> { { "player", "魅族" } }, "player");
                TacticsBattlePanel panel = TacticsBattlePanel.Open(root.transform, new GameModel(), battle, null);
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var elapsed = typeof(TacticsBattlePanel).GetField("battleElapsed", flags);
                var timer = (Text)typeof(TacticsBattlePanel).GetField("timerText", flags).GetValue(panel);
                elapsed.SetValue(panel, 99f); Invoke(panel, "RefreshBattleHud");
                Color normal = timer.color;
                elapsed.SetValue(panel, 100f); Invoke(panel, "RefreshBattleHud");
                Assert.That(timer.text, Is.EqualTo("剩余 00:20"));
                Assert.That(timer.color, Is.Not.EqualTo(normal));
                var phase = (Text)typeof(TacticsBattlePanel).GetField("phaseText", flags).GetValue(panel);
                Assert.That(phase.text, Does.Contain("超时判负"));
                Invoke(panel, "TogglePause"); Invoke(panel, "Update");
                Assert.That(elapsed.GetValue(panel), Is.EqualTo(100f));
                Assert.That(battle.ElapsedMilliseconds, Is.Zero);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void HighLevelMapOnlyStartsTheFixedEncounterWithoutRehearsal()
        {
            bool hadSave = PlayerPrefs.HasKey(GameModel.SaveKey), hadLegacy = PlayerPrefs.HasKey(GameModel.LegacySaveKey);
            string before = PlayerPrefs.GetString(GameModel.SaveKey), legacy = PlayerPrefs.GetString(GameModel.LegacySaveKey);
            GameObject root = new GameObject("Fixed Encounter Map Test", typeof(RectTransform));
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(720, 1536);
            try
            {
                PlayerPrefs.SetString(GameModel.SaveKey, JsonUtility.ToJson(new GameSave { StoryProgress = 100 }));
                var model = new GameModel();
                LevelMapPanel map = LevelMapPanel.Open(root.transform, model);
                Assert.That(map.GetComponentsInChildren<Transform>(true)
                    .Any(item => item.name == "RehearsalToggle"), Is.False,
                    "已取消适配试演，不能留下可见或隐藏的切换入口。");
                Assert.That(map.GetComponentsInChildren<Text>(true)
                    .Any(item => item.text.Contains("适配") || item.text.Contains("级档")), Is.False);
                RectTransform start = FindRect(map.transform, "StartChallenge");
                Assert.That(start.GetComponentInChildren<Text>().text,
                    Is.EqualTo("开始挑战").Or.EqualTo("再次挑战"));
                start.GetComponent<Button>().onClick.Invoke();
                TacticsBattlePanel battlePanel = root.GetComponentInChildren<TacticsBattlePanel>();
                Assert.That(battlePanel, Is.Not.Null);
                Assert.That(battlePanel.Battle.EnemyHpMultiplierPermille,
                    Is.EqualTo(model.CurrentBattleDifficultyProfile.EnemyHpPermille));
                Assert.That(battlePanel.GetComponentsInChildren<Text>(true)
                    .Any(item => item.text.Contains("适配") || item.text.Contains("级档")), Is.False);
                Assert.That(battlePanel.GetComponentsInChildren<RectTransform>(true)
                    .Any(rect => rect.name.StartsWith("BossPhaseMarker-")), Is.False,
                    "首领自身的阶段阈值不能错误标到所有敌人的总血条上");
                Assert.That(model.LevelOf(0), Is.EqualTo(68));
                Assert.That(model.Save.Stamina, Is.EqualTo(GameModel.MaxStamina - battlePanel.Battle.Stage.StaminaCost));
            }
            finally
            {
                Object.DestroyImmediate(root);
                if (hadSave) PlayerPrefs.SetString(GameModel.SaveKey, before); else PlayerPrefs.DeleteKey(GameModel.SaveKey);
                if (hadLegacy) PlayerPrefs.SetString(GameModel.LegacySaveKey, legacy); else PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
            }
        }

        [Test]
        public void BattleControlsUseOneRowAndExitLivesInBlockingPauseMenu()
        {
            GameObject root = new GameObject("Battle Header Test", typeof(RectTransform));
            try
            {
                TacticsBattlePanel panel = TacticsBattlePanel.Open(root.transform, new GameModel(), CreateBattle(), null);
                RectTransform[] controls = { FindRect(panel.transform, "AutoToggle"),
                    FindRect(panel.transform, "SpeedToggle"), FindRect(panel.transform, "PauseToggle") };
                foreach (RectTransform control in controls)
                {
                    Assert.That(Top(control), Is.EqualTo(20));
                    Assert.That(control.rect.height, Is.EqualTo(52));
                    Assert.That(control.rect.width, Is.GreaterThanOrEqualTo(72));
                    AssertNoOverlap(control, FindRect(panel.transform, "BossHpFrame"), "顶部操作不能压住血条");
                    AssertNoOverlap(control, FindRect(panel.transform, "BossPhaseBadge"), "阶段信息不能挤入按钮");
                    AssertNoOverlap(control, FindRect(panel.transform, "BattleStageTitle"), "关卡名称不能压住按钮");
                }
                for (int i = 0; i < controls.Length - 1; i++)
                    AssertNoOverlap(controls[i], controls[i + 1], "同排按钮之间必须留出独立触摸区域");
                RectTransform exit = FindRect(panel.transform, "BattleExit");
                Assert.That(exit.gameObject.activeInHierarchy, Is.False);
                Invoke(panel, "TogglePause");
                Assert.That(exit.gameObject.activeInHierarchy, Is.True);
                Assert.That(exit.parent.name, Is.EqualTo("PauseDialog"));
                Assert.That(FindRect(panel.transform, "PauseOverlay").GetComponent<Image>().raycastTarget, Is.True);
                FindRect(panel.transform, "ResumeBattle").GetComponent<Button>().onClick.Invoke();
                Assert.That(panel.IsPaused, Is.False);
                Assert.That(exit.gameObject.activeInHierarchy, Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void BottomSurfaceHidesBakedUiAndStatusNeverCoversDiceOrFaces()
        {
            GameObject root = new GameObject("Battle Bottom Test", typeof(RectTransform));
            try
            {
                TacticsBattlePanel panel = TacticsBattlePanel.Open(root.transform, new GameModel(), CreateBattle(4), null);
                RectTransform surface = FindRect(panel.transform, "BattleCommandSurface");
                Assert.That(surface.GetComponent<Image>().color.a, Is.EqualTo(1f), "旧背景光圈必须完全遮盖");
                Assert.That(Top(surface), Is.LessThanOrEqualTo(840));
                Assert.That(Top(surface) + surface.rect.height, Is.GreaterThanOrEqualTo(1536));
                Text rosterTitle = FindRect(panel.transform, "TeamRoster").GetComponent<Text>();
                Assert.That(rosterTitle.preferredHeight, Is.LessThanOrEqualTo(rosterTitle.rectTransform.rect.height),
                    "中文字体真实行高必须能装进标题框，不能只验证框之间不重叠");
                for (int index = 0; index < 5; index++)
                {
                    RectTransform status = FindRect(panel.transform, "DiceStatus-" + index);
                    Assert.That(status.parent.name, Is.EqualTo("DiceConsole"));
                    AssertNoOverlap(status, FindRect(panel.transform, "Dice-" + index), "保留状态应在骰子下方");
                    AssertNoOverlap(status, FindRect(panel.transform, "DiceReroll"), "状态不能盖住按钮");
                }
                foreach (RectTransform card in panel.GetComponentsInChildren<RectTransform>(true)
                             .Where(item => item.name.StartsWith("Cell-P-")))
                {
                    RectTransform portrait = FindRect(card, "Portrait");
                    Assert.That(portrait.rect.height, Is.GreaterThanOrEqualTo(98));
                    AssertNoOverlap(portrait, FindRect(card, "UnitName"), "名字与头像独立分区");
                    AssertNoOverlap(portrait, FindRect(card, "Hp"), "血条不能盖住脸部");
                    AssertNoOverlap(portrait, FindRect(card, "UnitStatus"), "异常状态不能盖住脸部");
                    Assert.That(FindRect(card, "BattleFrame").GetComponent<Image>().sprite, Is.Null);
                    Text hp = FindRect(card, "UnitHpText").GetComponent<Text>();
                    hp.text = "9999/9999";
                    Assert.That(hp.preferredHeight, Is.LessThanOrEqualTo(hp.rectTransform.rect.height),
                        "成员血量数字必须可见，不能被字体行高裁切");
                }
                RectTransform enemy = FindRect(panel.transform, "Cell-E-0-0");
                AssertNoOverlap(FindRect(enemy, "UnitHpText"), FindRect(enemy, "UnitStatus"),
                    "敌方血量与异常状态必须使用独立行");
                foreach (string id in new[] { "xingli", "yeying", "feiyin", "wubai" })
                {
                    Sprite portrait = (Sprite)InvokeWithResult(panel, "BattlePortrait", id);
                    Sprite original = Resources.Load<Sprite>(GameModel.Members.First(member => member.Id == id).ResourcePath);
                    Assert.That(portrait, Is.Not.Null);
                    Assert.That(portrait.texture, Is.SameAs(original.texture), "只改变显示取景，不替换原立绘");
                    Assert.That(portrait.rect.height, Is.LessThan(original.rect.height * .4f));
                    Assert.That(InvokeWithResult(panel, "BattlePortrait", id), Is.SameAs(portrait), "重复刷新复用裁切精灵");
                }
                BattleUnit player = panel.Battle.Units.First(unit => unit.Side == BattleSide.Player);
                InvokeWithResult(panel, "BuildSkillButtons", player);
                Assert.That(FindRect(panel.transform, "SkillWaitingState").gameObject.activeSelf, Is.False);
                Invoke(panel, "ClearSkillBar");
                Assert.That(FindRect(panel.transform, "SkillWaitingState").gameObject.activeSelf, Is.True);
                Assert.That(FindRect(panel.transform, "SkillWaitingState").GetComponent<Text>().text, Is.Not.Empty);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void SinglePlayerRosterStaysAboveDiceConsoleAndSkillDeck()
        {
            GameObject root = new GameObject("Tactics Layout Test", typeof(RectTransform));
            try
            {
                TacticsBattlePanel panel = TacticsBattlePanel.Open(root.transform, new GameModel(), CreateBattle(4),
                    null);
                RectTransform dice = FindRect(panel.transform, "DiceConsole");
                RectTransform rosterLabel = FindRect(panel.transform, "TeamRoster");
                RectTransform playerCard = FindRect(panel.transform, "Cell-P-0-0");
                RectTransform deck = FindRect(panel.transform, "SkillCommandDeck");
                RectTransform skillBar = FindRect(panel.transform, "SkillBar");
                RectTransform previewBoard = FindRect(panel.transform, "PreviewBoard");

                float diceBottom = Top(dice) + dice.rect.height;
                float labelBottom = Top(rosterLabel) + rosterLabel.rect.height;
                float cardTop = Top(playerCard);
                float cardBottom = cardTop + playerCard.rect.height;
                float deckTop = Top(deck);

                Assert.That(labelBottom, Is.LessThanOrEqualTo(cardTop),
                    "出战成员标题不能压在第一排成员卡上。 ");
                Assert.That(cardBottom, Is.LessThanOrEqualTo(Top(dice)),
                    "成员头像必须在骰子上方，不能覆盖骰子。 ");
                Assert.That(diceBottom, Is.LessThanOrEqualTo(deckTop));
                RectTransform cueArea = FindRect(panel.transform, "CombatCueArea");
                Assert.That(Top(cueArea) + cueArea.rect.height, Is.LessThanOrEqualTo(Top(dice)),
                    "攻击连线裁切范围不能进入骰子操作区。");
                Assert.That(panel.GetComponentsInChildren<Transform>(true).Any(item =>
                    item.name.StartsWith("AttackPlayerProxy") || item.name == "AttackSourcePortrait"), Is.False);
                Assert.That(cardBottom, Is.LessThanOrEqualTo(deckTop),
                    "成员卡不能覆盖技能指令栏。 ");
                RectTransform[] activePlayerCards = panel.GetComponentsInChildren<RectTransform>(true)
                    .Where(item => item.name.StartsWith("Cell-P-") && item.gameObject.activeSelf)
                    .ToArray();
                Assert.That(activePlayerCards, Has.Length.EqualTo(4));
                for (int first = 0; first < activePlayerCards.Length; first++)
                {
                    Assert.That(Top(activePlayerCards[first]), Is.EqualTo(cardTop).Within(0.01f),
                        "四名出战成员应保持在同一行。 ");
                    for (int second = first + 1; second < activePlayerCards.Length; second++)
                        AssertNoOverlap(activePlayerCards[first], activePlayerCards[second],
                            "出战成员卡之间不能互相覆盖。 ");
                }
                Assert.That(Top(skillBar) + skillBar.rect.height, Is.LessThanOrEqualTo(Top(previewBoard)),
                    "技能按钮容器不能进入目标预览区。 ");

                Text preview = FindRect(previewBoard, "TargetPreview").GetComponent<Text>();
                Assert.That(preview.resizeTextForBestFit, Is.True,
                    "目标预览会拼接技能名和多个单位名，必须允许受控缩字号。 ");
                RectTransform teamHealth = FindRect(previewBoard, "TeamHealthSummary");
                AssertNoOverlap(teamHealth, preview.rectTransform,
                    "队伍总生命摘要不能覆盖目标预览说明。");
                Assert.That(teamHealth.gameObject.activeInHierarchy, Is.True);

                RectTransform reroll = FindRect(panel.transform, "DiceReroll");
                RectTransform energyReroll = FindRect(panel.transform, "EnergyReroll");
                RectTransform instruction = FindRect(panel.transform, "DiceInstruction");
                RectTransform diceTitle = FindRect(panel.transform, "DiceConsoleTitle");
                RectTransform diceSummary = FindRect(panel.transform, "DiceHandSummary");
                AssertNoOverlap(diceTitle, diceSummary, "骰子标题不能压住骰型摘要。 ");
                AssertNoOverlap(reroll, energyReroll, "两种重投按钮不能互相覆盖。 ");
                AssertNoOverlap(instruction, reroll, "骰子说明不能压住普通重投按钮。 ");
                AssertNoOverlap(instruction, energyReroll, "骰子说明不能压住能量重投按钮。 ");
                for (int index = 0; index < DiceRules.DiceCount; index++)
                {
                    RectTransform die = FindRect(panel.transform, "Dice-" + index);
                    AssertNoOverlap(die, reroll, $"第 {index + 1} 颗骰子不能压住普通重投按钮。 ");
                    AssertNoOverlap(die, energyReroll, $"第 {index + 1} 颗骰子不能压住能量重投按钮。 ");
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DiceConsoleUsesCalmGlassAndClearActionLanguage()
        {
            GameObject root = new GameObject("Tactics Dice Hierarchy Test", typeof(RectTransform));
            try
            {
                TacticsBattlePanel panel = TacticsBattlePanel.Open(root.transform, new GameModel(), CreateBattle(),
                    null);
                Image console = FindRect(panel.transform, "DiceConsole").GetComponent<Image>();
                Image glow = FindRect(panel.transform, "DiceConsoleGlow").GetComponent<Image>();
                Image diceHitArea = FindRect(panel.transform, "Dice-0").GetComponent<Image>();
                Image diceFace = FindRect(panel.transform, "DiceFace-0").GetComponent<Image>();
                Text reroll = FindRect(panel.transform, "DiceReroll").GetComponentInChildren<Text>(true);
                Text energy = FindRect(panel.transform, "EnergyReroll").GetComponentInChildren<Text>(true);
                Transform highlightNode = FindRect(panel.transform, "Cell-P-0-0").Find("Highlight");
                Assert.That(highlightNode, Is.Not.Null);
                Image playerHighlight = highlightNode.GetComponent<Image>();

                Assert.That(console.color.b, Is.GreaterThan(console.color.r),
                    "骰子台应使用冷色深玻璃，不再以大面积洋红色抢过舞台主体。");
                Assert.That(console.color.a, Is.GreaterThan(0.9f));
                Assert.That(glow.color.a, Is.LessThanOrEqualTo(0.08f),
                    "骰子台氛围光必须克制，不能重新把整块面板染成高饱和色。");
                Assert.That(diceHitArea.sprite, Is.Null,
                    "骰子点击热区不能重新挂载四边形边框美术。");
                Assert.That(diceHitArea.color.a, Is.Zero,
                    "骰子点击热区必须完全透明，只显示骰子本体。");
                Assert.That(diceHitArea.raycastTarget, Is.True,
                    "移除方框后仍要保留完整点击热区。");
                Assert.That(diceFace.preserveAspect, Is.True);
                Assert.That(panel.GetComponentsInChildren<RectTransform>(true)
                    .Any(item => item.name.StartsWith("DicePedestal-")), Is.False,
                    "骰子后方不应再生成额外底座光斑。");
                Assert.That(reroll.text, Does.StartWith("本场剩余 "));
                Assert.That(energy.text, Does.StartWith("全部重投"));
                Assert.That(panel.Battle.BattleDice.Energy, Is.Zero);
                Assert.That(FindRect(panel.transform, "EnergyReroll").GetComponent<Button>().interactable, Is.False);
                Text controlHint = FindRect(panel.transform, "BattleControlInstruction").GetComponent<Text>();
                Assert.That(controlHint.text, Does.Contain("自动释放"));
                Assert.That(controlHint.text, Does.Not.Contain("手动选技能"));
                Assert.That(controlHint.preferredHeight, Is.LessThanOrEqualTo(controlHint.rectTransform.rect.height));
                Assert.That(playerHighlight.color.a, Is.LessThanOrEqualTo(0.25f),
                    "目标高亮应保留角色可见性，而不是形成实色遮挡。");

                BattleUnit player = panel.Battle.Units.First(unit => unit.Side == BattleSide.Player);
                Invoke(panel, "RefreshRealtimeCommands");
                Image skillFrame = FindRect(panel.transform, "RealtimeSkill-0").GetComponent<Image>();
                Text skillLabel = skillFrame.GetComponentInChildren<Text>(true);
                Assert.That(skillLabel.text, Does.Contain("自动 · "), "双技能显示真实冷却，不再等待玩家点击释放。");
                Assert.That(FindRect(panel.transform, "RealtimeSkill-1"), Is.Not.Null);
                Assert.That(skillLabel.preferredHeight, Is.LessThanOrEqualTo(skillLabel.rectTransform.rect.height));
                Assert.That(skillFrame.color.a, Is.EqualTo(1f),
                    "简约技能卡必须挡住 Outline 的内部重复网格，避免整张卡被金色选中框染亮。");
                Assert.That(skillFrame.color.r, Is.LessThan(0.25f));
                Assert.That(skillFrame.color.b, Is.LessThan(0.3f));

                InvokeWithResult(panel, "SetActorHighlight", player);
                Image actorGlow = GetField<Image>(panel, "actorGlow");
                RectTransform playerCard = FindRect(panel.transform, "Cell-P-0-0");
                Assert.That(actorGlow.color.a, Is.LessThanOrEqualTo(0.18f));
                Assert.That(actorGlow.rectTransform.rect.width,
                    Is.LessThanOrEqualTo(playerCard.rect.width * 1.05f),
                    "行动者光晕不应扩张成遮挡相邻成员的大色块。");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [UnityTest]
        public IEnumerator RealtimeCombatContinuesInManualDiceModeAndPauseFreezesItsSingleClock()
        {
            GameObject root = new GameObject("Realtime Clock UI Test", typeof(RectTransform));
            TacticsBattlePanel panel = TacticsBattlePanel.Open(root.transform, new GameModel(), CreateBattle(1, 100000), null);
            try
            {
                Assert.That(panel.AutoMode, Is.False);
                Text autoLabel = FindRect(panel.transform, "AutoToggle").GetComponentInChildren<Text>();
                Assert.That(autoLabel.text, Is.EqualTo("手动重投"));
                Invoke(panel, "ToggleAuto");
                Assert.That(panel.AutoMode, Is.True);
                Assert.That(autoLabel.text, Is.EqualTo("自动重投"));
                Assert.That(autoLabel.preferredHeight, Is.LessThanOrEqualTo(autoLabel.rectTransform.rect.height));
                Invoke(panel, "ToggleAuto");
                Assert.That(autoLabel.text, Is.EqualTo("手动重投"));
                yield return new WaitForSecondsRealtime(2.3f);
                int before = panel.Battle.ElapsedMilliseconds;
                yield return new WaitForSecondsRealtime(0.6f);
                int normalAdvance = panel.Battle.ElapsedMilliseconds - before;
                Assert.That(normalAdvance, Is.GreaterThan(100), "手动重投模式也必须自动推进角色行动");
                Invoke(panel, "TogglePause");
                int pausedAt = panel.Battle.ElapsedMilliseconds;
                int enemyHp = panel.Battle.CurrentEnemyHp;
                yield return new WaitForSecondsRealtime(0.4f);
                Assert.That(panel.Battle.ElapsedMilliseconds, Is.EqualTo(pausedAt));
                Assert.That(panel.Battle.CurrentEnemyHp, Is.EqualTo(enemyHp));
                Invoke(panel, "TogglePause");
                Invoke(panel, "ToggleSpeed");
                before = panel.Battle.ElapsedMilliseconds;
                yield return new WaitForSecondsRealtime(0.6f);
                int doubleAdvance = panel.Battle.ElapsedMilliseconds - before;
                Assert.That(doubleAdvance, Is.GreaterThan(normalAdvance * 1.3f), "2倍速必须作用于模拟器，不能仅加速动画");
                Assert.That(panel.Battle.Log.Any(e => e.Kind == BattleEventKind.Damage), Is.True);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void DiceSummaryAndParticipatingDiceAreRenderedFromHand()
        {
            GameObject root = new GameObject("Tactics Dice Visual Test");
            try
            {
                TacticsBattlePanel panel = root.AddComponent<TacticsBattlePanel>();
                DiceTurn turn = BeginTurnFromFaces(new[] { 1, 1, 3, 4, 6 });
                Assert.That(turn.Values, Is.EqualTo(new[] { 1, 1, 3, 4, 6 }),
                    "测试输入必须按玩家看到的骰面传入，不能混用随机源的零基下标");
                SetField(panel, "diceTurn", turn);
                SetField(panel, "awaitingInput", true);
                Invoke(panel, "LoadBattleAiArt");

                Text summary = NewText("DiceHandSummary", root.transform);
                SetField(panel, "diceHandText", summary);
                List<GameObject> buttons = GetField<List<GameObject>>(panel, "diceButtons");
                List<Image> faceImages = GetField<List<Image>>(panel, "diceFaceImages");
                List<DiceRollPresentation> rolls = GetField<List<DiceRollPresentation>>(panel, "dicePresentations");
                List<Text> statuses = GetField<List<Text>>(panel, "diceHoldLabels");
                List<Outline> outlines = GetField<List<Outline>>(panel, "diceOutlines");
                var images = new List<Image>();
                var labels = new List<Text>();
                for (int index = 0; index < DiceRules.DiceCount; index++)
                {
                    GameObject button = new GameObject("Dice-" + index);
                    button.transform.SetParent(root.transform, false);
                    Image image = button.AddComponent<Image>();
                    button.AddComponent<Button>();
                    Text label = NewText("Label", button.transform);
                    RectTransform rollRig = new GameObject("DiceRollRig-" + index, typeof(RectTransform))
                        .GetComponent<RectTransform>();
                    rollRig.SetParent(button.transform, false);
                    Image faceImage = new GameObject("DiceFace-" + index).AddComponent<Image>();
                    faceImage.transform.SetParent(rollRig, false);
                    DiceRollPresentation roll = rollRig.gameObject.AddComponent<DiceRollPresentation>();
                    roll.Configure(faceImage, rollRig, () => false, () => 1);
                    Text status = NewText("DiceStatus-" + index, button.transform);
                    Outline outline = button.AddComponent<Outline>();
                    buttons.Add(button);
                    faceImages.Add(faceImage);
                    rolls.Add(roll);
                    statuses.Add(status);
                    outlines.Add(outline);
                    images.Add(image);
                    labels.Add(label);
                }

                Invoke(panel, "RefreshDiceUi");

                Assert.That(rolls.All(roll => roll.IsRolling), Is.True,
                    "新骰型必须先进入真实投掷过程，不能直接显示静态结果。");
                Assert.That(statuses.All(status => status.text == "投掷中"), Is.True);
                Assert.That(buttons.All(button => !button.GetComponent<Button>().interactable), Is.True,
                    "未落定时不得再次点击重投或保留。");
                foreach (DiceRollPresentation roll in rolls) roll.AdvancePresentation(.2f);
                Assert.That(rolls.All(roll => roll.IsRolling), Is.True);
                Assert.That(rolls[0].GetComponent<RectTransform>().anchoredPosition, Is.Not.EqualTo(Vector2.zero),
                    "投掷中间态应该有位移，而不仅是状态文字变化。");
                foreach (DiceRollPresentation roll in rolls) roll.AdvancePresentation(1f);
                Invoke(panel, "Update");
                Assert.That(rolls.All(roll => !roll.IsRolling), Is.True);
                Assert.That(buttons.All(button => button.GetComponent<Button>().interactable), Is.True);
                Assert.That(summary.text, Is.EqualTo("一对 ×1.15"));
                Assert.That(statuses[0].text, Is.EqualTo("成型"));
                Assert.That(statuses[1].text, Is.EqualTo("成型"));
                Assert.That(statuses[2].text, Is.Empty);
                for (int index = 0; index < DiceRules.DiceCount; index++)
                {
                    int face = turn.Values[index];
                    Assert.That(faceImages[index].enabled, Is.True);
                    Assert.That(faceImages[index].sprite,
                        Is.SameAs(Resources.Load<Sprite>($"Art/BattleUser/dice-face-{face}-user-v1")));
                    Assert.That(labels[index].text, Is.Empty,
                        "使用用户骰面图时不应再叠加代码数字。");
                }
                Assert.That(images.All(image => image.sprite == null && image.color.a == 0f), Is.True,
                    "所有骰子按钮都只能作为透明点击热区，不能恢复四边形底框。");
                Assert.That(faceImages[0].color, Is.Not.EqualTo(faceImages[2].color),
                    "参与当前牌型的骰子必须通过本体轻微色调显示状态");
                Assert.That(outlines[0].effectDistance.magnitude,
                    Is.GreaterThan(outlines[2].effectDistance.magnitude),
                    "参与当前牌型的骰子必须有更醒目的描边");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AutoTuneReplansHoldsAfterEveryReroll()
        {
            GameObject root = new GameObject("Tactics Dice Auto Test");
            try
            {
                TacticsBattlePanel panel = root.AddComponent<TacticsBattlePanel>();
                DiceTurn turn = BeginTurnFromFaces(new[]
                {
                    2, 2, 4, 5, 6,
                    3, 4, 5,
                    2, 2, 2,
                });
                SetField(panel, "diceTurn", turn);

                Invoke(panel, "AutoTuneDice");

                Assert.That(turn.Values, Is.EqualTo(new[] { 2, 2, 2, 2, 2 }));
                Assert.That(turn.Hand.Pattern, Is.EqualTo(DicePattern.FiveKind));
                Assert.That(turn.Held, Is.EqualTo(new[] { true, true, true, true, true }),
                    "最后一次重投后必须按新牌型重新计算自动保留");
                Assert.That(turn.RerollsRemaining, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void StableDiceSeedUsesStageRoundAndRollSequence()
        {
            MethodInfo method = typeof(TacticsBattlePanel).GetMethod("StableDiceSeed",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            ulong original = (ulong)method.Invoke(null, new object[] { "stage-1-1", 2, 7 });
            ulong repeated = (ulong)method.Invoke(null, new object[] { "stage-1-1", 2, 7 });
            ulong nextStage = (ulong)method.Invoke(null, new object[] { "stage-1-2", 2, 7 });
            ulong nextRound = (ulong)method.Invoke(null, new object[] { "stage-1-1", 3, 7 });
            ulong nextRoll = (ulong)method.Invoke(null, new object[] { "stage-1-1", 2, 8 });

            Assert.That(repeated, Is.EqualTo(original));
            Assert.That(nextStage, Is.Not.EqualTo(original));
            Assert.That(nextRound, Is.Not.EqualTo(original));
            Assert.That(nextRoll, Is.Not.EqualTo(original));
            Assert.That(method.GetParameters()[1].Name, Is.EqualTo("teamRound"));
            Assert.That(method.GetParameters()[2].Name, Is.EqualTo("rollSequence"));
        }

        [Test]
        public void NormalSpeedPacingKeepsSixToNineOperationsNearOneMinute()
        {
            MethodInfo method = typeof(TacticsBattlePanel).GetMethod("EstimateNormalSpeedBattleSeconds",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            float sixOperations = (float)method.Invoke(null, new object[] { 6 });
            float nineOperations = (float)method.Invoke(null, new object[] { 9 });
            Assert.That(sixOperations, Is.InRange(50f, 75f));
            Assert.That(nineOperations, Is.InRange(50f, 75f));
            Assert.That(nineOperations, Is.GreaterThan(sixOperations));
        }

        [Test]
        public void BattlePhaseDisplayWaitsForPendingEventsAndAdvancesInOrder()
        {
            MethodInfo method = typeof(TacticsBattlePanel).GetMethod("ResolveDisplayedEnemyPhase",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            var events = new List<BattleEvent>
            {
                new BattleEvent { Kind = BattleEventKind.PhaseChanged, Phase = 2 },
                new BattleEvent { Kind = BattleEventKind.PhaseChanged, Phase = 3 },
            };

            Assert.That(method.Invoke(null, new object[] { 3, events, 0 }), Is.EqualTo(1));
            Assert.That(method.Invoke(null, new object[] { 3, events, 1 }), Is.EqualTo(2));
            Assert.That(method.Invoke(null, new object[] { 3, events, 2 }), Is.EqualTo(3));
        }

        [Test]
        public void PhaseEventsWriteClearOrderedAnnouncementsToHudAndLog()
        {
            GameObject root = new GameObject("Tactics Phase Event Test");
            try
            {
                TacticsBattlePanel panel = root.AddComponent<TacticsBattlePanel>();
                SetField(panel, "battle", CreateBattle());
                Text phase = NewText("Phase", root.transform);
                Text currentEvent = NewText("Event", root.transform);
                Text log = NewText("Log", root.transform);
                SetField(panel, "phaseText", phase);
                SetField(panel, "eventText", currentEvent);
                SetField(panel, "logText", log);

                InvokeWithResult(panel, "PresentEvent",
                    new BattleEvent { Kind = BattleEventKind.PhaseChanged, Phase = 2 }, -1);
                Assert.That(phase.text, Is.EqualTo("阶段 2/3"));
                Assert.That(currentEvent.text, Is.EqualTo("阶段 2/3 · 敌方增幅"));

                InvokeWithResult(panel, "PresentEvent",
                    new BattleEvent { Kind = BattleEventKind.PhaseChanged, Phase = 3 }, -1);
                Assert.That(phase.text, Is.EqualTo("阶段 3/3"));
                Assert.That(currentEvent.text, Is.EqualTo("阶段 3/3 · 最终乐章"));
                Assert.That(log.text, Is.EqualTo("阶段 2/3 · 敌方增幅\n阶段 3/3 · 最终乐章"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BattleTimerLabelsRemainingTimeAndLegacyReplayOvertime()
        {
            MethodInfo method = typeof(TacticsBattlePanel).GetMethod("FormatBattleTimer",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            Assert.That(method.Invoke(null, new object[] { 0f }), Is.EqualTo("剩余 01:00"));
            Assert.That(method.Invoke(null, new object[] { 60f }), Is.EqualTo("剩余 00:00"));
            Assert.That(method.Invoke(null, new object[] { 61.2f }), Is.EqualTo("加时 +00:02"));

            BattleSimulator battle = CreateBattle();
            Assert.That(battle.Outcome, Is.EqualTo(BattleOutcome.Ongoing),
                "旧回放未推进模拟时，不应由格式化计时文本决定胜负");
        }

        private static DiceTurn BeginTurnFromFaces(int[] faces)
        {
            var picks = new int[faces.Length];
            for (int index = 0; index < faces.Length; index++)
            {
                Assert.That(faces[index], Is.InRange(1, 6), "骰子测试应使用真实点数 1–6");
                picks[index] = faces[index] - 1;
            }

            var turn = new DiceTurn(new ScriptedRandom(new[] { 0 }, picks));
            turn.Begin();
            return turn;
        }

        private static BattleSimulator CreateBattle(int playerCount = 1, int hitPoints = 1000)
        {
            var manifest = new TacticsManifest();
            manifest.Skills.Add(new SkillDefinition
            {
                Id = "strike", Name = "攻击", Effect = SkillEffect.Damage,
                Pattern = SkillPattern.Single, PowerPermille = 1000
            });
            manifest.Units.Add(new UnitDefinition
            {
                Id = "player", Name = "我方", MaxHp = hitPoints, Attack = 100, Defense = 20, Speed = 100,
                SkillIds = new List<string> { "strike" }
            });
            manifest.Units.Add(new UnitDefinition
            {
                Id = "enemy", Name = "敌方", MaxHp = hitPoints, Attack = 50, Defense = 10, Speed = 90,
                SkillIds = new List<string> { "strike" }
            });
            var stage = new StageDefinition
            {
                Id = "phase-ui-test", Name = "阶段测试", TurnLimit = 10,
                Enemies = new List<EnemySpawn>
                {
                    new EnemySpawn { UnitId = "enemy", Row = 0, Col = 0, ScalePermille = 1000 }
                }
            };
            manifest.Stages.Add(stage);
            var party = new List<PlayerUnitSetup>();
            for (int index = 0; index < playerCount; index++)
            {
                party.Add(new PlayerUnitSetup
                {
                    UnitId = "player",
                    Row = index % BattleGrid.Rows,
                    Col = index / BattleGrid.Rows,
                });
            }
            return new BattleSimulator(manifest, stage, party, new ScriptedRandom(new[] { 999 }));
        }

        private static Text NewText(string name, Transform parent)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child.AddComponent<Text>();
        }

        private static RectTransform FindRect(Transform root, string name)
        {
            RectTransform result = root.GetComponentsInChildren<RectTransform>(true)
                .FirstOrDefault(item => item.name == name);
            Assert.That(result, Is.Not.Null, $"未找到布局节点：{name}");
            return result;
        }

        private static float Top(RectTransform rect) => -rect.anchoredPosition.y;

        private static void AssertNoOverlap(RectTransform first, RectTransform second, string message)
        {
            float firstLeft = first.anchoredPosition.x;
            float firstRight = firstLeft + first.rect.width;
            float firstTop = Top(first);
            float firstBottom = firstTop + first.rect.height;
            float secondLeft = second.anchoredPosition.x;
            float secondRight = secondLeft + second.rect.width;
            float secondTop = Top(second);
            float secondBottom = secondTop + second.rect.height;
            bool horizontal = firstLeft < secondRight && firstRight > secondLeft;
            bool vertical = firstTop < secondBottom && firstBottom > secondTop;
            Assert.That(horizontal && vertical, Is.False, message);
        }

        private static T GetField<T>(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"未找到字段：{name}");
            return (T)field.GetValue(target);
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"未找到字段：{name}");
            field.SetValue(target, value);
        }

        private static void Invoke(object target, string name)
        {
            MethodInfo method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"未找到方法：{name}");
            method.Invoke(target, null);
        }

        private static object InvokeWithResult(object target, string name, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"未找到方法：{name}");
            return method.Invoke(target, arguments);
        }
    }
}
