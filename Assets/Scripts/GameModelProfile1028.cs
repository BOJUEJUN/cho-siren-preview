using System;
using System.Globalization;
using System.Text;

namespace ChoSiren
{
    /// <summary>
    /// 经纪人档案 (2026-10-28, 少爷: "得加一个刚刚进游戏要设置的用户名"): the name and avatar signed on the
    /// first-launch contract, shown in the top bar, the profile card and by the producer in stories.
    /// </summary>
    public sealed partial class GameModel
    {
        public const string DefaultPlayerName = "经纪人";
        public const int PlayerNameMaxLength = 8;
        /// <summary>The opening chapter played once, right after the contract is signed.</summary>
        public const string PrologueStoryId = "prologue";

        /// <summary>
        /// The default manager portrait, eight AI-drawn presets (2026-10-28, 少爷: "预设一些头像…用 ai 生图"), then the
        /// four starting idols. Append only: a save stores the index.
        /// </summary>
        public static readonly string[] PlayerAvatarResources =
        {
            "Art/ProfileAvatarUser",
            "Art/PlayerAvatars/avatar-manager-m",
            "Art/PlayerAvatars/avatar-manager-f",
            "Art/PlayerAvatars/avatar-rocker-m",
            "Art/PlayerAvatars/avatar-glasses-f",
            "Art/PlayerAvatars/avatar-fox",
            "Art/PlayerAvatars/avatar-devil",
            "Art/PlayerAvatars/avatar-mermaid",
            "Art/PlayerAvatars/avatar-mascot",
            "Art/Members/Avatars/avatar-xingli",
            "Art/Members/Avatars/avatar-feiyin",
            "Art/Members/Avatars/avatar-wubai",
            "Art/Members/Avatars/avatar-yeying"
        };

        /// <summary>Offered by the contract's 随机 button (the UI skips any its font cannot draw).</summary>
        public static readonly string[] SuggestedPlayerNames =
        {
            "林夏", "苏晴", "陆远", "江野", "沈星", "顾言", "许诺", "叶舟",
            "秦川", "白川", "温然", "程风", "夏木", "安然", "季风", "唐宁"
        };

        public bool NeedsPlayerName => string.IsNullOrWhiteSpace(Save.PlayerName);

        public string PlayerName => NeedsPlayerName ? DefaultPlayerName : Save.PlayerName;

        public int PlayerAvatar => Math.Max(0, Math.Min(PlayerAvatarResources.Length - 1, Save.PlayerAvatar));

        public string PlayerAvatarResource => PlayerAvatarResources[PlayerAvatar];

        /// <summary>Trims the ends, folds inner whitespace runs to one space and drops control characters.</summary>
        public static string CleanPlayerName(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;
            var builder = new StringBuilder(raw.Length);
            bool space = false;
            foreach (char c in raw.Trim())
            {
                if (char.IsWhiteSpace(c)) { space = true; continue; }
                if (char.IsControl(c)) continue;
                if (space && builder.Length > 0) builder.Append(' ');
                space = false;
                builder.Append(c);
            }
            return builder.ToString();
        }

        /// <summary>
        /// Why <paramref name="raw"/> cannot be signed, or null when it can. <paramref name="canDisplay"/> is the UI
        /// font's coverage: the WebGL font is a subset, and a name it cannot draw would show as empty boxes.
        /// </summary>
        public static string PlayerNameProblem(string raw, Func<char, bool> canDisplay = null)
        {
            string name = CleanPlayerName(raw);
            if (name.Length == 0) return "先写下你的名字";
            if (new StringInfo(name).LengthInTextElements > PlayerNameMaxLength) return $"名字最多 {PlayerNameMaxLength} 个字";
            foreach (char c in name)
            {
                // Rich-text tags and story placeholders must never come from a name.
                if (c == '<' || c == '>' || c == '{' || c == '}' || c == '\\') return "名字里不能有 < > { } \\ 这些符号";
                if (char.IsSurrogate(c)) return "名字里不能有表情符号";
                if (c != ' ' && canDisplay != null && !canDisplay(c)) return $"「{c}」暂时显示不了，换个字吧";
            }
            return null;
        }

        /// <summary>Signs (or renames) the manager. Returns false with a player-facing reason.</summary>
        public bool TrySetPlayerProfile(string name, int avatar, Func<char, bool> canDisplay, out string error)
        {
            error = PlayerNameProblem(name, canDisplay);
            if (error != null) return false;
            Save.PlayerName = CleanPlayerName(name);
            Save.PlayerAvatar = Math.Max(0, Math.Min(PlayerAvatarResources.Length - 1, avatar));
            SaveState();
            return true;
        }

        /// <summary>Story text with the manager's own name in place of {player}.</summary>
        public string Personalize(string text) =>
            string.IsNullOrEmpty(text) ? text : text.Replace("{player}", PlayerName);
    }
}
