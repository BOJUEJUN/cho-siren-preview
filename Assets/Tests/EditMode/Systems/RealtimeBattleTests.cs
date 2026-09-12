using System;
using System.Collections.Generic;
using System.Linq;
using ChoSiren.Systems;
using ChoSiren.Systems.Dice;
using ChoSiren.Systems.Tactics;
using NUnit.Framework;

namespace ChoSiren.Tests
{
    public sealed class RealtimeBattleTests
    {
        private static readonly Dictionary<string, string> Races = new Dictionary<string, string>
        {
            { "demon", "魔族 · 恶魔" }, { "charm", "魅族" },
            { "mermaid", "海灵族 · 人鱼" }, { "elf", "血精灵" }
        };

        // v0.3.3: 成长曲线已压制(原 1.065/1.085/1.05 → 现 1.042/1.052/1.032),Lv100 数值从 550× 压到 ~28×
        [TestCase(1, 280, 40, 6, 50)]
        [TestCase(5, 342, 47, 6, 88)]
        [TestCase(10, 441, 57, 7, 176)]
        [TestCase(30, 1217, 131, 14, 2879)]
        [TestCase(68, 8360, 629, 49, 583077)]
        [TestCase(100, 42336, 2349, 135, 51057107)]
        public void CompoundingUsesEachAuthoredFormulaInsteadOfConflictingIllustrativeTables(
            int level, int hp, int attack, int defense, int trainingCost)
        {
            var unit = new UnitDefinition { MaxHp = 280, Attack = 40, Defense = 6, GrowthModel = "idol-v1" };
            CombatStats stats = BattleSimulator.PlayerStats(unit, level);
            Assert.That(stats.Hp, Is.EqualTo(hp));
            Assert.That(stats.Attack, Is.EqualTo(attack));
            Assert.That(stats.Defense, Is.EqualTo(defense));
            Assert.That(BattleSimulator.TrainingCostAtLevel(level), Is.EqualTo(trainingCost));
            CombatStats equipped = BattleSimulator.PlayerStats(unit, level, new CombatStatBonuses(900, 900, 900));
            Assert.That(equipped.Attack, Is.EqualTo((int)((long)attack * 1200 / 1000)));
        }

        [TestCase("normal", 3)]
        [TestCase("elite", 3)]
        [TestCase("boss", 4)]
        [TestCase("world", 5)]
        public void EncounterTypeDeterminesWholeBattleBudget(string kind, int limit)
        {
            var stage = new StageDefinition { EncounterType = kind };
            Assert.That(stage.RerollLimit, Is.EqualTo(limit));
            BattleSimulator battle = Create("none", encounter: kind);
            Assert.That(battle.BattleDice.BattleRerollLimit, Is.EqualTo(limit));
            Assert.That(battle.WorldEncounter, Is.EqualTo(kind == "world"));
        }

        [Test]
        public void NormalEnemiesNeverRunBossPhasesAndEliteHardenReallyReducesDamageForTwoSeconds()
        {
            BattleSimulator normal = Create("none", encounter: "normal");
            normal.FindUnit(2).Hp = 1000;
            normal.AdvanceRealtime(1000);
            Assert.That(normal.Log.Any(e => e.Kind == BattleEventKind.PhaseChanged), Is.False);
            BattleSimulator elite = Create("none", encounter: "elite",
                rolls: new ScriptedRandom(new[] { 999 }, new[] { 0, 1, 2, 3, 5 }));
            elite.AdvanceRealtime(10000);
            int At(int ms) => elite.Log.Single(e => e.Kind == BattleEventKind.Damage && e.ActorId == 1 && e.SkillId == "rt-basic" && e.TimeMilliseconds == ms).Amount;
            Assert.That(At(8000), Is.EqualTo(At(7000) / 2));
            Assert.That(At(9000), Is.EqualTo(At(8000)));
            Assert.That(At(10000), Is.EqualTo(At(7000)));
        }

