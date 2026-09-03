using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.Tests
{
    public sealed class GameplayPanelTests
    {
        private const string SaveKey = GameModel.SaveKey;
        private const string LegacySaveKey = GameModel.LegacySaveKey;
        private GameObject host;

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.DeleteKey(LegacySaveKey);
            host = new GameObject("界面测试根节点", typeof(RectTransform));
        }

        [TearDown]
        public void TearDown()
        {
            if (host != null) UnityEngine.Object.DestroyImmediate(host);
            PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.DeleteKey(LegacySaveKey);
        }

        [TestCase(0, 1)]
        [TestCase(83, 1)]
        [TestCase(84, 2)]
        [TestCase(89, 3)]
        [TestCase(94, 4)]
        [TestCase(100, 4)]
        public void StoryProgressMapsToDisplayStageAndSettlementStopsAtCompletion(int progress, int expectedStage)
        {
            Assert.That(LevelMapPanel.StoryStageForProgress(progress), Is.EqualTo(expectedStage));
            Assert.That(StoryBattlePanel.IsCurrentStoryStage(expectedStage, progress),
                Is.EqualTo(progress < GameModel.MaxStoryProgress));
        }

        [TestCase(0, 100, false)]
        [TestCase(1, 83, false)]
        [TestCase(1, 84, true)]
        [TestCase(4, 99, false)]
        [TestCase(4, 100, true)]
        [TestCase(5, 100, false)]
        public void ClearedStateUsesTheSameProgressThresholdsAsSettlement(int stage, int progress, bool cleared)
        {
            Assert.That(LevelMapPanel.IsStoryStageCleared(stage, progress), Is.EqualTo(cleared));
        }

        [Test]
        public void LevelMapRejectsLockedNodeAndPreventsDuplicateBattleOpen()
        {
            GameModel model = CreateModel();
            LevelMapPanel map = LevelMapPanel.Open(host.transform, model);
            Assert.That(map.SelectedStage, Is.EqualTo(1));
            AssertContainsNoLatinText(map.transform);

            int levelNodeCount = 0;
            foreach (Button button in map.GetComponentsInChildren<Button>(true))
            {
                if (!button.name.StartsWith("Level-", StringComparison.Ordinal)) continue;
                levelNodeCount++;
                Assert.That(button.name, Does.StartWith("Level-1-"));
            }
            Assert.That(levelNodeCount, Is.EqualTo(4), "第一章地图只应显示 1-1 到 1-4。");

            FindButton(map.transform, "Level-1-2").onClick.Invoke();
            Assert.That(map.SelectedStage, Is.EqualTo(1));
            Transform toast = FindNamed<Transform>(map.transform, "LevelToast");
            Assert.That(toast.gameObject.activeSelf, Is.True);
            Assert.That(toast.GetComponentInChildren<Text>(true).text, Does.Contain("尚未解锁"));

            Button start = FindButton(map.transform, "StartChallenge");
            start.onClick.Invoke();
            start.onClick.Invoke();

            Assert.That(host.GetComponentsInChildren<ChoSiren.Panels.TacticsBattlePanel>(true), Has.Length.EqualTo(1));
            Assert.That(start.interactable, Is.False);
        }

        [Test]
        public void BattleLocksEverySkillDuringTurnFeedback()
        {
            StoryBattlePanel panel = StoryBattlePanel.Open(host.transform, CreateModel(), 3);
            AssertContainsNoLatinText(panel.transform);
            Button ultimate = FindButton(panel.transform, "SkillUltimate");
            Assert.That(ultimate.interactable, Is.False);

            FindButton(panel.transform, "SkillDance").onClick.Invoke();

            Assert.That(FindButton(panel.transform, "SkillVocal").interactable, Is.False);
            Assert.That(FindButton(panel.transform, "SkillDance").interactable, Is.False);
            Assert.That(FindButton(panel.transform, "SkillSupport").interactable, Is.False);
            Assert.That(ultimate.interactable, Is.False);
        }

        [Test]
        public void PerformanceCannotOpenWithoutEnoughStamina()
        {
            GameModel model = CreateModel();
            model.Save.Stamina = GameModel.PerformanceStaminaCost - 1;
            string message = null;

            PerformanceStagePanel panel = PerformanceStagePanel.Open(host.transform, model, null,
                value => message = value);

            Assert.That(panel, Is.Null);
            Assert.That(message, Is.EqualTo("体力不足，暂时无法开始演出"));
            Assert.That(host.GetComponentsInChildren<PerformanceStagePanel>(true), Is.Empty);
        }

        [Test]
        public void PerformanceTapDisablesUntilBeatFeedbackEnds()
        {
            PerformanceStagePanel panel = PerformanceStagePanel.Open(host.transform, CreateModel());
            AssertContainsNoLatinText(panel.transform);
            Button tap = FindButton(panel.transform, "PerformanceTap");
            Assert.That(tap.interactable, Is.True);

            tap.onClick.Invoke();

            Assert.That(tap.interactable, Is.False);
        }

        private static GameModel CreateModel()
        {
            return new GameModel(() => new DateTime(2026, 9, 2, 12, 0, 0, DateTimeKind.Local));
        }

        private static Button FindButton(Transform root, string name)
        {
            return FindNamed<Transform>(root, name).GetComponent<Button>();
        }

        private static void AssertContainsNoLatinText(Transform root)
        {
            Text[] labels = root.GetComponentsInChildren<Text>(true);
            for (int labelIndex = 0; labelIndex < labels.Length; labelIndex++)
            {
                string value = labels[labelIndex].text;
                for (int characterIndex = 0; characterIndex < value.Length; characterIndex++)
                {
                    char character = value[characterIndex];
                    bool latin = character >= 'A' && character <= 'Z' ||
                                 character >= 'a' && character <= 'z';
                    Assert.That(latin, Is.False,
                        $"界面节点 {labels[labelIndex].name} 出现非中文文案：{value}");
                }
            }
        }

        private static T FindNamed<T>(Transform root, string name) where T : Component
        {
            Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < descendants.Length; index++)
            {
                if (descendants[index].name == name) return descendants[index].GetComponent<T>();
            }

            Assert.Fail($"未找到界面节点：{name}");
            return null;
        }
    }
}
