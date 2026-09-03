using System;
using System.Collections.Generic;

namespace ChoSiren.Systems.Tactics
{
    public enum BattleSide
    {
        Player = 0,
        Enemy = 1
    }

    public enum BattleOutcome
    {
        Ongoing,
        Victory,
        Defeat
    }

    public sealed class StatusEffect
    {
        public string Effect;
        public int Permille;
        public int RoundsLeft;
    }

    public sealed class BattleUnit
    {
        public int Id;
        public UnitDefinition Definition;
        public BattleSide Side;
        public int Row;
        public int Col;
        public int Level = 1;
        public int MaxHp;
        public int Hp;
        public int BaseAttack;
        public int BaseDefense;
        public int Speed;
        public int Shield;
        public readonly Dictionary<string, int> Cooldowns = new Dictionary<string, int>(StringComparer.Ordinal);
        public readonly List<StatusEffect> Statuses = new List<StatusEffect>();

        public bool Alive => Hp > 0;

        public int Attack => ApplyStatus(BaseAttack, SkillEffect.BuffAttack);
        public int Defense => ApplyStatus(BaseDefense, SkillEffect.DebuffDefense);

        private int ApplyStatus(int baseValue, string effect)
        {
            int permille = 1000;
            for (int index = 0; index < Statuses.Count; index++)
            {
                if (Statuses[index].Effect != effect) continue;
                permille += effect == SkillEffect.DebuffDefense ? -Statuses[index].Permille : Statuses[index].Permille;
            }

            return Math.Max(0, (int)((long)baseValue * Math.Max(0, permille) / 1000));
        }
    }

    public enum BattleEventKind
    {
        RoundStart,
        Damage,
        Heal,
        Buff,
        Shield,
        Defeated,
        Finished
    }

    public sealed class BattleEvent
    {
        public BattleEventKind Kind;
        public int Round;
        public int ActorId = -1;
        public int TargetId = -1;
        public string SkillId;
        public int Amount;
        public bool Critical;
        public BattleOutcome Outcome;
    }

    public sealed class PlayerUnitSetup
    {
        public string UnitId;
        public int Row;
        public int Col;
        public int Level = 1;
    }

    public sealed class BattleAction
    {
        public int ActorId;
        public string SkillId;
        public int Row;
        public int Col;
        /// <summary>Per-action multiplier supplied by the current dice hand.</summary>
        public int PowerMultiplierPermille = 1000;
    }

    /// <summary>
    /// Brown Dust–style side-view tactics: two 3×3 grids face each other, units act in speed
    /// order, skills hit shaped areas. Integer math plus <see cref="IRandomSource"/> keeps a
    /// battle reproducible from (setup, stage, seed), so results can be verified or replayed.
    /// </summary>
    public sealed class BattleSimulator
    {
        public const int CritMultiplierPermille = 1500;
        public const int DefenseWeight = 4;

        private readonly TacticsManifest manifest;
        private readonly IRandomSource random;
        private readonly List<BattleUnit> units = new List<BattleUnit>();
        private readonly List<int> turnQueue = new List<int>();
        private readonly List<BattleEvent> log = new List<BattleEvent>();
        private int queueIndex;

        public BattleSimulator(TacticsManifest manifest, StageDefinition stage, IReadOnlyList<PlayerUnitSetup> party,
            IRandomSource random)
        {
            this.manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
            Stage = stage ?? throw new ArgumentNullException(nameof(stage));
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            if (party == null || party.Count == 0) throw new ArgumentException("需要至少一名出战成员", nameof(party));

            var occupied = new HashSet<int>();
            for (int index = 0; index < party.Count; index++)
            {
                PlayerUnitSetup setup = party[index];
                UnitDefinition definition = manifest.FindUnit(setup.UnitId)
                    ?? throw new ArgumentException($"未知单位：{setup.UnitId}", nameof(party));
                if (!BattleGrid.IsValid(setup.Row, setup.Col) || !occupied.Add(setup.Row * BattleGrid.Columns + setup.Col))
                    throw new ArgumentException($"成员 {setup.UnitId} 的站位无效或重复", nameof(party));
                AddUnit(definition, BattleSide.Player, setup.Row, setup.Col, Math.Max(1, setup.Level), 1000);
            }

            for (int index = 0; index < stage.Enemies.Count; index++)
            {
                EnemySpawn spawn = stage.Enemies[index];
                UnitDefinition definition = manifest.FindUnit(spawn.UnitId)
                    ?? throw new ArgumentException($"关卡引用了未知单位：{spawn.UnitId}", nameof(stage));
                AddUnit(definition, BattleSide.Enemy, spawn.Row, spawn.Col, 1, spawn.ScalePermille);
            }

            StartRound();
        }

