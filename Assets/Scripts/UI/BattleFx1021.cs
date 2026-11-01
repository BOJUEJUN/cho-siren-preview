using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>
    /// Hit-stop (2026-10-21 VFX pass): on a landed hit the battle presentation holds its pose for a
    /// few frames so the blow reads as heavy. Only the presentation clock stops; the model is untouched.
    /// </summary>
    public static class BattleHitStop
    {
        static float until;
        public static void Freeze(float seconds)
        {
            if (!Application.isPlaying || BattlePresentationOptions.ReduceMotion || seconds <= 0f) return;
            until = Mathf.Max(until, Time.unscaledTime + Mathf.Min(seconds, .2f));
        }
        public static bool Frozen => Application.isPlaying && Time.unscaledTime < until;
        public static float Delta(float seconds) => Frozen ? 0f : seconds;
        public static void Clear() => until = 0f;
    }

    /// <summary>Shared light material and generated effect shapes (no art files needed).</summary>
    public static class BattleFxArt1021
    {
        static Material additive;
        static Sprite dot, ring, spark, star;

        /// <summary>Additive UI material: overlapping light gets brighter instead of muddier.</summary>
        public static Material Additive
        {
            get
            {
                if (additive == null)
                {
                    Shader shader = Resources.Load<Shader>("Shaders/UIAdditive");
                    if (shader != null && shader.isSupported)
                        additive = new Material(shader) { name = "UIAdditive", hideFlags = HideFlags.DontSave };
                }
                return additive;
            }
        }

        public static Sprite Dot => dot != null ? dot : dot = Make("FxDot", 96, 96, (u, v) =>
        {
            float d = Mathf.Sqrt(u * u + v * v);
            return Mathf.Max(Mathf.Pow(Mathf.Clamp01(1f - d), 2.2f), Mathf.Clamp01(1f - d / .28f) * .9f);
        });

        public static Sprite Ring => ring != null ? ring : ring = Make("FxRing", 160, 160, (u, v) =>
        {
            float d = Mathf.Sqrt(u * u + v * v);
            float k = (d - .84f) / .055f, soft = (d - .8f) / .2f;
            return Mathf.Clamp01(Mathf.Exp(-k * k) + .3f * Mathf.Exp(-soft * soft));
        });

        /// <summary>Long thin diamond with a hot centre, pointing along +x.</summary>
        public static Sprite Spark => spark != null ? spark : spark = Make("FxSpark", 128, 32, (u, v) =>
        {
            float w = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(u)), 1.15f);
            float a = Mathf.Clamp01(1f - Mathf.Abs(v) / Mathf.Max(w, .001f));
            return a * a;
        });

        /// <summary>P5 / manga impact star: hard edge, irregular spikes.</summary>
        public static Sprite Star => star != null ? star : star = Make("FxStar", 192, 192, (u, v) =>
        {
            float d = Mathf.Sqrt(u * u + v * v), angle = Mathf.Atan2(v, u);
            const int spikes = 11;
            float sector = (angle / (2f * Mathf.PI) + 1f) * spikes, i = Mathf.Floor(sector), f = sector - i;
            float outer = .72f + .26f * Mathf.Abs(Mathf.Sin(i * 2.39f + 1.1f));
            float r = Mathf.Lerp(.42f, outer, 1f - Mathf.Abs(f * 2f - 1f));
            return Mathf.Clamp01((r - d) * 96f);
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

        public static Image NewImage(string name, Transform parent, Sprite sprite, Color color, bool light)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite; image.color = color; image.raycastTarget = false;
            if (light && Additive != null) image.material = Additive;
            var rect = image.rectTransform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            return image;
        }

        public static float EaseOut(float t) { t = Mathf.Clamp01(t); return 1f - (1f - t) * (1f - t) * (1f - t); }
    }

    /// <summary>
    /// Layered hit (2026-10-21, owner: "特效太低级"): white core flash → coloured glow → shock ring →
    /// radial sparks → (crit/ultimate) black-edged P5 impact star and drifting embers.
    /// Power 0 = ordinary hit, 1 = critical / skill, 2 = ultimate.
    /// </summary>
    public sealed class ImpactBurst1021 : MonoBehaviour
    {
        public const float Life = .8f;
        const int MaxLive = 10;
        public static int Live { get; private set; }

        struct Piece { public Image image; public Vector2 from, dir; public float dist, len, width, life, delay, sway; }
        readonly List<Piece> sparks = new List<Piece>(), embers = new List<Piece>();
        Image core, glow, ring, ring2, starBack, starFront;
        Func<bool> paused;
        float clock, size, spin;
        Color tint;

        public static ImpactBurst1021 Play(RectTransform layer, RectTransform target, Color tint, int power, Vector2 kick, Func<bool> isPaused)
        {
            if (layer == null || target == null || !Application.isPlaying || Live >= MaxLive) return null;
            power = Mathf.Clamp(power, 0, 2);
            var go = new GameObject("ImpactBurst1021", typeof(RectTransform));
            go.transform.SetParent(layer, false); go.transform.SetAsLastSibling();
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f); rect.sizeDelta = Vector2.zero;
            Vector3 world = target.TransformPoint(target.rect.center);
            rect.anchoredPosition = (Vector2)layer.InverseTransformPoint(world) - layer.rect.center;
            var fx = go.AddComponent<ImpactBurst1021>();
            fx.Build(tint, power, kick, isPaused);
            return fx;
        }

        void Awake() => Live++;
        void OnDestroy() => Live = Mathf.Max(0, Live - 1);

        void Build(Color color, int power, Vector2 kick, Func<bool> isPaused)
        {
            paused = isPaused; tint = color; tint.a = 1f;
            bool calm = BattlePresentationOptions.ReduceMotion;
            size = power == 2 ? 330f : power == 1 ? 240f : 160f;
            spin = UnityEngine.Random.Range(-25f, 25f);
            Color light = Color.Lerp(tint, Color.white, .45f);
            if (power >= 1)
            {
                starBack = BattleFxArt1021.NewImage("StarEdge", transform, BattleFxArt1021.Star, new Color32(10, 6, 18, 255), false);
                starFront = BattleFxArt1021.NewImage("Star", transform, BattleFxArt1021.Star, light, false);
            }
            glow = BattleFxArt1021.NewImage("Glow", transform, BattleFxArt1021.Dot, tint, true);
            ring = BattleFxArt1021.NewImage("Ring", transform, BattleFxArt1021.Ring, light, true);
            if (power >= 1) ring2 = BattleFxArt1021.NewImage("Ring2", transform, BattleFxArt1021.Ring, tint, true);
            Vector2 aim = kick.sqrMagnitude > .01f ? kick.normalized : UnityEngine.Random.insideUnitCircle.normalized;
            int count = calm ? 4 : power == 2 ? 16 : power == 1 ? 11 : 7;
            for (int i = 0; i < count; i++)
            {
                // Half the sparks spray on through the target along the blow; the rest scatter.
                float baseAngle = i % 2 == 0 ? Mathf.Atan2(aim.y, aim.x) + UnityEngine.Random.Range(-.7f, .7f)
                    : UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                var dir = new Vector2(Mathf.Cos(baseAngle), Mathf.Sin(baseAngle));
                float len = size * UnityEngine.Random.Range(.22f, .46f);
                sparks.Add(new Piece
                {
                    image = BattleFxArt1021.NewImage("Spark", transform, BattleFxArt1021.Spark, i % 3 == 0 ? Color.white : light, true),
                    dir = dir, dist = size * UnityEngine.Random.Range(.42f, .95f), len = len, width = len * .2f,
                    life = UnityEngine.Random.Range(.2f, .36f), delay = UnityEngine.Random.Range(0f, .04f)
                });
            }
            if (power >= 1 && !calm)
                for (int i = 0; i < (power == 2 ? 9 : 5); i++)
                    embers.Add(new Piece
                    {
                        image = BattleFxArt1021.NewImage("Ember", transform, BattleFxArt1021.Dot, light, true),
                        from = UnityEngine.Random.insideUnitCircle * size * .3f,
                        dist = UnityEngine.Random.Range(40f, 100f), width = UnityEngine.Random.Range(9f, 17f),
                        life = UnityEngine.Random.Range(.45f, .7f), delay = UnityEngine.Random.Range(.04f, .14f),
                        sway = UnityEngine.Random.Range(0f, 6.28f)
                    });
            core = BattleFxArt1021.NewImage("Core", transform, BattleFxArt1021.Dot, Color.white, true);
            Pose();
        }

        void Update()
        {
            if (paused != null && paused()) return;
            clock += BattleHitStop.Delta(Mathf.Min(Time.unscaledDeltaTime, .05f));
            Pose();
            if (clock >= Life) Destroy(gameObject);
        }

        void Pose()
        {
            float t = clock;
            Set(core, size * Mathf.Lerp(.5f, 1.15f, BattleFxArt1021.EaseOut(t / .14f)), 1f - Mathf.Clamp01(t / .14f), Color.white);
            Set(glow, size * Mathf.Lerp(.7f, 1.45f, BattleFxArt1021.EaseOut(t / .3f)), .9f * (1f - Mathf.Clamp01(t / .3f)), tint);
            float p = Mathf.Clamp01(t / .34f);
            Set(ring, size * Mathf.Lerp(.25f, 1.45f, BattleFxArt1021.EaseOut(p)), Mathf.Pow(1f - p, 1.4f), ring.color);
            if (ring2 != null)
            {
                float q = Mathf.Clamp01((t - .07f) / .42f);
                Set(ring2, size * Mathf.Lerp(.2f, 1.9f, BattleFxArt1021.EaseOut(q)), t < .07f ? 0f : .8f * Mathf.Pow(1f - q, 1.6f), ring2.color);
            }
            if (starFront != null)
            {
                float s = Mathf.Clamp01(t / .22f);
                float pop = s < .2f ? Mathf.Lerp(1.45f, 1f, s / .2f) : Mathf.Lerp(1f, .82f, (s - .2f) / .8f);
                float a = 1f - Mathf.Clamp01((s - .6f) / .4f);
                Set(starBack, size * .98f * pop, a, starBack.color); starBack.rectTransform.localEulerAngles = new Vector3(0, 0, spin + s * 6f);
                Set(starFront, size * .86f * pop, a, starFront.color); starFront.rectTransform.localEulerAngles = new Vector3(0, 0, spin + s * 6f);
            }
            foreach (var spark in sparks)
            {
                float k = Mathf.Clamp01((t - spark.delay) / spark.life);
                var r = spark.image.rectTransform;
                r.anchoredPosition = spark.dir * spark.dist * BattleFxArt1021.EaseOut(k);
                r.sizeDelta = new Vector2(spark.len * (1f - .55f * k), spark.width * (1f - .7f * k));
                r.localEulerAngles = new Vector3(0, 0, Mathf.Atan2(spark.dir.y, spark.dir.x) * Mathf.Rad2Deg);
                Color c = spark.image.color; c.a = t < spark.delay ? 0f : 1f - k * k; spark.image.color = c;
            }
            foreach (var ember in embers)
            {
                float k = Mathf.Clamp01((t - ember.delay) / ember.life);
                var r = ember.image.rectTransform;
                r.anchoredPosition = ember.from + new Vector2(Mathf.Sin(ember.sway + k * 5f) * 10f, ember.dist * BattleFxArt1021.EaseOut(k));
                r.sizeDelta = Vector2.one * ember.width * (1f - .5f * k);
                Color c = ember.image.color; c.a = t < ember.delay ? 0f : Mathf.Sin(k * Mathf.PI) * .95f; ember.image.color = c;
            }
        }

        static void Set(Image image, float diameter, float alpha, Color color)
        {
            if (image == null) return;
            image.rectTransform.sizeDelta = Vector2.one * Mathf.Max(0f, diameter);
            color.a = Mathf.Clamp01(alpha); image.color = color;
        }
    }

    /// <summary>
    /// Ultimate impact frame (2026-10-21): one beat of dark, then a full-screen flash in the
    /// caster's colour that fades out — the "the whole stage got hit" moment.
    /// </summary>
    public sealed class ScreenFlash1021 : MonoBehaviour
    {
        Image dark, light;
        Func<bool> paused;
        float clock;

        public static void Play(RectTransform layer, Color tint, Func<bool> isPaused)
        {
            if (layer == null || !Application.isPlaying || BattlePresentationOptions.ReduceMotion) return;
            var go = new GameObject("ScreenFlash1021", typeof(RectTransform));
            go.transform.SetParent(layer, false); go.transform.SetAsLastSibling();
            var rect = (RectTransform)go.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var fx = go.AddComponent<ScreenFlash1021>();
            fx.paused = isPaused;
            fx.dark = Full(go.transform, new Color(.02f, 0f, .05f, .45f), false);
            tint.a = 0f;
            fx.light = Full(go.transform, Color.Lerp(tint, Color.white, .35f), true);
        }

        static Image Full(Transform parent, Color color, bool additive)
        {
            var image = BattleFxArt1021.NewImage(additive ? "Flash" : "Dark", parent, null, color, additive);
            var r = image.rectTransform; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
            return image;
        }

        void Update()
        {
            if (paused != null && paused()) return;
            clock += Mathf.Min(Time.unscaledDeltaTime, .05f);
            Color d = dark.color; d.a = clock < .05f ? .45f : 0f; dark.color = d;
            Color l = light.color; l.a = clock < .05f ? 0f : .7f * (1f - Mathf.Clamp01((clock - .05f) / .25f)); light.color = l;
            if (clock > .32f) Destroy(gameObject);
        }
    }

    /// <summary>
    /// Damage-number gradient (2026-10-21): the glyph face runs from near-white at the top into its
    /// colour at the bottom; dark outline/shadow copies are left untouched.
    /// </summary>
    public sealed class PopupGradient1021 : BaseMeshEffect
    {
        public float TopWhite = .6f;
        readonly List<UIVertex> verts = new List<UIVertex>();
        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;
            verts.Clear(); vh.GetUIVertexStream(verts);
            float min = float.MaxValue, max = float.MinValue;
            foreach (var v in verts) { min = Mathf.Min(min, v.position.y); max = Mathf.Max(max, v.position.y); }
            float range = Mathf.Max(1f, max - min);
            for (int i = 0; i < verts.Count; i++)
            {
                UIVertex v = verts[i];
                Color c = v.color;
                if (c.r * .3f + c.g * .59f + c.b * .11f < .25f) continue;
                float k = Mathf.Clamp01((v.position.y - min) / range);
                Color lit = Color.Lerp(c, Color.white, TopWhite * k * k);
                lit.a = c.a; v.color = lit; verts[i] = v;
            }
            vh.Clear(); vh.AddUIVertexTriangleStream(verts);
        }
    }
}
