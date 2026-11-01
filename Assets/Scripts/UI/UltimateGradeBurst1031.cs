using System;
using ChoSiren.Systems.Tactics;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.UI
{
    /// <summary>Presentation tiers of an ultimate tap. GREAT is the tighter half of the GOOD window (display only).</summary>
    public enum UltimateGradeTier1031 { Miss, Good, Great, Perfect }

    /// <summary>
    /// Centre-screen judgement burst for the manual ultimate (2026-10-31, 赵金: "大招交互的 good、perfect 等等音效跟反馈").
    /// Torn-paper pop band (brand teal; gold + pink for PERFECT) sweeps across, the authored grade word art
    /// (Resources/UI/GradeStamps) slams in with an ink drop shadow and glow; GREAT uses the P5 ransom letters.
    /// PERFECT is the loudest: biggest overshoot pop, white flash, light rays, sparkles, "!!" and a short
    /// shake of the ultimate show. MISS is a small grey drop. ReduceMotion keeps a plain fade (no flash,
    /// shake, rays or pop). Freezes while paused. Sound / announcer are played by the caller.
    /// </summary>
    public sealed class UltimateGradeBurst1031 : MonoBehaviour
    {
        /// <summary>|offset| at or under this, inside the GOOD window, shows GREAT (auto-assist taps stay GOOD).</summary>
        public const float GreatWindow = .16f;
        private static readonly Color32 Gold = new Color32(255, 204, 72, 255);
        private static readonly Color32 Pink = new Color32(255, 54, 170, 255);
        private static readonly Color32 Paper = new Color32(246, 240, 248, 255);
        private static readonly Color32 MissGrey = new Color32(120, 112, 140, 255);

        private UltimateGradeTier1031 tier;
        private RectTransform root, band, art, rays, sparkles, bang, shakeTarget;
        private Image flash, glow;
        private CanvasGroup group;
        private Func<bool> paused;
        private Action interrupted;
        private bool calm, interruptedSent;
        private float clock, life;
        private Vector2 shakeRest;
        private bool shaking;

        public UltimateGradeTier1031 Tier => tier;

        public static UltimateGradeTier1031 TierFor(UltimateTimingGrade grade, float offsetSeconds) =>
            grade == UltimateTimingGrade.Perfect ? UltimateGradeTier1031.Perfect
            : grade == UltimateTimingGrade.Good ? (Mathf.Abs(offsetSeconds) <= GreatWindow ? UltimateGradeTier1031.Great : UltimateGradeTier1031.Good)
            : UltimateGradeTier1031.Miss;

        public static string SfxName(UltimateGradeTier1031 t) => "grade-" + t.ToString().ToLowerInvariant();

        /// <summary>Resources/Audio/Voice path of the English announcer, or null for MISS.</summary>
        public static string AnnouncerClip(UltimateGradeTier1031 t) =>
            t == UltimateGradeTier1031.Miss ? null : "Announcer/ult-" + t.ToString().ToLowerInvariant();

        /// <param name="layer">Full-screen parent (the ultimate show root's parent).</param>
        /// <param name="shake">Shaken briefly on PERFECT (restored afterwards); may be null.</param>
        /// <param name="onInterrupted">Invoked once if the game pauses mid-burst (stop the announcer).</param>
        public static UltimateGradeBurst1031 Play(RectTransform layer, Font font, UltimateGradeTier1031 tier,
            Func<bool> isPaused, RectTransform shake = null, Action onInterrupted = null)
        {
            if (layer == null || !Application.isPlaying) return null;
            Transform old = layer.Find("UltimateGradeBurst1031");
            if (old != null) Destroy(old.gameObject);
            var go = new GameObject("UltimateGradeBurst1031", typeof(RectTransform), typeof(CanvasGroup));
            var b = go.AddComponent<UltimateGradeBurst1031>();
            b.tier = tier; b.paused = isPaused; b.interrupted = onInterrupted;
            b.calm = BattlePresentationOptions.ReduceMotion;
            b.life = tier == UltimateGradeTier1031.Perfect ? 1.2f : tier == UltimateGradeTier1031.Great ? 1f
                : tier == UltimateGradeTier1031.Good ? .9f : .75f;
            b.root = (RectTransform)go.transform;
            b.root.SetParent(layer, false);
            b.root.anchorMin = Vector2.zero; b.root.anchorMax = Vector2.one; b.root.offsetMin = b.root.offsetMax = Vector2.zero;
            b.group = go.GetComponent<CanvasGroup>();
            b.group.blocksRaycasts = false; b.group.interactable = false;
            b.Build(font);
            if (shake != null && tier == UltimateGradeTier1031.Perfect && !b.calm)
            {
                b.shakeTarget = shake; b.shakeRest = shake.anchoredPosition; b.shaking = true;
            }
            b.Pose();
            return b;
        }

        // ---------------------------------------------------------------- build
        private void Build(Font font)
        {
            bool perfect = tier == UltimateGradeTier1031.Perfect, great = tier == UltimateGradeTier1031.Great,
                miss = tier == UltimateGradeTier1031.Miss;

            flash = NewImage("Flash", root, null, new Color(1, 1, 1, 0));
            Stretch(flash.rectTransform);

            var centre = NewRect("Centre", root);
            centre.anchoredPosition = new Vector2(0, 60);

            // light rays behind everything (PERFECT gold + white, GREAT teal)
            rays = NewRect("Rays", centre);
            if (!calm && (perfect || great))
            {
                int n = perfect ? 16 : 9;
                for (int i = 0; i < n; i++)
                {
                    var ray = NewImage("Ray-" + i, rays, CelShapes.Shard,
                        perfect ? (i % 2 == 0 ? (Color)Gold : Color.white) : (Color)Brand1025.Teal);
                    var r = ray.rectTransform;
                    r.pivot = new Vector2(0f, .5f);
                    r.sizeDelta = new Vector2(perfect ? 560f : 420f, (perfect ? 34f : 24f) * (i % 3 == 0 ? 1.4f : 1f));
                    r.localEulerAngles = new Vector3(0, 0, i * 360f / n + (i % 2) * 7f);
                }
            }

            // torn-paper band: ink shadow, colour band, white top edge, halftone, second accent band for PERFECT
            band = NewRect("Band", centre);
            band.localEulerAngles = new Vector3(0, 0, miss ? -4f : -9f);
            float bh = perfect ? 190f : miss ? 92f : 156f;
            Color32 main = perfect ? Gold : miss ? new Color32(36, 28, 52, 230) : Brand1025.Teal;
            P5Letters1023.Plate(band, "Shadow", new Vector2(14, -16), new Vector2(1900, bh), Brand1025.Ink, -16f);
            if (perfect) P5Letters1023.Plate(band, "Pink", new Vector2(0, -bh * .5f + 10f), new Vector2(1900, 46f), Pink, -16f);
            P5Letters1023.Plate(band, "Main", Vector2.zero, new Vector2(1900, bh), main, -16f);
            Image half = P5Letters1023.Plate(band, "Halftone", Vector2.zero, new Vector2(1900, bh),
                perfect ? new Color32(255, 120, 40, 70) : miss ? new Color32(255, 255, 255, 18) : new Color32(0, 120, 130, 80));
            if (half != null) { half.sprite = CelShapes.Halftone; half.type = Image.Type.Tiled; half.pixelsPerUnitMultiplier = 2f; }
            P5Letters1023.Plate(band, "Edge", new Vector2(0, bh * .5f - 4f), new Vector2(1900, 8f), Paper, -16f);
            P5Letters1023.Plate(band, "InkEdge", new Vector2(0, -bh * .5f + 3f), new Vector2(1900, 6f), Brand1025.Ink, -16f);

            // word art
            art = NewRect("Art", centre);
            float width = perfect ? 640f : miss ? 380f : 540f;
            if (great)
            {
                // No authored GREAT plate: ransom letters with a gold "!" on a teal/ink plate rhythm.
                var letters = NewRect("Letters", art);
                foreach (RectTransform l in P5Letters1023.Build(letters, font, "GREAT!", 104f, 1031))
                    foreach (Graphic gph in l.GetComponentsInChildren<Graphic>())
                    {
                        Color32 c = gph.color;
                        if (gph.name == "Plate" && c.r == Brand1025.Teal.r && c.g == Brand1025.Teal.g)
                            gph.color = Gold; // teal plates would vanish on the teal band
                    }
                glow = null;
            }
            else
            {
                UltimateTimingGrade g = perfect ? UltimateTimingGrade.Perfect : miss ? UltimateTimingGrade.Miss : UltimateTimingGrade.Good;
                Sprite sprite = BattleUltimateShow1020.GradeArt(g);
                float aspect = sprite != null ? sprite.rect.height / Mathf.Max(1f, sprite.rect.width) : .32f;
                Vector2 size = new Vector2(width, width * aspect);
                var shadow = NewImage("ArtShadow", art, sprite, new Color(0, 0, 0, .75f));
                shadow.preserveAspect = true; shadow.rectTransform.sizeDelta = size;
                shadow.rectTransform.anchoredPosition = new Vector2(12, -12);
                if (!miss)
                {
                    glow = NewImage("Glow", art, sprite, perfect ? new Color(1f, .85f, .3f, .0f) : new Color(.1f, .95f, .93f, 0f));
                    glow.preserveAspect = true; glow.rectTransform.sizeDelta = size * 1.08f;
                }
                var word = NewImage("Word", art, sprite, miss ? new Color(.82f, .8f, .9f, 1f) : Color.white);
                word.preserveAspect = true; word.rectTransform.sizeDelta = size;
                if (sprite == null)
                {
                    // fallback if the authored art is missing: ransom letters, never a bare label
                    var letters = NewRect("Letters", art);
                    P5Letters1023.Build(letters, font, perfect ? "PERFECT" : miss ? "MISS" : "GOOD", perfect ? 96f : 84f, 1031);
                }
            }
            if (perfect)
            {
                // "PERFECT!!": two ink plates with gold marks stamped under the word's tail (inside a 720 px screen,
                // clear of the letters, contrasting with the gold band)
                bang = NewRect("Bang", centre);
                bang.anchoredPosition = new Vector2(width * .5f - 92f, -112f);
                bang.localEulerAngles = new Vector3(0, 0, 12f);
                var marks = P5Letters1023.Build(bang, font, "!!", 96f, 7);
                foreach (RectTransform m in marks)
                    foreach (Graphic gph in m.GetComponentsInChildren<Graphic>())
                        gph.color = gph.name == "Plate" ? (Color)Brand1025.Ink : gph.name == "Shadow" ? (Color)Pink : (Color)Gold;
            }

            // sparkles (Star4) around the word
            sparkles = NewRect("Sparkles", centre);
            if (!calm && !miss)
            {
                int n = perfect ? 12 : great ? 7 : 4;
                var rng = new System.Random(1031 + (int)tier);
                for (int i = 0; i < n; i++)
                {
                    float a = (float)(i / (double)n * Math.PI * 2 + rng.NextDouble() * .4);
                    float d = (perfect ? 300f : 240f) * (.7f + (float)rng.NextDouble() * .5f);
                    var s = NewImage("Star-" + i, sparkles, CelShapes.Star4,
                        perfect ? (i % 3 == 0 ? (Color)Pink : i % 3 == 1 ? (Color)Gold : Color.white)
                                : i % 2 == 0 ? (Color)Brand1025.Teal : Color.white);
                    float sz = (perfect ? 70f : 54f) * (.6f + (float)rng.NextDouble() * .7f);
                    s.rectTransform.sizeDelta = new Vector2(sz, sz);
                    s.rectTransform.anchoredPosition = new Vector2(Mathf.Cos(a) * d, Mathf.Sin(a) * d * .55f);
                }
            }
            foreach (Graphic g in root.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
        }

        // ---------------------------------------------------------------- animation
        private void Update()
        {
            bool stop = paused != null && paused();
            if (stop)
            {
                if (!interruptedSent) { interruptedSent = true; interrupted?.Invoke(); }
                RestoreShake();
                return;
            }
            clock += Mathf.Min(Time.unscaledDeltaTime, .05f);
            Pose();
            if (clock >= life) Destroy(gameObject);
        }

        private static float OutBack(float t, float s = 2.2f)
        {
            t = Mathf.Clamp01(t) - 1f;
            return t * t * ((s + 1f) * t + s) + 1f;
        }

        private static float OutExpo(float t) { t = Mathf.Clamp01(t); return t >= 1f ? 1f : 1f - Mathf.Pow(2f, -10f * t); }

        private void Pose()
        {
            float t = clock;
            bool perfect = tier == UltimateGradeTier1031.Perfect, miss = tier == UltimateGradeTier1031.Miss;
            float exit = Mathf.Clamp01((t - (life - .2f)) / .2f);
            group.alpha = Mathf.Clamp01(t / (calm ? .12f : .04f)) * (1f - exit * exit);

            if (calm)
            {
                band.anchoredPosition = Vector2.zero; band.localScale = Vector3.one;
                art.localScale = Vector3.one; art.anchoredPosition = Vector2.zero;
                if (bang != null) bang.localScale = Vector3.one;
                flash.color = new Color(1, 1, 1, 0);
                if (glow != null) glow.color = new Color(glow.color.r, glow.color.g, glow.color.b, .35f);
                return;
            }

            // band sweeps in from the left (MISS: drops a little from above)
            float sweep = OutExpo(t / .14f);
            band.anchoredPosition = miss ? new Vector2(0, Mathf.Lerp(40f, 0f, sweep)) : new Vector2(Mathf.Lerp(-1900f, 0f, sweep), 0f);
            band.localScale = new Vector3(1f + exit * .1f, 1f - exit, 1f);

            // word pop: big overshoot for PERFECT, smaller for GREAT/GOOD, a heavy thud for MISS
            float from = perfect ? 2.6f : tier == UltimateGradeTier1031.Great ? 1.9f : tier == UltimateGradeTier1031.Good ? 1.55f : 1.15f;
            float k = (t - .04f) / (perfect ? .22f : .18f);
            float pop = k <= 0f ? 0f : Mathf.LerpUnclamped(from, 1f, OutBack(k, perfect ? 2.6f : 1.8f));
            art.localScale = new Vector3(pop * (1f + exit * .15f), pop * (1f - exit), 1f);
            art.anchoredPosition = miss ? new Vector2(0, Mathf.Lerp(26f, 0f, OutExpo(t / .2f))) : Vector2.zero;
            art.localEulerAngles = new Vector3(0, 0, miss ? Mathf.Sin(t * 30f) * 4f * Mathf.Exp(-t * 6f) - 3f : -4f);
            if (glow != null)
            {
                float pulse = .45f + .35f * Mathf.Sin(t * 22f);
                Color c = glow.color; c.a = Mathf.Clamp01(pulse * (1f - Mathf.Clamp01((t - .5f) / .4f)) * (perfect ? 1f : .7f));
                glow.color = c;
                glow.rectTransform.localScale = Vector3.one * (1f + .05f * Mathf.Sin(t * 22f));
            }
            if (bang != null)
            {
                float bk = (t - .2f) / .1f;
                bang.localScale = Vector3.one * (bk <= 0f ? 0f : Mathf.LerpUnclamped(2.2f, 1f, OutBack(bk)));
            }

            // flash: PERFECT strong, GREAT mild, GOOD faint, MISS none
            float peak = perfect ? .62f : tier == UltimateGradeTier1031.Great ? .3f : tier == UltimateGradeTier1031.Good ? .14f : 0f;
            flash.color = new Color(1f, perfect ? .97f : 1f, perfect ? .85f : 1f, peak * (1f - Mathf.Clamp01(t / .2f)));

            // rays spin out and fade
            float rk = OutExpo(t / .35f);
            rays.localScale = Vector3.one * Mathf.Lerp(.2f, perfect ? 1.25f : 1f, rk);
            rays.localEulerAngles = new Vector3(0, 0, t * (perfect ? 40f : 25f));
            foreach (Graphic g in rays.GetComponentsInChildren<Graphic>())
            { Color c = g.color; c.a = (1f - Mathf.Clamp01((t - .15f) / .45f)) * .85f; g.color = c; }

            // sparkles twinkle outward
            for (int i = 0; i < sparkles.childCount; i++)
            {
                var s = (RectTransform)sparkles.GetChild(i);
                float sk = (t - .06f - i * .025f) / .3f;
                float scale = sk <= 0f ? 0f : Mathf.Sin(Mathf.Clamp01(sk) * Mathf.PI) * (1f + .3f * Mathf.Sin(t * 30f + i));
                s.localScale = Vector3.one * scale;
                s.localEulerAngles = new Vector3(0, 0, t * 180f + i * 25f);
            }

            // PERFECT: a short damped shake of the ultimate show (screen feel), then rest
            if (shaking && shakeTarget != null)
            {
                float d = Mathf.Clamp01(1f - t / .3f);
                if (d <= 0f) RestoreShake();
                else shakeTarget.anchoredPosition = shakeRest + new Vector2(Mathf.Sin(t * 95f), Mathf.Cos(t * 77f) * .6f) * (16f * d * d);
            }
        }

        private void RestoreShake()
        {
            if (!shaking) return;
            shaking = false;
            if (shakeTarget != null) shakeTarget.anchoredPosition = shakeRest;
        }

        private void OnDestroy() => RestoreShake();

        // ---------------------------------------------------------------- helpers
        private static RectTransform NewRect(string name, Transform parent)
        {
            var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            r.SetParent(parent, false);
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f);
            r.sizeDelta = Vector2.zero;
            return r;
        }

        private static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
        {
            var img = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            img.transform.SetParent(parent, false);
            img.sprite = sprite; img.color = color; img.raycastTarget = false;
            var r = img.rectTransform; r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f);
            return img;
        }

        private static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
        }
    }
}
