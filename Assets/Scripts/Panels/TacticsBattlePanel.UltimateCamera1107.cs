using System.Collections;
using UnityEngine;

namespace ChoSiren.Panels
{
    /// <summary>
    /// 2026-11-07 会议 (镜头拉远拉近) + 2026-11-12 夸张加倍: at the last moment of an ultimate the "camera" snaps in
    /// close on the enemy stage and then pulls back twice as far for weight. The whole PSD battle is scaled about the
    /// enemy stage's centre; reduce-motion skips it.
    /// </summary>
    public sealed partial class TacticsBattlePanel
    {
        internal const float UltimatePushScale1107 = 1.32f;
        internal const float UltimatePullScale1107 = .94f;
        private Coroutine ultimateCamera1107;
        private Vector3 cameraHomePosition1107, cameraHomeScale1107 = Vector3.one;
        private bool cameraHomeSaved1107;

        private void UltimateCameraPunch1107()
        {
            if (psdRoot == null || BattlePresentationOptions.ReduceMotion) return;
            if (ultimateCamera1107 != null) { StopCoroutine(ultimateCamera1107); RestoreCamera1107(); }
            ultimateCamera1107 = StartCoroutine(UltimateCameraRoutine1107());
        }

        private IEnumerator UltimateCameraRoutine1107()
        {
            Transform root = psdRoot;
            cameraHomePosition1107 = root.localPosition;
            cameraHomeScale1107 = root.localScale;
            cameraHomeSaved1107 = true;
            // Focus: the centre of the enemy stage (PSD x 1473, y 2200), in the root's parent space.
            Vector3 focusWorld = root.TransformPoint(new Vector3(PsdStageWidth1031 * .5f * PsdScale - ((RectTransform)root).rect.width * ((RectTransform)root).pivot.x,
                -2200f * PsdScale + ((RectTransform)root).rect.height * (1f - ((RectTransform)root).pivot.y), 0f));
            Vector3 focus = root.parent != null ? root.parent.InverseTransformPoint(focusWorld) : focusWorld;
            // In close (0.11 s) → hold (0.07 s) → fast pull back with double overshoot (0.28 s).
            float[] times = { .11f, .07f, .28f };
            float elapsed = 0f, total = times[0] + times[1] + times[2];
            while (elapsed < total && !closing)
            {
                elapsed += Time.unscaledDeltaTime;
                float s;
                if (elapsed < times[0]) s = Mathf.Lerp(1f, UltimatePushScale1107, Smooth1107(elapsed / times[0]));
                else if (elapsed < times[0] + times[1]) s = UltimatePushScale1107;
                else
                {
                    float t = Mathf.Clamp01((elapsed - times[0] - times[1]) / times[2]);
                    // Back past 1 to UltimatePullScale1107 (0.94) and settle (a stronger "pull away").
                    s = t < .6f ? Mathf.Lerp(UltimatePushScale1107, UltimatePullScale1107, Smooth1107(t / .6f))
                        : Mathf.Lerp(UltimatePullScale1107, 1f, Smooth1107((t - .6f) / .4f));
                }
                ApplyCamera1107(root, focus, s);
                yield return null;
            }
            RestoreCamera1107();
            ultimateCamera1107 = null;
            if (!closing) stageShake = StartCoroutine(ShakeStage(.3f, 18f));
        }

        private void ApplyCamera1107(Transform root, Vector3 focus, float s)
        {
            root.localScale = cameraHomeScale1107 * s;
            root.localPosition = cameraHomePosition1107 + (cameraHomePosition1107 - focus) * (s - 1f);
        }

        private void RestoreCamera1107()
        {
            if (!cameraHomeSaved1107 || psdRoot == null) return;
            psdRoot.localScale = cameraHomeScale1107;
            psdRoot.localPosition = cameraHomePosition1107;
        }

        private static float Smooth1107(float t) => t * t * (3f - 2f * t);
    }
}
