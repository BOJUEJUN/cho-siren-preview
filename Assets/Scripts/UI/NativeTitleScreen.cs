using System.Collections;
using System.IO;
using ChoSiren.Panels;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

namespace ChoSiren.UI
{
    /// <summary>
    /// Title / loading screen for the installed app (the web build has its own HTML one):
    /// the looping loading CG fills the screen, with the logo, the 16+ age-rating badge
    /// (tap for the notice), a progress bar with tips, the health-gaming notice and, once
    /// the lobby is ready, TAP TO START. Layout keeps text inside the safe area.
    /// </summary>
    public sealed class NativeTitleScreen : MonoBehaviour, IPointerClickHandler
    {
        private const string VideoFile = "NativeLoading/loading-cg.mp4";
        private const float MinimumShowSeconds = 2.2f;
        private const string MusicOwner = "native-loading-cg";
        private const string AgreementKey = "cho-agreement-v1";
        private const string NoticeSeenKey = "cho-notice-seen";
#if UNITY_WEBGL
        private const string ReleaseUrl = "https://bojuejun.github.io/cho-siren-preview/release.json";
#else
        // Installed builds read the China-fast Tencent COS manifest (GitHub is often unreachable in China).
        private const string ReleaseUrl = AppUpdate1022.ManifestUrl;
#endif

        // visual-a 1101c: one glance each (≤ 10 characters), under a light-bulb tag instead of 「TIPS」.
        private static readonly string[] Tips =
        {
            "点角色，她会回应你",
            "打不过？去练习室",
            "敌人蓄力 → 开守护",
            "连签七天，大礼在后",
            "入口亮点 = 有奖励",
            "大招就绪，光碟上滑",
            "不满意的骰子可重投",
        };

        private RectTransform root;
        private Image progressFill;
        private Text progressText;
        private Text tipText;
        private GameObject progressGroup;
        private Text progressLabel;
        private Image titleLogo;
        // In-game update (2026-10-22): offered and downloaded right here on the title, reusing the
        // loading bar, so the side buttons (notice / repair / sound) stay usable.
        private bool updating;
        private GameObject updatePanel;
        private CanvasGroup startGroup;
        private CanvasGroup group;
        private VideoPlayer video;
        private RenderTexture videoTexture;
        private RawImage videoImage;
        private AspectRatioFitter videoFit;
        private GameObject ageDialog;
        private Image soundIcon;
        private Sprite soundOn, soundOff;
        private bool soundEnabled;
        private GameAudio music;
        private GameObject agreementDialog;
        private GameObject noticeDot;
        private string latestVersion;
        private RectTransform wipePink, wipeBlack;
        private Sprite irisSprite;
        private Texture2D irisTexture;
        private bool wipeSwapped;
        private float shownAt;
        private float nextTipAt;
        private int tipIndex;
        private bool ready;
        private bool leaving;
        private bool videoFailed, applicationPaused;
        private float leaveStarted;

        public static NativeTitleScreen Create(Transform canvas, RectTransform safeArea, bool sound, GameAudio audio = null)
        {
            var node = new GameObject("NativeTitleScreen", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            node.transform.SetParent(canvas, false);
            var screen = node.AddComponent<NativeTitleScreen>();
            screen.music = audio != null ? audio : FindAnyObjectByType<GameAudio>();
            screen.Build(safeArea, sound);
            return screen;
        }

        public void SetProgress(float value)
        {
            if (updating) return; // the bar is showing the update download
            float clamped = Mathf.Clamp01(value);
            if (progressFill != null) progressFill.rectTransform.anchorMax = new Vector2(clamped, 1f);
            if (progressText != null) progressText.text = $"{Mathf.RoundToInt(clamped * 100f)}%";
        }

        /// <summary>True while the title is still on screen and can host the update offer.</summary>
        public bool CanHostUpdate => this != null && !leaving;
        /// <summary>The first-launch agreement is open; the update offer waits for it.</summary>
        public bool AgreementOpen => agreementDialog != null;

        /// <summary>
        /// Shows 「发现新版本」 on the title: the changes since the player's version in a slanted panel and
        /// 立即更新 / 稍后. The loading bar below is reused for the download progress.
        /// </summary>
        public void ShowUpdateOffer(string version, string changes, string confirmLabel, System.Action onUpdate, System.Action onLater)
        {
            updating = true;
            if (updatePanel != null) Destroy(updatePanel);
            progressGroup.SetActive(true);
            startGroup.alpha = 0f;
            if (titleLogo != null) titleLogo.gameObject.SetActive(false);
            SetUpdateProgress(0f, $"新版本 v{version}", "存档保留", false);

            updatePanel = new GameObject("UpdateOffer", typeof(RectTransform));
            updatePanel.transform.SetParent(safeFrame, false);
            RectTransform panel = (RectTransform)updatePanel.transform;
            Anchor(panel, new Vector2(.5f, 0), new Vector2(0f, 214f), new Vector2(600f, 380f), new Vector2(.5f, 0));
            Image back = Solid("Back", new Color(.04f, .02f, .08f, .93f), panel);
            Fill(back.rectTransform);
            // Slant the art, not the reading surface: small rotated Chinese glyphs lose strokes.
            back.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 2.5f);
            back.raycastTarget = true;
            Image band = Solid("Band", Brand1025.Teal, panel);
            Anchor(band.rectTransform, new Vector2(0, 1), new Vector2(-10f, 14f), new Vector2(300f, 52f));
            band.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -3f);
            Text head = Label("Head", "更新内容", 24, Brand1025.Ink, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic, band.rectTransform);
            Fill(head.rectTransform);
            ReadingArea(panel, "Changes", changes, new Vector2(26f, 58f), new Vector2(548f, 230f));
            if (confirmLabel == null)
            {
                // Automatic update: the panel is only a notice; closing it leaves the download running.
                Image close = Solid("Close", new Color(0f, 0f, 0f, .6f), panel);
                close.raycastTarget = true;
                Anchor(close.rectTransform, new Vector2(1, 1), new Vector2(-10f, -10f), new Vector2(44f, 44f), new Vector2(1, 1));
                Text cross = Label("Text", "×", 24, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold, close.rectTransform);
                Fill(cross.rectTransform);
                close.gameObject.AddComponent<Button>().onClick.AddListener(() =>
                {
                    ModalPolish038Chrome.PlayClick();
                    if (updatePanel != null) updatePanel.SetActive(false);
                    if (titleLogo != null) titleLogo.gameObject.SetActive(true);
                });
            }
            else
            {
                UpdateButton(panel, confirmLabel, new Vector2(150f, 34f), Brand1025.Teal, onUpdate);
                UpdateButton(panel, "稍后", new Vector2(-150f, 34f), new Color(.3f, .26f, .4f, 1f), () =>
                {
                    HideUpdateOffer();
                    onLater?.Invoke();
                });
            }
            if (agreementDialog != null) agreementDialog.transform.SetAsLastSibling();
        }

