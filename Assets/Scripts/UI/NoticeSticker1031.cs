using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.UI
{
    /// <summary>
    /// 2026-10-31 (赵金): the one "reward waiting" notice, replacing the round red dot everywhere — a small torn
    /// piece of brand-teal paper with a torn white rim, hard ink shadow, halftone and a slanted "!"
    /// (Art/Lobby1031/notice-sticker-1031, from Tools/lobby1031/gen_lobby1031_art.py; refs/04 torn cyan band).
    /// It pops in when it appears, then breathes with a small periodic hop. Reduced motion keeps it still.
    /// The root is only a placement box (callers keep their old dot rect); the sticker is centred on it.
    /// </summary>
    public sealed class NoticeSticker1031 : MonoBehaviour
    {
        public const float DefaultSize = 36f;
        private static Sprite sprite;
        private static int created;
        private RectTransform face;
        private float phase, shownAt;

        public static Sprite StickerSprite => sprite != null ? sprite : sprite = Resources.Load<Sprite>("Art/Lobby1031/notice-sticker-1031");

        public static GameObject Create(string name, Transform parent, float size = DefaultSize)
        {
            var root = new GameObject(name, typeof(RectTransform));
            root.layer = parent != null ? parent.gameObject.layer : 5;
            root.transform.SetParent(parent, false);
            var art = new GameObject("Sticker", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            art.layer = root.layer;
            var rect = (RectTransform)art.transform;
            rect.SetParent(root.transform, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(size, size);
            var image = art.GetComponent<Image>();
            image.sprite = StickerSprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            if (image.sprite == null) image.color = Brand1025.Teal;
            var sticker = root.AddComponent<NoticeSticker1031>();
            sticker.face = rect;
            // Stickers hop out of step with each other.
            sticker.phase = Mathf.Repeat(++created * .93f, 2.6f);
            return root;
        }

        private void OnEnable() { shownAt = Time.unscaledTime; }

        private void OnDisable()
        {
            if (face == null) return;
            face.localScale = Vector3.one;
            face.localRotation = Quaternion.identity;
            face.anchoredPosition = Vector2.zero;
        }

        /// <summary>Pop-in scale for the first .32 s after appearing: 0.35 → 1.12 → 1.</summary>
        public static float PopIn(float t)
        {
            if (t >= 1f) return 1f;
            if (t <= 0f) return .35f;
            return t < .55f ? Mathf.Lerp(.35f, 1.12f, 1f - Mathf.Pow(1f - t / .55f, 3f))
                : Mathf.Lerp(1.12f, 1f, Mathf.SmoothStep(0f, 1f, (t - .55f) / .45f));
        }

        private void Update()
        {
            if (face == null) return;
            if (BattlePresentationOptions.ReduceMotion)
            {
                if (face.localScale != Vector3.one) OnDisable();
                return;
            }
            float now = Time.unscaledTime;
            float pop = PopIn((now - shownAt) / .32f);
            float t = now + phase;
            float breath = 1f + .045f * Mathf.Sin(t * Mathf.PI * 2f / 1.7f);
            // A short hop every 2.6 s: up, a little squash on landing, a tilt that settles.
            float hopT = Mathf.Repeat(t, 2.6f) / .42f;
            float hop = hopT < 1f ? Mathf.Sin(hopT * Mathf.PI) : 0f;
            float land = hopT >= 1f && hopT < 1.45f ? Mathf.Sin((hopT - 1f) / .45f * Mathf.PI) : 0f;
            float tilt = hopT < 1.6f ? Mathf.Sin(hopT * Mathf.PI * 1.25f) * 7f * (1f - hopT / 1.6f) : 0f;
            face.localScale = new Vector3(breath * pop * (1f + land * .08f), breath * pop * (1f - land * .1f), 1f);
            face.anchoredPosition = new Vector2(0f, hop * 3.2f);
            face.localRotation = Quaternion.Euler(0f, 0f, tilt);
        }
    }
}
