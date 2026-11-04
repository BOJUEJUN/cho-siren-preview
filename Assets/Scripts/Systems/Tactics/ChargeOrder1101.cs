using System;
using System.Collections.Generic;
using ChoSiren.Systems.Dice;
namespace ChoSiren.Systems.Tactics
{
    public sealed partial class BattleSimulator
    {
        /// <summary>
        /// 2026-11-03 planning deck (顺位充能 · 队长优先): the captain fills in 17s, then 20s / 25s / 30s for the other
        /// three in formation order (100% / 85% / 68% / 57%). Swapping the captain changes who releases first.
        /// </summary>
        public static readonly int[] ChargeOrderMilliseconds = { 17000, 20000, 25000, 30000 };
        public static readonly int[] ChargeOrderSpeedPermille = { 1000, 850, 680, 567 };
        readonly List<int> formationOrder1102 = new List<int>();
        readonly Dictionary<int, long> formationChargeRemainder1102 = new Dictionary<int, long>();
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
        // The card's slot in the formation (the view places cards and the captain plate by it). Changing captain never
        // reorders the formation; charge speed follows ChargeSpeedRank1103.
        public int ChargeRank(BattleUnit unit)
        {
            if (unit == null || unit.Side != BattleSide.Player) return -1;
            _ = FormationOrder1102;
            return formationOrder1102.IndexOf(unit.Id);
        }
        /// <summary>0 for the captain, then 1..3 for the others in formation order.</summary>
        public int ChargeSpeedRank1103(BattleUnit unit)
        {
            int slot = ChargeRank(unit);
            if (slot < 0) return -1;
            if (unit.Id == CurrentLeaderId) return 0;
            int rank = 1;
            for (int i = 0; i < slot; i++)
                if (formationOrder1102[i] != CurrentLeaderId) rank++;
            return Math.Min(rank, 3);
        }
        int ChargeMilliseconds1103(BattleUnit unit)
        {
            int rank = ChargeSpeedRank1103(unit);
            return ChargeOrderMilliseconds[rank < 0 ? 0 : rank];
        }
        public int ChargeOrderPermille(BattleUnit unit)
        {
            int rank = ChargeSpeedRank1103(unit);
            return rank < 0 ? 1000 : ChargeOrderSpeedPermille[rank];
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
            ChargeMilliseconds1103(unit) / (double)FormationChargeBonus1102(unit);
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
                // Energy accrues as ms × 100 × bonus‰ over (charge ms × 1000); the remainder keeps every fraction.
                long denominator = ChargeMilliseconds1103(unit) * 1000L;
                formationChargeRemainder1102.TryGetValue(unit.Id, out long fraction);
                long value = fraction + (long)milliseconds * MaxUnitEnergy * FormationChargeBonus1102(unit);
                clock.Energy = (int)Math.Min(MaxUnitEnergy, clock.Energy + value / denominator);
                formationChargeRemainder1102[unit.Id] = clock.Energy >= MaxUnitEnergy ? 0 : value % denominator;
            }
        }
    }
}
