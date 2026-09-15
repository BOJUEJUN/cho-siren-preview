using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    // Source-pixel contour measured inside the profile's irregular portrait frame.
    // The stencil and the foreground rim use the same contour, never a rectangular crop.
    internal static class ProfileAperture038
    {
        internal static readonly Vector2[] Points =
        {
            new Vector2(255,84), new Vector2(376,185), new Vector2(416,243),
            new Vector2(401,371), new Vector2(430,580), new Vector2(412,680),
            new Vector2(292,748), new Vector2(181,729), new Vector2(93,749),
            new Vector2(57,691), new Vector2(89,477), new Vector2(84,426),
            new Vector2(60,284), new Vector2(58,204),
        };
        internal static readonly Vector2 Center = new Vector2(244,430);
        internal static Vector2 Local(Rect rect, Vector2 point) =>
            new Vector2(rect.xMin + (point.x - 35) * rect.width / 425f,
                rect.yMax - (point.y - 70) * rect.height / 710f);
    }

    public sealed class ProfilePortraitMask038 : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            mesh.AddVert(ProfileAperture038.Local(rectTransform.rect, ProfileAperture038.Center), Color.white, Vector2.zero);
            foreach (Vector2 point in ProfileAperture038.Points)
                mesh.AddVert(ProfileAperture038.Local(rectTransform.rect, point), Color.white, Vector2.zero);
            for (int i = 0; i < ProfileAperture038.Points.Length; i++)
                mesh.AddTriangle(0, i + 1, (i + 1) % ProfileAperture038.Points.Length + 1);
        }
    }

    public sealed class ProfilePortraitRim038 : MaskableGraphic
    {
        private Sprite source;
        public Sprite Source { get => source; set { source = value; SetAllDirty(); } }
        public override Texture mainTexture => source != null ? source.texture : Texture2D.whiteTexture;
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (source == null) return;
            foreach (Vector2 point in ProfileAperture038.Points)
            {
                Vector2 direction = (point - ProfileAperture038.Center).normalized;
                Add(mesh, point - direction * 3f);
                Add(mesh, point + direction * 18f);
            }
            for (int i = 0; i < ProfileAperture038.Points.Length; i++)
            {
                int a = i * 2, b = ((i + 1) % ProfileAperture038.Points.Length) * 2;
                mesh.AddTriangle(a,a+1,b+1); mesh.AddTriangle(a,b+1,b);
            }
        }
        private void Add(VertexHelper mesh, Vector2 point)
        {
            Rect sourceRect = source.rect;
            Vector2 uv = new Vector2((sourceRect.x + point.x / 860f * sourceRect.width) / source.texture.width,
                (sourceRect.yMax - point.y / 1828f * sourceRect.height) / source.texture.height);
            mesh.AddVert(ProfileAperture038.Local(rectTransform.rect, point), Color.white, uv);
        }
    }

    // The passive frame is a six-sided outline. A filled heart serves only as a
    // dark inner face behind the affection number; the outer gem is existing RGBA art.
    public sealed class ProfileEmblemFrame038 : MaskableGraphic
    {
        public bool SolidHeart;
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = rectTransform.rect;
            if (SolidHeart)
            {
                const int count = 48;
                mesh.AddVert(rect.center, color, Vector2.zero);
                for (int i = 0; i < count; i++)
                {
                    float t = i * Mathf.PI * 2f / count;
                    float x = 16f * Mathf.Pow(Mathf.Sin(t), 3);
                    float y = 13f*Mathf.Cos(t) - 5f*Mathf.Cos(2*t) - 2f*Mathf.Cos(3*t) - Mathf.Cos(4*t);
                    mesh.AddVert(new Vector2(rect.center.x + x / 34f * rect.width,
                        rect.yMin + (y + 17f) / 31f * rect.height), color, Vector2.zero);
                }
                for (int i = 0; i < count; i++) mesh.AddTriangle(0,i+1,(i+1)%count+1);
                return;
            }
            for (int i = 0; i < 6; i++)
            {
                float angle = (30 + i * 60) * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                mesh.AddVert(rect.center + Vector2.Scale(direction, rect.size) * .47f, color, Vector2.zero);
                mesh.AddVert(rect.center + Vector2.Scale(direction, rect.size) * .41f, color, Vector2.zero);
            }
            for (int i = 0; i < 6; i++)
            {
                int a = i * 2, b = ((i+1)%6)*2;
                mesh.AddTriangle(a,b,b+1); mesh.AddTriangle(a,b+1,a+1);
            }
        }
    }
}
