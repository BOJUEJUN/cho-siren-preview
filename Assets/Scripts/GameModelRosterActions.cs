using System;
using System.Collections.Generic;
using System.Linq;

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
        /// <summary>Whether the active formation can start a battle; the message says what to fix.</summary>
        public bool IsTeamReady(out string message) => ValidateTeamComposition(Save.Team, out message);

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
                        int total = team.Count(i => IsValidMemberIndex(i) &&
                            string.Equals((Members[i].Race ?? string.Empty).Trim(), race, StringComparison.Ordinal));
                        message = $"同一种族最多同时上阵 2 人\n{race}当前 {total} 人，请更换 {total - MaxMembersPerRace} 人";
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

        /// <summary>Explicit player action only: find a legal formation before changing the save.</summary>
        public bool TryAutoTeam(out string message)
        {
            int[] available = Save.UnlockedMembers.Where(IsValidMemberIndex).Distinct()
                .Where(index => tactics.FindUnit(Members[index].Id) != null).ToArray();
            if (available.Length == 0)
            {
                message = "没有可出战的已签约成员，当前编队已保留";
                return false;
            }
            var powers = available.ToDictionary(index => index, PowerOf);
            int leader = Save.Team.Count > 0 && available.Contains(Save.Team[0]) ? Save.Team[0] : -1;
            // At most two members of any race can enter a team, so weaker members beyond
            // the first two of a race/career pair cannot improve a legal formation. Include
            // the current captain separately; this keeps the bounded search independent
            // of how many members the player has signed.
            int[] candidates = available.OrderByDescending(index => powers[index]).ThenBy(index => index)
                .GroupBy(index => (Members[index].Race ?? string.Empty) + "\n" + Members[index].Career)
                .SelectMany(group => group.Take(MaxMembersPerRace))
                .Concat(leader >= 0 ? new[] { leader } : Array.Empty<int>()).Distinct()
                .OrderByDescending(index => powers[index]).ThenBy(index => index).ToArray();
            var selected = new List<int>(TeamCapacity);
            var races = new Dictionary<string, int>(StringComparer.Ordinal);
            var careers = new Dictionary<string, int>(StringComparer.Ordinal);
            var best = new List<int>();
            bool bestKeepsLeader = false;
            int bestCareers = -1;
            long bestPower = -1;

            void Search(int start, long power, bool keepsLeader)
            {
                int count = selected.Count;
                bool legal = count > 0 && (count < TeamCapacity || careers.Count >= MinimumFullTeamCareers);
                if (legal && (count > best.Count || count == best.Count &&
                    (keepsLeader && !bestKeepsLeader || keepsLeader == bestKeepsLeader &&
                    (power > bestPower || power == bestPower && careers.Count > bestCareers))))
                {
                    best = new List<int>(selected);
                    bestKeepsLeader = keepsLeader;
                    bestCareers = careers.Count;
                    bestPower = power;
                }
                if (count >= TeamCapacity || count + candidates.Length - start < best.Count) return;
                for (int next = start; next < candidates.Length; next++)
                {
                    int index = candidates[next];
                    string race = (Members[index].Race ?? string.Empty).Trim();
                    string career = (Members[index].Career ?? string.Empty).Trim();
                    races.TryGetValue(race, out int raceCount);
                    if (!string.IsNullOrEmpty(race) && raceCount >= MaxMembersPerRace) continue;
                    careers.TryGetValue(career, out int careerCount);
                    selected.Add(index);
                    races[race] = raceCount + 1;
                    careers[career] = careerCount + 1;
                    Search(next + 1, power + powers[index], keepsLeader || index == leader);
                    selected.RemoveAt(selected.Count - 1);
                    if (raceCount == 0) races.Remove(race); else races[race] = raceCount;
                    if (careerCount == 0) careers.Remove(career); else careers[career] = careerCount;
                }
            }
            Search(0, 0, false);
            if (best.Count == 0 || !ValidateTeamComposition(best, out message))
            {
                message = "现有已签约成员无法组成可出战编队，当前编队已保留";
                return false;
            }
            if (leader >= 0 && best.Remove(leader)) best.Insert(0, leader);
            Save.Team = best;
            SaveState();
            message = best.Count == TeamCapacity
                ? "已按种族和定位整理编队" + (bestKeepsLeader ? "，保留现队长" : string.Empty)
                : $"已编队 {best.Count}/{TeamCapacity} 人；现有候选受种族或定位限制，无法补满";
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
