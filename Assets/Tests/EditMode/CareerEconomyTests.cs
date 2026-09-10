using System;
using System.Linq;
using ChoSiren.Systems.Economy;
using NUnit.Framework;
using UnityEngine;

namespace ChoSiren.Tests
{
    /// <summary>
    /// 面试经济专项（2026-09-09 会议纪要 11:36 追加）：风险与月薪。
    /// 覆盖旧档缺字段迁移、上下限、结算幂等、风险缓解效果、名气→薪资与术语/可负担性边界。
    /// </summary>
    public sealed class CareerEconomyTests
    {
        private DateTime now;

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey(GameModel.SaveKey);
            PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
            now = new DateTime(2026, 9, 2, 12, 0, 0, DateTimeKind.Local);
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(GameModel.SaveKey);
            PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
        }

        private GameModel NewModel() => new GameModel(() => now);

        // ------------------------------------------------------------------ risk (纯计算)

        [Test]
        public void RiskValueIsDeterministicAndBounded()
        {
            for (int index = 0; index < GameModel.Members.Length; index++)
            {
                string id = GameModel.MemberIdAt(index);
                int first = CareerEconomy.RiskValue(id);
                int second = CareerEconomy.RiskValue(id);
                Assert.That(second, Is.EqualTo(first), $"{id} 的风险值必须确定");
                Assert.That(first, Is.InRange(CareerEconomy.RiskMin, CareerEconomy.RiskMax),
                    $"{id} 的风险值必须落在 0–100");
            }
        }

        [Test]
        public void CandidateRiskIsIntrinsicToMemberNotPool()
        {
            // 风险由成员 ID 推导，不把线上渠道一概做成高风险：同一成员无论出现在哪个面试池，
            // 其风险都只取决于成员本身。
            var model = NewModel();
            var candidates = model.InterviewCandidates(0).Concat(model.InterviewCandidates(1)).Distinct();
            foreach (int index in candidates)
                Assert.That(model.RiskOf(index),
                    Is.EqualTo(CareerEconomy.RiskValue(GameModel.MemberIdAt(index))),
                    "候选风险必须由成员本身决定，而非面试渠道");
        }

        [Test]
        public void RiskTiersFollowThresholds()
        {
            Assert.That(CareerEconomy.RiskTier(0), Is.EqualTo("低"));
            Assert.That(CareerEconomy.RiskTier(33), Is.EqualTo("低"));
            Assert.That(CareerEconomy.RiskTier(34), Is.EqualTo("中"));
            Assert.That(CareerEconomy.RiskTier(66), Is.EqualTo("中"));
            Assert.That(CareerEconomy.RiskTier(67), Is.EqualTo("高"));
            Assert.That(CareerEconomy.RiskTier(100), Is.EqualTo("高"));
        }

        [Test]
        public void RiskFactorsAreExplicitAndBounded()
        {
            Assert.That(CareerEconomy.TeamBonusPenalty(0), Is.EqualTo(0));
            Assert.That(CareerEconomy.TeamBonusPenalty(100), Is.EqualTo(-12));
            Assert.That(CareerEconomy.IncomePenalty(0), Is.EqualTo(0));
            Assert.That(CareerEconomy.IncomePenalty(100), Is.EqualTo(-10));

            for (int risk = 0; risk <= 100; risk++)
            {
                Assert.That(CareerEconomy.TeamBonusPenalty(risk), Is.LessThanOrEqualTo(0),
                    "队内摩擦只能造成减益");
                Assert.That(CareerEconomy.IncomePenalty(risk), Is.LessThanOrEqualTo(0),
                    "绯闻压力只能造成减益");
                Assert.That(CareerEconomy.TeamBonusPenalty(risk), Is.InRange(-12, 0));
                Assert.That(CareerEconomy.IncomePenalty(risk), Is.InRange(-10, 0));
            }
        }

        // ------------------------------------------------------------------ salary (纯计算)

        [Test]
        public void EstimatedSalaryGrowsWithFame()
        {
            string id = GameModel.MemberIdAt(0);
            int baseSalary = CareerEconomy.BaseSalary(id);
            Assert.That(baseSalary, Is.InRange(80, 160));

            Assert.That(CareerEconomy.EstimatedSalary(id, 0), Is.EqualTo(baseSalary));
            Assert.That(CareerEconomy.EstimatedSalary(id, 100), Is.EqualTo(baseSalary * 2),
                "名气 100 时薪资应为基础两倍");
            Assert.That(CareerEconomy.EstimatedSalary(id, 50),
                Is.GreaterThanOrEqualTo(baseSalary), "名气提高不得降低薪资");
        }

        [Test]
        public void FameIsStoryProgressAndRaisesModelSalary()
        {
            var model = NewModel();
            int member = model.Save.UnlockedMembers[0];
            int atDefault = model.EstimatedMonthlySalary(member);

            model.Save.StoryProgress = 100;
            int atMax = model.EstimatedMonthlySalary(member);
            Assert.That(atMax, Is.GreaterThan(atDefault), "名气提高后薪资成本应上升");
        }

        // ------------------------------------------------------------------ 旧档迁移

