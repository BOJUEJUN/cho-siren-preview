using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.Panels
{
    /// <summary>
    /// Shared procedural decorations for the 2026-09-15 crystal/punk reference screens:
    /// slashed panel corners, neon stat glyphs and thin tilted bars. Everything is generated
    /// from white silhouettes tinted by Image.color, so no external art is required.
    /// </summary>
    internal static class ReferencePunkFx
    {
        public enum StatGlyphKind
        {
            Wave,
            Mic,
            Note,
            Spark,
            Gem,
            Lock,
        }

        private const int GlyphSize = 64;
        private static readonly Dictionary<int, Sprite> Glyphs = new Dictionary<int, Sprite>();
        private static Sprite barSprite;
        private static Sprite glowSprite;

        /// <summary>Small white silhouette glyph for one audition stat row.</summary>
        public static Sprite StatGlyph(StatGlyphKind kind)
        {
            int key = (int)kind;
            if (Glyphs.TryGetValue(key, out Sprite cached) && cached != null) return cached;

            float[] alpha = new float[GlyphSize * GlyphSize];
            switch (kind)
            {
                case StatGlyphKind.Wave:
                {
                    // Equalizer bars rising from the baseline.
                    float[] heights = { 18f, 32f, 48f, 26f, 38f };
                    for (int bar = 0; bar < heights.Length; bar++)
                    {
                        float cx = 12f + bar * 10f;
                        float half = heights[bar] * 0.5f;
                        FillRoundBar(alpha, cx - 3.2f, 32f - half, cx + 3.2f, 32f + half, 3.2f);
                    }
                    break;
                }
                case StatGlyphKind.Mic:
                {
                    FillEllipse(alpha, 32f, 21f, 9.5f, 13f);
                    StrokeEllipse(alpha, 32f, 26f, 14.5f, 17f, 3f, true);
                    FillRoundBar(alpha, 29.6f, 42f, 34.4f, 52f, 2.4f);
                    FillRoundBar(alpha, 22f, 52f, 42f, 56f, 2.4f);
                    break;
                }
                case StatGlyphKind.Note:
                {
                    FillEllipse(alpha, 24f, 47f, 10f, 7.5f);
                    FillRoundBar(alpha, 31f, 12f, 35f, 47f, 2f);
                    FillRoundBar(alpha, 35f, 12f, 51f, 21f, 2.6f);
                    break;
                }
                case StatGlyphKind.Spark:
                {
                    // Four-point sparkle: a tall spike plus a wide spike.
                    for (int y = 0; y < GlyphSize; y++)
                    {
                        for (int x = 0; x < GlyphSize; x++)
                        {
                            float dx = Mathf.Abs(x + .5f - 32f);
                            float dy = Mathf.Abs(y + .5f - 32f);
                            bool vertical = dy < 24f && dx < 7f * (1f - dy / 24f);
                            bool horizontal = dx < 24f && dy < 7f * (1f - dx / 24f);
                            if (vertical || horizontal) alpha[y * GlyphSize + x] = 1f;
                        }
                    }
                    break;
                }
                case StatGlyphKind.Gem:
                {
                    Vector2 top = new Vector2(32f, 9f);
                    Vector2 right = new Vector2(53f, 30f);
                    Vector2 bottom = new Vector2(32f, 55f);
                    Vector2 left = new Vector2(11f, 30f);
                    StrokeSegment(alpha, top, right, 3f);
                    StrokeSegment(alpha, right, bottom, 3f);
                    StrokeSegment(alpha, bottom, left, 3f);
                    StrokeSegment(alpha, left, top, 3f);
                    StrokeSegment(alpha, left, right, 2.4f);
                    StrokeSegment(alpha, top, new Vector2(24f, 30f), 2f);
                    StrokeSegment(alpha, top, new Vector2(40f, 30f), 2f);
                    break;
                }
                case StatGlyphKind.Lock:
                {
                    // Padlock: upper shackle arc (texture y grows upward on screen) + solid body.
                    StrokeEllipse(alpha, 32f, 38f, 11f, 13f, 4.5f, true);
                    FillRoundBar(alpha, 18f, 10f, 46f, 34f, 4f);
                    break;
                }
            }

            Sprite sprite = Bake("PunkGlyph-" + kind, alpha, GlyphSize);
            Glyphs[key] = sprite;
            return sprite;
        }

        /// <summary>Soft white bar used for slashes, lightning segments and neon underlines.</summary>
        public static Sprite Bar()
        {
            if (barSprite != null) return barSprite;
            const int size = 16;
            float[] alpha = new float[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dy = Mathf.Abs(y + .5f - size * .5f);
                    alpha[y * size + x] = Mathf.Clamp01(size * .5f - dy - (size * .5f - 4f) + 1.5f);
                }
            }
            barSprite = Bake("PunkBar", alpha, size);
            return barSprite;
        }

        /// <summary>Soft radial glow (shared, cached).</summary>
        public static Sprite Glow()
        {
            if (glowSprite != null) return glowSprite;
            const int size = 96;
            float[] alpha = new float[size * size];
            Vector2 center = Vector2.one * (size - 1) * .5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float normalized = Vector2.Distance(new Vector2(x + .5f, y + .5f), center) / (size * .5f);
                    alpha[y * size + x] = Mathf.Pow(Mathf.Clamp01(1f - normalized), 2.1f);
                }
            }
            glowSprite = Bake("PunkGlow", alpha, size);
            return glowSprite;
        }

        /// <summary>Creates an Image positioned by its top-left corner in parent space.</summary>
        public static Image Deco(Transform parent, string name, Sprite sprite, Color color,
            float x, float y, float width, float height)
        {
            var node = new GameObject(name, typeof(RectTransform));
            node.transform.SetParent(parent, false);
            RectTransform rect = node.GetComponent<RectTransform>();
            rect.localScale = Vector3.one;
            PlaceTop(rect, x, y, width, height);
            Image image = node.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>A straight neon bar between two points measured from the parent's top-left.</summary>
        public static Image Beam(Transform parent, string name, Vector2 from, Vector2 to,
            float thickness, Color color)
        {
            Vector2 localFrom = new Vector2(from.x, -from.y);
            Vector2 localTo = new Vector2(to.x, -to.y);
            Vector2 delta = localTo - localFrom;
            Image line = Deco(parent, name, Bar(), color, 0, 0, 10, 10);
            RectTransform rect = line.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = (localFrom + localTo) * .5f;
            rect.sizeDelta = new Vector2(Mathf.Max(1f, delta.magnitude), thickness);
            rect.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            return line;
        }

        /// <summary>Short diagonal slash centred on a point — the reference "cut" accent.</summary>
        public static Image Slash(Transform parent, string name, float centerX, float centerY,
            float length, float thickness, float degrees, Color color)
        {
            Image slash = Deco(parent, name, Bar(), color, 0, 0, length, thickness);
            RectTransform rect = slash.rectTransform;
            CenterPivot(rect);
            rect.anchoredPosition = new Vector2(centerX, -centerY);
            rect.localEulerAngles = new Vector3(0f, 0f, degrees);
            return slash;
        }

        /// <summary>
        /// Corner cuts: a diagonal neon bar across each corner, like the reference's sliced
        /// panel edges. Rotating around the bar centre keeps the host rect untouched.
        /// </summary>
        public static void CornerCuts(RectTransform host, Color color, float cut = 26f,
            float thickness = 3f, float inset = 4f)
        {
            float width = host.rect.width;
            float height = host.rect.height;
            if (width <= 0f || height <= 0f)
            {
                // Rect may not be laid out yet at build time; fall back to sizeDelta.
                width = host.sizeDelta.x;
                height = host.sizeDelta.y;
            }
            Slash(host, "CutTL", inset + cut * .30f, inset + cut * .30f, cut, thickness, -45f, color);
            Slash(host, "CutTR", width - inset - cut * .30f, inset + cut * .30f, cut, thickness, 45f, color);
            Slash(host, "CutBL", inset + cut * .30f, height - inset - cut * .30f, cut, thickness, 45f, color);
            Slash(host, "CutBR", width - inset - cut * .30f, height - inset - cut * .30f, cut, thickness, -45f, color);
        }

        /// <summary>Tilts a rect around its own centre without moving its placed box.</summary>
        public static void Tilt(RectTransform rect, float degrees)
        {
            CenterPivot(rect);
            rect.localEulerAngles = new Vector3(0f, 0f, degrees);
        }

        public static void PlaceTop(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        public static void CenterPivot(RectTransform rect)
        {
            Vector2 size = rect.sizeDelta;
            rect.pivot = Vector2.one * .5f;
            rect.anchoredPosition += new Vector2(size.x * .5f, -size.y * .5f);
        }

        // ------------------------------------------------------------------ raster

        private static Sprite Bake(string name, float[] alpha, int size)
        {
            var pixels = new Color32[size * size];
            for (int index = 0; index < alpha.Length; index++)
            {
                byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha[index]) * 255f);
                pixels[index] = new Color32(255, 255, 255, a);
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = name + "-Texture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size),
                new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static void FillEllipse(float[] alpha, float cx, float cy, float rx, float ry)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt(cx - rx - 1f));
            int maxX = Mathf.Min(GlyphSize - 1, Mathf.CeilToInt(cx + rx + 1f));
            int minY = Mathf.Max(0, Mathf.FloorToInt(cy - ry - 1f));
            int maxY = Mathf.Min(GlyphSize - 1, Mathf.CeilToInt(cy + ry + 1f));
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float dx = (x + .5f - cx) / rx;
                    float dy = (y + .5f - cy) / ry;
                    float distance = dx * dx + dy * dy;
                    float coverage = Mathf.Clamp01((1f - distance) * 4f + .5f);
                    int index = y * GlyphSize + x;
                    alpha[index] = Mathf.Max(alpha[index], coverage);
                }
            }
        }

        private static void StrokeEllipse(float[] alpha, float cx, float cy, float rx, float ry,
            float width, bool lowerHalfOnly)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt(cx - rx - width - 1f));
            int maxX = Mathf.Min(GlyphSize - 1, Mathf.CeilToInt(cx + rx + width + 1f));
            int minY = Mathf.Max(0, Mathf.FloorToInt(cy - ry - width - 1f));
            int maxY = Mathf.Min(GlyphSize - 1, Mathf.CeilToInt(cy + ry + width + 1f));
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    if (lowerHalfOnly && y + .5f < cy) continue;
                    float dx = (x + .5f - cx) / rx;
                    float dy = (y + .5f - cy) / ry;
                    float distance = Mathf.Abs(dx * dx + dy * dy - 1f);
                    float coverage = Mathf.Clamp01(1f - distance * rx / width + .5f);
                    int index = y * GlyphSize + x;
                    alpha[index] = Mathf.Max(alpha[index], coverage);
                }
            }
        }

        private static void FillRoundBar(float[] alpha, float x0, float y0, float x1, float y1,
            float radius)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(x0, x1) - radius - 1f));
            int maxX = Mathf.Min(GlyphSize - 1, Mathf.CeilToInt(Mathf.Max(x0, x1) + radius + 1f));
            int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(y0, y1) - radius - 1f));
            int maxY = Mathf.Min(GlyphSize - 1, Mathf.CeilToInt(Mathf.Max(y0, y1) + radius + 1f));
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float dx = Mathf.Max(Mathf.Max(x0 - (x + .5f), (x + .5f) - x1), 0f);
                    float dy = Mathf.Max(Mathf.Max(y0 - (y + .5f), (y + .5f) - y1), 0f);
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float coverage = Mathf.Clamp01(radius + .8f - distance);
                    int index = y * GlyphSize + x;
                    alpha[index] = Mathf.Max(alpha[index], coverage);
                }
            }
        }

        private static void StrokeSegment(float[] alpha, Vector2 from, Vector2 to, float width)
        {
            Vector2 delta = to - from;
            float length = delta.magnitude;
            if (length <= .001f) return;
            int steps = Mathf.Max(1, Mathf.CeilToInt(length * 1.6f));
            float radius = width * .5f;
            for (int i = 0; i <= steps; i++)
            {
                Vector2 point = Vector2.Lerp(from, to, i / (float)steps);
                int minX = Mathf.Max(0, Mathf.FloorToInt(point.x - radius - 1f));
                int maxX = Mathf.Min(GlyphSize - 1, Mathf.CeilToInt(point.x + radius + 1f));
                int minY = Mathf.Max(0, Mathf.FloorToInt(point.y - radius - 1f));
                int maxY = Mathf.Min(GlyphSize - 1, Mathf.CeilToInt(point.y + radius + 1f));
                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        float distance = Vector2.Distance(new Vector2(x + .5f, y + .5f), point);
                        float coverage = Mathf.Clamp01(radius + .7f - distance);
                        int index = y * GlyphSize + x;
                        alpha[index] = Mathf.Max(alpha[index], coverage);
                    }
                }
            }
        }
    }
}
