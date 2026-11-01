using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.UI
{
    /// <summary>A solid circular vinyl cover. Both its UI box and drawn radius stay uniform.</summary>
    public sealed class RecordTransitionDisc1019 : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            float radius = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * .5f;
            Vector2 center = rectTransform.rect.center;
            const int segments = 100;
            vh.AddVert(center, new Color32(8, 6, 18, 255), Vector2.zero);
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                vh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius,
                    new Color32(8, 6, 18, 255), Vector2.zero);
                if (i > 0) vh.AddTriangle(0, i, i + 1);
            }
            AlbumGraphicGeometry929.Ring(vh, center, radius, radius * .987f, 0, 360, new Color32(149, 94, 255, 230), 100);
            for (int i = 0; i < 7; i++)
                AlbumGraphicGeometry929.Ring(vh, center, radius * (.47f + i * .063f),
                    radius * (.468f + i * .063f), 0, 360, new Color32(189, 174, 221, 55), 100);
            AlbumGraphicGeometry929.Ring(vh, center, radius * .25f, radius * .045f, 0, 360, new Color32(112, 52, 219, 255), 64);
            AlbumGraphicGeometry929.Ring(vh, center, radius * .93f, radius * .91f, 16, 38, new Color32(131, 225, 244, 185), 24);
        }
    }
}
