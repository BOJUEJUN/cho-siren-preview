using System;
using ChoSiren.Panels;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.UI
{
    /// <summary>
    /// Calm list-page parts (2026-10-25, 少爷: "类似这种页面太难阅读了一大推信息文字太乱了…可读性要好"): a two-way
    /// segmented tab with an obvious selected side, quiet rows that carry one line of information and one action,
    /// and buttons in three weights. Brand teal marks the single thing to do; finished things fade back.
    /// Unified design (2026-10-25, "不要一个地方一个设计"): every part wears the shared SirenStickerUi sticker skin —
    /// no rounded pills of its own.
    /// </summary>
    internal static class CleanUi1025
    {
        // Rows are sticker sprites, so their tint is a plain fade: white = live, translucent = finished.
        public static readonly Color32 Row = new Color32(255, 255, 255, 255), RowDone = new Color32(255, 255, 255, 115),
            Text = new Color32(246, 242, 252, 255), Muted = new Color32(158, 148, 186, 255), Track = new Color32(255, 255, 255, 24),
            Gold = new Color32(255, 214, 120, 255), Cyan = new Color32(96, 226, 255, 255);

        public enum Weight { Primary, Outline, Quiet }

        /// <summary>A sticker button (primary teal by default). Returns its label so callers can restyle it later.</summary>
        public static Button Pill(PanelKit kit, Transform parent, string name, string label, int fontSize, Action onClick, out Text text)
        {
            Image plate = kit.NewImage(name, parent, null, Color.clear);
            plate.raycastTarget = true;
            text = kit.NewPlacedText(plate.transform, label, fontSize, Brand1025.Ink, 0, 0, 10, 10, TextAnchor.MiddleCenter, FontStyle.Bold);
            text.gameObject.name = "Label";
            PanelKit.Stretch(text.rectTransform); text.raycastTarget = false;
            var button = plate.gameObject.AddComponent<Button>();
            button.targetGraphic = plate; button.transition = Selectable.Transition.None;
            if (onClick != null) button.onClick.AddListener(() => { kit.PlayClick(); onClick(); });
            SirenStickerUi.SkinButton(plate.gameObject, true);
            return button;
        }

        public static void StylePill(Button button, Text text, Weight weight, string label, bool interactable)
        {
            if (button == null) return;
            if (label != null && text != null) text.text = label;
            button.interactable = interactable;
            // Primary = teal sticker, Outline = dark sticker, Quiet = faded tab sticker with a muted label.
            SirenStickerUi.SkinButton(button.gameObject, weight == Weight.Primary, weight == Weight.Quiet);
            if (weight == Weight.Quiet && text != null) text.color = Muted;
        }

        /// <summary>A two-way segmented control; the selected side is the teal primary sticker.</summary>
        public sealed class Segment
        {
            public Button Left, Right;
            public Text LeftText, RightText;
            public Image Track, Thumb;

            public void Select(bool left)
            {
                RectTransform r = Thumb.rectTransform;
                r.anchorMin = new Vector2(left ? 0f : .5f, 0f); r.anchorMax = new Vector2(left ? .5f : 1f, 1f);
                r.offsetMin = new Vector2(6f, 6f); r.offsetMax = new Vector2(-6f, -6f);
                LeftText.color = left ? (Color)Brand1025.Ink : Muted;
                RightText.color = left ? Muted : (Color)Brand1025.Ink;
                LeftText.fontStyle = RightText.fontStyle = FontStyle.Bold;
            }
        }

        public static Segment Segmented(PanelKit kit, Transform parent, string name, string left, string right, int fontSize,
            Action onLeft, Action onRight)
        {
            var seg = new Segment();
            seg.Track = kit.NewImage(name, parent, SirenStickerUi.QuietPanelSprite, Color.white);
            seg.Track.type = Image.Type.Sliced; seg.Track.raycastTarget = false;
            seg.Thumb = kit.NewImage("Selected", seg.Track.transform, SirenStickerUi.PrimarySprite, Color.white);
            seg.Thumb.type = Image.Type.Sliced; seg.Thumb.raycastTarget = false;
            seg.Left = Half(kit, seg.Track.transform, name + "-Left", left, fontSize, 0f, onLeft, out seg.LeftText);
            seg.Right = Half(kit, seg.Track.transform, name + "-Right", right, fontSize, .5f, onRight, out seg.RightText);
            seg.Select(true);
            return seg;
        }

        static Button Half(PanelKit kit, Transform parent, string name, string label, int fontSize, float x, Action onClick, out Text text)
        {
            Image hit = kit.NewImage(name, parent, null, new Color(1f, 1f, 1f, .001f));
            hit.raycastTarget = true;
            RectTransform r = hit.rectTransform;
            r.anchorMin = new Vector2(x, 0f); r.anchorMax = new Vector2(x + .5f, 1f); r.offsetMin = r.offsetMax = Vector2.zero;
            text = kit.NewPlacedText(hit.transform, label, fontSize, Muted, 0, 0, 10, 10, TextAnchor.MiddleCenter, FontStyle.Bold);
            PanelKit.Stretch(text.rectTransform); text.raycastTarget = false;
            var button = hit.gameObject.AddComponent<Button>();
            button.targetGraphic = hit; button.transition = Selectable.Transition.None;
            if (onClick != null) button.onClick.AddListener(() => { kit.PlayClick(); onClick(); });
            return button;
        }

        /// <summary>Makes a modal body scroll vertically; rows go into the returned content (top-left placed).</summary>
        public static RectTransform ScrollBody(PanelKit kit, RectTransform body, string name)
        {
            Image viewport = kit.NewImage(name + "Viewport", body, null, new Color(0f, 0f, 0f, .001f));
            viewport.raycastTarget = true;
            PanelKit.Stretch(viewport.rectTransform);
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform content = kit.NewRect(name + "Content", viewport.transform);
            content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f); content.pivot = new Vector2(.5f, 1f);
            content.anchoredPosition = Vector2.zero; content.sizeDelta = Vector2.zero;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content; scroll.viewport = viewport.rectTransform;
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic; scroll.scrollSensitivity = 40f;
            return content;
        }

        /// <summary>A quiet sticker row (the shared list-row look) with a thin state accent on its left edge.</summary>
        public static Image RowPanel(PanelKit kit, Transform parent, string name, out Image accent)
        {
            Image row = kit.NewImage(name, parent, SirenStickerUi.QuietPanelSprite, Row);
            row.type = Image.Type.Sliced; row.raycastTarget = false;
            accent = kit.NewImage("Accent", row.transform, null, Brand1025.Teal);
            accent.raycastTarget = false;
            RectTransform a = accent.rectTransform;
            a.anchorMin = new Vector2(0f, 0f); a.anchorMax = new Vector2(0f, 1f); a.pivot = new Vector2(0f, .5f);
            a.sizeDelta = new Vector2(6f, -30f); a.anchoredPosition = new Vector2(14f, 0f);
            return row;
        }
    }
}
