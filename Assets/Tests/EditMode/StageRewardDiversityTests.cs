using System;
using System.Collections.Generic;
using System.Linq;
using ChoSiren.Systems;
using ChoSiren.Systems.Economy;
using ChoSiren.Systems.Tactics;
using ChoSiren.UI;
using NUnit.Framework;
using UnityEngine;

namespace ChoSiren.Tests
{
    public sealed class StageRewardDiversityTests
    {
        private static readonly DateTime Now = new DateTime(2026, 9, 7, 12, 0, 0);
        [SetUp] public void SetUp() => ClearSave();
        [TearDown] public void TearDown() => ClearSave();
        private static void ClearSave()
        {
            PlayerPrefs.DeleteKey(GameModel.SaveKey);
            PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
        }

        [Test]
        public void EachStageHasDistinctNineItemPoolAndAllSixtySixEquipmentsAreReachable()
        {
            var model = new GameModel(() => Now);
            var signatures = new HashSet<string>();
            var firstRewards = new HashSet<int>();
            var reachable = new HashSet<int>();
            for (int number = 1; number <= 10; number++)
            {
                string stageId = "stage-1-" + number;
                IReadOnlyList<StageLootCandidate> pool = model.StageLootCandidates(stageId);
                // 强化碎片已从游戏移除，奖池由 9 项（7 饰品 + 碎片 + 金币）缩为 8 项。
                Assert.That(pool.Count, Is.EqualTo(8), stageId);
                Assert.That(pool.Count(item => GameModel.AccessoryIndexForItem(item.ItemId) >= 0), Is.EqualTo(7));
                Assert.That(pool.Any(item => item.ItemId == GameModel.EquipmentFragmentItemId), Is.False,
                    "碎片已移除，奖池不应再出现强化碎片。");
                Assert.That(signatures.Add(string.Join("|", pool.Select(item => item.ItemId))), Is.True,
                    "各关必须有不同的定向刷取选择，而不是同一掉落列表。");
                int guaranteed = GameModel.FirstClearAccessory(stageId);
                firstRewards.Add(guaranteed);
                Assert.That(pool.Any(item => item.ItemId == GameModel.AccessoryItemIds[guaranteed]), Is.True);
                foreach (StageLootCandidate item in pool)
                {
                    Assert.That(item.Chance, Is.InRange(.001f, 1f));
                    Assert.That(item.Minimum, Is.GreaterThan(0));
                    Assert.That(item.Maximum, Is.GreaterThanOrEqualTo(item.Minimum));
                    Assert.That(item.Use, Is.Not.EqualTo("收藏资源"), item.ItemId + " 必须有消费用途");
                    Assert.That(RewardItemVisuals.SpriteFor(item.ItemId), Is.Not.Null, item.ItemId);
                    int accessory = GameModel.AccessoryIndexForItem(item.ItemId);
                    if (accessory >= 0) reachable.Add(accessory);
                }
            }
            Assert.That(firstRewards.Count, Is.GreaterThanOrEqualTo(8));
            Assert.That(reachable.Count, Is.EqualTo(GameModel.AccessoryNames.Length));
        }

        [Test]
        public void AdvertisedChanceMatchesActualIndependentRolls()
        {
            var model = new GameModel(() => Now);
            const string stageId = "stage-1-10";
            string itemId = GameModel.AccessoryItemIds[3];
            // v0.3.3: 期望改为按当前掉落表动态推导,而非硬编码 1-0.9*0.9
            float advertised = model.StageItemDropChance(stageId, itemId);
            Assert.That(advertised, Is.GreaterThan(0f).And.LessThan(1f),
                "stage-1-10 必须能出 neon-clip 且不是必出");
            var random = new SeededRandom(847);
            int found = 0;
            const int trials = 10000;
            for (int i = 0; i < trials; i++)
                if (DropResolver.Roll(model.Tactics.FindStage(stageId).Drops, random).Any(item => item.ItemId == itemId)) found++;
            Assert.That(found / (float)trials, Is.EqualTo(advertised).Within(.025f));
        }

