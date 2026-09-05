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
        public void FullBattleBudgetNeverRefillsForNextActorOrOverflowAndRequiresEnergy()
        {
            DiceTurn dice = DiceTurn.ForBattle(new SeededRandom(2), 2, false);
            dice.Begin();
            Assert.That(dice.EnergyRerollAll(out _), Is.False);
            for (int i = 0; i < 2; i++)
            {
                dice.RecordDamage(int.MaxValue, true);
                Assert.That(dice.Energy, Is.EqualTo(100));
                Assert.That(dice.EnergyRerollAll(out _), Is.True);
                Assert.That(dice.Energy, Is.Zero, "溢出能量不能存成第二次重投");
                int[] faces = dice.Values.ToArray();
                dice.Begin();
                Assert.That(dice.Values, Is.EqualTo(faces));
                Assert.That(dice.RerollsRemaining, Is.EqualTo(1 - i));
            }
            dice.GainEnergy(100);
            dice.GrantFreeReroll();
            Assert.That(dice.CanEnergyReroll, Is.False);
        }

        [Test]
        public void OnlyMermaidCanRerollOneOrTwoDiceAndInvalidSelectionDoesNotSpend()
        {
            DiceTurn dice = DiceTurn.ForBattle(new SeededRandom(3), 3, true);
            dice.Begin();
            dice.GainEnergy(100);
            Assert.That(dice.RerollUnheld(out _), Is.False);
            Assert.That(dice.Energy, Is.EqualTo(100));
            for (int i = 0; i < 3; i++) dice.ToggleHold(i);
            int[] before = dice.Values.ToArray();
            Assert.That(dice.RerollUnheld(out _), Is.True);
            Assert.That(dice.Values.Take(3), Is.EqualTo(before.Take(3)));
            Assert.That(dice.UsedRerolls, Is.EqualTo(1));
            DiceTurn other = DiceTurn.ForBattle(new SeededRandom(3), 3, false);
            other.Begin(); other.GainEnergy(100);
            for (int i = 0; i < 4; i++) other.ToggleHold(i);
            Assert.That(other.RerollUnheld(out _), Is.False);
            Assert.That(other.Energy, Is.EqualTo(100));
        }

        [Test]
        public void BasicAttacksAndBothIndependentSkillsRunWithoutManualActions()
        {
            BattleSimulator battle = Create("demon");
            battle.AdvanceRealtime(12000);
            Assert.That(battle.Log.Count(e => e.ActorId == 1 && e.SkillId == "rt-basic"), Is.EqualTo(12));
            Assert.That(battle.Log.Count(e => e.ActorId == 1 && e.SkillId == "rt-demon-small"), Is.EqualTo(4));
            Assert.That(battle.Log.Count(e => e.ActorId == 1 && e.SkillId == "rt-demon-big"), Is.EqualTo(1));
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
            BattleEvent hit = battle.Log.Single(e => e.SkillId == "rt-bloodelf-small");
            Assert.That(hit.Amount, Is.EqualTo(85), "115伤害按85防御结算，技能本身固定穿甲15%");
            Assert.That(battle.BattleDice.FreeRerolls, Is.EqualTo(1));
            Assert.That(battle.BattleDice.EnergyRerollAll(out _), Is.True);
            battle.AdvanceRealtime(25);
            Assert.That(battle.BattleDice.FreeRerolls, Is.Zero, "免费重投不能递归获得无限免费重投");
        }

        [Test]
        public void CharmSilenceDelaysEnemySkillButNotBasicAttacks()
        {
            BattleSimulator battle = Create("charm");
            battle.AdvanceRealtime(14000);
            int[] specials = battle.Log.Where(e => e.ActorId == 2 && e.SkillId == "strike")
                .Select(e => e.TimeMilliseconds).ToArray();
            Assert.That(specials, Is.EqualTo(new[] { 4000, 8000, 13000 }));
            Assert.That(battle.Log.Any(e => e.ActorId == 2 && e.SkillId == "rt-basic" &&
                e.TimeMilliseconds == 12000), Is.True, "封技能时普攻仍继续");
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
            Assert.That(battle.CharacterDamageDealt, Is.EqualTo(200));
            battle.AdvanceRealtime(25);
            Assert.That(battle.CharacterDamageDealt, Is.EqualTo(200), "同一个骰型只在新投掷时触发一次即时追击");
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

        internal static BattleSimulator Create(string race, int enemyHp = 1000000, int attack = 100,
            IRandomSource rolls = null)
        {
            var manifest = new TacticsManifest();
            manifest.Skills.Add(new SkillDefinition { Id = "strike", Name = "打击" });
            manifest.Units.Add(new UnitDefinition { Id = race, Name = "测试成员", MaxHp = 100000,
                Attack = attack, Defense = 0, Speed = 100, CritPermille = 0, SkillIds = new List<string> { "strike" } });
            manifest.Units.Add(new UnitDefinition { Id = "enemy", Name = "测试敌人", MaxHp = enemyHp,
                Attack = 1, Defense = 0, Speed = 50, CritPermille = 0, SkillIds = new List<string> { "strike" } });
            var stage = new StageDefinition { Id = "realtime-test", Name = "计时测试", TurnLimit = 20,
                Enemies = new List<EnemySpawn> { new EnemySpawn { UnitId = "enemy", Row = 0, Col = 0 } } };
            manifest.Stages.Add(stage);
            var battle = new BattleSimulator(manifest, stage,
                new[] { new PlayerUnitSetup { UnitId = race, Level = 1 } }, rolls ?? new SeededRandom(847));
            battle.EnableRealtime(Races, race);
            return battle;
        }
    }
}
