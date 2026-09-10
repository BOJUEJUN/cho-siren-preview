using System;
using ChoSiren.Systems.Economy;

namespace ChoSiren
{
    /// <summary>A read-only display quote. Purchasing always checks the live balance again.</summary>
    public readonly struct EconomyExchangeQuote
    {
        public EconomyExchangeQuote(string receiveCurrency, int receiveAmount, int diamondCost,
            bool canPurchase, string unavailableReason)
        {
            ReceiveCurrency = receiveCurrency;
            ReceiveAmount = receiveAmount;
            DiamondCost = diamondCost;
            CanPurchase = canPurchase;
            UnavailableReason = unavailableReason ?? string.Empty;
        }

        public string ReceiveCurrency { get; }
        public int ReceiveAmount { get; }
        public int DiamondCost { get; }
        public bool CanPurchase { get; }
        public string UnavailableReason { get; }
        public string ConfirmationText => ReceiveAmount > 0
            ? $"消耗 {DiamondCost:N0} 星钻，获得 {ReceiveAmount:N0} {GameModel.CurrencyName(ReceiveCurrency)}？"
            : UnavailableReason;
    }

    public sealed partial class GameModel
    {
        public const int GoldExchangeDiamondCost = 100;
        public const int GoldExchangeAmount = 2000;
        public const int StaminaRefillMaximum = 60;
        public const int StaminaRefillDiamondCostPerPoint = 1;
        public const bool RealMoneyPurchasesEnabled = false;
        public const string RealMoneyPurchaseNotice = "当前为试玩版，尚未接入真实支付，不会扣款或模拟充值到账。星钻可通过游戏奖励获得。";

        public EconomyExchangeQuote PreviewGoldExchange()
        {
            string reason = Save.Gold > int.MaxValue - GoldExchangeAmount
                ? "星光币已接近持有上限，暂不能兑换"
                : Save.Diamonds < GoldExchangeDiamondCost ? "星钻不足，可先领取游戏奖励" : string.Empty;
            return new EconomyExchangeQuote(CurrencyIds.Gold, GoldExchangeAmount, GoldExchangeDiamondCost,
                reason.Length == 0, reason);
        }

        /// <summary>Includes natural recovery without mutating the save just to draw a modal.</summary>
        public EconomyExchangeQuote PreviewStaminaRefill()
        {
            var recovered = StaminaRegen.Apply(Save.Stamina, Save.StaminaRegenAnchorUnix, NowUnix, economy);
            return StaminaRefillQuoteFor(recovered);
        }

        private EconomyExchangeQuote StaminaRefillQuoteFor(StaminaSnapshot recovered)
        {
            int amount = Math.Min(StaminaRefillMaximum, Math.Max(0, StaminaCap - recovered.Stamina));
            int cost = amount * StaminaRefillDiamondCostPerPoint;
            string reason = amount == 0 ? "体力已满，无需补充"
                : Save.Diamonds < cost ? "星钻不足，可等待体力自然恢复" : string.Empty;
            return new EconomyExchangeQuote(CurrencyIds.Stamina, amount, cost, reason.Length == 0, reason);
        }

        public bool TryExchangeDiamondsForGold(out string message)
        {
            EconomyExchangeQuote quote = PreviewGoldExchange();
            if (!quote.CanPurchase)
            {
                message = quote.UnavailableReason;
                return false;
            }

            // One save and one change notification: observers never see a half-paid exchange.
            Save.Diamonds -= quote.DiamondCost;
            Save.Gold += quote.ReceiveAmount;
            SaveState();
            message = $"兑换成功：星光币 +{quote.ReceiveAmount:N0}，星钻 -{quote.DiamondCost:N0}";
            return true;
        }

        /// <summary>Require the displayed quote so stale confirmations cannot charge a different amount.</summary>
        public bool TryRefillStamina(int quotedAmount, int quotedDiamondCost, out string message)
        {
            long now = NowUnix;
            var recovered = StaminaRegen.Apply(Save.Stamina, Save.StaminaRegenAnchorUnix, now, economy);
            EconomyExchangeQuote quote = StaminaRefillQuoteFor(recovered);
            if (!quote.CanPurchase)
            {
                message = quote.UnavailableReason;
                return false;
            }
            if (quotedAmount <= 0 || quotedDiamondCost <= 0 ||
                quotedAmount != quote.ReceiveAmount || quotedDiamondCost != quote.DiamondCost)
            {
                message = "体力或报价已变化，请重新确认补充数量";
                return false;
            }

            Save.Diamonds -= quote.DiamondCost;
            Save.Stamina = recovered.Stamina + quote.ReceiveAmount;
            Save.StaminaRegenAnchorUnix = Save.Stamina >= StaminaCap ? now : recovered.LastRegenUnixSeconds;
            SaveState();
            message = $"补充成功：体力 +{quote.ReceiveAmount}，星钻 -{quote.DiamondCost}（{Save.Stamina}/{StaminaCap}）";
            return true;
        }

        public string CurrencyHelpDescription(string currencyId)
        {
            switch (currencyId)
            {
                case CurrencyIds.Diamond:
                    return "星钻 · 稀有资源\n用途：面试签约、选秀招募、兑换星光币、补充体力。\n获取：关卡首通、任务、签到和舞台收益。\n" + RealMoneyPurchaseNotice;
                case CurrencyIds.Gold:
                    return $"星光币 · 养成资源\n用途：训练成员、强化饰品、风险沟通休整。\n获取：关卡通关、任务和舞台收益。\n也可用 {GoldExchangeDiamondCost} 星钻兑换 {GoldExchangeAmount:N0} 星光币，确认后才会扣除。";
                case CurrencyIds.Stamina:
                    return $"体力 · 挑战消耗\n用途：进入关卡时消耗；并不是星光币。\n自然恢复：每 {economy.StaminaRegenSeconds / 60f:0.#} 分钟恢复 {economy.StaminaPerTick} 点，上限 {StaminaCap}。\n补充：每点 {StaminaRefillDiamondCostPerPoint} 星钻，每次最多 {StaminaRefillMaximum} 点；不足上限时只按实际补充数量收费。";
                default:
                    return "请选择星钻、星光币或体力查看用途与获取方式。";
            }
        }
    }
}
