using System.Collections.Generic;
using System.Linq;
using ChoSiren.Systems;
using ChoSiren.Systems.Data;
using ChoSiren.Systems.Tactics;
using NUnit.Framework;

namespace ChoSiren.Tests.Systems
{
    public sealed class BattlePacingTests
    {
        [Test]
        public void RealtimeChapterMeasurementsUseTheActualResourcePartyAndAllTenStages()
        {
            var repository = new GameDataRepository(new ResourcesGameDataSource(), new UnityJsonReader());
            Assert.That(repository.LoadAll(), Is.True);
            var races = GameModel.Members.ToDictionary(member => member.Id, member => member.Race);
            foreach (StageDefinition stage in repository.Tactics.Stages)
            {
                var battle = new BattleSimulator(repository.Tactics, stage, DefaultParty(), new SeededRandom(847));
                battle.EnableRealtime(races, "xingli", stage.RerollLimit);
                battle.AdvanceRealtime(battle.TimeLimitMilliseconds, true);
                TestContext.WriteLine($"REALTIME {stage.Id}: {battle.Outcome}, {battle.ElapsedMilliseconds}ms, " +
                    $"damage={battle.CharacterDamageDealt}, poison={battle.PoisonDamageDealt}, rerolls={battle.BattleDice.UsedRerolls}");
                Assert.That(battle.Outcome, Is.Not.EqualTo(BattleOutcome.Ongoing));
                Assert.That(battle.ElapsedMilliseconds, Is.LessThanOrEqualTo(battle.TimeLimitMilliseconds));
            }
            // These are measurements, not a claim that chapter pacing/balance has passed.
        }

        [Test]
        public void NewCohortProgressionMeasurementsUseRealtimeNotLegacyTurnCounts()
        {
            var repository = new GameDataRepository(new ResourcesGameDataSource(), new UnityJsonReader());
            Assert.That(repository.LoadAll(), Is.True, string.Join("\n", repository.Errors));
            Assert.That(repository.Tactics.Stages.Count, Is.EqualTo(GameModel.ChapterOneStageCount));
            var races = GameModel.Members.ToDictionary(member => member.Id, member => member.Race);
            foreach (int level in new[] { 1, 5, 10, 15, 20 })
            foreach (StageDefinition stage in repository.Tactics.Stages)
            {
                var party = DefaultParty();
                foreach (PlayerUnitSetup member in party) member.Level = level;
                var battle = new BattleSimulator(repository.Tactics, stage, party, new SeededRandom(847));
                battle.EnableRealtime(races, "xingli", stage.RerollLimit);
                battle.AdvanceRealtime(battle.TimeLimitMilliseconds, true);
                TestContext.WriteLine($"COHORT L{level} {stage.Id}: {battle.Outcome} {battle.ElapsedMilliseconds}ms " +
                    $"lost={battle.PlayerUnitsLost}, rerolls={battle.BattleDice.UsedRerolls}");
                Assert.That(battle.Outcome, Is.Not.EqualTo(BattleOutcome.Ongoing));
                Assert.That(battle.BattleDice.UsedRerolls, Is.LessThanOrEqualTo(stage.RerollLimit));
            }
            // Numeric measurement is deliberately not a release pacing assertion.
        }

        [Test]
        public void AuthoredLevelPacingMeasurementsCoverMultipleSeeds()
        {
            var repository = new GameDataRepository(new ResourcesGameDataSource(), new UnityJsonReader());
            Assert.That(repository.LoadAll(), Is.True);
            var races = GameModel.Members.ToDictionary(member => member.Id, member => member.Race);
            foreach (StageDefinition stage in repository.Tactics.Stages)
            {
                var times = new List<int>();
                int wins = 0, lost = 0, rerolls = 0;
                for (ulong seed = 1; seed <= 20; seed++)
                {
                    var party = DefaultParty();
                    foreach (var member in party) member.Level = stage.RecommendedLevel;
                    var battle = new BattleSimulator(repository.Tactics, stage, party, new SeededRandom(seed));
                    battle.EnableRealtime(races, "xingli", stage.RerollLimit);
                    battle.AdvanceRealtime(battle.TimeLimitMilliseconds, true);
                    if (battle.Outcome == BattleOutcome.Victory) wins++;
                    times.Add(battle.ElapsedMilliseconds);
                    lost += battle.PlayerUnitsLost;
                    rerolls += battle.BattleDice.UsedRerolls;
                }
                times.Sort();
                TestContext.WriteLine($"AUTHORED {stage.Id} L{stage.RecommendedLevel}: wins={wins}/20 " +
                    $"min={times.First()} median={times[10]} max={times.Last()} lost={lost} rerolls={rerolls}");
            }
        }

        [Test]
        public void AllCaptainsHaveMeasuredAuthoredStageDurations()
        {
            var repository = new GameDataRepository(new ResourcesGameDataSource(), new UnityJsonReader());
            Assert.That(repository.LoadAll(), Is.True);
            var races = GameModel.Members.ToDictionary(member => member.Id, member => member.Race);
            foreach (var stage in repository.Tactics.Stages)
            foreach (string captain in new[] { "feiyin", "wubai", "yeying" })
            {
                var times = new List<int>();
                int wins = 0;
                for (ulong seed = 1; seed <= 16; seed++)
                {
                    var party = DefaultParty();
                    foreach (var member in party) member.Level = stage.RecommendedLevel;
                    var battle = new BattleSimulator(repository.Tactics, stage, party, new SeededRandom(seed));
                    battle.EnableRealtime(races, captain, stage.RerollLimit);
                    battle.AdvanceRealtime(battle.TimeLimitMilliseconds, true);
                    if (battle.Outcome == BattleOutcome.Victory) wins++;
                    times.Add(battle.ElapsedMilliseconds);
                }
                times.Sort();
                TestContext.WriteLine($"CAPTAIN {stage.Id} {captain}: wins={wins}/16 min={times.First()} median={times[8]} max={times.Last()}");
            }
        }

        private static List<PlayerUnitSetup> DefaultParty()
        {
            return new List<PlayerUnitSetup>
            {
                new PlayerUnitSetup { UnitId = "xingli", Row = 0, Col = 0, Level = 68 },
                new PlayerUnitSetup { UnitId = "feiyin", Row = 1, Col = 0, Level = 64 },
                new PlayerUnitSetup { UnitId = "wubai", Row = 2, Col = 0, Level = 59 },
                new PlayerUnitSetup { UnitId = "yeying", Row = 0, Col = 1, Level = 57 },
            };
        }
    }
}
