using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ChoSiren.Systems.Economy;
using ChoSiren.Systems.Tactics;
using NUnit.Framework;
using UnityEngine;

namespace ChoSiren.Tests
{
    /// <summary>2026-11-07 会议数值 (Docs/BALANCE-1107-LV200.md): Lv200, gold-only below 30, 残魂 + race 生魂 after.</summary>
    public sealed class Level200Tests1107
    {
        private static readonly DateTime Now = new DateTime(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc);
        // 2026-11-11: chapters run to 20 stages; 1-15 / 1-20 gate the cap too (Docs/BALANCE-1111-CHAPTER20.md).
        private static readonly string[] AllGates = { "stage-1-5", "stage-1-10", "stage-1-15", "stage-1-20", "stage-2-5", "stage-2-10" };

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
            model.Save.Gold = 10_000_000;
            return model;
        }

        [Test]
        public void CapFollowsGatesThenOpensToTwoHundred()
        {
            Assert.That(GameModel.MaxMemberLevel, Is.EqualTo(200));
            Assert.That(NewModel(1).ProgressLevelCap1104, Is.EqualTo(12));
            Assert.That(NewModel(1, "stage-1-5").ProgressLevelCap1104, Is.EqualTo(17));
            Assert.That(NewModel(1, "stage-1-5", "stage-1-10").ProgressLevelCap1104, Is.EqualTo(20));
            Assert.That(NewModel(1, "stage-1-5", "stage-1-10", "stage-1-15").ProgressLevelCap1104, Is.EqualTo(23));
            Assert.That(NewModel(1, "stage-1-5", "stage-1-10", "stage-1-15", "stage-1-20").ProgressLevelCap1104, Is.EqualTo(26));
            Assert.That(NewModel(1, AllGates.Take(5).ToArray()).ProgressLevelCap1104, Is.EqualTo(29));
            Assert.That(NewModel(1, "stage-1-5", "stage-1-10", "stage-2-5").ProgressLevelCap1104, Is.EqualTo(20),
                "2-5 does not skip 1-15 / 1-20");
            Assert.That(NewModel(1, AllGates).ProgressLevelCap1104, Is.EqualTo(200));
            Assert.That(NewModel(1, AllGates).NextGateName1104, Is.Empty);
            Assert.That(NewModel(1, "stage-1-10").ProgressLevelCap1104, Is.EqualTo(12), "gates count in order");
            Assert.That(NewModel(1).NextGateName1104, Is.EqualTo("1-5"));
        }

        [Test]
        public void BelowThirtyTrainingIsGoldOnlyWithNoBreakthrough()
        {
            GameModel model = NewModel(17, "stage-1-5", "stage-1-10");
            Assert.That(model.NeedsBreakthrough1104(0), Is.False, "the 1104 Lv17 break is gone");
            model.Save.VoiceCrystals = 0;
            int gold = model.Save.Gold;
            Assert.That(model.Train(0, out string message), Is.True, message);
            Assert.That(model.LevelOf(0), Is.EqualTo(18));
            Assert.That(model.Save.Gold, Is.EqualTo(gold - BattleSimulator.TrainingCostAtLevel(17)));
            Assert.That(model.Save.VoiceCrystals, Is.EqualTo(0));

            GameModel capped = NewModel(12);
            Assert.That(capped.CanTrain(0, out _, out string reason), Is.False);
            Assert.That(reason, Is.EqualTo("通关 1-5 解锁"));
        }

        [Test]
        public void ThirtyNeedsTenRaceSoulsAndGold()
        {
            GameModel model = NewModel(30, AllGates);
            Assert.That(model.NeedsBreakthrough1104(0), Is.True);
            Assert.That(model.CanTrain(0, out _, out string trainReason), Is.False);
            Assert.That(trainReason, Is.EqualTo("需要突破"));
            Assert.That(model.CanBreakthrough1104(0, out GameModel.BreakCost1104 cost, out string reason), Is.False);
            Assert.That(cost.Souls, Is.EqualTo(10));
            Assert.That(cost.Gold, Is.EqualTo(3000));
            Assert.That(cost.SoulId, Is.EqualTo(model.SoulIdOf1107(0)));
            Assert.That(reason, Does.Contain("生魂不足"));

            model.Grant(new CurrencyAmount(cost.SoulId, 12));
            int gold = model.Save.Gold;
            Assert.That(model.Breakthrough1104(0, out string message), Is.True, message);
            Assert.That(model.Balance(cost.SoulId), Is.EqualTo(2));
            Assert.That(model.Save.Gold, Is.EqualTo(gold - 3000));
            Assert.That(model.LevelCapOf1104(0), Is.EqualTo(40));
            Assert.That(model.NextBreakCost1104(0).Gold, Is.EqualTo(6000));
        }

