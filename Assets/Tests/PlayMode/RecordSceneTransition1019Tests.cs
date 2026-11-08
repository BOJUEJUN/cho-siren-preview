using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using ChoSiren.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.Video;
using Object = UnityEngine.Object;

namespace ChoSiren.Tests
{
    public sealed class RecordSceneTransition1019Tests
    {
        GameObject host;
        bool hadReduceMotion;
        int previousReduceMotion;

        [SetUp] public void Before()
        {
            hadReduceMotion = PlayerPrefs.HasKey(BattlePresentationOptions.ReduceMotionKey);
            previousReduceMotion = PlayerPrefs.GetInt(BattlePresentationOptions.ReduceMotionKey, 0);
            PlayerPrefs.SetInt(BattlePresentationOptions.ReduceMotionKey, 0);
            host = new GameObject("RecordSceneTransition1019", typeof(RectTransform));
            ((RectTransform)host.transform).sizeDelta = new Vector2(720, 1536);
        }
        [TearDown] public void After()
        {
            if (host != null) Object.DestroyImmediate(host);
            if (hadReduceMotion) PlayerPrefs.SetInt(BattlePresentationOptions.ReduceMotionKey, previousReduceMotion);
            else PlayerPrefs.DeleteKey(BattlePresentationOptions.ReduceMotionKey);
        }

