using System;
using System.Collections.Generic;

namespace ChoSiren.Systems.Gacha
{
    public sealed class GachaPullResult
    {
        public string ItemId;
        public string Rarity;
        public bool IsFeatured;
        /// <summary>Pity counter value that produced this pull (1-based pull number in the streak).</summary>
        public int PullNumber;
        public bool HitHardPity;
        public bool UpgradedByTenPullGuarantee;
        public bool ConsumedFeaturedGuarantee;
        /// <summary>Filled by <see cref="DuplicateConverter"/>: true when the player did not own the item yet.</summary>
        public bool IsNew;
        public int ShardReward;
    }

    /// <summary>
    /// Deterministic gacha resolution. Given the same banner, state and random source the
    /// output is identical, which is what allows a server to verify or replay client pulls.
    /// The engine never touches currencies; the caller checks and deducts cost first.
    /// </summary>
    public static class GachaEngine
    {
        public const int TenPullCount = 10;

        public static List<GachaPullResult> Pull(GachaBannerDefinition banner, GachaBannerState state,
            IRandomSource random, int count)
        {
            if (banner == null) throw new ArgumentNullException(nameof(banner));
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (random == null) throw new ArgumentNullException(nameof(random));
            if (count <= 0 || count > TenPullCount) throw new ArgumentOutOfRangeException(nameof(count));
            if (state.BannerId != banner.Id) throw new ArgumentException("卡池状态与卡池不匹配", nameof(state));

            var results = new List<GachaPullResult>(count);
            for (int index = 0; index < count; index++) results.Add(PullOnce(banner, state, random));

            if (count == TenPullCount && banner.TenPullGuaranteesSr) ApplyTenPullGuarantee(banner, random, results);
            return results;
        }

        private static GachaPullResult PullOnce(GachaBannerDefinition banner, GachaBannerState state,
            IRandomSource random)
        {
            state.Pity++;
            state.TotalPulls++;
            int pullNumber = state.Pity;

            var result = new GachaPullResult { PullNumber = pullNumber };
            int roll = random.NextPermille();
            int ssrChance = banner.SsrChanceAt(pullNumber);

            if (roll < ssrChance)
            {
                result.Rarity = GachaRarity.Ssr;
                result.HitHardPity = pullNumber >= banner.HardPity;
                ResolveSsr(banner, state, random, result);
                state.Pity = 0;
                state.TotalSsr++;
                return result;
            }

            // The SR band sits directly above the SSR band so soft pity never eats into SR odds.
            if (roll < ssrChance + banner.SrRatePermille && banner.SrItemIds.Count > 0)
            {
                result.Rarity = GachaRarity.Sr;
                result.ItemId = PickUniform(banner.SrItemIds, random);
                return result;
            }

            result.Rarity = GachaRarity.R;
            result.ItemId = PickUniform(banner.RItemIds, random);
            return result;
        }

        private static void ResolveSsr(GachaBannerDefinition banner, GachaBannerState state, IRandomSource random,
            GachaPullResult result)
        {
            bool hasFeatured = banner.FeaturedItemIds.Count > 0;
            bool hasStandard = banner.StandardSsrItemIds.Count > 0;

            bool featured;
            if (!hasFeatured) featured = false;
            else if (!hasStandard) featured = true;
            else if (state.FeaturedGuaranteed)
            {
                featured = true;
                result.ConsumedFeaturedGuarantee = true;
            }
            else featured = random.NextPermille() < banner.RateUpSharePermille;

            result.IsFeatured = featured;
            result.ItemId = PickUniform(featured ? banner.FeaturedItemIds : banner.StandardSsrItemIds, random);
            state.FeaturedGuaranteed = !featured && hasFeatured && banner.GuaranteeFeaturedAfterLoss;
        }

        private static void ApplyTenPullGuarantee(GachaBannerDefinition banner, IRandomSource random,
            List<GachaPullResult> results)
        {
            for (int index = 0; index < results.Count; index++)
                if (GachaRarity.Rank(results[index].Rarity) >= GachaRarity.Rank(GachaRarity.Sr)) return;

            if (banner.SrItemIds.Count == 0) return;
            GachaPullResult last = results[results.Count - 1];
            last.Rarity = GachaRarity.Sr;
            last.ItemId = PickUniform(banner.SrItemIds, random);
            last.UpgradedByTenPullGuarantee = true;
        }

        private static string PickUniform(List<string> pool, IRandomSource random) =>
            pool[random.Next(pool.Count)];
    }

    /// <summary>
    /// Turns duplicate pulls into shards. Ownership is a set of item ids supplied by the caller
    /// (character roster for character banners, costume list for costume banners).
    /// </summary>
    public static class DuplicateConverter
    {
        public static int Apply(GachaManifest manifest, ISet<string> owned, List<GachaPullResult> results)
        {
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            if (owned == null) throw new ArgumentNullException(nameof(owned));
            if (results == null) throw new ArgumentNullException(nameof(results));

            int totalShards = 0;
            for (int index = 0; index < results.Count; index++)
            {
                GachaPullResult result = results[index];
                if (owned.Add(result.ItemId))
                {
                    result.IsNew = true;
                    result.ShardReward = 0;
                    continue;
                }

                result.IsNew = false;
                result.ShardReward = manifest.DuplicateShards(result.Rarity);
                totalShards += result.ShardReward;
            }

            return totalShards;
        }
    }
}
