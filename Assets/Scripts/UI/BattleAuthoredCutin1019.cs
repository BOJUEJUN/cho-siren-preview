namespace ChoSiren
{
    /// <summary>Authored art belongs to these exact members; race similarity never grants a match.</summary>
    public static class BattleAuthoredCutin1019
    {
        public static string KeyForMember(string memberId)
        {
            switch (memberId)
            {
                case "xingli": return "cutin-charm";
                case "feiyin": return "cutin-demon";
                case "wubai": return "cutin-mermaid";
                case "yeying": return "cutin-bloodelf";
                // 2026-11-11 赵金 战斗psd 组6/7/8 (the three new members).
                case "huansha": return "cutin-huansha";
                case "huanhuoling": return "cutin-huanhuoling";
                case "linyin": return "cutin-linyin";
                default: return null;
            }
        }

        /// <summary>
        /// 2026-11-11 (赵金: "这三个贴脸图在战斗中大小我也希望是这么大"): these groups play at their own PSD size and place —
        /// wider than the screen, cropped by both edges and allowed over the HUD and the cards for the beat.
        /// </summary>
        public static bool NativeSize(string key) =>
            key == "cutin-huansha" || key == "cutin-huanhuoling" || key == "cutin-linyin";
    }
}
