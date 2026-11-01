using NUnit.Framework;

namespace ChoSiren.Tests
{
    public sealed class BattleSfxVoicePolicy1019Tests
    {
        [Test]
        public void SameNamedMultiTargetVolleyKeepsSeventyMillisecondGap()
        {
            var p = new BattleSfxVoicePolicy1019();
            Assert.That(p.TryReserve("hit", .31f, .8f, 1f, out _), Is.True);
            Assert.That(p.TryReserve("hit", .31f, .8f, 1.069f, out _), Is.False);
            Assert.That(p.TryReserve("hit", .31f, .8f, 1.07f, out _), Is.True);
            Assert.That(p.RejectedDuplicateCount, Is.EqualTo(1));
        }

        [Test]
        public void RepeatedImpactsUseAtMostTwoVoicesAndReplaceOldest()
        {
            var p = new BattleSfxVoicePolicy1019();
            p.TryReserve("hit", 2f, 1f, 0f, out var a);
            p.TryReserve("hit", 2f, 1f, .08f, out var b);
            p.TryReserve("hit", 2f, 1f, .16f, out var c);
            Assert.That(p.ActiveVoiceCount(.16f), Is.EqualTo(2));
            Assert.That(c.Channel, Is.EqualTo(a.Channel));
            Assert.That(c.Channel, Is.Not.EqualTo(b.Channel));
            Assert.That(c.ReplacesVoice, Is.True);
        }

        [Test]
        public void ImportantCastCanReplaceQuietEventButNoiseCannotStealImportantVoices()
        {
            var p = new BattleSfxVoicePolicy1019();
            string[] events = { "victory", "ko", "ultimate", "dice", "shield", "crit" };
            for (int i = 0; i < events.Length; i++) Assert.That(p.TryReserve(events[i], 2f, 1f, 0f, out _), Is.True);
            Assert.That(p.TryReserve("hit", .3f, 1f, .1f, out _), Is.False);
            Assert.That(p.RejectedPriorityCount, Is.EqualTo(1));
            Assert.That(p.TryReserve("cut", .3f, 1f, .1f, out var cut), Is.True);
            Assert.That(cut.Channel, Is.EqualTo(4), "The shield, not an ultimate or victory, is the lowest priority.");
            Assert.That(cut.ReplacesVoice, Is.True);
            Assert.That(p.ActiveVoiceCount(.1f), Is.EqualTo(6));
        }

        [Test]
        public void LongDenseBurstNeverExceedsSixIncludingDistinctNames()
        {
            var p = new BattleSfxVoicePolicy1019();
            string[] events = { "hit", "hurt", "block", "crit", "debuff", "heal", "shield", "dice", "ultimate", "ko", "victory", "cut", "stamp", "glitch" };
            for (int i = 0; i < 200; i++)
            {
                float now = i * .017f;
                p.TryReserve(events[i % events.Length], 1f, 1f, now, out _);
                Assert.That(p.ActiveVoiceCount(now), Is.LessThanOrEqualTo(6));
            }
        }

        [Test]
        public void FinishedClipsFreeAChannelWithoutEvictingAnotherSound()
        {
            var p = new BattleSfxVoicePolicy1019();
            p.TryReserve("hit", .1f, 1f, 0f, out var a);
            p.TryReserve("ultimate", 1f, 1f, .11f, out var b);
            Assert.That(b.Channel, Is.EqualTo(a.Channel));
            Assert.That(b.ReplacesVoice, Is.False);
            Assert.That(p.ActiveVoiceCount(2f), Is.EqualTo(0));
        }

        [Test]
        public void PitchVariationIsSmallDeterministicAndDoesNotAffectMusicOrSpeech()
        {
            var p = new BattleSfxVoicePolicy1019();
            var other = new BattleSfxVoicePolicy1019();
            for (int i = 0; i < 12; i++)
            {
                p.TryReserve("hit", .1f, 1f, i, out var a);
                other.TryReserve("hit", .1f, 1f, i, out var b);
                Assert.That(a.Pitch, Is.EqualTo(b.Pitch));
                Assert.That(a.Pitch, Is.InRange(.95f, 1.05f)); // widened to ±4.5% (2026-10-21)
            }
            p.TryReserve("ultimate", .1f, 1f, 20f, out var cast);
            p.TryReserve("unrecognized-effect", .1f, 1f, 21f, out var unknown);
            Assert.That(cast.Pitch, Is.EqualTo(1f));
            Assert.That(unknown.Pitch, Is.EqualTo(1f));
        }

        [Test]
        public void InvalidOrSilentRequestsDoNotConsumeDeduplicationOrChannelBudget()
        {
            var p = new BattleSfxVoicePolicy1019();
            Assert.That(p.TryReserve("hit", .31f, 0f, 0f, out _), Is.False);
            Assert.That(p.TryReserve("hit", 0f, 1f, 0f, out _), Is.False);
            Assert.That(p.TryReserve("hit", .31f, 1f, float.NaN, out _), Is.False);
            Assert.That(p.TryReserve("hit", .31f, float.PositiveInfinity, 0f, out _), Is.False);
            Assert.That(p.ActiveVoiceCount(0f), Is.Zero);
            Assert.That(p.TryReserve("hit", .31f, .8f, 0f, out var voice), Is.True);
            Assert.That(voice.Gain, Is.EqualTo(.8f));
        }

        [Test]
        public void ClearStopsAllReservationsAndAllowsImmediateReplay()
        {
            var p = new BattleSfxVoicePolicy1019();
            p.TryReserve("dice", .75f, 1f, 0f, out _);
            p.Clear();
            Assert.That(p.ActiveVoiceCount(0f), Is.Zero);
            Assert.That(p.TryReserve("dice", .75f, 1f, .01f, out _), Is.True);
        }
    }
}
