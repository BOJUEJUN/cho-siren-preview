using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.UI
{
    /// <summary>Entrance only: existing result data, MVP screens and ticker remain authoritative.</summary>
    public sealed class BattleResultEncore1019 : MonoBehaviour
    {
        private Text title, stars;
        private string finalStars;
        private Vector3 titleScale;
        private int earned;
        private float elapsed;
        private bool playing;
        public void Play(Text existingTitle, Text existingStars, bool victory, int earnedStars)
        {
            Restore(); title = existingTitle; stars = existingStars;
            if (stars == null) return;
            if (title != null) titleScale = title.rectTransform.localScale;
            finalStars = stars.text;
            earned = victory ? Mathf.Clamp(earnedStars, 0, 3) : 0; elapsed = 0;
            playing = !BattlePresentationOptions.ReduceMotion;
            if (playing) Pose();
        }
        private void Update()
        {
            if (!playing) return;
            elapsed += Mathf.Min(Time.unscaledDeltaTime, .1f); Pose();
            if (elapsed >= .65f) { Restore(); playing = false; }
        }
        private void Pose()
        {
            float t = Mathf.Clamp01(elapsed / .3f);
            if (title != null) title.rectTransform.localScale = titleScale * (1 + .1f * Mathf.Sin(t * Mathf.PI));
            int shown = Mathf.Min(earned, Mathf.FloorToInt(elapsed / .13f));
            stars.text = new string('★', shown) + new string('☆', 3 - shown);
        }
        private void Restore()
        {
            if (title != null) title.rectTransform.localScale = titleScale;
            if (stars != null && finalStars != null) stars.text = finalStars;
        }
        private void OnDisable() { Restore(); playing = false; }
    }
}
