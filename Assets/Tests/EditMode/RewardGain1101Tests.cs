using System;
using System.Collections.Generic;
using System.Linq;
using ChoSiren.Systems.Economy;
using ChoSiren.Systems.Tactics;
using ChoSiren.UI;
using NUnit.Framework;
using UnityEngine;

namespace ChoSiren.Tests
{
    /// <summary>
    /// 2026-11-01 reward-ui: every way the player receives currencies/items publishes one batch through
    /// <see cref="GameModel.RewardsGranted1101"/>, which is the only thing the shared 获得奖励 presenter
    /// (<see cref="RewardGain1101"/>) listens to — so no entry can fall back to its own toast or banner.
    /// </summary>
    public sealed class RewardGain1101Tests
    {
        private DateTime now;

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey(GameModel.SaveKey);
            PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
            now = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Local);
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(GameModel.SaveKey);
            PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
        }

        private GameModel Model(GameSave save = null)
        {
            if (save != null)
            {
                save.StaminaRegenAnchorUnix = new DateTimeOffset(now).ToUnixTimeSeconds();
                PlayerPrefs.SetString(GameModel.SaveKey, JsonUtility.ToJson(save));
            }
            return new GameModel(() => now);
        }

        private static List<RewardGrant1101> Record(GameModel model, Action action)
        {
            var grants = new List<RewardGrant1101>();
            Action<RewardGrant1101> listener = grants.Add;
            model.RewardsGranted1101 += listener;
            try { action(); }
            finally { model.RewardsGranted1101 -= listener; }
            return grants;
        }

        private static RewardGrant1101 Single(List<RewardGrant1101> grants, string what)
        {
            Assert.That(grants.Count, Is.EqualTo(1), what + " 应发布且只发布一批奖励");
            Assert.That(grants[0].Items, Is.Not.Empty, what);
            return grants[0];
        }

        private static List<(string, int)> Tuples(IEnumerable<CurrencyAmount> rewards) =>
            rewards.Select(r => (r.Currency, r.Amount)).ToList();

        [Test]
        public void DailyCheckInPublishesItsDayRewardAsACheckInBatch()
        {
            GameModel model = Model();
            var grant = Single(Record(model, () => Assert.That(model.CheckIn(out _), Is.True)), "签到");
            Assert.That(grant.Source, Is.EqualTo(RewardGrant1101.SourceCheckIn));
            Assert.That(grant.Items, Is.EqualTo(new List<(string, int)> { GameModel.SignInReward(0) }));
            Assert.That(RewardGain1101.Classify(grant.Items, grant.Source, 1), Is.EqualTo(RewardGain1101.Mode.Full),
                "签到是里程碑，走完整的获得奖励弹窗");
        }

        [Test]
        public void TaskClaimIsARoutineBatchAndClaimingSeveralBecomesThePopup()
        {
            GameModel model = Model();
            CurrencyAmount reward = model.TaskViews().First(view => view.Definition.Id == "daily-login").Definition.Reward;
            var grant = Single(Record(model, () => Assert.That(model.TryClaimTask("daily-login", out _), Is.True)), "任务领取");
            Assert.That(grant.Source, Is.EqualTo(RewardGrant1101.SourceTask));
            Assert.That(grant.Items, Is.EqualTo(new List<(string, int)> { (reward.Currency, reward.Amount) }));
            Assert.That(RewardGain1101.Classify(grant.Items, grant.Source, 1), Is.EqualTo(RewardGain1101.Mode.Lite));
            Assert.That(RewardGain1101.Classify(grant.Items, grant.Source, 3), Is.EqualTo(RewardGain1101.Mode.Full),
                "一键领取多项合成一次弹窗");
        }

        [Test]
        public void MailGiftIncludingTheNewcomerGiftIsPublishedWhole()
        {
            GameModel model = Model();
            MailView welcome = model.Mails().First(mail => mail.Id == "welcome-2026");
            var grant = Single(Record(model, () => Assert.That(model.ClaimMail("welcome-2026", out _), Is.True)), "邮件领取");
            Assert.That(grant.Items, Is.EqualTo(welcome.Gift.ToList()));
            Assert.That(RewardGain1101.Classify(grant.Items, grant.Source, 1), Is.EqualTo(RewardGain1101.Mode.Full));
        }

        [Test]
        public void AchievementClaimIsPublished()
        {
            GameModel model = Model(new GameSave { ClearedStages = new List<StageClear> { new StageClear { Id = "stage-1-1", Stars = 1 } } });
            var grant = Single(Record(model, () => Assert.That(model.ClaimAchievement("first-live", out string m), Is.True)), "成就");
            Assert.That(grant.Items, Is.EqualTo(new List<(string, int)> { (CurrencyIds.Diamond, 50) }));
        }

        [Test]
        public void ChapterStarRewardsAndChapterTasksOfBothChaptersArePublished()
        {
            var clears = Enumerable.Range(1, 10).Select(i => new StageClear { Id = $"stage-1-{i}", Stars = 3 })
                .Concat(Enumerable.Range(1, 10).Select(i => new StageClear { Id = $"stage-2-{i}", Stars = 3 })).ToList();
            GameModel model = Model(new GameSave { StoryProgress = GameModel.MaxStoryProgress, ClearedStages = clears });
            foreach (int chapter in new[] { 1, 2 })
            {
                ChapterStarRewardView star = model.ChapterStarRewardViews(chapter).First(view => view.Claimable);
                var grant = Single(Record(model, () =>
                    Assert.That(model.TryClaimChapterStarReward(chapter, star.RequiredStars, out string m), Is.True, m)), $"第{chapter}章星级奖励");
                Assert.That(grant.Items, Is.EqualTo(Tuples(star.Rewards)));

                ChapterTaskView task = model.ChapterTaskViews(chapter).First(view => view.Claimable);
                grant = Single(Record(model, () =>
                    Assert.That(model.TryClaimChapterTask(chapter, task.Id, out string m), Is.True, m)), $"第{chapter}章任务");
                Assert.That(grant.Items, Is.EqualTo(Tuples(task.Rewards)));
            }
        }

        [Test]
        public void IdleIncomePerformanceExchangeAndRefillAreRoutineBatches()
        {
            GameModel model = Model(new GameSave { Diamonds = 500, Gold = 100, Stamina = 60 });
            now = now.AddHours(2);
            var idle = Single(Record(model, () => Assert.That(model.ClaimIdleIncome(out _), Is.True)), "舞台收益");
            Assert.That(idle.Source, Is.EqualTo(RewardGrant1101.SourceIdle));
            Assert.That(idle.Items.All(item => RewardCard1101.IsCurrency(item.ItemId)), Is.True);

            var perform = Single(Record(model, () => Assert.That(model.Perform(out _), Is.True)), "演出");
            Assert.That(perform.Source, Is.EqualTo(RewardGrant1101.SourcePerform));
            Assert.That(perform.Items[0].ItemId, Is.EqualTo(CurrencyIds.Gold));

            int gold = model.Save.Gold;
            var exchange = Single(Record(model, () => Assert.That(model.TryExchangeDiamondsForGold(out _), Is.True)), "兑换");
            Assert.That(exchange.Source, Is.EqualTo(RewardGrant1101.SourceExchange));
            Assert.That(exchange.Items, Is.EqualTo(new List<(string, int)> { (CurrencyIds.Gold, model.Save.Gold - gold) }));

            EconomyExchangeQuote quote = model.PreviewStaminaRefill();
            var refill = Single(Record(model, () =>
                Assert.That(model.TryRefillStamina(quote.ReceiveAmount, quote.DiamondCost, out string m), Is.True, m)), "体力补充");
            Assert.That(refill.Items, Is.EqualTo(new List<(string, int)> { (CurrencyIds.Stamina, quote.ReceiveAmount) }));
            foreach (var grant in new[] { idle, perform, exchange, refill })
                Assert.That(RewardGain1101.Classify(grant.Items, grant.Source, 1), Is.EqualTo(RewardGain1101.Mode.Lite));
        }

        [Test]
        public void SweepPublishesWhatItPaidAndStageSettlementIsLeftToTheVictoryPage()
        {
            GameModel model = Model(new GameSave { StoryProgress = 100, ClearedStages = new List<StageClear> { new StageClear { Id = "stage-1-1", Stars = 3 } } });
            var sweep = Single(Record(model, () => Assert.That(model.TrySweepStage("stage-1-1", 3, 7UL, out string m), Is.True, m)), "扫荡");
            var paid = model.LastBattleRewards.GroupBy(r => r.ItemId).Select(g => (g.Key, g.Sum(r => r.Amount))).ToList();
            Assert.That(sweep.Items.OrderBy(i => i.ItemId), Is.EqualTo(paid.OrderBy(i => i.Item1)));
            Assert.That(RewardGain1101.Classify(sweep.Items, sweep.Source, 1), Is.EqualTo(RewardGain1101.Mode.Full));

            PlayerPrefs.DeleteKey(GameModel.SaveKey);
            var battleModel = new GameModel(() => now, null, null, GameModelTests.BuildTactics(), null);
            string stageId = battleModel.Tactics.Stages[0].Id;
            BattleSimulator battle = battleModel.StartStageBattle(stageId, 1UL, out string message);
            Assert.That(battle, Is.Not.Null, message);
            Assert.That(battle.AutoPlay(), Is.EqualTo(BattleOutcome.Victory));
            var settled = Record(battleModel, () => battleModel.SettleStageBattle(battle, out _));
            Assert.That(settled.Count, Is.EqualTo(1));
            Assert.That(settled[0].Source, Is.EqualTo(RewardGrant1101.SourceBattle), "关卡结算由胜利页的同款奖励卡展示，全局弹窗跳过");
        }

        [Test]
        public void DuplicateAccessoryIsPublishedAsTheGoldThePlayerActuallyGets()
        {
            GameModel model = Model();
            Assert.That(model.OwnsAccessory(0), Is.True);
            var grant = Single(Record(model, () => model.Grant(new CurrencyAmount(GameModel.AccessoryItemIds[0], 1))), "重复饰品");
            Assert.That(grant.Items, Is.EqualTo(new List<(string, int)> { (CurrencyIds.Gold, GameModel.DuplicateAccessoryGold) }));
        }

        [Test]
        public void NothingGrantedPublishesNothing()
        {
            GameModel model = Model();
            Assert.That(Record(model, () => model.SetBattleSpeed(2)), Is.Empty);
            Assert.That(Record(model, () => model.CheckIn(out _)).Count, Is.EqualTo(1));
            Assert.That(Record(model, () => Assert.That(model.CheckIn(out _), Is.False)), Is.Empty, "重复签到不发布");
            Assert.That(model.PendingRewardCount1101, Is.Zero);
        }

        [TestCase("签到成功：连续第 1 天，星钻 +20", "签到成功 · 连续第 1 天")]
        [TestCase("已领取「欢迎加入 CHO-SIREN」附件：星钻 +150，星光币 +3000", "已领取「欢迎加入 CHO-SIREN」附件")]
        [TestCase("成就「初次登台」达成：星钻 ×50", "成就「初次登台」达成")]
        [TestCase("已领取：第一章首通（星钻 +20，星光币 +500）", "已领取 · 第一章首通")]
        [TestCase("扫荡 ×3 完成，体力 -30：星光币 +120\n新装备：星轨耳返", "扫荡 ×3 完成，体力 -30")]
        [TestCase("已领取：每日登录，星钻 +50", "已领取 · 每日登录")]
        [TestCase("已一键领取 3 项任务奖励", "已一键领取 3 项任务奖励")]
        public void ToastTextBecomesACaptionWithoutTheAmountsTheCardsShow(string message, string caption)
        {
            Assert.That(RewardGain1101.CaptionFrom(message), Is.EqualTo(caption));
        }

        [Test]
        public void HudNumbersRollBetweenTheOldAndNewValue()
        {
            Assert.That(RewardGain1101.RollText("1,200", "1,220", 0f), Is.EqualTo("1,200"));
            Assert.That(RewardGain1101.RollText("1,200", "1,220", .5f), Is.EqualTo("1,210"));
            Assert.That(RewardGain1101.RollText("90/120", "120/120", 1f), Is.EqualTo("120/120"));
            Assert.That(RewardGain1101.RollText("9.9万", "10万", .5f), Is.EqualTo("9.9万"), "无法精确解析的万位不做插值");
        }

        [TestCase(1)] [TestCase(2)] [TestCase(4)] [TestCase(5)] [TestCase(6)] [TestCase(8)]
        public void RewardGridStaysInsideItsBoxWithBalancedRows(int count)
        {
            const float available = 633f, gap = 18f;
            RewardCard1101.GridLayout(count, available, gap, 150f, out int rows, out int perRow, out Vector2 card);
            Assert.That(rows, Is.EqualTo(count <= 4 ? 1 : 2));
            List<Vector2> at = RewardCard1101.GridPositions(count, rows, perRow, card, gap, 14f);
            Assert.That(at.Count, Is.EqualTo(count));
            foreach (Vector2 p in at)
            {
                Assert.That(Mathf.Abs(p.x) + card.x * .5f, Is.LessThanOrEqualTo(available * .5f + .01f), "卡片不能超出可用宽度");
            }
            if (rows == 2)
            {
                int top = at.Count(p => p.y > 0f), bottom = count - top;
                Assert.That(top - bottom, Is.InRange(0, 1), "两行数量平衡");
            }
        }

        [Test]
        public void CurrencyIconsAreTheHudBadges()
        {
            foreach (string id in new[] { CurrencyIds.Gold, CurrencyIds.Diamond, CurrencyIds.Stamina })
            {
                Sprite badge = RewardItemVisuals.HudBadge(id);
                Assert.That(badge, Is.Not.Null, id);
                Assert.That(RewardItemVisuals.SpriteFor(id), Is.SameAs(badge), id);
            }
        }
    }
}
