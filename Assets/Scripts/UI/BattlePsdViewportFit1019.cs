using UnityEngine;

namespace ChoSiren
{
    /// <summary>Fits the authored PSD uniformly whenever the usable panel changes size.</summary>
    public sealed class BattlePsdViewportFit1019 : MonoBehaviour
    {
        private RectTransform authored, viewport;
        private Vector2 reference, previous = new Vector2(-1, -1);
        private bool applying;
        private UnityEngine.UI.RectMask2D visualClip;
        private RectTransform canvasRect;
        private readonly Vector3[] corners = new Vector3[4];
        public Vector2 RestPosition { get; private set; }
        public Rect VisualViewport { get; private set; }

        public void Configure(RectTransform content, RectTransform parent, Vector2 sourceSize)
        {
            authored = content; viewport = parent; reference = sourceSize;
            visualClip = content.GetComponent<UnityEngine.UI.RectMask2D>();
            var canvas = parent.GetComponentInParent<Canvas>();
            canvasRect = canvas != null ? (RectTransform)canvas.rootCanvas.transform : null;
            previous = new Vector2(-1, -1);
            RefreshFit();
        }

        private void OnRectTransformDimensionsChange() => RefreshFit();
        private void LateUpdate() => RefreshFit();

        public void RefreshFit()
        {
            if (applying || authored == null || viewport == null || reference.x <= 0 || reference.y <= 0) return;
            Vector2 usable = viewport.rect.size;
            if (usable.x <= 1 || usable.y <= 1) return;
            if ((usable - previous).sqrMagnitude < .001f) { FitVisualClip(); return; }
            applying = true;
            try
            {
                previous = usable;
                float fit = Mathf.Min(usable.x / reference.x, usable.y / reference.y);
                authored.anchorMin = authored.anchorMax = authored.pivot = new Vector2(.5f, 1f);
                authored.sizeDelta = reference;
                authored.localScale = Vector3.one * fit;
                RestPosition = new Vector2(0, -(usable.y - reference.y * fit) * .5f);
                authored.anchoredPosition = RestPosition;
                FitVisualClip();
            }
            finally { applying = false; }
        }

        // The PSD remains uniformly fitted for cards and hit targets. Its clipping boundary,
        // however, belongs to the viewport: a paper cut-in may bleed outside the 720-unit board.
        // Negative mask padding extends only the clip, never scales/stretches character art.
        private void FitVisualClip()
        {
            RectTransform bounds = viewport;
            if (canvasRect != null && canvasRect.rect.width / Mathf.Max(1f, canvasRect.rect.height) <= SafeAreaFitter.WideAspect)
                bounds = canvasRect;
            bounds.GetWorldCorners(corners);
            Vector2 min = authored.InverseTransformPoint(corners[0]);
            Vector2 max = authored.InverseTransformPoint(corners[2]);
            VisualViewport = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            if (visualClip == null) return;
            Rect r = authored.rect;
            Vector4 padding = new Vector4(min.x - r.xMin - 1f, min.y - r.yMin - 1f,
                r.xMax - max.x - 1f, r.yMax - max.y - 1f);
            if ((visualClip.padding - padding).sqrMagnitude > .001f) visualClip.padding = padding;
        }
    }
}