        public StageDefinition Stage { get; }
        public int Round { get; private set; }
        public BattleOutcome Outcome { get; private set; } = BattleOutcome.Ongoing;
        public IReadOnlyList<BattleUnit> Units => units;
        public IReadOnlyList<BattleEvent> Log => log;
        public int PlayerUnitsLost { get; private set; }

        public BattleUnit CurrentActor =>
            Outcome == BattleOutcome.Ongoing && queueIndex < turnQueue.Count ? FindUnit(turnQueue[queueIndex]) : null;

        public SkillDefinition LookupSkill(string id) => manifest.FindSkill(id);

        public BattleUnit FindUnit(int id)
        {
            for (int index = 0; index < units.Count; index++)
                if (units[index].Id == id) return units[index];
            return null;
        }

        public BattleUnit UnitAt(BattleSide side, int row, int col)
        {
            for (int index = 0; index < units.Count; index++)
            {
                BattleUnit unit = units[index];
                if (unit.Side == side && unit.Row == row && unit.Col == col && unit.Alive) return unit;
            }

            return null;
        }

        public bool IsSkillReady(BattleUnit actor, string skillId)
        {
            if (actor == null || !actor.Definition.SkillIds.Contains(skillId)) return false;
            return !actor.Cooldowns.TryGetValue(skillId, out int remaining) || remaining <= 0;
        }

        /// <summary>Anchor cells for <paramref name="skillId"/> that would affect at least one living unit.</summary>
        public List<(int Row, int Col)> LegalAnchors(BattleUnit actor, string skillId)
        {
            var anchors = new List<(int, int)>();
            SkillDefinition skill = manifest.FindSkill(skillId);
            if (actor == null || skill == null || !IsSkillReady(actor, skillId)) return anchors;

            BattleSide targetSide = TargetSideFor(actor, skill);
            for (int row = 0; row < BattleGrid.Rows; row++)
            {
                for (int col = 0; col < BattleGrid.Columns; col++)
                {
                    if (UnitAt(targetSide, row, col) == null) continue;
                    anchors.Add((row, col));
                }
            }

            return anchors;
        }

        public List<BattleUnit> AffectedUnits(BattleUnit actor, SkillDefinition skill, int row, int col)
        {
            var affected = new List<BattleUnit>();
            BattleSide targetSide = TargetSideFor(actor, skill);
            List<(int Row, int Col)> cells = BattleGrid.Cells(skill.Pattern, row, col);
            for (int index = 0; index < cells.Count; index++)
            {
                BattleUnit unit = UnitAt(targetSide, cells[index].Row, cells[index].Col);
                if (unit != null) affected.Add(unit);
            }

            return affected;
        }

        public int PreviewDamage(BattleUnit actor, SkillDefinition skill, BattleUnit target, bool critical = false,
            int powerMultiplierPermille = 1000)
        {
            int multiplier = ClampActionMultiplier(powerMultiplierPermille);
            long raw = (long)actor.Attack * skill.PowerPermille / 1000 * multiplier / 1000;
            long mitigated = raw * 1000 / (1000 + (long)target.Defense * DefenseWeight);
            if (critical) mitigated = mitigated * CritMultiplierPermille / 1000;
            return (int)Math.Max(1, mitigated);
        }

        public bool TryAct(BattleAction action, out string error)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (Outcome != BattleOutcome.Ongoing)
            {
                error = "战斗已经结束";
                return false;
            }

            BattleUnit actor = CurrentActor;
            if (actor == null || actor.Id != action.ActorId)
            {
                error = "还没轮到该单位行动";
                return false;
            }

            SkillDefinition skill = manifest.FindSkill(action.SkillId);
            if (skill == null || !IsSkillReady(actor, action.SkillId))
            {
                error = "技能不可用或冷却中";
                return false;
            }

