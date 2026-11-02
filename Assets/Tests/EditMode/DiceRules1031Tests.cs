using System;
using System.Collections.Generic;
using System.Linq;
using ChoSiren.Systems;
using ChoSiren.Systems.Dice;
using ChoSiren.Systems.Tactics;
using NUnit.Framework;
using UnityEngine;

namespace ChoSiren.Tests
{
    /// <summary>2026-10-31 赵金 dice rules: ≤2 dice per reroll, ≤3 rerolls per battle, a humane 自动 planner.</summary>
    public sealed class DiceRules1031Tests
    {
        private static readonly DateTime Now = new DateTime(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc);

        [SetUp, TearDown]
        public void Clean() { PlayerPrefs.DeleteKey(GameModel.SaveKey); PlayerPrefs.DeleteKey(GameModel.LegacySaveKey); }

        private static int RolledCount(DiceTurn dice) => dice.Rolled.Count(r => r);

        [Test]
        public void SelectingAThirdDieIsRefusedAndTheFirstTwoStaySelected()
        {
            var dice = DiceTurn.ForBattle(new SeededRandom(5), 3, true);
            dice.Begin();
            Assert.That(dice.ToggleHold(0), Is.False);
            Assert.That(dice.ToggleHold(3), Is.False);
            Assert.That(dice.CanToggleHold(4), Is.False, "第三颗不能再选");
            Assert.That(dice.ToggleHold(4), Is.True, "被拒绝时保持保留");
            Assert.That(dice.SelectedForRerollCount, Is.EqualTo(DiceTurn.MaxDicePerReroll));
            Assert.That(dice.Held[0] || dice.Held[3], Is.False, "已选的两颗不受影响");
            Assert.That(dice.CanToggleHold(0), Is.True, "已选的骰子可以取消");
            dice.GainEnergy(100);
            Assert.That(dice.RerollUnheld(out string error), Is.True, error);
            Assert.That(RolledCount(dice), Is.EqualTo(2));
            Assert.That(dice.Rolled[0] && dice.Rolled[3], Is.True);
        }

        [Test]
        public void RerollingWithNothingPickedRollsAllSixDiceAsOneOperation()
        {
            var dice = DiceTurn.ForBattle(new SeededRandom(5), 3, true);
            dice.Begin();
            dice.GainEnergy(100);
            int used = dice.RerollsUsedInBattle;
            Assert.That(dice.RerollAll(out string error), Is.True, error);
            Assert.That(RolledCount(dice), Is.EqualTo(dice.Values.Count), "不选骰子点重投 = 6 颗全部重投");
            Assert.That(dice.RerollsUsedInBattle, Is.EqualTo(used + 1), "全部重投只算一次");
        }

        [Test]
        public void TheAutoSuggestedRerollTouchesAtMostTwoDice()
        {
            for (ulong seed = 1; seed <= 40; seed++)
            {
                var dice = DiceTurn.ForBattle(new SeededRandom(seed), 3, seed % 2 == 0);
                dice.Begin();
                int[] before = dice.Values.ToArray();
                dice.GainEnergy(100);
                Assert.That(dice.RerollSuggested(out string error), Is.True, error);
                Assert.That(RolledCount(dice), Is.InRange(1, DiceTurn.MaxDicePerReroll), "seed " + seed);
                int changedSlots = 0;
                for (int i = 0; i < before.Length; i++) if (!dice.Rolled[i]) Assert.That(dice.Values[i], Is.EqualTo(before[i]));
                    else changedSlots++;
                Assert.That(changedSlots, Is.LessThanOrEqualTo(2));
            }
        }

        [TestCase(1), TestCase(2), TestCase(3), TestCase(5)]
        public void ABattleNeverAllowsMoreThanThreeRerolls(int authoredLimit)
        {
            // 2026-11-01c: the 3-a-battle cap now applies to the realtime charge bar only (no rhythm mode);
            // turn battles use the endless 骰子豆 (TurnBattleRerollsWhileItHasPipsAndNeverStoresMoreThanThree).
            var dice = DiceTurn.ForBattle(new SeededRandom(9), authoredLimit, true);
            dice.Begin();
            Assert.That(dice.BattleRerollLimit, Is.EqualTo(Math.Min(authoredLimit, DiceTurn.MaxRerollsPerBattle)));
            int done = 0;
            for (int i = 0; i < 12; i++)
            {
                dice.GainEnergy(250);
                if (i == 6) dice.GrantFreeReroll();
                if (dice.RerollAll(out _)) done++;
            }
            Assert.That(done, Is.LessThanOrEqualTo(DiceTurn.MaxRerollsPerBattle));
            Assert.That(dice.RerollsUsedInBattle, Is.EqualTo(done));
            Assert.That(dice.CanReroll, Is.False);
            Assert.That(dice.AvailableRerolls, Is.Zero);
        }

