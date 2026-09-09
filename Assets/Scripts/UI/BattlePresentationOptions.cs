using System;
using UnityEngine;

namespace ChoSiren
{
    /// <summary>
    /// Presentation-only options that never touch battle math. Reduced motion skips cut-ins,
    /// dice tumble and screen shake, but the same events, numbers and results still play.
    /// </summary>
    public static class BattlePresentationOptions
    {
        public const string ReduceMotionKey = "cho-siren.battle.reduce-motion";

        /// <summary>Raised after <see cref="ReduceMotion"/> changes so open panels can re-read it.</summary>
        public static event Action Changed;

        public static bool ReduceMotion
        {
            get => PlayerPrefs.GetInt(ReduceMotionKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(ReduceMotionKey, value ? 1 : 0);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        public static void Toggle() => ReduceMotion = !ReduceMotion;

        public static string Label(bool reduced) => reduced ? "减少动画：开" : "减少动画：关";
    }
}
