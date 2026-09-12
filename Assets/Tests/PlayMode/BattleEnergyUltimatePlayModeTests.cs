using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ChoSiren.Panels;
using ChoSiren.Systems;
using ChoSiren.Systems.Dice;
using ChoSiren.Systems.Tactics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.Tests
{
    /// <summary>
    /// v0.3.5 candidate presentation checks for the per-character energy bar, compact pause card,
    /// secondary rules surface, single-die reroll selection and reduced-motion handling.
    /// </summary>
    public sealed class BattleEnergyUltimatePlayModeTests
    {
        [TearDown]
        public void RestoreReduceMotionPreference()
        {
            PlayerPrefs.DeleteKey(BattlePresentationOptions.ReduceMotionKey);
            PlayerPrefs.Save();
        }

        [Test]
        public void PlayerCardsShowBlueEnergyOnItsOwnRowSeparatedFromHp()
        {
            GameObject root = new GameObject("Energy Row Test", typeof(RectTransform));
            try
            {
                TacticsBattlePanel panel = TacticsBattlePanel.Open(root.transform, new GameModel(),
                    CreateBattle(new[] { "xingli" }), null);
                panel.StopAllCoroutines();
                RectTransform card = FindRect(panel.transform, "Cell-P-0-0");
                RectTransform track = FindRect(card, "EnergyTrack");
                RectTransform label = FindRect(card, "EnergyLabel");
                Image fill = FindRect(card, "EnergyFill").GetComponent<Image>();
                Assert.That(fill.color.b, Is.GreaterThan(fill.color.r), "能量条必须是蓝色，与生命区分。");
                AssertNoOverlap(track, FindRect(card, "Hp"), "能量条不能压住生命条");
                AssertNoOverlap(label, FindRect(card, "UnitHpText"), "能量数字不能压住生命数字");
                AssertNoOverlap(track, FindRect(card, "UnitStatus"), "能量条不能压住状态行");
                Assert.That(label.GetComponent<Text>().text, Does.Contain("能量"));
                Assert.That(FindRect(card, "EnergyReadyMark").gameObject.activeSelf, Is.False,
                    "未满能量时不能常驻“大招就绪”。");
                Assert.That(panel.Battle.EnergyOf(panel.Battle.FindUnit(1)), Is.Zero);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void FullEnergyShowsWrittenReadyMarkerAndSoftGlow()
        {
            GameObject root = new GameObject("Energy Ready Test", typeof(RectTransform));
            try
            {
                TacticsBattlePanel panel = TacticsBattlePanel.Open(root.transform, new GameModel(),
                    CreateBattle(new[] { "xingli" }, enemyHp: 100000000), null);
                panel.StopAllCoroutines();
                panel.Battle.AdvanceRealtime(30000);
                Invoke(panel, "RefreshAllCells");

                RectTransform card = FindRect(panel.transform, "Cell-P-0-0");
                Assert.That(panel.Battle.EnergyOf(panel.Battle.FindUnit(1)),
                    Is.EqualTo(BattleSimulator.MaxUnitEnergy));
                Assert.That(FindRect(card, "EnergyLabel").GetComponent<Text>().text,
                    Is.EqualTo("大招就绪 · 满能量"), "满能量必须有文字可用标记，不能只靠颜色。");
                Assert.That(FindRect(card, "EnergyReadyMark").gameObject.activeSelf, Is.True);
                Assert.That(FindRect(card, "EnergyFill").GetComponent<Image>().fillAmount, Is.EqualTo(1f));
                Assert.That(FindRect(card, "EnergyGlow").GetComponent<Image>().color.a,
                    Is.EqualTo(BattleEnergyMeter.ReadyGlowColor.a).Within(0.0001f),
                    "满能量柔光必须直接使用归一化后的 alpha（150/255≈0.588），不能再除以 255。");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void EnergyGlowAlphaStaysNormalizedAcrossReadyNormalReducedAndRepeatedRefresh()
        {
            GameObject root = new GameObject("Energy Glow Alpha Test", typeof(RectTransform));
            try
            {
                TacticsBattlePanel panel = TacticsBattlePanel.Open(root.transform, new GameModel(),
                    CreateBattle(new[] { "xingli" }, enemyHp: 100000000), null);
                panel.StopAllCoroutines();
                RectTransform card = FindRect(panel.transform, "Cell-P-0-0");
                Image glow = FindRect(card, "EnergyGlow").GetComponent<Image>();
                BattleEnergyMeter meter = card.GetComponent<BattleEnergyMeter>();
                Assert.That(meter, Is.Not.Null, "角色卡必须挂载能量计组件。");

                float baseAlpha = BattleEnergyMeter.ReadyGlowColor.a;
                Assert.That(baseAlpha, Is.GreaterThan(0.4f),
                    "ReadyGlowColor 由 Color32 构造，alpha 已是 0–1；若这里很小说明又被除以 255。");
                Assert.That(glow.color.a, Is.EqualTo(0f).Within(0.0001f), "普通（未满能量）状态不应发光。");

                panel.Battle.AdvanceRealtime(30000);
                Invoke(panel, "RefreshAllCells");
                Assert.That(meter.Ready, Is.True, "30 秒存活后应满能量就绪。");
                Assert.That(glow.color.a, Is.EqualTo(baseAlpha).Within(0.0001f),
                    "Refresh 必须直接使用归一化 alpha，否则满能量光晕会几乎不可见。");

                for (int index = 0; index < 4; index++)
                {
                    meter.Refresh();
                    Assert.That(glow.color.a, Is.EqualTo(baseAlpha).Within(0.0001f),
                        "连续刷新不能把透明度越乘越小。");
                }

                BattlePresentationOptions.ReduceMotion = true;
                Invoke(meter, "Update");
                Assert.That(glow.color.a, Is.EqualTo(baseAlpha).Within(0.0001f),
                    "减少动画时满能量光晕稳定在基准透明度，不再脉冲。");

                BattlePresentationOptions.ReduceMotion = false;
                Invoke(meter, "Update");
                Assert.That(glow.color.a, Is.GreaterThanOrEqualTo(baseAlpha * 0.43f),
                    "普通动画的脉冲下限约为基础 alpha 的 44%，绝不应掉到 0.23%。");
                Assert.That(glow.color.a, Is.LessThanOrEqualTo(baseAlpha + 0.0001f),
                    "脉冲上限不能超过归一化基准 alpha。");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void AutoToggleOnlyChangesUltimateModeAndKeepsEveryCharge()
        {
            GameObject root = new GameObject("Auto Mode Test", typeof(RectTransform));
            try
            {
                TacticsBattlePanel panel = TacticsBattlePanel.Open(root.transform, new GameModel(),
                    CreateBattle(new[] { "xingli" }, enemyHp: 100000000), null);
                panel.StopAllCoroutines();
                BattleUnit player = panel.Battle.FindUnit(1);
                panel.Battle.AdvanceRealtime(5000);
                int energy = panel.Battle.EnergyOf(player);
                Assert.That(panel.Battle.AutoCastUltimates, Is.False, "默认是手动大招。");

                Invoke(panel, "ToggleAuto");
                Assert.That(panel.Battle.AutoCastUltimates, Is.True);
                Assert.That(LabelOf(panel, "AutoToggle").text, Is.EqualTo("自动"));
                Assert.That(panel.Battle.EnergyOf(player), Is.GreaterThanOrEqualTo(energy), "切换不能丢能量。");

                Invoke(panel, "ToggleAuto");
                Assert.That(panel.Battle.AutoCastUltimates, Is.False);
                Assert.That(LabelOf(panel, "AutoToggle").text, Is.EqualTo("手动"));
                Assert.That(panel.Battle.EnergyOf(player), Is.GreaterThanOrEqualTo(energy));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void PauseCardIsCompactAndRulesLiveInASecondaryOverlay()
        {
            GameObject root = new GameObject("Pause Rules Test", typeof(RectTransform));
            try
            {
                TacticsBattlePanel panel = TacticsBattlePanel.Open(root.transform, new GameModel(),
                    CreateBattle(new[] { "xingli" }), null);
                panel.StopAllCoroutines();
                Invoke(panel, "TogglePause");

                RectTransform dialog = FindRect(panel.transform, "PauseDialog");
                Assert.That(dialog.rect.height, Is.LessThanOrEqualTo(340f), "暂停面板必须紧凑。");
                Assert.That(dialog.rect.width, Is.LessThanOrEqualTo(420f));
                foreach (string node in new[] { "PauseTitle", "PauseHint", "ResumeBattle", "BattleExit",
                             "BattleRulesEntry" })
                    Assert.That(FindRect(dialog, node), Is.Not.Null, "暂停面板缺少节点：" + node);
                Assert.That(dialog.GetComponentsInChildren<Transform>(true).Any(item => item.name == "BattleRules"),
                    Is.False, "大段规则不能常驻在暂停面板里。");

                RectTransform rules = FindRect(panel.transform, "RulesOverlay");
                Assert.That(rules.gameObject.activeInHierarchy, Is.False, "二级规则默认隐藏。");
                FindRect(dialog, "BattleRulesEntry").GetComponent<Button>().onClick.Invoke();
                Assert.That(rules.gameObject.activeInHierarchy, Is.True);
                Text body = FindRect(rules, "RulesBody").GetComponent<Text>();
                Assert.That(body.text, Does.Contain("能量"));
                Assert.That(body.text, Does.Contain("重投"));
                Assert.That(body.text, Does.Contain("大招"));
                Assert.That(FindRect(rules, "ReduceMotionToggle"), Is.Not.Null, "减少动画开关放在二级说明里。");
                FindRect(rules, "RulesBack").GetComponent<Button>().onClick.Invoke();
                Assert.That(rules.gameObject.activeInHierarchy, Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void TickingDiceBuildsTheRerollCountAndSpendsOneBudget()
        {
            GameObject root = new GameObject("Dice Tick Test", typeof(RectTransform));
            try
            {
                TacticsBattlePanel panel = TacticsBattlePanel.Open(root.transform, new GameModel(),
                    CreateBattle(new[] { "xingli" }), null);
                panel.StopAllCoroutines();
                SetField(panel, "awaitingInput", true);
                SetField(panel, "awaitingDiceLanding", false);
                DiceTurn dice = panel.Battle.BattleDice;
                dice.GainEnergy(DiceTurn.MaxEnergy); // 充能满发 1 次重投
                int budget = dice.RerollsRemaining;
                Assert.That(dice.SelectedForRerollCount, Is.Zero, "开局默认全部保留。");

                FindRect(panel.transform, "Dice-0").GetComponent<Button>().onClick.Invoke();
                FindRect(panel.transform, "Dice-1").GetComponent<Button>().onClick.Invoke();
                Assert.That(FindRect(panel.transform, "DiceStatus-0").GetComponent<Text>().text,
                    Is.EqualTo("待重投"));
                Assert.That(FindRect(panel.transform, "DiceStatus-2").GetComponent<Text>().text,
                    Is.EqualTo("保留"), "没点选的骰子必须显示为保留。");
                Assert.That(LabelOf(panel, "DiceReroll").text, Is.EqualTo("重投已选2颗"));
                Assert.That(FindRect(panel.transform, "DiceReroll").GetComponent<Button>().interactable, Is.True);

                Invoke(panel, "RerollDice");
                Assert.That(dice.UsedRerolls, Is.EqualTo(1), "一次操作只扣一次额度。");
                Assert.That(dice.SelectedForRerollCount, Is.Zero, "重投后的新骰面默认保留。");
                Assert.That(dice.RerollsRemaining, Is.EqualTo(budget - 1));
                Assert.That(LabelOf(panel, "EnergyReroll").text, Does.Contain("全部重投"));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void RerollFeedbackShowsTheGainAndTheCaptainBenefit()
        {
            GameObject root = new GameObject("Reroll Feedback Test", typeof(RectTransform));
            try
            {
                TacticsBattlePanel panel = TacticsBattlePanel.Open(root.transform, new GameModel(),
                    CreateBattle(new[] { "xingli" }), null);
                panel.StopAllCoroutines();
                SetField(panel, "awaitingInput", true);
                SetField(panel, "awaitingDiceLanding", false);
                panel.Battle.BattleDice.GainEnergy(DiceTurn.MaxEnergy); // 充满一条发 1 次重投
                FindRect(panel.transform, "Dice-0").GetComponent<Button>().onClick.Invoke();
                Invoke(panel, "RerollDice");

                Text log = panel.GetComponentsInChildren<Text>(true).First(text => text.name == "BattleLog");
                Assert.That(log.text, Does.Contain("重投 →"), "重投必须留下可见的结算记录。");
                Assert.That(log.text, Does.Contain("累计 +"));
                Assert.That(panel.GetComponentsInChildren<Text>(true)
                    .Any(text => text.name == "Popup" && text.gameObject.activeSelf &&
                        text.text.Contains("骰子加成")), Is.True,
                    "重投后要立刻给出骰子加成弹字。");
                Assert.That(FindRect(panel.transform, "DiceInstruction").GetComponent<Text>().text,
                    Does.Contain("待重投"), "骰子台要说明当前点选状态。");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void CaptainBadgeAndSemanticStatusIconsComeFromTheModel()
        {
            GameObject root = new GameObject("Captain Icon Test", typeof(RectTransform));
            try
            {
                var model = new GameModel();
                model.Save.Team[0] = 2; // 雾白（人鱼，index 2）接任队长，验证护盾图标
                BattleSimulator battle = CreateBattle(new[] { "wubai", "xingli" },
                    rolls: new ScriptedRandom(new[] { 999 }, new[] { 0, 0, 0, 1, 2 }));
                TacticsBattlePanel panel = TacticsBattlePanel.Open(root.transform, model, battle, null);
                panel.StopAllCoroutines();
                Invoke(panel, "RefreshAllCells");
                Invoke(panel, "RefreshRealtimeCommands");

                BattleUnit captain = battle.FindUnit(battle.CurrentLeaderId);
                Assert.That(captain.Definition.Id, Is.EqualTo("wubai"));
                Assert.That(battle.CaptainShieldPermille, Is.GreaterThan(0), "三条开局应给护盾收益。");
                RectTransform card = FindRect(panel.transform, "Cell-P-" + captain.Row + "-" + captain.Col);
                Assert.That(FindRect(card, "CaptainBadge").gameObject.activeSelf, Is.True,
                    "队长标记必须是独立可见徽章，不只靠颜色。");
                BattleStatusIconStrip strip = card.GetComponent<BattleStatusIconStrip>();
                Assert.That(strip, Is.Not.Null);
                Assert.That(strip.VisibleKinds, Does.Contain(SkillIconKind.Shield),
                    "护盾收益要有对应图标。");

                Text captainEffect = FindRect(panel.transform, "CaptainEffect").GetComponent<Text>();
                Assert.That(captainEffect.text, Does.Contain("护盾"),
                    "人鱼指挥必须写明当前骰型实际提供的护盾。");
                Assert.That(captainEffect.text, Does.Contain("五同减伤"),
                    "人鱼种族特性只保留仍成立护盾/五同减伤。");
                Assert.That(captainEffect.text, Does.Not.Contain("精准重投"),
                    "自选重投已对所有队长开放，不能再写成人鱼专属能力。");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void ReduceMotionSkipsCutInAndDiceTumbleButKeepsTheResult()
        {
            GameObject root = new GameObject("Reduce Motion Test", typeof(RectTransform));
            try
            {
                BattlePresentationOptions.ReduceMotion = true;
                SkillCutInPresentation cutIn = root.AddComponent<SkillCutInPresentation>();
                cutIn.Configure(root.transform, () => false, () => 1,
                    () => BattlePresentationOptions.ReduceMotion);
                cutIn.Enqueue(null, "星璃", "魅夜终曲", Color.magenta);
                Assert.That(cutIn.PendingCount, Is.Zero);
                Assert.That(cutIn.IsShowing, Is.False, "减少动画时跳过大招切入。");

                GameObject dieRoot = new GameObject("Die", typeof(RectTransform));
                dieRoot.transform.SetParent(root.transform, false);
                Image face = dieRoot.AddComponent<Image>();
                DiceRollPresentation roll = dieRoot.AddComponent<DiceRollPresentation>();
                roll.Configure(face, dieRoot.GetComponent<RectTransform>(), () => false, () => 1,
                    () => BattlePresentationOptions.ReduceMotion);
                roll.PlayRoll(null, 4, null, 0, true);
                Assert.That(roll.IsRolling, Is.False, "减少动画时骰子直接落定，但结果照常写入。");
                Assert.That(roll.FinalValue, Is.EqualTo(4));

                BattlePresentationOptions.ReduceMotion = false;
                cutIn.Enqueue(null, "星璃", "魅夜终曲", Color.magenta);
                Assert.That(cutIn.IsShowing || cutIn.PendingCount > 0, Is.True, "关闭后恢复完整演出。");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void UltimateCutInIsBoundedAndDrainsItsQueue()
        {
            GameObject root = new GameObject("CutIn Queue Test", typeof(RectTransform));
            try
            {
                SkillCutInPresentation cutIn = root.AddComponent<SkillCutInPresentation>();
                cutIn.Configure(root.transform, () => false, () => 1);
                for (int index = 0; index < 7; index++)
                    cutIn.Enqueue(null, "成员" + index, "终曲" + index, Color.cyan);
                Assert.That(cutIn.PendingCount, Is.EqualTo(4), "最多排队四次，不能无限堆积。");
                cutIn.AdvancePresentation(.2f);
                Assert.That(cutIn.IsShowing, Is.True);
                for (int index = 0; index < 8; index++) cutIn.AdvancePresentation(1.5f);
                Assert.That(cutIn.PendingCount, Is.Zero);
                Assert.That(cutIn.IsShowing, Is.False, "大招切入必须在数秒内清空，不遮挡战场。");
            }
            finally { Object.DestroyImmediate(root); }
        }

        // ------------------------------------------------------------------ helpers

        private static BattleSimulator CreateBattle(IReadOnlyList<string> playerIds, int enemyHp = 100000,
            IRandomSource rolls = null)
        {
            var manifest = new TacticsManifest();
            manifest.Skills.Add(new SkillDefinition
            {
                Id = "strike", Name = "攻击", Effect = SkillEffect.Damage,
                Pattern = SkillPattern.Single, PowerPermille = 1000
            });
            foreach (string id in playerIds)
                manifest.Units.Add(new UnitDefinition
                {
                    Id = id, Name = id, MaxHp = 100000, Attack = 100, Defense = 20, Speed = 100,
                    SkillIds = new List<string> { "strike" }
                });
            manifest.Units.Add(new UnitDefinition
            {
                Id = "enemy", Name = "敌方", MaxHp = enemyHp, Attack = 1, Defense = 10, Speed = 50,
                SkillIds = new List<string> { "strike" }
            });
            var stage = new StageDefinition
            {
                Id = "energy-ui-test", Name = "能量界面测试", EncounterType = "normal",
                TurnLimit = 20, TimeLimitSeconds = 120, ThreeStarSeconds = 90,
                Enemies = new List<EnemySpawn> { new EnemySpawn { UnitId = "enemy", Row = 0, Col = 0 } }
            };
            manifest.Stages.Add(stage);
            var party = new List<PlayerUnitSetup>();
            for (int index = 0; index < playerIds.Count; index++)
                party.Add(new PlayerUnitSetup { UnitId = playerIds[index], Row = index % 3, Col = index / 3 });
            return new BattleSimulator(manifest, stage, party,
                rolls ?? new ScriptedRandom(new[] { 999 }, new[] { 0, 1, 2, 3, 5 }));
        }

        private static Text LabelOf(TacticsBattlePanel panel, string buttonName) =>
            FindRect(panel.transform, buttonName).GetComponentInChildren<Text>(true);

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

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "未找到字段：" + name);
            field.SetValue(target, value);
        }

        private static void Invoke(object target, string name)
        {
            MethodInfo method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "未找到方法：" + name);
            method.Invoke(target, null);
        }
    }
}
