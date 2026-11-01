using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ChoSiren.Tests
{
    public sealed class BattleSfxMixer1019Tests
    {
        [UnityTest]
        public IEnumerator RealEffectsUseBoundedIndependentSourcesAndAllObeyMute()
        {
            var host = new GameObject("BoundedBattleSfxFixture");
            var first = host.AddComponent<AudioSource>();
            var mixer = host.AddComponent<BattleSfxMixer1019>();
            mixer.Initialize(first);
            string[] names = { "hit", "hurt", "block", "crit", "debuff", "heal", "shield", "dice", "ultimate", "ko", "victory" };
            foreach (string name in names)
            {
                var clip = Resources.Load<AudioClip>("Audio/Sfx/" + name);
                Assert.That(clip, Is.Not.Null);
                mixer.Play(name, clip);
            }
            Assert.That(mixer.AllocatedChannelCount, Is.EqualTo(6));
            Assert.That(host.GetComponentsInChildren<AudioSource>().Length, Is.EqualTo(6));
            Assert.That(host.GetComponents<AudioSource>().Length, Is.EqualTo(1), "Extra voices are children; preserve GameAudio's first-source contract.");
            foreach (var source in host.GetComponentsInChildren<AudioSource>())
            {
                Assert.That(source.loop, Is.False);
                Assert.That(source.spatialBlend, Is.Zero);
                Assert.That(source.clip, Is.Not.Null, "Play uses one assigned clip per source, not unbounded PlayOneShot voices.");
            }
            mixer.SetMuted(true);
            foreach (var source in host.GetComponentsInChildren<AudioSource>()) Assert.That(source.mute, Is.True);
            Assert.That(mixer.Play("stamp", Resources.Load<AudioClip>("Audio/Sfx/stamp")), Is.False);
            mixer.SetMuted(false);
            foreach (var source in host.GetComponentsInChildren<AudioSource>()) Assert.That(source.mute, Is.False);
            mixer.StopAll();
            foreach (var source in host.GetComponentsInChildren<AudioSource>()) Assert.That(source.clip, Is.Null);
            Assert.That(mixer.ActiveVoiceCount, Is.Zero);
            Object.Destroy(host);
            yield return null;
        }

        [UnityTest]
        public IEnumerator MissingSilentAndDisabledRequestsNeverAllocateExtraChannels()
        {
            var host = new GameObject("BattleSfxAdmissionFixture");
            var mixer = host.AddComponent<BattleSfxMixer1019>();
            var clip = Resources.Load<AudioClip>("Audio/Sfx/ultimate");
            Assert.That(mixer.Play("ultimate", clip), Is.False, "No source until explicitly initialized.");
            mixer.Initialize(host.AddComponent<AudioSource>());
            Assert.That(mixer.Play("missing", null), Is.False);
            Assert.That(mixer.Play("ultimate", clip, 0f), Is.False);
            Assert.That(mixer.AllocatedChannelCount, Is.EqualTo(1));
            Assert.That(mixer.Play("ultimate", clip), Is.True);
            mixer.enabled = false;
            Assert.That(mixer.ActiveVoiceCount, Is.Zero);
            Assert.That(mixer.Play("ultimate", clip), Is.False);
            mixer.enabled = true;
            Assert.That(mixer.Play("ultimate", clip), Is.True, "Disabling clears old dedupe so a new encounter can start immediately.");
            Object.Destroy(host);
            yield return null;
        }
    }
}
