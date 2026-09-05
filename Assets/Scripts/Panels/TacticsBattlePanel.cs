using System;
using System.Collections;
using System.Collections.Generic;
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
    public sealed class TacticsBattlePanel : MonoBehaviour
    {
        private const float EnemyCellWidth = 152f;
        private const float EnemyCellHeight = 84f;
        private const float PlayerCellWidth = 158f;
        // The roster lives in the band between the dice console and command deck.
        private const float PlayerCellHeight = 172f;
        private const float PlayerRosterLabelTop = 1170f;
        private const float PlayerRosterTop = 1204f;
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
        }

        private sealed class CellImpactState
        {
            public int Version;
            public Vector2 Origin;
            public Vector3 Scale;
            public Color Background;
        }

        /// <summary>Pointer enter/exit relay so anchors can preview their area on hover.</summary>
        private sealed class CellPointer : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            public Action Enter;
            public Action Exit;

            public void OnPointerEnter(PointerEventData eventData) => Enter?.Invoke();
            public void OnPointerExit(PointerEventData eventData) => Exit?.Invoke();
        }

        private static readonly Color CellIdle = new Color32(13, 16, 38, 255);
        private static readonly Color CellEmpty = new Color32(18, 16, 52, 150);
        private static readonly Color CellEnemy = new Color32(27, 17, 43, 255);
        private static readonly Color AnchorTint = new Color32(80, 220, 255, 40);
        private static readonly Color AffectedTint = new Color32(255, 82, 194, 58);
        private static readonly Color DiceIdle = new Color32(220, 235, 255, 235);
        private static readonly Color DiceParticipating = new Color32(255, 232, 170, 255);
        private static readonly Color DiceHeld = new Color32(255, 180, 235, 255);
        private static readonly Color DiceParticipatingHeld = new Color32(255, 204, 190, 255);

        private readonly List<CellView> cells = new List<CellView>();
        private readonly List<GameObject> skillButtons = new List<GameObject>();
        private readonly List<string> logHistory = new List<string>();
        private readonly List<Text> popupPool = new List<Text>();
        private readonly List<GameObject> diceButtons = new List<GameObject>();
        private readonly List<Image> diceFaceImages = new List<Image>();
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

        private Text turnText;
        private Text actorText;
        private Text eventText;
        private Text previewText;
        private Text logText;
        private Text roundFlash;
        private Image actorGlow;
        private RectTransform skillBar;
        private GameObject autoButton;
        private GameObject speedButton;
        private GameObject retreatButton;
        private GameObject exitButton;
        private GameObject pauseOverlay;
        private RectTransform popupLayer;
        private GameObject resultOverlay;
        private Text resultTitle;
        private Text resultStars;
        private Text resultStats;
        private Text resultRewards;
        private Image enemyHpFill;
        private Text enemyHpText;
        private Text phaseText;
        private Text timerText;
        private Text diceHandText;
        private Image diceEnergyFill;
        private Text diceEnergyText;
        private GameObject rerollButton;
        private GameObject energyRerollButton;
        private Sprite battleStageSprite;
        private readonly Sprite[] userDiceFaceSprites = new Sprite[6];
        private Sprite userBossSprite;
        private Sprite rerollRingSprite;
        private Sprite memberFrameSprite;
        private Sprite skillButtonFrameSprite;
        private Sprite bossHitSlashSprite;
        private Sprite bossHeartImpactSprite;
        private Sprite bossChargeAuraSprite;
        private Sprite bossLowHealthFrameSprite;
        private BossBattlePresentation bossPresentation;
        private Image battleReadabilityVeil;
        private readonly Dictionary<RectTransform, CellImpactState> cellImpacts =
            new Dictionary<RectTransform, CellImpactState>();

        public static TacticsBattlePanel Open(Transform host, GameModel gameModel, BattleSimulator simulator,
            Action<BattleSimulator> finished, Func<BattleSimulator, IReadOnlyList<string>> rewards = null,
            Action back = null, Action<string> message = null)
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
            var races = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (MemberDefinition member in GameModel.Members) races[member.Id] = member.Race;
            string captain = gameModel.Save.Team.Count > 0 ? GameModel.MemberIdAt(gameModel.Save.Team[0]) : null;
            simulator.EnableRealtime(races, captain, simulator.Stage.RerollLimit,
                simulator.Stage.EncounterType == "world");
            panel.diceTurn = simulator.BattleDice;
            panel.Build();
            panel.StartCoroutine(panel.MainLoop());
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
            BuildPopups();
            // Round flash sits above popups but below the result overlay.
            roundFlash.transform.SetAsLastSibling();
            BuildResult();
            RefreshAllCells();
        }

        private void LoadBattleAiArt()
        {
            battleStageSprite = LoadRuntimeSprite("Art/BattleAI/battle-stage-hud-v1");
            userBossSprite = LoadRuntimeSprite("Art/BattleUser/boss-throne-user-v1");
            for (int index = 0; index < userDiceFaceSprites.Length; index++)
                userDiceFaceSprites[index] = LoadRuntimeSprite($"Art/BattleUser/dice-face-{index + 1}-user-v1");
            rerollRingSprite = LoadRuntimeSprite("Art/BattleAI/reroll-ring-v1");
            memberFrameSprite = LoadRuntimeSprite("Art/BattleAI/member-skill-frame-v1");
            skillButtonFrameSprite = LoadRuntimeSprite("Art/BattleAI/skill-button-frame-v1");
            bossHitSlashSprite = LoadRuntimeSprite("Art/BattleAI/battle-hit-slash-ai-v1");
            bossHeartImpactSprite = LoadRuntimeSprite("Art/BattleAI/battle-heart-impact-ai-v1");
            bossChargeAuraSprite = LoadRuntimeSprite("Art/BattleAI/battle-charge-aura-ai-v1");
            bossLowHealthFrameSprite = LoadRuntimeSprite("Art/BattleAI/battle-low-health-frame-ai-v1");
        }

        private Sprite LoadRuntimeSprite(string resourcePath)
        {
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
            PanelKit.Stretch(art.rectTransform);
            art.preserveAspect = false;
            art.raycastTarget = false;

            battleReadabilityVeil = kit.NewImage("BattleReadabilityVeil", transform, null,
                new Color32(3, 4, 22, 46));
            PanelKit.Stretch(battleReadabilityVeil.rectTransform);
            battleReadabilityVeil.raycastTarget = false;

            // The authored backdrop contains obsolete dice pedestals and four oversized rings.
            // Cover that entire band once; transparent cards must not reveal a second UI underneath.
            Image commandSurface = kit.NewImage("BattleCommandSurface", transform, null,
                new Color32(8, 9, 27, 255));
            PanelKit.PlaceTop(commandSurface.rectTransform, 0, 836, 720, 700);
            commandSurface.raycastTarget = false;
        }

        private void BuildHeader()
        {
            Image header = kit.NewImage("BattleHud", transform, null, new Color32(4, 7, 28, 168));
            PanelKit.PlaceTop(header.rectTransform, 0, 0, 720, 132);
            header.raycastTarget = true;
            StageDefinition stage = battle.Stage;
            Image topLine = kit.NewImage("BossHudTopGlow", header.transform, kit.RoundedSprite(3),
                new Color32(255, 52, 182, 190));
            PanelKit.PlaceTop(topLine.rectTransform, 18, 2, 684, 3);
            Image hpFrame = kit.NewImage("BossHpFrame", header.transform, kit.RoundedSprite(14),
                new Color32(54, 20, 79, 232));
            PanelKit.PlaceTop(hpFrame.rectTransform, 14, 88, 692, 32);
            hpFrame.type = Image.Type.Sliced;
            kit.AddOutline(hpFrame.gameObject, new Color32(255, 77, 193, 128), 1.5f);

            kit.NewPlacedText(header.transform, stage.UsesRealtime ? stage.EncounterLabel : "♥ 首领", 13, PanelKit.Pink, 18, 10, 130, 22,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            Text stageTitle = kit.NewPlacedText(header.transform, stage.Name, 22, PanelKit.White,
                18, 31, 252, 28, TextAnchor.MiddleLeft, FontStyle.Bold);
            stageTitle.gameObject.name = "BattleStageTitle";
            PanelKit.EnableBestFit(stageTitle, 18);

            GameObject phaseBadge = kit.NewPanel("BossPhaseBadge", header.transform,
                new Color32(35, 24, 84, 224), 18);
            PanelKit.PlaceTop(phaseBadge.GetComponent<RectTransform>(), 282, 12, 150, 66);
            kit.AddOutline(phaseBadge, new Color32(155, 115, 255, 84), 1f);
            phaseText = kit.NewPlacedText(phaseBadge.transform, "阶段 1/3", 18, PanelKit.Gold, 6, 3, 138, 28,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            timerText = kit.NewPlacedText(phaseBadge.transform, "剩余 01:00", 16, PanelKit.Muted, 6, 34, 138, 24,
                TextAnchor.MiddleCenter, FontStyle.Bold);

            enemyHpFill = kit.NewBar("EnemyHp", header.transform, 18, 93, 684, 22,
                new Color32(43, 17, 64, 255), new Color32(255, 42, 153, 255), 11);
            enemyHpText = kit.NewPlacedText(header.transform, string.Empty, 13, PanelKit.White, 18, 91, 684, 24,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            kit.AddOutline(enemyHpText.gameObject, new Color32(24, 5, 40, 210), 1f);
            turnText = kit.NewPlacedText(header.transform, battle.IsRealtime ? "实时演出" : "第 1 回合", 14, PanelKit.Muted, 18, 60, 252, 22,
                TextAnchor.MiddleLeft);
            if (battle.RehearsalLevel > 0) turnText.text = $"适配试演 · {battle.RehearsalLevel}级档";

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
            Image stage = kit.NewImage("EnemyStage", transform, null, new Color32(4, 5, 24, 0));
            PanelKit.PlaceTop(stage.rectTransform, 0, 132, 720, 700);

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
            PanelKit.PlaceTop(stagePulse.rectTransform, 68, 350, 584, 320);
            PanelKit.CenterPivot(stagePulse.rectTransform);
            stagePulse.raycastTarget = false;

            Image shadow = kit.NewImage("BossGroundShadow", stage.transform, kit.RadialSprite(),
                new Color32(7, 3, 28, 115));
            PanelKit.PlaceTop(shadow.rectTransform, 158, 560, 404, 92);
            PanelKit.CenterPivot(shadow.rectTransform);
            shadow.raycastTarget = false;

            Image rearAura = kit.NewImage("BossAuraBack", stage.transform, rerollRingSprite ?? kit.RadialSprite(),
                new Color32(255, 60, 203, 54));
            PanelKit.PlaceTop(rearAura.rectTransform, 62, 56, 596, 596);
            PanelKit.CenterPivot(rearAura.rectTransform);
            rearAura.preserveAspect = true;
            rearAura.raycastTarget = false;

            Image coreAura = kit.NewImage("BossAuraCore", stage.transform, kit.RadialSprite(),
                new Color32(122, 105, 255, 46));
            PanelKit.PlaceTop(coreAura.rectTransform, 134, 135, 452, 452);
            PanelKit.CenterPivot(coreAura.rectTransform);
            coreAura.raycastTarget = false;

            Image chargeArt = kit.NewImage("BossChargeAuraAI", stage.transform, bossChargeAuraSprite, PanelKit.White);
            PanelKit.PlaceTop(chargeArt.rectTransform, 75, 180, 570, 500);
            PanelKit.CenterPivot(chargeArt.rectTransform);
            chargeArt.preserveAspect = true;
            chargeArt.raycastTarget = false;
            chargeArt.gameObject.SetActive(false);

            RectTransform rig = kit.NewRect("BossMotionRig", stage.transform);
            PanelKit.PlaceTop(rig, 65, 4, 590, 680);
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
            PanelKit.PlaceTop(stageCaption.GetComponent<RectTransform>(), 16, 18, 232, 84);
            kit.AddOutline(stageCaption, new Color32(255, 76, 198, 76), 1f);
            kit.NewPlacedText(stageCaption.transform, "当前演出", 10, PanelKit.Pink, 12, 6, 180, 18,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            kit.NewPlacedText(stageCaption.transform, "魅声舞台", 17, PanelKit.White, 12, 26, 190, 28,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            kit.NewPlacedText(stageCaption.transform, battle.IsRealtime ? "自动释放 · 点击敌人集火" : "手动选技能 · 点击高亮目标", 14, PanelKit.Muted,
                12, 56, 208, 24, TextAnchor.MiddleLeft).gameObject.name = "BattleControlInstruction";

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

            bossPresentation = stage.gameObject.AddComponent<BossBattlePresentation>();
            bossPresentation.Configure(rig, portrait, echo, rearAura, coreAura, shadow, stagePulse,
                lowHealthFrame, hitSlash, heartImpact, chargeArt, bossState, effectRings, slashTrails,
                damageTexts, () => paused, () => speed);
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
            CanvasGroup group = root.AddComponent<CanvasGroup>();
            Image background = root.GetComponent<Image>();
            background.raycastTarget = true;

            Button button = root.AddComponent<Button>();
            button.targetGraphic = background;
            button.transition = Selectable.Transition.None;
            BattleSide capturedSide = side;
            int capturedRow = row;
            int capturedCol = col;
            button.onClick.AddListener(() => CellClicked(capturedSide, capturedRow, capturedCol));
            CellPointer pointer = root.AddComponent<CellPointer>();
            pointer.Enter = () => CellHovered(capturedSide, capturedRow, capturedCol, true);
            pointer.Exit = () => CellHovered(capturedSide, capturedRow, capturedCol, false);

            Image portrait = kit.NewImage("Portrait", root.transform, null, PanelKit.White);
            if (player)
            {
                PanelKit.PlaceTop(portrait.rectTransform, 8, 8, width - 16, 98);
                portrait.preserveAspect = true;
                portrait.useSpriteMesh = true;
            }
            else
            {
                portrait.enabled = false;
            }

            Image ornament = kit.NewImage("BattleFrame", root.transform, null, Color.clear);
            PanelKit.Stretch(ornament.rectTransform);
            ornament.preserveAspect = true;
            ornament.raycastTarget = false;

            Image highlight = kit.NewImage("Highlight", root.transform, kit.RoundedSprite(12), AnchorTint);
            highlight.type = Image.Type.Sliced;
            PanelKit.Stretch(highlight.rectTransform);
            highlight.enabled = false;

            Text unitName = kit.NewPlacedText(root.transform, string.Empty, player ? 20 : 16, PanelKit.White,
                player ? 10 : 8, player ? 108 : 3, width - (player ? 20 : 16), player ? 30 : 26,
                player ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft, FontStyle.Bold);
            unitName.gameObject.name = "UnitName";
            PanelKit.EnableBestFit(unitName, player ? 18 : 12);
            Image hpFill = kit.NewBar("Hp", root.transform, player ? 12 : 8, player ? 144 : 34,
                width - (player ? 24 : 16), 6,
                new Color32(66, 54, 117, 255), player ? PanelKit.Cyan : PanelKit.Pink, 5);
            Text hpText = kit.NewPlacedText(root.transform, string.Empty, 13, PanelKit.Muted,
                player ? 10 : 8, player ? 152 : 42, width - (player ? 20 : 16), 20,
                player ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft);
            hpText.gameObject.name = "UnitHpText";
            Image shieldFill = kit.NewBar("Shield", root.transform, player ? 12 : 8, player ? 139 : 30,
                width - (player ? 24 : 16), 3,
                new Color32(50, 60, 110, 255), new Color32(150, 230, 255, 255), 3);
            GameObject shieldTrack = shieldFill.transform.parent.gameObject;
            Text status = kit.NewPlacedText(root.transform, string.Empty, 13, PanelKit.Gold,
                8, player ? 152 : 62, width - 16, 20,
                player ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft, FontStyle.Bold);
            status.gameObject.name = "UnitStatus";
            PanelKit.EnableBestFit(status, 12);
            Text fallen = kit.NewPlacedText(root.transform, "倒下", 14, new Color32(196, 190, 220, 255), 4, player ? 31 : 22,
                width - 8, 26, TextAnchor.MiddleCenter, FontStyle.Bold);
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
                Outline = kit.AddOutline(root, new Color32(166, 112, 255, 0), 2),
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
            return view;
        }

        private void BuildStatusStrip()
        {
            GameObject strip = kit.NewPanel("TurnStrip", transform, new Color32(12, 11, 47, 78), 12);
            PanelKit.PlaceTop(strip.GetComponent<RectTransform>(), 20, 840, 680, 54);
            actorText = kit.NewPlacedText(strip.transform, string.Empty, 18, PanelKit.White, 14, 3, 652, 25,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            eventText = kit.NewPlacedText(strip.transform, "战斗开始", 16, PanelKit.Muted, 14, 27, 652, 22,
                TextAnchor.MiddleLeft);
            PanelKit.EnableBestFit(actorText, 16);
            PanelKit.EnableBestFit(eventText, 14);

            roundFlash = kit.NewPlacedText(transform, string.Empty, 40, PanelKit.White, 60, 300, 600, 80,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            roundFlash.verticalOverflow = VerticalWrapMode.Overflow;
            kit.AddOutline(roundFlash.gameObject, new Color32(255, 74, 196, 200), 3);
            roundFlash.gameObject.SetActive(false);
        }

        private void BuildDiceConsole()
        {
            GameObject console = kit.NewPanel("DiceConsole", transform, new Color32(12, 14, 35, 255), 24);
            PanelKit.PlaceTop(console.GetComponent<RectTransform>(), 20, 900, 680, 264);
            kit.AddOutline(console, new Color32(102, 218, 255, 92), 1.25f);
            Image consoleGlow = kit.NewImage("DiceConsoleGlow", console.transform, kit.RadialSprite(),
                new Color32(120, 79, 255, 16));
            PanelKit.PlaceTop(consoleGlow.rectTransform, 0, 0, 680, 264);
            consoleGlow.raycastTarget = false;
            consoleGlow.transform.SetAsFirstSibling();
            Image titleLine = kit.NewImage("DiceConsoleTitleLine", console.transform, kit.RoundedSprite(2),
                new Color32(106, 222, 255, 132));
            PanelKit.PlaceTop(titleLine.rectTransform, 18, 48, 644, 2);
            Text diceTitle = kit.NewPlacedText(console.transform, "骰子演出", 18, PanelKit.Pink,
                18, 9, 180, 24, TextAnchor.MiddleLeft, FontStyle.Bold);
            diceTitle.gameObject.name = "DiceConsoleTitle";
            diceHandText = kit.NewPlacedText(console.transform, "等待骰子回合", 18, PanelKit.White,
                214, 3, 274, 45, TextAnchor.MiddleCenter, FontStyle.Bold);
            diceHandText.gameObject.name = "DiceHandSummary";
            PanelKit.EnableBestFit(diceHandText, 16);
            diceEnergyText = kit.NewPlacedText(console.transform, "能量 0/100", 18, PanelKit.Cyan,
                500, 10, 160, 24, TextAnchor.MiddleRight, FontStyle.Bold);
            diceEnergyFill = kit.NewBar("DiceEnergy", console.transform, 500, 37, 160, 8,
                new Color32(49, 42, 90, 255), PanelKit.Cyan, 4);

            const float dieSize = 104f;
            const float gap = 22f;
            for (int index = 0; index < DiceRules.DiceCount; index++)
            {
                int captured = index;
                GameObject die = kit.NewButton("Dice-" + index, console.transform, "?", 38,
                    Color.clear, PanelKit.White, () => ToggleDie(captured), 20);
                PanelKit.PlaceTop(die.GetComponent<RectTransform>(), 36 + index * (dieSize + gap), 76, dieSize, dieSize);
                Image dieArt = die.GetComponent<Image>();
                // The button remains a full-size invisible hit target. The authored diamond die is the only visible art.
                dieArt.sprite = null;
                dieArt.type = Image.Type.Simple;
                dieArt.color = Color.clear;
                Image faceArt = kit.NewImage("DiceFace-" + index, die.transform, null, PanelKit.White);
                PanelKit.Stretch(faceArt.rectTransform);
                faceArt.type = Image.Type.Simple;
                faceArt.preserveAspect = true;
                faceArt.useSpriteMesh = true;
                faceArt.raycastTarget = false;
                Outline outline = kit.AddOutline(faceArt.gameObject, new Color32(96, 220, 255, 90), 1f);
                Text held = kit.NewPlacedText(console.transform, "", 16, PanelKit.Gold,
                    36 + index * (dieSize + gap), 184, dieSize, 24,
                    TextAnchor.MiddleCenter, FontStyle.Bold);
                held.gameObject.name = "DiceStatus-" + index;
                diceButtons.Add(die);
                diceFaceImages.Add(faceArt);
                diceHoldLabels.Add(held);
                diceOutlines.Add(outline);

            }

            rerollButton = kit.NewButton("DiceReroll", console.transform, "重投未保留 · 2次", 20,
                PanelKit.ButtonDark, PanelKit.White, RerollDice, 12);
            PanelKit.PlaceTop(rerollButton.GetComponent<RectTransform>(), 36, 214, 292, 42);
            energyRerollButton = kit.NewButton("EnergyReroll", console.transform, "能量重投", 20,
                PanelKit.ButtonDark, PanelKit.White, EnergyRerollDice, 12);
            PanelKit.PlaceTop(energyRerollButton.GetComponent<RectTransform>(), 352, 214, 292, 42);
            Text diceHint = kit.NewPlacedText(console.transform,
                battle.IsRealtime ? (diceTurn.SelectiveReroll
                    ? "人鱼队长：保留3至4颗，精准重投其余骰子"
                    : "伤害与击杀积攒能量 · 骰型持续增益全队") : "点击保留 · 骰型加成下一技能", 16,
                PanelKit.Muted, 36, 50, 608, 26, TextAnchor.MiddleCenter);
            diceHint.gameObject.name = "DiceInstruction";
            PanelKit.EnableBestFit(diceHint, 16);

            kit.NewPlacedText(transform, "出战成员", 18, PanelKit.Cyan,
                28, PlayerRosterLabelTop, 180, 30,
                TextAnchor.MiddleLeft, FontStyle.Bold).gameObject.name = "TeamRoster";
            RefreshDiceUi();
        }

        private void BuildSkillBar()
        {
            GameObject frame = kit.NewPanel("SkillCommandDeck", transform, new Color32(12, 14, 35, 255), 16);
            PanelKit.PlaceTop(frame.GetComponent<RectTransform>(), 20, 1386, 680, 92);
            kit.AddOutline(frame, new Color32(114, 207, 255, 76), 1.25f);
            skillWaitingText = kit.NewPlacedText(frame.transform, "等待演出开始", 20,
                PanelKit.Muted, 18, 14, 644, 64, TextAnchor.MiddleCenter);
            skillWaitingText.gameObject.name = "SkillWaitingState";
            skillBar = kit.NewRect("SkillBar", transform);
            PanelKit.PlaceTop(skillBar, 24, 1390, 672, 84);
        }

        private void BuildControls()
        {
            autoButton = kit.NewButton("AutoToggle", transform, "自动", 20, PanelKit.ButtonDark, PanelKit.White,
                ToggleAuto, 12);
            if (battle.IsRealtime)
            {
                PanelKit.LabelOf(autoButton).text = "手动重投";
                PanelKit.LabelOf(autoButton).fontSize = 17;
                PanelKit.EnableBestFit(PanelKit.LabelOf(autoButton), 15);
            }
            PanelKit.PlaceTop(autoButton.GetComponent<RectTransform>(), 440, 20, 94, 52);
            speedButton = kit.NewButton("SpeedToggle", transform, "1倍", 20, PanelKit.ButtonDark, PanelKit.White,
                ToggleSpeed, 12);
            PanelKit.PlaceTop(speedButton.GetComponent<RectTransform>(), 542, 20, 72, 52);
            retreatButton = kit.NewButton("PauseToggle", transform, "暂停", 20, PanelKit.ButtonDark,
                PanelKit.White, TogglePause, 12);
            PanelKit.PlaceTop(retreatButton.GetComponent<RectTransform>(), 622, 20, 78, 52);

            Image pauseShade = kit.NewImage("PauseOverlay", transform, null, new Color32(3, 5, 24, 168));
            PanelKit.Stretch(pauseShade.rectTransform);
            pauseShade.raycastTarget = true;
            GameObject dialog = kit.NewPanel("PauseDialog", pauseShade.transform, new Color32(17, 17, 43, 255), 24);
            PanelKit.PlaceTop(dialog.GetComponent<RectTransform>(), 140, 590, 440, 320);
            kit.AddOutline(dialog, new Color32(146, 120, 218, 120), 1);
            kit.NewPlacedText(dialog.transform, "演出已暂停", 30, PanelKit.White, 20, 24, 400, 54,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            GameObject resume = kit.NewButton("ResumeBattle", dialog.transform, "继续演出", 24,
                new Color32(87, 49, 143, 255), PanelKit.White, TogglePause, 12);
            PanelKit.PlaceTop(resume.GetComponent<RectTransform>(), 32, 100, 376, 60);
            exitButton = kit.NewButton("BattleExit", dialog.transform, "退出战斗", 22,
                PanelKit.ButtonDark, PanelKit.Muted, ExitBattle, 12);
            PanelKit.PlaceTop(exitButton.GetComponent<RectTransform>(), 32, 178, 376, 60);
            kit.NewPlacedText(dialog.transform, "退出不返还已消耗的体力", 18, PanelKit.Muted,
                20, 258, 400, 36, TextAnchor.MiddleCenter);
            pauseOverlay = pauseShade.gameObject;
            pauseOverlay.SetActive(false);
        }

        private void BuildPreview()
        {
            GameObject panel = kit.NewPanel("PreviewBoard", transform, new Color32(18, 15, 56, 64), 12);
            PanelKit.PlaceTop(panel.GetComponent<RectTransform>(), 20, 1486, 680, 40);
            previewText = kit.NewPlacedText(panel.transform, "保留骰子并重投，再选择技能和目标", 16,
                PanelKit.Muted, 12, 2, 656, 36, TextAnchor.MiddleCenter);
            PanelKit.EnableBestFit(previewText, 14);
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
            for (int index = 0; index < PopupPoolSize; index++)
            {
                Text popup = kit.NewText("Popup", popupLayer, string.Empty, 22, PanelKit.White, FontStyle.Bold,
                    TextAnchor.MiddleCenter);
                popup.horizontalOverflow = HorizontalWrapMode.Overflow;
                popup.verticalOverflow = VerticalWrapMode.Overflow;
                PanelKit.PlaceTop(popup.rectTransform, 0, 0, 160, 40);
                PanelKit.CenterPivot(popup.rectTransform);
                kit.AddOutline(popup.gameObject, new Color32(10, 8, 30, 220), 1.5f);
                popup.gameObject.SetActive(false);
                popupPool.Add(popup);
            }
        }

        private void BuildResult()
        {
            Image overlay = kit.NewImage("BattleResult", transform, null, new Color32(2, 4, 22, 132));
            PanelKit.Stretch(overlay.rectTransform);
            overlay.raycastTarget = true;
            resultOverlay = overlay.gameObject;

            GameObject card = kit.NewPanel("BattleResultCard", overlay.transform, new Color32(19, 14, 45, 255), 30);
            PanelKit.PlaceCentered(card.GetComponent<RectTransform>(), 610, 720);
            kit.AddOutline(card, new Color32(104, 220, 255, 184), 2);
            Image resultHalo = kit.NewImage("ResultHalo", card.transform, kit.RadialSprite(),
                new Color32(255, 66, 202, 72));
            PanelKit.PlaceTop(resultHalo.rectTransform, 80, 30, 450, 450);
            resultHalo.raycastTarget = false;
            resultHalo.transform.SetAsFirstSibling();

            kit.NewPlacedText(card.transform, "战术演出结算", 15, new Color32(255, 174, 226, 255), 40, 36, 530, 30,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            resultTitle = kit.NewPlacedText(card.transform, "胜利", 66, PanelKit.White, 40, 78, 530, 110,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            resultTitle.verticalOverflow = VerticalWrapMode.Overflow;
            kit.AddOutline(resultTitle.gameObject, new Color32(255, 71, 196, 155), 3);
            resultStars = kit.NewPlacedText(card.transform, "★★★", 44, PanelKit.Gold, 40, 196, 530, 66,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            resultStars.verticalOverflow = VerticalWrapMode.Overflow;
            resultStats = kit.NewPlacedText(card.transform, string.Empty, 18, PanelKit.White, 52, 272, 506, 70,
                TextAnchor.UpperCenter, FontStyle.Bold);
            GameObject reward = kit.NewPanel("BattleReward", card.transform, new Color32(30, 25, 58, 255), 22);
            PanelKit.PlaceTop(reward.GetComponent<RectTransform>(), 48, 356, 514, 210);
            kit.NewPlacedText(reward.transform, "奖励", 14, new Color32(255, 174, 226, 255), 20, 12, 474, 24,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            resultRewards = kit.NewPlacedText(reward.transform, string.Empty, 16, PanelKit.White, 20, 40, 474, 162,
                TextAnchor.UpperCenter, FontStyle.Bold);
            resultRewards.lineSpacing = 1.2f;
            GameObject done = kit.NewButton("ResultContinue", card.transform, "继续", 21, PanelKit.White, PanelKit.White,
                Close, 24);
            PanelKit.PlaceTop(done.GetComponent<RectTransform>(), 140, 604, 330, 70);
            Image doneFrame = done.GetComponent<Image>();
            doneFrame.sprite = skillButtonFrameSprite;
            doneFrame.type = Image.Type.Simple;
            doneFrame.preserveAspect = false;
            Image doneGlass = kit.NewImage("ResultButtonGlass", done.transform, kit.RoundedSprite(20),
                new Color32(48, 24, 88, 128));
            PanelKit.Stretch(doneGlass.rectTransform, 10, 9, -10, -9);
            doneGlass.type = Image.Type.Sliced;
            doneGlass.raycastTarget = false;
            doneGlass.transform.SetAsFirstSibling();
            resultOverlay.SetActive(false);
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
                        if (diceTurn.CanEnergyReroll) diceTurn.EnergyRerollAll(out _);
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
            yield return WaitBattleDelay(BattleIntroSeconds);
            while (!closing && battle.Outcome == BattleOutcome.Ongoing)
            {
                if (!paused)
                {
                    // A suspended browser must not instantly consume the encounter on returning.
                    realtimeFractionMs += Mathf.Min(Time.unscaledDeltaTime, 0.1f) * speed * 1000f;
                    int elapsedMs = Mathf.FloorToInt(realtimeFractionMs);
                    realtimeFractionMs -= elapsedMs;
                    battle.AdvanceRealtime(elapsedMs, autoMode);
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
            if (inputActor == null || !inputActor.Alive)
                foreach (BattleUnit unit in battle.Units)
                    if (unit.Side == BattleSide.Player && unit.Alive) { inputActor = unit; break; }
            if (inputActor == null) return;
            if (displayedPerformerId != inputActor.Id)
            {
                ClearSkillBar();
                displayedPerformerId = inputActor.Id;
                if (skillWaitingText != null) skillWaitingText.gameObject.SetActive(false);
                for (int i = 0; i < 2; i++)
                {
                    string description = BattleSimulator.ActiveSkillDescription(battle.RaceOf(inputActor), i == 1);
                    GameObject button = kit.NewButton("RealtimeSkill-" + i, skillBar, "", 20,
                        CellIdle, PanelKit.White,
                        () => Notify(description), 12);
                    PanelKit.PlaceTop(button.GetComponent<RectTransform>(), i * 342, 0, 330, 80);
                    skillButtons.Add(button);
                    PanelKit.EnableBestFit(PanelKit.LabelOf(button), 17);
                }
            }
            for (int i = 0; i < skillButtons.Count; i++)
            {
                SkillDefinition skill = battle.LookupSkill(battle.ActiveSkillId(inputActor, i == 1));
                float remaining = battle.SkillCooldownRemaining(inputActor, i == 1) / 1000f;
                PanelKit.LabelOf(skillButtons[i]).text = $"{skill.Name}\n自动 · {remaining:0.0}秒";
            }
            actorText.text = $"{inputActor.Definition.Name} · 普攻与双技能自动释放";
            BattleUnit focused = battle.FindUnit(battle.FocusTargetId);
            previewText.text = focused != null && focused.Alive
                ? $"集火：{focused.Definition.Name} · 点击其他敌人切换"
                : "点击角色查看技能 · 点击敌人集火";
            SetActorHighlight(inputActor);
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

        private float BattleAnimationDelta()
        {
            return Time.unscaledDeltaTime * Mathf.Clamp(speed, 1, 2);
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

        /// <summary>Applies one log entry to the view and returns how long to pause after it.</summary>
        private float PresentEvent(BattleEvent battleEvent, int previousActor)
        {
            BattleUnit actor = battleEvent.ActorId >= 0 ? battle.FindUnit(battleEvent.ActorId) : null;
            BattleUnit target = battleEvent.TargetId >= 0 ? battle.FindUnit(battleEvent.TargetId) : null;
            SkillDefinition skill = string.IsNullOrEmpty(battleEvent.SkillId) ? null : battle.LookupSkill(battleEvent.SkillId);
            string actorName = actor != null ? actor.Definition.Name : "——";
            string targetName = target != null ? target.Definition.Name : "——";
            string skillName = skill != null ? skill.Name : "技能";
            bool sameAction = previousActor == battleEvent.ActorId && battleEvent.Kind != BattleEventKind.RoundStart;

            switch (battleEvent.Kind)
            {
                case BattleEventKind.RoundStart:
                    turnText.text = $"第 {battleEvent.Amount} 回合";
                    AppendLog($"—— 第 {battleEvent.Amount} 回合 ——");
                    StartCoroutine(FlashRound(battleEvent.Amount));
                    RefreshAllCells();
                    return 0.35f;
                case BattleEventKind.Damage:
                    RefreshCell(target);
                    CellView damagedCell = FindCell(target);
                    if (damagedCell != null) StartCoroutine(AnimateCellImpact(damagedCell, battleEvent.Critical));
                    bool presentedByBossLayer = target != null && target.Side == BattleSide.Enemy &&
                                                bossPresentation != null;
                    if (presentedByBossLayer)
                    {
                        // The boss presentation owns its floating damage numbers.  Showing the generic
                        // cell popup as well makes one hit look like two separate damage events.
                        bossPresentation.PlayHit(battleEvent.Amount, battleEvent.Critical);
                    }
                    else
                    {
                        SpawnPopup(target, battleEvent.Critical ? $"暴击 -{battleEvent.Amount}" : $"-{battleEvent.Amount}",
                            battleEvent.Critical ? new Color32(255, 170, 80, 255) : new Color32(255, 120, 150, 255),
                            battleEvent.Critical ? 26 : 22);
                    }
                    eventText.text = $"{actorName} 使用「{skillName}」对 {targetName} 造成 {battleEvent.Amount} 伤害" +
                                     (battleEvent.Critical ? "（暴击）" : string.Empty);
                    AppendLog(eventText.text);
                    return sameAction ? 0.14f : BeatSeconds;
                case BattleEventKind.Heal:
                    RefreshCell(target);
                    SpawnPopup(target, $"+{battleEvent.Amount}", new Color32(120, 255, 170, 255), 22);
                    eventText.text = $"{actorName} 使用「{skillName}」为 {targetName} 恢复 {battleEvent.Amount}";
                    AppendLog(eventText.text);
                    return sameAction ? 0.14f : BeatSeconds;
                case BattleEventKind.Shield:
                    RefreshCell(target);
                    SpawnPopup(target, $"护盾 +{battleEvent.Amount}", new Color32(150, 230, 255, 255), 20);
                    eventText.text = $"{actorName} 使用「{skillName}」为 {targetName} 施加 {battleEvent.Amount} 护盾";
                    AppendLog(eventText.text);
                    return sameAction ? 0.14f : BeatSeconds;
                case BattleEventKind.Buff:
                    RefreshCell(target);
                    string effectLabel = skill != null ? EffectShort(skill.Effect) : "状态";
                    SpawnPopup(target, effectLabel, PanelKit.Gold, 20);
                    eventText.text = $"{actorName} 使用「{skillName}」：{targetName} {effectLabel}";
                    AppendLog(eventText.text);
                    return sameAction ? 0.14f : BeatSeconds;
                case BattleEventKind.Defeated:
                    RefreshCell(target);
                    CellView cell = FindCell(target);
                    if (cell != null) StartCoroutine(FadeOut(cell));
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
                    if (battle.Outcome == BattleOutcome.Ongoing && bossPresentation != null)
                        bossPresentation.PlayPhaseSurge(phase);
                    StartCoroutine(FlashPhase(phase));
                    return 0.45f;
                case BattleEventKind.Finished:
                    AppendLog(battleEvent.Outcome == BattleOutcome.Victory ? "战斗胜利" : "战斗失败");
                    eventText.text = battleEvent.Outcome == BattleOutcome.Victory ? "敌方全灭，演出成功"
                        : battle.IsRealtime ? "我方全灭或已达到60秒时限" : "我方全灭或超出回合上限";
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

        private IEnumerator FadeOut(CellView cell)
        {
            const float duration = 0.45f;
            float elapsed = 0f;
            cell.Fallen.gameObject.SetActive(true);
            while (elapsed < duration)
            {
                if (paused)
                {
                    yield return null;
                    continue;
                }
                elapsed += BattleAnimationDelta();
                cell.Group.alpha = Mathf.Lerp(1f, 0.38f, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            cell.Group.alpha = 0.38f;
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

        private void SpawnPopup(BattleUnit target, string text, Color color, int fontSize)
        {
            CellView cell = FindCell(target);
            if (cell == null) return;
            int slot = -1;
            // Small portrait cards show the newest feedback for that target. Every underlying
            // hit remains in the combat log; overlapping text must not obscure the member.
            for (int i = 0; i < popupPool.Count; i++)
                if (popupOwners[i] == target.Id && popupPool[i].gameObject.activeSelf) { slot = i; break; }
            if (slot < 0) { slot = popupIndex; popupIndex = (popupIndex + 1) % popupPool.Count; }
            Text popup = popupPool[slot];
            if (popupRoutines[slot] != null) StopCoroutine(popupRoutines[slot]);
            popupOwners[slot] = target.Id;
            popup.text = text;
            popup.color = color;
            popup.fontSize = fontSize;
            Vector2 anchored = cell.Rect.anchoredPosition;
            popup.rectTransform.anchoredPosition = new Vector2(anchored.x + cell.Rect.rect.width * 0.5f,
                anchored.y - cell.Rect.rect.height * 0.45f);
            popup.gameObject.SetActive(true);
            popup.transform.SetAsLastSibling();
            popupRoutines[slot] = StartCoroutine(AnimatePopup(popup));
        }

        private IEnumerator AnimatePopup(Text popup)
        {
            const float duration = 0.7f;
            float elapsed = 0f;
            Vector2 start = popup.rectTransform.anchoredPosition;
            Color color = popup.color;
            while (elapsed < duration && popup.gameObject.activeSelf)
            {
                if (paused)
                {
                    yield return null;
                    continue;
                }
                elapsed += BattleAnimationDelta();
                float t = Mathf.Clamp01(elapsed / duration);
                popup.rectTransform.anchoredPosition = start + new Vector2(0f, 46f * t);
                color.a = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, t));
                popup.color = color;
                yield return null;
            }

            popup.gameObject.SetActive(false);
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
            for (int index = 0; index < cells.Count; index++) cells[index].Unit = null;
            IReadOnlyList<BattleUnit> units = battle.Units;
            for (int index = 0; index < units.Count; index++)
            {
                CellView cell = FindCell(units[index]);
                if (cell != null) cell.Unit = units[index];
            }

            int playerIndex = 0;
            int enemyIndex = 0;
            for (int index = 0; index < cells.Count; index++)
            {
                CellView cell = cells[index];
                if (cell.Unit != null)
                {
                    if (cell.Side == BattleSide.Player)
                    {
                        int row = playerIndex / 4;
                        int col = playerIndex % 4;
                        PanelKit.PlaceTop(cell.Rect, 20f + col * 174f, PlayerRosterTop + row * 182f,
                            PlayerCellWidth, PlayerCellHeight);
                        playerIndex++;
                    }
                    else
                    {
                        int pair = enemyIndex / 2;
                        float left = enemyIndex % 2 == 0 ? 548f : 20f;
                        PanelKit.PlaceTop(cell.Rect, left, 304f + pair * 94f,
                            EnemyCellWidth, EnemyCellHeight);
                        enemyIndex++;
                    }
                }
                ApplyCell(cell);
            }
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
            bool occupied = unit != null;
            cell.Root.SetActive(occupied);
            if (!occupied) return;
            cell.Name.gameObject.SetActive(occupied);
            cell.HpFill.transform.parent.gameObject.SetActive(occupied);
            cell.HpText.gameObject.SetActive(occupied);
            cell.Status.gameObject.SetActive(occupied);
            cell.Background.color = cell.Side == BattleSide.Player ? CellIdle : CellEnemy;
            cell.Name.text = unit.Definition.Name;
            if (cell.Portrait != null)
            {
                Sprite portrait = cell.Side == BattleSide.Player
                    ? BattlePortrait(unit.Definition.Id)
                    : null;
                cell.Portrait.sprite = portrait;
                cell.Portrait.enabled = portrait != null;
            }
            cell.HpFill.fillAmount = unit.MaxHp > 0 ? Mathf.Clamp01(unit.Hp / (float)unit.MaxHp) : 0f;
            cell.HpText.text = $"{unit.Hp}/{unit.MaxHp}";
            bool shielded = unit.Shield > 0;
            cell.ShieldTrack.SetActive(shielded);
            if (shielded) cell.ShieldFill.fillAmount = Mathf.Clamp01(unit.Shield / (float)Mathf.Max(1, unit.MaxHp));
            string realtimeStatus = battle.IsRealtime ? battle.RealtimeStatus(unit) : string.Empty;
            cell.Status.text = string.IsNullOrEmpty(realtimeStatus) ? StatusSummary(unit) : realtimeStatus;
            if (cell.Side == BattleSide.Player)
                cell.HpText.gameObject.SetActive(string.IsNullOrEmpty(cell.Status.text));
            if (!unit.Alive)
            {
                cell.Fallen.gameObject.SetActive(true);
                cell.Group.alpha = 0.38f;
            }
            else
            {
                cell.Fallen.gameObject.SetActive(false);
                cell.Group.alpha = 1f;
            }
        }

        private Sprite BattlePortrait(string memberId)
        {
            if (battlePortraits.TryGetValue(memberId, out Sprite cached)) return cached;
            Sprite source = PanelKit.MemberSpriteOrNull(memberId, false);
            if (source == null) return null;
            // UI-only framing, never replace the shared full-body artwork. Values are normalized
            // texture rectangles, with y measured from the bottom (Unity sprite coordinates).
            Rect focus;
            switch (memberId)
            {
                case "xingli": focus = new Rect(.50f, .62f, .46f, .29f); break;
                case "yeying": focus = new Rect(.23f, .48f, .52f, .29f); break;
                case "feiyin": focus = new Rect(.30f, .60f, .48f, .30f); break;
                case "wubai": focus = new Rect(.32f, .66f, .48f, .29f); break;
                case "yaoguang": focus = new Rect(.32f, .61f, .48f, .30f); break;
                case "hupo": focus = new Rect(.27f, .63f, .52f, .31f); break;
                case "xianyue": focus = new Rect(.32f, .63f, .52f, .30f); break;
                case "chuxue": focus = new Rect(.25f, .55f, .52f, .30f); break;
                case "chengxia": focus = new Rect(.28f, .48f, .52f, .30f); break;
                default: focus = new Rect(.20f, .48f, .65f, .42f); break;
            }
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

        private void Update()
        {
            if (!paused && battle != null && !battle.IsRealtime && battle.Outcome == BattleOutcome.Ongoing)
                battleElapsed += Time.unscaledDeltaTime;
            RefreshBattleHud();
            if (actorGlow == null || !actorGlow.enabled) return;
            float pulse = 0.5f + Mathf.Sin(Time.unscaledTime * 4.2f) * 0.5f;
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
            IReadOnlyList<BattleUnit> units = battle.Units;
            for (int index = 0; index < units.Count; index++)
            {
                if (units[index].Side != BattleSide.Enemy) continue;
                current += Mathf.Max(0, units[index].Hp);
                maximum += Mathf.Max(1, units[index].MaxHp);
            }

            float normalized = maximum > 0 ? Mathf.Clamp01(current / (float)maximum) : 0f;
            enemyHpFill.fillAmount = normalized;
            enemyHpFill.color = normalized <= 0.3f
                ? Color.Lerp(new Color32(255, 34, 116, 255), new Color32(255, 154, 48, 255),
                    0.5f + Mathf.Sin(Time.unscaledTime * 6f) * 0.5f)
                : Color.Lerp(new Color32(255, 72, 208, 255), new Color32(255, 44, 143, 255), 1f - normalized);
            enemyHpText.text = $"{Mathf.RoundToInt(normalized * 100f)}%    {current:N0}/{maximum:N0}";
            enemyHpText.color = normalized <= 0.3f ? new Color32(255, 225, 174, 255) : PanelKit.White;
            if (battleReadabilityVeil != null)
            {
                Color veil = battleReadabilityVeil.color;
                veil.a = normalized <= 0.3f
                    ? Mathf.Lerp(0.12f, 0.24f, 0.5f + Mathf.Sin(Time.unscaledTime * 5.4f) * 0.5f)
                    : 46f / 255f;
                battleReadabilityVeil.color = veil;
            }
            if (bossPresentation != null) bossPresentation.SetHealthRatio(normalized);
            int phase = ResolveDisplayedEnemyPhase(battle.EnemyPhase, battle.Log, logCursor);
            phaseText.text = battle.Stage.UsesRealtime && !battle.Stage.HasBossPhases
                ? battle.Stage.EncounterLabel : $"阶段 {phase}/3";
            timerText.text = FormatBattleTimer(battleElapsed);
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
            if (paused || !awaitingInput || diceTurn == null) return;
            if (diceTurn.IsBattleSession && !diceTurn.SelectiveReroll)
            {
                Notify("人鱼队长可保留3至4颗骰子，其他队长使用全部重投");
                return;
            }
            diceTurn.ToggleHold(index);
            RefreshDiceUi();
        }

        private void RerollDice()
        {
            if (paused || !awaitingInput || diceTurn == null) return;
            if (!diceTurn.RerollUnheld(out string error)) Notify(error);
            RefreshDiceUi();
        }

        private void EnergyRerollDice()
        {
            if (paused || !awaitingInput || diceTurn == null) return;
            if (!diceTurn.EnergyRerollAll(out string error)) Notify(error);
            else diceEnergy = diceTurn.Energy;
            RefreshDiceUi();
        }

        private void RefreshDiceUi()
        {
            bool active = diceTurn != null && diceTurn.Hand != null;
            for (int index = 0; index < diceButtons.Count; index++)
            {
                Text label = PanelKit.LabelOf(diceButtons[index]);
                int face = active ? diceTurn.Values[index] : 0;
                Sprite faceSprite = face >= 1 && face <= userDiceFaceSprites.Length
                    ? userDiceFaceSprites[face - 1]
                    : null;
                if (index < diceFaceImages.Count)
                {
                    Image faceImage = diceFaceImages[index];
                    faceImage.sprite = faceSprite;
                    faceImage.enabled = faceSprite != null;
                }
                label.text = faceSprite != null ? string.Empty : active ? face.ToString() : "?";
                bool held = active && diceTurn.Held[index];
                bool participating = active && diceTurn.Hand.Participating[index];
                diceHoldLabels[index].text = held ? "已保留" : participating ? "成型" : "";
                Color background = participating
                    ? held ? DiceParticipatingHeld : DiceParticipating
                    : held ? DiceHeld : DiceIdle;
                Button dieButton = diceButtons[index].GetComponent<Button>();
                if (dieButton != null) dieButton.interactable = awaitingInput && !paused;
                Image hitArea = diceButtons[index].GetComponent<Image>();
                if (hitArea != null)
                {
                    hitArea.sprite = null;
                    hitArea.color = Color.clear;
                }
                if (index < diceFaceImages.Count)
                {
                    // Use a restrained tint on the die itself instead of restoring a square state background.
                    float tintStrength = participating ? 0.16f : held ? 0.11f : 0f;
                    diceFaceImages[index].color = Color.Lerp(PanelKit.White, background, tintStrength);
                }
                if (index < diceOutlines.Count)
                {
                    diceOutlines[index].effectColor = participating
                        ? new Color32(255, 204, 83, 150)
                        : held ? new Color32(255, 105, 210, 105) : new Color32(96, 220, 255, 56);
                    float distance = participating ? 2f : 1f;
                    diceOutlines[index].effectDistance = new Vector2(distance, -distance);
                }
            }

            int energy = active ? diceTurn.Energy : diceEnergy;
            if (diceEnergyFill != null) diceEnergyFill.fillAmount = energy / 100f;
            if (diceEnergyText != null) diceEnergyText.text = $"能量 {energy}/100";
            if (diceHandText != null)
                diceHandText.text = active
                    ? $"{diceTurn.Hand.DisplayName} ×{diceTurn.Hand.MultiplierPermille / 1000f:0.##}\n" +
                      $"总点 {diceTurn.Hand.PipTotal} · 计分点 {diceTurn.Hand.ParticipatingPipTotal}"
                    : "等待骰子回合";
            if (rerollButton != null)
            {
                int rerolls = active ? diceTurn.RerollsRemaining : 0;
                PanelKit.LabelOf(rerollButton).text = $"重投未保留 · {rerolls}次";
                PanelKit.SetButtonState(rerollButton, awaitingInput && rerolls > 0,
                    rerolls > 0 ? new Color32(126, 62, 181, 252) : PanelKit.Disabled);
            }
            if (energyRerollButton != null)
            {
                PanelKit.LabelOf(energyRerollButton).text = energy >= 100 ? "能量重投 · 就绪" : "能量重投 · 充能中";
                PanelKit.SetButtonState(energyRerollButton, awaitingInput && active && diceTurn.CanEnergyReroll,
                    energy >= 100 ? new Color32(58, 71, 126, 255) : PanelKit.ButtonDark);
            }
            if (active && diceTurn.IsBattleSession)
            {
                diceHandText.text = $"{diceTurn.Hand.DisplayName} ×{diceTurn.Hand.MultiplierPermille / 1000f:0.##}\n" +
                    $"本场重投 {diceTurn.UsedRerolls}/{diceTurn.BattleRerollLimit}";
                bool canReroll = awaitingInput && !paused && diceTurn.CanEnergyReroll;
                PanelKit.LabelOf(rerollButton).text = diceTurn.SelectiveReroll
                    ? "精准重投 · 人鱼" : $"本场剩余 {diceTurn.RerollsRemaining} 次";
                PanelKit.SetButtonState(rerollButton, canReroll && diceTurn.SelectiveReroll,
                    canReroll && diceTurn.SelectiveReroll ? PanelKit.ButtonDark : PanelKit.Disabled);
                PanelKit.LabelOf(energyRerollButton).text = diceTurn.FreeRerolls > 0 ? "免费重投 · 1次"
                    : diceTurn.RerollsRemaining == 0 ? "本场重投已用完"
                    : canReroll ? "全部重投 · 100能量" : "全部重投 · 充能中";
                PanelKit.SetButtonState(energyRerollButton, canReroll,
                    canReroll ? new Color32(100, 55, 151, 255) : PanelKit.ButtonDark);
            }
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

        private void CellClicked(BattleSide side, int row, int col)
        {
            kit.PlayClick();
            if (paused) return;
            if (battle.IsRealtime)
            {
                BattleUnit unit = battle.UnitAt(side, row, col);
                if (unit == null) return;
                if (side == BattleSide.Player) inputActor = unit;
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
            autoMode = !autoMode;
            if (battle.IsRealtime)
            {
                PanelKit.LabelOf(autoButton).text = autoMode ? "自动重投" : "手动重投";
                PanelKit.SetButtonState(autoButton, true,
                    autoMode ? new Color32(116, 43, 177, 252) : PanelKit.ButtonDark);
                Notify(autoMode ? "已开启自动重投，角色技能持续自动释放" : "已切换手动重投，角色技能持续自动释放");
                return;
            }
            PanelKit.LabelOf(autoButton).text = autoMode ? "自动开" : "自动";
            PanelKit.SetButtonState(autoButton, true, autoMode ? new Color32(116, 43, 177, 252) : PanelKit.ButtonDark);
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
            speed = speed == 1 ? 2 : 1;
            PanelKit.LabelOf(speedButton).text = speed == 2 ? "2倍" : "1倍";
            PanelKit.SetButtonState(speedButton, true, speed == 2 ? new Color32(116, 43, 177, 252) : PanelKit.ButtonDark);
            Notify(speed == 2 ? "已切换到 2 倍速" : "已恢复 1 倍速");
        }

        private void TogglePause()
        {
            paused = !paused;
            PanelKit.LabelOf(retreatButton).text = paused ? "继续" : "暂停";
            PanelKit.SetButtonState(retreatButton, true,
                paused ? new Color32(116, 43, 177, 252) : PanelKit.ButtonDark);
            if (pauseOverlay != null)
            {
                pauseOverlay.SetActive(paused);
                if (paused) pauseOverlay.transform.SetAsLastSibling();
                // Modal shade blocks battle input; this one control can still close the pause menu.
                retreatButton.transform.SetAsLastSibling();
                if (exitButton != null) exitButton.transform.SetAsLastSibling();
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
            awaitingInput = false;
            ClearSkillBar();
            actorGlow.enabled = false;
            RefreshAllCells();

            ReportBattleFinished();

            bool victory = battle.Outcome == BattleOutcome.Victory;
            resultTitle.text = victory ? "胜利" : "失败";
            resultTitle.color = victory ? PanelKit.White : new Color32(196, 190, 220, 255);
            int stars = battle.StarRating();
            resultStars.text = StarText(stars);
            resultStars.color = victory ? PanelKit.Gold : new Color32(120, 116, 150, 255);
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

            if (victory) kit.PlaySuccess();
            resultOverlay.SetActive(true);
            resultOverlay.transform.SetAsLastSibling();
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

        private void OnDestroy()
        {
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
