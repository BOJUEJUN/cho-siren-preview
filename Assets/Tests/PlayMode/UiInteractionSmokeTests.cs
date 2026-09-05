using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ChoSiren.Panels;
using ChoSiren.Systems.Tactics;
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
            Assert.That(Object.FindObjectsByType<Text>(FindObjectsInactive.Exclude)
                    .Count(text => text.text == "已切换到每周任务"), Is.EqualTo(1),
                "任务切换反馈只能出现一次，不能同时覆盖底部说明与全局提示。");
            Click("Back");
            yield return null;
            RequireActiveObject("LobbyCards");
            AssertInactiveOrMissing("TaskBoardPanel");
            AssertActiveUiUsesChineseOnly();
        }

        [UnityTest]
        public IEnumerator AccessoryPreviewAndEquipShowRealStatsWithoutFakeEnhancementOrSaveChanges()
        {
            Click("Nav-accessory");
            yield return null;
            var model = new GameModel();
            int gold = model.Save.Gold;
            string saved = PlayerPrefs.GetString(SaveKey);
            Click("Accessory-1");
            yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.That(PlayerPrefs.GetString(SaveKey), Is.EqualTo(saved),
                "Browsing an accessory is not an equip or purchase operation.");
            Assert.That(RequireActiveObject("AccessoryBefore-3").GetComponent<Text>().text,
                Is.EqualTo(model.TeamPower.ToString("N0")));
            Assert.That(RequireActiveObject("AccessoryAfter-3").GetComponent<Text>().text,
                Is.EqualTo(model.TeamPowerWithAccessory(1).ToString("N0")));
            Text effects = RequireActiveObject("AccessoryEffects").GetComponent<Text>();
            Assert.That(effects.text, Is.EqualTo("生命 +4%\n攻击 +8%\n防御 +0%"));
            Assert.That(effects.preferredHeight, Is.LessThanOrEqualTo(effects.rectTransform.rect.height));
            foreach (Text text in RequireActiveObject("AccessoryDetail").GetComponentsInChildren<Text>())
            {
                Assert.That(text.text, Does.Not.Contain("强化 +"));
                Assert.That(text.text, Does.Not.Contain("套装"));
                Assert.That(text.text, Does.Not.Contain("暴击率"));
            }
            for (int row = 0; row < 4; row++)
            {
                Text before = RequireActiveObject("AccessoryBefore-" + row).GetComponent<Text>();
                Text after = RequireActiveObject("AccessoryAfter-" + row).GetComponent<Text>();
                Assert.That(before.preferredHeight, Is.LessThanOrEqualTo(before.rectTransform.rect.height));
                Assert.That(after.preferredHeight, Is.LessThanOrEqualTo(after.rectTransform.rect.height));
            }
            Click("AccessoryEquip");
            yield return null;
            var equipped = new GameModel();
            Assert.That(equipped.Save.EquippedAccessory, Is.EqualTo(1));
            Assert.That(equipped.Save.Gold, Is.EqualTo(gold));
            Assert.That(equipped.TeamPower, Is.EqualTo(model.TeamPowerWithAccessory(1)));
            Assert.That(RequireActiveObject("AccessoryPowerChange").GetComponent<Text>().text,
                Is.EqualTo("战力变化 0"));
            Click("AccessoryEquip");
            yield return null;
            Assert.That(new GameModel().Save.EquippedAccessory, Is.EqualTo(-1));
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
            RequireActiveObject("TopBar");
            RequireActiveObject("BottomNavigation");
            RequireActiveObject("Nav-members");
            RequireActiveObject("InterviewPool-0");
            RequireActiveObject("InterviewPool-1");
            RequireActiveObject("CandidateCard");
            RequireActiveObject("CandidatePortrait");
            RequireActiveObject("CandidateStats");
            RequireActiveObject("InterviewActions");
            RequireActiveObject("SignCandidate");
            AssertInactiveOrMissing("BalanceDiamond");
            AssertInactiveOrMissing("BalanceGold");
            AssertInactiveOrMissing("PullTen");
            AssertInactiveOrMissing("RateBoard");
            AssertInactiveOrMissing("PityBoard");
            AssertInactiveOrMissing("Back");
            AssertActiveUiUsesChineseOnly();

            Click("Nav-members");
            yield return null;
            RequireActiveObject("Member-" + GameModel.Members[0].Id);
            AssertInactiveOrMissing("GachaPanel");
            AssertActiveUiUsesChineseOnly();
        }

        [UnityTest]
        public IEnumerator AuditionCandidateCarouselPoolSwitchAndSigningCompleteTheirClickChains()
        {
            Click("Nav-audition");
            yield return null;
            GachaPanel panel = Object.FindAnyObjectByType<GachaPanel>();
            Assert.That(panel, Is.Not.Null);
            AssertActiveUiUsesChineseOnly();

            string firstName = RequireActiveObject("CandidateName").GetComponent<Text>().text;
            Click("NextCandidate");
            yield return null;
            string secondName = RequireActiveObject("CandidateName").GetComponent<Text>().text;
            Assert.That(secondName, Is.Not.EqualTo(firstName), "左右候选卡必须能够切换当前候选人。");

            Click("InterviewPool-1");
            yield return null;
            Assert.That(panel.InterviewPoolIndex, Is.EqualTo(1));
            Assert.That(RequireActiveObject("ViewInterview").transform.Find("Label").GetComponent<Text>().text,
                Is.EqualTo("开始现场面试"));
            string offlineName = RequireActiveObject("CandidateName").GetComponent<Text>().text;
            string beforeCounter = RequireActiveObject("CandidateCounter").GetComponent<Text>().text;
            int beforeCount = int.Parse(beforeCounter.Split('/')[1].Trim());

            Click("SignCandidate");
            yield return null;
            string nextOfflineName = RequireActiveObject("CandidateName").GetComponent<Text>().text;
            Assert.That(nextOfflineName, Is.Not.EqualTo(offlineName), "签约成功后应从当前候选池移除该成员。");
            string afterCounter = RequireActiveObject("CandidateCounter").GetComponent<Text>().text;
            Assert.That(int.Parse(afterCounter.Split('/')[1].Trim()), Is.EqualTo(beforeCount - 1),
                "签约后只能移除当前候选，不能补入新人或重排当日名单。");
            Assert.That(Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude)
                .Count(item => item.name == "CandidateCard"), Is.EqualTo(1));
            AssertActiveUiUsesChineseOnly();

            Click("Nav-lobby");
            yield return null;
            AssertInactiveOrMissing("GachaPanel");
            RequireActiveObject("LobbyCards");
            AssertActiveUiUsesChineseOnly();
        }

        [UnityTest]
        public IEnumerator MemberDossierShowsRealBattleAttributesAndKeepsSkillColumnsSeparate()
        {
            Click("Nav-members");
            yield return null;
            Click("Member-xingli");
            yield return null;
            var manifest = ChoSiren.Systems.Data.GameData.Repository.Tactics;
            var definition = manifest.FindUnit("xingli");
            Assert.That(RequireActiveObject("MemberStatAttack").GetComponent<Text>().text,
                Is.EqualTo(ChoSiren.Systems.Tactics.BattleSimulator.MemberStatAtLevel(definition.Attack, 68).ToString("N0")));
            Assert.That(RequireActiveObject("MemberSkillPrimary").GetComponent<Text>().text,
                Is.EqualTo(manifest.FindSkill("high-note").Name));
            Assert.That(RequireActiveObject("MemberSkillSecondary").GetComponent<Text>().text,
                Is.EqualTo(manifest.FindSkill("finale").Name));
            RectTransform first = RequireActiveObject("MemberSkillPrimary").GetComponent<RectTransform>();
            RectTransform second = RequireActiveObject("MemberSkillSecondary").GetComponent<RectTransform>();
            Assert.That(first.anchoredPosition.x + first.rect.width, Is.LessThan(second.anchoredPosition.x),
                "两项技能须分别进入左右美术槽，不能都堆在左侧。");
            Click("Close");
            yield return null;
            AssertInactiveOrMissing("MemberModal");
        }

        [UnityTest]
        public IEnumerator MemberTrainingShowsCostAndKeepsUpdatedDossierOpen()
        {
            Click("Nav-members");
            yield return null;
            Click("Member-xingli");
            yield return null;
            GameSave before = JsonUtility.FromJson<GameSave>(PlayerPrefs.GetString(SaveKey));
            int cost = 180 + before.MemberLevels[0] * 12;
            Assert.That(RequireActiveObject("MemberTrainingPreview").GetComponent<Text>().text,
                Does.Contain("68 → 69"));
            Assert.That(RequireActiveObject("MemberTrainingCost").GetComponent<Text>().text,
                Does.Contain(cost.ToString("N0")));
            Click("Train");
            yield return null;
            RequireActiveObject("MemberModal");
            Assert.That(RequireActiveObject("MemberTrainingPreview").GetComponent<Text>().text,
                Does.Contain("69 → 70"));
            Assert.That(RequireActiveObject("MemberPower").GetComponent<Text>().text,
                Does.Contain("等级 69"));
            GameSave after = JsonUtility.FromJson<GameSave>(PlayerPrefs.GetString(SaveKey));
            Assert.That(after.Gold, Is.EqualTo(before.Gold - cost));
            Assert.That(after.MemberLevels.Skip(1), Is.EqualTo(before.MemberLevels.Skip(1)));
            var definition = ChoSiren.Systems.Data.GameData.Repository.Tactics.FindUnit("xingli");
            Assert.That(RequireActiveObject("MemberStatAttack").GetComponent<Text>().text,
                Is.EqualTo(BattleSimulator.MemberStatAtLevel(definition.Attack, 69).ToString("N0")));
            Text preview = RequireActiveObject("MemberTrainingPreview").GetComponent<Text>();
            Text price = RequireActiveObject("MemberTrainingCost").GetComponent<Text>();
            Canvas.ForceUpdateCanvases();
            Assert.That(preview.preferredHeight, Is.LessThanOrEqualTo(preview.rectTransform.rect.height));
            Assert.That(price.preferredHeight, Is.LessThanOrEqualTo(price.rectTransform.rect.height));
            Click("Close");
            yield return null;
            AssertInactiveOrMissing("MemberModal");
        }

        [UnityTest]
        public IEnumerator DeferredViewportRefreshNeverDismissesMemberDossier()
        {
            Click("Nav-members");
            yield return null;
            Click("Member-xingli");
            yield return null;
            GameObject dossier = RequireActiveObject("MemberModal");
            var app = Object.FindAnyObjectByType<ChoSirenApp>();
            // Drive the real debounce boundary deterministically; Editor Game View does
            // not reliably honor Screen.SetResolution. WebGL viewport resize is also QA'd.
            var pendingRefresh = typeof(ChoSirenApp).GetField("memberResizeRefreshAt",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(pendingRefresh, Is.Not.Null);
            pendingRefresh.SetValue(app, 0f);
            yield return null;
            yield return null;
            Assert.That(RequireActiveObject("MemberModal"), Is.SameAs(dossier));
            RequireActiveObject("MemberTrainingCost");
            Click("Close");
            yield return null;
            yield return null;
            AssertInactiveOrMissing("MemberModal");
            Assert.That((float)pendingRefresh.GetValue(app), Is.LessThan(0f),
                "关闭档案后仍须完成背景列表的适配，不能丢弃重排请求。");
            RequireActiveObject("Member-xingli");
        }

        [UnityTest]
        public IEnumerator TrainingBecomesUnavailableBeforeOverspending()
        {
            Click("Nav-members");
            yield return null;
            Click("Member-xingli");
            yield return null;
            int attempts = 0;
            while (RequireActiveObject("Train").GetComponent<Button>().interactable && attempts++ < 30)
            {
                Click("Train");
                yield return null;
            }
            Assert.That(attempts, Is.LessThan(30));
            RequireActiveObject("MemberModal");
            Button button = RequireActiveObject("Train").GetComponent<Button>();
            Assert.That(button.interactable, Is.False);
            Assert.That(button.transform.Find("Label").GetComponent<Text>().text, Is.EqualTo("金币不足"));
            GameSave saved = JsonUtility.FromJson<GameSave>(PlayerPrefs.GetString(SaveKey));
            Assert.That(saved.Gold, Is.GreaterThanOrEqualTo(0));
            Assert.That(saved.Gold, Is.LessThan(180 + saved.MemberLevels[0] * 12));
            Click("Close");
            yield return null;
        }

        [UnityTest]
        public IEnumerator AuditionCandidateUsesLocalArtAndOneHighlightedStrongestStat()
        {
            Click("Nav-audition");
            yield return null;
            RequireActiveObject("GachaPanel");

            Image portrait = RequireActiveObject("CandidatePortrait").GetComponent<Image>();
            Assert.That(portrait.enabled, Is.True);
            Assert.That(portrait.sprite, Is.Not.Null, "候选卡必须加载本地角色素材。");
            Assert.That(portrait.preserveAspect, Is.True, "候选立绘不能拉伸。");
            Assert.That(Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude)
                    .Count(item => item.name == "StrongestStat"), Is.EqualTo(1),
                "舞台四维只能强调当前候选人的一个最强项。");
            RequireActiveObject("CandidateCharm");
            RequireActiveObject("CandidateRecommendation");
            RequireActiveObject("SigningPrice");

            Click("Nav-members");
            yield return null;
            AssertInactiveOrMissing("GachaPanel");
            RequireActiveObject("Member-" + GameModel.Members[0].Id);
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
            GameObject globalToast = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include)
                .Select(item => item.gameObject)
                .SingleOrDefault(item => item.name == "Toast");
            Assert.That(globalToast, Is.Not.Null, "大厅全局提示节点必须存在。");
            Assert.That(globalToast.activeSelf, Is.False,
                "章节地图已有专用提示时，不能再叠加一条遮住章节奖励的全局提示。");

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
            RequireActiveObject("BattleStageArt");
            RequireActiveObject("BattleHud");
            RequireActiveObject("EnemyStage");
            RequireActiveObject("DiceConsole");
            RequireActiveObject("Dice-0");
            RequireActiveObject("Dice-4");
            RequireActiveObject("DiceReroll");
            RequireActiveObject("EnergyReroll");
            RequireActiveObject("TeamRoster");
            AssertInactiveOrMissing("BattleExit");
            AssertActiveUiUsesChineseOnly();

            TacticsBattlePanel tactics = Object.FindAnyObjectByType<TacticsBattlePanel>();
            Assert.That(tactics, Is.Not.Null);
            Click("PauseToggle");
            Assert.That(tactics.IsPaused, Is.True, "暂停按钮必须真正冻结战斗推进");
            RequireActiveObject("PauseOverlay");
            RequireActiveObject("BattleExit");
            Click("PauseToggle");
            Assert.That(tactics.IsPaused, Is.False, "继续按钮必须恢复战斗推进");

            Click("PauseToggle");
            Click("BattleExit");
            yield return null;
            AssertInactiveOrMissing("TacticsBattlePanel");
            RequireActiveObject("LevelMapPanel");

            Click("Level-1-1");
            Click("StartChallenge");
            yield return null;
            RequireActiveObject("TacticsBattlePanel");

            Click("AutoToggle");
            float battleTimeout = Time.realtimeSinceStartup + 90f;
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
        public IEnumerator ProgrammaticBattleCloseAbortsSettlesAndAllowsImmediateRetry()
        {
            GameObject battleHost = new GameObject("战斗关闭测试根节点", typeof(RectTransform));
            var model = new GameModel(() => new System.DateTime(2026, 9, 3, 12, 0, 0,
                System.DateTimeKind.Local));
            int staminaBefore = model.Save.Stamina;
            BattleSimulator battle = model.StartStageBattle("stage-1-1", 7331UL, out string startMessage);
            Assert.That(battle, Is.Not.Null, startMessage);
            int staminaAfterStart = model.Save.Stamina;
            Assert.That(staminaAfterStart, Is.LessThan(staminaBefore), "开始关卡时应且仅应扣除一次体力。");

            int backCount = 0;
            TacticsBattlePanel panel = TacticsBattlePanel.Open(battleHost.transform, model, battle,
                finished: simulator => model.SettleStageBattle(simulator, out _),
                back: () => backCount++);
            Assert.That(panel.Battle.Outcome, Is.EqualTo(BattleOutcome.Ongoing));

            panel.Close();
            yield return null;

            Assert.That(panel == null, Is.True, "进行中的战斗调用 Close 后必须销毁面板。");
            Assert.That(backCount, Is.EqualTo(1), "安全关闭后必须回到关卡地图一次。");
            Assert.That(model.Save.Stamina, Is.EqualTo(staminaAfterStart), "关闭战斗不能二次扣除体力。");
            model.SettleStageBattle(battle, out string settleAgain);
            Assert.That(settleAgain, Does.Contain("已经结算"), "Close 必须清除模型里的 pending battle。");

            BattleSimulator retry = model.StartStageBattle("stage-1-1", 7332UL, out string retryMessage);
            Assert.That(retry, Is.Not.Null, retryMessage);
            retry.AutoPlay(0);
            model.SettleStageBattle(retry, out _);
            Object.Destroy(battleHost);
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
                "SSR", "SR", "R", "S", "A", "B", "C", "Rapper", "DJ",
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
