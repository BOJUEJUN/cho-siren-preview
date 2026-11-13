using System;

namespace ChoSiren.Systems.Tactics
{
    /// <summary>
    /// Ultimate tap judgment, 2026-10-25 (赵金: "判定不是很好点…点圆框判定这个玩法是可以 但是要…有意思一点").
    /// The old ring had a ±60 ms PERFECT and ±176 ms GOOD window — tighter than touch / WebGL input latency.
    /// A three-ring version followed the same day, but 少爷 found three too many to follow ("保持一个就行了"),
    /// so it is one approach ring again with wide windows (±140 ms / ±320 ms) and a longer, readable approach.
    /// The beat count stays a constant so the scoring rule holds for more beats. Grades and bonuses are the
    /// existing PERFECT +10% / GOOD +5%.
    /// </summary>
    public sealed class UltimateBeatChallenge1025
    {
        public const int Beats = 1;
        public const float Approach = 1.5f;        // a ring's travel time onto its target
        public const float Gap = .45f;              // between beats (when there is more than one)
        public const float PerfectWindow = .14f, GoodWindow = .32f;
        /// <summary>Taps earlier than the GOOD window but inside this one count as a miss (stops mashing).</summary>
        public const float EarlyMissWindow = .55f;

        public static float BeatTime(int beat) => Approach + beat * Gap;
        public static float DurationSeconds => BeatTime(Beats - 1) + GoodWindow;

        private readonly UltimateTimingGrade[] grades = new UltimateTimingGrade[Beats];
        private readonly bool[] judged = new bool[Beats];

        public float Elapsed { get; private set; }
        /// <summary>The beat the next tap judges; <see cref="Beats"/> once all are judged.</summary>
        public int Next { get; private set; }
        public bool Resolved => Next >= Beats;
        public bool IsJudged(int beat) => beat >= 0 && beat < Beats && judged[beat];
        public UltimateTimingGrade GradeOf(int beat) => IsJudged(beat) ? grades[beat] : UltimateTimingGrade.Miss;

        /// <summary>PERFECT = 2, GOOD = 1 per beat. All PERFECT but at most one GOOD is PERFECT; at least one point
        /// per beat after the first is GOOD (one beat: PERFECT → PERFECT, GOOD → GOOD).</summary>
        public UltimateTimingGrade Grade
        {
            get
            {
                int score = 0;
                for (int i = 0; i < Next; i++) score += grades[i] == UltimateTimingGrade.Perfect ? 2 : grades[i] == UltimateTimingGrade.Good ? 1 : 0;
                int perfect = 2 * Beats - (Beats > 1 ? 1 : 0), good = Math.Max(1, Beats - 1);
                return score >= perfect ? UltimateTimingGrade.Perfect : score >= good ? UltimateTimingGrade.Good : UltimateTimingGrade.Miss;
            }
        }

        /// <summary>Advances time; beats whose window has passed without a tap become misses.</summary>
        public void Advance(float seconds)
        {
            if (Resolved || seconds <= 0 || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            Elapsed += seconds;
            while (!Resolved && Elapsed > BeatTime(Next) + GoodWindow) Judge(UltimateTimingGrade.Miss);
        }

        /// <summary>Judges the next beat. Returns the beat index, or -1 when the tap is too early to count.</summary>
        public int Hit()
        {
            if (Resolved) return -1;
            float offset = Elapsed - BeatTime(Next);
            if (offset < -EarlyMissWindow) return -1;
            float distance = Math.Abs(offset);
            int beat = Next;
            Judge(distance <= PerfectWindow ? UltimateTimingGrade.Perfect
                : distance <= GoodWindow ? UltimateTimingGrade.Good : UltimateTimingGrade.Miss);
            return beat;
        }

        /// <summary>Ends the challenge now; unjudged beats are misses.</summary>
        public void Finish() { while (!Resolved) Judge(UltimateTimingGrade.Miss); }

        private void Judge(UltimateTimingGrade grade) { grades[Next] = grade; judged[Next] = true; Next++; }
    }
}
