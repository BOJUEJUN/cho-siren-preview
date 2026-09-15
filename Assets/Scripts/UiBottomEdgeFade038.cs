using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    // Blend only the last 26% of an authored header panel into the current page.
    // Sprite pixels/UVs are unchanged; live HUD values are separate unaffected nodes.
    [RequireComponent(typeof(Graphic))]
    public sealed class UiBottomEdgeFade038 : BaseMeshEffect
    {
        public const float FadeFraction = .26f;

        public override void ModifyMesh(VertexHelper vertices)
        {
            if (!IsActive() || vertices.currentVertCount != 4) return;
            UIVertex bottomLeft = default, topLeft = default, topRight = default, bottomRight = default;
            vertices.PopulateUIVertex(ref bottomLeft, 0);
            vertices.PopulateUIVertex(ref topLeft, 1);
            vertices.PopulateUIVertex(ref topRight, 2);
            vertices.PopulateUIVertex(ref bottomRight, 3);
            vertices.Clear();
            float[] rows = { 0f, .065f, .13f, .195f, FadeFraction, 1f };
            for (int row = 0; row < rows.Length; row++)
            {
                float t = rows[row];
                float fade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / FadeFraction));
                vertices.AddVert(Interpolate(bottomLeft, topLeft, t, fade));
                vertices.AddVert(Interpolate(bottomRight, topRight, t, fade));
                if (row == 0) continue;
                int lower = (row - 1) * 2;
                vertices.AddTriangle(lower, lower + 2, lower + 3);
                vertices.AddTriangle(lower + 3, lower + 1, lower);
            }
        }

        private static UIVertex Interpolate(UIVertex bottom, UIVertex top, float t, float alpha)
        {
            UIVertex vertex = bottom;
            vertex.position = Vector3.Lerp(bottom.position, top.position, t);
            vertex.uv0 = Vector4.Lerp(bottom.uv0, top.uv0, t);
            vertex.uv1 = Vector4.Lerp(bottom.uv1, top.uv1, t);
            Color color = Color.Lerp(bottom.color, top.color, t);
            color.a *= alpha;
            vertex.color = color;
            return vertex;
        }
    }
}
