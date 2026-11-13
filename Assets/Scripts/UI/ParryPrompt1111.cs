using System;
using ChoSiren.Systems.Tactics;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.UI
{
    /// <summary>
    /// 2026-11-11 弹反 (赵金: "加入弹反机制在关键 QTE 按准可以反弹伤害"). When a 第三章 enemy releases the special it
    /// charged, a pink ring closes on a teal one; tap the screen as they meet. Perfect blocks the whole special and throws
    /// it back, Good half, a miss or no tap nothing. Grading is the ultimate's <see cref="UltimateTimingChallenge"/>.
    /// </summary>
    public sealed class ParryPrompt1111 : MonoBehaviour
    {
        public const float TargetSize = 190f;
        public const float StartSize = 560f;
        private const float ResultSeconds = .55f;

        private RectTransform root, closing, target;
        private Image veil, closingRing, targetRing;
        private Text caption, result;
        private CanvasGroup group;
        private UltimateTimingChallenge challenge;
        private Func<bool> paused;
        private Action<UltimateTimingGrade> done;
        private float resultClock = -1f;
        private static Sprite ring;

        public bool IsShowing => root != null && root.gameObject.activeSelf;

        public static ParryPrompt1111 Create(Transform parent, Font font)
        {
            var go = new GameObject("ParryPrompt1111", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var prompt = go.AddComponent<ParryPrompt1111>();
            prompt.Build(font);
            return prompt;
        }

        private void Build(Font font)
        {
            root = (RectTransform)transform;
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            group = gameObject.AddComponent<CanvasGroup>();
            veil = NewImage("ParryVeil", root, null, new Color32(8, 4, 22, 120));
            Stretch(veil.rectTransform);
            veil.raycastTarget = true;
            var tap = veil.gameObject.AddComponent<Button>();
            tap.transition = Selectable.Transition.None;
            tap.onClick.AddListener(Tap);
            targetRing = NewImage("ParryTarget", root, Ring(), new Color32(62, 232, 220, 255));
            target = targetRing.rectTransform;
            Center(target, TargetSize);
            closingRing = NewImage("ParryClosing", root, Ring(), new Color32(255, 70, 196, 255));
            closing = closingRing.rectTransform;
            Center(closing, StartSize);
            caption = NewText("ParryCaption", font, 40, Color.white);
            caption.text = "弹反！看准时机点屏幕";
            Place(caption.rectTransform, 0, 230, 680, 64);
            result = NewText("ParryResult", font, 56, new Color32(62, 232, 220, 255));
            Place(result.rectTransform, 0, 0, 680, 90);
            foreach (Text t in new[] { caption, result })
            {
                var outline = t.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color32(6, 3, 18, 255); outline.effectDistance = new Vector2(3f, -3f);
            }
            root.gameObject.SetActive(false);
        }

        /// <summary>Starts the prompt; <paramref name="onGrade"/> gets the result once (after its short result flash).</summary>
        public void Play(Func<bool> isPaused, Action<UltimateTimingGrade> onGrade)
        {
            paused = isPaused; done = onGrade;
            challenge = new UltimateTimingChallenge();
            resultClock = -1f;
            result.text = string.Empty;
            caption.enabled = true; closingRing.enabled = true;
            group.alpha = 1f;
            transform.SetAsLastSibling();
            root.gameObject.SetActive(true);
            Pose();
        }

        public void Tap()
        {
            if (challenge == null || challenge.Resolved) return;
            challenge.Hit();
            ShowResult();
        }

        /// <summary>Test / skip path: resolve immediately with no tap.</summary>
        public void Cancel()
        {
            if (challenge == null) return;
            if (!challenge.Resolved) challenge.Cancel();
            Finish();
        }

        private void Update() => Advance(Mathf.Min(Time.unscaledDeltaTime, .1f));

        public void Advance(float seconds)
        {
            if (challenge == null || (paused?.Invoke() ?? false)) return;
            if (resultClock >= 0f)
            {
                resultClock += seconds;
                group.alpha = Mathf.Clamp01(1f - (resultClock - ResultSeconds * .6f) / (ResultSeconds * .4f));
                if (resultClock >= ResultSeconds) Finish();
                return;
            }
            challenge.Advance(seconds);
            Pose();
            if (challenge.Resolved) ShowResult();
        }

        private void Pose()
        {
            // The closing ring meets the target exactly at the perfect moment (progress = Target).
            float p = challenge.Progress / UltimateTimingChallenge.Target;
            float size = Mathf.LerpUnclamped(StartSize, TargetSize, p);
            closing.sizeDelta = Vector2.one * Mathf.Max(20f, size);
            float near = Mathf.Abs(challenge.Progress - UltimateTimingChallenge.Target);
            targetRing.color = near <= UltimateTimingChallenge.GoodHalfWidth
                ? new Color32(255, 255, 255, 255) : new Color32(62, 232, 220, 255);
        }

        private void ShowResult()
        {
            if (resultClock >= 0f) return;
            resultClock = 0f;
            closingRing.enabled = false; caption.enabled = false;
            switch (challenge.Grade)
            {
                case UltimateTimingGrade.Perfect: result.text = "完美弹反！"; result.color = new Color32(62, 232, 220, 255); break;
                case UltimateTimingGrade.Good: result.text = "弹反！"; result.color = new Color32(255, 214, 92, 255); break;
                default: result.text = "没接住"; result.color = new Color32(255, 110, 140, 255); break;
            }
        }

        private void Finish()
        {
            if (challenge == null) return;
            UltimateTimingGrade grade = challenge.Grade;
            challenge = null;
            root.gameObject.SetActive(false);
            Action<UltimateTimingGrade> callback = done;
            done = null;
            callback?.Invoke(grade);
        }

        // ------------------------------------------------------------ tiny UI helpers (no PanelKit dependency)
        private static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite; image.color = color; image.raycastTarget = false;
            return image;
        }

        private Text NewText(string name, Font font, int size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(root, false);
            var text = go.GetComponent<Text>();
            text.font = font; text.fontSize = size; text.color = color; text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static void Center(RectTransform rect, float size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .55f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.one * size;
        }

        private static void Place(RectTransform rect, float x, float y, float w, float h)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .55f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(w, h);
        }

        private static Sprite Ring()
        {
            if (ring != null) return ring;
            const int n = 256;
            var texture = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "ParryRing1111", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[n * n];
            float c = (n - 1) / 2f, outer = n / 2f - 2f, inner = outer - 14f;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                float a = Mathf.Clamp01(outer - d + .5f) * Mathf.Clamp01(d - inner + .5f);
                pixels[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            ring = Sprite.Create(texture, new Rect(0, 0, n, n), Vector2.one * .5f, 100f);
            return ring;
        }
    }
}
