using System;
using System.Collections.Generic;

namespace ChoSiren.Systems.Dice
{
    public enum DicePattern
    {
        HighPoint,
        Pair,
        TwoPair,
        ThreeKind,
        Straight,
        FullHouse,
        FourKind,
        FiveKind,
        ThreePair,
        DoubleThreeKind,
        SixStraight,
        SixKind
    }

    /// <summary>An immutable dice result that can be passed directly to the battle UI.</summary>
    public sealed class DiceHand
    {
        private readonly int[] values;
        private readonly IReadOnlyList<int> readOnlyValues;
        private readonly bool[] participating;
        private readonly IReadOnlyList<bool> readOnlyParticipating;

        public DiceHand(IReadOnlyList<int> values, DicePattern pattern, string displayName,
            int multiplierPermille)
            : this(values, pattern, displayName, multiplierPermille,
                DiceRules.BuildParticipationMask(values, pattern))
        {
        }

        public DiceHand(IReadOnlyList<int> values, DicePattern pattern, string displayName,
            int multiplierPermille, IReadOnlyList<bool> participating)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            if (participating == null) throw new ArgumentNullException(nameof(participating));
            if (participating.Count != values.Count)
                throw new ArgumentException("参与掩码必须与骰子数量相同", nameof(participating));

            this.values = new int[values.Count];
            this.participating = new bool[participating.Count];
            int pipTotal = 0;
            int participatingPipTotal = 0;
            for (int index = 0; index < values.Count; index++)
            {
                int value = values[index];
                bool isParticipating = participating[index];
                this.values[index] = value;
                this.participating[index] = isParticipating;
                pipTotal += value;
                if (isParticipating) participatingPipTotal += value;
            }

            readOnlyValues = Array.AsReadOnly(this.values);
            readOnlyParticipating = Array.AsReadOnly(this.participating);
            Pattern = pattern;
            DisplayName = displayName ?? string.Empty;
            MultiplierPermille = multiplierPermille;
            PipTotal = pipTotal;
            ParticipatingPipTotal = participatingPipTotal;
        }

