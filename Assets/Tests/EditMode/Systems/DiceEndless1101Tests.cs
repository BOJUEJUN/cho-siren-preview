using ChoSiren.Systems;
using ChoSiren.Systems.Dice;
using NUnit.Framework;

namespace ChoSiren.Tests.Systems
{
    /// <summary>2026-11-01c 无限骰子: 骰子豆 refill per action, store at most three, no per-battle total, bonus capped.</summary>
    public sealed class DiceEndless1101Tests
    {
        private static DiceTurn Endless(ulong seed = 7)
        {
            var dice = DiceTurn.ForBattle(new SeededRandom(seed), 2, true);
            dice.Begin();
            dice.EnableRhythm();
            return dice;
        }

        [Test]
        public void BattleStartsWithTwoPipsAndRefillsOnePerThreeActions()
        {
            DiceTurn dice = Endless();
            Assert.That(dice.StoredRerolls, Is.EqualTo(DiceTurn.StartingStoredRerolls));
            Assert.That(dice.StoredRerollCapacity, Is.EqualTo(DiceTurn.MaxStoredRerolls));
            Assert.That(dice.RerollAll(out string error), Is.True, error);
            Assert.That(dice.RerollAll(out error), Is.True, error);
            Assert.That(dice.StoredRerolls, Is.Zero);
            Assert.That(dice.CanReroll, Is.False);
            dice.ChargeForAction1101(6);
            dice.ChargeForAction1101(6);
            Assert.That(dice.StoredRerolls, Is.Zero);
            Assert.That(dice.NextRerollProgress, Is.EqualTo(.68f).Within(.01f), "环形进度 2 × 34%");
            dice.ChargeForAction1101(6);
            Assert.That(dice.StoredRerolls, Is.EqualTo(1), "三次出手回一颗骰子豆");
            Assert.That(dice.CanReroll, Is.True);
        }

        [Test]
        public void StoreNeverHoldsMoreThanThreeAndShowsAFullRing()
        {
            DiceTurn dice = Endless();
            for (int i = 0; i < 40; i++) dice.ChargeForAction1101(6);
            Assert.That(dice.StoredRerolls, Is.EqualTo(DiceTurn.MaxStoredRerolls));
            Assert.That(dice.AvailableRerolls, Is.EqualTo(DiceTurn.MaxStoredRerolls));
            Assert.That(dice.NextRerollProgress, Is.EqualTo(1f));
            dice.GrantRhythmReroll();
            Assert.That(dice.StoredRerolls, Is.EqualTo(DiceTurn.MaxStoredRerolls), "新一波也不能超过存储上限");
            Assert.That(dice.RerollAll(out _), Is.True);
            Assert.That(dice.NextRerollProgress, Is.Zero, "满存时溢出的充能不保留：花掉一颗才开始充下一颗");
        }

        [Test]
        public void ThereIsNoPerBattleTotalAnyMore()
        {
            DiceTurn dice = Endless(11);
            int rolled = 0;
            for (int round = 0; round < 60; round++)
            {
                for (int i = 0; i < 3; i++) dice.ChargeForAction1101(6);
                while (dice.CanReroll) { Assert.That(dice.RerollAll(out string error), Is.True, error); rolled++; }
                Assert.That(dice.AccumulatedBonusPermille, Is.LessThanOrEqualTo(DiceTurn.EndlessBonusCapPermille), "骰子加成封顶");
                Assert.That(dice.DamageMultiplierPermille, Is.LessThanOrEqualTo(1000 + DiceTurn.EndlessBonusCapPermille));
            }
            Assert.That(rolled, Is.GreaterThanOrEqualTo(60), "一场可以一直投");
            Assert.That(dice.UsedRerolls, Is.EqualTo(rolled));
        }

        [Test]
        public void PickedDiceStillCapAtTwoAndNoneMeansAllSix()
        {
            DiceTurn dice = Endless(3);
            Assert.That(dice.ToggleHold(0), Is.False);
            Assert.That(dice.ToggleHold(1), Is.False);
            Assert.That(dice.CanToggleHold(2), Is.False, "每次最多选 2 颗");
            Assert.That(dice.RerollUnheld(out string error), Is.True, error);
            Assert.That(dice.Rolled, Is.EqualTo(new[] { true, true, false, false, false, false }));
            Assert.That(dice.RerollAll(out error), Is.True, error);
            Assert.That(dice.Rolled, Has.All.True, "不选 = 6 颗全部重投");
        }

        [Test]
        public void NoDiceControlGroupNeverGetsPips()
        {
            var dice = DiceTurn.ForBattle(new SeededRandom(5), 2, true);
            dice.Begin();
            dice.NeutralizeForBalanceAudit1101();
            dice.EnableRhythm();
            for (int i = 0; i < 20; i++) dice.ChargeForAction1101(6);
            dice.GrantRhythmReroll();
            Assert.That(dice.StoredRerolls, Is.Zero);
            Assert.That(dice.CanReroll, Is.False);
        }
    }
}
