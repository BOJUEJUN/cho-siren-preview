using System;
using System.Linq;
using ChoSiren.Systems.Data;
using ChoSiren.Systems.Tactics;
using NUnit.Framework;
using UnityEngine;

namespace ChoSiren.Tests
{
    /// <summary>
    /// 2026-11-11 (玩家反馈, 少爷批准 Builds/qa-logs/chapter20-1111): twenty stages per chapter with 首领 on every
    /// fifth stage, a few "三敌同屏" stages, stories at chapter openings and boss stages, and the new level-cap gates.
    /// </summary>
    public sealed class Chapter20Tests1111
    {
        private static readonly DateTime Now = new DateTime(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);
        private static readonly string[] WideStages =
            { "stage-1-2", "stage-1-8", "stage-1-13", "stage-1-18", "stage-2-2", "stage-2-8", "stage-2-13", "stage-2-18" };

        [SetUp, TearDown]
        public void Clean()
        {
            PlayerPrefs.DeleteKey(GameModel.SaveKey);
            PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
        }

        private static GameModel NewModel(params string[] cleared)
        {
            var model = new GameModel(() => Now, new InMemorySaveStore());
            foreach (string id in cleared) model.Save.ClearedStages.Add(new StageClear { Id = id, Stars = 3 });
            return model;
        }

        private static string[] Route(int chapter, int last) =>
            Enumerable.Range(1, last).Select(n => $"stage-{chapter}-{n}").ToArray();

        [Test]
        public void EachChapterHasTwentyStagesWithBossesOnEveryFifth()
        {
            TacticsManifest tactics = GameData.Repository.Tactics;
            Assert.That(GameModel.ChapterOneStageCount, Is.EqualTo(20));
            for (int chapter = 1; chapter <= 2; chapter++)
                for (int n = 1; n <= 20; n++)
                {
                    StageDefinition stage = tactics.FindStage($"stage-{chapter}-{n}");
                    Assert.That(stage, Is.Not.Null, $"{chapter}-{n}");
                    Assert.That(stage.TryValidate(out string error), Is.True, error);
                    bool boss = n % 5 == 0;
                    Assert.That(stage.EncounterType == "boss", Is.EqualTo(boss), $"{chapter}-{n} 首领关");
                    if (boss)
                    {
                        Assert.That(stage.BossTier, Is.EqualTo(n % 10 == 0 ? "major" : "mini"), $"{chapter}-{n}");
                        Assert.That(StageBoss1103.For(stage.Id), Is.Not.Null, $"{chapter}-{n} has a named boss");
                        Assert.That(Resources.Load<Sprite>(StageBoss1103.For(stage.Id).Art), Is.Not.Null, $"{chapter}-{n} boss art");
                    }
                    // Recommended levels never run ahead of the cap the earlier gates open.
                    Assert.That(stage.RecommendedLevel, Is.GreaterThanOrEqualTo(1));
                }
            Assert.That(StageBoss1103.For("stage-1-15").Name, Is.EqualTo("狂响幻蚀歌姬"));
            Assert.That(StageBoss1103.For("stage-1-20").Name, Is.EqualTo("幻蚀歌枢·梅洛蒂"));
            Assert.That(StageBoss1103.For("stage-2-15").Name, Is.EqualTo("残影歌姬"));
            Assert.That(StageBoss1103.For("stage-2-20").Name, Is.EqualTo("深潮女王·赛琳"));
        }

        [Test]
        public void OnlyTheAuthoredStagesPutThreeEnemiesOnStage()
        {
            TacticsManifest tactics = GameData.Repository.Tactics;
            foreach (StageDefinition stage in tactics.Stages)
            {
                bool wide = WideStages.Contains(stage.Id);
                Assert.That(stage.MaxOnStage == 3, Is.EqualTo(wide), stage.Id);
                if (wide) Assert.That(stage.Enemies.Count(e => e.Wave == 0), Is.EqualTo(3), stage.Id + " opens with three");
            }
        }

        [Test]
        public void WideStagesFieldThreeMinionsAndQueueTheRest()
        {
            GameModel model = NewModel(Route(1, 12));
            model.Save.StoryProgress = 100;
            BattleSimulator battle = model.StartStageBattle("stage-1-13", 1111UL, out string message);
            Assert.That(battle, Is.Not.Null, message);
            battle.EnableTurnBased();
            Assert.That(battle.MinionSlots, Is.EqualTo(3));
            Assert.That(battle.ActiveMinionCount, Is.EqualTo(3));
            for (int guard = 0; guard < 4000 && battle.Outcome == BattleOutcome.Ongoing; guard++)
            {
                if (battle.NextTurnActor() == null) break;
                battle.AutoTurn(true);
                Assert.That(battle.ActiveMinionCount, Is.LessThanOrEqualTo(3));
            }
            BattleSimulator normal = NewModel().StartStageBattle("stage-1-1", 1UL, out message);
            Assert.That(normal.MinionSlots, Is.EqualTo(2), "every other stage keeps two");
        }

