using System;
using System.Collections.Generic;

namespace ChoSiren.Systems.Presentation
{
    /// <summary>
    /// The four player-facing groups of a member profile. Interview/stage qualities are
    /// deliberately NOT part of this list: they describe casting potential, not combat data.
    /// </summary>
    public enum MemberProfileSectionKind
    {
        BaseStats,
        NormalAttack,
        ActiveSkills,
        PassiveCaptain,
    }

    public readonly struct MemberProfileLine
    {
        public MemberProfileLine(string label, string value)
        {
            Label = label ?? string.Empty;
            Value = value ?? string.Empty;
        }

        public string Label { get; }
        public string Value { get; }
    }

    public sealed class MemberProfileSection
    {
        public MemberProfileSection(MemberProfileSectionKind kind, string title,
            IReadOnlyList<MemberProfileLine> lines)
        {
            Kind = kind;
            Title = title ?? string.Empty;
            Lines = lines ?? Array.Empty<MemberProfileLine>();
        }

        public MemberProfileSectionKind Kind { get; }
        public string Title { get; }
        public IReadOnlyList<MemberProfileLine> Lines { get; }
    }

    /// <summary>One real battle skill, name and description copied from the shared definitions.</summary>
    public sealed class MemberSkillCopy
    {
        public MemberSkillCopy(string name, string description)
        {
            Name = name ?? string.Empty;
            Description = description ?? string.Empty;
        }

        public string Name { get; }
        public string Description { get; }
    }

    /// <summary>Everything the profile needs, already read from the authoritative runtime data.</summary>
    public sealed class MemberProfileInput
    {
        public string MemberId = string.Empty;
        public string Name = string.Empty;
        public string Career = string.Empty;
        public string Race = string.Empty;
        public int Level;
        public int Power;
        public int Vocal;
        public int Rhythm;
        public int Presence;
        public int Resonance;
        public string NormalAttackName = string.Empty;
        public string NormalAttackDescription = string.Empty;
        public IReadOnlyList<MemberSkillCopy> ActiveSkills = Array.Empty<MemberSkillCopy>();
        public string CaptainTraitTitle = string.Empty;
        public string CaptainTraitDescription = string.Empty;
    }

    public sealed class MemberProfileView
    {
        private static readonly MemberProfileSection[] Empty = Array.Empty<MemberProfileSection>();

        private MemberProfileView(bool revealed, string lockedNotice, IReadOnlyList<MemberProfileSection> sections)
        {
            Revealed = revealed;
            LockedNotice = lockedNotice ?? string.Empty;
            Sections = sections ?? Empty;
        }

        public bool Revealed { get; }
        public string LockedNotice { get; }
        public IReadOnlyList<MemberProfileSection> Sections { get; }

        public static MemberProfileView Locked(string notice) => new MemberProfileView(false, notice, Empty);

        public static MemberProfileView CreateRevealed(IReadOnlyList<MemberProfileSection> sections) =>
            new MemberProfileView(true, string.Empty, sections);

        public bool TryGet(MemberProfileSectionKind kind, out MemberProfileSection section)
        {
            for (int index = 0; index < Sections.Count; index++)
            {
                if (Sections[index].Kind != kind) continue;
                section = Sections[index];
                return true;
            }

            section = null;
            return false;
        }
    }

    /// <summary>
    /// Builds the grouped profile from the canonical 舞台四维 values (GameModel.StageStats).
    /// Locked members never reach
    /// <see cref="Build"/>: the caller must use <see cref="MemberProfileView.Locked"/> instead.
    /// </summary>
    public static class MemberProfileSections
    {
        public const string BaseStatsTitle = "舞台四维";
        public const string NormalAttackTitle = "普通攻击";
        public const string ActiveSkillsTitle = "主动技能";
        public const string PassiveCaptainTitle = "被动 · 队长特性";
        public const string UnknownValue = "暂无资料";

        public static MemberProfileView Build(MemberProfileInput input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));

            var sections = new List<MemberProfileSection>(4)
            {
                new MemberProfileSection(MemberProfileSectionKind.BaseStats, BaseStatsTitle,
                    new[]
                    {
                        new MemberProfileLine("等级", input.Level.ToString()),
                        new MemberProfileLine("战力", input.Power.ToString("N0")),
                        new MemberProfileLine("声能", input.Vocal.ToString()),
                        new MemberProfileLine("律动", input.Rhythm.ToString()),
                        new MemberProfileLine("气场", input.Presence.ToString()),
                        new MemberProfileLine("共鸣", input.Resonance.ToString()),
                    }),
                new MemberProfileSection(MemberProfileSectionKind.NormalAttack, NormalAttackTitle,
                    new[]
                    {
                        new MemberProfileLine(
                            string.IsNullOrWhiteSpace(input.NormalAttackName) ? UnknownValue : input.NormalAttackName,
                            string.IsNullOrWhiteSpace(input.NormalAttackDescription) ? UnknownValue : input.NormalAttackDescription),
                    }),
            };

            var activeLines = new List<MemberProfileLine>();
            if (input.ActiveSkills != null)
            {
                for (int index = 0; index < input.ActiveSkills.Count; index++)
                {
                    MemberSkillCopy skill = input.ActiveSkills[index];
                    if (skill == null) continue;
                    activeLines.Add(new MemberProfileLine(
                        string.IsNullOrWhiteSpace(skill.Name) ? UnknownValue : skill.Name,
                        string.IsNullOrWhiteSpace(skill.Description) ? UnknownValue : skill.Description));
                }
            }

            if (activeLines.Count == 0) activeLines.Add(new MemberProfileLine(UnknownValue, UnknownValue));
            sections.Add(new MemberProfileSection(MemberProfileSectionKind.ActiveSkills, ActiveSkillsTitle, activeLines));

            sections.Add(new MemberProfileSection(MemberProfileSectionKind.PassiveCaptain, PassiveCaptainTitle,
                new[]
                {
                    new MemberProfileLine(
                        string.IsNullOrWhiteSpace(input.CaptainTraitTitle) ? "队长特性" : input.CaptainTraitTitle,
                        string.IsNullOrWhiteSpace(input.CaptainTraitDescription) ? UnknownValue : input.CaptainTraitDescription),
                }));

            return MemberProfileView.CreateRevealed(sections);
        }
    }

    /// <summary>
    /// Casting-stage qualities shown to interview candidates. They are computed from the
    /// four stage dimensions and must never reuse combat skill names or combat stats.
    /// </summary>
    public static class MemberStageQualities
    {
        public const string Vocal = "声能主导";
        public const string Rhythm = "律动主导";
        public const string Presence = "气场主导";
        public const string Resonance = "共鸣主导";

        public static readonly IReadOnlyList<string> All =
            Array.AsReadOnly(new[] { Vocal, Rhythm, Presence, Resonance });

        /// <summary>Highest of the four stage dimensions; ties resolve in authored order.</summary>
        public static string DominantTrait(int vocal, int rhythm, int presence, int resonance)
        {
            int best = vocal;
            string trait = Vocal;
            if (rhythm > best) { best = rhythm; trait = Rhythm; }
            if (presence > best) { best = presence; trait = Presence; }
            if (resonance > best) { best = resonance; trait = Resonance; }
            return trait;
        }

        public static string Describe(int vocal, int rhythm, int presence, int resonance) =>
            "舞台特质 · " + DominantTrait(vocal, rhythm, presence, resonance);
    }
}
