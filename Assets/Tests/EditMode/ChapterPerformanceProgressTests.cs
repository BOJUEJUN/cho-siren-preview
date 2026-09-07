using System;
using System.Linq;
using ChoSiren.Systems.Tactics;
using NUnit.Framework;
using UnityEngine;

namespace ChoSiren.Tests
{
    public sealed class ChapterPerformanceProgressTests
    {
        private string priorSave, priorLegacy;
        private bool hadSave, hadLegacy;
        private DateTime now;
        private GameModel model;

        [SetUp]
        public void SetUp()
        {
            hadSave = PlayerPrefs.HasKey(GameModel.SaveKey);
            hadLegacy = PlayerPrefs.HasKey(GameModel.LegacySaveKey);
            priorSave = PlayerPrefs.GetString(GameModel.SaveKey);
            priorLegacy = PlayerPrefs.GetString(GameModel.LegacySaveKey);
            PlayerPrefs.DeleteKey(GameModel.SaveKey);
            PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
            now = new DateTime(2026, 9, 7, 12, 0, 0);
            model = new GameModel(() => now);
        }

        [TearDown]
        public void TearDown()
        {
            if (hadSave) PlayerPrefs.SetString(GameModel.SaveKey, priorSave);
            else PlayerPrefs.DeleteKey(GameModel.SaveKey);
            if (hadLegacy) PlayerPrefs.SetString(GameModel.LegacySaveKey, priorLegacy);
            else PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
        }

        [Test]
        public void ChapterVictoryCountsDailyAndWeeklyPerformanceExactlyOnceAndPersists()
        {
            BattleSimulator battle = WinBattle();
            model.SettleStageBattle(battle, out _);
            Assert.That(model.Save.DailyPerformances, Is.EqualTo(1));
            Assert.That(Progress("daily-perform-3"), Is.EqualTo(1));
            Assert.That(Progress("weekly-perform-15"), Is.EqualTo(1));
            model.SettleStageBattle(battle, out _);
            Assert.That(model.Save.DailyPerformances, Is.EqualTo(1));
            Assert.That(Progress("daily-perform-3"), Is.EqualTo(1));
            Assert.That(Progress("weekly-perform-15"), Is.EqualTo(1));
            model = new GameModel(() => now);
            Assert.That(model.Save.DailyPerformances, Is.EqualTo(1));
            Assert.That(Progress("weekly-perform-15"), Is.EqualTo(1));
        }

        [Test]
        public void OngoingAbandonedAndRepeatedDefeatNeverCountAsPerformance()
        {
            BattleSimulator battle = model.StartStageBattle("stage-1-1", 42, out string message);
            Assert.That(battle, Is.Not.Null, message);
            model.SettleStageBattle(battle, out _);
            Assert.That(model.Save.DailyPerformances, Is.Zero);
            battle.AutoPlay(0);
            model.SettleStageBattle(battle, out _);
            model.SettleStageBattle(battle, out _);
            Assert.That(model.Save.DailyPerformances, Is.Zero);
            Assert.That(Progress("daily-perform-3"), Is.Zero);
            Assert.That(Progress("weekly-perform-15"), Is.Zero);
        }

        [Test]
        public void ExistingDailyBonusPaysOnceAndMidnightVictoryCountsInNewDay()
        {
            for (int i = 0; i < 2; i++) model.SettleStageBattle(WinBattle(), out _);
            int diamonds = model.Save.Diamonds;
            model.SettleStageBattle(WinBattle(), out string message);
            Assert.That(model.Save.Diamonds - diamonds, Is.EqualTo(GameModel.DailyPerformanceDiamondReward));
            Assert.That(message, Does.Contain("每日演出目标达成"));
            Assert.That(Progress("daily-perform-3"), Is.EqualTo(3));
            diamonds = model.Save.Diamonds;
            model.SettleStageBattle(WinBattle(), out _);
            Assert.That(model.Save.Diamonds, Is.EqualTo(diamonds));
            BattleSimulator crossingMidnight = WinBattle();
            now = now.AddDays(1);
            model.SettleStageBattle(crossingMidnight, out _);
            Assert.That(model.Save.DailyPerformances, Is.EqualTo(1));
            Assert.That(Progress("daily-perform-3"), Is.EqualTo(1));
            Assert.That(Progress("weekly-perform-15"), Is.EqualTo(5));
        }

        private int Progress(string id) => model.TaskViews().Single(task => task.Definition.Id == id).Progress;

        private BattleSimulator WinBattle()
        {
            BattleSimulator battle = model.StartStageBattle("stage-1-1", 42, out string message);
            Assert.That(battle, Is.Not.Null, message);
            // Settlement integration fixture, not a pacing test: preserve authored waves while
            // making their defeat deterministic and cheap without touching persistent levels.
            foreach (BattleUnit unit in battle.Units)
                if (unit.Side == BattleSide.Player) unit.BaseAttack = 100000;
            battle.AutoPlay();
            Assert.That(battle.Outcome, Is.EqualTo(BattleOutcome.Victory));
            return battle;
        }
    }
}
