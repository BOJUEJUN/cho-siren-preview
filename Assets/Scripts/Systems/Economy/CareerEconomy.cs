using System;

namespace ChoSiren.Systems.Economy
{
    /// <summary>
    /// 成员经纪经营的纯计算规则：风险与月薪。所有数值都由成员 ID 与名气（剧情进度）确定性
    /// 推导，不依赖存档、不随每帧漂移，便于 UI 展示与单元测试。与品质/战力/面试渠道解耦。
    ///
    /// 术语与单位（统一）：
    ///   加成(bonus) = 百分比 %，来自技能/饰品，与风险无关；
    ///   收入(income) = 星光币，来自演出/关卡/挂机；
    ///   月薪(salary) = 星光币/经营期，签约成员的成本；
    ///   风险(risk)   = 0–100 无单位，通过虚构个性因素影响加成或收入。
    /// </summary>
    public static class CareerEconomy
    {
        public const int RiskMin = 0;
        public const int RiskMax = 100;

        /// <summary>沟通/休整：每次消耗星光币、降低风险点数，冷却为每成员每日一次。</summary>
        public const int MitigationGoldCost = 400;
        public const int MitigationRiskReduction = 20;

        /// <summary>面试渠道起点：线上低价低起点、线下高价高起点。培养后线上同样可达满级。</summary>
        public const int OnlineStartingLevelOffset = -10;
        public const int OfflineStartingLevelOffset = 8;

        /// <summary>模拟经营月：每 7 个自然日结算一次月薪，复用每日时间轴，不强加现实 30 天等待。</summary>
        public const int SalaryMonthDays = 7;

        /// <summary>模拟经营月的纪元锚点，仅用于把日期折算成连续天数，不参与存档。</summary>
        public static readonly DateTime SalaryEpoch = new DateTime(2026, 1, 1);

        // ---------------------------------------------------------------- risk

        /// <summary>风险值由成员 ID 确定性推导（0–100），与稀有度/战力/面试渠道无关。</summary>
        public static int RiskValue(string memberId)
        {
            uint hash = Fnv1a("risk|" + (memberId ?? string.Empty));
            return (int)(hash % (uint)(RiskMax - RiskMin + 1));
        }

        public static string RiskTier(int risk) => risk <= 33 ? "低" : risk <= 66 ? "中" : "高";

        /// <summary>状态色只是辅助，不替代文字等级。低=绿 / 中=橙 / 高=红。</summary>
        public static string RiskTierColorHex(int risk) =>
            risk <= 33 ? "#3B6D11" : risk <= 66 ? "#B07A1A" : "#B23A3A";

        /// <summary>队内摩擦 → 团队加成减益（负百分比）。风险越高摩擦越重。</summary>
        public static int TeamBonusPenalty(int risk) => -Clamp(risk / 8, 0, 12);

        /// <summary>绯闻压力 → 经营收入减益（负百分比）。</summary>
        public static int IncomePenalty(int risk) => -Clamp(risk / 10, 0, 10);

        /// <summary>
        /// 对星光币奖励应用负百分比减益，只应用一次、不逐成员叠加。penalty ≤ 0（如 -8），
        /// 结果向下取整；penalty ≥ 0 时原样返回。奖励不会因减益变为负数。
        /// </summary>
        public static int ApplyPenalty(int amount, int penaltyPercent)
        {
            if (amount <= 0) return 0;
            if (penaltyPercent >= 0) return amount;
            int multiplier = Math.Max(0, 100 + penaltyPercent);
            return amount * multiplier / 100;
        }

        // ---------------------------------------------------------------- salary

        /// <summary>基础月薪（星光币/经营期），由成员 ID 确定性推导：80–160。</summary>
        public static int BaseSalary(string memberId)
        {
            uint hash = Fnv1a("salary|" + (memberId ?? string.Empty));
            return 80 + (int)(hash % 81u);
        }

        /// <summary>名气（剧情进度）提高后薪资成本上升：0 → 1.0 倍，100 → 2.0 倍。</summary>
        public static float FameMultiplier(int fame) => 1f + Clamp(fame, 0, 100) / 100f;

        /// <summary>预估月薪 = 基础月薪 × 名气倍率，签约前即可明示。</summary>
        public static int EstimatedSalary(string memberId, int fame) =>
            RoundToInt(BaseSalary(memberId) * FameMultiplier(fame));

        // ---------------------------------------------------------------- timeline

        /// <summary>
        /// 单调周期序号：epoch 起每 7 天为一期。时间向前推进序号单调不减；回拨得到较小序号，
        /// 用于幂等结算时即可判定“不收费、不倒退”。
        /// </summary>
        public static int PeriodIndex(DateTime now)
        {
            int days = (int)(now.Date - SalaryEpoch.Date).TotalDays;
            return days < 0 ? 0 : days / SalaryMonthDays;
        }

        /// <summary>当前经营期开始时间（用于展示下次结算时间）。</summary>
        public static DateTime PeriodStart(DateTime now) => SalaryEpoch.AddDays(PeriodIndex(now) * SalaryMonthDays);

        /// <summary>下次结算时间 = 当前经营期开始 + 7 天。</summary>
        public static DateTime NextSettlement(DateTime now) => PeriodStart(now).AddDays(SalaryMonthDays);

        // ---------------------------------------------------------------- helpers

        private static uint Fnv1a(string key)
        {
            uint hash = 2166136261;
            for (int index = 0; index < key.Length; index++)
            {
                hash ^= key[index];
                hash *= 16777619;
            }

            return hash;
        }

        private static int Clamp(int value, int min, int max) =>
            value < min ? min : value > max ? max : value;

        private static int RoundToInt(float value) => (int)Math.Floor(value + 0.5f);
    }

    /// <summary>某成员的风险快照：数值、档位与两则虚构个性因素的具体影响幅度。</summary>
    public sealed class RiskProfile
    {
        public int Value;
        public string Tier = string.Empty;
        public int TeamBonusPenalty;
        public int IncomePenalty;
    }
}
