using System;
using System.Linq;
using ChoSiren.UI;
using UnityEngine;
using UnityEngine.UI;
using ChoSiren.Panels;
using ChoSiren.Systems.Presentation;

namespace ChoSiren
{
    public sealed partial class ChoSirenApp
    {
        /// <summary>
        /// Convex clip window matching the authored slanted card interiors. Used as the Mask
        /// graphic so silhouette/portrait art follows the jagged frame instead of a rectangle.
        /// </summary>
        private sealed class RosterPolish038SlantWindow : MaskableGraphic
        {
            private static readonly Vector2[] Verts =
            {
                new Vector2(.085f, 1f), new Vector2(.965f, 1f), new Vector2(1f, .93f),
                new Vector2(1f, .10f), new Vector2(.915f, 0f), new Vector2(.035f, 0f),
                new Vector2(0f, .045f), new Vector2(0f, .90f),
            };

            protected override void OnPopulateMesh(VertexHelper vh)
            {
                vh.Clear();
                Rect r = rectTransform.rect;
                int first = vh.currentVertCount;
                vh.AddVert(r.center, color, Vector2.zero);
                foreach (Vector2 v in Verts)
                    vh.AddVert(new Vector2(r.xMin + v.x * r.width, r.yMin + v.y * r.height),
                        color, Vector2.zero);
                for (int i = 0; i < Verts.Length; i++)
                    vh.AddTriangle(first, first + 1 + i, first + 1 + (i + 1) % Verts.Length);
            }
        }

        /// <summary>Stencil clip shaped like the card's slanted interior; children get clipped.</summary>
        private GameObject RosterPolish038Clip(Transform parent, string name,
            float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(RosterPolish038SlantWindow), typeof(Mask));
            go.transform.SetParent(parent, false);
            PlaceTop((RectTransform)go.transform, x, y, w, h);
            RosterPolish038SlantWindow window = go.GetComponent<RosterPolish038SlantWindow>();
            window.color = White;
            window.raycastTarget = false;
            go.GetComponent<Mask>().showMaskGraphic = false;
            return go;
        }

        private static void AddQuietPanelEdge(GameObject panel)
        {
            Outline edge = UnityNull.Real(panel.GetComponent<Outline>()) ?? panel.AddComponent<Outline>();
            edge.effectColor = new Color32(122, 160, 224, 90);
            edge.effectDistance = new Vector2(1, -1);
            edge.useGraphicAlpha = true;
        }

        private void BuildReadableMemberSkill(Transform parent, string name, string skill, string description,
            float x, Color accent, float y = 48, float height = 192)
        {
            GameObject card = NewPanel(name + "Card", parent, new Color32(12, 19, 45, 248), 14);
            PlaceTop(card.GetComponent<RectTransform>(), x, y, 258, height);
            AddQuietPanelEdge(card);
            SirenStickerUi.SkinPanel(card, false);
            var icon = SkillIconVisuals.Create(card.transform, name + "Icon", skill, description, accent);
            PlaceTop(icon.rectTransform, 12, 14, 48, 48);
            Text heading = NewPlacedText(card.transform, skill, 17, White, 72, 12, 174, 54,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            heading.name = name;
            PanelKit.EnableBestFit(heading, 14);
            Text body = NewPlacedText(card.transform, description, 15, Muted, 14, 80, 230,
                Mathf.Max(48f, height - 94f), TextAnchor.UpperLeft);
            body.name = name + "Description";
            PanelKit.EnableBestFit(body, 12);
            body.verticalOverflow = VerticalWrapMode.Truncate;
        }

        // ------------------------------------------------------------------
        // RosterPolish038 — authored members board, labels inside tilted chip interiors

        private void BuildRosterPolish038MembersBoard()
        {
            RectTransform root = ReferenceBoard038("MembersReferenceBoard038", "Art/Reference038/members-board-038");
            // 2026-11-08 (少爷: "成员页面的背景也统一改成饰品这样的"): the same full-screen page background as 团队 / 饰品,
            // retaining the measured card/filter layout.
            Sprite shared = Resources.Load<Sprite>("Art/Ui1107/page-bg-board");
            if (shared != null)
            {
                root.GetComponent<Image>().sprite = shared;
                root.GetComponent<Image>().preserveAspect = false;
                CoverPageBackground1108(root, shared);
            }
            Text heading = BoardText038(root, "MembersTitle", "成员", 54, White, 46, 210, 650, 84);
            SirenStickerUi.SkinTitle(heading);
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
            }, 12, index => model.IsUnlocked(index) ? 0 : 1);
            memberPageIndex = page.PageIndex;
            BoardText038(root, "ScreenSubtitle",
                $"已拥有 {model.Save.UnlockedMembers.Count}/{GameModel.Members.Length}",
                18, White, 46, 307, 735, 32);

