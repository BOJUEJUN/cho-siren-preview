using System.Collections.Generic;
using ChoSiren.Panels;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>
    /// Reference-home composition (punk v2). The baked artwork stays on dedicated
    /// *VisualV2 image nodes with raycastTarget=false, so the hit areas remain
    /// independent from the art and the layout can be re-measured without touching input.
    /// All rects are measured in the approved 720x1536 reference and converted into
    /// content space: content top edge sits 104px below the safe-area top, so
    /// contentY = screenY - 104 and the content surface is 720x1290.
    /// </summary>
    public sealed partial class ChoSirenApp
    {
        // 720x1536 reference rects converted to 720x1290 content space (screenY - 104).
        private static readonly Rect LobbyLogoSpec = new Rect(10f, 2f, 350f, 306f);      // [10,106,350,306]
        private static readonly Rect PracticeHitSpec = new Rect(24f, 442f, 273f, 250f);  // hit [24,546,273,250]
        private static readonly Rect PracticeVisualSpec = new Rect(13f, 410f, 309f, 316f); // [13,514,309,316]
        private static readonly Rect AlbumHitSpec = new Rect(23f, 734f, 260f, 168f);     // hit [23,838,260,168]
        private static readonly Rect AlbumVisualSpec = new Rect(15f, 712f, 282f, 216f);  // [15,816,282,216]
        private static readonly Rect TasksHitSpec = new Rect(25f, 922f, 247f, 158f);     // hit [25,1026,247,158]
        private static readonly Rect TasksVisualSpec = new Rect(17f, 896f, 271f, 207f);  // [17,1000,271,207]
        private static readonly Rect StageHitSpec = new Rect(387f, 899f, 317f, 271f);    // hit [387,1003,317,271]
        private static readonly Rect StageVisualSpec = new Rect(365f, 688f, 355f, 577f); // [365,792,355,577]
        private static readonly Rect FaceSafeSpec = new Rect(318f, 80f, 217f, 267f);     // [318,184,217,267]

        private void BuildPunkLobby()
        {
            GameObject heroLayer = NewObject("HeroLayer", contentRoot);
            Stretch(heroLayer.AddComponent<RectTransform>());

            // Regression-only composition guide. It deliberately has no Graphic, so it can
            // never block input or become visible in a player build.
            GameObject faceSafeZone = NewObject("HeroFaceSafeZone", heroLayer.transform);
            RectTransform faceRect = faceSafeZone.AddComponent<RectTransform>();

            GameObject cardLayer = NewObject("LobbyCards", contentRoot);
            RectTransform cardLayerRect = cardLayer.AddComponent<RectTransform>();
            Stretch(cardLayerRect);
            LobbyPunkResponsiveLayout responsiveLayout = cardLayer.AddComponent<LobbyPunkResponsiveLayout>();

            BuildPunkLobbyLogo(cardLayer.transform, responsiveLayout);

            BuildPunkLobbyCard(cardLayer.transform, responsiveLayout, "PracticeRoom", "PracticeVisualV2",
                "练习室", "PRACTICE ROOM",
                "Art/LobbyPunk/lobby-practice-punk-v2", "Art/LobbyPunk/lobby-practice-punk-v1",
                PracticeHitSpec, PracticeVisualSpec, new Rect(8f, 0f, 220f, 150f),
                OpenLobbyPractice);
            BuildPunkLobbyCard(cardLayer.transform, responsiveLayout, "AlbumProduction", "AlbumVisualV2",
                "专辑制作", "ALBUM PRODUCTION",
                "Art/LobbyPunk/lobby-album-punk-v2", "Art/LobbyPunk/lobby-album-punk-v1",
                AlbumHitSpec, AlbumVisualSpec, new Rect(244f, 0f, 220f, 150f),
                () => Toast("专辑制作即将开放"), true);
            BuildPunkLobbyCard(cardLayer.transform, responsiveLayout, "Tasks", "TasksVisualV2",
                "任务", "TASKS",
                "Art/LobbyPunk/lobby-task-punk-v2", "Art/LobbyPunk/lobby-task-punk-v1",
                TasksHitSpec, TasksVisualSpec, new Rect(480f, 0f, 220f, 150f),
                OpenDailyTasks);
            BuildPunkStageCallToAction(cardLayer.transform, responsiveLayout);

            responsiveLayout.Add(faceRect, FaceSafeSpec, new Rect(710f, 0f, 8f, 8f), false);
            responsiveLayout.Configure(cardLayerRect);

            cardLayer.transform.SetAsLastSibling();

            GameObject loadingBadge = NewPanel("HeroLoading", heroLayer.transform,
                new Color32(20, 18, 65, 220), 15);
            RectTransform loadingRect = loadingBadge.GetComponent<RectTransform>();
            PlaceTop(loadingRect, 40f, 1180f, 188f, 34f);
            loadingBadge.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            Text loadingText = NewText("Status", loadingBadge.transform, "舞台资源载入中 · 0%", 12,
                new Color32(232, 217, 250, 255), FontStyle.Bold, TextAnchor.MiddleCenter);
            Stretch(loadingText.rectTransform, 8, 2, -8, -2);

            lobbyVideoPlayer?.SetMusicEnabled(model.Save.MusicEnabled);
            gameAudio?.SetLobbyVideoMusicActive(false);
            lobbyVideoPlayer?.StartLoop(() =>
            {
                ApplyMusicRouting();
                if (loadingText != null) loadingText.text = "舞台资源载入中 · 100%";
                UpdateStartupLoading(1f);
                if (loadingBadge != null) loadingBadge.SetActive(false);
                FinishStartupLoading();
            }, error =>
            {
                gameAudio?.SetLobbyVideoMusicActive(false);
                if (loadingBadge != null) loadingBadge.SetActive(false);
                FinishStartupLoading();
                Debug.LogWarning($"CHO-SIREN lobby video fallback: {error}");
            });
        }

        private void BuildPunkLobbyLogo(Transform parent, LobbyPunkResponsiveLayout layout)
        {
            GameObject logo = NewObject("LobbyLogo", parent);
            RectTransform logoRect = logo.AddComponent<RectTransform>();
            GameObject visual = NewVisualV2("LogoVisualV2", logo.transform,
                AiUiSprite("Art/LobbyPunk/lobby-logo-punk-v2"));
            Stretch(visual.GetComponent<RectTransform>());
            layout.Add(logoRect, LobbyLogoSpec, new Rect(548f, 150f, 160f, 140f), false);
        }

        private void BuildPunkLobbyCard(Transform parent, LobbyPunkResponsiveLayout layout,
            string objectName, string visualName, string title, string english,
            string artworkV2, string artworkV1, Rect hitSpec, Rect visualSpec, Rect fallbackSpec,
            UnityEngine.Events.UnityAction action, bool locked = false)
        {
            GameObject card = NewImage(objectName, parent, null, Color.clear);
            RectTransform rect = card.GetComponent<RectTransform>();
            Image hitArea = card.GetComponent<Image>();
            hitArea.raycastTarget = true;

            Button button = card.AddComponent<Button>();
            button.targetGraphic = hitArea;
            button.onClick.AddListener(() =>
            {
                action?.Invoke();
                ResumeMediaAfterUserGesture();
                gameAudio?.PlayClick();
            });

            bool baked = AiUiSprite(artworkV2) != null;
            GameObject visual = NewVisualV2(visualName, card.transform,
                baked ? AiUiSprite(artworkV2) : AiUiSprite(artworkV1));
            visual.transform.SetAsFirstSibling();

            layout.Add(rect, hitSpec, fallbackSpec, false);
            layout.Add(visual.GetComponent<RectTransform>(),
                new Rect(visualSpec.x - hitSpec.x, visualSpec.y - hitSpec.y,
                    visualSpec.width, visualSpec.height),
                Rect.zero, true);

            // The v2 card bakes its own copy, so the live titles stay as hidden
            // accessibility/test nodes instead of rendering over the artwork.
            Color copyColor = new Color(1f, 1f, 1f, baked ? 0f : 1f);
            Text titleText = NewText("Title", card.transform, title, 25, copyColor,
                FontStyle.Bold, TextAnchor.MiddleCenter);
            AnchorBand(titleText.rectTransform, 0.08f, 0.26f, 0.92f, 0.56f);
            Text englishText = NewText("EnglishTitle", card.transform, english, 15, copyColor,
                FontStyle.Bold, TextAnchor.MiddleCenter);
            AnchorBand(englishText.rectTransform, 0.08f, 0.54f, 0.92f, 0.76f);

            if (!locked) return;
            Text lockText = NewText("LockedState", card.transform, "即将开放", 12, copyColor,
                FontStyle.Bold, TextAnchor.MiddleCenter);
            AnchorBand(lockText.rectTransform, 0.08f, 0.74f, 0.92f, 0.94f);
        }

        private void BuildPunkStageCallToAction(Transform parent, LobbyPunkResponsiveLayout layout)
        {
            const string artworkV2 = "Art/LobbyPunk/lobby-perform-cta-punk-v2";
            GameObject stage = NewImage("LiveOnStage", parent, null, Color.clear);
            RectTransform stageRect = stage.GetComponent<RectTransform>();
            Image hitArea = stage.GetComponent<Image>();
            hitArea.raycastTarget = true;

            Button button = stage.AddComponent<Button>();
            button.targetGraphic = hitArea;
            button.onClick.AddListener(() =>
            {
                OpenLevelMap();
                ResumeMediaAfterUserGesture();
                gameAudio?.PlayClick();
            });

            bool baked = AiUiSprite(artworkV2) != null;
            GameObject visual = NewVisualV2("LiveOnStageVisualV2", stage.transform,
                baked ? AiUiSprite(artworkV2) : AiUiSprite("Art/LobbyPunk/lobby-perform-cta-punk-v1"));
            visual.transform.SetAsFirstSibling();

            layout.Add(stageRect, StageHitSpec, new Rect(8f, 160f, 316f, 180f), false);
            layout.Add(visual.GetComponent<RectTransform>(),
                new Rect(StageVisualSpec.x - StageHitSpec.x, StageVisualSpec.y - StageHitSpec.y,
                    StageVisualSpec.width, StageVisualSpec.height),
                Rect.zero, true);

            Color copyColor = new Color(1f, 1f, 1f, baked ? 0f : 1f);
            Text live = NewText("Title", stage.transform, "开始演出", 36, copyColor,
                FontStyle.Bold, TextAnchor.MiddleCenter);
            AnchorBand(live.rectTransform, 0.06f, 0.42f, 0.94f, 0.64f);
            Text english = NewText("EnglishTitle", stage.transform, "START LIVE", 17, copyColor,
                FontStyle.Bold, TextAnchor.MiddleCenter);
            AnchorBand(english.rectTransform, 0.06f, 0.62f, 0.94f, 0.80f);
            Text ready = NewText("ReadyState", stage.transform, "舞台已就绪", 14, copyColor,
                FontStyle.Bold, TextAnchor.MiddleCenter);
            AnchorBand(ready.rectTransform, 0.06f, 0.78f, 0.94f, 0.92f);
        }

        private void OpenLobbyPractice()
        {
            int memberIndex = model.Save.Team.Count > 0 ? model.Save.Team[0] : -1;
            if (memberIndex < 0 || !model.IsUnlocked(memberIndex))
            {
                Toast("请先在团队中配置一名已签约成员");
                return;
            }

            CloseModal();
            SuspendLobbyMedia();
            MemberPracticePanel.Open(safeRoot, model, memberIndex,
                () => ShowScreen("lobby"), Toast);
        }

        /// <summary>Positions a text node on a proportional band inside its parent so it
        /// always stays contained regardless of the hit area's size.</summary>
        private static void AnchorBand(RectTransform rect, float xMin, float yMin, float xMax, float yMax)
        {
            rect.anchorMin = new Vector2(xMin, yMin);
            rect.anchorMax = new Vector2(xMax, yMax);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }

    /// <summary>
    /// Keeps the approved portrait composition while giving very short/headless surfaces a
    /// compact, non-overlapping fallback. Portrait surfaces keep the measured 720-wide
    /// layout and only compress vertically once the content is shorter than the 1290px
    /// design height, so the CTA hit can never reach the bottom navigation.
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class LobbyPunkResponsiveLayout : MonoBehaviour
    {
        private const float DesignHeight = 1290f;
        private const float PortraitMinHeight = 850f;

        private struct Placement
        {
            public RectTransform rect;
            public Rect spec;
            public Rect fallback;
            public bool stretchInFallback;
        }

        private readonly List<Placement> placements = new List<Placement>();
        private RectTransform root;
        private float appliedHeight = -1f;

        /// <param name="spec">Design-space rect in the rect's parent coordinates.</param>
        /// <param name="fallback">Absolute rect used on very short surfaces.</param>
        /// <param name="stretchInFallback">True for decoration children that fill their entry.</param>
        public void Add(RectTransform rect, Rect spec, Rect fallback, bool stretchInFallback)
        {
            placements.Add(new Placement
            {
                rect = rect,
                spec = spec,
                fallback = fallback,
                stretchInFallback = stretchInFallback,
            });
        }

        public void Configure(RectTransform layoutRoot)
        {
            root = layoutRoot;
            Apply(true);
        }

        private void LateUpdate() => Apply(false);

        private void OnRectTransformDimensionsChange() => Apply(false);

        private void Apply(bool force)
        {
            if (root == null) return;
            float height = root.rect.height;
            if (!force && Mathf.Abs(height - appliedHeight) < 0.5f) return;
            appliedHeight = height;

            bool portrait = height >= PortraitMinHeight;
            float scale = Mathf.Min(1f, height / DesignHeight);
            for (int index = 0; index < placements.Count; index++)
            {
                Placement placement = placements[index];
                if (placement.rect == null) continue;
                if (portrait)
                {
                    // The approved composition is fixed-width: shorter portrait surfaces
                    // only compress the vertical axis, preserving measured widths.
                    PlaceTop(placement.rect, placement.spec.x, placement.spec.y * scale,
                        placement.spec.width, placement.spec.height * scale);
                }
                else if (placement.stretchInFallback)
                {
                    Fill(placement.rect);
                }
                else
                {
                    PlaceTop(placement.rect, placement.fallback.x, placement.fallback.y,
                        placement.fallback.width, placement.fallback.height);
                }
            }
        }

        private static void Fill(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void PlaceTop(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }
    }
}
