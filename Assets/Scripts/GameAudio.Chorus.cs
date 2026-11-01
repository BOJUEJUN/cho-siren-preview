using UnityEngine;

namespace ChoSiren
{
    public sealed partial class GameAudio
    {
        private AudioSource chorusSource;
        private string activeChorusOwner;
        public event System.Action BattleChorusInterrupted;
        public bool IsBattleChorusPlaying => chorusSource != null && chorusSource.isPlaying;
        public bool BattleChorusAvailable => MusicEnabled && SfxEnabled && MusicTransportAvailable &&
            !MediaOwnsMusic && CurrentMusicContext == MusicContext.Battle;

        // A short wordless response, mixed below speech. This source never acquires a music
        // lease, replaces the BGM clip, seeks its transport or applies another duck multiplier.
        public bool PlayBattleChorus(string memberId, int take, string owner)
        {
            if (!BattleChorusAvailable || string.IsNullOrEmpty(owner)) return false;
            if (memberId != "xingli" && memberId != "feiyin" && memberId != "wubai" && memberId != "yeying") return false;
            AudioClip clip = Resources.Load<AudioClip>("Audio/Chorus/" + memberId + "-" + (take % 2 + 1));
            if (clip == null) return false;
            if (chorusSource == null)
            {
                chorusSource = gameObject.AddComponent<AudioSource>();
                chorusSource.playOnAwake = false;
                chorusSource.loop = false;
                chorusSource.spatialBlend = 0f;
                chorusSource.ignoreListenerPause = true;
            }
            chorusSource.Stop();
            chorusSource.clip = clip;
            chorusSource.volume = .48f * MusicScale;
            activeChorusOwner = owner;
            chorusSource.Play();
            return true;
        }

        public void StopBattleChorus(string owner)
        {
            if (string.IsNullOrEmpty(owner) || activeChorusOwner != owner) return;
            chorusSource?.Stop();
            activeChorusOwner = null;
        }

        private void ApplyBattleChorusSettings()
        {
            if (chorusSource != null) chorusSource.volume = .48f * MusicScale;
            if (!BattleChorusAvailable) InterruptBattleChorus();
        }

        private void InterruptBattleChorus()
        {
            chorusSource?.Stop();
            activeChorusOwner = null;
            // Cancel queued responses synchronously. A background/mute/CG round-trip may
            // happen without a LateUpdate between it, especially on mobile or WebGL.
            BattleChorusInterrupted?.Invoke();
        }
    }
}
