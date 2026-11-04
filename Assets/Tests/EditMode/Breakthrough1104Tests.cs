using System;
using System.Collections.Generic;
using System.Reflection;
using ChoSiren.Systems.Economy;
using ChoSiren.Systems.Tactics;
using NUnit.Framework;
using UnityEngine;

namespace ChoSiren.Tests
{
    /// <summary>2026-11-04 数值: stage-progress level caps, per-member 突破 and its materials.</summary>
    public sealed class Breakthrough1104Tests
    {
        private static readonly DateTime Now = new DateTime(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

        [SetUp, TearDown]
        public void Clean()
        {
            PlayerPrefs.DeleteKey(GameModel.SaveKey);
            PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
        }

        private static GameModel NewModel(int level, params string[] cleared)
        {
            var model = new GameModel(() => Now, new InMemorySaveStore());
            model.Save.StoryProgress = 100;
            for (int i = 0; i < model.Save.MemberLevels.Count; i++) model.Save.MemberLevels[i] = level;
            foreach (string id in cleared) model.Save.ClearedStages.Add(new StageClear { Id = id, Stars = 3 });
            model.Save.Gold = 1_000_000;
            return model;
        }

        [Test]
        public void CapStartsAtTwelveAndEachFifthStageAddsFive()
        {
            Assert.That(NewModel(1).ProgressLevelCap1104, Is.EqualTo(12));
            Assert.That(NewModel(1, "stage-1-5").ProgressLevelCap1104, Is.EqualTo(17));
            Assert.That(NewModel(1, "stage-1-5", "stage-1-10").ProgressLevelCap1104, Is.EqualTo(22));
            Assert.That(NewModel(1, "stage-1-5", "stage-1-10", "stage-2-5", "stage-2-10").ProgressLevelCap1104, Is.EqualTo(32));
            // Gates count in order: 1-10 without 1-5 opens nothing.
            Assert.That(NewModel(1, "stage-1-10").ProgressLevelCap1104, Is.EqualTo(12));
            Assert.That(NewModel(1).NextGateName1104, Is.EqualTo("1-5"));
        }

        [Test]
        public void TrainingStopsAtTheCapAndNamesTheStageThatOpensIt()
        {
            GameModel model = NewModel(12);
            Assert.That(model.CanTrain(0, out _, out string reason), Is.False);
            Assert.That(reason, Is.EqualTo("通关 1-5 解锁"));
            Assert.That(model.Train(0, out _), Is.False);
            Assert.That(model.LevelOf(0), Is.EqualTo(12));

            model.Save.MemberLevels[0] = 11;
            Assert.That(model.Train(0, out string message), Is.True, message);
            Assert.That(model.LevelOf(0), Is.EqualTo(12));
        }

        [Test]
        public void SeventeenNeedsABreakthroughPaidInCrystalsAndGold()
        {
            GameModel model = NewModel(17, "stage-1-5", "stage-1-10");
            Assert.That(model.NeedsBreakthrough1104(0), Is.True);
            Assert.That(model.CanTrain(0, out _, out string trainReason), Is.False);
            Assert.That(trainReason, Is.EqualTo("需要突破"));
            Assert.That(model.CanBreakthrough1104(0, out GameModel.BreakCost1104 cost, out string reason), Is.False);
            Assert.That(reason, Does.StartWith("声纹碎晶不足"));
            Assert.That(cost.Crystals, Is.EqualTo(10));
            Assert.That(cost.Gold, Is.EqualTo(3000));

            model.Save.VoiceCrystals = 12;
            int gold = model.Save.Gold;
            Assert.That(model.Breakthrough1104(0, out string message), Is.True, message);
            Assert.That(model.Save.VoiceCrystals, Is.EqualTo(2));
            Assert.That(model.Save.Gold, Is.EqualTo(gold - 3000));
            Assert.That(model.LevelCapOf1104(0), Is.EqualTo(22));
            Assert.That(model.Train(0, out string trained), Is.True, trained);
            Assert.That(model.LevelOf(0), Is.EqualTo(18));
        }

        [Test]
        public void NoBreakthroughIsOfferedBeforeTheStageAllowsMore()
        {
            GameModel model = NewModel(17, "stage-1-5");
            Assert.That(model.NeedsBreakthrough1104(0), Is.False);
            Assert.That(model.LevelCapReason1104(0), Is.EqualTo("通关 1-10 解锁"));
        }

        [Test]
        public void MissingCoresAreCoveredByShardsAtFiftyEach()
        {
            GameModel model = NewModel(27, "stage-1-5", "stage-1-10", "stage-2-5", "stage-2-10");
            Assert.That(model.BreaksOf1104(0), Is.EqualTo(1), "Lv27 already implies the first breakthrough");
            Assert.That(model.NeedsBreakthrough1104(0), Is.True);
            model.Save.VoiceCrystals = 20;
            model.Save.VoiceCores = 1;
            model.Save.Shards = 49;
            Assert.That(model.CanBreakthrough1104(0, out _, out string reason), Is.False);
            Assert.That(reason, Does.StartWith("声核不足"));
            model.Save.Shards = 60;
            Assert.That(model.Breakthrough1104(0, out string message), Is.True, message);
            Assert.That(model.Save.VoiceCores, Is.EqualTo(0));
            Assert.That(model.Save.Shards, Is.EqualTo(10));
            Assert.That(model.LevelCapOf1104(0), Is.EqualTo(32));
        }

        [Test]
        public void LevelsAboveTheCapAreKeptButStopTraining()
        {
            GameModel model = NewModel(30);
            Assert.That(model.LevelOf(0), Is.EqualTo(30));
            Assert.That(model.CanTrain(0, out _, out _), Is.False);
            Assert.That(model.BreaksOf1104(0), Is.EqualTo(2));
        }

        [Test]
        public void EliteAndBossStagesPayBreakthroughMaterials()
        {
            GameModel model = NewModel(1);
            MethodInfo add = typeof(GameModel).GetMethod("AddMaterialRewards1104", BindingFlags.Instance | BindingFlags.NonPublic);
            List<(string, int)> Roll(string stageId, bool first, ulong seed)
            {
                var rewards = new List<(string ItemId, int Amount)>();
                add.Invoke(model, new object[] { model.Tactics.FindStage(stageId), first, seed, rewards });
                return rewards;
            }
            Assert.That(Roll("stage-1-1", true, 1), Is.Empty, "normal stages pay gold only");
            foreach ((string id, int amount) in Roll("stage-1-3", false, 7))
            {
                Assert.That(id, Is.EqualTo(CurrencyIds.VoiceCrystal));
                Assert.That(amount, Is.InRange(2, 3));
            }
            Assert.That(Roll("stage-1-10", true, 1), Does.Contain((CurrencyIds.VoiceCrystal, 3)));
            Assert.That(Roll("stage-1-10", true, 1), Does.Contain((CurrencyIds.VoiceCore, 3)));
            Assert.That(Roll("stage-1-5", true, 1), Does.Contain((CurrencyIds.VoiceCore, 1)));
            int cores = 0;
            for (ulong seed = 1; seed <= 400; seed++)
                foreach ((string id, int amount) in Roll("stage-1-5", false, seed))
                    if (id == CurrencyIds.VoiceCore) cores += amount;
            Assert.That(cores, Is.InRange(20, 65), "a boss replay pays a core about one time in ten");
        }

        [Test]
        public void MaterialsAreCurrenciesWithNames()
        {
            GameModel model = NewModel(1);
            model.Grant(new CurrencyAmount(CurrencyIds.VoiceCrystal, 4));
            model.Grant(new CurrencyAmount(CurrencyIds.VoiceCore, 2));
            Assert.That(model.Balance(CurrencyIds.VoiceCrystal), Is.EqualTo(4));
            Assert.That(model.Balance(CurrencyIds.VoiceCore), Is.EqualTo(2));
            Assert.That(model.Save.OwnedCostumes, Does.Not.Contain(CurrencyIds.VoiceCrystal));
            Assert.That(GameModel.CurrencyName(CurrencyIds.VoiceCrystal), Is.EqualTo("声纹碎晶"));
            Assert.That(GameModel.CurrencyName(CurrencyIds.VoiceCore), Is.EqualTo("声核"));
        }

        [Test]
        public void TrainingCostGrowsMoreGentlyAfterLevelThirty()
        {
            Assert.That(BattleSimulator.TrainingCostAtLevel(10), Is.EqualTo(176));
            Assert.That(BattleSimulator.TrainingCostAtLevel(30), Is.EqualTo(2879));
            Assert.That(BattleSimulator.TrainingCostAtLevel(31), Is.EqualTo((int)Math.Ceiling(50 * Math.Pow(1.15, 29) * 1.08)));
            Assert.That(BattleSimulator.TrainingCostAtLevel(40), Is.LessThan(7000));
        }
    }
}
