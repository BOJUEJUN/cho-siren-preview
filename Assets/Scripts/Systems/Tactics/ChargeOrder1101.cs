using System;
using System.Collections.Generic;
using ChoSiren.Systems.Dice;
namespace ChoSiren.Systems.Tactics
{
    public sealed partial class BattleSimulator
    {
        public static readonly int[] ChargeOrderSpeedPermille = { 1000, 800, 600, 400 };
        readonly List<int> formationOrder1102 = new List<int>();
        readonly Dictionary<int, long> formationChargeRemainder1102 = new Dictionary<int, long>();
        const long ChargeDenominator1102 = 120000000L;
        public IReadOnlyList<int> FormationOrder1102
        {
            get
            {
                if (formationOrder1102.Count == 0)
                    foreach (BattleUnit unit in units)
                        if (unit.Side == BattleSide.Player) formationOrder1102.Add(unit.Id);
                return formationOrder1102.AsReadOnly();
            }
        }
        // Position determines speed; changing captain never reorders the formation.
        public int ChargeRank(BattleUnit unit)
        {
            if (unit == null || unit.Side != BattleSide.Player) return -1;
            _ = FormationOrder1102;
            return formationOrder1102.IndexOf(unit.Id);
        }
        public int ChargeOrderPermille(BattleUnit unit)
        {
            int rank = ChargeRank(unit);
            return rank < 0 ? 1000 : ChargeOrderSpeedPermille[Math.Min(rank, 3)];
        }
        public bool SwapFormationPositions1102(int firstId, int secondId)
        {
            if (!IsTurnBased || Outcome != BattleOutcome.Ongoing || firstId == secondId) return false;
            _ = FormationOrder1102;
            int a = formationOrder1102.IndexOf(firstId), b = formationOrder1102.IndexOf(secondId);
            if (a < 0 || b < 0) return false;
            formationOrder1102[a] = secondId; formationOrder1102[b] = firstId;
            return true;
        }
        public static int CaptainHandChargePermille1102(DicePattern pattern)
        {
            switch (pattern)
            {
                case DicePattern.ThreeKind: case DicePattern.FullHouse: case DicePattern.DoubleThreeKind:
                case DicePattern.Straight: case DicePattern.SixStraight: return 1040;
                case DicePattern.FourKind: case DicePattern.FiveKind: case DicePattern.SixKind: return 1050;
                default: return 1000;
            }
        }
        public int FormationChargeBonus1102(BattleUnit unit) => unit != null && unit.Id == CurrentLeaderId
            ? CaptainHandChargePermille1102(BattleDice?.Hand?.Pattern ?? DicePattern.HighPoint) : 1000;
        public double ChargeSeconds1102(BattleUnit unit) =>
            12000000d / (ChargeOrderPermille(unit) * FormationChargeBonus1102(unit));
        /// <summary>Only effective combat time; the view excludes pause, intros and ultimate movies.</summary>
        public void AdvanceFormationCharge1102(int milliseconds)
        {
            if (milliseconds < 0) throw new ArgumentOutOfRangeException(nameof(milliseconds));
            if (!IsTurnBased || Outcome != BattleOutcome.Ongoing || milliseconds == 0) return;
            foreach (BattleUnit unit in units)
            {
                if (unit.Side != BattleSide.Player || !unit.Alive || UltimatesUsed(unit) >= MaxUltimatesPerMember ||
                    !performerClocks.TryGetValue(unit.Id, out PerformerClock clock) || clock.ReleasingUltimate ||
                    clock.Energy >= MaxUnitEnergy) continue;
                formationChargeRemainder1102.TryGetValue(unit.Id, out long fraction);
                long value = fraction + (long)milliseconds * ChargeOrderPermille(unit) * FormationChargeBonus1102(unit);
                clock.Energy = (int)Math.Min(MaxUnitEnergy, clock.Energy + value / ChargeDenominator1102);
                formationChargeRemainder1102[unit.Id] = clock.Energy >= MaxUnitEnergy ? 0 : value % ChargeDenominator1102;
            }
        }
    }
}
