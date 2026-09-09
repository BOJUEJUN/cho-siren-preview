namespace ChoSiren.Systems.Presentation
{
    /// <summary>
    /// Player-facing captain (leader) trait copy. The strings are the authored race-command
    /// effects actually implemented by the realtime battle leader hooks, so the profile never
    /// invents an effect. Kept Unity-free so the wording can be asserted from EditMode tests.
    /// </summary>
    public static class MemberCaptainTraits
    {
        public const string CharmTitle = "魅族指挥";
        public const string MermaidTitle = "人鱼指挥";
        public const string DemonTitle = "魔族指挥";
        public const string BloodElfTitle = "血精灵指挥";
        public const string NeutralTitle = "全队增益";

        /// <summary>Same race resolution as BattleSimulator.ParseCombatRace, string-in only.</summary>
        public static string Resolve(string raceLabel)
        {
            string value = raceLabel ?? string.Empty;
            if (value.Contains("人鱼") || value.Contains("海灵")) return "mermaid";
            if (value.Contains("血精灵")) return "bloodelf";
            if (value.Contains("恶魔") || value.Contains("魔族")) return "demon";
            if (value.Contains("魅族")) return "charm";
            return "none";
        }

        public static string Title(string raceLabel)
        {
            switch (Resolve(raceLabel))
            {
                case "charm": return CharmTitle;
                case "mermaid": return MermaidTitle;
                case "demon": return DemonTitle;
                case "bloodelf": return BloodElfTitle;
                default: return NeutralTitle;
            }
        }

        public static string Describe(string raceLabel)
        {
            switch (Resolve(raceLabel))
            {
                case "charm": return "魅族指挥：骰型赋予全队追击次数，随普攻触发；好骰型获得更多追击。";
                case "mermaid": return "人鱼指挥：骰型为全队提供护盾；五同时全队额外减伤。";
                case "demon": return "魔族指挥：全队普攻附加中毒，持续消耗敌人；骰型决定叠毒层数。";
                case "bloodelf": return "血精灵指挥：骰型提供穿甲，收割低血量目标；首次散点可免费重投一次。";
                default: return "全队享有骰子累计伤害增益。当前成员暂无额外种族指挥效果。";
            }
        }
    }
}
