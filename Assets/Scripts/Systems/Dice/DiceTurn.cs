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
        private int legacyRerolls = InitialRerolls;
        private long energyMicros;
        private const long EnergyScale = 1000000;

        public bool IsBattleSession { get; private set; }
        public bool SelectiveReroll { get; private set; }
        public int BattleRerollLimit { get; private set; }
        public int UsedRerolls { get; private set; }
        public int FreeRerolls { get; private set; }
        public int Revision { get; private set; }
        public const int MaxBattleBonusPermille = 1000;
        public int AccumulatedBonusPermille { get; private set; }
        public int LastBonusGainPermille { get; private set; }
        public int DamageMultiplierPermille => IsBattleSession ? 1000 + AccumulatedBonusPermille : Hand.MultiplierPermille;

        public static DiceTurn ForBattle(IRandomSource random, int rerollLimit, bool selectiveReroll)
        {
            if (rerollLimit < 1 || rerollLimit > 5) throw new ArgumentOutOfRangeException(nameof(rerollLimit));
            return new DiceTurn(random)
            {
                IsBattleSession = true, BattleRerollLimit = rerollLimit, SelectiveReroll = selectiveReroll
            };
        }

        public DiceTurn(IRandomSource random, int startingEnergy = 0)
        {
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            Energy = Math.Max(0, Math.Min(MaxEnergy, startingEnergy));
            readOnlyValues = Array.AsReadOnly(values);
            readOnlyHeld = Array.AsReadOnly(held);
        }

        public IReadOnlyList<int> Values => readOnlyValues;
        public IReadOnlyList<bool> Held => readOnlyHeld;
        public int RerollsRemaining => IsBattleSession ? BattleRerollLimit - UsedRerolls : legacyRerolls;
        public int Energy { get; private set; }
        public bool CanEnergyReroll => IsBattleSession
            ? begun && RerollsRemaining > 0 && (Energy >= MaxEnergy || FreeRerolls > 0)
            : Energy >= MaxEnergy;
        public DiceHand Hand { get; private set; }

        public void Begin()
        {
            // A battle owns one persistent hand and budget. A new actor must not refill it.
            if (IsBattleSession && begun) return;
            for (int index = 0; index < held.Length; index++) held[index] = false;
            legacyRerolls = InitialRerolls;
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
            if (IsBattleSession) return RerollBattle(true, out error);
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
            legacyRerolls--;
            RefreshHand();
            error = string.Empty;
            return true;
        }

        public bool EnergyRerollAll(out string error)
        {
            if (IsBattleSession) return RerollBattle(false, out error);
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
            if (IsBattleSession)
            {
                AddEnergyMicros((long)amount * EnergyScale);
                return;
            }
            Energy = (int)Math.Min(MaxEnergy, (long)Energy + amount);
        }

        /// <summary>Only actual HP/shield loss counts; callers must remove overkill first.</summary>
        public void RecordDamage(int appliedDamage, bool killed, int gainPermille = 1000)
        {
            if (!IsBattleSession) throw new InvalidOperationException("需要局内骰子状态");
            if (appliedDamage < 0 || gainPermille < 0 || gainPermille > 2000)
                throw new ArgumentOutOfRangeException(nameof(appliedDamage));
            // Keep fractional energy across small hits: 125 one-point hits also earn one energy.
            long earned = (long)appliedDamage * 8000 + (killed ? 25 * EnergyScale : 0);
            AddEnergyMicros(earned * gainPermille / 1000);
        }

        public void GrantFreeReroll()
        {
            if (IsBattleSession && RerollsRemaining > 0) FreeRerolls = Math.Min(1, FreeRerolls + 1);
        }

        /// <summary>Changes command capability without rerolling or refilling any battle budget.</summary>
        public void SetBattleSelectiveReroll(bool enabled)
        {
            if (!IsBattleSession) throw new InvalidOperationException("需要局内骰子状态");
            SelectiveReroll = enabled;
            if (!enabled) Array.Clear(held, 0, held.Length);
        }

        private void AddEnergyMicros(long amount)
        {
            if (RerollsRemaining <= 0) return;
            energyMicros = Math.Min(MaxEnergy * EnergyScale, energyMicros + amount);
            Energy = (int)(energyMicros / EnergyScale);
        }

        private bool RerollBattle(bool selective, out string error)
        {
            if (!CanEnergyReroll)
            {
                error = RerollsRemaining <= 0 ? "本场重投次数已用完" : "造成伤害积攒能量，满100可重投";
                return false;
            }
            if (selective)
            {
                int unheld = 0;
                for (int i = 0; i < held.Length; i++) if (!held[i]) unheld++;
                if (!SelectiveReroll || unheld < 1 || unheld > 2)
                {
                    error = SelectiveReroll ? "请保留3至4颗骰子，只重投1至2颗" : "人鱼队长才能精准重投";
                    return false;
                }
            }
            if (FreeRerolls > 0) FreeRerolls--;
            else { energyMicros = 0; Energy = 0; }
            UsedRerolls++;
            for (int i = 0; i < values.Length; i++)
            {
                if (!selective || !held[i]) values[i] = RollDie();
                if (!selective) held[i] = false;
            }
            RefreshHand();
            error = string.Empty;
            return true;
        }

        private int RollDie() => random.Next(6) + 1;

        private void RollAll()
        {
            for (int index = 0; index < values.Length; index++) values[index] = RollDie();
        }

        private void RefreshHand()
        {
            Hand = DiceRules.Evaluate(values);
            if (IsBattleSession)
            {
                // A hand contributes a bounded additive encore bonus, never a compound multiplier.
                // A reroll always progresses by at least 5%; opening high point is the neutral baseline.
                int gain = (Hand.MultiplierPermille - 1000) / 4;
                if (Revision > 0) gain = Math.Max(50, gain);
                LastBonusGainPermille = Math.Min(gain, MaxBattleBonusPermille - AccumulatedBonusPermille);
                AccumulatedBonusPermille += LastBonusGainPermille;
            }
            Revision++;
        }
    }
}
