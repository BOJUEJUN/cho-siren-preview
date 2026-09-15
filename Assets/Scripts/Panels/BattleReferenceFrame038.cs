using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.Panels
{
    // Reference-shaped decoration only. The real Button remains an unchanged hit target.
    public sealed class BattleReferenceFrame038 : MaskableGraphic
    {
        public enum FrameKind { SelectedReroll, AllReroll, Member, Information, Heart }
        public FrameKind Kind;
        public Color Accent = new Color32(193, 76, 255, 255);
        private static Sprite transparentHitSprite;
        public static Sprite TransparentHitSprite
        {
            get
            {
                if (transparentHitSprite != null) return transparentHitSprite;
                var texture = new Texture2D(2,2,TextureFormat.RGBA32,false);
                texture.SetPixels(new[] { Color.clear, Color.clear, Color.clear, Color.clear });
                texture.Apply(false,true); texture.hideFlags = HideFlags.DontSave;
                transparentHitSprite = Sprite.Create(texture,new Rect(0,0,2,2),Vector2.one*.5f);
                transparentHitSprite.hideFlags = HideFlags.DontSave;
                return transparentHitSprite;
            }
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (Kind == FrameKind.Heart) { Heart(mesh); return; }
            Vector2[] outline = Kind == FrameKind.SelectedReroll
                ? new[] { P(.01f,.04f),P(.22f,.21f),P(.99f,.08f),P(.89f,.47f),P(.97f,.93f),P(.15f,.98f),P(.02f,.69f),P(.12f,.48f) }
                : Kind == FrameKind.AllReroll
                ? new[] { P(.01f,.31f),P(.34f,.16f),P(.97f,.02f),P(.90f,.45f),P(.99f,.96f),P(.76f,.85f),P(.11f,.98f),P(.04f,.70f) }
                : Kind == FrameKind.Member
                ? new[] { P(.11f,.03f),P(.97f,0),P(.88f,.91f),P(.01f,1) }
                : new[] { P(.06f,.13f),P(.98f,.02f),P(.90f,.86f),P(.01f,1) };
            Polygon(mesh, outline, new Color32(13, 7, 31, 248));
            for (int i = 0; i < outline.Length; i++)
                Line(mesh, outline[i], outline[(i + 1) % outline.Length], 2.2f, i % 3 == 0 ? Color.white : Accent);
            Vector2 center = P(.5f,.5f);
            for (int i = 0; i < outline.Length; i++)
                Line(mesh, Vector2.Lerp(outline[i], center, .10f),
                    Vector2.Lerp(outline[(i + 1) % outline.Length], center, .10f), 1f,
                    new Color(Accent.r, Accent.g, Accent.b, .8f));
            if (Kind == FrameKind.SelectedReroll || Kind == FrameKind.AllReroll)
            {
                Triangle(mesh,P(.02f,.10f),P(.13f,.33f),P(.06f,.44f),Color.white);
                Triangle(mesh,P(.03f,.82f),P(.19f,.74f),P(.13f,.95f),Accent);
                Triangle(mesh,P(.90f,.06f),P(.97f,.01f),P(.86f,.32f),Accent);
                Triangle(mesh,P(.93f,.66f),P(.995f,.95f),P(.78f,.81f),Color.white);
                Line(mesh,P(.13f,.26f),P(.67f,.12f),2,Color.white);
                Line(mesh,P(.23f,.92f),P(.77f,.83f),2,Accent);
                for (int i=0;i<5;i++) Line(mesh,P(.73f+i*.018f,.71f),P(.76f+i*.018f,.52f),2,Accent);
            }
            else
            {
                Triangle(mesh,P(.03f,.04f),P(.20f,.01f),P(.08f,.16f),Accent);
                Triangle(mesh,P(.91f,.83f),P(1,.71f),P(.95f,.97f),Accent);
                Line(mesh,P(.13f,.06f),P(.63f,.035f),2,Color.white);
            }
        }

        private void Heart(VertexHelper mesh)
        {
            Vector2[] plate = { P(.02f,.27f), P(.76f,.02f), P(.99f,.64f), P(.28f,.99f) };
            Polygon(mesh,plate,new Color32(193,24,133,255));
            for(int i=0;i<plate.Length;i++) Line(mesh,plate[i],plate[(i+1)%plate.Length],2,Color.white);
            Vector2[] heart = { P(.50f,.34f),P(.37f,.20f),P(.20f,.24f),P(.15f,.41f),P(.29f,.62f),
                P(.53f,.79f),P(.79f,.44f),P(.78f,.27f),P(.63f,.19f) };
            Polygon(mesh,heart,new Color32(255,54,177,255));
            for(int i=0;i<heart.Length;i++) Line(mesh,heart[i],heart[(i+1)%heart.Length],2,Color.white);
            Line(mesh,P(.25f,.29f),P(.36f,.25f),3,new Color32(255,202,244,255));
        }
        private Vector2 P(float x,float y) => new Vector2(rectTransform.rect.xMin+x*rectTransform.rect.width,
            rectTransform.rect.yMax-y*rectTransform.rect.height);
        private static void Polygon(VertexHelper mesh,Vector2[] points,Color tint)
        {
            Vector2 center=Vector2.zero; foreach(Vector2 p in points) center+=p; center/=points.Length;
            for(int i=0;i<points.Length;i++) Triangle(mesh,center,points[i],points[(i+1)%points.Length],tint);
        }
        private static void Triangle(VertexHelper mesh,Vector2 a,Vector2 b,Vector2 c,Color tint)
        {
            int start=mesh.currentVertCount; mesh.AddVert(a,tint,Vector2.zero); mesh.AddVert(b,tint,Vector2.zero);
            mesh.AddVert(c,tint,Vector2.zero); mesh.AddTriangle(start,start+1,start+2);
        }
        private static void Line(VertexHelper mesh,Vector2 a,Vector2 b,float thickness,Color tint)
        {
            Vector2 d=b-a; if(d.sqrMagnitude<.001f)return;
            Vector2 n=new Vector2(-d.y,d.x).normalized*thickness*.5f;
            int i=mesh.currentVertCount; mesh.AddVert(a+n,tint,Vector2.zero);mesh.AddVert(b+n,tint,Vector2.zero);
            mesh.AddVert(b-n,tint,Vector2.zero);mesh.AddVert(a-n,tint,Vector2.zero);
            mesh.AddTriangle(i,i+1,i+2);mesh.AddTriangle(i+2,i+3,i);
        }
    }
}
