using System;
using System.Collections.Generic;

namespace ChoSiren.Systems.Tactics
{
    public enum TurnCommand { Basic, Skill, Ultimate }

    /// <summary>
    /// Turn-based layer over the realtime rules (2026-10-20 request: slower, readable fights).
    /// Every living unit acts once per round in speed order. Players choose 普攻 / 技能 / 大招;
    /// enemies attack, or announce a special one turn ahead and release it on their next turn.
    /// Damage, races, dice, shields, poison and energy all go through the existing realtime
    /// paths; each action spends one fixed slice of logical time so timed effects still expire.
    /// </summary>
    public sealed partial class BattleSimulator
    {
        public const int TurnSliceMilliseconds = 1000;
        public const int MaxTurnRounds = 20;
        public const int MaxBossTurnRounds = 30;
        /// <summary>Round limit of this fight: bosses get longer.</summary>
        public int TurnRoundLimit => Stage.HasBossPhases || Stage.EncounterType == "boss" ? MaxBossTurnRounds : MaxTurnRounds;
        public const int TurnSkillCooldownRounds = 2;
        public const int TurnEnergyPerAction = 10; // owner 2026-10-20: ultimates felt constant; slower bar
        /// <summary>
        /// After any team ultimate the next one waits until the next round. 2026-10-24 (赵金, after 天命之子): the real
        /// spacing is that a cast empties every teammate's bar, so the team recharges before the next close-up.
        /// </summary>
        public const int TeamUltimateRoundGap = 1;
        /// <summary>Turn mode: bar gained per own action, plus up to <see cref="TurnEnergyPerHitMax"/> from the hit itself.</summary>
        public const int TurnEnergyBasePerAction = 6;
        public const int TurnEnergyPerHitMax = 9;
        private const int EnemySpecialEveryTurns = 3;
        /// <summary>Turn-mode damage scale against the realtime hit values (balance 2026-10-20).</summary>
        public const int TurnDamagePermille = 13000;
        public const int TurnEnemyDamagePermille = 8000;
        /// <summary>Turn-mode heal / poison scale.</summary>
        public const int TurnSupportPermille = 4000;
        /// <summary>Reroll charge every member action adds, on top of damage-based charge.</summary>
        public const int TurnRerollChargePerAction = 6;

        private readonly List<int> turnOrderQueue = new List<int>();
        private readonly Dictionary<int, int> skillReadyRound = new Dictionary<int, int>();
        private readonly Dictionary<int, int> enemyTurnsTaken = new Dictionary<int, int>();

        public bool IsTurnBased { get; private set; }
        /// <summary>Turn mode: the whole team may release at most one ultimate per round.</summary>
        public int LastTeamUltimateRound { get; private set; }
        public bool TeamUltimateUsedThisRound => false; // Independent bars; the UI serializes presentations.
        /// <summary>A member the player tapped during auto battle: casts on her next turn.</summary>
        public int QueuedUltimateId { get; set; } = -1;
        /// <summary>Each member may release at most this many ultimates per battle.</summary>
        public const int MaxUltimatesPerMember = 3;
        private readonly Dictionary<int, int> ultimatesUsed = new Dictionary<int, int>();
        public int UltimatesUsed(BattleUnit unit) => unit != null && ultimatesUsed.TryGetValue(unit.Id, out int n) ? n : 0;
        public int TurnRound { get; private set; }
        public BattleUnit TurnActor { get; private set; }

        /// <summary>Remaining actors this round after the current one, in order (for the order strip).</summary>
        public IReadOnlyList<int> UpcomingTurns => turnOrderQueue;

        public void EnableTurnBased()
        {
            if (!IsRealtime) throw new InvalidOperationException("回合制需要在实时规则初始化之后开启");
            if (IsTurnBased) return;
            IsTurnBased = true;
            AutoCastUltimates = false;
            TurnRound = 0;
            EnableDiceRhythm();
        }

