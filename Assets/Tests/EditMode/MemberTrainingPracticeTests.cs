using ChoSiren.Panels;
using ChoSiren.Systems.Presentation;
using ChoSiren.Systems.Tactics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.Tests
{
    /// <summary>
    /// Executable checks for the compact practice feedback: the plan is career-specific, the
    /// committed level-up uses the existing gold-only economy, the before/after deltas are
    /// readable, and the panel exposes no diamond cost.
    /// </summary>
    public sealed class MemberTrainingPracticeTests
    {
        private GameObject host;

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey(GameModel.SaveKey);
            PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
            PlayerPrefs.DeleteKey(MemberTrainingPractice.ReduceMotionPreferenceKey);
            host = new GameObject("练习测试根节点", typeof(RectTransform));
        }

        [TearDown]
        public void TearDown()
        {
            if (host != null) Object.DestroyImmediate(host);
            PlayerPrefs.DeleteKey(GameModel.SaveKey);
            PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
            PlayerPrefs.DeleteKey(MemberTrainingPractice.ReduceMotionPreferenceKey);
        }

        [Test]
        public void PracticePlanIsCareerSpecificAndNeverChargesDiamonds()
        {
            Assert.That(MemberTrainingPractice.BuildPlan("主唱", 500, false).ActionName,
                Is.EqualTo(MemberTrainingPractice.VocalPractice));
            Assert.That(MemberTrainingPractice.BuildPlan("主舞", 500, false).ActionName,
                Is.EqualTo(MemberTrainingPractice.DancePractice));
            Assert.That(MemberTrainingPractice.BuildPlan("Rapper", 500, false).ActionName,
                Is.EqualTo(MemberTrainingPractice.RapPractice));
            Assert.That(MemberTrainingPractice.BuildPlan("门面", 500, false).ActionName,
                Is.EqualTo(MemberTrainingPractice.FacePractice));

            MemberPracticePlan plan = MemberTrainingPractice.BuildPlan("主唱", 500, false);
            Assert.That(plan.DiamondCost, Is.Zero);
            Assert.That(plan.DurationSeconds, Is.InRange(1f, 2f));
            Assert.That(MemberTrainingPractice.BuildPlan("主唱", 500, true).DurationSeconds,
                Is.LessThan(plan.DurationSeconds));
        }

        [Test]
        public void OutcomeKeepsReadableBeforeAfterDeltasAndRealDiamondFreeCostLine()
        {
            MemberPracticeOutcome outcome = MemberTrainingPractice.BuildOutcome(68, 69, 9200, 9450,
                1234, 1286, 5678, 5921, 12, 12, 140, 140, 1250);
            Assert.That(outcome.Headline, Does.Contain("等级 68 → 69"));
            Assert.That(outcome.Headline, Does.Contain("+250"));
            Assert.That(outcome.CostLine, Does.Contain("星光币 1,250"));
            Assert.That(outcome.CostLine, Does.Contain("不消耗星钻"));
            Assert.That(outcome.Deltas.Count, Is.EqualTo(6));
            Assert.That(outcome.Deltas[0].Value, Is.EqualTo("68 → 69 (+1)"));
            Assert.That(outcome.Deltas[2].Value, Is.EqualTo("1,234 → 1,286 (+52)"));
            Assert.That(outcome.Deltas[4].Value, Does.Contain("±0"));
            Assert.That(MemberTrainingPractice.FormatDelta(5, 5), Is.EqualTo("±0"));
            Assert.That(MemberTrainingPractice.FormatDelta(5, 4), Is.EqualTo("-1"));
        }

        [Test]
        public void PanelCommitsExactlyOneGoldOnlyLevelAndShowsDeltas()
        {
            var model = new GameModel();
            int levelBefore = model.LevelOf(0);
            int goldBefore = model.Save.Gold;
            int diamondsBefore = model.Save.Diamonds;
            int cost = BattleSimulator.TrainingCostAtLevel(levelBefore);

            MemberPracticePanel panel = MemberPracticePanel.Open(host.transform, model, 0);
            Assert.That(panel.transform.name, Is.EqualTo("MemberPracticePanel"));
            Assert.That(Find<Image>(panel.transform, "PracticePortrait").sprite,
                Is.EqualTo(Resources.Load<Sprite>(GameModel.Members[0].ResourcePath)),
                "练习面板必须复用现有立绘。");
            Assert.That(Find<Text>(panel.transform, "PracticeNoDiamond").text, Does.Contain("不消耗星钻"));
            Assert.That(Find<Text>(panel.transform, "PracticeQuoteGold").text, Does.Contain(cost.ToString("N0")));
            Assert.That(Find<Text>(panel.transform, "PracticeAction").text, Does.Contain("练习"));
            Assert.That(Find<Button>(panel.transform, "StartPractice").interactable, Is.True);
            Assert.That(panel.LastOutcome, Is.Null);

            Assert.That(panel.CommitTraining(), Is.True);
            Assert.That(model.LevelOf(0), Is.EqualTo(levelBefore + 1));
            Assert.That(model.Save.Gold, Is.EqualTo(goldBefore - cost));
            Assert.That(model.Save.Diamonds, Is.EqualTo(diamondsBefore), "升级不得新增钻石消耗");
            Assert.That(panel.LastOutcome, Is.Not.Null);
            Assert.That(panel.LastOutcome.GoldSpent, Is.EqualTo(cost));
            Assert.That(panel.LastOutcome.LevelBefore, Is.EqualTo(levelBefore));
            Assert.That(panel.LastOutcome.LevelAfter, Is.EqualTo(levelBefore + 1));
            Assert.That(panel.LastHeadline, Does.Contain($"等级 {levelBefore} → {levelBefore + 1}"));
            Assert.That(panel.LastCostLine, Does.Contain("不消耗星钻"));
            Assert.That(panel.LastOutcome.PowerDelta, Is.GreaterThanOrEqualTo(0));

            foreach (string label in new[] { "等级", "战力", "攻击", "生命", "暴击", "速度" })
            {
                Text line = Find<Text>(panel.transform, "PracticeDelta-" + label);
                Assert.That(line.text, Does.Contain("→"), label + " 缺少前后差值");
            }

            // 保存兼容：重载后等级与金币都保留。
            var reloaded = new GameModel();
            Assert.That(reloaded.LevelOf(0), Is.EqualTo(levelBefore + 1));
            Assert.That(reloaded.Save.Gold, Is.EqualTo(goldBefore - cost));
        }

        [Test]
        public void ReduceMotionTogglePersistsAndDoesNotChangeTheQuote()
        {
            var model = new GameModel();
            int cost = BattleSimulator.TrainingCostAtLevel(model.LevelOf(0));
            MemberPracticePanel panel = MemberPracticePanel.Open(host.transform, model, 0);
            Assert.That(panel.ReduceMotion, Is.False);
            Assert.That(Find<Text>(panel.transform, "PracticeReduceMotionLabel").text, Does.Contain("减弱动画：关"));

            panel.ToggleReduceMotion();
            Assert.That(panel.ReduceMotion, Is.True);
            Assert.That(PlayerPrefs.GetInt(MemberTrainingPractice.ReduceMotionPreferenceKey, 0), Is.EqualTo(1));
            Assert.That(Find<Text>(panel.transform, "PracticeReduceMotionLabel").text, Does.Contain("减弱动画：开"));
            Assert.That(Find<Text>(panel.transform, "PracticeQuoteGold").text, Does.Contain(cost.ToString("N0")),
                "减弱动画不得改变报价");
        }

        [Test]
        public void RepeatedCommitSpendsGoldEachTimeAndStopsAtLevelCap()
        {
            var model = new GameModel();
            model.Save.Gold = 100000000;
            model.Save.MemberLevels[0] = GameModel.MaxMemberLevel - 1;
            int goldBefore = model.Save.Gold;
            MemberPracticePanel panel = MemberPracticePanel.Open(host.transform, model, 0);

            Assert.That(panel.CommitTraining(), Is.True);
            Assert.That(model.LevelOf(0), Is.EqualTo(GameModel.MaxMemberLevel));
            int spentOnce = goldBefore - model.Save.Gold;
            Assert.That(spentOnce, Is.GreaterThan(0));

            Assert.That(panel.CommitTraining(), Is.False, "满级后不得继续扣费");
            Assert.That(model.Save.Gold, Is.EqualTo(goldBefore - spentOnce));
            Assert.That(Find<Text>(panel.transform, "PracticeQuoteGold").text, Does.Contain("最高等级").Or.Contain("满级"));
        }

        private static T Find<T>(Transform root, string name) where T : Component
        {
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < all.Length; index++)
                if (all[index].name == name)
                {
                    T component = all[index].GetComponent<T>();
                    Assert.That(component, Is.Not.Null, name + " 缺少 " + typeof(T).Name);
                    return component;
                }

            Assert.Fail("未找到界面节点：" + name);
            return null;
        }
    }
}
