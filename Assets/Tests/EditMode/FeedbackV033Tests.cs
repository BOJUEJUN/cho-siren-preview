using System;
using System.Linq;
using ChoSiren.Systems.Tactics;
using NUnit.Framework;
using UnityEngine;

namespace ChoSiren.Tests
{
    /// <summary>
    /// Regression cover for the 2026-09-08 player feedback pass (v0.3.3): starting purse,
    /// flattened growth curve, signing on 星钻, 好感度, accessory quality tiers and the
    /// deeper-stage loot curve.
    /// </summary>
    public sealed class FeedbackV033Tests
    {
        private DateTime now;

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey(GameModel.SaveKey);
            PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
            now = new DateTime(2026, 9, 2, 12, 0, 0, DateTimeKind.Local);
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(GameModel.SaveKey);
            PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
        }

        private GameModel NewModel() => new GameModel(() => now);

        // ------------------------------------------------------------------ economy

        [Test]
        public void NewSaveStartsWithSmallPurse()
        {
            var model = NewModel();
            Assert.That(model.Save.Diamonds, Is.EqualTo(300), "新号初始星钻应保持小额");
            Assert.That(model.Save.Gold, Is.EqualTo(1200), "新号初始星光币应保持小额");
        }

        [Test]
        public void ExistingSaveKeepsItsOwnBalance()
        {
            var existing = new GameSave { Diamonds = 5000, Gold = 9000 };
            PlayerPrefs.SetString(GameModel.SaveKey, JsonUtility.ToJson(existing));
            PlayerPrefs.Save();

            var loaded = NewModel();
            Assert.That(loaded.Save.Diamonds, Is.EqualTo(5000), "已有存档余额不能被新默认值覆盖");
            Assert.That(loaded.Save.Gold, Is.EqualTo(9000));
        }

        // ------------------------------------------------------------------ growth curve

        [Test]
        public void HighLevelStatsStayReadableAfterGrowthNerf()
        {
            var model = NewModel();
            model.Save.MemberLevels[0] = 100;
            CombatStats stats = model.StatsOf(0);

            // Old curve (1.085/1.065/1.05) produced roughly 550x attack and 2400x HP at level 100.
            Assert.That(stats.Attack, Is.LessThan(3200), "100 级攻击不应再膨胀到数万");
            Assert.That(stats.Hp, Is.LessThan(50000), "100 级生命不应再膨胀到数十万");
            Assert.That(stats.Attack, Is.GreaterThan(800), "削弱后仍要明显强于 1 级");
        }

        [Test]
        public void LevelOneStatsAreUnchangedByTheNerf()
        {
            var model = NewModel();
            Assert.That(model.StatsOf(0).Attack, Is.EqualTo(40));
            Assert.That(model.StatsOf(0).Hp, Is.EqualTo(280));
        }

        // ------------------------------------------------------------------ signing currency

        [Test]
        public void SigningSpendsDiamondsAndNeverStarCoins()
        {
            var model = NewModel();
            int candidate = model.InterviewCandidates(0).First();
            int quote = model.InterviewQuote(0, candidate);
            int diamondsBefore = model.Save.Diamonds;
            int goldBefore = model.Save.Gold;

            Assert.That(model.SignInterviewCandidate(0, candidate, model.CurrentInterviewCycle, quote, out string message),
                Is.True, message);

            Assert.That(model.Save.Diamonds, Is.EqualTo(diamondsBefore - quote), "签约应扣星钻");
            Assert.That(model.Save.Gold, Is.EqualTo(goldBefore), "签约不应扣星光币");
        }

        [Test]
        public void TrainingSpendsOnlyStarCoins()
        {
            var model = NewModel();
            int diamondsBefore = model.Save.Diamonds;
            Assert.That(model.CanTrain(0, out int cost, out _), Is.True);
            Assert.That(model.Train(0, out string message), Is.True, message);

            Assert.That(model.Save.Gold, Is.EqualTo(1200 - cost), "训练只扣星光币");
            Assert.That(model.Save.Diamonds, Is.EqualTo(diamondsBefore), "训练不扣星钻");
        }

        [Test]
        public void GoldCurrencyIsNamedStarCoin()
        {
            Assert.That(GameModel.CurrencyName("gold"), Is.EqualTo("星光币"));
            Assert.That(GameModel.CurrencyName("diamond"), Is.EqualTo("星钻"));
        }

        // ------------------------------------------------------------------ affection

        [Test]
        public void SigningGrantsStartingAffection()
        {
            var model = NewModel();
            int candidate = model.InterviewCandidates(0).First();
            Assert.That(model.AffectionOf(candidate), Is.EqualTo(0), "未签约成员好感度为 0");

            int quote = model.InterviewQuote(0, candidate);
            Assert.That(model.SignInterviewCandidate(0, candidate, model.CurrentInterviewCycle, quote, out string message),
                Is.True, message);

            Assert.That(model.AffectionOf(candidate), Is.EqualTo(GameModel.StartingAffection));
            Assert.That(model.AffectionTierOf(candidate), Is.EqualTo("陌生"));
        }

