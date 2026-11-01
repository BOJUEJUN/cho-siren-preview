using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    public enum DamageNumberStyle1101 { Gold, Critical, Skill }

    /// <summary>
    /// The pre-rendered damage glyphs (Tools/damage1101/make_damage_digits.py; Anton, SIL OFL 1.1).
    /// Every glyph has an outline/shadow/extrusion layer and an aligned face layer, both grey-scale.
    /// </summary>
    public static class DamageGlyphAtlas1101
    {
        [Serializable] private sealed class GlyphJson { public string c; public float originX, baseline, capTop, advance; public int w, h, x, y, fx; }
        [Serializable] private sealed class RectJson { public int x, y, w, h; }
        [Serializable] private sealed class AtlasJson { public int atlas; public float cap; public RectJson glow; public GlyphJson[] glyphs; }

        public struct Glyph
        {
            public float OriginX, Baseline, CapTop, Advance, W, H;
            public Rect BackUv, FrontUv;
        }

        public const int Comma = 10, Plus = 11, Critical = 12;
        private static bool tried;
        public static Texture2D Texture { get; private set; }
        public static Glyph[] Glyphs { get; private set; }
        public static Rect GlowUv { get; private set; }
        public static bool Ready => Load();

        public static bool Load()
        {
            if (tried) return Texture != null;
            tried = true;
            Texture = Resources.Load<Texture2D>("UI/DamageFont1101/damage-digits");
            TextAsset json = Resources.Load<TextAsset>("UI/DamageFont1101/damage-digits");
            if (Texture == null || json == null) { Texture = null; return false; }
            var data = JsonUtility.FromJson<AtlasJson>(json.text);
            float a = Mathf.Max(1, data.atlas);
            Glyphs = new Glyph[13];
            foreach (GlyphJson g in data.glyphs)
            {
                int index = g.c == "," ? Comma : g.c == "+" ? Plus : g.c == "CRITICAL" ? Critical
                    : g.c.Length == 1 && g.c[0] >= '0' && g.c[0] <= '9' ? g.c[0] - '0' : -1;
                if (index < 0) continue;
                Glyphs[index] = new Glyph
                {
                    OriginX = g.originX, Baseline = g.baseline, CapTop = g.capTop, Advance = g.advance, W = g.w, H = g.h,
                    BackUv = new Rect(g.x / a, 1f - (g.y + g.h) / a, g.w / a, g.h / a),
                    FrontUv = new Rect(g.fx / a, 1f - (g.y + g.h) / a, g.w / a, g.h / a),
                };
            }
            GlowUv = new Rect(data.glow.x / a, 1f - (data.glow.y + data.glow.h) / a, data.glow.w / a, data.glow.h / a);
            return true;
        }
    }

    /// <summary>
    /// One damage number (or the CRITICAL tag, or the soft glow) as a single mesh: every outline layer first,
    /// then every face with a three-stop vertical gradient, so neighbouring digits' outlines merge cleanly.
    /// The mesh is rebuilt only when the value changes; motion and fading happen on the parent transform/group.
    /// </summary>
    public sealed class DamageNumberGraphic1101 : MaskableGraphic
    {
        public enum Mode { Number, Word, Glow }
        private readonly int[] codes = new int[16];
        private static readonly int[] scratch = new int[16];
        private int count;
        private Mode mode;
        private float unitsPerPx = 1f;
        public Color Top = Color.white, Mid = Color.white, Bottom = Color.white, Rim = Color.black;
        public float Width { get; private set; }
        public float CapUnits { get; private set; }

        public override Texture mainTexture => DamageGlyphAtlas1101.Texture != null ? DamageGlyphAtlas1101.Texture : s_WhiteTexture;

        /// <summary>Lays out <paramref name="value"/> with thousands separators at <paramref name="cap"/> digit height (canvas units).</summary>
        public float SetNumber(int value, float cap)
        {
            mode = Mode.Number;
            value = Mathf.Max(0, value);
            int n = 0;
            do { scratch[n++] = value % 10; value /= 10; } while (value > 0 && n < 12);
            count = 0;
            for (int i = n - 1; i >= 0; i--)
            {
                codes[count++] = scratch[i];
                if (i > 0 && i % 3 == 0 && count < codes.Length) codes[count++] = DamageGlyphAtlas1101.Comma;
            }
            return Relayout(cap);
        }

        public float SetWord(float cap) { mode = Mode.Word; count = 1; codes[0] = DamageGlyphAtlas1101.Critical; return Relayout(cap); }

        public void SetGlow(Vector2 size)
        {
            mode = Mode.Glow; count = 0;
            rectTransform.sizeDelta = size; Width = size.x;
            SetVerticesDirty();
        }

        private float Relayout(float cap)
        {
            if (!DamageGlyphAtlas1101.Ready) { Width = 0; SetVerticesDirty(); return 0; }
            var g0 = DamageGlyphAtlas1101.Glyphs[codes[0]];
            unitsPerPx = cap / Mathf.Max(1f, g0.Baseline - g0.CapTop);
            CapUnits = cap;
            Measure(out float left, out float right);
            Width = (right - left) * unitsPerPx;
            rectTransform.sizeDelta = new Vector2(Width, cap * 1.5f);
            SetVerticesDirty();
            return Width;
        }

        private void Measure(out float left, out float right)
        {
            left = float.MaxValue; right = float.MinValue;
            float pen = 0;
            var glyphs = DamageGlyphAtlas1101.Glyphs;
            for (int i = 0; i < count; i++)
            {
                var g = glyphs[codes[i]];
                left = Mathf.Min(left, pen - g.OriginX);
                right = Mathf.Max(right, pen - g.OriginX + g.W);
                pen += g.Advance;
            }
            if (count == 0) left = right = 0;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (!DamageGlyphAtlas1101.Ready) return;
            if (mode == Mode.Glow)
            {
                Rect r = rectTransform.rect, uv = DamageGlyphAtlas1101.GlowUv;
                Color32 c = color;
                vh.AddVert(new Vector3(r.xMin, r.yMin), c, new Vector2(uv.xMin, uv.yMin));
                vh.AddVert(new Vector3(r.xMin, r.yMax), c, new Vector2(uv.xMin, uv.yMax));
                vh.AddVert(new Vector3(r.xMax, r.yMax), c, new Vector2(uv.xMax, uv.yMax));
                vh.AddVert(new Vector3(r.xMax, r.yMin), c, new Vector2(uv.xMax, uv.yMin));
                vh.AddTriangle(0, 1, 2); vh.AddTriangle(2, 3, 0);
                return;
            }
            if (count == 0) return;
            var glyphs = DamageGlyphAtlas1101.Glyphs;
            Measure(out float left, out float right);
            float k = unitsPerPx;
            float offsetX = -(left + right) * .5f * k;
            var g0 = glyphs[codes[0]];
            float baselineY = -(g0.Baseline - g0.CapTop) * k * .5f;
            float alpha = color.a;
            Color32 rim = Scale(Rim, alpha);
            // Pass 1: outlines, extrusion, shadow.
            float pen = 0;
            for (int i = 0; i < count; i++)
            {
                var g = glyphs[codes[i]];
                float x0 = (pen - g.OriginX) * k + offsetX, top = baselineY + g.Baseline * k;
                Quad(vh, x0, top, g.W * k, g.H * k, g.BackUv, rim);
                pen += g.Advance;
            }
            // Pass 2: faces, 3-stop gradient between cap top and baseline.
            Color32 cTop = Scale(Top, alpha), cMid = Scale(Mid, alpha), cBottom = Scale(Bottom, alpha);
            pen = 0;
            for (int i = 0; i < count; i++)
            {
                var g = glyphs[codes[i]];
                float x0 = (pen - g.OriginX) * k + offsetX, top = baselineY + g.Baseline * k;
                float w = g.W * k, h = g.H * k;
                Rect uv = g.FrontUv;
                // rows, as fractions of the cell from its top
                float capTop = Mathf.Clamp01(g.CapTop / g.H), baseline = Mathf.Clamp01(g.Baseline / g.H);
                float mid = (capTop + baseline) * .5f;
                int start = vh.currentVertCount;
                Row(vh, x0, w, top, h, uv, 0f, cTop);
                Row(vh, x0, w, top, h, uv, capTop, cTop);
                Row(vh, x0, w, top, h, uv, mid, cMid);
                Row(vh, x0, w, top, h, uv, baseline, cBottom);
                Row(vh, x0, w, top, h, uv, 1f, cBottom);
                for (int r = 0; r < 4; r++)
                {
                    int a = start + r * 2;
                    vh.AddTriangle(a, a + 1, a + 3); vh.AddTriangle(a + 3, a + 2, a);
                }
                pen += g.Advance;
            }
        }

        private static Color32 Scale(Color c, float alpha) { c.a *= alpha; return c; }

        private static void Quad(VertexHelper vh, float x, float top, float w, float h, Rect uv, Color32 c)
        {
            int s = vh.currentVertCount;
            vh.AddVert(new Vector3(x, top - h), c, new Vector2(uv.xMin, uv.yMin));
            vh.AddVert(new Vector3(x, top), c, new Vector2(uv.xMin, uv.yMax));
            vh.AddVert(new Vector3(x + w, top), c, new Vector2(uv.xMax, uv.yMax));
            vh.AddVert(new Vector3(x + w, top - h), c, new Vector2(uv.xMax, uv.yMin));
            vh.AddTriangle(s, s + 1, s + 2); vh.AddTriangle(s + 2, s + 3, s);
        }

        /// <summary>A left/right vertex pair at fraction <paramref name="f"/> (from the top) of a glyph cell.</summary>
        private static void Row(VertexHelper vh, float x, float w, float top, float h, Rect uv, float f, Color32 c)
        {
            float y = top - h * f, v = uv.yMax - uv.height * f;
            vh.AddVert(new Vector3(x, y), c, new Vector2(uv.xMin, v));
            vh.AddVert(new Vector3(x + w, y), c, new Vector2(uv.xMax, v));
        }
    }

    /// <summary>
    /// 2026-11-01 (赵金: "伤害数字要这么大参考天命之子，这样看起来才爽"): the big numbers our side deals to enemies —
    /// roughly a third to a half of the screen wide, centred on the enemy, heavy italic gold→orange with a thick dark
    /// outline and 3D extrusion (skills: white→cyan→blue). They slam in, bounce, hold, then float up and fade;
    /// crits are larger and hotter with a CRITICAL tag; rapid hits on one enemy stack, the newest on top and the
    /// earlier ones pushed down beneath it. Pooled, no per-frame allocation; respects pause, speed and ReduceMotion.
    /// </summary>
    public sealed class BigDamageNumbers1101 : MonoBehaviour
    {
        public const int Capacity = 10;
        public const float MinWidth = 250f, MaxWidth = 430f, MaxCritWidth = 480f;
        // timeline (seconds at 1× speed)
        public const float PopIn = .07f, Settle = .21f, HoldEnd = .66f, Life = 1.02f, CritExtra = .14f;

        private sealed class Entry
        {
            public RectTransform Root, Body;
            public CanvasGroup Group;
            public DamageNumberGraphic1101 Number, Word, Glow;
            public bool Active;
            public float Age, Size, Width, LifeEnd, HoldUntil;
            public int Owner, Level, Amount;
            public DamageNumberStyle1101 Style;
            public Vector2 Base, Offset, TargetOffset;
            public float Jitter, Spin;
        }

        private struct Pending { public Vector2 At; public int Amount, Owner; public DamageNumberStyle1101 Style; public float Size; }

        private readonly Entry[] entries = new Entry[Capacity];
        private readonly Pending[] pending = new Pending[12];
        private int pendingHead, pendingCount, cursor;
        private float releaseTimer;
        private RectTransform layer;
        private Func<bool> paused, hold;
        private Func<float> delta;
        private Rect bounds = new Rect(0, 0, 720, 1500);
        private Entry newest;

        public int ActiveCount { get { int n = 0; foreach (Entry e in entries) if (e != null && e.Active) n++; return n; } }
        public int PendingCount => pendingCount;
        public int SpawnedCount { get; private set; }

        /// <summary><paramref name="area"/>: usable rectangle in layer units (x, y from the top-left).</summary>
        public void Configure(RectTransform host, Func<bool> isPaused, Func<float> animationDelta, Func<bool> holdWhile, Rect area)
        {
            layer = host; paused = isPaused; delta = animationDelta; hold = holdWhile; bounds = area;
            if (entries[0] != null) return;
            DamageGlyphAtlas1101.Load();
            for (int i = 0; i < Capacity; i++) entries[i] = Build(i);
        }

        private Entry Build(int index)
        {
            var root = new GameObject("BigDamage-" + index, typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
            root.SetParent(layer, false);
            root.anchorMin = root.anchorMax = new Vector2(0f, 1f);
            root.pivot = new Vector2(.5f, .5f);
            root.sizeDelta = Vector2.zero;
            var group = root.GetComponent<CanvasGroup>(); group.blocksRaycasts = false; group.interactable = false;
            var body = new GameObject("Body", typeof(RectTransform)).GetComponent<RectTransform>();
            body.SetParent(root, false);
            body.sizeDelta = Vector2.zero;
            var e = new Entry { Root = root, Body = body, Group = group };
            e.Glow = Part(body, "Glow");
            e.Number = Part(body, "Number");
            e.Word = Part(body, "Critical");
            root.gameObject.SetActive(false);
            return e;
        }

        private static DamageNumberGraphic1101 Part(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(DamageNumberGraphic1101));
            go.transform.SetParent(parent, false);
            var g = go.GetComponent<DamageNumberGraphic1101>();
            g.raycastTarget = false;
            g.rectTransform.pivot = new Vector2(.5f, .5f);
            return g;
        }

        /// <summary>Digit height (canvas units) for a style before the width clamp.</summary>
        public static float CapFor(DamageNumberStyle1101 style) =>
            style == DamageNumberStyle1101.Critical ? 150f : style == DamageNumberStyle1101.Skill ? 128f : 118f;

        /// <summary>Shows <paramref name="amount"/> centred at <paramref name="at"/> (layer units, from the top-left).</summary>
        public bool Spawn(Vector2 at, int amount, DamageNumberStyle1101 style, int owner, float size = 1f)
        {
            if (layer == null || amount <= 0 || !DamageGlyphAtlas1101.Ready) return false;
            if (hold != null && hold())
            {
                // The skill close-up owns the foreground: its hits land the moment it slips away.
                if (pendingCount == pending.Length) { pendingHead = (pendingHead + 1) % pending.Length; pendingCount--; }
                pending[(pendingHead + pendingCount) % pending.Length] = new Pending { At = at, Amount = amount, Owner = owner, Style = style, Size = size };
                pendingCount++;
                return true;
            }
            Show(at, amount, style, owner, size);
            return true;
        }

        private void Show(Vector2 at, int amount, DamageNumberStyle1101 style, int owner, float size)
        {
            Entry e = Take();
            bool crit = style == DamageNumberStyle1101.Critical;
            // Push this enemy's earlier numbers down beneath the new one (refs/08: 4,645 over the previous hit).
            int siblings = 0;
            foreach (Entry other in entries)
            {
                if (other == null || !other.Active || other == e || other.Owner != owner || other.Age >= other.HoldUntil) continue;
                other.Level++;
                siblings++;
                if (other.Level >= 3) other.Age = Mathf.Max(other.Age, other.HoldUntil);   // the oldest leaves at once
            }
            e.Active = true; e.Age = 0f; e.Owner = owner; e.Level = 0; e.Style = style; e.Amount = amount;
            e.HoldUntil = HoldEnd + (crit ? CritExtra : 0f);
            e.LifeEnd = Life + (crit ? CritExtra : 0f);
            Palette(style, out Color top, out Color mid, out Color bottom, out Color rim, out Color glow);
            e.Number.Top = top; e.Number.Mid = mid; e.Number.Bottom = bottom; e.Number.Rim = rim;
            float cap = CapFor(style) * Mathf.Max(.5f, size);
            float width = e.Number.SetNumber(amount, cap);
            float max = crit ? MaxCritWidth : MaxWidth;
            if (width > 0f && (width < MinWidth || width > max))
            {
                float fit = Mathf.Clamp(width, MinWidth, max) / width;
                cap = Mathf.Min(cap * fit, 178f);
                width = e.Number.SetNumber(amount, cap);
            }
            e.Width = width; e.Size = cap;
            e.Number.rectTransform.anchoredPosition = Vector2.zero;
            e.Glow.color = glow;
            e.Glow.SetGlow(new Vector2(width * 1.3f + 60f, cap * 2.3f));
            e.Glow.rectTransform.anchoredPosition = new Vector2(0f, -cap * .05f);
            e.Word.gameObject.SetActive(crit);
            if (crit)
            {
                e.Word.Top = Color.white; e.Word.Mid = new Color32(255, 150, 222, 255); e.Word.Bottom = new Color32(255, 40, 150, 255);
                e.Word.Rim = new Color32(120, 0, 70, 255);
                float wordWidth = e.Word.SetWord(cap * .36f);
                e.Word.rectTransform.anchoredPosition = new Vector2(-width * .5f + wordWidth * .5f + cap * .1f, cap * .72f);
                e.Word.rectTransform.localEulerAngles = new Vector3(0f, 0f, 6f);
            }
            float half = width * .5f + 6f;
            at.x = Mathf.Clamp(at.x, bounds.xMin + half, Mathf.Max(bounds.xMin + half, bounds.xMax - half));
            at.y = Mathf.Clamp(at.y, bounds.yMin + cap, Mathf.Max(bounds.yMin + cap, bounds.yMax - cap));
            e.Base = at;
            // A stacked hit lands a little to the side of the previous one so the two read as separate numbers.
            e.Jitter = siblings == 0 ? 0f : (SpawnedCount % 2 == 0 ? 1f : -1f) * Mathf.Min(40f, 18f + siblings * 10f);
            e.Spin = BattlePresentationOptions.ReduceMotion ? 0f : crit ? -5f : (SpawnedCount % 2 == 0 ? -2.5f : 2.5f);
            e.Offset = e.TargetOffset = new Vector2(e.Jitter, 0f);
            e.Root.gameObject.SetActive(true);
            e.Root.SetAsLastSibling();
            newest = e;
            SpawnedCount++;
            Pose(e);
        }

        private Entry Take()
        {
            foreach (Entry e in entries) if (!e.Active) return e;
            // Pool full: recycle the oldest.
            Entry oldest = entries[0];
            foreach (Entry e in entries) if (e.Age > oldest.Age) oldest = e;
            return oldest;
        }

        public static void Palette(DamageNumberStyle1101 style, out Color top, out Color mid, out Color bottom, out Color rim, out Color glow)
        {
            switch (style)
            {
                case DamageNumberStyle1101.Critical:
                    top = Color.white; mid = new Color32(255, 238, 96, 255); bottom = new Color32(255, 72, 38, 255);
                    rim = new Color32(196, 0, 64, 255); glow = new Color32(255, 70, 150, 210); return;
                case DamageNumberStyle1101.Skill:
                    top = Color.white; mid = new Color32(150, 248, 255, 255); bottom = new Color32(36, 136, 255, 255);
                    rim = new Color32(20, 40, 150, 255); glow = new Color32(30, 232, 232, 150); return;
                default:
                    top = new Color32(255, 250, 200, 255); mid = new Color32(255, 214, 40, 255); bottom = new Color32(255, 110, 10, 255);
                    rim = new Color32(150, 40, 0, 255); glow = new Color32(255, 170, 40, 130); return;
            }
        }

        private void Update()
        {
            if (layer == null || (paused != null && paused())) return;
            float dt = delta != null ? delta() : Time.unscaledDeltaTime;
            if (dt <= 0f || float.IsNaN(dt) || float.IsInfinity(dt)) return;
            dt = Mathf.Min(dt, .1f);
            if (pendingCount > 0 && (hold == null || !hold()))
            {
                releaseTimer -= dt;
                if (releaseTimer <= 0f)
                {
                    Pending p = pending[pendingHead];
                    pendingHead = (pendingHead + 1) % pending.Length; pendingCount--;
                    Show(p.At, p.Amount, p.Style, p.Owner, p.Size);
                    releaseTimer = .07f;
                }
            }
            else if (pendingCount == 0) releaseTimer = 0f;
            float follow = 1f - Mathf.Exp(-dt * 20f);
            foreach (Entry e in entries)
            {
                if (e == null || !e.Active) continue;
                e.Age += dt;
                if (e.Age >= e.LifeEnd) { Retire(e); continue; }
                // Each earlier hit drops about three quarters of a digit and slides aside: its value stays readable.
                e.TargetOffset = new Vector2(e.Jitter + e.Level * 22f * (e.Jitter >= 0f ? 1f : -1f), e.Level * e.Size * .8f);
                e.Offset = BattlePresentationOptions.ReduceMotion ? e.TargetOffset : Vector2.Lerp(e.Offset, e.TargetOffset, follow);
                Pose(e);
            }
        }

        private void Pose(Entry e)
        {
            bool calm = BattlePresentationOptions.ReduceMotion;
            float t = e.Age;
            float scale, rise = 0f, alpha;
            float outStart = e.HoldUntil, outLen = Mathf.Max(.01f, e.LifeEnd - e.HoldUntil);
            float fadeOut = Mathf.Clamp01((t - outStart) / outLen);
            if (calm)
            {
                scale = 1f;
                alpha = Mathf.Clamp01(t / .1f) * (1f - fadeOut);
            }
            else
            {
                if (t < PopIn)
                {
                    float p = t / PopIn;
                    scale = Mathf.Lerp(.25f, 1.32f, 1f - (1f - p) * (1f - p));
                }
                else if (t < Settle)
                {
                    // damped bounce 1.32 → 0.92 → 1.0
                    float p = (t - PopIn) / (Settle - PopIn);
                    scale = 1f + .32f * Mathf.Cos(p * Mathf.PI * 1.5f) * (1f - p) * (1f - p);
                }
                else scale = 1f;
                scale *= 1f - .06f * fadeOut;
                rise = Mathf.Lerp(0f, 10f, Mathf.Clamp01((t - Settle) / Mathf.Max(.01f, outStart - Settle))) + 86f * fadeOut * fadeOut;
                alpha = Mathf.Clamp01(t / .03f) * (1f - fadeOut * fadeOut * (3f - 2f * fadeOut));
            }
            float stackShrink = Mathf.Pow(.8f, e.Level);
            float stackDim = e.Level == 0 ? 1f : e.Level == 1 ? .92f : .7f;
            e.Root.anchoredPosition = new Vector2(e.Base.x + e.Offset.x, -(e.Base.y + e.Offset.y) + rise);
            e.Body.localScale = new Vector3(scale * stackShrink, scale * stackShrink, 1f);
            e.Body.localEulerAngles = new Vector3(0f, 0f, e.Spin);
            e.Group.alpha = alpha * stackDim;
            // Glow: a hot flash on impact that sinks to a faint halo.
            float flash = calm ? .35f : Mathf.Clamp01(1f - t / .32f);
            float baseGlow = e.Style == DamageNumberStyle1101.Critical ? .55f : .3f;
            e.Glow.canvasRenderer.SetAlpha(Mathf.Lerp(baseGlow, 1f, flash * flash) * (e.Level == 0 ? 1f : .4f));
            e.Glow.rectTransform.localScale = Vector3.one * (calm ? 1f : 1f + .35f * flash);
            if (e.Word.gameObject.activeSelf)
            {
                float w = calm ? 1f : Mathf.Clamp01((t - .04f) / .08f);
                e.Word.canvasRenderer.SetAlpha(w);
                e.Word.rectTransform.localScale = Vector3.one * (calm ? 1f : Mathf.Lerp(1.8f, 1f, 1f - (1f - w) * (1f - w)));
            }
        }

        private void Retire(Entry e)
        {
            e.Active = false;
            e.Root.gameObject.SetActive(false);
            if (newest == e) newest = null;
        }

        /// <summary>Removes every number and anything waiting behind the close-up (battle end, wave change).</summary>
        public void Clear()
        {
            pendingCount = 0; pendingHead = 0;
            foreach (Entry e in entries) if (e != null && e.Active) Retire(e);
        }

        // ---- inspection (tests / captures)
        public bool TryNewest(out int amount, out DamageNumberStyle1101 style, out float width, out float cap, out RectTransform root)
        {
            amount = 0; style = default; width = cap = 0; root = null;
            if (newest == null || !newest.Active) return false;
            amount = newest.Amount; style = newest.Style; width = newest.Width; cap = newest.Size; root = newest.Root;
            return true;
        }

        public int ActiveCountFor(int owner) { int n = 0; foreach (Entry e in entries) if (e != null && e.Active && e.Owner == owner) n++; return n; }

        /// <summary>Advance the clock by hand (tests, captures).</summary>
        public void Step(float seconds)
        {
            Func<float> old = delta;
            delta = () => seconds;
            try { Update(); } finally { delta = old; }
        }
    }
}
