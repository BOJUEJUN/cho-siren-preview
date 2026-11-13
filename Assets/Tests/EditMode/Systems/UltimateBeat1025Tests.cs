using ChoSiren.Systems.Tactics;
using NUnit.Framework;

namespace ChoSiren.Tests
{
    /// <summary>2026-10-25: the ultimate tap judgment is one approach ring with wide windows.</summary>
    public sealed class UltimateBeat1025Tests
    {
        private static UltimateBeatChallenge1025 TapAt(float offset)
        {
            var beat = new UltimateBeatChallenge1025();
            beat.Advance(UltimateBeatChallenge1025.BeatTime(0) + offset);
            Assert.That(beat.Hit(), Is.EqualTo(0));
            return beat;
        }

        [Test]
        public void OneRing() => Assert.That(UltimateBeatChallenge1025.Beats, Is.EqualTo(1));

        [TestCase(0f, UltimateTimingGrade.Perfect)]
        [TestCase(.09f, UltimateTimingGrade.Perfect)]
        [TestCase(-.09f, UltimateTimingGrade.Perfect)]
        [TestCase(.18f, UltimateTimingGrade.Good)]
        [TestCase(-.22f, UltimateTimingGrade.Good)]
        [TestCase(.13f, UltimateTimingGrade.Perfect)]
        [TestCase(.3f, UltimateTimingGrade.Good)]
        [TestCase(-.4f, UltimateTimingGrade.Miss)]
        public void WideWindows(float offset, UltimateTimingGrade expected)
        {
            var beat = TapAt(offset);
            Assert.That(beat.Resolved, Is.True);
            Assert.That(beat.Grade, Is.EqualTo(expected));
        }

        [Test]
        public void TouchLatencyStillScoresGood()
        {
            // 200 ms late (slow phone / WebGL input) is GOOD, not MISS.
            Assert.That(TapAt(.2f).Grade, Is.EqualTo(UltimateTimingGrade.Good));
        }

        [Test]
        public void VeryEarlyTapsAreIgnored()
        {
            var beat = new UltimateBeatChallenge1025();
            beat.Advance(.1f);
            Assert.That(beat.Hit(), Is.EqualTo(-1), "a tap long before the ring lands does nothing");
            Assert.That(beat.Resolved, Is.False);
        }

        [Test]
        public void UntappedRingMissesAndFinishResolves()
        {
            var beat = new UltimateBeatChallenge1025();
            beat.Advance(UltimateBeatChallenge1025.DurationSeconds + .05f);
            Assert.That(beat.Resolved, Is.True);
            Assert.That(beat.Grade, Is.EqualTo(UltimateTimingGrade.Miss));
            Assert.That(beat.Hit(), Is.EqualTo(-1));
            var other = new UltimateBeatChallenge1025();
            other.Finish();
            Assert.That(other.Resolved, Is.True);
        }
    }
}
