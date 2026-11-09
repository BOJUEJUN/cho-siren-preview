using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace ChoSiren
{
    /// <summary>
    /// One music transport with per-song Home/Battle bookmarks and independent SFX/voice sources.
    /// </summary>
    public sealed partial class GameAudio : MonoBehaviour
    {
        // Music assets measure about -14.5 LUFS / <= -1 dBTP. At 100, Home preserves that
        // mastered level; Battle keeps about 2 dB for overlapping hits and voices. Applying
        // another 0.55 multiplier here made Battle about -20 LUFS even at the maximum slider.
        public const float MusicOutputVolume = 1f;
        public const float BattleMusicOutputVolume = 0.8f;
        private const float BattleSfxOutputVolume = 1f;
        private float MusicScale => model == null ? 1f : Mathf.Clamp01(model.Save.MusicVolume / 100f);
        private float SfxScale => model == null ? 1f : Mathf.Clamp01(model.Save.SfxVolume / 100f);
        // External character-video audio keeps the existing Home mix. Short combat speech needs
        // far less attenuation so the beat does not repeatedly disappear between callouts.
        public const float DuckedMusicMultiplier = 0.28f;
        public const float VoiceDuckedMusicMultiplier = 0.60f;
        public const float VoiceDuckHoldSeconds = 0.18f;
        public const float VoiceDuckReleaseSeconds = 0.65f;
        public enum MusicContext { Home, Silent, Battle }

        public bool HasSoundtrack { get; private set; }
        public bool MusicEnabled => model != null && model.Save.MusicEnabled;
        public bool SfxEnabled => model != null && model.Save.SfxEnabled;
        public bool IsVoicePlaying => voiceSource != null && voiceSource.isPlaying;
        private string activeVoiceOwner;
        public bool MediaOwnsMusic => lobbyVideoOwnsMusic || mediaMusicOwners.Count > 0;
        public MusicContext CurrentMusicContext { get; private set; } = MusicContext.Home;
        public event Action<bool> MusicEnabledChanged;
        public int HomeMusicTrackIndex { get; private set; }
        public string HomeMusicTitle => HomeMusicPlaylist.Get(HomeMusicTrackIndex).Title;
        public event Action<int> HomeMusicTrackChanged;
        // Transport pause belongs to the lobby, not the global music preference. A paused
        // record therefore never silences the next battle or changes the saved volume.
        public bool HomeMusicPaused { get; private set; }
        public bool HomeMusicPlaying => CurrentMusicContext == MusicContext.Home && !HomeMusicPaused &&
            MusicEnabled && !MediaOwnsMusic && musicSource != null && musicSource.isPlaying;
        public event Action<bool> HomeMusicPausedChanged;

        private GameModel model;
        private AudioSource musicSource;
        private AudioSource sfxSource;
        private AudioSource voiceSource;
        private AudioClip musicClip;
        private AudioClip nativeHomeMusicClip;
        private AudioClip battleMusicClip;
        private AudioClip clickClip;
        private AudioClip successClip;
        private bool lobbyVideoOwnsMusic;
        private bool musicHasStarted;
        private bool ownsMusicClip;
        private bool externalMusicDucked;
        private bool applicationPaused;
        private bool publishedMusicSetting;
        private bool lastMusicEnabled;
        private readonly HashSet<string> mediaMusicOwners = new HashSet<string>(StringComparer.Ordinal);
        private MusicContext baseMusicContext = MusicContext.Home;
        private readonly Dictionary<string, ContextOwner> contextMusicOwners =
            new Dictionary<string, ContextOwner>(StringComparer.Ordinal);
        private readonly Dictionary<string, float> musicBookmarks = new Dictionary<string, float>(StringComparer.Ordinal);
        private int loadedHomeMusicTrackIndex;
        private bool observedHomePlayback;
        private float lastHomePlaybackPosition;
        private MusicContext loadedMusicContext = MusicContext.Home;
        private long contextSequence;
        private enum MusicFade { None, Out, In }
        private MusicFade musicFade;
        private float contextFadeGain = 1f;
        private float duckGain = 1f;
        private float externalDuckGain = 1f;
        private float voiceDuckGain = 1f;
        private float voiceDuckUntil;
        private bool resumeWithFade;

        // The same cancellation/decode contract serves WebGL and native StreamingAssets.
        // Native retains its already imported jazz and battle clips; new songs stay external.
        private const string WebBattleMusicFile = "battle-b0859b88305ca4f4.mp3";
        // 2026-10-21: boss stages have their own track (RunningHub Suno, external on every platform).
        private const string WebBossMusicFile = "boss-707b8a25a7adf39d.mp3";
        private bool bossBattle;
        private AudioClip nativeBattleMusicClip;
        public bool BossBattle => bossBattle;
        private UnityWebRequest webMusicRequest;
        private Coroutine webMusicRoutine;
        private AudioClip webPendingMusicClip;
        private AudioClip webOwnedMusicClip;
        private long webRequestSerial;
        private int webContextEntry;
        private int webFailedEntry = -1;
        private bool webBridgeRefreshPending;
        private bool webDestroying;
        private MusicContext webOwnedMusicContext;
        private int webOwnedHomeTrackIndex;
#if UNITY_WEBGL && !UNITY_EDITOR
        private bool webApplicationFocused = true;
#endif

        private readonly struct ContextOwner
        {
            public readonly MusicContext Context;
            public readonly long Sequence;
            public ContextOwner(MusicContext context, long sequence) { Context = context; Sequence = sequence; }
        }

        public void Initialize(GameModel gameModel)
        {
            if (musicSource != null) { ApplySettings(); return; }
            model = gameModel ?? throw new ArgumentNullException(nameof(gameModel));
            model.Changed += ApplySettings;

            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = false;
            musicSource.volume = MusicOutputVolume;
            musicSource.ignoreListenerPause = true;
            musicSource.spatialBlend = 0f;

            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
            sfxSource.volume = 0.32f;
            sfxSource.ignoreListenerPause = true;

            voiceSource = gameObject.AddComponent<AudioSource>();
            voiceSource.playOnAwake = false;
            voiceSource.loop = false;
            voiceSource.volume = 0.9f;
            voiceSource.ignoreListenerPause = true;

            // 2026-11-01: the lobby opens on the player's saved 首页曲 (first song when unset/locked).
            HomeMusicTrackIndex = HomeMusicPlaylist.OpeningIndex(model);
#if UNITY_WEBGL && !UNITY_EDITOR
            // Long music is external to the startup archive and never blocks app creation.
            HasSoundtrack = false;
#else
            nativeHomeMusicClip = LoadSoundtrackClip();
            HasSoundtrack = nativeHomeMusicClip != null;
            ownsMusicClip = !HasSoundtrack;
            if (ownsMusicClip) nativeHomeMusicClip = CreateMusicLoop();
            battleMusicClip = nativeBattleMusicClip = Resources.Load<AudioClip>("Audio/bgm-battle");
            musicClip = NativeHomeClip(HomeMusicTrackIndex);
#endif
            clickClip = Resources.Load<AudioClip>(UiAudio1102 + "ui-click");
            successClip = Resources.Load<AudioClip>(UiAudio1102 + "ui-confirm");
            musicSource.clip = musicClip;
            loadedHomeMusicTrackIndex = HomeMusicTrackIndex;
            ChoSirenMusicBridge.Attach(this);
            ApplySettings();
        }

        private static AudioClip LoadSoundtrackClip()
        {
            AudioClip clip = Resources.Load<AudioClip>("Audio/bgm");
            if (clip != null)
                Debug.Log($"[GameAudio] 使用自定义背景音乐：{clip.name}（{clip.length:0.#}s）");
            return clip;
        }

        public void ApplySettings()
        {
            MusicContext nextContext = ResolveMusicContext();
            if (nextContext != CurrentMusicContext)
            {
                webContextEntry++;
                CancelWebMusicRequest();
            }
            CurrentMusicContext = nextContext;
            if (model == null || musicSource == null || sfxSource == null) return;
            ApplyBattleChorusSettings();

            sfxSource.mute = !model.Save.SfxEnabled;
            sfxSource.volume = .85f * SfxScale;
            if (battleSfxSource != null) battleSfxSource.volume = BattleSfxOutputVolume * SfxScale;
            if (voiceSource != null) voiceSource.volume = SfxScale;
            if (battleSfxSource != null) battleSfxSource.mute = !model.Save.SfxEnabled;
            battleSfxMixer?.SetMuted(!model.Save.SfxEnabled);
            if (voiceSource != null) voiceSource.mute = !model.Save.SfxEnabled;
            ApplyRecordedUiSettings1102();
            PrepareWebMusic();
            AudioClip wantedClip = DesiredMusicClip();
            if (!CanPlayMusic(wantedClip))
            {
                musicSource.mute = true;
                PauseMusicImmediately();
                if (CurrentMusicContext == MusicContext.Silent || wantedClip == null) musicSource.volume = 0f;
                // Missing battle audio is silence; never leave the vocal clip eligible to resume.
                if (CurrentMusicContext == MusicContext.Battle && wantedClip == null)
                {
                    musicSource.clip = null;
                    musicHasStarted = false;
                }
            }
            else
            {
                ReconcileMusicClip(wantedClip);
                musicSource.mute = false;
            }
            if (!publishedMusicSetting || lastMusicEnabled != MusicEnabled)
            {
                publishedMusicSetting = true;
                lastMusicEnabled = MusicEnabled;
                MusicEnabledChanged?.Invoke(lastMusicEnabled);
            }
        }

        public void SetBaseMusicContext(MusicContext context)
        {
            ValidateMusicContext(context);
            baseMusicContext = context;
            ApplySettings();
        }

        public void SetContextMusicActive(string owner, MusicContext context, bool active)
        {
            if (string.IsNullOrEmpty(owner)) return;
            ValidateMusicContext(context);
            if (active)
            {
                if (contextMusicOwners.TryGetValue(owner, out ContextOwner existing) && existing.Context == context)
                    return; // Rebinding an existing page cannot jump above a newer overlay.
                contextMusicOwners[owner] = new ContextOwner(context, ++contextSequence);
            }
            else if (!contextMusicOwners.Remove(owner)) return;
            ApplySettings();
        }

        private static void ValidateMusicContext(MusicContext context)
        {
            if (context != MusicContext.Home && context != MusicContext.Silent && context != MusicContext.Battle)
                throw new ArgumentOutOfRangeException(nameof(context));
        }

        private MusicContext ResolveMusicContext()
        {
            MusicContext context = baseMusicContext;
            long newest = -1;
            foreach (ContextOwner owner in contextMusicOwners.Values)
                if (owner.Sequence > newest) { newest = owner.Sequence; context = owner.Context; }
            return context;
        }

        private AudioClip DesiredMusicClip() => CurrentMusicContext == MusicContext.Home ? musicClip
            : CurrentMusicContext == MusicContext.Battle ? battleMusicClip : null;

        private bool MusicTransportAvailable => !applicationPaused && isActiveAndEnabled
#if UNITY_WEBGL && !UNITY_EDITOR
            && webApplicationFocused
#endif
            ;

        private bool CanPlayMusic(AudioClip clip) => clip != null && MusicTransportAvailable &&
            (CurrentMusicContext != MusicContext.Home || !HomeMusicPaused) &&
            ShouldPlayFallbackMusic(MusicEnabled, MediaOwnsMusic);

        private float ActiveMusicVolume => CurrentMusicContext == MusicContext.Silent ||
            (CurrentMusicContext == MusicContext.Battle && battleMusicClip == null) ? 0f :
            musicSource != null && battleMusicClip != null && musicSource.clip == battleMusicClip
                ? BattleMusicOutputVolume * MusicScale : MusicOutputVolume * MusicScale;

        private void RememberMusicPosition()
        {
            AudioClip clip = musicSource.clip;
            // Unity 6.6 reports time=0 on a paused source. Retain the position captured
            // before pausing when settings or a later context change repeat this call.
            if (clip != null && musicHasStarted && musicSource.isPlaying)
                musicBookmarks[LoadedMusicBookmarkKey] = Mathf.Clamp(musicSource.time, 0f, Mathf.Max(0f, clip.length - .001f));
        }

        private void PauseMusicImmediately()
        {
            RememberMusicPosition();
            if (musicSource.isPlaying) musicSource.Pause();
            resumeWithFade |= musicHasStarted;
            musicFade = MusicFade.None;
            contextFadeGain = 1f;
        }

        private void ReconcileMusicClip(AudioClip wantedClip)
        {
            if (musicSource.clip != wantedClip)
            {
                if (musicSource.isPlaying && contextFadeGain > 0f)
                    musicFade = MusicFade.Out;
                else SwitchMusicClip(wantedClip);
                return;
            }
            if (musicFade == MusicFade.Out) musicFade = MusicFade.In;
            if (musicSource.isPlaying) return;
            if (resumeWithFade)
            {
                contextFadeGain = 0f;
                musicSource.volume = 0f;
                musicFade = MusicFade.In;
                resumeWithFade = false;
            }
            if (musicHasStarted) musicSource.UnPause();
            else
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                // Set the initial position before creating the live channel. A seek
                // immediately after Play can run while FakeMOD still marks it paused,
                // making its pitch correction fall back to 44.1 kHz instead of the buffer rate.
                if (musicBookmarks.TryGetValue(LoadedMusicBookmarkKey, out float webBookmark))
                    musicSource.time = Mathf.Clamp(webBookmark, 0f, Mathf.Max(0f, wantedClip.length - .001f));
#endif
                musicSource.Play();
                musicHasStarted = true;
#if !UNITY_WEBGL || UNITY_EDITOR
                // Native transport retains its existing resume order and seconds-based bookmark.
                if (musicBookmarks.TryGetValue(LoadedMusicBookmarkKey, out float bookmark))
                    musicSource.time = Mathf.Clamp(bookmark, 0f, Mathf.Max(0f, wantedClip.length - .001f));
#endif
            }
        }

        private void SwitchMusicClip(AudioClip clip)
        {
            RememberMusicPosition();
            if (musicSource.isPlaying) musicSource.Pause();
            musicSource.volume = 0f;
            musicSource.clip = clip;
            loadedMusicContext = CurrentMusicContext;
            loadedHomeMusicTrackIndex = HomeMusicTrackIndex;
            musicSource.loop = loadedMusicContext == MusicContext.Battle;
            observedHomePlayback = false;
            lastHomePlaybackPosition = 0f;
            musicHasStarted = false;
            contextFadeGain = 0f;
            musicFade = MusicFade.In;
            resumeWithFade = false;
            ReconcileMusicClip(clip);
        }

        private string LoadedMusicBookmarkKey => loadedMusicContext == MusicContext.Home
            ? HomeMusicPlaylist.Get(loadedHomeMusicTrackIndex).BookmarkKey : bossBattle ? "boss" : "battle";

        /// <summary>Chooses the boss or the regular battle track for the next/current battle.</summary>
        public void SetBossBattle(bool boss)
        {
            if (bossBattle == boss) return;
            if (musicSource != null) RememberMusicPosition();
            webContextEntry++;
            CancelWebMusicRequest();
            if (webOwnedMusicClip != null && webOwnedMusicContext == MusicContext.Battle) ReleaseWebMusicClip();
            bossBattle = boss;
            battleMusicClip = boss ? null : nativeBattleMusicClip;
            if (musicSource != null) ApplySettings();
        }

        /// <summary>Changes selection without ever enabling a muted music preference.</summary>
        public void SelectHomeMusic(int index)
        {
            HomeMusicPlaylist.Get(index);
            if (HomeMusicTrackIndex == index) return;
            if (musicSource != null) RememberMusicPosition();
            HomeMusicTrackIndex = index;
            webContextEntry++;
            CancelWebMusicRequest();
            if (webOwnedMusicClip != null && webOwnedMusicContext == MusicContext.Home) ReleaseWebMusicClip();
            musicClip = NativeHomeClip(index);
            observedHomePlayback = false;
            ApplySettings();
            HomeMusicTrackChanged?.Invoke(index);
        }

        public bool PreviousHomeMusic()
        {
            if (CurrentMusicContext != MusicContext.Home) return false;
            SelectHomeMusic(HomeMusicPlaylist.PreviousIndex(HomeMusicTrackIndex, model));
            return true;
        }

        /// <summary>Next unlocked song; in shuffle mode a random other one.</summary>
        public bool NextHomeMusic()
        {
            if (CurrentMusicContext != MusicContext.Home) return false;
            SelectHomeMusic(HomeMusicPlayMode == HomeMusicPlaylist.PlayMode.Shuffle
                ? HomeMusicPlaylist.ShuffleIndex(HomeMusicTrackIndex, model)
                : HomeMusicPlaylist.NextIndex(HomeMusicTrackIndex, model));
            return true;
        }

        public bool ToggleHomeMusicPaused()
        {
            if (CurrentMusicContext != MusicContext.Home) return false;
            HomeMusicPaused = !HomeMusicPaused;
            webContextEntry++;
            CancelWebMusicRequest();
            ApplySettings();
            HomeMusicPausedChanged?.Invoke(HomeMusicPaused);
            return true;
        }

        private bool WebMusicWanted(MusicContext context) => !webDestroying &&
            CurrentMusicContext == context && context != MusicContext.Silent &&
            (context != MusicContext.Home || !HomeMusicPaused) &&
            MusicTransportAvailable && ShouldPlayFallbackMusic(MusicEnabled, MediaOwnsMusic);

        private bool WebMusicRequestWanted(MusicContext context, int homeTrack, long serial, int entry) =>
            serial == webRequestSerial && entry == webContextEntry &&
            (context != MusicContext.Home || homeTrack == HomeMusicTrackIndex) && WebMusicWanted(context);

        private void PrepareWebMusic()
        {
            if (webOwnedMusicClip != null && (webOwnedMusicContext != CurrentMusicContext ||
                (webOwnedMusicContext == MusicContext.Home && webOwnedHomeTrackIndex != HomeMusicTrackIndex)))
                ReleaseWebMusicClip();
            if (CurrentMusicContext == MusicContext.Home && musicClip == null)
                musicClip = NativeHomeClip(HomeMusicTrackIndex);
            if (!WebMusicWanted(CurrentMusicContext))
            {
                CancelWebMusicRequest();
                return;
            }
            if (DesiredMusicClip() != null || webMusicRoutine != null || webFailedEntry == webContextEntry) return;
            // The first yield gives the app a frame to render before any optional music request starts.
            webMusicRoutine = StartCoroutine(LoadWebMusic(CurrentMusicContext, HomeMusicTrackIndex, ++webRequestSerial, webContextEntry));
        }

        private IEnumerator LoadWebMusic(MusicContext context, int homeTrack, long serial, int entry)
        {
            yield return null;
            if (!WebMusicRequestWanted(context, homeTrack, serial, entry))
            {
                webMusicRoutine = null;
                yield break;
            }
            string file = context == MusicContext.Home ? HomeMusicPlaylist.Get(homeTrack).FileName
                : bossBattle ? WebBossMusicFile : WebBattleMusicFile;
            string url = Application.streamingAssetsPath.TrimEnd('/') + "/Music/" + file;
            if (!url.Contains("://")) url = new Uri(url).AbsoluteUri;
            UnityWebRequest request = null;
            UnityWebRequestAsyncOperation operation = null;
            bool setupFailed = false;
            try
            {
                request = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.MPEG);
                var handler = (DownloadHandlerAudioClip)request.downloadHandler;
                handler.streamAudio = false;
                // Chromium already forces AudioBuffer decoding in Unity 6000.6. Use the
                // same path on every browser so readiness and metadata have one contract.
                handler.compressed = false;
                request.timeout = 20;
                webMusicRequest = request;
                operation = request.SendWebRequest();
            }
            catch (Exception)
            {
                setupFailed = true;
                request?.Dispose();
                webMusicRequest = null;
            }
            if (setupFailed)
            {
                webMusicRoutine = null;
                webFailedEntry = entry;
                Debug.LogWarning("Context music could not start downloading; this page remains silent.");
                yield break;
            }
            float downloadDeadline = Time.realtimeSinceStartup + 20f;
            while (!operation.isDone && Time.realtimeSinceStartup < downloadDeadline)
            {
                if (!WebMusicRequestWanted(context, homeTrack, serial, entry))
                {
                    if (serial == webRequestSerial) CancelWebMusicRequest();
                    yield break;
                }
                yield return null;
            }
            if (!WebMusicRequestWanted(context, homeTrack, serial, entry))
            {
                if (serial == webRequestSerial) CancelWebMusicRequest();
                yield break;
            }
            if (!operation.isDone) request.Abort();
            AudioClip downloaded = null;
            UnityWebRequest.Result result = request.result;
            long responseCode = request.responseCode;
            if (result == UnityWebRequest.Result.Success)
            {
                try { downloaded = DownloadHandlerAudioClip.GetContent(request); }
                catch (Exception) { }
            }
            // HTTP completion only starts Web Audio's asynchronous decodeAudioData.
            // Until its callback, Unity reports Loading with zero length/channels.
            // Own the pending clip explicitly so StopCoroutine cannot strand it.
            webPendingMusicClip = downloaded;
            float decodeDeadline = Time.realtimeSinceStartup + 20f;
            while (downloaded != null && downloaded.loadState != AudioDataLoadState.Failed)
            {
                if (!WebMusicRequestWanted(context, homeTrack, serial, entry))
                {
                    if (serial == webRequestSerial) CancelWebMusicRequest();
                    yield break;
                }
                if (downloaded.loadState == AudioDataLoadState.Loaded &&
                    downloaded.length > 0f && downloaded.channels > 0) break;
                if (Time.realtimeSinceStartup >= decodeDeadline) break;
                yield return null;
            }
            bool stillWanted = WebMusicRequestWanted(context, homeTrack, serial, entry);
            AudioDataLoadState loadState = downloaded != null ? downloaded.loadState : AudioDataLoadState.Failed;
            webPendingMusicClip = null;
            webMusicRequest = null;
            webMusicRoutine = null;
            request.Dispose();
            if (!stillWanted)
            {
                if (downloaded != null) Destroy(downloaded);
                yield break;
            }
            if (downloaded == null || loadState != AudioDataLoadState.Loaded ||
                downloaded.length <= 0f || downloaded.channels < 1)
            {
                if (downloaded != null) Destroy(downloaded);
                webFailedEntry = entry;
                Debug.LogWarning($"Context music {context} unavailable (HTTP {responseCode}, {result}, decode {loadState}); this page remains silent until reopened.");
                yield break;
            }
            webOwnedMusicClip = downloaded;
            webOwnedMusicContext = context;
            webOwnedHomeTrackIndex = homeTrack;
            downloaded.name = context == MusicContext.Home ? HomeMusicPlaylist.Get(homeTrack).Title : "Battle Web Music";
            if (context == MusicContext.Home) musicClip = downloaded;
            else battleMusicClip = downloaded;
            HasSoundtrack = true;
            webBridgeRefreshPending = true;
            Debug.Log($"[GameAudio] Context music {context} ready ({downloaded.length:0.###}s, {downloaded.channels}ch, {downloaded.frequency}Hz).");
            ApplySettings(); // Recheck context, settings and all media owners before assigning or playing.
        }

        private void CancelWebMusicRequest()
        {
            webRequestSerial++;
            UnityWebRequest request = webMusicRequest;
            Coroutine routine = webMusicRoutine;
            AudioClip pending = webPendingMusicClip;
            webMusicRequest = null;
            webMusicRoutine = null;
            webPendingMusicClip = null;
            if (routine != null) StopCoroutine(routine);
            if (request != null) { request.Abort(); request.Dispose(); }
            if (pending != null) Destroy(pending);
        }

        private void ReleaseWebMusicClip()
        {
            if (webOwnedMusicClip == null) return;
            if (musicSource != null)
            {
                musicSource.mute = true;
                PauseMusicImmediately();
                // The position is now in the context bookmark. Fully stop the native
                // channel before clearing its dynamic clip so the next clip cannot
                // inherit a paused channel's fallback sample rate or pitch state.
                musicSource.Stop();
                musicSource.clip = null;
            }
            musicHasStarted = false;
            if (musicClip == webOwnedMusicClip) musicClip = null;
            if (battleMusicClip == webOwnedMusicClip) battleMusicClip = null;
            Destroy(webOwnedMusicClip);
            webOwnedMusicClip = null;
            HasSoundtrack = nativeHomeMusicClip != null || battleMusicClip != null;
            webBridgeRefreshPending = true;
        }

        public void SetMusicEnabled(bool enabled)
        {
            if (model == null) return;
            if (model.Save.MusicEnabled != enabled) model.ToggleMusic();
            ApplySettings();
        }

        public void SetMusicDucked(bool ducked)
        {
            externalMusicDucked = ducked;
        }

        public void SetMediaMusicActive(string owner, bool active)
        {
            if (string.IsNullOrEmpty(owner)) return;
            bool changed = active ? mediaMusicOwners.Add(owner) : mediaMusicOwners.Remove(owner);
            if (changed) ApplySettings();
        }

        public void SetBrowserMediaMusicOwners(string owners)
        {
            // Replace only browser leases atomically; a native/video lease cannot be released by the page.
            mediaMusicOwners.RemoveWhere(owner => owner.StartsWith("browser:", StringComparison.Ordinal));
            foreach (string owner in (owners ?? string.Empty).Split(','))
            {
                if (owner.Length == 0 || owner.Length > 48) continue;
                bool valid = true;
                foreach (char c in owner)
                    if (!(c >= 'a' && c <= 'z') && !(c >= 'A' && c <= 'Z') &&
                        !(c >= '0' && c <= '9') && c != '-' && c != '_') { valid = false; break; }
                if (valid) mediaMusicOwners.Add("browser:" + owner);
            }
            ApplySettings();
        }

        private void Update()
        {
            if (musicSource == null) return;
#if UNITY_WEBGL && !UNITY_EDITOR
            if (webBridgeRefreshPending)
            {
                webBridgeRefreshPending = false;
                ChoSirenMusicBridge.Attach(this);
            }
#endif
            AudioClip wantedClip = DesiredMusicClip();
            if (CurrentMusicContext == MusicContext.Home && CanPlayMusic(wantedClip) &&
                musicSource.clip == wantedClip && loadedHomeMusicTrackIndex == HomeMusicTrackIndex)
            {
                if (musicSource.isPlaying)
                {
                    observedHomePlayback = true;
                    lastHomePlaybackPosition = musicSource.time;
                }
                else if (observedHomePlayback && musicHasStarted && musicFade != MusicFade.Out &&
                    lastHomePlaybackPosition >= wantedClip.length - Mathf.Max(.3f, Time.unscaledDeltaTime * 2f))
                {
                    // A completed song starts fresh the next time the playlist wraps to it.
                    // Manual switching and scene/CG pauses keep their independent bookmarks.
                    musicBookmarks[LoadedMusicBookmarkKey] = 0f;
                    observedHomePlayback = false;
                    AdvanceFinishedHomeSong(); // play mode: list loop / single loop / shuffle
                    wantedClip = DesiredMusicClip();
                }
            }
            if (!CanPlayMusic(wantedClip))
            {
                if (musicSource.isPlaying || musicFade != MusicFade.None) ApplySettings();
            }
            else if (musicFade == MusicFade.Out)
            {
                contextFadeGain = Mathf.MoveTowards(contextFadeGain, 0f, Time.unscaledDeltaTime / .25f);
                if (contextFadeGain <= 0f) SwitchMusicClip(wantedClip);
            }
            else if (musicFade == MusicFade.In)
            {
                contextFadeGain = Mathf.MoveTowards(contextFadeGain, 1f, Time.unscaledDeltaTime / .5f);
                if (contextFadeGain >= 1f) musicFade = MusicFade.None;
            }
            bool speaking = voiceSource != null && !voiceSource.mute && voiceSource.isPlaying;
            if (speaking) voiceDuckUntil = Time.unscaledTime + VoiceDuckHoldSeconds;
            float targetVoiceDuck = speaking || Time.unscaledTime < voiceDuckUntil ? VoiceDuckedMusicMultiplier : 1f;
            float voiceSeconds = targetVoiceDuck < voiceDuckGain ? .18f : VoiceDuckReleaseSeconds;
            voiceDuckGain = Mathf.MoveTowards(voiceDuckGain, targetVoiceDuck,
                Time.unscaledDeltaTime * (1f - VoiceDuckedMusicMultiplier) / voiceSeconds);
            float targetExternalDuck = externalMusicDucked ? DuckedMusicMultiplier : 1f;
            float externalSeconds = targetExternalDuck < externalDuckGain ? .15f : .35f;
            externalDuckGain = Mathf.MoveTowards(externalDuckGain, targetExternalDuck,
                Time.unscaledDeltaTime / externalSeconds);
            // Gain-only: speech and silent skill accents never pause, seek, replace, or restart
            // the music transport. Only a real media owner/context can take it over.
            duckGain = Mathf.Min(externalDuckGain, voiceDuckGain);
            musicSource.volume = ActiveMusicVolume * contextFadeGain * duckGain;
        }

        private void ResetMusicDucking()
        {
            externalMusicDucked = false;
            duckGain = 1f;
            externalDuckGain = voiceDuckGain = 1f;
            voiceDuckUntil = 0f;
            if (musicSource != null) musicSource.volume = ActiveMusicVolume * contextFadeGain;
        }

        private void OnApplicationPause(bool paused)
        {
            applicationPaused = paused;
            if (paused)
            {
                StopVinylScrub();
                voiceSource?.Stop();
                chorusSource?.Stop();
                ResetMusicDucking();
            }
            ApplySettings();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) StopVinylScrub();
            if (!focused) ResetMusicDucking();
