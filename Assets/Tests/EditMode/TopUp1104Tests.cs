using System;
using System.Linq;
using ChoSiren.Systems.Economy;
using NUnit.Framework;
using UnityEngine;

namespace ChoSiren.Tests
{
    /// <summary>2026-11-04: 星钻 is the top-up currency; recurring free rewards pay 签约券 / 星光币; stamina may overflow.</summary>
    public sealed class TopUp1104Tests
    {
        private static readonly DateTime Now = new DateTime(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

        [SetUp, TearDown]
        public void Clean()
        {
            PlayerPrefs.DeleteKey(GameModel.SaveKey);
            PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
        }

        [Test]
        public void TopUpRefusesAndGrantsNothingWhilePaymentIsClosed()
        {
            var model = new GameModel(() => Now, new InMemorySaveStore());
            int diamonds = model.Save.Diamonds;
            Assert.That(GameModel.PaymentOpen1104, Is.False);
            foreach (GameModel.TopUpPack1104 pack in GameModel.TopUpPacks1104)
            {
                Assert.That(model.TryTopUp1104(pack.Id, out string message), Is.False);
                Assert.That(message, Does.Contain("不会扣款"));
                Assert.That(model.FirstChargeBonusAvailable1104(pack.Id), Is.True);
                Assert.That(pack.Diamonds, Is.EqualTo(pack.PriceYuan * 10), "1 元 = 10 星钻");
            }
            Assert.That(model.Save.Diamonds, Is.EqualTo(diamonds));
        }

        [Test]
        public void RecurringRewardsNoLongerPayDiamonds()
        {
            var model = new GameModel(() => Now, new InMemorySaveStore());
            for (int day = 0; day < GameModel.SignInCycle; day++)
                Assert.That(GameModel.SignInReward(day).ItemId, Is.Not.EqualTo(CurrencyIds.Diamond), "day " + day);
            Assert.That(GameModel.SignInReward(6), Is.EqualTo((CurrencyIds.RecruitTicket, 1)));
            foreach (var task in model.TaskViews())
                Assert.That(task.Definition.Reward.Currency, Is.Not.EqualTo(CurrencyIds.Diamond), task.Definition.Id);
            Assert.That(model.TaskViews().Count(t => t.Definition.Reward.Currency == CurrencyIds.RecruitTicket), Is.GreaterThanOrEqualTo(3));
            var welcome = model.Mails().First(m => m.Id == "welcome-2026");
            Assert.That(welcome.Gift, Does.Contain((CurrencyIds.RecruitTicket, 10)));
            Assert.That(welcome.Gift.Any(g => g.Item1 == CurrencyIds.Diamond), Is.False);
        }

        [Test]
        public void StaminaRewardsOverflowTheRegenCapAndSurviveASave()
        {
            var store = new InMemorySaveStore();
            var model = new GameModel(() => Now, store);
            model.Save.Stamina = model.StaminaCap;
            model.Grant(new CurrencyAmount(CurrencyIds.Stamina, 30));
            Assert.That(model.Save.Stamina, Is.EqualTo(model.StaminaCap + 30));
            var reloaded = new GameModel(() => Now.AddMinutes(30), store);
            Assert.That(reloaded.Save.Stamina, Is.EqualTo(reloaded.StaminaCap + 30), "overflow is kept and does not regenerate");
        }
    }
}
