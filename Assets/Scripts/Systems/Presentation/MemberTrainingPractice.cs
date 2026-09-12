using System;
using System.Collections.Generic;

namespace ChoSiren.Systems.Presentation
{
    /// <summary>One compact practice run offered at the existing training entry.</summary>
    public sealed class MemberPracticePlan
    {
        public MemberPracticePlan(string career, string actionName, string focusLabel, int goldCost,
            float durationSeconds, bool reduceMotion)
        {
            Career = career ?? string.Empty;
            ActionName = actionName ?? string.Empty;
            FocusLabel = focusLabel ?? string.Empty;
            GoldCost = Math.Max(0, goldCost);
            DurationSeconds = durationSeconds;
            ReduceMotion = reduceMotion;
        }

        public string Career { get; }
        public string ActionName { get; }
        public string FocusLabel { get; }
        public int GoldCost { get; }

        /// <summary>Practice never charges diamonds; the training economy stays gold-only.</summary>
        public int DiamondCost => 0;

        public float DurationSeconds { get; }
        public bool ReduceMotion { get; }
    }

    /// <summary>Before/after values for one practice run, formatted for the compact panel.</summary>
    public sealed class MemberPracticeOutcome
    {
        public MemberPracticeOutcome(int levelBefore, int levelAfter, int powerBefore, int powerAfter,
            int vocalBefore, int vocalAfter, int rhythmBefore, int rhythmAfter, int presenceBefore,
            int presenceAfter, int resonanceBefore, int resonanceAfter, int goldSpent)
        {
            LevelBefore = levelBefore;
            LevelAfter = levelAfter;
            PowerBefore = powerBefore;
            PowerAfter = powerAfter;
            VocalBefore = vocalBefore;
            VocalAfter = vocalAfter;
            RhythmBefore = rhythmBefore;
            RhythmAfter = rhythmAfter;
            PresenceBefore = presenceBefore;
            PresenceAfter = presenceAfter;
            ResonanceBefore = resonanceBefore;
            ResonanceAfter = resonanceAfter;
            GoldSpent = Math.Max(0, goldSpent);

            var lines = new List<MemberProfileLine>(6)
            {
                Line("等级", levelBefore, levelAfter),
                Line("战力", powerBefore, powerAfter),
                Line("声能", vocalBefore, vocalAfter),
                Line("律动", rhythmBefore, rhythmAfter),
                Line("气场", presenceBefore, presenceAfter),
                Line("共鸣", resonanceBefore, resonanceAfter),
            };
            Deltas = lines.AsReadOnly();
        }

        public int LevelBefore { get; }
        public int LevelAfter { get; }
        public int PowerBefore { get; }
        public int PowerAfter { get; }
        public int VocalBefore { get; }
        public int VocalAfter { get; }
        public int RhythmBefore { get; }
        public int RhythmAfter { get; }
        public int PresenceBefore { get; }
        public int PresenceAfter { get; }
        public int ResonanceBefore { get; }
        public int ResonanceAfter { get; }
        public int GoldSpent { get; }
        public IReadOnlyList<MemberProfileLine> Deltas { get; }

        public int PowerDelta => PowerAfter - PowerBefore;

        public string Headline =>
            $"等级 {LevelBefore} → {LevelAfter} · 战力 {PowerBefore:N0} → {PowerAfter:N0} " +
            MemberTrainingPractice.FormatDelta(PowerBefore, PowerAfter);

        public string CostLine => $"本次仅消耗星光币 {GoldSpent:N0} · 不消耗星钻";

        private static MemberProfileLine Line(string label, int before, int after, string suffix = "")
        {
            return new MemberProfileLine(label,
                $"{before:N0}{suffix} → {after:N0}{suffix} ({MemberTrainingPractice.FormatDelta(before, after)}{suffix})");
        }
    }

    /// <summary>
    /// Pure rules for the lightweight practice feedback at the training entry. It describes the
    /// short career practice move and the readable stat deltas; the actual level-up still goes
    /// through GameModel.Train (gold-only, existing cost formula).
    /// </summary>
    public static class MemberTrainingPractice
    {
        public const float StandardDurationSeconds = 1.4f;
        public const float ReducedDurationSeconds = 0.35f;
        public const float MinimumDurationSeconds = 1f;
        public const float MaximumDurationSeconds = 2f;
        public const string ReduceMotionPreferenceKey = "ChoSiren.Settings.ReduceMotion";

        public const string VocalPractice = "发声练习";
        public const string DancePractice = "律动练习";
        public const string RapPractice = "节奏练习";
        public const string FacePractice = "镜头练习";
        public const string GeneralPractice = "基础练习";

        public const string VocalFocus = "音准与气息";
        public const string DanceFocus = "节拍与身位";
        public const string RapFocus = "咬字与律动";
        public const string FaceFocus = "表情与镜头感";
        public const string GeneralFocus = "舞台基础";

        public static string ActionNameFor(string career)
        {
            switch ((career ?? string.Empty).Trim())
            {
                case PracticeCareers.Vocalist: return VocalPractice;
                case PracticeCareers.Dancer: return DancePractice;
                case PracticeCareers.Rapper: return RapPractice;
                case PracticeCareers.Face: return FacePractice;
                default: return GeneralPractice;
            }
        }

        public static string FocusFor(string career)
        {
            switch ((career ?? string.Empty).Trim())
            {
                case PracticeCareers.Vocalist: return VocalFocus;
                case PracticeCareers.Dancer: return DanceFocus;
                case PracticeCareers.Rapper: return RapFocus;
                case PracticeCareers.Face: return FaceFocus;
                default: return GeneralFocus;
            }
        }

        public static float Duration(bool reduceMotion) =>
            reduceMotion ? ReducedDurationSeconds : StandardDurationSeconds;

        public static MemberPracticePlan BuildPlan(string career, int goldCost, bool reduceMotion)
        {
            string normalized = (career ?? string.Empty).Trim();
            return new MemberPracticePlan(normalized, ActionNameFor(normalized), FocusFor(normalized),
                goldCost, Duration(reduceMotion), reduceMotion);
        }

        public static MemberPracticeOutcome BuildOutcome(int levelBefore, int levelAfter, int powerBefore,
            int powerAfter, int vocalBefore, int vocalAfter, int rhythmBefore, int rhythmAfter,
            int presenceBefore, int presenceAfter, int resonanceBefore, int resonanceAfter, int goldSpent)
        {
            return new MemberPracticeOutcome(levelBefore, levelAfter, powerBefore, powerAfter, vocalBefore,
                vocalAfter, rhythmBefore, rhythmAfter, presenceBefore, presenceAfter, resonanceBefore,
                resonanceAfter, goldSpent);
        }

        /// <summary>"+1,234", "-12" or "±0" so a stat row never reads as a fake gain.</summary>
        public static string FormatDelta(int before, int after)
        {
            long delta = (long)after - before;
            if (delta > 0) return "+" + delta.ToString("N0");
            if (delta < 0) return delta.ToString("N0");
            return "±0";
        }
    }

    /// <summary>Career labels owned by MemberCatalog; mirrored here to keep this file dependency-free.</summary>
    internal static class PracticeCareers
    {
        public const string Vocalist = "主唱";
        public const string Dancer = "主舞";
        public const string Rapper = "Rapper";
        public const string Face = "门面";
    }
}
