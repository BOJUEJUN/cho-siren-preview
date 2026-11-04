using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>
    /// 2026-10-31 (赵金, refs/02 Persona dialogue): the lobby idol's speech box. A slanted brand-teal name plate
    /// sits on the top-left corner of a large translucent deep-violet rounded box; a teal stroke runs from the
    /// plate round the box's left corner; the bottom-right indicator is a spinning CHO-SIREN record inside a
    /// broken teal ring. The line types out. Node names (PsdCharacterDialogue / Speaker / DialogueLine) and the
    /// CanvasGroup fade are unchanged, so the existing lines and triggers drive it as before.
    /// 2026-11-01 (赵金 refs/02 "这个对话框有点太挡了"): about a third smaller, more see-through, placed per idol
    /// clear of her face (FaceRects / Placements) and of the top-left record bar, shown a little shorter, and a
    /// tap on it closes it.
    /// </summary>
    public static class PsdHomeDialogue1031
    {
        // 2026-11-01 (赵金 "这个对话框有点太挡了"): about two thirds of the 10-31 box (540×108), text kept readable.
        public const float Width = 362f, Height = 80f;
        public const int LineSize = 21, LineMinSize = 16, SpeakerSize = 16;
        private static readonly Color32 InkText = new Color32(16, 8, 34, 255);

        /// <summary>
        /// Each idol's face in stage units (720-wide PSD stage, origin top-left, y down), measured on her
        /// authored portrait and both idle clips with a margin for the clips' head motion. The box is placed
        /// per idol so it never covers this rect.
        /// </summary>
        // 2026-11-04: offset by PsdHomeCharacterCarousel.CenterShift1104 (-25 / +63 / -40 / +54), which centres each face.
        public static readonly Rect[] FaceRects =
        {
            Rect.MinMaxRect(265f, 245f, 460f, 420f),  // 卡塔莉娜 CATALENA
            Rect.MinMaxRect(268f, 310f, 463f, 495f),  // 莉莉丝 LILITH
            Rect.MinMaxRect(275f, 180f, 475f, 350f),  // 紫晶精灵 AMETHYST
            Rect.MinMaxRect(309f, 320f, 539f, 520f),  // 粉珀人鱼 CORALIE
        };

        /// <summary>Top-left of the box (not its name plate) per idol, in the same stage units.</summary>
        public static readonly Vector2[] Placements =
        {
            new Vector2(14f, 444f),   // left of her chin, over the tail and braid
            new Vector2(282f, 514f),  // under her chin, right of the microphone
            new Vector2(16f, 366f),   // below-left of her chin, over the hair and sleeve
            new Vector2(282f, 540f),  // under her chin, right of the microphone
        };

        /// <summary>How long a line stays: long enough to read after it types out, shorter than the old 4.5 s.</summary>
        public static float HoldSeconds(string line) =>
            Mathf.Clamp(1.9f + (line?.Length ?? 0) * .075f, 2.6f, 3.6f);

        public sealed class Parts
        {
            public CanvasGroup Group;
            public Text Speaker, Line;
            public PsdHomeDialogueFrame1031 Frame;
            public PsdHomeDialogueReveal1031 Reveal;
            public PsdHomeDialogueDismiss1101 Dismiss;
        }

        public static Parts Build(RectTransform stage, Font font)
        {
            var panel = new GameObject("PsdCharacterDialogue", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(PsdHomeDialogueFrame1031), typeof(CanvasGroup), typeof(PsdHomeDialogueDismiss1101));
            panel.layer = stage.gameObject.layer;
            panel.transform.SetParent(stage, false);
            var rect = (RectTransform)panel.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(Width, Height);
            var frame = panel.GetComponent<PsdHomeDialogueFrame1031>();
            // The box itself takes the tap that closes it early (only while it is shown; see PsdHomeMotion).
            frame.raycastTarget = true;

            var group = panel.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            Text speaker = NewText("Speaker", panel.transform, font, SpeakerSize, InkText, FontStyle.BoldAndItalic, TextAnchor.MiddleLeft);
            speaker.horizontalOverflow = HorizontalWrapMode.Overflow;
            Place(speaker.rectTransform, PsdHomeDialogueFrame1031.TagX + 16f, -PsdHomeDialogueFrame1031.TagY, 260f,
                PsdHomeDialogueFrame1031.TagHeight);

            Text line = NewText("DialogueLine", panel.transform, font, LineSize, Color.white, FontStyle.Normal, TextAnchor.MiddleLeft);
            line.lineSpacing = 1.04f;
            // Long personalised lines shrink a little instead of spilling out of the box.
            line.resizeTextForBestFit = true;
            line.resizeTextMinSize = LineMinSize;
            line.resizeTextMaxSize = LineSize;
            Place(line.rectTransform, 20f, -16f, Width - 20f - 64f, Height - 16f - 4f);
            var reveal = line.gameObject.AddComponent<PsdHomeDialogueReveal1031>();
            // After the reveal effect, so the shadow copies each glyph's typed-in alpha. Keeps the white
            // line legible now the box lets more of the art through.
            var shade = line.gameObject.AddComponent<Shadow>();
            shade.effectColor = new Color32(10, 4, 24, 190);
            shade.effectDistance = new Vector2(1.2f, -1.4f);

            // The indicator: a broken teal ring round the spinning record, over the bottom-right corner.
            var ring = new GameObject("DialogueDiscRing", typeof(RectTransform), typeof(CanvasRenderer), typeof(PsdHomeDiscRing1031));
            ring.layer = panel.layer;
            ring.transform.SetParent(panel.transform, false);
            var ringRect = (RectTransform)ring.transform;
            ringRect.anchorMin = ringRect.anchorMax = ringRect.pivot = new Vector2(.5f, .5f);
            // Centred 34 in from the right and 45 down: inside the box's lower-right, clear of the line's text.
            ringRect.anchoredPosition = new Vector2(Width * .5f - 34f, Height * .5f - 45f);
            ringRect.sizeDelta = new Vector2(56f, 56f);
            ring.GetComponent<PsdHomeDiscRing1031>().raycastTarget = false;

            var disc = new GameObject("DialogueDisc", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(PsdHomeDiscSpin1031));
            disc.layer = panel.layer;
            disc.transform.SetParent(ring.transform, false);
            var discRect = (RectTransform)disc.transform;
            discRect.anchorMin = discRect.anchorMax = discRect.pivot = new Vector2(.5f, .5f);
            discRect.anchoredPosition = Vector2.zero;
            discRect.sizeDelta = new Vector2(41f, 41f);
            var discImage = disc.GetComponent<Image>();
            discImage.sprite = Resources.Load<Sprite>("Art/Lobby1031/dialogue-disc-1031");
            discImage.preserveAspect = true;
            discImage.raycastTarget = false;
            disc.GetComponent<PsdHomeDiscSpin1031>().Group = group;

            var parts = new Parts { Group = group, Speaker = speaker, Line = line, Frame = frame, Reveal = reveal,
                Dismiss = panel.GetComponent<PsdHomeDialogueDismiss1101>() };
            PlaceFor(parts, 0);
            FitTag(parts);
            return parts;
        }

        /// <summary>Moves the box to the idol's own safe place, clear of her face.</summary>
        public static void PlaceFor(Parts parts, int character)
        {
            if (parts?.Group == null) return;
            var rect = (RectTransform)parts.Group.transform;
            Vector2 p = Placements[Mathf.Clamp(character, 0, Placements.Length - 1)];
            rect.anchoredPosition = new Vector2(p.x, -p.y);
        }

        /// <summary>
        /// Picks the largest size (21 → 16) at which the line fits without leaving a lone character or
        /// punctuation mark on its second row (Text wraps CJK per glyph, so "……会吃醋的" + "。" happened).
        /// Falls back to best fit when no size does.
        /// </summary>
        public static void FitLine(Parts parts)
        {
            Text line = parts?.Line;
            if (line == null) return;
            Vector2 size = line.rectTransform.rect.size;
            string text = line.text ?? string.Empty;
            if (size.x <= 0f || size.y <= 0f || text.Length == 0) return;
            line.resizeTextForBestFit = false;
            TextGenerator generator = line.cachedTextGeneratorForLayout;
            for (int points = LineSize; points >= LineMinSize; points--)
            {
                line.fontSize = points;
                TextGenerationSettings settings = line.GetGenerationSettings(size);
                settings.verticalOverflow = VerticalWrapMode.Overflow;
                generator.Populate(text, settings);
                float height = generator.GetPreferredHeight(text, settings) / Mathf.Max(.01f, line.pixelsPerUnit);
                if (height > size.y + 1f) continue;
                IList<UILineInfo> rows = generator.lines;
                if (rows.Count <= 1) return;
                int tail = rows[rows.Count - 1].startCharIdx;
                if (tail <= text.Length && text.Substring(tail).Trim().Length >= 3) return;
            }
            line.fontSize = LineSize;
            line.resizeTextForBestFit = true;
        }

        /// <summary>The name plate grows with the speaker's name.</summary>
        public static void FitTag(Parts parts)
        {
            if (parts?.Speaker == null || parts.Frame == null) return;
            parts.Frame.TagWidth = Mathf.Clamp(parts.Speaker.preferredWidth + 34f, 110f, Width - 84f);
        }

        private static Text NewText(string name, Transform parent, Font font, int size, Color color, FontStyle style, TextAnchor anchor)
        {
            var node = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            node.layer = parent.gameObject.layer;
            node.transform.SetParent(parent, false);
            var text = node.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = anchor;
            text.raycastTarget = false;
            text.supportRichText = false;
            return text;
        }

        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }
    }

    /// <summary>The box, its teal rim and left-corner stroke, and the slanted name plate — one mesh, rebuilt
    /// only when the plate's width changes.</summary>
    public sealed class PsdHomeDialogueFrame1031 : MaskableGraphic
    {
        public const float TagX = 12f, TagY = -14f, TagHeight = 26f, TagSlant = 9f;
        private const float Radius = 16f;
        // 2026-11-01: the body is see-through enough for the art behind to read (was 91–99% opaque).
        public const float BodyAlphaLeft = 196f, BodyAlphaRight = 158f;
        private static readonly Color32 BoxTop = new Color32(40, 16, 76, 255);
        private static readonly Color32 BoxBottom = new Color32(14, 6, 32, 255);
        private static readonly Color32 Teal = new Color32(30, 232, 232, 255);
        private static readonly Color32 Shadow = new Color32(8, 3, 18, 220);
        private static readonly List<Vector2> Path = new List<Vector2>(64);
        private float tagWidth = 220f;

        public float TagWidth
        {
            get => tagWidth;
            set { if (Mathf.Abs(value - tagWidth) < .5f) return; tagWidth = value; SetVerticesDirty(); }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            if (r.width <= 0f || r.height <= 0f) return;
            RoundedPath(r, Radius);
            // Translucent body: a vertical violet gradient fanned from the centre, a little denser on the left
            // where the line starts; the right lets her hair read through, like refs/02's glass box.
            int centre = vh.currentVertCount;
            vh.AddVert(r.center, BodyColour(r, r.center), Vector2.zero);
            for (int i = 0; i < Path.Count; i++)
                vh.AddVert(Path[i], BodyColour(r, Path[i]), Vector2.zero);
            for (int i = 0; i < Path.Count; i++)
                vh.AddTriangle(centre, centre + 1 + i, centre + 1 + (i + 1) % Path.Count);
            // A faint teal rim inside the edge.
            Stroke(vh, 0, Path.Count, -1.5f, -0.2f, new Color32(Teal.r, Teal.g, Teal.b, 70), true);
            // The plate's stroke continues round the top-left corner and fades down the left side (refs/02 arc).
            int start = 0, end = Path.Count;
            for (int i = 0; i < Path.Count; i++)
                if (Path[i].x <= r.xMin + TagX + 30f && Path[i].y >= r.yMax - .5f) { start = i; break; }
            for (int i = start; i < Path.Count; i++)
                if (Path[i].x <= r.xMin + .5f && Path[i].y <= r.yMin + r.height * .28f) { end = i; break; }
            Tapered(vh, start, end, 1f, 5f);

            // Name plate: ink drop shadow, white paper rim, teal face — the same pop sticker language as the notices.
            float x0 = r.xMin + TagX, y1 = r.yMax - TagY, y0 = y1 - TagHeight;
            Parallelogram(vh, x0 + 4f, y0 - 4f, tagWidth, TagHeight, Shadow, 0f);
            Parallelogram(vh, x0, y0, tagWidth, TagHeight, Color.white, 2f);
            Parallelogram(vh, x0, y0, tagWidth, TagHeight, Teal, 0f);
            // A thin ink slash near the plate's tail, a small P5-style cut.
            float sx = x0 + tagWidth - 12f;
            Quad(vh, new Vector2(sx, y0 + 3f), new Vector2(sx + 4f, y0 + 3f), new Vector2(sx + 4f + TagSlant * .7f, y1 - 3f),
                new Vector2(sx + TagSlant * .7f, y1 - 3f), new Color32(16, 8, 34, 200));
        }

        private static Color32 BodyColour(Rect r, Vector2 p)
        {
            Color32 c = Color32.Lerp(BoxBottom, BoxTop, Mathf.InverseLerp(r.yMin, r.yMax, p.y));
            c.a = (byte)Mathf.RoundToInt(Mathf.Lerp(BodyAlphaLeft, BodyAlphaRight, Mathf.InverseLerp(r.xMin, r.xMax, p.x)));
            return c;
        }

        private static void RoundedPath(Rect r, float radius)
        {
            Path.Clear();
            radius = Mathf.Min(radius, r.height * .5f, r.width * .5f);
            const int Steps = 8;
            // Counter-clockwise from the top-right corner: top edge runs right → left first.
            Vector2[] centres =
            {
                new Vector2(r.xMax - radius, r.yMax - radius), new Vector2(r.xMin + radius, r.yMax - radius),
                new Vector2(r.xMin + radius, r.yMin + radius), new Vector2(r.xMax - radius, r.yMin + radius),
            };
            for (int c = 0; c < 4; c++)
                for (int s = 0; s <= Steps; s++)
                {
                    float a = (c * 90f + s * 90f / Steps) * Mathf.Deg2Rad;
                    Path.Add(centres[c] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
                }
        }

        private static Vector2 Normal(int i)
        {
            int n = Path.Count;
            Vector2 d = Path[(i + 1) % n] - Path[(i - 1 + n) % n];
            return new Vector2(d.y, -d.x).normalized; // outward for a counter-clockwise path
        }

        private static void Stroke(VertexHelper vh, int from, int to, float inner, float outer, Color32 tint, bool closed)
        {
            int first = vh.currentVertCount, count = 0;
            int last = closed ? to : to - 1;
            for (int k = from; k <= last; k++)
            {
                int i = k % Path.Count;
                Vector2 n = Normal(i);
                vh.AddVert(Path[i] + n * inner, tint, Vector2.zero);
                vh.AddVert(Path[i] + n * outer, tint, Vector2.zero);
                count++;
            }
            for (int k = 0; k < count - 1; k++)
            {
                int a = first + k * 2;
                vh.AddTriangle(a, a + 1, a + 3); vh.AddTriangle(a, a + 3, a + 2);
            }
        }

        private static void Tapered(VertexHelper vh, int from, int to, float offset, float width)
        {
            int first = vh.currentVertCount, count = to - from;
            if (count < 2) return;
            for (int k = 0; k < count; k++)
            {
                int i = from + k;
                float t = k / (float)(count - 1);
                float w = width * (1f - t * t);
                Vector2 n = Normal(i);
                Color32 tint = new Color32(30, 232, 232, (byte)(255 * (1f - t * .7f)));
                vh.AddVert(Path[i] + n * offset, tint, Vector2.zero);
                vh.AddVert(Path[i] + n * (offset + w), tint, Vector2.zero);
            }
            for (int k = 0; k < count - 1; k++)
            {
                int a = first + k * 2;
                vh.AddTriangle(a, a + 1, a + 3); vh.AddTriangle(a, a + 3, a + 2);
            }
        }

        private static void Parallelogram(VertexHelper vh, float x, float y, float w, float h, Color32 tint, float grow)
        {
            Quad(vh, new Vector2(x - grow, y - grow), new Vector2(x + w + grow, y - grow),
                new Vector2(x + w + TagSlant + grow, y + h + grow), new Vector2(x + TagSlant - grow, y + h + grow), tint);
        }

        private static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color32 tint)
        {
            int i = vh.currentVertCount;
            vh.AddVert(a, tint, Vector2.zero); vh.AddVert(b, tint, Vector2.zero);
            vh.AddVert(c, tint, Vector2.zero); vh.AddVert(d, tint, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3);
        }
    }

    /// <summary>2026-11-01: a tap on the shown box closes it early.</summary>
    public sealed class PsdHomeDialogueDismiss1101 : MonoBehaviour, UnityEngine.EventSystems.IPointerClickHandler
    {
        public System.Action Clicked;

        public void OnPointerClick(UnityEngine.EventSystems.PointerEventData eventData) => Clicked?.Invoke();
    }

    /// <summary>The broken teal ring round the record (refs/02's indicator ring), drawn once.</summary>
    public sealed class PsdHomeDiscRing1031 : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            float radius = Mathf.Min(r.width, r.height) * .5f;
            if (radius <= 0f) return;
            Vector2 c = r.center;
            // A soft dark disc behind the record lifts it off the violet box.
            AlbumGraphicGeometry929.Ring(vh, c, radius * .80f, 0f, 0, 360, new Color32(6, 2, 16, 150), 40);
            AlbumGraphicGeometry929.Ring(vh, c, radius, radius * .9f, 108, 222, new Color32(30, 232, 232, 255), 40);
            AlbumGraphicGeometry929.Ring(vh, c, radius, radius * .9f, 342, 70, new Color32(30, 232, 232, 255), 16);
            AlbumGraphicGeometry929.Ring(vh, c, radius * .86f, radius * .83f, 200, 90, new Color32(255, 255, 255, 150), 18);
        }
    }

    /// <summary>Turns the record while the box is visible; still under reduced motion.</summary>
    public sealed class PsdHomeDiscSpin1031 : MonoBehaviour
    {
        public CanvasGroup Group;
        private float angle;

        private void Update()
        {
            if (Group != null && Group.alpha <= 0f) return;
            if (BattlePresentationOptions.ReduceMotion) return;
            angle = Mathf.Repeat(angle - Time.unscaledDeltaTime * 360f / 2.2f, 360f);
            transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    /// <summary>
    /// Typewriter reveal on the line's own mesh: Text.text always holds the whole line; glyphs fade in one by one.
    /// Only re-meshes while revealing. Reduced motion shows the whole line at once.
    /// </summary>
    public sealed class PsdHomeDialogueReveal1031 : BaseMeshEffect
    {
        private const float CharsPerSecond = 30f;
        private float started = -10f, shown = float.MaxValue;

        public void Restart()
        {
            started = Time.unscaledTime;
            shown = BattlePresentationOptions.ReduceMotion ? float.MaxValue : 0f;
            graphic?.SetVerticesDirty();
        }

        public bool Revealing => shown < float.MaxValue;

        private void Update()
        {
            if (!Revealing) return;
            float next = (Time.unscaledTime - started) * CharsPerSecond;
            // Done once every glyph is opaque (a generous bound: the line is short).
            if (next > 200f || BattlePresentationOptions.ReduceMotion) next = float.MaxValue;
            if (next == shown) return;
            shown = next;
            graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || !Revealing) return;
            UIVertex vertex = default;
            int quads = vh.currentVertCount / 4;
            for (int q = 0; q < quads; q++)
            {
                float a = Mathf.Clamp01(shown - q);
                if (a >= 1f) continue;
                for (int k = 0; k < 4; k++)
                {
                    vh.PopulateUIVertex(ref vertex, q * 4 + k);
                    vertex.color.a = (byte)(vertex.color.a * a);
                    vh.SetUIVertex(vertex, q * 4 + k);
                }
            }
        }
    }
}