            // 三枚斜切筛选钮：命中区覆盖整颗钮，文字收在量出的内切安全区中心。
            string[] names = { "MemberRoleFilter", "MemberRaceFilter", "MemberOwnedFilter" };
            string[] labels = { role.Length == 0 ? "全部职业" : role,
                race.Length == 0 ? "全部种族" : race,
                memberOwnedOnly ? "已拥有" : "全部图鉴" };
            Rect[] chips = { new Rect(45, 344, 240, 56), new Rect(307, 344, 225, 56), new Rect(543, 344, 248, 56) };
            for (int i = 0; i < 3; i++)
            {
                int filter = i;
                Rect chip = chips[i];
                GameObject button = BoardButton038(names[i], root, chips[i], () =>
                {
                    // 2026-10-20 (owner): 职业 / 种族 open a list to pick from instead of cycling.
                    if (filter == 0)
                        OpenRosterFilterMenu(root, chip, "职业", roles, memberRoleFilterIndex, pick => memberRoleFilterIndex = pick);
                    else if (filter == 1)
                        OpenRosterFilterMenu(root, chip, "种族", races, memberRaceFilterIndex, pick => memberRaceFilterIndex = pick);
                    else { memberOwnedOnly = !memberOwnedOnly; memberPageIndex = 0; ShowScreen("members"); }
                });
                Text filterLabel = BoardText038(button.transform, "Label", labels[i], 23, White,
                    12f, 9, chip.width - 24f, 38, TextAnchor.MiddleCenter);
                PanelKit.EnableBestFit(filterLabel, 18);
                filterLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
                filterLabel.verticalOverflow = VerticalWrapMode.Truncate;
                SirenPageChrome.Tab(button, i == 0 ? role.Length > 0 : i == 1 ? race.Length > 0 : memberOwnedOnly);
            }

            GameObject search = NewImage("MemberSearch", root, null, Color.clear);
            PlaceTop(search.GetComponent<RectTransform>(), 42, 419, 733, 61);
            search.GetComponent<Image>().raycastTarget = true;
            SirenStickerUi.SkinPanel(search, false);
            Text placeholder = BoardText038(search.transform, "Placeholder", "搜索成员",
                22, Muted, 15, 0, 655, 60);
            Text value = BoardText038(search.transform, "Value", memberSearchQuery, 18, White, 15, 0, 655, 60);
            InputField input = search.AddComponent<InputField>();
            input.targetGraphic = search.GetComponent<Image>();
            input.textComponent = value;
            input.placeholder = placeholder;
            input.characterLimit = 12;
            input.text = memberSearchQuery;
            input.onEndEdit.AddListener(query =>
            {
                string text = (query ?? "").Trim();
                if (text == memberSearchQuery) return;
                memberSearchQuery = text; memberPageIndex = 0; ShowScreen("members");
            });

