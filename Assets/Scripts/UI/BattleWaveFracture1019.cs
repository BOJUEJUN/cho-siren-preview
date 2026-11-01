using System;
using System.Collections.Generic;
using ChoSiren.Panels;
using UnityEngine;

namespace ChoSiren
{
    /// <summary>
    /// Wave change. The original floor still breaks and reforms (2026-10-19), and since 2026-10-21
    /// (owner: "每一波野怪要有过渡动画") a P5 wave card cuts across the stage: black wedge and red
    /// band slam in, "第二波" stamps with a light flash and speed lines, the card tears away and the
    /// new enemies slide in. The battle waits for <see cref="IsShowing"/> to clear.
    /// </summary>
    public sealed class BattleWaveFracture1019 : MonoBehaviour
    {
        // 2026-10-21 research §5.8: ~1.15 real seconds at the normal .5 presentation pace —
        // slam (overshoot) → band → stamp + flash + shake → hold → card tears into shards → enemies spring in.
        public const float Duration = .58f;
        /// <summary>Fired on the stamp frame (sound + small shake owned by the battle panel).</summary>
        public Action OnStamp;
        bool stamped;
        RectTransform tilt, shardRoot;
        UnityEngine.UI.Image halftone;
        struct CardShard { public RectTransform Rect; public PolyGraphic1022 Graphic; public Vector2 Velocity; public float Spin; public Vector2 Home; }
        readonly List<CardShard> shards = new List<CardShard>();
        // 2026-10-23 review #51: the floor splits open, then closes as the new wave rises out of it.
        const float EnemyRise = 150f;
        RectTransform layer, host, card, wedge, band, title;
        BattleFloorFractureGraphic1019 floor;
        UnityEngine.UI.Text caption, titleText, countText, subText;
        UnityEngine.UI.Image dim, flash;
        readonly RectTransform[] speedLines = new RectTransform[6];
        CanvasGroup group, enemies, cardGroup;
        RectTransform enemyRect;
        Vector2 enemyOrigin;
        PanelKit kit;
        Func<bool> paused;
        Func<int> speed;
        float elapsed, enemyAlpha = 1;
        public bool IsShowing { get; private set; }
        public int Wave { get; private set; }
        public float VisualClock { get; private set; }
        public void Configure(RectTransform host, RectTransform enemyLayer, Sprite stage, Func<bool> pause, Func<int> playbackSpeed)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            paused = pause; speed = playbackSpeed;
            enemyRect = enemyLayer;
            enemies = enemyLayer != null ? enemyLayer.GetComponent<CanvasGroup>() : null;
            if (enemyLayer != null && enemies == null) enemies = enemyLayer.gameObject.AddComponent<CanvasGroup>();
            if (layer != null) return;
            this.host = host;
            kit = new PanelKit("WaveFracture1019"); layer = kit.NewRect("CrystalFloorWaveChange", host);
            PanelKit.PlaceTop(layer, 0, 665, 720, 220);
            group = layer.gameObject.AddComponent<CanvasGroup>(); group.blocksRaycasts = false; group.interactable = false;
            var go = new GameObject("OriginalFloorShards", typeof(RectTransform), typeof(CanvasRenderer), typeof(BattleFloorFractureGraphic1019));
            go.transform.SetParent(layer, false); floor = go.GetComponent<BattleFloorFractureGraphic1019>();
            PanelKit.Stretch(floor.rectTransform); floor.raycastTarget = false;
            floor.Configure(stage, new Rect(0, 665, 720, 220), host.rect.height > 0 ? host.rect.height : 1501.63f);
            caption = kit.NewPlacedText(layer, string.Empty, 30, Color.white, 140, 75, 440, 55, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic);
            caption.raycastTarget = false;
            layer.gameObject.SetActive(false);
            BuildCard();
        }

