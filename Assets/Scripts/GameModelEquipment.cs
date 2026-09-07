using System;
using System.Collections.Generic;
using System.Linq;
using ChoSiren.Systems.Tactics;
using UnityEngine;

namespace ChoSiren
{
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
        public static readonly string[] AccessoryItemIds = { "accessory-ear-monitor", "accessory-heart-necklace", "accessory-dance-boots",
            "accessory-neon-clip", "accessory-neon-earring", "accessory-stage-crown" };
        public int LastAwardedAccessory { get; private set; } = -1;
        public bool OwnsAccessory(int index) => index >= 0 && index < AccessoryNames.Length && Save.OwnedAccessories.Contains(index);
        public int AccessoryUpgradeLevel(int index) => index >= 0 && index < Save.AccessoryUpgradeLevels.Count ? Save.AccessoryUpgradeLevels[index] : 0;
        public static int AccessoryIndexForItem(string id) => Array.IndexOf(AccessoryItemIds, id);
        public static int FirstClearAccessory(string stageId)
        {
            if (!TryGetChapterOneStageNumber(stageId, out int stage)) return -1;
            return stage < 5 ? 3 : stage < 10 ? 4 : 5;
        }
        public static string AccessorySource(int index) => index < 3 ? "初始赠送" : index == 3 ? "1-1 至 1-4" : index == 4 ? "1-5 至 1-9" : "1-10";
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
            long weight = table.Entries.Where(e => e != null && e.Weight > 0 && e.ItemId == itemId && e.Min > 0).Sum(e => (long)e.Weight);
            return total == 0 ? 0 : (float)(1 - Math.Pow(1 - weight / (double)total, table.Rolls));
        }
        public string StageEquipmentPreview(string stageId)
        {
            int index = FirstClearAccessory(stageId);
            if (index < 0) return "本关无装备掉落";
            string first = IsStageCleared(stageId) ? "首通已领取" : $"首通必得：{AccessoryNames[index]}";
            return $"{first}\n每次掉落：{AccessoryNames[index]} {StageItemDropChance(stageId, AccessoryItemIds[index]):P0} · 详情";
        }
        public string StageLootDescription(string stageId)
        {
            StageDefinition stage = tactics.FindStage(stageId);
            if (stage == null) return "暂无掉落信息";
            int item = FirstClearAccessory(stageId);
            string first = IsStageCleared(stageId) ? "首通奖励已领取，不重复发放。" : $"首通额外：星钻 {stage.DiamondFirstClear}" +
                (item >= 0 ? $" + {AccessoryNames[item]} ×1（必得）" : string.Empty);
            var lines = new List<string> { first, $"每次胜利：基础金币 {PreviewStageGoldReward(stageId)}", "", "随机奖励（每场至少获得一次的概率）：" };
            foreach (var group in stage.Drops.Entries.GroupBy(e => e.ItemId))
            {
                int index = AccessoryIndexForItem(group.Key);
                string name = index >= 0 ? AccessoryNames[index] : CurrencyName(group.Key);
                lines.Add($"{name}  {StageItemDropChance(stageId, group.Key):P1}");
            }
            lines.Add("\n每场多次抽取，以上概率不能直接相加。\n新装备进入饰品页；每件重复装备转为3强化碎片。\n再次挑战不保证掉装备；失败不发通关奖励。\n三星提升章节星数，不重复发首通奖励。");
            return string.Join("\n", lines);
        }
    }
}
