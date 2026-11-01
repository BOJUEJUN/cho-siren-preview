using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ChoSiren.Tests
{
    public sealed class DiscSwipe1025Tests
    {
        GameObject root;
        DiscSwipe1025 swipe;
        int launches, cancels, thresholds;
        float lastLift;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Card", typeof(RectTransform));
            swipe = root.AddComponent<DiscSwipe1025>();
            launches = cancels = thresholds = 0; lastLift = -1f;
            swipe.CanSwipe = () => true;
            swipe.Lift = lift => lastLift = lift;
            swipe.ReachedThreshold = () => thresholds++;
            swipe.Launch = () => launches++;
            swipe.Cancel = () => cancels++;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        static PointerEventData Pointer(float x, float y) =>
            new PointerEventData(null) { pressPosition = Vector2.zero, position = new Vector2(x, y), eligibleForClick = true };

        [Test]
        public void UpwardDragPastTheThresholdLaunchesAndIsNotAlsoAClick()
        {
            var e = Pointer(0f, 20f);
            swipe.OnBeginDrag(e);
            e.position = new Vector2(0f, swipe.Threshold + 20f);
            swipe.OnDrag(e);
            swipe.OnEndDrag(e);
            Assert.That(launches, Is.EqualTo(1));
            Assert.That(cancels, Is.EqualTo(0));
            Assert.That(thresholds, Is.EqualTo(1));
            Assert.That(e.eligibleForClick, Is.False, "a swipe must not also count as a tap on the card");
        }

        [Test]
        public void ShortDragSpringsBack()
        {
            var e = Pointer(0f, 20f);
            swipe.OnBeginDrag(e);
            e.position = new Vector2(0f, swipe.Threshold * .5f);
            swipe.OnDrag(e);
            swipe.OnEndDrag(e);
            Assert.That(launches, Is.EqualTo(0));
            Assert.That(cancels, Is.EqualTo(1));
            Assert.That(lastLift, Is.EqualTo(swipe.Threshold * .5f).Within(.01f));
        }

        [Test]
        public void LiftIsRubberBandedPastTheThreshold()
        {
            var e = Pointer(0f, 20f);
            swipe.OnBeginDrag(e);
            e.position = new Vector2(0f, swipe.Threshold + 100f);
            swipe.OnDrag(e);
            Assert.That(lastLift, Is.GreaterThan(swipe.Threshold));
            Assert.That(lastLift, Is.LessThan(swipe.Threshold + 100f));
        }

        [Test]
        public void SidewaysDragsAndUnreadyCardsAreIgnored()
        {
            var sideways = Pointer(60f, 20f);
            swipe.OnBeginDrag(sideways);
            sideways.position = new Vector2(200f, 200f);
            swipe.OnDrag(sideways);
            swipe.OnEndDrag(sideways);
            Assert.That(launches + cancels, Is.EqualTo(0));
            Assert.That(sideways.eligibleForClick, Is.True, "an ignored drag leaves the tap alone");

            swipe.CanSwipe = () => false;
            var up = Pointer(0f, 30f);
            swipe.OnBeginDrag(up);
            up.position = new Vector2(0f, swipe.Threshold + 50f);
            swipe.OnDrag(up);
            swipe.OnEndDrag(up);
            Assert.That(launches + cancels, Is.EqualTo(0));
            Assert.That(lastLift, Is.EqualTo(-1f));
        }
    }
}
