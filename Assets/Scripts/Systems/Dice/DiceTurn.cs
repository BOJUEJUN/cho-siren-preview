using System;
using System.Collections.Generic;
using ChoSiren.Systems;

namespace ChoSiren.Systems.Dice
{
    /// <summary>Owns one five-die combat turn, including holds, rerolls and the energy reroll.</summary>
    public sealed class DiceTurn
    {
        public const int MaxEnergy = 100;
        public const int InitialRerolls = 2;

        private readonly IRandomSource random;
        private readonly int[] values = new int[DiceRules.DiceCount];
        private readonly bool[] held = new bool[DiceRules.DiceCount];
        private readonly IReadOnlyList<int> readOnlyValues;
        private readonly IReadOnlyList<bool> readOnlyHeld;
        private bool begun;

        public DiceTurn(IRandomSource random, int startingEnergy = 0)
        {
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            Energy = Math.Max(0, Math.Min(MaxEnergy, startingEnergy));
            readOnlyValues = Array.AsReadOnly(values);
            readOnlyHeld = Array.AsReadOnly(held);
        }

        public IReadOnlyList<int> Values => readOnlyValues;
        public IReadOnlyList<bool> Held => readOnlyHeld;
        public int RerollsRemaining { get; private set; } = InitialRerolls;
        public int Energy { get; private set; }
        public bool CanEnergyReroll => Energy >= MaxEnergy;
        public DiceHand Hand { get; private set; }

        public void Begin()
        {
            for (int index = 0; index < held.Length; index++) held[index] = false;
            RerollsRemaining = InitialRerolls;
            RollAll();
            begun = true;
            RefreshHand();
        }

        /// <summary>Toggles a die and returns its new held state.</summary>
        public bool ToggleHold(int index)
        {
            if (!begun) throw new InvalidOperationException("请先开始骰子回合");
            if (index < 0 || index >= held.Length) throw new ArgumentOutOfRangeException(nameof(index));
            held[index] = !held[index];
            return held[index];
        }

        public bool RerollUnheld(out string error)
        {
            if (!begun)
            {
                error = "请先开始骰子回合";
                return false;
            }

            if (RerollsRemaining <= 0)
            {
                error = "本回合的重投次数已经用完";
                return false;
            }

            bool hasUnheld = false;
            for (int index = 0; index < held.Length; index++) hasUnheld |= !held[index];
            if (!hasUnheld)
            {
                error = "至少取消保留一颗骰子才能重投";
                return false;
            }

            for (int index = 0; index < values.Length; index++)
                if (!held[index]) values[index] = RollDie();
            RerollsRemaining--;
            RefreshHand();
            error = string.Empty;
            return true;
        }

        public bool EnergyRerollAll(out string error)
        {
            if (!begun)
            {
                error = "请先开始骰子回合";
                return false;
            }

            if (Energy < MaxEnergy)
            {
                error = $"能量达到 {MaxEnergy} 才能全部重投";
                return false;
            }

            Energy = 0;
            for (int index = 0; index < held.Length; index++) held[index] = false;
            RollAll();
            RefreshHand();
            error = string.Empty;
            return true;
        }

        public void GainEnergy(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            Energy = (int)Math.Min(MaxEnergy, (long)Energy + amount);
        }

        private int RollDie() => random.Next(6) + 1;

        private void RollAll()
        {
            for (int index = 0; index < values.Length; index++) values[index] = RollDie();
        }

        private void RefreshHand()
        {
            Hand = DiceRules.Evaluate(values);
        }
    }
}
