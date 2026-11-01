using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using ChoSiren.UI;

namespace ChoSiren.Panels
{
    /// <summary>
    /// 0.3.8 page chrome for the secondary task/practice panels: slashed crystal plates,
    /// diagonal neon accents and action buttons that reuse the shared modal body-motion
    /// contract (<see cref="ModalPolish038Chrome.ActionButton"/>). The Button keeps a
    /// fixed outer hit rect while its "Body" child carries every authored visual, so
    /// hover/press/release/exit feedback moves frame, inset and caption coherently.
    /// </summary>
    internal static class SecondaryTasks038Chrome
    {
        public static readonly Color EdgeSelected = new Color32(247, 245, 255, 255);
        public static readonly Color InsetSelected = new Color32(11, 8, 18, 250);
        public static readonly Color EdgeIdle = new Color32(183, 155, 255, 160);
        public static readonly Color InsetIdle = new Color32(11, 8, 18, 235);
        public static readonly Color EdgeGold = new Color32(255, 205, 96, 225);
        public static readonly Color InsetGold = new Color32(11, 8, 18, 250);
        public static readonly Color EdgeCyan = new Color32(91, 215, 255, 235);
        public static readonly Color InsetCyan = new Color32(11, 8, 18, 250);
        public static readonly Color EdgeViolet = new Color32(157, 144, 247, 235);
        public static readonly Color InsetViolet = new Color32(11, 8, 18, 250);
        public static readonly Color EdgeDisabled = new Color32(104, 98, 140, 120);
        public static readonly Color InsetDisabled = new Color32(24, 23, 46, 225);
        public static readonly Color LabelIdle = new Color32(196, 190, 220, 225);

        /// <summary>
        /// 112px header band matching the shared panel header layout (Back at 18,27 / 88×56,
        /// eyebrow+title at x128) but with a slashed back button and a neon beam divider
        /// so the strip reads as the 0.3.8 crystal chrome instead of plain rounded boxes.
        /// </summary>
        public static GameObject Header(PanelKit kit, Transform parent, string eyebrow,
            string title, string backName, UnityAction onBack)
        {
            // v0.3.41 punk header (matches the task board): black band, slanted dark back
            // button with a pink offset, big white title, pink italic eyebrow and a pink rule.
            Image band = kit.NewImage("Header", parent, null, new Color32(11, 8, 18, 240));
            PanelKit.PlaceTop(band.rectTransform, 0f, 0f, 720f, 112f);
            band.raycastTarget = true;

            GameObject back = Button(band.transform, backName, "‹ 返回", 17, onBack,
                ModalPolish038ButtonStyle.Ghost);
            PanelKit.PlaceTop(back.GetComponent<RectTransform>(), 18f, 30f, 96f, 52f);

            Text titleText = kit.NewPlacedText(band.transform, title, 34, PanelKit.White,
                136f, 30f, 360f, 52f, TextAnchor.MiddleLeft, FontStyle.Bold);
            PanelKit.EnableBestFit(titleText, 20);
            SirenStickerUi.SkinTitle(titleText, true);
            Text eyebrowText = kit.NewPlacedText(band.transform, eyebrow, 20, SirenStickerUi.Muted,
                136f, 8f, 300f, 24f, TextAnchor.MiddleLeft, FontStyle.BoldAndItalic);
            PanelKit.EnableBestFit(eyebrowText, 10);

            Image rule = kit.NewImage("Divider", band.transform, SirenStickerUi.TabSprite, SirenStickerUi.Paper);
            PanelKit.PlaceTop(rule.rectTransform, 0f, 108f, 720f, 4f);
            rule.raycastTarget = false;
            return band.gameObject;
        }

        /// <summary>Slashed action button with shared modal hover/press/release body motion.</summary>
        public static GameObject Button(Transform parent, string name, string label, int fontSize,
            UnityAction action, ModalPolish038ButtonStyle style)
        {
            GameObject button = ModalPolish038Chrome.ActionButton(parent, name, label, Mathf.Max(24, fontSize), action, style);
            SirenStickerUi.SkinButton(button, style == ModalPolish038ButtonStyle.Primary);
            return button;
        }

        /// <summary>
        /// Caption text inside the button body. The caption is the only Text under "Body";
        /// callers may rename the node (e.g. StartPracticeLabel) so the lookup tolerates
        /// names other than the default "Label".
        /// </summary>
        public static Text LabelOf(GameObject button)
        {
            if (button == null) return null;
            Transform body = button.transform.Find("Body");
            return body != null ? body.GetComponentInChildren<Text>(true) : null;
        }

