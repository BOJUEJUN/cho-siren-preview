using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ChoSiren.Systems.Dice;
using ChoSiren.Systems.Tactics;
using NUnit.Framework;
using UnityEngine;

namespace ChoSiren.Tests
{
    /// <summary>
    /// 2026-10-31 赵金「我方过强」: auto-plays every stage in turn mode (the shipped mode) with the panel's 自动 dice
    /// policy, and with no rerolls (手动 without taps). Reports win rate, rounds, big-hand rate and damage per action
    /// so the dice nerf can be compared before/after. Writes Builds/qa-logs/zj-1031/dice/balance-*.txt when present.
    /// </summary>
    public sealed class DiceBalance1031Tests
    {
        private const string Label = "after";
        private static readonly DateTime Now = new DateTime(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc);
        private static readonly ulong[] Seeds = { 3, 7, 11, 19, 23, 31, 44, 59 } /* same seeds as the "before" run */;

        [SetUp, TearDown]
        public void Clean() { PlayerPrefs.DeleteKey(GameModel.SaveKey); PlayerPrefs.DeleteKey(GameModel.LegacySaveKey); }

        private sealed class Tally
        {
            public int Battles, Wins, Rounds, PlayerActions, BigHandActions, Rerolls, RerollFormed, MaxRerollsInBattle;
            public long PlayerDamage, DiceWeighted, DiceHits;
            public string Line(string key) =>
                $"{key,-22} n={Battles,3} win={100f * Wins / Math.Max(1, Battles),5:0.0}% rounds={(float)Rounds / Math.Max(1, Battles),5:0.0} " +
                $"dmg/act={(float)PlayerDamage / Math.Max(1, PlayerActions),7:0} bigHand={100f * BigHandActions / Math.Max(1, PlayerActions),5:0.0}% " +
                $"diceMul={(float)DiceWeighted / Math.Max(1, DiceHits) / 1000f:0.000} rerolls/battle={(float)Rerolls / Math.Max(1, Battles):0.00} " +
                $"maxRerolls={MaxRerollsInBattle} formedAfterReroll={100f * RerollFormed / Math.Max(1, Rerolls),5:0.0}%";
        }

        [Test]
        public void ReportTurnBattleBalanceWithAutoDice()
        {
            var tallies = Report(Label, Seeds);
            // Dice: 2026-11-01c 无限骰子 — turn battles reroll past the old 3 a battle (骰子豆 refill), idle never rerolls.
            // 2026-11-01 (赵金 "怪要变强", Balance1101Tests): the recommended level is
            // no longer a guaranteed win — the "normal stages ≥95% / idle ≥90%" gates of 10-31 are replaced by the
            // 通过率 targets in Balance1101Tests; here 自动 must simply not do worse than leaving the dice alone.
            AssertAutoNotWorseThanIdle(tallies);
            Assert.That(tallies["lvrec auto ALL"].MaxRerollsInBattle, Is.GreaterThan(DiceTurn.MaxRerollsPerBattle), "回合制不再每场限 3 次");
            Assert.That(tallies["lvrec manual-idle ALL"].MaxRerollsInBattle, Is.Zero);
        }

        /// <summary>
        /// The hand now lasts a whole battle, so eight seeds are only eight opening hands: a wider sample shows the
        /// expected values (written to balance-after-wide.txt; no "before" counterpart exists for it).
        /// </summary>
        [Test]
        public void ReportTurnBattleBalanceWithManySeeds()
        {
            var seeds = new ulong[32];
            for (int i = 0; i < seeds.Length; i++) seeds[i] = (ulong)(1000 + i * 37);
            var tallies = Report(Label + "-wide", seeds);
            AssertAutoNotWorseThanIdle(tallies);
        }

        private static void AssertAutoNotWorseThanIdle(IDictionary<string, Tally> tallies)
        {
            foreach (string level in new[] { "lvrec", "lvrec-2" })
            {
                Tally auto = tallies[level + " auto ALL"], idle = tallies[level + " manual-idle ALL"];
                Assert.That(auto.Wins * 100 + auto.Battles * 5, Is.GreaterThanOrEqualTo(idle.Wins * 100 * auto.Battles / idle.Battles), level);
            }
        }

