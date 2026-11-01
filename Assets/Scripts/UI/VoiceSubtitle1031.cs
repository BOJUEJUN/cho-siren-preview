using System;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.UI
{
    /// <summary>
    /// Chinese subtitle for a spoken battle line (2026-10-31, 阵亡台词): a torn-paper ink caption with a
    /// brand-teal edge and the speaker's name tag, pinned beside her card for the length of the clip.
    /// Follows the card if the stage shakes; freezes while paused. ReduceMotion keeps only the fade.
    /// </summary>
    public sealed class VoiceSubtitle1031 : MonoBehaviour
    {
        private const float Width = 330f;
        private RectTransform root, anchor, layer, body;
        private CanvasGroup group;
        private Func<bool> paused;
        private float clock, life;
        private Vector2 offset;

        public static VoiceSubtitle1031 Show(RectTransform layer, RectTransform anchor, Font font, string speaker,
            string text, float seconds, Func<bool> isPaused)
        {
            if (layer == null || string.IsNullOrEmpty(text) || !Application.isPlaying) return null;
            // One caption per speaker: a newer line replaces her older one.
            string id = "VoiceSubtitle1031-" + (speaker ?? "");
            Transform old = layer.Find(id);
            if (old != null) Destroy(old.gameObject);

            var go = new GameObject(id, typeof(RectTransform), typeof(CanvasGroup));
            var s = go.AddComponent<VoiceSubtitle1031>();
            s.layer = layer; s.anchor = anchor; s.paused = isPaused;
            s.life = Mathf.Clamp(seconds + .45f, 1.4f, 4f);
            s.root = (RectTransform)go.transform;
            s.root.SetParent(layer, false);
            s.root.SetAsLastSibling();
            s.root.anchorMin = s.root.anchorMax = s.root.pivot = new Vector2(.5f, .5f);
            s.root.sizeDelta = new Vector2(Width, 80f);
            s.group = go.GetComponent<CanvasGroup>();
            s.group.blocksRaycasts = false; s.group.interactable = false; s.group.alpha = 0f;
            s.Build(font, speaker, text);
            s.offset = new Vector2(0f, 14f);
            s.Pose();
            return s;
        }

        private void Build(Font font, string speaker, string text)
        {
            body = new GameObject("Body", typeof(RectTransform)).GetComponent<RectTransform>();
            body.SetParent(root, false);
            body.anchorMin = body.anchorMax = body.pivot = new Vector2(.5f, .5f);
            body.sizeDelta = new Vector2(Width, 80f);
            body.localEulerAngles = new Vector3(0, 0, 2.5f);

            Text line = NewText(body, "Line", font, text, 24, Color.white, FontStyle.Bold);
            line.horizontalOverflow = HorizontalWrapMode.Wrap;
            line.rectTransform.sizeDelta = new Vector2(Width - 36f, 0f);
            float h = Mathf.Max(52f, line.preferredHeight + 22f);
            line.rectTransform.sizeDelta = new Vector2(Width - 36f, h);

            // ink shadow, ink plate, teal edge under the plate, a sliver of white on top (ref 04 band)
            P5Letters1023.Plate(body, "Shadow", new Vector2(8, -8), new Vector2(Width, h), Brand1025.Ink, -8f);
            P5Letters1023.Plate(body, "Teal", new Vector2(0, -5), new Vector2(Width + 8, h + 4), Brand1025.Teal, -8f);
            P5Letters1023.Plate(body, "Ink", Vector2.zero, new Vector2(Width, h), new Color32(18, 10, 34, 242), -8f);
            Image half = P5Letters1023.Plate(body, "Halftone", Vector2.zero, new Vector2(Width, h), Brand1025.TealA(34));
            if (half != null) { half.sprite = CelShapes.Halftone; half.type = Image.Type.Tiled; half.pixelsPerUnitMultiplier = 2f; }
            P5Letters1023.Plate(body, "Edge", new Vector2(0, h * .5f - 2f), new Vector2(Width - 10f, 4f), new Color32(246, 240, 248, 230), -8f);
            line.transform.SetAsLastSibling();

            if (!string.IsNullOrEmpty(speaker))
            {
                var tag = new GameObject("Speaker", typeof(RectTransform)).GetComponent<RectTransform>();
                tag.SetParent(body, false);
                tag.anchorMin = tag.anchorMax = tag.pivot = new Vector2(.5f, .5f);
                tag.anchoredPosition = new Vector2(-Width * .5f + 52f, h * .5f + 12f);
                tag.localEulerAngles = new Vector3(0, 0, -6f);
                P5Letters1023.Plate(tag, "Back", new Vector2(4, -4), new Vector2(100, 34), Brand1025.Ink, -14f);
                P5Letters1023.Plate(tag, "Plate", Vector2.zero, new Vector2(100, 34), Brand1025.Teal, -14f);
                Text name = NewText(tag, "Name", font, speaker, 23, Brand1025.Ink, FontStyle.Bold);
                name.rectTransform.sizeDelta = new Vector2(100, 34);
                Destroy(name.GetComponent<Outline>()); // ink on teal reads cleanly; an outline muddies it
            }
            root.sizeDelta = new Vector2(Width, h);
        }

        private static Text NewText(RectTransform parent, string name, Font font, string value, int size, Color color, FontStyle style)
        {
            var t = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)).GetComponent<Text>();
            t.transform.SetParent(parent, false);
            t.font = font; t.text = value; t.fontSize = size; t.color = color; t.fontStyle = style;
            t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            var r = t.rectTransform; r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f);
            var outline = t.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, .8f); outline.effectDistance = new Vector2(2, -2);
            return t;
        }

        private void Update()
        {
            if (paused != null && paused()) return;
            clock += Mathf.Min(Time.unscaledDeltaTime, .05f);
            Pose();
            if (clock >= life) Destroy(gameObject);
        }

        private static float EaseOut(float t) { t = Mathf.Clamp01(t); return 1f - (1f - t) * (1f - t) * (1f - t); }

        private void Pose()
        {
            // Pin above the card; keep the whole caption inside the layer.
            if (anchor != null && layer != null)
            {
                Vector3 world = anchor.TransformPoint(anchor.rect.center);
                Vector2 local = layer.InverseTransformPoint(world);
                // just above the card's top edge
                Vector2 at = local + offset + new Vector2(0, anchor.rect.height * anchor.lossyScale.y /
                    Mathf.Max(.0001f, layer.lossyScale.y) * .5f + root.sizeDelta.y * .5f);
                Rect r = layer.rect;
                float hw = root.sizeDelta.x * .5f + 12f, hh = root.sizeDelta.y * .5f + 24f;
                at.x = Mathf.Clamp(at.x, r.xMin + hw, r.xMax - hw);
                at.y = Mathf.Clamp(at.y, r.yMin + hh, r.yMax - hh);
                root.anchoredPosition = at;
            }
            bool calm = BattlePresentationOptions.ReduceMotion;
            float inK = EaseOut(clock / .16f);
            float outK = Mathf.Clamp01((clock - (life - .3f)) / .3f);
            group.alpha = Mathf.Clamp01(clock / .1f) * (1f - outK);
            if (calm) { body.anchoredPosition = Vector2.zero; body.localScale = Vector3.one; return; }
            body.anchoredPosition = new Vector2(Mathf.Lerp(-40f, 0f, inK), Mathf.Lerp(-6f, 0f, inK) + outK * 10f);
            body.localScale = Vector3.one * Mathf.Lerp(1.12f, 1f, inK);
        }
    }
}
