using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using ChoSiren.Systems.Story;
using UnityEngine;
using UnityEngine.UI;
using ChoSiren.UI;

namespace ChoSiren.Panels
{
    /// <summary>
    /// Visual-novel presenter for <see cref="StoryRunner"/>: scene actors and a separate speaker
    /// bust beside the lower-right reading area, choices, 自动 / 跳过 / 记录. Audio directives are
    /// forwarded to <c>onAudio(command, assetId)</c>; <c>onFinished</c> fires once when the script ends.
    /// </summary>
    public sealed class StoryPanel : MonoBehaviour
    {
        private const float CharSeconds = 0.03f;
        private const float AutoBaseDelay = 1.2f;
        private const float AutoPerCharDelay = 0.045f;
        private const string DefaultBackgroundResource =
            "Art/MemberAI/member-gallery-calm-bg-ai-v1-20260903";
        private const string LobbyBackgroundResource = "Art/LobbyBackground";
        private const string SpeakerPortraitResource = "Art/Story/Portraits1019/";
        private const string SceneBackgroundResource = "Art/Story/Scenes1019/";
        // Owner pick C2 (2026-10-20): one full-width torn-paper strip along the bottom, a magenta
        // name ribbon on its top edge; the box no longer stands taller than the speaker.
        private const float ConversationHeight = 232f;
        private const float ConversationX = 12f;
        private const float ConversationWidth = 696f;
        private static readonly Color DialogueInk = new Color32(20, 14, 26, 255);
        private static Sprite paperStripArt, nameRibbonArt;
        private static Sprite LoadUiArt(ref Sprite cache, string path)
        {
            if (cache != null) return cache;
            Texture2D texture = Resources.Load<Texture2D>(path);
            if (texture == null) return null;
            cache = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 100f);
            return cache;
        }

