using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ChoSiren
{
    /// <summary>
    /// Swipe the ready record out of its sleeve (2026-10-25, 少爷: "那个光碟要支持滑出去按照箭头把光碟滑出去"): an upward drag on a
    /// ready member card lifts the record with the finger along the flame's ^^ arrows; past <see cref="Threshold"/> (or a quick
    /// upward flick) it launches, otherwise it springs back. Sideways and downward drags are ignored, and a plain tap still clicks.
    /// </summary>
    public sealed class DiscSwipe1025 : MonoBehaviour, IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        /// <summary>Lift in canvas units that launches the record on release.</summary>
        public float Threshold = 110f;
        const float FlickSpeed = 700f, FlickMinLift = 35f, Overpull = .35f;

        public Func<bool> CanSwipe;
        public Action Began;
        /// <summary>Current lift in canvas units (≥ 0, rubber-banded past the threshold).</summary>
        public Action<float> Lift;
        public Action ReachedThreshold;
        public Action Launch;
        public Action Cancel;

        bool active, armed;
        float lift, speed, lastY, lastTime;
        Canvas canvas;

        float Scale
        {
            get
            {
                if (canvas == null) canvas = GetComponentInParent<Canvas>()?.rootCanvas;
                return canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
            }
        }

        public void OnInitializePotentialDrag(PointerEventData e) => e.useDragThreshold = true;

        public void OnBeginDrag(PointerEventData e)
        {
            Vector2 d = e.position - e.pressPosition;
            active = CanSwipe != null && CanSwipe() && d.y > Mathf.Abs(d.x) * .6f;
            if (!active) return;
            // A swipe is not a tap: the card's click (and its long-press timer) must not also fire.
            e.eligibleForClick = false;
            armed = false; lift = speed = 0f;
            lastY = e.position.y; lastTime = Time.unscaledTime;
            Began?.Invoke();
            OnDrag(e);
        }

        public void OnDrag(PointerEventData e)
        {
            if (!active) return;
            float raw = Mathf.Max(0f, (e.position.y - e.pressPosition.y) / Scale);
            lift = raw <= Threshold ? raw : Threshold + (raw - Threshold) * Overpull;
            float now = Time.unscaledTime, dt = now - lastTime;
            if (dt > .0001f) speed = Mathf.Lerp(speed, (e.position.y - lastY) / Scale / dt, .5f);
            lastY = e.position.y; lastTime = now;
            if (!armed && raw >= Threshold) { armed = true; ReachedThreshold?.Invoke(); }
            else if (armed && raw < Threshold) armed = false;
            Lift?.Invoke(lift);
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (!active) return;
            active = false;
            bool flick = speed > FlickSpeed && lift > FlickMinLift;
            if ((armed || flick) && CanSwipe != null && CanSwipe()) Launch?.Invoke();
            else Cancel?.Invoke();
        }

        void OnDisable()
        {
            if (!active) return;
            active = false;
            Cancel?.Invoke();
        }
    }
}
