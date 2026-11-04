using System;
using System.Collections.Generic;
using System.Linq;
using ChoSiren.Systems;
using ChoSiren.Systems.Economy;
using ChoSiren.Systems.Tactics;
using UnityEngine;

namespace ChoSiren
{
    public sealed partial class GameSave
    {
        /// <summary>2026-11-04: breakthroughs done per member (indexed like MemberLevels). Missing = none.</summary>
        public List<int> MemberBreaks1104 = new List<int>();
        /// <summary>声纹碎晶 (breakthrough material from elite and boss stages).</summary>
        public int VoiceCrystals;
        /// <summary>声核 (rare breakthrough material from bosses, or 50 shards each).</summary>
        public int VoiceCores;
    }

    /// <summary>
    /// 2026-11-04 数值方案 (赵金 1104: "只靠钱币升级可以一直升级…关卡角色就变很弱"): training is capped by stage progress.
    /// The cap starts at Lv12 and every fifth stage (1-5, 1-10, 2-5, 2-10 …) cleared in order raises it by 5. Passing
    /// Lv17, Lv27, Lv37 … also needs a per-member 突破 paid in 声纹碎晶 / 声核 / gold. Levels already earned are never
    /// taken away: a member above the cap keeps her level and only stops training until progress catches up.
    /// The cap leaves 1–3 levels over each boss's recommended level (1-5 Lv10, 1-10 Lv16, 2-5 Lv20, 2-10 Lv24).
    /// </summary>
    public sealed partial class GameModel
    {
        public const int BaseLevelCap1104 = 12;
        public const int LevelCapStep1104 = 5;
        public const int FirstBreakLevel1104 = 17;
        public const int BreakSpacing1104 = 10;
        public const int ShardsPerCore1104 = 50;

        /// <summary>The gate stages in play order: every stage whose number is a multiple of five.</summary>
        private IEnumerable<StageDefinition> GateStages1104() => tactics.Stages.Where(s => StageNumber1104(s.Id) % 5 == 0);

        private static int StageNumber1104(string stageId)
        {
            string[] parts = (stageId ?? string.Empty).Split('-');
            return parts.Length >= 3 && int.TryParse(parts[parts.Length - 1], out int n) ? n : 0;
        }

        /// <summary>Gates cleared in order (an out-of-order clear does not skip a gate).</summary>
        public int GatesCleared1104
        {
            get
            {
                int count = 0;
                foreach (StageDefinition gate in GateStages1104())
                {
                    if (!IsStageCleared(gate.Id)) break;
                    count++;
                }
                return count;
            }
        }

        /// <summary>The stage-progress level cap shared by every member.</summary>
        public int ProgressLevelCap1104 => Math.Min(MaxMemberLevel, BaseLevelCap1104 + LevelCapStep1104 * GatesCleared1104);

        /// <summary>Short name of the next gate ("1-5"), or empty once every gate is cleared.</summary>
        public string NextGateName1104
        {
            get
            {
                StageDefinition next = GateStages1104().FirstOrDefault(s => !IsStageCleared(s.Id));
                return next == null ? string.Empty : next.Id.Replace("stage-", string.Empty);
            }
        }

        /// <summary>Breakthroughs counted for a member: the saved count, or what her current level already implies.</summary>
        public int BreaksOf1104(int member)
        {
            if (!IsValidMemberIndex(member)) return 0;
            int saved = Save.MemberBreaks1104 != null && member < Save.MemberBreaks1104.Count ? Save.MemberBreaks1104[member] : 0;
            int level = LevelOf(member);
            int implied = level <= FirstBreakLevel1104 ? 0 : (level - FirstBreakLevel1104 + BreakSpacing1104 - 1) / BreakSpacing1104;
            return Math.Max(saved, implied);
        }

        /// <summary>The level a member must break through at next.</summary>
        public int BreakLevelOf1104(int member) => FirstBreakLevel1104 + BreakSpacing1104 * BreaksOf1104(member);

        /// <summary>The highest level this member can train to right now.</summary>
        public int LevelCapOf1104(int member) =>
            Math.Min(ProgressLevelCap1104, Math.Min(MaxMemberLevel, BreakLevelOf1104(member)));

        /// <summary>True when she sits at her breakthrough level and stage progress already allows more.</summary>
        public bool NeedsBreakthrough1104(int member)
        {
            if (!IsUnlocked(member)) return false;
            int level = LevelOf(member);
            return level < MaxMemberLevel && level >= BreakLevelOf1104(member) && ProgressLevelCap1104 > level;
        }

