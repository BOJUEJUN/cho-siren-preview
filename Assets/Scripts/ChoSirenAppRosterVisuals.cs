using System;
using System.Linq;
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
            Outline edge = panel.GetComponent<Outline>() ?? panel.AddComponent<Outline>();
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
        // RosterPolish038 — authored team board, live text inside measured safe polygons

        private void BuildRosterPolish038TeamBoard()
        {
            RectTransform root = ReferenceBoard038("TeamReferenceBoard038", "Art/Reference038/team-board-038");
            int count = Math.Min(GameModel.TeamCapacity, model.Save.Team.Count);
            int roles = model.Save.Team.Take(count).Where(i => i >= 0 && i < GameModel.Members.Length)
                .Select(i => GameModel.Members[i].Career).Distinct().Count();
            // 总战力卡：三条互不重叠的安全行，全部离开右下锯齿边。
            BoardText038(root, "TeamPowerValue", model.TeamPower.ToString("N0"), 56, White,
                544, 236, 184, 76, TextAnchor.MiddleCenter);
            BoardText038(root, "TeamResonanceValue", $"职业种类 {roles}/4", 16, Cyan,
                546, 314, 180, 26, TextAnchor.MiddleCenter);
            BoardText038(root, "TeamMemberCount", $"成员 {count}/{GameModel.TeamCapacity}", 15,
                new Color32(255, 148, 214, 255), 554, 342, 164, 24, TextAnchor.MiddleCenter);

            Rect[] portraits = { new Rect(235, 382, 365, 348), new Rect(12, 605, 315, 360),
                new Rect(550, 579, 295, 380), new Rect(210, 806, 390, 305) };
            Rect[] plates = { new Rect(352, 728, 163, 67), new Rect(81, 956, 183, 74),
                new Rect(635, 948, 178, 77), new Rect(334, 1107, 205, 69) };
            Rect[] targets = { new Rect(327, 382, 201, 413), new Rect(45, 605, 220, 425),
                new Rect(620, 579, 193, 446), new Rect(291, 806, 250, 370) };
            // Frame silhouettes in board px: the orbit union rects physically overlap
            // (each angular frame's spikes reach into the neighbour's rect), so a plain
            // rectangular slice drags the adjacent frame + record arcs. Each polygon
            // hugs its own frame+plate; where silhouettes interleave we clip our own
            // tip rather than cover the neighbour's art.
            Vector2[][] silhouettes =
            {
                new[] { new Vector2(300, 668), new Vector2(340, 618), new Vector2(400, 585),
                    new Vector2(478, 575), new Vector2(598, 608), new Vector2(570, 690),
                    new Vector2(548, 798), new Vector2(380, 806), new Vector2(312, 792) },
                new[] { new Vector2(40, 590), new Vector2(150, 578), new Vector2(330, 685),
                    new Vector2(348, 712), new Vector2(290, 748), new Vector2(300, 880),
                    new Vector2(330, 955), new Vector2(330, 1040), new Vector2(60, 1050),
                    new Vector2(18, 980), new Vector2(16, 700), new Vector2(10, 640) },
                new[] { new Vector2(480, 605), new Vector2(560, 608), new Vector2(715, 558),
                    new Vector2(840, 595), new Vector2(838, 715), new Vector2(720, 858),
                    new Vector2(842, 880), new Vector2(838, 1040), new Vector2(600, 1050),
                    new Vector2(545, 960), new Vector2(555, 860), new Vector2(478, 792),
                    new Vector2(490, 700) },
                new[] { new Vector2(225, 815), new Vector2(300, 800), new Vector2(560, 850),
                    new Vector2(605, 872), new Vector2(575, 960), new Vector2(615, 1100),
                    new Vector2(575, 1185), new Vector2(350, 1182), new Vector2(310, 1120),
                    new Vector2(285, 1040), new Vector2(235, 955), new Vector2(215, 880) },
            };
            Image teamBoard = root.GetComponent<Image>();
            for (int slot = 0; slot < GameModel.TeamCapacity; slot++)
            {
                int capturedSlot = slot;
                int index = slot < count ? model.Save.Team[slot] : -1;
                bool valid = index >= 0 && index < GameModel.Members.Length;
                Rect p = portraits[slot], label = plates[slot];
                GameObject hit = BoardButton038("TeamOrbit-" + slot, root, targets[slot], () =>
                { if (valid) OpenTeamMember(index, capturedSlot); else OpenTeamSlotPicker(Math.Min(capturedSlot, model.Save.Team.Count)); });
                // Orbit body = the authored frame union (portrait window + nameplate);
                // the live TeamLabel/TeamEmpty nodes sit on the board root, so they
                // join the motion as extras while keeping their own transforms.
                Rect orbitRegion = new Rect(p.x, p.y, p.width, label.y + label.height - p.y);
                if (!valid)
                {
                    Text emptyMark = BoardText038(root, "TeamEmpty-" + slot, "+", 46, Cyan,
                        p.x, p.y + p.height * .5f, p.width, 60, TextAnchor.MiddleCenter);
                    MainBakedMotion038Attach(hit, teamBoard, orbitRegion,
                        silhouettes[slot], emptyMark.transform);
                    continue;
                }
                MemberDefinition m = GameModel.Members[index];
                GameObject portrait = NewImage("TeamCharacter-" + slot, hit.transform,
                    Resources.Load<Sprite>(m.ResourcePath), White);
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
                MainBakedMotion038Attach(hit, teamBoard, orbitRegion, silhouettes[slot], tag.transform);
            }

            // 职业配置卡：透明容器把动态内容收进卡内安全区，避开顶部/右侧锯齿。
            GameObject synergy = NewImage("TeamSynergy", root, null, Color.clear);
            PlaceTop(synergy.GetComponent<RectTransform>(), 78, 1290, 690, 150);
            synergy.GetComponent<Image>().raycastTarget = false;
            BoardText038(synergy.transform, "TeamAttributes", "职业自由搭配", 13, Muted,
                540, 12, 112, 24, TextAnchor.MiddleRight);
            float[] statusX = { 40f, 188f, 338f, 482f };
            for (int i = 0; i < MemberCareers.All.Count; i++)
            {
                string career = MemberCareers.All[i];
                int number = model.Save.Team.Count(index =>
                    index >= 0 && index < GameModel.Members.Length && GameModel.Members[index].Career == career);
                BoardText038(synergy.transform, "CareerStatus-" + i, $"{career} · {number}人",
                    16, White, statusX[i], 42, 100, 26, TextAnchor.MiddleCenter);
                GameObject glyph = NewImage("CareerGlyph-" + i, synergy.transform, NavIconSprite(i), White);
                PlaceTop(glyph.GetComponent<RectTransform>(), statusX[i] + 18, 72, 64, 46);
                glyph.GetComponent<Image>().preserveAspect = true;
                glyph.GetComponent<Image>().raycastTarget = false;
            }

            GameObject changeLeader = BoardButton038("ChangeLeader", root, new Rect(118, 1450, 304, 110), OpenCaptainPicker);
            GameObject autoTeam = BoardButton038("AutoTeam", root, new Rect(465, 1450, 303, 110), () =>
            { model.AutoTeam(); Toast("已优先兼顾职业并补齐阵容"); ShowScreen("team"); });
            MainBakedMotion038Attach(changeLeader, teamBoard,
                MainBakedMotion038Inflate(new Rect(118, 1450, 304, 110), 4f));
            MainBakedMotion038Attach(autoTeam, teamBoard,
                MainBakedMotion038Inflate(new Rect(465, 1450, 303, 110), 4f));
        }

        // ------------------------------------------------------------------
        // RosterPolish038 — authored members board, labels inside tilted chip interiors

        private void BuildRosterPolish038MembersBoard()
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
            BoardText038(root, "ScreenSubtitle",
                $"已拥有 {model.Save.UnlockedMembers.Count}/{GameModel.Members.Length} · 本页 {page.VisibleCount} 名",
                18, White, 46, 307, 735, 32);

            // 三枚斜切筛选钮：命中区覆盖整颗钮，文字收在量出的内切安全区中心。
            string[] names = { "MemberRoleFilter", "MemberRaceFilter", "MemberOwnedFilter" };
            string[] labels = { "职业：" + (role.Length == 0 ? "全部" : role),
                "种族：" + (race.Length == 0 ? "全部" : race),
                "仅已拥有：" + (memberOwnedOnly ? "开" : "关") };
            Rect[] chips = { new Rect(45, 344, 240, 56), new Rect(307, 344, 225, 56), new Rect(543, 344, 248, 56) };
            float[] labelX = { 28f, 6f, -6f }; // interior safe centers: ~158 / ~398 / ~622 board-x
            for (int i = 0; i < 3; i++)
            {
                int filter = i;
                GameObject button = BoardButton038(names[i], root, chips[i], () =>
                {
                    if (filter == 0) memberRoleFilterIndex = (memberRoleFilterIndex + 1) % roles.Length;
                    else if (filter == 1) memberRaceFilterIndex = (memberRaceFilterIndex + 1) % races.Length;
                    else memberOwnedOnly = !memberOwnedOnly;
                    memberPageIndex = 0; ShowScreen("members");
                });
                Text filterLabel = BoardText038(button.transform, "Label", labels[i], 16, White,
                    labelX[i], 9, 170, 38, TextAnchor.MiddleCenter);
                PanelKit.EnableBestFit(filterLabel, 12);
                filterLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
                filterLabel.verticalOverflow = VerticalWrapMode.Truncate;
                MainBakedMotion038Attach(button, root.GetComponent<Image>(),
                    MainBakedMotion038Inflate(chips[i], 3f));
            }

            GameObject search = NewImage("MemberSearch", root, null, Color.clear);
            PlaceTop(search.GetComponent<RectTransform>(), 42, 419, 733, 61);
            search.GetComponent<Image>().raycastTarget = true;
            Text placeholder = BoardText038(search.transform, "Placeholder", "◇  搜索成员名称，输入后按回车",
                17, Muted, 15, 0, 655, 60);
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

            // 15 个真实卡位，5 列 3 行，对齐背板锯齿卡框（行高不同）。
            float[] rowY = { 500f, 806f, 1088f };
            float[] rowH = { 276f, 252f, 248f };
            for (int slot = 0; slot < page.VisibleCount; slot++)
            {
                int index = page.SourceIndexAt(slot);
                MemberRosterCell cell = MemberRosterPagination.CellFor(slot);
                RosterPolish038MemberCard(root, index,
                    new Rect(32 + cell.Column * 160, rowY[cell.Row], 143, rowH[cell.Row]));
            }
            if (page.IsEmpty)
                BoardText038(root, "MemberEmpty", "没有符合条件的成员，请调整筛选", 23, White,
                    70, 670, 700, 80, TextAnchor.MiddleCenter);

            GameObject previous = BoardButton038("MemberPreviousPage", root, new Rect(165, 1386, 185, 76), () =>
            { memberPageIndex = MemberRosterPagination.MovePage(memberPageIndex, -1, page.PageCount); ShowScreen("members"); });
            previous.GetComponent<Button>().interactable = page.HasPrevious;
            BoardText038(previous.transform, "Label", "上一页", 24,
                page.HasPrevious ? White : Muted, 0, 0, 185, 76, TextAnchor.MiddleCenter);
            BoardText038(root, "MemberPageLabel", $"{(page.PageCount == 0 ? 0 : page.PageIndex + 1)}/{page.PageCount}",
                34, White, 360, 1382, 120, 86, TextAnchor.MiddleCenter);
            GameObject next = BoardButton038("MemberNextPage", root, new Rect(491, 1386, 186, 76), () =>
            { memberPageIndex = MemberRosterPagination.MovePage(memberPageIndex, 1, page.PageCount); ShowScreen("members"); });
            next.GetComponent<Button>().interactable = page.HasNext;
            BoardText038(next.transform, "Label", "下一页", 24,
                page.HasNext ? White : Muted, 0, 0, 186, 76, TextAnchor.MiddleCenter);
            Image membersBoard = root.GetComponent<Image>();
            MainBakedMotion038Attach(previous, membersBoard,
                MainBakedMotion038Inflate(new Rect(165, 1386, 185, 76), 4f));
            MainBakedMotion038Attach(next, membersBoard,
                MainBakedMotion038Inflate(new Rect(491, 1386, 186, 76), 4f));
        }

        /// <summary>One real roster slot over the authored jagged card frame.</summary>
        private void RosterPolish038MemberCard(Transform root, int index, Rect rect)
        {
            MemberDefinition m = GameModel.Members[index];
            GameObject card = BoardButton038("Member-" + m.Id, root, rect, () => OpenMember(index));
            if (!model.IsUnlocked(index))
            {
                BuildLockedMemberCard(card, (int)rect.width, (int)rect.height);
                // Whole-card motion: baked jagged frame slice plus the live
                // silhouette/mark/locked labels all move as one unit; hit stays fixed.
                MainBakedMotion038Attach(card, root.GetComponent<Image>(),
                    MainBakedMotion038Inflate(rect, 3f));
                return;
            }

            // Owned: bust art clipped to the same slanted window shape — no rectangular seam.
            GameObject clip = RosterPolish038Clip(card.transform, "PortraitFrame",
                5, 5, rect.width - 10, rect.height - 97);
            GameObject art = NewImage("Portrait", clip.transform,
                Resources.Load<Sprite>(m.ThumbnailResourcePath), White);
            art.GetComponent<Image>().raycastTarget = false;
            PanelKit.FrameBustPortrait(art.GetComponent<Image>(),
                clip.GetComponent<RectTransform>(), m.Id);

            GameObject ribbon = NewImage("DeploymentBadge", card.transform, RibbonSprite(),
                model.IsInTeam(index) ? new Color32(36, 16, 45, 235) : new Color32(13, 8, 32, 230));
            PlaceTop(ribbon.GetComponent<RectTransform>(), 0, 4, 103, 27);
            ribbon.GetComponent<Image>().raycastTarget = false;
            BoardText038(ribbon.transform, "DeploymentLabel", MemberDeploymentLabel(index), 14,
                model.Save.Team.IndexOf(index) == 0 ? Pink : new Color32(255, 216, 110, 255),
                0, 0, 103, 27, TextAnchor.MiddleCenter);
            BoardText038(card.transform, "Taxonomy", $"{MemberRaceFamily(m, index)} · {m.Career}",
                12, Pink, 6, rect.height - 88, 131, 24);
            BoardText038(card.transform, "MemberName", m.Name, 19, White, 6, rect.height - 62, 131, 30);
            BoardText038(card.transform, "MemberLevel", $"等级 {model.LevelOf(index)}",
                13, Cyan, 6, rect.height - 32, 131, 22);
            // Whole-card motion: baked card frame slice plus live portrait frame,
            // deployment badge, name/level captions move together; hit stays fixed.
            MainBakedMotion038Attach(card, root.GetComponent<Image>(),
                MainBakedMotion038Inflate(rect, 3f));
        }
    }
}
