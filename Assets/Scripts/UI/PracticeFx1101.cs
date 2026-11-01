using System;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.UI
{
    /// <summary>
    /// 2026-11-01 practice room (练习室) art helpers: generated white masks tinted by Image.color
    /// (spotlight cone, torn paper edge, starburst rays, vertical fade) plus the shared silhouette
    /// material, so the stage and result card need no new texture files. Everything is cached once.
    /// </summary>
    public static class PracticeFx1101
    {
        private static Sprite cone, torn, rays, fade, slab;
        private static Material silhouette;

        /// <summary>Flat-colour copy of a sprite's alpha (P5 sticker outline / drop shadow).</summary>
        public static Material Silhouette
        {
            get
            {
                if (silhouette != null) return silhouette;
                Shader shader = Resources.Load<Shader>("Shaders/UISilhouette");
                if (shader == null || !shader.isSupported) return null;
                return silhouette = new Material(shader) { name = "PracticeSilhouette1101", hideFlags = HideFlags.DontSave };
            }
        }

        /// <summary>Spotlight cone: narrow source at the top, soft wide pool at the bottom.</summary>
        public static Sprite Cone => cone != null ? cone : cone = Make("PracticeCone1101", 128, 256, (u, v) =>
        {
            float t = (v + 1f) * .5f; // 0 bottom → 1 top
            float half = Mathf.Lerp(.96f, .16f, t);
            float edge = Mathf.Clamp01((half - Mathf.Abs(u)) / (half * .55f));
            return edge * edge * Mathf.Lerp(.42f, 1f, Mathf.Pow(t, .7f)) * Mathf.Clamp01(t * 6f);
        });

        /// <summary>Torn paper strip: solid below a deterministic ragged top edge.</summary>
        public static Sprite TornEdge => torn != null ? torn : torn = Make("PracticeTorn1101", 1024, 48, (u, v) =>
        {
            float x = (u + 1f) * 512f, y = (v + 1f) * 24f;
            float ragged = 26f + 7f * Mathf.Sin(x * .043f) + 4f * Mathf.Sin(x * .17f + 1.3f)
                + 3f * Mathf.Sin(x * .61f + .4f) + ((int)(x / 9f) % 3 == 0 ? 3f : 0f);
            return Mathf.Clamp01(ragged - y);
        });

        /// <summary>Starburst rays for the level-up card.</summary>
        public static Sprite Rays => rays != null ? rays : rays = Make("PracticeRays1101", 256, 256, (u, v) =>
        {
            float d = Mathf.Sqrt(u * u + v * v);
            float angle = Mathf.Atan2(v, u);
            float wedge = Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * 8f)), 6f);
            return wedge * Mathf.Clamp01(1f - d) * Mathf.Clamp01(d * 4f);
        });

        /// <summary>Vertical fade: opaque at the bottom, clear at the top.</summary>
        public static Sprite Fade => fade != null ? fade : fade = Make("PracticeFade1101", 8, 128, (u, v) =>
        {
            float t = (1f - v) * .5f;
            return t * t * (3f - 2f * t);
        });

        /// <summary>Hard parallelogram slab (pop-art backing plate).</summary>
        public static Sprite Slab => slab != null ? slab : slab = Make("PracticeSlab1101", 256, 96, (u, v) =>
        {
            float x = (u + 1f) * 128f, y = (v + 1f) * 48f;
            float left = 4f + y * .22f, right = 252f - (96f - y) * .22f;
            return Mathf.Clamp01(Mathf.Min(x - left, right - x)) * Mathf.Clamp01(Mathf.Min(y - 1f, 95f - y));
        });

        public static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
        {
            var node = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            node.transform.SetParent(parent, false);
            var image = node.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static Text NewText(string name, Transform parent, Font font, string value, int size, Color color,
            TextAnchor anchor = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Bold)
        {
            var node = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            node.transform.SetParent(parent, false);
            var text = node.GetComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = anchor;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = true;
            return text;
        }

        public static void Ink(Text text, float size = 1.6f)
        {
            if (text == null) return;
            var outline = text.GetComponent<Outline>();
            if (outline == null) outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color32(0, 0, 0, 235);
            outline.effectDistance = new Vector2(size, -size);
        }

        /// <summary>Top-left placement inside the parent, y measured downward.</summary>
        public static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        /// <summary>Centre placement inside the parent's top-left space, y measured downward.</summary>
        public static void PlaceCenter(RectTransform rect, float cx, float cy, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(cx, -cy);
            rect.sizeDelta = new Vector2(width, height);
        }

        public static void Stretch(RectTransform rect, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(right, top);
        }

        public static float EaseOutBack(float t)
        {
            t = Mathf.Clamp01(t);
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        public static float EaseOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - Mathf.Pow(1f - t, 3f);
        }

        private static Sprite Make(string name, int w, int h, Func<float, float, float> alpha)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + .5f) / w * 2f - 1f, v = (y + .5f) / h * 2f - 1f;
                    px[y * w + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha(u, v)) * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }
    }

    /// <summary>
    /// Four-axis stat diamond (声能 / 律动 / 气场 / 共鸣) on the 0–100 stage scale: two guide
    /// rings, the axes, a translucent value fill and a bright outline. Values are 0..1.
    /// </summary>
    public sealed class PracticeRadar1101 : MaskableGraphic
    {
        private readonly float[] values = { .5f, .5f, .5f, .5f };
        public Color GridColor = new Color(1f, 1f, 1f, .22f);
        public Color FillColor = new Color32(28, 242, 236, 110);
        public Color LineColor = new Color32(28, 242, 236, 255);
        /// <summary>Optional per-axis tip colours (matching the stat bar legend).</summary>
        public Color[] AxisColors;

        public float Value(int axis) => values[Mathf.Clamp(axis, 0, 3)];

        public void SetValues(float a, float b, float c, float d)
        {
            values[0] = Mathf.Clamp01(a); values[1] = Mathf.Clamp01(b);
            values[2] = Mathf.Clamp01(c); values[3] = Mathf.Clamp01(d);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            Vector2 c = r.center;
            float radius = Mathf.Min(r.width, r.height) * .5f - 2f;
            Vector2[] axis = { Vector2.up, Vector2.right, Vector2.down, Vector2.left };

            // Guide diamonds at 50% and 100%, then the four axes.
            foreach (float ring in new[] { .5f, 1f })
                for (int i = 0; i < 4; i++)
                    Line(vh, c + axis[i] * radius * ring, c + axis[(i + 1) % 4] * radius * ring, 1.4f, GridColor);
            for (int i = 0; i < 4; i++) Line(vh, c, c + axis[i] * radius, 1f, GridColor);

            Vector2[] p = new Vector2[4];
            for (int i = 0; i < 4; i++) p[i] = c + axis[i] * radius * Mathf.Max(.06f, values[i]);
            int start = vh.currentVertCount;
            vh.AddVert(c, FillColor, Vector2.zero);
            for (int i = 0; i < 4; i++) vh.AddVert(p[i], FillColor, Vector2.zero);
            for (int i = 0; i < 4; i++) vh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % 4);
            for (int i = 0; i < 4; i++) Line(vh, p[i], p[(i + 1) % 4], 2.4f, LineColor);
            for (int i = 0; i < 4; i++)
            {
                Color tip = AxisColors != null && i < AxisColors.Length ? AxisColors[i] : LineColor;
                Dot(vh, c + axis[i] * radius, 4.2f, tip);
                Dot(vh, p[i], 5f, tip);
            }
        }

        private static void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color32 color)
        {
            Vector2 dir = (b - a).normalized;
            Vector2 n = new Vector2(-dir.y, dir.x) * width * .5f;
            int i = vh.currentVertCount;
            vh.AddVert(a - n, color, Vector2.zero);
            vh.AddVert(a + n, color, Vector2.zero);
            vh.AddVert(b + n, color, Vector2.zero);
            vh.AddVert(b - n, color, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i, i + 2, i + 3);
        }

        private static void Dot(VertexHelper vh, Vector2 c, float r, Color32 color)
        {
            int i = vh.currentVertCount;
            vh.AddVert(c + new Vector2(0, r), color, Vector2.zero);
            vh.AddVert(c + new Vector2(r, 0), color, Vector2.zero);
            vh.AddVert(c + new Vector2(0, -r), color, Vector2.zero);
            vh.AddVert(c + new Vector2(-r, 0), color, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i, i + 2, i + 3);
        }
    }
}
