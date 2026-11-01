using System;
using ChoSiren.Panels;
using ChoSiren.Systems.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    public sealed partial class ChoSirenApp
    {
        private void OpenProfileBoard038(int memberIndex, int teamSlot, Sprite board)
        {
            memberProfileReturn = null;
            CloseModal();
            MemberDefinition member = GameModel.Members[memberIndex];
            int level = model.LevelOf(memberIndex);
            bool canTrain = model.CanTrain(memberIndex, out int trainingCost, out _);
            bool atCap = level >= GameModel.MaxMemberLevel;
            bool inTeam = model.IsInTeam(memberIndex);
            bool captain = model.Save.Team.Count > 0 && model.Save.Team[0] == memberIndex;
            int affection = model.AffectionOf(memberIndex);
            GameModel.StageStats(member, memberIndex, model.SigningChannelOf(memberIndex) == 0 ? 0 : 1, level, out int vocal, out int rhythm,
                out int dance, out int fame, out int beauty);
            MemberSkillCopy(member, out string firstName, out string firstEffect,
                out string secondName, out string secondEffect);
            MemberNormalAttackCopy(member, out string normalName, out string normalEffect);

            GameObject overlay = NewImage("MemberModal", safeRoot, null, new Color32(5, 3, 19, 255));
            Stretch(overlay.GetComponent<RectTransform>());
            overlay.GetComponent<Image>().raycastTarget = true;
            modalObject = overlay;
            Canvas.ForceUpdateCanvases();
            RectTransform host = overlay.GetComponent<RectTransform>();
            float width = host.rect.width > 1 ? host.rect.width : 720f;
            float height = host.rect.height > 1 ? host.rect.height : 1536f;
            float scale = Mathf.Min(width / board.rect.width, height / board.rect.height);
            GameObject panel = NewImage("Panel", overlay.transform, board, White);
            RectTransform page = panel.GetComponent<RectTransform>();
            page.anchorMin = page.anchorMax = page.pivot = new Vector2(.5f, .5f);
            page.sizeDelta = board.rect.size;
            page.anchoredPosition = Vector2.zero;
            page.localScale = Vector3.one * scale;
            panel.GetComponent<Image>().preserveAspect = true;
            panel.GetComponent<Image>().raycastTarget = false;
            GameObject close = BoardButton038("CloseTop", page, new Rect(768, 58, 77, 82), () =>
            {
                Action back = memberProfileReturn;
                memberProfileReturn = null;
                CloseModal(); back?.Invoke();
            });

            // Clip to the authored aperture, including its angled left and bottom edges.
            GameObject portraitFrame = new GameObject("PortraitFrame", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(ProfilePortraitMask038), typeof(Mask));
            portraitFrame.transform.SetParent(page, false);
            RectTransform portraitRect = portraitFrame.GetComponent<RectTransform>();
            PlaceTop(portraitRect, 35, 70, 425, 710);
            portraitFrame.GetComponent<ProfilePortraitMask038>().raycastTarget = false;
            portraitFrame.GetComponent<Mask>().showMaskGraphic = false;
            GameObject portrait = NewImage("Portrait", portraitFrame.transform, Resources.Load<Sprite>(member.ResourcePath), White);
            Image portraitImage = portrait.GetComponent<Image>();
            portraitImage.raycastTarget = false;
            PanelKit.FrameBustPortrait(portraitImage, portraitRect, member.Id);
            portraitImage.rectTransform.anchoredPosition += Vector2.down * 90f;
            GameObject portraitRim = new GameObject("PortraitAuthoredRim", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(ProfilePortraitRim038));
            portraitRim.transform.SetParent(page, false);
            PlaceTop(portraitRim.GetComponent<RectTransform>(), 35, 70, 425, 710);
            portraitRim.GetComponent<ProfilePortraitRim038>().Source = board;
            portraitRim.GetComponent<ProfilePortraitRim038>().raycastTarget = false;
            GameObject careerChip = new GameObject("MemberCareerTagChip", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(ProfileEmblemFrame038));
            careerChip.transform.SetParent(page, false);
            PlaceTop(careerChip.GetComponent<RectTransform>(), 56, 715, 246, 48);
            ProfileEmblemFrame038 chipShape = careerChip.GetComponent<ProfileEmblemFrame038>();
            chipShape.Slant = 9f; chipShape.color = new Color32(10, 6, 30, 235); chipShape.raycastTarget = false;
            BoardText038(page, "MemberCareerTagText", $"{member.Career} · 等级 {level}", 23, White, 82, 720, 210, 38);

            BoardText038(page, "MemberOwnershipStatus", "已签约成员", 20, Accent, 480, 102, 250, 35);
            BoardText038(page, "MemberName", member.Name, 66, White, 481, 142, 297, 94);
            BoardText038(page, "MemberRaceCareer", $"{MemberRace(member, memberIndex)} · {member.Career}",
                23, White, 487, 239, 295, 43);
            BoardText038(page, "MemberPowerNumber", $"战力 {model.PowerOf(memberIndex):N0}", 43, White, 483, 308, 337, 65);
            BoardText038(page, "MemberPower", $"等级 {level} / {GameModel.MaxMemberLevel}", 26, White, 489, 416, 318, 47);
            ProfileBar038(page, "MemberLevelTrack", 496, 463, 286, 7, level / (float)GameModel.MaxMemberLevel, Accent);
            string[] infoNames = { "定位", "种族", "编队", "风险", "薪资", "羁绊" };
            string[] infoValues = { member.Career, MemberRaceFamily(member, memberIndex), captain ? "当前队长" : inTeam ? "出战中" : "待命",
                model.RiskOf(memberIndex).ToString(), $"{model.EstimatedMonthlySalary(memberIndex):N0}/期",
                model.DeepTalkUnlocked(memberIndex) ? "已解锁" : $"好感{GameModel.DeepTalkAffectionUnlock}解锁" };
            // Authored table slots sit between separators at y 485/540/583/626/669/711/760;
            // keep every row inside its slot so the first and last lines clear the frame.
            float[] infoRowTops = { 494f, 543f, 586f, 629f, 672f, 717f };
            for (int i = 0; i < infoNames.Length; i++)
            {
                BoardText038(page, "MemberInfoLabel-" + i, infoNames[i], 19, Muted, 502, infoRowTops[i], 84, 37);
                BoardText038(page, "MemberInfoValue-" + i, infoValues[i], 20, White, 588, infoRowTops[i], 205, 37);
            }
            BoardText038(page, "MemberStageTrait", $"「 {MemberStageQualities.DominantTrait(vocal, rhythm, dance, fame)} 」",
                23, Accent, 60, 803, 382, 67, TextAnchor.MiddleCenter);

            GameObject skills = ProfileGroup038(page, "MemberSkillPanel", new Rect(47, 953, 416, 294));
            ProfileSkill038(skills.transform, "MemberSkillPrimary", firstName, firstEffect, 0, Accent);
            ProfileSkill038(skills.transform, "MemberSkillSecondary", secondName, secondEffect, 153, Cyan);
            GameObject stats = ProfileGroup038(page, "MemberStatPanel", new Rect(505, 873, 292, 265));
            string[] labels = { "声波", "说唱", "舞蹈", "名气", "颜值" };
            string[] names = { "MemberStatVocal", "MemberStatRhythm", "MemberStatPresence", "MemberStatResonance", "MemberStatCharm" };
            int[] values = { vocal, rhythm, dance, fame, beauty };
            for (int i = 0; i < labels.Length; i++)
            {
                BoardText038(stats.transform, names[i] + "Label", labels[i], 24, Muted, 12, 3 + i * 49, 145, 43);
                BoardText038(stats.transform, names[i], values[i].ToString(), 27, i == 4 ? Cyan : White,
                    160, 3 + i * 49, 116, 43, TextAnchor.MiddleRight);
            }

            GameObject passive = ProfileGroup038(page, "MemberCaptainPanel", new Rect(53, 1296, 390, 100));
            GameObject passiveBadge = new GameObject("CaptainPassiveBadge", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(ProfileEmblemFrame038));
            passiveBadge.transform.SetParent(passive.transform, false);
            PlaceTop(passiveBadge.GetComponent<RectTransform>(), 10, 20, 62, 62);
            ProfileEmblemFrame038 badgeFill = passiveBadge.GetComponent<ProfileEmblemFrame038>();
            badgeFill.SolidHex = true; badgeFill.color = new Color32(44, 18, 88, 255); badgeFill.raycastTarget = false;
            GameObject passiveFrame = new GameObject("CaptainPassiveFrame", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(ProfileEmblemFrame038));
            passiveFrame.transform.SetParent(passive.transform, false);
            PlaceTop(passiveFrame.GetComponent<RectTransform>(), 10, 20, 62, 62);
            passiveFrame.GetComponent<ProfileEmblemFrame038>().color = new Color32(158, 86, 239, 255);
            passiveFrame.GetComponent<ProfileEmblemFrame038>().raycastTarget = false;
            var passiveIcon = SkillIconVisuals.Create(passive.transform, "CaptainPassiveIcon", "被动爆发", string.Empty,
                new Color32(205, 150, 255, 255));
            passiveIcon.Kind = SkillIconKind.Burst;
            PlaceTop(passiveIcon.rectTransform, 25, 35, 32, 32);
            BoardText038(passive.transform, "CaptainPassiveTitle", "被动 · 队长特性", 20, White, 84, 12, 178, 30);
            GameObject appoint = BoardButton038("AppointCaptain", passive.transform, new Rect(268, 10, 114, 32),
                () => ConfirmCaptain(memberIndex));
            appoint.GetComponent<Button>().interactable = !captain;
            GameObject appointChip = new GameObject("Chip", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(ProfileEmblemFrame038));
            appointChip.transform.SetParent(appoint.transform, false);
            PlaceTop(appointChip.GetComponent<RectTransform>(), 0, 0, 114, 32);
            ProfileEmblemFrame038 appointChipShape = appointChip.GetComponent<ProfileEmblemFrame038>();
            appointChipShape.Slant = 5f; appointChipShape.color = new Color32(32, 15, 70, 240);
            appointChipShape.raycastTarget = false;
            BoardText038(appoint.transform, "Label", captain ? "当前队长" : inTeam ? "设为队长" : "上阵并任命", 15,
                captain ? Muted : Cyan, 0, 0, 114, 32, TextAnchor.MiddleCenter);
            Text passiveCopy = BoardText038(passive.transform, "CaptainEffectDescription", CaptainEffectCopy(member.Race), 16, Muted,
                84, 46, 298, 48);
            passiveCopy.verticalOverflow = VerticalWrapMode.Truncate;
            PanelKit.EnableBestFit(passiveCopy, 12);

            // Containing heart: neon rim + dark face, current affection number inside it.
            GameObject heartRim = new GameObject("MemberAffectionHeartRim", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(ProfileEmblemFrame038));
            heartRim.transform.SetParent(page, false);
            PlaceTop(heartRim.GetComponent<RectTransform>(), 494, 1214, 142, 134);
            ProfileEmblemFrame038 rimShape = heartRim.GetComponent<ProfileEmblemFrame038>();
            rimShape.HeartRing = true; rimShape.color = Heart; rimShape.raycastTarget = false;
            GameObject heartCore = new GameObject("MemberAffectionHeartCore", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(ProfileEmblemFrame038));
            heartCore.transform.SetParent(page, false);
            PlaceTop(heartCore.GetComponent<RectTransform>(), 508, 1228, 114, 106);
            ProfileEmblemFrame038 coreShape = heartCore.GetComponent<ProfileEmblemFrame038>();
            coreShape.SolidHeart = true; coreShape.color = new Color32(22, 8, 48, 250); coreShape.raycastTarget = false;
            Text affectionNumber = BoardText038(page, "MemberStatAffection", affection.ToString(), 46, White,
                506, 1240, 118, 66, TextAnchor.MiddleCenter);
            PanelKit.EnableBestFit(affectionNumber, 30);
            BoardText038(page, "MemberAffectionTier", $"· {model.AffectionTierOf(memberIndex)}", 27, White, 650, 1256, 148, 42);
            BoardText038(page, "MemberAffectionProgress", $"{affection} / {GameModel.MaxAffection}", 18, Muted, 676, 1218, 136, 30, TextAnchor.MiddleRight);
            ProfileBar038(page, "MemberAffectionBar", 496, 1354, 304, 8, affection / (float)GameModel.MaxAffection, Heart);

            BoardText038(page, "MemberTrainingPreview", atCap ? $"等级 {level} · 已达到等级上限" : $"等级 {level} → {level + 1} · 仅提升当前成员",
                20, White, 57, 1492, 353, 47);
            BoardText038(page, "MemberTrainingCost", atCap ? "已满级 · 无需继续训练" : $"星光币 {trainingCost:N0} · 持有 {model.Save.Gold:N0}",
                18, canTrain || atCap ? Cyan : Warn, 57, 1541, 353, 40);
            // The reference has three material apertures. The game charges only gold;
            // show actual currency, normal attack and current level rather than invented costs.
            GameObject currency = NewImage("MemberTrainingCurrency", page, PanelKit.CurrencyIcon("gold"), White);
            PlaceTop(currency.GetComponent<RectTransform>(), 455, 1460, 58, 64);
            currency.GetComponent<Image>().preserveAspect = true; currency.GetComponent<Image>().raycastTarget = false;
            BoardText038(page, "MemberTrainingGoldBalance", model.Save.Gold.ToString("N0"), 16, White, 434, 1540, 104, 37, TextAnchor.MiddleCenter);
            Text normalTitle = BoardText038(page, "MemberNormalAttack", normalName, 17, Cyan, 551, 1470, 98, 30, TextAnchor.MiddleCenter);
            PanelKit.EnableBestFit(normalTitle, 14);
            normalTitle.verticalOverflow = VerticalWrapMode.Truncate;
            Text normalDescription = BoardText038(page, "MemberNormalAttackDescription", normalEffect, 12, Muted,
                549, 1510, 103, 54, TextAnchor.MiddleCenter);
            PanelKit.EnableBestFit(normalDescription, 10);
            normalDescription.verticalOverflow = VerticalWrapMode.Truncate;
            BoardText038(page, "MemberLevelLabel", $"等级\n{level}", 23, White, 664, 1466, 100, 93, TextAnchor.MiddleCenter);

            GameObject train = BoardButton038("Train", page, new Rect(30, 1636, 200, 114), () =>
            { if (model.CanTrain(memberIndex, out _, out _)) OpenTrainingPractice(memberIndex, teamSlot); });
            train.GetComponent<Button>().interactable = canTrain;
            ProfileSemanticLabel038(train, "练习升级");
            if (!canTrain) ProfileActionState038(train.transform, atCap ? "已满级" : "星光币不足", 200, true);

            GameObject team = BoardButton038("Team", page, new Rect(258, 1636, 177, 114), () =>
            {
                if (!model.IsInTeam(memberIndex) && model.Save.Team.Count >= GameModel.TeamCapacity)
                { OpenTeamReplacement(memberIndex); return; }
                model.ToggleTeamMember(memberIndex, out string message);
                Toast(message); CloseModal(); ShowScreen(currentScreen);
            });
            ProfileSemanticLabel038(team, inTeam ? "移出编队" : "加入编队");
            GameObject replace = null;
            if (teamSlot >= 0)
            {
                // This action occupies the existing deployment row's value slot (y 586-623).
                replace = BoardButton038("ReplaceTeamMember", page, new Rect(588, 587, 205, 34),
                    () => OpenTeamSlotPicker(teamSlot));
                BoardText038(replace.transform, "Label", "更换成员 ›", 20, Cyan, 2, 1, 201, 32, TextAnchor.MiddleRight);
                Transform deployment = page.Find("MemberInfoValue-2");
                if (deployment != null) deployment.gameObject.SetActive(false);
            }
            if (!inTeam)
            {
                // Only this caption is state dependent: authored text says 移出编队.
                GameObject state = NewImage("JoinTeamCaption", page, null, new Color32(36, 9, 84, 250));
                PlaceTop(state.GetComponent<RectTransform>(), 276, 1660, 148, 61);
                state.GetComponent<Image>().raycastTarget = false;
                BoardText038(state.transform, "Label", "加入编队", 25, White, 0, 0, 148, 61, TextAnchor.MiddleCenter);
            }
            GameObject equipment = BoardButton038("MemberEquipment", page, new Rect(454, 1636, 170, 114), () =>
            {
                equipmentMember = memberIndex;
                selectedAccessoryIndex = Math.Max(0, model.EquippedAccessoryFor(memberIndex));
                ShowScreen("accessory");
            });
            ProfileSemanticLabel038(equipment, "角色饰品");
            GameObject deep = BoardButton038("MemberDeepTalkButton", page, new Rect(652, 1640, 176, 110), () =>
            {
                int before = model.AffectionOf(memberIndex);
                if (!model.DeepTalk(memberIndex, out string message, out string dialogue)) { Toast(message); return; }
                OpenDeepTalkResult(memberIndex, teamSlot, dialogue, before, model.AffectionOf(memberIndex));
            });
            bool deepBlocked = model.DeepTalkUsedToday(memberIndex) || !model.DeepTalkUnlocked(memberIndex);
            string deepState = model.DeepTalkUsedToday(memberIndex) ? "今日已交流"
                : model.DeepTalkUnlocked(memberIndex) ? "深入交流" : $"好感{GameModel.DeepTalkAffectionUnlock}解锁";
            ProfileSemanticLabel038(deep, "羁绊");
            ProfileActionState038(deep.transform, deepState, 176, deepBlocked);

            // Whole-body baked feedback: each parallelogram's frame+lettering plus its
            // live state chip/labels move as one unit (children are animated in place,
            // so transform.Find("StateChip"/"StateLabel"/"Label") keeps resolving).
            // JoinTeamCaption is a page-level sibling of Team — passed as an extra so
            // it still rides with the button it covers.
            Image profileBoard = panel.GetComponent<Image>();
            MainBakedMotion038Attach(close, profileBoard,
                MainBakedMotion038Inflate(new Rect(768, 58, 77, 82), 4f));
            MainBakedMotion038Attach(appoint, profileBoard,
                MainBakedMotion038Inflate(new Rect(321, 1306, 114, 32), 2f));
            MainBakedMotion038Attach(train, profileBoard,
                MainBakedMotion038Inflate(new Rect(30, 1636, 200, 114), 4f));
            MainBakedMotion038Attach(team, profileBoard,
                MainBakedMotion038Inflate(new Rect(258, 1636, 177, 114), 4f),
                page.Find("JoinTeamCaption"));
            if (replace != null)
                MainBakedMotion038Attach(replace, profileBoard,
                    MainBakedMotion038Inflate(new Rect(588, 587, 205, 34), 3f));
            MainBakedMotion038Attach(equipment, profileBoard,
                MainBakedMotion038Inflate(new Rect(454, 1636, 170, 114), 4f));
            MainBakedMotion038Attach(deep, profileBoard,
                MainBakedMotion038Inflate(new Rect(652, 1640, 176, 110), 4f));
        }

        private GameObject ProfileGroup038(Transform parent, string name, Rect bounds)
        {
            GameObject group = new GameObject(name, typeof(RectTransform));
            group.transform.SetParent(parent, false);
            PlaceTop(group.GetComponent<RectTransform>(), bounds.x, bounds.y, bounds.width, bounds.height);
            return group;
        }

        private void ProfileSkill038(Transform parent, string name, string skill, string effect, float y, Color accent)
        {
            GameObject card = ProfileGroup038(parent, name + "Card", new Rect(0, y, 416, 139));
            var icon = SkillIconVisuals.Create(card.transform, name + "Icon", skill, effect, accent);
            PlaceTop(icon.rectTransform, 13, 23, 88, 88);
            if (icon.Kind == SkillIconKind.Charm && ProfileGemHeart038() != null)
            {
                icon.color = Color.clear;
                GameObject gem = NewImage(name + "GemArt", card.transform, ProfileGemHeart038(), White);
                PlaceTop(gem.GetComponent<RectTransform>(), 7, 15, 101, 105);
                gem.GetComponent<Image>().preserveAspect = true;
                gem.GetComponent<Image>().raycastTarget = false;
            }
            BoardText038(card.transform, name, skill, 25, White, 125, 7, 275, 38);
            Text description = BoardText038(card.transform, name + "Description", effect, 19, Muted,
                124, 48, 276, 84, TextAnchor.UpperLeft);
            description.verticalOverflow = VerticalWrapMode.Truncate;
            PanelKit.EnableBestFit(description, 15);
        }

        private void ProfileBar038(Transform parent, string name, float x, float y, float width, float height,
            float amount, Color fillColor)
        {
            GameObject track = NewImage(name, parent, null, new Color32(46, 30, 73, 255));
            PlaceTop(track.GetComponent<RectTransform>(), x, y, width, height);
            track.GetComponent<Image>().raycastTarget = false;
            GameObject fill = NewImage(name + "Fill", track.transform, null, fillColor);
            PlaceTop(fill.GetComponent<RectTransform>(), 0, 0, width * Mathf.Clamp01(amount), height);
            fill.GetComponent<Image>().raycastTarget = false;
        }

        private void ProfileActionState038(Transform parent, string label, float width, bool blocked)
        {
            // Visible state chip inside the parallelogram's lower interior — under the
            // baked lettering, above the frame's bottom edge (board y ~1700-1740).
            GameObject chip = new GameObject("StateChip", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(ProfileEmblemFrame038));
            chip.transform.SetParent(parent, false);
            PlaceTop(chip.GetComponent<RectTransform>(), 32, 72, width - 70, 26);
            ProfileEmblemFrame038 chipShape = chip.GetComponent<ProfileEmblemFrame038>();
            chipShape.Slant = 4f;
            chipShape.color = new Color32(48, 20, 96, 230);
            chipShape.raycastTarget = false;
            BoardText038(parent, "StateLabel", label, 15, blocked ? Warn : Cyan,
                32, 72, width - 70, 26, TextAnchor.MiddleCenter);
        }

        private void ProfileSemanticLabel038(GameObject button, string caption)
        {
            // Keep action names discoverable without drawing over the authored lettering.
            Rect bounds = button.GetComponent<RectTransform>().rect;
            BoardText038(button.transform, "Label", caption, 16, Color.clear,
                3, 3, bounds.width - 6, bounds.height - 6, TextAnchor.MiddleCenter);
        }

        private Sprite profileHeartGem038;
        private Sprite ProfileGemHeart038()
        {
            if (profileHeartGem038 != null) return profileHeartGem038;
            Sprite source = Resources.Load<Sprite>("Art/AccessoryAI/Items/accessory-heart-necklace-ai-v1");
            if (source == null) return null;
            Rect r = source.rect;
            profileHeartGem038 = Sprite.Create(source.texture,
                new Rect(r.x + r.width * .07f, r.y + r.height * .10f, r.width * .70f, r.height * .43f),
                new Vector2(.5f,.5f), source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            profileHeartGem038.name = "ProfileNativeGemHeart038";
            profileHeartGem038.hideFlags = HideFlags.DontSave;
            return profileHeartGem038;
        }
    }
}
