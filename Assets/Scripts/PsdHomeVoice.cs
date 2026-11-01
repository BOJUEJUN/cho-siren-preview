using System.Runtime.InteropServices;
using UnityEngine;

namespace ChoSiren
{
    internal static class PsdHomeVoice
    {
        private static readonly string[] Lines =
        {
            "ねえ、今日も一緒に歌ってくれる？",
            "この光、きっと君にも届くよ。",
            "さあ、私たちのステージを始めよう！"
        };

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void ChoSirenSpeakJapanese(string line);
#endif

        public static void Speak(int index)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try { ChoSirenSpeakJapanese(Lines[index % Lines.Length]); }
            catch (System.Exception error) { Debug.LogWarning("Japanese dialogue unavailable: " + error.Message); }
#endif
        }
    }
}
