using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.UI
{
    /// <summary>
    /// 2026-11-01c 无限骰子: a filled annulus arc (clockwise from 12 o'clock) for the reroll charge ring. Pure mesh, no
    /// texture; the caller animates <see cref="Fill"/> and colour. Soft edges come from a 1-px feather ring of vertices.
    /// </summary>
    public sealed class ArcRing1101 : MaskableGraphic
    {
        [Range(0f, 1f)] [SerializeField] private float fill = 1f;
        [Range(.04f, .6f)] public float Thickness = .16f;
        public int Segments = 64;

        public float Fill
        {
            get => fill;
            set { value = Mathf.Clamp01(value); if (Mathf.Abs(value - fill) < .0005f) return; fill = value; SetVerticesDirty(); }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (fill <= 0f) return;
            Rect r = rectTransform.rect;
            Vector2 c = r.center;
            float outer = Mathf.Min(r.width, r.height) * .5f, inner = outer * (1f - Thickness);
            float feather = Mathf.Max(.75f, outer * .04f);
            int steps = Mathf.Max(2, Mathf.CeilToInt(Segments * fill));
            Color32 solid = color, clear = color; clear.a = 0;
            for (int i = 0; i <= steps; i++)
            {
                float a = Mathf.PI * .5f - fill * Mathf.PI * 2f * i / steps;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                vh.AddVert(c + d * (outer + feather), clear, Vector2.zero);
                vh.AddVert(c + d * outer, solid, Vector2.zero);
                vh.AddVert(c + d * inner, solid, Vector2.zero);
                vh.AddVert(c + d * (inner - feather), clear, Vector2.zero);
                if (i == 0) continue;
                int b = (i - 1) * 4, n = i * 4;
                for (int k = 0; k < 3; k++)
                {
                    vh.AddTriangle(b + k, b + k + 1, n + k + 1);
                    vh.AddTriangle(b + k, n + k + 1, n + k);
                }
            }
        }
    }
}
