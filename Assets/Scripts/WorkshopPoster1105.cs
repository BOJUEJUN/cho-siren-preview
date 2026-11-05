using System;
using System.Collections.Generic;
using ChoSiren.UI;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>
    /// 2026-11-05 创意工坊 poster look (用户: "如果是 p5 那家公司会怎么做…就这个方向"): asymmetric, slanted, high-contrast
    /// pieces drawn at runtime — parallelogram plates, torn/jagged stickers, slanted colour bands and a halftone dot field.
    /// Colours stay the game's own: ink + paper + teal (solid teal only on the selected item), with a hot pink accent.
    /// </summary>
    public static class WorkshopPoster1105
    {
        public static readonly Color Ink = new Color32(7, 7, 12, 255);
        public static readonly Color Paper = new Color32(244, 242, 234, 255);
        public static readonly Color Hot = new Color32(255, 46, 110, 255);
        public static readonly Color Violet = new Color32(109, 60, 255, 255);
        public static Color Teal => Brand1025.Teal;

        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        /// <summary>A parallelogram leaning right by <paramref name="slant"/> of its height.</summary>
        public static Sprite Slant(float slant = .32f)
        {
            string key = "slant" + slant.ToString("0.00");
            if (Cache.TryGetValue(key, out Sprite s) && s != null) return s;
            const int w = 256, h = 64;
            float d = slant * h;
            s = Polygon(w, h, new[] { new Vector2(d, h), new Vector2(w, h), new Vector2(w - d, 0), new Vector2(0, 0) });
            Cache[key] = s;
            return s;
        }

        /// <summary>A torn sticker outline (ransom-note chip).</summary>
        public static Sprite Jag()
        {
            if (Cache.TryGetValue("jag", out Sprite s) && s != null) return s;
            const int w = 200, h = 100;
            var pts = new[]
            {
                new Vector2(0, h * .92f), new Vector2(w * .06f, h), new Vector2(w * .30f, h * .94f), new Vector2(w * .52f, h),
                new Vector2(w * .78f, h * .93f), new Vector2(w, h), new Vector2(w * .97f, h * .48f), new Vector2(w, 0),
                new Vector2(w * .70f, h * .07f), new Vector2(w * .46f, 0), new Vector2(w * .22f, h * .08f), new Vector2(0, 0),
                new Vector2(w * .03f, h * .5f),
            };
            s = Polygon(w, h, pts);
            Cache["jag"] = s;
            return s;
        }

        /// <summary>One halftone dot on a transparent tile, for a tiled dot field.</summary>
        public static Sprite Dots()
        {
            if (Cache.TryGetValue("dots", out Sprite s) && s != null) return s;
            const int n = 14;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = x + .5f - n * .5f, dy = y + .5f - n * .5f;
                    float a = Mathf.Clamp01(2.2f - Mathf.Sqrt(dx * dx + dy * dy));
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px); tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
            Cache["dots"] = s;
            return s;
        }

        private static Sprite Polygon(int w, int h, Vector2[] pts)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    // 2x2 supersampling for soft edges.
                    int hits = 0;
                    for (int sy = 0; sy < 2; sy++)
                        for (int sx = 0; sx < 2; sx++)
                            if (Inside(pts, x + .25f + sx * .5f, y + .25f + sy * .5f)) hits++;
                    px[y * w + x] = new Color32(255, 255, 255, (byte)(hits * 255 / 4));
                }
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
        }

        private static bool Inside(Vector2[] p, float x, float y)
        {
            bool inside = false;
            for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
                if ((p[i].y > y) != (p[j].y > y) && x < (p[j].x - p[i].x) * (y - p[i].y) / (p[j].y - p[i].y) + p[i].x)
                    inside = !inside;
            return inside;
        }

        // ------------------------------------------------------------------ building blocks

        public static Image Block(Transform parent, string name, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.sprite = sprite; image.color = color; image.raycastTarget = false;
            return image;
        }

        /// <summary>Places by centre in the parent's coordinates (anchored to the parent's centre).</summary>
        public static RectTransform At(RectTransform r, float x, float y, float w, float h, float degrees = 0f)
        {
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f);
            r.anchoredPosition = new Vector2(x, y); r.sizeDelta = new Vector2(w, h);
            r.localRotation = Quaternion.Euler(0f, 0f, degrees);
            return r;
        }

        /// <summary>Places from a corner anchor (0..1) with the pivot on that corner.</summary>
        public static RectTransform Corner(RectTransform r, Vector2 anchor, float x, float y, float w, float h, float degrees = 0f)
        {
            r.anchorMin = r.anchorMax = r.pivot = anchor;
            r.anchoredPosition = new Vector2(x, y); r.sizeDelta = new Vector2(w, h);
            r.localRotation = Quaternion.Euler(0f, 0f, degrees);
            return r;
        }

        public static Text Word(Transform parent, string name, string value, int size, Color color, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            Text t = VisualA1101.Label(parent, name, value, size, color, anchor, FontStyle.Bold);
            t.raycastTarget = false;
            return t;
        }

        /// <summary>A hard offset shadow (poster style), not a soft blur.</summary>
        public static void HardShadow(Graphic g, Color color, float dx, float dy)
        {
            Shadow s = g.gameObject.AddComponent<Shadow>();
            s.effectColor = color; s.effectDistance = new Vector2(dx, dy); s.useGraphicAlpha = true;
        }

        /// <summary>
        /// The main-action ticket: a slanted plate with a paper drop block, a teal frame and an ink face with a teal
        /// caption (the game's "main action" look; solid teal stays for selection).
        /// </summary>
        public static Button Ticket(Transform parent, string name, string caption, string badge, int size, Action click,
            Color? frame = null, Color? face = null, Color? text = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            Image hit = go.GetComponent<Image>(); hit.color = new Color(0f, 0f, 0f, .001f);
            Sprite slant = Slant(.28f);
            Image drop = Block(go.transform, "Drop", slant, Paper); Fill(drop.rectTransform, 8f, -8f, 8f, -8f);
            Image rim = Block(go.transform, "Frame", slant, frame ?? Teal); Fill(rim.rectTransform, 0f, 0f, 0f, 0f);
            Image body = Block(go.transform, "Face", slant, face ?? Ink); Fill(body.rectTransform, 5f, 4f, -5f, -4f);
            var row = new GameObject("Caption", typeof(RectTransform), typeof(HorizontalLayoutGroup)).GetComponent<RectTransform>();
            row.SetParent(go.transform, false); Fill(row, 0f, 0f, 0f, 0f);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter; layout.spacing = 12f;
            layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            Text label = Word(row, "Text", caption, size, text ?? Teal);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.gameObject.AddComponent<LayoutElement>().minHeight = size + 8;
            if (!string.IsNullOrEmpty(badge))
            {
                Image tag = Block(row, "Badge", null, Hot);
                var le = tag.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = 22f + badge.Length * (size * .62f); le.preferredHeight = size + 4;
                Text bt = Word(tag.transform, "Text", badge, Mathf.RoundToInt(size * .62f), Paper);
                VisualA1101.Stretch(bt.rectTransform);
            }
            Button b = go.GetComponent<Button>(); b.transition = Selectable.Transition.None; b.targetGraphic = hit;
            b.onClick.AddListener(() => { VisualA1101.Click(); click?.Invoke(); });
            go.AddComponent<VisualA1101Motion>().Press();
            return b;
        }

        /// <summary>Stretches inside the parent with left/bottom/right/top insets (right/top negative = inset).</summary>
        public static void Fill(RectTransform r, float left, float bottom, float right, float top)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.pivot = new Vector2(.5f, .5f);
            r.offsetMin = new Vector2(left, bottom); r.offsetMax = new Vector2(right, top);
        }

        /// <summary>Slanted bands, a dark wash and a halftone field over the scene image (the poster backdrop).</summary>
        public static RectTransform Backdrop(RectTransform page)
        {
            var root = new GameObject("WorkshopPoster", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(page, false); Fill(root, 0f, 0f, 0f, 0f);
            Image wash = Block(root, "Wash", null, new Color(Ink.r, Ink.g, Ink.b, .62f)); Fill(wash.rectTransform, -60f, -60f, 60f, 60f);
            Image violet = Block(root, "BandViolet", null, new Color(Violet.r, Violet.g, Violet.b, .88f));
            At(violet.rectTransform, 0f, -40f, 1800f, 300f, -14f);
            Image teal = Block(root, "BandTeal", null, new Color(Teal.r, Teal.g, Teal.b, .82f));
            At(teal.rectTransform, 0f, -40f, 1800f, 74f, -14f);
            Image paper = Block(root, "BandPaper", null, Paper);
            At(paper.rectTransform, 0f, -470f, 1800f, 44f, -14f);
            Image dots = Block(root, "Halftone", Dots(), new Color(1f, 1f, 1f, .16f));
            dots.type = Image.Type.Tiled; dots.pixelsPerUnitMultiplier = 1f;
            Fill(dots.rectTransform, 0f, 0f, 0f, 0f);
            return root;
        }
    }
}
