using System;
using ChoSiren.Panels;
using UnityEngine;

namespace ChoSiren
{
    public enum BattleBasicForm1019 { Fireball, PortraitBurst }

    /// <summary>Two bounded basic-attack cues. Owns only cloned visuals, never a unit or its layout.</summary>
    public sealed class BattleBasicAttack1019 : MonoBehaviour
    {
        public const int Capacity = 3;
        sealed class Cue
        {
            public RectTransform Root;
            public UnityEngine.UI.Image Portrait, Glow, Tail;
            public BattleFireballGraphic1019 Fireball;
            public Vector2 From, To;
            public Rect PortraitBounds;
            public float Time;
            public bool Active;
            public BattleBasicForm1019 Form;
            public Color Tint;
        }
        readonly Cue[] cues = new Cue[Capacity];
        RectTransform host, layer;
        PanelKit kit;
        Func<bool> paused;
        Func<int> speed;
        public int ActiveCount { get { int count = 0; foreach (var c in cues) if (c != null && c.Active) count++; return count; } }
        public BattleBasicForm1019 LastForm { get; private set; }
        public float VisualClock { get; private set; }

        public void Configure(RectTransform parent, Func<bool> pause, Func<int> playbackSpeed)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            Cancel(); host = parent; paused = pause; speed = playbackSpeed;
            if (layer != null) { layer.SetParent(host, false); return; }
            kit = new PanelKit("BattleBasic1019");
            layer = kit.NewRect("AuthoredBasicLayer", host); PanelKit.Stretch(layer);
            var group = layer.gameObject.AddComponent<CanvasGroup>(); group.blocksRaycasts = false; group.interactable = false;
            for (int i = 0; i < Capacity; i++)
            {
                var c = new Cue { Root = kit.NewRect("BasicCue-" + i, layer) };
                PanelKit.Stretch(c.Root);
                c.Portrait = kit.NewImage("FullPortraitBurst", c.Root, null, Color.white);
                c.Portrait.preserveAspect = true; c.Portrait.useSpriteMesh = true;
                c.Glow = kit.NewImage("FireballGlow", c.Root, kit.RadialSprite(), Color.clear);
                c.Tail = kit.NewImage("FireballTail", c.Root, kit.RadialSprite(), Color.clear);
                var fire = new GameObject("Fireball", typeof(RectTransform), typeof(CanvasRenderer), typeof(BattleFireballGraphic1019));
                fire.transform.SetParent(c.Root, false); c.Fireball = fire.GetComponent<BattleFireballGraphic1019>();
                foreach (var g in c.Root.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) g.raycastTarget = false;
                c.Root.gameObject.SetActive(false); cues[i] = c;
            }
        }

        public bool Play(RectTransform source, RectTransform target, Sprite fullPortrait, Color tint, BattleBasicForm1019 form,
            RectTransform memberCard = null, BattlePsdArt928.Entry authoredPlacement = null)
        {
            if (host == null || !isActiveAndEnabled || source == null || target == null || BattlePresentationOptions.ReduceMotion) return false;
            Cue c = Array.Find(cues, entry => entry != null && !entry.Active);
            if (c == null) return false; // Never replay stale attacks from a queue.
            if (form == BattleBasicForm1019.PortraitBurst && fullPortrait == null) form = BattleBasicForm1019.Fireball;
            c.From = Point(source, source.rect.center); c.To = Point(target, target.rect.center);
            // L012 is a full figure emerging BEHIND the sleeve, not another small bust
            // attached to the animated portrait's foot. The actual card is the stable anchor.
            RectTransform anchor = memberCard != null ? memberCard : source;
            Vector2 cardTop = Point(anchor, new Vector2(anchor.rect.center.x, anchor.rect.yMax));
            // 2026-10-20: full-size figures covered half the stage. Generic members rise at 60%,
            // feet hidden behind their own card.
            float height = 3727f * (720f / 2946f) * .6f;
            float width = fullPortrait != null ? height * fullPortrait.rect.width / fullPortrait.rect.height : height;
            c.PortraitBounds = new Rect(cardTop.x - width * .5f, cardTop.y + 110f - height, width, height);
            if (authoredPlacement != null)
            {
                const float scale = 720f / 2946f;
                // The exact supplied L012 has its own transparent padding. Retain that
                // native box instead of re-centring the painted figure inside a new crop.
                c.PortraitBounds = new Rect(cardTop.x + (authoredPlacement.x - 1488f - 354f) * scale,
                    cardTop.y + (authoredPlacement.y - 3670f) * scale,
                    authoredPlacement.width * scale, authoredPlacement.height * scale);
            }
            c.Form = LastForm = form; c.Time = 0; c.Tint = tint; c.Active = true;
            c.Portrait.sprite = fullPortrait; c.Root.gameObject.SetActive(true); Draw(c); return true;
        }