        // Keep the original semantic IDs usable by older saves/scripts while new scripts can
        // point at any Resources path directly. This also makes chapter data portable when its
        // art is reorganised later.
        private static readonly Dictionary<string, string> BackgroundResourceAliases =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "bg-rehearsal-room-night", "Art/Story/rehearsal-night" },
                { "bg-neon-alley", SceneBackgroundResource + "stage-1-1" },
            };

        // These authored paintings already contain their actors. Other Scenes1019 art is
        // environment-only and must keep the classic show/hide presentation semantics.
        private static readonly Dictionary<string, string> IllustratedSceneFallbacks =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { SceneBackgroundResource + "chapter-01-rehearsal-solo", "Art/Story/rehearsal-night" },
                { SceneBackgroundResource + "chapter-01-rehearsal-arrival", "Art/Story/rehearsal-night" },
                { SceneBackgroundResource + "chapter-01-alley-outro", SceneBackgroundResource + "stage-1-1" },
            };

        private sealed class SlotView
        {
            public string Position;
            public string CharacterId;
            public string Expression;
            public GameObject Root;
            public CanvasGroup Group;
            public Image Portrait;
            public GameObject Silhouette;
            public Text SilhouetteName;
        }

        private static readonly string[] SlotOrder = { "left", "center", "right" };

        private readonly List<SlotView> slots = new List<SlotView>();
        private readonly List<GameObject> choiceButtons = new List<GameObject>();
        private readonly List<string> history = new List<string>();
        private readonly Dictionary<Sprite, Sprite> fallbackBusts = new Dictionary<Sprite, Sprite>();

        private PanelKit kit;
        private GameModel model;
        private StoryRunner runner;
        private Action onFinished;
        private Action<string, string> onAudio;
        private Func<string, string> nameResolver;
        private Action onBack;
        private Action<string> onMessage;

        private bool closing;
        private bool finishedReported;
        private bool ended;
        private bool autoMode;
        private bool typing;
        private string fullLine = string.Empty;
        private Coroutine typeRoutine;
        private Coroutine autoRoutine;
        private Sprite backgroundFallback;

        private Image background;
        private UnityEngine.UI.Image readabilityVeil;
        private UnityEngine.UI.Image vignette;
        private UnityEngine.UI.Image illustratedReadingShade;
        private bool illustratedScene;
        private AspectRatioFitter backgroundCover;
        private Text backgroundId;
        private Text titleText;
        private GameObject dialogueBox;
        private GameObject nameTag;
        private GameObject producerAvatar;
        private Image producerAvatarArt;
        private Text nameText;
        private Text bodyText;
        private Text continueHint;
        private ScrollRect dialogueScroll;
        private RectTransform choiceLayer;
        private SecondaryStory038Action autoControl;
        private GameObject historyOverlay;
        private Text historyText;
        private RectTransform historyContent;
        private RectTransform speakerRoot;
        private Image speakerPortrait;
        private UnityEngine.UI.Outline speakerOutline;
        private Text producerMarker;
        private string speakerCharacterId;
        private Vector2 speakerRest;
        private Vector2 dialogueRest;
        private Coroutine speakerEntrance;
        private int backgroundRequest, speakerRequest;

        public static StoryPanel Open(Transform host, GameModel gameModel, StoryRunner storyRunner,
            Action finished, Action<string, string> audio = null, Func<string, string> characterName = null,
            Action back = null, Action<string> message = null)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            if (gameModel == null) throw new ArgumentNullException(nameof(gameModel));
            if (storyRunner == null) throw new ArgumentNullException(nameof(storyRunner));

            StoryPanel existing = host.GetComponentInChildren<StoryPanel>(true);
            if (existing != null) Destroy(existing.gameObject);

            GameObject panelObject = PanelKit.CreateOverlayRoot("StoryPanel", host);
            StoryPanel panel = panelObject.AddComponent<StoryPanel>();
            panel.model = gameModel;
            panel.runner = storyRunner;
            panel.onFinished = finished;
            panel.onAudio = audio;
            panel.nameResolver = characterName;
            panel.onBack = back;
            panel.onMessage = message;
            panel.PrewarmStoryArt();
            MusicContextScope.Attach(panelObject, GameAudio.MusicContext.Silent);
            panel.Build();
            panel.Begin();
            panel.StartCoroutine(panel.EnterConversation());
            return panel;
        }

        public bool AutoMode => autoMode;
        public bool IsTyping => typing;
        public bool Ended => ended;
        public IReadOnlyList<string> History => history;
        public string SpeakerCharacterId => speakerCharacterId;

        // ------------------------------------------------------------------ build

        private void Build()
        {
            kit = new PanelKit("Story");
            backgroundFallback = kit.CreateGradientSprite("StoryBackdrop",
                new Color32(12, 10, 48, 255), new Color32(46, 18, 96, 255), new Color32(8, 20, 60, 255));

            Image inputBlocker = kit.NewImage("StoryInputBlocker", transform, null, new Color(0f, 0f, 0f, 0.001f));
            PanelKit.Stretch(inputBlocker.rectTransform);
            inputBlocker.raycastTarget = true;

            // The generated gradient is deliberately stretchable. Scene art lives in its own
            // cover image below so raster backgrounds are never distorted on tall screens.
            Image fallback = kit.NewImage("StoryFallbackBackground", transform, backgroundFallback, PanelKit.White);
            PanelKit.Stretch(fallback.rectTransform);
            fallback.raycastTarget = false;

            background = kit.NewImage("Background", transform, null, new Color(1f, 1f, 1f, 0f));
            PanelKit.Stretch(background.rectTransform);
            background.raycastTarget = false;
            background.preserveAspect = true;
            backgroundCover = background.gameObject.AddComponent<AspectRatioFitter>();
            backgroundCover.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            ApplyBackgroundSprite(LoadFirstBackground(DefaultBackgroundResource, LobbyBackgroundResource));

            readabilityVeil = kit.NewImage("StoryReadabilityVeil", transform, null,
                new Color32(5, 9, 35, 54));
            PanelKit.Stretch(readabilityVeil.rectTransform);
            readabilityVeil.raycastTarget = false;

            vignette = kit.NewImage("Vignette", transform, kit.RadialSprite(), new Color32(255, 78, 212, 40));
            PanelKit.PlaceTop(vignette.rectTransform, -100, 500, 920, 900);

            illustratedReadingShade = kit.NewImage("IllustratedReadingShade", transform,
                kit.CreateGradientSprite("IllustratedReadingShade", new Color32(0, 0, 0, 0),
                    new Color32(0, 0, 0, 36), new Color32(0, 0, 0, 132)), Color.white);
            illustratedReadingShade.raycastTarget = false;
            illustratedReadingShade.gameObject.SetActive(false);

            backgroundId = kit.NewPlacedText(transform, string.Empty, 12, new Color32(200, 190, 230, 200), 16, 78, 400, 22,
                TextAnchor.MiddleLeft);

            BuildTopBar();
            BuildSlots();
            BuildSpeaker();
            BuildDialogue();
            BuildChoices();
            BuildHistory();
            LayoutConversation();
        }

        private void BuildTopBar()
        {
            Image bar = kit.NewImage("TopBar", transform, null, new Color32(11, 8, 18, 112));
            PanelKit.PlaceTop(bar.rectTransform, 0, 0, 720, 72);

            SecondaryStory038Action back = SecondaryStory038Chrome.GlassAction(bar.transform, "Back",
                "返回", 24, SecondaryStory038Chrome.SocketUtility, SecondaryStory038Chrome.EdgeViolet,
                Leave, mirror: true);
            PanelKit.PlaceTop(back.Root.GetComponent<RectTransform>(), 18, 14, 86, 48);

            titleText = kit.NewPlacedText(bar.transform, runner.Script.Title, 26, SirenStickerUi.Paper, 116, 14, 260, 48,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            PanelKit.EnableBestFit(titleText, 22);
            SirenStickerUi.SkinTitle(titleText);

            autoControl = SecondaryStory038Chrome.GlassAction(bar.transform, "AutoPlay", "自动", 24,
                SecondaryStory038Chrome.SocketUtility, SecondaryStory038Chrome.EdgeViolet, ToggleAuto);
            PanelKit.PlaceTop(autoControl.Root.GetComponent<RectTransform>(), 392, 14, 92, 48);
            SecondaryStory038Action skip = SecondaryStory038Chrome.GlassAction(bar.transform, "Skip",
                "跳过", 24, SecondaryStory038Chrome.SocketUtility, SecondaryStory038Chrome.EdgeViolet,
                SkipToDecision, mirror: true);
            PanelKit.PlaceTop(skip.Root.GetComponent<RectTransform>(), 496, 14, 92, 48);
            SecondaryStory038Action historyButton = SecondaryStory038Chrome.GlassAction(bar.transform,
                "History", "记录", 24, SecondaryStory038Chrome.SocketUtility,
                SecondaryStory038Chrome.EdgeViolet, ShowHistory);
            PanelKit.PlaceTop(historyButton.Root.GetComponent<RectTransform>(), 600, 14, 102, 48);
        }

        private void BuildSlots()
        {
            float[] lefts = { -30f, 200f, 430f };
            for (int index = 0; index < SlotOrder.Length; index++)
            {
                RectTransform root = kit.NewRect("Slot-" + SlotOrder[index], transform);
                PanelKit.PlaceTop(root, lefts[index], 180, 280, 700);
                CanvasGroup group = root.gameObject.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false;

                Image portrait = kit.NewImage("Portrait", root, null, PanelKit.White);
                PanelKit.Stretch(portrait.rectTransform);
                portrait.preserveAspect = true;

                GameObject silhouette = SecondaryStory038Chrome.Plate(root, "Silhouette",
                    new Color32(20, 16, 58, 215), index % 2 == 1, 24f,
                    SecondaryStory038Chrome.CutViolet, 60, 160, 200, 640).gameObject;
                Image head = kit.NewImage("Head", silhouette.transform, kit.RadialSprite(), new Color32(120, 90, 200, 180));
                PanelKit.PlaceTop(head.rectTransform, 40, 50, 120, 120);
                Text silhouetteName = kit.NewPlacedText(silhouette.transform, string.Empty, 18, PanelKit.White, 10, 300,
                    180, 40, TextAnchor.MiddleCenter, FontStyle.Bold);
                kit.NewPlacedText(silhouette.transform, string.Empty, 12, PanelKit.Muted, 10, 344, 180, 24,
                    TextAnchor.MiddleCenter);

                SlotView slot = new SlotView
                {
                    Position = SlotOrder[index],
                    Root = root.gameObject,
                    Group = group,
                    Portrait = portrait,
                    Silhouette = silhouette,
                    SilhouetteName = silhouetteName,
                };
                root.gameObject.SetActive(false);
                slots.Add(slot);
            }
        }

        private void BuildSpeaker()
        {
            speakerRoot = kit.NewRect("SpeakerPortraitRoot", transform);
            speakerRoot.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
            speakerPortrait = kit.NewImage("SpeakerPortrait", speakerRoot, null, Color.white);
            PanelKit.Stretch(speakerPortrait.rectTransform);
            speakerPortrait.rectTransform.pivot = new Vector2(.5f, 0f);
            speakerPortrait.preserveAspect = true;
            speakerPortrait.raycastTarget = false;
            speakerOutline = speakerPortrait.gameObject.AddComponent<UnityEngine.UI.Outline>();
            speakerOutline.effectColor = new Color32(237, 241, 255, 235);
            speakerOutline.effectDistance = new Vector2(2.5f, -2.5f);
            speakerOutline.useGraphicAlpha = true;
            producerMarker = kit.NewPlacedText(speakerRoot, model.PlayerName, 24, SirenStickerUi.Paper,
                32, 460, 210, 40, TextAnchor.MiddleLeft, FontStyle.Bold);
            producerMarker.gameObject.name = "ProducerMarker";
            var markerShadow = producerMarker.gameObject.AddComponent<UnityEngine.UI.Shadow>();
            markerShadow.effectColor = new Color32(8, 5, 18, 235);
            markerShadow.effectDistance = new Vector2(1.5f, -1.5f);
            speakerRoot.gameObject.SetActive(false);
        }

        private void BuildDialogue()
        {
            SecondaryStory038Action dialogue = SecondaryStory038Chrome.GlassAction(transform,
                "DialogueBox", null, 12, Color.clear,
                new Color32(196, 162, 255, 110), DialogueClicked, sheenStrength: .02f);
            dialogueBox = dialogue.Root;
            PlaceBottom(dialogueBox.GetComponent<RectTransform>(), ConversationX, 20, ConversationWidth, ConversationHeight);
            dialogue.Socket.color = Color.clear;
            Transform face = dialogue.Body;
            Sprite strip = LoadUiArt(ref paperStripArt, "UI/StoryC2/dialogue-paper");
            dialogue.Face.color = strip != null ? Color.clear : SirenStickerUi.Paper;
            // The glass edge/sheen read as a purple block behind the paper: hide them.
            Image edge = dialogue.Face.transform.parent != null ? dialogue.Face.transform.parent.GetComponent<Image>() : null;
            if (edge != null) edge.color = Color.clear;
            if (dialogue.Sheen != null) dialogue.Sheen.gameObject.SetActive(false);
            Image paper = kit.NewImage("DialoguePaperStrip", face, strip, strip != null ? Color.white : Color.clear);
            PanelKit.Stretch(paper.rectTransform, -14f, -16f, 14f, 16f);
            paper.raycastTarget = false;

            Sprite ribbon = LoadUiArt(ref nameRibbonArt, "UI/StoryC2/name-ribbon");
            Image tag = kit.NewImage("NameTag", face, ribbon, ribbon != null ? Color.white : new Color32(255, 47, 142, 255));
            tag.preserveAspect = false;
            tag.raycastTarget = false;
            nameTag = tag.gameObject;
            nameText = kit.NewPlacedText(nameTag.transform, string.Empty, 30, Color.white,
                30, 0, 200, 64, TextAnchor.MiddleLeft, FontStyle.Bold);
            nameText.gameObject.name = "SpeakerName";
            nameText.rectTransform.localEulerAngles = new Vector3(0, 0, 5f);
            var nameShadow = nameText.gameObject.AddComponent<UnityEngine.UI.Shadow>();
            nameShadow.effectColor = new Color32(20, 14, 26, 255); nameShadow.effectDistance = new Vector2(2f, -2f);
            PanelKit.EnableBestFit(nameText, 22);

            // The producer (经纪人) has a portrait badge riding the strip's top-right edge.
            producerAvatar = kit.NewImage("ProducerAvatarFrame", face, null, new Color32(255, 47, 142, 255)).gameObject;
            producerAvatar.GetComponent<Image>().raycastTarget = false;
            var avatarMask = kit.NewImage("Mask", producerAvatar.transform, null, new Color32(20, 14, 26, 255));
            PanelKit.Stretch(avatarMask.rectTransform, 5f, 5f, -5f, -5f);
            avatarMask.gameObject.AddComponent<RectMask2D>();
            producerAvatarArt = kit.NewImage("Avatar", avatarMask.transform, null, Color.white);
            PanelKit.Stretch(producerAvatarArt.rectTransform);
            producerAvatarArt.preserveAspect = true; producerAvatarArt.raycastTarget = false;
            producerAvatar.transform.localEulerAngles = new Vector3(0f, 0f, 4f);
            producerAvatar.SetActive(false);

            RectTransform viewport = kit.NewRect("DialogueReadingArea", face);
            PanelKit.PlaceTop(viewport, 62, 70, 546, 122);
            var reader = viewport.gameObject.AddComponent<UnityEngine.UI.Image>();
            reader.color = new Color(0, 0, 0, .001f);
            reader.raycastTarget = true;
            viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            bodyText = kit.NewText("DialogueText", viewport, string.Empty, 26, DialogueInk,
                FontStyle.Normal, TextAnchor.UpperLeft);
            bodyText.lineSpacing = 1.22f;
            bodyText.verticalOverflow = VerticalWrapMode.Overflow;
            RectTransform content = bodyText.rectTransform;
            content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1); content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            bodyText.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit =
                UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            dialogueScroll = viewport.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            dialogueScroll.viewport = viewport; dialogueScroll.content = content;
            dialogueScroll.horizontal = false; dialogueScroll.vertical = true;
            dialogueScroll.movementType = ScrollRect.MovementType.Clamped;
            dialogueScroll.scrollSensitivity = 36f;
            // A clear "next" cue: a bobbing pink arrow with a black offset, like a P5 prompt.
            continueHint = kit.NewPlacedText(face, "▼", 34, new Color32(255, 47, 142, 255),
                630, 176, 48, 44, TextAnchor.MiddleCenter, FontStyle.Bold);
            var hintShadow = continueHint.gameObject.AddComponent<UnityEngine.UI.Shadow>();
            hintShadow.effectColor = new Color32(20, 14, 26, 255); hintShadow.effectDistance = new Vector2(2.5f, -2.5f);
            continueHint.gameObject.name = "ContinueHint";
            dialogue.Sheen.transform.SetAsLastSibling();
        }

        private static void PlaceBottom(RectTransform rect, float x, float bottom, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(x, bottom);
            rect.sizeDelta = new Vector2(width, height);
        }

        private float StageHeight
        {
            get
            {
                float height = ((RectTransform)transform).rect.height;
                return height > 100f ? height : 1536f;
            }
        }

        private float StageWidth
        {
            get
            {
                float width = ((RectTransform)transform).rect.width;
                return width > 100f ? width : 720f;
            }
        }

        private void LayoutConversation()
        {
            if (dialogueBox == null || speakerRoot == null) return;
            float height = StageHeight;
            if (illustratedReadingShade != null)
                PlaceBottom(illustratedReadingShade.rectTransform, 0f, 0f, StageWidth,
                    Mathf.Min(height * .34f, 520f));
            // The canvas can be narrower than its 720 design reference (editor targets and
            // embedded previews included). Keep the portrait/text gap, shrink the reading
            // measure, and let long text scroll instead of reducing its font or leaving screen.
            float width = Mathf.Max(1f, StageWidth - ConversationX * 2f);
            PlaceBottom(dialogueBox.GetComponent<RectTransform>(), ConversationX, 22, width, ConversationHeight);
            if (dialogueScroll != null)
                // Keep clear of the torn paper's ragged edge (≈20–40 px deep on every side).
                PanelKit.PlaceTop(dialogueScroll.viewport, 62f, 70f, Mathf.Max(1f, width - 150f), 122f);
            if (nameTag != null)
            {
                // The ribbon rides the strip's top edge, half above it.
                PanelKit.PlaceTop(nameTag.GetComponent<RectTransform>(), 10f, -38f, 300f, 84f);
                PanelKit.PlaceTop(nameText.rectTransform, 52f, 10f, 210f, 60f);
            }
            if (continueHint != null)
                PanelKit.PlaceTop(continueHint.rectTransform, width - 78f, 170f, 48f, 44f);
            if (producerAvatar != null)
                PanelKit.PlaceTop((RectTransform)producerAvatar.transform, width - 162f, -118f, 140f, 140f);
            if (titleText != null)
            {
                // Keep the utility buttons reachable on the same actual canvas width.
                Transform bar = titleText.transform.parent;
                PanelKit.PlaceTop((RectTransform)bar, 0f, 0f, StageWidth, 72f);
                float autoX = StageWidth - 328f;
                PanelKit.PlaceTop(titleText.rectTransform, 116f, 14f, Mathf.Max(1f, autoX - 132f), 48f);
                PanelKit.PlaceTop(autoControl.Root.GetComponent<RectTransform>(), autoX, 14f, 92f, 48f);
                PanelKit.PlaceTop((RectTransform)bar.Find("Skip"), StageWidth - 224f, 14f, 92f, 48f);
                PanelKit.PlaceTop((RectTransform)bar.Find("History"), StageWidth - 120f, 14f, 102f, 48f);
            }
            float bustHeight = Mathf.Clamp(height * .5f, 360f, 680f);
            // The speaker stands behind the strip (its lower body is covered, like the C2 concept).
            PlaceBottom(speakerRoot, 0f, 120f, 380f, bustHeight);
            PlaceBottom(producerMarker.rectTransform, 40f, 48f, 214f, 40f);
            float stageBottom = Mathf.Max(380f, Mathf.Min(height * .4f, 620f));
            float stageHeight = Mathf.Clamp(height - stageBottom - 120f, 220f, 700f);
            float[] lefts = { -12f, 220f, 452f };
            for (int i = 0; i < slots.Count; i++)
                PanelKit.PlaceTop((RectTransform)slots[i].Root.transform, lefts[i],
                    height - stageBottom - stageHeight, 280f, stageHeight);
            speakerRest = speakerRoot.anchoredPosition;
            dialogueRest = dialogueBox.GetComponent<RectTransform>().anchoredPosition;
        }

        private void OnRectTransformDimensionsChange() => LayoutConversation();

        private void BuildChoices()
        {
            choiceLayer = kit.NewRect("Choices", transform);
            PanelKit.Stretch(choiceLayer);
            choiceLayer.gameObject.SetActive(false);
        }

        private void BuildHistory()
        {
            ModalPolish038Frame frame = ModalPolish038Chrome.Begin(transform, "HistoryPanel",
                "已读台词", "剧情记录", HideHistory, 620f, "HistoryClose");
            historyOverlay = frame.Overlay;

            GameObject listPlate = ModalPolish038Chrome.RowPlate(frame.Body, "HistoryFrame");
            SecondaryStory038Chrome.Stretch(listPlate.GetComponent<RectTransform>(), 2f, 2f, -2f, -2f);

            RectTransform viewport = kit.NewRect("HistoryList", listPlate.transform);
            SecondaryStory038Chrome.Stretch(viewport, 10f, 10f, -10f, -10f);
            Image viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0.001f);
            viewport.gameObject.AddComponent<RectMask2D>();
            historyContent = kit.NewRect("Content", viewport);
            historyContent.anchorMin = new Vector2(0, 1);
            historyContent.anchorMax = new Vector2(1, 1);
            historyContent.pivot = new Vector2(0.5f, 1);
            historyContent.anchoredPosition = Vector2.zero;
            historyText = kit.NewText("Text", historyContent, string.Empty, 26, SirenStickerUi.Paper, FontStyle.Normal,
                TextAnchor.UpperLeft);
            historyText.lineSpacing = 1.35f;
            historyText.verticalOverflow = VerticalWrapMode.Overflow;
            PanelKit.Stretch(historyText.rectTransform, 12, 0, -12, 0);
            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = historyContent;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            historyOverlay.SetActive(false);
        }

        // ------------------------------------------------------------------ flow

        private void Begin()
        {
            StoryFrame frame = runner.Current ?? runner.Start();
            Present(frame);
        }

        private void Present(StoryFrame frame)
        {
            ApplyDirectives(frame, true);
            if (frame.IsEnd)
            {
                PresentEnd();
                return;
            }

            if (frame.IsChoice)
            {
                PresentChoice(frame.Blocking);
                return;
            }

            PresentSay(frame.Blocking);
        }

        private void ApplyDirectives(StoryFrame frame, bool playSound)
        {
            for (int index = 0; index < frame.Directives.Count; index++)
            {
                StoryLine line = frame.Directives[index];
                switch (line.Command)
                {
                    case StoryCommand.Background:
                        activeSceneCg = null;
                        SetBackground(line.Subject);
                        break;
                    case StoryCommand.Music:
                        onAudio?.Invoke(StoryCommand.Music, line.Subject);
                        break;
                    case StoryCommand.Sound:
                        if (playSound) onAudio?.Invoke(StoryCommand.Sound, line.Subject);
                        break;
                    case StoryCommand.Show:
                        ShowCharacter(line.Subject, line.Expression, line.Position);
                        break;
                    case StoryCommand.Hide:
                        HideCharacter(line.Subject);
                        break;
                }
            }
        }

        private void PresentSay(StoryLine line)
        {
            HideChoices();
            dialogueBox.SetActive(true);
            bool narrator = string.IsNullOrEmpty(line.Subject);
            nameTag.SetActive(!narrator);
            if (producerAvatar != null) producerAvatar.SetActive(line.Subject == "producer");
            if (line.Subject == "producer" && producerAvatarArt != null)
            {
                producerAvatarArt.sprite = LoadFirstBackground(model.PlayerAvatarResource,
                    "Art/ProfileAvatarUser", "Art/ProfileAvatar");
                producerMarker.text = model.PlayerName;
            }
            string speaker = narrator ? string.Empty : ResolveName(line.Subject);
            nameText.text = speaker;

            if (!narrator)
            {
                if (FindSlotByCharacter(line.Subject) == null && !string.IsNullOrEmpty(line.Position))
                    ShowCharacter(line.Subject, line.Expression, line.Position);
                else if (!string.IsNullOrEmpty(line.Expression))
                    RefreshPortrait(FindSlotByCharacter(line.Subject), line.Expression);
            }

            ApplySceneCg(line, narrator ? null : line.Subject);
            PlayLineVoice(line);
            FocusSpeaker(narrator ? null : line.Subject);
            PresentSpeaker(narrator ? null : line.Subject, line.Expression);
            // 2026-10-28: the manager's signed name stands in for {player}.
            string spoken = model.Personalize(line.Text);
            history.Add(narrator ? spoken : $"{speaker}：{spoken}");
            StartTyping(spoken);
        }

        // 2026-10-20 (owner): each speaker gets a painted in-scene CG (GPT, from her portrait +
        // the stage scene) instead of a bust pasted over the background. Lines by the producer
        // or narrator keep the last CG. Busts stay hidden while a CG is on screen.
        private string activeSceneCg;
        private static HashSet<string> sceneCgIndex;
        private static bool HasSceneCg(string key)
        {
            if (sceneCgIndex == null)
            {
                sceneCgIndex = new HashSet<string>(StringComparer.Ordinal);
                TextAsset index = Resources.Load<TextAsset>("Data/StoryCgIndex");
                if (index != null)
                    foreach (string row in index.text.Split('\n'))
                        if (!string.IsNullOrWhiteSpace(row)) sceneCgIndex.Add(row.Trim());
            }
            return sceneCgIndex.Contains(key);
        }

        private void ApplySceneCg(StoryLine line, string subject)
        {
            string scriptId = runner?.Script?.Id;
            if (!string.IsNullOrEmpty(scriptId))
            {
                int index = runner.Script.Lines != null ? runner.Script.Lines.IndexOf(line) : -1;
                // A continuous painted scene survives speaker changes. Legacy speaker composites
                // can have a hard rectangle across their bodies; never fall back to those.
                string key = StorySceneDirector1028.Resolve(runner.Script, index, activeSceneCg, HasSceneCg);
                if (key != null && HasSceneCg(key) && key != activeSceneCg)
                {
                    activeSceneCg = key;
                    SetBackground(key);
                    SetScenePresentation(true);
                }
            }
            if (activeSceneCg == null) return;
            speakerRoot.gameObject.SetActive(false);
            foreach (SlotView slot in slots) if (slot.Root != null) slot.Root.SetActive(false);
        }

        private void PresentChoice(StoryLine line)
        {
            StopTyping(true);
            ApplySceneCg(line, null);
            nameTag.SetActive(false);
            if (producerAvatar != null) producerAvatar.SetActive(false);
            bodyText.text = string.IsNullOrEmpty(line.Text) ? "请做出选择" : model.Personalize(line.Text);
            continueHint.gameObject.SetActive(false);
            FocusSpeaker(null);
            PresentSpeaker(null, null);
            history.Add("【选择】" + (string.IsNullOrEmpty(line.Text) ? string.Empty : model.Personalize(line.Text)));

            HideChoices();
            choiceLayer.gameObject.SetActive(true);
            int count = line.Choices.Count;
            const float height = 76f;
            const float gap = 20f;
            float total = count * height + (count - 1) * gap;
            float freeBottom = StageHeight - ConversationHeight - 70f;
            float top = Mathf.Max(96f, (100f + freeBottom - total) * .5f);
            for (int index = 0; index < count; index++)
            {
                int captured = index;
                GameObject button = SecondaryStory038Chrome.GlassAction(choiceLayer,
                    "Choice-" + index, model.Personalize(line.Choices[index].Text), 26,
                    SecondaryStory038Chrome.SocketChoice, SecondaryStory038Chrome.EdgePink,
                    () => Choose(captured), mirror: index % 2 == 1).Root;
                PanelKit.PlaceTop(button.GetComponent<RectTransform>(), 100, top + index * (height + gap), 520, height);
                choiceButtons.Add(button);
            }
        }

        private void PresentEnd()
        {
            StopTyping(true);
            HideChoices();
            ended = true;
            CancelAuto();
            history.Add("—— 剧情结束 ——");
            // 2026-10-24 (owner): no system 「本段剧情结束 · 点击对话框返回」 card. The scene's last spoken line
            // hands straight over to what follows. Closing first lets the map refresh while it is still on
            // screen; a card left under the battle used to refresh the hidden map and scramble its labels.
            // Only the finished callback runs (not back as well), so the story is completed exactly once.
            if (closing) return;
            closing = true;
            gameObject.SetActive(false);
            Destroy(gameObject);
            ReportFinished();
        }

        private void ReportFinished()
        {
            if (finishedReported) return;
            finishedReported = true;
            try
            {
                onFinished?.Invoke();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private void Choose(int index)
        {
            if (closing || runner.Current == null || !runner.Current.IsChoice) return;
            List<StoryChoice> options = runner.Current.Blocking.Choices;
            if (index < 0 || index >= options.Count) return;
            history.Add("　→ " + model.Personalize(options[index].Text));
            StoryFrame frame = runner.Choose(index);
            HideChoices();
            Present(frame);
        }

        private void DialogueClicked()
        {
            if (closing) return;
            if (ended)
            {
                Close();
                return;
            }

            if (runner.Current == null || runner.Current.IsChoice) return;
            if (typing)
            {
                StopTyping(true);
                return;
            }

            AdvanceLine();
        }

        private void AdvanceLine()
        {
            if (closing || runner.Finished || runner.Current == null || runner.Current.IsChoice ||
                historyOverlay != null && historyOverlay.activeSelf) return;
            StoryFrame frame = runner.Advance();
            Present(frame);
        }

        private void SkipToDecision()
        {
            if (closing || ended) return;
            StoryFrame frame = runner.Current;
            if (frame == null) return;
            int guard = 0;
            while (!frame.IsChoice && !frame.IsEnd && guard++ < 512)
            {
                frame = runner.Advance();
                if (frame.IsChoice || frame.IsEnd) break;
                // Skipped lines still update the stage (bg/show/hide) and land in 记录; sfx are muted.
                ApplyDirectives(frame, false);
                ApplySceneCg(frame.Blocking, frame.Blocking.Subject);
                string speaker = string.IsNullOrEmpty(frame.Blocking.Subject) ? string.Empty : ResolveName(frame.Blocking.Subject);
                history.Add(string.IsNullOrEmpty(speaker) ? frame.Blocking.Text : $"{speaker}：{frame.Blocking.Text}");
            }

            // Skipping to the end closes the scene now (no end card), so only a choice needs a note.
            if (frame.IsChoice) Notify("已跳到选择支");
            Present(frame);
        }

        private void ToggleAuto()
        {
            autoMode = !autoMode;
            SecondaryStory038Chrome.SetActionFace(autoControl,
                autoMode ? SecondaryStory038Chrome.EdgePink : SecondaryStory038Chrome.EdgeViolet,
                autoMode ? SecondaryStory038Chrome.FaceToggleOn : SecondaryStory038Chrome.FaceDark,
                autoMode ? "自动：开" : "自动");
            if (autoMode && !typing) ScheduleAuto();
            else if (!autoMode) CancelAuto();
            Notify(autoMode ? "已开启自动播放" : "已关闭自动播放");
        }

        private void ScheduleAuto()
        {
            CancelAuto();
            if (!autoMode || ended || runner.Current == null || runner.Current.IsChoice ||
                historyOverlay != null && historyOverlay.activeSelf) return;
            autoRoutine = StartCoroutine(AutoAdvance(fullLine.Length));
        }

        private void CancelAuto()
        {
            if (autoRoutine == null) return;
            StopCoroutine(autoRoutine);
            autoRoutine = null;
        }

        private IEnumerator AutoAdvance(int length)
        {
            yield return new WaitForSecondsRealtime(AutoBaseDelay + length * AutoPerCharDelay);
            while (autoMode && !ended && voiceAudio != null &&
                (voiceAudio.IsVoicePlayingFor(StoryVoiceOwner) || voiceAudio.IsVoiceLoading(storyVoiceClip1102)))
                yield return null;
            autoRoutine = null;
            if (autoMode && !typing && !ended && !historyOverlay.activeSelf) AdvanceLine();
        }

        // ------------------------------------------------------------------ typewriter

        private void StartTyping(string text)
        {
            if (dialogueScroll != null) dialogueScroll.verticalNormalizedPosition = 1f;
            CancelAuto();
            if (typeRoutine != null) StopCoroutine(typeRoutine);
            fullLine = text ?? string.Empty;
            typing = true;
            continueHint.gameObject.SetActive(false);
            bodyText.text = string.Empty;
            typeRoutine = StartCoroutine(TypeLine());
        }

        private IEnumerator TypeLine()
        {
            int shown = 0;
            float elapsed = 0f;
            while (shown < fullLine.Length)
            {
                elapsed += Time.unscaledDeltaTime;
                int target = Mathf.Min(fullLine.Length, Mathf.FloorToInt(elapsed / CharSeconds));
                if (target != shown)
                {
                    shown = target;
                    bodyText.text = fullLine.Substring(0, shown);
                }

                yield return null;
            }

            typeRoutine = null;
            FinishTyping();
        }

        private void StopTyping(bool showFull)
        {
            if (typeRoutine != null)
            {
                StopCoroutine(typeRoutine);
                typeRoutine = null;
            }

            if (typing && showFull) FinishTyping();
            else typing = false;
        }

        private void FinishTyping()
        {
            typing = false;
            bodyText.text = fullLine;
            continueHint.gameObject.SetActive(!ended && runner.Current != null && !runner.Current.IsChoice);
            if (autoMode) ScheduleAuto();
        }

        private void Update()
        {
            if (continueHint == null || !continueHint.gameObject.activeSelf) return;
            Color color = continueHint.color;
            color.a = 0.75f + Mathf.Sin(Time.unscaledTime * 4f) * 0.25f;
            continueHint.color = color;
            Vector2 at = continueHint.rectTransform.anchoredPosition;
            continueHint.rectTransform.anchoredPosition = new Vector2(at.x, -170f + Mathf.Abs(Mathf.Sin(Time.unscaledTime * 5f)) * -6f);
        }

        // ------------------------------------------------------------------ stage

        private void SetBackground(string assetId)
        {
            int request = ++backgroundRequest;
            string key = NormalizeBackgroundKey(assetId);
            Sprite original = StoryArt1019.Load(key);
            Sprite sprite = original != null ? original : ResolveBackgroundSprite(key);
            bool originalShown = original != null;
            bool containsActors = key != null && IllustratedSceneFallbacks.TryGetValue(key, out _);
            ApplyBackgroundSprite(sprite);
            SetScenePresentation(containsActors);
            if (containsActors && IllustratedSceneFallbacks.TryGetValue(key, out string emptyScene) &&
                StoryArt1019.IsManaged(emptyScene) && StoryArt1019.Load(emptyScene) == null)
                StoryArt1019.Request(emptyScene, loaded =>
                {
                    if (this == null || closing || !gameObject.activeInHierarchy || request != backgroundRequest ||
                        originalShown || loaded == null) return;
                    ApplyBackgroundSprite(loaded);
                });
            if (StoryArt1019.IsManaged(key) && original == null)
                StoryArt1019.Request(key, loaded =>
                {
                    if (this == null || closing || !gameObject.activeInHierarchy || request != backgroundRequest || loaded == null) return;
                    originalShown = true;
                    ApplyBackgroundSprite(loaded);
                });

            // Asset identifiers are implementation details. A missing optional scene-specific
            // image must never replace the stage art with a debug warning in the player's UI.
            backgroundId.text = string.Empty;
        }

        private static string NormalizeBackgroundKey(string assetId)
        {
            if (string.IsNullOrWhiteSpace(assetId)) return null;
            string key = assetId.Trim().Replace('\\', '/');
            if (key.StartsWith("Resources/", StringComparison.Ordinal)) key = key.Substring("Resources/".Length);
            return BackgroundResourceAliases.TryGetValue(key, out string mapped) ? mapped : key;
        }

        private void PrewarmStoryArt()
        {
            foreach (StoryLine line in runner.Script.Lines)
            {
                if (line.Command == StoryCommand.Background)
                {
                    string key = NormalizeBackgroundKey(line.Subject);
                    StoryArt1019.Prewarm(key);
                    if (key != null && IllustratedSceneFallbacks.TryGetValue(key, out string emptyScene))
                        StoryArt1019.Prewarm(emptyScene);
                }
                else if ((line.Command == StoryCommand.Say || line.Command == StoryCommand.Show) &&
                    !string.IsNullOrEmpty(line.Subject) && line.Subject != "producer")
                {
                    string key = SpeakerArtKey(line.Subject, string.IsNullOrEmpty(line.Expression) ? "neutral" : line.Expression);
                    if (key != null) StoryArt1019.Prewarm(key);
                }
            }
        }

        private static Sprite ResolveBackgroundSprite(string assetId)
        {
            if (!string.IsNullOrWhiteSpace(assetId))
            {
                string normalized = assetId.Trim().Replace('\\', '/');
                if (normalized.StartsWith("Resources/", StringComparison.Ordinal))
                    normalized = normalized.Substring("Resources/".Length);

                if (BackgroundResourceAliases.TryGetValue(normalized, out string mapped))
                {
                    Sprite aliased = StoryArt1019.Load(mapped);
                    if (aliased != null) return aliased;
                    normalized = mapped;
                }

                Sprite direct = StoryArt1019.Load(normalized);
                if (direct != null) return direct;

                // A missing/slow complete painting falls back to its matching empty setting,
                // never a previous painting with the wrong actors still baked into it.
                if (IllustratedSceneFallbacks.TryGetValue(normalized, out string emptyScene))
                    return ResolveBackgroundSprite(emptyScene);

                // New per-stage art arrives independently; retain the correct chapter setting
                // while its imported Sprite is unavailable, rather than showing member-gallery art.
                if (normalized.StartsWith(SceneBackgroundResource + "stage-", StringComparison.Ordinal))
                {
                    Sprite chapterScene = LoadFirstBackground(normalized.Contains("/stage-2-")
                        ? "Art/Story/harbor-night" : "Art/Story/rehearsal-night");
                    if (chapterScene != null) return chapterScene;
                }

                if (!normalized.StartsWith("Art/", StringComparison.Ordinal))
                {
                    Sprite legacy = Resources.Load<Sprite>("Art/Story/" + normalized);
                    if (legacy != null) return legacy;
                }
            }

            return LoadFirstBackground(DefaultBackgroundResource, LobbyBackgroundResource);
        }

        private static Sprite LoadFirstBackground(params string[] resourcePaths)
        {
            for (int index = 0; index < resourcePaths.Length; index++)
            {
                Sprite sprite = StoryArt1019.Load(resourcePaths[index]);
                if (sprite != null) return sprite;
            }

            return null;
        }

        private void ApplyBackgroundSprite(Sprite sprite)
        {
            background.sprite = sprite;
            ChoSiren.UI.PageAmbient.Set(gameObject, sprite, .45f);
            background.color = sprite != null ? (illustratedScene ? Color.white : PanelKit.White)
                : new Color(1f, 1f, 1f, 0f);
            if (sprite != null && backgroundCover != null)
                backgroundCover.aspectRatio = sprite.rect.width / Mathf.Max(1f, sprite.rect.height);
        }

        private void SetScenePresentation(bool containsActors)
        {
            illustratedScene = containsActors;
            if (background.sprite != null) background.color = containsActors ? Color.white : PanelKit.White;
            // Keep the original scene colours above the reading area. The classic tinted
            // backdrop remains unchanged for environment-only scenes and legacy scripts.
            readabilityVeil.enabled = !containsActors;
            vignette.enabled = !containsActors;
            illustratedReadingShade.gameObject.SetActive(containsActors);
            FocusSpeaker(speakerCharacterId);
        }

        private void ShowCharacter(string characterId, string expression, string position)
        {
            if (string.IsNullOrEmpty(characterId) || characterId == "producer") return;
            SlotView existing = FindSlotByCharacter(characterId);
            string wanted = NormalizePosition(position, existing != null ? existing.Position : "center");
            if (existing != null && existing.Position != wanted) ClearSlot(existing);

            SlotView slot = FindSlotByPosition(wanted);
            if (slot == null) return;
            slot.CharacterId = characterId;
            // The logical actor survives while its complete scene painting is displayed,
            // downloading or unavailable. The separate lower speaker portrait still works.
            slot.Root.SetActive(!illustratedScene);
            slot.Group.alpha = 1f;
            RefreshPortrait(slot, expression);
        }

        private void RefreshPortrait(SlotView slot, string expression)
        {
            if (slot == null || string.IsNullOrEmpty(slot.CharacterId)) return;
            if (!string.IsNullOrEmpty(expression)) slot.Expression = expression;
            Sprite sprite = null;
            if (!string.IsNullOrEmpty(slot.Expression))
                sprite = Resources.Load<Sprite>("Art/Story/Portraits/" + slot.CharacterId + "-" + slot.Expression);
            if (sprite == null) sprite = Resources.Load<Sprite>("Art/Story/Portraits/" + slot.CharacterId);
            if (sprite == null) sprite = PanelKit.MemberSpriteOrNull(slot.CharacterId, false);

            slot.Portrait.sprite = sprite;
            slot.Portrait.enabled = sprite != null;
            slot.Silhouette.SetActive(sprite == null);
            slot.SilhouetteName.text = ResolveName(slot.CharacterId);
        }

        private void HideCharacter(string characterId)
        {
            SlotView slot = FindSlotByCharacter(characterId);
            if (slot != null) ClearSlot(slot);
        }

        private static void ClearSlot(SlotView slot)
        {
            slot.CharacterId = null;
            slot.Expression = null;
            slot.Portrait.sprite = null;
            slot.Root.SetActive(false);
        }

        private void FocusSpeaker(string characterId)
        {
            for (int index = 0; index < slots.Count; index++)
            {
                SlotView slot = slots[index];
                if (string.IsNullOrEmpty(slot.CharacterId)) continue;
                slot.Root.SetActive(!illustratedScene);
                if (illustratedScene) continue;
                bool speaking = string.IsNullOrEmpty(characterId) || slot.CharacterId == characterId;
                slot.Group.alpha = speaking ? .72f : .46f;
                slot.Root.transform.localScale = Vector3.one;
                if (speaking && !string.IsNullOrEmpty(characterId)) slot.Root.transform.SetAsLastSibling();
            }

            // Stage actors never cover the separate foreground speaker or the reading UI.
            speakerRoot.SetAsLastSibling();
            dialogueBox.transform.SetAsLastSibling();
            choiceLayer.SetAsLastSibling();
            historyOverlay.transform.SetAsLastSibling();
        }

        private void PresentSpeaker(string characterId, string expression)
        {
            if (activeSceneCg != null) { speakerRoot.gameObject.SetActive(false); return; }
            int request = ++speakerRequest;
            bool changed = speakerCharacterId != characterId;
            speakerCharacterId = characterId;
            if (speakerEntrance != null) StopCoroutine(speakerEntrance);
            speakerEntrance = null;
            speakerRoot.anchoredPosition = speakerRest;
            speakerRoot.GetComponent<CanvasGroup>().alpha = 1f;
            producerMarker.gameObject.SetActive(characterId == "producer");
            bool authoredEdge = false;
            Sprite sprite = null;
            if (!string.IsNullOrEmpty(characterId) && characterId != "producer")
            {
                SlotView actor = FindSlotByCharacter(characterId);
                string pose = !string.IsNullOrEmpty(expression) ? expression
                    : !string.IsNullOrEmpty(actor?.Expression) ? actor.Expression : "neutral";
                sprite = ResolveSpeakerPortrait(characterId, pose, out authoredEdge);
            }
            speakerPortrait.sprite = sprite;
            speakerPortrait.enabled = sprite != null;
            speakerOutline.enabled = sprite != null && !authoredEdge;
            speakerRoot.gameObject.SetActive(sprite != null || characterId == "producer");
            if (changed && sprite != null && !BattlePresentationOptions.ReduceMotion)
                speakerEntrance = StartCoroutine(EnterSpeaker());
            if (!string.IsNullOrEmpty(characterId) && characterId != "producer")
            {
                SlotView actor = FindSlotByCharacter(characterId);
                string pose = !string.IsNullOrEmpty(expression) ? expression
                    : !string.IsNullOrEmpty(actor?.Expression) ? actor.Expression : "neutral";
                string key = SpeakerArtKey(characterId, pose);
                if (key != null && StoryArt1019.Load(key) == null)
                    StoryArt1019.Request(key, loaded =>
                    {
                        if (this == null || closing || !gameObject.activeInHierarchy || request != speakerRequest ||
                            speakerCharacterId != characterId || loaded == null) return;
                        speakerPortrait.sprite = loaded; speakerPortrait.enabled = true;
                        speakerOutline.enabled = false; speakerRoot.gameObject.SetActive(true);
                    });
            }
        }

        private static string SpeakerArtKey(string characterId, string expression) => StoryArt1019.FirstAvailable(
            SpeakerPortraitResource + characterId + "-" + expression,
            SpeakerPortraitResource + characterId + "-neutral", SpeakerPortraitResource + characterId);

        private Sprite ResolveSpeakerPortrait(string characterId, string expression, out bool authoredEdge)
        {
            // Identity is an exact member ID. In particular, the producer must never borrow an idol.
            authoredEdge = false;
            if (string.IsNullOrEmpty(characterId) || characterId == "producer") return null;
            Sprite sprite = LoadFirstBackground(SpeakerPortraitResource + characterId + "-" + expression,
                SpeakerPortraitResource + characterId + "-neutral", SpeakerPortraitResource + characterId);
            if (sprite != null) { authoredEdge = true; return sprite; }
            sprite = LoadFirstBackground("Art/Story/Portraits/" + characterId + "-" + expression,
                "Art/Story/Portraits/" + characterId);
            if (sprite != null) return sprite;
            Sprite original = PanelKit.MemberSpriteOrNull(characterId, false);
            if (original == null) return null;
            if (fallbackBusts.TryGetValue(original, out Sprite cached)) return cached;
            Rect source = original.rect;
            // Preserve the entire native top edge (hair, ears and horns). Only remove the lower body.
            // 2026-11-01 avatar-face: the half-body window follows the annotated face, not a fixed 45% cut.
            Rect uv = ChoSiren.UI.PortraitFace1101.UvFor(characterId, original, source.width / (source.height * .55f),
                ChoSiren.UI.PortraitFace1101.Framing.Half);
            Rect crop = new Rect(source.x + source.width * uv.x, source.y + source.height * uv.y,
                source.width * uv.width, source.height * uv.height);
            Sprite bust = Sprite.Create(original.texture, crop, new Vector2(.5f, 0f),
                original.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            bust.name = characterId + "-StoryHalfBody";
            fallbackBusts.Add(original, bust);
            return bust;
        }

        private IEnumerator EnterSpeaker()
        {
            CanvasGroup group = speakerRoot.GetComponent<CanvasGroup>();
            float elapsed = 0f;
            while (elapsed < .25f && !closing)
            {
                float t = Mathf.Clamp01(elapsed / .25f);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                speakerRoot.anchoredPosition = speakerRest + Vector2.left * (24f * (1f - eased));
                group.alpha = eased;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            speakerRoot.anchoredPosition = speakerRest;
            group.alpha = 1f;
            speakerEntrance = null;
        }

        private IEnumerator EnterConversation()
        {
            if (BattlePresentationOptions.ReduceMotion) yield break;
            RectTransform rect = dialogueBox.GetComponent<RectTransform>();
            CanvasGroup group = dialogueBox.AddComponent<CanvasGroup>();
            float elapsed = 0f;
            while (elapsed < .25f && !closing)
            {
                float t = Mathf.Clamp01(elapsed / .25f);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                rect.anchoredPosition = dialogueRest + Vector2.down * (18f * (1f - eased));
                group.alpha = .75f + eased * .25f;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            rect.anchoredPosition = dialogueRest;
            group.alpha = 1f;
        }

        private SlotView FindSlotByCharacter(string characterId)
        {
            if (string.IsNullOrEmpty(characterId)) return null;
            for (int index = 0; index < slots.Count; index++)
                if (slots[index].CharacterId == characterId) return slots[index];
            return null;
        }

        private SlotView FindSlotByPosition(string position)
        {
            for (int index = 0; index < slots.Count; index++)
                if (slots[index].Position == position) return slots[index];
            return null;
        }

        private static string NormalizePosition(string position, string fallback)
        {
            if (string.IsNullOrEmpty(position)) return fallback;
            string lowered = position.Trim().ToLowerInvariant();
            for (int index = 0; index < SlotOrder.Length; index++)
                if (SlotOrder[index] == lowered) return lowered;
            return fallback;
        }

        private string ResolveName(string characterId)
        {
            if (characterId == "producer") return model.PlayerName;
            string custom = nameResolver?.Invoke(characterId);
            return string.IsNullOrEmpty(custom) ? PanelKit.MemberNameOrId(characterId) : custom;
        }

        private void HideChoices()
        {
            for (int index = 0; index < choiceButtons.Count; index++)
            {
                choiceButtons[index].SetActive(false);
                Destroy(choiceButtons[index]);
            }

            choiceButtons.Clear();
            choiceLayer.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ history

        private void ShowHistory()
        {
            // Reading history pauses auto advance; closing it resumes with a fresh reading delay.
            StopTyping(true);
            CancelAuto();
            var builder = new StringBuilder();
            if (history.Count == 0) builder.Append("尚无已读台词");
            for (int index = 0; index < history.Count; index++)
            {
                if (index > 0) builder.Append('\n');
                builder.Append(history[index]);
            }

            historyText.text = builder.ToString();
            float preferred = historyText.preferredHeight + 24f;
            historyContent.sizeDelta = new Vector2(0, Mathf.Max(preferred, 100f));
            historyOverlay.SetActive(true);
            historyOverlay.transform.SetAsLastSibling();
        }

        private void HideHistory()
        {
            historyOverlay.SetActive(false);
            if (autoMode && !typing) ScheduleAuto();
        }

        // ------------------------------------------------------------------ lifecycle

        private void Notify(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            onMessage?.Invoke(message);
        }

        private void Leave()
        {
            if (ended)
            {
                Close();
                return;
            }

            ClosePanel("已退出剧情，进度不会保存");
        }

        public void Close()
        {
            ClosePanel(null);
        }

        private void ClosePanel(string message)
        {
            if (closing) return;
            closing = true;
            Action callback = onBack;
            Action<string> notify = onMessage;
            gameObject.SetActive(false);
            Destroy(gameObject);
            callback?.Invoke();
            if (!string.IsNullOrEmpty(message)) notify?.Invoke(message);
        }

        // Every spoken line has an exact Japanese recording, including producer, narration and outros.
        private const string StoryVoiceOwner = "story-panel";
        private GameAudio voiceAudio;
        private string storyVoiceClip1102;
        private void PlayLineVoice(StoryLine line)
        {
            if (voiceAudio == null) voiceAudio = FindFirstObjectByType<GameAudio>();
            if (voiceAudio == null) return;
            voiceAudio.StopVoice(StoryVoiceOwner);
            string scriptId = runner?.Script?.Id;
            int index = runner?.Script?.Lines != null ? runner.Script.Lines.IndexOf(line) : -1;
            storyVoiceClip1102 = DialogueVoice1102.StoryClip(scriptId, index, line.Subject, line.Text);
            if (string.IsNullOrEmpty(storyVoiceClip1102)) return;
            voiceAudio.PlayVoice(storyVoiceClip1102, StoryVoiceOwner);
            for (int next = index + 1; next < runner.Script.Lines.Count; next++)
            {
                StoryLine upcoming = runner.Script.Lines[next];
                if (upcoming.Command != StoryCommand.Say) continue;
                string clip = DialogueVoice1102.StoryClip(scriptId, next, upcoming.Subject, upcoming.Text);
                if (!string.IsNullOrEmpty(clip)) voiceAudio.PrefetchVoice(clip);
                break;
            }
        }

        private void OnDestroy()
        {
            if (voiceAudio != null) voiceAudio.StopVoice(StoryVoiceOwner);
            closing = true;
            backgroundRequest++; speakerRequest++;
            StopAllCoroutines();
            foreach (Sprite sprite in fallbackBusts.Values)
                if (sprite != null)
                {
                    if (Application.isPlaying) Destroy(sprite);
                    else DestroyImmediate(sprite);
                }
            fallbackBusts.Clear();
            kit?.Dispose();
        }
    }
}