        /// <summary>
        /// Moves to the next living actor, opening a new round (and its speed order) when the
        /// current one is used up. Returns null once the battle has ended.
        /// </summary>
        public BattleUnit NextTurnActor()
        {
            if (!IsTurnBased || Outcome != BattleOutcome.Ongoing) return TurnActor = null;
            PayPendingEncoreEnergy();
            for (int guard = 0; guard < 64; guard++)
            {
                if (turnOrderQueue.Count == 0)
                {
                    TurnRound++;
                    if (TurnRound > TurnRoundLimit) { Finish(BattleOutcome.Defeat); return TurnActor = null; }
                    var order = new List<BattleUnit>();
                    foreach (BattleUnit unit in units) if (unit.Alive) order.Add(unit);
                    // Faster first; ties go to the team, then formation order.
                    order.Sort((a, b) => b.Speed != a.Speed ? b.Speed.CompareTo(a.Speed)
                        : a.Side != b.Side ? (a.Side == BattleSide.Player ? -1 : 1) : a.Id.CompareTo(b.Id));
                    foreach (BattleUnit unit in order) turnOrderQueue.Add(unit.Id);
                    AddBossExtraTurns1101(); // 2026-11-01: bosses act twice a round (BossMechanics1101)
                    // 2026-10-31 (赵金: "手动的时候，只要你不点重投，骰子就不会重投"): no free re-deal each round any more —
                    // that re-deal was the "dice reroll by themselves" bug in both 自动 and 手动. The hand only changes
                    // through a reroll (the player's tap, or 自动's planner).
                    Emit(BattleEventKind.RoundStart, -1, -1, "turn-round", TurnRound, false);
                    OnTurnRoundStart1101(); // 2026-11-01b: mermaid captain 深海壁垒 (CaptainPassive1101)
                }
                BattleUnit next = FindUnit(turnOrderQueue[0]);
                turnOrderQueue.RemoveAt(0);
                if (next == null || !next.Alive) continue;
                PerformerClock clock = performerClocks[next.Id];
                if (clock.StunnedUntil > ElapsedMilliseconds)
                {
                    // A stunned unit loses this turn; time still passes so the stun wears off.
                    Emit(BattleEventKind.Buff, next.Id, next.Id, "rt-stun", 0, false);
                    SpendTurnTime();
                    if (Outcome != BattleOutcome.Ongoing) return TurnActor = null;
                    continue;
                }
                return TurnActor = next;
            }
            return TurnActor = null;
        }

        public bool CanUseTurnSkill(BattleUnit unit) =>
            IsTurnBased && unit != null && unit.Alive && unit.Side == BattleSide.Player && unit == TurnActor &&
            performerClocks[unit.Id].SilencedUntil <= ElapsedMilliseconds &&
            (!skillReadyRound.TryGetValue(unit.Id, out int ready) || TurnRound >= ready);

        public int TurnSkillRoundsLeft(BattleUnit unit) =>
            unit != null && skillReadyRound.TryGetValue(unit.Id, out int ready) ? Math.Max(0, ready - TurnRound) : 0;

        /// <summary>The current player's command. Ultimates use <see cref="TryTurnUltimate"/>.</summary>
        public bool TryTurnCommand(TurnCommand command)
        {
            BattleUnit actor = TurnActor;
            if (!IsTurnBased || actor == null || actor.Side != BattleSide.Player || Outcome != BattleOutcome.Ongoing)
                return false;
            if (command == TurnCommand.Ultimate) return TryTurnUltimate(UltimateTimingGrade.Miss);
            if (command == TurnCommand.Skill)
            {
                if (!CanUseTurnSkill(actor)) return false;
                CastRaceSkill(actor, false);
                skillReadyRound[actor.Id] = TurnRound + TurnSkillCooldownRounds;
            }
            else BasicAttack(actor);
            FinishPlayerTurn(actor);
            return true;
        }

        /// <summary>Ultimate with the existing timing bonus (Miss = no bonus).</summary>
        public bool TryTurnUltimate(UltimateTimingGrade grade)
        {
            BattleUnit actor = TurnActor;
            if (!IsTurnBased || actor == null || actor.Side != BattleSide.Player || !IsUltimateReady(actor)) return false;
            bool cast = grade == UltimateTimingGrade.Miss ? TryCastUltimate(actor.Id) : TryCastUltimateWithTiming(actor.Id, grade);
            if (!cast) cast = TryCastUltimate(actor.Id); // never show the clip without spending the bar
            if (!cast) return false;
            RecordTeamUltimate(actor);
            if (QueuedUltimateId == actor.Id) QueuedUltimateId = -1;
            // The ultimate spends the whole bar; this turn must not refill it (owner: 放完蓝就没了).
            FinishPlayerTurn(actor, false);
            return true;
        }

