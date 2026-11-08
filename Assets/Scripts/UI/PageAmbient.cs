using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.UI
{
    /// <summary>
    /// Full-screen ambient layer behind the safe area. Each page (or overlay panel) registers
    /// its own art; the most recently registered live owner wins. The art is shrunk to a tiny
    /// GPU texture and stretched back up, which reads as a soft blur, then dimmed — so the
    /// notch band, home-indicator strip and side margins continue the page's own colours
    /// instead of ending in a hard edge. On screens without insets it is simply hidden behind
    /// the page.
    /// </summary>
    public sealed class PageAmbient : MonoBehaviour
    {
        private sealed class Entry
        {
            public GameObject Owner;
            public Texture Texture;
            public Color Tint;
            public float Aspect;
            public RectTransform Feather;
            public Rect Uv = new Rect(0f, 0f, 1f, 1f);
        }

        private static PageAmbient instance;
        private static readonly Dictionary<Sprite, Texture2D> BlurCache = new Dictionary<Sprite, Texture2D>();

        private readonly List<Entry> entries = new List<Entry>();
        private RawImage image;
        private AspectRatioFitter fitter;
        private Entry shown;
        private Transform safeRoot;

        /// <summary>Creates the layer under <paramref name="canvas"/>, just below <paramref name="safeRoot"/>.</summary>
        public static void Install(Transform canvas, Transform safeRoot)
        {
            if (instance != null) return;
            var node = new GameObject("PageAmbient", typeof(RectTransform), typeof(RawImage));
            node.transform.SetParent(canvas, false);
            node.transform.SetSiblingIndex(safeRoot.GetSiblingIndex());
            instance = node.AddComponent<PageAmbient>();
            instance.safeRoot = safeRoot;
            instance.image = node.GetComponent<RawImage>();
            instance.image.raycastTarget = false;
            instance.fitter = node.AddComponent<AspectRatioFitter>();
            instance.fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            instance.image.enabled = false;
            // 2026-11-01 (edge-fit): full-page backgrounds inside the safe area cover the whole screen.
            node.AddComponent<EdgeBleed1101>().Bind(safeRoot as RectTransform);
        }

        /// <summary>Shows <paramref name="art"/> (blurred) while <paramref name="owner"/> is alive and active.</summary>
        /// <remarks>
        /// With <paramref name="featherEdges"/> (an image showing the art edge to edge — a full-page
        /// board) the board is also bled to the real screen edges (<see cref="ScreenEdgeExtend.AttachBoard"/>);
        /// the feather then only softens edges that still end short of the screen (the tablet /
        /// landscape column), so on phones and the Mac window no blurred bar shows beside the page.
        /// </remarks>
        public static void Set(GameObject owner, Sprite art, float brightness = .62f, RectTransform featherEdges = null)
        {
            if (instance == null || owner == null || art == null) return;
            Texture blurred = Blur(art);
            if (blurred == null) return;
            if (featherEdges != null) ScreenEdgeExtend.AttachBoard(featherEdges, art);
            instance.entries.RemoveAll(entry => entry.Owner == owner);
            instance.entries.Add(new Entry
            {
                Owner = owner,
                Texture = blurred,
                Tint = new Color(brightness, brightness * .96f, brightness * 1.04f, 1f),
                Aspect = art.rect.width / Mathf.Max(1f, art.rect.height),
                // Feathering draws over the given art image (as its children), below later UI.
                Feather = featherEdges,
            });
            instance.Refresh();
        }

        /// <summary>
        /// 2026-11-08 (少爷: "背景没有适配好…左边空出一点"): a page whose board art is only a background shows that art itself,
        /// sharp and at full brightness, covering the whole screen (aspect kept, cropped), instead of a dimmed blur plus
        /// stretched edge strips. The caller hides its own copy of the art and keeps the board only as the layout root.
        /// </summary>
        public static void SetCover(GameObject owner, Sprite art)
        {
            if (instance == null || owner == null || art == null || art.texture == null) return;
            Rect r = art.textureRect;
            Texture texture = art.texture;
            instance.entries.RemoveAll(entry => entry.Owner == owner);
            instance.entries.Add(new Entry
            {
                Owner = owner,
                Texture = texture,
                Tint = Color.white,
                Aspect = r.width / Mathf.Max(1f, r.height),
                Uv = new Rect(r.x / texture.width, r.y / texture.height, r.width / texture.width, r.height / texture.height),
            });
            instance.shown = null;
            instance.Refresh();
        }

        /// <summary>A plain colour for owners without art (e.g. a dark page).</summary>
        public static void SetColor(GameObject owner, Color color)
        {
            if (instance == null || owner == null) return;
            instance.entries.RemoveAll(entry => entry.Owner == owner);
            instance.entries.Add(new Entry { Owner = owner, Texture = Texture2D.whiteTexture, Tint = color, Aspect = 0f });
            instance.Refresh();
        }

        private void LateUpdate() => Refresh();

        private void Refresh()
        {
            entries.RemoveAll(entry => entry.Owner == null);
            Entry top = null;
            for (int i = entries.Count - 1; i >= 0; i--)
                if (entries[i].Owner.activeInHierarchy) { top = entries[i]; break; }
            if (top == shown) { UpdateFeathers(); return; }
            shown = top;
            image.enabled = top != null;
            // Stay directly under the safe area, above full-screen art added later (lobby).
            if (safeRoot != null && transform.GetSiblingIndex() != safeRoot.GetSiblingIndex() - 1)
                transform.SetSiblingIndex(Mathf.Max(0, safeRoot.GetSiblingIndex() - (transform.GetSiblingIndex() < safeRoot.GetSiblingIndex() ? 1 : 0)));
            BuildFeathers(top);
            if (top == null) return;
            image.texture = top.Texture;
            image.uvRect = top.Uv;
            image.color = top.Tint;
            fitter.enabled = top.Aspect > 0f;
            if (top.Aspect > 0f) fitter.aspectRatio = top.Aspect;
            else
            {
                RectTransform rect = (RectTransform)transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
            }
        }

        // ------------------------------------------------------------- edge feathering

        private readonly List<FeatherQuad> feathers = new List<FeatherQuad>();

        /// <summary>
        /// Four thin quads over the page's own edges that show the ambient (same texture,
        /// same screen position) fading from opaque at the edge to clear inward, so the sharp
        /// page dissolves into its blurred surround instead of ending in a seam.
        /// </summary>
        private void BuildFeathers(Entry top)
        {
            foreach (FeatherQuad quad in feathers) if (quad != null) Destroy(quad.gameObject);
            feathers.Clear();
            if (top == null || top.Feather == null || top.Aspect <= 0f) return;
            for (int side = 0; side < 4; side++)
            {
                var node = new GameObject("AmbientFeather" + side, typeof(RectTransform), typeof(CanvasRenderer));
                node.transform.SetParent(top.Feather, false);
                FeatherQuad quad = node.AddComponent<FeatherQuad>();
                quad.Setup(side, image, top.Texture, top.Tint);
                feathers.Add(quad);
            }
            UpdateFeathers();
        }

        private void UpdateFeathers()
        {
            for (int i = 0; i < feathers.Count; i++)
                if (feathers[i] != null) feathers[i].SetVerticesDirty();
        }

        private static Color[] BoxBlur(Color[] src, int w, int h)
        {
            var dst = new Color[src.Length];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Color sum = Color.clear; int n = 0;
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int xx = Mathf.Clamp(x + dx, 0, w - 1), yy = Mathf.Clamp(y + dy, 0, h - 1);
                    sum += src[yy * w + xx]; n++;
                }
                dst[y * w + x] = sum / n;
            }
            return dst;
        }

        /// <summary>The soft blurred copy of <paramref name="art"/> used for page surrounds (cached).</summary>
        public static Texture BlurredTexture(Sprite art) => art == null ? null : Blur(art);

        private static Texture Blur(Sprite art)
        {
            if (BlurCache.TryGetValue(art, out Texture2D cached) && cached != null) return cached;
            Texture source = art.texture;
            if (source == null) return null;
            Rect r = art.textureRect;
            // Two-step shrink keeps the result smooth (one big jump would alias).
            float aspect = r.width / Mathf.Max(1f, r.height);
            int h1 = 160, w1 = Mathf.Max(8, Mathf.RoundToInt(h1 * aspect));
            int h2 = 40, w2 = Mathf.Max(4, Mathf.RoundToInt(h2 * aspect));
            var mid = RenderTexture.GetTemporary(w1, h1, 0);
            mid.filterMode = FilterMode.Bilinear;
            Vector2 scale = new Vector2(r.width / source.width, r.height / source.height);
            Vector2 offset = new Vector2(r.x / source.width, r.y / source.height);
            Graphics.Blit(source, mid, scale, offset);
            var smallRt = RenderTexture.GetTemporary(w2, h2, 0);
            Graphics.Blit(mid, smallRt);
            RenderTexture.ReleaseTemporary(mid);
            // Read back into a normal texture and box-blur it twice on the CPU (tiny image),
            // so the upscaled result is a smooth wash rather than visible blocks.
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = smallRt;
            var small = new Texture2D(w2, h2, TextureFormat.RGBA32, false) { name = "Ambient-" + art.name };
            small.ReadPixels(new Rect(0, 0, w2, h2), 0, 0, false);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(smallRt);
            Color[] pixels = small.GetPixels();
            for (int pass = 0; pass < 2; pass++) pixels = BoxBlur(pixels, w2, h2);
            small.SetPixels(pixels);
            small.filterMode = FilterMode.Bilinear;
            small.wrapMode = TextureWrapMode.Clamp;
            small.Apply(false, true);
            BlurCache[art] = small;
            return small;
        }
    }

    /// <summary>One edge band: ambient texture mapped by screen position, alpha ramps inward.</summary>
    internal sealed class FeatherQuad : MaskableGraphic
    {
        private const float Width = 48f;
        private static readonly Vector3[] Corners = new Vector3[4];
        private int side;
        private RawImage ambient;
        private Texture texture;
        private ScreenEdgeExtend bleed;

        public override Texture mainTexture => texture != null ? texture : base.mainTexture;

        public void Setup(int edge, RawImage ambientImage, Texture ambientTexture, Color tint)
        {
            side = edge;
            ambient = ambientImage;
            texture = ambientTexture;
            color = tint;
            raycastTarget = false;
            RectTransform rect = rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (ambient == null) return;
            // A board bled to the screen edge on this side needs no softening there: feathering
            // it would put a blurred band right where the page should end crisply at the edge.
            if (bleed == null) bleed = transform.parent != null ? transform.parent.GetComponentInParent<ScreenEdgeExtend>() : null;
            ScreenEdgeExtend.Side edge = side == 0 ? ScreenEdgeExtend.Side.Top : side == 1 ? ScreenEdgeExtend.Side.Bottom
                : side == 2 ? ScreenEdgeExtend.Side.Left : ScreenEdgeExtend.Side.Right;
            if (bleed != null && bleed.Covers(edge)) return;
            Rect r = rectTransform.rect;
            float w = Width;
            // Outer edge first (alpha 1), inner edge second (alpha 0).
            Vector2 o0, o1, i0, i1;
            switch (side)
            {
                case 0: o0 = new Vector2(r.xMin, r.yMax); o1 = new Vector2(r.xMax, r.yMax); i0 = o0 - new Vector2(0, w); i1 = o1 - new Vector2(0, w); break;
                case 1: o0 = new Vector2(r.xMin, r.yMin); o1 = new Vector2(r.xMax, r.yMin); i0 = o0 + new Vector2(0, w); i1 = o1 + new Vector2(0, w); break;
                case 2: o0 = new Vector2(r.xMin, r.yMin); o1 = new Vector2(r.xMin, r.yMax); i0 = o0 + new Vector2(w, 0); i1 = o1 + new Vector2(w, 0); break;
                default: o0 = new Vector2(r.xMax, r.yMin); o1 = new Vector2(r.xMax, r.yMax); i0 = o0 - new Vector2(w, 0); i1 = o1 - new Vector2(w, 0); break;
            }
            Color outer = color, inner = new Color(color.r, color.g, color.b, 0f);
            vh.AddVert(o0, outer, Uv(o0));
            vh.AddVert(o1, outer, Uv(o1));
            vh.AddVert(i1, inner, Uv(i1));
            vh.AddVert(i0, inner, Uv(i0));
            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 3, 0);
        }

        /// <summary>Where this local point falls on the ambient image, as 0..1 texture coordinates.</summary>
        private Vector2 Uv(Vector2 local)
        {
            RectTransform target = ambient.rectTransform;
            Vector3 world = rectTransform.TransformPoint(local);
            Vector2 p = target.InverseTransformPoint(world);
            Rect a = target.rect;
            return new Vector2((p.x - a.xMin) / a.width, (p.y - a.yMin) / a.height);
        }
    }
}