        [TestCase(1), TestCase(3)]
        public void TurnBattleRerollsWhileItHasPipsAndNeverStoresMoreThanThree(int authoredLimit)
        {
            // 2026-11-01c 无限骰子 (用户: "我还是想一直能投"): no per-battle total, at most 3 骰子豆 waiting.
            var dice = DiceTurn.ForBattle(new SeededRandom(9), authoredLimit, true);
            dice.Begin();
            dice.EnableRhythm();
            int done = 0;
            for (int i = 0; i < 12; i++)
            {
                dice.GainEnergy(250);
                dice.GrantRhythmReroll();
                Assert.That(dice.StoredRerolls, Is.LessThanOrEqualTo(DiceTurn.MaxStoredRerolls));
                if (dice.RerollAll(out _)) done++;
            }
            Assert.That(done, Is.EqualTo(12), "每次有豆子都能投");
            Assert.That(dice.RerollsUsedInBattle, Is.EqualTo(done));
            Assert.That(done, Is.GreaterThan(DiceTurn.MaxRerollsPerBattle));
        }

        [Test]
        public void EveryStageTypeCapsRerollsAtThree()
        {
            foreach (string type in new[] { "normal", "elite", "boss", "world" })
                Assert.That(new StageDefinition { EncounterType = type }.RerollLimit, Is.InRange(1, 3), type);
            Assert.That(new StageDefinition { EncounterType = "normal" }.RerollLimit, Is.EqualTo(2));
        }

        [Test]
        public void AutoPlannerKeepsTheFormedDiceAndPicksAtMostTwoLooseOnes()
        {
            int[] pair = { 2, 5, 2, 1, 3, 6 };
            bool[] mask = DiceAutoPlanner1031.ChooseRerollMask(pair, DiceTurn.MaxDicePerReroll, false);
            Assert.That(DiceAutoPlanner1031.Count(mask), Is.InRange(1, 2));
            Assert.That(mask[0] || mask[2], Is.False, "一对的两颗必须保留");
            int[] high = { 1, 2, 3, 4, 6, 6 }; // 一对 6 + 1234 → keep the run/pair, roll the misfit
            Assert.That(DiceAutoPlanner1031.Count(DiceAutoPlanner1031.ChooseRerollMask(high, 2, false)), Is.InRange(1, 2));
            foreach (int[] formed in new[] { new[] { 3, 3, 5, 5, 1, 2 }, new[] { 4, 4, 4, 1, 2, 6 }, new[] { 1, 2, 3, 4, 5, 5 } })
                Assert.That(DiceAutoPlanner1031.IsWeakHand(DiceRules.Evaluate(formed)), Is.False, string.Join(",", formed));
            Assert.That(DiceAutoPlanner1031.IsWeakHand(DiceRules.Evaluate(pair)), Is.True);
        }

        [Test]
        public void AutoPlannerOnlyRerollsWeakHandsAndWithinTheBudget()
        {
            // 2026-11-01c: turn battles open with 2 骰子豆; 自动 rerolls below 三条, or whenever the store is full.
            for (ulong seed = 1; seed <= 60; seed++)
            {
                var dice = DiceTurn.ForBattle(new SeededRandom(seed), 3, true);
                dice.Begin();
                dice.EnableRhythm();
                bool helps = DiceAutoPlanner1031.Count(DiceAutoPlanner1031.ChooseRerollMask(dice.Values, 2, false)) > 0;
                bool low = dice.Hand.MultiplierPermille < DiceRules.MultiplierOf(DicePattern.ThreeKind);
                Assert.That(DiceAutoPlanner1031.ShouldAutoReroll(dice), Is.False, "开局尚未充能");
                dice.GainEnergy(200);
                Assert.That(DiceAutoPlanner1031.ShouldAutoReroll(dice), Is.EqualTo(low && helps), "seed " + seed + " (2 颗豆)");
                dice.GainEnergy(100);
                Assert.That(dice.StoredRerolls, Is.EqualTo(DiceTurn.MaxStoredRerolls));
                Assert.That(DiceAutoPlanner1031.ShouldAutoReroll(dice), Is.EqualTo(helps), "seed " + seed + " (存满)");
            }
            var spent = DiceTurn.ForBattle(new SeededRandom(3), 3, true);
            spent.Begin();
            spent.EnableRhythm();
            while (spent.CanReroll) Assert.That(spent.RerollAll(out _), Is.True);
            Assert.That(DiceAutoPlanner1031.ShouldAutoReroll(spent), Is.False, "没豆子不投");
        }

