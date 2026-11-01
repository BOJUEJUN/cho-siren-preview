using UnityEngine;

namespace ChoSiren
{
    /// <summary>
    /// Desktop builds (2026-10-24, owner: "在电脑上面打开感觉很小"): the fixed 470x1010 default is pixels, so on a
    /// Retina Mac it opened as a 235x505-point strip. On launch, a window shorter than 60% of the display grows to
    /// 86% of the display height at the game's portrait shape. A window the player made bigger is left alone, and
    /// fullscreen (green button / Alt+Enter) still works.
    /// </summary>
    public static class DesktopWindow1024
    {
        private const float PortraitAspect = 720f / 1536f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Fit()
        {
#if (UNITY_STANDALONE_OSX || UNITY_STANDALONE_WIN) && !UNITY_EDITOR
            if (Screen.fullScreenMode != FullScreenMode.Windowed) return;
            // Capture runs pass an exact -screen-width/-screen-height; keep those.
            foreach (string arg in System.Environment.GetCommandLineArgs())
                if (arg == "-screen-height" || arg == "-smokeCapture") return;
            int displayHeight = Display.main.systemHeight;
            int displayWidth = Display.main.systemWidth;
            if (displayHeight <= 0 || Screen.height >= displayHeight * .6f) return;
            int height = Mathf.RoundToInt(displayHeight * .86f);
            int width = Mathf.RoundToInt(height * PortraitAspect);
            if (width > displayWidth * .9f) { width = Mathf.RoundToInt(displayWidth * .9f); height = Mathf.RoundToInt(width / PortraitAspect); }
            Screen.SetResolution(width, height, FullScreenMode.Windowed);
            Debug.Log($"CHO_SIREN_WINDOW {Screen.width}x{Screen.height} -> {width}x{height} (display {displayWidth}x{displayHeight})");
#endif
        }
    }
}
