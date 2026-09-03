using System;
using System.Collections.Generic;
using System.Globalization;

namespace ChoSiren.Systems.Economy
{
    /// <summary>Gameplay events that tasks listen to. Reported by the UI/battle layer.</summary>
    public static class TaskTriggers
    {
        public const string Login = "login";
        public const string CheckIn = "check-in";
        public const string Perform = "perform";
        public const string BattleWin = "battle-win";
        public const string GachaPull = "gacha-pull";
        public const string Train = "train";
        public const string SpendStamina = "spend-stamina";
        public const string ClaimIdle = "claim-idle";
        public const string ReadStory = "read-story";

        public static readonly string[] All =
        {
            Login, CheckIn, Perform, BattleWin, GachaPull, Train, SpendStamina, ClaimIdle, ReadStory
        };

        public static bool IsKnown(string id) => Array.IndexOf(All, id) >= 0;
    }

    public static class TaskCadence
    {
        public const string Daily = "daily";
        public const string Weekly = "weekly";
    }

    [Serializable]
    public sealed class TaskDefinition
    {
        public string Id = string.Empty;
        public string Title = string.Empty;
        public string Cadence = TaskCadence.Daily;
        public string Trigger = TaskTriggers.Perform;
        public int Target = 1;
        public CurrencyAmount Reward = new CurrencyAmount(CurrencyIds.Diamond, 50);

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(Id))
            {
                error = "任务缺少 ID";
                return false;
            }

            if (string.IsNullOrWhiteSpace(Title))
            {
                error = $"任务 {Id} 缺少标题";
                return false;
            }

            if (Cadence != TaskCadence.Daily && Cadence != TaskCadence.Weekly)
            {
                error = $"任务 {Id} 的周期无效：{Cadence}";
                return false;
            }

            if (!TaskTriggers.IsKnown(Trigger))
            {
                error = $"任务 {Id} 的触发事件未知：{Trigger}";
                return false;
            }

            if (Target <= 0)
            {
                error = $"任务 {Id} 的目标次数必须大于 0";
                return false;
            }

            if (Reward == null || Reward.Amount <= 0 || !CurrencyIds.IsKnown(Reward.Currency))
            {
                error = $"任务 {Id} 的奖励无效";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }

    [Serializable]
    public sealed class TaskProgress
    {
        public string Id = string.Empty;
        public int Progress;
        public bool Claimed;
    }

    /// <summary>Persisted part of the task board. Serializable so it can live inside the save.</summary>
    [Serializable]
    public sealed class TaskBoardState
    {
        public string DailyKey = string.Empty;
        public string WeeklyKey = string.Empty;
        public List<TaskProgress> Entries = new List<TaskProgress>();
    }

    public sealed class TaskView
    {
        public TaskDefinition Definition;
        public int Progress;
        public bool Claimed;
        public bool Completed => Progress >= Definition.Target;
        public bool Claimable => Completed && !Claimed;
    }

    public static class PeriodKeys
    {
        public static string Daily(DateTime localTime) =>
            localTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        /// <summary>ISO-8601 week (Monday start) so weekly resets line up with most live games.</summary>
        public static string Weekly(DateTime localTime)
        {
            // ISO weeks belong to the year containing their Thursday. Implemented by hand so it
            // behaves identically on every scripting backend and API compatibility level.
            DateTime date = localTime.Date;
            int mondayBased = ((int)date.DayOfWeek + 6) % 7;
            DateTime thursday = date.AddDays(3 - mondayBased);
            int year = thursday.Year;
            int week = (thursday.DayOfYear - 1) / 7 + 1;
            return year.ToString("0000", CultureInfo.InvariantCulture) + "-W" +
                   week.ToString("00", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// Daily/weekly task logic with zero Unity dependencies. Call <see cref="Refresh"/> with the
    /// current local time before reading or mutating so period rollovers are applied lazily.
    /// </summary>
    public static class TaskBoard
    {
        public static bool Refresh(TaskBoardState state, IReadOnlyList<TaskDefinition> definitions, DateTime localNow)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));

            string daily = PeriodKeys.Daily(localNow);
            string weekly = PeriodKeys.Weekly(localNow);
            bool changed = false;

            if (state.DailyKey != daily)
            {
                ResetCadence(state, definitions, TaskCadence.Daily);
                state.DailyKey = daily;
                changed = true;
            }

            if (state.WeeklyKey != weekly)
            {
                ResetCadence(state, definitions, TaskCadence.Weekly);
                state.WeeklyKey = weekly;
                changed = true;
            }

            return changed;
        }