        [Ignore("Quarantined 2026-11-08: record transition replaced by the vinyl stage map (11-02); see Docs/TEST-QUARANTINE-1108.md")]
        [Test] public void WarmHasNoInputLockVideoDecoderOrMusicSource()
        {
            var fx = ChallengeVideoTransition.Warm(host.transform);
            Assert.That(ChallengeVideoTransition.IsCoveringGame, Is.False);
            Assert.That(fx.IsPlaying, Is.False);
            Assert.That(fx.GetComponent<CanvasGroup>().alpha, Is.Zero);
            Assert.That(fx.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
            Assert.That(fx.GetComponentsInChildren<VideoPlayer>(true), Is.Empty);
            Assert.That(fx.GetComponentsInChildren<AudioSource>(true), Is.Empty);
            var cg = fx.GetComponentsInChildren<Image>(true).Where(i => i.name == "GeneratedActionCG").ToArray();
            Assert.That(cg, Has.Length.EqualTo(3));
            Assert.That(cg.All(i => i.sprite != null && i.sprite.name == "OriginalXingliActionCG"), Is.True);
            Assert.That(cg.All(i => i.preserveAspect), Is.True);
        }

        [Ignore("Quarantined 2026-11-08: record transition replaced by the vinyl stage map (11-02); see Docs/TEST-QUARANTINE-1108.md")]
        [Test] public void CallbackRunsOnceUnderCoverAndInputRemainsLockedUntilTheFullReveal()
        {
            var fx = ChallengeVideoTransition.Warm(host.transform);
            int calls = 0;
            bool callbackCovered = false;
            fx.Play(host.transform, () => { calls++; callbackCovered = ChallengeVideoTransition.IsCoveringGame; }, null);
            Assert.That(ChallengeVideoTransition.IsCoveringGame, Is.True);
            Assert.That(fx.GetComponent<CanvasGroup>().blocksRaycasts, Is.True);
            fx.Advance(ChallengeVideoTransition.CoverTime - .01f);
            Assert.That(calls, Is.Zero);
            fx.Advance(.02f);
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(callbackCovered, Is.True);
            fx.Advance(ChallengeVideoTransition.SplitStart - fx.Clock + .01f);
            Assert.That(ChallengeVideoTransition.IsCoveringGame, Is.True, "A partially open paper reveal still blocks input and the battle clock");
            fx.Advance(ChallengeVideoTransition.Duration - fx.Clock + .01f);
            Assert.That(ChallengeVideoTransition.IsCoveringGame, Is.False);
            Assert.That(calls, Is.EqualTo(1));
        }

        [TestCase(720, 1536)] [TestCase(1280, 720)] [TestCase(390, 844)]
        public void EveryRecordStaysSquareAndTheActionCgKeepsItsNativeRatio(int width, int height)
        {
            ((RectTransform)host.transform).sizeDelta = new Vector2(width, height);
            var fx = ChallengeVideoTransition.Warm(host.transform);
            fx.Play(host.transform, null, null);
            foreach (float time in new[] { .14f, .4f, 1.25f })
            {
                fx.Advance(time - fx.Clock);
                foreach (var disc in fx.GetComponentsInChildren<RecordTransitionDisc1019>(true))
                {
                    var rect = disc.rectTransform;
                    Assert.That(rect.rect.width, Is.EqualTo(rect.rect.height).Within(.01f));
                    Assert.That(rect.localScale.x, Is.EqualTo(rect.localScale.y).Within(.001f));
                }
                foreach (var cg in fx.GetComponentsInChildren<Image>(true).Where(i => i.name == "GeneratedActionCG"))
                    Assert.That(cg.rectTransform.rect.width / cg.rectTransform.rect.height, Is.EqualTo(1.5f).Within(.001f));
            }
        }

        [Test] public void DoubleClicksCannotDispatchTheSameOrAnotherTransitionTwice()
        {
            var owner = new GameObject("SecondOwner", typeof(RectTransform)); owner.transform.SetParent(host.transform, false);
            var first = ChallengeVideoTransition.Warm(host.transform);
            var other = ChallengeVideoTransition.Warm(owner.transform);
            int firstCalls = 0, otherCalls = 0;
            first.Play(host.transform, () => firstCalls++, null);
            first.Play(host.transform, () => firstCalls += 100, null);
            other.Play(host.transform, () => otherCalls++, null);
            first.Advance(2);
            Assert.That(firstCalls, Is.EqualTo(1));
            Assert.That(otherCalls, Is.Zero);
            Assert.That(other.IsPlaying, Is.False);
            Assert.That(ChallengeVideoTransition.IsCoveringGame, Is.False);
        }

        [TestCase(false)] [TestCase(true)] public void CancellationReleasesCoverWithoutOpeningTheScene(bool destroy)
        {
            var fx = ChallengeVideoTransition.Warm(host.transform);
            int calls = 0;
            fx.Play(host.transform, () => calls++, null);
            if (destroy) Object.DestroyImmediate(fx.gameObject); else fx.gameObject.SetActive(false);
            Assert.That(ChallengeVideoTransition.IsCoveringGame, Is.False);
            Assert.That(calls, Is.Zero);
        }

        [Ignore("Quarantined 2026-11-08: record transition replaced by the vinyl stage map (11-02); see Docs/TEST-QUARANTINE-1108.md")]
        [Test] public void FailedSceneOrDestroyedOverlayCannotLeaveAStuckInputLock()
        {
            var fx = ChallengeVideoTransition.Warm(host.transform);
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: transition-scene-test"));
            fx.Play(host.transform, () => throw new InvalidOperationException("transition-scene-test"), null);
            fx.Advance(.4f);
            Assert.That(ChallengeVideoTransition.IsCoveringGame, Is.False);
            var owner = new GameObject("DestroyedOverlayOwner", typeof(RectTransform)); owner.transform.SetParent(host.transform, false);
            var destroyed = ChallengeVideoTransition.Warm(owner.transform);
            destroyed.Play(host.transform, () => Object.DestroyImmediate(destroyed.gameObject), null);
            Assert.DoesNotThrow(() => destroyed.Advance(.4f));
            Assert.That(ChallengeVideoTransition.IsCoveringGame, Is.False);
        }

        [Ignore("Quarantined 2026-11-08: record transition replaced by the vinyl stage map (11-02); see Docs/TEST-QUARANTINE-1108.md")]
        [Test] public void ReducedMotionReturnsHomeExactlyOnceAndNeverLeavesTheCoverActive()
        {
            PlayerPrefs.SetInt(BattlePresentationOptions.ReduceMotionKey, 1);
            var fx = ChallengeVideoTransition.Warm(host.transform);
            int calls = 0;
            fx.Play(host.transform, () => calls++, null, true);
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(ChallengeVideoTransition.IsCoveringGame, Is.False);
            Assert.That(fx.GetComponentsInChildren<Text>(true).All(t => t.text == "返回大厅"), Is.True);
        }

        [Ignore("Quarantined 2026-11-08: record transition replaced by the vinyl stage map (11-02); see Docs/TEST-QUARANTINE-1108.md")]
        [UnityTest] public IEnumerator CancelledObjectAndDisabledComponentCanResumeOnTheirActualUpdate()
        {
            for (int mode = 0; mode < 2; mode++)
            {
                var fx = ChallengeVideoTransition.Warm(host.transform);
                int cancelledCalls = 0, resumedCalls = 0;
                fx.Play(host.transform, () => cancelledCalls++, null);
                if (mode == 0) fx.gameObject.SetActive(false); else fx.enabled = false;
                Assert.That(fx.IsPlaying, Is.False);
                Assert.That(ChallengeVideoTransition.IsCoveringGame, Is.False);
                fx.Advance(2);
                Assert.That(cancelledCalls, Is.Zero);
                fx.Play(host.transform, () => resumedCalls++, null);
                Assert.That(fx.gameObject.activeInHierarchy && fx.enabled, Is.True);
                Assert.That(fx.IsPlaying && ChallengeVideoTransition.IsCoveringGame, Is.True);
                yield return null;
                yield return null;
                Assert.That(fx.Clock, Is.GreaterThan(0), "Reusing a cancelled instance must restart Unity Update, not register a permanently stuck cover");
                fx.Advance(2);
                fx.Play(host.transform, () => resumedCalls += 100, null);
                Assert.That(resumedCalls, Is.EqualTo(1), "The completed instance cannot dispatch again before deferred Destroy");
                Assert.That(cancelledCalls, Is.Zero);
                Assert.That(ChallengeVideoTransition.IsCoveringGame, Is.False);
                yield return null;
            }
        }

        [Test] public void InactiveDestinationCompletesImmediatelyOnceWithoutRegisteringAStuckCover()
        {
            var fx = ChallengeVideoTransition.Warm(host.transform);
            host.SetActive(false);
            int calls = 0;
            fx.Play(host.transform, () => calls++, null);
            fx.Play(host.transform, () => calls++, null);
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(fx.IsPlaying, Is.False);
            Assert.That(ChallengeVideoTransition.IsCoveringGame, Is.False);
        }

        [Test] public void CancellingARealHomeEntryEnumeratorClearsItsOpeningStateWithoutWaitingForDestroy()
        {
            bool previousActive = PsdHomeStageBridge.Active, previousHidden = PsdHomeStageBridge.UiHidden;
            var inactiveApp = new GameObject("CancelledHomeEntryApp"); inactiveApp.SetActive(false);
            try
            {
                var app = inactiveApp.AddComponent<ChoSirenApp>();
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var home = new GameObject("CancelledHomeEntryArt", typeof(RectTransform)); home.transform.SetParent(host.transform, false);
                typeof(ChoSirenApp).GetField("safeRoot", flags).SetValue(app, host.transform);
                typeof(ChoSirenApp).GetField("psdHome", flags).SetValue(app, home);
                var routine = (IEnumerator)typeof(ChoSirenApp).GetMethod("OpenPsdHomeAfterMotion", flags).Invoke(app, new object[] { "LiveOnStage" });
                Assert.That(routine.MoveNext(), Is.True);
                var fx = host.GetComponentInChildren<ChallengeVideoTransition>(true);
                Assert.That(fx.IsPlaying, Is.True);
                fx.gameObject.SetActive(false);
                Assert.That(routine.MoveNext(), Is.False, "Cancellation ends the home wait even while its transition object still exists");
                Assert.That(typeof(ChoSirenApp).GetField("psdOpening", flags).GetValue(app), Is.False);
                Assert.That(fx, Is.Not.Null);
                Assert.That(ChallengeVideoTransition.IsCoveringGame, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(inactiveApp);
                PsdHomeStageBridge.SetActive(previousActive); PsdHomeStageBridge.SetUiHidden(previousHidden);
            }
        }

        [Test] public void ReturnCaptionStaysSmallAndFitsFourChineseCharacters()
        {
            var fx = ChallengeVideoTransition.Warm(host.transform);
            fx.Play(host.transform, null, null, true);
            foreach (var title in fx.GetComponentsInChildren<Text>(true))
            {
                Assert.That(title.text, Is.EqualTo("返回大厅"));
                Assert.That(title.fontSize, Is.InRange(36, 42));
                Assert.That(title.rectTransform.rect.height, Is.LessThan(80));
                Assert.That(title.preferredWidth, Is.LessThan(title.rectTransform.rect.width));
                Assert.That(title.resizeTextForBestFit, Is.True);
            }
        }
    }
}