        /// <summary>Download progress on the loading bar (it stays level; only the panel is slanted).</summary>
        public void SetUpdateProgress(float value, string label, string detail, bool hideButtons = true)
        {
            updating = true;
            progressGroup.SetActive(true);
            startGroup.alpha = 0f;
            float clamped = Mathf.Clamp01(value);
            if (progressFill != null) progressFill.rectTransform.anchorMax = new Vector2(clamped, 1f);
            if (progressText != null) progressText.text = clamped > 0f ? $"{Mathf.RoundToInt(clamped * 100f)}%" : string.Empty;
            if (progressLabel != null) progressLabel.text = label;
            if (tipText != null) tipText.text = detail;
            if (hideButtons && updatePanel != null)
                foreach (Button button in updatePanel.GetComponentsInChildren<Button>())
                    if (button.name != "Close") button.gameObject.SetActive(false);
        }

        public void HideUpdateOffer()
        {
            updating = false;
            if (updatePanel != null) Destroy(updatePanel);
            updatePanel = null;
            if (titleLogo != null) titleLogo.gameObject.SetActive(true);
            if (progressLabel != null) progressLabel.text = "载入中";
            if (tipText != null) tipText.text = Tips[tipIndex];
            SetProgressSilently(ready ? 1f : 0f);
        }