        [Test]
        public void OldSaveMissingCareerFieldsMigratesWithoutBackCharging()
        {
            var existing = new GameSave { Diamonds = 5000, Gold = 9000 };
            PlayerPrefs.SetString(GameModel.SaveKey, JsonUtility.ToJson(existing));
            PlayerPrefs.Save();

            var loaded = NewModel();
            Assert.That(loaded.Save.Gold, Is.EqualTo(9000), "旧档缺字段不得被追缴历史月薪");
            Assert.That(loaded.Save.MemberRisk, Is.Not.Null);
            Assert.That(loaded.Save.MemberRisk.Count, Is.EqualTo(GameModel.Members.Length),
                "迁移应为每名成员补齐风险");
            Assert.That(loaded.Save.MemberRiskMitigationDate.Count, Is.EqualTo(GameModel.Members.Length));
            Assert.That(loaded.Save.SalarySettlementIndex, Is.GreaterThanOrEqualTo(0), "迁移应初始化经营期序号");
        }

        [Test]
        public void OutOfRangeRiskIsClampedOnLoad()
        {
            var existing = new GameSave { Gold = 9000 };
            existing.MemberRisk = Enumerable.Repeat(5000, GameModel.Members.Length).ToList();
            existing.MemberRisk[0] = -40;
            PlayerPrefs.SetString(GameModel.SaveKey, JsonUtility.ToJson(existing));
            PlayerPrefs.Save();

            var loaded = NewModel();
            Assert.That(loaded.RiskOf(0), Is.EqualTo(0), "负风险应钳制到 0");
            for (int index = 1; index < GameModel.Members.Length; index++)
                Assert.That(loaded.RiskOf(index), Is.EqualTo(100), "超上限风险应钳制到 100");
        }

        // ------------------------------------------------------------------ 结算幂等

        [Test]
        public void SettlementIsIdempotentWithinSameMonth()
        {
            var model = NewModel();
            int goldBefore = model.Save.Gold;
            int total = model.TotalTeamMonthlySalary();
            Assert.That(total, Is.GreaterThan(0), "有签约成员时总月薪应为正");

            // 跨入下一个 7 天经营月，应扣一次月薪。
            var later = new GameModel(() => now.AddDays(8));
            Assert.That(later.Save.Gold, Is.EqualTo(goldBefore - total), "跨月应扣除一次月薪");

            // 同月重开不得重复扣。
            var again = new GameModel(() => now.AddDays(8));
            Assert.That(again.Save.Gold, Is.EqualTo(goldBefore - total), "同月重开不得重复扣款");
        }

        [Test]
        public void InsufficientGoldPausesSalaryWithoutDiamondOrMemberLoss()
        {
            var existing = new GameSave { Diamonds = 500, Gold = 10 };
            PlayerPrefs.SetString(GameModel.SaveKey, JsonUtility.ToJson(existing));
            PlayerPrefs.Save();

            var model = NewModel();
            int diamondsBefore = model.Save.Diamonds;
            int unlockedBefore = model.Save.UnlockedMembers.Count;

            var later = new GameModel(() => now.AddDays(8));
            Assert.That(later.Save.Gold, Is.EqualTo(10), "余额不足不得扣星光币");
            Assert.That(later.Save.Diamonds, Is.EqualTo(diamondsBefore), "余额不足不得扣星钻");
            Assert.That(later.Save.UnlockedMembers.Count, Is.EqualTo(unlockedBefore), "余额不足不得移除成员");
            Assert.That(later.SalarySettlementNotice, Does.Contain("不足"), "余额不足需明确提示");
        }

        // ------------------------------------------------------------------ 风险缓解

        [Test]
        public void MitigationReducesRiskCostsGoldAndCoolsDown()
        {
            var model = NewModel();
            int member = model.Save.UnlockedMembers[0];
            model.Save.MemberRisk[member] = 50;
            int goldBefore = model.Save.Gold;

            Assert.That(model.CanMitigateRisk(member, out int cost, out _), Is.True);
            Assert.That(cost, Is.EqualTo(CareerEconomy.MitigationGoldCost));
            Assert.That(model.MitigateRisk(member, out string message), Is.True, message);

            Assert.That(model.RiskOf(member), Is.EqualTo(30), "休整应降低 20 点风险");
            Assert.That(model.Save.Gold, Is.EqualTo(goldBefore - cost), "休整应扣除星光币");
            Assert.That(model.RiskMitigatedToday(member), Is.True);

            Assert.That(model.MitigateRisk(member, out string second), Is.False, "同一天不得重复休整");
            Assert.That(second, Does.Contain("明日"));
        }

        [Test]
        public void MitigationClampsAtZeroAndNeverSpendsWhenBlocked()
        {
            var model = NewModel();
            int member = model.Save.UnlockedMembers[0];
            model.Save.MemberRisk[member] = 10;
            int goldBefore = model.Save.Gold;

            Assert.That(model.MitigateRisk(member, out _), Is.True);
            Assert.That(model.RiskOf(member), Is.EqualTo(0), "风险不得降到负数");

            // 已无风险 + 当日已处理，不得再扣款。
            int goldAfter = model.Save.Gold;
            Assert.That(model.MitigateRisk(member, out string blocked), Is.False);
            Assert.That(model.Save.Gold, Is.EqualTo(goldAfter), "被阻止的休整不得扣款");
            Assert.That(blocked, Does.Contain("风险").Or.Contain("明日"));
        }

        // ------------------------------------------------------------------ 可负担性

        [Test]
        public void ChapterOneNewAccountSalaryIsAffordableWithinOneWeek()
        {
            var model = NewModel();
            int total = model.TotalTeamMonthlySalary();

            // 第一章新号一周内的最低舞台收益（每天 3 场演出，每场 520 星光币）。
            int oneWeekPerformIncome = 3 * GameModel.PerformanceGoldReward * CareerEconomy.SalaryMonthDays;
            Assert.That(total, Is.LessThanOrEqualTo(model.Save.Gold + oneWeekPerformIncome),
                "第一章新号月薪应可在首周舞台收益内负担");
        }
    }
}