#if UNITY_WEBGL && !UNITY_EDITOR
            webApplicationFocused = focused;
            ApplySettings();
#endif
        }

        private void OnEnable() => ApplySettings();

        private void OnDisable()
        {
            StopVinylScrub();
            CancelWebMusicRequest();
            voiceSource?.Stop();
            InterruptBattleChorus();
            ResetMusicDucking();
            if (musicSource != null)
            {
                musicSource.mute = true;
                PauseMusicImmediately();
            }
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
            PlayRecordedUi1102(clickClip);
        }

        private readonly Dictionary<string, AudioClip> sfxClips = new Dictionary<string, AudioClip>();
        private AudioSource battleSfxSource;
        private BattleSfxMixer1019 battleSfxMixer;

        /// <summary>
        /// Short battle/transition effect from Resources/Audio/Sfx/&lt;name&gt;. Repeats of the same
        /// sound inside 70 ms collapse into one. Six explicit voices also bound overlap on WebGL.
        /// </summary>
        public void PlaySfx(string name, float volume = 1f)
        {
            if (model == null || !model.Save.SfxEnabled || string.IsNullOrEmpty(name)) return;
            AudioClip clip = SfxTake(name);
            if (clip == null) return;
            if (battleSfxSource == null)
            {
                battleSfxSource = gameObject.AddComponent<AudioSource>();
                battleSfxSource.playOnAwake = false;
                battleSfxSource.loop = false;
                battleSfxSource.volume = BattleSfxOutputVolume * SfxScale;
                battleSfxMixer = gameObject.AddComponent<BattleSfxMixer1019>();
                battleSfxMixer.Initialize(battleSfxSource);
                battleSfxMixer.SetMuted(!model.Save.SfxEnabled);
            }
            battleSfxMixer.Play(name, clip, volume);
        }

        // 2026-10-23: battle sounds ship as takes — name.wav plus optional name-2.wav, name-3.wav — and a
        // different take plays each time (never the same one twice in a row), so repeated hits don't drone.
        private readonly Dictionary<string, AudioClip[]> sfxTakes = new Dictionary<string, AudioClip[]>();
        private readonly Dictionary<string, int> lastSfxTake = new Dictionary<string, int>();

        /// <summary>
        /// 2026-10-31: loads (and decodes) sounds ahead of time so the first ultimate judgement does not stall the
        /// frame while Resources.Load decodes the stinger + announcer on the spot (felt as a hitch after GOOD).
        /// </summary>
        public void Preload(string[] sfxNames, string[] voiceNames)
        {
            if (sfxNames != null)
                foreach (string name in sfxNames)
                {
                    SfxTake(name);
                    if (sfxTakes.TryGetValue(name, out AudioClip[] takes))
                        foreach (AudioClip take in takes) take?.LoadAudioData();
                }
            if (voiceNames != null)
                foreach (string name in voiceNames)
                    Resources.Load<AudioClip>("Audio/Voice/" + name)?.LoadAudioData();
        }

        private AudioClip SfxTake(string name)
        {
            if (!sfxTakes.TryGetValue(name, out AudioClip[] takes))
            {
                var found = new List<AudioClip>();
                AudioClip first = Resources.Load<AudioClip>("Audio/Sfx/" + name);
                if (first != null) found.Add(first);
                for (int i = 2; first != null && i <= 4; i++)
                {
                    AudioClip more = Resources.Load<AudioClip>("Audio/Sfx/" + name + "-" + i);
                    if (more == null) break;
                    found.Add(more);
                }
                sfxTakes[name] = takes = found.ToArray();
                if (first != null) sfxClips[name] = first;
            }
            if (takes.Length == 0) return null;
            if (takes.Length == 1) return takes[0];
            int last = lastSfxTake.TryGetValue(name, out int l) ? l : -1;
            int pick = UnityEngine.Random.Range(0, takes.Length - (last >= 0 ? 1 : 0));
            if (last >= 0 && pick >= last) pick++;
            lastSfxTake[name] = pick;
            return takes[pick];
        }

        public void PlaySuccess()
        {
            PlayRecordedUi1102(successClip, .9f, true);
        }

        /// <summary>
        /// Plays a recorded line from Resources/Audio/Voice/&lt;name&gt; (mp3/ogg/wav). Returns false
        /// when that clip does not exist yet, so callers can fall back. A new line cuts the old one.
        /// </summary>
        public bool PlayVoice(string name) => PlayVoice(name, null);

        /// <summary>An optional owner prevents a closing battle from stopping another page's voice.</summary>
        public bool PlayVoice(string name, string owner)
        {
            if (model == null || voiceSource == null || !model.Save.SfxEnabled) return false;
            if (IsStreamedVoice(name)) return PlayStreamedVoice(name, owner);
            AudioClip clip = Resources.Load<AudioClip>("Audio/Voice/" + name);
            if (clip != null) voiceRequest++; // a pending streamed line must not cut in afterwards
            if (clip == null) return false;
            ResumeAfterUserGesture();
            voiceSource.Stop();
            voiceSource.clip = clip;
            activeVoiceOwner = owner;
            TuneVoice1104(name);
            voiceSource.Play();
            return true;
        }

        public bool IsVoicePlayingFor(string owner) => !string.IsNullOrEmpty(owner) &&
            string.Equals(activeVoiceOwner, owner, StringComparison.Ordinal) && IsVoicePlaying;

        public void StopVoice(string owner)
        {
            if (string.IsNullOrEmpty(owner) || !string.Equals(activeVoiceOwner, owner, StringComparison.Ordinal)) return;
            voiceRequest++; // a line still downloading must not start after it was stopped
            voiceSource?.Stop();
            activeVoiceOwner = null;
        }

        // ------------------------------------------------------------------ streamed story voice
        // 2026-10-21: story lines live in StreamingAssets/Voice/Story (not in the startup data
        // file). Each line downloads when it is shown; the next line is prefetched.
        private const int StreamedVoiceCacheSize = 8;
        private readonly Dictionary<string, AudioClip> streamedVoices = new Dictionary<string, AudioClip>(StringComparer.Ordinal);
        private readonly List<string> streamedVoiceOrder = new List<string>();
        private readonly HashSet<string> streamedVoiceLoading = new HashSet<string>(StringComparer.Ordinal);
        private int voiceRequest;
        private bool loggedStreamedVoice;

        private static bool IsStreamedVoice(string name) => name != null && name.StartsWith("Story/", StringComparison.Ordinal);

        private bool PlayStreamedVoice(string name, string owner)
        {
            int token = ++voiceRequest;
            voiceSource.Stop();
            activeVoiceOwner = owner;
            TuneVoice1104(name);
            if (streamedVoices.TryGetValue(name, out AudioClip cached) && cached != null)
            {
                StartVoiceClip(cached);
                return true;
            }
            StartCoroutine(LoadStreamedVoice(name, owner, token));
            return true;
        }

        public void PrefetchVoice(string name)
        {
            if (!IsStreamedVoice(name) || streamedVoices.ContainsKey(name) || streamedVoiceLoading.Contains(name)) return;
            StartCoroutine(LoadStreamedVoice(name, null, -1));
        }

        private void StartVoiceClip(AudioClip clip)
        {
            ResumeAfterUserGesture();
            voiceSource.clip = clip;
            voiceSource.Play();
        }

        /// <summary>2026-11-04 创意工坊 声线: the speaking member's pitch and volume apply wherever her lines play.</summary>
        private void TuneVoice1104(string clipName)
        {
            if (voiceSource == null || model == null) return;
            model.VoiceTuning1104(UI.DialogueVoice1102.SubjectOf(clipName), out float pitch, out float volume);
            voiceSource.pitch = pitch;
            voiceSource.volume = SfxScale * volume;
        }

        private IEnumerator LoadStreamedVoice(string name, string owner, int token)
        {
            if (!streamedVoiceLoading.Add(name))
            {
                // Already downloading (prefetch): wait for it, then play if still wanted.
                float wait = Time.realtimeSinceStartup + 10f;
                while (streamedVoiceLoading.Contains(name) && Time.realtimeSinceStartup < wait) yield return null;
                if (token == voiceRequest && streamedVoices.TryGetValue(name, out AudioClip ready) && ready != null &&
                    model != null && model.Save.SfxEnabled) StartVoiceClip(ready);
                yield break;
            }
            string url = Application.streamingAssetsPath.TrimEnd('/') + "/Voice/" + name + ".mp3";
            if (!url.Contains("://")) url = new Uri(url).AbsoluteUri;
            AudioClip clip = null;
            using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.MPEG))
            {
                request.timeout = 10;
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success)
                {
                    try { clip = DownloadHandlerAudioClip.GetContent(request); } catch (Exception) { clip = null; }
                }
            }
            float decode = Time.realtimeSinceStartup + 5f;
            while (clip != null && clip.loadState == AudioDataLoadState.Loading && Time.realtimeSinceStartup < decode) yield return null;
            streamedVoiceLoading.Remove(name);
            if (clip == null || clip.loadState == AudioDataLoadState.Failed || clip.length <= 0f)
            {
                if (clip != null) Destroy(clip);
                yield break;
            }
            clip.name = name;
            if (!loggedStreamedVoice) { loggedStreamedVoice = true; Debug.Log($"[Voice] streamed {name} ({clip.length:0.0}s)"); }
            streamedVoices[name] = clip;
            streamedVoiceOrder.Remove(name);
            streamedVoiceOrder.Add(name);
            while (streamedVoiceOrder.Count > StreamedVoiceCacheSize)
            {
                string oldest = streamedVoiceOrder[0];
                streamedVoiceOrder.RemoveAt(0);
                if (streamedVoices.TryGetValue(oldest, out AudioClip old) && old != null && voiceSource.clip != old) Destroy(old);
                streamedVoices.Remove(oldest);
            }
            if (token == voiceRequest && model != null && model.Save.SfxEnabled && voiceSource != null) StartVoiceClip(clip);
        }

        public static bool ShouldPlayFallbackMusic(bool musicEnabled, bool lobbyVideoOwnsMusic) =>
            musicEnabled && !lobbyVideoOwnsMusic;

        private void OnDestroy()
        {
            webDestroying = true;
            CancelWebMusicRequest();
            ReleaseWebMusicClip();
            if (model != null) model.Changed -= ApplySettings;
            if (ownsMusicClip && nativeHomeMusicClip != null) Destroy(nativeHomeMusicClip);
            // UI clips are imported shared CC0 assets, not runtime-generated tones.
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
