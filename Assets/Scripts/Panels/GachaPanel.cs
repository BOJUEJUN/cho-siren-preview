using System;
using System.Collections;
using System.Collections.Generic;
using ChoSiren.Systems.Gacha;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.Panels
{
    /// <summary>
    /// Everything the gacha screen needs from the game. GameModel implements this once the
    /// currency/pity persistence lands; tests can hand in a fake.
    /// </summary>
    public interface IGachaService
    {
        IReadOnlyList<GachaBannerDefinition> Banners { get; }

        /// <summary>Persisted pity counters for one banner. May return null before the first pull.</summary>
        GachaBannerState BannerState(string bannerId);

        int Balance(string currencyId);

        bool TryPull(string bannerId, int count, ulong seed, out List<GachaPullResult> results, out string message);

        /// <summary>Player-facing Chinese name for a pulled item id. Return null to fall back to the roster.</summary>
        string ItemDisplayName(string itemId);
    }

    /// <summary>
    /// 星光签约 (gacha) screen: banner tabs, published rates, pity counter, ×1 / ×10 pulls and a
    /// staggered reveal grid. Opened with GachaPanel.Open(safeRoot, model, service, onBack, toast).
    /// </summary>
    public sealed class GachaPanel : MonoBehaviour
    {
        private const float RevealInterval = 0.12f;
        private const float RevealDuration = 0.22f;

        private sealed class ResultCell
        {
            public GameObject Root;
            public RectTransform Rect;
            public Image Background;
            public Image Portrait;
            public Outline Outline;
            public Text Rarity;
            public Text Name;
            public GameObject NewBadge;
            public Text Footer;
            public bool Ssr;
        }

        private static readonly Color SsrColor = new Color32(120, 62, 26, 250);
        private static readonly Color SrColor = new Color32(92, 46, 150, 250);
        private static readonly Color RColor = new Color32(43, 48, 92, 250);
        private const string AiArtRoot = "Art/GachaAI/";
        private const string AiUiRoot = "Art/GachaAI/UI/";
        private static readonly Color DetailGlass = new Color32(17, 27, 75, 124);
        private static readonly Color ActionGlass = new Color32(23, 32, 88, 142);
        private static readonly Color TenPullGlass = new Color32(36, 22, 82, 146);
        private static readonly Color DisabledActionGlass = new Color32(42, 44, 82, 150);

        private readonly List<GameObject> bannerTabs = new List<GameObject>();
        private readonly List<Image> bannerTabEmblems = new List<Image>();
        private readonly List<ResultCell> resultCells = new List<ResultCell>();

        private PanelKit kit;
        private GameModel model;
        private IGachaService service;
        private Action onBack;
        private Action<string> onMessage;
        private bool embeddedMode;
        private bool closing;
        private int bannerIndex;
        private int lastPullCount = 10;
        private bool pulling;
        private bool revealing;
        private Coroutine revealRoutine;

        private Text bannerTitle;
        private Text bannerKind;
        private Text featuredText;
        private Image featuredPortrait;
        private Text featuredFallback;
        private Text rateText;
        private Text pityText;
        private Image pityFill;
        private GameObject guaranteeChip;
        private Text totalsText;
        private Text diamondText;
        private Image ticketIcon;
        private Text ticketNameText;
        private Text ticketText;
        private Text goldText;
        private GameObject pullOneButton;
        private GameObject pullTenButton;
        private Text pullOneLabel;
        private Text pullTenLabel;
        private Text pullTenCostLabel;
        private Image pullTenFrame;
        private Text hintText;
        private GameObject resultOverlay;
        private Text resultTitle;
        private Text resultSummary;
        private GameObject pullAgainButton;
        private Button resultSkipButton;

        public static GachaPanel Open(Transform host, GameModel gameModel, IGachaService gachaService,
            Action back = null, Action<string> message = null)
        {
            return Create(host, gameModel, gachaService, false, back, message);
        }

        /// <summary>
        /// Opens inside the main app's contentRoot. The global player HUD and five-item navigation
        /// remain visible and interactive, so recruitment behaves like a primary tab rather than
        /// a modal screen with a duplicate header.
        /// </summary>
        public static GachaPanel OpenEmbedded(Transform contentHost, GameModel gameModel,
            IGachaService gachaService, Action<string> message = null)
        {
            return Create(contentHost, gameModel, gachaService, true, null, message);
        }

        private static GachaPanel Create(Transform host, GameModel gameModel, IGachaService gachaService,
            bool embedded, Action back, Action<string> message)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            if (gameModel == null) throw new ArgumentNullException(nameof(gameModel));
            if (gachaService == null) throw new ArgumentNullException(nameof(gachaService));

            GachaPanel existing = host.GetComponentInChildren<GachaPanel>(true);
            if (existing != null) Destroy(existing.gameObject);

            GameObject panelObject = PanelKit.CreateOverlayRoot("GachaPanel", host);
            GachaPanel panel = panelObject.AddComponent<GachaPanel>();
            panel.model = gameModel;
            panel.service = gachaService;
            panel.onBack = back;
            panel.onMessage = message;
            panel.embeddedMode = embedded;
            panel.Build();
            return panel;
        }

        public int BannerIndex => bannerIndex;
        public bool IsRevealing => revealing;

        // ------------------------------------------------------------------ build

        private void Build()
        {
            kit = new PanelKit("Gacha");
            model.Changed += HandleModelChanged;
            kit.BuildBackdrop(transform);
            BuildStageBackdrop();

            if (!embeddedMode) BuildLightHeader();
            BuildBannerCard();
            // The portrait is the stage. Controls are created afterwards so they always render
            // above the character instead of being washed out by the hero image.
            BuildBannerTabs();
            BuildRates();
            BuildPity();
            BuildBalances();
            BuildPullButtons();
            BuildResult();
            Refresh();
        }

        /// <summary>
        /// The recruitment center owns a dedicated portrait stage. The generated background
        /// deliberately reserves quiet side lanes for live HUD while keeping the summon gate
        /// behind the featured character.
        /// </summary>
        private void BuildStageBackdrop()
        {
            Sprite stageSprite = embeddedMode
                ? LoadResourceSprite(AiArtRoot + "gacha-calm-stage-bg-ai-v2-20260903")
                : null;
            stageSprite ??= LoadResourceSprite(AiArtRoot + "gacha-stage-bg-ai-v1-20260903");
            if (stageSprite == null) return;

            Image stage = kit.NewImage("ImmersiveStage", transform, stageSprite,
                Color.white);
            PanelKit.Stretch(stage.rectTransform);
            stage.preserveAspect = true;
            stage.raycastTarget = false;
            AspectRatioFitter fitter = stage.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = stageSprite.rect.width / Mathf.Max(1f, stageSprite.rect.height);
            stage.transform.SetSiblingIndex(1);

            Image veil = kit.NewImage("StageVeil", transform, null, new Color32(4, 5, 28, 42));
            PanelKit.Stretch(veil.rectTransform);
            veil.raycastTarget = false;
            veil.transform.SetSiblingIndex(2);
        }

        private Sprite LoadResourceSprite(string resourcePath)
        {
            Sprite sprite = Resources.Load<Sprite>(resourcePath);
            if (sprite != null) return sprite;

            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null) return null;
            return kit.NewSprite(texture, new Rect(0f, 0f, texture.width, texture.height),
                Vector2.one * 0.5f, Vector4.zero);
        }

        /// <summary>
        /// Places an authored AI UI surface behind live labels and controls.  The fallback panel
        /// remains in place when an optional art asset has not imported yet, keeping the screen
        /// functional while avoiding a second decorative layer once the asset is available.
        /// </summary>
        private Image AddAiUiSkin(GameObject owner, string assetName, Color tint)
        {
            if (owner == null || string.IsNullOrEmpty(assetName)) return null;
            Sprite sprite = LoadResourceSprite(AiUiRoot + assetName);
            if (sprite == null) return null;

            Image skin = kit.NewImage("AI-" + assetName, owner.transform, sprite, tint);
            PanelKit.Stretch(skin.rectTransform);
            skin.preserveAspect = false;
            skin.raycastTarget = false;
            skin.transform.SetAsFirstSibling();
            return skin;
        }

        private void BuildLightHeader()
        {
            Image shade = kit.NewImage("GachaTopShade", transform,
                kit.CreateGradientSprite("TopShade", new Color32(4, 7, 31, 238),
                    new Color32(4, 7, 31, 150), new Color32(4, 7, 31, 0)), Color.white);
            PanelKit.PlaceTop(shade.rectTransform, 0, 0, 720, 112);
            GameObject back = kit.NewButton("Back", transform, "返回", 15,
                new Color32(20, 19, 66, 82), PanelKit.White, Close, 16);
            PanelKit.PlaceTop(back.GetComponent<RectTransform>(), 18, 18, 78, 48);
            kit.AddOutline(back, new Color32(156, 111, 255, 72), 1);
            kit.NewPlacedText(transform, "招募中心", 25, PanelKit.White, 112, 16, 210, 46,
                TextAnchor.MiddleLeft, FontStyle.Bold);
        }

        private void BuildBannerTabs()
        {
            IReadOnlyList<GachaBannerDefinition> banners = service.Banners;
            int count = banners == null ? 0 : banners.Count;
            if (count == 0)
            {
                kit.NewPlacedText(transform, "暂无开放中的卡池", 16, PanelKit.Muted, 20,
                    embeddedMode ? 48 : 128, 680, 44,
                    TextAnchor.MiddleCenter, FontStyle.Bold);
                return;
            }

            float tabsTop = embeddedMode ? 68f : 208f;
            float tabStride = embeddedMode ? 122f : 142f;
            float tabHeight = embeddedMode ? 110f : 126f;
            float emblemSize = embeddedMode ? 86f : 100f;

            for (int index = 0; index < count; index++)
            {
                int captured = index;
                GachaBannerDefinition banner = banners[index];
                GameObject tab = kit.NewButton("BannerTab-" + banner.Id, transform, ShortBannerLabel(banner), 13,
                    new Color32(7, 8, 37, 12), PanelKit.White, () => SelectBanner(captured), 18);
                PanelKit.PlaceTop(tab.GetComponent<RectTransform>(), 14, tabsTop + index * tabStride, 140, tabHeight);

                Image activeMark = kit.NewImage("ActiveMark", tab.transform, kit.RadialSprite(),
                    new Color32(255, 69, 205, 112));
                PanelKit.PlaceTop(activeMark.rectTransform, 4, -2, 132, tabHeight - 4f);
                activeMark.transform.SetAsFirstSibling();
                activeMark.gameObject.SetActive(index == bannerIndex);

                Sprite emblemSprite = Resources.Load<Sprite>(BannerEmblemPath(banner));
                Image emblem = kit.NewImage("BannerEmblem", tab.transform, emblemSprite, Color.white);
                PanelKit.PlaceTop(emblem.rectTransform, (140f - emblemSize) * 0.5f, 0, emblemSize, emblemSize);
                emblem.preserveAspect = true;
                emblem.raycastTarget = false;
                emblem.transform.SetSiblingIndex(1);

                Text label = PanelKit.LabelOf(tab);
                PanelKit.PlaceTop(label.rectTransform, 8, embeddedMode ? 80 : 91, 124, 30);
                label.alignment = TextAnchor.MiddleCenter;
                kit.AddOutline(label.gameObject, new Color32(8, 5, 30, 230), 1.5f);
                bannerTabs.Add(tab);
                bannerTabEmblems.Add(emblem);
            }
        }

        private static string BannerEmblemPath(GachaBannerDefinition banner)
        {
            if (banner != null && banner.Kind == GachaBannerKind.Costume)
                return AiArtRoot + "gacha-emblem-costume-ai-v1-20260903";
            if (banner != null && banner.Id.IndexOf("standard", StringComparison.OrdinalIgnoreCase) >= 0)
                return AiArtRoot + "gacha-emblem-standard-ai-v1-20260903";
            return AiArtRoot + "gacha-emblem-debut-ai-v1-20260903";
        }

        private static string ShortBannerLabel(GachaBannerDefinition banner)
        {
            if (banner != null && banner.Kind == GachaBannerKind.Costume) return "霓虹衣装";
            if (banner != null && banner.Id.IndexOf("standard", StringComparison.OrdinalIgnoreCase) >= 0)
                return "常驻签约";
            return "初登场";
        }

        private void BuildBannerCard()
        {
            GameObject card = kit.NewPanel("BannerCard", transform, new Color32(9, 10, 42, 12), 0);
            float cardTop = embeddedMode ? 0f : 70f;
            float cardHeight = embeddedMode ? 914f : 1150f;
            PanelKit.PlaceTop(card.GetComponent<RectTransform>(), 0, cardTop, 720, cardHeight);

            Image glow = kit.NewImage("BannerGlow", card.transform, kit.RadialSprite(), new Color32(95, 128, 255, 92));
            PanelKit.PlaceTop(glow.rectTransform, 24, 0, 672, embeddedMode ? 850 : 1010);
            Image lowerGlow = kit.NewImage("BannerLowerGlow", card.transform, kit.RadialSprite(),
                new Color32(255, 61, 193, 74));
            PanelKit.PlaceTop(lowerGlow.rectTransform, 60, embeddedMode ? 510 : 610, 600,
                embeddedMode ? 390 : 520);

            GameObject frame = kit.NewPanel("FeaturedFrame", card.transform, new Color32(7, 9, 38, 3), 28);
            PanelKit.PlaceTop(frame.GetComponent<RectTransform>(), 38, 0, 644, embeddedMode ? 874 : 1090);
            kit.AddOutline(frame, new Color32(157, 111, 255, 42), 1);
            featuredPortrait = kit.NewImage("FeaturedPortrait", frame.transform, null, PanelKit.White);
            PanelKit.Stretch(featuredPortrait.rectTransform, -34, -18, 34, 18);
            featuredPortrait.preserveAspect = true;
            featuredPortrait.useSpriteMesh = true;
            featuredFallback = kit.NewPlacedText(frame.transform, string.Empty, 12, Color.clear,
                0, 0, 1, 1, TextAnchor.MiddleCenter);

            GameObject caption = kit.NewPanel("HeroCaption", card.transform, new Color32(8, 10, 42, 148), 22);
            PanelKit.PlaceTop(caption.GetComponent<RectTransform>(), 154, embeddedMode ? 716 : 824, 412, 144);
            AddAiUiSkin(caption, "gacha-identity-panel-ai-v2", Color.white);
            kit.AddOutline(caption, new Color32(255, 103, 216, 155), 1.5f);
            bannerKind = kit.NewPlacedText(caption.transform, "角色招募", 13,
                new Color32(255, 173, 226, 255), 34, 16, 350, 24, TextAnchor.MiddleLeft, FontStyle.Bold);
            bannerTitle = kit.NewPlacedText(caption.transform, string.Empty, 30, PanelKit.White,
                34, 38, 350, 44, TextAnchor.MiddleLeft, FontStyle.Bold);
            featuredText = kit.NewPlacedText(caption.transform, string.Empty, 16, PanelKit.Gold,
                34, 80, 350, 28, TextAnchor.MiddleLeft, FontStyle.Bold);
            kit.NewPlacedText(caption.transform, "重复获得转化为碎片 · 十连至少获得 SR", 12, PanelKit.Muted,
                34, 108, 350, 24, TextAnchor.MiddleLeft);
        }

        private void BuildRates()
        {
            GameObject panel = kit.NewPanel("RateBoard", transform, new Color32(8, 11, 44, 116), 16);
            PanelKit.PlaceTop(panel.GetComponent<RectTransform>(), 166, embeddedMode ? 914 : 1052, 536, 62);
            AddAiUiSkin(panel, "gacha-rates-panel-ai-v2", Color.white);
            kit.AddOutline(panel, new Color32(151, 119, 255, 72), 1);
            kit.NewPlacedText(panel.transform, "概率公示", 13, new Color32(255, 173, 226, 255),
                26, 0, 82, 62, TextAnchor.MiddleLeft, FontStyle.Bold);
            rateText = kit.NewPlacedText(panel.transform, string.Empty, 13, PanelKit.White,
                112, 0, 400, 62, TextAnchor.MiddleLeft, FontStyle.Bold);
        }

        private void BuildPity()
        {
            GameObject panel = kit.NewPanel("PityBoard", transform, new Color32(8, 11, 44, 124), 16);
            PanelKit.PlaceTop(panel.GetComponent<RectTransform>(), 166, embeddedMode ? 982 : 1122, 536, 84);
            AddAiUiSkin(panel, "gacha-pity-panel-ai-v2", Color.white);
            kit.AddOutline(panel, new Color32(151, 119, 255, 76), 1);
            kit.NewPlacedText(panel.transform, "保底进度", 13, new Color32(255, 173, 226, 255),
                26, 0, 82, 84, TextAnchor.MiddleLeft, FontStyle.Bold);
            pityText = kit.NewPlacedText(panel.transform, string.Empty, 13, PanelKit.White,
                102, 5, 420, 27, TextAnchor.MiddleLeft, FontStyle.Bold);
            pityFill = kit.NewBar("PityBar", panel.transform, 102, 35, 250, 9,
                new Color32(66, 54, 117, 255), PanelKit.Pink, 9);
            totalsText = kit.NewPlacedText(panel.transform, string.Empty, 12, PanelKit.Muted,
                102, 48, 286, 27, TextAnchor.MiddleLeft);
            guaranteeChip = kit.NewPanel("GuaranteeChip", panel.transform, new Color32(255, 205, 96, 235), 12);
            PanelKit.PlaceTop(guaranteeChip.GetComponent<RectTransform>(), 392, 44, 130, 28);
            kit.NewPlacedText(guaranteeChip.transform, "限定保底已激活", 12,
                new Color32(52, 30, 8, 255), 4, 0, 122, 28, TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        private void BuildBalances()
        {
            if (!embeddedMode)
            {
                diamondText = BalanceChip("BalanceDiamond", 356, 14, "diamond", 168);
                goldText = BalanceChip("BalanceGold", 532, 14, "gold", 170);
            }

            ticketText = BalanceChip("BalanceTicket", 20, embeddedMode ? 1078 : 1218,
                "recruit-ticket", 142);
        }

        private Text BalanceChip(string name, float x, float y, string currency, float width)
        {
            GameObject chip = kit.NewPanel(name, transform, new Color32(13, 16, 55, 88), 17);
            PanelKit.PlaceTop(chip.GetComponent<RectTransform>(), x, y, width, 54);
            if (name == "BalanceTicket")
                AddAiUiSkin(chip, "gacha-ticket-panel-ai-v2", Color.white);
            kit.AddOutline(chip, new Color32(137, 110, 222, 56), 1);
            Sprite icon = PanelKit.CurrencyIcon(currency);
            Image iconImage = kit.NewImage("Icon", chip.transform, icon ?? kit.RoundedSprite(8),
                icon != null ? Color.white : PanelKit.CurrencyColor(currency));
            PanelKit.PlaceTop(iconImage.rectTransform, 12, 12, 32, 32);
            iconImage.preserveAspect = true;
            Text currencyName = kit.NewPlacedText(chip.transform, PanelKit.CurrencyName(currency), 13, PanelKit.Muted,
                50, 5, width - 58, 21, TextAnchor.MiddleLeft, FontStyle.Bold);
            currencyName.name = "CurrencyName";
            Text value = kit.NewPlacedText(chip.transform, "0", 17, PanelKit.White,
                50, 25, width - 58, 25, TextAnchor.MiddleLeft, FontStyle.Bold);
            value.name = "Value";
            if (name == "BalanceTicket")
            {
                ticketIcon = iconImage;
                ticketNameText = currencyName;
            }
            return value;
        }

        private void BuildPullButtons()
        {
            float sideButtonTop = embeddedMode ? 1152f : 1308f;
            float sideButtonHeight = embeddedMode ? 58f : 66f;
            float tenButtonTop = embeddedMode ? 1138f : 1274f;
            float tenButtonHeight = embeddedMode ? 88f : 116f;
            GameObject details = kit.NewButton("GachaDetails", transform, "详情", 15,
                DetailGlass, PanelKit.White, () => Notify(RateSummary(CurrentBanner)), 18);
            PanelKit.PlaceTop(details.GetComponent<RectTransform>(), 20, sideButtonTop, 130, sideButtonHeight);
            kit.AddOutline(details, new Color32(174, 148, 255, 115), 1);

            pullOneButton = kit.NewButton("PullOne", transform, string.Empty, 20,
                ActionGlass, PanelKit.White, () => Pull(1), 20);
            PanelKit.PlaceTop(pullOneButton.GetComponent<RectTransform>(), 552,
                embeddedMode ? sideButtonTop : 1312, 150, sideButtonHeight);
            AddAiUiSkin(pullOneButton, "gacha-one-pull-frame-ai-v2", Color.white);
            pullOneLabel = PanelKit.LabelOf(pullOneButton);
            pullOneLabel.fontSize = 14;
            kit.AddOutline(pullOneButton, new Color32(197, 156, 255, 105), 1);

            pullTenButton = kit.NewButton("PullTen", transform, string.Empty, 20,
                TenPullGlass, PanelKit.White, () => Pull(10), 30);
            PanelKit.PlaceTop(pullTenButton.GetComponent<RectTransform>(), 158, tenButtonTop, 380, tenButtonHeight);
            pullTenLabel = PanelKit.LabelOf(pullTenButton);
            pullTenLabel.text = "签约 ×10";
            pullTenLabel.fontSize = 18;
            pullTenLabel.alignment = TextAnchor.MiddleCenter;
            PanelKit.PlaceTop(pullTenLabel.rectTransform, 58, embeddedMode ? 17 : 30, 264, 26);
            pullTenCostLabel = kit.NewPlacedText(pullTenButton.transform, string.Empty, 12,
                new Color32(242, 231, 255, 255), 54, embeddedMode ? 48 : 60, 272, 20,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            pullTenCostLabel.gameObject.name = "PullTenCost";
            Sprite frameSprite = LoadResourceSprite(AiUiRoot + "gacha-ten-pull-frame-ai-v2") ??
                                 LoadResourceSprite(AiArtRoot + "gacha-ten-pull-frame-ai-v1-20260903");
            pullTenFrame = kit.NewImage("TenPullCrystalFrame", pullTenButton.transform, frameSprite, Color.white);
            PanelKit.Stretch(pullTenFrame.rectTransform);
            pullTenFrame.type = Image.Type.Simple;
            pullTenFrame.raycastTarget = false;
            pullTenFrame.transform.SetAsFirstSibling();
            pullTenLabel.transform.SetAsLastSibling();
            pullTenCostLabel.transform.SetAsLastSibling();

            hintText = kit.NewPlacedText(transform, "选择心仪卡池，与舞台上的她签订契约", 13,
                new Color32(220, 206, 239, 255), 40, embeddedMode ? 1232 : 1402, 640, 30,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            kit.NewPlacedText(transform, "星光汇聚，下一位成员正在等待", 12, new Color32(151, 140, 190, 255),
                40, embeddedMode ? 1260 : 1436, 640, 24, TextAnchor.MiddleCenter);
        }

        private void BuildResult()
        {
            Image overlay = kit.NewImage("GachaResult", transform, null, new Color32(3, 4, 23, 238));
            PanelKit.Stretch(overlay.rectTransform);
            overlay.raycastTarget = true;
            resultOverlay = overlay.gameObject;
            resultSkipButton = resultOverlay.AddComponent<Button>();
            resultSkipButton.targetGraphic = overlay;
            resultSkipButton.transition = Selectable.Transition.None;
            resultSkipButton.onClick.AddListener(RevealAllNow);

            Image glow = kit.NewImage("ResultGlow", overlay.transform, kit.RadialSprite(), new Color32(255, 78, 212, 70));
            PanelKit.PlaceTop(glow.rectTransform, 60, 120, 600, 600);

            resultTitle = kit.NewPlacedText(overlay.transform, "签约结果", 30, PanelKit.White,
                40, 150, 640, 52, TextAnchor.MiddleCenter, FontStyle.Bold);
            kit.AddOutline(resultTitle.gameObject, new Color32(255, 74, 196, 150), 2);
            resultSummary = kit.NewPlacedText(overlay.transform, string.Empty, 15, PanelKit.Muted,
                40, 206, 640, 30, TextAnchor.MiddleCenter, FontStyle.Bold);

            for (int index = 0; index < GachaEngine.TenPullCount; index++)
                resultCells.Add(BuildResultCell(overlay.transform, index));

            pullAgainButton = kit.NewButton("PullAgain", overlay.transform, "再来十连", 21, PanelKit.Pink,
                PanelKit.White, PullAgain, 24);
            PanelKit.PlaceTop(pullAgainButton.GetComponent<RectTransform>(), 60, 1000, 290, 74);
            GameObject back = kit.NewButton("ResultBack", overlay.transform, "返回", 21, PanelKit.ButtonDark,
                PanelKit.White, CloseResult, 24);
            PanelKit.PlaceTop(back.GetComponent<RectTransform>(), 370, 1000, 290, 74);
            kit.NewPlacedText(overlay.transform, "揭示过程中点击任意处可直接查看全部结果", 12, PanelKit.Muted,
                40, 1090, 640, 28, TextAnchor.MiddleCenter);

            resultOverlay.SetActive(false);
        }

        private ResultCell BuildResultCell(Transform parent, int index)
        {
            const float cellWidth = 120f;
            const float cellHeight = 168f;
            const float gap = 12f;
            int column = index % 5;
            int row = index / 5;
            float x = 36f + column * (cellWidth + gap);
            float y = 268f + row * (cellHeight + gap);

            GameObject root = kit.NewPanel("Result-" + index, parent, RColor, 18);
            RectTransform rect = root.GetComponent<RectTransform>();
            PanelKit.PlaceTop(rect, x, y, cellWidth, cellHeight);
            PanelKit.CenterPivot(rect);

            Image portrait = kit.NewImage("Portrait", root.transform, null, PanelKit.White);
            PanelKit.PlaceTop(portrait.rectTransform, 10, 30, 100, 92);
            portrait.preserveAspect = true;

            Text rarity = kit.NewPlacedText(root.transform, "R", 13, PanelKit.White, 8, 6, 60, 22,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            GameObject badge = kit.NewPanel("NewBadge", root.transform, PanelKit.Pink, 8);
            PanelKit.PlaceTop(badge.GetComponent<RectTransform>(), 80, 6, 34, 22);
            kit.NewPlacedText(badge.transform, "新", 12, PanelKit.White, 0, 0, 34, 22, TextAnchor.MiddleCenter,
                FontStyle.Bold);
            Text name = kit.NewPlacedText(root.transform, string.Empty, 14, PanelKit.White, 6, 122, 108, 24,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            Text footer = kit.NewPlacedText(root.transform, string.Empty, 12, PanelKit.Muted, 6, 144, 108, 20,
                TextAnchor.MiddleCenter);

            ResultCell cell = new ResultCell
            {
                Root = root,
                Rect = rect,
                Background = root.GetComponent<Image>(),
                Portrait = portrait,
                Outline = kit.AddOutline(root, new Color32(255, 205, 96, 0), 3),
                Rarity = rarity,
                Name = name,
                NewBadge = badge,
                Footer = footer,
            };
            root.SetActive(false);
            return cell;
        }

        // ------------------------------------------------------------------ state

        private GachaBannerDefinition CurrentBanner
        {
            get
            {
                IReadOnlyList<GachaBannerDefinition> banners = service.Banners;
                if (banners == null || banners.Count == 0) return null;
                bannerIndex = Mathf.Clamp(bannerIndex, 0, banners.Count - 1);
                return banners[bannerIndex];
            }
        }

        private void SelectBanner(int index)
        {
            if (pulling) return;
            bannerIndex = index;
            Refresh();
            GachaBannerDefinition banner = CurrentBanner;
            if (banner != null) Notify($"已切换到「{banner.Name}」");
        }

        private void HandleModelChanged()
        {
            Refresh();
        }

        private void Refresh()
        {
            if (kit == null) return;
            GachaBannerDefinition banner = CurrentBanner;

            for (int index = 0; index < bannerTabs.Count; index++)
            {
                bool selected = index == bannerIndex;
                PanelKit.SetButtonState(bannerTabs[index], true,
                    Color.clear);
                Text label = PanelKit.LabelOf(bannerTabs[index]);
                if (label != null) label.color = selected ? PanelKit.White : new Color32(184, 173, 211, 220);
                if (index < bannerTabEmblems.Count)
                    bannerTabEmblems[index].color = selected ? Color.white : new Color(0.62f, 0.59f, 0.76f, 0.78f);
                Transform activeMark = bannerTabs[index].transform.Find("ActiveMark");
                if (activeMark != null) activeMark.gameObject.SetActive(selected);
                bannerTabs[index].transform.localScale = selected ? Vector3.one * 1.04f : Vector3.one;
            }

            if (diamondText != null) diamondText.text = service.Balance("diamond").ToString("N0");
            RefreshTicketBalance(banner);
            if (goldText != null) goldText.text = service.Balance("gold").ToString("N0");

            if (banner == null)
            {
                bannerTitle.text = "暂无卡池";
                bannerKind.text = string.Empty;
                featuredText.text = string.Empty;
                rateText.text = "卡池数据尚未配置";
                pityText.text = string.Empty;
                pityFill.fillAmount = 0f;
                totalsText.text = string.Empty;
                guaranteeChip.SetActive(false);
                pullOneLabel.text = "签约 ×1";
                pullTenLabel.text = "签约 ×10";
                pullTenCostLabel.text = string.Empty;
                PanelKit.SetButtonState(pullOneButton, false, DisabledActionGlass);
                PanelKit.SetButtonState(pullTenButton, false, DisabledActionGlass);
                if (pullTenFrame != null) pullTenFrame.color = new Color(0.55f, 0.55f, 0.65f, 0.62f);
                featuredPortrait.enabled = false;
                featuredFallback.gameObject.SetActive(false);
                return;
            }

            bannerTitle.text = banner.Name;
            bannerKind.text = banner.Kind == GachaBannerKind.Costume ? "服装招募" : "角色招募";
            featuredText.text = FeaturedLine(banner);
            Sprite featuredSprite = ResolveBannerPortrait(banner) ?? Resources.Load<Sprite>("Art/HeroFallback");
            featuredPortrait.sprite = featuredSprite;
            featuredPortrait.enabled = featuredSprite != null;
            featuredFallback.gameObject.SetActive(false);

            rateText.text = InlineRateSummary(banner);

            GachaBannerState state = service.BannerState(banner.Id);
            int pity = state != null ? state.Pity : 0;
            int remaining = Mathf.Max(0, banner.HardPity - pity);
            pityText.text = remaining == 0 ? "下一抽必得 SSR" : $"已累计 {pity} 抽 · 再 {remaining} 抽必得 SSR";
            pityFill.fillAmount = banner.HardPity > 0 ? Mathf.Clamp01(pity / (float)banner.HardPity) : 0f;
            pityFill.color = pity >= banner.SoftPityStart ? PanelKit.Gold : PanelKit.Pink;
            totalsText.text = state != null
                ? $"累计签约 {state.TotalPulls} 次 · 获得 SSR {state.TotalSsr} 次"
                : "尚未在此卡池签约";
            guaranteeChip.SetActive(state != null && state.FeaturedGuaranteed && banner.GuaranteeFeaturedAfterLoss);

            string currencyName = PanelKit.CurrencyName(banner.CostCurrency);
            int balance = service.Balance(banner.CostCurrency);
            int tickets = string.IsNullOrEmpty(banner.TicketCurrency) ? 0 : service.Balance(banner.TicketCurrency);
            bool ticketCoversOne = tickets >= 1;
            bool ticketCoversTen = tickets >= GachaEngine.TenPullCount;
            pullOneLabel.text = ticketCoversOne
                ? $"签约 ×1\n{PanelKit.CurrencyName(banner.TicketCurrency)} ×1（余 {tickets}）"
                : $"签约 ×1\n{currencyName} {banner.CostPerPull}（余 {balance:N0}）";
            pullTenLabel.text = "签约 ×10";
            pullTenCostLabel.text = ticketCoversTen
                ? $"{PanelKit.CurrencyName(banner.TicketCurrency)} ×10 · 余 {tickets}"
                : $"{currencyName} {banner.CostTenPull} · 余 {balance:N0}";

            bool canOne = !pulling && (ticketCoversOne || balance >= banner.CostPerPull);
            bool canTen = !pulling && (ticketCoversTen || balance >= banner.CostTenPull);
            PanelKit.SetButtonState(pullOneButton, canOne,
                canOne ? ActionGlass : DisabledActionGlass);
            PanelKit.SetButtonState(pullTenButton, canTen,
                canTen ? TenPullGlass : DisabledActionGlass);
            if (pullTenFrame != null)
                pullTenFrame.color = canTen ? Color.white : new Color(0.55f, 0.55f, 0.65f, 0.62f);
        }

        private void RefreshTicketBalance(GachaBannerDefinition banner)
        {
            string currency = banner != null && !string.IsNullOrEmpty(banner.TicketCurrency)
                ? banner.TicketCurrency
                : "recruit-ticket";
            if (ticketText != null) ticketText.text = service.Balance(currency).ToString("N0");
            if (ticketNameText != null) ticketNameText.text = PanelKit.CurrencyName(currency);
            if (ticketIcon == null) return;

            Sprite icon = PanelKit.CurrencyIcon(currency);
            ticketIcon.sprite = icon ?? kit.RoundedSprite(8);
            ticketIcon.color = icon != null ? Color.white : PanelKit.CurrencyColor(currency);
            ticketIcon.preserveAspect = true;
        }

        private static string InlineRateSummary(GachaBannerDefinition banner)
        {
            if (banner == null) return string.Empty;
            int rPermille = Mathf.Max(0, 1000 - banner.SsrRatePermille - banner.SrRatePermille);
            string guarantee = banner.TenPullGuaranteesSr ? " · 十连至少 SR" : string.Empty;
            return $"SSR {PanelKit.Permille(banner.SsrRatePermille)} · " +
                   $"SR {PanelKit.Permille(banner.SrRatePermille)} · " +
                   $"R {PanelKit.Permille(rPermille)}{guarantee}";
        }

        /// <summary>
        /// Each public pool owns a distinct local stage model: debut = xingli,
        /// standard = feiyin (wubai fallback), costume = yeying (yaoguang fallback).
        /// The visual assignment is intentionally independent from probability data.
        /// </summary>
        private static Sprite ResolveBannerPortrait(GachaBannerDefinition banner)
        {
            if (banner == null) return null;

            string[] visualCandidates;
            if (banner.Kind == GachaBannerKind.Costume)
                visualCandidates = new[] { "yeying", "yaoguang" };
            else if (banner.Id.IndexOf("standard", StringComparison.OrdinalIgnoreCase) >= 0)
                visualCandidates = new[] { "feiyin", "wubai" };
            else
                visualCandidates = new[] { "xingli" };

            for (int index = 0; index < visualCandidates.Length; index++)
            {
                Sprite visual = PanelKit.MemberSpriteOrNull(visualCandidates[index], false);
                if (visual != null) return visual;
            }

            for (int index = 0; index < banner.FeaturedItemIds.Count; index++)
            {
                string itemId = banner.FeaturedItemIds[index];
                Sprite exact = PanelKit.MemberSpriteOrNull(itemId, false);
                if (exact != null) return exact;

                MemberDefinition[] members = GameModel.Members;
                for (int memberIndex = 0; memberIndex < members.Length; memberIndex++)
                {
                    MemberDefinition member = members[memberIndex];
                    if (member == null || string.IsNullOrEmpty(member.Id)) continue;
                    if (!itemId.Contains("-" + member.Id + "-")) continue;
                    Sprite owner = PanelKit.MemberSpriteOrNull(member.Id, false);
                    if (owner != null) return owner;
                }
            }

            IReadOnlyList<string>[] fallbackPools =
            {
                banner.StandardSsrItemIds,
                banner.SrItemIds,
                banner.RItemIds,
            };
            for (int poolIndex = 0; poolIndex < fallbackPools.Length; poolIndex++)
            {
                IReadOnlyList<string> pool = fallbackPools[poolIndex];
                for (int index = 0; index < pool.Count; index++)
                {
                    Sprite sprite = PanelKit.MemberSpriteOrNull(pool[index], false);
                    if (sprite != null) return sprite;
                }
            }

            // A malformed banner must still never produce an empty art frame. This is a final
            // defensive fallback to the first valid local roster portrait.
            MemberDefinition[] roster = GameModel.Members;
            for (int index = 0; index < roster.Length; index++)
            {
                MemberDefinition member = roster[index];
                if (member == null) continue;
                Sprite sprite = PanelKit.MemberSpriteOrNull(member.Id, false);
                if (sprite != null) return sprite;
            }

            return null;
        }

        private string FeaturedLine(GachaBannerDefinition banner)
        {
            if (banner.FeaturedItemIds.Count == 0) return "常驻卡池 · 无限定角色";
            var builder = new System.Text.StringBuilder();
            for (int index = 0; index < banner.FeaturedItemIds.Count; index++)
            {
                if (index > 0) builder.Append(" · ");
                builder.Append(DisplayName(banner.FeaturedItemIds[index]));
            }

            return builder.ToString();
        }

        /// <summary>
        /// Rate table text generated purely from the banner numbers so 概率公示 can never drift
        /// from what the engine rolls. Public so tests can assert on the wording.
        /// </summary>
        public static string RateSummary(GachaBannerDefinition banner)
        {
            if (banner == null) return string.Empty;
            int rPermille = Mathf.Max(0, 1000 - banner.SsrRatePermille - banner.SrRatePermille);
            var builder = new System.Text.StringBuilder();
            builder.Append("SSR ").Append(PanelKit.Permille(banner.SsrRatePermille))
                .Append(" · SR ").Append(PanelKit.Permille(banner.SrRatePermille))
                .Append(" · R ").Append(PanelKit.Permille(rPermille)).Append('\n');
            builder.Append("第 ").Append(banner.SoftPityStart).Append(" 抽起概率提升，每抽 +")
                .Append(PanelKit.Permille(banner.SoftPityStepPermille))
                .Append(" · ").Append(banner.HardPity).Append(" 抽必得 SSR").Append('\n');
            if (banner.FeaturedItemIds.Count > 0 && banner.StandardSsrItemIds.Count > 0)
            {
                builder.Append("SSR 中限定角色占 ").Append(PanelKit.Permille(banner.RateUpSharePermille));
                if (banner.GuaranteeFeaturedAfterLoss) builder.Append(" · 未中限定后下一次 SSR 必为限定");
                builder.Append('\n');
            }

            if (banner.TenPullGuaranteesSr) builder.Append("每次十连至少获得一名 SR 及以上 · ");
            builder.Append("单抽 ").Append(banner.CostPerPull).Append(' ').Append(PanelKit.CurrencyName(banner.CostCurrency))
                .Append("，十连 ").Append(banner.CostTenPull).Append(' ').Append(PanelKit.CurrencyName(banner.CostCurrency));
            return builder.ToString();
        }

        /// <summary>Short form used by the immersive right-side probability card.</summary>
        public static string CompactRateSummary(GachaBannerDefinition banner)
        {
            if (banner == null) return string.Empty;
            int rPermille = Mathf.Max(0, 1000 - banner.SsrRatePermille - banner.SrRatePermille);
            var builder = new System.Text.StringBuilder();
            builder.Append("SSR ").Append(PanelKit.Permille(banner.SsrRatePermille)).Append('\n');
            builder.Append("SR  ").Append(PanelKit.Permille(banner.SrRatePermille)).Append('\n');
            builder.Append("R   ").Append(PanelKit.Permille(rPermille)).Append("\n\n");
            builder.Append(banner.TenPullGuaranteesSr ? "十连至少获得 SR" : "概率以公示为准");
            return builder.ToString();
        }

        private string DisplayName(string itemId)
        {
            string custom = service.ItemDisplayName(itemId);
            return string.IsNullOrEmpty(custom) ? PanelKit.MemberNameOrId(itemId) : custom;
        }

        // ------------------------------------------------------------------ pulls

        private void Pull(int count)
        {
            if (pulling || closing) return;
            GachaBannerDefinition banner = CurrentBanner;
            if (banner == null)
            {
                Notify("当前没有可用卡池");
                return;
            }

            pulling = true;
            Refresh();
            ulong totalPulls = 0;
            IReadOnlyList<GachaBannerDefinition> banners = service.Banners;
            for (int index = 0; index < banners.Count; index++)
            {
                GachaBannerState state = service.BannerState(banners[index].Id);
                if (state != null) totalPulls += (ulong)Math.Max(0, state.TotalPulls);
            }

            ulong seed = unchecked((ulong)DateTime.UtcNow.Ticks ^ totalPulls);
            bool succeeded = service.TryPull(banner.Id, count, seed, out List<GachaPullResult> results, out string message);
            pulling = false;

            if (!succeeded || results == null || results.Count == 0)
            {
                Notify(string.IsNullOrEmpty(message) ? "签约失败" : message);
                Refresh();
                return;
            }

            lastPullCount = count;
            if (!string.IsNullOrEmpty(message)) hintText.text = message;
            Refresh();
            ShowResults(banner, results);
        }

        private void ShowResults(GachaBannerDefinition banner, List<GachaPullResult> results)
        {
            if (revealRoutine != null) StopCoroutine(revealRoutine);
            resultOverlay.SetActive(true);
            resultOverlay.transform.SetAsLastSibling();
            resultTitle.text = results.Count == 1 ? "签约结果 ×1" : "签约结果 ×10";

            int ssr = 0;
            int sr = 0;
            int shards = 0;
            int fresh = 0;
            for (int index = 0; index < results.Count; index++)
            {
                if (results[index].Rarity == GachaRarity.Ssr) ssr++;
                else if (results[index].Rarity == GachaRarity.Sr) sr++;
                shards += results[index].ShardReward;
                if (results[index].IsNew) fresh++;
            }

            resultSummary.text = $"{banner.Name} · SSR {ssr} · SR {sr} · 新成员 {fresh} · 碎片 +{shards}";
            PanelKit.LabelOf(pullAgainButton).text = results.Count == 1 ? "再来一次" : "再来十连";

            for (int index = 0; index < resultCells.Count; index++)
            {
                ResultCell cell = resultCells[index];
                if (index >= results.Count)
                {
                    cell.Root.SetActive(false);
                    continue;
                }

                FillCell(cell, results[index]);
                cell.Root.SetActive(false);
                cell.Rect.localScale = Vector3.zero;
            }

            LayoutCells(results.Count);
            revealRoutine = StartCoroutine(RevealResults(results.Count));
        }

        private void LayoutCells(int count)
        {
            const float cellWidth = 120f;
            const float cellHeight = 168f;
            const float gap = 12f;
            if (count == 1)
            {
                ResultCell single = resultCells[0];
                PanelKit.PlaceTop(single.Rect, 300f - 30f, 300f, 180f, 250f);
                PanelKit.CenterPivot(single.Rect);
                PanelKit.PlaceTop(single.Portrait.rectTransform, 12, 36, 156, 150);
                PanelKit.PlaceTop(single.Name.rectTransform, 8, 192, 164, 30);
                PanelKit.PlaceTop(single.Footer.rectTransform, 8, 222, 164, 22);
                PanelKit.PlaceTop(single.NewBadge.GetComponent<RectTransform>(), 138, 8, 34, 22);
                single.Name.fontSize = 18;
                return;
            }

            for (int index = 0; index < count && index < resultCells.Count; index++)
            {
                ResultCell cell = resultCells[index];
                int column = index % 5;
                int row = index / 5;
                PanelKit.PlaceTop(cell.Rect, 36f + column * (cellWidth + gap), 268f + row * (cellHeight + gap),
                    cellWidth, cellHeight);
                PanelKit.CenterPivot(cell.Rect);
                PanelKit.PlaceTop(cell.Portrait.rectTransform, 10, 30, 100, 92);
                PanelKit.PlaceTop(cell.Name.rectTransform, 6, 122, 108, 24);
                PanelKit.PlaceTop(cell.Footer.rectTransform, 6, 144, 108, 20);
                PanelKit.PlaceTop(cell.NewBadge.GetComponent<RectTransform>(), 80, 6, 34, 22);
                cell.Name.fontSize = 14;
            }
        }

        private void FillCell(ResultCell cell, GachaPullResult result)
        {
            bool ssr = result.Rarity == GachaRarity.Ssr;
            bool sr = result.Rarity == GachaRarity.Sr;
            cell.Ssr = ssr;
            cell.Background.color = ssr ? SsrColor : sr ? SrColor : RColor;
            cell.Rarity.text = result.Rarity;
            cell.Rarity.color = ssr ? PanelKit.Gold : sr ? new Color32(214, 170, 255, 255) : PanelKit.Muted;
            cell.Name.text = DisplayName(result.ItemId);
            cell.NewBadge.SetActive(result.IsNew);
            cell.Outline.effectColor = new Color(1f, 0.8f, 0.37f, 0f);

            Sprite portrait = ResolveResultPortrait(result.ItemId);
            cell.Portrait.sprite = portrait;
            cell.Portrait.enabled = portrait != null;

            string footer;
            if (result.IsNew) footer = result.IsFeatured ? "限定 · 新成员" : "新成员";
            else footer = result.ShardReward > 0 ? $"碎片 +{result.ShardReward}" : "重复";
            if (result.HitHardPity) footer = "保底 · " + footer;
            else if (result.UpgradedByTenPullGuarantee) footer = "十连保底 · " + footer;
            cell.Footer.text = footer;
            cell.Footer.color = result.IsNew ? PanelKit.Pink : PanelKit.Muted;
        }

        private static Sprite ResolveResultPortrait(string itemId)
        {
            Sprite exact = PanelKit.MemberSpriteOrNull(itemId, true);
            if (exact != null) return exact;

            MemberDefinition[] members = GameModel.Members;
            if (!string.IsNullOrEmpty(itemId))
            {
                for (int index = 0; index < members.Length; index++)
                {
                    MemberDefinition member = members[index];
                    if (member == null || string.IsNullOrEmpty(member.Id)) continue;
                    string marker = "-" + member.Id + "-";
                    if (itemId.IndexOf(marker, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    Sprite owner = PanelKit.MemberSpriteOrNull(member.Id, true);
                    if (owner != null) return owner;
                }

                if (itemId.StartsWith("accessory-", StringComparison.OrdinalIgnoreCase))
                {
                    Sprite accessory = Resources.Load<Sprite>(
                        AiArtRoot + "gacha-emblem-costume-ai-v1-20260903");
                    if (accessory != null) return accessory;
                }
            }

            // Keep malformed or future non-member pool entries legible until they receive
            // dedicated art instead of silently leaving a transparent result card.
            for (int index = 0; index < members.Length; index++)
            {
                MemberDefinition member = members[index];
                if (member == null) continue;
                Sprite fallback = PanelKit.MemberSpriteOrNull(member.Id, true);
                if (fallback != null) return fallback;
            }

            return Resources.Load<Sprite>(AiArtRoot + "gacha-emblem-debut-ai-v1-20260903");
        }

        private IEnumerator RevealResults(int count)
        {
            revealing = true;
            pullAgainButton.SetActive(false);
            for (int index = 0; index < count && index < resultCells.Count; index++)
            {
                StartCoroutine(RevealCell(resultCells[index]));
                yield return new WaitForSecondsRealtime(RevealInterval);
            }

            yield return new WaitForSecondsRealtime(RevealDuration);
            FinishReveal();
        }

        private IEnumerator RevealCell(ResultCell cell)
        {
            cell.Root.SetActive(true);
            float duration = cell.Ssr ? RevealDuration * 1.6f : RevealDuration;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // SSR cards overshoot to ~1.2× mid-way and settle back; others ease straight in.
                float scale = cell.Ssr
                    ? Mathf.SmoothStep(0.4f, 1f, t) + 0.22f * Mathf.Sin(t * Mathf.PI)
                    : Mathf.SmoothStep(0.4f, 1f, t);
                cell.Rect.localScale = new Vector3(scale, scale, 1f);
                if (cell.Ssr) cell.Outline.effectColor = new Color(1f, 0.8f, 0.37f, t);
                yield return null;
            }

            cell.Rect.localScale = Vector3.one;
            if (cell.Ssr)
            {
                cell.Outline.effectColor = new Color(1f, 0.8f, 0.37f, 1f);
                kit.PlaySuccess();
            }
        }

        private void RevealAllNow()
        {
            if (!revealing) return;
            StopAllCoroutines();
            revealRoutine = null;
            for (int index = 0; index < resultCells.Count; index++)
            {
                ResultCell cell = resultCells[index];
                if (index >= lastPullCount) continue;
                cell.Root.SetActive(true);
                cell.Rect.localScale = Vector3.one;
                if (cell.Ssr) cell.Outline.effectColor = new Color(1f, 0.8f, 0.37f, 1f);
            }

            FinishReveal();
        }

        private void FinishReveal()
        {
            revealing = false;
            pullAgainButton.SetActive(true);
        }

        private void PullAgain()
        {
            if (revealing) return;
            CloseResult();
            Pull(lastPullCount);
        }

        private void CloseResult()
        {
            if (revealRoutine != null)
            {
                StopCoroutine(revealRoutine);
                revealRoutine = null;
            }

            revealing = false;
            resultOverlay.SetActive(false);
            Refresh();
        }

        // ------------------------------------------------------------------ lifecycle

        private void Notify(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            if (hintText != null) hintText.text = message;
            onMessage?.Invoke(message);
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

        private void OnDestroy()
        {
            StopAllCoroutines();
            if (model != null) model.Changed -= HandleModelChanged;
            kit?.Dispose();
        }
    }
}
