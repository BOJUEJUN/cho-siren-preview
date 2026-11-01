using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using ChoSiren.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ChoSiren.Tests
{
    public sealed class BattleChorus1028Tests
    {
        private readonly List<GameAudio> suspended = new List<GameAudio>();
        private GameObject host;
        private GameAudio audio;
        private BattleChorus1028 chorus;
        private bool blocked;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            foreach (GameAudio current in Object.FindObjectsByType<GameAudio>(FindObjectsSortMode.None))
                if (current.enabled) { suspended.Add(current); current.enabled = false; }
            host = new GameObject("BattleChorusFixture");
            if (Object.FindAnyObjectByType<AudioListener>() == null) host.AddComponent<AudioListener>();
            audio = host.AddComponent<GameAudio>();
            audio.Initialize(new GameModel(() => DateTime.Now, new InMemorySaveStore()));
            audio.SetBaseMusicContext(GameAudio.MusicContext.Battle);
            chorus = host.AddComponent<BattleChorus1028>();
            chorus.Configure(audio, () => blocked);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (host != null) Object.Destroy(host);
            yield return null;
            foreach (GameAudio current in suspended) if (current != null) current.enabled = true;
            suspended.Clear();
        }

        [UnityTest]
        public IEnumerator RealShortVocalRespondsWithoutSeekingOrStoppingTheBattleSong()
        {
            var music = Source("musicSource");
            AudioClip song = Resources.Load<AudioClip>("Audio/bgm-battle");
            float ready = Time.realtimeSinceStartup + 3f;
            while ((!music.isPlaying || music.clip != song ||
                    music.volume < GameAudio.BattleMusicOutputVolume - .001f) &&
                   Time.realtimeSinceStartup < ready) yield return null;
            Assert.That(music.clip, Is.SameAs(song), "Wait for the Home-to-Battle fade before testing chorus continuity.");
            Assert.That(music.isPlaying, Is.True);
            float before = music.time;
            chorus.Queue("xingli");
            float deadline = Time.realtimeSinceStartup + 1.5f;
            while (!audio.IsBattleChorusPlaying && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(audio.IsBattleChorusPlaying, Is.True);
            Assert.That(Source("chorusSource").clip.length, Is.InRange(1.4f, 1.8f));
            Assert.That(music.clip, Is.SameAs(song));
            Assert.That(music.isPlaying, Is.True);
            Assert.That(music.time, Is.GreaterThan(before));
            Assert.That(audio.MediaOwnsMusic, Is.False);
            blocked = true;
            yield return null;
            yield return null;
            Assert.That(audio.IsBattleChorusPlaying, Is.False);
            blocked = false;
            yield return new WaitForSecondsRealtime(.6f);
            Assert.That(audio.IsBattleChorusPlaying, Is.False, "Resume must not replay a cancelled response.");
        }

        [UnityTest]
        public IEnumerator SameFrameMuteAndResumeDropThePendingResponse()
        {
            chorus.Queue("xingli");
            audio.SetMusicEnabled(false);
            audio.SetMusicEnabled(true);
            yield return new WaitForSecondsRealtime(.7f);
            Assert.That(audio.IsBattleChorusPlaying, Is.False,
                "Even without an Update during mute, the old response must be cancelled.");
            chorus.Queue("feiyin");
            audio.SetMediaMusicActive("brief-cg", true);
            audio.SetMediaMusicActive("brief-cg", false);
            yield return new WaitForSecondsRealtime(.7f);
            Assert.That(audio.IsBattleChorusPlaying, Is.False);
        }

        [UnityTest]
        public IEnumerator ChorusRespectsMusicMuteMediaOwnerAndHasTwoRealTakesPerMember()
        {
            foreach (string member in new[] { "xingli", "feiyin", "wubai", "yeying" })
                for (int take = 1; take <= 2; take++)
                    Assert.That(Resources.Load<AudioClip>("Audio/Chorus/" + member + "-" + take), Is.Not.Null);
            audio.SetMusicEnabled(false);
            Assert.That(audio.PlayBattleChorus("xingli", 0, "test"), Is.False);
            audio.SetMusicEnabled(true);
            audio.SetMediaMusicActive("test-cg", true);
            Assert.That(audio.PlayBattleChorus("xingli", 0, "test"), Is.False);
            audio.SetMediaMusicActive("test-cg", false);
            Assert.That(audio.PlayBattleChorus("xingli", 0, "test"), Is.True);
            AudioClip first = Source("chorusSource").clip;
            audio.StopBattleChorus("other-owner");
            Assert.That(audio.IsBattleChorusPlaying, Is.True);
            audio.StopBattleChorus("test");
            Assert.That(audio.IsBattleChorusPlaying, Is.False);
            Assert.That(audio.PlayBattleChorus("xingli", 1, "test"), Is.True);
            Assert.That(Source("chorusSource").clip, Is.Not.SameAs(first));
            audio.SetMusicEnabled(false);
            yield return null;
            Assert.That(audio.IsBattleChorusPlaying, Is.False);
        }

        private AudioSource Source(string field) => (AudioSource)typeof(GameAudio)
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(audio);
    }
}
