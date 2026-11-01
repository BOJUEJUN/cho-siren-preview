using System;
using System.Collections.Generic;

namespace ChoSiren
{
    /// <summary>
    /// 2026-11-01 (avatar-face, 赵金 1101c: "这个头像要支持点击头像换头像"): the manager may wear any owned
    /// member's face as the avatar, not only the preset list. Optional field (no schema bump): older saves read
    /// it as empty = use the preset index.
    /// </summary>
    public sealed partial class GameSave
    {
        /// <summary>Member id worn as the manager avatar; empty = <see cref="PlayerAvatar"/> preset.</summary>
        public string PlayerAvatarMember = string.Empty;
    }

    public sealed partial class GameModel
    {
        /// <summary>The worn member id when it is still owned, otherwise null (the preset applies).</summary>
        public string PlayerAvatarMemberId
        {
            get
            {
                string id = Save.PlayerAvatarMember;
                if (string.IsNullOrEmpty(id)) return null;
                int index = MemberIndexFor1101(id);
                return index >= 0 && IsUnlocked(index) ? id : null;
            }
        }

        /// <summary>Preset index for a member that already has a hand-framed preset (the four starters), else -1.</summary>
        public static int PresetAvatarForMember(string memberId)
        {
            if (string.IsNullOrEmpty(memberId)) return -1;
            string path = "Art/Members/Avatars/avatar-" + memberId;
            return Array.IndexOf(PlayerAvatarResources, path);
        }

        /// <summary>Owned members offered as avatars (those with a preset are offered as that preset instead).</summary>
        public List<string> AvatarMemberChoices()
        {
            var ids = new List<string>();
            foreach (int index in Save.UnlockedMembers)
            {
                if (!IsValidMemberIndex(index)) continue;
                string id = Members[index].Id;
                if (PresetAvatarForMember(id) >= 0 || ids.Contains(id)) continue;
                ids.Add(id);
            }
            return ids;
        }

        /// <summary>Wears preset <paramref name="index"/>; the name is untouched.</summary>
        public void SetPlayerAvatarPreset(int index)
        {
            Save.PlayerAvatar = Math.Max(0, Math.Min(PlayerAvatarResources.Length - 1, index));
            Save.PlayerAvatarMember = string.Empty;
            SaveState();
        }

        /// <summary>Wears an owned member's face. Returns false for unknown or locked members.</summary>
        public bool SetPlayerAvatarMember(string memberId)
        {
            int preset = PresetAvatarForMember(memberId);
            int index = MemberIndexFor1101(memberId);
            if (index < 0 || !IsUnlocked(index)) return false;
            if (preset >= 0) { SetPlayerAvatarPreset(preset); return true; }
            Save.PlayerAvatarMember = memberId;
            SaveState();
            return true;
        }

        public static int MemberIndexFor1101(string memberId)
        {
            MemberDefinition[] members = Members;
            for (int i = 0; i < members.Length; i++)
                if (members[i] != null && members[i].Id == memberId) return i;
            return -1;
        }
    }
}
