using System;
using System.Collections.Generic;
using System.Linq;
using ChoSiren.Systems;
using ChoSiren.Systems.Tactics;
using NUnit.Framework;

namespace ChoSiren.Tests.Systems
{
    public sealed class BattleSimulatorTests
    {
        private static TacticsManifest Manifest()
        {
            var manifest = new TacticsManifest();
            manifest.Skills.Add(new SkillDefinition { Id = "strike", Name = "普攻", Effect = SkillEffect.Damage, Pattern = SkillPattern.Single, PowerPermille = 1000, Cooldown = 0, CanCrit = false });
            manifest.Skills.Add(new SkillDefinition { Id = "sweep", Name = "横扫", Effect = SkillEffect.Damage, Pattern = SkillPattern.Row, PowerPermille = 800, Cooldown = 2, CanCrit = false });
            manifest.Skills.Add(new SkillDefinition { Id = "mend", Name = "治疗", Effect = SkillEffect.Heal, Pattern = SkillPattern.Single, PowerPermille = 1000, Cooldown = 1, CanCrit = false });
            manifest.Skills.Add(new SkillDefinition { Id = "guard", Name = "护盾", Effect = SkillEffect.Shield, Pattern = SkillPattern.Single, PowerPermille = 500, Duration = 2, Cooldown = 3, CanCrit = false });
            manifest.Skills.Add(new SkillDefinition { Id = "expose", Name = "破防", Effect = SkillEffect.DebuffDefense, Pattern = SkillPattern.Single, PowerPermille = 1000, Duration = 1, Cooldown = 2, CanCrit = false });

            manifest.Units.Add(new UnitDefinition { Id = "singer", Name = "主唱", MaxHp = 1000, Attack = 200, Defense = 50, Speed = 110, CritPermille = 0, SkillIds = new List<string> { "strike", "sweep", "expose" } });
            manifest.Units.Add(new UnitDefinition { Id = "healer", Name = "支援", MaxHp = 1200, Attack = 100, Defense = 50, Speed = 90, CritPermille = 0, SkillIds = new List<string> { "strike", "mend", "guard" } });
            manifest.Units.Add(new UnitDefinition { Id = "drone", Name = "无人机", MaxHp = 300, Attack = 60, Defense = 0, Speed = 100, CritPermille = 0, SkillIds = new List<string> { "strike" } });
            manifest.Units.Add(new UnitDefinition { Id = "golem", Name = "魔像", MaxHp = 3000, Attack = 120, Defense = 250, Speed = 40, CritPermille = 0, SkillIds = new List<string> { "strike" } });

            manifest.Stages.Add(new StageDefinition
            {
                Id = "test-stage",
                Name = "测试关",
                TurnLimit = 10,
                ThreeStarRounds = 3,
                Enemies = new List<EnemySpawn>
                {
                    new EnemySpawn { UnitId = "drone", Row = 0, Col = 0 },
                    new EnemySpawn { UnitId = "drone", Row = 0, Col = 2 },
                    new EnemySpawn { UnitId = "golem", Row = 2, Col = 1 },
                },
                Drops = new DropTable
                {
                    Rolls = 2,
                    Entries = new List<DropEntry>
                    {
                        new DropEntry { ItemId = "gold", Weight = 3, Min = 10, Max = 20 },
                        new DropEntry { ItemId = "shard", Weight = 1, Min = 1, Max = 1 },
                    }
                }
            });
            Assert.That(manifest.TryValidate(out string error), Is.True, error);
            return manifest;
        }

        private static List<PlayerUnitSetup> Party() => new List<PlayerUnitSetup>
        {
            new PlayerUnitSetup { UnitId = "singer", Row = 1, Col = 0, Level = 1 },
            new PlayerUnitSetup { UnitId = "healer", Row = 1, Col = 2, Level = 1 },
        };

        private static BattleSimulator NewBattle(ulong seed = 7) =>
            new BattleSimulator(Manifest(), Manifest().FindStage("test-stage"), Party(), new SeededRandom(seed));

