using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.UI
{
    /// <summary>
    /// 2026-11-01 (edge-fit, 赵金 1101b): full-page backgrounds reach the real screen edge.
    /// Pages open inside the safe area, so a background that "covers its parent" (an
    /// <see cref="AspectRatioFitter"/> in EnvelopeParent mode on a page-sized parent) stopped at
    /// the notch / home-indicator line and the band beyond showed the blurred page ambient.
    /// This watcher finds such backgrounds under the safe root and hands them to
    /// <see cref="StageCover1101"/>, which covers the whole screen instead (the portrait column
    /// on landscape / tablet screens); full-page tints stretched over the same parent follow,
    /// so the dimming stays continuous. Buttons and text are untouched and stay in the safe area.
    /// </summary>
    public sealed class EdgeBleed1101 : MonoBehaviour
    {
        private const int ScanEveryFrames = 10;
        private static readonly Vector3[] Corners = new Vector3[4];
        private readonly List<AspectRatioFitter> fitters = new List<AspectRatioFitter>();
        private RectTransform safeRoot;
        private int nextScan;

        public void Bind(RectTransform safe) => safeRoot = safe;

        private void LateUpdate()
        {
            if (safeRoot == null || Time.frameCount < nextScan) return;
            nextScan = Time.frameCount + ScanEveryFrames;
            Scan(safeRoot);
        }

        /// <summary>Converts every page-sized cover background under <paramref name="safe"/> (also used by tests).</summary>
        public static void Scan(RectTransform safe)
        {
            if (safe == null) return;
            var found = new List<AspectRatioFitter>();
            safe.GetComponentsInChildren(false, found);
            Rect safeRect = safe.rect;
            foreach (AspectRatioFitter fitter in found)
            {
                if (fitter == null || !fitter.enabled || fitter.aspectMode != AspectRatioFitter.AspectMode.EnvelopeParent) continue;
                if (fitter.GetComponent<StageCover1101>() != null) continue;
                var parent = fitter.transform.parent as RectTransform;
                if (parent == null || !CoversSafe(parent, safe, safeRect)) continue;
                fitter.gameObject.AddComponent<StageCover1101>().Envelope(fitter, safe);
                foreach (Transform sibling in parent)
                {
                    if (sibling == fitter.transform || sibling.childCount > 0) continue;
                    var rect = sibling as RectTransform;
                    if (rect == null || rect.GetComponent<StageCover1101>() != null || rect.GetComponent<AspectRatioFitter>() != null) continue;
                    if (rect.GetComponent<Graphic>() == null || !FullStretch(rect)) continue;
                    rect.gameObject.AddComponent<StageCover1101>().Stretch(safe);
                }
            }
        }

        private static bool FullStretch(RectTransform rect) =>
            rect.anchorMin == Vector2.zero && rect.anchorMax == Vector2.one &&
            rect.offsetMin.sqrMagnitude < .01f && rect.offsetMax.sqrMagnitude < .01f &&
            rect.localScale == Vector3.one && rect.localRotation == Quaternion.identity;

        /// <summary>The parent spans the whole safe area (a full page, not a card or a panel).</summary>
        private static bool CoversSafe(RectTransform parent, RectTransform safe, Rect safeRect)
        {
            parent.GetWorldCorners(Corners);
            Vector2 a = safe.InverseTransformPoint(Corners[0]);
            Vector2 b = safe.InverseTransformPoint(Corners[2]);
            const float slack = 2f;
            return a.x <= safeRect.xMin + slack && a.y <= safeRect.yMin + slack &&
                   b.x >= safeRect.xMax - slack && b.y >= safeRect.yMax - slack;
        }
    }

    /// <summary>
    /// Sizes a full-page background to the screen stage rather than its (safe-area) parent:
    /// the whole canvas on phones and the Mac window, the portrait column's width × full height
    /// on landscape / tablet screens. Envelope keeps the art's aspect (cover, never stretched);
    /// Stretch is for flat full-page tints.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StageCover1101 : MonoBehaviour
    {
        private static readonly Vector3[] Corners = new Vector3[4];
        private AspectRatioFitter fitter;
        private RectTransform safe;
        private RectTransform canvasRect;
        private float aspect;
        private bool envelope;

        public void Envelope(AspectRatioFitter source, RectTransform safeRoot)
        {
            fitter = source;
            aspect = source.aspectRatio;
            source.enabled = false;
            envelope = true;
            safe = safeRoot;
            Apply();
        }

        public void Stretch(RectTransform safeRoot)
        {
            envelope = false;
            safe = safeRoot;
            Apply();
        }

        private void LateUpdate() => Apply();

        private void Apply()
        {
            var rect = (RectTransform)transform;
            var parent = rect.parent as RectTransform;
            if (parent == null || safe == null) return;
            if (fitter != null)
            {
                // The page may retune (or re-enable) its own cover; follow its aspect, keep it off.
                if (fitter.aspectRatio > 0f) aspect = fitter.aspectRatio;
                if (fitter.enabled) fitter.enabled = false;
            }
            if (canvasRect == null)
            {
                Canvas canvas = rect.GetComponentInParent<Canvas>();
                if (canvas == null) return;
                canvasRect = (RectTransform)canvas.rootCanvas.transform;
            }
            if (!StageRect(parent, out Rect stage)) return;
            Vector2 size = stage.size;
            if (envelope && aspect > 0f)
                size = stage.width / stage.height > aspect
                    ? new Vector2(stage.width, stage.width / aspect)
                    : new Vector2(stage.height * aspect, stage.height);
            if (rect.anchorMin != new Vector2(.5f, .5f) || rect.anchorMax != new Vector2(.5f, .5f))
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            if (rect.pivot != new Vector2(.5f, .5f)) rect.pivot = new Vector2(.5f, .5f);
            Vector2 position = stage.center - parent.rect.center;
            if ((rect.anchoredPosition - position).sqrMagnitude > .0001f) rect.anchoredPosition = position;
            if ((rect.sizeDelta - size).sqrMagnitude > .0001f) rect.sizeDelta = size;
        }

        /// <summary>The screen stage in <paramref name="parent"/>'s local space.</summary>
        private bool StageRect(RectTransform parent, out Rect stage)
        {
            stage = default;
            Rect canvas = canvasRect.rect;
            if (canvas.width <= 1f || canvas.height <= 1f) return false;
            canvasRect.GetWorldCorners(Corners);
            Vector2 cMin = parent.InverseTransformPoint(Corners[0]);
            Vector2 cMax = parent.InverseTransformPoint(Corners[2]);
            if (canvas.width / canvas.height > SafeAreaFitter.WideAspect)
            {
                // Portrait column: the safe root's width, the screen's height.
                safe.GetWorldCorners(Corners);
                Vector2 sMin = parent.InverseTransformPoint(Corners[0]);
                Vector2 sMax = parent.InverseTransformPoint(Corners[2]);
                cMin.x = sMin.x; cMax.x = sMax.x;
            }
            stage = Rect.MinMaxRect(Mathf.Min(cMin.x, cMax.x), Mathf.Min(cMin.y, cMax.y), Mathf.Max(cMin.x, cMax.x), Mathf.Max(cMin.y, cMax.y));
            return stage.width > 1f && stage.height > 1f;
        }
    }
}
