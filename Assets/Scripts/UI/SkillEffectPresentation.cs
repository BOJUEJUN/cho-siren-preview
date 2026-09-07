using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    public enum SkillVisualKind { Cast, Impact, Slash, Hex, Pierce, Heal, Shield, Tide, Rift, Thorn, Prism }

    /// <summary>Bounded local effects; never touches combat state or crosses the dice controls.</summary>
    public sealed class SkillEffectPresentation : MonoBehaviour
    {
        public const int Capacity = 20;
        private sealed class Effect
        {
            public SkillEffectGraphic Graphic;
            public Image Accent;
            public RectTransform Target;
            public float Elapsed, Duration, Radius;
        }

        private readonly List<Effect> pool = new List<Effect>();
        private RectTransform layer;
        private Func<bool> paused;
        private Func<int> speed;
        public int ActiveCount { get { int count = 0; foreach (var e in pool) if (e.Graphic.gameObject.activeSelf) count++; return count; } }
        public int PoolCount => pool.Count;
        public int PlayCount { get; private set; }
        public float VisualClock { get; private set; }

        public void Configure(RectTransform host, Func<bool> pause, Func<int> playbackSpeed)
        {
            layer = host != null ? host : throw new ArgumentNullException(nameof(host));
            paused = pause;
            speed = playbackSpeed;
            Clear();
        }

        public void Play(RectTransform target, SkillVisualKind kind, Color color, bool strong = false, Sprite art = null)
        {
            if (layer == null || target == null || !isActiveAndEnabled) return;
            Effect effect = pool.Find(e => !e.Graphic.gameObject.activeSelf);
            if (effect == null)
            {
                if (pool.Count >= Capacity) return;
                var go = new GameObject("SkillEffect-" + pool.Count, typeof(RectTransform), typeof(CanvasRenderer), typeof(SkillEffectGraphic));
                go.transform.SetParent(layer, false);
                var graphic = go.GetComponent<SkillEffectGraphic>();
                graphic.raycastTarget = false;
                graphic.rectTransform.anchorMin = graphic.rectTransform.anchorMax = new Vector2(.5f, .5f);
                graphic.rectTransform.pivot = new Vector2(.5f, .5f);
                var accent = new GameObject("AuthoredAccent", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
                accent.transform.SetParent(go.transform, false);
                accent.rectTransform.anchorMin = accent.rectTransform.anchorMax = new Vector2(.5f, .5f);
                accent.raycastTarget = false;
                accent.preserveAspect = true;
                effect = new Effect { Graphic = graphic, Accent = accent };
                pool.Add(effect);
            }
            effect.Target = target;
            effect.Elapsed = 0;
            effect.Duration = kind == SkillVisualKind.Cast ? .32f : kind == SkillVisualKind.Impact ? .28f : strong ? .8f : .58f;
            effect.Radius = Mathf.Clamp(Mathf.Min(target.rect.width, target.rect.height) * .44f, 30, 90);
            effect.Graphic.rectTransform.sizeDelta = Vector2.one * effect.Radius * 2.8f;
            effect.Graphic.Kind = kind;
            effect.Graphic.Strong = strong;
            effect.Graphic.color = color;
            effect.Accent.sprite = art;
            effect.Accent.enabled = art != null;
            effect.Accent.rectTransform.sizeDelta = Vector2.one * effect.Radius * 2.1f;
            effect.Graphic.gameObject.SetActive(true);
            PlayCount++;
            Apply(effect);
        }

        private void Update() => AdvancePresentation(Time.unscaledDeltaTime);

        public void AdvancePresentation(float seconds)
        {
            if (!isActiveAndEnabled || layer == null || seconds <= 0 || float.IsNaN(seconds) || float.IsInfinity(seconds) || (paused != null && paused())) return;
            float delta = seconds * Mathf.Clamp(speed != null ? speed() : 1, 1, 2);
            VisualClock += delta;
            foreach (var effect in pool)
            {
                if (!effect.Graphic.gameObject.activeSelf) continue;
                effect.Elapsed += delta;
                if (effect.Elapsed >= effect.Duration) effect.Graphic.gameObject.SetActive(false);
                else Apply(effect);
            }
        }

        private void Apply(Effect e)
        {
            if (e.Target != null)
                e.Graphic.rectTransform.anchoredPosition = layer.InverseTransformPoint(e.Target.TransformPoint(e.Target.rect.center));
            float t = Mathf.Clamp01(e.Elapsed / e.Duration);
            e.Graphic.SetFrame(t, e.Radius);
            e.Accent.color = new Color(1, 1, 1, Mathf.Sin(Mathf.PI * t) * .85f);
            bool directional = e.Graphic.Kind == SkillVisualKind.Pierce;
            e.Accent.rectTransform.localScale = directional
                ? new Vector3(Mathf.Lerp(.6f, 1.35f, t), Mathf.Lerp(.9f, .6f, t), 1)
                : Vector3.one * Mathf.Lerp(.65f, 1.25f, t);
            e.Accent.rectTransform.localEulerAngles = new Vector3(0, 0,
                directional ? 32 : e.Graphic.Kind == SkillVisualKind.Tide ? Mathf.Lerp(-55, 35, t) : Mathf.Lerp(-12, 12, t));
        }

        public void Clear()
        {
            foreach (var effect in pool) if (effect.Graphic != null) effect.Graphic.gameObject.SetActive(false);
        }
        private void OnDisable() => Clear();
        private void OnDestroy()
        {
            foreach (var effect in pool) if (effect.Graphic != null) Destroy(effect.Graphic.gameObject);
            pool.Clear();
        }
    }

    /// <summary>Smooth UI mesh ribbons and soft edged particles; no extra textures/materials.</summary>
    public sealed class SkillEffectGraphic : MaskableGraphic
    {
        public SkillVisualKind Kind;
        public bool Strong;
        private float phase, radius;
        public void SetFrame(float progress, float size) { phase = progress; radius = size; SetVerticesDirty(); }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            float fade = Mathf.Sin(Mathf.PI * Mathf.Clamp01(phase));
            Color tint = color; tint.a *= fade;
            float r = radius * Mathf.Lerp(.55f, 1.15f, phase);
            switch (Kind)
            {
                case SkillVisualKind.Cast:
                    Arc(vh, Vector2.zero, r, .48f, phase * 70, 290, 2, tint);
                    break;
                case SkillVisualKind.Impact:
                    Star(vh, Vector2.zero, radius * .28f * (1 + phase), tint);
                    break;
                case SkillVisualKind.Slash:
                    Arc(vh, new Vector2(-r * .12f, 0), r, .62f, -45 + phase * 60, 235, 5, tint);
                    Arc(vh, new Vector2(r * .12f, 0), r * .82f, .72f, 125 - phase * 45, 195, 3, tint);
                    break;
                case SkillVisualKind.Hex:
                    Arc(vh, Vector2.zero, r * .8f, .7f, phase * 75, 340, 3, tint);
                    Arc(vh, Vector2.zero, r, .7f, -phase * 60, 285, 1.5f, tint);
                    for (int i = 0; i < 6; i++) Star(vh, Polar(r * .77f, i * 60 + phase * 50), radius * .09f, tint);
                    break;
                case SkillVisualKind.Pierce:
                    Ribbon(vh, new Vector2(-r, -r * .64f), new Vector2(r, r * .64f), 4, tint);
                    Ribbon(vh, new Vector2(-r * .6f, r * .7f), new Vector2(r * .6f, -r * .7f), 2, tint);
                    Star(vh, Vector2.zero, radius * .35f, tint);
                    break;
                case SkillVisualKind.Tide:
                    for (int i = 0; i < 2; i++)
                        Arc(vh, new Vector2((phase - .5f) * r * .9f, -r * .25f + i * r * .25f), r * (1 - i * .15f), .4f, 10 + phase * 85, 210, 6 - i, tint);
                    break;
                case SkillVisualKind.Rift:
                    for (int i = 0; i < 3; i++)
                    {
                        float x = (i - 1) * r * .38f;
                        Ribbon(vh, new Vector2(x - r * .2f, -r), new Vector2(x + r * .35f, r * .9f), (1 - phase) * 8 + 1, tint);
                    }
                    break;
                case SkillVisualKind.Thorn:
                    for (int i = 0; i < 7; i++)
                    {
                        float angle = i * 360f / 7;
                        Vector2 p = Polar(r, angle);
                        Ribbon(vh, p, Polar(r * .2f, angle + 25), 4, tint);
                        Star(vh, p, radius * .12f, tint);
                    }
                    break;
                case SkillVisualKind.Prism:
                    for (int i = 0; i < 4; i++)
                    {
                        Vector2 p = Polar(r * .8f, 45 + i * 90 + phase * 40);
                        Ribbon(vh, p + Vector2.up * r * .32f, p + Vector2.right * r * .12f, 3, tint);
                        Ribbon(vh, p + Vector2.right * r * .12f, p - Vector2.up * r * .32f, 3, tint);
                        Ribbon(vh, p - Vector2.up * r * .32f, p - Vector2.right * r * .12f, 3, tint);
                        Ribbon(vh, p - Vector2.right * r * .12f, p + Vector2.up * r * .32f, 3, tint);
                    }
                    break;
                case SkillVisualKind.Heal:
                    Arc(vh, new Vector2(0, -radius * .45f + phase * radius * .3f), r, .28f, phase * 100, 330, 3, tint);
                    for (int i = 0; i < 3; i++)
                    {
                        Vector2 p = new Vector2((i - 1) * radius * .48f, (phase - .4f) * radius + i * radius * .14f);
                        Ribbon(vh, p - Vector2.right * 5, p + Vector2.right * 5, 2, tint);
                        Ribbon(vh, p - Vector2.up * 6, p + Vector2.up * 6, 2, tint);
                    }
                    break;
                case SkillVisualKind.Shield:
                    Vector2[] shield = { new Vector2(0, .8f), new Vector2(.62f, .45f), new Vector2(.48f, -.35f), new Vector2(0, -.8f), new Vector2(-.48f, -.35f), new Vector2(-.62f, .45f) };
                    for (int i = 0; i < shield.Length; i++) Ribbon(vh, shield[i] * r, shield[(i + 1) % shield.Length] * r, 2.5f, tint);
                    Arc(vh, Vector2.zero, r * 1.08f, .82f, phase * 90, 255, 2, tint);
                    break;
            }
            if (Kind == SkillVisualKind.Cast || Kind == SkillVisualKind.Impact) return;
            int count = Strong ? 8 : 5;
            for (int i = 0; i < count; i++)
            {
                float angle = i * 137.5f + phase * 22;
                Vector2 point = Polar(radius * (.45f + phase * .85f), angle);
                Star(vh, point, Strong ? 4 : 2.8f, tint);
            }
        }

        private static Vector2 Polar(float r, float degrees) => new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad)) * r;

        private static void Arc(VertexHelper vh, Vector2 origin, float r, float aspect, float start, float sweep, float width, Color tint)
        {
            const int segments = 40;
            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments, b = (i + 1) / (float)segments;
                Vector2 p = Polar(r, start + sweep * a), q = Polar(r, start + sweep * b);
                p.y *= aspect; q.y *= aspect;
                Ribbon(vh, origin + p, origin + q, width * Mathf.Max(.08f, Mathf.Sin(a * Mathf.PI)), tint);
            }
        }

        private static void Star(VertexHelper vh, Vector2 p, float size, Color tint)
        {
            Ribbon(vh, p - Vector2.up * size, p + Vector2.up * size, size * .28f, tint);
            Ribbon(vh, p - Vector2.right * size * .65f, p + Vector2.right * size * .65f, size * .25f, tint);
        }

        private static void Ribbon(VertexHelper vh, Vector2 a, Vector2 b, float width, Color tint)
        {
            Vector2 delta = b - a;
            if (delta.sqrMagnitude < .0001f) return;
            Vector2 normal = new Vector2(-delta.y, delta.x).normalized;
            Quad(vh, a, b, normal * (width + 3), new Color(tint.r, tint.g, tint.b, tint.a * .12f));
            Quad(vh, a, b, normal * width, tint);
            Quad(vh, a, b, normal * width * .25f, Color.Lerp(tint, new Color(1, 1, 1, tint.a), .72f));
        }

        private static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 halfWidth, Color tint)
        {
            int index = vh.currentVertCount;
            vh.AddVert(a - halfWidth, tint, Vector2.zero); vh.AddVert(a + halfWidth, tint, Vector2.zero);
            vh.AddVert(b + halfWidth, tint, Vector2.zero); vh.AddVert(b - halfWidth, tint, Vector2.zero);
            vh.AddTriangle(index, index + 1, index + 2); vh.AddTriangle(index, index + 2, index + 3);
        }
    }
}
