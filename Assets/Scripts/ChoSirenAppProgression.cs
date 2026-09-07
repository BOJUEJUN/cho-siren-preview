using System;
using System.Linq;
using ChoSiren.Systems.Tactics;
using ChoSiren.Systems.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    public sealed partial class ChoSirenApp
    {
        private int equipmentMember = -1;
        private GameObject ProgressionModal(string name, string title, float height = 750)
        {
            CloseModal();
            modalObject = NewImage(name, safeRoot, null, new Color32(3, 4, 20, 224));
            Stretch(modalObject.GetComponent<RectTransform>());
            modalObject.GetComponent<Image>().raycastTarget = true;
            GameObject card = NewPanel("Panel", modalObject.transform, new Color32(23, 25, 58, 255), 26);
            RectTransform rect = card.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(620, height);
            FlowText(card.transform, "Title", title, 26, 28, 24, 490, 46);
            FlowButton(card.transform, "CloseProgression", "×", 542, 22, 52, 52, CloseModal);
            return card;
        }
        private void OpenTeamSlotPicker(int slot, int page = 0)
        {
            GameObject card = ProgressionModal("TeamMemberPicker", $"位置 {slot + 1}{(slot == 0 ? " · 队长" : "")}：选择成员", 900);
            FlowText(card.transform, "PickerHelp", "选择即替换；已出战成员会与此位置互换。职业可自由搭配。", 16, 28, 86, 560, 55, Muted);
            int[] owned = model.Save.UnlockedMembers.ToArray();
            int pages = Math.Max(1, (owned.Length + 7) / 8);
            page = Mathf.Clamp(page, 0, pages - 1);
            for (int i = page * 8; i < Math.Min(owned.Length, page * 8 + 8); i++)
            {
                int member = owned[i];
                var m = GameModel.Members[member];
                FlowButton(card.transform, "PickMember-" + member, $"{m.Name} · {m.Career} · Lv.{model.LevelOf(member)}{(model.IsInTeam(member) ? "  [出战]" : "")}",
                    28, 152 + (i % 8) * 74, 564, 64, () =>
                    { model.ReplaceTeamSlot(slot, member, out string message); ShowScreen("team"); Toast(message); });
            }
            int p = page;
            FlowButton(card.transform, "PickerPrevious", "上一页", 28, 778, 170, 58, () => OpenTeamSlotPicker(slot, p - 1)).GetComponent<Button>().interactable = page > 0;
            FlowText(card.transform, "PickerPage", $"{page + 1}/{pages}", 20, 275, 778, 100, 58);
            FlowButton(card.transform, "PickerNext", "下一页", 422, 778, 170, 58, () => OpenTeamSlotPicker(slot, p + 1)).GetComponent<Button>().interactable = page + 1 < pages;
        }
        private void OpenTeamReplacement(int member)
        {
            GameObject card = ProgressionModal("TeamReplacementPicker", $"让{GameModel.Members[member].Name}替换谁？", 500);
            for (int slot = 0; slot < model.Save.Team.Count; slot++)
            {
                int captured = slot;
                FlowButton(card.transform, "ReplaceSlot-" + slot, $"位置 {slot + 1} · {GameModel.Members[model.Save.Team[slot]].Name}{(slot == 0 ? "（队长）" : "")}",
                    28, 108 + slot * 82, 564, 66, () =>
                    { model.ReplaceTeamSlot(captured, member, out string message); ShowScreen("team"); Toast(message); });
            }
        }
        private void OpenCurrency(string currency)
        {
            GameObject card = ProgressionModal("CurrencyModal", GameModel.CurrencyName(currency), 740);
            FlowText(card.transform, "CurrencyBalance", $"当前持有  {model.Balance(currency):N0}", 22, 28, 90, 560, 44, Cyan);
            FlowText(card.transform, "CurrencyHelp", model.CurrencyHelpDescription(currency), 20, 28, 151, 564, 286, Muted);
            bool diamond = currency == CurrencyIds.Diamond;
            EconomyExchangeQuote quote = currency == CurrencyIds.Stamina ? model.PreviewStaminaRefill() : model.PreviewGoldExchange();
            if (!diamond)
            {
                string label = quote.CanPurchase ? $"{quote.DiamondCost} 星钻 → {quote.ReceiveAmount:N0} {GameModel.CurrencyName(currency)}" : quote.UnavailableReason;
                FlowButton(card.transform, "CurrencyExchange", label, 28, 459, 564, 62, () => OpenCurrencyConfirmation(currency, quote))
                    .GetComponent<Button>().interactable = quote.CanPurchase;
            }
            else
                FlowText(card.transform, "NoRealPayment", "真实充值暂未开放 · 无支付入口", 20, 28, 459, 564, 62, Pink);
            FlowButton(card.transform, "CurrencyTasks", "前往任务领取奖励", 28, 549, 564, 60, () => { CloseModal(); OpenDailyTasks(); });
            FlowButton(card.transform, "CurrencyStages", "前往关卡获取奖励", 28, 631, 564, 60, () => { CloseModal(); OpenLevelMap(); });
        }
        private void OpenCurrencyConfirmation(string currency, EconomyExchangeQuote quote)
        {
            GameObject card = ProgressionModal("CurrencyConfirmation", "确认兑换", 410);
            FlowText(card.transform, "ExchangeQuote", quote.ConfirmationText, 23, 30, 104, 558, 94);
            bool submitted = false;
            FlowButton(card.transform, "ConfirmExchange", "确认扣除星钻", 30, 262, 268, 66, () =>
            {
                if (submitted) return;
                submitted = true;
                string message;
                if (currency == CurrencyIds.Stamina) model.TryRefillStamina(quote.ReceiveAmount, quote.DiamondCost, out message);
                else model.TryExchangeDiamondsForGold(out message);
                OpenCurrency(currency); Toast(message);
            });
            FlowButton(card.transform, "CancelExchange", "取消", 322, 262, 268, 66, () => OpenCurrency(currency));
        }
        private void BuildPersonalEquipment()
        {
            BuildAccessoryStageBackdrop();
            ScreenTitle("角色装备", "舞台饰品", "每人一个装备位 · 同一饰品只能装备给一人");
            if (!model.IsUnlocked(equipmentMember)) equipmentMember = model.Save.Team[0];
            if (selectedAccessoryIndex < 0 || selectedAccessoryIndex >= GameModel.AccessoryNames.Length)
                selectedAccessoryIndex = Math.Max(0, model.EquippedAccessoryFor(equipmentMember));
            int member = equipmentMember, item = selectedAccessoryIndex;
            GameObject viewport = NewImage("EquipmentScroll", contentRoot, null, Color.clear);
            RectTransform vr = viewport.GetComponent<RectTransform>();
            Stretch(vr, 20, 0, -20, -104);
            viewport.AddComponent<RectMask2D>();
            GameObject body = NewImage("EquipmentBody", viewport.transform, null, Color.clear);
            PlaceTop(body.GetComponent<RectTransform>(), 0, 0, 680, 1058);
            ScrollRect scroll = viewport.AddComponent<ScrollRect>();
            scroll.viewport = vr;
            scroll.content = body.GetComponent<RectTransform>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            Transform root = body.transform;

            GameObject selector = NewPanel("EquipmentMemberSelector", root, Glass, 20);
            PlaceTop(selector.GetComponent<RectTransform>(), 0, 0, 680, 86);
            FlowButton(selector.transform, "EquipmentPreviousMember", "‹", 12, 16, 56, 54, () => CycleEquipmentMember(-1));
            FlowButton(selector.transform, "EquipmentNextMember", "›", 612, 16, 56, 54, () => CycleEquipmentMember(1));
            FlowText(selector.transform, "EquipmentMemberName", $"{GameModel.Members[member].Name} · 等级 {model.LevelOf(member)}", 23, 88, 8, 504, 34);
            int current = model.EquippedAccessoryFor(member);
            FlowText(selector.transform, "EquipmentMemberStatus", $"{(model.IsInTeam(member) ? "出战中" : "待命")} · 当前：{(current < 0 ? "未装备" : GameModel.AccessoryNames[current])}", 16, 88, 45, 504, 28, Cyan);

            GameObject detail = NewPanel("AccessoryDetail", root, new Color32(18, 23, 54, 245), 22);
            PlaceTop(detail.GetComponent<RectTransform>(), 0, 100, 680, 488);
            GameObject portrait = NewImage("EquipmentPortrait", detail.transform, Resources.Load<Sprite>(GameModel.Members[member].ResourcePath), White);
            PlaceTop(portrait.GetComponent<RectTransform>(), 16, 18, 208, 240);
            portrait.GetComponent<Image>().preserveAspect = true;
            GameObject icon = NewImage("EquipmentSelectedArt", detail.transform, AccessoryItemSprite(item), White);
            PlaceTop(icon.GetComponent<RectTransform>(), 232, 18, 74, 74);
            icon.GetComponent<Image>().preserveAspect = true;
            FlowText(detail.transform, "EquipmentItemName", GameModel.AccessoryNames[item], 24, 320, 16, 342, 40);
            int owner = model.AccessoryOwner(item);
            string state = !model.OwnsAccessory(item) ? "尚未获得" : owner < 0 ? "未装备" : $"{GameModel.Members[owner].Name} 使用中";
            FlowText(detail.transform, "EquipmentItemOwner", $"{state} · 强化 +{model.AccessoryUpgradeLevel(item)}", 16, 320, 58, 342, 28, Cyan);
            int candidateItem = current == item ? -1 : item;
            CombatStats before = model.StatsOf(member), after = model.StatsOf(member, candidateItem);
            string[] names = { "生命", "攻击", "防御", "队伍战力" };
            int[] oldValues = { before.Hp, before.Attack, before.Defense, model.TeamPower };
            int[] newValues = { after.Hp, after.Attack, after.Defense, model.TeamPowerWithMemberAccessory(member, candidateItem) };
            FlowText(detail.transform, "EquipmentCompareHeading", current == item ? "当前     →     卸下后" : "当前     →     装备后", 16, 346, 106, 302, 28, Muted);
            for (int row = 0; row < 4; row++)
            {
                FlowText(detail.transform, "AccessoryStatName-" + row, names[row], 18, 236, 143 + row * 34, 112, 32, Muted);
                FlowText(detail.transform, "AccessoryBefore-" + row, oldValues[row].ToString("N0"), 18, 354, 143 + row * 34, 130, 32);
                FlowText(detail.transform, "AccessoryAfter-" + row, newValues[row].ToString("N0"), 18, 518, 143 + row * 34, 138, 32, Cyan);
            }
            CombatStatBonuses bonus = model.EffectiveAccessoryBonuses(item);
            FlowText(detail.transform, "AccessoryEffects", $"生命 +{bonus.Hp / 10f:0.#}%\n攻击 +{bonus.Attack / 10f:0.#}%\n防御 +{bonus.Defense / 10f:0.#}%", 17, 30, 270, 195, 78, Cyan);
            int delta = model.TeamPowerWithMemberAccessory(member, candidateItem) - model.TeamPower;
            FlowText(detail.transform, "AccessoryPowerChange", $"战力变化 {(delta > 0 ? "+" : "")}{delta:N0}", 18, 236, 290, 400, 32, delta >= 0 ? Cyan : Pink);
            FlowText(detail.transform, "EquipmentSource", $"来源：{GameModel.AccessorySource(item)}\n重复获得转为 3 强化碎片", 16, 236, 327, 418, 52, Muted);
            string equipLabel = !model.OwnsAccessory(item) ? "尚未获得 · 去关卡" : current == item ? "卸下饰品"
                : owner >= 0 ? $"从{GameModel.Members[owner].Name}转移" : "装备给当前角色";
            FlowButton(detail.transform, "AccessoryEquip", equipLabel, 20, 398, 310, 62, () =>
            {
                if (!model.OwnsAccessory(item)) { OpenLevelMap(); return; }
                model.EquipAccessoryForMember(member, item, out string message);
                ShowScreen("accessory"); Toast(message);
            });
            bool upgrade = model.CanUpgradeAccessory(item, out int fragments, out int gold);
            GameObject improve = FlowButton(detail.transform, "AccessoryUpgrade", model.AccessoryUpgradeLevel(item) >= 3 ? "已满级 +3"
                : $"强化 · 碎片 {fragments} / 金币 {gold}", 350, 398, 310, 62, () =>
            {
                model.UpgradeAccessory(item, out string message); ShowScreen("accessory"); Toast(message);
            });
            improve.GetComponent<Button>().interactable = upgrade;
            FlowText(root, "EquipmentInventoryTitle", $"饰品收藏 {model.Save.OwnedAccessories.Count}/{GameModel.AccessoryNames.Length}      强化碎片 {model.Save.EquipmentFragments}", 21, 8, 602, 660, 42);
            for (int i = 0; i < GameModel.AccessoryNames.Length; i++)
            {
                int captured = i;
                float x = i % 3 * 230, y = 654 + i / 3 * 166;
                GameObject card = FlowButton(root, "Accessory-" + i, string.Empty, x, y, 220, 152, () =>
                { selectedAccessoryIndex = captured; ShowScreen("accessory"); });
                card.GetComponent<Image>().color = i == item ? new Color32(72, 50, 115, 250) : new Color32(27, 32, 64, 245);
                GameObject art = NewImage("ItemArt", card.transform, AccessoryItemSprite(i), model.OwnsAccessory(i) ? White : new Color(1, 1, 1, .4f));
                PlaceTop(art.GetComponent<RectTransform>(), 73, 6, 74, 72);
                art.GetComponent<Image>().preserveAspect = true;
                FlowText(card.transform, "ItemName", GameModel.AccessoryNames[i], 18, 12, 82, 196, 29);
                int wearer = model.AccessoryOwner(i);
                FlowText(card.transform, "ItemStatus", !model.OwnsAccessory(i) ? $"掉落：{GameModel.AccessorySource(i)}" : wearer < 0 ? "已拥有 · 空闲" : $"装备：{GameModel.Members[wearer].Name}", 14, 12, 117, 196, 25, Cyan);
            }
            FlowText(root, "EquipmentRules", "只有穿戴者获得属性。更换角色不自动换装。\n同一件饰品转移后，原角色会卸下；变更从下场战斗生效。", 16, 12, 994, 654, 60, Muted);
        }

        private void CycleEquipmentMember(int direction)
        {
            var owned = model.Save.UnlockedMembers;
            equipmentMember = owned[(owned.IndexOf(equipmentMember) + direction + owned.Count) % owned.Count];
            selectedAccessoryIndex = Math.Max(0, model.EquippedAccessoryFor(equipmentMember));
            ShowScreen("accessory");
        }
        private Text FlowText(Transform parent, string name, string value, int size, float x, float y, float width, float height, Color? color = null)
        {
            Text text = NewPlacedText(parent, value, size, color ?? White, x, y, width, height, TextAnchor.MiddleLeft);
            text.name = name;
            return text;
        }
        private GameObject FlowButton(Transform parent, string name, string value, float x, float y, float width, float height, UnityEngine.Events.UnityAction action)
        {
            GameObject button = NewButton(name, parent, value, 18, Purple, White, action);
            PlaceTop(button.GetComponent<RectTransform>(), x, y, width, height);
            return button;
        }
    }
}
