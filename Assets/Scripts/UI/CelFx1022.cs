using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>
    /// Hard-edged cel palette (research 2026-10-21 §2.2): white core → light → main → dark rim.
    /// Only the white core and the slash are additive; shards and rims use normal blending so the
    /// effect still reads on bright portraits.
    /// </summary>
    public readonly struct CelPalette
    {
        public readonly Color Light, Main, Dark;
        public CelPalette(Color32 light, Color32 main, Color32 dark) { Light = light; Main = main; Dark = dark; }

        public static readonly CelPalette Charm = new CelPalette(new Color32(255, 186, 232, 255), new Color32(255, 79, 184, 255), new Color32(110, 12, 82, 255));
        public static readonly CelPalette Demon = new CelPalette(new Color32(226, 190, 255, 255), new Color32(166, 77, 255, 255), new Color32(48, 10, 92, 255));
        public static readonly CelPalette BloodElf = new CelPalette(new Color32(255, 170, 180, 255), new Color32(255, 42, 74, 255), new Color32(84, 0, 18, 255));
        public static readonly CelPalette Mermaid = new CelPalette(new Color32(176, 242, 255, 255), new Color32(42, 184, 244, 255), new Color32(11, 58, 140, 255));
        /// <summary>Every enemy shares one hostile colour, clearly apart from the four races.</summary>
        public static readonly CelPalette Enemy = new CelPalette(new Color32(255, 196, 216, 255), new Color32(255, 61, 127, 255), new Color32(36, 6, 20, 255));
    }

    /// <summary>Procedural hard-edged shapes (no art files): four-point star, crescent slash, shard.</summary>
    public static class CelShapes
    {
        static Sprite star4, crescent, shard, halftone, jagged;

        public static Sprite Star4 => star4 != null ? star4 : star4 = Make("CelStar4", 128, 128, (u, v) =>
        {
            float d = Mathf.Sqrt(Mathf.Abs(u)) + Mathf.Sqrt(Mathf.Abs(v));
            return Mathf.Clamp01((1f - d) * 40f);
        });

        /// <summary>Crescent slash, long axis along +x, thick in the middle, sharp tips.</summary>
        public static Sprite Crescent => crescent != null ? crescent : crescent = Make("CelCrescent", 256, 96, (u, v) =>
        {
            float y = v * .375f; // aspect
            float outer = 1f - Mathf.Sqrt(u * u + (y + .62f) * (y + .62f));
            float inner = Mathf.Sqrt(u * u + (y + .9f) * (y + .9f)) - 1.02f;
            return Mathf.Clamp01(Mathf.Min(outer, inner) * 90f);
        });

        /// <summary>Long thin triangle pointing along +x.</summary>
        public static Sprite Shard => shard != null ? shard : shard = Make("CelShard", 96, 32, (u, v) =>
        {
            float half = (1f - u) * .5f; // full width at the back, a point at the front
            return Mathf.Clamp01((half - Mathf.Abs(v)) * 30f);
        });

        /// <summary>Jagged red impact star (P5), hard edge.</summary>
        public static Sprite Jagged => jagged != null ? jagged : jagged = Make("CelJagged", 192, 192, (u, v) =>
        {
            float d = Mathf.Sqrt(u * u + v * v), angle = Mathf.Atan2(v, u);
            const int spikes = 9;
            float sector = (angle / (2f * Mathf.PI) + 1f) * spikes, i = Mathf.Floor(sector), f = sector - i;
            float outer = .7f + .28f * Mathf.Abs(Mathf.Sin(i * 3.1f + .4f));
            float r = Mathf.Lerp(.36f, outer, 1f - Mathf.Abs(f * 2f - 1f));
            return Mathf.Clamp01((r - d) * 96f);
        });

        /// <summary>Tileable halftone dots for dark P5 panels.</summary>
        public static Sprite Halftone => halftone != null ? halftone : halftone = MakeTile("CelHalftone", 32, (x, y) =>
        {
            float dx = x - 16f, dy = y - 16f;
            return Mathf.Clamp01((5.5f - Mathf.Sqrt(dx * dx + dy * dy)) * .8f);
        });

        static Sprite Make(string name, int w, int h, Func<float, float, float> alpha)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
                { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + .5f) / w * 2f - 1f, v = (y + .5f) / h * 2f - 1f;
                    px[y * w + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha(u, v)) * 255f));
                }
            tex.SetPixels32(px); tex.Apply(false, true);
            var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(.5f, .5f), 100f);
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }

        static Sprite MakeTile(string name, int n, Func<float, float, float> alpha)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
                { name = name, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha(x + .5f, y + .5f)) * 255f));
            tex.SetPixels32(px); tex.Apply(false, true);
            var sprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }
    }

    /// <summary>Materials for the hard-edged cel shader (Resources/Shaders/UICelErode).</summary>
    public static class CelMaterials
    {
        static Shader shader;
        static Texture2D noise;
        static Material cutoff;

        static Shader Shader => shader != null ? shader : shader = Resources.Load<Shader>("Shaders/UICelErode");

        /// <summary>Tileable fractal value noise (R channel) for erosion.</summary>
        public static Texture2D Noise
        {
            get
            {
                if (noise != null) return noise;
                const int n = 128;
                noise = new Texture2D(n, n, TextureFormat.RGBA32, false, true)
                    { name = "CelNoise", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
                var rng = new System.Random(1022);
                float[,] Grid(int cells)
                {
                    var g = new float[cells, cells];
                    for (int y = 0; y < cells; y++) for (int x = 0; x < cells; x++) g[x, y] = (float)rng.NextDouble();
                    return g;
                }
                var octaves = new[] { (Grid(8), .55f), (Grid(16), .3f), (Grid(32), .15f) };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float v = 0f;
                        foreach (var (grid, weight) in octaves)
                        {
                            int cells = grid.GetLength(0);
                            float fx = (float)x / n * cells, fy = (float)y / n * cells;
                            int x0 = (int)fx, y0 = (int)fy, x1 = (x0 + 1) % cells, y1 = (y0 + 1) % cells;
                            float tx = Mathf.SmoothStep(0, 1, fx - x0), ty = Mathf.SmoothStep(0, 1, fy - y0);
                            v += weight * Mathf.Lerp(Mathf.Lerp(grid[x0, y0], grid[x1, y0], tx), Mathf.Lerp(grid[x0, y1], grid[x1, y1], tx), ty);
                        }
                        byte b = (byte)(Mathf.Clamp01(v) * 255f);
                        px[y * n + x] = new Color32(b, b, b, 255);
                    }
                noise.SetPixels32(px); noise.Apply(false, true);
                return noise;
            }
        }

        /// <summary>A per-effect erosion material (animate "_Erode" 0 → 1). Destroy it with its owner.</summary>
        public static Material NewErode(Color edge)
        {
            if (Shader == null || !Shader.isSupported) return null;
            var m = new Material(Shader) { name = "CelErode", hideFlags = HideFlags.DontSave };
            m.SetTexture("_NoiseTex", Noise);
            m.SetColor("_EdgeColor", edge);
            return m;
        }

        /// <summary>Shared hard-cutoff material: soft sprites become flat cel shapes.</summary>
        public static Material Cutoff
        {
            get
            {
                if (cutoff == null && Shader != null && Shader.isSupported)
                {
                    cutoff = new Material(Shader) { name = "CelCutoff", hideFlags = HideFlags.DontSave };
                    cutoff.SetTexture("_NoiseTex", Noise);
                    cutoff.SetFloat("_Cutoff", .22f);
                }
                return cutoff;
            }
        }
    }

    /// <summary>Broken shock ring drawn as a mesh: crisp at any size, thickness and gaps animate.</summary>
    public sealed class CelRingGraphic : MaskableGraphic
    {
        public float Radius = 50f, Thickness = 8f, Rotation;
        public int Gaps = 3;
        public void SetShape(float radius, float thickness, float rotation)
        {
            Radius = radius; Thickness = thickness; Rotation = rotation; SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (Radius <= 0f || Thickness <= .2f) return;
            const int segments = 54;
            Color32 c = color;
            float gapWidth = .16f; // fraction of each section left open
            for (int i = 0; i < segments; i++)
            {
                float a0 = (float)i / segments, a1 = (float)(i + 1) / segments;
                float section = a0 * Gaps;
                float within = section - Mathf.Floor(section);
                if (within > 1f - gapWidth) continue;
                // Each arc tapers toward its ends: thick in the middle of a section.
                float taper0 = Mathf.Sin(Mathf.Clamp01(within / (1f - gapWidth)) * Mathf.PI);
                float w1 = (a1 * Gaps) - Mathf.Floor(a0 * Gaps);
                float taper1 = Mathf.Sin(Mathf.Clamp01(w1 / (1f - gapWidth)) * Mathf.PI);
                float r0 = Rotation * Mathf.Deg2Rad + a0 * Mathf.PI * 2f, r1 = Rotation * Mathf.Deg2Rad + a1 * Mathf.PI * 2f;
                Vector2 d0 = new Vector2(Mathf.Cos(r0), Mathf.Sin(r0)), d1 = new Vector2(Mathf.Cos(r1), Mathf.Sin(r1));
                float t0 = Thickness * (.25f + .75f * taper0), t1 = Thickness * (.25f + .75f * taper1);
                int start = vh.currentVertCount;
                vh.AddVert(d0 * (Radius - t0 * .5f), c, Vector2.zero);
                vh.AddVert(d0 * (Radius + t0 * .5f), c, Vector2.zero);
                vh.AddVert(d1 * (Radius + t1 * .5f), c, Vector2.zero);
                vh.AddVert(d1 * (Radius - t1 * .5f), c, Vector2.zero);
                vh.AddTriangle(start, start + 1, start + 2);
                vh.AddTriangle(start, start + 2, start + 3);
            }
        }
    }

    /// <summary>
    /// The hit (research 2026-10-21 §5.1): strongest on frame 0, then gone fast. White four-point star
    /// core, a slash along the blow, a broken tapering shock ring, directional shards with dark rims.
    /// Normal ≤ .45 s, crit/skill ≤ .55 s, ultimate ≤ .7 s. Hit-stop freezes it like everything else.
    /// </summary>
    public sealed class CelImpact1022 : MonoBehaviour
    {
        const int MaxLive = 10;
        public static int Live { get; private set; }

        struct Shard { public Image Image, Rim; public Vector2 Dir; public float Dist, Len, Delay; }

        readonly List<Shard> shards = new List<Shard>();
        Image star, star2, slash, slashRim, slash2, slash2Rim, impactStar, impactStarRim;
        CelRingGraphic ring, ring2;
        CelPalette palette;
        Func<bool> paused;
        Vector2 aim;
        float clock, size, life, angle;
        int power;

        public static CelImpact1022 Play(RectTransform layer, RectTransform target, CelPalette palette, int power, Vector2 kick, Func<bool> isPaused)
        {
            if (layer == null || target == null || !Application.isPlaying || Live >= MaxLive) return null;
            var go = new GameObject("CelImpact1022", typeof(RectTransform));
            go.transform.SetParent(layer, false); go.transform.SetAsLastSibling();
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f); rect.sizeDelta = Vector2.zero;
            Vector3 world = target.TransformPoint(target.rect.center);
            rect.anchoredPosition = (Vector2)layer.InverseTransformPoint(world) - layer.rect.center;
            var fx = go.AddComponent<CelImpact1022>();
            fx.Build(palette, Mathf.Clamp(power, 0, 2), kick, isPaused);
            return fx;
        }

        void Awake() => Live++;
        void OnDestroy() => Live = Mathf.Max(0, Live - 1);

        Image New(string name, Sprite sprite, Color color, bool light)
        {
            var image = BattleFxArt1021.NewImage(name, transform, sprite, color, light);
            var c = image.color; c.a = 0f; image.color = c;
            return image;
        }

        CelRingGraphic NewRing(string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(CelRingGraphic));
            go.transform.SetParent(transform, false);
            var g = go.GetComponent<CelRingGraphic>(); g.color = color; g.raycastTarget = false;
            var r = g.rectTransform; r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f); r.sizeDelta = Vector2.one;
            return g;
        }

        void Build(CelPalette colors, int strength, Vector2 kick, Func<bool> isPaused)
        {
            palette = colors; power = strength; paused = isPaused;
            aim = kick.sqrMagnitude > .01f ? kick.normalized : Vector2.up;
            angle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            bool calm = BattlePresentationOptions.ReduceMotion;
            size = power == 2 ? 360f : power == 1 ? 260f : 190f;
            life = power == 2 ? .7f : power == 1 ? .55f : .45f;
            // Back to front: rims and rings, shards, slash, impact star, white core.
            ring = NewRing("Ring", palette.Light);
            if (power >= 1) ring2 = NewRing("Ring2", palette.Main);
            int count = calm ? 4 : power == 2 ? 15 : power == 1 ? 10 : 6;
            for (int i = 0; i < count; i++)
            {
                float spread = i % 3 == 2 ? UnityEngine.Random.Range(-2.6f, 2.6f) : UnityEngine.Random.Range(-1.05f, 1.05f);
                float a = Mathf.Atan2(aim.y, aim.x) + spread;
                float len = size * UnityEngine.Random.Range(.16f, .3f);
                shards.Add(new Shard
                {
                    Rim = New("ShardRim", CelShapes.Shard, palette.Dark, false),
                    Image = New("Shard", CelShapes.Shard, i % 2 == 0 ? palette.Main : palette.Light, false),
                    Dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a)),
                    Dist = size * UnityEngine.Random.Range(.45f, .85f), Len = len,
                    Delay = UnityEngine.Random.Range(0f, .03f),
                });
            }
            slashRim = New("SlashRim", CelShapes.Crescent, palette.Dark, false);
            slash = New("Slash", CelShapes.Crescent, palette.Light, true);
            if (power == 2)
            {
                slash2Rim = New("Slash2Rim", CelShapes.Crescent, palette.Dark, false);
                slash2 = New("Slash2", CelShapes.Crescent, palette.Light, true);
            }
            if (power >= 1)
            {
                impactStarRim = New("ImpactStarRim", CelShapes.Jagged, new Color32(10, 4, 16, 255), false);
                impactStar = New("ImpactStar", CelShapes.Jagged, palette.Main, false);
            }
            star = New("Core", CelShapes.Star4, Color.white, true);
            star2 = New("Core2", CelShapes.Star4, palette.Light, true);
            Pose();
        }

        void Update()
        {
            if (paused != null && paused()) return;
            clock += BattleHitStop.Delta(Mathf.Min(Time.unscaledDeltaTime, .05f));
            Pose();
            if (clock >= life) Destroy(gameObject);
        }

        static void Put(Graphic g, Vector2 at, Vector2 sizeDelta, float rotation, float alpha)
        {
            if (g == null) return;
            var r = g.rectTransform;
            r.anchoredPosition = at; r.sizeDelta = sizeDelta; r.localEulerAngles = new Vector3(0, 0, rotation);
            var c = g.color; c.a = Mathf.Clamp01(alpha); g.color = c;
        }

        static float Out(float t) => 1f - Mathf.Exp(-5f * Mathf.Clamp01(t));

        void Pose()
        {
            float t = clock;
            const float frame = 1f / 60f;
            // White four-point core: full on frame 0, gone by frame 3 (×1.5 longer for ultimates).
            float coreLife = frame * (power == 2 ? 6f : power == 1 ? 4f : 3f);
            float k = Mathf.Clamp01(t / coreLife);
            float coreSize = size * (power == 2 ? .8f : .58f);
            Put(star, Vector2.zero, Vector2.one * coreSize * Mathf.Lerp(1f, .35f, k), 0f, 1f - k);
            Put(star2, Vector2.zero, Vector2.one * coreSize * Mathf.Lerp(.75f, 1.15f, k), 45f, (1f - k) * .9f);
            // Slash along the blow: drawn in 3 frames, eroded by frame 10.
            PoseSlash(slash, slashRim, t, angle, 0f);
            if (slash2 != null) PoseSlash(slash2, slash2Rim, t - frame * 2f, angle + 70f, .1f);
            // P5 impact star (crit / ultimate): stamps big on frame 0, settles, snaps away.
            if (impactStar != null)
            {
                float s = Mathf.Clamp01(t / (frame * 14f));
                float pop = s < .15f ? Mathf.Lerp(1.35f, 1f, s / .15f) : 1f;
                float a = s < .7f ? 1f : 1f - (s - .7f) / .3f;
                float d = size * (power == 2 ? .9f : .7f) * pop;
                Put(impactStarRim, Vector2.zero, Vector2.one * d * 1.12f, angle * .3f, a);
                Put(impactStar, Vector2.zero, Vector2.one * d, angle * .3f, a);
            }
            // Broken shock ring: frames 1–12, thickness 14% → 1% of its size.
            PoseRing(ring, t - frame, frame * 11f, .2f, .95f);
            if (ring2 != null) PoseRing(ring2, t - frame * 5f, frame * 14f, .15f, power == 2 ? 1.35f : 1.1f);
            // Shards: 70% of the way by frame 6, shrink to nothing by frame 16.
            foreach (Shard shard in shards)
            {
                float st = t - shard.Delay;
                float travel = Out(st / (frame * 16f));
                float shrink = 1f - Mathf.Clamp01((st - frame * 6f) / (frame * 12f));
                Vector2 at = shard.Dir * shard.Dist * travel;
                float rot = Mathf.Atan2(shard.Dir.y, shard.Dir.x) * Mathf.Rad2Deg;
                float alpha = st < 0f ? 0f : shrink > 0f ? 1f : 0f;
                Vector2 dims = new Vector2(shard.Len * shrink, shard.Len * .32f * shrink);
                Put(shard.Rim, at - shard.Dir * 2f, dims * 1.25f, rot, alpha);
                Put(shard.Image, at, dims, rot, alpha);
            }
        }

        void PoseSlash(Image image, Image rim, float t, float rotation, float offset)
        {
            if (image == null) return;
            const float frame = 1f / 60f;
            float draw = Mathf.Clamp01(t / (frame * 3f));
            float erode = Mathf.Clamp01((t - frame * 4f) / (frame * 7f));
            float length = size * (power == 2 ? 1.5f : 1.2f) * (.35f + .65f * draw);
            float thick = size * .42f * (1f - erode);
            Vector2 at = -aim * size * (offset - .05f) + aim * size * .12f * draw;
            float alpha = t < 0f ? 0f : 1f - erode * erode;
            Put(rim, at, new Vector2(length * 1.06f, thick * 1.35f), rotation, alpha);
            Put(image, at, new Vector2(length, thick), rotation, alpha);
        }

        void PoseRing(CelRingGraphic g, float t, float span, float from, float to)
        {
            if (g == null) return;
            float p = Mathf.Clamp01(t / span);
            float radius = size * .5f * Mathf.Lerp(from, to, Out(p));
            float thickness = size * Mathf.Lerp(.14f, .01f, p);
            g.SetShape(radius, thickness, angle + p * 20f);
            var c = g.color; c.a = t < 0f || p >= 1f ? 0f : 1f; g.color = c;
        }
    }

    /// <summary>
    /// P5 impact frame (research §5.5), ultimate only: the screen turns white with every fighter as a
    /// black silhouette, a red jagged star and black radial lines at the hit; then one inverted
    /// frame (black, white silhouettes); then a white flash clears it. Disabled by 减弱动画.
    /// </summary>
    public sealed class ImpactFrame1022 : MonoBehaviour
    {
        Image paper, flash, star, starRim;
        readonly List<Image> silhouettes = new List<Image>();
        readonly List<Image> rays = new List<Image>();
        float clock;
        Func<bool> paused;
        const float WhitePhase = .075f, InvertPhase = .05f, FlashPhase = .12f;

        public static void Play(RectTransform layer, Vector2 hitLocal, IEnumerable<Image> figures, Func<bool> isPaused)
        {
            if (layer == null || !Application.isPlaying || BattlePresentationOptions.ReduceMotion) return;
            var go = new GameObject("ImpactFrame1022", typeof(RectTransform));
            go.transform.SetParent(layer, false); go.transform.SetAsLastSibling();
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            var fx = go.AddComponent<ImpactFrame1022>();
            fx.paused = isPaused;
            fx.Build(layer, hitLocal, figures);
        }

        Image Full(string name, Color color)
        {
            var image = BattleFxArt1021.NewImage(name, transform, null, color, false);
            var r = image.rectTransform; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
            return image;
        }

        void Build(RectTransform layer, Vector2 hit, IEnumerable<Image> figures)
        {
            paper = Full("Paper", Color.white);
            var corners = new Vector3[4];
            if (figures != null)
                foreach (Image source in figures)
                {
                    if (source == null || source.sprite == null || !source.isActiveAndEnabled) continue;
                    source.rectTransform.GetWorldCorners(corners);
                    Vector2 min = layer.InverseTransformPoint(corners[0]), max = layer.InverseTransformPoint(corners[2]);
                    var copy = BattleFxArt1021.NewImage("Silhouette", transform, source.sprite, Color.black, false);
                    copy.preserveAspect = source.preserveAspect;
                    copy.rectTransform.anchoredPosition = (min + max) * .5f - layer.rect.center;
                    copy.rectTransform.sizeDelta = new Vector2(Mathf.Abs(max.x - min.x), Mathf.Abs(max.y - min.y));
                    copy.rectTransform.localEulerAngles = source.rectTransform.eulerAngles;
                    silhouettes.Add(copy);
                }
            for (int i = 0; i < 14; i++)
            {
                var ray = BattleFxArt1021.NewImage("Ray", transform, CelShapes.Shard, Color.black, false);
                float a = i * Mathf.PI * 2f / 14f + UnityEngine.Random.Range(-.12f, .12f);
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                float len = UnityEngine.Random.Range(520f, 900f);
                ray.rectTransform.anchoredPosition = hit + dir * (120f + len * .5f);
                ray.rectTransform.sizeDelta = new Vector2(len, UnityEngine.Random.Range(22f, 46f));
                ray.rectTransform.localEulerAngles = new Vector3(0, 0, a * Mathf.Rad2Deg + 180f); // point inward
                rays.Add(ray);
            }
            starRim = BattleFxArt1021.NewImage("StarRim", transform, CelShapes.Jagged, Color.black, false);
            starRim.rectTransform.anchoredPosition = hit; starRim.rectTransform.sizeDelta = Vector2.one * 420f;
            star = BattleFxArt1021.NewImage("Star", transform, CelShapes.Jagged, Brand1025.Teal, false);
            star.rectTransform.anchoredPosition = hit; star.rectTransform.sizeDelta = Vector2.one * 360f;
            flash = Full("Flash", new Color(1, 1, 1, 0));
            Pose();
        }

        void Update()
        {
            if (paused != null && paused()) return;
            clock += Mathf.Min(Time.unscaledDeltaTime, .05f);
            Pose();
            if (clock >= WhitePhase + InvertPhase + FlashPhase) Destroy(gameObject);
        }

        void Pose()
        {
            bool white = clock < WhitePhase, invert = !white && clock < WhitePhase + InvertPhase;
            bool shown = white || invert;
            paper.color = shown ? (invert ? Color.black : Color.white) : Color.clear;
            foreach (Image s in silhouettes) s.color = shown ? (invert ? Color.white : Color.black) : Color.clear;
            foreach (Image r in rays) r.color = white ? Color.black : Color.clear;
            star.color = white ? (Color)Brand1025.Teal : Color.clear;
            starRim.color = white ? Color.black : Color.clear;
            float f = Mathf.Clamp01((clock - WhitePhase - InvertPhase) / FlashPhase);
            flash.color = new Color(1, 1, 1, shown ? 0f : .85f * (1f - f));
        }
    }

    /// <summary>Getting hit (research §5.3): the screen edge flashes red for ~6 frames.</summary>
    public sealed class EdgeFlash1022 : MonoBehaviour
    {
        static Sprite vignette;
        Image image;
        float clock;
        const float Life = .14f;

        static Sprite Vignette()
        {
            if (vignette != null) return vignette;
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + .5f) / n * 2f - 1f, v = (y + .5f) / n * 2f - 1f;
                    float edge = Mathf.Max(Mathf.Abs(u), Mathf.Abs(v));
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01((edge - .62f) / .38f) * 255f));
                }
            tex.SetPixels32(px); tex.Apply(false, true);
            vignette = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(.5f, .5f), 100f);
            vignette.hideFlags = HideFlags.DontSave;
            return vignette;
        }

        public static void Play(RectTransform layer, Color color)
        {
            if (layer == null || !Application.isPlaying || BattlePresentationOptions.ReduceMotion) return;
            Transform old = layer.Find("EdgeFlash1022");
            if (old != null) Destroy(old.gameObject);
            var go = new GameObject("EdgeFlash1022", typeof(RectTransform));
            go.transform.SetParent(layer, false); go.transform.SetAsLastSibling();
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            var fx = go.AddComponent<EdgeFlash1022>();
            fx.image = BattleFxArt1021.NewImage("Vignette", go.transform, Vignette(), color, false);
            var r = fx.image.rectTransform; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
        }

        void Update()
        {
            clock += Mathf.Min(Time.unscaledDeltaTime, .05f);
            var c = image.color; c.a = .7f * (1f - Mathf.Clamp01(clock / Life)); image.color = c;
            if (clock >= Life) Destroy(gameObject);
        }
    }

    /// <summary>
    /// Continuous ribbon trail (research §5.2: "拖尾用连续丝带不用圆点"): a strip through the last
    /// positions, full width and colour at the head, tapering to nothing at the tail.
    /// </summary>
    public sealed class RibbonTrail1022 : MaskableGraphic
    {
        public float Width = 24f;
        public int MaxPoints = 14;
        public float MinStep = 6f;
        readonly List<Vector2> points = new List<Vector2>();

        public static RibbonTrail1022 Create(Transform parent, Color color, float width, bool additive)
        {
            var go = new GameObject("Ribbon", typeof(RectTransform), typeof(CanvasRenderer), typeof(RibbonTrail1022));
            go.transform.SetParent(parent, false);
            go.transform.SetAsFirstSibling(); // behind the projectile
            var ribbon = go.GetComponent<RibbonTrail1022>();
            ribbon.color = color; ribbon.Width = width; ribbon.raycastTarget = false;
            if (additive && BattleFxArt1021.Additive != null) ribbon.material = BattleFxArt1021.Additive;
            var r = ribbon.rectTransform; r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f); r.sizeDelta = Vector2.one;
            return ribbon;
        }

        public void Push(Vector2 point)
        {
            if (points.Count > 0 && (points[points.Count - 1] - point).sqrMagnitude < MinStep * MinStep) return;
            points.Add(point);
            while (points.Count > MaxPoints) points.RemoveAt(0);
            SetVerticesDirty();
        }

        /// <summary>Shrinks the trail from the tail (call after the head stops).</summary>
        public void Drain()
        {
            if (points.Count == 0) return;
            points.RemoveAt(0);
            SetVerticesDirty();
        }

        public void Clear() { points.Clear(); SetVerticesDirty(); }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            int n = points.Count;
            if (n < 2) return;
            Color head = color;
            for (int i = 0; i < n; i++)
            {
                Vector2 p = points[i];
                Vector2 dir = i < n - 1 ? points[i + 1] - p : p - points[i - 1];
                if (dir.sqrMagnitude < .0001f) dir = Vector2.right;
                Vector2 side = new Vector2(-dir.y, dir.x).normalized;
                float k = (float)i / (n - 1); // 0 tail → 1 head
                float w = Width * .5f * k;
                Color c = head; c.a *= k * k;
                vh.AddVert(p + side * w, c, Vector2.zero);
                vh.AddVert(p - side * w, c, Vector2.zero);
                if (i > 0)
                {
                    int b = i * 2;
                    vh.AddTriangle(b - 2, b - 1, b + 1);
                    vh.AddTriangle(b - 2, b + 1, b);
                }
            }
        }
    }
}
