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
        /// <summary>2026-11-07: breakthroughs on the Lv30/40/… ladder per member (1104 counts used 17/27/…).</summary>
        public List<int> MemberBreaks1107 = new List<int>();
        /// <summary>敌人残魂 (was 声纹碎晶; same save field): per-level material from Lv30.</summary>
        public int VoiceCrystals;
        /// <summary>通用生魂 (was 声核; same save field): stands in for any race's 生魂.</summary>
        public int VoiceCores;
        /// <summary>2026-11-07 种族生魂 by <see cref="ChoSiren.Systems.Economy.CurrencyIds.Souls"/> order (魅/魔/海灵/血精灵).</summary>
        public List<int> RaceSouls1107 = new List<int>();
    }

    /// <summary>
    /// 2026-11-07 会议数值 (Docs/BALANCE-1107-LV200.md): members level to 200. Lv1–29 cost 星光币 only, capped by stage
    /// progress (12, then +5 at 1-5 / 1-10 / 2-5); clearing the last gate (2-10) lifts the cap to 200. From Lv30 each
    /// level also costs 敌人残魂, and Lv30, 40 … 190 each need a 突破 paid in 10 of the member's race 生魂 plus gold
    /// (通用生魂 then shards at 10 each cover missing souls). Levels already earned are never taken away.
    /// Method names keep the 1104 suffix because the practice page, member detail and battle advice call them.
    /// </summary>
    public sealed partial class GameModel
    {
        public const int BaseLevelCap1104 = 12;
        public const int LevelCapStep1104 = 5;
        public const int FirstBreakLevel1104 = 30;
        public const int BreakSpacing1104 = 10;
        public const int SoulsPerBreak1107 = 10;
        public const int ShardsPerSoul1107 = 10;
        public const int BreakGoldStep1107 = 3000;
        /// <summary>Gates before the cap opens to 200 (1-5, 1-10, 2-5, 2-10).</summary>
        public const int GatesToOpenCap1107 = 4;

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

        /// <summary>The stage-progress level cap shared by every member: 12 / 17 / 22 / 27, then 200.</summary>
        public int ProgressLevelCap1104
        {
            get
            {
                int gates = GatesCleared1104;
                return gates >= GatesToOpenCap1107 ? MaxMemberLevel : Math.Min(MaxMemberLevel, BaseLevelCap1104 + LevelCapStep1104 * gates);
            }
        }

        /// <summary>Short name of the next gate ("1-5") while it still limits the cap, else empty.</summary>
        public string NextGateName1104
        {
            get
            {
                if (GatesCleared1104 >= GatesToOpenCap1107) return string.Empty;
                StageDefinition next = GateStages1104().FirstOrDefault(s => !IsStageCleared(s.Id));
                return next == null ? string.Empty : next.Id.Replace("stage-", string.Empty);
            }
        }

        /// <summary>
        /// Breakthroughs counted for a member on the Lv30/40/… ladder: the saved 1107 count, or what her current level
        /// already implies. 1104 saves counted breaks at 17/27/…; those do not carry over (only the level does).
        /// </summary>
        public int BreaksOf1104(int member)
        {
            if (!IsValidMemberIndex(member)) return 0;
            int saved = Save.MemberBreaks1107 != null && member < Save.MemberBreaks1107.Count ? Save.MemberBreaks1107[member] : 0;
            int level = LevelOf(member);
            int implied = level <= FirstBreakLevel1104 ? 0 : (level - FirstBreakLevel1104 + BreakSpacing1104 - 1) / BreakSpacing1104;
            return Math.Min(MaxBreaks1107, Math.Max(saved, implied));
        }

        private static int MaxBreaks1107 => (MaxMemberLevel - FirstBreakLevel1104) / BreakSpacing1104;

        /// <summary>The level a member must break through at next (200 once every break is done).</summary>
        public int BreakLevelOf1104(int member)
        {
            int breaks = BreaksOf1104(member);
            return breaks >= MaxBreaks1107 ? MaxMemberLevel : FirstBreakLevel1104 + BreakSpacing1104 * breaks;
        }

        private int DoneBreaks1107(int member) => BreaksOf1104(member);

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
            /// <summary>Race 生魂 needed and which one (CurrencyIds.Soul*).</summary>
            public readonly int Souls, Gold;
            public readonly string SoulId;
            public BreakCost1104(int souls, string soulId, int gold) { Souls = souls; SoulId = soulId; Gold = gold; }
        }

        /// <summary>Cost of breakthrough number <paramref name="tier"/> (1 = the Lv30 one).</summary>
        public static BreakCost1104 BreakCostFor1104(int tier, string soulId) =>
            new BreakCost1104(SoulsPerBreak1107, soulId, BreakGoldStep1107 * Math.Max(1, tier));

        public BreakCost1104 NextBreakCost1104(int member) => BreakCostFor1104(DoneBreaks1107(member) + 1, SoulIdOf1107(member));

        /// <summary>The race 生魂 a member's breakthroughs use; empty race falls back to the charm one.</summary>
        public string SoulIdOf1107(int member) =>
            IsValidMemberIndex(member) ? SoulIdForRace1107(Members[member].Race) : CurrencyIds.SoulCharm;

        public static string SoulIdForRace1107(string race)
        {
            string r = race ?? string.Empty;
            if (r.StartsWith("魔族", StringComparison.Ordinal)) return CurrencyIds.SoulDemon;
            if (r.StartsWith("海灵", StringComparison.Ordinal)) return CurrencyIds.SoulSea;
            if (r.StartsWith("血精灵", StringComparison.Ordinal)) return CurrencyIds.SoulBlood;
            return CurrencyIds.SoulCharm;
        }

        private int SoulBalance1107(string soulId)
        {
            int index = Array.IndexOf(CurrencyIds.Souls, soulId);
            return index >= 0 && Save.RaceSouls1107 != null && index < Save.RaceSouls1107.Count ? Save.RaceSouls1107[index] : 0;
        }

        private bool AddSoul1107(string soulId, int delta)
        {
            int index = Array.IndexOf(CurrencyIds.Souls, soulId);
            if (index < 0) return false;
            Save.RaceSouls1107 ??= new List<int>();
            while (Save.RaceSouls1107.Count < CurrencyIds.Souls.Length) Save.RaceSouls1107.Add(0);
            Save.RaceSouls1107[index] = Math.Max(0, Save.RaceSouls1107[index] + delta);
            return true;
        }

        /// <summary>How a break's souls are paid: own race first, then 通用生魂, then shards (10 each).</summary>
        private (int Race, int Universal, int Shards) SoulPayment1107(BreakCost1104 cost)
        {
            int race = Math.Min(cost.Souls, SoulBalance1107(cost.SoulId));
            int universal = Math.Min(cost.Souls - race, Math.Max(0, Save.VoiceCores));
            int shards = (cost.Souls - race - universal) * ShardsPerSoul1107;
            return (race, universal, shards);
        }

        public bool CanBreakthrough1104(int member, out BreakCost1104 cost, out string message)
        {
            cost = NextBreakCost1104(member);
            if (!IsValidMemberIndex(member) || !IsUnlocked(member)) { message = "尚未签约该成员"; return false; }
            if (!NeedsBreakthrough1104(member)) { message = "现在不需要突破"; return false; }
            var pay = SoulPayment1107(cost);
            if (pay.Shards > Save.Shards)
            {
                message = $"{CurrencyName(cost.SoulId)}不足（{SoulBalance1107(cost.SoulId) + Save.VoiceCores}/{cost.Souls}，碎片 {ShardsPerSoul1107} 个可换 1 个）";
                return false;
            }
            if (Save.Gold < cost.Gold) { message = $"星光币不足，突破需要 {cost.Gold:N0}"; return false; }
            message = string.Empty;
            return true;
        }

        public bool Breakthrough1104(int member, out string message)
        {
            if (!CanBreakthrough1104(member, out BreakCost1104 cost, out message)) return false;
            var pay = SoulPayment1107(cost);
            AddSoul1107(cost.SoulId, -pay.Race);
            Save.VoiceCores -= pay.Universal;
            Save.Shards -= pay.Shards;
            Save.Gold -= cost.Gold;
            int done = DoneBreaks1107(member) + 1;
            Save.MemberBreaks1107 ??= new List<int>();
            while (Save.MemberBreaks1107.Count <= member) Save.MemberBreaks1107.Add(0);
            Save.MemberBreaks1107[member] = done;
            SaveState();
            message = $"{Members[member].Name} 突破成功，等级上限提升到 {LevelCapOf1104(member)}";
            if (pay.Universal > 0) message += $"（用了 {pay.Universal} 个通用生魂）";
            if (pay.Shards > 0) message += $"（用 {pay.Shards} 碎片兑换了生魂）";
            return true;
        }

        /// <summary>Why training stops at the cap, for the practice page.</summary>
        public string LevelCapReason1104(int member)
        {
            if (NeedsBreakthrough1104(member)) return "需要突破";
            string gate = NextGateName1104;
            return string.IsNullOrEmpty(gate) ? "已达当前上限" : $"通关 {gate} 解锁";
        }

        /// <summary>敌人残魂 one level-up costs from <paramref name="level"/>: none below 30, then 2 + ⌊(L−30)/10⌋.</summary>
        public static int RemnantCostAtLevel1107(int level) =>
            level < FirstBreakLevel1104 ? 0 : 2 + (Math.Min(level, MaxMemberLevel - 1) - FirstBreakLevel1104) / BreakSpacing1104;

        /// <summary>The race 生魂 a stage drops: races rotate by play order so each race has an elite to farm.</summary>
        public string StageSoulId1107(string stageId)
        {
            int index = tactics.Stages.FindIndex(s => s.Id == stageId);
            return CurrencyIds.Souls[Math.Max(0, index) % CurrencyIds.Souls.Length];
        }

        /// <summary>
        /// Materials a victory (or sweep run) pays: 敌人残魂 normal 1, elite 2–3, boss 3; the stage's race 生魂 normal 10%,
        /// elite 20%, boss replay 30% (1–2); a boss's first clear 3 生魂 (5 on a chapter's tenth stage).
        /// </summary>
        private void AddMaterialRewards1104(StageDefinition stage, bool firstClear, ulong seed, List<(string ItemId, int Amount)> rewards)
        {
            if (stage == null) return;
            var random = new SeededRandom(seed ^ 0x5EEDC0DEUL);
            string kind = stage.EncounterType ?? string.Empty;
            string soul = StageSoulId1107(stage.Id);
            if (kind == "elite")
            {
                rewards.Add((CurrencyIds.VoiceCrystal, 2 + (random.NextPermille() < 500 ? 1 : 0)));
                if (random.NextPermille() < 200) rewards.Add((soul, 1));
            }
            else if (kind == "boss")
            {
                rewards.Add((CurrencyIds.VoiceCrystal, 3));
                if (firstClear) rewards.Add((soul, StageNumber1104(stage.Id) % 10 == 0 ? 5 : 3));
                else if (random.NextPermille() < 300) rewards.Add((soul, 1 + (random.NextPermille() < 500 ? 1 : 0)));
            }
            else
            {
                rewards.Add((CurrencyIds.VoiceCrystal, 1));
                if (random.NextPermille() < 100) rewards.Add((soul, 1));
            }
        }
    }
}
