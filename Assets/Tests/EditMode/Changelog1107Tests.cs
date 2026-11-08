using NUnit.Framework;
using ChoSiren.UI;

namespace ChoSiren.Tests
{
    /// <summary>2026-11-07 (少爷): the notice lists every release since the version the player last read.</summary>
    public sealed class Changelog1107Tests
    {
        [Test]
        public void ListsEveryVersionAfterTheBaselineUpToThisBuild()
        {
            string text = NativeTitleScreen.ChangelogSince1107("0.9.35", "0.9.40");
            StringAssert.StartsWith("v0.9.35 → v0.9.40", text);
            foreach (string v in new[] { "0.9.40", "0.9.39", "0.9.37", "0.9.36" }) StringAssert.Contains("【v" + v + "】", text);
            StringAssert.DoesNotContain("【v0.9.35】", text);
            StringAssert.DoesNotContain("【v0.9.34】", text);
        }

        [Test]
        public void AFirstInstallShowsOnlyThisVersion()
        {
            string text = NativeTitleScreen.ChangelogSince1107(string.Empty, "0.9.40");
            StringAssert.Contains("【v0.9.40】", text);
            StringAssert.DoesNotContain("【v0.9.39】", text);
        }

        [Test]
        public void NothingNewerThanThisBuildIsListed()
        {
            string text = NativeTitleScreen.ChangelogSince1107("0.9.37", "0.9.39");
            StringAssert.Contains("【v0.9.39】", text);
            StringAssert.DoesNotContain("【v0.9.40】", text);
        }
    }
}
