using ChoSiren.Systems;
using ChoSiren.Systems.Dice;
using NUnit.Framework;

namespace ChoSiren.Tests.Systems
{
    public sealed class DiceTests
    {
        [Test]
        public void WeakerNewHandStillAddsToExistingBattleBonus()
        {
            var dice = DiceTurn.ForBattle(new ScriptedRandom(new[] { 0 }, new[] { 5, 5, 5, 5, 5, 5, 0, 0, 2, 3, 4, 5 }), 2, false);
            dice.Begin(); dice.GainEnergy(100);
            Assert.That(dice.EnergyRerollAll(out _), Is.True);
            // 2026-10-31 (用户): 全部重投 rolls all six again (1,1,3,4,5,6): 六同 → 一对; a weaker hand still adds
            // at least +5% on top of the opening 六同's +20%.
            Assert.That(dice.Hand.Pattern, Is.EqualTo(DicePattern.Pair));
            Assert.That(dice.DamageMultiplierPermille, Is.EqualTo(1000 + 200 + 50));
        }

        [Test]
        public void BattleBonusAccumulatesAdditivelyCapsAndResetsForNewBattle()
        {
            var dice = DiceTurn.ForBattle(new ScriptedRandom(new[] { 0 }, new[] { 5 }), 5, false);
            dice.Begin();
            Assert.That(dice.DamageMultiplierPermille, Is.EqualTo(1200));
            Assert.That(dice.BattleRerollLimit, Is.EqualTo(DiceTurn.MaxRerollsPerBattle), "每局最多 3 次");
            for (int i = 1; i <= DiceTurn.MaxRerollsPerBattle; i++)
            {
                dice.GainEnergy(100);
                Assert.That(dice.EnergyRerollAll(out _), Is.True);
                Assert.That(dice.AccumulatedBonusPermille, Is.EqualTo(200 * (i + 1)));
                Assert.That(dice.LastBonusGainPermille, Is.EqualTo(200));
                int revision = dice.Revision;
                dice.Begin();
                dice.SetBattleSelectiveReroll(true);
                Assert.That(dice.Revision, Is.EqualTo(revision));
            }
            Assert.That(dice.DamageMultiplierPermille, Is.EqualTo(1800));
            Assert.That(dice.EnergyRerollAll(out _), Is.False);
            var fresh = DiceTurn.ForBattle(new ScriptedRandom(new[] { 0 }, new[] { 0, 0, 2, 3, 4, 5, 0, 0, 2, 3, 4, 5 }), 2, false);
            fresh.Begin();
            Assert.That(fresh.AccumulatedBonusPermille, Is.EqualTo(25));
            fresh.GrantFreeReroll();
            Assert.That(fresh.EnergyRerollAll(out _), Is.True);
            Assert.That(fresh.LastBonusGainPermille, Is.GreaterThanOrEqualTo(50), "a reroll always progresses by at least 5%");
            Assert.That(fresh.DamageMultiplierPermille, Is.EqualTo(1000 + 25 + fresh.LastBonusGainPermille));
        }
        [TestCase(new[] { 1, 2, 3, 4, 6 }, DicePattern.HighPoint, 1000, 16, 6,
            new[] { false, false, false, false, true })]
        [TestCase(new[] { 1, 1, 3, 4, 6 }, DicePattern.Pair, 1100, 15, 2,
            new[] { true, true, false, false, false })]
        [TestCase(new[] { 1, 1, 3, 3, 6 }, DicePattern.TwoPair, 1200, 14, 8,
            new[] { true, true, true, true, false })]
        [TestCase(new[] { 2, 2, 2, 4, 6 }, DicePattern.ThreeKind, 1300, 16, 6,
            new[] { true, true, true, false, false })]
        [TestCase(new[] { 1, 2, 3, 4, 5 }, DicePattern.Straight, 1350, 15, 15,
            new[] { true, true, true, true, true })]
        [TestCase(new[] { 2, 3, 4, 5, 6 }, DicePattern.Straight, 1350, 20, 20,
            new[] { true, true, true, true, true })]
        [TestCase(new[] { 2, 2, 2, 5, 5 }, DicePattern.FullHouse, 1450, 16, 16,
            new[] { true, true, true, true, true })]
        [TestCase(new[] { 4, 4, 4, 4, 6 }, DicePattern.FourKind, 1600, 22, 16,
            new[] { true, true, true, true, false })]
        [TestCase(new[] { 6, 6, 6, 6, 6 }, DicePattern.FiveKind, 1750, 30, 30,
            new[] { true, true, true, true, true })]
        public void EvaluateRecognizesEveryPattern(int[] values, DicePattern expected, int multiplier,
            int pipTotal, int participatingPipTotal, bool[] participating)
        {
            DiceHand hand = DiceRules.Evaluate(values);

            Assert.That(hand.Pattern, Is.EqualTo(expected));
            Assert.That(hand.MultiplierPermille, Is.EqualTo(multiplier));
            Assert.That(hand.DisplayName, Is.Not.Empty);
            Assert.That(hand.Values, Is.EqualTo(values));
            Assert.That(hand.PipTotal, Is.EqualTo(pipTotal));
            Assert.That(hand.ParticipatingPipTotal, Is.EqualTo(participatingPipTotal));
            Assert.That(hand.Participating, Is.EqualTo(participating));
        }

