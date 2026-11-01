using System.Collections.Generic;
using ChoSiren.Systems.Dice;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.UI
{
    /// <summary>How one scoring die burns: colour, size and the extra flourish of its hand.</summary>
    public readonly struct DiceFxStyle
    {
        public readonly Color Color;
        public readonly float Scale;
        public readonly bool Electric, Rainbow, Sparkle;

        public DiceFxStyle(Color color, float scale, bool electric = false, bool rainbow = false, bool sparkle = false)
        {
            Color = color; Scale = scale; Electric = electric; Rainbow = rainbow; Sparkle = sparkle;
        }

        public static readonly Color Pink = new Color(1f, .42f, .72f);
        public static readonly Color Violet = new Color(.66f, .46f, 1f);
        public static readonly Color Magenta = new Color(.9f, .34f, 1f);
        public static readonly Color Cyan = new Color(.28f, .95f, 1f);
        public static readonly Color Gold = new Color(1f, .8f, .3f);

        /// <summary>
        /// Only dice that form the hand burn, and each hand burns its own way: a pair glows soft
        /// pink, two pairs split pink/violet, three of a kind flares magenta, a straight crackles
        /// cyan, a full house burns gold over pink, four of a kind rages violet with sparks, and
        /// five or six of a kind cycle through the rainbow.
        /// </summary>
        public static DiceFxStyle For(DicePattern pattern, IReadOnlyList<int> values, int index)
        {
            int face = values[index];
            int count = 0;
            for (int i = 0; i < values.Count; i++) if (values[i] == face) count++;
            switch (pattern)
            {
                case DicePattern.Pair: return new DiceFxStyle(Pink, .85f);
                case DicePattern.TwoPair: return new DiceFxStyle(GroupColor(values, face, Pink, Violet), .9f);
                case DicePattern.ThreeKind: return new DiceFxStyle(Magenta, 1f);
                case DicePattern.Straight:
                case DicePattern.SixStraight: return new DiceFxStyle(Cyan, 1f, electric: true);
                case DicePattern.FullHouse: return new DiceFxStyle(count >= 3 ? Gold : Pink, 1.05f);
                case DicePattern.ThreePair: return new DiceFxStyle(GroupColor(values, face, Pink, Violet, Cyan), 1f);
                case DicePattern.DoubleThreeKind: return new DiceFxStyle(GroupColor(values, face, Gold, Violet), 1.1f);
                case DicePattern.FourKind: return new DiceFxStyle(Violet, 1.15f, sparkle: true);
                case DicePattern.FiveKind:
                case DicePattern.SixKind: return new DiceFxStyle(Gold, 1.25f, rainbow: true, sparkle: true);
                default: return new DiceFxStyle(Pink, .8f);
            }
        }

        // Groups of equal faces get the palette in descending face order, so the higher group leads.
        private static Color GroupColor(IReadOnlyList<int> values, int face, params Color[] palette)
        {
            int rank = 0;
            for (int other = 6; other > face; other--)
            {
                int count = 0;
                for (int i = 0; i < values.Count; i++) if (values[i] == other) count++;
                if (count >= 2) rank++;
            }
            return palette[Mathf.Min(rank, palette.Length - 1)];
        }
    }

    /// <summary>
    /// Meeting row 17 (reference: 六六英雄): once a hand forms, every scoring die burns with a
    /// flickering flame in its hand's style, shows its own "+pips" under it, and the dice that
    /// do not score dim, so the pattern reads at a glance. Lives on the die button; purely
    /// presentational.
    /// </summary>
    public sealed class DiceScoreAura : MonoBehaviour
    {
        private static Sprite flameSprite;
        private static Sprite[] flameFrames;
        private static bool tintableFrames;
        private const int FlameFrameCount = 8;
        private int frameOffset;
        private Image outer, inner, spark;
        private Text points;
        private CanvasGroup group;
        private RectTransform visualLayer;
        private bool scoring, dimmed;
        private float clock, popAt;
        private DiceFxStyle style = new DiceFxStyle(DiceFxStyle.Pink, 1f);
        private Vector2 outerSize, innerSize;
        private System.Func<bool> paused;

        public static DiceScoreAura Attach(RectTransform die, Font font, System.Func<bool> isPaused)
        {
            DiceScoreAura aura = UnityNull.Real(die.GetComponent<DiceScoreAura>()) ?? die.gameObject.AddComponent<DiceScoreAura>();
            aura.paused = isPaused;
            aura.Build(die, font);
            return aura;
        }

        private void Build(RectTransform die, Font font)
        {
            if (outer != null) return;
            group = GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();
            var overlay = new GameObject("DiceScoreOverlay", typeof(RectTransform), typeof(CanvasGroup));
            overlay.transform.SetParent(die, false);
            visualLayer = overlay.GetComponent<RectTransform>();
            visualLayer.anchorMin = Vector2.zero; visualLayer.anchorMax = Vector2.one;
            visualLayer.offsetMin = visualLayer.offsetMax = Vector2.zero;
            var visualGroup = overlay.GetComponent<CanvasGroup>();
            visualGroup.blocksRaycasts = false; visualGroup.interactable = false;
            // Two flipbook layers at different sizes and phases give the fire volume.
            // The separate last-sibling layer is above the authored face. Narrow side wisps
            // leave the pip centre clear instead of an opaque sheet over the whole die.
            outer = Flame("DiceFlameOuter", visualLayer, new Color(1f, .75f, .9f, 0f), .52f);
            inner = Flame("DiceFlameInner", visualLayer, new Color(1f, 1f, 1f, 0f), .46f);
            outer.rectTransform.anchoredPosition = new Vector2(-die.rect.width * .36f, -die.rect.height * .18f);
            inner.rectTransform.anchoredPosition = new Vector2(die.rect.width * .36f, -die.rect.height * .18f);
            frameOffset = Random.Range(0, FlameFrameCount);
            // 2026-10-20: the flame wisps read as a glitch. A scoring die now gets a clean neon
            // frame in the hand's colour; the inner layer is retired.
            outer.sprite = FrameSprite();
            outer.type = Image.Type.Sliced;
            outer.rectTransform.pivot = new Vector2(.5f, .5f);
            outer.rectTransform.anchoredPosition = Vector2.zero;
            outer.rectTransform.sizeDelta = die.rect.size * 1.08f;
            inner.enabled = false;
            // 2026-10-20: a frame on scoring dice read as "selected"; selection owns the
            // highlight (lift + pink glow). Scoring dice keep their "+N" only.
            outer.enabled = false;
            outerSize = outer.rectTransform.sizeDelta;
            innerSize = inner.rectTransform.sizeDelta;
            spark = Flame("DiceSpark", visualLayer, new Color(1f, 1f, 1f, 0f), .7f);
            Sprite sparkle = ReferenceArt038.Load("Art/Reference038/vfx-sparkle");
            if (sparkle != null) spark.sprite = sparkle;
            spark.rectTransform.pivot = new Vector2(.5f, .5f);
            spark.rectTransform.sizeDelta = Vector2.one * Mathf.Max(40f, die.rect.width) * .55f;
            spark.rectTransform.anchoredPosition = new Vector2(die.rect.width * .28f, die.rect.height * .3f);
            var go = new GameObject("DicePoints", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(visualLayer, false);
            points = go.GetComponent<Text>();
            points.font = font;
            points.fontSize = 30;
            points.fontStyle = FontStyle.Bold;
            points.alignment = TextAnchor.MiddleCenter;
            points.horizontalOverflow = HorizontalWrapMode.Overflow;
            points.verticalOverflow = VerticalWrapMode.Overflow;
            points.color = new Color(1f, .84f, .3f, 0f);
            points.raycastTarget = false;
            Outline edge = go.AddComponent<Outline>();
            edge.effectColor = new Color(.15f, 0f, .1f, .9f);
            edge.effectDistance = new Vector2(2f, -2f);
            RectTransform r = points.rectTransform;
            // On the die's lower edge: below it the row runs into the home indicator on phones.
            r.anchorMin = r.anchorMax = new Vector2(.5f, 0f);
            r.pivot = new Vector2(.5f, .5f);
            r.sizeDelta = new Vector2(120f, 40f);
            r.anchoredPosition = new Vector2(0f, 14f);
        }

        private Image Flame(string name, RectTransform die, Color color, float size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(die, false);
            Image image = go.GetComponent<Image>();
            image.sprite = FlameSprite();
            image.color = color;
            image.raycastTarget = false;
            RectTransform r = image.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(.5f, .5f);
            r.pivot = new Vector2(.5f, .18f);
            Vector2 s = die.rect.size;
            float w = Mathf.Max(40f, s.x) * size;
            r.sizeDelta = new Vector2(w, w * 1.5f);
            r.anchoredPosition = new Vector2(0f, -s.y * .12f);
            return image;
        }

        /// <summary>Scoring die: flame + "+pips". Non-scoring die in a formed hand: dimmed.</summary>
        public void Show(bool isScoring, bool dimWhenIdle, int pips, float delay, DiceFxStyle fx)
        {
            if (visualLayer != null) visualLayer.SetAsLastSibling();
            style = fx;
            if (outer != null) outer.rectTransform.sizeDelta = outerSize;
            scoring = isScoring;
            dimmed = !isScoring && dimWhenIdle;
            popAt = clock + delay;
            if (points != null) points.text = isScoring ? "+" + pips : string.Empty;
        }

        public void Hide()
        {
            scoring = false;
            dimmed = false;
        }

        private void Update()
        {
            if (paused != null && paused()) return;
            clock += Time.unscaledDeltaTime;
            float since = clock - popAt;
            float on = scoring && since > 0f ? Mathf.Clamp01(since / .18f) : 0f;
            if (outer != null)
            {
                Color tint = style.Rainbow ? Color.HSVToRGB(Mathf.Repeat(clock * .35f + transform.GetSiblingIndex() * .12f, 1f), .55f, 1f)
                    : style.Color;
                float pulse = .78f + .22f * Mathf.Sin(clock * 5f + transform.GetSiblingIndex());
                float zap = style.Electric ? (Mathf.PerlinNoise(clock * 18f, transform.GetSiblingIndex()) > .3f ? 1f : .55f) : 1f;
                outer.color = new Color(tint.r, tint.g, tint.b, on * pulse * zap);
                // Snaps in from a little larger, then holds.
                outer.rectTransform.localScale = Vector3.one * (on <= 0f ? 1f : Mathf.Lerp(1.22f, 1f, on));
            }
            if (spark != null)
            {
                // Four of a kind and up: a star glints on the die every half second.
                float glint = style.Sparkle && on > 0f ? Mathf.Pow(Mathf.Max(0f, Mathf.Sin(clock * 6.3f + frameOffset)), 6f) : 0f;
                spark.color = new Color(1f, 1f, 1f, glint);
                spark.rectTransform.localScale = Vector3.one * (.6f + glint * .6f);
                spark.rectTransform.localRotation = Quaternion.Euler(0f, 0f, clock * 90f);
            }
            if (points != null)
            {
                float pop = scoring && since > 0f ? Mathf.Clamp01(since / .22f) : 0f;
                points.color = new Color(1f, .84f, .3f, pop);
                points.rectTransform.localScale = Vector3.one * (pop <= 0f ? 1f : 1f + (1f - pop) * .9f);
            }
            if (group != null) group.alpha = Mathf.MoveTowards(group.alpha, dimmed ? .5f : 1f, Time.unscaledDeltaTime * 4f);
        }

        private static Sprite frameSprite;
        /// <summary>Rounded-square neon outline with a soft outer glow, 9-sliced.</summary>
        private static Sprite FrameSprite()
        {
            if (frameSprite != null) return frameSprite;
            const int n = 64; const float r = 16f, line = 4f, glow = 8f;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                // Signed distance to a rounded square inset by the glow width.
                float half = n / 2f - glow, cx = Mathf.Abs(x + .5f - n / 2f) - (half - r), cy = Mathf.Abs(y + .5f - n / 2f) - (half - r);
                float d = new Vector2(Mathf.Max(cx, 0), Mathf.Max(cy, 0)).magnitude + Mathf.Min(Mathf.Max(cx, cy), 0) - r;
                float core = Mathf.Clamp01(1f - Mathf.Abs(d) / (line / 2f));
                float halo = d > 0 ? Mathf.Clamp01(1f - d / glow) * .45f : 0f;
                float a = Mathf.Max(core, halo * halo);
                byte w = (byte)(core > .5f ? 255 : 230);
                px[y * n + x] = new Color32(w, w, w, (byte)(a * 255));
            }
            tex.SetPixels32(px); tex.Apply(false, true);
            frameSprite = Sprite.Create(tex, new Rect(0, 0, n, n), Vector2.one * .5f, 100f, 0, SpriteMeshType.FullRect,
                new Vector4(26, 26, 26, 26));
            frameSprite.hideFlags = HideFlags.DontSave;
            return frameSprite;
        }

        /// <summary>The rendered 8-frame flame flipbook (vfx-dice-flame), or null to fall back.</summary>
        private static Sprite[] FlameFrames()
        {
            if (flameFrames != null) return flameFrames;
            // The white sheet takes each hand's colour; the pink original is the fallback.
            Sprite sheet = ReferenceArt038.Load("Art/Reference038/vfx-dice-flame-white");
            tintableFrames = sheet != null && sheet.texture != null;
            if (!tintableFrames) sheet = ReferenceArt038.Load("Art/Reference038/vfx-dice-flame");
            if (sheet == null || sheet.texture == null) return null;
            Rect r = sheet.textureRect;
            float w = r.width / FlameFrameCount;
            flameFrames = new Sprite[FlameFrameCount];
            for (int i = 0; i < FlameFrameCount; i++)
            {
                flameFrames[i] = Sprite.Create(sheet.texture, new Rect(r.x + i * w, r.y, w, r.height), new Vector2(.5f, .1f), 100f, 0, SpriteMeshType.FullRect);
                flameFrames[i].hideFlags = HideFlags.DontSave;
            }
            return flameFrames;
        }

        /// <summary>Soft teardrop flame: wide hot base, tapering wavy tip, smooth alpha.</summary>
        private static Sprite FlameSprite()
        {
            if (flameSprite != null) return flameSprite;
            const int w = 64, h = 96;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
                { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float v = y / (h - 1f);                 // 0 bottom .. 1 tip
                float u = (x + .5f) / w * 2f - 1f;      // -1 .. 1
                float half = Mathf.Lerp(.95f, 0f, Mathf.Pow(v, .8f)) * (1f - Mathf.Pow(Mathf.Clamp01(.18f - v) / .18f, 2f) * .5f);
                float d = Mathf.Abs(u) / Mathf.Max(.001f, half);
                float a = Mathf.Clamp01(1f - d) * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(v / .12f)) * Mathf.Clamp01((1f - v) * 1.4f);
                px[y * w + x] = new Color32(255, 255, 255, (byte)(255f * Mathf.SmoothStep(0f, 1f, a)));
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            flameSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(.5f, .18f), 100f, 0, SpriteMeshType.FullRect);
            flameSprite.hideFlags = HideFlags.DontSave;
            return flameSprite;
        }
    }
}
