namespace ChoSiren.Systems.Presentation
{
    /// <summary>
    /// Roster/collection reveal rules. Owned members are shown completely; not-yet-signed
    /// members only ever expose a silhouette plus progress counters. This is the single
    /// authority the roster grid and the member profile both read, so no screen can
    /// re-introduce a real name, career, stat or skill for a locked member.
    /// </summary>
    public static class MemberRosterVisibility
    {
        public const string LockedName = "未签约成员";
        public const string LockedCareer = "身份待揭晓";
        public const string LockedSilhouetteMark = "?";
        public const string LockedCardHint = "剪影 · 签约后揭晓";
        public const string LockedProfileHint = "签约后才能查看形象、属性、普通攻击与技能。";

        // Locked members are drawn as one generic, procedurally generated bust silhouette.
        // Only these flat values reach a locked card/profile; no member artwork is sampled.
        public const string SilhouetteSpriteName = "LockedMemberSilhouette";
        public const float SilhouetteRed = 0.22f;
        public const float SilhouetteGreen = 0.25f;
        public const float SilhouetteBlue = 0.43f;
        public const float SilhouetteAlpha = 1f;

        /// <summary>A real portrait sprite may only be rendered for an owned member.</summary>
        public static bool ShowsRealPortrait(bool unlocked) => unlocked;

        public static bool ShowsRealName(bool unlocked) => unlocked;

        public static bool ShowsCareer(bool unlocked) => unlocked;

        public static bool ShowsRace(bool unlocked) => unlocked;

        public static bool ShowsLevel(bool unlocked) => unlocked;

        public static bool ShowsCombatStats(bool unlocked) => unlocked;

        public static bool ShowsSkills(bool unlocked) => unlocked;

        public static bool ShowsCaptainTrait(bool unlocked) => unlocked;

        /// <summary>
        /// True when the roster is narrowed by member identity. A locked member has no public
        /// identity, so it must never be matched by (or revealed through) these filters.
        /// </summary>
        public static bool HasIdentityFilter(string careerFilter, string raceFilter, string searchQuery) =>
            !string.IsNullOrWhiteSpace(careerFilter) ||
            !string.IsNullOrWhiteSpace(raceFilter) ||
            !string.IsNullOrWhiteSpace(searchQuery);

        /// <summary>
        /// Single roster predicate shared by the member grid and the member picker. Owned members
        /// match their real career/race/name; locked members stay anonymous placeholders that are
        /// only listed while no identity filter is active, so a search or filter can never confirm
        /// an unrevealed name, career or race.
        /// </summary>
        public static bool MatchesRosterFilter(
            bool unlocked,
            string name,
            string career,
            string race,
            bool ownedOnly,
            string careerFilter,
            string raceFilter,
            string searchQuery)
        {
            if (ownedOnly && !unlocked) return false;
            if (!unlocked) return !HasIdentityFilter(careerFilter, raceFilter, searchQuery);

            if (!string.IsNullOrWhiteSpace(careerFilter) &&
                !string.Equals(career ?? string.Empty, careerFilter, System.StringComparison.Ordinal))
                return false;
            if (!string.IsNullOrWhiteSpace(raceFilter) &&
                !string.Equals(race ?? string.Empty, raceFilter, System.StringComparison.Ordinal))
                return false;
            if (!string.IsNullOrWhiteSpace(searchQuery) &&
                (name ?? string.Empty).IndexOf(searchQuery, System.StringComparison.OrdinalIgnoreCase) < 0)
                return false;
            return true;
        }

        /// <summary>Short line shown while an identity filter hides the anonymous silhouettes.</summary>
        public static string LockedFilterNotice(int remaining) =>
            remaining <= 0 ? string.Empty : $"未获得 {remaining} 位仅按剪影计数";

        /// <summary>
        /// Coverage (0..1) of the generic locked-member bust at one portrait pixel. The shape is
        /// a head, neck and shoulders in a 160x240 space (origin bottom-left) and never reads
        /// member artwork; the UI only rasterises this into a flat alpha mask. Kept here so the
        /// silhouette itself is covered by the source-built logic checks.
        /// </summary>
        public static float SilhouetteCoverage(float px, float py)
        {
            float headX = (px - 80f) / 45f;
            float headY = (py - 180f) / 53f;
            float head = Clamp01((1f - (float)System.Math.Sqrt(headX * headX + headY * headY)) * 22f);

            float neck = System.Math.Min(
                Clamp01(15f - System.Math.Abs(px - 80f)),
                Clamp01((py - 112f) * 0.9f));
            neck = System.Math.Min(neck, Clamp01((152f - py) * 0.9f));

            float shoulder = Clamp01((132f - py) / 132f);
            float halfWidth = 20f + 56f * (float)System.Math.Pow(shoulder, 0.7f);
            float body = py <= 132f ? Clamp01(halfWidth - System.Math.Abs(px - 80f)) : 0f;

            return Clamp01(System.Math.Max(head, System.Math.Max(neck, body)));
        }

        private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;

        /// <summary>Remaining not-yet-signed members; never negative even if data is inconsistent.</summary>
        public static int RemainingCount(int ownedCount, int totalCount) =>
            totalCount <= 0 ? 0 : System.Math.Max(0, totalCount - System.Math.Max(0, ownedCount));

        /// <summary>"已拥有 4/54 · 未获得 50".</summary>
        public static string ProgressLabel(int ownedCount, int totalCount)
        {
            int owned = System.Math.Max(0, System.Math.Min(ownedCount, System.Math.Max(0, totalCount)));
            return $"已拥有 {owned}/{System.Math.Max(0, totalCount)} · 未获得 {RemainingCount(ownedCount, totalCount)}";
        }

        /// <summary>Short card copy that tells the player how many silhouettes are left.</summary>
        public static string LockedCardTitle(int remaining)
        {
            if (remaining <= 0) return LockedName;
            return remaining == 1 ? "还有 1 位待揭晓" : $"还有 {remaining} 位待揭晓";
        }

        /// <summary>Locked profile progress line, e.g. "未获得 50 位 · 已拥有 4/54".</summary>
        public static string LockedProfileProgress(int ownedCount, int totalCount) =>
            $"未获得 {RemainingCount(ownedCount, totalCount)} 位 · 已拥有 {System.Math.Max(0, ownedCount)}/{System.Math.Max(0, totalCount)}";

        /// <summary>Locked profile body copy: progress + the real acquisition route.</summary>
        public static string LockedProfileNotice(int ownedCount, int totalCount) =>
            LockedProfileProgress(ownedCount, totalCount) +
            "\n在选秀的线上或线下面试查看候选；浏览免费，按报价签约后即可查看完整资料。";
    }
}
