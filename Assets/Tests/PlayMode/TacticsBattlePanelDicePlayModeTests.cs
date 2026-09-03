using System.Collections.Generic;
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
    public sealed class TacticsBattlePanelDicePlayModeTests
    {
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
                    Image faceImage = new GameObject("DiceFace-" + index).AddComponent<Image>();
                    faceImage.transform.SetParent(button.transform, false);
                    Text status = NewText("DiceStatus-" + index, button.transform);
                    Outline outline = button.AddComponent<Outline>();
                    buttons.Add(button);
                    faceImages.Add(faceImage);
                    statuses.Add(status);
                    outlines.Add(outline);
                    images.Add(image);
                    labels.Add(label);
                }

                Invoke(panel, "RefreshDiceUi");

                Assert.That(summary.text, Is.EqualTo("一对 ×1.5\n总点 15 · 成型点 2"));
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
                Assert.That(images[0].color, Is.Not.EqualTo(images[2].color),
                    "参与当前牌型的骰子必须有独立高亮色");
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
        public void BattleTimerUsesOvertimeWithoutEndingTheEncounter()
        {
            MethodInfo method = typeof(TacticsBattlePanel).GetMethod("FormatBattleTimer",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            Assert.That(method.Invoke(null, new object[] { 0f }), Is.EqualTo("目标 01:00"));
            Assert.That(method.Invoke(null, new object[] { 60f }), Is.EqualTo("目标 00:00"));
            Assert.That(method.Invoke(null, new object[] { 61.2f }), Is.EqualTo("加时 +00:02"));

            BattleSimulator battle = CreateBattle();
            Assert.That(battle.Outcome, Is.EqualTo(BattleOutcome.Ongoing),
                "60 秒目标是展示节奏，不应改变模拟器胜负");
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
                Id = "player", Name = "我方", MaxHp = 1000, Attack = 100, Defense = 20, Speed = 100,
                SkillIds = new List<string> { "strike" }
            });
            manifest.Units.Add(new UnitDefinition
            {
                Id = "enemy", Name = "敌方", MaxHp = 1000, Attack = 50, Defense = 10, Speed = 90,
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
            return new BattleSimulator(manifest, stage, new List<PlayerUnitSetup>
            {
                new PlayerUnitSetup { UnitId = "player", Row = 0, Col = 0 }
            }, new ScriptedRandom(new[] { 999 }));
        }

        private static Text NewText(string name, Transform parent)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child.AddComponent<Text>();
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
