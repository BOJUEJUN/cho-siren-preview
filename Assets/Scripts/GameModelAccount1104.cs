using System;
using System.Collections.Generic;
using System.Linq;
using ChoSiren.Systems.Economy;
using ChoSiren.Systems.Tactics;

namespace ChoSiren
{
    public sealed partial class GameSave
    {
        /// <summary>2026-11-04 经纪人等级. 0 = a save from before the system (initialised from its progress on load).</summary>
        public int AccountLevel1104;
        /// <summary>Experience inside the current level.</summary>
        public int AccountXp1104;
        /// <summary>Levels reached whose rewards have not been presented and granted yet.</summary>
        public List<int> PendingLevelUps1104 = new List<int>();
    }

    /// <summary>
    /// 2026-11-04 (用户: 账号等级跟队伍等级应该是两个系统，要有经验条，打完有经验，不同等级有奖励): the 经纪人 (account) level.
    /// It is separate from member levels (those stay capped by stage progress and 突破). Experience comes from what the
    /// player does — 10 per stamina a won stage or sweep spends (twice on a first clear), 10 per stamina of a 舞台演出,
    /// 30 a daily task, 100 a weekly task, 50 a check-in, 100 a story — and each level pays 星光币 and 体力, every
    /// fifth level a 签约券, every tenth two 签约券 and a 声核. The curve (100 + 40n + 3n² to the next level) puts a
    /// new manager at about Lv10 after a day, Lv20 after a week, Lv30 after two and a half weeks and Lv60 (the cap)
    /// after about three and a half months of regular play.
    /// </summary>
    public sealed partial class GameModel
    {
        public const int MaxAccountLevel1104 = 60;
        public const int XpPerStamina1104 = 10;
        public const int DailyTaskXp1104 = 30, WeeklyTaskXp1104 = 100, CheckInXp1104 = 50, StoryXp1104 = 100;

        public int AccountLevel1104 => Math.Max(1, Save.AccountLevel1104);
        public int AccountXp1104 => Save.AccountXp1104;
        public bool AccountLevelMaxed1104 => AccountLevel1104 >= MaxAccountLevel1104;

        /// <summary>Experience from <paramref name="level"/> to the next.</summary>
        public static int XpToNext1104(int level)
        {
            int n = Math.Max(0, level - 1);
            return 100 + 40 * n + 3 * n * n;
        }

        /// <summary>What reaching <paramref name="level"/> pays.</summary>
        public static List<(string ItemId, int Amount)> LevelReward1104(int level)
        {
            var reward = new List<(string, int)> { (CurrencyIds.Gold, 300 + 100 * level), (CurrencyIds.Stamina, 30) };
            if (level % 10 == 0) { reward.Add((CurrencyIds.RecruitTicket, 2)); reward.Add((CurrencyIds.VoiceCore, 3)); } // 1107: 3 通用生魂
            else if (level % 5 == 0) reward.Add((CurrencyIds.RecruitTicket, 1));
            else if (level % 3 == 0) reward.Add((CurrencyIds.VoiceCrystal, 3));
            return reward;
        }

        /// <summary>The next level that pays a 签约券 (for the profile's "下一档奖励").</summary>
        public int NextMilestoneLevel1104
        {
            get
            {
                for (int l = AccountLevel1104 + 1; l <= MaxAccountLevel1104; l++)
                    if (l % 5 == 0) return l;
                return 0;
            }
        }

        public int PendingLevelUpCount1104 => Save.PendingLevelUps1104?.Count ?? 0;

        /// <summary>Adds experience; levels reached are queued for <see cref="ClaimLevelUps1104"/>. Does not save.</summary>
        private void GainAccountXp1104(int xp)
        {
            if (xp <= 0) return;
            EnsureAccountLevel1104();
            if (AccountLevelMaxed1104) { Save.AccountXp1104 = 0; return; }
            Save.AccountXp1104 = (int)Math.Min(int.MaxValue, (long)Save.AccountXp1104 + xp);
            Save.PendingLevelUps1104 ??= new List<int>();
            while (!AccountLevelMaxed1104 && Save.AccountXp1104 >= XpToNext1104(Save.AccountLevel1104))
            {
                Save.AccountXp1104 -= XpToNext1104(Save.AccountLevel1104);
                Save.AccountLevel1104++;
                Save.PendingLevelUps1104.Add(Save.AccountLevel1104);
            }
            if (AccountLevelMaxed1104) Save.AccountXp1104 = 0;
        }

        /// <summary>Grants the queued level rewards as one batch (so the shared reward popup shows them). Returns the
        /// highest level reached, or 0 when nothing was pending.</summary>
        public int ClaimLevelUps1104()
        {
            if (PendingLevelUpCount1104 == 0) return 0;
            int top = Save.PendingLevelUps1104.Max();
            TagRewards1101(RewardGrant1101.SourceLevelUp);
            foreach (int level in Save.PendingLevelUps1104)
                foreach ((string itemId, int amount) in LevelReward1104(level))
                    GrantItemInternal(itemId, amount);
            Save.PendingLevelUps1104.Clear();
            SaveState();
            return top;
        }

        /// <summary>
        /// A save from before the system starts at the level its progress already earned (cleared stages at the first-
        /// clear rate, finished stories) without paying those levels' rewards again; a new save starts at Lv1.
        /// </summary>
        private void EnsureAccountLevel1104()
        {
            if (Save.AccountLevel1104 > 0) return;
            Save.AccountLevel1104 = 1;
            Save.AccountXp1104 = 0;
            Save.PendingLevelUps1104 ??= new List<int>();
            long earned = 0;
            foreach (StageClear clear in Save.ClearedStages ?? new List<StageClear>())
            {
                StageDefinition stage = tactics.FindStage(clear.Id);
                if (stage != null) earned += 2L * stage.StaminaCost * XpPerStamina1104;
            }
            earned += (long)(Save.CompletedStories?.Count ?? 0) * StoryXp1104;
            while (Save.AccountLevel1104 < MaxAccountLevel1104 && earned >= XpToNext1104(Save.AccountLevel1104))
            {
                earned -= XpToNext1104(Save.AccountLevel1104);
                Save.AccountLevel1104++;
            }
            Save.AccountXp1104 = Save.AccountLevel1104 >= MaxAccountLevel1104 ? 0 : (int)earned;
        }
    }
}
