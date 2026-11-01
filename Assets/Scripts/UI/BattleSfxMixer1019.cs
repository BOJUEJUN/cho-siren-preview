using UnityEngine;

namespace ChoSiren
{
    /// <summary>
    /// One clip per source and a bounded source pool. Music, UI clicks, and approved character
    /// voices keep their existing transports, pitch, ownership, and independent settings.
    /// </summary>
    public sealed class BattleSfxMixer1019 : MonoBehaviour
    {
        public const float OutputGain = .62f;
        private readonly AudioSource[] channels = new AudioSource[BattleSfxVoicePolicy1019.VoiceLimit];
        private readonly BattleSfxVoicePolicy1019 policy = new BattleSfxVoicePolicy1019();
        private bool muted;
        public int AllocatedChannelCount { get; private set; }
        public int ActiveVoiceCount => policy.ActiveVoiceCount(Time.unscaledTime);
        public BattleSfxVoicePolicy1019 Policy => policy;

        public void Initialize(AudioSource firstChannel)
        {
            if (firstChannel == null || channels[0] != null) return;
            channels[0] = firstChannel;
            ConfigureSource(firstChannel);
            AllocatedChannelCount = 1;
        }

        public bool Play(string name, AudioClip clip, float gain = 1f)
        {
            if (muted || clip == null || !isActiveAndEnabled || channels[0] == null) return false;
            if (!policy.TryReserve(name, clip.length, gain, Time.unscaledTime, out var voice)) return false;
            AudioSource source = GetChannel(voice.Channel);
            source.Stop();
            source.clip = clip;
            source.volume = OutputGain * voice.Gain;
            source.pitch = voice.Pitch;
            // Native benefit only; the pool, not this setting, enforces the WebGL limit.
            source.priority = Mathf.Clamp(256 - voice.Priority * 2, 0, 256);
            source.Play();
            return true;
        }

        public void SetMuted(bool value)
        {
            muted = value;
            for (int i = 0; i < channels.Length; i++) if (channels[i] != null) channels[i].mute = value;
        }

        public void StopAll()
        {
            for (int i = 0; i < channels.Length; i++)
                if (channels[i] != null) { channels[i].Stop(); channels[i].clip = null; }
            policy.Clear();
        }

        private AudioSource GetChannel(int index)
        {
            if (channels[index] != null) return channels[index];
            var child = new GameObject("BattleSfxVoice" + (index + 1));
            child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>();
            ConfigureSource(source);
            channels[index] = source;
            AllocatedChannelCount++;
            return source;
        }

        private void ConfigureSource(AudioSource source)
        {
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.volume = OutputGain;
            source.mute = muted;
            source.ignoreListenerPause = channels[0] != null && channels[0].ignoreListenerPause;
        }

        private void OnDisable() => StopAll();
    }
}
