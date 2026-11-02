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
            page.gameObject.AddComponent<RectMask2D>();

            GameObject back = MapBoardHit("Back", page, 32, 28, 84, 80, Close);
            SirenStickerUi.SkinButton(back, false, true);
            NewPlacedText(back.transform, "‹", 62, White, 0, -6, 84, 80, TextAnchor.MiddleCenter, FontStyle.Bold).verticalOverflow = VerticalWrapMode.Overflow;
            GameObject switcher = MapBoardHit("ChapterSwitch", page, 142, 32, 306, 74,
                () => SwitchChapter(chapter == 1 ? 2 : 1));
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

            Image disc = NewImage("HalfVinyl1102", page, Resources.Load<Sprite>("Art/LevelMap1102/vinyl"), Color.white);
            PlaceTop(disc.rectTransform, 296, 255, 1124, 1124);
            disc.preserveAspect = true;
            for (int i = 0; i < ChapterStageCount; i++)
            {
                int stage = i + 1;
                float angle = (-105f - i * 150f / (ChapterStageCount - 1)) * Mathf.Deg2Rad;
                float x = 858 + Mathf.Cos(angle) * 480, y = 817 + Mathf.Sin(angle) * 480;
                GameObject node = NewPanelButton($"Level-{chapter}-{stage}", page, new Color32(16, 10, 31, 245), 48, () => SelectStage(stage));
                node.GetComponent<Image>().type = Image.Type.Simple;
                PlaceTop(node.GetComponent<RectTransform>(), x - 45, y - 45, 90, 90);
                Outline rim = node.AddComponent<Outline>(); rim.effectColor = new Color32(137, 115, 177, 230); rim.effectDistance = new Vector2(1.5f, -1.5f);
                Image glow = NewImage("SelectedRim", node.transform, RoundedSprite(48), Accent);
                glow.type = Image.Type.Simple;
                PlaceTop(glow.rectTransform, -5, -5, 100, 100); glow.transform.SetAsFirstSibling();
                Image core = NewImage("Core", node.transform, RoundedSprite(48), new Color32(14, 13, 29, 255));
                core.type = Image.Type.Simple;
                Stretch(core.rectTransform);
                Text label = NewPlacedText(node.transform, $"{chapter}-{stage}", 31, White, 0, 8, 90, 44, TextAnchor.MiddleCenter, FontStyle.Bold);
                label.verticalOverflow = VerticalWrapMode.Overflow;
                Text status = NewPlacedText(node.transform, "", 18, StarGold, 0, 51, 90, 34, TextAnchor.MiddleCenter);
                status.verticalOverflow = VerticalWrapMode.Overflow;
                nodeViews[stage] = new NodeView { Stage = stage, Button = node.GetComponent<Button>(), Background = core,
                    Glow = glow, Outline = rim, StageLabel = label, StatusLabel = status };
            }
            sceneNumber1102 = NewPlacedText(page, "", 20, Accent, 42, 701, 255, 40, TextAnchor.MiddleLeft, FontStyle.Bold);
            sceneTitle1102 = NewPlacedText(page, "", 48, White, 39, 750, 255, 166, TextAnchor.UpperLeft, FontStyle.Bold);
            sceneTitle1102.horizontalOverflow = HorizontalWrapMode.Wrap;
            Readable(sceneTitle1102);

            GameObject team = MapBoardHit("StageTeamPreset", page, 40, 1352, 778, 52, OpenStageTeamPresets1028);
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
            PlaceTop(rewardStars1101.transform.parent as RectTransform, 188, 18, 150, 32);
            PlaceTop(taskCount1101.transform.parent as RectTransform, 174, 18, 190, 30);
            BuildSweepRow(page);
            PlaceTop(sweepRow.GetComponent<RectTransform>(), 548, 1644, 270, 52);
            sweepRow.transform.localScale = Vector3.one * .81f;
        }

        private void RefreshVinylMap1102(StageDefinition stage)
        {
            if (!vinylMap1102) return;
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
    }
}
