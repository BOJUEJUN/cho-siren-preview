using System.Collections.Generic;
using System.Linq;
using ChoSiren.Systems.Economy;
using UnityEngine;

namespace ChoSiren
{
    /// <summary>
    /// Chapter two star rewards and chapter tasks (2026-10-21). Chapter one keeps its original API;
    /// the chapter-number overloads let the map show either chapter with the same P5 popups.
    /// </summary>
    public sealed partial class GameModel
    {
        public const string ChapterTwoTaskClearStageThree = "chapter-2-clear-3";
        public const string ChapterTwoTaskFifteenStars = "chapter-2-stars-15";
        public const string ChapterTwoTaskClearFinalStage = "chapter-2-clear-10";
        public const string ChapterTwoTaskFortyFiveStars = "chapter-2-stars-45";
        public const string ChapterTwoTaskClearStageTwenty = "chapter-2-clear-20";

        private static readonly ChapterRewardDefinition[] ChapterTwoStarRewards =
        {
            new ChapterRewardDefinition { RequiredStars = 10, Rewards = new[] { new CurrencyAmount(CurrencyIds.Diamond, 40) } },
            new ChapterRewardDefinition { RequiredStars = 20, Rewards = new[] { new CurrencyAmount(CurrencyIds.Gold, 3000) } },
            new ChapterRewardDefinition
            {
                RequiredStars = 30,
                Rewards = new[] { new CurrencyAmount(CurrencyIds.Diamond, 100), new CurrencyAmount(CurrencyIds.RecruitTicket, 1) }
            },
            // 2026-11-11: twenty stages → 60 stars.
            new ChapterRewardDefinition { RequiredStars = 40, Rewards = new[] { new CurrencyAmount(CurrencyIds.Diamond, 50) } },
            new ChapterRewardDefinition { RequiredStars = 50, Rewards = new[] { new CurrencyAmount(CurrencyIds.Gold, 5000) } },
            new ChapterRewardDefinition
            {
                RequiredStars = 60,
                Rewards = new[] { new CurrencyAmount(CurrencyIds.Diamond, 120), new CurrencyAmount(CurrencyIds.RecruitTicket, 1) }
            },
        };

        private static readonly ChapterTaskDefinition[] ChapterTwoTasks =
        {
            new ChapterTaskDefinition { Id = ChapterTwoTaskClearStageThree, Title = "首次通关 2-3", Target = 1,
                Rewards = new[] { new CurrencyAmount(CurrencyIds.Diamond, 15) } },
            new ChapterTaskDefinition { Id = ChapterTwoTaskFifteenStars, Title = "第二章累计获得 15 星", Target = 15,
                Rewards = new[] { new CurrencyAmount(CurrencyIds.Gold, 1500) } },
            new ChapterTaskDefinition { Id = ChapterTwoTaskClearFinalStage, Title = "首次通关 2-10", Target = 1,
                Rewards = new[] { new CurrencyAmount(CurrencyIds.RecruitTicket, 1), new CurrencyAmount(CurrencyIds.Diamond, 40) } },
            new ChapterTaskDefinition { Id = ChapterTwoTaskFortyFiveStars, Title = "第二章累计获得 45 星", Target = 45,
                Rewards = new[] { new CurrencyAmount(CurrencyIds.Gold, 3000) } },
            new ChapterTaskDefinition { Id = ChapterTwoTaskClearStageTwenty, Title = "首次通关 2-20", Target = 1,
                Rewards = new[] { new CurrencyAmount(CurrencyIds.RecruitTicket, 1), new CurrencyAmount(CurrencyIds.Diamond, 50) } },
        };

        private static string ChapterTwoStageId(int stageNumber) => $"stage-2-{stageNumber}";

        public int ChapterTwoTotalStars
        {
            get
            {
                int total = 0;
                for (int stage = 1; stage <= ChapterOneStageCount; stage++) total += StarsOf(ChapterTwoStageId(stage));
                return total;
            }
        }

        public int ChapterTotalStars(int chapter) => chapter == 3 ? ChapterThreeTotalStars : chapter == 2 ? ChapterTwoTotalStars : ChapterOneTotalStars;

