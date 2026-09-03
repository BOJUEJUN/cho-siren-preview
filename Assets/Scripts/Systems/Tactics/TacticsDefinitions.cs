using System;
using System.Collections.Generic;

namespace ChoSiren.Systems.Tactics
{
    public static class SkillEffect
    {
        public const string Damage = "damage";
        public const string Heal = "heal";
        public const string BuffAttack = "buff-attack";
        public const string DebuffDefense = "debuff-defense";
        public const string Shield = "shield";

        public static readonly string[] All = { Damage, Heal, BuffAttack, DebuffDefense, Shield };
        public static bool IsKnown(string id) => Array.IndexOf(All, id) >= 0;
        public static bool TargetsEnemies(string effect) => effect == Damage || effect == DebuffDefense;
    }

    /// <summary>Area shapes on a 3×3 side grid, anchored at the chosen cell.</summary>
    public static class SkillPattern
    {
        public const string Single = "single";
        public const string Plus = "plus";
        public const string Row = "row";
        public const string Column = "column";
        public const string All = "all";

        public static readonly string[] Known = { Single, Plus, Row, Column, All };
        public static bool IsKnown(string id) => Array.IndexOf(Known, id) >= 0;
    }

    [Serializable]
    public sealed class SkillDefinition
    {
        public string Id = string.Empty;
        public string Name = string.Empty;
        public string Effect = SkillEffect.Damage;
        public string Pattern = SkillPattern.Single;
        /// <summary>Damage/heal as ‰ of the actor's attack; buffs/debuffs as ‰ stat change; shield as ‰ of max HP.</summary>
        public int PowerPermille = 1000;
        /// <summary>Rounds a buff/debuff/shield stays active.</summary>
        public int Duration = 2;
        /// <summary>Rounds before the skill can be used again. Every unit needs one skill with 0.</summary>
        public int Cooldown;
        public bool CanCrit = true;

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(Name))
            {
                error = "技能缺少 ID 或名称";
                return false;
            }

            if (!SkillEffect.IsKnown(Effect))
            {
                error = $"技能 {Id} 的效果未知：{Effect}";
                return false;
            }

            if (!SkillPattern.IsKnown(Pattern))
            {
                error = $"技能 {Id} 的范围未知：{Pattern}";
                return false;
            }

            if (PowerPermille <= 0 || Cooldown < 0 || Duration < 0)
            {
                error = $"技能 {Id} 的数值无效";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }

    [Serializable]
    public sealed class UnitDefinition
    {
        public string Id = string.Empty;
        public string Name = string.Empty;
        public string Role = string.Empty;
        public int MaxHp = 1000;
        public int Attack = 100;
        public int Defense = 50;
        public int Speed = 100;
        public int CritPermille = 50;
        public List<string> SkillIds = new List<string>();

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(Name))
            {
                error = "单位缺少 ID 或名称";
                return false;
            }

            if (MaxHp <= 0 || Attack <= 0 || Defense < 0 || Speed <= 0 || CritPermille < 0 || CritPermille > 1000)
            {
                error = $"单位 {Id} 的属性无效";
                return false;
            }

            if (SkillIds.Count == 0)
            {
                error = $"单位 {Id} 没有技能";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }

    [Serializable]
    public sealed class EnemySpawn
    {
        public string UnitId = string.Empty;
        public int Row;
        public int Col;
        /// <summary>Stat multiplier (‰) so one enemy definition can serve several stages.</summary>
        public int ScalePermille = 1000;
    }

    [Serializable]
    public sealed class DropEntry
    {
        public string ItemId = string.Empty;
        public int Weight = 1;
        public int Min = 1;
        public int Max = 1;
    }

    [Serializable]
    public sealed class DropTable
    {
        public int Rolls = 1;
        public List<DropEntry> Entries = new List<DropEntry>();
    }

    [Serializable]
    public sealed class StageDefinition
    {
        public string Id = string.Empty;
        public string Chapter = string.Empty;
        public string Name = string.Empty;
        public int StaminaCost = 8;
        public int TurnLimit = 20;
        /// <summary>Finishing within this many rounds earns the third star.</summary>
        public int ThreeStarRounds = 8;
        public int GoldReward = 300;
        public int DiamondFirstClear = 20;
        public List<EnemySpawn> Enemies = new List<EnemySpawn>();
        public DropTable Drops = new DropTable();

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(Name))
            {
                error = "关卡缺少 ID 或名称";
                return false;
            }

            if (StaminaCost < 0 || TurnLimit <= 0 || ThreeStarRounds <= 0 || GoldReward < 0 || DiamondFirstClear < 0)
            {
                error = $"关卡 {Id} 的数值无效";
                return false;
            }

            if (Enemies.Count == 0)
            {
                error = $"关卡 {Id} 没有敌人";
                return false;
            }

            var cells = new HashSet<int>();
            for (int index = 0; index < Enemies.Count; index++)
            {
                EnemySpawn spawn = Enemies[index];
                if (spawn == null || !BattleGrid.IsValid(spawn.Row, spawn.Col) || spawn.ScalePermille <= 0)
                {
                    error = $"关卡 {Id} 第 {index + 1} 个敌人的位置或缩放无效";
                    return false;
                }

                if (!cells.Add(spawn.Row * BattleGrid.Columns + spawn.Col))
                {
                    error = $"关卡 {Id} 有两个敌人占用同一格";
                    return false;
                }
            }

