using UnityEngine;
using UnityEngine.UI;
namespace ChoSiren
{
    internal static class AlbumGraphicGeometry929
    {
        public static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
        {int n=vh.currentVertCount;vh.AddVert(a,color,Vector2.zero);vh.AddVert(b,color,Vector2.zero);vh.AddVert(c,color,Vector2.zero);vh.AddVert(d,color,Vector2.zero);vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);}
        public static void Beam(VertexHelper vh,Vector2 a,Vector2 b,float width,Color color)
        {Vector2 n=new Vector2(-(b-a).y,(b-a).x).normalized*width*.5f;Quad(vh,a+n,a-n,b-n,b+n,color);}
        public static void Ring(VertexHelper vh,Vector2 center,float outer,float inner,float from,float sweep,Color color,int segments=80,float squash=1)
        {
            Vector2 P(float a,float r)=>center+new Vector2(Mathf.Cos(a)*r,Mathf.Sin(a)*r*squash);
            for(int i=0;i<segments;i++){float a=(from+sweep*i/segments)*Mathf.Deg2Rad,b=(from+sweep*(i+1)/segments)*Mathf.Deg2Rad;Quad(vh,P(a,outer),P(a,inner),P(b,inner),P(b,outer),color);}
        }
    }
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class AlbumPlayGlyph929 : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {vh.Clear();var r=rectTransform.rect;vh.AddVert(new Vector2(r.xMin,r.yMin),color,Vector2.zero);vh.AddVert(new Vector2(r.xMin,r.yMax),color,Vector2.zero);vh.AddVert(new Vector2(r.xMax,r.center.y),color,Vector2.zero);vh.AddTriangle(0,1,2);}
    }
}
