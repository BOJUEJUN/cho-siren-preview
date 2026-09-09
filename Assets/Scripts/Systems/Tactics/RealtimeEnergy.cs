using System;
using System.Collections.Generic;

namespace ChoSiren.Systems.Tactics
{
    /// <summary>
    /// Per-character ultimate energy and the authoritative ultimate release path. The battle model
    /// owns the 0-100 charge, so pausing, dying or the UI clock can never fabricate energy.
    /// </summary>
    public sealed partial class BattleSimulator
    {
        public const int MaxUnitEnergy = 100;
        /// <summary>Alive combat time grant: 4 energy per second, so a full bar takes 25 seconds.</summary>
        public const int UnitEnergyPerSecond = 4;
        /// <summary>Granted to the attacker when a hit actually removes HP or shield.</summary>
        public const int UnitEnergyPerHit = 6;
        /// <summary>Extra grant when the same hit defeats the target.</summary>
        public const int UnitEnergyPerKill = 10;
        private const int UnitEnergyTickMilliseconds = 1000;
        private int nextUnitEnergyTick = UnitEnergyTickMilliseconds;

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
            if (clock.Energy < MaxUnitEnergy || ElapsedMilliseconds < clock.NextBig ||
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
            clock.NextBig = ElapsedMilliseconds + BigCooldown(clock.Race);
            CastRaceSkill(unit, true);
            return true;
        }

        /// <summary>Auto mode releases one ready character per logical step, captain first.</summary>
        private void AutoReleaseUltimates()
        {
            if (!AutoCastUltimates) return;
            BattleUnit next = NextAutoUltimate();
            if (next != null) TryCastUltimate(next.Id);
        }

        /// <summary>Priority order: current captain, then the original formation order.</summary>
        public BattleUnit NextAutoUltimate()
        {
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
            if (amount <= 0 || Outcome != BattleOutcome.Ongoing || unit == null || !unit.Alive ||
                unit.Side != BattleSide.Player) return;
            if (!performerClocks.TryGetValue(unit.Id, out PerformerClock clock)) return;
            clock.Energy = Math.Min(MaxUnitEnergy, clock.Energy + amount);
        }
    }
}
