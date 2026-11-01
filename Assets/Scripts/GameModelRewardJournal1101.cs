using System;
using System.Collections.Generic;
using ChoSiren.Systems.Economy;

namespace ChoSiren
{
    /// <summary>One batch of things the player just received (one save boundary of one action).</summary>
    public sealed class RewardGrant1101
    {
        public const string SourceGeneral = "reward";
        public const string SourceCheckIn = "checkin";
        public const string SourceTask = "task";
        public const string SourceIdle = "idle";
        public const string SourcePerform = "perform";
        public const string SourceExchange = "exchange";
        public const string SourceStory = "story";
        /// <summary>Stage settlement: the victory page shows these itself, so the global popup skips them.</summary>
        public const string SourceBattle = "battle";

        public IReadOnlyList<(string ItemId, int Amount)> Items;
        public string Source;
    }

    /// <summary>
    /// 2026-11-01 reward-ui (用户: "领取到金币、钻石这些，设计要统一下，不要一个地方一个样"): every grant goes through
    /// <see cref="GrantItemInternal"/>, which records what the player actually ends up with (a duplicate accessory is
    /// recorded as its gold refund, a retired fragment as its gold), and <see cref="SaveState"/> publishes the batch.
    /// The UI listens once (<c>ChoSiren.UI.RewardGain1101.Bind</c>), so 签到 / 任务 / 邮件 / 成就 / 章节 / 扫荡 / 兑换 /
    /// 舞台收益 / 演出 all share one presentation instead of each screen inventing its own toast.
    /// </summary>
    public sealed partial class GameModel
    {
        private readonly List<(string ItemId, int Amount)> rewardJournal1101 = new List<(string ItemId, int Amount)>();
        private string rewardSource1101;

        /// <summary>Raised after the save that granted the items (never for an empty batch).</summary>
        public event Action<RewardGrant1101> RewardsGranted1101;

        /// <summary>Items recorded but not yet published (tests, and binding drops stale ones).</summary>
        public int PendingRewardCount1101 => rewardJournal1101.Count;

        public void DiscardPendingRewards1101()
        {
            rewardJournal1101.Clear();
            rewardSource1101 = null;
        }

        /// <summary>Marks the batch being recorded; the first tag of a batch wins.</summary>
        private void TagRewards1101(string source)
        {
            if (rewardSource1101 == null) rewardSource1101 = source;
        }

        private void RecordRewardGrant1101(string itemId, int amount)
        {
            if (string.IsNullOrEmpty(itemId) || amount <= 0) return;
            int accessory = AccessoryIndexForItem(itemId);
            if (accessory >= 0)
            {
                bool owned = OwnsAccessory(accessory);
                if (!owned) rewardJournal1101.Add((itemId, 1));
                int duplicates = owned ? amount : amount - 1;
                if (duplicates > 0) rewardJournal1101.Add((CurrencyIds.Gold, duplicates * DuplicateAccessoryGold));
                return;
            }
            if (itemId == EquipmentFragmentItemId)
            {
                rewardJournal1101.Add((CurrencyIds.Gold, amount * RetiredFragmentGold));
                return;
            }
            rewardJournal1101.Add((itemId, amount));
        }

        private void FlushRewardJournal1101()
        {
            if (rewardJournal1101.Count == 0) { rewardSource1101 = null; return; }
            var merged = new List<(string ItemId, int Amount)>();
            foreach ((string itemId, int amount) in rewardJournal1101)
            {
                int at = merged.FindIndex(entry => entry.ItemId == itemId);
                if (at >= 0) merged[at] = (itemId, merged[at].Amount + amount);
                else merged.Add((itemId, amount));
            }
            var grant = new RewardGrant1101 { Items = merged, Source = rewardSource1101 ?? RewardGrant1101.SourceGeneral };
            rewardJournal1101.Clear();
            rewardSource1101 = null;
            try { RewardsGranted1101?.Invoke(grant); }
            catch (Exception exception) { UnityEngine.Debug.LogException(exception); }
        }
    }
}