        private static SortedDictionary<string, Tally> Report(string label, ulong[] seeds)
        {
            var lines = new List<string> { $"DICE-BALANCE-1031 label={label} seeds={seeds.Length}" };
            var tallies = new SortedDictionary<string, Tally>(StringComparer.Ordinal);
            var catalog = new GameModel(() => Now, new InMemorySaveStore());
            foreach (int levelOffset in new[] { 0, -2 })
            foreach (bool autoDice in new[] { true, false })
            foreach (StageDefinition stage in catalog.Tactics.Stages)
            foreach (ulong seed in seeds)
            {
                int level = Math.Max(1, stage.RecommendedLevel + levelOffset);
                string mode = autoDice ? "auto" : "manual-idle";
                string type = stage.EncounterType;
                Tally all = Get(tallies, $"lv{(levelOffset == 0 ? "rec" : "rec-2")} {mode} ALL");
                Tally byType = Get(tallies, $"lv{(levelOffset == 0 ? "rec" : "rec-2")} {mode} {type}");
                Play(stage.Id, level, seed, autoDice, all, byType);
            }
            foreach (var pair in tallies) lines.Add(pair.Value.Line(pair.Key));
            string text = string.Join("\n", lines);
            Debug.Log(text);
            TestContext.WriteLine(text);
            // 2026-11-01: written next to the balance batch's logs; the 10-31 dice evidence in zj-1101/balance stays untouched
            // since 11-01c (无限骰子 writes its own copy).
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "qa-logs", "zj-1101c", "dice-endless"));
            if (Directory.Exists(dir)) File.WriteAllText(Path.Combine(dir, $"dice1031-balance-{label}.txt"), text + "\n");
            return tallies;
        }

        /// <summary>大牌: 顺子 and up (the same set the 0.9.15 table scored at ≥1.45×; 三对 is not one).</summary>
        private static bool IsBig(DicePattern p) => p == DicePattern.Straight || p == DicePattern.FullHouse ||
            p == DicePattern.FourKind || p == DicePattern.FiveKind || p == DicePattern.DoubleThreeKind ||
            p == DicePattern.SixStraight || p == DicePattern.SixKind;

        private static Tally Get(IDictionary<string, Tally> tallies, string key)
        {
            if (!tallies.TryGetValue(key, out Tally tally)) tallies[key] = tally = new Tally();
            return tally;
        }

        private static void Play(string stageId, int level, ulong seed, bool autoDice, params Tally[] into)
        {
            var model = new GameModel(() => Now, new InMemorySaveStore());
            for (int i = 0; i < model.Save.MemberLevels.Count; i++) model.Save.MemberLevels[i] = level;
            foreach (var s in model.Tactics.Stages) model.Save.ClearedStages.Add(new StageClear { Id = s.Id, Stars = 3 });
            model.Save.StoryProgress = 100;
            BattleSimulator battle = model.StartStageBattle(stageId, seed, out string error);
            Assert.That(battle, Is.Not.Null, stageId + " " + error);
            battle.EnableTurnBased();
            DiceTurn dice = battle.BattleDice;
            int actions = 0, playerActions = 0, big = 0, rerolls = 0, formed = 0, lastAuto = -100;
            int cursor = battle.Log.Count;
            long diceWeighted = 0, diceHits = 0;
            while (battle.Outcome == BattleOutcome.Ongoing && actions < 3000)
            {
                BattleUnit actor = battle.NextTurnActor();
                if (actor == null) break;
                if (actor.Side == BattleSide.Player)
                {
                    playerActions++;
                    if (autoDice && AutoReroll(dice, actions, ref lastAuto))
                    {
                        rerolls++;
                        if (IsBig(dice.Hand.Pattern) || dice.Hand.Pattern == DicePattern.ThreeKind) formed++;
                    }
                    if (IsBig(dice.Hand.Pattern)) big++;
                    battle.AutoTurn(autoDice);
                }
                else battle.AutoTurn();
                actions++;
                for (; cursor < battle.Log.Count; cursor++)
                {
                    BattleEvent e = battle.Log[cursor];
                    BattleUnit source = battle.FindUnit(e.ActorId);
                    if (e.Kind == BattleEventKind.Damage && source != null && source.Side == BattleSide.Player && e.DiceMultiplierPermille > 0)
                    { diceWeighted += e.DiceMultiplierPermille; diceHits++; }
                }
            }
            foreach (Tally tally in into)
            {
                tally.Battles++;
                if (battle.Outcome == BattleOutcome.Victory) tally.Wins++;
                tally.Rounds += battle.TurnRound;
                tally.PlayerActions += playerActions;
                tally.BigHandActions += big;
                tally.Rerolls += rerolls;
                tally.RerollFormed += formed;
                tally.MaxRerollsInBattle = Math.Max(tally.MaxRerollsInBattle, rerolls);
                tally.PlayerDamage += battle.CharacterDamageDealt;
                tally.DiceWeighted += diceWeighted;
                tally.DiceHits += diceHits;
            }
        }

        /// <summary>
        /// The shipped 自动 policy (TacticsBattlePanel.AutoReroll1031): weak hand only, ≤2 loose dice, and a gap —
        /// the panel waits ~2.75 s of real time, about three on-screen actions, between two auto rerolls.
        /// (The "before" numbers came from the 0.9.15 policy: below 三条 reroll every non-pattern die, every turn.)
        /// </summary>
        private static bool AutoReroll(DiceTurn dice, int action, ref int lastAction)
        {
            if (action - lastAction < 3 || !DiceAutoPlanner1031.ShouldAutoReroll(dice)) return false;
            bool[] pick = DiceAutoPlanner1031.ChooseRerollMask(dice.Values, DiceTurn.MaxDicePerReroll, false);
            for (int i = 0; i < pick.Length; i++) if (pick[i] && dice.Held[i]) dice.ToggleHold(i);
            if (!dice.RerollUnheld(out _)) return false;
            lastAction = action;
            return true;
        }
    }
}
