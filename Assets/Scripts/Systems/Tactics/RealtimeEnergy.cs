using System;
using System.Collections.Generic;
using ChoSiren.Systems.Dice;

namespace ChoSiren.Systems.Tactics
{
    /// <summary>
    /// Per-character ultimate energy and the authoritative ultimate release path. The battle model
    /// owns the 0-100 charge, so pausing, dying or the UI clock can never fabricate energy.
    /// </summary>
    public sealed partial class BattleSimulator
    {
        public const int MaxUnitEnergy = 100;
        /// <summary>Alive combat time grant: 2 energy per second before real-hit grants.</summary>
        public const int UnitEnergyPerSecond = 2;
        /// <summary>Granted to the attacker when a hit actually removes HP or shield.</summary>
        public const int UnitEnergyPerHit = 3;
        /// <summary>Extra grant when the same hit defeats the target.</summary>
        public const int UnitEnergyPerKill = 6;
        private const int UnitEnergyTickMilliseconds = 1000;
        private int nextUnitEnergyTick = UnitEnergyTickMilliseconds;
        private int nextAutoUltimateAt;
        public const int AutoUltimateSpacingMilliseconds = 4000;

        /// <summary>
        /// Skill-release mode for the whole battle. Basic attacks and small active skills always
        /// run automatically; this only controls whether a full-energy ultimate is released by the
        /// model (auto) or waits for the player's tap (manual).
        /// </summary>
        public bool AutoCastUltimates { get; set; } = true;

        /// <summary>Current ultimate energy of a character, 0-100. Enemies always read 0.</summary>
        public int EnergyOf(BattleUnit unit)
        {
            if (!IsRealtime || unit == null || unit.Side != BattleSide.Player) return 0;
            return performerClocks.TryGetValue(unit.Id, out PerformerClock clock) ? clock.Energy : 0;
        }

        public bool IsEnergyFull(BattleUnit unit) => EnergyOf(unit) >= MaxUnitEnergy;

        /// <summary>A full bar plus an off-cooldown, non-disabled caster is a legal ultimate.</summary>
        public bool IsUltimateReady(BattleUnit unit)
        {
            if (!IsRealtime || Outcome != BattleOutcome.Ongoing || unit == null || !unit.Alive ||
                unit.Side != BattleSide.Player) return false;
            if (!performerClocks.TryGetValue(unit.Id, out PerformerClock clock)) return false;
            // Turn mode: energy plus one team ultimate per round; the realtime cooldown clock does not apply.
            if (TeamUltimateUsedThisRound || (IsTurnBased && UltimatesUsed(unit) >= MaxUltimatesPerMember)) return false;
            if (clock.Energy < MaxUnitEnergy || (!IsTurnBased && ElapsedMilliseconds < clock.NextBig) ||
                clock.StunnedUntil > ElapsedMilliseconds || clock.SilencedUntil > ElapsedMilliseconds ||
                clock.CastUntil > 0) return false;
            // Damage ultimates need a living target; the Mermaid ultimate is party-wide support.
            return clock.Race == CombatRace.Mermaid || SelectEnemy(unit) != null;
        }

        public int UltimateCooldownRemaining(BattleUnit unit)
        {
            if (!IsRealtime || unit == null || !performerClocks.TryGetValue(unit.Id, out PerformerClock clock))
                return 0;
            return Math.Max(0, clock.NextBig - ElapsedMilliseconds);
        }

        public int ReadyUltimateCount
        {
            get
            {
                int count = 0;
                foreach (BattleUnit unit in units) if (IsUltimateReady(unit)) count++;
                return count;
            }
        }

        /// <summary>
        /// Manual release. Deducts the 100 energy exactly once and routes through the same
        /// <see cref="CastRaceSkill"/> the automatic clocks use, so the two paths cannot double-cast.
        /// </summary>
        public bool TryCastUltimate(int unitId)
        {
            BattleUnit unit = FindUnit(unitId);
            if (!IsUltimateReady(unit)) return false;
            PerformerClock clock = performerClocks[unit.Id];
            clock.Energy = 0;
            clock.EnergyRemainder = 0;
            formationChargeRemainder1102.Remove(unit.Id);
            clock.NextBig = ElapsedMilliseconds + BigCooldown(clock.Race);
            nextAutoUltimateAt = ElapsedMilliseconds + AutoUltimateSpacingMilliseconds;
            // The release itself must not immediately recharge its caster from AOE hits.
            clock.ReleasingUltimate = true;
            try { CastRaceSkill(unit, true); }
            finally { clock.ReleasingUltimate = false; }
            return true;
        }

