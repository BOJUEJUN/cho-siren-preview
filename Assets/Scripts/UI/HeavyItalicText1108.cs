using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.UI
{
    /// <summary>
    /// 2026-11-08 (少爷: "战斗力还是没有完美复刻…字体也要像一些"): the 团队.psd power number is a heavy, strongly slanted
    /// display figure with tight spacing. The game ships one font (Noto Sans SC), so this mesh effect thickens the glyphs
    /// (eight same-colour copies around each glyph), shears them into a real italic and pulls the characters together.
    /// </summary>
    [RequireComponent(typeof(Text))]
    public sealed class HeavyItalicText1108 : BaseMeshEffect
    {
        [Range(0f, .6f)] public float Slant = .27f;      // tan of the italic angle (~15°)
        [Range(0f, .2f)] public float Weight = .028f;     // embolden radius as a share of the glyph height
        [Range(0f, .3f)] public float Tighten = .015f;    // tracking pulled in per character, share of glyph height

        static readonly List<UIVertex> Stream = new List<UIVertex>();
        static readonly List<UIVertex> Output = new List<UIVertex>();

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;
            Stream.Clear();
            vh.GetUIVertexStream(Stream);
            int count = Stream.Count;
            if (count < 6) return;
            float minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < count; i++) { float y = Stream[i].position.y; if (y < minY) minY = y; if (y > maxY) maxY = y; }
            float height = Mathf.Max(1f, maxY - minY);
            float midY = (minY + maxY) * .5f;
            int chars = count / 6;
            float pull = height * Tighten;
            for (int i = 0; i < count; i++)
            {
                UIVertex v = Stream[i];
                int c = i / 6;
                Vector3 p = v.position;
                p.x += (p.y - midY) * Slant - c * pull + (chars - 1) * pull * .5f;
                v.position = p;
                Stream[i] = v;
            }
            Output.Clear();
            float r = height * Weight;
            if (r > .01f)
            {
                for (int d = 0; d < 8; d++)
                {
                    float a = d * Mathf.PI * .25f;
                    Vector3 offset = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
                    for (int i = 0; i < count; i++) { UIVertex v = Stream[i]; v.position += offset; Output.Add(v); }
                }
            }
            Output.AddRange(Stream);
            vh.Clear();
            vh.AddUIVertexTriangleStream(Output);
        }
    }
}
