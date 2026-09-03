using System;
using System.Collections.Generic;
using ChoSiren.Systems;
using ChoSiren.Systems.Gacha;
using NUnit.Framework;

namespace ChoSiren.Tests.Systems
{
    public sealed class GachaEngineTests
    {
        private static GachaBannerDefinition Banner() => new GachaBannerDefinition
        {
            Id = "debut",
            Name = "测试卡池",
            SsrRatePermille = 30,
            SrRatePermille = 185,
            SoftPityStart = 60,
            SoftPityStepPermille = 60,
            HardPity = 80,
            RateUpSharePermille = 500,
            GuaranteeFeaturedAfterLoss = true,
            TenPullGuaranteesSr = true,
            FeaturedItemIds = new List<string> { "up" },
            StandardSsrItemIds = new List<string> { "ssr-a", "ssr-b" },
            SrItemIds = new List<string> { "sr-a", "sr-b" },
            RItemIds = new List<string> { "r-a", "r-b", "r-c" }
        };

        private static GachaBannerState State() => new GachaBannerState { BannerId = "debut" };

        [Test]
        public void PublishedRatesMatchEngineBands()
        {
            GachaBannerDefinition banner = Banner();
            Assert.That(banner.SsrChanceAt(1), Is.EqualTo(30));
            Assert.That(banner.SsrChanceAt(59), Is.EqualTo(30));
            Assert.That(banner.SsrChanceAt(60), Is.EqualTo(90));
            Assert.That(banner.SsrChanceAt(70), Is.EqualTo(690));
            Assert.That(banner.SsrChanceAt(80), Is.EqualTo(1000));
            Assert.That(banner.TryValidate(out string error), Is.True, error);
        }

        [Test]
        public void RollBelowSsrBandIsSsrAndResetsPity()
        {
            GachaBannerState state = State();
            state.Pity = 10;
            // permille 5 -> SSR; featured roll 700 -> loses 50/50; pick index 1 -> ssr-b
            var random = new ScriptedRandom(new[] { 5, 700 }, new[] { 1 });

            List<GachaPullResult> results = GachaEngine.Pull(Banner(), state, random, 1);

            Assert.That(results[0].Rarity, Is.EqualTo(GachaRarity.Ssr));
            Assert.That(results[0].IsFeatured, Is.False);
            Assert.That(results[0].ItemId, Is.EqualTo("ssr-b"));
            Assert.That(results[0].PullNumber, Is.EqualTo(11));
            Assert.That(state.Pity, Is.Zero);
            Assert.That(state.FeaturedGuaranteed, Is.True, "歪了之后必须武装大保底");
        }

        [Test]
        public void GuaranteeForcesFeaturedOnNextSsr()
        {
            GachaBannerState state = State();
            state.FeaturedGuaranteed = true;
            // SSR roll; the featured roll must NOT be consumed when guaranteed.
            var random = new ScriptedRandom(new[] { 0, 999 }, new[] { 0 });

            GachaPullResult result = GachaEngine.Pull(Banner(), state, random, 1)[0];

            Assert.That(result.IsFeatured, Is.True);
            Assert.That(result.ItemId, Is.EqualTo("up"));
            Assert.That(result.ConsumedFeaturedGuarantee, Is.True);
            Assert.That(state.FeaturedGuaranteed, Is.False);
        }

        [Test]
        public void HardPityForcesSsrOnEightiethPull()
        {
            GachaBannerState state = State();
            state.Pity = 79;
            var random = new ScriptedRandom(new[] { 999, 0 }, new[] { 0 });

            GachaPullResult result = GachaEngine.Pull(Banner(), state, random, 1)[0];

            Assert.That(result.Rarity, Is.EqualTo(GachaRarity.Ssr));
            Assert.That(result.HitHardPity, Is.True);
            Assert.That(result.PullNumber, Is.EqualTo(80));
        }

        [Test]
        public void SrBandSitsAboveSsrBandAndRestIsR()
        {
            GachaBannerState state = State();
            var random = new ScriptedRandom(new[] { 30, 214, 215 }, new[] { 0 });

            List<GachaPullResult> first = GachaEngine.Pull(Banner(), state, random, 1);
            List<GachaPullResult> second = GachaEngine.Pull(Banner(), state, random, 1);
            List<GachaPullResult> third = GachaEngine.Pull(Banner(), state, random, 1);

            Assert.That(first[0].Rarity, Is.EqualTo(GachaRarity.Sr));
            Assert.That(second[0].Rarity, Is.EqualTo(GachaRarity.Sr));
            Assert.That(third[0].Rarity, Is.EqualTo(GachaRarity.R));
            Assert.That(state.Pity, Is.EqualTo(3));
        }

