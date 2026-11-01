using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ChoSiren.Tests
{
    /// <summary>Exercises the actual long battle effect and the user's independent audio switches.</summary>
    public sealed class BattleSfxSettingsTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly List<GameAudio> suspended = new List<GameAudio>();
        private GameObject host;
        private GameAudio audio;
        private GameModel model;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            foreach (GameAudio current in Object.FindObjectsByType<GameAudio>(FindObjectsSortMode.None))
                if (current.enabled) { suspended.Add(current); current.enabled = false; }
            model = new GameModel(() => DateTime.Now, new InMemorySaveStore());
            host = new GameObject("BattleSfxSettingsFixture");
            if (Object.FindAnyObjectByType<AudioListener>() == null) host.AddComponent<AudioListener>();
            audio = host.AddComponent<GameAudio>();
            audio.Initialize(model);
            audio.SetBaseMusicContext(GameAudio.MusicContext.Silent);
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
        public IEnumerator TurningOffSfxImmediatelyMutesAnAlreadyPlayingBattleEffect()
        {
            AudioClip clip = Resources.Load<AudioClip>("Audio/Sfx/ultimate");
            Assert.That(clip, Is.Not.Null, "Use the real battle effect, not a synthetic test substitute.");
            Assert.That(clip.length, Is.GreaterThan(.5f));
            audio.PlaySfx("ultimate");
            AudioSource battle = Field<AudioSource>("battleSfxSource");
            Assert.That(battle, Is.Not.Null);
            float deadline = Time.realtimeSinceStartup + .25f;
            while (!battle.isPlaying && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(battle.isPlaying, Is.True, "The effect must actually have started before switching it off.");

            bool musicEnabled = model.Save.MusicEnabled;
            model.ToggleSfx();
            Assert.That(battle.mute, Is.True, "Already playing battle one-shots must obey the SFX switch immediately.");
            Assert.That(Field<AudioSource>("sfxSource").mute, Is.True);
            Assert.That(Field<AudioSource>("voiceSource").mute, Is.True);
            Assert.That(model.Save.MusicEnabled, Is.EqualTo(musicEnabled));
            yield return null;
            Assert.That(battle.mute, Is.True);

            model.ToggleSfx();
            Assert.That(battle.mute, Is.False, "Re-enabling SFX must restore the existing battle source.");
            Assert.That(Field<AudioSource>("battleSfxSource"), Is.SameAs(battle));
            yield return new WaitForSecondsRealtime(.08f);
            audio.PlaySfx("hit");
            Assert.That(battle.isPlaying, Is.True);
            Assert.That(host.GetComponents<AudioSource>().Length, Is.EqualTo(4));
        }

        [UnityTest]
        public IEnumerator DisabledBattleEffectsDoNotCreateOrRestartSourcesOrChangeMusic()
        {
            model.ToggleSfx();
            audio.PlaySfx("ultimate");
            audio.PlaySfx("dice");
            Assert.That(Field<AudioSource>("battleSfxSource"), Is.Null);
            Assert.That(host.GetComponents<AudioSource>().Length, Is.EqualTo(3));

            model.ToggleSfx();
            audio.PlaySfx("ultimate");
            AudioSource battle = Field<AudioSource>("battleSfxSource");
            Assert.That(battle, Is.Not.Null);
            model.ToggleSfx();
            audio.SetMusicEnabled(false);
            audio.SetBaseMusicContext(GameAudio.MusicContext.Battle);
            yield return null;
            Assert.That(battle.mute, Is.True, "Routing or changing music must not unmute disabled battle effects.");
            audio.PlaySfx("victory");
            Assert.That(Field<AudioSource>("battleSfxSource"), Is.SameAs(battle));
            Assert.That(host.GetComponents<AudioSource>().Length, Is.EqualTo(4));
            Assert.That(model.Save.SfxEnabled, Is.False);
            Assert.That(model.Save.MusicEnabled, Is.False);
        }

        private T Field<T>(string name) =>
            (T)typeof(GameAudio).GetField(name, Private).GetValue(audio);
    }
}
