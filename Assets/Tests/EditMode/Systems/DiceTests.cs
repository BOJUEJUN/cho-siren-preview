using ChoSiren.Systems;
using ChoSiren.Systems.Dice;
using NUnit.Framework;

namespace ChoSiren.Tests.Systems
{
    public sealed class DiceTests
    {
        [TestCase(new[] { 1, 2, 3, 4, 6 }, DicePattern.HighPoint, 1000, 16, 6,
            new[] { false, false, false, false, true })]
        [TestCase(new[] { 1, 1, 3, 4, 6 }, DicePattern.Pair, 1500, 15, 2,
            new[] { true, true, false, false, false })]
        [TestCase(new[] { 1, 1, 3, 3, 6 }, DicePattern.TwoPair, 2000, 14, 8,
            new[] { true, true, true, true, false })]
        [TestCase(new[] { 2, 2, 2, 4, 6 }, DicePattern.ThreeKind, 2500, 16, 6,
            new[] { true, true, true, false, false })]
        [TestCase(new[] { 1, 2, 3, 4, 5 }, DicePattern.Straight, 3000, 15, 15,
            new[] { true, true, true, true, true })]
        [TestCase(new[] { 2, 3, 4, 5, 6 }, DicePattern.Straight, 3000, 20, 20,
            new[] { true, true, true, true, true })]
        [TestCase(new[] { 2, 2, 2, 5, 5 }, DicePattern.FullHouse, 4900, 16, 16,
            new[] { true, true, true, true, true })]
        [TestCase(new[] { 4, 4, 4, 4, 6 }, DicePattern.FourKind, 5000, 22, 16,
            new[] { true, true, true, true, false })]
        [TestCase(new[] { 6, 6, 6, 6, 6 }, DicePattern.FiveKind, 10000, 30, 30,
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
                new[] { 0, 1, 2, 3, 4, 5, 5, 5 }));
            turn.Begin();
            turn.ToggleHold(0);
            turn.ToggleHold(4);

            Assert.That(turn.RerollUnheld(out string error), Is.True, error);
            Assert.That(turn.Values, Is.EqualTo(new[] { 1, 6, 6, 6, 5 }));
            Assert.That(turn.Held, Is.EqualTo(new[] { true, false, false, false, true }));
            Assert.That(turn.RerollsRemaining, Is.EqualTo(1));
            Assert.That(turn.Hand.Pattern, Is.EqualTo(DicePattern.ThreeKind));
        }

        [Test]
        public void EnergyRerollRequiresFullEnergyThenRollsEverythingAndClearsEnergy()
        {
            var turn = new DiceTurn(new ScriptedRandom(new[] { 0 },
                new[] { 0, 0, 0, 0, 0, 5, 4, 3, 2, 1 }), 99);
            turn.Begin();
            turn.ToggleHold(0);

            Assert.That(turn.EnergyRerollAll(out string error), Is.False);
            Assert.That(error, Does.Contain("100"));
            turn.GainEnergy(1);
            Assert.That(turn.CanEnergyReroll, Is.True);
            Assert.That(turn.EnergyRerollAll(out error), Is.True, error);

            Assert.That(turn.Values, Is.EqualTo(new[] { 6, 5, 4, 3, 2 }));
            Assert.That(turn.Held, Is.EqualTo(new[] { false, false, false, false, false }));
            Assert.That(turn.Energy, Is.Zero);
            Assert.That(turn.RerollsRemaining, Is.EqualTo(2), "能量重投不应消耗普通重投次数");
            Assert.That(turn.Hand.Pattern, Is.EqualTo(DicePattern.Straight));
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
