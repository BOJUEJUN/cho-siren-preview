using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ChoSiren.Systems.Economy;
using UnityEngine;

namespace ChoSiren
{
    public sealed partial class GameSave
    {
        /// <summary>第三章 star rewards / tasks (2026-11-11).</summary>
        public List<int> ClaimedChapterThreeStarRewards = new List<int>();
        public List<string> ClaimedChapterThreeTasks = new List<string>();
    }

    /// <summary>
    /// 第三章 (2026-11-11, 赵金 11 月内容): twenty stages after 2-20 with the 第三章 rules (BOSS 护盾, 弹反) and the dice
    /// loadout. Same reward / task popups as the first two chapters.
    /// </summary>
    public sealed partial class GameModel
    {
        public const int ChapterThreeStageCount = 20;
        public const string ChapterThreeTaskClearStageFive = "chapter-3-clear-5";
        public const string ChapterThreeTaskThirtyStars = "chapter-3-stars-30";
        public const string ChapterThreeTaskClearStageTen = "chapter-3-clear-10";
        public const string ChapterThreeTaskClearStageTwenty = "chapter-3-clear-20";

        private static readonly ChapterRewardDefinition[] ChapterThreeStarRewards =
        {
            new ChapterRewardDefinition { RequiredStars = 10, Rewards = new[] { new CurrencyAmount(CurrencyIds.Diamond, 50) } },
            new ChapterRewardDefinition { RequiredStars = 20, Rewards = new[] { new CurrencyAmount(CurrencyIds.Gold, 4000) } },
            new ChapterRewardDefinition
            {
                RequiredStars = 30,
                Rewards = new[] { new CurrencyAmount(CurrencyIds.Diamond, 120), new CurrencyAmount(CurrencyIds.RecruitTicket, 1) }
            },
            new ChapterRewardDefinition { RequiredStars = 40, Rewards = new[] { new CurrencyAmount(CurrencyIds.Diamond, 60) } },
            new ChapterRewardDefinition { RequiredStars = 50, Rewards = new[] { new CurrencyAmount(CurrencyIds.Gold, 6000) } },
            new ChapterRewardDefinition
            {
                RequiredStars = 60,
                Rewards = new[] { new CurrencyAmount(CurrencyIds.Diamond, 150), new CurrencyAmount(CurrencyIds.RecruitTicket, 2) }
            },
        };

        private static readonly ChapterTaskDefinition[] ChapterThreeTasks =
        {
            new ChapterTaskDefinition { Id = ChapterThreeTaskClearStageFive, Title = "首次通关 3-5（解锁破盾骰）", Target = 1,
                Rewards = new[] { new CurrencyAmount(CurrencyIds.Diamond, 20) } },
            new ChapterTaskDefinition { Id = ChapterThreeTaskThirtyStars, Title = "第三章累计获得 30 星", Target = 30,
                Rewards = new[] { new CurrencyAmount(CurrencyIds.Gold, 3000) } },
            new ChapterTaskDefinition { Id = ChapterThreeTaskClearStageTen, Title = "首次通关 3-10（解锁充能骰）", Target = 1,
                Rewards = new[] { new CurrencyAmount(CurrencyIds.RecruitTicket, 1), new CurrencyAmount(CurrencyIds.Diamond, 40) } },
            new ChapterTaskDefinition { Id = ChapterThreeTaskClearStageTwenty, Title = "首次通关 3-20", Target = 1,
                Rewards = new[] { new CurrencyAmount(CurrencyIds.RecruitTicket, 2), new CurrencyAmount(CurrencyIds.Diamond, 60) } },
        };

        private static string ChapterThreeStageId(int stageNumber) => $"stage-3-{stageNumber}";

