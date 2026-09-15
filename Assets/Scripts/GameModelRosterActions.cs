using System;
using System.Collections.Generic;

namespace ChoSiren
{
    public sealed partial class GameModel
    {
        private const int MaxMembersPerRace = 2;
        private const int MinimumFullTeamCareers = 3;

        /// <summary>
        /// A pair may share one race, but a formation cannot chain the same race command through
        /// all four slots. A full team also keeps room for at least three combat careers.
        /// Older saves are not rewritten; they receive the same validation when edited or played.
        /// </summary>
        private static bool ValidateTeamComposition(IReadOnlyList<int> team, out string message)
        {
            var raceCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            var careers = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < team.Count; index++)
            {
                int memberIndex = team[index];
                if (!IsValidMemberIndex(memberIndex)) continue;
                MemberDefinition member = Members[memberIndex];
                string race = (member.Race ?? string.Empty).Trim();
                if (!string.IsNullOrEmpty(race))
                {
                    raceCounts.TryGetValue(race, out int count);
                    if (++count > MaxMembersPerRace)
                    {
                        message = "同一种族最多同时上阵 2 人，请为其他种族保留位置";
                        return false;
                    }
                    raceCounts[race] = count;
                }
                string career = (member.Career ?? string.Empty).Trim();
                if (!string.IsNullOrEmpty(career)) careers.Add(career);
            }

            if (team.Count == TeamCapacity && careers.Count < MinimumFullTeamCareers)
            {
                message = "满编队伍至少需要 3 种不同定位";
                return false;
            }
            message = string.Empty;
            return true;
        }

        /// <summary>
        /// Replaces one visible party slot in a single persisted transaction. Selecting
        /// an existing teammate swaps positions, so neither member silently disappears.
        /// Slot zero is the leader; the committed result must satisfy the formation limits above.
        /// </summary>
        public bool ReplaceTeamSlot(int slot, int memberIndex, out string message)
        {
            if (slot < 0 || slot >= TeamCapacity || slot > Save.Team.Count)
            {
                message = "编队位置无效";
                return false;
            }
            if (!IsUnlocked(memberIndex))
            {
                message = "请先签约该成员";
                return false;
            }
            if (tactics.FindUnit(Members[memberIndex].Id) == null)
            {
                message = "该成员的战斗资料尚未就绪";
                return false;
            }

            int previousSlot = Save.Team.IndexOf(memberIndex);
            if (previousSlot == slot)
            {
                message = "该成员已在这个位置";
                return false;
            }
            var nextTeam = new List<int>(Save.Team);
            if (previousSlot >= 0)
            {
                if (slot == nextTeam.Count)
                {
                    // An occupied member dragged to an empty end slot moves there;
                    // party count is unchanged and no duplicate member is inserted.
                    nextTeam.RemoveAt(previousSlot);
                    nextTeam.Add(memberIndex);
                }
                else
                {
                    int displacedMember = nextTeam[slot];
                    nextTeam[slot] = memberIndex;
                    nextTeam[previousSlot] = displacedMember;
                }
            }
            else if (slot == nextTeam.Count)
                nextTeam.Add(memberIndex);
            else
                nextTeam[slot] = memberIndex;

            if (!ValidateTeamComposition(nextTeam, out message)) return false;

            Save.Team = nextTeam;
            SaveState();
            message = slot == 0
                ? $"{Members[memberIndex].Name} 已成为队长"
                : $"{Members[memberIndex].Name} 已加入第 {Save.Team.IndexOf(memberIndex) + 1} 位";
            return true;
        }
    }
}