        [Test]
        public void BossCrossesSixtyAndThirtyPercentWithRealShieldAndEnrageAndCannotReviveOnLethalHit()
        {
            BattleSimulator battle = Create("none", encounter: "boss");
            BattleUnit boss = battle.FindUnit(2);
            boss.Hp = 600000;
            battle.AdvanceRealtime(1000);
            Assert.That(battle.EnemyPhase, Is.EqualTo(2));
            Assert.That(boss.Shield, Is.EqualTo(150000));
            boss.Hp = 300000; boss.Shield = 0;
            battle.AdvanceRealtime(1000);
            Assert.That(battle.EnemyPhase, Is.EqualTo(3));
            Assert.That(boss.PhaseAttackMultiplierPermille, Is.EqualTo(1500));
            battle.AdvanceRealtime(4000);
            Assert.That(battle.Log.Any(e => e.ActorId == boss.Id && e.SkillId == "rt-enemy-finale"), Is.True);
            BattleSimulator lethal = Create("none", enemyHp: 10, attack: 10000, encounter: "boss");
            lethal.AdvanceRealtime(1000);
            Assert.That(lethal.Outcome, Is.EqualTo(BattleOutcome.Victory));
            Assert.That(lethal.FindUnit(2).Shield, Is.Zero);
        }

        [Test]
        public void BattleEnergyRetainsFractionalHitsAndHonorsWorldAndThreeKindModifiers()
        {
            DiceTurn normal = DiceTurn.ForBattle(new SeededRandom(1), 4, false);
            normal.Begin();
            for (int i = 0; i < 125; i++) normal.RecordDamage(1, false);
            Assert.That(normal.Energy, Is.EqualTo(1));
            normal.RecordDamage(1000, true);
            Assert.That(normal.Energy, Is.EqualTo(34));
            DiceTurn world = DiceTurn.ForBattle(new SeededRandom(1), 5, false);
            world.Begin();
            world.RecordDamage(1000, false, 750);
            Assert.That(world.Energy, Is.EqualTo(6));
            world.RecordDamage(1000, false, 1300);
            Assert.That(world.Energy, Is.EqualTo(16));
        }

        [Test]
        public void FullBattleBudgetNeverRefillsForNextActorAndSpendsOneQuotaPerOperation()
        {
            DiceTurn dice = DiceTurn.ForBattle(new SeededRandom(2), 2, true);
            dice.Begin();
            dice.GainEnergy(DiceTurn.MaxEnergy * 2); // 两条充能发满 2 次额度
            Assert.That(dice.RerollsRemaining, Is.EqualTo(2));
            for (int i = 0; i < 2; i++)
            {
                Assert.That(dice.RerollAll(out _), Is.True, "首章额度的每次操作只花一次");
                Assert.That(dice.RerollsRemaining, Is.EqualTo(1 - i));
                int[] faces = dice.Values.ToArray();
                dice.Begin();
                Assert.That(dice.Values, Is.EqualTo(faces), "换人不能重投或刷新骰面");
            }
            Assert.That(dice.CanReroll, Is.False, "额度用完就不能再重投");
            Assert.That(dice.RerollAll(out _), Is.False);
            dice.GrantFreeReroll();
            Assert.That(dice.CanReroll, Is.True, "救场免费重投是独立的一次机会");
            Assert.That(dice.RerollAll(out _), Is.True);
            Assert.That(dice.RerollsRemaining, Is.Zero, "免费重投不额外消耗普通额度");
            Assert.That(dice.FreeRerolls, Is.Zero);
            Assert.That(dice.CanReroll, Is.False);
        }

        [Test]
        public void SelectiveRerollRequiresATickAndSpendsExactlyOneBudget()
        {
            DiceTurn dice = DiceTurn.ForBattle(new SeededRandom(3), 3, true);
            dice.Begin();
            dice.GainEnergy(DiceTurn.MaxEnergy); // 充能满发 1 次重投
            Assert.That(dice.Held.All(held => held), Is.True, "开局全部保留，避免误触重投整手");
            Assert.That(dice.RerollUnheld(out _), Is.False, "没有点选骰子时不能重投");
            Assert.That(dice.UsedRerolls, Is.Zero, "非法选择不消耗额度");
            for (int i = 0; i < 3; i++) dice.ToggleHold(i);
            int keptFourth = dice.Values[3], keptFifth = dice.Values[4];
            Assert.That(dice.SelectedForRerollCount, Is.EqualTo(3));
            Assert.That(dice.RerollUnheld(out string error), Is.True, error);
            Assert.That(dice.Values[3], Is.EqualTo(keptFourth), "没点选的骰子必须保留");
            Assert.That(dice.Values[4], Is.EqualTo(keptFifth), "没点选的骰子必须保留");
            Assert.That(dice.UsedRerolls, Is.EqualTo(1), "一次操作只扣一次额度");
            Assert.That(dice.SelectedForRerollCount, Is.Zero, "重投后新骰面默认全部保留");
            DiceTurn noSelection = DiceTurn.ForBattle(new SeededRandom(3), 3, false);
            noSelection.Begin();
            noSelection.GainEnergy(DiceTurn.MaxEnergy); // 有额度仍受自选开关约束
            for (int i = 0; i < 4; i++) noSelection.ToggleHold(i);
            Assert.That(noSelection.RerollUnheld(out _), Is.False, "不支持自选重投的会话只能全部重投");
            Assert.That(noSelection.UsedRerolls, Is.Zero);
        }