        void BuildCard()
        {
            card = kit.NewRect("WaveCard1021", host);
            PanelKit.Stretch(card);
            cardGroup = card.gameObject.AddComponent<CanvasGroup>(); cardGroup.blocksRaycasts = false; cardGroup.interactable = false;
            dim = kit.NewImage("Dim", card, null, new Color(.02f, .01f, .05f, 0f)); PanelKit.Stretch(dim.rectTransform); dim.raycastTarget = false;
            tilt = kit.NewRect("Tilt", card);
            PanelKit.PlaceTop(tilt, 0, 360, 720, 220);
            tilt.localEulerAngles = new Vector3(0, 0, 6f);
            wedge = Bar("Wedge", tilt, new Color32(10, 6, 18, 255), 1000, 250, 0);
            // P5 halftone in the dark wedge.
            halftone = kit.NewImage("Halftone", wedge, CelShapes.Halftone, Brand1025.TealA(70));
            halftone.type = UnityEngine.UI.Image.Type.Tiled; halftone.raycastTarget = false;
            halftone.pixelsPerUnitMultiplier = 2.2f;
            PanelKit.Stretch(halftone.rectTransform);
            // White hairlines make the black wedge read against the dark stage (2026-10-21).
            Bar("WedgeEdgeTop", wedge, Color.white, 1000, 4, 123);
            Bar("WedgeEdgeBottom", wedge, Color.white, 1000, 4, -123);
            band = Bar("Band", tilt, Brand1025.Teal, 1000, 150, -2);
            Bar("EdgeTop", band, Color.white, 1000, 7, 78);
            Bar("EdgeBottom", band, Color.white, 1000, 7, -78);
            for (int i = 0; i < speedLines.Length; i++)
            {
                var line = BattleFxArt1021.NewImage("Speed-" + i, band, BattleFxArt1021.Spark, new Color(1, 1, 1, .5f), true);
                speedLines[i] = line.rectTransform;
                speedLines[i].sizeDelta = new Vector2(220 + i % 3 * 60, 6 + i % 2 * 3);
            }
            title = kit.NewRect("Title", tilt);
            title.anchorMin = title.anchorMax = title.pivot = new Vector2(.5f, .5f);
            title.sizeDelta = new Vector2(560, 150); title.anchoredPosition = new Vector2(-40, 0);
            titleText = Label(title, 104, Color.white, new Vector2(560, 150), Vector2.zero, 5f);
            countText = Label(band, 44, Color.white, new Vector2(170, 64), new Vector2(262, -30), 3.5f);
            subText = Label(wedge, 34, new Color32(255, 220, 70, 255), new Vector2(560, 48), new Vector2(-20, -100), 3f);
            flash = BattleFxArt1021.NewImage("Flash", band, null, new Color(1, 1, 1, 0), true);
            flash.rectTransform.sizeDelta = new Vector2(1000, 140);
            BuildShards();
            title.SetAsLastSibling(); // the stamped title stays above the flying shards
            card.gameObject.SetActive(false);
        }

