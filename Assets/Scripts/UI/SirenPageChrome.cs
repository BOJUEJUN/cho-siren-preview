using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.UI
{
    /// <summary>Page-only selection cues. Built once with the semantic control, never scans a scene.</summary>
    internal static class SirenPageChrome
    {
        public static void Tab(GameObject root, bool selected)
        {
            if (root == null) return;
            // Unified design (2026-10-25): a selected tab is the teal primary sticker with an ink label. Chips painted
            // into a baked art board keep the board's own plate (a teal face gets cut into its slanted silhouette);
            // there the selection is a teal underline.
            bool baked = root.GetComponent<BakedRegionMotion038>() != null;
            SirenStickerUi.SkinButton(root, false, true, selected && !baked);
            Transform existing = root.transform.Find("SirenPageSelection");
            UnityEngine.UI.Image marker;
            if (existing == null)
            {
                var node = new GameObject("SirenPageSelection", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                node.transform.SetParent(root.transform, false);
                marker = node.GetComponent<UnityEngine.UI.Image>();
                RectTransform rect = marker.rectTransform;
                rect.anchorMin = new Vector2(.16f, 0f);
                rect.anchorMax = new Vector2(.84f, 0f);
                rect.pivot = new Vector2(.5f, 0f);
                rect.offsetMin = new Vector2(0f, 5f);
                rect.offsetMax = new Vector2(0f, 9f);
                marker.raycastTarget = false;
            }
            else marker = existing.GetComponent<UnityEngine.UI.Image>();
            marker.color = Brand1025.Teal;
            marker.gameObject.SetActive(selected && baked);
            Transform label = root.transform.Find("Label");
            if (label != null && label.TryGetComponent<UnityEngine.UI.Text>(out var text))
                text.color = selected && !baked ? (Color)Brand1025.Ink : SirenStickerUi.Paper;
        }
    }
}