        [Test]
        public void BasicAttacksAndBothIndependentSkillsRunWithoutManualActions()
        {
            BattleSimulator battle = Create("demon");
            battle.AdvanceRealtime(12000);
            Assert.That(battle.Log.Count(e => e.Kind == BattleEventKind.ActionStarted && e.ActorId == 1 && e.SkillId == "rt-basic"), Is.EqualTo(12));
            Assert.That(battle.Log.Count(e => e.Kind == BattleEventKind.ActionStarted && e.ActorId == 1 && e.SkillId == "rt-demon-small"), Is.EqualTo(4));
            Assert.That(battle.Log.Count(e => e.Kind == BattleEventKind.ActionStarted && e.ActorId == 1 && e.SkillId == "rt-demon-big"), Is.EqualTo(1));
            Assert.That(battle.Log.First(e => e.SkillId == "rt-demon-small").TimeMilliseconds, Is.EqualTo(3000));
            Assert.That(battle.TryAct(new BattleAction(), out string message), Is.False);
            Assert.That(message, Does.Contain("自动"));
        }

        [Test]
        public void FramePartitionDoesNotChangeCombatAndZeroTimeDoesNotAdvancePause()
        {
            BattleSimulator one = Create("charm"), many = Create("charm");
            one.AdvanceRealtime(16000);
            for (int i = 0; i < 1600; i++) many.AdvanceRealtime(10);
            Assert.That(Signature(one), Is.EqualTo(Signature(many)));
            int hp = many.CurrentEnemyHp;
            many.AdvanceRealtime(0, true);
            Assert.That(many.CurrentEnemyHp, Is.EqualTo(hp));
            Assert.That(many.ElapsedMilliseconds, Is.EqualTo(16000));
        }

        [Test]
        public void ActualDamageAndKillCreditExcludeOverkillAndNoDiceClickDealsIndependentDamage()
        {
            BattleSimulator battle = Create("none", enemyHp: 10, attack: 10000);
            int before = battle.CurrentEnemyHp;
            battle.BattleDice.GainEnergy(100);
            Assert.That(battle.BattleDice.EnergyRerollAll(out _), Is.True);
            Assert.That(battle.CurrentEnemyHp, Is.EqualTo(before));
            battle.AdvanceRealtime(1000);
            Assert.That(battle.CharacterDamageDealt, Is.EqualTo(10));
            Assert.That(battle.BattleDice.Energy, Is.InRange(25, 32), "只有实际损失的10生命和一次击杀奖励");
            Assert.That(battle.Log.Count(e => e.Kind == BattleEventKind.Defeated), Is.EqualTo(1));
            long dealt = battle.CharacterDamageDealt;
            battle.AdvanceRealtime(10000);
            Assert.That(battle.CharacterDamageDealt, Is.EqualTo(dealt));
        }

        [Test]
        public void MermaidHealDoesNotReviveAndTemporaryShieldExpiresAtFourSeconds()
        {
            BattleSimulator battle = Create("mermaid");
            BattleUnit mermaid = battle.FindUnit(1);
            mermaid.Hp -= 10000;
            int hp = mermaid.Hp;
            battle.AdvanceRealtime(3000);
            Assert.That(mermaid.Hp, Is.GreaterThan(hp));
            battle.AdvanceRealtime(10000);
            int shield = mermaid.Shield;
            Assert.That(shield, Is.GreaterThan(0));
            Assert.That(battle.Log.Any(e => e.SkillId == "rt-mermaid-big" && e.TimeMilliseconds == 13000), Is.True);
            battle.AdvanceRealtime(4000);
            Assert.That(mermaid.Shield, Is.LessThan(shield), "主动护盾只持续4秒，骰型护盾保留");
        }

