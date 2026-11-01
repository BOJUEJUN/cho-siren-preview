using System;
using System.Collections.Generic;

namespace ChoSiren.Systems.Dice
{
    /// <summary>
    /// 2026-10-31 (赵金: "自动重投…自己选择两个不一样的骰子去重投，要人性化一点"). Picks the dice a reroll touches:
    /// never a die that forms the current pattern, at most <see cref="DiceTurn.MaxDicePerReroll"/>, and only the ones
    /// whose reroll actually raises the expected hand. 自动 only plays it on a weak hand (一对 or worse).
    /// </summary>
    public static class DiceAutoPlanner1031
    {
        /// <summary>自动 rerolls only below 两对; a formed hand (两对 and up) is kept.</summary>
        public static bool IsWeakHand(DiceHand hand) =>
            hand != null && hand.MultiplierPermille < DiceRules.MultiplierOf(DicePattern.TwoPair);

        /// <summary>
        /// True when 自动 should spend a reroll on this hand now. 2026-11-01c 无限骰子: in a turn battle 自动 keeps the dice
        /// moving — it rolls any hand below 三条 (where the bonus starts) and, when the 骰子豆 store is full, any hand that
        /// can still improve, so a pip is never wasted. The legacy realtime battle keeps the 一对-or-worse rule.
        /// </summary>
        public static bool ShouldAutoReroll(DiceTurn dice)
        {
            if (dice == null || !dice.IsBattleSession || !dice.CanReroll || !dice.SelectiveReroll) return false;
            bool wanted = dice.RhythmMode
                ? dice.Hand.MultiplierPermille < DiceRules.MultiplierOf(DicePattern.ThreeKind) ||
                  dice.StoredRerolls >= dice.StoredRerollCapacity
                : IsWeakHand(dice.Hand);
            return wanted && Count(ChooseRerollMask(dice.Values, DiceTurn.MaxDicePerReroll, false)) > 0;
        }

        /// <summary>
        /// Dice to reroll (true = reroll). Only dice outside the current pattern are candidates; among 1..maxDice of
        /// them the set with the best expected hand wins (fewer dice on a tie). Empty when no reroll improves the
        /// expectation, unless <paramref name="force"/> asks for at least one die (an explicit reroll command).
        /// </summary>
        public static bool[] ChooseRerollMask(IReadOnlyList<int> values, int maxDice, bool force)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            maxDice = Math.Max(1, maxDice);
            DiceHand hand = DiceRules.Evaluate(values);
            var mask = new bool[values.Count];
            var free = new List<int>();
            for (int i = 0; i < values.Count; i++) if (!hand.Participating[i]) free.Add(i);

            double bestEv = hand.MultiplierPermille + .5; // a reroll must raise the expectation
            int[] best = null;
            foreach (int[] subset in Subsets(free, Math.Min(maxDice, free.Count)))
            {
                double ev = Expected(values, subset);
                if (ev > bestEv + 1e-6 || best != null && Math.Abs(ev - bestEv) <= 1e-6 && subset.Length < best.Length)
                {
                    bestEv = ev;
                    best = subset;
                }
            }
            if (best == null && force)
            {
                // An explicit command on a hand that cannot improve: the lowest loose dice, else the lowest dice.
                var pool = new List<int>(free.Count > 0 ? free : AllIndices(values.Count));
                pool.Sort((a, b) => values[a] != values[b] ? values[a].CompareTo(values[b]) : b.CompareTo(a));
                best = pool.GetRange(0, Math.Min(maxDice, pool.Count)).ToArray();
            }
            if (best != null) foreach (int index in best) mask[index] = true;
            return mask;
        }

        public static int Count(bool[] mask)
        {
            int n = 0;
            if (mask != null) foreach (bool b in mask) if (b) n++;
            return n;
        }

        private static IEnumerable<int[]> Subsets(List<int> items, int maxSize)
        {
            for (int a = 0; a < items.Count; a++)
            {
                yield return new[] { items[a] };
                if (maxSize < 2) continue;
                for (int b = a + 1; b < items.Count; b++) yield return new[] { items[a], items[b] };
            }
        }

        private static double Expected(IReadOnlyList<int> values, int[] subset)
        {
            var trial = new int[values.Count];
            for (int i = 0; i < trial.Length; i++) trial[i] = values[i];
            int outcomes = subset.Length == 1 ? 6 : 36;
            long total = 0;
            for (int k = 0; k < outcomes; k++)
            {
                trial[subset[0]] = k % 6 + 1;
                if (subset.Length > 1) trial[subset[1]] = k / 6 + 1;
                total += DiceRules.Evaluate(trial).MultiplierPermille;
            }
            return (double)total / outcomes;
        }

        private static List<int> AllIndices(int count)
        {
            var list = new List<int>(count);
            for (int i = 0; i < count; i++) list.Add(i);
            return list;
        }
    }
}
