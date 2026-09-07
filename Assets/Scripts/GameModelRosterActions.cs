namespace ChoSiren
{
    public sealed partial class GameModel
    {
        /// <summary>
        /// Replaces one visible party slot in a single persisted transaction. Selecting
        /// an existing teammate swaps positions, so neither member silently disappears.
        /// Slot zero is the leader; class diversity is advice, never a hard restriction.
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
            if (previousSlot >= 0)
            {
                if (slot == Save.Team.Count)
                {
                    // An occupied member dragged to an empty end slot moves there;
                    // party count is unchanged and no duplicate member is inserted.
                    Save.Team.RemoveAt(previousSlot);
                    Save.Team.Add(memberIndex);
                }
                else
                {
                    int displacedMember = Save.Team[slot];
                    Save.Team[slot] = memberIndex;
                    Save.Team[previousSlot] = displacedMember;
                }
            }
            else if (slot == Save.Team.Count)
                Save.Team.Add(memberIndex);
            else
                Save.Team[slot] = memberIndex;

            SaveState();
            message = slot == 0
                ? $"{Members[memberIndex].Name} 已成为队长"
                : $"{Members[memberIndex].Name} 已加入第 {Save.Team.IndexOf(memberIndex) + 1} 位";
            return true;
        }
    }
}
