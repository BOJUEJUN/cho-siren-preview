using System;
using UnityEngine;

namespace ChoSiren
{
    /// <summary>The background record turns once; navigation hit targets and labels never rotate.</summary>
    public sealed class LobbyNavigationDisc1019 : MonoBehaviour
    {
        private RectTransform record;
        private Action<float> remember;
        private float start, target, elapsed;
        public bool Spinning { get; private set; }
        public float Angle { get; private set; }

        public void Configure(RectTransform art, float from, float to, Action<float> poseChanged)
        {
            record = art; remember = poseChanged; start = from; target = to; elapsed = 0;
            Spinning = !BattlePresentationOptions.ReduceMotion && Mathf.Abs(to - from) > .01f;
            Pose(Spinning ? from : to);
        }

        private void Update() => Advance(Time.unscaledDeltaTime);

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
            if (record != null) record.localEulerAngles = new Vector3(0, 0, degrees);
            remember?.Invoke(degrees);
        }
    }
}
