using System;
using System.Linq;
using ChoSiren.Panels;
using ChoSiren.Systems.Economy;
using ChoSiren.Systems.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    public sealed partial class ChoSirenApp
    {
        // Authored backgrounds contain static lettering and empty plaques only.
        // Portraits, ownership, values and every action remain live game UI.
        private RectTransform ReferenceBoard038(string name, string resource, float reserveFooter = 0f)
        {
            Sprite art = ReferenceArt038.Load(resource);
            GameObject board = NewImage(name, contentRoot, art, White);
            RectTransform rect = board.GetComponent<RectTransform>();
            Canvas.ForceUpdateCanvases();
            Vector2 size = art != null ? art.rect.size : new Vector2(864, 1824);
            float scale = Mathf.Min(contentRoot.rect.width / size.x, (contentRoot.rect.height + 246f - reserveFooter) / size.y);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = new Vector2(0, -19f + reserveFooter * .5f);
            rect.localScale = Vector3.one * scale;
            board.GetComponent<Image>().preserveAspect = true;
            board.GetComponent<Image>().raycastTarget = false;
            ConfigureReferenceHeader038(true);
            return rect;
        }

        /// <summary>
        /// HeaderPolish038 / SharedHeaderBand038: user-directed unified header. One
        /// members-target band artwork (HeaderBandMembers038) is drawn at the back of
        /// the TopBar on every common-HUD screen and covers whatever header the page
        /// bakes underneath, so lobby/team/native header looks are no longer kept
        /// per page. One geometry below is the members reference measured on the
        /// board art: left profile cell + ring, three empty slanted resource frames,
        /// and the right icon cell that hosts a live mail envelope and the gear.
        /// Only live values, icons, avatar, compact plus tiles, mail and gear
        /// overlay are positioned here; hit rects stay fixed while visuals respond.
        /// </summary>
        private sealed class HeaderPolish038Layout
        {
            public Rect Profile;
            public Rect Avatar;      // local to Profile
            public Rect Name;        // local to Profile
            public Rect Level;       // local to Profile
            public Rect Power;       // local to Profile
            public Rect[] Pills = new Rect[3];
            public Rect[] Icons = new Rect[3];   // local to pill; the band frames are empty so icons are live
            public Rect[] Values = new Rect[3];  // local to pill
            public Rect[] Tiles = new Rect[3];   // bar coordinates; hit rect stays larger than the compact tile
            public int[] ValueMax = new int[3];  // per-currency resizeTextMaxSize: bounds the
                                                 // rendered width so long values ("120/120")
                                                 // can never reach under the icon or plus tile
            public Rect Settings;
            public Rect Gear;        // local to Settings
            public Rect Mail;
            public Rect MailIcon;    // local to Mail
            public bool NativeChrome;
            public bool LiveIcons;

            public static HeaderPolish038Layout For(string screen)
            {
                // One shared members-target geometry on every common-HUD screen.
                // Band frames: (222,0,128,46) (354,0,122,46) (478,0,124,46); the
                // right icon cell spans ~x606-714 and holds mail then settings.
                return new HeaderPolish038Layout
                {
                    Profile = new Rect(0, 0, 215, 62),
                    Avatar = new Rect(29, 26, 52, 52),
                    Name = new Rect(108, 4, 100, 20),
                    Level = new Rect(108, 26, 96, 14),
                    Power = new Rect(108, 42, 100, 16),
                    Pills = new[] { new Rect(222, 0, 128, 46), new Rect(354, 0, 122, 46), new Rect(478, 0, 124, 46) },
                    Icons = new[] { new Rect(8, 9, 24, 24), new Rect(8, 9, 24, 24), new Rect(8, 9, 24, 24) },
                    Values = new[] { new Rect(36, 9, 50, 26), new Rect(36, 9, 46, 26), new Rect(35, 9, 48, 26) },
                    Tiles = new[] { new Rect(312, 5, 36, 36), new Rect(440, 5, 36, 36), new Rect(564, 5, 36, 36) },
                    Settings = new Rect(662, 2, 54, 46),
                    Gear = new Rect(0, 0, 48, 46),
                    Mail = new Rect(610, 2, 52, 44),
                    MailIcon = new Rect(-5, -7, 58, 58),
                    NativeChrome = false,
                    LiveIcons = true,
                    ValueMax = new[] { 15, 14, 12 },
                };
            }
        }

        private void ConfigureReferenceHeader038(bool active)
        {
            Transform bar = safeRoot.Find("TopBar");
            if (bar == null) return;
            HeaderPolish038Layout layout = HeaderPolish038Layout.For(currentScreen);

            // The pasted 100px lobby strip is gone for good; delete any stale node.
            Transform strip = bar.Find("ReferenceHeader038");
            if (strip != null)
            {
                strip.gameObject.SetActive(false);
                Destroy(strip.gameObject);
            }
            foreach (Transform node in bar.GetComponentsInChildren<Transform>(true))
            {
                if (node.name == "ReferenceHudIcon" || node.name == "ReferencePlus")
                    node.gameObject.SetActive(false);
            }

            // Clip header chrome to the bar so band-page art clipped at the screen top
            // (avatar ring, gear tips, tiles) never bleeds above the safe area.
            if (bar.GetComponent<RectMask2D>() == null) bar.gameObject.AddComponent<RectMask2D>();

            // SharedHeaderBand038: the members-target band art is the one shared
            // header on every common-HUD page. Drawn as the bar's rearmost child it
            // covers each page's own baked header (lobby pill strip, team quad,
            // empty clean tops) with the unified profile cell, three empty slanted
            // resource frames and the right icon cell that hosts mail + settings.
            Transform band = bar.Find("HeaderBandMembers038");
            if (band == null)
            {
                Sprite bandSprite = AiUiSprite("Art/LobbyPunk/038/header-band-members-038");
                if (bandSprite != null)
                {
                    band = NewImage("HeaderBandMembers038", bar, bandSprite, White).transform;
                    Image created = band.GetComponent<Image>();
                    created.preserveAspect = false;
                    created.raycastTarget = false;
                }
            }
            if (band != null)
            {
                band.gameObject.SetActive(true);
                PlaceTop((RectTransform)band, 0f, 0f, 720f, 100f);
                band.SetAsFirstSibling();
            }

            Transform profile = bar.Find("Profile");
            if (profile == null) return;
            PlaceTop((RectTransform)profile, layout.Profile.x, layout.Profile.y,
                layout.Profile.width, layout.Profile.height);
            // Avatar discs may protrude below/above the band by design (the baked ring
            // does); only the bar-level mask may clip them at the screen edge.
            RectMask2D staleProfileMask = profile.GetComponent<RectMask2D>();
            if (staleProfileMask != null) staleProfileMask.enabled = false;
            Transform profileVisual = profile.Find("ProfileVisualV2");
            if (profileVisual != null)
            {
                profileVisual.gameObject.SetActive(layout.NativeChrome);
                if (layout.NativeChrome) Stretch((RectTransform)profileVisual);
            }
            Transform avatarMask = profile.Find("AvatarMask");
            if (avatarMask != null)
            {
                avatarMask.gameObject.SetActive(true);
                PlaceTop((RectTransform)avatarMask, layout.Avatar.x, layout.Avatar.y,
                    layout.Avatar.width, layout.Avatar.height);
            }
            Text name = profile.Find("PlayerName")?.GetComponent<Text>();
            if (name != null)
            {
                PlaceTop(name.rectTransform, layout.Name.x, layout.Name.y,
                    layout.Name.width, layout.Name.height);
                name.alignment = TextAnchor.MiddleLeft;
                name.fontSize = 17;
                name.fontStyle = FontStyle.Bold;
                name.horizontalOverflow = HorizontalWrapMode.Overflow;
                // NotoSansSC line height is 1.448em: at 17pt the line needs ~24.6px,
                // taller than every name band — with Truncate Unity drops the whole
                // line, which is exactly why the name vanished on all pages.
                name.verticalOverflow = VerticalWrapMode.Overflow;
            }
            Text level = profile.Find("TeamAverageLevel")?.GetComponent<Text>();
            if (level == null)
                level = BoardText038(profile, "TeamAverageLevel", "LV." + TeamAverageLevel,
                    11, Muted, layout.Level.x, layout.Level.y, layout.Level.width, layout.Level.height);
            PlaceTop(level.rectTransform, layout.Level.x, layout.Level.y,
                layout.Level.width, layout.Level.height);
            level.alignment = TextAnchor.MiddleLeft;
            level.fontSize = 11;
            level.horizontalOverflow = HorizontalWrapMode.Overflow;
            PlaceTop(teamLevelText.rectTransform, layout.Power.x, layout.Power.y,
                layout.Power.width, layout.Power.height);
            teamLevelText.alignment = TextAnchor.MiddleLeft;
            teamLevelText.fontSize = 13;
            teamLevelText.horizontalOverflow = HorizontalWrapMode.Overflow;

            string[] currencies = { CurrencyIds.Diamond, CurrencyIds.Gold, CurrencyIds.Stamina };
            string[] iconNames = { "DiamondIcon", "GoldIcon", "StaminaIcon" };
            string[] iconPaths = { "Art/UI/ResourceDiamond-C", "Art/UI/ResourceGold-C", "Art/UI/ResourceStamina-C" };
            Text[] values = { diamondText, goldText, staminaText };
            for (int i = 0; i < currencies.Length; i++)
            {
                Transform pill = bar.Find("Currency-" + currencies[i]);
                if (pill == null) continue;
                Rect pillRect = layout.Pills[i];
                PlaceTop((RectTransform)pill, pillRect.x, pillRect.y, pillRect.width, pillRect.height);
                Transform visual = pill.Find(currencies[i] == CurrencyIds.Diamond ? "DiamondVisualV2" :
                    currencies[i] == CurrencyIds.Gold ? "GoldVisualV2" : "StaminaVisualV2");
                if (visual != null)
                {
                    visual.gameObject.SetActive(layout.NativeChrome);
                    if (layout.NativeChrome) Stretch((RectTransform)visual);
                }

                Rect iconRect = layout.Icons[i];
                Transform icon = pill.Find(iconNames[i]);
                if (icon == null) icon = NewObject(iconNames[i], pill).transform;
                icon.gameObject.SetActive(true);
                Image iconImage = icon.GetComponent<Image>();
                if (layout.LiveIcons)
                {
                    if (iconImage == null)
                    {
                        iconImage = icon.gameObject.AddComponent<Image>();
                        iconImage.sprite = Resources.Load<Sprite>(iconPaths[i]);
                        iconImage.preserveAspect = true;
                        iconImage.useSpriteMesh = true;
                        iconImage.raycastTarget = false;
                    }
                    iconImage.enabled = true;
                }
                else if (iconImage != null) iconImage.enabled = false;
                RectTransform iconTransform = icon as RectTransform ??
                    icon.gameObject.AddComponent<RectTransform>();
                PlaceTop(iconTransform, iconRect.x, iconRect.y, iconRect.width, iconRect.height);

                Rect valueRect = layout.Values[i];
                Text value = values[i];
                PlaceTop(value.rectTransform, valueRect.x, valueRect.y, valueRect.width, valueRect.height);
                value.alignment = TextAnchor.MiddleCenter;
                value.horizontalOverflow = HorizontalWrapMode.Overflow;
                value.resizeTextForBestFit = true;
                value.resizeTextMinSize = 10;
                // Bound the rendered width: under Overflow, bestFit only checks the
                // vertical fit, so a long value ("120/120" = 3.72em) rendered at 17
                // overflowed into the opaque icon/plus tile on members ("20/12").
                // Capping maxSize makes the worst case impossible on every page.
                value.resizeTextMaxSize = layout.ValueMax[i];

                Rect tileRect = layout.Tiles[i];
                Transform plus = bar.Find("CurrencyPlus-" + currencies[i]);
                if (plus != null)
                    PlaceTop((RectTransform)plus, tileRect.x, tileRect.y, tileRect.width, tileRect.height);
            }

            Transform settings = bar.Find("Settings");
            if (settings == null) return;
            PlaceTop((RectTransform)settings, layout.Settings.x, layout.Settings.y,
                layout.Settings.width, layout.Settings.height);
            Transform gear = settings.Find("SettingsVisualV2");
            if (gear != null)
            {
                gear.gameObject.SetActive(true);
                PlaceTop((RectTransform)gear, layout.Gear.x, layout.Gear.y,
                    layout.Gear.width, layout.Gear.height);
                Image gearImage = gear.GetComponent<Image>();
                if (gearImage != null) gearImage.preserveAspect = true;
            }

            // SharedHeaderBand038: the members icon cell hosts a live mail entry on
            // its left half and the gear on its right. Mail keeps the existing
            // OpenInbox route; only visibility/geometry is unified here.
            Transform mail = bar.Find("Mail");
            if (mail != null)
            {
                mail.gameObject.SetActive(true);
                PlaceTop((RectTransform)mail, layout.Mail.x, layout.Mail.y,
                    layout.Mail.width, layout.Mail.height);
                Transform mailIcon = mail.Find("Icon");
                if (mailIcon != null)
                    PlaceTop((RectTransform)mailIcon, layout.MailIcon.x, layout.MailIcon.y,
                        layout.MailIcon.width, layout.MailIcon.height);
                AttachLobbyHotspotFeedback(mail.gameObject, LobbyHotspotFeedback.VisualKind.Entry);
            }

            foreach (Text label in bar.GetComponentsInChildren<Text>(true)) label.enabled = true;
            foreach (LobbyHotspotFeedback feedback in bar.GetComponentsInChildren<LobbyHotspotFeedback>(true)) feedback.enabled = true;
            UpdateTopBar();
        }

        private static string HudAmount038(int amount)
        {
            var culture = System.Globalization.CultureInfo.InvariantCulture;
            if (amount >= 100000000) return (amount / 100000000d).ToString("0.#", culture) + "亿";
            if (amount >= 10000) return (amount / 10000d).ToString("0.#", culture) + "万";
            return amount.ToString("N0", culture);
        }

        private Text BoardText038(Transform parent, string name, string value, int size, Color color,
            float x, float y, float width, float height, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            Text label = NewPlacedText(parent, value, size, color, x, y, width, height, anchor, FontStyle.Bold);
            label.name = name;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.raycastTarget = false;
            return label;
        }

        private GameObject BoardButton038(string name, Transform parent, Rect bounds, UnityEngine.Events.UnityAction action)
        {
            GameObject obj = NewImage(name, parent, null, Color.clear);
            PlaceTop(obj.GetComponent<RectTransform>(), bounds.x, bounds.y, bounds.width, bounds.height);
            Image hit = obj.GetComponent<Image>();
            hit.raycastTarget = true;
            Button button = obj.AddComponent<Button>();
            button.targetGraphic = hit;
            button.onClick.AddListener(() => { action?.Invoke(); ResumeMediaAfterUserGesture(); });
            AttachLobbyHotspotFeedback(obj, LobbyHotspotFeedback.VisualKind.Entry);
            return obj;
        }

        // ------------------------------------------------------------------
        // MainBakedMotion038 — binding layer that hands baked-board controls to the
        // core BakedRegionMotion038 whole-body feedback. Hit RectTransforms never
        // move; the overlay slice is pixel-identical at rest and scales on hover.

        /// <summary>Attach whole-body feedback for a control whose art sits at a known
        /// board-pixel region. extras are page-level nodes that must move with it.
        /// The overlay group is then moved to the bottom of the button's own stack so
        /// live children (labels, portraits, chips) stay visible above the opaque
        /// slice — the component animates children in place, so Find paths, sibling
        /// order among the children, and dynamic labels are all preserved.</summary>
        private void MainBakedMotion038Attach(GameObject button, Image board,
            Rect regionInBoardPixels, params Transform[] extras)
        {
            if (button == null || board == null) return;
            if (BakedRegionMotion038.Attach(button, board, regionInBoardPixels, extras) == null)
                return;
            Transform body = button.transform.Find(BakedRegionMotion038.BodyName);
            if (body != null) body.SetAsFirstSibling();
        }

        /// <summary>Attach with an authored silhouette polygon (board source pixels,
        /// top-left origin). The slice is vertex-clipped to the polygon so neighbouring
        /// baked art inside the rectangular region is not dragged on hover/press.</summary>
        private void MainBakedMotion038Attach(GameObject button, Image board,
            Rect regionInBoardPixels, Vector2[] silhouetteBoardPixels, params Transform[] extras)
        {
            if (button == null || board == null) return;
            if (BakedRegionMotion038.Attach(button, board, regionInBoardPixels, 3f,
                    silhouetteBoardPixels, extras) == null)
                return;
            Transform body = button.transform.Find(BakedRegionMotion038.BodyName);
            if (body != null) body.SetAsFirstSibling();
        }

        /// <summary>Attach using the button's current footprint on the displayed board —
        /// required for the lobby, where hotspots are laid out in 720x1290 design space
        /// while the board PNG is 862x1825. The derived region is the exact board art
        /// under the control, whatever the aspect/letterbox mapping is.</summary>
        private void MainBakedMotion038AttachFootprint(GameObject button, Image board,
            float inflateBoardPixels, params Transform[] extras)
        {
            Rect? region = MainBakedMotion038Footprint(button, board, inflateBoardPixels);
            if (region.HasValue)
                MainBakedMotion038Attach(button, board, region.Value, extras);
        }

        /// <summary>Footprint attach plus an authored silhouette polygon (board source
        /// pixels, top-left origin): the runtime-derived slice is vertex-clipped to the
        /// control's own contour so a neighbour's interleaved baked art stays static.</summary>
        private void MainBakedMotion038AttachFootprint(GameObject button, Image board,
            float inflateBoardPixels, Vector2[] silhouetteBoardPixels, params Transform[] extras)
        {
            Rect? region = MainBakedMotion038Footprint(button, board, inflateBoardPixels);
            if (region.HasValue)
                MainBakedMotion038Attach(button, board, region.Value, silhouetteBoardPixels, extras);
        }

        private Rect? MainBakedMotion038Footprint(GameObject button, Image board, float inflate)
        {
            if (button == null || board == null || board.sprite == null) return null;
            RectTransform host = button.transform as RectTransform;
            if (host == null) return null;
            RectTransform boardRect = board.rectTransform;
            Sprite sprite = board.sprite;
            Rect boardLocal = boardRect.rect;
            float fit = Mathf.Min(boardLocal.width / sprite.rect.width,
                boardLocal.height / sprite.rect.height);
            if (fit <= 0f) return null;
            Vector2 boardCentre = new Vector2(boardLocal.width * (.5f - boardRect.pivot.x),
                boardLocal.height * (.5f - boardRect.pivot.y));
            Vector3[] corners = new Vector3[4];
            host.GetWorldCorners(corners);
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                Vector3 local = boardRect.InverseTransformPoint(corners[i]);
                float px = (local.x - boardCentre.x) / fit + sprite.rect.width * .5f;
                float py = sprite.rect.height * .5f - (local.y - boardCentre.y) / fit;
                minX = Mathf.Min(minX, px); maxX = Mathf.Max(maxX, px);
                minY = Mathf.Min(minY, py); maxY = Mathf.Max(maxY, py);
            }
            return Rect.MinMaxRect(
                Mathf.Max(0f, minX - inflate), Mathf.Max(0f, minY - inflate),
                Mathf.Min(sprite.rect.width, maxX + inflate),
                Mathf.Min(sprite.rect.height, maxY + inflate));
        }

        private static Rect MainBakedMotion038Inflate(Rect rect, float boardPixels)
        {
            return new Rect(rect.x - boardPixels, rect.y - boardPixels,
                rect.width + boardPixels * 2f, rect.height + boardPixels * 2f);
        }

        private void BuildTeamBoard038()
        {
            RectTransform root = ReferenceBoard038("TeamReferenceBoard038", "Art/Reference038/team-board-038");
            int count = Math.Min(GameModel.TeamCapacity, model.Save.Team.Count);
            int roles = model.Save.Team.Take(count).Where(i => i >= 0 && i < GameModel.Members.Length)
                .Select(i => GameModel.Members[i].Career).Distinct().Count();
            BoardText038(root, "TeamPowerValue", model.TeamPower.ToString("N0"), 64, White, 556, 232, 218, 92);
            BoardText038(root, "TeamResonanceValue", $"职业种类 {roles}/4", 19, Cyan, 560, 331, 192, 32);
            BoardText038(root, "TeamMemberCount", $"成员 {count}/{GameModel.TeamCapacity}", 17, Muted, 561, 366, 180, 28);
            Rect[] portraits = { new Rect(235, 382, 365, 348), new Rect(12, 605, 315, 360),
                new Rect(550, 579, 295, 380), new Rect(210, 806, 390, 305) };
            Rect[] plates = { new Rect(352, 728, 163, 67), new Rect(81, 956, 183, 74),
                new Rect(635, 948, 178, 77), new Rect(334, 1107, 205, 69) };
            Rect[] targets = { new Rect(327, 382, 201, 413), new Rect(45, 605, 220, 425),
                new Rect(620, 579, 193, 446), new Rect(291, 806, 250, 370) };
            for (int slot = 0; slot < GameModel.TeamCapacity; slot++)
            {
                int capturedSlot = slot;
                int index = slot < count ? model.Save.Team[slot] : -1;
                bool valid = index >= 0 && index < GameModel.Members.Length;
                Rect p = portraits[slot], label = plates[slot];
                GameObject hit = BoardButton038("TeamOrbit-" + slot, root,
                    targets[slot], () =>
                    { if (valid) OpenTeamMember(index, capturedSlot); else OpenTeamSlotPicker(Math.Min(capturedSlot, model.Save.Team.Count)); });
                if (!valid)
                {
                    BoardText038(root, "TeamEmpty-" + slot, "+", 46, Cyan, p.x, p.y + p.height * .5f, p.width, 60, TextAnchor.MiddleCenter);
                    continue;
                }
                MemberDefinition m = GameModel.Members[index];
                GameObject portrait = NewImage("TeamCharacter-" + slot, hit.transform, Resources.Load<Sprite>(m.ResourcePath), White);
                PlaceTop(portrait.GetComponent<RectTransform>(), p.x - targets[slot].x,
                    p.y - targets[slot].y, p.width, p.height);
                portrait.GetComponent<Image>().preserveAspect = true;
                portrait.GetComponent<Image>().raycastTarget = false;
                GameObject tag = NewImage("TeamLabel-" + slot, root, null, Color.clear);
                PlaceTop(tag.GetComponent<RectTransform>(), label.x, label.y, label.width, label.height);
                tag.GetComponent<Image>().raycastTarget = false;
                BoardText038(tag.transform, "MemberName", m.Name, 21, White, 3, 3, label.width - 6, 23);
                BoardText038(tag.transform, "MemberCareer", $"{m.Career} · 等级 {model.LevelOf(index)}", 13, Pink, 3, 28, label.width - 6, 17);
                BoardText038(tag.transform, "MemberPower", $"战力 {model.PowerOf(index):N0}", 14, Cyan, 3, 47, label.width - 6, 17);
            }
            GameObject synergy = NewImage("TeamSynergy", root, null, Color.clear);
            PlaceTop(synergy.GetComponent<RectTransform>(), 78, 1268, 690, 164);
            synergy.GetComponent<Image>().raycastTarget = false;
            BoardText038(synergy.transform, "TeamAttributes", "职业自由搭配", 14, Muted, 492, 0, 190, 26, TextAnchor.MiddleRight);
            for (int i = 0; i < MemberCareers.All.Count; i++)
            {
                string career = MemberCareers.All[i];
                int number = model.Save.Team.Count(index => index >= 0 && index < GameModel.Members.Length && GameModel.Members[index].Career == career);
                float x = 29 + i * 156;
                BoardText038(synergy.transform, "CareerStatus-" + i, $"{career} · {number}人", 17, White, x, 28, 150, 32, TextAnchor.MiddleCenter);
                GameObject glyph = NewImage("CareerGlyph-" + i, synergy.transform, NavIconSprite(i), White);
                PlaceTop(glyph.GetComponent<RectTransform>(), x + 37, 67, 76, 74);
                glyph.GetComponent<Image>().preserveAspect = true;
                glyph.GetComponent<Image>().raycastTarget = false;
            }
            BoardButton038("ChangeLeader", root, new Rect(118, 1450, 304, 110), OpenCaptainPicker);
            BoardButton038("AutoTeam", root, new Rect(465, 1450, 303, 110), () =>
            { model.AutoTeam(); Toast("已优先兼顾职业并补齐阵容"); ShowScreen("team"); });
        }

        private void BuildMembersBoard038()
        {
            RectTransform root = ReferenceBoard038("MembersReferenceBoard038", "Art/Reference038/members-board-038");
            string[] roles = new[] { string.Empty }.Concat(MemberCareers.All).ToArray();
            string[] races = { string.Empty, "魅族", "魔族", "海灵族", "血精灵" };
            memberRoleFilterIndex = Mathf.Clamp(memberRoleFilterIndex, 0, roles.Length - 1);
            memberRaceFilterIndex = Mathf.Clamp(memberRaceFilterIndex, 0, races.Length - 1);
            string role = roles[memberRoleFilterIndex], race = races[memberRaceFilterIndex];
            MemberRosterPage page = MemberRosterPagination.Build(GameModel.Members.Length, memberPageIndex, index =>
            {
                MemberDefinition m = GameModel.Members[index];
                return MemberRosterVisibility.MatchesRosterFilter(model.IsUnlocked(index), m.Name, m.Career,
                    MemberRaceFamily(m, index), memberOwnedOnly, role, race, memberSearchQuery);
            }, 15, index => model.IsUnlocked(index) ? 0 : 1);
            memberPageIndex = page.PageIndex;
            BoardText038(root, "ScreenSubtitle", $"已拥有 {model.Save.UnlockedMembers.Count}/{GameModel.Members.Length} · 本页 {page.VisibleCount} 名",
                18, White, 46, 307, 735, 32);
            string[] names = { "MemberRoleFilter", "MemberRaceFilter", "MemberOwnedFilter" };
            string[] labels = { "职业：" + (role.Length == 0 ? "全部" : role), "种族：" + (race.Length == 0 ? "全部" : race), "仅已拥有：" + (memberOwnedOnly ? "开" : "关") };
            for (int i = 0; i < 3; i++)
            {
                int filter = i;
                GameObject button = BoardButton038(names[i], root, new Rect(43 + i * 252, 347, 238, 53), () =>
                {
                    if (filter == 0) memberRoleFilterIndex = (memberRoleFilterIndex + 1) % roles.Length;
                    else if (filter == 1) memberRaceFilterIndex = (memberRaceFilterIndex + 1) % races.Length;
                    else memberOwnedOnly = !memberOwnedOnly;
                    memberPageIndex = 0; ShowScreen("members");
                });
                Text filterLabel = BoardText038(button.transform, "Label", labels[i], 17, White,
                    28, 9, 182, 35, TextAnchor.MiddleCenter);
                PanelKit.EnableBestFit(filterLabel, 15);
                filterLabel.verticalOverflow = VerticalWrapMode.Truncate;
            }
            GameObject search = NewImage("MemberSearch", root, null, Color.clear);
            PlaceTop(search.GetComponent<RectTransform>(), 42, 419, 733, 61);
            search.GetComponent<Image>().raycastTarget = true;
            Text placeholder = BoardText038(search.transform, "Placeholder", "◇  搜索成员名称，输入后按回车", 17, Muted, 15, 0, 690, 60);
            Text value = BoardText038(search.transform, "Value", memberSearchQuery, 18, White, 15, 0, 690, 60);
            InputField input = search.AddComponent<InputField>();
            input.targetGraphic = search.GetComponent<Image>(); input.textComponent = value; input.placeholder = placeholder;
            input.characterLimit = 12; input.text = memberSearchQuery;
            input.onEndEdit.AddListener(query => { string text = (query ?? "").Trim(); if (text == memberSearchQuery) return;
                memberSearchQuery = text; memberPageIndex = 0; ShowScreen("members"); });
            for (int slot = 0; slot < page.VisibleCount; slot++)
            {
                int index = page.SourceIndexAt(slot);
                MemberDefinition m = GameModel.Members[index];
                GameObject card = BoardButton038("Member-" + m.Id, root,
                    new Rect(32 + (slot % 5) * 160, 503 + (slot / 5) * 283, 143, 269), () => OpenMember(index));
                if (!model.IsUnlocked(index))
                {
                    BuildLockedMemberCard(card, 143, 269);
                    card.AddComponent<RectMask2D>();
                    Image silhouette = card.transform.Find("LockedSilhouette").GetComponent<Image>();
                    PlaceTop(silhouette.rectTransform, 10, 24, 123, 174);
                    Text lockedName = card.transform.Find("LockedName").GetComponent<Text>();
                    PlaceTop(lockedName.rectTransform, 12, 202, 119, 28);
                    lockedName.fontSize = 16;
                    lockedName.verticalOverflow = VerticalWrapMode.Truncate;
                    PanelKit.EnableBestFit(lockedName, 14);
                    Text lockedProgress = card.transform.Find("LockedProgress").GetComponent<Text>();
                    PlaceTop(lockedProgress.rectTransform, 12, 234, 119, 22);
                    lockedProgress.fontSize = 11;
                    lockedProgress.verticalOverflow = VerticalWrapMode.Truncate;
                    PanelKit.EnableBestFit(lockedProgress, 10);
                    Sprite unknown = ReferenceArt038.Load("Art/Reference038/unknown-member-038");
                    if (unknown != null)
                    {
                        silhouette.sprite = unknown;
                        silhouette.color = White;
                        card.transform.Find("CareerGlow").gameObject.SetActive(false);
                        card.transform.Find("LockedMark").gameObject.SetActive(false);
                    }
                    continue;
                }
                GameObject frame = NewImage("PortraitFrame", card.transform, null, Color.clear);
                PlaceTop(frame.GetComponent<RectTransform>(), 3, 3, 137, 186); frame.AddComponent<RectMask2D>();
                GameObject art = NewImage("Portrait", frame.transform, Resources.Load<Sprite>(m.ThumbnailResourcePath), White);
                PanelKit.FrameBustPortrait(art.GetComponent<Image>(), frame.GetComponent<RectTransform>(), m.Id);
                GameObject ribbon = NewImage("DeploymentBadge", card.transform, RibbonSprite(), model.IsInTeam(index) ? new Color32(36, 16, 45, 235) : new Color32(13, 8, 32, 230));
                PlaceTop(ribbon.GetComponent<RectTransform>(), 0, 4, 103, 27);
                BoardText038(ribbon.transform, "DeploymentLabel", MemberDeploymentLabel(index), 14, model.Save.Team.IndexOf(index) == 0 ? Pink : new Color32(255, 216, 110, 255), 0, 0, 103, 27, TextAnchor.MiddleCenter);
                BoardText038(card.transform, "Taxonomy", $"{MemberRaceFamily(m, index)} · {m.Career}", 12, Pink, 6, 185, 130, 25);
                BoardText038(card.transform, "MemberName", m.Name, 19, White, 6, 208, 130, 31);
                BoardText038(card.transform, "MemberLevel", $"等级 {model.LevelOf(index)}", 13, Cyan, 6, 238, 130, 22);
            }
            if (page.IsEmpty) BoardText038(root, "MemberEmpty", "没有符合条件的成员，请调整筛选", 23, White, 70, 670, 700, 80, TextAnchor.MiddleCenter);
            GameObject previous = BoardButton038("MemberPreviousPage", root, new Rect(165, 1386, 185, 76), () =>
            { memberPageIndex = MemberRosterPagination.MovePage(memberPageIndex, -1, page.PageCount); ShowScreen("members"); });
            previous.GetComponent<Button>().interactable = page.HasPrevious;
            BoardText038(previous.transform, "Label", "上一页", 24, page.HasPrevious ? White : Muted, 0, 0, 185, 76, TextAnchor.MiddleCenter);
            BoardText038(root, "MemberPageLabel", $"{(page.PageCount == 0 ? 0 : page.PageIndex + 1)}/{page.PageCount}", 34, White, 360, 1382, 120, 86, TextAnchor.MiddleCenter);
            GameObject next = BoardButton038("MemberNextPage", root, new Rect(491, 1386, 186, 76), () =>
            { memberPageIndex = MemberRosterPagination.MovePage(memberPageIndex, 1, page.PageCount); ShowScreen("members"); });
            next.GetComponent<Button>().interactable = page.HasNext;
            BoardText038(next.transform, "Label", "下一页", 24, page.HasNext ? White : Muted, 0, 0, 186, 76, TextAnchor.MiddleCenter);
        }
    }
}
