using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ChoSiren.Systems;
using ChoSiren.Systems.Tactics;
using NUnit.Framework;

namespace ChoSiren.Tests
{
    public sealed class ChapterTwoEnemyRoles1019Tests
    {
        [TestCase("stage-1-9", 19, "normal", EnemySpecialCapability.StunStrike)]
        [TestCase("stage-2-1", 10, "normal", EnemySpecialCapability.StunStrike)]
        [TestCase("stage-3-1", 21, "normal", EnemySpecialCapability.StunStrike)]
        [TestCase("stage-2-1", 11, "normal", EnemySpecialCapability.AllyShield)]
        [TestCase("stage-2-3", 13, "elite", EnemySpecialCapability.AllyShield)]
        public void OnlyExistingSecondChapterRecommendedElevenPlusNonBossUsesSupportRole(
            string stage, int recommended, string encounter, EnemySpecialCapability expected)
        {
            var battle = Fixture("pulse-guard", stage, recommended, encounter);
            Assert.That(battle.EnemyCapabilityOf(Enemy(battle, "pulse-guard")), Is.EqualTo(expected));
        }

        [TestCase("pulse-guard", CombatCondition.Stun)]
        [TestCase("noise-wraith", CombatCondition.ArmorBreak)]
        public void FirstChapterStillDealsItsSingleHeavyStrikeAndOriginalControl(string id, CombatCondition condition)
        {
            var battle = Fixture(id, "stage-1-9", 9);
            battle.AdvanceRealtime(6000);
            var enemy = Enemy(battle, id);
            var hits = battle.Log.Where(e => e.Kind == BattleEventKind.Damage && e.ActorId == enemy.Id).ToArray();
            Assert.That(hits.Length, Is.EqualTo(1));
            Assert.That(hits[0].SkillId, Is.EqualTo("rt-enemy-heavy"));
            Assert.That(hits[0].Amount, Is.EqualTo(128), "160% of ATK100, then the captain's 20% cover");
            Assert.That(battle.ConditionRemaining(battle.FindUnit(hits[0].TargetId), condition), Is.GreaterThan(0));
            Assert.That(battle.Log.Any(e => e.SkillId == BattleSimulator.EnemyGuardShieldSkillId ||
                e.SkillId == BattleSimulator.EnemyTeamSweepSkillId), Is.False);
        }

        [Test]
        public void SecondChapterSweepHasFourRealDamageTargetsAndNoArmorBreakOrExtraHeavyStrike()
        {
            var battle = Fixture("noise-wraith"); var enemy = Enemy(battle, "noise-wraith");
            battle.AdvanceRealtime(4000);
            Assert.That(battle.EnemyCapabilityOf(enemy), Is.EqualTo(EnemySpecialCapability.TeamSweep));
            Assert.That(battle.EnemyCastIntent(enemy), Is.EqualTo("全攻"));
            Assert.That(battle.EnemyCastTargetDescription(enemy), Is.EqualTo("全队"));
            Assert.That(battle.EnemyThreatName(enemy), Does.Contain("全体"));
            battle.AdvanceRealtime(2000);
            var hits = battle.Log.Where(e => e.Kind == BattleEventKind.Damage && e.ActorId == enemy.Id).ToArray();
            Assert.That(hits.Length, Is.EqualTo(4));
            Assert.That(hits.Select(e => e.TargetId).Distinct().Count(), Is.EqualTo(4));
            Assert.That(hits.All(e => e.SkillId == BattleSimulator.EnemyTeamSweepSkillId), Is.True);
            Assert.That(hits.Select(e => e.Amount), Is.EqualTo(new[] { 32, 40, 40, 40 }),
                "40% each, with the captain's 20% cover on her");
            Assert.That(battle.Units.Where(u => u.Side == BattleSide.Player)
                .All(u => battle.ConditionRemaining(u, CombatCondition.ArmorBreak) == 0), Is.True);
            var skill = battle.LookupSkill(BattleSimulator.EnemyTeamSweepSkillId);
            Assert.That(skill.Pattern, Is.EqualTo(SkillPattern.All));
            Assert.That(skill.PowerPermille, Is.EqualTo(400));
        }