        [Test]
        public void TurnOrderFollowsSpeedWithPlayersWinningTies()
        {
            BattleSimulator battle = NewBattle();

            Assert.That(battle.Round, Is.EqualTo(1));
            Assert.That(battle.CurrentActor.Definition.Id, Is.EqualTo("singer"), "速度 110 最先行动");

            // Drone speed 100 > healer 90 > golem 40.
            BattleUnit singer = battle.CurrentActor;
            Assert.That(battle.TryAct(new BattleAction { ActorId = singer.Id, SkillId = "strike", Row = 0, Col = 0 }, out string error), Is.True, error);
            Assert.That(battle.CurrentActor.Definition.Id, Is.EqualTo("drone"));
        }

        [Test]
        public void DamageFormulaIsIntegerAndMitigatedByDefense()
        {
            BattleSimulator battle = NewBattle();
            BattleUnit singer = battle.CurrentActor;
            BattleUnit drone = battle.UnitAt(BattleSide.Enemy, 0, 0);
            BattleUnit golem = battle.UnitAt(BattleSide.Enemy, 2, 1);
            SkillDefinition strike = battle.LookupSkill("strike");

            Assert.That(battle.PreviewDamage(singer, strike, drone), Is.EqualTo(200), "防御 0 时全额");
            // 200 * 1000 / (1000 + 250*4) = 100
            Assert.That(battle.PreviewDamage(singer, strike, golem), Is.EqualTo(100));
            Assert.That(battle.PreviewDamage(singer, strike, golem, critical: true), Is.EqualTo(150));
        }

        [Test]
        public void EnemyHpScaleIsIndependentAndZeroFallsBackToLegacyScale()
        {
            TacticsManifest manifest = Manifest();
            StageDefinition stage = manifest.FindStage("test-stage");
            stage.Enemies = new List<EnemySpawn>
            {
                new EnemySpawn
                {
                    UnitId = "drone", Row = 0, Col = 0,
                    ScalePermille = 2000, HpScalePermille = 3000
                }
            };

            var independent = new BattleSimulator(manifest, stage, Party(), new ScriptedRandom(new[] { 999 }));
            BattleUnit scaledEnemy = independent.UnitAt(BattleSide.Enemy, 0, 0);
            Assert.That(scaledEnemy.MaxHp, Is.EqualTo(900), "HP 应只使用独立的 3000‰");
            Assert.That(scaledEnemy.BaseAttack, Is.EqualTo(120), "攻击仍使用通用的 2000‰");
            Assert.That(scaledEnemy.BaseDefense, Is.Zero);

            stage.Enemies[0].HpScalePermille = 0;
            var compatible = new BattleSimulator(manifest, stage, Party(), new ScriptedRandom(new[] { 999 }));
            Assert.That(compatible.UnitAt(BattleSide.Enemy, 0, 0).MaxHp, Is.EqualTo(600),
                "旧 JSON 未提供独立 HP 时必须回退到 ScalePermille");
        }

