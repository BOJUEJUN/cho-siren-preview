using System;
using ChoSiren.Systems.Economy;
using ChoSiren.Systems.Tactics;
using UnityEngine;

namespace ChoSiren
{
    /// <summary>
    /// 经纪经营（风险 + 月薪 + 面试渠道）模型接线。规则由 <see cref="CareerEconomy"/> 提供，这里只负责
    /// 读写存档、幂等结算与 UI 可读接口。
    /// </summary>
    public sealed partial class GameModel
    {
        // ------------------------------------------------------------------ risk

        /// <summary>成员当前风险：已签约成员为休整后的存档值，未签约候选为确定性初始值。</summary>
        public int RiskOf(int memberIndex) =>
            IsValidMemberIndex(memberIndex) && memberIndex < Save.MemberRisk.Count
                ? ClampRisk(Save.MemberRisk[memberIndex])
                : 0;

        public RiskProfile RiskProfileOf(int memberIndex)
        {
            if (!IsValidMemberIndex(memberIndex))
                return new RiskProfile();
            int risk = RiskOf(memberIndex);
            return new RiskProfile
            {
                Value = risk,
                Tier = CareerEconomy.RiskTier(risk),
                TeamBonusPenalty = CareerEconomy.TeamBonusPenalty(risk),
                IncomePenalty = CareerEconomy.IncomePenalty(risk)
            };
        }

        /// <summary>该成员今日是否已处理过风险（每日一次冷却，日历日与签到一致）。</summary>
        public bool RiskMitigatedToday(int memberIndex) =>
            IsValidMemberIndex(memberIndex) && memberIndex < Save.MemberRiskMitigationDate.Count &&
            Save.MemberRiskMitigationDate[memberIndex] == DateKey(Today);

        public bool CanMitigateRisk(int memberIndex, out int cost, out string message)
        {
            cost = 0;
            message = string.Empty;
            if (!IsValidMemberIndex(memberIndex)) { message = "成员不存在"; return false; }
            if (!IsUnlocked(memberIndex)) { message = "尚未签约该成员"; return false; }
            if (RiskOf(memberIndex) <= 0) { message = "该成员目前没有风险压力"; return false; }
            if (RiskMitigatedToday(memberIndex)) { message = "今日已处理过该成员的风险，明日再来"; return false; }

            cost = CareerEconomy.MitigationGoldCost;
            if (Save.Gold < cost) { message = $"星光币不足，处理风险需要 {cost:N0}"; return false; }
            return true;
        }

        /// <summary>
        /// 沟通/休整：消耗星光币、永久降低该成员风险并记录当日冷却。失败不扣款、不改变状态。
        /// </summary>
        public bool MitigateRisk(int memberIndex, out string message)
        {
            if (!CanMitigateRisk(memberIndex, out int cost, out message)) return false;

            int before = RiskOf(memberIndex);
            while (Save.MemberRisk.Count <= memberIndex)
                Save.MemberRisk.Add(CareerEconomy.RiskValue(Members[Save.MemberRisk.Count].Id));
            while (Save.MemberRiskMitigationDate.Count <= memberIndex)
                Save.MemberRiskMitigationDate.Add(string.Empty);

            Save.Gold -= cost;
            Save.MemberRisk[memberIndex] = ClampRisk(before - CareerEconomy.MitigationRiskReduction);
            Save.MemberRiskMitigationDate[memberIndex] = DateKey(Today);
            SaveState();

            message = $"{Members[memberIndex].Name} 的风险已由 {before} 降至 {Save.MemberRisk[memberIndex]}（星光币 -{cost:N0}）";
            return true;
        }

        /// <summary>
        /// 队内摩擦 → 团队加成减益：当前编队成员风险的平均值取 <see cref="CareerEconomy.TeamBonusPenalty"/>，
        /// 一次演出只应用一次，不逐成员叠加、不超上限。
        /// </summary>
        public int TeamFrictionPenaltyPercent()
        {
            if (Save.Team.Count == 0) return 0;
            long sum = 0;
            int count = 0;
            foreach (int index in Save.Team)
            {
                if (!IsValidMemberIndex(index) || !IsUnlocked(index)) continue;
                sum += RiskOf(index);
                count++;
            }
            if (count == 0) return 0;
            return CareerEconomy.TeamBonusPenalty((int)Math.Round((double)sum / count));
        }

