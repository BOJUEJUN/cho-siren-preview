using System.Collections;
using ChoSiren.Systems.Tactics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ChoSiren.Tests
{
    public sealed class UltimateTimingInput1019Tests
    {
        [UnityTest]
        public IEnumerator PauseCancelAndRepeatedInputCannotDuplicateARelease()
        {
            var host = new GameObject("TimingHost", typeof(RectTransform));
            ((RectTransform)host.transform).sizeDelta = new Vector2(720, 1536);
            var view = host.AddComponent<BattleUltimateTimingView1019>();
            bool paused = false; int releases = 0; UltimateTimingGrade grade = UltimateTimingGrade.Miss;
            view.Configure(host.transform, Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"), () => paused);
            Assert.That(view.Begin(null, null, null, result => { releases++; grade = result; }), Is.True);
            Assert.That(view.Begin(null, null, null, _ => releases += 100), Is.False, "a second card tap cannot replace the pending command");
            paused = true; view.Advance(5); view.Hit();
            Assert.That(releases, Is.Zero);
            paused = false; view.Advance(.7f * UltimateTimingChallenge.DurationSeconds); view.Hit(); view.Hit(); view.Advance(.2f);
            Assert.That(releases, Is.EqualTo(1)); Assert.That(grade, Is.EqualTo(UltimateTimingGrade.Perfect));
            view.Advance(10); Assert.That(releases, Is.EqualTo(1));
            Assert.That(view.Begin(null, null, null, _ => releases++), Is.True);
            view.Cancel(); view.Advance(10); Assert.That(releases, Is.EqualTo(1), "closing a prompt never spends its cast");
            Object.Destroy(host); yield return null;
        }
    }
}
