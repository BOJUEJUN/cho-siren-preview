using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    [Serializable]
    internal sealed class AnimationTiming
    {
        public int[] durationsMs;
    }

    [RequireComponent(typeof(Image))]
    public sealed class SpriteSequencePlayer : MonoBehaviour
    {
        [SerializeField] private string resourcePath = "Art/HeroFrames";
        [SerializeField] private string timingPath = "Art/hero-animation";
        [SerializeField] private string framePrefix = "hero_";
        [SerializeField] private int fallbackFrameCount = 238;
        [SerializeField] private float fadeDuration = 0.38f;

        private static Sprite[] cachedFrames;
        private static int[] cachedDurations;
        private static string cachedResourcePath;
        private static string cachedTimingPath;

        private Image target;
        private Sprite[] frames = Array.Empty<Sprite>();
        private int[] durations = Array.Empty<int>();
        private int frameIndex;
        private float elapsed;
        private bool isReady;

        public event Action<float> WarmupProgress;
        private event Action ready;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeCache()
        {
            cachedFrames = null;
            cachedDurations = null;
            cachedResourcePath = null;
            cachedTimingPath = null;
        }

        private void Awake()
        {
            target = GetComponent<Image>();
            target.preserveAspect = true;
            target.useSpriteMesh = true;
            target.canvasRenderer.SetAlpha(0f);

            if (cachedDurations != null && cachedTimingPath == timingPath)
            {
                durations = cachedDurations;
            }
            else
            {
                TextAsset timingAsset = Resources.Load<TextAsset>(timingPath);
                if (timingAsset != null)
                {
                    AnimationTiming timing = JsonUtility.FromJson<AnimationTiming>(timingAsset.text);
                    durations = timing?.durationsMs ?? Array.Empty<int>();
                }
                cachedDurations = durations;
                cachedTimingPath = timingPath;
            }

            if (cachedFrames != null && cachedFrames.Length > 0 && cachedResourcePath == resourcePath)
            {
                ApplyFrames(cachedFrames);
                MarkReady();
            }
            else
            {
                StartCoroutine(WarmFrames());
            }
        }

        public void WhenReady(Action callback)
        {
            if (callback == null) return;
            if (isReady) callback();
            else ready += callback;
        }

        private IEnumerator WarmFrames()
        {
            int requestedCount = durations.Length > 0 ? durations.Length : Mathf.Max(1, fallbackFrameCount);
            Sprite[] loadedFrames = new Sprite[requestedCount];
            bool missingFrame = false;

            for (int index = 0; index < requestedCount; index++)
            {
                ResourceRequest request = Resources.LoadAsync<Sprite>(
                    $"{resourcePath}/{framePrefix}{index:000}");
                yield return request;

                Sprite sprite = request.asset as Sprite;
                loadedFrames[index] = sprite;
                if (sprite == null) missingFrame = true;
                WarmupProgress?.Invoke((index + 1f) / requestedCount);
            }

            if (!missingFrame)
            {
                cachedFrames = loadedFrames;
                cachedResourcePath = resourcePath;
                ApplyFrames(loadedFrames);
            }
            else
            {
                Debug.LogError($"Hero animation warm-up was incomplete at Resources/{resourcePath}; using fallback image.");
            }

            MarkReady();
        }

        private void ApplyFrames(Sprite[] loadedFrames)
        {
            frames = loadedFrames;
            frameIndex = 0;
            elapsed = 0f;
            if (frames.Length > 0) target.sprite = frames[0];
        }

        private void MarkReady()
        {
            isReady = true;
            target.CrossFadeAlpha(1f, Mathf.Max(0.01f, fadeDuration), true);
            Action callbacks = ready;
            ready = null;
            callbacks?.Invoke();
        }

        private void Update()
        {
            if (frames.Length <= 1 || !Application.isFocused) return;

            elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.25f);
            float frameDuration = durations.Length > frameIndex
                ? Mathf.Max(0.03f, durations[frameIndex] / 1000f)
                : 0.111f;

            bool frameChanged = false;
            while (elapsed >= frameDuration)
            {
                elapsed -= frameDuration;
                frameIndex = (frameIndex + 1) % frames.Length;
                frameChanged = true;
                frameDuration = durations.Length > frameIndex
                    ? Mathf.Max(0.03f, durations[frameIndex] / 1000f)
                    : 0.111f;
            }
            if (frameChanged) target.sprite = frames[frameIndex];
        }
    }

    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
    public sealed class UiEntranceMotion : MonoBehaviour
    {
        [SerializeField] private float delay = 0.08f;
        [SerializeField] private float duration = 0.42f;
        [SerializeField] private float verticalOffset = 12f;

        private RectTransform rectTransform;
        private CanvasGroup canvasGroup;
        private Vector2 restingPosition;
        private float startTime;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            canvasGroup = GetComponent<CanvasGroup>();
            restingPosition = rectTransform.anchoredPosition;
            PrepareHiddenState();
            enabled = false;
        }

        public void Play()
        {
            PrepareHiddenState();
            startTime = Time.unscaledTime + Mathf.Max(0f, delay);
            enabled = true;
        }

        private void PrepareHiddenState()
        {
            rectTransform.anchoredPosition = restingPosition + Vector2.down * verticalOffset;
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        private void Update()
        {
            float progress = Mathf.Clamp01((Time.unscaledTime - startTime) / Mathf.Max(0.01f, duration));
            float eased = progress * progress * (3f - 2f * progress);
            canvasGroup.alpha = eased;
            rectTransform.anchoredPosition = Vector2.LerpUnclamped(
                restingPosition + Vector2.down * verticalOffset, restingPosition, eased);

            if (progress < 1f) return;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            enabled = false;
        }
    }

    public sealed class BreathingMotion : MonoBehaviour
    {
        [SerializeField] private float verticalDistance = 3f;
        [SerializeField] private float scaleAmount = 0f;
        [SerializeField] private float speed = 0.75f;

        private RectTransform rectTransform;
        private Vector2 basePosition;
        private Vector3 baseScale;

        private void Awake()
        {
            rectTransform = transform as RectTransform;
            if (rectTransform == null) return;
            basePosition = rectTransform.anchoredPosition;
            baseScale = rectTransform.localScale;
        }

        private void Update()
        {
            if (rectTransform == null) return;
            if (!Application.isFocused) return;
            float wave = Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f * speed);
            float pixelAlignedOffset = Mathf.Round(wave * verticalDistance);
            rectTransform.anchoredPosition = basePosition + Vector2.up * pixelAlignedOffset;
            rectTransform.localScale = baseScale * (1f + wave * scaleAmount);
        }
    }

    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform panel;
        private Rect lastSafeArea;
        private Vector2Int lastScreen;

        private void Awake()
        {
            panel = GetComponent<RectTransform>();
            Apply();
        }

        private void Update()
        {
            if (lastSafeArea != Screen.safeArea || lastScreen.x != Screen.width || lastScreen.y != Screen.height)
                Apply();
        }

        private void Apply()
        {
            Rect safe = Screen.safeArea;
            lastSafeArea = safe;
            lastScreen = new Vector2Int(Screen.width, Screen.height);

            Vector2 min = safe.position;
            Vector2 max = safe.position + safe.size;
            min.x /= Mathf.Max(1, Screen.width);
            min.y /= Mathf.Max(1, Screen.height);
            max.x /= Mathf.Max(1, Screen.width);
            max.y /= Mathf.Max(1, Screen.height);
            panel.anchorMin = min;
            panel.anchorMax = max;
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
        }
    }
}