        [Test]
        public void StageLootQualityRisesAlongChapterOne()
        {
            var model = new GameModel(() => Now);
            int previousMax = -1;
            for (int stageNumber = 1; stageNumber <= 10; stageNumber++)
            {
                StageDefinition stage = model.Tactics.FindStage($"stage-1-{stageNumber}");
                int max = stage.Drops.Entries
                    .Select(e => GameModel.AccessoryIndexForItem(e.ItemId))
                    .Where(i => i >= 0)
                    .Max(i => (int)GameModel.AccessoryRarityOf(i));
                Assert.That(max, Is.GreaterThanOrEqualTo(previousMax),
                    $"stage-1-{stageNumber} 可掉落品质不得比前一关倒退");
                previousMax = max;
            }
            Assert.That(previousMax, Is.EqualTo((int)GameModel.AccessoryRarity.Legendary),
                "收官关必须能出传说品质");
            StageDefinition first = model.Tactics.FindStage("stage-1-1");
            int firstMax = first.Drops.Entries
                .Select(e => GameModel.AccessoryIndexForItem(e.ItemId))
                .Where(i => i >= 0)
                .Max(i => (int)GameModel.AccessoryRarityOf(i));
            Assert.That(firstMax, Is.LessThanOrEqualTo((int)GameModel.AccessoryRarity.Fine),
                "首关不得出稀有以上装备");
        }

        [Test]
        public void ZeroQuantityRandomEntriesAreCountedAsMissesInPublishedProbability()
        {
            var tactics = GameModelTests.BuildTactics();
            StageDefinition stage = tactics.Stages[0];
            stage.Drops = new DropTable { Rolls = 2, Entries = new List<DropEntry>
            {
                new DropEntry { ItemId = GameModel.EquipmentFragmentItemId, Weight = 1, Min = 0, Max = 1 },
                new DropEntry { ItemId = "gold", Weight = 1, Min = 1, Max = 1 }
            } };
            var model = new GameModel(() => Now, null, null, tactics, null);
            Assert.That(model.StageItemDropChance(stage.Id, GameModel.EquipmentFragmentItemId),
                Is.EqualTo(.4375f).Within(.00001f));
        }

        [Test]
        public void RetiredFragmentDropsSettleAsGoldAndUpgradeSpendsGoldOnly()
        {
            // 碎片已退役：旧数据若仍配了碎片条目，结算时按折算价转金币，强化改为纯金币消耗。
            var tactics = GameModelTests.BuildTactics();
            StageDefinition stage = tactics.Stages[0];
            stage.Drops = new DropTable { Rolls = 1, Entries = new List<DropEntry>
            { new DropEntry { ItemId = GameModel.EquipmentFragmentItemId, Weight = 1, Min = 3, Max = 3 } } };
            var model = new GameModel(() => Now, null, null, tactics, null);
            int goldBefore = model.Save.Gold;
            BattleSimulator battle = model.StartStageBattle(stage.Id, 44, out string error);
            Assert.That(battle, Is.Not.Null, error);
            Assert.That(battle.AutoPlay(), Is.EqualTo(BattleOutcome.Victory));
            model.SettleStageBattle(battle, out string message);
            Assert.That(message, Does.Not.Contain("碎片"), "结算文案不应再提碎片。");
            int rewardCount = model.LastBattleRewards.Count;
            model.SettleStageBattle(battle, out _);
            Assert.That(model.LastBattleRewards.Count, Is.EqualTo(rewardCount), "重复结算不能增加图标或重新发奖。");
            var loaded = new GameModel(() => Now, null, null, tactics, null);
            Assert.That(loaded.Save.EquipmentFragments, Is.Zero, "碎片字段必须保持清零。");
            Assert.That(loaded.Save.Gold, Is.GreaterThan(goldBefore), "碎片应折算为金币入账。");
            loaded.Save.Gold = 100000;
            Assert.That(loaded.UpgradeAccessory(0, out _), Is.True);
            Assert.That(loaded.AccessoryUpgradeLevel(0), Is.EqualTo(1));
            Assert.That(loaded.Save.EquipmentFragments, Is.Zero);
        }

        [Test]
        public void AlreadyClaimedOldFirstClearIsNotReplacedOrPaidAgainAfterPoolRevision()
        {
            var save = new GameSave { OwnedAccessories = new List<int> { 0, 1, 2, 3 }, EquipmentFragments = 3,
                ClearedStages = new List<StageClear> { new StageClear { Id = "stage-1-2", Stars = 2 } },
                EquipmentFirstClearClaims = new List<string> { "stage-1-2" } };
            PlayerPrefs.SetString(GameModel.SaveKey, JsonUtility.ToJson(save));
            for (int reload = 0; reload < 3; reload++)
            {
                var model = new GameModel(() => Now);
                Assert.That(model.OwnsAccessory(3), Is.True, "已经领取的旧装备保留。");
                Assert.That(model.OwnsAccessory(6), Is.False, "改表不得让旧首通再次领取新装备。");
                // 旧档残留碎片一次性折算为金币后清零，不能凭空消失也不能反复退款。
                Assert.That(model.Save.EquipmentFragments, Is.Zero);
                Assert.That(model.StarsOf("stage-1-2"), Is.EqualTo(2));
            }
        }

