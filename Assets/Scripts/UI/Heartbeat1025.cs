using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.UI
{
    /// <summary>
    /// 2026-10-25 (赵金): 应急守护 "要可以用时候可以像心跳一样跳动提醒 提醒玩家去点". While the guard is usable
    /// the badge beats lub-dub and sends out a ring on each beat; spent or unavailable it rests. The art is
    /// moved under a centred holder so the beat scales around its middle and never fights the button's own
    /// press motion (which scales the parent group).
    /// </summary>
    public sealed class Heartbeat1025 : MonoBehaviour
    {
        private const float Period = 1.15f;
        private RectTransform holder, ring;
        private Image ringImage;
        private Color ringColor;
        private float clock;
        private bool on;

        public bool Active
        {
            get => on;
            set { if (on == value) return; on = value; clock = 0f; if (!on) Rest(); }
        }

        /// <summary>Wraps <paramref name="art"/> (and any <paramref name="riders"/> that should beat with it).</summary>
        public static Heartbeat1025 Attach(RectTransform art, Color ringColor, params RectTransform[] riders)
        {
            if (art == null) return null;
            var holder = new GameObject("Heartbeat1025", typeof(RectTransform)).GetComponent<RectTransform>();
            holder.SetParent(art.parent, false);
            holder.SetSiblingIndex(art.GetSiblingIndex());
            holder.anchorMin = art.anchorMin; holder.anchorMax = art.anchorMax; holder.sizeDelta = art.sizeDelta;
            holder.pivot = new Vector2(.5f, .5f);
            holder.anchoredPosition = art.anchoredPosition + Vector2.Scale(new Vector2(.5f, .5f) - art.pivot, art.sizeDelta);
            var ringGo = new GameObject("BeatRing", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            ringGo.transform.SetParent(holder, false);
            var beat = holder.gameObject.AddComponent<Heartbeat1025>();
            beat.holder = holder; beat.ringColor = ringColor;
            beat.ringImage = ringGo.GetComponent<Image>(); beat.ringImage.sprite = RingSprite(); beat.ringImage.raycastTarget = false;
            beat.ring = beat.ringImage.rectTransform;
            float side = Mathf.Min(art.sizeDelta.x, art.sizeDelta.y) * .92f;
            beat.ring.anchorMin = beat.ring.anchorMax = beat.ring.pivot = new Vector2(.5f, .5f);
            beat.ring.sizeDelta = new Vector2(side, side); beat.ring.anchoredPosition = Vector2.zero;
            art.SetParent(holder, true);
            foreach (RectTransform rider in riders) if (rider != null) rider.SetParent(holder, true);
            beat.Rest();
            return beat;
        }

        private void Rest()
        {
            if (holder != null) holder.localScale = Vector3.one;
            if (ringImage != null) ringImage.color = Color.clear;
        }

        private static float Bump(float t, float at) { float d = (t - at) / .07f; return Mathf.Exp(-d * d); }

        private void Update()
        {
            if (!on || BattlePresentationOptions.ReduceMotion) { Rest(); return; }
            clock = (clock + Mathf.Min(Time.unscaledDeltaTime, .1f)) % Period;
            // lub (strong) … dub (softer) … rest
            float pulse = Bump(clock, .07f) + .65f * Bump(clock, .3f);
            holder.localScale = Vector3.one * (1f + .1f * pulse);
            float r = Mathf.Clamp01((clock - .04f) / .6f);
            ring.localScale = Vector3.one * Mathf.Lerp(1f, 1.55f, 1f - (1f - r) * (1f - r));
            Color c = ringColor; c.a *= clock < .04f ? 0f : 1f - r; ringImage.color = c;
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
                    float a = Mathf.Clamp01(1f - Mathf.Abs(d - .88f) / .08f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(pixels); tex.Apply();
            return ringSprite = Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * .5f, 100);
        }
    }
}
