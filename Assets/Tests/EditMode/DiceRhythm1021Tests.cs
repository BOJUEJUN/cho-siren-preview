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
    /// <summary>2026-10-21 骰子节奏: current-hand bonus, heat, unlimited charge rerolls, wave targets.</summary>
    public sealed class DiceRhythm1021Tests
    {
        private static readonly DateTime Now = new DateTime(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc);

        [SetUp, TearDown]
        public void Clean() { PlayerPrefs.DeleteKey(GameModel.SaveKey); PlayerPrefs.DeleteKey(GameModel.LegacySaveKey); }

        private static DiceTurn Rhythm(params int[] picks) => Rhythm(2, false, picks);

        private static DiceTurn Rhythm(int limit, bool selective, params int[] picks)
        {
            var dice = DiceTurn.ForBattle(new ScriptedRandom(new[] { 0 }, picks), limit, selective);
            dice.Begin();
            dice.EnableRhythm();
            return dice;
        }

        [Test]
        public void BonusFollowsTheCurrentHandAndHeatGrowsOnlyWhileNotWorse()
        {
            // 2026-10-31 rules: each reroll changes at most two dice (here the ones the player ticks).
            // Opening 2,2,3,3,5,6 = 两对; 5,6 → 2,5 = 葫芦; 2,5 → 3,1 = 葫芦 (not worse); then 3,3 → 4,6 = 一对.
            // 2026-11-01b (骰子加成约 +10%): 两对 adds nothing, 葫芦 +8.3%, each not-worse reroll +1%.
            // 2026-11-01c 无限骰子: the sum is capped at DiceTurn.EndlessBonusCapPermille (+5%).
            var dice = Rhythm(3, true, 1, 1, 2, 2, 4, 5, 1, 4, 2, 0, 3, 5);
            Assert.That(dice.Hand.Pattern, Is.EqualTo(DicePattern.TwoPair));
            Assert.That(dice.DamageMultiplierPermille, Is.EqualTo(1000));
            dice.GainEnergy(100);
            dice.ToggleHold(4); dice.ToggleHold(5);
            Assert.That(dice.RerollUnheld(out string error), Is.True, error);
            Assert.That(dice.Hand.Pattern, Is.EqualTo(DicePattern.FullHouse));
            Assert.That(dice.Heat, Is.EqualTo(1));
            Assert.That(dice.DamageMultiplierPermille, Is.EqualTo(1000 + Math.Min(DiceTurn.EndlessBonusCapPermille, 83 + 10)));
            dice.GainEnergy(100);
            dice.ToggleHold(4); dice.ToggleHold(5);
            Assert.That(dice.RerollUnheld(out error), Is.True, error);
            Assert.That(dice.Heat, Is.EqualTo(2), "葫芦 → 葫芦 is not worse");
            Assert.That(dice.DamageMultiplierPermille, Is.EqualTo(1000 + Math.Min(DiceTurn.EndlessBonusCapPermille, 83 + 20)));
            dice.GainEnergy(100);
            dice.ToggleHold(2); dice.ToggleHold(3);
            Assert.That(dice.RerollUnheld(out error), Is.True, error);
            Assert.That(dice.Hand.MultiplierPermille, Is.LessThan(1450));
            Assert.That(dice.Heat, Is.Zero, "摇得更差，热度清零");
            Assert.That(dice.LastBonusGainPermille, Is.LessThan(0));
        }

        [Test]
        public void BonusStaysUnderTheFortyPercentCap()
        {
            // 2026-11-01c 无限骰子: ten rerolls in a row (more than the old 3 a battle) — 六同 + full heat still stops at +5%.
            var dice = Rhythm(3, false, 5);
            for (int i = 0; i < 10; i++)
            {
                dice.GainEnergy(100);
                Assert.That(dice.EnergyRerollAll(out string error), Is.True, error);
            }
            Assert.That(dice.Heat, Is.EqualTo(DiceTurn.RhythmHeatMax));
            Assert.That(dice.DamageMultiplierPermille, Is.EqualTo(1000 + Math.Min(DiceTurn.EndlessBonusCapPermille,
                DiceTurn.RhythmHandBonusPermille(1800) + DiceTurn.RhythmHeatMax * DiceTurn.RhythmHeatStepPermille)));
            Assert.That(dice.DamageMultiplierPermille, Is.LessThanOrEqualTo(1000 + DiceTurn.RhythmMaxBonusPermille));
            Assert.That(dice.BonusCapped, Is.True);
        }

        [Test]
        public void ChargeBarGrantsRerollsUpToTheBattleBudgetOnly()
        {
            // 2026-11-01c 无限骰子: the charge fills 骰子豆 up to the store (3) — no per-battle total any more.
            var dice = DiceTurn.ForBattle(new ScriptedRandom(new[] { 0 }, new[] { 0, 1, 2, 3, 4, 4 }), 3, false);
            dice.Begin();
            dice.EnableRhythm();
            Assert.That(dice.StoredRerolls, Is.EqualTo(DiceTurn.StartingStoredRerolls));
            dice.GainEnergy(450);
            Assert.That(dice.RerollsRemaining, Is.EqualTo(DiceTurn.MaxStoredRerolls));
            Assert.That(dice.Energy, Is.Zero, "存满后溢出的充能作废");
            for (int i = 0; i < 3; i++) Assert.That(dice.EnergyRerollAll(out _), Is.True);
            Assert.That(dice.CanReroll, Is.False, "豆子用完要等充能");
            Assert.That(dice.EnergyRerollAll(out string error), Is.False);
            Assert.That(error, Does.Contain("充能中"));
            dice.GainEnergy(500);
            dice.GrantRhythmReroll();
            Assert.That(dice.StoredRerolls, Is.EqualTo(DiceTurn.MaxStoredRerolls));
            for (int i = 0; i < 3; i++) Assert.That(dice.EnergyRerollAll(out error), Is.True, error);
            Assert.That(dice.UsedRerolls, Is.EqualTo(6), "每场不再限 3 次");
        }

        [Test]
        public void ANewWavePreservesChargeWithoutGrantingFreeRerolls()
        {
            var dice = Rhythm(0, 1, 2, 3, 4, 4);
            dice.GainEnergy(68);
            dice.GrantRhythmReroll();
            dice.GrantRhythmReroll();
            Assert.That(dice.AvailableRerolls, Is.Zero);
            Assert.That(dice.EarnedRerolls, Is.Zero);
            Assert.That(dice.Energy, Is.EqualTo(68));
            Assert.That(dice.Revision, Is.EqualTo(1));
            Assert.That(dice.FreeRerolls, Is.Zero);
            dice.GainEnergy(32);
            Assert.That(dice.AvailableRerolls, Is.EqualTo(1));
        }

        [Test]
        public void TheHandStaysPutBetweenRoundsUntilSomebodyRerolls()
        {
            // 2026-10-31 (赵金): the 10-26 free re-deal each round looked like the dice rerolling by themselves.
            var model = new GameModel(() => Now);
            var stage = model.Tactics.Stages.First(s => s.Id == "stage-1-1");
            for (int i = 0; i < model.Save.MemberLevels.Count; i++)
                model.Save.MemberLevels[i] = Mathf.Max(model.Save.MemberLevels[i], stage.RecommendedLevel);
            BattleSimulator battle = model.StartStageBattle("stage-1-1", 7, out string error);
            Assert.That(battle, Is.Not.Null, error);
            battle.EnableTurnBased();
            int[] opening = battle.BattleDice.Values.ToArray();
            int revision = battle.BattleDice.Revision;
            while (battle.Outcome == BattleOutcome.Ongoing && battle.TurnRound < 3)
            {
                if (battle.NextTurnActor() == null) break;
                battle.AutoTurn(false);
            }
            Assert.That(battle.TurnRound, Is.GreaterThanOrEqualTo(2));
            Assert.That(battle.BattleDice.Revision, Is.EqualTo(revision));
            Assert.That(battle.BattleDice.Values.ToArray(), Is.EqualTo(opening));
        }

        [Test]
        public void TurnBattleAdvancesWaveTargetsWhilePreservingChargedRerolls()
        {
            var model = new GameModel(() => Now);
            var stage = model.Tactics.Stages.First(s => s.Id == "stage-1-1");
            for (int i = 0; i < model.Save.MemberLevels.Count; i++)
                model.Save.MemberLevels[i] = Mathf.Max(model.Save.MemberLevels[i], stage.RecommendedLevel);
            BattleSimulator battle = model.StartStageBattle("stage-1-1", 44, out string error);
            Assert.That(battle, Is.Not.Null, error);
            var races = new Dictionary<string, string>();
            foreach (MemberDefinition member in GameModel.Members) races[member.Id] = member.Race;
            battle.EnableRealtime(races, null, stage.RerollLimit, false);
            battle.EnableTurnBased();
            Assert.That(battle.BattleDice.RhythmMode, Is.True);
            Assert.That(battle.HasDiceTarget, Is.True);
            Assert.That(battle.DiceTargetScore, Is.GreaterThanOrEqualTo(1250));
            int wavesSeen = 0, wave = battle.CurrentWave, encores = 0;
            for (int guard = 0; guard < 2000 && battle.Outcome == BattleOutcome.Ongoing; guard++)
            {
                BattleUnit actor = battle.NextTurnActor();
                if (actor == null) break;
                battle.AutoTurn();
                if (battle.CurrentWave > wave)
                {
                    wave = battle.CurrentWave;
                    wavesSeen++;
                    Assert.That(battle.DiceTargetWave, Is.EqualTo(wave));
                }
                Assert.That(battle.BattleDice.StoredRerolls, Is.LessThanOrEqualTo(battle.BattleDice.StoredRerollCapacity));
            }
            foreach (BattleEvent entry in battle.Log)
                if (entry.SkillId == BattleSimulator.DiceEncoreSkillId) encores++;
            Assert.That(battle.Outcome, Is.EqualTo(BattleOutcome.Victory));
            Assert.That(wavesSeen, Is.GreaterThan(0), "新一波正常切换骰子目标");
            TestContext.WriteLine($"waves={wave} encores={encores} target={battle.DiceTargetLabel}");
        }
    }
}
