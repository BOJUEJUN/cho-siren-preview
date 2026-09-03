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
        private readonly List<Image> navHighlights = new List<Image>();
        private Sprite[] navIconSprites;
        private Sprite[] lobbyEmblemSprites;
        private Sprite stageGlowSprite;

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
        private int lastStaminaDisplaySecond = -1;

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

            if (currentScreen == "members" && memberResizeRefreshAt >= 0f &&
                Time.unscaledTime >= memberResizeRefreshAt)
            {
                memberResizeRefreshAt = -1f;
                ShowScreen("members");
            }

            if (model == null || staminaText == null) return;
            int second = (int)Time.unscaledTime;
            if (second == lastStaminaDisplaySecond) return;
            lastStaminaDisplaySecond = second;
            model.RefreshDailyState();
            UpdateTopBar();
        }

        private void OnDestroy()
        {
            if (model != null) model.Changed -= UpdateTopBar;
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
            GameObject avatar = NewImage("Avatar", avatarMask.transform, Resources.Load<Sprite>("Art/ProfileAvatar"), White);
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

            AddResourceIcon(bar.transform, "DiamondIcon", "Art/UI/ResourceDiamond-C", 194, 28, 25);
            diamondText = NewText("Diamonds", bar.transform, string.Empty, 17, Cyan, FontStyle.Bold, TextAnchor.MiddleLeft);
            PlaceTop(diamondText.rectTransform, 221, 18, 84, 44);
            ConfigureHudNumber(diamondText);

            AddResourceIcon(bar.transform, "GoldIcon", "Art/UI/ResourceGold-C", 309, 28, 25);
            goldText = NewText("Gold", bar.transform, string.Empty, 17, new Color32(255, 219, 126, 255), FontStyle.Bold, TextAnchor.MiddleLeft);
            PlaceTop(goldText.rectTransform, 336, 18, 78, 44);
            ConfigureHudNumber(goldText);

            AddResourceIcon(bar.transform, "StaminaIcon", "Art/UI/ResourceStamina-C", 419, 27, 26);
            staminaText = NewText("Stamina", bar.transform, string.Empty, 17, new Color32(255, 151, 211, 255), FontStyle.Bold, TextAnchor.MiddleLeft);
            PlaceTop(staminaText.rectTransform, 447, 18, 113, 44);
            ConfigureHudNumber(staminaText);

            AddSpriteIconButton(bar.transform, "Mail",
                Resources.Load<Sprite>("Art/UI/HudIcons/Mail"), 112, OpenInbox);
            AddSpriteIconButton(bar.transform, "Music",
                Resources.Load<Sprite>("Art/UI/HudIcons/Music"), 68, () =>
            {
                model.ToggleMusic();
                ApplyMusicRouting();
                Toast(model.Save.MusicEnabled ? "音乐已开启" : "音乐已关闭");
            });
            GameObject settings = AddSpriteIconButton(bar.transform, "Settings",
                Resources.Load<Sprite>("Art/UI/HudIcons/Settings"), 24, OpenSettings);
            GameObject notice = NewImage("Notice", settings.transform, null, new Color32(255, 79, 157, 255));
            RectTransform noticeRect = notice.GetComponent<RectTransform>();
            noticeRect.anchorMin = noticeRect.anchorMax = new Vector2(1f, 1f);
            noticeRect.pivot = new Vector2(0.5f, 0.5f);
            noticeRect.anchoredPosition = new Vector2(-2f, -3f);
            noticeRect.sizeDelta = new Vector2(8f, 8f);
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
            // Option A: the stage itself is the menu. These three lightweight hotspots keep the
            // full-screen video and character visible instead of covering them with solid cards.
            LobbyHotspot(cardLayer.transform, "闪耀舞台", 1, 26, 350, 208, 146, OpenActivity);
            LobbyHotspot(cardLayer.transform, "冒险剧本", 2, 486, 350, 208, 146, OpenLevelMap);
            LobbyHotspot(cardLayer.transform, "任务", 3, 500, 642, 190, 140, OpenDailyTasks);
            if (model.ClaimableTaskCount > 0)
            {
                Transform dailyCard = cardLayer.transform.Find("任务");
                if (dailyCard != null)
                {
                    GameObject claimDot = NewImage("ClaimableDot", dailyCard, null, new Color32(255, 79, 157, 255));
                    RectTransform dotRect = claimDot.GetComponent<RectTransform>();
                    dotRect.anchorMin = dotRect.anchorMax = new Vector2(1f, 1f);
                    dotRect.pivot = new Vector2(0.5f, 0.5f);
                    dotRect.anchoredPosition = new Vector2(-12f, -12f);
                    dotRect.sizeDelta = new Vector2(14f, 14f);
                }
            }
            NewPlacedText(cardLayer.transform,
                $"今日演出 {Mathf.Min(GameModel.DailyPerformanceGoal, model.Save.DailyPerformances)}/{GameModel.DailyPerformanceGoal}   ·   体力 {model.Save.Stamina}/{model.StaminaCap}",
                12, new Color32(244, 228, 252, 245), 210, 1014, 300, 26,
                TextAnchor.MiddleCenter, FontStyle.Bold);
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
            ScreenTitle("当前编队", "团队成员", "组合战力与当前编队");
            GameObject powerCard = NewPanel("TeamPower", contentRoot, Glass, 20);
            PlaceTop(powerCard.GetComponent<RectTransform>(), 20, 90, 680, 96);
            NewPlacedText(powerCard.transform, "共鸣战力", 20, Muted, 20, 16, 220, 32, TextAnchor.MiddleLeft);
            NewPlacedText(powerCard.transform, model.TeamPower.ToString("N0"), 31, White, 430, 10, 220, 48,
                TextAnchor.MiddleRight, FontStyle.Bold);

            GameObject auto = NewButton("AutoTeam", contentRoot, "自动编队", 17, GlassLight, White, () =>
            {
                model.AutoTeam();
                Toast("已按战力自动完成编队");
                ShowScreen("team");
            });
            PlaceTop(auto.GetComponent<RectTransform>(), 530, 18, 170, 58);

            for (int slot = 0; slot < 4; slot++)
            {
                int x = slot % 2 == 0 ? 20 : 370;
                int y = 210 + (slot / 2) * 346;
                if (slot < model.Save.Team.Count)
                    TeamMemberCard(model.Save.Team[slot], x, y);
                else
                    EmptyTeamCard(x, y);
            }

            NewPlacedText(contentRoot, "点击成员可训练或调整编队；所有变化都会自动存档。", 14, Muted,
                30, 922, 660, 38, TextAnchor.MiddleCenter);
        }

        private void TeamMemberCard(int memberIndex, int x, int y)
        {
            MemberDefinition member = GameModel.Members[memberIndex];
            GameObject card = NewPanel($"Team-{member.Id}", contentRoot, Glass, 20);
            PlaceTop(card.GetComponent<RectTransform>(), x, y, 330, 322);
            Button button = card.AddComponent<Button>();
            button.targetGraphic = card.GetComponent<Image>();
            button.onClick.AddListener(() =>
            {
                OpenMember(memberIndex);
                ResumeMediaAfterUserGesture();
            });

            GameObject portrait = NewImage("Portrait", card.transform, Resources.Load<Sprite>(member.ResourcePath), White);
            PlaceTop(portrait.GetComponent<RectTransform>(), 14, 12, 302, 220);
            portrait.GetComponent<Image>().preserveAspect = true;
            NewPlacedText(card.transform, $"{member.Role} · {member.Rarity}", 13, Pink, 16, 225, 160, 25, TextAnchor.MiddleLeft);
            NewPlacedText(card.transform, member.Name, 24, White, 16, 252, 160, 32, TextAnchor.MiddleLeft, FontStyle.Bold);
            NewPlacedText(card.transform, $"等级 {model.LevelOf(memberIndex)}", 18, Muted, 180, 255, 130, 28, TextAnchor.MiddleRight, FontStyle.Bold);
            NewPlacedText(card.transform, $"战力 {model.PowerOf(memberIndex):N0}", 14, Cyan, 16, 288, 294, 24, TextAnchor.MiddleLeft);
        }

        private void EmptyTeamCard(int x, int y)
        {
            GameObject card = NewPanel("EmptySlot", contentRoot, new Color32(28, 26, 78, 150), 20);
            PlaceTop(card.GetComponent<RectTransform>(), x, y, 330, 322);
            Text plus = NewText("Plus", card.transform, "+", 54, Muted, FontStyle.Normal, TextAnchor.MiddleCenter);
            Stretch(plus.rectTransform, 0, 0, 0, -42);
            Text label = NewText("Label", card.transform, "空闲位置\n从成员列表加入", 16, Muted, FontStyle.Normal, TextAnchor.MiddleCenter);
            PlaceTop(label.rectTransform, 30, 200, 270, 70);
            Button button = card.AddComponent<Button>();
            button.targetGraphic = card.GetComponent<Image>();
            button.onClick.AddListener(() =>
            {
                ShowScreen("members");
                ResumeMediaAfterUserGesture();
            });
        }

        private void BuildMembers()
        {
            string[] roleFilters = { string.Empty, "主唱", "舞者", "支援" };
            string[] rarityFilters = { string.Empty, "SSR", "SR", "R" };
            memberRoleFilterIndex = Mathf.Clamp(memberRoleFilterIndex, 0, roleFilters.Length - 1);
            memberRarityFilterIndex = Mathf.Clamp(memberRarityFilterIndex, 0, rarityFilters.Length - 1);
            string roleFilter = roleFilters[memberRoleFilterIndex];
            string rarityFilter = rarityFilters[memberRarityFilterIndex];

            Canvas.ForceUpdateCanvases();
            float contentHeight = Mathf.Max(960f, contentRoot.rect.height);
            int visibleRows = MemberRosterPagination.RowsForContentHeight(contentHeight);
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
                page.HasPrevious ? Purple : new Color32(70, 65, 102, 180), White, () =>
                {
                    memberPageIndex = MemberRosterPagination.MovePage(memberPageIndex, -1, page.PageCount);
                    ShowScreen("members");
                });
            PlaceTop(previous.GetComponent<RectTransform>(), 164, pagerY, 150, 50);
            previous.GetComponent<Button>().interactable = page.HasPrevious;

            NewPlacedText(contentRoot, $"{visiblePageNumber} / {page.PageCount}", 17, White,
                315, pagerY, 90, 50, TextAnchor.MiddleCenter, FontStyle.Bold);

            GameObject next = NewButton("MemberNextPage", contentRoot, "下一页", 16,
                page.HasNext ? Purple : new Color32(70, 65, 102, 180), White, () =>
                {
                    memberPageIndex = MemberRosterPagination.MovePage(memberPageIndex, 1, page.PageCount);
                    ShowScreen("members");
                });
            PlaceTop(next.GetComponent<RectTransform>(), 406, pagerY, 150, 50);
            next.GetComponent<Button>().interactable = page.HasNext;
        }

        private void MemberFilterButton(string name, string label, int x, int y, int width,
            UnityEngine.Events.UnityAction action, bool selected = false)
        {
            GameObject button = NewButton(name, contentRoot, label, 14,
                selected ? Purple : new Color32(30, 27, 78, 224), selected ? White : Muted, action);
            PlaceTop(button.GetComponent<RectTransform>(), x, y, width, 44);
            Outline outline = button.AddComponent<Outline>();
            outline.effectColor = selected ? new Color32(255, 109, 212, 210) : new Color32(113, 174, 255, 100);
            outline.effectDistance = new Vector2(1f, -1f);
        }

        private void BuildMemberSearchBox()
        {
            GameObject box = NewPanel("MemberSearch", contentRoot, new Color32(16, 18, 58, 226), 16);
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
            Color cardColor = unlocked ? new Color32(31, 28, 82, 232) : new Color32(16, 19, 55, 220);
            GameObject card = NewPanel($"Member-{member.Id}", contentRoot, cardColor, 18);
            PlaceTop(card.GetComponent<RectTransform>(), x, y, width, height);
            Button button = card.AddComponent<Button>();
            button.targetGraphic = card.GetComponent<Image>();
            button.onClick.AddListener(() =>
            {
                if (unlocked) OpenMember(index);
                else Toast("尚未签约：前往女团选秀查看候选人");
                ResumeMediaAfterUserGesture();
            });

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
                ? (member.Rarity == "SSR" ? new Color32(255, 190, 86, 190) : new Color32(120, 190, 255, 130))
                : new Color32(95, 111, 165, 80);
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

        private void BuildAccessoryPreview(int selected)
        {
            GameObject preview = NewPanel("AccessoryPreview", contentRoot, new Color32(10, 18, 58, 145), 28);
            PlaceTop(preview.GetComponent<RectTransform>(), 20, 112, 430, 686);
            Outline previewEdge = preview.AddComponent<Outline>();
            previewEdge.effectColor = new Color32(112, 208, 255, 110);
            previewEdge.effectDistance = new Vector2(1.2f, -1.2f);

            GameObject auraOuter = NewImage("PreviewAuraOuter", preview.transform, RoundedSprite(30),
                new Color32(112, 69, 224, 54));
            PlaceTop(auraOuter.GetComponent<RectTransform>(), 55, 118, 320, 410);
            GameObject auraInner = NewImage("PreviewAuraInner", preview.transform, RoundedSprite(30),
                new Color32(255, 91, 193, 38));
            PlaceTop(auraInner.GetComponent<RectTransform>(), 84, 160, 262, 330);

            NewPlacedText(preview.transform, "角色佩戴预览", 15, new Color32(255, 181, 230, 255),
                130, 18, 170, 30, TextAnchor.MiddleCenter, FontStyle.Bold);

            GameObject character = NewImage("AccessoryPreviewCharacter", preview.transform,
                Resources.Load<Sprite>("Art/HeroFallback"), White);
            PlaceTop(character.GetComponent<RectTransform>(), 46, 74, 338, 548);
            Image characterImage = character.GetComponent<Image>();
            characterImage.preserveAspect = true;
            characterImage.useSpriteMesh = true;

            GameObject wornGlow = NewPanel("WornAccessoryGlow", preview.transform,
                new Color32(255, 76, 195, 90), 30);
            PlaceTop(wornGlow.GetComponent<RectTransform>(), 268, 126, 86, 86);
            wornGlow.GetComponent<Image>().raycastTarget = false;
            Outline wornEdge = wornGlow.AddComponent<Outline>();
            wornEdge.effectColor = new Color32(124, 226, 255, 220);
            wornEdge.effectDistance = new Vector2(2f, -2f);
            GameObject wornArt = NewImage("WornAccessory", wornGlow.transform,
                LobbyEmblemSprite(selected), White);
            PlaceTop(wornArt.GetComponent<RectTransform>(), 8, 8, 70, 70);
            wornArt.GetComponent<Image>().preserveAspect = true;

            string[] slotNames = { "耳返", "心链", "舞鞋", "挂饰", "手环", "冠冕" };
            Vector2[] slotPositions =
            {
                new Vector2(12, 78), new Vector2(326, 78),
                new Vector2(4, 260), new Vector2(334, 260),
                new Vector2(12, 448), new Vector2(326, 448),
            };
            for (int index = 0; index < slotPositions.Length; index++)
            {
                int captured = index;
                bool selectable = index < GameModel.AccessoryNames.Length;
                bool active = selectable && index == selected;
                bool equipped = selectable && model.Save.EquippedAccessory == index;
                GameObject slot = NewButton(selectable ? $"Accessory-{index}" : $"AccessorySlot-{index}",
                    preview.transform, string.Empty, 1,
                    active ? new Color32(86, 43, 139, 225) : new Color32(10, 20, 60, 205), White,
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
                PlaceTop(slot.GetComponent<RectTransform>(), slotPositions[index].x, slotPositions[index].y, 92, 112);
                Outline slotEdge = slot.AddComponent<Outline>();
                slotEdge.effectColor = active
                    ? new Color32(255, 95, 201, 245)
                    : new Color32(96, 211, 255, 132);
                slotEdge.effectDistance = active ? new Vector2(2f, -2f) : new Vector2(1f, -1f);

                GameObject art = NewImage("Art", slot.transform, LobbyEmblemSprite(index),
                    selectable ? White : new Color32(176, 184, 224, 190));
                PlaceTop(art.GetComponent<RectTransform>(), 10, 5, 72, 72);
                art.GetComponent<Image>().preserveAspect = true;
                NewPlacedText(slot.transform, slotNames[index], 13, active ? White : Muted,
                    4, 78, 84, 22, TextAnchor.MiddleCenter, FontStyle.Bold);
                if (equipped)
                    NewPlacedText(slot.transform, "已装备", 11, new Color32(111, 255, 194, 255),
                        4, 96, 84, 15, TextAnchor.MiddleCenter, FontStyle.Bold);
            }

            NewPlacedText(preview.transform, "切换饰品可立即查看角色佩戴效果", 13, Muted,
                80, 637, 270, 28, TextAnchor.MiddleCenter);
        }

        private void BuildAccessoryDetail(int selected, bool equipped)
        {
            GameObject detail = NewPanel("AccessoryDetail", contentRoot, new Color32(9, 17, 55, 224), 24);
            PlaceTop(detail.GetComponent<RectTransform>(), 462, 112, 238, 686);
            Outline edge = detail.AddComponent<Outline>();
            edge.effectColor = new Color32(94, 211, 255, 158);
            edge.effectDistance = new Vector2(1.2f, -1.2f);

            NewPlacedText(detail.transform, GameModel.AccessoryNames[selected], 22, White,
                18, 20, 154, 38, TextAnchor.MiddleLeft, FontStyle.Bold);
            NewPlacedText(detail.transform, selected == 2 ? "SR" : "SSR", 19,
                selected == 2 ? Cyan : new Color32(255, 213, 97, 255),
                172, 20, 48, 38, TextAnchor.MiddleRight, FontStyle.Bold);
            NewPlacedText(detail.transform, $"强化 +{12 - selected * 2}", 15, new Color32(255, 202, 102, 255),
                18, 62, 100, 26, TextAnchor.MiddleLeft, FontStyle.Bold);
            NewPlacedText(detail.transform, equipped ? "已装备" : "可装备", 13,
                equipped ? new Color32(112, 255, 196, 255) : Pink,
                126, 62, 94, 26, TextAnchor.MiddleRight, FontStyle.Bold);
            NewPlacedText(detail.transform, $"组合战力  +{GameModel.AccessoryPower[selected]:N0}", 16, Pink,
                18, 100, 202, 32, TextAnchor.MiddleLeft, FontStyle.Bold);

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

            GameObject settings = NewButton("AccessorySettings", detail.transform, "游戏设置", 15,
                new Color32(45, 52, 105, 220), White, OpenSettings);
            PlaceTop(settings.GetComponent<RectTransform>(), 18, 598, 202, 50);
        }

        private void BuildAccessoryCollection(int selected)
        {
            GameObject collection = NewPanel("AccessoryCollection", contentRoot,
                new Color32(8, 15, 50, 150), 24);
            PlaceTop(collection.GetComponent<RectTransform>(), 20, 816, 680, 318);
            Outline edge = collection.AddComponent<Outline>();
            edge.effectColor = new Color32(255, 91, 194, 92);
            edge.effectDistance = new Vector2(1f, -1f);

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
                    active ? new Color32(74, 39, 126, 225) : new Color32(18, 25, 69, 210), White,
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
                PlaceTop(item.GetComponent<RectTransform>(), 16 + index * 109, 58, 102, 182);
                Outline itemEdge = item.AddComponent<Outline>();
                itemEdge.effectColor = active
                    ? new Color32(255, 88, 198, 230)
                    : new Color32(91, 206, 255, owned ? (byte)105 : (byte)52);
                itemEdge.effectDistance = active ? new Vector2(2f, -2f) : new Vector2(1f, -1f);

                GameObject art = NewImage("Art", item.transform, LobbyEmblemSprite(index),
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
                15, White, 18, 262, 644, 34, TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        private void OpenMember(int memberIndex)
        {
            CloseModal();
            MemberDefinition member = GameModel.Members[memberIndex];
            GameObject overlay = NewImage("MemberModal", safeRoot, null, new Color32(3, 4, 20, 220));
            Stretch(overlay.GetComponent<RectTransform>());
            overlay.GetComponent<Image>().raycastTarget = true;
            modalObject = overlay;

            GameObject panel = NewPanel("Panel", overlay.transform, new Color32(29, 23, 76, 250), 28);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(620, 850);

            GameObject portrait = NewImage("Portrait", panel.transform, Resources.Load<Sprite>(member.ResourcePath), White);
            PlaceTop(portrait.GetComponent<RectTransform>(), 100, 24, 420, 470);
            portrait.GetComponent<Image>().preserveAspect = true;
            NewPlacedText(panel.transform, member.Name, 34, White, 38, 480, 360, 48, TextAnchor.MiddleLeft, FontStyle.Bold);
            NewPlacedText(panel.transform, $"{member.Role} · {member.Rarity}", 17, Pink, 400, 489, 175, 34, TextAnchor.MiddleRight);
            NewPlacedText(panel.transform, $"等级 {model.LevelOf(memberIndex)}    战力 {model.PowerOf(memberIndex):N0}", 21, Cyan,
                38, 540, 544, 42, TextAnchor.MiddleLeft, FontStyle.Bold);
            NewPlacedText(panel.transform, "舞台表现、训练等级和装备共同决定组合战力。", 16, Muted,
                38, 590, 544, 50, TextAnchor.MiddleLeft);

            GameObject train = NewButton("Train", panel.transform, "训练升级", 18, Pink, White, () =>
            {
                model.Train(memberIndex, out string message);
                Toast(message);
                CloseModal();
                ShowScreen(currentScreen);
            });
            PlaceTop(train.GetComponent<RectTransform>(), 38, 660, 250, 62);

            GameObject team = NewButton("Team", panel.transform, model.IsInTeam(memberIndex) ? "移出编队" : "加入编队",
                18, Purple, White, () =>
            {
                model.ToggleTeamMember(memberIndex, out string message);
                Toast(message);
                CloseModal();
                ShowScreen(currentScreen);
            });
            PlaceTop(team.GetComponent<RectTransform>(), 332, 660, 250, 62);

            GameObject close = NewButton("Close", panel.transform, "关闭", 17, new Color32(69, 60, 107, 255), White, CloseModal);
            PlaceTop(close.GetComponent<RectTransform>(), 185, 756, 250, 58);
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
            GachaPanel.Open(safeRoot, model, model, () => ShowScreen("lobby"), Toast);
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

            GameObject glow = NewImage("Glow", hotspot.transform, StageGlowSprite(),
                new Color32(154, 82, 255, 155));
            PlaceTop(glow.GetComponent<RectTransform>(), 12, 2, width - 24, height - 18);
            glow.GetComponent<Image>().raycastTarget = false;

            float emblemSize = Mathf.Min(106f, height - 28f);
            GameObject emblem = NewImage("Emblem", hotspot.transform, LobbyEmblemSprite(emblemIndex), White);
            PlaceTop(emblem.GetComponent<RectTransform>(), (width - emblemSize) * 0.5f, -4,
                emblemSize, emblemSize);
            Image emblemImage = emblem.GetComponent<Image>();
            emblemImage.preserveAspect = true;
            emblemImage.useSpriteMesh = true;
            emblemImage.raycastTarget = false;

            GameObject label = NewPanel("HotspotLabel", hotspot.transform, new Color32(15, 13, 54, 172), 16);
            PlaceTop(label.GetComponent<RectTransform>(), 12, height - 45, width - 24, 40);
            label.GetComponent<Image>().raycastTarget = false;
            Outline outline = label.AddComponent<Outline>();
            outline.effectColor = new Color32(147, 213, 255, 150);
            outline.effectDistance = new Vector2(1f, -1f);
            Text labelText = NewText("Label", label.transform, title, 18, White, FontStyle.Bold,
                TextAnchor.MiddleCenter);
            Stretch(labelText.rectTransform, 6, 2, -6, -2);
            AddReadableShadow(labelText);
        }

        private void BuildStageCallToAction(Transform parent)
        {
            GameObject stage = NewImage("LiveOnStage", parent, null, Color.clear);
            PlaceTop(stage.GetComponent<RectTransform>(), 205, 1044, 310, 221);
            Image hitImage = stage.GetComponent<Image>();
            hitImage.raycastTarget = true;
            Button button = stage.AddComponent<Button>();
            button.targetGraphic = hitImage;
            button.onClick.AddListener(() =>
            {
                OpenPerformanceConfirm();
                ResumeMediaAfterUserGesture();
            });

            GameObject glow = NewImage("StageGlow", stage.transform, StageGlowSprite(), White);
            PlaceTop(glow.GetComponent<RectTransform>(), -4, 8, 313, 205);
            glow.GetComponent<Image>().raycastTarget = false;

            GameObject frame = NewImage("StageFrame", stage.transform, LobbyEmblemSprite(5),
                White);
            PlaceTop(frame.GetComponent<RectTransform>(), -43, -42, 390, 305);
            Image frameImage = frame.GetComponent<Image>();
            frameImage.preserveAspect = true;
            frameImage.useSpriteMesh = true;
            glow.transform.SetAsFirstSibling();

            NewPlacedText(stage.transform, "♪   ♫", 21, new Color32(255, 226, 250, 255),
                61, 14, 197, 30, TextAnchor.MiddleCenter, FontStyle.Bold);
            Text live = NewPlacedText(stage.transform, "开始演出", 40, White, 49, 34, 221, 82,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            live.fontStyle = FontStyle.Bold;
            NewPlacedText(stage.transform, "舞台已就绪", 17, new Color32(255, 231, 249, 255),
                63, 108, 193, 28, TextAnchor.MiddleCenter, FontStyle.Bold);
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
            if (model.Save.Stamina >= cap)
            {
                staminaText.text = $"{model.Save.Stamina}/{cap}";
                return;
            }

            long seconds = Math.Max(0L, model.SecondsUntilNextStamina);
            int minutes = (int)(seconds / 60L);
            int remain = (int)(seconds % 60L);
            staminaText.text = $"{model.Save.Stamina}/{cap} {minutes:00}:{remain:00}";
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
    }
}
