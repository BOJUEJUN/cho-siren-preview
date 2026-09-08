using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace ChoSiren.Tests
{
    /// <summary>
    /// Regression cover for v0.3.4 深入交流 (赵金反馈 #12): 羁绊档解锁、每日一次、
    /// 星光币消耗、好感度收益、旧存档兼容与文案池完整性。
    /// </summary>
    public sealed class DeepTalkV034Tests
    {
        private DateTime now;

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey(GameModel.SaveKey);
            PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
            now = new DateTime(2026, 9, 8, 12, 0, 0, DateTimeKind.Local);
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(GameModel.SaveKey);
            PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
        }

        private GameModel NewModel() => new GameModel(() => now);

        // ------------------------------------------------------------------ unlock gate

        [Test]
        public void DeepTalkIsLockedBelowBondTier()
        {
            var model = NewModel();
            Assert.That(model.AffectionOf(0), Is.LessThan(GameModel.DeepTalkAffectionUnlock),
                "新号好感度应低于深入交流门槛");
            Assert.That(model.DeepTalkUnlocked(0), Is.False);
            Assert.That(model.CanDeepTalk(0, out string reason), Is.False);
            Assert.That(reason, Does.Contain("羁绊"), "拒绝原因应说明需要羁绊档");
        }

        [Test]
        public void DeepTalkUnlocksExactlyAtBondThreshold()
        {
            var model = NewModel();
            model.GainAffection(0, GameModel.DeepTalkAffectionUnlock - model.AffectionOf(0));
            Assert.That(model.DeepTalkUnlocked(0), Is.True, "80 好感度即羁绊档，应立即解锁");
            Assert.That(model.CanDeepTalk(0, out _), Is.True);
        }

        // ------------------------------------------------------------------ costs and rewards

        [Test]
        public void DeepTalkSpendsStarCoinsAndRaisesAffection()
        {
            var model = NewModel();
            int affectionBefore = model.AffectionOf(0); // 初始签约成员自带 5 点
            model.GainAffection(0, 80);
            int goldBefore = model.Save.Gold;
            int diamondsBefore = model.Save.Diamonds;

            Assert.That(model.DeepTalk(0, out string message, out string dialogue), Is.True, message);

            Assert.That(model.Save.Gold, Is.EqualTo(goldBefore - GameModel.DeepTalkGoldCost),
                "深入交流只扣星光币");
            Assert.That(model.Save.Diamonds, Is.EqualTo(diamondsBefore), "深入交流不扣星钻");
            Assert.That(model.AffectionOf(0), Is.EqualTo(affectionBefore + 80 + GameModel.DeepTalkAffectionGain));
            Assert.That(dialogue, Is.Not.Empty, "必须返回文案，UI 不得自造");
        }

        [Test]
        public void DeepTalkAffectionIsCappedAtMax()
        {
            var model = NewModel();
            model.GainAffection(0, GameModel.MaxAffection - 1); // 99
            Assert.That(model.DeepTalk(0, out _, out _), Is.True);
            Assert.That(model.AffectionOf(0), Is.EqualTo(GameModel.MaxAffection), "好感度不可超过上限");
        }

        [Test]
        public void DeepTalkFailsWithoutEnoughStarCoins()
        {
            var model = NewModel();
            model.GainAffection(0, 90);
            model.Save.Gold = GameModel.DeepTalkGoldCost - 1;
            Assert.That(model.DeepTalk(0, out string message, out _), Is.False);
            Assert.That(message, Does.Contain("星光币不足"));
        }

        // ------------------------------------------------------------------ daily limit

        [Test]
        public void DeepTalkIsOncePerMemberPerDay()
        {
            var model = NewModel();
            model.GainAffection(0, 85);
            model.GainAffection(1, 85);
            Assert.That(model.DeepTalk(0, out _, out _), Is.True);

            Assert.That(model.DeepTalkUsedToday(0), Is.True);
            Assert.That(model.DeepTalk(0, out string message, out _), Is.False, "同一成员当日第二次应拒绝");
            Assert.That(message, Does.Contain("今日"));

            Assert.That(model.DeepTalk(1, out _, out _), Is.True, "每日次数按成员独立，其他成员不受影响");
        }

        [Test]
        public void DeepTalkResetsOnTheNextDay()
        {
            var model = NewModel();
            model.GainAffection(0, 85);
            Assert.That(model.DeepTalk(0, out _, out _), Is.True);
            Assert.That(model.DeepTalk(0, out _, out _), Is.False);

            now = now.AddDays(1);
            var nextDay = NewModel();
            Assert.That(nextDay.DeepTalkUsedToday(0), Is.False, "跨日后每日次数应重置");
            Assert.That(nextDay.DeepTalk(0, out string message, out _), Is.True, message);
        }

        [Test]
        public void DeepTalkDailyStatePersistsAcrossReload()
        {
            var model = NewModel();
            int affectionBefore = model.AffectionOf(0);
            model.GainAffection(0, 80);
            Assert.That(model.DeepTalk(0, out _, out _), Is.True);

            var loaded = NewModel();
            Assert.That(loaded.AffectionOf(0), Is.EqualTo(affectionBefore + 80 + GameModel.DeepTalkAffectionGain),
                "好感度收益必须持久化");
            Assert.That(loaded.DeepTalkUsedToday(0), Is.True, "当日已交流状态必须持久化");
            Assert.That(loaded.DeepTalk(0, out _, out _), Is.False, "重载后当日仍不可重复交流");
        }

        // ------------------------------------------------------------------ save compatibility

        [Test]
        public void OldSaveWithoutDeepTalkFieldStaysPlayable()
        {
            var existing = new GameSave { Diamonds = 500, Gold = 2000 };
            // 模拟 v0.3.3 及更早的真实存档：序列化后剔除新字段，旧档 JSON 中不存在它。
            string json = JsonUtility.ToJson(existing)
                .Replace("\"MemberDeepTalkDate\":[],", string.Empty);
            Assert.That(json, Does.Not.Contain("MemberDeepTalkDate"), "前置：旧档序列化不含新字段");
            PlayerPrefs.SetString(GameModel.SaveKey, json);
            PlayerPrefs.Save();

            var loaded = NewModel();
            Assert.That(loaded.DeepTalkUsedToday(0), Is.False, "旧档应视为从未交流");

            loaded.GainAffection(0, 80);
            Assert.That(loaded.DeepTalk(0, out string message, out _), Is.True, message,
                "旧档加载后应可直接使用深入交流");
        }

        [Test]
        public void SigningStillGrantsAffectionAfterDeepTalkFieldAdded()
        {
            var model = NewModel();
            int candidate = model.InterviewCandidates(0).First();
            int quote = model.InterviewQuote(0, candidate);
            Assert.That(model.SignInterviewCandidate(0, candidate, model.CurrentInterviewCycle, quote,
                out string message), Is.True, message);
            Assert.That(model.AffectionOf(candidate), Is.EqualTo(GameModel.StartingAffection),
                "签约送好感的既有行为不能被新字段破坏");
        }

        // ------------------------------------------------------------------ dialogue catalog

        [Test]
        public void EveryDialogueTemplateInterpolatesTheMemberName()
        {
            var templates = DeepTalkCatalog.Templates;
            Assert.That(templates.Count, Is.GreaterThanOrEqualTo(8), "文案池至少 8 条");
            foreach (string template in templates)
            {
                Assert.That(template, Does.Contain("{name}"), "模板必须包含姓名占位符");
                Assert.That(template.Length, Is.GreaterThan(10), "文案不能是空串");
            }
            foreach (MemberDefinition member in GameModel.Members)
            {
                string rendered = DeepTalkCatalog.RenderLine("{name}在等你。", member);
                Assert.That(rendered, Does.Contain(member.Name), "插值必须替换为真实成员名");
            }
        }
    }
}
