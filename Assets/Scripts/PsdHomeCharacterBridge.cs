using System.Runtime.InteropServices;

namespace ChoSiren
{
    /// <summary>Play the supplied character performances on the WebGL portrait stage.</summary>
    internal static class PsdHomeCharacterBridge
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int ChoSirenCharacterMomentPlay(int index, int audioEnabled);
        [DllImport("__Internal")]
        private static extern void ChoSirenCharacterMomentStop();
        [DllImport("__Internal")]
        private static extern void ChoSirenCharacterIdle(int enabled);
#endif

        /// <summary>Allow or stop the page's idle loop (only the character with performances has one).</summary>
        public static void SetIdle(bool enabled)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            ChoSirenCharacterIdle(enabled ? 1 : 0);
#endif
        }

        public static void Stop()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            ChoSirenCharacterMomentStop();
#endif
        }

        public static bool Play(int index, bool audioEnabled)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return ChoSirenCharacterMomentPlay(index, audioEnabled ? 1 : 0) != 0;
#else
            return false;
#endif
        }
    }
}
