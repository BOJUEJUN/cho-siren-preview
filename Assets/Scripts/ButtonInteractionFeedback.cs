using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>Tactile feedback on separate artwork; the button's hit geometry is never animated.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class ButtonInteractionFeedback : MonoBehaviour, IPointerEnterHandler,
        IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        private Button button;
        private CanvasGroup effect;
        private LobbyHotspotAccentGraphic accent;
        private readonly TactileVisualMotion visuals = new TactileVisualMotion();
        private readonly TactileFeedbackTween motion = new TactileFeedbackTween();
        private bool hovered, pressed;
        private float sweep = 1f;

        private void Awake()
        {
            button = GetComponent<Button>();
            GameObject root = new GameObject("ButtonInteractionFx", typeof(RectTransform), typeof(CanvasGroup));
            root.layer = gameObject.layer;
            root.transform.SetParent(transform, false);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            effect = root.GetComponent<CanvasGroup>();
            effect.alpha = 0f; effect.blocksRaycasts = false; effect.interactable = false;
            accent = root.AddComponent<LobbyHotspotAccentGraphic>();
            accent.Configure(LobbyHotspotFeedback.VisualKind.Entry);
            visuals.Capture(transform);
        }

        private void OnEnable() => ResetFeedback();
        private void OnDisable() => ResetFeedback();
        private void OnDestroy()
        {
            visuals.Restore();
            if (effect != null) Destroy(effect.gameObject);
        }

        private void LateUpdate()
        {
            if (button == null || effect == null) return;
            if (!button.IsInteractable() || !button.enabled || GetComponent<LobbyHotspotFeedback>() != null)
            { ResetFeedback(); return; }
            motion.Update(pressed ? 2 : hovered ? 1 : 0, Time.unscaledDeltaTime);
            effect.alpha = motion.Alpha * .65f;
            visuals.Apply(motion.Scale, motion.Lift * .6f);
            if (hovered && sweep < 1f)
            { sweep = Mathf.Min(1f, sweep + Time.unscaledDeltaTime / .30f); accent.Phase = sweep; }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (button == null || !button.IsInteractable()) return;
            hovered = true; sweep = 0f;
            visuals.Capture(transform);
        }
        public void OnPointerExit(PointerEventData eventData) { hovered = false; pressed = false; }
        public void OnPointerDown(PointerEventData eventData)
        { if (eventData.button == PointerEventData.InputButton.Left && button != null && button.IsInteractable()) pressed = true; }
        public void OnPointerUp(PointerEventData eventData) { pressed = false; }

        private void ResetFeedback()
        {
            hovered = pressed = false; sweep = 1f; motion.Reset(); visuals.Restore();
            if (effect != null) effect.alpha = 0f;
            if (accent != null) accent.Phase = 1f;
        }
    }

    /// <summary>Explicit short transitions, with a very small spring only on release.</summary>
    internal sealed class TactileFeedbackTween
    {
        public float Alpha { get; private set; }
        public float Scale { get; private set; } = 1f;
        public float Lift { get; private set; }
        private int state;
        private float elapsed, duration, fromAlpha, fromScale = 1f, fromLift;
        private bool release;

        public void Update(int target, float dt)
        {
            if (state != target)
            {
                release = state == 2 && target == 1;
                state = target; elapsed = 0f;
                duration = target == 2 ? .08f : target == 0 ? .12f : .16f;
                fromAlpha = Alpha; fromScale = Scale; fromLift = Lift;
            }
            elapsed = Mathf.Min(duration, elapsed + dt);
            float t = duration <= 0 ? 1f : elapsed / duration;
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            float spring = release ? 1f + 1.65f * Mathf.Pow(t - 1f, 3f) + .65f * Mathf.Pow(t - 1f, 2f) : eased;
            Alpha = Mathf.Lerp(fromAlpha, state == 2 ? .92f : state == 1 ? .62f : 0f, eased);
            Scale = Mathf.LerpUnclamped(fromScale, state == 2 ? .97f : state == 1 ? 1.025f : 1f, spring);
            Lift = Mathf.LerpUnclamped(fromLift, state == 1 ? 2f : 0f, spring);
        }

        public void Reset() { state = 0; elapsed = duration = 0; Alpha = Lift = fromAlpha = fromLift = 0f; Scale = fromScale = 1f; release = false; }
    }

    /// <summary>Only image artwork moves; captions, underlines and input/layout objects stay fixed.</summary>
    internal sealed class TactileVisualMotion
    {
        private sealed class Part
        {
            public RectTransform Rect;
            public Vector3 Scale, LastScale;
            public Vector2 Position, LastPosition;
        }
        private readonly List<Part> parts = new List<Part>();

        public void Capture(Transform host)
        {
            Restore(); parts.Clear();
            foreach (Image image in host.GetComponentsInChildren<Image>(true))
            {
                string name = image.name;
                bool artwork = name.EndsWith("VisualV2") || name == "Icon" || name == "Portrait" || name == "ItemArt" || name.StartsWith("TeamCharacter-");
                if (!artwork || image.transform == host || image.sprite == null || image.raycastTarget || !image.isActiveAndEnabled) continue;
                if (image.GetComponentInParent<Button>()?.transform != host) continue;
                Add(image.rectTransform);
            }
        }

        private void Add(RectTransform rect)
        {
            if (rect == null) return;
            parts.Add(new Part { Rect = rect, Scale = rect.localScale, LastScale = rect.localScale,
                Position = rect.anchoredPosition, LastPosition = rect.anchoredPosition });
        }

        public void Apply(float scale, float lift)
        {
            foreach (Part part in parts)
            {
                if (part.Rect == null) continue;
                // An external responsive-layout change becomes the new baseline.
                if ((part.Rect.localScale - part.LastScale).sqrMagnitude > .000001f) part.Scale = part.Rect.localScale;
                if ((part.Rect.anchoredPosition - part.LastPosition).sqrMagnitude > .000001f) part.Position = part.Rect.anchoredPosition;
                part.LastScale = part.Scale * scale;
                Vector2 centreOffset = Vector2.Scale(part.Rect.rect.size, Vector2.one * .5f - part.Rect.pivot);
                Vector2 compensation = Vector2.Scale(centreOffset, new Vector2(part.Scale.x, part.Scale.y)) * (1f - scale);
                part.LastPosition = part.Position + compensation + Vector2.up * lift;
                part.Rect.localScale = part.LastScale; part.Rect.anchoredPosition = part.LastPosition;
            }
        }

        public void Restore()
        {
            foreach (Part part in parts)
            {
                if (part.Rect == null) continue;
                if ((part.Rect.localScale - part.LastScale).sqrMagnitude <= .000001f) part.Rect.localScale = part.Scale;
                if ((part.Rect.anchoredPosition - part.LastPosition).sqrMagnitude <= .000001f) part.Rect.anchoredPosition = part.Position;
                part.LastScale = part.Rect.localScale; part.LastPosition = part.Rect.anchoredPosition;
            }
        }
    }
}
