using System;
using System.Collections;
using System.Collections.Generic;
using ChoSiren.Systems.Gacha;
using ChoSiren.Systems.Presentation;
using ChoSiren.UI;
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
    public sealed partial class GachaPanel : MonoBehaviour
    {
        private const float RevealInterval = 0.12f;
        private const float RevealDuration = 0.22f;
        private const float CompactInterviewHeight = 1168f;

        private sealed class ResultCell
        {
            public GameObject Root;
            public RectTransform Rect;
            public Image Background;
            public RectTransform PortraitFrame;
            public Image Portrait;
            public Outline Outline;
            public Text Profile;
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

        // Control silhouettes traced on audition-board-038.png (853x1844, top-left origin).
        // The prev/next plaques interleave with the counter's spikes and the 签约 burst's
        // left shard reaches past the old rect, so each slice is vertex-clipped to its own
        // contour; the counter spikes and shared seams stay static.
        private static readonly Vector2[] AuditionPreviousSilhouette =
        {
            new Vector2(88f, 1342f), new Vector2(112f, 1300f), new Vector2(150f, 1282f),
            new Vector2(255f, 1278f), new Vector2(332f, 1288f), new Vector2(345f, 1298f),
            new Vector2(330f, 1345f), new Vector2(318f, 1390f), new Vector2(305f, 1396f),
            new Vector2(150f, 1397f), new Vector2(108f, 1398f), new Vector2(96f, 1386f),
        };
        private static readonly Vector2[] AuditionNextSilhouette =
        {
            new Vector2(512f, 1295f), new Vector2(562f, 1265f), new Vector2(695f, 1285f),
            new Vector2(712f, 1292f), new Vector2(738f, 1338f), new Vector2(705f, 1385f),
            new Vector2(665f, 1398f), new Vector2(618f, 1400f), new Vector2(560f, 1392f),
            new Vector2(508f, 1386f), new Vector2(502f, 1340f),
        };
        private static readonly Vector2[] AuditionViewSilhouette =
        {
            new Vector2(258f, 1558f), new Vector2(285f, 1518f), new Vector2(305f, 1510f),
            new Vector2(448f, 1510f), new Vector2(462f, 1520f), new Vector2(482f, 1558f),
            new Vector2(462f, 1595f), new Vector2(445f, 1600f), new Vector2(295f, 1602f),
            new Vector2(275f, 1592f),
        };
        private static readonly Vector2[] AuditionSignSilhouette =
        {
            new Vector2(545f, 1448f), new Vector2(795f, 1398f), new Vector2(838f, 1408f),
            new Vector2(856f, 1465f), new Vector2(848f, 1545f), new Vector2(866f, 1572f),
            new Vector2(848f, 1612f), new Vector2(835f, 1645f), new Vector2(700f, 1668f),
            new Vector2(640f, 1680f), new Vector2(555f, 1662f), new Vector2(528f, 1648f),
            new Vector2(452f, 1568f), new Vector2(449f, 1556f),
        };
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
        private Action<int> onSigned;
        /// <summary>2026-11-08 (少爷: "双击工牌可以进去这个页面"): opens the member detail page for a candidate.</summary>
        private Action<int> onViewMember;
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

        // The primary "选秀" tab is an interview browser, not a rarity gacha.  The legacy
        // standalone gacha stays available for old entry points while embedded mode uses these
        // lightweight pool and candidate states inside the unchanged global HUD/navigation shell.
        private int interviewPoolIndex;
        private int interviewCandidateIndex;
        private readonly int[] interviewChannelPositions = new int[2];
        private GameObject interviewContent;
        private string displayedInterviewCycle;
        private float nextInterviewRefreshCheck;

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
            IGachaService gachaService, Action<string> message = null, Action<int> signed = null, Action<int> viewMember = null)
        {
            GachaPanel panel = Create(contentHost, gameModel, gachaService, true, null, message);
            panel.onSigned = signed;
            panel.onViewMember = viewMember;
            return panel;
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
        public int InterviewPoolIndex => interviewPoolIndex;
        public int InterviewCandidateIndex => interviewCandidateIndex;

        // ------------------------------------------------------------------ build

        private void Build()
        {
            kit = new PanelKit("Gacha");

            if (embeddedMode)
            {
                BuildInterview();
                return;
            }

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

        private void BuildInterview()
        {
            displayedInterviewCycle = model.CurrentInterviewCycle;
            interviewContent = kit.NewObject("InterviewContent", transform);
            RectTransform contentRect = interviewContent.AddComponent<RectTransform>();
            PanelKit.Stretch(contentRect);

            // 2026-11-07: 选秀.psd layout (GachaPanel.Interview1107.cs) replaces the 038 authored board.
            if (Resources.Load<Sprite>(InterviewArt1107 + "iv-board") != null)
            {
                BuildInterview1107(contentRect);
                return;
            }

            Sprite authoredBoard = ReferenceArt038.Load("Art/Reference038/audition-board-038");
            if (authoredBoard != null)
            {
                BuildAuthoredInterview(contentRect, authoredBoard);
                return;
            }

            Image shade = kit.NewImage("InterviewShade", interviewContent.transform,
                kit.CreateGradientSprite("InterviewShade", new Color32(6, 8, 29, 232),
                    new Color32(8, 12, 42, 184), new Color32(6, 8, 29, 218)), Color.white);
            PanelKit.Stretch(shade.rectTransform);

            Canvas.ForceUpdateCanvases();
            bool compact = contentRect.rect.height > 1f && contentRect.rect.height < CompactInterviewHeight;
            BuildInterviewTabs(interviewContent.transform, compact);

            List<int> candidates = InterviewCandidates();
            if (candidates.Count == 0)
            {
                BuildInterviewComplete(interviewContent.transform);
                return;
            }

            interviewCandidateIndex = Mathf.Clamp(interviewCandidateIndex, 0, candidates.Count - 1);
            int memberIndex = candidates[interviewCandidateIndex];
            int previousIndex = candidates[(interviewCandidateIndex - 1 + candidates.Count) % candidates.Count];
            int nextIndex = candidates[(interviewCandidateIndex + 1) % candidates.Count];

            BuildInterviewSideCard(interviewContent.transform, "PreviousCandidate", previousIndex,
                -62f, compact ? 160f : 246f, -1, candidates.Count > 1);
            BuildInterviewSideCard(interviewContent.transform, "NextCandidate", nextIndex,
                606f, compact ? 160f : 246f, 1, candidates.Count > 1);
            BuildInterviewFigure(interviewContent.transform, memberIndex, compact);
            BuildInterviewCandidateCard(interviewContent.transform, memberIndex, candidates.Count, compact);
            AttachCandidateSwipe1102(contentRect, new Rect(45f, compact ? 116f : 156f, 630f, compact ? 620f : 684f));
            BuildInterviewActions(interviewContent.transform, memberIndex, compact);
        }

        private void BuildAuthoredInterview(RectTransform host, Sprite board)
        {
            // One source-pixel coordinate system for art, live labels and hit regions.
            // The embedded shell excludes 104px at top and 142px at bottom.
            Canvas.ForceUpdateCanvases();
            float pageWidth = host.rect.width > 1 ? host.rect.width : 720f;
            float pageHeight = host.rect.height > 1 ? host.rect.height + 246f : 1536f;
            float scale = Mathf.Min(pageWidth / board.rect.width, pageHeight / board.rect.height);
            RectTransform page = kit.NewRect("AuthoredInterviewPage", host);
            PanelKit.PlaceTop(page, (pageWidth - board.rect.width * scale) * .5f,
                -104f + (pageHeight - board.rect.height * scale) * .5f, board.rect.width, board.rect.height);
            page.localScale = Vector3.one * scale;
            Image art = kit.NewImage("AuditionReferenceBoard", page, board, Color.white);
            ChoSiren.UI.PageAmbient.Set(gameObject, board, featherEdges: art.rectTransform);
            PanelKit.Stretch(art.rectTransform);
            art.preserveAspect = true;
            art.raycastTarget = false;

            GameObject pool0 = InterviewBoardHotspot("InterviewPool-0", page, 48, 185, 400, 120,
                () => SelectInterviewPool(0));
            GameObject pool1 = InterviewBoardHotspot("InterviewPool-1", page, 480, 185, 350, 120,
                () => SelectInterviewPool(1));
            // 2026-11-01 visual-b: channel subtitles + refresh line drawn as icons (GachaPanel.Audition1101.cs).
            Transform poolSubtitle0 = AuditionPoolSubtitle1101(page, 0, 95f, 252f);
            Transform poolSubtitle1 = AuditionPoolSubtitle1101(page, 1, 490f, 252f);
            AuditionRefresh1101(page);
            // Selection marker: a saturated underline plus a soft radial glow under the
            // active plaque — unmistakable at 720w while the baked tab art stays untouched.
            Color channelColor = interviewPoolIndex == 0 ? PanelKit.Cyan : new Color32(202, 151, 255, 255);
            float channelX = interviewPoolIndex == 0 ? 147f : 542f;
            Image selGlow = ReferencePunkFx.Deco(page, "SelectedChannelGlow",
                ReferencePunkFx.Glow(), new Color(channelColor.r, channelColor.g, channelColor.b, .45f),
                channelX - 37f, 276f, 300f, 40f);
            Image selected = kit.NewImage("SelectedInterviewChannel", page, null, channelColor);
            PanelKit.PlaceTop(selected.rectTransform, channelX, 290, 226, 10);
            selected.raycastTarget = false;
            // Tab plaques are baked into the board: bind each hit to its art region so the
            // whole plaque reacts; the live subtitle and selection marker ride along as
            // in-place extras (they keep their page parent and Find paths).
            BakedRegionMotion038.Attach(pool0, art, new Rect(30f, 172f, 430f, 158f),
                poolSubtitle0.transform, interviewPoolIndex == 0 ? selected.transform : null,
                interviewPoolIndex == 0 ? selGlow.transform : null);
            BakedRegionMotion038.Attach(pool1, art, new Rect(458f, 172f, 390f, 158f),
                poolSubtitle1.transform, interviewPoolIndex == 1 ? selected.transform : null,
                interviewPoolIndex == 1 ? selGlow.transform : null);

            List<int> candidates = InterviewCandidates();
            if (candidates.Count == 0) { BuildInterviewComplete(page, 70f, 520f); return; }
            interviewCandidateIndex = Mathf.Clamp(interviewCandidateIndex, 0, candidates.Count - 1);
            int memberIndex = candidates[interviewCandidateIndex];
            MemberDefinition member = GameModel.Members[memberIndex];
            string career = InterviewCareer(member, memberIndex);
            Image portrait = kit.NewImage("CandidatePortrait", page, CandidateSprite(member), Color.white);
            PanelKit.PlaceTop(portrait.rectTransform, 400, 370, 453, 969);
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;

            BuildAuthoredCandidateCard(page, member, memberIndex, career);
            AttachCandidateSwipe1102(page, new Rect(45f, 370f, 800f, 860f));
            BuildAuthoredCandidateNavigation(page, candidates.Count, art);
            BuildAuthoredContract(page, member, memberIndex, career, art);
        }

        /// <summary>
        /// 姓名/档案/五维整组挂在 CandidateCard 下并随授权斜卡 -3.5° 微倾，使文字行与
        /// 属性条与卡面平行；全部元素留在暗色卡面安全区内。
        /// </summary>
        private void BuildAuthoredCandidateCard(RectTransform page, MemberDefinition member,
            int memberIndex, string career)
        {
            RectTransform card = kit.NewRect("CandidateCard", page);
            PanelKit.PlaceTop(card, 55, 430, 390, 800);
            ReferencePunkFx.Tilt(card, -3.5f);
            Image face = kit.NewImage("CandidateStickerFace", card, null, SirenStickerUi.Ink);
            PanelKit.Stretch(face.rectTransform);
            face.raycastTarget = false;
            SirenStickerUi.SkinPanel(face.gameObject);
            Text candidateName = kit.NewPlacedText(card, member.Name, 50, PanelKit.White, 22, 58, 330, 84,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            candidateName.name = "CandidateName";
            SirenStickerUi.SkinTitle(candidateName, true);
            // 2026-11-01 visual-b: identity as race badge + role chip + position chip; the 舞台特质 line,
            // the 舞台五维 title and the three management sentences became icons (GachaPanel.Audition1101.cs).
            AuditionIdentity1101(card, member, memberIndex, career, 150f);

            CandidateStats(member, memberIndex, interviewPoolIndex, out int vocal, out int rhythm,
                out int dance, out int fame, out int beauty);
            string[] keys = { "Vocal", "Rhythm", "Presence", "Resonance", "Charm" };
            string[] labels = { "声乐", "说唱", "舞蹈", "名气", "颜值" };
            int[] values = { vocal, rhythm, dance, fame, beauty };
            var glyphs = new[]
            {
                ReferencePunkFx.StatGlyphKind.Wave, ReferencePunkFx.StatGlyphKind.Note,
                ReferencePunkFx.StatGlyphKind.Spark, ReferencePunkFx.StatGlyphKind.Mic,
                ReferencePunkFx.StatGlyphKind.Gem,
            };
            int strongest = 0;
            for (int i = 1; i < values.Length; i++) if (values[i] > values[strongest]) strongest = i;
            for (int i = 0; i < keys.Length; i++)
            {
                float y = 250 + i * 64;
                bool lead = i == strongest;
                Color accent = lead ? (Color)Brand1025.Teal : i == 4 ? PanelKit.Cyan : PanelKit.Purple;
                Image icon = kit.NewImage("StatIcon-" + keys[i], card,
                    ReferencePunkFx.StatGlyph(glyphs[i]),
                    lead ? accent : new Color32(178, 166, 224, 235));
                PanelKit.PlaceTop(icon.rectTransform, 22, y + 2, 28, 28);
                icon.raycastTarget = false;
                kit.NewPlacedText(card, labels[i], 20, lead ? accent : PanelKit.Muted,
                    58, y, 80, 32, TextAnchor.MiddleLeft,
                    lead ? FontStyle.Bold : FontStyle.Normal).name = "StatLabel-" + keys[i];
                if (lead)
                {
                    Image star = VisualB1101.NeonIcon(card, "StrongestStat", "star", PanelKit.Gold, 136, y + 2, 28);
                    VisualB1101.Pulse(star, 1.2f, .55f);
                }
                kit.NewPlacedText(card, values[i].ToString(), 24, PanelKit.White,
                    252, y - 6, 90, 42, TextAnchor.MiddleRight, FontStyle.Bold)
                    .name = "StatValue-" + keys[i];
                Image fill = kit.NewBar("StatBar-" + keys[i], card, 58, y + 36, 284, 9,
                    new Color32(26, 20, 55, 255), accent, 0);
                fill.fillAmount = Mathf.Clamp01(values[i] / 100f);
            }

            AuditionManagement1101(card, member, memberIndex, career);
        }

        /// <summary>
        /// 契约条与「签约」大按钮已烘进整页美术：报价/倒计时/两个动作按实测位置叠加为
        /// 透明命中区 + 实时数值。容器命名沿用 InterviewActions 语义。
        /// </summary>
        private void BuildAuthoredContract(RectTransform page, MemberDefinition member,
            int memberIndex, string career, Image board)
        {
            RectTransform actions = kit.NewRect("InterviewActions", page);
            PanelKit.PlaceTop(actions, 15, 1400, 830, 268);
            int cost = InterviewCost(memberIndex);
            Image currency = kit.NewImage("SigningCurrency", actions,
                PanelKit.CurrencyIcon("diamond"),
                PanelKit.CurrencyIcon("diamond") != null ? Color.white : PanelKit.CurrencyColor("diamond"));
            PanelKit.PlaceTop(currency.rectTransform, 30, 30, 46, 46);
            currency.preserveAspect = true; currency.raycastTarget = false;
            kit.NewPlacedText(actions, cost.ToString("N0"), 38, PanelKit.Cyan,
                84, 24, 160, 58, TextAnchor.MiddleLeft, FontStyle.Bold).name = "SigningPrice";
            TimeSpan refreshIn = model.TimeUntilNextInterviewCycle();
            VisualB1101.NeonIcon(actions, "CountdownIcon", "clock", PanelKit.Muted, 32, 104, 26, false);
            kit.NewPlacedText(actions, $"{(int)refreshIn.TotalHours:00}:{refreshIn.Minutes:00}",
                20, PanelKit.Muted, 66, 100, 160, 34, TextAnchor.MiddleLeft, FontStyle.Bold)
                .name = "CandidateRefreshCountdown";

            string detailLabel = interviewPoolIndex == 0 ? "查看视频面试" : "开始现场面试";
            GameObject view = InterviewBoardHotspot("ViewInterview", actions, 253, 100, 195, 105,
                () => Notify($"{member.Name}：{InterviewTrait(career, memberIndex)}，战斗定位：{InterviewPosition(career, memberIndex)}。"));
            Text viewLabel = PanelKit.LabelOf(view);
            if (viewLabel != null) { viewLabel.text = detailLabel; viewLabel.color = Color.clear; }
            GameObject sign = InterviewBoardHotspot("SignCandidate", actions, 455, 10, 360, 218,
                () => SignInterviewCandidate(memberIndex, cost));
            // Both contract buttons are baked art: the hex button and the 签约 burst move as
            // whole bodies clipped to their traced silhouettes, while the fixed hit rects
            // stay put. The sign burst's left shard (x432) and the hex's right tip share a
            // ~25px occluded strip — each keeps its own contour intact.
            BakedRegionMotion038.Attach(view, board, new Rect(256f, 1508f, 228f, 96f), 3f,
                AuditionViewSilhouette);
            BakedRegionMotion038.Attach(sign, board, new Rect(430f, 1386f, 440f, 296f), 3f,
                AuditionSignSilhouette);
        }

        private GameObject InterviewBoardHotspot(string name, Transform parent, float x, float y,
            float width, float height, UnityEngine.Events.UnityAction action)
        {
            GameObject hit = kit.NewButton(name, parent, string.Empty, 12, Color.clear, PanelKit.White, action, 0);
            PanelKit.PlaceTop(hit.GetComponent<RectTransform>(), x, y, width, height);
            hit.GetComponent<Button>().transition = Selectable.Transition.None;
            hit.AddComponent<LobbyHotspotFeedback>().Configure(name == "SignCandidate"
                ? LobbyHotspotFeedback.VisualKind.CallToAction : LobbyHotspotFeedback.VisualKind.Entry);
            return hit;
        }

        /// <summary>
        /// 上一位/计数/下一位三块铭牌已烘进授权整页美术（源像素约 y1250–1419）。
        /// 这里只叠真实命中区、方向高光 chevron 和实时计数，不再画任何底板。
        /// </summary>
        private void BuildAuthoredCandidateNavigation(RectTransform page, int count, Image board)
        {
            RectTransform strip = kit.NewRect("CandidateNavigation", page);
            PanelKit.PlaceTop(strip, 30, 1250, 790, 150);
            Text counter = kit.NewPlacedText(strip, $"{interviewCandidateIndex + 1} / {count}",
                34, PanelKit.White, 330, 48, 160, 68, TextAnchor.MiddleCenter, FontStyle.Bold);
            counter.name = "CandidateCounter"; counter.raycastTarget = false;
            for (int direction = -1; direction <= 1; direction += 2)
            {
                int step = direction;
                GameObject hit = InterviewBoardHotspot(step < 0 ? "PreviousCandidate" : "NextCandidate",
                    strip, step < 0 ? 10 : 530, 5, step < 0 ? 300 : 280, 140,
                    () => MoveInterviewCandidate(step));
                hit.GetComponent<Button>().interactable = count > 1;
                Image visual = kit.NewImage("CandidateArrowVisualV2", hit.transform,
                    AuditionPolish038.Chevron(step < 0), Color.white);
                PanelKit.PlaceTop(visual.rectTransform, step < 0 ? 14 : 216, 43, 42, 54);
                visual.preserveAspect = true;
                visual.raycastTarget = false;
                visual.transform.SetAsFirstSibling();
                // Baked plaque + live chevron move as one body (TactileVisualMotion skips
                // children under a BakedRegionMotion038 host, so the arrow is not animated
                // twice); the hit rect itself never moves. The traced silhouette keeps the
                // counter's interleaved spikes static.
                BakedRegionMotion038.Attach(hit, board, step < 0
                        ? new Rect(86f, 1275f, 264f, 133f)
                        : new Rect(500f, 1263f, 240f, 139f),
                    3f, step < 0 ? AuditionPreviousSilhouette : AuditionNextSilhouette);
            }
        }

        private void BuildInterviewTabs(Transform parent, bool compact)
        {
            float top = compact ? 10f : 24f;
            float height = compact ? 70f : 88f;
            const float width = 320f;
            string[] titles = { "线上面试", "线下面试" };
            string[] subtitles = { "视频候选 · 预算较低", "当面试镜 · 预算较高" };

            for (int index = 0; index < 2; index++)
            {
                int captured = index;
                bool selected = index == interviewPoolIndex;
                Color selectedColor = index == 0 ? PanelKit.Cyan : new Color32(190, 129, 255, 255);
                GameObject tab = kit.NewButton("InterviewPool-" + index, parent, string.Empty, 16,
                    selected ? new Color32(17, 29, 66, 196) : new Color32(12, 15, 48, 112),
                    selected ? selectedColor : PanelKit.Muted, () => SelectInterviewPool(captured), 20);
                PanelKit.PlaceTop(tab.GetComponent<RectTransform>(), 36f + index * 324f, top, width, height);
                kit.AddOutline(tab, selected ? new Color(selectedColor.r, selectedColor.g, selectedColor.b, .68f)
                    : new Color32(121, 109, 170, 54), selected ? 1.5f : 1f);
                // Slanted-chip corner accents; the rect itself stays axis-aligned so the
                // compact-height layout keeps the tab inside the content bounds.
                ReferencePunkFx.CornerCuts(tab.GetComponent<RectTransform>(),
                    selected ? new Color32(255, 138, 226, 170) : new Color32(140, 120, 210, 90),
                    18f, 2.4f, 3f);

                Text title = kit.NewPlacedText(tab.transform, titles[index], compact ? 19 : 21,
                    selected ? selectedColor : new Color32(198, 190, 221, 225),
                    12, compact ? 5 : 10, width - 24, compact ? 28 : 34,
                    TextAnchor.MiddleCenter, FontStyle.Bold);
                title.name = "PoolTitle";
                Text subtitle = kit.NewPlacedText(tab.transform, subtitles[index], compact ? 12 : 13,
                    selected ? new Color32(229, 237, 255, 245) : new Color32(176, 168, 203, 205),
                    12, compact ? 35 : 46, width - 24, compact ? 22 : 24,
                    TextAnchor.MiddleCenter);
                subtitle.name = "PoolSubtitle";

                Image underline = kit.NewImage("PoolUnderline", tab.transform, null,
                    selected ? selectedColor : Color.clear);
                PanelKit.PlaceTop(underline.rectTransform, 78, compact ? 65 : 82,
                    width - 156, selected ? 3 : 1);
            }

            Text refresh = kit.NewPlacedText(parent, "每日免费刷新 · 18:00 更新", 14,
                new Color32(193, 187, 221, 225), 40, compact ? 84 : 120, 640, compact ? 30 : 34,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            refresh.name = "InterviewRefresh";
        }

        private void BuildInterviewComplete(Transform parent, float x = 80f, float y = 280f)
        {
            GameObject complete = kit.NewPanel("InterviewComplete", parent, new Color32(20, 23, 67, 188), 26);
            PanelKit.PlaceTop(complete.GetComponent<RectTransform>(), x, y, 560, 350);
            SirenStickerUi.SkinPanel(complete);
            kit.NewPlacedText(complete.transform, "本期候选已全部签约", 28, PanelKit.White,
                30, 76, 500, 52, TextAnchor.MiddleCenter, FontStyle.Bold);
            kit.NewPlacedText(complete.transform,
                $"她们已经进入成员列表。\n{model.NextInterviewRefreshLabel()}（候选每日 18:00 刷新）。", 17,
                PanelKit.Muted, 50, 146, 460, 78, TextAnchor.MiddleCenter);
            GameObject switchPool = kit.NewButton("SwitchInterviewPool", complete.transform,
                interviewPoolIndex == 0 ? "查看线下面试" : "查看线上面试", 17,
                new Color32(66, 49, 128, 210), PanelKit.White,
                () => SelectInterviewPool(1 - interviewPoolIndex), 18);
            PanelKit.PlaceTop(switchPool.GetComponent<RectTransform>(), 140, 252, 280, 58);
            SirenStickerUi.SkinButton(switchPool, true);
        }

        /// <summary>
        /// The free-standing candidate figure on the right, layered over the peek cards and
        /// behind the tilted stat card — the 2026-09-15 audition composition.
        /// </summary>
        private void BuildInterviewFigure(Transform parent, int memberIndex, bool compact)
        {
            MemberDefinition member = GameModel.Members[memberIndex];
            ReferencePunkFx.Deco(parent, "CandidateAura", ReferencePunkFx.Glow(),
                interviewPoolIndex == 0 ? new Color32(88, 154, 255, 54) : new Color32(190, 92, 255, 50),
                300f, compact ? 120f : 150f, 420f, 560f);

            // Thin neon beams behind the figure echo the crystal spikes in the reference.
            ReferencePunkFx.Beam(parent, "FigureBeam-A", new Vector2(392f, compact ? 220f : 250f),
                new Vector2(668f, compact ? 92f : 122f), 2.5f, new Color32(122, 214, 255, 96));
            ReferencePunkFx.Beam(parent, "FigureBeam-B", new Vector2(676f, compact ? 560f : 600f),
                new Vector2(520f, compact ? 640f : 700f), 2.5f, new Color32(255, 108, 210, 82));

            Image portrait = kit.NewImage("CandidatePortrait", parent, CandidateSprite(member), Color.white);
            PanelKit.PlaceTop(portrait.rectTransform, 342f, compact ? 116f : 156f, 378f, 520f);
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;
        }

        private void BuildInterviewSideCard(Transform parent, string name, int memberIndex,
            float x, float y, int direction, bool enabled)
        {
            MemberDefinition member = GameModel.Members[memberIndex];
            GameObject side = kit.NewButton(name, parent, string.Empty, 12,
                new Color32(25, 20, 70, enabled ? (byte)148 : (byte)72), PanelKit.White,
                enabled ? (UnityEngine.Events.UnityAction)(() => MoveInterviewCandidate(direction)) : null, 24);
            PanelKit.PlaceTop(side.GetComponent<RectTransform>(), x, y, 176, 568);
            // Fan the peek cards outward a few degrees like the reference's stacked deck.
            ReferencePunkFx.Tilt(side.GetComponent<RectTransform>(), direction < 0 ? 4f : -4f);
            kit.AddOutline(side, new Color32(153, 97, 228, enabled ? (byte)92 : (byte)36), 1);

            Image portraitFrame = kit.NewImage("PortraitFrame", side.transform, null, Color.clear);
            PanelKit.PlaceTop(portraitFrame.rectTransform, 8, 16, 160, 474);
            portraitFrame.gameObject.AddComponent<RectMask2D>();
            Image portrait = kit.NewImage("Portrait", portraitFrame.transform, CandidateSprite(member),
                enabled ? new Color(0.72f, 0.67f, 0.90f, 0.58f) : new Color(0.45f, 0.43f, 0.58f, 0.3f));
            portrait.preserveAspect = true;
            PanelKit.FrameBustPortrait(portrait, portraitFrame.rectTransform, member.Id);
            kit.NewPlacedText(side.transform, direction < 0 ? "‹" : "›", 42,
                enabled ? PanelKit.Muted : new Color32(120, 116, 145, 120),
                direction < 0 ? 100 : 10, 494, 66, 58, TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        private void BuildInterviewCandidateCard(Transform parent, int memberIndex, int candidateCount,
            bool compact)
        {
            MemberDefinition member = GameModel.Members[memberIndex];
            string career = InterviewCareer(member, memberIndex);
            CandidateStats(member, memberIndex, interviewPoolIndex, out int vocal, out int rhythm,
                out int presence, out int resonance, out int charm);

            float cardTop = compact ? 118f : 166f;
            float cardHeight = compact ? 620f : 684f;
            // A second tilted plate behind the card gives the stacked punk frame from the
            // reference without disturbing the measured card rect itself.
            Image backPlate = ReferencePunkFx.Deco(parent, "CandidateBackPlate", null,
                new Color32(16, 9, 44, 168), 44f, cardTop - 12f, 400f, cardHeight + 24f);
            ReferencePunkFx.Tilt(backPlate.rectTransform, -6.2f);

            GameObject card = kit.NewPanel("CandidateCard", parent, new Color32(9, 11, 42, 242), 18);
            RectTransform cardRect = card.GetComponent<RectTransform>();
            PanelKit.PlaceTop(cardRect, 54, cardTop, 384, cardHeight);
            kit.AddOutline(card, interviewPoolIndex == 0
                ? new Color32(91, 215, 255, 176)
                : new Color32(190, 129, 255, 158), 1.5f);
            ReferencePunkFx.CornerCuts(cardRect,
                interviewPoolIndex == 0 ? new Color32(104, 224, 255, 220) : new Color32(216, 150, 255, 210),
                30f, 3f, 5f);
            ReferencePunkFx.Tilt(cardRect, -3.5f);
            SirenStickerUi.SkinPanel(card);

            Text index = kit.NewPlacedText(card.transform,
                $"{interviewCandidateIndex + 1:00} / {candidateCount:00}", 18,
                interviewPoolIndex == 0 ? PanelKit.Cyan : new Color32(202, 151, 255, 255),
                20, compact ? 12 : 14, 140, 28, TextAnchor.MiddleLeft, FontStyle.Bold);
            index.name = "CandidateCounter";

            Text name = kit.NewPlacedText(card.transform, member.Name, compact ? 36 : 40,
                PanelKit.White, 20, compact ? 42 : 46, 250, compact ? 54 : 60,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            name.name = "CandidateName";
            kit.AddOutline(name.gameObject, new Color32(255, 96, 208, 130), 1.5f);

            BuildInterviewChip(card.transform, InterviewShortRace(member, memberIndex), 20,
                compact ? 106 : 116);
            BuildInterviewChip(card.transform, career, 120, compact ? 106 : 116);

            // Single dense line keeps race + career + battle position together.
            Text identity = kit.NewPlacedText(card.transform,
                $"{InterviewShortRace(member, memberIndex)} · {career} · 战斗定位：{InterviewPosition(career, memberIndex)}",
                15, new Color32(225, 220, 245, 255), 20, compact ? 144 : 154, 344, 24,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            identity.name = "CandidateIdentity";
            PanelKit.EnableBestFit(identity, 12);

            Text trait = kit.NewPlacedText(card.transform, InterviewTrait(career, memberIndex), 15,
                new Color32(205, 167, 255, 255), 20, compact ? 170 : 182, 344, 26,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            trait.name = "CandidateTrait";

            kit.NewPlacedText(card.transform, "舞台四维", 17, PanelKit.White,
                20, compact ? 204 : 220, 150, 28, TextAnchor.MiddleLeft, FontStyle.Bold)
                .name = "StatsTitle";
            Image statsLine = kit.NewImage("StatsTitleLine", card.transform, null,
                new Color32(255, 105, 212, 120));
            PanelKit.PlaceTop(statsLine.rectTransform, 168, compact ? 219 : 235, 180, 1.5f);

            GameObject statVeil = kit.NewPanel("CandidateStats", card.transform,
                new Color32(6, 8, 32, 172), 14);
            PanelKit.PlaceTop(statVeil.GetComponent<RectTransform>(), 16,
                compact ? 234 : 252, 352, compact ? 228 : 252);

            int[] values = { vocal, rhythm, presence, resonance, charm };
            // 09-15 参考图的可见标签；StatValue-*/StatBar-* 节点名沿用旧四维键，
            // 保证按节点名取值的既有检查仍然命中间一个真实属性。
            string[] labels = { "声乐", "说唱", "舞蹈", "名气", "颜值" };
            string[] nodeKeys = { "声能", "律动", "气场", "共鸣", "颜值" };
            int strongest = 0;
            for (int stat = 1; stat < values.Length; stat++)
                if (values[stat] > values[strongest]) strongest = stat;
            for (int stat = 0; stat < values.Length; stat++)
                BuildInterviewStat(statVeil.transform, nodeKeys[stat], labels[stat], values[stat],
                    stat, stat == strongest, compact ? 44f : 46f);

            float dividerY = compact ? 474f : 516f;
            Image divider = kit.NewImage("ManagementDivider", card.transform, null,
                new Color32(153, 133, 204, 64));
            PanelKit.PlaceTop(divider.rectTransform, 20, dividerY, 344, 1);
            int benefit = Mathf.Clamp((charm - 50) / 2, 8, 24);
            Text management = kit.NewPlacedText(card.transform,
                $"魅力 {charm} · 演出收益 +{benefit}%", 15,
                new Color32(229, 217, 249, 255), 20, dividerY + 10, 212, 26,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            management.name = "CandidateCharm";

            // 风险值：线上低价池风险更高，线下试镜更稳，让报价差有实际含义。
            int risk = model.InterviewRisk(interviewPoolIndex, memberIndex);
            string riskTier = GameModel.InterviewRiskLabel(risk);
            Color32 riskColor = riskTier == "高"
                ? new Color32(255, 108, 128, 255)
                : riskTier == "中" ? new Color32(255, 196, 92, 255) : new Color32(104, 226, 160, 255);
            Text riskText = kit.NewPlacedText(card.transform,
                $"风险值 {risk} · {riskTier}", 15, riskColor, 234, dividerY + 10, 130, 26,
                TextAnchor.MiddleRight, FontStyle.Bold);
            riskText.name = "CandidateRisk";
            Image riskTrack = kit.NewBar("CandidateRiskBar", card.transform, 20, dividerY + 42, 344, 6,
                new Color32(83, 78, 126, 86), riskColor, 6);
            riskTrack.fillAmount = risk / 100f;

            // 签约前明示预估月薪：签约后按 7 天经营期从星光币扣除（见成员档案「经纪经营」）。
            Text salaryText = kit.NewPlacedText(card.transform,
                $"预估月薪 {model.EstimatedMonthlySalary(memberIndex):N0} 星光币/期（7 天一期）",
                12, new Color32(255, 210, 117, 255), 20, dividerY + 54, 344, 24,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            salaryText.name = "CandidateSalary";
            PanelKit.EnableBestFit(salaryText, 9);

            // Bottom-edge neon underline closes the tilted card.
            Image footerLine = kit.NewImage("CandidateFooterLine", card.transform, null,
                interviewPoolIndex == 0 ? new Color32(91, 215, 255, 130) : new Color32(255, 105, 212, 120));
            PanelKit.PlaceTop(footerLine.rectTransform, 20, cardHeight - 24, 150, 2);
        }

        /// <summary>Small outlined tag chip under the candidate name （种族简称 / 定位职业）.</summary>
        private void BuildInterviewChip(Transform parent, string text, float x, float y)
        {
            GameObject chip = kit.NewPanel("CandidateChip", parent, new Color32(20, 16, 56, 160), 8);
            PanelKit.PlaceTop(chip.GetComponent<RectTransform>(), x, y, 92, 28);
            kit.AddOutline(chip, new Color32(169, 141, 245, 110), 1f);
            kit.NewPlacedText(chip.transform, text, 13, new Color32(226, 216, 252, 255),
                4, 1, 84, 26, TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        /// <summary>Race names can carry a sub-line like 「魔族 · 恶魔」; the chip keeps the head.</summary>
        private static string InterviewShortRace(MemberDefinition member, int memberIndex)
        {
            string race = InterviewRace(member, memberIndex);
            int split = race.IndexOf('·');
            return split > 0 ? race.Substring(0, split).Trim() : race;
        }

        /// <summary>One neon stat row: glyph icon, label, right-aligned value, fill bar.
        /// <paramref name="nodeKey"/> names the StatValue-*/StatBar-* nodes and stays on the
        /// legacy four-dimension vocabulary so value lookups keep resolving.</summary>
        private void BuildInterviewStat(Transform parent, string nodeKey, string label, int value,
            int row, bool strongest, float spacing)
        {
            float y = 12f + row * spacing;
            Color accent = strongest ? new Color32(255, 120, 216, 255) : new Color32(157, 117, 234, 255);
            Image icon = kit.NewImage("StatIcon-" + nodeKey, parent,
                ReferencePunkFx.StatGlyph((ReferencePunkFx.StatGlyphKind)row),
                strongest ? accent : new Color32(150, 138, 205, 235));
            PanelKit.PlaceTop(icon.rectTransform, 18, y + 1, 24, 24);
            kit.NewPlacedText(parent, label, 14, strongest ? accent : PanelKit.Muted,
                50, y, 56, 26, TextAnchor.MiddleLeft, strongest ? FontStyle.Bold : FontStyle.Normal);
            Text valueText = kit.NewPlacedText(parent, value.ToString(), 17,
                strongest ? accent : PanelKit.White,
                246, y, 48, 26, TextAnchor.MiddleRight, FontStyle.Bold);
            valueText.name = "StatValue-" + nodeKey;
            Image fill = kit.NewBar("StatBar-" + nodeKey, parent, 50, y + 30, 296, 7,
                new Color32(83, 78, 126, 86), accent, 6);
            fill.fillAmount = value / 100f;
            if (strongest)
            {
                GameObject chip = kit.NewPanel("StrongestStat", parent, new Color32(122, 26, 96, 215), 9);
                PanelKit.PlaceTop(chip.GetComponent<RectTransform>(), 300, y, 48, 26);
                kit.NewPlacedText(chip.transform, "天花板", 11, new Color32(255, 196, 235, 255),
                    1, 0, 46, 26, TextAnchor.MiddleCenter, FontStyle.Bold);
            }
        }

        private void BuildInterviewActions(Transform parent, int memberIndex, bool compact)
        {
            MemberDefinition member = GameModel.Members[memberIndex];
            string career = InterviewCareer(member, memberIndex);
            int cost = InterviewCost(memberIndex);
            GameObject action = kit.NewPanel("InterviewActions", parent, new Color32(10, 13, 46, 218), 20);
            PanelKit.PlaceTop(action.GetComponent<RectTransform>(), 34, compact ? 766 : 880,
                652, compact ? 206 : 220);
            kit.AddOutline(action, new Color32(136, 111, 213, 68), 1);
            SirenStickerUi.SkinPanel(action);
            ReferencePunkFx.CornerCuts(action.GetComponent<RectTransform>(),
                new Color32(169, 120, 255, 150), 24f, 3f, 5f);
            ReferencePunkFx.Slash(action.transform, "ActionsSlash", 636, 20, 44, 3f, -38f,
                new Color32(255, 105, 212, 140));

            Text recommend = kit.NewPlacedText(action.transform, TeamNeedsCareer(career)
                    ? $"★ 团队缺少{career} · 推荐"
                    : $"★ {InterviewRace(member, memberIndex)} · 阵容适配",
                15, new Color32(255, 210, 117, 255), 18, 12, 610, 30,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            recommend.name = "CandidateRecommendation";

            kit.NewPlacedText(action.transform, "签约报价", 14, PanelKit.Muted,
                18, 54, 150, 26, TextAnchor.MiddleLeft);
            Image currency = kit.NewImage("SigningCurrency", action.transform,
                PanelKit.CurrencyIcon("diamond"), PanelKit.CurrencyIcon("diamond") != null
                    ? Color.white : PanelKit.CurrencyColor("diamond"));
            PanelKit.PlaceTop(currency.rectTransform, 18, 86, 35, 35);
            currency.preserveAspect = true;
            Text price = kit.NewPlacedText(action.transform, cost.ToString("N0"), 30,
                PanelKit.CurrencyColor("diamond"), 60, 78, 160, 50,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            price.name = "SigningPrice";
            kit.NewPlacedText(action.transform,
                (interviewPoolIndex == 0 ? "线上候选 · 星钻报价" : "线下候选 · 星钻报价") +
                "\n" + model.NextInterviewRefreshLabel(),
                12, PanelKit.Muted, 18, 128, 226, 44, TextAnchor.MiddleLeft);

            string detailLabel = interviewPoolIndex == 0 ? "查看视频面试" : "开始现场面试";
            GameObject details = kit.NewButton("ViewInterview", action.transform, detailLabel, 15,
                new Color32(18, 22, 65, 218), PanelKit.White,
                () => Notify($"{member.Name}：{InterviewTrait(career, memberIndex)}，战斗定位：{InterviewPosition(career, memberIndex)}。"),
                16);
            PanelKit.PlaceTop(details.GetComponent<RectTransform>(), 254, 84, 174, 66);
            kit.AddOutline(details, new Color32(159, 139, 211, 98), 1);

            GameObject sign = kit.NewButton("SignCandidate", action.transform, "签约", 21,
                new Color32(92, 65, 191, 245), PanelKit.White,
                () => SignInterviewCandidate(memberIndex, cost), 18);
            PanelKit.PlaceTop(sign.GetComponent<RectTransform>(), 442, 76, 190, 82);
            kit.AddOutline(sign, interviewPoolIndex == 0
                ? new Color32(91, 215, 255, 168)
                : new Color32(255, 102, 190, 160), 1.5f);

            SirenStickerUi.SkinButton(details);
            SirenStickerUi.SkinButton(sign, true);
            BuildInterviewDots(action.transform, InterviewCandidates().Count);
        }

        private void BuildInterviewDots(Transform parent, int candidateCount)
        {
            int visible = Mathf.Min(5, candidateCount);
            float start = 326f - (visible * 18f - 8f) * .5f;
            int windowStart = Mathf.Clamp(interviewCandidateIndex - visible / 2, 0,
                Mathf.Max(0, candidateCount - visible));
            for (int index = 0; index < visible; index++)
            {
                int candidateIndex = windowStart + index;
                bool active = candidateIndex == interviewCandidateIndex;
                Image dot = kit.NewImage("CandidateDot-" + candidateIndex, parent, kit.RoundedSprite(8),
                    active ? PanelKit.Cyan : new Color32(139, 135, 171, 180));
                PanelKit.PlaceTop(dot.rectTransform, start + index * 18f, 184, active ? 10 : 8, active ? 10 : 8);
            }
        }

        private void SelectInterviewPool(int index)
        {
            if (index == interviewPoolIndex) return;
            interviewChannelPositions[interviewPoolIndex] = interviewCandidateIndex;
            interviewPoolIndex = Mathf.Clamp(index, 0, 1);
            interviewCandidateIndex = interviewChannelPositions[interviewPoolIndex];
            RebuildInterview();
        }

        private void MoveInterviewCandidate(int direction)
        {
            List<int> candidates = InterviewCandidates();
            if (candidates.Count <= 1) return;
            interviewCandidateIndex = (interviewCandidateIndex + direction + candidates.Count) % candidates.Count;
            interviewChannelPositions[interviewPoolIndex] = interviewCandidateIndex;
            RebuildInterview();
        }

        private GameObject signModal;

        private void SignInterviewCandidate(int memberIndex, int cost) => ShowSignConfirm(memberIndex, cost);

        /// <summary>
        /// 签约确认：大立绘 + 种族/定位徽章 + 星钻报价 + 余额 A→B（2026-11-01 visual-b，见
        /// GachaPanel.Sign1101.cs）。余额不足时确认键锁定并显示缺口；任何路径都不在确认前扣款或解锁。
        /// </summary>
        private void ShowSignConfirm(int memberIndex, int cost) => ShowSignConfirm1101(memberIndex, cost);

        private void ConfirmSign(int memberIndex, int cost, bool useTicket = false)
        {
            bool signed = model.SignInterviewCandidate(interviewPoolIndex, memberIndex,
                displayedInterviewCycle, cost, useTicket, out string message);
            if (!signed)
            {
                Notify(message);
                CloseSignModal();
                if (displayedInterviewCycle != model.CurrentInterviewCycle) RebuildInterview();
                return;
            }
            List<int> remaining = InterviewCandidates();
            interviewCandidateIndex = remaining.Count == 0
                ? 0
                : Mathf.Clamp(interviewCandidateIndex, 0, remaining.Count - 1);
            RebuildInterview();
            // 2026-10-21: the signing moment gets a full-screen P5 reveal before the result card.
            CloseSignModal();
            MemberDefinition signedMember = GameModel.Members[memberIndex];
            if (Application.isPlaying)
                ChoSiren.UI.SignReveal1022.Play(transform, kit.Font, CandidateSprite(signedMember), signedMember.Name,
                    $"{InterviewRace(signedMember, memberIndex)} · {InterviewCareer(signedMember, memberIndex)}",
                    () => FindAnyObjectByType<ChoSiren.GameAudio>()?.PlaySfx("stamp", .9f),
                    () => { if (this != null) ShowSignedCard(memberIndex); });
            else ShowSignedCard(memberIndex);
        }

        /// <summary>
        /// 签约成功：立绘结果卡 + 实际等级/定位/预估月薪，可继续面试或跳转成员档案。
        /// 只在真实扣款成功后由 ConfirmSign 调用。
        /// </summary>
        private void ShowSignedCard(int memberIndex) => ShowSignedCard1101(memberIndex);

        private void CloseSignModal()
        {
            if (signModal == null) return;
            if (Application.isPlaying) Destroy(signModal);
            else DestroyImmediate(signModal);
            signModal = null;
        }

        private void RebuildInterview()
        {
            CloseSignModal();
            if (interviewContent != null)
            {
                interviewContent.SetActive(false);
                if (Application.isPlaying) Destroy(interviewContent);
                else DestroyImmediate(interviewContent);
            }
            BuildInterview();
        }

        // 2026-11-08 (少爷: "点了最底部的这张卡之后…只有第一张被换走"): tapping a back badge swaps it with the front one only,
        // so the shown order can differ from the model's; it is kept per pool while the candidate set is unchanged.
        private readonly Dictionary<int, List<int>> interviewOrder1108 = new Dictionary<int, List<int>>();

        private List<int> InterviewCandidates()
        {
            var fresh = new List<int>(model.InterviewCandidates(interviewPoolIndex));
            if (interviewOrder1108.TryGetValue(interviewPoolIndex, out List<int> order)
                && order.Count == fresh.Count && order.TrueForAll(fresh.Contains))
                return new List<int>(order);
            interviewOrder1108.Remove(interviewPoolIndex);
            return fresh;
        }

        /// <summary>Swaps the candidate <paramref name="steps"/> behind the front one into the front (the others keep their places).</summary>
        private void SwapInterviewCandidate1108(int steps)
        {
            List<int> order = InterviewCandidates();
            if (order.Count <= 1 || steps <= 0) return;
            int a = interviewCandidateIndex, b = (interviewCandidateIndex + steps) % order.Count;
            (order[a], order[b]) = (order[b], order[a]);
            interviewOrder1108[interviewPoolIndex] = order;
            RebuildInterview();
        }

        private int InterviewCost(int memberIndex)
        {
            return model.InterviewQuote(interviewPoolIndex, memberIndex);
        }

        private void Update()
        {
            if (!embeddedMode || model == null || Time.unscaledTime < nextInterviewRefreshCheck) return;
            nextInterviewRefreshCheck = Time.unscaledTime + 1f;
            if (displayedInterviewCycle == model.CurrentInterviewCycle) return;
            Array.Clear(interviewChannelPositions, 0, interviewChannelPositions.Length);
            interviewCandidateIndex = 0;
            RebuildInterview();
        }

        private static Sprite CandidateSprite(MemberDefinition member)
        {
            if (member == null) return null;
            return PanelKit.MemberSpriteOrNull(member.Id, false) ?? Resources.Load<Sprite>(member.ResourcePath);
        }

        private static string InterviewRace(MemberDefinition member, int memberIndex)
        {
            if (member != null && !string.IsNullOrWhiteSpace(member.Race)) return member.Race.Trim();
            string[] races = { "魅族", "魔族 · 恶魔", "海灵族 · 人鱼", "血精灵" };
            return races[Mathf.Abs(memberIndex) % races.Length];
        }

        private static string InterviewCareer(MemberDefinition member, int memberIndex)
        {
            return member == null ? "未指定" : member.Career;
        }

        private string InterviewPosition(string career, int memberIndex)
        {
            // 2026-10-20: battles act by race (design doc 四大定位), so the label follows the race.
            switch (MemberCaptainTraits.Resolve(InterviewRace(GameModel.Members[memberIndex], memberIndex)))
            {
                case "charm": return "连击输出";
                case "demon": return "持续毒伤";
                case "mermaid": return "治疗 / 护盾";
                case "bloodelf": return "爆发斩杀";
                default: return MemberBattlePresentation.Position(model.Tactics, GameModel.Members[memberIndex].Id);
            }
        }

        /// <summary>
        /// 面试素质来自舞台四维（声能/律动/气场/共鸣），与战斗技能/属性是两套概念，
        /// 不再把战斗技能名当面试特质。
        /// </summary>
        private string InterviewTrait(string career, int memberIndex)
        {
            CandidateStats(GameModel.Members[memberIndex], memberIndex, interviewPoolIndex,
                out int vocal, out int rhythm, out int presence, out int resonance, out _);
            return MemberStageQualities.Describe(vocal, rhythm, presence, resonance);
        }

        private void CandidateStats(MemberDefinition member, int memberIndex, int poolIndex,
            out int vocal, out int rhythm, out int presence, out int resonance, out int charm)
            // 预览真实签约起点；线下等级加成与线上等级偏移在签约时落入存档。
            => GameModel.StageStats(member, memberIndex, poolIndex, model.PreviewSigningLevel(memberIndex, poolIndex),
                out vocal, out rhythm, out presence, out resonance, out charm);

        private bool TeamNeedsCareer(string candidateCareer)
        {
            List<int> team = model.Save.Team;
            for (int index = 0; index < team.Count; index++)
            {
                int memberIndex = team[index];
                if (memberIndex < 0 || memberIndex >= GameModel.Members.Length) continue;
                if (InterviewCareer(GameModel.Members[memberIndex], memberIndex) == candidateCareer) return false;
            }
            return true;
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
            kit.NewPlacedText(transform, "签约中心", 25, PanelKit.White, 112, 16, 210, 46,
                TextAnchor.MiddleLeft, FontStyle.Bold);
        }

        private void BuildBannerTabs()
        {
            IReadOnlyList<GachaBannerDefinition> banners = service.Banners;
            int count = banners == null ? 0 : banners.Count;
            if (count == 0)
            {
                kit.NewPlacedText(transform, "暂无开放中的签约企划", 16, PanelKit.Muted, 20,
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
            bannerKind = kit.NewPlacedText(caption.transform, "成员签约", 13,
                new Color32(255, 173, 226, 255), 34, 16, 350, 24, TextAnchor.MiddleLeft, FontStyle.Bold);
            bannerTitle = kit.NewPlacedText(caption.transform, string.Empty, 30, PanelKit.White,
                34, 38, 350, 44, TextAnchor.MiddleLeft, FontStyle.Bold);
            featuredText = kit.NewPlacedText(caption.transform, string.Empty, 16, PanelKit.Gold,
                34, 80, 350, 28, TextAnchor.MiddleLeft, FontStyle.Bold);
            kit.NewPlacedText(caption.transform, "重复候选转化为碎片 · 十连至少触发一次重点邀约", 12, PanelKit.Muted,
                34, 108, 350, 24, TextAnchor.MiddleLeft);
        }

        private void BuildRates()
        {
            GameObject panel = kit.NewPanel("RateBoard", transform, new Color32(8, 11, 44, 116), 16);
            PanelKit.PlaceTop(panel.GetComponent<RectTransform>(), 166, embeddedMode ? 914 : 1052, 536, 62);
            AddAiUiSkin(panel, "gacha-rates-panel-ai-v2", Color.white);
            kit.AddOutline(panel, new Color32(151, 119, 255, 72), 1);
            kit.NewPlacedText(panel.transform, "邀约概率", 13, new Color32(255, 173, 226, 255),
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
            kit.NewPlacedText(panel.transform, "重点邀约", 13, new Color32(255, 173, 226, 255),
                26, 0, 82, 84, TextAnchor.MiddleLeft, FontStyle.Bold);
            pityText = kit.NewPlacedText(panel.transform, string.Empty, 13, PanelKit.White,
                102, 5, 420, 27, TextAnchor.MiddleLeft, FontStyle.Bold);
            pityFill = kit.NewBar("PityBar", panel.transform, 102, 35, 250, 9,
                new Color32(66, 54, 117, 255), PanelKit.Pink, 9);
            totalsText = kit.NewPlacedText(panel.transform, string.Empty, 12, PanelKit.Muted,
                102, 48, 286, 27, TextAnchor.MiddleLeft);
            guaranteeChip = kit.NewPanel("GuaranteeChip", panel.transform, new Color32(255, 205, 96, 235), 12);
            PanelKit.PlaceTop(guaranteeChip.GetComponent<RectTransform>(), 392, 44, 130, 28);
            kit.NewPlacedText(guaranteeChip.transform, "限定邀约已激活", 12,
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

            hintText = kit.NewPlacedText(transform, "选择心仪企划，与舞台上的她签订契约", 13,
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
            resultSummary.name = "ResultSummary";

            for (int index = 0; index < GachaEngine.TenPullCount; index++)
                resultCells.Add(BuildResultCell(overlay.transform, index));

            pullAgainButton = kit.NewButton("PullAgain", overlay.transform, "再来十连", 21, PanelKit.Pink,
                PanelKit.White, PullAgain, 24);
            PanelKit.PlaceTop(pullAgainButton.GetComponent<RectTransform>(), 60, 1000, 290, 74);
            SirenStickerUi.SkinButton(pullAgainButton, true);   // unified design: the shared teal primary
            GameObject back = kit.NewButton("ResultBack", overlay.transform, "返回", 21, PanelKit.ButtonDark,
                PanelKit.White, CloseResult, 24);
            PanelKit.PlaceTop(back.GetComponent<RectTransform>(), 370, 1000, 290, 74);
            SirenStickerUi.SkinButton(back);
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

            Image portraitFrame = kit.NewImage("PortraitFrame", root.transform, null, Color.clear);
            PanelKit.PlaceTop(portraitFrame.rectTransform, 10, 30, 100, 92);
            portraitFrame.gameObject.AddComponent<RectMask2D>();
            Image portrait = kit.NewImage("Portrait", portraitFrame.transform, null, PanelKit.White);
            PanelKit.FitInsideFrame(portrait);

            Text profile = kit.NewPlacedText(root.transform, "候选资料", 10, PanelKit.White, 6, 4, 72, 30,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            profile.name = "CandidateProfile";
            GameObject badge = kit.NewPanel("NewBadge", root.transform, Brand1025.Teal, 8);
            PanelKit.PlaceTop(badge.GetComponent<RectTransform>(), 80, 6, 34, 22);
            kit.NewPlacedText(badge.transform, "新", 12, Brand1025.Ink, 0, 0, 34, 22, TextAnchor.MiddleCenter,
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
                PortraitFrame = portraitFrame.rectTransform,
                Portrait = portrait,
                Outline = kit.AddOutline(root, new Color32(255, 205, 96, 0), 3),
                Profile = profile,
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
                bannerTitle.text = "暂无签约企划";
                bannerKind.text = string.Empty;
                featuredText.text = string.Empty;
                rateText.text = "签约数据尚未配置";
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
            bannerKind.text = banner.Kind == GachaBannerKind.Costume ? "造型签约" : "成员签约";
            featuredText.text = FeaturedLine(banner);
            Sprite featuredSprite = ResolveBannerPortrait(banner) ?? Resources.Load<Sprite>("Art/HeroFallback");
            featuredPortrait.sprite = featuredSprite;
            featuredPortrait.enabled = featuredSprite != null;
            featuredFallback.gameObject.SetActive(false);

            rateText.text = InlineRateSummary(banner);

            GachaBannerState state = service.BannerState(banner.Id);
            int pity = state != null ? state.Pity : 0;
            int remaining = Mathf.Max(0, banner.HardPity - pity);
            pityText.text = remaining == 0 ? "下一次将触发特别邀约" : $"已累计 {pity} 次 · 再 {remaining} 次触发特别邀约";
            pityFill.fillAmount = banner.HardPity > 0 ? Mathf.Clamp01(pity / (float)banner.HardPity) : 0f;
            pityFill.color = pity >= banner.SoftPityStart ? PanelKit.Gold : (Color)Brand1025.Teal;
            totalsText.text = state != null
                ? $"累计签约 {state.TotalPulls} 次 · 特别邀约 {state.TotalSsr} 次"
                : "尚未参与此签约企划";
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
            string guarantee = banner.TenPullGuaranteesSr ? " · 十连含重点邀约" : string.Empty;
            return $"特别邀约 {PanelKit.Permille(banner.SsrRatePermille)} · " +
                   $"重点邀约 {PanelKit.Permille(banner.SrRatePermille)} · " +
                   $"常规邀约 {PanelKit.Permille(rPermille)}{guarantee}";
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
            if (banner.FeaturedItemIds.Count == 0) return "常驻候选 · 无限定成员";
            var builder = new System.Text.StringBuilder();
            for (int index = 0; index < banner.FeaturedItemIds.Count; index++)
            {
                if (index > 0) builder.Append(" · ");
                builder.Append(DisplayName(banner.FeaturedItemIds[index]));
            }

            return builder.ToString();
        }

        /// <summary>
        /// Rate table text generated purely from the banner numbers so 邀约概率 can never drift
        /// from what the engine rolls. Public so tests can assert on the wording.
        /// </summary>
        public static string RateSummary(GachaBannerDefinition banner)
        {
            if (banner == null) return string.Empty;
            int rPermille = Mathf.Max(0, 1000 - banner.SsrRatePermille - banner.SrRatePermille);
            var builder = new System.Text.StringBuilder();
            builder.Append("特别邀约 ").Append(PanelKit.Permille(banner.SsrRatePermille))
                .Append(" · 重点邀约 ").Append(PanelKit.Permille(banner.SrRatePermille))
                .Append(" · 常规邀约 ").Append(PanelKit.Permille(rPermille)).Append('\n');
            builder.Append("第 ").Append(banner.SoftPityStart).Append(" 次签约起概率提升，每次 +")
                .Append(PanelKit.Permille(banner.SoftPityStepPermille))
                .Append(" · 第 ").Append(banner.HardPity).Append(" 次触发特别邀约").Append('\n');
            if (banner.FeaturedItemIds.Count > 0 && banner.StandardSsrItemIds.Count > 0)
            {
                builder.Append("特别邀约中限定成员占 ").Append(PanelKit.Permille(banner.RateUpSharePermille));
                if (banner.GuaranteeFeaturedAfterLoss) builder.Append(" · 未遇限定后下一次特别邀约锁定限定成员");
                builder.Append('\n');
            }

            if (banner.TenPullGuaranteesSr) builder.Append("每次十连至少触发一次重点邀约 · ");
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
            builder.Append("特别邀约 ").Append(PanelKit.Permille(banner.SsrRatePermille)).Append('\n');
            builder.Append("重点邀约 ").Append(PanelKit.Permille(banner.SrRatePermille)).Append('\n');
            builder.Append("常规邀约 ").Append(PanelKit.Permille(rPermille)).Append("\n\n");
            builder.Append(banner.TenPullGuaranteesSr ? "十连至少触发重点邀约" : "概率以公示为准");
            return builder.ToString();
        }

        private string DisplayName(string itemId)
        {
            string custom = service.ItemDisplayName(itemId);
            return string.IsNullOrEmpty(custom) ? PanelKit.MemberNameOrId(itemId) : custom;
        }

        private string CandidateProfile(string itemId)
        {
            MemberDefinition[] members = GameModel.Members;
            for (int index = 0; index < members.Length; index++)
            {
                MemberDefinition member = members[index];
                if (member == null || string.IsNullOrEmpty(member.Id)) continue;
                bool exact = string.Equals(itemId, member.Id, StringComparison.OrdinalIgnoreCase);
                bool ownedItem = !string.IsNullOrEmpty(itemId) &&
                                 itemId.IndexOf("-" + member.Id + "-", StringComparison.OrdinalIgnoreCase) >= 0;
                if (!exact && !ownedItem) continue;

                string race = InterviewRace(member, index).Split('·')[0].Trim();
                string career = InterviewCareer(member, index);
                return $"{race} · {career}\n{InterviewPosition(career, index)}";
            }

            if (!string.IsNullOrEmpty(itemId) &&
                itemId.StartsWith("accessory-", StringComparison.OrdinalIgnoreCase))
                return "舞台饰品\n造型支援";
            return "签约候选\n资料待确认";
        }

        // ------------------------------------------------------------------ pulls

        private void Pull(int count)
        {
            if (pulling || closing) return;
            GachaBannerDefinition banner = CurrentBanner;
            if (banner == null)
            {
                Notify("当前没有开放中的签约企划");
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

            resultSummary.text = $"{banner.Name} · 特别邀约 {ssr} · 重点邀约 {sr} · 新成员 {fresh} · 碎片 +{shards}";
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
                PanelKit.PlaceTop(single.Profile.rectTransform, 8, 6, 124, 30);
                PanelKit.PlaceTop(single.Portrait.rectTransform, 12, 40, 156, 146);
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
                PanelKit.PlaceTop(cell.Profile.rectTransform, 6, 4, 72, 30);
                PanelKit.PlaceTop(cell.Portrait.rectTransform, 10, 34, 100, 88);
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
            cell.Profile.text = CandidateProfile(result.ItemId);
            cell.Profile.color = ssr ? PanelKit.Gold : sr ? new Color32(214, 170, 255, 255) : PanelKit.Muted;
            cell.Name.text = DisplayName(result.ItemId);
            cell.NewBadge.SetActive(result.IsNew);
            cell.Outline.effectColor = new Color(1f, 0.8f, 0.37f, 0f);

            Sprite portrait = ResolveResultPortrait(result.ItemId);
            cell.Portrait.sprite = portrait;
            cell.Portrait.enabled = portrait != null;
            string portraitMember = PanelKit.MemberIdOfItem(result.ItemId);
            if (portraitMember != null)
                PanelKit.FrameBustPortrait(cell.Portrait, cell.PortraitFrame, portraitMember);
            else
                PanelKit.FitInsideFrame(cell.Portrait);

            string footer;
            if (result.IsNew) footer = result.IsFeatured ? "限定 · 新成员" : "新成员";
            else footer = result.ShardReward > 0 ? $"碎片 +{result.ShardReward}" : "重复";
            if (result.HitHardPity) footer = "特别邀约 · " + footer;
            else if (result.UpgradedByTenPullGuarantee) footer = "重点邀约 · " + footer;
            cell.Footer.text = footer;
            cell.Footer.color = result.IsNew ? (Color)Brand1025.Teal : PanelKit.Muted;
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
