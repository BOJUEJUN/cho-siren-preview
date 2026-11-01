using System;
using System.Collections.Generic;
using System.Linq;

namespace ChoSiren
{
    [Serializable]
    public sealed class TeamPresetSave
    {
        public string Name = string.Empty;
        public List<int> Members = new List<int>();
    }

    public sealed partial class GameModel
    {
        /// <summary>Hard cap on saved teams; the player starts with one and adds up to this.</summary>
        public const int TeamPresetCount = 5;
        public const int TeamNameMaxLength = 8;
        public string ActiveTeamName => Save.TeamPresets[Save.ActiveTeamPreset].Name;
        public int VisibleTeamPresetCount => Save.TeamPresets.Count;

        private void NormalizeTeamPresets()
        {
            Save.TeamPresets ??= new List<TeamPresetSave>();
            if (Save.TeamPresetSlots <= 0)
            {
                // Legacy saves always carried all TeamPresetCount slots, empty ones serving as
                // "store the current formation here" buttons. Keep team 1 plus every slot that
                // actually holds members, so old players still see their saved teams.
                var kept = new List<TeamPresetSave>();
                int active = 0;
                for (int i = 0; i < Save.TeamPresets.Count; i++)
                {
                    TeamPresetSave preset = Save.TeamPresets[i];
                    bool used = preset != null && preset.Members != null && preset.Members.Count > 0;
                    if (i != 0 && !used) continue;
                    if (i == Save.ActiveTeamPreset) active = kept.Count;
                    kept.Add(preset ?? new TeamPresetSave());
                }
                if (kept.Count == 0) kept.Add(new TeamPresetSave());
                Save.TeamPresets = kept;
                Save.ActiveTeamPreset = active;
            }
            if (Save.TeamPresets.Count > TeamPresetCount)
                Save.TeamPresets.RemoveRange(TeamPresetCount, Save.TeamPresets.Count - TeamPresetCount);
            if (Save.TeamPresets.Count == 0) Save.TeamPresets.Add(new TeamPresetSave());
            Save.ActiveTeamPreset = UnityEngine.Mathf.Clamp(Save.ActiveTeamPreset, 0, Save.TeamPresets.Count - 1);
            for (int i = 0; i < Save.TeamPresets.Count; i++)
            {
                var preset = Save.TeamPresets[i] ??= new TeamPresetSave();
                preset.Name = CleanTeamName(preset.Name);
                if (preset.Name.Length == 0) preset.Name = "队伍" + (i + 1);
                preset.Members = (preset.Members ?? new List<int>()).Where(IsUnlocked).Distinct().Take(TeamCapacity).ToList();
            }
            Save.TeamPresetSlots = Save.TeamPresets.Count;
            // The active formation remains authoritative: legacy saves and existing roster actions
            // keep their exact order, and editing one preset never mutates another preset's list.
            Save.TeamPresets[Save.ActiveTeamPreset].Members = new List<int>(Save.Team);
        }

        public static string CleanTeamName(string name)
        {
            string clean = new string((name ?? string.Empty).Where(c => !char.IsControl(c) && c != '<' && c != '>').ToArray()).Trim();
            var elements = System.Globalization.StringInfo.GetTextElementEnumerator(clean);
            var result = new System.Text.StringBuilder();
            for (int count = 0; count < TeamNameMaxLength && elements.MoveNext(); count++) result.Append(elements.GetTextElement());
            return result.ToString();
        }

        public bool RenameTeamPreset(int slot, string name, out string message)
        {
            if (slot < 0 || slot >= Save.TeamPresets.Count) { message = "队伍位置无效"; return false; }
            string clean = CleanTeamName(name);
            if (clean.Length == 0) { message = "请填写队伍名称"; return false; }
            Save.TeamPresets[slot].Name = clean;
            SaveState();
            message = "队伍已改名";
            return true;
        }

        /// <summary>Adds a team seeded with the current formation so it is usable immediately.</summary>
        public bool AddTeamPreset(out string message)
        {
            if (Save.TeamPresets.Count >= TeamPresetCount) { message = "最多 " + TeamPresetCount + " 支队伍"; return false; }
            Save.TeamPresets[Save.ActiveTeamPreset].Members = new List<int>(Save.Team);
            int number = Save.TeamPresets.Count + 1;
            var taken = new HashSet<string>(Save.TeamPresets.Select(p => p.Name));
            while (taken.Contains("队伍" + number)) number++;
            Save.TeamPresets.Add(new TeamPresetSave { Name = "队伍" + number, Members = new List<int>(Save.Team) });
            Save.ActiveTeamPreset = Save.TeamPresets.Count - 1;
            Save.TeamPresetSlots = Save.TeamPresets.Count;
            SaveState();
            message = "已添加" + ActiveTeamName;
            return true;
        }

        /// <summary>Deletes a saved team. At least one team always remains.</summary>
        public bool DeleteTeamPreset(int slot, out string message)
        {
            if (slot < 0 || slot >= Save.TeamPresets.Count) { message = "队伍位置无效"; return false; }
            if (Save.TeamPresets.Count <= 1) { message = "至少保留一支队伍"; return false; }
            string removed = Save.TeamPresets[slot].Name;
            Save.TeamPresets.RemoveAt(slot);
            if (slot < Save.ActiveTeamPreset) Save.ActiveTeamPreset--;
            else if (slot == Save.ActiveTeamPreset) Save.ActiveTeamPreset = UnityEngine.Mathf.Clamp(slot, 0, Save.TeamPresets.Count - 1);
            Save.Team = new List<int>(Save.TeamPresets[Save.ActiveTeamPreset].Members);
            Save.TeamPresetSlots = Save.TeamPresets.Count;
            SaveState();
            message = "已删除「" + removed + "」";
            return true;
        }

        public bool SaveTeamPreset(int slot, out string message)
        {
            if (slot < 0 || slot >= Save.TeamPresets.Count) { message = "队伍位置无效"; return false; }
            if (!ValidateTeamComposition(Save.Team, out message)) return false;
            Save.TeamPresets[Save.ActiveTeamPreset].Members = new List<int>(Save.Team);
            Save.TeamPresets[slot].Members = new List<int>(Save.Team);
            Save.ActiveTeamPreset = slot;
            SaveState();
            message = "已保存到" + ActiveTeamName;
            return true;
        }

        public bool SelectTeamPreset(int slot, out string message)
        {
            if (slot < 0 || slot >= Save.TeamPresets.Count) { message = "队伍位置无效"; return false; }
            if (slot == Save.ActiveTeamPreset) { message = "正在使用这支队伍"; return true; }
            var target = Save.TeamPresets[slot].Members;
            if (target.Count == 0) { message = "请先存入一支队伍"; return false; }
            if (target.Count > TeamCapacity || target.Distinct().Count() != target.Count || target.Any(i => !IsUnlocked(i)))
            { message = "队伍包含重复或未签约成员，请重新保存"; return false; }
            if (!ValidateTeamComposition(target, out message)) return false;
            Save.TeamPresets[Save.ActiveTeamPreset].Members = new List<int>(Save.Team);
            Save.Team = new List<int>(target);
            Save.ActiveTeamPreset = slot;
            SaveState();
            message = "已切换到" + ActiveTeamName;
            return true;
        }
    }
}
