using System;
using System.Collections.Generic;
using System.Linq;
using ChoSiren.Systems;
using ChoSiren.Systems.Data;
using ChoSiren.Systems.Tactics;
using NUnit.Framework;
using UnityEngine;

namespace ChoSiren.Tests.Systems
{
    public sealed class BattleRecoveryAdviceTests
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
            model = new GameModel(() => new DateTime(2026, 9, 7, 12, 0, 0));
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
        public void WipeSuggestsSurvivalAndUnderLevelPartyGetsActualTrainingQuote()
        {
            BattleSimulator battle = CreateBattle();
            foreach (BattleUnit player in battle.Units.Where(unit => unit.Side == BattleSide.Player)) player.Hp = 0;
            battle.AutoPlay(0);
            string advice = BattleRecoveryAdvice.Describe(model, battle);
            Assert.That(advice, Does.Contain("全员倒下"));
            Assert.That(advice, Does.Contain("生存"));
            Assert.That(advice, Does.Contain("队均1级，建议5级"));
            Assert.That(advice, Does.Contain("训练星璃需50金币"));
            Assert.That(advice.Length, Is.LessThanOrEqualTo(140));
        }

        [Test]
        public void LivingTimeoutSuggestsOutputButEarlyExitDoesNotPretendItTimedOut()
        {
            BattleSimulator timedOut = CreateBattle();
            timedOut.AdvanceRealtime(90000);
            Assert.That(timedOut.Units.Any(unit => unit.Side == BattleSide.Player && unit.Alive), Is.True);
            Assert.That(BattleRecoveryAdvice.Describe(model, timedOut), Does.Contain("存活但超时"));
            Assert.That(BattleRecoveryAdvice.Describe(model, timedOut), Does.Contain("输出"));
            BattleSimulator abandoned = CreateBattle();
            abandoned.AutoPlay(0);
            Assert.That(BattleRecoveryAdvice.Describe(model, abandoned), Does.Not.Contain("超时"));
        }

        [Test]
        public void UnaffordableTrainingOffersOnlyReachableGoldSources()
        {
            BattleSimulator battle = CreateBattle();
            model.Save.Gold = 0;
            string firstStage = BattleRecoveryAdvice.Describe(model, battle);
            Assert.That(firstStage, Does.Contain("金币不足"));
            Assert.That(firstStage, Does.Contain("任务金币或挂机收益"));
            Assert.That(firstStage, Does.Not.Contain("重刷"));
            model.Save.ClearedStages.Add(new StageClear { Id = "stage-1-1", Stars = 1 });
            Assert.That(BattleRecoveryAdvice.Describe(model, battle), Does.Contain("重刷已通关关卡"));
        }

        [Test]
        public void AdviceUsesBestAvailableAccessoryAndNeverChangesSaveDiceOrCombat()
        {
            BattleSimulator battle = CreateBattle();
            int best = Enumerable.Range(0, GameModel.AccessoryNames.Length)
                .OrderByDescending(model.AccessoryPowerChange).First();
            string before = JsonUtility.ToJson(model.Save);
            string persistedBefore = PlayerPrefs.GetString(GameModel.SaveKey);
            int[] hp = battle.Units.Select(unit => unit.Hp).ToArray();
            int revision = battle.BattleDice.Revision;
            string advice = BattleRecoveryAdvice.Describe(model, battle);
            Assert.That(advice, Does.Contain(GameModel.AccessoryNames[best]));
            Assert.That(advice, Does.Contain("非必然更强"));
            Assert.That(JsonUtility.ToJson(model.Save), Is.EqualTo(before));
            Assert.That(PlayerPrefs.GetString(GameModel.SaveKey), Is.EqualTo(persistedBefore));
            CollectionAssert.AreEqual(hp, battle.Units.Select(unit => unit.Hp));
            Assert.That(battle.BattleDice.Revision, Is.EqualTo(revision));
            Assert.That(battle.ElapsedMilliseconds, Is.Zero);
            Assert.That(advice.Length, Is.LessThanOrEqualTo(140));
        }

        private BattleSimulator CreateBattle()
        {
            var repository = new GameDataRepository(new ResourcesGameDataSource(), new UnityJsonReader());
            Assert.That(repository.LoadAll(), Is.True);
            var stage = new StageDefinition { Id = "recovery-test", Name = "恢复建议测试", EncounterType = "normal",
                RecommendedLevel = 5, TimeLimitSeconds = 90, ThreeStarSeconds = 75,
                Enemies = new List<EnemySpawn> { new EnemySpawn { UnitId = "siren-queen", Row = 0, Col = 0,
                    ScalePermille = 1, HpScalePermille = 1000000 } } };
            var party = model.Save.Team.Select((index, slot) => new PlayerUnitSetup {
                UnitId = GameModel.Members[index].Id, Row = slot % 3, Col = slot / 3, Level = model.LevelOf(index)
            }).ToArray();
            var battle = new BattleSimulator(repository.Tactics, stage, party, new SeededRandom(42));
            battle.EnableRealtime(GameModel.Members.ToDictionary(member => member.Id, member => member.Race), party[0].UnitId);
            return battle;
        }
    }
}