        private static bool TryGetChapterThreeStageNumber(string stageId, out int stageNumber)
        {
            const string prefix = "stage-3-";
            stageNumber = 0;
            if (string.IsNullOrEmpty(stageId) || !stageId.StartsWith(prefix, StringComparison.Ordinal)) return false;
            if (!int.TryParse(stageId.Substring(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture,
                    out stageNumber)) return false;
            return stageNumber >= 1 && stageNumber <= ChapterThreeStageCount;
        }

        /// <summary>3-1 opens after 2-20; then in order.</summary>
        private bool TryChapterThreeUnlocked(string stageId, out bool unlocked)
        {
            unlocked = false;
            if (!TryGetChapterThreeStageNumber(stageId, out int n)) return false;
            unlocked = n == 1 ? IsStageCleared("stage-2-20") : IsStageCleared(ChapterThreeStageId(n - 1));
            return true;
        }

        public int ChapterThreeTotalStars
        {
            get
            {
                int total = 0;
                for (int stage = 1; stage <= ChapterThreeStageCount; stage++) total += StarsOf(ChapterThreeStageId(stage));
                return total;
            }
        }

        private List<ChapterStarRewardView> ChapterThreeStarRewardViews()
        {
            int stars = ChapterThreeTotalStars;
            return ChapterThreeStarRewards.Select(definition => new ChapterStarRewardView
            {
                RequiredStars = definition.RequiredStars,
                CurrentStars = stars,
                Claimed = Save.ClaimedChapterThreeStarRewards.Contains(definition.RequiredStars),
                Rewards = CopyRewards(definition.Rewards),
            }).ToList();
        }

        private bool TryClaimChapterThreeStarReward(int requiredStars, out string message)
        {
            ChapterRewardDefinition definition = ChapterThreeStarRewards.FirstOrDefault(d => d.RequiredStars == requiredStars);
            if (definition == null) { message = "章节星级奖励档位不存在"; return false; }
            if (Save.ClaimedChapterThreeStarRewards.Contains(requiredStars)) { message = "章节星级奖励已经领取"; return false; }
            int stars = ChapterThreeTotalStars;
            if (stars < requiredStars) { message = $"章节星数不足（{stars}/{requiredStars}）"; return false; }
            GrantRewards(definition.Rewards);
            Save.ClaimedChapterThreeStarRewards.Add(requiredStars);
            Save.ClaimedChapterThreeStarRewards.Sort();
            SaveState();
            message = $"已领取 {requiredStars} 星章节奖励：{FormatRewards(ToRewardTuples(definition.Rewards))}";
            return true;
        }

        private List<ChapterTaskView> ChapterThreeTaskViews() => ChapterThreeTasks.Select(definition => new ChapterTaskView
        {
            Id = definition.Id,
            Title = definition.Title,
            Progress = ChapterThreeTaskProgress(definition.Id),
            Target = definition.Target,
            Claimed = Save.ClaimedChapterThreeTasks.Contains(definition.Id),
            Rewards = CopyRewards(definition.Rewards),
        }).ToList();

        private bool TryClaimChapterThreeTask(string taskId, out string message)
        {
            ChapterTaskDefinition definition = ChapterThreeTasks.FirstOrDefault(d => d.Id == taskId);
            if (definition == null) { message = "章节任务不存在"; return false; }
            if (Save.ClaimedChapterThreeTasks.Contains(taskId)) { message = "章节任务奖励已经领取"; return false; }
            int progress = ChapterThreeTaskProgress(taskId);
            if (progress < definition.Target) { message = $"章节任务尚未完成（{progress}/{definition.Target}）"; return false; }
            GrantRewards(definition.Rewards);
            Save.ClaimedChapterThreeTasks.Add(taskId);
            SaveState();
            message = $"已领取：{definition.Title}（{FormatRewards(ToRewardTuples(definition.Rewards))}）";
            return true;
        }

        private int ChapterThreeTaskProgress(string taskId)
        {
            switch (taskId)
            {
                case ChapterThreeTaskClearStageFive: return IsStageCleared(ChapterThreeStageId(5)) ? 1 : 0;
                case ChapterThreeTaskThirtyStars: return Mathf.Min(30, ChapterThreeTotalStars);
                case ChapterThreeTaskClearStageTen: return IsStageCleared(ChapterThreeStageId(10)) ? 1 : 0;
                case ChapterThreeTaskClearStageTwenty: return IsStageCleared(ChapterThreeStageId(20)) ? 1 : 0;
                default: return 0;
            }
        }
    }
}
