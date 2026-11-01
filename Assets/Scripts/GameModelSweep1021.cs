using System;
using System.Collections.Generic;
using System.Linq;
using ChoSiren.Systems.Economy;
using ChoSiren.Systems;
using ChoSiren.Systems.Tactics;
using UnityEngine;

namespace ChoSiren
{
    /// <summary>
    /// 扫荡 (2026-10-21): a stage cleared with three stars can be replayed instantly. Each sweep
    /// spends the stage's stamina and rolls the same repeat-victory rewards as a real win — gold
    /// (scaled by the current difficulty) and random drops — but never first-clear diamonds or
    /// first-clear equipment, and never story progress or stars.
    /// </summary>
    public sealed partial class GameModel
    {
        public const int MaxSweepTimes = 10;

        public bool CanSweep(string stageId) =>
            tactics.FindStage(stageId) != null && IsStageUnlocked(stageId) && StarsOf(stageId) >= 3;

        public bool TrySweepStage(string stageId, int times, ulong seed, out string message)
        {
            StageDefinition stage = tactics.FindStage(stageId);
            lastBattleRewards.Clear();
            LastAwardedAccessory = -1;
            if (stage == null) { message = "关卡不存在"; return false; }
            if (!CanSweep(stageId)) { message = "三星通关后才能扫荡"; return false; }
            times = Mathf.Clamp(times, 1, MaxSweepTimes);
            bool stateChanged = Tick();
            int cost = stage.StaminaCost * times;
            if (Save.Stamina < cost || !SpendStaminaInternal(cost))
            {
                if (stateChanged) SaveState();
                message = $"体力不足，扫荡 ×{times} 需要 {cost} 点";
                return false;
            }

            BattleDifficultyProfile difficulty = CurrentBattleDifficultyProfile;
            var rewards = new List<(string ItemId, int Amount)>();
            int dailyBonus = 0;
            for (int run = 0; run < times; run++)
            {
                ulong runSeed = unchecked(seed + (ulong)run * 0x9E3779B97F4A7C15UL);
                List<(string ItemId, int Amount)> drops = DropResolver.Roll(stage.Drops,
                    new SeededRandom(runSeed ^ 0xD1CE5EEDUL));
                int rawGold = Mathf.Max(0, stage.GoldReward) + drops
                    .Where(drop => drop.ItemId == CurrencyIds.Gold)
                    .Sum(drop => Mathf.Max(0, drop.Amount));
                int gold = ScalePositiveReward(rawGold, difficulty.GoldRewardPermille);
                if (gold > 0) rewards.Add((CurrencyIds.Gold, gold));
                rewards.AddRange(drops.Where(drop => drop.ItemId != CurrencyIds.Gold));
                if (RecordSuccessfulPerformance()) dailyBonus += DailyPerformanceDiamondReward;
                Report(TaskTriggers.BattleWin);
            }
            if (dailyBonus > 0) rewards.Add((CurrencyIds.Diamond, dailyBonus));

            var equipmentNotes = new List<string>();
            var granted = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach ((string itemId, int amount) in rewards)
            {
                if (amount <= 0) continue;
                int equipment = AccessoryIndexForItem(itemId);
                if (equipment >= 0)
                {
                    bool owned = OwnsAccessory(equipment);
                    int duplicates = owned ? amount : Math.Max(0, amount - 1);
                    if (!owned)
                    {
                        equipmentNotes.Add($"新装备：{AccessoryNames[equipment]}");
                        Add(granted, itemId, 1);
                        LastAwardedAccessory = equipment;
                    }
                    if (duplicates > 0) Add(granted, CurrencyIds.Gold, duplicates * DuplicateAccessoryGold);
                }
                else Add(granted, itemId, amount);
                GrantItemInternal(itemId, amount);
            }
            foreach (var pair in granted) lastBattleRewards.Add((pair.Key, pair.Value));
            GainTeamAffection(times);
            SaveState();
            message = $"扫荡 ×{times} 完成，体力 -{cost}：{FormatRewards(lastBattleRewards.Where(r => AccessoryIndexForItem(r.ItemId) < 0).ToList())}";
            if (equipmentNotes.Count > 0) message += "\n" + string.Join("\n", equipmentNotes);
            return true;
        }

        private static void Add(Dictionary<string, int> totals, string itemId, int amount)
        {
            totals.TryGetValue(itemId, out int current);
            totals[itemId] = current + amount;
        }
    }
}
