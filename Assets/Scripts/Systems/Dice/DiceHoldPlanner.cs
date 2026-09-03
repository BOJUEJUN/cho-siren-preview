using System;
using System.Collections.Generic;

namespace ChoSiren.Systems.Dice
{
    /// <summary>Chooses a deterministic set of dice to keep before a normal reroll.</summary>
    public static class DiceHoldPlanner
    {
        public static bool[] Choose(IReadOnlyList<int> values)
        {
            DiceHand hand = DiceRules.Evaluate(values);
            var held = new bool[DiceRules.DiceCount];

            // Every scored pattern keeps the dice that establish it. This preserves an
            // existing hand while still allowing unrelated kickers to be rerolled.
            if (hand.Pattern != DicePattern.HighPoint)
            {
                for (int index = 0; index < held.Length; index++)
                    held[index] = hand.Participating[index];
                return held;
            }

            int bestStart = 0;
            int bestLength = 0;
            int runStart = 0;
            int runLength = 0;
            for (int value = 1; value <= 6; value++)
            {
                bool present = Contains(values, value);
                if (present)
                {
                    if (runLength == 0) runStart = value;
                    runLength++;
                    if (runLength > bestLength ||
                        (runLength == bestLength && value > bestStart + bestLength - 1))
                    {
                        bestStart = runStart;
                        bestLength = runLength;
                    }
                }
                else
                {
                    runLength = 0;
                }
            }

            if (bestLength >= 2)
            {
                int bestEnd = bestStart + bestLength - 1;
                for (int index = 0; index < values.Count; index++)
                    held[index] = values[index] >= bestStart && values[index] <= bestEnd;
                return held;
            }

            int highestIndex = 0;
            for (int index = 1; index < values.Count; index++)
            {
                if (values[index] > values[highestIndex]) highestIndex = index;
            }

            held[highestIndex] = true;
            return held;
        }

        private static bool Contains(IReadOnlyList<int> values, int target)
        {
            for (int index = 0; index < values.Count; index++)
            {
                if (values[index] == target) return true;
            }

            return false;
        }
    }
}
