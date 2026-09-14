using System.Collections.Generic;
using ChoSiren.Panels;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>
    /// 0.3.8 reference-home composition. One approved, aspect-safe golden owns all
    /// pixels; transparent buttons preserve the existing routes without redrawing text.
    /// </summary>
    public sealed partial class ChoSirenApp
    {
        // 720x1536 reference rects converted to 720x1290 content space (screenY - 104).
        private static readonly Rect LobbyLogoSpec = new Rect(12f, 3f, 412f, 297f);       // [12,107,412,297]
        private static readonly Rect PracticeHitSpec = new Rect(12f, 402f, 283f, 251f);   // [12,506,283,251]
        private static readonly Rect AlbumHitSpec = new Rect(16f, 654f, 274f, 245f);      // [16,758,274,245]
        private static readonly Rect TasksHitSpec = new Rect(19f, 899f, 265f, 196f);      // [19,1003,265,196]
        private static readonly Rect StageHitSpec = new Rect(354f, 730f, 365f, 464f);     // [354,834,365,464]
        private static readonly Rect StageVisualSpec = StageHitSpec; // Legacy helper remains compile-safe.
        private static readonly Rect FaceSafeSpec = new Rect(318f, 80f, 217f, 267f);     // [318,184,217,267]

        private void BuildPunkLobby()
        {
            GameObject heroLayer = NewObject("HeroLayer", contentRoot);
            Stretch(heroLayer.AddComponent<RectTransform>());

            GameObject golden = NewImage("LobbyHomeGolden038", safeRoot,
                AiUiSprite("Art/LobbyPunk/038/lobby-home-base-038"), White);
            RectTransform goldenRect = golden.GetComponent<RectTransform>();
            Stretch(goldenRect);
            Image goldenImage = golden.GetComponent<Image>();
            goldenImage.preserveAspect = true;
            goldenImage.useSpriteMesh = false;
            goldenImage.raycastTarget = false;

            // Regression-only composition guide. It deliberately has no Graphic, so it can
            // never block input or become visible in a player build.
            GameObject faceSafeZone = NewObject("HeroFaceSafeZone", heroLayer.transform);
            RectTransform faceRect = faceSafeZone.AddComponent<RectTransform>();

            GameObject cardLayer = NewObject("LobbyCards", contentRoot);
            RectTransform cardLayerRect = cardLayer.AddComponent<RectTransform>();
            Stretch(cardLayerRect);
            LobbyPunkResponsiveLayout responsiveLayout = cardLayer.AddComponent<LobbyPunkResponsiveLayout>();

            GameObject logo = NewObject("LobbyLogo", cardLayer.transform);
            RectTransform logoRect = logo.AddComponent<RectTransform>();
            responsiveLayout.Add(logoRect, LobbyLogoSpec, LobbyLogoSpec, false);

            Build038LobbyHotspot(cardLayer.transform, responsiveLayout, "PracticeRoom",
                PracticeHitSpec, new Rect(8f, 0f, 220f, 150f), OpenLobbyPractice);
            Build038LobbyHotspot(cardLayer.transform, responsiveLayout, "AlbumProduction",
                AlbumHitSpec, new Rect(244f, 0f, 220f, 150f), () => Toast("专辑制作即将开放"));
            GameObject albumLockState = NewObject("LockedState", cardLayer.transform.Find("AlbumProduction"));
            albumLockState.AddComponent<RectTransform>();
            Build038LobbyHotspot(cardLayer.transform, responsiveLayout, "Tasks",
                TasksHitSpec, new Rect(480f, 0f, 220f, 150f), OpenDailyTasks);
            Build038LobbyHotspot(cardLayer.transform, responsiveLayout, "LiveOnStage",
                StageHitSpec, new Rect(8f, 160f, 316f, 180f), OpenLevelMap);

            responsiveLayout.Add(faceRect, FaceSafeSpec, new Rect(710f, 0f, 8f, 8f), false);
            responsiveLayout.Configure(cardLayerRect);

            golden.transform.SetAsFirstSibling();
            cardLayer.transform.SetAsLastSibling();

            if (lobbyVideoObject != null) lobbyVideoObject.SetActive(false);
            gameAudio?.SetLobbyVideoMusicActive(false);
            UpdateStartupLoading(1f);
            FinishStartupLoading();
        }

        private void Build038LobbyHotspot(Transform parent, LobbyPunkResponsiveLayout layout,
            string objectName, Rect spec, Rect fallback, UnityEngine.Events.UnityAction action)
        {
            GameObject hotspot = NewImage(objectName, parent, null, Color.clear);
            RectTransform rect = hotspot.GetComponent<RectTransform>();
            Image hit = hotspot.GetComponent<Image>();
            hit.raycastTarget = true;
            Button button = hotspot.AddComponent<Button>();
            button.targetGraphic = hit;
            button.onClick.AddListener(() =>
            {
                action?.Invoke();
                ResumeMediaAfterUserGesture();
                gameAudio?.PlayClick();
            });
            layout.Add(rect, spec, fallback, false);
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
