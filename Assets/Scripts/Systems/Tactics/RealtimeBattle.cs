using System;
using System.Collections.Generic;
using ChoSiren.Systems.Dice;

namespace ChoSiren.Systems.Tactics
{
    public enum CombatRace { None, Demon, Charm, Mermaid, BloodElf }

    public sealed partial class BattleSimulator
    {
        public const int RealtimeStepMilliseconds = 25;
        public const int BattleTimeLimitMilliseconds = 60000;
        private const int MeasureMilliseconds = 4000;

        private sealed class PerformerClock
        {
            public CombatRace Race;
            public int NextBasic, NextSmall, NextBig;
            public int HasteStacks, ExtraAttacks;
            public int SilencedUntil, AntiHealUntil, ReductionUntil, AttackBuffUntil, HardenedUntil;
            public int TimedShieldUntil, TimedShield;
            public int PoisonStacks, PoisonSource;
            public int StunnedUntil, SlowUntil, ArmorBrokenUntil, ControlWardUntil, ControlRecoveryUntil;
            public int CastUntil, CastTargetId;
        }

        private static readonly Dictionary<string, SkillDefinition> realtimeSkills = CreateRealtimeSkills();
        private readonly Dictionary<int, PerformerClock> performerClocks = new Dictionary<int, PerformerClock>();
        private int realtimeRemainder, diceRevision, leaderId, nextMeasure = MeasureMilliseconds;
        private bool elfRescueUsed;
        private int encounterLeadId, nextEliteHarden = 8000;
        private CombatRace leaderRace;

        public bool IsRealtime { get; private set; }
        public int CurrentLeaderId => IsRealtime ? leaderId : -1;
        public CombatRace CurrentLeaderRace => IsRealtime ? leaderRace : CombatRace.None;
        public int ElapsedMilliseconds { get; private set; }
        public int FocusTargetId { get; private set; } = -1;
        public int TimeLimitMilliseconds => Stage.TimeLimitSeconds * 1000;
        public int CurrentWave { get; private set; } = 1;
        public int TotalWaves
        {
            get { int count = 1; foreach (var unit in units) count = Math.Max(count, unit.Wave + 1); return count; }
        }
        public DiceTurn BattleDice { get; private set; }
        public long CharacterDamageDealt { get; private set; }
        public long PoisonDamageDealt { get; private set; }
        public long BasicAndActiveDamageDealt { get; private set; }
        public bool WorldEncounter { get; private set; }

        public static CombatRace ParseCombatRace(string label)
        {
            string value = label ?? string.Empty;
            if (value.Contains("人鱼") || value.Contains("海灵")) return CombatRace.Mermaid;
            if (value.Contains("血精灵")) return CombatRace.BloodElf;
            if (value.Contains("恶魔") || value.Contains("魔族")) return CombatRace.Demon;
            if (value.Contains("魅族")) return CombatRace.Charm;
            return CombatRace.None;
        }

        /// <summary>
        /// Starts independent basic/small/big clocks on the existing unit and settlement state.
        /// The old turn API remains available to old replay fixtures, but cannot mutate this mode.
        /// </summary>
        public void EnableRealtime(IReadOnlyDictionary<string, string> races, string captainUnitId = null,
            int rerollLimit = 4, bool worldEncounter = false)
        {
            if (IsRealtime) return;
            if (Outcome != BattleOutcome.Ongoing || Round != 1 || log.Count != 1)
                throw new InvalidOperationException("只能在战斗开始前初始化实时规则");
            if (rerollLimit < 1 || rerollLimit > 5) throw new ArgumentOutOfRangeException(nameof(rerollLimit));
            WorldEncounter = worldEncounter;
            foreach (BattleUnit unit in units)
            {
                if (unit.Side == BattleSide.Enemy) unit.Spawned = unit.Wave == 0;
                string label = null;
                races?.TryGetValue(unit.Definition.Id, out label);
                var clock = new PerformerClock { Race = unit.Side == BattleSide.Player
                    ? ParseCombatRace(label) : CombatRace.None };
                performerClocks.Add(unit.Id, clock);
                if (unit.Side == BattleSide.Enemy &&
                    (encounterLeadId == 0 || unit.MaxHp > FindUnit(encounterLeadId).MaxHp)) encounterLeadId = unit.Id;
                if (unit.Side == BattleSide.Player && (leaderId == 0 || unit.Definition.Id == captainUnitId))
                {
                    leaderId = unit.Id;
                    leaderRace = clock.Race;
                }
                clock.NextBasic = BasicInterval(unit, clock);
                clock.NextSmall = unit.Side == BattleSide.Player ? SmallCooldown(clock.Race) : 4000;
                clock.NextBig = BigCooldown(clock.Race);
            }
            BattleDice = DiceTurn.ForBattle(random, rerollLimit, leaderRace == CombatRace.Mermaid);
            BattleDice.Begin();
            IsRealtime = true;
            // The opening hand becomes active on the first logical tick, never while UI opens/pauses.
        }

