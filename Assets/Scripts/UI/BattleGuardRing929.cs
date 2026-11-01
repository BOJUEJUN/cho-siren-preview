using System;
using ChoSiren.Systems.Tactics;
using UnityEngine;
using UnityEngine.UI;
namespace ChoSiren
{
    /// <summary>
    /// 应急守护 shield (meeting row 21). Reads as a shield: a neon double-outlined crest stamps
    /// onto each member card while a glowing rim hugs the card edge (Hearthstone divine-shield
    /// style). Hits flash both and jolt the crest; when the guard ends the crest shrinks and
    /// fades. Art: vfx-shield-crest, vfx-shield-frame (9-sliced), vfx-sparkle.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BattleGuardRing929 : MaskableGraphic
    {
        BattleSimulator battle; Func<BattleUnit> unit; Func<bool> paused; Func<int> speed;
        bool seen; float age, fracture = -1, hit;
        Image crest, frame; Image[] sparkles;
        public bool Active => battle != null && battle.GuardRemaining(unit?.Invoke()) > 0;

        public void Configure(BattleSimulator model, Func<BattleUnit> actor, Func<bool> pause, Func<int> playbackSpeed)
        {
            battle = model; unit = actor; paused = pause; speed = playbackSpeed; raycastTarget = false;
            BuildLayers();
        }

        public void PulseHit() { if (Active) hit = 1; }

        // 2026-10-24 (赵金): in turn battles the shield blocks one hit and then visibly breaks — the crest splits into
        // nine pieces that fly off with spin and gravity, over a white flash and a cyan shock ring.
        struct Shard { public Image Image; public Vector2 Velocity, Home; public float Spin; }
        readonly System.Collections.Generic.List<Shard> shards = new System.Collections.Generic.List<Shard>();
        Image shatterRing;
        float shatterAge = -1f;

        public void Shatter()
        {
            if (crest == null || crest.sprite == null) return;
            foreach (Shard old in shards) if (old.Image != null) Destroy(old.Image.gameObject);
            shards.Clear();
            Rect r = crest.sprite.textureRect;
            Vector2 size = crest.rectTransform.sizeDelta, centre = crest.rectTransform.anchoredPosition;
            if (size.x < 1f) { float w = Mathf.Min(rectTransform.rect.width * .3f, rectTransform.rect.height * .24f); size = new Vector2(w, w * 576f / 512f); centre = new Vector2(0f, w * .25f); }
            var rng = new System.Random(UnityEngine.Random.Range(1, int.MaxValue));
            for (int y = 0; y < 3; y++)
            for (int x = 0; x < 3; x++)
            {
                var piece = Sprite.Create(crest.sprite.texture, new Rect(r.x + r.width * x / 3f, r.y + r.height * y / 3f, r.width / 3f, r.height / 3f),
                    new Vector2(.5f, .5f), crest.sprite.pixelsPerUnit);
                piece.hideFlags = HideFlags.DontSave;
                Image img = Layer("ShieldShard", transform, piece);
                img.rectTransform.anchorMin = img.rectTransform.anchorMax = crest.rectTransform.anchorMin;
                img.rectTransform.sizeDelta = size / 3f * 1.35f;
                Vector2 offset = new Vector2((x - 1) * size.x / 3f, (y - 1) * size.y / 3f);
                Vector2 dir = offset.sqrMagnitude > 1f ? offset.normalized : new Vector2(0f, 1f);
                shards.Add(new Shard
                {
                    Image = img, Home = centre + offset,
                    Velocity = (dir * (220f + 160f * (float)rng.NextDouble()) + new Vector2(((float)rng.NextDouble() - .5f) * 120f, 140f)),
                    Spin = ((float)rng.NextDouble() - .5f) * 900f,
                });
            }
            if (shatterRing == null)
            {
                shatterRing = Layer("ShieldShatterRing", transform, ReferenceArt038.Load("Art/Reference038/vfx-sparkle"));
                shatterRing.rectTransform.anchorMin = shatterRing.rectTransform.anchorMax = crest.rectTransform.anchorMin;
                if (BattleFxArt1021.Additive != null) shatterRing.material = BattleFxArt1021.Additive;
            }
            shatterRing.rectTransform.anchoredPosition = centre;
            shatterAge = 0f;
            fracture = 1f; seen = false;   // skip the old shrink-out: the crest is already in pieces
            SetAlpha(crest, 0);
        }

        void AdvanceShatter(float dt)
        {
            if (shatterAge < 0f) return;
            shatterAge += dt;
            float t = shatterAge;
            foreach (Shard sh in shards)
            {
                if (sh.Image == null) continue;
                var rt = sh.Image.rectTransform;
                rt.anchoredPosition = sh.Home + sh.Velocity * t + new Vector2(0f, -900f * t * t);
                rt.localEulerAngles = new Vector3(0f, 0f, sh.Spin * t);
                float a = 1f - Mathf.Clamp01((t - .25f) / .45f);
                sh.Image.color = new Color(t < .05f ? 1.6f : 1f, 1f, 1f, a);
            }
            // the card-wide rim flashes white and fades with the break
            if (frame != null) frame.color = new Color(1f, 1f, 1f, Mathf.Clamp01(1f - t / .3f) * .9f);
            if (shatterRing != null)
            {
                float k = Mathf.Clamp01(t / .3f);
                float side = crest.rectTransform.sizeDelta.x * Mathf.Lerp(.6f, 2.4f, EaseOut(k));
                shatterRing.rectTransform.sizeDelta = new Vector2(side, side);
                shatterRing.color = new Color(.55f, .95f, 1f, 1f - k);
            }
            if (t > .75f)
            {
                foreach (Shard sh in shards) if (sh.Image != null) Destroy(sh.Image.gameObject);
                shards.Clear();
                if (shatterRing != null) SetAlpha(shatterRing, 0f);
                shatterAge = -1f;
            }
        }

        void Update() => Advance(Time.unscaledDeltaTime);

        public void Advance(float seconds)
        {
            if (paused?.Invoke() ?? false) return;
            float dt = seconds * (speed?.Invoke() ?? 1);
            if (Active)
            {
                // Card art, bars and labels are re-ordered while the battle runs; the shield
                // must sit over the whole card the moment the guard goes up.
                if (!seen) { age = 0; transform.SetAsLastSibling(); }
                seen = true; fracture = -1; age += dt;
            }
            else if (seen) { seen = false; fracture = 0; }
            if (fracture >= 0) fracture += dt;
            hit = Mathf.Max(0, hit - dt * 4);
            Present();
            AdvanceShatter(seconds);
        }

        // The graphic itself draws nothing; the layered sprites carry the look.
        protected override void OnPopulateMesh(VertexHelper vh) => vh.Clear();

        void BuildLayers()
        {
            if (crest != null) return;
            frame = Layer("ShieldFrame", transform, Sliced(ReferenceArt038.Load("Art/Reference038/vfx-shield-frame"), 120));
            frame.type = Image.Type.Sliced;
            frame.pixelsPerUnitMultiplier = 2.6f;
            frame.rectTransform.anchorMin = Vector2.zero;
            frame.rectTransform.anchorMax = Vector2.one;
            frame.rectTransform.offsetMin = new Vector2(-14, -14);
            frame.rectTransform.offsetMax = new Vector2(14, 14);
            crest = Layer("ShieldCrest", transform, ReferenceArt038.Load("Art/Reference038/vfx-shield-crest"));
            crest.preserveAspect = true;
            // Over her head on the card's top edge, not across the face (2026-10-23 review).
            crest.rectTransform.anchorMin = crest.rectTransform.anchorMax = new Vector2(.5f, 1f);
            Sprite glint = ReferenceArt038.Load("Art/Reference038/vfx-sparkle");
            sparkles = new Image[3];
            for (int i = 0; i < sparkles.Length; i++)
            {
                sparkles[i] = Layer("ShieldSparkle" + i, crest.transform, glint);
                sparkles[i].rectTransform.anchorMin = sparkles[i].rectTransform.anchorMax = new Vector2(.5f, .5f);
                sparkles[i].rectTransform.sizeDelta = new Vector2(30, 30);
            }
            Present();
        }

        /// <summary>Same texture with a 9-slice border so the glow keeps its width on any card size.</summary>
        static Sprite Sliced(Sprite source, float border)
        {
            if (source == null || source.texture == null) return source;
            var sprite = Sprite.Create(source.texture, source.textureRect, new Vector2(.5f, .5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }

        static Image Layer(string name, Transform parent, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            image.color = new Color(1, 1, 1, 0);
            image.enabled = sprite != null;
            return image;
        }

        void Present()
        {
            if (crest == null) return;
            bool active = Active;
            bool breaking = !active && fracture >= 0 && fracture <= .4f;
            if (!active && !breaking)
            {
                SetAlpha(crest, 0); SetAlpha(frame, 0);
                foreach (Image s in sparkles) SetAlpha(s, 0);
                return;
            }
            bool still = BattlePresentationOptions.ReduceMotion;
            Rect rect = rectTransform.rect;
            float w = Mathf.Min(rect.width * .3f, rect.height * .24f);
            crest.rectTransform.sizeDelta = new Vector2(w, w * 576f / 512f);
            crest.rectTransform.anchoredPosition = new Vector2(0f, w * .25f);
            float appear = active ? Mathf.Clamp01(age / .25f) : 1f;
            float burst = breaking ? Mathf.Clamp01(fracture / .4f) : 0f;
            // Stamp in from large, breathe while up, jolt on hits; shrink and fade on break.
            float pulse = still ? 0f : Mathf.Sin(age * 3.2f) * .5f + .5f;
            float scale = active
                ? Mathf.Lerp(1.7f, 1f, EaseOut(appear)) + pulse * .03f + hit * .12f
                : 1f - burst * .35f;
            float alpha = active ? appear : 1f - burst;
            crest.rectTransform.localScale = Vector3.one * scale;
            crest.rectTransform.localRotation = Quaternion.Euler(0, 0, hit * Mathf.Sin(age * 60f) * 6f);
            crest.color = new Color(1f, 1f, 1f, alpha * (.9f + hit * .1f));
            frame.color = new Color(1f, 1f, 1f, alpha * (.55f + pulse * .3f + hit * .15f));
            for (int i = 0; i < sparkles.Length; i++)
            {
                float phase = (age * .8f + i * .33f) % 1f;
                float blink = breaking ? 0f : Mathf.Sin(phase * Mathf.PI);
                Vector2[] spots = { new Vector2(-.3f, .32f), new Vector2(.33f, .22f), new Vector2(.05f, -.2f) };
                sparkles[i].rectTransform.anchoredPosition = new Vector2(spots[i].x * w, spots[i].y * w * 1.12f);
                sparkles[i].rectTransform.localScale = Vector3.one * (.4f + blink * .9f);
                SetAlpha(sparkles[i], (still ? 0 : blink) * alpha);
            }
        }

        static float EaseOut(float t) => 1f - (1f - t) * (1f - t) * (1f - t);

        static void SetAlpha(Image image, float a)
        {
            if (image == null) return;
            Color c = image.color; c.a = a; image.color = c;
        }
    }
}