        /// <summary>
        /// 绯闻压力 → 经营收入减益：所有已签约成员风险的平均值取 <see cref="CareerEconomy.IncomePenalty"/>，
        /// 一次挂机领取只应用一次。
        /// </summary>
        public int IncomePressurePenaltyPercent()
        {
            long sum = 0;
            int count = 0;
            foreach (int index in Save.UnlockedMembers)
            {
                if (!IsValidMemberIndex(index)) continue;
                sum += RiskOf(index);
                count++;
            }
            if (count == 0) return 0;
            return CareerEconomy.IncomePenalty((int)Math.Round((double)sum / count));
        }

        // ------------------------------------------------------------------ signing channel

        /// <summary>签约渠道：0 = 线上、1 = 线下、-1 = 旧档或其他入口。</summary>
        public int SigningChannelOf(int memberIndex) =>
            IsValidMemberIndex(memberIndex) && memberIndex < Save.MemberSigningChannel.Count
                ? Save.MemberSigningChannel[memberIndex] : -1;

        /// <summary>渠道起点等级偏移：线上低起点、线下高起点，旧入口无偏移。</summary>
        public static int SigningStartingLevelOffset(int channel) =>
            channel == 0 ? CareerEconomy.OnlineStartingLevelOffset
            : channel == 1 ? CareerEconomy.OfflineStartingLevelOffset
            : 0;

        /// <summary>面试卡预览：按渠道起点结算后的签约等级（未签约成员以当前基准等级计算）。</summary>
        public int PreviewSigningLevel(int memberIndex, int channel) =>
            IsValidMemberIndex(memberIndex)
                ? Mathf.Clamp(Save.MemberLevels[memberIndex] + SigningStartingLevelOffset(channel), 1, MaxMemberLevel)
                : 0;

        /// <summary>
        /// 签约时把渠道写入存档：起点等级偏移落到真实属性（培养仍可达满级，不降低旧档角色），
        /// 并把面试卡展示的渠道风险落成成员的初始经营风险，保证「签约前看到的」与「签约后要管的」是同一个数。
        /// </summary>
        private void ApplySigningChannel(int memberIndex, int signingChannel)
        {
            while (Save.MemberSigningChannel.Count <= memberIndex) Save.MemberSigningChannel.Add(-1);
            Save.MemberSigningChannel[memberIndex] = signingChannel;
            int offset = SigningStartingLevelOffset(signingChannel);
            if (offset != 0 && IsValidMemberIndex(memberIndex))
                Save.MemberLevels[memberIndex] = Mathf.Clamp(Save.MemberLevels[memberIndex] + offset, 1, MaxMemberLevel);

            while (Save.MemberRisk.Count <= memberIndex)
                Save.MemberRisk.Add(CareerEconomy.RiskValue(Members[Save.MemberRisk.Count].Id));
            int seededRisk = signingChannel is 0 or 1
                ? InterviewRisk(signingChannel, memberIndex)
                : CareerEconomy.RiskValue(Members[memberIndex].Id);
            Save.MemberRisk[memberIndex] = Mathf.Clamp(seededRisk, CareerEconomy.RiskMin, CareerEconomy.RiskMax);
        }

        // ------------------------------------------------------------------ salary

        /// <summary>名气取剧情进度（0–100），随章节推进上升，带动月薪成本上升。</summary>
        public int Fame => Save.StoryProgress;

        public int EstimatedMonthlySalary(int memberIndex) =>
            IsValidMemberIndex(memberIndex) ? CareerEconomy.EstimatedSalary(Members[memberIndex].Id, Fame) : 0;

