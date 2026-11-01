using System.Collections.Generic;
using UnityEngine;

namespace ChoSiren
{
    /// <summary>
    /// 2026-11-01 唱片收藏: play modes, seeking and progress for the Home transport. Every rule of the
    /// existing transport (context routing, web request cancellation, per-song bookmarks, pause owned by
    /// the lobby) is unchanged; these members only read it or reuse its own start path.
    /// </summary>
    public sealed partial class GameAudio
    {
        private readonly Dictionary<string, AudioClip> nativeTrackClips = new Dictionary<string, AudioClip>(System.StringComparer.Ordinal);

        public HomeMusicPlaylist.PlayMode HomeMusicPlayMode =>
            model != null ? model.MusicPlayMode : HomeMusicPlaylist.PlayMode.ListLoop;

        /// <summary>Raised when single-loop (or a one-song list) starts the same song over.</summary>
        public event System.Action HomeMusicRestarted;

        /// <summary>Imported native clip for a song (Resources path in the playlist), or null to stream it.</summary>
        private AudioClip NativeHomeClip(int index)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return null;
#else
            string path = HomeMusicPlaylist.Get(index).Clip;
            if (string.IsNullOrEmpty(path)) return null;
            if (path == HomeMusicPlaylist.NativeHomeClipPath) return nativeHomeMusicClip;
            if (!nativeTrackClips.TryGetValue(path, out AudioClip clip))
                nativeTrackClips[path] = clip = Resources.Load<AudioClip>(path);
            // One AudioClip cannot belong to the Home and Battle contexts at once; such a song streams instead.
            return clip != null && clip != nativeBattleMusicClip ? clip : null;
#endif
        }

        private void AdvanceFinishedHomeSong()
        {
            HomeMusicPlaylist.PlayMode mode = HomeMusicPlayMode;
            int next = mode == HomeMusicPlaylist.PlayMode.Shuffle
                ? HomeMusicPlaylist.ShuffleIndex(HomeMusicTrackIndex, model)
                : HomeMusicPlaylist.NextIndex(HomeMusicTrackIndex, model);
            if (mode == HomeMusicPlaylist.PlayMode.SingleLoop || next == HomeMusicTrackIndex) RestartHomeSong();
            else SelectHomeMusic(next);
        }

        private void RestartHomeSong()
        {
            AudioClip clip = musicClip;
            if (clip == null || musicSource == null || musicSource.clip != clip) return;
            musicBookmarks[HomeMusicPlaylist.Get(HomeMusicTrackIndex).BookmarkKey] = 0f;
            musicSource.Stop();
            musicHasStarted = false;
            observedHomePlayback = false;
            lastHomePlaybackPosition = 0f;
            ReconcileMusicClip(clip);
            HomeMusicRestarted?.Invoke();
        }

        private bool HomeClipLoaded(out AudioClip clip)
        {
            clip = musicClip;
            return clip != null && musicSource != null && musicSource.clip == clip &&
                   loadedMusicContext == MusicContext.Home && loadedHomeMusicTrackIndex == HomeMusicTrackIndex &&
                   clip.loadState == AudioDataLoadState.Loaded && clip.length > 0f;
        }

        /// <summary>Length of a song: the decoded clip when it is the loaded one, otherwise the playlist value.</summary>
        public float HomeMusicLength(int index)
        {
            if (index == HomeMusicTrackIndex && HomeClipLoaded(out AudioClip clip)) return clip.length;
            return HomeMusicPlaylist.Get(index).Duration;
        }

        /// <summary>
        /// Current Home position/length without allocating. A paused source reports 0 in Unity 6.6, so a paused
        /// or not yet started song shows its bookmark. Returns whether the song's audio is loaded.
        /// </summary>
        public bool TryGetHomeMusicProgress(out float time, out float length)
        {
            HomeMusicPlaylist.Track track = HomeMusicPlaylist.Get(HomeMusicTrackIndex);
            bool loaded = HomeClipLoaded(out AudioClip clip);
            length = loaded ? clip.length : track.Duration;
            if (loaded && musicSource.isPlaying) time = musicSource.time;
            else if (!musicBookmarks.TryGetValue(track.BookmarkKey, out time)) time = 0f;
            time = Mathf.Clamp(time, 0f, Mathf.Max(0f, length));
            return loaded;
        }

        /// <summary>
        /// Jumps the Home song to <paramref name="normalized"/> (0–1). Native sets the playhead directly; WebGL
        /// restarts its buffer at the position (set before Play, the order the transport already uses). When the
        /// audio is paused, not yet downloaded or cannot seek, the position becomes the song's bookmark so it starts
        /// there — never an error. Returns true when the live playhead moved.
        /// </summary>
        public bool SeekHomeMusic(float normalized)
        {
            if (musicSource == null || CurrentMusicContext != MusicContext.Home) return false;
            normalized = Mathf.Clamp01(float.IsNaN(normalized) ? 0f : normalized);
            HomeMusicPlaylist.Track track = HomeMusicPlaylist.Get(HomeMusicTrackIndex);
            bool loaded = HomeClipLoaded(out AudioClip clip);
            float length = loaded ? clip.length : track.Duration;
            if (length <= 0f) return false;
            // Stay clear of the last moments so a seek to the end still plays and then advances normally.
            float target = normalized * Mathf.Max(0f, length - .4f);
            musicBookmarks[track.BookmarkKey] = target;
            observedHomePlayback = false;
            lastHomePlaybackPosition = target;
            if (!loaded) return false;
            if (musicSource.isPlaying)
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                musicSource.Stop();
                musicSource.time = target;
                musicSource.Play();
#else
                musicSource.time = target;
#endif
                return true;
            }
            if (musicHasStarted)
            {
                // Paused: resume from the bookmark through the normal start path rather than UnPause.
                musicSource.Stop();
                musicHasStarted = false;
            }
            return false;
        }
    }
}
