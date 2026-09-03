using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ChoSiren.Panels;
using ChoSiren.Systems.Economy;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ChoSiren
{
    public sealed class ChoSirenApp : MonoBehaviour
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
        private int memberRarityFilterIndex;
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

            if ((currentScreen == "members" || currentScreen == "team") && memberResizeRefreshAt >= 0f &&
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
            PlaceTop(avatarFrame.GetComponent<RectTransform>(), 19, 13, 56, 56);
            GameObject avatarMask = NewImage("AvatarMask", avatarFrame.transform, RoundedSprite(30), White);
            Stretch(avatarMask.GetComponent<RectTransform>(), 2, 2, -2, -2);
            Mask mask = avatarMask.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            GameObject avatar = NewImage("Avatar", avatarMask.transform,
                AiUiSprite("Art/ProfileAvatarUser") ?? Resources.Load<Sprite>("Art/ProfileAvatar"), White);
            Stretch(avatar.GetComponent<RectTransform>());
            avatar.GetComponent<Image>().preserveAspect = false;

            Text name = NewText("PlayerName", bar.transform, "音律少女", 19, White, FontStyle.Bold, TextAnchor.UpperLeft);
            PlaceTop(name.rectTransform, 83, 14, 104, 29);
            AddReadableShadow(name);
            Text level = NewText("PlayerLevel", bar.transform, "等级 68", 14,
                new Color32(225, 215, 242, 255), FontStyle.Bold, TextAnchor.UpperLeft);
            PlaceTop(level.rectTransform, 83, 43, 82, 22);
            AddReadableShadow(level);

            GameObject profileHit = NewButton("Profile", bar.transform, string.Empty, 1, Color.clear, Color.clear, OpenProfile);
            PlaceTop(profileHit.GetComponent<RectTransform>(), 12, 8, 176, 70);
            profileHit.transform.SetAsFirstSibling();

            // 资源组以约 120 像素为节拍排布，邮件紧跟体力；音乐设置已归入设置弹窗后，
            // 这里不再保留一个看起来像“缺按钮”的空槽。
            AddResourceIcon(bar.transform, "DiamondIcon", "Art/UI/ResourceDiamond-C", 200, 28, 25);
            diamondText = NewText("Diamonds", bar.transform, string.Empty, 17, Cyan, FontStyle.Bold, TextAnchor.MiddleLeft);
            PlaceTop(diamondText.rectTransform, 228, 18, 86, 44);
            ConfigureHudNumber(diamondText);

            AddResourceIcon(bar.transform, "GoldIcon", "Art/UI/ResourceGold-C", 320, 28, 25);
            goldText = NewText("Gold", bar.transform, string.Empty, 17, new Color32(255, 219, 126, 255), FontStyle.Bold, TextAnchor.MiddleLeft);
            PlaceTop(goldText.rectTransform, 348, 18, 86, 44);
            ConfigureHudNumber(goldText);

            AddResourceIcon(bar.transform, "StaminaIcon", "Art/UI/ResourceStamina-C", 440, 27, 26);
            staminaText = NewText("Stamina", bar.transform, string.Empty, 17, new Color32(255, 151, 211, 255), FontStyle.Bold, TextAnchor.MiddleLeft);
            PlaceTop(staminaText.rectTransform, 468, 18, 80, 44);
            ConfigureHudNumber(staminaText);

            AddSpriteIconButton(bar.transform, "Mail",
                Resources.Load<Sprite>("Art/UI/HudIcons/Mail"), 120, OpenInbox);
            AddSpriteIconButton(bar.transform, "Settings",
                Resources.Load<Sprite>("Art/UI/HudIcons/Settings"), 80, OpenSettings);
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
            LobbyHotspot(cardLayer.transform, "冒险剧本", 2, 0, 676, 276, 244, OpenLevelMap);
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
            GameObject titlePlaque = NewAiDecoration("TeamTitlePlaque", contentRoot,
                "Art/TeamAI/UI/team-title-plaque-ai-v2");
            PlaceTop(titlePlaque.GetComponent<RectTransform>(), 8, 2, 472, 142);
            // The AI plaque has a large treble-clef ornament on its left. Keep all copy inside
            // the clear center field so the artwork and text read as one authored component.
            NewPlacedText(contentRoot, "当前编队", 13, new Color32(255, 174, 225, 255),
                116, 16, 312, 24, TextAnchor.MiddleLeft, FontStyle.Bold);
            NewPlacedText(contentRoot, "星环编队", 30, White,
                108, 39, 312, 46, TextAnchor.MiddleLeft, FontStyle.Bold);
            NewPlacedText(contentRoot, "四重星轨 · 协同舞台阵列", 14, Muted,
                110, 80, 310, 27, TextAnchor.MiddleLeft);
            NewPlacedText(contentRoot, "编队 1", 15, new Color32(185, 222, 255, 255),
                22, 107, 150, 30, TextAnchor.MiddleLeft, FontStyle.Bold);

            int teamCount = Mathf.Min(GameModel.TeamCapacity, model.Save.Team.Count);
            int roleCount = model.Save.Team.Take(teamCount)
                .Where(index => index >= 0 && index < GameModel.Members.Length)
                .Select(index => GameModel.Members[index].Role)
                .Distinct()
                .Count();
            int resonance = teamCount * 2;
            int harmony = roleCount * 2;
            int focus = teamCount > 0 ? 4 : 0;
            int resonanceScore = resonance + harmony + focus;

            GameObject powerCard = NewPanel("TeamPower", contentRoot, new Color32(8, 18, 55, 92), 20);
            PlaceTop(powerCard.GetComponent<RectTransform>(), 492, 16, 208, 130);
            if (!ApplyAiUiSprite(powerCard, "Art/TeamAI/UI/team-power-panel-ai-v2"))
            {
                Outline powerEdge = powerCard.AddComponent<Outline>();
                powerEdge.effectColor = new Color32(104, 224, 255, 118);
                powerEdge.effectDistance = new Vector2(1f, -1f);
            }
            NewPlacedText(powerCard.transform, "总战力", 13, Muted, 14, 10, 180, 22, TextAnchor.MiddleLeft);
            Text teamPower = NewPlacedText(powerCard.transform, model.TeamPower.ToString("N0"), 30, White,
                14, 28, 180, 46, TextAnchor.MiddleLeft, FontStyle.Bold);
            teamPower.horizontalOverflow = HorizontalWrapMode.Overflow;
            teamPower.verticalOverflow = VerticalWrapMode.Overflow;
            teamPower.name = "TeamPowerValue";
            NewPlacedText(powerCard.transform, $"阵容共鸣评分  {resonanceScore}", 14,
                new Color32(112, 242, 255, 255), 14, 74, 180, 24, TextAnchor.MiddleLeft, FontStyle.Bold);
            NewPlacedText(powerCard.transform, $"成员  {teamCount}/{GameModel.TeamCapacity}", 12, Muted,
                14, 99, 180, 20, TextAnchor.MiddleLeft);

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

            GameObject synergyBar = NewPanel("TeamSynergy", contentRoot, new Color32(7, 17, 52, 112), 20);
            PlaceTop(synergyBar.GetComponent<RectTransform>(), 20, synergyY, 680, synergyHeight);
            if (!ApplyAiUiSprite(synergyBar, "Art/TeamAI/UI/team-synergy-panel-ai-v2"))
            {
                Outline synergyEdge = synergyBar.AddComponent<Outline>();
                synergyEdge.effectColor = new Color32(192, 118, 255, 94);
                synergyEdge.effectDistance = new Vector2(1f, -1f);
            }
            NewPlacedText(synergyBar.transform, "组合效果（展示）", 15, new Color32(255, 184, 232, 255),
                18, 9, 128, 24, TextAnchor.MiddleLeft, FontStyle.Bold);
            NewPlacedText(synergyBar.transform, $"星轨配合  {resonance}", 14, White,
                18, 39, 205, 28, TextAnchor.MiddleLeft, FontStyle.Bold);
            NewPlacedText(synergyBar.transform, $"职业和声  {harmony}", 14, White,
                236, 39, 205, 28, TextAnchor.MiddleLeft, FontStyle.Bold);
            NewPlacedText(synergyBar.transform, $"队长聚光  {focus}", 14, White,
                454, 39, 205, 28, TextAnchor.MiddleLeft, FontStyle.Bold);
            NewPlacedText(synergyBar.transform, "阵容评价", 12, new Color32(132, 222, 255, 255),
                544, 11, 116, 22, TextAnchor.MiddleRight, FontStyle.Bold).name = "TeamAttributes";

            GameObject leader = NewButton("ChangeLeader", contentRoot, "更换队长", 16,
                new Color32(22, 35, 82, 178), White, () =>
                {
                    model.RotateTeamLeader(out string message);
                    Toast(message);
                    ShowScreen("team");
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
                Toast("已按战力自动完成编队");
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
                if (hasMember) OpenMember(memberIndex);
                else ShowScreen("members");
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
            NewPlacedText(tag.transform, $"{member.Rarity} · {member.Role}  等级 {model.LevelOf(memberIndex)}",
                11, new Color32(255, 160, 222, 255), 11, 28, size.x - 38f, 18, TextAnchor.MiddleLeft, FontStyle.Bold);
            NewPlacedText(tag.transform, $"战力 {model.PowerOf(memberIndex):N0}", 11,
                new Color32(102, 221, 255, 255), 11, 46, size.x - 38f, 17, TextAnchor.MiddleLeft, FontStyle.Bold);
        }

        private void BuildMembers()
        {
            BuildMemberGalleryBackdrop();
            string[] roleFilters = { string.Empty, "主唱", "舞者", "支援" };
            string[] rarityFilters = { string.Empty, "SSR", "SR", "R" };
            memberRoleFilterIndex = Mathf.Clamp(memberRoleFilterIndex, 0, roleFilters.Length - 1);
            memberRarityFilterIndex = Mathf.Clamp(memberRarityFilterIndex, 0, rarityFilters.Length - 1);
            string roleFilter = roleFilters[memberRoleFilterIndex];
            string rarityFilter = rarityFilters[memberRarityFilterIndex];

            Canvas.ForceUpdateCanvases();
            float contentHeight = Mathf.Max(1f, contentRoot.rect.height);
            int visibleRows = contentHeight >= 938f
                ? MemberRosterPagination.RowsForContentHeight(contentHeight)
                : Mathf.Max(1, Mathf.FloorToInt((contentHeight - 278f) / 220f));
            int dynamicPageSize = MemberRosterPagination.DefaultColumns * visibleRows;

            MemberRosterPage page = MemberRosterPagination.Build(GameModel.Members.Length, memberPageIndex, index =>
            {
                MemberDefinition member = GameModel.Members[index];
                if (memberOwnedOnly && !model.IsUnlocked(index)) return false;
                if (!string.IsNullOrEmpty(roleFilter) && member.Role != roleFilter) return false;
                if (!string.IsNullOrEmpty(rarityFilter) && member.Rarity != rarityFilter) return false;
                return string.IsNullOrEmpty(memberSearchQuery) ||
                       member.Name.IndexOf(memberSearchQuery, StringComparison.OrdinalIgnoreCase) >= 0;
            }, dynamicPageSize);
            memberPageIndex = page.PageIndex;
            int visiblePageNumber = page.PageCount == 0 ? 0 : page.PageIndex + 1;
            ScreenTitle("成员档案", "全部成员",
                $"已拥有 {model.Save.UnlockedMembers.Count}/{GameModel.Members.Length} · 本页 {page.VisibleCount} 名");

            MemberFilterButton("MemberRoleFilter", $"定位：{(string.IsNullOrEmpty(roleFilter) ? "全部" : roleFilter)}",
                20, 108, 196, () =>
                {
                    memberRoleFilterIndex = (memberRoleFilterIndex + 1) % roleFilters.Length;
                    memberPageIndex = 0;
                    ShowScreen("members");
                });
            MemberFilterButton("MemberRarityFilter", $"稀有度：{(string.IsNullOrEmpty(rarityFilter) ? "全部" : rarityFilter)}",
                226, 108, 196, () =>
                {
                    memberRarityFilterIndex = (memberRarityFilterIndex + 1) % rarityFilters.Length;
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

            Color glowColor = member.Rarity == "SSR"
                ? new Color32(255, 191, 82, unlocked ? (byte)76 : (byte)24)
                : new Color32(80, 199, 255, unlocked ? (byte)62 : (byte)20);
            GameObject glow = NewImage("RarityGlow", card.transform, StageGlowSprite(), glowColor);
            PlaceTop(glow.GetComponent<RectTransform>(), 3, 3, width - 6, 148);

            GameObject portrait = NewImage("Portrait", card.transform,
                Resources.Load<Sprite>(member.ThumbnailResourcePath),
                unlocked ? White : new Color(0.68f, 0.68f, 0.78f, 0.72f));
            PlaceTop(portrait.GetComponent<RectTransform>(), 4, 4, width - 8, 145);
            portrait.GetComponent<Image>().preserveAspect = true;
            NewPlacedText(card.transform, $"{member.Rarity} · {member.Role}", 11, unlocked ? Pink : Muted,
                7, 140, width - 14, 20, TextAnchor.MiddleLeft, FontStyle.Bold);
            NewPlacedText(card.transform, member.Name, 17, unlocked ? White : new Color32(222, 215, 238, 255),
                7, 160, width - 14, 27, TextAnchor.MiddleLeft, FontStyle.Bold);
            NewPlacedText(card.transform, unlocked ? $"等级 {model.LevelOf(index)}" : "未签约",
                12, unlocked ? Cyan : Muted, 7, 185, width - 14, 20, TextAnchor.MiddleLeft, FontStyle.Bold);

            Outline edge = card.AddComponent<Outline>();
            edge.effectColor = unlocked
                ? (member.Rarity == "SSR" ? new Color32(255, 190, 86, 178) : new Color32(120, 190, 255, 112))
                : new Color32(95, 111, 165, 58);
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
            NewPlacedText(card.transform, $"{member.Role} · {member.Rarity}", 14, Pink, 14, 282, 186, 25, TextAnchor.MiddleLeft);
            NewPlacedText(card.transform, $"潜力战力\n{member.BasePower + model.LevelOf(memberIndex) * 135:N0}", 15, Cyan,
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
            BuildAccessoryStageBackdrop();
            ScreenTitle("饰品与设置", "星环试衣舱", "选择饰品，实时预览舞台搭配与战力变化");

            if (selectedAccessoryIndex < 0 || selectedAccessoryIndex >= GameModel.AccessoryNames.Length)
            {
                selectedAccessoryIndex = model.Save.EquippedAccessory >= 0
                    ? model.Save.EquippedAccessory
                    : 0;
            }

            int selected = selectedAccessoryIndex;
            int selectedPower = GameModel.AccessoryPower[selected];
            bool selectedEquipped = model.Save.EquippedAccessory == selected;

            NewPlacedText(contentRoot, $"队伍战力  {model.TeamPower:N0}", 17, White,
                470, 34, 230, 36, TextAnchor.MiddleRight, FontStyle.Bold);
            NewPlacedText(contentRoot, selectedEquipped ? "已同步当前装备" : $"装备后 +{selectedPower:N0}", 13,
                selectedEquipped ? new Color32(112, 255, 196, 255) : Pink,
                470, 70, 230, 24, TextAnchor.MiddleRight, FontStyle.Bold);

            BuildAccessoryPreview(selected);
            BuildAccessoryDetail(selected, selectedEquipped);
            BuildAccessoryCollection(selected);
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

            NewPlacedText(preview.transform, "角色佩戴预览", 15, new Color32(255, 181, 230, 255),
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

            NewPlacedText(preview.transform, "切换饰品可立即查看角色佩戴效果", 13, Muted,
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
            NewPlacedText(detail.transform, selected == 2 ? "SR" : "SSR", 19,
                selected == 2 ? Cyan : new Color32(255, 213, 97, 255),
                172, 20, 48, 38, TextAnchor.MiddleRight, FontStyle.Bold);
            NewPlacedText(detail.transform, $"强化 +{12 - selected * 2}", 15, new Color32(255, 202, 102, 255),
                18, 62, 100, 26, TextAnchor.MiddleLeft, FontStyle.Bold);
            NewPlacedText(detail.transform, equipped ? "已装备" : "可装备", 13,
                equipped ? new Color32(112, 255, 196, 255) : Pink,
                106, 66, 48, 26, TextAnchor.MiddleRight, FontStyle.Bold);
            GameObject detailArt = NewImage("AccessoryDetailArt", detail.transform, AccessoryItemSprite(selected), White);
            PlaceTop(detailArt.GetComponent<RectTransform>(), 158, 58, 58, 58);
            detailArt.GetComponent<Image>().preserveAspect = true;
            NewPlacedText(detail.transform, $"组合战力  +{GameModel.AccessoryPower[selected]:N0}", 16, Pink,
                18, 112, 132, 32, TextAnchor.MiddleLeft, FontStyle.Bold);

            GameObject divider = NewImage("DetailDivider", detail.transform, null, new Color32(99, 213, 255, 92));
            PlaceTop(divider.GetComponent<RectTransform>(), 18, 142, 202, 2);
            NewPlacedText(detail.transform, "属性变化", 14, Muted,
                18, 157, 202, 26, TextAnchor.MiddleLeft, FontStyle.Bold);

            string[] names = { "攻击", "暴击", "舞台", "生命" };
            string[] before = { "360", "3.0%", "6.0%", "2.0%" };
            string[] after = { "720", "6.0%", "12.0%", "4.0%" };
            for (int row = 0; row < names.Length; row++)
            {
                float y = 192 + row * 43;
                GameObject reading = NewPanel("AccessoryStat-" + row, detail.transform,
                    new Color32(62, 52, 112, 48), 10);
                PlaceTop(reading.GetComponent<RectTransform>(), 12, y - 5, 202, 34);
                reading.GetComponent<Image>().raycastTarget = false;
                NewPlacedText(detail.transform, names[row], 13, White,
                    18, y, 46, 24, TextAnchor.MiddleLeft, FontStyle.Bold);
                NewPlacedText(detail.transform, before[row], 12, Muted,
                    66, y, 50, 24, TextAnchor.MiddleRight);
                NewPlacedText(detail.transform, "→", 13, Cyan,
                    119, y, 23, 24, TextAnchor.MiddleCenter, FontStyle.Bold);
                NewPlacedText(detail.transform, after[row], 12, new Color32(111, 255, 194, 255),
                    144, y, 74, 24, TextAnchor.MiddleRight, FontStyle.Bold);
            }

            int setPieces = selected + 2;
            NewPlacedText(detail.transform, $"星轨套装  {setPieces}/4", 15, Pink,
                18, 382, 202, 28, TextAnchor.MiddleLeft, FontStyle.Bold);
            GameObject track = NewPanel("AccessorySetTrack", detail.transform, new Color32(63, 55, 113, 220), 12);
            PlaceTop(track.GetComponent<RectTransform>(), 18, 418, 202, 12);
            track.GetComponent<Image>().raycastTarget = false;
            GameObject fill = NewPanel("AccessorySetFill", track.transform,
                new Color32(102, 219, 255, 255), 12);
            PlaceTop(fill.GetComponent<RectTransform>(), 0, 0, 202 * Mathf.Clamp01(setPieces / 4f), 12);
            fill.GetComponent<Image>().raycastTarget = false;
            NewPlacedText(detail.transform, "2件：暴击率 +8.0%", 12, White,
                18, 440, 202, 24, TextAnchor.MiddleLeft);
            NewPlacedText(detail.transform, "4件：技能伤害 +15.0%", 12,
                setPieces >= 4 ? White : Muted,
                18, 470, 202, 24, TextAnchor.MiddleLeft);

            GameObject equip = NewButton("AccessoryEquip", detail.transform, equipped ? "卸下" : "装备", 17,
                equipped ? new Color32(77, 70, 123, 255) : Pink, White, () =>
                {
                    model.EquipAccessory(selected);
                    Toast(model.Save.EquippedAccessory == selected ? "饰品已装备" : "饰品已卸下");
                    ShowScreen("accessory");
            });
            PlaceTop(equip.GetComponent<RectTransform>(), 18, 528, 202, 56);
            ApplyAiUiSprite(equip, "Art/AccessoryAI/UI/accessory-action-pink-ai-v1");

            GameObject settings = NewButton("AccessorySettings", detail.transform, "游戏设置", 15,
                new Color32(45, 52, 105, 220), White, OpenSettings);
            PlaceTop(settings.GetComponent<RectTransform>(), 18, 598, 202, 50);
            ApplyAiUiSprite(settings, "Art/AccessoryAI/UI/accessory-action-blue-ai-v1");
        }

        private void BuildAccessoryCollection(int selected)
        {
            GameObject collection = NewPanel("AccessoryCollection", contentRoot,
                new Color32(8, 15, 50, 44), 24);
            PlaceTop(collection.GetComponent<RectTransform>(), 20, 824, 680, 300);
            if (!ApplyAiUiSprite(collection, "Art/AccessoryAI/UI/accessory-collection-panel-ai-v1"))
            {
                Outline edge = collection.AddComponent<Outline>();
                edge.effectColor = new Color32(255, 91, 194, 52);
                edge.effectDistance = new Vector2(1f, -1f);
            }

            NewPlacedText(collection.transform, "饰品图鉴", 20, White,
                18, 14, 180, 34, TextAnchor.MiddleLeft, FontStyle.Bold);
            NewPlacedText(collection.transform, "已收集 3/6", 13, Muted,
                198, 18, 120, 28, TextAnchor.MiddleLeft);
            NewPlacedText(collection.transform, "选中饰品会同步至上方佩戴预览", 13, Cyan,
                338, 18, 324, 28, TextAnchor.MiddleRight);

            string[] names = { "星轨耳返", "霓虹心链", "月桂舞鞋", "麦克风挂饰", "星辉手环", "舞台冠冕" };
            for (int index = 0; index < names.Length; index++)
            {
                int captured = index;
                bool owned = index < GameModel.AccessoryNames.Length;
                bool active = owned && selected == index;
                GameObject item = NewButton($"AccessoryCollection-{index}", collection.transform, string.Empty, 1,
                    active ? new Color32(74, 39, 126, 122) : new Color32(18, 25, 69, 30), White,
                    () =>
                    {
                        if (!owned)
                        {
                            Toast("该饰品将在后续舞台活动中开放");
                            return;
                        }
                        selectedAccessoryIndex = captured;
                        ShowScreen("accessory");
                    });
                PlaceTop(item.GetComponent<RectTransform>(), 16 + index * 109, 56, 102, 176);
                Outline itemEdge = item.AddComponent<Outline>();
                itemEdge.effectColor = active
                    ? new Color32(255, 88, 198, 230)
                    : new Color32(91, 206, 255, owned ? (byte)105 : (byte)52);
                itemEdge.effectDistance = active ? new Vector2(2f, -2f) : new Vector2(1f, -1f);

                GameObject art = NewImage("Art", item.transform, AccessoryItemSprite(index),
                    owned ? White : new Color32(117, 126, 169, 155));
                PlaceTop(art.GetComponent<RectTransform>(), 8, 8, 86, 92);
                art.GetComponent<Image>().preserveAspect = true;
                NewPlacedText(item.transform, names[index], 12, owned ? White : Muted,
                    5, 105, 92, 34, TextAnchor.MiddleCenter, FontStyle.Bold);
                NewPlacedText(item.transform, owned ? (index == 2 ? "SR" : "SSR") : "待收集", 11,
                    owned ? (index == 2 ? Cyan : new Color32(255, 211, 102, 255)) : Muted,
                    5, 140, 92, 20, TextAnchor.MiddleCenter, FontStyle.Bold);
                if (model.Save.EquippedAccessory == index)
                    NewPlacedText(item.transform, "已装备", 11, new Color32(111, 255, 194, 255),
                        5, 160, 92, 18, TextAnchor.MiddleCenter, FontStyle.Bold);
            }

            NewPlacedText(collection.transform,
                $"当前搭配加成  +{GameModel.AccessoryPower[selected]:N0}    ·    队伍战力  {model.TeamPower:N0}",
                15, White, 18, 248, 644, 34, TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        private void OpenMember(int memberIndex)
        {
            CloseModal();
            MemberDefinition member = GameModel.Members[memberIndex];
            bool unlocked = model.IsUnlocked(memberIndex);
            int level = model.LevelOf(memberIndex);
            int displayPower = unlocked
                ? model.PowerOf(memberIndex)
                : member.BasePower + level * 135;
            MemberDisplayStats(member, memberIndex, out int attack, out int hp, out int critPercent,
                out int speed);
            MemberSkillCopy(member, out string firstSkillName, out string firstSkillEffect,
                out string secondSkillName, out string secondSkillEffect);

            GameObject overlay = NewImage("MemberModal", safeRoot, null, new Color32(3, 4, 20, 220));
            Stretch(overlay.GetComponent<RectTransform>());
            overlay.GetComponent<Image>().raycastTarget = true;
            modalObject = overlay;

            GameObject panel = NewPanel("Panel", overlay.transform, new Color32(29, 23, 76, 250), 28);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(620, 1110);
            GameObject profileArt = NewAiDecoration("MemberProfilePanelArt", panel.transform,
                "Art/MemberAI/UI/member-profile-panel-ai-v1");
            PlaceTop(profileArt.GetComponent<RectTransform>(), 0, 0, 620, 905);

            GameObject portrait = NewImage("Portrait", panel.transform, Resources.Load<Sprite>(member.ResourcePath), White);
            PlaceTop(portrait.GetComponent<RectTransform>(), 24, 52, 292, 390);
            Image portraitImage = portrait.GetComponent<Image>();
            portraitImage.preserveAspect = true;
            portraitImage.useSpriteMesh = true;

            Text ownership = NewPlacedText(panel.transform, unlocked ? "已签约成员" : "尚未签约", 14,
                unlocked ? new Color32(111, 255, 194, 255) : new Color32(255, 185, 218, 255),
                328, 48, 250, 28, TextAnchor.MiddleLeft, FontStyle.Bold);
            ownership.name = "MemberOwnershipStatus";
            NewPlacedText(panel.transform, member.Name, 32, White,
                326, 76, 250, 48, TextAnchor.MiddleLeft, FontStyle.Bold);
            NewPlacedText(panel.transform, $"{member.Rarity} · {member.Role}", 17, Pink,
                328, 122, 248, 30, TextAnchor.MiddleLeft, FontStyle.Bold);
            Text power = NewPlacedText(panel.transform,
                unlocked ? $"等级 {level}  ·  战力 {displayPower:N0}" : $"推荐等级 {level}  ·  潜力战力 {displayPower:N0}",
                16, Cyan, 328, 152, 252, 38, TextAnchor.MiddleLeft, FontStyle.Bold);
            power.name = "MemberPower";

            GameObject statPanel = NewPanel("MemberStatPanel", panel.transform,
                new Color32(12, 23, 67, 215), 20);
            PlaceTop(statPanel.GetComponent<RectTransform>(), 320, 198, 276, 238);
            ApplyAiUiSprite(statPanel, "Art/MemberAI/UI/member-stat-panel-ai-v1");
            NewPlacedText(statPanel.transform, "基础属性", 16, new Color32(255, 183, 229, 255),
                16, 12, 244, 28, TextAnchor.MiddleLeft, FontStyle.Bold);
            AddMemberStat(statPanel.transform, "MemberStatAttack", "攻击", attack.ToString("N0"), 50);
            AddMemberStat(statPanel.transform, "MemberStatHp", "生命", hp.ToString("N0"), 91);
            AddMemberStat(statPanel.transform, "MemberStatCrit", "暴击", critPercent + "%", 132);
            AddMemberStat(statPanel.transform, "MemberStatSpeed", "速度", speed.ToString(), 173);

            GameObject skillPanel = NewPanel("MemberSkillPanel", panel.transform,
                new Color32(18, 22, 70, 222), 22);
            PlaceTop(skillPanel.GetComponent<RectTransform>(), 28, 458, 564, 302);
            ApplyAiUiSprite(skillPanel, "Art/MemberAI/UI/member-skill-panel-ai-v1");
            NewPlacedText(skillPanel.transform, "成员技能", 17, new Color32(255, 184, 230, 255),
                18, 12, 520, 28, TextAnchor.MiddleLeft, FontStyle.Bold);
            Text firstSkill = NewPlacedText(skillPanel.transform, firstSkillName, 17, White,
                20, 50, 520, 28, TextAnchor.MiddleLeft, FontStyle.Bold);
            firstSkill.name = "MemberSkillPrimary";
            NewPlacedText(skillPanel.transform, firstSkillEffect, 14, Muted,
                20, 80, 520, 52, TextAnchor.UpperLeft);
            Text secondSkill = NewPlacedText(skillPanel.transform, secondSkillName, 17, White,
                20, 150, 520, 28, TextAnchor.MiddleLeft, FontStyle.Bold);
            secondSkill.name = "MemberSkillSecondary";
            NewPlacedText(skillPanel.transform, secondSkillEffect, 14, Muted,
                20, 180, 520, 52, TextAnchor.UpperLeft);
            NewPlacedText(skillPanel.transform, MemberTeamBonus(member), 13,
                new Color32(110, 225, 255, 255), 20, 248, 520, 32, TextAnchor.MiddleLeft, FontStyle.Bold);

            GameObject guidePanel = NewPanel("MemberAcquireGuide", panel.transform,
                new Color32(20, 26, 73, 220), 18);
            PlaceTop(guidePanel.GetComponent<RectTransform>(), 28, 780, 564, 128);
            ApplyAiUiSprite(guidePanel, unlocked
                ? "Art/MemberAI/UI/member-stat-panel-ai-v1"
                : "Art/MemberAI/UI/member-locked-panel-ai-v1");
            NewPlacedText(guidePanel.transform, unlocked ? "培养建议" : "获取方式", 15,
                new Color32(255, 188, 231, 255), 18, 12, 520, 26, TextAnchor.MiddleLeft, FontStyle.Bold);
            NewPlacedText(guidePanel.transform,
                unlocked
                    ? $"优先提升{member.Role}核心属性；训练等级、饰品套装与编队协同都会计入战力。"
                    : MemberAcquisitionCopy(member),
                14, White, 18, 42, 520, 66, TextAnchor.UpperLeft);

            if (unlocked)
            {
                GameObject train = NewButton("Train", panel.transform, "训练升级", 18, Pink, White, () =>
                {
                    model.Train(memberIndex, out string message);
                    Toast(message);
                    CloseModal();
                    ShowScreen(currentScreen);
                });
                PlaceTop(train.GetComponent<RectTransform>(), 34, 930, 258, 60);
                ApplyAiUiSprite(train, "Art/MemberAI/UI/member-action-pink-ai-v1");

                GameObject team = NewButton("Team", panel.transform,
                    model.IsInTeam(memberIndex) ? "移出编队" : "加入编队", 18, Purple, White, () =>
                {
                    model.ToggleTeamMember(memberIndex, out string message);
                    Toast(message);
                    CloseModal();
                    ShowScreen(currentScreen);
                });
                PlaceTop(team.GetComponent<RectTransform>(), 328, 930, 258, 60);
                ApplyAiUiSprite(team, "Art/MemberAI/UI/member-action-cyan-ai-v1");
            }
            else
            {
                GameObject acquire = NewButton("AcquireMember", panel.transform, "前往选秀", 18,
                    Pink, White, () =>
                {
                    CloseModal();
                    ShowScreen("audition");
                });
                PlaceTop(acquire.GetComponent<RectTransform>(), 154, 930, 312, 60);
                ApplyAiUiSprite(acquire, "Art/MemberAI/UI/member-action-pink-ai-v1");
            }

            GameObject close = NewButton("Close", panel.transform, "关闭档案", 16,
                new Color32(63, 57, 108, 245), White, CloseModal);
            PlaceTop(close.GetComponent<RectTransform>(), 185, 1012, 250, 56);
        }

        private static void MemberDisplayStats(MemberDefinition member, int index, out int attack, out int hp,
            out int critPercent, out int speed)
        {
            int rarityBonus = member.Rarity == "SSR" ? 24 : member.Rarity == "SR" ? 12 : 0;
            int roleAttack = member.Role == "主唱" ? 38 : member.Role == "舞者" ? 24 : 8;
            int roleHp = member.Role == "支援" ? 260 : member.Role == "舞者" ? 130 : 0;
            attack = 82 + member.BasePower / 92 + rarityBonus + roleAttack;
            hp = 980 + member.BasePower / 7 + roleHp;
            critPercent = 6 + rarityBonus / 3 + (member.Role == "舞者" ? 7 : member.Role == "主唱" ? 3 : 0);
            speed = 88 + index % 9 + (member.Role == "舞者" ? 22 : member.Role == "主唱" ? 12 : 4);
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

        private static void MemberSkillCopy(MemberDefinition member, out string firstName, out string firstEffect,
            out string secondName, out string secondEffect)
        {
            switch (member.Role)
            {
                case "舞者":
                    firstName = "流光连舞";
                    firstEffect = "对十字范围造成伤害，并提高自身下一次行动的暴击率。";
                    secondName = "星澜律动";
                    secondEffect = "连续命中时积累舞步，满层后为全队提升速度。";
                    break;
                case "支援":
                    firstName = "和声守护";
                    firstEffect = "为生命最低的成员回复生命，并附加短时护盾。";
                    secondName = "应援回响";
                    secondEffect = "提高全队攻击并延长正面状态，持续两个行动回合。";
                    break;
                default:
                    firstName = "星声穿透";
                    firstEffect = "对一列目标造成高音伤害，暴击时额外削弱防御。";
                    secondName = "幻域终演";
                    secondEffect = "对全体敌人造成伤害，并依据当前共鸣提高倍率。";
                    break;
            }
        }

        private static string MemberTeamBonus(MemberDefinition member)
        {
            return member.Role switch
            {
                "舞者" => "编队加成 · 全队速度 +6%，连击伤害 +4%",
                "支援" => "编队加成 · 治疗与护盾 +8%，受击伤害 -3%",
                _ => "编队加成 · 全队攻击 +6%，暴击伤害 +4%",
            };
        }

        private static string MemberAcquisitionCopy(MemberDefinition member)
        {
            return member.Rarity == "SSR"
                ? "可在限定签约或常驻签约中获取。签约后即可训练、加入编队并参与舞台战斗。"
                : "可在常驻签约与章节奖励中获取。首次获得后会永久加入成员档案。";
        }

        private void OpenProfile()
        {
            OpenInfoModal("制作人档案",
                $"音律少女  ·  等级 68\n当前编队 {model.Save.Team.Count}/4 人\n组合战力 {model.TeamPower:N0}\n已签约 {model.Save.UnlockedMembers.Count}/{GameModel.Members.Length} 名成员",
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
            string body = $"挂机舞台收益\n已累积金币 {gold:N0}\n已累积星钻 {diamonds:N0}";
            if (preview.Capped) body += "\n收益已达上限，请尽快领取。";
            else if (!canClaim) body += "\n收益还在累积，稍后再来。";

            OpenInfoModal("闪耀舞台", body,
                canClaim ? $"领取 · 金币 {gold:N0} / 星钻 {diamonds:N0}" : "暂无收益",
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
            GachaPanel.OpenEmbedded(contentRoot, model, model, Toast);
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
            LevelMapPanel.Open(safeRoot, model, () => ShowScreen("lobby"), Toast);
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

            float confirmUntil = -1f;
            GameObject reset = NewButton("Reset", panel.transform, "清除本机存档", 16,
                new Color32(108, 49, 89, 255), White, () =>
            {
                if (Time.unscaledTime > confirmUntil)
                {
                    confirmUntil = Time.unscaledTime + 4f;
                    Toast("请在 4 秒内再次点击，确认清除本机存档");
                    return;
                }

                model.Reset();
                ApplyMusicRouting();
                CloseModal();
                ShowScreen("lobby");
                Toast("本机存档已重置");
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
                OpenPerformanceConfirm();
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
            string[] resourcePaths =
            {
                "Art/AccessoryAI/Items/accessory-ear-monitor-ai-v1",
                "Art/AccessoryAI/Items/accessory-heart-necklace-ai-v1",
                "Art/AccessoryAI/Items/accessory-dance-boots-ai-v1",
                "Art/AccessoryAI/Items/accessory-microphone-charm-ai-v1",
                "Art/AccessoryAI/Items/accessory-star-bracelet-ai-v1",
                "Art/AccessoryAI/Items/accessory-stage-crown-ai-v1",
            };
            return index >= 0 && index < resourcePaths.Length ? AiUiSprite(resourcePaths[index]) : null;
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

        private void UpdateTopBar()
        {
            if (diamondText == null) return;
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
