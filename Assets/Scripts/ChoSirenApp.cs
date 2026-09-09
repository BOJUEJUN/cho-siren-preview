using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ChoSiren.Panels;
using ChoSiren.Systems.Economy;
using ChoSiren.Systems.Tactics;
using ChoSiren.Systems.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ChoSiren
{
    public sealed partial class ChoSirenApp : MonoBehaviour
    {
        private static readonly Color Ink = new Color32(8, 12, 42, 255);
        private static readonly Color White = new Color32(248, 246, 255, 255);
        private static readonly Color Muted = new Color32(193, 187, 221, 255);
        private static readonly Color Pink = new Color32(255, 102, 190, 255);
        private static readonly Color Purple = new Color32(153, 97, 228, 255);
        private static readonly Color Cyan = new Color32(91, 215, 255, 255);
        private static readonly Color Glass = new Color32(28, 26, 78, 210);
        private static readonly Color GlassLight = new Color32(91, 55, 132, 215);

        private readonly Dictionary<int, Sprite> roundedSprites = new Dictionary<int, Sprite>();
        private readonly Dictionary<int, Sprite> generatedLobbySprites = new Dictionary<int, Sprite>();
        private readonly Dictionary<string, Sprite> aiUiSprites = new Dictionary<string, Sprite>();
        private readonly List<Sprite> runtimeAiUiSprites = new List<Sprite>();
        private readonly List<Image> navHighlights = new List<Image>();
        private Sprite[] navIconSprites;
        private Sprite[] lobbyEmblemSprites;
        private Sprite stageGlowSprite;
        private Sprite accessoryDressingRoomSprite;
        private Sprite teamStellarStageSprite;
        private Sprite memberGalleryCalmSprite;

        private GameModel model;
        private GameAudio gameAudio;
        private Font font;
        private Transform safeRoot;
        private RectTransform contentRoot;
        private RectTransform navRoot;
        private GameObject lobbyVideoObject;
        private LobbyVideoLoopPlayer lobbyVideoPlayer;
        private Text diamondText;
        private Text goldText;
        private Text staminaText;
        private Text teamLevelText;
        private Text toastText;
        private GameObject toastObject;
        private GameObject modalObject;
        private GameObject startupLoadingObject;
        private CanvasGroup startupLoadingGroup;
        private Text startupProgressText;
        private Image startupProgressFill;
        private bool startupFinished;
        private string currentScreen = "lobby";
        private int memberPageIndex;
        private int memberRoleFilterIndex;
        private int memberRaceFilterIndex;
        private bool memberOwnedOnly;
        private string memberSearchQuery = string.Empty;
        private int selectedAccessoryIndex = -1;
        private int lastViewportWidth;
        private int lastViewportHeight;
        private float memberResizeRefreshAt = -1f;
        private float toastHideAt;
        private int lastResourceRefreshSecond = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRuntime()
        {
            if (FindAnyObjectByType<ChoSirenApp>() != null) return;
            new GameObject("CHO-SIREN App").AddComponent<ChoSirenApp>();
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            model = new GameModel();
            model.Changed += UpdateTopBar;
            gameAudio = gameObject.AddComponent<GameAudio>();
            gameAudio.Initialize(model);
            font = Resources.Load<Font>("Fonts/NotoSansSC-Subset") ??
                   Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildShell();
            lastViewportWidth = Screen.width;
            lastViewportHeight = Screen.height;
            ShowScreen("lobby");
        }

        private void Update()
        {
            if (Input.GetMouseButtonDown(0) || Input.anyKeyDown ||
                (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began))
                ResumeMediaAfterUserGesture();

            if (toastObject != null && toastObject.activeSelf && Time.unscaledTime >= toastHideAt)
                toastObject.SetActive(false);

            if (Screen.width != lastViewportWidth || Screen.height != lastViewportHeight)
            {
                lastViewportWidth = Screen.width;
                lastViewportHeight = Screen.height;
                memberResizeRefreshAt = Time.unscaledTime + 0.12f;
            }

            // The canvas already adapts the open modal to viewport changes. Defer the
            // background grid rebuild until it closes; ShowScreen would discard the dossier.
            if (modalObject == null && (currentScreen == "members" || currentScreen == "team") && memberResizeRefreshAt >= 0f &&
                Time.unscaledTime >= memberResizeRefreshAt)
            {
                memberResizeRefreshAt = -1f;
                ShowScreen(currentScreen);
            }

            if (model == null || staminaText == null) return;
            int second = (int)Time.unscaledTime;
            if (second == lastResourceRefreshSecond) return;
            lastResourceRefreshSecond = second;
            model.RefreshDailyState();
            UpdateTopBar();
        }

        private void OnDestroy()
        {
            if (model != null) model.Changed -= UpdateTopBar;
            if (lobbyVideoPlayer != null) lobbyVideoPlayer.MusicAvailabilityChanged -= ApplyMusicRouting;
            DestroyRuntimeSprite(ref teamStellarStageSprite);
            DestroyRuntimeSprite(ref memberGalleryCalmSprite);
            DestroyRuntimeSprite(ref accessoryDressingRoomSprite);
            for (int index = 0; index < runtimeAiUiSprites.Count; index++)
                if (runtimeAiUiSprites[index] != null) Destroy(runtimeAiUiSprites[index]);
            runtimeAiUiSprites.Clear();
            aiUiSprites.Clear();
        }

        private void BuildShell()
        {
            EnsureEventSystem();

            GameObject canvasObject = NewObject("Canvas", transform);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720, 1536);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            // The approved web composition is a fixed-width portrait stage.  Matching
            // width preserves its coordinates on taller/shorter desktop windows.
            scaler.matchWidthOrHeight = 0f;
            canvasObject.AddComponent<GraphicRaycaster>();
            canvasObject.AddComponent<ButtonInteractionFeedbackInstaller>();

            GameObject background = NewImage("Background", canvasObject.transform, Resources.Load<Sprite>("Art/LobbyBackground"), White);
            Stretch(background.GetComponent<RectTransform>());
            background.GetComponent<Image>().preserveAspect = false;

            lobbyVideoObject = NewObject("LobbyVideoBackground", canvasObject.transform);
            RectTransform lobbyVideoRect = lobbyVideoObject.AddComponent<RectTransform>();
            Stretch(lobbyVideoRect);
            RawImage lobbyVideoSurface = lobbyVideoObject.AddComponent<RawImage>();
            lobbyVideoSurface.color = White;
            lobbyVideoSurface.raycastTarget = false;
            lobbyVideoPlayer = lobbyVideoObject.AddComponent<LobbyVideoLoopPlayer>();
            lobbyVideoPlayer.MusicAvailabilityChanged += ApplyMusicRouting;
            lobbyVideoObject.SetActive(false);

            GameObject tint = NewImage("Atmosphere", canvasObject.transform, null, new Color32(8, 6, 42, 65));
            Stretch(tint.GetComponent<RectTransform>());

            GameObject safe = NewObject("SafeArea", canvasObject.transform);
            RectTransform safeRect = safe.AddComponent<RectTransform>();
            Stretch(safeRect);
            safe.AddComponent<SafeAreaFitter>();
            safeRoot = safe.transform;

            BuildTopBar();

            GameObject content = NewObject("Content", safeRoot);
            contentRoot = content.AddComponent<RectTransform>();
            contentRoot.anchorMin = Vector2.zero;
            contentRoot.anchorMax = Vector2.one;
            contentRoot.offsetMin = new Vector2(0, 142);
            contentRoot.offsetMax = new Vector2(0, -104);

            GameObject nav = NewObject("BottomNavigation", safeRoot);
            navRoot = nav.AddComponent<RectTransform>();
            navRoot.anchorMin = new Vector2(0, 0);
            navRoot.anchorMax = new Vector2(1, 0);
            navRoot.pivot = new Vector2(0.5f, 0);
            navRoot.offsetMin = new Vector2(16, 12);
            navRoot.offsetMax = new Vector2(-16, 150);
            Image navBackground = nav.AddComponent<Image>();
            navBackground.sprite = RoundedSprite(26);
            navBackground.type = Image.Type.Sliced;
            navBackground.color = Color.clear;
            navBackground.raycastTarget = false;

            BuildToast();
            BuildStartupLoading();

            // Explicit render order: lobby art < HUD < navigation < transient UI.
            // Content used to be created after the HUD and could cover it when the
            // reference-sized hero extended into the top area.
            contentRoot.SetAsFirstSibling();
        }

        private void BuildTopBar()
        {
            GameObject bar = NewImage("TopBar", safeRoot, null, Color.clear);
            RectTransform barRect = bar.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0, 1);
            barRect.anchorMax = Vector2.one;
            barRect.pivot = new Vector2(0.5f, 1);
            barRect.offsetMin = new Vector2(0, -82);
            barRect.offsetMax = Vector2.zero;

            GameObject avatarFrame = NewImage("AvatarFrame", bar.transform, RoundedSprite(30),
                new Color32(222, 195, 255, 230));
            PlaceTop(avatarFrame.GetComponent<RectTransform>(), 22, 13, 56, 56);
            GameObject avatarMask = NewImage("AvatarMask", avatarFrame.transform, RoundedSprite(30), White);
            Stretch(avatarMask.GetComponent<RectTransform>(), 2, 2, -2, -2);
            Mask mask = avatarMask.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            GameObject avatar = NewImage("Avatar", avatarMask.transform,
                AiUiSprite("Art/ProfileAvatarUser") ?? Resources.Load<Sprite>("Art/ProfileAvatar"), White);
            Stretch(avatar.GetComponent<RectTransform>());
            avatar.GetComponent<Image>().preserveAspect = false;

            Text name = NewText("PlayerName", bar.transform, "音律少女", 19, White, FontStyle.Bold, TextAnchor.UpperLeft);
            PlaceTop(name.rectTransform, 86, 14, 104, 29);
            AddReadableShadow(name);
            teamLevelText = NewText("PlayerLevel", bar.transform, string.Empty, 14,
                new Color32(225, 215, 242, 255), FontStyle.Bold, TextAnchor.UpperLeft);
            PlaceTop(teamLevelText.rectTransform, 86, 43, 112, 22);
            teamLevelText.resizeTextForBestFit = true;
            teamLevelText.resizeTextMinSize = 10;
            teamLevelText.resizeTextMaxSize = 14;
            teamLevelText.horizontalOverflow = HorizontalWrapMode.Overflow;
            AddReadableShadow(teamLevelText);

            GameObject profileHit = NewButton("Profile", bar.transform, string.Empty, 1, Color.clear, Color.clear, OpenProfile);
            PlaceTop(profileHit.GetComponent<RectTransform>(), 16, 8, 190, 70);
            profileHit.transform.SetAsFirstSibling();

            // Each quiet pill is the action: no extra purchase symbols or competing glow.
            diamondText = BuildResourcePill(bar.transform, CurrencyIds.Diamond, "DiamondIcon", "Diamonds",
                "Art/UI/ResourceDiamond-C", 208, 120, Cyan);
            goldText = BuildResourcePill(bar.transform, CurrencyIds.Gold, "GoldIcon", "Gold",
                "Art/UI/ResourceGold-C", 338, 120, new Color32(255, 219, 126, 255));
            staminaText = BuildResourcePill(bar.transform, CurrencyIds.Stamina, "StaminaIcon", "Stamina",
                "Art/UI/ResourceStamina-C", 468, 126, new Color32(255, 151, 211, 255));

            AddSpriteIconButton(bar.transform, "Mail",
                Resources.Load<Sprite>("Art/UI/HudIcons/Mail"), 83, OpenInbox);
            AddSpriteIconButton(bar.transform, "Settings",
                Resources.Load<Sprite>("Art/UI/HudIcons/Settings"), 37, OpenSettings);
            UpdateTopBar();
        }

        private void BuildNavigation()
        {
            ClearChildren(navRoot);
            navHighlights.Clear();
            string[] ids = { "team", "members", "lobby", "accessory", "audition" };
            string[] labels = { "团队", "成员", "大厅", "饰品", "选秀" };

            for (int index = 0; index < ids.Length; index++)
            {
                int captured = index;
                bool selected = currentScreen == ids[index];
                GameObject buttonObject = NewImage($"Nav-{ids[index]}", navRoot, null, Color.clear);
                RectTransform rect = buttonObject.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(index / 5f, 0);
                rect.anchorMax = new Vector2((index + 1) / 5f, 1);
                rect.offsetMin = new Vector2(3, 4);
                rect.offsetMax = new Vector2(-3, -4);
                Image buttonGraphic = buttonObject.GetComponent<Image>();
                buttonGraphic.raycastTarget = true;
                Button button = buttonObject.AddComponent<Button>();
                button.targetGraphic = buttonGraphic;
                button.onClick.AddListener(() =>
                {
                    ShowScreen(ids[captured]);
                    ResumeMediaAfterUserGesture();
                });

                GameObject icon = NewImage("Icon", buttonObject.transform, NavIconSprite(index),
                    selected ? White : new Color32(197, 183, 218, 215));
                RectTransform iconRect = icon.GetComponent<RectTransform>();
                iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 1);
                iconRect.pivot = new Vector2(0.5f, 1);
                iconRect.anchoredPosition = new Vector2(0, -6);
                iconRect.sizeDelta = new Vector2(68, 66);
                Image iconImage = icon.GetComponent<Image>();
                iconImage.preserveAspect = true;
                iconImage.useSpriteMesh = true;

                Text label = NewText("Label", buttonObject.transform, labels[index], selected ? 17 : 16,
                    selected ? White : Muted, selected ? FontStyle.Bold : FontStyle.Normal,
                    TextAnchor.MiddleCenter);
                PlaceTopStretch(label.rectTransform, 76, 32);

                GameObject highlight = NewImage("Highlight", buttonObject.transform, null,
                    selected ? Pink : Color.clear);
                RectTransform highlightRect = highlight.GetComponent<RectTransform>();
                highlightRect.anchorMin = new Vector2(0.3f, 0);
                highlightRect.anchorMax = new Vector2(0.7f, 0);
                highlightRect.offsetMin = new Vector2(0, 2);
                highlightRect.offsetMax = new Vector2(0, 7);
                navHighlights.Add(highlight.GetComponent<Image>());
            }
        }

        private void ShowScreen(string screen)
        {
            model.RefreshDailyState();
            currentScreen = screen;
            if (screen != "lobby" && lobbyVideoObject != null)
            {
                lobbyVideoPlayer?.PauseLoop();
                lobbyVideoObject.SetActive(false);
            }
            ApplyMusicRouting();
            CloseModal();
            ClearChildren(contentRoot);
            BuildNavigation();

            switch (screen)
            {
                case "team": BuildTeam(); break;
                case "members": BuildMembers(); break;
                case "accessory": BuildAccessories(); break;
                case "audition": OpenGacha(); break;
                default: BuildLobby(); break;
            }
        }

        private void ApplyMusicRouting()
        {
            if (model == null) return;
            lobbyVideoPlayer?.SetMusicEnabled(model.Save.MusicEnabled);
            bool videoOwnsMusic = currentScreen == "lobby" && lobbyVideoPlayer != null &&
                                   lobbyVideoPlayer.CanProvideMusic;
            gameAudio?.SetLobbyVideoMusicActive(videoOwnsMusic);
            gameAudio?.ApplySettings();
        }

        private void ResumeMediaAfterUserGesture()
        {
            lobbyVideoPlayer?.ResumeAudioAfterUserGesture();
            ApplyMusicRouting();
            gameAudio?.ResumeAfterUserGesture();
        }

        private void SuspendLobbyMedia()
        {
            lobbyVideoPlayer?.PauseLoop();
            if (lobbyVideoObject != null) lobbyVideoObject.SetActive(false);
            gameAudio?.SetLobbyVideoMusicActive(false);
        }

        private void BuildLobby()
        {
            Text eyebrow = NewText("Eyebrow", contentRoot, "星途舞台", 15,
                new Color32(255, 177, 228, 255), FontStyle.Bold, TextAnchor.MiddleCenter);
            PlaceTop(eyebrow.rectTransform, 253, 173, 214, 20);

            GameObject heroLayer = NewObject("HeroLayer", contentRoot);
            Stretch(heroLayer.AddComponent<RectTransform>());
            GameObject cardLayer = NewObject("LobbyCards", contentRoot);
            Stretch(cardLayer.AddComponent<RectTransform>());

            GameObject loadingBadge = NewPanel("HeroLoading", heroLayer.transform,
                new Color32(20, 18, 65, 220), 15);
            PlaceTop(loadingBadge.GetComponent<RectTransform>(), 266, 936, 188, 34);
            loadingBadge.GetComponent<Image>().raycastTarget = false;
            Text loadingText = NewText("Status", loadingBadge.transform, "舞台资源载入中 · 0%", 12,
                new Color32(232, 217, 250, 255), FontStyle.Bold, TextAnchor.MiddleCenter);
            Stretch(loadingText.rectTransform, 8, 2, -8, -2);
            // The stage itself is the menu. Each hotspot uses a complete transparent AI-rendered
            // holographic device; code only supplies localized labels and interaction.
            LobbyHotspot(cardLayer.transform, "闪耀舞台", 1, 8, 326, 270, 238, OpenActivity);
            LobbyHotspot(cardLayer.transform, "任务", 3, 446, 520, 274, 242, OpenDailyTasks);
            BuildStageCallToAction(cardLayer.transform);
            cardLayer.transform.SetAsLastSibling();
            UiEntranceMotion cardEntrance = cardLayer.AddComponent<UiEntranceMotion>();
            lobbyVideoPlayer?.SetMusicEnabled(model.Save.MusicEnabled);
            gameAudio?.SetLobbyVideoMusicActive(false);
            lobbyVideoPlayer?.StartLoop(() =>
            {
                ApplyMusicRouting();
                if (loadingText != null) loadingText.text = "舞台资源载入中 · 100%";
                UpdateStartupLoading(1f);
                if (loadingBadge != null) loadingBadge.SetActive(false);
                FinishStartupLoading();
                if (cardEntrance != null) cardEntrance.Play();
            }, error =>
            {
                gameAudio?.SetLobbyVideoMusicActive(false);
                if (loadingBadge != null) loadingBadge.SetActive(false);
                FinishStartupLoading();
                if (cardEntrance != null) cardEntrance.Play();
                Debug.LogWarning($"CHO-SIREN lobby video fallback: {error}");
            });
        }

        private void BuildTeam()
        {
            BuildTeamStellarBackdrop();
            GameObject titlePlaque = NewPanel("TeamTitlePlaque", contentRoot, new Color32(12, 18, 42, 236), 20);
            PlaceTop(titlePlaque.GetComponent<RectTransform>(), 20, 12, 456, 134);
            AddQuietPanelEdge(titlePlaque);
            NewPlacedText(titlePlaque.transform, "当前编队", 13, new Color32(255, 174, 225, 255),
                20, 10, 408, 22, TextAnchor.MiddleLeft, FontStyle.Bold).name = "TeamTitleEyebrow";
            Text formationTitle = NewPlacedText(titlePlaque.transform, "星环编队", 28, White,
                20, 32, 408, 48, TextAnchor.MiddleLeft, FontStyle.Bold);
            formationTitle.name = "TeamTitleName";
            formationTitle.verticalOverflow = VerticalWrapMode.Overflow;
            NewPlacedText(titlePlaque.transform, "点击角色培养 / 装备 / 换人", 14, Muted,
                20, 82, 408, 22, TextAnchor.MiddleLeft).name = "TeamTitleHint";
            Text teamIndexLabel = NewPlacedText(titlePlaque.transform, "编队 1", 12,
                new Color32(185, 222, 255, 255),
                20, 105, 408, 20, TextAnchor.MiddleLeft, FontStyle.Bold);
            teamIndexLabel.name = "TeamIndexLabel";

            int teamCount = Mathf.Min(GameModel.TeamCapacity, model.Save.Team.Count);
            int roleCount = model.Save.Team.Take(teamCount)
                .Where(index => index >= 0 && index < GameModel.Members.Length)
                .Select(index => GameModel.Members[index].Role)
                .Distinct()
                .Count();

            GameObject powerCard = NewPanel("TeamPower", contentRoot, new Color32(12, 18, 42, 236), 20);
            PlaceTop(powerCard.GetComponent<RectTransform>(), 492, 12, 208, 134);
            AddQuietPanelEdge(powerCard);
            NewPlacedText(powerCard.transform, "总战力", 13, Muted, 16, 10, 176, 20, TextAnchor.MiddleLeft).name = "TeamPowerHeading";
            Text teamPower = NewPlacedText(powerCard.transform, model.TeamPower.ToString("N0"), 30, White,
                16, 33, 176, 43, TextAnchor.MiddleLeft, FontStyle.Bold);
            teamPower.resizeTextForBestFit = true;
            teamPower.resizeTextMinSize = 20;
            teamPower.resizeTextMaxSize = 30;
            teamPower.horizontalOverflow = HorizontalWrapMode.Wrap;
            teamPower.verticalOverflow = VerticalWrapMode.Truncate;
            teamPower.name = "TeamPowerValue";
            Text resonanceText = NewPlacedText(powerCard.transform, $"职业种类  {roleCount}/4", 14,
                new Color32(112, 242, 255, 255), 16, 77, 176, 22, TextAnchor.MiddleLeft, FontStyle.Bold);
            resonanceText.name = "TeamResonanceValue";
            PanelKit.EnableBestFit(resonanceText, 11);
            Text memberCountText = NewPlacedText(powerCard.transform,
                $"成员  {teamCount}/{GameModel.TeamCapacity}", 12, Muted,
                16, 103, 176, 20, TextAnchor.MiddleLeft);
            memberCountText.name = "TeamMemberCount";
            PanelKit.EnableBestFit(memberCountText, 10);

            Canvas.ForceUpdateCanvases();
            float contentHeight = Mathf.Max(1f, contentRoot.rect.height);
            const float buttonHeight = 48f;
            const float synergyHeight = 88f;
            float buttonY = Mathf.Max(0f, contentHeight - buttonHeight - 8f);
            float synergyY = Mathf.Max(0f, buttonY - synergyHeight - 10f);
            float formationTop = 146f;
            float formationHeight = Mathf.Max(1f, synergyY - formationTop - 8f);
            Vector2[] orbitPositions =
            {
                new Vector2(220f, formationTop + formationHeight * 0.04f),
                new Vector2(22f, formationTop + formationHeight * 0.34f),
                new Vector2(448f, formationTop + formationHeight * 0.34f),
                new Vector2(224f, formationTop + formationHeight * 0.62f),
            };
            Vector2[] orbitSizes =
            {
                new Vector2(280f, Mathf.Min(390f, formationHeight * 0.48f)),
                new Vector2(250f, Mathf.Min(350f, formationHeight * 0.41f)),
                new Vector2(250f, Mathf.Min(350f, formationHeight * 0.41f)),
                new Vector2(272f, Mathf.Min(340f, formationHeight * 0.38f)),
            };

            for (int slot = 0; slot < GameModel.TeamCapacity; slot++)
            {
                int memberIndex = slot < teamCount ? model.Save.Team[slot] : -1;
                TeamOrbitSlot(slot, memberIndex, orbitPositions[slot], orbitSizes[slot], slot == 0);
            }

            GameObject synergyBar = NewPanel("TeamSynergy", contentRoot, new Color32(12, 18, 42, 236), 20);
            PlaceTop(synergyBar.GetComponent<RectTransform>(), 20, synergyY, 680, synergyHeight);
            AddQuietPanelEdge(synergyBar);
            NewPlacedText(synergyBar.transform, "职业配置", 15, new Color32(255, 184, 232, 255),
                18, 9, 128, 24, TextAnchor.MiddleLeft, FontStyle.Bold);
            for (int careerIndex = 0; careerIndex < MemberCareers.All.Count; careerIndex++)
            {
                string career = MemberCareers.All[careerIndex];
                int count = model.Save.Team.Count(index => GameModel.Members[index].Career == career);
                Text status = NewPlacedText(synergyBar.transform,
                    $"{career} · {count}人",
                    14, count > 0 ? White : Muted, 18 + careerIndex * 163, 43, 152, 28,
                    TextAnchor.MiddleLeft, FontStyle.Bold);
                status.name = "CareerStatus-" + careerIndex;
            }
            NewPlacedText(synergyBar.transform, "职业自由搭配", 12, new Color32(132, 222, 255, 255),
                448, 11, 212, 22, TextAnchor.MiddleRight, FontStyle.Bold).name = "TeamAttributes";

            GameObject leader = NewButton("ChangeLeader", contentRoot, "更换队长", 16,
                new Color32(22, 35, 82, 178), White, () =>
                {
                    OpenCaptainPicker();
            });
            PlaceTop(leader.GetComponent<RectTransform>(), 174, buttonY, 178, buttonHeight);
            if (!ApplyAiUiSprite(leader, "Art/TeamAI/UI/team-action-cyan-ai-v2"))
            {
                Outline leaderEdge = leader.AddComponent<Outline>();
                leaderEdge.effectColor = new Color32(101, 211, 255, 150);
                leaderEdge.effectDistance = new Vector2(1f, -1f);
            }
            GameObject swapIcon = NewAiDecoration("TeamSwapIcon", leader.transform,
                "Art/TeamAI/UI/team-swap-ai-v2");
            PlaceTop(swapIcon.GetComponent<RectTransform>(), 12, 9, 30, 30);
            RectTransform leaderLabel = leader.transform.Find("Label")?.GetComponent<RectTransform>();
            if (leaderLabel != null) Stretch(leaderLabel, 36, 4, -8, -4);

            GameObject auto = NewButton("AutoTeam", contentRoot, "一键编队", 16,
                new Color32(74, 45, 132, 194), White, () =>
            {
                model.AutoTeam();
                Toast("已优先兼顾职业并补齐阵容");
                ShowScreen("team");
            });
            PlaceTop(auto.GetComponent<RectTransform>(), 368, buttonY, 178, buttonHeight);
            if (!ApplyAiUiSprite(auto, "Art/TeamAI/UI/team-action-pink-ai-v2"))
            {
                Outline autoEdge = auto.AddComponent<Outline>();
                autoEdge.effectColor = new Color32(255, 185, 238, 168);
                autoEdge.effectDistance = new Vector2(1f, -1f);
            }
        }

        private void BuildTeamStellarBackdrop()
        {
            Sprite backdrop = TeamStellarStageSprite();
            GameObject stage = NewImage("TeamStellarBackground", contentRoot, backdrop, Color.white);
            Stretch(stage.GetComponent<RectTransform>());
            Image stageImage = stage.GetComponent<Image>();
            stageImage.raycastTarget = false;
            stageImage.preserveAspect = false;
            if (backdrop != null)
            {
                AspectRatioFitter fitter = stage.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = backdrop.rect.width / Mathf.Max(1f, backdrop.rect.height);
            }

            GameObject veil = NewImage("TeamStellarVeil", contentRoot, null, new Color32(2, 6, 28, 28));
            Stretch(veil.GetComponent<RectTransform>());
            veil.GetComponent<Image>().raycastTarget = false;
        }

        private Sprite TeamStellarStageSprite()
        {
            if (teamStellarStageSprite != null) return teamStellarStageSprite;
            const string path = "Art/TeamAI/team-stellar-stage-bg-ai-v1-20260903";
            teamStellarStageSprite = Resources.Load<Sprite>(path);
            if (teamStellarStageSprite != null) return teamStellarStageSprite;

            Texture2D texture = Resources.Load<Texture2D>(path);
            if (texture == null) return null;
            teamStellarStageSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            teamStellarStageSprite.name = "Team-Stellar-Stage-AI";
            teamStellarStageSprite.hideFlags = HideFlags.DontSave;
            return teamStellarStageSprite;
        }

        private void TeamOrbitSlot(int slot, int memberIndex, Vector2 position, Vector2 size, bool isLeader)
        {
            bool hasMember = memberIndex >= 0 && memberIndex < GameModel.Members.Length;
            GameObject orbit = NewPanel($"TeamOrbit-{slot}", contentRoot, new Color32(8, 15, 50, 8), 28);
            PlaceTop(orbit.GetComponent<RectTransform>(), position.x, position.y, size.x, size.y);
            Image orbitImage = orbit.GetComponent<Image>();
            orbitImage.raycastTarget = true;
            Button orbitButton = orbit.AddComponent<Button>();
            orbitButton.targetGraphic = orbitImage;
            orbitButton.onClick.AddListener(() =>
            {
                if (hasMember) OpenTeamMember(memberIndex, slot);
                else OpenTeamSlotPicker(Math.Min(slot, model.Save.Team.Count));
                ResumeMediaAfterUserGesture();
            });

            if (!hasMember)
            {
                NewPlacedText(orbit.transform, "+", 42, new Color32(140, 224, 255, 210),
                    0, size.y * 0.38f, size.x, 56, TextAnchor.MiddleCenter, FontStyle.Bold);
                NewPlacedText(orbit.transform, "添加成员", 14, Muted,
                    0, size.y - 64f, size.x, 28, TextAnchor.MiddleCenter, FontStyle.Bold);
                return;
            }

            MemberDefinition member = GameModel.Members[memberIndex];
            GameObject character = NewImage($"TeamCharacter-{slot}", orbit.transform,
                Resources.Load<Sprite>(member.ResourcePath), White);
            PlaceTop(character.GetComponent<RectTransform>(), 0, 0, size.x, size.y - 50f);
            Image characterImage = character.GetComponent<Image>();
            characterImage.preserveAspect = true;
            characterImage.useSpriteMesh = true;

            if (isLeader)
            {
                Text leader = NewPlacedText(orbit.transform, "队长", 13, new Color32(255, 218, 113, 255),
                    size.x * 0.5f - 40f, 5f, 80f, 26f, TextAnchor.MiddleCenter, FontStyle.Bold);
                leader.name = "TeamLeader";
            }

            GameObject tag = NewPanel($"TeamLabel-{slot}", orbit.transform, new Color32(6, 13, 44, 132), 14);
            PlaceTop(tag.GetComponent<RectTransform>(), 8, size.y - 69f, size.x - 16f, 66f);
            NewPlacedText(tag.transform, member.Name, isLeader ? 20 : 18, White,
                11, 4, size.x - 38f, 26, TextAnchor.MiddleLeft, FontStyle.Bold);
            NewPlacedText(tag.transform, $"{MemberRace(member, memberIndex)} · {member.Career}  等级 {model.LevelOf(memberIndex)}",
                11, new Color32(255, 160, 222, 255), 11, 28, size.x - 38f, 18, TextAnchor.MiddleLeft, FontStyle.Bold);
            NewPlacedText(tag.transform, $"战力 {model.PowerOf(memberIndex):N0}", 11,
                new Color32(102, 221, 255, 255), 11, 46, size.x - 38f, 17, TextAnchor.MiddleLeft, FontStyle.Bold);
        }

        private void BuildMembers()
        {
            BuildMemberGalleryBackdrop();
            string[] roleFilters = new[] { string.Empty }.Concat(MemberCareers.All).ToArray();
            string[] raceFilters = { string.Empty, "魅族", "魔族", "海灵族", "血精灵" };
            memberRoleFilterIndex = Mathf.Clamp(memberRoleFilterIndex, 0, roleFilters.Length - 1);
            memberRaceFilterIndex = Mathf.Clamp(memberRaceFilterIndex, 0, raceFilters.Length - 1);
            string roleFilter = roleFilters[memberRoleFilterIndex];
            string raceFilter = raceFilters[memberRaceFilterIndex];

            Canvas.ForceUpdateCanvases();
            float contentHeight = Mathf.Max(1f, contentRoot.rect.height);
            int visibleRows = contentHeight >= 938f
                ? MemberRosterPagination.RowsForContentHeight(contentHeight)
                : Mathf.Max(1, Mathf.FloorToInt((contentHeight - 278f) / 220f));
            int dynamicPageSize = MemberRosterPagination.DefaultColumns * visibleRows;

            MemberRosterPage page = MemberRosterPagination.Build(GameModel.Members.Length, memberPageIndex, index =>
            {
                MemberDefinition member = GameModel.Members[index];
                // 未获得成员是匿名占位：真实姓名/职业/种族不参与搜索与筛选，
                // 只有在没有任何身份筛选时才以剪影计数出现。
                return MemberRosterVisibility.MatchesRosterFilter(
                    model.IsUnlocked(index), member.Name, member.Career, MemberRaceFamily(member, index),
                    memberOwnedOnly, roleFilter, raceFilter, memberSearchQuery);
            }, dynamicPageSize, index => model.IsUnlocked(index) ? 0 : 1);
            memberPageIndex = page.PageIndex;
            int visiblePageNumber = page.PageCount == 0 ? 0 : page.PageIndex + 1;
            string rosterSubtitle =
                $"已拥有 {model.Save.UnlockedMembers.Count}/{GameModel.Members.Length} · 本页 {page.VisibleCount} 名";
            if (MemberRosterVisibility.HasIdentityFilter(roleFilter, raceFilter, memberSearchQuery))
            {
                rosterSubtitle += " · " + MemberRosterVisibility.LockedFilterNotice(
                    MemberRosterVisibility.RemainingCount(model.Save.UnlockedMembers.Count,
                        GameModel.Members.Length));
            }
            ScreenTitle("成员档案", "全部成员", rosterSubtitle);

            MemberFilterButton("MemberRoleFilter", $"职业：{(string.IsNullOrEmpty(roleFilter) ? "全部" : roleFilter)}",
                20, 108, 196, () =>
                {
                    memberRoleFilterIndex = (memberRoleFilterIndex + 1) % roleFilters.Length;
                    memberPageIndex = 0;
                    ShowScreen("members");
                });
            MemberFilterButton("MemberRaceFilter", $"种族：{(string.IsNullOrEmpty(raceFilter) ? "全部" : raceFilter)}",
                226, 108, 196, () =>
                {
                    memberRaceFilterIndex = (memberRaceFilterIndex + 1) % raceFilters.Length;
                    memberPageIndex = 0;
                    ShowScreen("members");
                });
            MemberFilterButton("MemberOwnedFilter", memberOwnedOnly ? "只看已拥有：开" : "只看已拥有：关",
                432, 108, 268, () =>
                {
                    memberOwnedOnly = !memberOwnedOnly;
                    memberPageIndex = 0;
                    ShowScreen("members");
                }, memberOwnedOnly);
            BuildMemberSearchBox();

            const int cardWidth = 128;
            const int cardHeight = 210;
            for (int slot = 0; slot < page.VisibleCount; slot++)
            {
                int memberIndex = page.SourceIndexAt(slot);
                MemberRosterCell cell = MemberRosterPagination.CellFor(slot);
                int x = 20 + cell.Column * 137;
                int y = 218 + cell.Row * 220;
                MemberGridCard(memberIndex, x, y, cardWidth, cardHeight);
            }

            if (page.IsEmpty)
            {
                NewPlacedText(contentRoot, "没有符合条件的成员\n请调整筛选或搜索内容", 20, Muted,
                    80, 520, 560, 100, TextAnchor.MiddleCenter, FontStyle.Bold);
            }

            float pagerY = contentHeight - 58f;
            GameObject previous = NewButton("MemberPreviousPage", contentRoot, "上一页", 16,
                page.HasPrevious ? new Color32(111, 66, 181, 138) : new Color32(46, 44, 79, 92), White, () =>
                {
                    memberPageIndex = MemberRosterPagination.MovePage(memberPageIndex, -1, page.PageCount);
                    ShowScreen("members");
                });
            PlaceTop(previous.GetComponent<RectTransform>(), 164, pagerY, 150, 50);
            previous.GetComponent<Button>().interactable = page.HasPrevious;

            NewPlacedText(contentRoot, $"{visiblePageNumber} / {page.PageCount}", 17, White,
                315, pagerY, 90, 50, TextAnchor.MiddleCenter, FontStyle.Bold);

            GameObject next = NewButton("MemberNextPage", contentRoot, "下一页", 16,
                page.HasNext ? new Color32(111, 66, 181, 138) : new Color32(46, 44, 79, 92), White, () =>
                {
                    memberPageIndex = MemberRosterPagination.MovePage(memberPageIndex, 1, page.PageCount);
                    ShowScreen("members");
                });
            PlaceTop(next.GetComponent<RectTransform>(), 406, pagerY, 150, 50);
            next.GetComponent<Button>().interactable = page.HasNext;
        }

        private void BuildMemberGalleryBackdrop()
        {
            Sprite backdrop = MemberGalleryCalmSprite();
            GameObject stage = NewImage("MemberGalleryBackground", contentRoot, backdrop, Color.white);
            Stretch(stage.GetComponent<RectTransform>());
            Image stageImage = stage.GetComponent<Image>();
            stageImage.raycastTarget = false;
            stageImage.preserveAspect = false;
            if (backdrop != null)
            {
                AspectRatioFitter fitter = stage.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = backdrop.rect.width / Mathf.Max(1f, backdrop.rect.height);
            }

            GameObject veil = NewImage("MemberGalleryVeil", contentRoot, null, new Color32(3, 8, 31, 34));
            Stretch(veil.GetComponent<RectTransform>());
            veil.GetComponent<Image>().raycastTarget = false;
        }

        private Sprite MemberGalleryCalmSprite()
        {
            if (memberGalleryCalmSprite != null) return memberGalleryCalmSprite;
            const string path = "Art/MemberAI/member-gallery-calm-bg-ai-v1-20260903";
            memberGalleryCalmSprite = Resources.Load<Sprite>(path);
            if (memberGalleryCalmSprite != null) return memberGalleryCalmSprite;

            Texture2D texture = Resources.Load<Texture2D>(path);
            if (texture == null) return null;
            memberGalleryCalmSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            memberGalleryCalmSprite.name = "Member-Gallery-Calm-AI";
            memberGalleryCalmSprite.hideFlags = HideFlags.DontSave;
            return memberGalleryCalmSprite;
        }

        private void MemberFilterButton(string name, string label, int x, int y, int width,
            UnityEngine.Events.UnityAction action, bool selected = false)
        {
            GameObject button = NewButton(name, contentRoot, label, 14,
                selected ? new Color32(133, 72, 194, 142) : new Color32(20, 22, 68, 92),
                selected ? White : Muted, action);
            PlaceTop(button.GetComponent<RectTransform>(), x, y, width, 44);
            Outline outline = button.AddComponent<Outline>();
            outline.effectColor = selected ? new Color32(255, 109, 212, 210) : new Color32(113, 174, 255, 100);
            outline.effectDistance = new Vector2(1f, -1f);
        }

        private void BuildMemberSearchBox()
        {
            GameObject box = NewPanel("MemberSearch", contentRoot, new Color32(12, 17, 58, 88), 16);
            PlaceTop(box.GetComponent<RectTransform>(), 20, 162, 680, 44);
            Outline outline = box.AddComponent<Outline>();
            outline.effectColor = new Color32(113, 174, 255, 110);
            outline.effectDistance = new Vector2(1f, -1f);

            Text placeholder = NewText("Placeholder", box.transform, "搜索成员名称，输入后按回车", 14, Muted,
                FontStyle.Normal, TextAnchor.MiddleLeft);
            Stretch(placeholder.rectTransform, 18, 4, -18, -4);
            Text value = NewText("Value", box.transform, memberSearchQuery, 15, White,
                FontStyle.Normal, TextAnchor.MiddleLeft);
            Stretch(value.rectTransform, 18, 4, -18, -4);

            InputField input = box.AddComponent<InputField>();
            input.targetGraphic = box.GetComponent<Image>();
            input.textComponent = value;
            input.placeholder = placeholder;
            input.lineType = InputField.LineType.SingleLine;
            input.characterLimit = 12;
            input.text = memberSearchQuery;
            input.onEndEdit.AddListener(query =>
            {
                string normalized = (query ?? string.Empty).Trim();
                if (normalized == memberSearchQuery) return;
                memberSearchQuery = normalized;
                memberPageIndex = 0;
                ShowScreen("members");
            });
        }

        private void MemberGridCard(int index, int x, int y, int width, int height)
        {
            MemberDefinition member = GameModel.Members[index];
            bool unlocked = model.IsUnlocked(index);
            Color cardColor = unlocked ? new Color32(25, 24, 78, 78) : new Color32(12, 17, 49, 46);
            GameObject card = NewPanel($"Member-{member.Id}", contentRoot, cardColor, 18);
            PlaceTop(card.GetComponent<RectTransform>(), x, y, width, height);
            Button button = card.AddComponent<Button>();
            button.targetGraphic = card.GetComponent<Image>();
            button.onClick.AddListener(() =>
            {
                // 未签约成员也属于可浏览的图鉴内容；拥有状态只限制培养与编队操作。
                OpenMember(index);
                ResumeMediaAfterUserGesture();
            });

            // 未获得角色只给剪影与剩余进度：不加载真实形象，也不出现姓名/职业/种族/等级。
            if (!MemberRosterVisibility.ShowsRealPortrait(unlocked))
            {
                BuildLockedMemberCard(card, width, height);
                return;
            }

            Color glowColor = member.Career == "主唱"
                ? new Color32(80, 224, 255, unlocked ? (byte)76 : (byte)24)
                : member.Career == "主舞"
                    ? new Color32(181, 111, 255, unlocked ? (byte)70 : (byte)22)
                    : new Color32(255, 119, 202, unlocked ? (byte)66 : (byte)20);
            GameObject glow = NewImage("CareerGlow", card.transform, StageGlowSprite(), glowColor);
            PlaceTop(glow.GetComponent<RectTransform>(), 3, 3, width - 6, 148);

            GameObject portrait = NewImage("Portrait", card.transform,
                Resources.Load<Sprite>(member.ThumbnailResourcePath),
                unlocked ? White : new Color(0.68f, 0.68f, 0.78f, 0.72f));
            PlaceTop(portrait.GetComponent<RectTransform>(), 4, 4, width - 8, 145);
            portrait.GetComponent<Image>().preserveAspect = true;
            NewPlacedText(card.transform, $"{MemberRaceFamily(member, index)} · {member.Career}", 11, unlocked ? Pink : Muted,
                7, 140, width - 14, 20, TextAnchor.MiddleLeft, FontStyle.Bold);
            NewPlacedText(card.transform, member.Name, 17, unlocked ? White : new Color32(222, 215, 238, 255),
                7, 160, width - 14, 27, TextAnchor.MiddleLeft, FontStyle.Bold);
            NewPlacedText(card.transform, unlocked ? $"等级 {model.LevelOf(index)}" : "未签约",
                12, unlocked ? Cyan : Muted, 7, 185, width - 14, 20, TextAnchor.MiddleLeft, FontStyle.Bold);

            if (unlocked)
            {
                bool deployed = model.IsInTeam(index);
                GameObject badge = NewPanel("DeploymentBadge", card.transform,
                    deployed ? new Color32(12, 66, 76, 245) : new Color32(25, 30, 52, 235), 8);
                PlaceTop(badge.GetComponent<RectTransform>(), 5, 5, width - 10, 24);
                badge.GetComponent<Image>().raycastTarget = false;
                Text label = NewPlacedText(badge.transform, MemberDeploymentLabel(index), 12,
                    model.Save.Team.IndexOf(index) == 0 ? new Color32(255, 215, 112, 255) : deployed ? Cyan : Muted,
                    0, 0, width - 10, 24, TextAnchor.MiddleCenter, FontStyle.Bold);
                label.name = "DeploymentLabel";
                label.raycastTarget = false;
            }

            Outline edge = card.AddComponent<Outline>();
            edge.effectColor = unlocked ? new Color32(120, 190, 255, 112) : new Color32(95, 111, 165, 58);
            edge.effectDistance = new Vector2(1f, -1f);
        }

        private void BuildAudition()
        {
            ScreenTitle("签约中心", "女团选秀", $"每次签约消耗 ◇{GameModel.RecruitCost:N0}");
            List<int> candidates = Enumerable.Range(0, GameModel.Members.Length)
                .Where(index => !model.IsUnlocked(index))
                .Take(3)
                .ToList();

            if (candidates.Count == 0)
            {
                GameObject complete = NewPanel("AuditionComplete", contentRoot, Glass, 24);
                PlaceTop(complete.GetComponent<RectTransform>(), 60, 250, 600, 360);
                NewPlacedText(complete.transform, "全员签约完成", 32, White, 30, 70, 540, 60,
                    TextAnchor.MiddleCenter, FontStyle.Bold);
                NewPlacedText(complete.transform, "所有候选人都已加入成员列表。\n去训练和编队，打造你的顶流女团。", 18, Muted,
                    50, 150, 500, 90, TextAnchor.MiddleCenter);
                GameObject goMembers = NewButton("GoMembers", complete.transform, "查看全部成员", 18, Pink, White,
                    () => ShowScreen("members"));
                PlaceTop(goMembers.GetComponent<RectTransform>(), 160, 270, 280, 58);
                return;
            }

            for (int slot = 0; slot < candidates.Count; slot++)
            {
                int memberIndex = candidates[slot];
                AuditionCard(memberIndex, 20 + slot * 226, 130);
            }

            GameObject info = NewPanel("AuditionInfo", contentRoot, new Color32(32, 23, 76, 225), 20);
            PlaceTop(info.GetComponent<RectTransform>(), 20, 600, 680, 170);
            NewPlacedText(info.transform, "星探评估", 20, Pink, 22, 18, 200, 30, TextAnchor.MiddleLeft, FontStyle.Bold);
            NewPlacedText(info.transform,
                "签约后成员将永久加入账号，可参与训练、编队与舞台演出。角色数据会保存在本机。",
                16, White, 22, 58, 636, 70, TextAnchor.UpperLeft);
            NewPlacedText(info.transform, "提示：先看定位与战力，再决定本期资源投向。", 14, Muted,
                22, 130, 630, 24, TextAnchor.MiddleLeft);
        }

        private void AuditionCard(int memberIndex, int x, int y)
        {
            MemberDefinition member = GameModel.Members[memberIndex];
            GameObject card = NewPanel($"Candidate-{member.Id}", contentRoot, GlassLight, 20);
            PlaceTop(card.GetComponent<RectTransform>(), x, y, 214, 430);
            GameObject portrait = NewImage("Portrait", card.transform, Resources.Load<Sprite>(member.ResourcePath), White);
            PlaceTop(portrait.GetComponent<RectTransform>(), 6, 6, 202, 245);
            portrait.GetComponent<Image>().preserveAspect = true;
            NewPlacedText(card.transform, member.Name, 25, White, 14, 244, 186, 36, TextAnchor.MiddleLeft, FontStyle.Bold);
            NewPlacedText(card.transform, $"{MemberRaceFamily(member, memberIndex)} · {member.Career}", 14, Pink,
                14, 282, 186, 25, TextAnchor.MiddleLeft);
            NewPlacedText(card.transform, $"入队战力\n{model.PowerOf(memberIndex):N0}", 15, Cyan,
                14, 314, 186, 52, TextAnchor.MiddleLeft, FontStyle.Bold);
            GameObject recruit = NewButton("Recruit", card.transform, $"签约 ◇{GameModel.RecruitCost}", 16, Pink, White, () =>
            {
                model.Recruit(memberIndex, out string message);
                Toast(message);
                ShowScreen("audition");
            });
            PlaceTop(recruit.GetComponent<RectTransform>(), 14, 372, 186, 46);
        }

        private void BuildAccessories()
        {
            BuildPersonalEquipment();
        }

        private void BuildAccessoryStageBackdrop()
        {
            Sprite backdrop = AccessoryDressingRoomSprite();
            GameObject stage = NewImage("AccessoryDressingRoomStage", contentRoot, backdrop, Color.white);
            Stretch(stage.GetComponent<RectTransform>());
            Image stageImage = stage.GetComponent<Image>();
            stageImage.preserveAspect = false;
            stageImage.raycastTarget = false;
            if (backdrop != null)
            {
                AspectRatioFitter fitter = stage.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = backdrop.rect.width / Mathf.Max(1f, backdrop.rect.height);
            }

            GameObject veil = NewImage("AccessoryDressingRoomVeil", contentRoot, null,
                new Color32(3, 6, 29, 92));
            Stretch(veil.GetComponent<RectTransform>());
            veil.GetComponent<Image>().raycastTarget = false;
        }

        private Sprite AccessoryDressingRoomSprite()
        {
            if (accessoryDressingRoomSprite != null) return accessoryDressingRoomSprite;
            const string path = "Art/AccessoryAI/accessory-calm-bg-ai-v2-20260903";
            accessoryDressingRoomSprite = Resources.Load<Sprite>(path);
            if (accessoryDressingRoomSprite != null) return accessoryDressingRoomSprite;

            Texture2D texture = Resources.Load<Texture2D>(path);
            if (texture == null) return null;
            accessoryDressingRoomSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            accessoryDressingRoomSprite.name = "Accessory-Calm-Stage-AI";
            accessoryDressingRoomSprite.hideFlags = HideFlags.DontSave;
            return accessoryDressingRoomSprite;
        }

        private void BuildAccessoryPreview(int selected)
        {
            // A calm veil prevents the decorative rings baked into older backgrounds from reading as
            // additional equipment slots. The six interactive rings below are now the single source of truth.
            GameObject preview = NewPanel("AccessoryPreview", contentRoot, new Color32(4, 11, 39, 28), 28);
            PlaceTop(preview.GetComponent<RectTransform>(), 12, 108, 462, 704);
            Outline previewEdge = preview.AddComponent<Outline>();
            previewEdge.effectColor = new Color32(112, 208, 255, 42);
            previewEdge.effectDistance = new Vector2(1f, -1f);

            GameObject previewArt = NewAiDecoration("AccessoryPreviewArt", preview.transform,
                "Art/AccessoryAI/UI/accessory-preview-panel-ai-v1");
            PlaceTop(previewArt.GetComponent<RectTransform>(), 45, 4, 360, 700);

            NewPlacedText(preview.transform, "舞台搭配 · 编队共用", 15, new Color32(255, 181, 230, 255),
                132, 18, 186, 30, TextAnchor.MiddleCenter, FontStyle.Bold);

            GameObject character = NewImage("AccessoryPreviewCharacter", preview.transform,
                Resources.Load<Sprite>("Art/Members/member-feiyin") ?? Resources.Load<Sprite>("Art/HeroFallback"), White);
            PlaceTop(character.GetComponent<RectTransform>(), 74, 90, 302, 548);
            Image characterImage = character.GetComponent<Image>();
            characterImage.preserveAspect = true;
            characterImage.useSpriteMesh = true;

            string[] slotNames = { "耳返", "心链", "舞鞋", "挂饰", "手环", "冠冕" };
            Vector2[] slotPositions =
            {
                new Vector2(52, 92), new Vector2(298, 92),
                new Vector2(52, 296), new Vector2(298, 296),
                new Vector2(52, 502), new Vector2(298, 502),
            };
            for (int index = 0; index < slotPositions.Length; index++)
            {
                int captured = index;
                bool selectable = index < GameModel.AccessoryNames.Length;
                bool active = selectable && index == selected;
                bool equipped = selectable && model.Save.EquippedAccessory == index;
                GameObject slot = NewButton(selectable ? $"Accessory-{index}" : $"AccessorySlot-{index}",
                    preview.transform, string.Empty, 1,
                    active ? new Color32(86, 43, 139, 158) : new Color32(10, 20, 60, 34), White,
                    () =>
                    {
                        if (!selectable)
                        {
                            Toast("该饰品将在后续舞台活动中开放");
                            return;
                        }
                        selectedAccessoryIndex = captured;
                        ShowScreen("accessory");
                    });
                PlaceTop(slot.GetComponent<RectTransform>(), slotPositions[index].x, slotPositions[index].y, 100, 112);
                bool hasAiRing = ApplyAiUiSprite(slot, "Art/AccessoryAI/UI/accessory-slot-ring-ai-v1", true);
                Image slotImage = slot.GetComponent<Image>();
                if (hasAiRing)
                {
                    slotImage.color = active
                        ? White
                        : (selectable ? new Color32(218, 230, 255, 235) : new Color32(124, 133, 178, 155));
                }
                else
                {
                    Outline slotEdge = slot.AddComponent<Outline>();
                    slotEdge.effectColor = active
                        ? new Color32(255, 95, 201, 245)
                        : new Color32(96, 211, 255, 132);
                    slotEdge.effectDistance = active ? new Vector2(2f, -2f) : new Vector2(1f, -1f);
                }

                GameObject art = NewImage("Art", slot.transform, AccessoryItemSprite(index),
                    selectable ? White : new Color32(176, 184, 224, 190));
                PlaceTop(art.GetComponent<RectTransform>(), 16, 7, 68, 68);
                art.GetComponent<Image>().preserveAspect = true;
                NewPlacedText(slot.transform, slotNames[index], 13, active ? White : Muted,
                    5, 77, 90, 20, TextAnchor.MiddleCenter, FontStyle.Bold);
                if (equipped)
                    NewPlacedText(slot.transform, "已装备", 11, new Color32(111, 255, 194, 255),
                        5, 94, 90, 14, TextAnchor.MiddleCenter, FontStyle.Bold);
            }

            NewPlacedText(preview.transform, "预览属性，装备后下场战斗生效", 13, Muted,
                82, 668, 286, 26, TextAnchor.MiddleCenter);
        }

        private void BuildAccessoryDetail(int selected, bool equipped)
        {
            GameObject detail = NewPanel("AccessoryDetail", contentRoot, new Color32(9, 17, 55, 104), 22);
            PlaceTop(detail.GetComponent<RectTransform>(), 474, 116, 226, 692);
            if (!ApplyAiUiSprite(detail, "Art/AccessoryAI/UI/accessory-detail-panel-ai-v1"))
            {
                Outline edge = detail.AddComponent<Outline>();
                edge.effectColor = new Color32(94, 211, 255, 82);
                edge.effectDistance = new Vector2(1f, -1f);
            }

            NewPlacedText(detail.transform, GameModel.AccessoryNames[selected], 22, White,
                18, 20, 154, 38, TextAnchor.MiddleLeft, FontStyle.Bold);
            NewPlacedText(detail.transform, "共享", 14,
                selected == 2 ? Cyan : new Color32(255, 213, 97, 255),
                172, 20, 48, 38, TextAnchor.MiddleRight, FontStyle.Bold);
            NewPlacedText(detail.transform, "编队搭配", 15, new Color32(255, 202, 102, 255),
                18, 62, 80, 26, TextAnchor.MiddleLeft, FontStyle.Bold);
            NewPlacedText(detail.transform, equipped ? "已装备" : model.OwnsAccessory(selected) ? "可装备" : "未获得", 13,
                equipped ? new Color32(112, 255, 196, 255) : Pink,
                106, 66, 48, 26, TextAnchor.MiddleRight, FontStyle.Bold);
            GameObject detailArt = NewImage("AccessoryDetailArt", detail.transform, AccessoryItemSprite(selected), White);
            PlaceTop(detailArt.GetComponent<RectTransform>(), 158, 58, 58, 58);
            detailArt.GetComponent<Image>().preserveAspect = true;
            Text powerChange = NewPlacedText(detail.transform,
                $"战力变化 {model.AccessoryPowerChange(selected):+#,0;-#,0;0}", 16, Pink,
                18, 112, 202, 32, TextAnchor.MiddleLeft, FontStyle.Bold);
            powerChange.name = "AccessoryPowerChange";
            PanelKit.EnableBestFit(powerChange, 13);

            GameObject divider = NewImage("DetailDivider", detail.transform, null, new Color32(99, 213, 255, 92));
            PlaceTop(divider.GetComponent<RectTransform>(), 18, 142, 202, 2);
            NewPlacedText(detail.transform, "属性变化", 14, Muted,
                18, 150, 202, 24, TextAnchor.MiddleLeft, FontStyle.Bold);

            string[] names = { "生命", "攻击", "防御", "战力" };
            IReadOnlyList<CombatStats> currentStats = model.PartyStatsWithAccessory(model.Save.EquippedAccessory);
            IReadOnlyList<CombatStats> selectedStats = model.PartyStatsWithAccessory(selected);
            int[] before = { currentStats.Sum(stat => stat.Hp), currentStats.Sum(stat => stat.Attack),
                currentStats.Sum(stat => stat.Defense), currentStats.Sum(stat => stat.Power) };
            int[] after = { selectedStats.Sum(stat => stat.Hp), selectedStats.Sum(stat => stat.Attack),
                selectedStats.Sum(stat => stat.Defense), selectedStats.Sum(stat => stat.Power) };
            for (int row = 0; row < names.Length; row++)
            {
                float y = 180 + row * 56;
                GameObject reading = NewPanel("AccessoryStat-" + row, detail.transform,
                    new Color32(62, 52, 112, 48), 10);
                PlaceTop(reading.GetComponent<RectTransform>(), 12, y - 2, 202, 54);
                reading.GetComponent<Image>().raycastTarget = false;
                Text statName = NewPlacedText(detail.transform, names[row], 16, White,
                    18, y, 202, 24, TextAnchor.MiddleLeft, FontStyle.Bold);
                statName.name = "AccessoryStatName-" + row;
                Text beforeText = NewPlacedText(detail.transform, before[row].ToString("N0"), 18, Muted,
                    18, y + 24, 82, 28, TextAnchor.MiddleRight);
                beforeText.name = "AccessoryBefore-" + row;
                PanelKit.EnableBestFit(beforeText, 16);
                NewPlacedText(detail.transform, "→", 16, Cyan,
                    100, y + 24, 22, 28, TextAnchor.MiddleCenter, FontStyle.Bold);
                Text afterText = NewPlacedText(detail.transform, after[row].ToString("N0"), 18,
                    after[row] >= before[row] ? new Color32(111, 255, 194, 255) : Pink,
                    122, y + 24, 96, 28, TextAnchor.MiddleRight, FontStyle.Bold);
                afterText.name = "AccessoryAfter-" + row;
                PanelKit.EnableBestFit(afterText, 16);
            }

            CombatStatBonuses bonuses = model.EffectiveAccessoryBonuses(selected);
            NewPlacedText(detail.transform, $"搭配效果 · 强化 +{model.AccessoryUpgradeLevel(selected)}", 14, Pink,
                18, 408, 202, 28, TextAnchor.MiddleLeft, FontStyle.Bold);
            Text effects = NewPlacedText(detail.transform,
                $"生命 +{bonuses.Hp / 10f:0.#}%\n攻击 +{bonuses.Attack / 10f:0.#}%\n防御 +{bonuses.Defense / 10f:0.#}%",
                17, White, 18, 442, 202, 84, TextAnchor.UpperLeft);
            effects.name = "AccessoryEffects";

            GameObject equip = NewButton("AccessoryEquip", detail.transform, equipped ? "卸下" : "装备", 17,
                equipped ? new Color32(77, 70, 123, 255) : Pink, White, () =>
                {
                    model.EquipAccessory(selected, out string equipMessage);
                    Toast(equipMessage);
                    ShowScreen("accessory");
            });
            PlaceTop(equip.GetComponent<RectTransform>(), 18, 548, 202, 56);
            ApplyAiUiSprite(equip, "Art/AccessoryAI/UI/accessory-action-pink-ai-v1");
            equip.GetComponent<Button>().interactable = model.OwnsAccessory(selected);
            if (!model.OwnsAccessory(selected)) PanelKit.LabelOf(equip).text = "未获得";

            bool canUpgrade = model.CanUpgradeAccessory(selected, out int pieces, out int gold);
            string upgradeLabel = model.AccessoryUpgradeLevel(selected) >= 3 ? "已强化至 +3" :
                $"强化：{pieces}碎片 + {gold}星光币\n持有碎片 {model.Save.EquipmentFragments}";
            GameObject settings = NewButton("AccessoryUpgrade", detail.transform, upgradeLabel, 12,
                canUpgrade ? new Color32(45, 90, 125, 255) : new Color32(45, 52, 105, 220), White,
                () => { model.UpgradeAccessory(selected, out string upgradeMessage); ShowScreen("accessory"); Toast(upgradeMessage); });
            PlaceTop(settings.GetComponent<RectTransform>(), 18, 618, 202, 50);
            ApplyAiUiSprite(settings, "Art/AccessoryAI/UI/accessory-action-blue-ai-v1");
            settings.GetComponent<Button>().interactable = canUpgrade;
        }

        private void BuildAccessoryCollection(int selected)
        {
            float contentHeight = Mathf.Max(1f, contentRoot.rect.height);
            bool compact = contentHeight < 1132f;
            float collectionHeight = compact ? 202f : 300f;
            GameObject collection = NewPanel("AccessoryCollection", contentRoot,
                new Color32(8, 15, 50, 44), 24);
            PlaceTop(collection.GetComponent<RectTransform>(), 20, 824, 680, collectionHeight);
            if (!ApplyAiUiSprite(collection, "Art/AccessoryAI/UI/accessory-collection-panel-ai-v1"))
            {
                Outline edge = collection.AddComponent<Outline>();
                edge.effectColor = new Color32(255, 91, 194, 52);
                edge.effectDistance = new Vector2(1f, -1f);
            }

            NewPlacedText(collection.transform, "饰品图鉴", compact ? 18 : 20, White,
                18, compact ? 8 : 14, 180, compact ? 28 : 34,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            NewPlacedText(collection.transform, $"已收集 {model.Save.OwnedAccessories.Count}/6", 13, Muted,
                198, compact ? 10 : 18, 120, 28, TextAnchor.MiddleLeft);
            NewPlacedText(collection.transform, $"来源：{GameModel.AccessorySource(selected)} · 首通必得/重复强化", 12, Cyan,
                338, compact ? 10 : 18, 324, 28, TextAnchor.MiddleRight);

            string[] names = { "星轨耳返", "霓虹心链", "月桂舞鞋", "麦克风挂饰", "星辉手环", "舞台冠冕" };
            for (int index = 0; index < names.Length; index++)
            {
                int captured = index;
                bool owned = model.OwnsAccessory(index);
                bool active = selected == index;
                GameObject item = NewButton($"AccessoryCollection-{index}", collection.transform, string.Empty, 1,
                    active ? new Color32(74, 39, 126, 122) : new Color32(18, 25, 69, 30), White,
                    () =>
                    {
                        selectedAccessoryIndex = captured;
                        ShowScreen("accessory");
                    });
                PlaceTop(item.GetComponent<RectTransform>(), 16 + index * 109,
                    compact ? 44 : 56, 102, compact ? 118 : 176);
                Outline itemEdge = item.AddComponent<Outline>();
                itemEdge.effectColor = active
                    ? new Color32(255, 88, 198, 230)
                    : new Color32(91, 206, 255, owned ? (byte)105 : (byte)52);
                itemEdge.effectDistance = active ? new Vector2(2f, -2f) : new Vector2(1f, -1f);

                GameObject art = NewImage("Art", item.transform, AccessoryItemSprite(index),
                    owned ? White : new Color32(117, 126, 169, 155));
                PlaceTop(art.GetComponent<RectTransform>(), 8, compact ? 4 : 8, 86, compact ? 56 : 92);
                art.GetComponent<Image>().preserveAspect = true;
                NewPlacedText(item.transform, names[index], 12, owned ? White : Muted,
                    5, compact ? 58 : 105, 92, compact ? 28 : 34,
                    TextAnchor.MiddleCenter, FontStyle.Bold);
                NewPlacedText(item.transform, owned ? $"强化 +{model.AccessoryUpgradeLevel(index)}" : "查看来源", 11,
                    owned ? (index == 2 ? Cyan : new Color32(255, 211, 102, 255)) : Muted,
                    5, compact ? 86 : 140, 92, compact ? 17 : 20,
                    TextAnchor.MiddleCenter, FontStyle.Bold);
                if (model.Save.EquippedAccessory == index)
                    NewPlacedText(item.transform, "已装备", 11, new Color32(111, 255, 194, 255),
                        5, compact ? 101 : 160, 92, compact ? 14 : 18,
                        TextAnchor.MiddleCenter, FontStyle.Bold);
            }

            Text collectionSummary = NewPlacedText(collection.transform,
                $"当前战力 {model.TeamPower:N0}    ·    该搭配战力 {model.TeamPowerWithAccessory(selected):N0}",
                compact ? 13 : 15, White, 18, compact ? 166 : 248, 644,
                compact ? 28 : 34, TextAnchor.MiddleCenter, FontStyle.Bold);
            collectionSummary.name = "AccessoryCollectionSummary";
            PanelKit.EnableBestFit(collectionSummary, 10);
        }

        private void OpenMember(int memberIndex) => OpenTeamMember(memberIndex, -1);
        private void OpenTeamMember(int memberIndex, int teamSlot)
        {
            memberProfileReturn = null;
            CloseModal();
            MemberDefinition member = GameModel.Members[memberIndex];
            bool unlocked = model.IsUnlocked(memberIndex);
            int level = model.LevelOf(memberIndex);
            bool canTrain = model.CanTrain(memberIndex, out int trainingCost, out _);
            bool atLevelCap = level >= GameModel.MaxMemberLevel;
            int displayPower = model.PowerOf(memberIndex);
            MemberDisplayStats(member, memberIndex, out int attack, out int hp, out int critPercent,
                out int speed);
            MemberSkillCopy(member, out string firstSkillName, out string firstSkillEffect,
                out string secondSkillName, out string secondSkillEffect);
            MemberNormalAttackCopy(member, out string normalAttackName, out string normalAttackEffect);

            GameObject overlay = NewImage("MemberModal", safeRoot, null, new Color32(3, 4, 20, 220));
            Stretch(overlay.GetComponent<RectTransform>());
            overlay.GetComponent<Image>().raycastTarget = true;
            modalObject = overlay;

            GameObject panel = NewPanel("Panel", overlay.transform, new Color32(12, 18, 40, 253), 28);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(620, 1250);
            AddQuietPanelEdge(panel);

            if (MemberRosterVisibility.ShowsRealPortrait(unlocked))
            {
                GameObject portrait = NewImage("Portrait", panel.transform,
                    Resources.Load<Sprite>(member.ResourcePath), White);
                PlaceTop(portrait.GetComponent<RectTransform>(), 24, 52, 292, 390);
                Image portraitImage = portrait.GetComponent<Image>();
                portraitImage.preserveAspect = true;
                portraitImage.useSpriteMesh = true;
            }
            else
            {
                BuildLockedMemberPortrait(panel);
            }

            Text ownership = NewPlacedText(panel.transform, unlocked ? "已签约成员" : "尚未签约", 14,
                unlocked ? new Color32(111, 255, 194, 255) : new Color32(255, 185, 218, 255),
                328, 48, 250, 28, TextAnchor.MiddleLeft, FontStyle.Bold);
            ownership.name = "MemberOwnershipStatus";
            NewPlacedText(panel.transform, unlocked ? member.Name : MemberRosterVisibility.LockedName, 32,
                unlocked ? White : new Color32(198, 192, 226, 255),
                326, 76, 250, 48, TextAnchor.MiddleLeft, FontStyle.Bold);
            NewPlacedText(panel.transform,
                unlocked ? $"{MemberRace(member, memberIndex)} · {member.Career}" : MemberRosterVisibility.LockedCareer,
                17, unlocked ? Pink : Muted,
                328, 122, 248, 30, TextAnchor.MiddleLeft, FontStyle.Bold);
            Text power = NewPlacedText(panel.transform,
                unlocked ? $"等级 {level}  ·  战力 {displayPower:N0}"
                    : MemberRosterVisibility.LockedProfileProgress(model.Save.UnlockedMembers.Count,
                        GameModel.Members.Length),
                16, Cyan, 328, 152, 252, 38, TextAnchor.MiddleLeft, FontStyle.Bold);
            power.name = "MemberPower";

            // 未获得角色到此为止：不创建属性/技能/队长面板，避免未公开数据出现在界面树里。
            if (!MemberRosterVisibility.ShowsCombatStats(unlocked))
            {
                BuildLockedMemberProfileBody(panel, memberIndex);
                return;
            }

            GameObject statPanel = NewPanel("MemberStatPanel", panel.transform,
                new Color32(12, 23, 67, 215), 20);
            PlaceTop(statPanel.GetComponent<RectTransform>(), 320, 198, 276, 238);
            AddQuietPanelEdge(statPanel);
            Text statTitle = NewPlacedText(statPanel.transform, MemberProfileSections.BaseStatsTitle, 16,
                new Color32(255, 183, 229, 255), 16, 12, 244, 28, TextAnchor.MiddleLeft, FontStyle.Bold);
            statTitle.name = "MemberSectionBaseStats";
            AddMemberStat(statPanel.transform, "MemberStatAttack", "攻击", attack.ToString("N0"), 46);
            AddMemberStat(statPanel.transform, "MemberStatHp", "生命", hp.ToString("N0"), 84);
            AddMemberStat(statPanel.transform, "MemberStatCrit", "暴击", critPercent + "%", 122);
            AddMemberStat(statPanel.transform, "MemberStatSpeed", "速度", speed.ToString(), 160);
            AddMemberStat(statPanel.transform, "MemberStatAffection", "好感度",
                $"{model.AffectionOf(memberIndex)} · {model.AffectionTierOf(memberIndex)}", 198);

            GameObject skillPanel = NewPanel("MemberSkillPanel", panel.transform,
                new Color32(18, 22, 70, 222), 22);
            PlaceTop(skillPanel.GetComponent<RectTransform>(), 28, 458, 564, 302);
            AddQuietPanelEdge(skillPanel);
            Text normalTitle = NewPlacedText(skillPanel.transform, MemberProfileSections.NormalAttackTitle, 16,
                new Color32(255, 184, 230, 255), 18, 8, 520, 26, TextAnchor.MiddleLeft, FontStyle.Bold);
            normalTitle.name = "MemberSectionNormalAttack";
            Text normalCopy = NewPlacedText(skillPanel.transform,
                $"{normalAttackName} · {normalAttackEffect}", 14, White,
                18, 34, 520, 26, TextAnchor.MiddleLeft, FontStyle.Bold);
            normalCopy.name = "MemberNormalAttack";
            PanelKit.EnableBestFit(normalCopy, 11);
            Text activeTitle = NewPlacedText(skillPanel.transform, MemberProfileSections.ActiveSkillsTitle, 16,
                new Color32(255, 184, 230, 255), 18, 62, 520, 26, TextAnchor.MiddleLeft, FontStyle.Bold);
            activeTitle.name = "MemberSectionActiveSkills";
            BuildReadableMemberSkill(skillPanel.transform, "MemberSkillPrimary", firstSkillName, firstSkillEffect,
                14, Pink, 90, 152);
            BuildReadableMemberSkill(skillPanel.transform, "MemberSkillSecondary", secondSkillName, secondSkillEffect,
                292, Cyan, 90, 152);
            NewPlacedText(skillPanel.transform, MemberTeamBonus(member), 13,
                new Color32(110, 225, 255, 255), 20, 248, 520, 32, TextAnchor.MiddleLeft, FontStyle.Bold);

            GameObject guidePanel = NewPanel("MemberAcquireGuide", panel.transform,
                new Color32(20, 26, 73, 220), 18);
            PlaceTop(guidePanel.GetComponent<RectTransform>(), 28, 780, 564, 128);
            AddQuietPanelEdge(guidePanel);
            NewPlacedText(guidePanel.transform, unlocked ? "本次培养" : "获取方式", 15,
                new Color32(255, 188, 231, 255), 18, 12, 520, 26, TextAnchor.MiddleLeft, FontStyle.Bold);
            if (unlocked)
            {
                Text preview = NewPlacedText(guidePanel.transform, atLevelCap
                        ? $"等级 {level} · 已达到当前等级上限"
                        : $"等级 {level} → {level + 1} · 仅提升当前成员",
                    16, White, 18, 42, 520, 30, TextAnchor.MiddleLeft);
                preview.name = "MemberTrainingPreview";
                PanelKit.EnableBestFit(preview, 14);
                Text cost = NewPlacedText(guidePanel.transform, atLevelCap
                        ? "已满级 · 无需继续训练"
                        : $"消耗星光币 {trainingCost:N0} · 持有 {model.Save.Gold:N0}",
                    16, canTrain || atLevelCap ? Cyan : Pink, 18, 78, 520, 30, TextAnchor.MiddleLeft);
                cost.name = "MemberTrainingCost";
                PanelKit.EnableBestFit(cost, 14);
            }
            else
                NewPlacedText(guidePanel.transform, MemberAcquisitionCopy(member),
                    14, White, 18, 42, 520, 66, TextAnchor.UpperLeft);

            if (unlocked)
            {
                GameObject train = NewButton("Train", panel.transform,
                    atLevelCap ? "已满级" : canTrain ? "练习升级" : "星光币不足", 18, Pink, White, () =>
                {
                    // 只进入练习反馈面板；真正的升级仍调用既有 model.Train（只扣星光币）。
                    if (!canTrain) return;
                    OpenTrainingPractice(memberIndex, teamSlot);
                });
                train.GetComponent<Button>().interactable = canTrain;
                PlaceTop(train.GetComponent<RectTransform>(), 34, 1070, 258, 60);
                AddQuietPanelEdge(train);

                GameObject team = NewButton("Team", panel.transform,
                    teamSlot >= 0 ? "更换成员" : model.IsInTeam(memberIndex) ? "移出编队" : "加入 / 替换", 18, Purple, White, () =>
                {
                    if (teamSlot >= 0) { OpenTeamSlotPicker(teamSlot); return; }
                    if (!model.IsInTeam(memberIndex) && model.Save.Team.Count >= GameModel.TeamCapacity)
                    { OpenTeamReplacement(memberIndex); return; }
                    model.ToggleTeamMember(memberIndex, out string message);
                    Toast(message);
                    CloseModal();
                    ShowScreen(currentScreen);
                });
                PlaceTop(team.GetComponent<RectTransform>(), 328, 1070, 258, 60);
                AddQuietPanelEdge(team);
            }
            else
            {
                GameObject acquire = NewButton("AcquireMember", panel.transform, "前往选秀", 18,
                    Pink, White, () =>
                {
                    CloseModal();
                    ShowScreen("audition");
                });
                PlaceTop(acquire.GetComponent<RectTransform>(), 154, 1070, 312, 60);
                AddQuietPanelEdge(acquire);
            }

            GameObject captainPanel = NewPanel("MemberCaptainPanel", panel.transform, new Color32(26, 33, 65, 255), 18);
            PlaceTop(captainPanel.GetComponent<RectTransform>(), 28, 924, 564, 130);
            FlowText(captainPanel.transform, "CaptainEffectTitle", MemberProfileSections.PassiveCaptainTitle, 16, 16, 10, 532, 26, Cyan);
            FlowText(captainPanel.transform, "CaptainEffectDescription", CaptainEffectCopy(member.Race), 14, 16, 43, 320, 72, Muted);
            bool isCaptain = model.Save.Team.Count > 0 && model.Save.Team[0] == memberIndex;
            FlowButton(captainPanel.transform, "AppointCaptain", !unlocked ? "签约后可任命" : isCaptain ? "当前队长" : model.IsInTeam(memberIndex) ? "设为队长" : "上阵并任命", 352, 52, 196, 52,
                () => ConfirmCaptain(memberIndex)).GetComponent<Button>().interactable = unlocked && !isCaptain;

            GameObject close = NewButton("Close", panel.transform, "关闭档案", 16,
                new Color32(63, 57, 108, 245), White, () =>
                { Action back = memberProfileReturn; memberProfileReturn = null; CloseModal(); back?.Invoke(); });
            PlaceTop(close.GetComponent<RectTransform>(), unlocked ? 406 : 185, 1152, 186, 56);
            if (unlocked)
            {
                FlowButton(panel.transform, "MemberEquipment", "角色饰品", 28, 1152, 172, 56, () =>
                { equipmentMember = memberIndex; selectedAccessoryIndex = Math.Max(0, model.EquippedAccessoryFor(memberIndex)); ShowScreen("accessory"); });

                // v0.3.4 深入交流：羁绊档专属入口。锁定/已用仍可点击，用 Toast 说明原因。
                bool deepUnlocked = model.DeepTalkUnlocked(memberIndex);
                bool deepUsed = model.DeepTalkUsedToday(memberIndex);
                string deepLabel = deepUsed ? "今日已交流"
                    : deepUnlocked ? "深入交流"
                    : $"好感度{GameModel.DeepTalkAffectionUnlock}解锁";
                Color deepBackground = deepUnlocked && !deepUsed
                    ? new Color32(201, 92, 148, 255)
                    : new Color32(63, 57, 108, 245);
                GameObject deepTalk = NewButton("DeepTalk", panel.transform, deepLabel, 15,
                    deepBackground, White, () =>
                {
                    int affectionBefore = model.AffectionOf(memberIndex);
                    if (!model.DeepTalk(memberIndex, out string message, out string dialogue))
                    {
                        Toast(message);
                        return;
                    }
                    OpenDeepTalkResult(memberIndex, teamSlot, dialogue, affectionBefore, model.AffectionOf(memberIndex));
                });
                deepTalk.name = "MemberDeepTalkButton";
                PlaceTop(deepTalk.GetComponent<RectTransform>(), 208, 1152, 190, 56);
                AddQuietPanelEdge(deepTalk);
            }
        }

        /// <summary>v0.3.4 深入交流结果弹窗：立绘 + 羁绊文案 + 好感度变化，返回原档案。</summary>
        private void OpenDeepTalkResult(int memberIndex, int teamSlot, string dialogue,
            int affectionBefore, int affectionAfter)
        {
            MemberDefinition member = GameModel.Members[memberIndex];
            CloseModal();

            GameObject overlay = NewImage("DeepTalkModal", safeRoot, null, new Color32(3, 4, 20, 220));
            Stretch(overlay.GetComponent<RectTransform>());
            overlay.GetComponent<Image>().raycastTarget = true;
            modalObject = overlay;

            GameObject panel = NewPanel("Panel", overlay.transform, new Color32(26, 14, 40, 253), 28);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(560, 640);
            AddQuietPanelEdge(panel);

            GameObject portrait = NewImage("Portrait", panel.transform,
                Resources.Load<Sprite>(member.ResourcePath), White);
            PlaceTop(portrait.GetComponent<RectTransform>(), 28, 28, 204, 288);
            Image portraitImage = portrait.GetComponent<Image>();
            portraitImage.preserveAspect = true;
            portraitImage.useSpriteMesh = true;

            NewPlacedText(panel.transform, "深入交流 · 羁绊时刻", 22, new Color32(255, 183, 229, 255),
                248, 44, 292, 34, TextAnchor.MiddleLeft, FontStyle.Bold);
            NewPlacedText(panel.transform, member.Name, 30, White,
                248, 84, 292, 44, TextAnchor.MiddleLeft, FontStyle.Bold);
            NewPlacedText(panel.transform, $"{MemberRace(member, memberIndex)} · {member.Career}", 15, Pink,
                250, 132, 290, 28, TextAnchor.MiddleLeft, FontStyle.Bold);

            Text dialogueText = NewPlacedText(panel.transform, dialogue, 17, White,
                28, 340, 504, 176, TextAnchor.UpperLeft);
            dialogueText.name = "DeepTalkDialogue";
            dialogueText.lineSpacing = 1.25f;

            Text affectionGain = NewPlacedText(panel.transform,
                $"好感度 {affectionBefore} → {affectionAfter}（+{affectionAfter - affectionBefore}） · 明日可再来",
                16, new Color32(111, 255, 194, 255), 28, 528, 504, 30,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            affectionGain.name = "DeepTalkAffectionResult";
            PanelKit.EnableBestFit(affectionGain, 13);

            GameObject back = NewButton("BackToProfile", panel.transform, "返回档案", 17,
                new Color32(63, 57, 108, 245), White, () => OpenTeamMember(memberIndex, teamSlot));
            PlaceTop(back.GetComponent<RectTransform>(), 28, 572, 504, 52);
            AddQuietPanelEdge(back);
        }

        private void MemberDisplayStats(MemberDefinition member, int index, out int attack, out int hp,
            out int critPercent, out int speed)
        {
            CombatStats stats = model.StatsOf(index);
            attack = stats.Attack;
            hp = stats.Hp;
            critPercent = stats.CritPermille / 10;
            speed = stats.Speed;
        }

        private void AddMemberStat(Transform parent, string name, string label, string value, float y)
        {
            Text labelText = NewPlacedText(parent, label, 14, Muted,
                18, y, 100, 28, TextAnchor.MiddleLeft, FontStyle.Bold);
            labelText.name = name + "Label";
            Text valueText = NewPlacedText(parent, value, 15, White,
                126, y, 130, 28, TextAnchor.MiddleRight, FontStyle.Bold);
            valueText.name = name;
        }

        private void MemberSkillCopy(MemberDefinition member, out string firstName, out string firstEffect,
            out string secondName, out string secondEffect)
        {
            if (model.Tactics.FindUnit(member.Id)?.GrowthModel == "idol-v1")
            {
                CombatRace race = BattleSimulator.ParseCombatRace(member.Race);
                firstName = BattleSimulator.ActiveSkillName(race, false);
                secondName = BattleSimulator.ActiveSkillName(race, true);
                firstEffect = BattleSimulator.ActiveSkillDescription(race, false);
                secondEffect = BattleSimulator.ActiveSkillDescription(race, true);
                return;
            }
            List<SkillDefinition> skills = MemberBattlePresentation.FeaturedSkills(model.Tactics, member.Id);
            SkillDefinition first = skills.Count > 0 ? skills[0] : null;
            SkillDefinition second = skills.Count > 1 ? skills[1] : null;
            firstName = first?.Name ?? "暂无技能";
            secondName = second?.Name ?? "暂无第二技能";
            firstEffect = MemberBattlePresentation.DescribeSkill(first);
            secondEffect = MemberBattlePresentation.DescribeSkill(second);
        }

        private static string MemberTeamBonus(MemberDefinition member)
        {
            return "技能随战斗骰型强化；具体效果以技能说明为准。";
        }

        private static string MemberRace(MemberDefinition member, int index)
        {
            if (member != null && !string.IsNullOrWhiteSpace(member.Race)) return member.Race.Trim();
            string[] fallback = { "魅族", "魔族 · 恶魔", "海灵族 · 人鱼", "血精灵" };
            return fallback[Mathf.Abs(index) % fallback.Length];
        }

        private static string MemberRaceFamily(MemberDefinition member, int index)
        {
            string race = MemberRace(member, index);
            int separator = race.IndexOf('·');
            return separator > 0 ? race.Substring(0, separator).Trim() : race;
        }

        private static string MemberAcquisitionCopy(MemberDefinition member)
        {
            return "在选秀的线上或线下面试查看候选；浏览免费，按报价签约后可培养、编队并出战。";
        }

        private void OpenProfile()
        {
            OpenInfoModal("制作人档案",
                $"音律少女  ·  队伍均级 {TeamAverageLevel}\n当前编队 {model.Save.Team.Count}/4 人\n组合战力 {model.TeamPower:N0}\n已签约 {model.Save.UnlockedMembers.Count}/{GameModel.Members.Length} 名成员",
                "返回大厅", null);
        }

        private void OpenInbox()
        {
            OpenInfoModal("事务收件箱",
                "目前没有未读事务。演出奖励、签约结果和活动进度都会在完成操作时立即结算并保存。",
                "知道了", null);
        }

        private void OpenActivity()
        {
            IdleIncomeReport preview = model.PreviewIdleIncome();
            int gold = preview.AmountOf(CurrencyIds.Gold);
            int diamonds = preview.AmountOf(CurrencyIds.Diamond);
            bool canClaim = model.CanClaimIdleIncome && (gold > 0 || diamonds > 0);
            string body = $"挂机舞台收益\n已累积星光币 {gold:N0}\n已累积星钻 {diamonds:N0}";
            if (preview.Capped) body += "\n收益已达上限，请尽快领取。";
            else if (!canClaim) body += "\n收益还在累积，稍后再来。";

            OpenInfoModal("闪耀舞台", body,
                canClaim ? $"领取 · 星光币 {gold:N0} / 星钻 {diamonds:N0}" : "暂无收益",
                canClaim
                    ? () =>
                    {
                        if (model.ClaimIdleIncome(out string message)) Toast(message);
                        else Toast(string.IsNullOrEmpty(message) ? "暂无收益" : message);
                    }
                    : null);
        }

        private void OpenDailyTasks()
        {
            CloseModal();
            SuspendLobbyMedia();
            TaskBoardPanel.Open(safeRoot, model, model, () => ShowScreen("lobby"), Toast);
        }

        private void OpenGacha()
        {
            CloseModal();
            GachaPanel.OpenEmbedded(contentRoot, model, model, Toast, member =>
            {
                memberOwnedOnly = true;
                memberRoleFilterIndex = memberRaceFilterIndex = memberPageIndex = 0;
                memberSearchQuery = string.Empty;
                ShowScreen("members");
                OpenMember(member);
            });
        }

        private void OpenPerformanceConfirm()
        {
            OpenPerformanceStage();
        }

        private void OpenPerformanceStage()
        {
            CloseModal();
            if (model.Save.Stamina < GameModel.PerformanceStaminaCost)
            {
                Toast("体力不足，暂时无法开始演出");
                return;
            }

            SuspendLobbyMedia();
            PerformanceStagePanel.Open(safeRoot, model, () => ShowScreen("lobby"), Toast);
        }

        private void OpenLevelMap()
        {
            CloseModal();
            SuspendLobbyMedia();
            // The chapter map owns a dedicated in-context toast positioned above its dock.
            // Forwarding the same message to the app toast creates two simultaneous notices,
            // with the global one covering chapter rewards and tasks.
            LevelMapPanel.Open(safeRoot, model, () => ShowScreen("lobby"), growth: OpenBattleGrowth);
        }

        private void OpenBattleGrowth(string destination)
        {
            if (destination == "training")
            {
                ShowScreen("members");
                int lowest = -1;
                foreach (int index in model.Save.Team)
                    if (model.IsUnlocked(index) && (lowest < 0 || model.LevelOf(index) < model.LevelOf(lowest))) lowest = index;
                if (lowest >= 0) OpenMember(lowest);
            }
            else
            {
                if (destination == "accessory" && model.LastAwardedAccessory >= 0)
                    selectedAccessoryIndex = model.LastAwardedAccessory;
                ShowScreen(destination == "accessory" ? "accessory" : "team");
            }
        }

        private void OpenInfoModal(string title, string body, string primaryLabel,
            UnityEngine.Events.UnityAction primaryAction)
        {
            CloseModal();
            GameObject overlay = NewImage("InfoModal", safeRoot, null, new Color32(3, 4, 20, 224));
            Stretch(overlay.GetComponent<RectTransform>());
            overlay.GetComponent<Image>().raycastTarget = true;
            modalObject = overlay;

            GameObject panel = NewPanel("Panel", overlay.transform, new Color32(29, 23, 76, 252), 28);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(600, 490);

            NewPlacedText(panel.transform, title, 29, White, 38, 34, 455, 50,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            GameObject close = NewButton("Close", panel.transform, "×", 28, Color.clear, White, CloseModal);
            PlaceTop(close.GetComponent<RectTransform>(), 520, 25, 50, 50);
            NewPlacedText(panel.transform, body, 17, Muted, 42, 116, 516, 210,
                TextAnchor.UpperLeft);

            UnityEngine.Events.UnityAction confirmed = () =>
            {
                CloseModal();
                primaryAction?.Invoke();
            };
            GameObject primary = NewButton("Primary", panel.transform, primaryLabel, 18, Pink, White, confirmed);
            PlaceTop(primary.GetComponent<RectTransform>(), 170, 360, 260, 64);
        }

        private void OpenSettings()
        {
            CloseModal();
            GameObject overlay = NewImage("SettingsModal", safeRoot, null, new Color32(3, 4, 20, 220));
            Stretch(overlay.GetComponent<RectTransform>());
            overlay.GetComponent<Image>().raycastTarget = true;
            modalObject = overlay;

            GameObject panel = NewPanel("Panel", overlay.transform, new Color32(29, 23, 76, 252), 28);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(600, 720);

            NewPlacedText(panel.transform, "游戏设置", 30, White, 35, 28, 400, 48, TextAnchor.MiddleLeft, FontStyle.Bold);
            GameObject close = NewButton("Close", panel.transform, "×", 28, Color.clear, White, CloseModal);
            PlaceTop(close.GetComponent<RectTransform>(), 520, 22, 50, 50);

            SettingsRow(panel.transform, "音乐", model.Save.MusicEnabled ? "已开启" : "已关闭", 115, () =>
            {
                model.ToggleMusic();
                ApplyMusicRouting();
                OpenSettings();
            });
            SettingsRow(panel.transform, "音效", model.Save.SfxEnabled ? "已开启" : "已关闭", 205, () =>
            {
                model.ToggleSfx();
                gameAudio.ApplySettings();
                OpenSettings();
            });
            SettingsRow(panel.transform, "画质", model.Save.QualityLevel == 1 ? "高清" : "流畅", 295, () =>
            {
                model.ToggleQuality();
                OpenSettings();
            });

            NewPlacedText(panel.transform, "画面采用固定比例与安全区适配，电脑、网页和安卓设备共用同一布局。",
                15, Muted, 45, 405, 510, 72, TextAnchor.UpperLeft);

            GameObject reset = NewButton("Reset", panel.transform, "清除本机存档", 16,
                new Color32(108, 49, 89, 255), White, () =>
            {
                OpenInfoModal("确认清除本机存档？",
                    "这会删除本机的培养、资源和通关记录，无法撤销。\n\n确认后从 1-1 重新开始，成员回到 1 级，后续关卡锁定，通关星级清零。\n\n不想清除，请点右上角 ×。",
                    "确认清除并重新开始", () =>
                {
                    model.Reset();
                    ApplyMusicRouting();
                    ShowScreen("lobby");
                    Toast("已重新开始：成员 1 级，仅开放 1-1");
                });
            });
            PlaceTop(reset.GetComponent<RectTransform>(), 150, 525, 300, 58);

            GameObject done = NewButton("Done", panel.transform, "完成", 18, Pink, White, CloseModal);
            PlaceTop(done.GetComponent<RectTransform>(), 150, 620, 300, 62);
        }

        private void SettingsRow(Transform parent, string title, string value, int y, UnityEngine.Events.UnityAction action)
        {
            GameObject row = NewPanel($"Setting-{title}", parent, new Color32(52, 43, 102, 220), 16);
            PlaceTop(row.GetComponent<RectTransform>(), 40, y, 520, 70);
            NewPlacedText(row.transform, title, 18, White, 20, 12, 220, 45, TextAnchor.MiddleLeft, FontStyle.Bold);
            GameObject toggle = NewButton("Toggle", row.transform, value, 16, Purple, White, action);
            PlaceTop(toggle.GetComponent<RectTransform>(), 335, 10, 165, 50);
        }

        private void AdvanceStory()
        {
            bool succeeded = model.AdvanceStory(out string message);
            if (succeeded) gameAudio.PlaySuccess();
            Toast(message);
            ShowScreen("lobby");
        }

        private void CheckIn()
        {
            bool succeeded = model.CheckIn(out string message);
            if (succeeded) gameAudio.PlaySuccess();
            Toast(message);
            ShowScreen("lobby");
        }

        private void ScreenTitle(string eyebrow, string title, string subtitle)
        {
            NewPlacedText(contentRoot, eyebrow, 13, new Color32(255, 174, 225, 255), 20, 8, 340, 24,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            NewPlacedText(contentRoot, title, 34, White, 20, 31, 430, 50, TextAnchor.MiddleLeft, FontStyle.Bold);
            NewPlacedText(contentRoot, subtitle, 14, Muted, 22, 75, 500, 27, TextAnchor.MiddleLeft);
        }

        private void MiniCard(Transform parent, string title, string subtitle, int x, int y, int width, int height,
            Color color, UnityEngine.Events.UnityAction action, bool darkText = false, string actionLabel = "",
            string eyebrow = "", int emblemIndex = -1, int titleSize = 20)
        {
            GameObject card = NewPanel(title, parent, color, 20);
            PlaceTop(card.GetComponent<RectTransform>(), x, y, width, height);
            Mask cardMask = card.AddComponent<Mask>();
            cardMask.showMaskGraphic = true;
            Outline edge = card.AddComponent<Outline>();
            edge.effectColor = darkText
                ? new Color32(255, 255, 255, 128)
                : new Color32(255, 184, 238, 125);
            edge.effectDistance = new Vector2(1f, -1f);
            Button button = card.AddComponent<Button>();
            button.targetGraphic = card.GetComponent<Image>();
            button.onClick.AddListener(() =>
            {
                action?.Invoke();
                ResumeMediaAfterUserGesture();
                gameAudio?.PlayClick();
            });
            Color primary = darkText ? new Color32(72, 54, 105, 255) : White;
            Color secondary = darkText ? new Color32(93, 72, 116, 255) : new Color32(238, 221, 250, 255);

            if (emblemIndex >= 0)
            {
                float iconSize;
                float iconX;
                float iconY;
                switch (emblemIndex)
                {
                    case 0: iconSize = 131f; iconX = 22f; iconY = 53f; break;
                    case 1: iconSize = 138f; iconX = 114f; iconY = -27f; break;
                    case 2: iconSize = 144f; iconX = 105f; iconY = -24f; break;
                    case 3: iconSize = 131f; iconX = 115f; iconY = 40f; break;
                    case 4: iconSize = 128f; iconX = 32f; iconY = 54f; break;
                    case 5: iconSize = 135f; iconX = 98f; iconY = -29f; break;
                    default:
                        iconSize = Mathf.Min(width * 0.48f, height * 0.84f);
                        iconX = width - iconSize - 3f;
                        iconY = (height - iconSize) * 0.5f;
                        break;
                }
                float emblemAlpha = emblemIndex switch
                {
                    0 => 0.82f,
                    1 => 0.70f,
                    2 => 0.48f,
                    3 => 0.56f,
                    4 => 0.84f,
                    5 => 0.38f,
                    _ => 0.45f,
                };
                GameObject emblem = NewImage("Emblem", card.transform, LobbyEmblemSprite(emblemIndex),
                    new Color(1f, 1f, 1f, emblemAlpha));
                PlaceTop(emblem.GetComponent<RectTransform>(), iconX, iconY, iconSize, iconSize);
                Image emblemImage = emblem.GetComponent<Image>();
                emblemImage.preserveAspect = true;
                emblemImage.useSpriteMesh = true;
                emblem.transform.SetAsFirstSibling();
            }

            if (!string.IsNullOrEmpty(eyebrow))
                NewPlacedText(card.transform, eyebrow, 10,
                    darkText ? new Color32(192, 91, 151, 255) : new Color32(255, 172, 226, 255),
                    14, 9, width - 28, 18, TextAnchor.MiddleLeft, FontStyle.Bold);

            NewPlacedText(card.transform, title, titleSize, primary,
                14, string.IsNullOrEmpty(eyebrow) ? 16 : 30, width - 28, 42,
                TextAnchor.MiddleLeft, FontStyle.Bold);

            bool liveCard = emblemIndex == 0;
            float actionWidth = string.IsNullOrEmpty(actionLabel) ? 0 : 76;
            NewPlacedText(card.transform, subtitle, 12, secondary,
                liveCard ? 12f : 14f, liveCard ? height - 87f : height - 47f,
                liveCard ? width - 24f : width - 28 - actionWidth, 30,
                liveCard ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft);
            if (!string.IsNullOrEmpty(actionLabel))
            {
                bool liveAction = liveCard;
                float pillWidth = liveAction ? 122f : 68f;
                float pillX = liveAction ? (width - pillWidth) * 0.5f : width - pillWidth - 12f;
                float pillY = liveAction ? height - 48f : height - 43f;
                GameObject actionPill = NewPanel("Action", card.transform,
                    darkText ? new Color32(100, 65, 127, 72) : new Color32(255, 123, 200, (byte)(liveAction ? 238 : 72)), 12);
                PlaceTop(actionPill.GetComponent<RectTransform>(), pillX, pillY, pillWidth, liveAction ? 34f : 28f);
                actionPill.GetComponent<Image>().raycastTarget = false;
                Text actionText = NewText("Label", actionPill.transform, actionLabel, liveAction ? 13 : 11,
                    liveAction ? White : primary,
                    FontStyle.Bold, TextAnchor.MiddleCenter);
                Stretch(actionText.rectTransform, 4, 2, -4, -2);
            }
        }

        private void LobbyHotspot(Transform parent, string title, int emblemIndex,
            int x, int y, int width, int height, UnityEngine.Events.UnityAction action)
        {
            GameObject hotspot = NewImage(title, parent, null, Color.clear);
            PlaceTop(hotspot.GetComponent<RectTransform>(), x, y, width, height);
            Image hitArea = hotspot.GetComponent<Image>();
            hitArea.raycastTarget = true;
            Button button = hotspot.AddComponent<Button>();
            button.targetGraphic = hitArea;
            button.onClick.AddListener(() =>
            {
                action?.Invoke();
                ResumeMediaAfterUserGesture();
                gameAudio?.PlayClick();
            });

            GameObject emblem = NewImage("Emblem", hotspot.transform, GeneratedLobbySprite(emblemIndex), White);
            Stretch(emblem.GetComponent<RectTransform>());
            Image emblemImage = emblem.GetComponent<Image>();
            emblemImage.preserveAspect = true;
            emblemImage.useSpriteMesh = true;
            emblemImage.raycastTarget = false;

            Text labelText = NewPlacedText(hotspot.transform, title, 22, White,
                22, height - 58, width - 44, 46, TextAnchor.MiddleCenter, FontStyle.Bold);
            AddReadableShadow(labelText);
            Outline outline = labelText.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color32(238, 118, 255, 215);
            outline.effectDistance = new Vector2(1.4f, -1.4f);
        }

        private void BuildStageCallToAction(Transform parent)
        {
            GameObject stage = NewImage("LiveOnStage", parent, null, Color.clear);
            PlaceTop(stage.GetComponent<RectTransform>(), 165, 1002, 390, 264);
            Image hitImage = stage.GetComponent<Image>();
            hitImage.raycastTarget = true;
            Button button = stage.AddComponent<Button>();
            button.targetGraphic = hitImage;
            button.onClick.AddListener(() =>
            {
                OpenLevelMap();
                ResumeMediaAfterUserGesture();
            });

            GameObject frame = NewImage("StageFrame", stage.transform, GeneratedLobbySprite(5), White);
            Stretch(frame.GetComponent<RectTransform>());
            Image frameImage = frame.GetComponent<Image>();
            frameImage.preserveAspect = true;
            frameImage.useSpriteMesh = true;
            frameImage.raycastTarget = false;

            Text live = NewPlacedText(stage.transform, "开始演出", 40, White, 67, 77, 256, 70,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            live.fontStyle = FontStyle.Bold;
            AddReadableShadow(live);
            Outline outline = live.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color32(239, 77, 255, 220);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            NewPlacedText(stage.transform, "舞台已就绪", 16, new Color32(255, 231, 249, 255),
                91, 139, 208, 30, TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        private Sprite GeneratedLobbySprite(int index)
        {
            if (generatedLobbySprites.TryGetValue(index, out Sprite cached) && cached != null)
                return cached;

            string path = index switch
            {
                1 => "Art/LobbyAI/lobby-stage-hotspot-v2",
                2 => "Art/LobbyAI/lobby-story-hotspot-v2",
                3 => "Art/LobbyAI/lobby-task-hotspot-v2",
                5 => "Art/LobbyAI/lobby-perform-cta-v2",
                _ => string.Empty
            };

            Sprite sprite = string.IsNullOrEmpty(path) ? null : Resources.Load<Sprite>(path);
            if (sprite == null && !string.IsNullOrEmpty(path))
            {
                Texture2D texture = Resources.Load<Texture2D>(path);
                if (texture != null)
                {
                    sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                    sprite.name = $"GeneratedLobby-{index}";
                    sprite.hideFlags = HideFlags.DontSave;
                }
            }

            sprite ??= LobbyEmblemSprite(index);
            generatedLobbySprites[index] = sprite;
            return sprite;
        }

        private GameObject NewAiDecoration(string name, Transform parent, string resourcePath)
        {
            Sprite sprite = AiUiSprite(resourcePath);
            GameObject decoration = NewImage(name, parent, sprite, sprite != null ? White : Color.clear);
            Image image = decoration.GetComponent<Image>();
            image.preserveAspect = false;
            image.useSpriteMesh = true;
            image.raycastTarget = false;
            return decoration;
        }

        private bool ApplyAiUiSprite(GameObject target, string resourcePath, bool preserveAspect = false)
        {
            Sprite sprite = AiUiSprite(resourcePath);
            Image image = target != null ? target.GetComponent<Image>() : null;
            if (sprite == null || image == null) return false;

            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.color = White;
            image.preserveAspect = preserveAspect;
            image.useSpriteMesh = true;
            return true;
        }

        private Sprite AiUiSprite(string resourcePath)
        {
            if (string.IsNullOrWhiteSpace(resourcePath)) return null;
            if (aiUiSprites.TryGetValue(resourcePath, out Sprite cached) && cached != null) return cached;

            Sprite sprite = Resources.Load<Sprite>(resourcePath);
            if (sprite == null)
            {
                Texture2D texture = Resources.Load<Texture2D>(resourcePath);
                if (texture != null)
                {
                    sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                    sprite.name = "Runtime-AI-UI-" + resourcePath.Replace('/', '-');
                    sprite.hideFlags = HideFlags.DontSave;
                    runtimeAiUiSprites.Add(sprite);
                }
            }

            if (sprite != null) aiUiSprites[resourcePath] = sprite;
            return sprite;
        }

        private Sprite AccessoryItemSprite(int index)
        {
            return index >= 0 && index < GameModel.AccessoryItemIds.Length
                ? ChoSiren.UI.RewardItemVisuals.SpriteFor(GameModel.AccessoryItemIds[index]) : null;
        }

        /// <summary>Quality colour shared by every accessory surface so a tier always looks the same.</summary>
        protected static Color AccessoryRarityColor(int index)
        {
            Color parsed;
            return ColorUtility.TryParseHtmlString(GameModel.AccessoryRarityColorHexOf(index), out parsed) ? parsed : White;
        }

        private void AddIconButton(Transform parent, string name, string glyph, int x, UnityEngine.Events.UnityAction action)
        {
            GameObject button = NewButton(name, parent, glyph, 23, Color.clear, White, action);
            PlaceTopRight(button.GetComponent<RectTransform>(), x, 17, 40, 48);
        }

        private void AddResourceIcon(Transform parent, string name, string resourcePath,
            float x, float y, float size)
        {
            GameObject icon = NewImage(name, parent, Resources.Load<Sprite>(resourcePath), White);
            PlaceTop(icon.GetComponent<RectTransform>(), x, y, size, size);
            Image image = icon.GetComponent<Image>();
            image.preserveAspect = true;
            image.useSpriteMesh = true;
            image.raycastTarget = false;
        }

        private Text BuildResourcePill(Transform parent, string currency, string iconName, string valueName,
            string iconPath, float x, float width, Color tint)
        {
            GameObject pill = NewButton("Currency-" + currency, parent, string.Empty, 1,
                new Color32(16, 22, 48, 150), White, () => OpenCurrency(currency));
            PlaceTop(pill.GetComponent<RectTransform>(), x, 18, width, 46);
            AddResourceIcon(pill.transform, iconName, iconPath, 10, 10, 25);
            Text value = NewText(valueName, pill.transform, string.Empty, 17, tint, FontStyle.Bold, TextAnchor.MiddleLeft);
            PlaceTop(value.rectTransform, 38, 0, width - 48, 46);
            ConfigureHudNumber(value);
            value.raycastTarget = false;
            ColorBlock feedback = pill.GetComponent<Button>().colors;
            feedback.highlightedColor = new Color(1.3f, 1.3f, 1.4f, 1);
            feedback.pressedColor = new Color(.75f, .8f, .9f, 1);
            feedback.fadeDuration = .12f;
            pill.GetComponent<Button>().colors = feedback;
            return value;
        }

        private static void ConfigureHudNumber(Text text)
        {
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 14;
            text.resizeTextMaxSize = 17;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            AddReadableShadow(text);
        }

        private static void AddReadableShadow(Text text)
        {
            Shadow shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color32(5, 8, 31, 180);
            shadow.effectDistance = new Vector2(1f, -1f);
            shadow.useGraphicAlpha = true;
        }

        private GameObject AddSpriteIconButton(Transform parent, string name, Sprite icon, int right,
            UnityEngine.Events.UnityAction action)
        {
            GameObject button = NewButton(name, parent, string.Empty, 1, Color.clear, White, action);
            PlaceTopRight(button.GetComponent<RectTransform>(), right, 17, 40, 48);
            GameObject iconObject = NewImage("Icon", button.transform, icon, White);
            PlaceTop(iconObject.GetComponent<RectTransform>(), 4, 8, 32, 32);
            Image iconImage = iconObject.GetComponent<Image>();
            iconImage.preserveAspect = true;
            iconImage.useSpriteMesh = true;
            iconImage.raycastTarget = false;
            return button;
        }

        private Sprite StageGlowSprite()
        {
            if (stageGlowSprite != null) return stageGlowSprite;

            const int width = 160;
            const int height = 112;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "Stage-Radial-Glow",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            Color32[] pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float nx = (x + 0.5f - width * 0.5f) / (width * 0.5f);
                    float ny = (y + 0.5f - height * 0.48f) / (height * 0.5f);
                    float distance = Mathf.Sqrt(nx * nx + ny * ny);
                    float edge = Mathf.Clamp01((1f - distance) * 7f);
                    float blend = Mathf.Clamp01(distance);
                    Color inner = new Color32(255, 67, 196, 245);
                    Color outer = new Color32(104, 29, 142, 178);
                    Color color = Color.Lerp(inner, outer, blend);
                    color.a *= edge;
                    pixels[y * width + x] = color;
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            stageGlowSprite = Sprite.Create(texture, new Rect(0, 0, width, height),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            stageGlowSprite.name = texture.name;
            stageGlowSprite.hideFlags = HideFlags.DontSave;
            return stageGlowSprite;
        }

        private Sprite NavIconSprite(int index)
        {
            const int count = 5;
            index = Mathf.Clamp(index, 0, count - 1);
            if (navIconSprites == null) navIconSprites = new Sprite[count];
            if (navIconSprites[index] != null) return navIconSprites[index];

            Sprite sheet = Resources.Load<Sprite>("Art/UI/NavIcons");
            if (sheet == null) return null;
            Rect source = sheet.textureRect;
            float cellWidth = source.width / count;
            Rect cell = new Rect(source.x + cellWidth * index, source.y, cellWidth, source.height);
            Sprite sprite = Sprite.Create(sheet.texture, cell, new Vector2(0.5f, 0.5f), sheet.pixelsPerUnit,
                0, SpriteMeshType.FullRect);
            sprite.name = $"NavIcon-{index}";
            sprite.hideFlags = HideFlags.DontSave;
            navIconSprites[index] = sprite;
            return sprite;
        }

        private Sprite LobbyEmblemSprite(int index)
        {
            const int columns = 3;
            const int rows = 2;
            const int count = columns * rows;
            index = Mathf.Clamp(index, 0, count - 1);
            if (lobbyEmblemSprites == null) lobbyEmblemSprites = new Sprite[count];
            if (lobbyEmblemSprites[index] != null) return lobbyEmblemSprites[index];

            Sprite sheet = Resources.Load<Sprite>("Art/UI/Emblems");
            if (sheet == null) return null;
            Rect source = sheet.textureRect;
            float cellWidth = source.width / columns;
            float cellHeight = source.height / rows;
            int column = index % columns;
            int rowFromBottom = index < columns ? 1 : 0;
            Rect cell = new Rect(source.x + cellWidth * column, source.y + cellHeight * rowFromBottom,
                cellWidth, cellHeight);
            Sprite sprite = Sprite.Create(sheet.texture, cell, new Vector2(0.5f, 0.5f), sheet.pixelsPerUnit,
                0, SpriteMeshType.FullRect);
            sprite.name = $"LobbyEmblem-{index}";
            sprite.hideFlags = HideFlags.DontSave;
            lobbyEmblemSprites[index] = sprite;
            return sprite;
        }

        // There is no producer XP system. Show actual party levels, not a demo account level.
        private int TeamAverageLevel => model.Save.Team.Count == 0 ? 0
            : Mathf.FloorToInt((float)model.Save.Team.Average(index => model.LevelOf(index)));

        private void UpdateTopBar()
        {
            if (diamondText == null) return;
            teamLevelText.text = model.Save.Team.Count == 0 ? "尚未编队" : $"战力 {model.TeamPower:N0}";
            diamondText.text = $"{model.Save.Diamonds:N0}";
            goldText.text = $"{model.Save.Gold:N0}";
            int cap = model.StaminaCap;
            staminaText.text = $"{model.Save.Stamina}/{cap}";
        }

        private void BuildToast()
        {
            toastObject = NewPanel("Toast", safeRoot, new Color32(14, 13, 42, 245), 18);
            RectTransform rect = toastObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0);
            rect.anchorMax = new Vector2(0.5f, 0);
            rect.pivot = new Vector2(0.5f, 0);
            rect.anchoredPosition = new Vector2(0, 132);
            rect.sizeDelta = new Vector2(620, 70);
            toastText = NewText("Message", toastObject.transform, string.Empty, 16, White, FontStyle.Normal, TextAnchor.MiddleCenter);
            Stretch(toastText.rectTransform, 22, 8, -22, -8);
            toastObject.SetActive(false);
        }

        private void BuildStartupLoading()
        {
            startupLoadingObject = NewImage("StartupLoading", safeRoot,
                Resources.Load<Sprite>("Art/LobbyBackground"), White);
            RectTransform rootRect = startupLoadingObject.GetComponent<RectTransform>();
            Stretch(rootRect);
            Image background = startupLoadingObject.GetComponent<Image>();
            background.preserveAspect = false;
            background.raycastTarget = true;
            startupLoadingGroup = startupLoadingObject.AddComponent<CanvasGroup>();
            startupLoadingGroup.alpha = 1f;
            startupLoadingGroup.interactable = false;
            startupLoadingGroup.blocksRaycasts = true;

            GameObject shade = NewImage("Shade", startupLoadingObject.transform, null,
                new Color32(5, 6, 35, 174));
            Stretch(shade.GetComponent<RectTransform>());

            GameObject aura = NewImage("Aura", startupLoadingObject.transform, RoundedSprite(30),
                new Color32(172, 74, 230, 72));
            PlaceTop(aura.GetComponent<RectTransform>(), 105, 360, 510, 510);

            NewPlacedText(startupLoadingObject.transform, "幻域魅声", 18,
                new Color32(255, 173, 231, 255), 60, 390, 600, 34,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            NewPlacedText(startupLoadingObject.transform, "幻域魅声", 52, White,
                60, 434, 600, 82, TextAnchor.MiddleCenter, FontStyle.Bold);
            NewPlacedText(startupLoadingObject.transform, "通往舞台的星途", 14,
                new Color32(211, 196, 239, 255), 60, 520, 600, 30,
                TextAnchor.MiddleCenter, FontStyle.Bold);

            GameObject record = NewPanel("StageRecord", startupLoadingObject.transform,
                new Color32(45, 25, 100, 232), 30);
            PlaceTop(record.GetComponent<RectTransform>(), 250, 610, 220, 220);
            Outline recordOutline = record.AddComponent<Outline>();
            recordOutline.effectColor = new Color32(255, 112, 218, 180);
            recordOutline.effectDistance = new Vector2(2, -2);
            NewPlacedText(record.transform, "♪", 74, Pink, 20, 35, 180, 105,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            NewPlacedText(record.transform, "演出", 28, White, 20, 128, 180, 50,
                TextAnchor.MiddleCenter, FontStyle.Bold);

            GameObject track = NewPanel("ProgressTrack", startupLoadingObject.transform,
                new Color32(39, 32, 89, 232), 12);
            PlaceTop(track.GetComponent<RectTransform>(), 110, 918, 500, 18);
            GameObject fill = NewPanel("ProgressFill", track.transform, Pink, 12);
            RectTransform fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            startupProgressFill = fill.GetComponent<Image>();

            startupProgressText = NewPlacedText(startupLoadingObject.transform,
                "正在载入舞台资源 · 0%", 16, White, 110, 950, 500, 38,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            NewPlacedText(startupLoadingObject.transform, "首次进入会准备角色动画，完成后将自动进入大厅",
                13, Muted, 90, 1002, 540, 40, TextAnchor.MiddleCenter);

            startupLoadingObject.transform.SetAsLastSibling();
            UpdateStartupLoading(0f);
        }

        private void UpdateStartupLoading(float progress)
        {
            if (startupLoadingObject == null || startupFinished) return;
            float clamped = Mathf.Clamp01(progress);
            if (startupProgressFill != null)
                startupProgressFill.rectTransform.anchorMax = new Vector2(clamped, 1f);
            if (startupProgressText != null)
                startupProgressText.text = $"正在载入舞台资源 · {Mathf.RoundToInt(clamped * 100f)}%";
        }

        private void FinishStartupLoading()
        {
            if (startupLoadingObject == null || startupFinished) return;
            UpdateStartupLoading(1f);
            startupFinished = true;

            // The loading screen must stop intercepting input as soon as loading has
            // completed.  The fade is presentation only; it must never be able to
            // leave an invisible click-blocking overlay behind if a component is
            // removed during a scene/test transition.
            if (startupLoadingGroup != null) startupLoadingGroup.blocksRaycasts = false;
            Graphic[] loadingGraphics = startupLoadingObject.GetComponentsInChildren<Graphic>(true);
            for (int index = 0; index < loadingGraphics.Length; index++)
                loadingGraphics[index].raycastTarget = false;

            StartCoroutine(FadeStartupLoading());
        }

        private IEnumerator FadeStartupLoading()
        {
            yield return new WaitForSecondsRealtime(0.18f);
            if (startupLoadingObject == null) yield break;

            GameObject overlay = startupLoadingObject;
            CanvasGroup group = startupLoadingGroup;
            if (group == null)
                group = overlay.GetComponent<CanvasGroup>() ?? overlay.AddComponent<CanvasGroup>();
            const float duration = 0.34f;
            float started = Time.unscaledTime;
            while (overlay != null && Time.unscaledTime - started < duration)
            {
                float progress = Mathf.Clamp01((Time.unscaledTime - started) / duration);
                if (group != null) group.alpha = 1f - progress * progress;
                yield return null;
            }

            if (overlay != null) Destroy(overlay);
            if (startupLoadingObject == overlay) startupLoadingObject = null;
            startupLoadingGroup = null;
        }

        private void Toast(string message)
        {
            if (toastObject == null) return;
            toastText.text = message;
            toastObject.transform.SetAsLastSibling();
            toastObject.SetActive(true);
            toastHideAt = Time.unscaledTime + 3.2f;
        }

        private void CloseModal()
        {
            if (modalObject == null) return;
            modalObject.SetActive(false);
            Destroy(modalObject);
            modalObject = null;
        }

        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            GameObject system = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            DontDestroyOnLoad(system);
        }

        private GameObject NewObject(string name, Transform parent)
        {
            GameObject result = new GameObject(name);
            result.transform.SetParent(parent, false);
            return result;
        }

        private GameObject NewImage(string name, Transform parent, Sprite sprite, Color color)
        {
            GameObject result = NewObject(name, parent);
            RectTransform rect = result.AddComponent<RectTransform>();
            rect.localScale = Vector3.one;
            Image image = result.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return result;
        }

        private GameObject NewPanel(string name, Transform parent, Color color, int radius)
        {
            GameObject result = NewImage(name, parent, RoundedSprite(radius), color);
            Image image = result.GetComponent<Image>();
            image.type = Image.Type.Sliced;
            image.raycastTarget = true;
            return result;
        }

        private GameObject NewButton(string name, Transform parent, string label, int fontSize, Color background,
            Color foreground, UnityEngine.Events.UnityAction action)
        {
            GameObject result = NewPanel(name, parent, background, 16);
            Image image = result.GetComponent<Image>();
            image.raycastTarget = true;
            Button button = result.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.9f, 1f);
            colors.selectedColor = Color.white;
            button.colors = colors;
            button.onClick.AddListener(() =>
            {
                action?.Invoke();
                ResumeMediaAfterUserGesture();
            });

            Text text = NewText("Label", result.transform, label, fontSize, foreground, FontStyle.Bold, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform, 6, 4, -6, -4);
            text.raycastTarget = false;
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
            text.supportRichText = true;
            text.raycastTarget = false;
            return text;
        }

        private Text NewPlacedText(Transform parent, string value, int size, Color color, float x, float y, float width,
            float height, TextAnchor alignment, FontStyle style = FontStyle.Normal)
        {
            Text text = NewText("Text", parent, value, size, color, style, alignment);
            PlaceTop(text.rectTransform, x, y, width, height);
            return text;
        }

        private Sprite RoundedSprite(int radius)
        {
            radius = Mathf.Clamp(radius, 4, 30);
            if (roundedSprites.TryGetValue(radius, out Sprite cached)) return cached;

            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = $"Rounded-{radius}",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            Color32[] pixels = new Color32[size * size];
            float r = radius;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nearestX = Mathf.Clamp(x + 0.5f, r, size - r);
                    float nearestY = Mathf.Clamp(y + 0.5f, r, size - r);
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(nearestX, nearestY));
                    byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(r - distance + 0.5f) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
            sprite.name = texture.name;
            roundedSprites[radius] = sprite;
            return sprite;
        }

        private static void PlaceTop(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void PlaceTopRight(RectTransform rect, float right, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(1, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(1, 1);
            rect.anchoredPosition = new Vector2(-right, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void PlaceTopStretch(RectTransform rect, float y, float height)
        {
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(0.5f, 1);
            rect.anchoredPosition = new Vector2(0, -y);
            rect.sizeDelta = new Vector2(0, height);
        }

        private static void Stretch(RectTransform rect, float left = 0, float bottom = 0, float right = 0, float top = 0)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(right, top);
        }

        private static void ClearChildren(Transform parent)
        {
            for (int index = parent.childCount - 1; index >= 0; index--)
            {
                GameObject child = parent.GetChild(index).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
        }

        private static void DestroyRuntimeSprite(ref Sprite sprite)
        {
            if (sprite != null && (sprite.hideFlags & HideFlags.DontSave) != 0)
                UnityEngine.Object.Destroy(sprite);
            sprite = null;
        }
    }
}
