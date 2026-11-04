using System;
using System.Collections.Generic;
using System.Reflection;
using ChoSiren.Systems.Economy;
using NUnit.Framework;
using UnityEngine;

namespace ChoSiren.Tests
{
    /// <summary>2026-11-04 经纪人等级: separate from member levels, fed by play, paying level rewards.</summary>
    public sealed class Account1104Tests
    {
        private static readonly DateTime Now = new DateTime(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

        [SetUp, TearDown]
        public void Clean()
        {
            PlayerPrefs.DeleteKey(GameModel.SaveKey);
            PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
        }

        private static void Gain(GameModel model, int xp) =>
            typeof(GameModel).GetMethod("GainAccountXp1104", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(model, new object[] { xp });

        [Test]
        public void NewManagerStartsAtLevelOneAndTheCurveGrows()
        {
            var model = new GameModel(() => Now, new InMemorySaveStore());
            Assert.That(model.AccountLevel1104, Is.EqualTo(1));
            Assert.That(model.AccountXp1104, Is.Zero);
            Assert.That(GameModel.XpToNext1104(1), Is.EqualTo(100));
            Assert.That(GameModel.XpToNext1104(10), Is.EqualTo(100 + 40 * 9 + 3 * 81));
            for (int l = 1; l < GameModel.MaxAccountLevel1104; l++)
                Assert.That(GameModel.XpToNext1104(l + 1), Is.GreaterThan(GameModel.XpToNext1104(l)));
        }

        [Test]
        public void LevelUpsQueueThenPayOnceWhenClaimed()
        {
            var model = new GameModel(() => Now, new InMemorySaveStore());
            int gold = model.Save.Gold, stamina = model.Save.Stamina;
            Gain(model, GameModel.XpToNext1104(1) + GameModel.XpToNext1104(2) + 30);
            Assert.That(model.AccountLevel1104, Is.EqualTo(3));
            Assert.That(model.AccountXp1104, Is.EqualTo(30));
            Assert.That(model.PendingLevelUpCount1104, Is.EqualTo(2));
            Assert.That(model.Save.Gold, Is.EqualTo(gold), "rewards wait for the claim (no popup inside a battle)");
            Assert.That(model.ClaimLevelUps1104(), Is.EqualTo(3));
            Assert.That(model.Save.Gold, Is.EqualTo(gold + (300 + 100 * 2) + (300 + 100 * 3)));
            Assert.That(model.Save.Stamina, Is.EqualTo(stamina + 60));
            Assert.That(model.Save.VoiceCrystals, Is.EqualTo(3), "Lv3 pays 3 声纹碎晶");
            Assert.That(model.ClaimLevelUps1104(), Is.Zero);
        }

        [Test]
        public void MilestonesPayRecruitTicketsAndCores()
        {
            Assert.That(GameModel.LevelReward1104(5), Does.Contain((CurrencyIds.RecruitTicket, 1)));
            Assert.That(GameModel.LevelReward1104(10), Does.Contain((CurrencyIds.RecruitTicket, 2)));
            Assert.That(GameModel.LevelReward1104(10), Does.Contain((CurrencyIds.VoiceCore, 1)));
            Assert.That(GameModel.LevelReward1104(7).Exists(r => r.Item1 == CurrencyIds.RecruitTicket), Is.False);
            foreach (var r in GameModel.LevelReward1104(30)) Assert.That(r.Item1, Is.Not.EqualTo(CurrencyIds.Diamond));
        }

        [Test]
        public void PlayGivesExperience()
        {
            var model = new GameModel(() => Now, new InMemorySaveStore());
            Assert.That(model.Perform(out string message), Is.True, message);
            Assert.That(model.AccountXp1104 + (model.AccountLevel1104 > 1 ? 100 : 0),
                Is.EqualTo(GameModel.PerformanceStaminaCost * GameModel.XpPerStamina1104));
            int before = model.AccountXp1104;
            Assert.That(model.CheckIn(out _), Is.True);
            Assert.That(model.AccountXp1104, Is.EqualTo((before + GameModel.CheckInXp1104) % 100).Or.EqualTo(before + GameModel.CheckInXp1104));
        }

        [Test]
        public void OldSavesStartAtTheLevelTheirProgressEarnedWithoutRepayingRewards()
        {
            var save = new GameSave { StoryProgress = 100 };
            foreach (string id in new[] { "stage-1-1", "stage-1-2", "stage-1-3", "stage-1-4", "stage-1-5" })
                save.ClearedStages.Add(new StageClear { Id = id, Stars = 3 });
            PlayerPrefs.SetString(GameModel.SaveKey, JsonUtility.ToJson(save));
            PlayerPrefs.Save();
            var model = new GameModel(() => Now);
            // (8+8+9+9+10) stamina × 10 × 2 = 880 XP → Lv5 (100+143+192+247 = 682) with 198 left.
            Assert.That(model.AccountLevel1104, Is.EqualTo(5));
            Assert.That(model.AccountXp1104, Is.EqualTo(198));
            Assert.That(model.PendingLevelUpCount1104, Is.Zero);
        }

        [Test]
        public void TheCapStopsExperience()
        {
            var model = new GameModel(() => Now, new InMemorySaveStore());
            Gain(model, int.MaxValue / 2);
            Assert.That(model.AccountLevel1104, Is.EqualTo(GameModel.MaxAccountLevel1104));
            Assert.That(model.AccountXp1104, Is.Zero);
            Assert.That(model.PendingLevelUpCount1104, Is.EqualTo(GameModel.MaxAccountLevel1104 - 1));
        }
    }
}
