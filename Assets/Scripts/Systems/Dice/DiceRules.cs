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
        FiveKind
    }

    /// <summary>An immutable five-die result that can be passed directly to the battle UI.</summary>
    public sealed class DiceHand
    {
        private readonly int[] values;
        private readonly IReadOnlyList<int> readOnlyValues;

        public DiceHand(IReadOnlyList<int> values, DicePattern pattern, string displayName,
            int multiplierPermille)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            this.values = new int[values.Count];
            for (int index = 0; index < values.Count; index++) this.values[index] = values[index];
            readOnlyValues = Array.AsReadOnly(this.values);
            Pattern = pattern;
            DisplayName = displayName ?? string.Empty;
            MultiplierPermille = multiplierPermille;
        }

        public IReadOnlyList<int> Values => readOnlyValues;
        public DicePattern Pattern { get; }
        public string DisplayName { get; }
        public int MultiplierPermille { get; }
    }

    /// <summary>Evaluates the five-die patterns and multipliers agreed for dungeon combat.</summary>
    public static class DiceRules
    {
        public const int DiceCount = 5;

        public static DiceHand Evaluate(IReadOnlyList<int> values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            if (values.Count != DiceCount)
                throw new ArgumentException($"必须提供 {DiceCount} 颗骰子", nameof(values));

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

        private static bool IsStraight(int[] counts)
        {
            bool low = true;
            bool high = true;
            for (int value = 1; value <= 5; value++) low &= counts[value] == 1;
            for (int value = 2; value <= 6; value++) high &= counts[value] == 1;
            return low || high;
        }

        private static int MultiplierFor(DicePattern pattern)
        {
            switch (pattern)
            {
                case DicePattern.Pair: return 1500;
                case DicePattern.TwoPair: return 2000;
                case DicePattern.ThreeKind: return 2500;
                case DicePattern.Straight: return 3000;
                case DicePattern.FullHouse: return 4900;
                case DicePattern.FourKind: return 5000;
                case DicePattern.FiveKind: return 10000;
                default: return 1000;
            }
        }

        private static string DisplayNameFor(DicePattern pattern)
        {
            switch (pattern)
            {
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
