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
    public sealed class TacticsBattlePanelDicePlayModeTests
    {
        [Test]
        public void PlayerRosterStaysBetweenDiceConsoleAndSkillDeck()
        {
            GameObject root = new GameObject("Tactics Layout Test", typeof(RectTransform));
            try
            {
                TacticsBattlePanel panel = TacticsBattlePanel.Open(root.transform, new GameModel(), CreateBattle(),
                    null);
                RectTransform dice = FindRect(panel.transform, "DiceConsole");
                RectTransform rosterLabel = FindRect(panel.transform, "TeamRoster");
                RectTransform playerCard = FindRect(panel.transform, "Cell-P-0-0");
                RectTransform deck = FindRect(panel.transform, "SkillCommandDeck");

                float diceBottom = Top(dice) + dice.rect.height;
                float labelBottom = Top(rosterLabel) + rosterLabel.rect.height;
                float cardTop = Top(playerCard);
                float cardBottom = cardTop + playerCard.rect.height;
                float deckTop = Top(deck);

                Assert.That(labelBottom, Is.LessThanOrEqualTo(cardTop),
                    "出战成员标题不能压在第一排成员卡上。 ");
                Assert.That(diceBottom, Is.LessThanOrEqualTo(cardTop),
                    "成员卡不能覆盖骰子台底部的重投操作。 ");
                Assert.That(cardBottom, Is.LessThanOrEqualTo(deckTop),
                    "成员卡不能覆盖技能指令栏。 ");

                Text preview = FindRect(panel.transform, "PreviewBoard").GetComponentInChildren<Text>(true);
                Assert.That(preview.resizeTextForBestFit, Is.True,
                    "目标预览会拼接技能名和多个单位名，必须允许受控缩字号。 ");
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
                Assert.That(reroll.text, Does.StartWith("重投未保留 · "));
                Assert.That(energy.text, Does.StartWith("能量重投"));
                Assert.That(playerHighlight.color.a, Is.LessThanOrEqualTo(0.25f),
                    "目标高亮应保留角色可见性，而不是形成实色遮挡。");

                BattleUnit player = panel.Battle.Units.First(unit => unit.Side == BattleSide.Player);
                InvokeWithResult(panel, "BuildSkillButtons", player);
                Image skillFrame = FindRect(panel.transform, "Skill-strike").GetComponent<Image>();
                Text skillLabel = skillFrame.GetComponentInChildren<Text>(true);
                Assert.That(skillLabel.text, Does.Contain("单体 · 伤害"),
                    "技能按钮应把作用范围和效果分隔，避免两个词黏成难读文案。");
                Assert.That(skillFrame.color.a, Is.LessThanOrEqualTo(0.85f),
                    "技能美术框应退居文字之后，避免高亮边框压过技能名称。");

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

                Assert.That(summary.text, Is.EqualTo("一对 ×1.5\n总点 15 · 计分点 2"));
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

        private static RectTransform FindRect(Transform root, string name)
        {
            RectTransform result = root.GetComponentsInChildren<RectTransform>(true)
                .FirstOrDefault(item => item.name == name);
            Assert.That(result, Is.Not.Null, $"未找到布局节点：{name}");
            return result;
        }

        private static float Top(RectTransform rect) => -rect.anchoredPosition.y;

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
