using System.Collections.Generic;
using ChoSiren.Panels;
using ChoSiren.Systems;
using ChoSiren.Systems.Data;
using ChoSiren.Systems.Gacha;
using ChoSiren.Systems.Story;
using ChoSiren.Systems.Tactics;
using NUnit.Framework;

namespace ChoSiren.Tests.Systems
{
    /// <summary>
    /// Loads the shipped JSON under Assets/Resources/Data through JsonUtility. This is the test
    /// that fails when a designer commits a broken table; it needs the Unity Editor to run.
    /// </summary>
    public sealed class GameDataTablesTests
    {
        private static GameDataRepository Load()
        {
            var repository = new GameDataRepository(new ResourcesGameDataSource(), new UnityJsonReader());
            Assert.That(repository.LoadAll(), Is.True, string.Join("\n", repository.Errors));
            return repository;
        }

        [Test]
        public void ShippedTablesPassValidation()
        {
            GameDataRepository repository = Load();

            Assert.That(repository.Economy.Tasks.Count, Is.GreaterThanOrEqualTo(6));
            Assert.That(repository.Gacha.Banners.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(repository.Tactics.Units.Count, Is.GreaterThanOrEqualTo(9));
            Assert.That(repository.Tactics.Stages.Count, Is.EqualTo(4));
            Assert.That(repository.Tactics.Stages.ConvertAll(stage => stage.Id),
                Is.EqualTo(new[] { "stage-1-1", "stage-1-2", "stage-1-3", "stage-1-4" }));
            Assert.That(repository.Tactics.Stages.TrueForAll(stage => stage.Chapter == "第 01 章"),
                Is.True);
        }

        [Test]
        public void EveryLegacyMemberHasABattleUnit()
        {
            // Catalog expands GameModel.Members to 50+; tactics.json currently covers the
            // original nine. New hero-* ids use roster presentation only until battle data lands.
            string[] legacyIds =
            {
                "xingli", "feiyin", "wubai", "yeying", "yaoguang", "hupo", "xianyue", "chuxue", "chengxia",
            };

            GameDataRepository repository = Load();
            for (int index = 0; index < legacyIds.Length; index++)
            {
                Assert.That(repository.Tactics.FindUnit(legacyIds[index]), Is.Not.Null,
                    $"成员 {legacyIds[index]} 缺少 tactics.json 单位定义");
            }

            for (int index = 0; index < legacyIds.Length; index++)
            {
                Assert.That(GameModel.Members[index].Id, Is.EqualTo(legacyIds[index]),
                    "前九名成员顺序必须与遗留存档约定一致");
            }
        }

        [Test]
        public void EveryShippedStageIsWinnableByAutoBattleWithAFullSsrParty()
        {
            GameDataRepository repository = Load();
            var party = new List<PlayerUnitSetup>
            {
                new PlayerUnitSetup { UnitId = "xingli", Row = 0, Col = 0, Level = 40 },
                new PlayerUnitSetup { UnitId = "feiyin", Row = 1, Col = 0, Level = 40 },
                new PlayerUnitSetup { UnitId = "wubai", Row = 1, Col = 1, Level = 40 },
                new PlayerUnitSetup { UnitId = "yeying", Row = 2, Col = 0, Level = 40 },
            };

            for (int index = 0; index < repository.Tactics.Stages.Count; index++)
            {
                StageDefinition stage = repository.Tactics.Stages[index];
                var battle = new BattleSimulator(repository.Tactics, stage, party, new SeededRandom(1000 + (ulong)index));
                Assert.That(battle.AutoPlay(), Is.EqualTo(BattleOutcome.Victory), $"{stage.Name} 用 40 级满编队自动战斗应能通关");
            }
        }

        [Test]
        public void DebutBannerMatchesPublishedRates()
        {
            GameDataRepository repository = Load();
            GachaBannerDefinition banner = repository.Gacha.Find("debut-xingli");

            Assert.That(banner, Is.Not.Null);
            Assert.That(banner.SsrRatePermille, Is.EqualTo(30));
            Assert.That(banner.HardPity, Is.EqualTo(80));
            Assert.That(banner.FeaturedItemIds, Is.EqualTo(new[] { "xingli" }));
            Assert.That(GachaPanel.CompactRateSummary(banner), Is.EqualTo(
                "SSR 3.0%\nSR  18.5%\nR   78.5%\n\n十连至少获得 SR"));
        }

        [Test]
        public void ChapterOneScriptRunsToTheEndOnBothBranches()
        {
            GameDataRepository repository = Load();
            Assert.That(repository.TryGetStory("chapter-01", out StoryScript script, out string error), Is.True, error);

            for (int option = 0; option < 2; option++)
            {
                var runner = new StoryRunner(script);
                StoryFrame frame = runner.Start();
                int guard = 0;
                while (!frame.IsEnd && guard++ < 200)
                    frame = frame.IsChoice ? runner.Choose(option) : runner.Advance();

                Assert.That(frame.IsEnd, Is.True, $"选项 {option} 的分支没有走到结尾");
                Assert.That(runner.HasFlag("ch1-complete"), Is.True);
                Assert.That(runner.HasFlag("ch1-xingli-bond"), Is.EqualTo(option == 0));
            }
        }
    }
}