        [Test]
        public void MissingSoulsUseUniversalThenShardsAtTenEach()
        {
            GameModel model = NewModel(30, AllGates);
            string soul = model.SoulIdOf1107(0);
            model.Grant(new CurrencyAmount(soul, 4));
            model.Save.VoiceCores = 3;
            model.Save.Shards = 29;
            Assert.That(model.CanBreakthrough1104(0, out _, out _), Is.False, "3 souls short need 30 shards");
            model.Save.Shards = 35;
            Assert.That(model.Breakthrough1104(0, out string message), Is.True, message);
            Assert.That(model.Balance(soul), Is.EqualTo(0));
            Assert.That(model.Save.VoiceCores, Is.EqualTo(0));
            Assert.That(model.Save.Shards, Is.EqualTo(5));
        }

        [Test]
        public void AfterThirtyEachLevelAlsoCostsRemnants()
        {
            Assert.That(GameModel.RemnantCostAtLevel1107(29), Is.EqualTo(0));
            Assert.That(GameModel.RemnantCostAtLevel1107(30), Is.EqualTo(2));
            Assert.That(GameModel.RemnantCostAtLevel1107(49), Is.EqualTo(3));
            Assert.That(GameModel.RemnantCostAtLevel1107(199), Is.EqualTo(18));
            Assert.That(Enumerable.Range(30, 170).Sum(GameModel.RemnantCostAtLevel1107), Is.EqualTo(1700));

            GameModel model = NewModel(31, AllGates);
            model.Save.VoiceCrystals = 1;
            Assert.That(model.CanTrain(0, out _, out string reason), Is.False);
            Assert.That(reason, Does.StartWith("敌人残魂不足"));
            model.Save.VoiceCrystals = 5;
            Assert.That(model.Train(0, out string message), Is.True, message);
            Assert.That(model.Save.VoiceCrystals, Is.EqualTo(3));
        }

        [Test]
        public void LevelsAlreadyEarnedAreKeptAndImplyBreaks()
        {
            GameModel model = NewModel(32, AllGates);
            Assert.That(model.LevelOf(0), Is.EqualTo(32));
            Assert.That(model.BreaksOf1104(0), Is.EqualTo(1));
            Assert.That(model.LevelCapOf1104(0), Is.EqualTo(40));
            Assert.That(NewModel(200, AllGates).NeedsBreakthrough1104(0), Is.False);
            Assert.That(NewModel(200, AllGates).CanTrain(0, out _, out _), Is.False);
            Assert.That(NewModel(190, AllGates).NeedsBreakthrough1104(0), Is.True);
            Assert.That(NewModel(191, AllGates).LevelCapOf1104(0), Is.EqualTo(200));
        }

        [Test]
        public void SeventeenBreaksTakeAMemberToTwoHundred()
        {
            GameModel model = NewModel(30, AllGates);
            string soul = model.SoulIdOf1107(0);
            model.Grant(new CurrencyAmount(soul, 170));
            model.Save.VoiceCrystals = 1700;
            int breaks = 0;
            while (model.LevelOf(0) < 200)
            {
                if (model.NeedsBreakthrough1104(0)) { Assert.That(model.Breakthrough1104(0, out string m), Is.True, m); breaks++; }
                else Assert.That(model.Train(0, out string t), Is.True, t);
            }
            Assert.That(breaks, Is.EqualTo(17));
            Assert.That(model.Balance(soul), Is.EqualTo(0));
            Assert.That(model.Save.VoiceCrystals, Is.EqualTo(0));
        }

