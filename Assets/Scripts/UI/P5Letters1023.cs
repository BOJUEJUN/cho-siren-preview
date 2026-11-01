using System;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>
    /// P5 "ransom note" lettering (2026-10-23 review: the 击破/全灭 plate and the 队长演出 strip read as
    /// placeholders): every character on its own tilted plate, alternating white / black / red, like the
    /// title logo. Pure layout — callers animate the returned letters.
    /// </summary>
    public static class P5Letters1023
    {
        static readonly Color32 Ink = new Color32(14, 8, 20, 255), Paper = new Color32(246, 240, 248, 255), Red = Brand1025.Teal;

        /// <summary>Builds the letters centred on <paramref name="parent"/>; returns them left to right.</summary>
        public static RectTransform[] Build(RectTransform parent, Font font, string text, float size, int seed)
        {
            var rng = new System.Random(seed);
            var letters = new RectTransform[text.Length];
            float step = size * .92f, x = -(text.Length - 1) * step * .5f;
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                bool mark = ch == '!' || ch == '！' || ch == '·';
                // white, black, brand teal, white … with the punctuation always teal, the calling-card rhythm
                int scheme = mark ? 2 : i % 3;
                Color32 plate = scheme == 0 ? Paper : scheme == 1 ? Ink : Red;
                Color32 ink = scheme == 1 ? Paper : Ink;
                float s = size * (mark ? .72f : 1f) * (1f + ((float)rng.NextDouble() - .5f) * .16f);
                var holder = new GameObject("Letter-" + i, typeof(RectTransform)).GetComponent<RectTransform>();
                holder.SetParent(parent, false);
                holder.anchorMin = holder.anchorMax = holder.pivot = new Vector2(.5f, .5f);
                holder.sizeDelta = new Vector2(s, s);
                holder.anchoredPosition = new Vector2(x, ((float)rng.NextDouble() - .5f) * size * .14f);
                holder.localEulerAngles = new Vector3(0, 0, ((float)rng.NextDouble() - .5f) * 18f);
                Plate(holder, "Shadow", new Vector2(s * .07f, -s * .07f), new Vector2(s, s), Ink);
                Plate(holder, "Plate", Vector2.zero, new Vector2(s, s), plate);
                var t = new GameObject("Char", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)).GetComponent<Text>();
                t.transform.SetParent(holder, false);
                t.font = font; t.text = ch.ToString(); t.fontSize = Mathf.RoundToInt(s * .8f); t.fontStyle = FontStyle.Bold;
                t.color = ink; t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false;
                t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
                var tr = t.rectTransform; tr.anchorMin = tr.anchorMax = tr.pivot = new Vector2(.5f, .5f); tr.sizeDelta = new Vector2(s, s);
                letters[i] = holder;
                x += step;
            }
            return letters;
        }

        public static Image Plate(RectTransform parent, string name, Vector2 at, Vector2 size, Color color, float skew = 0f)
        {
            Graphic g;
            GameObject go;
            if (Mathf.Abs(skew) > .01f)
            {
                go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(ChoSiren.UI.SkewedPlate1021));
                var plate = go.GetComponent<ChoSiren.UI.SkewedPlate1021>(); plate.SkewDegrees = skew; g = plate;
            }
            else { go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)); g = go.GetComponent<Image>(); }
            go.transform.SetParent(parent, false);
            g.color = color; g.raycastTarget = false;
            var r = g.rectTransform; r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f); r.anchoredPosition = at; r.sizeDelta = size;
            return g as Image;
        }
    }

    /// <summary>
    /// 「队长演出!」 banner when the wave's dice target is reached (2026-10-23 review #35): a red ribbon
    /// sweeps in, ransom letters stamp in one by one, a gold tag names the hand, then it all snaps away.
    /// </summary>
    public sealed class CaptainShowBanner1023 : MonoBehaviour
    {
        RectTransform[] letters;
        RectTransform ribbon, tag;
        CanvasGroup group;
        Func<bool> paused;
        float clock;
        const float Life = 1.25f;

        public static CaptainShowBanner1023 Play(RectTransform layer, Vector2 localPos, Font font, string tagText, Func<bool> isPaused)
        {
            if (layer == null || !Application.isPlaying) return null;
            var root = new GameObject("CaptainShowBanner1023", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
            root.SetParent(layer, false); root.SetAsLastSibling();
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(.5f, .5f); root.sizeDelta = Vector2.zero;
            root.anchoredPosition = localPos; root.localEulerAngles = new Vector3(0, 0, 6f);
            var b = root.gameObject.AddComponent<CaptainShowBanner1023>();
            b.paused = isPaused; b.group = root.GetComponent<CanvasGroup>(); b.group.blocksRaycasts = false; b.group.interactable = false;
            b.ribbon = new GameObject("Ribbon", typeof(RectTransform)).GetComponent<RectTransform>();
            b.ribbon.SetParent(root, false); b.ribbon.anchorMin = b.ribbon.anchorMax = b.ribbon.pivot = new Vector2(.5f, .5f);
            P5Letters1023.Plate(b.ribbon, "Shadow", new Vector2(10, -10), new Vector2(470, 62), new Color32(14, 8, 20, 255), -18f);
            P5Letters1023.Plate(b.ribbon, "Red", Vector2.zero, new Vector2(470, 62), Brand1025.Teal, -18f);
            P5Letters1023.Plate(b.ribbon, "Stripe", new Vector2(0, -22), new Vector2(430, 6), new Color32(246, 240, 248, 255), -18f);
            var letterRoot = new GameObject("Letters", typeof(RectTransform)).GetComponent<RectTransform>();
            letterRoot.SetParent(root, false); letterRoot.anchorMin = letterRoot.anchorMax = letterRoot.pivot = new Vector2(.5f, .5f);
            b.letters = P5Letters1023.Build(letterRoot, font, "队长演出!", 64f, 1023);
            b.tag = new GameObject("Tag", typeof(RectTransform)).GetComponent<RectTransform>();
            b.tag.SetParent(root, false); b.tag.anchorMin = b.tag.anchorMax = b.tag.pivot = new Vector2(.5f, .5f);
            b.tag.anchoredPosition = new Vector2(-120, 58);
            P5Letters1023.Plate(b.tag, "Gold", Vector2.zero, new Vector2(230, 34), new Color32(255, 205, 110, 255), -14f);
            var t = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)).GetComponent<Text>();
            t.transform.SetParent(b.tag, false); t.font = font; t.text = tagText; t.fontSize = 20; t.fontStyle = FontStyle.BoldAndItalic;
            t.color = new Color32(14, 8, 20, 255); t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.rectTransform.sizeDelta = new Vector2(230, 34);
            b.Pose();
            return b;
        }

        void Update()
        {
            if (paused != null && paused()) return;
            clock += Mathf.Min(Time.unscaledDeltaTime, .05f);
            Pose();
            if (clock >= Life) Destroy(gameObject);
        }

        static float EaseOut(float t) { t = Mathf.Clamp01(t); return 1f - (1f - t) * (1f - t) * (1f - t); }

        void Pose()
        {
            bool calm = BattlePresentationOptions.ReduceMotion;
            float t = clock;
            // ribbon sweeps in from the left, letters slam in one after another, tag drops last
            ribbon.anchoredPosition = new Vector2(calm ? 0f : Mathf.Lerp(-520f, 0f, EaseOut(t / .16f)), 0f);
            for (int i = 0; i < letters.Length; i++)
            {
                float k = (t - .08f - i * .045f) / .09f;
                float s = calm ? 1f : k <= 0f ? 0f : k < 1f ? Mathf.Lerp(1.8f, 1f, EaseOut(k)) : 1f;
                letters[i].localScale = Vector3.one * s;
            }
            float tk = (t - .38f) / .1f;
            tag.localScale = calm ? Vector3.one : Vector3.one * (tk <= 0f ? 0f : Mathf.Lerp(1.5f, 1f, EaseOut(tk)));
            float exit = Mathf.Clamp01((t - (Life - .16f)) / .16f);
            transform.localScale = new Vector3(1f + exit * .15f, 1f - exit, 1f);
            group.alpha = 1f - exit * exit;
        }
    }
}