        /// <summary>Fixed-step logical time. The caller applies speed and supplies zero time while paused.</summary>
        public void AdvanceRealtime(int milliseconds, bool autoDice = false)
        {
            if (!IsRealtime) throw new InvalidOperationException("尚未初始化实时战斗");
            if (milliseconds < 0 || milliseconds > Math.Max(BattleTimeLimitMilliseconds, TimeLimitMilliseconds))
                throw new ArgumentOutOfRangeException(nameof(milliseconds));
            if (Outcome != BattleOutcome.Ongoing || milliseconds == 0) return;
            realtimeRemainder += milliseconds;
            while (realtimeRemainder >= RealtimeStepMilliseconds && Outcome == BattleOutcome.Ongoing)
            {
                realtimeRemainder -= RealtimeStepMilliseconds;
                ElapsedMilliseconds += RealtimeStepMilliseconds;
                ApplyNewHand();
                if (autoDice && BattleDice.CanEnergyReroll && BattleDice.Hand.MultiplierPermille < 1600)
                {
                    AutoReroll();
                    ApplyNewHand();
                }
                ExpireTimedEffects();
                if (Stage.EncounterType == "elite" && ElapsedMilliseconds >= nextEliteHarden)
                {
                    BattleUnit elite = FindUnit(encounterLeadId);
                    if (elite != null && elite.Alive)
                    {
                        performerClocks[elite.Id].HardenedUntil = ElapsedMilliseconds + 2000;
                        Emit(BattleEventKind.Buff, elite.Id, elite.Id, "rt-elite-harden", 500, false);
                    }
                    nextEliteHarden += 8000;
                }
                foreach (BattleUnit actor in units)
                {
                    if (!actor.Alive || Outcome != BattleOutcome.Ongoing) continue;
                    PerformerClock clock = performerClocks[actor.Id];
                    if (clock.StunnedUntil > ElapsedMilliseconds) continue;
                    if (clock.CastUntil > 0)
                    {
                        if (ElapsedMilliseconds >= clock.CastUntil)
                        {
                            clock.CastUntil = 0;
                            ResolveTelegraphedSpecial(actor);
                            clock.NextSmall = ElapsedMilliseconds + 6000;
                            clock.NextBasic = Math.Max(clock.NextBasic, ElapsedMilliseconds + 350);
                        }
                        continue;
                    }
                    if (ElapsedMilliseconds >= clock.NextBasic)
                    {
                        BasicAttack(actor);
                        clock.NextBasic = ElapsedMilliseconds + BasicInterval(actor, clock);
                    }
                    if (Outcome != BattleOutcome.Ongoing) break;
                    // Silence suppresses active skills only. Basic attacks never stop.
                    if (clock.SilencedUntil > ElapsedMilliseconds) continue;
                    if (ElapsedMilliseconds >= clock.NextSmall)
                    {
                        if (actor.Side == BattleSide.Enemy && Stage.UsesRealtime) BeginTelegraphedSpecial(actor);
                        else if (actor.Side == BattleSide.Enemy) EnemySpecial(actor);
                        else CastRaceSkill(actor, false);
                        clock.NextSmall = ElapsedMilliseconds +
                            (actor.Side == BattleSide.Enemy ? 4000 : SmallCooldown(clock.Race));
                    }
                    if (Outcome == BattleOutcome.Ongoing && actor.Side == BattleSide.Player &&
                        ElapsedMilliseconds >= clock.NextBig)
                    {
                        CastRaceSkill(actor, true);
                        clock.NextBig = ElapsedMilliseconds + BigCooldown(clock.Race);
                    }
                }
                if (Outcome == BattleOutcome.Ongoing && ElapsedMilliseconds >= nextMeasure)
                {
                    ResolvePoison();
                    Round++;
                    nextMeasure += MeasureMilliseconds;
                    ResetComboBudget();
                }
                if (Outcome == BattleOutcome.Ongoing && ElapsedMilliseconds >= TimeLimitMilliseconds)
                    Finish(BattleOutcome.Defeat);
            }
        }

