using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ChoSiren.Panels
{
    internal enum PunkButtonStyle
    {
        /// <summary>Pink fill, white text, black offset shadow: the one action that matters.</summary>
        Primary,
        /// <summary>White outline on clear: secondary actions such as 前往.</summary>
        Outline,
        /// <summary>Text only, muted: finished or unavailable states.</summary>
        Ghost,
        /// <summary>Black fill, pink offset shadow: back buttons and chips.</summary>
        Dark,
        /// <summary>Selected tab: white fill, black text, pink offset shadow.</summary>
        TabOn,
        /// <summary>Unselected tab: black fill, thin white outline.</summary>
        TabOff,
    }

    /// <summary>
    /// The simplified punk style (v0.3.39 design): black cards with a coloured left stripe,
    /// slanted parallelogram buttons and tabs, one pink accent, cyan for "in progress".
    /// Sprites are generated once at runtime, so no art assets are needed.
    /// </summary>
    internal static class PunkUi
    {
        public static readonly Color Hot = new Color32(255, 46, 147, 255);
        public static readonly Color Cyan = new Color32(63, 240, 255, 255);
        public static readonly Color Muted = new Color32(169, 156, 196, 255);
        public static readonly Color Done = new Color32(93, 83, 120, 255);
        public static readonly Color CardGlass = new Color32(18, 9, 34, 140);
        public static readonly Color Black = new Color32(0, 0, 0, 255);

        private static Sprite slantFill;
        private static Sprite slantLine;

        public static Sprite SlantFill => slantFill != null ? slantFill : slantFill = MakeSlant(false);
        public static Sprite SlantLine => slantLine != null ? slantLine : slantLine = MakeSlant(true);

        /// <summary>A parallelogram leaning right (top edge shifted right), anti-aliased.</summary>
        private static Sprite MakeSlant(bool outline)
        {
            const int width = 160, height = 56, slant = 12;
            const float stroke = 2.2f;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                float left = slant * (y + .5f) / height;
                float right = left + width - slant;
                for (int x = 0; x < width; x++)
                {
                    float px = x + .5f;
                    float inside = Mathf.Clamp01(Mathf.Min(px - left, right - px) + .5f);
                    if (outline)
                    {
                        float edge = Mathf.Min(Mathf.Min(px - left, right - px), Mathf.Min(y + .5f, height - y - .5f));
                        inside *= Mathf.Clamp01(stroke - edge + .5f);
                    }
                    pixels[y * width + x] = new Color32(255, 255, 255, (byte)(inside * 255));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(.5f, .5f), 100f,
                0, SpriteMeshType.FullRect);
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }

        /// <summary>Slanted button with a label child named "Label" and an optional offset shadow.</summary>
        public static GameObject Button(PanelKit kit, Transform parent, string name, string label, int fontSize,
            UnityAction action, PunkButtonStyle style)
        {
            Image hit = kit.NewImage(name, parent, SlantFill, Color.clear);
            hit.raycastTarget = true;
            GameObject root = hit.gameObject;
            Image shadow = kit.NewImage("Shadow", root.transform, SlantFill, Color.clear);
            PanelKit.Stretch(shadow.rectTransform, 4f, -4f, 4f, -4f);
            Image body = kit.NewImage("Body", root.transform, SlantFill, Color.clear);
            PanelKit.Stretch(body.rectTransform);
            Text text = kit.NewPlacedText(root.transform, label, fontSize, Color.white, 0, 0, 10, 10,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            text.name = "Label";
            PanelKit.Stretch(text.rectTransform);
            Button button = root.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            if (action != null) button.onClick.AddListener(action);
            Restyle(root, style, label, true);
            return root;
        }

        public static void Restyle(GameObject button, PunkButtonStyle style, string label, bool interactable)
        {
            if (button == null) return;
            Image shadow = button.transform.Find("Shadow")?.GetComponent<Image>();
            Image body = button.transform.Find("Body")?.GetComponent<Image>();
            Text text = button.transform.Find("Label")?.GetComponent<Text>();
            Button control = button.GetComponent<Button>();
            if (control != null) control.interactable = interactable;
            if (text != null && label != null) text.text = label;
            if (body == null || shadow == null || text == null) return;
            body.sprite = style == PunkButtonStyle.Outline || style == PunkButtonStyle.TabOff ? SlantLine : SlantFill;
            switch (style)
            {
                case PunkButtonStyle.Primary:
                    body.color = Hot; shadow.color = Black; text.color = Color.white; break;
                case PunkButtonStyle.Outline:
                    body.color = new Color(1f, 1f, 1f, .75f); shadow.color = Color.clear; text.color = Color.white; break;
                case PunkButtonStyle.Ghost:
                    body.color = Color.clear; shadow.color = Color.clear; text.color = Muted; break;
                case PunkButtonStyle.Dark:
                    body.color = Black; shadow.color = Hot; text.color = Color.white; break;
                case PunkButtonStyle.TabOn:
                    body.color = Color.white; shadow.color = Hot; text.color = Black; break;
                case PunkButtonStyle.TabOff:
                    body.color = new Color(1f, 1f, 1f, .22f); shadow.color = Color.clear; text.color = Color.white; break;
            }
            // A black under-fill keeps outline/tab-off buttons readable on busy art.
            if (style == PunkButtonStyle.TabOff || style == PunkButtonStyle.Outline)
            {
                shadow.sprite = SlantFill;
                shadow.color = new Color(0f, 0f, 0f, .55f);
                shadow.rectTransform.offsetMin = Vector2.zero;
                shadow.rectTransform.offsetMax = Vector2.zero;
            }
            else
            {
                shadow.rectTransform.offsetMin = new Vector2(4f, -4f);
                shadow.rectTransform.offsetMax = new Vector2(4f, -4f);
            }
            ChoSiren.UI.SirenStickerUi.SkinButton(button,
                style == PunkButtonStyle.Primary || style == PunkButtonStyle.TabOn,
                style == PunkButtonStyle.TabOn || style == PunkButtonStyle.TabOff || style == PunkButtonStyle.Ghost);
        }

        /// <summary>Glass card with a 6 px status stripe on its left edge (returned).</summary>
        public static Image Card(PanelKit kit, Transform parent, string name, out Image stripe)
        {
            Image card = kit.NewImage(name, parent, null, CardGlass);
            card.raycastTarget = false;
            // No Outline effect here: on a solid quad it repeats the whole card and greys it out.
            stripe = kit.NewImage("Stripe", card.transform, null, Cyan);
            RectTransform stripeRect = stripe.rectTransform;
            stripeRect.anchorMin = new Vector2(0f, 0f);
            stripeRect.anchorMax = new Vector2(0f, 1f);
            stripeRect.pivot = new Vector2(0f, .5f);
            stripeRect.offsetMin = Vector2.zero;
            stripeRect.offsetMax = new Vector2(6f, 0f);
            ChoSiren.UI.SirenStickerUi.SkinPanel(card.gameObject, false);
            return card;
        }
    }
}
