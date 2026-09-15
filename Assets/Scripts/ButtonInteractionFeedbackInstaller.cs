using UnityEngine;
using UnityEngine.EventSystems;
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
                if (candidate == null) continue;
                ButtonInteractionFeedback existing = candidate.GetComponent<ButtonInteractionFeedback>();
                // Respect authored feedback and unrelated event callbacks. Never clear EventTrigger entries.
                if (candidate.GetComponent<LobbyHotspotFeedback>() != null || HasCustomPointerFeedback(candidate))
                {
                    if (existing != null) { existing.enabled = false; Destroy(existing); }
                    continue;
                }
                if (existing != null) continue;
                candidate.gameObject.AddComponent<ButtonInteractionFeedback>();
                installed++;
            }

            return installed;
        }

        private static bool HasCustomPointerFeedback(Button candidate)
        {
            EventTrigger trigger = candidate.GetComponent<EventTrigger>();
            if (trigger == null || !trigger.enabled || trigger.triggers == null) return false;
            foreach (EventTrigger.Entry entry in trigger.triggers)
            {
                if (entry == null) continue;
                if (entry.eventID == EventTriggerType.PointerEnter || entry.eventID == EventTriggerType.PointerExit ||
                    entry.eventID == EventTriggerType.PointerDown || entry.eventID == EventTriggerType.PointerUp ||
                    entry.eventID == EventTriggerType.Select || entry.eventID == EventTriggerType.Deselect) return true;
            }
            return false;
        }
    }
}