        [Test]
        public void AdvancedEquipmentHasStableUniqueIdsAndRealPersonalStats()
        {
            var model = new GameModel(() => Now);
            Assert.That(GameModel.AccessoryItemIds.Length, Is.EqualTo(GameModel.AccessoryNames.Length));
            Assert.That(GameModel.AccessoryItemIds.Distinct().Count(), Is.EqualTo(GameModel.AccessoryNames.Length));
            for (int i = 6; i < GameModel.AccessoryNames.Length; i++)
            {
                model.Grant(new CurrencyAmount(GameModel.AccessoryItemIds[i], 1));
                Assert.That(model.OwnsAccessory(i), Is.True);
                Assert.That(model.StatsOf(0, i).Power, Is.GreaterThan(model.StatsOf(0, -1).Power));
                Assert.That(RewardItemVisuals.SpriteFor(GameModel.AccessoryItemIds[i]), Is.Not.Null);
            }
            Assert.That(new GameModel(() => Now).Save.OwnedAccessories.Count, Is.EqualTo(GameModel.AccessoryNames.Length - 3));
        }

        [Test]
        public void FiftyFourCollectionNamesIdsPathsAndCategoriesStayInMatchingVisualOrder()
        {
            string[] groups = { "ear", "neck", "wrist", "ring", "hair", "charm" };
            string[] categories = { "耳饰", "项链", "手环", "戒指", "发饰", "挂饰" };
            string[] firstNames = { "紫月水滴耳坠", "紫晶心项链", "银月开口镯", "紫月戒", "紫星冠", "紫晶麦挂饰" };
            string[] lastNames = { "幻彩彗星耳坠", "黑星蚀领", "黑曜雷链", "青彗轨戒", "粉晶狐耳发饰", "粉流星香水挂饰" };
            Assert.That(GameModel.AccessoryNames.Length, Is.EqualTo(66));
            for (int group = 0; group < groups.Length; group++)
            {
                var statProfiles = new HashSet<string>();
                Assert.That(GameModel.AccessoryNames[12 + group * 9], Is.EqualTo(firstNames[group]));
                Assert.That(GameModel.AccessoryNames[20 + group * 9], Is.EqualTo(lastNames[group]));
                for (int variant = 0; variant < 9; variant++)
                {
                    int index = 12 + group * 9 + variant;
                    Assert.That(GameModel.AccessoryCategory(index), Is.EqualTo(categories[group]));
                    Assert.That(GameModel.AccessoryItemIds[index], Is.EqualTo($"accessory-collection-{groups[group]}-{variant + 1:00}"));
                    Assert.That(GameModel.AccessoryCollectionResourcePath(index),
                        Is.EqualTo($"Art/AccessoryAI/Collection54/accessory-{groups[group]}-{variant + 1:00}-v1"));
                    CombatStatBonuses stats = GameModel.AccessoryBonuses(index);
                    Assert.That(stats.Hp, Is.InRange(0, 170));
                    Assert.That(stats.Attack, Is.InRange(0, 170));
                    Assert.That(stats.Defense, Is.InRange(0, 170));
                    Assert.That(statProfiles.Add($"{stats.Hp}/{stats.Attack}/{stats.Defense}"), Is.True,
                        "同一分类的九种饰品不能只有名字不同、属性完全重复。");
                    Assert.That(GameModel.AccessorySource(index), Does.Not.Contain("暂无来源"));
                }
            }
        }

        [Test]
        public void RetiredFragmentGrantsSaturateGoldInsteadOfOverflowing()
        {
            // 碎片折算入金币时必须饱和到 int.MaxValue，不能溢出成负数把玩家资产清空。
            var model = new GameModel(() => Now);
            model.Grant(new CurrencyAmount(GameModel.EquipmentFragmentItemId, int.MaxValue));
            model.Grant(new CurrencyAmount(GameModel.EquipmentFragmentItemId, 100));
            var loaded = new GameModel(() => Now);
            Assert.That(loaded.Save.EquipmentFragments, Is.Zero);
            Assert.That(loaded.Save.Gold, Is.EqualTo(int.MaxValue));
        }
    }
}
