using System;
using System.Linq;
using ChoSiren.Panels;
using ChoSiren.Systems.Economy;
using ChoSiren.Systems.Presentation;
using ChoSiren.UI;
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
            ChoSiren.UI.PageAmbient.Set(board, art, featherEdges: rect);
            ConfigureReferenceHeader038(true);
            return rect;
        }

        /// <summary>
        /// HeaderPolish038 / NativeHeaderChrome038: user-directed unified header,
        /// second revision. The opaque universal band was rejected — each page's own
        /// art must stay continuous behind the header. So the shared header is now
        /// composed only of the genuine individual RGBA chrome sprites (v2 profile
        /// plaque, three v2 resource pills, gear) plus the live mail envelope, all
        /// at one shared members-target geometry on every common-HUD screen. The
        /// chrome interiors are opaque so they still sit cleanly over whatever each
        /// page bakes in its header region. Contents are centred inside each frame;
        /// hit rects stay fixed and larger than the compact plus/mail/gear visuals.
        /// </summary>
        private sealed class HeaderPolish038Layout
        {
            public Rect Profile;
            public Rect Avatar;      // local to Profile
            public Rect Name;        // local to Profile
            public Rect Level;       // local to Profile
            public Rect Power;       // local to Profile
            public Rect[] Pills = new Rect[3];
            public Rect[] Icons = new Rect[3];   // local to pill; v2 pills bake their currency icon
            public Rect[] Values = new Rect[3];  // local to pill, centred in the interior
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
                // The v2 pill art is 1152x256: icon cap x43-300, interior x300-880,
                // plus cap x880-1113 -> at the sizes below the cap centres land on
                // the tile hit centres and interiors hold icon+value.
                // Matches the lobby's approved header: profile plaque, three equal pills
                // (v2 art: icon cap 4-26%, interior 26-76%, plus cap 76-97%), and mail over
                // gear stacked at the right edge so nothing is squeezed.
                // Mail + gear live only on the lobby (stacked at its right edge), so the three
                // pills spread across the full width after the profile plaque.
                const float pillW = 136f, pillH = 52f, pillY = 26f;
                float[] pillX = { 288f, 432f, 576f };
                return new HeaderPolish038Layout
                {
                    Profile = new Rect(4, 5, 282, 104),
                    Avatar = new Rect(43, 29, 50, 50),
                    Name = new Rect(106, 8, 148, 34),
                    Level = new Rect(110, 44, 150, 25),
                    Power = new Rect(110, 69, 162, 30),
                    Pills = pillX.Select(x => new Rect(x, pillY, pillW, pillH)).ToArray(),
                    Icons = Enumerable.Repeat(new Rect(6, 13, 26, 26), 3).ToArray(),
                    Values = Enumerable.Repeat(new Rect(pillW * .26f, 8, pillW * .5f, 36), 3).ToArray(),
                    Tiles = pillX.Select(x => new Rect(x + pillW * .76f, pillY + 8, 34, 36)).ToArray(),
                    Settings = new Rect(678, 48, 38, 38),
                    Gear = new Rect(3, 3, 32, 32),
                    Mail = new Rect(678, 8, 38, 36),
                    MailIcon = new Rect(0, 3, 38, 30),
                    NativeChrome = true,
                    LiveIcons = false,
                    ValueMax = new[] { 21, 21, 19 },
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

            // NativeHeaderChrome038: the opaque universal band was rejected — the
            // page background must stay continuous under individual chrome sprites.
            // Remove any leftover shared band node so nothing can seam.
            Transform band = bar.Find("HeaderBandMembers038");
            if (band != null)
            {
                band.gameObject.SetActive(false);
                Destroy(band.gameObject);
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
                Image avatarShape = avatarMask.GetComponent<Image>();
                if (avatarShape != null)
                {
                    avatarShape.sprite = RoundedSprite(32);
                    avatarShape.type = Image.Type.Simple;
                }
            }
            Text name = profile.Find("PlayerName")?.GetComponent<Text>();
            if (name != null)
            {
                Transform quiet = profile.Find("ProfileNameQuietPlate1019");
                if (quiet != null) quiet.gameObject.SetActive(false);
                name.transform.SetAsLastSibling();
                PlaceTop(name.rectTransform, layout.Name.x, layout.Name.y,
                    layout.Name.width, layout.Name.height);
                name.alignment = TextAnchor.MiddleLeft;
                name.text = model.PlayerName;
                FitPlayerName1028(name);
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
            level.fontSize = 16;
            level.color = new Color32(225, 215, 242, 255);
            level.horizontalOverflow = HorizontalWrapMode.Overflow;
            level.verticalOverflow = VerticalWrapMode.Overflow;
            PlaceTop(teamLevelText.rectTransform, layout.Power.x, layout.Power.y,
                layout.Power.width, layout.Power.height);
            teamLevelText.alignment = TextAnchor.MiddleLeft;
            teamLevelText.resizeTextForBestFit = false;
            teamLevelText.fontSize = 18;
            teamLevelText.color = White;
            teamLevelText.horizontalOverflow = HorizontalWrapMode.Overflow;
            teamLevelText.verticalOverflow = VerticalWrapMode.Overflow;
            // Achievement red dot on the plaque (the lobby's painted header used to carry it).
            Transform achievementBadge = profile.Find("AchievementBadge");
            if (achievementBadge == null)
                achievementBadge = UI.NoticeSticker1031.Create("AchievementBadge", profile).transform;
            PlaceTop((RectTransform)achievementBadge, 77, 8, 22, 22);

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
                value.color = White;
                value.fontStyle = FontStyle.Bold;
                value.horizontalOverflow = HorizontalWrapMode.Overflow;
                value.verticalOverflow = VerticalWrapMode.Overflow;
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
            settings.gameObject.SetActive(false);
            Transform mail = bar.Find("Mail");
            if (mail != null)
            {
                mail.gameObject.SetActive(false);
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

        /// <summary>Attach using an independent RGBA sprite when the native art asset
        /// exists, falling back to the baked board slice while assets are generated.
        /// The native body owns its alpha silhouette (artist-padded so glow/overshoot
        /// are never clipped), so the flattened scene under the control is never
        /// copied or scaled — neighbouring art stays pixel-stationary in all four
        /// pointer states. Hit RectTransforms still never move. Sprites load via
        /// Resources so they resolve identically in the editor and WebGL builds
        /// (the Reference038 streaming manifest only covers page boards).</summary>
        private void MainNativeOrBakedMotion038Attach(GameObject button, Image board,
            float inflateBoardPixels, string nativeResourcePath,
            Vector2[] silhouetteBoardPixels = null, params Transform[] extras)
        {
            if (button == null || board == null) return;
            Rect? region = MainBakedMotion038Footprint(button, board, inflateBoardPixels);
            if (!region.HasValue) return;
            Sprite native = string.IsNullOrEmpty(nativeResourcePath)
                ? null : Resources.Load<Sprite>(nativeResourcePath);
            if (native != null && BakedRegionMotion038.AttachNative(
                    button, board, region.Value, native, extras) != null)
            {
                Transform body = button.transform.Find(BakedRegionMotion038.BodyName);
                if (body != null) body.SetAsFirstSibling();
                return;
            }
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
            // Meeting row 27: NIKKE-style squad screen — calm backdrop, four upright member
            // cards (bust, level, name, race/role, power), squad power up top, two actions.
            RectTransform root = ReferenceBoard038("TeamReferenceBoard038", "Art/Reference038/team-board-038");
            int count = Math.Min(GameModel.TeamCapacity, model.Save.Team.Count);
            int roles = model.Save.Team.Take(count).Where(i => i >= 0 && i < GameModel.Members.Length)
                .Select(i => GameModel.Members[i].Career).Distinct().Count();
            GameObject titleHit = BoardButton038("RenameTeamTitle", root, new Rect(36, 198, 226, 88),
                () => OpenTeamNameDialog1028(model.Save.ActiveTeamPreset));
            titleHit.GetComponent<Image>().color = Color.clear;
            Text teamTitle = BoardText038(root, "TeamTitle", model.ActiveTeamName, 48, White, 44, 206, 210, 72);
            teamTitle.raycastTarget = false;
            PanelKit.EnableBestFit(teamTitle, 24);
            SirenStickerUi.SkinTitle(teamTitle, true);
            RectTransform titlePlate = root.Find("SirenTitlePlate-" + teamTitle.name) as RectTransform;
            float renameX = titlePlate != null
                ? titlePlate.anchoredPosition.x + titlePlate.rect.width * (1f - titlePlate.pivot.x) + 8f
                : 240f;
            GameObject renameTeam = BoardButton038("RenameTeam", root, new Rect(renameX, 226, 44, 44),
                () => OpenTeamNameDialog1028(model.Save.ActiveTeamPreset));
            // Match the profile's pencil affordance; only editable content gets a button.
            UnityEngine.UI.Image renameHit = renameTeam.GetComponent<UnityEngine.UI.Image>();
            renameHit.sprite = VisualA1101.Glow;
            renameHit.color = VisualA1101.Teal * new Color(1f, 1f, 1f, .22f);
            renameHit.preserveAspect = true;
            RectTransform renamePencil = VisualA1101.NeonIcon(renameTeam.transform,
                "Pencil", "pencil", VisualA1101.Teal, 38f);
            VisualA1101.Center(renamePencil, 38f);
            bool ready = model.IsTeamReady(out string formationProblem);
            if (ready)
                BoardText038(root, "TeamSubtitle", "点头像看详情 · 点下方换人", 18, Muted, 48, 282, 380, 28);
            else
                BoardText038(root, "TeamFormationIssue", formationProblem.Split('\n').Last(), 26, Warn, 48, 282, 380, 82);
            // Read-only statistic: no editable-name frame, hit area or hover treatment.
            RectTransform powerReadout = VisualA1101.Rect("TeamPowerReadout", root);
            PlaceTop(powerReadout, 452, 210, 372, 96);
            BoardText038(powerReadout, "Label", "队伍战力", 17, Muted,
                44, 0, 320, 26, TextAnchor.MiddleRight).raycastTarget = false;
            RectTransform powerIcon = VisualA1101.NeonIcon(powerReadout,
                "PowerIcon", "bolt", VisualA1101.Gold, 40f);
            UnityEngine.UI.Text powerValue = BoardText038(powerReadout, "TeamPowerValue",
                model.TeamPower.ToString("N0"), 48, VisualA1101.Gold,
                60, 26, 304, 64, TextAnchor.MiddleRight);
            powerValue.fontStyle = FontStyle.BoldAndItalic;
            powerValue.raycastTarget = false;
            float powerWidth = Mathf.Min(powerValue.rectTransform.rect.width, powerValue.preferredWidth);
            VisualA1101.Place(powerIcon, 364f - powerWidth - 48f, 38, 40, 40);
            foreach (UnityEngine.UI.Graphic graphic in powerReadout.GetComponentsInChildren<UnityEngine.UI.Graphic>())
                graphic.raycastTarget = false;
            BoardText038(root, "TeamResonanceValue", $"职业种类 {roles}/4", 18, Cyan, 470, 314, 180, 28);
            BoardText038(root, "TeamMemberCount", $"成员 {count}/{GameModel.TeamCapacity}", 18, Muted, 660, 314, 160, 28, TextAnchor.MiddleRight);

            const float cardW = 334f, cardH = 452f, artH = 336f;
            for (int slot = 0; slot < GameModel.TeamCapacity; slot++)
            {
                int capturedSlot = slot;
                int index = slot < count ? model.Save.Team[slot] : -1;
                bool valid = index >= 0 && index < GameModel.Members.Length;
                Rect cardRect = new Rect(80 + (slot % 2) * (cardW + 36), 372 + (slot / 2) * (cardH + 22), cardW, cardH);
                GameObject hit = BoardButton038("TeamOrbit-" + slot, root, cardRect, () =>
                    { if (valid) OpenTeamMember(index, capturedSlot); else OpenTeamSlotPicker(Math.Min(capturedSlot, model.Save.Team.Count)); });
                Image back = hit.GetComponent<Image>();
                back.color = new Color32(8, 4, 16, 255);
                Outline rim = hit.AddComponent<Outline>();
                rim.effectDistance = new Vector2(3, -3);
                SirenStickerUi.SkinPanel(hit);
                if (!valid)
                {
                    rim.effectColor = new Color(1, 1, 1, .25f);
                    BoardText038(hit.transform, "TeamEmpty-" + slot, "+", 72, Cyan, 0, 130, cardW, 100, TextAnchor.MiddleCenter);
                    BoardText038(hit.transform, "EmptyHint", "添加成员", 22, Muted, 0, 240, cardW, 36, TextAnchor.MiddleCenter);
                    continue;
                }
                MemberDefinition m = GameModel.Members[index];
                string race = MemberRaceFamily(m, index);
                Color raceColor = TeamRaceColor(race);
                rim.effectColor = raceColor;
                GameObject window = NewImage("PortraitWindow", hit.transform, null, new Color32(24, 14, 40, 255));
                PlaceTop(window.GetComponent<RectTransform>(), 6, 6, cardW - 12, artH - 6);
                window.GetComponent<Image>().raycastTarget = false;
                window.AddComponent<RectMask2D>();
                GameObject portrait = NewImage("TeamCharacter-" + slot, window.transform, Resources.Load<Sprite>(m.ResourcePath), White);
                portrait.GetComponent<Image>().preserveAspect = true;
                portrait.GetComponent<Image>().raycastTarget = false;
                PanelKit.FrameBustPortrait(portrait.GetComponent<Image>(), window.GetComponent<RectTransform>(), m.Id);
                // Dark fade at the bust's foot so level and name read over any outfit.
                GameObject fade = NewImage("Fade", window.transform, BottomFade(), new Color(0, 0, 0, .92f));
                PlaceTop(fade.GetComponent<RectTransform>(), 0, artH - 130, cardW, 130);
                fade.GetComponent<Image>().raycastTarget = false;
                BoardText038(window.transform, "LvTag", "LV.", 16, White, 14, artH - 74, 40, 20);
                BoardText038(window.transform, "Level", model.LevelOf(index).ToString(), 48, White, 12, artH - 60, 90, 56);
                Text name = BoardText038(window.transform, "MemberName", m.Name, 30, White, 96, artH - 54, cardW - 108, 44, TextAnchor.MiddleRight);
                PanelKit.EnableBestFit(name, 20);
                name.verticalOverflow = VerticalWrapMode.Truncate;
                GameObject band = NewImage("Band", hit.transform, null, raceColor);
                PlaceTop(band.GetComponent<RectTransform>(), 0, artH, cardW, 6);
                band.GetComponent<Image>().raycastTarget = false;
                // Race + role badges stacked top-left like NIKKE's element / class hexes.
                Sprite raceIcon = RaceIconSprite(race);
                GameObject raceChip = NewImage("RaceChip", hit.transform, raceIcon ?? Panels.PunkUi.SlantFill, raceIcon != null ? (Color)White : raceColor);
                PlaceTop(raceChip.GetComponent<RectTransform>(), 8, 8, 58, 58);
                raceChip.GetComponent<Image>().preserveAspect = true;
                raceChip.GetComponent<Image>().raycastTarget = false;
                if (raceIcon == null) BoardText038(raceChip.transform, "Race", race, 15, Color.black, 0, 0, 58, 58, TextAnchor.MiddleCenter);
                GameObject roleChip = NewImage("RoleChip", hit.transform, SirenStickerUi.TabSprite, SirenStickerUi.Ink);
                PlaceTop(roleChip.GetComponent<RectTransform>(), 8, 70, 92, 28);
                roleChip.GetComponent<Image>().raycastTarget = false;
                BoardText038(roleChip.transform, "Role", m.Career, 16, White, 0, 0, 92, 28, TextAnchor.MiddleCenter);
                if (slot == 0)
                {
                    GameObject captain = NewImage("CaptainTag", hit.transform, SirenStickerUi.TabSprite, SirenStickerUi.Violet);
                    PlaceTop(captain.GetComponent<RectTransform>(), cardW - 100, 10, 90, 34);
                    captain.GetComponent<Image>().raycastTarget = false;
                    BoardText038(captain.transform, "Label", "队长", 19, White, 0, 0, 90, 34, TextAnchor.MiddleCenter);
                }
                // Keep the portrait's detail action; the whole information band is a
                // separate, phone-sized replacement target rather than a tiny text link.
                GameObject tag = BoardButton038("TeamLabel-" + slot, hit.transform,
                    new Rect(0, artH + 6, cardW, cardH - artH - 6),
                    () => OpenTeamSlotPicker(capturedSlot));
                BoardText038(tag.transform, "MemberCareer", $"{race} · {m.Career}", 18, SirenStickerUi.Paper, 16, 12, cardW - 32, 28);
                BoardText038(tag.transform, "MemberPower", $"战力 {model.PowerOf(index):N0}", 24, Cyan, 16, 50, cardW - 130, 40);
                GameObject swapTag = NewImage("TeamSwapTag", tag.transform, SirenStickerUi.TabSprite, SirenStickerUi.Violet);
                PlaceTop(swapTag.GetComponent<RectTransform>(), cardW - 104, 50, 92, 42);
                swapTag.GetComponent<Image>().raycastTarget = false;
                BoardText038(swapTag.transform, "Info", "换人 ›", 25, White, 0, 0, 92, 42, TextAnchor.MiddleCenter);
            }

            GameObject synergy = NewImage("TeamSynergy", root, null, Color.clear);
            PlaceTop(synergy.GetComponent<RectTransform>(), 80, 1328, 704, 52);
            synergy.GetComponent<Image>().raycastTarget = false;
            for (int i = 0; i < MemberCareers.All.Count; i++)
            {
                string career = MemberCareers.All[i];
                int number = model.Save.Team.Count(index => index >= 0 && index < GameModel.Members.Length && GameModel.Members[index].Career == career);
                GameObject chip = NewImage("CareerChip-" + i, synergy.transform, Panels.PunkUi.SlantFill,
                    number > 0 ? new Color32(6, 3, 12, 235) : new Color32(6, 3, 12, 140));
                PlaceTop(chip.GetComponent<RectTransform>(), i * 178, 0, 168, 50);
                chip.GetComponent<Image>().raycastTarget = false;
                BoardText038(chip.transform, "CareerStatus-" + i, $"{career} ×{number}", 18, number > 0 ? (Color)White : Muted,
                    0, 0, 168, 50, TextAnchor.MiddleCenter);
            }
            TeamActionButton038("ChangeLeader", root, new Rect(80, 1404, 334, 92), "更换队长", false, OpenCaptainPicker);
            TeamActionButton038("AutoTeam", root, new Rect(450, 1404, 334, 92), "一键编队", true, () =>
            {
                model.TryAutoTeam(out string message);
                ShowScreen("team");
                Toast(message);
            });
            BuildTeamPresetControls1028(root);
        }

        private void TeamActionButton038(string name, Transform parent, Rect bounds, string label, bool primary, UnityEngine.Events.UnityAction action)
        {
            GameObject button = BoardButton038(name, parent, bounds, action);
            BoardText038(button.transform, "Label", label, 30, White, 0, 0, bounds.width, bounds.height, TextAnchor.MiddleCenter);
            SirenStickerUi.SkinButton(button, primary);
        }

        private static Sprite bottomFade;

        /// <summary>Vertical alpha ramp: clear at the top, opaque at the bottom.</summary>
        private static Sprite BottomFade()
        {
            if (bottomFade != null) return bottomFade;
            const int length = 64;
            var texture = new Texture2D(1, length, TextureFormat.RGBA32, false)
                { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
            var pixels = new Color32[length];
            for (int i = 0; i < length; i++)
            {
                float t = 1f - i / (length - 1f);
                pixels[i] = new Color32(255, 255, 255, (byte)(255f * Mathf.SmoothStep(0f, 1f, t)));
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            bottomFade = Sprite.Create(texture, new Rect(0, 0, 1, length), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
            bottomFade.hideFlags = HideFlags.DontSave;
            return bottomFade;
        }

        private static Color TeamRaceColor(string race) => race switch
        {
            "魅族" => new Color32(255, 46, 147, 255),
            "魔族" => new Color32(178, 92, 255, 255),
            "海灵族" => new Color32(63, 240, 255, 255),
            "血精灵" => new Color32(255, 84, 84, 255),
            _ => new Color32(220, 220, 230, 255),
        };

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
            string[] labels = { "职业：" + (role.Length == 0 ? "全部" : role), "种族：" + (race.Length == 0 ? "全部" : race), memberOwnedOnly ? "范围：已拥有" : "范围：全部图鉴" };
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
                BoardText038(ribbon.transform, "DeploymentLabel", MemberDeploymentLabel(index), 14, model.Save.Team.IndexOf(index) == 0 ? Accent : new Color32(255, 216, 110, 255), 0, 0, 103, 27, TextAnchor.MiddleCenter);
                BoardText038(card.transform, "Taxonomy", $"{MemberRaceFamily(m, index)} · {m.Career}", 12, Accent, 6, 185, 130, 25);
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
