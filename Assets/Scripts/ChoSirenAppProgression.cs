using System;
using System.Linq;
using ChoSiren.Panels;
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
        private int lastEquipmentMember = -1;
        private int lastEquipmentAccessory = -1;
        private bool equipmentRevealSelection;
        private Action memberProfileReturn;

        /// <summary>
        /// 0.3.8 crystal modal scaffold: dim overlay + approved illustrated backdrop +
        /// centered title + CloseProgression. Returns the safe Body rect for content.
        /// Overlay name keeps the test-facing id (CurrencyModal, pickers, confirmations).
        /// </summary>
        private RectTransform ProgressionModal(string name, string title, string eyebrow, float width)
        {
            CloseModal();
            ModalPolish038Frame frame = ModalPolish038Chrome.Begin(safeRoot, name, title, eyebrow,
                CloseModal, width, closeName: "CloseProgression");
            modalObject = frame.Overlay;
            return frame.Body;
        }

        private static float ModalBodyWidth(float panelWidth)
            => panelWidth * (ModalPolish038Chrome.BodyRight - ModalPolish038Chrome.BodyLeft);

        private void OpenTeamSlotPicker(int slot, int page = 0) => OpenOwnedMemberPicker(slot, page, false);
        private void OpenOwnedMemberPicker(int slot, int page, bool equipmentSelection)
            => OpenMemberSelection(equipmentSelection ? "equipment" : "team", slot, page);
        private void OpenCaptainPicker() => OpenMemberSelection("captain", 0, 0);
        private void OpenMemberSelection(string purpose, int slot, int page, bool ownedOnly = true, string query = "")
        {
            bool equipmentSelection = purpose == "equipment", captain = purpose == "captain", replacement = purpose == "replacement";
            string title = equipmentSelection ? "选择饰品管理角色" : captain ? "选择队长" : replacement ? $"让{GameModel.Members[slot].Name}替换谁？" : $"位置 {slot + 1}：选择成员";
            RectTransform body = ProgressionModal(equipmentSelection ? "EquipmentMemberPicker" : captain ? "CaptainMemberPicker" : replacement ? "TeamReplacementPicker" : "TeamMemberPicker",
                title, equipmentSelection ? "饰品" : "编队", 660f);
            float width = ModalBodyWidth(660f);

            Text help = ModalPolish038Chrome.Text(body, "PickerHelp",
                equipmentSelection ? "仅切换管理对象，不改变编队或转移装备。未签约角色只能查看。"
                    : captain ? "点击查看队长效果并任命；待命成员任命时会替换当前队长。"
                    : replacement ? "选择要替换的出战位置；原成员和装备均保留。"
                    : "已出战成员与此位置互换；未签约角色只能查看。", 14,
                ModalPolish038Chrome.TextMuted, FontStyle.Normal, TextAnchor.MiddleLeft);
            PanelKit.PlaceTop(help.rectTransform, 4f, 0f, width - 8f, 38f);
            PanelKit.EnableBestFit(help, 11);

            GameObject ownership = ModalPolish038Chrome.ActionButton(body, "PickerOwnership",
                ownedOnly ? "已拥有 ✓ · 切换全部" : "全部 · 只看已拥有", 14,
                () => OpenMemberSelection(purpose, slot, 0, !ownedOnly, query),
                ModalPolish038ButtonStyle.Secondary);
            PanelKit.PlaceTop(ownership.GetComponent<RectTransform>(), 0f, 46f, 252f, 44f);
            ModalPolish038Chrome.SetActionEnabled(ownership, !replacement);

            GameObject search = ModalPolish038Chrome.RowPlate(body, "PickerSearch");
            PanelKit.PlaceTop(search.GetComponent<RectTransform>(), 260f, 46f, width - 260f, 44f);
            search.GetComponent<Image>().raycastTarget = true;
            Text placeholder = ModalPolish038Chrome.Text(search.transform, "Placeholder",
                "搜索角色名 · 回车", 13, ModalPolish038Chrome.TextMuted, FontStyle.Normal,
                TextAnchor.MiddleLeft);
            PanelKit.PlaceTop(placeholder.rectTransform, 14f, 6f, width - 290f, 32f);
            Text searchValue = ModalPolish038Chrome.Text(search.transform, "Value", query, 13,
                ModalPolish038Chrome.TextWhite, FontStyle.Normal, TextAnchor.MiddleLeft);
            PanelKit.PlaceTop(searchValue.rectTransform, 14f, 6f, width - 290f, 32f);
            InputField input = search.AddComponent<InputField>();
            input.targetGraphic = search.GetComponent<Image>();
            input.textComponent = searchValue;
            input.placeholder = placeholder;
            input.characterLimit = 12;
            input.text = query;
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
            const float rowTop = 98f;
            const float rowHeight = 54f;
            const float rowGap = 6f;
            for (int i = page * 8; i < Math.Min(owned.Length, page * 8 + 8); i++)
            {
                int member = owned[i];
                var m = GameModel.Members[member];
                RectTransform rowBody;
                GameObject row = ModalPolish038Chrome.RowButton(body, "PickMember-" + member, () =>
                    {
                        if (!model.IsUnlocked(member) || captain)
                        {
                            OpenMember(member);
                            memberProfileReturn = () => OpenMemberSelection(purpose, slot, page, ownedOnly, query);
                            return;
                        }
                        if (equipmentSelection)
                        { equipmentMember = member; selectedAccessoryIndex = Math.Max(0, model.EquippedAccessoryFor(member)); equipmentRevealSelection = true; ShowScreen("accessory"); return; }
                        int targetSlot = replacement ? model.Save.Team.IndexOf(member) : slot;
                        model.ReplaceTeamSlot(targetSlot, replacement ? slot : member, out string message); ShowScreen("team"); Toast(message);
                    }, (i & 1) == 1, out rowBody);
                if (replacement) row.name = "ReplaceSlot-" + model.Save.Team.IndexOf(member);
                PanelKit.PlaceTop(row.GetComponent<RectTransform>(), 0f,
                    rowTop + (i % 8) * (rowHeight + rowGap), width, rowHeight);

                GameObject portraitFrame = NewImage("PickerPortraitFrame-" + member, rowBody, null, Color.clear);
                PanelKit.PlaceTop(portraitFrame.GetComponent<RectTransform>(), 6f, 4f, 44f, 46f);
                portraitFrame.AddComponent<RectMask2D>();
                GameObject portrait = NewImage("PickerPortrait-" + member, portraitFrame.transform,
                    model.IsUnlocked(member) ? Resources.Load<Sprite>(m.ResourcePath) : LockedSilhouetteSprite(),
                    model.IsUnlocked(member) ? White : LockedSilhouetteTint);
                Image pickerPortrait = portrait.GetComponent<Image>();
                pickerPortrait.preserveAspect = true;
                pickerPortrait.raycastTarget = false;
                if (model.IsUnlocked(member))
                    ChoSiren.Panels.PanelKit.FrameBustPortrait(pickerPortrait,
                        portraitFrame.GetComponent<RectTransform>(), m.Id);

                Text rowName = ModalPolish038Chrome.Text(rowBody, "PickerName-" + member,
                    model.IsUnlocked(member) ? m.Name : MemberRosterVisibility.LockedName, 16,
                    ModalPolish038Chrome.TextWhite, FontStyle.Bold, TextAnchor.MiddleLeft);
                PanelKit.PlaceTop(rowName.rectTransform, 58f, 3f, 246f, 24f);
                PanelKit.EnableBestFit(rowName, 12);
                Text role = ModalPolish038Chrome.Text(rowBody, "PickerRole-" + member,
                    model.IsUnlocked(member)
                        ? $"{m.Career} · Lv.{model.LevelOf(member)} · 战力 {model.PowerOf(member):N0}"
                        : $"{MemberRosterVisibility.LockedCareer} · 仅可查看档案", 12,
                    ModalPolish038Chrome.TextMuted, FontStyle.Normal, TextAnchor.MiddleLeft);
                PanelKit.PlaceTop(role.rectTransform, 58f, 29f, 300f, 19f);
                PanelKit.EnableBestFit(role, 10);
                Text state = ModalPolish038Chrome.Text(rowBody, "PickerState-" + member,
                    MemberDeploymentLabel(member), 13,
                    model.IsInTeam(member) ? ModalPolish038Chrome.AccentCyan : ModalPolish038Chrome.AccentPink,
                    FontStyle.Bold, TextAnchor.MiddleRight);
                PanelKit.PlaceTop(state.rectTransform, width - 152f, 15f, 136f, 24f);
            }
            int p = page;
            if (owned.Length == 0)
            {
                GameObject empty = ModalPolish038Chrome.StateBlock(body, "PickerEmpty",
                    ModalPolish038StateKind.Empty, "没有符合条件的角色", "可修改搜索或切换全部");
                PanelKit.PlaceTop(empty.GetComponent<RectTransform>(), 0f, 232f, width, 84f);
            }
            // Pager sits clear of the bottom-right crystal cluster: the cluster mass reaches
            // ~0.72 of panel width at this height, so the group leans left of center.
            float pagerY = 578f;
            GameObject previous = ModalPolish038Chrome.ActionButton(body, "PickerPrevious", "上一页", 15,
                () => OpenMemberSelection(purpose, slot, p - 1, ownedOnly, query),
                ModalPolish038ButtonStyle.Ghost);
            PanelKit.PlaceTop(previous.GetComponent<RectTransform>(), 40f, pagerY, 104f, 38f);
            ModalPolish038Chrome.SetActionEnabled(previous, page > 0);
            Text pageText = ModalPolish038Chrome.Text(body, "PickerPage", $"{page + 1}/{pages}", 18,
                ModalPolish038Chrome.TextWhite, FontStyle.Bold, TextAnchor.MiddleCenter);
            PanelKit.PlaceTop(pageText.rectTransform, 152f, pagerY, 140f, 38f);
            GameObject next = ModalPolish038Chrome.ActionButton(body, "PickerNext", "下一页", 15,
                () => OpenMemberSelection(purpose, slot, p + 1, ownedOnly, query),
                ModalPolish038ButtonStyle.Ghost);
            PanelKit.PlaceTop(next.GetComponent<RectTransform>(), 300f, pagerY, 104f, 38f);
            ModalPolish038Chrome.SetActionEnabled(next, page + 1 < pages);
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
            RectTransform body = ProgressionModal("CaptainConfirmation", "上阵并任命队长", "确认", 470f);
            float width = ModalBodyWidth(470f);
            string previous = model.Save.Team.Count > 0 ? GameModel.Members[model.Save.Team[0]].Name : "空位";

            ModalPolish038Chrome.PortraitSlot(body, "CaptainPortraitFrame",
                Resources.Load<Sprite>(GameModel.Members[member].ResourcePath),
                GameModel.Members[member].Id, 0f, 6f, 104f, 138f, White);
            Text notice = ModalPolish038Chrome.Text(body, "CaptainReplacementNotice",
                $"{GameModel.Members[member].Name}将替换{previous}的队长位置。\n原成员转为待命，等级和装备保留；其他队员不变。",
                14, ModalPolish038Chrome.TextMuted, FontStyle.Normal, TextAnchor.UpperLeft);
            PanelKit.PlaceTop(notice.rectTransform, 120f, 8f, width - 128f, 140f);
            notice.lineSpacing = 1.2f;

            GameObject confirm = ModalPolish038Chrome.ActionButton(body, "ConfirmCaptain", "确认任命", 18,
                () => ApplyCaptain(member), ModalPolish038ButtonStyle.Primary);
            PanelKit.PlaceTop(confirm.GetComponent<RectTransform>(), 0f, 190f, width, 54f);
            GameObject cancel = ModalPolish038Chrome.ActionButton(body, "CancelCaptain", "返回档案", 16,
                () => OpenMember(member), ModalPolish038ButtonStyle.Ghost);
            PanelKit.PlaceTop(cancel.GetComponent<RectTransform>(), 0f, 258f, width, 50f);
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
            RectTransform body = ProgressionModal("CurrencyModal", GameModel.CurrencyName(currency), "资源", 600f);
            float width = ModalBodyWidth(600f);
            Color accent = PanelKit.CurrencyColor(currency);

            GameObject iconFrame = ModalPolish038Chrome.RowPlate(body, "CurrencyIconFrame");
            PanelKit.PlaceTop(iconFrame.GetComponent<RectTransform>(), 0f, 2f, 66f, 66f);
            Sprite icon = PanelKit.CurrencyIcon(currency);
            Image iconImage = NewImage("CurrencyIcon", iconFrame.transform,
                icon ?? RoundedSprite(8), icon != null ? White : accent).GetComponent<Image>();
            PanelKit.PlaceTop(iconImage.rectTransform, 15f, 15f, 36f, 36f);
            iconImage.preserveAspect = true;

            Text balance = ModalPolish038Chrome.Text(body, "CurrencyBalance",
                $"当前持有  {model.Balance(currency):N0}", 23, accent, FontStyle.Bold, TextAnchor.MiddleLeft);
            PanelKit.PlaceTop(balance.rectTransform, 80f, 8f, width - 88f, 36f);
            PanelKit.EnableBestFit(balance, 16);
            if (currency == CurrencyIds.Stamina)
            {
                long wait = model.SecondsUntilNextStamina;
                Text cap = ModalPolish038Chrome.Text(body, "CurrencyCap",
                    wait > 0 ? $"上限 {model.StaminaCap} · 下次恢复 {wait / 60:0}:{wait % 60:00}"
                        : $"上限 {model.StaminaCap} · 自动恢复中", 12,
                    ModalPolish038Chrome.TextMuted, FontStyle.Normal, TextAnchor.MiddleLeft);
                PanelKit.PlaceTop(cap.rectTransform, 80f, 46f, width - 88f, 22f);
            }
            else
            {
                Text kind = ModalPolish038Chrome.Text(body, "CurrencyKind",
                    currency == CurrencyIds.Diamond ? "稀有资源 · 游戏奖励获取" : "养成资源 · 可兑换", 12,
                    ModalPolish038Chrome.TextMuted, FontStyle.Normal, TextAnchor.MiddleLeft);
                PanelKit.PlaceTop(kind.rectTransform, 80f, 46f, width - 88f, 22f);
            }

            ModalPolish038Chrome.Divider(body, "CurrencyDivider", 0f, 84f, width);
            Text helpText = ModalPolish038Chrome.Text(body, "CurrencyHelp",
                model.CurrencyHelpDescription(currency), 15, ModalPolish038Chrome.TextMuted,
                FontStyle.Normal, TextAnchor.UpperLeft);
            PanelKit.PlaceTop(helpText.rectTransform, 4f, 94f, width - 8f, 152f);
            helpText.lineSpacing = 1.25f;

            bool diamond = currency == CurrencyIds.Diamond;
            EconomyExchangeQuote quote = currency == CurrencyIds.Stamina ? model.PreviewStaminaRefill() : model.PreviewGoldExchange();
            if (!diamond)
            {
                string label = quote.CanPurchase
                    ? $"{quote.DiamondCost} 星钻 → {quote.ReceiveAmount:N0} {GameModel.CurrencyName(currency)}"
                    : quote.UnavailableReason;
                GameObject exchange = ModalPolish038Chrome.ActionButton(body, "CurrencyExchange",
                    label, 16, () => OpenCurrencyConfirmation(currency, quote),
                    ModalPolish038ButtonStyle.Primary);
                PanelKit.PlaceTop(exchange.GetComponent<RectTransform>(), 0f, 260f, width, 56f);
                ModalPolish038Chrome.SetActionEnabled(exchange, quote.CanPurchase);
            }
            else
            {
                GameObject locked = ModalPolish038Chrome.StateBlock(body, "NoRealPayment",
                    ModalPolish038StateKind.Locked, "真实充值暂未开放 · 无支付入口",
                    "星钻经关卡、任务、签到和舞台奖励获取");
                PanelKit.PlaceTop(locked.GetComponent<RectTransform>(), 0f, 258f, width, 64f);
            }

            GameObject tasks = ModalPolish038Chrome.ActionButton(body, "CurrencyTasks",
                "前往任务领取奖励", 16, () => { CloseModal(); OpenDailyTasks(); },
                ModalPolish038ButtonStyle.Secondary);
            PanelKit.PlaceTop(tasks.GetComponent<RectTransform>(), 0f, 338f, width, 52f);
            GameObject stages = ModalPolish038Chrome.ActionButton(body, "CurrencyStages",
                "前往关卡获取奖励", 16, () => { CloseModal(); OpenLevelMap(); },
                ModalPolish038ButtonStyle.Secondary);
            PanelKit.PlaceTop(stages.GetComponent<RectTransform>(), 0f, 400f, width, 52f);

            Text footnote = ModalPolish038Chrome.Text(body, "CurrencyFootnote",
                diamond ? "不会扣款或模拟充值到账" : "兑换在确认窗口二次核对 · 未确认不扣费", 12,
                ModalPolish038Chrome.TextMuted, FontStyle.Normal, TextAnchor.MiddleCenter);
            PanelKit.PlaceTop(footnote.rectTransform, 0f, 470f, width, 26f);
        }
        private void OpenCurrencyConfirmation(string currency, EconomyExchangeQuote quote)
        {
            RectTransform body = ProgressionModal("CurrencyConfirmation", "确认兑换", "确认", 460f);
            float width = ModalBodyWidth(460f);

            Text quoteText = ModalPolish038Chrome.Text(body, "ExchangeQuote", quote.ConfirmationText,
                19, ModalPolish038Chrome.TextWhite, FontStyle.Bold, TextAnchor.MiddleCenter);
            PanelKit.PlaceTop(quoteText.rectTransform, 4f, 22f, width - 8f, 84f);
            PanelKit.EnableBestFit(quoteText, 14);
            if (quote.CanPurchase)
            {
                Text after = ModalPolish038Chrome.Text(body, "ExchangeAfter",
                    $"确认后星钻余额 {(model.Save.Diamonds - quote.DiamondCost):N0}", 13,
                    ModalPolish038Chrome.TextMuted, FontStyle.Normal, TextAnchor.MiddleCenter);
                PanelKit.PlaceTop(after.rectTransform, 4f, 110f, width - 8f, 24f);
            }

            bool submitted = false;
            GameObject confirm = ModalPolish038Chrome.ActionButton(body, "ConfirmExchange",
                "确认扣除星钻", 17, () =>
                {
                    if (submitted) return;
                    submitted = true;
                    string message;
                    if (currency == CurrencyIds.Stamina) model.TryRefillStamina(quote.ReceiveAmount, quote.DiamondCost, out message);
                    else model.TryExchangeDiamondsForGold(out message);
                    OpenCurrency(currency); Toast(message);
                }, ModalPolish038ButtonStyle.Primary);
            PanelKit.PlaceTop(confirm.GetComponent<RectTransform>(), 0f, 196f, width, 54f);
            GameObject cancel = ModalPolish038Chrome.ActionButton(body, "CancelExchange", "取消", 16,
                () => OpenCurrency(currency), ModalPolish038ButtonStyle.Ghost);
            PanelKit.PlaceTop(cancel.GetComponent<RectTransform>(), 0f, 264f, width, 50f);
        }
        private void BuildPersonalEquipment() => BuildEquipmentBoard038();

        private void CycleEquipmentMember(int direction)
        {
            var owned = model.Save.UnlockedMembers;
            equipmentMember = owned[(owned.IndexOf(equipmentMember) + direction + owned.Count) % owned.Count];
            selectedAccessoryIndex = Math.Max(0, model.EquippedAccessoryFor(equipmentMember));
            equipmentRevealSelection = true;
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