        /// <summary>Advances every task listening to <paramref name="trigger"/>. Returns how many changed.</summary>
        public static int Report(TaskBoardState state, IReadOnlyList<TaskDefinition> definitions, string trigger,
            int amount = 1)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            if (amount <= 0) return 0;

            int changed = 0;
            for (int index = 0; index < definitions.Count; index++)
            {
                TaskDefinition definition = definitions[index];
                if (definition.Trigger != trigger) continue;

                TaskProgress entry = GetOrCreate(state, definition.Id);
                if (entry.Claimed || entry.Progress >= definition.Target) continue;

                entry.Progress = Math.Min(definition.Target, entry.Progress + amount);
                changed++;
            }

            return changed;
        }

        public static bool TryClaim(TaskBoardState state, IReadOnlyList<TaskDefinition> definitions, string taskId,
            out CurrencyAmount reward, out string message)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));

            TaskDefinition definition = null;
            for (int index = 0; index < definitions.Count; index++)
            {
                if (definitions[index].Id == taskId)
                {
                    definition = definitions[index];
                    break;
                }
            }

            reward = null;
            if (definition == null)
            {
                message = "任务不存在";
                return false;
            }

            TaskProgress entry = GetOrCreate(state, taskId);
            if (entry.Claimed)
            {
                message = "奖励已经领取";
                return false;
            }

            if (entry.Progress < definition.Target)
            {
                message = $"任务尚未完成（{entry.Progress}/{definition.Target}）";
                return false;
            }

            entry.Claimed = true;
            reward = new CurrencyAmount(definition.Reward.Currency, definition.Reward.Amount);
            message = $"已领取：{definition.Title}";
            return true;
        }

        public static List<TaskView> Views(TaskBoardState state, IReadOnlyList<TaskDefinition> definitions,
            string cadence = null)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));

            var views = new List<TaskView>(definitions.Count);
            for (int index = 0; index < definitions.Count; index++)
            {
                TaskDefinition definition = definitions[index];
                if (cadence != null && definition.Cadence != cadence) continue;
                TaskProgress entry = Find(state, definition.Id);
                views.Add(new TaskView
                {
                    Definition = definition,
                    Progress = entry?.Progress ?? 0,
                    Claimed = entry?.Claimed ?? false
                });
            }

            return views;
        }

        public static int ClaimableCount(TaskBoardState state, IReadOnlyList<TaskDefinition> definitions)
        {
            int count = 0;
            List<TaskView> views = Views(state, definitions);
            for (int index = 0; index < views.Count; index++)
                if (views[index].Claimable) count++;
            return count;
        }

        private static void ResetCadence(TaskBoardState state, IReadOnlyList<TaskDefinition> definitions,
            string cadence)
        {
            for (int index = 0; index < definitions.Count; index++)
            {
                if (definitions[index].Cadence != cadence) continue;
                TaskProgress entry = Find(state, definitions[index].Id);
                if (entry == null) continue;
                entry.Progress = 0;
                entry.Claimed = false;
            }
        }

        private static TaskProgress Find(TaskBoardState state, string id)
        {
            for (int index = 0; index < state.Entries.Count; index++)
                if (state.Entries[index].Id == id) return state.Entries[index];
            return null;
        }

        private static TaskProgress GetOrCreate(TaskBoardState state, string id)
        {
            TaskProgress entry = Find(state, id);
            if (entry != null) return entry;
            entry = new TaskProgress { Id = id };
            state.Entries.Add(entry);
            return entry;
        }
    }
}