        public readonly struct BreakCost1104
        {
            public readonly int Crystals, Cores, Gold;
            public BreakCost1104(int crystals, int cores, int gold) { Crystals = crystals; Cores = cores; Gold = gold; }
        }

        /// <summary>Cost of breakthrough number <paramref name="tier"/> (1 = the Lv17 one).</summary>
        public static BreakCost1104 BreakCostFor1104(int tier)
        {
            switch (Math.Max(1, tier))
            {
                case 1: return new BreakCost1104(10, 0, 3000);
                case 2: return new BreakCost1104(20, 2, 6000);
                case 3: return new BreakCost1104(0, 4, 15000);
                default:
                    int scale = 1 << Math.Min(6, tier - 3);
                    return new BreakCost1104(0, 4 * scale, 15000 * scale);
            }
        }

        public BreakCost1104 NextBreakCost1104(int member) => BreakCostFor1104(BreaksOf1104(member) + 1);

        /// <summary>Cores still missing after using owned ones; shards cover them at 50 each.</summary>
        private int ShardsNeededForCores1104(int cores) => Math.Max(0, cores - Save.VoiceCores) * ShardsPerCore1104;

        public bool CanBreakthrough1104(int member, out BreakCost1104 cost, out string message)
        {
            cost = NextBreakCost1104(member);
            if (!IsValidMemberIndex(member) || !IsUnlocked(member)) { message = "尚未签约该成员"; return false; }
            if (!NeedsBreakthrough1104(member)) { message = "现在不需要突破"; return false; }
            if (Save.VoiceCrystals < cost.Crystals) { message = $"声纹碎晶不足（{Save.VoiceCrystals}/{cost.Crystals}）"; return false; }
            if (ShardsNeededForCores1104(cost.Cores) > Save.Shards)
            {
                message = $"声核不足（{Save.VoiceCores}/{cost.Cores}，碎片 {ShardsPerCore1104} 个可换 1 个）";
                return false;
            }
            if (Save.Gold < cost.Gold) { message = $"星光币不足，突破需要 {cost.Gold:N0}"; return false; }
            message = string.Empty;
            return true;
        }

        public bool Breakthrough1104(int member, out string message)
        {
            if (!CanBreakthrough1104(member, out BreakCost1104 cost, out message)) return false;
            int shards = ShardsNeededForCores1104(cost.Cores);
            Save.Shards -= shards;
            Save.VoiceCores = Math.Max(0, Save.VoiceCores - (cost.Cores - shards / ShardsPerCore1104));
            Save.VoiceCrystals -= cost.Crystals;
            Save.Gold -= cost.Gold;
            Save.MemberBreaks1104 ??= new List<int>();
            while (Save.MemberBreaks1104.Count <= member) Save.MemberBreaks1104.Add(0);
            Save.MemberBreaks1104[member] = BreaksOf1104(member) + 1;
            SaveState();
            message = $"{Members[member].Name} 突破成功，等级上限提升到 {LevelCapOf1104(member)}";
            if (shards > 0) message += $"（用 {shards} 碎片兑换了声核）";
            return true;
        }

        /// <summary>Why training stops at the cap, for the practice page.</summary>
        public string LevelCapReason1104(int member)
        {
            if (NeedsBreakthrough1104(member)) return "需要突破";
            string gate = NextGateName1104;
            return string.IsNullOrEmpty(gate) ? "已达当前上限" : $"通关 {gate} 解锁";
        }

        /// <summary>
        /// Breakthrough materials a victory (or sweep run) pays: elite 2–3 声纹碎晶, boss 3 碎晶, a boss's first clear
        /// 1 声核 (3 on a chapter's tenth stage), a boss replay 1 声核 one time in ten.
        /// </summary>
        private void AddMaterialRewards1104(StageDefinition stage, bool firstClear, ulong seed, List<(string ItemId, int Amount)> rewards)
        {
            if (stage == null) return;
            var random = new SeededRandom(seed ^ 0x5EEDC0DEUL);
            string kind = stage.EncounterType ?? string.Empty;
            if (kind == "elite") rewards.Add((CurrencyIds.VoiceCrystal, 2 + (random.NextPermille() < 500 ? 1 : 0)));
            else if (kind == "boss")
            {
                rewards.Add((CurrencyIds.VoiceCrystal, 3));
                if (firstClear) rewards.Add((CurrencyIds.VoiceCore, StageNumber1104(stage.Id) % 10 == 0 ? 3 : 1));
                else if (random.NextPermille() < 100) rewards.Add((CurrencyIds.VoiceCore, 1));
            }
        }
    }
}
