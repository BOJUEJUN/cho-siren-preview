using System;
using System.Collections.Generic;
using System.Linq;
using ChoSiren.Systems.Data;
using ChoSiren.Systems.Tactics;
using NUnit.Framework;

namespace ChoSiren.Tests.Systems
{
    public sealed class ChapterEnemyVarietyTests
    {
        private static TacticsManifest Load()
        {
            var repository = new GameDataRepository(new ResourcesGameDataSource(), new UnityJsonReader());
            Assert.That(repository.LoadAll(), Is.True, string.Join("\n", repository.Errors));
            return repository.Tactics;
        }

        [Test]
        public void FemaleEnemyArchetypesAreDistinctAndNeverBecomeRecruitableMembers()
        {
            TacticsManifest data = Load();
            UnitDefinition scout = data.FindUnit("neon-scout");
            UnitDefinition guard = data.FindUnit("pulse-guard");
            UnitDefinition hexer = data.FindUnit("velvet-hexer");
            foreach (UnitDefinition unit in new[] { scout, guard, hexer })
            {
                Assert.That(unit, Is.Not.Null);
                Assert.That(unit.Role, Is.EqualTo("敌方"));
                Assert.That(GameModel.Members.Any(member => member.Id == unit.Id), Is.False);
            }
            Assert.That(scout.Speed, Is.GreaterThan(guard.Speed));
            Assert.That(guard.Speed, Is.GreaterThan(hexer.Speed));
            Assert.That(guard.Defense, Is.GreaterThan(scout.Defense));
            Assert.That(guard.Defense, Is.GreaterThan(hexer.Defense));
            Assert.That(hexer.Attack, Is.GreaterThan(guard.Attack));
            Assert.That(hexer.Attack, Is.GreaterThan(scout.Attack));
        }

        [Test]
        public void EveryStageHasItsOwnCompositionAndMechanicalEnemiesAreOccasional()
        {
            TacticsManifest data = Load();
            var signatures = new HashSet<string>();
            var feminine = new HashSet<string> { "neon-scout", "noise-wraith", "pulse-guard", "velvet-hexer", "siren-queen" };
            int total = 0, female = 0;
            foreach (StageDefinition stage in data.Stages)
            {
                string signature = string.Join("|", stage.Enemies.GroupBy(spawn => spawn.UnitId)
                    .OrderBy(group => group.Key).Select(group => group.Key + ":" + group.Count()));
                Assert.That(signatures.Add(signature), Is.True, stage.Id + "不应复制另一关的敌人构成");
                total += stage.Enemies.Count;
                female += stage.Enemies.Count(spawn => feminine.Contains(spawn.UnitId));
            }
            Assert.That(female / (float)total, Is.GreaterThanOrEqualTo(.9f));
            Assert.That(data.Stages.Count(stage => stage.Enemies.Any(spawn => spawn.UnitId == "echo-drone")), Is.EqualTo(2));
            Assert.That(data.Stages.Count(stage => stage.Enemies.Any(spawn => spawn.UnitId == "static-golem")), Is.EqualTo(1));
            Assert.That(data.Stages.Take(3).Any(stage => stage.Enemies.Any(spawn => spawn.UnitId == "pulse-guard")), Is.False);
            Assert.That(data.Stages.Take(6).Any(stage => stage.Enemies.Any(spawn => spawn.UnitId == "velvet-hexer")), Is.False);
        }

        [Test]
        public void ArtAndCompositionChangesPreserveCalibratedHpWaveCountsAndTimeBudgets()
        {
            TacticsManifest data = Load();
            int[] expectedHp = { 27600, 30600, 27198, 34200, 27819, 31000, 42000, 45000, 39699, 36756 };
            int[] expectedCounts = { 6, 6, 4, 6, 4, 4, 6, 6, 5, 5 };
            int[] expectedWaves = { 3, 3, 2, 3, 2, 2, 3, 3, 2, 2 };
            for (int i = 0; i < data.Stages.Count; i++)
            {
                StageDefinition stage = data.Stages[i];
                int totalHp = stage.Enemies.Sum(spawn => BattleSimulator.EnemyStats(data.FindUnit(spawn.UnitId), spawn).Hp);
                Assert.That(Math.Abs(totalHp - expectedHp[i]), Is.LessThanOrEqualTo(2), stage.Id);
                Assert.That(stage.Enemies.Count, Is.EqualTo(expectedCounts[i]), stage.Id);
                Assert.That(stage.Enemies.Select(spawn => spawn.Wave).Distinct().Count(), Is.EqualTo(expectedWaves[i]), stage.Id);
                Assert.That(stage.TimeLimitSeconds, Is.EqualTo(90));
                Assert.That(stage.ThreeStarSeconds, Is.EqualTo(75));
                Assert.That(stage.RecommendedLevel, Is.EqualTo(i + 1));
                if (i == 4 || i == 9)
                {
                    Assert.That(stage.Enemies.Count(spawn => spawn.UnitId == "siren-queen"), Is.EqualTo(1));
                    Assert.That(stage.Enemies.Where(spawn => spawn.Wave > 0).All(spawn => spawn.BossPhase == 2), Is.True);
                }
            }
        }
    }
}