        private void ActivateEnemy(BattleUnit unit)
        {
            unit.Spawned = true;
            var clock = performerClocks[unit.Id];
            clock.NextBasic = ElapsedMilliseconds + BasicInterval(unit, clock);
            clock.NextSmall = ElapsedMilliseconds + 4000;
            CurrentWave = Math.Max(CurrentWave, unit.Wave + 1);
            Emit(BattleEventKind.Spawned, unit.Id, unit.Id, "reinforcement", CurrentWave, false);
        }

        private bool SpawnNextWave()
        {
            int next = int.MaxValue;
            foreach (var unit in units)
                if (unit.Side == BattleSide.Enemy && !unit.Spawned) next = Math.Min(next, unit.Wave);
            if (next == int.MaxValue) return false;
            foreach (var unit in units)
                if (unit.Side == BattleSide.Enemy && !unit.Spawned && unit.Wave == next) ActivateEnemy(unit);
            return true;
        }

        private void SpawnPhaseReinforcements(int phase)
        {
            foreach (var unit in units)
                if (unit.Side == BattleSide.Enemy && !unit.Spawned && unit.SpawnAtBossPhase == phase)
                    ActivateEnemy(unit);
        }

        public bool FocusEnemy(int id)
        {
            BattleUnit target = FindUnit(id);
            if (!IsRealtime || Outcome != BattleOutcome.Ongoing || target == null ||
                !target.Alive || target.Side != BattleSide.Enemy) return false;
            FocusTargetId = id;
            return true;
        }

        public CombatRace RaceOf(BattleUnit unit) => unit != null && performerClocks.TryGetValue(unit.Id, out var clock)
            ? clock.Race : CombatRace.None;

        public string ActiveSkillId(BattleUnit unit, bool big) => SkillId(RaceOf(unit), big);
        public static string ActiveSkillName(CombatRace race, bool big) => realtimeSkills[SkillId(race, big)].Name;

        public static string ActiveSkillDescription(CombatRace race, bool big)
        {
            switch (race)
            {
                case CombatRace.Demon: return big ? "全体160%伤害，禁疗30%，持续3秒"
                    : "单体110%伤害，附加2层毒；迟缓3秒，普攻间隔+35%";
                case CombatRace.Charm: return big ? "前排175%伤害，眩晕1.2秒并打断蓄力，封技2秒；首领控时减半"
                    : "两段共120%伤害，自身攻速叠加5%";
                case CombatRace.Mermaid: return big ? "全队18%护盾、减伤15%持续4秒；免疫眩晕2秒"
                    : "每人净化1个负面状态并恢复8%生命，不复活阵亡成员";
                case CombatRace.BloodElf: return big ? "敌人生命低于35%时造成200%伤害，击杀增攻10%"
                    : "单体115%伤害，无视15%防御；破甲30%持续4秒，供全队集火";
                default: return big ? "单体160%伤害，自动释放" : "单体110%伤害，自动释放";
            }
        }

        public int SkillCooldownRemaining(BattleUnit unit, bool big)
        {
            if (unit == null || !performerClocks.TryGetValue(unit.Id, out var clock)) return 0;
            return Math.Max(0, (big ? clock.NextBig : clock.NextSmall) - ElapsedMilliseconds);
        }

        public string RealtimeStatus(BattleUnit unit)
        {
            if (unit == null || !unit.Alive || !performerClocks.TryGetValue(unit.Id, out var clock)) return string.Empty;
            string tactical = TacticalStatus(unit);
            if (!string.IsNullOrEmpty(tactical)) return tactical;
            if (clock.SilencedUntil > ElapsedMilliseconds) return "封技";
            if (clock.HardenedUntil > ElapsedMilliseconds) return "硬化";
            if (clock.PoisonStacks > 0) return $"毒 {clock.PoisonStacks}";
            if (clock.AntiHealUntil > ElapsedMilliseconds) return "禁疗30%";
            if (clock.ReductionUntil > ElapsedMilliseconds) return "减伤15%";
            if (clock.AttackBuffUntil > ElapsedMilliseconds) return "攻击+10%";
            if (unit.Shield > 0) return "护盾";
            return string.Empty;
        }