        [Test]
        public void TenPullUpgradesLastRWhenNothingAboveR()
        {
            GachaBannerState state = State();
            var random = new ScriptedRandom(new[] { 999 }, new[] { 2 });

            List<GachaPullResult> results = GachaEngine.Pull(Banner(), state, random, 10);

            Assert.That(results.Count, Is.EqualTo(10));
            for (int index = 0; index < 9; index++) Assert.That(results[index].Rarity, Is.EqualTo(GachaRarity.R));
            Assert.That(results[9].Rarity, Is.EqualTo(GachaRarity.Sr));
            Assert.That(results[9].UpgradedByTenPullGuarantee, Is.True);
            Assert.That(state.TotalPulls, Is.EqualTo(10));
        }

        [Test]
        public void SeededPullsAreReproducible()
        {
            List<GachaPullResult> a = GachaEngine.Pull(Banner(), State(), new SeededRandom(20260903), 10);
            List<GachaPullResult> b = GachaEngine.Pull(Banner(), State(), new SeededRandom(20260903), 10);

            for (int index = 0; index < 10; index++)
            {
                Assert.That(a[index].ItemId, Is.EqualTo(b[index].ItemId));
                Assert.That(a[index].Rarity, Is.EqualTo(b[index].Rarity));
            }
        }

        [Test]
        public void HardPityBoundsEverySsrDroughtInLongSimulation()
        {
            GachaBannerDefinition banner = Banner();
            GachaBannerState state = State();
            var random = new SeededRandom(42);
            int worstDrought = 0;
            int ssrCount = 0;
            for (int pull = 0; pull < 20000; pull++)
            {
                GachaPullResult result = GachaEngine.Pull(banner, state, random, 1)[0];
                if (result.Rarity != GachaRarity.Ssr) continue;
                ssrCount++;
                worstDrought = Math.Max(worstDrought, result.PullNumber);
            }

            Assert.That(worstDrought, Is.LessThanOrEqualTo(80));
            // With soft pity the consolidated SSR rate should land well above the base 3%.
            Assert.That(ssrCount, Is.GreaterThan(20000 * 30 / 1000));
            Assert.That(ssrCount, Is.LessThan(20000 * 60 / 1000));
        }

        [Test]
        public void DuplicateConverterMarksNewAndPaysShards()
        {
            var manifest = new GachaManifest { DuplicateShardsSsr = 50, DuplicateShardsSr = 15, DuplicateShardsR = 5 };
            var owned = new HashSet<string>(StringComparer.Ordinal) { "r-a" };
            var results = new List<GachaPullResult>
            {
                new GachaPullResult { ItemId = "up", Rarity = GachaRarity.Ssr },
                new GachaPullResult { ItemId = "up", Rarity = GachaRarity.Ssr },
                new GachaPullResult { ItemId = "r-a", Rarity = GachaRarity.R },
            };

            int shards = DuplicateConverter.Apply(manifest, owned, results);

            Assert.That(results[0].IsNew, Is.True);
            Assert.That(results[1].IsNew, Is.False);
            Assert.That(results[1].ShardReward, Is.EqualTo(50));
            Assert.That(results[2].ShardReward, Is.EqualTo(5));
            Assert.That(shards, Is.EqualTo(55));
            Assert.That(owned, Does.Contain("up"));
        }

        [Test]
        public void InvalidRequestsAreRejected()
        {
            GachaBannerDefinition banner = Banner();
            Assert.Throws<ArgumentOutOfRangeException>(() => GachaEngine.Pull(banner, State(), new SeededRandom(1), 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => GachaEngine.Pull(banner, State(), new SeededRandom(1), 11));
            Assert.Throws<ArgumentException>(() =>
                GachaEngine.Pull(banner, new GachaBannerState { BannerId = "other" }, new SeededRandom(1), 1));

            banner.RItemIds.Clear();
            Assert.That(banner.TryValidate(out string error), Is.False);
            Assert.That(error, Does.Contain("R 奖品"));
        }
    }
}
