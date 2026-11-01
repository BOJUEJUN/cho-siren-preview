using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace ChoSiren
{
    /// <summary>Clipboard / file / paste helpers for the save code (browser jslib on WebGL).</summary>
    public static class SaveTransferBridge1021
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern int ChoSirenCopyText(string text);
        [DllImport("__Internal")] private static extern int ChoSirenDownloadText(string name, string text);
        [DllImport("__Internal")] private static extern string ChoSirenPromptText(string message);
#endif
        public static bool CanDownload =>
#if UNITY_WEBGL && !UNITY_EDITOR
            true;
#else
            false;
#endif

        public static bool Copy(string text)
        {
            try
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                if (ChoSirenCopyText(text) == 1) return true;
#endif
                GUIUtility.systemCopyBuffer = text;
                return true;
            }
            catch (Exception) { return false; }
        }

        public static bool Download(string fileName, string text)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try { return ChoSirenDownloadText(fileName, text) == 1; } catch (Exception) { return false; }
#else
            return false;
#endif
        }

        /// <summary>Browser: a native paste prompt. Elsewhere: whatever is on the clipboard.</summary>
        public static string Paste(string message)
        {
            try
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return ChoSirenPromptText(message) ?? string.Empty;
#else
                return GUIUtility.systemCopyBuffer ?? string.Empty;
#endif
            }
            catch (Exception) { return string.Empty; }
        }
    }
}
