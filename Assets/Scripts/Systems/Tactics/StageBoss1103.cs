using System;
using System.Collections.Generic;

namespace ChoSiren.Systems.Tactics
{
    /// <summary>
    /// 2026-11-03 (用户: boss 尽量按照 PPT 来): every boss stage used to field the same 「魅声女王」. The planning deck
    /// names the chapter-one bosses — 1-5 小BOSS「碎音·琉娜」, 1-10 大BOSS「幻蚀歌枢·梅洛蒂」 — and chapter two's story
    /// calls for a second, deep-sea queen. The boss keeps the shared "siren-queen" rules (phases, specials, voice);
    /// only who she is changes per stage: her name, her art, and whether the user's entrance video (which shows the
    /// chapter-one bosses) belongs to her.
    /// </summary>
    public static class StageBoss1103
    {
        public const string BossUnitId = "siren-queen";

        public sealed class Look
        {
            public string Name;
            /// <summary>Resources path of the boss figure.</summary>
            public string Art;
            /// <summary>The user's mini/major entrance videos show the chapter-one bosses only.</summary>
            public bool EntranceVideo;
        }

        private static readonly Look Luna = new Look
            { Name = "碎音·琉娜", Art = "Art/BattleUser/boss-throne-20260928", EntranceVideo = true };
        private static readonly Look Melody = new Look
            { Name = "幻蚀歌枢·梅洛蒂", Art = "Art/BattleUser/boss-throne-user-v1", EntranceVideo = true };
        private static readonly Look Selene = new Look
            { Name = "深潮女王·赛琳", Art = "Art/BattleUser/boss-selene-1103", EntranceVideo = false };

        private static readonly Dictionary<string, Look> Stages = new Dictionary<string, Look>(StringComparer.Ordinal)
        {
            ["stage-1-5"] = Luna,
            ["stage-1-10"] = Melody,
            ["stage-2-5"] = Selene,
            ["stage-2-10"] = Selene,
        };

        public static Look For(string stageId) =>
            stageId != null && Stages.TryGetValue(stageId, out Look look) ? look : null;

        /// <summary>The boss row as this stage presents her; mechanics keep reading the shared unit id.</summary>
        public static UnitDefinition Present(StageDefinition stage, UnitDefinition definition)
        {
            if (definition == null || definition.Id != BossUnitId) return definition;
            Look look = For(stage?.Id);
            if (look == null || look.Name == definition.Name) return definition;
            return new UnitDefinition
            {
                Id = definition.Id,
                Name = look.Name,
                Role = definition.Role,
                MaxHp = definition.MaxHp,
                Attack = definition.Attack,
                Defense = definition.Defense,
                Speed = definition.Speed,
                CritPermille = definition.CritPermille,
                GrowthModel = definition.GrowthModel,
                SkillIds = new List<string>(definition.SkillIds),
            };
        }
    }
}
