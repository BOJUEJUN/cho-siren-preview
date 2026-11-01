using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>
    /// Selected-stage fire (owner 2026-10-20: the flame-frame texture looked pasted on). Live
    /// particle flames rise from the plaque's top edge and both ends, following its slant, over a
    /// warm glow; tongues cool from white-gold to pink-red as they climb.
    /// </summary>
    public sealed class MapSelectedFlame1020 : MonoBehaviour
    {
        private const int Count = 46;
        private static Sprite softSprite, tongueSprite;
        private readonly Image[] parts = new Image[Count];
        private readonly float[] age = new float[Count], life = new float[Count], speed = new float[Count],
            size = new float[Count], seed = new float[Count];
        private readonly Vector2[] origin = new Vector2[Count];
        private Image glow;
        private Vector2 extent = new Vector2(164f, 66f);
        private float skew = 18f; // the plaques lean right: top edge shifted by this many px
        private float clock, appear;

        public void Configure(Vector2 plaqueSize, float plaqueSkew)
        {
            extent = plaqueSize; skew = plaqueSkew;
        }

        private void Awake()
        {
            var image = GetComponent<Image>(); if (image != null) image.enabled = false; // old frame texture
            glow = NewPart("Glow", SoftSprite());
            for (int i = 0; i < Count; i++) { parts[i] = NewPart("Tongue-" + i, i % 4 == 0 ? SoftSprite() : TongueSprite()); Respawn(i, Random.value); }
        }

        private void OnEnable() { appear = 0f; }

        private Image NewPart(string name, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(transform, false);
            var image = go.GetComponent<Image>(); image.sprite = sprite; image.raycastTarget = false; image.color = Color.clear;
            // 2026-10-21: hard-edged cel fire (cutoff + stepped bands); only the glow stays additive.
            if (name != "Glow" && CelMaterials.Cutoff != null) image.material = CelMaterials.Cutoff;
            else if (BattleFxArt1021.Additive != null) image.material = BattleFxArt1021.Additive;
            var rect = image.rectTransform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            return image;
        }

        private void Respawn(int i, float startAge = 0f)
        {
            float hx = extent.x * .5f, hy = extent.y * .5f;
            float pick = Random.value;
            if (pick < .7f)
            {   // along the top edge (shifted by the skew)
                float u = Random.Range(-1f, 1f);
                origin[i] = new Vector2(u * hx * .92f + skew * .5f, hy - 4f);
                life[i] = Random.Range(.45f, .85f) * (1f - .3f * Mathf.Abs(u));
            }
            else
            {   // up the left or right end
                float side = pick < .85f ? -1f : 1f, v = Random.Range(-1f, 1f);
                origin[i] = new Vector2(side * hx * .95f + skew * .5f * v, v * hy * .8f);
                life[i] = Random.Range(.35f, .6f);
            }
            speed[i] = Random.Range(.85f, 1.3f);
            size[i] = Random.Range(18f, 30f);
            seed[i] = Random.value * 100f;
            age[i] = startAge * life[i];
        }

        private void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, .1f);
            bool still = BattlePresentationOptions.ReduceMotion;
            clock += dt; appear = Mathf.Min(1f, appear + dt / .25f);
            float flick = .8f + .2f * Mathf.PerlinNoise(clock * 5f, 1f);
            glow.rectTransform.sizeDelta = new Vector2(extent.x * 1.45f, extent.y * 1.9f);
            glow.rectTransform.anchoredPosition = new Vector2(skew * .25f, 6f);
            glow.color = new Color(.11f, .95f, .93f, .42f * flick * appear);
            for (int i = 0; i < Count; i++)
            {
                if (!still) age[i] += dt * speed[i];
                if (age[i] >= life[i]) Respawn(i);
                float t = Mathf.Clamp01(age[i] / life[i]);
                float sway = (Mathf.PerlinNoise(seed[i], clock * 2.6f) - .5f) * 22f * t + Mathf.Sin(clock * 10f + seed[i]) * 1.6f;
                var rect = parts[i].rectTransform;
                rect.anchoredPosition = origin[i] + new Vector2(sway, t * 54f * speed[i]);
                float s = size[i] * (1f - .6f * t);
                rect.sizeDelta = new Vector2(s * 1.05f, s * 2f); // cel cutoff trims the soft rim, so draw a little larger
                rect.localEulerAngles = new Vector3(0f, 0f, -sway * 1.2f);
                Color c = t < .22f ? new Color(.9f, 1f, 1f) : t < .48f ? new Color(.55f, 1f, .97f)
                    : t < .74f ? new Color(.11f, .95f, .93f) : new Color(0f, .5f, .55f);
                c.a = Mathf.Clamp01(t / .12f) * (1f - Mathf.SmoothStep(.5f, 1f, t)) * appear;
                parts[i].color = c;
            }
        }

        private static Sprite SoftSprite()
        {
            if (softSprite != null) return softSprite;
            return softSprite = Make(128, (u, v) => Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(u * u + v * v)), 2f));
        }

        private static Sprite TongueSprite()
        {
            if (tongueSprite != null) return tongueSprite;
            return tongueSprite = Make(128, (u, v) =>
            {
                float halfWidth = Mathf.Lerp(.95f, .05f, Mathf.Clamp01((v + 1f) * .5f));
                float across = Mathf.Abs(u) / Mathf.Max(.05f, halfWidth);
                return Mathf.Pow(Mathf.Clamp01(1f - across), 1.6f) * Mathf.Clamp01((1f - Mathf.Abs(v)) * 1.6f);
            });
        }

        private static Sprite Make(int n, System.Func<float, float, float> alpha)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
                px[y * n + x] = new Color(1f, 1f, 1f, alpha((x + .5f) / n * 2f - 1f, (y + .5f) / n * 2f - 1f));
            tex.SetPixels32(px); tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(.5f, .5f), 100f);
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }
    }
}