        /// <summary>
        /// Auto play for one team turn: ultimate when ready (only if <paramref name="allowUltimate"/>), else skill,
        /// else attack. Manual battles pass false: attacks stay automatic, ultimates are the player's taps (赵金 2026-10-24).
        /// </summary>
        public void AutoTurn(bool allowUltimate = true)
        {
            BattleUnit actor = TurnActor;
            if (actor == null) return;
            if (actor.Side == BattleSide.Enemy) { EnemyTurn(); return; }
            if (allowUltimate && HoldsUltimateSpotlight(actor) && TryTurnUltimate(UltimateTimingGrade.Miss)) return;
            TryTurnCommand(CanUseTurnSkill(actor) ? TurnCommand.Skill : TurnCommand.Basic);
        }

        /// <summary>
        /// A tapped member's ultimate, cast right away between turns (天命之子: tap a full card, she goes now).
        /// It does not use up her place in the turn order; if it is her turn, the turn ends with it.
        /// </summary>
        public bool TryCastUltimateNow(BattleUnit unit, UltimateTimingGrade grade)
        {
            if (!IsTurnBased || unit == null || unit.Side != BattleSide.Player || Outcome != BattleOutcome.Ongoing || !IsUltimateReady(unit))
                return false;
            if (unit == TurnActor) return TryTurnUltimate(grade);
            bool cast = grade == UltimateTimingGrade.Miss ? TryCastUltimate(unit.Id) : TryCastUltimateWithTiming(unit.Id, grade);
            if (!cast) cast = TryCastUltimate(unit.Id);
            if (!cast) return false;
            RecordTeamUltimate(unit);
            BattleDice?.ChargeForAction1101(TurnRerollChargePerAction); // 2026-11-01c 骰子豆 (DiceEndless1101)
            SpendTurnTime();
            return true;
        }

        /// <summary>One cast per full bar: only the caster spends energy; other members keep charging.</summary>
        private void RecordTeamUltimate(BattleUnit caster)
        {
            LastTeamUltimateRound = TurnRound;
            ultimatesUsed[caster.Id] = UltimatesUsed(caster) + 1;
            spotlightSince[caster.Id] = log.Count;
            if (QueuedUltimateId == caster.Id) QueuedUltimateId = -1;
            // Teammates keep their independent bars and fractional charge.
        }

        /// <summary>
        /// Turn-mode bar from one hit: the share of the target's health it took (stronger attackers fill faster,
        /// the dice multiplier is already in the damage), up to <see cref="TurnEnergyPerHitMax"/>.
        /// </summary>
        private int TurnHitEnergy(BattleUnit target, int applied)
        {
            if (target == null || target.MaxHp <= 0 || applied <= 0) return 0;
            long share = (long)applied * 250 / target.MaxHp;   // 1% of its health → 2.5 points
            return (int)Math.Min(TurnEnergyPerHitMax, Math.Max(1, share));
        }

        // 2026-10-23 review #49 ("一直都是队长在放大"): the fastest member — usually the captain — was always first
        // to a full bar, so she took every ultimate close-up. In auto play the stage spotlight now decides.
        private readonly Dictionary<int, int> spotlightSince = new Dictionary<int, int>();

        /// <summary>
        /// Stage spotlight since her last ultimate: damage dealt + twice the damage taken + healing and shields given.
        /// The captain counts at 80% — she already owns the 队长演出 moment.
        /// </summary>
        public int Spotlight(BattleUnit unit)
        {
            if (unit == null || unit.Side != BattleSide.Player) return 0;
            long score = 0;
            for (int k = spotlightSince.TryGetValue(unit.Id, out int from) ? from : 0; k < log.Count; k++)
            {
                BattleEvent e = log[k];
                if (e.Kind == BattleEventKind.Damage)
                {
                    if (e.ActorId == unit.Id) score += e.HpLostAmount + e.AbsorbedAmount;
                    else if (e.TargetId == unit.Id) score += 2L * (e.HpLostAmount + e.AbsorbedAmount);
                }
                else if ((e.Kind == BattleEventKind.Heal || e.Kind == BattleEventKind.Shield) && e.ActorId == unit.Id) score += e.Amount;
            }
            if (unit.Id == CurrentLeaderId) score = score * 4 / 5;
            return (int)Math.Min(int.MaxValue, score);
        }

