using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>
    /// Basic attacks with a move of their own per race (owner 2026-10-21: "不要每个都是简单的发射出去的点线面").
    /// 魅魔/狐 — three fox-fire wisps fan out and home in one after another;
    /// 恶魔 — no projectile: a rift glows on the target and three claw slashes rip across it;
    /// 血精灵 — a spinning blood crescent with after-images, finished by an X cross-cut;
    /// 人鱼 — a weaving water jet of droplets that bursts into a splash ring and bubbles.
    /// Visual only; <c>onImpact</c> fires at the moment of contact (flash / hit-stop / sound hook).
    /// </summary>
    public sealed class BasicStrike1021 : MonoBehaviour
    {
        enum Style { FoxFire, Claw, Crescent, Tide, Beam, Sonic }

        struct Part { public Image image; public Image under; public float seed; }

        Style style;
        RectTransform layer;
        Vector2 from, to, dir, side;
        float w, clock, impactAt, life;
        Func<bool> paused;
        Action impact;
        bool hit;
        Color tint;
        Sprite proj;
        Image hitArt, glow, ring;
        // false = travel only: the caller draws the contact itself (our side's hits use Fx1023 since 2026-10-23)
        bool contact = true;
        readonly List<Part> parts = new List<Part>(), ghosts = new List<Part>(), bubbles = new List<Part>();
        readonly List<RibbonTrail1022> ribbons = new List<RibbonTrail1022>();

        public static int Live { get; private set; }

        public static bool Play(RectTransform layer, RectTransform source, RectTransform target, string raceKey,
            Func<bool> isPaused, Action onImpact, float size = .5f, float delay = 0f, string hitArtKey = null, Color? tintOverride = null,
            bool contact = true)
        {
            if (layer == null || source == null || target == null) return false;
            var go = new GameObject("BasicStrike-" + raceKey, typeof(RectTransform));
            go.transform.SetParent(layer, false); go.transform.SetAsLastSibling();
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f); rect.sizeDelta = Vector2.zero;
            var strike = go.AddComponent<BasicStrike1021>();
            strike.clock = -Mathf.Max(0f, delay);
            strike.contact = contact;
            strike.Build(layer, source, target, raceKey, isPaused, onImpact, size, hitArtKey, tintOverride);
            return true;
        }

        void Awake() => Live++;
        Material erodeMaterial;
        void OnDestroy()
        {
            Live = Mathf.Max(0, Live - 1);
            if (erodeMaterial != null) Destroy(erodeMaterial);
        }

        static Vector2 Local(RectTransform layer, RectTransform rect) =>
            (Vector2)layer.InverseTransformPoint(rect.TransformPoint(rect.rect.center)) - layer.rect.center;

        static Sprite Load(string key)
        {
            Texture2D texture = Resources.Load<Texture2D>("UI/UltFx/" + key);
            return texture == null ? null : Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 100f);
        }

        Image New(string name, Sprite sprite, Color color, bool light)
        {
            var image = BattleFxArt1021.NewImage(name, transform, sprite, color, light);
            image.preserveAspect = sprite != null && sprite != BattleFxArt1021.Spark;
            var c = image.color; c.a = 0f; image.color = c;
            return image;
        }

        void Build(RectTransform host, RectTransform source, RectTransform target, string raceKey, Func<bool> isPaused, Action onImpact, float size, string hitArtKey, Color? tintOverride)
        {
            layer = host; paused = isPaused; impact = onImpact;
            from = Local(host, source); to = Local(host, target);
            dir = (to - from).sqrMagnitude > 1f ? (to - from).normalized : Vector2.up;
            side = new Vector2(-dir.y, dir.x);
            w = Mathf.Max(400f, host.rect.width) * size;
            style = raceKey == "demon" || raceKey == "enemy-claw" ? Style.Claw : raceKey == "bloodelf" ? Style.Crescent
                : raceKey == "mermaid" ? Style.Tide : raceKey == "enemy-beam" ? Style.Beam : raceKey == "enemy-sonic" ? Style.Sonic : Style.FoxFire;
            tint = style == Style.Claw ? new Color32(176, 92, 255, 255) : style == Style.Crescent ? new Color32(255, 52, 84, 255)
                : style == Style.Tide ? new Color32(70, 214, 255, 255) : style == Style.FoxFire ? new Color32(255, 92, 190, 255)
                : new Color32(255, 70, 160, 255);
            if (tintOverride.HasValue) tint = tintOverride.Value;
            proj = Load(raceKey + "-basic-proj");
            Sprite boom = contact ? Load(hitArtKey ?? raceKey + "-basic-hit") : null;
            glow = New("Glow", BattleFxArt1021.Dot, tint, true);
            switch (style)
            {
                case Style.FoxFire:
                    impactAt = .34f;
                    for (int i = 0; i < 3; i++)
                    {
                        parts.Add(new Part { image = New("Wisp-" + i, proj ?? BattleFxArt1021.Dot, Color.white, false),
                            under = New("WispGlow-" + i, BattleFxArt1021.Dot, tint, true), seed = i });
                        ribbons.Add(RibbonTrail1022.Create(transform, Color.Lerp(tint, Color.white, .35f), w * .2f, true));
                    }
                    break;
                case Style.Claw:
                    impactAt = .18f;
                    for (int i = 0; i < 3; i++)
                        parts.Add(new Part { under = New("ClawEdge-" + i, BattleFxArt1021.Spark, new Color32(14, 4, 24, 255), false),
                            image = New("Claw-" + i, BattleFxArt1021.Spark, Color.Lerp(tint, Color.white, .45f), true), seed = i });
                    break;
                case Style.Crescent:
                    impactAt = .27f;
                    for (int g = 0; g < 3; g++)
                        ghosts.Add(new Part { image = New("CrescentTrail-" + g, proj ?? BattleFxArt1021.Star, tint, true), seed = g });
                    parts.Add(new Part { image = New("Crescent", proj ?? BattleFxArt1021.Star, Color.white, false) });
                    ribbons.Add(RibbonTrail1022.Create(transform, tint, w * .3f, true));
                    for (int i = 0; i < 2; i++)
                        bubbles.Add(new Part { under = New("CrossEdge-" + i, BattleFxArt1021.Spark, new Color32(30, 2, 10, 255), false),
                            image = New("Cross-" + i, BattleFxArt1021.Spark, new Color32(255, 140, 150, 255), true), seed = i });
                    break;
                case Style.Beam:
                    // Charge at the muzzle, then a laser lances straight through the target.
                    impactAt = .2f;
                    parts.Add(new Part { image = New("BeamOuter", BattleFxArt1021.Spark, tint, true) });
                    parts.Add(new Part { image = New("BeamCore", BattleFxArt1021.Spark, Color.white, true) });
                    ring = New("Muzzle", BattleFxArt1021.Dot, Color.Lerp(tint, Color.white, .4f), true);
                    break;
                case Style.Sonic:
                    // Three sound-wave rings pulse down the line and break on the target.
                    impactAt = .34f; // 0.1 s of charge (telegraph) before the waves leave
                    for (int i = 0; i < 3; i++)
                        parts.Add(new Part { image = New("Wave-" + i, BattleFxArt1021.Ring, i == 1 ? Color.white : tint, true), seed = i });
                    bubbles.Add(new Part { image = New("Charge", BattleFxArt1021.Ring, Color.Lerp(tint, Color.white, .3f), true) });
                    ring = New("Break", BattleFxArt1021.Ring, Color.Lerp(tint, Color.white, .4f), true);
                    break;
                default:
                    impactAt = .24f;
                    for (int i = 0; i < 9; i++)
                        parts.Add(new Part { image = New("Drop-" + i, BattleFxArt1021.Dot, i % 3 == 0 ? Color.white : tint, true), seed = i });
                    ring = New("Splash", BattleFxArt1021.Ring, Color.Lerp(tint, Color.white, .4f), true);
                    ribbons.Add(RibbonTrail1022.Create(transform, Color.Lerp(tint, Color.white, .25f), w * .16f, true));
                    for (int i = 0; i < 7; i++)
                        bubbles.Add(new Part { image = New("Bubble-" + i, BattleFxArt1021.Ring, Color.Lerp(tint, Color.white, .5f), true),
                            seed = UnityEngine.Random.Range(0f, 6.28f) });
                    break;
            }
            hitArt = boom != null ? New("Hit", boom, Color.white, false) : null;
            // The hit art breaks apart along noise with a bright rim instead of squashing away (research §4.4).
            if (hitArt != null)
            {
                erodeMaterial = CelMaterials.NewErode(Color.Lerp(tint, Color.white, .55f));
                if (erodeMaterial != null) hitArt.material = erodeMaterial;
            }
            life = impactAt + .45f;
            Pose();
        }

        void Update()
        {
            if (paused != null && paused()) return;
            clock += BattleHitStop.Delta(Mathf.Min(Time.unscaledDeltaTime, .05f));
            Pose();
            if (clock >= life) Destroy(gameObject);
        }

        static void Put(Image image, Vector2 at, Vector2 size, float angle, float alpha)
        {
            if (image == null) return;
            var r = image.rectTransform;
            r.anchoredPosition = at; r.sizeDelta = size; r.localEulerAngles = new Vector3(0, 0, angle);
            var c = image.color; c.a = Mathf.Clamp01(alpha); image.color = c;
        }

        static void Hide(Image image)
        {
            if (image == null) return;
            var c = image.color; c.a = 0f; image.color = c;
        }

        static float Deg(Vector2 v) => Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;

        void Pose()
        {
            float t = clock;
            if (!hit && t >= impactAt)
            {
                hit = true;
                try { impact?.Invoke(); } catch (Exception e) { Debug.LogWarning("Basic strike impact skipped: " + e.Message); }
            }
            switch (style)
            {
                case Style.FoxFire: FoxFire(t); break;
                case Style.Claw: Claw(t); break;
                case Style.Crescent: Crescent(t); break;
                case Style.Beam: Beam(t); break;
                case Style.Sonic: Sonic(t); break;
                default: Tide(t); break;
            }
            // The cel impact owns the contact glow; soft target glows would wash it out (2026-10-21).
            if (t >= impactAt) Hide(glow);
            if (!contact)
            {
                Hide(glow); Hide(ring);
                foreach (var b in bubbles) { Hide(b.image); Hide(b.under); }
                if (style == Style.Claw) foreach (var p in parts) { Hide(p.image); Hide(p.under); }
            }
            // The race's own hit art stamps in (1.3 → 1.0 in 4 frames), holds, then snaps away
            // by squashing flat — never a slow sticker-like zoom and fade.
            float h = t - impactAt;
            float stamp = h < .067f ? Mathf.Lerp(1.3f, 1f, h / .067f) : 1f + .06f * (h - .067f);
            float vanish = Mathf.Clamp01((h - .2f) / .12f);
            if (hitArt != null)
            {
                if (erodeMaterial != null)
                {
                    Put(hitArt, to, Vector2.one * w * .62f * stamp, 8f, h < 0f ? 0f : 1f);
                    erodeMaterial.SetFloat("_Erode", vanish * 1.05f);
                }
                else
                {
                    Put(hitArt, to, Vector2.one * w * .62f * stamp, 8f, h < 0f ? 0f : 1f - vanish * vanish);
                    hitArt.rectTransform.localScale = new Vector3(1f + .25f * vanish, 1f - .9f * vanish, 1f);
                }
            }
        }

        // Three wisps fan out from the caster and converge, landing one after another.
        Vector2 WispAt(int i, float k)
        {
            Vector2 ctrl = Vector2.Lerp(from, to, .45f) + side * (i - 1) * w * .55f + dir * -w * .05f;
            float u = 1f - k;
            return u * u * from + 2f * u * k * ctrl + k * k * to + side * Mathf.Sin(k * Mathf.PI * 3f + i) * w * .02f;
        }

        void FoxFire(float t)
        {
            const float travel = .3f;
            for (int i = 0; i < parts.Count; i++)
            {
                float start = impactAt - travel - (i == 1 ? 0f : i == 0 ? -.05f : -.1f);
                float k = Mathf.Clamp01((t - start) / travel), e = k * k * (3f - 2f * k);
                Vector2 at = WispAt(i, e), ahead = WispAt(i, Mathf.Min(1f, e + .03f));
                bool flying = t >= start && k < 1f;
                Put(parts[i].image, at, new Vector2(w * .3f, w * .15f), Deg(ahead - at), flying ? Mathf.Clamp01((t - start) / .06f) : 0f);
                Put(parts[i].under, at, Vector2.one * w * .26f * (1f + .15f * Mathf.Sin(t * 40f + i)), 0f, flying ? .85f : 0f);
                if (i < ribbons.Count) { if (flying) ribbons[i].Push(at); else if (k >= 1f) ribbons[i].Drain(); }
            }
            Put(glow, to, Vector2.one * w * Mathf.Lerp(.4f, 1.1f, BattleFxArt1021.EaseOut((t - impactAt) / .3f)), 0f,
                t < impactAt ? 0f : .9f * (1f - Mathf.Clamp01((t - impactAt) / .4f)));
        }

        void Claw(float t)
        {
            // Wind-up: a rift glows on the target, then three rakes tear through it.
            float wind = Mathf.Clamp01(t / .1f);
            Put(glow, to, Vector2.one * w * Mathf.Lerp(.2f, .8f, wind), 0f,
                t < impactAt ? .7f * wind : .8f * (1f - Mathf.Clamp01((t - impactAt) / .35f)));
            float angle = -58f;
            Vector2 along = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            Vector2 across = new Vector2(-along.y, along.x);
            for (int i = 0; i < parts.Count; i++)
            {
                float start = .1f + i * .045f;
                float draw = BattleFxArt1021.EaseOut((t - start) / .07f);
                float fade = 1f - Mathf.Clamp01((t - start - .12f) / .22f);
                float len = w * (.95f - Mathf.Abs(i - 1) * .18f) * draw;
                // The slash grows from its upper end downward.
                Vector2 head = to + across * (i - 1) * w * .14f - along * w * .45f;
                Vector2 mid = head + along * len * .5f;
                float a = t < start ? 0f : fade;
                Put(parts[i].under, mid, new Vector2(len * 1.04f, w * .11f), angle, a);
                Put(parts[i].image, mid, new Vector2(len, w * .07f), angle, a);
            }
        }

        void Crescent(float t)
        {
            float travel = impactAt - .02f;
            float k = Mathf.Clamp01((t - .02f) / travel), e = k * k;
            Vector2 at = Vector2.Lerp(from, to, e) + side * Mathf.Sin(k * Mathf.PI) * w * .08f;
            bool flying = t >= .02f && k < 1f;
            float spin = -t * 1440f;
            Put(parts[0].image, at, new Vector2(w * .42f, w * .22f), spin, flying ? 1f : 0f);
            if (ribbons.Count > 0) { if (flying) ribbons[0].Push(at); else if (k >= 1f) ribbons[0].Drain(); }
            for (int g = 0; g < ghosts.Count; g++)
            {
                float gk = Mathf.Clamp01((t - .02f - (g + 1) * .025f) / travel), ge = gk * gk;
                Put(ghosts[g].image, Vector2.Lerp(from, to, ge) + side * Mathf.Sin(gk * Mathf.PI) * w * .08f,
                    new Vector2(w * .42f, w * .22f) * (1f - g * .1f), spin + (g + 1) * 40f, flying ? .5f - g * .14f : 0f);
            }
            // X cross-cut at contact.
            for (int i = 0; i < bubbles.Count; i++)
            {
                float start = impactAt + i * .04f;
                float draw = BattleFxArt1021.EaseOut((t - start) / .06f), fade = 1f - Mathf.Clamp01((t - start - .1f) / .2f);
                float len = w * 1.05f * draw, angle = i == 0 ? 42f : -42f;
                float a = t < start ? 0f : fade;
                Put(bubbles[i].under, to, new Vector2(len * 1.04f, w * .1f), angle, a);
                Put(bubbles[i].image, to, new Vector2(len, w * .065f), angle, a);
            }
            Put(glow, to, Vector2.one * w * .9f, 0f, t < impactAt ? 0f : .75f * (1f - Mathf.Clamp01((t - impactAt) / .3f)));
        }

        void Tide(float t)
        {
            const float travel = .22f;
            for (int i = 0; i < parts.Count; i++)
            {
                float start = impactAt - travel + i * .018f;
                float k = Mathf.Clamp01((t - start) / travel);
                Vector2 at = Vector2.Lerp(from, to, k) + side * Mathf.Sin(k * Mathf.PI * 2f + i * .5f) * w * .06f * (1f - k);
                Put(parts[i].image, at, Vector2.one * w * (.17f - i * .01f), 0f, t >= start && k < 1f ? .95f : 0f);
                if (i == 0 && ribbons.Count > 0) { if (t >= start && k < 1f) ribbons[0].Push(at); else if (k >= 1f) ribbons[0].Drain(); }
            }
            float s = Mathf.Clamp01((t - impactAt) / .36f);
            Put(ring, to, new Vector2(1.6f, 1f) * w * Mathf.Lerp(.2f, .95f, BattleFxArt1021.EaseOut(s)), 0f,
                t < impactAt ? 0f : 1f - s);
            for (int i = 0; i < bubbles.Count; i++)
            {
                float start = impactAt + .03f + i * .03f;
                float k = Mathf.Clamp01((t - start) / .45f);
                float x = Mathf.Cos(bubbles[i].seed) * w * .3f;
                Vector2 at = to + new Vector2(x + Mathf.Sin(k * 6f + i) * 6f, -w * .05f + w * .45f * BattleFxArt1021.EaseOut(k));
                Put(bubbles[i].image, at, Vector2.one * w * (.05f + .02f * (i % 3)), 0f, t < start ? 0f : Mathf.Sin(k * Mathf.PI) * .9f);
            }
            Put(glow, to, Vector2.one * w * .8f, 0f, t < impactAt ? 0f : .8f * (1f - Mathf.Clamp01((t - impactAt) / .3f)));
        }

        void Beam(float t)
        {
            float charge = Mathf.Clamp01(t / .16f);
            Put(ring, from, Vector2.one * w * Mathf.Lerp(.1f, .5f, charge), 0f,
                t < 0f ? 0f : t < .2f ? .9f * charge : .9f * (1f - Mathf.Clamp01((t - .2f) / .2f)));
            float k = Mathf.Clamp01((t - .14f) / .06f), fade = 1f - Mathf.Clamp01((t - .26f) / .2f);
            Vector2 span = to - from;
            float length = span.magnitude * BattleFxArt1021.EaseOut(k) * 1.15f;
            Vector2 mid = from + span.normalized * length * .5f;
            float pulse = 1f + .25f * Mathf.Sin(t * 60f);
            float a = t < .14f ? 0f : fade;
            Put(parts[0].image, mid, new Vector2(length, w * .2f * pulse * fade), Deg(span), a * .9f);
            Put(parts[1].image, mid, new Vector2(length, w * .07f * fade), Deg(span), a);
            Put(glow, to, Vector2.one * w * .9f, 0f, t < impactAt ? 0f : .8f * (1f - Mathf.Clamp01((t - impactAt) / .3f)));
        }

        void Sonic(float t)
        {
            const float travel = .24f;
            // Telegraph: a ring contracts onto the singer before the waves leave.
            if (bubbles.Count > 0)
            {
                float c = Mathf.Clamp01(t / .1f);
                Put(bubbles[0].image, from, Vector2.one * w * Mathf.Lerp(.8f, .2f, c), 0f, t < 0f || c >= 1f ? 0f : .4f + .6f * c);
            }
            for (int i = 0; i < parts.Count; i++)
            {
                float start = impactAt - travel + i * .05f;
                float k = Mathf.Clamp01((t - start) / travel);
                Vector2 at = Vector2.Lerp(from, to, k);
                float size = w * Mathf.Lerp(.25f, .6f, k);
                Put(parts[i].image, at, new Vector2(size, size * .6f), Deg(dir) + 90f, t >= start && k < 1f ? .85f : 0f);
            }
            float s = Mathf.Clamp01((t - impactAt) / .35f);
            Put(ring, to, Vector2.one * w * Mathf.Lerp(.3f, 1.2f, BattleFxArt1021.EaseOut(s)), 0f, t < impactAt ? 0f : 1f - s);
            Put(glow, to, Vector2.one * w * .8f, 0f, t < impactAt ? 0f : .7f * (1f - Mathf.Clamp01((t - impactAt) / .3f)));
        }
    }
}