        [Test]
        public void GuardPreviewAndResolutionChooseLowestRatioThenRefreshOnlyTwoPercentForFourSeconds()
        {
            var battle = Fixture("pulse-guard", withAlly: true);
            var guard = Enemy(battle, "pulse-guard"); var ally = Enemy(battle, "echo-drone");
            guard.Hp = 4000; ally.Hp = 10000;
            battle.AdvanceRealtime(4000);
            Assert.That(battle.CastTarget(guard), Is.SameAs(guard), "40% beats 50%, despite lower raw HP");
            ally.Hp = 5000; // Falls to 25% during the warning; preview follows actual resolution.
            ally.Shield = 100; // An unrelated permanent shield must survive refresh and expiry.
            Assert.That(battle.CastTarget(guard), Is.SameAs(ally));
            Assert.That(battle.EnemyCastIntent(guard), Is.EqualTo("护盾"));
            Assert.That(battle.EnemyCastTargetDescription(guard), Is.EqualTo(ally.Definition.Name));
            battle.AdvanceRealtime(2000);
            var shield = battle.Log.Single(e => e.Kind == BattleEventKind.Shield && e.ActorId == guard.Id);
            Assert.That(shield.TargetId, Is.EqualTo(ally.Id));
            Assert.That(shield.Amount, Is.EqualTo(400), "2% of the protected ally's MAX HP20000");
            Assert.That(ally.Shield, Is.EqualTo(500));
            var skill = battle.LookupSkill(BattleSimulator.EnemyGuardShieldSkillId);
            Assert.That(skill.Effect, Is.EqualTo(SkillEffect.Shield));
            Assert.That(skill.PowerPermille, Is.EqualTo(20));
            typeof(BattleSimulator).GetMethod("ResolveTelegraphedSpecial", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(battle, new object[] { guard });
            Assert.That(ally.Shield, Is.EqualTo(500), "a repeated cast refreshes rather than stacking the timed layer");
            Assert.That(battle.Log.Any(e => e.ActorId == guard.Id &&
                (e.Kind == BattleEventKind.Damage || e.SkillId == "rt-stun")), Is.False);
            battle.AdvanceRealtime(3975); Assert.That(ally.Shield, Is.EqualTo(500));
            battle.AdvanceRealtime(25); Assert.That(ally.Shield, Is.EqualTo(100), "only the timed layer expires at 4000ms");
        }

        [TestCase("noise-wraith")]
        [TestCase("pulse-guard")]
        public void ExistingInterruptStillCancelsTheNewRoleRatherThanResolvingItEarly(string id)
        {
            var battle = Fixture(id); var enemy = Enemy(battle, id);
            battle.AdvanceRealtime(4000);
            Assert.That(battle.CastRemaining(enemy), Is.EqualTo(2000));
            Assert.That(battle.TryTacticalInterrupt(), Is.True);
            battle.AdvanceRealtime(2000);
            Assert.That(battle.CastRemaining(enemy), Is.Zero);
            Assert.That(battle.Log.Any(e => e.SkillId == BattleSimulator.EnemyGuardShieldSkillId ||
                e.SkillId == BattleSimulator.EnemyTeamSweepSkillId), Is.False);
        }

        [TestCase("pulse-guard", "boss")]
        [TestCase("noise-wraith", "boss")]
        [TestCase("pulse-guard", "world")]
        [TestCase("noise-wraith", "world")]
        public void AllBossEncounterRolesAndTheirRealOpeningSpecialRemainUnchanged(string id, string encounter)
        {
            var battle = Fixture(id, "stage-2-5", 15, encounter);
            var enemy = Enemy(battle, id);
            Assert.That(battle.EnemyCapabilityOf(enemy), Is.EqualTo(id == "pulse-guard"
                ? EnemySpecialCapability.StunStrike : EnemySpecialCapability.ArmorBreakStrike));
            battle.AdvanceRealtime(6000);
            Assert.That(battle.Log.Count(e => e.Kind == BattleEventKind.Damage && e.ActorId == enemy.Id), Is.EqualTo(1));
            Assert.That(battle.Log.Any(e => e.ActorId == enemy.Id && e.SkillId == "rt-enemy-heavy"), Is.True);
            Assert.That(battle.Log.Any(e => e.SkillId == BattleSimulator.EnemyGuardShieldSkillId ||
                e.SkillId == BattleSimulator.EnemyTeamSweepSkillId), Is.False);
        }

        private static BattleUnit Enemy(BattleSimulator battle, string id) =>
            battle.Units.Single(u => u.Side == BattleSide.Enemy && u.Definition.Id == id);

        private static BattleSimulator Fixture(string enemyId, string stageId = "stage-2-1", int recommended = 11,
            string encounter = "normal", bool withAlly = false)
        {
            var manifest = new TacticsManifest();
            manifest.Skills.Add(new SkillDefinition { Id = "basic", Name = "basic", Cooldown = 0 });
            UnitDefinition Unit(string id, int hp, int atk) => new UnitDefinition
            {
                Id = id, Name = id, MaxHp = hp, Attack = atk, Defense = 0,
                Speed = 1, CritPermille = 0, SkillIds = new List<string> { "basic" }
            };
            for (int i = 0; i < 4; i++) manifest.Units.Add(Unit("player" + i, 10000, 1));
            manifest.Units.Add(Unit(enemyId, 10000, 100));
            var stage = new StageDefinition { Id = stageId, RecommendedLevel = recommended,
                EncounterType = encounter, TimeLimitSeconds = 120 };
            stage.Enemies.Add(new EnemySpawn { UnitId = enemyId, Row = 0, Col = 0 });
            if (withAlly)
            {
                manifest.Units.Add(Unit("echo-drone", 20000, 100));
                stage.Enemies.Add(new EnemySpawn { UnitId = "echo-drone", Row = 1, Col = 0 });
            }
            var party = new List<PlayerUnitSetup>();
            for (int i = 0; i < 4; i++) party.Add(new PlayerUnitSetup { UnitId = "player" + i, Row = i % 3, Col = i / 3 });
            var battle = new BattleSimulator(manifest, stage, party,
                new ScriptedRandom(new[] { 999 }, new[] { 0, 1, 2, 3, 4, 5 }));
            // Healers avoid outgoing skill damage, isolating shield expiry without stopping model clocks.
            var races = Enumerable.Range(0, 4).ToDictionary(i => "player" + i, i => "海灵族 · 人鱼");
            battle.EnableRealtime(races, "player0",
                stage.RerollLimit, encounter == "world");
            battle.AutoCastUltimates = false;
            return battle;
        }
    }
}
