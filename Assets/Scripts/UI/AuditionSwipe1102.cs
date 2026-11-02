using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ChoSiren.UI
{
    /// <summary>Horizontal candidate browsing follows the pointer; short drags spring home.</summary>
    public sealed class AuditionSwipe1102 : MonoBehaviour, IInitializePotentialDragHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        const float Threshold = 90f, FlickDistance = 28f, FlickSpeed = 650f;
        RectTransform surface;
        RectTransform[] targets;
        Vector2[] origins;
        Quaternion[] rotations;
        Func<bool> allowed;
        Action<int> changed;
        ScrollRect parentScroll;
        bool dragging, passVertical, settling;
        float dragStartOffset, speed, previousX, previousTime;
        float settleFrom, settleTo, settleTime, settleDuration;
        int pendingDirection;
        public float Offset { get; private set; }
        public bool IsDragging => dragging;

        public void Configure(RectTransform[] moving, Func<bool> canSwipe, Action<int> onChanged)
        {
            surface = (RectTransform)transform; targets = moving; allowed = canSwipe; changed = onChanged;
            origins = new Vector2[targets.Length]; rotations = new Quaternion[targets.Length];
            for (int i = 0; i < targets.Length; i++)
            {
                origins[i] = targets[i].anchoredPosition; rotations[i] = targets[i].localRotation;
            }
            parentScroll = transform.parent != null ? transform.parent.GetComponentInParent<ScrollRect>() : null;
        }

        public void Enter(float from)
        {
            Apply(from); Settle(0f, .17f, 0);
        }

        public void OnInitializePotentialDrag(PointerEventData e)
        {
            e.useDragThreshold = true;
            if (parentScroll != null) parentScroll.OnInitializePotentialDrag(e);
        }
        public void OnBeginDrag(PointerEventData e)
        {
            Vector2 delta = e.position - e.pressPosition;
            passVertical = Mathf.Abs(delta.y) > Mathf.Abs(delta.x) * .8f;
            if (passVertical)
            {
                if (parentScroll != null) parentScroll.OnBeginDrag(e);
                return;
            }
            if (allowed == null || !allowed() || pendingDirection != 0) return;
            dragging = true; settling = false; e.eligibleForClick = false;
            dragStartOffset = Offset; speed = 0f;
            previousX = LocalX(e.position, e); previousTime = Time.unscaledTime;
            OnDrag(e);
        }
        public void OnDrag(PointerEventData e)
        {
            if (passVertical)
            {
                if (parentScroll != null) parentScroll.OnDrag(e);
                return;
            }
            if (!dragging) return;
            e.eligibleForClick = false;
            float x = LocalX(e.position, e), now = Time.unscaledTime;
            float dt = now - previousTime;
            if (dt > .001f) speed = Mathf.Lerp(speed, (x - previousX) / dt, .55f);
            previousX = x; previousTime = now;
            float raw = dragStartOffset + x - LocalX(e.pressPosition, e);
            float extent = Mathf.Abs(raw);
            Apply(Mathf.Sign(raw) * (extent <= 180f ? extent : 180f + (extent - 180f) * .22f));
        }
        public void OnEndDrag(PointerEventData e)
        {
            if (passVertical)
            {
                if (parentScroll != null) parentScroll.OnEndDrag(e);
                passVertical = false; return;
            }
            if (!dragging) return;
            OnDrag(e); dragging = false; e.eligibleForClick = false;
            bool flick = Mathf.Abs(speed) > FlickSpeed && Mathf.Abs(Offset) > FlickDistance
                && Mathf.Sign(speed) == Mathf.Sign(Offset);
            if ((Mathf.Abs(Offset) >= Threshold || flick) && (allowed?.Invoke() ?? false))
                Settle(Mathf.Sign(Offset) * Mathf.Max(210f, Mathf.Abs(Offset)), .1f, Offset < 0 ? 1 : -1);
            else Settle(0f, .16f, 0);
        }
        float LocalX(Vector2 screenPoint, PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(surface, screenPoint, e.pressEventCamera, out Vector2 p);
            return p.x;
        }
        void Settle(float to, float duration, int direction)
        {
            settling = true; settleFrom = Offset; settleTo = to; settleTime = 0f;
            settleDuration = duration; pendingDirection = direction;
        }
        void Update()
        {
            if (!settling) return;
            settleTime += Mathf.Min(Time.unscaledDeltaTime, .05f);
            float t = Mathf.Clamp01(settleTime / settleDuration);
            Apply(Mathf.Lerp(settleFrom, settleTo, 1f - Mathf.Pow(1f - t, 3f)));
            if (t < 1f) return;
            settling = false; int direction = pendingDirection; pendingDirection = 0;
            if (direction != 0 && (allowed?.Invoke() ?? false)) changed?.Invoke(direction);
            else if (direction != 0) Settle(0f, .16f, 0);
        }
        void Apply(float x)
        {
            Offset = x;
            if (targets == null) return;
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] == null) continue;
                targets[i].anchoredPosition = origins[i] + Vector2.right * x;
                targets[i].localRotation = rotations[i] * Quaternion.Euler(0f, 0f, Mathf.Clamp(-x * .006f, -1.2f, 1.2f));
            }
        }
        void OnDisable()
        {
            dragging = settling = passVertical = false; pendingDirection = 0; Apply(0f);
        }
    }
}
