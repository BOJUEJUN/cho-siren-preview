using UnityEngine;

namespace ChoSiren
{
    /// <summary>Fits the authored PSD uniformly whenever the usable panel changes size.</summary>
    public sealed class BattlePsdViewportFit1019 : MonoBehaviour
    {
        private RectTransform authored, viewport;
        private Vector2 reference, previous = new Vector2(-1, -1);
        private bool applying;
        public Vector2 RestPosition { get; private set; }

        public void Configure(RectTransform content, RectTransform parent, Vector2 sourceSize)
        {
            authored = content; viewport = parent; reference = sourceSize;
            previous = new Vector2(-1, -1);
            RefreshFit();
        }

        private void OnRectTransformDimensionsChange() => RefreshFit();
        private void LateUpdate() => RefreshFit();

        public void RefreshFit()
        {
            if (applying || authored == null || viewport == null || reference.x <= 0 || reference.y <= 0) return;
            Vector2 usable = viewport.rect.size;
            if (usable.x <= 1 || usable.y <= 1 || (usable - previous).sqrMagnitude < .001f) return;
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
            }
            finally { applying = false; }
        }
    }
}
