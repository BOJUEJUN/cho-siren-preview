using System.Collections.Generic;

namespace ChoSiren
{
    public sealed partial class GameSave
    {
        /// <summary>2026-11-04: top-up tiers already bought once (their first-purchase double is spent).</summary>
        public List<string> FirstChargeTiers1104 = new List<string>();
    }

    /// <summary>
    /// 2026-11-04 (用户: "我打算用这个钻石当作用钱充的"): 星钻 is the top-up currency. Free rewards pay 签约券 /
    /// 星光币 instead (only one-time first clears, chapter stars and achievements still give a few 星钻), and the shop
    /// lists the packs and the monthly card. No payment channel is connected yet (it needs a licence, a merchant
    /// account and real-name / minor spending limits), so buying shows a notice and never grants anything.
    /// 1 元 = 10 星钻; one pull is 150 星钻.
    /// </summary>
    public sealed partial class GameModel
    {
        public sealed class TopUpPack1104
        {
            public string Id;
            public int PriceYuan;
            public int Diamonds;
        }

        public static readonly TopUpPack1104[] TopUpPacks1104 =
        {
            new TopUpPack1104 { Id = "diamond-60", PriceYuan = 6, Diamonds = 60 },
            new TopUpPack1104 { Id = "diamond-300", PriceYuan = 30, Diamonds = 300 },
            new TopUpPack1104 { Id = "diamond-680", PriceYuan = 68, Diamonds = 680 },
            new TopUpPack1104 { Id = "diamond-1280", PriceYuan = 128, Diamonds = 1280 },
            new TopUpPack1104 { Id = "diamond-3280", PriceYuan = 328, Diamonds = 3280 },
            new TopUpPack1104 { Id = "diamond-6480", PriceYuan = 648, Diamonds = 6480 },
        };

        public const int MonthlyCardPriceYuan1104 = 30;
        public const int MonthlyCardInstantDiamonds1104 = 300;
        public const int MonthlyCardDailyDiamonds1104 = 100;
        public const int MonthlyCardDays1104 = 30;
        /// <summary>False until a real payment channel is connected; purchases never grant anything while false.</summary>
        public const bool PaymentOpen1104 = false;
        public const string PaymentPendingNotice1104 = "充值通道接入中，开放后即可购买。现在不会扣款，也不会到账。";

        /// <summary>The first purchase of each tier doubles its 星钻.</summary>
        public bool FirstChargeBonusAvailable1104(string packId) =>
            Save.FirstChargeTiers1104 == null || !Save.FirstChargeTiers1104.Contains(packId);

        /// <summary>Always refuses while <see cref="PaymentOpen1104"/> is false.</summary>
        public bool TryTopUp1104(string packId, out string message)
        {
            message = PaymentPendingNotice1104;
            return false;
        }
    }
}