            if (!BattleGrid.IsValid(action.Row, action.Col) ||
                UnitAt(TargetSideFor(actor, skill), action.Row, action.Col) == null)
            {
                error = "目标位置上没有可作用的单位";
                return false;
            }

            Resolve(actor, skill, action.Row, action.Col, ClampActionMultiplier(action.PowerMultiplierPermille));
            if (skill.Cooldown > 0) actor.Cooldowns[skill.Id] = skill.Cooldown;
            EvaluateOutcome();
            if (Outcome == BattleOutcome.Ongoing) AdvanceTurn();
            error = string.Empty;
            return true;
        }

        /// <summary>Runs the battle with both sides controlled by <see cref="EnemyAi"/> until it ends.</summary>
        public BattleOutcome AutoPlay(int maxActions = 400)
        {
            int guard = 0;
            while (Outcome == BattleOutcome.Ongoing && guard++ < maxActions)
            {
                BattleUnit actor = CurrentActor;
                if (actor == null) break;
                BattleAction action = EnemyAi.Choose(this, actor);
                if (action == null || !TryAct(action, out _))
                {
                    AdvanceTurn();
                }
            }

            if (Outcome == BattleOutcome.Ongoing) Finish(BattleOutcome.Defeat);
            return Outcome;
        }

        public int StarRating()
        {
            if (Outcome != BattleOutcome.Victory) return 0;
            int stars = 1;
            if (PlayerUnitsLost == 0) stars++;
            if (Round <= Stage.ThreeStarRounds) stars++;
            return stars;
        }

        private void AddUnit(UnitDefinition definition, BattleSide side, int row, int col, int level, int scalePermille)
        {
            int levelPermille = 1000 + (level - 1) * 30;
            var unit = new BattleUnit
            {
                Id = units.Count + 1,
                Definition = definition,
                Side = side,
                Row = row,
                Col = col,
                Level = level,
                MaxHp = Scale(definition.MaxHp, levelPermille, scalePermille, 1),
                BaseAttack = Scale(definition.Attack, levelPermille, scalePermille, 1),
                BaseDefense = Scale(definition.Defense, levelPermille, scalePermille, 0),
                Speed = definition.Speed,
            };
            unit.Hp = unit.MaxHp;
            units.Add(unit);
        }

        private static int Scale(int value, int levelPermille, int scalePermille, int minimum) =>
            Math.Max(minimum, (int)((long)value * levelPermille / 1000 * scalePermille / 1000));

        private static BattleSide TargetSideFor(BattleUnit actor, SkillDefinition skill)
        {
            bool enemies = SkillEffect.TargetsEnemies(skill.Effect);
            if (actor.Side == BattleSide.Player) return enemies ? BattleSide.Enemy : BattleSide.Player;
            return enemies ? BattleSide.Player : BattleSide.Enemy;
        }

        private static int ClampActionMultiplier(int multiplierPermille) =>
            Math.Max(1000, Math.Min(10000, multiplierPermille));

        private void Resolve(BattleUnit actor, SkillDefinition skill, int row, int col, int powerMultiplierPermille)
        {
            List<BattleUnit> targets = AffectedUnits(actor, skill, row, col);
            for (int index = 0; index < targets.Count; index++)
            {
                BattleUnit target = targets[index];
                switch (skill.Effect)
                {
                    case SkillEffect.Damage:
                    {
                        bool critical = skill.CanCrit && random.NextPermille() < actor.Definition.CritPermille;
                        int damage = PreviewDamage(actor, skill, target, critical, powerMultiplierPermille);
                        int absorbed = Math.Min(target.Shield, damage);
                        target.Shield -= absorbed;
                        int applied = damage - absorbed;
                        target.Hp = Math.Max(0, target.Hp - applied);
                        Emit(BattleEventKind.Damage, actor.Id, target.Id, skill.Id, damage, critical);
                        if (!target.Alive)
                        {
                            if (target.Side == BattleSide.Player) PlayerUnitsLost++;
                            Emit(BattleEventKind.Defeated, actor.Id, target.Id, skill.Id, 0, false);
                        }

                        break;
                    }
                    case SkillEffect.Heal:
                    {
                        int amount = (int)Math.Max(1,
                            (long)actor.Attack * skill.PowerPermille / 1000 * powerMultiplierPermille / 1000);
                        int healed = Math.Min(amount, target.MaxHp - target.Hp);
                        target.Hp += healed;
                        Emit(BattleEventKind.Heal, actor.Id, target.Id, skill.Id, healed, false);
                        break;
                    }
                    case SkillEffect.Shield:
                    {
                        int amount = (int)Math.Max(1,
                            (long)target.MaxHp * skill.PowerPermille / 1000 * powerMultiplierPermille / 1000);
                        target.Shield = Math.Max(target.Shield, amount);
                        Emit(BattleEventKind.Shield, actor.Id, target.Id, skill.Id, amount, false);
                        break;
                    }
                    default:
                    {
                        RefreshStatus(target, skill);
                        Emit(BattleEventKind.Buff, actor.Id, target.Id, skill.Id, skill.PowerPermille, false);
                        break;
                    }
                }
            }
        }

