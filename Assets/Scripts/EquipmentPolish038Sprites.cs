using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChoSiren
{
    /// <summary>
    /// Procedural crystalline shapes for the 0.3.8 accessory board: the slanted member
    /// ribbon and the angular panel/chip/thumb sprites used by the equipment member
    /// selector. The portrait aperture itself is the prebaked overlay
    /// Art/Reference038/EquipmentPolish038-aperture-overlay (authored rim pixels with a
    /// jagged transparent window), so these helpers only need runtime-built textures.
    /// </summary>
    public static class EquipmentPolish038Sprites
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        /// <summary>
        /// Slanted crystalline ribbon for member name/level/power: a hexagonal shard that
        /// rises toward the right, echoing the aperture's lower rim. Drawn in a 460x150
        /// texture (y up); the sprite is stretched over its placement rect.
        /// </summary>
        public static Sprite Plaque()
        {
            const int w = 460, h = 150;
            if (Cache.TryGetValue("plaque", out Sprite cached)) return cached;
            Vector2[] body =
            {
                new Vector2(8, 66), new Vector2(38, 118), new Vector2(404, 140), new Vector2(452, 88),
                new Vector2(406, 34), new Vector2(52, 12),
            };
            Color32[] pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float d = PolygonDistance(x + .5f, y + .5f, body);
                    float inside = Mathf.Clamp01(-d + .5f);
                    if (inside <= 0f) { pixels[y * w + x] = new Color32(0, 0, 0, 0); continue; }
                    float t = Mathf.Clamp01(y / (float)h);
                    byte r = (byte)Mathf.Lerp(12, 32, t);
                    byte g = (byte)Mathf.Lerp(8, 20, t);
                    byte b = (byte)Mathf.Lerp(38, 74, t);
                    byte a = (byte)(inside * 233f);
                    // Bright crystalline edge: 3px band just inside the outline, stronger on top.
                    float edge = Mathf.Clamp01((-d) / 3f);
                    float rim = (1f - edge) * Mathf.Lerp(.35f, .95f, t);
                    r = (byte)Mathf.Min(255, r + (int)(rim * 118));
                    g = (byte)Mathf.Min(255, g + (int)(rim * 132));
                    b = (byte)Mathf.Min(255, b + (int)(rim * 255 * .9f));
                    // Diagonal glint sweeping down-right across the ribbon.
                    float glint = Mathf.Abs((x + y * 1.9f) - 560f);
                    if (glint < 5f)
                    {
                        r = (byte)Mathf.Min(255, r + 40);
                        g = (byte)Mathf.Min(255, g + 60);
                        b = (byte)Mathf.Min(255, b + 90);
                    }
                    pixels[y * w + x] = new Color32(r, g, b, a);
                }
            }
            // Bright shard tip on the left point.
            Vector2[] tip = { new Vector2(8, 66), new Vector2(38, 118), new Vector2(64, 92), new Vector2(30, 58) };
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (PolygonDistance(x + .5f, y + .5f, tip) < 0f)
                        pixels[y * w + x] = new Color32(150, 220, 255, (byte)(pixels[y * w + x].a * .9f + 40));
                }
            }
            return Cache["plaque"] = Bake("EquipmentPolish038Plaque", w, h, pixels);
        }

        /// <summary>Sliced angular panel with chamfered corners for the selector card.</summary>
        public static Sprite AngularPanel() => Chamfered("panel", 96, 96, 18,
            new Color32(17, 13, 46, 244), new Color32(64, 46, 128, 255), new Color32(150, 130, 255, 255));

        /// <summary>Sliced angular chip for filter/search/pager buttons inside the selector.</summary>
        public static Sprite Chip() => Chamfered("chip", 96, 64, 12,
            new Color32(34, 26, 84, 244), new Color32(92, 66, 170, 255), new Color32(80, 220, 255, 255));

        /// <summary>Sliced darker angular card used for each member row in the selector.</summary>
        public static Sprite MemberCard() => Chamfered("memberCard", 96, 96, 14,
            new Color32(23, 18, 60, 238), new Color32(52, 38, 104, 255), new Color32(120, 96, 210, 255));

        /// <summary>
        /// Chamfered window drawn over selector thumbnails: transparent center, crystalline
        /// ring filling the clipped corners so the bust reads as an angular cut without a
        /// stencil mask. Baked at the exact 76x104 frame size used by the selector grid.
        /// </summary>
        public static Sprite ThumbRing()
        {
            const int w = 76, h = 104;
            if (Cache.TryGetValue("thumbRing", out Sprite cached)) return cached;
            const float c = 12f, inset = 3.5f;
            Vector2[] inner =
            {
                new Vector2(c + inset, inset), new Vector2(w - c - inset, inset),
                new Vector2(w - inset, c + inset), new Vector2(w - inset, h - c - inset),
                new Vector2(w - c - inset, h - inset), new Vector2(c + inset, h - inset),
                new Vector2(inset, h - c - inset), new Vector2(inset, c + inset),
            };
            Color32[] pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float di = PolygonDistance(x + .5f, y + .5f, inner);
                    if (di < -0.5f) { pixels[y * w + x] = new Color32(0, 0, 0, 0); continue; }
                    float rim = Mathf.Clamp01(di / 1.6f);
                    Color32 edge = new Color32(80, 220, 255, 255);
                    Color32 fill = new Color32(26, 20, 64, 255);
                    Color32 col = new Color32(
                        (byte)Mathf.Lerp(edge.r, fill.r, rim),
                        (byte)Mathf.Lerp(edge.g, fill.g, rim),
                        (byte)Mathf.Lerp(edge.b, fill.b, rim), 255);
                    pixels[y * w + x] = col;
                }
            return Cache["thumbRing"] = Bake("EquipmentPolish038ThumbRing", w, h, pixels);
        }

        private static Sprite Chamfered(string key, int w, int h, int chamfer,
            Color32 fill, Color32 edge, Color32 highlight)
        {
            if (Cache.TryGetValue(key, out Sprite cached)) return cached;
            float c = chamfer;
            Vector2[] poly =
            {
                new Vector2(c, 1), new Vector2(w - c, 1), new Vector2(w - 1, c), new Vector2(w - 1, h - c),
                new Vector2(w - c, h - 1), new Vector2(c, h - 1), new Vector2(1, h - c), new Vector2(1, c),
            };
            Color32[] pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float d = PolygonDistance(x + .5f, y + .5f, poly);
                    float inside = Mathf.Clamp01(-d + .5f);
                    if (inside <= 0f) { pixels[y * w + x] = new Color32(0, 0, 0, 0); continue; }
                    float rim = 1f - Mathf.Clamp01((-d) / 2f);
                    Color32 col = rim > .5f ? highlight : (rim > .15f ? edge : fill);
                    pixels[y * w + x] = new Color32(col.r, col.g, col.b, (byte)(col.a * inside));
                }
            }
            int border = Mathf.Max(4, chamfer + 6);
            return Cache[key] = Bake("EquipmentPolish038-" + key, w, h, pixels,
                new Vector4(border, border, border, border));
        }

        /// <summary>Signed distance to polygon (negative inside), in pixel space.</summary>
        private static float PolygonDistance(float px, float py, Vector2[] poly)
        {
            float d = float.MaxValue;
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                Vector2 a = poly[i], b = poly[j];
                Vector2 e = b - a, v = new Vector2(px, py) - a;
                float t = Mathf.Clamp01(Vector2.Dot(v, e) / Mathf.Max(.0001f, e.sqrMagnitude));
                d = Mathf.Min(d, (v - e * t).magnitude);
                if ((a.y > py) != (b.y > py) && px < (b.x - a.x) * (py - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside ? -d : d;
        }

        private static Sprite Bake(string name, int w, int h, Color32[] pixels, Vector4? slice = null)
        {
            Texture2D texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(.5f, .5f),
                100f, 0, SpriteMeshType.FullRect, slice ?? Vector4.zero);
            sprite.name = name;
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }
    }
}
