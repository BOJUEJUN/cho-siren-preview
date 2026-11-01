using System;

namespace ChoSiren.Systems.Tactics
{
    public enum UltimateTimingGrade { Miss, Good, Perfect }

    /// <summary>One tap, one result. Presentation supplies active (unpaused) time.</summary>
    public sealed class UltimateTimingChallenge
    {
        public const float DurationSeconds = 1.1f;
        public const float Target = .7f, GoodHalfWidth = .16f, PerfectHalfWidth = .055f;
        public float Elapsed { get; private set; }
        public float Progress => Math.Min(1f, Elapsed / DurationSeconds);
        public bool Resolved { get; private set; }
        public bool Cancelled { get; private set; }
        public UltimateTimingGrade Grade { get; private set; }

        public void Advance(float activeSeconds)
        {
            if (Resolved || activeSeconds <= 0 || float.IsNaN(activeSeconds)) return;
            Elapsed += activeSeconds;
            if (Elapsed >= DurationSeconds) { Resolved = true; Grade = UltimateTimingGrade.Miss; }
        }

        public UltimateTimingGrade Hit()
        {
            if (!Resolved) { Grade = At(Progress); Resolved = true; }
            return Grade;
        }

        public void Cancel() { Cancelled = true; Resolved = true; Grade = UltimateTimingGrade.Miss; }

        public static UltimateTimingGrade At(float progress)
        {
            if (float.IsNaN(progress) || progress < 0 || progress >= 1) return UltimateTimingGrade.Miss;
            float distance = Math.Abs(progress - Target);
            return distance <= PerfectHalfWidth ? UltimateTimingGrade.Perfect
                : distance <= GoodHalfWidth ? UltimateTimingGrade.Good : UltimateTimingGrade.Miss;
        }

        public static int BonusPermille(UltimateTimingGrade grade) => grade == UltimateTimingGrade.Perfect ? 100
            : grade == UltimateTimingGrade.Good ? 50 : 0;
    }

    public sealed partial class BattleSimulator
    {
        private int timingUltimateActor = -1, timingUltimateBonus;

        /// <summary>Bonus exists only inside this one authoritative cast, including its AOE hits.</summary>
        public bool TryCastUltimateWithTiming(int unitId, UltimateTimingGrade grade)
        {
            if ((AutoCastUltimates && !IsTurnBased) || timingUltimateActor >= 0 || !IsUltimateReady(FindUnit(unitId))) return false;
            timingUltimateActor = unitId;
            timingUltimateBonus = UltimateTimingChallenge.BonusPermille(grade);
            try { return TryCastUltimate(unitId); }
            finally { timingUltimateActor = -1; timingUltimateBonus = 0; }
        }

        // Hook before final capping at the damage, healing and shield amount entry points.
        // The actual skill effect must agree, so a shield cast cannot also boost unrelated damage.
        private long AdjustManualUltimateAmount(BattleUnit actor, string skill, long amount, string effect)
        {
            if (actor == null || actor.Id != timingUltimateActor || timingUltimateBonus <= 0 ||
                actor.Side != BattleSide.Player || !performerClocks.TryGetValue(actor.Id, out PerformerClock clock) ||
                !clock.ReleasingUltimate || skill != SkillId(clock.Race, true) || LookupSkill(skill)?.Effect != effect) return amount;
            return amount * (1000 + timingUltimateBonus) / 1000;
        }
    }
}