        private static void RefreshStatus(BattleUnit target, SkillDefinition skill)
        {
            for (int index = 0; index < target.Statuses.Count; index++)
            {
                StatusEffect status = target.Statuses[index];
                if (status.Effect != skill.Effect) continue;
                // Re-applying the same effect refreshes rather than stacks, keeping numbers bounded.
                status.Permille = Math.Max(status.Permille, skill.PowerPermille);
                status.RoundsLeft = Math.Max(status.RoundsLeft, skill.Duration);
                return;
            }

            target.Statuses.Add(new StatusEffect
            {
                Effect = skill.Effect,
                Permille = skill.PowerPermille,
                RoundsLeft = skill.Duration
            });
        }

        private void EvaluateOutcome()
        {
            if (!AnyAlive(BattleSide.Enemy)) Finish(BattleOutcome.Victory);
            else if (!AnyAlive(BattleSide.Player)) Finish(BattleOutcome.Defeat);
        }

        private bool AnyAlive(BattleSide side)
        {
            for (int index = 0; index < units.Count; index++)
                if (units[index].Side == side && units[index].Alive) return true;
            return false;
        }

        private void AdvanceTurn()
        {
            queueIndex++;
            while (queueIndex < turnQueue.Count && !FindUnit(turnQueue[queueIndex]).Alive) queueIndex++;
            if (queueIndex >= turnQueue.Count)
            {
                if (Round >= Stage.TurnLimit)
                {
                    Finish(BattleOutcome.Defeat);
                    return;
                }

                EndRound();
                StartRound();
            }
        }

        private void EndRound()
        {
            for (int index = 0; index < units.Count; index++)
            {
                BattleUnit unit = units[index];
                var keys = new List<string>(unit.Cooldowns.Keys);
                for (int keyIndex = 0; keyIndex < keys.Count; keyIndex++)
                    unit.Cooldowns[keys[keyIndex]] = Math.Max(0, unit.Cooldowns[keys[keyIndex]] - 1);

                for (int statusIndex = unit.Statuses.Count - 1; statusIndex >= 0; statusIndex--)
                {
                    unit.Statuses[statusIndex].RoundsLeft--;
                    if (unit.Statuses[statusIndex].RoundsLeft <= 0) unit.Statuses.RemoveAt(statusIndex);
                }
            }
        }

        private void StartRound()
        {
            Round++;
            turnQueue.Clear();
            queueIndex = 0;

            var order = new List<BattleUnit>();
            for (int index = 0; index < units.Count; index++)
                if (units[index].Alive) order.Add(units[index]);
            // Stable ordering: faster first, players win ties, then creation order.
            order.Sort((a, b) =>
            {
                int bySpeed = b.Speed.CompareTo(a.Speed);
                if (bySpeed != 0) return bySpeed;
                int bySide = a.Side.CompareTo(b.Side);
                return bySide != 0 ? bySide : a.Id.CompareTo(b.Id);
            });
            for (int index = 0; index < order.Count; index++) turnQueue.Add(order[index].Id);

            Emit(BattleEventKind.RoundStart, -1, -1, null, Round, false);
        }

        private void Finish(BattleOutcome outcome)
        {
            Outcome = outcome;
            log.Add(new BattleEvent { Kind = BattleEventKind.Finished, Round = Round, Outcome = outcome });
        }