        public IReadOnlyList<int> Values => readOnlyValues;
        public IReadOnlyList<bool> Participating => readOnlyParticipating;
        public DicePattern Pattern { get; }
        public string DisplayName { get; }
        public int MultiplierPermille { get; }
        public int PipTotal { get; }
        public int ParticipatingPipTotal { get; }
    }

    /// <summary>Evaluates six-die patterns (and legacy five-die records) and multipliers agreed for dungeon combat.</summary>
    public static class DiceRules
    {
        public const int DiceCount = 6;
        public const int MaxMultiplierPermille = 2000;

        public static DiceHand Evaluate(IReadOnlyList<int> values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            // Five-die snapshots remain readable; every new DiceTurn allocates six.
            if (values.Count == 5) return EvaluateFive(values);
            if (values.Count != DiceCount) throw new ArgumentException("必须提供 6 颗骰子", nameof(values));
            var counts = new int[7];
            foreach (int value in values)
            {
                if (value < 1 || value > 6) throw new ArgumentOutOfRangeException(nameof(values));
                counts[value]++;
            }
            int pairs = 0, triples = 0, singles = 0;
            bool six = false;
            for (int face = 1; face <= 6; face++)
            {
                if (counts[face] == 2) pairs++;
                if (counts[face] == 3) triples++;
                if (counts[face] == 1) singles++;
                if (counts[face] == 6) six = true;
            }
            DicePattern? whole = six ? DicePattern.SixKind : singles == 6 ? DicePattern.SixStraight
                : triples == 2 ? DicePattern.DoubleThreeKind : pairs == 3 ? DicePattern.ThreePair : (DicePattern?)null;
            if (whole.HasValue)
            {
                var all = new bool[DiceCount];
                for (int i = 0; i < all.Length; i++) all[i] = true;
                return new DiceHand(values, whole.Value, DisplayNameFor(whole.Value), MultiplierFor(whole.Value), all);
            }
            // Score the strongest five-die subset; the sixth kicker never receives a false
            // participation highlight or a captain benefit. Ties prefer higher scored pips.
            DiceHand best = null;
            bool[] mask = null;
            for (int omitted = 0; omitted < DiceCount; omitted++)
            {
                var subset = new int[5];
                for (int i = 0, j = 0; i < DiceCount; i++) if (i != omitted) subset[j++] = values[i];
                var hand = EvaluateFive(subset);
                if (best != null && (hand.MultiplierPermille < best.MultiplierPermille ||
                    hand.MultiplierPermille == best.MultiplierPermille && hand.ParticipatingPipTotal <= best.ParticipatingPipTotal)) continue;
                best = hand; mask = new bool[DiceCount];
                for (int i = 0, j = 0; i < DiceCount; i++) if (i != omitted) mask[i] = hand.Participating[j++];
            }
            return new DiceHand(values, best.Pattern, best.DisplayName, best.MultiplierPermille, mask);
        }

        private static DiceHand EvaluateFive(IReadOnlyList<int> values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            if (values.Count != 5) throw new ArgumentException("旧记录必须为五颗骰子", nameof(values));

            var counts = new int[7];
            for (int index = 0; index < values.Count; index++)
            {
                int value = values[index];
                if (value < 1 || value > 6)
                    throw new ArgumentOutOfRangeException(nameof(values), value, "骰子点数必须在 1 到 6 之间");
                counts[value]++;
            }

            int pairs = 0;
            bool hasThree = false;
            bool hasFour = false;
            bool hasFive = false;
            for (int value = 1; value <= 6; value++)
            {
                switch (counts[value])
                {
                    case 2: pairs++; break;
                    case 3: hasThree = true; break;
                    case 4: hasFour = true; break;
                    case 5: hasFive = true; break;
                }
            }

            bool straight = IsStraight(counts);
            DicePattern pattern;
            if (hasFive) pattern = DicePattern.FiveKind;
            else if (hasFour) pattern = DicePattern.FourKind;
            else if (hasThree && pairs == 1) pattern = DicePattern.FullHouse;
            else if (straight) pattern = DicePattern.Straight;
            else if (hasThree) pattern = DicePattern.ThreeKind;
            else if (pairs == 2) pattern = DicePattern.TwoPair;
            else if (pairs == 1) pattern = DicePattern.Pair;
            else pattern = DicePattern.HighPoint;

            return new DiceHand(values, pattern, DisplayNameFor(pattern), MultiplierFor(pattern));
        }

        internal static bool[] BuildParticipationMask(IReadOnlyList<int> values, DicePattern pattern)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));

            var participating = new bool[values.Count];
            if (values.Count == 0) return participating;
            if (values.Count == DiceCount)
            {
                var evaluated = Evaluate(values);
                for (int i = 0; i < values.Count; i++) participating[i] = evaluated.Participating[i];
                return participating;
            }

            if (pattern == DicePattern.Straight || pattern == DicePattern.FullHouse ||
                pattern == DicePattern.FiveKind)
            {
                for (int index = 0; index < participating.Length; index++) participating[index] = true;
                return participating;
            }

            if (pattern == DicePattern.HighPoint)
            {
                int highestIndex = 0;
                for (int index = 1; index < values.Count; index++)
                {
                    if (values[index] > values[highestIndex]) highestIndex = index;
                }

                participating[highestIndex] = true;
                return participating;
            }

            var counts = new int[7];
            for (int index = 0; index < values.Count; index++)
            {
                int value = values[index];
                if (value >= 1 && value <= 6) counts[value]++;
            }

            int requiredCount;
            switch (pattern)
            {
                case DicePattern.Pair:
                case DicePattern.TwoPair:
                    requiredCount = 2;
                    break;
                case DicePattern.ThreeKind:
                    requiredCount = 3;
                    break;
                case DicePattern.FourKind:
                    requiredCount = 4;
                    break;
                default:
                    return participating;
            }

            for (int index = 0; index < values.Count; index++)
            {
                int value = values[index];
                participating[index] = value >= 1 && value <= 6 && counts[value] == requiredCount;
            }

            return participating;
        }

        private static bool IsStraight(int[] counts)
        {
            bool low = true;
            bool high = true;
            for (int value = 1; value <= 5; value++) low &= counts[value] == 1;
            for (int value = 2; value <= 6; value++) high &= counts[value] == 1;
            return low || high;
        }

        public static int MultiplierOf(DicePattern pattern) => MultiplierFor(pattern);

        // 2026-10-31 (赵金: "大成的骰子几率…不用这么高"): every pattern above 一对 toned down, big hands the most.
        private static int MultiplierFor(DicePattern pattern)
        {
            switch (pattern)
            {
                case DicePattern.ThreePair: return 1250;
                case DicePattern.DoubleThreeKind: return 1500;
                case DicePattern.SixStraight: return 1550;
                case DicePattern.SixKind: return 1800;
                case DicePattern.Pair: return 1100;
                case DicePattern.TwoPair: return 1200;
                case DicePattern.ThreeKind: return 1300;
                case DicePattern.Straight: return 1350;
                case DicePattern.FullHouse: return 1450;
                case DicePattern.FourKind: return 1600;
                case DicePattern.FiveKind: return 1750;
                default: return 1000;
            }
        }

        private static string DisplayNameFor(DicePattern pattern)
        {
            switch (pattern)
            {
                case DicePattern.ThreePair: return "三对";
                case DicePattern.DoubleThreeKind: return "双三条";
                case DicePattern.SixStraight: return "六连顺";
                case DicePattern.SixKind: return "六同";
                case DicePattern.Pair: return "一对";
                case DicePattern.TwoPair: return "两对";
                case DicePattern.ThreeKind: return "三条";
                case DicePattern.Straight: return "顺子";
                case DicePattern.FullHouse: return "葫芦";
                case DicePattern.FourKind: return "四条";
                case DicePattern.FiveKind: return "五条";
                default: return "高点";
            }
        }
    }
}