        [Test]
        public void TheSecondHalfUnlocksInOrderAndChapterTwoWaitsForTwenty()
        {
            GameModel fresh = NewModel(Route(1, 9));
            Assert.That(fresh.IsStageUnlocked("stage-1-10"), Is.True);
            Assert.That(fresh.IsStageUnlocked("stage-1-11"), Is.False);
            GameModel half = NewModel(Route(1, 10));
            Assert.That(half.IsStageUnlocked("stage-1-11"), Is.True);
            Assert.That(half.IsStageUnlocked("stage-1-12"), Is.False);
            Assert.That(half.IsStageUnlocked("stage-2-1"), Is.False, "chapter two now waits for 1-20");
            Assert.That(half.IsChapterOneComplete, Is.False);
            GameModel full = NewModel(Route(1, 20));
            Assert.That(full.IsChapterOneComplete, Is.True);
            Assert.That(full.IsStageUnlocked("stage-2-1"), Is.True);
            GameModel veteran = NewModel(Route(1, 10).Concat(Route(2, 3)).ToArray());
            Assert.That(veteran.IsStageUnlocked("stage-2-4"), Is.True, "a save already in chapter two keeps it");
            Assert.That(veteran.IsStageUnlocked("stage-1-11"), Is.True);
            GameModel endOfTwo = NewModel(Route(1, 20).Concat(Route(2, 10)).ToArray());
            Assert.That(endOfTwo.IsStageUnlocked("stage-2-11"), Is.True);
            Assert.That(endOfTwo.IsStageUnlocked("stage-2-12"), Is.False);
        }

        [Test]
        public void OlderSavesKeepTheCapTheyHadOpened()
        {
            var save = new GameSave { StoryProgress = 100 };
            foreach (string id in Route(1, 10)) save.ClearedStages.Add(new StageClear { Id = id, Stars = 3 });
            var store = new InMemorySaveStore();
            store.SetString(GameModel.SaveKey, JsonUtility.ToJson(save));
            var model = new GameModel(() => Now, store);
            Assert.That(model.ProgressLevelCap1104, Is.EqualTo(22), "1-10 opened Lv22 before the 20-stage chapters");
            Assert.That(model.GatesCleared1104, Is.EqualTo(2));
            model.Save.ClearedStages.Add(new StageClear { Id = "stage-1-15", Stars = 3 });
            Assert.That(model.ProgressLevelCap1104, Is.EqualTo(23));

            var veteran = new GameSave { StoryProgress = 100 };
            foreach (string id in Route(1, 10).Concat(Route(2, 10))) veteran.ClearedStages.Add(new StageClear { Id = id, Stars = 3 });
            store = new InMemorySaveStore();
            store.SetString(GameModel.SaveKey, JsonUtility.ToJson(veteran));
            Assert.That(new GameModel(() => Now, store).ProgressLevelCap1104, Is.EqualTo(GameModel.MaxMemberLevel));

            GameModel fresh = NewModel(Route(1, 10));
            Assert.That(fresh.ProgressLevelCap1104, Is.EqualTo(20), "new saves follow the new gates");
        }

        [Test]
        public void StoriesOpenTheChaptersAndFrameEveryBoss()
        {
            GameDataRepository data = GameData.Repository;
            for (int chapter = 1; chapter <= 2; chapter++)
            {
                Assert.That(data.TryGetStory($"chapter-0{chapter}", out _, out string error), Is.True, error);
                for (int n = 1; n <= 20; n++)
                {
                    string id = $"stage-{chapter}-{n}";
                    Assert.That(data.TryGetStory(id, out var intro, out error), Is.True, id + " " + error);
                    if (n % 5 == 0) Assert.That(data.TryGetStory(id + "-end", out _, out error), Is.True, id + "-end " + error);
                    else if (n > 10)
                        Assert.That(intro.Lines.Count(l => l.Command == "say"), Is.InRange(2, 3), id + " keeps it short");
                }
            }
        }

        [Test]
        public void ChapterRewardsAndTasksCoverSixtyStars()
        {
            GameModel model = NewModel();
            Assert.That(model.ChapterStarRewardViews(1).Max(v => v.RequiredStars), Is.EqualTo(60));
            Assert.That(model.ChapterStarRewardViews(2).Max(v => v.RequiredStars), Is.EqualTo(60));
            Assert.That(model.ChapterTaskViews(1).Any(t => t.Title == "首次通关 1-20"), Is.True);
            Assert.That(model.ChapterTaskViews(2).Any(t => t.Title == "首次通关 2-20"), Is.True);
        }
    }
}