        /// <summary>The card pre-cut into jagged slices; they replace the card on the tear frame.</summary>
        void BuildShards()
        {
            shardRoot = kit.NewRect("Shards", tilt);
            shardRoot.anchorMin = shardRoot.anchorMax = shardRoot.pivot = new Vector2(.5f, .5f);
            shardRoot.sizeDelta = Vector2.zero; shardRoot.anchoredPosition = Vector2.zero;
            var random = new System.Random(1021);
            float[] cuts = { -500, -340, -190, -60, 70, 200, 330, 500 };
            for (int layerIndex = 0; layerIndex < 2; layerIndex++)
            {
                float half = layerIndex == 0 ? 125f : 75f;
                float y0 = layerIndex == 0 ? 0f : -2f;
                Color color = layerIndex == 0 ? new Color32(10, 6, 18, 255) : (Color)Brand1025.Teal;
                for (int i = 0; i < cuts.Length - 1; i++)
                {
                    float lean0 = i == 0 ? 0f : (float)(random.NextDouble() * 70 - 35);
                    float lean1 = i == cuts.Length - 2 ? 0f : (float)(random.NextDouble() * 70 - 35);
                    float cx = (cuts[i] + cuts[i + 1]) * .5f;
                    var points = new[]
                    {
                        new Vector2(cuts[i] - lean0 - cx, -half), new Vector2(cuts[i] + lean0 - cx, half),
                        new Vector2(cuts[i + 1] + lean1 - cx, half), new Vector2(cuts[i + 1] - lean1 - cx, -half),
                    };
                    var go = new GameObject("Shard", typeof(RectTransform), typeof(CanvasRenderer), typeof(PolyGraphic1022));
                    go.transform.SetParent(shardRoot, false);
                    var poly = go.GetComponent<PolyGraphic1022>();
                    poly.color = color; poly.raycastTarget = false; poly.SetPoints(points);
                    var r = poly.rectTransform; r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f);
                    r.sizeDelta = Vector2.one; r.anchoredPosition = new Vector2(cx, y0);
                    float side = cx / 500f;
                    shards.Add(new CardShard
                    {
                        Rect = r, Graphic = poly, Home = new Vector2(cx, y0),
                        Velocity = new Vector2(side * 900f + (float)(random.NextDouble() * 300 - 150), (float)(random.NextDouble() * 900 - 450)),
                        Spin = (float)(random.NextDouble() * 500 - 250),
                    });
                }
            }
            shardRoot.gameObject.SetActive(false);
        }

        RectTransform Bar(string name, Transform parent, Color color, float w, float h, float y)
        {
            var image = kit.NewImage(name, parent, null, color); image.raycastTarget = false;
            var r = image.rectTransform; r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f);
            r.sizeDelta = new Vector2(w, h); r.anchoredPosition = new Vector2(0, y);
            return r;
        }

        UnityEngine.UI.Text Label(Transform parent, int size, Color color, Vector2 box, Vector2 at, float edge)
        {
            var text = kit.NewPlacedText(parent, string.Empty, size, color, 0, 0, box.x, box.y, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic);
            text.raycastTarget = false; text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow; // a CJK line taller than its box would vanish
            var r = text.rectTransform; r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f);
            r.sizeDelta = box; r.anchoredPosition = at;
            var outline = text.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color32(12, 4, 20, 255); outline.effectDistance = new Vector2(edge, -edge);
            return text;
        }

        static string Numeral(int n)
        {
            string[] cn = { "零", "一", "二", "三", "四", "五", "六", "七", "八", "九", "十" };
            return n >= 0 && n < cn.Length ? cn[n] : n.ToString();
        }

        public bool Play(int wave) => Play(wave, 0);

        /// <summary>Boss stages have no waves: the same card announces the boss once at the start.</summary>
        public bool PlayBoss(string bossName)
            => Announce("首领登场", bossName ?? string.Empty, string.Empty); // 2026-11-02 visual-battle: no slogan, just her name

        /// <summary>Boss phase change (2026-10-21): the same card stamps the new phase instead of a HUD pulse.</summary>
        public bool PlayPhase(int phase)
            => Announce(phase >= 3 ? "最终乐章" : "第二乐章", string.Empty, Mathf.Clamp(phase, 2, 3) + " / 3"); // the boss surge shows it

        bool Announce(string title, string sub, string count)
        {
            if (layer == null || !isActiveAndEnabled || IsShowing || BattlePresentationOptions.ReduceMotion) return false;
            enemyAlpha = enemies != null ? enemies.alpha : 1;
            if (enemyRect != null) enemyOrigin = enemyRect.anchoredPosition;
            elapsed = 0; IsShowing = true; caption.text = string.Empty; stamped = false;
            titleText.text = title; countText.text = count;
            subText.text = sub;
            layer.gameObject.SetActive(true);
            card.gameObject.SetActive(true); card.SetAsLastSibling();
            Pose(); return true;
        }
        public bool Play(int wave, int total)
        {
            if (layer == null || !isActiveAndEnabled || wave < 2 || wave <= Wave) return false;
            Cancel(); Wave = wave;
            if (BattlePresentationOptions.ReduceMotion) return true;
            enemyAlpha = enemies != null ? enemies.alpha : 1;
            if (enemyRect != null) enemyOrigin = enemyRect.anchoredPosition;
            elapsed = 0; IsShowing = true; caption.text = string.Empty; stamped = false;
            titleText.text = "第" + Numeral(wave) + "波";
            countText.text = total > 0 ? wave + " / " + total : string.Empty;
            subText.text = string.Empty; // 2026-11-02 visual-battle: 第N波 + N / M say it all
            layer.gameObject.SetActive(true);
            card.gameObject.SetActive(true); card.SetAsLastSibling();
            Pose(); return true;
        }
        void Update() => AdvancePresentation(Mathf.Min(Time.unscaledDeltaTime, .1f));
        public void AdvancePresentation(float seconds)
        {
            if (BattlePresentationOptions.ReduceMotion) { Cancel(); return; }
            if (!IsShowing || (paused?.Invoke() ?? false) || seconds <= 0 || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            float dt = seconds * Mathf.Clamp(speed?.Invoke() ?? 1, 1, 1.25f) * BattlePresentationOptions.Pace;
            VisualClock += dt; elapsed += dt;
            if (elapsed >= Duration) Cancel(); else Pose();
        }
        static float Out(float t) { t = Mathf.Clamp01(t); return 1f - (1f - t) * (1f - t) * (1f - t); }
        static float In(float t) { t = Mathf.Clamp01(t); return t * t * t; }
        static float Back(float t) { t = Mathf.Clamp01(t); const float c = 1.9f; float u = t - 1f; return 1f + (c + 1f) * u * u * u + c * u * u; }
        void Pose()
        {
            float t = Mathf.Clamp01(elapsed / Duration);
            group.alpha = Mathf.Min(Mathf.Clamp01(t / .09f), Mathf.Clamp01((1 - t) / .15f));
            floor.SetFrame(t);
            const float Tear = .58f;
            bool torn = t >= Tear;
            dim.color = new Color(.02f, .01f, .05f, .55f * Mathf.Min(Mathf.Clamp01(t / .08f), Mathf.Clamp01((.85f - t) / .25f)));
            // Slam: the wedge drives in from the left and overshoots, the red band follows from the right.
            wedge.anchoredPosition = new Vector2(Mathf.LerpUnclamped(-1000f, 0f, Back(t / .1f)), 0f);
            band.anchoredPosition = new Vector2(Mathf.LerpUnclamped(1000f, 0f, Back((t - .07f) / .1f)), -2f);
            wedge.gameObject.SetActive(!torn);
            band.gameObject.SetActive(!torn);
            // Stamp: title 1.6 → 1.0 in ~3 frames with a white flash; the panel adds sound + shake.
            float stamp = Mathf.Clamp01((t - .17f) / .05f);
            title.localScale = Vector3.one * Mathf.Lerp(1.6f, 1f, stamp);
            title.anchoredPosition = new Vector2(-40 + 14 * Mathf.Clamp01((t - .22f) / .36f), 0);
            SetAlpha(titleText, t < .17f ? 0f : 1f);
            SetAlpha(countText, Mathf.Clamp01((t - .22f) / .04f));
            SetAlpha(subText, Mathf.Clamp01((t - .24f) / .05f));
            flash.color = new Color(1, 1, 1, .9f * Mathf.Clamp01(1f - Mathf.Abs(t - .19f) / .04f));
            if (!stamped && t >= .17f)
            {
                stamped = true;
                try { OnStamp?.Invoke(); } catch (Exception e) { Debug.LogWarning("Wave stamp hook: " + e.Message); }
            }
            for (int i = 0; i < speedLines.Length; i++)
            {
                float x = Mathf.Repeat(VisualClock * 1500f + i * 197f, 1200f);
                speedLines[i].anchoredPosition = new Vector2(600f - x, -46 + i * 18.5f);
            }
            // Tear: the card breaks into its pre-cut slices, which fly apart, spin and fade.
            shardRoot.gameObject.SetActive(torn && t < .9f);
            if (torn)
            {
                float since = (t - Tear) * Duration / BattlePresentationOptions.Pace; // real seconds
                foreach (CardShard shard in shards)
                {
                    shard.Rect.anchoredPosition = shard.Home + shard.Velocity * since + new Vector2(0f, -900f * since * since);
                    shard.Rect.localEulerAngles = new Vector3(0, 0, shard.Spin * since);
                    float fade = 1f - Mathf.Clamp01(since / .35f);
                    shard.Rect.localScale = Vector3.one * Mathf.Lerp(.6f, 1f, fade);
                    var c = shard.Graphic.color; c.a = fade; shard.Graphic.color = c;
                }
                title.localScale = Vector3.one * (1f + .5f * Mathf.Clamp01(since / .15f));
                SetAlpha(titleText, 1f - Mathf.Clamp01(since / .12f));
                SetAlpha(countText, 0f); SetAlpha(subText, 0f);
            }
            // New enemies rise out of the opened floor (ease-out-back) as the card breaks and the floor closes.
            float enter = Back((t - .58f) / .34f);
            if (enemies != null) enemies.alpha = enemyAlpha * Mathf.Clamp01((t - .56f) / .1f);
            if (enemyRect != null) enemyRect.anchoredPosition = enemyOrigin + new Vector2(0f, -EnemyRise * (1f - enter));
        }
        static void SetAlpha(UnityEngine.UI.Graphic g, float a) { var c = g.color; c.a = a; g.color = c; }
        public void Cancel()
        {
            if (IsShowing && enemies != null) enemies.alpha = enemyAlpha;
            if (IsShowing && enemyRect != null) enemyRect.anchoredPosition = enemyOrigin;
            IsShowing = false; if (layer != null) layer.gameObject.SetActive(false);
            if (card != null) card.gameObject.SetActive(false);
        }
        void OnDisable() => Cancel();
        void OnDestroy() { Cancel(); if (layer != null) Destroy(layer.gameObject); if (card != null) Destroy(card.gameObject); kit?.Dispose(); }
    }

    public sealed class BattleFloorFractureGraphic1019 : UnityEngine.UI.MaskableGraphic
    {
        Sprite stage;
        Rect sourceRegion;
        float fullHeight, phase;
        public override Texture mainTexture => stage != null ? stage.texture : Texture2D.whiteTexture;
        public void Configure(Sprite source, Rect region, float sourceHeight) { stage = source; sourceRegion = region; fullHeight = Mathf.Max(1, sourceHeight); SetVerticesDirty(); }
        public void SetFrame(float t) { phase = Mathf.Clamp01(t); SetVerticesDirty(); }
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh)
        {
            vh.Clear(); Rect r = rectTransform.rect;
            // opens fast, holds wide while the card stamps, closes as the new wave rises
            float split = phase < .2f ? Mathf.SmoothStep(0f, 1f, phase / .2f) : phase < .6f ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (phase - .6f) / .35f);
            Quad(vh, r, new Color32(46, 6, 58, (byte)(split * 235)));
            // A fan of six real floor pieces pulls apart along the crystal joints, then closes.
            Vector2 center = new Vector2(r.center.x, r.center.y - r.height * .1f);
            Vector2[] rim = { new Vector2(r.xMin, r.yMin), new Vector2(r.xMin, r.yMax),
                new Vector2(r.center.x - 35, r.yMax), new Vector2(r.xMax, r.yMax),
                new Vector2(r.xMax, r.yMin), new Vector2(r.center.x + 55, r.yMin) };
            for (int i = 0; i < rim.Length; i++)
            {
                Vector2 a = rim[i], b = rim[(i + 1) % rim.Length];
                Vector2 centroid = (a + b + center) / 3;
                Vector2 offset = (centroid - center).normalized * (46 + i % 3 * 16) * split;
                int start = vh.currentVertCount;
                Vertex(vh, center + offset, center, r); Vertex(vh, a + offset, a, r); Vertex(vh, b + offset, b, r);
                vh.AddTriangle(start, start + 1, start + 2);
            }
        }
        void Vertex(UnityEngine.UI.VertexHelper vh, Vector2 position, Vector2 original, Rect r)
        {
            float x = sourceRegion.x + (original.x - r.xMin) / Mathf.Max(1, r.width) * sourceRegion.width;
            float y = sourceRegion.y + (r.yMax - original.y) / Mathf.Max(1, r.height) * sourceRegion.height;
            Vector2 uv = new Vector2(x / 720, 1 - y / fullHeight);
            vh.AddVert(position, stage != null ? Color.white : new Color32(64, 30, 104, 255), uv);
        }
        static void Quad(UnityEngine.UI.VertexHelper vh, Rect r, Color tint)
        {
            vh.AddVert(new Vector2(r.xMin, r.yMin), tint, Vector2.zero); vh.AddVert(new Vector2(r.xMin, r.yMax), tint, Vector2.zero);
            vh.AddVert(new Vector2(r.xMax, r.yMax), tint, Vector2.zero); vh.AddVert(new Vector2(r.xMax, r.yMin), tint, Vector2.zero);
            vh.AddTriangle(0, 1, 2); vh.AddTriangle(0, 2, 3);
        }
    }

    /// <summary>Convex polygon fill (card shards).</summary>
    public sealed class PolyGraphic1022 : UnityEngine.UI.MaskableGraphic
    {
        Vector2[] points = new Vector2[0];
        public void SetPoints(Vector2[] value) { points = value ?? new Vector2[0]; SetVerticesDirty(); }
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh)
        {
            vh.Clear();
            if (points.Length < 3) return;
            Color32 c = color;
            foreach (Vector2 p in points) vh.AddVert(p, c, Vector2.zero);
            for (int i = 1; i < points.Length - 1; i++) vh.AddTriangle(0, i, i + 1);
        }
    }
}
