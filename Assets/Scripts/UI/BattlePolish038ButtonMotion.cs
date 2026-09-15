using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>
    /// Whole-visual-group pointer motion for battle buttons: the authored frame, caption,
    /// glyphs and accent layer live under one container and that container is what eases.
    /// The Button's own RectTransform (the hit target) is never animated or resized.
    /// Mirrors the bottom-navigation feel — modest hover enlarge/lift, press compress,
    /// release spring, exit restores the captured baseline exactly.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class BattlePolish038ButtonMotion : MonoBehaviour, IPointerEnterHandler,
        IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        private const float HoverScale = 1.035f;
        private const float PressScale = 0.96f;
        private const float HoverLift = 2.5f;
        private const float PressDuration = .08f;
        private const float HoverDuration = .16f;
        private const float ExitDuration = .12f;

        private Button button;
        private RectTransform visualRoot;
        private Vector3 baseScale;
        private Vector2 basePosition;
        private bool configured;
        private bool hovered, pressed;
        private int state;
        private float elapsed, duration, fromScale, fromLift;
        private float scale = 1f, lift;
        private bool release;

        /// <summary>Captures the visual container's resting pose; only this subtree animates.</summary>
        public void Configure(RectTransform visualContainer)
        {
            button = GetComponent<Button>();
            visualRoot = visualContainer;
            if (visualRoot == null) return;
            baseScale = visualRoot.localScale;
            basePosition = visualRoot.anchoredPosition;
            configured = true;
            RestoreExact();
        }

        private void OnEnable() => ResetMotion();
        private void OnDisable() => ResetMotion();

        private void LateUpdate()
        {
            if (!configured || visualRoot == null) return;
            int target = (pressed && HoveredOrPressedUsable()) ? 2
                : (hovered && HoveredOrPressedUsable()) ? 1 : 0;
            if (state != target)
            {
                release = state == 2 && target == 1;
                state = target;
                elapsed = 0f;
                duration = target == 2 ? PressDuration : target == 0 ? ExitDuration : HoverDuration;
                fromScale = scale;
                fromLift = lift;
            }
            if (elapsed < duration)
            {
                elapsed = Mathf.Min(duration, elapsed + Time.unscaledDeltaTime);
                float t = duration <= 0f ? 1f : elapsed / duration;
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                float blend = release
                    ? 1f + 1.65f * Mathf.Pow(t - 1f, 3f) + .65f * Mathf.Pow(t - 1f, 2f)
                    : eased;
                float targetScale = target == 2 ? PressScale : target == 1 ? HoverScale : 1f;
                float targetLift = target == 1 ? HoverLift : 0f;
                scale = Mathf.LerpUnclamped(fromScale, targetScale, blend);
                lift = Mathf.LerpUnclamped(fromLift, targetLift, blend);
            }
            else if (state == 0)
            {
                RestoreExact();
                return;
            }
            Apply();
        }

        private bool HoveredOrPressedUsable() =>
            button != null && button.enabled && button.IsInteractable();

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!HoveredOrPressedUsable()) return;
            hovered = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            pressed = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && HoveredOrPressedUsable())
                pressed = true;
        }

        public void OnPointerUp(PointerEventData eventData) { pressed = false; }

        private void Apply()
        {
            visualRoot.localScale = new Vector3(baseScale.x * scale, baseScale.y * scale, baseScale.z);
            visualRoot.anchoredPosition = basePosition + Vector2.up * lift;
        }

        private void RestoreExact()
        {
            scale = 1f;
            lift = 0f;
            if (visualRoot == null) return;
            visualRoot.localScale = baseScale;
            visualRoot.anchoredPosition = basePosition;
        }

        private void ResetMotion()
        {
            hovered = pressed = false;
            release = false;
            state = 0;
            elapsed = duration = 0f;
            fromScale = 1f;
            fromLift = 0f;
            if (configured) RestoreExact();
        }
    }
}
