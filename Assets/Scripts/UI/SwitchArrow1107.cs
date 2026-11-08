using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ChoSiren.UI
{
    /// <summary>
    /// 2026-11-07 (赵金: "这个替换按钮就统一成所有的点击左右替换按钮"): the one left/right switch button — the thick
    /// white chevron from the member-detail page (<see cref="PsdHomeChevron"/>) with its hover / press feedback, on a
    /// generous invisible hit area. Every ‹ › that switches a member, page or item uses this.
    /// </summary>
    public static class SwitchArrow1107
    {
        /// <summary>Builds the button at <paramref name="hit"/> (top-left based, in the parent's units).</summary>
        public static Button Create(Transform parent, string name, int direction, Rect hit, UnityAction action,
            float glyphW = 26f, float glyphH = 44f)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(hit.x, -hit.y);
            rect.sizeDelta = new Vector2(hit.width, hit.height);
            var hitImage = go.GetComponent<Image>();
            hitImage.color = new Color(1f, 1f, 1f, 0f); // invisible but raycastable
            var visual = new GameObject("ArrowVisual", typeof(RectTransform));
            visual.transform.SetParent(go.transform, false);
            var visualRect = (RectTransform)visual.transform;
            visualRect.anchorMin = visualRect.anchorMax = visualRect.pivot = new Vector2(.5f, .5f);
            visualRect.sizeDelta = new Vector2(glyphW, glyphH);
            var glyph = new GameObject("Chevron", typeof(RectTransform), typeof(CanvasRenderer), typeof(PsdHomeChevron));
            glyph.transform.SetParent(visual.transform, false);
            var glyphRect = (RectTransform)glyph.transform;
            glyphRect.anchorMin = Vector2.zero; glyphRect.anchorMax = Vector2.one;
            glyphRect.offsetMin = glyphRect.offsetMax = Vector2.zero;
            var chevron = glyph.GetComponent<PsdHomeChevron>();
            chevron.Initialize(direction < 0 ? -1 : 1);
            var button = go.GetComponent<Button>();
            button.targetGraphic = chevron;
            button.transition = Selectable.Transition.None;
            if (action != null) button.onClick.AddListener(action);
            go.AddComponent<PsdHomeChevronFeedback>().Configure(chevron, visualRect);
            return button;
        }
    }
}
