namespace ChoSiren.Systems.Tactics
{
    /// <summary>
    /// 2026-11-01 (赵金: "PPT 里面我们角色的充能顺序"). 企划书「大招充能机制：顺位充能 · 队长优先 · 节奏可控」:
    /// 站位顺位决定充能速度 — 第1位（队长）100%、第2位 80%、第3位 60%、第4位 40%（满能 12 / 15 / 20 / 30 秒），
    /// 大招槽独立不共享；换队长 = 改变大招释放顺序。Turn battles apply it to every bar gain (actions, hits, kills,
    /// 队长演出). The order is the formation order with the current captain first, so a captain change re-ranks.
    /// </summary>
    public sealed partial class BattleSimulator
    {
        /// <summary>Charge speed by rank (captain first), in permille.</summary>
        public static readonly int[] ChargeOrderSpeedPermille = { 1000, 800, 600, 400 };

        /// <summary>0 = captain, then formation order (dead members keep their place).</summary>
        public int ChargeRank(BattleUnit unit)
        {
            if (unit == null || unit.Side != BattleSide.Player) return -1;
            if (unit.Id == CurrentLeaderId) return 0;
            int rank = 1;
            foreach (BattleUnit other in units)
            {
                if (other == unit) return rank;
                if (other.Side == BattleSide.Player && other.Id != CurrentLeaderId) rank++;
            }
            return rank;
        }

        public int ChargeOrderPermille(BattleUnit unit)
        {
            int rank = ChargeRank(unit);
            if (rank < 0) return 1000;
            return ChargeOrderSpeedPermille[System.Math.Min(rank, ChargeOrderSpeedPermille.Length - 1)];
        }
    }
}
