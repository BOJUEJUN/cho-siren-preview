using System;
using System.Collections.Generic;
using ChoSiren.Systems.Economy;
using ChoSiren.Panels;
using ChoSiren.Systems.Story;
using ChoSiren.Systems.Tactics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>
    /// Self-contained neon city chapter map. The panel owns every object it creates and can be
    /// opened from the existing application with one call:
    /// LevelMapPanel.Open(safeRoot, model, null, Toast);
    /// </summary>
    public sealed class LevelMapPanel : MonoBehaviour
    {
        private enum LevelState
        {
            Cleared,
            Current,
            Locked,
        }

        private sealed class NodeView
        {
            public int Stage;
            public LevelState State;
            public Button Button;
            public Image Background;
            public Image Glow;
            public Outline Outline;
            public Text StageLabel;
            public Text StatusLabel;
        }

        private sealed class RouteView
        {
            public int TargetStage;
            public Image Glow;
            public Image Core;
            public Image Marker;
        }

        private static readonly Color White = new Color32(249, 247, 255, 255);
        private static readonly Color Muted = new Color32(171, 181, 213, 255);
        private static readonly Color Pink = new Color32(255, 112, 220, 255);
        private static readonly Color Cyan = new Color32(104, 198, 255, 255);
        private static readonly Color Glass = new Color32(8, 18, 48, 232);
        private const int ChapterStageCount = GameModel.ChapterOneStageCount;

        private readonly Dictionary<int, Sprite> roundedSprites = new Dictionary<int, Sprite>();
        private readonly Dictionary<int, NodeView> nodeViews = new Dictionary<int, NodeView>();
        private readonly List<RouteView> routeViews = new List<RouteView>();
        private readonly List<UnityEngine.Object> generatedAssets = new List<UnityEngine.Object>();

        private GameModel model;
        private Action onBack;
        private Action<string> onMessage;
        private Font font;
        private bool built;
        private bool challengeOpen;
        private bool closing;
        private int selectedStage = 1;

        private Text staminaText;
        private Text stageText;
        private Text stageStatusText;
        private Text progressText;
        private Text staminaCostText;
        private Text diamondRewardText;
        private Text goldRewardText;
        private Text startLabel;
        private Text stageTitleText;
        private Text storyChapterLabel;
        private Text rewardSummaryText;
        private Text taskSummaryText;
        private Text rewardBadgeText;
        private Text taskBadgeText;
        private Button startButton;
        private Image startBackground;
        private Image currentGlow;
        private Sprite stageNodeSprite;
        private Sprite rewardChestSprite;
        private Sprite actionFrameSprite;
        private GameObject modalRoot;
        private GameObject toastObject;
        private Text toastText;
        private float toastHideAt;
        private string lastSettleMessage = string.Empty;

        /// <summary>
        /// Opens one panel below <paramref name="host"/>. Calling Open again reuses the existing
        /// panel instead of stacking overlays. The host is normally ChoSirenApp's safeRoot.
        /// </summary>
        public static LevelMapPanel Open(Transform host, GameModel gameModel, Action back = null,
            Action<string> message = null)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            if (gameModel == null) throw new ArgumentNullException(nameof(gameModel));

            LevelMapPanel existing = host.GetComponentInChildren<LevelMapPanel>(true);
            if (existing != null && existing.isActiveAndEnabled)
            {
                existing.gameObject.SetActive(true);
                existing.transform.SetAsLastSibling();
                existing.Bind(gameModel, back, message);
                return existing;
            }

            GameObject panelObject = new GameObject("LevelMapPanel", typeof(RectTransform), typeof(CanvasGroup));
            panelObject.transform.SetParent(host, false);
            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            Stretch(panelRect);

            CanvasGroup canvasGroup = panelObject.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;

            LevelMapPanel panel = panelObject.AddComponent<LevelMapPanel>();
            panel.Bind(gameModel, back, message);
            return panel;
        }

        public int SelectedStage => selectedStage;

        public void Close()
        {
            if (closing) return;
            closing = true;
            Action callback = onBack;
            gameObject.SetActive(false);
            Destroy(gameObject);
            callback?.Invoke();
        }

        private void Bind(GameModel gameModel, Action back, Action<string> message)
        {
            if (model != null) model.Changed -= HandleModelChanged;
            model = gameModel;
            onBack = back;
            onMessage = message;
            model.Changed += HandleModelChanged;
            selectedStage = CurrentStoryStage();

            if (!built)
            {
                Build();
                built = true;
            }

            Refresh();
        }

        private void Update()
        {
            float pulse = 0.5f + Mathf.Sin(Time.unscaledTime * 2.8f) * 0.5f;
            if (currentGlow != null)
            {
                Color color = currentGlow.color;
                color.a = Mathf.Lerp(0.18f, 0.48f, pulse);
                currentGlow.color = color;
                currentGlow.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.96f, 1.08f, pulse);
            }

            if (toastObject != null && toastObject.activeSelf && Time.unscaledTime >= toastHideAt)
                toastObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (model != null) model.Changed -= HandleModelChanged;
            for (int index = 0; index < generatedAssets.Count; index++)
            {
                UnityEngine.Object asset = generatedAssets[index];
                if (asset == null) continue;
                if (Application.isPlaying) Destroy(asset);
                else DestroyImmediate(asset);
            }
        }

        private void Build()
        {
            font = Resources.Load<Font>("Fonts/NotoSansSC-Subset") ??
                   Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            stageNodeSprite = Resources.Load<Sprite>("Art/LevelMapAI/stage-node-frame-ai-v1");
            rewardChestSprite = Resources.Load<Sprite>("Art/LevelMapAI/chapter-reward-chest-ai-v1");
            actionFrameSprite = Resources.Load<Sprite>("Art/LevelMapAI/chapter-action-frame-ai-v1");

            BuildBackdrop();
            BuildHeader();
            BuildMap();
            BuildDetails();
            BuildChapterDock();
            BuildToast();
        }

        private void BuildBackdrop()
        {
            Image inputBlocker = NewImage("LevelMapInputBlocker", transform, null, new Color(0f, 0f, 0f, 0.001f));
            Stretch(inputBlocker.rectTransform);
            inputBlocker.raycastTarget = true;

            Image gradient = NewImage("NightGradient", transform, CreateGradientSprite(
                new Color32(3, 8, 24, 255), new Color32(8, 20, 49, 255), new Color32(4, 15, 38, 255)), White);
            Stretch(gradient.rectTransform);
            gradient.raycastTarget = false;

            // User-approved chapter-map art is used as a clean background plate only. All labels,
            // stage state and hit targets remain live Unity UI below, so save data never disagrees
            // with text baked into a screenshot. EnvelopeParent preserves the original composition
            // without stretching on either the Windows portrait window or WebGL.
            Sprite chapterCity = Resources.Load<Sprite>("Art/LevelMapUser/chapter-01-city-clean-ai-v1");
            if (chapterCity != null)
            {
                Image city = NewImage("Chapter01UserCityBackground", transform, chapterCity,
                    new Color32(219, 229, 255, 255));
                Stretch(city.rectTransform);
                city.preserveAspect = true;
                city.raycastTarget = false;
                AspectRatioFitter cover = city.gameObject.AddComponent<AspectRatioFitter>();
                cover.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                cover.aspectRatio = chapterCity.rect.width / Mathf.Max(1f, chapterCity.rect.height);
            }

            Sprite glowSprite = CreateRadialSprite(128, false);
            AddGlow("SkyGlow", new Vector2(360, 205), new Vector2(650, 390),
                new Color32(64, 104, 210, 62), glowSprite);
            AddGlow("CityGlow", new Vector2(360, 735), new Vector2(760, 720),
                new Color32(255, 72, 199, 38), glowSprite);
            AddGlow("CyanGlow", new Vector2(590, 535), new Vector2(360, 560),
                new Color32(72, 188, 255, 28), glowSprite);

            if (chapterCity == null)
            {
                Image distantCity = NewImage("DistantSkyline", transform, CreateSkylineSprite(29, true),
                    new Color(0.7f, 0.72f, 1f, 0.48f));
                PlaceTop(distantCity.rectTransform, 0, 130, 720, 670);

                Image nearCity = NewImage("NearSkyline", transform, CreateSkylineSprite(73, false),
                    new Color(1f, 1f, 1f, 0.86f));
                PlaceTop(nearCity.rectTransform, 0, 410, 720, 710);
            }

            Image horizon = NewImage("HorizonGlow", transform, null, new Color32(124, 164, 255, 42));
            PlaceTop(horizon.rectTransform, 0, 1055, 720, 3);
            Image reflection = NewImage("Reflection", transform, CreateGradientSprite(
                new Color32(79, 31, 155, 80), new Color32(17, 15, 61, 26), new Color32(4, 7, 29, 0)), White);
            PlaceTop(reflection.rectTransform, 0, 1058, 720, 166);

            // Preserve the city as the hero visual while keeping text and the star route legible.
            Image readabilityVeil = NewImage("LevelMapReadabilityVeil", transform, null,
                new Color32(2, 8, 27, 82));
            Stretch(readabilityVeil.rectTransform);
            readabilityVeil.raycastTarget = false;
        }

        private void BuildHeader()
        {
            Image header = NewImage("HeaderGlass", transform, null, new Color32(2, 8, 25, 236));
            PlaceTop(header.rectTransform, 0, 0, 720, 116);

            GameObject back = NewButton("Back", header.transform, "〈", 38,
                new Color32(11, 22, 54, 226), White, Close, 17);
            PlaceTop(back.GetComponent<RectTransform>(), 22, 21, 76, 66);
            AddGlassOutline(back, new Color32(134, 169, 246, 112), 1f);

            NewPlacedText(header.transform, "第01章", 16, new Color32(220, 202, 255, 255),
                122, 14, 142, 26, TextAnchor.MiddleLeft, FontStyle.Bold);
            NewPlacedText(header.transform, "踏梦迷踪", 31, White,
                122, 39, 254, 48, TextAnchor.MiddleLeft, FontStyle.Bold);

            GameObject stamina = NewPanel("Stamina", header.transform, new Color32(12, 21, 54, 220), 23);
            PlaceTop(stamina.GetComponent<RectTransform>(), 508, 24, 190, 60);
            AddGlassOutline(stamina, new Color32(135, 147, 232, 70), 1f);
            NewPlacedText(stamina.transform, "体力", 15, new Color32(255, 166, 225, 255),
                16, 10, 52, 40, TextAnchor.MiddleLeft, FontStyle.Bold);
            staminaText = NewPlacedText(stamina.transform, string.Empty, 20, White,
                63, 10, 110, 40, TextAnchor.MiddleRight, FontStyle.Bold);

            Image divider = NewImage("Divider", header.transform, null, new Color32(133, 163, 240, 78));
            PlaceTop(divider.rectTransform, 24, 112, 672, 1.5f);
        }

        private void BuildMap()
        {
            GameObject mapLayer = NewObject("CityRoute", transform);
            Stretch(mapLayer.AddComponent<RectTransform>());

            GameObject chapterTitle = NewPanelButton("StoryChapter-01", mapLayer.transform,
                new Color32(4, 12, 35, 148), 18, OpenStoryChapter01);
            PlaceTop(chapterTitle.GetComponent<RectTransform>(), 24, 136, 292, 108);
            AddGlassOutline(chapterTitle, new Color32(133, 166, 239, 70), 1f);
            storyChapterLabel = NewPlacedText(chapterTitle.transform, "主线剧情 · 第一章", 13,
                new Color32(255, 145, 220, 255), 18, 12, 246, 24, TextAnchor.MiddleLeft, FontStyle.Bold);
            NewPlacedText(chapterTitle.transform, "欲望都市", 29, White,
                18, 38, 194, 46, TextAnchor.MiddleLeft, FontStyle.Bold);
            Image titleLine = NewImage("ChapterTitleLine", chapterTitle.transform, null,
                new Color32(192, 211, 255, 155));
            PlaceTop(titleLine.rectTransform, 18, 91, 236, 1.5f);
            Image titleStar = NewImage("ChapterTitleStar", chapterTitle.transform, RoundedSprite(4),
                new Color32(244, 246, 255, 235));
            PlaceTop(titleStar.rectTransform, 10, 88, 7, 7);
            CenterPivot(titleStar.rectTransform);
            titleStar.rectTransform.localEulerAngles = new Vector3(0, 0, 45);

            Vector2[] points =
            {
                new Vector2(414, 1044),
                new Vector2(268, 956),
                new Vector2(420, 868),
                new Vector2(554, 780),
                new Vector2(430, 690),
                new Vector2(260, 600),
                new Vector2(422, 510),
                new Vector2(552, 420),
                new Vector2(382, 330),
                new Vector2(544, 246),
            };

            GameObject routeLines = NewObject("RouteLines", mapLayer.transform);
            Stretch(routeLines.AddComponent<RectTransform>());
            int currentStage = CurrentStoryStage();
            for (int index = 0; index < points.Length - 1; index++)
                AddRouteSegment(routeLines.transform, points[index], points[index + 1], index + 2 > currentStage);

            for (int index = 0; index < points.Length; index++)
                BuildNode(mapLayer.transform, index + 1, points[index]);
        }

        private void BuildNode(Transform parent, int stage, Vector2 center)
        {
            LevelState state = StateFor(stage);
            float width = state == LevelState.Current ? 146f : 118f;
            float height = state == LevelState.Current ? 96f : 78f;

            Image glow = NewImage($"Glow-1-{stage}", parent, CreateRadialSprite(96, false),
                state == LevelState.Locked
                    ? new Color32(80, 105, 160, 22)
                    : state == LevelState.Current
                        ? new Color32(255, 96, 222, 102)
                        : new Color32(119, 154, 255, 48));
            PlaceTop(glow.rectTransform, center.x - width * 0.72f, center.y - height * 0.95f,
                width * 1.44f, height * 1.9f);
            CenterPivot(glow.rectTransform);

            Color frameTint = state == LevelState.Locked
                ? new Color32(104, 111, 151, 160)
                : state == LevelState.Current
                    ? White
                    : new Color32(205, 216, 255, 226);
            GameObject node = NewSpriteButton($"Level-1-{stage}", parent,
                stageNodeSprite, frameTint, () => SelectStage(stage));
            RectTransform rect = node.GetComponent<RectTransform>();
            PlaceTop(rect, center.x - width * 0.5f, center.y - height * 0.5f, width, height);
            Text nodeLabel = NewPlacedText(node.transform, $"1-{stage}",
                state == LevelState.Current ? 24 : 20,
                state == LevelState.Locked ? new Color32(151, 153, 190, 255) : White,
                8, height * 0.17f, width - 16, height * 0.36f, TextAnchor.MiddleCenter, FontStyle.Bold);
            Shadow labelShadow = nodeLabel.gameObject.AddComponent<Shadow>();
            labelShadow.effectColor = new Color32(12, 4, 38, 235);
            labelShadow.effectDistance = new Vector2(1.5f, -1.5f);

            Outline outline = node.AddComponent<Outline>();
            outline.effectDistance = new Vector2(1.25f, -1.25f);
            outline.effectColor = state == LevelState.Locked
                ? new Color32(111, 127, 177, 105)
                : state == LevelState.Current
                    ? new Color32(255, 154, 232, 245)
                    : new Color32(154, 182, 245, 186);

            string status = StateLabel(state);
            Text statusLabel = NewPlacedText(node.transform, status, state == LevelState.Cleared ? 12 : 11,
                state == LevelState.Locked ? new Color32(150, 154, 193, 255) : new Color32(255, 186, 229, 255),
                8, height * 0.52f, width - 16, height * 0.20f, TextAnchor.MiddleCenter, FontStyle.Bold);

            NodeView view = new NodeView
            {
                Stage = stage,
                State = state,
                Button = node.GetComponent<Button>(),
                Background = node.GetComponent<Image>(),
                Glow = glow,
                Outline = outline,
                StageLabel = nodeLabel,
                StatusLabel = statusLabel,
            };
            nodeViews[stage] = view;
            if (state == LevelState.Current) currentGlow = glow;
        }

        private void BuildDetails()
        {
            GameObject card = NewPanel("SelectedLevel", transform, Glass, 28);
            PlaceTop(card.GetComponent<RectTransform>(), 20, 1100, 680, 230);
            AddGlassOutline(card, new Color32(132, 164, 238, 126), 1.5f);

            Image topAccent = NewImage("SelectedLevelAccent", card.transform, null,
                new Color32(255, 91, 207, 225));
            PlaceTop(topAccent.rectTransform, 28, 0, 184, 3);

            stageText = NewPlacedText(card.transform, "1-1", 38, White,
                28, 14, 106, 56, TextAnchor.MiddleLeft, FontStyle.Bold);
            stageTitleText = NewPlacedText(card.transform, "霓虹序曲", 20, White,
                140, 16, 292, 31, TextAnchor.MiddleLeft, FontStyle.Bold);
            stageStatusText = NewPlacedText(card.transform, "当前关卡", 14,
                new Color32(255, 142, 217, 255), 140, 48, 292, 24, TextAnchor.MiddleLeft, FontStyle.Bold);
            progressText = NewPlacedText(card.transform, string.Empty, 13, Muted,
                28, 79, 414, 28, TextAnchor.MiddleLeft);

            staminaCostText = InfoChip(card.transform, "StaminaCost", "体力 -8",
                28, 145, 126, new Color32(255, 157, 220, 255));
            diamondRewardText = InfoChip(card.transform, "DiamondReward", "星钻 ×20",
                164, 145, 140, Cyan);
            goldRewardText = InfoChip(card.transform, "GoldReward", "星币 ×300",
                314, 145, 146, new Color32(255, 215, 111, 255));

            GameObject start = NewSpriteButton("StartChallenge", card.transform,
                actionFrameSprite, White, StartChallenge);
            PlaceTop(start.GetComponent<RectTransform>(), 474, 105, 184, 92);
            startButton = start.GetComponent<Button>();
            startBackground = start.GetComponent<Image>();
            startLabel = NewPlacedText(start.transform, "开始挑战", 23, White,
                12, 23, 160, 46, TextAnchor.MiddleCenter, FontStyle.Bold);
            startLabel.name = "Label";
        }

        private Text InfoChip(Transform parent, string name, string label, float x, float y, float width, Color accent)
        {
            GameObject chip = NewPanel(name, parent, new Color32(13, 24, 58, 228), 14);
            PlaceTop(chip.GetComponent<RectTransform>(), x, y, width, 54);
            AddGlassOutline(chip, new Color32(120, 151, 222, 68), 1f);
            Image accentLine = NewImage("Accent", chip.transform, null, accent);
            PlaceTop(accentLine.rectTransform, 0, 8, 3, 38);
            return NewPlacedText(chip.transform, label, 13, White,
                8, 6, width - 14, 42, TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        private void BuildChapterDock()
        {
            GameObject dock = NewPanel("ChapterControlDock", transform, new Color32(4, 12, 36, 236), 24);
            PlaceTop(dock.GetComponent<RectTransform>(), 20, 1348, 680, 168);
            AddGlassOutline(dock, new Color32(122, 157, 231, 98), 1.25f);

            GameObject rewards = NewPanelButton("ChapterRewards", dock.transform,
                new Color32(10, 23, 55, 228), 20, OpenChapterRewards);
            PlaceTop(rewards.GetComponent<RectTransform>(), 14, 18, 319, 122);
            AddGlassOutline(rewards, new Color32(119, 158, 235, 80), 1f);
            Image rewardIcon = NewImage("ChapterRewardsIcon", rewards.transform, rewardChestSprite, Color.white);
            PlaceTop(rewardIcon.rectTransform, 18, 20, 76, 76);
            rewardIcon.preserveAspect = true;
            NewPlacedText(rewards.transform, "章节奖励", 18, White,
                106, 23, 176, 34, TextAnchor.MiddleLeft, FontStyle.Bold);
            rewardSummaryText = NewPlacedText(rewards.transform, "章节奖励", 11, Muted,
                106, 62, 176, 28, TextAnchor.MiddleLeft, FontStyle.Normal);
            rewardBadgeText = NewNotificationBadge(rewards.transform, "RewardBadge", 278, 8);

            GameObject tasks = NewPanelButton("ChapterTasks", dock.transform,
                new Color32(10, 23, 55, 228), 20, OpenChapterTasks);
            PlaceTop(tasks.GetComponent<RectTransform>(), 347, 18, 319, 122);
            AddGlassOutline(tasks, new Color32(119, 158, 235, 80), 1f);
            Image taskIcon = NewImage("ChapterTasksIcon", tasks.transform, actionFrameSprite,
                new Color32(255, 232, 252, 255));
            PlaceTop(taskIcon.rectTransform, 17, 25, 78, 70);
            taskIcon.preserveAspect = true;
            NewPlacedText(tasks.transform, "章节任务", 18, White,
                106, 23, 176, 34, TextAnchor.MiddleLeft, FontStyle.Bold);
            taskSummaryText = NewPlacedText(tasks.transform, "0/3 已完成", 11, Muted,
                106, 62, 176, 28, TextAnchor.MiddleLeft, FontStyle.Normal);
            taskBadgeText = NewNotificationBadge(tasks.transform, "TaskBadge", 278, 8);
        }

        private void OpenChapterRewards()
        {
            GameObject card = OpenModalShell("ChapterRewardsModal", 232, 1048);
            Image chest = NewImage("ChapterRewardsAIChest", card.transform, rewardChestSprite, Color.white);
            PlaceTop(chest.rectTransform, 242, 16, 176, 138);
            chest.preserveAspect = true;
            NewPlacedText(card.transform, "章节奖励", 27, White,
                205, 139, 250, 42, TextAnchor.MiddleCenter, FontStyle.Bold);
            NewPlacedText(card.transform, $"已获得 {model.ChapterOneTotalStars}/30 星  ·  达成里程碑即可领取",
                12, Muted, 105, 178, 450, 28, TextAnchor.MiddleCenter, FontStyle.Normal);

            List<ChapterStarRewardView> rewards = model.ChapterOneStarRewardViews();
            for (int index = 0; index < rewards.Count; index++)
            {
                ChapterStarRewardView view = rewards[index];
                GameObject row = NewPanel($"StarRewardRow-{view.RequiredStars}", card.transform,
                    new Color32(23, 22, 72, 225), 22);
                PlaceTop(row.GetComponent<RectTransform>(), 35, 226 + index * 204, 590, 174);
                AddGlassOutline(row, view.Claimable
                    ? new Color32(255, 104, 211, 205)
                    : new Color32(132, 108, 211, 92), view.Claimable ? 2f : 1f);

                NewPlacedText(row.transform, $"{view.RequiredStars} 星", 25,
                    view.Completed ? new Color32(255, 218, 104, 255) : White,
                    22, 16, 118, 42, TextAnchor.MiddleLeft, FontStyle.Bold);
                NewPlacedText(row.transform, FormatRewards(view.Rewards), 14, White,
                    22, 62, 365, 34, TextAnchor.MiddleLeft, FontStyle.Bold);
                string progress = view.Claimed
                    ? "奖励已领取"
                    : view.Claimable
                        ? "里程碑已达成"
                        : $"当前 {view.CurrentStars}/{view.RequiredStars} 星";
                NewPlacedText(row.transform, progress, 12, view.Claimable ? Pink : Muted,
                    22, 104, 360, 30, TextAnchor.MiddleLeft, FontStyle.Bold);

                int requiredStars = view.RequiredStars;
                GameObject claim = NewSpriteButton($"ChapterStarReward-{requiredStars}", row.transform,
                    actionFrameSprite,
                    view.Claimed ? new Color32(100, 105, 143, 190) : Color.white,
                    () => ClaimStarReward(requiredStars));
                PlaceTop(claim.GetComponent<RectTransform>(), 406, 45, 160, 82);
                NewPlacedText(claim.transform, view.Claimed ? "已领取" : view.Claimable ? "领取" : "未达成", 16,
                    view.Claimable ? White : Muted,
                    14, 22, 132, 34, TextAnchor.MiddleCenter, FontStyle.Bold);
                claim.GetComponent<Button>().interactable = view.Claimable;
            }

            NewPlacedText(card.transform, "章节星数取每一关的最佳评价，同一关可重复挑战提升星级", 11,
                new Color32(170, 180, 222, 255), 60, 884, 540, 30, TextAnchor.MiddleCenter, FontStyle.Normal);
        }

        private void ClaimStarReward(int requiredStars)
        {
            bool claimed = model.TryClaimChapterOneStarReward(requiredStars, out string message);
            if (claimed)
            {
                CloseChapterModal();
                Refresh();
            }
            Notify(message);
        }

        private void OpenChapterTasks()
        {
            GameObject card = OpenModalShell("ChapterTasksModal", 246, 1016);
            Image headerArt = NewImage("ChapterTasksAIHeader", card.transform, actionFrameSprite, Color.white);
            PlaceTop(headerArt.rectTransform, 126, 24, 408, 208);
            headerArt.preserveAspect = true;
            NewPlacedText(card.transform, "章节任务", 27, White,
                186, 82, 288, 40, TextAnchor.MiddleCenter, FontStyle.Bold);
            NewPlacedText(card.transform, "沿着欲望都市的舞台航线完成挑战", 12, Muted,
                146, 124, 368, 26, TextAnchor.MiddleCenter, FontStyle.Normal);

            List<ChapterTaskView> tasks = model.ChapterOneTaskViews();
            for (int index = 0; index < tasks.Count; index++)
            {
                ChapterTaskView view = tasks[index];
                GameObject row = NewPanel($"ChapterTaskRow-{view.Id}", card.transform,
                    new Color32(23, 22, 72, 225), 22);
                PlaceTop(row.GetComponent<RectTransform>(), 35, 232 + index * 200, 590, 170);
                AddGlassOutline(row, view.Claimable
                    ? new Color32(91, 218, 255, 200)
                    : new Color32(132, 108, 211, 92), view.Claimable ? 2f : 1f);

                NewPlacedText(row.transform, view.Title, 18, White,
                    22, 17, 360, 34, TextAnchor.MiddleLeft, FontStyle.Bold);
                NewPlacedText(row.transform, FormatRewards(view.Rewards), 13,
                    new Color32(255, 201, 110, 255),
                    22, 56, 360, 30, TextAnchor.MiddleLeft, FontStyle.Bold);
                string progress = view.Claimed ? "已完成并领取" : $"任务进度 {Mathf.Min(view.Progress, view.Target)}/{view.Target}";
                NewPlacedText(row.transform, progress, 12, view.Claimable ? Cyan : Muted,
                    22, 96, 360, 30, TextAnchor.MiddleLeft, FontStyle.Bold);

                string taskId = view.Id;
                GameObject claim = NewSpriteButton($"ChapterTaskClaim-{index + 1}", row.transform,
                    actionFrameSprite,
                    view.Claimed ? new Color32(100, 105, 143, 190) : Color.white,
                    () => ClaimChapterTask(taskId));
                PlaceTop(claim.GetComponent<RectTransform>(), 406, 44, 160, 82);
                NewPlacedText(claim.transform, view.Claimed ? "已领取" : view.Claimable ? "领取" : "进行中", 16,
                    view.Claimable ? White : Muted,
                    14, 22, 132, 34, TextAnchor.MiddleCenter, FontStyle.Bold);
                claim.GetComponent<Button>().interactable = view.Claimable;
            }

            NewPlacedText(card.transform, "章节任务奖励每个存档仅可领取一次", 11,
                new Color32(170, 180, 222, 255), 60, 872, 540, 28, TextAnchor.MiddleCenter, FontStyle.Normal);
        }

        private void ClaimChapterTask(string taskId)
        {
            bool claimed = model.TryClaimChapterOneTask(taskId, out string message);
            if (claimed)
            {
                CloseChapterModal();
                Refresh();
            }
            Notify(message);
        }

        private GameObject OpenModalShell(string name, float top, float height)
        {
            CloseChapterModal();
            modalRoot = NewObject(name, transform);
            RectTransform modalRect = modalRoot.AddComponent<RectTransform>();
            Stretch(modalRect);
            modalRoot.transform.SetAsLastSibling();

            Image blocker = NewImage("ModalBlocker", modalRoot.transform, null, new Color32(1, 2, 18, 222));
            Stretch(blocker.rectTransform);
            blocker.raycastTarget = true;
            Button blockerButton = blocker.gameObject.AddComponent<Button>();
            blockerButton.targetGraphic = blocker;
            blockerButton.onClick.AddListener(CloseChapterModal);

            GameObject card = NewPanel("ModalCard", modalRoot.transform, new Color32(12, 13, 51, 249), 30);
            PlaceTop(card.GetComponent<RectTransform>(), 30, top, 660, height);
            card.GetComponent<Image>().raycastTarget = true;
            AddGlassOutline(card, new Color32(188, 112, 255, 220), 2f);

            GameObject close = NewButton("CloseChapterModal", card.transform, "×", 28,
                new Color32(61, 39, 106, 244), White, CloseChapterModal, 18);
            PlaceTop(close.GetComponent<RectTransform>(), 586, 18, 52, 52);
            return card;
        }

        private void CloseChapterModal()
        {
            if (modalRoot == null) return;
            GameObject closingModal = modalRoot;
            modalRoot = null;
            if (Application.isPlaying) Destroy(closingModal);
            else DestroyImmediate(closingModal);
        }

        private Text NewNotificationBadge(Transform parent, string name, float x, float y)
        {
            GameObject badge = NewPanel(name, parent, Pink, 14);
            PlaceTop(badge.GetComponent<RectTransform>(), x, y, 31, 31);
            Text value = NewPlacedText(badge.transform, "0", 13, White,
                2, 1, 27, 28, TextAnchor.MiddleCenter, FontStyle.Bold);
            value.name = "Value";
            return value;
        }

        private static string FormatRewards(IReadOnlyList<CurrencyAmount> rewards)
        {
            if (rewards == null || rewards.Count == 0) return "无奖励";
            var labels = new List<string>(rewards.Count);
            for (int index = 0; index < rewards.Count; index++)
            {
                CurrencyAmount reward = rewards[index];
                if (reward == null) continue;
                labels.Add($"{CurrencyLabel(reward.Currency)} ×{reward.Amount}");
            }

            return labels.Count == 0 ? "无奖励" : string.Join("  ·  ", labels);
        }

        private static string CurrencyLabel(string currency)
        {
            switch (currency)
            {
                case CurrencyIds.Diamond: return "星钻";
                case CurrencyIds.Gold: return "星币";
                case CurrencyIds.RecruitTicket: return "签约券";
                case CurrencyIds.CostumeTicket: return "服装券";
                case CurrencyIds.Stamina: return "体力";
                default: return "奖励";
            }
        }

        private void BuildToast()
        {
            toastObject = NewPanel("LevelToast", transform, new Color32(10, 11, 42, 247), 20);
            PlaceTop(toastObject.GetComponent<RectTransform>(), 70, 900, 580, 64);
            Outline outline = toastObject.AddComponent<Outline>();
            outline.effectColor = new Color32(255, 105, 211, 120);
            outline.effectDistance = new Vector2(1, -1);
            toastText = NewPlacedText(toastObject.transform, string.Empty, 15, White,
                20, 8, 540, 52, TextAnchor.MiddleCenter, FontStyle.Bold);
            toastObject.SetActive(false);
        }

        private void SelectStage(int stage)
        {
            LevelState state = StateFor(stage);
            if (state == LevelState.Locked)
            {
                Notify($"1-{stage} 尚未解锁，请先完成当前关卡");
                return;
            }

            selectedStage = stage;
            Refresh();
            Notify(state == LevelState.Cleared
                ? $"已选择 1-{stage}，可再次挑战；首通奖励不会重复发放"
                : $"已选择当前关卡 1-{stage}");
        }

        private void StartChallenge()
        {
            if (challengeOpen) return;
            if (StateFor(selectedStage) == LevelState.Locked)
            {
                Notify("该关卡尚未解锁");
                return;
            }

            if (selectedStage < 1 || selectedStage > ChapterStageCount)
            {
                Notify("该关卡暂未开放战术演出");
                return;
            }

            string stageId = $"stage-1-{selectedStage}";
            ulong seed = unchecked((ulong)DateTime.UtcNow.Ticks ^ (ulong)selectedStage);
            BattleSimulator battle = model.StartStageBattle(stageId, seed, out string message);
            if (battle == null)
            {
                Notify(string.IsNullOrEmpty(message) ? "无法开始挑战" : message);
                return;
            }

            challengeOpen = true;
            Refresh();
            lastSettleMessage = string.Empty;
            Notify(message);
            TacticsBattlePanel.Open(transform.parent, model, battle,
                finished: simulator =>
                {
                    model.SettleStageBattle(simulator, out lastSettleMessage);
                    if (!string.IsNullOrEmpty(lastSettleMessage)) Notify(lastSettleMessage);
                },
                rewards: BuildBattleRewardLines,
                back: () =>
                {
                    challengeOpen = false;
                    selectedStage = CurrentStoryStage();
                    Refresh();
                },
                message: Notify);
        }

        private IReadOnlyList<string> BuildBattleRewardLines(BattleSimulator battle)
        {
            var lines = new List<string>();
            if (battle == null)
            {
                lines.Add("结算异常");
                return lines;
            }

            if (battle.Outcome != BattleOutcome.Victory)
            {
                lines.Add(string.IsNullOrEmpty(lastSettleMessage) ? "演出失败，调整编队后再来" : lastSettleMessage);
                return lines;
            }

            lines.Add($"{new string('★', Math.Max(0, battle.StarRating()))} 胜利");
            if (!string.IsNullOrEmpty(lastSettleMessage)) lines.Add(lastSettleMessage);
            else
            {
                StageDefinition stage = battle.Stage;
                if (stage != null)
                {
                    if (stage.GoldReward > 0) lines.Add($"金币 +{stage.GoldReward}");
                    if (stage.DiamondFirstClear > 0) lines.Add($"星钻 +{stage.DiamondFirstClear}");
                }
            }

            return lines;
        }

        private void OpenStoryChapter01()
        {
            if (!model.TryStartStory("chapter-01", out StoryRunner runner, out string message))
            {
                Notify(string.IsNullOrEmpty(message) ? "剧情无法开始" : message);
                return;
            }

            StoryPanel.Open(transform.parent, model, runner,
                finished: () =>
                {
                    model.CompleteStory("chapter-01");
                    Notify("已读完第 01 章");
                    Refresh();
                },
                audio: null,
                characterName: ResolveStoryCharacterName,
                back: Refresh,
                message: Notify);
        }

        private static string ResolveStoryCharacterName(string characterId)
        {
            if (string.IsNullOrEmpty(characterId)) return "未知";
            if (string.Equals(characterId, "producer", StringComparison.Ordinal)) return "制作人";
            int index = GameModel.IndexOfMember(characterId);
            return index >= 0 ? GameModel.Members[index].Name : "未知";
        }

        private void HandleModelChanged()
        {
            Refresh();
        }

        private void Refresh()
        {
            if (!built || model == null) return;

            staminaText.text = $"{model.Save.Stamina}/{model.StaminaCap}";
            if (storyChapterLabel != null)
                storyChapterLabel.text = model.IsStoryCompleted("chapter-01") ? "主线剧情 · 已读" : "主线剧情 · 第一章";
            stageText.text = $"1-{selectedStage}";

            LevelState state = StateFor(selectedStage);
            string selectedStageId = $"stage-1-{selectedStage}";
            StageDefinition selectedDefinition = model.Tactics.FindStage(selectedStageId);
            int staminaCost = selectedDefinition != null ? selectedDefinition.StaminaCost : GameModel.StoryStaminaCost;
            int diamondReward = selectedDefinition != null ? selectedDefinition.DiamondFirstClear : GameModel.StoryDiamondReward;
            int goldReward = selectedDefinition != null
                ? model.PreviewStageGoldReward(selectedStageId)
                : GameModel.StoryGoldReward;
            bool chapterComplete = model.IsChapterOneComplete;
            stageTitleText.text = selectedDefinition != null && !string.IsNullOrWhiteSpace(selectedDefinition.Name)
                ? selectedDefinition.Name
                : "欲望都市演出";
            stageStatusText.text = chapterComplete && selectedStage == ChapterStageCount
                ? "章节已完成"
                : state == LevelState.Cleared
                    ? $"已通关 · 最佳 {Mathf.Max(1, model.StarsOf(selectedStageId))} 星"
                    : "当前关卡";
            progressText.text = state == LevelState.Cleared
                ? $"可重复挑战提升评价  ·  推荐战力 {RecommendedPower(selectedStage):N0}"
                : $"严格顺序解锁  ·  推荐战力 {RecommendedPower(selectedStage):N0}";
            staminaCostText.text = $"体力 -{staminaCost}";
            diamondRewardText.text = state == LevelState.Cleared ? "首通已领" : $"星钻 ×{diamondReward}";
            goldRewardText.text = $"星币 ×{goldReward}";
            startLabel.text = state == LevelState.Cleared ? "再次挑战" : "开始挑战";
            startButton.interactable = !challengeOpen;
            startBackground.color = model.Save.Stamina < staminaCost
                ? new Color32(125, 135, 160, 175)
                : state == LevelState.Cleared
                    ? new Color32(210, 224, 255, 235)
                    : White;

            int claimableRewards = model.ChapterOneClaimableStarRewardCount;
            rewardSummaryText.text = claimableRewards > 0 ? $"{claimableRewards} 项奖励可领取" : "查看星级奖励";
            rewardBadgeText.text = claimableRewards.ToString();
            rewardBadgeText.transform.parent.gameObject.SetActive(claimableRewards > 0);

            List<ChapterTaskView> chapterTasks = model.ChapterOneTaskViews();
            int completedTasks = 0;
            for (int taskIndex = 0; taskIndex < chapterTasks.Count; taskIndex++)
                if (chapterTasks[taskIndex].Completed) completedTasks++;
            taskSummaryText.text = $"{completedTasks}/{chapterTasks.Count} 已完成";
            int claimableTasks = model.ChapterOneClaimableTaskCount;
            taskBadgeText.text = claimableTasks.ToString();
            taskBadgeText.transform.parent.gameObject.SetActive(claimableTasks > 0);

            foreach (KeyValuePair<int, NodeView> pair in nodeViews)
            {
                NodeView view = pair.Value;
                view.State = StateFor(view.Stage);
                bool selected = view.Stage == selectedStage;
                if (view.State == LevelState.Locked)
                {
                    view.Glow.enabled = false;
                    view.Background.color = new Color32(104, 111, 151, 160);
                    view.StageLabel.color = new Color32(151, 153, 190, 255);
                    view.StatusLabel.text = StateLabel(view.State);
                    view.StatusLabel.color = new Color32(150, 154, 193, 255);
                    view.Outline.effectColor = new Color32(111, 127, 177, 105);
                    continue;
                }

                view.Glow.enabled = selected || view.State == LevelState.Current;
                if (view.State == LevelState.Current) currentGlow = view.Glow;
                int stars = model.StarsOf($"stage-1-{view.Stage}");
                view.StatusLabel.text = view.State == LevelState.Cleared
                    ? new string('★', Mathf.Clamp(stars, 1, 3))
                    : StateLabel(view.State);
                view.StageLabel.color = White;
                view.StatusLabel.color = new Color32(255, 186, 229, 255);
                view.Outline.effectColor = selected
                    ? new Color32(255, 164, 235, 255)
                    : new Color32(154, 182, 245, 186);
                view.Background.color = selected || view.State == LevelState.Current
                    ? White
                    : new Color32(205, 216, 255, 226);
            }

            for (int index = 0; index < routeViews.Count; index++)
            {
                RouteView route = routeViews[index];
                bool future = StateFor(route.TargetStage) == LevelState.Locked;
                route.Glow.color = future
                    ? new Color32(94, 118, 175, 34)
                    : new Color32(255, 98, 218, 58);
                route.Core.color = future
                    ? new Color32(159, 177, 219, 145)
                    : new Color32(232, 220, 255, 235);
                route.Marker.color = future
                    ? new Color32(166, 181, 216, 180)
                    : new Color32(255, 214, 246, 255);
            }
        }

        private void Notify(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            onMessage?.Invoke(message);
            if (toastObject == null) return;
            toastText.text = message;
            toastObject.transform.SetAsLastSibling();
            toastObject.SetActive(true);
            toastHideAt = Time.unscaledTime + 3.2f;
        }

        private LevelState StateFor(int stage)
        {
            if (stage < 1 || stage > ChapterStageCount) return LevelState.Locked;
            string stageId = $"stage-1-{stage}";
            if (model.IsStageCleared(stageId) || IsStoryStageCleared(stage, model.Save.StoryProgress))
                return LevelState.Cleared;
            return model.IsStageUnlocked(stageId) ? LevelState.Current : LevelState.Locked;
        }

        private int CurrentStoryStage()
        {
            return model.CurrentChapterOneStage;
        }

        public static int StoryStageForProgress(int progress)
        {
            int normalizedProgress = Mathf.Clamp(progress, 0, GameModel.MaxStoryProgress);
            for (int index = 0; index < GameModel.StoryStageThresholds.Length; index++)
            {
                if (normalizedProgress < GameModel.StoryStageThresholds[index]) return index + 1;
            }

            return ChapterStageCount;
        }

        public static bool IsStoryStageCleared(int stage, int progress)
        {
            if (stage < 1 || stage > ChapterStageCount) return false;
            return Mathf.Clamp(progress, 0, GameModel.MaxStoryProgress) >=
                   GameModel.StoryStageThresholds[stage - 1];
        }

        private static string StateLabel(LevelState state)
        {
            return state == LevelState.Cleared ? "★★★" : state == LevelState.Current ? "当前" : "锁定";
        }

        private static int RecommendedPower(int stage)
        {
            return 38600 + Mathf.Max(0, stage - 1) * 2400;
        }

        private void AddRouteSegment(Transform parent, Vector2 from, Vector2 to, bool future)
        {
            Color glowColor = future ? new Color32(94, 118, 175, 34) : new Color32(255, 98, 218, 58);
            Color lineColor = future ? new Color32(159, 177, 219, 145) : new Color32(232, 220, 255, 235);
            Image glow = AddLine(parent, from, to, 11f, glowColor);
            Image core = AddLine(parent, from, to, 3f, lineColor);

            Vector2 middle = Vector2.Lerp(from, to, 0.5f);
            Image marker = NewImage("RouteMarker", parent, RoundedSprite(5), future
                ? new Color32(166, 181, 216, 180)
                : new Color32(255, 214, 246, 255));
            PlaceTop(marker.rectTransform, middle.x - 5, middle.y - 5, 10, 10);
            CenterPivot(marker.rectTransform);
            marker.rectTransform.localEulerAngles = new Vector3(0, 0, 45);

            routeViews.Add(new RouteView
            {
                TargetStage = routeViews.Count + 2,
                Glow = glow,
                Core = core,
                Marker = marker,
            });
        }

        private Image AddLine(Transform parent, Vector2 from, Vector2 to, float thickness, Color color)
        {
            Vector2 localFrom = new Vector2(from.x, -from.y);
            Vector2 localTo = new Vector2(to.x, -to.y);
            Vector2 delta = localTo - localFrom;
            Image line = NewImage("Route", parent, RoundedSprite(12), color);
            RectTransform rect = line.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = (localFrom + localTo) * 0.5f;
            rect.sizeDelta = new Vector2(delta.magnitude, thickness);
            rect.localEulerAngles = new Vector3(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            return line;
        }

        private void AddGlow(string name, Vector2 center, Vector2 size, Color color, Sprite sprite)
        {
            Image glow = NewImage(name, transform, sprite, color);
            PlaceTop(glow.rectTransform, center.x - size.x * 0.5f, center.y - size.y * 0.5f, size.x, size.y);
        }

        private Sprite CreateGradientSprite(Color32 top, Color32 middle, Color32 bottom)
        {
            const int width = 4;
            const int height = 256;
            Texture2D texture = NewTexture("LevelMap-Gradient", width, height);
            Color32[] pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                float fromBottom = y / (height - 1f);
                Color color = fromBottom < 0.5f
                    ? Color.Lerp(bottom, middle, fromBottom * 2f)
                    : Color.Lerp(middle, top, (fromBottom - 0.5f) * 2f);
                Color32 pixel = color;
                for (int x = 0; x < width; x++) pixels[y * width + x] = pixel;
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return NewSprite(texture, new Rect(0, 0, width, height), Vector2.one * 0.5f, Vector4.zero);
        }

        private Sprite CreateRadialSprite(int size, bool solidCenter)
        {
            Texture2D texture = NewTexture(solidCenter ? "LevelMap-Coin" : "LevelMap-Glow", size, size);
            Color32[] pixels = new Color32[size * size];
            Vector2 center = Vector2.one * (size - 1) * 0.5f;
            float radius = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float normalized = Vector2.Distance(new Vector2(x, y), center) / radius;
                    float alpha = solidCenter
                        ? Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.82f, 1f, normalized))
                        : Mathf.Pow(Mathf.Clamp01(1f - normalized), 2.2f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return NewSprite(texture, new Rect(0, 0, size, size), Vector2.one * 0.5f, Vector4.zero);
        }

        private Sprite CreateSkylineSprite(int seed, bool distant)
        {
            const int width = 360;
            const int height = 520;
            Texture2D texture = NewTexture(distant ? "LevelMap-City-Distant" : "LevelMap-City-Near", width, height);
            Color32[] pixels = new Color32[width * height];
            System.Random random = new System.Random(seed);
            int x = -8;
            while (x < width)
            {
                int buildingWidth = random.Next(distant ? 18 : 24, distant ? 38 : 52);
                int buildingHeight = random.Next(distant ? 110 : 135, distant ? 300 : 430);
                int gap = random.Next(4, 10);
                Color32 body = distant
                    ? new Color32((byte)random.Next(20, 35), (byte)random.Next(25, 43), (byte)random.Next(66, 100), 220)
                    : new Color32((byte)random.Next(15, 30), (byte)random.Next(16, 38), (byte)random.Next(55, 91), 245);
                FillRect(pixels, width, height, x, 0, buildingWidth, buildingHeight, body);
                FillRect(pixels, width, height, x + 1, buildingHeight - 2, buildingWidth - 2, 2,
                    new Color32(126, 76, 216, 165));

                int windowStepX = distant ? 7 : 8;
                int windowStepY = distant ? 11 : 13;
                for (int wx = x + 4; wx < x + buildingWidth - 3; wx += windowStepX)
                {
                    for (int wy = 9; wy < buildingHeight - 8; wy += windowStepY)
                    {
                        if (random.NextDouble() < (distant ? 0.38 : 0.52))
                        {
                            Color32 light = random.NextDouble() < 0.48
                                ? new Color32(81, 205, 255, (byte)random.Next(105, 205))
                                : new Color32(255, 83, 202, (byte)random.Next(105, 215));
                            FillRect(pixels, width, height, wx, wy, distant ? 2 : 3, distant ? 3 : 4, light);
                        }
                    }
                }

                x += buildingWidth + gap;
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return NewSprite(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0f), Vector4.zero);
        }

        private static void FillRect(Color32[] pixels, int textureWidth, int textureHeight,
            int x, int y, int width, int height, Color32 color)
        {
            int minX = Mathf.Clamp(x, 0, textureWidth);
            int maxX = Mathf.Clamp(x + width, 0, textureWidth);
            int minY = Mathf.Clamp(y, 0, textureHeight);
            int maxY = Mathf.Clamp(y + height, 0, textureHeight);
            for (int py = minY; py < maxY; py++)
            {
                int row = py * textureWidth;
                for (int px = minX; px < maxX; px++) pixels[row + px] = color;
            }
        }

        private Texture2D NewTexture(string name, int width, int height)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            generatedAssets.Add(texture);
            return texture;
        }

        private Sprite NewSprite(Texture2D texture, Rect rect, Vector2 pivot, Vector4 border)
        {
            Sprite sprite = Sprite.Create(texture, rect, pivot, 100f, 0, SpriteMeshType.FullRect, border);
            sprite.name = texture.name + "-Sprite";
            sprite.hideFlags = HideFlags.DontSave;
            generatedAssets.Add(sprite);
            return sprite;
        }

        private Sprite RoundedSprite(int radius)
        {
            radius = Mathf.Clamp(radius, 4, 30);
            if (roundedSprites.TryGetValue(radius, out Sprite cached)) return cached;

            const int size = 64;
            Texture2D texture = NewTexture($"LevelMap-Rounded-{radius}", size, size);
            Color32[] pixels = new Color32[size * size];
            float r = radius;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nearestX = Mathf.Clamp(x + 0.5f, r, size - r);
                    float nearestY = Mathf.Clamp(y + 0.5f, r, size - r);
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f),
                        new Vector2(nearestX, nearestY));
                    byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(r - distance + 0.5f) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = NewSprite(texture, new Rect(0, 0, size, size), Vector2.one * 0.5f,
                new Vector4(radius, radius, radius, radius));
            roundedSprites[radius] = sprite;
            return sprite;
        }

        private GameObject NewObject(string name, Transform parent)
        {
            GameObject result = new GameObject(name);
            result.transform.SetParent(parent, false);
            return result;
        }

        private Image NewImage(string name, Transform parent, Sprite sprite, Color color)
        {
            GameObject result = NewObject(name, parent);
            RectTransform rect = result.AddComponent<RectTransform>();
            rect.localScale = Vector3.one;
            Image image = result.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private GameObject NewPanel(string name, Transform parent, Color color, int radius)
        {
            Image image = NewImage(name, parent, RoundedSprite(radius), color);
            image.type = Image.Type.Sliced;
            image.raycastTarget = false;
            return image.gameObject;
        }

        private GameObject NewSpriteButton(string name, Transform parent, Sprite sprite, Color tint,
            UnityAction action)
        {
            Image image = NewImage(name, parent, sprite != null ? sprite : RoundedSprite(24), tint);
            image.preserveAspect = sprite != null;
            image.raycastTarget = true;
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ConfigureButtonColors(button);
            button.onClick.AddListener(action);
            AddButtonFeedback(image.gameObject);
            return image.gameObject;
        }

        private GameObject NewPanelButton(string name, Transform parent, Color background, int radius,
            UnityAction action)
        {
            GameObject result = NewPanel(name, parent, background, radius);
            Image image = result.GetComponent<Image>();
            image.raycastTarget = true;
            Button button = result.AddComponent<Button>();
            button.targetGraphic = image;
            ConfigureButtonColors(button);
            button.onClick.AddListener(action);
            AddButtonFeedback(result);
            return result;
        }

        private GameObject NewButton(string name, Transform parent, string label, int fontSize, Color background,
            Color foreground, UnityAction action, int radius)
        {
            GameObject result = NewPanel(name, parent, background, radius);
            Image image = result.GetComponent<Image>();
            image.raycastTarget = true;
            Button button = result.AddComponent<Button>();
            button.targetGraphic = image;
            ConfigureButtonColors(button);
            button.onClick.AddListener(action);
            AddButtonFeedback(result);

            Text text = NewText("Label", result.transform, label, fontSize, foreground, FontStyle.Bold,
                TextAnchor.MiddleCenter);
            Stretch(text.rectTransform, 6, 4, -6, -4);
            return result;
        }

        private static void ConfigureButtonColors(Button button)
        {
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.14f, 1.10f, 1.18f, 1f);
            colors.pressedColor = new Color(0.70f, 0.76f, 0.92f, 1f);
            colors.selectedColor = new Color(1.05f, 1.03f, 1.08f, 1f);
            colors.disabledColor = new Color(0.45f, 0.46f, 0.55f, 0.65f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
        }

        private static void AddButtonFeedback(GameObject target)
        {
            EventTrigger trigger = target.AddComponent<EventTrigger>();
            AddPointerEvent(trigger, EventTriggerType.PointerEnter,
                () => target.transform.localScale = Vector3.one * 1.045f);
            AddPointerEvent(trigger, EventTriggerType.PointerExit,
                () => target.transform.localScale = Vector3.one);
            AddPointerEvent(trigger, EventTriggerType.PointerDown,
                () => target.transform.localScale = Vector3.one * 0.955f);
            AddPointerEvent(trigger, EventTriggerType.PointerUp,
                () => target.transform.localScale = Vector3.one * 1.045f);
        }

        private static void AddPointerEvent(EventTrigger trigger, EventTriggerType eventType, UnityAction action)
        {
            var entry = new EventTrigger.Entry { eventID = eventType };
            entry.callback.AddListener(_ => action());
            trigger.triggers.Add(entry);
        }

        private static void AddGlassOutline(GameObject target, Color color, float distance)
        {
            Outline outline = target.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(distance, -distance);
        }

        private Text NewText(string name, Transform parent, string value, int size, Color color, FontStyle style,
            TextAnchor alignment)
        {
            GameObject result = NewObject(name, parent);
            RectTransform rect = result.AddComponent<RectTransform>();
            rect.localScale = Vector3.one;
            Text text = result.AddComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.fontStyle = style;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.supportRichText = false;
            text.raycastTarget = false;
            return text;
        }

        private Text NewPlacedText(Transform parent, string value, int size, Color color, float x, float y,
            float width, float height, TextAnchor alignment, FontStyle style = FontStyle.Normal)
        {
            Text text = NewText("Text", parent, value, size, color, style, alignment);
            PlaceTop(text.rectTransform, x, y, width, height);
            return text;
        }

        private static void PlaceTop(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void CenterPivot(RectTransform rect)
        {
            Vector2 size = rect.sizeDelta;
            rect.pivot = Vector2.one * 0.5f;
            rect.anchoredPosition += new Vector2(size.x * 0.5f, -size.y * 0.5f);
        }

        private static void Stretch(RectTransform rect, float left = 0, float bottom = 0,
            float right = 0, float top = 0)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(right, top);
        }
    }
}
