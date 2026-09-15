using System;
using System.Collections.Generic;
using System.Linq;
using ChoSiren.Panels;
using ChoSiren.Systems.Presentation;
using ChoSiren.Systems.Tactics;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>
    /// 2026-09-15 reference screens: the stellar team orbit, the slanted member
    /// roster, the member dossier modal and the shared procedural decorations they
    /// use. All variable content stays on live data nodes; generated sprites are
    /// flat alpha masks tinted at runtime, never baked values.
    /// </summary>
    public sealed partial class ChoSirenApp
    {
        private readonly Dictionary<string, Sprite> referenceSprites =
            new Dictionary<string, Sprite>();

        // ------------------------------------------------------------------
        // generated decorations (white alpha masks tinted by Image.color)

        /// <summary>Slashed panel: sharp corners cut on two diagonals, sliced so it scales.</summary>
        private Sprite SlashPanelSprite(bool mirror)
        {
            string key = mirror ? "RefSlashPanel-M" : "RefSlashPanel";
            if (referenceSprites.TryGetValue(key, out Sprite cached) && cached != null) return cached;

            const int size = 64;
            const float cut = 17f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = x + .5f;
                    float py = y + .5f;
                    // Texture space: y grows upward. d measures the pixel along the
                    // TL→BR diagonal for the normal cut (BL + TR) or the TR→BL
                    // diagonal for the mirrored variant (TL + BR).
                    float d = mirror ? (size - px) + py : px + py;
                    float inside = Mathf.Min(
                        Mathf.Clamp01(2f * size - cut - d + .8f),
                        Mathf.Clamp01(d - cut + .8f));
                    byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(inside) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }
            Sprite sprite = BakeReferenceSprite(key, pixels, size, size, new Vector4(15, 15, 15, 15));
            referenceSprites[key] = sprite;
            return sprite;
        }

        /// <summary>Thin luminous orbit ring used for the team stage.</summary>
        private Sprite OrbitRingSprite()
        {
            const string key = "RefOrbitRing";
            if (referenceSprites.TryGetValue(key, out Sprite cached) && cached != null) return cached;

            const int size = 256;
            var pixels = new Color32[size * size];
            Vector2 center = Vector2.one * (size - 1) * .5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), center) / (size * .5f);
                    float ring = Mathf.Clamp01(1f - Mathf.Abs(d - .86f) * 9f);
                    float inner = Mathf.Pow(Mathf.Clamp01(1f - d), 2.6f) * .55f;
                    float rim = Mathf.Clamp01(1f - Mathf.Abs(d - .985f) * 22f) * .9f;
                    byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(Mathf.Max(ring, Mathf.Max(inner, rim))) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }
            Sprite sprite = BakeReferenceSprite(key, pixels, size, size, Vector4.zero);
            referenceSprites[key] = sprite;
            return sprite;
        }

        /// <summary>Horizontal equalizer strip used as a decoration behind titles.</summary>
        private Sprite WaveformSprite()
        {
            const string key = "RefWaveform";
            if (referenceSprites.TryGetValue(key, out Sprite cached) && cached != null) return cached;

            const int width = 160;
            const int height = 32;
            var pixels = new Color32[width * height];
            for (int bar = 0; bar < 26; bar++)
            {
                float cx = 4f + bar * 6f;
                float half = (Mathf.Sin(bar * 1.7f) * .5f + .5f) * 11f + 3f;
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        float dx = Mathf.Abs(x + .5f - cx);
                        float dy = Mathf.Abs(y + .5f - height * .5f);
                        if (dx > 2.1f || dy > half) continue;
                        float coverage = Mathf.Clamp01(2.1f - dx) * Mathf.Clamp01(half - dy + .6f);
                        int index = y * width + x;
                        byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(coverage) * 255f);
                        if (alpha > pixels[index].a) pixels[index] = new Color32(255, 255, 255, alpha);
                    }
                }
            }
            Sprite sprite = BakeReferenceSprite(key, pixels, width, height, Vector4.zero);
            referenceSprites[key] = sprite;
            return sprite;
        }

        /// <summary>Slanted ribbon for status flags (队长/出战中) and small tags.</summary>
        private Sprite RibbonSprite()
        {
            const string key = "RefRibbon";
            if (referenceSprites.TryGetValue(key, out Sprite cached) && cached != null) return cached;

            const int width = 96;
            const int height = 28;
            const float slant = 9f;
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                float t = (y + .5f) / height;
                float left = slant * (1f - t);
                float right = width - slant * t;
                for (int x = 0; x < width; x++)
                {
                    float px = x + .5f;
                    float coverage = Mathf.Min(Mathf.Clamp01(px - left + .8f), Mathf.Clamp01(right - px + .8f));
                    pixels[y * width + x] = new Color32(255, 255, 255,
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(coverage) * 255f));
                }
            }
            Sprite sprite = BakeReferenceSprite(key, pixels, width, height, new Vector4(10, 10, 10, 10));
            referenceSprites[key] = sprite;
            return sprite;
        }

        /// <summary>Magnifier glyph for the member search field.</summary>
        private Sprite SearchIconSprite()
        {
            const string key = "RefSearchIcon";
            if (referenceSprites.TryGetValue(key, out Sprite cached) && cached != null) return cached;

            const int size = 32;
            var pixels = new Color32[size * size];
            Vector2 center = new Vector2(13f, 19f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float ring = Mathf.Abs(Vector2.Distance(new Vector2(x + .5f, y + .5f), center) - 8f);
                    float coverage = Mathf.Clamp01(2.4f - ring);
                    Vector2 handle = new Vector2(x + .5f - 21f, y + .5f - 10f);
                    if (handle.x > 0f && handle.y < 0f)
                    {
                        float along = Mathf.Min(handle.x, -handle.y);
                        float across = Mathf.Abs(handle.x + handle.y) * .5f;
                        coverage = Mathf.Max(coverage, Mathf.Clamp01(2.2f - across) *
                            Mathf.Clamp01(along + 1f) * Mathf.Clamp01(8f - along));
                    }
                    pixels[y * size + x] = new Color32(255, 255, 255,
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(coverage) * 255f));
                }
            }
            Sprite sprite = BakeReferenceSprite(key, pixels, size, size, Vector4.zero);
            referenceSprites[key] = sprite;
            return sprite;
        }

        /// <summary>Heart silhouette for the affection row.</summary>
        private Sprite HeartSprite()
        {
            const string key = "RefHeart";
            if (referenceSprites.TryGetValue(key, out Sprite cached) && cached != null) return cached;

            const int size = 64;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Classic parametric heart, origin centered, texture y-up.
                    float px = (x + .5f - size * .5f) / (size * .5f);
                    float py = (y + .5f - size * .44f) / (size * .5f);
                    float a = px * px + py * py - 1f;
                    float inside = a * a * a - px * px * py * py * py;
                    float coverage = Mathf.Clamp01(-inside * 34f + .6f);
                    pixels[y * size + x] = new Color32(255, 255, 255,
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(coverage) * 255f));
                }
            }
            Sprite sprite = BakeReferenceSprite(key, pixels, size, size, Vector4.zero);
            referenceSprites[key] = sprite;
            return sprite;
        }

        /// <summary>Soft top-down gradient used to lift the approved lobby golden.</summary>
        private Sprite GradientVeilSprite()
        {
            const string key = "RefGradientVeil";
            if (referenceSprites.TryGetValue(key, out Sprite cached) && cached != null) return cached;

            // Aspect must match the full-screen display rect (720:1536 ≈ 0.469):
            // the lobby regression flags any visible image stretched over 2%.
            const int width = 64;
            const int height = 136;
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                float t = (y + .5f) / height; // 0 bottom → 1 top
                byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(t) * 255f);
                for (int x = 0; x < width; x++)
                    pixels[y * width + x] = new Color32(255, 255, 255, alpha);
            }
            Sprite sprite = BakeReferenceSprite(key, pixels, width, height, new Vector4(4, 4, 4, 4));
            referenceSprites[key] = sprite;
            return sprite;
        }

        private static Sprite BakeReferenceSprite(string name, Color32[] pixels, int width, int height,
            Vector4 border)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = name + "-Texture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, width, height),
                new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect, border);
            sprite.name = name;
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }

        // ------------------------------------------------------------------
        // shared reference builders

        /// <summary>Slashed glass panel. raycastTarget stays off; buttons opt in.</summary>
        private GameObject RefPanel(string name, Transform parent, float x, float y, float w, float h,
            Color fill, Color edge, bool mirror = false)
        {
            GameObject panel = NewPanel(name, parent, fill, 8);
            Image image = panel.GetComponent<Image>();
            image.sprite = SlashPanelSprite(mirror);
            image.type = Image.Type.Sliced;
            PlaceTop(panel.GetComponent<RectTransform>(), x, y, w, h);
            if (edge.a > .01f)
            {
                Outline outline = panel.AddComponent<Outline>();
                outline.effectColor = edge;
                outline.effectDistance = new Vector2(1f, -1f);
            }
            ReferencePunkFx.CornerCuts(panel.GetComponent<RectTransform>(), edge, 18f, 2f, 3f);
            return panel;
        }

        /// <summary>Slashed button with the same press feedback as the shared NewButton.</summary>
        private GameObject RefButton(string name, Transform parent, string label, int fontSize,
            float x, float y, float w, float h, Color fill, Color foreground,
            UnityEngine.Events.UnityAction action, bool mirror = false, Sprite glyph = null)
        {
            GameObject panel = RefPanel(name, parent, x, y, w, h, fill,
                new Color(foreground.r, foreground.g, foreground.b, .55f), mirror);
            Image image = panel.GetComponent<Image>();
            image.raycastTarget = true;
            Button button = panel.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.16f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.92f, 1f);
            colors.selectedColor = Color.white;
            button.colors = colors;
            button.onClick.AddListener(() =>
            {
                action?.Invoke();
                ResumeMediaAfterUserGesture();
            });

            if (glyph != null)
            {
                GameObject icon = NewImage("Glyph", panel.transform, glyph, foreground);
                PlaceTop(icon.GetComponent<RectTransform>(), 10f, (h - 24f) * .5f, 24f, 24f);
                icon.GetComponent<Image>().preserveAspect = true;
            }
            Text text = NewText("Label", panel.transform, label, fontSize, foreground,
                FontStyle.Bold, TextAnchor.MiddleCenter);
            float textLeft = glyph != null ? 34f : 6f;
            Stretch(text.rectTransform, textLeft, 4, -6, -4);
            text.raycastTarget = false;
            return panel;
        }

        /// <summary>Jagged title block used by the reference screens.</summary>
        private void ReferenceTitle(string eyebrow, string title, string subtitle)
        {
            GameObject tag = RefPanel("ScreenEyebrow", contentRoot, 20, 10, 96, 26,
                new Color32(196, 44, 128, 220), new Color32(255, 146, 222, 160));
            tag.GetComponent<Image>().raycastTarget = false;
            NewPlacedText(tag.transform, eyebrow, 13, White, 4, 1, 88, 24,
                TextAnchor.MiddleCenter, FontStyle.Bold).name = "ScreenEyebrowText";

            Text heading = NewPlacedText(contentRoot, title, 40, White, 20, 38, 460, 56,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            heading.name = "ScreenTitle";
            Outline headingEdge = heading.gameObject.AddComponent<Outline>();
            headingEdge.effectColor = new Color32(255, 92, 208, 150);
            headingEdge.effectDistance = new Vector2(1.4f, -1.4f);
            ReferencePunkFx.Slash(contentRoot, "ScreenTitleSlashA", 212, 88, 34, 5f, -32f,
                new Color32(255, 120, 220, 190));
            ReferencePunkFx.Slash(contentRoot, "ScreenTitleSlashB", 224, 92, 22, 3f, -32f,
                new Color32(120, 226, 255, 170));

            Text sub = NewPlacedText(contentRoot, subtitle, 14, Muted, 22, 96, 560, 26,
                TextAnchor.MiddleLeft);
            sub.name = "ScreenSubtitle";
        }

        // ------------------------------------------------------------------
        // team — central stellar orbit stage

        private void BuildTeamReference() => BuildTeamBoard038();

        /// <summary>
        /// One orbit slot on the stellar stage. The hit rect stays unrotated so the
        /// layout tests keep exact bounds; the slant lives in the nameplate sprite
        /// and tilted label plate children.
        /// </summary>
        private void TeamOrbitSlotReference(int slot, int memberIndex, Vector2 feet,
            float portraitHeight, bool isLeader)
        {
            bool hasMember = memberIndex >= 0 && memberIndex < GameModel.Members.Length;
            float portraitWidth = portraitHeight * .62f;
            const float plateHeight = 62f;
            const float plateWidth = 196f;

            GameObject orbit = NewPanel($"TeamOrbit-{slot}", contentRoot, new Color32(8, 15, 50, 8), 8);
            float orbitWidth = Mathf.Max(plateWidth + 12f, portraitWidth + 24f);
            PlaceTop(orbit.GetComponent<RectTransform>(), feet.x - orbitWidth * .5f,
                feet.y - portraitHeight - 8f, orbitWidth, portraitHeight + plateHeight + 14f);
            Image orbitImage = orbit.GetComponent<Image>();
            orbitImage.raycastTarget = true;
            Button orbitButton = orbit.AddComponent<Button>();
            orbitButton.targetGraphic = orbitImage;
            orbitButton.onClick.AddListener(() =>
            {
                if (hasMember) OpenTeamMember(memberIndex, slot);
                else OpenTeamSlotPicker(Math.Min(slot, model.Save.Team.Count));
                ResumeMediaAfterUserGesture();
            });
            AttachLobbyHotspotFeedback(orbit, LobbyHotspotFeedback.VisualKind.Entry);

            if (!hasMember)
            {
                NewPlacedText(orbit.transform, "+", 40, new Color32(140, 224, 255, 210),
                    0, portraitHeight * .34f, orbitWidth, 52, TextAnchor.MiddleCenter, FontStyle.Bold);
                NewPlacedText(orbit.transform, "添加成员", 14, Muted,
                    0, portraitHeight + plateHeight - 18f, orbitWidth, 24,
                    TextAnchor.MiddleCenter, FontStyle.Bold);
                return;
            }

            MemberDefinition member = GameModel.Members[memberIndex];
            GameObject glow = NewImage($"TeamOrbitGlow-{slot}", orbit.transform, ReferencePunkFx.Glow(),
                new Color32(150, 120, 255, 80));
            PlaceTop(glow.GetComponent<RectTransform>(),
                (orbitWidth - portraitWidth * 1.3f) * .5f, 4f, portraitWidth * 1.3f, portraitHeight * .92f);

            GameObject character = NewImage($"TeamCharacter-{slot}", orbit.transform,
                Resources.Load<Sprite>(member.ResourcePath), White);
            PlaceTop(character.GetComponent<RectTransform>(),
                (orbitWidth - portraitWidth) * .5f, 6f, portraitWidth, portraitHeight - 12f);
            Image characterImage = character.GetComponent<Image>();
            characterImage.preserveAspect = true;
            characterImage.useSpriteMesh = true;
            characterImage.raycastTarget = false;

            if (isLeader)
            {
                GameObject ribbon = NewImage("TeamLeaderRibbon", orbit.transform, RibbonSprite(),
                    new Color32(255, 84, 170, 240));
                PlaceTop(ribbon.GetComponent<RectTransform>(),
                    (orbitWidth - 88f) * .5f, 0f, 88f, 24f);
                ReferencePunkFx.Tilt(ribbon.GetComponent<RectTransform>(), -7f);
                Text leader = NewPlacedText(orbit.transform, "队长", 13,
                    new Color32(255, 240, 250, 255),
                    (orbitWidth - 88f) * .5f, 1f, 88f, 22f, TextAnchor.MiddleCenter, FontStyle.Bold);
                leader.name = "TeamLeader";
                ReferencePunkFx.Tilt(leader.rectTransform, -7f);
            }

            GameObject tag = NewPanel($"TeamLabel-{slot}", orbit.transform, new Color32(6, 13, 44, 218), 8);
            tag.GetComponent<Image>().sprite = SlashPanelSprite(slot % 2 == 1);
            tag.GetComponent<Image>().type = Image.Type.Sliced;
            tag.GetComponent<Image>().raycastTarget = false;
            RectTransform tagRect = tag.GetComponent<RectTransform>();
            PlaceTop(tagRect, (orbitWidth - plateWidth) * .5f, portraitHeight - 34f,
                plateWidth, plateHeight);
            ReferencePunkFx.Tilt(tagRect, slot % 2 == 0 ? -3.5f : 3.5f);
            Outline tagEdge = tag.AddComponent<Outline>();
            tagEdge.effectColor = isLeader ? new Color32(255, 130, 214, 160) : new Color32(110, 200, 255, 130);
            tagEdge.effectDistance = new Vector2(1f, -1f);

            NewPlacedText(tag.transform, member.Name, isLeader ? 19 : 17, White,
                14, 3, plateWidth - 28f, 24, TextAnchor.MiddleLeft, FontStyle.Bold);
            NewPlacedText(tag.transform, $"{MemberRaceFamily(member, memberIndex)} · {member.Career} · 等级{model.LevelOf(memberIndex)}",
                10, new Color32(255, 160, 222, 255), 14, 26, plateWidth - 28f, 17,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            NewPlacedText(tag.transform, $"战力 {model.PowerOf(memberIndex):N0}", 11,
                new Color32(102, 221, 255, 255), 14, 42, plateWidth - 28f, 17,
                TextAnchor.MiddleLeft, FontStyle.Bold);
        }

        // ------------------------------------------------------------------
        // members — slanted roster grid

        private void BuildMembersReference() => BuildMembersBoard038();

        private void MemberFilterButtonReference(string name, string label, int x, int y, int width,
            UnityEngine.Events.UnityAction action, bool mirror = false, bool selected = false)
        {
            GameObject button = RefButton(name, contentRoot, label, 14, x, y, width, 42,
                selected ? new Color32(150, 62, 160, 190) : new Color32(18, 20, 62, 150),
                selected ? White : Muted, action, mirror);
            Outline outline = button.GetComponent<Outline>() ?? button.AddComponent<Outline>();
            outline.effectColor = selected ? new Color32(255, 109, 212, 220) : new Color32(113, 174, 255, 110);
            outline.effectDistance = new Vector2(1f, -1f);
        }

        private void BuildMemberSearchBoxReference()
        {
            GameObject box = RefPanel("MemberSearch", contentRoot, 20, 178, 680, 44,
                new Color32(12, 17, 58, 150), new Color32(113, 174, 255, 120));
            box.GetComponent<Image>().raycastTarget = true;

            GameObject icon = NewImage("SearchIcon", box.transform, SearchIconSprite(),
                new Color32(160, 200, 255, 220));
            PlaceTop(icon.GetComponent<RectTransform>(), 16, 11, 22, 22);
            GameObject wave = NewImage("SearchWaveform", box.transform, WaveformSprite(),
                new Color32(255, 140, 220, 80));
            PlaceTop(wave.GetComponent<RectTransform>(), 540, 12, 120, 20);

            Text placeholder = NewText("Placeholder", box.transform, "搜索成员名称，输入后按回车", 14, Muted,
                FontStyle.Normal, TextAnchor.MiddleLeft);
            Stretch(placeholder.rectTransform, 48, 4, -18, -4);
            Text value = NewText("Value", box.transform, memberSearchQuery, 15, White,
                FontStyle.Normal, TextAnchor.MiddleLeft);
            Stretch(value.rectTransform, 48, 4, -18, -4);

            InputField input = box.AddComponent<InputField>();
            input.targetGraphic = box.GetComponent<Image>();
            input.textComponent = value;
            input.placeholder = placeholder;
            input.lineType = InputField.LineType.SingleLine;
            input.characterLimit = 12;
            input.text = memberSearchQuery;
            input.onEndEdit.AddListener(query =>
            {
                string normalized = (query ?? string.Empty).Trim();
                if (normalized == memberSearchQuery) return;
                memberSearchQuery = normalized;
                memberPageIndex = 0;
                ShowScreen("members");
            });
        }

        /// <summary>Slanted roster card. Rect stays axis-aligned for the layout tests;
        /// the slant lives in the panel sprite and the tilted status ribbon.</summary>
        private void MemberGridCardReference(int index, int x, int y, int width, int height)
        {
            MemberDefinition member = GameModel.Members[index];
            bool unlocked = model.IsUnlocked(index);
            Color cardColor = unlocked ? new Color32(25, 24, 78, 78) : new Color32(12, 17, 49, 46);
            GameObject card = NewPanel($"Member-{member.Id}", contentRoot, cardColor, 8);
            card.GetComponent<Image>().sprite = SlashPanelSprite(index % 2 == 1);
            card.GetComponent<Image>().type = Image.Type.Sliced;
            PlaceTop(card.GetComponent<RectTransform>(), x, y, width, height);
            Button button = card.AddComponent<Button>();
            button.targetGraphic = card.GetComponent<Image>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1.14f, 1.14f, 1.2f, 1f);
            colors.pressedColor = new Color(0.8f, 0.82f, 0.95f, 1f);
            button.colors = colors;
            button.onClick.AddListener(() =>
            {
                // 未签约成员也属于可浏览的图鉴内容；拥有状态只限制培养与编队操作。
                OpenMember(index);
                ResumeMediaAfterUserGesture();
            });
            AttachLobbyHotspotFeedback(card, LobbyHotspotFeedback.VisualKind.Entry);

            // 未获得角色只给剪影与剩余进度：不加载真实形象，也不出现姓名/职业/种族/等级。
            if (!MemberRosterVisibility.ShowsRealPortrait(unlocked))
            {
                BuildLockedMemberCard(card, width, height);
                return;
            }

            Color glowColor = member.Career == "主唱"
                ? new Color32(80, 224, 255, 76)
                : member.Career == "主舞"
                    ? new Color32(181, 111, 255, 70)
                    : new Color32(255, 119, 202, 66);
            GameObject glow = NewImage("CareerGlow", card.transform, StageGlowSprite(), glowColor);
            PlaceTop(glow.GetComponent<RectTransform>(), 3, 3, width - 6, 142);

            GameObject portraitFrame = NewImage("PortraitFrame", card.transform, null, Color.clear);
            PlaceTop(portraitFrame.GetComponent<RectTransform>(), 4, 4, width - 8, 140);
            portraitFrame.AddComponent<RectMask2D>();
            GameObject portrait = NewImage("Portrait", portraitFrame.transform,
                Resources.Load<Sprite>(member.ThumbnailResourcePath), White);
            PanelKit.FrameBustPortrait(portrait.GetComponent<Image>(),
                portraitFrame.GetComponent<RectTransform>(), member.Id);

            // Slanted status ribbon over the portrait top.
            bool deployed = model.IsInTeam(index);
            GameObject ribbon = NewImage("DeploymentBadge", card.transform, RibbonSprite(),
                deployed ? new Color32(255, 88, 170, 235) : new Color32(38, 46, 92, 235));
            PlaceTop(ribbon.GetComponent<RectTransform>(), -4, 10, 78, 22);
            ReferencePunkFx.Tilt(ribbon.GetComponent<RectTransform>(), -8f);
            ribbon.GetComponent<Image>().raycastTarget = false;
            Text label = NewPlacedText(ribbon.transform, MemberDeploymentLabel(index), 11,
                model.Save.Team.IndexOf(index) == 0 ? new Color32(255, 246, 200, 255) : White,
                0, 0, 78, 22, TextAnchor.MiddleCenter, FontStyle.Bold);
            label.name = "DeploymentLabel";
            label.raycastTarget = false;

            // Bottom info band: taxonomy pink, name white, level cyan — reference order.
            GameObject band = NewImage("InfoBand", card.transform, SlashPanelSprite(false),
                new Color32(8, 12, 44, 205));
            band.GetComponent<Image>().type = Image.Type.Sliced;
            band.GetComponent<Image>().raycastTarget = false;
            PlaceTop(band.GetComponent<RectTransform>(), 5, 148, width - 10, 57);
            NewPlacedText(card.transform, $"{MemberRaceFamily(member, index)} · {member.Career}", 10, Pink,
                10, 150, width - 20, 18, TextAnchor.MiddleLeft, FontStyle.Bold);
            NewPlacedText(card.transform, member.Name, 16, White,
                10, 166, width - 20, 24, TextAnchor.MiddleLeft, FontStyle.Bold);
            NewPlacedText(card.transform, $"等级 {model.LevelOf(index)}", 11, Cyan,
                10, 188, width - 20, 17, TextAnchor.MiddleLeft, FontStyle.Bold);

            Outline edge = card.AddComponent<Outline>();
            edge.effectColor = new Color32(140, 190, 255, 130);
            edge.effectDistance = new Vector2(1f, -1f);
        }

        // ------------------------------------------------------------------
        // member dossier — reference profile modal

        private void OpenTeamMemberReference(int memberIndex, int teamSlot)
        {
            Sprite authoredProfile = ReferenceArt038.Load("Art/Reference038/profile-board-038");
            if (authoredProfile != null && model.IsUnlocked(memberIndex))
            {
                OpenProfileBoard038(memberIndex, teamSlot, authoredProfile);
                return;
            }
            memberProfileReturn = null;
            CloseModal();
            MemberDefinition member = GameModel.Members[memberIndex];
            bool unlocked = model.IsUnlocked(memberIndex);
            int level = model.LevelOf(memberIndex);
            bool canTrain = model.CanTrain(memberIndex, out int trainingCost, out _);
            bool atLevelCap = level >= GameModel.MaxMemberLevel;
            int displayPower = model.PowerOf(memberIndex);
            // 与 GachaPanel 同一数据源：GameModel.StageStats → 声波/说唱/舞蹈/名气/颜值。
            GameModel.StageStats(member, memberIndex, model.SigningChannelOf(memberIndex) == 0 ? 0 : 1, level, out int vocal, out int rhythm,
                out int presence, out int resonance, out int charm);
            MemberSkillCopy(member, out string firstSkillName, out string firstSkillEffect,
                out string secondSkillName, out string secondSkillEffect);
            MemberNormalAttackCopy(member, out string normalAttackName, out string normalAttackEffect);

            GameObject overlay = NewImage("MemberModal", safeRoot, null, new Color32(3, 4, 20, 220));
            Stretch(overlay.GetComponent<RectTransform>());
            overlay.GetComponent<Image>().raycastTarget = true;
            modalObject = overlay;

            GameObject panel = NewPanel("Panel", overlay.transform, new Color32(12, 18, 40, 253), 28);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(620, 1250);
            AddQuietPanelEdge(panel);
            ReferencePunkFx.CornerCuts(panelRect, new Color32(255, 110, 214, 160), 34f, 3f, 8f);

            GameObject closeTop = NewButton("CloseTop", panel.transform, "×", 28, Color.clear, White, () =>
                { Action back = memberProfileReturn; memberProfileReturn = null; CloseModal(); back?.Invoke(); });
            PlaceTop(closeTop.GetComponent<RectTransform>(), 556, 14, 50, 50);

            const float infoX = 324f;
            const float infoW = 272f;
            if (MemberRosterVisibility.ShowsRealPortrait(unlocked))
            {
                GameObject frame = NewImage("PortraitFrame", panel.transform, SlashPanelSprite(false),
                    new Color32(16, 20, 52, 240));
                frame.GetComponent<Image>().type = Image.Type.Sliced;
                PlaceTop(frame.GetComponent<RectTransform>(), 20, 54, 280, 384);
                ReferencePunkFx.Tilt(frame.GetComponent<RectTransform>(), -2.5f);
                Mask portraitMask = frame.AddComponent<Mask>();
                portraitMask.showMaskGraphic = true;
                Outline portraitEdge = frame.AddComponent<Outline>();
                portraitEdge.effectColor = new Color32(255, 130, 220, 150);
                portraitEdge.effectDistance = new Vector2(1.5f, -1.5f);
                GameObject portrait = NewImage("Portrait", frame.transform,
                    Resources.Load<Sprite>(member.ResourcePath), White);
                PanelKit.FrameBustPortrait(portrait.GetComponent<Image>(),
                    frame.GetComponent<RectTransform>(), member.Id);

                GameObject tag = NewImage("MemberCareerTag", panel.transform, RibbonSprite(),
                    new Color32(30, 20, 68, 245));
                PlaceTop(tag.GetComponent<RectTransform>(), 40, 396, 158, 30);
                ReferencePunkFx.Tilt(tag.GetComponent<RectTransform>(), -6f);
                Outline tagEdge = tag.AddComponent<Outline>();
                tagEdge.effectColor = new Color32(255, 120, 210, 170);
                tagEdge.effectDistance = new Vector2(1f, -1f);
                Text tagText = NewPlacedText(tag.transform, $"{member.Career} · 等级 {level}", 14, White,
                    0, 2, 158, 26, TextAnchor.MiddleCenter, FontStyle.Bold);
                tagText.name = "MemberCareerTagText";
                ReferencePunkFx.Tilt(tagText.rectTransform, -6f);

                GameObject wave = NewImage("ProfileWaveform", panel.transform, WaveformSprite(),
                    new Color32(120, 226, 255, 140));
                PlaceTop(wave.GetComponent<RectTransform>(), 56, 430, 160, 22);
            }
            else
            {
                BuildLockedMemberPortrait(panel);
            }

            Text ownership = NewPlacedText(panel.transform, unlocked ? "已签约成员" : "尚未签约", 14,
                unlocked ? new Color32(111, 255, 194, 255) : new Color32(255, 185, 218, 255),
                infoX, 56, infoW, 26, TextAnchor.MiddleLeft, FontStyle.Bold);
            ownership.name = "MemberOwnershipStatus";
            Text memberName = NewPlacedText(panel.transform,
                unlocked ? member.Name : MemberRosterVisibility.LockedName, 38,
                unlocked ? White : new Color32(198, 192, 226, 255),
                infoX, 84, infoW, 54, TextAnchor.MiddleLeft, FontStyle.Bold);
            memberName.name = "MemberName";
            memberName.verticalOverflow = VerticalWrapMode.Overflow;
            Outline nameEdge = memberName.gameObject.AddComponent<Outline>();
            nameEdge.effectColor = new Color32(255, 96, 208, 130);
            nameEdge.effectDistance = new Vector2(1.4f, -1.4f);
            NewPlacedText(panel.transform,
                unlocked ? $"{MemberRace(member, memberIndex)} · {member.Career}" : MemberRosterVisibility.LockedCareer,
                16, unlocked ? Pink : Muted,
                infoX, 140, infoW, 26, TextAnchor.MiddleLeft, FontStyle.Bold).name = "MemberRaceCareer";
            Text power = NewPlacedText(panel.transform,
                unlocked ? $"等级 {level}  ·  战力 {displayPower:N0}"
                    : MemberRosterVisibility.LockedProfileProgress(model.Save.UnlockedMembers.Count,
                        GameModel.Members.Length),
                15, Cyan, infoX, 168, infoW, 26, TextAnchor.MiddleLeft, FontStyle.Bold);
            power.name = "MemberPower";

            if (unlocked)
            {
                // Level strip + progress bar against the real level cap.
                NewPlacedText(panel.transform, $"等级 {level} / {GameModel.MaxMemberLevel}", 13, Muted,
                    infoX, 200, infoW, 20, TextAnchor.MiddleLeft, FontStyle.Bold).name = "MemberLevelLabel";
                GameObject track = NewPanel("MemberLevelTrack", panel.transform,
                    new Color32(40, 44, 96, 240), 6);
                PlaceTop(track.GetComponent<RectTransform>(), infoX, 224, infoW, 10);
                track.GetComponent<Image>().raycastTarget = false;
                GameObject fill = NewPanel("MemberLevelFill", track.transform,
                    new Color32(255, 110, 210, 255), 6);
                RectTransform fillRect = fill.GetComponent<RectTransform>();
                fillRect.anchorMin = Vector2.zero;
                fillRect.anchorMax = new Vector2(0f, 1f);
                fillRect.offsetMin = Vector2.zero;
                fillRect.offsetMax = Vector2.zero;
                fillRect.anchorMax = new Vector2(
                    Mathf.Clamp01(level / (float)GameModel.MaxMemberLevel), 1f);
                fill.GetComponent<Image>().raycastTarget = false;
            }

            // 未获得角色到此为止：不创建属性/技能/队长面板，避免未公开数据出现在界面树里。
            if (!MemberRosterVisibility.ShowsCombatStats(unlocked))
            {
                BuildLockedMemberProfileBody(panel, memberIndex);
                return;
            }

            // 舞台五维：与选秀候选卡共用 GameModel.StageStats，标签与之一致。
            GameObject statPanel = RefPanel("MemberStatPanel", panel.transform, infoX, 246, infoW, 250,
                new Color32(12, 23, 67, 222), new Color32(120, 210, 255, 120));
            statPanel.GetComponent<Image>().raycastTarget = false;
            Text statTitle = NewPlacedText(statPanel.transform, "舞台五维", 15,
                new Color32(255, 183, 229, 255), 14, 8, 150, 24, TextAnchor.MiddleLeft, FontStyle.Bold);
            statTitle.name = "MemberSectionBaseStats";
            NewPlacedText(statPanel.transform, "实时数据", 9, new Color32(150, 145, 200, 200),
                150, 12, 110, 18, TextAnchor.MiddleRight, FontStyle.Italic).name = "MemberSectionBaseStatsEn";
            ReferenceStatRow(statPanel.transform, "MemberStatVocal", "声波", vocal.ToString(), 0);
            ReferenceStatRow(statPanel.transform, "MemberStatRhythm", "说唱", rhythm.ToString(), 1);
            ReferenceStatRow(statPanel.transform, "MemberStatPresence", "舞蹈", presence.ToString(), 2);
            ReferenceStatRow(statPanel.transform, "MemberStatResonance", "名气", resonance.ToString(), 3);
            ReferenceStatRow(statPanel.transform, "MemberStatCharm", "颜值", charm.ToString(), 4);
            ReferenceStatRow(statPanel.transform, "MemberStatAffection", "好感度",
                $"{model.AffectionOf(memberIndex)} · {model.AffectionTierOf(memberIndex)}", 5, true);

            // Stylised stage-trait line between the portrait and the skill section.
            Text trait = NewPlacedText(panel.transform,
                $"「 舞台特质 · {MemberStageQualities.DominantTrait(vocal, rhythm, presence, resonance)} 」",
                17, new Color32(255, 160, 226, 255), 24, 500, 572, 32,
                TextAnchor.MiddleLeft, FontStyle.Bold | FontStyle.Italic);
            trait.name = "MemberStageTrait";

            GameObject skillPanel = RefPanel("MemberSkillPanel", panel.transform, 24, 540, 572, 296,
                new Color32(18, 22, 70, 228), new Color32(255, 130, 215, 110));
            skillPanel.GetComponent<Image>().raycastTarget = false;
            Text normalTitle = NewPlacedText(skillPanel.transform, MemberProfileSections.NormalAttackTitle, 15,
                new Color32(255, 184, 230, 255), 18, 10, 200, 24, TextAnchor.MiddleLeft, FontStyle.Bold);
            normalTitle.name = "MemberSectionNormalAttack";
            Text normalCopy = NewPlacedText(skillPanel.transform,
                $"{normalAttackName} · {normalAttackEffect}", 14, White,
                18, 36, 536, 24, TextAnchor.MiddleLeft, FontStyle.Bold);
            normalCopy.name = "MemberNormalAttack";
            PanelKit.EnableBestFit(normalCopy, 11);
            Text activeTitle = NewPlacedText(skillPanel.transform, MemberProfileSections.ActiveSkillsTitle, 15,
                new Color32(255, 184, 230, 255), 18, 66, 200, 24, TextAnchor.MiddleLeft, FontStyle.Bold);
            activeTitle.name = "MemberSectionActiveSkills";
            NewPlacedText(skillPanel.transform, "技能效果", 9, new Color32(150, 145, 200, 200),
                424, 70, 130, 18, TextAnchor.MiddleRight, FontStyle.Italic).name = "MemberSectionActiveSkillsEn";
            BuildReadableMemberSkill(skillPanel.transform, "MemberSkillPrimary", firstSkillName, firstSkillEffect,
                18, Pink, 96, 156);
            BuildReadableMemberSkill(skillPanel.transform, "MemberSkillSecondary", secondSkillName, secondSkillEffect,
                296, Cyan, 96, 156);
            NewPlacedText(skillPanel.transform, MemberTeamBonus(member), 12,
                new Color32(110, 225, 255, 255), 18, 258, 536, 26, TextAnchor.MiddleLeft, FontStyle.Bold);

            GameObject guidePanel = RefPanel("MemberAcquireGuide", panel.transform, 24, 848, 572, 100,
                new Color32(20, 26, 73, 226), new Color32(255, 190, 140, 110));
            guidePanel.GetComponent<Image>().raycastTarget = false;
            NewPlacedText(guidePanel.transform, "本次培养", 14,
                new Color32(255, 188, 231, 255), 18, 8, 200, 22, TextAnchor.MiddleLeft, FontStyle.Bold);
            Text preview = NewPlacedText(guidePanel.transform, atLevelCap
                    ? $"等级 {level} · 已达到当前等级上限"
                    : $"等级 {level} → {level + 1} · 仅提升当前成员",
                15, White, 18, 32, 536, 26, TextAnchor.MiddleLeft);
            preview.name = "MemberTrainingPreview";
            PanelKit.EnableBestFit(preview, 13);
            Text cost = NewPlacedText(guidePanel.transform, atLevelCap
                    ? "已满级 · 无需继续训练"
                    : $"消耗星光币 {trainingCost:N0} · 持有 {model.Save.Gold:N0}",
                15, canTrain || atLevelCap ? Cyan : Pink, 18, 60, 536, 26, TextAnchor.MiddleLeft);
            cost.name = "MemberTrainingCost";
            PanelKit.EnableBestFit(cost, 13);

            GameObject captainPanel = RefPanel("MemberCaptainPanel", panel.transform, 24, 960, 572, 110,
                new Color32(26, 33, 65, 240), new Color32(120, 214, 255, 110));
            captainPanel.GetComponent<Image>().raycastTarget = false;
            FlowText(captainPanel.transform, "CaptainEffectTitle", MemberProfileSections.PassiveCaptainTitle, 15, 16, 8, 320, 24, Cyan);
            FlowText(captainPanel.transform, "CaptainEffectDescription", CaptainEffectCopy(member.Race), 13, 16, 36, 330, 62, Muted);
            bool isCaptain = model.Save.Team.Count > 0 && model.Save.Team[0] == memberIndex;
            FlowButton(captainPanel.transform, "AppointCaptain", isCaptain ? "当前队长" : model.IsInTeam(memberIndex) ? "设为队长" : "上阵并任命", 362, 30, 196, 56,
                () => ConfirmCaptain(memberIndex)).GetComponent<Button>().interactable = !isCaptain;

            // Bottom action row: four slanted action plates like the reference.
            GameObject train = RefButton("Train", panel.transform,
                atLevelCap ? "已满级" : canTrain ? "练习升级" : "星光币不足", 16,
                24, 1084, 136, 58, new Color32(214, 70, 140, 240), White, () =>
            {
                // 只进入练习反馈面板；真正的升级仍调用既有 model.Train（只扣星光币）。
                if (!canTrain) return;
                OpenTrainingPractice(memberIndex, teamSlot);
            });
            train.GetComponent<Button>().interactable = canTrain;

            RefButton("Team", panel.transform,
                teamSlot >= 0 ? "更换成员" : model.IsInTeam(memberIndex) ? "移出编队" : "加入 / 替换", 15,
                168, 1084, 136, 58, new Color32(122, 74, 190, 240), White, () =>
            {
                if (teamSlot >= 0) { OpenTeamSlotPicker(teamSlot); return; }
                if (!model.IsInTeam(memberIndex) && model.Save.Team.Count >= GameModel.TeamCapacity)
                { OpenTeamReplacement(memberIndex); return; }
                model.ToggleTeamMember(memberIndex, out string message);
                Toast(message);
                CloseModal();
                ShowScreen(currentScreen);
            }, true);

            RefButton("MemberEquipment", panel.transform, "角色饰品", 15,
                312, 1084, 136, 58, new Color32(122, 74, 190, 240), White, () =>
            { equipmentMember = memberIndex; selectedAccessoryIndex = Math.Max(0, model.EquippedAccessoryFor(memberIndex)); ShowScreen("accessory"); });

            // v0.3.4 深入交流：羁绊档专属入口。锁定/已用仍可点击，用 Toast 说明原因。
            bool deepUnlocked = model.DeepTalkUnlocked(memberIndex);
            bool deepUsed = model.DeepTalkUsedToday(memberIndex);
            string deepLabel = deepUsed ? "今日已交流"
                : deepUnlocked ? "深入交流"
                : $"好感度{GameModel.DeepTalkAffectionUnlock}解锁";
            Color deepBackground = deepUnlocked && !deepUsed
                ? new Color32(201, 92, 148, 240)
                : new Color32(63, 57, 108, 235);
            GameObject deepTalk = RefButton("MemberDeepTalkButton", panel.transform, deepLabel, 12,
                456, 1084, 136, 58, deepBackground, White, () =>
            {
                int affectionBefore = model.AffectionOf(memberIndex);
                if (!model.DeepTalk(memberIndex, out string message, out string dialogue))
                {
                    Toast(message);
                    return;
                }
                OpenDeepTalkResult(memberIndex, teamSlot, dialogue, affectionBefore, model.AffectionOf(memberIndex));
            }, true);
            PanelKit.EnableBestFit(PanelKit.LabelOf(deepTalk), 10);
        }

        /// <summary>One stage-stat row inside 舞台五维: label left, value right.</summary>
        private void ReferenceStatRow(Transform parent, string name, string label, string value,
            int row, bool affection = false)
        {
            float y = 38f + row * 33f;
            if (affection)
            {
                GameObject heart = NewImage(name + "Heart", parent, HeartSprite(),
                    new Color32(255, 92, 150, 235));
                PlaceTop(heart.GetComponent<RectTransform>(), 12, y + 1, 20, 20);
            }
            Text labelText = NewPlacedText(parent, label, 14, affection ? new Color32(255, 170, 205, 255) : Muted,
                affection ? 38 : 14, y, 84, 28, TextAnchor.MiddleLeft, FontStyle.Bold);
            labelText.name = name + "Label";
            Text valueText = NewPlacedText(parent, value, 15, White,
                100, y, 158, 28, TextAnchor.MiddleRight, FontStyle.Bold);
            valueText.name = name;
            if (affection) PanelKit.EnableBestFit(valueText, 11);
        }
    }
}
