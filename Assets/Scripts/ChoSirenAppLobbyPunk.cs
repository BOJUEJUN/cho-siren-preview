using ChoSiren.Panels;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>
    /// Reference-home composition. The transparent punk artwork remains independent from
    /// the hit areas so it can be replaced without changing navigation or responsive layout.
    /// </summary>
    public sealed partial class ChoSirenApp
    {
        private void BuildPunkLobby()
        {
            GameObject heroLayer = NewObject("HeroLayer", contentRoot);
            Stretch(heroLayer.AddComponent<RectTransform>());

            // Regression-only composition guide. It deliberately has no Graphic, so it can
            // never block input or become visible in a player build.
            GameObject faceSafeZone = NewObject("HeroFaceSafeZone", heroLayer.transform);
            RectTransform faceRect = faceSafeZone.AddComponent<RectTransform>();
            PlaceTop(faceRect, 348f, 220f, 254f, 304f);

            GameObject cardLayer = NewObject("LobbyCards", contentRoot);
            RectTransform cardLayerRect = cardLayer.AddComponent<RectTransform>();
            Stretch(cardLayerRect);

            BuildPunkLobbyLogo(cardLayer.transform);

            BuildPunkLobbyCard(cardLayer.transform, "PracticeRoom", "练习室", "PRACTICE ROOM",
                "Art/LobbyPunk/lobby-practice-punk-v1", 8f, 470f / 1290f, 310f, 210f,
                OpenLobbyPractice);
            BuildPunkLobbyCard(cardLayer.transform, "AlbumProduction", "专辑制作", "ALBUM PRODUCTION",
                "Art/LobbyPunk/lobby-album-punk-v1", 8f, 720f / 1290f, 300f, 190f,
                () => Toast("专辑制作即将开放"), true);
            BuildPunkLobbyCard(cardLayer.transform, "Tasks", "任务", "TASKS",
                "Art/LobbyPunk/lobby-task-punk-v1", 8f, 930f / 1290f, 300f, 190f,
                OpenDailyTasks);
            BuildPunkStageCallToAction(cardLayer.transform);

            LobbyPunkResponsiveLayout responsiveLayout = cardLayer.AddComponent<LobbyPunkResponsiveLayout>();
            responsiveLayout.Configure(cardLayerRect, faceRect,
                cardLayer.transform.Find("PracticeRoom") as RectTransform,
                cardLayer.transform.Find("AlbumProduction") as RectTransform,
                cardLayer.transform.Find("Tasks") as RectTransform,
                cardLayer.transform.Find("LiveOnStage") as RectTransform);

            cardLayer.transform.SetAsLastSibling();

            GameObject loadingBadge = NewPanel("HeroLoading", heroLayer.transform,
                new Color32(20, 18, 65, 220), 15);
            RectTransform loadingRect = loadingBadge.GetComponent<RectTransform>();
            PlaceTop(loadingRect, 266f, 960f, 188f, 34f);
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

        private void BuildPunkLobbyLogo(Transform parent)
        {
            Text logo = NewPlacedText(parent, "幻域魅声", 62, White,
                22, 48, 360, 92, TextAnchor.MiddleLeft, FontStyle.Bold);
            logo.name = "LobbyLogo";
            logo.raycastTarget = false;
            AddReadableShadow(logo);

            Text script = NewPlacedText(parent, "卡塔琳娜", 27,
                new Color32(205, 132, 255, 255), 96, 120, 246, 48,
                TextAnchor.MiddleCenter, FontStyle.BoldAndItalic);
            script.name = "LobbyLogoScript";
            script.raycastTarget = false;
            AddReadableShadow(script);

            Text welcome = NewPlacedText(parent, "欢迎来到卡塔琳娜 · 律动此刻", 12,
                new Color32(212, 184, 255, 230), 24, 170, 350, 28,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            welcome.name = "LobbyLogoCaption";
            welcome.raycastTarget = false;
        }

        private void BuildPunkLobbyCard(Transform parent, string objectName, string title, string english,
            string artworkPath, float x, float topFraction, float width, float height,
            UnityEngine.Events.UnityAction action, bool locked = false)
        {
            GameObject card = NewImage(objectName, parent, null, Color.clear);
            RectTransform rect = card.GetComponent<RectTransform>();
            PlaceLobbyTopFraction(rect, x, topFraction, width, height);
            UnityEngine.UI.Image hitArea = card.GetComponent<UnityEngine.UI.Image>();
            hitArea.raycastTarget = true;

            UnityEngine.UI.Button button = card.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = hitArea;
            button.onClick.AddListener(() =>
            {
                action?.Invoke();
                ResumeMediaAfterUserGesture();
                gameAudio?.PlayClick();
            });

            GameObject artwork = NewImage("PunkArtwork", card.transform, AiUiSprite(artworkPath), White);
            Stretch(artwork.GetComponent<RectTransform>());
            UnityEngine.UI.Image artworkImage = artwork.GetComponent<UnityEngine.UI.Image>();
            artworkImage.preserveAspect = true;
            artworkImage.useSpriteMesh = true;
            artworkImage.raycastTarget = false;

            Text titleText = NewPlacedText(card.transform, title, 25, White,
                28, 42, 216, 45, TextAnchor.MiddleLeft, FontStyle.Bold);
            titleText.name = "Title";
            AddReadableShadow(titleText);

            Text englishText = NewPlacedText(card.transform, english, 15,
                new Color32(203, 145, 255, 255), 28, 87, 216, 30,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            englishText.name = "EnglishTitle";
            AddReadableShadow(englishText);

            if (!locked) return;
            Text lockText = NewPlacedText(card.transform, "即将开放", 12,
                new Color32(255, 223, 247, 255), 150, 122, 92, 26,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            lockText.name = "LockedState";
            AddReadableShadow(lockText);
        }

        private void BuildPunkStageCallToAction(Transform parent)
        {
            GameObject stage = NewImage("LiveOnStage", parent, null, Color.clear);
            RectTransform stageRect = stage.GetComponent<RectTransform>();
            PlaceLobbyTopFraction(stageRect, 382f, 850f / 1290f, 316f, 330f);
            UnityEngine.UI.Image hitArea = stage.GetComponent<UnityEngine.UI.Image>();
            hitArea.raycastTarget = true;

            UnityEngine.UI.Button button = stage.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = hitArea;
            button.onClick.AddListener(() =>
            {
                OpenLevelMap();
                ResumeMediaAfterUserGesture();
                gameAudio?.PlayClick();
            });

            GameObject artwork = NewImage("PunkArtwork", stage.transform,
                AiUiSprite("Art/LobbyPunk/lobby-perform-cta-punk-v1"), White);
            Stretch(artwork.GetComponent<RectTransform>());
            UnityEngine.UI.Image artworkImage = artwork.GetComponent<UnityEngine.UI.Image>();
            artworkImage.preserveAspect = true;
            artworkImage.useSpriteMesh = true;
            artworkImage.raycastTarget = false;

            Text live = NewPlacedText(stage.transform, "开始演出", 36, White,
                42, 82, 232, 62, TextAnchor.MiddleCenter, FontStyle.Bold);
            live.name = "Title";
            AddReadableShadow(live);
            Outline outline = live.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color32(239, 77, 255, 220);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            Text english = NewPlacedText(stage.transform, "START LIVE", 17,
                new Color32(212, 150, 255, 255), 58, 140, 200, 28,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            english.name = "EnglishTitle";
            AddReadableShadow(english);

            Text ready = NewPlacedText(stage.transform, "舞台已就绪", 14,
                new Color32(255, 231, 249, 255), 70, 174, 176, 26,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            ready.name = "ReadyState";
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

        private static void PlaceLobbyTopFraction(RectTransform rect, float x, float topFraction,
            float width, float height)
        {
            float anchorY = 1f - Mathf.Clamp01(topFraction);
            rect.anchorMin = new Vector2(0f, anchorY);
            rect.anchorMax = new Vector2(0f, anchorY);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, 0f);
            rect.sizeDelta = new Vector2(width, height);
        }

    }

    /// <summary>
    /// Keeps the approved portrait composition while giving landscape/headless test surfaces a
    /// compact, non-overlapping fallback. Player-facing portrait sizes remain fixed.
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class LobbyPunkResponsiveLayout : MonoBehaviour
    {
        private const float PortraitThreshold = 800f;
        private RectTransform root;
        private RectTransform faceSafeZone;
        private RectTransform practice;
        private RectTransform album;
        private RectTransform tasks;
        private RectTransform liveOnStage;
        private float appliedHeight = -1f;

        public void Configure(RectTransform layoutRoot, RectTransform face, RectTransform practiceRoom,
            RectTransform albumProduction, RectTransform taskEntry, RectTransform stageEntry)
        {
            root = layoutRoot;
            faceSafeZone = face;
            practice = practiceRoom;
            album = albumProduction;
            tasks = taskEntry;
            liveOnStage = stageEntry;
            Apply(true);
        }

        private void LateUpdate() => Apply(false);

        private void OnRectTransformDimensionsChange() => Apply(false);

        private void Apply(bool force)
        {
            if (root == null || practice == null || album == null || tasks == null ||
                liveOnStage == null || faceSafeZone == null) return;
            float height = root.rect.height;
            if (!force && Mathf.Abs(height - appliedHeight) < 0.5f) return;
            appliedHeight = height;

            if (height >= PortraitThreshold)
            {
                PlaceTop(faceSafeZone, 348f, 220f, 254f, 304f);
                PlaceFraction(practice, 8f, 470f / 1290f, 310f, 210f);
                PlaceFraction(album, 8f, 720f / 1290f, 300f, 190f);
                PlaceFraction(tasks, 8f, 930f / 1290f, 300f, 190f);
                PlaceFraction(liveOnStage, 382f, 850f / 1290f, 316f, 330f);
                return;
            }

            // Landscape fallback is used by headless/browser surfaces with very little vertical
            // room. Secondary routes become one row and the CTA occupies the clear lower-left.
            PlaceTop(faceSafeZone, 710f, 0f, 8f, 8f);
            PlaceTop(practice, 8f, 0f, 220f, 150f);
            PlaceTop(album, 244f, 0f, 220f, 150f);
            PlaceTop(tasks, 480f, 0f, 220f, 150f);
            PlaceTop(liveOnStage, 8f, 84f, 316f, 210f);
        }

        private static void PlaceFraction(RectTransform rect, float x, float topFraction,
            float width, float height)
        {
            float anchorY = 1f - Mathf.Clamp01(topFraction);
            rect.anchorMin = new Vector2(0f, anchorY);
            rect.anchorMax = new Vector2(0f, anchorY);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, 0f);
            rect.sizeDelta = new Vector2(width, height);
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
