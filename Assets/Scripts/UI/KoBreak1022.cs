using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>
    /// Enemy knock-out (2026-10-21): instead of a quiet fade, the enemy flashes into a white
    /// silhouette, erodes away with a hot-pink rim while red/black/white cel shards burst out,
    /// and a slanted 「击破」 plate stamps over it. Presentation only; the battle already counted the KO.
    /// </summary>
    public sealed class KoBreak1022 : MonoBehaviour
    {
        struct Bit { public PolyGraphic1022 Graphic; public Vector2 Velocity; public float Spin; }

        const float FlashPhase = .08f;
        float Life = .78f;
        static readonly int FlashId = Shader.PropertyToID("_Flash");
        static readonly int ErodeId = Shader.PropertyToID("_Erode");

        readonly List<Bit> bits = new List<Bit>();
        Image ghost;
        Material flashMat, erodeMat;
        RectTransform ghostRect, stamp;
        Vector3 ghostScale;
        Vector2 ghostHome;
        Func<bool> paused;
        float clock, stampSize = 1f;
        bool eroding;

        public static KoBreak1022 Play(RectTransform layer, Image figure, Font font, Func<bool> isPaused)
            => Play(layer, figure, font, isPaused, false);

        /// <summary>finale: the last enemy of the battle — a bigger 「全灭!」 plate that holds a little longer.</summary>
        public static KoBreak1022 Play(RectTransform layer, Image figure, Font font, Func<bool> isPaused, bool finale)
        {
            if (layer == null || figure == null || figure.sprite == null || !Application.isPlaying) return null;
            var go = new GameObject("KoBreak1022", typeof(RectTransform));
            go.transform.SetParent(layer, false); go.transform.SetAsLastSibling();
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f); rect.sizeDelta = Vector2.zero;
            Vector3 world = figure.rectTransform.TransformPoint(figure.rectTransform.rect.center);
            rect.anchoredPosition = (Vector2)layer.InverseTransformPoint(world) - layer.rect.center;
            var fx = go.AddComponent<KoBreak1022>();
            fx.paused = isPaused;
            if (finale) fx.Life = 1.1f;
            fx.Build(figure, font, finale);
            return fx;
        }

        void Build(Image figure, Font font, bool finale)
        {
            // Ghost copy of the enemy: built under the figure's parent so it inherits its exact pose, then moved here.
            var go = new GameObject("Ghost", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            ghostRect = (RectTransform)go.transform;
            ghostRect.SetParent(figure.rectTransform.parent, false);
            RectTransform source = figure.rectTransform;
            ghostRect.anchorMin = source.anchorMin; ghostRect.anchorMax = source.anchorMax; ghostRect.pivot = source.pivot;
            ghostRect.anchoredPosition = source.anchoredPosition; ghostRect.sizeDelta = source.sizeDelta;
            ghostRect.localRotation = source.localRotation; ghostRect.localScale = source.localScale;
            ghostRect.SetParent(transform, true);
            ghostScale = ghostRect.localScale; ghostHome = ghostRect.anchoredPosition;
            ghost = go.GetComponent<Image>();
            ghost.sprite = figure.sprite; ghost.preserveAspect = figure.preserveAspect; ghost.raycastTarget = false;
            Shader flash = Resources.Load<Shader>("Shaders/UIFlash");
            if (flash != null && flash.isSupported)
            {
                flashMat = new Material(flash) { name = "KoFlash", hideFlags = HideFlags.DontSave };
                flashMat.SetFloat(FlashId, 1f);
                ghost.material = flashMat;
            }
            erodeMat = CelMaterials.NewErode(Brand1025.Teal);
            if (erodeMat != null) { erodeMat.SetFloat("_EdgeWidth", .1f); erodeMat.SetFloat("_NoiseScale", 2.2f); }

            bool calm = BattlePresentationOptions.ReduceMotion;
            int count = calm ? 4 : finale ? 20 : 12;
            Color[] colors = { Brand1025.Teal, Color.white, new Color32(14, 8, 20, 255) };
            for (int i = 0; i < count; i++)
            {
                var bitGo = new GameObject("Bit", typeof(RectTransform), typeof(CanvasRenderer), typeof(PolyGraphic1022));
                bitGo.transform.SetParent(transform, false);
                var g = bitGo.GetComponent<PolyGraphic1022>();
                g.raycastTarget = false; g.color = colors[i % colors.Length];
                float r = UnityEngine.Random.Range(14f, 34f);
                float a0 = UnityEngine.Random.Range(0f, 6.28f);
                g.SetPoints(new[]
                {
                    new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * r,
                    new Vector2(Mathf.Cos(a0 + 2.3f), Mathf.Sin(a0 + 2.3f)) * r * .55f,
                    new Vector2(Mathf.Cos(a0 + 3.9f), Mathf.Sin(a0 + 3.9f)) * r * .8f,
                });
                float dir = UnityEngine.Random.Range(.15f, Mathf.PI - .15f) + (i % 4 == 3 ? Mathf.PI : 0f);
                float speed = UnityEngine.Random.Range(520f, 980f);
                bits.Add(new Bit { Graphic = g, Velocity = new Vector2(Mathf.Cos(dir), Mathf.Sin(dir)) * speed, Spin = UnityEngine.Random.Range(-720f, 720f) });
                g.rectTransform.localScale = Vector3.zero;
            }

            // 2026-10-23 review ("太临时"): P5 calling-card lettering — each character on its own tilted
            // white/black/teal plate over a brand-teal slash ribbon, the same language as the title logo.
            stamp = new GameObject("Stamp", typeof(RectTransform)).GetComponent<RectTransform>();
            stamp.SetParent(transform, false);
            stamp.anchorMin = stamp.anchorMax = stamp.pivot = new Vector2(.5f, .5f);
            stamp.sizeDelta = new Vector2(240f, 90f);
            stamp.localEulerAngles = new Vector3(0, 0, -7f);
            stampSize = finale ? 1.35f : 1f;
            // Above the hit point so damage popups and crit strips do not cover the word.
            stamp.anchoredPosition = new Vector2(10f, finale ? 190f : 130f);
            string word = finale ? "全灭!" : "击破!";
            P5Letters1023.Plate(stamp, "RibbonShadow", new Vector2(8, -14), new Vector2(word.Length * 70f + 50f, 40), new Color32(14, 8, 20, 255), -20f);
            P5Letters1023.Plate(stamp, "Ribbon", new Vector2(0, -6), new Vector2(word.Length * 70f + 50f, 40), Brand1025.Teal, -20f);
            var letterRoot = new GameObject("Letters", typeof(RectTransform)).GetComponent<RectTransform>();
            letterRoot.SetParent(stamp, false); letterRoot.anchorMin = letterRoot.anchorMax = letterRoot.pivot = new Vector2(.5f, .5f);
            P5Letters1023.Build(letterRoot, font, word, 74f, finale ? 7 : 3);
            stamp.localScale = Vector3.zero;
            Pose();
        }

        void Update()
        {
            if (paused != null && paused()) return;
            clock += BattleHitStop.Delta(Mathf.Min(Time.unscaledDeltaTime, .05f));
            Pose();
            if (clock >= Life) Destroy(gameObject);
        }

        void Pose()
        {
            float t = clock;
            // Ghost: white silhouette for the first frames, then a cel erosion with a hot-pink rim.
            if (!eroding && t >= FlashPhase && erodeMat != null) { eroding = true; ghost.material = erodeMat; }
            float e = Mathf.Clamp01((t - FlashPhase) / .42f);
            if (erodeMat != null) erodeMat.SetFloat(ErodeId, eroding ? Mathf.Lerp(.05f, 1.02f, e) : 0f);
            ghost.color = eroding ? new Color(.7f, 1f, .78f, 1f) : Color.white;
            if (erodeMat == null && eroding) ghost.color = new Color(1, 1, 1, 1f - e);
            ghostRect.localScale = ghostScale * (t < FlashPhase ? 1f + .08f * (t / FlashPhase) : Mathf.Lerp(1.08f, 1.14f, e));
            ghostRect.anchoredPosition = ghostHome + Vector2.up * (16f * e);
            ghost.enabled = e < 1f;

            foreach (Bit bit in bits)
            {
                float k = Mathf.Max(0f, t - .02f);
                var r = bit.Graphic.rectTransform;
                r.anchoredPosition = bit.Velocity * k + .5f * new Vector2(0f, -1900f) * k * k;
                r.localEulerAngles = new Vector3(0, 0, bit.Spin * k);
                float s = t < .02f ? 0f : Mathf.Clamp01(1.3f - k * 1.6f);
                r.localScale = Vector3.one * s;
            }

            // Stamp slams 1.7 → 1 at .06s, holds, then shrinks out at the end.
            float st = t < .06f ? 0f : Mathf.Lerp(1.7f, 1f, Mathf.Clamp01((t - .06f) / .07f));
            float exit = Mathf.Clamp01((t - (Life - .14f)) / .14f);
            stamp.localScale = Vector3.one * st * stampSize * (1f - exit);
        }

        void OnDestroy()
        {
            if (flashMat != null) Destroy(flashMat);
            if (erodeMat != null) Destroy(erodeMat);
        }
    }
}
