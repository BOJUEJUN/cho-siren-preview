using System.Collections.Generic;
using System.Linq;
using ChoSiren.Systems.Economy;

namespace ChoSiren
{
    /// <summary>One system mail: some carry a one-time gift, the rest are notices.</summary>
    public sealed class MailView
    {
        public string Id;
        public string Title;
        public string Body;
        public IReadOnlyList<(string ItemId, int Amount)> Gift;
        public bool Claimed;
        public bool HasGift => Gift != null && Gift.Count > 0;
        public bool Claimable => HasGift && !Claimed;
    }

    public sealed partial class GameModel
    {
        // Claimed gifts are recorded as story flags, so the save structure is unchanged.
        private const string MailFlagPrefix = "mail-claimed:";

        public List<MailView> Mails()
        {
            var mails = new List<MailView>
            {
                new MailView
                {
                    Id = "welcome-2026",
                    Title = "欢迎加入 CHO-SIREN",
                    Body = "{player}，你好！女团的舞台从今天开始。这是公司准备的出道支援，请收下。",
                    Gift = new List<(string, int)> { (CurrencyIds.Diamond, 150), (CurrencyIds.Gold, 3000) },
                },
                new MailView
                {
                    Id = "chapter-two-open",
                    Title = "第二章「魅声海湾」开放",
                    Body = "打通第一章最后一关「魅声王座」后，在「开始演出」顶部切换到第二章，海湾巨蛋在等着你们。",
                    Gift = new List<(string, int)> { (CurrencyIds.Stamina, 60) },
                },
                new MailView
                {
                    Id = "idle-stage",
                    Title = "闪耀舞台开张",
                    Body = "女团不在线时也会在闪耀舞台演出，持续累积星光币和星钻。大厅右侧「闪耀舞台」随时领取。",
                },
            };
            foreach (MailView mail in mails) mail.Claimed = HasStoryFlag(MailFlagPrefix + mail.Id);
            return mails;
        }

        public int ClaimableMailCount => Mails().Count(mail => mail.Claimable);

        // ------------------------------------------------------------------ daily sign-in
        // The lobby's seven-day calendar is a view of the existing check-in (Save.CheckInDay /
        // LastCheckInDate), so the task board's check-in and the weekly task stay in step.
        public const int SignInCycle = 7;
        private static readonly (string ItemId, int Amount)[] SignInRewards =
        {
            (CurrencyIds.Diamond, 20), (CurrencyIds.Gold, 2000), (CurrencyIds.Stamina, 30),
            (CurrencyIds.Diamond, 20), (CurrencyIds.Gold, 4000), (CurrencyIds.Stamina, 60),
            (CurrencyIds.Diamond, 60),
        };

        public bool SignedToday => HasCheckedInToday;
        public int SignedDays => string.IsNullOrEmpty(Save.LastCheckInDate) ? 0 : Save.CheckInDay;
        /// <summary>The cycle day (0-6) that today's sign-in gives, or gave if already signed.</summary>
        public int SignInDay
        {
            get
            {
                if (SignedToday) return (Save.CheckInDay - 1) % SignInCycle;
                bool yesterday = System.DateTime.TryParseExact(Save.LastCheckInDate, "yyyy-MM-dd",
                    System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None,
                    out System.DateTime previous) && previous.Date == Today.AddDays(-1);
                return yesterday ? Save.CheckInDay % SignInCycle : 0;
            }
        }
        public static (string ItemId, int Amount) SignInReward(int day) => SignInRewards[((day % SignInCycle) + SignInCycle) % SignInCycle];

        public bool SignIn(out string message) => CheckIn(out message);

        /// <summary>True the first time it is asked for <paramref name="flag"/>; remembers it in the save.</summary>
        public bool FirstTime(string flag)
        {
            if (HasStoryFlag(flag)) return false;
            Save.StoryFlags.Add(flag);
            SaveState();
            return true;
        }

        public bool ClaimMail(string mailId, out string message)
        {
            MailView mail = Mails().FirstOrDefault(item => item.Id == mailId);
            if (mail == null || !mail.HasGift)
            {
                message = "这封邮件没有附件";
                return false;
            }
            if (mail.Claimed)
            {
                message = "附件已经领取过了";
                return false;
            }
            foreach ((string itemId, int amount) in mail.Gift) GrantItemInternal(itemId, amount);
            Save.StoryFlags.Add(MailFlagPrefix + mail.Id);
            SaveState();
            message = $"已领取「{mail.Title}」附件：" + FormatRewards(mail.Gift.ToList());
            return true;
        }
    }
}
