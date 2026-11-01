using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using Context = ChoSiren.GameAudio.MusicContext;

namespace ChoSiren.Tests
{
    /// <summary>Uses both imported soundtracks and requires an advancing native audio backend.</summary>
    public sealed class ContextMusicTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const float HomeGain = GameAudio.MusicOutputVolume;
        private const float BattleGain = GameAudio.BattleMusicOutputVolume;
        private readonly List<GameAudio> suspended = new List<GameAudio>();
        private readonly List<GameObject> scopeHosts = new List<GameObject>();
        private GameObject host;
        private GameAudio audio;
        private GameModel model;
        private InMemorySaveStore store;
        private AudioSource music, sfx, voice;
        private AudioClip homeClip, battleClip;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // Isolate routing and preferences from the app loaded by the test scene.
            foreach (GameAudio current in Object.FindObjectsByType<GameAudio>(FindObjectsSortMode.None))
                if (current.enabled) { suspended.Add(current); current.enabled = false; }
            store = new InMemorySaveStore();
            model = new GameModel(() => DateTime.Now, store);
            host = new GameObject("ContextMusicFixture");
            if (Object.FindAnyObjectByType<AudioListener>() == null) host.AddComponent<AudioListener>();
            audio = host.AddComponent<GameAudio>();
            audio.Initialize(model);
            audio.SelectHomeMusic(HomeMusicPlaylist.NativeResourceTrackIndex);
            music = Field<AudioSource>(audio, "musicSource");
            sfx = Field<AudioSource>(audio, "sfxSource");
            voice = Field<AudioSource>(audio, "voiceSource");
            homeClip = Resources.Load<AudioClip>("Audio/bgm");
            battleClip = Resources.Load<AudioClip>("Audio/bgm-battle");
            Assert.That(homeClip, Is.Not.Null, "Import the selected real Home soundtrack.");
            Assert.That(battleClip, Is.Not.Null, "Import the selected real Battle soundtrack.");
            Assert.That(homeClip.length, Is.GreaterThan(30f), "A synthetic fixture loop is not music acceptance.");
            Assert.That(battleClip.length, Is.GreaterThan(30f), "A synthetic fixture loop is not music acceptance.");
            Assert.That(battleClip, Is.Not.SameAs(homeClip));
            Assert.That(audio.HasSoundtrack, Is.True);
            yield return Audible(Context.Home, homeClip, HomeGain);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameObject node in scopeHosts) if (node != null) Object.Destroy(node);
            scopeHosts.Clear();
            if (host != null) Object.Destroy(host);
            yield return null;
            foreach (GameAudio current in suspended) if (current != null) current.enabled = true;
            suspended.Clear();
        }

        [UnityTest]
        public IEnumerator HomeSilenceResumesTheBookmarkAndRepeatedHomeNeverRestartsOrAddsSources()
        {
            AudioSource original = music;
            yield return AdvanceFor(.35f);
            int before = music.timeSamples;
            for (int i = 0; i < 4; i++)
            {
                audio.Initialize(model);
            audio.SelectHomeMusic(HomeMusicPlaylist.NativeResourceTrackIndex);
                audio.SetBaseMusicContext(Context.Home);
                audio.SetContextMusicActive("home-panel", Context.Home, true);
                audio.ApplySettings();
                audio.ResumeAfterUserGesture();
                yield return null;
                Assert.That(music.timeSamples, Is.GreaterThanOrEqualTo(before), "Repeated Home routing restarted the clip.");
            }
            AssertTransport(original);
            yield return ProgressPast(before);

            audio.SetContextMusicActive("story-panel", Context.Silent, true);
            yield return Silent();
            int pausedAt = music.timeSamples;
            Assert.That(pausedAt, Is.GreaterThan(0));
            audio.PlayClick();
            audio.ApplySettings();
            audio.ResumeAfterUserGesture();
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(music.isPlaying, Is.False, "An SFX or user gesture cannot bypass a silent page.");
            Assert.That(music.timeSamples, Is.EqualTo(pausedAt));
            Assert.That(sfx.mute, Is.False);

            audio.SetContextMusicActive("story-panel", Context.Silent, false);
            Assert.That(music.clip, Is.SameAs(homeClip));
            Assert.That(music.timeSamples, Is.GreaterThanOrEqualTo(pausedAt), "Home restarted instead of unpausing.");
            yield return Audible(Context.Home, homeClip, HomeGain);
            yield return ProgressPast(pausedAt);
            AssertTransport(original);
        }

        [UnityTest]
        public IEnumerator NewestRouteWinsAndReleasingOldStoryCannotInterruptBattleOrMap()
        {
            audio.SetContextMusicActive("home-panel", Context.Home, true);
            yield return AdvanceFor(.35f);
            audio.SetContextMusicActive("old-story", Context.Silent, true);
            yield return Silent();
            int homeBookmark = music.timeSamples;

            audio.SetContextMusicActive("battle-panel", Context.Battle, true);
            yield return Audible(Context.Battle, battleClip, BattleGain);
            int battleBeforeRelease = music.timeSamples;
            audio.SetContextMusicActive("old-story", Context.Silent, false);
            // A repeated active call from an older Home panel must not acquire a newer sequence.
            audio.SetContextMusicActive("home-panel", Context.Home, true);
            audio.ResumeAfterUserGesture();
            Assert.That(audio.CurrentMusicContext, Is.EqualTo(Context.Battle));
            Assert.That(music.clip, Is.SameAs(battleClip));
            Assert.That(music.timeSamples, Is.GreaterThanOrEqualTo(battleBeforeRelease));
            yield return ProgressPast(battleBeforeRelease);

            audio.SetContextMusicActive("map-panel", Context.Silent, true);
            yield return Silent();
            int mapPausedAt = music.timeSamples;
            audio.SetContextMusicActive("old-story", Context.Silent, false); // Duplicate stale close.
            audio.SetContextMusicActive("battle-panel", Context.Battle, false);
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(audio.CurrentMusicContext, Is.EqualTo(Context.Silent));
            Assert.That(music.isPlaying, Is.False, "Closing an older route cannot bypass the open Map.");
            Assert.That(music.timeSamples, Is.EqualTo(mapPausedAt));

            audio.SetContextMusicActive("map-panel", Context.Silent, false);
            yield return ClipSelected(homeClip);
            Assert.That(music.timeSamples, Is.GreaterThanOrEqualTo(homeBookmark - homeClip.frequency / 100),
                "Switching back from Battle lost the Home bookmark (allow 10 ms for seconds/sample conversion).");
            Assert.That(music.timeSamples, Is.LessThanOrEqualTo(homeBookmark + homeClip.frequency / 5),
                "An inactive Home soundtrack advanced while another context owned the transport.");
            yield return Audible(Context.Home, homeClip, HomeGain);

            // Reopening Battle after an actual clip swap must restore its own bookmark as well.
            audio.SetContextMusicActive("battle-panel", Context.Battle, true);
            yield return ClipSelected(battleClip);
            Assert.That(music.timeSamples, Is.GreaterThanOrEqualTo(mapPausedAt - battleClip.frequency / 100),
                "Battle restarted after the transport had returned to Home.");
            Assert.That(music.timeSamples, Is.LessThanOrEqualTo(mapPausedAt + battleClip.frequency / 5),
                "An inactive Battle soundtrack advanced while Home owned the transport.");
            yield return Audible(Context.Battle, battleClip, BattleGain);
        }

        [UnityTest]
        public IEnumerator OverlappingCutscenesPauseBattleAndReleaseBackToItsExactTransport()
        {
            audio.SetBaseMusicContext(Context.Battle);
            yield return Audible(Context.Battle, battleClip, BattleGain);
            yield return AdvanceFor(.35f);
            audio.SetMediaMusicActive("battle-cg", true);
            int battleBookmark = music.timeSamples;
            Assert.That(audio.CurrentMusicContext, Is.EqualTo(Context.Battle), "CG ownership must preserve the underlying route.");
            Assert.That(audio.MediaOwnsMusic, Is.True);
            Assert.That(music.isPlaying, Is.False);

            audio.SetMediaMusicActive("second-cg", true);
            audio.SetMediaMusicActive("battle-cg", false);
            audio.SetContextMusicActive("brief-map", Context.Silent, true);
            audio.SetContextMusicActive("brief-map", Context.Silent, false);
            audio.ApplySettings();
            audio.ResumeAfterUserGesture();
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(audio.CurrentMusicContext, Is.EqualTo(Context.Battle));
            Assert.That(music.isPlaying, Is.False, "A remaining CG lease has priority over routes and gestures.");
            Assert.That(music.clip, Is.SameAs(battleClip));
            Assert.That(music.timeSamples, Is.EqualTo(battleBookmark));

            audio.SetMediaMusicActive("second-cg", false);
            Assert.That(audio.MediaOwnsMusic, Is.False);
            Assert.That(music.timeSamples, Is.GreaterThanOrEqualTo(battleBookmark), "The Battle clip restarted after CG.");
            yield return Audible(Context.Battle, battleClip, BattleGain);
            yield return ProgressPast(battleBookmark);
            AssertTransport(music);
        }

        [UnityTest]
        public IEnumerator DisablingMusicDuringAFadeCannotLeakPlaybackOrChangeIndependentSfx()
        {
            audio.SetBaseMusicContext(Context.Battle);
            yield return null; // Disable during the transition, before its fade-out/fade-in can finish.
            audio.SetMusicEnabled(false);
            audio.PlaySuccess();
            yield return null;
            Assert.That(sfx.isPlaying, Is.True, "SFX should still play while music is disabled.");
            Assert.That(sfx.mute, Is.False);
            Assert.That(voice.mute, Is.False);
            Assert.That(music.mute, Is.True);
            Assert.That(music.isPlaying, Is.False);

            model.ToggleSfx();
            Assert.That(sfx.mute, Is.True);
            Assert.That(voice.mute, Is.True);
            float deadline = Time.realtimeSinceStartup + .85f;
            while (Time.realtimeSinceStartup < deadline)
            {
                audio.ResumeAfterUserGesture();
                Assert.That(music.mute, Is.True);
                Assert.That(music.isPlaying, Is.False, "A pending fade resumed disabled music.");
                yield return null;
            }
            Assert.That(new GameModel(() => DateTime.Now, store).Save.MusicEnabled, Is.False);
            model.ToggleSfx();
            Assert.That(sfx.mute, Is.False);
            Assert.That(voice.mute, Is.False);
            Assert.That(music.mute, Is.True, "Enabling SFX must not enable music.");

            audio.SetMusicEnabled(true);
            yield return Audible(Context.Battle, battleClip, BattleGain);
            audio.SetContextMusicActive("silent-map", Context.Silent, true);
            yield return Silent();
            int pausedAt = music.timeSamples;
            audio.PlaySuccess();
            yield return null;
            Assert.That(sfx.isPlaying, Is.True);
            Assert.That(music.isPlaying, Is.False, "The SFX gesture path bypassed the silent context.");
            Assert.That(music.timeSamples, Is.EqualTo(pausedAt));
            AssertTransport(music);
        }

        [UnityTest]
        public IEnumerator RealBattleSpeechDucksGentlyWithoutInterruptingTheMusicPlayhead()
        {
            audio.SetBaseMusicContext(Context.Battle);
            yield return Audible(Context.Battle, battleClip, BattleGain);
            AudioSource original = music;
            AudioClip originalClip = music.clip;
            int before = music.timeSamples;
            const string owner = "continuous-battle-music-test";
            Assert.That(audio.PlayVoice("Battle/xingli-active-01", owner), Is.True);
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(voice.isPlaying, Is.True, "Use a real approved voice recording.");
            Assert.That(audio.MediaOwnsMusic, Is.False, "A callout is not a CG media lease.");
            Assert.That(music.isPlaying, Is.True);
            Assert.That(music.mute, Is.False);
            Assert.That(music.clip, Is.SameAs(originalClip));
            Assert.That(music.timeSamples, Is.GreaterThan(before), "Speech must not pause or restart the beat.");
            Assert.That(music.volume, Is.EqualTo(BattleGain * .6f).Within(.001f),
                "Short combat speech should lower music by about 4.4 dB, not make it nearly vanish.");

            audio.StopVoice(owner);
            yield return new WaitForSecondsRealtime(.08f);
            Assert.That(music.volume, Is.EqualTo(BattleGain * .6f).Within(.001f),
                "Hold through a short speech gap instead of pumping the mix up and down.");
            Assert.That(audio.PlayVoice("Battle/xingli-active-02", owner), Is.True);
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(music.volume, Is.EqualTo(BattleGain * .6f).Within(.001f));
            audio.StopVoice(owner);
            yield return new WaitForSecondsRealtime(1f);
            yield return Audible(Context.Battle, originalClip, BattleGain);
            yield return ProgressPast(before);
            AssertTransport(original);
        }

        [UnityTest]
        public IEnumerator SilentSkillAccentsAndUnavailableUltimateVideoNeverTakeTheMusicTransport()
        {
            audio.SetBaseMusicContext(Context.Battle);
            yield return Audible(Context.Battle, battleClip, BattleGain);
            bool hadReduce = PlayerPrefs.HasKey(BattlePresentationOptions.ReduceMotionKey);
            int oldReduce = PlayerPrefs.GetInt(BattlePresentationOptions.ReduceMotionKey);
            PlayerPrefs.SetInt(BattlePresentationOptions.ReduceMotionKey, 0);
            var stage = new GameObject("SilentBattleAccentFixture", typeof(RectTransform));
            stage.transform.SetParent(host.transform, false);
            scopeHosts.Add(stage);
            try
            {
                var rect = (RectTransform)stage.transform;
                rect.sizeDelta = new Vector2(720, 1502);
                Sprite art = Resources.Load<Sprite>("Art/Members/member-xingli");
                Assert.That(art, Is.Not.Null);
                var active = stage.AddComponent<BattleActiveCloseup1019>();
                active.Configure(rect, () => false, () => 1);
                Assert.That(active.Play(art, "星璃", "短招演出", Color.magenta), Is.True);
                var ultimate = stage.AddComponent<BattleAlbumUltimate929>();
                ultimate.Configure(rect, null, () => false);
                ultimate.Enqueue(art, "星璃", "大招演出", Color.magenta);
                var optional = stage.AddComponent<BattleUltimateVideo1019>();
                optional.Configure(rect, () => false);
                Assert.That(stage.GetComponent<UnityEngine.Video.VideoPlayer>().audioOutputMode,
                    Is.EqualTo(UnityEngine.Video.VideoAudioOutputMode.None));
                Assert.That(optional.TryPlay("not-an-approved-video-id", null), Is.False);
                int before = music.timeSamples;
                yield return new WaitForSecondsRealtime(.25f);
                Assert.That(audio.CurrentMusicContext, Is.EqualTo(Context.Battle));
                Assert.That(audio.MediaOwnsMusic, Is.False);
                Assert.That(music.mute, Is.False);
                Assert.That(music.volume, Is.EqualTo(BattleGain).Within(.001f));
                yield return ProgressPast(before);
                AssertTransport(music);
            }
            finally
            {
                if (hadReduce) PlayerPrefs.SetInt(BattlePresentationOptions.ReduceMotionKey, oldReduce);
                else PlayerPrefs.DeleteKey(BattlePresentationOptions.ReduceMotionKey);
            }
        }

        [UnityTest]
        public IEnumerator ScopeEarlyCloseDisableReenableAndDestroyLeaveNoRouteOwnersBehind()
        {
            GameObject earlyHost = ScopeHost("EarlyClose");
            MusicContextScope early = MusicContextScope.Attach(earlyHost, Context.Silent);
            Assert.That(MusicContextScope.Attach(earlyHost, Context.Silent), Is.SameAs(early));
            Assert.That(earlyHost.GetComponents<MusicContextScope>().Length, Is.EqualTo(1));
            early.Release();
            early.Release(); // Closing before the first frame and duplicate close are both safe.
            Object.Destroy(earlyHost);
            yield return null;
            yield return Audible(Context.Home, homeClip, HomeGain);

            GameObject storyHost = ScopeHost("StoryScope");
            MusicContextScope.Attach(storyHost, Context.Silent);
            yield return Silent();
            storyHost.SetActive(false);
            yield return Audible(Context.Home, homeClip, HomeGain);
            storyHost.SetActive(true);
            yield return Silent();

            GameObject battleHost = ScopeHost("BattleScope");
            MusicContextScope.Attach(battleHost, Context.Battle);
            yield return Audible(Context.Battle, battleClip, BattleGain);
            int battleBeforeOldDisable = music.timeSamples;
            storyHost.SetActive(false);
            Assert.That(audio.CurrentMusicContext, Is.EqualTo(Context.Battle));
            yield return ProgressPast(battleBeforeOldDisable);
            Object.Destroy(battleHost); // Both OnDisable and OnDestroy run for the active scope.
            yield return null;
            yield return Audible(Context.Home, homeClip, HomeGain);
            Object.Destroy(storyHost); // Destroying an already-disabled older scope is also harmless.
            yield return null;
            yield return Audible(Context.Home, homeClip, HomeGain);
            AssertTransport(music);
        }

        private IEnumerator Audible(Context context, AudioClip clip, float gain)
        {
            float deadline = Time.realtimeSinceStartup + 3f;
            while ((audio.CurrentMusicContext != context || music.clip != clip || !music.isPlaying ||
                    Mathf.Abs(music.volume - gain) > .001f) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(audio.CurrentMusicContext, Is.EqualTo(context));
            Assert.That(music.clip, Is.SameAs(clip));
            Assert.That(music.isPlaying, Is.True);
            Assert.That(music.mute, Is.False);
            Assert.That(music.volume, Is.EqualTo(gain).Within(.001f));
            yield return ProgressPast(music.timeSamples);
        }

        private IEnumerator Silent()
        {
            float deadline = Time.realtimeSinceStartup + 2f;
            while ((audio.CurrentMusicContext != Context.Silent || music.isPlaying || music.volume > .001f) &&
                   Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(audio.CurrentMusicContext, Is.EqualTo(Context.Silent));
            Assert.That(music.isPlaying, Is.False);
            Assert.That(music.volume, Is.EqualTo(0f).Within(.001f));
        }

        private IEnumerator ClipSelected(AudioClip clip)
        {
            float deadline = Time.realtimeSinceStartup + 2f;
            while (music.clip != clip && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(music.clip, Is.SameAs(clip));
        }

        private IEnumerator AdvanceFor(float seconds)
        {
            int before = music.timeSamples;
            yield return new WaitForSecondsRealtime(seconds);
            yield return ProgressPast(before);
        }

        private IEnumerator ProgressPast(int before)
        {
            float deadline = Time.realtimeSinceStartup + 3f;
            while (music.timeSamples <= before && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(music.isPlaying, Is.True);
            Assert.That(music.timeSamples, Is.GreaterThan(before),
                "The imported soundtrack's real playhead must advance; null audio is not runtime acceptance.");
        }

        private GameObject ScopeHost(string name)
        {
            var node = new GameObject("ContextMusic" + name);
            node.transform.SetParent(host.transform, false);
            scopeHosts.Add(node);
            return node;
        }

        private void AssertTransport(AudioSource original)
        {
            Assert.That(Field<AudioSource>(audio, "musicSource"), Is.SameAs(original));
            Assert.That(host.GetComponents<AudioSource>().Length, Is.EqualTo(3));
            Assert.That(music.loop, Is.EqualTo(music.clip == battleClip), "The loaded Battle clip loops; a muted or fading route may still hold that paused clip.");
        }

        private static T Field<T>(object target, string name) =>
            (T)target.GetType().GetField(name, Private).GetValue(target);
    }
}
