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
                (equipmentSlotFilter >= 0 ? GameModel.AccessorySlot(i) == equipmentSlotFilter
                    : equipmentCategory == "全部" || GameModel.AccessoryCategory(i) == equipmentCategory) &&
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
            Image equipmentBoard = board.GetComponent<Image>();
            GameObject body = NewImage("EquipmentBody", board, null, Color.clear);
            RectTransform root = body.GetComponent<RectTransform>();
            // The regenerated board is wider than the original 858px content column
            // (art outpainted symmetrically so the page fills the usable canvas and
            // the internal black side stripes are gone). Inset the body to that
            // original column so every child rect below keeps its authored x.
            float contentPadX = Mathf.Max(0f,
                (equipmentBoard.sprite != null ? equipmentBoard.sprite.rect.width : board.rect.width) - 858f) * .5f;
            Stretch(root, contentPadX, 0f, -contentPadX, 0f);
            body.GetComponent<Image>().raycastTarget = false;
            MemberDefinition m = GameModel.Members[member];
            GameObject selector = BoardButton038("EquipmentCycleMember", root, new Rect(22, 194, 378, 491), () => CycleEquipmentMember(1));
            // FrameBustPortrait intentionally overflows the frame for bust framing;
            // without a mask the portrait spilled left past the crystalline rim.
            selector.AddComponent<RectMask2D>();
            GameObject portrait = NewImage("EquipmentPortrait", selector.transform, Resources.Load<Sprite>(m.ResourcePath), White);
            PanelKit.FrameBustPortrait(portrait.GetComponent<Image>(), selector.GetComponent<RectTransform>(), m.Id);
            portrait.GetComponent<Image>().preserveAspect = true;
            portrait.GetComponent<Image>().raycastTarget = false;
            // Diagonal vertex dissolve: the bust melts into the aperture glow, no waist cut.
            portrait.AddComponent<EquipmentPolish038PortraitFade>();
            // Authored crystalline rim with a jagged transparent window replaces the square clip.
            // Lives outside Art/Reference038: WebGL builds stream that folder separately and
            // exclude it from Resources, so a path under it would load null in the player.
            Sprite apertureSprite = Resources.Load<Sprite>("Art/EquipmentPolish038-aperture-overlay");
            GameObject aperture = NewImage("EquipmentPolish038Aperture", selector.transform, apertureSprite,
                apertureSprite == null ? Color.clear : White);
            if (apertureSprite == null)
                Debug.LogError("EquipmentPolish038 aperture overlay sprite missing; hiding instead of rendering an opaque quad.");
            PlaceTop(aperture.GetComponent<RectTransform>(), 0, 0, 378, 491);
            aperture.GetComponent<Image>().raycastTarget = false;
            // Slanted crystalline ribbon at the aperture base carries name/level/power.
            GameObject memberPlaque = NewImage("EquipmentMemberPlaque", selector.transform,
                EquipmentPolish038Sprites.Plaque(), White);
            PlaceTop(memberPlaque.GetComponent<RectTransform>(), 4, 376, 348, 113);
            memberPlaque.GetComponent<Image>().raycastTarget = false;
            Text memberName = BoardText038(memberPlaque.transform, "EquipmentMemberName",
                $"{m.Name} · 等级 {model.LevelOf(member)}", 23, White, 58, 14, 250, 38);
            PanelKit.EnableBestFit(memberName, 18);
            memberName.verticalOverflow = VerticalWrapMode.Truncate;
            Text memberPower = BoardText038(memberPlaque.transform, "EquipmentMemberPower",
                $"战力 {model.PowerOf(member):N0}", 18, Cyan, 64, 54, 240, 30);
            PanelKit.EnableBestFit(memberPower, 15);
            memberPower.verticalOverflow = VerticalWrapMode.Truncate;
            // Four fixed gear slots (耳饰/项链/手饰/头饰), one item each, down the portrait's
            // left edge: the worn item's icon or an empty "+"; tapping filters the list to that slot.
            for (int slot = 0; slot < GameModel.EquipmentSlotCount; slot++)
            {
                int s = slot;
                int worn = model.EquippedInSlot(member, s);
                bool active = equipmentSlotFilter == s;
                GameObject chip = BoardButton038("EquipmentSlot-" + s, root, new Rect(30, 204 + s * 88, 80, 80), () =>
                {
                    equipmentSlotFilter = equipmentSlotFilter == s ? -1 : s;
                    if (equipmentSlotFilter >= 0 && worn >= 0) selectedAccessoryIndex = worn;
                    equipmentPage = 0; equipmentRevealSelection = true; ShowScreen("accessory");
                });
                Image chipBack = chip.GetComponent<Image>();
                chipBack.color = active ? new Color32(255, 46, 147, 235) : new Color32(12, 8, 22, 220);
                Outline rim = chip.AddComponent<Outline>();
                rim.effectColor = active ? (Color)White : (worn >= 0 ? Accent : new Color(1, 1, 1, .35f));
                rim.effectDistance = new Vector2(2, -2);
                if (worn >= 0)
                {
                    GameObject icon = NewImage("Icon", chip.transform, AccessoryItemSprite(worn), White);
                    PlaceTop(icon.GetComponent<RectTransform>(), 6, 4, 68, 54);
                    icon.GetComponent<Image>().preserveAspect = true;
                    icon.GetComponent<Image>().raycastTarget = false;
                }
                else BoardText038(chip.transform, "Empty", "+", 34, new Color(1, 1, 1, .55f), 0, 2, 80, 54, TextAnchor.MiddleCenter);
                BoardText038(chip.transform, "SlotName", GameModel.EquipmentSlotNames[s], 15, White, 0, 56, 80, 22, TextAnchor.MiddleCenter);
            }
            GameObject chooseMember = BoardButton038("EquipmentChooseMember", root, new Rect(712, 442, 137, 244), () => OpenEquipmentMemberSelector());
            BoardText038(root, "EquipmentItemName", GameModel.AccessoryNames[item], 30, White, 442, 268, 305, 48);
            GameObject selectedArt = NewImage("EquipmentSelectedArt", root, AccessoryItemSprite(item), White);
            PlaceTop(selectedArt.GetComponent<RectTransform>(), 440, 321, 278, 205);
            selectedArt.GetComponent<Image>().preserveAspect = true;
            selectedArt.GetComponent<Image>().raycastTarget = false;
            GameModel.StageStats(m, member, model.SigningChannelOf(member) == 0 ? 0 : 1, model.LevelOf(member), out int vocal, out int rap,
                out int dance, out int popularity, out int appearance);
            string[] names = { "声乐", "说唱", "舞蹈", "名气", "颜值" };
            int[] values = { vocal, rap, dance, popularity, appearance };
            for (int i = 0; i < 5; i++)
                BoardText038(root, "EquipmentStageStat-" + i, $"{names[i]} {values[i]}", 16, i == 4 ? Accent : Cyan,
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
                if (equipmentSlotFilter >= 0) { equipmentSlotFilter = -1; equipmentCategory = "全部"; }
                else equipmentCategory = categories[(Array.IndexOf(categories, equipmentCategory) + 1) % categories.Length];
                equipmentPage = 0; ShowScreen("accessory");
            });
            BoardText038(category.transform, "Label", equipmentSlotFilter >= 0 ? "格子：" + GameModel.EquipmentSlotNames[equipmentSlotFilter] : "类别：" + equipmentCategory, 19, White, 0, 0, 200, 43, TextAnchor.MiddleCenter);
            GameObject owned = BoardButton038("EquipmentOwnedFilter", root, new Rect(604, 720, 220, 43), () =>
            { equipmentOwnedOnly = !equipmentOwnedOnly; equipmentPage = 0; ShowScreen("accessory"); });
            BoardText038(owned.transform, "Label", "只看拥有：" + (equipmentOwnedOnly ? "开" : "关"), 18, White, 0, 0, 220, 43, TextAnchor.MiddleCenter);
            for (int slot = 0; slot < visible.Length; slot++)
            {
                int index = visible[slot];
                int wearer = model.AccessoryOwner(index);
                Rect cardRect = new Rect(44 + (slot % 3) * 270, 786 + (slot / 3) * 190, 233, 180);
                GameObject card = BoardButton038("Accessory-" + index, root, cardRect, () =>
                    { selectedAccessoryIndex = index; ShowScreen("accessory"); });
                // Live jagged card chrome (ContentFrames038): the board art no longer
                // bakes the 12 inventory frames, so filtered/last pages never show
                // empty slots — chrome exists only where a real item does.
                Image cardChrome = card.GetComponent<Image>();
                cardChrome.sprite = Resources.Load<Sprite>("Art/ContentFrames038/accessory-card-frame-038");
                if (cardChrome.sprite != null) cardChrome.color = White;
                GameObject art = NewImage("ItemArt", card.transform, AccessoryItemSprite(index), model.OwnsAccessory(index) ? White : new Color(1, 1, 1, .6f));
                PlaceTop(art.GetComponent<RectTransform>(), 44, 0, 145, 82);
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
                    MainBakedMotion038AttachFootprint(equip, equipmentBoard, 2f);
                    if (owns)
                    {
                        bool can = model.CanUpgradeAccessory(index, out int cost);
                        GameObject upgrade = BoardButton038("QuickUpgrade", card.transform, new Rect(116, 145, 113, 30), () =>
                        { bool ok = model.UpgradeAccessory(index, out string message); RefreshEquipmentCollection(); UpgradeFeedback1022(index, ok, message); });
                        upgrade.GetComponent<Button>().interactable = can;
                        BoardText038(upgrade.transform, "Label", model.AccessoryUpgradeLevel(index) >= 3 ? "已满级" : $"强化 {cost}", 15, can ? White : Muted, 0, 0, 113, 29, TextAnchor.MiddleCenter);
                        MainBakedMotion038AttachFootprint(upgrade, equipmentBoard, 2f);
                    }
                }
                else BoardText038(card.transform, "ItemStatus", !model.OwnsAccessory(index) ? "掉落：" + GameModel.AccessorySource(index)
                    : wearer < 0 ? "已拥有 · 空闲" : "装备：" + GameModel.Members[wearer].Name, 14, Cyan, 5, 145, 227, 30);
            }
            if (visible.Length == 0) BoardText038(root, "EquipmentEmpty", "暂无符合筛选条件的饰品", 25, White, 100, 931, 660, 64, TextAnchor.MiddleCenter);
            GameObject previousPage = BoardButton038("EquipmentPreviousPage", root, new Rect(87, 1566, 219, 72), () =>
            { equipmentPage = Math.Max(0, equipmentPage - 1); RefreshEquipmentCollection(); });
            previousPage.GetComponent<Button>().interactable = equipmentPage > 0;
            BoardText038(root, "EquipmentPageCount", $"{equipmentPage + 1}/{pageCount}", 22, White, 372, 1576, 95, 42, TextAnchor.MiddleCenter);
            GameObject nextPage = BoardButton038("EquipmentNextPage", root, new Rect(548, 1566, 240, 72), () =>
            { equipmentPage = Math.Min(pageCount - 1, equipmentPage + 1); RefreshEquipmentCollection(); });
            nextPage.GetComponent<Button>().interactable = equipmentPage + 1 < pageCount;

            // Whole-body baked feedback for the board-art controls. Footprint-derived
            // regions stay correct under the wider regenerated board no matter how much
            // symmetric outpaint padding the delivered art carries. Accessory cards keep
            // their existing live ItemArt motion (native body), so they are not re-bound.
            MainBakedMotion038AttachFootprint(selector, equipmentBoard, 4f);
            MainBakedMotion038AttachFootprint(chooseMember, equipmentBoard, 4f);
            MainBakedMotion038AttachFootprint(category, equipmentBoard, 3f);
            MainBakedMotion038AttachFootprint(owned, equipmentBoard, 3f);
            MainBakedMotion038AttachFootprint(previousPage, equipmentBoard, 4f);
            MainBakedMotion038AttachFootprint(nextPage, equipmentBoard, 4f);
        }
    }
}
