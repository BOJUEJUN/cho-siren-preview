using NUnit.Framework;
using UnityEngine;

namespace ChoSiren.Tests
{
    /// <summary>Explicit-time component checks; no editor/runtime boot or scene assets required.</summary>
    public sealed class LobbyNavigationDisc1019Tests
    {
        private GameObject host;
        private bool savedMotion, hadMotionPreference;
        private int savedPreference;

        [SetUp]
        public void SetUp()
        {
            hadMotionPreference = PlayerPrefs.HasKey(BattlePresentationOptions.ReduceMotionKey);
            savedPreference = PlayerPrefs.GetInt(BattlePresentationOptions.ReduceMotionKey, 0);
            savedMotion = BattlePresentationOptions.ReduceMotion;
            BattlePresentationOptions.ReduceMotion = false;
            host = new GameObject("NavigationDiscTests", typeof(RectTransform));
        }

        [TearDown]
        public void TearDown()
        {
            if (host != null) Object.DestroyImmediate(host);
            BattlePresentationOptions.ReduceMotion = savedMotion;
            if (hadMotionPreference) PlayerPrefs.SetInt(BattlePresentationOptions.ReduceMotionKey, savedPreference);
            else PlayerPrefs.DeleteKey(BattlePresentationOptions.ReduceMotionKey);
        }

        [Test]
        public void OneShortTurnReachesItsExactTargetAndDoesNotKeepSpinning()
        {
            var disc = Disc("OneTurn", out RectTransform art);
            float remembered = -1; int calls = 0;
            disc.Configure(art, 15, 75, value => { remembered = value; calls++; });
            Assert.That(disc.Angle, Is.EqualTo(15)); Assert.That(disc.Spinning, Is.True);
            disc.Advance(.3f);
            Assert.That(disc.Angle, Is.GreaterThan(15).And.LessThan(75));
            disc.Advance(.38f);
            Assert.That(disc.Angle, Is.EqualTo(75).Within(.0001f));
            Assert.That(remembered, Is.EqualTo(75).Within(.0001f));
            Assert.That(Mathf.DeltaAngle(art.localEulerAngles.z, 75), Is.EqualTo(0).Within(.001f));
            Assert.That(disc.Spinning, Is.False);
            int settledCalls = calls; disc.Advance(10);
            Assert.That(calls, Is.EqualTo(settledCalls), "there is no idle rotation after a selection");
        }

        [Test]
        public void ReduceMotionSettlesImmediatelyAndCanBeEnabledMidTurn()
        {
            var disc = Disc("Reduced", out RectTransform art);
            float remembered = 0;
            disc.Configure(art, 0, 60, value => remembered = value); disc.Advance(.1f);
            Assert.That(disc.Angle, Is.GreaterThan(0).And.LessThan(60));
            BattlePresentationOptions.ReduceMotion = true; disc.Advance(.01f);
            Assert.That(disc.Spinning, Is.False); Assert.That(disc.Angle, Is.EqualTo(60));
            disc.Configure(art, 60, 120, value => remembered = value);
            Assert.That(disc.Spinning, Is.False); Assert.That(remembered, Is.EqualTo(120));
            Assert.That(Mathf.DeltaAngle(art.localEulerAngles.z, 120), Is.EqualTo(0).Within(.001f));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void InvalidTimeDoesNotChangePoseOrNotifyTheOwner(float delta)
        {
            var disc = Disc("InvalidDelta", out RectTransform art);
            int calls = 0; disc.Configure(art, 23, 83, _ => calls++);
            int configuredCalls = calls;
            disc.Advance(delta);
            Assert.That(disc.Angle, Is.EqualTo(23)); Assert.That(disc.Spinning, Is.True);
            Assert.That(calls, Is.EqualTo(configuredCalls));
        }

        [Test]
        public void RapidSelectionsContinueFromTheLastRenderedPose()
        {
            var disc = Disc("Rapid", out RectTransform art);
            float remembered = 0, target = 60;
            disc.Configure(art, remembered, target, value => remembered = value);
            for (int i = 0; i < 5; i++)
            {
                disc.Advance(.08f); float visible = remembered; target = visible + 60;
                disc.Configure(art, visible, target, value => remembered = value);
                Assert.That(disc.Angle, Is.EqualTo(visible), "retargeting cannot snap back to an old origin");
            }
            disc.Advance(1);
            Assert.That(disc.Angle, Is.EqualTo(target).Within(.0001f));
            Assert.That(remembered, Is.EqualTo(target).Within(.0001f));
            Assert.That(disc.Spinning, Is.False);
        }

        [Test]
        public void DisableAndReenableKeepTheCurrentPoseAndRemainingTarget()
        {
            var disc = Disc("Disabled", out RectTransform art);
            disc.Configure(art, 350, 410, null); disc.Advance(.2f);
            float pose = disc.Angle;
            disc.gameObject.SetActive(false);
            Assert.That(disc.Angle, Is.EqualTo(pose));
            disc.gameObject.SetActive(true); disc.Advance(.48f);
            Assert.That(disc.Angle, Is.EqualTo(410).Within(.0001f));
            Assert.That(Mathf.DeltaAngle(art.localEulerAngles.z, 50), Is.EqualTo(0).Within(.001f));
        }

        [Test]
        public void RebuildingAfterASelectionPreservesTheOwnerRecordedAngle()
        {
            float ownerAngle = 0;
            var previous = Disc("Previous", out RectTransform previousArt);
            previous.Configure(previousArt, 0, 60, value => ownerAngle = value); previous.Advance(.2f);
            float visible = ownerAngle;
            previous.gameObject.SetActive(false); Object.DestroyImmediate(previous.gameObject);
            var next = Disc("Next", out RectTransform nextArt);
            next.Configure(nextArt, ownerAngle, ownerAngle + 60, value => ownerAngle = value);
            Assert.That(next.Angle, Is.EqualTo(visible));
            next.Advance(1);
            Assert.That(ownerAngle, Is.EqualTo(visible + 60).Within(.0001f));
        }

        [Test]
        public void ARemovedRecordCannotReceiveFurtherPoseUpdates()
        {
            var disc = Disc("SeparateOwner", out RectTransform ownArt);
            var record = new GameObject("ExternalRecord", typeof(RectTransform)); record.transform.SetParent(host.transform, false);
            int calls = 0; disc.Configure((RectTransform)record.transform, 0, 60, _ => calls++);
            Object.DestroyImmediate(record); int before = calls;
            Assert.DoesNotThrow(() => disc.Advance(.5f));
            Assert.That(calls, Is.EqualTo(before), "destroyed art cannot deliver stale angle callbacks");
        }

        private LobbyNavigationDisc1019 Disc(string name, out RectTransform art)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(host.transform, false);
            art = (RectTransform)go.transform;
            return go.AddComponent<LobbyNavigationDisc1019>();
        }
    }
}