        private static int SmallCooldown(CombatRace race) => race == CombatRace.Charm ? 2500
            : race == CombatRace.BloodElf ? 2800 : 3000;
        private static int BigCooldown(CombatRace race) => race == CombatRace.Demon ? 12000
            : race == CombatRace.Charm ? 11000 : race == CombatRace.Mermaid ? 13000 : 10000;

        private int BasicInterval(BattleUnit actor, PerformerClock clock) =>
            (int)Math.Max(250, 100000000L / ((long)Math.Max(1, actor.Speed) * (1000 + clock.HasteStacks * 50)))
            * (clock.SlowUntil > ElapsedMilliseconds ? 135 : 100) / 100;

        private BattleUnit SelectEnemy(BattleUnit actor)
        {
            if (actor.Side == BattleSide.Player)
            {
                BattleUnit focus = FindUnit(FocusTargetId);
                if (focus != null && focus.Alive && focus.Side == BattleSide.Enemy) return focus;
            }
            BattleUnit best = null;
            foreach (BattleUnit candidate in units)
            {
                if (!candidate.Alive || candidate.Side == actor.Side) continue;
                if (best == null || candidate.Row < best.Row ||
                    candidate.Row == best.Row && candidate.Hp < best.Hp) best = candidate;
            }
            return best;
        }

        private void BasicAttack(BattleUnit actor)
        {
            BattleUnit target = SelectEnemy(actor);
            if (target == null) return;
            Emit(BattleEventKind.ActionStarted, actor.Id, target.Id, "rt-basic", 0, false);
            Deal(actor, target, "rt-basic", 1000);
            if (actor.Side != BattleSide.Player || Outcome != BattleOutcome.Ongoing) return;
            if (leaderRace == CombatRace.Demon && target.Alive)
                AddPoison(target, actor, PoisonLayers(BattleDice.Hand.Pattern));
            PerformerClock clock = performerClocks[actor.Id];
            if (leaderRace == CombatRace.Charm && clock.ExtraAttacks > 0)
            {
                clock.ExtraAttacks--;
                target = SelectEnemy(actor);
                if (target != null) Deal(actor, target, "rt-charm-follow", 1000);
            }
        }

