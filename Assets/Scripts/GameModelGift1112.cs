using System;

namespace ChoSiren
{
    public sealed partial class GameSave
    {
        /// <summary>2026-11-12: 幻纱 / 幻惑伶 / 凛音 have been given to this save (once).</summary>
        public bool NewMembersGifted1112;
    }

    /// <summary>
    /// 2026-11-12 (少爷: "新角色直接送所有人"): every save — old ones on their next load, new ones from the start — owns the
    /// three November members. Runs once per save; a member already signed or drawn stays as she is (no duplicate, no shards).
    /// They draw no salary (GameModelCareer.EstimatedMonthlySalary).
    /// </summary>
    public sealed partial class GameModel
    {
        public static readonly string[] GiftedMembers1112 = { "huansha", "huanhuoling", "linyin" };

        /// <summary>The gifted members draw no monthly salary (a present must not cost gold every week).</summary>
        public static bool IsGiftedMember1112(string memberId) => System.Array.IndexOf(GiftedMembers1112, memberId) >= 0;

        private void GiftNewMembers1112()
        {
            if (Save.NewMembersGifted1112) return;
            Save.NewMembersGifted1112 = true;
            // She joins at the level of the weakest member the player already owns (Lv1 in a new game), so she can
            // fight alongside the team instead of starting from scratch.
            int joinLevel = int.MaxValue;
            foreach (int owned in Save.UnlockedMembers)
                if (owned >= 0 && owned < Save.MemberLevels.Count && !IsGiftedMember1112(Members[owned].Id))
                    joinLevel = Math.Min(joinLevel, Save.MemberLevels[owned]);
            if (joinLevel == int.MaxValue) joinLevel = 1;
            foreach (string id in GiftedMembers1112)
            {
                int index = IndexOfMember(id);
                if (index < 0 || Save.UnlockedMembers.Contains(index)) continue;
                UnlockMemberInternal(index);
                if (index < Save.MemberLevels.Count) Save.MemberLevels[index] = Math.Max(Save.MemberLevels[index], joinLevel);
            }
        }
    }
}