        private void SetProgressSilently(float value)
        {
            if (progressFill != null) progressFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(value), 1f);
            if (progressText != null) progressText.text = $"{Mathf.RoundToInt(Mathf.Clamp01(value) * 100f)}%";
        }

        private void UpdateButton(RectTransform parent, string label, Vector2 position, Color color, System.Action action)
        {
            Image plate = Solid("Button-" + label, color, parent);
            plate.raycastTarget = true;
            Anchor(plate.rectTransform, new Vector2(.5f, 0), position, new Vector2(220f, 56f), new Vector2(.5f, 0));
            Button button = plate.gameObject.AddComponent<Button>();
            button.onClick.AddListener(() => { ModalPolish038Chrome.PlayClick(); action?.Invoke(); });
            Text text = Label("Text", label, 22, color == (Color)Brand1025.Teal ? (Color)Brand1025.Ink : Color.white, TextAnchor.MiddleCenter, FontStyle.Bold, plate.rectTransform);
            Fill(text.rectTransform);
        }

        /// <summary>The lobby is live underneath: switch the bar for TAP TO START.</summary>
        public void MarkReady()
        {
            SetProgress(1f);
            ready = true;
        }

        private void Build(RectTransform safeArea, bool sound)
        {
            root = (RectTransform)transform;
            Fill(root);
            transform.SetAsLastSibling();
            group = GetComponent<CanvasGroup>();
            Image blocker = GetComponent<Image>();
            blocker.color = new Color32(9, 6, 22, 255);
            blocker.raycastTarget = true;
            shownAt = Time.unscaledTime;

            // CG video, cover-fitted; the poster shows until the first frame is decoded.
            var videoNode = new GameObject("LoadingCg", typeof(RectTransform), typeof(RawImage));
            videoNode.transform.SetParent(transform, false);
            videoImage = videoNode.GetComponent<RawImage>();
            videoImage.raycastTarget = false;
            Texture2D poster = Resources.Load<Texture2D>("Art/Loading/loading-poster");
            videoImage.texture = poster;
            videoFit = videoNode.AddComponent<AspectRatioFitter>();
            videoFit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            videoFit.aspectRatio = poster != null ? poster.width / (float)poster.height : 9f / 16f;
            // The CG owns this screen's music; the global transport pauses underneath.
            soundEnabled = music != null ? music.MusicEnabled : sound;
            if (music != null) music.MusicEnabledChanged += SetSoundIcon;
            music?.SetMediaMusicActive(MusicOwner, true);
            StartVideo(sound);

            Shade("ShadeTop", true, .34f, .62f);
            Shade("ShadeBottom", false, .46f, .9f);

            // Every control lives in a frame that copies the safe area each frame, so the
            // top row sits just under the Dynamic Island and the bottom block just above the
            // home indicator on any phone. The CG and shades stay full screen behind it.
            safeSource = safeArea;
            var frameNode = new GameObject("SafeFrame", typeof(RectTransform));
            frameNode.transform.SetParent(transform, false);
            safeFrame = (RectTransform)frameNode.transform;
            safeFrame.offsetMin = safeFrame.offsetMax = Vector2.zero;
            SyncSafeFrame();
            const float top = 0f, bottom = 0f;
            int firstUi = transform.childCount;

            // Right-side title entries form one vertical column: notice, repair, sound, 16+.
            // The latest user screenshot takes precedence over the older top-left note.
            Image badge = Picture("AgeBadge", "Art/Loading/age-16");
            // Age rating: alone in the top-left corner (status-bar clock sits above it).
            Anchor(badge.rectTransform, new Vector2(0, 1), new Vector2(52f, -(top + 100f)), new Vector2(72f, 92f), new Vector2(.5f, 1f));
            badge.raycastTarget = true;
            Button badgeButton = badge.gameObject.AddComponent<Button>();
            badgeButton.transition = Selectable.Transition.None;
            badgeButton.onClick.AddListener(ShowAgeNotice);

            soundOn = SpriteFrom("Art/Loading/sound-on");
            soundOff = SpriteFrom("Art/Loading/sound-off");
            GameObject notice = SideButton("NoticeButton", "公告", SpriteFrom("Art/Loading/icon-notice"), ShowNotice, top + 96f, out _);
            SideButton("RepairButton", "修复", SpriteFrom("Art/Loading/icon-repair"), ShowRepair, top + 172f, out _);
            SideButton("SoundButton", "声音", null, ToggleSound, top + 248f, out soundIcon);
            RefreshSoundIcon();
            // 2026-10-31: the same torn-paper teal notice sticker as every other entry (was the red dot).
            noticeDot = NoticeSticker1031.Create("NoticeDot", notice.transform, 30f);
            Anchor((RectTransform)noticeDot.transform, new Vector2(.5f, 1), new Vector2(20f, -14f), new Vector2(16f, 16f));
            noticeDot.SetActive(false);
            StartCoroutine(CheckNotice());

            Image title = Picture("Title", "Art/Loading/loading-title");
            titleLogo = title;
            Anchor(title.rectTransform, new Vector2(.5f, 0), new Vector2(0f, bottom + 214f), new Vector2(330f, 154f), new Vector2(.5f, 0));

            // Bottom block (bottom-anchored, above the home indicator).
            Text health = Label("Health", "抵制不良游戏，拒绝盗版游戏。注意自我保护，谨防受骗上当。\n适度游戏益脑，沉迷游戏伤身。合理安排时间，享受健康生活。",
                13, new Color(1f, 1f, 1f, .62f), TextAnchor.MiddleCenter, FontStyle.Normal);
            Anchor(health.rectTransform, new Vector2(.5f, 0), new Vector2(0f, bottom + 6f), new Vector2(660f, 44f), new Vector2(.5f, 0));
            Text version = Label("Version", "v" + Application.version, 13, new Color(1f, 1f, 1f, .55f), TextAnchor.MiddleRight, FontStyle.Bold);
            Anchor(version.rectTransform, new Vector2(1, 0), new Vector2(-22f, bottom + 56f), new Vector2(200f, 24f), new Vector2(1, 0));

            progressGroup = new GameObject("Progress", typeof(RectTransform));
            progressGroup.transform.SetParent(transform, false);
            RectTransform progress = (RectTransform)progressGroup.transform;
            Anchor(progress, new Vector2(.5f, 0), new Vector2(0f, bottom + 92f), new Vector2(620f, 100f), new Vector2(.5f, 0));
            Text label = Label("Label", "载入中", 19, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold, progress);
            progressLabel = label;
            Anchor(label.rectTransform, new Vector2(0, 1), Vector2.zero, new Vector2(400f, 32f));
            progressText = Label("Percent", "0%", 24, PunkUi.Cyan, TextAnchor.MiddleRight, FontStyle.BoldAndItalic, progress);
            Anchor(progressText.rectTransform, new Vector2(1, 1), Vector2.zero, new Vector2(160f, 32f), new Vector2(1, 1));
            Image track = Solid("Track", new Color(1f, 1f, 1f, .2f), progress);
            Anchor(track.rectTransform, new Vector2(0, 1), new Vector2(0f, -42f), new Vector2(620f, 6f));
            progressFill = Solid("Fill", Brand1025.Teal, track.rectTransform);
            progressFill.rectTransform.anchorMin = Vector2.zero;
            progressFill.rectTransform.anchorMax = new Vector2(0f, 1f);
            progressFill.rectTransform.offsetMin = progressFill.rectTransform.offsetMax = Vector2.zero;
            // visual-a 1101c: a light-bulb sticker instead of the English 「TIPS」 tag.
            Image tipTag = Solid("TipTag", Brand1025.Teal, progress);
            tipTag.sprite = PunkUi.SlantFill;
            Anchor(tipTag.rectTransform, new Vector2(0, 1), new Vector2(0f, -64f), new Vector2(44f, 32f));
            RectTransform bulb = VisualA1101.NeonIcon(tipTag.rectTransform, "TipIcon", "sparkle", Brand1025.Ink, 24f, 0f);
            bulb.anchorMin = bulb.anchorMax = bulb.pivot = new Vector2(.5f, .5f);
            bulb.anchoredPosition = Vector2.zero;
            tipText = Label("Tip", Tips[0], 18, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold, progress);
            Anchor(tipText.rectTransform, new Vector2(0, 1), new Vector2(56f, -66f), new Vector2(564f, 28f));
            nextTipAt = Time.unscaledTime + 4f;

            var start = new GameObject("TapToStart", typeof(RectTransform), typeof(CanvasGroup));
            start.transform.SetParent(transform, false);
            RectTransform startRect = (RectTransform)start.transform;
            Anchor(startRect, new Vector2(.5f, 0), new Vector2(0f, bottom + 104f), new Vector2(620f, 48f), new Vector2(.5f, 0));
            startGroup = start.GetComponent<CanvasGroup>();
            startGroup.alpha = 0f;
            startGroup.blocksRaycasts = false;
            // A black slanted plate with a pink offset, like the web page's start prompt.
            Image plate = Solid("Plate", new Color(0f, 0f, 0f, .72f), startRect);
            plate.sprite = PunkUi.SlantFill;
            Anchor(plate.rectTransform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(300f, 48f));
            Shadow plateShadow = plate.gameObject.AddComponent<Shadow>();
            plateShadow.effectColor = Brand1025.Teal;
            plateShadow.effectDistance = new Vector2(4f, -4f);
            plateShadow.enabled = false;
            // 2026-10-20 (owner): no plate behind the prompt — plain words with a thin pink slash.
            plate.sprite = null;
            plate.color = Brand1025.Teal;
            Anchor(plate.rectTransform, new Vector2(.5f, .5f), new Vector2(0f, -20f), new Vector2(240f, 3f));
            plate.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            Text tapSub = Label("TapSub", "点击开始", 24, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold, startRect);
            Anchor(tapSub.rectTransform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(300f, 48f));
            Shadow tapShadow = tapSub.gameObject.AddComponent<Shadow>();
            tapShadow.effectColor = new Color(0f, 0f, 0f, .85f);
            tapShadow.effectDistance = new Vector2(1.5f, -2f);

            // Move every control created above into the safe-area frame.
            var controls = new System.Collections.Generic.List<Transform>();
            for (int i = firstUi; i < transform.childCount; i++) controls.Add(transform.GetChild(i));
            foreach (Transform control in controls) control.SetParent(safeFrame, false);

            if (PlayerPrefs.GetInt(AgreementKey, 0) != 1) ShowAgreement();
            // 2026-10-28 (少爷): after an update a returning player sees the notice open by itself, once. A relaunch of
            // the same version, or a first launch (nothing seen yet), keeps the red dot only.
            else
            {
                string seen = PlayerPrefs.GetString(NoticeSeenKey, string.Empty);
                if (!string.IsNullOrEmpty(seen) && seen != Application.version) ShowNotice();
            }
        }

        private void StartVideo(bool sound)
        {
            string path = Path.Combine(Application.streamingAssetsPath, VideoFile);
            video = gameObject.AddComponent<VideoPlayer>();
            video.playOnAwake = false;
            video.isLooping = true;
            video.source = VideoSource.Url;
            video.url = path;
            video.renderMode = VideoRenderMode.RenderTexture;
            videoTexture = new RenderTexture(720, 1280, 0) { name = "LoadingCg" };
            video.targetTexture = videoTexture;
            video.audioOutputMode = VideoAudioOutputMode.Direct;
            video.controlledAudioTrackCount = 1;
            video.EnableAudioTrack(0, true);
            video.SetDirectAudioVolume(0, .45f);
            video.SetDirectAudioMute(0, !soundEnabled);
            video.prepareCompleted += player =>
            {
                if (videoFailed || leaving) return;
                if (player.width > 0 && player.height > 0) videoFit.aspectRatio = player.width / (float)player.height;
                videoImage.texture = videoTexture;
                if (isActiveAndEnabled)
                {
                    music?.SetMediaMusicActive(MusicOwner, player.audioTrackCount > 0);
                    // Direct audio controls are applied again once the native track exists.
                    if (player.audioTrackCount > 0) player.SetDirectAudioVolume(0, .45f);
                    player.SetDirectAudioMute(0, !soundEnabled);
                    if (!applicationPaused) player.Play();
                }
            };
            video.errorReceived += (_, message) => FailVideo(message);
            video.Prepare();
            StartCoroutine(WaitForVideo());
        }

        private IEnumerator WaitForVideo()
        {
            float waited = 0f;
            while (video != null && !video.isPrepared && !videoFailed && !leaving)
            {
                if (!applicationPaused) waited += Time.unscaledDeltaTime;
                if (waited >= 15f) { FailVideo("prepare timed out; using the poster and global music"); yield break; }
                yield return null;
            }
        }

        private void FailVideo(string message)
        {
            if (videoFailed) return;
            videoFailed = true;
            if (video != null) { video.SetDirectAudioMute(0, true); video.Stop(); }
            music?.SetMediaMusicActive(MusicOwner, false);
            Debug.LogWarning("Loading CG: " + message);
        }

        private RectTransform safeFrame;
        private RectTransform safeSource;

        private void SyncSafeFrame()
        {
            if (safeFrame == null || safeSource == null) return;
            safeFrame.anchorMin = safeSource.anchorMin;
            safeFrame.anchorMax = safeSource.anchorMax;
        }

        private void LateUpdate() => SyncSafeFrame();

        private void Update()
        {
            if (leaving)
            {
                // One round monochrome iris. The title disappears only at full coverage.
                float t = Mathf.Clamp01((Time.unscaledTime - leaveStarted) / .62f);
                float e = t * t * (3f - 2f * t);
                float iris = t < .5f ? Mathf.SmoothStep(0f, 1f, t * 2f)
                    : Mathf.SmoothStep(1f, 0f, (t - .5f) * 2f);
                if (wipePink != null) wipePink.localScale = Vector3.one * iris;
                if (!wipeSwapped && t >= .5f)
                {
                    wipeSwapped = true;
                    if (video != null) video.Stop();
                    music?.SetMediaMusicActive(MusicOwner, false);
                    GetComponent<Image>().color = Color.clear;
                    foreach (Transform child in transform)
                        if (child != wipePink && child != wipeBlack) child.gameObject.SetActive(false);
                }
                if (t >= 1f) Destroy(gameObject);
                return;
            }

            if (ready && !updating && Time.unscaledTime - shownAt >= MinimumShowSeconds)
            {
                if (progressGroup.activeSelf) progressGroup.SetActive(false);
                startGroup.alpha = .72f + .28f * Mathf.Sin(Time.unscaledTime * 3f);
            }

            if (tipText != null && !updating && Time.unscaledTime >= nextTipAt)
            {
                tipIndex = (tipIndex + 1) % Tips.Length;
                tipText.text = Tips[tipIndex];
                nextTipAt = Time.unscaledTime + 4f;
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!ready || leaving || updating || ageDialog != null || Time.unscaledTime - shownAt < MinimumShowSeconds) return;
            if (agreementDialog != null) return;
            if (PlayerPrefs.GetInt(AgreementKey, 0) != 1) { ShowAgreement(); return; }
            ModalPolish038Chrome.PlayClick();
            leaving = true;
            leaveStarted = Time.unscaledTime;
            group.blocksRaycasts = false;
            BuildWipe();
        }

        private void BuildWipe()
        {
            Image iris = Solid("WipeDisc", SirenStickerUi.Violet);
            const int size = 256;
            var texture = irisTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + .5f, y + .5f), Vector2.one * size * .5f);
                pixels[y * size + x] = new Color32(255, 255, 255,
                    (byte)(255f * Mathf.Clamp01(size * .5f - 1f - distance)));
            }
            texture.SetPixels32(pixels); texture.Apply(false, true);
            iris.sprite = irisSprite = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f);
            wipePink = iris.rectTransform;
            wipeBlack = null;
            wipePink.anchorMin = wipePink.anchorMax = wipePink.pivot = Vector2.one * .5f;
            wipePink.sizeDelta = Vector2.one * Mathf.Max(root.rect.width, root.rect.height) * 1.5f;
            wipePink.anchoredPosition = Vector2.zero;
            wipePink.localScale = Vector3.zero;
            wipePink.SetAsLastSibling();
        }

        private IEnumerator CheckNotice()
        {
            using (UnityWebRequest request = UnityWebRequest.Get(ReleaseUrl + "?_=" + System.DateTime.UtcNow.Ticks))
            {
                request.timeout = 6;
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success) yield break;
                ReleaseInfo info = JsonUtility.FromJson<ReleaseInfo>(request.downloadHandler.text);
                if (info == null || string.IsNullOrEmpty(info.version)) yield break;
                latestVersion = info.version;
                if (noticeDot != null) noticeDot.SetActive(PlayerPrefs.GetString(NoticeSeenKey, string.Empty) != latestVersion);
            }
        }

        private void ShowAgreement()
        {
            if (agreementDialog != null) return;
            // visual-a 1101c: the three promises as pictures (免注册 / 无充值 / 本机存档 / 16+); the consent line and the
            // two documents stay as legal text.
            ModalPolish038Frame frame = ModalPolish038Chrome.Begin(transform, "AgreementDialog", "用户协议与隐私政策", null,
                null, 600f, closeName: "Close");
            agreementDialog = frame.Overlay;
            float width = frame.Width * (ModalPolish038Chrome.BodyRight - ModalPolish038Chrome.BodyLeft);
            (string icon, string caption)[] promises = { ("noaccount", "免注册"), ("nopay", "无充值"), ("device", "本机存档"), (null, "16+") };
            float tile = (width - 3 * 10f) / 4f;
            for (int i = 0; i < promises.Length; i++)
            {
                RectTransform cell = VisualA1101.Rect("Promise-" + i, frame.Body);
                VisualA1101.Place(cell, i * (tile + 10f), 0f, tile, 112f);
                Image plate = cell.gameObject.AddComponent<Image>();
                SirenStickerUi.SkinPanel(cell.gameObject, false);
                plate.raycastTarget = false;
                if (promises[i].icon != null)
                {
                    RectTransform glyph = VisualA1101.NeonIcon(cell, "Icon", promises[i].icon, VisualA1101.Teal, 52f);
                    VisualA1101.Place(glyph, (tile - 52f) * .5f, 12f, 52f, 52f);
                    Text caption = VisualA1101.Label(cell, "Caption", promises[i].caption, 16, VisualA1101.Paper);
                    VisualA1101.Place(caption.rectTransform, 0f, 72f, tile, 30f);
                }
                else
                {
                    Image age = Picture("AgeIcon", "Art/Loading/age-16");
                    age.transform.SetParent(cell, false);
                    age.raycastTarget = false;
                    VisualA1101.Place(age.rectTransform, (tile - 66f) * .5f, 12f, 66f, 84f);
                }
                cell.gameObject.AddComponent<VisualA1101Motion>().Pop(i * .06f);
            }
            // Legal wording stays on screen: consent, what is (not) collected, the age line and play-time reminder.
            Text body = ModalPolish038Chrome.Text(frame.Body, "Notice",
                "开始前请阅读并同意《用户协议》和《隐私政策》\n不收集姓名、手机号等个人信息\n适合 16 周岁及以上用户，请合理安排游戏时间",
                15, ModalPolish038Chrome.TextWhite, FontStyle.Normal, TextAnchor.MiddleCenter);
            body.lineSpacing = 1.15f;
            PanelKit.PlaceTop(body.rectTransform, 0f, 118f, width, 66f);
            float half = (width - 16f) * .5f;
            GameObject terms = ModalPolish038Chrome.ActionButton(frame.Body, "Terms", "《用户协议》", 16,
                () => ShowDocument(0), ModalPolish038ButtonStyle.Secondary);
            PanelKit.PlaceTop(terms.GetComponent<RectTransform>(), 0f, 184f, half, 48f);
            VisualA1101.DecorateAction(terms, "doc", 24f);
            GameObject privacy = ModalPolish038Chrome.ActionButton(frame.Body, "Privacy", "《隐私政策》", 16,
                () => ShowDocument(1), ModalPolish038ButtonStyle.Secondary);
            PanelKit.PlaceTop(privacy.GetComponent<RectTransform>(), half + 16f, 184f, half, 48f);
            VisualA1101.DecorateAction(privacy, "shield", 24f);
            Text hint = ModalPolish038Chrome.Text(frame.Body, "Hint", string.Empty, 15, new Color32(255, 190, 92, 255),
                FontStyle.Bold, TextAnchor.MiddleCenter);
            PanelKit.PlaceTop(hint.rectTransform, 0f, 238f, width, 24f);
            GameObject decline = ModalPolish038Chrome.ActionButton(frame.Body, "Decline", "不同意", 18, () =>
            {
                hint.text = "同意后才能进入";
                VisualA1101.Deny();
            }, ModalPolish038ButtonStyle.Ghost);
            PanelKit.PlaceTop(decline.GetComponent<RectTransform>(), 0f, 268f, half, 58f);
            VisualA1101.DecorateAction(decline, "close", 24f, VisualA1101.Muted);
            GameObject agree = ModalPolish038Chrome.ActionButton(frame.Body, "Agree", "同意", 20, () =>
            {
                PlayerPrefs.SetInt(AgreementKey, 1);
                PlayerPrefs.Save();
                VisualA1101.Success();
                Destroy(agreementDialog);
                agreementDialog = null;
            }, ModalPolish038ButtonStyle.Primary);
            PanelKit.PlaceTop(agree.GetComponent<RectTransform>(), half + 16f, 268f, half, 58f);
            VisualA1101.DecorateAction(agree, "check", 28f);
            ModalPolish038Chrome.FitHeight(frame, 330f);
        }

        private void ShowDocument(int index)
        {
            if (ageDialog != null) return;
            string[] titles = { "用户协议", "隐私政策" };
            string[] bodies =
            {
                "本游戏为试玩版本，供个人娱乐体验。\n· 游戏内容、数值与美术会随版本更新调整。\n· 存档保存在你的设备本机，删除 App 会删除存档。\n· 部分美术与视频素材由 AI 辅助生成。\n· 请合理安排游戏时间。",
                "试玩版不要求注册账号，也不收集你的姓名、手机号等个人信息。\n· 游戏进度只保存在本机。\n· 游戏不接入广告和第三方统计。\n· 「公告」会联网读取最新版本说明，不上传任何信息。",
            };
            ShowTextDialog(titles[index], null, bodies[index], "返回", null);
            ageDialog.transform.SetAsLastSibling();
        }

        /// <summary>Diamond icon button with a label pill underneath (NIKKE title-screen entry).</summary>
        private static Sprite dotSprite;
        private static Sprite DotSprite()
        {
            if (dotSprite != null) return dotSprite;
            const int n = 32; var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), Vector2.one * n / 2f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(n / 2f - d) * 255));
                }
            tex.SetPixels32(px); tex.Apply();
            return dotSprite = Sprite.Create(tex, new Rect(0, 0, n, n), Vector2.one * .5f, 100);
        }

        private GameObject SideButton(string name, string label, Sprite icon, UnityEngine.Events.UnityAction action, float y, out Image glyph)
        {
            var node = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            node.transform.SetParent(transform, false);
            RectTransform rect = (RectTransform)node.transform;
            // 2026-10-20: icons only (no plate, frame or caption); a soft shadow keeps them legible.
            Anchor(rect, new Vector2(1, 1), new Vector2(-14f, -y), new Vector2(64f, 64f), new Vector2(1f, 1f));
            Image hit = node.GetComponent<Image>();
            hit.color = new Color(0, 0, 0, 0.004f);
            Button button = node.GetComponent<Button>();
            button.targetGraphic = hit;
            button.onClick.AddListener(action);
            glyph = Solid("Icon", Color.white, rect);
            glyph.sprite = icon;
            glyph.preserveAspect = true;
            Anchor(glyph.rectTransform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(40f, 40f), new Vector2(.5f, .5f));
            Shadow shade = glyph.gameObject.AddComponent<Shadow>();
            shade.effectColor = new Color(0f, 0f, 0f, .7f);
            shade.effectDistance = new Vector2(1.5f, -2f);
            return node;
        }

        private void ToggleSound()
        {
            if (music != null) music.SetMusicEnabled(!music.MusicEnabled);
            else SetSoundIcon(!soundEnabled);
        }

        private void SetSoundIcon(bool enabled)
        {
            soundEnabled = enabled;
            if (video != null) video.SetDirectAudioMute(0, !enabled);
            RefreshSoundIcon();
        }

        private void RefreshSoundIcon()
        {
            if (soundIcon != null) soundIcon.sprite = soundEnabled ? soundOn : soundOff;
        }

        private void ShowNotice()
        {
            if (ageDialog != null) return;
            ShowTextDialog("公告", null, $"v{Application.version}\n\n…", "知道了", null);
            if (noticeDot != null) noticeDot.SetActive(false);
            PlayerPrefs.SetString(NoticeSeenKey, string.IsNullOrEmpty(latestVersion) ? Application.version : latestVersion);
            StartCoroutine(FetchNotice());
        }

        private IEnumerator FetchNotice()
        {
            using (UnityWebRequest request = UnityWebRequest.Get(ReleaseUrl + "?_=" + System.DateTime.UtcNow.Ticks))
            {
                request.timeout = 6;
                yield return request.SendWebRequest();
                if (ageDialog == null) yield break;
                Text body = ageDialog.GetComponentInChildren<Text>(true) != null
                    ? FindBody(ageDialog.transform) : null;
                if (body == null) yield break;
                if (request.result != UnityWebRequest.Result.Success)
                {
                    body.text = $"v{Application.version}\n\n网络不可用";
                    yield break;
                }
                ReleaseInfo info = JsonUtility.FromJson<ReleaseInfo>(request.downloadHandler.text);
                body.text = info == null ? $"v{Application.version}"
                    : $"{(info.version == Application.version ? "" : $"v{Application.version} → ")}v{info.version}{(string.IsNullOrEmpty(info.date) ? "" : "  " + info.date)}\n\n{(!string.IsNullOrEmpty(info.notice) ? info.notice : !string.IsNullOrEmpty(info.notes) ? info.notes : info.name)}";
            }
        }

        [System.Serializable]
        private sealed class ReleaseInfo
        {
            public string version;
            public string date;
            public string name;
            public string notice;
            public string notes; // app-version.json (installed builds)
        }

        private static Text FindBody(Transform root)
        {
            foreach (Text text in root.GetComponentsInChildren<Text>(true))
                if (text.name == "Notice") return text;
            return null;
        }

        private void ShowRepair()
        {
            if (ageDialog != null) return;
            // visual-a 1101c: a wrench, "clear cache → reload", "save kept" — the sentence is behind 「?」.
            ModalPolish038Frame frame = ModalPolish038Chrome.Begin(transform, "TitleDialog", "修复", null,
                () => { Destroy(ageDialog); ageDialog = null; }, 520f, closeName: "Close");
            ageDialog = frame.Overlay;
            float width = frame.Width * (ModalPolish038Chrome.BodyRight - ModalPolish038Chrome.BodyLeft);
            GameObject help = VisualA1101.HelpButton(frame.Panel, "RepairHelp", frame.Overlay.transform, 44f,
                ("wrench", "画面异常、卡住或显示不对时使用"),
                ("refresh", "清理本机临时缓存并重新载入游戏"),
                ("device", "存档和进度不受影响"));
            RectTransform helpRect = help.GetComponent<RectTransform>();
            helpRect.anchorMin = helpRect.anchorMax = helpRect.pivot = new Vector2(1f, 1f);
            helpRect.anchoredPosition = new Vector2(-frame.Width * (1f - ModalPolish038Chrome.BodyRight) - 64f, -frame.Height * .045f - 2f);
            RectTransform medal = VisualA1101.Medallion(frame.Body, "RepairMedal", "wrench", VisualA1101.Teal, 128f);
            VisualA1101.Place(medal, (width - 128f) * .5f, 0f, 128f, 128f);
            medal.gameObject.AddComponent<VisualA1101Motion>().Pop(0f);
            (string icon, string caption)[] steps = { ("refresh", "重新载入"), ("device", "存档保留") };
            float tile = (width - 14f) * .5f;
            for (int i = 0; i < steps.Length; i++)
            {
                RectTransform cell = VisualA1101.Rect("RepairStep-" + i, frame.Body);
                VisualA1101.Place(cell, i * (tile + 14f), 142f, tile, 60f);
                Image plate = cell.gameObject.AddComponent<Image>();
                SirenStickerUi.SkinPanel(cell.gameObject, false);
                plate.raycastTarget = false;
                RectTransform glyph = VisualA1101.NeonIcon(cell, "Icon", steps[i].icon, VisualA1101.Teal, 34f);
                VisualA1101.Place(glyph, 18f, 13f, 34f, 34f);
                Text caption = VisualA1101.Label(cell, "Caption", steps[i].caption, 18, VisualA1101.Paper, TextAnchor.MiddleLeft);
                VisualA1101.Place(caption.rectTransform, 62f, 0f, tile - 66f, 60f);
            }
            GameObject ok = ModalPolish038Chrome.ActionButton(frame.Body, "Ok", "修复", 20, () =>
            {
                Destroy(ageDialog);
                ageDialog = null;
#if !UNITY_WEBGL
                Caching.ClearCache();
#endif
                Resources.UnloadUnusedAssets();
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }, ModalPolish038ButtonStyle.Primary);
            PanelKit.PlaceTop(ok.GetComponent<RectTransform>(), (width - 240f) * .5f, 222f, 240f, 58f);
            VisualA1101.DecorateAction(ok, "wrench", 28f);
            ModalPolish038Chrome.FitHeight(frame, 284f);
        }

        private void ShowTextDialog(string title, string eyebrow, string text, string okLabel, System.Action onOk)
        {
            ModalPolish038Frame frame = ModalPolish038Chrome.Begin(transform, "TitleDialog", title, eyebrow,
                () => { Destroy(ageDialog); ageDialog = null; }, 600f, closeName: "Close");
            ageDialog = frame.Overlay;
            float width = frame.Width * (ModalPolish038Chrome.BodyRight - ModalPolish038Chrome.BodyLeft);
            ReadingArea(frame.Body, "Notice", text, Vector2.zero, new Vector2(width, 340f));
            GameObject ok = ModalPolish038Chrome.ActionButton(frame.Body, "Ok", okLabel, 18, () =>
            {
                Destroy(ageDialog);
                ageDialog = null;
                onOk?.Invoke();
            }, ModalPolish038ButtonStyle.Primary);
            PanelKit.PlaceTop(ok.GetComponent<RectTransform>(), (width - 240f) * .5f, 362f, 240f, 56f);
            ModalPolish038Chrome.FitHeight(frame, 432f);
        }

        private static Text ReadingArea(RectTransform parent, string name, string value, Vector2 at, Vector2 size)
        {
            var node = new GameObject(name + "Scroll", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            node.transform.SetParent(parent, false);
            RectTransform viewport = (RectTransform)node.transform;
            PanelKit.PlaceTop(viewport, at.x, at.y, size.x, size.y);
            node.GetComponent<Image>().color = Color.clear;
            Text body = ModalPolish038Chrome.Text(viewport, name, value, 26, Color.white,
                FontStyle.Normal, TextAnchor.UpperLeft);
            body.verticalOverflow = VerticalWrapMode.Overflow;
            body.lineSpacing = 1.38f;
            PanelKit.PlaceTop(body.rectTransform, 0f, 0f, size.x, 0f);
            body.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            ScrollRect scroll = node.GetComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = body.rectTransform;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;
            return body;
        }

        private static Sprite SpriteFrom(string resource)
        {
            Texture2D texture = Resources.Load<Texture2D>(resource);
            return texture == null ? null
                : Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
        }

        private void ShowAgeNotice()
        {
            if (ageDialog != null) return;
            ShowTextDialog("适龄提示", "CADPA 16+",
                "1.\u00A0本游戏是一款偶像养成与骰子策略战斗游戏，适用于年满 16 周岁及以上的用户，建议未成年人在家长监护下使用。\n\n" +
                "2.\u00A0游戏中有虚拟角色的舞台对战，没有血腥画面；部分角色服装较为时尚性感，没有现实暴力或不良引导内容。\n\n" +
                "3.\u00A0本作为试玩版，不需要注册账号、没有充值入口。请合理安排游戏时间，避免沉迷。",
                "知道了", null);
        }

        private void OnDestroy()
        {
            music?.SetMediaMusicActive(MusicOwner, false);
            if (music != null) music.MusicEnabledChanged -= SetSoundIcon;
            if (irisSprite != null) Destroy(irisSprite);
            if (irisTexture != null) Destroy(irisTexture);
            if (videoTexture != null)
            {
                videoTexture.Release();
                Destroy(videoTexture);
            }
        }

        // ------------------------------------------------------------------ helpers
        private void OnDisable()
        {
            if (video != null) { video.SetDirectAudioMute(0, true); video.Pause(); }
            music?.SetMediaMusicActive(MusicOwner, false);
        }

        private void OnEnable()
        {
            if (video == null || leaving || videoFailed) return;
            music?.SetMediaMusicActive(MusicOwner, !video.isPrepared || video.audioTrackCount > 0);
            video.SetDirectAudioMute(0, !soundEnabled);
            if (video.isPrepared && !applicationPaused) video.Play();
        }

        private void OnApplicationPause(bool paused)
        {
            applicationPaused = paused;
            if (video == null || leaving || videoFailed) return;
            if (paused) video.Pause();
            else if (isActiveAndEnabled && video.isPrepared) video.Play();
        }


        private static float SafeInset(RectTransform safeArea, bool top)
        {
            if (safeArea == null || !(safeArea.parent is RectTransform canvas)) return 0f;
            float height = canvas.rect.height;
            return top ? (1f - safeArea.anchorMax.y) * height : safeArea.anchorMin.y * height;
        }

        private void Shade(string name, bool top, float heightFraction, float alpha)
        {
            Texture2D ramp = new Texture2D(1, 32, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            var pixels = new Color32[32];
            for (int i = 0; i < 32; i++)
            {
                float t = i / 31f;
                float a = top ? t : 1f - t;
                pixels[i] = new Color32(8, 4, 20, (byte)(255f * alpha * a * a));
            }
            ramp.SetPixels32(pixels);
            ramp.Apply(false, true);
            var node = new GameObject(name, typeof(RectTransform), typeof(RawImage));
            node.transform.SetParent(transform, false);
            RawImage image = node.GetComponent<RawImage>();
            image.texture = ramp;
            image.raycastTarget = false;
            RectTransform rect = (RectTransform)node.transform;
            rect.anchorMin = new Vector2(0f, top ? 1f - heightFraction : 0f);
            rect.anchorMax = new Vector2(1f, top ? 1f : heightFraction);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private Image Picture(string name, string resource)
        {
            Texture2D texture = Resources.Load<Texture2D>(resource);
            Sprite sprite = texture != null
                ? Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect)
                : null;
            Image image = Solid(name, Color.white, root);
            image.sprite = sprite;
            image.preserveAspect = true;
            return image;
        }

        private Image Solid(string name, Color color, RectTransform parent = null)
        {
            var node = new GameObject(name, typeof(RectTransform), typeof(Image));
            node.transform.SetParent(parent != null ? parent : transform, false);
            Image image = node.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private Text Label(string name, string value, int size, Color color, TextAnchor alignment, FontStyle style,
            RectTransform parent = null)
        {
            Text text = ModalPolish038Chrome.Text(parent != null ? parent : transform, name, value, size, color, style, alignment);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static void Fill(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static void Anchor(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size, Vector2? pivot = null)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot ?? anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
