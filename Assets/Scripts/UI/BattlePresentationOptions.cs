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

        /// <summary>Whole-battle tempo at 1x: attacks, effects and the simulation clock all run
        /// at this fraction of real time so each hit stays readable. The previous .7 pace still
        /// felt rushed at the displayed 1x speed; .5 gives each action 40% more reading time.</summary>
        public const float Pace = .5f;

        /// <summary>Raised after <see cref="ReduceMotion"/> changes so open panels can re-read it.</summary>
        public static event Action Changed;

        // 2026-10-25: effects ask this hundreds of times a frame; each PlayerPrefs read is a system defaults lookup
        // (the iOS log was flooded with them). In play it is read once per frame; edit-time code reads it directly.
        static int cachedFrame = -1;
        static bool cachedValue;

        public static bool ReduceMotion
        {
            get
            {
                if (!Application.isPlaying) return PlayerPrefs.GetInt(ReduceMotionKey, 0) == 1;
                int frame = Time.frameCount;
                if (frame != cachedFrame) { cachedFrame = frame; cachedValue = PlayerPrefs.GetInt(ReduceMotionKey, 0) == 1; }
                return cachedValue;
            }
            set
            {
                PlayerPrefs.SetInt(ReduceMotionKey, value ? 1 : 0);
                PlayerPrefs.Save();
                cachedFrame = -1;
                Changed?.Invoke();
            }
        }

        public static void Toggle() => ReduceMotion = !ReduceMotion;

        public static string Label(bool reduced) => reduced ? "减少动画：开" : "减少动画：关";
    }
}
