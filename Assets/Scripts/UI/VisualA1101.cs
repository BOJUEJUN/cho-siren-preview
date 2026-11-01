using System;
using System.Collections.Generic;
using ChoSiren.Panels;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ChoSiren.UI
{
    /// <summary>
    /// 2026-11-01 (visual-a, 赵金 1101c · 「视觉视听享受游戏…用最少文字」): the shared picture language for the
    /// secondary pages — neon line icons with a pink misregistered offset, torn-paper medallions, progress rings,
    /// icon + 2-word buttons, a 「?」 that keeps the long explanations out of sight until asked for, and the
    /// pop / pulse motion and sounds that replace sentences. Icons live in Resources/Art/VisualA1101 (white
    /// glyphs + glow, tinted here); see Builds/qa-logs/zj-1101c/visual-a/tools/make_icons.py.
    /// </summary>
    public static class VisualA1101
    {
        public const string Folder = "Art/VisualA1101/";
        public static readonly Color Teal = Brand1025.Teal;
        public static readonly Color Pink = new Color32(255, 79, 216, 255);
        public static readonly Color Violet = new Color32(156, 104, 255, 255);
        /// <summary>"In progress" — a light violet that still reads on the ink plates.</summary>
        public static readonly Color Lavender = new Color32(196, 172, 255, 255);
        public static readonly Color Gold = new Color32(255, 205, 96, 255);
        public static readonly Color Danger = new Color32(255, 92, 124, 255);
        public static readonly Color Ink = Brand1025.Ink;
        public static readonly Color Paper = new Color32(249, 247, 255, 255);
        public static readonly Color Muted = new Color32(178, 170, 206, 255);
        public static readonly Color Plate = new Color32(22, 12, 40, 255);

        private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();

        public static Sprite Load(string name)
        {
            if (Sprites.TryGetValue(name, out Sprite sprite) && sprite != null) return sprite;
            sprite = Resources.Load<Sprite>(Folder + name);
            if (sprite == null)
            {
                Texture2D texture = Resources.Load<Texture2D>(Folder + name);
                if (texture != null)
                    sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), Vector2.one * .5f, 100f);
            }
            Sprites[name] = sprite;
            return sprite;
        }

        public static Sprite Icon(string name) => Load("icon-" + name);
        public static Sprite RingSprite => Load("ring");
        public static Sprite TornSprite => Load("badge-torn");

        // ------------------------------------------------------------------ icons

        /// <summary>
        /// A neon icon: a pink copy offset down-right (the misregistered print every sticker in the game has)
        /// under the tinted glyph. Returns the root rect (size × size, top-left placement is the caller's).
        /// </summary>
        public static RectTransform NeonIcon(Transform parent, string name, string icon, Color color, float size,
            float offset = -1f)
        {
            RectTransform root = Rect(name, parent);
            root.sizeDelta = new Vector2(size, size);
            if (offset < 0f) offset = Mathf.Clamp(size * .05f, 1.5f, 5f);
            Sprite sprite = Icon(icon);
            if (offset > 0f)
            {
                Image shadow = NewImage("IconOffset", root, sprite, new Color(Pink.r, Pink.g, Pink.b, color.a * .78f));
                Stretch(shadow.rectTransform);
                shadow.rectTransform.anchoredPosition = new Vector2(offset, -offset);
                shadow.preserveAspect = true;
            }
            Image glyph = NewImage("Glyph", root, sprite, color);
            Stretch(glyph.rectTransform);
            glyph.preserveAspect = true;
            return root;
        }

        public static Image Glyph(RectTransform neonIcon) => neonIcon != null ? neonIcon.Find("Glyph")?.GetComponent<Image>() : null;

        /// <summary>Recolours a <see cref="NeonIcon"/> (dim states, selected tabs).</summary>
        public static void Tint(RectTransform neonIcon, Color color, bool offset = true)
        {
            if (neonIcon == null) return;
            Image glyph = Glyph(neonIcon);
            if (glyph != null) glyph.color = color;
            Image shadow = neonIcon.Find("IconOffset")?.GetComponent<Image>();
            if (shadow != null) { shadow.enabled = offset; shadow.color = new Color(Pink.r, Pink.g, Pink.b, color.a * .78f); }
        }

        /// <summary>A torn-paper sticker disc (accent rim + ink plate) carrying a neon icon.</summary>
        public static RectTransform Medallion(Transform parent, string name, string icon, Color accent, float size,
            float iconScale = .58f)
        {
            RectTransform root = Rect(name, parent);
            root.sizeDelta = new Vector2(size, size);
            Image glow = NewImage("MedallionGlow", root, Glow, new Color(accent.r, accent.g, accent.b, .22f));
            Center(glow.rectTransform, size * 1.5f);
            Image rim = NewImage("MedallionRim", root, TornSprite, accent);
            Center(rim.rectTransform, size);
            rim.rectTransform.localEulerAngles = new Vector3(0f, 0f, 9f);
            Image plate = NewImage("MedallionPlate", root, TornSprite, Plate);
            Center(plate.rectTransform, size * .9f);
            plate.rectTransform.localEulerAngles = new Vector3(0f, 0f, -14f);
            if (!string.IsNullOrEmpty(icon))
            {
                RectTransform glyph = NeonIcon(root, "MedallionIcon", icon, accent, size * iconScale);
                glyph.anchorMin = glyph.anchorMax = glyph.pivot = Vector2.one * .5f;
                glyph.anchoredPosition = Vector2.zero;
            }
            return root;
        }

        /// <summary>A progress ring (dim track + radial fill from 12 o'clock); returns the fill.</summary>
        public static Image Ring(Transform parent, string name, float size, float value, Color color, float trackAlpha = .1f)
        {
            RectTransform root = Rect(name, parent);
            root.sizeDelta = new Vector2(size, size);
            Image track = NewImage("RingTrack", root, RingSprite, new Color(1f, 1f, 1f, trackAlpha));
            Stretch(track.rectTransform);
            Image fill = NewImage("RingFill", root, RingSprite, color);
            Stretch(fill.rectTransform);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Radial360;
            fill.fillOrigin = (int)Image.Origin360.Top;
            fill.fillClockwise = true;
            fill.fillAmount = Mathf.Clamp01(value);
            return fill;
        }

        // ------------------------------------------------------------------ type

        public static Text Label(Transform parent, string name, string value, int size, Color color,
            TextAnchor anchor = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Bold)
        {
            Text text = ModalPolish038Chrome.Text(parent, name, value, size, color, style, anchor);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        /// <summary>Big display number (战力, 余额…) with an ink outline so it pops off any backdrop.</summary>
        public static Text Number(Transform parent, string name, string value, int size, Color color,
            TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            Text text = Label(parent, name, value, size, color, anchor, FontStyle.BoldAndItalic);
            Outline outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, .85f);
            outline.effectDistance = new Vector2(1.6f, -1.6f);
            return text;
        }

        /// <summary>
        /// One stat as a picture: neon icon over a big number, an optional 2-character caption under it.
        /// </summary>
        public static RectTransform StatTile(Transform parent, string name, string icon, Color accent, string value,
            string caption, float width, float height)
        {
            RectTransform root = Rect(name, parent);
            root.sizeDelta = new Vector2(width, height);
            Image plate = NewImage("TilePlate", root, null, new Color32(255, 255, 255, 255));
            Stretch(plate.rectTransform);
            SirenStickerUi.SkinPanel(plate.gameObject, false);
            plate.raycastTarget = false;
            float iconSize = Mathf.Min(height * .36f, width * .5f);
            RectTransform glyph = NeonIcon(root, "TileIcon", icon, accent, iconSize);
            Place(glyph, (width - iconSize) * .5f, height * .08f, iconSize, iconSize);
            bool captioned = !string.IsNullOrEmpty(caption);
            float numberTop = height * .08f + iconSize + 2f;
            float numberHeight = height * .28f;
            Text number = Number(root, "Value", value, Mathf.RoundToInt(height * .23f), Paper);
            Place(number.rectTransform, 2f, numberTop, width - 4f, numberHeight);
            if (captioned)
            {
                Text tag = Label(root, "Caption", caption, Mathf.Max(12, Mathf.RoundToInt(height * .125f)), Muted);
                Place(tag.rectTransform, 0f, numberTop + numberHeight, width, height - numberTop - numberHeight - 4f);
            }
            return root;
        }

        // ------------------------------------------------------------------ buttons

        /// <summary>
        /// Puts an icon in front of a <see cref="ModalPolish038Chrome.ActionButton"/>'s label (or alone, centred,
        /// when the label is empty). Call after the button is sized.
        /// </summary>
        public static RectTransform DecorateAction(GameObject button, string icon, float iconSize, Color? color = null)
        {
            if (button == null) return null;
            Transform body = button.transform.Find("Body");
            if (body == null) return null;
            Text label = body.Find("Label")?.GetComponent<Text>();
            float width = ((RectTransform)button.transform).sizeDelta.x;
            Button control = button.GetComponent<Button>();
            bool primary = label != null && label.color.r < .2f; // ink label on the brand sticker
            Color tint = color ?? (primary ? (Color)Brand1025.Ink : Teal);
            RectTransform glyph = NeonIcon(body, "ActionIcon", icon, tint, iconSize, primary ? 0f : -1f);
            glyph.anchorMin = glyph.anchorMax = glyph.pivot = new Vector2(.5f, .5f);
            glyph.SetAsLastSibling();
            bool hasText = label != null && !string.IsNullOrEmpty(label.text);
            if (!hasText)
            {
                glyph.anchoredPosition = Vector2.zero;
                return glyph;
            }
            const float gap = 8f;
            float textWidth = Mathf.Min(label.preferredWidth, width - iconSize - gap - 24f);
            float total = iconSize + gap + textWidth;
            glyph.anchoredPosition = new Vector2(-total * .5f + iconSize * .5f, 0f);
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = labelRect.anchorMax = labelRect.pivot = new Vector2(.5f, .5f);
            labelRect.sizeDelta = new Vector2(textWidth + 6f, ((RectTransform)button.transform).sizeDelta.y - 8f);
            labelRect.anchoredPosition = new Vector2(-total * .5f + iconSize + gap + textWidth * .5f, 0f);
            if (control != null && !control.interactable) Tint(glyph, new Color(tint.r, tint.g, tint.b, .55f), false);
            return glyph;
        }

        /// <summary>
        /// The 「?」: a small round button; tapping it raises a bubble with the explanations a page used to print
        /// as sentences (one icon + one line each). Tap anywhere to close.
        /// </summary>
        public static GameObject HelpButton(Transform parent, string name, Transform host, float size,
            params (string icon, string line)[] lines)
        {
            RectTransform root = Rect(name, parent);
            root.sizeDelta = new Vector2(size, size);
            Image hit = root.gameObject.AddComponent<Image>();
            hit.sprite = Glow;
            hit.color = new Color(Teal.r, Teal.g, Teal.b, .28f);
            hit.raycastTarget = true;
            RectTransform glyph = NeonIcon(root, "HelpIcon", "help", Teal, size * .86f);
            glyph.anchorMin = glyph.anchorMax = glyph.pivot = Vector2.one * .5f;
            glyph.anchoredPosition = Vector2.zero;
            Button button = root.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = hit;
            button.onClick.AddListener(() =>
            {
                ModalPolish038Chrome.PlayClick();
                ShowHelp(host != null ? host : parent, lines);
            });
            root.gameObject.AddComponent<VisualA1101Motion>().Press();
            return root.gameObject;
        }

        public static GameObject ShowHelp(Transform host, (string icon, string line)[] lines)
        {
            Transform old = host.Find("HelpBubble");
            if (old != null) UnityEngine.Object.Destroy(old.gameObject);
            RectTransform overlay = Rect("HelpBubble", host);
            Stretch(overlay);
            overlay.SetAsLastSibling();
            Image dim = overlay.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, .55f);
            dim.raycastTarget = true;
            Button close = overlay.gameObject.AddComponent<Button>();
            close.transition = Selectable.Transition.None;
            close.onClick.AddListener(() =>
            {
                ModalPolish038Chrome.PlayClick();
                UnityEngine.Object.Destroy(overlay.gameObject);
            });

            const float width = 520f, row = 64f;
            float height = 36f + lines.Length * row;
            RectTransform card = Rect("HelpCard", overlay);
            card.anchorMin = card.anchorMax = card.pivot = Vector2.one * .5f;
            card.sizeDelta = new Vector2(width, height);
            Image plate = card.gameObject.AddComponent<Image>();
            SirenStickerUi.SkinPanel(card.gameObject);
            plate.raycastTarget = false;
            Image band = NewImage("HelpBand", card, PunkUi.SlantFill, Teal);
            Place(band.rectTransform, 22f, -10f, 64f, 8f);
            for (int i = 0; i < lines.Length; i++)
            {
                float y = 18f + i * row;
                RectTransform icon = NeonIcon(card, "HelpIcon-" + i, lines[i].icon, Teal, 40f);
                Place(icon, 26f, y + 12f, 40f, 40f);
                Text text = ModalPolish038Chrome.Text(card, "HelpLine-" + i, lines[i].line, 19, Paper,
                    FontStyle.Normal, TextAnchor.MiddleLeft);
                Place(text.rectTransform, 80f, y, width - 104f, row);
                ModalPolish038Chrome.EnableBestFit(text, 14);
            }
            card.gameObject.AddComponent<VisualA1101Motion>().Pop(0f);
            return overlay.gameObject;
        }

        // ------------------------------------------------------------------ sound

        public static void Click() => ModalPolish038Chrome.PlayClick();
        public static void Success() => ModalPolish038Chrome.PlaySuccess();

        /// <summary>Denied / not yet (a short glitch instead of a sentence).</summary>
        public static void Deny() => Sfx("glitch", .45f);

        /// <summary>The sign-in seal.</summary>
        public static void Stamp() => Sfx("stamp", .8f);

        private static GameAudio audio;

        public static void Sfx(string name, float volume)
        {
            if (audio == null) audio = UnityEngine.Object.FindAnyObjectByType<GameAudio>();
            audio?.PlaySfx(name, volume);
        }

        // ------------------------------------------------------------------ plumbing

        private static Sprite glow;

        /// <summary>A soft round glow (radial falloff), cached.</summary>
        public static Sprite Glow
        {
            get
            {
                if (glow != null) return glow;
                const int n = 64;
                var texture = new Texture2D(n, n, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
                var pixels = new Color32[n * n];
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), Vector2.one * n * .5f) / (n * .5f);
                    float a = Mathf.Clamp01(1f - d);
                    pixels[y * n + x] = new Color32(255, 255, 255, (byte)(255f * a * a));
                }
                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                glow = Sprite.Create(texture, new Rect(0, 0, n, n), Vector2.one * .5f, 100f);
                glow.hideFlags = HideFlags.DontSave;
                return glow;
            }
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var node = new GameObject(name, typeof(RectTransform));
            node.transform.SetParent(parent, false);
            return (RectTransform)node.transform;
        }

        public static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
        {
            RectTransform rect = Rect(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>Top-left placement inside the parent (y grows downward).</summary>
        public static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        public static void Center(RectTransform rect, float size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(size, size);
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }

    /// <summary>
    /// Small unscaled-time motions for the low-text pages: Pop (overshoot scale-in, staggered), Pulse (a gentle
    /// breath on the one thing to do), Spin (slow turn for record/medallion rims) and Press (scale on press).
    /// </summary>
    public sealed class VisualA1101Motion : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private enum Mode { None, Pop, Pulse, Spin }
        private Mode mode;
        private float delay, started, speed;
        private bool press, pressed;
        private float pressAmount;
        private Vector3 baseScale = Vector3.one;

        public VisualA1101Motion Pop(float after)
        {
            mode = Mode.Pop; delay = after; started = Time.unscaledTime;
            transform.localScale = Vector3.zero;
            return this;
        }

        public VisualA1101Motion Pulse()
        {
            mode = Mode.Pulse; started = Time.unscaledTime + UnityEngine.Random.value;
            return this;
        }

        public VisualA1101Motion Spin(float degreesPerSecond)
        {
            mode = Mode.Spin; speed = degreesPerSecond;
            return this;
        }

        public VisualA1101Motion Press()
        {
            press = true;
            return this;
        }

        public void OnPointerDown(PointerEventData eventData) { if (press) pressed = true; }
        public void OnPointerUp(PointerEventData eventData) { pressed = false; }
        public void OnPointerExit(PointerEventData eventData) { pressed = false; }

        private void Update()
        {
            float t = Time.unscaledTime - started;
            switch (mode)
            {
                case Mode.Pop:
                {
                    float k = Mathf.Clamp01((t - delay) / .34f);
                    // easeOutBack
                    float s = k <= 0f ? 0f : 1f + 2.4f * Mathf.Pow(k - 1f, 3f) + 1.4f * Mathf.Pow(k - 1f, 2f);
                    baseScale = Vector3.one * s;
                    if (k >= 1f) { baseScale = Vector3.one; mode = Mode.None; }
                    break;
                }
                case Mode.Pulse:
                    baseScale = Vector3.one * (1f + .045f * (.5f + .5f * Mathf.Sin(t * 4.2f)));
                    break;
                case Mode.Spin:
                    transform.localEulerAngles = new Vector3(0f, 0f, transform.localEulerAngles.z - speed * Time.unscaledDeltaTime);
                    break;
            }
            pressAmount = Mathf.MoveTowards(pressAmount, pressed ? 1f : 0f, Time.unscaledDeltaTime * 12f);
            if (mode != Mode.Spin || press) transform.localScale = baseScale * (1f - .08f * pressAmount);
        }
    }
}
