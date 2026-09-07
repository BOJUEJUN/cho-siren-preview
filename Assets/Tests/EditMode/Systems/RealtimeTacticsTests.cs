using System.Linq;
using System.Reflection;
using ChoSiren.Systems.Tactics;
using NUnit.Framework;

namespace ChoSiren.Tests
{
    public sealed class RealtimeTacticsTests
    {
        private static bool Apply(BattleSimulator b, BattleUnit from, BattleUnit to, CombatCondition c, int ms) =>
            (bool)typeof(BattleSimulator).GetMethod("ApplyCondition", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(b, new object[] { from, to, c, ms });

        [Test]
        public void EnemyTelegraphsBeforeDamageAndManualInterruptCancelsThisCast()
        {
            var b = RealtimeBattleTests.Create("none", encounter: "normal");
            Assert.That(b.TryTacticalInterrupt(), Is.False);
            Assert.That(b.InterruptCooldownRemaining, Is.Zero);
            b.AdvanceRealtime(4000);
            Assert.That(b.CastRemaining(b.FindUnit(2)), Is.EqualTo(2000));
            Assert.That(b.Log.Any(e => e.SkillId == "rt-enemy-heavy"), Is.False);
            Assert.That(b.TryTacticalInterrupt(), Is.True);
            Assert.That(b.CastRemaining(b.FindUnit(2)), Is.Zero);
            Assert.That(b.ConditionRemaining(b.FindUnit(2), CombatCondition.Stun), Is.EqualTo(1200));
            Assert.That(b.InterruptCooldownRemaining, Is.EqualTo(12000));
            Assert.That(b.TryTacticalInterrupt(), Is.False);
            b.AdvanceRealtime(2500);
            Assert.That(b.Log.Any(e => e.SkillId == "rt-enemy-heavy"), Is.False);
            Assert.That(b.Log.Any(e => e.SkillId == "rt-interrupt"), Is.True);
        }

        [Test]
        public void UninterruptedCastActuallyDealsDamageAfterTwoSeconds()
        {
            var b = RealtimeBattleTests.Create("none", encounter: "normal");
            b.AdvanceRealtime(5975);
            Assert.That(b.Log.Any(e => e.Kind == BattleEventKind.Damage && e.SkillId == "rt-enemy-heavy"), Is.False);
            b.AdvanceRealtime(25);
            Assert.That(b.Log.Single(e => e.Kind == BattleEventKind.Damage && e.SkillId == "rt-enemy-heavy").TimeMilliseconds, Is.EqualTo(6000));
        }

        [Test]
        public void StunStopsRealAttacksExpiresAndCannotChainRefresh()
        {
            var b = RealtimeBattleTests.Create("none");
            var p = b.FindUnit(1); var enemy = b.FindUnit(2);
            Assert.That(Apply(b, enemy, p, CombatCondition.Stun, 1200), Is.True);
            Assert.That(Apply(b, enemy, p, CombatCondition.Stun, 1200), Is.False);
            b.AdvanceRealtime(1200);
            Assert.That(b.Log.Any(e => e.ActorId == 1 && e.Kind == BattleEventKind.ActionStarted), Is.False);
            Assert.That(b.ConditionRemaining(p, CombatCondition.Stun), Is.Zero);
            b.AdvanceRealtime(200);
            Assert.That(b.Log.Any(e => e.ActorId == 1 && e.Kind == BattleEventKind.ActionStarted), Is.True);
            Assert.That(Apply(b, enemy, p, CombatCondition.Stun, 1200), Is.False);
            b.AdvanceRealtime(2800);
            Assert.That(Apply(b, enemy, p, CombatCondition.Stun, 1200), Is.True);
        }

        [Test]
        public void BossStunIsShorterAndWardRejectsControlWithoutCancellingAValidCast()
        {
            var b = RealtimeBattleTests.Create("none", encounter: "boss");
            var boss = b.FindUnit(2); var p = b.FindUnit(1);
            Apply(b, p, boss, CombatCondition.Stun, 1200);
            Assert.That(b.ConditionRemaining(boss, CombatCondition.Stun), Is.EqualTo(600));
            Apply(b, p, p, CombatCondition.ControlWard, 2000);
            Assert.That(Apply(b, boss, p, CombatCondition.Stun, 1200), Is.False);
            Assert.That(b.ConditionRemaining(p, CombatCondition.Stun), Is.Zero);
        }

        [Test]
        public void ArmorBreakChangesActualDefenseWithoutPermanentlyChangingBaseStats()
        {
            var b = RealtimeBattleTests.Create("none");
            var enemy = b.FindUnit(2); enemy.BaseDefense = 100;
            Apply(b, b.FindUnit(1), enemy, CombatCondition.ArmorBreak, 1500);
            Assert.That(b.EffectiveRealtimeDefense(enemy), Is.EqualTo(70));
            Assert.That(enemy.BaseDefense, Is.EqualTo(100));
            b.AdvanceRealtime(1500);
            Assert.That(b.EffectiveRealtimeDefense(enemy), Is.EqualTo(100));
        }

        [Test]
        public void SlowReducesAttackFrequencyAndHealingCleansesOneEffect()
        {
            var normal = RealtimeBattleTests.Create("none");
            var slow = RealtimeBattleTests.Create("none");
            Apply(slow, slow.FindUnit(2), slow.FindUnit(1), CombatCondition.Slow, 5000);
            normal.AdvanceRealtime(5000); slow.AdvanceRealtime(5000);
            int Basics(BattleSimulator b) => b.Log.Count(e => e.ActorId == 1 && e.Kind == BattleEventKind.ActionStarted && e.SkillId == "rt-basic");
            Assert.That(Basics(slow), Is.LessThan(Basics(normal)));
            var healer = RealtimeBattleTests.Create("mermaid");
            var p = healer.FindUnit(1); p.Hp -= 10000;
            Apply(healer, healer.FindUnit(2), p, CombatCondition.ArmorBreak, 5000);
            Apply(healer, healer.FindUnit(2), p, CombatCondition.Slow, 5000);
            healer.AdvanceRealtime(3000);
            Assert.That(healer.ConditionRemaining(p, CombatCondition.ArmorBreak), Is.Zero);
            Assert.That(healer.ConditionRemaining(p, CombatCondition.Slow), Is.GreaterThan(0));
            Assert.That(healer.Log.Any(e => e.SkillId == "rt-cleanse"), Is.True);
            Assert.That(p.Hp, Is.GreaterThan(90000));
        }

        [TestCase("echo-drone", CombatCondition.Slow)]
        [TestCase("noise-wraith", CombatCondition.ArmorBreak)]
        [TestCase("static-golem", CombatCondition.Stun)]
        public void EnemyRolesApplyTheirAdvertisedMechanics(string id, CombatCondition condition)
        {
            var b = RealtimeBattleTests.Create("none", encounter: "normal");
            b.FindUnit(2).Definition.Id = id;
            b.AdvanceRealtime(6000);
            Assert.That(b.ConditionRemaining(b.FindUnit(1), condition), Is.GreaterThan(0));
        }

        [Test]
        public void EnemyHealerRestoresWeakestAllyInsteadOfDamagingPlayer()
        {
            var b = RealtimeBattleTests.Create("none", encounter: "normal");
            var enemy = b.FindUnit(2); enemy.Definition.Id = "velvet-hexer"; enemy.Hp /= 2;
            b.AdvanceRealtime(6000);
            Assert.That(b.Log.Any(e => e.ActorId == enemy.Id && e.Kind == BattleEventKind.Heal && e.TargetId == enemy.Id), Is.True);
            Assert.That(b.Log.Any(e => e.SkillId == "rt-enemy-heavy"), Is.False);
        }
    }
}
