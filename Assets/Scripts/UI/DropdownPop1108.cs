using UnityEngine;

namespace ChoSiren
{
    /// <summary>2026-11-08: a drop-down panel unfolds from its top edge (fade + vertical grow, slight overshoot).</summary>
    [DisallowMultipleComponent]
    public sealed class DropdownPop1108 : MonoBehaviour
    {
        const float Seconds = .18f;
        RectTransform rect;
        CanvasGroup group;
        float time;

        void Awake()
        {
            rect = (RectTransform)transform;
            Vector2 size = rect.sizeDelta;
            rect.anchoredPosition += Vector2.Scale(new Vector2(.5f, 1f) - rect.pivot, size);
            rect.pivot = new Vector2(.5f, 1f);
            group = GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();
            Apply(0f);
        }

        void Update()
        {
            if (time >= Seconds) return;
            time = Mathf.Min(Seconds, time + Time.unscaledDeltaTime);
            Apply(time / Seconds);
        }

        void Apply(float t)
        {
            float k = 1f - Mathf.Pow(1f - t, 3f);
            float over = Mathf.Sin(t * Mathf.PI) * .05f;
            rect.localScale = new Vector3(1f, Mathf.Lerp(.55f, 1f, k) + over, 1f);
            group.alpha = Mathf.Clamp01(t * 1.6f);
        }
    }
}