        private void Emit(BattleEventKind kind, int actorId, int targetId, string skillId, int amount, bool critical)
        {
            log.Add(new BattleEvent
            {
                Kind = kind,
                Round = Round,
                ActorId = actorId,
                TargetId = targetId,
                SkillId = skillId,
                Amount = amount,
                Critical = critical
            });
        }
    }

    /// <summary>
    /// Greedy scorer used for enemies and for auto-battle. Deterministic: ties resolve by skill
    /// order then grid order, so the same state always yields the same action.
    /// </summary>
    public static class EnemyAi
    {
        public static BattleAction Choose(BattleSimulator battle, BattleUnit actor)
        {
            if (battle == null || actor == null) return null;

            BattleAction best = null;
            long bestScore = long.MinValue;

            for (int skillIndex = 0; skillIndex < actor.Definition.SkillIds.Count; skillIndex++)
            {
                string skillId = actor.Definition.SkillIds[skillIndex];
                SkillDefinition skill = battle.LookupSkill(skillId);
                if (skill == null) continue;
                List<(int Row, int Col)> anchors = battle.LegalAnchors(actor, skillId);
                for (int anchorIndex = 0; anchorIndex < anchors.Count; anchorIndex++)
                {
                    (int row, int col) = anchors[anchorIndex];
                    long score = Score(battle, actor, skill, row, col);
                    if (score <= bestScore) continue;
                    bestScore = score;
                    best = new BattleAction { ActorId = actor.Id, SkillId = skillId, Row = row, Col = col };
                }
            }

            return best;
        }

        private static long Score(BattleSimulator battle, BattleUnit actor, SkillDefinition skill, int row, int col)
        {
            List<BattleUnit> targets = battle.AffectedUnits(actor, skill, row, col);
            long score = 0;
            switch (skill.Effect)
            {
                case SkillEffect.Damage:
                    for (int index = 0; index < targets.Count; index++)
                    {
                        int damage = battle.PreviewDamage(actor, skill, targets[index]);
                        int effective = Math.Min(damage, targets[index].Hp + targets[index].Shield);
                        score += effective;
                        if (damage >= targets[index].Hp + targets[index].Shield) score += 400;
                    }

                    break;
                case SkillEffect.Heal:
                    for (int index = 0; index < targets.Count; index++)
                    {
                        int missing = targets[index].MaxHp - targets[index].Hp;
                        // Healing is only worth taking when someone is actually hurt.
                        score += missing > targets[index].MaxHp / 4 ? missing : -1000;
                    }

                    break;
                case SkillEffect.Shield:
                    for (int index = 0; index < targets.Count; index++)
                        score += targets[index].Shield == 0 ? targets[index].MaxHp / 3 : -500;
                    break;
                default:
                    for (int index = 0; index < targets.Count; index++)
                    {
                        bool already = false;
                        for (int statusIndex = 0; statusIndex < targets[index].Statuses.Count; statusIndex++)
                            if (targets[index].Statuses[statusIndex].Effect == skill.Effect) already = true;
                        score += already ? -800 : targets[index].Attack * 2;
                    }

                    break;
            }

            // Prefer using a cooldown skill when it is ready so specials are not wasted.
            if (skill.Cooldown > 0 && score > 0) score += 50;
            return score;
        }
    }

    public static class DropResolver
    {
        public static List<(string ItemId, int Amount)> Roll(DropTable table, IRandomSource random)
        {
            var results = new List<(string, int)>();
            if (table == null || table.Entries.Count == 0 || random == null) return results;

            int totalWeight = 0;
            for (int index = 0; index < table.Entries.Count; index++) totalWeight += table.Entries[index].Weight;
            if (totalWeight <= 0) return results;

            for (int roll = 0; roll < table.Rolls; roll++)
            {
                int pick = random.Next(totalWeight);
                for (int index = 0; index < table.Entries.Count; index++)
                {
                    DropEntry entry = table.Entries[index];
                    pick -= entry.Weight;
                    if (pick >= 0) continue;
                    int amount = entry.Min + (entry.Max > entry.Min ? random.Next(entry.Max - entry.Min + 1) : 0);
                    if (amount > 0) Merge(results, entry.ItemId, amount);
                    break;
                }
            }

            return results;
        }

        private static void Merge(List<(string ItemId, int Amount)> results, string itemId, int amount)
        {
            for (int index = 0; index < results.Count; index++)
            {
                if (results[index].ItemId != itemId) continue;
                results[index] = (itemId, results[index].Amount + amount);
                return;
            }

            results.Add((itemId, amount));
        }
    }
}
