using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ChoSiren.Systems;
using ChoSiren.Systems.Tactics;
using NUnit.Framework;

namespace ChoSiren.Tests
{
    public sealed class RealtimeLeadershipTests
    {
        [Test]
        public void FallenCaptainTransfersInInitialPartyOrderAndWipeLeavesNoCommander()
        {
            BattleSimulator battle = CreateParty("demon", "mermaid", "elf");
            Assert.That(battle.CurrentLeaderId, Is.EqualTo(1));
            Kill(battle, 1);
            Assert.That(battle.CurrentLeaderId, Is.EqualTo(2));
            Assert.That(battle.CurrentLeaderRace, Is.EqualTo(CombatRace.Mermaid));
            Assert.That(battle.BattleDice.SelectiveReroll, Is.True);
            Kill(battle, 2);
            Assert.That(battle.CurrentLeaderId, Is.EqualTo(3));
            Assert.That(battle.CurrentLeaderRace, Is.EqualTo(CombatRace.BloodElf));
            Assert.That(battle.BattleDice.SelectiveReroll, Is.True, "自选重投对所有队长开放，接任不改变规则");
            Kill(battle, 3);
            Assert.That(battle.CurrentLeaderId, Is.EqualTo(-1));
            Assert.That(battle.CurrentLeaderRace, Is.EqualTo(CombatRace.None));
            Assert.That(battle.Outcome, Is.EqualTo(BattleOutcome.Defeat));
            Assert.That(battle.PlayerUnitsLost, Is.EqualTo(3));
            CollectionAssert.AreEqual(new[] { 2, 3, -1 }, battle.Log
                .Where(e => e.Kind == BattleEventKind.LeaderChanged).Select(e => e.TargetId));
            Assert.That(battle.Log.Count(e => e.Kind == BattleEventKind.Finished), Is.EqualTo(1));
        }

        [Test]
        public void NonCaptainDeathDoesNotChangeCommandOrReplayHand()
        {
            BattleSimulator battle = CreateParty("demon", "mermaid", "elf");
            Kill(battle, 2);
            Assert.That(battle.CurrentLeaderId, Is.EqualTo(1));
            Assert.That(battle.Log.Any(e => e.Kind == BattleEventKind.LeaderChanged), Is.False);
            Kill(battle, 1);
            Assert.That(battle.CurrentLeaderId, Is.EqualTo(3), "接任时跳过已倒下成员");
        }

        [Test]
        public void HandoverPreservesDiceBudgetsCooldownsAndAppliedShieldsWithoutReplayingHand()
        {
            BattleSimulator battle = CreateParty("mermaid", "elf", "mermaid");
            battle.AdvanceRealtime(25);
            battle.BattleDice.GainEnergy(100);
            Assert.That(battle.BattleDice.EnergyRerollAll(out _), Is.True);
            battle.AdvanceRealtime(25);
            battle.BattleDice.GainEnergy(43);
            battle.BattleDice.GrantFreeReroll();
            for (int i = 0; i < 3; i++) battle.BattleDice.ToggleHold(i);
            int[] values = battle.BattleDice.Values.ToArray();
            int energy = battle.BattleDice.Energy, revision = battle.BattleDice.Revision;
            int used = battle.BattleDice.UsedRerolls, free = battle.BattleDice.FreeRerolls;
            int bonus = battle.BattleDice.AccumulatedBonusPermille;
            BattleUnit successor = battle.FindUnit(2);
            int small = battle.SkillCooldownRemaining(successor, false);
            int big = battle.SkillCooldownRemaining(successor, true);
            int shield = successor.Shield;
            int appliedEffects = battle.Log.Count(e => e.Kind == BattleEventKind.Shield || e.Kind == BattleEventKind.Heal);
            Kill(battle, 1);
            CollectionAssert.AreEqual(values, battle.BattleDice.Values);
            Assert.That(battle.BattleDice.Energy, Is.EqualTo(energy));
            Assert.That(battle.BattleDice.Revision, Is.EqualTo(revision));
            Assert.That(battle.BattleDice.UsedRerolls, Is.EqualTo(used));
            Assert.That(battle.BattleDice.FreeRerolls, Is.EqualTo(free));
            Assert.That(battle.BattleDice.AccumulatedBonusPermille, Is.EqualTo(bonus));
            Assert.That(battle.BattleDice.SelectedForRerollCount, Is.EqualTo(3),
                "接任不清空玩家已点选的重投骰，也不能重投或刷新骰面");
            Assert.That(battle.SkillCooldownRemaining(successor, false), Is.EqualTo(small));
            Assert.That(battle.SkillCooldownRemaining(successor, true), Is.EqualTo(big));
            Assert.That(successor.Shield, Is.EqualTo(shield));
            battle.AdvanceRealtime(25);
            Assert.That(battle.Log.Count(e => e.Kind == BattleEventKind.Shield || e.Kind == BattleEventKind.Heal),
                Is.EqualTo(appliedEffects));
            Kill(battle, 2);
            Assert.That(battle.BattleDice.SelectiveReroll, Is.True,
                "自选重投是全场共用规则，接任后依旧可用，并非人鱼专属能力");
            Assert.That(battle.BattleDice.UsedRerolls, Is.EqualTo(used));
            Assert.That(battle.BattleDice.Revision, Is.EqualTo(revision));
        }

