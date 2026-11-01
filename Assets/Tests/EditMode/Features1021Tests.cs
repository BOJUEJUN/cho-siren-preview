using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace ChoSiren.Tests
{
    /// <summary>2026-10-21: 扫荡, save-code transfer and chapter-two rewards.</summary>
    public sealed class Features1021Tests
    {
        private DateTime now;

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey(GameModel.SaveKey);
            PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
            now = new DateTime(2026, 10, 21, 12, 0, 0, DateTimeKind.Local);
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(GameModel.SaveKey);
            PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
        }

        [Test]
        public void SweepNeedsThreeStarsSpendsStaminaAndKeepsProgress()
        {
            var model = new GameModel(() => now);
            int cost = model.Tactics.FindStage("stage-1-1").StaminaCost;
            int stamina = model.Save.Stamina;
            Assert.That(model.TrySweepStage("stage-1-1", 3, 7UL, out _), Is.False, "未三星不能扫荡");
            Assert.That(model.Save.Stamina, Is.EqualTo(stamina));

            model.Save.ClearedStages.Add(new StageClear { Id = "stage-1-1", Stars = 3 });
            model.SetBattleSpeed(2); // persist once so save normalisation settles before measuring
            int gold = model.Save.Gold;
            int progress = model.Save.StoryProgress;
            Assert.That(model.TrySweepStage("stage-1-1", 3, 7UL, out string message), Is.True, message);
            Assert.That(model.Save.Stamina, Is.EqualTo(stamina - cost * 3));
            Assert.That(model.Save.Gold, Is.GreaterThan(gold));
            Assert.That(model.Save.StoryProgress, Is.EqualTo(progress));
            Assert.That(model.StarsOf("stage-1-1"), Is.EqualTo(3));
            Assert.That(model.LastBattleRewards, Is.Not.Empty);
        }

        [Test]
        public void SweepRefusesWithoutEnoughStamina()
        {
            var model = new GameModel(() => now);
            model.Save.ClearedStages.Add(new StageClear { Id = "stage-1-1", Stars = 3 });
            model.Save.Stamina = 3;
            Assert.That(model.TrySweepStage("stage-1-1", 1, 1UL, out string message), Is.False);
            Assert.That(message, Does.Contain("体力不足"));
            Assert.That(model.Save.Stamina, Is.EqualTo(3));
        }

        [Test]
        public void SaveCodeRoundTripsAndRejectsGarbage()
        {
            var model = new GameModel(() => now);
            model.Save.Gold = 12345;
            model.Save.ClearedStages.Add(new StageClear { Id = "stage-1-1", Stars = 2 });
            string code = model.ExportSaveCode();
            Assert.That(code, Does.StartWith(GameModel.SaveCodePrefix));

            model.Save.Gold = 1;
            Assert.That(model.TryImportSaveCode("hello", out _), Is.False);
            Assert.That(model.TryImportSaveCode(GameModel.SaveCodePrefix + "!!!", out _), Is.False);
            Assert.That(model.Save.Gold, Is.EqualTo(1));

            Assert.That(model.TryImportSaveCode("  " + code + "\n", out string message), Is.True, message);
            Assert.That(model.Save.Gold, Is.EqualTo(12345));
            Assert.That(model.StarsOf("stage-1-1"), Is.EqualTo(2));
            Assert.That(new GameModel(() => now).Save.Gold, Is.EqualTo(12345), "导入后必须写入本机存档");
        }

        [Test]
        public void ChapterTwoStarRewardsAndTasksClaimOnce()
        {
            var model = new GameModel(() => now);
            for (int stage = 1; stage <= 4; stage++)
                model.Save.ClearedStages.Add(new StageClear { Id = $"stage-2-{stage}", Stars = 3 });
            Assert.That(model.ChapterTotalStars(2), Is.EqualTo(12));
            Assert.That(model.ChapterClaimableStarRewardCount(2), Is.EqualTo(1));
            int diamonds = model.Save.Diamonds;
            Assert.That(model.TryClaimChapterStarReward(2, 10, out _), Is.True);
            Assert.That(model.Save.Diamonds, Is.EqualTo(diamonds + 40));
            Assert.That(model.TryClaimChapterStarReward(2, 10, out _), Is.False);
            Assert.That(model.TryClaimChapterStarReward(2, 20, out _), Is.False);

            Assert.That(model.TryClaimChapterTask(2, GameModel.ChapterTwoTaskClearStageThree, out _), Is.True);
            Assert.That(model.TryClaimChapterTask(2, GameModel.ChapterTwoTaskClearFinalStage, out _), Is.False);
            Assert.That(model.ChapterOneClaimableStarRewardCount, Is.Zero, "第二章奖励不能算进第一章");
        }

        [Test]
        public void BattleSpeedIsClampedAndRemembered()
        {
            var model = new GameModel(() => now);
            model.SetBattleSpeed(5);
            Assert.That(model.Save.BattleSpeed, Is.EqualTo(3));
            Assert.That(new GameModel(() => now).Save.BattleSpeed, Is.EqualTo(3));
            model.SetBattleSpeed(0);
            Assert.That(model.Save.BattleSpeed, Is.EqualTo(1));
        }
    }
}
