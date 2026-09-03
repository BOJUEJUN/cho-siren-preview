using System;
using System.Collections.Generic;
using ChoSiren.Systems.Economy;

namespace ChoSiren.Systems.Gacha
{
    public static class GachaRarity
    {
        public const string Ssr = "SSR";
        public const string Sr = "SR";
        public const string R = "R";

        public static int Rank(string rarity)
        {
            switch (rarity)
            {
                case Ssr: return 3;
                case Sr: return 2;
                case R: return 1;
                default: return 0;
            }
        }
    }

    public static class GachaBannerKind
    {
        /// <summary>Pulls characters (BD2/NIKKE character banner).</summary>
        public const string Character = "character";
        /// <summary>Pulls costumes that unlock a new skin/skill for an owned character (BD2 costume banner).</summary>
        public const string Costume = "costume";
    }

    /// <summary>
    /// One banner. Rates are permille (‰) integers so the numbers shown to players in the
    /// "概率公示" screen are exactly the numbers the engine uses.
    /// </summary>
    [Serializable]
    public sealed class GachaBannerDefinition
    {
        public string Id = string.Empty;
        public string Name = string.Empty;
        public string Kind = GachaBannerKind.Character;

        public string CostCurrency = CurrencyIds.Diamond;
        public int CostPerPull = 150;
        public int CostTenPull = 1500;
        /// <summary>Optional ticket that substitutes for the currency one pull at a time.</summary>
        public string TicketCurrency = CurrencyIds.RecruitTicket;

        /// <summary>Base SSR chance per pull (‰). 30 = 3%.</summary>
        public int SsrRatePermille = 30;
        /// <summary>SR chance per pull (‰). Remainder is R.</summary>
        public int SrRatePermille = 185;

        /// <summary>Pull number at which the SSR chance starts climbing (inclusive).</summary>
        public int SoftPityStart = 60;
        /// <summary>Extra SSR chance (‰) added for every pull past <see cref="SoftPityStart"/>.</summary>
        public int SoftPityStepPermille = 60;
        /// <summary>Pull number at which an SSR is forced.</summary>
        public int HardPity = 80;

        /// <summary>Share of SSR pulls that land on the featured items (‰). 500 = 50/50.</summary>
        public int RateUpSharePermille = 500;
        /// <summary>大保底: after losing the 50/50 once, the next SSR is guaranteed featured.</summary>
        public bool GuaranteeFeaturedAfterLoss = true;
        /// <summary>Every 10-pull contains at least one SR-or-better.</summary>
        public bool TenPullGuaranteesSr = true;

        public List<string> FeaturedItemIds = new List<string>();
        public List<string> StandardSsrItemIds = new List<string>();
        public List<string> SrItemIds = new List<string>();
        public List<string> RItemIds = new List<string>();

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(Name))
            {
                error = "卡池缺少 ID 或名称";
                return false;
            }

            if (Kind != GachaBannerKind.Character && Kind != GachaBannerKind.Costume)
            {
                error = $"卡池 {Id} 的类型无效：{Kind}";
                return false;
            }

            if (!CurrencyIds.IsKnown(CostCurrency) || CostPerPull <= 0 || CostTenPull <= 0 ||
                CostTenPull > CostPerPull * 10)
            {
                error = $"卡池 {Id} 的消耗设置无效";
                return false;
            }

            if (SsrRatePermille <= 0 || SrRatePermille < 0 || SsrRatePermille + SrRatePermille >= 1000)
            {
                error = $"卡池 {Id} 的稀有度概率无效";
                return false;
            }

            if (HardPity <= 0 || SoftPityStart <= 0 || SoftPityStart > HardPity || SoftPityStepPermille < 0)
            {
                error = $"卡池 {Id} 的保底设置无效";
                return false;
            }

            if (RateUpSharePermille < 0 || RateUpSharePermille > 1000)
            {
                error = $"卡池 {Id} 的 UP 占比无效";
                return false;
            }

            if (FeaturedItemIds.Count == 0 && StandardSsrItemIds.Count == 0)
            {
                error = $"卡池 {Id} 没有任何 SSR 奖品";
                return false;
            }

            if (SrRatePermille > 0 && SrItemIds.Count == 0)
            {
                error = $"卡池 {Id} 有 SR 概率但没有 SR 奖品";
                return false;
            }

            if (RItemIds.Count == 0)
            {
                error = $"卡池 {Id} 没有 R 奖品";
                return false;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (List<string> pool in new[] { FeaturedItemIds, StandardSsrItemIds, SrItemIds, RItemIds })
            {
                for (int index = 0; index < pool.Count; index++)
                {
                    if (string.IsNullOrWhiteSpace(pool[index]) || !seen.Add(pool[index]))
                    {
                        error = $"卡池 {Id} 的奖品 ID 为空或重复：{pool[index]}";
                        return false;
                    }
                }
            }

            error = string.Empty;
            return true;
        }

        /// <summary>SSR chance (‰) for the pull number <paramref name="pullNumber"/> (1-based).</summary>
        public int SsrChanceAt(int pullNumber)
        {
            if (pullNumber >= HardPity) return 1000;
            int extra = pullNumber >= SoftPityStart ? (pullNumber - SoftPityStart + 1) * SoftPityStepPermille : 0;
            return Math.Min(1000, SsrRatePermille + extra);
        }
    }

    [Serializable]
    public sealed class GachaManifest
    {
        public int SchemaVersion = 1;
        /// <summary>Shards granted when a pulled character is already owned, by rarity.</summary>
        public int DuplicateShardsSsr = 50;
        public int DuplicateShardsSr = 15;
        public int DuplicateShardsR = 5;
        public List<GachaBannerDefinition> Banners = new List<GachaBannerDefinition>();

        public bool TryValidate(out string error)
        {
            if (SchemaVersion != 1)
            {
                error = $"gacha.json 版本不支持：{SchemaVersion}";
                return false;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < Banners.Count; index++)
            {
                if (Banners[index] == null)
                {
                    error = $"第 {index + 1} 个卡池为空";
                    return false;
                }

                if (!Banners[index].TryValidate(out error)) return false;
                if (!ids.Add(Banners[index].Id))
                {
                    error = $"卡池 ID 重复：{Banners[index].Id}";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        public GachaBannerDefinition Find(string bannerId)
        {
            for (int index = 0; index < Banners.Count; index++)
                if (Banners[index].Id == bannerId) return Banners[index];
            return null;
        }

        public int DuplicateShards(string rarity)
        {
            switch (rarity)
            {
                case GachaRarity.Ssr: return DuplicateShardsSsr;
                case GachaRarity.Sr: return DuplicateShardsSr;
                default: return DuplicateShardsR;
            }
        }
    }

    /// <summary>Per-banner persisted counters. Lives in the save next to currencies.</summary>
    [Serializable]
    public sealed class GachaBannerState
    {
        public string BannerId = string.Empty;
        /// <summary>Pulls since the last SSR (0 right after an SSR).</summary>
        public int Pity;
        /// <summary>True when the previous SSR was not featured and 大保底 is armed.</summary>
        public bool FeaturedGuaranteed;
        public int TotalPulls;
        public int TotalSsr;
    }
}