        [Test]
        public void TrainingRaisesAffectionAndPersists()
        {
            var model = NewModel();
            int before = model.AffectionOf(0);
            Assert.That(model.Train(0, out _), Is.True);
            Assert.That(model.AffectionOf(0), Is.EqualTo(before + 3));

            var loaded = NewModel();
            Assert.That(loaded.AffectionOf(0), Is.EqualTo(before + 3), "好感度必须随存档持久化");
        }

        [Test]
        public void AffectionNeverExceedsCap()
        {
            var model = NewModel();
            for (int i = 0; i < 60; i++) model.GainAffection(0, 10);
            Assert.That(model.AffectionOf(0), Is.EqualTo(GameModel.MaxAffection));
            Assert.That(model.AffectionTierOf(0), Is.EqualTo("羁绊"));
        }

        [Test]
        public void AffectionTiersFollowThresholds()
        {
            Assert.That(GameModel.AffectionTierName(0), Is.EqualTo("陌生"));
            Assert.That(GameModel.AffectionTierName(10), Is.EqualTo("友好"));
            Assert.That(GameModel.AffectionTierName(25), Is.EqualTo("熟悉"));
            Assert.That(GameModel.AffectionTierName(55), Is.EqualTo("亲密"));
            Assert.That(GameModel.AffectionTierName(80), Is.EqualTo("羁绊"));
        }

        // ------------------------------------------------------------------ accessory quality

        [Test]
        public void CombatAccessoriesAreTheRarestTier()
        {
            for (int index = 0; index < 6; index++)
                Assert.That(GameModel.AccessoryRarityOf(index), Is.EqualTo(GameModel.AccessoryRarity.Legendary));
            for (int index = 6; index < 12; index++)
                Assert.That(GameModel.AccessoryRarityOf(index), Is.EqualTo(GameModel.AccessoryRarity.Epic));
        }

        [Test]
        public void CollectionAccessoriesWalkFineRareEpicPerCategory()
        {
            GameModel.AccessoryRarity[] expected =
            {
                GameModel.AccessoryRarity.Fine, GameModel.AccessoryRarity.Fine, GameModel.AccessoryRarity.Fine,
                GameModel.AccessoryRarity.Rare, GameModel.AccessoryRarity.Rare, GameModel.AccessoryRarity.Rare,
                GameModel.AccessoryRarity.Epic, GameModel.AccessoryRarity.Epic, GameModel.AccessoryRarity.Epic,
            };
            for (int index = 12; index < GameModel.AccessoryNames.Length; index++)
                Assert.That(GameModel.AccessoryRarityOf(index), Is.EqualTo(expected[(index - 12) % 9]),
                    $"饰品 {GameModel.AccessoryNames[index]} 品质分档错误");
        }

        [Test]
        public void EveryAccessoryHasAQualityNameAndStatDescription()
        {
            for (int index = 0; index < GameModel.AccessoryNames.Length; index++)
            {
                Assert.That(string.IsNullOrEmpty(GameModel.AccessoryRarityNameOf(index)), Is.False);
                Assert.That(GameModel.AccessoryStatDescription(index), Does.Contain("+"),
                    $"{GameModel.AccessoryNames[index]} 缺少属性说明");
                Assert.That(GameModel.AccessoryRarityColorHexOf(index), Does.StartWith("#"));
            }
        }

        // ------------------------------------------------------------------ loot curve

        [Test]
        public void DeeperStagesDropHigherQualityAccessories()
        {
            var model = NewModel();
            int RareCount(string stageId) => model.StageLootCandidates(stageId).Count(candidate =>
            {
                int index = GameModel.AccessoryIndexForItem(candidate.ItemId);
                return index >= 0 && GameModel.AccessoryRarityOf(index) >= GameModel.AccessoryRarity.Epic;
            });

            Assert.That(RareCount("stage-1-1"), Is.EqualTo(1), "首关只应含极少量高稀有度");
            Assert.That(RareCount("stage-1-10"), Is.GreaterThan(RareCount("stage-1-1")),
                "越后面的关卡稀有掉落应越多");
            Assert.That(RareCount("stage-1-10"), Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void LootTablesStillExposeEveryStage()
        {
            var model = NewModel();
            for (int stage = 1; stage <= 10; stage++)
            {
                var candidates = model.StageLootCandidates($"stage-1-{stage}");
                Assert.That(candidates.Count, Is.GreaterThan(0), $"stage-1-{stage} 掉落表为空");
            }
        }

        // ------------------------------------------------------------------ interview cooldown

        [Test]
        public void InterviewCooldownShowsRemainingHours()
        {
            var model = NewModel();
            string label = model.NextInterviewRefreshLabel();
            Assert.That(label, Does.Contain("小时").Or.Contain("分钟").Or.EqualTo("新名单已开放"));
            Assert.That(model.TimeUntilNextInterviewCycle(), Is.GreaterThan(TimeSpan.Zero));
            Assert.That(model.TimeUntilNextInterviewCycle(), Is.LessThanOrEqualTo(TimeSpan.FromHours(24)));
        }

        [Test]
        public void InterviewCooldownCountsDownToTheEveningBoundary()
        {
            // 12:00 local -> next 18:00 boundary is 6 hours away.
            var model = NewModel();
            Assert.That(model.TimeUntilNextInterviewCycle().TotalHours, Is.EqualTo(6).Within(0.01));
            Assert.That(model.NextInterviewRefreshLabel(), Is.EqualTo("6 小时后可约下一批"));
        }
    }
}