        /// <summary>总团队月薪 = 所有已签约成员预估月薪之和（名气越高越贵）。</summary>
        public int TotalTeamMonthlySalary()
        {
            int total = 0;
            foreach (int index in Save.UnlockedMembers)
                if (IsValidMemberIndex(index)) total += EstimatedMonthlySalary(index);
            return total;
        }

        public string SalarySettlementNotice => string.IsNullOrEmpty(Save.SalarySettlementNotice)
            ? string.Empty : Save.SalarySettlementNotice;

        /// <summary>e.g. "3 天后结算月薪（每 7 天一期）"；到期则返回"月薪结算已到期"。</summary>
        public string NextSalarySettlementLabel()
        {
            DateTime now = nowProvider();
            int days = (int)(CareerEconomy.NextSettlement(now).Date - now.Date).TotalDays;
            days = Math.Max(0, days);
            return days <= 0 ? "月薪结算已到期" : $"{days} 天后结算月薪（每 7 天一期）";
        }

        /// <summary>
        /// 工资区域的可见状态（供制作人档案直接展示，不另开弹窗）：待付时明示金额与补足后自动
        /// 支付规则；最近已付时显示结算金额；否则显示下一期倒计时。
        /// </summary>
        public string SalarySettlementStatusLabel()
        {
            int total = TotalTeamMonthlySalary();
            if (total <= 0) return "暂无签约成员，无月薪成本";

            if (Save.SalarySettlementPending)
                return $"本期月薪 {total:N0} 星光币待付 · 金币不足已暂停，补足后自动结算（不扣星钻、不移除成员）";

            string next = NextSalarySettlementLabel();
            if (Save.SalarySettlementIndex >= 0 && !string.IsNullOrEmpty(Save.SalarySettlementNotice))
                return $"{Save.SalarySettlementNotice} {next}";
            return next;
        }

        /// <summary>
        /// 幂等月薪结算：使用持久化单调周期序号。回拨（序号变小）不收费、不倒退；首次启用
        /// （新号或旧档缺字段）只记账不追缴历史；余额不足标记待付，补足后恢复支付且只扣一次。
        /// </summary>
        private bool SettleSalaryIfDue()
        {
            int current = CareerEconomy.PeriodIndex(nowProvider());

            if (Save.SalarySettlementIndex < 0)
            {
                Save.SalarySettlementIndex = current;
                Save.SalarySettlementPending = false;
                Save.SalarySettlementNotice = string.Empty;
                return true;
            }

            if (current < Save.SalarySettlementIndex)
            {
                // 时钟回拨：不收费、不倒退。
                return false;
            }

            if (current == Save.SalarySettlementIndex)
            {
                // 本期已处理；若因余额不足待付，补足后恢复支付。
                return Save.SalarySettlementPending && TryPaySalary();
            }

            // 进入新一期（可能跨多期）：只结当前期，不制造历史欠款。
            Save.SalarySettlementIndex = current;
            Save.SalarySettlementPending = false;
            return TryPaySalary();
        }

        private bool TryPaySalary()
        {
            int total = TotalTeamMonthlySalary();
            if (total <= 0)
            {
                bool changed = Save.SalarySettlementPending || !string.IsNullOrEmpty(Save.SalarySettlementNotice);
                Save.SalarySettlementPending = false;
                Save.SalarySettlementNotice = string.Empty;
                return changed;
            }

            if (Save.Gold < total)
            {
                string notice = $"本期月薪 {total:N0} 星光币不足，已暂停支付；补足星光币后自动结算，不会扣除星钻或移除成员。";
                if (Save.SalarySettlementPending && Save.SalarySettlementNotice == notice) return false;
                Save.SalarySettlementPending = true;
                Save.SalarySettlementNotice = notice;
                return true;
            }

            Save.Gold -= total;
            Save.SalarySettlementPending = false;
            Save.SalarySettlementNotice = $"本期已结算成员月薪 {total:N0} 星光币。";
            return true;
        }

        private static int ClampRisk(int value) =>
            value < CareerEconomy.RiskMin ? CareerEconomy.RiskMin
            : value > CareerEconomy.RiskMax ? CareerEconomy.RiskMax : value;
    }
}
