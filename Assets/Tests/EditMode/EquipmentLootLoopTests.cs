using System;
using System.Collections.Generic;
using System.Linq;
using ChoSiren.Systems.Economy;
using ChoSiren.Systems.Tactics;
using NUnit.Framework;
using UnityEngine;

namespace ChoSiren.Tests
{
    public sealed class EquipmentLootLoopTests
    {
        private const string StageId = "stage-1-1";
        /// <summary>本关首通必得的饰品索引。不写死数字，品质曲线调整后测试仍成立。</summary>
        private static int Guaranteed => GameModel.FirstClearAccessory(StageId);
        private static readonly DateTime Now = new DateTime(2026, 9, 7, 12, 0, 0);

        [SetUp]
        public void SetUp() => ClearSave();

        [TearDown]
        public void TearDown() => ClearSave();

        private static void ClearSave()
        {
            PlayerPrefs.DeleteKey(GameModel.SaveKey);
            PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
        }

        private static GameModel CreateModel(bool guaranteedRandomEquipment = false)
        {
            // Deterministic short battle isolates settlement from combat balancing.
            var tactics = GameModelTests.BuildTactics();
            var stage = tactics.Stages[0];
            stage.Id = StageId;
            stage.Drops = new DropTable { Rolls = guaranteedRandomEquipment ? 1 : 0,
                Entries = new List<DropEntry> { new DropEntry {
                    ItemId = GameModel.AccessoryItemIds[Guaranteed], Weight = 1, Min = 1, Max = 1 } } };
            return new GameModel(() => Now, null, null, tactics, null);
        }

        private static BattleSimulator Win(GameModel model, ulong seed = 1)
        {
            BattleSimulator battle = model.StartStageBattle(StageId, seed, out string message);
            Assert.That(battle, Is.Not.Null, message);
            Assert.That(battle.AutoPlay(), Is.EqualTo(BattleOutcome.Victory));
            model.SettleStageBattle(battle, out message);
            Assert.That(message, Does.Contain("胜利"));
            return battle;
        }

        [Test]
        public void FirstClearGuaranteesEquipmentEvenWhenRandomDropsAreDisabled()
        {
            var model = CreateModel();
            int diamonds = model.Save.Diamonds;
            Assert.That(model.OwnsAccessory(Guaranteed), Is.False);
            Assert.That(model.StageEquipmentPreview(StageId), Does.Contain("首通必得"));
            Win(model);
            Assert.That(model.OwnsAccessory(Guaranteed), Is.True);
            Assert.That(model.Save.EquipmentFragments, Is.Zero);
            Assert.That(model.Save.Diamonds, Is.EqualTo(diamonds + model.Tactics.FindStage(StageId).DiamondFirstClear));
            Assert.That(model.LastAwardedAccessory, Is.EqualTo(Guaranteed));
            Assert.That(model.Save.EquipmentFirstClearClaims, Is.EqualTo(new[] { StageId }));
            Assert.That(model.StageEquipmentPreview(StageId), Does.Contain("首通已领取"));
            var loaded = CreateModel();
            Assert.That(loaded.OwnsAccessory(Guaranteed), Is.True);
            Assert.That(loaded.Save.EquipmentFragments, Is.Zero);
        }

        [Test]
        public void RepeatClearPaysNormalGoldButNoFirstClearEquipmentOrDiamonds()
        {
            var model = CreateModel();
            Win(model);
            int diamonds = model.Save.Diamonds;
            int gold = model.Save.Gold;
            Win(model, 2);
            Assert.That(model.Save.Diamonds, Is.EqualTo(diamonds));
            Assert.That(model.Save.Gold, Is.EqualTo(gold + model.Tactics.FindStage(StageId).GoldReward));
            Assert.That(model.Save.EquipmentFragments, Is.Zero);
            Assert.That(model.LastAwardedAccessory, Is.EqualTo(-1));
            Assert.That(model.Save.EquipmentFirstClearClaims.Count, Is.EqualTo(1));
        }

        [Test]
        public void SameBattleCannotPayEquipmentOrCurrenciesTwice()
        {
            var model = CreateModel(true);
            BattleSimulator battle = Win(model);
            string before = JsonUtility.ToJson(model.Save);
            model.SettleStageBattle(battle, out string message);
            Assert.That(message, Does.Contain("已经结算"));
            Assert.That(JsonUtility.ToJson(model.Save), Is.EqualTo(before));
        }

