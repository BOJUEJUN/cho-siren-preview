using System;

namespace ChoSiren.Systems.Economy
{
    public readonly struct StaminaSnapshot
    {
        public StaminaSnapshot(int stamina, long lastRegenUnixSeconds, int recovered)
        {
            Stamina = stamina;
            LastRegenUnixSeconds = lastRegenUnixSeconds;
            Recovered = recovered;
        }

        public int Stamina { get; }
        /// <summary>Anchor for the next tick; keeps partial progress across sessions.</summary>
        public long LastRegenUnixSeconds { get; }
        public int Recovered { get; }

        public long SecondsUntilNextPoint(long nowUnixSeconds, int regenSeconds, int max)
        {
            if (Stamina >= max) return 0;
            long elapsed = Math.Max(0, nowUnixSeconds - LastRegenUnixSeconds);
            return Math.Max(0, regenSeconds - elapsed);
        }
    }

    /// <summary>
    /// Pure stamina recovery. The caller stores <see cref="StaminaSnapshot.LastRegenUnixSeconds"/>
    /// in the save; nothing here touches PlayerPrefs or DateTime.Now so it is fully testable
    /// and can later be re-evaluated against server time.
    /// </summary>
    public static class StaminaRegen
    {
        public static StaminaSnapshot Apply(int current, long lastRegenUnixSeconds, long nowUnixSeconds,
            EconomyConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            return Apply(current, config.StaminaMax, lastRegenUnixSeconds, nowUnixSeconds,
                config.StaminaRegenSeconds, config.StaminaPerTick);
        }

        public static StaminaSnapshot Apply(int current, int max, long lastRegenUnixSeconds, long nowUnixSeconds,
            int regenSeconds, int perTick)
        {
            if (max <= 0) throw new ArgumentOutOfRangeException(nameof(max));
            if (regenSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(regenSeconds));
            if (perTick <= 0) throw new ArgumentOutOfRangeException(nameof(perTick));

            current = Math.Max(0, current);

            // Stamina above the cap (from items/level-ups) is kept but never regenerates.
            if (current >= max) return new StaminaSnapshot(current, nowUnixSeconds, 0);

            // A clock that went backwards must not steal progress or grant free ticks.
            if (nowUnixSeconds < lastRegenUnixSeconds)
                return new StaminaSnapshot(current, nowUnixSeconds, 0);

            long elapsed = nowUnixSeconds - lastRegenUnixSeconds;
            long ticks = elapsed / regenSeconds;
            if (ticks <= 0) return new StaminaSnapshot(current, lastRegenUnixSeconds, 0);

            long recovered = Math.Min((long)max - current, ticks * perTick);
            int stamina = current + (int)recovered;
            long anchor = stamina >= max
                ? nowUnixSeconds
                : lastRegenUnixSeconds + ticks * regenSeconds;
            return new StaminaSnapshot(stamina, anchor, (int)recovered);
        }

        /// <summary>Spends stamina; when leaving the cap the regen timer restarts from now.</summary>
        public static bool TrySpend(int current, int cost, long lastRegenUnixSeconds, long nowUnixSeconds, int max,
            out StaminaSnapshot result)
        {
            if (cost < 0) throw new ArgumentOutOfRangeException(nameof(cost));
            if (current < cost)
            {
                result = new StaminaSnapshot(current, lastRegenUnixSeconds, 0);
                return false;
            }

            long anchor = current >= max ? nowUnixSeconds : lastRegenUnixSeconds;
            result = new StaminaSnapshot(current - cost, anchor, 0);
            return true;
        }
    }
}
