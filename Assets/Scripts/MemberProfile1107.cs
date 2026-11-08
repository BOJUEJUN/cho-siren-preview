using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChoSiren
{
    /// <summary>
    /// 2026-11-07 会议: "面试界面需显示声乐、舞蹈、说唱、名气四项能力，并加入角色性格描述（年龄、职业、爱好、性格特点）".
    /// One profile per member from Resources/Data/member-profiles.json (age, job, hobby, trait, blood type, birthday).
    /// </summary>
    [Serializable]
    public sealed class MemberProfile1107
    {
        public string Id = string.Empty;
        public int Age;
        public string Job = string.Empty, Hobby = string.Empty, Trait = string.Empty, Blood = string.Empty, Birthday = string.Empty;

        [Serializable] private sealed class Table { public int SchemaVersion; public List<MemberProfile1107> Profiles = new List<MemberProfile1107>(); }

        private static Dictionary<string, MemberProfile1107> byId;

        /// <summary>The member's profile, or a neutral one when the table lacks her.</summary>
        public static MemberProfile1107 Of(string memberId)
        {
            if (byId == null)
            {
                byId = new Dictionary<string, MemberProfile1107>();
                TextAsset json = Resources.Load<TextAsset>("Data/member-profiles");
                if (json != null)
                    foreach (MemberProfile1107 p in JsonUtility.FromJson<Table>(json.text).Profiles)
                        if (!string.IsNullOrEmpty(p.Id)) byId[p.Id] = p;
            }
            return memberId != null && byId.TryGetValue(memberId, out MemberProfile1107 found)
                ? found : new MemberProfile1107 { Id = memberId ?? string.Empty, Age = 18, Job = "练习生", Hobby = "唱歌爱好者", Trait = "元气", Blood = "O+", Birthday = "1月1日" };
        }

        /// <summary>"18岁  学生  舞蹈爱好者" — the line under the name.</summary>
        public string Line => $"{Age}岁  {Job}  {Hobby}";

        /// <summary>Rank letter for a 0..100 stat (the PSD shows a letter before each value).</summary>
        public static string Rank(int value) =>
            value >= 90 ? "S" : value >= 75 ? "A" : value >= 60 ? "B" : value >= 45 ? "C" : value >= 30 ? "D" : value >= 15 ? "E" : "F";
    }
}
