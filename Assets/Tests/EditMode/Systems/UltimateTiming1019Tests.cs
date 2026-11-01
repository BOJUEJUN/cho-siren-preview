using System.Linq;
using ChoSiren.Systems.Tactics;
using NUnit.Framework;

namespace ChoSiren.Tests
{
    public sealed class UltimateTiming1019Tests
    {
        [TestCase(.7f, UltimateTimingGrade.Perfect)]
        [TestCase(.59f, UltimateTimingGrade.Good)]
        [TestCase(.05f, UltimateTimingGrade.Miss)]
        public void TimingHasDistinctBoundedWindows(float progress, UltimateTimingGrade expected)
        {
            var timing = new UltimateTimingChallenge();
            timing.Advance(UltimateTimingChallenge.DurationSeconds * progress);
            Assert.That(timing.Hit(), Is.EqualTo(expected));
            timing.Advance(100);
            Assert.That(timing.Hit(), Is.EqualTo(expected), "repeated input cannot reroll a timing result");
        }

        [Test]
        public void ExpirationAndCancellationNeverTurnIntoAReward()
        {
            var expired = new UltimateTimingChallenge(); expired.Advance(2);
            Assert.That(expired.Resolved, Is.True);
            Assert.That(expired.Hit(), Is.EqualTo(UltimateTimingGrade.Miss));
            var cancelled = new UltimateTimingChallenge(); cancelled.Advance(.7f); cancelled.Cancel();
            Assert.That(cancelled.Cancelled, Is.True);
            Assert.That(cancelled.Hit(), Is.EqualTo(UltimateTimingGrade.Miss));
            Assert.That(UltimateTimingChallenge.BonusPermille((UltimateTimingGrade)999), Is.Zero);
        }

        [TestCase(UltimateTimingGrade.Miss, 1000)]
        [TestCase(UltimateTimingGrade.Good, 1050)]
        [TestCase(UltimateTimingGrade.Perfect, 1100)]
        public void ManualBonusChangesActualUltimateDamageOnceAndNeverLeaksIntoBasics(UltimateTimingGrade grade, int multiplier)
        {
            BattleSimulator baseline = Ready(); BattleSimulator timed = Ready();
            int baselineStart = baseline.Log.Count, timedStart = timed.Log.Count;
            Assert.That(baseline.TryCastUltimate(1), Is.True);
            Assert.That(timed.TryCastUltimateWithTiming(1, grade), Is.True);
            long normalDamage = UltimateDamage(baseline, baselineStart);
            long timingDamage = UltimateDamage(timed, timedStart);
            Assert.That(normalDamage, Is.GreaterThan(0));
            Assert.That(timingDamage, Is.EqualTo(normalDamage * multiplier / 1000), "read capped real HP/shield damage, not UI labels");
            Assert.That(timed.TryCastUltimateWithTiming(1, UltimateTimingGrade.Perfect), Is.False, "one energy charge casts only once");
            Assert.That(timed.EnergyOf(timed.FindUnit(1)), Is.Zero);
            baseline.AdvanceRealtime(1000); timed.AdvanceRealtime(1000);
            long normalBasic = baseline.Log.Last(e => e.Kind == BattleEventKind.Damage && e.ActorId == 1 && e.SkillId == "rt-basic").Amount;
            long timingBasic = timed.Log.Last(e => e.Kind == BattleEventKind.Damage && e.ActorId == 1 && e.SkillId == "rt-basic").Amount;
            Assert.That(timingBasic, Is.EqualTo(normalBasic), "timing bonus is cleared immediately after the cast");
        }

        [Test]
        public void AutoModeAndInvalidCasterCannotClaimTimingReward()
        {
            BattleSimulator battle = Ready(); int energy = battle.EnergyOf(battle.FindUnit(1));
            battle.AutoCastUltimates = true;
            Assert.That(battle.TryCastUltimateWithTiming(1, UltimateTimingGrade.Perfect), Is.False);
            Assert.That(battle.EnergyOf(battle.FindUnit(1)), Is.EqualTo(energy));
            battle.AutoCastUltimates = false;
            Assert.That(battle.TryCastUltimateWithTiming(999, UltimateTimingGrade.Perfect), Is.False);
            battle.AutoCastUltimates = true;
            Assert.That(battle.TryCastUltimate(1), Is.True, "normal auto release still uses the existing path");
        }

        [TestCase(UltimateTimingGrade.Good, 1050)]
        [TestCase(UltimateTimingGrade.Perfect, 1100)]
        public void SupportUltimateAmplifiesOnlyItsActualShieldAmount(UltimateTimingGrade grade, int multiplier)
        {
            BattleSimulator baseline = Ready("mermaid"), timed = Ready("mermaid");
            Assert.That(baseline.TryCastUltimate(1), Is.True);
            Assert.That(timed.TryCastUltimateWithTiming(1, grade), Is.True);
            int normalShield = baseline.Log.Last(e => e.Kind == BattleEventKind.Shield && e.ActorId == 1 && e.SkillId == "rt-mermaid-big").Amount;
            int timedShield = timed.Log.Last(e => e.Kind == BattleEventKind.Shield && e.ActorId == 1 && e.SkillId == "rt-mermaid-big").Amount;
            Assert.That(timedShield, Is.EqualTo(normalShield * multiplier / 1000), "permanent dice shields are not amplified");
            Assert.That(timed.FindUnit(1).Shield, Is.LessThanOrEqualTo(timed.FindUnit(1).MaxHp));
            Assert.That(timed.TryCastUltimateWithTiming(1, grade), Is.False);
        }

        private static BattleSimulator Ready(string race = "charm")
        {
            var battle = RealtimeBattleTests.Create(race, enemyHp: 100000000, attack: 1000, encounter: "normal");
            battle.AutoCastUltimates = false; battle.AdvanceRealtime(30000);
            Assert.That(battle.IsUltimateReady(battle.FindUnit(1)), Is.True);
            return battle;
        }
        private static long UltimateDamage(BattleSimulator battle, int start) => battle.Log.Skip(start)
            .Where(e => e.Kind == BattleEventKind.Damage && e.ActorId == 1 && e.SkillId == "rt-charm-big")
            .Sum(e => (long)e.HpLostAmount + e.AbsorbedAmount);
    }
}
