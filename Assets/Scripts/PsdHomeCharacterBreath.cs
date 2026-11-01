using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>
    /// Idle life for the flat lobby portrait, in the manner of a Live2D idle: the quad is
    /// split into a grid and bent instead of moved as one piece. The body sways from the
    /// feet, the chest rises with each breath, the head follows the sway a beat late and
    /// the hair at both sides trails behind. The portrait and its rim share one field,
    /// measured against the portrait's rect in the parent, so they always bend together.
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    public sealed class PsdHomeCharacterBreath : BaseMeshEffect
    {
        private const int Columns = 12, Rows = 28;
        private readonly UIVertex[] corners = new UIVertex[4];
        public RectTransform Reference { get; set; }
        // 0 holds the graphic still (its mesh is rebuilt once more, then left alone).
        public float Amount { get; set; } = 1f;
        private float builtAmount = -1f;

        private void Update()
        {
            if (graphic == null || !graphic.enabled) return;
            if (Amount > 0f || builtAmount != 0f) graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || Reference == null || vh.currentVertCount != 4) return;
            // Image.Type.Simple order: bottom-left, top-left, top-right, bottom-right.
            for (int i = 0; i < 4; i++) vh.PopulateUIVertex(ref corners[i], i);
            vh.Clear();
            builtAmount = Amount;
            float now = Time.unscaledTime;
            Vector2 origin = ((RectTransform)transform).localPosition;
            Rect box = Reference.rect;
            Vector2 boxOrigin = Reference.localPosition;
            for (int row = 0; row <= Rows; row++)
            {
                float v = (float)row / Rows;
                for (int column = 0; column <= Columns; column++)
                {
                    float u = (float)column / Columns;
                    UIVertex vertex = corners[0];
                    vertex.position = Vector3.Lerp(Vector3.Lerp(corners[0].position, corners[3].position, u),
                        Vector3.Lerp(corners[1].position, corners[2].position, u), v);
                    vertex.uv0 = Vector4.Lerp(Vector4.Lerp(corners[0].uv0, corners[3].uv0, u),
                        Vector4.Lerp(corners[1].uv0, corners[2].uv0, u), v);
                    Vector2 parent = origin + (Vector2)vertex.position;
                    float x = Mathf.InverseLerp(boxOrigin.x + box.xMin, boxOrigin.x + box.xMax, parent.x) - .5f;
                    float h = Mathf.InverseLerp(boxOrigin.y + box.yMin, boxOrigin.y + box.yMax, parent.y);
                    vertex.position += (Vector3)(Offset(x, h, now) * Amount);
                    vh.AddVert(vertex);
                }
            }
            for (int row = 0; row < Rows; row++)
            {
                for (int column = 0; column < Columns; column++)
                {
                    int a = row * (Columns + 1) + column, b = a + Columns + 1;
                    vh.AddTriangle(a, b, b + 1);
                    vh.AddTriangle(b + 1, a + 1, a);
                }
            }
        }

        // x: -.5 (left) to .5 (right) of the portrait; h: 0 at the feet, 1 at the top.
        // Offsets are in canvas units (the canvas is 720 wide), deliberately small.
        private static Vector2 Offset(float x, float h, float now)
        {
            float breath = .5f - .5f * Mathf.Cos(now * 1.75f);              // ~3.6 s per breath
            float sway = Mathf.Sin(now * 1.2f);                                // ~5.2 s per sway
            float lag = Mathf.Sin(now * 1.2f - .9f);                           // head follows late
            float trail = Mathf.Sin(now * 1.55f - 1.6f);                       // hair trails further
            float dx = 2.4f * sway * h * h
                + 1.3f * lag * Smooth(.72f, .95f, h)
                + 2.0f * trail * Smooth(.17f, .34f, Mathf.Abs(x)) * Smooth(.22f, .5f, h) * (1f - Smooth(.68f, .82f, h));
            // The chest lifts and the shoulders, head and everything above rise with it.
            float dy = 2.1f * breath * Smooth(.42f, .64f, h);
            // A breath also widens the ribcage a little.
            dx += 1.2f * breath * x * Bump(h, .55f, .12f);
            return new Vector2(dx, dy);
        }

        private static float Smooth(float from, float to, float t) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, t));
        private static float Bump(float t, float centre, float width) => Mathf.Clamp01(1f - Mathf.Abs(t - centre) / width);
    }
}
