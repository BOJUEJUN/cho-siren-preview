using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ChoSiren.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.Video;
using Object = UnityEngine.Object;

namespace ChoSiren.Tests
{
    /// <summary>Requires the actual imported Resources/Audio/bgm and a working audio device.</summary>
    public sealed class GlobalMusicTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly List<GameAudio> suspended = new List<GameAudio>();
        private GameObject host, titleCanvas;
        private GameAudio audio;
        private GameModel model;
        private InMemorySaveStore store;
        private AudioSource music, sfx, voice;
        private AudioClip testVoice;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // Keep any startup app's transport quiet without touching its model or saved preferences.
            foreach (GameAudio current in Object.FindObjectsByType<GameAudio>(FindObjectsSortMode.None))
                if (current.enabled) { suspended.Add(current); current.enabled = false; }
            store = new InMemorySaveStore();
            model = new GameModel(() => DateTime.Now, store);
            host = new GameObject("GlobalMusicFixture");
            if (Object.FindAnyObjectByType<AudioListener>() == null) host.AddComponent<AudioListener>();
            audio = host.AddComponent<GameAudio>();
            audio.Initialize(model);
            audio.SelectHomeMusic(HomeMusicPlaylist.NativeResourceTrackIndex);
            music = Field<AudioSource>(audio, "musicSource");
            sfx = Field<AudioSource>(audio, "sfxSource");
            voice = Field<AudioSource>(audio, "voiceSource");
            Assert.That(audio.HasSoundtrack, Is.True, "Import the selected original Resources/Audio/bgm before acceptance.");
            Assert.That(music.clip, Is.SameAs(Resources.Load<AudioClip>("Audio/bgm")));
            Assert.That(music.clip.length, Is.GreaterThan(5f), "Use the real soundtrack, not the short fixture tones.");
            yield return ProgressPast(0);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (titleCanvas != null) Object.Destroy(titleCanvas);
            if (host != null) Object.Destroy(host);
            if (testVoice != null) Object.Destroy(testVoice);
            yield return null;
            foreach (GameAudio current in suspended) if (current != null) current.enabled = true;
            suspended.Clear();
        }

        [UnityTest]
        public IEnumerator MusicSliderRestoresMasteredLevelWithoutRestartingTheSong()
        {
            model.Save.MusicVolume = 100;
            audio.ApplySettings();
            yield return new WaitForSecondsRealtime(.65f);
            Assert.That(music.volume, Is.EqualTo(1f).Within(.001f));
            int before = music.timeSamples;

            model.Save.MusicVolume = 50;
            audio.ApplySettings();
            yield return null;
            Assert.That(music.volume, Is.EqualTo(.5f).Within(.001f));
            yield return ProgressPast(before);

            model.Save.MusicVolume = 0;
            audio.ApplySettings();
            yield return null;
            Assert.That(music.volume, Is.Zero);
            before = music.timeSamples;
            model.Save.MusicVolume = 100;
            audio.ApplySettings();
            yield return null;
            Assert.That(music.volume, Is.EqualTo(1f).Within(.001f));
            yield return ProgressPast(before);
        }

        [UnityTest]
        public IEnumerator RepeatedRoutingAndMusicSwitchKeepOneSourceAndResumeTheActualPlayhead()
        {
            AudioSource originalSource = music;
            AudioClip originalClip = music.clip;
            int before = music.timeSamples;
            audio.Initialize(model);
            audio.SelectHomeMusic(HomeMusicPlaylist.NativeResourceTrackIndex); // Repeated initialization must not add another three sources.
            for (int i = 0; i < 5; i++)
            {
                audio.SetLobbyVideoMusicActive(false); // A silent page never acquires the music transport.
                audio.ApplySettings();
                audio.ResumeAfterUserGesture();
                yield return null;
            }
            Assert.That(music.isPlaying, Is.True);
            Assert.That(music.loop, Is.EqualTo(audio.CurrentMusicContext == GameAudio.MusicContext.Battle), "Home advances the playlist; only Battle loops one clip.");
            Assert.That(host.GetComponents<AudioSource>().Length, Is.EqualTo(3));
            Assert.That(music, Is.SameAs(originalSource));
            Assert.That(music.clip, Is.SameAs(originalClip));
            yield return ProgressPast(before);

            audio.SetMusicEnabled(false);
            int pausedAt = music.timeSamples;
            Assert.That(pausedAt, Is.GreaterThan(0));
            Assert.That(music.mute, Is.True);
            Assert.That(music.isPlaying, Is.False);
            Assert.That(sfx.mute, Is.False);
            audio.PlayClick(); // An SFX/user-gesture path must respect the muted music setting.
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(music.timeSamples, Is.EqualTo(pausedAt));
            Assert.That(new GameModel(() => DateTime.Now, store).Save.MusicEnabled, Is.False);

            audio.SetMusicEnabled(true);
            Assert.That(music.timeSamples, Is.GreaterThanOrEqualTo(pausedAt), "Resume must not call Play() at sample zero.");
            Assert.That(music, Is.SameAs(originalSource));
            Assert.That(music.clip, Is.SameAs(originalClip));
            yield return ProgressPast(pausedAt);
        }

        [UnityTest]
        public IEnumerator BrowserDuckChangesOnlyTheTrueSoundtrackGainAndBackgroundClearsIt()
        {
            float originalSfxVolume = sfx.volume;
            ChoSirenMusicBridge bridge = host.GetComponentInChildren<ChoSirenMusicBridge>();
            Assert.That(bridge.gameObject.name, Is.EqualTo("ChoSirenMusicBridge"));
            bridge.SetMusicEnabled("0");
            int pausedAt = music.timeSamples;
            bridge.SetMusicDucked("1");
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(music.volume, Is.EqualTo(GameAudio.MusicOutputVolume * GameAudio.DuckedMusicMultiplier).Within(.001f));
            Assert.That(music.timeSamples, Is.EqualTo(pausedAt));
            Assert.That(sfx.volume, Is.EqualTo(originalSfxVolume).Within(.001f));
            bridge.SetMusicDucked("false");
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(music.volume, Is.EqualTo(GameAudio.MusicOutputVolume).Within(.001f));
            Assert.That(music.timeSamples, Is.EqualTo(pausedAt));

            bridge.SetMusicEnabled("true");
            yield return ProgressPast(pausedAt);
            bridge.SetMusicDucked("1");
            yield return new WaitForSecondsRealtime(.2f);
            Call(audio, "OnApplicationPause", true);
            int backgroundAt = music.timeSamples;
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(music.timeSamples, Is.EqualTo(backgroundAt));
            Assert.That(music.volume, Is.EqualTo(GameAudio.MusicOutputVolume).Within(.001f));
            Call(audio, "OnApplicationPause", false);
            yield return ProgressPast(backgroundAt);
        }

        [UnityTest]
        public IEnumerator SfxSwitchMutesAnAlreadyPlayingVoiceWithoutMutingMusic()
        {
            // Only this short voice is synthetic; the music is the imported original verified in SetUp.
            testVoice = AudioClip.Create("GlobalMusicTestVoice", 48000, 1, 48000, false);
            voice.clip = testVoice;
            voice.Play();
            Assert.That(voice.isPlaying, Is.True);
            model.ToggleSfx();
            Assert.That(sfx.mute, Is.True);
            Assert.That(voice.mute, Is.True);
            Assert.That(music.mute, Is.False);
            Assert.That(music.isPlaying, Is.True);
            int before = music.timeSamples;
            yield return ProgressPast(before);
            model.ToggleSfx();
            Assert.That(sfx.mute, Is.False);
            Assert.That(voice.mute, Is.False);
            Assert.That(music.mute, Is.False);
        }

        [UnityTest]
        public IEnumerator NativeTitleUsesOwnMusicAndSavedSwitchThenResumesGlobalPlayhead()
        {
            int before = music.timeSamples;
            titleCanvas = new GameObject("GlobalMusicTitleFixture", typeof(RectTransform), typeof(Canvas));
            var safe = new GameObject("Safe", typeof(RectTransform));
            safe.transform.SetParent(titleCanvas.transform, false);
            NativeTitleScreen title = NativeTitleScreen.Create(titleCanvas.transform,
                safe.GetComponent<RectTransform>(), true, audio);
            title.StopAllCoroutines(); // No release-notice HTTP request is part of audio acceptance.
            VideoPlayer cg = title.GetComponent<VideoPlayer>();
            Assert.That(cg, Is.Not.Null);
            Assert.That(cg.audioOutputMode, Is.EqualTo(VideoAudioOutputMode.Direct));
            Assert.That(cg.controlledAudioTrackCount, Is.EqualTo(1));
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!cg.isPrepared && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(cg.isPrepared, Is.True, "Decode the actual loading movie before accepting native audio controls.");
            Assert.That(cg.audioTrackCount, Is.GreaterThan(0));
            Assert.That(cg.GetDirectAudioVolume(0), Is.EqualTo(.45f).Within(.001f));
            Assert.That(audio.MediaOwnsMusic, Is.True);
            Assert.That(music.isPlaying, Is.False);
            int pausedAt = music.timeSamples;
            Assert.That(pausedAt, Is.GreaterThanOrEqualTo(before));
            Button button = title.GetComponentsInChildren<Button>(true).Single(item => item.name == "SoundButton");
            button.onClick.Invoke();
            Assert.That(model.Save.MusicEnabled, Is.False);
            Assert.That(music.mute, Is.True);
            Assert.That(Field<bool>(title, "soundEnabled"), Is.False);
            Assert.That(cg.GetDirectAudioMute(0), Is.True);
            Assert.That(sfx.mute, Is.False);
            audio.SetMusicEnabled(true);
            Assert.That(Field<bool>(title, "soundEnabled"), Is.True);
            Assert.That(cg.GetDirectAudioMute(0), Is.False);
            Assert.That(music.isPlaying, Is.False, "An enabled preference must not override the active CG.");
            Object.Destroy(title.gameObject);
            yield return null;
            Assert.That(audio.MediaOwnsMusic, Is.False);
            yield return ProgressPast(pausedAt);
        }

        [UnityTest]
        public IEnumerator OverlappingMediaOwnersAndBrowserRebindingNeverResumeEarly()
        {
            audio.SetMediaMusicActive("native-cutscene", true);
            int pausedAt = music.timeSamples;
            audio.SetBrowserMediaMusicOwners("loading-cg,story-cg");
            audio.SetMediaMusicActive("native-cutscene", false);
            audio.SetBrowserMediaMusicOwners("story-cg");
            audio.ResumeAfterUserGesture();
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(music.isPlaying, Is.False);
            Assert.That(music.timeSamples, Is.EqualTo(pausedAt));
            audio.SetMediaMusicActive("native-cutscene", true);
            audio.SetBrowserMediaMusicOwners(string.Empty);
            Assert.That(music.isPlaying, Is.False, "Browser dismissal cannot release a native cutscene's lease.");
            audio.SetMediaMusicActive("native-cutscene", false);
            yield return ProgressPast(pausedAt);
            audio.SetBrowserMediaMusicOwners("bad/id,, ");
            Assert.That(audio.MediaOwnsMusic, Is.False);
            audio.SetLobbyVideoMusicActive(true);
            Assert.That(music.isPlaying, Is.False, "An audible lobby video has the same priority as a cutscene.");
            audio.SetLobbyVideoMusicActive(false);
            yield return ProgressPast(music.timeSamples);
        }

        [UnityTest]
        public IEnumerator MediaReleasePreservesDisabledMusicAndNativeFailureReleasesImmediately()
        {
            audio.SetMusicEnabled(false);
            audio.SetMediaMusicActive("story-cg", true);
            audio.SetMediaMusicActive("story-cg", false);
            Assert.That(music.isPlaying, Is.False);
            Assert.That(model.Save.MusicEnabled, Is.False);
            audio.SetMusicEnabled(true);
            titleCanvas = new GameObject("CgFailureFixture", typeof(RectTransform), typeof(Canvas));
            var safe = new GameObject("Safe", typeof(RectTransform));
            safe.transform.SetParent(titleCanvas.transform, false);
            NativeTitleScreen title = NativeTitleScreen.Create(titleCanvas.transform, safe.GetComponent<RectTransform>(), true, audio);
            title.StopAllCoroutines();
            int pausedAt = music.timeSamples;
            LogAssert.Expect(LogType.Warning, "Loading CG: fixture decode failure");
            Call(title, "FailVideo", "fixture decode failure");
            Assert.That(title.GetComponent<VideoPlayer>().GetDirectAudioMute(0), Is.True);
            Assert.That(audio.MediaOwnsMusic, Is.False);
            yield return ProgressPast(pausedAt);
            title.gameObject.SetActive(false);
            title.gameObject.SetActive(true);
            Assert.That(audio.MediaOwnsMusic, Is.False, "A failed decoder cannot reacquire music on enable.");
        }

        [UnityTest]
        public IEnumerator DestroyingTransportLeavesSharedSoundtrackUsableByTheNextApp()
        {
            AudioClip shared = music.clip;
            Object.Destroy(host);
            yield return null;
            Assert.That(shared != null, Is.True, "Resources audio must not be destroyed as if it were a synthesized clip.");
            Assert.That(Resources.Load<AudioClip>("Audio/bgm"), Is.SameAs(shared));
            host = new GameObject("GlobalMusicFixtureNextApp");
            if (Object.FindAnyObjectByType<AudioListener>() == null) host.AddComponent<AudioListener>();
            audio = host.AddComponent<GameAudio>();
            audio.Initialize(model);
            audio.SelectHomeMusic(HomeMusicPlaylist.NativeResourceTrackIndex);
            music = Field<AudioSource>(audio, "musicSource");
            Assert.That(music.clip, Is.SameAs(shared));
            yield return ProgressPast(0);
        }

        private IEnumerator ProgressPast(int previous)
        {
            float deadline = Time.realtimeSinceStartup + 3f;
            while (music.timeSamples <= previous && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(music.isPlaying, Is.True);
            Assert.That(music.timeSamples, Is.GreaterThan(previous), "The real audio transport must advance; a silent/null audio backend is not runtime acceptance.");
        }

        private static T Field<T>(object target, string name) =>
            (T)target.GetType().GetField(name, Private).GetValue(target);

        private static void Call(object target, string name, params object[] args) =>
            target.GetType().GetMethod(name, Private).Invoke(target, args);
    }
}
