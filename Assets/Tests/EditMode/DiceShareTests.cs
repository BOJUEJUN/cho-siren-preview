using System;
using System.Linq;
using ChoSiren.Systems.Tactics;
using NUnit.Framework;
using UnityEngine;

namespace ChoSiren.Tests
{
    public sealed class DiceShareTests
    {
        private string priorSave, priorLegacy;
        private bool hadSave, hadLegacy;
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
            var now = new DateTime(2026, 9, 7, 12, 0, 0);
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
        public void CaptainCarriesTheWholeDiceBuffAndEnemiesNone()
        {
            BattleSimulator battle = model.StartStageBattle("stage-1-1", 42, out string message);
            Assert.That(battle, Is.Not.Null, message);
            BattleUnit captain = battle.Units.First(u => u.Id == battle.CurrentLeaderId);
            Assert.That(battle.DiceMultiplierFor(captain), Is.EqualTo(battle.DiceDamageMultiplierPermille));
            foreach (BattleUnit enemy in battle.Units.Where(u => u.Side == BattleSide.Enemy))
                Assert.That(battle.DiceMultiplierFor(enemy), Is.EqualTo(1000));
            Assert.That(battle.DiceMultiplierFor(null), Is.EqualTo(1000));

            // Teammates never carry more than the captain, and never less than nothing.
            foreach (BattleUnit mate in battle.Units.Where(u => u.Side == BattleSide.Player && u.Id != captain.Id))
                Assert.That(battle.DiceMultiplierFor(mate), Is.InRange(1000, battle.DiceDamageMultiplierPermille));
        }
    }
}
