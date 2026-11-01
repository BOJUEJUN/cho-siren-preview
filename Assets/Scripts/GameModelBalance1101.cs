using System;
using ChoSiren.Systems.Tactics;

namespace ChoSiren
{
    /// <summary>
    /// 2026-11-01 赵金数值批次 (Docs/BALANCE-1101.md).
    /// · 装备：单件饰品对战力的提升最多约 5% — every stat of one item is at most +4.5% (<see cref="MaxItemStatPermille"/>;
    ///   the half point is headroom for rounding a level-1 stat), so no single item raises a member's 战力 (生命/10 + 攻击×5
    ///   + 防御) by more than 5%. The authored stat mixes keep their shape, scaled by <see cref="ItemBonusScalePermille"/>;
    ///   upgrades add +0.5% a level up to the same cap.
    /// · 新手保底 <see cref="FirstStageNewcomerGuard"/> (2026-11-01b 用户: "第一关可以简单点" → on by default): until 1-1 is
    ///   cleared for the first time it is fought with 简单 enemies (health/attack ×0.85, ≈95% win in 自动); rewards, stars and
    ///   the 难度 label stay 普通. Every later 1-1 (replays) and every other stage keeps 赵金's ≈60%.
    /// </summary>
    public sealed partial class GameModel
    {
        public const int MaxItemStatPermille = 45;
        public const int ItemBonusScalePermille = 250;
        public const int AccessoryUpgradeStepPermille = 5;

        /// <summary>
        /// On by default since 2026-11-01b. A fresh account's 1-1 battles use the 简单 enemy multipliers until 1-1 is cleared
        /// once (first-fight guard against the 40% loss rate of an untrained team); set false for 赵金's raw numbers.
        /// </summary>
        public static bool FirstStageNewcomerGuard = true;

        /// <summary>简单 enemies, 普通 name and rewards: the guard is a safety net, not a different difficulty.</summary>
        private static readonly BattleDifficultyProfile NewcomerGuardProfile =
            new BattleDifficultyProfile(BattleDifficulty.Normal, "普通", 850, 850, 1000);

        private BattleDifficultyProfile BattleProfileFor1101(StageDefinition stage) =>
            NewcomerGuardApplies(stage) && CurrentBattleDifficulty == BattleDifficulty.Normal ? NewcomerGuardProfile : CurrentBattleDifficultyProfile;

        public static CombatStatBonuses ScaleItemBonus1101(CombatStatBonuses authored)
        {
            int Scale(int value) => value <= 0 ? 0
                : Math.Min(MaxItemStatPermille, Math.Max(1, (int)Math.Round(value * ItemBonusScalePermille / 1000.0)));
            return new CombatStatBonuses(Scale(authored.Hp), Scale(authored.Attack), Scale(authored.Defense));
        }

        /// <summary>How much one accessory raises a member's 战力 (permille of her power without it), at her level.</summary>
        public int AccessoryPowerGainPermille(int member, int accessory)
        {
            if (!IsValidMemberIndex(member)) return 0;
            UnitDefinition unit = tactics.FindUnit(Members[member].Id);
            if (unit == null) return 0;
            int bare = BattleSimulator.PlayerStats(unit, LevelOf(member)).Power;
            int worn = BattleSimulator.PlayerStats(unit, LevelOf(member), EffectiveAccessoryBonuses(accessory)).Power;
            return bare <= 0 ? 0 : (int)((long)(worn - bare) * 1000 / bare);
        }

        private bool NewcomerGuardApplies(StageDefinition stage) =>
            FirstStageNewcomerGuard && stage != null && stage.Id == "stage-1-1" && !IsStageCleared(stage.Id);
    }
}