        private void CastRaceSkill(BattleUnit actor, bool big)
        {
            PerformerClock clock = performerClocks[actor.Id];
            string skill = SkillId(clock.Race, big);
            BattleUnit target = SelectEnemy(actor);
            Emit(BattleEventKind.ActionStarted, actor.Id,
                clock.Race == CombatRace.Mermaid || target == null ? actor.Id : target.Id, skill, 0, false);
            int utilityMultiplier = BattleDice.Hand.Pattern == DicePattern.FiveKind ? 1500 : 1000;
            switch (clock.Race)
            {
                case CombatRace.Demon:
                    if (!big)
                    {
                        if (target == null) return;
                        Deal(actor, target, skill, 1100);
                        if (target.Alive) AddPoison(target, actor, 2);
                        ApplyCondition(actor, target, CombatCondition.Slow, 3000);
                    }
                    else foreach (BattleUnit enemy in LivingOpponents(actor))
                    {
                        if (!enemy.Alive || enemy.Side == actor.Side) continue;
                        Deal(actor, enemy, skill, 1600);
                        performerClocks[enemy.Id].AntiHealUntil = ElapsedMilliseconds + 3000;
                    }
                    break;
                case CombatRace.Charm:
                    if (target == null) return;
                    if (!big)
                    {
                        Deal(actor, target, skill, 600);
                        target = SelectEnemy(actor);
                        if (target != null) Deal(actor, target, skill, 600);
                        // Limited by the 60s encounter; defensive cap avoids runaway imported stats.
                        clock.HasteStacks = Math.Min(24, clock.HasteStacks + 1);
                    }
                    else
                    {
                        int frontRow = target.Row;
                        foreach (BattleUnit enemy in LivingOpponents(actor))
                        {
                            if (!enemy.Alive || enemy.Side == actor.Side || enemy.Row != frontRow) continue;
                            Deal(actor, enemy, skill, 1750);
                            performerClocks[enemy.Id].SilencedUntil = ElapsedMilliseconds + 2000;
                            ApplyCondition(actor, enemy, CombatCondition.Stun, 1200);
                        }
                    }
                    break;
                case CombatRace.Mermaid:
                    foreach (BattleUnit ally in units)
                    {
                        if (!ally.Alive || ally.Side != actor.Side) continue;
                        if (!big)
                        {
                            CleanseOne(actor, ally);
                            HealPercent(actor, ally, skill, 80 * utilityMultiplier / 1000);
                        }
                        else
                        {
                            AddShield(actor, ally, skill, 180 * utilityMultiplier / 1000, true);
                            performerClocks[ally.Id].ReductionUntil = ElapsedMilliseconds + 4000;
                            ApplyCondition(actor, ally, CombatCondition.ControlWard, 2000);
                        }
                    }
                    break;
                case CombatRace.BloodElf:
                    if (big)
                    {
                        foreach (BattleUnit enemy in units)
                            if (enemy.Alive && enemy.Side != actor.Side &&
                                (target == null || (long)enemy.Hp * target.MaxHp < (long)target.Hp * enemy.MaxHp))
                                target = enemy;
                    }
                    if (target == null) return;
                    int power = big ? ((long)target.Hp * 100 < (long)target.MaxHp * 35 ? 2000 : 1150) : 1150;
                    Deal(actor, target, skill, power, big ? 0 : 150);
                    if (!big) ApplyCondition(actor, target, CombatCondition.ArmorBreak, 4000);
                    if (big && !target.Alive) clock.AttackBuffUntil = ElapsedMilliseconds + 3000;
                    break;
                default:
                    if (target != null) Deal(actor, target, skill, big ? 1600 : 1100);
                    break;
            }
        }

        // AOE targets are selected when the cast starts. Reinforcements cannot be hit by
        // the same cast that cleared the previous wave or triggered their arrival.
        private List<BattleUnit> LivingOpponents(BattleUnit actor)
        {
            var targets = new List<BattleUnit>();
            foreach (var unit in units) if (unit.Alive && unit.Side != actor.Side) targets.Add(unit);
            return targets;
        }

        private void EnemySpecial(BattleUnit actor)
        {
            if (Stage.UsesRealtime)
            {
                BattleUnit target = SelectEnemy(actor);
                if (target == null) return;
                if (Stage.HasBossPhases && actor.Id == encounterLeadId && EnemyPhase >= 3)
                {
                    Emit(BattleEventKind.ActionStarted, actor.Id, target.Id, "rt-enemy-finale", 0, false);
                    foreach (BattleUnit ally in units)
                        if (ally.Alive && ally.Side == BattleSide.Player) Deal(actor, ally, "rt-enemy-finale", 1400);
                }
                else
                {
                    Emit(BattleEventKind.ActionStarted, actor.Id, target.Id, "rt-enemy-heavy", 0, false);
                    Deal(actor, target, "rt-enemy-heavy", 1600);
                }
                return;
            }
            BattleAction chosen = EnemyAi.Choose(this, actor);
            SkillDefinition skill = chosen == null ? null : LookupSkill(chosen.SkillId);
            if (skill == null) return;
            List<BattleUnit> targets = AffectedUnits(actor, skill, chosen.Row, chosen.Col);
            Emit(BattleEventKind.ActionStarted, actor.Id, targets.Count > 0 ? targets[0].Id : actor.Id,
                skill.Id, 0, false);
            foreach (BattleUnit target in targets)
            {
                if (skill.Effect == SkillEffect.Damage)
                    Deal(actor, target, skill.Id, Math.Max(1400, skill.PowerPermille));
                else if (skill.Effect == SkillEffect.Heal)
                {
                    int healing = (int)Math.Min(int.MaxValue, (long)actor.Attack * skill.PowerPermille / 1000);
                    HealAmount(actor, target, skill.Id, healing);
                }
                else if (skill.Effect == SkillEffect.Shield) AddShield(actor, target, skill.Id, skill.PowerPermille, true);
                else RefreshStatus(target, skill);
            }
        }

