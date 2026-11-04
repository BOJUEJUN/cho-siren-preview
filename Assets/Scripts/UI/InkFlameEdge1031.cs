using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ChoSiren.UI
{
    /// <summary>
    /// 2026-10-31 (赵金): the 「开始演出」 green brush edge carried over to the lobby's top-right buttons and the
    /// bottom navigation — only when selected or pressed. The edge hugs the button's own silhouette: the art's
    /// alpha is baked once into a small distance field, and UIInkEdge1031.shader wraps the approved R02 brush's
    /// ragged, flowing edge around it in <see cref="PsdHomeAlbumInkMotion.InkColor"/>. Drawn behind the art, so
    /// only the part outside the silhouette shows. A steady edge costs no CPU per frame (the brush frames run on
    /// the GPU); the quad is skipped entirely while unlit.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class InkFlameEdge1031 : MaskableGraphic
    {
        private const int FieldSize = 128;
        private const float FlareSeconds = .78f;
        private static readonly Dictionary<(Sprite, int, int), Texture2D> Fields = new Dictionary<(Sprite, int, int), Texture2D>();
        private static Material shared;
        private static Shader bakeShader;
        private static bool frozen;

        private RectTransform art;
        private Vector2 artCentre;
        private Texture2D field;
        private float reach, steady, flareAt = -10f;
        private bool selected, hovered;

        /// <summary>
        /// 2026-11-03 (用户: 大厅一直在燃烧不对，要鼠标悬停才显示): lit while the pointer rests on the button, like the
        /// 「开始演出」 card; it no longer burns on the current tab or waits for a click.
        /// </summary>
        public bool Hovered
        {
            get => hovered;
            set { hovered = value; enabled = true; }
        }

        /// <summary>Lit steadily (kept for callers that pin an edge on).</summary>
        public bool Selected
        {
            get => selected;
            set { selected = value; enabled = true; }
        }

        public float Reach => reach;
        public override Texture mainTexture => field != null ? (Texture)field : Texture2D.blackTexture;

        /// <summary>
        /// Adds the edge behind <paramref name="artImage"/> (a sibling placed just before it). The padding is the
        /// longest the flame tongues may reach, as a fraction of the art's drawn size.
        /// </summary>
        public static InkFlameEdge1031 Attach(Image artImage, float padFraction)
        {
            if (artImage == null || artImage.sprite == null || artImage.transform.parent == null) return null;
            Material material = SharedMaterial;
            if (material == null) return null;
            RectTransform artRect = artImage.rectTransform;
            Rect drawn = DrawnRect(artImage);
            float pad = padFraction * (drawn.width + drawn.height) * .5f;
            Texture2D baked = Field(artImage.sprite, drawn.size, pad);
            if (baked == null) return null;

            var node = new GameObject("InkFlameEdge1031", typeof(RectTransform), typeof(CanvasRenderer), typeof(InkFlameEdge1031));
            node.layer = artImage.gameObject.layer;
            var rect = (RectTransform)node.transform;
            rect.SetParent(artRect.parent, false);
            rect.SetSiblingIndex(artRect.GetSiblingIndex());
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = drawn.size + Vector2.one * pad * 2f;
            var edge = node.GetComponent<InkFlameEdge1031>();
            edge.art = artRect;
            edge.artCentre = drawn.center;
            edge.field = baked;
            edge.material = material;
            edge.raycastTarget = false;
            edge.color = PsdHomeAlbumInkMotion.InkColor("LiveOnStage");
            edge.Follow(true);
            return edge;
        }

        /// <summary>
        /// One flare of the edge on press, and a pointer-down hook on the button so it answers the touch itself
        /// (the click may immediately rebuild or cover the page).
        /// </summary>
        public static InkFlameEdge1031 AttachToButton(Button button, Image artImage, float padFraction)
        {
            InkFlameEdge1031 edge = Attach(artImage, padFraction);
            if (edge == null || button == null) return edge;
            var trigger = UnityNull.Real(button.GetComponent<InkFlameTrigger1031>()) ?? button.gameObject.AddComponent<InkFlameTrigger1031>();
            trigger.Edge = edge;
            button.onClick.AddListener(edge.Flare);
            return edge;
        }

        /// <summary>A single burst: the edge leaps out past its rest reach and settles back.</summary>
        public void Flare()
        {
            float now = Time.unscaledTime;
            // Pointer-down already lit it; the click that follows must not restart the burst.
            if (now - flareAt < .3f) return;
            flareAt = now;
            enabled = true;
        }

        /// <summary>The selected tab lights with one burst, then holds.</summary>
        public void Ignite()
        {
            selected = true;
            steady = 0f;
            flareAt = -10f;
            Flare();
        }

        private static Material SharedMaterial
        {
            get
            {
                if (shared != null) return shared;
                if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return null;
                Shader shader = Resources.Load<Shader>("Shaders/UIInkEdge1031");
                Texture2D ink = Resources.Load<Texture2D>("Art/Lobby1031/ink-edge-strip-1031");
                if (shader == null || ink == null || !shader.isSupported) return null;
                shared = new Material(shader) { name = "UIInkEdge1031", hideFlags = HideFlags.DontSave };
                shared.SetTexture("_InkTex", ink);
                ApplyMotionSetting();
                return shared;
            }
        }

        private static void ApplyMotionSetting()
        {
            frozen = BattlePresentationOptions.ReduceMotion;
            if (shared != null) shared.SetFloat("_Frozen", frozen ? 17f : -1f);
        }

        private static Rect DrawnRect(Image image)
        {
            Rect rect = image.rectTransform.rect;
            Sprite sprite = image.sprite;
            if (!image.preserveAspect || sprite == null || sprite.rect.height <= 0f) return rect;
            float aspect = sprite.rect.width / sprite.rect.height;
            Vector2 size = rect.width / rect.height > aspect
                ? new Vector2(rect.height * aspect, rect.height) : new Vector2(rect.width, rect.width / aspect);
            return new Rect(rect.center - size * .5f, size);
        }

        /// <summary>
        /// The art's alpha (padded) as a distance field: R = 1 inside, falling to 0 at the padding; G = the
        /// mirrored position around the outline. Baked once per sprite and padding.
        /// </summary>
        private static Texture2D Field(Sprite sprite, Vector2 drawn, float pad)
        {
            Texture source = sprite.texture;
            if (source == null || drawn.x <= 0f || drawn.y <= 0f) return null;
            float quadW = drawn.x + pad * 2f, quadH = drawn.y + pad * 2f;
            var key = (sprite, Mathf.RoundToInt(pad / Mathf.Max(drawn.x, drawn.y) * 4096f), Mathf.RoundToInt(drawn.x / drawn.y * 1024f));
            if (Fields.TryGetValue(key, out Texture2D cached) && cached != null) return cached;
            if (bakeShader == null) bakeShader = Resources.Load<Shader>("Shaders/InkEdgeBake1031");
            if (bakeShader == null) return null;

            int w, h;
            if (quadW >= quadH) { w = FieldSize; h = Mathf.Max(16, Mathf.RoundToInt(FieldSize * quadH / quadW)); }
            else { h = FieldSize; w = Mathf.Max(16, Mathf.RoundToInt(FieldSize * quadW / quadH)); }
            var bake = new Material(bakeShader) { hideFlags = HideFlags.DontSave };
            Rect r = sprite.rect;
            bake.SetVector("_Src", new Vector4(r.x / source.width, r.y / source.height, r.width / source.width, r.height / source.height));
            bake.SetVector("_Inner", new Vector4(pad / quadW, pad / quadH, drawn.x / quadW, drawn.y / quadH));
            RenderTexture target = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            RenderTexture previous = RenderTexture.active;
            var read = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
            Color32[] pixels;
            try
            {
                Graphics.Blit(source, target, bake);
                RenderTexture.active = target;
                read.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
                read.Apply(false);
                pixels = read.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                Release(bake);
                Release(read);
            }

            float[] distance = DistanceToInside(pixels, w, h);
            bool[] outside = ReachableFromBorder(pixels, w, h);
            float padPixels = pad * w / quadW;
            float cx = (pad + drawn.x * .5f) * w / quadW, cy = (pad + drawn.y * .5f) * h / quadH;
            // Each button starts the brush at a different place round its outline.
            float offset = Mathf.Repeat(Fields.Count * .6180339f, 1f);
            var output = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    // Fire licks upward: below the art's middle the tongues reach about half as far, so the
                    // edge never runs down onto a caption under the icon (the nav labels).
                    float below = Mathf.Clamp01((cy - (y + .5f)) / Mathf.Max(1f, cy));
                    float inside = 1f - Mathf.Clamp01(distance[i] * Mathf.Lerp(1f, 2.1f, below) / padPixels);
                    float around = Mathf.Atan2(y + .5f - cy, x + .5f - cx) / (Mathf.PI * 2f) + .5f + offset;
                    float mirrored = Mathf.Abs(Mathf.Repeat(around, 1f) * 2f - 1f);
                    // B marks the open air round the silhouette; the art's own holes (the gear's hub) stay clean.
                    output[i] = new Color32((byte)Mathf.RoundToInt(inside * 255f), (byte)Mathf.RoundToInt(mirrored * 255f),
                        (byte)(outside[i] ? 255 : 0), 255);
                }
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false, true)
            {
                name = "InkEdgeField1031-" + sprite.name, wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave
            };
            texture.SetPixels32(output);
            texture.Apply(false, true);
            Fields[key] = texture;
            return texture;
        }

        private static void Release(Object item)
        {
            if (Application.isPlaying) Object.Destroy(item); else Object.DestroyImmediate(item);
        }

        /// <summary>Transparent texels connected to the field's border (4-neighbour flood fill).</summary>
        private static bool[] ReachableFromBorder(Color32[] pixels, int w, int h)
        {
            var open = new bool[w * h];
            var queue = new int[w * h];
            int head = 0, tail = 0;
            void Seed(int i) { if (!open[i] && pixels[i].a < 128) { open[i] = true; queue[tail++] = i; } }
            for (int x = 0; x < w; x++) { Seed(x); Seed((h - 1) * w + x); }
            for (int y = 0; y < h; y++) { Seed(y * w); Seed(y * w + w - 1); }
            while (head < tail)
            {
                int i = queue[head++], x = i % w, y = i / w;
                if (x > 0) Seed(i - 1);
                if (x < w - 1) Seed(i + 1);
                if (y > 0) Seed(i - w);
                if (y < h - 1) Seed(i + w);
            }
            return open;
        }

        /// <summary>Exact Euclidean distance (in texels) to the nearest texel at least half opaque.</summary>
        private static float[] DistanceToInside(Color32[] pixels, int w, int h)
        {
            const float Far = 1e10f;
            var grid = new float[w * h];
            for (int i = 0; i < grid.Length; i++) grid[i] = pixels[i].a >= 128 ? 0f : Far;
            int n = Mathf.Max(w, h);
            var f = new float[n]; var d = new float[n]; var z = new float[n + 1]; var v = new int[n];
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++) f[y] = grid[y * w + x];
                Transform1D(f, h, d, v, z);
                for (int y = 0; y < h; y++) grid[y * w + x] = d[y];
            }
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++) f[x] = grid[y * w + x];
                Transform1D(f, w, d, v, z);
                for (int x = 0; x < w; x++) grid[y * w + x] = Mathf.Sqrt(d[x]);
            }
            return grid;
        }

        // Felzenszwalb & Huttenlocher's lower envelope of parabolas.
        private static void Transform1D(float[] f, int n, float[] d, int[] v, float[] z)
        {
            int k = 0;
            v[0] = 0; z[0] = float.NegativeInfinity; z[1] = float.PositiveInfinity;
            for (int q = 1; q < n; q++)
            {
                float s = ((f[q] + q * q) - (f[v[k]] + v[k] * v[k])) / (2f * q - 2f * v[k]);
                while (s <= z[k])
                {
                    k--;
                    s = ((f[q] + q * q) - (f[v[k]] + v[k] * v[k])) / (2f * q - 2f * v[k]);
                }
                k++; v[k] = q; z[k] = s; z[k + 1] = float.PositiveInfinity;
            }
            k = 0;
            for (int q = 0; q < n; q++)
            {
                while (z[k + 1] < q) k++;
                float dq = q - v[k];
                d[q] = dq * dq + f[v[k]];
            }
        }

        /// <summary>Flare envelope: a fast leap past rest (1.22) in .1 s, then an ease back to nothing.</summary>
        public static float FlareEnvelope(float t)
        {
            if (t <= 0f || t >= 1f) return 0f;
            float rise = Mathf.Clamp01(t / .13f);
            float fall = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.32f, 1f, t));
            return 1.22f * (1f - (1f - rise) * (1f - rise)) * fall;
        }

        private void Update()
        {
            if (BattlePresentationOptions.ReduceMotion != frozen) ApplyMotionSetting();
            float dt = Mathf.Min(Time.unscaledDeltaTime, .1f);
            float target = selected || hovered ? 1f : 0f;
            steady = frozen ? target : Mathf.MoveTowards(steady, target, dt / .22f);
            float since = (Time.unscaledTime - flareAt) / FlareSeconds;
            // Reduced motion: the press still answers, as a brief steady edge without the leap.
            float flare = frozen ? (since >= 0f && since < .45f ? 1f : 0f) : FlareEnvelope(since);
            float next = Mathf.Max(steady, flare);
            if (Mathf.Abs(next - reach) > .002f || (next == 0f && reach != 0f))
            {
                reach = next;
                SetVerticesDirty();
            }
            // Nothing lit and nothing pending: sleep until selected or pressed again.
            if (reach == 0f && !selected && !hovered && since >= 1f) enabled = false;
        }

        private void LateUpdate()
        {
            if (reach > 0f) Follow(false);
        }

        // The edge rides along with the art's press squash/lift and the lobby's slide-in.
        private void Follow(bool force)
        {
            if (art == null) return;
            Vector3 scale = art.localScale;
            // artCentre is in the art's own rect space (relative to its pivot).
            Vector3 centre = art.localPosition + art.localRotation * Vector3.Scale(artCentre, scale);
            if (force || (rectTransform.localPosition - centre).sqrMagnitude > .0001f) rectTransform.localPosition = centre;
            if (force || (rectTransform.localScale - scale).sqrMagnitude > .000001f) rectTransform.localScale = scale;
            if (force || rectTransform.localRotation != art.localRotation) rectTransform.localRotation = art.localRotation;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (reach <= 0f || field == null) return;
            Rect r = rectTransform.rect;
            Color32 tint = color;
            tint.a = (byte)Mathf.RoundToInt(Mathf.Clamp01(reach / 1.25f) * 255f);
            vh.AddVert(new Vector3(r.xMin, r.yMin), tint, new Vector2(0f, 0f));
            vh.AddVert(new Vector3(r.xMin, r.yMax), tint, new Vector2(0f, 1f));
            vh.AddVert(new Vector3(r.xMax, r.yMax), tint, new Vector2(1f, 1f));
            vh.AddVert(new Vector3(r.xMax, r.yMin), tint, new Vector2(1f, 0f));
            vh.AddTriangle(0, 1, 2); vh.AddTriangle(2, 3, 0);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (!selected) reach = 0f;
        }
    }

    /// <summary>Lights the button's ink edge while hovered, and flares it the moment it is pressed.</summary>
    public sealed class InkFlameTrigger1031 : MonoBehaviour, IPointerDownHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public InkFlameEdge1031 Edge;

        public void OnPointerEnter(PointerEventData eventData)
        {
            var button = GetComponent<Button>();
            if (Edge != null && (button == null || button.IsInteractable())) Edge.Hovered = true;
        }

        public void OnPointerExit(PointerEventData eventData) { if (Edge != null) Edge.Hovered = false; }

        private void OnDisable() { if (Edge != null) Edge.Hovered = false; }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (Edge == null || eventData.button != PointerEventData.InputButton.Left) return;
            var button = GetComponent<Button>();
            if (button != null && !button.IsInteractable()) return;
            Edge.Flare();
        }
    }
}
