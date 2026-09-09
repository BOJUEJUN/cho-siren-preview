using System;
using System.Collections;
using System.Collections.Generic;
using ChoSiren.Systems.Gacha;
using ChoSiren.Systems.Presentation;
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
        private const float CompactInterviewHeight = 1168f;

        private sealed class ResultCell
        {
            public GameObject Root;
            public RectTransform Rect;
            public Image Background;
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
            IGachaService gachaService, Action<string> message = null, Action<int> signed = null)
        {
            GachaPanel panel = Create(contentHost, gameModel, gachaService, true, null, message);
            panel.onSigned = signed;
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
            BuildInterviewCandidateCard(interviewContent.transform, memberIndex, candidates.Count, compact);
            BuildInterviewActions(interviewContent.transform, memberIndex, compact);
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

        private void BuildInterviewComplete(Transform parent)
        {
            GameObject complete = kit.NewPanel("InterviewComplete", parent, new Color32(20, 23, 67, 188), 26);
            PanelKit.PlaceTop(complete.GetComponent<RectTransform>(), 80, 280, 560, 350);
            kit.AddOutline(complete, new Color32(153, 97, 228, 98), 1);
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
        }

        private void BuildInterviewSideCard(Transform parent, string name, int memberIndex,
            float x, float y, int direction, bool enabled)
        {
            MemberDefinition member = GameModel.Members[memberIndex];
            GameObject side = kit.NewButton(name, parent, string.Empty, 12,
                new Color32(25, 20, 70, enabled ? (byte)148 : (byte)72), PanelKit.White,
                enabled ? (UnityEngine.Events.UnityAction)(() => MoveInterviewCandidate(direction)) : null, 24);
            PanelKit.PlaceTop(side.GetComponent<RectTransform>(), x, y, 176, 568);
            kit.AddOutline(side, new Color32(153, 97, 228, enabled ? (byte)92 : (byte)36), 1);

            Image portrait = kit.NewImage("Portrait", side.transform, CandidateSprite(member),
                enabled ? new Color(0.72f, 0.67f, 0.90f, 0.58f) : new Color(0.45f, 0.43f, 0.58f, 0.3f));
            PanelKit.PlaceTop(portrait.rectTransform, 8, 16, 160, 474);
            portrait.preserveAspect = true;
            portrait.useSpriteMesh = true;
            kit.NewPlacedText(side.transform, direction < 0 ? "‹" : "›", 42,
                enabled ? PanelKit.Muted : new Color32(120, 116, 145, 120),
                direction < 0 ? 100 : 10, 494, 66, 58, TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        private void BuildInterviewCandidateCard(Transform parent, int memberIndex, int candidateCount,
            bool compact)
        {
            MemberDefinition member = GameModel.Members[memberIndex];
            string career = InterviewCareer(member, memberIndex);
            CandidateStats(member, memberIndex, out int vocal, out int rhythm, out int presence,
                out int resonance, out int charm);

            GameObject card = kit.NewPanel("CandidateCard", parent, new Color32(10, 14, 49, 236), 28);
            PanelKit.PlaceTop(card.GetComponent<RectTransform>(), 102, compact ? 126 : 172,
                516, compact ? 684 : 724);
            kit.AddOutline(card, interviewPoolIndex == 0
                ? new Color32(91, 215, 255, 164)
                : new Color32(190, 129, 255, 150), 1.5f);

            Image softGlow = kit.NewImage("CandidateGlow", card.transform, kit.RadialSprite(),
                interviewPoolIndex == 0
                    ? new Color32(58, 159, 255, 44)
                    : new Color32(177, 77, 255, 40));
            PanelKit.PlaceTop(softGlow.rectTransform, 186, 22, 340, 630);

            Text index = kit.NewPlacedText(card.transform,
                $"{interviewCandidateIndex + 1:00} / {candidateCount:00}", 19,
                interviewPoolIndex == 0 ? PanelKit.Cyan : new Color32(202, 151, 255, 255),
                24, 20, 180, 34, TextAnchor.MiddleLeft, FontStyle.Bold);
            index.name = "CandidateCounter";

            Text name = kit.NewPlacedText(card.transform, member.Name, 35, PanelKit.White,
                24, 62, 260, 54, TextAnchor.MiddleLeft, FontStyle.Bold);
            name.name = "CandidateName";

            string meta = $"{InterviewRace(member, memberIndex)}\n{career}\n战斗定位：{InterviewPosition(career, memberIndex)}";
            Text identity = kit.NewPlacedText(card.transform, meta, 16, new Color32(225, 220, 245, 255),
                24, 122, 248, 98, TextAnchor.UpperLeft, FontStyle.Bold);
            identity.name = "CandidateIdentity";

            Text trait = kit.NewPlacedText(card.transform, InterviewTrait(career, memberIndex), 15,
                new Color32(205, 167, 255, 255), 24, 226, 236, 32,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            trait.name = "CandidateTrait";

            Image portrait = kit.NewImage("CandidatePortrait", card.transform, CandidateSprite(member), Color.white);
            PanelKit.PlaceTop(portrait.rectTransform, 244, 42, 282, 616);
            portrait.preserveAspect = true;
            portrait.useSpriteMesh = true;

            GameObject statVeil = kit.NewPanel("CandidateStats", card.transform, new Color32(7, 10, 39, 194), 18);
            PanelKit.PlaceTop(statVeil.GetComponent<RectTransform>(), 18, 270, 276, 326);
            kit.NewPlacedText(statVeil.transform, "舞台四维", 16, PanelKit.White,
                14, 10, 220, 30, TextAnchor.MiddleLeft, FontStyle.Bold);

            int[] values = { vocal, rhythm, presence, resonance };
            string[] labels = { "声能", "律动", "气场", "共鸣" };
            int strongest = 0;
            for (int stat = 1; stat < values.Length; stat++)
                if (values[stat] > values[strongest]) strongest = stat;
            for (int stat = 0; stat < values.Length; stat++)
                BuildInterviewStat(statVeil.transform, labels[stat], values[stat], stat,
                    stat == strongest);

            Image divider = kit.NewImage("ManagementDivider", card.transform, null,
                new Color32(153, 133, 204, 64));
            PanelKit.PlaceTop(divider.rectTransform, 24, 620, 468, 1);
            int benefit = Mathf.Clamp((charm - 50) / 2, 8, 24);
            Text management = kit.NewPlacedText(card.transform,
                $"魅力  {charm}        演出收益  +{benefit}%", 16,
                new Color32(229, 217, 249, 255), 24, 632, 360, 40,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            management.name = "CandidateCharm";
        }

        private void BuildInterviewStat(Transform parent, string label, int value, int row, bool strongest)
        {
            float y = 50f + row * 63f;
            Color accent = strongest ? PanelKit.Cyan : new Color32(157, 117, 234, 255);
            kit.NewPlacedText(parent, label, 14, strongest ? accent : PanelKit.Muted,
                14, y, 58, 28, TextAnchor.MiddleLeft, strongest ? FontStyle.Bold : FontStyle.Normal);
            Text valueText = kit.NewPlacedText(parent, value.ToString(), 16, strongest ? accent : PanelKit.White,
                210, y, 42, 28, TextAnchor.MiddleRight, FontStyle.Bold);
            valueText.name = "StatValue-" + label;
            Image fill = kit.NewBar("StatBar-" + label, parent, 14, y + 32, 238, 8,
                new Color32(83, 78, 126, 86), accent, 8);
            fill.fillAmount = value / 100f;
            if (strongest)
            {
                GameObject chip = kit.NewPanel("StrongestStat", parent, new Color32(41, 118, 147, 205), 9);
                PanelKit.PlaceTop(chip.GetComponent<RectTransform>(), 151, y - 1, 55, 27);
                kit.NewPlacedText(chip.transform, "最强项", 12, new Color32(188, 246, 255, 255),
                    2, 0, 51, 27, TextAnchor.MiddleCenter, FontStyle.Bold);
            }
        }

        private void BuildInterviewActions(Transform parent, int memberIndex, bool compact)
        {
            MemberDefinition member = GameModel.Members[memberIndex];
            string career = InterviewCareer(member, memberIndex);
            int cost = InterviewCost(memberIndex);
            GameObject action = kit.NewPanel("InterviewActions", parent, new Color32(10, 13, 46, 218), 20);
            PanelKit.PlaceTop(action.GetComponent<RectTransform>(), 34, compact ? 820 : 944,
                652, compact ? 206 : 220);
            kit.AddOutline(action, new Color32(136, 111, 213, 68), 1);

            Text recommend = kit.NewPlacedText(action.transform, TeamNeedsCareer(career)
                    ? $"✦ 团队缺少{career} · 推荐"
                    : $"✦ {InterviewRace(member, memberIndex)} · 阵容适配",
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
            interviewPoolIndex = Mathf.Clamp(index, 0, 1);
            interviewCandidateIndex = 0;
            RebuildInterview();
        }

        private void MoveInterviewCandidate(int direction)
        {
            List<int> candidates = InterviewCandidates();
            if (candidates.Count <= 1) return;
            interviewCandidateIndex = (interviewCandidateIndex + direction + candidates.Count) % candidates.Count;
            RebuildInterview();
        }

        private void SignInterviewCandidate(int memberIndex, int cost)
        {
            bool signed = model.SignInterviewCandidate(interviewPoolIndex, memberIndex,
                displayedInterviewCycle, cost, out string message);
            Notify(message);
            if (!signed)
            {
                if (displayedInterviewCycle != model.CurrentInterviewCycle) RebuildInterview();
                return;
            }
            List<int> remaining = InterviewCandidates();
            interviewCandidateIndex = remaining.Count == 0
                ? 0
                : Mathf.Clamp(interviewCandidateIndex, 0, remaining.Count - 1);
            RebuildInterview();
            onSigned?.Invoke(memberIndex);
        }

        private void RebuildInterview()
        {
            if (interviewContent != null)
            {
                interviewContent.SetActive(false);
                if (Application.isPlaying) Destroy(interviewContent);
                else DestroyImmediate(interviewContent);
            }
            BuildInterview();
        }

        private List<int> InterviewCandidates()
        {
            return new List<int>(model.InterviewCandidates(interviewPoolIndex));
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
            return MemberBattlePresentation.Position(model.Tactics, GameModel.Members[memberIndex].Id);
        }

        /// <summary>
        /// 面试素质来自舞台四维（声能/律动/气场/共鸣），与战斗技能/属性是两套概念，
        /// 不再把战斗技能名当面试特质。
        /// </summary>
        private string InterviewTrait(string career, int memberIndex)
        {
            CandidateStats(GameModel.Members[memberIndex], memberIndex, out int vocal, out int rhythm,
                out int presence, out int resonance, out _);
            return MemberStageQualities.Describe(vocal, rhythm, presence, resonance);
        }

        private static void CandidateStats(MemberDefinition member, int memberIndex, out int vocal,
            out int rhythm, out int presence, out int resonance, out int charm)
        {
            string career = InterviewCareer(member, memberIndex);
            int powerBias = member == null ? 0 : Mathf.Clamp((member.BasePower - 6200) / 500, 0, 10);
            vocal = Mathf.Clamp(68 + memberIndex * 7 % 19 + powerBias + (career == "主唱" ? 10 : 0), 55, 98);
            rhythm = Mathf.Clamp(64 + memberIndex * 5 % 21 + powerBias + (career == "主舞" ? 11 : 0), 55, 98);
            presence = Mathf.Clamp(70 + memberIndex * 3 % 20 + powerBias + (career == "Rapper" ? 7 : 0), 55, 98);
            resonance = Mathf.Clamp(66 + memberIndex * 9 % 20 + powerBias + (career == MemberCareers.Face ? 10 : 0), 55, 98);
            charm = Mathf.Clamp(72 + memberIndex * 4 % 18 + powerBias, 65, 96);
        }

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

            Text profile = kit.NewPlacedText(root.transform, "候选资料", 10, PanelKit.White, 6, 4, 72, 30,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            profile.name = "CandidateProfile";
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
            pityFill.color = pity >= banner.SoftPityStart ? PanelKit.Gold : PanelKit.Pink;
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

            string footer;
            if (result.IsNew) footer = result.IsFeatured ? "限定 · 新成员" : "新成员";
            else footer = result.ShardReward > 0 ? $"碎片 +{result.ShardReward}" : "重复";
            if (result.HitHardPity) footer = "特别邀约 · " + footer;
            else if (result.UpgradedByTenPullGuarantee) footer = "重点邀约 · " + footer;
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
