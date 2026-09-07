using System;
using System.Collections.Generic;
using System.Linq;
using ChoSiren.Systems.Economy;
using ChoSiren.Systems.Tactics;
using UnityEngine;

namespace ChoSiren
{
    public readonly struct StageLootCandidate
    {
        public StageLootCandidate(string itemId, int minimum, int maximum, float chance)
        { ItemId = itemId; Minimum = minimum; Maximum = maximum; Chance = chance; }
        public string ItemId { get; }
        public int Minimum { get; }
        public int Maximum { get; }
        public float Chance { get; }
        public string Name => GameModel.RewardItemName(ItemId);
        public string Use => GameModel.RewardItemUse(ItemId);
        public string AmountLabel => Minimum == Maximum ? $"×{Minimum}" : $"×{Minimum}–{Maximum}";
    }

    [Serializable]
    public sealed class MemberAccessoryBinding
    {
        public string MemberId;
        public int Accessory;
    }

    public sealed partial class GameModel
    {
        public int EquippedAccessoryFor(int member) => Save.MemberAccessories?
            .FirstOrDefault(b => b.MemberId == MemberIdAt(member))?.Accessory ?? -1;
        public int AccessoryOwner(int item) => IndexOfMember(Save.MemberAccessories?
            .FirstOrDefault(b => b.Accessory == item)?.MemberId);
        private int PreviewAccessoryFor(int member, int target, int item) => member == target ? item
            : EquippedAccessoryFor(member) == item ? -1 : EquippedAccessoryFor(member);
        public int TeamPowerWithMemberAccessory(int member, int item) => Save.Team.Where(IsUnlocked)
            .Sum(i => StatsOf(i, PreviewAccessoryFor(i, member, item)).Power);
        public bool EquipAccessoryForMember(int member, int item, out string message)
        {
            if (!IsUnlocked(member)) { message = "请先签约该成员"; return false; }
            if (!OwnsAccessory(item)) { message = "尚未获得该饰品，请查看掉落来源"; return false; }
            string id = MemberIdAt(member);
            bool remove = EquippedAccessoryFor(member) == item;
            Save.MemberAccessories.RemoveAll(b => b.MemberId == id || b.Accessory == item);
            if (!remove) Save.MemberAccessories.Add(new MemberAccessoryBinding { MemberId = id, Accessory = item });
            SaveState();
            message = $"{Members[member].Name} 已{(remove ? "卸下" : "装备")}{AccessoryNames[item]}，下场战斗生效";
            return true;
        }
        // Legacy drop IDs remain stable so existing cosmetic-only records can be recovered.
        public static readonly string[] AccessoryItemIds = new[] { "accessory-ear-monitor", "accessory-heart-necklace", "accessory-dance-boots",
            "accessory-neon-clip", "accessory-neon-earring", "accessory-stage-crown", "accessory-prism-monitor",
            "accessory-pulse-necklace", "accessory-spotlight-boots", "accessory-echo-note", "accessory-moon-bracelet", "accessory-starlit-crown" }
            .Concat(new[] { "ear", "neck", "wrist", "ring", "hair", "charm" }.SelectMany(group =>
                Enumerable.Range(1, 9).Select(number => $"accessory-collection-{group}-{number:00}"))).ToArray();
        public const string EquipmentFragmentItemId = "equipment-fragment";
        public static readonly string[] AccessoryCategories = { "全部", "耳饰", "项链", "手环", "戒指", "发饰", "挂饰", "舞台" };
        public static string AccessoryCategory(int index)
        {
            if (index < 0 || index >= AccessoryNames.Length) return "未知";
            if (index >= 12) return AccessoryCategories[1 + (index - 12) / 9];
            string[] legacy = { "耳饰", "项链", "舞台", "挂饰", "手环", "发饰" };
            return legacy[index % 6];
        }
        public static string AccessoryCollectionResourcePath(int index)
        {
            if (index < 12 || index >= AccessoryNames.Length) return null;
            string[] groups = { "ear", "neck", "wrist", "ring", "hair", "charm" };
            return $"Art/AccessoryAI/Collection54/accessory-{groups[(index - 12) / 9]}-{(index - 12) % 9 + 1:00}-v1";
        }
        private static CombatStatBonuses CollectionAccessoryBonuses(int index)
        {
            if (index < 12 || index >= AccessoryNames.Length) return default;
            // All collections share one slot. Specialisation, rather than exponential rarity
            // inflation, makes an offensive item compete with a survival or armour item.
            int[,] patterns = { { 100, 40, 40 }, { 130, 20, 30 }, { 90, 70, 20 },
                { 110, 0, 80 }, { 70, 80, 50 }, { 140, 40, 0 },
                { 60, 60, 110 }, { 100, 80, 30 }, { 80, 70, 100 } };
            int group = (index - 12) / 9, variant = (index - 12) % 9;
            int hp = patterns[variant, (3 - group % 3) % 3];
            int attack = patterns[variant, (4 - group % 3) % 3];
            int defense = patterns[variant, (5 - group % 3) % 3];
            if (group % 3 == 0) hp += group * 5;
            else if (group % 3 == 1) attack += group * 5;
            else defense += group * 5;
            return new CombatStatBonuses(hp, attack, defense);
        }
        public int LastAwardedAccessory { get; private set; } = -1;
        private readonly List<(string ItemId, int Amount)> lastBattleRewards = new List<(string ItemId, int Amount)>();
        public IReadOnlyList<(string ItemId, int Amount)> LastBattleRewards => lastBattleRewards;
        public bool OwnsAccessory(int index) => index >= 0 && index < AccessoryNames.Length && Save.OwnedAccessories.Contains(index);
        public int AccessoryUpgradeLevel(int index) => index >= 0 && index < Save.AccessoryUpgradeLevels.Count ? Save.AccessoryUpgradeLevels[index] : 0;
        public static int AccessoryIndexForItem(string id) => Array.IndexOf(AccessoryItemIds, id);
        public static int FirstClearAccessory(string stageId)
        {
            if (!TryGetChapterOneStageNumber(stageId, out int stage)) return -1;
            int[] firstClearItems = { 3, 6, 7, 8, 4, 9, 10, 6, 11, 5 };
            return firstClearItems[stage - 1];
        }
        public static string AccessorySource(int index)
        {
            if (index < 0 || index >= AccessoryItemIds.Length) return "暂无来源";
            var stages = ChoSiren.Systems.Data.GameData.Repository.Tactics.Stages;
            string[] sources = stages.Where(stage => stage.Drops?.Entries != null && stage.Drops.Rolls > 0 &&
                stage.Drops.Entries.Any(entry => entry.ItemId == AccessoryItemIds[index] && entry.Weight > 0 && entry.Max > 0))
                .Select(stage => stage.Id.Replace("stage-", string.Empty)).ToArray();
            return (index < 3 ? "初始赠送 / " : string.Empty) + (sources.Length > 0 ? string.Join("、", sources) : "暂无来源");
        }
        public static string RewardItemName(string itemId)
        {
            int item = AccessoryIndexForItem(itemId);
            return item >= 0 ? AccessoryNames[item] : itemId == EquipmentFragmentItemId ? "强化碎片" : CurrencyName(itemId);
        }
        public static string RewardItemUse(string itemId)
        {
            int index = AccessoryIndexForItem(itemId);
            if (index >= 0)
            {
                CombatStatBonuses bonus = AccessoryBonuses(index);
                var stats = new List<string>();
                if (bonus.Hp > 0) stats.Add($"生命 +{bonus.Hp / 10f:0.#}%");
                if (bonus.Attack > 0) stats.Add($"攻击 +{bonus.Attack / 10f:0.#}%");
                if (bonus.Defense > 0) stats.Add($"防御 +{bonus.Defense / 10f:0.#}%");
                return string.Join(" · ", stats);
            }
            switch (itemId)
            {
                case EquipmentFragmentItemId: return "饰品强化材料";
                case CurrencyIds.Gold: return "成员训练 / 饰品强化";
                case CurrencyIds.Diamond: return "选秀 / 补体力 / 换金币";
                case CurrencyIds.RecruitTicket: return "选秀招募抵扣星钻";
                case CurrencyIds.CostumeTicket: return "服装招募使用";
                default: return "收藏资源";
            }
        }
        public CombatStatBonuses EffectiveAccessoryBonuses(int index)
        {
            CombatStatBonuses b = AccessoryBonuses(index);
            int extra = AccessoryUpgradeLevel(index) * 20;
            return new CombatStatBonuses(b.Hp > 0 ? b.Hp + extra : 0, b.Attack > 0 ? b.Attack + extra : 0, b.Defense > 0 ? b.Defense + extra : 0);
        }
        public bool CanUpgradeAccessory(int index, out int fragments, out int gold)
        {
            int level = AccessoryUpgradeLevel(index);
            fragments = (level + 1) * 3;
            gold = (level + 1) * 100;
            return OwnsAccessory(index) && level < 3 && Save.EquipmentFragments >= fragments && Save.Gold >= gold;
        }
        public bool UpgradeAccessory(int index, out string message)
        {
            if (!OwnsAccessory(index)) { message = "尚未获得该饰品"; return false; }
            if (AccessoryUpgradeLevel(index) >= 3) { message = "已达强化上限 +3"; return false; }
            if (!CanUpgradeAccessory(index, out int fragments, out int gold))
            { message = $"需要强化碎片 {fragments}、金币 {gold}；重复装备可转为碎片"; return false; }
            Save.EquipmentFragments -= fragments;
            Save.Gold -= gold;
            Save.AccessoryUpgradeLevels[index]++;
            SaveState();
            message = $"{AccessoryNames[index]} 强化 +{AccessoryUpgradeLevel(index)}，下场战斗生效";
            return true;
        }
        private void GrantAccessory(int index, int amount)
        {
            if (!OwnsAccessory(index)) { Save.OwnedAccessories.Add(index); amount--; }
            Save.EquipmentFragments = (int)Math.Min(int.MaxValue, (long)Save.EquipmentFragments + (long)Math.Max(0, amount) * 3);
        }
        private void NormalizeEquipment()
        {
            Save.OwnedAccessories = (Save.OwnedAccessories ?? new List<int>()).Where(i => i >= 0 && i < AccessoryNames.Length)
                .Concat(new[] { 0, 1, 2 }).Distinct().ToList();
            Save.AccessoryUpgradeLevels ??= new List<int>();
            while (Save.AccessoryUpgradeLevels.Count < AccessoryNames.Length) Save.AccessoryUpgradeLevels.Add(0);
            for (int i = 0; i < Save.AccessoryUpgradeLevels.Count; i++) Save.AccessoryUpgradeLevels[i] = Mathf.Clamp(Save.AccessoryUpgradeLevels[i], 0, 3);
            Save.EquipmentFragments = Math.Max(0, Save.EquipmentFragments);
            Save.EquipmentFirstClearClaims = CleanStrings(Save.EquipmentFirstClearClaims);
            foreach (string item in Save.OwnedCostumes)
            {
                int index = AccessoryIndexForItem(item);
                if (index >= 0 && !OwnsAccessory(index)) Save.OwnedAccessories.Add(index);
            }
            // One-time retroactive grants, independent of schema defaults for Unity JsonUtility.
            // Keep original cosmetics and clear records; never pay old first-clear diamonds again.
            foreach (StageClear clear in Save.ClearedStages)
            {
                int index = FirstClearAccessory(clear.Id);
                if (index < 0 || Save.EquipmentFirstClearClaims.Contains(clear.Id)) continue;
                Save.EquipmentFirstClearClaims.Add(clear.Id);
                GrantAccessory(index, 1);
            }
            if (Save.EquippedAccessory >= 0 && !OwnsAccessory(Save.EquippedAccessory)) Save.EquippedAccessory = -1;
            Save.MemberAccessories ??= new List<MemberAccessoryBinding>();
            if (!Save.PersonalEquipmentMigrated)
            {
                if (Save.EquippedAccessory >= 0 && Save.MemberAccessories.Count == 0)
                    Save.MemberAccessories.Add(new MemberAccessoryBinding { MemberId = MemberIdAt(Save.Team[0]), Accessory = Save.EquippedAccessory });
                Save.PersonalEquipmentMigrated = true;
            }
            var members = new HashSet<string>();
            var items = new HashSet<int>();
            Save.MemberAccessories = Save.MemberAccessories.Where(b => b != null &&
                IsUnlocked(IndexOfMember(b.MemberId)) && OwnsAccessory(b.Accessory) &&
                members.Add(b.MemberId) && items.Add(b.Accessory)).ToList();
            // Compatibility projection only; changing captain must never move her equipment.
            Save.EquippedAccessory = EquippedAccessoryFor(Save.Team[0]);
        }
        public float StageItemDropChance(string stageId, string itemId)
        {
            DropTable table = tactics.FindStage(stageId)?.Drops;
            if (table?.Entries == null || table.Rolls <= 0) return 0;
            long total = table.Entries.Where(e => e != null && e.Weight > 0).Sum(e => (long)e.Weight);
            double positiveWeight = table.Entries.Where(e => e != null && e.Weight > 0 && e.ItemId == itemId)
                .Sum(e => e.Weight * (e.Max <= 0 ? 0d : e.Min > 0 ? 1d : e.Max / ((double)e.Max - e.Min + 1)));
            return total == 0 ? 0 : (float)(1 - Math.Pow(1 - positiveWeight / total, table.Rolls));
        }
        public IReadOnlyList<StageLootCandidate> StageLootCandidates(string stageId)
        {
            DropTable table = tactics.FindStage(stageId)?.Drops;
            if (table?.Entries == null || table.Rolls <= 0) return Array.Empty<StageLootCandidate>();
            return table.Entries.Where(e => e != null && e.Weight > 0 && e.Max > 0)
                .GroupBy(e => e.ItemId).Select(group => new StageLootCandidate(group.Key,
                    Math.Max(1, group.Min(e => e.Min)), group.Max(e => e.Max), StageItemDropChance(stageId, group.Key)))
                .ToArray();
        }
        public string StageEquipmentPreview(string stageId)
        {
            int index = FirstClearAccessory(stageId);
            if (index < 0) return "本关无装备掉落";
            string first = IsStageCleared(stageId) ? "首通已领取" : $"首通必得：{AccessoryNames[index]}";
            return $"{first}\n随机池 {StageLootCandidates(stageId).Count} 种 · 点击查看概率";
        }
        public string StageLootDescription(string stageId)
        {
            StageDefinition stage = tactics.FindStage(stageId);
            if (stage == null) return "暂无掉落信息";
            int item = FirstClearAccessory(stageId);
            string first = IsStageCleared(stageId) ? "首通奖励已领取，不重复发放。" : $"首通额外：星钻 {stage.DiamondFirstClear}" +
                (item >= 0 ? $" + {AccessoryNames[item]} ×1（必得）" : string.Empty);
            var lines = new List<string> { first, $"每次胜利：基础金币 {PreviewStageGoldReward(stageId)}", "", "随机奖励（每场至少获得一次的概率）：" };
            foreach (StageLootCandidate candidate in StageLootCandidates(stageId))
            {
                lines.Add($"{candidate.Name} {candidate.AmountLabel}  {candidate.Chance:P1}");
            }
            lines.Add("\n每场多次抽取，以上概率不能直接相加。\n新装备进入饰品页；每件重复装备转为3强化碎片。\n再次挑战不保证掉装备；失败不发通关奖励。\n三星提升章节星数，不重复发首通奖励。");
            return string.Join("\n", lines);
        }
    }
}
