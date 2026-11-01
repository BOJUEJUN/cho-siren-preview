using System;
using UnityEngine;

namespace ChoSiren.UI
{
    /// <summary>A separately clipped result ticker. Reduced motion keeps a short, readable caption.</summary>
    public sealed class BattleStickerMarquee : MonoBehaviour
    {
        private UnityEngine.UI.Text caption;
        private RectTransform viewport;
        private Func<bool> stopped;
        private string fullText, stillText;
        private float width, clock;
        private bool lastReduced;

        public void Configure(RectTransform view, UnityEngine.UI.Text text, Func<bool> isStopped)
        {
            viewport = view;
            caption = text;
            stopped = isStopped;
        }

        public void SetText(string scrollingText, string reducedText)
        {
            fullText = scrollingText;
            stillText = reducedText;
            clock = 0f;
            ApplyText();
        }

        private void ApplyText()
        {
            if (caption == null) return;
            lastReduced = BattlePresentationOptions.ReduceMotion;
            caption.text = lastReduced ? stillText : fullText;
            width = Mathf.Max(1f, caption.preferredWidth + 24f);
            caption.rectTransform.sizeDelta = new Vector2(width, viewport.rect.height);
            caption.rectTransform.anchoredPosition = new Vector2(12f, 0f);
        }

        private void OnEnable() { clock = 0f; if (caption != null) ApplyText(); }

        private void Update()
        {
            if (caption == null || viewport == null) return;
            if (lastReduced != BattlePresentationOptions.ReduceMotion) ApplyText();
            if (lastReduced || (stopped?.Invoke() ?? false) || width <= viewport.rect.width) return;
            clock += Time.unscaledDeltaTime;
            // Hold the opening text before it moves; no flicker or sudden full-screen transition.
            float travel = Mathf.Max(0f, clock - 1.5f) * 34f;
            float loop = width + viewport.rect.width + 32f;
            if (travel > loop) { clock = 1.5f; travel = 0f; }
            caption.rectTransform.anchoredPosition = new Vector2(12f - travel, 0f);
        }
    }
}
