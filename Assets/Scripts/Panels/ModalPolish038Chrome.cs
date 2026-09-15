using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ChoSiren.Panels
{
    /// <summary>Handles returned by <see cref="ModalPolish038Chrome.Begin"/>.</summary>
    public sealed class ModalPolish038Frame
    {
        /// <summary>Full-screen dimming root; assign to the caller's modal slot.</summary>
        public GameObject Overlay;
        /// <summary>Crystal backdrop image rect (3:4, preserveAspect).</summary>
        public RectTransform Panel;
        /// <summary>Safe content rect inside the crystal perimeter.</summary>
        public RectTransform Body;
        /// <summary>Top-right close button (null when no close action was passed).</summary>
        public GameObject CloseButton;
        public float Width;
        public float Height;
    }

    public enum ModalPolish038ButtonStyle
    {
        Primary,
        Secondary,
        Danger,
        Ghost,
    }

    public enum ModalPolish038StateKind
    {
        Empty,
        Error,
        Locked,
        Confirm,
        Info,
    }

    /// <summary>
    /// Shared 0.3.8 modal chrome: the approved illustrated crystal backdrop (opaque, native
    /// aspect), a centered title band inside the safe interior, a fixed-hit-rect action button
    /// with body hover/press/release feedback, and empty/error/locked/confirm/info state
    /// blocks. Self-contained so both PanelKit panels and ChoSirenApp partials can build the
    /// same modal language without inheriting the rejected giant rounded purple panels.
    /// </summary>
    public static class ModalPolish038Chrome
    {
        public const string BackdropResource = "Art/ModalPolish038/modal-panel-038";

        /// <summary>Width per unit height of the approved 1086×1448 art (0.75).</summary>
        public const float PanelAspect = 1086f / 1448f;

        // Safe interior measured on the approved art. The crystal mass concentrates in the
        // top-left and bottom-right corners with thin shards along both edges; the centred
        // title band and this body rect keep every control on the flat dark interior.
        public const float BodyLeft = .105f;
        public const float BodyRight = .895f;
        public const float BodyTop = .172f;
        public const float BodyTopBare = .135f;
        public const float BodyBottom = .878f;

        public static readonly Color TextWhite = new Color32(249, 247, 255, 255);
        public static readonly Color TextMuted = new Color32(196, 190, 220, 255);
        public static readonly Color AccentPink = new Color32(255, 102, 190, 255);
        public static readonly Color AccentCyan = new Color32(91, 215, 255, 255);
        public static readonly Color AccentViolet = new Color32(178, 152, 255, 255);
        public static readonly Color AccentGold = new Color32(255, 205, 96, 255);
        public static readonly Color AccentDanger = new Color32(255, 122, 146, 255);
        public static readonly Color EyebrowPink = new Color32(255, 173, 226, 255);
        public static readonly Color InsetDark = new Color32(13, 10, 38, 246);
        public static readonly Color RowDark = new Color32(19, 20, 54, 235);

        private static Sprite backdrop;
        private static Font font;
        private static GameAudio audio;

        public static Font SharedFont
        {
            get
            {
                if (font == null)
                    font = Resources.Load<Font>("Fonts/NotoSansSC-Subset") ??
                           Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        /// <summary>Approved illustrated backdrop; null only if the resource is missing.</summary>
        public static Sprite BackdropSprite()
        {
            if (backdrop != null) return backdrop;
            backdrop = Resources.Load<Sprite>(BackdropResource);
            if (backdrop != null) return backdrop;
            Texture2D texture = Resources.Load<Texture2D>(BackdropResource);
            if (texture == null) return null;
            backdrop = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                Vector2.one * .5f, 100f, 0, SpriteMeshType.FullRect);
            backdrop.name = "ModalPolish038-Backdrop";
            backdrop.hideFlags = HideFlags.DontSave;
            return backdrop;
        }

        /// <summary>
        /// Builds overlay + crystal panel + centered title band + optional close button and
        /// returns the safe <see cref="ModalPolish038Frame.Body"/> for content. The dim layer
        /// blocks raycasts but never dismisses on click — every destructive or paid action
        /// keeps an explicit button path, matching the previous confirmation safeguards.
        /// </summary>
        public static ModalPolish038Frame Begin(Transform host, string overlayName, string title,
            string eyebrow, UnityAction onClose, float panelWidth,
            string closeName = "CloseProgression", bool titleVisible = true)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            var frame = new ModalPolish038Frame
            {
                Width = panelWidth,
                Height = panelWidth / PanelAspect,
            };

            GameObject overlay = Rect(overlayName, host).gameObject;
            Stretch(overlay.GetComponent<RectTransform>());
            Image dim = overlay.AddComponent<Image>();
            dim.color = new Color32(2, 3, 14, 233);
            dim.raycastTarget = true;
            frame.Overlay = overlay;

            RectTransform panel = Rect("Panel", overlay.transform);
            panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.one * .5f;
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = new Vector2(frame.Width, frame.Height);
            Image panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.sprite = BackdropSprite();
            panelImage.preserveAspect = true;
            panelImage.raycastTarget = true;
            frame.Panel = panel;

            if (titleVisible)
            {
                float cx = frame.Width * .5f;
                if (!string.IsNullOrEmpty(eyebrow))
                {
                    Text brow = Text(panel, "Eyebrow", eyebrow, 13, EyebrowPink,
                        FontStyle.Bold, TextAnchor.MiddleCenter);
                    PlaceTop(brow.rectTransform, cx - frame.Width * .30f, frame.Height * .088f,
                        frame.Width * .60f, 22f);
                    EnableBestFit(brow, 10);
                }

                Text head = Text(panel, "Title", title, 27, TextWhite,
                    FontStyle.Bold, TextAnchor.MiddleCenter);
                PlaceTop(head.rectTransform, cx - frame.Width * .32f, frame.Height * .114f,
                    frame.Width * .64f, 44f);
                EnableBestFit(head, 18);
                ReferencePunkFx.Beam(panel, "TitleBeam",
                    new Vector2(cx - Mathf.Min(150f, frame.Width * .26f), frame.Height * .168f),
                    new Vector2(cx + Mathf.Min(150f, frame.Width * .26f), frame.Height * .168f),
                    1.4f, new Color32(165, 103, 240, 132));
            }

            if (onClose != null)
            {
                GameObject close = ActionButton(panel, closeName, "×", 24, onClose,
                    ModalPolish038ButtonStyle.Ghost);
                PlaceTop(close.GetComponent<RectTransform>(),
                    frame.Width * .845f, frame.Height * .105f, 52f, 50f);
                frame.CloseButton = close;
            }

            RectTransform body = Rect("Body", panel);
            body.anchorMin = new Vector2(BodyLeft, 1f - BodyBottom);
            body.anchorMax = new Vector2(BodyRight, 1f - (titleVisible ? BodyTop : BodyTopBare));
            body.pivot = Vector2.one * .5f;
            body.offsetMin = body.offsetMax = Vector2.zero;
            frame.Body = body;
            return frame;
        }

        /// <summary>
        /// Slashed-edge action button with a fixed outer hit rect. The inner body scales and
        /// brightens through <see cref="ModalPolish038PressFeedback"/> so hover/press/release
        /// are all visible without moving the clickable area.
        /// </summary>
        public static GameObject ActionButton(Transform parent, string name, string label,
            int fontSize, UnityAction action,
            ModalPolish038ButtonStyle style = ModalPolish038ButtonStyle.Primary)
        {
            GameObject outer = Rect(name, parent).gameObject;
            Image hit = outer.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;
            Button button = outer.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;

            RectTransform body = Rect("Body", outer.transform);
            Stretch(body);
            body.pivot = Vector2.one * .5f;
            body.gameObject.AddComponent<CanvasGroup>();

            StylePalette(style, out Color edgeColor, out Color insetColor, out Color labelColor);
            Image edge = NewImage("Edge", body, ReferencePunkFx.SlashPanel(false), edgeColor);
            edge.type = Image.Type.Sliced;
            Stretch(edge.rectTransform);
            Image inset = NewImage("Inset", edge.transform, ReferencePunkFx.SlashPanel(false), insetColor);
            inset.type = Image.Type.Sliced;
            Stretch(inset.rectTransform, 2f, 2f, -2f, -2f);

            Text caption = Text(body, "Label", label, fontSize, labelColor,
                FontStyle.Bold, TextAnchor.MiddleCenter);
            Stretch(caption.rectTransform, 8f, 4f, -8f, -4f);
            EnableBestFit(caption, Mathf.Max(10, fontSize - 5));

            GameObject sheenObject = Rect("Sheen", body).gameObject;
            Stretch(sheenObject.GetComponent<RectTransform>());
            Image sheenImage = sheenObject.AddComponent<Image>();
            sheenImage.sprite = ReferencePunkFx.SlashPanel(false);
            sheenImage.type = Image.Type.Sliced;
            sheenImage.color = new Color(1f, 1f, 1f, 1f);
            sheenImage.raycastTarget = false;
            CanvasGroup sheen = sheenObject.AddComponent<CanvasGroup>();
            sheen.alpha = 0f;
            sheen.interactable = false;
            sheen.blocksRaycasts = false;

            button.onClick.AddListener(() =>
            {
                PlayClick();
                action?.Invoke();
            });
            outer.AddComponent<ModalPolish038PressFeedback>().Configure(body, sheen);
            return outer;
        }

        /// <summary>Dims/locks an action built by <see cref="ActionButton"/>; label swap optional.</summary>
        public static void SetActionEnabled(GameObject actionButton, bool enabled, string label = null)
        {
            if (actionButton == null) return;
            Button button = actionButton.GetComponent<Button>();
            if (button != null) button.interactable = enabled;
            Transform body = actionButton.transform.Find("Body");
            CanvasGroup group = body != null ? body.GetComponent<CanvasGroup>() : null;
            if (group != null) group.alpha = enabled ? 1f : .42f;
            if (label != null && body != null)
            {
                Transform caption = body.Find("Label");
                Text text = caption != null ? caption.GetComponent<Text>() : null;
                if (text != null) text.text = label;
            }
        }

        /// <summary>
        /// Compact state block for empty/error/locked/confirm/info content: slashed dark plate,
        /// colored accent bar + semantic glyph, bold title and muted detail line.
        /// </summary>
        public static GameObject StateBlock(Transform parent, string name,
            ModalPolish038StateKind kind, string title, string detail)
        {
            GameObject block = Rect(name, parent).gameObject;
            Image bg = block.AddComponent<Image>();
            bg.sprite = ReferencePunkFx.SlashPanel(true);
            bg.type = Image.Type.Sliced;
            bg.color = new Color32(11, 9, 34, 228);
            bg.raycastTarget = false;

            Color accent = StateAccent(kind);
            Image bar = NewImage("Accent", block.transform, ReferencePunkFx.Bar(), accent);
            RectTransform barRect = bar.rectTransform;
            barRect.anchorMin = new Vector2(0f, .18f);
            barRect.anchorMax = new Vector2(0f, .82f);
            barRect.pivot = Vector2.one * .5f;
            barRect.anchoredPosition = new Vector2(10f, 0f);
            barRect.sizeDelta = new Vector2(4f, 0f);
            barRect.localEulerAngles = new Vector3(0f, 0f, 9f);

            Image dot = NewImage("Dot", block.transform, StateGlyph(kind), accent);
            RectTransform dotRect = dot.rectTransform;
            dotRect.anchorMin = dotRect.anchorMax = new Vector2(0f, .5f);
            dotRect.pivot = Vector2.one * .5f;
            dotRect.anchoredPosition = new Vector2(34f, 0f);
            dotRect.sizeDelta = new Vector2(26f, 26f);
            dot.preserveAspect = true;

            Text head = Text(block.transform, "StateTitle", title, 15, accent,
                FontStyle.Bold, TextAnchor.MiddleLeft);
            if (string.IsNullOrEmpty(detail))
            {
                Stretch(head.rectTransform, 56f, 6f, -12f, -6f);
            }
            else
            {
                head.alignment = TextAnchor.LowerLeft;
                Stretch(head.rectTransform, 56f, 8f, -12f, -34f);
                Text body = Text(block.transform, "StateDetail", detail, 13, TextMuted,
                    FontStyle.Normal, TextAnchor.UpperLeft);
                Stretch(body.rectTransform, 56f, 36f, -12f, -6f);
                EnableBestFit(body, 10);
            }

            return block;
        }

        /// <summary>
        /// Clickable quiet row: fixed outer hit rect + slashed plate body that scales/brightens
        /// on hover and compresses on press. Callers place row content inside the returned
        /// <paramref name="body"/> (row-local coordinates).
        /// </summary>
        public static GameObject RowButton(Transform parent, string name, UnityAction action,
            bool mirror, out RectTransform body)
        {
            GameObject outer = Rect(name, parent).gameObject;
            Image hit = outer.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;
            Button button = outer.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;

            body = Rect("Body", outer.transform);
            Stretch(body);
            body.pivot = Vector2.one * .5f;
            Image plate = NewImage("Plate", body, ReferencePunkFx.SlashPanel(mirror), RowDark);
            plate.type = Image.Type.Sliced;
            Stretch(plate.rectTransform);
            Outline edge = plate.gameObject.AddComponent<Outline>();
            edge.effectColor = new Color32(122, 160, 224, 78);
            edge.effectDistance = new Vector2(1f, -1f);
            edge.useGraphicAlpha = true;

            GameObject sheenObject = Rect("Sheen", body).gameObject;
            Stretch(sheenObject.GetComponent<RectTransform>());
            Image sheenImage = sheenObject.AddComponent<Image>();
            sheenImage.sprite = ReferencePunkFx.SlashPanel(mirror);
            sheenImage.type = Image.Type.Sliced;
            sheenImage.color = new Color(1f, 1f, 1f, 1f);
            sheenImage.raycastTarget = false;
            CanvasGroup sheen = sheenObject.AddComponent<CanvasGroup>();
            sheen.alpha = 0f;
            sheen.interactable = false;
            sheen.blocksRaycasts = false;

            button.onClick.AddListener(() =>
            {
                PlayClick();
                action?.Invoke();
            });
            outer.AddComponent<ModalPolish038PressFeedback>().Configure(body, sheen, .2f);
            return outer;
        }

        /// <summary>Quiet slashed row plate (member rows, settings rows, info rows).</summary>
        public static GameObject RowPlate(Transform parent, string name, bool mirror = false)
        {
            GameObject row = Rect(name, parent).gameObject;
            Image bg = row.AddComponent<Image>();
            bg.sprite = ReferencePunkFx.SlashPanel(mirror);
            bg.type = Image.Type.Sliced;
            bg.color = RowDark;
            bg.raycastTarget = false;
            Outline edge = row.AddComponent<Outline>();
            edge.effectColor = new Color32(122, 160, 224, 78);
            edge.effectDistance = new Vector2(1f, -1f);
            edge.useGraphicAlpha = true;
            return row;
        }

        /// <summary>Portrait aperture with slashed frame + mask; bust-framed via member id.</summary>
        public static RectTransform PortraitSlot(Transform parent, string name, Sprite sprite,
            string memberId, float x, float y, float width, float height, Color tint)
        {
            GameObject frame = RowPlate(parent, name);
            PlaceTop(frame.GetComponent<RectTransform>(), x, y, width, height);
            RectTransform clip = Rect("Clip", frame.transform);
            Stretch(clip, 3f, 3f, -3f, -3f);
            clip.gameObject.AddComponent<RectMask2D>();
            Image portrait = NewImage("Portrait", clip, sprite, tint);
            Stretch(portrait.rectTransform);
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;
            PanelKit.FrameBustPortrait(portrait, clip, memberId);
            return frame.GetComponent<RectTransform>();
        }

        public static Text Text(Transform parent, string name, string value, int size, Color color,
            FontStyle style, TextAnchor alignment)
        {
            RectTransform rect = Rect(name, parent);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = SharedFont;
            text.text = value;
            text.fontSize = Mathf.Max(12, size);
            text.color = color;
            text.fontStyle = style;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.supportRichText = false;
            text.raycastTarget = false;
            return text;
        }

        public static void EnableBestFit(Text text, int minimumSize)
        {
            if (text == null) return;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Clamp(minimumSize, 8, text.fontSize);
            text.resizeTextMaxSize = text.fontSize;
        }

        /// <summary>Horizontal hairline used to separate modal content bands.</summary>
        public static Image Divider(Transform parent, string name, float x, float y, float width)
        {
            return ReferencePunkFx.Beam(parent, name, new Vector2(x, y), new Vector2(x + width, y),
                1.2f, new Color32(152, 105, 226, 96));
        }

        public static void PlayClick()
        {
            if (audio == null) audio = UnityEngine.Object.FindAnyObjectByType<GameAudio>();
            if (audio != null) audio.PlayClick();
        }

        public static void PlaySuccess()
        {
            if (audio == null) audio = UnityEngine.Object.FindAnyObjectByType<GameAudio>();
            if (audio != null) audio.PlaySuccess();
        }

        // ------------------------------------------------------------------ internals

        private static void StylePalette(ModalPolish038ButtonStyle style, out Color edge,
            out Color inset, out Color label)
        {
            switch (style)
            {
                case ModalPolish038ButtonStyle.Secondary:
                    edge = new Color32(157, 144, 247, 235);
                    inset = new Color32(23, 17, 56, 250);
                    label = TextWhite;
                    return;
                case ModalPolish038ButtonStyle.Danger:
                    edge = new Color32(226, 96, 128, 240);
                    inset = new Color32(54, 15, 36, 250);
                    label = TextWhite;
                    return;
                case ModalPolish038ButtonStyle.Ghost:
                    edge = new Color32(122, 160, 224, 150);
                    inset = new Color32(15, 15, 42, 210);
                    label = TextWhite;
                    return;
                default:
                    edge = AccentPink;
                    inset = new Color32(44, 17, 56, 250);
                    label = TextWhite;
                    return;
            }
        }

        private static Color StateAccent(ModalPolish038StateKind kind)
        {
            switch (kind)
            {
                case ModalPolish038StateKind.Error: return AccentDanger;
                case ModalPolish038StateKind.Locked: return AccentGold;
                case ModalPolish038StateKind.Confirm: return AccentViolet;
                case ModalPolish038StateKind.Info: return AccentCyan;
                default: return new Color32(126, 160, 200, 255);
            }
        }

        private static Sprite StateGlyph(ModalPolish038StateKind kind)
        {
            switch (kind)
            {
                case ModalPolish038StateKind.Error: return ReferencePunkFx.StatGlyph(ReferencePunkFx.StatGlyphKind.Note);
                case ModalPolish038StateKind.Locked: return ReferencePunkFx.StatGlyph(ReferencePunkFx.StatGlyphKind.Lock);
                case ModalPolish038StateKind.Confirm: return ReferencePunkFx.StatGlyph(ReferencePunkFx.StatGlyphKind.Spark);
                case ModalPolish038StateKind.Info: return ReferencePunkFx.StatGlyph(ReferencePunkFx.StatGlyphKind.Gem);
                default: return ReferencePunkFx.StatGlyph(ReferencePunkFx.StatGlyphKind.Wave);
            }
        }

        private static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
        {
            RectTransform rect = Rect(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            GameObject node = new GameObject(name, typeof(RectTransform));
            node.transform.SetParent(parent, false);
            RectTransform rect = node.GetComponent<RectTransform>();
            rect.localScale = Vector3.one;
            return rect;
        }

        private static void Stretch(RectTransform rect, float left = 0f, float bottom = 0f,
            float right = 0f, float top = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(right, top);
        }

        private static void PlaceTop(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }
    }
}
