using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>
    /// 0.3.8 modal control feedback: the visible body scales/brightens on hover, compresses on
    /// press and springs back on release, matching the footer interaction contract. Motion is
    /// applied to a child "Body" rect so the outer hit rectangle never moves; a sheen overlay
    /// provides the brighten pass. Reuses the shared <see cref="TactileFeedbackTween"/> timing
    /// so modal controls feel identical to the rest of the game.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class ModalPolish038PressFeedback : MonoBehaviour, IPointerEnterHandler,
        IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private float sheenStrength = .34f;

        private Button button;
        private RectTransform body;
        private CanvasGroup sheen;
        private Vector3 baseScale = Vector3.one;
        private bool configured;
        private bool hovered, pressed;
        private readonly TactileFeedbackTween motion = new TactileFeedbackTween();

        private void Awake()
        {
            button = GetComponent<Button>();
        }

        /// <summary>bodyRect is the scaled visual group; sheenGroup fades in on hover/press.</summary>
        public void Configure(RectTransform bodyRect, CanvasGroup sheenGroup, float strength = .34f)
        {
            if (button == null) button = GetComponent<Button>();
            body = bodyRect;
            sheen = sheenGroup;
            sheenStrength = Mathf.Clamp01(strength);
            if (body != null) baseScale = body.localScale;
            if (sheen != null) sheen.alpha = 0f;
            configured = true;
            ResetFeedback();
        }

        private void OnEnable()
        {
            if (configured) ResetFeedback();
        }

        private void OnDisable()
        {
            if (configured) ResetFeedback();
        }

        private void LateUpdate()
        {
            if (!configured || body == null) return;
            if (button == null || !button.enabled || !button.IsInteractable())
            {
                ResetFeedback();
                return;
            }

            motion.Update(pressed ? 2 : hovered ? 1 : 0, Time.unscaledDeltaTime);
            body.localScale = baseScale * motion.Scale;
            if (sheen != null) sheen.alpha = motion.Alpha * sheenStrength;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (button == null || !button.IsInteractable()) return;
            hovered = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            pressed = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left &&
                button != null && button.IsInteractable()) pressed = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            pressed = false;
        }

        private void ResetFeedback()
        {
            hovered = pressed = false;
            motion.Reset();
            if (body != null) body.localScale = baseScale;
            if (sheen != null) sheen.alpha = 0f;
        }
    }
}
