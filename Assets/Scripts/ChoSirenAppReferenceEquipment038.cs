using System;
using System.Linq;
using ChoSiren.Panels;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    public sealed partial class ChoSirenApp
    {
        private void BuildEquipmentBoard038()
        {
            if (!model.IsUnlocked(equipmentMember)) equipmentMember = model.Save.Team[0];
            int member = equipmentMember;
            if (selectedAccessoryIndex < 0 || selectedAccessoryIndex >= GameModel.AccessoryNames.Length)
                selectedAccessoryIndex = Math.Max(0, model.EquippedAccessoryFor(member));
            int[] inventory = Enumerable.Range(0, GameModel.AccessoryNames.Length).Where(i =>
                (equipmentCategory == "全部" || GameModel.AccessoryCategory(i) == equipmentCategory) &&
                (!equipmentOwnedOnly || model.OwnsAccessory(i))).ToArray();
            if (inventory.Length > 0 && !inventory.Contains(selectedAccessoryIndex)) selectedAccessoryIndex = inventory[0];
            bool reveal = equipmentRevealSelection || member != lastEquipmentMember || selectedAccessoryIndex != lastEquipmentAccessory;
            equipmentRevealSelection = false; lastEquipmentMember = member; lastEquipmentAccessory = selectedAccessoryIndex;
            if (reveal && inventory.Contains(selectedAccessoryIndex)) equipmentPage = Array.IndexOf(inventory, selectedAccessoryIndex) / 12;
            int item = selectedAccessoryIndex;
            int pageCount = Math.Max(1, (inventory.Length + 11) / 12);
            equipmentPage = Mathf.Clamp(equipmentPage, 0, pageCount - 1);
            int[] visible = inventory.Skip(equipmentPage * 12).Take(12).ToArray();
            // Keep the authored pagination above the common, measured bottom-nav strip.
            RectTransform board = ReferenceBoard038("EquipmentReferenceBoard038", "Art/Reference038/accessory-board-038", 120);
            GameObject body = NewImage("EquipmentBody", board, null, Color.clear);
            RectTransform root = body.GetComponent<RectTransform>();
            Stretch(root);
            body.GetComponent<Image>().raycastTarget = false;
            MemberDefinition m = GameModel.Members[member];
            GameObject selector = BoardButton038("EquipmentCycleMember", root, new Rect(22, 194, 378, 491), () => CycleEquipmentMember(1));
            GameObject portraitClip = NewImage("EquipmentPortraitClip", selector.transform, null, Color.clear);
            PlaceTop(portraitClip.GetComponent<RectTransform>(), 0, 0, 378, 412);
            portraitClip.GetComponent<Image>().raycastTarget = false;
            portraitClip.AddComponent<RectMask2D>();
            GameObject portrait = NewImage("EquipmentPortrait", portraitClip.transform, Resources.Load<Sprite>(m.ResourcePath), White);
            PlaceTop(portrait.GetComponent<RectTransform>(), -215, -62, 648, 1000);
            portrait.GetComponent<Image>().preserveAspect = true;
            portrait.GetComponent<Image>().raycastTarget = false;
            BoardText038(selector.transform, "EquipmentMemberName", $"{m.Name} · 等级 {model.LevelOf(member)}", 27, White, 13, 416, 345, 40);
            BoardText038(selector.transform, "EquipmentMemberPower", $"战力 {model.PowerOf(member):N0}", 21, White, 13, 457, 345, 33);
            BoardButton038("EquipmentChooseMember", root, new Rect(712, 442, 137, 244), () => OpenOwnedMemberPicker(0, 0, true));
            BoardText038(root, "EquipmentItemName", GameModel.AccessoryNames[item], 30, White, 442, 268, 305, 48);
            GameObject selectedArt = NewImage("EquipmentSelectedArt", root, AccessoryItemSprite(item), White);
            PlaceTop(selectedArt.GetComponent<RectTransform>(), 440, 321, 278, 205);
            selectedArt.GetComponent<Image>().preserveAspect = true;
            selectedArt.GetComponent<Image>().raycastTarget = false;
            GameModel.StageStats(m, member, model.SigningChannelOf(member) == 0 ? 0 : 1, model.LevelOf(member), out int vocal, out int rap,
                out int dance, out int popularity, out int appearance);
            string[] names = { "声波", "说唱", "舞蹈", "名气", "颜值" };
            int[] values = { vocal, rap, dance, popularity, appearance };
            for (int i = 0; i < 5; i++)
                BoardText038(root, "EquipmentStageStat-" + i, $"{names[i]} {values[i]}", 16, i == 4 ? Pink : Cyan,
                    439 + (i % 3) * 93, 525 + (i / 3) * 33, 91, 29);
            bool equipped = model.AccessoryOwner(item) == member;
            int before = equipped ? model.PreviewEquipmentTeamPower(member, item, true) : model.TeamPower;
            int after = model.PreviewEquipmentTeamPower(member, item);
            int delta = after - before;
            BoardText038(root, "EquipmentCompareHeading", equipped ? "队伍战力 · 未装备→已装备" : "队伍战力 · 当前→装备后", 15, Muted, 438, 600, 277, 26);
            BoardText038(root, "AccessoryDelta-3", $"{before:N0}→{after:N0}", 28, White, 438, 629, 278, 41);
            BoardText038(root, "AccessoryPowerChange", $"{(equipped ? "已生效" : "战力")} {(delta > 0 ? "+" : "")}{delta:N0}",
                15, Cyan, 441, 671, 260, 25);
            BoardText038(root, "EquipmentInventoryTitle", $"{model.Save.OwnedAccessories.Count}/{GameModel.AccessoryNames.Length}", 22, White, 157, 720, 170, 38);
            GameObject category = BoardButton038("EquipmentCategoryFilter", root, new Rect(383, 720, 200, 43), () =>
            {
                string[] categories = new[] { "全部" }.Concat(GameModel.AccessoryCategories.Where(c => c != "全部")).ToArray();
                equipmentCategory = categories[(Array.IndexOf(categories, equipmentCategory) + 1) % categories.Length];
                equipmentPage = 0; ShowScreen("accessory");
            });
            BoardText038(category.transform, "Label", "类别：" + equipmentCategory, 19, White, 0, 0, 200, 43, TextAnchor.MiddleCenter);
            GameObject owned = BoardButton038("EquipmentOwnedFilter", root, new Rect(604, 720, 220, 43), () =>
            { equipmentOwnedOnly = !equipmentOwnedOnly; equipmentPage = 0; ShowScreen("accessory"); });
            BoardText038(owned.transform, "Label", "只看拥有：" + (equipmentOwnedOnly ? "开" : "关"), 18, White, 0, 0, 220, 43, TextAnchor.MiddleCenter);
            for (int slot = 0; slot < visible.Length; slot++)
            {
                int index = visible[slot];
                int wearer = model.AccessoryOwner(index);
                GameObject card = BoardButton038("Accessory-" + index, root,
                    new Rect(44 + (slot % 3) * 270, 786 + (slot / 3) * 190, 233, 180), () =>
                    { selectedAccessoryIndex = index; ShowScreen("accessory"); });
                GameObject art = NewImage("ItemArt", card.transform, AccessoryItemSprite(index), model.OwnsAccessory(index) ? White : new Color(1, 1, 1, .6f));
                PlaceTop(art.GetComponent<RectTransform>(), 47, 0, 145, 82);
                art.GetComponent<Image>().preserveAspect = true;
                art.GetComponent<Image>().raycastTarget = false;
                BoardText038(card.transform, "ItemName", GameModel.AccessoryNames[index], 20, AccessoryRarityColor(index), 5, 84, 228, 30);
                BoardText038(card.transform, "ItemQuality", GameModel.AccessoryRarityNameOf(index) + " · " + GameModel.AccessoryCategory(index), 13,
                    AccessoryRarityColor(index), 5, 117, 225, 24);
                if (index == item)
                {
                    bool owns = model.OwnsAccessory(index);
                    GameObject equip = BoardButton038("QuickEquip", card.transform, new Rect(3, 145, 108, 30), () =>
                    {
                        if (!model.OwnsAccessory(index)) { OpenLevelMap(); return; }
                        model.EquipAccessoryForMember(member, index, out string message); RefreshEquipmentCollection(); Toast(message);
                    });
                    BoardText038(equip.transform, "Label", !owns ? "去关卡" : wearer == member ? "卸下" : wearer >= 0 ? "转移" : "装备", 17, Cyan, 0, 0, 108, 29, TextAnchor.MiddleCenter);
                    if (owns)
                    {
                        bool can = model.CanUpgradeAccessory(index, out int cost);
                        GameObject upgrade = BoardButton038("QuickUpgrade", card.transform, new Rect(116, 145, 113, 30), () =>
                        { model.UpgradeAccessory(index, out string message); RefreshEquipmentCollection(); Toast(message); });
                        upgrade.GetComponent<Button>().interactable = can;
                        BoardText038(upgrade.transform, "Label", model.AccessoryUpgradeLevel(index) >= 3 ? "已满级" : $"强化 {cost}", 15, can ? White : Muted, 0, 0, 113, 29, TextAnchor.MiddleCenter);
                    }
                }
                else BoardText038(card.transform, "ItemStatus", !model.OwnsAccessory(index) ? "掉落：" + GameModel.AccessorySource(index)
                    : wearer < 0 ? "已拥有 · 空闲" : "装备：" + GameModel.Members[wearer].Name, 14, Cyan, 5, 145, 227, 30);
            }
            if (visible.Length == 0) BoardText038(root, "EquipmentEmpty", "暂无符合筛选条件的饰品", 25, White, 100, 931, 660, 64, TextAnchor.MiddleCenter);
            BoardButton038("EquipmentPreviousPage", root, new Rect(87, 1566, 219, 72), () =>
            { equipmentPage = Math.Max(0, equipmentPage - 1); RefreshEquipmentCollection(); }).GetComponent<Button>().interactable = equipmentPage > 0;
            BoardText038(root, "EquipmentPageCount", $"{equipmentPage + 1}/{pageCount}", 22, White, 372, 1576, 95, 42, TextAnchor.MiddleCenter);
            BoardButton038("EquipmentNextPage", root, new Rect(548, 1566, 240, 72), () =>
            { equipmentPage = Math.Min(pageCount - 1, equipmentPage + 1); RefreshEquipmentCollection(); }).GetComponent<Button>().interactable = equipmentPage + 1 < pageCount;
        }
    }
}
