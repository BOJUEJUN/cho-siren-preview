using System;
using System.Collections.Generic;
using System.Linq;

namespace ChoSiren
{
    public sealed partial class GameSave
    {
        /// <summary>2026-11-07 角色体力 per member (indexed like MemberLevels); missing = full.</summary>
        public List<int> MemberStamina1107 = new List<int>();
        /// <summary>Unix seconds each member's stamina was last settled (regen runs from here).</summary>
        public List<long> MemberStaminaAt1107 = new List<long>();
    }

    /// <summary>
    /// 2026-11-07 会议 (风险与体力系统: "体力影响角色持续作战能力，降至零需休息，可通过道具或休息恢复"): every member has
    /// 120 体力. Each battle or sweep run costs everyone in the team 8 (a boss 12); below 30% she is 疲劳 and fights at
    /// 90% HP and attack; at less than one battle's cost she needs rest and cannot start a battle. Rest refills 1 every
    /// 3 minutes (a full bar in 6 hours); 补满 spends 星光币 (10 per missing point) at once.
    /// </summary>
    public sealed partial class GameModel
    {
        public const int MemberStaminaMax1107 = 120;
        public const int BattleMemberStamina1107 = 8;
        public const int BossMemberStamina1107 = 12;
        public const int MemberStaminaRegenSeconds1107 = 180;
        public const int TiredBelow1107 = 36;
        public const int TiredPermille1107 = 900;
        public const int RestGoldPerPoint1107 = 10;

        private long NowUnix1107 => new DateTimeOffset(DateTime.SpecifyKind(nowProvider(), DateTimeKind.Utc)).ToUnixTimeSeconds();

        /// <summary>Stamina a battle on this stage costs each team member.</summary>
        public static int MemberStaminaCost1107(Systems.Tactics.StageDefinition stage) =>
            stage != null && stage.EncounterType == "boss" ? BossMemberStamina1107 : BattleMemberStamina1107;

        /// <summary>Current stamina after regen (does not write the save).</summary>
        public int MemberStamina1107(int member)
        {
            if (!IsValidMemberIndex(member)) return 0;
            List<int> values = Save.MemberStamina1107;
            if (values == null || member >= values.Count || values[member] < 0) return MemberStaminaMax1107;
            long at = Save.MemberStaminaAt1107 != null && member < Save.MemberStaminaAt1107.Count ? Save.MemberStaminaAt1107[member] : 0;
            long gained = at > 0 ? Math.Max(0, (NowUnix1107 - at) / MemberStaminaRegenSeconds1107) : MemberStaminaMax1107;
            return (int)Math.Min(MemberStaminaMax1107, values[member] + gained);
        }

        public bool IsTired1107(int member) => MemberStamina1107(member) < TiredBelow1107;

        /// <summary>Seconds until the member has rested back to full.</summary>
        public int SecondsToRested1107(int member) =>
            (MemberStaminaMax1107 - MemberStamina1107(member)) * MemberStaminaRegenSeconds1107;

        private void SetMemberStamina1107(int member, int value)
        {
            Save.MemberStamina1107 ??= new List<int>();
            Save.MemberStaminaAt1107 ??= new List<long>();
            while (Save.MemberStamina1107.Count <= member) Save.MemberStamina1107.Add(-1);
            while (Save.MemberStaminaAt1107.Count <= member) Save.MemberStaminaAt1107.Add(0);
            int clamped = Math.Max(0, Math.Min(MemberStaminaMax1107, value));
            Save.MemberStamina1107[member] = clamped >= MemberStaminaMax1107 ? -1 : clamped;
            Save.MemberStaminaAt1107[member] = NowUnix1107;
        }

        /// <summary>The first team member too tired for <paramref name="runs"/> battles of <paramref name="cost"/>, or −1.</summary>
        private int TooTiredInTeam1107(int cost, int runs)
        {
            foreach (int member in Save.Team)
                if (IsValidMemberIndex(member) && MemberStamina1107(member) < cost * runs) return member;
            return -1;
        }

        private string NeedsRestMessage1107(int member, int cost, int runs) =>
            $"{Members[member].Name} 体力不足（{MemberStamina1107(member)}/{cost * runs}），需要休息或换人";

        private void SpendTeamStamina1107(int cost)
        {
            foreach (int member in Save.Team.Distinct())
                if (IsValidMemberIndex(member)) SetMemberStamina1107(member, MemberStamina1107(member) - cost);
        }

        /// <summary>Battle multiplier for a member's HP and attack: tired members fight at 90%.</summary>
        public int FatiguePermille1107(int member) => IsTired1107(member) ? TiredPermille1107 : 1000;

        /// <summary>星光币 to refill a member's stamina now.</summary>
        public int RestCost1107(int member) => (MemberStaminaMax1107 - MemberStamina1107(member)) * RestGoldPerPoint1107;

        /// <summary>补满: pays 星光币 to refill the member's stamina at once.</summary>
        public bool TryRestNow1107(int member, out string message)
        {
            if (!IsValidMemberIndex(member) || !IsUnlocked(member)) { message = "尚未签约该成员"; return false; }
            int cost = RestCost1107(member);
            if (cost <= 0) { message = $"{Members[member].Name} 体力已满"; return false; }
            if (Save.Gold < cost) { message = $"星光币不足，补满需要 {cost:N0}"; return false; }
            Save.Gold -= cost;
            SetMemberStamina1107(member, MemberStaminaMax1107);
            SaveState();
            message = $"{Members[member].Name} 体力已补满";
            return true;
        }
    }
}