        [TestCase(new[] { 2, 2, 4, 5, 6 }, new[] { true, true, false, false, false },
            TestName = "Planner_PairTakesPriorityOverLongRun")]
        [TestCase(new[] { 1, 1, 3, 3, 6 }, new[] { true, true, true, true, false },
            TestName = "Planner_KeepsBothPairs")]
        [TestCase(new[] { 2, 2, 2, 4, 6 }, new[] { true, true, true, false, false },
            TestName = "Planner_KeepsThreeKind")]
        [TestCase(new[] { 1, 2, 4, 5, 6 }, new[] { false, false, true, true, true },
            TestName = "Planner_ChoosesLongestHighRun")]
        [TestCase(new[] { 1, 2, 3, 5, 6 }, new[] { true, true, true, false, false },
            TestName = "Planner_ChoosesLongestLowRun")]
        [TestCase(new[] { 1, 2, 3, 4, 5 }, new[] { true, true, true, true, true },
            TestName = "Planner_DoesNotBreakStraight")]
        [TestCase(new[] { 2, 2, 2, 5, 5 }, new[] { true, true, true, true, true },
            TestName = "Planner_DoesNotBreakFullHouse")]
        [TestCase(new[] { 4, 4, 4, 4, 6 }, new[] { true, true, true, true, false },
            TestName = "Planner_KeepsFourKind")]
        [TestCase(new[] { 6, 6, 6, 6, 6 }, new[] { true, true, true, true, true },
            TestName = "Planner_DoesNotBreakFiveKind")]
        public void HoldPlannerChoosesDeterministicBestKeep(int[] values, bool[] expected)
        {
            bool[] first = DiceHoldPlanner.Choose(values);
            bool[] second = DiceHoldPlanner.Choose(values);

            Assert.That(first, Is.EqualTo(expected));
            Assert.That(second, Is.EqualTo(expected));
        }

        [Test]
        public void NormalRerollPreservesHeldDiceAndConsumesOneUse()
        {
            var turn = new DiceTurn(new ScriptedRandom(new[] { 0 },
                new[] { 0, 1, 2, 3, 4, 5, 5, 5, 5, 5 }));
            turn.Begin();
            turn.ToggleHold(0);
            turn.ToggleHold(4);

            Assert.That(turn.RerollUnheld(out string error), Is.True, error);
            Assert.That(turn.Values, Is.EqualTo(new[] { 1, 6, 6, 6, 5, 6 }));
            Assert.That(turn.Held, Is.EqualTo(new[] { true, false, false, false, true, false }));
            Assert.That(turn.RerollsRemaining, Is.EqualTo(1));
            Assert.That(turn.Hand.Pattern, Is.EqualTo(DicePattern.FourKind));
        }

        [Test]
        public void EnergyRerollRequiresFullEnergyThenRollsEverythingAndClearsEnergy()
        {
            var turn = new DiceTurn(new ScriptedRandom(new[] { 0 },
                new[] { 0, 0, 0, 0, 0, 0, 5, 4, 3, 2, 1, 0 }), 99);
            turn.Begin();
            turn.ToggleHold(0);

            Assert.That(turn.EnergyRerollAll(out string error), Is.False);
            Assert.That(error, Does.Contain("100"));
            turn.GainEnergy(1);
            Assert.That(turn.CanEnergyReroll, Is.True);
            Assert.That(turn.EnergyRerollAll(out error), Is.True, error);

            Assert.That(turn.Values, Is.EqualTo(new[] { 6, 5, 4, 3, 2, 1 }));
            Assert.That(turn.Held, Is.EqualTo(new[] { false, false, false, false, false, false }));
            Assert.That(turn.Energy, Is.Zero);
            Assert.That(turn.RerollsRemaining, Is.EqualTo(2), "能量重投不应消耗普通重投次数");
            Assert.That(turn.Hand.Pattern, Is.EqualTo(DicePattern.SixStraight));
        }

        [Test]
        public void AllHeldDiceDoNotConsumeAReroll()
        {
            var turn = new DiceTurn(new ScriptedRandom(new[] { 0 }, new[] { 0, 1, 2, 3, 4 }));
            turn.Begin();
            for (int index = 0; index < DiceRules.DiceCount; index++) turn.ToggleHold(index);

            Assert.That(turn.RerollUnheld(out string error), Is.False);
            Assert.That(error, Does.Contain("取消保留"));
            Assert.That(turn.RerollsRemaining, Is.EqualTo(2));
        }
    }
}