        private void Deal(BattleUnit actor, BattleUnit target, string skill, int power, int ignoreDefense = 0)
        {
            if (!actor.Alive || !target.Alive || Outcome != BattleOutcome.Ongoing) return;
            long raw = (long)actor.Attack * power / 1000;
            bool player = actor.Side == BattleSide.Player;
            if (player)
            {
                raw = raw * BattleDice.Hand.MultiplierPermille / 1000;
                if (performerClocks[actor.Id].AttackBuffUntil > ElapsedMilliseconds) raw = raw * 1100 / 1000;
                if (leaderRace == CombatRace.BloodElf)
                {
                    ignoreDefense = Math.Max(ignoreDefense, Pierce(BattleDice.Hand.Pattern));
                    if ((long)target.Hp * 100 < (long)target.MaxHp * 30) raw = raw * 1250 / 1000;
                }
                if (BattleDice.Hand.Pattern == DicePattern.TwoPair) raw = raw * 1080 / 1000;
                if (BattleDice.Hand.Pattern == DicePattern.Pair && actor.Id == leaderId) raw = raw * 1100 / 1000;
            }
            int defense = (int)((long)EffectiveRealtimeDefense(target) * (1000 - Math.Min(500, ignoreDefense)) / 1000);
            raw = raw * 1000 / (1000 + (long)defense * DefenseWeight);
            bool critical = random.NextPermille() < actor.Definition.CritPermille;
            if (critical) raw = raw * CritMultiplierPermille / 1000;
            if (performerClocks[target.Id].ReductionUntil > ElapsedMilliseconds) raw = raw * 850 / 1000;
            if (performerClocks[target.Id].HardenedUntil > ElapsedMilliseconds) raw = raw * 500 / 1000;
            if (target.Side == BattleSide.Player && leaderRace == CombatRace.Mermaid &&
                BattleDice.Hand.Pattern == DicePattern.FiveKind) raw = raw * 750 / 1000;
            ApplyActualDamage(actor, target, skill, (int)Math.Max(1, Math.Min(int.MaxValue, raw)), critical);
        }

        private void ApplyActualDamage(BattleUnit actor, BattleUnit target, string skill, int damage, bool critical)
        {
            if (!target.Alive || Outcome != BattleOutcome.Ongoing) return;
            int absorbed = Math.Min(target.Shield, damage);
            target.Shield -= absorbed;
            PerformerClock targetClock = performerClocks[target.Id];
            targetClock.TimedShield = Math.Max(0, targetClock.TimedShield - absorbed);
            int lostHp = Math.Min(target.Hp, damage - absorbed);
            target.Hp -= lostHp;
            int applied = absorbed + lostHp;
            Emit(BattleEventKind.Damage, actor.Id, target.Id, skill, applied, critical);
            if (actor.Side == BattleSide.Player && target.Side == BattleSide.Enemy)
            {
                CharacterDamageDealt += applied;
                if (skill == "rt-poison") PoisonDamageDealt += applied;
                else BasicAndActiveDamageDealt += applied;
                int gain = BattleDice.Hand.Pattern == DicePattern.ThreeKind ? 1300 : 1000;
                if (WorldEncounter) gain = gain * 750 / 1000;
                BattleDice.RecordDamage(applied, !target.Alive, gain);
            }
            if (!target.Alive)
            {
                if (target.Side == BattleSide.Player) PlayerUnitsLost++;
                Emit(BattleEventKind.Defeated, actor.Id, target.Id, skill, 0, false);
                if (target.Side == BattleSide.Player && target.Id == leaderId) TransferBattleCommand();
            }
            UpdateEnemyPhase();
            EvaluateOutcome();
        }

        private void TransferBattleCommand()
        {
            int previousLeaderId = leaderId;
            leaderId = -1;
            leaderRace = CombatRace.None;
            // Creation order is the initial formation order, independent of later HP or speed.
            foreach (BattleUnit candidate in units)
            {
                if (candidate.Side != BattleSide.Player || !candidate.Alive) continue;
                leaderId = candidate.Id;
                leaderRace = performerClocks[candidate.Id].Race;
                break;
            }
            BattleDice.SetBattleSelectiveReroll(leaderRace == CombatRace.Mermaid);
            // Do not refresh the hand/revision, combo budgets, cooldowns, energy or rescue flag.
            // Applied poison/shields also remain; only subsequent commander-dependent effects change.
            Emit(BattleEventKind.LeaderChanged, previousLeaderId, leaderId, "command-transfer",
                (int)leaderRace, false);
        }