        public List<ChapterStarRewardView> ChapterStarRewardViews(int chapter)
        {
            if (chapter == 3) return ChapterThreeStarRewardViews();
            if (chapter != 2) return ChapterOneStarRewardViews();
            int stars = ChapterTwoTotalStars;
            return ChapterTwoStarRewards.Select(definition => new ChapterStarRewardView
            {
                RequiredStars = definition.RequiredStars,
                CurrentStars = stars,
                Claimed = Save.ClaimedChapterTwoStarRewards.Contains(definition.RequiredStars),
                Rewards = CopyRewards(definition.Rewards),
            }).ToList();
        }

        public int ChapterClaimableStarRewardCount(int chapter) => ChapterStarRewardViews(chapter).Count(view => view.Claimable);

        public bool TryClaimChapterStarReward(int chapter, int requiredStars, out string message)
        {
            if (chapter == 3) return TryClaimChapterThreeStarReward(requiredStars, out message);
            if (chapter != 2) return TryClaimChapterOneStarReward(requiredStars, out message);
            ChapterRewardDefinition definition = ChapterTwoStarRewards.FirstOrDefault(d => d.RequiredStars == requiredStars);
            if (definition == null) { message = "章节星级奖励档位不存在"; return false; }
            if (Save.ClaimedChapterTwoStarRewards.Contains(requiredStars)) { message = "章节星级奖励已经领取"; return false; }
            int stars = ChapterTwoTotalStars;
            if (stars < requiredStars) { message = $"章节星数不足（{stars}/{requiredStars}）"; return false; }
            GrantRewards(definition.Rewards);
            Save.ClaimedChapterTwoStarRewards.Add(requiredStars);
            Save.ClaimedChapterTwoStarRewards.Sort();
            SaveState();
            message = $"已领取 {requiredStars} 星章节奖励：{FormatRewards(ToRewardTuples(definition.Rewards))}";
            return true;
        }

        public List<ChapterTaskView> ChapterTaskViews(int chapter)
        {
            if (chapter == 3) return ChapterThreeTaskViews();
            if (chapter != 2) return ChapterOneTaskViews();
            return ChapterTwoTasks.Select(definition => new ChapterTaskView
            {
                Id = definition.Id,
                Title = definition.Title,
                Progress = ChapterTwoTaskProgress(definition.Id),
                Target = definition.Target,
                Claimed = Save.ClaimedChapterTwoTasks.Contains(definition.Id),
                Rewards = CopyRewards(definition.Rewards),
            }).ToList();
        }

        public int ChapterClaimableTaskCount(int chapter) => ChapterTaskViews(chapter).Count(view => view.Claimable);

        public bool TryClaimChapterTask(int chapter, string taskId, out string message)
        {
            if (chapter == 3) return TryClaimChapterThreeTask(taskId, out message);
            if (chapter != 2) return TryClaimChapterOneTask(taskId, out message);
            ChapterTaskDefinition definition = ChapterTwoTasks.FirstOrDefault(d => d.Id == taskId);
            if (definition == null) { message = "章节任务不存在"; return false; }
            if (Save.ClaimedChapterTwoTasks.Contains(taskId)) { message = "章节任务奖励已经领取"; return false; }
            int progress = ChapterTwoTaskProgress(taskId);
            if (progress < definition.Target) { message = $"章节任务尚未完成（{progress}/{definition.Target}）"; return false; }
            GrantRewards(definition.Rewards);
            Save.ClaimedChapterTwoTasks.Add(taskId);
            SaveState();
            message = $"已领取：{definition.Title}（{FormatRewards(ToRewardTuples(definition.Rewards))}）";
            return true;
        }

        private int ChapterTwoTaskProgress(string taskId)
        {
            switch (taskId)
            {
                case ChapterTwoTaskClearStageThree: return IsStageCleared(ChapterTwoStageId(3)) ? 1 : 0;
                case ChapterTwoTaskFifteenStars: return Mathf.Min(15, ChapterTwoTotalStars);
                case ChapterTwoTaskClearFinalStage: return IsStageCleared(ChapterTwoStageId(10)) ? 1 : 0;
                case ChapterTwoTaskFortyFiveStars: return Mathf.Min(45, ChapterTwoTotalStars);
                case ChapterTwoTaskClearStageTwenty: return IsStageCleared(ChapterTwoStageId(20)) ? 1 : 0;
                default: return 0;
            }
        }
    }
}