        [Test]
        public void DemonPoisonHasFifteenStackCapAndBurstRemainsCharacterAttributed()
        {
            BattleSimulator battle = Create("demon");
            battle.AdvanceRealtime(3900);
            Assert.That(battle.PoisonStacks(battle.FindUnit(2)), Is.LessThanOrEqualTo(15));
            battle.AdvanceRealtime(100);
            Assert.That(battle.PoisonDamageDealt, Is.GreaterThan(0));
            Assert.That(battle.PoisonStacks(battle.FindUnit(2)), Is.LessThanOrEqualTo(7));
            Assert.That(battle.Log.Where(e => e.SkillId == "rt-poison").All(e => e.ActorId == 1), Is.True);
        }

        [Test]
        public void BloodElfSmallSkillPiercesIndependentlyOfDiceAndHighPointRescueCannotLoop()
        {
            BattleSimulator battle = Create("elf");
            battle.AdvanceRealtime(2800);
            Assert.That(battle.Log.Any(e => e.SkillId == "rt-bloodelf-small" && e.TimeMilliseconds == 2800), Is.True);
            for (int i = 0; i < 30; i++)
            {
                battle.BattleDice.GainEnergy(100);
                battle.BattleDice.EnergyRerollAll(out _);
                battle.AdvanceRealtime(25, true);
            }
            Assert.That(battle.BattleDice.UsedRerolls, Is.LessThanOrEqualTo(4));
        }

        [Test]
        public void ElfPiercingReducesDefenseWithoutRequiringAHighHand()
        {
            var random = new ScriptedRandom(new[] { 999 }, new[] { 0, 1, 2, 3, 5 });
            BattleSimulator battle = Create("elf", rolls: random);
            BattleUnit enemy = battle.FindUnit(2);
            enemy.BaseDefense = 100;
            Assert.That(battle.BattleDice.Hand.Pattern, Is.EqualTo(DicePattern.HighPoint));
            battle.AdvanceRealtime(2800);
            BattleEvent hit = battle.Log.Single(e => e.Kind == BattleEventKind.Damage && e.SkillId == "rt-bloodelf-small");
            Assert.That(hit.Amount, Is.EqualTo(85), "115伤害按85防御结算，技能本身固定穿甲15%");
            Assert.That(battle.BattleDice.FreeRerolls, Is.EqualTo(1));
            Assert.That(battle.BattleDice.EnergyRerollAll(out _), Is.True);
            battle.AdvanceRealtime(25);
            Assert.That(battle.BattleDice.FreeRerolls, Is.Zero, "免费重投不能递归获得无限免费重投");
        }

        [Test]
        public void CharmStunsBrieflyThenSilenceContinuesSuppressingSkills()
        {
            BattleSimulator battle = Create("charm");
            battle.AdvanceRealtime(14000);
            int[] specials = battle.Log.Where(e => e.Kind == BattleEventKind.ActionStarted && e.ActorId == 2 && e.SkillId == "strike")
                .Select(e => e.TimeMilliseconds).ToArray();
            Assert.That(specials, Is.EqualTo(new[] { 4000, 8000, 13000 }));
            Assert.That(battle.Log.Any(e => e.ActorId == 2 && e.SkillId == "rt-basic" &&
                e.TimeMilliseconds == 12000), Is.False, "眩晕期间不能继续普攻");
            Assert.That(battle.Log.Any(e => e.ActorId == 2 && e.SkillId == "rt-basic" &&
                e.TimeMilliseconds == 12400), Is.True, "眩晕结束后即使仍封技，也恢复普攻");
        }

        [Test]
        public void OpeningHandAndRepeatedUiInitializationCannotDealDamageBeforeTheClockStarts()
        {
            var random = new ScriptedRandom(new[] { 999 }, new[] { 5, 5, 5, 5, 5 });
            BattleSimulator battle = Create("charm", rolls: random);
            Assert.That(battle.CharacterDamageDealt, Is.Zero);
            Assert.That(battle.CurrentEnemyHp, Is.EqualTo(1000000));
            battle.EnableRealtime(Races, "charm");
            battle.AdvanceRealtime(0);
            Assert.That(battle.CharacterDamageDealt, Is.Zero);
            battle.AdvanceRealtime(25);
            Assert.That(battle.CharacterDamageDealt, Is.EqualTo(125));
            battle.AdvanceRealtime(25);
            Assert.That(battle.CharacterDamageDealt, Is.EqualTo(125), "同一个骰型只在新投掷时触发一次即时追击");
        }

