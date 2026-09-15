using System;
using System.Linq;
using ChoSiren.Panels;
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

        private void ConfigureReferenceHeader038(bool active)
        {
            Transform bar = safeRoot.Find("TopBar");
            if (bar == null) return;
            foreach (Transform node in bar.GetComponentsInChildren<Transform>(true))
            {
                if (node.name.EndsWith("VisualV2") || node.name == "AvatarMask" ||
                    node.name == "ReferenceHudIcon" || node.name == "ReferencePlus")
                    node.gameObject.SetActive(false);
            }
            // Reuse the approved header strip as an authored UI panel. Its icon/frame
            // pixels and spacing are identical on every page; only values are live.
            Transform visual = bar.Find("ReferenceHeader038");
            if (visual == null)
            {
                Sprite source = ReferenceArt038.Load("Art/Reference038/lobby-board-038");
                if (source != null)
                {
                    Rect sourceRect = source.rect;
                    float height = sourceRect.width * 100f / 720f;
                    Sprite strip = Sprite.Create(source.texture,
                        new Rect(sourceRect.x, sourceRect.yMax - height, sourceRect.width, height),
                        new Vector2(.5f, .5f), source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
                    strip.name = "ReferenceHeaderStrip038";
                    strip.hideFlags = HideFlags.DontSave;
                    GameObject image = NewImage("ReferenceHeader038", bar, strip, White);
                    PlaceTop(image.GetComponent<RectTransform>(), 0, 5.8f, 720, 100);
                    image.GetComponent<Image>().preserveAspect = true;
                    image.GetComponent<Image>().raycastTarget = false;
                    image.AddComponent<UiBottomEdgeFade038>();
                    image.transform.SetAsFirstSibling();
                    visual = image.transform;
                }
            }
            if (visual != null) visual.gameObject.SetActive(currentScreen != "lobby");
            Text name = bar.Find("Profile/PlayerName")?.GetComponent<Text>();
            if (name != null) { PlaceTop(name.rectTransform, 87, 10, 103, 26); name.fontSize = 17; name.fontStyle = FontStyle.Normal; }
            PlaceTop(teamLevelText.rectTransform, 87, 53, 104, 23);
            Transform profile = bar.Find("Profile");
            if (profile.Find("TeamAverageLevel") == null)
                BoardText038(profile, "TeamAverageLevel", "队伍等级 " + TeamAverageLevel, 11, Muted, 87, 35, 105, 18);
            // Numbers sit after the original small icons, with a clear independent + zone.
            PlaceTop(diamondText.rectTransform, 60, 17, 41, 30);
            PlaceTop(goldText.rectTransform, 46, 16, 59, 30);
            PlaceTop(staminaText.rectTransform, 42, 17, 65, 30);
            foreach (Text value in new[] { diamondText, goldText, staminaText })
            {
                value.horizontalOverflow = HorizontalWrapMode.Overflow;
                value.resizeTextForBestFit = true;
                value.resizeTextMinSize = 10;
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