        [Test]
        public void DifficultyScalesEnemyHpAndAttackWithoutChangingDefenseOrLegacyCallers()
        {
            TacticsManifest manifest = Manifest();
            StageDefinition stage = manifest.FindStage("test-stage");
            stage.Enemies = new List<EnemySpawn>
            {
                new EnemySpawn
                {
                    UnitId = "golem", Row = 1, Col = 1,
                    ScalePermille = 2000, HpScalePermille = 3000
                }
            };

            var normal = new BattleSimulator(manifest, stage, Party(), new ScriptedRandom(new[] { 999 }));
            BattleUnit normalEnemy = normal.UnitAt(BattleSide.Enemy, 1, 1);
            Assert.That(normalEnemy.MaxHp, Is.EqualTo(9000));
            Assert.That(normalEnemy.BaseAttack, Is.EqualTo(240));
            Assert.That(normalEnemy.BaseDefense, Is.EqualTo(500));
            Assert.That(normal.EnemyHpMultiplierPermille, Is.EqualTo(1000));
            Assert.That(normal.EnemyAttackMultiplierPermille, Is.EqualTo(1000));

            var hard = new BattleSimulator(manifest, stage, Party(), new ScriptedRandom(new[] { 999 }),
                1300, 1200);
            BattleUnit hardEnemy = hard.UnitAt(BattleSide.Enemy, 1, 1);
            Assert.That(hardEnemy.MaxHp, Is.EqualTo(11700));
            Assert.That(hardEnemy.BaseAttack, Is.EqualTo(288));
            Assert.That(hardEnemy.BaseDefense, Is.EqualTo(500), "难度不应悄悄改变敌方防御与玩家伤害公式。");
            Assert.That(hard.EnemyHpMultiplierPermille, Is.EqualTo(1300));
            Assert.That(hard.EnemyAttackMultiplierPermille, Is.EqualTo(1200));

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new BattleSimulator(manifest, stage, Party(), new ScriptedRandom(new[] { 999 }), 0, 1000));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new BattleSimulator(manifest, stage, Party(), new ScriptedRandom(new[] { 999 }), 1000, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new BattleSimulator(manifest, stage, Party(), new ScriptedRandom(new[] { 999 }),
                    BattleSimulator.MaxEnemyDifficultyMultiplierPermille + 1, 1000));
        }

        [Test]
        public void EnemyTotalHpAdvancesThroughOrderedThreePhaseEventsAndRemainsWinnable()
        {
            TacticsManifest manifest = Manifest();
            UnitDefinition drone = manifest.FindUnit("drone");
            drone.MaxHp = 3000;
            StageDefinition stage = manifest.FindStage("test-stage");
            stage.TurnLimit = 10;
            stage.Enemies = new List<EnemySpawn>
            {
                new EnemySpawn { UnitId = "drone", Row = 0, Col = 0, ScalePermille = 1000 }
            };
            var party = new List<PlayerUnitSetup>
            {
                new PlayerUnitSetup { UnitId = "singer", Row = 1, Col = 0, Level = 1 }
            };
            var battle = new BattleSimulator(manifest, stage, party, new ScriptedRandom(new[] { 999 }));
            Assert.That(battle.EnemyPhase, Is.EqualTo(1));
            Assert.That(battle.InitialEnemyHp, Is.EqualTo(3000));

            ActCurrentPlayerStrike(battle, 5500);
            Assert.That(battle.EnemyPhase, Is.EqualTo(2));
            Assert.That(battle.UnitAt(BattleSide.Enemy, 0, 0).Attack, Is.EqualTo(64),
                "第二阶段敌方攻击只小幅提升 8%");

            AdvanceEnemyTurns(battle);
            ActCurrentPlayerStrike(battle, 5500);
            Assert.That(battle.EnemyPhase, Is.EqualTo(3));
            Assert.That(battle.UnitAt(BattleSide.Enemy, 0, 0).Attack, Is.EqualTo(69),
                "第三阶段敌方攻击总增幅应为 16%");

            int[] phases = battle.Log.Where(item => item.Kind == BattleEventKind.PhaseChanged)
                .Select(item => item.Phase).ToArray();
            Assert.That(phases, Is.EqualTo(new[] { 2, 3 }));

            AdvanceEnemyTurns(battle);
            ActCurrentPlayerStrike(battle, 5500);
            Assert.That(battle.Outcome, Is.EqualTo(BattleOutcome.Victory));
        }