        /// <summary>
        /// Drives one action button's full visual state: interactable flag, body alpha,
        /// slash edge/inset palette and caption text/color. Keeps the disabled look readable
        /// (in-progress and claimed states stay legible instead of going invisible).
        /// </summary>
        public static void SetState(GameObject button, bool interactable, Color edge, Color inset,
            Color labelColor, string text = null, float disabledAlpha = .5f)
        {
            if (button == null) return;
            Button component = button.GetComponent<Button>();
            if (component != null) component.interactable = interactable;

            Transform body = button.transform.Find("Body");
            if (body == null) return;
            CanvasGroup group = body.GetComponent<CanvasGroup>();
            if (group != null) group.alpha = interactable ? 1f : Mathf.Max(.72f, disabledAlpha);

            Transform edgeNode = body.Find("Edge");
            Image edgeImage = edgeNode != null ? edgeNode.GetComponent<Image>() : null;
            if (edgeImage != null) edgeImage.color = interactable ? SirenStickerUi.Paper : SirenStickerUi.Muted;
            Transform insetNode = edgeNode != null ? edgeNode.Find("Inset") : null;
            Image insetImage = insetNode != null ? insetNode.GetComponent<Image>() : null;
            if (insetImage != null) insetImage.color = SirenStickerUi.Ink;

            SirenStickerUi.SkinButton(button, interactable && button.name == "StartPractice");

            Text label = LabelOf(button);
            if (label == null) return;
            if (text != null) label.text = text;
            label.color = labelColor;
        }

        /// <summary>Flat glass plate with a 5 px status stripe in the edge colour (punk style).</summary>
        public static GameObject Plate(PanelKit kit, Transform parent, string name, Color color,
            Color edge, bool mirror = false)
        {
            Image image = kit.NewImage(name, parent, null, color);
            SirenStickerUi.SkinPanel(image.gameObject, edge.a > 0f);
            if (edge.a > 0f)
            {
                Image stripe = kit.NewImage("Stripe", image.transform, null, new Color(edge.r, edge.g, edge.b, 1f));
                RectTransform rect = stripe.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, .5f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = new Vector2(5f, 0f);
                stripe.raycastTarget = false;
            }

            return image.gameObject;
        }

        /// <summary>
        /// Crystal progress bar: stretched slash track, neon fill (Image.Filled) and a spark
        /// marker that rides the fill edge. Returns the fill; the caller moves the marker.
        /// </summary>
        public static Image Progress(PanelKit kit, Transform parent, string name, float x, float y,
            float width, float height, Color trackColor, Color fillColor, out Image marker)
        {
            Image track = kit.NewImage(name, parent, ReferencePunkFx.SlashPanel(false), trackColor);
            PanelKit.PlaceTop(track.rectTransform, x, y, width, height);

            Image fill = kit.NewImage("Fill", track.transform, ReferencePunkFx.Bar(), fillColor);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            PanelKit.Stretch(fill.rectTransform, 3f, 2f, -3f, -2f);

            marker = kit.NewImage("ProgressMarker", parent,
                ReferencePunkFx.StatGlyph(ReferencePunkFx.StatGlyphKind.Spark), fillColor);
            marker.preserveAspect = true;
            marker.raycastTarget = false;
            RectTransform markerRect = marker.rectTransform;
            markerRect.anchorMin = markerRect.anchorMax = new Vector2(0f, 1f);
            markerRect.pivot = Vector2.one * .5f;
            markerRect.sizeDelta = new Vector2(height + 6f, height + 6f);
            markerRect.anchoredPosition = new Vector2(x + 3f, -(y + height * .5f));
            return fill;
        }

        /// <summary>Moves the spark marker to the visible end of the progress fill.</summary>
        public static void SetProgressMarker(Image marker, float x, float y, float width,
            float height, float fillAmount, Color color)
        {
            if (marker == null) return;
            RectTransform rect = marker.rectTransform;
            float usable = Mathf.Max(0f, width - 6f);
            rect.anchoredPosition = new Vector2(x + 3f + usable * Mathf.Clamp01(fillAmount),
                -(y + height * .5f));
            marker.color = color;
        }
    }
}
