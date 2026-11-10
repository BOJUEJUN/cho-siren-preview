using System.Collections.Generic;
using UnityEngine;

namespace ChoSiren.UI
{
    /// <summary>
    /// 2026-11-10 (少爷: 每个角色技能图标应该是不一样的；"R" 定位徽章太粗糙): one designed icon per member skill
    /// (small + ultimate, 128 in all, GPT Image sheets sliced into Resources/Art/Ui1107/SkillIcons1110) and four
    /// role badges (主唱 / 主舞 / Rapper / 门面) drawn to match the race badges.
    /// </summary>
    public static class MemberSkillIcons1110
    {
        private const string SkillRoot = "Art/Ui1107/SkillIcons1110/";
        private const string RoleRoot = "Art/Ui1107/RoleBadges1110/";
        private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        /// <summary>The member's own skill icon, or null when none was drawn for her.</summary>
        public static Sprite Skill(string memberId, bool big)
            => string.IsNullOrEmpty(memberId) ? null : Load(SkillRoot + memberId + (big ? "-big" : "-small"));

        /// <summary>The role badge for a career label, or null for an unknown career.</summary>
        public static Sprite Role(string career)
        {
            string key = career switch
            {
                "主唱" => "role-vocal",
                "主舞" => "role-dance",
                "Rapper" => "role-rap",
                "门面" => "role-visual",
                _ => null,
            };
            return key == null ? null : Load(RoleRoot + key);
        }

        private static Sprite Load(string path)
        {
            if (cache.TryGetValue(path, out Sprite sprite) && sprite != null) return sprite;
            sprite = Resources.Load<Sprite>(path);
            cache[path] = sprite;
            return sprite;
        }
    }
}
