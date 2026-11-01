using System.Collections.Generic;
using ChoSiren.Panels;
using ChoSiren.Systems.Economy;
using ChoSiren.Systems.Tactics;
using ChoSiren.UI;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>
    /// 2026-11-01 (visual-b, 赵金 1101c): the stage card under the chapter map speaks in pictures —
    /// recommended vs. team level as two coloured level chips (green when the team is ready), earned
    /// stars as star icons, drops as big icons with a rate corner badge, the active team as a team
    /// icon + name + swap glyph, chapter rewards as a star tally and tasks as a progress bar. The drop
    /// pool modal is an icon grid; its rules moved behind 「?」.
    /// </summary>
    public sealed partial class LevelMapPanel
    {
        private RectTransform paceRow1101;
        private Text rewardStars1101, taskCount1101;
        private Image taskBar1101;

        private static readonly string[] DropHelpLines =
        {
            "百分比 = 本场至少掉落 1 次的机会",
            "每场独立抽取多次，概率不能相加",
            "重复饰品会自动换成金币",
            "首通奖励只发一次；失败没有奖励",
        };

        private void BuildStageInfo1101(Transform selected, Transform team, Transform rewards, Transform tasks)
        {
            paceRow1101 = VisualB1101.Rect("StagePaceRow", selected);
            PlaceTop(paceRow1101, 14, 58, 460, 38);
            if (progressText != null) progressText.enabled = false;

            if (activeTeamText1028 != null)
            {
                VisualB1101.NeonIcon(team, "TeamIcon", "team", Accent, 14, 7, 40);
                PlaceTop(activeTeamText1028.rectTransform, 66, 4, 600, 46);
                VisualB1101.NeonIcon(team, "SwapIcon", "exchange", White, 722, 9, 36, false);
            }

            if (rewardSummaryText != null)
            {
                rewardSummaryText.enabled = false;
                RectTransform row = VisualB1101.Rect("ChapterStarTally", rewards);
                PlaceTop(row, 70, 50, 150, 32);
                VisualB1101.NeonIcon(row, "Star", "star", StarGold, 0, 2, 28, false);
                rewardStars1101 = VisualB1101.Label(row, "Value", string.Empty, 20, White, 34, 0, 110, 32);
            }
            if (taskSummaryText != null)
            {
                taskSummaryText.enabled = false;
                RectTransform row = VisualB1101.Rect("ChapterTaskTally", tasks);
                PlaceTop(row, 40, 52, 190, 30);
                taskBar1101 = VisualB1101.Bar(row, "Bar", 0, 11, 120, 8, 0f, Accent);
                taskCount1101 = VisualB1101.Label(row, "Value", string.Empty, 20, White, 130, -1, 70, 32);
            }
        }

        private void RefreshStageInfo1101(StageDefinition stage, LevelState state, string stageId, int doneTasks, int totalTasks)
        {
            if (activeTeamText1028 != null) activeTeamText1028.text = model.ActiveTeamName;
            if (rewardStars1101 != null) rewardStars1101.text = $"{ChapterStars()}/{ChapterStageCount * 3}";
            if (taskCount1101 != null) taskCount1101.text = $"{doneTasks}/{totalTasks}";
            if (taskBar1101 != null) taskBar1101.rectTransform.anchorMax = new Vector2(totalTasks > 0 ? doneTasks / (float)totalTasks : 0f, 1f);
            if (paceRow1101 == null) return;

            for (int i = paceRow1101.childCount - 1; i >= 0; i--)
            {
                GameObject old = paceRow1101.GetChild(i).gameObject;
                old.SetActive(false);
                if (Application.isPlaying) Destroy(old); else DestroyImmediate(old);
            }
            float x = 0f;
            if (stage != null && stage.UsesRealtime)
            {
                int[] party = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Where(model.Save.Team,
                    i => i >= 0 && i < GameModel.Members.Length));
                float average = party.Length > 0 ? (float)System.Linq.Enumerable.Average(party, i => model.Save.MemberLevels[i]) : 1f;
                bool ready = average >= stage.RecommendedLevel;
                x += VisualB1101.Chip(paceRow1101, "StageRecommended", "target", stage.RecommendedLevel + "级",
                    White, x, 0, 36, out _, null, 19).sizeDelta.x + 8f;
                x += VisualB1101.Chip(paceRow1101, "StageTeamLevel", "team", average.ToString("0.#") + "级",
                    ready ? VisualB1101.Good : VisualB1101.Danger, x, 0, 36, out Text teamLevel, null, 19).sizeDelta.x + 12f;
                teamLevel.color = ready ? VisualB1101.Good : VisualB1101.Danger;
            }
            else
            {
                x += VisualB1101.Chip(paceRow1101, "StageEnemyPower", "sword", model.EnemyPowerOfStage(stageId).ToString("N0"),
                    VisualB1101.Danger, x, 0, 36, out _, null, 19).sizeDelta.x + 12f;
            }
            if (state == LevelState.Cleared)
            {
                int stars = Mathf.Clamp(model.StarsOf(stageId), 1, 3);
                for (int i = 0; i < 3; i++)
                    VisualB1101.NeonIcon(paceRow1101, "Star-" + i, "star", i < stars ? StarGold : new Color(1f, 1f, 1f, .2f),
                        x + i * 30f, 4, 28, i < stars);
            }
        }

        /// <summary>One drop slot on the stage card: tier glow, big icon, tier rim, rate corner badge.</summary>
        private void DropTile1101(int i, float cx, StageLootCandidate reward, string stageId)
        {
            Color tier = RewardItemVisuals.AccentFor(reward.ItemId);
            const float icon = 84f, top = 30f;
            Image glow = NewImage("DropGlow-" + i, dropPreviewIcons, VisualB1101.Glow(), new Color(tier.r, tier.g, tier.b, .42f));
            PlaceTop(glow.rectTransform, cx - 56, top - 18, 112, 120);
            RewardItemVisuals.CreateIcon(dropPreviewIcons, reward.ItemId, "DropIcon-" + reward.ItemId, cx - icon * .5f, top, icon);
            Image rim = NewImage("DropTier-" + i, dropPreviewIcons, PunkUi.SlantFill, tier);
            PlaceTop(rim.rectTransform, cx - 42, top + icon + 4, 84, 6);
            bool sure = reward.Chance >= .995f, likely = reward.Chance >= .5f;
            RectTransform badge = VisualB1101.Rect("DropRate-" + i, dropPreviewIcons);
            PlaceTop(badge, cx - 8, top + icon - 26, 56, 28);
            Image plate = VisualB1101.Img("Plate", badge, PunkUi.SlantFill, new Color32(10, 6, 20, 240));
            Stretch(plate.rectTransform);
            Image edge = VisualB1101.Img("Edge", badge, PunkUi.SlantLine, likely ? StarGold : tier);
            Stretch(edge.rectTransform);
            Text rate = VisualB1101.Label(badge, "Rate", sure ? "必得" : $"{reward.Chance:P0}".Replace(" ", ""), 18,
                likely ? (Color)StarGold : White, 0, 0, 56, 28, TextAnchor.MiddleCenter, 1f);
            rate.rectTransform.sizeDelta = new Vector2(56, 28);
            int accessory = GameModel.AccessoryIndexForItem(reward.ItemId);
            if (accessory >= 0 && accessory == GameModel.FirstClearAccessory(stageId) && !model.IsStageCleared(stageId))
            {
                Image tag = NewImage("FirstClearTag-" + i, dropPreviewIcons, PunkUi.SlantFill, Accent);
                PlaceTop(tag.rectTransform, cx - 46, top - 14, 56, 24);
                Text first = NewPlacedText(tag.transform, "首通", 16, Brand1025.Ink, 0, 0, 56, 24, TextAnchor.MiddleCenter, FontStyle.Bold);
                first.verticalOverflow = VerticalWrapMode.Overflow;
            }
        }

        private void OpenDropPreview1101()
        {
            string stageId = StageId(selectedStage);
            StageDefinition stage = model.Tactics.FindStage(stageId);
            IReadOnlyList<StageLootCandidate> candidates = model.StageLootCandidates(stageId);
            const int columns = 4;
            int rows = Mathf.Max(1, (candidates.Count + columns - 1) / columns);
            float height = 352f + rows * 190f + 20f;
            RectTransform host = (RectTransform)transform;
            float hostH = host.rect.height > 1f ? host.rect.height : 1536f;
            GameObject card = OpenModalShell("StageLootModal", Mathf.Max(120f, (hostH - height) * .5f), height);

            // Header: stage id with a gift glyph, 「?」 for the rules.
            VisualB1101.NeonIcon(card.transform, "TitleIcon", "gift", Accent, 28, 24, 44);
            Text title = VisualB1101.Label(card.transform, "Title", $"{chapter}-{selectedStage}", 34, White, 84, 20, 300, 52);
            SirenStickerUi.SkinTitle(title);
            VisualB1101.HelpButton(card.transform, "DropHelp", 520, 24, 42, "掉落说明", DropHelpLines);

            // First clear: two big tiles, checked off once claimed.
            bool cleared = model.IsStageCleared(stageId);
            VisualB1101.Chip(card.transform, "FirstClearChip", "flag", "首通", Accent, 28, 92, 36, out _, null, 19);
            FirstClearTile1101(card.transform, "FirstClearDiamonds", CurrencyIds.Diamond, $"×{stage?.DiamondFirstClear ?? 0}", 28, cleared);
            int equipment = GameModel.FirstClearAccessory(stageId);
            if (equipment >= 0)
                FirstClearTile1101(card.transform, "FirstClearEquipment", GameModel.AccessoryItemIds[equipment], "×1", 190, cleared);

            // Every win: gold + number of pool rolls.
            VisualB1101.Chip(card.transform, "PerWinChip", "bolt", "每胜", StarGold, 370, 92, 36, out _, null, 19);
            Image coin = VisualB1101.Img("WinGold", card.transform, RewardItemVisuals.SpriteFor(CurrencyIds.Gold), Color.white);
            coin.preserveAspect = true;
            PlaceTop(coin.rectTransform, 372, 148, 52, 52);
            VisualB1101.Label(card.transform, "WinGoldValue", model.PreviewStageGoldReward(stageId).ToString("N0"), 30, StarGold,
                432, 148, 180, 52);
            VisualB1101.NeonIcon(card.transform, "RollIcon", "dice", Accent, 374, 216, 48);
            VisualB1101.Label(card.transform, "RollValue", "×" + (stage?.Drops?.Rolls ?? 0), 30, White, 432, 214, 180, 52);

            ReferencePunkFx.Beam(card.transform, "PoolBeam", new Vector2(28, 316), new Vector2(632, 316), 1.4f,
                new Color32(152, 105, 226, 110));

            // Pool grid: icon, rate badge, short name; tap a tile for its stats.
            const float tileW = 142f, tileH = 176f, gap = 12f, gridTop = 336f;
            for (int i = 0; i < candidates.Count; i++)
            {
                StageLootCandidate candidate = candidates[i];
                Color tier = RewardItemVisuals.AccentFor(candidate.ItemId);
                GameObject tile = ModalPolish038Chrome.RowPlate(card.transform, "LootCandidate-" + candidate.ItemId);
                PlaceTop(tile.GetComponent<RectTransform>(), 28 + (i % columns) * (tileW + gap), gridTop + (i / columns) * (tileH + gap), tileW, tileH);
                Image glow = NewImage("Glow", tile.transform, VisualB1101.Glow(), new Color(tier.r, tier.g, tier.b, .35f));
                PlaceTop(glow.rectTransform, 6, 0, tileW - 12, 126);
                RewardItemVisuals.CreateIcon(tile.transform, candidate.ItemId, "RewardArt", (tileW - 96) * .5f, 14, 96);
                Image bar = NewImage("RarityBar", tile.transform, PunkUi.SlantFill, tier);
                PlaceTop(bar.rectTransform, 10, tileH - 12, tileW - 20, 6);
                Text name = VisualB1101.Label(tile.transform, "Name", candidate.Name, 18, tier, 6, 116, tileW - 12, 40,
                    TextAnchor.MiddleCenter, 1f);
                name.horizontalOverflow = HorizontalWrapMode.Wrap;
                PanelKit.EnableBestFit(name, 12);
                RectTransform badge = VisualB1101.Rect("Rate", tile.transform);
                PlaceTop(badge, tileW - 66, 6, 60, 28);
                Image plate = VisualB1101.Img("Plate", badge, PunkUi.SlantFill, new Color32(10, 6, 20, 240));
                Stretch(plate.rectTransform);
                Image edge = VisualB1101.Img("Edge", badge, PunkUi.SlantLine, candidate.Chance >= .5f ? StarGold : Accent);
                Stretch(edge.rectTransform);
                VisualB1101.Label(badge, "Value", candidate.Chance >= .995f ? "必得" : $"{candidate.Chance:P0}".Replace(" ", ""), 18,
                    candidate.Chance >= .5f ? (Color)StarGold : White, 0, 0, 60, 28, TextAnchor.MiddleCenter, 1f)
                    .rectTransform.sizeDelta = new Vector2(60, 28);
                string[] lines = { candidate.Use, "每次掉落 " + candidate.AmountLabel, $"本场至少 1 次 {candidate.Chance:P1}" };
                VisualB1101.MakeButton(tile, () => VisualB1101.ShowHelp(tile.transform, candidate.Name, lines));
                VisualB1101.Pop((RectTransform)tile.transform, .03f * i, .8f);
            }
        }

        private void FirstClearTile1101(Transform card, string name, string itemId, string amount, float x, bool claimed)
        {
            GameObject tile = ModalPolish038Chrome.RowPlate(card, name);
            PlaceTop(tile.GetComponent<RectTransform>(), x, 140, 150, 160);
            Color tier = RewardItemVisuals.AccentFor(itemId);
            Image glow = NewImage("Glow", tile.transform, VisualB1101.Glow(), new Color(tier.r, tier.g, tier.b, claimed ? .12f : .4f));
            PlaceTop(glow.rectTransform, 0, 0, 150, 120);
            Image icon = RewardItemVisuals.CreateIcon(tile.transform, itemId, "RewardArt", 27, 12, 96);
            if (claimed) icon.color = new Color(1, 1, 1, .4f);
            VisualB1101.Label(tile.transform, "Amount", amount, 24, claimed ? Muted : White, 0, 112, 150, 40, TextAnchor.MiddleCenter);
            if (claimed) VisualB1101.NeonIcon(tile.transform, "Claimed", "check", Accent, 45, 30, 60);
        }
    }
}