        void Update() => AdvancePresentation(Mathf.Min(Time.unscaledDeltaTime, .1f));
        public void AdvancePresentation(float seconds)
        {
            if (BattlePresentationOptions.ReduceMotion) { Cancel(); return; }
            if (host == null || !isActiveAndEnabled || (paused?.Invoke() ?? false) || seconds <= 0 || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            float dt = seconds * Mathf.Clamp(speed?.Invoke() ?? 1, 1, 2) * BattlePresentationOptions.Pace;
            VisualClock += dt;
            foreach (var c in cues)
            {
                if (c == null || !c.Active) continue;
                c.Time += dt;
                if (c.Time >= .62f) { c.Active = false; c.Root.gameObject.SetActive(false); }
                else Draw(c);
            }
        }
        Vector2 Point(RectTransform rect, Vector2 local)
        {
            Vector3 p = host.InverseTransformPoint(rect.TransformPoint(local));
            return new Vector2(p.x - host.rect.xMin, host.rect.yMax - p.y);
        }
        static void Place(RectTransform rect, Vector2 p, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = Vector2.one * .5f;
            rect.anchoredPosition = new Vector2(p.x, -p.y); rect.sizeDelta = size;
        }
        void Draw(Cue c)
        {
            bool burst = c.Form == BattleBasicForm1019.PortraitBurst;
            c.Portrait.gameObject.SetActive(burst); c.Fireball.gameObject.SetActive(!burst);
            c.Glow.gameObject.SetActive(!burst); c.Tail.gameObject.SetActive(!burst);
            float t = Mathf.Clamp01(c.Time / .62f), enter = 1 - Mathf.Pow(1 - Mathf.Clamp01(t / .24f), 3);
            float alpha = Mathf.Clamp01((1 - t) / .36f) * enter;
            if (burst)
            {
                Rect bounds = c.PortraitBounds;
                // PSD L012 native y=2104, height=3727; only translate horizontally with
                // the caster's card. Preserve the source figure's aspect ratio.
                Place(c.Portrait.rectTransform, bounds.center + new Vector2(0, 48 * (1 - enter)), bounds.size);
                c.Portrait.color = new Color(1, 1, 1, alpha * .94f);
                c.Portrait.rectTransform.localEulerAngles = Vector3.zero;
            }
            else
            {
                float travel = Mathf.Clamp01((t - .12f) / .6f);
                Vector2 point = Vector2.Lerp(c.From, c.To, travel);
                point.y -= Mathf.Sin(travel * Mathf.PI) * 28;
                Vector2 direction = c.To - c.From; if (direction.sqrMagnitude < .001f) direction = Vector2.up;
                float angle = Mathf.Atan2(-direction.y, direction.x) * Mathf.Rad2Deg;
                float fade = t > .78f ? (1 - t) / .22f : 1;
                Place(c.Fireball.rectTransform, point, Vector2.one * 30);
                c.Fireball.rectTransform.localEulerAngles = new Vector3(0, 0, angle);
                c.Fireball.color = new Color(c.Tint.r, c.Tint.g, c.Tint.b, fade);
                Place(c.Glow.rectTransform, point, Vector2.one * 58);
                c.Glow.color = new Color(c.Tint.r, c.Tint.g, c.Tint.b, .26f * fade);
                Place(c.Tail.rectTransform, point - direction.normalized * 22, new Vector2(66, 15));
                c.Tail.rectTransform.localEulerAngles = new Vector3(0, 0, angle);
                c.Tail.color = new Color(c.Tint.r, c.Tint.g, c.Tint.b, .48f * fade);
            }
        }
        public void Cancel() { foreach (var c in cues) if (c != null) { c.Active = false; if (c.Root != null) c.Root.gameObject.SetActive(false); } }
        void OnDisable() => Cancel();
        void OnDestroy() { if (layer != null) Destroy(layer.gameObject); kit?.Dispose(); }
    }

    /// <summary>A small hot core with a pointed flame silhouette; no particles or external textures.</summary>
    public sealed class BattleFireballGraphic1019 : UnityEngine.UI.MaskableGraphic
    {
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh)
        {
            vh.Clear(); float r = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * .5f;
            Vector2[] outer = { new Vector2(-1.8f, .15f), new Vector2(-.55f, .48f), new Vector2(-.8f, .95f),
                new Vector2(.25f, .72f), new Vector2(.94f, .1f), new Vector2(.42f, -.62f),
                new Vector2(-.52f, -.82f), new Vector2(-.38f, -.35f) };
            Fan(vh, outer, r, color);
            Fan(vh, new[] { new Vector2(-.65f, .04f), new Vector2(.12f, .45f), new Vector2(.68f, .02f), new Vector2(.08f, -.4f) }, r, new Color(1, .98f, .82f, color.a));
        }
        static void Fan(UnityEngine.UI.VertexHelper vh, Vector2[] outline, float r, Color tint)
        {
            int start = vh.currentVertCount; vh.AddVert(Vector3.zero, tint, Vector2.zero);
            foreach (var p in outline) vh.AddVert(p * r, tint, Vector2.zero);
            for (int i = 0; i < outline.Length; i++) vh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % outline.Length);
        }
    }
}
