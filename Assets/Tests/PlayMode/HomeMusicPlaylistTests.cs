using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ChoSiren.Tests
{
    /// <summary>Real approved MP3s, one live audio transport, and independent muted preferences.</summary>
    public sealed class HomeMusicPlaylistTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly List<GameAudio> suspended = new List<GameAudio>();
        private GameObject host, captionHost;
        private GameAudio audio;
        private GameModel model;
        private InMemorySaveStore store;
        private AudioSource music, sfx;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            foreach (GameAudio current in Object.FindObjectsByType<GameAudio>(FindObjectsSortMode.None))
                if (current.enabled) { suspended.Add(current); current.enabled = false; }
            store = new InMemorySaveStore();
            model = new GameModel(() => DateTime.Now, store);
            model.ToggleMusic(); // The playlist starts muted and must not force a user preference on.
            host = new GameObject("HomeMusicPlaylistFixture");
            if (Object.FindAnyObjectByType<AudioListener>() == null) host.AddComponent<AudioListener>();
            audio = host.AddComponent<GameAudio>();
            audio.Initialize(model);
            music = Field<AudioSource>("musicSource");
            sfx = Field<AudioSource>("sfxSource");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (captionHost != null) Object.Destroy(captionHost);
            if (host != null) Object.Destroy(host);
            yield return null;
            foreach (GameAudio current in suspended) if (current != null) current.enabled = true;
            suspended.Clear();
        }

        [Test]
        public void DataDrivenPlaylistKeepsTheApprovedSongsFirstAndOnlyRealFiles()
        {
            // 2026-11-01: Data/music-playlist.json drives the list. The two approved Home songs keep their ids,
            // order and native clip; every other entry must be a real immutable StreamingAssets/Music file.
            Assert.That(HomeMusicPlaylist.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(HomeMusicPlaylist.NativeResourceTrackIndex, Is.EqualTo(0));
            Assert.That(HomeMusicPlaylist.Get(0).Id, Is.EqualTo("my-light-my-rules"));
            Assert.That(HomeMusicPlaylist.Get(0).Title, Is.EqualTo("光的规则"));
            Assert.That(HomeMusicPlaylist.Get(0).FileName, Is.EqualTo("home-f19d89ea8124a31b.mp3"));
            Assert.That(HomeMusicPlaylist.Get(1).Id, Is.EqualTo("chase-the-glow-a"));
            Assert.That(HomeMusicPlaylist.Get(1).FileName, Is.EqualTo("home-chase-5d360e8e7acbc7c6.mp3"));
            for (int i = 0; i < HomeMusicPlaylist.Count; i++)
            {
                HomeMusicPlaylist.Track track = HomeMusicPlaylist.Get(i);
                Assert.That(File.Exists(Path.Combine(Application.streamingAssetsPath, "Music", track.FileName)), Is.True, track.FileName);
                Assert.That(track.FileName, Does.Not.Contain("neon"), "Neon Moonwalk was removed by the user (R69).");
                Assert.That(Resources.Load<Texture2D>(track.Cover), Is.Not.Null, track.Cover);
                Assert.That(track.Duration, Is.GreaterThan(60f));
                Assert.That(HomeMusicPlaylist.IndexOf(track.Id), Is.EqualTo(i));
                Assert.That(HomeMusicPlaylist.NextIndex(i), Is.EqualTo((i + 1) % HomeMusicPlaylist.Count));
            }
            Assert.Throws<ArgumentOutOfRangeException>(() => HomeMusicPlaylist.Get(HomeMusicPlaylist.Count));
            Assert.Throws<ArgumentOutOfRangeException>(() => HomeMusicPlaylist.NextIndex(HomeMusicPlaylist.Count));
        }

        [UnityTest]
        public IEnumerator SwitchingWhileMutedDoesNotDownloadOrEnableMusicAndKeepsOriginalSfx()
        {
            AudioSource original = music;
            bool sfxEnabled = model.Save.SfxEnabled;
            Assert.That(audio.HomeMusicTrackIndex, Is.EqualTo(0));
            Assert.That(audio.HomeMusicTitle, Is.EqualTo("光的规则"));
            Assert.That(audio.HasSoundtrack, Is.True);
            AssertApprovedNativeSong(0);
            for (int i = 0; i < HomeMusicPlaylist.Count; i++)
            {
                audio.PlayClick();
                Assert.That(audio.NextHomeMusic(), Is.True);
                yield return null;
                Assert.That(audio.MusicEnabled, Is.False);
                Assert.That(music.isPlaying, Is.False);
                Assert.That(Field<UnityEngine.Networking.UnityWebRequest>("webMusicRequest"), Is.Null,
                    "Muted selection must not download optional music.");
            }
            Assert.That(audio.HomeMusicTrackIndex, Is.EqualTo(0));
            Assert.That(new GameModel(() => DateTime.Now, store).Save.MusicEnabled, Is.False);
            Assert.That(model.Save.SfxEnabled, Is.EqualTo(sfxEnabled));
            Assert.That(sfx.mute, Is.EqualTo(!sfxEnabled));
            Assert.That(music, Is.SameAs(original));
            Assert.That(host.GetComponents<AudioSource>().Length, Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator TwoRealSongsFinishInOrderAndWrapWithoutAddingSources()
        {
            AudioSource original = music;
            audio.SetMusicEnabled(true);
            // 2026-11-01: the list holds only the two songs the user supplied; they finish, advance and wrap.
            for (int index = 0; index < 2; index++)
            {
                yield return Audible(index);
                AssertApprovedNativeSong(index);
                Assert.That(music.loop, Is.False, "Home must advance at the end rather than loop one song.");
                music.time = music.clip.length - .16f;
                float deadline = Time.realtimeSinceStartup + 3f;
                int next = HomeMusicPlaylist.NextIndex(index);
                while (audio.HomeMusicTrackIndex != next && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(audio.HomeMusicTrackIndex, Is.EqualTo(next), "The finished song did not advance.");
            }
            yield return Audible(0);
            Assert.That(music.time, Is.LessThan(1.5f), "A completed song must restart when the playlist wraps.");
            Assert.That(music, Is.SameAs(original));
            Assert.That(host.GetComponents<AudioSource>().Length, Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator ManualSwitchesKeepEachSongBookmarkAndCgKeepsTheSelectedSongSilent()
        {
            audio.SetMusicEnabled(true);
            yield return Audible(0);
            music.time = 8f;
            yield return new WaitForSecondsRealtime(.12f);
            float jazzPosition = music.time;
            audio.NextHomeMusic();
            yield return Audible(1);
            music.time = 12f;
            yield return new WaitForSecondsRealtime(.12f);
            float chasePosition = music.time;
            audio.PreviousHomeMusic();
            yield return Audible(0);
            Assert.That(music.time, Is.InRange(jazzPosition - .15f, jazzPosition + 1.5f));

            audio.SetMediaMusicActive("test-music-cg", true);
            Assert.That(music.isPlaying, Is.False);
            audio.NextHomeMusic(); // list loop: 0 -> 1
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(audio.HomeMusicTrackIndex, Is.EqualTo(1));
            Assert.That(music.isPlaying, Is.False, "Selecting music must not play through a CG lease.");
            Assert.That(Field<UnityEngine.Networking.UnityWebRequest>("webMusicRequest"), Is.Null,
                "Selecting the other approved song during a CG must not start downloading it.");
            audio.SetMediaMusicActive("test-music-cg", false);
            yield return Audible(1);
            Assert.That(music.time, Is.InRange(chasePosition - .15f, chasePosition + 1.5f));
            Assert.That(sfx.mute, Is.False);
        }

        [UnityTest]
        public IEnumerator RapidSelectionAndPageChangesCannotStartAnOutdatedDownload()
        {
            audio.SetMusicEnabled(true); // The default approved jazz uses the existing native clip.
            yield return Audible(0);
            AssertApprovedNativeSong(0);
            Assert.That(Field<UnityEngine.Networking.UnityWebRequest>("webMusicRequest"), Is.Null);
            audio.SelectHomeMusic(1); // Chase is optional and only requests after the first frame.
            audio.SelectHomeMusic(0); // Cancel the pending selection before it can download.
            audio.SelectHomeMusic(1);
            audio.SetBaseMusicContext(GameAudio.MusicContext.Silent);
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(music.isPlaying, Is.False);
            Assert.That(Field<UnityEngine.Networking.UnityWebRequest>("webMusicRequest"), Is.Null);
            Assert.That(audio.NextHomeMusic(), Is.False, "Hidden Home controls must not change another page.");

            audio.SetBaseMusicContext(GameAudio.MusicContext.Battle);
            AudioClip battle = Resources.Load<AudioClip>("Audio/bgm-battle");
            float deadline = Time.realtimeSinceStartup + 3f;
            while ((music.clip != battle || !music.isPlaying ||
                    Mathf.Abs(music.volume - GameAudio.BattleMusicOutputVolume) > .001f) &&
                   Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(music.clip, Is.SameAs(battle));
            Assert.That(music.loop, Is.True);
            Assert.That(music.volume, Is.EqualTo(GameAudio.BattleMusicOutputVolume).Within(.001f));
            audio.SetBaseMusicContext(GameAudio.MusicContext.Home);
            yield return Audible(1);
            AssertApprovedNativeSong(1);
            Assert.That(audio.HomeMusicTitle, Is.EqualTo("Chase the Glow"));
        }

        [UnityTest]
        public IEnumerator LobbyLabelFollowsAutomaticSelectionAndRebindingDoesNotDuplicateCallbacks()
        {
            captionHost = new GameObject("MusicCaption", typeof(RectTransform), typeof(Text));
            Text text = captionHost.GetComponent<Text>();
            HomeMusicSwitcher view = captionHost.AddComponent<HomeMusicSwitcher>();
            view.Bind(audio, text);
            view.Bind(audio, text);
            Assert.That(text.text, Is.EqualTo(HomeMusicPlaylist.Get(0).Title));
            audio.NextHomeMusic();
            Assert.That(text.text, Is.EqualTo(HomeMusicPlaylist.Get(1).Title));
            captionHost.SetActive(false);
            audio.NextHomeMusic();
            Assert.That(text.text, Is.EqualTo(HomeMusicPlaylist.Get(1).Title));
            captionHost.SetActive(true);
            Assert.That(text.text, Is.EqualTo(HomeMusicPlaylist.Get(2 % HomeMusicPlaylist.Count).Title));
            Object.Destroy(captionHost);
            yield return null;
            Assert.DoesNotThrow(() => audio.NextHomeMusic());
        }

        [UnityTest]
        public IEnumerator LobbyPauseKeepsPositionAndDoesNotSilenceBattle()
        {
            audio.SetMusicEnabled(true);
            yield return Audible(0);
            music.time = 9f;
            yield return new WaitForSecondsRealtime(.1f);
            float before = music.time;
            Assert.That(audio.ToggleHomeMusicPaused(), Is.True);
            Assert.That(audio.HomeMusicPaused, Is.True);
            Assert.That(model.Save.MusicEnabled, Is.True);
            audio.PlayClick();
            audio.ResumeAfterUserGesture();
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(music.isPlaying, Is.False, "A click must not accidentally resume a paused record.");

            audio.SetContextMusicActive("pause-test-battle", GameAudio.MusicContext.Battle, true);
            float deadline = Time.realtimeSinceStartup + 3f;
            while ((!music.isPlaying || music.clip != Resources.Load<AudioClip>("Audio/bgm-battle")) &&
                   Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(music.isPlaying, Is.True, "Lobby pause must not become a global music mute.");
            Assert.That(audio.ToggleHomeMusicPaused(), Is.False, "Hidden lobby controls cannot change battle playback.");
            audio.SetContextMusicActive("pause-test-battle", GameAudio.MusicContext.Battle, false);
            yield return null;
            Assert.That(music.isPlaying, Is.False, "Returning to the lobby must remember its paused state.");
            audio.ToggleHomeMusicPaused();
            yield return Audible(0);
            Assert.That(music.time, Is.InRange(before - .15f, before + 1.5f));
        }

        [UnityTest]
        public IEnumerator PauseAndTrackSelectionKeepMutedPreferenceAndRecordStill()
        {
            var disc = new GameObject("PausedRecordFixture", typeof(RectTransform));
            HomeMusicDiscSpin spin = disc.AddComponent<HomeMusicDiscSpin>();
            spin.Bind(audio);
            Quaternion before = disc.transform.localRotation;
            audio.ToggleHomeMusicPaused();
            audio.NextHomeMusic();
            spin.AdvancePresentation(1f);
            Assert.That(disc.transform.localRotation, Is.EqualTo(before));
            Assert.That(audio.HomeMusicPaused, Is.True);
            Assert.That(audio.MusicEnabled, Is.False);
            Assert.That(music.isPlaying, Is.False);
            Assert.That(Field<UnityEngine.Networking.UnityWebRequest>("webMusicRequest"), Is.Null);
            Object.Destroy(disc);
            yield return null;
        }

        private IEnumerator Audible(int index)
        {
            float deadline = Time.realtimeSinceStartup + 10f;
            while ((audio.HomeMusicTrackIndex != index || music.clip == null || !music.isPlaying ||
                    music.mute || music.volume < GameAudio.MusicOutputVolume - .001f) &&
                   Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(audio.HomeMusicTrackIndex, Is.EqualTo(index));
            Assert.That(music.clip, Is.Not.Null, "The approved real audio must decode, not fall back to a tone.");
            Assert.That(music.clip.length, Is.GreaterThan(120f));
            Assert.That(music.isPlaying, Is.True, "This test requires an advancing native audio backend.");
            Assert.That(music.mute, Is.False);
            Assert.That(music.volume, Is.EqualTo(GameAudio.MusicOutputVolume).Within(.001f));
            int before = music.timeSamples;
            yield return new WaitForSecondsRealtime(.06f);
            Assert.That(music.timeSamples, Is.GreaterThan(before));
        }

        private void AssertApprovedNativeSong(int index)
        {
            string[] approvedHashes =
            {
                "f19d89ea8124a31b0bae5a7a8811166cd0efdb45363994f60e8c98122a527a95",
                "5d360e8e7acbc7c6f0f34a2fcf214750f9ce0d89f9d7d13b04468b0d74b2ffff",
            };
            HomeMusicPlaylist.Track track = HomeMusicPlaylist.Get(index);
            string path = Path.Combine(Application.streamingAssetsPath, "Music", track.FileName);
            using (var hash = SHA256.Create())
            using (var source = File.OpenRead(path))
                Assert.That(BitConverter.ToString(hash.ComputeHash(source)).Replace("-", "").ToLowerInvariant(),
                    Is.EqualTo(approvedHashes[index]), "Playlist selection must retain the approved source bytes.");

            if (index == HomeMusicPlaylist.NativeResourceTrackIndex)
            {
                Assert.That(music.clip, Is.SameAs(Resources.Load<AudioClip>("Audio/bgm")));
                Assert.That(music.clip.length, Is.EqualTo(172.3567f).Within(.12f));
            }
            else
            {
                Assert.That(music.clip.name, Is.EqualTo(track.Title));
                const float sourceDecodedSeconds = 158.7335f;
                // Native GetAudioClip(MPEG) uses FMOD metadata, which can round/pad to
                // the following whole second rather than reporting the exact source duration.
                // Source identity stays exact; Web Audio's decoded duration is verified
                // separately. Seek against this native clip's own end, then require the
                // real transport to finish and advance rather than assuming source timing.
                Assert.That(music.clip.length,
                    Is.InRange(sourceDecodedSeconds - .12f, Mathf.Ceil(sourceDecodedSeconds) + 1.001f));
                TestContext.WriteLine($"{track.Title}: source decode {sourceDecodedSeconds:0.000}s; " +
                    $"native MPEG metadata {music.clip.length:0.000}s, {music.clip.samples} samples/{music.clip.frequency}Hz.");
            }
        }

        private T Field<T>(string name) => (T)typeof(GameAudio).GetField(name, Private).GetValue(audio);
    }
}
