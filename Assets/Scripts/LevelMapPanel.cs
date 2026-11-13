using System;
using System.Collections.Generic;
using System.Linq;
using ChoSiren.Systems.Economy;
using ChoSiren.Panels;
using ChoSiren.Systems.Story;
using ChoSiren.Systems.Tactics;
using ChoSiren.UI;
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
    public sealed partial class LevelMapPanel : MonoBehaviour
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
            public Image Tag;
            public Image LockIcon;
        }

        private sealed class RouteView
        {
            public int TargetStage;
            public List<Image> Glow;
            public List<Image> Core;
            public Image Marker;
        }

        private static readonly Color White = new Color32(249, 247, 255, 255);
        private static readonly Color Muted = new Color32(171, 181, 213, 255);
        // Unified design (2026-10-25): the map accent is the brand teal; stars stay gold, warnings amber.
        private static readonly Color Accent = Brand1025.Teal;
        private static readonly Color StarGold = new Color32(255, 214, 92, 255);
        private static readonly Color Warn = new Color32(255, 190, 92, 255);
        private static readonly Color Cyan = new Color32(104, 198, 255, 255);
        private static readonly Color Glass = new Color32(8, 18, 48, 232);
        private const int ChapterStageCount = GameModel.ChapterOneStageCount;

        // Control silhouettes traced on map-board-038.png (858x1834, top-left origin).
        // The magenta starburst, cyan CTA burst, and dock plaques interleave with
        // neighbouring art, so each slice is vertex-clipped to its own contour and the
        // shared seam decor (gap shard x335-348, long shard y1640-1670) stays static.
        private static readonly Vector2[] MapStorySilhouette =
        {
            new Vector2(8f, 242f), new Vector2(25f, 195f), new Vector2(58f, 168f),
            new Vector2(140f, 160f), new Vector2(230f, 142f), new Vector2(310f, 128f),
            new Vector2(398f, 124f), new Vector2(430f, 158f), new Vector2(432f, 205f),
            new Vector2(415f, 245f), new Vector2(436f, 278f), new Vector2(412f, 312f),
            new Vector2(428f, 342f), new Vector2(390f, 368f), new Vector2(360f, 395f),
            new Vector2(300f, 412f), new Vector2(215f, 432f), new Vector2(168f, 443f),
            new Vector2(118f, 447f), new Vector2(72f, 442f), new Vector2(33f, 422f),
            new Vector2(12f, 393f), new Vector2(5f, 340f), new Vector2(8f, 290f),
        };
        private static readonly Vector2[] MapStartSilhouette =
        {
            new Vector2(575f, 1355f), new Vector2(640f, 1300f), new Vector2(720f, 1282f),
            new Vector2(795f, 1300f), new Vector2(832f, 1342f), new Vector2(855f, 1425f),
            new Vector2(848f, 1500f), new Vector2(858f, 1565f), new Vector2(838f, 1622f),
            new Vector2(800f, 1650f), new Vector2(745f, 1682f), new Vector2(700f, 1665f),
            new Vector2(648f, 1665f), new Vector2(575f, 1645f), new Vector2(518f, 1652f),
            new Vector2(545f, 1595f), new Vector2(568f, 1575f), new Vector2(560f, 1500f),
            new Vector2(548f, 1440f), new Vector2(562f, 1405f),
        };
        private static readonly Vector2[] MapRewardsSilhouette =
        {
            new Vector2(15f, 1692f), new Vector2(180f, 1680f), new Vector2(312f, 1672f),
            new Vector2(330f, 1692f), new Vector2(318f, 1762f), new Vector2(296f, 1794f),
            new Vector2(20f, 1800f), new Vector2(10f, 1790f), new Vector2(8f, 1745f),
            new Vector2(10f, 1700f),
        };
        private static readonly Vector2[] MapTasksSilhouette =
        {
            new Vector2(352f, 1688f), new Vector2(500f, 1680f), new Vector2(660f, 1678f),
            new Vector2(688f, 1690f), new Vector2(695f, 1745f), new Vector2(688f, 1795f),
            new Vector2(365f, 1798f), new Vector2(352f, 1788f), new Vector2(348f, 1740f),
        };

        private readonly Dictionary<int, Sprite> roundedSprites = new Dictionary<int, Sprite>();
        private readonly Dictionary<int, NodeView> nodeViews = new Dictionary<int, NodeView>();
        private readonly List<RouteView> routeViews = new List<RouteView>();
        private readonly List<UnityEngine.Object> generatedAssets = new List<UnityEngine.Object>();

        private GameModel model;
        private Action onBack;
        private Action<string> onGrowth;
        // Opens the stamina refill when a challenge cannot start for lack of stamina.
        private Action onStamina;
        private Action<string> onMessage;
        private Font font;
        private bool built;
        private bool challengeOpen;
        private bool closing;
        private int selectedStage = 1;
        // Two ten-stage chapters share this map; the chapter last viewed is remembered.
        public const int ChapterCount = 3; // 2026-11-11 第三章
        private static int lastChapter;
        private int chapter = 1;
        private Text chapterSwitchLabel;
        private string StageId(int stage) => $"stage-{chapter}-{stage}";
        private bool ChapterUnlocked(int number) => number == 1 || model.IsStageUnlocked($"stage-{number}-1");

        private Text staminaText;
        private Text stageText;
        private Text stageStatusText;
        private Text progressText;
        private Text staminaCostText;
        private Text diamondRewardText;
        private Text goldRewardText;
        private Text equipmentDropText;
        private Transform dropPreviewIcons;
        private string lastDropPreviewStage;
        private Text startLabel;
        private Text stageTitleText;
        private Text activeTeamText1028;
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
        private bool authoredMap;
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
            Action<string> message = null, Action<string> growth = null, Action stamina = null)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            if (gameModel == null) throw new ArgumentNullException(nameof(gameModel));

            LevelMapPanel existing = host.GetComponentInChildren<LevelMapPanel>(true);
            if (existing != null && existing.isActiveAndEnabled)
            {
                existing.gameObject.SetActive(true);
                existing.transform.SetAsLastSibling();
                existing.Bind(gameModel, back, message);
                existing.onGrowth = growth;
                existing.onStamina = stamina;
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
            ChoSiren.UI.PageAmbient.SetColor(panelObject, new Color32(9, 6, 22, 255));

            LevelMapPanel panel = panelObject.AddComponent<LevelMapPanel>();
            panel.Bind(gameModel, back, message);
            panel.onGrowth = growth;
            panel.onStamina = stamina;
            return panel;
        }

        public int SelectedStage => selectedStage;
        private static Sprite selectedFlame;
        private static Sprite SelectedFlameSprite()
        {
            if (selectedFlame != null) return selectedFlame;
            Texture2D texture = Resources.Load<Texture2D>("UI/MapFx/stage-selected-flame");
            if (texture == null) return null;
            return selectedFlame = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 100f);
        }

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
            RewardGain1101.Bind(model, transform, null); // chapter rewards / sweeps use the shared 获得奖励 popup
            // Open the chapter the player last looked at; once chapter two has begun, open it.
            chapter = lastChapter > 0 && ChapterUnlocked(lastChapter) ? lastChapter
                : model.IsStageCleared("stage-2-20") ? 3
                : model.IsStageUnlocked("stage-2-1") && model.IsStageCleared("stage-1-20") ? 2 : 1;
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

            // Switch to the new layout only after the reviewed disc has been installed.
            if (Resources.Load<Sprite>("Art/LevelMap1102/vinyl") != null)
            {
                BuildVinylMap1102();
                BuildToast();
                return;
            }
            vinylMap1102 = false;
            Sprite board = chapter == 2
                ? ReferenceArt038.Load("Art/Reference038/map-board-038-ch2") ?? ReferenceArt038.Load("Art/Reference038/map-board-038")
                : ReferenceArt038.Load("Art/Reference038/map-board-038");
            if (board != null)
            {
                authoredMap = true;
                BuildAuthoredMap(board);
                BuildToast();
                return;
            }
            BuildBackdrop();
            BuildHeader();
            BuildMap();
            BuildDetails();
            BuildChapterDock();
            BuildToast();
        }

        private void BuildAuthoredMap(Sprite board)
        {
            // Input-only: the blurred page ambient shows through around the board.
            Image blocker = NewImage("LevelMapInputBlocker", transform, null, new Color32(7, 4, 20, 0));
            Stretch(blocker.rectTransform); blocker.raycastTarget = true;
            Canvas.ForceUpdateCanvases();
            RectTransform host = (RectTransform)transform;
            float w = host.rect.width > 1 ? host.rect.width : 720f;
            float h = host.rect.height > 1 ? host.rect.height : 1536f;
            float scale = Mathf.Min(w / board.rect.width, h / board.rect.height);
            RectTransform page = NewObject("AuthoredMapPage", transform).AddComponent<RectTransform>();
            PlaceTop(page, (w - board.rect.width * scale) * .5f, (h - board.rect.height * scale) * .5f,
                board.rect.width, board.rect.height);
            page.localScale = Vector3.one * scale;
            Image art = NewImage("MapReferenceBoard", page, board, Color.white);
            Stretch(art.rectTransform); art.preserveAspect = true; art.raycastTarget = false;
            // Keep the authored board at its native aspect. A dim ambient surround fills the
            // system insets; repeating its border pixels caused long streaks on tall iPhones.
            PageAmbient.Set(gameObject, board, brightness: .45f);
            GameObject back = MapBoardHit("Back", page, 12, 15, 96, 100, Close);
            BakedRegionMotion038.Attach(back, art, new Rect(3f, 3f, 102f, 112f));
            NewPlacedText(page, chapter == 2 ? "第02章" : "第01章", 23, Accent, 145, 25, 300, 35, TextAnchor.MiddleLeft, FontStyle.Bold);
            NewPlacedText(page, chapter == 2 ? "浪潮巡演" : "踏梦迷踪", 38, White, 135, 62, 355, 64, TextAnchor.MiddleLeft, FontStyle.Bold);
            // Separate row below stamina, clear of the chapter title and the 1-10 node.
            GameObject switcher = MapBoardHit("ChapterSwitch", page, 590, 112, 214, 60,
                () => SwitchChapter(chapter == 1 ? 2 : 1));
            chapterSwitchLabel = NewPlacedText(switcher.transform, string.Empty, 21, White, 0, 0, 214, 60,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            chapterSwitchLabel.name = "ChapterSwitchLabel";
            SirenStickerUi.SkinButton(switcher, false, true);
            Image staminaIcon = NewImage("StaminaIcon", page, Resources.Load<Sprite>("Art/UI/ResourceStamina-C"), White);
            PlaceTop(staminaIcon.rectTransform, 620, 30, 44, 44);
            staminaIcon.preserveAspect = true; staminaIcon.raycastTarget = false;
            staminaText = NewPlacedText(page, string.Empty, 24, new Color32(255, 186, 235, 255),
                672, 30, 124, 46, TextAnchor.MiddleCenter, FontStyle.Bold);
            storyChapterLabel = NewPlacedText(page, string.Empty, 20, Accent, 65, 207, 300, 38, TextAnchor.MiddleLeft, FontStyle.Bold);
            GameObject story = MapBoardHit(chapter == 2 ? "StoryChapter-02" : "StoryChapter-01", page, 24, 210, 370, 190,
                OpenStoryChapter01);
            // The 欲望都市 burst is bigger than the hit; bind the traced silhouette and let
            // the live 主线剧情 label ride along in place (it keeps its page parent).
            BakedRegionMotion038.Attach(story, art, new Rect(3f, 122f, 435f, 327f), 3f,
                MapStorySilhouette, storyChapterLabel.transform);
            Vector2[] centers = {
                new Vector2(256,1182), new Vector2(393,1073), new Vector2(360,974),
                new Vector2(486,855), new Vector2(482,756), new Vector2(451,650),
                new Vector2(565,543), new Vector2(497,453), new Vector2(560,337), new Vector2(671,217)
            };
            for (int i = 0; i < centers.Length; i++)
            {
                int stage = i + 1;
                Vector2 c = centers[i];
                GameObject node = MapBoardHit($"Level-{chapter}-{stage}", page, c.x - 82, c.y - 33, 164, 66, () => SelectStage(stage));
                // 2026-10-20 (owner): the selected stage burns — GPT flame frame around the plaque.
                Image flame = NewImage($"SelectedFlame-{chapter}-{stage}", node.transform, null, Color.clear);
                PlaceTop(flame.rectTransform, 0, 0, 164, 66); flame.raycastTarget = false;
                flame.gameObject.AddComponent<MapSelectedFlame1020>().Configure(new Vector2(150f, 58f), 22f);
                flame.transform.SetAsFirstSibling();
                flame.gameObject.SetActive(false);
                Image glow = NewImage($"Glow-{chapter}-{stage}", node.transform, null, new Color(.7f,.25f,1f,.15f));
                PlaceTop(glow.rectTransform, 12, 51, 138, 3); glow.raycastTarget = false;
                Image lockIcon = NewImage("LockIcon", node.transform,
                    ReferencePunkFx.StatGlyph(ReferencePunkFx.StatGlyphKind.Lock), White);
                PlaceTop(lockIcon.rectTransform, 10, 15, 27, 30); lockIcon.raycastTarget = false;
                Text label = NewPlacedText(node.transform, $"{chapter}-{stage}", 27, White, 36, 6, 106, 42,
                    TextAnchor.MiddleCenter, FontStyle.Bold); label.name = "StageLabel";
                Text status = NewPlacedText(node.transform, string.Empty, 12, Accent, 43, 44, 100, 18,
                    TextAnchor.MiddleCenter, FontStyle.Bold); status.name = "StatusLabel";
                nodeViews[stage] = new NodeView { Stage=stage, Button=node.GetComponent<Button>(),
                    Background=node.GetComponent<Image>(), Glow=glow, StageLabel=label,
                    StatusLabel=status, LockIcon=lockIcon };
                // Baked node plaque + its live labels/glow move as one body; children keep
                // their parents so node.Find("StageLabel") paths stay intact.
                BakedRegionMotion038.Attach(node, art,
                    new Rect(c.x - 82f, c.y - 33f, 164f, 66f));
            }
            GameObject selected = NewPanel("SelectedLevel", page, SirenStickerUi.Ink, 0);
            GameObject team = MapBoardHit("StageTeamPreset", page, 54, 1214, 778, 54, OpenStageTeamPresets1028);
            SirenStickerUi.SkinPanel(team, false);
            activeTeamText1028 = NewPlacedText(team.transform, string.Empty, 23, White, 18, 4, 742, 46,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            activeTeamText1028.name = "ActiveTeamName";
            activeTeamText1028.supportRichText = false;
            PlaceTop(selected.GetComponent<RectTransform>(), 54, 1284, 490, 325);
            SirenStickerUi.SkinPanel(selected, false);
            stageText = NewPlacedText(selected.transform, string.Empty, 40, White, 8, 0, 126, 55, TextAnchor.MiddleLeft, FontStyle.Bold);
            stageTitleText = NewPlacedText(selected.transform, string.Empty, 29, White, 138, 5, 327, 46, TextAnchor.MiddleLeft, FontStyle.Bold);
            SirenStickerUi.SkinTitle(stageTitleText);
            stageStatusText = NewPlacedText(selected.transform, string.Empty, 23, Accent, 14, 56, 460, 34, TextAnchor.MiddleLeft, FontStyle.Bold);
            Readable(stageStatusText);
            progressText = NewPlacedText(selected.transform, string.Empty, 20, White, 14, 56, 460, 34, TextAnchor.MiddleLeft, FontStyle.Bold);
            Readable(progressText);
            equipmentDropText = NewPlacedText(selected.transform, string.Empty, 20, Cyan, 14, 124, 460, 32, TextAnchor.MiddleLeft, FontStyle.Bold);
            Readable(equipmentDropText);
            equipmentDropText.name = "StageDropSummary";
            GameObject drops = MapBoardHit("StageDropPreview", selected.transform, 10, 100, 470, 170, OpenDropPreview);
            dropPreviewIcons = drops.transform;
            RewardItemVisuals.CreateIcon(selected.transform, CurrencyIds.Stamina, "CostStaminaIcon", 14, 280, 34);
            staminaCostText = NewPlacedText(selected.transform, string.Empty, 22, White, 52, 281, 100, 32, TextAnchor.MiddleLeft, FontStyle.Bold);
            Readable(staminaCostText);
            RewardItemVisuals.CreateIcon(selected.transform, CurrencyIds.Diamond, "RewardDiamondIcon", 166, 280, 34);
            diamondRewardText = NewPlacedText(selected.transform, string.Empty, 22, Cyan, 204, 281, 110, 32, TextAnchor.MiddleLeft, FontStyle.Bold);
            Readable(diamondRewardText);
            RewardItemVisuals.CreateIcon(selected.transform, CurrencyIds.Gold, "RewardGoldIcon", 318, 280, 34);
            goldRewardText = NewPlacedText(selected.transform, string.Empty, 22, new Color32(255, 214, 92, 255), 356, 281, 120, 32, TextAnchor.MiddleLeft, FontStyle.Bold);
            Readable(goldRewardText);
            GameObject start = MapBoardHit("StartChallenge", page, 576, 1324, 256, 284, StartChallenge);
            startButton = start.GetComponent<Button>(); startBackground = start.GetComponent<Image>();
            BuildSweepRow(page);
            // The authored 开始挑战 remains the primary caption; this live sublabel can say 再次挑战.
            startLabel = NewPlacedText(start.transform, string.Empty, 17, White, 32, 232, 190, 35, TextAnchor.MiddleCenter);
            startLabel.name = "Label";
            // The 开始挑战 burst silhouette extends well past the measured hit; the traced
            // polygon keeps the burst's own shards while the SelectedLevel frame stays put.
            BakedRegionMotion038.Attach(start, art, new Rect(508f, 1278f, 352f, 406f), 3f,
                MapStartSilhouette);
            GameObject rewards = MapBoardHit("ChapterRewards", page, 28, 1697, 275, 92, OpenChapterRewards);
            NewPlacedText(rewards.transform, "章节奖励", 24, White, 20, 2, 218, 49, TextAnchor.MiddleCenter, FontStyle.Bold);
            rewardSummaryText = NewPlacedText(rewards.transform, string.Empty, 15, Muted, 15, 51, 245, 29, TextAnchor.MiddleCenter);
            rewardBadgeText = NewNotificationBadge(rewards.transform, "RewardBadge", 234, 5);
            // Baked dock plaques: live labels + badge ride along in place; the traced
            // silhouettes leave the shared gap shard and seam decor static.
            BakedRegionMotion038.Attach(rewards, art, new Rect(6f, 1670f, 326f, 132f), 3f,
                MapRewardsSilhouette);
            GameObject tasks = MapBoardHit("ChapterTasks", page, 355, 1697, 260, 92, OpenChapterTasks);
            NewPlacedText(tasks.transform, "章节任务", 24, White, 10, 2, 224, 49, TextAnchor.MiddleCenter, FontStyle.Bold);
            taskSummaryText = NewPlacedText(tasks.transform, string.Empty, 15, Muted, 10, 51, 230, 29, TextAnchor.MiddleCenter);
            taskBadgeText = NewNotificationBadge(tasks.transform, "TaskBadge", 222, 5);
            BakedRegionMotion038.Attach(tasks, art, new Rect(346f, 1676f, 351f, 124f), 3f,
                MapTasksSilhouette);
            BuildStageInfo1101(selected.transform, team.transform, rewards.transform, tasks.transform); // 2026-11-01 visual-b
        }

        private GameObject MapBoardHit(string name, Transform parent, float x, float y, float w, float h, UnityEngine.Events.UnityAction action)
        {
            // Build this hit directly: legacy NewPanelButton adds root-scaling EventTriggers.
            GameObject hit = NewPanel(name, parent, Color.clear, 0);
            PlaceTop(hit.GetComponent<RectTransform>(), x, y, w, h);
            Image visual = hit.GetComponent<Image>();
            visual.raycastTarget = true;
            Button button = hit.AddComponent<Button>();
            button.targetGraphic = visual;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(action);
            hit.AddComponent<LobbyHotspotFeedback>().Configure(name == "StartChallenge"
                ? LobbyHotspotFeedback.VisualKind.CallToAction : LobbyHotspotFeedback.VisualKind.Entry);
            return hit;
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
            SirenStickerUi.SkinPanel(header.gameObject, false);

            GameObject back = NewButton("Back", header.transform, string.Empty, 20,
                new Color32(11, 22, 54, 120), White, Close, 12);
            PlaceTop(back.GetComponent<RectTransform>(), 24, 28, 48, 48);
            AddGlassOutline(back, new Color32(134, 169, 246, 64), 1f);
            for (int side = -1; side <= 1; side += 2)
            {
                Image stroke = NewImage("BackChevron-" + side, back.transform, RoundedSprite(2), White);
                PlaceTop(stroke.rectTransform, 17, 23 + side * 6, 18, 2.5f);
                stroke.rectTransform.pivot = new Vector2(.5f, .5f);
                stroke.rectTransform.anchoredPosition += new Vector2(9, -1.25f);
                stroke.rectTransform.localRotation = Quaternion.Euler(0, 0, -side * 45);
                stroke.raycastTarget = false;
            }

            NewPlacedText(header.transform, "第01章", 16, new Color32(220, 202, 255, 255),
                90, 16, 142, 26, TextAnchor.MiddleLeft, FontStyle.Bold);
            NewPlacedText(header.transform, "踏梦迷踪", 28, White,
                90, 42, 254, 44, TextAnchor.MiddleLeft, FontStyle.Bold);

            GameObject stamina = NewPanel("Stamina", header.transform, new Color32(12, 21, 54, 220), 23);
            PlaceTop(stamina.GetComponent<RectTransform>(), 530, 28, 166, 48);
            AddGlassOutline(stamina, new Color32(135, 147, 232, 70), 1f);
            Image staminaIcon = NewImage("StaminaIcon", stamina.transform, Resources.Load<Sprite>("Art/UI/ResourceStamina-C"), White);
            PlaceTop(staminaIcon.rectTransform, 12, 7, 34, 34);
            staminaIcon.preserveAspect = true; staminaIcon.raycastTarget = false;
            staminaText = NewPlacedText(stamina.transform, string.Empty, 20, White,
                56, 4, 96, 40, TextAnchor.MiddleRight, FontStyle.Bold);

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
            // The reference card leans with the route; the click target is the rect, so the
            // visual tilt keeps the same hit area.
            ReferencePunkFx.Tilt(chapterTitle.GetComponent<RectTransform>(), -2.5f);
            ReferencePunkFx.CornerCuts(chapterTitle.GetComponent<RectTransform>(),
                new Color32(255, 138, 226, 150), 22f, 3f, 4f);
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
                ? new Color32(190, 198, 231, 225)
                : state == LevelState.Current
                    ? White
                    : new Color32(205, 216, 255, 226);
            GameObject node = NewSpriteButton($"Level-1-{stage}", parent,
                stageNodeSprite, frameTint, () => SelectStage(stage));
            RectTransform rect = node.GetComponent<RectTransform>();
            PlaceTop(rect, center.x - width * 0.5f, center.y - height * 0.5f, width, height);
            // The reference plaques all lean the same way; rotating the button rect keeps the
            // pointer target attached to the visual.
            ReferencePunkFx.Tilt(rect, -6f);

            Image tag = NewImage("NodeTag", node.transform, RoundedSprite(3),
                state == LevelState.Locked
                    ? new Color32(120, 132, 178, 160)
                    : state == LevelState.Current
                        ? new Color32(255, 112, 220, 235)
                        : new Color32(104, 198, 255, 205));
            PlaceTop(tag.rectTransform, 5, height * 0.16f, 6, height * 0.68f);

            Image lockIcon = NewImage("LockIcon", node.transform,
                ReferencePunkFx.StatGlyph(ReferencePunkFx.StatGlyphKind.Lock),
                new Color32(226, 232, 255, 235));
            PlaceTop(lockIcon.rectTransform, width * 0.5f - 30f, height * 0.12f, 17, 19);
            lockIcon.enabled = state == LevelState.Locked;

            Text nodeLabel = NewPlacedText(node.transform, $"1-{stage}",
                state == LevelState.Current ? 24 : 22, White,
                8, height * 0.08f, width - 16, height * 0.42f, TextAnchor.MiddleCenter, FontStyle.Bold);
            nodeLabel.name = "StageLabel";
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
            Text statusLabel = NewPlacedText(node.transform, status, 12,
                state == LevelState.Locked ? new Color32(215, 219, 242, 255) : new Color32(255, 186, 229, 255),
                8, height * 0.48f, width - 16, height * 0.24f, TextAnchor.MiddleCenter, FontStyle.Bold);
            statusLabel.name = "StatusLabel";

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
                Tag = tag,
                LockIcon = lockIcon,
            };
            nodeViews[stage] = view;
            if (state == LevelState.Current) currentGlow = glow;
        }

        private void BuildDetails()
        {
            GameObject card = NewPanel("SelectedLevel", transform, Glass, 28);
            PlaceTop(card.GetComponent<RectTransform>(), 20, 1100, 680, 318);
            AddGlassOutline(card, new Color32(132, 164, 238, 126), 1.5f);
            SirenStickerUi.SkinPanel(card);
            ReferencePunkFx.CornerCuts(card.GetComponent<RectTransform>(),
                new Color32(255, 122, 222, 170), 30f, 3.5f, 5f);
            ReferencePunkFx.Slash(card.transform, "SelectedLevelSlash", 652, 16, 52, 3.5f, -40f,
                new Color32(126, 214, 255, 150));

            Image topAccent = NewImage("SelectedLevelAccent", card.transform, null,
                new Color32(255, 91, 207, 225));
            PlaceTop(topAccent.rectTransform, 28, 0, 184, 3);

            stageText = NewPlacedText(card.transform, "1-1", 38, White,
                28, 14, 106, 56, TextAnchor.MiddleLeft, FontStyle.Bold);
            stageTitleText = NewPlacedText(card.transform, "霓虹序曲", 20, White,
                140, 16, 292, 31, TextAnchor.MiddleLeft, FontStyle.Bold);
            stageStatusText = NewPlacedText(card.transform, "当前关卡", 14,
                new Color32(255, 142, 217, 255), 140, 48, 292, 24, TextAnchor.MiddleLeft, FontStyle.Bold);
            progressText = NewPlacedText(card.transform, string.Empty, 14, Muted,
                28, 72, 624, 24, TextAnchor.MiddleLeft);

            GameObject drops = NewPanelButton("StageDropPreview", card.transform, new Color32(11, 29, 55, 180), 8, OpenDropPreview);
            PlaceTop(drops.GetComponent<RectTransform>(), 24, 124, 632, 100);
            dropPreviewIcons = drops.transform;
            equipmentDropText = NewPlacedText(card.transform, string.Empty, 14, Cyan,
                28, 96, 624, 26, TextAnchor.MiddleLeft);
            equipmentDropText.gameObject.name = "StageDropSummary";

            staminaCostText = InfoChip(card.transform, "StaminaCost", "体力 -8",
                24, 244, 122, new Color32(255, 157, 220, 255));
            diamondRewardText = InfoChip(card.transform, "DiamondReward", "星钻 ×20",
                154, 244, 142, Cyan);
            goldRewardText = InfoChip(card.transform, "GoldReward", "星币 ×300",
                304, 244, 146, new Color32(255, 215, 111, 255));

            GameObject start = NewSpriteButton("StartChallenge", card.transform,
                actionFrameSprite, White, StartChallenge);
            PlaceTop(start.GetComponent<RectTransform>(), 466, 230, 190, 78);
            // Image.preserveAspect aligns its drawing using the RectTransform pivot.
            // Center the authored frame as well as the label, including hover/press scaling.
            CenterPivot(start.GetComponent<RectTransform>());
            // The reference CTA leans into the card; pivot is already centred so the measured
            // pivot stays (0.5, 0.5) and only the visual rotates.
            start.GetComponent<RectTransform>().localEulerAngles = new Vector3(0f, 0f, -4f);
            startButton = start.GetComponent<Button>();
            startBackground = start.GetComponent<Image>();
            startLabel = NewPlacedText(start.transform, "开始挑战", 20, White,
                43, 21, 104, 36, TextAnchor.MiddleCenter, FontStyle.Bold);
            startLabel.name = "Label";
            startLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            startLabel.resizeTextForBestFit = true;
            startLabel.resizeTextMinSize = 17;
            startLabel.resizeTextMaxSize = 20;
            SirenStickerUi.SkinButton(start, true);
        }

        /// <summary>2026-11-01 visual-b: icon grid with rate badges; rules behind 「?」 (LevelMapPanel.Visual1101.cs).</summary>
        private void OpenDropPreview() => OpenDropPreview1101();

        private void RefreshDropPreview(string stageId)
        {
            equipmentDropText.text = string.Empty;
            if (lastDropPreviewStage == stageId) return;
            lastDropPreviewStage = stageId;
            for (int i = dropPreviewIcons.childCount - 1; i >= 0; i--)
            {
                GameObject old = dropPreviewIcons.GetChild(i).gameObject;
                old.SetActive(false);
                if (Application.isPlaying) Destroy(old); else DestroyImmediate(old);
            }
            IReadOnlyList<StageLootCandidate> candidates = model.StageLootCandidates(stageId);
            int count = Mathf.Min(5, candidates.Count);
            float previewWidth = authoredMap ? 470f : 632f;
            float width = previewWidth / Mathf.Max(1, count);
            for (int i = 0; i < count; i++)
            {
                int candidateIndex = candidates.Count > 5 && i >= 3 ? candidates.Count - 5 + i : i;
                StageLootCandidate reward = candidates[candidateIndex];
                // 2026-11-01 visual-b: big icon + drop-rate corner badge (LevelMapPanel.Visual1101.cs).
                DropTile1101(i, i * width + width * .5f, reward, stageId);
            }
        }

        /// <summary>Hard dark outline so small map text stays readable over the neon art.</summary>
        private static void Readable(Text text)
        {
            Outline edge = text.gameObject.AddComponent<Outline>();
            edge.effectColor = new Color(0f, 0f, 0f, .9f);
            edge.effectDistance = new Vector2(1.5f, -1.5f);
        }

        private Text InfoChip(Transform parent, string name, string label, float x, float y, float width, Color accent)
        {
            GameObject chip = NewPanel(name, parent, new Color32(13, 24, 58, 228), 14);
            PlaceTop(chip.GetComponent<RectTransform>(), x, y, width, 54);
            AddGlassOutline(chip, new Color32(120, 151, 222, 68), 1f);
            SirenStickerUi.SkinPanel(chip, false);
            Image accentLine = NewImage("Accent", chip.transform, null, accent);
            PlaceTop(accentLine.rectTransform, 0, 8, 3, 38);
            return NewPlacedText(chip.transform, label, 13, White,
                8, 6, width - 14, 42, TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        private void BuildChapterDock()
        {
            GameObject dock = NewPanel("ChapterControlDock", transform, new Color32(4, 12, 36, 236), 24);
            PlaceTop(dock.GetComponent<RectTransform>(), 20, 1430, 680, 88);
            AddGlassOutline(dock, new Color32(122, 157, 231, 98), 1.25f);
            SirenStickerUi.SkinPanel(dock, false);
            ReferencePunkFx.CornerCuts(dock.GetComponent<RectTransform>(),
                new Color32(168, 128, 255, 150), 22f, 3f, 4f);

            GameObject rewards = NewPanelButton("ChapterRewards", dock.transform,
                new Color32(10, 23, 55, 228), 20, OpenChapterRewards);
            PlaceTop(rewards.GetComponent<RectTransform>(), 12, 10, 322, 68);
            AddGlassOutline(rewards, new Color32(119, 158, 235, 80), 1f);
            Image rewardIcon = NewImage("ChapterRewardsIcon", rewards.transform, rewardChestSprite, Color.white);
            PlaceTop(rewardIcon.rectTransform, 14, 8, 52, 52);
            rewardIcon.preserveAspect = true;
            NewPlacedText(rewards.transform, "章节奖励", 18, White,
                78, 4, 196, 32, TextAnchor.MiddleLeft, FontStyle.Bold);
            rewardSummaryText = NewPlacedText(rewards.transform, "章节奖励", 11, Muted,
                78, 36, 196, 24, TextAnchor.MiddleLeft, FontStyle.Normal);
            rewardBadgeText = NewNotificationBadge(rewards.transform, "RewardBadge", 278, 8);

            GameObject tasks = NewPanelButton("ChapterTasks", dock.transform,
                new Color32(10, 23, 55, 228), 20, OpenChapterTasks);
            PlaceTop(tasks.GetComponent<RectTransform>(), 346, 10, 322, 68);
            AddGlassOutline(tasks, new Color32(119, 158, 235, 80), 1f);
            NewPlacedText(tasks.transform, "章节任务", 18, White,
                22, 4, 230, 32, TextAnchor.MiddleLeft, FontStyle.Bold);
            taskSummaryText = NewPlacedText(tasks.transform, "0/3 已完成", 11, Muted,
                22, 36, 230, 24, TextAnchor.MiddleLeft, FontStyle.Normal);
            taskBadgeText = NewNotificationBadge(tasks.transform, "TaskBadge", 278, 8);
            SirenStickerUi.SkinButton(rewards, false, true);
            SirenStickerUi.SkinButton(tasks, false, true);
        }

        private int ChapterStars()
        {
            int stars = 0;
            for (int stage = 1; stage <= ChapterStageCount; stage++) stars += model.StarsOf(StageId(stage));
            return stars;
        }

        private int ChapterClears()
        {
            int clears = 0;
            for (int stage = 1; stage <= ChapterStageCount; stage++) if (model.IsStageCleared(StageId(stage))) clears++;
            return clears;
        }

        private void OpenChapterRewards()
        {
            // 2026-10-20 (owner: redraw): P5-style card — red slanted title band, a star track
            // with milestone notches, and slanted tag rows; claimable rows turn to white paper.
            GameObject card = OpenModalShell("ChapterRewardsModal", 390, 720);
            int stars = model.ChapterTotalStars(chapter);
            ModalHeader(card.transform, chapter == 3 ? "第三章" : chapter == 2 ? "第二章" : "本章", "章节奖励", $"★ {stars}/60");
            List<ChapterStarRewardView> rewards = model.ChapterStarRewardViews(chapter);
            int maxStars = 30;
            foreach (var r in rewards) maxStars = Mathf.Max(maxStars, r.RequiredStars);
            // Star track
            Image track = NewImage("StarTrack", card.transform, null, new Color32(40, 28, 62, 255));
            PlaceTop(track.rectTransform, 50, 168, 560, 16);
            Image fill = NewImage("StarTrackFill", track.transform, null, Brand1025.Teal);
            fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(stars / (float)maxStars), 1f);
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
            foreach (var r in rewards)
            {
                float x = 50 + 560 * r.RequiredStars / (float)maxStars;
                Image notch = NewImage("Milestone-" + r.RequiredStars, card.transform, null,
                    r.Completed ? new Color32(255, 214, 92, 255) : new Color32(120, 104, 150, 255));
                PlaceTop(notch.rectTransform, x - 13, 163, 26, 26);
                notch.rectTransform.localEulerAngles = new Vector3(0, 0, 45);
                NewPlacedText(card.transform, r.RequiredStars.ToString(), 13, r.Completed ? White : Muted, x - 30, 192, 60, 22, TextAnchor.MiddleCenter, FontStyle.Bold);
            }
            for (int index = 0; index < rewards.Count; index++)
            {
                ChapterStarRewardView view = rewards[index];
                int requiredStars = view.RequiredStars;
                GameObject row = P5Row(card.transform, $"StarRewardRow-{view.RequiredStars}", 236 + index * 136, view.Claimable, view.Claimed);
                NewPlacedText(row.transform, $"{view.RequiredStars}★", 30, view.Completed ? new Color32(255, 214, 92, 255) : (Color)White,
                    38, 30, 110, 60, TextAnchor.MiddleLeft, FontStyle.BoldAndItalic);
                NewPlacedText(row.transform, FormatRewards(view.Rewards), 20, White,
                    136, 18, 280, 44, TextAnchor.MiddleLeft, FontStyle.Bold);
                NewPlacedText(row.transform, view.Claimed ? "已领取" : view.Claimable ? "可以领取了" : $"还差 {Mathf.Max(0, view.RequiredStars - view.CurrentStars)} 星",
                    16, view.Claimable ? (Color)Brand1025.Teal : new Color32(176, 164, 210, 255), 136, 66, 280, 30, TextAnchor.MiddleLeft, FontStyle.Bold);
                P5Claim(row.transform, $"ChapterStarReward-{requiredStars}", view.Claimable, view.Claimed, "进行中", () => ClaimStarReward(requiredStars));
            }
            NewPlacedText(card.transform, "星数取每关最佳评价 · 同一关可重复挑战提升星级", 12,
                new Color32(170, 160, 210, 255), 60, 236 + rewards.Count * 136 + 8, 540, 28, TextAnchor.MiddleCenter, FontStyle.Normal);
        }

        /// <summary>
        /// The shared modal header (unified design 2026-10-25, same as ModalPolish038 modals): teal eyebrow, white title on the
        /// sticker title plate, a short teal beam, and an optional gold tally on the right.
        /// </summary>
        private void ModalHeader(Transform card, string eyebrow, string title, string tally)
        {
            Text brow = NewPlacedText(card, eyebrow, 15, Brand1025.Teal, 44, 24, 300, 22, TextAnchor.MiddleLeft, FontStyle.BoldAndItalic);
            brow.name = "Eyebrow";
            Text head = NewPlacedText(card, title, 30, White, 44, 48, 300, 46, TextAnchor.MiddleLeft, FontStyle.Bold);
            head.name = "Title";
            SirenStickerUi.SkinTitle(head, true);
            Image beam = NewImage("TitleBeam", card, PunkUi.SlantFill, Brand1025.Teal);
            PlaceTop(beam.rectTransform, 44, 104, 64, 6);
            if (!string.IsNullOrEmpty(tally))
                NewPlacedText(card, tally, 26, new Color32(255, 214, 92, 255), 330, 48, 240, 46, TextAnchor.MiddleRight, FontStyle.BoldAndItalic).name = "Tally";
        }

        /// <summary>The shared list row: quiet sticker frame with a left state bar (teal = something to do).</summary>
        private GameObject P5Row(Transform card, string name, float y, bool claimable, bool claimed)
        {
            Image row = NewImage(name, card, SirenStickerUi.QuietPanelSprite, claimed ? new Color32(255, 255, 255, 115) : (Color32)Color.white);
            row.type = Image.Type.Sliced;
            PlaceTop(row.rectTransform, 30, y, 600, 120);
            Image edge = NewImage("Edge", row.transform, null, claimable ? (Color)Brand1025.Teal : new Color(1f, 1f, 1f, .22f));
            PlaceTop(edge.rectTransform, 14, 15, 6, 90);
            return row.gameObject;
        }

        /// <summary>The shared sticker button: teal 领取 when claimable, a faded tab for 进行中 / 已领取.</summary>
        private void P5Claim(Transform row, string name, bool claimable, bool claimed, string waiting, UnityEngine.Events.UnityAction action)
        {
            GameObject claim = NewSpriteButton(name, row, null, Color.clear, action);
            PlaceTop(claim.GetComponent<RectTransform>(), 426, 26, 150, 68);
            Text label = NewPlacedText(claim.transform, claimed ? "已领取 ✓" : claimable ? "领取" : waiting, claimable ? 24 : 18,
                White, 0, 0, 150, 68, TextAnchor.MiddleCenter, FontStyle.Bold);
            label.name = "Label";
            claim.GetComponent<Button>().interactable = claimable;
            SirenStickerUi.SkinButton(claim, claimable, !claimable);
        }

        private void ClaimStarReward(int requiredStars)
        {
            bool claimed = model.TryClaimChapterStarReward(chapter, requiredStars, out string message);
            if (claimed)
            {
                ModalPolish038Chrome.PlaySuccess();
                CloseChapterModal();
                Refresh();
            }
            Notify(message);
        }

        private void OpenChapterTasks()
        {
            GameObject card = OpenModalShell("ChapterTasksModal", 420, 640);
            List<ChapterTaskView> tasks = model.ChapterTaskViews(chapter);
            int done = 0; foreach (var t in tasks) if (t.Claimed || t.Claimable) done++;
            ModalHeader(card.transform, chapter == 3 ? "第三章" : chapter == 2 ? "第二章" : "本章", "章节任务", $"{done}/{tasks.Count}");
            for (int index = 0; index < tasks.Count; index++)
            {
                ChapterTaskView view = tasks[index];
                string taskId = view.Id;
                GameObject row = P5Row(card.transform, $"ChapterTaskRow-{view.Id}", 176 + index * 136, view.Claimable, view.Claimed);
                Color ink = White;
                // visual-a 1101c: an icon says what (flag = clear a stage, star = collect stars), the rewards are badges.
                RectTransform kind = VisualA1101.NeonIcon(row.transform, "TaskIcon", view.Title.Contains("星") ? "star" : "flag",
                    view.Claimable ? VisualA1101.Teal : VisualA1101.Gold, 34f);
                PlaceTop(kind, 36, 12, 34, 34);
                NewPlacedText(row.transform, view.Title, 20, ink, 78, 8, 340, 38, TextAnchor.MiddleLeft, FontStyle.Bold);
                float rx = 40f;
                foreach (CurrencyAmount reward in view.Rewards)
                {
                    if (reward == null) continue;
                    Image badge = NewImage("Reward-" + reward.Currency, row.transform, RewardItemVisuals.SpriteFor(reward.Currency), Color.white);
                    badge.preserveAspect = true;
                    PlaceTop(badge.rectTransform, rx, 46, 30, 30);
                    Text amount = NewPlacedText(row.transform, $"×{reward.Amount}", 17, new Color32(255, 201, 110, 255),
                        rx + 32, 46, 90, 30, TextAnchor.MiddleLeft, FontStyle.Bold);
                    rx += 32f + amount.preferredWidth + 16f;
                }
                // Progress bar inside the row.
                float progress = view.Target > 0 ? Mathf.Clamp01(view.Progress / (float)view.Target) : 1f;
                Image bar = NewImage("Progress", row.transform, null, new Color32(48, 36, 70, 255));
                PlaceTop(bar.rectTransform, 40, 88, 290, 12);
                Image barFill = NewImage("Fill", bar.transform, null, Brand1025.Teal);
                barFill.rectTransform.anchorMin = Vector2.zero; barFill.rectTransform.anchorMax = new Vector2(progress, 1f);
                barFill.rectTransform.offsetMin = barFill.rectTransform.offsetMax = Vector2.zero;
                NewPlacedText(row.transform, $"{Mathf.Min(view.Progress, view.Target)}/{view.Target}", 15, Muted,
                    334, 80, 80, 28, TextAnchor.MiddleLeft, FontStyle.Bold);
                P5Claim(row.transform, $"ChapterTaskClaim-{index + 1}", view.Claimable, view.Claimed, "未达成", () => ClaimChapterTask(taskId));
            }
        }

        private void ClaimChapterTask(string taskId)
        {
            bool claimed = model.TryClaimChapterTask(chapter, taskId, out string message);
            if (claimed)
            {
                ModalPolish038Chrome.PlaySuccess();
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

            // Shared paper contour, while the city/harbor art remains visible behind it.
            Image cardImage = NewImage("ModalCard", modalRoot.transform,
                null, SirenStickerUi.Ink);
            cardImage.type = Image.Type.Sliced;
            cardImage.raycastTarget = true;
            GameObject card = cardImage.gameObject;
            PlaceTop(card.GetComponent<RectTransform>(), 30, top, 660, height);
            SirenStickerUi.SkinPanel(card);

            Image closeImage = NewImage("CloseChapterModal", card.transform,
                ReferencePunkFx.SlashPanel(false), new Color32(35, 19, 59, 255));
            closeImage.type = Image.Type.Sliced;
            closeImage.raycastTarget = true;
            GameObject close = closeImage.gameObject;
            PlaceTop(close.GetComponent<RectTransform>(), 586, 18, 52, 52);
            Button closeButton = close.AddComponent<Button>();
            closeButton.targetGraphic = closeImage;
            closeButton.transition = Selectable.Transition.None;
            closeButton.onClick.AddListener(CloseChapterModal);
            NewPlacedText(close.transform, "×", 28, White, 0, 0, 52, 52,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            close.AddComponent<LobbyHotspotFeedback>().Configure(LobbyHotspotFeedback.VisualKind.Settings);
            SirenStickerUi.SkinButton(close, false, true);
            return card;
        }

        // ------------------------------------------------------------------ 扫荡 (2026-10-21)

        private GameObject sweepRow;
        private Text sweepOneLabel, sweepThreeLabel;

        private void BuildSweepRow(Transform page)
        {
            sweepRow = NewObject("SweepRow", page);
            RectTransform row = sweepRow.AddComponent<RectTransform>();
            PlaceTop(row, 360, 1154, 330, 52); // Clear of both the 1-1 node and the team row below.
            sweepOneLabel = SweepButton(row, "SweepOnce", 0, 150, () => Sweep(1));
            sweepThreeLabel = SweepButton(row, "SweepThree", 166, 164, () => Sweep(3));
            sweepRow.SetActive(false);
        }

        private void LateUpdateSweepOrder()
        {
            if (sweepRow != null && sweepRow.activeSelf && sweepRow.transform.GetSiblingIndex() != sweepRow.transform.parent.childCount - 1)
                sweepRow.transform.SetAsLastSibling();
        }

        private Text SweepButton(Transform row, string name, float x, float width, UnityAction action)
        {
            Image hit = NewImage(name, row, null, Color.clear);
            hit.raycastTarget = true;
            PlaceTop(hit.rectTransform, x, 0, width, 52);
            Button button = hit.gameObject.AddComponent<Button>();
            button.targetGraphic = hit; button.transition = Selectable.Transition.None;
            button.onClick.AddListener(action);
            Text label = NewPlacedText(hit.transform, string.Empty, 20, White, 0, 0, width, 52, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(label.rectTransform); label.raycastTarget = false; label.name = "Label";
            label.supportRichText = true;
            // Unified design: the shared dark sticker button (no slanted plate + underline of its own).
            SirenStickerUi.SkinButton(hit.gameObject);
            hit.gameObject.AddComponent<LobbyHotspotFeedback>().Configure(LobbyHotspotFeedback.VisualKind.Settings);
            return label;
        }

        private void RefreshSweepRow(string stageId, int staminaCost)
        {
            if (sweepRow == null) return;
            bool can = model.CanSweep(stageId);
            sweepRow.SetActive(can && !challengeOpen);
            LateUpdateSweepOrder();
            if (!can) return;
            sweepOneLabel.text = $"扫荡×1  <color=#FFBE5C>-{staminaCost}</color>";
            sweepThreeLabel.text = $"扫荡×3  <color=#FFBE5C>-{staminaCost * 3}</color>";
            sweepOneLabel.supportRichText = sweepThreeLabel.supportRichText = true;
        }

        private void Sweep(int times)
        {
            if (challengeOpen || ChoSiren.UI.ChallengeVideoTransition.IsCoveringGame) return;
            string stageId = StageId(selectedStage);
            ulong seed = unchecked((ulong)DateTime.UtcNow.Ticks ^ (ulong)(selectedStage * 7919));
            if (!model.TrySweepStage(stageId, times, seed, out string message))
            {
                Notify(message);
                return;
            }
            Refresh();
            Notify(message); // reward-ui 1101: the shared 获得奖励 popup shows the haul; this becomes its caption
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
            // 2026-10-31: the shared torn-paper teal notice sticker (no count), same as the lobby.
            GameObject badge = ChoSiren.UI.NoticeSticker1031.Create(name, parent);
            PlaceTop(badge.GetComponent<RectTransform>(), x + 11, y + 2, 18, 18);
            Text value = NewPlacedText(badge.transform, "0", 13, White,
                2, 1, 27, 28, TextAnchor.MiddleCenter, FontStyle.Bold);
            value.name = "Value";
            value.gameObject.SetActive(false);
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
            // Unified design: the shared sticker panel (no rounded pink-rimmed card of its own).
            toastObject = NewPanel("LevelToast", transform, new Color32(10, 11, 42, 247), 20);
            PlaceTop(toastObject.GetComponent<RectTransform>(), 70, 900, 580, 70);
            SirenStickerUi.SkinPanel(toastObject);
            toastText = NewPlacedText(toastObject.transform, string.Empty, 18, White,
                24, 8, 532, 54, TextAnchor.MiddleCenter, FontStyle.Bold);
            toastObject.SetActive(false);
        }

        private void SelectStage(int stage)
        {
            if (stage < 1 || stage > ChapterStageCount) return;
            LevelState state = StateFor(stage);
            selectedStage = stage;
            Refresh();
            if (state == LevelState.Locked)
            {
                Notify($"{chapter}-{stage} 尚未解锁，请先完成当前关卡");
                return;
            }
            if (vinylMap1102) return; // Selection already updates the scene, title and reward card.
            Notify(state == LevelState.Cleared
                ? $"已选择 {chapter}-{stage}，可再次挑战；首通奖励不会重复发放"
                : $"已选择当前关卡 {chapter}-{stage}");
        }

        private bool introOpen;

        private void OpenStageTeamPresets1028()
        {
            GameObject card = OpenModalShell("StageTeamPresets", 500, 620);
            NewPlacedText(card.transform, "出战队伍", 30, White, 32, 30, 540, 54, TextAnchor.MiddleLeft, FontStyle.Bold);
            for (int i = 0; i < model.VisibleTeamPresetCount; i++)
            {
                int slot = i;
                var preset = model.Save.TeamPresets[i];
                bool empty = preset.Members.Count == 0;
                GameObject button = NewButton("StageTeam-" + i, card.transform,
                    preset.Name + (empty ? " · 未保存" : i == model.Save.ActiveTeamPreset ? " · 出战中" : ""),
                    25, i == model.Save.ActiveTeamPreset ? Accent : new Color32(35, 19, 59, 255), White, () =>
                    {
                        if (!model.SelectTeamPreset(slot, out string message)) { Notify(message); return; }
                        CloseChapterModal(); Refresh();
                    }, 12);
                PlaceTop(button.GetComponent<RectTransform>(), 32, 108 + i * 86, 576, 72);
                button.GetComponent<Button>().interactable = !empty;
            }
        }

        private void OpenFormationProblem(string problem)
        {
            // Keep the selected chapter and its explanation visible until the player
            // chooses to adjust the formation. A transient map toast disappears on exit.
            GameObject card = OpenModalShell("FormationProblemModal", 420, 480);
            SirenStickerUi.SkinTitle(NewPlacedText(card.transform, "编队需要调整", 27, White,
                30, 34, 520, 52, TextAnchor.MiddleLeft, FontStyle.Bold));
            Text reason = NewPlacedText(card.transform, problem, 23, Warn,
                34, 118, 588, 132, TextAnchor.MiddleLeft, FontStyle.Bold);
            reason.name = "FormationProblemReason";
            reason.horizontalOverflow = HorizontalWrapMode.Wrap;
            reason.verticalOverflow = VerticalWrapMode.Overflow;
            NewPlacedText(card.transform, "角色和培养进度已保留，本次未消耗体力", 18, White,
                34, 266, 588, 42, TextAnchor.MiddleCenter, FontStyle.Normal);
            GameObject cancel = NewButton("FormationProblemCancel", card.transform, "暂不调整", 22,
                new Color32(35, 19, 59, 255), White, CloseChapterModal, 12);
            PlaceTop(cancel.GetComponent<RectTransform>(), 34, 350, 264, 76);
            if (onGrowth != null)
            {
                GameObject adjust = NewButton("FormationProblemAdjust", card.transform, "调整编队", 22,
                    Accent, White, () =>
                    {
                        Action<string> growth = onGrowth;
                        CloseChapterModal();
                        gameObject.SetActive(false);
                        Destroy(gameObject);
                        growth("team-fix");
                    }, 12);
                PlaceTop(adjust.GetComponent<RectTransform>(), 326, 350, 300, 76);
            }
        }

        private void StartChallenge()
        {
            // A callback must obey the same cover as pointer input. Starting another
            // transition while one owns the screen would spend stamina then lose Play.
            if (challengeOpen || ChoSiren.UI.ChallengeVideoTransition.IsCoveringGame) return;
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

            string stageId = StageId(selectedStage);
            if (!model.IsTeamReady(out string teamProblem))
            {
                OpenFormationProblem(teamProblem);
                return;
            }
            StageDefinition costDefinition = model.Tactics.FindStage(stageId);
            if (onStamina != null && costDefinition != null && model.Save.Stamina < costDefinition.StaminaCost)
            {
                Notify($"体力不足：本关需要 {costDefinition.StaminaCost} 点");
                onStamina();
                return;
            }
            // First time on a stage: a short scene plays, then the challenge continues.
            string introId = stageId;
            if (introOpen) return;
            if (!model.IsStageCleared(stageId) && !model.IsStoryCompleted(introId) &&
                model.TryStartStory(introId, out StoryRunner intro, out _))
            {
                introOpen = true;
                StoryPanel.Open(transform.parent, model, intro,
                    finished: () =>
                    {
                        introOpen = false;
                        model.CompleteStory(introId);
                        StartChallenge();
                    },
                    audio: null,
                    characterName: ResolveStoryCharacterName,
                    back: () => { introOpen = false; model.CompleteStory(introId); Refresh(); },
                    message: Notify);
                return;
            }

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
            bool firstClear = !model.IsStageCleared(stageId);
            pendingOutro = null;
            Notify(message);
            Transform battleHost = transform.parent;
            Action openBattle = () => TacticsBattlePanel.Open(battleHost, model, battle,
                finished: simulator =>
                {
                    model.SettleStageBattle(simulator, out lastSettleMessage);
                    // 2026-10-24 (owner): a first clear ends with the cast talking about the win, not a system card.
                    if (firstClear && simulator.Outcome == BattleOutcome.Victory) pendingOutro = stageId + "-end";
                    if (!string.IsNullOrEmpty(lastSettleMessage)) Notify(lastSettleMessage);
                    if (model.IsStageCleared("stage-1-20") && model.FirstTime("notice-chapter-two-open"))
                        lastSettleMessage += "\n第二章已开放：点右上「第二章 ▶」";
                },
                rewards: BuildBattleRewardLines,
                growth: destination =>
                {
                    challengeOpen = false;
                    if (destination == "farm")
                    {
                        while (selectedStage > 1 && StateFor(selectedStage) != LevelState.Cleared) selectedStage--;
                        gameObject.SetActive(true);
                        Refresh();
                        Notify(StateFor(selectedStage) == LevelState.Cleared
                            ? "重刷可拿基础资源，首通奖励只发一次"
                            : "先升级成员，再来挑战 1-1");
                    }
                    else if (onGrowth != null)
                    {
                        gameObject.SetActive(false);
                        Destroy(gameObject);
                        onGrowth(destination);
                    }
                    else Close();
                },
                back: () =>
                {
                    challengeOpen = false;
                    selectedStage = CurrentStoryStage();
                    Refresh();
                    PlayPendingOutro();
                },
                message: Notify);
            // The reference clip plays first; the battle is built underneath once it covers the map.
            GameAudio audio = FindAnyObjectByType<GameAudio>();
            ChoSiren.UI.ChallengeVideoTransition.Warm(transform).Play(battleHost, openBattle, sound => audio?.PlaySfx(sound));
        }

        private string pendingOutro;

        /// <summary>The short post-battle scene of a first clear (Data/Story/stage-x-y-end), once.</summary>
        private void PlayPendingOutro()
        {
            string outroId = pendingOutro;
            pendingOutro = null;
            if (string.IsNullOrEmpty(outroId) || model.IsStoryCompleted(outroId) || introOpen) return;
            if (!model.TryStartStory(outroId, out StoryRunner outro, out _)) return;
            introOpen = true;
            StoryPanel.Open(transform.parent, model, outro,
                finished: () => { introOpen = false; model.CompleteStory(outroId); Refresh(); },
                audio: null,
                characterName: ResolveStoryCharacterName,
                back: () => { introOpen = false; model.CompleteStory(outroId); Refresh(); },
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
                    if (stage.GoldReward > 0) lines.Add($"星光币 +{stage.GoldReward}");
                    if (stage.DiamondFirstClear > 0) lines.Add($"星钻 +{stage.DiamondFirstClear}");
                }
            }

            return lines;
        }

        private void OpenStoryChapter01()
        {
            if (chapter == 3) { Notify("第三章剧情还在制作中"); return; }
            string storyId = chapter == 2 ? "chapter-02" : "chapter-01";
            if (!model.TryStartStory(storyId, out StoryRunner runner, out string message))
            {
                Notify(string.IsNullOrEmpty(message) ? "剧情无法开始" : message);
                return;
            }

            StoryPanel.Open(transform.parent, model, runner,
                finished: () =>
                {
                    model.CompleteStory(storyId);
                    Notify(chapter == 2 ? "已读完第 02 章" : "已读完第 01 章");
                    Refresh();
                },
                audio: null,
                characterName: ResolveStoryCharacterName,
                back: Refresh,
                message: Notify);
        }

        private string ResolveStoryCharacterName(string characterId)
        {
            if (string.IsNullOrEmpty(characterId)) return "未知";
            if (string.Equals(characterId, "producer", StringComparison.Ordinal)) return model.PlayerName;
            int index = GameModel.IndexOfMember(characterId);
            return index >= 0 ? GameModel.Members[index].Name : "未知";
        }

        private void HandleModelChanged()
        {
            Refresh();
        }

        private string StagePaceLabel(StageDefinition stage)
        {
            var party = model.Save.Team.Where(i => i >= 0 && i < GameModel.Members.Length).ToArray();
            double average = party.Length > 0 ? party.Average(i => model.Save.MemberLevels[i]) : 1f;
            string comparison = $"\u5efa\u8bae{stage.RecommendedLevel}\u7ea7 \u00b7 \u961f\u5747{average:0.#}\u7ea7";
            return average >= stage.RecommendedLevel * 2f ? comparison + " \u00b7 \u9002\u5408\u901f\u5237" : comparison;
        }

        private void Refresh()
        {
            if (!built || model == null) return;
            if (activeTeamText1028 != null) activeTeamText1028.text = "出战：" + model.ActiveTeamName + "    切换 ›";
            // Keep the 开始挑战 clip decoded while the map is on screen.
            if (isActiveAndEnabled && !challengeOpen) ChoSiren.UI.ChallengeVideoTransition.Warm(transform);

            staminaText.text = $"{model.Save.Stamina}/{model.StaminaCap}";
            if (storyChapterLabel != null)
                storyChapterLabel.text = chapter == 3 ? "主线剧情 · 制作中" : chapter == 2
                    ? (model.IsStoryCompleted("chapter-02") ? "主线剧情 · 已读" : "主线剧情 · 第二章")
                    : model.IsStoryCompleted("chapter-01") ? "主线剧情 · 已读" : "主线剧情 · 第一章";
            stageText.text = $"{chapter}-{selectedStage}";
            if (chapterSwitchLabel != null)
            {
                bool otherOpen = ChapterUnlocked(chapter == 1 ? 2 : 1);
                chapterSwitchLabel.text = chapter == 1 ? (otherOpen ? "第二章 ▶" : "第二章 · 未解锁") : "◀ 第一章";
                chapterSwitchLabel.color = otherOpen ? White : Muted;
            }

            LevelState state = StateFor(selectedStage);
            string selectedStageId = StageId(selectedStage);
            StageDefinition selectedDefinition = model.Tactics.FindStage(selectedStageId);
            int staminaCost = selectedDefinition != null ? selectedDefinition.StaminaCost : GameModel.StoryStaminaCost;
            int diamondReward = selectedDefinition != null ? selectedDefinition.DiamondFirstClear : GameModel.StoryDiamondReward;
            int goldReward = selectedDefinition != null
                ? model.PreviewStageGoldReward(selectedStageId)
                : GameModel.StoryGoldReward;
            bool chapterComplete = chapter == 1 ? model.IsChapterOneComplete : model.IsStageCleared($"stage-{chapter}-20");
            stageTitleText.text = selectedDefinition != null && !string.IsNullOrWhiteSpace(selectedDefinition.Name)
                ? selectedDefinition.Name
                : chapter == 3 ? "幻域终章演出" : chapter == 2 ? "魅声海湾演出" : "欲望都市演出";
            // One short line: pace + (when cleared) the best stars. Everything else is visual.
            stageStatusText.text = string.Empty;
            string bestStars = state == LevelState.Cleared ? "   " + new string('★', Mathf.Clamp(model.StarsOf(selectedStageId), 1, 3)) : string.Empty;
            progressText.text = (selectedDefinition != null && selectedDefinition.UsesRealtime
                ? StagePaceLabel(selectedDefinition)
                : $"敌方战力 {model.EnemyPowerOfStage(selectedStageId):N0}") + bestStars;
            staminaCostText.text = $"-{staminaCost}";
            diamondRewardText.text = state == LevelState.Cleared ? "已领" : $"+{diamondReward}";
            goldRewardText.text = $"+{goldReward}";
            RefreshDropPreview(selectedStageId);
            startLabel.text = state == LevelState.Cleared ? "再次挑战" : "开始挑战";
            startButton.interactable = !challengeOpen;
            RefreshSweepRow(selectedStageId, staminaCost);
            startBackground.color = model.Save.Stamina < staminaCost
                ? new Color32(125, 135, 160, 175)
                : state == LevelState.Cleared
                    ? new Color32(210, 224, 255, 235)
                    : White;
            if (authoredMap)
            {
                startBackground.color = Color.clear;
                startLabel.text = state == LevelState.Cleared ? "再次挑战" : string.Empty;
            }

            int claimableRewards = model.ChapterClaimableStarRewardCount(chapter);
            rewardSummaryText.text = claimableRewards > 0 ? "有奖励可领取" : "查看星级奖励";
            rewardBadgeText.text = claimableRewards.ToString();
            rewardBadgeText.transform.parent.gameObject.SetActive(claimableRewards > 0);

            List<ChapterTaskView> chapterTasks = model.ChapterTaskViews(chapter);
            int completedTasks = 0;
            for (int taskIndex = 0; taskIndex < chapterTasks.Count; taskIndex++)
                if (chapterTasks[taskIndex].Completed) completedTasks++;
            taskSummaryText.text = $"{completedTasks}/{chapterTasks.Count} 已完成";
            int claimableTasks = model.ChapterClaimableTaskCount(chapter);
            taskBadgeText.text = claimableTasks.ToString();
            taskBadgeText.transform.parent.gameObject.SetActive(claimableTasks > 0);
            if (authoredMap) RefreshStageInfo1101(selectedDefinition, state, selectedStageId, completedTasks, chapterTasks.Count);


            foreach (KeyValuePair<int, NodeView> pair in nodeViews)
            {
                NodeView view = pair.Value;
                view.State = StateFor(view.Stage);
                bool selected = view.Stage == selectedStage;
                if (view.LockIcon != null) view.LockIcon.enabled = view.State == LevelState.Locked;
                if (authoredMap)
                {
                    view.Background.color = view.State == LevelState.Locked
                        ? new Color32(8, 7, 23, 162) : Color.clear;
                    view.Glow.enabled = selected || view.State == LevelState.Current;
                    Transform flameNode = view.Button != null ? view.Button.transform.Find($"SelectedFlame-{chapter}-{view.Stage}") : null;
                    if (flameNode != null) flameNode.gameObject.SetActive(selected && view.State != LevelState.Locked);
                    // The selected stage sits on the teal plate: ink text, pink/white vanish on it.
                    bool onGreen = selected && view.State != LevelState.Locked;
                    view.StageLabel.color = view.State == LevelState.Locked ? Muted : onGreen ? (Color)Brand1025.Ink : White;
                    view.StatusLabel.text = view.State == LevelState.Cleared
                        ? new string('★', Mathf.Clamp(model.StarsOf(StageId(view.Stage)), 1, 3))
                        : view.State == LevelState.Locked ? string.Empty : "可挑战";
                    view.StatusLabel.color = view.State == LevelState.Locked ? Muted : onGreen ? (Color)Brand1025.Ink
                        : view.State == LevelState.Cleared ? StarGold : Accent;
                    continue;
                }
                if (view.State == LevelState.Locked)
                {
                    view.Glow.enabled = false;
                    view.Background.color = new Color32(190, 198, 231, 225);
                    view.StageLabel.color = White;
                    view.StatusLabel.text = StateLabel(view.State);
                    view.StatusLabel.color = new Color32(215, 219, 242, 255);
                    view.Outline.effectColor = new Color32(111, 127, 177, 105);
                    if (view.Tag != null) view.Tag.color = new Color32(120, 132, 178, 160);
                    continue;
                }

                if (view.Tag != null)
                    view.Tag.color = view.State == LevelState.Current || selected
                        ? new Color32(255, 112, 220, 235)
                        : new Color32(104, 198, 255, 205);
                view.Glow.enabled = selected || view.State == LevelState.Current;
                if (view.State == LevelState.Current) currentGlow = view.Glow;
                int stars = model.StarsOf(StageId(view.Stage));
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

            RefreshVinylMap1102(selectedDefinition);

            for (int index = 0; index < routeViews.Count; index++)
            {
                RouteView route = routeViews[index];
                bool future = StateFor(route.TargetStage) == LevelState.Locked;
                Color glowColor = future
                    ? new Color32(94, 118, 175, 34)
                    : new Color32(255, 98, 218, 58);
                Color lineColor = future
                    ? new Color32(159, 177, 219, 145)
                    : new Color32(232, 220, 255, 235);
                for (int segment = 0; segment < route.Glow.Count; segment++)
                    route.Glow[segment].color = glowColor;
                for (int segment = 0; segment < route.Core.Count; segment++)
                    route.Core[segment].color = lineColor;
                route.Marker.color = future
                    ? new Color32(166, 181, 216, 180)
                    : new Color32(255, 214, 246, 255);
            }
        }

        private void Notify(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            if (RewardGain1101.Absorb(message)) return; // same action just raised the 获得奖励 popup
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
            string stageId = StageId(stage);
            if (model.IsStageCleared(stageId) || (chapter == 1 && IsStoryStageCleared(stage, model.Save.StoryProgress)))
                return LevelState.Cleared;
            return model.IsStageUnlocked(stageId) ? LevelState.Current : LevelState.Locked;
        }

        private int CurrentStoryStage()
        {
            if (chapter == 1) return model.CurrentChapterOneStage;
            for (int stage = 1; stage <= ChapterStageCount; stage++)
                if (!model.IsStageCleared(StageId(stage))) return stage;
            return ChapterStageCount;
        }

        /// <summary>Rebuilds the map for the other chapter; a locked chapter only explains why.</summary>
        private void SwitchChapter(int number)
        {
            if (number < 1 || number > ChapterCount || number == chapter) return;
            if (!ChapterUnlocked(number))
            {
                Notify(number == 3 ? "通关 2-20 后开放第三章" : "通关 1-20 后开放第二章");
                return;
            }
            lastChapter = number;
            chapter = number;
            for (int index = transform.childCount - 1; index >= 0; index--)
                Destroy(transform.GetChild(index).gameObject);
            nodeViews.Clear();
            routeViews.Clear();
            currentGlow = null;
            built = false;
            selectedStage = CurrentStoryStage();
            Build();
            built = true;
            Refresh();
            Notify(chapter == 3 ? "第三章 · 幻域终章" : chapter == 2 ? "第二章 · 魅声海湾" : "第一章 · 欲望都市");
        }

        public static int StoryStageForProgress(int progress)
        {
            int normalizedProgress = Mathf.Clamp(progress, 0, GameModel.MaxStoryProgress);
            for (int index = 0; index < GameModel.StoryStageThresholds.Length; index++)
            {
                if (normalizedProgress < GameModel.StoryStageThresholds[index]) return index + 1;
            }

            return GameModel.LegacyStoryStageCount;
        }

        public static bool IsStoryStageCleared(int stage, int progress)
        {
            // Only 1-1…1-10 have legacy story progress (2026-11-11: chapters run to 20 stages).
            if (stage < 1 || stage > GameModel.LegacyStoryStageCount) return false;
            return Mathf.Clamp(progress, 0, GameModel.MaxStoryProgress) >=
                   GameModel.StoryStageThresholds[stage - 1];
        }

        private static string StateLabel(LevelState state)
        {
            return state == LevelState.Cleared ? "★★★" : state == LevelState.Current ? "当前" : "锁定";
        }

        private void AddRouteSegment(Transform parent, Vector2 from, Vector2 to, bool future)
        {
            Color glowColor = future ? new Color32(94, 118, 175, 34) : new Color32(255, 98, 218, 58);
            Color lineColor = future ? new Color32(159, 177, 219, 145) : new Color32(232, 220, 255, 235);

            // Lightning bolt: two sharp jags around the midpoint instead of one straight wire.
            Vector2 delta = to - from;
            Vector2 perp = delta.sqrMagnitude > 0.01f
                ? new Vector2(-delta.y, delta.x).normalized
                : Vector2.right;
            float side = routeViews.Count % 2 == 0 ? 1f : -1f;
            Vector2 apexA = Vector2.Lerp(from, to, 0.36f) + perp * (17f * side);
            Vector2 apexB = Vector2.Lerp(from, to, 0.64f) - perp * (12f * side);
            Vector2[] spine = { from, apexA, apexB, to };

            var glow = new List<Image>();
            var core = new List<Image>();
            for (int segment = 0; segment < spine.Length - 1; segment++)
            {
                glow.Add(AddLine(parent, spine[segment], spine[segment + 1], 11f, glowColor));
                core.Add(AddLine(parent, spine[segment], spine[segment + 1], 3f, lineColor));
            }

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
            // one teal main action per dialog (unified design 2026-10-25)
            SirenStickerUi.SkinButton(result, name == "StartChallenge" || name == "SweepResultOk" || name == "FormationProblemAdjust", true);
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
