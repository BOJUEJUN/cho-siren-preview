using System.Runtime.InteropServices;

namespace ChoSiren
{
    /// <summary>Match the WebGL rhythm-stage overlay to the actual Unity lobby lifetime.</summary>
    public static class PsdHomeStageBridge
    {
        public static bool Active { get; private set; }
        public static bool UiHidden { get; private set; }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void ChoSirenHomeStageActive(int active);
        [DllImport("__Internal")]
        private static extern void ChoSirenHomeStageUiHidden(int hidden);
        [DllImport("__Internal")]
        private static extern void ChoSirenHomeStageClick(string action, float x, float y);
#endif

        public static void SetActive(bool active)
        {
            Active = active;
            if (!active) SetUiHidden(false);
#if UNITY_WEBGL && !UNITY_EDITOR
            ChoSirenHomeStageActive(active ? 1 : 0);
#endif
        }

        public static void SetUiHidden(bool hidden)
        {
            UiHidden = hidden;
#if UNITY_WEBGL && !UNITY_EDITOR
            ChoSirenHomeStageUiHidden(hidden ? 1 : 0);
#endif
        }

        public static void PlayClick(string action, float x, float y)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            ChoSirenHomeStageClick(action, x, y);
#endif
        }
    }
}
