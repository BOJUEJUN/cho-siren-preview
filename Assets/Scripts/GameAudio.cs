using UnityEngine;

namespace ChoSiren
{
    /// <summary>
    /// Lightweight runtime audio used by the prototype. It keeps the music and sound settings
    /// genuinely audible without adding a large streamed soundtrack to the first build.
    /// </summary>
    public sealed class GameAudio : MonoBehaviour
    {
        private GameModel model;
        private AudioSource musicSource;
        private AudioSource sfxSource;
        private AudioClip musicClip;
        private AudioClip clickClip;
        private AudioClip successClip;
        private bool lobbyVideoOwnsMusic;

        public void Initialize(GameModel gameModel)
        {
            model = gameModel;

            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = true;
            musicSource.volume = 0.12f;
            musicSource.ignoreListenerPause = true;

            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
            sfxSource.volume = 0.32f;
            sfxSource.ignoreListenerPause = true;

            musicClip = CreateMusicLoop();
            clickClip = CreateTone("CHO-SIREN Click", 740f, 0.075f, 0.16f, 0.010f);
            successClip = CreateSuccessTone();
            musicSource.clip = musicClip;
            ApplySettings();
        }

        public void ApplySettings()
        {
            if (model == null || musicSource == null || sfxSource == null) return;

            musicSource.mute = !model.Save.MusicEnabled;
            sfxSource.mute = !model.Save.SfxEnabled;
            bool playFallback = ShouldPlayFallbackMusic(model.Save.MusicEnabled, lobbyVideoOwnsMusic);
            if (playFallback && !musicSource.isPlaying) musicSource.Play();
            if (!playFallback && musicSource.isPlaying) musicSource.Pause();
        }

        public void SetLobbyVideoMusicActive(bool active)
        {
            lobbyVideoOwnsMusic = active;
            ApplySettings();
        }

        public void ResumeAfterUserGesture()
        {
            ApplySettings();
        }

        public void PlayClick()
        {
            if (model == null || sfxSource == null) return;
            ResumeAfterUserGesture();
            if (!model.Save.SfxEnabled) return;
            sfxSource.PlayOneShot(clickClip);
        }

        public void PlaySuccess()
        {
            if (model == null || sfxSource == null) return;
            ResumeAfterUserGesture();
            if (!model.Save.SfxEnabled) return;
            sfxSource.PlayOneShot(successClip, 0.9f);
        }

        public static bool ShouldPlayFallbackMusic(bool musicEnabled, bool lobbyVideoOwnsMusic) =>
            musicEnabled && !lobbyVideoOwnsMusic;

        private void OnDestroy()
        {
            if (musicClip != null) Destroy(musicClip);
            if (clickClip != null) Destroy(clickClip);
            if (successClip != null) Destroy(successClip);
        }

        private static AudioClip CreateMusicLoop()
        {
            const int sampleRate = 22050;
            const float duration = 8f;
            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];
            float[] notes = { 220f, 277.18f, 329.63f, 415.30f, 329.63f, 277.18f, 246.94f, 329.63f };

            for (int index = 0; index < sampleCount; index++)
            {
                float time = index / (float)sampleRate;
                int beat = Mathf.FloorToInt(time * 2f) % notes.Length;
                float localBeat = Mathf.Repeat(time * 2f, 1f);
                float envelope = Mathf.SmoothStep(1f, 0f, localBeat) * 0.42f + 0.16f;
                float pad = Mathf.Sin(2f * Mathf.PI * 110f * time) * 0.24f;
                float melody = Mathf.Sin(2f * Mathf.PI * notes[beat] * time) * envelope;
                float shimmer = Mathf.Sin(2f * Mathf.PI * notes[beat] * 2f * time) * envelope * 0.18f;
                float loopFade = Mathf.Min(Mathf.Clamp01(time / 0.08f), Mathf.Clamp01((duration - time) / 0.08f));
                samples[index] = (pad + melody + shimmer) * 0.18f * loopFade;
            }

            AudioClip clip = AudioClip.Create("CHO-SIREN Ambient", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateSuccessTone()
        {
            const int sampleRate = 22050;
            const float duration = 0.34f;
            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];
            float[] notes = { 523.25f, 659.25f, 783.99f };

            for (int index = 0; index < sampleCount; index++)
            {
                float time = index / (float)sampleRate;
                int note = Mathf.Min(notes.Length - 1, Mathf.FloorToInt(time / (duration / notes.Length)));
                float noteTime = Mathf.Repeat(time, duration / notes.Length);
                float envelope = Mathf.Clamp01(1f - noteTime / (duration / notes.Length));
                samples[index] = Mathf.Sin(2f * Mathf.PI * notes[note] * time) * envelope * 0.22f;
            }

            AudioClip clip = AudioClip.Create("CHO-SIREN Success", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateTone(string name, float frequency, float duration, float volume, float attack)
        {
            const int sampleRate = 22050;
            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];
            for (int index = 0; index < sampleCount; index++)
            {
                float time = index / (float)sampleRate;
                float fadeIn = Mathf.Clamp01(time / Mathf.Max(0.001f, attack));
                float fadeOut = Mathf.Clamp01((duration - time) / Mathf.Max(0.001f, duration * 0.45f));
                samples[index] = Mathf.Sin(2f * Mathf.PI * frequency * time) * volume * fadeIn * fadeOut;
            }

            AudioClip clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