        private void HealPercent(BattleUnit actor, BattleUnit target, string skill, int permille) =>
            HealAmount(actor, target, skill, (int)((long)target.MaxHp * permille / 1000));

        private void HealAmount(BattleUnit actor, BattleUnit target, string skill, int amount)
        {
            if (!target.Alive) return;
            if (performerClocks[target.Id].AntiHealUntil > ElapsedMilliseconds) amount = amount * 700 / 1000;
            int applied = Math.Min(target.MaxHp - target.Hp, amount);
            if (applied <= 0) return;
            target.Hp += applied;
            Emit(BattleEventKind.Heal, actor.Id, target.Id, skill, applied, false);
        }

        private void AddShield(BattleUnit actor, BattleUnit target, string skill, int permille, bool timed)
        {
            PerformerClock clock = performerClocks[target.Id];
            // Recasting the active shield refreshes its own layer, not the permanent dice layer.
            if (timed) { target.Shield = Math.Max(0, target.Shield - clock.TimedShield); clock.TimedShield = 0; }
            int amount = (int)Math.Min((long)target.MaxHp - target.Shield, (long)target.MaxHp * permille / 1000);
            if (amount <= 0) return;
            target.Shield += amount;
            if (timed) { clock.TimedShield = amount; clock.TimedShieldUntil = ElapsedMilliseconds + 4000; }
            Emit(BattleEventKind.Shield, actor.Id, target.Id, skill, amount, false);
        }

        private void ExpireTimedEffects()
        {
            foreach (BattleUnit unit in units)
            {
                PerformerClock clock = performerClocks[unit.Id];
                if (clock.TimedShield > 0 && ElapsedMilliseconds >= clock.TimedShieldUntil)
                {
                    unit.Shield = Math.Max(0, unit.Shield - clock.TimedShield);
                    clock.TimedShield = 0;
                }
            }
        }

        private void AddPoison(BattleUnit target, BattleUnit actor, int layers)
        {
            if (layers <= 0) return;
            PerformerClock clock = performerClocks[target.Id];
            clock.PoisonStacks = Math.Min(15, clock.PoisonStacks + layers);
            clock.PoisonSource = actor.Id;
        }

        public int PoisonStacks(BattleUnit unit) => unit != null && performerClocks.TryGetValue(unit.Id, out var clock)
            ? clock.PoisonStacks : 0;

        private void ResolvePoison()
        {
            foreach (BattleUnit target in units)
            {
                PerformerClock clock = performerClocks[target.Id];
                BattleUnit source = FindUnit(clock.PoisonSource);
                if (target.Alive && source != null && clock.PoisonStacks > 0 && Outcome == BattleOutcome.Ongoing)
                {
                    int damage = (int)Math.Min(int.MaxValue, (long)source.Attack * clock.PoisonStacks * 120 / 1000);
                    ApplyActualDamage(source, target, "rt-poison", Math.Max(1, damage), false);
                    clock.PoisonStacks /= 2;
                }
            }
            foreach (BattleUnit unit in units)
                for (int i = unit.Statuses.Count - 1; i >= 0; i--)
                    if (--unit.Statuses[i].RoundsLeft <= 0) unit.Statuses.RemoveAt(i);
        }

        private void ApplyNewHand()
        {
            if (diceRevision == BattleDice.Revision) return;
            diceRevision = BattleDice.Revision;
            ResetComboBudget();
            BattleUnit leader = FindUnit(leaderId);
            if (leader == null || !leader.Alive) return;
            DicePattern pattern = BattleDice.Hand.Pattern;
            if (leaderRace == CombatRace.BloodElf && pattern == DicePattern.HighPoint && !elfRescueUsed)
            {
                elfRescueUsed = true;
                BattleDice.GrantFreeReroll();
            }
            foreach (BattleUnit ally in units)
            {
                if (!ally.Alive || ally.Side != BattleSide.Player) continue;
                if (pattern == DicePattern.Straight) HealPercent(leader, ally, "rt-tide-hand", 100);
                if (leaderRace == CombatRace.Mermaid)
                    AddShield(leader, ally, "rt-tide-hand", ShieldPercent(pattern), false);
                if (leaderRace == CombatRace.Charm && (pattern == DicePattern.FiveKind ||
                    ally.Id == leader.Id && Combo(pattern) >= 3))
                {
                    BattleUnit target = SelectEnemy(ally);
                    if (target != null) Deal(ally, target, "rt-charm-follow", 1000);
                }
            }
        }

