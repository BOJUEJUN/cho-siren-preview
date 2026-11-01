using System;
using System.Linq;
using ChoSiren.Systems;
using ChoSiren.Systems.Dice;
using ChoSiren.Systems.Tactics;
using NUnit.Framework;

namespace ChoSiren.Tests.Systems
{
    public sealed class SixDice928Tests
    {
        [TestCase(new[] {1,1,3,3,5,5}, DicePattern.ThreePair, 1250, 6)]
        [TestCase(new[] {2,2,2,5,5,5}, DicePattern.DoubleThreeKind, 1500, 6)]
        [TestCase(new[] {6,2,4,1,3,5}, DicePattern.SixStraight, 1550, 6)]
        [TestCase(new[] {6,6,6,6,6,6}, DicePattern.SixKind, 1800, 6)]
        [TestCase(new[] {6,6,6,6,6,1}, DicePattern.FiveKind, 1750, 5)]
        [TestCase(new[] {4,4,4,4,2,2}, DicePattern.FourKind, 1600, 4)]
        [TestCase(new[] {3,3,3,2,2,6}, DicePattern.FullHouse, 1450, 5)]
        [TestCase(new[] {1,2,3,4,5,5}, DicePattern.Straight, 1350, 5)]
        public void SixDiceScoreAndParticipation(int[] values, DicePattern pattern, int score, int participating)
        {
            var hand = DiceRules.Evaluate(values);
            Assert.That(hand.Pattern, Is.EqualTo(pattern));
            Assert.That(hand.MultiplierPermille, Is.EqualTo(score));
            Assert.That(hand.Participating.Count(x => x), Is.EqualTo(participating));
            Assert.That(DiceHoldPlanner.Choose(values), Is.EqualTo(hand.Participating));
        }

        [Test]
        public void ExhaustiveSixDiceHandsAreValidAndPermutationIndependent()
        {
            var values = new int[6];
            for (int code = 0; code < 46656; code++)
            {
                int n = code;
                for (int i = 0; i < 6; i++) { values[i] = n % 6 + 1; n /= 6; }
                var hand = DiceRules.Evaluate(values);
                var reversed = DiceRules.Evaluate(values.Reverse().ToArray());
                Assert.That(hand.Pattern, Is.EqualTo(reversed.Pattern));
                Assert.That(hand.MultiplierPermille, Is.InRange(1000, 2000));
                Assert.That(hand.ParticipatingPipTotal, Is.EqualTo(reversed.ParticipatingPipTotal));
                Assert.That(hand.Values.Count, Is.EqualTo(6));
            }
        }

        [Test]
        public void SixthDieRerollKeepsFiveOthersAndSpendsExactlyOneCharge()
        {
            var dice = DiceTurn.ForBattle(new ScriptedRandom(new[] {0},new[] {0,1,2,3,4,5,0}), 3, true);
            dice.Begin(); var first = dice.Values.ToArray();
            dice.ToggleHold(5); dice.GainEnergy(100);
            Assert.That(dice.RerollUnheld(out var error), Is.True, error);
            Assert.That(dice.Values.Take(5), Is.EqualTo(first.Take(5)));
            Assert.That(dice.Values[5], Is.EqualTo(1));
            Assert.That(dice.UsedRerolls, Is.EqualTo(1));
            Assert.That(dice.Held.All(x => x), Is.True);
            Assert.That(dice.RerollUnheld(out _), Is.False);
        }

        [Test]
        public void NewPatternsUseCaptainTablesAndKeepDamageCap()
        {
            // 2026-10-31 (赵金 "我方过强"): toned-down captain tables; 2026-11-01b (骰子加成约 +10%): 毒 6 → 4, 护盾 6% → 3%
            // (the mermaid wall refreshes every round now). 2026-11-01c 无限骰子: passives stop at 四条 (六同 毒 = 四条 3 层),
            // 六连顺 wall 3% → 2%.
            Assert.That(BattleSimulator.PoisonLayers(DicePattern.SixKind), Is.EqualTo(BattleSimulator.PoisonLayers(DicePattern.FourKind)));
            Assert.That(BattleSimulator.PoisonLayers(DicePattern.SixKind), Is.EqualTo(3));
            Assert.That(BattleSimulator.Pierce(DicePattern.DoubleThreeKind), Is.EqualTo(200));
            Assert.That(BattleSimulator.ShieldPercent(DicePattern.SixStraight), Is.EqualTo(20));
            var dice = DiceTurn.ForBattle(new ScriptedRandom(new[] {0}, new[] {5}), 5, false);
            dice.Begin();
            for (int i = 0; i < DiceTurn.MaxRerollsPerBattle; i++) { dice.GainEnergy(100); Assert.That(dice.RerollAll(out _), Is.True); }
            Assert.That(dice.DamageMultiplierPermille, Is.EqualTo(1800));
            Assert.That(dice.RerollAll(out _), Is.False);
        }
    }
}