        [Test]
        public void FiveKindBurstEmitsBothPhaseBoundariesInsteadOfJumpingDirectlyToThree()
        {
            TacticsManifest manifest = Manifest();
            StageDefinition stage = manifest.FindStage("test-stage");
            stage.Enemies = new List<EnemySpawn>
            {
                new EnemySpawn { UnitId = "drone", Row = 0, Col = 0, ScalePermille = 1000 }
            };
            var party = new List<PlayerUnitSetup>
            {
                new PlayerUnitSetup { UnitId = "singer", Row = 1, Col = 0, Level = 1 }
            };
            var battle = new BattleSimulator(manifest, stage, party, new ScriptedRandom(new[] { 999 }));

            ActCurrentPlayerStrike(battle, 10000);

            Assert.That(battle.Outcome, Is.EqualTo(BattleOutcome.Victory));
            Assert.That(battle.EnemyPhase, Is.EqualTo(3));
            BattleEvent[] phaseEvents = battle.Log
                .Where(item => item.Kind == BattleEventKind.PhaseChanged).ToArray();
            Assert.That(phaseEvents.Length, Is.EqualTo(2));
            Assert.That(phaseEvents[0].Phase, Is.EqualTo(2));
            Assert.That(phaseEvents[1].Phase, Is.EqualTo(3));
            Assert.That(battle.Log[battle.Log.Count - 1].Kind, Is.EqualTo(BattleEventKind.Finished));
        }

        [Test]
        public void DiceMultiplierChangesTheDamageActuallyApplied()
        {
            BattleSimulator battle = NewBattle();
            BattleUnit singer = battle.CurrentActor;
            BattleUnit golem = battle.UnitAt(BattleSide.Enemy, 2, 1);
            int hpBefore = golem.Hp;

            Assert.That(battle.TryAct(new BattleAction
            {
                ActorId = singer.Id,
                SkillId = "strike",
                Row = 2,
                Col = 1,
                PowerMultiplierPermille = 2500
            }, out string error), Is.True, error);

            // Base result is 100 after defense; the three-of-a-kind multiplier makes it 250.
            Assert.That(hpBefore - golem.Hp, Is.EqualTo(250));
            Assert.That(battle.Log[battle.Log.Count - 1].Amount, Is.EqualTo(250));
        }

        [Test]
        public void RowPatternHitsEveryEnemyOnThatRowAndClipsEmptyCells()
        {
            BattleSimulator battle = NewBattle();
            BattleUnit singer = battle.CurrentActor;
            SkillDefinition sweep = battle.LookupSkill("sweep");

            List<BattleUnit> hit = battle.AffectedUnits(singer, sweep, 0, 1);
            Assert.That(hit.Count, Is.EqualTo(2), "第 0 行有两台无人机，中间格为空");

            List<(int Row, int Col)> anchors = battle.LegalAnchors(singer, "sweep");
            Assert.That(anchors, Is.EquivalentTo(new[] { (0, 0), (0, 2), (2, 1) }), "锚点必须落在有单位的格上");

            Assert.That(BattleGrid.Cells(SkillPattern.Plus, 0, 0).Count, Is.EqualTo(3), "角落十字只有 3 格");
            Assert.That(BattleGrid.Cells(SkillPattern.All, 1, 1).Count, Is.EqualTo(9));
        }

        [Test]
        public void CooldownBlocksReuseUntilRoundsPass()
        {
            BattleSimulator battle = NewBattle();
            BattleUnit singer = battle.CurrentActor;

            Assert.That(battle.TryAct(new BattleAction { ActorId = singer.Id, SkillId = "sweep", Row = 0, Col = 0 }, out string error), Is.True, error);
            Assert.That(battle.IsSkillReady(singer, "sweep"), Is.False);
            Assert.That(battle.IsSkillReady(singer, "strike"), Is.True);

            // Finish the round: drone, drone, healer, golem.
            for (int index = 0; index < 4 && battle.Round == 1; index++)
                battle.TryAct(EnemyAi.Choose(battle, battle.CurrentActor), out _);

            Assert.That(battle.Round, Is.EqualTo(2));
            Assert.That(battle.IsSkillReady(singer, "sweep"), Is.False, "冷却 2 回合，第 2 回合仍不可用");
        }