        [Test]
        public void EncounterStopsExactlyAtSixtySecondsAndCannotBeReinitialized()
        {
            BattleSimulator battle = Create("none");
            battle.AdvanceRealtime(60000);
            Assert.That(battle.Outcome, Is.EqualTo(BattleOutcome.Defeat));
            Assert.That(battle.ElapsedMilliseconds, Is.EqualTo(60000));
            battle.EnableRealtime(Races);
            battle.AdvanceRealtime(1000);
            Assert.That(battle.ElapsedMilliseconds, Is.EqualTo(60000));
            Assert.That(battle.Log.Count(e => e.Kind == BattleEventKind.Finished), Is.EqualTo(1));
        }

        private static string Signature(BattleSimulator battle) => string.Join(";", battle.Log.Select(e =>
            $"{e.TimeMilliseconds}:{e.Kind}:{e.ActorId}:{e.TargetId}:{e.SkillId}:{e.Amount}"));

        [Test]
        public void PendingWaveCannotBeFocusedAttackedOrActBeforeSpawning()
        {
            BattleSimulator battle = CreateWaves();
            BattleUnit reserve = battle.FindUnit(3);
            Assert.That(reserve.Spawned, Is.False);
            Assert.That(reserve.Alive, Is.False);
            Assert.That(battle.FocusEnemy(reserve.Id), Is.False);
            Assert.That(battle.UnitAt(BattleSide.Enemy, reserve.Row, reserve.Col), Is.Null);
            battle.AdvanceRealtime(10000);
            Assert.That(reserve.Hp, Is.EqualTo(reserve.MaxHp));
            Assert.That(battle.Log.Any(e => e.Kind == BattleEventKind.Damage &&
                (e.TargetId == reserve.Id || e.ActorId == reserve.Id)), Is.False);
        }

        [Test]
        public void ClearingOpeningWaveSpawnsReserveInsteadOfFinishingBattle()
        {
            BattleSimulator battle = CreateWaves();
            battle.BattleDice.GainEnergy(100);
            Assert.That(battle.BattleDice.EnergyRerollAll(out _), Is.True);
            int bonus = battle.BattleDice.AccumulatedBonusPermille;
            battle.FindUnit(2).Hp = 1;
            battle.AdvanceRealtime(1000);
            Assert.That(battle.Outcome, Is.EqualTo(BattleOutcome.Ongoing));
            Assert.That(battle.FindUnit(3).Spawned, Is.True);
            Assert.That(battle.CurrentWave, Is.EqualTo(2));
            Assert.That(battle.BattleDice.AccumulatedBonusPermille, Is.EqualTo(bonus));
            Assert.That(battle.Log.Count(e => e.Kind == BattleEventKind.Spawned), Is.EqualTo(1));
            Assert.That(battle.Log.Any(e => e.Kind == BattleEventKind.Finished), Is.False);
            Assert.That(battle.FindUnit(3).Hp, Is.EqualTo(battle.FindUnit(3).MaxHp));
        }

        [Test]
        public void BossSixtyPercentPhaseSpawnsAuthoredReinforcementOnce()
        {
            BattleSimulator battle = CreateWaves(encounter: "boss");
            BattleUnit boss = battle.FindUnit(2);
            boss.Hp = 600000;
            battle.AdvanceRealtime(1000);
            Assert.That(battle.EnemyPhase, Is.EqualTo(2));
            Assert.That(boss.Alive, Is.True);
            Assert.That(battle.FindUnit(3).Spawned, Is.True);
            Assert.That(battle.Log.Count(e => e.Kind == BattleEventKind.Spawned), Is.EqualTo(1));
            battle.AdvanceRealtime(1000);
            Assert.That(battle.Log.Count(e => e.Kind == BattleEventKind.Spawned), Is.EqualTo(1));
        }

