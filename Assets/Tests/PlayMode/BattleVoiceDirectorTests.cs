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
    /// <summary>Requires the four real approved sample recordings and an advancing native audio backend.</summary>
    public sealed class BattleVoiceDirectorTests
    {
        private readonly List<GameAudio> suspended = new List<GameAudio>();
        private GameObject host, directorHost;
        private GameAudio audio;
        private BattleVoiceDirector director;
        private AudioSource voice;
        private GameModel model;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            foreach (GameAudio current in Object.FindObjectsByType<GameAudio>(FindObjectsSortMode.None))
                if (current.enabled) { suspended.Add(current); current.enabled = false; }
            foreach (string member in new[] { "xingli", "feiyin", "wubai", "yeying" })
                Assert.That(Resources.Load<AudioClip>("Audio/Voice/Battle/" + member + "-active-01"), Is.Not.Null,
                    "Import the real sample recordings before accepting speech runtime.");
            host = new GameObject("BattleVoiceDirectorFixture");
            if (Object.FindAnyObjectByType<AudioListener>() == null) host.AddComponent<AudioListener>();
            model = new GameModel(() => DateTime.Now, new InMemorySaveStore());
            audio = host.AddComponent<GameAudio>();
            audio.Initialize(model);
            audio.SetMusicEnabled(false);
            audio.SetBaseMusicContext(GameAudio.MusicContext.Battle);
            voice = (AudioSource)typeof(GameAudio).GetField("voiceSource", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(audio);
            directorHost = new GameObject("BattleVoiceOwner");
            directorHost.transform.SetParent(host.transform, false);
            director = directorHost.AddComponent<BattleVoiceDirector>();
            director.Configure(audio);
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
        public IEnumerator SimultaneousSkillsChooseCaptainAndNewActiveDoesNotInterrupt()
        {
            director.Queue("xingli", BattleVoiceDirector.Cue.Active);
            director.Queue("wubai", BattleVoiceDirector.Cue.Active);
            director.Queue("feiyin", BattleVoiceDirector.Cue.Active, true);
            director.Queue("yeying", BattleVoiceDirector.Cue.Active);
            yield return Audible("feiyin-active-01");
            AudioClip first = voice.clip;
            int before = voice.timeSamples;
            director.Queue("xingli", BattleVoiceDirector.Cue.Active, true);
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(voice.clip, Is.SameAs(first), "Another active must not clip the speaking captain.");
            Assert.That(voice.timeSamples, Is.GreaterThan(before));
            Assert.That(host.GetComponents<AudioSource>().Length, Is.EqualTo(3), "All callouts share GameAudio's one voice source.");
            Assert.That(BattleVoiceDirector.MinimumStartInterval, Is.GreaterThanOrEqualTo(1.2f));
        }

        [UnityTest]
        public IEnumerator RepeatingCaptainCalloutLeavesRoomForAnotherMemberButNeverBlocksAnUltimate()
        {
            director.Queue("feiyin", BattleVoiceDirector.Cue.Active, true);
            yield return Audible("feiyin-active-01");
            voice.Stop(); // End only this real recording early to isolate scheduling from its length.
            yield return new WaitForSecondsRealtime(1.3f);
            director.Queue("feiyin", BattleVoiceDirector.Cue.Active, true);
            director.Queue("xingli", BattleVoiceDirector.Cue.Active);
            yield return Audible("xingli-active-01");
            Assert.That(voice.clip.name, Is.EqualTo("xingli-active-01"),
                "The captain cannot monopolize every small-skill cycle with the same two recordings.");
            yield return new WaitForSecondsRealtime(1.3f);
            director.Queue("feiyin", BattleVoiceDirector.Cue.Ultimate, true);
            yield return Audible("feiyin-ultimate-01");
        }

        [UnityTest]
        public IEnumerator CgAndSfxOffCancelSpeechWithoutResumingStaleQueue()
        {
            director.Queue("xingli", BattleVoiceDirector.Cue.Active);
            yield return Audible("xingli-active-01");
            director.Queue("feiyin", BattleVoiceDirector.Cue.Active);
            audio.SetMediaMusicActive("battle-voice-test-cg", true);
            yield return null;
            yield return null;
            Assert.That(voice.isPlaying, Is.False, "A cutscene must own its speech and music.");
            audio.SetMediaMusicActive("battle-voice-test-cg", false);
            yield return new WaitForSecondsRealtime(1.3f);
            Assert.That(voice.isPlaying, Is.False, "Do not replay a stale skill after CG.");
            director.Queue("wubai", BattleVoiceDirector.Cue.Active);
            yield return Audible("wubai-active-01");
            model.ToggleSfx();
            yield return null;
            yield return null;
            Assert.That(voice.mute, Is.True);
            Assert.That(voice.isPlaying, Is.False);
            Assert.That(model.Save.MusicEnabled, Is.False);
        }

        [UnityTest]
        public IEnumerator DisableStopsOwnedVoiceButCannotStopAnotherPagesNewVoice()
        {
            director.Queue("yeying", BattleVoiceDirector.Cue.Active);
            yield return Audible("yeying-active-01");
            directorHost.SetActive(false);
            Assert.That(voice.isPlaying, Is.False);

            // A different page starts unowned speech before the old battle's final destruction.
            Assert.That(audio.PlayVoice("Battle/wubai-active-01"), Is.True);
            yield return Audible("wubai-active-01");
            director.Release();
            Assert.That(voice.isPlaying, Is.True, "Releasing an old battle must not stop a newer page's line.");
        }

        [UnityTest]
        public IEnumerator DefeatLineOutranksActiveReportsItsVariantAndSurvivesALostBattle()
        {
            var spoken = new List<string>();
            director.Spoke += (member, cue, variant, seconds) => spoken.Add($"{member}-{cue}-{variant}-{seconds > .5f}");
            director.Queue("xingli", BattleVoiceDirector.Cue.Active);
            yield return Audible("xingli-active-01");
            yield return new WaitForSecondsRealtime(1.25f);
            director.Queue("wubai", BattleVoiceDirector.Cue.Defeat);
            yield return Audible("wubai-defeat-01");
            Assert.That(spoken, Does.Contain("wubai-Defeat-1-True"), "subtitles need the actual variant and clip length");
            director.Complete(false, "xingli");
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(voice.isPlaying, Is.True, "a wipe's last 阵亡台词 is not cut by the defeat result");
            Assert.That(voice.clip.name, Is.EqualTo("wubai-defeat-01"));
        }

        private IEnumerator Audible(string expectedName)
        {
            float deadline = Time.realtimeSinceStartup + 1f;
            while ((!voice.isPlaying || voice.clip == null || voice.clip.name != expectedName) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(voice.isPlaying, Is.True);
            Assert.That(voice.clip.name, Is.EqualTo(expectedName));
            int before = voice.timeSamples;
            deadline = Time.realtimeSinceStartup + .3f;
            while (voice.timeSamples <= before && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(voice.timeSamples, Is.GreaterThan(before), "The real voice playhead must advance.");
        }
    }
}
