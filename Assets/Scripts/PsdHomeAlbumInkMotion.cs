using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace ChoSiren
{
    // The exact 48 RGBA frames approved in ink-study R02, below an entry's artwork: the three
    // left cards are framed by it, the right-hand CTA gets it bent into a diamond.
    public sealed class PsdHomeAlbumInkMotion : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        // One copy of the four sheets serves every entry; it is released with the last entry.
        private static readonly Texture2D[] sheets = new Texture2D[4];
        private static bool loading, loaded, failed;
        private static int users;
        private static PsdHomeAlbumInkMotion loader;

        private Image fallback;
        private PsdHomeInkAtlas ink;
        private PsdHomeMotion home;
        private bool hovered;
        private float phase, impact;
        public bool Selected { get; private set; }
        public float Reveal { get; private set; }
        public bool Ready => loaded;

        /// <summary>Every entry uses the album's approved green over the original white RGBA sheets.</summary>
        public static Color InkColor(string action) => new Color(.3f, .86f, .14f);

        // Where the paint is actually visible inside the drawn frame (fractions, measured on the
        // R02 frames): the flow keeps the top and bottom bands and the right tail thin.
        private const float PaintRight = .89f, PaintTop = .135f, PaintBottom = .865f;

        /// <summary>
        /// A left-hand entry: the brush's visible paint extends just 4-5% beyond the card artwork.
        /// Keep the frame's natural transparent margins instead of clipping the brush to a box.
        /// </summary>
        public void InitializeAroundArt(RectTransform art, RectTransform root, Color tint)
        {
            const float left = -.04f, right = 1.05f, top = -.05f, bottom = 1.05f;
            Vector2 a = art.rect.size;
            float width = (right - left) * a.x / PaintRight;
            float height = (bottom - top) * a.y / (PaintBottom - PaintTop);
            Vector2 offset = art.anchoredPosition + new Vector2(left * a.x, -(top * a.y - PaintTop * height));
            Build(null, root, offset, new Vector2(width, height), false, tint);
        }

        /// <summary>
        /// The CTA restores its approved broad diamond edge; the three left cards stay restrained.
        /// </summary>
        public void InitializeBehindCallToAction(RectTransform root, Color tint)
        {
            RectTransform hit = (RectTransform)transform;
            Vector2 card = hit.rect.size;
            Build(null, root, Vector2.zero, card, false, tint);
            Vector2 P(float x, float y) => new Vector2(x * card.x, -y * card.y);
            // Restore the original CTA footprint around its centre, keeping the brush's
            // natural RGBA edge and the same album-green tint as the other entries.
            Vector2 B = P(.46f, 1.21f), R = P(1.18f, .46f), T = P(.82f, -.18f), L = P(-.26f, .54f);
            Vector2 mid = (B + R + T + L) * .25f;
            Vector2 Out(Vector2 corner) => mid + (corner - mid) * 1.3f;
            ink.SetQuad(new[] { Out(B), Out(R), Out(T), Out(L) }, new Rect(0f, 0f, 1f, 1f));
        }

        private void Build(Image source, RectTransform root, Vector2 offset, Vector2 size, bool mirror, Color tint)
        {
            fallback = source;
            if (fallback != null) fallback.enabled = false;
            home = root.GetComponent<PsdHomeMotion>();
            GameObject field = new GameObject("AlbumSelectedInk", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(PsdHomeInkAtlas));
            field.transform.SetParent(transform, false);
            field.transform.SetAsFirstSibling();
            RectTransform rect = (RectTransform)field.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.sizeDelta = size;
            rect.anchoredPosition = offset;
            if (mirror) rect.localScale = new Vector3(-1f, 1f, 1f);
            ink = field.GetComponent<PsdHomeInkAtlas>();
            ink.raycastTarget = false;
            ink.color = tint;
            users++;
            if (!loaded && !loading && !failed) { loader = this; StartCoroutine(LoadSheets()); }
        }

        private static IEnumerator LoadSheets()
        {
            loading = true;
            for (int i = 0; i < sheets.Length; i++)
            {
                string path = Application.streamingAssetsPath + "/AlbumInkR02/ink-white-" + i + ".png";
                if (!path.Contains("://")) path = "file://" + path;
                // No mip chain: four 2048×1440 RGBA sheets are ~45 MB already; mips added a third more on phones.
                var parameters = DownloadedTextureParams.Default;
                parameters.readable = false; parameters.mipmapChain = false;
                using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(path, parameters))
                {
                    request.timeout = 15;
                    yield return request.SendWebRequest();
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        Debug.LogWarning("Album ink unavailable; using authored fallback: " + request.error);
                        loading = false;
                        failed = true;
                        yield break;
                    }
                    sheets[i] = DownloadHandlerTexture.GetContent(request);
                    sheets[i].wrapMode = TextureWrapMode.Clamp;
                    sheets[i].filterMode = FilterMode.Bilinear;
                }
            }
            loading = false;
            loaded = users > 0;
            if (!loaded) Release();
        }

        public void SetSelected(bool value)
        {
            Selected = value;
            if (value) impact = 1f;
        }
        // Battle reuses this ink without a lobby (no PsdHomeMotion): never hidden.
        private bool UiHidden => home != null && home.UiHidden;
        /// <summary>Touch-only hosts (battle reroll): only Selected lights the ink.</summary>
        public bool IgnoreHover { get; set; }
        public void OnPointerEnter(PointerEventData data) { if (!UiHidden && !IgnoreHover) hovered = true; }
        public void OnPointerExit(PointerEventData data) { hovered = false; }
        private void OnApplicationFocus(bool focused) { if (!focused) hovered = false; }

        private void Update()
        {
            if (ink == null) return;
            if (UiHidden) hovered = false;
            float dt = Mathf.Min(Time.unscaledDeltaTime, .1f);
            float target = !UiHidden && (Selected || hovered) ? 1f : 0f;
            Reveal += (target - Reveal) * (1f - Mathf.Exp(-dt * (target > 0 ? 25f : 23f)));
            impact *= Mathf.Exp(-dt * 12f);
            if (Reveal > .002f) phase += dt;
            if (loaded)
            {
                int frame = (int)(phase * 30f) % 48;
                ink.Draw(sheets[frame / 12], frame % 12, Reveal, impact);
            }
            else if (fallback != null)
            {
                fallback.enabled = Reveal > .002f;
                fallback.color = new Color(1, 1, 1, Reveal);
            }
        }

        private void OnDisable() { hovered = false; Selected = false; Reveal = 0; }

        private void OnDestroy()
        {
            if (ink == null) return;
            users = Mathf.Max(0, users - 1);
            // A lobby torn down mid-download stops its coroutine; the next lobby starts over.
            if (loader == this && loading) { loading = false; Release(); }
            if (users == 0 && !loading) Release();
        }

        private static void Release()
        {
            for (int i = 0; i < sheets.Length; i++)
            {
                if (sheets[i] != null) Destroy(sheets[i]);
                sheets[i] = null;
            }
            loaded = false;
            failed = false;
        }
    }

    public sealed class PsdHomeInkAtlas : MaskableGraphic
    {
        private const int Grid = 14;
        private Texture2D sheet;
        private int frame;
        private float reveal, impact;
        private Vector2[] quad;
        private Rect crop;

        /// <summary>Draw the frame's crop region into a free quad (bl, br, tr, tl) instead of the rect.</summary>
        public void SetQuad(Vector2[] corners, Rect frameCrop)
        {
            quad = corners;
            crop = frameCrop;
            SetVerticesDirty();
        }
        public override Texture mainTexture => sheet == null ? Texture2D.whiteTexture : sheet;

        public void Draw(Texture2D texture, int index, float level, float hit)
        {
            if (sheet != texture) { sheet = texture; SetMaterialDirty(); }
            frame = index; reveal = level; impact = hit;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (sheet == null || reveal < .002f) return;
            if (quad != null) { PopulateQuad(mesh); return; }
            Rect r = rectTransform.rect;
            float sx = 1 + impact * .012f, sy = 1 + impact * .008f;
            float x0 = r.center.x - r.width * sx / 2 + impact / 640 * r.width * sx;
            float y0 = r.center.y - r.height * sy / 2 + impact / 600 * r.height * sy;
            float x1 = x0 + r.width * sx, y1 = y0 + r.height * sy;
            float left = Mathf.Max(r.xMin, x0), right = Mathf.Min(r.xMin + r.width * Mathf.Min(1, reveal * 1.18f), x1);
            float bottom = Mathf.Max(r.yMin, y0), top = Mathf.Min(r.yMax, y1);
            if (right <= left || top <= bottom) return;
            float u0 = (frame % 4) / 4f, v0 = 1f - (frame / 4 + 1) / 3f;
            Color tint = color;
            tint.a *= Mathf.Min(1, reveal * 6);
            Add(mesh, left, bottom, u0 + (left-x0)/(x1-x0)/4, v0 + (bottom-y0)/(y1-y0)/3, tint);
            Add(mesh, left, top, u0 + (left-x0)/(x1-x0)/4, v0 + (top-y0)/(y1-y0)/3, tint);
            Add(mesh, right, top, u0 + (right-x0)/(x1-x0)/4, v0 + (top-y0)/(y1-y0)/3, tint);
            Add(mesh, right, bottom, u0 + (right-x0)/(x1-x0)/4, v0 + (bottom-y0)/(y1-y0)/3, tint);
            mesh.AddTriangle(0, 1, 2); mesh.AddTriangle(2, 3, 0);
        }

        // Bilinear grid so the paint bends smoothly into a non-parallelogram diamond. The frame is
        // mirrored about the diamond's middle from its densest column outward (its left edge is a
        // hard cut with empty corners), so every visible edge is the brush's own ragged edge and
        // no corner of the diamond goes bare. The stroke bursts out from the centre to both ends.
        private const float SeamColumn = .5f;
        private void PopulateQuad(VertexHelper mesh)
        {
            float shown = Mathf.Min(1, reveal * 1.18f);
            Vector2 centre = (quad[0] + quad[1] + quad[2] + quad[3]) * .25f;
            float grow = 1 + impact * .008f;
            float fu = (frame % 4) / 4f, fv = 1f - (frame / 4 + 1) / 3f;
            Color tint = color;
            tint.a *= Mathf.Min(1, reveal * 6);
            for (int j = 0; j <= Grid; j++)
            {
                float v = j / (float)Grid;
                for (int i = 0; i <= Grid; i++)
                {
                    float u = .5f + (i / (float)Grid - .5f) * shown;
                    Vector2 p = Vector2.Lerp(Vector2.Lerp(quad[0], quad[1], u), Vector2.Lerp(quad[3], quad[2], u), v);
                    p = centre + (p - centre) * grow;
                    // Seam on the frame's densest column (paint top to bottom), tails at both ends.
                    float texU = SeamColumn + Mathf.Abs(u * 2f - 1f) * (1f - SeamColumn);
                    Add(mesh, p.x, p.y, fu + Mathf.Lerp(crop.xMin, crop.xMax, texU) / 4f,
                        fv + Mathf.Lerp(crop.yMin, crop.yMax, v) / 3f, tint);
                }
            }
            for (int j = 0; j < Grid; j++)
                for (int i = 0; i < Grid; i++)
                {
                    int a = j * (Grid + 1) + i, b = a + Grid + 1;
                    mesh.AddTriangle(a, b, b + 1);
                    mesh.AddTriangle(b + 1, a + 1, a);
                }
        }

        private static void Add(VertexHelper mesh, float x, float y, float u, float v, Color color)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = new Vector3(x, y); vertex.uv0 = new Vector2(u, v); vertex.color = color;
            mesh.AddVert(vertex);
        }
    }
}