        [Test]
        public void LethalBossHitDoesNotSkipUnspawnedReinforcements()
        {
            BattleSimulator battle = CreateWaves(encounter: "boss");
            battle.FindUnit(2).Hp = 1;
            battle.AdvanceRealtime(1000);
            Assert.That(battle.FindUnit(2).Alive, Is.False);
            Assert.That(battle.FindUnit(2).Shield, Is.Zero);
            Assert.That(battle.FindUnit(3).Spawned, Is.True);
            Assert.That(battle.Outcome, Is.EqualTo(BattleOutcome.Ongoing));
            battle.FindUnit(3).Hp = 1;
            battle.AdvanceRealtime(1000);
            Assert.That(battle.Outcome, Is.EqualTo(BattleOutcome.Victory));
        }

        [Test]
        public void OneAreaSkillCannotHitTheNextWaveItJustSpawned()
        {
            BattleSimulator battle = CreateWaves("demon");
            battle.AdvanceRealtime(11975);
            battle.FindUnit(2).Hp = 250;
            battle.AdvanceRealtime(25);
            Assert.That(battle.FindUnit(2).Alive, Is.False);
            Assert.That(battle.FindUnit(3).Spawned, Is.True);
            Assert.That(battle.Log.Any(e => e.Kind == BattleEventKind.Damage &&
                e.SkillId == "rt-demon-big" && e.TargetId == 3), Is.False,
                "范围技能只能命中释放时在场的敌人，不能跨波扫到刚刷新的增援");
        }

        [Test]
        public void TwoMinuteDeadlineDefeatsExactlyOnceAndZeroStepDoesNotConsumeTime()
        {
            BattleSimulator battle = CreateWaves();
            battle.Stage.TimeLimitSeconds = 120;
            battle.AdvanceRealtime(119975);
            Assert.That(battle.Outcome, Is.EqualTo(BattleOutcome.Ongoing));
            battle.AdvanceRealtime(0);
            Assert.That(battle.ElapsedMilliseconds, Is.EqualTo(119975));
            battle.AdvanceRealtime(25);
            Assert.That(battle.Outcome, Is.EqualTo(BattleOutcome.Defeat));
            Assert.That(battle.ElapsedMilliseconds, Is.EqualTo(120000));
            battle.AdvanceRealtime(1000);
            Assert.That(battle.Log.Count(e => e.Kind == BattleEventKind.Finished), Is.EqualTo(1));
        }

        [Test]
        public void NinetySecondStageDoesNotStopAtLegacySixtySecondLimit()
        {
            BattleSimulator battle = CreateWaves();
            battle.AdvanceRealtime(60000);
            Assert.That(battle.Outcome, Is.EqualTo(BattleOutcome.Ongoing));
            battle.AdvanceRealtime(30000);
            Assert.That(battle.Outcome, Is.EqualTo(BattleOutcome.Defeat));
            Assert.That(battle.ElapsedMilliseconds, Is.EqualTo(90000));
            Assert.That(battle.Log.Count(e => e.Kind == BattleEventKind.Finished), Is.EqualTo(1));
        }

        [TestCase(75000, 3)]
        [TestCase(76000, 2)]
        public void ThreeStarTimeUsesStageSeventyFiveSecondBoundary(int victoryTime, int expectedStars)
        {
            BattleSimulator battle = Create("none");
            battle.Stage.TimeLimitSeconds = 90;
            battle.Stage.ThreeStarSeconds = 75;
            battle.AdvanceRealtime(victoryTime - 1000);
            battle.FindUnit(2).Hp = 1;
            battle.AdvanceRealtime(1000);
            Assert.That(battle.Outcome, Is.EqualTo(BattleOutcome.Victory));
            Assert.That(battle.ElapsedMilliseconds, Is.EqualTo(victoryTime));
            Assert.That(battle.StarRating(), Is.EqualTo(expectedStars));
        }

        [Test]
        public void WaveAndPhaseEventsRemainIdenticalAcrossTimeStepPartitions()
        {
            BattleSimulator one = CreateWaves(encounter: "boss");
            BattleSimulator many = CreateWaves(encounter: "boss");
            one.FindUnit(2).Hp = 600000;
            many.FindUnit(2).Hp = 600000;
            one.AdvanceRealtime(90000, true);
            for (int i = 0; i < 9000; i++) many.AdvanceRealtime(10, true);
            Assert.That(Signature(one), Is.EqualTo(Signature(many)));
            Assert.That(one.Units.Select(unit => unit.Hp), Is.EqualTo(many.Units.Select(unit => unit.Hp)));
            Assert.That(one.Outcome, Is.EqualTo(many.Outcome));
        }

