using System;
using UnityEngine;

namespace ChoSiren
{
    /// <summary>Published CC0 sample-based UI feedback; independent of music and speech.</summary>
    public sealed partial class GameAudio
    {
        const string UiAudio1102 = "Audio/Ui1102/";
        AudioSource vinylScrubSource1102;
        AudioClip vinylScrubClip1102, vinylTickClip1102;
        float vinylLastRequest1102, vinylTargetGain1102, vinylTargetPitch1102 = 1f;
        float uiLastPlayed1102 = float.NegativeInfinity;
        AudioClip uiLastClip1102;

        public void PlayUiAction(string action)
        {
            string name = (action ?? string.Empty).ToLowerInvariant();
            string clip = name.Contains("close") || name.Contains("cancel") || name.Contains("back") || name.Contains("exit")
                ? "ui-cancel"
                : name.Contains("error") || name.Contains("invalid") || name.Contains("failed") ? "ui-error"
                : name.Contains("next") || name.Contains("previous") || name.Contains("switch") || name.Contains("swap") ||
                  name.StartsWith("nav") || name.Contains("pool") || name.Contains("tab") ? "ui-switch"
                : name.Contains("confirm") || name.Contains("accept") || name.Contains("agree") ||
                  name.Contains("save") || name.Contains("claim") ? "ui-confirm" : "ui-click";
            PlayRecordedUi1102(Resources.Load<AudioClip>(UiAudio1102 + clip), 1f,
                clip == "ui-confirm" || clip == "ui-error");
        }

        public void PlayCancel() => PlayUiAction("cancel");
        public void PlayError() => PlayUiAction("error");
        public void PlaySwitch() => PlayUiAction("switch");

        public void PlayUiMessage(string message)
        {
            if (!string.IsNullOrEmpty(message) && (message.Contains("不足") || message.Contains("失败") ||
                message.Contains("无法") || message.Contains("不能") || message.Contains("未解锁"))) PlayError();
        }

        void PlayRecordedUi1102(AudioClip clip, float gain = 1f, bool priority = false)
        {
            if (model == null || sfxSource == null || clip == null || !SfxEnabled || applicationPaused ||
                SfxScale <= 0f) return;
            if (Time.unscaledTime - uiLastPlayed1102 < .045f)
            {
                if (!priority || clip == uiLastClip1102) return;
                sfxSource.Stop(); // A clear success/error result replaces the preceding generic click.
            }
            uiLastPlayed1102 = Time.unscaledTime;
            uiLastClip1102 = clip;
            ResumeAfterUserGesture();
            sfxSource.PlayOneShot(clip, gain);
        }

        /// <summary>Movement speed, 0..1; the sound exists only while a record is actually moving.</summary>
        public void SetVinylScrub(float normalizedSpeed)
        {
            if (!isActiveAndEnabled || !SfxEnabled || SfxScale <= 0f || applicationPaused || float.IsNaN(normalizedSpeed) ||
                float.IsInfinity(normalizedSpeed) || normalizedSpeed <= .015f)
            { StopVinylScrub(); return; }
            if (vinylScrubClip1102 == null) vinylScrubClip1102 = Resources.Load<AudioClip>(UiAudio1102 + "vinyl-scrub");
            if (vinylScrubClip1102 == null) return;
            if (vinylScrubSource1102 == null)
            {
                vinylScrubSource1102 = gameObject.AddComponent<AudioSource>();
                vinylScrubSource1102.playOnAwake = false;
                vinylScrubSource1102.loop = true;
                vinylScrubSource1102.spatialBlend = 0f;
                vinylScrubSource1102.ignoreListenerPause = false;
                vinylScrubSource1102.clip = vinylScrubClip1102;
            }
            float speed = Mathf.Clamp01(normalizedSpeed);
            vinylLastRequest1102 = Time.unscaledTime;
            vinylTargetGain1102 = Mathf.Lerp(.2f, .65f, Mathf.Sqrt(speed));
            vinylTargetPitch1102 = Mathf.Lerp(.7f, 1.45f, speed);
            vinylScrubSource1102.mute = false;
            if (!vinylScrubSource1102.isPlaying)
            {
                vinylScrubSource1102.volume = vinylTargetGain1102 * SfxScale * .6f;
                vinylScrubSource1102.pitch = vinylTargetPitch1102;
                vinylScrubSource1102.Play();
            }
        }

        // Stops the continuous scratch only. A short snap/tick on sfxSource may finish naturally.
        public void StopVinylScrub()
        {
            vinylTargetGain1102 = 0f;
            if (vinylScrubSource1102 == null) return;
            vinylScrubSource1102.volume = 0f;
            if (vinylScrubSource1102.isPlaying) vinylScrubSource1102.Stop();
        }

        public void PlayVinylSnap() => PlayVinylTick();
        public void PlayVinylTick()
        {
            if (vinylTickClip1102 == null) vinylTickClip1102 = Resources.Load<AudioClip>(UiAudio1102 + "vinyl-tick");
            PlayRecordedUi1102(vinylTickClip1102, .8f);
        }

        void ApplyRecordedUiSettings1102()
        {
            if (vinylScrubSource1102 == null) return;
            vinylScrubSource1102.mute = !SfxEnabled;
            if (!SfxEnabled || SfxScale <= 0f) StopVinylScrub();
            else vinylScrubSource1102.volume = vinylTargetGain1102 * SfxScale;
        }

        void LateUpdate()
        {
            if (vinylScrubSource1102 == null) return;
            if (!SfxEnabled || applicationPaused || Time.unscaledTime - vinylLastRequest1102 > .10f)
            { StopVinylScrub(); return; }
            vinylScrubSource1102.volume = Mathf.MoveTowards(vinylScrubSource1102.volume,
                vinylTargetGain1102 * SfxScale, Time.unscaledDeltaTime * 8f);
            vinylScrubSource1102.pitch = Mathf.MoveTowards(vinylScrubSource1102.pitch,
                vinylTargetPitch1102, Time.unscaledDeltaTime * 5f);
        }
    }
}