        /// <summary>
        /// Auto play: a ready member casts only if no teammate has a stronger claim — fewer ultimates so far, or as many
        /// with more spotlight — while that teammate is ready or about to be (bar at 80%+). The close-up rotates.
        /// A member the player tapped always goes.
        /// </summary>
        public bool HoldsUltimateSpotlight(BattleUnit unit)
        {
            if (!IsUltimateReady(unit)) return false;
            if (IsTurnBased || QueuedUltimateId == unit.Id) return true;
            int mine = Spotlight(unit), used = UltimatesUsed(unit);
            foreach (BattleUnit other in units)
            {
                if (other == unit || other.Side != BattleSide.Player || !other.Alive || UltimatesUsed(other) >= MaxUltimatesPerMember) continue;
                int energy = EnergyOf(other);
                bool contending = IsUltimateReady(other) || (energy >= MaxUnitEnergy * 4 / 5 && energy < MaxUnitEnergy);
                if (!contending) continue;
                int theirs = UltimatesUsed(other);
                if (theirs < used || (theirs == used && Spotlight(other) > mine)) return false;
            }
            return true;
        }

        /// <summary>
        /// Enemy turn. A charged special (announced last turn) is released now; otherwise every
        /// third turn it starts charging one (visible, interruptible), else it attacks.
        /// </summary>
        public void EnemyTurn()
        {
            BattleUnit actor = TurnActor;
            if (!IsTurnBased || actor == null || actor.Side != BattleSide.Enemy || Outcome != BattleOutcome.Ongoing) return;
            PerformerClock clock = performerClocks[actor.Id];
            EnemyTurnStart1101(actor);
            if (BossTurn1101(actor)) { SpendTurnTime(); TurnActor = null; return; }
            enemyTurnsTaken.TryGetValue(actor.Id, out int taken);
            enemyTurnsTaken[actor.Id] = taken + 1;
            if (clock.CastUntil > 0)
            {
                clock.CastUntil = 0;
                // A sealed enemy (魅语迷心) cannot release what it was charging.
                if (clock.SilencedUntil > ElapsedMilliseconds) BasicAttack(actor);
                else ResolveTelegraphedSpecial(actor);
            }
            else if ((taken + 1) % EnemySpecialEveryTurns == 0 && clock.SilencedUntil <= ElapsedMilliseconds)
            {
                if (Stage.UsesRealtime) BeginTelegraphedSpecial(actor);
                else EnemySpecial(actor);
            }
            else BasicAttack(actor);
            SpendTurnTime();
            TurnActor = null;
        }

        private void FinishPlayerTurn(BattleUnit actor, bool gainEnergy = true)
        {
            // Supports (heals/shields, no hits) get a bigger base so they still reach their ultimate.
            if (gainEnergy) GainUltimateEnergy(actor, TurnEnergyBasePerAction + (RaceOf(actor) == CombatRace.Mermaid ? 4 : 0));
            BattleDice?.ChargeForAction1101(TurnRerollChargePerAction); // 2026-11-01c 骰子豆 (DiceEndless1101)
            SpendTurnTime();
            TurnActor = null;
        }

        /// <summary>One slice of logical time: effects, poison measures, hand changes, energy.</summary>
        private void SpendTurnTime()
        {
            if (Outcome != BattleOutcome.Ongoing) return;
            // A telegraph must not resolve on the clock in turn mode; it waits for its next turn.
            var pending = new List<(PerformerClock clock, int until)>();
            foreach (PerformerClock clock in performerClocks.Values)
                if (clock.CastUntil > 0) { pending.Add((clock, clock.CastUntil)); clock.CastUntil = int.MaxValue; }
            AdvanceRealtime(TurnSliceMilliseconds);
            foreach (var item in pending) if (item.clock.CastUntil == int.MaxValue) item.clock.CastUntil = item.until;
        }
    }
}
