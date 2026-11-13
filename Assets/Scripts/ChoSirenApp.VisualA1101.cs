using System;
using System.Collections.Generic;
using System.Linq;
using ChoSiren.Panels;
using ChoSiren.Systems.Economy;
using ChoSiren.UI;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>
    /// 2026-11-01 (visual-a, 赵金 1101c): 「我是视觉视听享受游戏，所有页面都要做到用最少文字」— the producer
    /// profile, achievements, mail, daily sign-in, settings, the resource (shop / exchange) cards and every
    /// generic confirm card rebuilt as pictures: neon icons, torn-paper medallions, rings, faces, big numbers,
    /// 2-word buttons and sounds; the sentences they used to print live behind a 「?」. Object names used by tests
    /// and other code are kept (ProfileModal, InboxModal, SignInModal, SignInClaim, SignInDay-N/Icon,
    /// MailClaim-*, SettingsModal, Setting-*, Toggle, Done, Reset, ExportSave, ImportSave, InfoModal, Primary,
    /// Close, CurrencyModal, CurrencyBalance, CurrencyExchange, CurrencyConfirmation, ConfirmExchange,
    /// CancelExchange, CloseProgression …) and every action calls the same model methods as before.
    /// </summary>
    public sealed partial class ChoSirenApp
    {
        private static float BodyWidth1101(ModalPolish038Frame frame)
            => frame.Width * (ModalPolish038Chrome.BodyRight - ModalPolish038Chrome.BodyLeft);

        /// <summary>The first clause of a model message ("星钻不足，可先领取…" → "星钻不足").</summary>
        private static string Short1101(string message)
        {
            if (string.IsNullOrEmpty(message)) return string.Empty;
            int cut = message.IndexOfAny(new[] { '，', '。', '；', ',' });
            return cut > 0 ? message.Substring(0, cut) : message;
        }

        private GameObject IconButton1101(Transform parent, string name, string icon, string label, int fontSize,
            UnityAction action, ModalPolish038ButtonStyle style, float x, float y, float width, float height,
            float iconSize = 34f, Color? iconColor = null)
        {
            GameObject button = ModalPolish038Chrome.ActionButton(parent, name, label ?? string.Empty, fontSize, action, style);
            PanelKit.PlaceTop(button.GetComponent<RectTransform>(), x, y, width, height);
            VisualA1101.DecorateAction(button, icon, iconSize, iconColor);
            return button;
        }

        private static RectTransform CurrencyChip1101(Transform parent, string name, string currency, string value,
            Color valueColor, float iconSize, int fontSize)
        {
            RectTransform root = VisualA1101.Rect(name, parent);
            Image icon = VisualA1101.NewImage("Icon", root, RewardItemVisuals.HudBadge(currency) ?? RewardItemVisuals.SpriteFor(currency), Color.white);
            icon.preserveAspect = true;
            VisualA1101.Place(icon.rectTransform, 0f, 0f, iconSize, iconSize);
            Text number = VisualA1101.Number(root, "Value", value, fontSize, valueColor, TextAnchor.MiddleLeft);
            VisualA1101.Place(number.rectTransform, iconSize + 4f, 0f, number.preferredWidth + 6f, iconSize);
            root.sizeDelta = new Vector2(iconSize + 10f + number.preferredWidth, iconSize);
            return root;
        }

        // ================================================================== profile

        private void OpenProfile1101()
        {
            CloseModal();
            ModalPolish038Frame frame = ModalPolish038Chrome.Begin(safeRoot, "ProfileModal", "档案", null,
                CloseModal, 620f, closeName: "Close");
            modalObject = frame.Overlay;
            RectTransform body = frame.Body;
            float width = BodyWidth1101(frame);

            // Face + name + level + the one number that matters.
            const float avatarSize = 132f;
            RectTransform avatarRoot = VisualA1101.Rect("ProfileAvatarFrame", body);
            VisualA1101.Place(avatarRoot, 0f, 0f, avatarSize, avatarSize);
            Image halo = VisualA1101.NewImage("AvatarGlow", avatarRoot, VisualA1101.Glow, VisualA1101.Teal * new Color(1f, 1f, 1f, .45f));
            VisualA1101.Center(halo.rectTransform, avatarSize * 1.35f);
            Image ring = VisualA1101.NewImage("AvatarRing", avatarRoot, VisualA1101.RingSprite, VisualA1101.Teal);
            VisualA1101.Center(ring.rectTransform, avatarSize);
            GameObject mask = NewImage("AvatarMask", avatarRoot, DotSprite(), White);
            VisualA1101.Center(mask.GetComponent<RectTransform>(), avatarSize - 18f);
            mask.AddComponent<Mask>().showMaskGraphic = false;
            Sprite avatar = PlayerAvatarSpriteNow();
            if (avatar != null)
            {
                Image face = NewImage("Avatar", mask.transform, avatar, White).GetComponent<Image>();
                Stretch(face.rectTransform);
                face.preserveAspect = true;
                face.raycastTarget = false;
            }
            Button avatarButton = avatarRoot.gameObject.AddComponent<Button>();
            avatarButton.transition = Selectable.Transition.None;
            avatarButton.targetGraphic = ring;
            ring.raycastTarget = true;
            // 2026-11-01 (赵金): 点头像就是换头像，改名留在旁边的铅笔上。
            avatarButton.onClick.AddListener(() => { VisualA1101.Click(); OpenAvatarPicker1101(); });
            avatarRoot.gameObject.AddComponent<VisualA1101Motion>().Press();

            float infoX = avatarSize + 20f;
            Text name = VisualA1101.Label(body, "ProfileName", model.PlayerName, 32, VisualA1101.Paper, TextAnchor.MiddleLeft);
            name.horizontalOverflow = HorizontalWrapMode.Wrap;
            VisualA1101.Place(name.rectTransform, infoX, 4f, width - infoX - 58f, 46f);
            ModalPolish038Chrome.EnableBestFit(name, 18);
            // 改名: a pencil sticker beside the name (no plate, no word).
            RectTransform rename = VisualA1101.Rect("ProfileRename", body);
            VisualA1101.Place(rename, width - 52f, 2f, 52f, 52f);
            Image renameHit = rename.gameObject.AddComponent<Image>();
            renameHit.sprite = VisualA1101.Glow;
            renameHit.color = VisualA1101.Teal * new Color(1f, 1f, 1f, .22f);
            RectTransform pencil = VisualA1101.NeonIcon(rename, "Pencil", "pencil", VisualA1101.Teal, 38f);
            VisualA1101.Center(pencil, 38f);
            Button renameButton = rename.gameObject.AddComponent<Button>();
            renameButton.transition = Selectable.Transition.None;
            renameButton.targetGraphic = renameHit;
            renameButton.onClick.AddListener(() => { VisualA1101.Click(); OpenPlayerContract1028(false, null); });
            rename.gameObject.AddComponent<VisualA1101Motion>().Press();
            // 2026-11-04 经纪人等级 (separate from member levels) with this level's experience bar.
            Text level = VisualA1101.Number(body, "ProfileLevel", $"LV.{model.AccountLevel1104}", 20, VisualA1101.Teal, TextAnchor.MiddleLeft);
            VisualA1101.Place(level.rectTransform, infoX, 54f, 80f, 28f);
            int xpNeed = GameModel.XpToNext1104(model.AccountLevel1104);
            float barX = infoX + 84f, barW = width - infoX - 84f - 58f;
            Image xpTrack = VisualA1101.NewImage("ProfileXpTrack", body, RoundedSprite(6), new Color(1f, 1f, 1f, .14f));
            xpTrack.type = Image.Type.Sliced;
            VisualA1101.Place(xpTrack.rectTransform, barX, 58f, barW, 8f);
            Image xpFill = VisualA1101.NewImage("ProfileXpFill", body, RoundedSprite(6), VisualA1101.Teal);
            xpFill.type = Image.Type.Sliced;
            float xpShare = model.AccountLevelMaxed1104 ? 1f : Mathf.Clamp01(model.AccountXp1104 / (float)xpNeed);
            VisualA1101.Place(xpFill.rectTransform, barX, 58f, Mathf.Max(8f, barW * xpShare), 8f);
            xpFill.enabled = xpShare > 0f;
            Text xpText = VisualA1101.Label(body, "ProfileXp", model.AccountLevelMaxed1104 ? "已满级"
                : $"经验 {model.AccountXp1104:N0}/{xpNeed:N0}", 13, VisualA1101.Muted, TextAnchor.MiddleLeft);
            VisualA1101.Place(xpText.rectTransform, barX, 66f, barW, 20f);
            RectTransform bolt = VisualA1101.NeonIcon(body, "ProfilePowerIcon", "bolt", VisualA1101.Gold, 40f);
            VisualA1101.Place(bolt, infoX - 4f, 88f, 40f, 40f);
            Text power = VisualA1101.Number(body, "ProfilePower", model.TeamPower.ToString("N0"), 38, VisualA1101.Gold, TextAnchor.MiddleLeft);
            VisualA1101.Place(power.rectTransform, infoX + 40f, 84f, width - infoX - 40f, 48f);

            // The team as faces: tap to arrange it (was the 调整编队 button and the 当前编队 4/4 line).
            const float stripTop = 154f, stripHeight = 96f;
            GameObject strip = ModalPolish038Chrome.RowButton(body, "ProfileTeam", () => { CloseModal(); ShowScreen("team"); },
                false, out RectTransform stripBody);
            PanelKit.PlaceTop(strip.GetComponent<RectTransform>(), 0f, stripTop, width, stripHeight);
            const float faceSize = 74f;
            for (int slot = 0; slot < 4; slot++)
            {
                float x = 20f + slot * (faceSize + 18f);
                RectTransform cell = VisualA1101.Rect("TeamFace-" + slot, stripBody);
                VisualA1101.Place(cell, x, (stripHeight - faceSize) * .5f, faceSize, faceSize);
                bool filled = slot < model.Save.Team.Count;
                Image faceRing = VisualA1101.NewImage("Ring", cell, VisualA1101.RingSprite,
                    filled ? (slot == 0 ? VisualA1101.Gold : VisualA1101.Teal) : new Color(1f, 1f, 1f, .2f));
                VisualA1101.Stretch(faceRing.rectTransform);
                if (!filled)
                {
                    RectTransform empty = VisualA1101.NeonIcon(cell, "Empty", "team", new Color(1f, 1f, 1f, .25f), faceSize * .5f, 0f);
                    empty.anchorMin = empty.anchorMax = empty.pivot = Vector2.one * .5f;
                    empty.anchoredPosition = Vector2.zero;
                    continue;
                }
                MemberDefinition member = GameModel.Members[model.Save.Team[slot]];
                GameObject faceMask = NewImage("Mask", cell, DotSprite(), White);
                VisualA1101.Center(faceMask.GetComponent<RectTransform>(), faceSize - 10f);
                faceMask.AddComponent<Mask>().showMaskGraphic = false;
                Sprite art = Resources.Load<Sprite>(member.ThumbnailResourcePath) ?? Resources.Load<Sprite>(member.ResourcePath);
                Image head = NewImage("Face", faceMask.transform, PortraitFace1101.CropSprite(member.Id, art, 1f, PortraitFace1101.Framing.Head), White).GetComponent<Image>();
                Stretch(head.rectTransform);
                head.raycastTarget = false;
                if (slot == 0)
                {
                    RectTransform crown = VisualA1101.NeonIcon(cell, "Captain", "crown", VisualA1101.Gold, 28f);
                    VisualA1101.Place(crown, faceSize - 26f, -8f, 28f, 28f);
                }
                cell.gameObject.AddComponent<VisualA1101Motion>().Pop(.05f + slot * .05f);
            }
            RectTransform go = VisualA1101.NeonIcon(stripBody, "TeamGo", "arrow", VisualA1101.Teal, 36f);
            VisualA1101.Place(go, width - 56f, (stripHeight - 36f) * .5f, 36f, 36f);

            // Records as icon + number tiles (2-character captions only).
            int cleared = model.Save.ClearedStages.Count(clear => clear.Id.StartsWith("stage-", StringComparison.Ordinal));
            int stars = model.Save.ClearedStages.Where(clear => clear.Id.StartsWith("stage-", StringComparison.Ordinal)).Sum(clear => clear.Stars);
            (string icon, Color color, string value, string caption)[] stats =
            {
                ("cards", VisualA1101.Violet, $"{model.Save.UnlockedMembers.Count}/{GameModel.Members.Length}", "成员"),
                ("flag", VisualA1101.Teal, $"{cleared}/20", "关卡"),
                ("star", VisualA1101.Gold, $"{stars}/60", "星数"),
                ("calendar", VisualA1101.Pink, model.SignedDays.ToString(), "连签"),
            };
            const float tileTop = 268f, tileHeight = 118f;
            float tileWidth = (width - 3 * 10f) / 4f;
            for (int index = 0; index < stats.Length; index++)
            {
                RectTransform tile = VisualA1101.StatTile(body, "ProfileStat-" + index, stats[index].icon, stats[index].color,
                    stats[index].value, stats[index].caption, tileWidth, tileHeight);
                VisualA1101.Place(tile, index * (tileWidth + 10f), tileTop, tileWidth, tileHeight);
                tile.gameObject.AddComponent<VisualA1101Motion>().Pop(.12f + index * .05f);
            }

            // The next 签约券 level and what it pays.
            int milestone = model.NextMilestoneLevel1104;
            if (milestone > 0)
            {
                string pays = string.Join(" · ", GameModel.LevelReward1104(milestone)
                    .Where(r => r.ItemId != Systems.Economy.CurrencyIds.Gold && r.ItemId != Systems.Economy.CurrencyIds.Stamina)
                    .Select(r => $"{GameModel.CurrencyName(r.ItemId)}×{r.Amount}"));
                Text next = VisualA1101.Label(body, "ProfileNextReward", $"LV.{milestone} 奖励：{pays}，每升一级送星光币和体力",
                    14, VisualA1101.Muted, TextAnchor.MiddleCenter);
                VisualA1101.Place(next.rectTransform, 0f, 392f, width, 24f);
            }
            const float actionTop = 424f;
            float half = (width - 12f) * .5f;
            IconButton1101(body, "ProfileMembers", "cards", "图鉴", 20,
                () => { CloseModal(); ShowScreen("members"); }, ModalPolish038ButtonStyle.Secondary, 0f, actionTop, half, 60f, 36f);
            int claimable = model.ClaimableAchievementCount;
            GameObject achievements = IconButton1101(body, "ProfileAchievements", "trophy", "成就", 20, OpenAchievements,
                claimable > 0 ? ModalPolish038ButtonStyle.Primary : ModalPolish038ButtonStyle.Secondary,
                half + 12f, actionTop, half, 60f, 36f);
            if (claimable > 0)
            {
                GameObject dot = UI.NoticeSticker1031.Create("AchievementDot", achievements.transform);
                PlaceTop(dot.GetComponent<RectTransform>(), half - 26f, -6f, 18f, 18f);
                achievements.AddComponent<VisualA1101Motion>().Pulse();
            }
            ModalPolish038Chrome.FitHeight(frame, actionTop + 64f);
        }

        // ================================================================== achievements

        private static (string icon, string tag) AchievementArt1101(string id)
        {
            switch (id)
            {
                case "first-live": return ("mic", "1-1");
                case "chapter-one": return ("crown", "1-10");
                case "chapter-two": return ("crown", "2-10");
                case "chapter-one-full": return ("crown", "1-20");
                case "chapter-two-full": return ("crown", "2-20");
                case "stages-10": return ("flag", null);
                case "three-star-10": return ("star", null);
                case "members-8": return ("team", null);
                case "members-16": return ("cards", null);
                case "level-10": return ("dumbbell", null);
                case "accessories-10": return ("gift", null);
                case "stories-10": return ("book", null);
                default: return ("trophy", null);
            }
        }

        private void OpenAchievements1101()
        {
            CloseModal();
            ModalPolish038Frame frame = ModalPolish038Chrome.Begin(safeRoot, "AchievementModal", "成就", null,
                CloseModal, 640f, closeName: "Close");
            modalObject = frame.Overlay;
            RectTransform body = frame.Body;
            float width = BodyWidth1101(frame);
            List<AchievementView> all = model.Achievements();
            var ordered = all.Select((a, i) => (a, i))
                .OrderBy(p => p.a.Claimable ? 0 : p.a.Claimed ? 2 : 1).ThenBy(p => p.i).Select(p => p.a).ToList();

            // Header: how far along, as a ring around the trophy; the details sit behind 「?」.
            int done = all.Count(a => a.Claimed);
            Image summary = VisualA1101.Ring(frame.Panel, "AchievementSummaryRing", 64f, all.Count == 0 ? 0f : done / (float)all.Count, VisualA1101.Gold);
            RectTransform summaryRect = (RectTransform)summary.transform.parent;
            summaryRect.anchorMin = summaryRect.anchorMax = summaryRect.pivot = new Vector2(1f, 1f);
            summaryRect.anchoredPosition = new Vector2(-frame.Width * (1f - ModalPolish038Chrome.BodyRight) - 132f, -frame.Height * .075f);
            RectTransform summaryIcon = VisualA1101.NeonIcon(summaryRect, "Trophy", "trophy", VisualA1101.Gold, 34f);
            VisualA1101.Center(summaryIcon, 34f);
            Text summaryValue = VisualA1101.Number(summaryRect, "AchievementSummary", $"{done}/{all.Count}", 18, VisualA1101.Paper, TextAnchor.MiddleRight);
            summaryValue.rectTransform.anchorMin = summaryValue.rectTransform.anchorMax = new Vector2(0f, .5f);
            summaryValue.rectTransform.pivot = new Vector2(1f, .5f);
            summaryValue.rectTransform.anchoredPosition = new Vector2(-8f, 0f);
            summaryValue.rectTransform.sizeDelta = new Vector2(70f, 30f);
            GameObject help = VisualA1101.HelpButton(frame.Panel, "AchievementHelp", frame.Overlay.transform, 44f,
                all.Select(a => (AchievementArt1101(a.Id).icon, $"{a.Title}：{a.Detail}")).Take(10).ToArray());
            RectTransform helpRect = help.GetComponent<RectTransform>();
            helpRect.anchorMin = helpRect.anchorMax = helpRect.pivot = new Vector2(1f, 1f);
            helpRect.anchoredPosition = new Vector2(-frame.Width * (1f - ModalPolish038Chrome.BodyRight) - 64f, -frame.Height * .045f - 2f);

            PanelKit ck = cleanKit ??= new PanelKit("AppClean1025");
            RectTransform list = CleanUi1025.ScrollBody(ck, body, "Achievements");
            const float gap = 12f, cardHeight = 150f;
            float cardWidth = (width - gap) * .5f;
            for (int index = 0; index < ordered.Count; index++)
            {
                AchievementView item = ordered[index];
                float x = index % 2 * (cardWidth + gap), y = index / 2 * (cardHeight + gap);
                RectTransform card = VisualA1101.Rect("Achievement-" + item.Id, list);
                VisualA1101.Place(card, x, y, cardWidth, cardHeight);
                Image plate = card.gameObject.AddComponent<Image>();
                SirenStickerUi.SkinPanel(card.gameObject, item.Claimable);
                plate.raycastTarget = false;
                card.gameObject.AddComponent<CanvasGroup>().alpha = item.Claimed ? .55f : 1f;

                (string icon, string tag) = AchievementArt1101(item.Id);
                Color accent = item.Claimable ? VisualA1101.Teal : item.Claimed ? VisualA1101.Muted : VisualA1101.Lavender;
                int target = Mathf.Max(1, item.Target), progress = Mathf.Clamp(item.Progress, 0, target);
                Image ring = VisualA1101.Ring(card, "Progress", 100f, progress / (float)target, item.Claimable ? VisualA1101.Teal : VisualA1101.Violet);
                VisualA1101.Place((RectTransform)ring.transform.parent, 12f, (cardHeight - 100f) * .5f, 100f, 100f);
                RectTransform medal = VisualA1101.Medallion(card, "Medal", icon, accent, 82f);
                VisualA1101.Place(medal, 21f, (cardHeight - 82f) * .5f, 82f, 82f);
                if (item.Claimed)
                {
                    RectTransform stamp = VisualA1101.NeonIcon(card, "Done", "check", VisualA1101.Teal, 40f);
                    VisualA1101.Place(stamp, 74f, cardHeight - 58f, 40f, 40f);
                }

                float textX = 124f, textWidth = cardWidth - textX - 10f;
                Text title = VisualA1101.Label(card, "Title", item.Title, 22, VisualA1101.Paper, TextAnchor.MiddleLeft);
                title.horizontalOverflow = HorizontalWrapMode.Wrap;
                VisualA1101.Place(title.rectTransform, textX, 14f, textWidth, 32f);
                ModalPolish038Chrome.EnableBestFit(title, 15);
                RectTransform reward = CurrencyChip1101(card, "Reward", CurrencyIds.Diamond, $"×{item.Diamonds}", VisualA1101.Paper, 30f, 18);
                if (item.Claimable)
                {
                    // Done: the count gives way to the prize, and one button takes it.
                    VisualA1101.Place(reward, textX, 50f, reward.sizeDelta.x, 30f);
                    string id = item.Id;
                    GameObject claim = IconButton1101(card, "Claim", "gift", "领取", 18, () =>
                    {
                        if (model.ClaimAchievement(id, out string message)) { gameAudio?.PlaySuccess(); Toast(message); }
                        else VisualA1101.Deny();
                        OpenAchievements();
                    }, ModalPolish038ButtonStyle.Primary, textX - 4f, 88f, textWidth + 2f, 48f, 28f);
                    claim.AddComponent<VisualA1101Motion>().Pulse();
                }
                else
                {
                    Text value = VisualA1101.Number(card, "Count", tag ?? $"{progress}/{target}", 20, VisualA1101.Muted, TextAnchor.MiddleLeft);
                    VisualA1101.Place(value.rectTransform, textX, 50f, textWidth, 28f);
                    VisualA1101.Place(reward, textX, 92f, reward.sizeDelta.x, 30f);
                }
                card.gameObject.AddComponent<VisualA1101Motion>().Pop(Mathf.Min(.4f, index * .04f));
            }
            float contentHeight = (ordered.Count + 1) / 2 * (cardHeight + gap) - gap;
            list.sizeDelta = new Vector2(0f, contentHeight);
            ModalPolish038Chrome.FitHeight(frame, contentHeight);
        }

        // ================================================================== mail

        private void OpenInbox1101()
        {
            CloseModal();
            ModalPolish038Frame frame = ModalPolish038Chrome.Begin(safeRoot, "InboxModal", "邮箱", null,
                CloseModal, 600f, closeName: "Close");
            modalObject = frame.Overlay;
            RectTransform body = frame.Body;
            float width = BodyWidth1101(frame);
            PanelKit ck = cleanKit ??= new PanelKit("AppClean1025");
            RectTransform list = CleanUi1025.ScrollBody(ck, body, "Mails");
            var mails = model.Mails().Select((m, i) => (m, i))
                .OrderBy(p => p.m.Claimable ? 0 : 1).ThenBy(p => p.i).Select(p => p.m).ToList();
            if (mails.Count == 0)
            {
                EmptyState1101(list, "mail", width);
                ModalPolish038Chrome.FitHeight(frame, 260f);
                return;
            }
            const float rowHeight = 112f, gap = 12f;
            float y = 0f;
            for (int index = 0; index < mails.Count; index++)
            {
                MailView mail = mails[index];
                string id = mail.Id;
                GameObject row = ModalPolish038Chrome.RowButton(list, "Mail-" + id, () => OpenMail1101(id), false, out RectTransform rowBody);
                PanelKit.PlaceTop(row.GetComponent<RectTransform>(), 0f, y, width, rowHeight);
                bool fresh = mail.Claimable;
                row.AddComponent<CanvasGroup>().alpha = mail.HasGift && mail.Claimed ? .6f : 1f;
                RectTransform medal = VisualA1101.Medallion(rowBody, "Envelope", fresh ? "mail" : "mailopen",
                    fresh ? VisualA1101.Teal : VisualA1101.Violet, 80f);
                VisualA1101.Place(medal, 14f, (rowHeight - 80f) * .5f, 80f, 80f);
                if (fresh)
                {
                    GameObject dot = UI.NoticeSticker1031.Create("MailDot", medal);
                    PlaceTop(dot.GetComponent<RectTransform>(), 60f, -2f, 18f, 18f);
                }
                float textX = 108f, right = mail.HasGift ? 138f : 54f;
                Text title = VisualA1101.Label(rowBody, "MailTitle-" + id, mail.Title, 22, VisualA1101.Paper, TextAnchor.MiddleLeft);
                title.horizontalOverflow = HorizontalWrapMode.Wrap;
                VisualA1101.Place(title.rectTransform, textX, 14f, width - textX - right, 34f);
                ModalPolish038Chrome.EnableBestFit(title, 15);
                if (mail.HasGift)
                {
                    float gx = textX;
                    foreach (var gift in mail.Gift.Take(3))
                    {
                        RectTransform chip = CurrencyChip1101(rowBody, "Gift-" + gift.ItemId, gift.ItemId, "×" + gift.Amount.ToString("N0"),
                            VisualA1101.Gold, 34f, 19);
                        VisualA1101.Place(chip, gx, 60f, chip.sizeDelta.x, 34f);
                        gx += chip.sizeDelta.x + 14f;
                    }
                    if (mail.Claimable)
                    {
                        GameObject claim = IconButton1101(rowBody, "MailClaim-" + id, "gift", "领取", 18, () =>
                        {
                            if (model.ClaimMail(id, out string message)) { gameAudio?.PlaySuccess(); Toast(message); }
                            else VisualA1101.Deny();
                            OpenInbox();
                        }, ModalPolish038ButtonStyle.Primary, width - 124f, (rowHeight - 52f) * .5f, 110f, 52f, 24f);
                        claim.AddComponent<VisualA1101Motion>().Pulse();
                    }
                    else
                    {
                        RectTransform check = VisualA1101.NeonIcon(rowBody, "MailClaimed", "check", VisualA1101.Teal, 40f);
                        VisualA1101.Place(check, width - 80f, (rowHeight - 40f) * .5f, 40f, 40f);
                    }
                }
                else
                {
                    Text hint = VisualA1101.Label(rowBody, "MailPeek", "…", 26, VisualA1101.Muted, TextAnchor.MiddleLeft);
                    VisualA1101.Place(hint.rectTransform, textX, 54f, 60f, 34f);
                    RectTransform go = VisualA1101.NeonIcon(rowBody, "MailOpen", "arrow", VisualA1101.Teal, 32f);
                    VisualA1101.Place(go, width - 50f, (rowHeight - 32f) * .5f, 32f, 32f);
                }
                row.AddComponent<VisualA1101Motion>().Pop(index * .05f);
                y += rowHeight + gap;
            }
            list.sizeDelta = new Vector2(0f, y - gap);
            ModalPolish038Chrome.FitHeight(frame, y - gap);
        }

        /// <summary>One letter, opened: big envelope, the title, the (short) letter, the gift and one button.</summary>
        private void OpenMail1101(string mailId)
        {
            MailView mail = model.Mails().FirstOrDefault(m => m.Id == mailId);
            if (mail == null) { OpenInbox(); return; }
            CloseModal();
            ModalPolish038Frame frame = ModalPolish038Chrome.Begin(safeRoot, "InboxModal", "邮件", null,
                CloseModal, 560f, closeName: "Close");
            modalObject = frame.Overlay;
            RectTransform body = frame.Body;
            float width = BodyWidth1101(frame);
            RectTransform medal = VisualA1101.Medallion(body, "MailEnvelope", "mailopen", VisualA1101.Teal, 120f, .52f);
            VisualA1101.Place(medal, (width - 120f) * .5f, 0f, 120f, 120f);
            medal.gameObject.AddComponent<VisualA1101Motion>().Pop(0f);
            Text title = VisualA1101.Label(body, "MailTitle-" + mail.Id, mail.Title, 24, VisualA1101.Paper);
            title.horizontalOverflow = HorizontalWrapMode.Wrap;
            VisualA1101.Place(title.rectTransform, 0f, 130f, width, 36f);
            ModalPolish038Chrome.EnableBestFit(title, 16);
            Text letter = ModalPolish038Chrome.Text(body, "MailBody-" + mail.Id, model.Personalize(mail.Body), 18,
                ModalPolish038Chrome.TextMuted, FontStyle.Normal, TextAnchor.UpperCenter);
            letter.lineSpacing = 1.25f;
            VisualA1101.Place(letter.rectTransform, 30f, 176f, width - 60f, 84f);
            ModalPolish038Chrome.EnableBestFit(letter, 13);
            float y = 270f;
            if (mail.HasGift)
            {
                float total = 0f;
                var chips = new List<RectTransform>();
                foreach (var gift in mail.Gift.Take(3))
                {
                    RectTransform chip = CurrencyChip1101(body, "Gift-" + gift.ItemId, gift.ItemId, "×" + gift.Amount.ToString("N0"),
                        VisualA1101.Gold, 44f, 24);
                    chips.Add(chip);
                    total += chip.sizeDelta.x + 22f;
                }
                float gx = (width - total + 22f) * .5f;
                foreach (RectTransform chip in chips)
                {
                    VisualA1101.Place(chip, gx, y, chip.sizeDelta.x, 44f);
                    gx += chip.sizeDelta.x + 22f;
                }
                y += 64f;
            }
            string id = mail.Id;
            float half = (width - 12f) * .5f;
            IconButton1101(body, "MailBack", "arrow", "返回", 18, OpenInbox, ModalPolish038ButtonStyle.Ghost, 0f, y, half, 56f, 24f);
            Transform backIcon = body.Find("MailBack/Body/ActionIcon");
            if (backIcon != null) backIcon.localEulerAngles = new Vector3(0f, 0f, 180f);
            if (mail.Claimable)
            {
                GameObject claim = IconButton1101(body, "MailClaim-" + id, "gift", "领取", 20, () =>
                {
                    if (model.ClaimMail(id, out string message)) { gameAudio?.PlaySuccess(); Toast(message); }
                    else VisualA1101.Deny();
                    OpenInbox();
                }, ModalPolish038ButtonStyle.Primary, half + 12f, y, half, 56f, 26f);
                claim.AddComponent<VisualA1101Motion>().Pulse();
            }
            else
            {
                IconButton1101(body, "MailDone", mail.HasGift ? "check" : "close", mail.HasGift ? "已领" : "关闭", 18, CloseModal,
                    ModalPolish038ButtonStyle.Secondary, half + 12f, y, half, 56f, 24f);
            }
            ModalPolish038Chrome.FitHeight(frame, y + 60f);
        }

        /// <summary>Empty lists show a chibi and an icon instead of a sentence.</summary>
        private void EmptyState1101(Transform parent, string icon, float width)
        {
            Sprite chibi = AiUiSprite("Art/SirenSticker20261013/chibi-xingli");
            if (chibi != null)
            {
                Image picture = NewImage("Chibi-Empty", parent, chibi, White).GetComponent<Image>();
                picture.preserveAspect = true;
                picture.raycastTarget = false;
                PlaceTop(picture.rectTransform, (width - 180f) * .5f, 10f, 180f, 180f);
            }
            RectTransform glyph = VisualA1101.NeonIcon(parent, "EmptyIcon", icon, new Color(1f, 1f, 1f, .5f), 54f);
            VisualA1101.Place(glyph, (width - 54f) * .5f + 90f, 150f, 54f, 54f);
        }

        // ================================================================== daily sign-in

        private void OpenSignIn1101()
        {
            CloseModal();
            ModalPolish038Frame frame = ModalPolish038Chrome.Begin(safeRoot, "SignInModal", "签到", null,
                CloseModal, 600f, closeName: "Close");
            modalObject = frame.Overlay;
            RectTransform body = frame.Body;
            float width = BodyWidth1101(frame);
            bool signedToday = model.SignedToday;
            int today = model.SignInDay;
            int claimedInCycle = signedToday ? today + 1 : today;

            GameObject help = VisualA1101.HelpButton(frame.Panel, "SignInHelp", frame.Overlay.transform, 44f,
                ("calendar", "连续签到七天一轮，第七天奖励最丰厚"),
                ("refresh", "中断后从第 1 天重新开始"));
            RectTransform helpRect = help.GetComponent<RectTransform>();
            helpRect.anchorMin = helpRect.anchorMax = helpRect.pivot = new Vector2(1f, 1f);
            helpRect.anchoredPosition = new Vector2(-frame.Width * (1f - ModalPolish038Chrome.BodyRight) - 64f, -frame.Height * .045f - 2f);

            const float gap = 10f, cellHeight = 140f;
            float cell = (width - 3 * gap) / 4f;
            for (int day = 0; day < GameModel.SignInCycle; day++)
            {
                (string itemId, int amount) = GameModel.SignInReward(day);
                bool claimed = day < claimedInCycle;
                bool isToday = day == today && !signedToday;
                bool big = day == GameModel.SignInCycle - 1;
                float x = day < 4 ? day * (cell + gap) : (day - 4) * (cell + gap);
                float y = day < 4 ? 0f : cellHeight + gap;
                float w = big ? cell * 2f + gap : cell;
                GameObject plate = ModalPolish038Chrome.RowPlate(body, "SignInDay-" + (day + 1));
                PanelKit.PlaceTop(plate.GetComponent<RectTransform>(), x, y, w, cellHeight);
                if (isToday)
                {
                    Image glow = VisualA1101.NewImage("TodayGlow", plate.transform, VisualA1101.Glow, VisualA1101.Teal * new Color(1f, 1f, 1f, .5f));
                    VisualA1101.Center(glow.rectTransform, cellHeight * 1.2f);
                    glow.rectTransform.SetAsFirstSibling();
                    SirenStickerUi.SkinPanel(plate, true);
                    plate.AddComponent<VisualA1101Motion>().Pulse();
                }
                Text number = VisualA1101.Number(plate.transform, "Day", (day + 1).ToString(), big ? 30 : 24,
                    isToday ? VisualA1101.Teal : claimed ? VisualA1101.Muted : VisualA1101.Paper, TextAnchor.UpperLeft);
                VisualA1101.Place(number.rectTransform, 12f, 6f, 50f, 34f);
                if (big)
                {
                    RectTransform crown = VisualA1101.NeonIcon(plate.transform, "Crown", "crown", VisualA1101.Gold, 34f);
                    VisualA1101.Place(crown, w - 46f, 8f, 34f, 34f);
                }
                Sprite icon = RewardItemVisuals.SpriteFor(itemId);
                float iconSize = big ? 74f : 58f;
                if (icon != null)
                {
                    Image iconImage = NewImage("Icon", plate.transform, icon, claimed ? new Color(1f, 1f, 1f, .4f) : Color.white).GetComponent<Image>();
                    PlaceTop(iconImage.rectTransform, (w - iconSize) * .5f, 30f, iconSize, iconSize);
                    iconImage.preserveAspect = true;
                    iconImage.raycastTarget = false;
                }
                Text reward = VisualA1101.Number(plate.transform, "Reward", $"×{amount:N0}", big ? 24 : 20,
                    claimed ? VisualA1101.Muted : isToday ? VisualA1101.Paper : VisualA1101.Gold);
                VisualA1101.Place(reward.rectTransform, 2f, cellHeight - 42f, w - 4f, 32f);
                if (claimed)
                {
                    RectTransform stamp = VisualA1101.Medallion(plate.transform, "Stamp", "check", VisualA1101.Teal, 62f, .58f);
                    stamp.anchorMin = stamp.anchorMax = stamp.pivot = Vector2.one * .5f;
                    stamp.anchoredPosition = new Vector2(0f, 4f);
                    stamp.localEulerAngles = new Vector3(0f, 0f, -12f);
                    bool justSigned = signedToday && day == today;
                    if (justSigned) stamp.gameObject.AddComponent<VisualA1101Motion>().Pop(.12f);
                }
            }

            float buttonTop = cellHeight * 2f + gap + 22f;
            GameObject claim = IconButton1101(body, "SignInClaim", signedToday ? "check" : "calendar",
                signedToday ? "明天见" : "签到", 22, () =>
                {
                    if (model.SignIn(out string message)) { VisualA1101.Stamp(); gameAudio?.PlaySuccess(); Toast(message); }
                    else VisualA1101.Deny();
                    OpenSignIn();
                }, signedToday ? ModalPolish038ButtonStyle.Secondary : ModalPolish038ButtonStyle.Primary,
                (width - 300f) * .5f, buttonTop, 300f, 62f, 30f);
            ModalPolish038Chrome.SetActionEnabled(claim, !signedToday);
            if (!signedToday) claim.AddComponent<VisualA1101Motion>().Pulse();
            ModalPolish038Chrome.FitHeight(frame, buttonTop + 66f);
        }

        // ================================================================== settings

        private void OpenSettings1101()
        {
            CloseModal();
            ModalPolish038Frame frame = ModalPolish038Chrome.Begin(safeRoot, "SettingsModal", "设置", null,
                CloseModal, 560f, closeName: "Close");
            modalObject = frame.Overlay;
            RectTransform body = frame.Body;
            float width = BodyWidth1101(frame);

            SettingsSlider1101(body, "音乐", "music", "music", model.Save.MusicEnabled ? model.Save.MusicVolume : 0, 0f, width, value =>
            {
                model.Save.MusicVolume = value;
                if ((value > 0) != model.Save.MusicEnabled) { model.ToggleMusic(); ApplyMusicRouting(); }
                gameAudio.ApplySettings();
            });
            SettingsSlider1101(body, "音效", "sfx", "mute", model.Save.SfxEnabled ? model.Save.SfxVolume : 0, 76f, width, value =>
            {
                model.Save.SfxVolume = value;
                if ((value > 0) != model.Save.SfxEnabled) model.ToggleSfx();
                gameAudio.ApplySettings();
            }, () => gameAudio.PlaySfx("hit"));

            // 画质: one switch, two pictures (sparkle = 高清, bolt = 流畅).
            bool high = model.Save.QualityLevel == 1;
            GameObject qualityRow = ModalPolish038Chrome.RowPlate(body, "Setting-画质");
            PanelKit.PlaceTop(qualityRow.GetComponent<RectTransform>(), 0f, 152f, width, 64f);
            RectTransform qualityIcon = VisualA1101.NeonIcon(qualityRow.transform, "SettingIcon", "sparkle", VisualA1101.Teal, 38f);
            VisualA1101.Place(qualityIcon, 18f, 13f, 38f, 38f);
            GameObject toggle = ModalPolish038Chrome.ActionButton(qualityRow.transform, "Toggle", string.Empty, 16, () =>
            {
                model.ToggleQuality();
                OpenSettings();
            }, ModalPolish038ButtonStyle.Ghost);
            float switchWidth = width - 90f;
            PanelKit.PlaceTop(toggle.GetComponent<RectTransform>(), 76f, 8f, switchWidth, 48f);
            Transform toggleBody = toggle.transform.Find("Body");
            for (int side = 0; side < 2; side++)
            {
                bool on = side == 0 ? high : !high;
                RectTransform half = VisualA1101.Rect(side == 0 ? "High" : "Smooth", toggleBody);
                VisualA1101.Place(half, side * switchWidth * .5f + 3f, 3f, switchWidth * .5f - 6f, 42f);
                Image face = half.gameObject.AddComponent<Image>();
                face.sprite = on ? SirenStickerUi.PrimarySprite : SirenStickerUi.QuietPanelSprite;
                face.type = Image.Type.Sliced;
                face.color = on ? Color.white : new Color(1f, 1f, 1f, .35f);
                face.raycastTarget = false;
                Color ink = on ? (Color)Brand1025.Ink : VisualA1101.Muted;
                RectTransform glyph = VisualA1101.NeonIcon(half, "Icon", side == 0 ? "sparkle" : "bolt", ink, 26f, 0f);
                VisualA1101.Place(glyph, half.sizeDelta.x * .5f - 44f, 8f, 26f, 26f);
                Text caption = VisualA1101.Label(half, "Caption", side == 0 ? "高清" : "流畅", 18, ink, TextAnchor.MiddleLeft);
                VisualA1101.Place(caption.rectTransform, half.sizeDelta.x * .5f - 10f, 0f, 60f, 42f);
            }

            // Save: three picture tiles.
            const float tilesTop = 240f, tileHeight = 100f;
            float tileWidth = (width - 2 * 12f) / 3f;
            (string name, string icon, string label, UnityAction action, ModalPolish038ButtonStyle style, Color color)[] tiles =
            {
                ("ExportSave", "export", "导出", ExportSave1021, ModalPolish038ButtonStyle.Secondary, VisualA1101.Teal),
                ("ImportSave", "import", "导入", ImportSave1021, ModalPolish038ButtonStyle.Secondary, VisualA1101.Teal),
                ("Reset", "trash", "清除", ConfirmReset1101, ModalPolish038ButtonStyle.Danger, VisualA1101.Danger),
            };
            for (int index = 0; index < tiles.Length; index++)
            {
                GameObject tile = ModalPolish038Chrome.ActionButton(body, tiles[index].name, tiles[index].label, 17,
                    tiles[index].action, tiles[index].style);
                PanelKit.PlaceTop(tile.GetComponent<RectTransform>(), index * (tileWidth + 12f), tilesTop, tileWidth, tileHeight);
                Transform tileBody = tile.transform.Find("Body");
                RectTransform labelRect = tileBody.Find("Label") as RectTransform;
                if (labelRect != null)
                {
                    labelRect.anchorMin = new Vector2(0f, 0f); labelRect.anchorMax = new Vector2(1f, 0f);
                    labelRect.pivot = new Vector2(.5f, 0f);
                    labelRect.anchoredPosition = new Vector2(0f, 10f); labelRect.sizeDelta = new Vector2(-12f, 28f);
                }
                RectTransform glyph = VisualA1101.NeonIcon(tileBody, "ActionIcon", tiles[index].icon, tiles[index].color, 44f);
                glyph.anchorMin = glyph.anchorMax = glyph.pivot = new Vector2(.5f, 1f);
                glyph.anchoredPosition = new Vector2(0f, -12f);
            }

            const float doneTop = tilesTop + tileHeight + 22f;
            IconButton1101(body, "Done", "check", "完成", 22, CloseModal, ModalPolish038ButtonStyle.Primary,
                (width - 240f) * .5f, doneTop, 240f, 58f, 28f);

            // Footer: terms / privacy (legal text, kept) and about.
            (string icon, string label)[] links = { ("doc", "协议"), ("shield", "隐私"), ("info", "关于") };
            float linkWidth = (width - 24f) / 3f;
            for (int index = 0; index < links.Length; index++)
            {
                int captured = index;
                IconButton1101(body, "SettingsLink-" + index, links[index].icon, links[index].label, 16,
                    () => OpenSettingsDocument038(captured), ModalPolish038ButtonStyle.Ghost,
                    index * (linkWidth + 12f), doneTop + 74f, linkWidth, 44f, 22f, VisualA1101.Muted);
            }
            ModalPolish038Chrome.FitHeight(frame, doneTop + 74f + 46f);
        }

        private void SettingsSlider1101(Transform parent, string title, string icon, string mutedIcon, int value, float y,
            float rowWidth, Action<int> changed, Action released = null)
        {
            GameObject row = ModalPolish038Chrome.RowPlate(parent, $"Setting-{title}");
            PanelKit.PlaceTop(row.GetComponent<RectTransform>(), 0f, y, rowWidth, 64f);
            RectTransform glyph = VisualA1101.NeonIcon(row.transform, "SettingIcon", value > 0 ? icon : mutedIcon,
                value > 0 ? VisualA1101.Teal : VisualA1101.Muted, 38f);
            VisualA1101.Place(glyph, 18f, 13f, 38f, 38f);
            Text number = VisualA1101.Number(row.transform, "SettingValue", value.ToString(), 18, VisualA1101.Teal, TextAnchor.MiddleRight);
            VisualA1101.Place(number.rectTransform, rowWidth - 70f, 10f, 52f, 44f);
            var sliderGo = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            sliderGo.transform.SetParent(row.transform, false);
            PanelKit.PlaceTop((RectTransform)sliderGo.transform, 76f, 22f, rowWidth - 160f, 20f);
            Image track = NewImage("Track", sliderGo.transform, RoundedSprite(8), new Color32(40, 28, 70, 255)).GetComponent<Image>();
            Stretch(track.rectTransform);
            RectTransform fillArea = SliderArea038("FillArea", sliderGo.transform);
            Image fill = NewImage("Fill", fillArea, RoundedSprite(8), Brand1025.Teal).GetComponent<Image>();
            Stretch(fill.rectTransform);
            RectTransform handleArea = SliderArea038("HandleArea", sliderGo.transform);
            Image handle = NewImage("Handle", handleArea, DotSprite(), White).GetComponent<Image>();
            handle.rectTransform.sizeDelta = new Vector2(32f, 32f);
            handle.raycastTarget = true;
            Slider slider = sliderGo.GetComponent<Slider>();
            slider.fillRect = fill.rectTransform; slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle; slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0; slider.maxValue = 100; slider.wholeNumbers = true; slider.value = value;
            Image glyphImage = VisualA1101.Glyph(glyph);
            Image offset = glyph.Find("IconOffset")?.GetComponent<Image>();
            slider.onValueChanged.AddListener(v =>
            {
                number.text = ((int)v).ToString();
                Sprite sprite = VisualA1101.Icon(v > 0 ? icon : mutedIcon);
                if (glyphImage != null) { glyphImage.sprite = sprite; glyphImage.color = v > 0 ? VisualA1101.Teal : VisualA1101.Muted; }
                if (offset != null) offset.sprite = sprite;
                changed((int)v);
            });
            if (released != null)
            {
                var trigger = sliderGo.AddComponent<UnityEngine.EventSystems.EventTrigger>();
                var up = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerUp };
                up.callback.AddListener(_ => released());
                trigger.triggers.Add(up);
            }
        }

        private void ConfirmReset1101()
        {
            int cleared = model.Save.ClearedStages.Count(clear => clear.Id.StartsWith("stage-", StringComparison.Ordinal));
            OpenConfirm1101(new Confirm1101
            {
                Title = "清除存档",
                Icon = "trash",
                Accent = VisualA1101.Danger,
                Note = "无法撤销",
                Chips = new[]
                {
                    ("flag", cleared.ToString(), VisualA1101.Paper),
                    ("cards", model.Save.UnlockedMembers.Count.ToString(), VisualA1101.Paper),
                    (CurrencyIds.Diamond, model.Save.Diamonds.ToString("N0"), VisualA1101.Paper),
                },
                ChipsStruck = true,
                PrimaryLabel = "清除",
                PrimaryIcon = "trash",
                PrimaryStyle = ModalPolish038ButtonStyle.Danger,
                Primary = () =>
                {
                    model.Reset();
                    ApplyMusicRouting();
                    ShowScreen("lobby");
                    Toast("已重新开始");
                },
                Cancel = OpenSettings,
                Help = new[]
                {
                    ("trash", "永久删除本机的培养、资源和通关记录"),
                    ("refresh", "成员回到 1 级，关卡从 1-1 重新开始"),
                    ("close", "关闭此窗口即可取消"),
                },
            });
        }

        // ================================================================== confirm card

        /// <summary>
        /// The shared confirm / notice card: a torn medallion with one icon, a 2–6 character title, a row of
        /// icon + number chips (optionally "from → to"), a short note, and ✕取消 / ✓确认.
        /// </summary>
        private sealed class Confirm1101
        {
            public string Name = "InfoModal";
            public string CloseName = "Close";
            public string Title;
            public string Icon = "info";
            public Color Accent = VisualA1101.Teal;
            /// <summary>icon = a VisualA1101 icon name, or a currency id (diamond / gold / stamina) for the HUD badge.</summary>
            public (string icon, string value, Color color)[] Chips;
            /// <summary>Chips[0] → Chips[1] with an arrow.</summary>
            public bool ChipsArrow;
            /// <summary>What is about to be lost (清除存档): chips drawn crossed out.</summary>
            public bool ChipsStruck;
            public string Note;
            public Color? NoteColor;
            public string PrimaryName = "Primary";
            public string PrimaryLabel = "确认";
            public string PrimaryIcon = "check";
            public ModalPolish038ButtonStyle PrimaryStyle = ModalPolish038ButtonStyle.Primary;
            public bool PrimaryEnabled = true;
            /// <summary>Null → a single centred button that only closes.</summary>
            public UnityAction Primary;
            /// <summary>Run instead of CloseModal for the primary when set (e.g. exchange keeps its own flow).</summary>
            public bool PrimaryKeepsModal;
            public string CancelName = "Cancel";
            public string CancelLabel = "取消";
            public UnityAction Cancel;
            public (string icon, string line)[] Help;
            public float Width = 520f;
            /// <summary>Members shown as faces instead of the medallion: Faces[0] → Faces[1] (null = empty seat).</summary>
            public MemberDefinition[] Faces;
        }

        /// <summary>A round face cropped through the shared face anchors; an empty seat shows a dim team glyph.</summary>
        private void Face1101(Transform parent, string name, MemberDefinition member, Color rim, float x, float y, float size)
        {
            RectTransform cell = VisualA1101.Rect(name, parent);
            VisualA1101.Place(cell, x, y, size, size);
            Image ring = VisualA1101.NewImage("Ring", cell, VisualA1101.RingSprite, rim);
            VisualA1101.Stretch(ring.rectTransform);
            if (member == null)
            {
                RectTransform empty = VisualA1101.NeonIcon(cell, "Empty", "team", new Color(1f, 1f, 1f, .3f), size * .5f, 0f);
                VisualA1101.Center(empty, size * .5f);
                return;
            }
            GameObject mask = NewImage("Mask", cell, DotSprite(), White);
            VisualA1101.Center(mask.GetComponent<RectTransform>(), size - 12f);
            mask.AddComponent<Mask>().showMaskGraphic = false;
            Sprite art = Resources.Load<Sprite>(member.ThumbnailResourcePath) ?? Resources.Load<Sprite>(member.ResourcePath);
            Image head = NewImage("Face", mask.transform, PortraitFace1101.CropSprite(member.Id, art, 1f, PortraitFace1101.Framing.Head), White).GetComponent<Image>();
            Stretch(head.rectTransform);
            head.raycastTarget = false;
        }

        private static bool IsCurrency1101(string id) =>
            id == CurrencyIds.Diamond || id == CurrencyIds.Gold || id == CurrencyIds.Stamina;

        private RectTransform Chip1101(Transform parent, string name, (string icon, string value, Color color) chip, float size, int fontSize)
        {
            if (IsCurrency1101(chip.icon)) return CurrencyChip1101(parent, name, chip.icon, chip.value, chip.color, size, fontSize);
            RectTransform root = VisualA1101.Rect(name, parent);
            RectTransform glyph = VisualA1101.NeonIcon(root, "Icon", chip.icon, VisualA1101.Teal, size * .82f);
            VisualA1101.Place(glyph, size * .09f, size * .09f, size * .82f, size * .82f);
            Text number = VisualA1101.Number(root, "Value", chip.value, fontSize, chip.color, TextAnchor.MiddleLeft);
            VisualA1101.Place(number.rectTransform, size + 4f, 0f, number.preferredWidth + 6f, size);
            root.sizeDelta = new Vector2(size + 10f + number.preferredWidth, size);
            return root;
        }

        private void OpenConfirm1101(Confirm1101 spec)
        {
            CloseModal();
            UnityAction cancel = spec.Cancel ?? CloseModal;
            ModalPolish038Frame frame = ModalPolish038Chrome.Begin(safeRoot, spec.Name, spec.Title, null,
                CloseModal, spec.Width, closeName: spec.CloseName);
            modalObject = frame.Overlay;
            RectTransform body = frame.Body;
            float width = BodyWidth1101(frame);
            if (spec.Help != null && spec.Help.Length > 0)
            {
                GameObject help = VisualA1101.HelpButton(frame.Panel, "ConfirmHelp", frame.Overlay.transform, 44f, spec.Help);
                RectTransform helpRect = help.GetComponent<RectTransform>();
                helpRect.anchorMin = helpRect.anchorMax = helpRect.pivot = new Vector2(1f, 1f);
                helpRect.anchoredPosition = new Vector2(-frame.Width * (1f - ModalPolish038Chrome.BodyRight) - 64f, -frame.Height * .045f - 2f);
            }

            float y = 0f;
            if (spec.Faces != null && spec.Faces.Length == 2)
            {
                const float face = 116f, arrowSize = 44f, faceGap = 18f;
                float faceX = (width - face * 2f - arrowSize - faceGap * 2f) * .5f;
                Face1101(body, "ConfirmFaceFrom", spec.Faces[0], new Color(1f, 1f, 1f, .35f), faceX, 6f, face);
                RectTransform to = VisualA1101.NeonIcon(body, "ConfirmArrow", "arrow", VisualA1101.Teal, arrowSize);
                VisualA1101.Place(to, faceX + face + faceGap, 6f + (face - arrowSize) * .5f, arrowSize, arrowSize);
                float toX = faceX + face + faceGap * 2f + arrowSize;
                Face1101(body, "ConfirmFaceTo", spec.Faces[1], spec.Accent, toX, 6f, face);
                RectTransform crown = VisualA1101.NeonIcon(body, "ConfirmCrown", spec.Icon, spec.Accent, 44f);
                VisualA1101.Place(crown, toX + face - 34f, -6f, 44f, 44f);
                y += 134f;
            }
            else
            {
                RectTransform medal = VisualA1101.Medallion(body, "ConfirmMedal", spec.Icon, spec.Accent, 132f);
                VisualA1101.Place(medal, (width - 132f) * .5f, y, 132f, 132f);
                medal.gameObject.AddComponent<VisualA1101Motion>().Pop(0f);
                y += 140f;
            }

            if (spec.Chips != null && spec.Chips.Length > 0)
            {
                const float size = 44f;
                var chips = new List<RectTransform>();
                float total = 0f, arrow = 40f, gap = 24f;
                for (int i = 0; i < spec.Chips.Length; i++)
                {
                    RectTransform chip = Chip1101(body, "ConfirmChip-" + i, spec.Chips[i], size, 24);
                    chips.Add(chip);
                    total += chip.sizeDelta.x + (i > 0 ? (spec.ChipsArrow ? arrow + 16f : gap) : 0f);
                }
                float x = (width - total) * .5f;
                for (int i = 0; i < chips.Count; i++)
                {
                    if (i > 0)
                    {
                        if (spec.ChipsArrow)
                        {
                            RectTransform to = VisualA1101.NeonIcon(body, "ConfirmArrow", "arrow", VisualA1101.Teal, arrow * .8f);
                            VisualA1101.Place(to, x + 8f, y + (size - arrow * .8f) * .5f, arrow * .8f, arrow * .8f);
                            x += arrow + 16f;
                        }
                        else x += gap;
                    }
                    VisualA1101.Place(chips[i], x, y, chips[i].sizeDelta.x, size);
                    if (spec.ChipsStruck)
                    {
                        Image strike = VisualA1101.NewImage("Strike", chips[i], PunkUi.SlantFill, spec.Accent);
                        strike.rectTransform.anchorMin = new Vector2(0f, .5f); strike.rectTransform.anchorMax = new Vector2(1f, .5f);
                        strike.rectTransform.sizeDelta = new Vector2(8f, 4f);
                        strike.rectTransform.anchoredPosition = Vector2.zero;
                        strike.rectTransform.localEulerAngles = new Vector3(0f, 0f, -6f);
                    }
                    chips[i].gameObject.AddComponent<VisualA1101Motion>().Pop(.08f + i * .06f);
                    x += chips[i].sizeDelta.x;
                }
                y += size + 14f;
            }

            if (!string.IsNullOrEmpty(spec.Note))
            {
                Text note = VisualA1101.Label(body, "ConfirmNote", spec.Note, 19, spec.NoteColor ?? spec.Accent);
                note.horizontalOverflow = HorizontalWrapMode.Wrap;
                VisualA1101.Place(note.rectTransform, 0f, y, width, 30f);
                ModalPolish038Chrome.EnableBestFit(note, 14);
                y += 38f;
            }
            y += 10f;

            UnityAction confirmed = () =>
            {
                if (!spec.PrimaryKeepsModal) CloseModal();
                spec.Primary?.Invoke();
            };
            if (spec.Primary == null)
            {
                IconButton1101(body, spec.PrimaryName, spec.PrimaryIcon, spec.PrimaryLabel, 22, () => cancel(),
                    ModalPolish038ButtonStyle.Primary, (width - 240f) * .5f, y, 240f, 58f, 28f);
            }
            else
            {
                float half = (width - 14f) * .5f;
                IconButton1101(body, spec.CancelName, "close", spec.CancelLabel, 20, cancel, ModalPolish038ButtonStyle.Ghost,
                    0f, y, half, 58f, 24f, VisualA1101.Muted);
                GameObject primary = IconButton1101(body, spec.PrimaryName, spec.PrimaryIcon, spec.PrimaryLabel, 22, confirmed,
                    spec.PrimaryStyle, half + 14f, y, half, 58f, 28f,
                    spec.PrimaryStyle == ModalPolish038ButtonStyle.Danger ? VisualA1101.Danger : (Color?)null);
                if (!spec.PrimaryEnabled) ModalPolish038Chrome.SetActionEnabled(primary, false);
            }
            ModalPolish038Chrome.FitHeight(frame, y + 62f);
        }

        // ================================================================== stage income (闪耀舞台)

        private void OpenStageIncome1101()
        {
            IdleIncomeReport preview = model.PreviewIdleIncome();
            int gold = preview.AmountOf(CurrencyIds.Gold);
            int diamonds = preview.AmountOf(CurrencyIds.Diamond);
            bool canClaim = model.CanClaimIdleIncome && (gold > 0 || diamonds > 0);
            OpenConfirm1101(new Confirm1101
            {
                Title = "闪耀舞台",
                Icon = "mic",
                Accent = VisualA1101.Gold,
                Chips = new[]
                {
                    (CurrencyIds.Gold, gold.ToString("N0"), VisualA1101.Gold),
                    (CurrencyIds.Diamond, diamonds.ToString("N0"), VisualA1101.Teal),
                },
                Note = preview.Capped ? "已满" : canClaim ? null : "累积中",
                NoteColor = preview.Capped ? VisualA1101.Danger : VisualA1101.Muted,
                PrimaryLabel = canClaim ? "领取" : "稍后",
                PrimaryIcon = canClaim ? "gift" : "clock",
                Primary = canClaim
                    ? () =>
                    {
                        if (model.ClaimIdleIncome(out string message)) { gameAudio?.PlaySuccess(); Toast(message); }
                        else { VisualA1101.Deny(); Toast(string.IsNullOrEmpty(message) ? "暂无收益" : message); }
                    }
                    : (UnityAction)null,
                Help = new[]
                {
                    ("mic", "女团不在线时也在闪耀舞台演出，持续累积星光币"),
                    ("clock", "收益有上限，满了就不再增长，记得常来领取"),
                },
            });
        }

        // ================================================================== resources (shop / exchange)

        private static (string icon, string caption)[] CurrencyUses1101(string currency)
        {
            if (currency == CurrencyIds.Diamond) return new[] { ("gacha", "签约"), ("exchange", "兑换"), ("heart", "体力") };
            if (currency == CurrencyIds.Gold) return new[] { ("dumbbell", "训练"), ("gift", "饰品") };
            return new[] { ("flag", "关卡"), ("mic", "演出") };
        }

        private static (string icon, string caption)[] CurrencySources1101(string currency)
        {
            if (currency == CurrencyIds.Stamina) return new[] { ("clock", "恢复"), ("calendar", "签到"), ("exchange", "兑换") };
            if (currency == CurrencyIds.Diamond) return new[] { ("gift", "充值"), ("flag", "首通"), ("star", "章节") };
            return new[] { ("flag", "关卡"), ("check", "任务"), ("calendar", "签到"), ("coin", "舞台") };
        }

        private void OpenCurrency1101(string currency)
        {
            RectTransform body = ProgressionModal("CurrencyModal", GameModel.CurrencyName(currency), null, 600f);
            ModalPolish038Frame frame = new ModalPolish038Frame
            {
                Overlay = modalObject, Body = body, Panel = (RectTransform)body.parent, Width = 600f, Height = 600f / ModalPolish038Chrome.PanelAspect,
            };
            float width = BodyWidth1101(frame);
            Color accent = PanelKit.CurrencyColor(currency);
            bool diamond = currency == CurrencyIds.Diamond;
            bool stamina = currency == CurrencyIds.Stamina;

            GameObject help = VisualA1101.HelpButton(frame.Panel, "CurrencyHelpButton", frame.Overlay.transform, 44f,
                model.CurrencyHelpDescription(currency).Split('\n').Where(l => l.Trim().Length > 0)
                    .Select((line, i) => (i == 0 ? "info" : line.StartsWith("用途") ? "star" : line.StartsWith("获取") ? "gift" : "info", line.Trim()))
                    .Take(5).ToArray());
            RectTransform helpRect = help.GetComponent<RectTransform>();
            helpRect.anchorMin = helpRect.anchorMax = helpRect.pivot = new Vector2(1f, 1f);
            helpRect.anchoredPosition = new Vector2(-frame.Width * (1f - ModalPolish038Chrome.BodyRight) - 64f, -frame.Height * .045f - 2f);

            // Hero: the badge, big, and the balance.
            Image glow = VisualA1101.NewImage("CurrencyGlow", body, VisualA1101.Glow, accent * new Color(1f, 1f, 1f, .4f));
            VisualA1101.Place(glow.rectTransform, (width - 220f) * .5f, -40f, 220f, 220f);
            Image badge = VisualA1101.NewImage("CurrencyIcon", body, RewardItemVisuals.HudBadge(currency) ?? PanelKit.CurrencyIcon(currency), Color.white);
            badge.preserveAspect = true;
            VisualA1101.Place(badge.rectTransform, (width - 120f) * .5f, 0f, 120f, 120f);
            badge.gameObject.AddComponent<VisualA1101Motion>().Pop(0f);
            string balanceText = stamina ? $"{model.Balance(currency):N0}/{model.StaminaCap}" : model.Balance(currency).ToString("N0");
            Text balance = VisualA1101.Number(body, "CurrencyBalance", balanceText, 40, VisualA1101.Paper);
            VisualA1101.Place(balance.rectTransform, 0f, 124f, width, 50f);
            float y = 178f;
            if (stamina)
            {
                long wait = model.SecondsUntilNextStamina;
                RectTransform next = Chip1101(body, "CurrencyCap", ("clock", wait > 0 ? $"{wait / 60:0}:{wait % 60:00}" : "已满", VisualA1101.Muted), 30f, 18);
                VisualA1101.Place(next, (width - next.sizeDelta.x) * .5f, y, next.sizeDelta.x, 30f);
                y += 40f;
            }
            y += 6f;

            // Uses / sources as two strips of picture chips.
            y = CurrencyStrip1101(body, "CurrencyUses", "用途", CurrencyUses1101(currency), accent, width, y);
            y = CurrencyStrip1101(body, "CurrencySources", "获取", CurrencySources1101(currency), VisualA1101.Teal, width, y + 8f);
            y += 16f;

            if (!diamond)
            {
                EconomyExchangeQuote quote = stamina ? model.PreviewStaminaRefill() : model.PreviewGoldExchange();
                GameObject plate = ModalPolish038Chrome.RowPlate(body, "CurrencyExchangeCard");
                PanelKit.PlaceTop(plate.GetComponent<RectTransform>(), 0f, y, width, 80f);
                if (quote.ReceiveAmount > 0)
                {
                    RectTransform pay = CurrencyChip1101(plate.transform, "Pay", CurrencyIds.Diamond, quote.DiamondCost.ToString("N0"), VisualA1101.Teal, 40f, 24);
                    VisualA1101.Place(pay, 18f, 20f, pay.sizeDelta.x, 40f);
                    RectTransform arrow = VisualA1101.NeonIcon(plate.transform, "Arrow", "arrow", VisualA1101.Teal, 30f);
                    VisualA1101.Place(arrow, pay.sizeDelta.x + 30f, 25f, 30f, 30f);
                    RectTransform get = CurrencyChip1101(plate.transform, "Get", currency, quote.ReceiveAmount.ToString("N0"), VisualA1101.Gold, 40f, 24);
                    VisualA1101.Place(get, pay.sizeDelta.x + 74f, 20f, get.sizeDelta.x, 40f);
                }
                else
                {
                    // Nothing to buy (体力已满): a full gauge says it.
                    RectTransform full = VisualA1101.NeonIcon(plate.transform, "Full", "check", VisualA1101.Teal, 40f);
                    VisualA1101.Place(full, 18f, 20f, 40f, 40f);
                    Image gauge = VisualA1101.NewImage("FullGauge", plate.transform, null, VisualA1101.Teal);
                    VisualA1101.Place(gauge.rectTransform, 70f, 36f, width - 240f, 8f);
                }
                GameObject exchange = IconButton1101(plate.transform, "CurrencyExchange", quote.CanPurchase ? "exchange" : "lock",
                    quote.CanPurchase ? "兑换" : Short1101(quote.UnavailableReason), 18,
                    () => OpenCurrencyConfirmation(currency, quote), ModalPolish038ButtonStyle.Primary,
                    width - 148f, 12f, 136f, 56f, 28f);
                ModalPolish038Chrome.SetActionEnabled(exchange, quote.CanPurchase);
                y += 96f;
            }
            else
            {
                // 2026-11-04: 星钻 is the top-up currency; the shop lists the packs (payment not connected yet).
                GameObject locked = ModalPolish038Chrome.RowPlate(body, "NoRealPayment");
                PanelKit.PlaceTop(locked.GetComponent<RectTransform>(), 0f, y, width, 72f);
                RectTransform glyph = VisualA1101.NeonIcon(locked.transform, "Icon", "nopay", VisualA1101.Gold, 40f);
                VisualA1101.Place(glyph, 18f, 16f, 40f, 40f);
                Text tag = VisualA1101.Label(locked.transform, "StateTitle", "充值通道接入中", 18, VisualA1101.Gold, TextAnchor.MiddleLeft);
                VisualA1101.Place(tag.rectTransform, 70f, 0f, width - 240f, 72f);
                IconButton1101(locked.transform, "OpenTopUp", "gift", "星钻商城", 18, OpenTopUp1104,
                    ModalPolish038ButtonStyle.Primary, width - 168f, 8f, 156f, 56f, 26f);
                y += 88f;
            }

            float half = (width - 12f) * .5f;
            IconButton1101(body, "CurrencyTasks", "check", "任务", 18, () => { CloseModal(); OpenDailyTasks(); },
                ModalPolish038ButtonStyle.Secondary, 0f, y, half, 54f, 26f);
            IconButton1101(body, "CurrencyStages", "flag", "关卡", 18, () => { CloseModal(); OpenLevelMap(); },
                ModalPolish038ButtonStyle.Secondary, half + 12f, y, half, 54f, 26f);
            ModalPolish038Chrome.FitHeight(frame, y + 58f);
        }

        private float CurrencyStrip1101(Transform body, string name, string tag, (string icon, string caption)[] items,
            Color accent, float width, float y)
        {
            RectTransform strip = VisualA1101.Rect(name, body);
            VisualA1101.Place(strip, 0f, y, width, 86f);
            Image plate = strip.gameObject.AddComponent<Image>();
            SirenStickerUi.SkinPanel(strip.gameObject, false);
            plate.raycastTarget = false;
            Text label = VisualA1101.Label(strip, "Tag", tag, 16, VisualA1101.Muted);
            VisualA1101.Place(label.rectTransform, 6f, 0f, 58f, 86f);
            float cell = (width - 74f) / Mathf.Max(3, items.Length);
            float start = 66f + (width - 74f - cell * items.Length) * .5f;
            for (int i = 0; i < items.Length; i++)
            {
                RectTransform glyph = VisualA1101.NeonIcon(strip, "Icon-" + i, items[i].icon, accent, 40f);
                VisualA1101.Place(glyph, start + i * cell + (cell - 40f) * .5f, 8f, 40f, 40f);
                Text caption = VisualA1101.Label(strip, "Caption-" + i, items[i].caption, 15, VisualA1101.Paper);
                VisualA1101.Place(caption.rectTransform, start + i * cell, 52f, cell, 26f);
            }
            return y + 86f;
        }

        private void OpenCurrencyConfirmation1101(string currency, EconomyExchangeQuote quote)
        {
            bool submitted = false;
            OpenConfirm1101(new Confirm1101
            {
                Name = "CurrencyConfirmation",
                CloseName = "CloseProgression",
                Title = "兑换",
                Icon = "exchange",
                Accent = VisualA1101.Teal,
                Chips = new[]
                {
                    (CurrencyIds.Diamond, "-" + quote.DiamondCost.ToString("N0"), VisualA1101.Danger),
                    (currency, "+" + quote.ReceiveAmount.ToString("N0"), VisualA1101.Gold),
                },
                ChipsArrow = true,
                Note = quote.CanPurchase ? null : Short1101(quote.UnavailableReason),
                NoteColor = VisualA1101.Danger,
                PrimaryName = "ConfirmExchange",
                PrimaryLabel = "兑换",
                PrimaryIcon = "exchange",
                PrimaryEnabled = quote.CanPurchase,
                PrimaryKeepsModal = true,
                Primary = () =>
                {
                    if (submitted) return;
                    submitted = true;
                    string message;
                    bool ok = currency == CurrencyIds.Stamina
                        ? model.TryRefillStamina(quote.ReceiveAmount, quote.DiamondCost, out message)
                        : model.TryExchangeDiamondsForGold(out message);
                    if (ok) VisualA1101.Success(); else VisualA1101.Deny();
                    OpenCurrency(currency); Toast(message);
                },
                CancelName = "CancelExchange",
                Cancel = () => OpenCurrency(currency),
                Help = new[]
                {
                    ("exchange", quote.ConfirmationText),
                    ("lock", "确认后才会扣除星钻，取消不扣费"),
                },
            });
        }
    }
}
