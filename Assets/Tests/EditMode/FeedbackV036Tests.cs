using System.Linq;
using ChoSiren;
using ChoSiren.UI;
using NUnit.Framework;
using UnityEngine;

namespace ChoSiren.Tests
{
    /// <summary>0.3.6 反馈：饰品等级要看得出来、线上/线下面试要有真实差距、候选要有风险值。</summary>
    public class FeedbackV036Tests
    {
        private static GameModel NewModel()
        {
            PlayerPrefs.DeleteKey("chosiren.save.v1");
            PlayerPrefs.DeleteKey("chosiren.save");
            return new GameModel();
        }

        [Test]
        public void EveryRarityTierGetsItsOwnDistinctColour()
        {
            GameModel.AccessoryRarity[] tiers =
            {
                GameModel.AccessoryRarity.Common, GameModel.AccessoryRarity.Fine,
                GameModel.AccessoryRarity.Rare, GameModel.AccessoryRarity.Epic,
                GameModel.AccessoryRarity.Legendary,
            };
            string[] hexes = tiers.Select(GameModel.AccessoryRarityColorHex).ToArray();
            Assert.That(hexes.Distinct().Count(), Is.EqualTo(tiers.Length),
                "五个品质档必须各有自己的颜色，不能出现同色导致看起来都一样。");
            foreach (string hex in hexes) Assert.That(hex, Does.StartWith("#"));

            Color[] colours = tiers.Select(RewardItemVisuals.RarityColor).Cast<Color>().ToArray();
            for (int a = 0; a < colours.Length; a++)
            for (int b = a + 1; b < colours.Length; b++)
            {
                float gap = Mathf.Abs(colours[a].r - colours[b].r) +
                            Mathf.Abs(colours[a].g - colours[b].g) +
                            Mathf.Abs(colours[a].b - colours[b].b);
                Assert.That(gap, Is.GreaterThan(0.2f),
                    $"{tiers[a]} 与 {tiers[b]} 的颜色差距太小，深色底上分辨不出来。");
            }
        }

        [Test]
        public void AccessoryAccentFollowsRarityNotCategory()
        {
            // 同一品质的不同品类必须同色；不同品质必须不同色——否则玩家分不出等级。
            int legendary = 0, epic = 6;
            Assert.That(GameModel.AccessoryRarityOf(legendary), Is.EqualTo(GameModel.AccessoryRarity.Legendary));
            Assert.That(GameModel.AccessoryRarityOf(epic), Is.EqualTo(GameModel.AccessoryRarity.Epic));
            Color legendaryAccent = RewardItemVisuals.AccentFor(GameModel.AccessoryItemIds[legendary]);
            Color epicAccent = RewardItemVisuals.AccentFor(GameModel.AccessoryItemIds[epic]);
            Assert.That(legendaryAccent, Is.Not.EqualTo(epicAccent),
                "传说与史诗的强调色必须不同。");
            Assert.That(legendaryAccent, Is.EqualTo((Color)RewardItemVisuals.RarityColor(
                GameModel.AccessoryRarity.Legendary)));
        }

        [Test]
        public void OfflineInterviewBuysStrongerNumbersThanOnline()
        {
            Assert.That(GameModel.InterviewChannelStatDelta(1),
                Is.GreaterThan(GameModel.InterviewChannelStatDelta(0)),
                "线下报价更高，四维读数就必须更强，否则贵得没道理。");
        }

        [Test]
        public void OnlineInterviewIsCheaperButRiskierThanOffline()
        {
            GameModel model = NewModel();
            int checkedMembers = 0;
            for (int index = 0; index < GameModel.Members.Length && checkedMembers < 12; index++)
            {
                int online = model.InterviewRisk(0, index);
                int offline = model.InterviewRisk(1, index);
                Assert.That(online, Is.InRange(5, 95));
                Assert.That(offline, Is.InRange(5, 95));
                Assert.That(online, Is.GreaterThan(offline),
                    "线上低价池的风险必须高于线下试镜，这样报价差才有意义。");
                Assert.That(model.InterviewQuote(1, index),
                    Is.GreaterThan(model.InterviewQuote(0, index)));
                checkedMembers++;
            }
            Assert.That(checkedMembers, Is.GreaterThan(0));
        }

        [Test]
        public void RiskIsStableAcrossModelInstances()
        {
            // 风险值不能每次启动都变，所以不能依赖 string.GetHashCode。
            GameModel first = NewModel();
            int[] a = Enumerable.Range(0, 8).Select(index => first.InterviewRisk(0, index)).ToArray();
            GameModel second = NewModel();
            int[] b = Enumerable.Range(0, 8).Select(index => second.InterviewRisk(0, index)).ToArray();
            Assert.That(a, Is.EqualTo(b), "同一成员的风险值必须稳定，不能跨实例漂移。");
        }

        [Test]
        public void RiskLabelBandsFollowTheNumber()
        {
            Assert.That(GameModel.InterviewRiskLabel(10), Is.EqualTo("低"));
            Assert.That(GameModel.InterviewRiskLabel(40), Is.EqualTo("中"));
            Assert.That(GameModel.InterviewRiskLabel(70), Is.EqualTo("高"));
        }

        [Test]
        public void ChannelDominatesRiskBandsSoPricingNeverLooksBackwards()
        {
            // 两个池装的是不同成员，所以成员方差不能盖过渠道差异，否则线上会显得比线下更安全。
            GameModel model = NewModel();
            int count = Mathf.Min(24, GameModel.Members.Length);
            int[] online = Enumerable.Range(0, count).Select(i => model.InterviewRisk(0, i)).ToArray();
            int[] offline = Enumerable.Range(0, count).Select(i => model.InterviewRisk(1, i)).ToArray();
            Assert.That(online.Min(), Is.GreaterThan(offline.Max()),
                "线上池最安全的候选也必须比线下池最危险的候选更险，风险条才不会看起来是反的。");
            Assert.That(online.All(r => GameModel.InterviewRiskLabel(r) != "低"), Is.True,
                "线上低价池不该出现“低风险”标。");
            Assert.That(offline.All(r => GameModel.InterviewRiskLabel(r) != "高"), Is.True,
                "线下高价池不该出现“高风险”标。");
        }
    }
}
