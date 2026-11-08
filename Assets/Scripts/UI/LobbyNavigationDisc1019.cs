using System;
using UnityEngine;

namespace ChoSiren
{
    /// <summary>The background record turns once per selection; navigation hit targets and labels never rotate.
    /// 2026-11-08 (少爷: "光碟做缓慢旋转类似播放的光碟效果"): on top of that it keeps turning slowly like a playing record. The
    /// drift is visual only (shared across page rebuilds so it never jumps) and is not part of the remembered selection pose.</summary>
    public sealed class LobbyNavigationDisc1019 : MonoBehaviour
    {
        private RectTransform record;
        private Action<float> remember;
        private float start, target, elapsed;
        public bool Spinning { get; private set; }
        public float Angle { get; private set; }
        public const float IdleDegreesPerSecond = 7f;
        private static float idleDrift;

        public void Configure(RectTransform art, float from, float to, Action<float> poseChanged)
        {
            record = art; remember = poseChanged; start = from; target = to; elapsed = 0;
            Spinning = !BattlePresentationOptions.ReduceMotion && Mathf.Abs(to - from) > .01f;
            Pose(Spinning ? from : to);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            Advance(dt);
            if (record == null || BattlePresentationOptions.ReduceMotion || dt <= 0f || dt > 1f) return;
            idleDrift = Mathf.Repeat(idleDrift - IdleDegreesPerSecond * dt, 360f); // clockwise, like a turntable
            record.localEulerAngles = new Vector3(0, 0, Angle + idleDrift);
        }

        public void Advance(float seconds)
        {
            if (!Spinning || record == null || seconds <= 0 || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            if (BattlePresentationOptions.ReduceMotion) { Spinning = false; Pose(target); return; }
            elapsed += seconds;
            float t = Mathf.Clamp01(elapsed / .68f);
            float eased = 1 - Mathf.Pow(1 - t, 3);
            Pose(Mathf.LerpUnclamped(start, target, eased));
            if (t >= 1) Spinning = false;
        }

        private void Pose(float degrees)
        {
            Angle = degrees;
            if (record != null) record.localEulerAngles = new Vector3(0, 0, degrees + (Application.isPlaying ? idleDrift : 0f));
            remember?.Invoke(degrees);
        }
    }
}
