using System;
using System.Linq;
using System.Collections.Generic;
using ChoSiren.Systems.Economy;

namespace ChoSiren
{
    /// <summary>One long-term milestone: live progress from the save, one-time diamond reward.</summary>
    public sealed class AchievementView
    {
        public string Id;
        public string Title;
        public string Detail;
        public int Progress;
        public int Target;
        public int Diamonds;
        public bool Claimed;
        public bool Done => Progress >= Target;
        public bool Claimable => Done && !Claimed;
    }

    public sealed partial class GameModel
    {
        private const string AchievementFlagPrefix = "achievement-claimed:";

        /// <summary>Milestones derived from existing save state, so nothing extra is tracked.</summary>
        public List<AchievementView> Achievements()
        {
            int stagesCleared = Save.ClearedStages.Count(clear => clear.Id.StartsWith("stage-", StringComparison.Ordinal));
            int threeStars = Save.ClearedStages.Count(clear => clear.Id.StartsWith("stage-", StringComparison.Ordinal) && clear.Stars >= 3);
            int topLevel = Save.UnlockedMembers.Count == 0 ? 0 : Save.UnlockedMembers.Max(LevelOf);
            int stories = Save.CompletedStories.Count;
            var list = new List<AchievementView>
            {
                Make("first-live", "初次登台", "打赢第一场演出 1-1", IsStageCleared("stage-1-1") ? 1 : 0, 1, 50),
                Make("chapter-one", "王座之上", "通关第一章 1-10", IsStageCleared("stage-1-10") ? 1 : 0, 1, 200),
                Make("chapter-two", "海湾巨星", "通关第二章 2-10", IsStageCleared("stage-2-10") ? 1 : 0, 1, 300),
                Make("stages-10", "巡演老手", "累计通关 10 个关卡", stagesCleared, 10, 100),
                Make("three-star-10", "完美演出", "10 个关卡拿到三星", threeStars, 10, 150),
                Make("members-8", "壮大团队", "签约 8 名成员", Save.UnlockedMembers.Count, 8, 100),
                Make("members-16", "星光满堂", "签约 16 名成员", Save.UnlockedMembers.Count, 16, 200),
                Make("level-10", "刻苦练习", "任意成员练到 10 级", topLevel, 10, 100),
                Make("accessories-10", "饰品收藏家", "收集 10 件饰品", Save.OwnedAccessories.Count, 10, 100),
                Make("stories-10", "剧情爱好者", "读完 10 段剧情", stories, 10, 80),
            };
            return list;
        }

        public int ClaimableAchievementCount => Achievements().Count(item => item.Claimable);

        public bool ClaimAchievement(string id, out string message)
        {
            AchievementView item = Achievements().FirstOrDefault(view => view.Id == id);
            if (item == null) { message = "成就不存在"; return false; }
            if (item.Claimed) { message = "奖励已经领取过了"; return false; }
            if (!item.Done) { message = $"还差一点：{item.Progress}/{item.Target}"; return false; }
            GrantItemInternal(CurrencyIds.Diamond, item.Diamonds);
            Save.StoryFlags.Add(AchievementFlagPrefix + id);
            SaveState();
            message = $"成就「{item.Title}」达成：星钻 ×{item.Diamonds}";
            return true;
        }

        private AchievementView Make(string id, string title, string detail, int progress, int target, int diamonds) =>
            new AchievementView
            {
                Id = id, Title = title, Detail = detail, Target = target, Diamonds = diamonds,
                Progress = Math.Min(progress, target),
                Claimed = Save.StoryFlags.Contains(AchievementFlagPrefix + id),
            };
    }
}
