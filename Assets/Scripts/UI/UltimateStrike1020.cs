using System;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>
    /// After an ultimate's show (owner 2026-10-20): the member's attack visibly flies from her card
    /// to each enemy it hits — fox-fire, poison orb, tide or blood crescent — and bursts on impact
    /// with a white flash. Visual only; damage is already resolved by the battle model.
    /// </summary>
    public sealed class UltimateStrike1020 : MonoBehaviour
    {
        private float Travel = .34f, Burst = .5f, scale = 1f;
        private RectTransform layer, projectile, burst, flash;
        private Image projectileImage, burstImage, flashImage;
        private readonly Image[] trail = new Image[3];
        private Vector2 from, to;
        private float delay, clock;
        private Func<bool> paused;
        private Action impact;
        private bool hit, flip;
        private static readonly System.Collections.Generic.HashSet<string> logged = new System.Collections.Generic.HashSet<string>();

        /// <summary>Basic attack: same flight, about half the size and quicker (per-member art).</summary>
        public static void PlayBasic(RectTransform layer, RectTransform source, RectTransform target, string raceKey,
            Func<bool> isPaused, Action onImpact, float size = .5f) => Play(layer, source, target, raceKey + "-basic", 0f, isPaused, onImpact, size, .24f + (size - .5f) * .2f, .32f + (size - .5f) * .3f);

        public static void Play(RectTransform layer, RectTransform source, RectTransform target, string raceKey,
            float delay, Func<bool> isPaused, Action onImpact, float size = 1f, float travel = .34f, float burst = .5f)
        {
            if (layer == null || source == null || target == null) return;
            Sprite proj = Load(raceKey + "-proj"), boom = Load(raceKey + "-hit");
            if (proj == null && boom == null) { Debug.LogWarning("[Strike] missing art " + raceKey); return; }
            if (logged.Add(raceKey)) Debug.Log("[Strike] " + raceKey);
            var go = new GameObject("UltimateStrike-" + raceKey, typeof(RectTransform));
            go.transform.SetParent(layer, false);
            go.transform.SetAsLastSibling();
            var strike = go.AddComponent<UltimateStrike1020>();
            strike.flip = raceKey == "mermaid";
            strike.scale = size; strike.Travel = travel; strike.Burst = burst;
            strike.layer = layer; strike.delay = delay; strike.paused = isPaused; strike.impact = onImpact;
            strike.from = Local(layer, source); strike.to = Local(layer, target);
            var rect = (RectTransform)go.transform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = Vector2.zero; rect.sizeDelta = Vector2.zero;
            // 2026-10-21 VFX pass: glowing after-images trail the strike; the contact flash is light.
            for (int i = strike.trail.Length - 1; i >= 0; i--)
            {
                strike.trail[i] = strike.Part("Trail-" + i, proj, out _);
                if (BattleFxArt1021.Additive != null) strike.trail[i].material = BattleFxArt1021.Additive;
            }
            strike.projectileImage = strike.Part("Projectile", proj, out strike.projectile);
            strike.flashImage = strike.Part("Flash", SoftDot(), out strike.flash);
            if (BattleFxArt1021.Additive != null) strike.flashImage.material = BattleFxArt1021.Additive;
            strike.burstImage = strike.Part("Burst", boom, out strike.burst);
            strike.Pose();
        }

        private static Vector2 Local(RectTransform layer, RectTransform rect)
        {
            Vector3 world = rect.TransformPoint(rect.rect.center);
            return (Vector2)layer.InverseTransformPoint(world) - layer.rect.center;
        }

        private Image Part(string name, Sprite sprite, out RectTransform rect)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(transform, false);
            var image = go.GetComponent<Image>(); image.sprite = sprite; image.preserveAspect = true; image.raycastTarget = false;
            image.color = new Color(1, 1, 1, 0);
            rect = image.rectTransform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            return image;
        }

        private void Update()
        {
            if (paused != null && paused()) return;
            clock += Mathf.Min(Time.unscaledDeltaTime, .05f);
            Pose();
            if (clock > delay + Travel + Burst + .05f) Destroy(gameObject);
        }

        private void Pose()
        {
            float t = clock - delay;
            float w = (layer != null ? Mathf.Max(400f, layer.rect.width) : 720f) * scale;
            // Flight: eases in like a thrown strike, slight arc, nose along the path.
            float f = Mathf.Clamp01(t / Travel);
            float e = f * f;
            Vector2 dir = to - from;
            Vector2 arc = new Vector2(-dir.y, dir.x).normalized * Mathf.Sin(f * Mathf.PI) * w * .06f;
            projectile.anchoredPosition = Vector2.Lerp(from, to, e) + arc;
            projectile.localEulerAngles = new Vector3(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            projectile.localScale = new Vector3(flip ? -1f : 1f, 1f, 1f); // art whose head points left
            projectile.sizeDelta = new Vector2(w * .5f, w * .25f) * Mathf.Lerp(.7f, 1.05f, f);
            projectileImage.color = new Color(1, 1, 1, t < 0 ? 0f : f < 1f ? Mathf.Clamp01(f / .1f) : 0f);
            for (int i = 0; i < trail.Length; i++)
            {
                float g = Mathf.Clamp01((t - (i + 1) * .035f) / Travel), ge = g * g;
                var r = trail[i].rectTransform;
                r.anchoredPosition = Vector2.Lerp(from, to, ge) + new Vector2(-dir.y, dir.x).normalized * Mathf.Sin(g * Mathf.PI) * w * .06f;
                r.localEulerAngles = projectile.localEulerAngles; r.localScale = projectile.localScale;
                r.sizeDelta = projectile.sizeDelta * (1f - .1f * (i + 1));
                trail[i].color = new Color(1, 1, 1, t < (i + 1) * .035f || g >= 1f ? 0f : (.42f - i * .12f) * Mathf.Clamp01(g / .15f));
            }
            // Impact: burst grows and spins out, a white flash pops on contact.
            float b = Mathf.Clamp01((t - Travel) / Burst);
            bool landed = t >= Travel;
            if (landed && !hit) { hit = true; try { impact?.Invoke(); } catch (Exception ex) { Debug.LogWarning(ex.Message); } }
            burst.anchoredPosition = to;
            burst.sizeDelta = Vector2.one * w * Mathf.Lerp(.25f, .62f, 1f - (1f - b) * (1f - b));
            burst.localEulerAngles = new Vector3(0, 0, b * 25f);
            burstImage.color = new Color(1, 1, 1, landed ? 1f - Mathf.SmoothStep(.45f, 1f, b) : 0f);
            flash.anchoredPosition = to;
            flash.sizeDelta = Vector2.one * w * Mathf.Lerp(.15f, .7f, b);
            flashImage.color = new Color(1, 1, 1, landed ? (1f - b) * .8f : 0f);
        }

        private static Sprite Load(string key)
        {
            Texture2D texture = Resources.Load<Texture2D>("UI/UltFx/" + key);
            return texture == null ? null : Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 100f);
        }

        private static Sprite soft;
        private static Sprite SoftDot()
        {
            if (soft != null) return soft;
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float d = new Vector2(x - n / 2f + .5f, y - n / 2f + .5f).magnitude / (n / 2f);
                px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01(1f - d), 2.2f));
            }
            tex.SetPixels32(px); tex.Apply();
            soft = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(.5f, .5f), 100f);
            soft.hideFlags = HideFlags.DontSave;
            return soft;
        }
    }
}