        [Test]
        public void DeadDemonCaptainStopsGrantingNewPoisonButExistingPoisonSurvives()
        {
            BattleSimulator battle = CreatePartyWithHand(new[] { 0, 0, 1, 2, 3 }, "demon", "none");
            BattleUnit enemy = battle.Units.Last();
            battle.AdvanceRealtime(3000);
            int layers = battle.PoisonStacks(enemy);
            Assert.That(layers, Is.GreaterThan(0));
            Kill(battle, 1);
            Assert.That(battle.PoisonStacks(enemy), Is.EqualTo(layers));
            battle.AdvanceRealtime(1000);
            Assert.That(battle.PoisonDamageDealt, Is.GreaterThan(0), "已施加的毒不会随施法者倒下被抹除");
            Assert.That(battle.PoisonStacks(enemy), Is.EqualTo(layers / 2), "原队长倒下后，普通成员不再追加队长毒层");
            Assert.That(battle.CurrentLeaderRace, Is.EqualTo(CombatRace.None));
        }

        [Test]
        public void IncomingDemonCommanderImmediatelyGrantsContinuousPoisonPassive()
        {
            BattleSimulator battle = CreatePartyWithHand(new[] { 0, 0, 1, 2, 3 }, "none", "demon");
            BattleUnit enemy = battle.Units.Last();
            battle.AdvanceRealtime(1000);
            Assert.That(battle.PoisonStacks(enemy), Is.Zero);
            int revision = battle.BattleDice.Revision;
            Kill(battle, 1);
            battle.AdvanceRealtime(1000);
            Assert.That(battle.PoisonStacks(enemy), Is.EqualTo(1));
            Assert.That(battle.BattleDice.Revision, Is.EqualTo(revision));
        }

        [Test]
        public void FullHealthHealingStillEmitsOneActionStartedWithoutFakeHealing()
        {
            BattleSimulator battle = CreateParty("mermaid", "none");
            CastSkill(battle, 1, false);
            BattleEvent started = battle.Log.Single(e => e.Kind == BattleEventKind.ActionStarted);
            Assert.That(started.SkillId, Is.EqualTo("rt-mermaid-small"));
            Assert.That(started.ActorId, Is.EqualTo(1));
            Assert.That(started.TargetId, Is.EqualTo(1));
            Assert.That(battle.Log.Any(e => e.Kind == BattleEventKind.Heal), Is.False);
        }

        [Test]
        public void FullShieldSkillStillEmitsOneStartForTheWholeParty()
        {
            BattleSimulator battle = CreateParty("mermaid", "none");
            foreach (BattleUnit unit in battle.Units.Where(unit => unit.Side == BattleSide.Player))
                unit.Shield = unit.MaxHp;
            CastSkill(battle, 1, true);
            Assert.That(battle.Log.Count(e => e.Kind == BattleEventKind.ActionStarted &&
                e.SkillId == "rt-mermaid-big"), Is.EqualTo(1));
            Assert.That(battle.Log.Any(e => e.Kind == BattleEventKind.Shield), Is.False);
        }

