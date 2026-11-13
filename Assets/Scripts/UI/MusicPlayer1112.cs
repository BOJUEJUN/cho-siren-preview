using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ChoSiren.UI
{
    /// <summary>
    /// 2026-11-12 新播放器 (合作方原画同事 ~/Downloads/播放器.psd, 1024×744). The artist's single flattened layer is split by
    /// Tools/music-player-1112/split_player.py into Resources/Art/Music1112 — the panel with every baked word, number,
    /// meter, progress mark and button glyph painted out (player-base), the round 下一首 button, the six button glyphs, a
    /// pause glyph in the same style, the progress wedge (track / fill / lightning thumb) and a light that orbits the
    /// record's rim while a song plays. Everything the art showed as sample text comes from the live Home transport:
    /// title, artist, elapsed / total time, progress, play state and play mode.
    /// The component's own RectTransform is the PSD layer (1006×729, top-left pivot); callers place and scale it.
    /// Used twice: the lobby's music bar (top-left, quiet: no halo on hover, per AGENTS.md 2026-11-03) and the 唱片机 page.
    /// </summary>
    public sealed class MusicPlayer1112 : MonoBehaviour
    {
        public const string Art = "Art/Music1112/";
        public const float Width = 1006f, Height = 729f;
        // Progress wedge: left end (553, 457.5) → right end (882, 408) along its centre line.
        private const float BarX = 553f, BarW = 329f, BarY0 = 457.5f, BarRise = 49.5f;
        private static readonly Color32 Teal = new Color32(30, 232, 232, 255);
        private static readonly Color32 Violet = new Color32(166, 128, 240, 255);
        private static readonly Color32 Meter = new Color32(168, 72, 255, 255);
        private static readonly float[] RestLevels = { .2f, .35f, .2f, .5f, .3f, .85f };
        private static Sprite softDot;

        private GameAudio audio;
        private GameModel model;
        private Font font;
        private Action<string> toast;
        private Action userGesture;
        private bool quiet;

        private RectTransform rect, orbit, bolt, spin;
        private Image orbitImage, barFill, playIcon, loopIcon, shuffleIcon;
        private Text titleText, artistText, albumText, curTimeText, durTimeText, loopOne;
        private Image[] levelBars;
        private MusicSeekBar1101 seekBar;
        private Sprite playSprite, pauseSprite;
        private float orbitAlpha;
        private int shownCur = -1, shownDur = -1;

        /// <summary>Tapping the black panel (title / artist area). The lobby opens 唱片机 here.</summary>
        public Action PanelTapped;
        /// <summary>Tapping the record. The lobby plays / pauses here.</summary>
        public Action DiscTapped;

        public RectTransform Rect => rect;
        public string ShownTitle => titleText != null ? titleText.text : null;
        public string ShownElapsed => curTimeText != null ? curTimeText.text : null;
        public string ShownDuration => durTimeText != null ? durTimeText.text : null;
        public float ShownProgress => barFill != null ? barFill.fillAmount : 0f;

        public static MusicPlayer1112 Create(Transform parent, string name, GameAudio audio, GameModel model, Font font,
            Action<string> toast, Action userGesture, bool quietHover)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var r = (RectTransform)go.transform;
            r.SetParent(parent, false);
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
            r.sizeDelta = new Vector2(Width, Height);
            var player = go.AddComponent<MusicPlayer1112>();
            player.audio = audio; player.model = model; player.font = font;
            player.toast = toast; player.userGesture = userGesture; player.quiet = quietHover;
            player.rect = r;
            player.Build();
            player.Subscribe();
            player.Refresh();
            return player;
        }

        /// <summary>Places the player with its top-left at (x, y) of a top-left-anchored parent, scaled by k.</summary>
        public void Place(float x, float y, float k)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.localScale = new Vector3(k, k, 1f);
        }

        // ------------------------------------------------------------------ build

        private void Build()
        {
            Sprite baseSprite = Resources.Load<Sprite>(Art + "player-base");
            Image panel = NewImage("PlayerBase", baseSprite, 0f, 0f, Width, Height);
            if (baseSprite == null) panel.color = new Color32(20, 8, 40, 255);

            // 2026-11-12 (少爷: 猪这里是要转的，像光碟那样转): the record face with the mascot, cut from the base
            // (Tools/music-player-1112/cut_disc.py), turns like a disc while a song plays and stops on pause.
            Sprite spinSprite = Resources.Load<Sprite>(Art + "disc-spin");
            if (spinSprite != null)
            {
                spin = NewImage("RecordSpin", spinSprite, 123.5f, 96f, 408f, 408f).rectTransform;
                CenterPivot(spin);
            }

            // A light runs round the crystal record while a song plays (the glass dome and its mascot stay put).
            orbitImage = NewImage("RecordOrbit", Resources.Load<Sprite>(Art + "disc-orbit"), 323f - 230f, 309f - 230f, 460f, 460f);
            orbit = orbitImage.rectTransform;
            CenterPivot(orbit);
            orbitImage.color = new Color(1f, 1f, 1f, 0f);

            // Taps on the record and on the black panel (both optional, set by the owner).
            Button disc = NewButton("MusicDiscToggle", 327f - 170f, 300f - 170f, 340f, 340f, () => { if (DiscTapped != null) DiscTapped(); else TogglePause(); });
            disc.GetComponent<Image>().raycastTarget = true;
            NewButton("PanelOpen", 575f, 228f, 330f, 166f, () => PanelTapped?.Invoke());

            // Title, ARTIST / ALBUM on the black panel, left aligned like the art.
            titleText = NewText("SongTitle", string.Empty, 80, Color.white, FontStyle.Bold, TextAnchor.LowerLeft);
            Put(titleText.rectTransform, 588f, 246f, 238f, 128f);
            titleText.lineSpacing = .86f;
            artistText = NewText("SongArtist", string.Empty, 19, Violet, FontStyle.Bold, TextAnchor.MiddleLeft);
            Put(artistText.rectTransform, 588f, 376f, 236f, 24f);
            albumText = NewText("SongAlbum", "ALBUM/CHO-SIREN", 19, Violet, FontStyle.Bold, TextAnchor.MiddleLeft);
            Put(albumText.rectTransform, 590f, 399f, 236f, 24f);

            // Level meter (segmented violet bars, right of the title).
            levelBars = new Image[6];
            Sprite seg = Resources.Load<Sprite>(Art + "eq-seg");
            for (int i = 0; i < levelBars.Length; i++)
            {
                Image bar = NewImage("Level" + i, seg, 831f + i * 12.4f, 297f, 8f, 58f);
                bar.type = seg != null ? Image.Type.Tiled : Image.Type.Simple;
                bar.color = Meter;
                bar.rectTransform.pivot = new Vector2(.5f, 0f);
                bar.rectTransform.anchoredPosition = new Vector2(835f + i * 12.4f, -355f);
                levelBars[i] = bar;
            }

            // Progress wedge with the lightning thumb; tap or drag anywhere along it to seek.
            NewImage("ProgressTrack", Resources.Load<Sprite>(Art + "bar-track"), BarX, 403f, BarW, 62f);
            barFill = NewImage("ProgressFill", Resources.Load<Sprite>(Art + "bar-fill"), BarX, 403f, BarW, 62f);
            barFill.type = Image.Type.Filled;
            barFill.fillMethod = Image.FillMethod.Horizontal;
            barFill.fillOrigin = 0;
            barFill.fillAmount = 0f;
            bolt = NewImage("ProgressBolt", Resources.Load<Sprite>(Art + "bar-bolt"), BarX - 14f, BarY0 - 13.5f, 28f, 27f).rectTransform;
            CenterPivot(bolt);
            RectTransform seekRoot = NewRect("Seek");
            Put(seekRoot, BarX, 392f, BarW, 80f);
            Image seekHit = seekRoot.gameObject.AddComponent<Image>();
            seekHit.color = Color.clear;
            seekHit.raycastTarget = true;
            seekBar = seekRoot.gameObject.AddComponent<MusicSeekBar1101>();
            seekBar.Committed = value =>
            {
                userGesture?.Invoke();
                audio?.SeekHomeMusic(value);
            };
            curTimeText = NewText("Elapsed", "00:00", 21, Color.white, FontStyle.Bold, TextAnchor.MiddleLeft);
            Put(curTimeText.rectTransform, 546f, 462f, 120f, 28f);
            durTimeText = NewText("Duration", "00:00", 21, Color.white, FontStyle.Bold, TextAnchor.MiddleRight);
            Put(durTimeText.rectTransform, 762f, 422f, 120f, 28f);

            // The round 下一首 button left of the record; the button itself springs.
            Image roundGlow = Glow("RoundGlow", 134.5f, 481.5f, 300f);
            Image round = NewImage("RoundNextArt", Resources.Load<Sprite>(Art + "player-round"), 134.5f - 106f, 481.5f - 106f, 212f, 212f);
            CenterPivot(round.rectTransform);
            Image roundIcon = Icon("Icon", round.transform, "icon-round-next", 106f - 4.5f, 106f - 2.5f);
            roundIcon.color = Color.white;
            Button roundButton = NewButton("RoundNext", 134.5f - 100f, 481.5f - 100f, 200f, 200f, Next);
            Feedback(roundButton, round.rectTransform, roundGlow, 1.06f, .92f);

            // Loop · previous · play / pause · next · shuffle on the slanted strip.
            loopIcon = TransportButton("Loop", "icon-loop", 302f, 655.5f, 110f, 84f, ToggleSingleLoop);
            loopOne = NewText("One", "1", 18, Teal, FontStyle.Bold, TextAnchor.MiddleCenter);
            Put(loopOne.rectTransform, 318f, 628f, 22f, 22f);
            loopOne.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, .8f);
            TransportButton("Previous", "icon-prev", 417f, 636f, 110f, 84f, Previous);
            playSprite = Resources.Load<Sprite>(Art + "icon-play");
            pauseSprite = Resources.Load<Sprite>(Art + "icon-pause");
            playIcon = TransportButton("PlayPause", "icon-play", 555f, 613.5f, 136f, 128f, TogglePause);
            TransportButton("Next", "icon-next", 690f, 593f, 112f, 84f, Next);
            shuffleIcon = TransportButton("Shuffle", "icon-shuffle", 807.5f, 572.5f, 112f, 84f, ToggleShuffle);
        }

        private Image Glow(string name, float cx, float cy, float size)
        {
            Image glow = NewImage(name, SoftDot(), cx - size * .5f, cy - size * .5f, size, size);
            glow.color = new Color(Teal.r / 255f, Teal.g / 255f, Teal.b / 255f, 0f);
            glow.enabled = false;
            return glow;
        }

        private Image Icon(string name, Transform parent, string sprite, float cx, float cy)
        {
            Sprite s = Resources.Load<Sprite>(Art + sprite);
            Vector2 size = s != null ? s.rect.size : new Vector2(40f, 40f);
            Image image = NewImage(name, s, cx - size.x * .5f, cy - size.y * .5f, size.x, size.y, parent);
            CenterPivot(image.rectTransform);
            return image;
        }

        private Image TransportButton(string name, string sprite, float cx, float cy, float w, float h, Action onTap)
        {
            Image glow = Glow(name + "Glow", cx, cy, Mathf.Max(w, h) * 1.25f);
            Image icon = Icon(name + "Icon", rect, sprite, cx, cy);
            Button button = NewButton(name, cx - w * .5f, cy - h * .5f, w, h, onTap);
            Feedback(button, icon.rectTransform, glow, 1.16f, .82f);
            return icon;
        }

        private void Feedback(Button button, RectTransform target, Graphic glow, float hover, float press)
        {
            var fx = button.gameObject.AddComponent<MusicPressFx1112>();
            fx.Target = target;
            fx.Glow = quiet ? null : glow;
            if (quiet && glow != null) glow.gameObject.SetActive(false);
            fx.HoverScale = hover;
            fx.PressScale = press;
        }

        // ------------------------------------------------------------------ actions

        private bool CanControl => audio != null && audio.CurrentMusicContext == GameAudio.MusicContext.Home;

        private void Click()
        {
            userGesture?.Invoke();
            audio?.PlayClick();
        }

        public void Next()
        {
            Click();
            if (CanControl) audio.NextHomeMusic();
        }

        public void Previous()
        {
            Click();
            if (CanControl) audio.PreviousHomeMusic();
        }

        public void TogglePause()
        {
            Click();
            if (!CanControl) return;
            audio.ToggleHomeMusicPaused();
            if (!audio.MusicEnabled) toast?.Invoke("音乐已在设置中关闭");
        }

        /// <summary>The loop button: 单曲循环 on, or back to 列表循环.</summary>
        public void ToggleSingleLoop() => SetMode(model != null && model.MusicPlayMode == HomeMusicPlaylist.PlayMode.SingleLoop
            ? HomeMusicPlaylist.PlayMode.ListLoop : HomeMusicPlaylist.PlayMode.SingleLoop);

        /// <summary>The shuffle button: 随机播放 on, or back to 列表循环.</summary>
        public void ToggleShuffle() => SetMode(model != null && model.MusicPlayMode == HomeMusicPlaylist.PlayMode.Shuffle
            ? HomeMusicPlaylist.PlayMode.ListLoop : HomeMusicPlaylist.PlayMode.Shuffle);

        private void SetMode(HomeMusicPlaylist.PlayMode mode)
        {
            Click();
            if (model == null) return;
            model.SetMusicPlayMode(mode);
            toast?.Invoke("播放模式：" + HomeMusicPlaylist.PlayModeNames[(int)mode]);
        }

        // ------------------------------------------------------------------ refresh

        private void Subscribe()
        {
            if (audio != null)
            {
                audio.HomeMusicTrackChanged += OnTrack;
                audio.HomeMusicPausedChanged += OnFlag;
                audio.MusicEnabledChanged += OnFlag;
            }
            if (model != null) model.Changed += Refresh;
        }

        private void OnDestroy()
        {
            if (audio != null)
            {
                audio.HomeMusicTrackChanged -= OnTrack;
                audio.HomeMusicPausedChanged -= OnFlag;
                audio.MusicEnabledChanged -= OnFlag;
            }
            if (model != null) model.Changed -= Refresh;
        }

        private void OnTrack(int index) => Refresh();
        private void OnFlag(bool value) => Refresh();

        public void Refresh()
        {
            if (titleText == null) return;
            int index = audio != null ? audio.HomeMusicTrackIndex : 0;
            HomeMusicPlaylist.Track track = HomeMusicPlaylist.Get(index);
            SetTitle(track.Title);
            FitText(artistText, "ARTIST/" + (string.IsNullOrEmpty(track.Artist) ? "CHO-SIREN" : track.Artist), 236f);
            var mode = model != null ? model.MusicPlayMode : HomeMusicPlaylist.PlayMode.ListLoop;
            loopIcon.color = mode == HomeMusicPlaylist.PlayMode.SingleLoop ? (Color)Teal : Color.white;
            loopOne.gameObject.SetActive(mode == HomeMusicPlaylist.PlayMode.SingleLoop);
            shuffleIcon.color = mode == HomeMusicPlaylist.PlayMode.Shuffle ? (Color)Teal : Color.white;
            shownCur = shownDur = -1;
        }

        /// <summary>
        /// The art's big bold title: one line as large as fits (up to the art's 80 px), else two balanced lines
        /// (split at a space near the middle when there is one), else an ellipsis at the smallest size.
        /// </summary>
        private void SetTitle(string value)
        {
            const float w = 238f, h = 128f;
            value = string.IsNullOrEmpty(value) ? "—" : value.Trim();
            string[] options = { value, SplitTitle(value) };
            foreach (string option in options)
            {
                if (option == null) continue;
                for (int size = 80; size >= 34; size -= 2)
                {
                    titleText.fontSize = size;
                    titleText.text = option;
                    if (titleText.preferredWidth <= w && titleText.preferredHeight <= h) return;
                    if (option == value && size <= 56) break; // a long single line reads better as two lines
                }
            }
            titleText.fontSize = 34;
            string two = SplitTitle(value) ?? value;
            titleText.text = two;
            for (int n = two.Length - 1; n > 1 && titleText.preferredWidth > w; n--)
                titleText.text = two.Substring(0, n).TrimEnd() + "…";
        }

        private static string SplitTitle(string value)
        {
            if (value.Length < 2) return null;
            int middle = value.Length / 2, best = -1;
            for (int i = 1; i < value.Length - 1; i++)
                if (value[i] == ' ' && (best < 0 || Mathf.Abs(i - middle) < Mathf.Abs(best - middle))) best = i;
            if (best > 0 && Mathf.Abs(best - middle) <= value.Length / 3)
                return value.Substring(0, best).TrimEnd() + "\n" + value.Substring(best + 1).TrimStart();
            int cut = (value.Length + 1) / 2;
            return value.Substring(0, cut) + "\n" + value.Substring(cut);
        }

        private void LateUpdate()
        {
            if (titleText == null) return;
            float dt = Time.unscaledDeltaTime, t = Time.unscaledTime;
            bool playing = audio != null && audio.HomeMusicPlaying;
            bool motion = playing && !BattlePresentationOptions.ReduceMotion;
            Sprite glyph = playing ? pauseSprite : playSprite;
            if (glyph != null && playIcon.sprite != glyph)
            {
                playIcon.sprite = glyph;
                playIcon.rectTransform.sizeDelta = glyph.rect.size;
            }
            orbitAlpha = Mathf.MoveTowards(orbitAlpha, playing ? 1f : 0f, dt / .35f);
            orbitImage.color = new Color(1f, 1f, 1f, orbitAlpha * (.8f + .2f * Mathf.Sin(t * 3.1f)));
            orbitImage.enabled = orbitAlpha > .001f;
            if (motion) orbit.Rotate(0f, 0f, dt * 360f / 5f);
            if (motion && spin != null) spin.Rotate(0f, 0f, -dt * 360f / 6f);
            for (int i = 0; i < levelBars.Length; i++)
            {
                float k = motion
                    ? .25f + .75f * Mathf.Abs(Mathf.Sin(t * (2.3f + i * .7f) + i * 1.3f) * Mathf.Sin(t * (1.1f + i * .37f) + i))
                    : RestLevels[i];
                levelBars[i].rectTransform.sizeDelta = new Vector2(8f, Mathf.Round(Mathf.Lerp(6f, 58f, k) / 6f) * 6f);
            }

            float time = 0f, length = 0f;
            if (audio != null) audio.TryGetHomeMusicProgress(out time, out length);
            if (seekBar.Dragging) time = seekBar.Value * length;
            float p = length > 0f ? Mathf.Clamp01(time / length) : 0f;
            barFill.fillAmount = p;
            bolt.anchoredPosition = new Vector2(BarX + p * BarW, -(BarY0 - p * BarRise));
            bolt.localScale = seekBar.Dragging ? Vector3.one * 1.35f : Vector3.one;
            int cur = Mathf.FloorToInt(time), dur = Mathf.FloorToInt(length);
            if (cur != shownCur) { shownCur = cur; curTimeText.text = Clock(cur); }
            if (dur != shownDur) { shownDur = dur; durTimeText.text = Clock(dur); }
        }

        /// <summary>mm:ss with a leading zero, as the art shows (01:06 / 03:29).</summary>
        public static string Clock(int seconds)
        {
            seconds = Mathf.Max(0, seconds);
            return (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");
        }

        // ------------------------------------------------------------------ helpers (PSD layer pixels, y down)

        private static void Put(RectTransform r, float x, float y, float w, float h)
        {
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
            r.anchoredPosition = new Vector2(x, -y);
            r.sizeDelta = new Vector2(w, h);
        }

        private static void CenterPivot(RectTransform r)
        {
            Vector2 size = r.sizeDelta;
            r.pivot = new Vector2(.5f, .5f);
            r.anchoredPosition += new Vector2(size.x * .5f, -size.y * .5f);
        }

        private RectTransform NewRect(string name, Transform parent = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = gameObject.layer;
            var r = (RectTransform)go.transform;
            r.SetParent(parent != null ? parent : rect, false);
            return r;
        }

        private Image NewImage(string name, Sprite sprite, float x, float y, float w, float h, Transform parent = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = gameObject.layer;
            var r = (RectTransform)go.transform;
            r.SetParent(parent != null ? parent : rect, false);
            Put(r, x, y, w, h);
            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        private Text NewText(string name, string value, int size, Color color, FontStyle style, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.layer = gameObject.layer;
            go.transform.SetParent(rect, false);
            Text text = go.GetComponent<Text>();
            text.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.fontStyle = style;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.supportRichText = false;
            return text;
        }

        private Button NewButton(string name, float x, float y, float w, float h, Action onTap)
        {
            Image image = NewImage(name, null, x, y, w, h);
            image.color = new Color(0, 0, 0, 0);
            image.raycastTarget = true;
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => onTap());
            return button;
        }

        private static void FitText(Text text, string value, float width)
        {
            text.text = value;
            if (width <= 0f || text.preferredWidth <= width) return;
            for (int n = value.Length - 1; n > 0; n--)
            {
                text.text = value.Substring(0, n).TrimEnd() + "…";
                if (text.preferredWidth <= width) return;
            }
        }

        private static Sprite SoftDot()
        {
            if (softDot != null) return softDot;
            const int n = 64;
            var texture = new Texture2D(n, n, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(n * .5f, n * .5f)) / (n * .5f);
                    float a = Mathf.Clamp01((1f - d) / .28f);
                    pixels[y * n + x] = new Color32(255, 255, 255, (byte)(a * a * 255));
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return softDot = Sprite.Create(texture, new Rect(0, 0, n, n), new Vector2(.5f, .5f), 100f);
        }
    }

    /// <summary>
    /// 2026-11-12 player controls: hover grows the glyph (and lights a teal halo, except on the quiet lobby bar), press
    /// squeezes it, release springs it back past its size and settles. Only the artwork moves; the hit area stays put.
    /// </summary>
    public sealed class MusicPressFx1112 : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public RectTransform Target;
        public Graphic Glow;
        public float HoverScale = 1.15f, PressScale = .84f, GlowAlpha = .55f;
        private bool hovered, pressed;
        private float scale = 1f, velocity, glow;

        /// <summary>0 rest, 1 hover, 2 pressed.</summary>
        public int State => pressed ? 2 : hovered ? 1 : 0;
        public float CurrentScale => scale;
        public float CurrentGlow => glow;

        public void OnPointerEnter(PointerEventData eventData) => hovered = true;
        public void OnPointerExit(PointerEventData eventData) { hovered = false; pressed = false; }
        public void OnPointerDown(PointerEventData eventData) => pressed = true;
        public void OnPointerUp(PointerEventData eventData) => pressed = false;

        private void LateUpdate()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
            float goal = pressed ? PressScale : hovered ? HoverScale : 1f;
            velocity += (goal - scale) * 520f * dt;
            velocity *= Mathf.Exp(-20f * dt);
            scale += velocity * dt;
            glow = Mathf.MoveTowards(glow, pressed ? 1f : hovered ? .6f : 0f, dt * 7f);
            Apply();
        }

        private void Apply()
        {
            if (Target != null) Target.localScale = new Vector3(scale, scale, 1f);
            if (Glow != null)
            {
                Color c = Glow.color;
                c.a = glow * GlowAlpha;
                Glow.color = c;
                Glow.enabled = glow > .001f;
            }
        }

        private void OnDisable()
        {
            hovered = pressed = false;
            scale = 1f; velocity = 0f; glow = 0f;
            Apply();
        }
    }
}
