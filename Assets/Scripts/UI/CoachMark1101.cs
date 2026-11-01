using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ChoSiren.UI
{
    /// <summary>
    /// 2026-11-01 (用户: "刚刚进游戏的新手介绍文字太多了，用最少的文字方案"): one coaching beat = a lit hole over the thing
    /// to press, a tapping hand pointing at it and ONE short line. The hole lets taps through, so the player learns by
    /// doing it; the step ends by itself once <c>done</c> reports true. Tapping the dim closes it too (never a dead end).
    /// </summary>
    public sealed class CoachMark1101 : MonoBehaviour
    {
        public const string HandResource = "UI/Onboarding1101/coach-hand";
        /// <summary>The fingertip inside coach-hand.png (normalised, from the bottom-left).</summary>
        static readonly Vector2 HandTip = new Vector2(.47f, .875f);
        const float Pad = 12f, HandSize = 136f;

        RectTransform root, frame, caption;
        Image[] dims;
        RectTransform[] targets;
        RectTransform pointAt;
        Func<bool> done;
        Action<bool> closed;
        CoachHand1101 hand;
        float clock;
        bool closing;

        public string Line { get; private set; }
        public bool IsOpen => !closing;
        /// <summary>The lit area in the coach's own space (tests and QA).</summary>
        public Rect Hole { get; private set; }

        /// <param name="host">Full-screen parent (battle panel / lobby safe root).</param>
        /// <param name="targets">Lit together; taps on them go through.</param>
        /// <param name="pointAt">What the hand taps (default: the middle of all targets).</param>
        /// <param name="done">Polled every frame; true ends the step as "done by doing".</param>
        /// <param name="onClosed">true = done by doing it, false = dismissed by tapping the dim.</param>
        public static CoachMark1101 Show(RectTransform host, Font font, string line, RectTransform[] targets,
            RectTransform pointAt, Func<bool> done, Action<bool> onClosed)
        {
            var go = new GameObject("CoachMark1101", typeof(RectTransform));
            go.transform.SetParent(host, false);
            go.transform.SetAsLastSibling();
            var coach = go.AddComponent<CoachMark1101>();
            coach.Line = line;
            coach.targets = targets ?? new RectTransform[0];
            coach.pointAt = pointAt;
            coach.done = done;
            coach.closed = onClosed;
            coach.Build(font);
            coach.Layout();
            return coach;
        }

        public void Close(bool byAction)
        {
            if (closing) return;
            closing = true;
            Action<bool> callback = closed; closed = null;
            Destroy(gameObject);
            try { callback?.Invoke(byAction); } catch (Exception e) { Debug.LogException(e); }
        }

        void Build(Font font)
        {
            root = (RectTransform)transform;
            Stretch(root);
            dims = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                // The project renders in linear space: .87 alpha reads as roughly a 60% dim to the eye.
                dims[i] = NewImage("Dim-" + i, root, new Color(.02f, .01f, .06f, .87f));
                dims[i].raycastTarget = true;
                dims[i].gameObject.AddComponent<CoachDimTap1101>().Owner = this;
            }
            // A teal frame (white inner hairline) that breathes around the hole.
            frame = NewRect("Frame", root);
            for (int i = 0; i < 8; i++)
            {
                bool inner = i >= 4;
                int side = i % 4;
                Image edge = NewImage("Edge-" + i, frame, inner ? new Color(1f, 1f, 1f, .85f) : (Color)Brand1025.Teal);
                RectTransform r = edge.rectTransform;
                bool horizontal = side < 2;
                float thick = inner ? 1.5f : 4f, inset = inner ? 5f : 0f;
                r.anchorMin = horizontal ? new Vector2(0, side == 0 ? 1 : 0) : new Vector2(side == 2 ? 0 : 1, 0);
                r.anchorMax = horizontal ? new Vector2(1, side == 0 ? 1 : 0) : new Vector2(side == 2 ? 0 : 1, 1);
                r.sizeDelta = horizontal ? new Vector2(-inset * 2f, thick) : new Vector2(thick, -inset * 2f);
                r.anchoredPosition = horizontal ? new Vector2(0, side == 0 ? -inset : inset) : new Vector2(side == 2 ? inset : -inset, 0);
            }
            hand = CoachHand1101.Create(root, HandSize);
            caption = Caption(root, font, Line);
        }

        /// <summary>One line on a P5 plate: pink offset shadow, ink face, a teal tick on the left. Never takes taps.</summary>
        public static RectTransform Caption(Transform parent, Font font, string line, int fontSize = 32)
        {
            RectTransform plate = NewRect("Caption", parent);
            var shadow = NewPlate("Shadow", plate, new Color32(255, 46, 147, 255));
            shadow.rectTransform.offsetMin = new Vector2(8, -8); shadow.rectTransform.offsetMax = new Vector2(8, -8);
            NewPlate("Plate", plate, new Color32(12, 6, 22, 255));
            Image tick = NewImage("Tick", plate, Brand1025.Teal);
            tick.rectTransform.anchorMin = new Vector2(0, .5f); tick.rectTransform.anchorMax = new Vector2(0, .5f);
            tick.rectTransform.sizeDelta = new Vector2(9, fontSize * 1.25f); tick.rectTransform.anchoredPosition = new Vector2(26, 0);
            tick.rectTransform.localEulerAngles = new Vector3(0, 0, -10f);
            var text = new GameObject("Line", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)).GetComponent<Text>();
            text.transform.SetParent(plate, false);
            text.font = font; text.fontSize = fontSize; text.fontStyle = FontStyle.Bold; text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false; text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow; text.verticalOverflow = VerticalWrapMode.Overflow;
            text.text = line;
            Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(30, 0);
            float width = Mathf.Clamp(text.preferredWidth + 96f, 240f, 640f);
            plate.anchorMin = plate.anchorMax = new Vector2(.5f, .5f);
            plate.sizeDelta = new Vector2(width, fontSize * 2.45f);
            plate.localEulerAngles = new Vector3(0, 0, 2f);
            return plate;
        }

        /// <summary>Re-measures the targets every frame: the reroll bar slides and cards bob.</summary>
        void Layout()
        {
            Rect area = root.rect;
            bool any = Bounds(targets, out Vector2 min, out Vector2 max);
            if (!any) { min = max = area.center; }
            else { min -= Vector2.one * Pad; max += Vector2.one * Pad; }
            min = Vector2.Max(min, area.min); max = Vector2.Min(max, area.max);
            Hole = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            Piece(dims[0], area.xMin, max.y, area.xMax, area.yMax);
            Piece(dims[1], area.xMin, area.yMin, area.xMax, min.y);
            Piece(dims[2], area.xMin, min.y, min.x, max.y);
            Piece(dims[3], max.x, min.y, area.xMax, max.y);
            frame.gameObject.SetActive(any);
            frame.anchorMin = frame.anchorMax = new Vector2(.5f, .5f);
            frame.pivot = new Vector2(.5f, .5f);
            frame.anchoredPosition = Hole.center - area.center;
            frame.sizeDelta = Hole.size;

            // The hand taps the chosen target (its on-screen part); it comes from the roomier side (below for the top
            // half, above otherwise).
            Vector2 tip = Hole.center;
            if (pointAt != null && pointAt.gameObject.activeInHierarchy && Bounds(new[] { pointAt }, out Vector2 pMin, out Vector2 pMax))
                tip = (Vector2.Max(pMin, Hole.min) + Vector2.Min(pMax, Hole.max)) * .5f;
            bool fromBelow = Hole.center.y > area.center.y;
            float reach = Mathf.Min(Hole.height * .25f, 40f);
            tip += new Vector2(0, fromBelow ? -reach : reach);
            hand.Aim(tip - area.center, fromBelow);

            // The line sits just past the hand, on the same side, inside the screen.
            float halfW = caption.sizeDelta.x * .5f, halfH = caption.sizeDelta.y * .5f;
            float y = fromBelow ? Mathf.Min(Hole.yMin, tip.y - HandSize) - 22f - halfH
                : Mathf.Max(Hole.yMax, tip.y + HandSize) + 22f + halfH;
            if (y - halfH < area.yMin + 24f || y + halfH > area.yMax - 120f)
                y = fromBelow ? Hole.yMax + 30f + halfH : Hole.yMin - 30f - halfH; // no room: the other side
            y = Mathf.Clamp(y, area.yMin + halfH + 24f, area.yMax - halfH - 120f);
            float x = Mathf.Clamp(tip.x, area.xMin + halfW + 18f, area.xMax - halfW - 18f);
            caption.anchorMin = caption.anchorMax = new Vector2(.5f, .5f);
            caption.anchoredPosition = new Vector2(x, y) - area.center;
        }

        bool Bounds(RectTransform[] list, out Vector2 min, out Vector2 max)
        {
            min = max = Vector2.zero;
            bool any = false;
            var corners = new Vector3[4];
            if (list == null) return false;
            foreach (RectTransform target in list)
            {
                if (target == null || !target.gameObject.activeInHierarchy) continue;
                target.GetWorldCorners(corners);
                foreach (Vector3 corner in corners)
                {
                    Vector2 p = root.InverseTransformPoint(corner);
                    if (!any) { min = max = p; any = true; }
                    else { min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
                }
            }
            return any;
        }

        internal void DimTapped()
        {
            if (clock < .35f) return; // the tap that opened the step must not close it
            Close(false);
        }

        void LateUpdate()
        {
            if (closing) return;
            clock += Time.unscaledDeltaTime;
            transform.SetAsLastSibling();
            Layout();
            float pulse = BattlePresentationOptions.ReduceMotion ? 1f : 1f + .02f * Mathf.Sin(clock * 6f);
            frame.localScale = new Vector3(pulse, pulse, 1f);
            float enter = Mathf.Clamp01(clock / .18f);
            caption.localScale = Vector3.one * Mathf.Lerp(1.25f, 1f, 1f - (1f - enter) * (1f - enter));
            if (done != null && done()) Close(true);
        }

        void Piece(Image image, float x0, float y0, float x1, float y1)
        {
            bool show = x1 > x0 && y1 > y0;
            image.enabled = show;
            if (!show) return;
            RectTransform r = image.rectTransform;
            Rect area = root.rect;
            r.anchorMin = r.anchorMax = new Vector2(.5f, .5f);
            r.pivot = Vector2.zero;
            r.anchoredPosition = new Vector2(x0, y0) - area.center;
            r.sizeDelta = new Vector2(x1 - x0, y1 - y0);
        }

        static SkewedPlate1021 NewPlate(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(SkewedPlate1021));
            go.transform.SetParent(parent, false);
            var plate = go.GetComponent<SkewedPlate1021>();
            plate.color = color; plate.raycastTarget = false; plate.SkewDegrees = -12f;
            Stretch(plate.rectTransform);
            return plate;
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static Image NewImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = false;
            return image;
        }

        static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }

        internal static Sprite HandSprite()
        {
            if (handSprite != null) return handSprite;
            Texture2D texture = Resources.Load<Texture2D>(HandResource);
            if (texture == null) return null;
            handSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), HandTip, 100f, 0, SpriteMeshType.FullRect);
            handSprite.hideFlags = HideFlags.DontSave;
            return handSprite;
        }
        static Sprite handSprite;

        internal static Vector2 TipPivot => HandTip;
    }

    /// <summary>The dim around the hole: a tap there closes the step.</summary>
    internal sealed class CoachDimTap1101 : MonoBehaviour, IPointerClickHandler
    {
        public CoachMark1101 Owner;
        public void OnPointerClick(PointerEventData eventData) { if (Owner != null) Owner.DimTapped(); }
    }

    /// <summary>
    /// The tapping hand on its own (no dim, no text): it presses toward its tip once a second and a teal ring ripples
    /// out where it lands. Used inside the coach and as a silent pointer (the map's 「开始挑战」, a full member card).
    /// </summary>
    public sealed class CoachHand1101 : MonoBehaviour
    {
        RectTransform rect, ripple;
        Image rippleImage, handImage;
        Vector2 tip;
        bool fromBelow = true;
        float clock;
        static Sprite ringSprite;

        public static CoachHand1101 Create(Transform parent, float size)
        {
            var go = new GameObject("CoachHand1101", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var hand = go.AddComponent<CoachHand1101>();
            hand.rect = (RectTransform)go.transform;
            hand.rect.anchorMin = hand.rect.anchorMax = new Vector2(.5f, .5f);
            hand.rect.sizeDelta = Vector2.zero;
            hand.ripple = new GameObject("Ripple", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<RectTransform>();
            hand.ripple.SetParent(hand.rect, false);
            hand.ripple.sizeDelta = new Vector2(size * .8f, size * .8f);
            hand.rippleImage = hand.ripple.GetComponent<Image>();
            hand.rippleImage.sprite = RingSprite(); hand.rippleImage.raycastTarget = false;
            hand.rippleImage.color = new Color(0, 0, 0, 0);
            var art = new GameObject("Hand", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            art.transform.SetParent(hand.rect, false);
            art.sprite = CoachMark1101.HandSprite(); art.raycastTarget = false; art.preserveAspect = true;
            art.rectTransform.pivot = CoachMark1101.TipPivot;
            art.rectTransform.sizeDelta = new Vector2(size, size);
            art.enabled = art.sprite != null;
            hand.handImage = art;
            return hand;
        }

        /// <param name="localTip">Where the fingertip lands, in the parent's centred space.</param>
        /// <param name="pointUp">true: the hand sits below and points up; false: above, pointing down.</param>
        public void Aim(Vector2 localTip, bool pointUp)
        {
            tip = localTip; fromBelow = pointUp;
            rect.anchoredPosition = tip;
            handImage.rectTransform.localEulerAngles = new Vector3(0, 0, pointUp ? 14f : 194f);
        }

        void LateUpdate()
        {
            clock += Time.unscaledDeltaTime;
            bool still = BattlePresentationOptions.ReduceMotion;
            float t = clock % 1.1f;
            // press in (0–.22 s), hold a beat, ease back; the ring blooms from the press.
            float press = still ? 0f : t < .22f ? t / .22f : t < .36f ? 1f : Mathf.Max(0f, 1f - (t - .36f) / .4f);
            float ease = press * press * (3f - 2f * press);
            float back = (1f - ease) * 26f;
            handImage.rectTransform.anchoredPosition = new Vector2(0, fromBelow ? -back : back);
            handImage.rectTransform.localScale = Vector3.one * (1f - .06f * ease);
            float since = still ? .3f : t - .22f;
            if (since >= 0f && since < .7f)
            {
                float k = since / .7f;
                ripple.localScale = Vector3.one * Mathf.Lerp(.35f, 1.35f, 1f - (1f - k) * (1f - k));
                rippleImage.color = new Color(.11f, .95f, .93f, (1f - k) * .9f);
            }
            else rippleImage.color = new Color(0, 0, 0, 0);
        }

        static Sprite RingSprite()
        {
            if (ringSprite != null) return ringSprite;
            const int n = 128;
            var texture = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            var pixels = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), Vector2.one * n * .5f);
                    float a = Mathf.Clamp01(1f - Mathf.Abs(d - n * .43f) / 5f);
                    pixels[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            texture.SetPixels32(pixels); texture.Apply(false, true);
            ringSprite = Sprite.Create(texture, new Rect(0, 0, n, n), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
            ringSprite.hideFlags = HideFlags.DontSave;
            return ringSprite;
        }
    }
}
