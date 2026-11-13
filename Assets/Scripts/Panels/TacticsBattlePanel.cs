using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ChoSiren.Systems;
using ChoSiren.Systems.Dice;
using ChoSiren.Systems.Tactics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ChoSiren.Panels
{
    /// <summary>
    /// Portrait dice-battle presentation for <see cref="BattleSimulator"/>. The existing tactics
    /// simulator owns units, targeting, independent skill clocks, dice energy and settlement.
    /// Presentation never delays the combat clock while damage animations are playing.
    /// </summary>
    public sealed partial class TacticsBattlePanel : MonoBehaviour
    {
        private const float EnemyCellWidth = 206f;
        private const float EnemyCellHeight = 120f;
        private const float PlayerCellWidth = 158f;
        // One real roster, directly above the dice. No duplicate portraits on the stage.
        // 180 keeps the 98 px portrait and adds a dedicated blue ultimate-energy row.
        private const float PlayerCellHeight = 180f;
        private const float PlayerRosterLabelTop = 900f;
        private const float PlayerRosterTop = 934f;
        private const float BeatSeconds = 2f;
        private const float BattleIntroSeconds = 2f;
        private const int LogLines = 5;
        private const int PopupPoolSize = 14;

        private sealed class CellView
        {
            public BattleSide Side;
            public int Row;
            public int Col;
            public GameObject Root;
            public RectTransform Rect;
            public CanvasGroup Group;
            public Image Background;
            public Image Highlight;
            public Outline Outline;
            public Image Portrait;
            public Image Ornament;
            public Text Name;
            public Image HpFill;
            public Text HpText;
            public GameObject ShieldTrack;
            public Image ShieldFill;
            public Text Status;
            public Text Fallen;
            public BattleUnit Unit;
            public EnemyUnitPresentation Motion;
            public Text ActionLabel;
            public float ActionLabelUntil;
            public Text HurtLabel;
            public float HurtLabelUntil;
            public Image CastFill;
            public Text CastLabel;
            public int MotionBoundId = -1;
            public BattleEnergyMeter Energy;
            public BattleStatusIconStrip StatusIcons;
            public GameObject CaptainBadge;
        }

        private sealed class CellImpactState
        {
            public int Version;
            public Vector2 Origin;
            public Vector3 Scale;
            public Color Background;
        }

        /// <summary>Pointer enter/exit relay so anchors can preview their area on hover.</summary>
        private sealed class CellPointer : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
            IPointerDownHandler, IPointerUpHandler
        {
            public const float HoldSeconds = .5f;
            public Action Enter;
            public Action Exit;
            /// <summary>Fires once when the card is held down for <see cref="HoldSeconds"/>.</summary>
            public Action Hold;
            /// <summary>True from a completed hold until the matching click is swallowed.</summary>
            public bool HoldFired;
            private float downAt = -1f;

            public void OnPointerEnter(PointerEventData eventData) => Enter?.Invoke();
            public void OnPointerExit(PointerEventData eventData) { Exit?.Invoke(); downAt = -1f; }
            public void OnPointerDown(PointerEventData eventData) { downAt = Time.unscaledTime; HoldFired = false; }
            public void OnPointerUp(PointerEventData eventData) => downAt = -1f;
            /// <summary>A swipe took over this press: no long-press either.</summary>
            public void CancelHold() => downAt = -1f;

            private void Update()
            {
                if (downAt < 0f || Time.unscaledTime - downAt < HoldSeconds) return;
                downAt = -1f;
                HoldFired = true;
                Hold?.Invoke();
            }
        }

        private static readonly Color CellIdle = new Color32(7, 5, 20, 230);
        private static readonly Color CellEmpty = new Color32(18, 16, 52, 150);
        private static readonly Color CellEnemy = new Color32(10, 4, 23, 236);
        private static readonly Color AnchorTint = new Color32(80, 220, 255, 40);
        private static readonly Color AffectedTint = new Color32(255, 82, 194, 58);
        private static readonly Color DiceIdle = new Color32(220, 235, 255, 235);
        private static readonly Color DiceSelected = new Color32(118, 208, 255, 255);
        private static readonly Color DiceParticipating = new Color32(255, 232, 170, 255);
        private static readonly Color DiceHeld = new Color32(255, 180, 235, 255);
        private static readonly Color DiceParticipatingHeld = new Color32(255, 204, 190, 255);

        private readonly List<CellView> cells = new List<CellView>();
        private readonly List<GameObject> skillButtons = new List<GameObject>();
        private readonly List<string> logHistory = new List<string>();
        private readonly List<Text> popupPool = new List<Text>();
        private readonly SkillIconGraphic[] popupIcons = new SkillIconGraphic[PopupPoolSize];
        private readonly List<GameObject> diceButtons = new List<GameObject>();
        private readonly List<Image> diceFaceImages = new List<Image>();
        private readonly List<DiceRollPresentation> dicePresentations = new List<DiceRollPresentation>();
        private int displayedDiceRevision = -1;
        private bool awaitingDiceLanding;
        private Text diceInstructionText;
        private readonly List<Text> diceHoldLabels = new List<Text>();
        private readonly List<Outline> diceOutlines = new List<Outline>();
        private readonly List<Sprite> runtimeSprites = new List<Sprite>();
        private readonly Dictionary<string, Sprite> battlePortraits = new Dictionary<string, Sprite>();
        private Text skillWaitingText;
        private readonly StringBuilder logBuilder = new StringBuilder();

        private PanelKit kit;
        private GameModel model;
        private BattleSimulator battle;
        private Action<BattleSimulator> onFinished;
        private Func<BattleSimulator, IReadOnlyList<string>> rewardLines;
        private Action onBack;
        private Action<string> onMessage;
        private Action<string> onGrowth;
        private GameObject recoveryActions;
        private RectTransform resultCardRect;
        private Text resultRewardTitle;

        private bool closing;
        private bool finishedReported;
        private bool autoMode;
        private bool paused;
        private int speed = 1;
        private int logCursor;
        private bool awaitingInput;
        private BattleUnit inputActor;
        private string selectedSkillId;
        private readonly List<(int Row, int Col)> anchors = new List<(int Row, int Col)>();
        private int popupIndex;
        private readonly int[] popupOwners = new int[PopupPoolSize];
        private readonly Coroutine[] popupRoutines = new Coroutine[PopupPoolSize];
        private DiceTurn diceTurn;
        private int diceEnergy;
        private int diceRollSequence;
        private float battleElapsed;
        private float realtimeFractionMs;
        private int displayedPerformerId = -1;
        private int nextRealtimeUiRefresh;
        private int phaseFlashVersion;
        private int ultFlashVersion;

        private Text turnText;
        private Text actorText;
        private Text eventText;
        private Text previewText;
        private Text logText;
        private Text roundFlash;
        private Image ultFlash;
        private Image actorGlow;
        private RectTransform skillBar;
        private GameObject autoButton;
        private GameObject speedButton;
        private GameObject retreatButton;
        private GameObject exitButton;
        private GameObject pauseOverlay;
        private GameObject rulesOverlay;
        private bool rulesResumeOnClose;
        private GameObject reduceMotionButton;
        private GameObject abilityOverlay;
        private Text abilityBody;
        private RectTransform abilityContent;
        private ScrollRect abilityScroll;
        private readonly SkillIconGraphic[] abilityIcons = new SkillIconGraphic[4];
        private ChoSiren.UI.BattleStickerResultView resultScreens;
        private ChoSiren.UI.BattleStickerMarquee resultTicker;
        private Text resultMvpNote;
        private RectTransform resultRewardContent;
        private ScrollRect resultRewardScroll;
        private RectTransform popupLayer;
        private ChoSiren.UI.DiceComboShow comboShow;
        private readonly List<ChoSiren.UI.DiceScoreAura> diceAuras = new List<ChoSiren.UI.DiceScoreAura>();
        // The lit ring under each die; only dice that form the hand keep theirs glowing.
        private readonly List<Image> dicePedestals = new List<Image>();
        private static readonly Color PedestalIdle = new Color(.36f, .36f, .5f, 1f);
        private GameObject resultOverlay;
        private Text resultTitle;
        private Text resultStars;
        private Text resultStats;
        private Text resultRewards;
        private Image enemyHpFill;
        private Text enemyHpText;
        private Image teamHpFill;
        private Text teamHpText;
        private Text phaseText;
        private Text timerText;
        private Text diceHandText;
        private Image diceEnergyFill;
        private Text diceEnergyText;
        private GameObject rerollButton;
        private GameObject energyRerollButton;
        private Text rerollLabel;
        private Text energyRerollLabel;
        private Sprite battleStageSprite;
        private readonly Sprite[] userDiceFaceSprites = new Sprite[6];
        private Sprite userBossSprite;
        private readonly Dictionary<string, Sprite> enemyArt = new Dictionary<string, Sprite>();
        private readonly Dictionary<int, Image> enemyFigures = new Dictionary<int, Image>();
        private readonly Dictionary<int, RectTransform> enemyAnchors = new Dictionary<int, RectTransform>();
        private readonly Dictionary<int, EnemyUnitPresentation> enemyMotions = new Dictionary<int, EnemyUnitPresentation>();
        private readonly HashSet<int> defeatedPresentations = new HashSet<int>();
        private RectTransform enemyStageRoot;
        private Text captainNameText;
        private Text captainEffectText;
        private SkillCutInPresentation skillCutIn;
        private float presentationClock;
        private SkillEffectPresentation skillEffects;
        private AttackTrajectoryPresentation attackTrajectories;
        private GameObject guardButton;
        private Text enemyHealthCaption;
        private Text enemyDefeatedText;
        private Sprite punkPlateSprite;
        private Texture2D punkPlateTexture;
        private readonly List<Image> healthWaveBars = new List<Image>();
        private Text guardHint;
        private string commandFeedback;
        private int commandFeedbackUntil;
        private Sprite crystalBurstSprite, tideSpellSprite, lanceSpellSprite;
        private readonly Dictionary<int, string> lastPresentedActions = new Dictionary<int, string>();
        private Sprite rerollRingSprite;
        private Sprite bossHitSlashSprite;
        private Sprite bossHeartImpactSprite;
        private Sprite bossChargeAuraSprite;
        private Sprite bossLowHealthFrameSprite;
        private BossBattlePresentation bossPresentation;
        private int bossUnitId = -1;
        private Image battleReadabilityVeil;
        private readonly Dictionary<RectTransform, CellImpactState> cellImpacts =
            new Dictionary<RectTransform, CellImpactState>();

        public static TacticsBattlePanel Open(Transform host, GameModel gameModel, BattleSimulator simulator,
            Action<BattleSimulator> finished, Func<BattleSimulator, IReadOnlyList<string>> rewards = null,
            Action back = null, Action<string> message = null, Action<string> growth = null)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            if (gameModel == null) throw new ArgumentNullException(nameof(gameModel));
            if (simulator == null) throw new ArgumentNullException(nameof(simulator));

            TacticsBattlePanel existing = host.GetComponentInChildren<TacticsBattlePanel>(true);
            if (existing != null) Destroy(existing.gameObject);

            GameObject panelObject = PanelKit.CreateOverlayRoot("TacticsBattlePanel", host);
            TacticsBattlePanel panel = panelObject.AddComponent<TacticsBattlePanel>();
            panel.model = gameModel;
            panel.battle = simulator;
            panel.onFinished = finished;
            panel.rewardLines = rewards;
            panel.onBack = back;
            panel.onMessage = message;
            panel.onGrowth = growth;
            // Pick the boss track before the battle context starts its music (2026-10-21).
            UnityEngine.Object.FindAnyObjectByType<GameAudio>()?.SetBossBattle(simulator?.Stage != null && simulator.Stage.HasBossPhases);
            MusicContextScope.Attach(panelObject, GameAudio.MusicContext.Battle);
            panel.gameAudio = UnityEngine.Object.FindAnyObjectByType<GameAudio>();
            panel.InitializeBattleVoices();
            var races = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (MemberDefinition member in GameModel.Members) races[member.Id] = member.Race;
            string captain = gameModel.Save.Team.Count > 0 ? GameModel.MemberIdAt(gameModel.Save.Team[0]) : null;
            simulator.EnableRealtime(races, captain, simulator.Stage.RerollLimit,
                simulator.Stage.EncounterType == "world");
            gameModel.ApplyRelicLoadout(simulator); // 2026-11-12 第三章功能骰 (GameModelDice1111)
            // Manual skill release is the default: basics/small skills stay automatic, ultimates
            // wait for the player's tap until the top button is switched to 自动.
            simulator.AutoCastUltimates = false;
            if (UseTurnBased)
            {
                simulator.EnableTurnBased();
                // 2026-10-20: turn battles open on 自动; 手动 is one tap on the top button.
                panel.autoMode = true;
            }
            panel.diceTurn = simulator.BattleDice;
            panel.speed = Mathf.Clamp(gameModel.Save.BattleSpeed, 1, 3);
            #if UNITY_WEBGL && !UNITY_EDITOR
            panel.StartCoroutine(panel.LoadPsdBattle());
#else
            BattlePsdArt928.LoadLocal();
            panel.Build();
            panel.StartCoroutine(panel.MainLoop());
#endif
            return panel;
        }

        public BattleSimulator Battle => battle;
        public bool AutoMode => autoMode;
        public int Speed => speed;
        public bool AwaitingInput => awaitingInput;
        public string SelectedSkillId => selectedSkillId;
        public DiceHand CurrentDiceHand => diceTurn != null ? diceTurn.Hand : null;
        public int DiceEnergy => diceTurn != null ? diceTurn.Energy : diceEnergy;
        public bool IsPaused => paused;

        // ------------------------------------------------------------------ build

        private void Build()
        {
            kit = new PanelKit("Tactics");
            kit.BuildBackdrop(transform);
            LoadBattleAiArt();
            BuildBattleArtBackdrop();
            BuildHeader();
            BuildEnemyStage();
            BuildGrids();
            BuildStatusStrip();
            BuildDiceConsole();
            BuildSkillBar();
            BuildControls();
            BuildPreview();
            BuildLog();
            ApplyPsdBattleLayout();
            RectTransform effectsLayer = kit.NewRect("SkillEffects", transform);
            PanelKit.Stretch(effectsLayer);
            effectsLayer.SetSiblingIndex(pauseOverlay.transform.GetSiblingIndex());
            skillEffects = gameObject.AddComponent<SkillEffectPresentation>();
            skillEffects.Configure(effectsLayer, () => paused || closing, () => speed);
            attackTrajectories = gameObject.AddComponent<AttackTrajectoryPresentation>();
            RectTransform combatCueArea = kit.NewRect("CombatCueArea", transform);
            PanelKit.PlaceTop(combatCueArea, 0, 132, 720, PlayerRosterTop + PlayerCellHeight - 132);
            attackTrajectories.Configure(combatCueArea, () => paused || closing, () => speed);
            BuildPopups();
            skillCutIn = gameObject.AddComponent<SkillCutInPresentation>();
            skillCutIn.Configure(transform, () => paused || closing, () => speed,
                () => BattlePresentationOptions.ReduceMotion);
            // Round flash sits above popups but below the result overlay.
            roundFlash.transform.SetAsLastSibling();
            if (psdRoot != null)
            {
                // The album page covers combat VFX, while real modals stay above it.
                psdUltimate = gameObject.AddComponent<BattleAlbumUltimate929>();
                psdUltimate.Configure(transform, BattlePsdArt928.Sprite("album-disc"), () => paused || closing);
                pauseOverlay.transform.SetAsLastSibling();
                rulesOverlay.transform.SetAsLastSibling();
                if (abilityOverlay != null) abilityOverlay.transform.SetAsLastSibling();
            }
            BuildResult();
            RefreshAllCells();
            BuildPsdPresentation1019();
            PositionUltimateFlashBelowOverlays();
        }

        private void LoadBattleAiArt()
        {
            // Reuse clean stage art: the old image had obsolete HP cards baked into its pixels.
            battleStageSprite = LoadRuntimeSprite("Art/Reference038/battle-stage-038")
                ?? LoadRuntimeSprite("Art/GachaAI/gacha-calm-stage-bg-ai-v2-20260903");
            // 2026-11-03: each boss stage shows its own boss (StageBoss1103, per the planning deck).
            string bossArt = StageBoss1103.For(battle?.Stage?.Id)?.Art;
            userBossSprite = bossArt != null ? LoadRuntimeSprite(bossArt) : null;
            userBossSprite ??= LoadRuntimeSprite("Art/BattleUser/boss-throne-user-v1");
            for (int index = 0; index < userDiceFaceSprites.Length; index++)
                userDiceFaceSprites[index] = LoadRuntimeSprite($"Art/BattleUser/dice-face-{index + 1}-user-v1");
            rerollRingSprite = LoadRuntimeSprite("Art/BattleAI/reroll-ring-v1");
            bossHitSlashSprite = LoadRuntimeSprite("Art/BattleAI/battle-hit-slash-ai-v1");
            crystalBurstSprite = LoadRuntimeSprite("Art/BattleAI/battle-crystal-burst-ai-v2");
            tideSpellSprite = LoadRuntimeSprite("Art/BattleAI/battle-tide-spell-ai-v2");
            lanceSpellSprite = LoadRuntimeSprite("Art/BattleAI/battle-lance-spell-ai-v2");
            bossHeartImpactSprite = LoadRuntimeSprite("Art/BattleAI/battle-heart-impact-ai-v1");
            bossChargeAuraSprite = LoadRuntimeSprite("Art/BattleAI/battle-charge-aura-ai-v1");
            bossLowHealthFrameSprite = LoadRuntimeSprite("Art/BattleAI/battle-low-health-frame-ai-v1");
        }

        private Sprite LoadRuntimeSprite(string resourcePath)
        {
            if (resourcePath.StartsWith("Art/Reference038/", StringComparison.Ordinal))
                return ReferenceArt038.Load(resourcePath); // Cache-owned; never add to runtimeSprites.
            Sprite imported = Resources.Load<Sprite>(resourcePath);
            if (imported != null) return imported;
            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null) return null;
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 100f);
            sprite.name = texture.name + "-RuntimeSprite";
            runtimeSprites.Add(sprite);
            return sprite;
        }

        private void BuildBattleArtBackdrop()
        {
            Image art = kit.NewImage("BattleStageArt", transform, battleStageSprite, PanelKit.White);
            ChoSiren.UI.PageAmbient.Set(gameObject, battleStageSprite, featherEdges: art.rectTransform);
            PanelKit.PlaceTop(art.rectTransform, 0, 0, 720, 1536);
            art.preserveAspect = true;
            art.raycastTarget = false;

            battleReadabilityVeil = kit.NewImage("BattleReadabilityVeil", transform, null,
                new Color32(3, 4, 22, 12));
            PanelKit.Stretch(battleReadabilityVeil.rectTransform);
            battleReadabilityVeil.raycastTarget = false;

            // The new stage has no baked controls. Keep its crystal floor visible behind the deck.
            Image commandSurface = kit.NewImage("BattleCommandSurface", transform, null,
                new Color32(8, 9, 27, 92));
            PanelKit.PlaceTop(commandSurface.rectTransform, 0, 836, 720, 700);
            commandSurface.raycastTarget = false;
        }

        // Geometry-only plates: keep the hit box stable while the visible silhouette is slashed.
        // Portraits, numbers and input never get nonuniformly scaled or tilted with the frame.
        private Sprite PunkPlateSprite()
        {
            if (punkPlateSprite != null) return punkPlateSprite;
            const int size = 128;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float fy = y / (float)(size - 1), fx = x / (float)(size - 1);
                float edge = Mathf.Min(fx - .10f * fy, .90f + .10f * fy - fx) * size;
                pixels[y * size + x] = new Color32(255, 255, 255,
                    (byte)Mathf.RoundToInt(Mathf.Clamp01(edge + .5f) * 255));
            }
            punkPlateTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            { name = "BattlePunkPlate", wrapMode = TextureWrapMode.Clamp };
            punkPlateTexture.SetPixels32(pixels);
            punkPlateTexture.Apply(false, true);
            punkPlateSprite = Sprite.Create(punkPlateTexture, new Rect(0, 0, size, size), Vector2.one * .5f, 100);
            runtimeSprites.Add(punkPlateSprite);
            return punkPlateSprite;
        }

        private void StylePunkPlate(Image image, Color accent)
        {
            image.sprite = PunkPlateSprite();
            image.type = Image.Type.Simple;
            RectTransform rect = image.rectTransform;
            float w = rect.sizeDelta.x, h = rect.sizeDelta.y;
            Vector2[] points = { new Vector2(w * .10f, 1), new Vector2(w - 1, 1),
                new Vector2(w * .90f, h - 1), new Vector2(1, h - 1) };
            for (int i = 0; i < points.Length; i++)
                ReferencePunkFx.Beam(rect, "PunkFrameEdge-" + i, points[i], points[(i + 1) % points.Length],
                    i % 2 == 0 ? 2.8f : 1.5f, accent);
            ReferencePunkFx.Beam(rect, "PunkFrameWhiteSlash", new Vector2(w * .14f, 5),
                new Vector2(w * .61f, 5), 1.5f, new Color32(231, 220, 255, 216));
            ReferencePunkFx.Slash(rect, "PunkFrameAccent", w * .90f, h * .82f,
                Mathf.Min(40, h * .4f), 4, 57, accent);
        }

        private void BuildHeader()
        {
            Image header = kit.NewImage("BattleHud", transform, null, new Color32(4, 7, 28, 96));
            PanelKit.PlaceTop(header.rectTransform, 0, 0, 720, 196);
            header.raycastTarget = true;
            StageDefinition stage = battle.Stage;
            Image topLine = kit.NewImage("BossHudTopGlow", header.transform, kit.RoundedSprite(3),
                new Color32(255, 52, 182, 190));
            PanelKit.PlaceTop(topLine.rectTransform, 18, 2, 684, 3);
            Image hpFrame = kit.NewImage("BossHpFrame", header.transform, null,
                new Color32(54, 20, 79, 132));
            PanelKit.PlaceTop(hpFrame.rectTransform, 18, 174, 684, 9);

            kit.NewPlacedText(header.transform, stage.UsesRealtime ? stage.EncounterLabel : "♥ 首领", 13, PanelKit.Pink, 18, 10, 130, 22,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            Text stageTitle = kit.NewPlacedText(header.transform, stage.Name, 29, PanelKit.White,
                18, 27, 252, 38, TextAnchor.MiddleLeft, FontStyle.BoldAndItalic);
            stageTitle.gameObject.name = "BattleStageTitle";
            PanelKit.EnableBestFit(stageTitle, 18);

            GameObject phaseBadge = kit.NewPanel("BossPhaseBadge", header.transform,
                new Color32(35, 24, 84, 224), 18);
            PanelKit.PlaceTop(phaseBadge.GetComponent<RectTransform>(), 282, 12, 150, 66);
            StylePunkPlate(phaseBadge.GetComponent<Image>(), PanelKit.Gold);
            phaseBadge.GetComponent<Image>().color = new Color32(12, 7, 29, 226);
            AddReferenceBattleFrame(phaseBadge.transform, BattleReferenceFrame038.FrameKind.Information, PanelKit.Gold);
            phaseText = kit.NewPlacedText(phaseBadge.transform, "阶段 1/3", 22, PanelKit.Gold, 6, 0, 138, 36,
                TextAnchor.MiddleCenter, FontStyle.BoldAndItalic);
            PanelKit.EnableBestFit(phaseText, 16);
            timerText = kit.NewPlacedText(phaseBadge.transform, "剩余 01:00", 16, PanelKit.Muted, 6, 34, 138, 24,
                TextAnchor.MiddleCenter, FontStyle.Bold);

            enemyHpFill = kit.NewBar("EnemyHp", header.transform, 18, 176, 684, 4,
                new Color32(43, 17, 64, 255), new Color32(255, 42, 153, 255), 1);
            enemyHealthCaption = kit.NewPlacedText(header.transform, "本波敌方生命", 19, PanelKit.Pink,
                215, 93, 290, 29, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic);
            enemyHpText = kit.NewPlacedText(header.transform, string.Empty, 29, PanelKit.White, 170, 119, 380, 42,
                TextAnchor.MiddleCenter, FontStyle.BoldAndItalic);
            PanelKit.EnableBestFit(enemyHpText, 18);
            enemyDefeatedText = kit.NewPlacedText(header.transform, string.Empty, 17, PanelKit.Pink,
                536, 134, 162, 29, TextAnchor.MiddleRight, FontStyle.BoldAndItalic);
            for (int i = 0; i < 114; i++)
            {
                float height = 3 + Mathf.Abs(Mathf.Sin(i * 1.31f) * Mathf.Cos(i * .37f)) * 27;
                Image wave = kit.NewImage("EnemyHealthWave-" + i, header.transform, null, PanelKit.Pink);
                PanelKit.PlaceTop(wave.rectTransform, 19 + i * 6, 178 - height / 2, 3, height);
                wave.raycastTarget = false;
                healthWaveBars.Add(wave);
            }
            kit.AddOutline(enemyHpText.gameObject, new Color32(24, 5, 40, 210), 1f);
            turnText = kit.NewPlacedText(header.transform, battle.IsRealtime ? "实时演出" : "第 1 回合", 14, PanelKit.Muted, 18, 60, 252, 22,
                TextAnchor.MiddleLeft);

            // This bar sums ALL enemies. Boss phase thresholds use only the boss's own HP,
            // so dividing the aggregate bar into thirds would promise the wrong trigger points.
            for (int marker = 1; marker <= 2 && !stage.UsesRealtime; marker++)
            {
                Image phaseMarker = kit.NewImage("BossPhaseMarker-" + marker, header.transform,
                    kit.RoundedSprite(2), new Color32(255, 224, 245, 210));
                PanelKit.PlaceTop(phaseMarker.rectTransform, 18 + 684 * marker / 3f, 92, 2, 24);
            }
        }

        private void BuildEnemyStage()
        {
            foreach (BattleUnit unit in battle.Units)
                if (unit.Side == BattleSide.Enemy && (unit.Definition.Id == "siren-queen" ||
                    !battle.Stage.UsesRealtime && bossUnitId < 0)) bossUnitId = unit.Id;
            Image stage = kit.NewImage("EnemyStage", transform, null, new Color32(4, 5, 24, 0));
            PanelKit.PlaceTop(stage.rectTransform, 0, 196, 720, 636);
            stage.raycastTarget = false;
            enemyStageRoot = stage.rectTransform;

            for (int index = 0; index < 5; index++)
            {
                Image beam = kit.NewImage("BossSpotlight-" + index, stage.transform, kit.RadialSprite(),
                    index % 2 == 0 ? new Color32(255, 80, 213, 26) : new Color32(77, 203, 255, 22));
                PanelKit.PlaceTop(beam.rectTransform, 86 + index * 136, 40, 80, 580);
                PanelKit.CenterPivot(beam.rectTransform);
                beam.rectTransform.localEulerAngles = new Vector3(0f, 0f, -11f + index * 5.5f);
                beam.raycastTarget = false;
            }

            Image stagePulse = kit.NewImage("BossStagePulse", stage.transform, kit.RadialSprite(),
                new Color32(255, 57, 202, 38));
            PanelKit.PlaceTop(stagePulse.rectTransform, 40, 380, 640, 360);
            PanelKit.CenterPivot(stagePulse.rectTransform);
            stagePulse.raycastTarget = false;

            Image shadow = kit.NewImage("BossGroundShadow", stage.transform, kit.RadialSprite(),
                new Color32(7, 3, 28, 115));
            PanelKit.PlaceTop(shadow.rectTransform, 140, 620, 440, 100);
            PanelKit.CenterPivot(shadow.rectTransform);
            shadow.raycastTarget = false;

            Image rearAura = kit.NewImage("BossAuraBack", stage.transform, rerollRingSprite ?? kit.RadialSprite(),
                new Color32(255, 60, 203, 54));
            PanelKit.PlaceTop(rearAura.rectTransform, 30, 76, 660, 660);
            PanelKit.CenterPivot(rearAura.rectTransform);
            rearAura.preserveAspect = true;
            rearAura.raycastTarget = false;

            Image coreAura = kit.NewImage("BossAuraCore", stage.transform, kit.RadialSprite(),
                new Color32(122, 105, 255, 46));
            PanelKit.PlaceTop(coreAura.rectTransform, 100, 146, 520, 520);
            PanelKit.CenterPivot(coreAura.rectTransform);
            coreAura.raycastTarget = false;

            Image chargeArt = kit.NewImage("BossChargeAuraAI", stage.transform, bossChargeAuraSprite, PanelKit.White);
            PanelKit.PlaceTop(chargeArt.rectTransform, 50, 136, 620, 540);
            PanelKit.CenterPivot(chargeArt.rectTransform);
            chargeArt.preserveAspect = true;
            chargeArt.raycastTarget = false;
            chargeArt.gameObject.SetActive(false);

            // The header now owns 196 pixels. Fit the square rig inside the remaining stage,
            // preserving a dominant boss without crossing into the command strip.
            RectTransform rig = kit.NewRect("BossMotionRig", stage.transform);
            PanelKit.PlaceTop(rig, 44, 0, 632, 632);
            PanelKit.CenterPivot(rig);
            Image echo = kit.NewImage("BossHitEcho", rig, userBossSprite, new Color32(255, 50, 190, 0));
            PanelKit.Stretch(echo.rectTransform);
            echo.preserveAspect = true;
            echo.useSpriteMesh = true;
            echo.raycastTarget = false;
            Image portrait = kit.NewImage("BossPortrait", rig,
                userBossSprite ?? Resources.Load<Sprite>("Art/Members/hero-1037/portrait") ??
                Resources.Load<Sprite>("Art/HeroFallback"), PanelKit.White);
            PanelKit.Stretch(portrait.rectTransform);
            portrait.preserveAspect = true;
            portrait.useSpriteMesh = true;
            portrait.raycastTarget = false;
            if (bossUnitId >= 0)
            {
                portrait.raycastTarget = true;
                Button bossFocus = portrait.gameObject.AddComponent<Button>();
                bossFocus.transition = Selectable.Transition.None;
                bossFocus.onClick.AddListener(() => { if (!paused && battle.FocusEnemy(bossUnitId)) RefreshRealtimeCommands(); });
            }

            Image lowHealthFrame = kit.NewImage("BossLowHealthFrameAI", stage.transform,
                bossLowHealthFrameSprite, new Color32(255, 255, 255, 0));
            PanelKit.PlaceTop(lowHealthFrame.rectTransform, 34, 98, 652, 500);
            PanelKit.CenterPivot(lowHealthFrame.rectTransform);
            lowHealthFrame.preserveAspect = true;
            lowHealthFrame.raycastTarget = false;

            Image heartImpact = kit.NewImage("BossHeartImpactAI", stage.transform,
                bossHeartImpactSprite, PanelKit.White);
            PanelKit.PlaceTop(heartImpact.rectTransform, 100, 110, 520, 520);
            PanelKit.CenterPivot(heartImpact.rectTransform);
            heartImpact.preserveAspect = true;
            heartImpact.raycastTarget = false;
            heartImpact.gameObject.SetActive(false);

            Image hitSlash = kit.NewImage("BossHitSlashAI", stage.transform, bossHitSlashSprite, PanelKit.White);
            PanelKit.PlaceTop(hitSlash.rectTransform, 70, 92, 590, 570);
            PanelKit.CenterPivot(hitSlash.rectTransform);
            hitSlash.preserveAspect = true;
            hitSlash.raycastTarget = false;
            hitSlash.gameObject.SetActive(false);

            var effectRings = new Image[3];
            for (int index = 0; index < effectRings.Length; index++)
            {
                Image ring = kit.NewImage("BossShockwave-" + index, stage.transform,
                    rerollRingSprite ?? kit.RadialSprite(), new Color32(255, 72, 206, 0));
                float inset = 92f + index * 32f;
                PanelKit.PlaceTop(ring.rectTransform, inset, 92f + index * 18f,
                    720f - inset * 2f, 520f - index * 36f);
                PanelKit.CenterPivot(ring.rectTransform);
                ring.preserveAspect = true;
                ring.raycastTarget = false;
                ring.gameObject.SetActive(false);
                effectRings[index] = ring;
            }

            var slashTrails = new Image[3];
            for (int index = 0; index < slashTrails.Length; index++)
            {
                Image trail = kit.NewImage("BossSlashTrail-" + index, stage.transform, kit.RoundedSprite(5),
                    new Color32(255, 224, 250, 0));
                PanelKit.PlaceTop(trail.rectTransform, 108 + index * 25, 248 + index * 70, 505, 8 + index * 2);
                PanelKit.CenterPivot(trail.rectTransform);
                trail.rectTransform.localEulerAngles = new Vector3(0f, 0f, -24f + index * 21f);
                trail.raycastTarget = false;
                trail.gameObject.SetActive(false);
                slashTrails[index] = trail;
            }

            GameObject stageCaption = kit.NewPanel("BossStageCaption", stage.transform,
                new Color32(9, 8, 38, 188), 14);
            PanelKit.PlaceTop(stageCaption.GetComponent<RectTransform>(), 20, 10, 430, 64);
            StylePunkPlate(stageCaption.GetComponent<Image>(), new Color32(165, 75, 255, 255));
            stageCaption.GetComponent<Image>().color = new Color32(9, 8, 30, 206);
            AddReferenceBattleFrame(stageCaption.transform, BattleReferenceFrame038.FrameKind.Information, PanelKit.Purple);
            captainNameText = kit.NewPlacedText(stageCaption.transform, "当前队长", 17, PanelKit.White,
                14, 5, 396, 26, TextAnchor.MiddleLeft, FontStyle.Bold);
            captainNameText.gameObject.name = "CurrentCaptain";
            captainEffectText = kit.NewPlacedText(stageCaption.transform, string.Empty, 14, PanelKit.Cyan,
                14, 32, 396, 24, TextAnchor.MiddleLeft);
            captainEffectText.gameObject.name = "CaptainEffect";
            Image instructionPlate = kit.NewImage("BattleInstructionPlate", stage.transform, null, new Color32(9, 8, 30, 206));
            PanelKit.PlaceTop(instructionPlate.rectTransform, 470, 10, 230, 64);
            StylePunkPlate(instructionPlate, new Color32(165, 75, 255, 255));
            instructionPlate.raycastTarget = false;
            AddReferenceBattleFrame(instructionPlate.transform, BattleReferenceFrame038.FrameKind.Information, PanelKit.Purple);
            kit.NewPlacedText(instructionPlate.transform, battle.IsRealtime ? "普攻自动释放\n满能量点头像放大招" : "点击高亮目标", 14, PanelKit.Muted,
                14, 8, 202, 48, TextAnchor.MiddleRight).gameObject.name = "BattleControlInstruction";

            Text bossState = kit.NewPlacedText(stage.transform, string.Empty, 15, PanelKit.Pink,
                210, 88, 300, 32, TextAnchor.MiddleCenter, FontStyle.Bold);
            bossState.gameObject.name = "BossAnimationState";
            kit.AddOutline(bossState.gameObject, new Color32(8, 4, 28, 230), 1.5f);

            RectTransform damageLayer = kit.NewRect("BossDamageNumbers", stage.transform);
            PanelKit.Stretch(damageLayer);
            var damageTexts = new Text[6];
            for (int index = 0; index < damageTexts.Length; index++)
            {
                Text damage = kit.NewPlacedText(damageLayer, string.Empty, 28, PanelKit.Pink,
                    280, 280, 160, 48, TextAnchor.MiddleCenter, FontStyle.Bold);
                damage.gameObject.name = "BossDamage-" + index;
                PanelKit.CenterPivot(damage.rectTransform);
                kit.AddOutline(damage.gameObject, new Color32(12, 4, 28, 230), 2f);
                damage.gameObject.SetActive(false);
                damageTexts[index] = damage;
            }

            if (!battle.Stage.UsesRealtime || battle.Stage.HasBossPhases)
            {
                bossPresentation = stage.gameObject.AddComponent<BossBattlePresentation>();
                bossPresentation.Configure(rig, portrait, echo, rearAura, coreAura, shadow, stagePulse,
                    lowHealthFrame, hitSlash, heartImpact, chargeArt, bossState, effectRings, slashTrails,
                    damageTexts, () => paused, () => speed);
            }
            else
            {
                rig.gameObject.SetActive(false);
                rearAura.gameObject.SetActive(false);
                coreAura.gameObject.SetActive(false);
            }
            foreach (BattleUnit unit in battle.Units)
            {
                if (unit.Side != BattleSide.Enemy) continue;
                RectTransform anchor = kit.NewRect("EnemyAnchor-" + unit.Id, stage.transform);
                enemyAnchors.Add(unit.Id, anchor);
                if (unit.Id == bossUnitId) continue;
                RectTransform motionRoot = kit.NewRect("EnemyMotion-" + unit.Id, anchor);
                motionRoot.anchorMin = motionRoot.anchorMax = new Vector2(.5f, 1f);
                motionRoot.pivot = new Vector2(.5f, .5f);
                motionRoot.sizeDelta = new Vector2(300, battle.Stage.HasBossPhases ? 190 : 408);
                motionRoot.anchoredPosition = new Vector2(0, battle.Stage.HasBossPhases ? -172 : -272);
                Image figure = kit.NewImage("EnemyFigure-" + unit.Id, motionRoot,
                    EnemySprite(unit.Definition.Id), PanelKit.White);
                PanelKit.Stretch(figure.rectTransform);
                figure.preserveAspect = true;
                figure.raycastTarget = true;
                int id = unit.Id;
                Button focus = figure.gameObject.AddComponent<Button>();
                focus.targetGraphic = figure;
                focus.onClick.AddListener(() => { if (!paused && battle.FocusEnemy(id)) RefreshRealtimeCommands(); });
                enemyFigures.Add(unit.Id, figure);
                EnemyUnitPresentation motion = motionRoot.gameObject.AddComponent<EnemyUnitPresentation>();
                motion.Configure(figure, () => paused || closing || resultOverlay != null && resultOverlay.activeSelf,
                    () => speed, unit.Definition.Id);
                enemyMotions.Add(unit.Id, motion);
            }
            damageLayer.SetAsLastSibling();
        }

        private Image BossPortrait1112()
        {
            Transform rig = bossPresentation != null ? bossPresentation.MotionRoot : null;
            Image portrait = rig != null ? rig.Find("BossPortrait")?.GetComponent<Image>() : null;
            return portrait != null && portrait.isActiveAndEnabled && portrait.sprite != null ? portrait : null;
        }

        private Sprite EnemySprite(string id)
        {
            if (id == "siren-queen") return userBossSprite;
            if (!enemyArt.TryGetValue(id, out Sprite sprite))
            {
                // 2026-10-29: enemies now use the user's new 幻浊体 designs under Art/Enemies (no borrowed member portraits).
                sprite = LoadRuntimeSprite("Art/Enemies/" + id + "-ai-v1");
                var trimmed = BattleSilhouette929.Trim(sprite, id);
                if (trimmed != sprite && trimmed != null) runtimeSprites.Add(trimmed);
                sprite = trimmed;
                enemyArt[id] = sprite;
            }
            return sprite;
        }

        private void RefreshEnemyFigures()
        {
            if (psdRoot != null) { RefreshPsdEnemyFigures(); return; }
            var active = new List<BattleUnit>();
            foreach (var unit in battle.Units)
                if (unit.Side == BattleSide.Enemy && unit.Alive && unit.Id != bossUnitId) active.Add(unit);
            foreach (var unit in battle.Units)
            {
                if (unit.Side != BattleSide.Enemy || !enemyAnchors.TryGetValue(unit.Id, out var anchor)) continue;
                bool fading = enemyMotions.TryGetValue(unit.Id, out var motion) && motion.IsDefeating;
                anchor.gameObject.SetActive(unit.Alive || fading);
                if (unit.Id == bossUnitId) PanelKit.PlaceTop(anchor, 250, 84, 220, 84);
            }
            for (int index = 0; index < active.Count; index++)
            {
                BattleUnit unit = active[index];
                bool bossAdds = battle.Stage.HasBossPhases;
                float width = Mathf.Min(320, 680f / Mathf.Max(1, active.Count));
                float left = (720 - active.Count * width) / 2 + index * width;
                RectTransform anchor = enemyAnchors[unit.Id];
                PanelKit.PlaceTop(anchor, left, bossAdds ? 321 : 94, width, bossAdds ? 280 : 476);
                RectTransform motionRect = enemyMotions[unit.Id].GetComponent<RectTransform>();
                motionRect.sizeDelta = new Vector2(Mathf.Min(300, width - 12), bossAdds ? 190 : 408);
            }
        }

        private void BuildGrids()
        {
            actorGlow = kit.NewImage("ActorGlow", transform, kit.RadialSprite(), new Color32(255, 92, 214, 120));
            PanelKit.PlaceTop(actorGlow.rectTransform, 0, 0, 190, 120);
            PanelKit.CenterPivot(actorGlow.rectTransform);
            actorGlow.enabled = false;

            for (int row = 0; row < BattleGrid.Rows; row++)
            {
                for (int col = 0; col < BattleGrid.Columns; col++)
                {
                    cells.Add(BuildCell(BattleSide.Player, row, col));
                    cells.Add(BuildCell(BattleSide.Enemy, row, col));
                }
            }
        }

        private CellView BuildCell(BattleSide side, int row, int col)
        {
            bool player = side == BattleSide.Player;
            string name = $"Cell-{(player ? "P" : "E")}-{row}-{col}";
            GameObject root = kit.NewPanel(name, transform, CellEmpty, 12);
            RectTransform rect = root.GetComponent<RectTransform>();
            float width = player ? PlayerCellWidth : EnemyCellWidth;
            float height = player ? PlayerCellHeight : EnemyCellHeight;
            float left = player ? 22f + col * 170f : 548f;
            float top = player ? 1082f + row * 164f : 310f + (row * 3 + col) * 62f;
            PanelKit.PlaceTop(rect, left, top, width, height);
            StylePunkPlate(root.GetComponent<Image>(), player ? new Color32(175, 86, 255, 255) : PanelKit.Pink);
            CanvasGroup group = root.AddComponent<CanvasGroup>();
            Image background = root.GetComponent<Image>();
            background.raycastTarget = true;

            Button button = root.AddComponent<Button>();
            button.targetGraphic = background;
            button.transition = Selectable.Transition.None;
            BattleSide capturedSide = side;
            int capturedRow = row;
            int capturedCol = col;
            CellPointer pointer = root.AddComponent<CellPointer>();
            button.onClick.AddListener(() =>
            {
                // A long press already acted (captain swap); don't also treat it as a tap.
                if (pointer.HoldFired) { pointer.HoldFired = false; return; }
                CellClicked(capturedSide, capturedRow, capturedCol);
            });
            if (player) pointer.Hold = () => CellHeld(capturedRow, capturedCol);
            pointer.Enter = () => CellHovered(capturedSide, capturedRow, capturedCol, true);
            pointer.Exit = () => CellHovered(capturedSide, capturedRow, capturedCol, false);

            RectTransform playerMotionRoot = player ? kit.NewRect("PerformerMotion", root.transform) : null;
            if (player) PanelKit.Stretch(playerMotionRoot);
            Image portrait = kit.NewImage("Portrait", player ? playerMotionRoot : root.transform, null, PanelKit.White);
            if (player)
            {
                PanelKit.PlaceTop(portrait.rectTransform, 8, 2, width - 16, 98);
                portrait.preserveAspect = true;
                portrait.useSpriteMesh = true;
            }
            else
            {
                PanelKit.PlaceTop(portrait.rectTransform, width - 38, 2, 32, 30);
                portrait.preserveAspect = true;
                portrait.raycastTarget = false;
            }

            Image ornament = kit.NewImage("BattleFrame", root.transform, null, Color.clear);
            PanelKit.Stretch(ornament.rectTransform);
            ornament.preserveAspect = true;
            ornament.raycastTarget = false;
            AddReferenceBattleFrame(root.transform, player ? BattleReferenceFrame038.FrameKind.Member
                : BattleReferenceFrame038.FrameKind.Information, player ? PanelKit.Purple : PanelKit.Pink);

            Image highlight = kit.NewImage("Highlight", root.transform, PunkPlateSprite(), AnchorTint);
            PanelKit.Stretch(highlight.rectTransform);
            highlight.enabled = false;

            // Live member names keep the reference card's separate 20px name band,
            // fully below the portrait (ends y100) and clear of the shield/HP rows.
            // Enemy plates: the plate is a sheared quad — top-left corner beveled, top edge
            // diagonal (ceiling ~y15 at the name's left edge falling to ~y10 at its right),
            // so the name needs a real left inset plus a lowered band; rows below shift down
            // coherently inside the slightly taller plate. Player cards unchanged.
            Text unitName = kit.NewPlacedText(root.transform, string.Empty, player ? 12 : 16, PanelKit.White,
                player ? 10 : 24, player ? 100 : 14, width - (player ? 20 : 50), player ? 20 : 30,
                player ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft, player ? FontStyle.Bold : FontStyle.BoldAndItalic);
            unitName.gameObject.name = "UnitName";
            unitName.verticalOverflow = VerticalWrapMode.Overflow;
            PanelKit.EnableBestFit(unitName, player ? 12 : 11);
            Image hpFill = kit.NewBar("Hp", root.transform, player ? 12 : 18, player ? 122 : 48,
                width - (player ? 24 : 34), player ? 7 : 6,
                new Color32(66, 54, 117, 255), player ? PanelKit.Cyan : PanelKit.Pink, 5);
            Text hpText = kit.NewPlacedText(root.transform, string.Empty, player ? 12 : 13, PanelKit.Muted,
                player ? 10 : 17, player ? 129 : 55, width - (player ? 20 : 34), player ? 18 : 18,
                player ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft);
            hpText.gameObject.name = "UnitHpText";
            Image shieldFill = kit.NewBar("Shield", root.transform, player ? 12 : 18, player ? 119 : 44,
                width - (player ? 24 : 34), 3,
                new Color32(50, 60, 110, 255), new Color32(150, 230, 255, 255), 3);
            GameObject shieldTrack = shieldFill.transform.parent.gameObject;
            Text status = kit.NewPlacedText(root.transform, string.Empty, 12, PanelKit.Gold,
                player ? 8 : 13, player ? 170 : 74, width - (player ? 16 : 38), player ? 10 : 18,
                player ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft, FontStyle.Bold);
            status.gameObject.name = "UnitStatus";
            PanelKit.EnableBestFit(status, 8);
            Text fallen = kit.NewPlacedText(root.transform, "倒下", 14, new Color32(196, 190, 220, 255), player ? 4 : 22, player ? 31 : 26,
                width - (player ? 8 : 46), 26, TextAnchor.MiddleCenter, FontStyle.Bold);
            fallen.gameObject.SetActive(false);

            CellView view = new CellView
            {
                Side = side,
                Row = row,
                Col = col,
                Root = root,
                Rect = rect,
                Group = group,
                Background = background,
                Highlight = highlight,
                // Outline on a filled translucent plate repeats the full polygon four times,
                // flooding the card with cyan. Only the narrow edge receives selection glow.
                Outline = kit.AddOutline(root.transform.Find("PunkFrameEdge-0").gameObject,
                    new Color32(166, 112, 255, 0), 2),
                Portrait = portrait,
                Ornament = ornament,
                Name = unitName,
                HpFill = hpFill,
                HpText = hpText,
                ShieldTrack = shieldTrack,
                ShieldFill = shieldFill,
                Status = status,
                Fallen = fallen,
            };
            if (!player)
            {
                view.CastFill = kit.NewBar("EnemyCast", root.transform, 11, 113, width - 46, 4,
                    new Color32(70, 53, 25, 200), CombatFeedbackPalette.Control, 2);
                view.CastFill.transform.parent.gameObject.SetActive(false);
                view.CastLabel = kit.NewPlacedText(root.transform, "", 11, CombatFeedbackPalette.Control,
                    11, 93, width - 38, 18, TextAnchor.MiddleLeft, FontStyle.Bold);
                view.CastLabel.gameObject.name = "EnemyCastIntent";
            }
            if (player)
            {
                view.Motion = playerMotionRoot.gameObject.AddComponent<EnemyUnitPresentation>();
                view.ActionLabel = kit.NewPlacedText(root.transform, string.Empty, 13, PanelKit.Gold,
                    4, 72, width - 8, 24, TextAnchor.MiddleCenter, FontStyle.Bold);
                view.ActionLabel.gameObject.name = "PerformerAction";
                kit.AddOutline(view.ActionLabel.gameObject, new Color32(8, 9, 27, 255), 2);
                view.ActionLabel.gameObject.SetActive(false);
                view.HurtLabel = kit.NewPlacedText(root.transform, string.Empty, 12, CombatFeedbackPalette.Hurt,
                    4, 44, width - 8, 24, TextAnchor.MiddleCenter, FontStyle.Bold);
                view.HurtLabel.gameObject.name = "PerformerHurt";
                kit.AddOutline(view.HurtLabel.gameObject, new Color32(8, 9, 27, 255), 2);
                view.HurtLabel.gameObject.SetActive(false);

                // Per-character ultimate energy. The value is read from the simulator on every
                // refresh; this view never runs its own timer.
                view.Energy = root.AddComponent<BattleEnergyMeter>();
                view.Energy.Configure(rect, battle, () => view.Unit, () => paused || closing,
                    () => BattlePresentationOptions.ReduceMotion, kit);
                view.StatusIcons = root.AddComponent<BattleStatusIconStrip>();
                view.StatusIcons.Configure(rect, battle, () => view.Unit, kit);

                GameObject captainBadge = kit.NewPanel("CaptainBadge", root.transform,
                    new Color32(112, 78, 18, 232), 7);
                PanelKit.PlaceTop(captainBadge.GetComponent<RectTransform>(), width - 50, 5, 44, 17);
                kit.AddOutline(captainBadge, new Color32(255, 205, 96, 150), 1f);
                kit.NewPlacedText(captainBadge.transform, "队长", 12, PanelKit.Gold, 2, 0, 40, 17,
                    TextAnchor.MiddleCenter, FontStyle.Bold).gameObject.name = "CaptainBadgeText";
                captainBadge.SetActive(false);
                view.CaptainBadge = captainBadge;
            }
            return view;
        }

        private void BuildStatusStrip()
        {
            GameObject strip = kit.NewPanel("TurnStrip", transform, new Color32(12, 11, 47, 78), 12);
            PanelKit.PlaceTop(strip.GetComponent<RectTransform>(), 20, 840, 680, 54);
            actorText = kit.NewPlacedText(strip.transform, string.Empty, 18, PanelKit.White, 14, 3, battle.IsRealtime ? 248 : 652, 25,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            eventText = kit.NewPlacedText(strip.transform, "战斗开始", 16, PanelKit.Muted, 14, 27, 652, 22,
                TextAnchor.MiddleLeft);
            if (battle.IsRealtime)
            {
                PanelKit.PlaceTop(eventText.rectTransform, 14, 27, 248, 22);
                // Removed command: do not create an invisible Selectable or retain its callback.
                actorText.gameObject.SetActive(false);
                eventText.gameObject.SetActive(false);
                // The strip itself collapses to an invisible anchor so no empty glass bar shows.
                strip.GetComponent<Image>().color = new Color32(12, 11, 47, 0);
                guardButton = kit.NewButton("TacticalGuard", strip.transform, "应急守护", 16,
                    new Color32(24, 61, 88, 255), CombatFeedbackPalette.Shield, () =>
                    {
                        if (!paused && !closing && battle.TryTacticalGuard())
                        {
                            commandFeedback = "守护生效 · 全队减伤50%";
                            commandFeedbackUntil = battle.ElapsedMilliseconds + 3000;
                        }
                        RefreshRealtimeCommands();
                    }, 10);
                PanelKit.PlaceTop(guardButton.GetComponent<RectTransform>(), 476, 2, 190, 50);
                StylePunkPlate(guardButton.GetComponent<Image>(), CombatFeedbackPalette.Shield);
                PanelKit.PlaceTop(PanelKit.LabelOf(guardButton).rectTransform, 4, 1, 182, 26);
                guardHint = kit.NewPlacedText(guardButton.transform, "全队减伤50% · 3秒", 11, CombatFeedbackPalette.Shield,
                    4, 28, 182, 18, TextAnchor.MiddleCenter);
                ReferencePunkFx.CornerCuts(guardButton.GetComponent<RectTransform>(),
                    new Color32(122, 214, 255, 190), 18f, 2.4f, 2f);
                RectTransform guardVisuals = GroupButtonVisuals(guardButton, "GuardVisualGroup");
                guardButton.AddComponent<BattlePolish038ButtonMotion>().Configure(guardVisuals);
            }
            PanelKit.EnableBestFit(actorText, 16);
            PanelKit.EnableBestFit(eventText, 14);

            roundFlash = kit.NewPlacedText(transform, string.Empty, 40, PanelKit.White, 60, 300, 600, 80,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            roundFlash.verticalOverflow = VerticalWrapMode.Overflow;
            kit.AddOutline(roundFlash.gameObject, new Color32(255, 74, 196, 200), 3);
            roundFlash.gameObject.SetActive(false);

            // 大招释放的全屏瞬时闪光：透明常驻，释放时打一拍彩色脉冲。
            ultFlash = kit.NewImage("UltFlash", transform, null, new Color(1f, 1f, 1f, 0f));
            RectTransform flashRect = ultFlash.rectTransform;
            flashRect.anchorMin = Vector2.zero;
            flashRect.anchorMax = Vector2.one;
            flashRect.offsetMin = Vector2.zero;
            flashRect.offsetMax = Vector2.zero;
            ultFlash.raycastTarget = false;
            ultFlash.gameObject.SetActive(false);
            ultFlash.transform.SetAsLastSibling();
        }

        private void AddReferenceBattleFrame(Transform parent, BattleReferenceFrame038.FrameKind kind, Color accent)
        {
            GameObject decoration = new GameObject("ReferenceBattleFrame038", typeof(RectTransform));
            decoration.transform.SetParent(parent, false);
            PanelKit.Stretch(decoration.GetComponent<RectTransform>());
            var graphic = decoration.AddComponent<BattleReferenceFrame038>();
            graphic.Kind = kind;
            graphic.Accent = accent;
            graphic.raycastTarget = false;
            decoration.transform.SetAsFirstSibling();
        }

        private void BuildReferenceRerollButton(GameObject buttonObject, bool all)
        {
            UnityEngine.UI.Image hit = buttonObject.GetComponent<UnityEngine.UI.Image>();
            // Transparent pixels keep the full rectangular hit target while model
            // state changes can still tint labels without drawing a filled rectangle.
            hit.sprite = BattleReferenceFrame038.TransparentHitSprite;
            hit.color = Color.clear;
            hit.raycastTarget = true;
            GameObject frameObject = new GameObject("ReferenceRerollFrame", typeof(RectTransform));
            frameObject.transform.SetParent(buttonObject.transform, false);
            PanelKit.Stretch(frameObject.GetComponent<RectTransform>());
            var frame = frameObject.AddComponent<BattleReferenceFrame038>();
            frame.Kind = all ? BattleReferenceFrame038.FrameKind.AllReroll : BattleReferenceFrame038.FrameKind.SelectedReroll;
            frame.Accent = all ? PanelKit.Pink : PanelKit.Purple;
            frame.raycastTarget = false;
            frameObject.transform.SetAsFirstSibling();
            UnityEngine.UI.Text label = PanelKit.LabelOf(buttonObject);
            label.fontStyle = FontStyle.BoldAndItalic;
            label.alignment = TextAnchor.MiddleCenter;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = all ? 17 : 18;
            label.resizeTextMaxSize = all ? 23 : 22;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            float width = buttonObject.GetComponent<RectTransform>().rect.width;
            float height = buttonObject.GetComponent<RectTransform>().rect.height;
            // Caption stays inside the frame's pink hatch marks at the right shard; the
            // all-reroll caption keeps a gap to both the hatch and the heart plate.
            PanelKit.PlaceTop(label.rectTransform, all ? 94 : 27, height*.28f,
                width-(all ? 179 : 92), Mathf.Max(34,height*.47f));
            if (all)
            {
                GameObject heart = new GameObject("RerollHeart", typeof(RectTransform));
                heart.transform.SetParent(buttonObject.transform,false);
                float heartSize = Mathf.Min(80, Mathf.Max(40,height-14));
                PanelKit.PlaceTop(heart.GetComponent<RectTransform>(), 15, (height-heartSize)*.5f, heartSize, heartSize);
                var glyph = heart.AddComponent<BattleReferenceFrame038>();
                glyph.Kind = BattleReferenceFrame038.FrameKind.Heart;
                glyph.raycastTarget = false;
            }
            if (all) energyRerollLabel = label; else rerollLabel = label;
            buttonObject.AddComponent<LobbyHotspotFeedback>().Configure(all
                ? LobbyHotspotFeedback.VisualKind.CallToAction : LobbyHotspotFeedback.VisualKind.Entry);
            // Frame, caption, heart and accent layer move as one authored body; the
            // button's own RectTransform stays fixed so the hit area never drifts.
            RectTransform visualGroup = GroupButtonVisuals(buttonObject,
                all ? "AllRerollVisualGroup" : "SelectedRerollVisualGroup");
            buttonObject.AddComponent<BattlePolish038ButtonMotion>().Configure(visualGroup);
        }

        /// <summary>
        /// Moves every current child (frame art, captions, glyphs, accent fx) under a
        /// centered container the same size as the button. Children keep their authored
        /// top-left coordinates; scaling the container scales the whole visual group.
        /// </summary>
        private RectTransform GroupButtonVisuals(GameObject buttonObject, string groupName)
        {
            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            GameObject groupObject = new GameObject(groupName, typeof(RectTransform));
            RectTransform group = groupObject.GetComponent<RectTransform>();
            group.SetParent(buttonObject.transform, false);
            group.anchorMin = group.anchorMax = group.pivot = new Vector2(.5f, .5f);
            group.sizeDelta = buttonRect.rect.size;
            group.anchoredPosition = Vector2.zero;
            var children = new List<Transform>();
            foreach (Transform child in buttonObject.transform) if (child != group) children.Add(child);
            foreach (Transform child in children) child.SetParent(group, false);
            group.SetAsFirstSibling();
            return group;
        }

        private void BuildDiceConsole()
        {
            // 实时战斗回收了原底部指令框与其下方 56px 空白：骰子台加高、骰面放大，
            // 台面→提示行→骰面→重投键四段连续排布。09-15 版式再撤掉队伍总生命条与
            // 底部说明行，骰子台继续向下延伸把空隙收干净。
            bool realtime = battle.IsRealtime;
            float consoleH = realtime ? 396f : 264f;
            float hintTop = realtime ? 60f : 50f;
            float hintH = realtime ? 28f : 26f;
            float faceTop = realtime ? 100f : 76f;
            float dieSize = realtime ? 102f : 92f;
            float gap = realtime ? 10f : 14f;
            float diceLeft = realtime ? 10f : 36f;
            float statusTop = realtime ? 234f : 184f;
            float buttonTop = realtime ? 286f : 214f;
            float buttonH = realtime ? 96f : 48f;
            GameObject console = kit.NewPanel("DiceConsole", transform, new Color32(7, 6, 20, 218), 0);
            PanelKit.PlaceTop(console.GetComponent<RectTransform>(), 20, 1116, 680, consoleH);
            StylePunkPlate(console.GetComponent<Image>(), new Color32(185, 64, 249, 255));
            Image consoleGlow = kit.NewImage("DiceConsoleGlow", console.transform, kit.RadialSprite(),
                new Color32(120, 79, 255, 16));
            PanelKit.PlaceTop(consoleGlow.rectTransform, 0, 0, 680, consoleH);
            consoleGlow.raycastTarget = false;
            consoleGlow.transform.SetAsFirstSibling();
            Image titleLine = kit.NewImage("DiceConsoleTitleLine", console.transform, kit.RoundedSprite(2),
                new Color32(106, 222, 255, 132));
            PanelKit.PlaceTop(titleLine.rectTransform, 18, 48, 644, 2);
            Text diceTitle = kit.NewPlacedText(console.transform, "骰子演出", 16, PanelKit.Pink,
                18, 3, 180, 32, TextAnchor.MiddleLeft, FontStyle.Bold);
            diceTitle.gameObject.name = "DiceConsoleTitle";
            diceHandText = kit.NewPlacedText(console.transform, "等待骰子回合", 18, PanelKit.White,
                214, 1, 274, 32, TextAnchor.MiddleCenter, FontStyle.Bold);
            diceHandText.gameObject.name = "DiceHandSummary";
            PanelKit.EnableBestFit(diceHandText, 12);
            diceEnergyText = kit.NewPlacedText(console.transform, "重投充能 0%", 16, PanelKit.Cyan,
                500, 3, 160, 32, TextAnchor.MiddleRight, FontStyle.Bold);
            diceEnergyFill = kit.NewBar("DiceEnergy", console.transform, 18, 37, 644, 8,
                new Color32(49, 42, 90, 255), PanelKit.Cyan, 4);

            for (int index = 0; index < DiceRules.DiceCount; index++)
            {
                int captured = index;
                GameObject die = kit.NewButton("Dice-" + index, console.transform, "?", 38,
                    Color.clear, PanelKit.White, () => ToggleDie(captured), 20);
                PanelKit.PlaceTop(die.GetComponent<RectTransform>(), diceLeft + index * (dieSize + gap), faceTop, dieSize, dieSize);
                Image dieArt = die.GetComponent<Image>();
                // The button remains a full-size invisible hit target. The authored diamond die is the only visible art.
                dieArt.sprite = null;
                dieArt.type = Image.Type.Simple;
                dieArt.color = Color.clear;
                RectTransform rollRig = kit.NewRect("DiceRollRig-" + index, die.transform);
                PanelKit.Stretch(rollRig);
                Image faceArt = kit.NewImage("DiceFace-" + index, rollRig, null, PanelKit.White);
                PanelKit.Stretch(faceArt.rectTransform);
                faceArt.type = Image.Type.Simple;
                faceArt.preserveAspect = true;
                faceArt.useSpriteMesh = true;
                faceArt.raycastTarget = false;
                Outline outline = kit.AddOutline(faceArt.gameObject, new Color32(96, 220, 255, 90), 1f);
                Text held = kit.NewPlacedText(console.transform, "", 16, PanelKit.Gold,
                    diceLeft + index * (dieSize + gap), statusTop, dieSize, 24,
                    TextAnchor.MiddleCenter, FontStyle.Bold);
                held.gameObject.name = "DiceStatus-" + index;
                diceButtons.Add(die);
                diceFaceImages.Add(faceArt);
                diceHoldLabels.Add(held);
                diceOutlines.Add(outline);
                DiceRollPresentation roll = rollRig.gameObject.AddComponent<DiceRollPresentation>();
                roll.Configure(faceArt, rollRig, () => paused || closing || EntryPresentationCovered, () => speed,
                    () => BattlePresentationOptions.ReduceMotion, renderedRoll: false);
                dicePresentations.Add(roll);
                DiceRollPresentation.HardLanding -= OnDiceHardLanding;
                DiceRollPresentation.HardLanding += OnDiceHardLanding;

            }

            rerollButton = kit.NewButton("DiceReroll", console.transform, "重投已选0颗", 22,
                PanelKit.ButtonDark, PanelKit.White, RerollDice, 0);
            PanelKit.PlaceTop(rerollButton.GetComponent<RectTransform>(),
                realtime ? 12f : 24f, buttonTop, realtime ? 262f : 270f, buttonH);
            energyRerollButton = kit.NewButton("EnergyReroll", console.transform, "全部重投", 23,
                PanelKit.ButtonDark, PanelKit.White, EnergyRerollDice, 0);
            PanelKit.PlaceTop(energyRerollButton.GetComponent<RectTransform>(),
                realtime ? 285f : 309f, buttonTop, realtime ? 383f : 350f, buttonH);
            BuildReferenceRerollButton(rerollButton, false);
            BuildReferenceRerollButton(energyRerollButton, true);
            Text diceHint = kit.NewPlacedText(console.transform,
                battle.IsRealtime
                    ? "点骰子标记“待重投”，再按下方“重投已选”按钮；没点的保留"
                    : "点击保留 · 骰型加成下一技能", 16,
                PanelKit.Muted, 36, hintTop, 608, hintH, TextAnchor.MiddleCenter);
            diceHint.gameObject.name = "DiceInstruction";
            diceInstructionText = diceHint;
            PanelKit.EnableBestFit(diceHint, 16);

            kit.NewPlacedText(transform, "出战成员", 18, PanelKit.Cyan,
                28, PlayerRosterLabelTop, 180, 30,
                TextAnchor.MiddleLeft, FontStyle.Bold).gameObject.name = "TeamRoster";
            if (battle.IsRealtime)
            {
                // Abilities now hang off the roster header as a compact dropdown, right next to
                // the members they describe, instead of a full-width deck pinned to the bottom.
                // ▼ is already part of the shipped font subset (StoryPanel uses it); a fresher
                // glyph like ▾ would only exist after the next font rebuild and render as tofu.
                // 配色与战术条的应急守护同一套玻璃感，避免孤立的深色块。
                GameObject details = kit.NewButton("AbilityDetails", transform, "角色能力 ▼", 15,
                    new Color32(38, 46, 96, 235), new Color32(198, 214, 255, 255), OpenAbilityDetails, 9);
                PanelKit.PlaceTop(details.GetComponent<RectTransform>(),
                    536, PlayerRosterLabelTop - 3, 164, 36);
                StylePunkPlate(details.GetComponent<Image>(), new Color32(169, 93, 250, 255));
                details.GetComponent<Image>().color = new Color32(12, 7, 29, 226);
                RectTransform abilityVisuals = GroupButtonVisuals(details, "AbilityVisualGroup");
                details.AddComponent<BattlePolish038ButtonMotion>().Configure(abilityVisuals);
            }
            RefreshDiceUi();
        }

        private void BuildSkillBar()
        {
            // Realtime play no longer gets a boxed command deck at the bottom: the roster cards
            // already show 大招就绪/满能量 per member and the mode strip already names the actor,
            // so the panel only repeated existing information. It survives as an invisible anchor
            // for the turn-based skill buttons and the shared status line.
            bool realtime = battle.IsRealtime;
            GameObject frame = kit.NewPanel("SkillCommandDeck", transform,
                realtime ? new Color32(12, 14, 35, 0) : new Color32(12, 14, 35, 255), 16);
            PanelKit.PlaceTop(frame.GetComponent<RectTransform>(), 20, 1386,
                680, realtime ? 34 : 92);
            if (!realtime) kit.AddOutline(frame, new Color32(114, 207, 255, 76), 1.25f);
            skillWaitingText = kit.NewPlacedText(frame.transform, "等待演出开始", realtime ? 15 : 20,
                PanelKit.Muted, 18, realtime ? 3 : 14, 644, realtime ? 28 : 64, TextAnchor.MiddleCenter);
            skillWaitingText.gameObject.name = "SkillWaitingState";
            skillBar = kit.NewRect("SkillBar", transform);
            PanelKit.PlaceTop(skillBar, 24, 1390, 672, realtime ? 26 : 84);
            // Legacy turn-based layout remains available; realtime owns no footer instruction UI.
            frame.SetActive(!realtime);
            skillBar.gameObject.SetActive(!realtime);
        }

        /// <summary>Compact secondary surface for the selected character's two active skills.</summary>
        private void OpenAbilityDetails()
        {
            BattleUnit actor = inputActor;
            if (actor == null) return;
            if (abilityOverlay == null) BuildAbilityOverlay();
            CombatRace race = battle.RaceOf(actor);
            string passive = race == CombatRace.Charm ? "魅族指挥" : race == CombatRace.Demon ? "魔族指挥"
                : race == CombatRace.Mermaid ? "人鱼指挥" : race == CombatRace.BloodElf ? "血精灵指挥" : "全队增益";
            string[] names = { BattleSimulator.BasicAttackName, MemberSkillNames1103.Name(actor.Definition.Id, race, false),
                MemberSkillNames1103.Name(actor.Definition.Id, race, true), passive };
            string kitPassive = BattleSimulator.KitPassiveDescription(battle.KitOf(actor)); // 2026-11-11 new members
            string[] descriptions = { "自动普攻 · 单体伤害", BattleSimulator.MemberSkillDescription(actor.Definition.Id, race, false),
                BattleSimulator.MemberSkillDescription(actor.Definition.Id, race, true),
                (kitPassive != null ? kitPassive + "\n" : string.Empty) +
                "成为队长时生效。" + ChoSiren.Systems.Presentation.MemberCaptainTraits.Describe(passive) };
            // 2026-10-25 readability pass: a small muted kind line, the skill name in bold, then what it does.
            string[] roles = { "基础攻击 · 自动", "小技能 · 自动", "大招 · 能量满后点她", "骰子被动 · 当队长时生效" };
            string prefix = "<b>" + actor.Definition.Name + "</b>\n\n";
            for (int i = 0; i < names.Length; i++)
            {
                float top = MeasureBattleText(abilityBody, prefix);
                PanelKit.PlaceTop(abilityIcons[i].rectTransform, 4, top + 2f, 36, 36);
                abilityIcons[i].SkillKey = names[i];
                abilityIcons[i].Kind = SkillIconVisuals.Resolve(names[i], descriptions[i]);
                prefix += "<size=18><color=#9E94BA>" + roles[i] + "</color></size>\n<b><color=#1CF2EC>" + names[i] +
                    "</color></b>\n" + descriptions[i] + "\n\n";
            }
            abilityBody.text = prefix + (battle.IsUltimateReady(actor) ? "大招就绪 · 点她的卡释放"
                : battle.IsEnergyFull(actor) ? "大招暂不可用 · 冷却或控制中" : "能量蓄积中") +
                "\n点左下角「队长」牌可以换队长";
            float height = Mathf.Max(610f, MeasureBattleText(abilityBody, abilityBody.text) + 16f);
            abilityContent.sizeDelta = new Vector2(0f, height);
            PanelKit.PlaceTop(abilityBody.rectTransform, 58f, 0f, 450f, height);
            abilityScroll.verticalNormalizedPosition = 1f;
            abilityOverlay.SetActive(true);
            abilityOverlay.transform.SetAsLastSibling();
        }

        private void BuildAbilityOverlay()
        {
            Image shade = kit.NewImage("AbilityOverlay", transform, null, new Color32(3, 2, 12, 225));
            PanelKit.Stretch(shade.rectTransform);
            shade.raycastTarget = true;
            GameObject card = kit.NewImage("AbilityCard", shade.transform, null, ChoSiren.UI.SirenStickerUi.Ink).gameObject;
            PanelKit.PlaceCentered(card.GetComponent<RectTransform>(), 580, 850);
            ChoSiren.UI.SirenStickerUi.SkinPanel(card);
            Text title = kit.NewPlacedText(card.transform, "角色能力", 30, PanelKit.White, 28, 22, 524, 44,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            title.name = "AbilityTitle";
            ChoSiren.UI.SirenStickerUi.SkinTitle(title);
            abilityScroll = BuildBattleScroll(card.transform, "AbilityScroll", 28, 86, 524, 650, out abilityContent);
            abilityBody = kit.NewPlacedText(abilityContent, string.Empty, 22, ChoSiren.UI.SirenStickerUi.Paper, 58, 0,
                450, 650, TextAnchor.UpperLeft);
            abilityBody.gameObject.name = "AbilityBody";
            abilityBody.supportRichText = true;
            abilityBody.lineSpacing = 1.2f;
            abilityBody.verticalOverflow = VerticalWrapMode.Overflow;
            for (int i = 0; i < abilityIcons.Length; i++)
                abilityIcons[i] = SkillIconVisuals.Create(abilityContent, "AbilityIcon-" + i,
                    string.Empty, string.Empty, i == 3 ? ChoSiren.UI.SirenStickerUi.Cyan : ChoSiren.UI.SirenStickerUi.Paper);
            GameObject close = PunkUi.Button(kit, card.transform, "AbilityClose", "关闭", 22,
                CloseAbilityOverlay, PunkButtonStyle.Primary);
            PanelKit.PlaceTop(close.GetComponent<RectTransform>(), 28, 766, 524, 58);
            ChoSiren.UI.SirenStickerUi.SkinButton(close, true);
            abilityOverlay = shade.gameObject;
            abilityOverlay.SetActive(false);
        }

        private static float MeasureBattleText(Text text, string value)
        {
            var settings = text.GetGenerationSettings(new Vector2(450f, 0f));
            return text.cachedTextGeneratorForLayout.GetPreferredHeight(value, settings) / text.pixelsPerUnit;
        }

        private ScrollRect BuildBattleScroll(Transform parent, string name, float x, float y, float width,
            float height, out RectTransform content)
        {
            RectTransform root = kit.NewRect(name, parent);
            PanelKit.PlaceTop(root, x, y, width, height);
            RectTransform viewport = kit.NewRect("Viewport", root);
            PanelKit.Stretch(viewport);
            Image hit = viewport.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, .001f);
            hit.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            content = kit.NewRect("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0, height);
            ScrollRect scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 38f;
            // Content is explicitly sized from measured text / reward rows, not a competing fitter.
            return scroll;
        }

        private void CloseAbilityOverlay()
        {
            if (abilityOverlay != null) abilityOverlay.SetActive(false);
        }

        private void BuildControls()
        {
            autoButton = kit.NewButton("AutoToggle", transform, "自动", 20, PanelKit.ButtonDark, PanelKit.White,
                ToggleAuto, 12);
            if (battle.IsRealtime)
            {
                PanelKit.LabelOf(autoButton).text = autoMode ? "自动" : "手动";
                PanelKit.LabelOf(autoButton).fontSize = 17;
                PanelKit.EnableBestFit(PanelKit.LabelOf(autoButton), 15);
            }
            PanelKit.PlaceTop(autoButton.GetComponent<RectTransform>(), 440, 20, 94, 52);
            speedButton = kit.NewButton("SpeedToggle", transform, speed + "倍", 20, PanelKit.ButtonDark, PanelKit.White,
                ToggleSpeed, 12);
            PanelKit.PlaceTop(speedButton.GetComponent<RectTransform>(), 542, 20, 72, 52);
            retreatButton = kit.NewButton("PauseToggle", transform, "暂停", 20, PanelKit.ButtonDark,
                PanelKit.White, TogglePause, 12);
            PanelKit.PlaceTop(retreatButton.GetComponent<RectTransform>(), 622, 20, 78, 52);
            foreach (GameObject control in new[] { autoButton, speedButton, retreatButton })
            {
                StylePunkPlate(control.GetComponent<Image>(), new Color32(173, 99, 252, 255));
                control.GetComponent<Image>().color = new Color32(10, 6, 29, 232);
                PanelKit.LabelOf(control).fontStyle = FontStyle.BoldAndItalic;
            }

            Image pauseShade = kit.NewImage("PauseOverlay", transform, null, new Color32(3, 5, 24, 168));
            PanelKit.Stretch(pauseShade.rectTransform);
            pauseShade.raycastTarget = true;

            // Compact pause card: title, one-line hint, continue, exit, and a link to the rules.
            // The long rule text now lives in the secondary overlay and never covers the battle.
            Image pauseDrop = kit.NewImage("PauseDialogShadow", pauseShade.transform, null, ChoSiren.UI.SirenStickerUi.Violet);
            PanelKit.PlaceCentered(pauseDrop.rectTransform, 380, 300);
            pauseDrop.rectTransform.anchoredPosition += new Vector2(6f, -6f);
            pauseDrop.raycastTarget = false;
            GameObject dialog = kit.NewImage("PauseDialog", pauseShade.transform, null, new Color32(12, 6, 24, 255)).gameObject;
            RectTransform dialogRect = dialog.GetComponent<RectTransform>();
            PanelKit.PlaceCentered(dialogRect, 380, 300);
            Image titleBar = kit.NewImage("PauseTitleBar", dialog.transform, null, ChoSiren.UI.SirenStickerUi.Violet);
            PanelKit.PlaceTop(titleBar.rectTransform, 0, 0, 380, 6);
            kit.NewPlacedText(dialog.transform, "战斗暂停", 28, PanelKit.White, 20, 20, 340, 44,
                TextAnchor.MiddleCenter, FontStyle.Bold).gameObject.name = "PauseTitle";
            Text pauseHint = kit.NewPlacedText(dialog.transform, "退出战斗不返还体力", 14,
                PanelKit.Muted, 20, 65, 340, 36, TextAnchor.MiddleCenter);
            pauseHint.gameObject.name = "PauseHint";
            PanelKit.EnableBestFit(pauseHint, 12);
            GameObject resume = PunkUi.Button(kit, dialog.transform, "ResumeBattle", "继续战斗", 22,
                TogglePause, PunkButtonStyle.Primary);
            PanelKit.PlaceTop(resume.GetComponent<RectTransform>(), 30, 108, 320, 58);
            exitButton = PunkUi.Button(kit, dialog.transform, "BattleExit", "退出战斗", 20,
                ExitBattle, PunkButtonStyle.Outline);
            PanelKit.PlaceTop(exitButton.GetComponent<RectTransform>(), 30, 176, 320, 54);
            GameObject rulesButton = PunkUi.Button(kit, dialog.transform, "BattleRulesEntry", "战斗规则与设置", 16,
                OpenRulesOverlay, PunkButtonStyle.Ghost);
            PanelKit.LabelOf(rulesButton).color = PunkUi.Cyan;
            PanelKit.PlaceTop(rulesButton.GetComponent<RectTransform>(), 30, 240, 320, 42);
            ChoSiren.UI.SirenStickerUi.SkinPanel(dialog);
            ChoSiren.UI.SirenStickerUi.SkinTitle(dialog.transform.Find("PauseTitle").GetComponent<Text>());
            foreach (GameObject action in new[] { resume, exitButton, rulesButton })
                ChoSiren.UI.SirenStickerUi.SkinButton(action, action == resume);
            pauseOverlay = pauseShade.gameObject;
            pauseOverlay.SetActive(false);
            BuildRulesOverlay();
        }

        /// <summary>Secondary rules/settings surface, reachable from the compact pause card.</summary>
        private void BuildRulesOverlay()
        {
            Image shade = kit.NewImage("RulesOverlay", transform, null, new Color32(2, 4, 20, 214));
            PanelKit.Stretch(shade.rectTransform);
            shade.raycastTarget = true;
            Image rulesDrop = kit.NewImage("RulesCardShadow", shade.transform, null, ChoSiren.UI.SirenStickerUi.Violet);
            PanelKit.PlaceCentered(rulesDrop.rectTransform, 640, 1040);
            rulesDrop.rectTransform.anchoredPosition += new Vector2(6f, -6f);
            rulesDrop.raycastTarget = false;
            GameObject card = kit.NewImage("RulesCard", shade.transform, null, new Color32(12, 6, 24, 255)).gameObject;
            RectTransform cardRect = card.GetComponent<RectTransform>();
            PanelKit.PlaceCentered(cardRect, 640, 1040);
            Image rulesRule = kit.NewImage("RulesTopRule", card.transform, null, ChoSiren.UI.SirenStickerUi.Violet);
            PanelKit.PlaceTop(rulesRule.rectTransform, 0, 0, 640, 6);
            kit.NewPlacedText(card.transform, "战斗规则与设置", 26, PanelKit.White, 24, 22, 592, 40,
                TextAnchor.MiddleCenter, FontStyle.Bold).gameObject.name = "RulesTitle";
            Text body = kit.NewPlacedText(card.transform,
                // 2026-10-25 readability pass: the current rules in short points (it still described the old judge bar,
                // light pillars and long-press captain swaps). 2026-11-01 (用户 "最少文字"): the first fight teaches only
                // dice / 重投 / 大招 by doing; everything is here, one short line per rule (≤ one line on a phone).
                "<color=#1CF2EC>【骰子】</color>\n" +
                "·\u00A0点骰子选中，每次最多2颗，再点取消。\n" +
                "·\u00A0点左侧「重投」：不选=6颗全部重投。\n" +
                "·\u00A0重投不限次数，每次花1颗骰子豆。\n" +
                "·\u00A0我方出手3次攒1颗，最多存3颗；新一波+1颗。\n" +
                "·\u00A0牌型越好，队长攻击越高，最高+5%。\n" +
                "·\u00A0达成本波牌型目标，队长追加一击。\n\n" +
                "<color=#1CF2EC>【大招】</color>\n" +
                "·\u00A0能量满了点她的卡放大招，上滑光碟也行。\n" +
                "·\u00A0光圈合拢时点：PERFECT\u00A0+10%，GOOD\u00A0+5%。\n" +
                "·\u00A0一人放了大招，其他人能量清零重攒。\n\n" +
                "<color=#1CF2EC>【队长】</color>\n" +
                "·\u00A0点左下「队长」牌，再点一位成员即换队长。\n" +
                "·\u00A0队长吃满骰子加成，受到伤害-20%。\n" +
                "·\u00A0队长倒下，由下一位接任。\n\n" +
                "<color=#1CF2EC>【自动 / 倍速】</color>\n" +
                "·\u00A0普攻和技能一直自动。\n" +
                "·\u00A0自动：牌型差时自己重投、放大招。\n" +
                "·\u00A0手动：重投和大招都等你点。\n" +
                "·\u00A0右上「1倍」切换倍速。\n\n" +
                "<color=#1CF2EC>【应急守护】</color>\n" +
                "·\u00A0每场一次，全员各挡一次攻击。\n\n" +
                "<color=#1CF2EC>【敌人】</color>\n" +
                "·\u00A0敌人会预告要放的招。\n" +
                "·\u00A0点敌人集火，先打会治疗的。",
                24, ChoSiren.UI.SirenStickerUi.Paper, 34, 76, 572, 780, TextAnchor.UpperLeft);
            body.gameObject.name = "RulesBody";
            body.supportRichText = true; // PanelKit texts default to plain; the teal section heads are <color> tags
            body.lineSpacing = 1.12f;
            ScrollRect rulesScroll = BuildBattleScroll(card.transform, "RulesScroll", 34, 76, 572, 780,
                out RectTransform rulesContent);
            body.transform.SetParent(rulesContent, false);
            body.verticalOverflow = VerticalWrapMode.Overflow;
            float rulesHeight = Mathf.Max(780f, body.cachedTextGeneratorForLayout.GetPreferredHeight(body.text,
                body.GetGenerationSettings(new Vector2(572f, 0f))) / body.pixelsPerUnit + 16f);
            rulesContent.sizeDelta = new Vector2(0f, rulesHeight);
            PanelKit.PlaceTop(body.rectTransform, 0f, 0f, 572f, rulesHeight);
            reduceMotionButton = PunkUi.Button(kit, card.transform, "ReduceMotionToggle",
                BattlePresentationOptions.Label(BattlePresentationOptions.ReduceMotion), 17,
                ToggleReduceMotion, PunkButtonStyle.Outline);
            PanelKit.PlaceTop(reduceMotionButton.GetComponent<RectTransform>(), 34, 866, 572, 56);
            GameObject back = PunkUi.Button(kit, card.transform, "RulesBack", "返回", 20,
                CloseRulesOverlay, PunkButtonStyle.Primary);
            PanelKit.PlaceTop(back.GetComponent<RectTransform>(), 34, 938, 572, 58);
            ChoSiren.UI.SirenStickerUi.SkinPanel(card);
            ChoSiren.UI.SirenStickerUi.SkinTitle(card.transform.Find("RulesTitle").GetComponent<Text>());
            ChoSiren.UI.SirenStickerUi.SkinButton(reduceMotionButton);
            ChoSiren.UI.SirenStickerUi.SkinButton(back, true);
            rulesOverlay = shade.gameObject;
            rulesOverlay.SetActive(false);
        }

        private void OpenRulesOverlay()
        {
            if (rulesOverlay == null) return;
            RefreshReduceMotionButton();
            rulesOverlay.SetActive(true);
            rulesOverlay.transform.SetAsLastSibling();
            rulesOverlay.GetComponentInChildren<ScrollRect>().verticalNormalizedPosition = 1f;
        }

        private void OpenBattleQuickGuide()
        {
            if (closing || battle == null || battle.Outcome != BattleOutcome.Ongoing) return;
            bool resumeAfterReading = !paused;
            if (!paused) TogglePause();
            rulesResumeOnClose = resumeAfterReading;
            OpenRulesOverlay();
        }

        private void CloseRulesOverlay()
        {
            if (rulesOverlay != null) rulesOverlay.SetActive(false);
            bool resume = rulesResumeOnClose && paused && !closing &&
                battle != null && battle.Outcome == BattleOutcome.Ongoing;
            rulesResumeOnClose = false;
            if (resume) TogglePause();
        }

        private void ToggleReduceMotion()
        {
            BattlePresentationOptions.Toggle();
            RefreshReduceMotionButton();
            Notify(BattlePresentationOptions.ReduceMotion ? "已开启减少动画" : "已恢复完整动画");
        }

        private void RefreshReduceMotionButton()
        {
            if (reduceMotionButton == null) return;
            PanelKit.LabelOf(reduceMotionButton).text =
                BattlePresentationOptions.Label(BattlePresentationOptions.ReduceMotion);
        }

        private void BuildPreview()
        {
            GameObject panel = kit.NewPanel("PreviewBoard", transform, new Color32(18, 15, 56, 64), 12);
            PanelKit.PlaceTop(panel.GetComponent<RectTransform>(), 20,
                1486, 680, battle.IsRealtime ? 50 : 46);
            teamHpFill = kit.NewBar("TeamHealth", panel.transform, 8, 2, 664, 18,
                new Color32(24, 57, 56, 255), new Color32(67, 206, 154, 255), 8);
            teamHpText = kit.NewPlacedText(panel.transform, string.Empty, 13, PanelKit.White,
                8, 0, 664, 22, TextAnchor.MiddleCenter, FontStyle.Bold);
            teamHpText.gameObject.name = "TeamHealthSummary";
            previewText = kit.NewPlacedText(panel.transform, "点击角色查看技能 · 点击敌人集火", 13,
                PanelKit.Muted, 12, 23, 656, 21, TextAnchor.MiddleCenter);
            previewText.gameObject.name = "TargetPreview";
            PanelKit.EnableBestFit(previewText, 14);
            panel.SetActive(!battle.IsRealtime);
        }

        private void BuildLog()
        {
            logText = kit.NewPlacedText(transform, string.Empty, 1, Color.clear, 0, 0, 1, 1,
                TextAnchor.UpperLeft);
            logText.gameObject.name = "BattleLog";
        }

        private void BuildPopups()
        {
            popupLayer = kit.NewRect("Popups", transform);
            PanelKit.Stretch(popupLayer);
            comboShow = ChoSiren.UI.DiceComboShow.Attach(popupLayer, () => paused || closing, BattleAnimationDelta);
            for (int index = 0; index < PopupPoolSize; index++)
            {
                Text popup = kit.NewText("Popup", popupLayer, string.Empty, 22, PanelKit.White, FontStyle.Bold,
                    TextAnchor.MiddleCenter);
                popup.horizontalOverflow = HorizontalWrapMode.Overflow;
                popup.verticalOverflow = VerticalWrapMode.Overflow;
                PanelKit.PlaceTop(popup.rectTransform, 0, 0, 160, 40);
                PanelKit.CenterPivot(popup.rectTransform);
                kit.AddOutline(popup.gameObject, Color.black, 2.8f);
                // 2026-10-21: P5 slab — a hard drop shadow under the outline, and a white-to-colour face.
                var slab = popup.gameObject.AddComponent<Shadow>();
                slab.effectColor = new Color(.05f, 0f, .08f, .95f); slab.effectDistance = new Vector2(3f, -4f);
                popup.gameObject.AddComponent<PopupGradient1021>();
                popup.fontStyle = FontStyle.BoldAndItalic;
                popup.gameObject.SetActive(false);
                popupPool.Add(popup);
                // Semantic icon docked on the popup: heal/shield/buff reads come from a glyph
                // plus a short number instead of a full sentence floating over the stage.
                SkillIconGraphic icon = CombatStatusIconVisuals.CreateIcon(popup.transform,
                    "PopupIcon", 22f);
                icon.gameObject.SetActive(false);
                popupIcons[index] = icon;
                icon.gameObject.AddComponent<BattlePsdEffectIcon1019>().Configure(
                    icon, SkillIconKind.Heal, "effect-heal-plus");
            }
        }

        private Text resultTitleSub;
        private Image resultBackdrop;
        private Button resultRewardButton;

        /// <summary>
        /// 2026-10-21 owner: the result screen replicates the picked R2 mock-up 1:1 — full screen over
        /// the stage scene dimmed 72%, VICTORY with a red drop shadow top-left, MVP half-body cover,
        /// red band, stars, round avatars, rewards and a red slanted 继续 button. No card frame.
        /// </summary>
        private void BuildResult()
        {
            Image overlay = kit.NewImage("BattleResult", transform, null, new Color32(8, 5, 14, 255));
            PanelKit.Stretch(overlay.rectTransform);
            overlay.raycastTarget = true;
            resultOverlay = overlay.gameObject;
            resultBackdrop = kit.NewImage("ResultBackdrop", overlay.transform, null, Color.white);
            resultBackdrop.raycastTarget = false;
            var cover = resultBackdrop.gameObject.AddComponent<AspectRatioFitter>();
            cover.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; cover.aspectRatio = 2f / 3f;
            Image dim = kit.NewImage("ResultDim", overlay.transform, null, new Color(8 / 255f, 5 / 255f, 14 / 255f, .72f));
            PanelKit.Stretch(dim.rectTransform); dim.raycastTarget = false;
            RectTransform cardRect = kit.NewRect("BattleResultCard", overlay.transform);
            PanelKit.Stretch(cardRect);
            resultCardRect = cardRect;
            Transform card = cardRect;
            RectTransform screens = kit.NewRect("ResultConcertScreens", card);
            PanelKit.Stretch(screens);
            resultScreens = screens.gameObject.AddComponent<ChoSiren.UI.BattleStickerResultView>();
            // .vic{font:900 28px(×3) Impact;color:#fff;text-shadow:3px 3px 0 var(--red)} small{12px(×3)}
            resultTitle = kit.NewPlacedText(card, "VICTORY", 84, Color.white, 36, 60, 640, 96,
                TextAnchor.MiddleLeft, FontStyle.BoldAndItalic);
            resultTitle.horizontalOverflow = HorizontalWrapMode.Overflow;
            resultTitle.verticalOverflow = VerticalWrapMode.Overflow;
            resultTitle.rectTransform.pivot = new Vector2(0f, .5f);
            var redShadow = resultTitle.gameObject.AddComponent<Shadow>();
            redShadow.effectColor = Brand1025.Teal; redShadow.effectDistance = new Vector2(9f, -9f);
            resultTitleSub = kit.NewPlacedText(card, "胜利", 36, Color.white, 40, 150, 300, 44,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            resultTitleSub.verticalOverflow = VerticalWrapMode.Overflow;
            var subShadow = resultTitleSub.gameObject.AddComponent<Shadow>();
            subShadow.effectColor = Brand1025.Teal; subShadow.effectDistance = new Vector2(4f, -4f);
            resultMvpNote = kit.NewPlacedText(card, string.Empty, 16, ChoSiren.UI.SirenStickerUi.Cyan,
                0, 0, 10, 10, TextAnchor.MiddleCenter, FontStyle.Bold);
            resultMvpNote.gameObject.SetActive(false);
            resultStars = kit.NewPlacedText(card, "★★★", 66, new Color32(255, 212, 90, 255), 0, 0, 720, 80,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            resultStars.verticalOverflow = VerticalWrapMode.Overflow;
            resultStats = kit.NewPlacedText(card, string.Empty, 19, PanelKit.White, 0, 0, 10, 10,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            resultStats.gameObject.SetActive(false);
            Image tickerPlate = kit.NewImage("ResultTicker", card, null, Color.clear);
            PanelKit.PlaceTop(tickerPlate.rectTransform, 0, 0, 10, 10);
            Text tickerText = kit.NewPlacedText(tickerPlate.transform, string.Empty, 17, PanelKit.White,
                0, 0, 10, 10, TextAnchor.MiddleLeft, FontStyle.Bold);
            resultTicker = tickerPlate.gameObject.AddComponent<ChoSiren.UI.BattleStickerMarquee>();
            resultTicker.Configure(tickerPlate.rectTransform, tickerText, () => closing);
            tickerPlate.gameObject.SetActive(false);
            // .rw{centered row, cyan} — tapping it opens the equipment that dropped.
            Image reward = kit.NewImage("BattleReward", card, null, Color.clear);
            reward.raycastTarget = true;
            resultRewardButton = reward.gameObject.AddComponent<Button>();
            resultRewardButton.transition = Selectable.Transition.None;
            resultRewardButton.onClick.AddListener(() => { if (model.LastAwardedAccessory >= 0 && onGrowth != null) OpenRecovery("accessory"); });
            resultRewardTitle = kit.NewPlacedText(reward.transform, string.Empty, 17, ChoSiren.UI.SirenStickerUi.Cyan,
                0, 0, 10, 10, TextAnchor.MiddleLeft, FontStyle.Bold);
            resultRewardTitle.gameObject.SetActive(false);
            resultRewardScroll = BuildBattleScroll(reward.transform, "RewardScroll", 0, 0, 640, 96, out resultRewardContent);
            resultRewards = kit.NewPlacedText(resultRewardContent, string.Empty, 22, ChoSiren.UI.SirenStickerUi.Cyan,
                0, 0, 640, 96, TextAnchor.UpperCenter, FontStyle.Bold);
            resultRewards.verticalOverflow = VerticalWrapMode.Overflow;
            resultRewards.lineSpacing = 1.1f;
            // .btn{left:18%;right:18%;background:var(--red);font-weight:900;transform:skewX(-10deg)}
            Image hit = kit.NewImage("ResultContinue", card, null, Color.clear);
            hit.raycastTarget = true;
            var done = hit.gameObject.AddComponent<Button>();
            done.transition = Selectable.Transition.None;
            done.onClick.AddListener(Close);
            var plateGo = new GameObject("RedSkew", typeof(RectTransform), typeof(CanvasRenderer), typeof(ChoSiren.UI.SkewedPlate1021));
            plateGo.transform.SetParent(hit.transform, false);
            var plate = plateGo.GetComponent<ChoSiren.UI.SkewedPlate1021>();
            plate.color = Brand1025.Teal; plate.raycastTarget = false;
            PanelKit.Stretch(plate.rectTransform);
            Text doneLabel = kit.NewPlacedText(hit.transform, "继续", 38, Brand1025.Ink, 0, 0, 10, 10,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            PanelKit.Stretch(doneLabel.rectTransform); doneLabel.raycastTarget = false;
            recoveryActions = kit.NewRect("RecoveryActions", card).gameObject;
            string[] labels = { "训练升级", "检查装备", "调整编队", "返回关卡" };
            string[] routes = { "training", "accessory", "team", "farm" };
            for (int i = 0; i < routes.Length; i++)
            {
                string route = routes[i];
                GameObject action = PunkUi.Button(kit, recoveryActions.transform, "Recovery-" + route, labels[i], 24,
                    () => OpenRecovery(route), i == 0 ? PunkButtonStyle.Primary : PunkButtonStyle.Outline);
                PanelKit.PlaceTop(action.GetComponent<RectTransform>(), i % 2 * 320, i / 2 * 76, 300, 64);
                ChoSiren.UI.SirenStickerUi.SkinButton(action, i == 0);
            }
            recoveryActions.SetActive(false);
            GameObject lootButton = PunkUi.Button(kit, card, "VictoryEquipment", "查看获得的装备", 19,
                () => OpenRecovery("accessory"), PunkButtonStyle.Outline);
            PanelKit.PlaceTop(lootButton.GetComponent<RectTransform>(), 0, 0, 10, 10);
            lootButton.SetActive(false);
            resultOverlay.SetActive(false);
        }

        private float resultHeroFraction = .58f, resultRowTop = -1f;

        private void LayoutResult(bool victory)
        {
            RectTransform host = resultOverlay.GetComponent<RectTransform>();
            float H = host.rect.height > 1f ? host.rect.height : 1536f;
            float W = host.rect.width > 1f ? host.rect.width : 720f;
            resultCardRect.localScale = Vector3.one;
            // Background: this stage's scene art (as in the mock-up), else the battle stage.
            Sprite scene = battle?.Stage != null ? ChoSiren.StoryArt1019.Load("Art/Story/Scenes1019/" + battle.Stage.Id) : null;
            resultBackdrop.sprite = scene != null ? scene : BattlePsdArt928.Sprite("stage");
            resultBackdrop.color = resultBackdrop.sprite != null ? Color.white : Color.clear;
            var cover = resultBackdrop.GetComponent<AspectRatioFitter>();
            if (resultBackdrop.sprite != null) cover.aspectRatio = resultBackdrop.sprite.rect.width / Mathf.Max(1f, resultBackdrop.sprite.rect.height);
            // Bottom-up so the button clears the home indicator on tall phones and short web frames.
            float buttonTop = H - Mathf.Max(56f, H * .04f) - 84f;
            float rewardTop = buttonTop - 150f;
            float rowTop = rewardTop - 212f;
            float starsTop = Mathf.Min(H * .645f, rowTop - 80f);
            resultHeroFraction = Mathf.Clamp((starsTop - 46f) / H - .06f, .42f, .58f);
            resultRowTop = rowTop;
            float top = Mathf.Max(H * .04f, 64f);
            PanelKit.PlaceTop(resultTitle.rectTransform, W * .05f, top, W * .9f, 96);
            resultTitle.rectTransform.pivot = new Vector2(0f, .5f);
            resultTitle.rectTransform.anchoredPosition = new Vector2(W * .05f, -(top + 48f));
            PanelKit.PlaceTop(resultTitleSub.rectTransform, W * .05f + 4f, top + 92f, 300, 44);
            PanelKit.PlaceTop(resultStars.rectTransform, 0, starsTop, W, 80);
            RectTransform reward = resultRewardScroll.transform.parent as RectTransform;
            PanelKit.PlaceTop(reward, (W - 640f) * .5f, rewardTop, 640, 140);
            PanelKit.PlaceTop(resultRewardScroll.transform as RectTransform, 0, 0, 640, 140);
            PanelKit.PlaceTop(recoveryActions.GetComponent<RectTransform>(), (W - 620f) * .5f, rewardTop - 8f, 620, 140);
            PanelKit.PlaceTop(resultCardRect.Find("ResultContinue") as RectTransform, W * .18f, buttonTop, W * .64f, 84);
        }

        /// <summary>
        /// Normal-speed pacing model used by tests and balancing: one meaningful player operation
        /// is paired with one enemy response, and both sides have a decision beat plus a result beat.
        /// Six to nine operations therefore occupy approximately 50-74 seconds.
        /// </summary>
        private static float EstimateNormalSpeedBattleSeconds(int meaningfulPlayerOperations)
        {
            int operations = Mathf.Max(0, meaningfulPlayerOperations);
            return BattleIntroSeconds + operations * BeatSeconds * 4f;
        }

        private static int ResolveDisplayedEnemyPhase(int simulatorPhase, IReadOnlyList<BattleEvent> events,
            int nextLogIndex)
        {
            int phase = Mathf.Clamp(simulatorPhase, 1, 3);
            if (events == null) return phase;

            // The simulator may cross both thresholds in one hit. Keep the HUD behind the next
            // unpresented phase event so players still see 1 -> 2 -> 3 in log order.
            for (int index = Mathf.Clamp(nextLogIndex, 0, events.Count); index < events.Count; index++)
            {
                BattleEvent pending = events[index];
                if (pending.Kind != BattleEventKind.PhaseChanged || pending.Phase <= 1) continue;
                return Mathf.Clamp(Mathf.Min(phase, pending.Phase - 1), 1, 3);
            }

            return phase;
        }

        private static string FormatBattleTimer(float elapsedSeconds)
        {
            float elapsed = Mathf.Max(0f, elapsedSeconds);
            if (elapsed <= 60f)
            {
                int remaining = Mathf.Max(0, Mathf.CeilToInt(60f - elapsed));
                return $"剩余 {remaining / 60:00}:{remaining % 60:00}";
            }

            int overtime = Mathf.CeilToInt(elapsed - 60f);
            return $"加时 +{overtime / 60:00}:{overtime % 60:00}";
        }

        // ------------------------------------------------------------------ main loop

        private IEnumerator MainLoop()
        {
            yield return PlayBossEntrance1102();
            if (closing) yield break;
            if (battle.IsTurnBased)
            {
                yield return TurnLoop();
                yield break;
            }
            if (battle.IsRealtime)
            {
                yield return RealtimeLoop();
                yield break;
            }
            while (!closing)
            {
                while (paused && !closing) yield return null;
                yield return PlayPendingEvents();
                if (battle.Outcome != BattleOutcome.Ongoing)
                {
                    if (bossPresentation != null)
                    {
                        bossPresentation.PlayOutcome(battle.Outcome == BattleOutcome.Victory);
                        yield return WaitBattleDelay(0.88f);
                    }
                    ShowResult();
                    yield break;
                }

                BattleUnit actor = battle.CurrentActor;
                if (actor == null)
                {
                    Notify("行动队列暂时无可用单位，正在等待恢复");
                    yield return WaitBattleDelay(0.1f);
                    continue;
                }

                SetActorHighlight(actor);
                if (actor.Side == BattleSide.Enemy || autoMode)
                {
                    actorText.text = actor.Side == BattleSide.Enemy
                        ? $"敌方行动：{actor.Definition.Name}"
                        : $"自动行动：{actor.Definition.Name}";
                    ClearSkillBar();
                    if (skillWaitingText != null)
                        skillWaitingText.text = actor.Side == BattleSide.Enemy
                            ? "对方行动中\n等待下一位成员登场"
                            : "自动演出中\n关闭右上角自动，即可手动操作";
                    if (previewText != null) previewText.text = "";
                    if (actor.Side == BattleSide.Player)
                    {
                        PrepareDiceTurn();
                        if (diceTurn.CanEnergyReroll) diceTurn.RerollSuggested(out _); // auto: at most 2 loose dice
                        AutoTuneDice();
                    }
                    BattleAction action = EnemyAi.Choose(battle, actor);
                    if (action != null && actor.Side == BattleSide.Player && diceTurn != null)
                        action.PowerMultiplierPermille = diceTurn.Hand.MultiplierPermille;
                    if (actor.Side == BattleSide.Enemy) BeginEnemyActionPresentation(actor, action);
                    yield return WaitBattleDelay(BeatSeconds);
                    if (closing) yield break;
                    if (action == null || !battle.TryAct(action, out _))
                    {
                        if (!TryFallbackAction(actor))
                        {
                            Notify("当前单位没有合法行动，已停止自动推进");
                            autoMode = false;
                            PanelKit.LabelOf(autoButton).text = "自动";
                            yield return WaitBattleDelay(0.1f);
                        }
                    }
                    else if (actor.Side == BattleSide.Player)
                    {
                        CompleteDiceTurn();
                    }

                    continue;
                }

                actorText.text = $"当前行动：{actor.Definition.Name} · 请选择技能";
                inputActor = actor;
                awaitingInput = true;
                PrepareDiceTurn();
                BuildSkillButtons(actor);
                while (awaitingInput && !closing) yield return null;
                inputActor = null;
            }
        }

        private IEnumerator RealtimeLoop()
        {
            awaitingInput = true;
            foreach (BattleUnit unit in battle.Units)
                if (unit.Side == BattleSide.Player) { inputActor = unit; break; }
            RefreshRealtimeCommands();
            RefreshDiceUi();
            // The transition constructs this panel before revealing it. Neither that covered
            // interval nor a pending hidden die may consume the opening throw or battle clock.
            while (!closing && EntryPresentationCovered) yield return null;
            if (closing) yield break;
            Sfx("dice", .8f); // One throw sound for the six-die opening, after the scene is visible.
            while (!closing && (EntryPresentationCovered || awaitingDiceLanding ||
                dicePresentations.Exists(item => item.Busy))) yield return null;
            yield return WaitBattleDelay(BattleIntroSeconds);
            while (!closing && battle.Outcome == BattleOutcome.Ongoing)
            {
                if (!paused && !EntryPresentationCovered && !ManualUltimateTimingActive)
                {
                    // A suspended browser must not instantly consume the encounter on returning.
                    realtimeFractionMs += Mathf.Min(Time.unscaledDeltaTime, 0.1f) * speed * ChoSiren.BattlePresentationOptions.Pace * 1000f;
                    int elapsedMs = Mathf.FloorToInt(realtimeFractionMs);
                    realtimeFractionMs -= elapsedMs;
                    // Dice rerolls are always a player decision; 自动/手动 only controls ultimates.
                    battle.AutoCastUltimates = autoMode;
                    battle.AdvanceRealtime(elapsedMs, false);
                    int previousActor = -1;
                    while (logCursor < battle.Log.Count)
                    {
                        BattleEvent entry = battle.Log[logCursor++];
                        if (entry.Kind == BattleEventKind.RoundStart) continue;
                        // All events are shown, but their animation durations don't stall the simulation.
                        PresentEvent(entry, previousActor);
                        previousActor = entry.ActorId;
                    }
                    battleElapsed = battle.ElapsedMilliseconds / 1000f;
                    if (battle.ElapsedMilliseconds >= nextRealtimeUiRefresh)
                    {
                        nextRealtimeUiRefresh = battle.ElapsedMilliseconds + 100;
                        RefreshAllCells();
                        RefreshRealtimeCommands();
                        RefreshDiceUi();
                    }
                }
                yield return null;
            }
            if (closing) yield break;
            awaitingInput = false;
            RefreshDiceUi();
            if (bossPresentation != null) bossPresentation.PlayOutcome(battle.Outcome == BattleOutcome.Victory);
            yield return WaitBattleDelay(0.88f);
            ShowResult();
        }

        private void RefreshRealtimeCommands()
        {
            // The top toggle is the single authority for ultimate release mode; the simulator keeps
            // all energy, so switching never refunds or double-spends a charge.
            battle.AutoCastUltimates = autoMode;
            RefreshRelicButtons1111(); // 2026-11-12 功能骰
            if (guardButton != null)
            {
                bool available = !paused && !closing && battle.TacticalActor != null;
                float guardCooldown = battle.GuardCooldownRemaining / 1000f;
                PanelKit.SetButtonState(guardButton, available && !battle.TacticalGuardUsed,
                    psdRoot != null ? Color.clear : new Color32(6, 21, 42, 240));
                // Refreshing availability must never paint a rectangle behind the alpha-cut PSD art.
                bool spent = battle.TacticalGuardUsed && guardCooldown <= 0;
                if (psdGuardRing != null)
                {
                    // 2026-10-19 PSD: a fixed badge on the right. Ready: bright; guarding: the
                    // cyan fill drains over 3 s; spent: greyed.
                    guardButton.SetActive(true);
                    guardButton.transform.SetAsLastSibling();
                    int guarded = battle.IsTurnBased ? battle.GuardedCount : 0;
                    if (battle.IsTurnBased) spent = battle.TacticalGuardUsed && guarded == 0;
                    psdGuardRing.fillAmount = battle.IsTurnBased ? guarded / 4f : guardCooldown > 0 ? guardCooldown / 3f : 0f;
                    float breathe = BattlePresentationOptions.ReduceMotion ? 1f : .85f + Mathf.Sin(Time.unscaledTime * 4f) * .15f;
                    // Spent stays readable on the dark stage: only slightly desaturated.
                    psdGuardArt.color = spent ? new Color(.78f, .76f, .86f, 1f) : new Color(breathe, breathe, 1f, 1f);
                    // Turn battles (2026-10-24): every member gets a shield that blocks one whole hit, then breaks.
                    guardHint.text = battle.IsTurnBased
                        ? guarded > 0 ? $"护盾 {guarded} 人" : spent ? "本场已使用" : "全员挡一击"
                        : guardCooldown > 0 ? $"守护中 {guardCooldown:0.0}秒" : spent ? "本场已使用" : "全队减伤50%";
                    guardHint.color = spent ? new Color(.7f, .7f, .78f, 1f) : Color.white;
                    if (psdGuardBeat != null) psdGuardBeat.Active = available && !battle.TacticalGuardUsed;
                }
                else
                {
                    if (psdGuardArt != null) psdGuardArt.color = spent ? new Color(.62f, .66f, .75f, .82f) : Color.white;
                    guardHint.text = guardCooldown > 0 ? $"守护 0/1 · {guardCooldown:0.0}秒"
                        : battle.TacticalGuardUsed ? "守护 0/1" : "守护 1/1 · 减伤50%";
                }
                if (commandFeedbackUntil > battle.ElapsedMilliseconds) eventText.text = commandFeedback;
                else eventText.text = "点敌人集火 · 点「队长」牌换队长";
            }
            if (inputActor == null || !inputActor.Alive)
            {
                inputActor = null;
                foreach (BattleUnit unit in battle.Units)
                    if (unit.Side == BattleSide.Player && unit.Alive) { inputActor = unit; break; }
            }
            if (inputActor == null)
            {
                ClearSkillBar();
                actorText.text = "全员倒下 · 演出结束";
                captainNameText.text = "无人可接任";
                captainEffectText.text = string.Empty;
                actorGlow.enabled = false;
                return;
            }
            if (displayedPerformerId != inputActor.Id)
            {
                ClearSkillBar();
                displayedPerformerId = inputActor.Id;
            }
            // One summary line replaces the two permanent per-skill countdown cards. Ability names
            // and descriptions live behind the explicit 角色能力 entry.
            if (skillWaitingText != null)
            {
                bool ultimateReady = battle.IsUltimateReady(inputActor);
                skillWaitingText.gameObject.SetActive(true);
                // The actor name and mode already head the strip above, so the slim bottom hint
                // only carries the ultimate state.
                skillWaitingText.text = "普攻与小技能自动 · 大招" +
                    (ultimateReady
                        ? autoMode ? "就绪 · 自动释放" : "就绪 · 点击满能量头像释放"
                        : "准备中");
                skillWaitingText.color = ultimateReady ? PanelKit.Gold : PanelKit.Muted;
                PanelKit.EnableBestFit(skillWaitingText, 15);
            }
            actorText.text = $"{inputActor.Definition.Name} · {(autoMode ? "自动战斗" : "手动战斗")}";
            BattleUnit focused = battle.FindUnit(battle.FocusTargetId);
            int readyUltimates = battle.ReadyUltimateCount;
            previewText.text = readyUltimates > 0 && !autoMode
                ? $"大招就绪 {readyUltimates}人"
                : focused != null && focused.Alive
                    ? $"集火：{focused.Definition.Name}"
                    : "点敌人集火";
            // Selection is not an attack. Every actual actor animates through ActionStarted.
            actorGlow.enabled = false;
            foreach (CellView cell in cells)
            {
                bool selected = cell.Unit != null && cell.Unit.Alive &&
                    (cell.Side == BattleSide.Player
                        ? cell.Unit.Id == inputActor.Id
                        : cell.Unit.Id == battle.FocusTargetId);
                // The captain keeps a persistent gold frame so command transfer is readable at a glance.
                bool isCaptain = cell.Side == BattleSide.Player && cell.Unit != null && cell.Unit.Alive &&
                    cell.Unit.Id == battle.CurrentLeaderId;
                cell.Outline.effectColor = selected ? new Color32(95, 219, 255, 150)
                    : isCaptain ? new Color32(255, 205, 110, 165)
                    : new Color32(166, 112, 255, 0);
            }
            BattleUnit captain = battle.FindUnit(battle.CurrentLeaderId);
            captainNameText.text = captain != null && captain.Alive ? $"当前队长 · {captain.Definition.Name}" : "无人可接任";
            captainEffectText.text = CaptainEffectLabel();
        }

        private IEnumerator PlayPendingEvents()
        {
            IReadOnlyList<BattleEvent> log = battle.Log;
            int previousActor = int.MinValue;
            while (logCursor < log.Count)
            {
                BattleEvent battleEvent = log[logCursor];
                logCursor++;
                float delay = PresentEvent(battleEvent, previousActor);
                previousActor = battleEvent.ActorId;
                if (delay > 0f) yield return WaitBattleDelay(delay);
                if (closing) yield break;
            }
        }

        private IEnumerator WaitBattleDelay(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration && !closing)
            {
                // Accumulate logical battle time instead of baking the speed into the initial
                // duration. This keeps the HUD wait, Boss coroutine and effects synchronized
                // even when the player changes 1x/2x halfway through an anticipation or hit.
                if (!paused) elapsed += BattleAnimationDelta();
                yield return null;
            }
        }

        private GameAudio gameAudio;

        /// <summary>
        /// The landing after the ultimate show: the skill name slams onto the enemy stage on a
        /// slanted black plate, the stage shakes hard and the flash fires (damage numbers follow).
        /// </summary>
        private void UltimateImpact(string title, Color accent)
        {
            if (psdRoot == null) return;
            Sfx("crit");
            if (!BattlePresentationOptions.ReduceMotion)
            {
                // 2026-11-07: the camera pushes in and pulls back first; the shake lands after it (one owner of the root).
                if (stageShake != null) { StopCoroutine(stageShake); stageShake = null; }
                UltimateCameraPunch1107();
            }
            StartCoroutine(UltimateTitleSlam(title, accent));
        }

        private IEnumerator UltimateTitleSlam(string title, Color accent)
        {
            var plateRect = kit.NewRect("UltimateImpactTitle", psdRoot);
            PsdPlace(plateRect, -200, 1650, 3346, 520);
            plateRect.localEulerAngles = new Vector3(0, 0, -7f);
            var edge = kit.NewImage("Edge", plateRect, null, accent); edge.raycastTarget = false;
            PanelKit.Stretch(edge.rectTransform); edge.rectTransform.offsetMin = new Vector2(0, -6); edge.rectTransform.offsetMax = new Vector2(0, 6);
            var plate = kit.NewImage("Plate", plateRect, null, new Color32(6, 3, 12, 240)); plate.raycastTarget = false;
            PanelKit.Stretch(plate.rectTransform);
            var text = kit.NewPlacedText(plateRect, title, 44, Color.white, 0, 0, 10, 10, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic);
            PanelKit.Stretch(text.rectTransform); text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow; text.verticalOverflow = VerticalWrapMode.Overflow;
            var outline = text.gameObject.AddComponent<Outline>(); outline.effectColor = accent; outline.effectDistance = new Vector2(3, -3);
            var group = plateRect.gameObject.AddComponent<CanvasGroup>(); group.blocksRaycasts = false;
            float t = 0f;
            while (t < 1.1f && !closing)
            {
                if (!paused) t += Time.unscaledDeltaTime;
                float slam = Mathf.Clamp01(t / .12f);
                plateRect.localScale = new Vector3(1f, Mathf.Lerp(.1f, 1f, slam), 1f);
                text.transform.localScale = Vector3.one * Mathf.Lerp(1.8f, 1f, 1f - Mathf.Pow(1f - slam, 3f));
                group.alpha = t < .85f ? 1f : 1f - (t - .85f) / .25f;
                yield return null;
            }
            Destroy(plateRect.gameObject);
        }
        private Coroutine stageShake;
        private Vector2? stageRest;

        /// <summary>A short decaying jolt of the whole stage for critical hits.</summary>
        private IEnumerator ShakeStage(float duration, float strength, Vector2 kick = default)
        {
            var fitter = psdRoot.GetComponent<BattlePsdViewportFit1019>();
            stageRest = fitter != null ? fitter.RestPosition : psdRoot.anchoredPosition;
            // 2026-10-21 VFX pass: a damped kick along the blow instead of random jitter.
            Vector2 dir = kick.sqrMagnitude > .01f ? kick.normalized : UnityEngine.Random.insideUnitCircle.normalized;
            if (dir.sqrMagnitude < .01f) dir = Vector2.right;
            Vector2 side = new Vector2(-dir.y, dir.x);
            float elapsed = 0f;
            while (elapsed < duration && !closing)
            {
                if (!paused && !BattleHitStop.Frozen) elapsed += Time.unscaledDeltaTime;
                if (fitter != null) stageRest = fitter.RestPosition;
                float k = Mathf.Clamp01(1f - elapsed / duration);
                float swing = Mathf.Cos(elapsed * Mathf.PI * 2f * 15f);
                psdRoot.anchoredPosition = stageRest.Value + dir * (strength * k * k * swing)
                    + side * (strength * .2f * k * Mathf.Sin(elapsed * Mathf.PI * 2f * 23f));
                yield return null;
            }
            psdRoot.anchoredPosition = fitter != null ? fitter.RestPosition : stageRest.Value;
            stageRest = null;
            stageShake = null;
        }

        private void Sfx(string name, float volume = 1f)
        {
            if (!closing) gameAudio?.PlaySfx(name, volume);
        }

        private float BattleAnimationDelta()
        {
            return BattleHitStop.Delta(Time.unscaledDeltaTime) * Mathf.Clamp(speed, 1, 3) * ChoSiren.BattlePresentationOptions.Pace;
        }

        private bool TryFallbackAction(BattleUnit actor)
        {
            if (actor == null) return false;
            for (int skillIndex = 0; skillIndex < actor.Definition.SkillIds.Count; skillIndex++)
            {
                string skillId = actor.Definition.SkillIds[skillIndex];
                List<(int Row, int Col)> legal = battle.LegalAnchors(actor, skillId);
                if (legal.Count == 0) continue;
                BattleAction fallback = new BattleAction
                {
                    ActorId = actor.Id,
                    SkillId = skillId,
                    Row = legal[0].Row,
                    Col = legal[0].Col,
                    PowerMultiplierPermille = actor.Side == BattleSide.Player && diceTurn != null
                        ? diceTurn.Hand.MultiplierPermille
                        : 1000,
                };
                if (!battle.TryAct(fallback, out _)) continue;
                if (actor.Side == BattleSide.Player) CompleteDiceTurn();
                return true;
            }
            return false;
        }

        /// <summary>2026-11-03: a member's two active skills show her own names; everything else its skill name.</summary>
        private string MemberSkillTitle1103(BattleUnit actor, string skillId)
        {
            if (string.IsNullOrEmpty(skillId)) return null;
            if (actor != null && actor.Side == BattleSide.Player)
            {
                if (skillId == battle.ActiveSkillId(actor, true)) return MemberSkillNames1103.Name(actor.Definition.Id, battle.RaceOf(actor), true);
                if (skillId == battle.ActiveSkillId(actor, false)) return MemberSkillNames1103.Name(actor.Definition.Id, battle.RaceOf(actor), false);
            }
            return battle.LookupSkill(skillId)?.Name;
        }

        /// <summary>Applies one log entry to the view and returns how long to pause after it.</summary>
        private float PresentEvent(BattleEvent battleEvent, int previousActor)
        {
            BattleUnit actor = battleEvent.ActorId >= 0 ? battle.FindUnit(battleEvent.ActorId) : null;
            BattleUnit target = battleEvent.TargetId >= 0 ? battle.FindUnit(battleEvent.TargetId) : null;
            SkillDefinition skill = string.IsNullOrEmpty(battleEvent.SkillId) ? null : battle.LookupSkill(battleEvent.SkillId);
            string actorName = actor != null ? actor.Definition.Name : "——";
            string targetName = target != null ? target.Definition.Name : "——";
            string skillName = MemberSkillTitle1103(actor, battleEvent.SkillId) ?? "技能";
            bool sameAction = previousActor == battleEvent.ActorId && battleEvent.Kind != BattleEventKind.RoundStart;

            ObservePsdSkillCue1019(battleEvent, actor);
            ObserveEnemyBark1031(battleEvent, actor, target); // 2026-10-31: enemy taunt bubbles (EnemyBarks1031)
            if (battleEvent.Kind == BattleEventKind.Buff && battleEvent.SkillId == BattleSimulator.DiceTargetSkillId)
            {
                // 2026-10-21: the wave's hand target was reached — 队长演出 stamp + extra strike.
                Sfx("stamp", .9f);
                if (diceHandText != null && psdRoot != null)
                {
                    // 2026-10-23 review #35: a designed P5 banner instead of a red strip under plain text.
                    RectTransform hand = diceHandText.rectTransform;
                    Vector2 at = (Vector2)psdRoot.InverseTransformPoint(hand.TransformPoint(hand.rect.center)) - psdRoot.rect.center;
                    CaptainShowBanner1023.Play(psdRoot, at + new Vector2(0f, 330f), kit.Font,
                        "目标 ✓", () => paused || closing); // 2026-11-02 visual-battle: the target chip shows which hand
                    PresentTargetReached1101();
                }
                else if (diceHandText != null)
                    SpawnPopupAt(diceHandText.rectTransform, -102, "目标达成 · 队长演出！", PanelKit.Gold, 30, 40f);
                AppendLog($"骰子目标达成：{battle.DiceTargetLabel} → {actorName} 追加演出");
                RefreshDiceUi();
                return .25f;
            }
            switch (battleEvent.Kind)
            {
                case BattleEventKind.ActionStarted:
                    PresentAction(actor, target, battleEvent);
                    return 0f;
                case BattleEventKind.LeaderChanged:
                    RefreshRealtimeCommands();
                    string transfer = target != null ? $"{actorName} 倒下 · {targetName} 接任队长" : "全员倒下 · 无人接任";
                    eventText.text = transfer;
                    AppendLog(transfer);
                    Notify(transfer);
                    return 0f;
                case BattleEventKind.Spawned:
                    RefreshAllCells();
                    PresentPsdWave1019();
                    if (psdWave1019 == null || !psdWave1019.IsShowing)
                        Notify($"第 {battle.CurrentWave}/{battle.TotalWaves} 波 · {targetName} 加入战斗");
                    AppendLog($"增援：{targetName}");
                    return 0f;
                case BattleEventKind.RoundStart:
                    turnText.text = $"第 {battleEvent.Amount} 回合";
                    AppendLog($"—— 第 {battleEvent.Amount} 回合 ——");
                    StartCoroutine(FlashRound(battleEvent.Amount));
                    RefreshAllCells();
                    return 0.35f;
                case BattleEventKind.Damage:
                    hitDeferred1021 = false;
                    PresentHitEffect(actor, target, battleEvent);
                    RefreshCell(target);
                    if (actor != null && actor.Side == BattleSide.Player)
                    {
                        // Actual-hit energy comes from the simulator; the view only reflects it.
                        CellView attackerCell = FindCell(actor);
                        if (attackerCell != null)
                        {
                            attackerCell.Energy?.Refresh();
                            attackerCell.Energy?.FlashGain();
                            attackerCell.StatusIcons?.Refresh();
                        }
                    }
                    CellView damagedCell = FindCell(target);
                    if (damagedCell != null && damagedCell.Motion != null) damagedCell.Motion.PlayHit(battleEvent.Critical);
                    if (damagedCell != null) damagedCell.Root.GetComponentInChildren<BattleGuardRing929>(true)?.PulseHit();
                    // Flying strikes land later: their impact (flash, freeze, shake, sound) waits for contact.
                    if (!hitDeferred1021) LandHit1021(actor, target, battleEvent);
                    bool presentedByBossLayer = target != null && target.Side == BattleSide.Enemy &&
                                                target.Id == bossUnitId && bossPresentation != null;
                    if (presentedByBossLayer)
                    {
                        // The boss presentation owns its floating damage numbers.  Showing the generic
                        // cell popup as well makes one hit look like two separate damage events.
                        bossPresentation.DecoratedHits = psdRoot == null;   // the PSD battle draws Fx1023 on the boss instead
                        bossPresentation.FloatingNumbers = !SpawnBigDamage1101(actor, target, battleEvent); // 2026-11-01 big numbers
                        PlayBossHit1031(actor, target, battleEvent);
                    }
                    else if (!SpawnBigDamage1101(actor, target, battleEvent)) // 2026-11-01: our hits on enemies → big numbers
                    {
                        bool fullyAbsorbed = battleEvent.AbsorbedAmount > 0 && DamageHpLoss(battleEvent) <= 0;
                        Color damageColor = fullyAbsorbed ? CombatFeedbackPalette.Shield
                            : battleEvent.SkillId == "rt-poison" ? CombatFeedbackPalette.Debuff
                            : battleEvent.Critical ? CombatFeedbackPalette.Critical
                            : target != null && target.Side == BattleSide.Player
                                ? CombatFeedbackPalette.Hurt : CombatFeedbackPalette.Attack;
                        if (battleEvent.AbsorbedAmount > 0)
                            SpawnPopupIcon(target, DamagePopupLabel(battleEvent), damageColor,
                                DamageFont1023(target, battleEvent.Critical), SkillIconKind.Shield);
                        else if (battleEvent.SkillId == "rt-poison")
                            SpawnPopupIcon(target, DamagePopupLabel(battleEvent), damageColor,
                                DamageFont1023(target, battleEvent.Critical), SkillIconKind.Poison);
                        else
                            SpawnPopup(target, DamagePopupLabel(battleEvent), damageColor,
                                DamageFont1023(target, battleEvent.Critical));
                    }
                    var damageNotes = new List<string>();
                    if (battleEvent.Critical) damageNotes.Add("暴击");
                    if (battleEvent.DiceMultiplierPermille > 1000)
                        damageNotes.Add($"骰子 ×{battleEvent.DiceMultiplierPermille / 1000f:0.##}");
                    if (battleEvent.AbsorbedAmount > 0)
                        damageNotes.Add($"护盾吸收 {battleEvent.AbsorbedAmount}");
                    string damageLine = $"{actorName} 使用「{skillName}」对 {targetName} 造成 {battleEvent.Amount} 伤害" +
                        (damageNotes.Count > 0 ? $"（{string.Join(" · ", damageNotes)}）" : string.Empty);
                    if (!battle.IsRealtime) eventText.text = damageLine;
                    AppendLog(damageLine);
                    return sameAction ? 0.14f : BeatSeconds;
                case BattleEventKind.Heal:
                    if (battleEvent.Amount > 0) Sfx("heal", .7f);
                    if (psdRoot == null && battleEvent.Amount > 0) skillEffects?.Play(UnitVisual(target), SkillVisualKind.Heal,
                        CombatFeedbackPalette.Heal, true);
                    RefreshCell(target);
                    if (battleEvent.Amount > 0) PlayHealBurst1026(target);
                    SpawnPopupIcon(target, $"+{battleEvent.Amount}", CombatFeedbackPalette.Heal, psdRoot != null ? 30 : 22,
                        SkillIconKind.Heal);
                    string healLine = $"{actorName} 使用「{skillName}」为 {targetName} 恢复 {battleEvent.Amount}";
                    if (!battle.IsRealtime) eventText.text = healLine;
                    AppendLog(healLine);
                    return sameAction ? 0.14f : BeatSeconds;
                case BattleEventKind.Shield:
                    if (battleEvent.Amount > 0) Sfx("shield", .7f);
                    if (battleEvent.Amount > 0) skillEffects?.Play(UnitVisual(target), SkillVisualKind.Shield,
                        CombatFeedbackPalette.Shield, true);
                    RefreshCell(target);
                    SpawnPopupIcon(target, $"+{battleEvent.Amount}", CombatFeedbackPalette.Shield, 20,
                        SkillIconKind.Shield);
                    string shieldLine = $"{actorName} 使用「{skillName}」为 {targetName} 施加 {battleEvent.Amount} 护盾";
                    if (!battle.IsRealtime) eventText.text = shieldLine;
                    AppendLog(shieldLine);
                    return sameAction ? 0.14f : BeatSeconds;
                case BattleEventKind.Buff:
                    if (battleEvent.SkillId == BattleSimulator.GuardBreakSkillId)
                    {
                        // The 应急守护 shield took this hit in full and breaks (turn battles, 2026-10-24).
                        RefreshCell(target);
                        FindCell(target)?.Root.GetComponentInChildren<BattleGuardRing929>(true)?.Shatter();
                        Sfx("block", .9f);
                        if (!PresentStatusPicture1101(battleEvent, actor, target))
                            SpawnPopupIcon(target, "格挡！", CombatFeedbackPalette.Shield, 26, SkillIconKind.Shield);
                        AppendLog($"{targetName} 的守护盾挡下 {battleEvent.Amount} 伤害后碎裂");
                        return sameAction ? 0.14f : BeatSeconds;
                    }
                    RefreshCell(target);
                    string effectLabel = battleEvent.SkillId.StartsWith("rt-") ? skillName : skill != null ? EffectShort(skill.Effect) : "状态";
                    effectLabel = PresentTelegraph1101(battleEvent, target, effectLabel); // 2026-11-01: name the charged skill (Balance1101)
                    Color effectColor = CombatFeedbackPalette.Skill(battleEvent.SkillId);
                    if (!PresentStatusPicture1101(battleEvent, actor, target)) // 2026-11-02: PSD symbols, not words
                    SpawnPopupIcon(target, effectLabel, effectColor, 20,
                        battleEvent.SkillId.StartsWith("rt-")
                            ? CombatStatusIconVisuals.ForSkill(battleEvent.SkillId)
                            : CombatStatusIconVisuals.ForStatusText(effectLabel));
                    if (battleEvent.SkillId == "rt-seal") SnapSealChains1026(target);
                    if (battleEvent.SkillId == "rt-stun" || battleEvent.SkillId == "rt-interrupt" ||
                        battleEvent.SkillId == "rt-armor-break" || battleEvent.SkillId == "rt-seal") Sfx("debuff", .6f);
                    if (battleEvent.SkillId == "rt-stun" || battleEvent.SkillId == "rt-interrupt")
                        skillEffects?.Play(UnitVisual(target), SkillVisualKind.Hex, effectColor, true);
                    else if (battleEvent.SkillId == "rt-armor-break")
                        skillEffects?.Play(UnitVisual(target), SkillVisualKind.Pierce, effectColor, true);
                    else if (battleEvent.SkillId == "rt-cleanse")
                        skillEffects?.Play(UnitVisual(target), SkillVisualKind.Heal, effectColor, true);
                    else if (battleEvent.SkillId == "rt-tactical-guard")
                        skillEffects?.Play(UnitVisual(target), SkillVisualKind.Shield, CombatFeedbackPalette.Shield, true);
                    string buffLine = $"{actorName} 使用「{skillName}」：{targetName} {effectLabel}";
                    if (!battle.IsRealtime) eventText.text = buffLine;
                    AppendLog(buffLine);
                    return sameAction ? 0.14f : BeatSeconds;
                case BattleEventKind.Defeated:
                    RefreshCell(target);
                    CellView cell = FindCell(target);
                    if (target != null && defeatedPresentations.Add(target.Id))
                    {
                        if (target.Side == BattleSide.Enemy) Sfx("ko");
                        else PresentMemberDefeat1031(target); // 2026-10-31 阵亡台词 + 字幕 (VoiceDefeat1031)
                        // 2026-10-21: enemies break apart (white flash → erosion + shards + 「击破」) in the PSD battle.
                        Image koFigure = null;
                        // 2026-11-12: the boss is drawn by bossPresentation, not enemyFigures — she shatters and leaves too.
                        bool bossFell = target.Side == BattleSide.Enemy && target.Id == bossUnitId && bossPresentation != null;
                        if (bossFell) koFigure = BossPortrait1112();
                        bool broke = target.Side == BattleSide.Enemy && psdRoot != null
                            && (bossFell ? koFigure != null : enemyFigures.TryGetValue(target.Id, out koFigure))
                            && KoBreak1022.Play(psdRoot, koFigure, kit.Font, () => paused || closing, battle.Outcome == BattleOutcome.Victory) != null;
                        if (broke) Fx1023Host().Defeat(koFigure.rectTransform);
                        // The final blow lands harder: a longer hit-stop and a P5 impact frame on the last enemy.
                        if (broke && battle.Outcome == BattleOutcome.Victory)
                        {
                            BattleHitStop.Freeze(.2f);
                            RectTransform lastRect = UnitVisual(target);
                            Vector2 lastLocal = lastRect != null
                                ? (Vector2)psdRoot.InverseTransformPoint(lastRect.TransformPoint(lastRect.rect.center)) - psdRoot.rect.center
                                : Vector2.zero;
                            ImpactFrame1022.Play(psdRoot, lastLocal, new[] { koFigure }, () => paused || closing);
                        }
                        if (bossFell) bossPresentation.PlayDeath(broke);
                        else if (enemyMotions.TryGetValue(target.Id, out var defeated)) defeated.PlayDefeat(broke);
                        // A fallen member keeps her portrait (black-and-white, see ShowFallen1023): only a hard flinch.
                        else if (cell?.Motion != null && cell.Side == BattleSide.Player && psdRoot != null) cell.Motion.PlayHit(true);
                        else if (cell?.Motion != null) cell.Motion.PlayDefeat();
                        if (cell != null && cell.Side == BattleSide.Enemy) cell.Root.SetActive(false);
                    }
                    eventText.text = $"{targetName} 倒下";
                    AppendLog(eventText.text);
                    return 0.3f;
                case BattleEventKind.PhaseChanged:
                    int phase = Mathf.Clamp(battleEvent.Phase, 1, 3);
                    string phaseMessage = phase >= 3
                        ? "阶段 3/3 · 最终乐章"
                        : "阶段 2/3 · 敌方增幅";
                    phaseText.text = $"阶段 {phase}/3";
                    eventText.text = phaseMessage;
                    AppendLog(phaseMessage);
                    // A lethal hit may enqueue threshold events before Finished.  Keep the ordered
                    // HUD/log update, but never make a 0-HP boss stand back up to transform twice.
                    // The turn loop waits on the phase card; the boss surge plays once the card has torn away.
                    bool phaseCard = battle.Outcome == BattleOutcome.Ongoing && psdWave1019 != null && psdWave1019.PlayPhase(phase);
                    if (phaseCard) Sfx("cut", .8f);
                    if (battle.Outcome == BattleOutcome.Ongoing && bossPresentation != null)
                    {
                        if (phaseCard) StartCoroutine(PhaseSurgeAfterCard1022(phase));
                        else bossPresentation.PlayPhaseSurge(phase);
                    }
                    StartCoroutine(FlashPhase(phase));
                    return 0.45f;
                case BattleEventKind.Finished:
                    if (battleEvent.Outcome == BattleOutcome.Victory) Sfx("victory", .9f);
                    AppendLog(battleEvent.Outcome == BattleOutcome.Victory ? "战斗胜利" : "战斗失败");
                    eventText.text = battleEvent.Outcome == BattleOutcome.Victory ? "敌方全灭，演出成功"
                        : battle.IsRealtime ? $"我方全灭或已达到{battle.Stage.TimeLimitSeconds}秒时限" : "我方全灭或超出回合上限";
                    RefreshAllCells();
                    return 0.4f;
                default:
                    return 0f;
            }
        }

        private void AppendLog(string line)
        {
            logHistory.Add(line);
            while (logHistory.Count > LogLines) logHistory.RemoveAt(0);
            logBuilder.Length = 0;
            for (int index = 0; index < logHistory.Count; index++)
            {
                if (index > 0) logBuilder.Append('\n');
                logBuilder.Append(logHistory[index]);
            }

            logText.text = logBuilder.ToString();
        }

        /// <summary>Captain command readout with the values the current hand actually applies.</summary>
        private string CaptainEffectLabel()
        {
            if (battle?.BattleDice?.Hand == null) return "接任不刷新骰子、能量或冷却";
            CombatRace race = ReadoutLeaderRace();
            DicePattern pattern = battle.BattleDice.Hand.Pattern;
            switch (race)
            {
                case CombatRace.Demon:
                    int poison = BattleSimulator.PoisonLayers(pattern);
                    return poison > 0
                        ? $"恶魔指挥 · 当前骰型普攻叠毒 {poison} 层"
                        : "恶魔指挥 · 当前骰型无叠毒，重投可提高层数";
                case CombatRace.Charm:
                    int combo = BattleSimulator.Combo(pattern);
                    return combo > 0
                        ? $"魅族指挥 · 当前骰型追击 {combo} 次"
                        : "魅族指挥 · 当前骰型无追击，重投可提高次数";
                case CombatRace.Mermaid:
                    // Selective reroll is a shared battle rule for every captain; only the
                    // mermaid's own dice benefits belong in the race-command readout.
                    int shield = BattleSimulator.ShieldPercent(pattern);
                    return shield > 0
                        ? $"人鱼指挥 · 当前骰型护盾 +{shield / 10f:0.#}% · 五同减伤"
                        : "人鱼指挥 · 当前骰型无护盾 · 五同减伤";
                case CombatRace.BloodElf:
                    int pierce = BattleSimulator.Pierce(pattern);
                    return pierce > 0
                        ? $"血精灵指挥 · 当前骰型穿甲 {pierce / 10f:0.#}% · 残血收割"
                        : "血精灵指挥 · 当前骰型无穿甲 · 残血收割";
                default:
                    return "接任不刷新骰子、能量或冷却";
            }
        }

        private static Color PerformerColor(CombatRace race)
        {
            switch (race)
            {
                case CombatRace.Demon: return new Color32(193, 113, 255, 255);
                case CombatRace.Mermaid: return new Color32(90, 223, 255, 255);
                case CombatRace.BloodElf: return new Color32(255, 205, 110, 255);
                default: return new Color32(255, 106, 211, 255);
            }
        }

        private int ultimateStrikeCount;
        private int lastCaptainBonusPermille;
        private void PresentAction(BattleUnit actor, BattleUnit target, BattleEvent entry)
        {
            if (actor == null) return;
            ultimateStrikeCount = 0;
            string stamp = entry.TimeMilliseconds + ":" + entry.SkillId;
            if (lastPresentedActions.TryGetValue(actor.Id, out string previous) && previous == stamp) return;
            lastPresentedActions[actor.Id] = stamp;
            QueueBattleVoice(actor, entry);
            AimForAction1111(actor, target); // 2026-11-11: the wide stage looks at who is hit / who acts
            bool heavy = entry.SkillId != "rt-basic";
            string title = MemberSkillTitle1103(actor, entry.SkillId) ?? (heavy ? "施放技能" : "普攻");
            CellView caster = FindCell(actor);
            bool ultimate = actor.Side == BattleSide.Player && entry.SkillId == battle.ActiveSkillId(actor, true);
            if (actor.Side == BattleSide.Player && caster != null)
            {
                // The local skill name and semantic icon identify passive casts; avoid a
                // second floating copy of the character when several skills trigger together.
                // 2026-10-31: the PSD card's bust acts out every cast itself (HeroLunge1031).
                if (!PlayHeroLunge1031(caster, actor, entry.SkillId) && (psdRoot == null || !heavy)) caster.Motion?.PlayAttack(heavy);
                if (caster.ActionLabel != null && heavy && psdRoot == null) // PSD: the skill cue icon + close-up name it (2026-11-02)
                {
                    // Skills keep their local names. Ordinary attacks already have motion,
                    // a trajectory and a real damage number; repeating 普攻 adds visual noise.
                    caster.ActionLabel.text = ultimate ? "大招 · " + title : title;
                    caster.ActionLabel.color = ultimate ? PanelKit.Gold
                        : CombatFeedbackPalette.Skill(entry.SkillId, actor.Side);
                    caster.ActionLabelUntil = presentationClock + (heavy ? 1.05f : .4f);
                    caster.ActionLabel.gameObject.SetActive(true);
                }
                if (ultimate)
                {
                    if (actor.Id != ultimateShownFor) Sfx("ultimate");
                    else UltimateImpact(title, CombatFeedbackPalette.Skill(entry.SkillId, actor.Side));
                    // The album cut-in and local skill label already identify this cast.
                    // Do not repeat the same title in a third floating popup.
                    CancelPsdPresentation1019();
                    PresentUltimateVideoOrPortrait(actor, caster.Portrait.sprite, title,
                        CombatFeedbackPalette.Skill(entry.SkillId, actor.Side));
                    caster.Energy?.Refresh();
                    // 2026-10-21: the PSD battle's P5 impact frame replaces this ~1 s gold full-screen veil
                    // (it washed the whole stage, HUD and wave card yellow).
                    if (ultFlash != null && psdRoot == null) StartCoroutine(FlashUltimate(
                        CombatFeedbackPalette.Skill(entry.SkillId, actor.Side)));
                }
                else if (heavy)
                {
                    // 2026-10-26 (少爷/赵金: "有些技能是出现技能图标的不是立绘"): small skills show their PSD icon at the
                    // disc's top-right (SkillCues1019), no portrait close-up.
                    // 非大招技能：一枚语义图标闪现替代文字喊话，治疗/盾/毒/控场一眼可辨。
                    SpawnPopupIcon(actor, string.Empty,
                        CombatFeedbackPalette.Skill(entry.SkillId, actor.Side), 20,
                        CombatStatusIconVisuals.ForSkill(entry.SkillId));
                    // 2026-10-30 (用户): small skills get their face close-up back, alongside the icons.
                    Color skillColor = CombatFeedbackPalette.Skill(entry.SkillId, actor.Side);
                    if (psdRoot != null && psdCloseup1019 != null)
                    {
                        string cutinKey = BattleAuthoredCutin1019.KeyForMember(actor.Definition.Id);
                        Sprite cutArt = cutinKey != null ? BattlePsdArt928.Sprite(cutinKey) : null;
                        Sprite closeArt = cutArt ?? PanelKit.MemberSpriteOrNull(actor.Definition.Id, false)
                            ?? Resources.Load<Sprite>("Art/Members/Avatars/avatar-" + actor.Definition.Id) ?? caster.Portrait?.sprite;
                        psdCloseup1019.Play(closeArt, actor.Definition.Name, title, skillColor, cutArt != null, actor.Definition.Id);
                    }
                    else skillCutIn?.Enqueue(caster.Portrait?.sprite, actor.Definition.Name, title, skillColor);
                }
            }
            else if (enemyMotions.TryGetValue(actor.Id, out var motion))
            {
                motion.PlayAttack(heavy);
                // 小怪重击起手给一枚语义图标闪现（盾/毒/控场…），替代原先的文字喊话。
                if (heavy)
                    SpawnPopupIcon(actor, string.Empty,
                        CombatFeedbackPalette.Skill(entry.SkillId, actor.Side), 20,
                        CombatStatusIconVisuals.ForSkill(entry.SkillId));
            }
            else if (actor.Id == bossUnitId && heavy) bossPresentation?.PlayCharge(title);
            if (heavy && actor.Side == BattleSide.Player)
                skillEffects?.Play(UnitVisual(actor), SkillVisualKind.Cast, PerformerColor(battle.RaceOf(actor)));
            // The shared strip is reserved for encounter milestones, not a rapidly overwritten combat feed.
            AppendLog($"{actor.Definition.Name} · {title}");
        }

        private RectTransform UnitVisual(BattleUnit unit)
        {
            if (unit == null) return null;
            if (enemyFigures.TryGetValue(unit.Id, out Image image)) return image.rectTransform;
            if (unit.Id == bossUnitId)
                // The PSD layout moves the boss stage out of enemyStageRoot (Psd928), so look on the stage itself.
                return (bossPresentation != null ? bossPresentation.transform.Find("BossMotionRig")
                    : enemyStageRoot.Find("BossMotionRig")) as RectTransform;
            return FindCell(unit)?.Portrait.rectTransform;
        }

        private bool hitDeferred1021;
        private float ultimateStrikeFlash1021;

        /// <summary>
        /// The moment of contact (2026-10-21 VFX pass): layered light burst, a short hit-stop,
        /// the target's flinch, the hit sound and a directional shake — all on the same frame.
        /// </summary>
        private void LandHit1021(BattleUnit actor, BattleUnit target, BattleEvent entry)
        {
            if (closing || target == null) return;
            HeroLungeImpact1031(actor);
            bool towardsUs = target.Side == BattleSide.Player;
            if (towardsUs) Sfx(entry.AbsorbedAmount > 0 && DamageHpLoss(entry) <= 0 ? "block" : "hurt", .75f);
            else Sfx(entry.Critical ? "crit" : "hit", entry.Critical ? 1f : .8f);
            BossHitLayer1031(target, entry); // 2026-10-31: heavier boss impact layer
            // Layered hit (research §2.6): a quiet race "material" layer under our hits, a glitch tick under theirs.
            if (actor != null && actor.Side == BattleSide.Player && entry.SkillId != "rt-poison")
            {
                CombatRace race = battle.RaceOf(actor);
                Sfx("strike-" + (race == CombatRace.Demon ? "demon" : race == CombatRace.BloodElf ? "bloodelf"
                    : race == CombatRace.Mermaid ? "mermaid" : "charm"), entry.Critical ? .5f : .3f);
            }
            else if (towardsUs) Sfx("glitch", .25f);
            bool player = actor != null && actor.Side == BattleSide.Player;
            int power = player && entry.SkillId == battle.ActiveSkillId(actor, true) ? 2
                : entry.Critical || player && entry.SkillId == battle.ActiveSkillId(actor, false)
                  || entry.SkillId == "rt-enemy-heavy" || entry.SkillId == "rt-enemy-finale" ? 1 : 0;
            // Crits from our side flash the target black-and-white for two frames (light impact frame).
            if (enemyMotions.TryGetValue(target.Id, out var enemyHit)) enemyHit.PlayHit(entry.Critical, player && power >= 1, KickDir1021(actor, target), power);
            if (psdRoot == null || entry.SkillId == "rt-poison") return;
            Vector2 kick = KickDir1021(actor, target);
            // 2026-10-23: our side's hits use the shader VFX (Fx1023): slash / crit / race skill / ultimate.
            // Enemy hits on us keep the hostile cel impact and flipbook.
            if (player)
            {
                CombatRace hitRace = battle.RaceOf(actor);
                string race = hitRace == CombatRace.Demon ? "demon" : hitRace == CombatRace.BloodElf ? "bloodelf"
                    : hitRace == CombatRace.Mermaid ? "mermaid" : "charm";
                RectTransform hitVisual = UnitVisual(target);
                if (hitVisual != null)
                {
                    Fx1023 fx = Fx1023Host();
                    fx.ImpactFrames = power < 2;   // the ultimate's impact frame below stays with ImpactFrame1022
                    if (power == 2) fx.Ultimate(race, hitVisual);
                    else if (power == 1 && entry.SkillId == battle.ActiveSkillId(actor, false)) fx.Skill(race, hitVisual, null, 1f, false);
                    else if (entry.Critical) fx.Crit(race, hitVisual);
                    else fx.Slash(race, hitVisual);
                }
            }
            else
            {
                CelImpact1022.Play(psdRoot, UnitVisual(target), CelPalette.Enemy, power, kick, () => paused || closing);
                if (towardsUs)
                    Flipbook1022.Play(psdRoot, UnitVisual(target), "hit-enemy", power >= 1 ? 380f : 300f, true,
                        UnityEngine.Random.Range(-20f, 20f), () => paused || closing);
            }
            // Hit-stop 4 / 8 / 12 frames.
            BattleHitStop.Freeze(HitStopSeconds1031(target, power));
            if (towardsUs) EdgeFlash1022.Play(psdRoot, new Color32(255, 30, 70, 255));
            if (power == 2 && ultimateStrikeFlash1021 < Time.unscaledTime)
            {
                // One impact frame per ultimate, not one per AOE target.
                ultimateStrikeFlash1021 = Time.unscaledTime + .5f;
                RectTransform hitRect = UnitVisual(target);
                Vector2 hitLocal = hitRect != null
                    ? (Vector2)psdRoot.InverseTransformPoint(hitRect.TransformPoint(hitRect.rect.center)) - psdRoot.rect.center
                    : Vector2.zero;
                // Enemy figures only: member card portraits have opaque backgrounds and read as black boxes.
                var figures = new List<Image>(enemyFigures.Values);
                ImpactFrame1022.Play(psdRoot, hitLocal, figures, () => paused || closing);
            }
            if (BattlePresentationOptions.ReduceMotion) return;
            if (stageShake != null)
            {
                if (power == 0) return;
                StopCoroutine(stageShake);
            }
            stageShake = StartCoroutine(ShakeStage(power == 2 ? .3f : power == 1 ? .2f : .1f,
                ShakeStrength1031(target, power, towardsUs), kick));
        }

        private Fx1023 fx1023;
        /// <summary>The shader VFX layer: a full-stage child of the PSD battle root, kept above the stage art.</summary>
        private Fx1023 Fx1023Host()
        {
            if (fx1023 == null)
            {
                var go = new GameObject("Fx1023", typeof(RectTransform));
                var rt = (RectTransform)go.transform;
                rt.SetParent(psdRoot, false);
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
                fx1023 = Fx1023.Ensure(rt, null, () => paused || closing);
            }
            // Above the enemy scene and its tags, under the member cards, dice, cut-ins and overlays
            // (2026-10-23 review: the effects covered the skill close-up and the pause dialog).
            int under = psdRoot.childCount - 1;
            foreach (var c in cells)
                if (c.Side == BattleSide.Player && c.Root.transform.parent == psdRoot) under = Mathf.Min(under, c.Root.transform.GetSiblingIndex());
            Transform closeup = psdRoot.Find("ActiveSkillCloseup");
            if (closeup != null) under = Mathf.Min(under, closeup.GetSiblingIndex());
            if (fx1023.transform.GetSiblingIndex() < under) under--;
            fx1023.transform.SetSiblingIndex(Mathf.Max(0, under));
            return fx1023;
        }

        private Vector2 KickDir1021(BattleUnit actor, BattleUnit target)
        {
            RectTransform from = actor != null ? UnitVisual(actor) : null, to = UnitVisual(target);
            Vector2 fallback = target != null && target.Side == BattleSide.Player ? Vector2.down : Vector2.up;
            if (from == null || to == null || psdRoot == null) return fallback;
            Vector2 d = (Vector2)psdRoot.InverseTransformPoint(to.TransformPoint(to.rect.center))
                - (Vector2)psdRoot.InverseTransformPoint(from.TransformPoint(from.rect.center));
            return d.sqrMagnitude > 1f ? d.normalized : fallback;
        }

        private void PresentHitEffect(BattleUnit actor, BattleUnit target, BattleEvent entry)
        {
            if (actor == null || target == null || entry.Amount <= 0) return;
            if (entry.SkillId == "rt-poison")
            {
                if (psdRoot == null) skillEffects?.Play(UnitVisual(target), SkillVisualKind.Thorn, CombatFeedbackPalette.Debuff, false);
                return;
            }
            bool player = actor.Side == BattleSide.Player;
            bool special = player
                ? entry.SkillId == battle.ActiveSkillId(actor, false) || entry.SkillId == battle.ActiveSkillId(actor, true)
                : entry.SkillId == "rt-enemy-heavy" || entry.SkillId == "rt-enemy-finale";
            bool strong = entry.Critical || special && player && entry.SkillId == battle.ActiveSkillId(actor, true);
            CombatRace race = battle.RaceOf(actor);
            bool ultimate = player && entry.SkillId == battle.ActiveSkillId(actor, true);
            SkillVisualKind kind = !special ? SkillVisualKind.Impact
                : race == CombatRace.Demon ? (ultimate ? SkillVisualKind.Thorn : SkillVisualKind.Rift)
                : race == CombatRace.BloodElf ? (ultimate ? SkillVisualKind.Prism : SkillVisualKind.Pierce)
                : race == CombatRace.Mermaid ? SkillVisualKind.Tide
                : (ultimate ? SkillVisualKind.Hex : SkillVisualKind.Slash);
            Sprite art = kind == SkillVisualKind.Tide ? tideSpellSprite
                : kind == SkillVisualKind.Pierce ? lanceSpellSprite
                : kind == SkillVisualKind.Prism || kind == SkillVisualKind.Thorn ? crystalBurstSprite
                : kind == SkillVisualKind.Hex ? bossHeartImpactSprite
                : kind == SkillVisualKind.Slash ? bossHitSlashSprite : null;
            if (ultimate && psdRoot != null)
            {
                // 2026-10-20 (owner): the ultimate's own attack flies from her card into each
                // target and bursts there; repeated AOE hits are staggered.
                string key = race == CombatRace.Demon ? "demon" : race == CombatRace.BloodElf ? "bloodelf"
                    : race == CombatRace.Mermaid ? "mermaid" : "charm";
                float stagger = ultimateStrikeCount++ * .09f;
                // 2026-10-21: the ultimate uses the race's own move at full size + its big hit art.
                BasicStrike1021.Play(psdRoot, UnitVisual(actor), UnitVisual(target), key,
                    () => paused || closing, () =>
                    {
                        if (closing) return;
                        Sfx("strike-" + key);
                        LandHit1021(actor, target, entry);
                    }, 1f, stagger, key + "-hit", contact: false);   // the contact is Fx1023.Ultimate (LandHit1021)
                hitDeferred1021 = Application.isPlaying;
                return;
            }
            BattleUnit playerUnit = player ? actor : target;
            int slot = 0;
            foreach (BattleUnit unit in battle.Units)
            {
                if (unit.Side != BattleSide.Player) continue;
                if (unit.Id == playerUnit.Id)
                {
                    Sprite actorPortrait = player ? FindCell(actor)?.Portrait.sprite
                        : enemyFigures.TryGetValue(actor.Id, out Image figure) ? figure.sprite : userBossSprite;
                    if (!TryPsdBasic1019(actor, target, entry, () => LandHit1021(actor, target, entry))
                        && !TryEnemyStrike1021(actor, target, entry, () => LandHit1021(actor, target, entry))) attackTrajectories?.Play(UnitVisual(actor), UnitVisual(target), actorPortrait,
                        actor.Definition.Name, target.Definition.Name, player ? CombatFeedbackPalette.Attack : CombatFeedbackPalette.Hurt,
                        !player, special, slot);
                    // Pink HP-loss numbers, hit motion and the impact ring identify the
                    // injured member. A second 受击 label would repeat that same event.
                }
                slot++;
            }
            // The PSD battle uses the race strikes + cel impact; the old generic overlay stays for the legacy layout only.
            if (psdRoot == null) skillEffects?.Play(UnitVisual(target), kind, player ? PerformerColor(race) : PanelKit.Pink, strong, art);
        }

        private void BeginEnemyActionPresentation(BattleUnit actor, BattleAction action)
        {
            if (actor == null || actor.Side != BattleSide.Enemy || action == null) return;
            SkillDefinition skill = battle.LookupSkill(action.SkillId);
            string skillName = skill != null ? skill.Name : "终曲";
            eventText.text = $"{actor.Definition.Name} 正在蓄力「{skillName}」";
            bossPresentation?.PlayCharge(skillName);
        }

        private IEnumerator FlashRound(int round)
        {
            roundFlash.text = $"第 {round} 回合";
            roundFlash.gameObject.SetActive(true);
            const float duration = 0.6f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (paused)
                {
                    yield return null;
                    continue;
                }
                elapsed += BattleAnimationDelta();
                float t = Mathf.Clamp01(elapsed / duration);
                Color color = roundFlash.color;
                color.a = t < 0.3f ? t / 0.3f : 1f - (t - 0.3f) / 0.7f;
                roundFlash.color = color;
                float scale = Mathf.Lerp(0.85f, 1.05f, t);
                roundFlash.rectTransform.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }

            roundFlash.gameObject.SetActive(false);
            Color reset = roundFlash.color;
            reset.a = 1f;
            roundFlash.color = reset;
        }

        private IEnumerator PhaseSurgeAfterCard1022(int phase)
        {
            while (!closing && psdWave1019 != null && psdWave1019.IsShowing) yield return null;
            if (!closing && battle.Outcome == BattleOutcome.Ongoing && bossPresentation != null) bossPresentation.PlayPhaseSurge(phase);
        }

        private IEnumerator FlashPhase(int phase)
        {
            int version = ++phaseFlashVersion;
            Color accent = phase >= 3 ? PanelKit.Pink : PanelKit.Cyan;
            phaseText.color = accent;
            const float duration = 0.5f;
            float elapsed = 0f;
            while (elapsed < duration && version == phaseFlashVersion)
            {
                if (paused)
                {
                    yield return null;
                    continue;
                }

                elapsed += BattleAnimationDelta();
                float t = Mathf.Clamp01(elapsed / duration);
                float pulse = Mathf.Sin(t * Mathf.PI);
                phaseText.rectTransform.localScale = Vector3.one * (1f + pulse * 0.12f);
                phaseText.color = Color.Lerp(accent, PanelKit.White, pulse * 0.45f);
                yield return null;
            }

            if (version != phaseFlashVersion) yield break;
            phaseText.rectTransform.localScale = Vector3.one;
            phaseText.color = PanelKit.Gold;
        }

        private void PositionUltimateFlashBelowOverlays()
        {
            if (ultFlash == null || psdRoot == null || ultFlash.transform.parent != psdRoot.parent) return;
            // Moving from before the PSD removes a sibling and shifts its index down by one.
            // Place immediately after the opaque PSD, never at the end over cut-ins or modals.
            int afterPsd = psdRoot.GetSiblingIndex() + 1;
            if (ultFlash.transform.GetSiblingIndex() < afterPsd) afterPsd--;
            ultFlash.transform.SetSiblingIndex(afterPsd);
        }

        /// <summary>Ultimate cast pulse: a fast full-panel colour beat keyed to the skill palette.</summary>
        private IEnumerator FlashUltimate(Color accent)
        {
            int version = ++ultFlashVersion;
            if (ultFlash == null) yield break;
            if (BattlePresentationOptions.ReduceMotion)
            {
                ultFlash.color = new Color(accent.r, accent.g, accent.b, 0f);
                ultFlash.gameObject.SetActive(false);
                yield break;
            }
            PositionUltimateFlashBelowOverlays();
            const float duration = 0.42f;
            float elapsed = 0f;
            ultFlash.color = new Color(accent.r, accent.g, accent.b, 0f);
            ultFlash.gameObject.SetActive(true);
            while (elapsed < duration && version == ultFlashVersion)
            {
                // Check before the pause branch so enabling reduced motion while paused
                // immediately retires an in-flight flash as well.
                if (BattlePresentationOptions.ReduceMotion) break;
                if (paused)
                {
                    yield return null;
                    continue;
                }

                elapsed += BattleAnimationDelta();
                float t = Mathf.Clamp01(elapsed / duration);
                float pulse = Mathf.Sin(t * Mathf.PI);
                ultFlash.color = new Color(accent.r, accent.g, accent.b, pulse * 0.34f);
                yield return null;
            }

            if (version != ultFlashVersion) yield break;
            ultFlash.color = new Color(accent.r, accent.g, accent.b, 0f);
            ultFlash.gameObject.SetActive(false);
        }

        private IEnumerator FadeOut(CellView cell)
        {
            const float duration = 0.45f;
            float elapsed = 0f;
            cell.Fallen.gameObject.SetActive(true);
            float faded = FallenAlpha1023(cell);
            ShowFallen1023(cell, true);
            while (elapsed < duration)
            {
                if (paused)
                {
                    yield return null;
                    continue;
                }
                elapsed += BattleAnimationDelta();
                cell.Group.alpha = Mathf.Lerp(1f, faded, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            cell.Group.alpha = faded;
        }

        private static Material fallenMaterial1023;

        // 2026-10-23 review: a fallen member stays on her card in black-and-white and stops taking taps,
        // instead of fading to a near-empty sleeve. Enemy cells keep the old fade.
        private float FallenAlpha1023(CellView cell) => cell.Side == BattleSide.Player && psdRoot != null ? .85f : .38f;

        private void ShowFallen1023(CellView cell, bool fallen)
        {
            if (cell.Side != BattleSide.Player || psdRoot == null) return;
            cell.Group.interactable = !fallen; cell.Group.blocksRaycasts = !fallen;
            if (fallen && fallenMaterial1023 == null)
            {
                Shader gray = Resources.Load<Shader>("Shaders/UIGrayscale");
                if (gray != null) fallenMaterial1023 = new Material(gray) { hideFlags = HideFlags.DontSave };
            }
            if (cell.Portrait != null) cell.Portrait.material = fallen ? fallenMaterial1023 : null;
        }

        private IEnumerator AnimateCellImpact(CellView cell, bool critical)
        {
            if (cell == null || cell.Rect == null || !cell.Root.activeSelf) yield break;
            RectTransform rect = cell.Rect;
            if (!cellImpacts.TryGetValue(rect, out CellImpactState impact))
            {
                impact = new CellImpactState
                {
                    Origin = rect.anchoredPosition,
                    Scale = rect.localScale,
                    Background = cell.Background.color,
                };
                cellImpacts.Add(rect, impact);
            }
            else
            {
                // Multi-hit events can arrive every 0.14 seconds, before the previous shake ends.
                // Always restart from the canonical pose instead of baking an in-flight offset.
                rect.anchoredPosition = impact.Origin;
                rect.localScale = impact.Scale;
                cell.Background.color = impact.Background;
            }
            int version = ++impact.Version;
            Vector2 origin = impact.Origin;
            Vector3 originalScale = impact.Scale;
            Color originalColor = impact.Background;
            float duration = critical ? 0.44f : 0.34f;
            float elapsed = 0f;
            float strength = critical ? 9f : 6f;
            while (elapsed < duration && rect != null && cellImpacts.TryGetValue(rect, out CellImpactState current) &&
                   current.Version == version)
            {
                if (paused)
                {
                    yield return null;
                    continue;
                }

                elapsed += BattleAnimationDelta();
                float t = Mathf.Clamp01(elapsed / duration);
                float envelope = 1f - t;
                rect.anchoredPosition = origin + new Vector2(Mathf.Sin(t * Mathf.PI * 9f) * strength * envelope,
                    Mathf.Sin(t * Mathf.PI * 5f) * 3f * envelope);
                float pop = Mathf.Sin(t * Mathf.PI) * (critical ? 0.08f : 0.045f);
                rect.localScale = originalScale * (1f + pop);
                cell.Background.color = Color.Lerp(originalColor,
                    critical ? new Color32(255, 174, 73, 230) : new Color32(255, 66, 182, 210),
                    envelope * 0.55f);
                yield return null;
            }

            if (rect == null || !cellImpacts.TryGetValue(rect, out CellImpactState final) ||
                final.Version != version) yield break;
            rect.anchoredPosition = origin;
            rect.localScale = originalScale;
            cell.Background.color = originalColor;
            cellImpacts.Remove(rect);
        }

        /// <summary>HP actually removed by a hit. Events without attribution fall back to Amount.</summary>
        private static int DamageHpLoss(BattleEvent entry) =>
            entry.AbsorbedAmount > 0 || entry.HpLostAmount > 0 ? entry.HpLostAmount : entry.Amount;

        /// <summary>Floating number text that separates shield absorption from real HP loss.</summary>
        private readonly Dictionary<int, float> popupStamp1022 = new Dictionary<int, float>();

        /// <summary>Reads "-123" or "暴击 -123" (no shield part) back into an amount.</summary>
        private static bool TryParseDamage1022(string label, out int amount, out bool critical)
        {
            amount = 0; critical = false;
            if (string.IsNullOrEmpty(label) || label.Contains("护盾")) return false;
            string body = label;
            if (body.StartsWith("暴击 ", StringComparison.Ordinal)) { critical = true; body = body.Substring(3); }
            return body.StartsWith("-", StringComparison.Ordinal) && int.TryParse(body.Substring(1), out amount);
        }

        private static string DamagePopupLabel(BattleEvent entry)
        {
            int lost = DamageHpLoss(entry);
            string label = entry.AbsorbedAmount <= 0
                ? entry.Critical ? $"暴击 -{lost}" : $"-{lost}"
                : lost > 0 ? $"{(entry.Critical ? "暴击 " : string.Empty)}-{lost} 护盾-{entry.AbsorbedAmount}"
                : $"护盾吸收 {entry.AbsorbedAmount}";
            // Dice multiplier stays on the authoritative hand readout and in the log.
            // Each hit popup carries only HP loss and any separately absorbed shield amount.
            return label;
        }

        // 2026-10-23 review: damage numbers read too small — bigger on the enemies, a little bigger on the cards.
        private static int DamageFont1023(BattleUnit target, bool critical) =>
            target != null && target.Side == BattleSide.Player ? (critical ? 30 : 26) : (critical ? 38 : 30);

        private void SpawnPopup(BattleUnit target, string text, Color color, int fontSize)
        {
            CellView cell = FindCell(target);
            if (cell == null) return;
            bool lowerPlayerReading = psdRoot != null && target.Side == BattleSide.Player &&
                psdAttackBadges.TryGetValue(cell, out _);
            RectTransform visual = target.Side == BattleSide.Enemy ? UnitVisual(target)
                : lowerPlayerReading ? (RectTransform)psdAttackBadges[cell].transform : cell.Rect;
            SpawnPopupAt(visual ?? cell.Rect, target.Id, text, color, fontSize, lowerPlayerReading ? -20f : 22f);
        }

        /// <summary>Popup carrying a semantic icon (heal/shield/buff reads) beside the short text.</summary>
        private void SpawnPopupIcon(BattleUnit target, string text, Color color, int fontSize,
            SkillIconKind icon)
        {
            CellView cell = FindCell(target);
            if (cell == null) return;
            bool lowerPlayerReading = psdRoot != null && target.Side == BattleSide.Player &&
                psdAttackBadges.TryGetValue(cell, out _);
            RectTransform visual = target.Side == BattleSide.Enemy ? UnitVisual(target)
                : lowerPlayerReading ? (RectTransform)psdAttackBadges[cell].transform : cell.Rect;
            SpawnPopupAt(visual ?? cell.Rect, target.Id, text, color, fontSize, lowerPlayerReading ? -20f : 22f, icon);
        }

        /// <summary>Floating feedback anchored to any rect (used by dice/captain reroll readouts).</summary>
        private void SpawnPopupAt(RectTransform anchor, int ownerId, string text, Color color,
            int fontSize, float offsetY = 22f, SkillIconKind? icon = null)
        {
            if (anchor == null) return;
            int slot = -1;
            // Small portrait cards show the newest feedback for that target. Every underlying
            // hit remains in the combat log; overlapping text must not obscure the member.
            for (int i = 0; i < popupPool.Count; i++)
                if (popupOwners[i] == ownerId && popupPool[i].gameObject.activeSelf) { slot = i; break; }
            if (slot < 0) { slot = popupIndex; popupIndex = (popupIndex + 1) % popupPool.Count; }
            Text popup = popupPool[slot];
            if (popupRoutines[slot] != null)
            {
                StopCoroutine(popupRoutines[slot]);
                // The stopped routine never reaches its cleanup: drop its crit strip here, or it stays
                // on screen as a bare red bar (2026-10-23 review "红框错误").
                Transform stale = popupLayer.Find("CritStrip-" + slot);
                if (stale != null) Destroy(stale.gameObject);
            }
            // 2026-10-21 research §5.4: plain damage on the same target within 0.4 s rolls into one
            // running total that re-pops, instead of each number overwriting the last.
            if (ownerId >= 0 && TryParseDamage1022(text, out int amount, out bool critHit) && popup.gameObject.activeSelf &&
                popupOwners[slot] == ownerId && popupStamp1022.TryGetValue(slot, out float stamped1022) && Time.unscaledTime - stamped1022 < .4f &&
                TryParseDamage1022(popup.text, out int previous, out bool wasCrit))
            {
                amount += previous;
                bool crit = critHit || wasCrit;
                text = crit ? $"暴击 -{amount}" : $"-{amount}";
                if (crit) fontSize = Mathf.Max(fontSize, 30);
            }
            popupStamp1022[slot] = Time.unscaledTime;
            popupOwners[slot] = ownerId;
            popup.text = text;
            popup.color = color;
            popup.fontSize = fontSize;
            Vector3 world = anchor.TransformPoint(new Vector3(anchor.rect.center.x,
                anchor.rect.center.y + offsetY, 0));
            Vector2 local = popupLayer.InverseTransformPoint(world);
            popup.rectTransform.anchoredPosition = local - new Vector2(popupLayer.rect.xMin, popupLayer.rect.yMax);
            SkillIconGraphic popupIcon = popupIcons[slot];
            var authoredPopupIcon = popupIcon.GetComponent<BattlePsdEffectIcon1019>();
            authoredPopupIcon?.Clear(); // The same owner can reuse a heal slot for a plain damage number.
            Graphic animatedPopupIcon = popupIcon;
            if (icon.HasValue)
            {
                popupIcon.Kind = icon.Value;
                popupIcon.color = CombatStatusIconVisuals.ColorFor(icon.Value);
                if (authoredPopupIcon != null)
                    animatedPopupIcon = authoredPopupIcon.Apply(icon.Value, popupIcon.color);
                // The glyph sits just left of the text; icon-only reads stay centered.
                float half = string.IsNullOrEmpty(text) ? 0f
                    : popup.preferredWidth * .5f + popupIcon.rectTransform.sizeDelta.x * .5f + 3f;
                popupIcon.rectTransform.anchoredPosition = new Vector2(-half, 0f);
                popupIcon.gameObject.SetActive(true);
            }
            else popupIcon.gameObject.SetActive(false);
            popup.gameObject.SetActive(true);
            popup.transform.SetAsLastSibling();
            popupRoutines[slot] = StartCoroutine(AnimatePopup(popup, animatedPopupIcon, popupIcon));
        }

        /// <summary>
        /// P5 crit plate drawn just behind a crit number (removed with its popup): black offset shadow, red slanted
        /// strip and a small 「CRITICAL」 tag on its top-left corner (2026-10-23 review #38).
        /// </summary>
        private RectTransform CritStrip1022(Text popup)
        {
            Transform old = popupLayer.Find("CritStrip-" + popupPool.IndexOf(popup));
            if (old != null) Destroy(old.gameObject);
            var root = new GameObject("CritStrip-" + popupPool.IndexOf(popup), typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
            root.SetParent(popupLayer, false);
            root.SetSiblingIndex(popup.transform.GetSiblingIndex());
            root.GetComponent<CanvasGroup>().blocksRaycasts = false;
            root.anchorMin = popup.rectTransform.anchorMin; root.anchorMax = popup.rectTransform.anchorMax; root.pivot = new Vector2(.5f, .5f);
            var size = new Vector2(popup.preferredWidth + 40f, popup.fontSize * 1.3f);
            root.sizeDelta = size;
            root.localEulerAngles = new Vector3(0f, 0f, 7f);
            P5Letters1023.Plate(root, "Shadow", new Vector2(7f, -7f), size, new Color32(14, 8, 20, 235), -16f);
            P5Letters1023.Plate(root, "Strip", Vector2.zero, size, Brand1025.Teal, -16f);
            P5Letters1023.Plate(root, "Edge", new Vector2(0f, -size.y * .42f), new Vector2(size.x * .9f, 4f), new Color32(246, 240, 248, 255), -16f);
            P5Letters1023.Plate(root, "TagPlate", new Vector2(-size.x * .5f + 46f, size.y * .5f + 6f), new Vector2(92f, 20f), new Color32(14, 8, 20, 255), -16f);
            var label = new GameObject("Critical", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)).GetComponent<Text>();
            label.transform.SetParent(root, false);
            label.font = kit.Font; label.text = "CRITICAL"; label.fontSize = 15; label.fontStyle = FontStyle.BoldAndItalic;
            label.color = new Color32(255, 205, 110, 255); label.alignment = TextAnchor.MiddleCenter; label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow; label.verticalOverflow = VerticalWrapMode.Overflow;
            label.rectTransform.anchoredPosition = new Vector2(-size.x * .5f + 46f, size.y * .5f + 6f); label.rectTransform.sizeDelta = new Vector2(92f, 20f);
            return root;
        }

        private void ClearPopups()
        {
            if (bigDamage1101 != null) bigDamage1101.Clear();
            foreach (Transform child in popupLayer)
                if (child.name.StartsWith("CritStrip-", StringComparison.Ordinal)) Destroy(child.gameObject);
            for (int index = 0; index < popupPool.Count; index++)
            {
                if (popupRoutines[index] != null) StopCoroutine(popupRoutines[index]);
                popupRoutines[index] = null;
                popupOwners[index] = 0;
                popupPool[index].gameObject.SetActive(false);
                popupIcons[index].GetComponent<BattlePsdEffectIcon1019>()?.Clear();
                popupIcons[index].gameObject.SetActive(false);
            }
        }

        private IEnumerator AnimatePopup(Text popup, Graphic icon, SkillIconGraphic owner)
        {
            const float duration = 0.7f;
            float elapsed = 0f;
            Vector2 start = popup.rectTransform.anchoredPosition;
            Color color = popup.color;
            bool grow = popup.text.StartsWith("+");
            // 2026-10-21 research pass (§5.4): numbers pop 0 → 1.3 → 1.0 in ~7 frames, hold, then
            // drift up and fade. Crits are bigger, tilted, and sit on a red slanted P5 strip.
            // Only real crits get the red strip: banners and dice notes are big too but are not hits.
            bool crit = popup.text.StartsWith("暴击", StringComparison.Ordinal);
            bool calm = BattlePresentationOptions.ReduceMotion;
            float drift = calm ? 0f : UnityEngine.Random.Range(-14f, 14f);
            popup.rectTransform.localEulerAngles = new Vector3(0f, 0f, crit && !calm ? 7f : calm ? 0f : UnityEngine.Random.Range(-4f, 4f));
            RectTransform strip = crit && !calm ? CritStrip1022(popup) : null;
            while (elapsed < duration && popup.gameObject.activeSelf)
            {
                if (paused)
                {
                    yield return null;
                    continue;
                }
                elapsed += BattleAnimationDelta();
                float t = Mathf.Clamp01(elapsed / duration);
                float rise = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.5f, 1f, t));
                popup.rectTransform.anchoredPosition = start + new Vector2(drift * rise, calm ? 0f : 46f * rise);
                float pop = Mathf.Clamp01(elapsed / .06f);
                float stamp = calm ? 1f
                    : grow ? (pop < 1f ? Mathf.Lerp(.45f, 1.15f, pop) : Mathf.Lerp(1.15f, 1f, Mathf.Clamp01((elapsed - .06f) / .1f)))
                    : (pop < .55f ? Mathf.Lerp(0f, 1.3f, pop / .55f) : Mathf.Lerp(1.3f, 1f, (pop - .55f) / .45f)) * (crit ? 1.12f : 1f);
                popup.rectTransform.localScale = Vector3.one * stamp;
                color.a = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.55f, 1f, t));
                if (strip != null)
                {
                    strip.anchoredPosition = popup.rectTransform.anchoredPosition;
                    strip.localScale = new Vector3(stamp, stamp, 1f);
                    var stripGroup = strip.GetComponent<CanvasGroup>(); if (stripGroup != null) stripGroup.alpha = color.a;
                }
                popup.color = color;
                if (icon != null && icon.gameObject.activeSelf)
                {
                    Color iconColor = icon.color;
                    iconColor.a = color.a;
                    icon.color = iconColor;
                }
                yield return null;
            }

            if (strip != null) Destroy(strip.gameObject);
            // Retire the semantic owner as well as its optional authored child.
            // Its OnDisable clears the pooled sprite before this slot is reused.
            if (owner != null) owner.gameObject.SetActive(false);
            popup.gameObject.SetActive(false);
            if (icon != null) icon.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ cells

        private CellView FindCell(BattleUnit unit)
        {
            if (unit == null) return null;
            for (int index = 0; index < cells.Count; index++)
            {
                CellView cell = cells[index];
                if (cell.Side == unit.Side && cell.Row == unit.Row && cell.Col == unit.Col) return cell;
            }

            return null;
        }

        private CellView FindCell(BattleSide side, int row, int col)
        {
            for (int index = 0; index < cells.Count; index++)
            {
                CellView cell = cells[index];
                if (cell.Side == side && cell.Row == row && cell.Col == col) return cell;
            }

            return null;
        }

        private void RefreshAllCells()
        {
            RefreshEnemyFigures();
            for (int index = 0; index < cells.Count; index++) cells[index].Unit = null;
            IReadOnlyList<BattleUnit> units = battle.Units;
            for (int index = 0; index < units.Count; index++)
            {
                if (!units[index].Spawned) continue;
                CellView cell = FindCell(units[index]);
                if (cell != null) cell.Unit = units[index];
            }

            int playerIndex = 0;
            // PSD battle: the captain stands in the first slot, right above the PSD 「队长」 plate.
            List<CellView> captainOrder = null;
            if (psdRoot != null)
                captainOrder = CaptainFirstOrder1026(cells.Where(c => c.Unit != null && c.Side == BattleSide.Player).ToList());
            for (int index = 0; index < cells.Count; index++)
            {
                CellView cell = cells[index];
                if (cell.Unit != null)
                {
                    if (cell.Side == BattleSide.Player)
                    {
                        int row = playerIndex / 4;
                        int col = playerIndex % 4;
                        if (psdRoot != null)
                        {
                            int slot = captainOrder.IndexOf(cell);
                            bool placed = psdSlots.TryGetValue(cell, out int previousSlot);
                            Vector2 previous = cell.Rect.anchoredPosition;
                            PositionPsdMember(cell, slot);
                            if (placed && previousSlot != slot) CardSlide1026.Move(cell.Rect, previous, CardSlideSeconds1026);
                        }
                        else PanelKit.PlaceTop(cell.Rect, 20f + col * 174f, PlayerRosterTop + row * 182f,
                            PlayerCellWidth, PlayerCellHeight);
                        playerIndex++;
                    }
                    else
                    {
                        RectTransform anchor = enemyAnchors[cell.Unit.Id];
                        if (cell.Rect.parent != anchor) cell.Rect.SetParent(anchor, false);
                        if (psdRoot != null) PositionPsdEnemyCell(cell, anchor);
                        else PanelKit.PlaceTop(cell.Rect, (anchor.rect.width - EnemyCellWidth) / 2, 0,
                            EnemyCellWidth, EnemyCellHeight);
                    }
                }
                ApplyCell(cell);
                EnsureSkillVisuals1026(cell);
            }
            RefreshCaptainPlate1026();
        }

        private void RefreshCell(BattleUnit unit)
        {
            CellView cell = FindCell(unit);
            if (cell == null) return;
            cell.Unit = unit;
            ApplyCell(cell);
        }

        private void ApplyCell(CellView cell)
        {
            BattleUnit unit = cell.Unit;
            bool occupied = unit != null && unit.Spawned &&
                (cell.Side == BattleSide.Player || unit.Alive && unit.Id != bossUnitId);
            cell.Root.SetActive(occupied);
            if (!occupied) return;
            cell.Name.gameObject.SetActive(occupied);
            cell.HpFill.transform.parent.gameObject.SetActive(occupied);
            cell.HpText.gameObject.SetActive(occupied);
            cell.Status.gameObject.SetActive(occupied);
            cell.Background.color = cell.Side == BattleSide.Player ? CellIdle : CellEnemy;
            cell.Name.text = unit.Definition.Name;
            // The captain's name is always gold, independent of the transient selection frame.
            bool captainName = cell.Side == BattleSide.Player && unit.Id == battle.CurrentLeaderId && unit.Alive;
            // 非队长名字用近白但非金色（r<0.95），让队长的金色在卡片阵列里唯一可辨。
            cell.Name.color = captainName ? new Color32(255, 205, 110, 255) : new Color32(226, 228, 245, 255);
            if (cell.Portrait != null)
            {
                Sprite portrait = cell.Side == BattleSide.Player
                    ? BattlePortrait(unit.Definition.Id)
                    : EnemySprite(unit.Definition.Id);
                if (psdRoot != null && cell.Side == BattleSide.Player && psdAlbums.TryGetValue(cell, out var album))
                    album.SetPortrait(PsdMemberPortrait(unit, cell));
                else cell.Portrait.sprite = portrait;
                cell.Portrait.enabled = portrait != null && cell.Side == BattleSide.Player;
                if (cell.Motion != null && cell.MotionBoundId != unit.Id)
                {
                    cell.MotionBoundId = unit.Id;
                    cell.Motion.Configure(cell.Portrait, () => paused || closing || resultOverlay != null && resultOverlay.activeSelf,
                        () => speed, "performer-" + battle.RaceOf(unit).ToString().ToLowerInvariant());
                }
            }
            cell.HpFill.fillAmount = unit.MaxHp > 0 ? Mathf.Clamp01(unit.Hp / (float)unit.MaxHp) : 0f;
            cell.HpText.text = $"{unit.Hp}/{unit.MaxHp}";
            bool shielded = unit.Shield > 0;
            cell.ShieldTrack.SetActive(shielded);
            if (shielded) cell.ShieldFill.fillAmount = Mathf.Clamp01(unit.Shield / (float)Mathf.Max(1, unit.MaxHp));
            string realtimeStatus = battle.IsRealtime
                ? battle.RealtimeStatus(unit, cell.Side == BattleSide.Player ? 2 : 1) : string.Empty;
            cell.Status.text = string.IsNullOrEmpty(realtimeStatus) ? StatusSummary(unit) : realtimeStatus;
            cell.Status.color = CombatFeedbackPalette.Status(cell.Status.text);
            if (cell.CastFill != null)
            {
                int remaining = battle.IsRealtime ? battle.CastRemaining(unit) : 0;
                cell.CastFill.transform.parent.gameObject.SetActive(remaining > 0);
                cell.CastFill.fillAmount = 1f - Mathf.Clamp01(remaining / 2000f);
                cell.CastFill.color = remaining <= 700 ? CombatFeedbackPalette.Hurt : CombatFeedbackPalette.Control;
                string intent = battle.EnemyCastIntent(unit);
                string intentTarget = battle.EnemyCastTargetDescription(unit);
                cell.CastLabel.gameObject.SetActive(remaining > 0);
                cell.CastLabel.text = remaining > 0 ? $"{intent}→{intentTarget} {remaining / 1000f:0.0}s" : "";
                if (remaining > 0) cell.Status.text = battle.EnemyThreatName(unit);
            }
            bool stunned = battle.IsRealtime && battle.ConditionRemaining(unit, CombatCondition.Stun) > 0;
            cell.Motion?.SetControlled(stunned);
            if (enemyMotions.TryGetValue(unit.Id, out var controlledMotion)) controlledMotion.SetControlled(stunned);
            if (cell.Side == BattleSide.Player)
            {
                // Health must remain readable when a shield/buff is present.
                cell.HpText.gameObject.SetActive(true);
                PanelKit.PlaceTop(cell.Status.rectTransform, 8, 170, PlayerCellWidth - 16, 10);
                cell.Status.fontSize = 8;
                PanelKit.EnableBestFit(cell.Status, 8);
                cell.Status.gameObject.SetActive(!string.IsNullOrEmpty(cell.Status.text));
                if (cell.CaptainBadge != null) cell.CaptainBadge.SetActive(captainName);
                cell.Energy?.Refresh();
                cell.StatusIcons?.Refresh();
            }
            if (!unit.Alive)
            {
                cell.Fallen.gameObject.SetActive(true);
                cell.Group.alpha = FallenAlpha1023(cell);
            }
            else
            {
                cell.Fallen.gameObject.SetActive(false);
                cell.Group.alpha = 1f;
            }
            ShowFallen1023(cell, !unit.Alive);
            if (psdRoot != null) RefreshPsdCell(cell);
        }

        private Sprite BattlePortrait(string memberId)
        {
            if (battlePortraits.TryGetValue(memberId, out Sprite cached)) return cached;
            Sprite source = PanelKit.MemberSpriteOrNull(memberId, false);
            if (source == null) return null;
            // UI-only framing, never replace the shared full-body artwork. Values are normalized
            // texture rectangles, with y measured from the bottom (Unity sprite coordinates).
            Rect focus = PanelKit.MemberBustFocus(memberId);
            // The previous shared focus stopped at .90/.97 and shaved off native hair/ears.
            // Keep the source's complete top edge and a generous horizontal head margin;
            // the battle-only sprite still ends at the existing chest/waist focus bottom.
            float left = Mathf.Max(0f, focus.xMin - .12f);
            float right = Mathf.Min(1f, focus.xMax + .12f);
            if (memberId == "wubai") right = 1f; // Her new right ponytail reaches the source edge.
            focus = new Rect(left, focus.yMin, right - left, 1f - focus.yMin);
            Rect original = source.rect;
            Rect crop = new Rect(original.x + original.width * focus.x,
                original.y + original.height * focus.y, original.width * focus.width, original.height * focus.height);
            Sprite portrait = Sprite.Create(source.texture, crop, Vector2.one * .5f, source.pixelsPerUnit,
                0, SpriteMeshType.FullRect);
            portrait.name = memberId + "-BattleCloseup";
            runtimeSprites.Add(portrait);
            battlePortraits.Add(memberId, portrait);
            return portrait;
        }

        private static string StatusSummary(BattleUnit unit)
        {
            if (unit.Statuses.Count == 0) return string.Empty;
            var builder = new StringBuilder();
            for (int index = 0; index < unit.Statuses.Count; index++)
            {
                if (index > 0) builder.Append(' ');
                builder.Append(EffectShort(unit.Statuses[index].Effect));
                builder.Append(unit.Statuses[index].RoundsLeft);
            }

            return builder.ToString();
        }

        private static string EffectShort(string effect)
        {
            switch (effect)
            {
                case SkillEffect.BuffAttack: return "攻↑";
                case SkillEffect.DebuffDefense: return "防↓";
                case SkillEffect.Shield: return "盾";
                case SkillEffect.Heal: return "疗";
                default: return "伤";
            }
        }

        private static string EffectName(string effect)
        {
            switch (effect)
            {
                case SkillEffect.Heal: return "治疗";
                case SkillEffect.BuffAttack: return "攻击提升";
                case SkillEffect.DebuffDefense: return "防御降低";
                case SkillEffect.Shield: return "护盾";
                default: return "伤害";
            }
        }

        private static string PatternName(string pattern)
        {
            switch (pattern)
            {
                case SkillPattern.Plus: return "十字";
                case SkillPattern.Row: return "整行";
                case SkillPattern.Column: return "整列";
                case SkillPattern.All: return "全体";
                default: return "单体";
            }
        }

        private void SetActorHighlight(BattleUnit actor)
        {
            CellView cell = FindCell(actor);
            for (int index = 0; index < cells.Count; index++)
            {
                cells[index].Outline.effectColor = cells[index] == cell
                    ? new Color32(255, 126, 226, 255)
                    : new Color32(166, 112, 255, 0);
            }

            if (cell == null)
            {
                actorGlow.enabled = false;
                return;
            }

            actorGlow.enabled = true;
            actorGlow.color = actor.Side == BattleSide.Player
                ? new Color32(80, 220, 255, 42)
                : new Color32(255, 92, 214, 42);
            Vector2 anchored = cell.Rect.anchoredPosition;
            actorGlow.rectTransform.sizeDelta = cell.Rect.rect.size * 1.04f;
            actorGlow.rectTransform.anchoredPosition = new Vector2(anchored.x + cell.Rect.rect.width * 0.5f,
                anchored.y - cell.Rect.rect.height * 0.5f);
            actorGlow.transform.SetSiblingIndex(cell.Root.transform.GetSiblingIndex());
        }

        private bool coveredByStory;
        private bool EntryPresentationCovered => coveredByStory || bossEntrancePlaying1102 || ChoSiren.UI.ChallengeVideoTransition.IsCoveringGame;

        private void Update()
        {
            AdvanceTurnBanner();
            if (pickingCaptain1024 && psdRoot != null)
                foreach (var c in cells) if (c.Side == BattleSide.Player) PickHint1024(c);
            AdvanceRerollGlow();
            // A stage-intro story can still be on screen (its exit animation) when the battle
            // opens; the opening dice roll waits until the player can actually see it. A story
            // left on its end card (it starts the battle from there) no longer covers anything.
            StoryPanel story = FindAnyObjectByType<StoryPanel>();
            coveredByStory = story != null && !story.Ended;
            if (paused || closing || EntryPresentationCovered) return;
            TickWideCamera1111(); // 2026-11-11: 三敌同屏 camera pan
            TickFormationCharge1102();
            presentationClock += BattleAnimationDelta();
            RefreshPsdSkillCues1019();
            foreach (CellView view in cells)
            {
                if (view.ActionLabel != null && view.ActionLabel.gameObject.activeSelf &&
                    presentationClock >= view.ActionLabelUntil) view.ActionLabel.gameObject.SetActive(false);
                if (view.HurtLabel != null && view.HurtLabel.gameObject.activeSelf &&
                    presentationClock >= view.HurtLabelUntil) view.HurtLabel.gameObject.SetActive(false);
            }
            if (awaitingDiceLanding && dicePresentations.Count > 0 && !dicePresentations.Exists(item => item.Busy))
            {
                awaitingDiceLanding = false;
                RefreshDiceUi();
                StartCoroutine(FlashDiceResult());
                PlayComboShow();
            }
            if (!paused && battle != null && !battle.IsRealtime && battle.Outcome == BattleOutcome.Ongoing)
                battleElapsed += Time.unscaledDeltaTime;
            RefreshBattleHud();
            if (actorGlow == null || !actorGlow.enabled) return;
            float pulse = 0.5f + Mathf.Sin(presentationClock * 4.2f) * 0.5f;
            Color color = actorGlow.color;
            color.a = Mathf.Lerp(0.08f, 0.18f, pulse);
            actorGlow.color = color;
            actorGlow.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.99f, 1.02f, pulse);
        }

        private void RefreshBattleHud()
        {
            if (battle == null || enemyHpFill == null) return;
            long current = 0;
            long maximum = 0;
            long teamCurrent = 0, teamMaximum = 0;
            int survivors = 0, playerCount = 0, aliveEnemies = 0, defeatedCount = 0, enemyCount = 0;
            BattleUnit boss = null;
            IReadOnlyList<BattleUnit> units = battle.Units;
            for (int index = 0; index < units.Count; index++)
            {
                BattleUnit unit = units[index];
                if (unit.Side == BattleSide.Player)
                {
                    teamCurrent += Mathf.Max(0, unit.Hp); teamMaximum += Mathf.Max(1, unit.MaxHp);
                    playerCount++; if (unit.Alive) survivors++;
                    continue;
                }
                enemyCount++;
                if (unit.Spawned && unit.Hp <= 0) defeatedCount++;
                if (unit.Alive) aliveEnemies++;
                if (unit.Id == bossUnitId) boss = unit;
                if (!unit.Spawned || unit.Wave != battle.CurrentWave - 1) continue;
                current += Mathf.Max(0, unit.Hp);
                maximum += Mathf.Max(1, unit.MaxHp);
            }
            if (boss != null) { current = Mathf.Max(0, boss.Hp); maximum = boss.MaxHp; }
            float normalized = maximum > 0 ? Mathf.Clamp01(current / (float)maximum) : 0f;
            enemyHpFill.fillAmount = normalized;
            enemyHpFill.color = normalized <= 0.3f
                ? Color.Lerp(new Color32(255, 34, 116, 255), new Color32(255, 154, 48, 255),
                    0.5f + Mathf.Sin(presentationClock * 6f) * 0.5f)
                : Color.Lerp(new Color32(255, 72, 208, 255), new Color32(255, 44, 143, 255), 1f - normalized);
            enemyHpText.text = $"{current:N0}/{maximum:N0}";
            // 2026-11-12 第三章 顶盾: the bar turns cyan while shield layers stand; the pieces sit under it.
            int layers1111 = boss != null ? battle.Barrier(boss) : 0;
            if (layers1111 > 0)
            {
                enemyHpFill.color = new Color32(62, 232, 220, 255);
                enemyHpText.text = $"顶盾×{layers1111}  ·  {current:N0}/{maximum:N0}";
            }
            else if (boss != null && battle.BarrierBroken(boss)) enemyHpText.text = $"破盾！  {current:N0}/{maximum:N0}";
            RefreshShieldPips1111(boss);
            enemyHealthCaption.text = boss != null ? boss.Definition.Name : "本波敌方生命";
            enemyDefeatedText.text = $"已击败 {defeatedCount}/{enemyCount}";
            for (int i = 0; i < healthWaveBars.Count; i++)
                healthWaveBars[i].color = i / (float)healthWaveBars.Count <= normalized
                    ? enemyHpFill.color : new Color32(69, 28, 90, 150);
            if (boss != null && !boss.Alive && aliveEnemies > 0)
                enemyHpText.text = $"Boss 已倒下 · 还需清理 {aliveEnemies} 名护卫";
            enemyHpText.color = normalized <= 0.3f ? new Color32(255, 225, 174, 255) : PanelKit.White;
            if (battleReadabilityVeil != null)
            {
                Color veil = battleReadabilityVeil.color;
                veil.a = 12f / 255f;
                battleReadabilityVeil.color = veil;
            }
            if (bossPresentation != null) bossPresentation.SetHealthRatio(normalized);
            if (teamHpFill != null)
            {
                float ratio = teamMaximum > 0 ? teamCurrent / (float)teamMaximum : 0;
                teamHpFill.fillAmount = ratio;
                teamHpFill.color = ratio <= .3f ? new Color32(250, 129, 111, 255) : new Color32(67, 206, 154, 255);
                teamHpText.text = $"队伍总生命 {Mathf.RoundToInt(ratio * 100)}%  {teamCurrent:N0}/{teamMaximum:N0}  · 存活 {survivors}/{playerCount}";
            }
            int phase = ResolveDisplayedEnemyPhase(battle.EnemyPhase, battle.Log, logCursor);
            phaseText.text = battle.Stage.UsesRealtime && !battle.Stage.HasBossPhases
                ? $"第 {battle.CurrentWave}/{battle.TotalWaves} 波" : $"阶段 {phase}/3";
            timerText.text = battle.IsRealtime
                ? $"剩余 {Mathf.Max(0, battle.Stage.TimeLimitSeconds - Mathf.CeilToInt(battleElapsed)) / 60:00}:{Mathf.Max(0, battle.Stage.TimeLimitSeconds - Mathf.CeilToInt(battleElapsed)) % 60:00}"
                : FormatBattleTimer(battleElapsed);
            bool timeWarning = battle.IsRealtime && battle.Stage.TimeLimitSeconds - battleElapsed <= 20;
            timerText.color = timeWarning ? new Color32(255, 111, 118, 255) : new Color32(193, 187, 221, 255);
            if (timeWarning && battle.Outcome == BattleOutcome.Ongoing) phaseText.text = "即将超时判负";
            if (psdRoot != null) RefreshPsdHud(timeWarning);
        }

        private void PrepareDiceTurn()
        {
            ulong seed = StableDiceSeed(battle.Stage != null ? battle.Stage.Id : string.Empty,
                battle.Round, diceRollSequence);
            diceRollSequence = unchecked(diceRollSequence + 1);
            diceTurn = new DiceTurn(new SeededRandom(seed), diceEnergy);
            diceTurn.Begin();
            RefreshDiceUi();
        }

        private static ulong StableDiceSeed(string stageId, int teamRound, int rollSequence)
        {
            // FNV-1a is stable across Mono/IL2CPP and processes; do not use string.GetHashCode,
            // whose randomized implementation can change between runtimes.
            ulong hash = 14695981039346656037UL;
            string value = stageId ?? string.Empty;
            for (int index = 0; index < value.Length; index++)
            {
                hash ^= value[index];
                hash *= 1099511628211UL;
            }
            hash ^= unchecked((uint)teamRound);
            hash *= 1099511628211UL;
            hash ^= unchecked((uint)rollSequence);
            return hash == 0 ? 1UL : hash;
        }

        private void CompleteDiceTurn()
        {
            if (diceTurn == null) return;
            diceTurn.GainEnergy(25);
            diceEnergy = diceTurn.Energy;
            RefreshDiceUi();
        }

        private void AutoTuneDice()
        {
            if (diceTurn == null) return;
            ApplyAutoHolds();
            while (diceTurn.RerollsRemaining > 0 && diceTurn.Hand.MultiplierPermille < 1400)
            {
                if (!diceTurn.RerollUnheld(out _)) break;
                ApplyAutoHolds();
            }
            RefreshDiceUi();
        }

        private void ApplyAutoHolds()
        {
            bool[] planned = DiceHoldPlanner.Choose(diceTurn.Values);
            for (int index = 0; index < planned.Length; index++)
            {
                if (diceTurn.Held[index] != planned[index]) diceTurn.ToggleHold(index);
            }
        }

        private void ToggleDie(int index)
        {
            if (paused || !awaitingInput || diceTurn == null || awaitingDiceLanding) return;
            // Held means "kept"; unheld dice are ticked for the next selective reroll (at most two).
            if (!TrySelectDie1031(index)) return;
            diceTurn.ToggleHold(index);
            RefreshDiceUi();
        }

        private void RerollDice()
        {
            if (paused || !awaitingInput || diceTurn == null || awaitingDiceLanding) return;
            int bonusBefore = diceTurn.AccumulatedBonusPermille;
            if (!diceTurn.RerollUnheld(out string error)) { Notify(error); return; }
            diceEnergy = diceTurn.Energy;
            Sfx("dice", .8f);
            ShowRerollFeedback(bonusBefore);
            RefreshDiceUi();
        }

        private void EnergyRerollDice()
        {
            if (paused || !awaitingInput || diceTurn == null || awaitingDiceLanding) return;
            int bonusBefore = diceTurn.AccumulatedBonusPermille;
            if (!diceTurn.RerollAll(out string error)) { Notify(error); return; }
            diceEnergy = diceTurn.Energy;
            Sfx("dice", .8f);
            ShowRerollFeedback(bonusBefore);
            RefreshDiceUi();
        }

        /// <summary>
        /// Short visible chain for one reroll operation: dice gain -> captain/pattern benefit ->
        /// the next real hit uses the new accumulated multiplier (shown on damage events).
        /// </summary>
        private void ShowRerollFeedback(int bonusBefore)
        {
            if (diceTurn == null || diceTurn.Hand == null) return;
            int gain = diceTurn.RhythmMode ? diceTurn.AccumulatedBonusPermille - bonusBefore
                : Mathf.Max(0, diceTurn.AccumulatedBonusPermille - bonusBefore);
            float total = diceTurn.AccumulatedBonusPermille / 10f;
            string gainLabel = gain > 0 ? $"骰子加成 +{gain / 10f:0.#}%"
                // Rhythm rerolls can land a worse hand; say so, or "-6%" reads like a bug (2026-10-23 review).
                : gain < 0 ? $"牌型变弱 · 加成 {gain / 10f:0.#}%"
                : diceTurn.RhythmMode ? "骰子加成不变" : "骰子加成已达上限";
            if (diceTurn.RhythmMode)
                gainLabel += diceTurn.LastHeatChange > 0 ? $" · 热度{diceTurn.Heat}" : diceTurn.LastHeatChange < 0 ? " · 热度清零" : "";
            // 加成本次收益要一眼可读：大字号 + 金色浮字，盖过普通伤害读数。
            SpawnPopupAt(diceHandText.rectTransform, -101, gainLabel,
                gain > 0 ? PanelKit.Gold : gain < 0 ? PanelKit.Pink : PanelKit.Cyan, 26, 12f);
            string captain = CaptainBenefitLabel();
            // Captain benefit is already permanent in DiceInstruction; avoid a second
            // moving sentence over the captain portrait. Its full attribution remains below.
            string line = $"重投 → {diceTurn.Hand.DisplayName} · {gainLabel} · 累计 +{total:0.#}%" +
                (string.IsNullOrEmpty(captain) ? string.Empty : " · " + captain);
            AppendLog(line);
            if (battle.IsRealtime)
            {
                commandFeedback = line;
                commandFeedbackUntil = battle.ElapsedMilliseconds + 2600;
            }
            else if (eventText != null) eventText.text = line;
            RefreshAllCells();
        }

        private void RefreshDiceUi()
        {
            bool active = diceTurn != null && diceTurn.Hand != null;
            bool newRoll = active && displayedDiceRevision != diceTurn.Revision;
            if (newRoll) { displayedDiceRevision = diceTurn.Revision; awaitingDiceLanding = true; HideDiceAuras(); }
            bool battleSession = active && diceTurn.IsBattleSession;
            int selected = battleSession ? diceTurn.SelectedForRerollCount : 0;
            for (int index = 0; index < diceButtons.Count; index++)
            {
                Text label = PanelKit.LabelOf(diceButtons[index]);
                int face = active ? diceTurn.Values[index] : 0;
                Sprite faceSprite = face >= 1 && face <= userDiceFaceSprites.Length
                    ? userDiceFaceSprites[face - 1]
                    : null;
                label.text = faceSprite != null ? string.Empty : active ? face.ToString() : "?";
                bool held = active && diceTurn.Held[index];
                bool participating = active && diceTurn.Hand.Participating[index];
                diceHoldLabels[index].text = !active ? string.Empty
                    : awaitingDiceLanding ? "投掷中"
                    : battleSession ? held ? participating ? "保留·成型" : "保留" : "待重投"
                    : held ? "已保留" : participating ? "成型" : string.Empty;
                Color background = battleSession && !held ? DiceSelected
                    : participating ? DiceParticipating
                    : DiceHeld;
                Button dieButton = diceButtons[index].GetComponent<Button>();
                if (dieButton != null) dieButton.interactable = awaitingInput && !paused && !awaitingDiceLanding;
                Image hitArea = diceButtons[index].GetComponent<Image>();
                if (hitArea != null)
                {
                    hitArea.sprite = null;
                    hitArea.color = Color.clear;
                }
                if (index < diceFaceImages.Count)
                {
                    // Use a restrained tint on the die itself instead of restoring a square state background.
                    float tintStrength = battleSession && !held ? 0.2f : participating ? 0.16f : 0.11f;
                    dicePresentations[index].SetRestingFace(faceSprite, face, participating,
                        Color.Lerp(PanelKit.White, background, tintStrength));
                    if (newRoll && diceTurn.Rolled[index])
                        dicePresentations[index].PlayRoll(faceSprite, face, userDiceFaceSprites, index, participating);
                }
                if (index < diceOutlines.Count)
                {
                    diceOutlines[index].effectColor = battleSession && !held ? new Color32(120, 212, 255, 200)
                        : participating ? new Color32(255, 204, 83, 150)
                        : new Color32(255, 105, 210, 105);
                    float distance = battleSession && !held ? 2.2f : participating ? 2f : 1f;
                    diceOutlines[index].effectDistance = new Vector2(distance, -distance);
                }
            }

            int bonus = active ? diceTurn.AccumulatedBonusPermille : 0;
            if (diceEnergyFill != null)
                diceEnergyFill.fillAmount = battleSession
                    ? (diceTurn.EarnedRerolls >= diceTurn.BattleRerollLimit ? 1f
                        : Mathf.Clamp01(diceTurn.Energy / 100f))
                    : Mathf.Clamp01(bonus / (float)DiceTurn.MaxBattleBonusPermille);
            if (diceEnergyText != null)
                diceEnergyText.text = battleSession && diceTurn.RhythmMode ? $"×{diceTurn.AvailableRerolls}" // 2026-11-01c
                    : battleSession
                    ? (diceTurn.EarnedRerolls >= diceTurn.BattleRerollLimit
                        ? (diceTurn.RerollsRemaining > 0
                            ? $"重投就绪 ×{diceTurn.RerollsRemaining} · 次数已发满"
                            : "本场重投已发完")
                        : diceTurn.RerollsRemaining > 0
                            ? $"重投就绪 ×{diceTurn.RerollsRemaining} · 下一发充能 {diceTurn.Energy}%"
                            : $"重投充能 {diceTurn.Energy}%")
                    : (bonus >= DiceTurn.MaxBattleBonusPermille ? "骰子加成 · 封顶×2"
                        : $"骰子加成 +{bonus / 10f:0.#}%");
            if (diceHandText != null)
                diceHandText.text = active
                    ? $"{diceTurn.Hand.DisplayName} ×{diceTurn.Hand.MultiplierPermille / 1000f:0.##}"
                    : "等待骰子回合";
            if (rerollButton != null)
            {
                int rerolls = active ? diceTurn.RerollsRemaining : 0;
                rerollLabel.text = battleSession
                    ? $"重投已选{selected}颗" : $"重投未保留 · {rerolls}次";
                bool usable = rerolls > 0 && (!battleSession || selected > 0);
                PanelKit.SetButtonState(rerollButton, awaitingInput && usable,
                    usable ? new Color32(126, 62, 181, 252) : PanelKit.Disabled);
            }
            if (energyRerollButton != null)
            {
                energyRerollLabel.text = !battleSession
                    ? active && diceTurn.Energy >= DiceTurn.MaxEnergy ? "能量重投 · 就绪" : "能量重投 · 充能中"
                    : "全部重投";
                bool energyUsable = active && (battleSession ? diceTurn.CanReroll
                    : diceTurn.Energy >= DiceTurn.MaxEnergy);
                PanelKit.SetButtonState(energyRerollButton, awaitingInput && energyUsable,
                    energyUsable ? new Color32(58, 71, 126, 255) : PanelKit.ButtonDark);
            }
            if (battleSession)
            {
                diceHandText.text = $"{diceTurn.Hand.DisplayName} · 累计 ×{diceTurn.DamageMultiplierPermille / 1000f:0.###}";
                bool canReroll = awaitingInput && !paused && !awaitingDiceLanding && diceTurn.CanReroll;
                rerollLabel.text = $"重投已选{selected}颗";
                PanelKit.SetButtonState(rerollButton, canReroll && selected > 0,
                    canReroll && selected > 0 ? new Color32(126, 62, 181, 252) : PanelKit.Disabled);
                // 2026-10-31: no more "全部重投" — this entry rerolls the two loosest dice for the player.
                energyRerollLabel.text = diceTurn.RhythmMode ? $"重投 ×{diceTurn.AvailableRerolls}" // 2026-11-01c 骰子豆
                    : diceTurn.AvailableRerolls > 0 ? $"全部重投 · 剩{diceTurn.AvailableRerolls}次"
                    : diceTurn.EarnedRerolls < diceTurn.BattleRerollLimit ? $"全部重投 · 充能{diceTurn.Energy}%"
                    : "本场重投已用完";
                PanelKit.SetButtonState(energyRerollButton, canReroll,
                    canReroll ? new Color32(100, 55, 151, 255) : PanelKit.ButtonDark);
            }
            if (awaitingDiceLanding && diceHandText != null) diceHandText.text = "骰子翻滚中…";
            if (diceInstructionText != null && battleSession)
            {
                float total = diceTurn.AccumulatedBonusPermille / 10f;
                float gain = diceTurn.LastBonusGainPermille / 10f;
                bool capped = diceTurn.AccumulatedBonusPermille >= DiceTurn.MaxBattleBonusPermille;
                string readout = capped
                    ? $"累计 +{total:0.#}% · 已封顶 ×2"
                    : gain > 0
                        ? $"本次 +{gain:0.#}% · 累计 +{total:0.#}%"
                        : $"累计 +{total:0.#}%";
                string captain = CaptainBenefitLabel();
                if (!string.IsNullOrEmpty(captain)) readout += " · " + captain;
                readout += $" · 待重投{selected}颗 · 保留{DiceRules.DiceCount - selected}颗";
                diceInstructionText.text = readout;
                // Warm-up colour makes a growing bonus readable without hunting for the number.
                diceInstructionText.color = total >= 75f ? new Color32(255, 168, 74, 255)
                    : total >= 40f ? PanelKit.Gold : PanelKit.Muted;
            }
            if (psdRoot != null) RefreshPsdDice();
        }

        /// <summary>成员目录的种族文案 → 战斗种族；用于回合制模式下的队长收益读数。</summary>
        private static CombatRace RaceFromMemberLabel(string race) =>
            string.IsNullOrEmpty(race) ? CombatRace.None
            : race.Contains("人鱼") ? CombatRace.Mermaid
            : race.Contains("魅族") ? CombatRace.Charm
            : race.Contains("恶魔") ? CombatRace.Demon
            : race.Contains("血精灵") ? CombatRace.BloodElf
            : CombatRace.None;

        /// <summary>
        /// 收益读数用的队长种族：实时模式用战场队长；回合制没有战场时钟，
        /// 按编队默认队长从成员目录解析，保证两种模式下队长收益都可读。
        /// </summary>
        private CombatRace ReadoutLeaderRace()
        {
            if (battle != null && battle.CurrentLeaderRace != CombatRace.None) return battle.CurrentLeaderRace;
            int captain = model != null && model.Save != null && model.Save.Team.Count > 0
                ? model.Save.Team[0] : -1;
            return captain >= 0 && captain < GameModel.Members.Length
                ? RaceFromMemberLabel(GameModel.Members[captain].Race)
                : CombatRace.None;
        }

        /// <summary>Current captain/pattern benefit using the same numbers RealtimeBattle applies.</summary>
        private string CaptainBenefitLabel()
        {
            if (battle?.BattleDice?.Hand == null) return string.Empty;
            CombatRace race = ReadoutLeaderRace();
            DicePattern pattern = battle.BattleDice.Hand.Pattern;
            switch (race)
            {
                case CombatRace.Mermaid:
                    int shield = BattleSimulator.ShieldPercent(pattern);
                    return shield > 0 ? $"人鱼护盾 +{shield / 10f:0.#}%" : string.Empty;
                case CombatRace.Demon:
                    int poison = BattleSimulator.PoisonLayers(pattern);
                    return poison > 0 ? $"魔族叠毒 {poison}层" : string.Empty;
                case CombatRace.Charm:
                    int combo = BattleSimulator.Combo(pattern);
                    return combo > 0 ? $"魅族追击 {combo}次" : string.Empty;
                case CombatRace.BloodElf:
                    int pierce = BattleSimulator.Pierce(pattern);
                    return pierce > 0 ? $"血精灵穿甲 {pierce / 10f:0.#}%" : string.Empty;
                default:
                    return string.Empty;
            }
        }

        /// <summary>Strong hands use a compact cue on their own readout; weak hands use dice auras.</summary>
        private void PlayComboShow()
        {
            if (comboShow == null || diceTurn == null || diceTurn.Hand == null || battle == null) return;
            int tier = ChoSiren.UI.DiceComboShow.TierOf((int)diceTurn.Hand.Pattern);
            ShowDiceAuras(tier);
            // 2026-10-20 (owner): every scoring hand (from 一对 up) gets its word-art stamp.
            if (BattlePresentationOptions.ReduceMotion || tier <= 0)
            {
                comboShow.Cancel(); // A weak new hand must also remove a previous strong-hand cue.
                return;
            }
            var from = new List<RectTransform>();
            for (int i = 0; i < diceButtons.Count && i < diceTurn.Hand.Participating.Count; i++)
                if (diceTurn.Hand.Participating[i]) from.Add(diceButtons[i].GetComponent<RectTransform>());
            // Only members actually boosted by this hand participate in the number cue.
            // The compact cue releases those badges immediately; no flying shard delays the values.
            List<CellView> members = cells.Where(c => c.Side == BattleSide.Player && c.Unit != null && c.Unit.Alive
                && battle.DiceMultiplierFor(c.Unit) > 1000).ToList();
            var targets = members.Select(c => c.Rect).ToList();
            float value = diceTurn.DamageMultiplierPermille / 1000f;
            foreach (CellView member in members)
                if (psdAttackBadges.TryGetValue(member, out var badge)) badge.Hold();
            var hit = new HashSet<int>();
            // 2026-10-20 (owner): the hand's bonus visibly flies from the scoring dice into the
            // captain's card; the badges release when it lands.
            CellView captainCell = cells.FirstOrDefault(c => c.Side == BattleSide.Player && c.Unit != null && c.Unit.Alive
                && c.Unit.Id == battle.CurrentLeaderId);
            int accumulated = diceTurn.AccumulatedBonusPermille;
            int gain = Mathf.Max(0, accumulated - lastCaptainBonusPermille);
            lastCaptainBonusPermille = accumulated;
            comboShow.Play(diceTurn.Hand.DisplayName, value, tier, diceHandText.font, from, new List<RectTransform>(), null,
                diceHandText.rectTransform);
            if (ChoSiren.UI.DiceComboShow.StampFor(diceTurn.Hand.DisplayName) != null) Sfx("stamp", tier >= 3 ? 1f : .7f);
            Action release = () =>
            {
                for (int i = 0; i < members.Count; i++)
                    if (hit.Add(i) && psdAttackBadges.TryGetValue(members[i], out var badge)) badge.Release();
            };
            if (psdRoot != null && captainCell != null && !BattlePresentationOptions.ReduceMotion)
            {
                Sfx("orb-chime", .7f);
                DiceBonusFlight1020.Play(psdRoot, from, captainCell.Rect,
                    gain > 0 ? $"攻击 +{gain / 10f:0.#}%" : $"倍率 ×{value:0.##}", diceHandText.font, () => paused || closing, release);
            }
            else release();
        }

        /// <summary>Scoring dice burn and show "+pips"; the rest dim while a hand is formed.</summary>
        private void ShowDiceAuras(int tier)
        {
            if (diceAuras.Count != diceButtons.Count)
            {
                diceAuras.Clear();
                foreach (GameObject die in diceButtons)
                    diceAuras.Add(ChoSiren.UI.DiceScoreAura.Attach(die.GetComponent<RectTransform>(), diceHandText.font,
                        () => paused || closing || EntryPresentationCovered));
            }
            for (int i = 0; i < diceAuras.Count; i++)
            {
                bool scoring = tier > 0 && diceTurn != null && i < diceTurn.Hand.Participating.Count && diceTurn.Hand.Participating[i];
                var fx = scoring ? ChoSiren.UI.DiceFxStyle.For(diceTurn.Hand.Pattern, diceTurn.Hand.Values, i)
                    : default;
                diceAuras[i].Show(scoring, tier > 0, diceTurn != null ? diceTurn.Values[i] : 0,
                    scoring ? .2f : 0f, fx);
                if (i < dicePedestals.Count && dicePedestals[i] != null)
                    dicePedestals[i].color = scoring ? Color.Lerp(Color.white, fx.Color, .5f) : PedestalIdle;
            }
        }

        private void HideDiceAuras()
        {
            foreach (var aura in diceAuras) if (aura != null) aura.Hide();
            foreach (Image pedestal in dicePedestals) if (pedestal != null) pedestal.color = PedestalIdle;
        }

        private IEnumerator FlashDiceResult()
        {
            if (diceHandText == null) yield break;
            const float duration = .75f;
            float elapsed = 0;
            while (elapsed < duration && !closing && !awaitingDiceLanding)
            {
                if (paused) { yield return null; continue; }
                elapsed += BattleAnimationDelta();
                float progress = Mathf.Clamp01(elapsed / duration);
                float pulse = Mathf.Sin(progress * Mathf.PI);
                diceHandText.rectTransform.localScale = Vector3.one * (1 + pulse * .18f);
                diceHandText.color = Color.Lerp(PanelKit.White, PanelKit.Gold, pulse);
                // The instruction line carries the actual bonus numbers, so it pulses too.
                if (diceInstructionText != null)
                {
                    diceInstructionText.rectTransform.localScale = Vector3.one * (1 + pulse * .12f);
                    diceInstructionText.color = Color.Lerp(diceInstructionText.color, PanelKit.Gold, pulse);
                }
                yield return null;
            }
            diceHandText.rectTransform.localScale = Vector3.one;
            diceHandText.color = PanelKit.White;
            if (diceInstructionText != null) diceInstructionText.rectTransform.localScale = Vector3.one;
        }

        // ------------------------------------------------------------------ player input

        private void BuildSkillButtons(BattleUnit actor)
        {
            ClearSkillBar();
            List<string> skillIds = actor.Definition.SkillIds;
            int count = skillIds.Count;
            if (count == 0) return;
            if (skillWaitingText != null) skillWaitingText.gameObject.SetActive(false);
            const float gap = 12f;
            float width = (672f - gap * (count - 1)) / count;
            for (int index = 0; index < count; index++)
            {
                string skillId = skillIds[index];
                SkillDefinition skill = battle.LookupSkill(skillId);
                if (skill == null) continue;
                bool ready = battle.IsSkillReady(actor, skillId);
                int remaining = actor.Cooldowns.TryGetValue(skillId, out int value) ? value : 0;
                string label = $"{skill.Name}\n{PatternName(skill.Pattern)} · {EffectName(skill.Effect)}";
                if (!ready && remaining > 0) label += $" · 冷却 {remaining}";
                GameObject button = kit.NewButton("Skill-" + skillId, skillBar, label, 20,
                    ready ? PanelKit.White : new Color32(110, 108, 145, 150), PanelKit.White,
                    () => SelectSkill(skillId), 20);
                PanelKit.PlaceTop(button.GetComponent<RectTransform>(), index * (width + gap), 0, width, 84);
                Image frame = button.GetComponent<Image>();
                frame.sprite = kit.RoundedSprite(12);
                frame.type = Image.Type.Sliced;
                frame.color = ready
                    ? new Color32(36, 27, 65, 255)
                    : new Color32(23, 21, 41, 255);
                Text skillLabel = PanelKit.LabelOf(button);
                skillLabel.lineSpacing = 1.15f;
                kit.AddOutline(skillLabel.gameObject, new Color32(4, 6, 28, 220), 1.2f);
                kit.AddOutline(button, new Color32(94, 218, 255, ready ? (byte)70 : (byte)20), 1.5f);
                button.GetComponent<Button>().interactable = ready;
                skillButtons.Add(button);
            }

            // Preselect the first ready skill so a single tap on an anchor already works.
            for (int index = 0; index < skillIds.Count; index++)
            {
                if (!battle.IsSkillReady(actor, skillIds[index])) continue;
                SelectSkill(skillIds[index]);
                break;
            }
        }

        private static Color SkillColor(string effect)
        {
            switch (effect)
            {
                case SkillEffect.Heal: return new Color32(20, 92, 83, 118);
                case SkillEffect.Shield: return new Color32(24, 76, 104, 118);
                case SkillEffect.BuffAttack: return new Color32(96, 70, 24, 112);
                case SkillEffect.DebuffDefense: return new Color32(69, 39, 112, 118);
                default: return new Color32(42, 22, 79, 122);
            }
        }

        private void ClearSkillBar()
        {
            for (int index = 0; index < skillButtons.Count; index++)
            {
                skillButtons[index].SetActive(false);
                Destroy(skillButtons[index]);
            }

            skillButtons.Clear();
            if (skillWaitingText != null)
            {
                skillWaitingText.gameObject.SetActive(true);
                skillWaitingText.text = "正在释放技能…";
            }
            selectedSkillId = null;
            ClearAnchors();
        }

        private void SelectSkill(string skillId)
        {
            if (paused || !awaitingInput || inputActor == null) return;
            if (!battle.IsSkillReady(inputActor, skillId))
            {
                Notify("技能冷却中");
                return;
            }

            selectedSkillId = skillId;
            for (int index = 0; index < skillButtons.Count; index++)
            {
                bool selected = skillButtons[index].name == "Skill-" + skillId;
                Outline outline = skillButtons[index].GetComponent<Outline>();
                if (outline != null) outline.effectColor = selected ? new Color32(255, 230, 160, 255) : new Color32(197, 156, 255, 0);
                // Selection changes a border, never the button's size or its neighbour's hit area.
                skillButtons[index].transform.localScale = Vector3.one;
            }

            SkillDefinition skill = battle.LookupSkill(skillId);
            anchors.Clear();
            List<(int Row, int Col)> legal = battle.LegalAnchors(inputActor, skillId);
            for (int index = 0; index < legal.Count; index++) anchors.Add(legal[index]);
            BattleSide targetSide = TargetSide(inputActor, skill);
            for (int index = 0; index < cells.Count; index++)
            {
                CellView cell = cells[index];
                bool isAnchor = cell.Side == targetSide && ContainsAnchor(cell.Row, cell.Col);
                cell.Highlight.enabled = isAnchor;
                cell.Highlight.color = AnchorTint;
            }

            previewText.text = $"{skill.Name} · {PatternName(skill.Pattern)} · {EffectName(skill.Effect)} · " +
                               $"可选目标 {anchors.Count} 处，点击{(targetSide == BattleSide.Enemy ? "敌方" : "我方")}高亮格释放";
        }

        private bool ContainsAnchor(int row, int col)
        {
            for (int index = 0; index < anchors.Count; index++)
                if (anchors[index].Row == row && anchors[index].Col == col) return true;
            return false;
        }

        private static BattleSide TargetSide(BattleUnit actor, SkillDefinition skill)
        {
            bool enemies = SkillEffect.TargetsEnemies(skill.Effect);
            if (actor.Side == BattleSide.Player) return enemies ? BattleSide.Enemy : BattleSide.Player;
            return enemies ? BattleSide.Player : BattleSide.Enemy;
        }

        private void ClearAnchors()
        {
            anchors.Clear();
            for (int index = 0; index < cells.Count; index++)
            {
                cells[index].Highlight.enabled = false;
                cells[index].Highlight.color = AnchorTint;
            }
        }

        private void CellHovered(BattleSide side, int row, int col, bool entered)
        {
            if (battle != null && battle.IsRealtime) return;
            if (!awaitingInput || inputActor == null || string.IsNullOrEmpty(selectedSkillId)) return;
            SkillDefinition skill = battle.LookupSkill(selectedSkillId);
            if (skill == null) return;
            BattleSide targetSide = TargetSide(inputActor, skill);
            bool isAnchor = side == targetSide && ContainsAnchor(row, col);

            for (int index = 0; index < cells.Count; index++)
            {
                CellView cell = cells[index];
                bool anchor = cell.Side == targetSide && ContainsAnchor(cell.Row, cell.Col);
                cell.Highlight.enabled = anchor;
                cell.Highlight.color = AnchorTint;
            }

            if (!entered || !isAnchor)
            {
                previewText.text = $"{skill.Name} · {PatternName(skill.Pattern)} · {EffectName(skill.Effect)} · 可选目标 {anchors.Count} 处";
                return;
            }

            List<BattleUnit> affected = battle.AffectedUnits(inputActor, skill, row, col);
            var builder = new StringBuilder();
            builder.Append(skill.Name).Append(" → 影响 ").Append(affected.Count).Append(" 个单位：");
            for (int index = 0; index < affected.Count; index++)
            {
                CellView cell = FindCell(affected[index]);
                if (cell != null)
                {
                    cell.Highlight.enabled = true;
                    cell.Highlight.color = AffectedTint;
                }

                if (index > 0) builder.Append('，');
                builder.Append(affected[index].Definition.Name);
                if (skill.Effect == SkillEffect.Damage)
                {
                    int multiplier = diceTurn != null && diceTurn.Hand != null
                        ? diceTurn.Hand.MultiplierPermille
                        : 1000;
                    builder.Append(' ').Append(battle.PreviewDamage(inputActor, skill, affected[index], false, multiplier));
                }
            }

            previewText.text = builder.ToString();
        }

        /// <summary>Long-press a member card: make her captain (race passive + dice buff).</summary>
        private void CellHeld(int row, int col)
        {
            if (paused || closing || battle == null || !battle.IsRealtime) return;
            BattleUnit unit = battle.UnitAt(BattleSide.Player, row, col);
            if (unit == null) return;
            bool swapped = battle.TrySwapCaptain(unit.Id, out string message);
            if (swapped) kit.PlaySuccess(); else kit.PlayClick();
            Notify(swapped ? message + " · 骰子加成与种族效果随之切换" : message);
            RefreshRealtimeCommands();
            RefreshAllCells();
        }

        private void CellClicked(BattleSide side, int row, int col)
        {
            kit.PlayClick();
            if (paused) return;
            if (battle.IsRealtime)
            {
                BattleUnit unit = battle.UnitAt(side, row, col);
                if (unit == null) return;
                if (side == BattleSide.Player && battle.IsTurnBased)
                {
                    // 换队长 mode (2026-10-24 review #21): the next card tapped becomes the captain.
                    if (pickingCaptain1024)
                    {
                        pickingCaptain1024 = false;
                        RefreshCaptainSwapButton1024();
                        bool swapped = battle.TrySwapCaptain(unit.Id, out string swapMessage);
                        if (swapped) kit.PlaySuccess();
                        Notify(swapMessage);
                        RefreshRealtimeCommands(); RefreshAllCells();
                        return;
                    }
                    // 天命之子 (赵金 2026-10-24): a full card casts her ultimate right away, in auto and manual alike.
                    if (battle.IsUltimateReady(unit))
                    {
                        // a tap launches the record too, so tap and swipe look the same
                        CellView tapped = cells.FirstOrDefault(c => c.Unit == unit);
                        if (tapped != null) LaunchUltimateDisc1025(tapped);
                        else { tappedUltimateId = unit.Id; Notify($"{unit.Definition.Name} 大招！"); }
                    }
                    else if (battle.EnergyOf(unit) >= BattleSimulator.MaxUnitEnergy)
                        Notify(battle.UltimatesUsed(unit) >= BattleSimulator.MaxUltimatesPerMember
                            ? $"{unit.Definition.Name} 本场大招次数已用完" : "刚放过大招，下回合再来");
                    else Notify($"{unit.Definition.Name} 能量 {battle.EnergyOf(unit)}%，满了再点");
                    return;
                }
                if (side == BattleSide.Player)
                {
                    inputActor = unit;
                    // Manual battle: a tap on a full-energy avatar is the release command.
                    if (!autoMode && battle.IsEnergyFull(unit) && !TryBeginManualUltimate(unit))
                    {
                        float cooldown = battle.UltimateCooldownRemaining(unit) / 1000f;
                        Notify(cooldown > 0 ? $"大招冷却 {cooldown:0.0}秒"
                            : battle.ConditionRemaining(unit, CombatCondition.Stun) > 0 ? "眩晕中无法放大招"
                            : "封技中无法放大招");
                    }
                }
                else battle.FocusEnemy(unit.Id);
                RefreshRealtimeCommands();
                return;
            }
            if (!awaitingInput || inputActor == null)
            {
                CellView info = FindCell(side, row, col);
                if (info != null && info.Unit != null)
                    Notify($"{info.Unit.Definition.Name} · 生命 {info.Unit.Hp}/{info.Unit.MaxHp} · 攻击 {info.Unit.Attack} · 防御 {info.Unit.Defense}");
                return;
            }

            if (string.IsNullOrEmpty(selectedSkillId))
            {
                Notify("请先选择技能");
                return;
            }

            SkillDefinition skill = battle.LookupSkill(selectedSkillId);
            if (skill == null) return;
            if (side != TargetSide(inputActor, skill) || !ContainsAnchor(row, col))
            {
                Notify("该位置不是合法目标");
                return;
            }

            BattleAction action = new BattleAction
            {
                ActorId = inputActor.Id,
                SkillId = selectedSkillId,
                Row = row,
                Col = col,
                PowerMultiplierPermille = diceTurn != null ? diceTurn.Hand.MultiplierPermille : 1000,
            };
            if (!battle.TryAct(action, out string error))
            {
                Notify(string.IsNullOrEmpty(error) ? "行动失败" : error);
                return;
            }

            CompleteDiceTurn();
            ClearSkillBar();
            previewText.text = diceTurn != null
                ? $"行动已执行 · {diceTurn.Hand.DisplayName} ×{diceTurn.Hand.MultiplierPermille / 1000f:0.##}"
                : "行动已执行";
            awaitingInput = false;
            RefreshDiceUi();
        }

        private void ToggleAuto()
        {
            CancelUltimateTiming();
            autoMode = !autoMode;
            if (battle.IsRealtime)
            {
                // Skill-release mode only. Energy lives in the simulator, so toggling preserves
                // every charge and can never double-cast or double-spend.
                battle.AutoCastUltimates = autoMode;
                PanelKit.LabelOf(autoButton).text = autoMode ? "自动" : "手动";
                PanelKit.SetButtonState(autoButton, true,
                    psdRoot != null ? Color.clear : autoMode ? new Color32(116, 43, 177, 252) : PanelKit.ButtonDark);
                Notify(battle.IsTurnBased
                    ? (autoMode ? "自动：重投和大招也自动" : "手动：重投和大招由你点")
                    : autoMode
                    ? "自动战斗：满能量按队长→编队顺序自动放大招，普攻与小技能照常自动"
                    : "手动战斗：普攻与小技能自动，点击满能量头像放大招");
                RefreshRealtimeCommands();
                return;
            }
            PanelKit.LabelOf(autoButton).text = autoMode ? "自动" : "手动";
            PanelKit.SetButtonState(autoButton, true, psdRoot != null ? Color.clear : autoMode ? new Color32(116, 43, 177, 252) : PanelKit.ButtonDark);
            if (autoMode && awaitingInput)
            {
                ClearSkillBar();
                awaitingInput = false;
                RefreshDiceUi();
            }

            Notify(autoMode ? "已开启自动战斗" : "已关闭自动战斗");
        }

        private void ToggleSpeed()
        {
            // 2026-10-21: 1 → 2 → 3 → 1, remembered in the save for the next battle.
            speed = speed >= 3 ? 1 : speed + 1;
            model?.SetBattleSpeed(speed);
            ApplySpeedLabel();
            Notify(speed == 1 ? "已恢复 1 倍速" : $"已切换到 {speed} 倍速");
        }

        private void ApplySpeedLabel()
        {
            if (speedButton == null) return;
            PanelKit.LabelOf(speedButton).text = speed + "倍";
            PanelKit.SetButtonState(speedButton, true, psdRoot != null ? Color.clear : speed >= 2 ? new Color32(116, 43, 177, 252) : PanelKit.ButtonDark);
        }

        private void TogglePause()
        {
            paused = !paused;
            PanelKit.LabelOf(retreatButton).text = psdRoot != null ? "" : paused ? "继续" : "暂停";
            if (psdRoot != null)
            {
                // Put the one resume control above the modal without lifting the whole battlefield.
                retreatButton.transform.SetParent(paused ? transform : psdRoot, true);
                if (psdPauseGlyph != null) psdPauseGlyph.gameObject.SetActive(!paused);
                if (psdResumeGlyph != null) psdResumeGlyph.gameObject.SetActive(paused);
            }
            PanelKit.SetButtonState(retreatButton, true,
                psdRoot != null ? Color.clear : paused ? new Color32(116, 43, 177, 252) : PanelKit.ButtonDark);
            if (pauseOverlay != null)
            {
                pauseOverlay.SetActive(paused);
                if (paused) pauseOverlay.transform.SetAsLastSibling();
                // Modal shade blocks battle input; this one control can still close the pause menu.
                retreatButton.transform.SetAsLastSibling();
                if (exitButton != null) exitButton.transform.SetAsLastSibling();
            }
            if (paused)
            {
                battleChorus?.CancelPending();
                CancelUltimateTiming();
                // A paused battle must not leave a queued ultimate banner or attack trail on screen.
                skillCutIn?.Cancel();
                psdCloseup1019?.Cancel(); // the small-skill paper cut-in froze above the pause card
                attackTrajectories?.Cancel();
                ClearPopups();
                CloseAbilityOverlay();
            }
            else
            {
                CloseRulesOverlay();
            }
            Notify(paused ? "战斗已暂停" : "战斗继续");
        }

        private void ExitBattle()
        {
            if (closing) return;
            paused = false;
            awaitingInput = false;

            // AutoPlay(0) is the simulator's public zero-action termination path: it marks the
            // ongoing encounter as a defeat without advancing a single combat action. Settling
            // that result clears GameModel's pending battle before the panel returns to the map.
            if (battle != null && battle.Outcome == BattleOutcome.Ongoing) battle.AutoPlay(0);
            ReportBattleFinished();

            CloseAfterSettlement("已退出战斗，本次体力已消耗");
        }

        // ------------------------------------------------------------------ result

        private void ShowResult()
        {
            CancelPsdPresentation1019();
            CancelUltimateTiming();
            skillCutIn?.Cancel();
                psdUltimate?.Cancel();
            skillEffects?.Clear();
            comboShow?.Cancel();
            attackTrajectories?.Cancel();
            foreach (DiceRollPresentation roll in dicePresentations) roll.CancelRoll();
            ClearPopups();
            CloseAbilityOverlay();
            CloseRulesOverlay();
            awaitingDiceLanding = false;
            awaitingInput = false;
            ClearSkillBar();
            actorGlow.enabled = false;
            RefreshAllCells();

            ReportBattleFinished();

            bool victory = battle.Outcome == BattleOutcome.Victory;
            CompleteBattleVoices(victory);
            LayoutResult(victory);
            string ticker = resultScreens.Bind(battle, kit.Font, unit =>
                PanelKit.MemberSpriteOrNull(unit.Definition.Id, false) ?? BattlePortrait(unit.Definition.Id),
                resultHeroFraction, resultRowTop);
            BattleUnit mvp = battle.FindUnit(resultScreens.MvpUnitId);
            resultMvpNote.text = string.Empty; // R2: the hero band already names the MVP
            resultTicker.SetText(ticker, mvp != null ? "输出 MVP · " + mvp.Definition.Name : "本场成员记录");
            resultRewards.gameObject.SetActive(true);
            Transform oldIcons = resultRewardContent.Find("EarnedRewardIcons");
            if (oldIcons != null) { oldIcons.gameObject.SetActive(false); Destroy(oldIcons.gameObject); }
            resultRewardContent.sizeDelta = new Vector2(0f, 96f);
            recoveryActions.SetActive(!victory);
            resultRewardButton.interactable = victory && model.LastAwardedAccessory >= 0 && onGrowth != null;
            resultRewardScroll.transform.parent.gameObject.SetActive(victory);
            resultRewardTitle.text = victory ? "奖励" : "下一步怎么变强";
            resultTitle.text = victory ? "VICTORY" : "DEFEAT";
            resultTitleSub.text = victory ? "胜利" : "失败";
            resultTitle.color = victory ? Color.white : new Color32(196, 190, 220, 255);
            int stars = battle.StarRating();
            resultStars.text = StarText(stars);
            resultStars.color = victory ? (Color)new Color32(255, 212, 90, 255) : new Color32(120, 116, 150, 255);
            resultStats.text = battle.IsRealtime
                ? $"{battle.Stage.Name}\n用时 {battle.ElapsedMilliseconds / 1000f:0.0} 秒 · 我方倒下 {battle.PlayerUnitsLost} 人"
                : $"{battle.Stage.Name}\n共 {battle.Round} 回合 · 我方倒下 {battle.PlayerUnitsLost} 人";

            IReadOnlyList<string> lines = null;
            try
            {
                lines = rewardLines?.Invoke(battle);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            if (lines == null || lines.Count == 0)
            {
                resultRewards.text = victory ? "奖励已结算" : "挑战失败，体力已消耗，可调整阵容后重试";
            }
            else
            {
                var builder = new StringBuilder();
                for (int index = 0; index < lines.Count; index++)
                {
                    if (index > 0) builder.Append('\n');
                    builder.Append(lines[index]);
                }

                resultRewards.text = builder.ToString();
            }
            if (!victory)
            {
                resultRewards.text = BattleRecoveryAdvice.Describe(model, battle);
                resultRewards.alignment = TextAnchor.UpperLeft;
                resultRewards.fontSize = 16;
                PanelKit.EnableBestFit(resultRewards, 13);
            }
            else if (model.LastBattleRewards.Count > 0)
            {
                resultRewards.gameObject.SetActive(false);
                Transform rewardHost = resultRewardContent;
                Transform previous = rewardHost.Find("EarnedRewardIcons");
                if (previous != null) { previous.gameObject.SetActive(false); Destroy(previous.gameObject); }
                RectTransform icons = kit.NewRect("EarnedRewardIcons", rewardHost);
                var earned = model.LastBattleRewards.GroupBy(r => r.ItemId)
                    .Select(g => (ItemId: g.Key, Amount: g.Sum(r => r.Amount))).ToList();
                // R2 .rw: currencies as one cyan text line; equipment (owner 2026-10-21: "结算页面要看到获得的装备")
                // as real icons on a rarity glow with the item name in its rarity colour.
                var line = new StringBuilder();
                var gear = new List<(string ItemId, int Amount)>();
                foreach (var item in earned)
                {
                    if (GameModel.AccessoryIndexForItem(item.ItemId) >= 0) { gear.Add(item); continue; }
                    if (line.Length > 0) line.Append("    ");
                    line.Append(GameModel.RewardItemName(item.ItemId)).Append(" ×").Append(item.Amount.ToString("N0"));
                }
                const float areaHeight = 140f, gearWidth = 128f;
                PanelKit.PlaceTop(icons, 0, 0, 640, areaHeight);
                resultRewardContent.sizeDelta = new Vector2(0f, areaHeight);
                Text chips = kit.NewPlacedText(icons, line.ToString(), 26, ChoSiren.UI.SirenStickerUi.Cyan,
                    0, gear.Count > 0 ? 0 : 36, 640, 34, TextAnchor.MiddleCenter, FontStyle.Bold);
                if (gear.Count == 0)
                    kit.NewPlacedText(icons, "本次未掉落装备", 20, new Color32(150, 140, 175, 255),
                        0, 76, 640, 30, TextAnchor.MiddleCenter, FontStyle.Bold);
                chips.horizontalOverflow = HorizontalWrapMode.Wrap; chips.verticalOverflow = VerticalWrapMode.Overflow;
                PanelKit.EnableBestFit(chips, 16);
                int shown = Mathf.Min(5, gear.Count);
                float gearLeft = (640f - shown * gearWidth) * .5f;
                for (int i = 0; i < shown; i++)
                {
                    var item = gear[i];
                    float x = gearLeft + i * gearWidth;
                    Color rarity = ChoSiren.UI.RewardItemVisuals.AccentFor(item.ItemId);
                    Image halo = kit.NewImage("GearGlow-" + item.ItemId, icons, BattleFxArt1021.Dot, new Color(rarity.r, rarity.g, rarity.b, .75f));
                    if (BattleFxArt1021.Additive != null) halo.material = BattleFxArt1021.Additive;
                    halo.raycastTarget = false;
                    PanelKit.PlaceTop(halo.rectTransform, x + (gearWidth - 104f) * .5f, 26, 104, 104);
                    ChoSiren.UI.RewardItemVisuals.CreateIcon(icons, item.ItemId, "Earned-" + item.ItemId, x + (gearWidth - 76f) * .5f, 40, 76);
                    Text gearName = kit.NewPlacedText(icons, GameModel.RewardItemName(item.ItemId) + (item.Amount > 1 ? " ×" + item.Amount : string.Empty),
                        18, rarity, x, 114, gearWidth, 26, TextAnchor.MiddleCenter, FontStyle.Bold);
                    PanelKit.EnableBestFit(gearName, 11);
                    kit.AddOutline(gearName.gameObject, Color.black, 1.5f);
                }
            }

            if (resultRewards.gameObject.activeSelf)
            {
                float contentHeight = Mathf.Max(96f, resultRewards.preferredHeight + 8f);
                resultRewardContent.sizeDelta = new Vector2(0f, contentHeight);
                resultRewards.rectTransform.sizeDelta = new Vector2(640f, contentHeight);
            }
            resultRewardScroll.verticalNormalizedPosition = 1f;
            if (victory) kit.PlaySuccess();
            resultOverlay.SetActive(true);
            resultOverlay.transform.SetAsLastSibling();
            ApplyResultB1025(victory, stars);
            PlayResultEncore(victory, stars);
        }

        private ChoSiren.UI.BattleResultB1025 resultB1025;

        /// <summary>
        /// 2026-10-25 (少爷 picked option B): the P5 calling-card result replaces the R2 concert layout. The
        /// old pieces stay built (tests and the encore still find them) but hidden; 继续 and the recovery
        /// actions are restyled and moved onto the new page.
        /// </summary>
        private void ApplyResultB1025(bool victory, int stars)
        {
            Canvas.ForceUpdateCanvases();
            resultScreens.gameObject.SetActive(false);
            resultTitle.gameObject.SetActive(false);
            resultTitleSub.gameObject.SetActive(false);
            resultStars.gameObject.SetActive(false);
            resultBackdrop.color = Color.clear;
            resultRewardScroll.transform.parent.gameObject.SetActive(false);
            var loot = new List<ChoSiren.UI.BattleResultB1025.Loot>();
            if (victory)
                foreach (var group in model.LastBattleRewards.GroupBy(r => r.ItemId))
                    loot.Add(new ChoSiren.UI.BattleResultB1025.Loot { ItemId = group.Key, Amount = group.Sum(r => r.Amount) });
            // Currencies first, then equipment.
            loot = loot.OrderBy(l => GameModel.AccessoryIndexForItem(l.ItemId) >= 0 ? 1 : 0).ToList();
            if (resultB1025 != null) Destroy(resultB1025.gameObject);
            Action gearTapped = model.LastAwardedAccessory >= 0 && onGrowth != null ? () => OpenRecovery("accessory") : (Action)null;
            resultB1025 = ChoSiren.UI.BattleResultB1025.Show(resultCardRect, kit.Font, battle, victory, stars, loot,
                unit => PanelKit.MemberSpriteOrNull(unit.Definition.Id, false) ?? BattlePortrait(unit.Definition.Id),
                gearTapped, name => Sfx(name, .8f));
            // 2026-11-03: once the loot cards have popped in, their currencies fly to the header too.
            for (int i = 0; i < loot.Count; i++)
            {
                var card = resultB1025.transform.Find("Loot-" + loot[i].ItemId) ??
                           resultB1025.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(r => r.name == "Loot-" + loot[i].ItemId);
                if (card != null) ChoSiren.UI.RewardGain1101.FlyFrom(model, loot[i].ItemId, card.position, 1.7f + i * .07f);
            }

            float W = resultCardRect.rect.width > 1f ? resultCardRect.rect.width : 720f;
            var cont = resultCardRect.Find("ResultContinue") as RectTransform;
            if (cont != null)
            {
                Rect slot = resultB1025.ContinueSlot;
                PanelKit.PlaceTop(cont, slot.x, slot.y, slot.width, slot.height);
                cont.localEulerAngles = Vector3.zero;
                // Unified design: the shared sticker button. Victory → the teal primary; on a defeat 训练升级 is the
                // primary, so 继续 is the dark secondary (one green action per screen).
                Transform oldPlate = cont.Find("RedSkew");
                if (oldPlate != null) oldPlate.gameObject.SetActive(false);
                Text label = cont.GetComponentsInChildren<Text>(true).FirstOrDefault();
                if (label != null) { label.text = "继续 ▶"; label.name = "Label"; label.fontSize = 34; }
                ChoSiren.UI.SirenStickerUi.SkinButton(cont.gameObject, victory);
                cont.SetAsLastSibling();
            }
            if (!victory)
            {
                PanelKit.PlaceTop(recoveryActions.GetComponent<RectTransform>(), (W - 620f) * .5f, resultB1025.RecoveryTop, 620, 140);
                recoveryActions.transform.SetAsLastSibling();
                RestyleRecovery1101(); // 2026-11-02 visual-battle: icon + two characters
            }
        }

        private void OpenRecovery(string route)
        {
            if (closing) return;
            if (onGrowth == null) { Close(); return; }
            ReportBattleFinished();
            closing = true;
            gameObject.SetActive(false);
            Destroy(gameObject);
            onGrowth(route);
        }

        public static string StarText(int stars)
        {
            stars = Mathf.Clamp(stars, 0, 3);
            var builder = new StringBuilder(3);
            for (int index = 0; index < 3; index++) builder.Append(index < stars ? '★' : '☆');
            return builder.ToString();
        }

        // ------------------------------------------------------------------ lifecycle

        private void Notify(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            if (eventText != null) eventText.text = message;
            onMessage?.Invoke(message);
        }

        public void Close()
        {
            if (closing) return;
            if (battle != null && battle.Outcome == BattleOutcome.Ongoing)
            {
                ExitBattle();
                return;
            }

            // A programmatic close can race the result coroutine after the simulator has decided
            // an outcome but before ShowResult reports it. Settle that narrow window as well.
            ReportBattleFinished();
            CloseAfterSettlement(null);
        }

        private void ReportBattleFinished()
        {
            // The map must be live again before its finish callback runs (it may start coroutines).
            RestoreCoveredLayers1022();
            if (finishedReported || battle == null) return;
            finishedReported = true;
            try
            {
                onFinished?.Invoke(battle);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            // The callback normally owns settlement. Calling the idempotent model API afterwards
            // is a safety net for programmatic users that omit or throw from that callback.
            model?.SettleStageBattle(battle, out _);
        }

        private void CloseAfterSettlement(string message)
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

        private float lastDiceShake = -1f;
        private void OnDiceHardLanding(DiceRollPresentation die)
        {
            if (die == null || !dicePresentations.Contains(die) || psdRoot == null || closing) return;
            if (Time.unscaledTime - lastDiceShake < .2f) return;
            lastDiceShake = Time.unscaledTime;
            Sfx("dice-land", .8f);
            if (BattlePresentationOptions.ReduceMotion || stageShake != null) return;
            stageShake = StartCoroutine(ShakeStage(.16f, 6f));
        }

        private void OnDestroy()
        {
            RestoreCoveredLayers1022();
            // Phone browsers have little memory: drop the battle's art and clips once the battle closes.
            if (Application.isPlaying) { Resources.UnloadUnusedAssets(); System.GC.Collect(); }
            DiceRollPresentation.HardLanding -= OnDiceHardLanding;
            CancelUltimateTiming();
            if (punkPlateTexture != null) Destroy(punkPlateTexture);
            closing = true;
            StopAllCoroutines();
            for (int index = 0; index < runtimeSprites.Count; index++)
            {
                if (runtimeSprites[index] != null) Destroy(runtimeSprites[index]);
            }
            runtimeSprites.Clear();
            kit?.Dispose();
        }
    }
}
