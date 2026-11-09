using System;
using System.Linq;
using ChoSiren.Panels;
using ChoSiren.UI;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>
    /// Meeting row 10: member detail page laid out after NIKKE's character screen — full-body
    /// art on the left, a stacked info column on the right (rarity, level/name plate, affection,
    /// power, attributes, actions, race/role badges), a tabbed equipment/skill/profile panel with
    /// our 4 equipment slots, and back/home buttons at the bottom. Content is our own game data.
    /// </summary>
    public sealed partial class ChoSirenApp
    {
        private const float DetailWidth = 720f;
        private int memberDetailTab;

        private static readonly Color DetailGold = new Color32(255, 196, 72, 255);
        private static readonly Color DetailPanel = new Color32(10, 6, 20, 238);
        private static readonly Color DetailBar = new Color32(0, 0, 0, 255);

        /// <summary>Race art key: 魅族 charm, 魔族 demon, 海灵族 sea, 血精灵 blood.</summary>
        private static string RaceIconKey(string race) => race switch
        {
            "魅族" => "charm",
            "魔族" => "demon",
            "海灵族" => "sea",
            "血精灵" => "blood",
            _ => null,
        };

        private static Sprite RaceIconSprite(string race)
        {
            string key = RaceIconKey(race);
            return key == null ? null : ReferenceArt038.Load("Art/Reference038/race-" + key);
        }

        /// <summary>
        /// Full-body art for the detail page. The current member pack is authoritative;
        /// the two lobby idols are used only when that member has no full-body resource.
        /// </summary>
        private Sprite MemberFullBodySprite(MemberDefinition member, int index)
        {
            Sprite current = Resources.Load<Sprite>(member.ResourcePath);
            if (current != null) return current;
            string race = MemberRaceFamily(member, index);
            Sprite art = race == "魅族" ? ReferenceArt038.Load("Art/Reference038/home-layer-04")
                : race == "魔族" ? ReferenceArt038.Load("Art/Reference038/idol-lilith-full") : null;
            return art != null ? art : Resources.Load<Sprite>(member.ResourcePath);
        }

        private void OpenMemberDetail(int memberIndex, int teamSlot)
        {
            Action back = memberProfileReturn;
            CloseModal();
            memberProfileReturn = back;
            MemberDefinition m = GameModel.Members[memberIndex];
            string race = MemberRaceFamily(m, memberIndex);
            Color raceColor = TeamRaceColor(race);
            int level = model.LevelOf(memberIndex);
            bool canTrain = model.CanTrain(memberIndex, out int trainingCost, out string trainReason);
            bool atCap = level >= GameModel.MaxMemberLevel;
            // 2026-11-04: a breakthrough is done in the practice room, so it opens the room like a level-up does.
            bool breakNow = model.NeedsBreakthrough1104(memberIndex);
            bool canPractice = canTrain || breakNow;
            int levelCap = model.LevelCapOf1104(memberIndex);
            bool inTeam = model.IsInTeam(memberIndex);
            bool captain = model.Save.Team.Count > 0 && model.Save.Team[0] == memberIndex;
            var stats = model.StatsOf(memberIndex);

            GameObject overlay = NewImage("MemberModal", safeRoot, null, new Color32(8, 4, 16, 255));
            Stretch(overlay.GetComponent<RectTransform>());
            overlay.GetComponent<Image>().raycastTarget = true;
            modalObject = overlay;
            Sprite backdropSprite = ReferenceArt038.Load("Art/Reference038/member-detail-bg");
            GameObject backdrop = NewImage("Backdrop", overlay.transform, backdropSprite, White);
            backdrop.GetComponent<Image>().raycastTarget = false;
            if (backdropSprite != null)
            {
                AspectRatioFitter fit = backdrop.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = backdropSprite.rect.width / backdropSprite.rect.height;
                PageAmbient.Set(overlay, backdropSprite);
            }

            Canvas.ForceUpdateCanvases();
            RectTransform host = overlay.GetComponent<RectTransform>();
            float hostW = host.rect.width > 1 ? host.rect.width : 720f;
            float hostH = host.rect.height > 1 ? host.rect.height : 1536f;
            // Safari's visible area can be shorter than the authored portrait page.
            // Keep the last equipment actions inside the viewport with bottom padding.
            const float lift = 50f, minimumPageHeight = 1384f;
            float scale = Mathf.Min(hostW / DetailWidth, hostH / (minimumPageHeight - lift));
            float pageH = hostH / scale + lift;
            GameObject pageObject = NewObject("Page", overlay.transform);
            RectTransform page = pageObject.AddComponent<RectTransform>();
            page.anchorMin = page.anchorMax = page.pivot = new Vector2(.5f, 1f);
            // Lift the page into the empty band under the status bar on notched phones.
            page.anchoredPosition = new Vector2(0f, lift * scale);
            page.sizeDelta = new Vector2(DetailWidth, pageH);
            page.localScale = Vector3.one * scale;
            overlay.AddComponent<MemberDetailViewportFit>().Bind(page, lift);

            // ---- full-body art, left, bleeding past the page edge like the reference
            Sprite art = MemberFullBodySprite(m, memberIndex);
            GameObject figure = NewImage("MemberFullBody", page, art, White);
            figure.GetComponent<Image>().raycastTarget = false;
            figure.GetComponent<Image>().preserveAspect = true;
            float artH = pageH * .8f;
            float artW = art != null ? artH * art.rect.width / art.rect.height : artH * .5f;
            PlaceTop(figure.GetComponent<RectTransform>(), 240f - artW * .5f, 170f, artW, artH);

            // ---- the shared header stays on top of this page (same bar as every screen)
            Transform topBar = safeRoot.Find("TopBar");
            if (topBar != null) { topBar.gameObject.SetActive(true); topBar.SetAsLastSibling(); }

            // ---- side diamonds (服装/收藏品/主力 in the reference)
            DetailDiamond(page, "DetailBond", 26, 150, "羁绊", "绊", model.DeepTalkUnlocked(memberIndex) && !model.DeepTalkUsedToday(memberIndex), () =>
            {
                int before = model.AffectionOf(memberIndex);
                if (!model.DeepTalk(memberIndex, out string message, out string dialogue)) { Toast(message); return; }
                OpenDeepTalkResult(memberIndex, teamSlot, dialogue, before, model.AffectionOf(memberIndex));
            });
            DetailDiamond(page, "DetailPractice", 26, 270, "练习室", "练", canPractice, () =>
            { if (canPractice) OpenTrainingPractice(memberIndex, teamSlot); else Toast(atCap ? "已满级" : trainReason); });
            DetailDiamond(page, "DetailCaptain", 606, 150, captain ? "当前队长" : "设为队长", "队", !captain, () =>
            { if (!captain) ConfirmCaptain(memberIndex); });

            // ---- right info column
            const float colX = 392f, colW = 314f;
            int stars = m.Rarity == "SSR" ? 3 : m.Rarity == "SR" ? 2 : 1;
            Text rarity = BoardText038(page, "DetailRarity", string.IsNullOrEmpty(m.Rarity) ? "R" : m.Rarity, 46,
                m.Rarity == "SSR" ? new Color32(255, 140, 40, 255) : m.Rarity == "SR" ? (Color)new Color32(200, 120, 255, 255) : Cyan,
                colX + 8, 330, 150, 60);
            rarity.fontStyle = FontStyle.BoldAndItalic;
            BoardText038(page, "DetailStars", new string('★', stars), 28, DetailGold, colX + 180, 336, 130, 50, TextAnchor.MiddleRight);

            GameObject plate = NewImage("DetailNamePlate", page, PunkUi.SlantFill, new Color32(16, 12, 24, 250));
            PlaceTop(plate.GetComponent<RectTransform>(), colX - 4, 384, colW + 8, 70);
            plate.GetComponent<Image>().raycastTarget = false;
            SirenStickerUi.SkinPanel(plate);
            BoardText038(plate.transform, "LvTag", "LV.", 17, White, 20, 30, 34, 26);
            BoardText038(plate.transform, "Level", level.ToString(), 36, DetailGold, 52, 10, 70, 50);
            BoardText038(plate.transform, "LevelCap", "/" + levelCap, 17, Muted, 112, 30, 50, 26);
            Text name = BoardText038(plate.transform, "MemberName", m.Name, 34, White, 156, 8, colW - 168, 54, TextAnchor.MiddleRight);
            PanelKit.EnableBestFit(name, 22);
            SirenStickerUi.SkinTitle(name);
            name.verticalOverflow = VerticalWrapMode.Truncate;
            GameObject levelChip = NewImage("DetailLevelChip", page, PunkUi.SlantFill, new Color32(60, 54, 74, 235));
            PlaceTop(levelChip.GetComponent<RectTransform>(), colX + 4, 458, 86, 26);
            levelChip.GetComponent<Image>().raycastTarget = false;
            BoardText038(levelChip.transform, "Label", atCap ? "已满级" : canTrain ? "可升级" : breakNow ? "可突破"
                : level >= levelCap ? "到上限" : "币不足", 13, Muted,
                0, 0, 86, 26, TextAnchor.MiddleCenter);

            int affection = model.AffectionOf(memberIndex);
            GameObject bond = BoardButton038("DetailAffection", page, new Rect(colX + 96, 458, colW - 96, 42), () =>
            {
                int before = model.AffectionOf(memberIndex);
                if (!model.DeepTalk(memberIndex, out string message, out string dialogue)) { Toast(message); return; }
                OpenDeepTalkResult(memberIndex, teamSlot, dialogue, before, model.AffectionOf(memberIndex));
            });
            Image bondFace = bond.GetComponent<Image>();
            bondFace.sprite = PunkUi.SlantFill; bondFace.color = new Color32(255, 240, 248, 255);
            BoardText038(bond.transform, "Label", $"»  好感 · {model.AffectionTierOf(memberIndex)}", 17, PunkUi.Hot, 14, 0, 160, 42);
            GameObject heart = new GameObject("Heart", typeof(RectTransform), typeof(CanvasRenderer), typeof(ProfileEmblemFrame038));
            heart.transform.SetParent(bond.transform, false);
            PlaceTop(heart.GetComponent<RectTransform>(), colW - 96 - 52, 3, 44, 38);
            ProfileEmblemFrame038 heartShape = heart.GetComponent<ProfileEmblemFrame038>();
            heartShape.SolidHeart = true; heartShape.color = PunkUi.Hot; heartShape.raycastTarget = false;
            SirenStickerUi.SkinButton(bond, false, true);
            BoardText038(heart.transform, "Value", affection.ToString(), 15, White, 0, 0, 44, 36, TextAnchor.MiddleCenter);

            BoardText038(page, "DetailPowerLabel", "战斗力", 18, White, colX + 140, 504, 120, 28);
            GameObject careerHex = new GameObject("DetailCareerHex", typeof(RectTransform), typeof(CanvasRenderer), typeof(ProfileEmblemFrame038));
            careerHex.transform.SetParent(page, false);
            PlaceTop(careerHex.GetComponent<RectTransform>(), colX + 28, 514, 92, 100);
            ProfileEmblemFrame038 hexShape = careerHex.GetComponent<ProfileEmblemFrame038>();
            hexShape.SolidHex = true; hexShape.color = new Color32(24, 22, 30, 255); hexShape.raycastTarget = false;
            Text careerGlyph = BoardText038(careerHex.transform, "Career", CareerGlyph(m.Career), 30, White, 0, 0, 92, 100, TextAnchor.MiddleCenter);
            careerGlyph.fontStyle = FontStyle.Bold;
            Text power = BoardText038(page, "MemberPower", model.PowerOf(memberIndex).ToString("N0"), 56, White, colX + 124, 530, 194, 74);
            PanelKit.EnableBestFit(power, 36);
            BoardText038(page, "DetailBattleTag", "BATTLE", 14, Muted, colX + 210, 596, 100, 20, TextAnchor.MiddleRight);

            // 属性: the three combat numbers the reference shows.
            GameObject attributes = NewImage("MemberStatPanel", page, null, SirenStickerUi.Ink);
            PlaceTop(attributes.GetComponent<RectTransform>(), colX, 620, colW, 150);
            attributes.GetComponent<Image>().raycastTarget = false;
            SirenStickerUi.SkinPanel(attributes, false);
            DetailHeaderBar(attributes.transform, "属性", colW);
            string[] attrNames = { "体力", "攻击力", "防御力" };
            int[] attrValues = { stats.Hp, stats.Attack, stats.Defense };
            for (int i = 0; i < 3; i++)
            {
                float y = 38 + i * 37;
                AttributeHex(attributes.transform, "Glyph-" + i, 14, y + 7, i);
                BoardText038(attributes.transform, "Name-" + i, attrNames[i], 19, SirenStickerUi.Paper, 42, y, 120, 34);
                BoardText038(attributes.transform, "Value-" + i, attrValues[i].ToString("N0"), 21, new Color32(214, 140, 20, 255), colW - 166, y, 130, 34, TextAnchor.MiddleRight); // clear of the slanted edge
            }

            // Team toggle (同步中) + training (核心强化) in the reference's two-button row.
            GameObject teamToggle = BoardButton038("Team", page, new Rect(colX, 780, 152, 54), () =>
            {
                if (!model.IsInTeam(memberIndex) && model.Save.Team.Count >= GameModel.TeamCapacity)
                { OpenTeamReplacement(memberIndex); return; }
                model.ToggleTeamMember(memberIndex, out string message);
                Toast(message); OpenMemberDetail(memberIndex, teamSlot);
            });
            teamToggle.GetComponent<Image>().sprite = PunkUi.SlantFill;
            teamToggle.GetComponent<Image>().color = new Color32(22, 18, 28, 255);
            BoardText038(teamToggle.transform, "Label", captain ? "队长出战中" : inTeam ? "出战中" : "加入编队", 19, DetailGold, 0, 0, 152, 54, TextAnchor.MiddleCenter);
            GameObject train = BoardButton038("Train", page, new Rect(colX + 160, 780, colW - 160, 54), () =>
            { if (canPractice) OpenTrainingPractice(memberIndex, teamSlot); else Toast(atCap ? "已满级" : trainReason); });
            train.GetComponent<Image>().sprite = PunkUi.SlantFill;
            train.GetComponent<Image>().color = canPractice ? PunkUi.Hot : new Color32(90, 70, 100, 255);
            BoardText038(train.transform, "Label", atCap ? "已满级" : breakNow ? "突破" : level >= levelCap ? trainReason : "练习升级",
                20, White, 0, 0, colW - 160, 54, TextAnchor.MiddleCenter);
            SirenStickerUi.SkinButton(teamToggle, false, true);
            SirenStickerUi.SkinButton(train, canPractice, true);

            // 小队 bar → race & role.
            GameObject squad = NewImage("DetailSquad", page, PunkUi.SlantFill, SirenStickerUi.Ink);
            PlaceTop(squad.GetComponent<RectTransform>(), colX, 844, colW, 40);
            squad.GetComponent<Image>().raycastTarget = false;
            SirenStickerUi.SkinPanel(squad, false);
            BoardText038(squad.transform, "Label", "种族", 17, SirenStickerUi.Muted, 18, 0, 60, 40);
            BoardText038(squad.transform, "Value", $"{race} · {m.Career}", 19, SirenStickerUi.Paper, 90, 0, colW - 110, 40, TextAnchor.MiddleCenter);

            // Badge row: race icon, role hex, two skill hexes.
            MemberSkillCopy(m, out string firstName, out string firstEffect, out string secondName, out string secondEffect);
            MemberNormalAttackCopy(m, out string normalName, out string normalEffect);
            GameObject raceBadge = NewImage("DetailRaceIcon", page, RaceIconSprite(race), White);
            PlaceTop(raceBadge.GetComponent<RectTransform>(), colX + 4, 892, 70, 70);
            raceBadge.GetComponent<Image>().preserveAspect = true;
            raceBadge.GetComponent<Image>().raycastTarget = false;
            DetailHex(page, "DetailRoleBadge", colX + 82, 892, new Color32(245, 242, 250, 255), CareerGlyph(m.Career), new Color32(30, 24, 40, 255), null);
            DetailHex(page, "DetailSkill1", colX + 160, 892, new Color32(245, 242, 250, 255), "", Accent, firstName);
            DetailHex(page, "DetailSkill2", colX + 238, 892, new Color32(245, 242, 250, 255), "", Cyan, secondName);

            // ---- tabbed lower panel
            const float panelX = 436f, panelY = 974f, panelW = 272f, panelH = 296f;
            string[] tabs = { "装备", "技能", "档案" };
            for (int i = 0; i < tabs.Length; i++)
            {
                int tabIndex = i;
                bool active = memberDetailTab == i;
                GameObject tabButton = BoardButton038("DetailTab-" + i, page, new Rect(panelX - 78, panelY + 6 + i * 96, 72, 88), () =>
                { memberDetailTab = tabIndex; OpenMemberDetail(memberIndex, teamSlot); });
                tabButton.GetComponent<Image>().color = active ? (Color)PunkUi.Hot : new Color32(20, 16, 26, 245);
                BoardText038(tabButton.transform, "Label", tabs[i], 20, White, 0, 0, 72, 88, TextAnchor.MiddleCenter);
                SirenPageChrome.Tab(tabButton, active);
            }
            GameObject panel = NewImage("DetailPanel", page, null, SirenStickerUi.Ink);
            PlaceTop(panel.GetComponent<RectTransform>(), panelX, panelY, panelW, panelH);
            panel.GetComponent<Image>().raycastTarget = false;
            SirenStickerUi.SkinPanel(panel, false);
            DetailHeaderBar(panel.transform, tabs[Mathf.Clamp(memberDetailTab, 0, 2)], panelW);
            if (memberDetailTab == 1) BuildDetailSkills(panel.transform, panelW, normalName, normalEffect, firstName, firstEffect, secondName, secondEffect, m);
            else if (memberDetailTab == 2) BuildDetailProfile(panel.transform, panelW, m, memberIndex, level);
            else BuildDetailEquipment(panel.transform, memberIndex);

            // Quick equip / unequip all.
            GameObject quick = BoardButton038("DetailQuickEquip", page, new Rect(panelX - 8, panelY + panelH + 10, 132, 66), () =>
            { model.QuickEquip(memberIndex, out string message); Toast(message); OpenMemberDetail(memberIndex, teamSlot); });
            quick.GetComponent<Image>().sprite = PunkUi.SlantFill;
            quick.GetComponent<Image>().color = new Color32(22, 18, 28, 255);
            BoardText038(quick.transform, "Label", "快捷\n穿戴", 18, White, 0, 0, 132, 66, TextAnchor.MiddleCenter);
            GameObject clear = BoardButton038("DetailUnequipAll", page, new Rect(panelX + 134, panelY + panelH + 18, 132, 66), () =>
            { model.UnequipAll(memberIndex, out string message); Toast(message); OpenMemberDetail(memberIndex, teamSlot); });
            clear.GetComponent<Image>().sprite = PunkUi.SlantFill;
            clear.GetComponent<Image>().color = new Color32(22, 18, 28, 255);
            BoardText038(clear.transform, "Label", "全部\n解除", 18, White, 0, 0, 132, 66, TextAnchor.MiddleCenter);
            SirenStickerUi.SkinButton(quick, false, true);
            SirenStickerUi.SkinButton(clear, false, true);

            // ---- back / home
            float bottom = pageH - 96f;
            GameObject backButton = null;
            backButton = BoardButton038("CloseTop", page, new Rect(-16, bottom, 224, 66), () =>
            {
                // 2026-11-08 (转场参考视频): tiles cascade over the page and the previous page comes back under them.
                PlayTileReturn1108(backButton != null ? (RectTransform)backButton.transform : null, () =>
                {
                    Action previous = memberProfileReturn;
                    memberProfileReturn = null;
                    CloseModal(); previous?.Invoke();
                });
            });
            backButton.GetComponent<Image>().sprite = PunkUi.SlantFill;
            backButton.GetComponent<Image>().color = PunkUi.Hot;
            BoardText038(backButton.transform, "Label", "‹  返回", 26, White, 20, 0, 200, 66, TextAnchor.MiddleCenter);
            GameObject home = BoardButton038("DetailHome", page, new Rect(212, bottom, 86, 66), () =>
            { memberProfileReturn = null; CloseModal(); ShowScreen("lobby"); });
            home.GetComponent<Image>().sprite = PunkUi.SlantFill;
            home.GetComponent<Image>().color = PunkUi.Hot;
            BoardText038(home.transform, "Label", "大厅", 22, White, 0, 0, 86, 66, TextAnchor.MiddleCenter);
            SirenStickerUi.SkinButton(backButton);
            SirenStickerUi.SkinButton(home, false, true);

            // ---- previous / next member: edge tabs, page dots, swipe (ChoSirenAppMemberDetail1101.cs)
            BuildDetailSwitcher1101(overlay, page, figure.GetComponent<RectTransform>(), memberIndex, pageH);
        }

        private static string CareerGlyph(string career) => career switch
        {
            "主唱" => "唱",
            "主舞" => "舞",
            "Rapper" => "R",
            "门面" => "颜",
            _ => string.IsNullOrEmpty(career) ? "?" : career.Substring(0, 1),
        };

        private void DetailHeaderBar(Transform parent, string title, float width)
        {
            GameObject bar = NewImage("Header", parent, PunkUi.SlantFill, DetailBar);
            PlaceTop(bar.GetComponent<RectTransform>(), 0, 0, width, 32);
            bar.GetComponent<Image>().raycastTarget = false;
            SirenStickerUi.SkinPanel(bar, false);
            SirenStickerUi.SkinTitle(BoardText038(bar.transform, "Title", title, 19, White, 16, 0, 160, 32));
        }

        private void DetailDiamond(Transform page, string name, float x, float y, string label, bool highlight, UnityEngine.Events.UnityAction action)
            => DetailDiamond(page, name, x, y, label, label.Substring(0, 1), highlight, action);

        private void DetailDiamond(Transform page, string name, float x, float y, string label, string glyph, bool highlight, UnityEngine.Events.UnityAction action)
        {
            GameObject hit = BoardButton038(name, page, new Rect(x, y, 92, 116), action);
            // 2026-11-03: a solid teal fill reads as "selected/pressed" (tabs use it that way), so an
            // available action keeps the dark face and shows itself with a teal rim and glyph instead.
            GameObject diamond = NewImage("Diamond", hit.transform, null, new Color32(26, 22, 34, 245));
            RectTransform d = diamond.GetComponent<RectTransform>();
            PlaceTop(d, 18, 6, 56, 56);
            d.pivot = new Vector2(.5f, .5f);
            d.anchoredPosition = new Vector2(46, -40);
            d.localRotation = Quaternion.Euler(0, 0, 45);
            diamond.GetComponent<Image>().raycastTarget = false;
            Outline rim = diamond.AddComponent<Outline>();
            rim.effectColor = highlight ? (Color)Brand1025.Teal : new Color(1, 1, 1, .8f);
            rim.effectDistance = new Vector2(2, -2);
            BoardText038(hit.transform, "Glyph", glyph, 24, highlight ? (Color)Brand1025.Teal : White, 0, 18, 92, 44, TextAnchor.MiddleCenter);
            BoardText038(hit.transform, "Label", label, 17, White, -10, 78, 112, 30, TextAnchor.MiddleCenter);
        }

        private void DetailHex(Transform page, string name, float x, float y, Color face, string glyph, Color glyphColor, string skillName)
        {
            GameObject hex = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(ProfileEmblemFrame038));
            hex.transform.SetParent(page, false);
            PlaceTop(hex.GetComponent<RectTransform>(), x, y, 70, 70);
            ProfileEmblemFrame038 shape = hex.GetComponent<ProfileEmblemFrame038>();
            shape.SolidHex = true; shape.color = face; shape.raycastTarget = false;
            if (skillName != null)
            {
                var icon = SkillIconVisuals.Create(hex.transform, "Icon", skillName, string.Empty, glyphColor);
                PlaceTop(icon.rectTransform, 19, 12, 32, 32);
                Text label = BoardText038(hex.transform, "Name", skillName, 11, new Color32(30, 24, 40, 255), 4, 44, 62, 18, TextAnchor.MiddleCenter);
                PanelKit.EnableBestFit(label, 8);
            }
            else BoardText038(hex.transform, "Glyph", glyph, 26, glyphColor, 0, 0, 70, 70, TextAnchor.MiddleCenter);
        }

        private void BuildDetailEquipment(Transform panel, int memberIndex)
        {
            for (int slot = 0; slot < GameModel.EquipmentSlotCount; slot++)
            {
                int s = slot;
                int item = model.EquippedInSlot(memberIndex, s);
                Rect cell = new Rect(12 + (s % 2) * 128, 44 + (s / 2) * 124, 120, 116);
                GameObject card = BoardButton038("DetailSlot-" + s, panel, cell, () =>
                {
                    Action back = () => OpenMemberDetail(memberIndex, -1);
                    if (item >= 0) OpenAccessoryCard(item, memberIndex, back);
                    else OpenAccessorySwap(memberIndex, s, back);
                });
                card.GetComponent<Image>().color = SirenStickerUi.Ink;
                SirenStickerUi.SkinPanel(card, false);
                GameObject strip = NewImage("RarityStrip", card.transform, null, item >= 0 ? AccessoryRarityColor(item) : new Color32(160, 150, 170, 255));
                PlaceTop(strip.GetComponent<RectTransform>(), 0, 108, 120, 8);
                strip.GetComponent<Image>().raycastTarget = false;
                if (item >= 0)
                {
                    GameObject icon = NewImage("Icon", card.transform, AccessoryItemSprite(item), White);
                    PlaceTop(icon.GetComponent<RectTransform>(), 30, 8, 86, 78);
                    icon.GetComponent<Image>().preserveAspect = true;
                    icon.GetComponent<Image>().raycastTarget = false;
                    BoardText038(card.transform, "LvTag", "LV.", 11, SirenStickerUi.Muted, 6, 58, 30, 14);
                    BoardText038(card.transform, "Level", (model.AccessoryUpgradeLevel(item) + 1).ToString("00"), 22, SirenStickerUi.Paper, 4, 70, 50, 30);
                }
                else BoardText038(card.transform, "Empty", "+", 40, new Color32(120, 110, 130, 255), 0, 14, 120, 60, TextAnchor.MiddleCenter);
                GameObject chip = NewImage("SlotChip", card.transform, PunkUi.SlantFill, new Color32(30, 24, 40, 235));
                PlaceTop(chip.GetComponent<RectTransform>(), 4, 4, 48, 20);
                chip.GetComponent<Image>().raycastTarget = false;
                BoardText038(chip.transform, "Label", GameModel.EquipmentSlotNames[s], 12, White, 0, 0, 48, 20, TextAnchor.MiddleCenter);
            }
        }

        private RectTransform DetailTextScroll(Transform panel, float width, string name)
        {
            GameObject host = NewObject(name, panel);
            RectTransform scrollRect = host.AddComponent<RectTransform>();
            PlaceTop(scrollRect, 8, 40, width - 16, 246);
            GameObject viewport = NewImage("Viewport", host.transform, null, new Color(0, 0, 0, .01f));
            Stretch(viewport.GetComponent<RectTransform>());
            viewport.GetComponent<Image>().raycastTarget = true;
            viewport.AddComponent<RectMask2D>();
            GameObject contentObject = NewObject("Content", viewport.transform);
            RectTransform content = contentObject.AddComponent<RectTransform>();
            PlaceTop(content, 0, 0, width - 16, 246);
            ScrollRect scroll = host.AddComponent<ScrollRect>();
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 36f;
            return content;
        }

        private void BuildDetailSkills(Transform panel, float width, string normalName, string normalEffect,
            string firstName, string firstEffect, string secondName, string secondEffect, MemberDefinition m)
        {
            string[] titles = { "普攻 · " + normalName, "技能 · " + firstName, "技能 · " + secondName, "队长特性" };
            string[] bodies = { normalEffect, firstEffect, secondEffect, CaptainEffectCopy(m.Race) };
            RectTransform content = DetailTextScroll(panel, width, "DetailSkillScroll");
            float y = 0f;
            for (int i = 0; i < titles.Length; i++)
            {
                Text title = BoardText038(content, "SkillTitle-" + i, titles[i], 23,
                    i == 3 ? SirenStickerUi.Cyan : SirenStickerUi.Paper, 4, y, width - 32, 58,
                    TextAnchor.UpperLeft);
                title.resizeTextForBestFit = false;
                float titleHeight = Mathf.Max(40f, title.preferredHeight + 6f);
                title.rectTransform.sizeDelta = new Vector2(width - 32, titleHeight);
                Text body = BoardText038(content, "SkillBody-" + i, bodies[i], 24,
                    SirenStickerUi.Muted, 4, y + titleHeight, width - 32, 320, TextAnchor.UpperLeft);
                body.fontStyle = FontStyle.Normal;
                body.resizeTextForBestFit = false;
                body.lineSpacing = 1.12f;
                float bodyHeight = Mathf.Max(72f, body.preferredHeight + 12f);
                body.rectTransform.sizeDelta = new Vector2(width - 32, bodyHeight);
                y += titleHeight + bodyHeight + 22f;
            }
            content.sizeDelta = new Vector2(width - 16, Mathf.Max(246f, y));
        }

        private void BuildDetailProfile(Transform panel, float width, MemberDefinition m, int memberIndex, int level)
        {
            GameModel.StageStats(m, memberIndex, model.SigningChannelOf(memberIndex) == 0 ? 0 : 1, level,
                out int vocal, out int rap, out int dance, out int fame, out int beauty);
            string[] names = { "声乐", "说唱", "舞蹈", "名气", "颜值", "薪资", "风险" };
            string[] values = { vocal.ToString(), rap.ToString(), dance.ToString(), fame.ToString(), beauty.ToString(),
                $"{model.EstimatedMonthlySalary(memberIndex):N0}/期", model.RiskOf(memberIndex).ToString() };
            RectTransform content = DetailTextScroll(panel, width, "DetailProfileScroll");
            for (int i = 0; i < names.Length; i++)
            {
                float y = i * 52;
                BoardText038(content, "ProfileName-" + i, names[i], 22, SirenStickerUi.Paper, 8, y, 84, 46);
                BoardText038(content, "ProfileValue-" + i, values[i], 23, i < 5 ? (Color)new Color32(255, 196, 72, 255) : SirenStickerUi.Paper,
                    92, y, width - 120, 46, TextAnchor.MiddleRight);
            }
            content.sizeDelta = new Vector2(width - 16, names.Length * 52f);
        }
    }

    // Fit the existing page when Safari expands/collapses its toolbar. Keeping the
    // same hierarchy preserves the selected member, tab, scroll state and callbacks.
    internal sealed class MemberDetailViewportFit : MonoBehaviour
    {
        private RectTransform host, page;
        private float lift;

        internal void Bind(RectTransform target, float topLift)
        {
            host = GetComponent<RectTransform>();
            page = target;
            lift = topLift;
            Apply();
        }

        private void OnRectTransformDimensionsChange() => Apply();

        private void Apply()
        {
            if (host == null || page == null || host.rect.width <= 1f || host.rect.height <= 1f) return;
            float scale = Mathf.Min(host.rect.width / page.rect.width,
                host.rect.height / Mathf.Max(1f, page.rect.height - lift));
            page.localScale = Vector3.one * scale;
            page.anchoredPosition = new Vector2(0f, lift * scale);
        }
    }
}
