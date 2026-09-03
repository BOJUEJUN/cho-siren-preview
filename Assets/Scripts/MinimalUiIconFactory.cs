using System;
using UnityEngine;

namespace ChoSiren
{
    /// <summary>
    /// Creates small, dependency-free toolbar icons for Unity UI Images.
    /// The artwork is rendered at 4x resolution and averaged down so diagonal
    /// strokes stay clean at the game's portrait resolution.
    /// </summary>
    public static class MinimalUiIconFactory
    {
        private const int OutputSize = 64;
        private const int Supersampling = 4;
        private const int WorkingSize = OutputSize * Supersampling;

        private static Sprite mailSprite;
        private static Sprite settingsSprite;

        public static Sprite Mail()
        {
            if (mailSprite == null)
            {
                mailSprite = Create("Minimal UI - Mail", DrawMail);
            }

            return mailSprite;
        }

        public static Sprite Settings()
        {
            if (settingsSprite == null)
            {
                settingsSprite = Create("Minimal UI - Settings", DrawSettings);
            }

            return settingsSprite;
        }

        private static Sprite Create(string name, Action<float[]> draw)
        {
            float[] highResolutionAlpha = new float[WorkingSize * WorkingSize];
            draw(highResolutionAlpha);

            Color32[] pixels = new Color32[OutputSize * OutputSize];
            int samplesPerPixel = Supersampling * Supersampling;
            for (int y = 0; y < OutputSize; y++)
            {
                for (int x = 0; x < OutputSize; x++)
                {
                    float alpha = 0f;
                    int originX = x * Supersampling;
                    int originY = y * Supersampling;
                    for (int sampleY = 0; sampleY < Supersampling; sampleY++)
                    {
                        int row = (originY + sampleY) * WorkingSize;
                        for (int sampleX = 0; sampleX < Supersampling; sampleX++)
                        {
                            alpha += highResolutionAlpha[row + originX + sampleX];
                        }
                    }

                    byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha / samplesPerPixel) * 255f);
                    pixels[y * OutputSize + x] = new Color32(255, 255, 255, a);
                }
            }

            Texture2D texture = new Texture2D(OutputSize, OutputSize, TextureFormat.RGBA32, false, true)
            {
                name = name + " Texture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, OutputSize, OutputSize),
                new Vector2(0.5f, 0.5f),
                OutputSize,
                0,
                SpriteMeshType.FullRect);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static void DrawMail(float[] alpha)
        {
            float s = Supersampling;
            float stroke = 2.7f * s;
            Vector2 bottomLeft = new Vector2(11.5f, 17.5f) * s;
            Vector2 bottomRight = new Vector2(52.5f, 17.5f) * s;
            Vector2 topLeft = new Vector2(11.5f, 46.5f) * s;
            Vector2 topRight = new Vector2(52.5f, 46.5f) * s;
            Vector2 center = new Vector2(32f, 31.3f) * s;

            DrawLine(alpha, bottomLeft, topLeft, stroke);
            DrawLine(alpha, topLeft, topRight, stroke);
            DrawLine(alpha, topRight, bottomRight, stroke);
            DrawLine(alpha, bottomRight, bottomLeft, stroke);

            // The two flap strokes meet just below centre, leaving a light,
            // recognisable envelope silhouette without filling the icon.
            DrawLine(alpha, topLeft + new Vector2(1.2f, -1.2f) * s, center, stroke);
            DrawLine(alpha, topRight + new Vector2(-1.2f, -1.2f) * s, center, stroke);
            DrawLine(alpha, bottomLeft + new Vector2(1.2f, 1.2f) * s, new Vector2(25.5f, 28.4f) * s, stroke * 0.82f);
            DrawLine(alpha, bottomRight + new Vector2(-1.2f, 1.2f) * s, new Vector2(38.5f, 28.4f) * s, stroke * 0.82f);
        }

        private static void DrawSettings(float[] alpha)
        {
            float s = Supersampling;
            Vector2 center = new Vector2(32f, 32f) * s;
            float ringRadius = 11.7f * s;
            float ringStroke = 3.15f * s;

            DrawCircle(alpha, center, ringRadius, ringStroke);

            // Eight restrained teeth read as a gear at toolbar size while the
            // open centre keeps the mark lighter than a filled emoji/glyph.
            for (int i = 0; i < 8; i++)
            {
                float radians = i * Mathf.PI / 4f;
                Vector2 direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
                Vector2 from = center + direction * (13.2f * s);
                Vector2 to = center + direction * (20.2f * s);
                DrawLine(alpha, from, to, 4.6f * s);
            }

            DrawCircle(alpha, center, 5.0f * s, 2.8f * s);
        }

        private static void DrawCircle(float[] alpha, Vector2 center, float radius, float width)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt(center.x - radius - width));
            int maxX = Mathf.Min(WorkingSize - 1, Mathf.CeilToInt(center.x + radius + width));
            int minY = Mathf.Max(0, Mathf.FloorToInt(center.y - radius - width));
            int maxY = Mathf.Min(WorkingSize - 1, Mathf.CeilToInt(center.y + radius + width));
            float halfWidth = width * 0.5f;

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    float coverage = Mathf.Clamp01(halfWidth + 0.75f - Mathf.Abs(distance - radius));
                    SetCoverage(alpha, x, y, coverage);
                }
            }
        }

        private static void DrawLine(float[] alpha, Vector2 from, Vector2 to, float width)
        {
            Vector2 delta = to - from;
            float length = delta.magnitude;
            if (length <= 0.001f)
            {
                return;
            }

            int steps = Mathf.Max(1, Mathf.CeilToInt(length * 1.25f));
            float radius = width * 0.5f;
            for (int i = 0; i <= steps; i++)
            {
                StampCircle(alpha, Vector2.Lerp(from, to, i / (float)steps), radius);
            }
        }

        private static void StampCircle(float[] alpha, Vector2 center, float radius)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt(center.x - radius - 1f));
            int maxX = Mathf.Min(WorkingSize - 1, Mathf.CeilToInt(center.x + radius + 1f));
            int minY = Mathf.Max(0, Mathf.FloorToInt(center.y - radius - 1f));
            int maxY = Mathf.Min(WorkingSize - 1, Mathf.CeilToInt(center.y + radius + 1f));

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    float coverage = Mathf.Clamp01(radius + 0.75f - distance);
                    SetCoverage(alpha, x, y, coverage);
                }
            }
        }

        private static void SetCoverage(float[] alpha, int x, int y, float coverage)
        {
            int index = y * WorkingSize + x;
            alpha[index] = Mathf.Max(alpha[index], coverage);
        }
    }
}
