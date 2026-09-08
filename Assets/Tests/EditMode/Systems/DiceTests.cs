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
            var dice = DiceTurn.ForBattle(new ScriptedRandom(new[] { 0 }, new[] { 5, 5, 5, 5, 5, 0, 1, 2, 3, 5 }), 2, false);
            dice.Begin(); dice.GainEnergy(100);
            Assert.That(dice.EnergyRerollAll(out _), Is.True);
            Assert.That(dice.Hand.Pattern, Is.EqualTo(DicePattern.HighPoint));
            Assert.That(dice.DamageMultiplierPermille, Is.EqualTo(1300));
        }

        [Test]
        public void BattleBonusAccumulatesAdditivelyCapsAndResetsForNewBattle()
        {
            var dice = DiceTurn.ForBattle(new ScriptedRandom(new[] { 0 }, new[] { 5 }), 5, false);
            dice.Begin();
            Assert.That(dice.DamageMultiplierPermille, Is.EqualTo(1250));
            for (int i = 1; i <= 5; i++)
            {
                dice.GainEnergy(100);
                Assert.That(dice.EnergyRerollAll(out _), Is.True);
                Assert.That(dice.AccumulatedBonusPermille, Is.EqualTo(System.Math.Min(1000, 250 * (i + 1))));
                Assert.That(dice.LastBonusGainPermille, Is.EqualTo(i < 4 ? 250 : 0));
                int revision = dice.Revision;
                dice.Begin();
                dice.SetBattleSelectiveReroll(true);
                Assert.That(dice.Revision, Is.EqualTo(revision));
            }
            Assert.That(dice.DamageMultiplierPermille, Is.EqualTo(2000));
            Assert.That(dice.EnergyRerollAll(out _), Is.False);
            var fresh = DiceTurn.ForBattle(new ScriptedRandom(new[] { 0 }, new[] { 0, 1, 2, 3, 5, 0, 1, 2, 3, 5 }), 2, false);
            fresh.Begin();
            Assert.That(fresh.AccumulatedBonusPermille, Is.Zero);
            fresh.GrantFreeReroll();
            Assert.That(fresh.EnergyRerollAll(out _), Is.True);
            Assert.That(fresh.LastBonusGainPermille, Is.EqualTo(50));
            Assert.That(fresh.DamageMultiplierPermille, Is.EqualTo(1050));
        }
        [TestCase(new[] { 1, 2, 3, 4, 6 }, DicePattern.HighPoint, 1000, 16, 6,
            new[] { false, false, false, false, true })]
        [TestCase(new[] { 1, 1, 3, 4, 6 }, DicePattern.Pair, 1150, 15, 2,
            new[] { true, true, false, false, false })]
        [TestCase(new[] { 1, 1, 3, 3, 6 }, DicePattern.TwoPair, 1250, 14, 8,
            new[] { true, true, true, true, false })]
        [TestCase(new[] { 2, 2, 2, 4, 6 }, DicePattern.ThreeKind, 1400, 16, 6,
            new[] { true, true, true, false, false })]
        [TestCase(new[] { 1, 2, 3, 4, 5 }, DicePattern.Straight, 1450, 15, 15,
            new[] { true, true, true, true, true })]
        [TestCase(new[] { 2, 3, 4, 5, 6 }, DicePattern.Straight, 1450, 20, 20,
            new[] { true, true, true, true, true })]
        [TestCase(new[] { 2, 2, 2, 5, 5 }, DicePattern.FullHouse, 1600, 16, 16,
            new[] { true, true, true, true, true })]
        [TestCase(new[] { 4, 4, 4, 4, 6 }, DicePattern.FourKind, 1800, 22, 16,
            new[] { true, true, true, true, false })]
        [TestCase(new[] { 6, 6, 6, 6, 6 }, DicePattern.FiveKind, 2000, 30, 30,
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