        private void ResetComboBudget()
        {
            foreach (PerformerClock clock in performerClocks.Values) clock.ExtraAttacks = Combo(BattleDice.Hand.Pattern);
        }

        private void AutoReroll()
        {
            if (BattleDice.SelectiveReroll)
            {
                bool[] planned = DiceHoldPlanner.Choose(BattleDice.Values);
                int held = 0;
                for (int i = 0; i < planned.Length; i++) if (planned[i]) held++;
                if (held == 3 || held == 4)
                {
                    for (int i = 0; i < planned.Length; i++)
                        if (BattleDice.Held[i] != planned[i]) BattleDice.ToggleHold(i);
                    BattleDice.RerollUnheld(out _);
                    return;
                }
            }
            BattleDice.EnergyRerollAll(out _);
        }

        private static int PoisonLayers(DicePattern p) => p == DicePattern.FiveKind ? 8
            : p == DicePattern.FourKind ? 5 : p == DicePattern.FullHouse ? 4 : p == DicePattern.ThreeKind ? 3
            : p == DicePattern.TwoPair ? 2 : p == DicePattern.Pair ? 1 : 0;
        private static int Combo(DicePattern p) => p == DicePattern.FiveKind ? 3 : PoisonLayers(p);
        private static int Pierce(DicePattern p) => p == DicePattern.FiveKind ? 500 : p == DicePattern.FourKind ? 300
            : p == DicePattern.FullHouse ? 250 : p == DicePattern.ThreeKind ? 200 : 0;
        private static int ShieldPercent(DicePattern p) => p == DicePattern.FiveKind ? 300 : p == DicePattern.FourKind ? 200
            : p == DicePattern.FullHouse ? 150 : p == DicePattern.ThreeKind ? 120 : p == DicePattern.Straight ? 80 : 0;
        private static string SkillId(CombatRace race, bool big) => "rt-" + race.ToString().ToLowerInvariant() + (big ? "-big" : "-small");
        private static SkillDefinition RealtimeSkill(string id) => id != null && realtimeSkills.TryGetValue(id, out var skill) ? skill : null;

        private static Dictionary<string, SkillDefinition> CreateRealtimeSkills()
        {
            var result = new Dictionary<string, SkillDefinition>(StringComparer.Ordinal);
            void Add(string id, string name, string effect = SkillEffect.Damage) =>
                result.Add(id, new SkillDefinition { Id = id, Name = name, Effect = effect });
            Add("rt-basic", "普攻");
            Add("rt-demon-small", "毒蚀斩"); Add("rt-demon-big", "万毒噬体");
            Add("rt-charm-small", "狐影瞬击"); Add("rt-charm-big", "魅语迷心");
            Add("rt-mermaid-small", "汐愈微光", SkillEffect.Heal);
            Add("rt-mermaid-big", "深海佑域", SkillEffect.Shield);
            Add("rt-bloodelf-small", "血刃穿刺"); Add("rt-bloodelf-big", "猩红处决");
            Add("rt-none-small", "音浪冲击"); Add("rt-none-big", "舞台共鸣");
            Add("rt-charm-follow", "魅影追击"); Add("rt-poison", "终末毒爆");
            Add("rt-tide-hand", "深海壁垒", SkillEffect.Shield);
            Add("rt-enemy-heavy", "重音蓄击"); Add("rt-enemy-finale", "终演震荡");
            Add("rt-elite-harden", "静电硬化", SkillEffect.Shield);
            Add("rt-boss-barrier", "首领屏障", SkillEffect.Shield);
            Add("rt-stun", "眩晕"); Add("rt-slow", "迟缓"); Add("rt-armor-break", "破甲");
            Add("rt-control-ward", "控场免疫", SkillEffect.Shield);
            Add("rt-cleanse", "净化", SkillEffect.Heal); Add("rt-interrupt", "打断");
            Add("rt-resist", "抵抗"); Add("rt-cast", "蓄力预警");
            return result;
        }
    }
}
