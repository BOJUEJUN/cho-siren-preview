using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>
    /// Dice bonus hand-off (owner 2026-10-20): after a hand scores, glowing orbs leave each scoring
    /// die and arc into the captain's card; when the last lands the card punches and the gain
    /// ("攻击 +3.7%") pops above it in gold.
    /// </summary>
    public sealed class DiceBonusFlight1020 : MonoBehaviour
    {
        private const float Flight = .42f, Stagger = .07f, Pop = .95f;
        private readonly List<RectTransform> orbs = new List<RectTransform>();
        private readonly List<Vector2> starts = new List<Vector2>();
        private RectTransform layer, target, label;
        private Text text;
        private Vector2 end;
        private Vector3 targetScale;
        private float clock;
        private Func<bool> paused;
        private Action arrived;
        private bool landed;

        public static void Play(RectTransform layer, IList<RectTransform> fromDice, RectTransform captain, string gain,
            Font font, Func<bool> isPaused, Action onArrive)
        {
            if (layer == null || captain == null || fromDice == null || fromDice.Count == 0) { onArrive?.Invoke(); return; }
            var go = new GameObject("DiceBonusFlight", typeof(RectTransform));
            go.transform.SetParent(layer, false); go.transform.SetAsLastSibling();
            var rect = (RectTransform)go.transform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f); rect.sizeDelta = Vector2.zero;
            var fx = go.AddComponent<DiceBonusFlight1020>();
            fx.layer = layer; fx.target = captain; fx.paused = isPaused; fx.arrived = onArrive;
            fx.targetScale = captain.localScale;
            fx.end = Local(layer, captain) + new Vector2(0f, captain.rect.height * .1f);
            foreach (RectTransform die in fromDice)
            {
                if (die == null) continue;
                fx.starts.Add(Local(layer, die));
                fx.orbs.Add(fx.Orb());
            }
            var labelGo = new GameObject("Gain", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelGo.transform.SetParent(go.transform, false);
            fx.text = labelGo.GetComponent<Text>();
            fx.text.font = font; fx.text.text = gain; fx.text.fontSize = 34; fx.text.fontStyle = FontStyle.BoldAndItalic;
            fx.text.alignment = TextAnchor.MiddleCenter; fx.text.horizontalOverflow = HorizontalWrapMode.Overflow;
            fx.text.color = new Color(1f, .84f, .36f, 0f); fx.text.raycastTarget = false;
            var outline = labelGo.AddComponent<Outline>(); outline.effectColor = new Color(.1f, .04f, .12f, 1f); outline.effectDistance = new Vector2(2.5f, -2.5f);
            fx.label = fx.text.rectTransform; fx.label.sizeDelta = new Vector2(320f, 50f);
            fx.label.anchorMin = fx.label.anchorMax = fx.label.pivot = new Vector2(.5f, .5f);
            fx.Pose();
        }

        private RectTransform Orb()
        {
            var go = new GameObject("Orb", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(transform, false);
            // 2026-10-21: hard-edged four-point stars (cel) instead of soft glow orbs.
            var image = go.GetComponent<Image>(); image.sprite = CelShapes.Star4; image.raycastTarget = false;
            if (BattleFxArt1021.Additive != null) image.material = BattleFxArt1021.Additive;
            image.color = new Color(1f, .82f, .35f, 0f);
            var rect = image.rectTransform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(38f, 38f);
            return rect;
        }

        private static Vector2 Local(RectTransform layer, RectTransform rect) =>
            (Vector2)layer.InverseTransformPoint(rect.TransformPoint(rect.rect.center)) - layer.rect.center;

        private void Update()
        {
            if (paused != null && paused()) return;
            clock += Mathf.Min(Time.unscaledDeltaTime, .05f);
            Pose();
            float total = Flight + Stagger * Mathf.Max(0, orbs.Count - 1) + Pop;
            if (clock > total + .05f) { if (target != null) target.localScale = targetScale; Destroy(gameObject); }
        }

        private void Pose()
        {
            float lastLanding = Flight + Stagger * Mathf.Max(0, orbs.Count - 1);
            for (int i = 0; i < orbs.Count; i++)
            {
                float f = Mathf.Clamp01((clock - i * Stagger) / Flight);
                float e = 1f - (1f - f) * (1f - f);
                Vector2 a = starts[i], b = end;
                Vector2 mid = (a + b) * .5f + new Vector2((i - orbs.Count * .5f) * 30f, 140f);
                Vector2 p = Vector2.Lerp(Vector2.Lerp(a, mid, e), Vector2.Lerp(mid, b, e), e); // quadratic arc
                orbs[i].anchoredPosition = p;
                orbs[i].sizeDelta = Vector2.one * Mathf.Lerp(58f, 34f, e);
                orbs[i].localEulerAngles = new Vector3(0, 0, clock * 540f + i * 25f);
                var image = orbs[i].GetComponent<Image>();
                image.color = new Color(1f, .82f + .15f * (1f - e), .35f + .5f * (1f - e), clock < i * Stagger ? 0f : f < 1f ? 1f : 0f);
            }
            if (!landed && clock >= lastLanding)
            {
                landed = true;
                try { arrived?.Invoke(); } catch (Exception ex) { Debug.LogWarning(ex.Message); }
            }
            float pop = Mathf.Clamp01((clock - lastLanding) / Pop);
            if (target != null)
            {
                // Every arriving star nudges the card; the last one gives the full punch.
                float punch = 0f;
                for (int i = 0; i < orbs.Count; i++)
                {
                    float since = clock - (i * Stagger + Flight);
                    if (since >= 0f && since < .1f) punch = Mathf.Max(punch, Mathf.Sin(since / .1f * Mathf.PI) * .04f);
                }
                if (clock >= lastLanding) punch = Mathf.Max(punch, Mathf.Sin(Mathf.Clamp01(pop / .3f) * Mathf.PI) * .1f);
                target.localScale = targetScale * (1f + punch);
            }
            label.anchoredPosition = end + new Vector2(0f, 70f + 60f * pop);
            label.localScale = Vector3.one * (pop < .15f ? Mathf.Lerp(1.6f, 1f, pop / .15f) : 1f);
            var c = text.color; c.a = clock < lastLanding ? 0f : 1f - Mathf.SmoothStep(.6f, 1f, pop); text.color = c;
        }

        private static Sprite soft;
        private static Sprite Soft()
        {
            if (soft != null) return soft;
            const int n = 48;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float d = new Vector2(x - n / 2f + .5f, y - n / 2f + .5f).magnitude / (n / 2f);
                float core = Mathf.Clamp01(1f - d * 2.2f);
                px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(Mathf.Pow(Mathf.Clamp01(1f - d), 2f) + core));
            }
            tex.SetPixels32(px); tex.Apply();
            soft = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(.5f, .5f), 100f);
            soft.hideFlags = HideFlags.DontSave;
            return soft;
        }
    }
}
