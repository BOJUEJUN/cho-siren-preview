using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>
    /// Discovers runtime-created buttons below a canvas, including inactive panel pages.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ButtonInteractionFeedbackInstaller : MonoBehaviour
    {
        private const float ScanInterval = 0.15f;
        private float nextScanAt;

        private void OnEnable()
        {
            nextScanAt = 0f;
            InstallNow();
        }

        private void LateUpdate()
        {
            if (Time.unscaledTime < nextScanAt) return;
            InstallNow();
            nextScanAt = Time.unscaledTime + ScanInterval;
        }

        public int InstallNow()
        {
            int installed = 0;
            Button[] buttons = GetComponentsInChildren<Button>(true);
            for (int index = 0; index < buttons.Length; index++)
            {
                Button candidate = buttons[index];
                if (candidate == null || candidate.GetComponent<ButtonInteractionFeedback>() != null) continue;
                candidate.gameObject.AddComponent<ButtonInteractionFeedback>();
                installed++;
            }

            return installed;
        }
    }
}
