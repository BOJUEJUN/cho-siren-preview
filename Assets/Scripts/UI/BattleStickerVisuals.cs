using UnityEngine;

namespace ChoSiren.UI
{
    /// <summary>Small shared presentation textures. No model state or imported artwork is changed.</summary>
    public static class BattleStickerVisuals
    {
        private static Sprite energy, star;
        public static Sprite EnergyGradient => energy != null ? energy : energy = MakeEnergy();
        public static Sprite Star => star != null ? star : star = MakeStar();

        private static Sprite MakeEnergy()
        {
            const int width = 128, height = 16;
            var pixels = new Color32[width * height];
            Color violet = new Color32(111, 46, 255, 255);
            Color cyan = new Color32(62, 231, 255, 255);
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float t = x / (width - 1f);
                Color color = t < .65f ? Color.Lerp(violet, cyan, t / .65f)
                    : Color.Lerp(cyan, Color.white, (t - .65f) / .35f);
                float edgeX = Mathf.Clamp(x + .5f, 8f, width - 8f);
                float distance = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(edgeX, 8f));
                color.a = Mathf.Clamp01(8.5f - distance);
                color = Color.Lerp(color, new Color(1f, 1f, 1f, color.a), y >= 12 ? .16f : 0f);
                pixels[y * width + x] = color;
            }
            return Bake("BattleSticker-EnergyGradient", width, height, pixels);
        }

        private static Sprite MakeStar()
        {
            const int size = 96;
            var points = new Vector2[10];
            for (int i = 0; i < points.Length; i++)
            {
                float angle = Mathf.PI * .5f + i * Mathf.PI / 5f;
                points[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (i % 2 == 0 ? .47f : .22f);
            }
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2((x + .5f) / size - .5f, (y + .5f) / size - .5f);
                if (!Inside(p, points)) continue;
                pixels[y * size + x] = Inside(p / .83f, points) ? Color.white : Color.black;
            }
            return Bake("BattleSticker-Star", size, size, pixels);
        }

        private static bool Inside(Vector2 p, Vector2[] polygon)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                Vector2 a = polygon[i], b = polygon[j];
                if ((a.y > p.y) != (b.y > p.y) &&
                    p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside;
        }

        private static Sprite Bake(string name, int width, int height, Color32[] pixels)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = name, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, width, height), Vector2.one * .5f, 100f);
            sprite.name = name;
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }
    }
}
