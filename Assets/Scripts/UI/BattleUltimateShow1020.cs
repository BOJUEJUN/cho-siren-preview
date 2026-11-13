using System;
using ChoSiren.Systems.Tactics;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace ChoSiren
{
    /// <summary>
    /// Manual ultimate (2026-10-20 request): the character's own animation fills the screen while
    /// approach rings close onto targets. Since 2026-10-25 (赵金: the single ring was hard to hit) it is
    /// three beats — tap anywhere as each ring lands (UltimateBeatChallenge1025); the grade from all three
    /// gives the existing +10% / +5% bonus. Characters without an approved clip get their portrait with the
    /// same beats. The whole show stays short and never owns combat state.
    /// </summary>
    public sealed class BattleUltimateShow1020 : MonoBehaviour
    {
        private const float RingStart = .5f;          // seconds after the cut-in
        private const float Tail = .7f;               // grade stamp time after the last beat
        private static float BeatsEnd => RingStart + UltimateBeatChallenge1025.DurationSeconds;
        private static float PortraitLength => BeatsEnd + Tail;
        private static readonly Color Green = Brand1025.Teal;
        private static readonly Color Cyan = new Color32(70, 230, 255, 255);
        private static readonly Color MissGrey = new Color(.75f, .72f, .85f);
        // One ring in the middle of the screen (少爷: three rings were too many to follow).
        private static readonly Vector2[] BeatSpots = { new Vector2(0, -60), new Vector2(150, 20), new Vector2(-30, -160) };
        private const float NoteSize = 420f;
        private const float RingRadius = .4f;           // the target ring, as a fraction of the shader quad's half-size
        private static readonly Vector4[] RingPalette =
        {
            new Vector4(.01f, .25f, .27f, 1f), new Vector4(.11f, .95f, .93f, 1f), new Vector4(.5f, 1f, .97f, 1f),
            new Vector4(.88f, 1f, 1f, 1f), new Vector4(.25f, .94f, 1f, 1f), new Vector4(.02f, .02f, .04f, 1f),
        };
        private static readonly string[] PaletteNames = { "_Deep", "_Main", "_Hot", "_Light", "_Acc", "_Ink" };

        /// <summary>One beat drawn with the Fx1023TapRing light shader: target ring, approach ring, burst.</summary>
        private sealed class Note
        {
            public RectTransform Root;
            public RawImage Target, Approach, Burst;
            public Text Label, Judge;
            public float JudgedAt = -1f;
        }

        private RectTransform root, stamp;
        private RawImage video;
        private Image portrait, shade;
        private Text stampText, combo;
        private readonly Note[] notes = new Note[UltimateBeatChallenge1025.Beats];
        private VideoPlayer player;
        private Func<bool> paused;
        private Action<UltimateTimingGrade> completed;
        private UltimateBeatChallenge1025 challenge;
        private float clock, length, stampClock = -1, stampDelay = -1, comboClock = -1;
        private int comboShown;
        private bool active, waitingForClip, clipEnded, clipRetried;
        private float prepareWait;

        public bool IsActive => active;
        public RectTransform Root => root;
        /// <summary>
        /// 2026-10-31: each judged beat with its tap offset (seconds, + = late). When something listens
        /// (UltimateGradeBurst1031 via the battle panel), it owns the grade's sound and big centre feedback,
        /// so the small ring label, the old hit/crit tick and the end stamp step aside.
        /// </summary>
        public event Action<UltimateTimingGrade, float> BeatJudged;

        // ---------------------------------------------------------------- cut-in (2026-10-25 redesign)
        // 少爷: the ult close-up and name card should use the small-skill art. The member's torn-paper
        // skill cut-in (the authored PSD group) slams in over a black wedge and a brand-teal band, and the
        // ultimate's name stamps across the band in ransom letters. Members without that art fall back to
        // the half-body avatar or the portrait. ~0.8 s, then the clip takes over.
        private const float CutInLength = .8f;
        private RectTransform cutIn, cutBand, cutWedge, cutArt, cutShadow, cutTitle, cutTag, cutLetterRoot;
        private Image cutArtImage, cutShadowImage, cutFlash, cutEmblem, cutEmblemBack;
        private Text cutName;
        private RectTransform[] cutLetters = new RectTransform[0];
        private Font cutFont;
        private float cutClock = -1f;
        private int cutStamps;
        private Action<string> cutSfx;

        public void ConfigureCutIn(Font font, Action<string> sfx)
        {
            if (root == null || cutIn != null) return;
            cutSfx = sfx; cutFont = font;
            cutIn = new GameObject("P5CutIn", typeof(RectTransform)).GetComponent<RectTransform>();
            cutIn.SetParent(root, false); Stretch(cutIn);
            var dark = NewChild<Image>("Dark", cutIn); dark.color = new Color(.02f, .01f, .04f, 1f); Stretch(dark.rectTransform);
            var tilt = new GameObject("Tilt", typeof(RectTransform)).GetComponent<RectTransform>();
            tilt.SetParent(cutIn, false); Centre(tilt, new Vector2(0, 40), new Vector2(1400, 900));
            tilt.localEulerAngles = new Vector3(0, 0, -12f);
            var wedge = NewChild<Image>("Wedge", tilt); wedge.color = Color.black; cutWedge = wedge.rectTransform;
            Centre(cutWedge, new Vector2(0, 120), new Vector2(1500, 640));
            var half = NewChild<Image>("Halftone", cutWedge); half.sprite = CelShapes.Halftone; half.type = Image.Type.Tiled;
            half.pixelsPerUnitMultiplier = 2f; half.color = Brand1025.TealA(80); Stretch(half.rectTransform);
            var band = NewChild<Image>("Band", tilt); band.color = Brand1025.Teal; cutBand = band.rectTransform;
            Centre(cutBand, new Vector2(0, -230), new Vector2(1500, 170));
            var edge = NewChild<Image>("Edge", cutBand); edge.color = Color.white;
            var er = edge.rectTransform; er.anchorMin = new Vector2(0, 1); er.anchorMax = Vector2.one; er.pivot = new Vector2(.5f, 1);
            er.sizeDelta = new Vector2(0, 10); er.anchoredPosition = Vector2.zero;
            var ink = NewChild<Image>("InkEdge", cutBand); ink.color = Brand1025.Ink;
            var ir = ink.rectTransform; ir.anchorMin = Vector2.zero; ir.anchorMax = new Vector2(1, 0); ir.pivot = new Vector2(.5f, 0);
            ir.sizeDelta = new Vector2(0, 8); ir.anchoredPosition = Vector2.zero;
            // The skill art keeps its own torn-paper edge; an ink silhouette under it gives the P5 drop.
            cutArt = new GameObject("Art", typeof(RectTransform)).GetComponent<RectTransform>();
            cutArt.SetParent(cutIn, false); Centre(cutArt, new Vector2(10, 170), new Vector2(780, 680));
            cutArt.localEulerAngles = new Vector3(0, 0, -4f);
            cutShadowImage = NewChild<Image>("Shadow", cutArt); cutShadowImage.preserveAspect = true;
            cutShadowImage.color = new Color(0, 0, 0, .85f); cutShadow = cutShadowImage.rectTransform; Stretch(cutShadow);
            cutArtImage = NewChild<Image>("Skill", cutArt); cutArtImage.preserveAspect = true; Stretch(cutArtImage.rectTransform);
            // Name card on the band (drawn after the art): ink 「ULTIMATE」 tag + her name, then the ransom title.
            cutTitle = new GameObject("Title", typeof(RectTransform)).GetComponent<RectTransform>();
            cutTitle.SetParent(cutIn, false); Centre(cutTitle, new Vector2(-48, -185), new Vector2(720, 220));
            cutTitle.localEulerAngles = new Vector3(0, 0, -12f);
            cutTag = new GameObject("Tag", typeof(RectTransform)).GetComponent<RectTransform>();
            cutTag.SetParent(cutTitle, false); Centre(cutTag, new Vector2(-150, 96), new Vector2(420, 44));
            P5Letters1023.Plate(cutTag, "Ink", new Vector2(-105, 0), new Vector2(190, 40), Brand1025.Ink, -14f);
            Text ult = NewText("Ultimate", cutTag, font, "ULTIMATE", 24, Brand1025.Teal);
            Centre(ult.rectTransform, new Vector2(-105, 0), new Vector2(190, 40)); ult.GetComponent<Outline>().enabled = false;
            cutName = NewText("Name", cutTag, font, "", 34, Color.white);
            cutName.alignment = TextAnchor.MiddleLeft; Centre(cutName.rectTransform, new Vector2(110, 0), new Vector2(220, 44));
            cutLetterRoot = new GameObject("Letters", typeof(RectTransform)).GetComponent<RectTransform>();
            cutLetterRoot.SetParent(cutTitle, false); Centre(cutLetterRoot, new Vector2(10, -4), Vector2.zero);
            // 2026-10-26 (赵金: "放大的时候没有出现魅语迷心那个图标的符号"): the ultimate's own PSD symbol stamps in
            // beside the title, so the close-up says which skill this is.
            cutEmblemBack = NewChild<Image>("EmblemBack", cutIn);
            cutEmblemBack.sprite = RadialBacking(); cutEmblemBack.color = new Color(0f, 0f, 0f, 0f);
            Centre(cutEmblemBack.rectTransform, new Vector2(232, -40), new Vector2(300, 300));
            cutEmblem = NewChild<Image>("Emblem", cutIn); cutEmblem.preserveAspect = true;
            Centre(cutEmblem.rectTransform, new Vector2(232, -40), new Vector2(210, 210));
            cutEmblem.rectTransform.localEulerAngles = new Vector3(0, 0, 8f);
            cutEmblem.gameObject.SetActive(false);
            cutFlash = NewChild<Image>("Flash", cutIn); cutFlash.color = new Color(1, 1, 1, 0); Stretch(cutFlash.rectTransform);
            // The root button must still receive taps through the cut-in.
            foreach (Graphic g in cutIn.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
            cutIn.gameObject.SetActive(false);
        }

        private Sprite cutArtOverride;
        public void SetCutInArt(Sprite art) => cutArtOverride = art;

        /// <summary>The ultimate's PSD symbol (heart lock, star burst, shield, dagger) for the close-up; null hides it.</summary>
        public void SetCutInEmblem(Sprite emblem)
        {
            if (cutEmblem == null) return;
            cutEmblem.sprite = emblem;
            cutEmblem.gameObject.SetActive(emblem != null);
            if (cutEmblemBack != null) cutEmblemBack.color = new Color(0f, 0f, 0f, 0f);
        }

        private static Sprite radialBacking;
        private static Sprite RadialBacking()
        {
            if (radialBacking != null) return radialBacking;
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + .5f) / n * 2f - 1f, dy = (y + .5f) / n * 2f - 1f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                px[y * n + x] = new Color32(255, 255, 255, (byte)(255 * Mathf.SmoothStep(0f, 1f, a * 1.6f)));
            }
            tex.SetPixels32(px); tex.Apply(false, true);
            return radialBacking = Sprite.Create(tex, new Rect(0, 0, n, n), Vector2.one * .5f, 100f);
        }

        public void SetCutIn(string memberName, string ultimateName)
        {
            if (cutIn == null) return;
            cutName.text = memberName ?? string.Empty;
            for (int i = cutLetterRoot.childCount - 1; i >= 0; i--) Destroy(cutLetterRoot.GetChild(i).gameObject);
            string title = string.IsNullOrEmpty(ultimateName) ? "大招!" : ultimateName + "!";
            float size = Mathf.Min(104f, 620f / (title.Length * .92f));
            cutLetters = P5Letters1023.Build(cutLetterRoot, cutFont, title, size, title.GetHashCode() & 0xffff);
            foreach (RectTransform letter in cutLetters)
                foreach (Graphic g in letter.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
        }

        private static float OutExpo(float t) { t = Mathf.Clamp01(t); return t >= 1f ? 1f : 1f - Mathf.Pow(2f, -10f * t); }

        private void PoseCutIn()
        {
            if (cutIn == null || cutClock < 0f) return;
            float t = cutClock;
            cutWedge.anchoredPosition = new Vector2(Mathf.Lerp(-1500f, 0f, OutExpo(t / .12f)), 120f);
            cutBand.anchoredPosition = new Vector2(Mathf.Lerp(1500f, 0f, OutExpo((t - .04f) / .12f)), -230f);
            float land = OutExpo((t - .08f) / .2f);
            cutArt.anchoredPosition = new Vector2(Mathf.Lerp(780f, 10f, land) - 30f * Mathf.Clamp01(t - .28f), 170f);
            cutArt.localScale = Vector3.one * Mathf.Lerp(1.25f, 1f, land);
            cutArtImage.color = new Color(1, 1, 1, Mathf.Clamp01((t - .08f) / .05f));
            cutShadowImage.color = new Color(0, 0, 0, .85f * Mathf.Clamp01((t - .08f) / .05f));
            cutShadow.anchoredPosition = Vector2.Lerp(Vector2.zero, new Vector2(18f, -18f), land);
            // A white hit-frame as the art lands.
            cutFlash.color = new Color(1, 1, 1, t < .28f ? 0f : .55f * (1f - Mathf.Clamp01((t - .28f) / .12f)));
            cutTag.localScale = Vector3.one * (t < .26f ? 0f : Mathf.Lerp(1.6f, 1f, Mathf.Clamp01((t - .26f) / .06f)));
            if (cutEmblem != null && cutEmblem.gameObject.activeSelf)
            {
                float stamp = t < .42f ? 0f : OutExpo((t - .42f) / .1f);
                cutEmblem.rectTransform.localScale = Vector3.one * (t < .42f ? 0f : Mathf.Lerp(2.2f, 1f, stamp));
                cutEmblem.color = new Color(1, 1, 1, stamp);
                cutEmblemBack.color = new Color(0f, 0f, 0f, .7f * stamp);
            }
            for (int i = 0; i < cutLetters.Length; i++)
            {
                float at = .3f + i * .05f;
                cutLetters[i].localScale = Vector3.one * (t < at ? 0f : Mathf.Lerp(1.9f, 1f, OutExpo((t - at) / .07f)));
                bool loud = i == 0 || i == cutLetters.Length - 1;
                if (t >= at && cutStamps <= i) { cutStamps = i + 1; if (loud) cutSfx?.Invoke("stamp"); }
            }
            // Exit: everything snaps away to the left in the last 0.1 s, revealing the clip.
            float exit = Mathf.Clamp01((t - (CutInLength - .1f)) / .1f);
            cutIn.anchoredPosition = new Vector2(-900f * exit * exit, 0f);
            if (t >= CutInLength) { cutClock = -1f; cutIn.gameObject.SetActive(false); cutIn.anchoredPosition = Vector2.zero; }
        }

        public void Configure(Transform host, Font font, Func<bool> isPaused)
        {
            paused = isPaused;
            var go = new GameObject("UltimateShow1020", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(host, false);
            root = (RectTransform)go.transform;
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = root.offsetMax = Vector2.zero;
            shade = go.GetComponent<Image>(); shade.color = new Color(0.03f, 0.01f, 0.08f, .92f);
            var button = go.GetComponent<Button>(); button.transition = Selectable.Transition.None; button.onClick.AddListener(Tap);

            video = NewChild<RawImage>("Clip", root); Stretch(video.rectTransform); video.raycastTarget = false;
            var fit = video.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; fit.aspectRatio = 672f / 1536f;
            portrait = NewChild<Image>("Portrait", root); portrait.preserveAspect = true; portrait.raycastTarget = false;
            Centre(portrait.rectTransform, new Vector2(0, 80), new Vector2(620, 900));

            for (int i = 0; i < notes.Length; i++) notes[i] = BuildNote(i, font);
            combo = NewText("Combo", root, font, "", 50, Green);
            Centre(combo.rectTransform, new Vector2(0, 400), new Vector2(600, 80));
            combo.GetComponent<Outline>().effectDistance = new Vector2(4, -4);
            combo.gameObject.SetActive(false);

            stamp = NewChild<Image>("Stamp", root).rectTransform;
            stamp.GetComponent<Image>().color = Color.black; stamp.GetComponent<Image>().raycastTarget = false;
            Centre(stamp, new Vector2(0, -40), new Vector2(560, 187));
            stamp.localEulerAngles = new Vector3(0, 0, -3f);
            stampText = NewText("Grade", stamp, font, "", 56, Green);
            Stretch(stampText.rectTransform);

            root.gameObject.SetActive(false);
        }

        private Note BuildNote(int index, Font font)
        {
            var note = new Note();
            note.Root = new GameObject("Beat-" + (index + 1), typeof(RectTransform)).GetComponent<RectTransform>();
            note.Root.SetParent(root, false); Centre(note.Root, BeatSpots[index], Vector2.one * NoteSize);
            note.Target = LightRing("Target", note.Root, 1.1f);
            note.Approach = LightRing("Approach", note.Root, 2.6f);
            note.Burst = LightRing("Burst", note.Root, 2.6f);
            note.Label = NewText("Label", note.Root, font, "TAP", 54, Color.white); Stretch(note.Label.rectTransform);
            note.Judge = NewText("Judge", note.Root, font, "", 46, Green);
            Centre(note.Judge.rectTransform, new Vector2(0, NoteSize * .72f), new Vector2(380, 70));
            foreach (Graphic g in note.Root.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
            note.Root.gameObject.SetActive(false);
            return note;
        }

        private static Shader ringShader;
        private readonly System.Collections.Generic.List<Material> ringMaterials = new System.Collections.Generic.List<Material>();

        /// <summary>A quad <paramref name="span"/> notes wide running the light-ring shader (additive).</summary>
        private RawImage LightRing(string name, Transform parent, float span)
        {
            var image = NewChild<RawImage>(name, parent);
            Centre(image.rectTransform, Vector2.zero, Vector2.one * NoteSize * span);
            if (ringShader == null) ringShader = Resources.Load<Shader>("Shaders/Fx1023TapRing");
            if (ringShader != null)
            {
                var m = new Material(ringShader) { hideFlags = HideFlags.DontSave };
                m.SetFloat("_SrcBlend", 1f); m.SetFloat("_DstBlend", 1f);
                m.SetFloat("_Seed", UnityEngine.Random.value * 10f);
                for (int i = 0; i < PaletteNames.Length; i++) m.SetVector(PaletteNames[i], RingPalette[i]);
                image.material = m; ringMaterials.Add(m);
            }
            image.color = Color.white;
            return image;
        }

        /// <summary>Radius in the target ring's units (1 = on the target), thickness, flash and alpha.</summary>
        private void PoseRing(RawImage image, float span, float radius, float thick, float flash, float alpha)
        {
            Material m = image.material;
            if (m == null) return;
            image.enabled = alpha > .001f;
            m.SetFloat("_R", Mathf.Clamp(RingRadius * 1.1f * radius / span, 0f, .99f));
            m.SetFloat("_Thick", thick / span);
            m.SetFloat("_Flash", flash);
            m.SetFloat("_Alpha", alpha);
            m.SetFloat("_FxTime", clock);
        }

        // One player + texture per clip so every member's first ultimate starts at once.
        private readonly System.Collections.Generic.Dictionary<string, VideoPlayer> players =
            new System.Collections.Generic.Dictionary<string, VideoPlayer>();
        private readonly System.Collections.Generic.List<RenderTexture> textures = new System.Collections.Generic.List<RenderTexture>();

        private static string ClipUrl(string clipPath)
        {
            string url = Application.streamingAssetsPath.TrimEnd('/') + "/UltimateVideos/" + clipPath;
#if !UNITY_WEBGL || UNITY_EDITOR
            // Native players need a file:// URI for local media.
            if (!url.Contains("://")) url = new System.Uri(System.IO.Path.GetFullPath(url)).AbsoluteUri;
#endif
            return url;
        }

        private VideoPlayer PlayerFor(string clipPath)
        {
            string url = ClipUrl(clipPath);
            if (players.TryGetValue(url, out VideoPlayer existing)) return existing;
            // On the always-active host: a VideoPlayer on the hidden layer cannot Prepare.
            VideoPlayer vp = gameObject.AddComponent<VideoPlayer>();
            vp.playOnAwake = false; vp.isLooping = false; vp.skipOnDrop = true; vp.waitForFirstFrame = true;
            vp.source = VideoSource.Url; vp.renderMode = VideoRenderMode.RenderTexture;
            vp.audioOutputMode = VideoAudioOutputMode.None;
            vp.prepareCompleted += source =>
            {
                if (source.targetTexture == null)
                {
                    var rt = new RenderTexture(Mathf.Max(2, (int)source.width), Mathf.Max(2, (int)source.height), 0,
                        RenderTextureFormat.ARGB32) { name = "UltimateShow-" + clipPath };
                    rt.Create(); textures.Add(rt); source.targetTexture = rt;
                }
                if (source == player && waitingForClip) { waitingForClip = false; ShowClip(source); }
            };
            vp.errorReceived += (source, message) =>
            {
                Debug.LogWarning("[UltShow] clip error " + clipPath + ": " + message);
                if (source != player || !active) return;
                // 2026-11-12 (少爷: "这个角色的大招动画怎么没有了"): one decoder error no longer drops the clip for the
                // portrait; the clip gets one fresh prepare before the still takes over.
                if (!clipRetried) { clipRetried = true; waitingForClip = true; prepareWait = 0f; source.Stop(); source.Prepare(); return; }
                waitingForClip = false; video.enabled = false; ShowPortraitFallback();
            };
            vp.loopPointReached += source =>
            {
                if (source == player && active) clipEnded = true;
            };
            vp.url = url;
            players[url] = vp;
            return vp;
        }

        private void ShowClip(VideoPlayer source)
        {
            clock = 0f; prepareWait = 0f; clipEnded = false;
            video.texture = source.targetTexture;
            var fit = video.GetComponent<AspectRatioFitter>();
            if (source.height > 0) fit.aspectRatio = source.width / (float)source.height;
            source.time = 0; source.Play();
        }

        /// <summary>Decode the clip ahead of time (first decode can exceed a second on phones).</summary>
        public void Warm(string clipPath)
        {
            if (!isActiveAndEnabled || string.IsNullOrEmpty(clipPath) || active) return;
            VideoPlayer vp = PlayerFor(clipPath);
            if (!vp.isPrepared) vp.Prepare();
        }

        /// <summary>Clip path is relative to StreamingAssets/UltimateVideos; null shows the portrait.</summary>
        private bool autoAssist;

        /// <param name="assist">Auto battle: a beat the player does not tap is hit late on GOOD.</param>
        public bool Begin(string clipPath, float clipLength, Sprite portraitSprite, Action<UltimateTimingGrade> onComplete,
            bool assist = false)
        {
            autoAssist = assist;
            if (root == null || active || onComplete == null) return false;
            completed = onComplete;
            challenge = new UltimateBeatChallenge1025();
            clock = 0; stampClock = stampDelay = comboClock = -1; comboShown = 0; active = true;
            portrait.sprite = portraitSprite;
            bool reduced = BattlePresentationOptions.ReduceMotion;
            bool clip = !string.IsNullOrEmpty(clipPath) && !reduced;
            video.enabled = clip; portrait.enabled = !clip && portraitSprite != null;
            clipEnded = false; prepareWait = 0f; clipRetried = false;
            length = clip ? Mathf.Max(clipLength, BeatsEnd + .6f) : PortraitLength;
            if (clip)
            {
                player = PlayerFor(clipPath);
                if (player.isPrepared && player.targetTexture != null) { waitingForClip = false; ShowClip(player); }
                else { waitingForClip = true; player.Prepare(); }
            }
            else player = null;
            foreach (Note note in notes) { note.JudgedAt = -1f; note.Root.gameObject.SetActive(false); }
            combo.gameObject.SetActive(false);
            stamp.gameObject.SetActive(false);
            root.gameObject.SetActive(true); root.SetAsLastSibling();
            Sprite cutSprite = cutArtOverride != null ? cutArtOverride : portraitSprite;
            if (cutIn != null && !reduced && cutSprite != null)
            {
                cutArtImage.sprite = cutShadowImage.sprite = cutSprite;
                cutClock = 0f; cutStamps = 0;
                cutIn.gameObject.SetActive(true); cutIn.SetAsLastSibling();
                if (player != null && player.isPlaying) player.Pause();
                PoseCutIn();
            }
            return true;
        }

        private void ShowPortraitFallback()
        {
            if (!active) return;
            portrait.enabled = portrait.sprite != null;
            length = Mathf.Min(length, PortraitLength + clock);
        }

        public void Tap()
        {
            if (!active || cutClock >= 0f || waitingForClip || challenge.Resolved || (paused?.Invoke() ?? false)) return;
            int beat = challenge.Hit();
            if (beat >= 0) Judged(beat);
        }

        private static Color GradeColor(UltimateTimingGrade grade) =>
            grade == UltimateTimingGrade.Perfect ? Green : grade == UltimateTimingGrade.Good ? Cyan : MissGrey;

        private void Judged(int beat)
        {
            Note note = notes[beat];
            note.JudgedAt = clock;
            UltimateTimingGrade grade = challenge.GradeOf(beat);
            bool burst = BeatJudged != null;
            note.Judge.text = burst ? string.Empty
                : grade == UltimateTimingGrade.Perfect ? "PERFECT" : grade == UltimateTimingGrade.Good ? "GOOD" : "MISS";
            note.Judge.color = GradeColor(grade);
            if (!burst && grade != UltimateTimingGrade.Miss) cutSfx?.Invoke(grade == UltimateTimingGrade.Perfect ? "crit" : "hit");
            BeatJudged?.Invoke(grade, challenge.Elapsed - UltimateBeatChallenge1025.BeatTime(beat));
            comboShown = grade == UltimateTimingGrade.Miss ? 0 : comboShown + 1;
            if (comboShown >= 2) { combo.text = "COMBO ×" + comboShown; combo.gameObject.SetActive(true); comboClock = 0f; }
            if (challenge.Resolved) stampDelay = .18f;
        }

        private void ShowStamp()
        {
            if (BeatJudged != null) return; // the 1031 grade burst already said it, bigger
            stamp.gameObject.SetActive(true); stamp.SetAsLastSibling(); stampClock = 0;
            UltimateTimingGrade grade = challenge.Grade;
            stampText.text = grade == UltimateTimingGrade.Perfect ? "PERFECT" : grade == UltimateTimingGrade.Good ? "GOOD" : "MISS";
            stampText.color = GradeColor(grade);
            // 2026-10-20 (owner pick D1): transparent torn-paper word art, no black bar.
            Sprite art = GradeArt(grade);
            Image plate = stamp.GetComponent<Image>();
            plate.sprite = art; plate.preserveAspect = true;
            plate.color = art != null ? Color.white : Color.black;
            stampText.enabled = art == null;
        }

        private static readonly System.Collections.Generic.Dictionary<UltimateTimingGrade, Sprite> gradeArt =
            new System.Collections.Generic.Dictionary<UltimateTimingGrade, Sprite>();
        public static Sprite GradeArt(UltimateTimingGrade grade)
        {
            if (gradeArt.TryGetValue(grade, out Sprite cached) && cached != null) return cached;
            string name = grade == UltimateTimingGrade.Perfect ? "perfect" : grade == UltimateTimingGrade.Good ? "good" : "miss";
            Texture2D texture = Resources.Load<Texture2D>("UI/GradeStamps/grade-" + name);
            if (texture == null) return null;
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 100f);
            gradeArt[grade] = sprite;
            return sprite;
        }

        private void Update()
        {
            if (!active) return;
            bool stopped = paused?.Invoke() ?? false;
            if (stopped) { if (player != null && player.isPlaying) player.Pause(); return; }
            if (cutClock < 0f && player != null && video.enabled && !waitingForClip && !clipEnded && !player.isPlaying && player.isPrepared && clock < length + 8f) player.Play();
            float dt = Mathf.Min(Time.unscaledDeltaTime, .1f);
            // The cut-in plays first; the clip and the beats wait for it.
            if (cutClock >= 0f)
            {
                cutClock += dt;
                PoseCutIn();
                if (cutClock >= 0f) { if (player != null && player.isPlaying) player.Pause(); return; }
            }
            // A slow clip decode holds the show (up to a second) instead of running the beats early.
            if (waitingForClip)
            {
                prepareWait += dt;
                if (prepareWait > 2.5f && !clipRetried && player != null)
                {
                    Debug.LogWarning("[UltShow] clip slow to prepare, retrying " + player.url);
                    clipRetried = true; prepareWait = 0f; player.Stop(); player.Prepare();
                    return;
                }
                else if (prepareWait > 2.5f)
                {
                    Debug.LogWarning("[UltShow] clip unavailable, showing the portrait " + player?.url);
                    waitingForClip = false; video.enabled = false; if (player != null) player.Stop(); ShowPortraitFallback(); clock = 0;
                }
                else return;
            }
            clock += dt;
            if (portrait.enabled)
            {
                float k = Mathf.Clamp01(clock / .35f);
                portrait.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.15f, 1f, 1f - Mathf.Pow(1f - k, 3f));
                portrait.color = new Color(1, 1, 1, k);
            }
            if (clock >= RingStart && !challenge.Resolved)
            {
                int before = challenge.Next;
                challenge.Advance(dt);
                for (int beat = before; beat < challenge.Next; beat++) Judged(beat);
                // Auto battle taps late in the GOOD window, so the player can still beat it to PERFECT.
                if (autoAssist && !challenge.Resolved && challenge.Elapsed >= UltimateBeatChallenge1025.BeatTime(challenge.Next) +
                    (UltimateBeatChallenge1025.PerfectWindow + UltimateBeatChallenge1025.GoodWindow) * .5f)
                {
                    int beat = challenge.Hit();
                    if (beat >= 0) Judged(beat);
                }
            }
            PoseNotes();
            if (comboClock >= 0f)
            {
                comboClock += dt;
                combo.transform.localScale = Vector3.one * Mathf.Lerp(1.6f, 1f, Mathf.Clamp01(comboClock / .1f));
                Color c = combo.color; c.a = 1f - Mathf.Clamp01((comboClock - .6f) / .3f); combo.color = c;
                if (comboClock > .9f) { comboClock = -1f; combo.gameObject.SetActive(false); }
            }
            if (stampDelay >= 0f) { stampDelay -= dt; if (stampDelay < 0f) ShowStamp(); }
            if (stampClock >= 0)
            {
                stampClock += dt;
                float s = Mathf.Clamp01(stampClock / .18f);
                stamp.localScale = Vector3.one * Mathf.Lerp(1.8f, 1f, s);
            }
            bool showingClip = video.enabled && player != null;
            // The supplied animation owns its ending. Do not fade or truncate its last frames
            // just because the QTE has finished or a manifest duration was rounded down.
            float fade = showingClip ? 1f : Mathf.Clamp01((length - clock) / .25f);
            shade.color = new Color(0.03f, 0.01f, 0.08f, .92f * fade);
            video.color = new Color(1, 1, 1, fade);
            if (showingClip)
            {
                if (clipEnded && clock >= PortraitLength) Finish();
                else if (clock >= Mathf.Max(length, (float)player.length) + 8f) Finish(); // decoder stall recovery
            }
            else if (clock >= length) Finish();
        }

        private void PoseNotes()
        {
            float elapsed = challenge.Elapsed;
            for (int i = 0; i < notes.Length; i++)
            {
                Note n = notes[i];
                float beatAt = UltimateBeatChallenge1025.BeatTime(i);
                float start = beatAt - UltimateBeatChallenge1025.Approach;
                bool judged = n.JudgedAt >= 0f;
                float since = judged ? clock - n.JudgedAt : 0f;
                bool show = clock >= RingStart && elapsed >= start && (!judged || since < .7f);
                if (n.Root.gameObject.activeSelf != show) n.Root.gameObject.SetActive(show);
                if (!show) continue;
                if (!judged)
                {
                    float appear = Mathf.Clamp01((elapsed - start) / .15f);
                    float travel = Mathf.Clamp01((elapsed - start) / UltimateBeatChallenge1025.Approach);
                    // in the PERFECT window the target lights up and says NOW
                    bool now = Mathf.Abs(elapsed - beatAt) <= UltimateBeatChallenge1025.PerfectWindow;
                    float pulse = .5f + .5f * Mathf.Sin(clock * 10f);
                    PoseRing(n.Target, 1.1f, 1f, .06f, now ? 1f : .15f * pulse, appear * (now ? 1f : .7f));
                    // the approach ring closes in, easing slightly so the last moment reads clearly
                    float k = 1f - (1f - travel) * (1f - travel * .35f);
                    PoseRing(n.Approach, 2.6f, Mathf.Lerp(2.4f, 1f, k), .07f, travel * travel * .4f, appear * (1f - Mathf.Clamp01((elapsed - beatAt) / .2f) * .6f));
                    n.Burst.enabled = false;
                    n.Label.text = now ? "NOW!" : "TAP";
                    n.Label.color = now ? (Color)Green : new Color(1, 1, 1, appear);
                    n.Label.transform.localScale = Vector3.one * (now ? 1.25f : 1f + .05f * pulse);
                    n.Judge.gameObject.SetActive(false);
                }
                else
                {
                    UltimateTimingGrade grade = challenge.GradeOf(i);
                    float b = Mathf.Clamp01(since / .4f);
                    float flash = grade == UltimateTimingGrade.Perfect ? 1f : grade == UltimateTimingGrade.Good ? .45f : 0f;
                    PoseRing(n.Target, 1.1f, 1f, .06f, flash, (1f - Mathf.Clamp01(since / .25f)) * (grade == UltimateTimingGrade.Miss ? .3f : 1f));
                    n.Approach.enabled = false;
                    // the hit bursts outward as a widening shock of light
                    PoseRing(n.Burst, 2.6f, Mathf.Lerp(1f, 2.3f, 1f - (1f - b) * (1f - b)), Mathf.Lerp(.12f, .02f, b), flash,
                        grade == UltimateTimingGrade.Miss ? 0f : 1f - b);
                    n.Label.color = Color.clear;
                    n.Judge.gameObject.SetActive(true);
                    n.Judge.transform.localScale = Vector3.one * Mathf.Lerp(1.6f, 1f, Mathf.Clamp01(since / .12f));
                    Color jc = n.Judge.color; jc.a = 1f - Mathf.Clamp01((since - .4f) / .3f); n.Judge.color = jc;
                }
            }
        }

        private void Finish()
        {
            if (!active) return;
            challenge.Finish();
            active = false;
            if (player != null) { player.Stop(); player.Prepare(); } // keep it decoded for next time
            root.gameObject.SetActive(false);
            var callback = completed; completed = null;
            callback?.Invoke(challenge.Grade);
        }

        /// <summary>Drops the show without casting (battle closing / outcome reached).</summary>
        public void Cancel()
        {
            cutClock = -1f; if (cutIn != null) cutIn.gameObject.SetActive(false);
            active = false; completed = null; waitingForClip = false;
            if (player != null) player.Stop();
            if (root != null) root.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            foreach (var rt in textures) if (rt != null) { rt.Release(); Destroy(rt); }
            foreach (var m in ringMaterials) if (m != null) Destroy(m);
        }

        private static Sprite ringSprite;
        private static Sprite RingSprite()
        {
            if (ringSprite != null) return ringSprite;
            const int size = 128; var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), Vector2.one * size / 2f) / (size / 2f);
                    float a = Mathf.Clamp01(1f - Mathf.Abs(d - .9f) / .06f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(pixels); tex.Apply();
            return ringSprite = Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * .5f, 100);
        }

        private static Sprite diskSprite;
        private static Sprite DiskSprite()
        {
            if (diskSprite != null) return diskSprite;
            const int size = 128; var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), Vector2.one * size / 2f) / (size / 2f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01((.9f - d) / .04f) * 255));
                }
            tex.SetPixels32(pixels); tex.Apply();
            return diskSprite = Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * .5f, 100);
        }

        private static T NewChild<T>(string name, Transform parent) where T : Component
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(T));
            go.transform.SetParent(parent, false);
            return go.GetComponent<T>();
        }

        private static Text NewText(string name, Transform parent, Font font, string value, int size, Color color)
        {
            Text text = NewChild<Text>(name, parent);
            text.font = font; text.text = value; text.fontSize = size; text.color = color;
            text.fontStyle = FontStyle.BoldAndItalic; text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow; text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            var outline = text.gameObject.AddComponent<Outline>(); outline.effectColor = Color.black; outline.effectDistance = new Vector2(3, -3);
            return text;
        }

        private static void Centre(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position; rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
