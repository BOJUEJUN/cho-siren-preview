using System;
using System.Linq;

namespace ChoSiren
{
    public sealed partial class GameModel
    {
        // Keep the six published indices stable, then append the stable roster order.
        private static string[] homeMemberIds1113;
        public static string[] HomeMemberIds1113 => homeMemberIds1113 ?? (homeMemberIds1113 =
            new[] { "xingli", "feiyin", "yeying", "wubai", "huanhuoling", "linyin" }
            .Concat(Members.Select(m => m.Id)).Distinct().ToArray());
        public static string HomeMemberId1113(int index) => HomeMemberIds1113[
            Math.Max(0, Math.Min(HomeMemberIds1113.Length - 1, index))];
        public static int HomeIndexForMember1113(int member) => member < 0 || member >= Members.Length ? -1
            : Array.IndexOf(HomeMemberIds1113, Members[member].Id);
        public bool CanShowHome1113(int index) => index >= 0 && index < HomeMemberIds1113.Length &&
            IsUnlocked(IndexOfMember(HomeMemberIds1113[index]));
    }
}
