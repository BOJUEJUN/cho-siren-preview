using System;
using System.Collections.Generic;

namespace ChoSiren.Systems.Economy
{
    public sealed class IdleIncomeReport
    {
        public long ElapsedSeconds;
        public long CreditedSeconds;
        public bool Capped;
        public List<CurrencyAmount> Rewards = new List<CurrencyAmount>();

        public int AmountOf(string currency)
        {
            for (int index = 0; index < Rewards.Count; index++)
                if (Rewards[index].Currency == currency) return Rewards[index].Amount;
            return 0;
        }
    }

    /// <summary>
    /// NIKKE-style outpost income: resources accrue linearly while away, up to a cap that
    /// makes logging in at least once or twice a day the optimal play.
    /// </summary>
    public static class IdleIncome
    {
        public static IdleIncomeReport Compute(long lastClaimUnixSeconds, long nowUnixSeconds, EconomyConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            var report = new IdleIncomeReport();
            long elapsed = Math.Max(0, nowUnixSeconds - lastClaimUnixSeconds);
            long cap = (long)config.IdleCapHours * 3600L;
            report.ElapsedSeconds = elapsed;
            report.Capped = elapsed >= cap;
            report.CreditedSeconds = Math.Min(elapsed, cap);

            int gold = (int)(report.CreditedSeconds * config.IdleGoldPerHour / 3600L);
            int diamonds = (int)(report.CreditedSeconds * config.IdleDiamondPerHour / 3600L);
            if (gold > 0) report.Rewards.Add(new CurrencyAmount(CurrencyIds.Gold, gold));
            if (diamonds > 0) report.Rewards.Add(new CurrencyAmount(CurrencyIds.Diamond, diamonds));
            return report;
        }

        /// <summary>
        /// Claiming below one full minute is rejected so a player cannot spam-claim to
        /// harvest rounding, and so the UI can show "nothing to collect yet".
        /// </summary>
        public static bool CanClaim(long lastClaimUnixSeconds, long nowUnixSeconds) =>
            nowUnixSeconds - lastClaimUnixSeconds >= 60;
    }
}
