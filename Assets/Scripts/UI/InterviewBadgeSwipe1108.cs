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
        IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler, IPointerUpHandler
    {
        const float Threshold = 95f, FlickDistance = 26f, FlickSpeed = 620f, ThrowDistance = 760f;
        const float ExitSeconds = .22f, EnterSeconds = .62f, HomeSeconds = .32f;

        /// <summary>Direction of the last throw; the next built badge enters from the stack with it.</summary>
        public static int PendingEntry;
        /// <summary>Batch-mode tests keep the ‹ › arrows instant unless a test opts into the one-badge animations.</summary>
        public static bool AnimateArrowsInBatch;

        RectTransform surface, front, back;
        CanvasGroup frontGroup;
        Vector2 frontHome, backHome;
        Quaternion frontRotation;
        Func<bool> allowed;
        Action<int> changed;
        ScrollRect parentScroll;
        bool dragging, passVertical;
        int throwSteps = 1;
        /// <summary>2026-11-08 (少爷: "点击下面两张直接切换或者上下左右"): maps a tap to how many candidates to skip (0 = ignore).</summary>
        public Func<PointerEventData, int> TapSteps;
        /// <summary>When set, a tapped back card is promoted by the owner's own animation instead of a throw.</summary>
        public Action<int> TapPromote;
        /// <summary>Double-tapping the front badge (2026-11-09: opens the candidate's detail page).</summary>
        public Action DoubleTapFront;
        float previousTime;
        // animation state
        enum Mode { Idle, Home, Exit, Enter, Retreat }
        Mode mode;
        float time, swing;
        int direction;

        // 2026-11-08 (少爷: "我把第一张划走那不应该是下面这张上来成第一位"): the back cards ride the drag. As the front badge
        // leaves, the first back card grows into the front badge's window and the second into the first's, so after the
        // throw the rebuilt page already shows them there (no entry animation).
        RectTransform card1, card2;
        Vector2 card1Home, card2Home, card1Target, card2Target;
        float card1Scale = 1f, card2Scale = 1f;
        const float PromoteReach = 300f;
        public bool StackPromotes => card1 != null;

        public void SetBackCards(RectTransform first, Vector2 firstTarget, float firstScale,
            RectTransform second, Vector2 secondTarget, float secondScale)
        {
            card1 = first; card2 = second;
            if (card1 != null) { card1Home = card1.anchoredPosition; card1Target = firstTarget; card1Scale = firstScale; }
            if (card2 != null) { card2Home = card2.anchoredPosition; card2Target = secondTarget; card2Scale = secondScale; }
        }

        /// <summary>Horizontal part of the badge's drag offset (canvas units).</summary>
        Vector2 frontBack, card1Back;
        float frontBackScale = 1f, card1BackScale = 1f;
        CanvasGroup card2Group;
        const float RetreatSeconds = .34f;

        /// <summary>Where the front badge and the first back card settle when ‹ steps back (window-matched places).</summary>
        public void SetRetreat(Vector2 frontTarget, float frontScale, Vector2 firstTarget, float firstScale)
        {
            frontBack = frontTarget; frontBackScale = frontScale; card1Back = firstTarget; card1BackScale = firstScale;
        }

        /// <summary>‹: the front badge goes back into the stack, then the previous candidate swings in from the left.</summary>
        public void Retreat()
        {
            if (Busy || allowed == null || !allowed() || card1 == null) return;
            mode = Mode.Retreat; time = 0f;
            if (card2 != null && card2Group == null)
            {
                card2Group = card2.GetComponent<CanvasGroup>();
                if (card2Group == null) card2Group = card2.gameObject.AddComponent<CanvasGroup>();
            }
        }

        public float Offset => drag.x;
        /// <summary>2026-11-08 (少爷: "工牌可以任意角度拖动滑走"): the full 2-D drag offset.</summary>
        public Vector2 Drag => drag;
        public bool Busy => dragging || mode != Mode.Idle;
        public bool IsDragging => dragging;

        Vector2 drag, dragStart, velocity, previousPoint, fromDrag, throwDir;

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

        /// <summary>Programmatic throw (direction +1 = next): the badge leaves sideways.</summary>
        public void Throw(int next)
        {
            if (next == 0 || Busy || allowed == null || !allowed()) return;
            throwSteps = Mathf.Max(1, Mathf.Abs(next));
            BeginExit(new Vector2(next > 0 ? -1f : 1f, 0f));
        }

        /// <summary>Tapping a back card of the stack brings it forward.</summary>
        public void OnPointerClick(PointerEventData e)
        {
            if (dragging || !e.eligibleForClick || TapSteps == null) return;
            int steps = TapSteps(e);
            if (steps == 0)
            {
                if (e.clickCount >= 2 && !Busy) DoubleTapFront?.Invoke();
                return;
            }
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
            // Only a page that scrolls keeps vertical gestures; otherwise the badge follows the finger at any angle.
            passVertical = parentScroll != null && Mathf.Abs(delta.y) > Mathf.Abs(delta.x) * .8f;
            if (passVertical) { parentScroll.OnBeginDrag(e); return; }
            if (mode == Mode.Exit || mode == Mode.Enter || allowed == null || !allowed()) return;
            dragging = true; mode = Mode.Idle; e.eligibleForClick = false;
            dragStart = drag; velocity = Vector2.zero;
            previousPoint = Local(e.position, e); previousTime = Time.unscaledTime;
            OnDrag(e);
        }

        public void OnDrag(PointerEventData e)
        {
            if (passVertical) { if (parentScroll != null) parentScroll.OnDrag(e); return; }
            if (!dragging) return;
            e.eligibleForClick = false;
            Vector2 point = Local(e.position, e);
            float now = Time.unscaledTime, dt = now - previousTime;
            if (dt > .001f) velocity = Vector2.Lerp(velocity, (point - previousPoint) / dt, .55f);
            previousPoint = point; previousTime = now;
            Vector2 raw = dragStart + point - Local(e.pressPosition, e);
            float extent = raw.magnitude;
            Vector2 soft = extent <= 220f ? raw : raw / extent * (220f + (extent - 220f) * .3f);
            Pose(soft, 0f, 1f);
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (passVertical) { if (parentScroll != null) parentScroll.OnEndDrag(e); passVertical = false; return; }
            if (!dragging) return;
            OnDrag(e); e.eligibleForClick = false;
            Release();
        }

        /// <summary>
        /// 2026-11-08 (少爷: "我划走了…bug了吗"): lifting the finger (pointer up) settles the drag too, and <see cref="Update"/>
        /// settles any drag whose finger is gone, so the badge is always either thrown or sprung home.
        /// </summary>
        public void OnPointerUp(PointerEventData e)
        {
            if (dragging) Release();
        }

        void Release()
        {
            if (!dragging) return;
            dragging = false;
            float extent = drag.magnitude;
            bool flick = velocity.magnitude > FlickSpeed && extent > FlickDistance && Vector2.Dot(velocity, drag) > 0f;
            if ((extent >= Threshold || flick) && (allowed?.Invoke() ?? false))
            {
                throwSteps = 1;
                // It flies off the way it was moving: the flick's direction, else the drag's.
                BeginExit(flick ? velocity.normalized : drag / Mathf.Max(.001f, extent));
            }
            else { mode = Mode.Home; time = 0f; fromDrag = drag; }
        }

        Vector2 Local(Vector2 screenPoint, PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(surface, screenPoint, e.pressEventCamera, out Vector2 p);
            return p;
        }

        void BeginExit(Vector2 dir)
        {
            mode = Mode.Exit; time = 0f; fromDrag = drag;
            throwDir = dir.sqrMagnitude > .0001f ? dir.normalized : Vector2.left;
            swing = throwDir.x >= 0f ? 1f : -1f;
            direction = StackPromotes ? 1 : throwDir.x < 0f ? 1 : -1; // with a promoting stack, any throw brings the next card up
        }

        void BeginEnter(int next)
        {
            mode = Mode.Enter; time = 0f; direction = next;
            swing = next > 0 ? 1f : -1f;
            Update();
        }

        static bool PointerHeld() => Input.touchCount > 0 || Input.GetMouseButton(0);

        void Update()
        {
            if (dragging && !PointerHeld() && Time.unscaledTime - previousTime > .25f) Release();
            if (!dragging && mode == Mode.Idle && drag.magnitude > .5f) { mode = Mode.Home; time = 0f; fromDrag = drag; }
            if (mode == Mode.Idle) return;
            time += Mathf.Min(Time.unscaledDeltaTime, .05f);
            switch (mode)
            {
                case Mode.Home:
                {
                    float t = Mathf.Clamp01(time / HomeSeconds);
                    float k = 1f - Mathf.Pow(1f - t, 3f);
                    float over = Mathf.Sin(t * Mathf.PI) * .12f * (1f - t);
                    Pose(Vector2.LerpUnclamped(fromDrag, Vector2.zero, k + over), 0f, 1f);
                    if (t >= 1f) { mode = Mode.Idle; Pose(Vector2.zero, 0f, 1f); }
                    break;
                }
                case Mode.Exit:
                {
                    float t = Mathf.Clamp01(time / ExitSeconds);
                    float k = t * t; // ease in: it leaves the hand and speeds up
                    Vector2 d = Vector2.Lerp(fromDrag, throwDir * ThrowDistance, k);
                    Pose(d, throwDir.x * 18f * k, 1f - Mathf.Clamp01((t - .45f) / .55f));
                    if (t >= 1f)
                    {
                        mode = Mode.Idle;
                        PendingEntry = StackPromotes && direction > 0 ? 0 : direction;
                        int steps = throwSteps; throwSteps = 1;
                        changed?.Invoke(direction * steps);
                    }
                    break;
                }
                case Mode.Retreat:
                {
                    float t = Mathf.Clamp01(time / RetreatSeconds);
                    float e = t * t * (3f - 2f * t);
                    float arc = Mathf.Sin(t * Mathf.PI);
                    if (front != null)
                    {
                        front.anchoredPosition = Vector2.LerpUnclamped(frontHome, frontBack, e) + new Vector2(-arc * 40f, 0f);
                        front.localScale = Vector3.one * Mathf.Lerp(1f, frontBackScale, e);
                        front.localRotation = frontRotation * Quaternion.Euler(0f, 0f, arc * 6f);
                    }
                    card1.anchoredPosition = Vector2.LerpUnclamped(card1Home, card1Back, e);
                    card1.localScale = Vector3.one * Mathf.Lerp(1f, card1BackScale, e);
                    if (card2Group != null) card2Group.alpha = 1f - e;
                    if (card2 != null) card2.anchoredPosition = card2Home + new Vector2(0f, -40f * e);
                    if (t >= 1f)
                    {
                        mode = Mode.Idle;
                        PendingEntry = -1;
                        changed?.Invoke(-1);
                    }
                    break;
                }
                case Mode.Enter:
                {
                    float t = Mathf.Clamp01(time / EnterSeconds);
                    float rise = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / .55f), 3f);
                    float pendulum = Mathf.Exp(-5.5f * t) * Mathf.Cos(t * 15f) * (1f - Mathf.Clamp01((t - .85f) / .15f));
                    float startX = swing > 0 ? 70f : -220f;
                    Vector2 d = new Vector2(Mathf.Lerp(startX, 0f, rise), Mathf.Lerp(-46f, 0f, rise));
                    PoseAt(d, -swing * 9f * pendulum, Mathf.Clamp01(t / .25f), Mathf.Lerp(.9f, 1f, rise), Mathf.Lerp(-28f, 0f, rise));
                    if (t >= 1f) { mode = Mode.Idle; Pose(Vector2.zero, 0f, 1f); }
                    break;
                }
            }
        }

        /// <summary>Badge drag offset on its lanyard: it follows the finger, tilts with the sideways part and lifts a little.</summary>
        void Pose(Vector2 d, float extraTilt, float alpha)
        {
            float extent = d.magnitude;
            Vector2 lifted = d + new Vector2(0f, -Mathf.Min(Mathf.Abs(d.x), 260f) * .07f);
            float tilt = Mathf.Clamp(-d.x * .045f, -13f, 13f) - extraTilt;
            float scale = 1f + Mathf.Min(extent, 160f) / 160f * .03f;
            PoseAt(lifted, tilt, alpha, scale, d.x * .16f, d);
        }

        void PoseAt(Vector2 position, float tilt, float alpha, float scale, float stackX, Vector2? dragOffset = null)
        {
            drag = dragOffset ?? (mode == Mode.Enter ? Vector2.zero : position);
            if (front != null)
            {
                front.anchoredPosition = frontHome + position;
                front.localRotation = frontRotation * Quaternion.Euler(0f, 0f, tilt);
                front.localScale = Vector3.one * scale;
            }
            if (frontGroup != null) frontGroup.alpha = alpha;
            if (card1 != null)
            {
                float p = Mathf.Clamp01(drag.magnitude / PromoteReach);
                float e = p * p * (3f - 2f * p);
                if (mode == Mode.Enter) e = 0f;
                card1.anchoredPosition = Vector2.LerpUnclamped(card1Home, card1Target, e);
                card1.localScale = Vector3.one * Mathf.LerpUnclamped(1f, card1Scale, e);
                if (card2 != null)
                {
                    card2.anchoredPosition = Vector2.LerpUnclamped(card2Home, card2Target, e);
                    card2.localScale = Vector3.one * Mathf.LerpUnclamped(1f, card2Scale, e);
                }
                return;
            }
            if (back != null) back.anchoredPosition = backHome + new Vector2(stackX, 0f);
        }

        void OnDisable()
        {
            dragging = passVertical = false;
            if (mode == Mode.Exit || mode == Mode.Retreat) PendingEntry = 0;
            mode = Mode.Idle;
            PoseAt(Vector2.zero, 0f, 1f, 1f, 0f, Vector2.zero);
        }
    }
}
