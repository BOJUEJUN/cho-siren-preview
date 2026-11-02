using System;
using System.Linq;
using ChoSiren.Panels;
using ChoSiren.Systems.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>
    /// 2026-11-01 (赵金 roster-ui): switch members without leaving the detail page — swipe the
    /// page sideways, tap the neon edge tabs, or press ←/→ on desktop. Order follows the member
    /// list's current filter/search/sort (owned only, the detail page is for signed members),
    /// wrapping at both ends. The selected tab (装备/技能/档案) is kept; every number is rebuilt.
    /// </summary>
    public sealed partial class ChoSirenApp
    {
        /// <summary>Signed members in the member list's current order; falls back to every signed member
        /// when the opened member is not part of the filtered list (opened from the team page, etc.).</summary>
        private int[] MemberDetailCycle1101(int current)
        {
            string[] roles = new[] { string.Empty }.Concat(MemberCareers.All).ToArray();
            string[] races = { string.Empty, "魅族", "魔族", "海灵族", "血精灵" };
            string role = roles[Mathf.Clamp(memberRoleFilterIndex, 0, roles.Length - 1)];
            string race = races[Mathf.Clamp(memberRaceFilterIndex, 0, races.Length - 1)];
            int count = GameModel.Members.Length;
            int[] listed = MemberRosterPagination.Build(count, 0, index =>
            {
                if (!model.IsUnlocked(index)) return false;
                MemberDefinition m = GameModel.Members[index];
                return MemberRosterVisibility.MatchesRosterFilter(true, m.Name, m.Career,
                    MemberRaceFamily(m, index), memberOwnedOnly, role, race, memberSearchQuery);
            }, Math.Max(1, count)).SourceIndices;
            if (Array.IndexOf(listed, current) >= 0) return listed;
            return Enumerable.Range(0, count).Where(model.IsUnlocked).ToArray();
        }

        /// <summary>Edge tabs, page dots and the swipe/keyboard handler. Built last so the tabs sit on top.</summary>
        private void BuildDetailSwitcher1101(GameObject overlay, RectTransform page, RectTransform figure,
            int memberIndex, float pageH)
        {
            int[] order = MemberDetailCycle1101(memberIndex);
            int at = Array.IndexOf(order, memberIndex);
            bool canSwitch = order.Length > 1 && at >= 0;
            MemberDetailSwipe1101 swipe = overlay.AddComponent<MemberDetailSwipe1101>();
            swipe.Bind(memberIndex, page, figure, canSwitch, (direction, offset) => SwitchMemberDetail1101(direction, offset));
            if (!canSwitch) return;

            // Vertical centre of the attribute panel: the right column's only spot where the
            // tab covers nothing but the panel's slanted border.
            const float midY = 695f;
            DetailEdgeTab1101(page, "DetailPrevious", -1, midY, () => SwitchMemberDetail1101(-1, 0f));
            DetailEdgeTab1101(page, "DetailNext", 1, midY, () => SwitchMemberDetail1101(1, 0f));
            DetailPager1101(page, order.Length, at, pageH);
        }

        /// <summary>Moves to the previous (-1) / next (+1) member. <paramref name="fromOffset"/> is the
        /// finger's drag distance so the outgoing art continues from where it was released.</summary>
        private void SwitchMemberDetail1101(int direction, float fromOffset)
        {
            MemberDetailSwipe1101 current = modalObject != null ? modalObject.GetComponent<MemberDetailSwipe1101>() : null;
            if (current == null) return;
            int[] order = MemberDetailCycle1101(current.MemberIndex);
            int at = Array.IndexOf(order, current.MemberIndex);
            if (order.Length <= 1 || at < 0) return;
            int target = order[(at + (direction < 0 ? -1 : 1) + order.Length) % order.Length];

            // Keep the outgoing art alive across the rebuild so it can slide away.
            RectTransform outgoing = current.Figure;
            if (outgoing != null)
            {
                outgoing.SetParent(safeRoot, false);
                outgoing.name = "MemberFullBodyOutgoing";
            }
            OpenMemberDetail(target, -1);
            MemberDetailSwipe1101 next = modalObject != null ? modalObject.GetComponent<MemberDetailSwipe1101>() : null;
            if (next == null || next.Figure == null)
            {
                if (outgoing != null) Destroy(outgoing.gameObject);
                return;
            }
            if (outgoing != null)
            {
                outgoing.SetParent(next.Figure.parent, false);
                outgoing.SetSiblingIndex(next.Figure.GetSiblingIndex());
            }
            modalObject.AddComponent<MemberDetailSlide1101>().Play(next.Figure, outgoing, direction, fromOffset);
            ModalPolish038Chrome.PlayClick();
        }

        private void DetailEdgeTab1101(RectTransform page, string name, int direction, float midY, UnityEngine.Events.UnityAction action)
        {
            const float hitW = 64f, hitH = 96f;
            bool left = direction < 0;
            GameObject hit = BoardButton038(name, page,
                new Rect(left ? 0f : DetailWidth - hitW, midY - hitH * .5f, hitW, hitH), action);
            foreach (var feedback in hit.GetComponents<LobbyHotspotFeedback>()) feedback.enabled = false;
            var visual = new GameObject("ArrowVisual", typeof(RectTransform));
            visual.transform.SetParent(hit.transform, false);
            PlaceTop(visual.GetComponent<RectTransform>(), 19, 26, 26, 44);
            var glyph = new GameObject("Chevron", typeof(RectTransform), typeof(CanvasRenderer), typeof(PsdHomeChevron));
            glyph.transform.SetParent(visual.transform, false);
            PlaceTop(glyph.GetComponent<RectTransform>(), 0, 0, 26, 44);
            var chevron = glyph.GetComponent<PsdHomeChevron>();
            chevron.Initialize(direction);
            var button = hit.GetComponent<Button>();
            button.targetGraphic = chevron;
            button.transition = Selectable.Transition.None;
            hit.AddComponent<PsdHomeChevronFeedback>().Configure(chevron, visual.GetComponent<RectTransform>());
            hit.transform.SetAsLastSibling();
        }

        private void DetailPager1101(RectTransform page, int count, int index, float pageH)
        {
            bool dots = count <= 12;
            const float dotStep = 17f;
            float dotsW = dots ? count * dotStep : 0f;
            string label = $"{index + 1}/{count}";
            float width = 26f + dotsW + 58f;
            GameObject chip = NewImage("DetailPager", page, PunkUi.SlantFill, new Color32(10, 6, 20, 214));
            PlaceTop(chip.GetComponent<RectTransform>(), 240f - width * .5f, pageH - 140f, width, 34f);
            chip.GetComponent<Image>().raycastTarget = false;
            Outline edge = chip.AddComponent<Outline>();
            edge.effectColor = new Color32(255, 46, 147, 150);
            edge.effectDistance = new Vector2(1.5f, -1.5f);
            Vector2[] diamond = { new Vector2(.5f, 0f), new Vector2(1f, .5f), new Vector2(.5f, 1f), new Vector2(0f, .5f) };
            for (int i = 0; dots && i < count; i++)
            {
                bool on = i == index;
                float size = on ? 15f : 8f;
                ConvexPoly1101 dot = Poly1101(chip.transform, "DetailPagerDot-" + i, diamond,
                    on ? (Color)Brand1025.Teal : new Color(1f, 1f, 1f, .42f));
                PlaceTop(dot.rectTransform, 18f + i * dotStep + (dotStep - size) * .5f, 17f - size * .5f, size, size);
            }
            Text text = BoardText038(chip.transform, "DetailPagerLabel", label, 18, White,
                22f + dotsW, 0f, 58f, 34f, TextAnchor.MiddleCenter);
            text.fontStyle = FontStyle.BoldAndItalic;
        }

        private static ConvexPoly1101 Poly1101(Transform parent, string name, Vector2[] points, Color color)
        {
            GameObject node = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(ConvexPoly1101));
            node.transform.SetParent(parent, false);
            ConvexPoly1101 poly = node.GetComponent<ConvexPoly1101>();
            poly.Points = points;
            poly.color = color;
            poly.raycastTarget = false;
            return poly;
        }
    }

    /// <summary>Filled convex polygon; points are normalised (0..1, y up) inside the rect.</summary>
    internal sealed class ConvexPoly1101 : MaskableGraphic
    {
        public Vector2[] Points;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (Points == null || Points.Length < 3) return;
            Rect r = rectTransform.rect;
            Vector2 centre = Vector2.zero;
            foreach (Vector2 p in Points) centre += p;
            centre /= Points.Length;
            vh.AddVert(new Vector2(r.xMin + centre.x * r.width, r.yMin + centre.y * r.height), color, Vector2.zero);
            foreach (Vector2 p in Points)
                vh.AddVert(new Vector2(r.xMin + p.x * r.width, r.yMin + p.y * r.height), color, Vector2.zero);
            for (int i = 0; i < Points.Length; i++)
                vh.AddTriangle(0, 1 + i, 1 + (i + 1) % Points.Length);
        }
    }

    /// <summary>Thick chevron (‹ / ›) built from two parallelograms; no font glyph dependency.</summary>
    internal sealed class Chevron1101 : MaskableGraphic
    {
        public bool PointsRight = true;
        public float Thickness = .46f;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            float t = Mathf.Clamp(Thickness, .1f, .9f);
            Quad(vh, r, new Vector2(0f, 1f), new Vector2(t, 1f), new Vector2(1f, .5f), new Vector2(1f - t, .5f));
            Quad(vh, r, new Vector2(1f - t, .5f), new Vector2(1f, .5f), new Vector2(t, 0f), new Vector2(0f, 0f));
        }

        private void Quad(VertexHelper vh, Rect r, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            int first = vh.currentVertCount;
            foreach (Vector2 p in new[] { a, b, c, d })
            {
                float x = PointsRight ? p.x : 1f - p.x;
                vh.AddVert(new Vector2(r.xMin + x * r.width, r.yMin + p.y * r.height), color, Vector2.zero);
            }
            vh.AddTriangle(first, first + 1, first + 2);
            vh.AddTriangle(first, first + 2, first + 3);
        }
    }

    /// <summary>Small periodic nudge toward the switch direction so the edge tabs read as interactive.</summary>
    internal sealed class DetailArrowNudge1101 : MonoBehaviour
    {
        private RectTransform target;
        private Vector2 origin;
        private float direction;

        internal void Bind(int towards)
        {
            target = (RectTransform)transform;
            origin = target.anchoredPosition;
            direction = towards < 0 ? -1f : 1f;
        }

        private void Update()
        {
            if (target == null) return;
            float wave = Mathf.Max(0f, Mathf.Sin(Time.unscaledTime * 2.4f));
            target.anchoredPosition = origin + new Vector2(direction * 4f * wave * wave * wave, 0f);
        }
    }

    /// <summary>
    /// Horizontal swipe on the member detail page. Lives on the page overlay, so a drag that starts on
    /// the art, the info column or a button (the button's click is cancelled) moves the full-body art
    /// with the finger; vertical drags and the tab panels' own scroll views are left alone.
    /// Past <see cref="CommitDistance"/> page units, or on a quick flick, it switches; otherwise it springs back.
    /// </summary>
    internal sealed class MemberDetailSwipe1101 : MonoBehaviour, IInitializePotentialDragHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        internal const float CommitDistance = 120f;
        private const float FlickSpeed = 900f, FlickMin = 40f, SpringTime = .22f;

        internal int MemberIndex { get; private set; }
        internal RectTransform Figure { get; private set; }
        internal float Offset => offset;

        private RectTransform page;
        private bool canSwitch, active;
        private float baseX, offset, speed, lastX, lastTime, springFrom, springT = -1f;
        private Vector2 pressLocal;
        private Action<int, float> commit;
        private Graphic art;

        internal void Bind(int memberIndex, RectTransform detailPage, RectTransform figure, bool switchable, Action<int, float> onCommit)
        {
            MemberIndex = memberIndex;
            page = detailPage;
            Figure = figure;
            canSwitch = switchable;
            commit = onCommit;
            art = figure != null ? figure.GetComponent<Graphic>() : null;
            baseX = figure != null ? figure.anchoredPosition.x : 0f;
        }

        public void OnInitializePotentialDrag(PointerEventData e) => e.useDragThreshold = true;

        public void OnBeginDrag(PointerEventData e)
        {
            Vector2 d = e.position - e.pressPosition;
            active = Figure != null && page != null && Mathf.Abs(d.x) > Mathf.Abs(d.y) * 1.1f;
            if (!active) return;
            e.eligibleForClick = false;
            GetComponent<MemberDetailSlide1101>()?.Finish();
            springT = -1f;
            speed = 0f;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(page, e.pressPosition, e.pressEventCamera, out pressLocal);
            lastX = pressLocal.x;
            lastTime = Time.unscaledTime;
            OnDrag(e);
        }

        public void OnDrag(PointerEventData e)
        {
            if (!active) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(page, e.position, e.pressEventCamera, out Vector2 local);
            float raw = local.x - pressLocal.x;
            float now = Time.unscaledTime, dt = now - lastTime;
            if (dt > .0001f) speed = Mathf.Lerp(speed, (local.x - lastX) / dt, .5f);
            lastX = local.x;
            lastTime = now;
            Apply(canSwitch ? raw : raw * .25f);
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (!active) return;
            active = false;
            bool flick = Mathf.Abs(speed) > FlickSpeed && Mathf.Abs(offset) > FlickMin && Mathf.Sign(speed) == Mathf.Sign(offset);
            if (canSwitch && (Mathf.Abs(offset) >= CommitDistance || flick))
            {
                // Finger moving left reveals the next member from the right.
                commit?.Invoke(offset < 0f ? 1 : -1, offset);
                return;
            }
            springFrom = offset;
            springT = 0f;
        }

        private void Apply(float value)
        {
            offset = value;
            if (Figure == null) return;
            Figure.anchoredPosition = new Vector2(baseX + value, Figure.anchoredPosition.y);
            if (art != null)
            {
                Color c = art.color;
                c.a = 1f - Mathf.Min(.45f, Mathf.Abs(value) / 700f);
                art.color = c;
            }
        }

        private void Update()
        {
            if (springT >= 0f)
            {
                springT += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(springT / SpringTime);
                float eased = 1f - Mathf.Pow(1f - k, 3f);
                Apply(Mathf.Lerp(springFrom, 0f, eased));
                if (k >= 1f) springT = -1f;
            }
            if (!canSwitch || active) return;
            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected != null && selected.GetComponent<InputField>() != null) return;
            if (Input.GetKeyDown(KeyCode.LeftArrow)) commit?.Invoke(-1, 0f);
            else if (Input.GetKeyDown(KeyCode.RightArrow)) commit?.Invoke(1, 0f);
        }

        private void OnDisable()
        {
            active = false;
            springT = -1f;
        }
    }

    /// <summary>Carousel hand-off: the old art leaves toward the swipe, the new art arrives from the other side.</summary>
    internal sealed class MemberDetailSlide1101 : MonoBehaviour
    {
        internal const float Duration = .32f;
        private RectTransform incoming, outgoing;
        private Graphic inArt, outArt;
        private float inBase, inFrom, outBase, outFrom, outTo, t;

        internal bool Running => incoming != null;

        internal void Play(RectTransform arriving, RectTransform leaving, int direction, float fromOffset)
        {
            float sign = direction < 0 ? -1f : 1f;
            incoming = arriving;
            outgoing = leaving;
            inArt = arriving.GetComponent<Graphic>();
            outArt = leaving != null ? leaving.GetComponent<Graphic>() : null;
            inBase = arriving.anchoredPosition.x;
            inFrom = sign * 300f;
            if (leaving != null)
            {
                // The outgoing art left its own page at the same base; fromOffset is already baked into it.
                outBase = leaving.anchoredPosition.x - fromOffset;
                outFrom = fromOffset;
                outTo = -sign * 460f;
            }
            t = 0f;
            Step(0f);
        }

        internal void Finish() => Step(Duration);

        private void Update() => Step(Time.unscaledDeltaTime);

        private void Step(float dt)
        {
            if (incoming == null) { Destroy(this); return; }
            t += dt;
            float k = Mathf.Clamp01(t / Duration);
            float eased = 1f - Mathf.Pow(1f - k, 3f);
            incoming.anchoredPosition = new Vector2(inBase + inFrom * (1f - eased), incoming.anchoredPosition.y);
            if (inArt != null) { Color c = inArt.color; c.a = Mathf.Lerp(.15f, 1f, eased); inArt.color = c; }
            if (outgoing != null)
            {
                outgoing.anchoredPosition = new Vector2(outBase + Mathf.Lerp(outFrom, outTo, eased), outgoing.anchoredPosition.y);
                if (outArt != null) { Color c = outArt.color; c.a = Mathf.Lerp(.85f, 0f, eased); outArt.color = c; }
            }
            if (k < 1f) return;
            if (outgoing != null) Destroy(outgoing.gameObject);
            incoming = null;
            outgoing = null;
            Destroy(this);
        }
    }
}
