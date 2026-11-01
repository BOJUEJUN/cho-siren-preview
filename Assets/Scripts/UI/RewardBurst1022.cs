using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.UI
{
    /// <summary>
    /// Non-blocking "获得" banner (2026-10-21): a red P5 strip slams across the middle of the screen,
    /// a white title stamps on it and the reward icons pop in one by one with their amounts, then the
    /// strip slides away. Used for chapter rewards/tasks and equipment upgrades instead of a bare toast.
    /// </summary>
    public sealed class RewardBurst1022 : MonoBehaviour
    {
        RectTransform strip, title;
        readonly List<RectTransform> items = new List<RectTransform>();
        CanvasGroup group;
        float clock;
        const float Life = 1.8f;

        public static RewardBurst1022 Play(Transform anyChild, Font font, string heading, IList<(string ItemId, int Amount)> rewards)
            => Play(anyChild, font, heading, rewards, null);

        /// <summary>labels: optional per-item caption (e.g. "Lv.2") instead of "×amount".</summary>
        public static RewardBurst1022 Play(Transform anyChild, Font font, string heading, IList<(string ItemId, int Amount)> rewards,
            IList<string> labels)
        {
            if (anyChild == null || !Application.isPlaying) return null; // presentation only
            var canvas = anyChild.GetComponentInParent<Canvas>();
            Transform host = canvas != null ? canvas.rootCanvas.transform : anyChild;
            Transform old = host.Find("RewardBurst1022");
            if (old != null) Destroy(old.gameObject);
            var go = new GameObject("RewardBurst1022", typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(host, false); go.transform.SetAsLastSibling();
            var burst = go.AddComponent<RewardBurst1022>();
            burst.Build(font, heading, rewards, labels);
            if (BattlePresentationOptions.ReduceMotion) burst.clock = .6f;
            burst.Pose();
            return burst;
        }

        static void Centre(RectTransform r, Vector2 at, Vector2 size) { r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f); r.anchoredPosition = at; r.sizeDelta = size; }

        Image Img(string name, Transform parent, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>(); image.sprite = sprite; image.color = color; image.raycastTarget = false;
            return image;
        }

        Text Label(Transform parent, Font font, string value, int size, Color color)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = font; t.text = value; t.fontSize = size; t.color = color; t.fontStyle = FontStyle.BoldAndItalic;
            t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            var o = go.AddComponent<Outline>(); o.effectColor = Color.black; o.effectDistance = new Vector2(2.5f, -2.5f);
            return t;
        }

        void Build(Font font, string heading, IList<(string ItemId, int Amount)> rewards, IList<string> labels)
        {
            var root = (RectTransform)transform;
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = root.offsetMax = Vector2.zero;
            group = GetComponent<CanvasGroup>(); group.blocksRaycasts = false; group.interactable = false;
            strip = new GameObject("Strip", typeof(RectTransform)).GetComponent<RectTransform>();
            strip.SetParent(transform, false); Centre(strip, new Vector2(0, 60), new Vector2(1400, 230));
            strip.localEulerAngles = new Vector3(0, 0, 6f);
            var black = Img("Black", strip, null, new Color32(10, 6, 18, 255)); Centre(black.rectTransform, new Vector2(0, -8), new Vector2(1400, 250));
            var red = Img("Red", strip, null, Brand1025.Teal); Centre(red.rectTransform, Vector2.zero, new Vector2(1400, 210));
            var edge = Img("Edge", strip, null, Color.white); Centre(edge.rectTransform, new Vector2(0, 108), new Vector2(1400, 8));
            title = new GameObject("Title", typeof(RectTransform)).GetComponent<RectTransform>();
            title.SetParent(strip, false); Centre(title, new Vector2(-170, 62), new Vector2(360, 70));
            var plate = Img("Plate", title, null, Color.white); Centre(plate.rectTransform, Vector2.zero, new Vector2(360, 70));
            Label(title, font, heading, 40, new Color32(20, 10, 24, 255)).GetComponent<Outline>().enabled = false;
            int count = Mathf.Min(5, rewards != null ? rewards.Count : 0);
            float spacing = 150f, left = -(count - 1) * spacing * .5f;
            for (int i = 0; i < count; i++)
            {
                var item = new GameObject("Item-" + i, typeof(RectTransform)).GetComponent<RectTransform>();
                item.SetParent(strip, false); Centre(item, new Vector2(left + i * spacing, -24), new Vector2(140, 140));
                var glow = Img("Glow", item, BattleFxArt1021.Dot, new Color(1f, .85f, .4f, .65f));
                if (BattleFxArt1021.Additive != null) glow.material = BattleFxArt1021.Additive;
                Centre(glow.rectTransform, Vector2.zero, new Vector2(150, 150));
                var icon = Img("Icon", item, RewardItemVisuals.SpriteFor(rewards[i].ItemId), Color.white);
                icon.preserveAspect = true; Centre(icon.rectTransform, new Vector2(0, 10), new Vector2(92, 92));
                if (icon.sprite == null) icon.color = RewardItemVisuals.AccentFor(rewards[i].ItemId);
                string caption = labels != null && i < labels.Count && !string.IsNullOrEmpty(labels[i]) ? labels[i] : "×" + rewards[i].Amount.ToString("N0");
                var amount = Label(item, font, caption, 30, Color.white);
                Centre(amount.rectTransform, new Vector2(0, -58), new Vector2(160, 40));
                items.Add(item);
            }
        }

        static float OutExpo(float t) { t = Mathf.Clamp01(t); return t >= 1f ? 1f : 1f - Mathf.Pow(2f, -10f * t); }
        static float Back(float t) { t = Mathf.Clamp01(t); const float c = 1.9f; float u = t - 1f; return 1f + (c + 1f) * u * u * u + c * u * u; }

        void Update()
        {
            clock += Mathf.Min(Time.unscaledDeltaTime, .05f);
            Pose();
            if (clock >= Life) Destroy(gameObject);
        }

        void Pose()
        {
            float t = clock;
            float exit = Mathf.Clamp01((t - (Life - .22f)) / .22f);
            strip.anchoredPosition = new Vector2(Mathf.Lerp(-1400f, 0f, OutExpo(t / .16f)) + 1500f * exit * exit, 60f);
            title.localScale = Vector3.one * (t < .14f ? 0f : Mathf.Lerp(1.6f, 1f, Mathf.Clamp01((t - .14f) / .06f)));
            for (int i = 0; i < items.Count; i++)
                items[i].localScale = Vector3.one * (t < .22f + i * .08f ? 0f : Back((t - .22f - i * .08f) / .2f));
            group.alpha = 1f - exit;
        }
    }
}
