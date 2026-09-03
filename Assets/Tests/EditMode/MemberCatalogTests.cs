using System.Collections.Generic;
using NUnit.Framework;

namespace ChoSiren.Tests
{
    public sealed class MemberCatalogTests
    {
        [Test]
        public void FiftyFourMemberManifestCreatesStableIdLookupAndLegacyDefinitions()
        {
            MemberCatalogManifest manifest = Manifest(54);

            bool created = MemberCatalog.TryCreate(manifest, out MemberCatalog catalog, out string error, 50,
                path => path.StartsWith("Art/Members/"));

            Assert.That(created, Is.True, error);
            Assert.That(catalog.Count, Is.EqualTo(54));
            Assert.That(catalog.TryGetIndex("member-053", out int index), Is.True);
            Assert.That(index, Is.EqualTo(53));
            Assert.That(catalog.ToLegacyDefinitions()[53].Id, Is.EqualTo("member-053"));
            Assert.That(catalog[0].ThumbnailResourcePath, Is.EqualTo(catalog[0].PortraitResourcePath));
        }

        [Test]
        public void LaunchDistributionRequiresBalancedRolesAndRarities()
        {
            MemberCatalogManifest manifest = Manifest(54);

            Assert.That(MemberCatalogRules.TryValidateLaunchDistribution(manifest.Members, out string error),
                Is.True, error);

            manifest.Members[0].Role = "舞者";
            Assert.That(MemberCatalogRules.TryValidateLaunchDistribution(manifest.Members, out error), Is.False);
            StringAssert.Contains("定位 主唱", error);
        }

        [Test]
        public void ManifestRejectsDuplicateIdsAndExtensionBearingResourcePaths()
        {
            MemberCatalogManifest duplicate = Manifest(2);
            duplicate.Members[1].Id = duplicate.Members[0].Id;
            Assert.That(MemberCatalog.TryCreate(duplicate, out _, out string duplicateError), Is.False);
            StringAssert.Contains("ID 重复", duplicateError);

            MemberCatalogManifest extension = Manifest(1);
            extension.Members[0].PortraitResourcePath += ".png";
            Assert.That(MemberCatalog.TryCreate(extension, out _, out string pathError), Is.False);
            StringAssert.Contains("立绘路径无效", pathError);
        }

        [TestCase("SSR")]
        [TestCase("SR")]
        [TestCase("R")]
        public void GeneratedPowerIsDeterministicAndWithinRarityBand(string rarity)
        {
            int first = MemberCatalogRules.DeterministicBasePower("stable-member", rarity);
            int second = MemberCatalogRules.DeterministicBasePower("stable-member", rarity);

            Assert.That(second, Is.EqualTo(first));
            Assert.That(MemberCatalogRules.IsBasePowerInBand(rarity, first), Is.True);
        }

        [Test]
        public void LegacyMigrationFollowsIdsWhenCatalogIsReorderedAndExpanded()
        {
            var oldOrder = new[]
            {
                Definition("alpha"),
                Definition("bravo"),
                Definition("charlie")
            };
            var legacy = new GameSave
            {
                UnlockedMembers = new List<int> { 0, 2 },
                MemberLevels = new List<int> { 68, 22, 47 },
                Team = new List<int> { 2, 0 }
            };
            MemberCatalogManifest manifest = new MemberCatalogManifest
            {
                Members = new List<MemberCatalogEntry>
                {
                    Entry("charlie", "R", 6200),
                    Entry("delta", "SR", 7500),
                    Entry("alpha", "SSR", 9000)
                }
            };
            Assert.That(MemberCatalog.TryCreate(manifest, out MemberCatalog catalog, out string error), Is.True, error);

            MemberRosterSaveV2 migrated = MemberSaveMigration.FromLegacy(legacy, oldOrder, catalog);

            Assert.That(migrated.Members[0].MemberId, Is.EqualTo("charlie"));
            Assert.That(migrated.Members[0].Level, Is.EqualTo(47));
            Assert.That(migrated.Members[0].Unlocked, Is.True);
            Assert.That(migrated.Members[1].MemberId, Is.EqualTo("delta"));
            Assert.That(migrated.Members[1].Level, Is.EqualTo(1));
            Assert.That(migrated.Members[2].Level, Is.EqualTo(68));
            CollectionAssert.AreEqual(new[] { "charlie", "alpha" }, migrated.TeamMemberIds);

            MemberSaveMigration.ToIndexLists(migrated, catalog, out List<int> unlocked, out List<int> levels,
                out List<int> team);
            CollectionAssert.AreEqual(new[] { 0, 2 }, unlocked);
            CollectionAssert.AreEqual(new[] { 47, 1, 68 }, levels);
            CollectionAssert.AreEqual(new[] { 0, 2 }, team);
        }

        [Test]
        public void MigrationPrunesRetiredMembersAndAlwaysLeavesAPlayableTeam()
        {
            var legacy = new GameSave
            {
                UnlockedMembers = new List<int> { 1 },
                MemberLevels = new List<int> { 20, 70 },
                Team = new List<int> { 1 }
            };
            var oldOrder = new[] { Definition("retained"), Definition("retired") };
            MemberCatalogManifest manifest = new MemberCatalogManifest
            {
                Members = new List<MemberCatalogEntry> { Entry("retained", "SSR", 9000) }
            };
            Assert.That(MemberCatalog.TryCreate(manifest, out MemberCatalog catalog, out string error), Is.True, error);

            MemberRosterSaveV2 migrated = MemberSaveMigration.FromLegacy(legacy, oldOrder, catalog);

            Assert.That(migrated.Members[0].Unlocked, Is.True);
            CollectionAssert.AreEqual(new[] { "retained" }, migrated.TeamMemberIds);
        }

        private static MemberCatalogManifest Manifest(int count)
        {
            var manifest = new MemberCatalogManifest();
            for (int index = 0; index < count; index++)
            {
                string rarity = index < 12 ? "SSR" : index < 30 ? "SR" : "R";
                MemberCatalogEntry entry = Entry($"member-{index:000}", rarity,
                    MemberCatalogRules.DeterministicBasePower($"member-{index:000}", rarity));
                entry.Role = index % 3 == 0 ? "主唱" : index % 3 == 1 ? "舞者" : "支援";
                manifest.Members.Add(entry);
            }

            return manifest;
        }

        private static MemberCatalogEntry Entry(string id, string rarity, int basePower)
        {
            return new MemberCatalogEntry
            {
                Id = id,
                Name = "测试角色",
                Role = "主唱",
                Rarity = rarity,
                PortraitResourcePath = $"Art/Members/{id}/portrait",
                BasePower = basePower,
                StartingLevel = 1
            };
        }

        private static MemberDefinition Definition(string id)
        {
            return new MemberDefinition(id, id, "主唱", "R", $"Art/Members/{id}/portrait", 6000);
        }
    }
}
