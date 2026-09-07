using System;
using ChoSiren.Systems.Economy;
using NUnit.Framework;
using UnityEngine;

namespace ChoSiren.Tests
{
    public sealed class GameModelEconomyActionsTests
    {
        private DateTime now;

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey(GameModel.SaveKey);
            PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
            now = new DateTime(2026, 9, 7, 12, 0, 0, DateTimeKind.Local);
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(GameModel.SaveKey);
            PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
        }

        private GameModel Model(int diamonds = 500, int gold = 100, int stamina = 120)
        {
            PlayerPrefs.SetString(GameModel.SaveKey, JsonUtility.ToJson(new GameSave
            {
                Diamonds = diamonds,
                Gold = gold,
                Stamina = stamina,
                StaminaRegenAnchorUnix = new DateTimeOffset(now).ToUnixTimeSeconds()
            }));
            return new GameModel(() => now);
        }

        [Test]
        public void GoldExchangePublishesCompleteTransactionAndSurvivesRestart()
        {
            GameModel model = Model();
            int events = 0;
            model.Changed += () =>
            {
                events++;
                Assert.That(model.Save.Diamonds, Is.EqualTo(400));
                Assert.That(model.Save.Gold, Is.EqualTo(2100));
            };
            EconomyExchangeQuote quote = model.PreviewGoldExchange();
            Assert.That(quote.CanPurchase, Is.True);
            Assert.That(quote.ConfirmationText, Does.Contain("100"));
            Assert.That(model.TryExchangeDiamondsForGold(out _), Is.True);
            Assert.That(events, Is.EqualTo(1));
            var loaded = new GameModel(() => now);
            Assert.That(loaded.Save.Diamonds, Is.EqualTo(400));
            Assert.That(loaded.Save.Gold, Is.EqualTo(2100));
        }

        [TestCase(99, 100)]
        [TestCase(500, int.MaxValue)]
        [TestCase(500, int.MaxValue - 1999)]
        public void RejectedGoldExchangeDoesNotChargeOrPublish(int diamonds, int gold)
        {
            GameModel model = Model(diamonds, gold);
            int events = 0;
            model.Changed += () => events++;
            Assert.That(model.TryExchangeDiamondsForGold(out _), Is.False);
            Assert.That(model.Save.Diamonds, Is.EqualTo(diamonds));
            Assert.That(model.Save.Gold, Is.EqualTo(gold));
            Assert.That(events, Is.Zero);
        }

        [TestCase(0, 60, 60)]
        [TestCase(60, 60, 120)]
        [TestCase(112, 8, 120)]
        [TestCase(119, 1, 120)]
        public void StaminaRefillOnlyChargesActualMissingPoints(int stamina, int amount, int expected)
        {
            GameModel model = Model(stamina: stamina);
            EconomyExchangeQuote quote = model.PreviewStaminaRefill();
            Assert.That(quote.ReceiveAmount, Is.EqualTo(amount));
            Assert.That(quote.DiamondCost, Is.EqualTo(amount));
            Assert.That(model.TryRefillStamina(quote.ReceiveAmount, quote.DiamondCost, out _), Is.True);
            Assert.That(model.Save.Diamonds, Is.EqualTo(500 - amount));
            Assert.That(model.Save.Stamina, Is.EqualTo(expected));
            var loaded = new GameModel(() => now);
            Assert.That(loaded.Save.Stamina, Is.EqualTo(expected));
            Assert.That(loaded.Save.Diamonds, Is.EqualTo(500 - amount));
        }

        [Test]
        public void NaturalRecoveryInvalidatesOldRefillQuoteWithoutCharging()
        {
            GameModel model = Model(stamina: 112);
            EconomyExchangeQuote quote = model.PreviewStaminaRefill();
            now = now.AddSeconds(360);
            Assert.That(model.PreviewStaminaRefill().ReceiveAmount, Is.EqualTo(7));
            Assert.That(model.TryRefillStamina(quote.ReceiveAmount, quote.DiamondCost, out string message), Is.False);
            Assert.That(message, Does.Contain("重新确认"));
            Assert.That(model.Save.Diamonds, Is.EqualTo(500));
            quote = model.PreviewStaminaRefill();
            Assert.That(model.TryRefillStamina(quote.ReceiveAmount, quote.DiamondCost, out _), Is.True);
            Assert.That(model.Save.Stamina, Is.EqualTo(120));
            Assert.That(model.Save.Diamonds, Is.EqualTo(493));
        }

        [TestCase(0, 0)]
        [TestCase(-1, -1)]
        [TestCase(8, -8)]
        [TestCase(9999, 1)]
        [TestCase(8, 7)]
        public void InvalidOrTamperedRefillQuoteCannotGrantStamina(int amount, int cost)
        {
            GameModel model = Model(stamina: 112);
            Assert.That(model.TryRefillStamina(amount, cost, out _), Is.False);
            Assert.That(model.Save.Diamonds, Is.EqualTo(500));
            Assert.That(model.Save.Stamina, Is.EqualTo(112));
        }

        [Test]
        public void ReusingConfirmedFillQuoteWhenFullDoesNotChargeTwice()
        {
            GameModel model = Model(stamina: 112);
            Assert.That(model.TryRefillStamina(8, 8, out _), Is.True);
            Assert.That(model.TryRefillStamina(8, 8, out _), Is.False);
            Assert.That(model.PreviewStaminaRefill().CanPurchase, Is.False);
            Assert.That(model.PreviewStaminaRefill().DiamondCost, Is.Zero);
            Assert.That(model.Save.Diamonds, Is.EqualTo(492));
        }

        [Test]
        public void InsufficientDiamondsRejectsRefillWithoutMutation()
        {
            GameModel model = Model(diamonds: 7, stamina: 112);
            Assert.That(model.TryRefillStamina(8, 8, out _), Is.False);
            Assert.That(model.Save.Diamonds, Is.EqualTo(7));
            Assert.That(model.Save.Stamina, Is.EqualTo(112));
        }

        [Test]
        public void PartialRefillPreservesNaturalRecoveryProgress()
        {
            GameModel model = Model(stamina: 0);
            now = now.AddSeconds(180);
            Assert.That(model.TryRefillStamina(60, 60, out _), Is.True);
            Assert.That(model.SecondsUntilNextStamina, Is.EqualTo(180));
            now = now.AddSeconds(180);
            Assert.That(new GameModel(() => now).Save.Stamina, Is.EqualTo(61));
        }

        [Test]
        public void CurrencyHelpDistinguishesUsesAndDoesNotPretendPaymentExists()
        {
            GameModel model = Model();
            Assert.That(model.CurrencyHelpDescription(CurrencyIds.Diamond), Does.Contain("选秀"));
            Assert.That(model.CurrencyHelpDescription(CurrencyIds.Gold), Does.Contain("训练成员"));
            Assert.That(model.CurrencyHelpDescription(CurrencyIds.Stamina), Does.Contain("自然恢复"));
            Assert.That(GameModel.RealMoneyPurchasesEnabled, Is.False);
            Assert.That(GameModel.RealMoneyPurchaseNotice, Does.Contain("尚未接入真实支付"));
        }
    }
}
