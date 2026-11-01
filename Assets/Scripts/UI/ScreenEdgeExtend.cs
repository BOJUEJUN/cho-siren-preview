using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.UI
{
    /// <summary>
    /// Fills the screen area OUTSIDE a page's art (Dynamic Island band, home-indicator strip,
    /// the side margins a fixed-aspect board leaves on a wider screen such as the ~1:2 Mac
    /// window) by continuing the art's own outermost pixels outward, so plaques, torn-paper
    /// bands and bursts that touch the art's edge reach the real screen edge. The fill joins the
    /// art seamlessly — no second copy, no box edge — while every button and label stays where
    /// it was inside the safe area. When the art already covers the screen nothing is drawn.
    /// 2026-11-01 (edge-fit): boards registered with <see cref="PageAmbient"/> are bled too, even
    /// sub-pixel margins are filled, and the strip only darkens far from the art, so narrow
    /// margins read as the art itself reaching the edge rather than a shaded gap.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScreenEdgeExtend : MonoBehaviour
    {
        public enum Side { Left, Top, Right, Bottom }

        /// <summary>Canvas units of full-strength bleed; wider margins fade to a vignette beyond it.</summary>
        private const float SolidReach = 40f;
        private static readonly Vector3[] Corners = new Vector3[4];

        private RectTransform target;
        private RectTransform art;
        private RectTransform parent;
        private RectTransform canvasRect;
        private Sprite source;
        private bool sidesInColumn = true;
        private int insetTexels;
        private readonly EdgeBleedStrip[] strips = new EdgeBleedStrip[4];
        private readonly bool[] covers = new bool[4];
        private readonly Texture2D[] bleeds = new Texture2D[4];
        private readonly float[] bleedSeam = new float[4];
        private readonly float[] bleedBuiltAt = new float[4];

        /// <summary>
        /// Extends <paramref name="target"/> (whose rect shows <paramref name="art"/> edge to edge)
        /// to the screen edges. The strips are created as siblings just below the target.
        /// </summary>
        public static ScreenEdgeExtend Attach(RectTransform target, Sprite art) => Attach(target, target, art);

        /// <summary>
        /// Like <see cref="Attach(RectTransform, Sprite)"/>, for art that is larger than the
        /// visible area (<paramref name="clip"/>): the strips repeat the art's pixels exactly at
        /// the visible edge, sampled through <paramref name="artRect"/>, so the join stays seamless.
        /// </summary>
        public static ScreenEdgeExtend Attach(RectTransform clip, RectTransform artRect, Sprite sprite)
            => Attach(clip, artRect, sprite, true);

        /// <summary>
        /// Like <see cref="Attach(RectTransform, RectTransform, Sprite)"/> for art with a transparent
        /// rim: the strips sample <paramref name="rimTexels"/> texels inside each edge and tuck that
        /// far under the art.
        /// </summary>
        public static ScreenEdgeExtend Attach(RectTransform clip, RectTransform artRect, Sprite sprite, int rimTexels)
        {
            ScreenEdgeExtend extend = Attach(clip, artRect, sprite, true);
            if (extend != null) { extend.insetTexels = Mathf.Max(0, rimTexels); extend.Fit(); }
            return extend;
        }

        /// <summary>
        /// A full-page board (registered through <see cref="PageAmbient.Set"/>): bled to the screen
        /// edges on phones and the Mac window. Its visible edge is the nearest masking ancestor when
        /// one clips it. In the landscape / tablet column the sides are left to the page ambient and
        /// its feathering (stretching a few pixels across half a monitor would smear).
        /// </summary>
        public static ScreenEdgeExtend AttachBoard(RectTransform artRect, Sprite sprite)
        {
            if (artRect == null) return null;
            RectTransform clip = artRect;
            for (Transform node = artRect.parent; node != null; node = node.parent)
            {
                if (node.GetComponent<Canvas>() != null) break;
                if (node.GetComponent<RectMask2D>() != null || node.GetComponent<Mask>() != null)
                {
                    clip = (RectTransform)node;
                    break;
                }
            }
            return Attach(clip, artRect, sprite, false);
        }

        private static ScreenEdgeExtend Attach(RectTransform clip, RectTransform artRect, Sprite sprite, bool columnSides)
        {
            if (clip == null || artRect == null || sprite == null || clip.parent == null) return null;
            ScreenEdgeExtend extend = UnityNull.Real(clip.GetComponent<ScreenEdgeExtend>()) ?? clip.gameObject.AddComponent<ScreenEdgeExtend>();
            extend.target = clip;
            extend.parent = (RectTransform)clip.parent;
            extend.sidesInColumn = columnSides;
            if (extend.source != sprite || extend.art != artRect)
            {
                extend.source = sprite;
                extend.art = artRect;
                extend.DestroyStrips();
            }
            extend.Fit();
            return extend;
        }

        /// <summary>
        /// True when the page reaches the real screen edge on <paramref name="side"/> — by itself or
        /// through a bleed strip — so nothing (e.g. the ambient feather) should soften that edge.
        /// </summary>
        public bool Covers(Side side) => isActiveAndEnabled && covers[(int)side];

        private void LateUpdate() => Fit();

        private void OnEnable() => Fit();

        private void OnDisable()
        {
            // Strips are siblings of the target: hide them with it.
            foreach (EdgeBleedStrip strip in strips)
                if (strip != null) strip.gameObject.SetActive(false);
            for (int i = 0; i < covers.Length; i++) covers[i] = false;
        }

        private void OnDestroy() => DestroyStrips();

        private void Fit()
        {
            if (target == null || parent == null || source == null || source.texture == null) return;
            if (art == null) art = target;
            if (canvasRect == null)
            {
                Canvas canvas = target.GetComponentInParent<Canvas>();
                if (canvas == null) return;
                canvasRect = (RectTransform)canvas.rootCanvas.transform;
            }
            Rect canvasLocal = canvasRect.rect;
            if (canvasLocal.width <= 1f || canvasLocal.height <= 1f) return;

            canvasRect.GetWorldCorners(Corners);
            Vector2 cMin = parent.InverseTransformPoint(Corners[0]);
            Vector2 cMax = parent.InverseTransformPoint(Corners[2]);
            if (!VisibleRect(out Rect visible, out Rect uv, out Rect artInParent)) return;
            // Parent units per canvas unit, so reach and vignette are judged on screen.
            float unit = Mathf.Max(.0001f, (cMax.x - cMin.x) / canvasLocal.width);
            // Seal: the strips tuck one canvas unit under the art (plus any transparent rim the
            // art has, e.g. the lobby backdrop's 11-texel left margin) and sample just inside it,
            // so pixel snapping or a soft rim never lets a hairline of the backdrop through.
            Seal(ref visible, ref uv, unit);

            bool column = canvasLocal.width / canvasLocal.height > SafeAreaFitter.WideAspect;
            bool sides = sidesInColumn || !column;
            float gapLeft = visible.xMin - cMin.x, gapRight = cMax.x - visible.xMax;
            float gapTop = cMax.y - visible.yMax, gapBottom = visible.yMin - cMin.y;
            // Even a sub-pixel margin leaves an anti-aliased line of whatever is behind; strips
            // also run a little past the screen edge so the outermost pixel row is always art.
            float none = .01f * unit, overshoot = 2f * unit;
            bool left = sides && gapLeft > none, right = sides && gapRight > none;
            bool top = gapTop > none, bottom = gapBottom > none;
            covers[(int)Side.Left] = left || gapLeft <= none;
            covers[(int)Side.Right] = right || gapRight <= none;
            covers[(int)Side.Top] = true;
            covers[(int)Side.Bottom] = true;
            cMin -= new Vector2(left ? overshoot : 0f, bottom ? overshoot : 0f);
            cMax += new Vector2(right ? overshoot : 0f, top ? overshoot : 0f);

            Place(Side.Left, left, Rect.MinMaxRect(cMin.x, visible.yMin, visible.xMin, visible.yMax), visible, uv, artInParent, unit);
            Place(Side.Right, right, Rect.MinMaxRect(visible.xMax, visible.yMin, cMax.x, visible.yMax), visible, uv, artInParent, unit);
            float x0 = left ? cMin.x : visible.xMin, x1 = right ? cMax.x : visible.xMax;
            Place(Side.Top, top, Rect.MinMaxRect(x0, visible.yMax, x1, cMax.y), visible, uv, artInParent, unit);
            Place(Side.Bottom, bottom, Rect.MinMaxRect(x0, cMin.y, x1, visible.yMin), visible, uv, artInParent, unit);
        }

        private void Seal(ref Rect visible, ref Rect uv, float unit)
        {
            float texW = source.texture.width, texH = source.texture.height;
            // Parent units per texel on each axis.
            float px = visible.width / Mathf.Max(.0001f, uv.width * texW);
            float py = visible.height / Mathf.Max(.0001f, uv.height * texH);
            float sx = Mathf.Min(unit + insetTexels * px, visible.width * .25f);
            float sy = Mathf.Min(unit + insetTexels * py, visible.height * .25f);
            float du = sx / visible.width * uv.width, dv = sy / visible.height * uv.height;
            visible = Rect.MinMaxRect(visible.xMin + sx, visible.yMin + sy, visible.xMax - sx, visible.yMax - sy);
            uv = Rect.MinMaxRect(uv.xMin + du, uv.yMin + dv, uv.xMax - du, uv.yMax - dv);
        }

        /// <summary>
        /// The part of the art actually on show, in parent space, and the matching texture
        /// coordinates (0..1 of the whole texture). A preserve-aspect image only paints a fitted
        /// sub-rect; a masking clip may cut the art further.
        /// </summary>
        private bool VisibleRect(out Rect visible, out Rect uv, out Rect artInParent)
        {
            visible = uv = artInParent = default;
            Rect drawn = art.rect;
            if (art.TryGetComponent(out Image image) && image.preserveAspect && image.type == Image.Type.Simple && image.sprite != null)
            {
                float spriteAspect = image.sprite.rect.width / Mathf.Max(1f, image.sprite.rect.height);
                float rectAspect = drawn.width / Mathf.Max(.0001f, drawn.height);
                Vector2 size = rectAspect > spriteAspect
                    ? new Vector2(drawn.height * spriteAspect, drawn.height)
                    : new Vector2(drawn.width, drawn.width / Mathf.Max(.0001f, spriteAspect));
                Vector2 pivot = art.pivot;
                drawn = new Rect(drawn.x + (drawn.width - size.x) * pivot.x, drawn.y + (drawn.height - size.y) * pivot.y, size.x, size.y);
            }
            if (drawn.width <= 0f || drawn.height <= 0f) return false;

            artInParent = ToParent(art, drawn);
            visible = artInParent;
            if (target != art)
            {
                Rect clip = ToParent(target, target.rect);
                visible = Rect.MinMaxRect(Mathf.Max(visible.xMin, clip.xMin), Mathf.Max(visible.yMin, clip.yMin),
                    Mathf.Min(visible.xMax, clip.xMax), Mathf.Min(visible.yMax, clip.yMax));
            }
            if (visible.width <= .5f || visible.height <= .5f) return false;

            float u0 = Mathf.Clamp01((visible.xMin - artInParent.xMin) / artInParent.width);
            float u1 = Mathf.Clamp01((visible.xMax - artInParent.xMin) / artInParent.width);
            float v0 = Mathf.Clamp01((visible.yMin - artInParent.yMin) / artInParent.height);
            float v1 = Mathf.Clamp01((visible.yMax - artInParent.yMin) / artInParent.height);
            Rect r = source.textureRect;
            float tw = source.texture.width, th = source.texture.height;
            uv = Rect.MinMaxRect((r.x + u0 * r.width) / tw, (r.y + v0 * r.height) / th,
                (r.x + u1 * r.width) / tw, (r.y + v1 * r.height) / th);
            return true;
        }

        private Rect ToParent(RectTransform node, Rect local)
        {
            Vector2 a = parent.InverseTransformPoint(node.TransformPoint(new Vector3(local.xMin, local.yMin)));
            Vector2 b = parent.InverseTransformPoint(node.TransformPoint(new Vector3(local.xMax, local.yMax)));
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        private void Place(Side side, bool needed, Rect region, Rect visible, Rect uv, Rect artInParent, float unit)
        {
            int index = (int)side;
            EdgeBleedStrip strip = strips[index];
            if (!needed || region.width <= .01f || region.height <= .01f)
            {
                if (strip != null && strip.gameObject.activeSelf) strip.gameObject.SetActive(false);
                return;
            }
            bool horizontal = side == Side.Left || side == Side.Right;
            float texels = horizontal ? source.texture.width : source.texture.height;
            float seamT = side == Side.Left ? uv.xMin : side == Side.Right ? uv.xMax : side == Side.Top ? uv.yMax : uv.yMin;
            seamT += (side == Side.Left || side == Side.Bottom ? .5f : -.5f) / texels;
            // Rebuild the bleed texture only when the seam moves across texels (parallax drifts it a
            // little; rebuilding every frame would read back from the GPU every frame).
            if (bleeds[index] == null || (Mathf.Abs(bleedSeam[index] - seamT) * texels > 2f && Time.unscaledTime >= bleedBuiltAt[index] + .25f))
            {
                Texture2D built = BuildBleed(horizontal, seamT);
                if (built == null) return;
                if (bleeds[index] != null) Destroy(bleeds[index]);
                bleeds[index] = built;
                bleedSeam[index] = seamT;
                bleedBuiltAt[index] = Time.unscaledTime;
            }
            if (strip == null) strip = strips[index] = CreateStrip(side);
            if (!strip.gameObject.activeSelf) strip.gameObject.SetActive(true);
            int targetIndex = target.GetSiblingIndex();
            if (strip.transform.GetSiblingIndex() > targetIndex) strip.transform.SetSiblingIndex(targetIndex);
            RectTransform rect = strip.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            Vector2 position = region.center - parent.rect.center;
            if ((rect.anchoredPosition - position).sqrMagnitude > .0001f) rect.anchoredPosition = position;
            if ((rect.sizeDelta - region.size).sqrMagnitude > .0001f) rect.sizeDelta = region.size;
            strip.Configure(side, bleeds[index], region, visible, artInParent, unit, SolidReach);
        }

        /// <summary>
        /// The bleed texture for one side: across (x for left/right, y for top/bottom) column 0 is
        /// the art's own texel line at the seam, crisp; further columns blur it more and more along
        /// the edge, so the continuation softens into the art's colours instead of smearing its
        /// halftone dots and hairlines into barcode streaks. Along covers the whole sprite edge.
        /// </summary>
        private Texture2D BuildBleed(bool horizontal, float seamT)
        {
            Texture src = source.texture;
            Rect r = source.textureRect;
            float texW = src.width, texH = src.height;
            int length = Mathf.Clamp(Mathf.RoundToInt(horizontal ? r.height : r.width), 8, 1024);
            RenderTexture rt = RenderTexture.GetTemporary(horizontal ? 1 : length, horizontal ? length : 1, 0);
            rt.filterMode = FilterMode.Bilinear;
            Vector2 scale = horizontal ? new Vector2(1f / texW, r.height / texH) : new Vector2(r.width / texW, 1f / texH);
            Vector2 offset = horizontal ? new Vector2(seamT - .5f / texW, r.y / texH) : new Vector2(r.x / texW, seamT - .5f / texH);
            Graphics.Blit(src, rt, scale, offset);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var line = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            line.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0, false);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            Color[] edge = line.GetPixels();
            Destroy(line);

            const int across = 24;
            var prefix = new Color[length + 1];
            for (int i = 0; i < length; i++) prefix[i + 1] = prefix[i] + edge[i];
            float maxRadius = Mathf.Max(2f, length / 32f);
            var pixels = new Color[across * length];
            for (int k = 0; k < across; k++)
            {
                float t = k / (across - 1f);
                int radius = Mathf.RoundToInt(maxRadius * Mathf.Pow(t, 1.4f));
                for (int i = 0; i < length; i++)
                {
                    int a = Mathf.Max(0, i - radius), b = Mathf.Min(length - 1, i + radius);
                    Color c = (prefix[b + 1] - prefix[a]) / (b - a + 1);
                    if (horizontal) pixels[i * across + k] = c; else pixels[k * length + i] = c;
                }
            }
            var bleed = new Texture2D(horizontal ? across : length, horizontal ? length : across, TextureFormat.RGBA32, false)
            {
                name = "EdgeBleed-" + source.name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };
            bleed.SetPixels(pixels);
            bleed.Apply(false, true);
            return bleed;
        }

        private EdgeBleedStrip CreateStrip(Side side)
        {
            var node = new GameObject("EdgeExtend" + side, typeof(RectTransform), typeof(CanvasRenderer));
            node.transform.SetParent(parent, false);
            node.transform.SetSiblingIndex(target.GetSiblingIndex());
            EdgeBleedStrip strip = node.AddComponent<EdgeBleedStrip>();
            strip.raycastTarget = false;
            return strip;
        }

        private void DestroyStrips()
        {
            for (int i = 0; i < strips.Length; i++)
            {
                if (strips[i] != null) Destroy(strips[i].gameObject);
                if (bleeds[i] != null) Destroy(bleeds[i]);
                strips[i] = null;
                bleeds[i] = null;
                covers[i] = false;
            }
        }
    }

    /// <summary>
    /// One bleed strip: the side's bleed texture (crisp art edge at the seam, softening outward)
    /// laid from the seam to the screen edge, following the art along the edge. Within
    /// <c>SolidReach</c> canvas units it stays at full strength; wider margins darken toward
    /// the screen edge like a vignette.
    /// </summary>
    internal sealed class EdgeBleedStrip : MaskableGraphic
    {
        private static readonly Color Vignette = new Color(.03f, .01f, .08f, 1f);
        private Texture texture;
        private ScreenEdgeExtend.Side side;
        private Rect region, visible, art;
        private float unit, solidReach;

        public override Texture mainTexture => texture != null ? texture : base.mainTexture;

        public void Configure(ScreenEdgeExtend.Side edge, Texture bleed, Rect stripRegion, Rect visibleArt, Rect artRect,
            float parentUnitsPerCanvasUnit, float solid)
        {
            if (texture == bleed && side == edge && Same(region, stripRegion) && Same(visible, visibleArt) && Same(art, artRect)
                && Mathf.Abs(unit - parentUnitsPerCanvasUnit) < .0001f) return;
            texture = bleed; side = edge; region = stripRegion; visible = visibleArt; art = artRect;
            unit = parentUnitsPerCanvasUnit; solidReach = solid;
            SetMaterialDirty();
            SetVerticesDirty();
        }

        private static bool Same(Rect a, Rect b) =>
            Mathf.Abs(a.xMin - b.xMin) < .01f && Mathf.Abs(a.yMin - b.yMin) < .01f &&
            Mathf.Abs(a.xMax - b.xMax) < .01f && Mathf.Abs(a.yMax - b.yMax) < .01f;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (texture == null || region.width <= 0f || region.height <= 0f || visible.width <= 0f || art.width <= 0f || art.height <= 0f) return;
            bool horizontal = side == ScreenEdgeExtend.Side.Left || side == ScreenEdgeExtend.Side.Right;
            float gap = horizontal ? region.width : region.height;
            float gapCanvas = gap / unit;
            float shade = Mathf.Clamp01((gapCanvas - solidReach) / 120f) * .72f;
            Color outer = Color.Lerp(Color.white, Vignette, shade);
            float solid = Mathf.Min(gap, solidReach * unit);
            // Blur grows with the distance on screen: full softness about 70 canvas units out.
            float acrossTexels = horizontal ? texture.width : texture.height;
            float seamT = .5f / acrossTexels, softT = 1f - .5f / acrossTexels;
            float AcrossT(float distance) => Mathf.Lerp(seamT, softT, Mathf.Clamp01(distance / (70f * unit)));

            // Distances from the seam at which vertex rows sit, and their colours.
            var rows = new System.Collections.Generic.List<float> { 0f };
            if (solid < gap - .01f) rows.Add(solid);
            if (70f * unit < gap - .01f && 70f * unit > solid + .01f) { rows.Add(70f * unit); rows.Sort(); }
            rows.Add(gap);
            Color RowColour(float distance) => distance <= solid ? Color.white
                : Color.Lerp(Color.white, outer, (distance - solid) / Mathf.Max(.0001f, gap - solid));

            // Along the edge: the bleed texture spans the whole art edge; beyond the visible
            // stretch (corners) it holds the last visible texel.
            float along0 = horizontal ? region.yMin : region.xMin, along1 = horizontal ? region.yMax : region.xMax;
            float vis0 = horizontal ? visible.yMin : visible.xMin, vis1 = horizontal ? visible.yMax : visible.xMax;
            float art0 = horizontal ? art.yMin : art.xMin, art1 = horizontal ? art.yMax : art.xMax;
            float AlongT(float p) => Mathf.Clamp01((Mathf.Clamp(p, vis0, vis1) - art0) / (art1 - art0));
            var cuts = new System.Collections.Generic.List<float> { along0 };
            if (vis0 > along0 + .01f && vis0 < along1 - .01f) cuts.Add(vis0);
            if (vis1 > along0 + .01f && vis1 < along1 - .01f) cuts.Add(vis1);
            cuts.Add(along1);

            float seam, outward;
            switch (side)
            {
                case ScreenEdgeExtend.Side.Left: seam = visible.xMin; outward = -1f; break;
                case ScreenEdgeExtend.Side.Right: seam = visible.xMax; outward = 1f; break;
                case ScreenEdgeExtend.Side.Top: seam = visible.yMax; outward = 1f; break;
                default: seam = visible.yMin; outward = -1f; break;
            }
            Vector2 centre = region.center;
            int columns = cuts.Count;
            for (int row = 0; row < rows.Count; row++)
            {
                float across = seam + outward * rows[row];
                float tex = AcrossT(rows[row]);
                Color c = RowColour(rows[row]) * color;
                for (int col = 0; col < columns; col++)
                {
                    float along = cuts[col];
                    Vector2 p = horizontal ? new Vector2(across, along) : new Vector2(along, across);
                    Vector2 t = horizontal ? new Vector2(tex, AlongT(along)) : new Vector2(AlongT(along), tex);
                    vh.AddVert(p - centre, c, t);
                }
            }
            for (int row = 0; row < rows.Count - 1; row++)
            for (int col = 0; col < columns - 1; col++)
            {
                int i = row * columns + col;
                vh.AddTriangle(i, i + 1, i + 1 + columns);
                vh.AddTriangle(i + 1 + columns, i + columns, i);
            }
        }
    }
}
