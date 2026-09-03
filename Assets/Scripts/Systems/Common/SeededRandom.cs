using System;

namespace ChoSiren.Systems
{
    /// <summary>
    /// Random source used by gacha and battle so that a seed fully determines the outcome.
    /// Keeping it behind an interface lets tests inject fixed sequences.
    /// </summary>
    public interface IRandomSource
    {
        /// <summary>Returns an integer in [0, maxExclusive).</summary>
        int Next(int maxExclusive);

        /// <summary>Returns an integer in [0, 1000) used for permille rate checks.</summary>
        int NextPermille();
    }

    /// <summary>
    /// xorshift64* generator. Unlike System.Random its sequence is identical on every
    /// runtime (Mono, IL2CPP, WebGL), which matters when a server or replay must
    /// reproduce a client roll.
    /// </summary>
    public sealed class SeededRandom : IRandomSource
    {
        private ulong state;

        public SeededRandom(ulong seed)
        {
            state = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;
        }

        public ulong NextRaw()
        {
            state ^= state >> 12;
            state ^= state << 25;
            state ^= state >> 27;
            return state * 0x2545F4914F6CDD1DUL;
        }

        public int Next(int maxExclusive)
        {
            if (maxExclusive <= 0) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            return (int)(NextRaw() % (ulong)maxExclusive);
        }

        public int NextPermille() => Next(1000);
    }

    /// <summary>Test helper: replays a fixed list of values, then repeats the last one.</summary>
    public sealed class ScriptedRandom : IRandomSource
    {
        private readonly int[] permilles;
        private readonly int[] picks;
        private int permilleIndex;
        private int pickIndex;

        public ScriptedRandom(int[] permilles, int[] picks = null)
        {
            this.permilles = permilles ?? new[] { 0 };
            this.picks = picks ?? new[] { 0 };
        }

        public int Next(int maxExclusive)
        {
            int value = picks[Math.Min(pickIndex, picks.Length - 1)];
            pickIndex++;
            return Math.Max(0, Math.Min(maxExclusive - 1, value));
        }

        public int NextPermille()
        {
            int value = permilles[Math.Min(permilleIndex, permilles.Length - 1)];
            permilleIndex++;
            return Math.Max(0, Math.Min(999, value));
        }
    }
}
