using System;
using System.Collections.Generic;
using ChoSiren.Panels;
using ChoSiren.Systems.Story;
using ChoSiren.Systems.Tactics;
using UnityEngine;
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
            public Image Background;
            public Image Glow;
            public Outline Outline;
            public Text StatusLabel;
        }

        private static readonly Color White = new Color32(249, 247, 255, 255);
        private static readonly Color Muted = new Color32(188, 183, 218, 255);
        private static readonly Color Pink = new Color32(255, 83, 196, 255);
        private static readonly Color Cyan = new Color32(86, 218, 255, 255);
        private static readonly Color Glass = new Color32(25, 20, 72, 242);
        private static readonly Color Locked = new Color32(45, 48, 86, 238);

        private readonly Dictionary<int, Sprite> roundedSprites = new Dictionary<int, Sprite>();
        private readonly Dictionary<int, NodeView> nodeViews = new Dictionary<int, NodeView>();
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
        private Text storyChapterLabel;
        private Button startButton;
        private Image startBackground;
        private Image currentGlow;
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

            BuildBackdrop();
            BuildHeader();
            BuildMap();
            BuildDetails();
            BuildToast();
        }

        private void BuildBackdrop()
        {
            Image gradient = NewImage("NightGradient", transform, CreateGradientSprite(
                new Color32(7, 8, 35, 255), new Color32(31, 14, 77, 255), new Color32(8, 24, 65, 255)), White);
            Stretch(gradient.rectTransform);
            gradient.raycastTarget = true;

            Sprite glowSprite = CreateRadialSprite(128, false);
            AddGlow("SkyGlow", new Vector2(360, 205), new Vector2(650, 390),
                new Color32(94, 49, 204, 95), glowSprite);
            AddGlow("CityGlow", new Vector2(360, 735), new Vector2(760, 720),
                new Color32(255, 48, 194, 54), glowSprite);
            AddGlow("CyanGlow", new Vector2(590, 535), new Vector2(360, 560),
                new Color32(50, 198, 255, 35), glowSprite);

            Image distantCity = NewImage("DistantSkyline", transform, CreateSkylineSprite(29, true),
                new Color(0.7f, 0.72f, 1f, 0.48f));
            PlaceTop(distantCity.rectTransform, 0, 130, 720, 670);
            distantCity.preserveAspect = false;

            Image nearCity = NewImage("NearSkyline", transform, CreateSkylineSprite(73, false),
                new Color(1f, 1f, 1f, 0.86f));
            PlaceTop(nearCity.rectTransform, 0, 410, 720, 710);
            nearCity.preserveAspect = false;

            Image horizon = NewImage("HorizonGlow", transform, null, new Color32(157, 73, 247, 54));
            PlaceTop(horizon.rectTransform, 0, 1055, 720, 3);
            Image reflection = NewImage("Reflection", transform, CreateGradientSprite(
                new Color32(79, 31, 155, 80), new Color32(17, 15, 61, 26), new Color32(4, 7, 29, 0)), White);
            PlaceTop(reflection.rectTransform, 0, 1058, 720, 166);

            for (int index = 0; index < 6; index++)
            {
                Image beam = NewImage("StageBeam", transform, null,
                    index % 2 == 0 ? new Color32(255, 92, 214, 18) : new Color32(73, 207, 255, 15));
                RectTransform rect = beam.rectTransform;
                PlaceTop(rect, 88 + index * 116, 120, 3, 880);
                CenterPivot(rect);
                rect.localEulerAngles = new Vector3(0, 0, index % 2 == 0 ? -7f : 6f);
            }
        }

        private void BuildHeader()
        {
            Image header = NewImage("HeaderGlass", transform, null, new Color32(6, 8, 34, 232));
            PlaceTop(header.rectTransform, 0, 0, 720, 112);

            GameObject back = NewButton("Back", header.transform, "返回", 17,
                new Color32(64, 42, 112, 235), White, Close, 18);
            PlaceTop(back.GetComponent<RectTransform>(), 18, 25, 86, 56);

            NewPlacedText(header.transform, "第 01 章", 17, new Color32(220, 198, 255, 255),
                126, 19, 140, 26, TextAnchor.MiddleLeft, FontStyle.Bold);
            NewPlacedText(header.transform, "踏梦迷踪", 30, White,
                126, 43, 235, 45, TextAnchor.MiddleLeft, FontStyle.Bold);

            GameObject story = NewButton("StoryChapter-01", header.transform, "剧情 · 第 01 章", 13,
                new Color32(74, 42, 128, 235), White, OpenStoryChapter01, 16);
            PlaceTop(story.GetComponent<RectTransform>(), 360, 22, 122, 52);
            storyChapterLabel = story.transform.Find("Label").GetComponent<Text>();

            GameObject stamina = NewPanel("Stamina", header.transform, new Color32(34, 24, 83, 235), 24);
            PlaceTop(stamina.GetComponent<RectTransform>(), 492, 27, 210, 54);
            NewPlacedText(stamina.transform, "体力", 15, new Color32(255, 151, 219, 255),
                18, 8, 58, 38, TextAnchor.MiddleLeft, FontStyle.Bold);
            staminaText = NewPlacedText(stamina.transform, string.Empty, 19, White,
                72, 8, 120, 38, TextAnchor.MiddleRight, FontStyle.Bold);

            Image divider = NewImage("Divider", header.transform, null, new Color32(152, 105, 226, 88));
            PlaceTop(divider.rectTransform, 18, 108, 684, 2);
        }

        private void BuildMap()
        {
            GameObject mapLayer = NewObject("CityRoute", transform);
            Stretch(mapLayer.AddComponent<RectTransform>());

            NewPlacedText(mapLayer.transform, "城市巡演路线", 11,
                new Color32(158, 189, 255, 205), 24, 126, 260, 22, TextAnchor.MiddleLeft, FontStyle.Bold);
            NewPlacedText(mapLayer.transform, "霓虹演出城区", 20, White,
                24, 148, 220, 34, TextAnchor.MiddleLeft, FontStyle.Bold);

            Vector2[] points =
            {
                new Vector2(140, 270),
                new Vector2(500, 470),
                new Vector2(220, 700),
                new Vector2(535, 930),
            };

            GameObject routeLines = NewObject("RouteLines", mapLayer.transform);
            Stretch(routeLines.AddComponent<RectTransform>());
            for (int index = 0; index < points.Length - 1; index++)
                AddRouteSegment(routeLines.transform, points[index], points[index + 1], index >= 2);

            for (int index = 0; index < points.Length; index++)
                BuildNode(mapLayer.transform, index + 1, points[index]);
        }

        private void BuildNode(Transform parent, int stage, Vector2 center)
        {
            LevelState state = StateFor(stage);
            float width = state == LevelState.Current ? 132f : 116f;
            float height = state == LevelState.Current ? 92f : 82f;

            Image glow = NewImage($"Glow-1-{stage}", parent, CreateRadialSprite(96, false),
                state == LevelState.Locked
                    ? new Color32(84, 94, 155, 28)
                    : state == LevelState.Current
                        ? new Color32(255, 70, 210, 105)
                        : new Color32(132, 84, 255, 72));
            PlaceTop(glow.rectTransform, center.x - width * 0.78f, center.y - height * 0.88f,
                width * 1.56f, height * 1.76f);
            CenterPivot(glow.rectTransform);

            Color background = state == LevelState.Locked
                ? Locked
                : state == LevelState.Current
                    ? new Color32(107, 40, 168, 250)
                    : new Color32(62, 40, 126, 246);
            GameObject node = NewButton($"Level-1-{stage}", parent, $"1-{stage}",
                state == LevelState.Current ? 27 : 24, background,
                state == LevelState.Locked ? new Color32(151, 153, 190, 255) : White,
                () => SelectStage(stage), state == LevelState.Current ? 24 : 20);
            RectTransform rect = node.GetComponent<RectTransform>();
            PlaceTop(rect, center.x - width * 0.5f, center.y - height * 0.5f, width, height);

            Outline outline = node.AddComponent<Outline>();
            outline.effectDistance = new Vector2(2f, -2f);
            outline.effectColor = state == LevelState.Locked
                ? new Color32(126, 132, 185, 130)
                : state == LevelState.Current
                    ? new Color32(255, 126, 226, 245)
                    : new Color32(197, 156, 255, 210);

            string status = StateLabel(state);
            Text statusLabel = NewPlacedText(node.transform, status, state == LevelState.Cleared ? 14 : 12,
                state == LevelState.Locked ? new Color32(150, 154, 193, 255) : new Color32(255, 186, 229, 255),
                6, height - 27, width - 12, 21, TextAnchor.MiddleCenter, FontStyle.Bold);

            NodeView view = new NodeView
            {
                Stage = stage,
                State = state,
                Background = node.GetComponent<Image>(),
                Glow = glow,
                Outline = outline,
                StatusLabel = statusLabel,
            };
            nodeViews[stage] = view;
            if (state == LevelState.Current) currentGlow = glow;
        }

        private void BuildDetails()
        {
            GameObject card = NewPanel("SelectedLevel", transform, Glass, 28);
            PlaceTop(card.GetComponent<RectTransform>(), 20, 1138, 680, 390);
            Outline outline = card.AddComponent<Outline>();
            outline.effectColor = new Color32(166, 112, 255, 150);
            outline.effectDistance = new Vector2(2, -2);

            stageText = NewPlacedText(card.transform, "1-1", 39, White,
                30, 20, 130, 58, TextAnchor.MiddleLeft, FontStyle.Bold);
            stageStatusText = NewPlacedText(card.transform, "当前关卡", 16,
                new Color32(255, 137, 213, 255), 30, 76, 170, 28, TextAnchor.MiddleLeft, FontStyle.Bold);
            progressText = NewPlacedText(card.transform, string.Empty, 13, Muted,
                30, 108, 255, 28, TextAnchor.MiddleLeft);

            GameObject cost = NewPanel("StaminaCost", card.transform, new Color32(54, 35, 105, 235), 18);
            PlaceTop(cost.GetComponent<RectTransform>(), 30, 145, 206, 56);
            NewPlacedText(cost.transform, "体力", 15, Muted, 18, 8, 70, 40, TextAnchor.MiddleLeft, FontStyle.Bold);
            staminaCostText = NewPlacedText(cost.transform, "-8", 22, new Color32(255, 138, 210, 255),
                116, 7, 68, 41, TextAnchor.MiddleRight, FontStyle.Bold);

            Image separator = NewImage("Separator", card.transform, null, new Color32(156, 116, 221, 90));
            PlaceTop(separator.rectTransform, 272, 28, 2, 176);

            NewPlacedText(card.transform, "奖励", 17, White,
                306, 20, 110, 34, TextAnchor.MiddleLeft, FontStyle.Bold);
            diamondRewardText = RewardChip(card.transform, "星钻 ×20", 306, 68, Cyan, true);
            goldRewardText = RewardChip(card.transform, "星币 ×300", 487, 68,
                new Color32(255, 211, 104, 255), false);

            Image cardDivider = NewImage("CardDivider", card.transform, null, new Color32(170, 125, 239, 70));
            PlaceTop(cardDivider.rectTransform, 28, 224, 624, 2);

            GameObject start = NewButton("StartChallenge", card.transform, "开始挑战", 27,
                Pink, White, StartChallenge, 28);
            PlaceTop(start.GetComponent<RectTransform>(), 30, 251, 620, 102);
            startButton = start.GetComponent<Button>();
            startBackground = start.GetComponent<Image>();
            startLabel = start.transform.Find("Label").GetComponent<Text>();

            Image buttonGlow = NewImage("ButtonGlow", card.transform, CreateRadialSprite(128, false),
                new Color32(255, 78, 212, 38));
            PlaceTop(buttonGlow.rectTransform, 70, 317, 540, 80);
            buttonGlow.transform.SetAsFirstSibling();
        }

        private Text RewardChip(Transform parent, string label, float x, float y, Color accent, bool diamond)
        {
            GameObject chip = NewPanel("Reward", parent, new Color32(43, 32, 93, 220), 17);
            PlaceTop(chip.GetComponent<RectTransform>(), x, y, 163, 82);

            Image icon = NewImage("Icon", chip.transform, diamond ? RoundedSprite(8) : CreateRadialSprite(64, true), accent);
            PlaceTop(icon.rectTransform, 12, 18, 46, 46);
            if (diamond)
            {
                CenterPivot(icon.rectTransform);
                icon.rectTransform.localEulerAngles = new Vector3(0, 0, 45);
            }

            return NewPlacedText(chip.transform, label, 15, White,
                62, 16, 93, 50, TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        private void BuildToast()
        {
            toastObject = NewPanel("LevelToast", transform, new Color32(10, 11, 42, 247), 20);
            PlaceTop(toastObject.GetComponent<RectTransform>(), 70, 1052, 580, 68);
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

            if (selectedStage < 1 || selectedStage > 4)
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
                storyChapterLabel.text = model.IsStoryCompleted("chapter-01") ? "剧情 · 已读" : "剧情 · 第 01 章";
            stageText.text = $"1-{selectedStage}";

            LevelState state = StateFor(selectedStage);
            StageDefinition selectedDefinition = model.Tactics.FindStage($"stage-1-{selectedStage}");
            int staminaCost = selectedDefinition != null ? selectedDefinition.StaminaCost : GameModel.StoryStaminaCost;
            int diamondReward = selectedDefinition != null ? selectedDefinition.DiamondFirstClear : GameModel.StoryDiamondReward;
            int goldReward = selectedDefinition != null ? selectedDefinition.GoldReward : GameModel.StoryGoldReward;
            bool chapterComplete = model.Save.StoryProgress >= GameModel.MaxStoryProgress;
            stageStatusText.text = chapterComplete && selectedStage == 4
                ? "章节已完成"
                : state == LevelState.Cleared ? "已通关 · 三星评价" : "当前关卡";
            progressText.text = state == LevelState.Cleared
                ? "首通奖励已领取  ·  最佳评价 卓越"
                : $"章节探索 {model.Save.StoryProgress}%  ·  推荐战力 {RecommendedPower(selectedStage):N0}";
            staminaCostText.text = $"-{staminaCost}";
            diamondRewardText.text = state == LevelState.Cleared ? "已领取" : $"星钻 ×{diamondReward}";
            // Gold is a repeatable stage drop; only the diamond reward is first-clear-only.
            goldRewardText.text = $"星币 ×{goldReward}";
            startLabel.text = state == LevelState.Cleared ? "再次挑战" : "开始挑战";
            startButton.interactable = !challengeOpen;
            startBackground.color = model.Save.Stamina < staminaCost
                ? new Color32(139, 66, 133, 245)
                : state == LevelState.Cleared
                    ? new Color32(126, 72, 194, 250)
                    : Pink;

            foreach (KeyValuePair<int, NodeView> pair in nodeViews)
            {
                NodeView view = pair.Value;
                view.State = StateFor(view.Stage);
                bool selected = view.Stage == selectedStage;
                if (view.State == LevelState.Locked)
                {
                    view.Glow.enabled = false;
                    view.Background.color = Locked;
                    view.StatusLabel.text = StateLabel(view.State);
                    view.StatusLabel.color = new Color32(150, 154, 193, 255);
                    continue;
                }

                view.Glow.enabled = selected || view.State == LevelState.Current;
                if (view.State == LevelState.Current) currentGlow = view.Glow;
                view.StatusLabel.text = StateLabel(view.State);
                view.StatusLabel.color = new Color32(255, 186, 229, 255);
                view.Outline.effectColor = selected
                    ? new Color32(255, 182, 236, 255)
                    : new Color32(178, 132, 246, 180);
                view.Background.color = selected
                    ? new Color32(116, 43, 177, 252)
                    : view.State == LevelState.Current
                        ? new Color32(92, 39, 154, 248)
                        : new Color32(62, 40, 126, 246);
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
            if (stage < 1 || stage > 4) return LevelState.Locked;
            if (IsStoryStageCleared(stage, model.Save.StoryProgress)) return LevelState.Cleared;
            return stage == CurrentStoryStage() ? LevelState.Current : LevelState.Locked;
        }

        private int CurrentStoryStage()
        {
            return StoryStageForProgress(model.Save.StoryProgress);
        }

        public static int StoryStageForProgress(int progress)
        {
            int normalizedProgress = Mathf.Clamp(progress, 0, GameModel.MaxStoryProgress);
            for (int index = 0; index < GameModel.StoryStageThresholds.Length; index++)
            {
                if (normalizedProgress < GameModel.StoryStageThresholds[index]) return index + 1;
            }

            return 4;
        }

        public static bool IsStoryStageCleared(int stage, int progress)
        {
            if (stage < 1 || stage > 4) return false;
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
            Color glowColor = future ? new Color32(113, 101, 181, 48) : new Color32(255, 60, 205, 72);
            Color lineColor = future ? new Color32(122, 121, 176, 145) : new Color32(238, 104, 255, 230);
            AddLine(parent, from, to, 17f, glowColor);
            AddLine(parent, from, to, 5f, lineColor);

            Vector2 middle = Vector2.Lerp(from, to, 0.5f);
            Image marker = NewImage("RouteMarker", parent, RoundedSprite(6), future
                ? new Color32(141, 139, 185, 180)
                : new Color32(255, 190, 240, 255));
            PlaceTop(marker.rectTransform, middle.x - 7, middle.y - 7, 14, 14);
            CenterPivot(marker.rectTransform);
            marker.rectTransform.localEulerAngles = new Vector3(0, 0, 45);
        }

        private void AddLine(Transform parent, Vector2 from, Vector2 to, float thickness, Color color)
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

        private GameObject NewButton(string name, Transform parent, string label, int fontSize, Color background,
            Color foreground, UnityAction action, int radius)
        {
            GameObject result = NewPanel(name, parent, background, radius);
            Image image = result.GetComponent<Image>();
            image.raycastTarget = true;
            Button button = result.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.9f, 1f);
            colors.selectedColor = Color.white;
            colors.colorMultiplier = 1f;
            button.colors = colors;
            button.onClick.AddListener(action);

            Text text = NewText("Label", result.transform, label, fontSize, foreground, FontStyle.Bold,
                TextAnchor.MiddleCenter);
            Stretch(text.rectTransform, 6, 4, -6, -4);
            return result;
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
