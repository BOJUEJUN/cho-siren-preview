using NUnit.Framework;
using UnityEngine;

namespace ChoSiren.Tests
{
    public sealed class BattleTempoReadability1019Tests
    {
        [TestCase(1, 1f, .3f)]
        [TestCase(2, .5f, .15f)]
        public void BasicAttackRemainsReadableAtOneTimesAndTwiceSpeedKeepsTheSameTimeline(
            int speed, float beforeFinishSeconds, float remainingSeconds)
        {
            bool hadReduce = PlayerPrefs.HasKey(BattlePresentationOptions.ReduceMotionKey);
            int oldReduce = PlayerPrefs.GetInt(BattlePresentationOptions.ReduceMotionKey);
            PlayerPrefs.SetInt(BattlePresentationOptions.ReduceMotionKey, 0);
            var root = new GameObject("TempoFixture", typeof(RectTransform));
            bool paused = false;
            try
            {
                var host = (RectTransform)root.transform; host.sizeDelta = new Vector2(720, 1502);
                var source = Point(root.transform, "Source", new Vector2(-180, -300));
                var target = Point(root.transform, "Target", new Vector2(180, 300));
                var cue = root.AddComponent<BattleBasicAttack1019>();
                cue.Configure(host, () => paused, () => speed);
                Assert.That(cue.Play(source, target, null, Color.cyan, BattleBasicForm1019.Fireball), Is.True);
                cue.AdvancePresentation(beforeFinishSeconds);
                Assert.That(cue.VisualClock, Is.EqualTo(.5f).Within(.001f));
                Assert.That(cue.ActiveCount, Is.EqualTo(1), "A one-times hit must remain visible for more than one real second.");
                paused = true; cue.AdvancePresentation(10);
                Assert.That(cue.VisualClock, Is.EqualTo(.5f).Within(.001f));
                Assert.That(cue.ActiveCount, Is.EqualTo(1));
                paused = false; cue.AdvancePresentation(remainingSeconds);
                Assert.That(cue.VisualClock, Is.EqualTo(.65f).Within(.001f));
                Assert.That(cue.ActiveCount, Is.Zero, "Slower playback must still retire the cue instead of piling up effects.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                if (hadReduce) PlayerPrefs.SetInt(BattlePresentationOptions.ReduceMotionKey, oldReduce);
                else PlayerPrefs.DeleteKey(BattlePresentationOptions.ReduceMotionKey);
            }
        }

        static RectTransform Point(Transform host, string name, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(host, false); rect.sizeDelta = new Vector2(100, 150); rect.anchoredPosition = position;
            return rect;
        }
    }
}
