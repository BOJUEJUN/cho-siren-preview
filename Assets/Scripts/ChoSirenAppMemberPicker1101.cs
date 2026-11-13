using System;
using System.Linq;
using ChoSiren.Panels;
using ChoSiren.Systems.Presentation;
using ChoSiren.UI;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>
    /// 2026-11-01 (赵金 roster-ui): the member picker (编队位置 / 选队长 / 让 X 替换谁 / 饰品管理角色)
    /// rebuilt as a near-full-screen sheet with a scrolling two-column grid of large art cards —
    /// half-body art in the roster's PSD neon card frame, a torn status ribbon, race icon, name,
    /// role chip, level, power and an action bar that says what a tap does. The tap logic is the
    /// original one, moved here unchanged.
    /// </summary>
    public sealed partial class ChoSirenApp
    {
        private const float PickerInset1101 = 30f, PickerHeader1101 = 122f, PickerGap1101 = 16f;
        public const float PickerCardHeight1101 = 420f, PickerCaptainCardHeight1101 = 520f;
        private static Sprite pickerGlow1101;

        private void OpenMemberSelection1101(string purpose, int slot, bool ownedOnly, string query)
        {
            bool equipmentSelection = purpose == "equipment", captain = purpose == "captain", replacement = purpose == "replacement";
            bool homeSelection = purpose == "home";
            if (homeSelection) ownedOnly = true;
            bool teamSelection = purpose == "team";
            string title = homeSelection ? "选择大厅形象" : equipmentSelection ? "选择饰品管理角色" : captain ? "选择队长" : replacement ? $"让{GameModel.Members[slot].Name}替换谁？" : $"位置 {slot + 1}：选择成员";
            string overlayName = homeSelection ? "HomeAppearancePicker1113" : equipmentSelection ? "EquipmentMemberPicker" : captain ? "CaptainMemberPicker" : replacement ? "TeamReplacementPicker" : "TeamMemberPicker";

            CloseModal();
            RectTransform host = (RectTransform)safeRoot;
            float hostW = host.rect.width > 1f ? host.rect.width : 720f;
            float hostH = host.rect.height > 1f ? host.rect.height : 1440f;
            float panelW = Mathf.Min(704f, hostW - 16f);
            float panelH = Mathf.Max(760f, hostH - 48f);
            ModalPolish038Frame frame = ModalPolish038Chrome.Begin(safeRoot, overlayName, title,
                equipmentSelection ? "饰品" : "编队", CloseModal, panelW, closeName: "CloseProgression");
            modalObject = frame.Overlay;
            PickerFrame1101(frame, panelW, panelH);
            RectTransform body = frame.Body;
            float width = panelW - PickerInset1101 * 2f;
            float bodyH = panelH - PickerHeader1101 - PickerInset1101;

            Text help = ModalPolish038Chrome.Text(body, "PickerHelp",
                homeSelection ? "点选已签约角色设为大厅形象；不改变编队。" : equipmentSelection ? "仅切换管理对象，不改变编队或转移装备。未签约角色只能查看。"
                    : captain ? "队长决定全队的「种族指挥」：每次骰型结算，都按队长种族给全队加效果（追击 / 护盾 / 中毒 / 穿甲），队长还独享骰子累计伤害与充能。点角色即可任命。"
                    : replacement ? "选择要替换的出战位置；原成员和装备均保留。"
                    : slot == 0 ? "新成员将成为队长；已出战互换，待命替换。"
                    : "点选待命成员替换；已出战成员交换位置。", 15,
                ModalPolish038Chrome.TextMuted, FontStyle.Normal, TextAnchor.MiddleLeft);
            float helpH = captain ? 66f : 34f;
            PanelKit.PlaceTop(help.rectTransform, 4f, 0f, width - 8f, helpH);
            if (captain) { help.fontSize = 16; help.color = ModalPolish038Chrome.TextWhite; help.lineSpacing = 1.15f; }
            PanelKit.EnableBestFit(help, 11);

            float filterY = helpH + 8f;
            GameObject ownership = ModalPolish038Chrome.ActionButton(body, "PickerOwnership",
                homeSelection ? "已签约角色" : ownedOnly ? "已拥有 ✓ · 切换全部" : "全部 · 只看已拥有", 16,
                () => OpenMemberSelection(purpose, slot, 0, !ownedOnly, query),
                ModalPolish038ButtonStyle.Secondary);
            PanelKit.PlaceTop(ownership.GetComponent<RectTransform>(), 0f, filterY, 236f, 48f);
            ModalPolish038Chrome.SetActionEnabled(ownership, !replacement && !homeSelection);

            GameObject search = ModalPolish038Chrome.RowPlate(body, "PickerSearch");
            PanelKit.PlaceTop(search.GetComponent<RectTransform>(), 248f, filterY, width - 248f, 48f);
            search.GetComponent<Image>().raycastTarget = true;
            Text placeholder = ModalPolish038Chrome.Text(search.transform, "Placeholder",
                "搜索角色名 · 回车", 15, ModalPolish038Chrome.TextMuted, FontStyle.Normal, TextAnchor.MiddleLeft);
            PanelKit.PlaceTop(placeholder.rectTransform, 16f, 6f, width - 280f, 36f);
            Text searchValue = ModalPolish038Chrome.Text(search.transform, "Value", query, 15,
                ModalPolish038Chrome.TextWhite, FontStyle.Normal, TextAnchor.MiddleLeft);
            PanelKit.PlaceTop(searchValue.rectTransform, 16f, 6f, width - 280f, 36f);
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

            float gridTop = filterY + 48f + 14f;
            if (owned.Length == 0)
            {
                GameObject empty = ModalPolish038Chrome.StateBlock(body, "PickerEmpty",
                    ModalPolish038StateKind.Empty, "没有符合条件的角色", "可修改搜索或切换全部");
                PanelKit.PlaceTop(empty.GetComponent<RectTransform>(), 0f, gridTop + 40f, width, 84f);
                return;
            }

            RectTransform content = PickerScroll1101(body, gridTop, width, bodyH - gridTop);
            const int columns = 2;
            float cardW = (width - PickerGap1101 * (columns - 1)) / columns;
            float cardH = captain ? PickerCaptainCardHeight1101 : PickerCardHeight1101;
            for (int i = 0; i < owned.Length; i++)
            {
                int member = owned[i];
                MemberDefinition m = GameModel.Members[member];
                bool current = homeSelection ? model.Save.HomeCharacterIndex == GameModel.HomeIndexForMember1113(member) : teamSelection && slot < model.Save.Team.Count && model.Save.Team[slot] == member;
                UnityEngine.Events.UnityAction pick = () =>
                {
                    if (teamSelection && slot < model.Save.Team.Count && model.Save.Team[slot] == member) return;
                    if (captain && model.IsUnlocked(member))
                    {
                        if (model.Save.Team.Count > 0 && model.Save.Team[0] == member) { Toast($"{m.Name} 已经是队长"); return; }
                        ConfirmCaptain(member);
                        return;
                    }
                    if (!model.IsUnlocked(member))
                    {
                        OpenMember(member);
                        memberProfileReturn = () => OpenMemberSelection(purpose, slot, 0, ownedOnly, query);
                        return;
                    }
                    if (homeSelection)
                    {
                        int index = GameModel.HomeIndexForMember1113(member);
                        model.SetHomeCharacter(index);
                        psdCharacters?.Select(index);
                        CloseModal(); Toast($"大厅形象：{m.Name}"); return;
                    }
                    if (equipmentSelection)
                    { equipmentMember = member; selectedAccessoryIndex = Math.Max(0, model.EquippedAccessoryFor(member)); equipmentRevealSelection = true; ShowScreen("accessory"); return; }
                    int targetSlot = replacement ? model.Save.Team.IndexOf(member) : slot;
                    if (!model.ReplaceTeamSlot(targetSlot, replacement ? slot : member, out string message))
                    { Toast(message); return; }
                    ShowScreen("team"); Toast(message);
                };
                Rect cell = new Rect((i % columns) * (cardW + PickerGap1101), (i / columns) * (cardH + PickerGap1101), cardW, cardH);
                GameObject card = PickerCard1101(content, member, cell, purpose, slot, current, pick);
                if (replacement) card.name = "ReplaceSlot-" + model.Save.Team.IndexOf(member);
            }
            int rows = (owned.Length + columns - 1) / columns;
            content.sizeDelta = new Vector2(width, rows * cardH + (rows - 1) * PickerGap1101 + 24f);
        }

        /// <summary>Grows the shared modal frame to a near-full-screen sheet and re-seats its chrome.</summary>
        private void PickerFrame1101(ModalPolish038Frame frame, float panelW, float panelH)
        {
            frame.Width = panelW;
            frame.Height = panelH;
            frame.Panel.sizeDelta = new Vector2(panelW, panelH);
            Transform shadow = frame.Overlay.transform.Find("PanelShadow");
            if (shadow != null) ((RectTransform)shadow).sizeDelta = new Vector2(panelW, panelH);
            Transform panel = frame.Panel;
            const float left = PickerInset1101 + 4f;
            Transform eyebrow = panel.Find("Eyebrow");
            if (eyebrow != null) PlaceTop((RectTransform)eyebrow, left + 2f, 26f, 300f, 22f);
            Text head = panel.Find("Title")?.GetComponent<Text>();
            if (head != null)
            {
                PlaceTop(head.rectTransform, left, 50f, panelW - left - 120f, 46f);
                SirenStickerUi.SkinTitle(head, true);
            }
            Transform beam = panel.Find("TitleBeam");
            if (beam != null) PlaceTop((RectTransform)beam, left, 102f, 64f, 6f);
            if (frame.CloseButton != null)
                PlaceTop(frame.CloseButton.GetComponent<RectTransform>(), panelW - PickerInset1101 - 56f, 34f, 56f, 46f);
            frame.Body.offsetMin = new Vector2(PickerInset1101, PickerInset1101);
            frame.Body.offsetMax = new Vector2(-PickerInset1101, -PickerHeader1101);
        }

        private RectTransform PickerScroll1101(Transform body, float top, float width, float height)
        {
            GameObject hostObject = NewObject("PickerScroll", body);
            RectTransform hostRect = hostObject.AddComponent<RectTransform>();
            PlaceTop(hostRect, 0f, top, width, height);
            GameObject viewport = NewImage("Viewport", hostObject.transform, null, new Color(0f, 0f, 0f, .01f));
            Stretch(viewport.GetComponent<RectTransform>());
            viewport.GetComponent<Image>().raycastTarget = true;
            viewport.AddComponent<RectMask2D>();
            GameObject contentObject = NewObject("Content", viewport.transform);
            RectTransform content = contentObject.AddComponent<RectTransform>();
            PlaceTop(content, 0f, 0f, width, height);
            ScrollRect scroll = hostObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = .12f;
            scroll.scrollSensitivity = 40f;
            // Ink fade on the lower edge: the next row peeks out instead of being cut by a hard line.
            GameObject fade = NewImage("PickerScrollFade", hostObject.transform, BottomFade(), new Color32(12, 6, 24, 235));
            RectTransform fadeRect = fade.GetComponent<RectTransform>();
            fadeRect.anchorMin = new Vector2(0f, 0f);
            fadeRect.anchorMax = new Vector2(1f, 0f);
            fadeRect.pivot = new Vector2(.5f, 0f);
            fadeRect.offsetMin = Vector2.zero;
            fadeRect.offsetMax = new Vector2(0f, 46f);
            return content;
        }

        private GameObject PickerCard1101(Transform parent, int member, Rect rect, string purpose, int slot,
            bool current, UnityEngine.Events.UnityAction pick)
        {
            MemberDefinition m = GameModel.Members[member];
            bool unlocked = model.IsUnlocked(member);
            bool captain = purpose == "captain", teamSelection = purpose == "team";
            bool equipmentSelection = purpose == "equipment", replacement = purpose == "replacement";
            int teamIndex = model.Save.Team.IndexOf(member);
            string race = MemberRaceFamily(m, member);
            float w = rect.width, h = rect.height;

            GameObject row = ModalPolish038Chrome.RowButton(parent, "PickMember-" + member, pick, false, out RectTransform card);
            PlaceTop(row.GetComponent<RectTransform>(), rect.x, rect.y, w, h);
            if (teamSelection) ModalPolish038Chrome.SetActionEnabled(row, !current);

            // The roster's own PSD neon card frame instead of a plain list plate.
            Sprite frameSprite = ContentFrames038MemberCard(h);
            Image plate = card.Find("Plate").GetComponent<Image>();
            Image sheen = card.Find("Sheen").GetComponent<Image>();
            if (frameSprite != null)
            {
                plate.sprite = frameSprite;
                plate.type = Image.Type.Simple;
                plate.color = White;
                sheen.sprite = frameSprite;
                Transform stripe = plate.transform.Find("Stripe");
                if (stripe != null) stripe.gameObject.SetActive(false);
            }

            GameObject clip = RosterPolish038Clip(card, "PickerPortraitFrame-" + member, 6f, 6f, w - 12f, h - 12f);
            RectTransform clipRect = clip.GetComponent<RectTransform>();
            Color raceColor = TeamRaceColor(race);
            if (unlocked)
            {
                GameObject glow = NewImage("PickerGlow", clip.transform, PickerGlowSprite1101(),
                    new Color(raceColor.r, raceColor.g, raceColor.b, .5f));
                PlaceTop(glow.GetComponent<RectTransform>(), -w * .2f, -20f, w * 1.4f, h * .82f);
            }
            GameObject portrait = NewImage("PickerPortrait-" + member, clip.transform,
                unlocked ? Resources.Load<Sprite>(m.ResourcePath) : LockedSilhouetteSprite(),
                unlocked ? White : LockedSilhouetteTint);
            Image art = portrait.GetComponent<Image>();
            art.preserveAspect = true;
            art.raycastTarget = false;
            if (unlocked) FrameHalfBody1101(art, clipRect, m.Id);
            else
            {
                // The roster's authored glitch silhouette (cover-fit), procedural bust as fallback.
                Sprite unknown = ReferenceArt038.Load("Art/Reference038/unknown-member-038");
                if (unknown != null)
                {
                    art.sprite = unknown;
                    art.color = White;
                    // Opaque art: keep 6 px clear of the clip so the neon frame edge stays visible.
                    float coverW = w - 24f;
                    float coverH = coverW * unknown.rect.height / Mathf.Max(1f, unknown.rect.width);
                    PlaceTop(art.rectTransform, 6f, 6f, coverW, Mathf.Max(h - 24f, coverH));
                }
                else PlaceTop(art.rectTransform, (w - 12f) * .16f, 34f, (w - 12f) * .68f, h * .56f);
            }
            if (current) art.color = new Color(.55f, .55f, .62f, 1f);
            GameObject shade = NewImage("PickerShade", clip.transform, BottomFade(), new Color32(8, 4, 16, 252));
            float shadeH = h * .58f;
            PlaceTop(shade.GetComponent<RectTransform>(), 0f, h - 12f - shadeH, w - 12f, shadeH);

            // Status ribbon (top-left): 当前成员 / 已出战·第 N 位 / 待命 / 未拥有.
            string badge = !unlocked ? "未拥有"
                : current ? purpose == "home" ? "当前形象" : "当前成员"
                : captain && teamIndex == 0 ? "当前队长"
                : teamIndex == 0 ? "队长 · 第 1 位"
                : teamIndex > 0 ? $"已出战 · 第 {teamIndex + 1} 位" : "待命";
            Color badgeFace = !unlocked ? new Color32(70, 64, 86, 240)
                : current || (captain && teamIndex == 0) ? (Color)Brand1025.Teal
                : teamIndex >= 0 ? PunkUi.Hot : new Color32(74, 44, 132, 245);
            Color badgeInk = (current || (captain && teamIndex == 0)) && unlocked ? (Color)Brand1025.Ink : White;
            GameObject ribbon = NewImage("PickerBadgeRibbon", card, RibbonSprite(), badgeFace);
            Text badgeText = ModalPolish038Chrome.Text(card, "PickerBadge-" + member, badge, 16, badgeInk,
                FontStyle.Bold, TextAnchor.MiddleCenter);
            float badgeW = Mathf.Clamp(badgeText.preferredWidth + 30f, 76f, w * .62f);
            PlaceTop(ribbon.GetComponent<RectTransform>(), 4f, 14f, badgeW, 32f);
            ReferencePunkFx.Tilt(ribbon.GetComponent<RectTransform>(), -4f);
            PlaceTop(badgeText.rectTransform, 8f, 17f, badgeW - 8f, 26f);
            PanelKit.EnableBestFit(badgeText, 11);

            if (unlocked)
            {
                Sprite raceIcon = RaceIconSprite(race);
                if (raceIcon != null)
                {
                    GameObject icon = NewImage("PickerRaceIcon", card, raceIcon, White);
                    PlaceTop(icon.GetComponent<RectTransform>(), w - 62f, 12f, 50f, 50f);
                    icon.GetComponent<Image>().preserveAspect = true;
                }
                if (!string.IsNullOrEmpty(m.Rarity))
                {
                    Text rarity = ModalPolish038Chrome.Text(card, "PickerRarity-" + member, m.Rarity, 17,
                        m.Rarity == "SSR" ? new Color32(255, 160, 60, 255) : m.Rarity == "SR" ? (Color)new Color32(214, 150, 255, 255) : Cyan,
                        FontStyle.BoldAndItalic, TextAnchor.MiddleCenter);
                    PlaceTop(rarity.rectTransform, w - 66f, 62f, 58f, 22f);
                    Outlined1101(rarity, 1.2f);
                }
            }

            // Info stack over the shaded lower half.
            float nameY = h - (captain ? 250f : 168f);
            Text name = ModalPolish038Chrome.Text(card, "PickerName-" + member,
                unlocked ? m.Name : MemberRosterVisibility.LockedName, 30, White, FontStyle.Bold, TextAnchor.MiddleLeft);
            PlaceTop(name.rectTransform, 16f, nameY, w - 32f, 40f);
            PanelKit.EnableBestFit(name, 18);
            Outlined1101(name, 1.6f);

            float chipY = nameY + 44f;
            GameObject chip = NewImage("PickerCareerChip", card, PunkUi.SlantFill, unlocked ? (Color)Brand1025.Teal : new Color32(80, 74, 96, 255));
            PlaceTop(chip.GetComponent<RectTransform>(), 16f, chipY, 76f, 28f);
            Text career = ModalPolish038Chrome.Text(chip.transform, "PickerCareer-" + member,
                unlocked ? m.Career : MemberRosterVisibility.LockedCareer, 15,
                unlocked ? (Color)Brand1025.Ink : White, FontStyle.Bold, TextAnchor.MiddleCenter);
            PlaceTop(career.rectTransform, 6f, 1f, 64f, 26f);
            PanelKit.EnableBestFit(career, 10);
            Text role = ModalPolish038Chrome.Text(card, "PickerRole-" + member,
                unlocked ? $"Lv.{model.LevelOf(member)} · {race}" : "签约后解锁", 16,
                ModalPolish038Chrome.TextWhite, FontStyle.Bold, TextAnchor.MiddleLeft);
            PlaceTop(role.rectTransform, 100f, chipY, w - 116f, 28f);
            PanelKit.EnableBestFit(role, 11);
            Outlined1101(role, 1.1f);

            float infoY = chipY + 34f;
            if (unlocked)
            {
                Text power = ModalPolish038Chrome.Text(card, "PickerPower-" + member,
                    $"战力 {model.PowerOf(member):N0}", 17, ModalPolish038Chrome.AccentGold, FontStyle.Bold,
                    captain ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft);
                PlaceTop(power.rectTransform, captain ? w - 146f : 16f, infoY, 130f, 24f);
                PanelKit.EnableBestFit(power, 12);
                Outlined1101(power, 1.1f);
            }
            if (captain && unlocked)
            {
                Text traitTitle = ModalPolish038Chrome.Text(card, "PickerCaptainTrait-" + member,
                    MemberCaptainTraits.Title(m.Race), 15, ModalPolish038Chrome.AccentCyan, FontStyle.Bold, TextAnchor.MiddleLeft);
                PlaceTop(traitTitle.rectTransform, 16f, infoY, w - 172f, 24f);
                PanelKit.EnableBestFit(traitTitle, 11);
                string effect = MemberCaptainTraits.Describe(m.Race);
                int colon = effect.IndexOf('：');
                Text traitBody = ModalPolish038Chrome.Text(card, "PickerCaptainEffect-" + member,
                    colon >= 0 ? effect.Substring(colon + 1) : effect, 15, ModalPolish038Chrome.TextWhite,
                    FontStyle.Normal, TextAnchor.UpperLeft);
                PlaceTop(traitBody.rectTransform, 16f, infoY + 28f, w - 32f, 72f);
                traitBody.lineSpacing = 1.05f;
                PanelKit.EnableBestFit(traitBody, 12);
            }
            else
            {
                string hint = !unlocked ? "未签约，仅可查看档案"
                    : teamSelection ? current ? "已在此位置" : teamIndex >= 0 ? $"与第 {teamIndex + 1} 位互换" : "原成员转为待命"
                    : equipmentSelection ? $"已装备 {EquippedCount1101(member)}/{GameModel.EquipmentSlotCount} 件"
                    : replacement ? $"换下后转为待命"
                    : string.Empty;
                if (hint.Length > 0)
                {
                    Text actionHint = ModalPolish038Chrome.Text(card, "PickerActionHint-" + member, hint, 14,
                        ModalPolish038Chrome.TextMuted, FontStyle.Normal, unlocked ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft);
                    PlaceTop(actionHint.rectTransform, unlocked ? 150f : 16f, infoY, unlocked ? w - 166f : w - 32f, 24f);
                    PanelKit.EnableBestFit(actionHint, 10);
                }
            }

            // Action bar: what a tap on this card does.
            string action = teamSelection ? current ? "当前成员" : !unlocked ? "仅查看" : teamIndex >= 0 ? "交换位置" : "替换上阵"
                : !unlocked ? "仅查看"
                : captain ? teamIndex == 0 ? "当前队长" : "任命队长"
                : purpose == "home" ? current ? "当前形象" : "设为大厅形象"
                : equipmentSelection ? "管理饰品"
                : "替换TA";
            bool idle = action == "当前成员" || action == "当前队长" || action == "仅查看";
            bool swap = action == "交换位置";
            GameObject bar = NewImage("PickerActionBar", card, PunkUi.SlantFill,
                idle ? new Color32(36, 30, 50, 240) : swap ? (Color)Brand1025.Teal : PunkUi.Hot);
            PlaceTop(bar.GetComponent<RectTransform>(), 14f, h - 58f, w - 28f, 42f);
            if (!idle)
            {
                GameObject barShadow = NewImage("PickerActionShadow", card, PunkUi.SlantFill, Color.black);
                PlaceTop(barShadow.GetComponent<RectTransform>(), 18f, h - 54f, w - 28f, 42f);
                barShadow.transform.SetSiblingIndex(bar.transform.GetSiblingIndex());
            }
            Text state = ModalPolish038Chrome.Text(bar.transform, "PickerState-" + member, action, 19,
                idle ? ModalPolish038Chrome.TextMuted : swap ? (Color)Brand1025.Ink : White, FontStyle.Bold, TextAnchor.MiddleCenter);
            PlaceTop(state.rectTransform, 10f, 3f, w - 48f, 36f);
            PanelKit.EnableBestFit(state, 13);

            sheen.transform.SetAsLastSibling();
            return row;
        }

        private int EquippedCount1101(int member)
        {
            int count = 0;
            for (int s = 0; s < GameModel.EquipmentSlotCount; s++) if (model.EquippedInSlot(member, s) >= 0) count++;
            return count;
        }

        private static void Outlined1101(Text text, float size)
        {
            Outline outline = UnityNull.Real(text.GetComponent<Outline>()) ?? text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color32(0, 0, 0, 230);
            outline.effectDistance = new Vector2(size, -size);
        }

        /// <summary>
        /// Half-body crop: the member's measured bust focus widened ~1.75× so shoulders, arms and
        /// waist read in the large card, top-aligned just above the head.
        /// </summary>
        private static void FrameHalfBody1101(Image image, RectTransform frame, string memberId)
        {
            if (image == null || frame == null || image.sprite == null) return;
            Rect src = image.sprite.rect;
            float fw = frame.rect.width, fh = frame.rect.height;
            if (src.width <= 0f || src.height <= 0f || fw <= 0f || fh <= 0f) return;
            Rect focus = PanelKit.MemberBustFocus(memberId);
            float vw = Mathf.Min(focus.width * 1.75f, 1f);
            float vh = vw * src.width * fh / (fw * src.height);
            if (vh > 1f) { vw /= vh; vh = 1f; }
            float top = Mathf.Clamp(focus.yMax + .03f, vh, 1f);
            float cx = Mathf.Clamp(focus.center.x, vw * .5f, 1f - vw * .5f);
            float scale = fw / (vw * src.width);
            RectTransform rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 1f);
            rect.sizeDelta = new Vector2(src.width * scale, src.height * scale);
            rect.anchoredPosition = new Vector2(src.width * scale * (.5f - cx), src.height * scale * (1f - top));
        }

        private static Sprite PickerGlowSprite1101()
        {
            if (pickerGlow1101 != null) return pickerGlow1101;
            const int size = 96;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + .5f) / size * 2f - 1f, dy = (y + .5f) / size * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - d);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(255f * a * a));
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            pickerGlow1101 = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
            pickerGlow1101.hideFlags = HideFlags.DontSave;
            return pickerGlow1101;
        }
    }
}
