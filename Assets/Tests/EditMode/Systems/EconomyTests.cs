using System;
using System.Collections.Generic;
using ChoSiren.Systems.Economy;
using NUnit.Framework;

namespace ChoSiren.Tests.Systems
{
    public sealed class StaminaRegenTests
    {
        private static EconomyConfig Config() => new EconomyConfig
        {
            StaminaMax = 120,
            StaminaRegenSeconds = 360,
            StaminaPerTick = 1
        };

        [Test]
        public void RecoversOnePointPerIntervalAndKeepsRemainder()
        {
            StaminaSnapshot result = StaminaRegen.Apply(50, 1000, 1000 + 360 * 3 + 200, Config());

            Assert.That(result.Stamina, Is.EqualTo(53));
            Assert.That(result.Recovered, Is.EqualTo(3));
            Assert.That(result.LastRegenUnixSeconds, Is.EqualTo(1000 + 360 * 3), "余下 200 秒进度必须保留");
        }

        [Test]
        public void CapsAtMaxAndAnchorsTimerToNow()
        {
            StaminaSnapshot result = StaminaRegen.Apply(118, 0, 360 * 100, Config());

            Assert.That(result.Stamina, Is.EqualTo(120));
            Assert.That(result.Recovered, Is.EqualTo(2));
            Assert.That(result.LastRegenUnixSeconds, Is.EqualTo(360 * 100));
        }

        [Test]
        public void OverCapStaminaIsKeptButDoesNotRegenerate()
        {
            StaminaSnapshot result = StaminaRegen.Apply(150, 0, 999999, Config());

            Assert.That(result.Stamina, Is.EqualTo(150));
            Assert.That(result.Recovered, Is.Zero);
        }

        [Test]
        public void ClockGoingBackwardsGrantsNothing()
        {
            StaminaSnapshot result = StaminaRegen.Apply(40, 5000, 1000, Config());

            Assert.That(result.Stamina, Is.EqualTo(40));
            Assert.That(result.LastRegenUnixSeconds, Is.EqualTo(1000), "时间倒退后锚点应重置，避免未来一次性补发");
        }

        [Test]
        public void SpendingFromFullRestartsTheTimer()
        {
            Assert.That(StaminaRegen.TrySpend(120, 8, 0, 7777, 120, out StaminaSnapshot spent), Is.True);
            Assert.That(spent.Stamina, Is.EqualTo(112));
            Assert.That(spent.LastRegenUnixSeconds, Is.EqualTo(7777));

            Assert.That(StaminaRegen.TrySpend(5, 8, 100, 200, 120, out StaminaSnapshot denied), Is.False);
            Assert.That(denied.Stamina, Is.EqualTo(5));
        }

        [Test]
        public void SecondsUntilNextPointCountsDown()
        {
            var snapshot = new StaminaSnapshot(10, 1000, 0);
            Assert.That(snapshot.SecondsUntilNextPoint(1100, 360, 120), Is.EqualTo(260));
            Assert.That(new StaminaSnapshot(120, 1000, 0).SecondsUntilNextPoint(1100, 360, 120), Is.Zero);
        }
    }

    public sealed class IdleIncomeTests
    {
        private static EconomyConfig Config() => new EconomyConfig
        {
            IdleGoldPerHour = 900,
            IdleDiamondPerHour = 6,
            IdleCapHours = 12
        };

        [Test]
        public void AccruesLinearlyBelowCap()
        {
            IdleIncomeReport report = IdleIncome.Compute(0, 3600 * 2, Config());

            Assert.That(report.Capped, Is.False);
            Assert.That(report.AmountOf(CurrencyIds.Gold), Is.EqualTo(1800));
            Assert.That(report.AmountOf(CurrencyIds.Diamond), Is.EqualTo(12));
        }

        [Test]
        public void StopsAtTwelveHours()
        {
            IdleIncomeReport report = IdleIncome.Compute(0, 3600 * 30, Config());

            Assert.That(report.Capped, Is.True);
            Assert.That(report.CreditedSeconds, Is.EqualTo(3600 * 12));
            Assert.That(report.AmountOf(CurrencyIds.Gold), Is.EqualTo(900 * 12));
        }

        [Test]
        public void SubMinuteClaimsAreRejected()
        {
            Assert.That(IdleIncome.CanClaim(1000, 1059), Is.False);
            Assert.That(IdleIncome.CanClaim(1000, 1060), Is.True);
        }
    }

    public sealed class TaskBoardTests
    {
        private static List<TaskDefinition> Definitions() => new List<TaskDefinition>
        {
            new TaskDefinition { Id = "d-perform", Title = "演出", Cadence = TaskCadence.Daily, Trigger = TaskTriggers.Perform, Target = 3, Reward = new CurrencyAmount(CurrencyIds.Diamond, 100) },
            new TaskDefinition { Id = "d-login", Title = "登录", Cadence = TaskCadence.Daily, Trigger = TaskTriggers.Login, Target = 1, Reward = new CurrencyAmount(CurrencyIds.Diamond, 50) },
            new TaskDefinition { Id = "w-win", Title = "周胜利", Cadence = TaskCadence.Weekly, Trigger = TaskTriggers.BattleWin, Target = 10, Reward = new CurrencyAmount(CurrencyIds.RecruitTicket, 1) },
        };

