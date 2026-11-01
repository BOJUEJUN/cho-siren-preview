using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ChoSiren.Systems.Economy;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ChoSiren
{
    public sealed partial class ChoSirenApp
    {
        [Serializable] private sealed class PsdHomeLayout
        {
            public float width, height;
            public PsdHomeElement[] elements;
            public PsdHomeHit[] hits;
        }
        [Serializable] private sealed class PsdHomeElement
        {
            public string id, name, action;
            public float x, y, w, h;
            public int z;
        }
        [Serializable] private sealed class PsdHomeHit
        {
            public string id;
            public float x, y, w, h;
        }

        private const string PsdHomePath = "Art/PsdHome20260921/";
        private GameObject psdHome;
        private Text psdDiamonds, psdGold, psdStamina, psdLevel, psdPower;
        private PsdHomeMotion psdMotion;
        private PsdHomeCharacterCarousel psdCharacters;
        private PsdHomeIdolVideo1025 psdIdols;
        private readonly Dictionary<string, PsdHomeAlbumInkMotion> psdEntryInks = new Dictionary<string, PsdHomeAlbumInkMotion>();
        private bool psdOpening;
        private GameObject psdTaskBadge;
        private GameObject psdMailBadge;
        private GameObject psdProfileBadge;
        private GameObject psdAlbumTag;
        private GameObject psdIdleBadge;
        private GameObject psdSignInBadge;

        private void ClearPsdHome()
        {
            PsdHomeStageBridge.SetActive(false);
            if (psdHome != null) { psdHome.SetActive(false); Destroy(psdHome); }
            psdHome = null;
            psdMotion = null;
            psdCharacters = null;
            psdEntryInks.Clear();
            psdOpening = false;
            psdDiamonds = psdGold = psdStamina = psdLevel = psdPower = null;
        }

        private bool BuildPsdHome()
        {
            TextAsset json = Resources.Load<TextAsset>(PsdHomePath + "layout");
            if (json == null) { Debug.LogError("Authored homepage layout is missing."); return false; }
            PsdHomeLayout layout = JsonUtility.FromJson<PsdHomeLayout>(json.text);
            float scale = 720f / layout.width;
            psdHomeScale = scale;
            // The lobby art fills the WHOLE screen (behind the safe area) so notched phones
            // show no frame; HUD and cards are shifted back inside the safe area below.
            psdHome = NewObject("PsdHome20260921", safeRoot.parent);
            RectTransform root = psdHome.AddComponent<RectTransform>();
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(.5f, .5f);
            root.sizeDelta = new Vector2(720f, layout.height * scale);
            psdHome.AddComponent<RectMask2D>();
            psdHome.AddComponent<PsdHomeFit>();
            psdHome.transform.SetSiblingIndex(safeRoot.GetSiblingIndex());
            ComputePsdSafeShift(layout.height * scale);
            psdMotion = psdHome.AddComponent<PsdHomeMotion>();
            // Double-tap hide also clears the shared header and the nav dock.
            psdMotion.RegisterFadeOnly(safeRoot.Find("TopBar")?.gameObject);
            psdMotion.RegisterFadeOnly(navRoot != null ? navRoot.gameObject : null);
            var backdropMotion = psdHome.AddComponent<PsdHomeBackdropMotion929>();
            psdMotion.Initialize(root, ShowPsdHomeDialogue, RoundedSprite(24), font);
            psdMotion.Personalize = model.Personalize;

            // This transparent surface receives clicks on the character and empty scene.
            // Hotspot buttons are rendered above it and retain their measured hit areas.
            GameObject sceneTouch = NewImage("PsdSceneTouch", root, null, Color.clear);
            PlaceTop(sceneTouch.GetComponent<RectTransform>(), 0, 0, 720, layout.height * scale);
            sceneTouch.GetComponent<Image>().raycastTarget = true;
            sceneTouch.AddComponent<PsdHomeSceneTouch>().Initialize(psdMotion);

            var buttons = new Dictionary<string, GameObject>();
            var hitRects = new Dictionary<string, PsdHomeHit>();
            foreach (PsdHomeHit hit in layout.hits) hitRects.Add(hit.id, hit);
            foreach (PsdHomeElement element in layout.elements)
            {
                // The painted bottom nav (and its underline) gives way to the shared disc dock.
                // layer-63 is the painted player name; a live 经纪人 label replaces it below.
                // The painted header (profile, currency pills) gives way to the one
                // shared TopBar every page uses; the lobby keeps its own stacked gear + mail.
                if (element.id == "layer-32" || element.id == "layer-63" ||
                    (!string.IsNullOrEmpty(element.action) && (element.action.StartsWith("Nav-", StringComparison.Ordinal)
                        || element.action == "Profile"
                        || element.action.StartsWith("Currency-", StringComparison.Ordinal))))
                    continue;
                Transform parent = root;
                float x = element.x, y = element.y;
                if (!string.IsNullOrEmpty(element.action))
                {
                    PsdHomeHit hit = hitRects[element.action];
                    if (!buttons.TryGetValue(element.action, out GameObject buttonObject))
                    {
                        buttonObject = NewImage(element.action, root, null, Color.clear);
                        PlaceTop(buttonObject.GetComponent<RectTransform>(),
                            hit.x * scale - PsdRightInset((hit.x + hit.w * .5f) / layout.width),
                            hit.y * scale + PsdSafeShift((hit.y + hit.h * .5f) / layout.height),
                            hit.w * scale, hit.h * scale);
                        var image = buttonObject.GetComponent<UnityEngine.UI.Image>();
                        image.raycastTarget = true;
                        var button = buttonObject.AddComponent<UnityEngine.UI.Button>();
                        button.targetGraphic = image;
                        button.transition = UnityEngine.UI.Selectable.Transition.None;
                        string action = element.action;
                        button.onClick.AddListener(() => ActivatePsdHomeAction(action));
                        buttons.Add(action, buttonObject);
                        // The eye stays on screen while everything else hides, so it can bring the UI back.
                        if (element.action != "HideUi") psdMotion.RegisterUi(buttonObject);
                    }
                    parent = buttonObject.transform;
                    if (element.id == "layer-22")
                    {
                        // Move the authored CTA face and its glow together. The hit
                        // rectangle stays fixed; a glow behind a static face reads as
                        // a second image moving underneath it.
                        GameObject face = NewObject("StageFaceBeat", parent);
                        RectTransform faceRect = face.AddComponent<RectTransform>();
                        faceRect.anchorMin = faceRect.anchorMax = faceRect.pivot = new Vector2(.5f, .5f);
                        faceRect.sizeDelta = new Vector2(hit.w * scale, hit.h * scale);
                        faceRect.anchoredPosition = Vector2.zero;
                        face.AddComponent<PsdHomeCallToActionBeat>();
                        parent = face.transform;
                    }
                    x -= hit.x; y -= hit.y;
                }
                Sprite sprite = ReferenceArt038.Load("Art/Reference038/home-" + element.id);
                if (sprite == null) Debug.LogError("Authored homepage layer missing: " + element.id);
                GameObject art = NewImage(element.id + "VisualV2", parent, sprite, Color.white);
                var visual = art.GetComponent<UnityEngine.UI.Image>();
                visual.raycastTarget = false;
                visual.useSpriteMesh = false;
                // The logo (layer-53) is scenery but sits right under the HUD, so it moves
                // down with the HUD to keep the authored spacing.
                float shift = element.id == "layer-53" ? psdShiftTop
                    : parent == root && !PsdHomeSceneLayer(element.id)
                    ? PsdSafeShift((element.y + element.h * .5f) / layout.height) : 0f;
                float inset = parent == root && !PsdHomeSceneLayer(element.id)
                    ? PsdRightInset((element.x + element.w * .5f) / layout.width) : 0f;
                PlaceTop(visual.rectTransform, x * scale - inset, y * scale + shift, element.w * scale, element.h * scale);
                if (string.IsNullOrEmpty(element.action) && !PsdHomeSceneLayer(element.id))
                    psdMotion.RegisterUi(art);
                if (element.id == "layer-00")
                {
                    psdMotion.SetBackground(visual.rectTransform);
                    // 1101b edge-fit: the backdrop PNG has an ~11-texel transparent left rim.
                    ChoSiren.UI.ScreenEdgeExtend.Attach(root, visual.rectTransform, sprite, 12);
                }
                if (element.id == "layer-52") backdropMotion.SetDisc(visual.rectTransform);
                if (element.id == "layer-53") backdropMotion.SetLogo(visual);
                if (element.id == "layer-01")
                {
                    // This orbit sits between the authored backdrop and character,
                    // so the meeting's rotating light reads behind her silhouette.
                    GameObject orbit = NewImage("BackdropLightOrbit", root,
                        PsdHomePulse.Sprite(4), new Color(.52f, .85f, 1f, .11f));
                    PlaceTop(orbit.GetComponent<RectTransform>(), 120f, 235f, 690f, 690f);
                    orbit.AddComponent<PsdHomeBackdropOrbit>();
                }
                if (element.id == "layer-03") psdMotion.SetOutline(visual.rectTransform);
                if (element.id == "layer-04")
                {
                    psdMotion.SetCharacter(visual.rectTransform);
                    GameObject performance = new GameObject("CharacterPerformance", typeof(RectTransform),
                        typeof(CanvasRenderer), typeof(UnityEngine.UI.RawImage));
                    performance.transform.SetParent(root, false);
                    performance.AddComponent<PsdHomeCharacterSurface>().Initialize(root, psdMotion);
                }
                // The album's own ink is added with the other entries once every layer exists.
                if (element.id == "layer-12") visual.enabled = false;
                if (element.id == "layer-22")
                {
                    GameObject echo = NewImage("StageBeatEcho", parent, sprite, new Color(1f, 1f, 1f, 0f));
                    var echoImage = echo.GetComponent<UnityEngine.UI.Image>();
                    echoImage.raycastTarget = false;
                    PlaceTop(echoImage.rectTransform, x * scale, y * scale,
                        element.w * scale, element.h * scale);
                    echo.transform.SetAsFirstSibling();
                    echo.AddComponent<PsdHomeBeatEcho>();
                }
            }

            // Header values live on the shared TopBar (see ConfigureReferenceHeader038).
            foreach (var pair in buttons)
            {
                var kind = pair.Key.StartsWith("Nav-") ? LobbyHotspotFeedback.VisualKind.Navigation :
                    pair.Key.StartsWith("Currency-") ? LobbyHotspotFeedback.VisualKind.CurrencyPlus :
                    pair.Key == "Profile" ? LobbyHotspotFeedback.VisualKind.Profile :
                    pair.Key == "Settings" || pair.Key == "Mail" ? LobbyHotspotFeedback.VisualKind.Settings :
                    pair.Key == "LiveOnStage" ? LobbyHotspotFeedback.VisualKind.CallToAction : LobbyHotspotFeedback.VisualKind.Entry;
                AttachLobbyHotspotFeedback(pair.Value, kind);
                if (kind == LobbyHotspotFeedback.VisualKind.Navigation)
                {
                    RectTransform rect = pair.Value.GetComponent<RectTransform>();
                    pair.Value.GetComponent<LobbyHotspotFeedback>().ConfigureNavigationVisual(
                        rect.rect.width * .5f, rect.rect.height - 3f, rect.rect.width * .65f);
                }
            }
            AttachLobbyFlames1031(buttons);
            if (buttons.TryGetValue("Tasks", out GameObject tasksButton))
            {
                // Red dot with a count while daily/weekly rewards wait to be claimed.
                RectTransform hit = tasksButton.GetComponent<RectTransform>();
                // 2026-10-31: the torn-paper teal notice sticker everywhere (was the red dot), no count.
                psdTaskBadge = UI.NoticeSticker1031.Create("TaskBadge", tasksButton.transform);
                // On the clipboard's top-right corner (the card art's own edge), like the reference's dot.
                PlaceTop(psdTaskBadge.GetComponent<RectTransform>(), 197f, 11f, 18f, 18f);
                psdMotion.RegisterUi(psdTaskBadge);
            }
            if (buttons.TryGetValue("Mail", out GameObject mailButton))
            {
                RectTransform hit = mailButton.GetComponent<RectTransform>();
                psdMailBadge = UI.NoticeSticker1031.Create("MailBadge", mailButton.transform);
                PlaceTop(psdMailBadge.GetComponent<RectTransform>(), hit.rect.width - 34f - psdCropX, 6f, 18f, 18f);
                psdMotion.RegisterUi(psdMailBadge);
            }
            if (buttons.TryGetValue("Profile", out GameObject profileButton))
            {
                RectTransform hit = profileButton.GetComponent<RectTransform>();
                psdProfileBadge = UI.NoticeSticker1031.Create("AchievementBadge", profileButton.transform);
                PlaceTop(psdProfileBadge.GetComponent<RectTransform>(), hit.rect.width * .2f, 4f, 18f, 18f);
                psdMotion.RegisterUi(psdProfileBadge);
            }
            // 闪耀舞台 and 每日签到 left the lobby with the 2026-10-19 PSD: both stay reachable from
            // the daily tasks (前往), and the sign-in still greets the first visit of the day.
            if (buttons.TryGetValue("AlbumProduction", out GameObject albumButton))
            {
                // The album card is the group's record book: achievements and their rewards.
                RectTransform hit = albumButton.GetComponent<RectTransform>();
                // 2026-10-31: the same torn-paper notice sticker as every other entry (no 「可领 N」 tag).
                psdAlbumTag = UI.NoticeSticker1031.Create("ComingSoonTag", albumButton.transform);
                // Slapped on the record's top-right edge, not floating off the card over the idol.
                PlaceTop(psdAlbumTag.GetComponent<RectTransform>(), 203f, 13f, 18f, 18f);
                psdMotion.RegisterUi(psdAlbumTag);
            }
            // The smaller, complete figure needs room: the entry cards hug the left edge and the
            // CTA hugs the bottom-right corner instead of covering her legs.
            // The approved R02 ink plays behind every entry, each in its own colour.
            {
                // Each card's own artwork layer, so the paint frames the art the same way on all three.
                var art = new Dictionary<string, string> { { "Tasks", "layer-17" }, { "AlbumProduction", "layer-13" }, { "PracticeRoom", "layer-21" } };
                foreach (string entry in new[] { "Tasks", "AlbumProduction", "PracticeRoom", "LiveOnStage" })
                {
                    if (!buttons.TryGetValue(entry, out GameObject card)) continue;
                    Color tint = PsdHomeAlbumInkMotion.InkColor(entry);
                    var cardArt = art.TryGetValue(entry, out string layer) ? card.transform.Find(layer + "VisualV2") as RectTransform : null;
                    var entryInk = card.AddComponent<PsdHomeAlbumInkMotion>();
                    if (cardArt == null) entryInk.InitializeBehindCallToAction(root, tint);
                    else entryInk.InitializeAroundArt(cardArt, root, tint);
                    psdEntryInks[entry] = entryInk;
                }
            }
            // Latest home PSD (2026-10-19): entries keep their authored size and place.
            BuildHomeMusicDisc(root);
            UI.ChallengeVideoTransition.Warm(psdHome.transform);
            UpdatePsdHomeHud();
            psdMotion.BuildDialogue();
            // 2026-10-28: an unnamed manager signs the contract, then the prologue plays once; the usual
            // first-visit guide / daily sign-in card follows whichever of them ran.
            // Opened a frame later, after the nav dock is built, so they sit above it.
            if (!Application.isBatchMode && model.NeedsPlayerName)
                StartCoroutine(NextFrame1028(() => OpenPlayerContract1028(true, () => PlayPrologue1028(GreetLobby1028))));
            else if (!Application.isBatchMode && !model.IsStoryCompleted(GameModel.PrologueStoryId))
                StartCoroutine(NextFrame1028(() => PlayPrologue1028(GreetLobby1028)));
            else GreetLobby1028();
            psdCharacters = psdHome.AddComponent<PsdHomeCharacterCarousel>();
            psdCharacters.Initialize(psdMotion, font, RoundedSprite(14), model.Save.HomeCharacterIndex,
                index => model.SetHomeCharacter(index), () => psdOpening);
            psdIdols = psdHome.AddComponent<PsdHomeIdolVideo1025>();
            psdIdols.Initialize(psdMotion);
            psdCharacters.AttachIdols(psdIdols);
            if (lobbyVideoObject != null) lobbyVideoObject.SetActive(false);
            gameAudio?.SetLobbyVideoMusicActive(false);
            UpdateStartupLoading(1f);
            FinishStartupLoading();
            PsdHomeStageBridge.SetActive(true);
            return true;
        }

        private float psdShiftTop, psdShiftBottom, psdCropX, psdHomeScale = 720f / 4034f;

        /// <summary>
        /// The PSD's record player (top-left): the record spins while home music plays and the
        /// bar's black window shows the current song; tapping anywhere on it plays the next song.
        /// </summary>
        private void BuildHomeMusicDisc(Transform root)
        {
            Transform button = root.Find("MusicNext");
            if (button == null) return;
            float scale = psdHomeScale;
            // Layer 40's black window is slanted about 8°: a rotated strip, centred on the window
            // (PSD pixels relative to the MusicNext hit at x 0, y 640), holds ◀ title ▶.
            RectTransform bar = new GameObject("SongBar", typeof(RectTransform)).GetComponent<RectTransform>();
            bar.SetParent(button, false);
            PlaceTop(bar, 740f * scale, (1055f - 640f - 85f) * scale, 800f * scale, 170f * scale);
            bar.localEulerAngles = new Vector3(0f, 0f, 8f);
            RectTransform window = new GameObject("SongWindow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask)).GetComponent<RectTransform>();
            window.SetParent(bar, false);
            window.anchorMin = Vector2.zero; window.anchorMax = Vector2.one;
            window.offsetMin = new Vector2(120f * scale, 0f); window.offsetMax = new Vector2(-120f * scale, 0f);
            window.GetComponent<Image>().raycastTarget = false;
            window.GetComponent<Mask>().showMaskGraphic = false;
            Text title = NewText("SongTitle", window, string.Empty, 22, Color.white, FontStyle.Bold, TextAnchor.MiddleLeft);
            title.raycastTarget = false;
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            title.verticalOverflow = VerticalWrapMode.Overflow;
            AddReadableShadow(title);
            window.gameObject.AddComponent<HomeMusicMarquee>().Bind(title);
            HomeMusicArrow(bar, "SongPrev", "◀", 0f, scale, () => { if (gameAudio != null && gameAudio.PreviousHomeMusic()) gameAudio.PlayClick(); });
            HomeMusicArrow(bar, "SongNext", "▶", 1f, scale, () => { if (gameAudio != null && gameAudio.NextHomeMusic()) gameAudio.PlayClick(); });
            Transform disc = root.Find("MusicNext/layer-disc-42VisualV2");
            if (disc != null) disc.gameObject.AddComponent<HomeMusicDiscSpin>().Bind(gameAudio, title.font);
            Text pausedMarker = null;
            if (disc is RectTransform discRect)
            {
                pausedMarker = NewText("MusicPaused", disc.parent, "▶", 24,
                    new Color32(255, 255, 255, 215), FontStyle.Normal, TextAnchor.MiddleCenter);
                RectTransform marker = pausedMarker.rectTransform;
                marker.anchorMin = discRect.anchorMin;
                marker.anchorMax = discRect.anchorMax;
                marker.pivot = new Vector2(.5f, .5f);
                marker.anchoredPosition = discRect.anchoredPosition;
                marker.sizeDelta = new Vector2(42f, 42f);
                pausedMarker.raycastTarget = false;
                AddReadableShadow(pausedMarker);
            }
            button.gameObject.AddComponent<HomeMusicSwitcher>().Bind(gameAudio, title, pausedMarker);
            BuildHomeMusicEntry1101(button, window, disc as RectTransform); // 2026-11-01 唱片收藏 entry
        }

        private void HomeMusicArrow(RectTransform bar, string name, string glyph, float side, float scale, Action onTap)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(bar, false);
            rect.anchorMin = rect.anchorMax = new Vector2(side, .5f);
            rect.pivot = new Vector2(side, .5f);
            rect.sizeDelta = new Vector2(120f * scale, 170f * scale);
            rect.anchoredPosition = Vector2.zero;
            go.GetComponent<Image>().color = new Color(0, 0, 0, .004f);
            go.GetComponent<Button>().onClick.AddListener(() => onTap());
            Text label = NewText("Glyph", rect, glyph, 20, new Color32(120, 230, 255, 255), FontStyle.Bold, TextAnchor.MiddleCenter);
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            label.raycastTarget = false;
            AddReadableShadow(label);
        }

        /// <summary>
        /// Safe-area insets expressed in lobby-root units. Zero on screens without a notch
        /// (web, desktop), so the authored composition is untouched there.
        /// </summary>
        private void ComputePsdSafeShift(float rootHeight)
        {
            psdShiftTop = psdShiftBottom = psdCropX = 0f;
            Canvas.ForceUpdateCanvases();
            RectTransform canvasRect = safeRoot.parent as RectTransform;
            RectTransform safe = safeRoot as RectTransform;
            if (canvasRect == null || safe == null) return;
            float width = canvasRect.rect.width, height = canvasRect.rect.height;
            if (width <= 0f || height <= 0f) return;
            float fit = PsdHomeFit.FitFor(width, height, 720f, rootHeight);
            // The inset includes the status-bar row; the island itself ends a little higher,
            // so the HUD only needs ~85% of it to clear the island with a small margin.
            psdShiftTop = .85f * (1f - safe.anchorMax.y) * height / fit;
            psdShiftBottom = safe.anchorMin.y * height / fit;
            // Shorter screens (e.g. WeChat's in-app browser under its title bar) crop the
            // cover-fitted art at top and bottom; keep the HUD and cards inside what is shown.
            float verticalCrop = Mathf.Max(0f, (rootHeight * fit - height) * .5f / fit);
            psdShiftTop += verticalCrop;
            psdShiftBottom += verticalCrop;
            // Covering a slightly taller screen trims a few units off each side; the right
            // column (stamina, gear, mail, idle stage, sign-in) slides in by that much.
            psdCropX = Mathf.Max(0f, (720f * fit - width) * .5f / fit);
        }

        /// <summary>HUD rows near the top move below the island; cards near the bottom move
        /// above the home indicator. The middle (character, scene) stays put.</summary>
        private float PsdRightInset(float centreFractionX) => centreFractionX > .82f ? psdCropX : 0f;

        private float PsdSafeShift(float centreFraction) =>
            centreFraction < .25f ? psdShiftTop : centreFraction > .42f ? -psdShiftBottom : 0f;

        private const string GuideTasksFlag = "guide-tasks-opened";
        private const string GuideDoneFlag = "guide-chain-done";

        /// <summary>
        /// New-player chain shown under the HUD until finished: win 1-1 → open tasks and claim →
        /// train a member → sign a new member. Each step reads real save state, so it never
        /// asks for something already done; tapping the strip goes straight to that step.
        /// </summary>
        private void BuildGuideStrip(Transform root)
        {
            if (model.HasStoryFlag(GuideDoneFlag)) return;
            bool trained = false;
            foreach (int member in model.Save.UnlockedMembers)
                if (model.LevelOf(member) > 1) { trained = true; break; }
            (string text, UnityEngine.Events.UnityAction go)[] steps =
            {
                (model.IsStageCleared("stage-1-1") ? null : "打赢第一场 1-1", OpenLevelMap),
                (model.HasStoryFlag(GuideTasksFlag) ? null : "去「任务」领奖励", OpenDailyTasks),
                (trained ? null : "去「练习室」升一级", OpenLobbyPractice),
                (model.Save.UnlockedMembers.Count > 4 ? null : "去「选秀」签新成员", () => ShowScreen("audition")),
            };
            int index = Array.FindIndex(steps, step => step.text != null);
            if (index < 0) { model.FirstTime(GuideDoneFlag); return; }

            GameObject strip = NewImage("GuideStrip", root, null, new Color32(8, 4, 18, 236));
            // Left column under the logo, clear of the character's face and the gear column.
            PlaceTop(strip.GetComponent<RectTransform>(), 10f, 366f + psdShiftTop, 300f, 46f);
            Image stripImage = strip.GetComponent<Image>();
            stripImage.raycastTarget = true;
            GameObject stripe = NewImage("Stripe", strip.transform, null, new Color32(255, 46, 147, 255));
            PlaceTop(stripe.GetComponent<RectTransform>(), 0f, 0f, 6f, 46f);
            Text tag = NewText("GuideStep", strip.transform, $"新手 {index + 1}/4", 14,
                new Color32(255, 46, 147, 255), FontStyle.BoldAndItalic, TextAnchor.MiddleLeft);
            PlaceTop(tag.rectTransform, 14f, 0f, 70f, 46f);
            tag.raycastTarget = false;
            Text line = NewText("GuideText", strip.transform, steps[index].text + "  ›", 16, Color.white,
                FontStyle.Bold, TextAnchor.MiddleLeft);
            PlaceTop(line.rectTransform, 84f, 0f, 210f, 46f);
            line.raycastTarget = false;
            line.resizeTextForBestFit = true;
            line.resizeTextMinSize = 12;
            line.resizeTextMaxSize = 16;
            Button button = strip.AddComponent<Button>();
            button.targetGraphic = stripImage;
            UnityEngine.Events.UnityAction go = steps[index].go;
            button.onClick.AddListener(() => { gameAudio?.PlayClick(); go(); });
            psdMotion.RegisterUi(strip);
        }

        private Text PsdHomeText(Transform parent, string name, string text, Rect source, float sourceFontSize,
            float scale, bool latin)
        {
            Text value = NewText(name, parent, text, Mathf.RoundToInt(sourceFontSize * scale), Color.white,
                FontStyle.Normal, TextAnchor.MiddleLeft);
            if (latin) value.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            value.fontStyle = FontStyle.Bold;
            float inset = PsdRightInset((source.x + source.width * .5f) * scale / 720f);
            PlaceTop(value.rectTransform, source.x * scale - inset, source.y * scale + psdShiftTop, source.width * scale, source.height * scale);
            value.raycastTarget = false;
            value.resizeTextForBestFit = true;
            value.resizeTextMaxSize = value.fontSize;
            value.resizeTextMinSize = Mathf.RoundToInt(value.fontSize * .75f);
            value.horizontalOverflow = HorizontalWrapMode.Wrap;
            value.verticalOverflow = VerticalWrapMode.Overflow;
            // The HUD plates are striped and busy: a dark rim and a soft drop keep the
            // live numbers readable where the plate's lines run behind them.
            Outline rim = value.gameObject.AddComponent<Outline>();
            rim.effectColor = new Color32(18, 6, 38, 230);
            rim.effectDistance = new Vector2(1.1f, -1.1f);
            Shadow drop = value.gameObject.AddComponent<Shadow>();
            drop.effectColor = new Color32(10, 2, 24, 170);
            drop.effectDistance = new Vector2(0f, -1.6f);
            return value;
        }

        private void UpdatePsdHomeHud()
        {
            if (psdHome == null || model == null) return;
            if (psdDiamonds != null)
            {
                psdDiamonds.text = HudAmount038(model.Save.Diamonds);
                psdGold.text = HudAmount038(model.Save.Gold);
                psdStamina.text = model.Save.Stamina + "/" + model.StaminaCap;
            }
            if (psdTaskBadge != null) psdTaskBadge.SetActive(model.ClaimableTaskCount > 0);
            if (psdMailBadge != null) psdMailBadge.SetActive(model.ClaimableMailCount > 0);
            if (psdProfileBadge != null) psdProfileBadge.SetActive(model.ClaimableAchievementCount > 0);
            if (psdAlbumTag != null)
            {
                var achievements = model.Achievements();
                psdAlbumTag.SetActive(achievements.Count(item => item.Claimable) > 0);
            }
            if (psdSignInBadge != null) psdSignInBadge.SetActive(!model.SignedToday);
            if (psdIdleBadge != null)
            {
                IdleIncomeReport idle = model.PreviewIdleIncome();
                psdIdleBadge.SetActive(model.CanClaimIdleIncome &&
                    (idle.AmountOf(CurrencyIds.Gold) > 0 || idle.AmountOf(CurrencyIds.Diamond) > 0));
            }
            if (psdLevel != null) psdLevel.text = "LV." + TeamAverageLevel;
            if (psdPower != null) psdPower.text = "战力 " + model.TeamPower.ToString("N0");
        }

        private void ActivatePsdHomeAction(string action)
        {
            if (action == "HideUi")
            {
                if (psdOpening || psdMotion == null) return;
                gameAudio?.PlayClick();
                psdMotion.ToggleUiHidden();
                return;
            }
            if (psdOpening || psdMotion == null || psdMotion.UiHidden) return;
            if (action == "MusicNext")
            {
                // 2026-11-01: the bar / song window opens 唱片收藏; the record itself plays/pauses (MusicDiscToggle).
                gameAudio?.PlayClick();
                ResumeMediaAfterUserGesture();
                OpenMusicCollection1101();
                return;
            }
            foreach (var pair in psdEntryInks)
                if (pair.Value != null) pair.Value.SetSelected(pair.Key == action && !pair.Value.Selected);
            psdMotion.PlayClick(action);
            ResumeMediaAfterUserGesture();
            gameAudio?.PlayClick();
            if (action == "AlbumProduction")
            {
                OpenAchievements();
                return;
            }
            StartCoroutine(OpenPsdHomeAfterMotion(action));
        }

        private IEnumerator OpenPsdHomeAfterMotion(string action)
        {
            psdOpening = true;
            if (action == "LiveOnStage")
            {
                // Hide the browser's lobby art, but keep its BGM until the covered scene
                // actually changes. The action CG has no soundtrack of its own.
                PsdHomeStageBridge.SetActive(false);
                var transition = UI.ChallengeVideoTransition.Warm(psdHome.transform);
                bool sceneOpened = false;
                transition.Play(safeRoot, () => { OpenLevelMap(); sceneOpened = true; }, sound => gameAudio?.PlaySfx(sound));
                while (transition != null && transition.IsPlaying) yield return null;
                psdOpening = false;
                if (!sceneOpened && psdHome != null) PsdHomeStageBridge.SetActive(true);
                yield break;
            }
            yield return new WaitForSecondsRealtime(.28f);
            psdOpening = false;
            switch (action)
            {
                case "Tasks": OpenDailyTasks(); break;
                case "PracticeRoom": OpenLobbyPractice(); break;
                case "LiveOnStage": OpenLevelMap(); break;
                case "Profile": OpenProfile(); break;
                case "Settings": OpenSettings(); break;
                case "Mail": OpenInbox(); break;
                default:
                    if (action.StartsWith("Nav-", StringComparison.Ordinal)) ShowScreen(action.Substring(4));
                    else if (action.StartsWith("Currency-", StringComparison.Ordinal)) OpenCurrency(action.Substring(9));
                    break;
            }
        }

        /// <summary>Record-and-banner stage transition featuring the lobby idol on screen.</summary>
        private UI.VinylStageTransition PlayVinylTransition(Action switchScene)
        {
            int index = psdCharacters != null ? psdCharacters.Index : model.Save.HomeCharacterIndex;
            Sprite art = index >= 1 && index <= 3
                ? ReferenceArt038.Load($"Art/Reference038/home-layer-character{index + 1}-portrait") : null;
            if (art == null) art = ReferenceArt038.Load("Art/Reference038/home-layer-04");
            string name = index == 1 ? "LILITH" : index == 2 ? "AMETHYST" : index == 3 ? "CORALIE" : "CATALENA";
            string tag = index == 1 ? "DEMON CLAN  ·  STAGE IDOL" : index == 2 ? "BLOOD ELF  ·  MOON VOCAL"
                : index == 3 ? "SEA SPIRIT  ·  TIDE VOCAL" : "CHARM CLAN  ·  LEAD VOCAL";
            return UI.VinylStageTransition.Play(safeRoot, switchScene, art, name, tag);
        }

        private static bool PsdHomeSceneLayer(string id) => id == "layer-00" || id == "layer-51" ||
            id == "layer-52" || id == "layer-53" || id == "layer-01" || id == "layer-03" ||
            id == "layer-04" || id == "layer-05";

        private void ShowPsdHomeDialogue(int index)
        {
            if (psdMotion == null) return;
            if (psdCharacters != null)
            {
                if (psdCharacters.Switching) return;
                // 2026-10-25: with the idol clips on screen a tap changes her to her other clip.
                bool swapped = psdIdols != null && psdIdols.Swap();
                if (swapped)
                {
                    bool voiced = model != null && model.Save.SfxEnabled;
                    if (psdCharacters.HasPerformance)
                    {
                        psdMotion.DisplayDialogue(index);
                        if (voiced && (gameAudio == null || !gameAudio.PlayVoice($"catalena-{index % 3 + 1}")))
                            PsdHomeVoice.Speak(index);
                    }
                    else if (psdCharacters.Index >= 2) psdMotion.DisplayGuestLine(psdCharacters.Index);
                    else
                    {
                        int said = psdMotion.DisplayLilithLine();
                        if (said >= 0 && gameAudio != null) gameAudio.PlayVoice($"lilith-{said + 1}");
                    }
                    gameAudio?.PlayClick();
                    return;
                }
                if (!psdCharacters.HasPerformance)
                {
                    psdCharacters.Respond();
                    if (psdCharacters.Index >= 2)
                    {
                        psdMotion.DisplayGuestLine(psdCharacters.Index);
                        gameAudio?.PlayClick();
                        return;
                    }
                    int line = psdMotion.DisplayLilithLine();
                    if (line < 0 || gameAudio == null || !gameAudio.PlayVoice($"lilith-{line + 1}"))
                        gameAudio?.PlayClick();
                    return;
                }
            }
            bool sound = model != null && model.Save.SfxEnabled;
            // The line shows with the performance too; its voice comes from the page.
            psdMotion.DisplayDialogue(index);
            // Web: the performance clip carries its own voice. Elsewhere (or without a clip) a
            // recorded line from Resources/Audio/Voice wins over the browser's speech fallback.
            if (!PsdHomeCharacterBridge.Play(index, sound) && sound &&
                (gameAudio == null || !gameAudio.PlayVoice($"catalena-{index % 3 + 1}")))
                PsdHomeVoice.Speak(index);
            gameAudio?.PlayClick();
        }
    }

    /// <summary>Contain the complete PSD inside the safe area without independent axis stretching.</summary>
    public sealed class PsdHomeFit : MonoBehaviour
    {
        private void LateUpdate()
        {
            RectTransform rect = (RectTransform)transform;
            RectTransform parent = rect.parent as RectTransform;
            if (parent == null || rect.rect.width <= 0 || rect.rect.height <= 0) return;
            float fit = FitFor(parent.rect.width, parent.rect.height, rect.rect.width, rect.rect.height);
            rect.localScale = new Vector3(fit, fit, 1);
        }

        /// <summary>
        /// Covers the screen when its proportions are within ~12% of the art (phones), so no
        /// frame shows; otherwise (tablets, wide desktop) contains the art as before.
        /// </summary>
        public static float FitFor(float parentWidth, float parentHeight, float width, float height)
        {
            float contain = Mathf.Min(parentWidth / width, parentHeight / height);
            float cover = Mathf.Max(parentWidth / width, parentHeight / height);
            return cover / contain <= 1.12f ? cover : contain;
        }
    }

}
