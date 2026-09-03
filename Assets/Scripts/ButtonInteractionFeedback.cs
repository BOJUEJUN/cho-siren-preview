using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>
    /// Pointer feedback layered on top of a button's authored colour and base scale.
    /// Selection code can still change localScale; that value becomes the new base.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class ButtonInteractionFeedback : MonoBehaviour, IPointerEnterHandler,
        IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        private const float HoverMultiplier = 1.04f;
        private const float PressedMultiplier = 0.96f;
        private const float Response = 20f;

        private Button button;
        private RectTransform target;
        private Vector3 baseScale = Vector3.one;
        private Vector3 lastAppliedScale = Vector3.one;
        private float currentMultiplier = 1f;
        private bool hovered;
        private bool pressed;

        private void Awake()
        {
            button = GetComponent<Button>();
            target = transform as RectTransform;
            CaptureBaseScale();
            ConfigureHighlightTint();
        }

        private void OnEnable()
        {
            hovered = false;
            pressed = false;
            currentMultiplier = 1f;
            CaptureBaseScale();
        }

        private void LateUpdate()
        {
            if (target == null) return;

            if (!Approximately(target.localScale, lastAppliedScale))
                baseScale = target.localScale;

            if (button == null || !button.IsInteractable())
            {
                hovered = false;
                pressed = false;
            }

            float desiredMultiplier = pressed ? PressedMultiplier : hovered ? HoverMultiplier : 1f;
            float blend = 1f - Mathf.Exp(-Response * Time.unscaledDeltaTime);
            currentMultiplier = Mathf.Lerp(currentMultiplier, desiredMultiplier, blend);
            if (Mathf.Abs(currentMultiplier - desiredMultiplier) < 0.0005f)
                currentMultiplier = desiredMultiplier;

            lastAppliedScale = baseScale * currentMultiplier;
            target.localScale = lastAppliedScale;
        }

        private void OnDisable()
        {
            if (target != null) target.localScale = baseScale;
            lastAppliedScale = baseScale;
            currentMultiplier = 1f;
            hovered = false;
            pressed = false;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            hovered = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            pressed = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (button != null && button.IsInteractable()) pressed = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            pressed = false;
        }

        private void CaptureBaseScale()
        {
            if (target == null) return;
            baseScale = target.localScale;
            lastAppliedScale = baseScale;
        }

        private void ConfigureHighlightTint()
        {
            if (button == null || button.transition != Selectable.Transition.ColorTint) return;

            ColorBlock colors = button.colors;
            Color highlighted = colors.highlightedColor;
            highlighted.r = Mathf.Max(highlighted.r, 1.04f);
            highlighted.g = Mathf.Max(highlighted.g, 1.04f);
            highlighted.b = Mathf.Max(highlighted.b, 1.04f);
            colors.highlightedColor = highlighted;
            colors.fadeDuration = Mathf.Min(colors.fadeDuration, 0.08f);
            button.colors = colors;
        }

        private static bool Approximately(Vector3 left, Vector3 right)
        {
            return (left - right).sqrMagnitude < 0.000001f;
        }
    }

}
