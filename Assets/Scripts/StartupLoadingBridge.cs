using System.Runtime.InteropServices;
using UnityEngine;

namespace ChoSiren
{
    /// <summary>Completion is independent of decoration, modals and music ownership.</summary>
    public static class StartupLoadingBridge
    {
        public static bool IsReady { get; private set; }
        public static string LastError { get; private set; }
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void ChoSirenStartupReport(string type, float progress, string message);
#endif
        public static void Begin()
        {
            IsReady = false;
            LastError = null;
            Progress(0f);
        }

        public static void Progress(float value)
        {
            if (IsReady) return;
#if UNITY_WEBGL && !UNITY_EDITOR
            ChoSirenStartupReport("progress", Mathf.Clamp01(value), "正在准备大厅素材");
#endif
        }

        public static void Ready()
        {
            if (IsReady) return;
            IsReady = true;
            LastError = null;
#if UNITY_WEBGL && !UNITY_EDITOR
            ChoSirenStartupReport("ready", 1f, "大厅已准备完成");
#endif
        }

        public static void Fail(string message)
        {
            if (IsReady) return;
            LastError = string.IsNullOrEmpty(message) ? "游戏界面启动失败，请重新载入" : message;
#if UNITY_WEBGL && !UNITY_EDITOR
            ChoSirenStartupReport("error", 0f, LastError);
#endif
        }
    }
}