        [Test]
        public void TurnModeKeepsAuthoredEnemiesPresentUntilRealtimeIsExplicitlyEnabled()
        {
            BattleSimulator battle = CreateWaves(enableRealtime: false);
            Assert.That(battle.IsRealtime, Is.False);
            Assert.That(battle.Units.Where(unit => unit.Side == BattleSide.Enemy).All(unit => unit.Alive),
                Is.True, "旧回合接口不能因为关卡写了实时分波，就漏掉后续敌人并提前结算");
        }

        [Test]
        public void AreaSkillEmitsOneActionStartAndSeparateDamageEventsForEveryTarget()
        {
            BattleSimulator battle = CreateWaves("demon");
            battle.FindUnit(3).Spawned = true;
            battle.AdvanceRealtime(12000);
            Assert.That(battle.Log.Count(e => e.Kind == BattleEventKind.ActionStarted &&
                e.ActorId == 1 && e.SkillId == "rt-demon-big"), Is.EqualTo(1));
            Assert.That(battle.Log.Count(e => e.Kind == BattleEventKind.Damage &&
                e.ActorId == 1 && e.SkillId == "rt-demon-big"), Is.EqualTo(2));
        }

        private static BattleSimulator CreateWaves(string race = "none", string encounter = "normal",
            bool enableRealtime = true)
        {
            var manifest = new TacticsManifest();
            manifest.Skills.Add(new SkillDefinition { Id = "strike", Name = "打击" });
            manifest.Units.Add(new UnitDefinition { Id = race, Name = "测试成员", MaxHp = 100000,
                Attack = 100, Defense = 0, Speed = 100, CritPermille = 0,
                SkillIds = new List<string> { "strike" } });
            manifest.Units.Add(new UnitDefinition { Id = "enemy", Name = "测试敌人", MaxHp = 1000000,
                Attack = 1, Defense = 0, Speed = 50, CritPermille = 0,
                SkillIds = new List<string> { "strike" } });
            var stage = new StageDefinition { Id = "wave-test", Name = "分波测试", EncounterType = encounter,
                TimeLimitSeconds = 90, ThreeStarSeconds = 75,
                Enemies = new List<EnemySpawn> {
                    new EnemySpawn { UnitId = "enemy", Row = 0, Col = 0, Wave = 0 },
                    new EnemySpawn { UnitId = "enemy", Row = 1, Col = 0, Wave = 1,
                        BossPhase = encounter == "boss" ? 2 : 0 }
                } };
            manifest.Stages.Add(stage);
            var battle = new BattleSimulator(manifest, stage,
                new[] { new PlayerUnitSetup { UnitId = race, Level = 1 } },
                new ScriptedRandom(new[] { 999 }, new[] { 0, 1, 2, 3, 5 }));
            if (enableRealtime) battle.EnableRealtime(Races, race, stage.RerollLimit);
            return battle;
        }

        internal static BattleSimulator Create(string race, int enemyHp = 1000000, int attack = 100,
            IRandomSource rolls = null, string encounter = "")
        {
            var manifest = new TacticsManifest();
            manifest.Skills.Add(new SkillDefinition { Id = "strike", Name = "打击" });
            manifest.Units.Add(new UnitDefinition { Id = race, Name = "测试成员", MaxHp = 100000,
                Attack = attack, Defense = 0, Speed = 100, CritPermille = 0, SkillIds = new List<string> { "strike" } });
            manifest.Units.Add(new UnitDefinition { Id = "enemy", Name = "测试敌人", MaxHp = enemyHp,
                Attack = 1, Defense = 0, Speed = 50, CritPermille = 0, SkillIds = new List<string> { "strike" } });
            var stage = new StageDefinition { Id = "realtime-test", Name = "计时测试", TurnLimit = 20, EncounterType = encounter,
                Enemies = new List<EnemySpawn> { new EnemySpawn { UnitId = "enemy", Row = 0, Col = 0 } } };
            manifest.Stages.Add(stage);
            var battle = new BattleSimulator(manifest, stage,
                new[] { new PlayerUnitSetup { UnitId = race, Level = 1 } }, rolls ?? new SeededRandom(847));
            battle.EnableRealtime(Races, race, stage.RerollLimit, encounter == "world");
            return battle;
        }
    }
}
