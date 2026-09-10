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
    /// v0.3.5 dice/battle feedback: the floating numbers and log must explain how much of a hit
    /// came from the dice bonus and how much was eaten by a shield instead of silently changing HP.
    /// </summary>
    public sealed class BattleFeedbackAttributionPlayModeTests
    {
        [Test]
        public void PlayerDamagePopupAndLogNameTheDiceMultiplier()
        {
            GameObject root = new GameObject("Dice attribution popup", typeof(RectTransform));
            try
            {
                TacticsBattlePanel panel = TacticsBattlePanel.Open(root.transform, new GameModel(), CreateBattle(), null);
                panel.StopAllCoroutines();
                BattleUnit player = panel.Battle.Units.First(unit => unit.Side == BattleSide.Player);
                BattleUnit enemy = panel.Battle.Units.First(unit => unit.Side == BattleSide.Enemy);

                Present(panel, new BattleEvent
                {
                    Kind = BattleEventKind.Damage, ActorId = player.Id, TargetId = enemy.Id,
                    SkillId = "strike", Amount = 100, HpLostAmount = 100, DiceMultiplierPermille = 1450
                });

                Assert.That(ActivePopups(panel).Any(text => text.Contains("-100") && text.Contains("×1.45")),
                    Is.True, "我方伤害弹字必须同时给出伤害与本次骰子累计倍率。实际弹字: [" +
                    string.Join(" | ", ActivePopups(panel)) + "] 诊断: 弹字池节点" +
                    panel.GetComponentsInChildren<Text>(true).Count(t => t.name == "Popup") + "个 / 激活" +
                    panel.GetComponentsInChildren<Text>(true).Count(t => t.name == "Popup" && t.gameObject.activeSelf) + "个 / 敌方格子" +
                    (FindRectOrNull(panel.transform, "Cell-E-0-0") != null ? "有" : "无"));
                Assert.That(LogText(panel).text, Does.Contain("骰子 ×1.45"));
                Assert.That(LogText(panel).text, Does.Contain("造成 100 伤害"));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void EnemyDamagePopupDoesNotInventADiceMultiplier()
        {
            GameObject root = new GameObject("Enemy damage attribution popup", typeof(RectTransform));
            try
            {
                TacticsBattlePanel panel = TacticsBattlePanel.Open(root.transform, new GameModel(), CreateBattle(), null);
                panel.StopAllCoroutines();
                BattleUnit player = panel.Battle.Units.First(unit => unit.Side == BattleSide.Player);
                BattleUnit enemy = panel.Battle.Units.First(unit => unit.Side == BattleSide.Enemy);

                Present(panel, new BattleEvent
                {
                    Kind = BattleEventKind.Damage, ActorId = enemy.Id, TargetId = player.Id,
                    SkillId = "strike", Amount = 40, HpLostAmount = 40, DiceMultiplierPermille = 1000
                });

                Assert.That(ActivePopups(panel).Any(text => text.Contains("-40") && text.Contains("×")),
                    Is.False, "敌方伤害没有骰子加成，不能显示 ×1.00 或任何倍率。");
                Assert.That(LogText(panel).text, Does.Not.Contain("骰子 ×"));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void ShieldAbsorptionPopupSeparatesHpLossFromAbsorbedDamage()
        {
            GameObject root = new GameObject("Shield absorption popup", typeof(RectTransform));
            try
            {
                TacticsBattlePanel panel = TacticsBattlePanel.Open(root.transform, new GameModel(), CreateBattle(), null);
                panel.StopAllCoroutines();
                BattleUnit player = panel.Battle.Units.First(unit => unit.Side == BattleSide.Player);
                BattleUnit enemy = panel.Battle.Units.First(unit => unit.Side == BattleSide.Enemy);

                Present(panel, new BattleEvent
                {
                    Kind = BattleEventKind.Damage, ActorId = player.Id, TargetId = enemy.Id,
                    SkillId = "strike", Amount = 100, AbsorbedAmount = 80, HpLostAmount = 20
                });
                Assert.That(ActivePopups(panel).Any(text => text.Contains("-20") && text.Contains("护盾-80")),
                    Is.True, "部分吸收必须显示真实掉血，而不是含护盾的总伤害。实际弹字: [" +
                    string.Join(" | ", ActivePopups(panel)) + "]");
                Assert.That(LogText(panel).text, Does.Contain("护盾吸收 80"));

                Present(panel, new BattleEvent
                {
                    Kind = BattleEventKind.Damage, ActorId = player.Id, TargetId = enemy.Id,
                    SkillId = "strike", Amount = 60, AbsorbedAmount = 60, HpLostAmount = 0
                });
                Assert.That(ActivePopups(panel).Any(text => text.Contains("护盾吸收 60")),
                    Is.True, "全吸收要明确写成护盾吸收，不能显示 -0。");
                Assert.That(ActivePopups(panel).Any(text => text.Contains("-0")), Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void DiceConsoleShowsCappedBonusInsteadOfFakeGrowth()
        {
            GameObject root = new GameObject("Dice cap feedback", typeof(RectTransform));
            try
            {
                TacticsBattlePanel panel = TacticsBattlePanel.Open(root.transform, new GameModel(), CreateBattle(), null);
                panel.StopAllCoroutines();
                Invoke(panel, "RefreshDiceUi");
                Text instruction = FindRect(panel.transform, "DiceInstruction").GetComponent<Text>();
                Assert.That(instruction.text, Does.Contain("累计 +"));
                Assert.That(instruction.text, Does.Not.Contain("已封顶"));

                DiceTurn dice = panel.Battle.BattleDice;
                typeof(DiceTurn).GetProperty("AccumulatedBonusPermille").GetSetMethod(true)
                    .Invoke(dice, new object[] { DiceTurn.MaxBattleBonusPermille });
                Invoke(panel, "RefreshDiceUi");

                Assert.That(instruction.text, Does.Contain("已封顶"), "累计 +100% 后必须明确封顶，不能显示 +0% 的假增长。");
                Assert.That(instruction.text, Does.Contain("×2"));
                Assert.That(instruction.text, Does.Not.Contain("本次 +0"));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void DiceConsoleNamesTheCaptainBenefitAndRerollCounts()
        {
            GameObject root = new GameObject("Dice benefit readout", typeof(RectTransform));
            try
            {
                TacticsBattlePanel panel = TacticsBattlePanel.Open(root.transform, new GameModel(), CreateBattle(), null);
                panel.StopAllCoroutines();
                // 同步测试不会推进骰子落地协程，模拟落地后的稳定状态，断言的是落地后的可读性。
                SetField(panel, "awaitingInput", true);
                SetField(panel, "awaitingDiceLanding", false);
                Invoke(panel, "RefreshDiceUi");
                Invoke(panel, "RefreshRealtimeCommands");

                Text instruction = FindRect(panel.transform, "DiceInstruction").GetComponent<Text>();
                Text hand = FindRect(panel.transform, "DiceHandSummary").GetComponent<Text>();
                Assert.That(hand.text, Does.Contain("五条"), "默认测试骰面为五条，摘要必须显示真实骰型。");
                // 战斗内倍率是「累计」口径（起手 +25%，封顶 ×2），摘要必须与模型读数一致，不得展示假倍率。
                Assert.That(hand.text, Does.Contain(
                    $"累计 ×{panel.Battle.BattleDice.DamageMultiplierPermille / 1000f:0.##}"),
                    "骰型摘要的倍率必须与模型当前累计读数一致。");
                Assert.That(instruction.text, Does.Contain("累计 +"));
                Assert.That(instruction.text, Does.Contain("魅族追击 3次"),
                    "默认队长星璃是魅族，五条应显示 3 次追击，而不是硬编码文案。");
                Text captainEffect = FindRect(panel.transform, "CaptainEffect").GetComponent<Text>();
                Assert.That(captainEffect.text, Does.Contain("当前骰型追击 3 次"),
                    "流派说明必须写明当前骰型实际给到的收益。");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void RapidRerollClicksSpendOnlyOneBattleBudget()
        {
            GameObject root = new GameObject("Reroll double click", typeof(RectTransform));
            try
            {
                TacticsBattlePanel panel = TacticsBattlePanel.Open(root.transform, new GameModel(), CreateBattle(), null);
                panel.StopAllCoroutines();
                SetField(panel, "awaitingInput", true);
                SetField(panel, "awaitingDiceLanding", false);
                DiceTurn dice = panel.Battle.BattleDice;
                dice.GainEnergy(DiceTurn.MaxEnergy);
                int budget = dice.RerollsRemaining;
                Assert.That(budget, Is.GreaterThan(0));

                Invoke(panel, "EnergyRerollDice");
                Invoke(panel, "EnergyRerollDice");
                Invoke(panel, "EnergyRerollDice");

                Assert.That(dice.UsedRerolls, Is.EqualTo(1), "连点不能重复扣重投次数。");
                Assert.That(dice.RerollsRemaining, Is.EqualTo(budget - 1));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void CaptainCardIsGoldWhileSelectionStaysCyan()
        {
            GameObject root = new GameObject("Captain badge", typeof(RectTransform));
            try
            {
                TacticsBattlePanel panel = TacticsBattlePanel.Open(root.transform, new GameModel(), CreateBattle(), null);
                panel.StopAllCoroutines();
                Invoke(panel, "RefreshAllCells");
                Invoke(panel, "RefreshRealtimeCommands");

                BattleUnit captain = panel.Battle.FindUnit(panel.Battle.CurrentLeaderId);
                Assert.That(captain, Is.Not.Null);
                RectTransform captainCard = FindRect(panel.transform, "Cell-P-" + captain.Row + "-" + captain.Col);
                Text captainName = FindRect(captainCard, "UnitName").GetComponent<Text>();
                Assert.That(captainName.color.r, Is.GreaterThan(0.9f));
                Assert.That(captainName.color.g, Is.GreaterThan(0.7f), "队长名字必须用金色标出。");

                BattleUnit other = panel.Battle.Units.First(unit => unit.Side == BattleSide.Player &&
                    unit.Id != captain.Id);
                RectTransform otherCard = FindRect(panel.transform, "Cell-P-" + other.Row + "-" + other.Col);
                Color otherNameColor = FindRect(otherCard, "UnitName").GetComponent<Text>().color;
                Assert.That(otherNameColor.r, Is.LessThan(0.95f), "普通队员名字不能也是金色。");

                // Inspecting another member moves the cyan selection frame; the captain keeps gold.
                SetField(panel, "inputActor", other);
                Invoke(panel, "RefreshRealtimeCommands");
                Outline captainOutline = captainCard.GetComponent<Outline>();
                Assert.That(captainOutline.effectColor.r, Is.GreaterThan(0.9f));
                Assert.That(captainOutline.effectColor.g, Is.GreaterThan(0.7f));
                Assert.That(captainOutline.effectColor.a, Is.GreaterThan(0.5f),
                    "队长必须始终保留金色描边徽章。");
                Outline otherOutline = otherCard.GetComponent<Outline>();
                Assert.That(otherOutline.effectColor.b, Is.GreaterThan(0.8f), "被查看的队员保留青色选中框。");
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "未找到字段：" + name);
            field.SetValue(target, value);
        }

        private static List<string> ActivePopups(TacticsBattlePanel panel) => panel
            .GetComponentsInChildren<Text>(true)
            .Where(text => text.name == "Popup" && text.gameObject.activeSelf)
            .Select(text => text.text)
            .ToList();

        private static Text LogText(TacticsBattlePanel panel) => panel
            .GetComponentsInChildren<Text>(true).First(text => text.name == "BattleLog");

        private static BattleSimulator CreateBattle()
        {
            var manifest = new TacticsManifest();
            manifest.Skills.Add(new SkillDefinition
            {
                Id = "strike", Name = "攻击", Effect = SkillEffect.Damage,
                Pattern = SkillPattern.Single, PowerPermille = 1000
            });
            manifest.Units.Add(new UnitDefinition
            {
                Id = "player", Name = "我方", MaxHp = 100000, Attack = 100, Defense = 20, Speed = 100,
                SkillIds = new List<string> { "strike" }
            });
            // 队长卡测试需要第二名队员对比金色/青色描边，原夹具只有一人会让 First() 直接抛空。
            manifest.Units.Add(new UnitDefinition
            {
                Id = "player2", Name = "队友", MaxHp = 100000, Attack = 100, Defense = 20, Speed = 100,
                SkillIds = new List<string> { "strike" }
            });
            manifest.Units.Add(new UnitDefinition
            {
                Id = "enemy", Name = "敌方", MaxHp = 100000, Attack = 50, Defense = 10, Speed = 90,
                SkillIds = new List<string> { "strike" }
            });
            var stage = new StageDefinition
            {
                Id = "feedback-attribution-test", Name = "反馈归因", TurnLimit = 10,
                // 普通遭遇：弹字走格子弹字池；不设会落进 boss 表现层，导致弹字断言拿不到 Popup 节点。
                EncounterType = "normal",
                Enemies = new List<EnemySpawn> { new EnemySpawn { UnitId = "enemy", Row = 0, Col = 0 } }
            };
            manifest.Stages.Add(stage);
            return new BattleSimulator(manifest, stage,
                new[]
                {
                    new PlayerUnitSetup { UnitId = "player", Row = 0, Col = 0, Level = 1 },
                    new PlayerUnitSetup { UnitId = "player2", Row = 0, Col = 1, Level = 1 },
                },
                new ScriptedRandom(new[] { 999 }));
        }

        private static void Present(TacticsBattlePanel panel, BattleEvent entry) =>
            typeof(TacticsBattlePanel).GetMethod("PresentEvent", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(panel, new object[] { entry, -1 });

        private static void Invoke(TacticsBattlePanel panel, string method) =>
            typeof(TacticsBattlePanel).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(panel, null);

        private static RectTransform FindRect(Transform root, string name)
        {
            RectTransform result = root.GetComponentsInChildren<RectTransform>(true)
                .FirstOrDefault(item => item.name == name);
            Assert.That(result, Is.Not.Null, "未找到布局节点：" + name);
            return result;
        }

        private static RectTransform FindRectOrNull(Transform root, string name) =>
            root.GetComponentsInChildren<RectTransform>(true)
                .FirstOrDefault(item => item.name == name);
    }
}