            if (Drops == null || Drops.Rolls < 0)
            {
                error = $"关卡 {Id} 的掉落表无效";
                return false;
            }

            for (int index = 0; index < Drops.Entries.Count; index++)
            {
                DropEntry entry = Drops.Entries[index];
                if (entry == null || string.IsNullOrWhiteSpace(entry.ItemId) || entry.Weight <= 0 ||
                    entry.Min < 0 || entry.Max < entry.Min)
                {
                    error = $"关卡 {Id} 第 {index + 1} 条掉落无效";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }
    }

    [Serializable]
    public sealed class TacticsManifest
    {
        public int SchemaVersion = 1;
        public List<SkillDefinition> Skills = new List<SkillDefinition>();
        public List<UnitDefinition> Units = new List<UnitDefinition>();
        public List<StageDefinition> Stages = new List<StageDefinition>();

        public SkillDefinition FindSkill(string id)
        {
            for (int index = 0; index < Skills.Count; index++)
                if (Skills[index].Id == id) return Skills[index];
            return null;
        }

        public UnitDefinition FindUnit(string id)
        {
            for (int index = 0; index < Units.Count; index++)
                if (Units[index].Id == id) return Units[index];
            return null;
        }

        public StageDefinition FindStage(string id)
        {
            for (int index = 0; index < Stages.Count; index++)
                if (Stages[index].Id == id) return Stages[index];
            return null;
        }

        public bool TryValidate(out string error)
        {
            if (SchemaVersion != 1)
            {
                error = $"tactics.json 版本不支持：{SchemaVersion}";
                return false;
            }

            var skillIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < Skills.Count; index++)
            {
                if (Skills[index] == null)
                {
                    error = $"第 {index + 1} 个技能为空";
                    return false;
                }

                if (!Skills[index].TryValidate(out error)) return false;

                if (!skillIds.Add(Skills[index].Id))
                {
                    error = $"技能 ID 重复：{Skills[index].Id}";
                    return false;
                }
            }

            var unitIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < Units.Count; index++)
            {
                UnitDefinition unit = Units[index];
                if (unit == null)
                {
                    error = $"第 {index + 1} 个单位为空";
                    return false;
                }

                if (!unit.TryValidate(out error)) return false;

                if (!unitIds.Add(unit.Id))
                {
                    error = $"单位 ID 重复：{unit.Id}";
                    return false;
                }

                bool hasBasic = false;
                for (int skillIndex = 0; skillIndex < unit.SkillIds.Count; skillIndex++)
                {
                    SkillDefinition skill = FindSkill(unit.SkillIds[skillIndex]);
                    if (skill == null)
                    {
                        error = $"单位 {unit.Id} 引用了不存在的技能：{unit.SkillIds[skillIndex]}";
                        return false;
                    }

                    if (skill.Cooldown == 0) hasBasic = true;
                }

                if (!hasBasic)
                {
                    error = $"单位 {unit.Id} 必须至少有一个无冷却技能";
                    return false;
                }
            }

            var stageIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < Stages.Count; index++)
            {
                StageDefinition stage = Stages[index];
                if (stage == null)
                {
                    error = $"第 {index + 1} 个关卡为空";
                    return false;
                }

                if (!stage.TryValidate(out error)) return false;

                if (!stageIds.Add(stage.Id))
                {
                    error = $"关卡 ID 重复：{stage.Id}";
                    return false;
                }

                for (int enemyIndex = 0; enemyIndex < stage.Enemies.Count; enemyIndex++)
                {
                    if (FindUnit(stage.Enemies[enemyIndex].UnitId) == null)
                    {
                        error = $"关卡 {stage.Id} 引用了不存在的单位：{stage.Enemies[enemyIndex].UnitId}";
                        return false;
                    }
                }
            }

            error = string.Empty;
            return true;
        }
    }

    public static class BattleGrid
    {
        public const int Rows = 3;
        public const int Columns = 3;

        public static bool IsValid(int row, int col) => row >= 0 && row < Rows && col >= 0 && col < Columns;

        /// <summary>Cells covered by <paramref name="pattern"/> when anchored at (row, col).</summary>
        public static List<(int Row, int Col)> Cells(string pattern, int row, int col)
        {
            var cells = new List<(int, int)>();
            if (!IsValid(row, col)) return cells;

            switch (pattern)
            {
                case SkillPattern.All:
                    for (int r = 0; r < Rows; r++)
                        for (int c = 0; c < Columns; c++) cells.Add((r, c));
                    break;
                case SkillPattern.Row:
                    for (int c = 0; c < Columns; c++) cells.Add((row, c));
                    break;
                case SkillPattern.Column:
                    for (int r = 0; r < Rows; r++) cells.Add((r, col));
                    break;
                case SkillPattern.Plus:
                    cells.Add((row, col));
                    if (IsValid(row - 1, col)) cells.Add((row - 1, col));
                    if (IsValid(row + 1, col)) cells.Add((row + 1, col));
                    if (IsValid(row, col - 1)) cells.Add((row, col - 1));
                    if (IsValid(row, col + 1)) cells.Add((row, col + 1));
                    break;
                default:
                    cells.Add((row, col));
                    break;
            }

            return cells;
        }
    }
}