        [Test]
        public void RollsAreUnweightedAfterARerollToo()
        {
            // No pity/bias: a rerolled die lands on every face about equally often, whatever the hand.
            var counts = new int[7];
            int total = 0;
            for (ulong seed = 1; seed <= 2500; seed++)
            {
                var dice = DiceTurn.ForBattle(new SeededRandom(seed), 3, true);
                dice.Begin();
                dice.GainEnergy(100);
                Assert.That(dice.RerollAll(out _), Is.True);
                for (int i = 0; i < dice.Values.Count; i++)
                    if (dice.Rolled[i]) { counts[dice.Values[i]]++; total++; }
            }
            for (int face = 1; face <= 6; face++)
                Assert.That(counts[face], Is.InRange(total / 6 * 85 / 100, total / 6 * 115 / 100), "face " + face);
        }

        [Test]
        public void BigHandMultipliersAndCaptainTablesAreToneDown()
        {
            Assert.That(DiceRules.MultiplierOf(DicePattern.Pair), Is.EqualTo(1100));
            Assert.That(DiceRules.MultiplierOf(DicePattern.TwoPair), Is.EqualTo(1200));
            Assert.That(DiceRules.MultiplierOf(DicePattern.ThreeKind), Is.EqualTo(1300));
            Assert.That(DiceRules.MultiplierOf(DicePattern.Straight), Is.EqualTo(1350));
            Assert.That(DiceRules.MultiplierOf(DicePattern.FullHouse), Is.EqualTo(1450));
            Assert.That(DiceRules.MultiplierOf(DicePattern.FourKind), Is.EqualTo(1600));
            Assert.That(DiceRules.MultiplierOf(DicePattern.FiveKind), Is.EqualTo(1750));
            Assert.That(DiceRules.MultiplierOf(DicePattern.SixKind), Is.EqualTo(1800));
            foreach (DicePattern p in Enum.GetValues(typeof(DicePattern)))
            {
                Assert.That(BattleSimulator.PoisonLayers(p), Is.LessThanOrEqualTo(6));
                Assert.That(BattleSimulator.Combo(p), Is.LessThanOrEqualTo(4));
                Assert.That(BattleSimulator.Pierce(p), Is.LessThanOrEqualTo(400));
                Assert.That(BattleSimulator.ShieldPercent(p), Is.LessThanOrEqualTo(220));
            }
            Assert.That(DiceTurn.RhythmMaxBonusPermille, Is.LessThanOrEqualTo(400));
        }

        [Test]
        public void TurnBattleRerollsPastThreeAndEachTouchesAtMostTwoDice()
        {
            var model = new GameModel(() => Now, new InMemorySaveStore());
            var stage = model.Tactics.Stages.First(s => s.Id == "stage-1-10");
            for (int i = 0; i < model.Save.MemberLevels.Count; i++) model.Save.MemberLevels[i] = stage.RecommendedLevel;
            model.Save.StoryProgress = 100;
            foreach (var s in model.Tactics.Stages) model.Save.ClearedStages.Add(new StageClear { Id = s.Id, Stars = 3 });
            BattleSimulator battle = model.StartStageBattle(stage.Id, 31, out string error);
            Assert.That(battle, Is.Not.Null, error);
            battle.EnableTurnBased();
            DiceTurn dice = battle.BattleDice;
            int attempts = 0;
            for (int guard = 0; guard < 3000 && battle.Outcome == BattleOutcome.Ongoing; guard++)
            {
                BattleUnit actor = battle.NextTurnActor();
                if (actor == null) break;
                if (actor.Side == BattleSide.Player && dice.CanReroll)
                {
                    int revision = dice.Revision;
                    attempts++;
                    if (dice.RerollSuggested(out _)) Assert.That(RolledCount(dice), Is.InRange(1, 2));
                    else Assert.That(dice.Revision, Is.EqualTo(revision));
                }
                battle.AutoTurn();
            }
            // 2026-11-01c 无限骰子: more than the old 3 a battle, but never more than the 骰子豆 earned (2 + 1 per ~3 actions + waves).
            Assert.That(dice.RerollsUsedInBattle, Is.GreaterThan(DiceTurn.MaxRerollsPerBattle));
            Assert.That(dice.RerollsUsedInBattle, Is.LessThanOrEqualTo(dice.EarnedRerolls + DiceTurn.StartingStoredRerolls));
            Assert.That(dice.StoredRerolls, Is.InRange(0, DiceTurn.MaxStoredRerolls));
            TestContext.WriteLine($"attempts={attempts} used={dice.RerollsUsedInBattle} outcome={battle.Outcome}");
        }
    }
}
