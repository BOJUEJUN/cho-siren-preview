using UnityEngine;

namespace ChoSiren.Panels
{
    /// <summary>
    /// Audition-owned helpers for the 0.3.8 approved full-page board. The prev/next plaques
    /// are baked into the artwork, so the live layer only needs a small always-visible
    /// chevron that <see cref="TactileVisualMotion"/> can scale/lift on hover and press.
    /// Everything is generated from white silhouettes tinted by Image.color.
    /// </summary>
    internal static class AuditionPolish038
    {
        private const int ChevronSize = 64;
        private static Sprite chevronLeft;
        private static Sprite chevronRight;

        /// <summary>Outlined double-chevron; <paramref name="pointLeft"/> picks the direction.</summary>
        public static Sprite Chevron(bool pointLeft)
        {
            if (pointLeft && chevronLeft != null) return chevronLeft;
            if (!pointLeft && chevronRight != null) return chevronRight;

            float[] alpha = new float[ChevronSize * ChevronSize];
            for (int stripe = 0; stripe < 2; stripe++)
            {
                float tip = 10f + stripe * 18f;
                for (int y = 0; y < ChevronSize; y++)
                {
                    for (int x = 0; x < ChevronSize; x++)
                    {
                        float px = pointLeft ? x + .5f : ChevronSize - x - .5f;
                        float py = y + .5f;
                        if (py < 10f || py > 54f) continue;
                        float spine = tip + Mathf.Abs(py - 32f) * .82f;
                        float distance = Mathf.Abs(px - spine);
                        float coverage = Mathf.Clamp01(3.6f - distance + .6f);
                        if (coverage > alpha[y * ChevronSize + x])
                            alpha[y * ChevronSize + x] = coverage;
                    }
                }
            }

            var pixels = new Color32[ChevronSize * ChevronSize];
            for (int index = 0; index < alpha.Length; index++)
            {
                byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha[index]) * 255f);
                pixels[index] = new Color32(255, 255, 255, a);
            }

            var texture = new Texture2D(ChevronSize, ChevronSize, TextureFormat.RGBA32, false, true)
            {
                name = "AuditionPolish038-Chevron-Texture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, ChevronSize, ChevronSize),
                Vector2.one * .5f, 100f, 0, SpriteMeshType.FullRect);
            sprite.name = "AuditionPolish038-Chevron" + (pointLeft ? "-L" : "-R");
            sprite.hideFlags = HideFlags.HideAndDontSave;
            if (pointLeft) chevronLeft = sprite; else chevronRight = sprite;
            return sprite;
        }
    }
}