        [Test]
        public void MultipleElfSuccessorsCannotRefreshTheOncePerBattleRescue()
        {
            BattleSimulator battle = CreateParty("elf", "none", "elf");
            battle.AdvanceRealtime(25);
            Assert.That(battle.BattleDice.FreeRerolls, Is.EqualTo(1));
            Assert.That(battle.BattleDice.EnergyRerollAll(out _), Is.True);
            battle.AdvanceRealtime(25);
            Assert.That(battle.BattleDice.FreeRerolls, Is.Zero);
            Kill(battle, 1);
            Kill(battle, 2);
            battle.AdvanceRealtime(25);
            Assert.That(battle.BattleDice.FreeRerolls, Is.Zero);
            battle.BattleDice.GainEnergy(100);
            Assert.That(battle.BattleDice.EnergyRerollAll(out _), Is.True);
            battle.AdvanceRealtime(25);
            Assert.That(battle.BattleDice.RerollsRemaining, Is.GreaterThan(0));
            Assert.That(battle.BattleDice.FreeRerolls, Is.Zero, "接任或新骰型都不能重置本场已使用的救场资格");
        }

        private static void Kill(BattleSimulator battle, int unitId)
        {
            // Drive the production damage/death path without advancing clocks or spending dice.
            MethodInfo damage = typeof(BattleSimulator).GetMethod("ApplyActualDamage",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(damage, Is.Not.Null);
            damage.Invoke(battle, new object[] { battle.Units.Last(), battle.FindUnit(unitId),
                "test-lethal", int.MaxValue, false, 1000 });
        }

        private static void CastSkill(BattleSimulator battle, int unitId, bool big)
        {
            MethodInfo cast = typeof(BattleSimulator).GetMethod("CastRaceSkill",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(cast, Is.Not.Null);
            cast.Invoke(battle, new object[] { battle.FindUnit(unitId), big });
        }

        private static BattleSimulator CreateParty(params string[] races) =>
            CreatePartyWithHand(new[] { 0, 1, 2, 3, 5 }, races);

        private static BattleSimulator CreatePartyWithHand(int[] hand, params string[] races)
        {
            var manifest = new TacticsManifest();
            manifest.Skills.Add(new SkillDefinition { Id = "strike", Name = "打击" });
            var party = new List<PlayerUnitSetup>();
            var labels = new Dictionary<string, string>();
            for (int i = 0; i < races.Length; i++)
            {
                string id = races[i] + "-" + i;
                labels[id] = races[i] == "demon" ? "恶魔" : races[i] == "mermaid" ? "人鱼"
                    : races[i] == "elf" ? "血精灵" : races[i] == "charm" ? "魅族" : "";
                manifest.Units.Add(new UnitDefinition { Id = id, Name = id, MaxHp = 100000,
                    Attack = 100, Defense = 0, Speed = 100, CritPermille = 0,
                    SkillIds = new List<string> { "strike" } });
                party.Add(new PlayerUnitSetup { UnitId = id, Row = i % 3, Col = i / 3, Level = 1 });
            }
            manifest.Units.Add(new UnitDefinition { Id = "enemy", Name = "测试敌人", MaxHp = 10000000,
                Attack = 1, Defense = 0, Speed = 50, CritPermille = 0,
                SkillIds = new List<string> { "strike" } });
            var stage = new StageDefinition { Id = "leadership-test", Name = "临时指挥测试",
                EncounterType = "normal", TimeLimitSeconds = 90, ThreeStarSeconds = 75,
                Enemies = new List<EnemySpawn> { new EnemySpawn { UnitId = "enemy", Row = 0, Col = 0 } } };
            manifest.Stages.Add(stage);
            var battle = new BattleSimulator(manifest, stage, party,
                new ScriptedRandom(new[] { 999 }, Enumerable.Range(0, 50).Select(i => hand[i % 5]).ToArray()));
            battle.EnableRealtime(labels, party[0].UnitId, 4);
            return battle;
        }
    }
}