        [Test]
        public void RandomDuplicateEquipmentConvertsToGoldEveryClear()
        {
            // 碎片退役后，重复饰品改为折算金币补偿。
            var model = CreateModel(true);
            int goldBefore = model.Save.Gold;
            Win(model);
            Assert.That(model.Save.EquipmentFragments, Is.Zero, "不再产出碎片。");
            Assert.That(model.Save.OwnedAccessories.Count(i => i == Guaranteed), Is.EqualTo(1));
            int afterFirst = model.Save.Gold;
            Assert.That(afterFirst, Is.GreaterThan(goldBefore), "首通同物重复应折算金币。");
            Win(model, 2);
            Assert.That(model.Save.Gold - afterFirst,
                Is.GreaterThanOrEqualTo(GameModel.DuplicateAccessoryGold),
                "再次通关的重复饰品同样折算金币。");
            Assert.That(CreateModel(true).Save.Gold, Is.EqualTo(model.Save.Gold), "金币必须持久化。");
        }

        [Test]
        public void OldClearReceivesEquipmentCompensationOnceWithoutRegrantingDiamonds()
        {
            var old = new GameSave { SchemaVersion = 4, Diamonds = 321, Gold = 654,
                ClearedStages = new List<StageClear> { new StageClear { Id = StageId, Stars = 2 },
                    new StageClear { Id = "stage-1-2", Stars = 1 } } };
            PlayerPrefs.SetString(GameModel.SaveKey, JsonUtility.ToJson(old));
            for (int reload = 0; reload < 3; reload++)
            {
                var model = CreateModel();
                Assert.That(model.OwnsAccessory(Guaranteed), Is.True);
                Assert.That(model.OwnsAccessory(GameModel.FirstClearAccessory("stage-1-2")), Is.True,
                    "从未领取过装备首通奖励的旧档按当前 1-2 奖励补偿。");
                Assert.That(model.Save.EquipmentFragments, Is.Zero);
                Assert.That(model.Save.EquipmentFirstClearClaims.Count, Is.EqualTo(2));
                Assert.That(model.Save.Diamonds, Is.EqualTo(321));
                Assert.That(model.Save.Gold, Is.EqualTo(654));
                Assert.That(model.StarsOf(StageId), Is.EqualTo(2));
            }
        }

        [Test]
        public void PreviouslyMisfiledEquipmentInCostumesBecomesUsableWithoutDeletingCosmetics()
        {
            var old = new GameSave { OwnedCostumes = new List<string> { GameModel.AccessoryItemIds[4], "costume-kept" } };
            PlayerPrefs.SetString(GameModel.SaveKey, JsonUtility.ToJson(old));
            var model = CreateModel();
            Assert.That(model.OwnsAccessory(4), Is.True);
            Assert.That(model.OwnsCostume("costume-kept"), Is.True);
            Assert.That(model.EquipAccessoryForMember(0, 4, out _), Is.True);
            Assert.That(CreateModel().EquippedAccessoryFor(0), Is.EqualTo(4));
        }

        [Test]
        public void ProductionStagesAdvertiseTheEquipmentActuallyInTheirDropTables()
        {
            var model = new GameModel(() => Now);
            for (int stageNumber = 1; stageNumber <= 10; stageNumber++)
            {
                string stageId = $"stage-1-{stageNumber}";
                int item = GameModel.FirstClearAccessory(stageId);
                var stage = model.Tactics.FindStage(stageId);
                Assert.That(stage.Drops.Entries.Any(e => e.ItemId == GameModel.AccessoryItemIds[item] && e.Weight > 0), Is.True, stageId);
                Assert.That(model.StageItemDropChance(stageId, GameModel.AccessoryItemIds[item]), Is.InRange(0.001f, 1f));
                Assert.That(model.StageLootDescription(stageId), Does.Contain(GameModel.AccessoryNames[item]));
            }
            // v0.3.3: 概率按当前掉落表动态推导(原硬编码 0.19 对应旧 2 rolls/权重 10/100)
            float advertisedChance = model.StageItemDropChance(StageId, GameModel.AccessoryItemIds[Guaranteed]);
            Assert.That(advertisedChance, Is.GreaterThan(0.05f).And.LessThan(0.5f),
                "stage-1-1 首通物应有合理掉率,且符合关卡递进");
        }
    }
}
