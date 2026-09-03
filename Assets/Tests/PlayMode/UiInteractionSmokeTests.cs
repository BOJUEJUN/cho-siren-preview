using System.Collections;
using System.Collections.Generic;
using ChoSiren.Panels;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ChoSiren.Tests
{
    /// <summary>
    /// Click-through acceptance tests for the runtime-built UI. These deliberately dispatch
    /// pointer clicks instead of calling application methods, so a missing/disabled Button or
    /// a broken onClick listener fails at the same boundary a player experiences.
    /// </summary>
    public sealed class UiInteractionSmokeTests
    {
        private const string SaveKey = GameModel.SaveKey;
        private const string LegacySaveKey = GameModel.LegacySaveKey;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.DeleteKey(LegacySaveKey);
            PlayerPrefs.Save();
            DestroyAll<ChoSirenApp>();
            DestroyAll<EventSystem>();
            yield return null;

            new GameObject("CHO-SIREN UI Smoke App").AddComponent<ChoSirenApp>();
            yield return null;

            Assert.That(Object.FindAnyObjectByType<ChoSirenApp>(), Is.Not.Null,
                "The app did not bootstrap for the UI smoke test.");
            RequireActiveObject("LobbyCards");
            Assert.That(EventSystem.current, Is.Not.Null,
                "Pointer-click smoke tests require an active EventSystem.");

            float timeout = Time.realtimeSinceStartup + 15f;
            while ((!IsInteractable("LiveOnStage") || GameObject.Find("StartupLoading") != null) &&
                   Time.realtimeSinceStartup < timeout)
                yield return null;

            Assert.That(IsInteractable("LiveOnStage"), Is.True,
                "The single performance entry never became interactable after startup loading.");
            Assert.That(GameObject.Find("StartupLoading"), Is.Null,
                "Startup loading must finish before click-through acceptance begins.");
            AssertActiveUiUsesChineseOnly();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            DestroyAll<ChoSirenApp>();
            DestroyAll<EventSystem>();
            yield return null;
            PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.DeleteKey(LegacySaveKey);
            PlayerPrefs.Save();
        }

        [UnityTest]
        public IEnumerator HeaderAndLobbyModalButtonsCompleteTheirClickChains()
        {
            Click("Profile");
            RequireActiveObject("InfoModal");
            Click("Primary");
            yield return null;
            AssertInactiveOrMissing("InfoModal");

            Click("Mail");
            RequireActiveObject("InfoModal");
            Click("Close");
            yield return null;
            AssertInactiveOrMissing("InfoModal");

            Click("Settings");
            RequireActiveObject("SettingsModal");
            Click("Toggle");
            yield return null;
            RequireActiveObject("SettingsModal");
            Click("Done");
            yield return null;
            AssertInactiveOrMissing("SettingsModal");

            RequireActiveObject("闪耀舞台");
            RequireActiveObject("冒险剧本");
            RequireActiveObject("任务");
            Click("闪耀舞台");
            RequireActiveObject("InfoModal");
            Click("Close");
            yield return null;
            AssertInactiveOrMissing("InfoModal");

            Click("任务");
            yield return null;
            RequireActiveObject("TaskBoardPanel");
            Click("TabWeekly");
            yield return null;
            RequireActiveObject("TaskBoardPanel");
            Click("Back");
            yield return null;
            RequireActiveObject("LobbyCards");
            AssertInactiveOrMissing("TaskBoardPanel");
            AssertActiveUiUsesChineseOnly();
        }

        [UnityTest]
        public IEnumerator BottomNavigationButtonsRenderEveryDestination()
        {
            Click("Nav-team");
            yield return null;
            RequireActiveObject("TeamPower");
            AssertActiveUiUsesChineseOnly();

            Click("Nav-members");
            yield return null;
            RequireActiveObject("Member-" + GameModel.Members[0].Id);
            AssertActiveUiUsesChineseOnly();

            Click("Nav-accessory");
            yield return null;
            RequireActiveObject("Accessory-0");
            RequireActiveObject("AccessorySlot-5");
            RequireActiveObject("AccessoryPreviewCharacter");
            RequireActiveObject("AccessoryDetail");
            RequireActiveObject("AccessoryCollection");
            AssertActiveUiUsesChineseOnly();

            Click("Nav-audition");
            yield return null;
            RequireActiveObject("GachaPanel");
            RequireActiveObject("GachaTopShade");
            RequireActiveObject("FeaturedFrame");
            RequireActiveObject("RateBoard");
            RequireActiveObject("PityBoard");
            RequireActiveObject("GachaDetails");
            RequireActiveObject("PullTen");
            AssertActiveUiUsesChineseOnly();

            Click("Back");
            yield return null;
            RequireActiveObject("LobbyCards");
            AssertInactiveOrMissing("GachaPanel");
            AssertActiveUiUsesChineseOnly();
        }

        [UnityTest]
        public IEnumerator GachaPullOneShowsResultAndReturns()
        {
            Click("Nav-audition");
            yield return null;
            RequireActiveObject("GachaPanel");
            AssertActiveUiUsesChineseOnly();

            Click("PullOne");
            float timeout = Time.realtimeSinceStartup + 8f;
            while (GameObject.Find("GachaResult") == null && Time.realtimeSinceStartup < timeout)
                yield return null;

            RequireActiveObject("GachaResult");
            AssertActiveUiUsesChineseOnly();
            Click("ResultBack");
            yield return null;
            AssertInactiveOrMissing("GachaResult");
            RequireActiveObject("GachaPanel");

            Click("Back");
            yield return null;
            AssertInactiveOrMissing("GachaPanel");
            RequireActiveObject("LobbyCards");
            AssertActiveUiUsesChineseOnly();
        }

        [UnityTest]
        public IEnumerator EveryGachaBannerUsesLocalCharacterArt()
        {
            Click("Nav-audition");
            yield return null;
            RequireActiveObject("GachaPanel");

            string[] bannerIds = { "debut-xingli", "standard-signing", "costume-neon-night" };
            for (int index = 0; index < bannerIds.Length; index++)
            {
                Click("BannerTab-" + bannerIds[index]);
                yield return null;

                Image portrait = RequireActiveObject("FeaturedPortrait").GetComponent<Image>();
                Assert.That(portrait, Is.Not.Null, "招募主视觉必须由 Image 渲染");
                Assert.That(portrait.enabled, Is.True, $"卡池 {bannerIds[index]} 的主视觉被隐藏");
                Assert.That(portrait.sprite, Is.Not.Null, $"卡池 {bannerIds[index]} 没有加载本地角色素材");
                Assert.That(portrait.preserveAspect, Is.True, "招募主视觉不能拉伸角色立绘");
                RectTransform frame = RequireActiveObject("FeaturedFrame").GetComponent<RectTransform>();
                Assert.That(frame.rect.width, Is.GreaterThanOrEqualTo(620f), "C 方案必须保持中央大角色主视觉");
            }

            Click("Back");
            yield return null;
            AssertInactiveOrMissing("GachaPanel");
        }

        [UnityTest]
        public IEnumerator StoryCardLevelSelectionBattleAndChapterRoute()
        {
            Click("冒险剧本");
            yield return null;

            LevelMapPanel map = Object.FindAnyObjectByType<LevelMapPanel>();
            Assert.That(map, Is.Not.Null, "The adventure card did not open the level map.");
            Assert.That(map.SelectedStage, Is.EqualTo(1));

            Click("Level-1-4");
            Assert.That(map.SelectedStage, Is.EqualTo(1),
                "Clicking a locked level must not silently select it.");
            RequireActiveObject("LevelToast");

            Click("StoryChapter-01");
            yield return null;
            RequireActiveObject("StoryPanel");
            AssertActiveUiUsesChineseOnly();

            float storyTimeout = Time.realtimeSinceStartup + 30f;
            while (GameObject.Find("Choice-0") == null && Time.realtimeSinceStartup < storyTimeout)
            {
                if (IsInteractable("DialogueBox")) Click("DialogueBox");
                yield return null;
            }

            RequireActiveObject("Choice-0");
            Click("Choice-0");
            yield return null;

            while (GameObject.Find("StoryPanel") != null && Time.realtimeSinceStartup < storyTimeout)
            {
                if (IsInteractable("DialogueBox")) Click("DialogueBox");
                yield return null;
            }

            AssertInactiveOrMissing("StoryPanel");
            RequireActiveObject("LevelMapPanel");
            AssertActiveUiUsesChineseOnly();

            Click("Level-1-1");
            Assert.That(map.SelectedStage, Is.EqualTo(1));
            Click("StartChallenge");
            yield return null;
            RequireActiveObject("TacticsBattlePanel");
            RequireActiveObject("BattleHud");
            RequireActiveObject("EnemyStage");
            RequireActiveObject("DiceConsole");
            RequireActiveObject("Dice-0");
            RequireActiveObject("Dice-4");
            RequireActiveObject("DiceReroll");
            RequireActiveObject("EnergyReroll");
            RequireActiveObject("TeamRoster");
            AssertActiveUiUsesChineseOnly();

            TacticsBattlePanel tactics = Object.FindAnyObjectByType<TacticsBattlePanel>();
            Assert.That(tactics, Is.Not.Null);
            Click("PauseToggle");
            Assert.That(tactics.IsPaused, Is.True, "暂停按钮必须真正冻结战斗推进");
            RequireActiveObject("PauseOverlay");
            Click("PauseToggle");
            Assert.That(tactics.IsPaused, Is.False, "继续按钮必须恢复战斗推进");

            Click("AutoToggle");
            float battleTimeout = Time.realtimeSinceStartup + 20f;
            while (GameObject.Find("BattleResult") == null && Time.realtimeSinceStartup < battleTimeout)
                yield return null;

            RequireActiveObject("BattleResult");
            Assert.That(RequireActiveObject("BattleResult").activeInHierarchy, Is.True);
            Click("ResultContinue");
            yield return null;
            AssertInactiveOrMissing("TacticsBattlePanel");
            RequireActiveObject("LevelMapPanel");
            AssertActiveUiUsesChineseOnly();

            Click("Back");
            yield return null;
            AssertInactiveOrMissing("LevelMapPanel");
            RequireActiveObject("LobbyCards");
        }

        [UnityTest]
        public IEnumerator PerformanceCardSixTapButtonsShowResultAndReturnToLobby()
        {
            Click("LiveOnStage");
            yield return null;
            RequireActiveObject("PerformanceStagePanel");

            for (int note = 0; note < 6; note++)
            {
                Click("PerformanceTap");
                yield return new WaitForSecondsRealtime(0.55f);
            }

            RequireActiveObject("PerformanceResult");
            Assert.That(RequireActiveObject("PerformanceResult").activeInHierarchy, Is.True,
                "Six accepted note-button clicks must reveal the performance result.");

            Click("ReturnLobby");
            yield return null;
            AssertInactiveOrMissing("PerformanceStagePanel");
            RequireActiveObject("LobbyCards");
            AssertActiveUiUsesChineseOnly();
        }

        private static void AssertActiveUiUsesChineseOnly()
        {
            HashSet<string> allowedGameTokens = new HashSet<string>
            {
                "SSR", "SR", "R", "S", "A", "B", "C",
            };

            Text[] labels = Object.FindObjectsByType<Text>(FindObjectsInactive.Exclude);
            for (int labelIndex = 0; labelIndex < labels.Length; labelIndex++)
            {
                string value = labels[labelIndex].text ?? string.Empty;
                int runStart = -1;
                for (int characterIndex = 0; characterIndex <= value.Length; characterIndex++)
                {
                    bool latin = characterIndex < value.Length &&
                                 (value[characterIndex] >= 'A' && value[characterIndex] <= 'Z' ||
                                  value[characterIndex] >= 'a' && value[characterIndex] <= 'z');
                    if (latin && runStart < 0) runStart = characterIndex;
                    if (latin || runStart < 0) continue;

                    string token = value.Substring(runStart, characterIndex - runStart);
                    Assert.That(allowedGameTokens.Contains(token), Is.True,
                        $"激活界面节点 {labels[labelIndex].name} 出现英文文案：{value}");
                    runStart = -1;
                }
            }

            string[] navigationLabels = { "团队", "成员", "大厅", "饰品", "选秀" };
            for (int index = 0; index < navigationLabels.Length; index++)
            {
                GameObject navigation = RequireActiveObject("Nav-" + new[]
                {
                    "team", "members", "lobby", "accessory", "audition",
                }[index]);
                Text label = navigation.transform.Find("Label").GetComponent<Text>();
                Assert.That(label.text, Is.EqualTo(navigationLabels[index]),
                    "底部导航必须只保留单行中文标签，不得附加英文副标题。");
            }
        }

        private static void Click(string objectName)
        {
            GameObject target = RequireActiveObject(objectName);
            Button button = target.GetComponent<Button>();
            Assert.That(button, Is.Not.Null, $"'{objectName}' exists but has no Button component.");
            Assert.That(button.enabled, Is.True, $"'{objectName}' has a disabled Button component.");
            Assert.That(button.IsInteractable(), Is.True, $"'{objectName}' is not interactable.");
            Assert.That(button.targetGraphic, Is.Not.Null, $"'{objectName}' has no target graphic.");
            Assert.That(button.targetGraphic.raycastTarget, Is.True,
                $"'{objectName}' cannot receive a UI raycast.");
            Assert.That(EventSystem.current, Is.Not.Null, "No EventSystem is available for pointer clicks.");

            PointerEventData pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
            };
            Assert.That(ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerClickHandler), Is.True,
                $"'{objectName}' did not handle the pointer click.");
        }

        private static GameObject RequireActiveObject(string objectName)
        {
            GameObject result = GameObject.Find(objectName);
            Assert.That(result, Is.Not.Null, $"Expected active UI object '{objectName}' was not found.");
            Assert.That(result.activeInHierarchy, Is.True, $"UI object '{objectName}' is inactive.");
            return result;
        }

        private static void AssertInactiveOrMissing(string objectName)
        {
            GameObject active = GameObject.Find(objectName);
            Assert.That(active, Is.Null, $"UI object '{objectName}' should have closed after the click.");
        }

        private static bool IsInteractable(string objectName)
        {
            GameObject target = GameObject.Find(objectName);
            Button button = target != null ? target.GetComponent<Button>() : null;
            return button != null && button.isActiveAndEnabled && button.IsInteractable();
        }

        private static void DestroyAll<T>() where T : Component
        {
            T[] objects = Object.FindObjectsByType<T>(FindObjectsInactive.Include);
            for (int index = 0; index < objects.Length; index++)
            {
                if (objects[index] != null) Object.Destroy(objects[index].gameObject);
            }
        }
    }
}