        /// <summary>Auto mode leaves breathing room between characters, captain first.</summary>
        private void AutoReleaseUltimates()
        {
            if (!AutoCastUltimates || ElapsedMilliseconds < nextAutoUltimateAt) return;
            BattleUnit next = NextAutoUltimate();
            if (next != null) TryCastUltimate(next.Id);
        }

        /// <summary>Priority order: current captain, then the original formation order.</summary>
        public BattleUnit NextAutoUltimate()
        {
            if (IsTurnBased)
            {
                foreach (int id in FormationOrder1102)
                { var member = FindUnit(id); if (IsUltimateReady(member)) return member; }
                return null;
            }
            BattleUnit captain = FindUnit(CurrentLeaderId);
            if (IsUltimateReady(captain)) return captain;
            foreach (BattleUnit unit in units)
                if (IsUltimateReady(unit)) return unit;
            return null;
        }

        /// <summary>Fixed-step alive-time grant. Paused frames never call this, so no fake energy.</summary>
        private void AccrueUltimateEnergy()
        {
            while (ElapsedMilliseconds >= nextUnitEnergyTick)
            {
                nextUnitEnergyTick += UnitEnergyTickMilliseconds;
                foreach (BattleUnit unit in units)
                    if (unit.Side == BattleSide.Player && unit.Alive)
                        GainUltimateEnergy(unit, UnitEnergyPerSecond);
            }
        }

        private void GainUltimateEnergy(BattleUnit unit, int amount)
        {
            if (IsTurnBased) return; // The formation clock owns turn-mode energy.
            if (amount <= 0 || Outcome != BattleOutcome.Ongoing || unit == null || !unit.Alive ||
                unit.Side != BattleSide.Player) return;
            if (!performerClocks.TryGetValue(unit.Id, out PerformerClock clock)) return;
            if (clock.ReleasingUltimate || clock.Energy >= MaxUnitEnergy) return;
            int scaled = amount * (IsTurnBased ? TurnEnergyGainPermille() * ChargeOrderPermille(unit) / 1000 : EnergyGainPermille(unit))
                + clock.EnergyRemainder;
            clock.Energy = Math.Min(MaxUnitEnergy, clock.Energy + scaled / 1000);
            clock.EnergyRemainder = clock.Energy >= MaxUnitEnergy ? 0 : scaled % 1000;
        }

        /// <summary>
        /// Turn mode (2026-10-24): the dice hand speeds up the whole team's bars, not only the captain's — the old
        /// captain-only boost made her first to every ultimate.
        /// </summary>
        public int TurnEnergyGainPermille()
        {
            // 2026-10-31 (赵金 "我方过强"): three quarters of the hand's edge, at most +30% (was the whole hand, up to +60%).
            // 2026-11-01b: counted above 两对 like the attack bonus (DiceTurn.RhythmBonusFloorPermille) — 一对/两对 add nothing.
            int hand = BattleDice?.Hand?.MultiplierPermille ?? 1000;
            return Math.Min(HandEnergyCap1101, 1000 + Math.Max(0, hand - DiceTurn.RhythmBonusFloorPermille) * 3 / 4); // 2026-11-01c cap (CaptainPassive1101)
        }

        /// <summary>Teammates charge at the base rate. Captain gets +25% and the current
        /// hand multiplier (not the accumulated damage bonus), bounded to 2.5x.</summary>
        public int EnergyGainPermille(BattleUnit unit)
        {
            if (unit == null || unit.Side != BattleSide.Player || unit.Id != CurrentLeaderId) return 1000;
            int hand = BattleDice?.Hand?.MultiplierPermille ?? 1000;
            return Math.Min(2500, Math.Max(1250, hand * 1250 / 1000));
        }
    }
}
