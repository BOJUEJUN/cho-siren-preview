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

        /// <summary>2026-11-03: member-specific skill names draw their race skill's pictogram.</summary>
        private static string RaceSkillKey(string skillName)
        {
            if (!ChoSiren.Systems.Tactics.MemberSkillNames1103.TryFind(skillName, out string memberId, out bool big)) return skillName;
            foreach (MemberDefinition member in GameModel.Members)
                if (member.Id == memberId)
                    return ChoSiren.Systems.Tactics.BattleSimulator.ActiveSkillName(
                        ChoSiren.Systems.Tactics.BattleSimulator.ParseCombatRace(member.Race), big);
            return skillName;
        }

        public static SkillIconGraphic Create(Transform parent, string name, string skillName,
            string description, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(SkillIconGraphic));
            go.transform.SetParent(parent, false);
            var graphic = go.GetComponent<SkillIconGraphic>();
            string key = RaceSkillKey(skillName);
            graphic.Kind = Resolve(key, description);
            graphic.SkillKey = key;
            graphic.color = color;
            graphic.raycastTarget = false;
            return graphic;
        }
    }

    public sealed class SkillIconGraphic : MaskableGraphic
    {
        private SkillIconKind kind;
        private string skillKey;
        public SkillIconKind Kind { get => kind; set { if (kind == value) return; kind = value; SetVerticesDirty(); } }
        public string SkillKey { get => skillKey; set { if (skillKey == value) return; skillKey = value; SetVerticesDirty(); } }
        private Vector2 Point(float x, float y) => new Vector2(rectTransform.rect.xMin + x * rectTransform.rect.width,
            rectTransform.rect.yMin + y * rectTransform.rect.height);

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (DrawNamedSkill(mesh)) return;
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

        // These are the simulator's actual skill names, not invented items or downloaded artwork.
        // Each active and captain passive has a distinct silhouette; semantic fallback remains
        // available for statuses and legacy skills which supply only Kind.
        private bool DrawNamedSkill(VertexHelper mesh)
        {
            switch (skillKey)
            {
                case "普攻":
                    Blade(mesh, .16f, .15f, .83f, .86f); break;
                case "毒蚀斩":
                    Drop(mesh, .31f, .40f, .45f); Blade(mesh, .36f, .15f, .88f, .80f); break;
                case "万毒噬体":
                    Drop(mesh, .27f, .59f, .36f); Drop(mesh, .72f, .59f, .36f); Drop(mesh, .5f, .27f, .42f); break;
                case "狐影瞬击":
                    Blade(mesh, .08f, .18f, .66f, .9f); Blade(mesh, .40f, .08f, .92f, .73f);
                    Line(mesh, .08f, .58f, .3f, .58f, .05f); break;
                case "魅语迷心":
                    Heart(mesh); Path(mesh, false, .035f, .17f,.2f, .07f,.48f, .17f,.83f);
                    Path(mesh, false, .035f, .83f,.2f, .93f,.48f, .83f,.83f); break;
                case "汐愈微光":
                    Quad(mesh, .43f, .38f, .57f, .87f); Quad(mesh, .25f, .55f, .75f, .69f);
                    Wave(mesh, .2f); Wave(mesh, .1f); break;
                case "深海佑域":
                    Path(mesh, true, .06f, .15f,.78f, .5f,.93f, .85f,.78f, .77f,.38f, .5f,.1f, .23f,.38f);
                    Wave(mesh, .5f); Wave(mesh, .35f); break;
                case "血刃穿刺":
                    Blade(mesh, .12f, .12f, .9f, .90f);
                    Path(mesh, true, .035f, .2f,.55f, .45f,.81f, .72f,.54f, .45f,.28f); break;
                case "猩红处决":
                    Blade(mesh, .13f, .14f, .85f, .85f); Blade(mesh, .84f, .14f, .15f, .85f);
                    Quad(mesh, .43f, .04f, .57f, .17f); break;
                case "音浪冲击":
                    Wave(mesh, .25f); Wave(mesh, .45f); Wave(mesh, .65f); break;
                case "舞台共鸣":
                    Path(mesh, true, .055f, .5f,.91f, .9f,.5f, .5f,.09f, .1f,.5f);
                    Quad(mesh, .44f, .29f, .56f, .71f); Quad(mesh, .29f, .44f, .71f, .56f); break;
                case "魅族指挥":
                    Dice(mesh); Line(mesh, .27f,.27f,.66f,.66f,.075f);
                    Line(mesh, .37f,.2f,.76f,.59f,.045f); break;
                case "魔族指挥":
                    Dice(mesh); Drop(mesh, .5f, .46f, .40f); break;
                case "人鱼指挥":
                    Dice(mesh); Wave(mesh, .48f); Wave(mesh, .34f); break;
                case "血精灵指挥":
                    Dice(mesh); Blade(mesh, .3f,.28f,.72f,.72f); break;
                default: return false;
            }
            return true;
        }

        private void Blade(VertexHelper mesh, float ax, float ay, float bx, float by)
        {
            Line(mesh, ax, ay, bx, by, .085f);
            Fan(mesh, bx, by, bx-.18f,by-.03f, bx,by+.09f, bx+.07f,by-.1f);
        }

        private void Drop(VertexHelper mesh, float x, float y, float size) =>
            Fan(mesh, x,y, x,y+size*.64f, x-size*.32f,y+.02f, x-size*.26f,y-size*.3f,
                x+size*.26f,y-size*.3f, x+size*.32f,y+.02f);

        private void Heart(VertexHelper mesh) =>
            Fan(mesh, .5f,.47f, .5f,.17f, .18f,.5f, .17f,.7f, .29f,.82f,
                .42f,.81f, .5f,.7f, .58f,.81f, .71f,.82f, .83f,.7f, .82f,.5f);

        private void Wave(VertexHelper mesh, float y) =>
            Path(mesh, false, .045f, .22f,y, .37f,y+.055f, .62f,y-.025f, .78f,y+.035f);

        private void Dice(VertexHelper mesh) =>
            Path(mesh, true, .055f, .5f,.94f, .89f,.51f, .5f,.06f, .11f,.51f);

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
