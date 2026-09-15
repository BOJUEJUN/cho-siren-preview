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
#if UNITY_WEBGL && !UNITY_EDITOR
            StartCoroutine(PreloadReferenceArt038());
#else
            ShowScreen("lobby");
#endif
        }

        private IEnumerator PreloadReferenceArt038()
        {
            string failure = null;
            Transform oldRetry = startupLoadingObject != null ? startupLoadingObject.transform.Find("ReferenceArtRetry") : null;
            if (oldRetry != null) oldRetry.gameObject.SetActive(false);
            if (startupLoadingGroup != null) startupLoadingGroup.interactable = false;
            UpdateStartupLoading(0);
            yield return ReferenceArt038.Preload(UpdateStartupLoading, error => failure = error);
            if (failure == null)
            {
                ShowScreen("lobby");
                yield break;
            }
            if (startupProgressText != null)
            {
                startupProgressText.text = failure;
                PlaceTop(startupProgressText.rectTransform, 80, 943, 560, 65);
            }
            if (startupLoadingGroup != null) startupLoadingGroup.interactable = true;
            if (startupLoadingObject != null)
            {
                if (oldRetry != null) oldRetry.gameObject.SetActive(true);
                else
                {
                    GameObject retry = NewButton("ReferenceArtRetry", startupLoadingObject.transform,
                        "重新载入", 22, Pink, White, () => StartCoroutine(PreloadReferenceArt038()));
                    PlaceTop(retry.GetComponent<RectTransform>(), 235, 1060, 250, 64);
                }
            }
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
            // v2 reference: root [12,1318,696,184] in the 720x1536 screen.
            navRoot.offsetMin = new Vector2(12, 34);
            navRoot.offsetMax = new Vector2(-12, 218);
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
            // 0.3.8 reference: the interaction layer follows the approved 720x1536
            // artwork exactly. Visuals are hidden on the lobby because the golden
            // image already contains the complete header without stretching.
            GameObject bar = NewImage("TopBar", safeRoot, null, Color.clear);
            RectTransform barRect = bar.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0, 1);
            barRect.anchorMax = new Vector2(1, 1);
            barRect.pivot = new Vector2(0.5f, 1);
            barRect.offsetMin = new Vector2(0, -100);
            barRect.offsetMax = Vector2.zero;

            // Profile hit [8,10,185,88]: baked header art plus the realtime avatar,
            // name and power layers on top.
            GameObject profile = NewImage("Profile", bar.transform, null, Color.clear);
            RectTransform profileRect = profile.GetComponent<RectTransform>();
            PlaceTop(profileRect, 7f, 10f, 194f, 90f);
            Image profileHit = profile.GetComponent<Image>();
            profileHit.raycastTarget = true;
            Button profileButton = profile.AddComponent<Button>();
            profileButton.targetGraphic = profileHit;
            profileButton.onClick.AddListener(() =>
            {
                OpenProfile();
                ResumeMediaAfterUserGesture();
                gameAudio?.PlayClick();
            });
            GameObject profileVisual = NewVisualV2("ProfileVisualV2", profile.transform,
                AiUiSprite("Art/LobbyPunk/lobby-header-profile-punk-v2"));
            Stretch(profileVisual.GetComponent<RectTransform>());
            profileVisual.transform.SetAsFirstSibling();

            GameObject avatarMask = NewImage("AvatarMask", profile.transform, RoundedSprite(30), White);
            PlaceTop(avatarMask.GetComponent<RectTransform>(), 12f, 16f, 54f, 54f);
            Mask mask = avatarMask.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            GameObject avatar = NewImage("Avatar", avatarMask.transform,
                AiUiSprite("Art/ProfileAvatarUser") ?? Resources.Load<Sprite>("Art/ProfileAvatar"), White);
            Stretch(avatar.GetComponent<RectTransform>());
            avatar.GetComponent<Image>().preserveAspect = true;

            Text name = NewText("PlayerName", profile.transform, "音律少女", 17, White, FontStyle.Bold, TextAnchor.MiddleLeft);
            PlaceTop(name.rectTransform, 72f, 14f, 106f, 26f);
            AddReadableShadow(name);
            teamLevelText = NewText("PlayerLevel", profile.transform, string.Empty, 13,
                new Color32(225, 215, 242, 255), FontStyle.Bold, TextAnchor.MiddleLeft);
            PlaceTop(teamLevelText.rectTransform, 72f, 44f, 108f, 24f);
            teamLevelText.resizeTextForBestFit = true;
            teamLevelText.resizeTextMinSize = 10;
            teamLevelText.resizeTextMaxSize = 13;
            teamLevelText.horizontalOverflow = HorizontalWrapMode.Overflow;
            AddReadableShadow(teamLevelText);
            AttachLobbyHotspotFeedback(profile, LobbyHotspotFeedback.VisualKind.Profile);

            // Currency pills keep the v1 quiet-glass plate so presses still tint; the
            // baked pill art sits on a non-raycast VisualV2 child. Hit rects are trimmed
            // inside the (overlapping) art so no two currency hit zones touch.
            diamondText = BuildResourcePill(bar.transform, CurrencyIds.Diamond, "DiamondIcon", "Diamonds",
                "Art/LobbyPunk/lobby-resource-diamond-punk-v2", "DiamondVisualV2",
                "Art/UI/ResourceDiamond-C",
                new Rect(209f, 20f, 137f, 53f), new Rect(209f, 20f, 137f, 53f), Cyan);
            goldText = BuildResourcePill(bar.transform, CurrencyIds.Gold, "GoldIcon", "Gold",
                "Art/LobbyPunk/lobby-resource-gold-punk-v2", "GoldVisualV2",
                "Art/UI/ResourceGold-C",
                new Rect(356f, 21f, 141f, 52f), new Rect(356f, 21f, 141f, 52f),
                new Color32(255, 219, 126, 255));
            staminaText = BuildResourcePill(bar.transform, CurrencyIds.Stamina, "StaminaIcon", "Stamina",
                "Art/LobbyPunk/lobby-resource-stamina-punk-v2", "StaminaVisualV2",
                "Art/UI/ResourceStamina-C",
                new Rect(498f, 20f, 140f, 55f), new Rect(498f, 20f, 140f, 55f),
                new Color32(255, 151, 211, 255));

            GameObject settings = NewImage("Settings", bar.transform, null, Color.clear);
            PlaceTop(settings.GetComponent<RectTransform>(), 640f, 7f, 67f, 80f);
            Image settingsHit = settings.GetComponent<Image>();
            settingsHit.raycastTarget = true;
            Button settingsButton = settings.AddComponent<Button>();
            settingsButton.targetGraphic = settingsHit;
            settingsButton.onClick.AddListener(() =>
            {
                OpenSettings();
                ResumeMediaAfterUserGesture();
                gameAudio?.PlayClick();
            });
            GameObject settingsVisual = NewVisualV2("SettingsVisualV2", settings.transform,
                AiUiSprite("Art/LobbyPunk/lobby-settings-punk-v2"));
            PlaceTop(settingsVisual.GetComponent<RectTransform>(), 0f, 0f, 67f, 80f);
            settingsVisual.transform.SetAsFirstSibling();
            AttachLobbyHotspotFeedback(settings, LobbyHotspotFeedback.VisualKind.Settings);

            // The v2 reference top bar has no inbox entry. The button stays built but
            // inactive so the inbox feature code remains wired for future placement.
            GameObject mail = AddSpriteIconButton(bar.transform, "Mail",
                Resources.Load<Sprite>("Art/UI/HudIcons/Mail"), 0, OpenInbox);
            mail.SetActive(false);

            UpdateTopBar();
        }

        private void BuildNavigation()
        {
            ClearChildren(navRoot);
            navHighlights.Clear();
            string[] ids = { "team", "members", "lobby", "accessory", "audition" };
            string[] labels = { "团队", "成员", "大厅", "饰品", "选秀" };
            Rect[] goldenHitRects038 =
            {
                new Rect(18f, 1328f, 132f, 143f),
                new Rect(161f, 1343f, 118f, 128f),
                new Rect(280f, 1337f, 142f, 149f),
                new Rect(424f, 1343f, 125f, 128f),
                new Rect(554f, 1326f, 147f, 148f),
            };
            // Keep visible art placement independent from pointer hit testing. These are
            // the per-item icon/frame/Chinese-label bounds measured from the 0.3.8 lobby.
            // Do not derive them from an equal-width strip or stretch them to another hit grid.
            Rect[] goldenVisualRects038 =
            {
                new Rect(18.375f, 1325.225f, 132f, 143f),
                new Rect(153.92f, 1337.81f, 118f, 128f),
                new Rect(276.55f, 1319.87f, 142f, 149f),
                new Rect(417.55f, 1337.375f, 125f, 128f),
                new Rect(528.815f, 1328.415f, 147f, 148f),
            };
            float[] goldenLabelCenters038 = { 84.63f, 212.67f, 347.28f, 479.27f, 602.05f };
            const float goldenUnderlineBaseline038 = 1473.09f;
            const float goldenUnderlineWidth038 = 87.7f;

            // The 0.3.8 golden is uniformly contained in the full 720x1536 SafeArea.
            // Its five painted destinations are deliberately not an equal-width grid,
            // so lobby input follows their measured screen-space bounds exactly while
            // the transparent navigation parent remains confined to the bottom strip.
            navRoot.anchorMin = new Vector2(0f, 0f);
            navRoot.anchorMax = new Vector2(1f, 0f);
            navRoot.pivot = new Vector2(0.5f, 0f);
            navRoot.offsetMin = new Vector2(12f, 34f);
            navRoot.offsetMax = new Vector2(-12f, 218f);
            BuildSharedNavigationBackdrop038();
            // v2 bakes icon + label into one PNG per destination. The shared visual strip
            // is [17,1329,684,168] on screen, i.e. (5,11,136.8,168) per item inside navRoot.
            string[] navArt =
            {
                "Art/LobbyPunk/lobby-nav-team-punk-v2",
                "Art/LobbyPunk/lobby-nav-member-punk-v2",
                "Art/LobbyPunk/lobby-nav-lobby-punk-v2",
                "Art/LobbyPunk/lobby-nav-accessory-punk-v2",
                "Art/LobbyPunk/lobby-nav-audition-punk-v2",
            };
            string[] navVisualNames =
            {
                "NavTeamVisualV2", "NavMemberVisualV2", "NavLobbyVisualV2",
                "NavAccessoryVisualV2", "NavAuditionVisualV2",
            };

            for (int index = 0; index < ids.Length; index++)
            {
                int captured = index;
                bool selected = currentScreen == ids[index];
                GameObject buttonObject = NewImage($"Nav-{ids[index]}", navRoot, null, Color.clear);
                RectTransform rect = buttonObject.GetComponent<RectTransform>();
                Rect hit = goldenHitRects038[index];
                PlaceTop(rect, hit.x - 12f, hit.y - 1318f, hit.width, hit.height);
                Image buttonGraphic = buttonObject.GetComponent<Image>();
                buttonGraphic.raycastTarget = true;
                Button button = buttonObject.AddComponent<Button>();
                button.targetGraphic = buttonGraphic;
                button.onClick.AddListener(() =>
                {
                    ShowScreen(ids[captured]);
                    ResumeMediaAfterUserGesture();
                });

                Sprite art = NavigationIconOnly038(navArt[index]);
                if (art != null)
                {
                    GameObject visual = NewVisualV2(navVisualNames[index], buttonObject.transform, art);
                    Rect visualBounds = goldenVisualRects038[index];
                    PlaceTop(visual.GetComponent<RectTransform>(),
                        visualBounds.x - hit.x, visualBounds.y - hit.y,
                        visualBounds.width, visualBounds.height - 20f);
                    Image visualImage = visual.GetComponent<Image>();
                    visualImage.preserveAspect = true;
                    visual.transform.SetAsFirstSibling();
                }
                else
                {
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
                }

                // All five pages use the same actual icon, Chinese label and baseline.
                Color labelColor = White;
                Text label = NewText("Label", buttonObject.transform, labels[index], 18,
                    labelColor, FontStyle.Normal, TextAnchor.MiddleCenter);
                PlaceTop(label.rectTransform, goldenLabelCenters038[index] - hit.x - 38,
                    1432f - hit.y, 76, 28);

                GameObject highlight = NewImage("Highlight", buttonObject.transform, null,
                    selected ? Pink : Color.clear);
                highlight.GetComponent<Image>().enabled = true;
                RectTransform highlightRect = highlight.GetComponent<RectTransform>();
                // 3px bar centred on the same baseline as the 8px hover underline.
                PlaceTop(highlightRect, goldenLabelCenters038[index] - hit.x - goldenUnderlineWidth038 * .5f,
                    goldenUnderlineBaseline038 - hit.y - 1.5f, goldenUnderlineWidth038, 3f);
                navHighlights.Add(highlight.GetComponent<Image>());
                AttachLobbyHotspotFeedback(buttonObject, LobbyHotspotFeedback.VisualKind.Navigation);
                LobbyHotspotFeedback navFeedback = buttonObject.GetComponent<LobbyHotspotFeedback>();
                navFeedback.ConfigureNavigationVisual(
                    goldenLabelCenters038[index] - hit.x,
                    goldenUnderlineBaseline038 - hit.y,
                    goldenUnderlineWidth038);
            }
        }

        private readonly System.Collections.Generic.Dictionary<string, Sprite> navigationGlyphs038 =
            new System.Collections.Generic.Dictionary<string, Sprite>();

        private Sprite NavigationIconOnly038(string resource)
        {
            if (navigationGlyphs038.TryGetValue(resource, out Sprite cached)) return cached;
            Sprite source = AiUiSprite(resource);
            if (source == null) return null;
            Rect rect = source.rect;
            // The bottom 64 pixels contain the old baked caption on all five assets; keep the complete
            // transparent icon/frame above it, and draw one readable Chinese label.
            float caption = rect.height * .2f;
            Sprite glyph = Sprite.Create(source.texture,
                new Rect(rect.x, rect.y + caption, rect.width, rect.height - caption),
                new Vector2(.5f, .5f), source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            glyph.hideFlags = HideFlags.DontSave;
            glyph.name = resource + "-icon-038";
            navigationGlyphs038[resource] = glyph;
            return glyph;
        }

        private void ShowScreen(string screen)
        {
            model.RefreshDailyState();
            currentScreen = screen;
            GameObject previousGolden = GameObject.Find("LobbyHomeGolden038");
            if (previousGolden != null) Destroy(previousGolden);
            SetGoldenLobbyShell(screen == "lobby");
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
            ConfigureReferenceHeader038(true);
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
            BuildPunkLobby();
        }

        private void BuildTeam()
        {
            BuildTeamReference();
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

        private void BuildMembers()
        {
            BuildMembersReference();
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
            GameObject portraitFrame = NewImage("PortraitFrame", card.transform, null, Color.clear);
            PlaceTop(portraitFrame.GetComponent<RectTransform>(), 6, 6, 202, 245);
            portraitFrame.AddComponent<RectMask2D>();
            GameObject portrait = NewImage("Portrait", portraitFrame.transform, Resources.Load<Sprite>(member.ResourcePath), White);
            portrait.GetComponent<Image>().preserveAspect = true;
            PanelKit.FrameBustPortrait(portrait.GetComponent<Image>(),
                portraitFrame.GetComponent<RectTransform>(), member.Id);
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


        private void OpenMember(int memberIndex) => OpenTeamMember(memberIndex, -1);
        private void OpenTeamMember(int memberIndex, int teamSlot)
        {
            OpenTeamMemberReference(memberIndex, teamSlot);
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

            GameObject portraitFrame = NewImage("PortraitFrame", panel.transform, null, Color.clear);
            PlaceTop(portraitFrame.GetComponent<RectTransform>(), 28, 28, 204, 288);
            portraitFrame.AddComponent<RectMask2D>();
            GameObject portrait = NewImage("Portrait", portraitFrame.transform,
                Resources.Load<Sprite>(member.ResourcePath), White);
            Image portraitImage = portrait.GetComponent<Image>();
            portraitImage.preserveAspect = true;
            PanelKit.FrameBustPortrait(portraitImage,
                portraitFrame.GetComponent<RectTransform>(), member.Id);

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

            GameObject panel = NewModalPunkPanel("Panel", overlay.transform, 600, 490);
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
            GameObject primary = NewModalPunkButton("Primary", panel.transform, primaryLabel, 18, confirmed, Pink);
            PlaceTop(primary.GetComponent<RectTransform>(), 170, 360, 260, 64);
            StyleModalClose(close);
        }

        private void OpenSettings()
        {
            CloseModal();
            GameObject overlay = NewImage("SettingsModal", safeRoot, null, new Color32(3, 4, 20, 220));
            Stretch(overlay.GetComponent<RectTransform>());
            overlay.GetComponent<Image>().raycastTarget = true;
            modalObject = overlay;

            GameObject panel = NewModalPunkPanel("Panel", overlay.transform, 600, 650);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(600, 650);

            GameObject gear = NewImage("SettingsEmblem", panel.transform,
                Resources.Load<Sprite>("Art/UI/HudIcons/Settings"), new Color32(208, 176, 255, 255));
            PlaceTop(gear.GetComponent<RectTransform>(), 38, 32, 42, 42);
            gear.GetComponent<UnityEngine.UI.Image>().preserveAspect = true;
            NewPlacedText(panel.transform, "游戏设置", 30, White, 96, 28, 380, 48, TextAnchor.MiddleLeft, FontStyle.Bold);
            GameObject close = NewButton("Close", panel.transform, "×", 28, Color.clear, White, CloseModal);
            PlaceTop(close.GetComponent<RectTransform>(), 520, 22, 50, 50);
            StyleModalClose(close);

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

            NewPlacedText(panel.transform, "存档管理", 16, Muted, 45, 404, 330, 28, TextAnchor.MiddleLeft);
            GameObject reset = NewModalPunkButton("Reset", panel.transform, "清除本机存档", 17, () =>
            {
                OpenInfoModal("确认清除本机存档？",
                    "这会永久删除本机的培养、资源和通关记录。\n\n成员回到 1 级，关卡从 1-1 重新开始。\n\n关闭此窗口即可取消。",
                    "确认清除并重新开始", () =>
                {
                    model.Reset();
                    ApplyMusicRouting();
                    ShowScreen("lobby");
                    Toast("已重新开始：成员 1 级，仅开放 1-1");
                });
            }, new Color32(196, 83, 143, 255));
            PlaceTop(reset.GetComponent<RectTransform>(), 40, 445, 520, 62);

            GameObject done = NewModalPunkButton("Done", panel.transform, "完成", 21, CloseModal, Purple);
            PlaceTop(done.GetComponent<RectTransform>(), 160, 551, 280, 62);
        }

        private void SettingsRow(Transform parent, string title, string value, int y, UnityEngine.Events.UnityAction action)
        {
            GameObject row = NewImage($"Setting-{title}", parent, SlashPanelSprite(false), new Color32(29, 18, 55, 245));
            row.GetComponent<UnityEngine.UI.Image>().type = UnityEngine.UI.Image.Type.Sliced;
            PlaceTop(row.GetComponent<RectTransform>(), 40, y, 520, 70);
            NewPlacedText(row.transform, title, 18, White, 20, 12, 220, 45, TextAnchor.MiddleLeft, FontStyle.Bold);
            ReferencePunkFx.Beam(row.transform, "SettingRule", new Vector2(18, 69), new Vector2(496, 69), 1,
                new Color32(153, 97, 228, 72));
            GameObject toggle = NewModalPunkButton("Toggle", row.transform, value, 16, action,
                value == "已关闭" ? Muted : Cyan);
            PlaceTop(toggle.GetComponent<RectTransform>(), 335, 10, 165, 50);
        }

        private GameObject NewModalPunkPanel(string name, Transform parent, float width, float height)
        {
            GameObject panel = NewImage(name, parent, SlashPanelSprite(false), new Color32(151, 96, 218, 235));
            var edge = panel.GetComponent<UnityEngine.UI.Image>();
            edge.type = UnityEngine.UI.Image.Type.Sliced; edge.raycastTarget = true;
            panel.GetComponent<RectTransform>().sizeDelta = new Vector2(width, height);
            GameObject fill = NewImage("ObsidianPlate", panel.transform, SlashPanelSprite(false), new Color32(12, 7, 28, 253));
            fill.GetComponent<UnityEngine.UI.Image>().type = UnityEngine.UI.Image.Type.Sliced;
            Stretch(fill.GetComponent<RectTransform>(), 2, 2, -2, -2);
            GameObject waveform = NewImage("SoundWaveDetail", panel.transform, WaveformSprite(), new Color32(159, 91, 239, 65));
            PlaceTop(waveform.GetComponent<RectTransform>(), width - 258, 27, 148, 44);
            ReferencePunkFx.Beam(panel.transform, "HeaderEdge", new Vector2(35, 93), new Vector2(width - 35, 93), 1.5f,
                new Color32(165, 103, 240, 138));
            ReferencePunkFx.Beam(panel.transform, "TopShard", new Vector2(18, 14), new Vector2(193, 3), 2,
                new Color32(236, 218, 255, 220));
            ReferencePunkFx.Beam(panel.transform, "BottomShard", new Vector2(width - 191, height - 3), new Vector2(width - 16, height - 15), 2,
                new Color32(157, 144, 247, 210));
            return panel;
        }

        private GameObject NewModalPunkButton(string name, Transform parent, string caption, int fontSize,
            UnityEngine.Events.UnityAction action, Color accent)
        {
            GameObject button = NewButton(name, parent, caption, fontSize, new Color32(35, 19, 59, 255), White, action);
            var face = button.GetComponent<UnityEngine.UI.Image>();
            face.sprite = SlashPanelSprite(false); face.type = UnityEngine.UI.Image.Type.Sliced;
            button.GetComponent<UnityEngine.UI.Button>().transition = UnityEngine.UI.Selectable.Transition.None;
            GameObject line = NewImage("ButtonEdge", button.transform, SlashPanelSprite(false), accent);
            line.GetComponent<UnityEngine.UI.Image>().type = UnityEngine.UI.Image.Type.Sliced;
            Stretch(line.GetComponent<RectTransform>());
            line.transform.SetAsFirstSibling();
            GameObject inner = NewImage("ButtonInset", line.transform, SlashPanelSprite(false), new Color32(29, 15, 50, 255));
            inner.GetComponent<UnityEngine.UI.Image>().type = UnityEngine.UI.Image.Type.Sliced;
            Stretch(inner.GetComponent<RectTransform>(), 1.5f, 1.5f, -1.5f, -1.5f);
            button.AddComponent<LobbyHotspotFeedback>().Configure(LobbyHotspotFeedback.VisualKind.Entry);
            return button;
        }

        private void StyleModalClose(GameObject close)
        {
            close.GetComponent<UnityEngine.UI.Image>().sprite = null;
            close.GetComponent<UnityEngine.UI.Button>().transition = UnityEngine.UI.Selectable.Transition.None;
            close.AddComponent<LobbyHotspotFeedback>().Configure(LobbyHotspotFeedback.VisualKind.Settings);
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
            string visualPath, string visualName, string fallbackIconPath,
            Rect hitRect, Rect visualRect, Color tint)
        {
            GameObject pill = NewPanel("Currency-" + currency, parent, Color.clear, 16);
            PlaceTop(pill.GetComponent<RectTransform>(), hitRect.x, hitRect.y, hitRect.width, hitRect.height);
            Image plate = pill.GetComponent<Image>();
            plate.raycastTarget = false;

            Sprite art = AiUiSprite(visualPath);
            GameObject visual = NewVisualV2(visualName, pill.transform, art);
            PlaceTop(visual.GetComponent<RectTransform>(),
                visualRect.x - hitRect.x, visualRect.y - hitRect.y, visualRect.width, visualRect.height);
            visual.transform.SetAsFirstSibling();

            if (art != null)
            {
                // The pill art bakes its icon; the named marker only anchors the value text.
                GameObject marker = NewObject(iconName, pill.transform);
                PlaceTop(marker.AddComponent<RectTransform>(), 12f,
                    (hitRect.height - 32f) * 0.5f, 30f, 32f);
            }
            else
            {
                AddResourceIcon(pill.transform, iconName, fallbackIconPath, 10f,
                    (hitRect.height - 32f) * 0.5f, 32f);
            }

            Text value = NewText(valueName, pill.transform, string.Empty, 17, tint,
                FontStyle.Bold, TextAnchor.MiddleLeft);
            PlaceTop(value.rectTransform, 46f, (hitRect.height - 30f) * 0.5f,
                Mathf.Min(85f, hitRect.width - 56f), 30f);
            ConfigureHudNumber(value);
            value.raycastTarget = false;
            Rect absolutePlus;
            if (currency == CurrencyIds.Diamond)
                absolutePlus = new Rect(306.9f, 36.6f, 36.8f, 42.1f);
            else if (currency == CurrencyIds.Gold)
                absolutePlus = new Rect(456f, 37.5f, 33.3f, 42.1f);
            else
                absolutePlus = new Rect(593.7f, 37.5f, 36.8f, 43f);

            GameObject plus = NewImage("CurrencyPlus-" + currency, parent, null, Color.clear);
            PlaceTop(plus.GetComponent<RectTransform>(), absolutePlus.x,
                absolutePlus.y, absolutePlus.width, absolutePlus.height);
            Image plusHit = plus.GetComponent<Image>();
            plusHit.raycastTarget = true;
            Button plusButton = plus.AddComponent<Button>();
            plusButton.targetGraphic = plusHit;
            plusButton.onClick.AddListener(() =>
            {
                OpenCurrency(currency);
                ResumeMediaAfterUserGesture();
                gameAudio?.PlayClick();
            });
            Sprite plusFeedbackSprite = AiUiSprite(
                "Art/LobbyPunk/038/lobby-resource-plus-" + currency + "-038");
            AttachLobbyHotspotFeedback(plus, LobbyHotspotFeedback.VisualKind.CurrencyPlus,
                plusFeedbackSprite);
            return value;
        }

        private void SetGoldenLobbyShell(bool active)
        {
            GameObject background = GameObject.Find("Background");
            if (background != null) background.SetActive(!active);

            GameObject topBar = GameObject.Find("TopBar");
            if (topBar == null) return;
            foreach (Transform child in topBar.GetComponentsInChildren<Transform>(true))
            {
                if (child == topBar.transform) continue;
                if (child.name.EndsWith("VisualV2") || child.name == "AvatarMask")
                    child.gameObject.SetActive(!active);
            }
            foreach (Text text in topBar.GetComponentsInChildren<Text>(true))
            {
                text.enabled = !active;
            }
            foreach (LobbyHotspotFeedback feedback in
                     topBar.GetComponentsInChildren<LobbyHotspotFeedback>(true))
            {
                feedback.enabled = active;
            }
        }

        /// <summary>Full-bleed baked artwork node. Always raycastTarget=false so the
        /// measured hit areas keep exclusive ownership of UI input.</summary>
        private GameObject NewVisualV2(string name, Transform parent, Sprite sprite)
        {
            GameObject visual = NewImage(name, parent, sprite, White);
            Image image = visual.GetComponent<Image>();
            image.preserveAspect = false;
            image.useSpriteMesh = true;
            image.raycastTarget = false;
            return visual;
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
            diamondText.text = HudAmount038(model.Save.Diamonds);
            goldText.text = HudAmount038(model.Save.Gold);
            int cap = model.StaminaCap;
            staminaText.text = $"{model.Save.Stamina}/{cap}";
            Text average = safeRoot.Find("TopBar/Profile/TeamAverageLevel")?.GetComponent<Text>();
            if (average != null) average.text = "队伍等级 " + TeamAverageLevel;
        }

        private void BuildToast()
        {
            toastObject = NewPanel("Toast", safeRoot, new Color32(14, 13, 42, 245), 18);
            RectTransform rect = toastObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0);
            rect.anchorMax = new Vector2(0.5f, 0);
            rect.pivot = new Vector2(0.5f, 0);
            rect.anchoredPosition = new Vector2(0, 228);
            rect.sizeDelta = new Vector2(620, 70);
            toastObject.GetComponent<Image>().raycastTarget = false;
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
            NewPlacedText(startupLoadingObject.transform, "首次进入会准备界面素材，完成后将自动进入大厅",
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
