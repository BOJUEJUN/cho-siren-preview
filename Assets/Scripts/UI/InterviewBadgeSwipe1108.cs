using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ChoSiren.UI
{
    /// <summary>
    /// 2026-11-08 (少爷: "可以拖动滑动工牌，有丝滑好看的工牌切换动画"): the 面试 ID badge hangs from its clip.
    /// Dragging swings it on the lanyard (it follows the finger, tilts and lifts while the cards behind drift a
    /// little); letting go past the threshold (or flicking) throws it off the stack, then the next badge rises out of
    /// the stack and swings to rest on a damped pendulum. Short drags spring home. The ‹ › arrows change at once and
    /// play the same entry.
    /// </summary>
    public sealed class InterviewBadgeSwipe1108 : MonoBehaviour, IInitializePotentialDragHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        const float Threshold = 95f, FlickDistance = 26f, FlickSpeed = 620f, ThrowDistance = 760f;
        const float ExitSeconds = .22f, EnterSeconds = .62f, HomeSeconds = .32f;

        /// <summary>Direction of the last throw; the next built badge enters from the stack with it.</summary>
        public static int PendingEntry;

        RectTransform surface, front, back;
        CanvasGroup frontGroup;
        Vector2 frontHome, backHome;
        Quaternion frontRotation;
        Func<bool> allowed;
        Action<int> changed;
        ScrollRect parentScroll;
        bool dragging, passVertical, verticalSwipe;
        int throwSteps = 1;
        /// <summary>2026-11-08 (少爷: "点击下面两张直接切换或者上下左右"): maps a tap to how many candidates to skip (0 = ignore).</summary>
        public Func<PointerEventData, int> TapSteps;
        /// <summary>When set, a tapped back card is promoted by the owner's own animation instead of a throw.</summary>
        public Action<int> TapPromote;
        float dragStartOffset, speed, previousX, previousTime;
        // animation state
        enum Mode { Idle, Home, Exit, Enter }
        Mode mode;
        float time, from, swing;
        int direction;

        public float Offset { get; private set; }
        public bool Busy => dragging || mode != Mode.Idle;
        public bool IsDragging => dragging;

        public void Configure(RectTransform badge, RectTransform stack, Func<bool> canSwipe, Action<int> onChanged)
        {
            surface = (RectTransform)transform; front = badge; back = stack; allowed = canSwipe; changed = onChanged;
            frontHome = front.anchoredPosition; frontRotation = front.localRotation;
            backHome = back != null ? back.anchoredPosition : Vector2.zero;
            frontGroup = front.GetComponent<CanvasGroup>();
            if (frontGroup == null) frontGroup = front.gameObject.AddComponent<CanvasGroup>();
            parentScroll = transform.parent != null ? transform.parent.GetComponentInParent<ScrollRect>() : null;
            int entry = PendingEntry; PendingEntry = 0;
            if (entry != 0) BeginEnter(entry);
        }

        /// <summary>‹ › arrows: throw the badge the same way a swipe would (direction +1 = next).</summary>
        public void Throw(int next)
        {
            if (next == 0 || Busy || allowed == null || !allowed()) return;
            throwSteps = Mathf.Max(1, Mathf.Abs(next));
            BeginExit(next > 0 ? -1f : 1f);
        }

        /// <summary>Tapping a back card of the stack brings it forward (the front badge is thrown off first).</summary>
        public void OnPointerClick(PointerEventData e)
        {
            if (dragging || !e.eligibleForClick || TapSteps == null) return;
            int steps = TapSteps(e);
            if (steps == 0) return;
            if (TapPromote != null)
            {
                if (Busy || allowed == null || !allowed()) return;
                TapPromote(steps);
            }
            else Throw(steps);
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
            // With no page scroll to hand it to, an up / down swipe changes the candidate too (up = next).
            verticalSwipe = passVertical && parentScroll == null;
            if (verticalSwipe) { passVertical = false; e.eligibleForClick = false; return; }
            if (passVertical) { if (parentScroll != null) parentScroll.OnBeginDrag(e); return; }
            if (mode == Mode.Exit || mode == Mode.Enter || allowed == null || !allowed()) return;
            dragging = true; mode = Mode.Idle; e.eligibleForClick = false;
            dragStartOffset = Offset; speed = 0f;
            previousX = LocalX(e.position, e); previousTime = Time.unscaledTime;
            OnDrag(e);
        }

        public void OnDrag(PointerEventData e)
        {
            if (verticalSwipe) { e.eligibleForClick = false; return; }
            if (passVertical) { if (parentScroll != null) parentScroll.OnDrag(e); return; }
            if (!dragging) return;
            e.eligibleForClick = false;
            float x = LocalX(e.position, e), now = Time.unscaledTime, dt = now - previousTime;
            if (dt > .001f) speed = Mathf.Lerp(speed, (x - previousX) / dt, .55f);
            previousX = x; previousTime = now;
            float raw = dragStartOffset + x - LocalX(e.pressPosition, e);
            float extent = Mathf.Abs(raw);
            Pose(Mathf.Sign(raw) * (extent <= 220f ? extent : 220f + (extent - 220f) * .3f), 0f, 1f);
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (verticalSwipe)
            {
                verticalSwipe = false; e.eligibleForClick = false;
                float dy = LocalY(e.position, e) - LocalY(e.pressPosition, e);
                if (Mathf.Abs(dy) >= Threshold * .7f) Throw(dy > 0 ? 1 : -1);
                return;
            }
            if (passVertical) { if (parentScroll != null) parentScroll.OnEndDrag(e); passVertical = false; return; }
            if (!dragging) return;
            OnDrag(e); dragging = false; e.eligibleForClick = false;
            bool flick = Mathf.Abs(speed) > FlickSpeed && Mathf.Abs(Offset) > FlickDistance && Mathf.Sign(speed) == Mathf.Sign(Offset);
            if ((Mathf.Abs(Offset) >= Threshold || flick) && (allowed?.Invoke() ?? false)) { throwSteps = 1; BeginExit(Mathf.Sign(Offset)); }
            else { mode = Mode.Home; time = 0f; from = Offset; }
        }

        float LocalY(Vector2 screenPoint, PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(surface, screenPoint, e.pressEventCamera, out Vector2 p);
            return p.y;
        }

        float LocalX(Vector2 screenPoint, PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(surface, screenPoint, e.pressEventCamera, out Vector2 p);
            return p.x;
        }

        void BeginExit(float sign) { mode = Mode.Exit; time = 0f; from = Offset; direction = sign < 0 ? 1 : -1; swing = sign; }

        void BeginEnter(int next)
        {
            mode = Mode.Enter; time = 0f; direction = next;
            // Came from the left after a "previous" throw, from the stack (right/below) after "next".
            swing = next > 0 ? 1f : -1f;
            Update();
        }

        void Update()
        {
            if (mode == Mode.Idle) return;
            time += Mathf.Min(Time.unscaledDeltaTime, .05f);
            switch (mode)
            {
                case Mode.Home:
                {
                    float t = Mathf.Clamp01(time / HomeSeconds);
                    // Spring home with a small overshoot.
                    float k = 1f - Mathf.Pow(1f - t, 3f);
                    float over = Mathf.Sin(t * Mathf.PI) * .12f * (1f - t);
                    Pose(Mathf.LerpUnclamped(from, 0f, k + over), 0f, 1f);
                    if (t >= 1f) { mode = Mode.Idle; Pose(0f, 0f, 1f); }
                    break;
                }
                case Mode.Exit:
                {
                    float t = Mathf.Clamp01(time / ExitSeconds);
                    float k = t * t; // ease in: it leaves the hand and speeds up
                    float x = Mathf.Lerp(from, swing * ThrowDistance, k);
                    Pose(x, swing * 18f * k, 1f - Mathf.Clamp01((t - .45f) / .55f));
                    if (t >= 1f)
                    {
                        mode = Mode.Idle;
                        PendingEntry = direction;
                        int steps = throwSteps; throwSteps = 1;
                        changed?.Invoke(direction * steps);
                    }
                    break;
                }
                case Mode.Enter:
                {
                    float t = Mathf.Clamp01(time / EnterSeconds);
                    // Rises from the stack: starts behind (down-right, smaller, faint), lands with a damped swing.
                    float rise = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / .55f), 3f);
                    float pendulum = Mathf.Exp(-5.5f * t) * Mathf.Cos(t * 15f) * (1f - Mathf.Clamp01((t - .85f) / .15f));
                    float startX = swing > 0 ? 70f : -220f;
                    float x = Mathf.Lerp(startX, 0f, rise);
                    Vector2 lift = new Vector2(0f, Mathf.Lerp(-46f, 0f, rise));
                    float scale = Mathf.Lerp(.9f, 1f, rise);
                    PoseAt(x, lift, -swing * 9f * pendulum, Mathf.Clamp01(t / .25f), scale, Mathf.Lerp(-28f, 0f, rise));
                    if (t >= 1f) { mode = Mode.Idle; Pose(0f, 0f, 1f); }
                    break;
                }
            }
        }

        /// <summary>Badge offset x (canvas units) on its lanyard: tilt and lift follow the drag; the stack drifts a little.</summary>
        void Pose(float x, float extraTilt, float alpha)
        {
            float lift = -Mathf.Min(Mathf.Abs(x), 260f) * .07f;
            float tilt = Mathf.Clamp(-x * .045f, -13f, 13f) + extraTilt * -1f;
            float scale = 1f + Mathf.Min(Mathf.Abs(x), 160f) / 160f * .03f;
            PoseAt(x, new Vector2(0f, lift), tilt, alpha, scale, x * .16f);
        }

        void PoseAt(float x, Vector2 lift, float tilt, float alpha, float scale, float stackX)
        {
            Offset = x;
            if (front != null)
            {
                front.anchoredPosition = frontHome + new Vector2(x, 0f) + lift;
                front.localRotation = frontRotation * Quaternion.Euler(0f, 0f, tilt);
                front.localScale = Vector3.one * scale;
            }
            if (frontGroup != null) frontGroup.alpha = alpha;
            if (back != null) back.anchoredPosition = backHome + new Vector2(stackX, 0f);
        }

        void OnDisable()
        {
            dragging = passVertical = verticalSwipe = false;
            if (mode == Mode.Exit) PendingEntry = 0;
            mode = Mode.Idle;
            PoseAt(0f, Vector2.zero, 0f, 1f, 1f, 0f);
        }
    }
}