        [Test]
        public void RacesMapToTheirOwnSoul()
        {
            Assert.That(GameModel.SoulIdForRace1107("魅族"), Is.EqualTo(CurrencyIds.SoulCharm));
            Assert.That(GameModel.SoulIdForRace1107("魔族 · 恶魔"), Is.EqualTo(CurrencyIds.SoulDemon));
            Assert.That(GameModel.SoulIdForRace1107("海灵族 · 人鱼"), Is.EqualTo(CurrencyIds.SoulSea));
            Assert.That(GameModel.SoulIdForRace1107("血精灵"), Is.EqualTo(CurrencyIds.SoulBlood));
        }

        [Test]
        public void StagesPayRemnantsAndTheirRaceSoul()
        {
            GameModel model = NewModel(1);
            MethodInfo add = typeof(GameModel).GetMethod("AddMaterialRewards1104", BindingFlags.Instance | BindingFlags.NonPublic);
            List<(string, int)> Roll(string stageId, bool first, ulong seed)
            {
                var rewards = new List<(string ItemId, int Amount)>();
                add.Invoke(model, new object[] { model.Tactics.FindStage(stageId), first, seed, rewards });
                return rewards;
            }
            Assert.That(Roll("stage-1-1", true, 1), Does.Contain((CurrencyIds.VoiceCrystal, 1)));
            string bossSoul = model.StageSoulId1107("stage-1-10");
            Assert.That(Roll("stage-1-10", true, 1), Does.Contain((bossSoul, 5)));
            Assert.That(Roll("stage-1-5", true, 1), Does.Contain((model.StageSoulId1107("stage-1-5"), 3)));

            int eliteSoulRuns = 0;
            string eliteSoul = model.StageSoulId1107("stage-1-3");
            for (ulong seed = 1; seed <= 1000; seed++)
                if (Roll("stage-1-3", false, seed).Any(r => r.Item1 == eliteSoul)) eliteSoulRuns++;
            Assert.That(eliteSoulRuns, Is.InRange(140, 260), "elites drop their soul about one time in five");

            // Every race has at least one elite stage to farm.
            var eliteRaces = model.Tactics.Stages.Where(s => s.EncounterType == "elite").Select(s => model.StageSoulId1107(s.Id)).Distinct();
            Assert.That(eliteRaces, Is.EquivalentTo(CurrencyIds.Souls));
        }

        [Test]
        public void MaterialsAreCurrenciesWithNewNames()
        {
            GameModel model = NewModel(1);
            model.Grant(new CurrencyAmount(CurrencyIds.VoiceCrystal, 4));
            model.Grant(new CurrencyAmount(CurrencyIds.SoulSea, 2));
            Assert.That(model.Balance(CurrencyIds.VoiceCrystal), Is.EqualTo(4));
            Assert.That(model.Balance(CurrencyIds.SoulSea), Is.EqualTo(2));
            Assert.That(model.TrySpend(CurrencyIds.SoulSea, 1), Is.True);
            Assert.That(model.Balance(CurrencyIds.SoulSea), Is.EqualTo(1));
            Assert.That(model.Save.OwnedCostumes, Does.Not.Contain(CurrencyIds.SoulSea));
            Assert.That(GameModel.CurrencyName(CurrencyIds.VoiceCrystal), Is.EqualTo("敌人残魂"));
            Assert.That(GameModel.CurrencyName(CurrencyIds.VoiceCore), Is.EqualTo("通用生魂"));
            Assert.That(GameModel.CurrencyName(CurrencyIds.SoulBlood), Is.EqualTo("血精灵生魂"));
        }

        [Test]
        public void GoldCurveAndGrowthMatchTheDesignDoc()
        {
            Assert.That(BattleSimulator.TrainingCostAtLevel(10), Is.EqualTo(176));
            Assert.That(BattleSimulator.TrainingCostAtLevel(29), Is.EqualTo(2504));
            Assert.That(BattleSimulator.TrainingCostAtLevel(30), Is.EqualTo(2880));
            Assert.That(BattleSimulator.TrainingCostAtLevel(199), Is.EqualTo(12006));
            long total = Enumerable.Range(30, 170).Sum(l => (long)BattleSimulator.TrainingCostAtLevel(l));
            Assert.That(total, Is.InRange(1_260_000L, 1_270_000L));
            Assert.That(BattleSimulator.GrowthSteps1107(30), Is.EqualTo(29));
            Assert.That(BattleSimulator.GrowthSteps1107(200), Is.EqualTo(114));
        }
    }
}
