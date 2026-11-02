using System.Collections;
using ChoSiren.Panels;
using ChoSiren.Systems.Economy;
using ChoSiren.Systems.Tactics;
using ChoSiren.UI;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    public sealed partial class LevelMapPanel
    {
        private bool vinylMap1102;
        private StageSceneArt1102 stageScene1102;
        private Text sceneTitle1102, sceneNumber1102;
        private readonly Image[] teamFaces1102 = new Image[GameModel.TeamCapacity];
        private readonly Text[] teamNames1102 = new Text[GameModel.TeamCapacity];
        private readonly int[] teamMembers1102 = { -2, -2, -2, -2 };
        private RectTransform teamSwapGlyph1102;
        private Image teamSwapGlow1102;
        private bool teamSwapAnimating1102;
        private VinylStageWheel1102 vinylWheel1102;

        // The approved B composition: a circular record clipped by the right screen edge.
        // It is a real circle. Only its location changes; its width and height always match.
        private void BuildVinylMap1102()
        {
            authoredMap = vinylMap1102 = true;
            Canvas.ForceUpdateCanvases();
            RectTransform host = (RectTransform)transform;
            float w = host.rect.width > 1 ? host.rect.width : 720f;
            float h = host.rect.height > 1 ? host.rect.height : 1536f;
            var background = new GameObject("LevelScene", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            background.transform.SetParent(transform, false);
            Stretch((RectTransform)background.transform);
            background.GetComponent<RawImage>().raycastTarget = true;
            stageScene1102 = background.AddComponent<StageSceneArt1102>();
            var page = NewObject("VinylMapPage1102", transform).AddComponent<RectTransform>();
            float scale = Mathf.Min(w / 858f, h / 1834f);
            PlaceTop(page, (w - 858f * scale) * .5f, (h - 1834f * scale) * .5f, 858, 1834);
            page.localScale = Vector3.one * scale;
            // Clip at the actual viewport, not the centred design page on wider phones/PC.
            if (host.GetComponent<RectMask2D>() == null) host.gameObject.AddComponent<RectMask2D>();
            float edgeExtra = (w / scale - 858f) * .5f;

            GameObject back = MapBoardHit("Back", page, 32, 28, 84, 80, Close);
            SirenStickerUi.SkinButton(back, false, true);
            NewPlacedText(back.transform, "‹", 62, White, 0, -6, 84, 80, TextAnchor.MiddleCenter, FontStyle.Bold).verticalOverflow = VerticalWrapMode.Overflow;
            GameObject switcher = MapBoardHit("ChapterSwitch", page, 142, 32, 306, 74,
                OpenVinylChapterPicker1102);
            SirenStickerUi.SkinButton(switcher, false, true);
            chapterSwitchLabel = NewPlacedText(switcher.transform, "", 26, White, 12, 0, 282, 74, TextAnchor.MiddleCenter, FontStyle.Bold);
            chapterSwitchLabel.name = "ChapterSwitchLabel";
            Image staminaIcon = NewImage("StaminaIcon", page, Resources.Load<Sprite>("Art/UI/ResourceStamina-C"), White);
            PlaceTop(staminaIcon.rectTransform, 620, 46, 46, 46); staminaIcon.preserveAspect = true;
            staminaText = NewPlacedText(page, "", 30, White, 674, 40, 158, 58, TextAnchor.MiddleLeft, FontStyle.Bold);
            Readable(staminaText);
            NewPlacedText(page, $"CHAPTER / {chapter:00}", 21, Accent, 42, 188, 350, 40, TextAnchor.MiddleLeft);
            Text chapterTitle = NewPlacedText(page, chapter == 1 ? "欲望都市" : "魅声海湾", 67, White,
                40, 237, 430, 104, TextAnchor.MiddleLeft, FontStyle.BoldAndItalic);
            Readable(chapterTitle);
            var rule = NewImage("ChapterAccent", page, null, Accent);
            PlaceTop(rule.rectTransform, 44, 354, 85, 7);
            GameObject story = MapBoardHit("StoryChapter-0" + chapter, page, 36, 378, 240, 52, OpenStoryChapter01);
            storyChapterLabel = NewPlacedText(story.transform, "", 21, White, 0, 0, 240, 52, TextAnchor.MiddleLeft);
            Readable(storyChapterLabel);

            Image wheelSurface = NewImage("VinylWheelInput1102", page, null, Color.clear);
            PlaceTop(wheelSurface.rectTransform, 296 + edgeExtra, 255, 1124, 1124);
            wheelSurface.raycastTarget = true;
            vinylWheel1102 = wheelSurface.gameObject.AddComponent<VinylStageWheel1102>();
            Image disc = NewImage("HalfVinyl1102", wheelSurface.transform,
                Resources.Load<Sprite>("Art/LevelMap1102/vinyl"), Color.white);
            PlaceTop(disc.rectTransform, 0, 0, 1124, 1124);
            CenterPivot(disc.rectTransform);
            disc.preserveAspect = true;
            var wheelNodes = new RectTransform[ChapterStageCount];
            var wheelButtons = new Button[ChapterStageCount];
            for (int i = 0; i < ChapterStageCount; i++)
            {
                int stage = i + 1;
                // No EventTrigger on these buttons: drag events must reach the shared record.
                GameObject node = MapBoardHit($"Level-{chapter}-{stage}", wheelSurface.transform,
                    0, 0, 122, 122, () => SelectVinylStage1102(stage));
                Image hit = node.GetComponent<Image>();
                hit.sprite = VisualA1101.TornSprite; hit.type = Image.Type.Simple;
                hit.color = new Color32(190, 165, 222, 255);
                Outline rim = node.AddComponent<Outline>();
                rim.effectColor = new Color32(137, 115, 177, 230); rim.effectDistance = new Vector2(1.5f, -1.5f);
                Image glow = NewImage("SelectedRim", node.transform, VisualA1101.TornSprite, Accent);
                glow.type = Image.Type.Simple;
                PlaceTop(glow.rectTransform, -6, -6, 134, 134); glow.transform.SetAsFirstSibling();
                Image core = NewImage("Core", node.transform, RoundedSprite(48), new Color32(14, 13, 29, 255));
                core.type = Image.Type.Simple;
                PlaceTop(core.rectTransform, 9, 9, 104, 104);
                Image nick = NewImage("PaperNick", node.transform, PunkUi.SlantFill, new Color32(238, 224, 255, 240));
                PlaceTop(nick.rectTransform, 86, 9, 29, 6); CenterPivot(nick.rectTransform);
                nick.rectTransform.localEulerAngles = new Vector3(0, 0, -28f);
                Text label = NewPlacedText(node.transform, $"{chapter}-{stage}", 39, White,
                    0, 15, 122, 57, TextAnchor.MiddleCenter, FontStyle.Bold);
                label.verticalOverflow = VerticalWrapMode.Overflow;
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                Text status = NewPlacedText(node.transform, "", 22, StarGold, 0, 78, 122, 36, TextAnchor.MiddleCenter);
                status.verticalOverflow = VerticalWrapMode.Overflow;
                wheelNodes[i] = node.GetComponent<RectTransform>(); wheelButtons[i] = node.GetComponent<Button>();
                nodeViews[stage] = new NodeView { Stage = stage, Button = wheelButtons[i], Background = core,
                    Glow = glow, Outline = rim, StageLabel = label, StatusLabel = status };
            }
            vinylWheel1102.Configure(disc.rectTransform, wheelNodes, wheelButtons, selectedStage,
                SelectVinylStage1102, stage => StateFor(stage) != LevelState.Locked);
            sceneNumber1102 = NewPlacedText(page, "", 20, Accent, 42, 701, 255, 40, TextAnchor.MiddleLeft, FontStyle.Bold);
            sceneTitle1102 = NewPlacedText(page, "", 48, White, 39, 750, 255, 166, TextAnchor.UpperLeft, FontStyle.Bold);
            sceneTitle1102.horizontalOverflow = HorizontalWrapMode.Wrap;
            Readable(sceneTitle1102);

            GameObject team = MapBoardHit("StageTeamPreset", page, 40, 1268, 490, 140, OpenVinylTeamPresets1102);
            SirenStickerUi.SkinPanel(team, false);
            activeTeamText1028 = NewPlacedText(team.transform, "", 23, White, 66, 4, 600, 46, TextAnchor.MiddleLeft);
            activeTeamText1028.name = "ActiveTeamName";
            activeTeamText1028.supportRichText = false;
            GameObject selected = NewPanel("SelectedLevel", page, SirenStickerUi.Ink, 0);
            PlaceTop(selected.GetComponent<RectTransform>(), 40, 1420, 490, 310);
            SirenStickerUi.SkinPanel(selected, false);
            stageText = NewPlacedText(selected.transform, "", 31, White, 14, 0, 96, 52, TextAnchor.MiddleLeft, FontStyle.Bold);
            stageTitleText = NewPlacedText(selected.transform, "", 27, White, 114, 0, 355, 52, TextAnchor.MiddleLeft, FontStyle.Bold);
            stageStatusText = NewPlacedText(selected.transform, "", 20, Accent, 14, 58, 460, 36, TextAnchor.MiddleLeft);
            progressText = NewPlacedText(selected.transform, "", 20, White, 14, 58, 460, 36, TextAnchor.MiddleLeft);
            equipmentDropText = NewPlacedText(selected.transform, "", 18, Cyan, 14, 114, 460, 30, TextAnchor.MiddleLeft);
            GameObject drops = MapBoardHit("StageDropPreview", selected.transform, 10, 94, 470, 165, OpenDropPreview);
            dropPreviewIcons = drops.transform;
            RewardItemVisuals.CreateIcon(selected.transform, CurrencyIds.Stamina, "CostStaminaIcon", 14, 264, 30);
            staminaCostText = NewPlacedText(selected.transform, "", 22, White, 50, 262, 100, 36, TextAnchor.MiddleLeft);
            RewardItemVisuals.CreateIcon(selected.transform, CurrencyIds.Diamond, "RewardDiamondIcon", 174, 264, 30);
            diamondRewardText = NewPlacedText(selected.transform, "", 22, Cyan, 210, 262, 100, 36, TextAnchor.MiddleLeft);
            RewardItemVisuals.CreateIcon(selected.transform, CurrencyIds.Gold, "RewardGoldIcon", 320, 264, 30);
            goldRewardText = NewPlacedText(selected.transform, "", 22, StarGold, 356, 262, 120, 36, TextAnchor.MiddleLeft);
            GameObject start = MapBoardHit("StartChallenge", page, 548, 1504, 270, 110, StartChallenge);
            SirenStickerUi.SkinButton(start, true, false);
            startButton = start.GetComponent<Button>(); startBackground = start.GetComponent<Image>();
            startLabel = NewPlacedText(start.transform, "开始挑战", 36, Brand1025.Ink, 10, 0, 250, 110, TextAnchor.MiddleCenter, FontStyle.Bold);
            GameObject rewards = MapBoardHit("ChapterRewards", page, 40, 1750, 370, 70, OpenChapterRewards);
            GameObject tasks = MapBoardHit("ChapterTasks", page, 440, 1750, 378, 70, OpenChapterTasks);
            SirenStickerUi.SkinButton(rewards, false, true); SirenStickerUi.SkinButton(tasks, false, true);
            NewPlacedText(rewards.transform, "章节奖励", 24, White, 20, 0, 160, 66, TextAnchor.MiddleCenter, FontStyle.Bold);
            NewPlacedText(tasks.transform, "章节任务", 24, White, 20, 0, 160, 66, TextAnchor.MiddleCenter, FontStyle.Bold);
            rewardSummaryText = NewPlacedText(rewards.transform, "", 20, StarGold, 190, 0, 130, 66, TextAnchor.MiddleLeft);
            taskSummaryText = NewPlacedText(tasks.transform, "", 20, Accent, 190, 0, 130, 66, TextAnchor.MiddleLeft);
            rewardBadgeText = NewNotificationBadge(rewards.transform, "RewardBadge", 326, 5);
            taskBadgeText = NewNotificationBadge(tasks.transform, "TaskBadge", 334, 5);
            BuildStageInfo1101(selected.transform, team.transform, rewards.transform, tasks.transform);
            BuildVinylTeamStrip1102(team.transform);
            PlaceTop(rewardStars1101.transform.parent as RectTransform, 188, 18, 150, 32);
            PlaceTop(taskCount1101.transform.parent as RectTransform, 174, 18, 190, 30);
            BuildSweepRow(page);
            PlaceTop(sweepRow.GetComponent<RectTransform>(), 548, 1644, 270, 52);
            sweepRow.transform.localScale = Vector3.one * .81f;
        }

        private void RefreshVinylMap1102(StageDefinition stage)
        {
            if (!vinylMap1102) return;
            RefreshVinylTeamStrip1102();
            vinylWheel1102?.ShowStage(selectedStage);
            string title = stage?.Name ?? "演出";
            int gap = title.IndexOf(' '); if (gap >= 0) title = title.Substring(gap + 1);
            stageTitleText.text = sceneTitle1102.text = title;
            sceneNumber1102.text = $"TRACK / {chapter}-{selectedStage}";
            chapterSwitchLabel.text = $"第 {chapter:00} 章   ▾";
            startLabel.text = StateFor(selectedStage) == LevelState.Cleared ? "再次挑战" : "开始挑战";
            stageScene1102.Show(StageId(selectedStage));
            foreach (var pair in nodeViews)
            {
                var v = pair.Value; bool selected = v.Stage == selectedStage;
                v.Glow.enabled = selected;
                v.Background.color = selected ? new Color32(8, 48, 56, 255) : new Color32(15, 11, 28, 250);
                v.StageLabel.color = v.State == LevelState.Locked ? Muted : White;
                v.StatusLabel.text = v.State == LevelState.Locked ? "锁" : v.State == LevelState.Cleared
                    ? new string('★', Mathf.Clamp(model.StarsOf(StageId(v.Stage)), 1, 3)) : "NEXT";
                v.StatusLabel.color = v.State == LevelState.Cleared ? StarGold : selected ? Accent : Muted;
                v.Outline.effectColor = selected ? Accent : new Color32(137, 115, 177, 200);
            }
        }

        private void OpenVinylChapterPicker1102()
        {
            float height = 178f + ChapterCount * 94f;
            RectTransform host = (RectTransform)transform;
            float hostHeight = host.rect.height > 1f ? host.rect.height : 1536f;
            GameObject card = OpenModalShell("ChapterPicker1102", Mathf.Max(80f, (hostHeight - height) * .5f), height);
            SirenStickerUi.SkinTitle(NewPlacedText(card.transform, "章节", 30, White,
                32, 24, 400, 60, TextAnchor.MiddleLeft, FontStyle.Bold));
            for (int number = 1; number <= ChapterCount; number++)
            {
                int captured = number;
                bool current = number == chapter;
                bool unlocked = ChapterUnlocked(number);
                string name = number == 1 ? "欲望都市" : number == 2 ? "魅声海湾" : "全新舞台";
                string state = current ? " · 当前" : unlocked ? "" : " · 锁定";
                GameObject option = NewButton("ChooseChapter-" + number, card.transform,
                    $"第{number:00}章  {name}{state}", 24, new Color32(35, 19, 59, 255),
                    current ? Accent : White, () =>
                    {
                        CloseChapterModal();
                        SwitchChapter(captured);
                    }, 12);
                PlaceTop(option.GetComponent<RectTransform>(), 32, 104 + (number - 1) * 94, 576, 76);
                option.GetComponent<Button>().interactable = unlocked;
            }
        }

        private void SelectVinylStage1102(int stage)
        {
            SelectStage(stage);
            vinylWheel1102?.ShowStage(selectedStage);
        }

        private void ExchangeStroke1102(Transform parent, float x, float y, float w, float h, float angle)
        {
            Image stroke = NewImage("ExchangeStroke", parent, null, Accent);
            PlaceTop(stroke.rectTransform, x, y, w, h);
            CenterPivot(stroke.rectTransform);
            stroke.rectTransform.localEulerAngles = new Vector3(0, 0, angle);
        }

        private void BuildVinylTeamStrip1102(Transform team)
        {
            activeTeamText1028.enabled = false;
            team.Find("TeamIcon")?.gameObject.SetActive(false);
            team.Find("SwapIcon")?.gameObject.SetActive(false);
            for (int slot = 0; slot < GameModel.TeamCapacity; slot++)
            {
                RectTransform member = VisualB1101.Rect("StageTeamMember-" + slot, team);
                PlaceTop(member, 20 + slot * 110, 4, 108, 132);
                Image rim = NewImage("AvatarRim", member, RoundedSprite(30),
                    slot == 0 ? Accent : new Color32(156, 104, 255, 180));
                rim.type = Image.Type.Simple;
                PlaceTop(rim.rectTransform, 8, 2, 92, 92);
                Image window = NewImage("AvatarWindow", member, RoundedSprite(30), Color.white);
                window.type = Image.Type.Simple;
                PlaceTop(window.rectTransform, 10, 4, 88, 88);
                window.gameObject.AddComponent<Mask>().showMaskGraphic = false;
                teamFaces1102[slot] = NewImage("Avatar", window.transform, null, White);
                teamNames1102[slot] = NewPlacedText(member, "", 24, White,
                    0, 96, 108, 36, TextAnchor.MiddleCenter, FontStyle.Bold);
                teamNames1102[slot].name = "MemberName";
                teamNames1102[slot].verticalOverflow = VerticalWrapMode.Overflow;
                PanelKit.EnableBestFit(teamNames1102[slot], 18);
                teamMembers1102[slot] = -2;
            }
            GameObject swap = MapBoardHit("StageTeamSwap", team, 600, 26, 88, 88, OpenVinylTeamPresets1102);
            teamSwapGlow1102 = NewImage("SwapGlow", swap.transform, VisualB1101.Glow(),
                new Color(Accent.r, Accent.g, Accent.b, .12f));
            PlaceTop(teamSwapGlow1102.rectTransform, -8, -8, 104, 104);
            RectTransform holder = VisualB1101.Rect("SwapGlyph", swap.transform);
            PlaceTop(holder, 14, 18, 60, 52);
            teamSwapGlyph1102 = VisualB1101.Rect("Glyph", holder);
            PlaceTop(teamSwapGlyph1102, 0, 0, 60, 52);
            CenterPivot(teamSwapGlyph1102);
            ExchangeStroke1102(teamSwapGlyph1102, 8, 14, 44, 4, 0);
            ExchangeStroke1102(teamSwapGlyph1102, 44, 9, 16, 4, -40);
            ExchangeStroke1102(teamSwapGlyph1102, 44, 19, 16, 4, 40);
            ExchangeStroke1102(teamSwapGlyph1102, 8, 34, 44, 4, 0);
            ExchangeStroke1102(teamSwapGlyph1102, 4, 29, 16, 4, 40);
            ExchangeStroke1102(teamSwapGlyph1102, 4, 39, 16, 4, -40);
        }

        private void RefreshVinylTeamStrip1102()
        {
            for (int slot = 0; slot < GameModel.TeamCapacity; slot++)
            {
                Image portrait = teamFaces1102[slot];
                if (portrait == null) continue;
                int index = slot < model.Save.Team.Count ? model.Save.Team[slot] : -1;
                if (index < 0 || index >= GameModel.Members.Length) index = -1;
                if (teamMembers1102[slot] == index) continue;
                teamMembers1102[slot] = index;
                bool valid = index >= 0;
                portrait.gameObject.SetActive(valid);
                teamNames1102[slot].text = valid ? GameModel.Members[index].Name : "空位";
                teamNames1102[slot].color = valid ? White : Muted;
                if (valid)
                {
                    var member = GameModel.Members[index];
                    portrait.sprite = PanelKit.MemberSpriteOrNull(member.Id, true);
                    PanelKit.FrameBustPortrait(portrait, (RectTransform)portrait.transform.parent, member.Id);
                }
                VisualB1101.Pop((RectTransform)portrait.transform.parent.parent, .025f * slot, .94f);
            }
        }

        private void OpenVinylTeamPresets1102()
        {
            if (teamSwapAnimating1102) return;
            StartCoroutine(AnimateVinylTeamSwap1102());
        }

        private IEnumerator AnimateVinylTeamSwap1102()
        {
            teamSwapAnimating1102 = true;
            float elapsed = 0f;
            while (elapsed < .32f && teamSwapGlyph1102 != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / .32f);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                teamSwapGlyph1102.localEulerAngles = new Vector3(0, 0, -360f * eased);
                teamSwapGlyph1102.localScale = Vector3.one * (1f + .14f * Mathf.Sin(t * Mathf.PI));
                teamSwapGlow1102.color = new Color(Accent.r, Accent.g, Accent.b,
                    .12f + .54f * Mathf.Sin(t * Mathf.PI));
                yield return null;
            }
            if (teamSwapGlyph1102 != null)
            {
                teamSwapGlyph1102.localEulerAngles = Vector3.zero;
                teamSwapGlyph1102.localScale = Vector3.one;
                teamSwapGlow1102.color = new Color(Accent.r, Accent.g, Accent.b, .12f);
            }
            teamSwapAnimating1102 = false;
            OpenStageTeamPresets1028();
        }
    }
}
