using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>
    /// Shader-driven battle VFX (2026-10-23, ported from the approved web preview "战斗特效全集").
    /// Every effect is a procedural cel-shaded layer (Resources/Shaders/Fx1023*.shader) drawn on a RawImage,
    /// laid out on a timeline: impact flash + speed lines + lens streak + shock rings, then the race body
    /// (charm heart flame, demon rift and claws, mermaid water, bloodelf crescent moon), orbiting ribbons,
    /// a pop sigil on the floor, cel smoke, and procedural particles. Big hits add hit-stop, a short slow-down,
    /// camera shake/roll and a two-frame inverted impact frame.
    /// Layout units are the web preview's: an enemy portrait 560 tall; positions are offsets from the enemy's
    /// centre (x right, y down) scaled to the real portrait, so the look matches the preview at any size.
    /// </summary>
    /// <summary>One procedural particle (positions in the Fx1023 host's space, y up; speeds in preview units).</summary>
    internal struct Particle { public float X, Y, Vx, Vy, Ay, Drag, Life, T, Size, Rot, Vr, Stretch, Sway, G0, G1; public int Kind; public Color Col; public bool Solid; }

    public sealed class Fx1023 : MonoBehaviour
    {
        public enum Race { Charm, Demon, Mermaid, BloodElf }
        enum Layer { Front, Back, Under, Wrap }

        /// <summary>deep, main, hot, light, accent, ink — the same values as the preview.</summary>
        static readonly Color[][] Palettes =
        {
            new[] { C(.32f, .05f, .42f), C(.86f, .07f, .5f), C(1f, .38f, .7f), C(1f, .95f, .98f), C(1f, .8f, .38f), C(.1f, .015f, .14f) },
            new[] { C(.12f, .03f, .26f), C(.46f, .12f, .86f), C(.74f, .48f, 1f), C(.96f, .92f, 1f), C(.72f, 1f, .4f), C(.04f, .01f, .09f) },
            new[] { C(.02f, .13f, .34f), C(0f, .5f, .86f), C(.35f, .88f, 1f), C(.92f, 1f, 1f), C(1f, .86f, .55f), C(.01f, .05f, .14f) },
            new[] { C(.26f, 0f, .07f), C(.8f, .04f, .15f), C(1f, .38f, .38f), C(1f, .93f, .9f), C(1f, .78f, .35f), C(.08f, 0f, .02f) },
        };
        static Color C(float r, float g, float b) => new Color(r, g, b, 1f);
        static readonly int[] Motifs = { 0, 3, 2, 1 };          // sigil symbol: heart, claws, drop, crescent
        static readonly int[] MotifParticle = { 1, 5, 3, 4 };  // heart, shard, drop, petal

        public static Race RaceFromKey(string key)
        {
            switch ((key ?? "").ToLowerInvariant())
            {
                case "demon": return Race.Demon;
                case "mermaid": return Race.Mermaid;
                case "bloodelf": return Race.BloodElf;
                default: return Race.Charm;
            }
        }

        // ---------- shaders / materials ----------
        static readonly Dictionary<string, Shader> shaders = new Dictionary<string, Shader>();
        static Texture2D flameMask, flashMask, ringMask;
        static Shader ShaderFor(string kind)
        {
            if (!shaders.TryGetValue(kind, out Shader s)) shaders[kind] = s = Resources.Load<Shader>("Shaders/Fx1023" + kind);
            return s;
        }
        static readonly HashSet<string> Additive = new HashSet<string> { "Arc", "Ring", "Star", "Pillar", "Comet", "Flare", "Glow", "Beams" };
        static readonly int T_ = Shader.PropertyToID("_T"), Time_ = Shader.PropertyToID("_FxTime"), Alpha_ = Shader.PropertyToID("_Alpha"),
            Seed_ = Shader.PropertyToID("_Seed"), Clip_ = Shader.PropertyToID("_Clip"), Mask_ = Shader.PropertyToID("_Mask"),
            Src_ = Shader.PropertyToID("_SrcBlend"), Dst_ = Shader.PropertyToID("_DstBlend");
        static readonly int[] PalIds = { Shader.PropertyToID("_Deep"), Shader.PropertyToID("_Main"), Shader.PropertyToID("_Hot"), Shader.PropertyToID("_Light"), Shader.PropertyToID("_Acc"), Shader.PropertyToID("_Ink") };

        /// <summary>One timed layer: kind of shader, when, where (preview units from the enemy centre), how big.</summary>
        sealed class Track
        {
            public string Kind; public float Start, Dur, X, Y, W, H, Rot, Alpha = 1f, Seed = -1f, Rise;
            public Layer Layer; public bool Caster;
            public Dictionary<string, float> P = new Dictionary<string, float>();
            public float[] R; public float Spd, Len, Thick, Phase, Gold;
            public RawImage[] Images; public Material[] Mats;
            public Track With(string k, float v) { P[k] = v; return this; }
        }


        // ---------- scene objects ----------
        RectTransform host, front, back, under, shakeRoot;
        RectTransform enemy;                  // the target portrait (its parent hosts the back/under layers)
        FxParticles1023 sparks, shapes;
        RawImage invertImage, flashImage;
        readonly List<Track> tracks = new List<Track>();
        readonly List<Particle> parts = new List<Particle>();
        readonly List<(float at, Action act)> events = new List<(float, Action)>();
        Race race;
        Func<bool> paused;
        string lastMoment;
        float clock, playT = -1f, total, unit = 1f;
        Vector2 enemyCenter, casterPos;
        float freeze, dilate, trauma, rollV, roll, invert, flash;
        // ultimate spotlight: the stage darkens around the target (a radial vignette centred on her)
        float dim, dimTarget, dimUntil, lastInvert = -10f;
        RawImage dimImage;
        static Texture2D vignette;
        Vector2 shakeBase; bool shakeBaseSet;
        readonly Dictionary<string, float> rate = new Dictionary<string, float>();

        public bool Playing => playT >= 0f;
        /// <summary>The ultimate's 终曲 is still running (the battle waits for it before the next action).</summary>
        public bool UltimatePlaying => Playing && lastMoment == "ult" && playT < total - .35f;
        /// <summary>Off while another system already shows an impact frame for this hit.</summary>
        public bool ImpactFrames { get; set; } = true;
        public static int Live { get; private set; }

        /// <summary>
        /// Create (or reuse) the director on <paramref name="layer"/>, a full-stage overlay RectTransform.
        /// <paramref name="shake"/> is moved for camera shake (usually the battle stage root); may be null.
        /// </summary>
        public static Fx1023 Ensure(RectTransform layer, RectTransform shake, Func<bool> isPaused = null)
        {
            var fx = layer.GetComponent<Fx1023>();
            if (fx == null) fx = layer.gameObject.AddComponent<Fx1023>();
            fx.host = layer; fx.shakeRoot = shake; fx.paused = isPaused;
            if (fx.front == null) fx.Build();
            return fx;
        }

        void Build()
        {
            front = MakeLayer("Fx1023-front", host);
            sparks = MakeParticles("Fx1023-shapes", front, false);
            shapes = sparks; // shapes drawn first (normal blend) …
            sparks = MakeParticles("Fx1023-sparks", front, true); // … sparks/glints on top (additive)
            invertImage = MakeOverlay("Fx1023-invert", "Invert"); invertImage.color = new Color(.76f, .74f, .6f, 1f);   // a linear-space negative washes out; this tint brings back the preview's yellow-green negative of the purple stage
            flashImage = MakeOverlay("Fx1023-flash", null); flashImage.color = new Color(1, 1, 1, 0);
            dimImage = MakeOverlay("Fx1023-dim", null); dimImage.texture = Vignette(); dimImage.color = new Color(0f, 0f, 0f, 0f);
            dimImage.transform.SetSiblingIndex(0);   // under the effects, over the stage
            var dr = dimImage.rectTransform; dr.anchorMin = dr.anchorMax = new Vector2(.5f, .5f);
            Texture2D Mask(string name) { var t = Resources.Load<Texture2D>("UI/Fx1023/" + name); if (t != null) t.wrapMode = TextureWrapMode.Clamp; return t; }
            if (flameMask == null) flameMask = Mask("flame-mask");
            if (flashMask == null) flashMask = Mask("flash");
            if (ringMask == null) ringMask = Mask("ring");
            var canvas = host.GetComponentInParent<Canvas>();
            if (canvas != null) canvas.rootCanvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
        }

        static RectTransform MakeLayer(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform; rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            if (parent is RectTransform pr) rt.pivot = pr.pivot;   // same origin as the host, so host-space points can be used as is
            return rt;
        }
        FxParticles1023 MakeParticles(string name, RectTransform parent, bool additive)
        {
            var rt = MakeLayer(name, parent);
            // 2026-11-01: explicit — FxParticles1023 has no script file of its own, and in the editor its inherited
            // RequireComponent was not applied; the renderer-less graphic made ClipperRegistry.Cull throw every frame,
            // which aborted every canvas rebuild after the first hit.
            if (rt.GetComponent<CanvasRenderer>() == null) rt.gameObject.AddComponent<CanvasRenderer>();
            var p = rt.gameObject.AddComponent<FxParticles1023>(); p.raycastTarget = false;
            p.material = new Material(ShaderFor("Particles")) { hideFlags = HideFlags.DontSave };
            p.material.SetFloat(Src_, 1f); p.material.SetFloat(Dst_, additive ? 1f : 10f);
            return p;
        }
        static Texture2D Vignette()
        {
            if (vignette != null) return vignette;
            const int n = 128;
            vignette = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = new Vector2(x - n / 2f + .5f, y - n / 2f + .5f).magnitude / (n / 2f);
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.1f, .5f, d));
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            vignette.SetPixels32(px); vignette.Apply();
            return vignette;
        }

        RawImage MakeOverlay(string name, string shaderKind)
        {
            var rt = MakeLayer(name, host);
            var img = rt.gameObject.AddComponent<RawImage>(); img.raycastTarget = false;
            if (shaderKind != null) img.material = new Material(ShaderFor(shaderKind)) { hideFlags = HideFlags.DontSave };
            rt.gameObject.SetActive(false);
            return img;
        }

        /// <summary>Back/under layers sit in the enemy's parent just behind the portrait and follow the host's rect.</summary>
        void PlaceBehind(RectTransform target)
        {
            if (target == null || target.parent == null) { back = under = front; return; }
            if (back == null || back == front || back.parent != target.parent)
            {
                if (back != null && back != front) Destroy(back.gameObject);
                if (under != null && under != front) Destroy(under.gameObject);
                under = MakeLayer("Fx1023-under", target.parent);
                back = MakeLayer("Fx1023-back", target.parent);
            }
            int i = target.GetSiblingIndex();
            under.SetSiblingIndex(i); back.SetSiblingIndex(i + 1); // the enemy is now at i + 2
            SyncBehind();
        }
        void SyncBehind()
        {
            foreach (var rt in new[] { back, under })
            {
                if (rt == null || rt == front) continue;
                rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f); rt.pivot = host.pivot;
                rt.position = host.position; rt.rotation = host.rotation;
                Vector3 hs = host.lossyScale, ps = rt.parent.lossyScale;
                rt.localScale = new Vector3(hs.x / Mathf.Max(ps.x, 1e-5f), hs.y / Mathf.Max(ps.y, 1e-5f), 1f);
                rt.sizeDelta = host.rect.size;
            }
        }

        Vector2 ToHost(RectTransform rt)
        {
            Vector3 world = rt.TransformPoint(rt.rect.center);
            return host.InverseTransformPoint(world);
        }

        // ---------- public moments ----------

        /// <summary>True (and a burst on <paramref name="target"/>) when the same moment just started on another target.</summary>
        bool SameMoment(string moment, RectTransform target)
        {
            // an ultimate's strikes land staggered over ~0.3 s; one effect covers them all
            float window = moment == "ult" ? .45f : moment == "skill" ? .2f : .12f;
            // While the 终曲 plays, other hits only add sparks: restarting would cut the ultimate short (2026-10-24).
            if (moment != "ult" && UltimatePlaying)
            {
                if (target != null) { Vector2 h = ToHost(target), k0 = enemyCenter; enemyCenter = h; Burst(0f, 0f, .5f); enemyCenter = k0; }
                return true;
            }
            if (moment == "ult" && Playing && lastMoment == "ult" && playT <= window && target == enemy) return true;
            if (!Playing || lastMoment != moment || playT > window || target == null || target == enemy) return false;
            Vector2 c = ToHost(target), keep = enemyCenter; enemyCenter = c;
            Burst(0f, 0f, .7f); enemyCenter = keep;
            return true;
        }

        /// <summary>Normal attack: a crescent slash across the target with sparks.</summary>
        public void Slash(string raceKey, RectTransform target)
        {
            if (SameMoment("slash", target)) return;
            Begin(raceKey, target, null, .9f); lastMoment = "slash";
            bool left = UnityEngine.Random.value < .5f;
            Add("Slash", 0f, .24f, 0f, 10f, 520f, 280f).Rot = left ? .5f : Mathf.PI - .5f;
            tracks[tracks.Count - 1].With("_Curve", .7f);
            Add("Star", .07f, .16f, 0f, 0f, 240f, 240f).With("_N", 6f).With("_Inner", 0f).Alpha = .8f;
            Add("Ring", .07f, .3f, 0f, 0f, 380f, 380f).Alpha = .6f;
            At(.07f, () => Hit(.55f, left ? 1f : -1f, false, false));
        }

        /// <summary>Critical: X-slash, impact frame, slow-down, speed lines.</summary>
        public void Crit(string raceKey, RectTransform target)
        {
            if (SameMoment("crit", target)) return;
            Begin(raceKey, target, null, 1.2f); lastMoment = "crit";
            Add("Slash", 0f, .26f, 0f, 0f, 620f, 320f).With("_Curve", .6f).Rot = .62f;
            Add("Slash", .09f, .26f, 0f, 0f, 620f, 320f).With("_Curve", .6f).Rot = Mathf.PI - .62f;
            Impact(.16f, 0f, 0f, 1f);
            At(.16f, () => Hit(1.1f, 1f, true, true));
        }

        /// <summary>Active skill of a race. <paramref name="caster"/> (optional) gets the charge, sigil and comet.</summary>
        public void Skill(string raceKey, RectTransform target, RectTransform caster, float scale = 1f, bool withCharge = true)
        {
            string moment = scale > 1f ? "ult" : "skill";
            if (SameMoment(moment, target)) return;
            Begin(raceKey, target, caster, 2.4f); lastMoment = moment;
            float t = withCharge && caster != null ? .6f : 0f;
            if (t > 0f) Charge();
            Impact(t, 0f, 0f, scale);
            Add("Ring", t + .03f, .6f, 0f, 416f, 800f, 230f).Layer = Layer.Wrap;
            var sig = Add("Sigil", t, 1.55f, 0f, 416f, 560f, 190f); sig.Layer = Layer.Under; sig.With("_Spin", -1.4f);
            Body(t + .02f, scale);
            Add("Star", t + .58f, .2f, 0f, -20f, 306f * scale, 306f * scale).With("_N", 8f).With("_Inner", 0f).Alpha = .85f;
            Add("Flare", t + .58f, .26f, 0f, -20f, 900f, 36f);
            At(t, () => Hit(1f, 1f, true, true));
            At(t + .32f, () => Hit(.35f, 1f, false, false));
            At(t + .58f, () => Hit(.8f, -1f, true, false));
            total = t + 1.85f;
        }

        /// <summary>Ultimate: the amplified skill (the portrait cut-in stays with the existing ultimate show).</summary>
        public void Ultimate(string raceKey, RectTransform target)
        {
            // 2026-10-24 (owner: the ultimate must not look like a bigger skill): its own 「终曲」 sequence —
            // the stage goes dark around the target, every light rushes in, a giant race emblem spins up beneath her,
            // a five-cut barrage, then the finale: sky pillar, inked shock rings, the race body at full size, a white-out.
            if (SameMoment("ult", target)) return;
            Begin(raceKey, target, null, 2.9f); lastMoment = "ult";
            dimTarget = .7f; dimUntil = 2.15f;
            Add("Beams", 0f, 1.3f, 0f, 0f, 2200f, 2200f);
            Add("Beams", .5f, .8f, 0f, 0f, 1700f, 1700f).Seed = 9.3f;
            { var sig = Add("Sigil", 0f, 2.5f, 0f, 416f, 1050f, 360f); sig.Layer = Layer.Under; sig.With("_Spin", 3.6f); }
            { var sig = Add("Sigil", .15f, 2.2f, 0f, -40f, 760f, 760f); sig.Layer = Layer.Back; sig.With("_Spin", -2.2f); sig.Alpha = .55f; }
            Orbit(.1f, 1.1f);
            float[] rots = { .45f, Mathf.PI - .35f, -.2f, Mathf.PI + .6f, 1.3f };
            for (int i = 0; i < rots.Length; i++)
            {
                float t = .5f + i * .09f;
                var sl = Add("Slash", t, .24f, (i % 2 == 0 ? -1f : 1f) * 30f * i, -40f + 25f * i, 640f, 330f); sl.Rot = rots[i]; sl.With("_Curve", .55f + .1f * (i % 3));
                Add("Star", t + .05f, .14f, 0f, -20f + 20f * i, 260f, 260f).With("_N", 6f).With("_Inner", 0f).Alpha = .8f;
                float dir = i % 2 == 0 ? 1f : -1f;
                At(t + .05f, () => Hit(.5f, dir, false, false));
            }
            const float F = 1.02f;   // the finale
            Impact(F, 0f, 0f, 1.8f);
            Add("Pillar", F - .04f, .75f, 0f, -330f, 380f, 1900f);
            Add("Shock", F, .7f, 0f, 0f, 1400f, 1400f).Seed = 1.3f;
            Add("Shock", F + .12f, .8f, 0f, 0f, 2100f, 2100f).Seed = 6.1f;
            { var w = Add("Shock", F + .05f, .65f, 0f, 416f, 1500f, 440f); w.Layer = Layer.Wrap; w.Seed = 3.7f; }
            Add("Star", F, .3f, 0f, 0f, 2000f, 2000f).With("_N", 72f).With("_Inner", .22f).Alpha = .7f;
            Add("Flare", F, .45f, 0f, 0f, 2400f, 70f);
            Add("Flare", F + .04f, .4f, 0f, 0f, 70f, 1600f);
            Body(F + .02f, 1.3f);
            At(F, () => { Hit(1.2f, 1f, true, true); flash = Mathf.Max(flash, .3f); trauma = 1f; Burst(0f, 0f, 1.4f); });
            At(F + .35f, () => Hit(.7f, -1f, true, false));
            total = F + 1.85f;
        }

        /// <summary>
        /// Defeat accent on top of KoBreak1022's shard break: a big ring, a lens streak, a flash and a burst
        /// in the colour of the last hit. Keeps the running effect if there is one.
        /// </summary>
        public void Defeat(RectTransform target)
        {
            if (target == null) return;
            if (!Playing) { Begin(RaceName(race), target, null, .8f); lastMoment = "ko"; }
            float at = playT;
            Vector2 c = ToHost(target), d = (c - enemyCenter) / unit;
            Add("Ring", at, .6f, d.x, -d.y + 200f, 900f, 900f).Alpha = .9f;
            Add("Flare", at, .4f, d.x, -d.y + 200f, 1200f, 60f);
            total = Mathf.Max(total, at + .7f);
            flash = .3f; trauma = Mathf.Min(1f, trauma + .6f);
            Vector2 keep = enemyCenter; enemyCenter = c; Burst(0f, 150f, 1.4f); enemyCenter = keep;
        }
        static string RaceName(Race r) => r == Race.Demon ? "demon" : r == Race.Mermaid ? "mermaid" : r == Race.BloodElf ? "bloodelf" : "charm";

        /// <summary>Break: X-slash, then the shock and a burst (the shard break itself stays with KoBreak1022).</summary>
        public void Break(string raceKey, RectTransform target)
        {
            Begin(raceKey, target, null, 1.4f);
            Add("Slash", 0f, .26f, 0f, 0f, 680f, 340f).With("_Curve", .6f).Rot = .7f;
            Add("Slash", .08f, .26f, 0f, 0f, 680f, 340f).With("_Curve", .6f).Rot = Mathf.PI - .7f;
            Impact(.18f, 0f, 0f, 1.1f);
            Add("Ring", .5f, .6f, 0f, 50f, 900f, 900f).Alpha = .9f;
            Add("Flare", .5f, .4f, 0f, 50f, 1200f, 60f);
            At(.18f, () => Hit(1f, 1f, true, true));
            At(.5f, () => { trauma = 1f; flash = .35f; invert = .05f; Burst(0f, 30f, 1.6f); });
        }

        // ---------- timeline building ----------
        void Begin(string raceKey, RectTransform target, RectTransform caster, float length)
        {
            Clear();
            race = RaceFromKey(raceKey);
            enemy = target;
            PlaceBehind(target);
            float h = target != null ? target.rect.height * target.lossyScale.y / Mathf.Max(host.lossyScale.y, 1e-5f) : host.rect.height * .55f;
            unit = Mathf.Max(.2f, h / 560f);
            enemyCenter = target != null ? ToHost(target) : Vector2.zero;
            casterPos = caster != null ? ToHost(caster) : enemyCenter + new Vector2(-120f, -280f) * unit;
            total = length; playT = 0f; Live++;
        }

        Track Add(string kind, float start, float dur, float x, float y, float w, float h)
        {
            var tr = new Track { Kind = kind, Start = start, Dur = dur, X = x, Y = y - 150f, W = w, H = h, Layer = Layer.Front };
            tracks.Add(tr);
            return tr;
        }
        void At(float t, Action a) => events.Add((t, a));

        void Impact(float t, float x, float y, float s)
        {
            Add("Star", t, .2f, x, y, 360f * s, 360f * s).With("_N", 8f).With("_Inner", 0f).With("_Rot", UnityEngine.Random.value).Alpha = .85f;
            Add("Star", t, .16f, x, y, 300f * s, 300f * s).With("_N", 4f).With("_Inner", 0f).With("_Rot", .78f);
            Add("Star", t, .16f, x, y, 1300f, 1300f).With("_N", 56f).With("_Inner", .32f).Alpha = .55f;
            Add("Flare", t, .3f, x, y, 1100f * s, 46f * s);
            Add("Ring", t + .01f, .4f, x, y, 600f * s, 600f * s).Alpha = .8f;
        }

        void Charge()
        {
            Track Cast(Track tr) { tr.Caster = true; return tr; }
            var sig = Cast(Add("Sigil", 0f, .78f, 0f, 30f + 150f, 320f, 118f)); sig.Layer = Layer.Front; sig.With("_Spin", 2.4f);
            for (int i = 0; i < 3; i++)
            {
                var a = Cast(Add("Arc", .04f * i, .5f - .04f * i, 0f, -30f + 150f, 300f, 300f));
                a.R = new[] { .95f, .25f }; a.Spd = 9f; a.Len = 2.6f; a.Thick = i == 0 ? .07f : .06f; a.Phase = i * 2.09f; a.Gold = i == 1 ? 1f : 0f;
            }
            Cast(Add("Pillar", .36f, .22f, 0f, -230f + 150f, 150f, 520f));
            Add("Comet", .48f, .12f, 0f, 0f, 300f, 110f);
        }

        void Orbit(float t, float dur)
        {
            var a = Add("Arc", t, dur, 0f, -10f, 560f, 190f); a.Layer = Layer.Wrap;
            a.R = new[] { .7f, .92f }; a.Spd = 7f; a.Len = 2.3f; a.Thick = .09f;
            var b = Add("Arc", t + .06f, dur - .06f, 0f, 160f, 500f, 160f); b.Layer = Layer.Wrap;
            b.R = new[] { .7f, .9f }; b.Spd = -6f; b.Len = 2f; b.Thick = .08f; b.Phase = 2.6f; b.Gold = 1f;
        }
        void Smokes(float t, float light)
        {
            float[][] s = { new[] { -60f, -80f, 260f, 280f, 1.3f, 150f, 0f }, new[] { 70f, -30f, 230f, 250f, 4.7f, 130f, .06f }, new[] { 5f, -170f, 250f, 270f, 8.2f, 170f, .12f } };
            foreach (var v in s) { var tr = Add("Smoke", t + v[6], 1.05f - v[6] * .5f, v[0], v[1], v[2], v[3]); tr.Seed = v[4]; tr.Rise = v[5]; tr.With("_Lightness", light); }
        }
        void Body(float t, float s)
        {
            switch (race)
            {
                case Race.Charm:
                    { var f = Add("Flame", t + .02f, .98f, 0f, -60f, 640f * s, 760f * s); f.Layer = Layer.Back; f.Seed = 1.7f; f.Alpha = .7f; f.With("_Hole", 0f).With("_Shape", 0f); }
                    { var f = Add("Flame", t + .04f, .92f, 0f, -40f, 520f * s, 640f * s); f.Seed = 7.3f; f.Alpha = .9f; f.With("_Hole", 1.1f).With("_Shape", 0f); }
                    Orbit(t + .06f, .9f); Smokes(t + .68f, 0f); break;
                case Race.Demon:
                    { var r = Add("Rift", t + .02f, 1.15f, 10f, -20f, 300f * s, 640f * s); r.Layer = Layer.Back; r.Seed = 3.1f; }
                    for (int i = 0; i < 3; i++) { var sl = Add("Slash", t + .09f * i, .26f, 30f - 30f * i, -80f + 70f * i, 560f * s, 300f * s); sl.Rot = 2f; sl.With("_Curve", .5f); }
                    { var f = Add("Flame", t + .12f, .9f, 0f, -50f, 460f * s, 560f * s); f.Layer = Layer.Back; f.Seed = 5.5f; f.Alpha = .55f; f.With("_Hole", 1.2f).With("_Shape", 1f); }
                    Orbit(t + .1f, .85f); Smokes(t + .7f, 0f); break;
                case Race.Mermaid:
                    Add("Pillar", t, .55f, 0f, -10f, 300f * s, 900f * s);
                    { var w = Add("Water", t + .02f, 1f, 0f, 270f, 640f * s, 520f * s); w.Layer = Layer.Wrap; w.Seed = 2.4f; }
                    { var w = Add("Water", t + .12f, .9f, 20f, 300f, 520f * s, 380f * s); w.Seed = 6.6f; w.Alpha = .9f; }
                    Add("Ring", t + .05f, .7f, 0f, 412f, 760f, 220f).Layer = Layer.Wrap;
                    Add("Ring", t + .3f, .7f, 0f, 412f, 700f, 200f).Layer = Layer.Wrap;
                    Orbit(t + .08f, .85f); Smokes(t + .7f, .75f); break;
                default:
                    { var m = Add("Moon", t - .2f, 1.6f, 40f, -180f, 520f * s, 520f * s); m.Layer = Layer.Back; m.Rise = 60f; }
                    { var sl = Add("Slash", t, .32f, 0f, -10f, 760f * s, 380f * s); sl.Rot = -.12f; sl.With("_Curve", .9f); }
                    { var sl = Add("Slash", t + .16f, .3f, 0f, 30f, 640f * s, 320f * s); sl.Rot = 3f; sl.With("_Curve", .9f); }
                    Orbit(t + .1f, .85f); Smokes(t + .7f, 0f); break;
            }
        }

        // ---------- hits ----------
        void Hit(float k, float dir, bool big, bool slowDown)
        {
            freeze = Mathf.Max(freeze, big ? .045f * k : .03f);
            trauma = Mathf.Min(1f, trauma + .55f * k);
            rollV += .5f * k * dir;
            bool calm = BattlePresentationOptions.ReduceMotion;
            // The P5 negative is a punctuation mark: at most one every 2.5 s, or it turns into strobing (2026-10-24).
            if (big && ImpactFrames && !calm && clock - lastInvert > 2.5f) { invert = .055f; flash = .1f; lastInvert = clock; }
            if (slowDown && !calm) dilate = .26f;
            Burst(0f, 0f, k);
            Kicked?.Invoke(k);
        }
        /// <summary>Raised on every hit of an effect (strength 0..1.2) so the battle can react (enemy recoil, sound).</summary>
        public event Action<float> Kicked;

        // ---------- particles ----------
        Color Pal(int i) => Palettes[(int)race][i];
        void Emit(Particle p) { if (parts.Count < 700) parts.Add(p); }
        void Burst(float x, float y, float k)
        {
            var o = enemyCenter + new Vector2(x, -(y - 150f)) * unit;
            for (int i = 0; i < Mathf.RoundToInt(34 * k); i++)
            {
                float a = UnityEngine.Random.value * Mathf.PI * 2f, sp = UnityEngine.Random.Range(700f, 1700f);
                Emit(new Particle { Kind = 0, X = o.x, Y = o.y, Vx = Mathf.Cos(a) * sp, Vy = Mathf.Sin(a) * sp, Drag = .015f, Ay = -600f, Life = UnityEngine.Random.Range(.25f, .55f), Size = UnityEngine.Random.Range(10f, 18f), Stretch = .0028f, Col = UnityEngine.Random.value < .35f ? Pal(4) : Pal(2), G0 = 1, G1 = 1 });
            }
            for (int i = 0; i < Mathf.RoundToInt(10 * k); i++)
            {
                float a = UnityEngine.Random.value * Mathf.PI * 2f, sp = UnityEngine.Random.Range(250f, 600f);
                Emit(new Particle { Kind = MotifParticle[(int)race], X = o.x, Y = o.y, Vx = Mathf.Cos(a) * sp, Vy = Mathf.Sin(a) * sp + 200f, Drag = .08f, Ay = -500f, Life = UnityEngine.Random.Range(.5f, .8f), Size = UnityEngine.Random.Range(16f, 28f), Vr = UnityEngine.Random.Range(-6f, 6f), Rot = UnityEngine.Random.value * 6.28f, Col = Pal(2), G0 = 1, G1 = 1 });
            }
            for (int i = 0; i < Mathf.RoundToInt(6 * k); i++)
                Emit(new Particle { Kind = 2, X = o.x + UnityEngine.Random.Range(-120f, 120f) * unit, Y = o.y + UnityEngine.Random.Range(-100f, 140f) * unit, Life = UnityEngine.Random.Range(.2f, .35f), Size = UnityEngine.Random.Range(40f, 80f), Col = UnityEngine.Random.value < .5f ? Pal(4) : Pal(2), G0 = .4f, G1 = 1.2f, Drag = 1 });
        }
        void Stream(string key, float perSecond, float dt, Func<Particle> make)
        {
            rate.TryGetValue(key, out float acc); acc += perSecond * dt;
            while (acc >= 1f) { acc -= 1f; Emit(make()); }
            rate[key] = acc;
        }
        void Emitters(float dt)
        {
            float R(float a, float b) => UnityEngine.Random.Range(a, b);
            float burnStart = Track0("Flame", "Water", "Rift", "Moon");
            if (burnStart >= 0f && playT > burnStart + .1f && playT < burnStart + .8f)
            {
                Vector2 c = enemyCenter;
                Stream("embers", 40, dt, () => new Particle { Kind = 0, X = c.x + R(-160, 160) * unit, Y = c.y + R(-160, 120) * unit, Vx = R(-40, 40), Vy = R(260, 480), Drag = .4f, Life = R(.5f, .9f), Size = R(6, 11), Stretch = .002f, Sway = R(2, 5), Col = R(0, 1) < .45f ? Pal(4) : Pal(2), G0 = 1, G1 = 1 });
                Stream("twinkle", 14, dt, () => new Particle { Kind = 2, X = c.x + R(-190, 190) * unit, Y = c.y + R(-130, 270) * unit, Vy = R(30, 60), Life = R(.2f, .35f), Size = R(10, 20), Col = R(0, 1) < .5f ? Pal(4) : Pal(2), G0 = .3f, G1 = 1.1f, Drag = 1 });
                if (race == Race.Mermaid)
                    Stream("motif", 18, dt, () => new Particle { Kind = 3, X = c.x + R(-150, 150) * unit, Y = c.y - 200 * unit, Vx = R(-200, 200), Vy = R(500, 900), Ay = -1600, Drag = 1, Life = R(.6f, 1f), Size = R(10, 18), Col = Pal(2), G0 = 1, G1 = 1 });
                else
                    Stream("motif", 18, dt, () => new Particle { Kind = MotifParticle[(int)race], X = c.x + R(-140, 140) * unit, Y = c.y + R(-40, 100) * unit, Vx = R(-40, 40), Vy = R(220, 360), Drag = .5f, Ay = 40, Life = R(.8f, 1.2f), Size = R(14, 26), Vr = R(-3, 3), Rot = R(0, 6.28f), Sway = R(2, 4), Col = Pal(2), G0 = 1, G1 = .4f });
            }
            // the comet head and its trail
            foreach (var tr in tracks)
            {
                if (tr.Kind != "Comet") continue;
                float p = (playT - tr.Start) / tr.Dur; if (p < 0f || p >= 1f) continue;
                Vector2 pos = CometPos(p, out Vector2 vel);
                float sp = Mathf.Max(vel.magnitude, 1f);
                Emit(new Particle { Kind = MotifParticle[(int)race], X = pos.x, Y = pos.y, Life = .03f, Size = 26, Rot = Mathf.Atan2(vel.y, vel.x) - Mathf.PI / 2, Col = Pal(3), Solid = true, G0 = 1, G1 = 1, Drag = 1 });
                Emit(new Particle { Kind = 2, X = pos.x, Y = pos.y, Life = .03f, Size = 90, Col = Pal(4), Rot = clock * 8, Solid = true, G0 = 1, G1 = 1, Drag = 1 });
                Stream("trail", 220, dt, () => new Particle { Kind = 0, X = pos.x + R(-8, 8), Y = pos.y + R(-8, 8), Vx = -vel.x / sp * R(150, 400) + R(-80, 80), Vy = -vel.y / sp * R(150, 400) + R(-80, 80), Drag = .05f, Life = R(.15f, .3f), Size = R(8, 16), Stretch = .003f, Col = R(0, 1) < .3f ? Pal(4) : Pal(2), G0 = 1, G1 = .3f });
            }
        }
        float Track0(params string[] kinds)
        {
            float best = -1f;
            foreach (var tr in tracks) if (Array.IndexOf(kinds, tr.Kind) >= 0 && (best < 0f || tr.Start < best)) best = tr.Start;
            return best;
        }
        Vector2 CometPos(float k, out Vector2 vel)
        {
            Vector2 a = casterPos + new Vector2(0f, 40f) * unit, b = enemyCenter + new Vector2(0f, 150f) * unit;
            Vector2 P(float u) { float e = u * u; return Vector2.Lerp(a, b, e) + new Vector2(Mathf.Sin(e * Mathf.PI) * 70f * unit, 0f); }
            Vector2 p0 = P(k), p1 = P(Mathf.Min(1f, k + .02f));
            vel = (p1 - p0) / (.02f * .12f);
            return p0;
        }

        // ---------- per frame ----------
        void Update()
        {
            float real = Mathf.Min(Time.unscaledDeltaTime, .05f);
            if (!Playing && parts.Count == 0 && trauma <= 0f) return;
            if (paused != null && paused()) return;
            float dt = real;
            if (BattleHitStop.Frozen) dt = 0f;
            else if (freeze > 0f) { freeze -= dt; dt = 0f; }
            else if (dilate > 0f) { dt *= .45f + .55f * Mathf.Pow(1f - dilate / .26f, 2f); dilate = Mathf.Max(0f, dilate - real); }
            clock += real;
            if (Playing)
            {
                playT += dt;
                for (int i = events.Count - 1; i >= 0; i--) if (playT >= events[i].at) { var a = events[i].act; events.RemoveAt(i); a(); }
                Emitters(dt);
                if (playT > total) { Clear(); }
            }
            Step(dt);
            Draw();
            Camera_(real);
        }

        void Step(float dt)
        {
            for (int i = parts.Count - 1; i >= 0; i--)
            {
                var p = parts[i]; p.T += dt;
                if (p.T >= p.Life) { parts.RemoveAt(i); continue; }
                float d = Mathf.Pow(p.Drag <= 0f ? 1f : p.Drag, dt);
                p.Vx *= d; p.Vy *= d; p.Vy += p.Ay * dt;
                if (p.Sway != 0f) p.Vx += Mathf.Sin(p.T * 6f + p.Size) * p.Sway * 60f * dt;
                p.X += p.Vx * dt * unit; p.Y += p.Vy * dt * unit; p.Rot += p.Vr * dt;
                parts[i] = p;
            }
        }

        void Draw()
        {
            SyncBehind();
            foreach (var tr in tracks) DrawTrack(tr);
            sparks.Fill(parts, true, unit); shapes.Fill(parts, false, unit);
            sparks.SetPalette(Pal(5), Pal(3)); shapes.SetPalette(Pal(5), Pal(3));
            invertImage.gameObject.SetActive(invert > 0f);
            if (dimImage != null)
            {
                dimImage.gameObject.SetActive(dim > .001f);
                if (dim > .001f)
                {
                    float side = Mathf.Max(host.rect.width, host.rect.height) * 1.35f;
                    dimImage.rectTransform.sizeDelta = new Vector2(side, side);
                    dimImage.rectTransform.anchoredPosition = enemyCenter + new Vector2(0f, 120f) * unit - host.rect.center;
                    dimImage.color = new Color(.02f, .0f, .05f, dim);
                }
            }
            flashImage.gameObject.SetActive(flash > 0f);
            if (flash > 0f) flashImage.color = new Color(1, 1, 1, flash);
        }

        void DrawTrack(Track tr)
        {
            float p = (playT - tr.Start) / tr.Dur;
            bool on = Playing && p >= 0f && p < 1f;
            if (!on) { if (tr.Images != null) foreach (var im in tr.Images) im.enabled = false; return; }
            if (tr.Images == null) Spawn(tr);
            float x = tr.X, y = tr.Y, w = tr.W, h = tr.H, rot = tr.Rot;
            float Ease(float v) => 1f - Mathf.Pow(1f - Mathf.Clamp01(v), 3f);
            float EaseIn(float v) => Mathf.Pow(Mathf.Clamp01(v), 2f);
            float alpha = tr.Alpha;
            switch (tr.Kind)
            {
                case "Star":
                    if (tr.P.TryGetValue("_Inner", out float inner) && inner > 0f) { float s = 1f + p * .25f; w *= s; h *= s; alpha *= 1f - p; }
                    else { float s = p < .18f ? .3f + .9f * Ease(p / .18f) : 1.2f - .5f * EaseIn((p - .18f) / .82f); w *= s; h *= s; alpha *= 1f - EaseIn((p - .5f) / .5f); }
                    rot = 0f; break;
                case "Smoke": y -= tr.Rise * Ease(p); { float s = .75f + .4f * Ease(p); w *= s; h *= s; } break;
                case "Ring": { float s = .12f + .95f * Ease(p); w *= s; h *= s; } break;
                case "Flame": { float s = .9f + .12f * Ease(p); w *= s; h *= s; } break;
                case "Moon": y -= tr.Rise * Ease(p); break;
                case "Pillar": w *= .7f + .3f * Ease(p / .3f); break;
                case "Arc": if (p > .8f) alpha *= 1f - (p - .8f) / .2f; break;
            }
            Vector2 pos;
            if (tr.Kind == "Comet")
            {
                pos = CometPos(p, out Vector2 vel); rot = Mathf.Atan2(vel.y, vel.x);
                pos -= new Vector2(Mathf.Cos(rot), Mathf.Sin(rot)) * w * .3f * unit;
            }
            else
            {
                Vector2 origin = tr.Caster ? casterPos : enemyCenter;
                pos = origin + new Vector2(x, -y) * unit;
                rot = -rot;                                         // preview angles are y-down
            }
            targets.Clear();
            for (int i = 0; i < tr.Images.Length; i++) { targets.Add(tr.Mats[i]); var mfr = tr.Images[i].materialForRendering; if (mfr != null) targets.Add(mfr); }
            foreach (var mat in targets)
            {
                mat.SetFloat(T_, p); mat.SetFloat(Time_, clock); mat.SetFloat(Alpha_, alpha);
                mat.SetFloat(Seed_, tr.Seed >= 0f ? tr.Seed : tr.Start * 7.1f);
                if (Array.IndexOf(tr.Mats, mat) < 0) SetPalette(mat);   // a mask's stencil copy
                foreach (var kv in tr.P) mat.SetFloat(kv.Key, kv.Value);
                if (tr.Kind == "Arc")
                {
                    float k = Ease(p);
                    mat.SetFloat("_R", tr.R[0] + (tr.R[1] - tr.R[0]) * k); mat.SetFloat("_Thick", tr.Thick);
                    mat.SetFloat("_Head", tr.Phase + tr.Spd * (playT - tr.Start)); mat.SetFloat("_Len", tr.Len * Mathf.Min(1f, p / .12f)); mat.SetFloat("_Gold", tr.Gold);
                }
                if (tr.Kind == "Sigil") { mat.SetFloat("_Motif", Motifs[(int)race]); if (tr.P.TryGetValue("_Spin", out float spin)) mat.SetFloat("_Spin", spin * (playT - tr.Start)); }
                if (tr.Kind == "Star" && !(tr.P.TryGetValue("_Inner", out float inn2) && inn2 > 0f)) mat.SetFloat("_Rot", (tr.P.TryGetValue("_Rot", out float r0) ? r0 : 0f) + p * .3f);
            }
            for (int i = 0; i < tr.Images.Length; i++)
            {
                var im = tr.Images[i]; im.enabled = true;
                var rt = im.rectTransform;
                rt.anchoredPosition = pos - host.rect.center; rt.sizeDelta = new Vector2(w, h) * unit; rt.localEulerAngles = new Vector3(0f, 0f, rot * Mathf.Rad2Deg);
            }
        }

        readonly HashSet<Material> targets = new HashSet<Material>();

        void Spawn(Track tr)
        {
            var mat = new Material(ShaderFor(tr.Kind)) { hideFlags = HideFlags.DontSave };
            bool add = Additive.Contains(tr.Kind) && !(tr.Kind == "Star" && tr.P.TryGetValue("_Inner", out float inn) && inn > 0f);
            mat.SetFloat(Src_, 1f); mat.SetFloat(Dst_, add ? 1f : 10f);
            if (tr.Kind == "Flame" && flameMask != null) mat.SetTexture(Mask_, flameMask);
            if (tr.Kind == "Star" && flashMask != null) mat.SetTexture(Mask_, flashMask);
            if (tr.Kind == "Ring" && ringMask != null) mat.SetTexture(Mask_, ringMask);
            RectTransform[] parents = tr.Layer == Layer.Wrap ? new[] { back, front } : new[] { tr.Layer == Layer.Back ? back : tr.Layer == Layer.Under ? under : front };
            tr.Images = new RawImage[parents.Length]; tr.Mats = new Material[parents.Length];
            for (int i = 0; i < parents.Length; i++)
            {
                var go = new GameObject("Fx1023-" + tr.Kind, typeof(RectTransform));
                var rt = (RectTransform)go.transform; rt.SetParent(parents[i], false);
                rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f);
                var img = go.AddComponent<RawImage>(); img.raycastTarget = false;
                var m = i == 0 ? mat : new Material(mat) { hideFlags = HideFlags.DontSave };
                m.SetFloat(Clip_, parents.Length == 2 ? (i == 0 ? 1f : 2f) : 0f);   // far half behind her, near half in front
                SetPalette(m);   // the palette uniforms are not declared properties, so a material copy does not carry them
                img.material = m; tr.Mats[i] = m; tr.Images[i] = img;
                if (front == parents[i]) rt.SetSiblingIndex(0);   // under the particles
            }
        }

        // set as vectors: the shaders expect the sRGB numbers of the preview and convert to linear themselves
        void SetPalette(Material m) { var pal = Palettes[(int)race]; for (int i = 0; i < 6; i++) m.SetVector(PalIds[i], pal[i]); }

        void Camera_(float real)
        {
            trauma = Mathf.Max(0f, trauma - real * 2.2f);
            invert = Mathf.Max(0f, invert - real);
            flash = Mathf.Max(0f, flash - real * 5f);
            float wantDim = Playing && playT < dimUntil ? dimTarget : 0f;
            dim = Mathf.MoveTowards(dim, wantDim, real * (wantDim > dim ? 3.5f : 1.6f));
            if (!Playing) dimTarget = 0f;
            rollV += (-roll * 160f - rollV * 14f) * real; roll += rollV * real;
            if (shakeRoot == null) return;
            if (!shakeBaseSet) { shakeBase = shakeRoot.anchoredPosition; shakeBaseSet = true; }
            float sh = trauma * trauma * 20f * unit, t = clock;
            var off = new Vector2((Mathf.Sin(t * 47f) + .6f * Mathf.Sin(t * 29f + 1.7f)) * sh, (Mathf.Sin(t * 41f + .8f) + .6f * Mathf.Sin(t * 23f + 2.9f)) * sh);
            shakeRoot.anchoredPosition = shakeBase + off;
            shakeRoot.localEulerAngles = new Vector3(0f, 0f, -roll * Mathf.Rad2Deg);
            if (trauma <= 0f && Mathf.Abs(roll) < 1e-4f && Mathf.Abs(rollV) < 1e-4f) { shakeRoot.anchoredPosition = shakeBase; shakeRoot.localEulerAngles = Vector3.zero; shakeBaseSet = false; }
        }

        void Clear()
        {
            foreach (var tr in tracks)
            {
                if (tr.Images != null) foreach (var im in tr.Images) if (im != null) Destroy(im.gameObject);
                if (tr.Mats != null) foreach (var m in tr.Mats) if (m != null) Destroy(m);
            }
            tracks.Clear(); events.Clear(); rate.Clear();
            if (playT >= 0f) Live = Mathf.Max(0, Live - 1);
            playT = -1f;
        }

        void OnDisable()
        {
            Clear(); parts.Clear();
            if (shakeRoot != null && shakeBaseSet) { shakeRoot.anchoredPosition = shakeBase; shakeRoot.localEulerAngles = Vector3.zero; shakeBaseSet = false; }
        }
    }

    /// <summary>Batches the procedural particles of <see cref="Fx1023"/> into one mesh (one quad each).</summary>
    public sealed class FxParticles1023 : MaskableGraphic
    {
        readonly List<UIVertex> verts = new List<UIVertex>();
        static readonly int Ink = Shader.PropertyToID("_Ink"), Light = Shader.PropertyToID("_Light");

        // also on materialForRendering: a mask's stencil copy does not carry undeclared uniforms
        internal void SetPalette(Color ink, Color light) { Apply(material, ink, light); Apply(materialForRendering, ink, light); }
        static void Apply(Material m, Color ink, Color light) { if (m != null) { m.SetVector(Ink, ink); m.SetVector(Light, light); } }

        /// <summary>Additive takes sparks (0) and glints (2); the other takes the inked shapes.</summary>
        internal void Fill(List<Particle> list, bool additive, float unit)
        {
            verts.Clear();
            foreach (var p in list)
            {
                bool add = p.Kind == 0 || p.Kind == 2;
                if (add != additive) continue;
                float k = p.T / p.Life, a = p.Solid ? 1f : Mathf.Min(1f, k / .08f) * (k > .6f ? 1f - (k - .6f) / .4f : 1f);
                float s = p.Size * (p.G0 + (p.G1 - p.G0) * k) * unit, sx = s, sy = s, rot = p.Rot;
                if (p.Stretch > 0f) { float sp = Mathf.Sqrt(p.Vx * p.Vx + p.Vy * p.Vy); rot = Mathf.Atan2(p.Vy, p.Vx); sx = s * (1f + sp * p.Stretch); }
                float c = Mathf.Cos(rot), si = Mathf.Sin(rot);
                var col = new Color(p.Col.r, p.Col.g, p.Col.b, a);
                for (int q = 0; q < 4; q++)
                {
                    float qx = q == 0 || q == 3 ? -1f : 1f, qy = q < 2 ? -1f : 1f;
                    float lx = qx * sx, ly = qy * sy;
                    var v = UIVertex.simpleVert;
                    v.position = new Vector3(p.X + c * lx - si * ly, p.Y + si * lx + c * ly, 0f);
                    v.color = col; v.uv0 = new Vector4(qx, qy, 0f, 0f); v.uv1 = new Vector4(p.Kind, 0f, 0f, 0f);
                    verts.Add(v);
                }
            }
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            for (int i = 0; i + 3 < verts.Count; i += 4)
            {
                vh.AddVert(verts[i]); vh.AddVert(verts[i + 1]); vh.AddVert(verts[i + 2]); vh.AddVert(verts[i + 3]);
                vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3);
            }
        }
    }
}