        [Test]
        public void ShieldAbsorbsBeforeHpAndDebuffLowersDefense()
        {
            BattleSimulator battle = NewBattle();
            BattleUnit singer = battle.CurrentActor;
            BattleUnit golem = battle.UnitAt(BattleSide.Enemy, 2, 1);

            Assert.That(battle.TryAct(new BattleAction { ActorId = singer.Id, SkillId = "expose", Row = 2, Col = 1 }, out string error), Is.True, error);
            Assert.That(golem.Defense, Is.Zero, "1000‰ 破防后防御归零");
            Assert.That(battle.PreviewDamage(singer, battle.LookupSkill("strike"), golem), Is.EqualTo(200));

            // Let the two drones act, then the healer shields the singer.
            battle.TryAct(EnemyAi.Choose(battle, battle.CurrentActor), out _);
            battle.TryAct(EnemyAi.Choose(battle, battle.CurrentActor), out _);
            BattleUnit healer = battle.CurrentActor;
            Assert.That(healer.Definition.Id, Is.EqualTo("healer"));
            int singerHpBefore = singer.Hp;
            Assert.That(battle.TryAct(new BattleAction { ActorId = healer.Id, SkillId = "guard", Row = 1, Col = 0 }, out error), Is.True, error);
            Assert.That(singer.Shield, Is.EqualTo(500));

            // Golem (attack 120 vs defense 50 -> 100) hits the shielded singer.
            BattleUnit golemActor = battle.CurrentActor;
            Assert.That(golemActor.Definition.Id, Is.EqualTo("golem"));
            Assert.That(battle.TryAct(new BattleAction { ActorId = golemActor.Id, SkillId = "strike", Row = 1, Col = 0 }, out error), Is.True, error);
            Assert.That(singer.Hp, Is.EqualTo(singerHpBefore), "护盾应吃下全部伤害");
            Assert.That(singer.Shield, Is.EqualTo(400));
        }

        [Test]
        public void IllegalActionsAreRefusedWithReasons()
        {
            BattleSimulator battle = NewBattle();
            BattleUnit singer = battle.CurrentActor;
            BattleUnit healer = battle.UnitAt(BattleSide.Player, 1, 2);

            Assert.That(battle.TryAct(new BattleAction { ActorId = healer.Id, SkillId = "strike", Row = 0, Col = 0 }, out string error), Is.False);
            Assert.That(error, Does.Contain("还没轮到"));
            Assert.That(battle.TryAct(new BattleAction { ActorId = singer.Id, SkillId = "mend", Row = 1, Col = 2 }, out error), Is.False);
            Assert.That(error, Does.Contain("技能不可用"));
            Assert.That(battle.TryAct(new BattleAction { ActorId = singer.Id, SkillId = "strike", Row = 1, Col = 1 }, out error), Is.False);
            Assert.That(error, Does.Contain("没有可作用的单位"));
        }

