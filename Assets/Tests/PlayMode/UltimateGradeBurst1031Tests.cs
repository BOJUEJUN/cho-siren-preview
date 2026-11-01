using System.Collections;
using ChoSiren.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ChoSiren.Tests
{
    public sealed class UltimateGradeBurst1031Tests
    {
        private GameObject canvas;
        private RectTransform layer, show;
        private bool reduceBefore;

        [SetUp]
        public void SetUp()
        {
            reduceBefore = BattlePresentationOptions.ReduceMotion;
            canvas = new GameObject("BurstCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            layer = (RectTransform)canvas.transform;
            show = new GameObject("Show", typeof(RectTransform)).GetComponent<RectTransform>();
            show.SetParent(layer, false);
        }

        [TearDown]
        public void TearDown()
        {
            BattlePresentationOptions.ReduceMotion = reduceBefore;
            Object.Destroy(canvas);
        }

        [UnityTest]
        public IEnumerator PerfectBurstsShakesThenRestoresAndCleansUp()
        {
            BattlePresentationOptions.ReduceMotion = false;
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var burst = UltimateGradeBurst1031.Play(layer, font, UltimateGradeTier1031.Perfect, () => false, show);
            Assert.That(burst, Is.Not.Null);
            Assert.That(burst.transform.Find("Centre/Bang"), Is.Not.Null, "PERFECT!! carries its two marks");
            Assert.That(burst.transform.Find("Centre/Rays").childCount, Is.GreaterThan(8));
            yield return new WaitForSecondsRealtime(.08f);
            Assert.That(burst.transform.Find("Flash").GetComponent<Image>().color.a, Is.GreaterThan(.1f));
            Assert.That(show.anchoredPosition, Is.Not.EqualTo(Vector2.zero), "PERFECT shakes the show");
            yield return new WaitForSecondsRealtime(1.4f);
            Assert.That(burst == null, Is.True, "the burst removes itself");
            Assert.That(show.anchoredPosition, Is.EqualTo(Vector2.zero));
        }

        [UnityTest]
        public IEnumerator ReducedMotionHasNoFlashShakeOrRaysAndPauseFreezes()
        {
            BattlePresentationOptions.ReduceMotion = true;
            bool paused = false; int interrupted = 0;
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var burst = UltimateGradeBurst1031.Play(layer, font, UltimateGradeTier1031.Perfect, () => paused, show, () => interrupted++);
            yield return new WaitForSecondsRealtime(.08f);
            Assert.That(burst.transform.Find("Flash").GetComponent<Image>().color.a, Is.EqualTo(0f));
            Assert.That(burst.transform.Find("Centre/Rays").childCount, Is.Zero);
            Assert.That(show.anchoredPosition, Is.EqualTo(Vector2.zero));
            paused = true;
            yield return new WaitForSecondsRealtime(1.5f);
            Assert.That(burst != null, Is.True, "a paused burst waits");
            Assert.That(interrupted, Is.EqualTo(1), "pausing stops the announcer once");
            paused = false;
            yield return new WaitForSecondsRealtime(1.4f);
            Assert.That(burst == null, Is.True);
        }

        [UnityTest]
        public IEnumerator MissIsASmallGreyDropWithoutFlash()
        {
            BattlePresentationOptions.ReduceMotion = false;
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var burst = UltimateGradeBurst1031.Play(layer, font, UltimateGradeTier1031.Miss, () => false, show);
            yield return new WaitForSecondsRealtime(.06f);
            Assert.That(burst.transform.Find("Flash").GetComponent<Image>().color.a, Is.EqualTo(0f));
            Assert.That(burst.transform.Find("Centre/Sparkles").childCount, Is.Zero);
            Assert.That(show.anchoredPosition, Is.EqualTo(Vector2.zero));
        }
    }
}
