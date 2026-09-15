using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>
    /// Soft diagonal dissolve for the equipment portrait: instead of the old hard waist
    /// cut, vertex alpha ramps to zero over a band that rises toward the right, echoing
    /// the crystalline lower rim of the authored aperture. Positions are evaluated in
    /// aperture space (pixels down from the frame top), so the effect works on the
    /// oversized bust-fitted image produced by PanelKit.FrameBustPortrait.
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    public sealed class EquipmentPolish038PortraitFade : BaseMeshEffect
    {
        public float FadeStart = 305f;
        public float FadeLength = 140f;
        public float RightRise = 64f;
        public float FrameWidth = 378f;
        public int Columns = 10;
        public int Rows = 26;

        public override void ModifyMesh(VertexHelper vertices)
        {
            if (!IsActive() || vertices.currentVertCount != 4) return;
            UIVertex bottomLeft = default, topLeft = default, topRight = default, bottomRight = default;
            vertices.PopulateUIVertex(ref bottomLeft, 0);
            vertices.PopulateUIVertex(ref topLeft, 1);
            vertices.PopulateUIVertex(ref topRight, 2);
            vertices.PopulateUIVertex(ref bottomRight, 3);
            vertices.Clear();

            RectTransform rect = graphic.rectTransform;
            // Image pivot is top-center: anchoredPosition is the pivot in aperture space
            // (x right, y up). Convert local vertex coords to aperture-space pixels.
            float pivotX = rect.anchoredPosition.x;
            float pivotY = rect.anchoredPosition.y;
            int cols = Mathf.Max(2, Columns), rows = Mathf.Max(2, Rows);
            for (int row = 0; row <= rows; row++)
            {
                float t = row / (float)rows;
                for (int col = 0; col <= cols; col++)
                {
                    float s = col / (float)cols;
                    UIVertex vertex = Interpolate(Interpolate(topLeft, topRight, s), Interpolate(bottomLeft, bottomRight, s), t);
                    float frameY = -pivotY - vertex.position.y;
                    float u = Mathf.Clamp01((pivotX + vertex.position.x + FrameWidth * .5f) / FrameWidth);
                    float start = FadeStart + RightRise * (.5f - u);
                    float fade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((start + FadeLength - frameY) / FadeLength));
                    Color color = vertex.color;
                    color.a *= fade;
                    vertex.color = color;
                    vertices.AddVert(vertex);
                }
            }
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    int a = row * (cols + 1) + col;
                    int b = a + cols + 1;
                    vertices.AddTriangle(b, a, a + 1);
                    vertices.AddTriangle(a + 1, b + 1, b);
                }
            }
        }

        private static UIVertex Interpolate(UIVertex top, UIVertex bottom, float t)
        {
            UIVertex vertex = top;
            vertex.position = Vector3.Lerp(top.position, bottom.position, t);
            vertex.uv0 = Vector4.Lerp(top.uv0, bottom.uv0, t);
            vertex.uv1 = Vector4.Lerp(top.uv1, bottom.uv1, t);
            vertex.color = Color.Lerp(top.color, bottom.color, t);
            return vertex;
        }
    }
}