        [Test]
        public void AutoPlayIsDeterministicAndEndsWithARating()
        {
            BattleSimulator a = NewBattle(99);
            BattleSimulator b = NewBattle(99);

            BattleOutcome outcomeA = a.AutoPlay();
            BattleOutcome outcomeB = b.AutoPlay();

            Assert.That(outcomeA, Is.Not.EqualTo(BattleOutcome.Ongoing));
            Assert.That(outcomeA, Is.EqualTo(outcomeB));
            Assert.That(a.Round, Is.EqualTo(b.Round));
            Assert.That(a.Log.Count, Is.EqualTo(b.Log.Count));
            Assert.That(a.Log[a.Log.Count - 1].Kind, Is.EqualTo(BattleEventKind.Finished));
            Assert.That(a.StarRating(), Is.InRange(0, 3));
            if (outcomeA == BattleOutcome.Victory) Assert.That(a.StarRating(), Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void TurnLimitProducesDefeat()
        {
            TacticsManifest manifest = Manifest();
            StageDefinition stage = manifest.FindStage("test-stage");
            stage.TurnLimit = 1;
            var battle = new BattleSimulator(manifest, stage, Party(), new SeededRandom(3));

            // Everyone just pokes the golem so nothing dies within one round.
            while (battle.Outcome == BattleOutcome.Ongoing)
            {
                BattleUnit actor = battle.CurrentActor;
                int targetRow = actor.Side == BattleSide.Player ? 2 : 1;
                int targetCol = actor.Side == BattleSide.Player ? 1 : 0;
                Assert.That(battle.TryAct(new BattleAction { ActorId = actor.Id, SkillId = "strike", Row = targetRow, Col = targetCol }, out string error), Is.True, error);
            }

            Assert.That(battle.Outcome, Is.EqualTo(BattleOutcome.Defeat));
            Assert.That(battle.Round, Is.EqualTo(1));
            Assert.That(battle.StarRating(), Is.Zero);
        }

        [Test]
        public void EnemyAiPrefersLethalHitsAndSkipsUselessHeals()
        {
            BattleSimulator battle = NewBattle();
            BattleUnit singer = battle.CurrentActor;
            BattleAction choice = EnemyAi.Choose(battle, singer);

            Assert.That(choice.SkillId, Is.EqualTo("sweep"), "横扫可以同时击杀两台 300 血无人机");
            Assert.That(choice.Row, Is.Zero);

            battle.TryAct(choice, out _);
            battle.TryAct(EnemyAi.Choose(battle, battle.CurrentActor), out _);
            battle.TryAct(EnemyAi.Choose(battle, battle.CurrentActor), out _);
            BattleUnit healer = battle.CurrentActor;
            Assert.That(healer.Definition.Id, Is.EqualTo("healer"));
            BattleAction healerChoice = EnemyAi.Choose(battle, healer);
            Assert.That(healerChoice.SkillId, Is.Not.EqualTo("mend"), "没人重伤时不应浪费治疗");
        }

        [Test]
        public void DropTableRollsAreWeightedAndMerged()
        {
            TacticsManifest manifest = Manifest();
            DropTable table = manifest.FindStage("test-stage").Drops;
            // pick 0 -> gold (weight 3 covers 0..2); amount roll 5 -> 15; pick 3 -> shard.
            var random = new ScriptedRandom(new[] { 0 }, new[] { 0, 5, 3 });

            List<(string ItemId, int Amount)> drops = DropResolver.Roll(table, random);

            Assert.That(drops.Count, Is.EqualTo(2));
            Assert.That(drops[0], Is.EqualTo(("gold", 15)));
            Assert.That(drops[1], Is.EqualTo(("shard", 1)));
        }

        [Test]
        public void ManifestValidationCatchesMissingBasicSkillAndOverlappingEnemies()
        {
            TacticsManifest manifest = Manifest();
            manifest.Units[0].SkillIds = new List<string> { "sweep" };
            Assert.That(manifest.TryValidate(out string error), Is.False);
            Assert.That(error, Does.Contain("无冷却技能"));

            manifest = Manifest();
            manifest.Stages[0].Enemies.Add(new EnemySpawn { UnitId = "drone", Row = 0, Col = 0 });
            Assert.That(manifest.TryValidate(out error), Is.False);
            Assert.That(error, Does.Contain("同一格"));
        }

        private static void ActCurrentPlayerStrike(BattleSimulator battle, int multiplierPermille)
        {
            BattleUnit actor = battle.CurrentActor;
            Assert.That(actor, Is.Not.Null);
            Assert.That(actor.Side, Is.EqualTo(BattleSide.Player));
            BattleUnit target = battle.UnitAt(BattleSide.Enemy, 0, 0);
            Assert.That(target, Is.Not.Null);
            Assert.That(battle.TryAct(new BattleAction
            {
                ActorId = actor.Id,
                SkillId = "strike",
                Row = target.Row,
                Col = target.Col,
                PowerMultiplierPermille = multiplierPermille
            }, out string error), Is.True, error);
        }

        private static void AdvanceEnemyTurns(BattleSimulator battle)
        {
            int guard = 0;
            while (battle.Outcome == BattleOutcome.Ongoing &&
                   battle.CurrentActor.Side == BattleSide.Enemy && guard++ < 20)
            {
                BattleAction action = EnemyAi.Choose(battle, battle.CurrentActor);
                Assert.That(battle.TryAct(action, out string error), Is.True, error);
            }

            Assert.That(guard, Is.LessThan(20));
        }
    }
}