        private static readonly DateTime Monday = new DateTime(2026, 9, 7, 9, 0, 0);

        [Test]
        public void ProgressClampsAtTargetAndBecomesClaimable()
        {
            var state = new TaskBoardState();
            List<TaskDefinition> definitions = Definitions();
            TaskBoard.Refresh(state, definitions, Monday);

            Assert.That(TaskBoard.Report(state, definitions, TaskTriggers.Perform, 5), Is.EqualTo(1));
            List<TaskView> views = TaskBoard.Views(state, definitions, TaskCadence.Daily);

            Assert.That(views[0].Progress, Is.EqualTo(3));
            Assert.That(views[0].Claimable, Is.True);
            Assert.That(views[1].Claimable, Is.False);
            Assert.That(TaskBoard.ClaimableCount(state, definitions), Is.EqualTo(1));
        }

        [Test]
        public void ClaimingPaysOnceAndRefusesTwice()
        {
            var state = new TaskBoardState();
            List<TaskDefinition> definitions = Definitions();
            TaskBoard.Refresh(state, definitions, Monday);
            TaskBoard.Report(state, definitions, TaskTriggers.Login);

            Assert.That(TaskBoard.TryClaim(state, definitions, "d-login", out CurrencyAmount reward, out _), Is.True);
            Assert.That(reward.Currency, Is.EqualTo(CurrencyIds.Diamond));
            Assert.That(reward.Amount, Is.EqualTo(50));
            Assert.That(TaskBoard.TryClaim(state, definitions, "d-login", out _, out string message), Is.False);
            Assert.That(message, Does.Contain("已经领取"));
            Assert.That(TaskBoard.TryClaim(state, definitions, "d-perform", out _, out message), Is.False);
            Assert.That(message, Does.Contain("尚未完成"));
        }

        [Test]
        public void DailyResetsNextDayWhileWeeklySurvivesUntilMonday()
        {
            var state = new TaskBoardState();
            List<TaskDefinition> definitions = Definitions();
            TaskBoard.Refresh(state, definitions, Monday);
            TaskBoard.Report(state, definitions, TaskTriggers.Perform, 2);
            TaskBoard.Report(state, definitions, TaskTriggers.BattleWin, 4);

            Assert.That(TaskBoard.Refresh(state, definitions, Monday.AddDays(1)), Is.True);
            Assert.That(TaskBoard.Views(state, definitions, TaskCadence.Daily)[0].Progress, Is.Zero);
            Assert.That(TaskBoard.Views(state, definitions, TaskCadence.Weekly)[0].Progress, Is.EqualTo(4));

            Assert.That(TaskBoard.Refresh(state, definitions, Monday.AddDays(6)), Is.True, "周日仍是同一周，仅日常重置");
            Assert.That(TaskBoard.Views(state, definitions, TaskCadence.Weekly)[0].Progress, Is.EqualTo(4));

            Assert.That(TaskBoard.Refresh(state, definitions, Monday.AddDays(7)), Is.True);
            Assert.That(TaskBoard.Views(state, definitions, TaskCadence.Weekly)[0].Progress, Is.Zero);
        }

        [Test]
        public void RefreshIsIdempotentWithinTheSamePeriod()
        {
            var state = new TaskBoardState();
            List<TaskDefinition> definitions = Definitions();
            Assert.That(TaskBoard.Refresh(state, definitions, Monday), Is.True);
            Assert.That(TaskBoard.Refresh(state, definitions, Monday.AddHours(10)), Is.False);
        }

        [Test]
        public void IsoWeekKeysHandleYearBoundary()
        {
            Assert.That(PeriodKeys.Weekly(new DateTime(2026, 12, 31)), Is.EqualTo("2026-W53"));
            Assert.That(PeriodKeys.Weekly(new DateTime(2027, 1, 3)), Is.EqualTo("2026-W53"), "2027-01-03 是周日，仍属 2026 年第 53 周");
            Assert.That(PeriodKeys.Weekly(new DateTime(2027, 1, 4)), Is.EqualTo("2027-W01"));
            Assert.That(PeriodKeys.Daily(new DateTime(2026, 9, 3, 23, 59, 0)), Is.EqualTo("2026-09-03"));
        }

        [Test]
        public void ConfigValidationCatchesBadTasks()
        {
            var config = new EconomyConfig();
            config.Tasks.Add(new TaskDefinition { Id = "a", Title = "甲", Trigger = "fly-to-moon" });
            Assert.That(config.TryValidate(out string error), Is.False);
            Assert.That(error, Does.Contain("触发事件未知"));

            config.Tasks.Clear();
            config.Tasks.Add(new TaskDefinition { Id = "a", Title = "甲" });
            config.Tasks.Add(new TaskDefinition { Id = "a", Title = "乙" });
            Assert.That(config.TryValidate(out error), Is.False);
            Assert.That(error, Does.Contain("重复"));
        }
    }
}