            // 12 个卡位，4 列 3 行（2026-10-25：5 列时字太小，改成更大的卡）。
            // The board no longer bakes empty frames (2026-10-20): only owned cards draw, evenly spaced.
            float[] rowY = { 500f, 792f, 1084f };
            float[] rowH = { 276f, 276f, 276f };
            for (int slot = 0; slot < page.VisibleCount; slot++)
            {
                int index = page.SourceIndexAt(slot);
                MemberRosterCell cell = MemberRosterPagination.CellFor(slot, 4);
                RosterPolish038MemberCard(root, index,
                    new Rect(32 + cell.Column * 200, rowY[cell.Row], 183, rowH[cell.Row]));
            }
            if (page.IsEmpty)
                BoardText038(root, "MemberEmpty", "没有符合条件的成员，请调整筛选", 23, White,
                    70, 670, 700, 80, TextAnchor.MiddleCenter);

            // 2026-11-07 (赵金: 统一所有左右切换按钮): the shared ‹ › chevrons beside the page counter.
            GameObject previous = SwitchArrow1107.Create(root, "MemberPreviousPage", -1, new Rect(250, 1386, 100, 76), () =>
            { memberPageIndex = MemberRosterPagination.MovePage(memberPageIndex, -1, page.PageCount); ShowScreen("members"); }).gameObject;
            previous.GetComponent<Button>().interactable = page.HasPrevious;
            BoardText038(root, "MemberPageLabel", $"{(page.PageCount == 0 ? 0 : page.PageIndex + 1)}/{page.PageCount}",
                34, White, 360, 1382, 120, 86, TextAnchor.MiddleCenter);
            GameObject next = SwitchArrow1107.Create(root, "MemberNextPage", 1, new Rect(491, 1386, 100, 76), () =>
            { memberPageIndex = MemberRosterPagination.MovePage(memberPageIndex, 1, page.PageCount); ShowScreen("members"); }).gameObject;
            next.GetComponent<Button>().interactable = page.HasNext;
            Image membersBoard = root.GetComponent<Image>();
            // One page (the usual owned roster): no pager at all.
            bool paged = page.PageCount > 1;
            previous.SetActive(paged); next.SetActive(paged);
            root.Find("MemberPageLabel")?.gameObject.SetActive(paged);
        }

        /// <summary>
        /// Independent jagged card chrome, generated per row height
        /// (ContentFrames038). The board art no longer bakes frames, so an empty slot
        /// shows plain background instead of a useless frame.
        /// </summary>
        private void OpenRosterFilterMenu(Transform root, Rect chip, string title, string[] options, int current, Action<int> pick)
        {
            Transform old = root.Find("RosterFilterMenu");
            if (old != null) { Destroy(old.gameObject); return; }
            // Full-screen catcher: a tap outside the list closes it.
            GameObject menu = NewImage("RosterFilterMenu", root, null, new Color(0f, 0f, 0f, .35f));
            Stretch(menu.GetComponent<RectTransform>());
            menu.GetComponent<Image>().raycastTarget = true;
            menu.AddComponent<Button>().onClick.AddListener(() => Destroy(menu));
            const float rowH = 54f;
            float width = Mathf.Max(220f, chip.width);
            GameObject list = NewImage("Options", menu.transform, null, new Color32(10, 6, 20, 245));
            PlaceTop(list.GetComponent<RectTransform>(), chip.x, chip.yMax + 6f, width, options.Length * rowH + 12f);
            list.GetComponent<Image>().raycastTarget = true;
            SirenStickerUi.SkinPanel(list, false);
            for (int i = 0; i < options.Length; i++)
            {
                int index = i;
                bool on = i == current;
                GameObject row = BoardButton038($"Option-{i}", list.transform, new Rect(6f, 6f + i * rowH, width - 12f, rowH - 6f), () =>
                {
                    pick(index); memberPageIndex = 0; ShowScreen("members");
                });
                Image plate = row.GetComponent<Image>();
                plate.sprite = PunkUi.SlantFill;
                // Solid teal = selected (2026-11-03 contract), same as the 饰品 sort drop-down.
                plate.color = on ? (Color)Brand1025.Teal : new Color32(36, 26, 56, 255);
                BoardText038(row.transform, "Label", (on ? "✓ " : string.Empty) + (options[i].Length == 0 ? "全部" + title : options[i]),
                    18, on ? (Color)Brand1025.Ink : White, 0, 0, width - 12f, rowH - 6f, TextAnchor.MiddleCenter);
            }
            list.AddComponent<DropdownPop1108>();
        }

        private static Sprite memberCardFrame;
        private static Sprite ContentFrames038MemberCard(float rowHeight)
        {
            // 2026-10-20: owned cards keep the original black card + neon frame (GPT-redrawn as a
            // standalone transparent sprite, since the old frames were baked into the board).
            if (memberCardFrame == null)
            {
                Texture2D texture = Resources.Load<Texture2D>("UI/MemberCard/member-card-frame");
                if (texture != null)
                    memberCardFrame = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 100f);
            }
            return memberCardFrame;
        }

        /// <summary>One real roster slot: live jagged frame sprite + content.</summary>
        private void RosterPolish038MemberCard(Transform root, int index, Rect rect)
        {
            MemberDefinition m = GameModel.Members[index];
            GameObject card = null;
            // 2026-11-08 (少爷: "切到这个详细角色信息页面的转场"): the race silhouette transition plays from the 成员 page too.
            card = BoardButton038("Member-" + m.Id, root, rect, () =>
                PlayRaceTransition1108(index, card != null ? (RectTransform)card.transform : null, () => OpenMember(index)));
            Image chrome = card.GetComponent<Image>();
            chrome.sprite = ContentFrames038MemberCard(rect.height);
            if (chrome.sprite != null) chrome.color = White;
            if (!model.IsUnlocked(index))
            {
                BuildLockedMemberCard(card, (int)rect.width, (int)rect.height);
                return;
            }

            // Owned: bust art clipped to the same slanted window shape — no rectangular seam.
            GameObject clip = RosterPolish038Clip(card.transform, "PortraitFrame",
                5, 5, rect.width - 10, rect.height - 97);
            // Generic hero-* thumbs are 256 px full bodies; the head crop needs the 512 px portrait to stay sharp.
            string artPath = m.Id.StartsWith("hero-", StringComparison.Ordinal) ? m.ResourcePath : m.ThumbnailResourcePath;
            GameObject art = NewImage("Portrait", clip.transform,
                Resources.Load<Sprite>(artPath) ?? Resources.Load<Sprite>(m.ThumbnailResourcePath), White);
            art.GetComponent<Image>().raycastTarget = false;
            PanelKit.FrameBustPortrait(art.GetComponent<Image>(),
                clip.GetComponent<RectTransform>(), m.Id);

            GameObject ribbon = NewImage("DeploymentBadge", card.transform, RibbonSprite(),
                model.IsInTeam(index) ? new Color32(36, 16, 45, 235) : new Color32(13, 8, 32, 230));
            PlaceTop(ribbon.GetComponent<RectTransform>(), 0, 4, 103, 27);
            ribbon.GetComponent<Image>().raycastTarget = false;
            SirenStickerUi.SkinPanel(ribbon, false);
            BoardText038(ribbon.transform, "DeploymentLabel", MemberDeploymentLabel(index), 16,
                model.Save.Team.IndexOf(index) == 0 ? Accent : new Color32(255, 216, 110, 255),
                0, 0, 103, 27, TextAnchor.MiddleCenter);
            BoardText038(card.transform, "Taxonomy", $"{MemberRaceFamily(m, index)} · {m.Career}",
                16, SirenStickerUi.Muted, 8, rect.height - 94, rect.width - 16, 26);
            BoardText038(card.transform, "MemberName", m.Name, 24, White, 8, rect.height - 68, rect.width - 16, 34);
            BoardText038(card.transform, "MemberLevel", $"等级 {model.LevelOf(index)}",
                17, Cyan, 8, rect.height - 33, rect.width - 16, 24);
        }
    }
}
