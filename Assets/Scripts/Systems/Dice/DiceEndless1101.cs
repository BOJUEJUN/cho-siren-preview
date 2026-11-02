using System;

namespace ChoSiren.Systems.Dice
{
    /// <summary>
    /// 2026-11-01c 无限骰子 (用户: "我还是想一直能投，但是数值你控制下…做数值上限"). Turn battles (rhythm mode) only.
    /// <list type="bullet">
    /// <item>骰子豆 (reroll pips): a battle starts with <see cref="StartingStoredRerolls"/>, each own member action fills
    /// <see cref="EndlessChargePerAction"/>% of the next one, wave changes preserve the charge, and at most
    /// <see cref="MaxStoredRerolls"/> wait — no per-battle total any more. A reroll spends one pip; the old rules stay:
    /// at most two picked dice, none picked = all six.</item>
    /// <item>收益上限: the dice attack bonus (hand + heat) is capped at <see cref="EndlessBonusCapPermille"/>; the captain
    /// passives stop growing at 四条 (BattleSimulator.PassiveTopPattern), and the hand no longer speeds up ultimate charge
    /// (BattleSimulator.HandEnergyCapPermille = 1000). So endless rerolls make the dice feel alive without stacking power:
    /// once a hand reaches the cap, more rolling is for the fun of the faces, not for more damage.</item>
    /// </list>
    /// </summary>
    public sealed partial class DiceTurn
    {
        public const int MaxStoredRerolls = 3;
        // Opening dice are rolled by Begin(); that roll must not grant spendable rerolls.
        public const int StartingStoredRerolls = 0;
        /// <summary>Charge per own member action, in percent of one pip (34 ≈ one pip every three actions).</summary>
        public const int EndlessChargePerAction = 34;
        /// <summary>骰子加成上限: hand + heat never add more than +5% attack (三条 +3.3%, 顺子 already +5%).</summary>
        public const int EndlessBonusCapPermille = 50;

        // Balance-harness knobs (Builds/qa-logs/zj-1101c/dice-endless/sim); the game uses the constants above.
        internal static int EndlessBonusCap1101 = EndlessBonusCapPermille;
        internal static int EndlessChargePerAction1101 = EndlessChargePerAction;
        internal static int StartingStoredRerolls1101 = StartingStoredRerolls;
        internal static int MaxStoredRerolls1101 = MaxStoredRerolls;

        private bool neutralized1101;

        /// <summary>骰子豆 ready now (turn battles); 0–<see cref="MaxStoredRerolls"/>.</summary>
        public int StoredRerolls { get; private set; }

        /// <summary>Pips a battle can hold.</summary>
        public int StoredRerollCapacity => RhythmMode ? MaxStoredRerolls1101 : BattleRerollLimit;

        /// <summary>Progress towards the next pip, 0–1 (1 while the store is full).</summary>
        public float NextRerollProgress => RhythmMode && StoredRerolls >= MaxStoredRerolls1101 ? 1f
            : Math.Max(0f, Math.Min(1f, energyMicros / (float)(MaxEnergy * EnergyScale)));

        /// <summary>True once the dice attack bonus sits at its cap (骰子加成上限).</summary>
        public bool BonusCapped => IsBattleSession && AccumulatedBonusPermille >= BonusCapPermille;

        /// <summary>Own action charge: endless pips in a turn battle, the legacy bar otherwise.</summary>
        public void ChargeForAction1101(int legacyAmount)
        {
            if (RhythmMode) AddEndlessCharge1101((long)EndlessChargePerAction1101 * EnergyScale);
            else GainEnergy(legacyAmount);
        }

        private void StartEndless1101()
        {
            StoredRerolls = neutralized1101 ? 0 : Math.Min(MaxStoredRerolls1101, StartingStoredRerolls1101);
            energyMicros = 0;
            Energy = 0;
        }

        private void AddEndlessCharge1101(long amount)
        {
            if (neutralized1101) { energyMicros = 0; Energy = 0; return; }
            energyMicros += amount;
            long pip = MaxEnergy * EnergyScale;
            while (energyMicros >= pip && StoredRerolls < MaxStoredRerolls1101)
            {
                energyMicros -= pip;
                StoredRerolls++;
                EarnedRerolls++;
            }
            // A full store wastes the overflow (the ring reads full): spend a pip to start the next one.
            if (StoredRerolls >= MaxStoredRerolls1101) energyMicros = 0;
            SyncEnergy1101();
        }

        private void SyncEnergy1101() => Energy = (int)Math.Min(MaxEnergy, energyMicros / EnergyScale);
    }
}
