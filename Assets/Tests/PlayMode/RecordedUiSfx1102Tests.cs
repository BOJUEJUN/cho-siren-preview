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
    public sealed class RecordedUiSfx1102Tests
    {
        [UnityTest] public IEnumerator RecordedScrubStopsOnMuteAndIdleWithoutStoppingTheSnap()
        {
            var suspended = new List<GameAudio>();
            foreach (var current in Object.FindObjectsByType<GameAudio>(FindObjectsSortMode.None))
                if (current.enabled) { suspended.Add(current); current.enabled = false; }
            var host = new GameObject("RecordedUiSfxReview");
            if (Object.FindAnyObjectByType<AudioListener>() == null) host.AddComponent<AudioListener>();
            try
            {
                var model = new GameModel(() => DateTime.Now, new InMemorySaveStore());
                var audio = host.AddComponent<GameAudio>(); audio.Initialize(model);
                audio.SetBaseMusicContext(GameAudio.MusicContext.Silent);
                foreach (string key in new[] { "ui-click", "ui-confirm", "ui-cancel", "ui-switch", "ui-error", "vinyl-scrub", "vinyl-tick" })
                {
                    AudioClip clip = Resources.Load<AudioClip>("Audio/Ui1102/" + key);
                    Assert.That(clip, Is.Not.Null, key + " must use the published sample asset");
                    Assert.That(clip.length, Is.GreaterThan(.02f));
                }
                AudioClip click = (AudioClip)typeof(GameAudio).GetField("clickClip", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(audio);
                Assert.That(click, Is.SameAs(Resources.Load<AudioClip>("Audio/Ui1102/ui-click")));
                audio.SetVinylScrub(.8f); yield return null;
                AudioSource scratch = Array.Find(host.GetComponents<AudioSource>(), s => s.clip != null && s.clip.name == "vinyl-scrub");
                Assert.That(scratch, Is.Not.Null); Assert.That(scratch.isPlaying, Is.True);
                audio.PlayVinylSnap(); audio.StopVinylScrub();
                Assert.That(scratch.isPlaying, Is.False);
                AudioSource oneShot = (AudioSource)typeof(GameAudio).GetField("sfxSource", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(audio);
                Assert.That(oneShot.isPlaying, Is.True, "stopping scratch must not cut the separate snap");
                audio.SetVinylScrub(.6f); model.ToggleSfx();
                Assert.That(scratch.mute, Is.True); Assert.That(scratch.isPlaying, Is.False);
                audio.SetVinylScrub(.8f); Assert.That(scratch.isPlaying, Is.False);
                model.ToggleSfx(); audio.SetVinylScrub(.8f); Assert.That(scratch.isPlaying, Is.True);
                yield return new WaitForSecondsRealtime(.14f);
                Assert.That(scratch.isPlaying, Is.False, "a missed pointer-end callback must not leave a loop playing");
                Assert.That(audio.CurrentMusicContext, Is.EqualTo(GameAudio.MusicContext.Silent));
            }
            finally
            {
                Object.DestroyImmediate(host);
                foreach (GameAudio previous in suspended) if (previous != null) previous.enabled = true;
            }
        }
    }
}
