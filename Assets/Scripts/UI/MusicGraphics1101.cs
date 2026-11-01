using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.UI
{
    /// <summary>
    /// 2026-11-01 唱片收藏: a slanted quad drawn from vertices (the mock's clip-path chips, NOW tag and seek
    /// track). Corner insets are fractions of the width; <see cref="Fill"/> splits it into a left colour
    /// (<see cref="FillColor"/>) and the remainder (<see cref="Graphic.color"/>) along its length.
    /// </summary>
    public sealed class MusicQuad1101 : MaskableGraphic
    {
        [SerializeField] private float insetTopLeft, insetTopRight, insetBottomRight, insetBottomLeft;
        [SerializeField] private float fill = -1f;
        [SerializeField] private Color fillColor = Color.white;

        public void SetInsets(float topLeft, float topRight, float bottomRight, float bottomLeft)
        {
            insetTopLeft = topLeft; insetTopRight = topRight; insetBottomRight = bottomRight; insetBottomLeft = bottomLeft;
            SetVerticesDirty();
        }

        /// <summary>Negative = no split (one colour).</summary>
        public float Fill
        {
            get => fill;
            set { if (Mathf.Approximately(fill, value)) return; fill = value; SetVerticesDirty(); }
        }

        public Color FillColor
        {
            get => fillColor;
            set { if (fillColor == value) return; fillColor = value; SetVerticesDirty(); }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            float w = r.width;
            float tl = r.xMin + insetTopLeft * w, tr = r.xMax - insetTopRight * w;
            float bl = r.xMin + insetBottomLeft * w, br = r.xMax - insetBottomRight * w;
            if (fill < 0f)
            {
                AddQuad(vh, bl, br, tl, tr, r.yMin, r.yMax, color);
                return;
            }
            float f = Mathf.Clamp01(fill);
            float topCut = Mathf.Lerp(tl, tr, f), bottomCut = Mathf.Lerp(bl, br, f);
            if (f > 0f) AddQuad(vh, bl, bottomCut, tl, topCut, r.yMin, r.yMax, fillColor);
            if (f < 1f) AddQuad(vh, bottomCut, br, topCut, tr, r.yMin, r.yMax, color);
        }

        private static void AddQuad(VertexHelper vh, float bx0, float bx1, float tx0, float tx1, float y0, float y1, Color c)
        {
            int start = vh.currentVertCount;
            vh.AddVert(new Vector3(bx0, y0), c, Vector2.zero);
            vh.AddVert(new Vector3(tx0, y1), c, Vector2.zero);
            vh.AddVert(new Vector3(tx1, y1), c, Vector2.zero);
            vh.AddVert(new Vector3(bx1, y0), c, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start + 2, start + 3, start);
        }
    }

    /// <summary>Crisp play ▶ / pause ❚❚ symbol (the font subset has no pause glyph).</summary>
    public sealed class MusicGlyph1101 : MaskableGraphic
    {
        [SerializeField] private bool paused;

        /// <summary>True draws the pause bars, false the play triangle.</summary>
        public bool ShowPause
        {
            get => paused;
            set { if (paused == value) return; paused = value; SetVerticesDirty(); }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            float s = Mathf.Min(r.width, r.height);
            Vector2 c = r.center;
            if (paused)
            {
                float bw = s * .26f, gap = s * .18f, h = s * .78f;
                Bar(vh, c.x - gap * .5f - bw, c.y - h * .5f, bw, h);
                Bar(vh, c.x + gap * .5f, c.y - h * .5f, bw, h);
                return;
            }
            float tw = s * .74f, th = s * .84f;
            int start = vh.currentVertCount;
            // Optical centre: the triangle sits slightly right of the box centre.
            float x0 = c.x - tw * .4f;
            vh.AddVert(new Vector3(x0, c.y - th * .5f), color, Vector2.zero);
            vh.AddVert(new Vector3(x0, c.y + th * .5f), color, Vector2.zero);
            vh.AddVert(new Vector3(x0 + tw, c.y), color, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
        }

        private void Bar(VertexHelper vh, float x, float y, float w, float h)
        {
            int start = vh.currentVertCount;
            vh.AddVert(new Vector3(x, y), color, Vector2.zero);
            vh.AddVert(new Vector3(x, y + h), color, Vector2.zero);
            vh.AddVert(new Vector3(x + w, y + h), color, Vector2.zero);
            vh.AddVert(new Vector3(x + w, y), color, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start + 2, start + 3, start);
        }
    }
}
