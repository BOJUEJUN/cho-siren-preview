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
        public void RealtimeCommandsRemoveStrikeAndKeepGuardUsableWithPauseProtection()
        {
            var root = new GameObject("Tactical Commands", typeof(RectTransform));
            try
            {
                var battle = CreateBattle();
                battle.EnableRealtime(new Dictionary<string, string> { { "player", "魅族" } }, "player");
                var panel = TacticsBattlePanel.Open(root.transform, new GameModel(), battle, null);
                Invoke(panel, "RefreshRealtimeCommands");
                var guard = FindRect(panel.transform, "TacticalGuard");
                Assert.That(panel.GetComponentsInChildren<Transform>(true)
                    .Any(item => item.name == "TacticalInterrupt"), Is.False,
                    "Removed strike must not survive as an invisible selectable or callback.");
                Assert.That(guard.GetComponent<Button>().interactable, Is.True);
                Assert.That(battle.InterruptCooldownRemaining, Is.Zero);
                Invoke(panel, "TogglePause");
                guard.GetComponent<Button>().onClick.Invoke();
                Assert.That(battle.GuardCooldownRemaining, Is.Zero);
                Invoke(panel, "TogglePause");
                Invoke(panel, "RefreshRealtimeCommands");
                guard.GetComponent<Button>().onClick.Invoke();
                Assert.That(battle.GuardCooldownRemaining, Is.GreaterThan(0));
                Assert.That(guard.GetComponent<Button>().interactable, Is.False);
                Assert.That(battle.InterruptCooldownRemaining, Is.Zero);
                Assert.That(panel.GetComponentsInChildren<Selectable>(true)
                    .Any(item => item.name == "TacticalInterrupt"), Is.False);
                Assert.That(panel.GetComponentsInChildren<Text>(true).Any(t => t.text.Contains("等待敌人蓄力")), Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }

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
                Assert.That(timer.text, Is.EqualTo("00:20"));
                Assert.That(timer.color, Is.Not.EqualTo(normal));
                var phase = (Text)typeof(TacticsBattlePanel).GetField("phaseText", flags).GetValue(panel);
                Assert.That(phase.text, Is.EqualTo($"{battle.CurrentWave}/{battle.TotalWaves}"));
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
                Text phaseLabel = GetField<Text>(panel, "phaseText");
                Assert.That(phaseLabel.text, Does.Contain("/"));
                RectTransform[] controls = { FindRect(panel.transform, "AutoToggle"),
                    FindRect(panel.transform, "SpeedToggle"), FindRect(panel.transform, "PauseToggle") };
                foreach (RectTransform control in controls)
                {
                    Assert.That(control.gameObject.activeInHierarchy, Is.True);
                    Assert.That(control.rect.height, Is.GreaterThanOrEqualTo(52));
                    Assert.That(control.rect.width, Is.GreaterThanOrEqualTo(60));
                    Assert.That(Top(control) + control.rect.height,
                        Is.LessThan(Top(FindRect(panel.transform, "PSD-battle-52"))),
                        "新版 PSD 控件仍必须在总血条上方。");
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
        public void CleanStageKeepsAspectRatioAndStatusNeverCoversDiceOrFaces()
        {
            GameObject root = new GameObject("Battle Bottom Test", typeof(RectTransform));
            try
            {
                TacticsBattlePanel panel = TacticsBattlePanel.Open(root.transform, new GameModel(), CreateBattle(4), null);
                var stageArt = FindRect(panel.transform, "PSD-stage").GetComponent<Image>();
                Assert.That(stageArt.sprite, Is.Not.Null);
                Assert.That(stageArt.rectTransform.rect.width / stageArt.rectTransform.rect.height,
                    Is.EqualTo(stageArt.sprite.rect.width / stageArt.sprite.rect.height).Within(.002f),
                    "新版 PSD 背景必须保持原图比例。");
                Assert.That(FindRect(panel.transform, "BattleCommandSurface").gameObject.activeInHierarchy, Is.False);
                for (int index = 0; index < DiceRules.DiceCount; index++)
                {
                    var status = FindRect(panel.transform, "DiceStatus-" + index);
                    Assert.That(status.parent.name, Is.EqualTo("BattlePSD928"));
                    AssertNoOverlap(status, FindRect(panel.transform, "Dice-" + index), "重投状态应在骰子下方");
                    AssertNoOverlap(status, FindRect(panel.transform, "PsdReroll"), "状态不能盖住心形按钮");
                }
                foreach (var card in panel.GetComponentsInChildren<RectTransform>(true)
                             .Where(item => item.name.StartsWith("Cell-P-") && item.gameObject.activeInHierarchy))
                {
                    var portrait = FindRect(card, "Portrait");
                    Assert.That(portrait.rect.height, Is.GreaterThanOrEqualTo(98));
                    Assert.That(FindRect(card, "PsdUnitFrame").GetComponent<Image>().sprite, Is.Not.Null);
                    var hp = FindRect(card, "UnitHpText").GetComponent<Text>();
                    hp.text = "9999/9999";
                    Assert.That(hp.preferredHeight, Is.LessThanOrEqualTo(hp.rectTransform.rect.height),
                        "成员血量数字必须可见，不能被字体行高裁切");
                }
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
                var firstDie = FindRect(panel.transform, "Dice-0");
                var activeCards = panel.GetComponentsInChildren<RectTransform>(true)
                    .Where(item => item.name.StartsWith("Cell-P-") && item.gameObject.activeInHierarchy).ToArray();
                Assert.That(activeCards, Has.Length.EqualTo(4));
                foreach (var card in activeCards)
                {
                    Assert.That(Top(card) + card.rect.height, Is.LessThan(Top(firstDie)),
                        "出战成员卡应完整显示在六骰操作区域上方。");
                    Assert.That(Top(card), Is.EqualTo(Top(activeCards[0])).Within(.01f));
                }
                for (int i = 0; i < activeCards.Length - 1; i++)
                    AssertNoOverlap(activeCards[i], activeCards[i + 1], "角色点击热区必须独立，图案可以出框。");
                Assert.That(FindRect(panel.transform, "TeamRoster").gameObject.activeInHierarchy, Is.False,
                    "旧标题不应遮住新版唱片造型卡片。");
                Assert.That(FindRect(panel.transform, "SkillCommandDeck").gameObject.activeInHierarchy, Is.False);
                Assert.That(FindRect(panel.transform, "SkillBar").gameObject.activeInHierarchy, Is.False);
                Assert.That(FindRect(panel.transform, "PreviewBoard").gameObject.activeInHierarchy, Is.False);
                var cue = FindRect(panel.transform, "CombatCueArea");
                Assert.That(Top(cue) + cue.rect.height, Is.LessThan(Top(firstDie)), "攻击连线不得进入骰子操作区。");
                var heart = FindRect(panel.transform, "PsdReroll");
                Assert.That(heart.gameObject.activeInHierarchy, Is.True);
                Assert.That(FindRect(panel.transform, "DiceReroll").gameObject.activeInHierarchy, Is.False);
                Assert.That(FindRect(panel.transform, "EnergyReroll").gameObject.activeInHierarchy, Is.False);
                for (int i = 0; i < DiceRules.DiceCount; i++)
                {
                    var die = FindRect(panel.transform, "Dice-" + i);
                    AssertNoOverlap(die, heart, "心形重投按钮不能压住骰子。");
                    Assert.That(Top(die) + die.rect.height, Is.LessThan(1536));
                    if (i > 0) AssertNoOverlap(FindRect(panel.transform, "Dice-" + (i - 1)), die, "六颗骰子的热区不得重叠。");
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
                Assert.That(console.color.a, Is.InRange(.70f, .90f),
                    "最新参考使用黑色半透明底，保留舞台纹理且不形成实色大块。");
                Assert.That(Mathf.Max(console.color.r, console.color.g, console.color.b), Is.LessThan(.12f));
                Assert.That(console.GetComponent<Outline>(), Is.Null,
                    "不能对整个半透明底重复描边填色；发光必须局限在边线上。");
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
                Assert.That(reroll.text, Is.EqualTo("重投已选0颗"), "开局全部保留，必须先点选要重投的骰子。");
                Assert.That(energy.text, Does.StartWith("全部重投"), "2026-10-31: 不选骰子时点重投 = 6 颗全部重投");
                Assert.That(panel.Battle.BattleDice.Energy, Is.Zero);
                // 同步测试不会推进骰子落地动画；模拟落地后的稳定状态再验证重投额度。
                SetField(panel, "awaitingInput", true);
                SetField(panel, "awaitingDiceLanding", false);
                Invoke(panel, "RefreshDiceUi");
                Assert.That(FindRect(panel.transform, "EnergyReroll").GetComponent<Button>().interactable, Is.False,
                    "开局没有重投次数，必须先积累重投充能。");
                panel.Battle.BattleDice.GainEnergy(DiceTurn.MaxEnergy);
                Invoke(panel, "RefreshDiceUi");
                Assert.That(FindRect(panel.transform, "EnergyReroll").GetComponent<Button>().interactable, Is.True,
                    "充能充满自动发放一次重投，此时即可全部重投。");
                Text controlHint = FindRect(panel.transform, "BattleControlInstruction").GetComponent<Text>();
                Assert.That(controlHint.text, Does.Contain("自动释放"));
                Assert.That(controlHint.text, Does.Not.Contain("手动选技能"));
                Assert.That(controlHint.preferredHeight, Is.LessThanOrEqualTo(controlHint.rectTransform.rect.height));
                Assert.That(playerHighlight.color.a, Is.LessThanOrEqualTo(0.25f),
                    "目标高亮应保留角色可见性，而不是形成实色遮挡。");

                BattleUnit player = panel.Battle.Units.First(unit => unit.Side == BattleSide.Player);
                Invoke(panel, "RefreshRealtimeCommands");
                Assert.That(panel.GetComponentsInChildren<Transform>(true)
                    .Count(item => item.name.StartsWith("RealtimeSkill-")), Is.Zero,
                    "头像下方的两条常驻自动技能倒计时卡必须删除。");
                Text summary = FindRect(panel.transform, "SkillWaitingState").GetComponent<Text>();
                Assert.That(summary.text, Does.Contain("普攻与小技能自动"));
                Assert.That(summary.text, Does.Contain("大招"));
                Assert.That(summary.text, Does.Not.Contain("秒"), "摘要里不能再出现技能倒计时。");
                Assert.That(summary.gameObject.activeInHierarchy, Is.False,
                    "自动普攻和大招能量不再作为页尾文字显示，技能详情入口仍可用。");
                Assert.That(FindRect(panel.transform, "AbilityDetails").GetComponent<Button>().interactable, Is.True,
                    "角色能力详情入口必须保留。");
                FindRect(panel.transform, "AbilityDetails").GetComponent<Button>().onClick.Invoke();
                Text abilityBody = FindRect(panel.transform, "AbilityBody").GetComponent<Text>();
                Assert.That(abilityBody.text, Does.Contain("小技能"));
                Assert.That(abilityBody.text, Does.Contain("大招"));
                FindRect(panel.transform, "AbilityClose").GetComponent<Button>().onClick.Invoke();
                Assert.That(FindRect(panel.transform, "AbilityOverlay").gameObject.activeInHierarchy, Is.False);

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
                Assert.That(autoLabel.text, Is.EqualTo("手动"));
                Invoke(panel, "ToggleAuto");
                Assert.That(panel.AutoMode, Is.True);
                Assert.That(autoLabel.text, Is.EqualTo("自动"));
                Assert.That(autoLabel.preferredHeight, Is.LessThanOrEqualTo(autoLabel.rectTransform.rect.height));
                Invoke(panel, "ToggleAuto");
                Assert.That(autoLabel.text, Is.EqualTo("手动"));
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
                DiceTurn turn = BeginTurnFromFaces(new[] { 1, 1, 2, 3, 4, 6 });
                Assert.That(turn.Values, Is.EqualTo(new[] { 1, 1, 2, 3, 4, 6 }),
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
                    2, 2, 1, 3, 4, 6,
                    1, 3, 4, 6,
                    2, 2, 2, 2,
                });
                SetField(panel, "diceTurn", turn);

                Invoke(panel, "AutoTuneDice");

                Assert.That(turn.Values, Is.EqualTo(new[] { 2, 2, 2, 2, 2, 2 }));
                Assert.That(turn.Hand.Pattern, Is.EqualTo(DicePattern.SixKind));
                Assert.That(turn.Held, Is.EqualTo(new[] { true, true, true, true, true, true }),
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
