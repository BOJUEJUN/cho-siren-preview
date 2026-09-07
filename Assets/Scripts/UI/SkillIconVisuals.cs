using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    public enum SkillIconKind { Slash, Charm, Heal, Shield, Pierce, Poison, Burst }

    /// <summary>Texture-free semantic skill pictograms, shared by profile and combat UI.</summary>
    public static class SkillIconVisuals
    {
        public static SkillIconKind Resolve(string skillName, string description)
        {
            string text = (skillName ?? string.Empty) + " " + (description ?? string.Empty);
            if (text.Contains("护盾") || text.Contains("佑域") || text.Contains("壁垒")) return SkillIconKind.Shield;
            if (text.Contains("恢复") || text.Contains("治疗") || text.Contains("治愈")) return SkillIconKind.Heal;
            if (text.Contains("毒") || text.Contains("禁疗")) return SkillIconKind.Poison;
            if (text.Contains("无视") || text.Contains("穿刺") || text.Contains("穿甲")) return SkillIconKind.Pierce;
            if (text.Contains("迷心") || text.Contains("魅惑") || text.Contains("封技能")) return SkillIconKind.Charm;
            if (text.Contains("低于") || text.Contains("爆") || text.Contains("全体")) return SkillIconKind.Burst;
            return SkillIconKind.Slash;
        }

        public static SkillIconGraphic Create(Transform parent, string name, string skillName,
            string description, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(SkillIconGraphic));
            go.transform.SetParent(parent, false);
            var graphic = go.GetComponent<SkillIconGraphic>();
            graphic.Kind = Resolve(skillName, description);
            graphic.color = color;
            graphic.raycastTarget = false;
            return graphic;
        }
    }

    public sealed class SkillIconGraphic : MaskableGraphic
    {
        private SkillIconKind kind;
        public SkillIconKind Kind { get => kind; set { kind = value; SetVerticesDirty(); } }
        private Vector2 Point(float x, float y) => new Vector2(rectTransform.rect.xMin + x * rectTransform.rect.width,
            rectTransform.rect.yMin + y * rectTransform.rect.height);

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            switch (kind)
            {
                case SkillIconKind.Heal:
                    Quad(mesh, .4f, .13f, .6f, .87f); Quad(mesh, .13f, .4f, .87f, .6f);
                    break;
                case SkillIconKind.Shield:
                    Path(mesh, true, .045f, .18f,.78f, .5f,.9f, .82f,.78f, .77f,.4f, .5f,.12f, .23f,.4f);
                    Line(mesh, .5f,.25f, .5f,.75f, .045f); Line(mesh, .32f,.56f,.68f,.56f,.045f);
                    break;
                case SkillIconKind.Charm:
                    Fan(mesh, .5f,.47f, .5f,.16f, .16f,.5f, .15f,.7f, .27f,.84f,
                        .42f,.83f, .5f,.71f, .58f,.83f, .73f,.84f, .85f,.7f, .84f,.5f);
                    break;
                case SkillIconKind.Pierce:
                    Line(mesh,.16f,.2f,.76f,.8f,.1f);
                    Fan(mesh,.73f,.77f, .54f,.86f, .89f,.9f, .85f,.55f);
                    Line(mesh,.19f,.4f,.39f,.19f,.055f);
                    Line(mesh,.43f,.66f,.56f,.53f,.04f);
                    break;
                case SkillIconKind.Poison:
                    Fan(mesh,.5f,.4f, .5f,.9f, .2f,.43f, .2f,.26f, .32f,.13f,
                        .67f,.13f, .8f,.26f, .8f,.43f);
                    break;
                case SkillIconKind.Burst:
                    Fan(mesh,.5f,.5f, .5f,.96f, .6f,.66f, .86f,.84f, .69f,.58f,
                        .97f,.5f, .68f,.39f, .86f,.16f, .59f,.3f, .5f,.04f,
                        .39f,.3f, .14f,.16f, .31f,.42f, .03f,.5f, .31f,.6f, .14f,.84f, .4f,.68f);
                    break;
                default:
                    Fan(mesh,.44f,.51f, .17f,.15f, .4f,.34f, .9f,.9f, .59f,.71f);
                    Fan(mesh,.28f,.63f, .08f,.35f, .24f,.47f, .63f,.92f, .44f,.78f);
                    break;
            }
        }

        private void Quad(VertexHelper mesh, float l, float b, float r, float t) =>
            Fan(mesh, (l+r)*.5f, (b+t)*.5f, l,b, r,b, r,t, l,t);

        private void Fan(VertexHelper mesh, float cx, float cy, params float[] xy)
        {
            int first = mesh.currentVertCount;
            mesh.AddVert(Point(cx,cy), color, Vector2.zero);
            for (int i = 0; i < xy.Length; i += 2) mesh.AddVert(Point(xy[i],xy[i+1]), color, Vector2.zero);
            int count = xy.Length / 2;
            for (int i = 0; i < count; i++) mesh.AddTriangle(first, first+1+i, first+1+(i+1)%count);
        }

        private void Line(VertexHelper mesh, float ax, float ay, float bx, float by, float width)
        {
            Vector2 a = Point(ax,ay), b = Point(bx,by);
            Vector2 normal = new Vector2(-(b-a).y,(b-a).x).normalized *
                Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * width * .5f;
            int first = mesh.currentVertCount;
            mesh.AddVert(a-normal,color,Vector2.zero); mesh.AddVert(a+normal,color,Vector2.zero);
            mesh.AddVert(b+normal,color,Vector2.zero); mesh.AddVert(b-normal,color,Vector2.zero);
            mesh.AddTriangle(first,first+1,first+2); mesh.AddTriangle(first,first+2,first+3);
        }

        private void Path(VertexHelper mesh, bool closed, float width, params float[] xy)
        {
            int count = xy.Length/2;
            for (int i=0; i<(closed ? count : count-1); i++)
            { int next=(i+1)%count; Line(mesh,xy[i*2],xy[i*2+1],xy[next*2],xy[next*2+1],width); }
        }
    }
}
