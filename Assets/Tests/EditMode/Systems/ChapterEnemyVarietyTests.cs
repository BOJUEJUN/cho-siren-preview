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
            // 2026-10-29 art: every enemy is a female 幻浊体 now (幻魅歌姬 / 狂响幻蚀歌姬 included).
            var feminine = new HashSet<string> { "neon-scout", "noise-wraith", "pulse-guard", "velvet-hexer", "echo-drone", "static-golem", "siren-queen" };
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
            // 2026-11-01 (赵金: "重复性有点高，建议把那几个怪都加进去"): every enemy with art fights in both chapters, at least
            // twice each, and the elite 狂响幻蚀歌姬 leads every elite stage (企划书: 1-3 / 1-6 / 1-9).
            string[] art = { "neon-scout", "noise-wraith", "pulse-guard", "velvet-hexer", "echo-drone", "static-golem" };
            foreach (string chapter in new[] { "stage-1-", "stage-2-" })
            {
                var stages = data.Stages.Where(stage => stage.Id.StartsWith(chapter)).ToList();
                Assert.That(stages.Count, Is.EqualTo(10), chapter);
                foreach (string id in art)
                    Assert.That(stages.Count(stage => stage.Enemies.Any(spawn => spawn.UnitId == id)), Is.GreaterThanOrEqualTo(2), chapter + id);
                foreach (StageDefinition elite in stages.Where(stage => stage.EncounterType == "elite"))
                    Assert.That(elite.Enemies[0].UnitId, Is.EqualTo("static-golem"), elite.Id);
                Assert.That(stages.Max(stage => stage.Enemies.Select(spawn => spawn.UnitId).Distinct().Count()), Is.GreaterThanOrEqualTo(4), chapter);
            }
            // Tank and poison minions arrive after the tutorial fights; 毒蚀蛛娘 debuts with the elite at 1-3 (企划书).
            Assert.That(data.Stages.Take(3).Any(stage => stage.Enemies.Any(spawn => spawn.UnitId == "pulse-guard")), Is.False);
            Assert.That(data.Stages.Take(2).Any(stage => stage.Enemies.Any(spawn => spawn.UnitId == "velvet-hexer")), Is.False);
        }

        [Test]
        public void ArtAndCompositionChangesPreserveCalibratedHpWaveCountsAndTimeBudgets()
        {
            TacticsManifest data = Load();
            // 2026-11-01 rosters (Docs/BALANCE-1101.md 怪物分配表); health and attack are re-tuned to the 通过率 targets
            // (Balance1101Tests), so only the shape and the budgets are pinned here.
            int[] expectedCounts = { 3, 4, 3, 4, 4, 4, 4, 5, 4, 4 };
            int[] expectedWaves = { 2, 2, 2, 2, 2, 2, 2, 3, 2, 2 };
            int[] expectedLevels = { 1, 1, 1, 1, 10, 11, 12, 13, 14, 16 };
            List<StageDefinition> chapterOne = data.Stages.FindAll(item => item.Id.StartsWith("stage-1-"));
            Assert.That(chapterOne.Count, Is.EqualTo(10));
            for (int i = 0; i < chapterOne.Count; i++)
            {
                StageDefinition stage = chapterOne[i];
                Assert.That(stage.Enemies.Count, Is.EqualTo(expectedCounts[i]), stage.Id);
                Assert.That(stage.Enemies.Select(spawn => spawn.Wave).Distinct().Count(), Is.EqualTo(expectedWaves[i]), stage.Id);
                Assert.That(stage.TimeLimitSeconds, Is.EqualTo(120));
                Assert.That(stage.ThreeStarSeconds, Is.EqualTo(100));
                Assert.That(stage.RecommendedLevel, Is.EqualTo(expectedLevels[i]), stage.Id);
                if (i == 4 || i == 9)
                {
                    Assert.That(stage.Enemies.Count(spawn => spawn.UnitId == "siren-queen"), Is.EqualTo(1));
                    Assert.That(stage.Enemies.Where(spawn => spawn.Wave > 0).All(spawn => spawn.BossPhase >= 2), Is.True);
                    Assert.That(stage.BossTier, Is.EqualTo(i == 4 ? "mini" : "major"));
                }
            }
        }
    }
}
