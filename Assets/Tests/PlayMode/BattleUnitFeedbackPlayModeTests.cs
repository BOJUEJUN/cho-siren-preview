using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ChoSiren.Panels;
using ChoSiren.Systems;
using ChoSiren.Systems.Data;
using ChoSiren.Systems.Tactics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.Tests
{
    public sealed class BattleUnitFeedbackPlayModeTests
    {
        private bool hadSave, hadLegacy;
        private string saved, legacy;

        [SetUp]
        public void PreserveSave()
        {
            hadSave = PlayerPrefs.HasKey(GameModel.SaveKey);
            hadLegacy = PlayerPrefs.HasKey(GameModel.LegacySaveKey);
            saved = PlayerPrefs.GetString(GameModel.SaveKey);
            legacy = PlayerPrefs.GetString(GameModel.LegacySaveKey);
        }

        [TearDown]
        public void RestoreSave()
        {
            if (hadSave) PlayerPrefs.SetString(GameModel.SaveKey, saved); else PlayerPrefs.DeleteKey(GameModel.SaveKey);
            if (hadLegacy) PlayerPrefs.SetString(GameModel.LegacySaveKey, legacy); else PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
        }

        [Test]
        public void SkillEffectsUseActualRecipientsAndReplaceCrossScreenTrails()
        {
            using (var fixture = new Fixture(false))
            {
                var caster = fixture.Battle.Units.First(u => u.Side == BattleSide.Player);
                var enemy = fixture.Battle.Units.First(u => u.Side == BattleSide.Enemy);
                int hp = enemy.Hp;
                Present(fixture.Panel, new BattleEvent { Kind = BattleEventKind.Damage, ActorId = caster.Id,
                    TargetId = enemy.Id, SkillId = fixture.Battle.ActiveSkillId(caster, true), Amount = 20 });
                var fx = fixture.Panel.GetComponent<SkillEffectPresentation>();
                Assert.That(fx.ActiveCount, Is.EqualTo(1));
                var trajectory = fixture.Panel.GetComponent<AttackTrajectoryPresentation>();
                Assert.That(trajectory.ActiveCount, Is.EqualTo(1), "真实伤害事件必须接入攻击者到目标的因果提示。");
                Assert.That(enemy.Hp, Is.EqualTo(hp), "特效不能重复执行伤害。");
                Present(fixture.Panel, new BattleEvent { Kind = BattleEventKind.Heal, ActorId = caster.Id,
                    TargetId = caster.Id, Amount = 10 });
                Present(fixture.Panel, new BattleEvent { Kind = BattleEventKind.Shield, ActorId = caster.Id,
                    TargetId = caster.Id, Amount = 10 });
                var graphics = fixture.Panel.GetComponentsInChildren<SkillEffectGraphic>();
                Assert.That(graphics.Any(g => g.Kind == SkillVisualKind.Heal), Is.True);
                Assert.That(graphics.Any(g => g.Kind == SkillVisualKind.Shield), Is.True);
                Assert.That(fixture.Panel.GetComponentsInChildren<Transform>().Any(t => t.name.StartsWith("ActionTrail-")), Is.False);
            }
        }

        [Test]
        public void PerformerNamesAndDiceSummaryHaveRoomToRenderAboveTheirBars()
        {
            using (var fixture = new Fixture(false))
            {
                foreach (BattleUnit actor in fixture.Battle.Units.Where(unit => unit.Side == BattleSide.Player))
                {
                    Text name = Find(fixture.Panel.transform, CellName(actor)).Find("UnitName").GetComponent<Text>();
                    Assert.That(name.text, Is.EqualTo(actor.Definition.Name));
                    Assert.That(name.rectTransform.rect.height, Is.GreaterThanOrEqualTo(name.fontSize * 1.6f));
                }
                Text hand = Find(fixture.Panel.transform, "DiceHandSummary").GetComponent<Text>();
                Assert.That(hand.text, Does.Not.Contain("\n"), "充能条上方只放单行骰型，剩余次数已在按钮显示。");
                Assert.That(-hand.rectTransform.anchoredPosition.y + hand.rectTransform.rect.height, Is.LessThan(37));
            }
        }

        [Test]
        public void EveryPerformerAnimatesItsOwnBasicAndBothSkillsWithoutChangingHp()
        {
            using (var fixture = new Fixture(false))
            {
                var originalHp = fixture.Battle.Units.ToDictionary(unit => unit.Id, unit => unit.Hp);
                BattleUnit enemy = fixture.Battle.Units.First(unit => unit.Side == BattleSide.Enemy && unit.Alive);
                int timestamp = 1000;
                foreach (BattleUnit actor in fixture.Battle.Units.Where(unit => unit.Side == BattleSide.Player))
                {
                    Transform cell = Find(fixture.Panel.transform, CellName(actor));
                    var motion = cell.Find("PerformerMotion").GetComponent<EnemyUnitPresentation>();
                    Assert.That(motion, Is.Not.Null, actor.Definition.Name + " 必须有自己的施法动作组件。");
                    var skills = new[] { "rt-basic", fixture.Battle.ActiveSkillId(actor, false),
                        fixture.Battle.ActiveSkillId(actor, true) };
                    foreach (string skill in skills)
                    {
                        bool mermaid = fixture.Battle.RaceOf(actor) == CombatRace.Mermaid;
                        Present(fixture.Panel, new BattleEvent
                        {
                            Kind = BattleEventKind.ActionStarted, ActorId = actor.Id,
                            TargetId = mermaid && skill != "rt-basic" ? actor.Id : enemy.Id,
                            SkillId = skill, TimeMilliseconds = timestamp++,
                        });
                    }
                    Assert.That(motion.ActionCount, Is.EqualTo(3),
                        actor.Definition.Name + " 的普攻、小技能、大技能不能都显示成首位队员行动。");
                    Text label = cell.Find("PerformerAction").GetComponent<Text>();
                    Assert.That(label.gameObject.activeInHierarchy, Is.True);
                    // 大招带「大招 · 」前缀（0.3.5 上线形态），回归点是必须指向该成员自己的大招名。
                    Assert.That(label.text, Does.EndWith(fixture.Battle.LookupSkill(skills[2]).Name),
                        actor.Definition.Name + " 的大招标签没有指向自己的大招");
                }
                foreach (BattleUnit unit in fixture.Battle.Units)
                    Assert.That(unit.Hp, Is.EqualTo(originalHp[unit.Id]),
                        "ActionStarted 是表现事件，不能重复扣血或为了播放治疗而修改满血角色。");
            }
        }

        [Test]
        public void RepeatedAoeActionEventDoesNotRestartCasterForEachTarget()
        {
            using (var fixture = new Fixture(false))
            {
                BattleUnit actor = fixture.Battle.Units.First(unit => unit.Side == BattleSide.Player);
                var motion = Find(fixture.Panel.transform, CellName(actor)).Find("PerformerMotion")
                    .GetComponent<EnemyUnitPresentation>();
                foreach (BattleUnit enemy in fixture.Battle.Units.Where(unit => unit.Side == BattleSide.Enemy && unit.Alive))
                    Present(fixture.Panel, new BattleEvent
                    {
                        Kind = BattleEventKind.ActionStarted, ActorId = actor.Id, TargetId = enemy.Id,
                        SkillId = fixture.Battle.ActiveSkillId(actor, true), TimeMilliseconds = 4000,
                    });
                Assert.That(motion.ActionCount, Is.EqualTo(1), "同一次群攻不能按命中目标数重复起手。");
                Present(fixture.Panel, new BattleEvent
                {
                    Kind = BattleEventKind.ActionStarted, ActorId = actor.Id,
                    SkillId = "rt-basic", TimeMilliseconds = 4000,
                });
                Assert.That(motion.ActionCount, Is.EqualTo(2), "同一tick的普攻与大招是不同动作，不能误去重。");
            }
        }

        [Test]
        public void EnemyHealthCardsAndArtShareTheCorrectUnitAnchorAndReserveRemainsHidden()
        {
            using (var fixture = new Fixture(false))
            {
                foreach (BattleUnit enemy in fixture.Battle.Units.Where(unit => unit.Side == BattleSide.Enemy))
                {
                    Transform anchor = Find(fixture.Panel.transform, "EnemyAnchor-" + enemy.Id);
                    Transform cell = Find(fixture.Panel.transform, CellName(enemy));
                    Transform motion = Find(fixture.Panel.transform, "EnemyMotion-" + enemy.Id);
                    Transform figure = Find(fixture.Panel.transform, "EnemyFigure-" + enemy.Id);
                    Assert.That(motion.parent, Is.SameAs(anchor));
                    Assert.That(figure.parent, Is.SameAs(motion));
                    Assert.That(cell.IsChildOf(motion), Is.False, "敌人受击位移不能让血条一起乱抖。");
                    Assert.That(anchor.gameObject.activeInHierarchy, Is.EqualTo(enemy.Spawned));
                    Assert.That(cell.gameObject.activeInHierarchy, Is.EqualTo(enemy.Spawned));
                    if (enemy.Spawned)
                    {
                        Assert.That(cell.parent, Is.SameAs(anchor), "血条应绑定该敌人的单位锚点，不留在左右边栏。");
                        var cardRect = cell.GetComponent<RectTransform>();
                        var motionRect = motion.GetComponent<RectTransform>();
                        Assert.That(cardRect.position.y, Is.GreaterThan(motionRect.position.y),
                            "敌人血条应位于对应立绘上方。");
                        Assert.That(cell.Find("UnitName").GetComponent<Text>().text, Is.EqualTo(enemy.Definition.Name));
                    }
                }
                BattleUnit reserve = fixture.Battle.Units.First(unit => unit.Side == BattleSide.Enemy && !unit.Spawned);
                reserve.Spawned = true;
                Invoke(fixture.Panel, "RefreshAllCells");
                Assert.That(Find(fixture.Panel.transform, CellName(reserve)).parent,
                    Is.SameAs(Find(fixture.Panel.transform, "EnemyAnchor-" + reserve.Id)),
                    "后续波出生时，其血条也必须绑定自己的画像锚点。");
            }
        }

        [Test]
        public void EnemyDefeatImmediatelyRemovesHpCardThenHidesFinishedFadeAnchor()
        {
            using (var fixture = new Fixture(false))
            {
                BattleUnit enemy = fixture.Battle.Units.First(unit => unit.Side == BattleSide.Enemy && unit.Alive);
                BattleUnit player = fixture.Battle.Units.First(unit => unit.Side == BattleSide.Player);
                Transform anchor = Find(fixture.Panel.transform, "EnemyAnchor-" + enemy.Id);
                Transform cell = Find(fixture.Panel.transform, CellName(enemy));
                var motion = Find(fixture.Panel.transform, "EnemyMotion-" + enemy.Id)
                    .GetComponent<EnemyUnitPresentation>();
                enemy.Hp = 0;
                Present(fixture.Panel, new BattleEvent
                {
                    Kind = BattleEventKind.Defeated, ActorId = player.Id, TargetId = enemy.Id,
                    SkillId = "rt-basic", TimeMilliseconds = 1000,
                });
                Assert.That(cell.gameObject.activeInHierarchy, Is.False, "死亡血条必须立即消失。");
                Assert.That(anchor.gameObject.activeInHierarchy, Is.True, "画像保留短暂退场，不应直接消失。");
                Assert.That(motion.IsDefeating, Is.True);
                Assert.That(fixture.Battle.FocusEnemy(enemy.Id), Is.False, "死敌不能继续被集火选择。");
                motion.AdvancePresentation(0.5f);
                Invoke(fixture.Panel, "RefreshAllCells");
                Assert.That(anchor.gameObject.activeInHierarchy, Is.False, "退场完成不能残留空画像/旧血条。");
            }
        }

        [Test]
        public void DamagingBossAddAnimatesThatAddNotTheBoss()
        {
            using (var fixture = new Fixture(true))
            {
                BattleUnit add = fixture.Battle.Units.First(unit => unit.Side == BattleSide.Enemy && unit.Definition.Id == "echo-drone");
                BattleUnit queen = fixture.Battle.Units.First(unit => unit.Definition.Id == "siren-queen");
                BattleUnit player = fixture.Battle.Units.First(unit => unit.Side == BattleSide.Player);
                var boss = fixture.Panel.GetComponentInChildren<BossBattlePresentation>(true);
                var addMotion = Find(fixture.Panel.transform, "EnemyMotion-" + add.Id)
                    .GetComponent<EnemyUnitPresentation>();
                Assert.That(boss, Is.Not.Null);
                int before = boss.HitReactionCount;
                Present(fixture.Panel, new BattleEvent
                {
                    Kind = BattleEventKind.Damage, ActorId = player.Id, TargetId = add.Id,
                    SkillId = "rt-basic", Amount = 5,
                });
                Assert.That(addMotion.HitReactionCount, Is.EqualTo(1));
                Assert.That(boss.HitReactionCount, Is.EqualTo(before), "打小怪不能在Boss身上摇晃并重复飘字。");
                Present(fixture.Panel, new BattleEvent
                {
                    Kind = BattleEventKind.Damage, ActorId = player.Id, TargetId = queen.Id,
                    SkillId = "rt-basic", Amount = 6,
                });
                Assert.That(boss.HitReactionCount, Is.EqualTo(before + 1));
            }
        }

        [Test]
        public void InspectingAnotherPerformerDoesNotChangeBattleCommander()
        {
            using (var fixture = new Fixture(false))
            {
                int captain = fixture.Battle.CurrentLeaderId;
                BattleUnit selected = fixture.Battle.Units.First(unit => unit.Side == BattleSide.Player && unit.Id != captain);
                Find(fixture.Panel.transform, CellName(selected)).GetComponent<Button>().onClick.Invoke();
                var inspected = (BattleUnit)typeof(TacticsBattlePanel).GetField("inputActor",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(fixture.Panel);
                Assert.That(inspected.Id, Is.EqualTo(selected.Id), "点击队员应允许查看其双技能。");
                Assert.That(fixture.Battle.CurrentLeaderId, Is.EqualTo(captain), "查看角色不能悄悄替换队长和种族指挥效果。");
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void HealthSummariesUseCurrentWaveOrBossAndSeparatelyReportTheWholeTeam(bool bossEncounter)
        {
            using (var fixture = new Fixture(bossEncounter))
            {
                BattleUnit player = fixture.Battle.Units.First(unit => unit.Side == BattleSide.Player);
                player.Hp /= 2;
                BattleUnit boss = fixture.Battle.Units.FirstOrDefault(unit => unit.Definition.Id == "siren-queen");
                if (boss != null) boss.Hp /= 4;
                Invoke(fixture.Panel, "RefreshBattleHud");
                Text enemyText = (Text)typeof(TacticsBattlePanel).GetField("enemyHpText",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(fixture.Panel);
                var displayedEnemies = fixture.Battle.Units.Where(unit => unit.Side == BattleSide.Enemy &&
                    (boss != null ? unit.Id == boss.Id : unit.Spawned && unit.Wave == fixture.Battle.CurrentWave - 1));
                long enemyHp = displayedEnemies.Sum(unit => (long)unit.Hp);
                long enemyMax = displayedEnemies.Sum(unit => (long)unit.MaxHp);
                Assert.That(enemyText.text, Does.Contain($"{enemyHp:N0}/{enemyMax:N0}"),
                    "总条不能把未出场增援算进当前波，也不能用小怪生命冒充Boss生命。");
                var players = fixture.Battle.Units.Where(unit => unit.Side == BattleSide.Player);
                long teamHp = players.Sum(unit => (long)unit.Hp);
                long teamMax = players.Sum(unit => (long)unit.MaxHp);
                Text summary = Find(fixture.Panel.transform, "TeamHealthSummary").GetComponent<Text>();
                Assert.That(summary.gameObject.activeInHierarchy, Is.True);
                Assert.That(summary.text, Does.Contain($"{teamHp:N0}/{teamMax:N0}"));
                Assert.That(summary.text, Does.Contain("存活 4/4"));
            }
        }

        [TestCase("training")]
        [TestCase("accessory")]
        [TestCase("team")]
        [TestCase("farm")]
        public void DefeatRecoveryRoutesCloseBattleWithoutSettlingOrChargingAgain(string route)
        {
            using (var fixture = new RecoveryFixture(false))
            {
                Transform actions = Find(fixture.Panel.transform, "RecoveryActions");
                Assert.That(actions.gameObject.activeInHierarchy, Is.True);
                Button button = Find(actions, "Recovery-" + route).GetComponent<Button>();
                Assert.That(button.interactable, Is.True);
                Assert.That(fixture.SettlementCalls, Is.EqualTo(1));
                int gold = fixture.Model.Save.Gold, diamonds = fixture.Model.Save.Diamonds;
                int stamina = fixture.Model.Save.Stamina;
                button.onClick.Invoke();
                // A second queued click must also be idempotent after the panel closes.
                button.onClick.Invoke();
                Assert.That(fixture.GrowthRoute, Is.EqualTo(route));
                Assert.That(fixture.GrowthCalls, Is.EqualTo(1));
                Assert.That(fixture.Panel.gameObject.activeSelf, Is.False);
                Assert.That(fixture.SettlementCalls, Is.EqualTo(1));
                Assert.That(fixture.Model.Save.Gold, Is.EqualTo(gold));
                Assert.That(fixture.Model.Save.Diamonds, Is.EqualTo(diamonds));
                Assert.That(fixture.Model.Save.Stamina, Is.EqualTo(stamina),
                    "导航去训练/装备/编队/刷关只是打开界面，不能再次收费或自动开始关卡。");
                Assert.That(fixture.BackCalls, Is.Zero, "恢复导航不能同时触发回地图并抢走目标页面。");
            }
        }

        [Test]
        public void VictoryHidesRecoveryActionsAndContinueStillClosesOnce()
        {
            using (var fixture = new RecoveryFixture(true))
            {
                Assert.That(fixture.Battle.Outcome, Is.EqualTo(BattleOutcome.Victory));
                Assert.That(Find(fixture.Panel.transform, "RecoveryActions").gameObject.activeInHierarchy, Is.False);
                Button button = Find(fixture.Panel.transform, "ResultContinue").GetComponent<Button>();
                Assert.That(button.gameObject.activeInHierarchy && button.interactable, Is.True);
                int gold = fixture.Model.Save.Gold, diamonds = fixture.Model.Save.Diamonds;
                button.onClick.Invoke();
                Assert.That(fixture.Panel.gameObject.activeSelf, Is.False);
                Assert.That(fixture.SettlementCalls, Is.EqualTo(1));
                Assert.That(fixture.BackCalls, Is.EqualTo(1));
                Assert.That(fixture.GrowthCalls, Is.Zero);
                Assert.That(fixture.Model.Save.Gold, Is.EqualTo(gold));
                Assert.That(fixture.Model.Save.Diamonds, Is.EqualTo(diamonds), "继续不能重复发放胜利奖励。");
            }
        }

        [Test]
        public void DefeatContinueRemainsAvailableWithoutForcingGrowthNavigation()
        {
            using (var fixture = new RecoveryFixture(false))
            {
                Button button = Find(fixture.Panel.transform, "ResultContinue").GetComponent<Button>();
                Assert.That(button.gameObject.activeInHierarchy && button.interactable, Is.True);
                button.onClick.Invoke();
                Assert.That(fixture.Panel.gameObject.activeSelf, Is.False);
                Assert.That(fixture.BackCalls, Is.EqualTo(1));
                Assert.That(fixture.GrowthCalls, Is.Zero);
                Assert.That(fixture.SettlementCalls, Is.EqualTo(1));
            }
        }

        private sealed class RecoveryFixture : IDisposable
        {
            private readonly GameObject root;
            public readonly GameModel Model;
            public readonly BattleSimulator Battle;
            public readonly TacticsBattlePanel Panel;
            public int SettlementCalls, GrowthCalls, BackCalls;
            public string GrowthRoute;

            public RecoveryFixture(bool victory)
            {
                // The enclosing fixture restores both save keys after each test.
                PlayerPrefs.DeleteKey(GameModel.SaveKey);
                PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
                Model = new GameModel();
                Battle = Model.StartStageBattle("stage-1-1", 713UL, out string message);
                Assert.That(Battle, Is.Not.Null, message);
                root = new GameObject("Battle recovery route fixture", typeof(RectTransform));
                Panel = TacticsBattlePanel.Open(root.transform, Model, Battle,
                    finished: battle => { SettlementCalls++; Model.SettleStageBattle(battle, out _); },
                    back: () => BackCalls++, growth: route => { GrowthCalls++; GrowthRoute = route; });
                Panel.StopAllCoroutines();
                if (victory)
                {
                    foreach (BattleUnit unit in Battle.Units)
                        if (unit.Side == BattleSide.Enemy) unit.Hp = 1;
                        else unit.BaseAttack = 10000;
                    Battle.AdvanceRealtime(Battle.TimeLimitMilliseconds, true);
                    Assert.That(Battle.Outcome, Is.EqualTo(BattleOutcome.Victory));
                }
                else Battle.AutoPlay(0);
                Invoke(Panel, "ShowResult");
            }

            public void Dispose() { UnityEngine.Object.DestroyImmediate(root); }
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject root;
            public readonly BattleSimulator Battle;
            public readonly TacticsBattlePanel Panel;

            public Fixture(bool boss)
            {
                root = new GameObject("Battle unit feedback fixture", typeof(RectTransform));
                var repository = new GameDataRepository(new ResourcesGameDataSource(), new UnityJsonReader());
                Assert.That(repository.LoadAll(), Is.True, string.Join("\n", repository.Errors));
                var stage = new StageDefinition
                {
                    Id = "unit-feedback-test", Name = "表现回归", EncounterType = boss ? "boss" : "normal",
                    Enemies = new List<EnemySpawn>
                    {
                        new EnemySpawn { UnitId = boss ? "siren-queen" : "noise-wraith", Row = 0, Col = 0 },
                        new EnemySpawn { UnitId = "echo-drone", Row = 1, Col = 0 },
                        new EnemySpawn { UnitId = "static-golem", Row = 0, Col = 1, Wave = 1 },
                    },
                };
                Battle = new BattleSimulator(repository.Tactics, stage, new List<PlayerUnitSetup>
                {
                    new PlayerUnitSetup { UnitId = "xingli", Row = 0, Col = 0, Level = 1 },
                    new PlayerUnitSetup { UnitId = "feiyin", Row = 1, Col = 0, Level = 1 },
                    new PlayerUnitSetup { UnitId = "wubai", Row = 2, Col = 0, Level = 1 },
                    new PlayerUnitSetup { UnitId = "yeying", Row = 0, Col = 1, Level = 1 },
                }, new SeededRandom(412));
                Panel = TacticsBattlePanel.Open(root.transform, new GameModel(), Battle, _ => { });
                Panel.StopAllCoroutines();
            }

            public void Dispose() { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static string CellName(BattleUnit unit) => $"Cell-{(unit.Side == BattleSide.Player ? "P" : "E")}-{unit.Row}-{unit.Col}";

        private static Transform Find(Transform root, string name)
        {
            Transform found = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(item => item.name == name);
            Assert.That(found, Is.Not.Null, "缺少验收节点：" + name);
            return found;
        }

        private static void Present(TacticsBattlePanel panel, BattleEvent entry) =>
            typeof(TacticsBattlePanel).GetMethod("PresentEvent", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(panel, new object[] { entry, -1 });

        private static void Invoke(TacticsBattlePanel panel, string method) =>
            typeof(TacticsBattlePanel).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(panel, null);
    }
}
