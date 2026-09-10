using System;
using System.Linq;
using ChoSiren.Systems.Presentation;
using ChoSiren.Systems.Tactics;
using ChoSiren.Systems.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    public sealed partial class ChoSirenApp
    {
        private int equipmentMember = -1;
        private int equipmentPage;
        private string equipmentCategory = "全部";
        private bool equipmentOwnedOnly;
        private Action memberProfileReturn;
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
        private void OpenTeamSlotPicker(int slot, int page = 0) => OpenOwnedMemberPicker(slot, page, false);
        private void OpenOwnedMemberPicker(int slot, int page, bool equipmentSelection)
            => OpenMemberSelection(equipmentSelection ? "equipment" : "team", slot, page);
        private void OpenCaptainPicker() => OpenMemberSelection("captain", 0, 0);
        private void OpenMemberSelection(string purpose, int slot, int page, bool ownedOnly = true, string query = "")
        {
            bool equipmentSelection = purpose == "equipment", captain = purpose == "captain", replacement = purpose == "replacement";
            string title = equipmentSelection ? "选择饰品管理角色" : captain ? "选择队长" : replacement ? $"让{GameModel.Members[slot].Name}替换谁？" : $"位置 {slot + 1}：选择成员";
            GameObject card = ProgressionModal(equipmentSelection ? "EquipmentMemberPicker" : captain ? "CaptainMemberPicker" : replacement ? "TeamReplacementPicker" : "TeamMemberPicker", title, 900);
            FlowText(card.transform, "PickerHelp", equipmentSelection ? "仅切换管理对象，不改变编队或转移装备。未签约角色只能查看。" : captain ? "点击查看队长效果并任命；待命成员任命时会替换当前队长。" : replacement ? "选择要替换的出战位置；原成员和装备均保留。" : "已出战成员与此位置互换；未签约角色只能查看。", 16, 28, 86, 560, 55, Muted);
            FlowButton(card.transform, "PickerOwnership", ownedOnly ? "已拥有 ✓ · 切换全部" : "全部 · 只看已拥有", 28, 146, 252, 44,
                () => OpenMemberSelection(purpose, slot, 0, !ownedOnly, query)).GetComponent<Button>().interactable = !replacement;
            GameObject search = NewPanel("PickerSearch", card.transform, new Color32(38, 46, 82, 255), 10);
            PlaceTop(search.GetComponent<RectTransform>(), 292, 146, 300, 44);
            Text placeholder = FlowText(search.transform, "Placeholder", "搜索角色名 · 回车", 15, 12, 6, 276, 32, Muted);
            Text searchValue = FlowText(search.transform, "Value", query, 15, 12, 6, 276, 32);
            InputField input = search.AddComponent<InputField>();
            input.targetGraphic = search.GetComponent<Image>(); input.textComponent = searchValue; input.placeholder = placeholder;
            input.characterLimit = 12; input.text = query;
            input.onEndEdit.AddListener(value =>
            {
                string normalized = (value ?? "").Trim();
                if (normalized != query) OpenMemberSelection(purpose, slot, 0, ownedOnly, normalized);
            });
            int[] owned = (replacement ? model.Save.Team : Enumerable.Range(0, GameModel.Members.Length))
                .Where(i => MemberRosterVisibility.MatchesRosterFilter(model.IsUnlocked(i),
                    GameModel.Members[i].Name, GameModel.Members[i].Career, GameModel.Members[i].Race,
                    ownedOnly, string.Empty, string.Empty, query))
                .OrderBy(i => model.IsUnlocked(i) ? 0 : 1).ThenBy(i => i).ToArray();
            int pages = Math.Max(1, (owned.Length + 7) / 8);
            page = Mathf.Clamp(page, 0, pages - 1);
            for (int i = page * 8; i < Math.Min(owned.Length, page * 8 + 8); i++)
            {
                int member = owned[i];
                var m = GameModel.Members[member];
                GameObject row = FlowButton(card.transform, "PickMember-" + member, string.Empty,
                    28, 204 + (i % 8) * 68, 564, 62, () =>
                    {
                        if (!model.IsUnlocked(member) || captain)
                        {
                            OpenMember(member);
                            memberProfileReturn = () => OpenMemberSelection(purpose, slot, page, ownedOnly, query);
                            return;
                        }
                        if (equipmentSelection)
                        { equipmentMember = member; selectedAccessoryIndex = Math.Max(0, model.EquippedAccessoryFor(member)); ShowScreen("accessory"); return; }
                        int targetSlot = replacement ? model.Save.Team.IndexOf(member) : slot;
                        model.ReplaceTeamSlot(targetSlot, replacement ? slot : member, out string message); ShowScreen("team"); Toast(message);
                    });
                if (replacement) row.name = "ReplaceSlot-" + model.Save.Team.IndexOf(member);
                row.GetComponent<Image>().color = new Color32(38, 46, 82, 255);
                AddQuietPanelEdge(row);
                GameObject portrait = NewImage("PickerPortrait-" + member, row.transform,
                    model.IsUnlocked(member) ? Resources.Load<Sprite>(m.ResourcePath) : LockedSilhouetteSprite(),
                    model.IsUnlocked(member) ? White : LockedSilhouetteTint);
                PlaceTop(portrait.GetComponent<RectTransform>(), 10, 3, 62, 58);
                portrait.GetComponent<Image>().preserveAspect = true;
                portrait.GetComponent<Image>().raycastTarget = false;
                FlowText(row.transform, "PickerName-" + member,
                    model.IsUnlocked(member) ? m.Name : MemberRosterVisibility.LockedName, 19, 88, 5, 262, 27);
                FlowText(row.transform, "PickerRole-" + member, model.IsUnlocked(member)
                        ? $"{m.Career} · Lv.{model.LevelOf(member)} · 战力 {model.PowerOf(member):N0}"
                        : $"{MemberRosterVisibility.LockedCareer} · 仅可查看档案", 14, 88, 34, 330, 23, Muted);
                FlowText(row.transform, "PickerState-" + member, MemberDeploymentLabel(member), 15, 434, 18, 112, 28,
                    model.IsInTeam(member) ? Cyan : Pink);
            }
            int p = page;
            if (owned.Length == 0) FlowText(card.transform, "PickerEmpty", "没有符合条件的角色，可修改搜索或切换全部。", 18, 28, 224, 564, 60, Muted);
            FlowButton(card.transform, "PickerPrevious", "上一页", 28, 778, 170, 58, () => OpenMemberSelection(purpose, slot, p - 1, ownedOnly, query)).GetComponent<Button>().interactable = page > 0;
            FlowText(card.transform, "PickerPage", $"{page + 1}/{pages}", 20, 275, 778, 100, 58);
            FlowButton(card.transform, "PickerNext", "下一页", 422, 778, 170, 58, () => OpenMemberSelection(purpose, slot, p + 1, ownedOnly, query)).GetComponent<Button>().interactable = page + 1 < pages;
        }
        private string MemberDeploymentLabel(int member)
        {
            if (!model.IsUnlocked(member)) return "未签约";
            int slot = model.Save.Team.IndexOf(member);
            return slot == 0 ? "队长" : slot > 0 ? "出战中" : "待命";
        }
        // 训练/展示专项：队长特性文案抽到 MemberCaptainTraits，本处仅保留原调用点，
        // 文案与效果不变，便于成员档案分组展示与独立测试共用同一份真实配置。
        private static string CaptainEffectCopy(string race) => MemberCaptainTraits.Describe(race);
        private void ConfirmCaptain(int member)
        {
            if (!model.IsUnlocked(member)) { Toast("请先签约该成员"); return; }
            if (model.IsInTeam(member)) { ApplyCaptain(member); return; }
            GameObject card = ProgressionModal("CaptainConfirmation", "上阵并任命队长", 410);
            string previous = model.Save.Team.Count > 0 ? GameModel.Members[model.Save.Team[0]].Name : "空位";
            FlowText(card.transform, "CaptainReplacementNotice", $"{GameModel.Members[member].Name}将替换{previous}的队长位置。\n原成员转为待命，等级和装备保留；其他队员不变。", 20, 28, 98, 564, 110, Muted);
            FlowButton(card.transform, "ConfirmCaptain", "确认任命", 28, 278, 270, 62, () => ApplyCaptain(member));
            FlowButton(card.transform, "CancelCaptain", "返回档案", 322, 278, 270, 62, () => OpenMember(member));
        }
        private void ApplyCaptain(int member)
        {
            model.AppointCaptain(member, out string message);
            ShowScreen("team"); Toast(message);
        }
        private void OpenTeamReplacement(int member)
            => OpenMemberSelection("replacement", member, 0);
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
            ScreenTitle("角色装备", "舞台饰品", "七个部位 · 同部位一件 · 属性相加，不连乘");
            if (!model.IsUnlocked(equipmentMember)) equipmentMember = model.Save.Team[0];
            if (selectedAccessoryIndex < 0 || selectedAccessoryIndex >= GameModel.AccessoryNames.Length)
                selectedAccessoryIndex = Math.Max(0, model.EquippedAccessoryFor(equipmentMember));
            int member = equipmentMember, item = selectedAccessoryIndex;
            GameObject viewport = NewImage("EquipmentScroll", contentRoot, null, Color.clear);
            RectTransform vr = viewport.GetComponent<RectTransform>();
            Stretch(vr, 20, 0, -20, -104);
            viewport.AddComponent<RectMask2D>();
            GameObject body = NewImage("EquipmentBody", viewport.transform, null, Color.clear);
            int[] inventory = Enumerable.Range(0, GameModel.AccessoryNames.Length)
                .Where(i => (equipmentCategory == "全部" || GameModel.AccessoryCategory(i) == equipmentCategory)
                    && (!equipmentOwnedOnly || model.OwnsAccessory(i))).ToArray();
            int pageCount = Math.Max(1, (inventory.Length + 11) / 12);
            equipmentPage = Mathf.Clamp(equipmentPage, 0, pageCount - 1);
            int[] visibleItems = inventory.Skip(equipmentPage * 12).Take(12).ToArray();
            float inventoryBottom = 706 + Mathf.CeilToInt(visibleItems.Length / 3f) * 166;
            PlaceTop(body.GetComponent<RectTransform>(), 0, 0, 680, inventoryBottom + 320);
            ScrollRect scroll = viewport.AddComponent<ScrollRect>();
            scroll.viewport = vr;
            scroll.content = body.GetComponent<RectTransform>();
            scroll.horizontal = false;
            scroll.scrollSensitivity = 48f;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            Transform root = body.transform;

            GameObject selector = NewPanel("EquipmentMemberSelector", root, Glass, 20);
            PlaceTop(selector.GetComponent<RectTransform>(), 0, 0, 680, 86);
            FlowButton(selector.transform, "EquipmentChooseMember", "选择角色", 518, 16, 150, 54, () => OpenOwnedMemberPicker(0, 0, true));
            FlowText(selector.transform, "EquipmentMemberName", $"{GameModel.Members[member].Name} · 等级 {model.LevelOf(member)}", 23, 20, 8, 480, 34);
            int current = model.EquippedAccessoryInSlot(member, GameModel.AccessoryCategory(item));
            FlowText(selector.transform, "EquipmentMemberStatus", $"{MemberDeploymentLabel(member)} · 已穿戴 {model.EquippedAccessoriesFor(member).Length}/7 件", 16, 20, 45, 480, 28, Cyan);
            FlowText(root, "WornEquipmentTitle", "当前穿戴 · 点击部位筛选背包", 16, 8, 94, 660, 26, Cyan);
            for (int s = 1; s < GameModel.AccessoryCategories.Length; s++)
            {
                string category = GameModel.AccessoryCategories[s];
                int equipped = model.EquippedAccessoryInSlot(member, category);
                GameObject slotCard = FlowButton(root, "EquipmentSlot-" + category, string.Empty, 4 + (s - 1) * 96, 124, 90, 124, () =>
                {
                    equipmentCategory = category; equipmentPage = 0;
                    selectedAccessoryIndex = equipped >= 0 ? equipped : Enumerable.Range(0, GameModel.AccessoryNames.Length).First(i => GameModel.AccessoryCategory(i) == category);
                    ShowScreen("accessory");
                });
                slotCard.GetComponent<Image>().color = new Color32(27, 32, 64, 245);
                if (equipped >= 0)
                {
                    GameObject art = NewImage("EquippedArt", slotCard.transform, AccessoryItemSprite(equipped), White);
                    PlaceTop(art.GetComponent<RectTransform>(), 12, 7, 66, 66);
                    art.GetComponent<Image>().preserveAspect = true;
                    art.GetComponent<Image>().raycastTarget = false;
                }
                FlowText(slotCard.transform, "SlotCategory", category, 14, 8, 74, 74, 22, Cyan);
                FlowText(slotCard.transform, "SlotState", equipped >= 0 ? "已装备" : "空位", 12, 8, 100, 74, 20, Muted);
            }
            GameObject detailsBody = NewImage("EquipmentDetailsBody", root, null, Color.clear);
            PlaceTop(detailsBody.GetComponent<RectTransform>(), 0, 174, 680, inventoryBottom + 146);
            detailsBody.GetComponent<Image>().raycastTarget = false;
            root = detailsBody.transform;

            GameObject detail = NewPanel("AccessoryDetail", root, new Color32(18, 23, 54, 245), 22);
            PlaceTop(detail.GetComponent<RectTransform>(), 0, 100, 680, 488);
            GameObject portrait = NewImage("EquipmentPortrait", detail.transform, Resources.Load<Sprite>(GameModel.Members[member].ResourcePath), White);
            PlaceTop(portrait.GetComponent<RectTransform>(), 16, 18, 208, 240);
            portrait.GetComponent<Image>().preserveAspect = true;
            GameObject icon = NewImage("EquipmentSelectedArt", detail.transform, AccessoryItemSprite(item), White);
            PlaceTop(icon.GetComponent<RectTransform>(), 232, 18, 74, 74);
            icon.GetComponent<Image>().preserveAspect = true;
            FlowText(detail.transform, "EquipmentItemName", GameModel.AccessoryNames[item], 24, 320, 16, 342, 40, AccessoryRarityColor(item));
            int owner = model.AccessoryOwner(item);
            string state = !model.OwnsAccessory(item) ? "尚未获得" : owner < 0 ? "未装备" : $"{GameModel.Members[owner].Name} 使用中";
            FlowText(detail.transform, "EquipmentItemOwner", $"{state} · 强化 +{model.AccessoryUpgradeLevel(item)}", 16, 320, 58, 342, 28, Cyan);
            FlowText(detail.transform, "EquipmentItemQuality", $"{GameModel.AccessoryRarityNameOf(item)} · {GameModel.AccessoryStatDescription(item)}",
                14, 320, 88, 342, 22, AccessoryRarityColor(item));
            bool wearingSelected = current == item;
            int candidateItem = item;
            CombatStats before = wearingSelected ? model.PreviewEquipmentStats(member, member, candidateItem, true) : model.StatsOf(member);
            CombatStats after = model.PreviewEquipmentStats(member, member, candidateItem);
            string[] names = { "生命", "攻击", "防御", "队伍战力" };
            int baselinePower = wearingSelected ? model.PreviewEquipmentTeamPower(member, candidateItem, true) : model.TeamPower;
            int[] oldValues = { before.Hp, before.Attack, before.Defense, baselinePower };
            int[] newValues = { after.Hp, after.Attack, after.Defense, model.PreviewEquipmentTeamPower(member, candidateItem) };
            FlowText(detail.transform, "EquipmentCompareHeading", wearingSelected ? "未装备时   →   当前已装备" : "当前     →     装备后预览", 16, 332, 112, 330, 28, Muted);
            for (int row = 0; row < 4; row++)
            {
                FlowText(detail.transform, "AccessoryStatName-" + row, names[row], 18, 236, 143 + row * 34, 112, 32, Muted);
                FlowText(detail.transform, "AccessoryBefore-" + row, oldValues[row].ToString("N0"), 18, 354, 143 + row * 34, 130, 32);
                FlowText(detail.transform, "AccessoryAfter-" + row, newValues[row].ToString("N0"), 18, 518, 143 + row * 34, 138, 32, Cyan);
            }
            CombatStatBonuses bonus = model.EffectiveAccessoryBonuses(item);
            FlowText(detail.transform, "AccessoryEffects", $"生命 +{bonus.Hp / 10f:0.#}%\n攻击 +{bonus.Attack / 10f:0.#}%\n防御 +{bonus.Defense / 10f:0.#}%", 17, 30, 270, 195, 78, Cyan);
            int delta = newValues[3] - baselinePower;
            FlowText(detail.transform, "AccessoryPowerChange", $"{(wearingSelected ? "已生效 · 战力" : "装备后战力")} {(delta > 0 ? "+" : "")}{delta:N0}", 18, 236, 290, 420, 32, delta >= 0 ? Cyan : Pink);
            FlowText(detail.transform, "EquipmentSource", $"来源：{GameModel.AccessorySource(item)}\n重复获得转为金币 +{GameModel.DuplicateAccessoryGold}", 16, 236, 327, 418, 52, Muted);
            string equipLabel = !model.OwnsAccessory(item) ? "尚未获得 · 去关卡" : current == item ? "卸下饰品"
                : owner >= 0 ? $"从{GameModel.Members[owner].Name}转移" : "装备给当前角色";
            FlowButton(detail.transform, "AccessoryEquip", equipLabel, 20, 398, 310, 62, () =>
            {
                if (!model.OwnsAccessory(item)) { OpenLevelMap(); return; }
                model.EquipAccessoryForMember(member, item, out string message);
                ShowScreen("accessory"); Toast(message);
            });
            bool upgrade = model.CanUpgradeAccessory(item, out int gold);
            GameObject improve = FlowButton(detail.transform, "AccessoryUpgrade", model.AccessoryUpgradeLevel(item) >= 3 ? "已满级 +3"
                : $"强化 · 金币 {gold}", 350, 398, 310, 62, () =>
            {
                model.UpgradeAccessory(item, out string message); ShowScreen("accessory"); Toast(message);
            });
            improve.GetComponent<Button>().interactable = upgrade;
            FlowText(root, "EquipmentInventoryTitle", $"饰品收藏 {model.Save.OwnedAccessories.Count}/{GameModel.AccessoryNames.Length}", 21, 8, 602, 660, 42);
            FlowButton(root, "EquipmentCategoryFilter", "类别：" + equipmentCategory, 8, 648, 318, 42, () =>
            {
                string[] categories = new[] { "全部" }.Concat(GameModel.AccessoryCategories.Where(c => c != "全部")).ToArray();
                equipmentCategory = categories[(Array.IndexOf(categories, equipmentCategory) + 1) % categories.Length];
                equipmentPage = 0; ShowScreen("accessory");
            });
            FlowButton(root, "EquipmentOwnedFilter", equipmentOwnedOnly ? "只看已拥有：开" : "只看已拥有：关", 346, 648, 318, 42, () =>
            { equipmentOwnedOnly = !equipmentOwnedOnly; equipmentPage = 0; ShowScreen("accessory"); });
            for (int slot = 0; slot < visibleItems.Length; slot++)
            {
                int i = visibleItems[slot];
                int captured = i;
                float x = slot % 3 * 230, y = 706 + slot / 3 * 178;
                GameObject card = FlowButton(root, "Accessory-" + i, string.Empty, x, y, 220, 166, () =>
                { selectedAccessoryIndex = captured; ShowScreen("accessory"); });
                card.GetComponent<Image>().color = i == item ? new Color32(72, 50, 115, 250) : new Color32(27, 32, 64, 245);
                GameObject art = NewImage("ItemArt", card.transform, AccessoryItemSprite(i), model.OwnsAccessory(i) ? White : new Color(1, 1, 1, .4f));
                PlaceTop(art.GetComponent<RectTransform>(), 73, 6, 74, 72);
                art.GetComponent<Image>().preserveAspect = true;
                FlowText(card.transform, "ItemName", GameModel.AccessoryNames[i], 18, 12, 82, 196, 29, AccessoryRarityColor(i));
                // 品质+属性压成一行窄卡文案：去掉空格分隔并用 BestFit 兜底，避免传说三属性被行高裁切。
                Text quality = FlowText(card.transform, "ItemQuality",
                    GameModel.AccessoryRarityNameOf(i) + "·" + GameModel.AccessoryStatDescription(i)
                        .Replace(" · ", "·").Replace("攻击 ", "攻").Replace("防御 ", "防"),
                    12, 8, 113, 204, 22, AccessoryRarityColor(i));
                ChoSiren.Panels.PanelKit.EnableBestFit(quality, 10);
                int wearer = model.AccessoryOwner(i);
                FlowText(card.transform, "ItemStatus", !model.OwnsAccessory(i) ? $"掉落：{GameModel.AccessorySource(i)}" : wearer < 0 ? "已拥有 · 空闲" : $"装备：{GameModel.Members[wearer].Name}", 14, 12, 137, 196, 25, Cyan);
            }
            if (visibleItems.Length == 0)
                FlowText(root, "EquipmentEmpty", "暂无符合筛选条件的饰品", 18, 16, inventoryBottom, 648, 36, Muted);
            FlowButton(root, "EquipmentPreviousPage", "上一页", 12, inventoryBottom + 40, 180, 42,
                () => { equipmentPage = Math.Max(0, equipmentPage - 1); RefreshEquipmentCollection(); }).GetComponent<Button>().interactable = equipmentPage > 0;
            FlowText(root, "EquipmentPageCount", $"{equipmentPage + 1} / {pageCount} · 共 {inventory.Length} 件", 17, 202, inventoryBottom + 40, 270, 42, Muted);
            FlowButton(root, "EquipmentNextPage", "下一页", 484, inventoryBottom + 40, 180, 42,
                () => { equipmentPage = Math.Min(pageCount - 1, equipmentPage + 1); RefreshEquipmentCollection(); }).GetComponent<Button>().interactable = equipmentPage + 1 < pageCount;
            FlowText(root, "EquipmentRules", "同部位替换，其他部位保留；转移仅卸下原角色的这一件。\n只有穿戴者获得属性；待命成员装备不增加出战队伍战力。", 15, 12, inventoryBottom + 88, 654, 54, Muted);
        }

        private void CycleEquipmentMember(int direction)
        {
            var owned = model.Save.UnlockedMembers;
            equipmentMember = owned[(owned.IndexOf(equipmentMember) + direction + owned.Count) % owned.Count];
            selectedAccessoryIndex = Math.Max(0, model.EquippedAccessoryFor(equipmentMember));
            ShowScreen("accessory");
        }

        private void RefreshEquipmentCollection()
        {
            ScrollRect oldScroll = contentRoot.GetComponentInChildren<ScrollRect>();
            float position = oldScroll != null ? oldScroll.verticalNormalizedPosition : 1f;
            ShowScreen("accessory");
            Canvas.ForceUpdateCanvases();
            ScrollRect newScroll = contentRoot.GetComponentInChildren<ScrollRect>();
            if (newScroll != null) newScroll.verticalNormalizedPosition = Mathf.Clamp01(position);
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
