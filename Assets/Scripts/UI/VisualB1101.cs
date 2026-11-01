using System;
using System.Collections.Generic;
using ChoSiren.Panels;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ChoSiren.UI
{
    /// <summary>
    /// 2026-11-01 (visual-b, 赵金 1101c): small shared vocabulary for the low-text pages — neon icons
    /// (VisualB1101 + VisualA1101 sets, tinted, with the pink misregistered echo), short icon chips,
    /// "506 → 406" value changes, a 「?」 that opens the long explanation on demand, radial progress
    /// rings, and a couple of tiny motions (pop-in, slow spin, pulse). Pages draw meaning with these
    /// instead of sentences; numbers stay, words shrink to 2–6 character labels.
    /// </summary>
    public static class VisualB1101
    {
        public static readonly Color Teal = new Color32(30, 232, 232, 255);
        public static readonly Color Pink = new Color32(255, 79, 216, 255);
        public static readonly Color Violet = new Color32(178, 132, 255, 255);
        public static readonly Color Gold = new Color32(255, 205, 96, 255);
        public static readonly Color Danger = new Color32(255, 96, 128, 255);
        public static readonly Color Good = new Color32(104, 236, 160, 255);
        public static readonly Color White = new Color32(249, 247, 255, 255);
        public static readonly Color Muted = new Color32(186, 176, 214, 255);
        public static readonly Color Plate = new Color32(16, 9, 30, 238);

        private static readonly Dictionary<string, Sprite> Icons = new Dictionary<string, Sprite>();
        private static GameAudio audio;

        /// <summary>Neon line icon by key: VisualB1101 first, then the VisualA1101 family.</summary>
        public static Sprite Icon(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (Icons.TryGetValue(key, out Sprite cached) && cached != null) return cached;
            Sprite sprite = Resources.Load<Sprite>("Art/VisualB1101/icon-" + key)
                            ?? Resources.Load<Sprite>("Art/VisualA1101/icon-" + key);
            Icons[key] = sprite;
            return sprite;
        }

        private static Sprite fadeDown, fadeRight, glow;

        /// <summary>Alpha ramp: opaque at the bottom (vertical) or at the left (horizontal).</summary>
        public static Sprite Fade(bool vertical)
        {
            if (vertical && fadeDown != null) return fadeDown;
            if (!vertical && fadeRight != null) return fadeRight;
            const int n = 64;
            var tex = new Texture2D(vertical ? 2 : n, vertical ? n : 2, TextureFormat.RGBA32, false)
                { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
            for (int i = 0; i < n; i++)
            {
                float t = i / (n - 1f);
                float a = vertical ? 1f - t : 1f - t;   // bottom row / left column opaque
                a = a * a * (3f - 2f * a);
                var c = new Color(1f, 1f, 1f, a);
                if (vertical) { tex.SetPixel(0, i, c); tex.SetPixel(1, i, c); }
                else { tex.SetPixel(i, 0, c); tex.SetPixel(i, 1, c); }
            }
            tex.Apply(false, true);
            Sprite sprite = Sprite.Create(tex, new UnityEngine.Rect(0, 0, tex.width, tex.height), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.hideFlags = HideFlags.DontSave;
            if (vertical) fadeDown = sprite; else fadeRight = sprite;
            return sprite;
        }

        /// <summary>Soft radial glow (white, alpha falls off to the rim).</summary>
        public static Sprite Glow()
        {
            if (glow != null) return glow;
            const int n = 96;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
                { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + .5f) / n * 2f - 1f, dy = (y + .5f) / n * 2f - 1f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                px[y * n + x] = new Color32(255, 255, 255, (byte)(255f * a * a));
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            glow = Sprite.Create(tex, new UnityEngine.Rect(0, 0, n, n), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
            glow.hideFlags = HideFlags.DontSave;
            return glow;
        }

        /// <summary>
        /// Puts a neon icon in front of an action button's centred caption (icon + short word).
        /// Call after the button has its final size.
        /// </summary>
        public static void AddButtonIcon(GameObject button, string iconKey, Color? color = null)
        {
            if (button == null) return;
            Transform body = button.transform.Find("Body");
            Text label = body != null ? body.Find("Label")?.GetComponent<Text>() : null;
            if (label == null) return;
            RectTransform rect = (RectTransform)button.transform;
            float w = rect.rect.width, h = rect.rect.height;
            float icon = Mathf.Min(h * .52f, 36f), gap = h * .12f;
            float textW = string.IsNullOrEmpty(label.text) ? 0f : Mathf.Min(label.preferredWidth, w - icon - gap - 24f);
            float total = icon + (textW > 0f ? gap + textW : 0f);
            float start = (w - total) * .5f;
            NeonIcon(body, "Icon", iconKey, color ?? label.color, start, (h - icon) * .5f, icon, false);
            label.alignment = TextAnchor.MiddleLeft;
            label.rectTransform.anchorMin = new Vector2(0f, 0f);
            label.rectTransform.anchorMax = new Vector2(0f, 1f);
            label.rectTransform.pivot = new Vector2(0f, .5f);
            label.rectTransform.anchoredPosition = new Vector2(start + icon + gap, 0f);
            label.rectTransform.sizeDelta = new Vector2(textW + 6f, -8f);
        }

        public static Sprite Ring() => Resources.Load<Sprite>("Art/VisualA1101/ring");
        public static Sprite TornBadge() => Resources.Load<Sprite>("Art/VisualA1101/badge-torn");

        /// <summary>Race art (魅族/魔族/海灵族/血精灵 → Reference038 race badges).</summary>
        public static Sprite RaceIcon(string race)
        {
            string key = race != null && race.StartsWith("魅") ? "charm" : race != null && race.StartsWith("魔") ? "demon"
                : race != null && race.StartsWith("海") ? "sea" : race != null && race.StartsWith("血") ? "blood" : null;
            return key == null ? null : ReferenceArt038.Load("Art/Reference038/race-" + key);
        }

        /// <summary>Battle-position icon for a race family (连击/毒伤/治疗护盾/爆发斩杀).</summary>
        public static string PositionIcon(string race)
        {
            if (string.IsNullOrEmpty(race)) return "target";
            if (race.StartsWith("魅")) return "combo";
            if (race.StartsWith("魔")) return "drop";
            if (race.StartsWith("海")) return "heal";
            if (race.StartsWith("血")) return "sword";
            return "target";
        }

        public static Color RaceColor(string race)
        {
            if (string.IsNullOrEmpty(race)) return Teal;
            if (race.StartsWith("魅")) return new Color32(255, 112, 200, 255);
            if (race.StartsWith("魔")) return new Color32(176, 112, 255, 255);
            if (race.StartsWith("海")) return new Color32(64, 214, 255, 255);
            if (race.StartsWith("血")) return new Color32(255, 92, 110, 255);
            return Teal;
        }

        // ------------------------------------------------------------------ building blocks

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent != null ? parent.gameObject.layer : 5;
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static Image Img(string name, Transform parent, Sprite sprite, Color color)
        {
            RectTransform rect = Rect(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>
        /// Tinted neon icon at (x, y, size) — a pink echo offset down-right behind the glyph gives the
        /// misprinted-poster look. Returns the glyph; its parent holder carries <paramref name="name"/>.
        /// </summary>
        public static Image NeonIcon(Transform parent, string name, string key, Color color,
            float x, float y, float size, bool echo = true)
        {
            RectTransform holder = Rect(name, parent);
            PanelKit.PlaceTop(holder, x, y, size, size);
            Sprite sprite = Icon(key);
            if (echo)
            {
                Image back = Img("Echo", holder, sprite, new Color(Pink.r, Pink.g, Pink.b, .8f));
                PanelKit.Stretch(back.rectTransform, size * .05f, -size * .05f, size * .05f, -size * .05f);
                back.preserveAspect = true;
            }
            Image glyph = Img("Glyph", holder, sprite, color);
            PanelKit.Stretch(glyph.rectTransform);
            glyph.preserveAspect = true;
            return glyph;
        }

        /// <summary>Outlined bold label (readable over art). Short words only.</summary>
        public static Text Label(Transform parent, string name, string value, int size, Color color,
            float x, float y, float width, float height, TextAnchor anchor = TextAnchor.MiddleLeft,
            float outline = 1.4f, FontStyle style = FontStyle.Bold)
        {
            Text text = ModalPolish038Chrome.Text(parent, name, value, size, color, style, anchor);
            PanelKit.PlaceTop(text.rectTransform, x, y, width, height);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            if (outline > 0f)
            {
                Outline edge = text.gameObject.AddComponent<Outline>();
                edge.effectColor = new Color(0f, 0f, 0f, .85f);
                edge.effectDistance = new Vector2(outline, -outline);
            }
            return text;
        }

        /// <summary>
        /// Slanted dark chip: [icon] short text. Width follows the text; returns the chip rect
        /// (top-left at x, y) so callers can line chips up.
        /// </summary>
        public static RectTransform Chip(Transform parent, string name, string iconKey, string value,
            Color accent, float x, float y, float height, out Text label, Color? face = null, int fontSize = 0)
        {
            RectTransform chip = Rect(name, parent);
            Image plate = chip.gameObject.AddComponent<Image>();
            plate.sprite = PunkUi.SlantFill;
            plate.color = face ?? Plate;
            plate.raycastTarget = false;
            Image rim = Img("Rim", chip, PunkUi.SlantLine, new Color(accent.r, accent.g, accent.b, .75f));
            PanelKit.Stretch(rim.rectTransform);
            float pad = height * .32f;
            float iconSize = string.IsNullOrEmpty(iconKey) ? 0f : height * .7f;
            if (iconSize > 0f) NeonIcon(chip, "Icon", iconKey, accent, pad, (height - iconSize) * .5f, iconSize, false);
            int size = fontSize > 0 ? fontSize : Mathf.RoundToInt(height * .5f);
            label = Label(chip, "Label", value, size, string.IsNullOrEmpty(iconKey) ? accent : White,
                pad + iconSize + (iconSize > 0f ? height * .14f : 0f), 0f, 400f, height, TextAnchor.MiddleLeft, 1f);
            float width = pad * 2f + iconSize + (iconSize > 0f ? height * .14f : 0f) + label.preferredWidth + height * .2f;
            if (string.IsNullOrEmpty(value)) width = pad * 2f + iconSize;
            label.rectTransform.sizeDelta = new Vector2(label.preferredWidth + 4f, height);
            PanelKit.PlaceTop(chip, x, y, width, height);
            return chip;
        }

        /// <summary>
        /// "[icon] 506 → 406" — a value before/after an action. The arrow is the neon arrow icon; the
        /// result is coloured (danger when it goes below zero / is short).
        /// </summary>
        public static RectTransform Delta(Transform parent, string name, Sprite currency, int from, int to,
            float x, float y, float height, Color resultColor, int fontSize = 0)
        {
            RectTransform row = Rect(name, parent);
            float cursor = 0f;
            if (currency != null)
            {
                Image icon = Img("Currency", row, currency, Color.white);
                icon.preserveAspect = true;
                PanelKit.PlaceTop(icon.rectTransform, 0f, height * .08f, height * .84f, height * .84f);
                cursor = height * .96f;
            }
            int size = fontSize > 0 ? fontSize : Mathf.RoundToInt(height * .62f);
            Text before = Label(row, "From", from.ToString("N0"), size, Muted, cursor, 0f, 300f, height);
            before.rectTransform.sizeDelta = new Vector2(before.preferredWidth + 2f, height);
            cursor += before.preferredWidth + height * .22f;
            NeonIcon(row, "Arrow", "arrow", resultColor, cursor, height * .2f, height * .6f, false);
            cursor += height * .6f + height * .22f;
            Text after = Label(row, "To", to.ToString("N0"), Mathf.RoundToInt(size * 1.12f), resultColor, cursor, 0f, 300f, height);
            after.rectTransform.sizeDelta = new Vector2(after.preferredWidth + 2f, height);
            cursor += after.preferredWidth;
            PanelKit.PlaceTop(row, x, y, cursor, height);
            return row;
        }

        /// <summary>Re-centres a row built at x=0 inside a parent of the given width.</summary>
        public static void CenterX(RectTransform row, float parentWidth)
        {
            Vector2 p = row.anchoredPosition;
            row.anchoredPosition = new Vector2((parentWidth - row.sizeDelta.x) * .5f, p.y);
        }

        /// <summary>Radial progress ring (VisualA1101 ring sprite, filled clockwise from the top).</summary>
        public static Image ProgressRing(Transform parent, string name, float x, float y, float size, float fill,
            Color color, Color track)
        {
            RectTransform holder = Rect(name, parent);
            PanelKit.PlaceTop(holder, x, y, size, size);
            Image back = Img("Track", holder, Ring(), track);
            PanelKit.Stretch(back.rectTransform);
            Image ring = Img("Fill", holder, Ring(), color);
            PanelKit.Stretch(ring.rectTransform);
            ring.type = Image.Type.Filled;
            ring.fillMethod = Image.FillMethod.Radial360;
            ring.fillOrigin = (int)Image.Origin360.Top;
            ring.fillClockwise = true;
            ring.fillAmount = Mathf.Clamp01(fill);
            return ring;
        }

        /// <summary>Thin slanted progress bar (track + fill), top-left at x, y.</summary>
        public static Image Bar(Transform parent, string name, float x, float y, float width, float height,
            float fill, Color color, Color? track = null)
        {
            RectTransform holder = Rect(name, parent);
            PanelKit.PlaceTop(holder, x, y, width, height);
            Image back = Img("Track", holder, null, track ?? new Color32(70, 60, 110, 120));
            PanelKit.Stretch(back.rectTransform);
            Image bar = Img("Fill", holder, null, color);
            bar.rectTransform.anchorMin = Vector2.zero;
            bar.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(fill), 1f);
            bar.rectTransform.offsetMin = bar.rectTransform.offsetMax = Vector2.zero;
            return bar;
        }

        /// <summary>
        /// Round 「?」 that opens the long explanation only when the player asks for it.
        /// </summary>
        public static GameObject HelpButton(Transform parent, string name, float x, float y, float size,
            string title, params string[] lines)
        {
            RectTransform hit = Rect(name, parent);
            PanelKit.PlaceTop(hit, x - size * .2f, y - size * .2f, size * 1.4f, size * 1.4f);
            Image catcher = hit.gameObject.AddComponent<Image>();
            catcher.color = new Color(0f, 0f, 0f, 0f);
            catcher.raycastTarget = true;
            RectTransform body = Body(hit);
            Image disc = Img("Disc", body, TornBadge(), new Color32(22, 12, 40, 245));
            PanelKit.Stretch(disc.rectTransform, size * .2f, size * .2f, -size * .2f, -size * .2f);
            NeonIcon(body, "Glyph", "help", Teal, size * .2f, size * .2f, size, false);
            Button button = hit.gameObject.AddComponent<Button>();
            button.targetGraphic = catcher;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => ShowHelp(hit, title, lines));
            hit.gameObject.AddComponent<VisualB1101Press>();
            return hit.gameObject;
        }

        /// <summary>Small card with the long text; tap anywhere to close.</summary>
        public static GameObject ShowHelp(Transform from, string title, string[] lines)
        {
            ModalPolish038Chrome.PlayClick();
            Transform host = from;
            while (host.parent != null && host.name != "SafeArea" && host.GetComponent<Canvas>() == null) host = host.parent;
            GameObject old = FindChild(host, "VisualBHelp");
            if (old != null) UnityEngine.Object.Destroy(old);

            RectTransform overlay = Rect("VisualBHelp", host);
            PanelKit.Stretch(overlay);
            Image dim = overlay.gameObject.AddComponent<Image>();
            dim.color = new Color32(2, 2, 10, 150);
            dim.raycastTarget = true;
            Button close = overlay.gameObject.AddComponent<Button>();
            close.transition = Selectable.Transition.None;
            close.onClick.AddListener(() => { ModalPolish038Chrome.PlayClick(); UnityEngine.Object.Destroy(overlay.gameObject); });

            const float width = 560f;
            float height = 88f + lines.Length * 44f;
            RectTransform card = Rect("Card", overlay);
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(.5f, .5f);
            card.sizeDelta = new Vector2(width, height);
            Image face = card.gameObject.AddComponent<Image>();
            face.color = new Color32(14, 8, 28, 252);
            face.raycastTarget = true;
            SirenStickerUi.SkinPanel(card.gameObject);
            NeonIcon(card, "Icon", "help", Teal, 26f, 22f, 40f);
            Text head = Label(card, "Title", title, 26, White, 78f, 22f, width - 110f, 40f);
            SirenStickerUi.SkinTitle(head);
            for (int i = 0; i < lines.Length; i++)
            {
                Image dot = Img("Dot-" + i, card, PunkUi.SlantFill, i % 2 == 0 ? Teal : Pink);
                PanelKit.PlaceTop(dot.rectTransform, 30f, 88f + i * 44f + 14f, 12f, 12f);
                Text line = Label(card, "Line-" + i, lines[i], 19, new Color32(226, 220, 244, 255),
                    54f, 80f + i * 44f, width - 80f, 40f, TextAnchor.MiddleLeft, 0f, FontStyle.Normal);
                line.horizontalOverflow = HorizontalWrapMode.Wrap;
                PanelKit.EnableBestFit(line, 13);
            }
            Pop(card);
            return overlay.gameObject;
        }

        /// <summary>Stretched, centre-pivoted "Body" child that press feedback scales.</summary>
        public static RectTransform Body(Transform parent)
        {
            RectTransform body = Rect("Body", parent);
            PanelKit.Stretch(body);
            body.pivot = new Vector2(.5f, .5f);
            return body;
        }

        private static GameObject FindChild(Transform host, string name)
        {
            for (int i = 0; i < host.childCount; i++)
                if (host.GetChild(i).name == name) return host.GetChild(i).gameObject;
            return null;
        }

        /// <summary>Wraps an existing Graphic-less holder in a button with press feedback.</summary>
        public static Button MakeButton(GameObject target, UnityAction action, bool sound = true)
        {
            Image hit = UnityNull.Real(target.GetComponent<Image>());
            if (hit == null)
            {
                hit = target.AddComponent<Image>();
                hit.color = new Color(0f, 0f, 0f, 0f);
            }
            hit.raycastTarget = true;
            Button button = UnityNull.Real(target.GetComponent<Button>()) ?? target.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() =>
            {
                if (sound) ModalPolish038Chrome.PlayClick();
                action?.Invoke();
            });
            if (UnityNull.Real(target.GetComponent<VisualB1101Press>()) == null) target.AddComponent<VisualB1101Press>();
            return button;
        }

        // ------------------------------------------------------------------ motion + sound

        /// <summary>
        /// Stage-stat radar: the polygon plus the stat glyph at each corner (the strongest one gold).
        /// </summary>
        public static VisualB1101Radar StatRadar(Transform parent, string name, float x, float y, float size,
            int[] values, Color accent)
        {
            RectTransform holder = Rect(name, parent);
            PanelKit.PlaceTop(holder, x, y, size, size);
            float pad = size * .16f;
            RectTransform plot = Rect("Plot", holder);
            PanelKit.Stretch(plot, pad, pad, -pad, -pad);
            VisualB1101Radar radar = plot.gameObject.AddComponent<VisualB1101Radar>();
            radar.raycastTarget = false;
            radar.Values = new float[values.Length];
            int strongest = 0;
            for (int i = 0; i < values.Length; i++)
            {
                radar.Values[i] = Mathf.Clamp01(values[i] / 100f);
                if (values[i] > values[strongest]) strongest = i;
            }
            radar.Fill = new Color(accent.r, accent.g, accent.b, .34f);
            radar.Rim = accent;
            var glyphs = new[]
            {
                ReferencePunkFx.StatGlyphKind.Wave, ReferencePunkFx.StatGlyphKind.Note,
                ReferencePunkFx.StatGlyphKind.Spark, ReferencePunkFx.StatGlyphKind.Mic,
                ReferencePunkFx.StatGlyphKind.Gem,
            };
            float r = size * .5f - pad * .5f, g = pad * 1.05f;
            for (int i = 0; i < values.Length && i < glyphs.Length; i++)
            {
                float a = Mathf.PI * .5f - i * Mathf.PI * 2f / values.Length;
                float cx = size * .5f + Mathf.Cos(a) * r, cy = size * .5f - Mathf.Sin(a) * r;
                Image icon = Img("Corner-" + i, holder, ReferencePunkFx.StatGlyph(glyphs[i]),
                    i == strongest ? Gold : new Color32(200, 190, 236, 230));
                icon.preserveAspect = true;
                PanelKit.PlaceTop(icon.rectTransform, cx - g * .5f, cy - g * .5f, g, g);
            }
            return radar;
        }

        public static void Pop(RectTransform target, float delay = 0f, float from = .86f)
        {
            if (target == null || !Application.isPlaying) return;
            VisualB1101Motion motion = target.gameObject.AddComponent<VisualB1101Motion>();
            motion.PlayPop(delay, from);
        }

        public static void Spin(RectTransform target, float degreesPerSecond)
        {
            if (target == null || !Application.isPlaying) return;
            target.gameObject.AddComponent<VisualB1101Motion>().PlaySpin(degreesPerSecond);
        }

        public static void Pulse(Graphic graphic, float period = 1.4f, float min = .45f)
        {
            if (graphic == null || !Application.isPlaying) return;
            graphic.gameObject.AddComponent<VisualB1101Motion>().PlayPulse(graphic, period, min);
        }

        public static void Sfx(string name, float volume = .8f)
        {
            if (audio == null) audio = UnityEngine.Object.FindAnyObjectByType<GameAudio>();
            if (audio != null) audio.PlaySfx(name, volume);
        }
    }

    /// <summary>
    /// Filled radar polygon (stage stats): a faint web, the value shape in the accent colour and a
    /// bright rim. Values are 0..1, drawn clockwise from the top.
    /// </summary>
    public sealed class VisualB1101Radar : MaskableGraphic
    {
        public float[] Values = new float[5];
        public Color Fill = new Color(.12f, .9f, .9f, .38f);
        public Color Rim = new Color(.12f, .9f, .9f, 1f);
        public Color Web = new Color(1f, 1f, 1f, .14f);
        public float RimWidth = 3f;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            int n = Values != null ? Values.Length : 0;
            if (n < 3) return;
            UnityEngine.Rect r = rectTransform.rect;
            Vector2 c = r.center;
            float radius = Mathf.Min(r.width, r.height) * .5f;
            Vector2 Dir(int i) { float a = Mathf.PI * .5f - i * Mathf.PI * 2f / n; return new Vector2(Mathf.Cos(a), Mathf.Sin(a)); }
            // web rings
            for (int ring = 1; ring <= 3; ring++)
            {
                float k = radius * ring / 3f;
                for (int i = 0; i < n; i++) Line(vh, c + Dir(i) * k, c + Dir((i + 1) % n) * k, 1.4f, Web);
            }
            for (int i = 0; i < n; i++) Line(vh, c, c + Dir(i) * radius, 1.2f, Web);
            // value polygon
            int center = vh.currentVertCount;
            vh.AddVert(c, Fill, Vector4.zero);
            for (int i = 0; i < n; i++)
                vh.AddVert(c + Dir(i) * radius * Mathf.Clamp(Values[i], .06f, 1f), Fill, Vector4.zero);
            for (int i = 0; i < n; i++) vh.AddTriangle(center, center + 1 + i, center + 1 + (i + 1) % n);
            for (int i = 0; i < n; i++)
            {
                Vector2 a = c + Dir(i) * radius * Mathf.Clamp(Values[i], .06f, 1f);
                Vector2 b = c + Dir((i + 1) % n) * radius * Mathf.Clamp(Values[(i + 1) % n], .06f, 1f);
                Line(vh, a, b, RimWidth, Rim);
            }
        }

        private static void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color color)
        {
            Vector2 d = (b - a).normalized;
            Vector2 nrm = new Vector2(-d.y, d.x) * width * .5f;
            int i = vh.currentVertCount;
            vh.AddVert(a - nrm, color, Vector4.zero);
            vh.AddVert(a + nrm, color, Vector4.zero);
            vh.AddVert(b + nrm, color, Vector4.zero);
            vh.AddVert(b - nrm, color, Vector4.zero);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i, i + 2, i + 3);
        }
    }

    /// <summary>Pop-in / slow spin / alpha pulse — unscaled time, self-contained.</summary>
    public sealed class VisualB1101Motion : MonoBehaviour
    {
        private enum Kind { Pop, Spin, Pulse }
        private Kind kind;
        private float t, delay, from, speed, period, min;
        private Graphic graphic;
        private CanvasGroup group;

        internal void PlayPop(float wait, float start)
        {
            kind = Kind.Pop; delay = wait; from = start; t = 0f;
            group = UnityNull.Real(GetComponent<CanvasGroup>()) ?? gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            transform.localScale = Vector3.one * from;
        }

        internal void PlaySpin(float degreesPerSecond) { kind = Kind.Spin; speed = degreesPerSecond; }

        internal void PlayPulse(Graphic target, float seconds, float low)
        { kind = Kind.Pulse; graphic = target; period = Mathf.Max(.2f, seconds); min = low; }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            switch (kind)
            {
                case Kind.Pop:
                    if (delay > 0f) { delay -= dt; return; }
                    t += dt / .24f;
                    float k = Mathf.Clamp01(t);
                    float back = 1f + 2.2f * Mathf.Pow(k - 1f, 3f) + 1.2f * Mathf.Pow(k - 1f, 2f);
                    transform.localScale = Vector3.one * Mathf.LerpUnclamped(from, 1f, back);
                    if (group != null) group.alpha = Mathf.Clamp01(k * 1.6f);
                    if (k >= 1f) { transform.localScale = Vector3.one; if (group != null) group.alpha = 1f; Destroy(this); }
                    break;
                case Kind.Spin:
                    transform.Rotate(0f, 0f, -speed * dt);
                    break;
                case Kind.Pulse:
                    if (graphic == null) return;
                    t += dt;
                    Color c = graphic.color;
                    c.a = Mathf.Lerp(min, 1f, .5f + .5f * Mathf.Sin(t / period * Mathf.PI * 2f));
                    graphic.color = c;
                    break;
            }
        }
    }

    /// <summary>Hover lift / press squash for small icon buttons (hit rect never moves).</summary>
    public sealed class VisualB1101Press : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler
    {
        private float target = 1f, current = 1f;
        private bool over;

        public void OnPointerEnter(PointerEventData e) { over = true; target = 1.06f; }
        public void OnPointerExit(PointerEventData e) { over = false; target = 1f; }
        public void OnPointerDown(PointerEventData e) => target = .92f;
        public void OnPointerUp(PointerEventData e) => target = over ? 1.06f : 1f;

        private Transform body;

        private void Update()
        {
            if (Mathf.Approximately(current, target)) return;
            if (body == null) body = transform.Find("Body");
            if (body == null) return;
            current = Mathf.MoveTowards(current, target, Time.unscaledDeltaTime * 1.6f);
            body.localScale = Vector3.one * current;
        }
    }
}
