using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ChoSiren.Panels;
using ChoSiren.Systems.Presentation;
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
        public IEnumerator ClearSaveHasVisibleConfirmationAndReturnsToUnclearedChapter()
        {
            var saved = JsonUtility.FromJson<GameSave>(PlayerPrefs.GetString(SaveKey));
            saved.StoryProgress = 83;
            saved.ClearedStages = new List<StageClear>
            {
                new StageClear { Id = "stage-1-1", Stars = 3 },
                new StageClear { Id = "stage-1-2", Stars = 2 }
            };
            saved.MemberLevels = saved.MemberLevels.Select(_ => 68).ToList();
            foreach (var member in saved.Roster.Members) member.Level = 68;
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(saved));
            DestroyAll<ChoSirenApp>();
            yield return null;
            new GameObject("CHO-SIREN Reset Smoke App").AddComponent<ChoSirenApp>();
            yield return null;
            float timeout = Time.realtimeSinceStartup + 15f;
            while (GameObject.Find("StartupLoading") != null && Time.realtimeSinceStartup < timeout)
                yield return null;
            Assert.That(RequireActiveObject("PlayerLevel").GetComponent<Text>().text, Is.EqualTo($"战力 {new GameModel().TeamPower:N0}"));

            Click("Settings");
            Click("Reset");
            yield return null;
            RequireActiveObject("InfoModal");
            Assert.That(new GameModel().IsStageCleared("stage-1-2"), Is.True,
                "Opening the confirmation must not erase progress.");
            Click("Close");
            yield return null;
            Assert.That(new GameModel().IsStageCleared("stage-1-2"), Is.True);
            Click("Settings");
            Click("Reset");
            yield return null;
            Click("Primary");
            yield return null;
            Assert.That(RequireActiveObject("PlayerLevel").GetComponent<Text>().text, Is.EqualTo($"战力 {new GameModel().TeamPower:N0}"));
            var fresh = new GameModel();
            Assert.That(fresh.Save.ClearedStages, Is.Empty);
            Assert.That(fresh.CurrentChapterOneStage, Is.EqualTo(1));
            Assert.That(fresh.IsStageUnlocked("stage-1-2"), Is.False);
            timeout = Time.realtimeSinceStartup + 15f;
            while (!IsInteractable("LiveOnStage") && Time.realtimeSinceStartup < timeout)
                yield return null;
            Click("LiveOnStage");
            yield return null;
            RequireActiveObject("LevelMapPanel");
            Assert.That(Object.FindObjectsByType<Text>(FindObjectsInactive.Exclude)
                .Any(text => text.text.Contains("已通关")), Is.False);
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
            AssertInactiveOrMissing("冒险剧本");
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
        public IEnumerator PersonalEquipmentPreviewTransferAndUnequipPersistCorrectCharacterStats()
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
                Is.EqualTo(model.TeamPowerWithMemberAccessory(0, 1).ToString("N0")));
            Assert.That(RequireActiveObject("EquipmentMemberName").GetComponent<Text>().text,
                Does.StartWith(GameModel.Members[0].Name));
            int originalFirstAttack = model.StatsOf(0).Attack;
            int originalSecondAttack = model.StatsOf(1).Attack;
            int originalSecondHp = model.StatsOf(1).Hp;
            Text effects = RequireActiveObject("AccessoryEffects").GetComponent<Text>();
            Assert.That(effects.text, Is.EqualTo("生命 +4%\n攻击 +8%\n防御 +0%"));
            Assert.That(effects.preferredHeight, Is.LessThanOrEqualTo(effects.rectTransform.rect.height));
            foreach (Text text in RequireActiveObject("AccessoryDetail").GetComponentsInChildren<Text>())
            {
                Assert.That(text.text, Does.Not.Contain("套装"));
                Assert.That(text.text, Does.Not.Contain("暴击率"));
            }
            for (int row = 0; row < 4; row++)
            {
                Text statName = RequireActiveObject("AccessoryStatName-" + row).GetComponent<Text>();
                Text before = RequireActiveObject("AccessoryBefore-" + row).GetComponent<Text>();
                Text after = RequireActiveObject("AccessoryAfter-" + row).GetComponent<Text>();
                Assert.That(statName.preferredHeight, Is.LessThanOrEqualTo(statName.rectTransform.rect.height));
                Assert.That(before.preferredHeight, Is.LessThanOrEqualTo(before.rectTransform.rect.height));
                Assert.That(after.preferredHeight, Is.LessThanOrEqualTo(after.rectTransform.rect.height));
                Assert.That(before.fontSize, Is.GreaterThanOrEqualTo(18));
                Assert.That(after.fontSize, Is.GreaterThanOrEqualTo(18));
            }
            Click("AccessoryEquip");
            yield return null;
            var equipped = new GameModel();
            Assert.That(equipped.EquippedAccessoryFor(0), Is.EqualTo(1));
            Assert.That(equipped.StatsOf(0).Attack, Is.GreaterThan(originalFirstAttack));
            Assert.That(equipped.StatsOf(1).Attack, Is.EqualTo(originalSecondAttack),
                "装备只影响穿戴者，不能给所有队员加上同一份属性。");
            Assert.That(equipped.Save.Gold, Is.EqualTo(gold));
            Assert.That(equipped.TeamPower, Is.EqualTo(model.TeamPowerWithMemberAccessory(0, 1)));
            Assert.That(RequireActiveObject("AccessoryPowerChange").GetComponent<Text>().text,
                Is.EqualTo($"已生效 · 战力 +{equipped.TeamPower - model.TeamPower:N0}"),
                "装备成功后展示已生效加成，不要默认用卸下损失误导玩家。");

            Click("EquipmentChooseMember");
            yield return null;
            Click("PickMember-1");
            yield return null;
            Assert.That(RequireActiveObject("EquipmentMemberName").GetComponent<Text>().text,
                Does.StartWith(GameModel.Members[1].Name));
            Assert.That(RequireActiveObject("EquipmentPortrait").GetComponent<Image>().sprite.texture,
                Is.EqualTo(Resources.Load<Sprite>(GameModel.Members[1].ResourcePath).texture));
            Assert.That(new GameModel().EquippedAccessoryFor(0), Is.EqualTo(1),
                "浏览另一角色不能自动移动已装备饰品。");
            Click("Accessory-1");
            yield return null;
            Assert.That(RequireActiveObject("AccessoryBefore-0").GetComponent<Text>().text,
                Is.EqualTo(originalSecondHp.ToString("N0")));
            Assert.That(RequireActiveObject("AccessoryAfter-1").GetComponent<Text>().text,
                Is.EqualTo(equipped.StatsOf(1, 1).Attack.ToString("N0")));
            Assert.That(RequireActiveObject("AccessoryEquip").GetComponentInChildren<Text>().text,
                Does.Contain("从" + GameModel.Members[0].Name + "转移"));
            Click("AccessoryEquip");
            yield return null;
            var transferred = new GameModel();
            Assert.That(transferred.EquippedAccessoryFor(0), Is.EqualTo(-1));
            Assert.That(transferred.EquippedAccessoryFor(1), Is.EqualTo(1));
            Assert.That(transferred.AccessoryOwner(1), Is.EqualTo(1));
            Assert.That(transferred.StatsOf(0).Attack, Is.EqualTo(originalFirstAttack));
            Assert.That(transferred.StatsOf(1).Attack, Is.GreaterThan(originalSecondAttack));
            Assert.That(transferred.TeamPower, Is.EqualTo(equipped.TeamPowerWithMemberAccessory(1, 1)));
            Click("AccessoryEquip");
            yield return null;
            var unequipped = new GameModel();
            Assert.That(unequipped.EquippedAccessoryFor(1), Is.EqualTo(-1));
            Assert.That(unequipped.StatsOf(1).Attack, Is.EqualTo(originalSecondAttack));
            Assert.That(unequipped.TeamPower, Is.EqualTo(model.TeamPower));
        }

        [UnityTest]
        public IEnumerator CurrencyPlusExplainsUseAndGoldExchangeRequiresOneConfirmedPayment()
        {
            var before = new GameModel();
            int gold = before.Save.Gold, diamonds = before.Save.Diamonds;
            Click("Currency-gold");
            yield return null;
            Assert.That(RequireActiveObject("CurrencyHelp").GetComponent<Text>().text,
                Does.Contain("训练成员"));
            Click("CurrencyExchange");
            yield return null;
            RequireActiveObject("CurrencyConfirmation");
            Assert.That(new GameModel().Save.Diamonds, Is.EqualTo(diamonds),
                "打开报价确认窗不能扣费。");
            Click("CancelExchange");
            yield return null;
            Assert.That(new GameModel().Save.Gold, Is.EqualTo(gold));
            Assert.That(new GameModel().Save.Diamonds, Is.EqualTo(diamonds));
            Click("CurrencyExchange");
            yield return null;
            Button submit = RequireActiveObject("ConfirmExchange").GetComponent<Button>();
            Click("ConfirmExchange");
            // A second queued event on the same dialog cannot repeat the purchase.
            submit.onClick.Invoke();
            yield return null;
            var after = new GameModel();
            Assert.That(after.Save.Gold, Is.EqualTo(gold + GameModel.GoldExchangeAmount));
            Assert.That(after.Save.Diamonds, Is.EqualTo(diamonds - GameModel.GoldExchangeDiamondCost));
            Assert.That(RequireActiveObject("CurrencyBalance").GetComponent<Text>().text,
                Does.Contain(after.Save.Gold.ToString("N0")));
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
            RequireActiveObject("Accessory-5");
            RequireActiveObject("EquipmentPortrait");
            RequireActiveObject("AccessoryDetail");
            RequireActiveObject("EquipmentScroll");
            RequireActiveObject("EquipmentInventoryTitle");
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
            RequireActiveObject("MemberModal");
            Assert.That(RequireActiveObject("MemberOwnershipStatus").GetComponent<Text>().text,
                Is.EqualTo("已签约成员"));
            Assert.That(RequireActiveObject("MemberModal").GetComponentsInChildren<Text>()
                .Any(text => text.text == offlineName), Is.True,
                "签约后必须直接展示刚获得的角色，而不是留在另一个未同步页面。");
            int signedIndex = System.Array.FindIndex(GameModel.Members, member => member.Name == offlineName);
            Assert.That(signedIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(new GameModel().IsUnlocked(signedIndex), Is.True);
            Click("CloseTop");
            yield return null;
            RequireActiveObject("Member-" + GameModel.Members[signedIndex].Id);
            Assert.That(RequireActiveObject("MemberOwnedFilter").GetComponentInChildren<Text>().text,
                Does.Contain("开"));
            Click("Nav-audition");
            yield return null;
            Click("InterviewPool-1");
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
            int xingliIndex = System.Array.FindIndex(GameModel.Members, member => member.Id == "xingli");
            GameModel.StageStats(GameModel.Members[xingliIndex], xingliIndex, 1, 1,
                out int vocal, out _, out _, out _, out _);
            Assert.That(RequireActiveObject("MemberStatVocal").GetComponent<Text>().text,
                Is.EqualTo(vocal.ToString()));
            Assert.That(RequireActiveObject("MemberSkillPrimary").GetComponent<Text>().text,
                Is.EqualTo(BattleSimulator.ActiveSkillName(CombatRace.Charm, false)));
            Assert.That(RequireActiveObject("MemberSkillSecondary").GetComponent<Text>().text,
                Is.EqualTo(BattleSimulator.ActiveSkillName(CombatRace.Charm, true)));
            RectTransform first = RequireActiveObject("MemberSkillPrimary").GetComponent<RectTransform>();
            RectTransform second = RequireActiveObject("MemberSkillSecondary").GetComponent<RectTransform>();
            RectTransform skillPanel = RequireActiveObject("MemberSkillPanel").GetComponent<RectTransform>();
            Canvas.ForceUpdateCanvases();
            Rect InSkillPanel(RectTransform child)
            {
                var corners = new Vector3[4];
                child.GetWorldCorners(corners);
                Vector3 min = skillPanel.InverseTransformPoint(corners[0]);
                Vector3 max = skillPanel.InverseTransformPoint(corners[2]);
                return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            }
            Rect firstRect = InSkillPanel(first), secondRect = InSkillPanel(second);
            Assert.That(firstRect.xMax, Is.LessThan(secondRect.xMin),
                "两项技能须在共同技能面板坐标中左右分列，不能比较各自卡片内的局部坐标。");
            Rect firstCard = InSkillPanel(first.parent as RectTransform);
            Rect secondCard = InSkillPanel(second.parent as RectTransform);
            Assert.That(firstCard.Overlaps(secondCard), Is.False, "两个完整技能卡不能重叠。");
            Assert.That(firstCard.Contains(firstRect.min) && firstCard.Contains(firstRect.max), Is.True);
            Assert.That(secondCard.Contains(secondRect.min) && secondCard.Contains(secondRect.max), Is.True);
            Click("CloseTop");
            yield return null;
            AssertInactiveOrMissing("MemberModal");
        }

        [UnityTest]
        public IEnumerator MemberTrainingPracticeShowsGoldOnlyCostAndReopensUpdatedDossier()
        {
            Click("Nav-members");
            yield return null;
            Click("Member-xingli");
            yield return null;
            GameSave before = JsonUtility.FromJson<GameSave>(PlayerPrefs.GetString(SaveKey));
            int level = before.MemberLevels[0];
            int cost = BattleSimulator.TrainingCostAtLevel(level);
            Assert.That(RequireActiveObject("MemberTrainingPreview").GetComponent<Text>().text,
                Does.Contain($"{level} → {level + 1}"));
            Assert.That(RequireActiveObject("MemberTrainingCost").GetComponent<Text>().text,
                Does.Contain(cost.ToString("N0")));

            // 减弱动画：批处理测试不必等完整 1.4 秒，但信息与正式版完全一致。
            PlayerPrefs.SetInt(MemberTrainingPractice.ReduceMotionPreferenceKey, 1);
            Click("Train");
            yield return null;
            MemberPracticePanel practice = Object.FindAnyObjectByType<MemberPracticePanel>();
            Assert.That(practice, Is.Not.Null, "训练入口必须进入紧凑练习反馈面板。");
            Assert.That(RequireActiveObject("PracticeNoDiamond").GetComponent<Text>().text,
                Does.Contain("不消耗星钻"), "升级不得新增钻石消耗。");
            Assert.That(RequireActiveObject("PracticeQuoteGold").GetComponent<Text>().text,
                Does.Contain(cost.ToString("N0")));
            Assert.That(RequireActiveObject("PracticePortrait").GetComponent<Image>().sprite,
                Is.EqualTo(Resources.Load<Sprite>(GameModel.Members[0].ResourcePath)),
                "练习反馈复用现有立绘，不新增美术。");

            Click("StartPractice");
            yield return new WaitForSeconds(MemberTrainingPractice.ReducedDurationSeconds + .35f);
            Assert.That(practice.LastOutcome, Is.Not.Null);
            Assert.That(practice.LastOutcome.GoldSpent, Is.EqualTo(cost));
            Assert.That(practice.LastHeadline, Does.Contain($"等级 {level} → {level + 1}"));
            Assert.That(practice.LastCostLine, Does.Contain("不消耗星钻"));
            GameSave after = JsonUtility.FromJson<GameSave>(PlayerPrefs.GetString(SaveKey));
            Assert.That(after.Gold, Is.EqualTo(before.Gold - cost));
            Assert.That(after.Diamonds, Is.EqualTo(before.Diamonds), "练习升级不得消耗星钻。");
            Assert.That(after.MemberLevels.Skip(1), Is.EqualTo(before.MemberLevels.Skip(1)));

            Click("PracticeDone");
            yield return null;
            RequireActiveObject("MemberModal");
            Assert.That(RequireActiveObject("MemberTrainingPreview").GetComponent<Text>().text,
                Does.Contain($"{level + 1} → {level + 2}"));
            Assert.That(RequireActiveObject("MemberPower").GetComponent<Text>().text,
                Does.Contain($"等级 {level + 1}"));
            int xingliIndex = System.Array.FindIndex(GameModel.Members, member => member.Id == "xingli");
            GameModel.StageStats(GameModel.Members[xingliIndex], xingliIndex, 1, level + 1,
                out int vocal, out _, out _, out _, out _);
            Assert.That(RequireActiveObject("MemberStatVocal").GetComponent<Text>().text,
                Is.EqualTo(vocal.ToString()));
            Text preview = RequireActiveObject("MemberTrainingPreview").GetComponent<Text>();
            Text price = RequireActiveObject("MemberTrainingCost").GetComponent<Text>();
            Canvas.ForceUpdateCanvases();
            Assert.That(preview.preferredHeight, Is.LessThanOrEqualTo(preview.rectTransform.rect.height));
            Assert.That(price.preferredHeight, Is.LessThanOrEqualTo(price.rectTransform.rect.height));
            Click("CloseTop");
            yield return null;
            AssertInactiveOrMissing("MemberModal");
            PlayerPrefs.DeleteKey(MemberTrainingPractice.ReduceMotionPreferenceKey);
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
            Click("CloseTop");
            yield return null;
            yield return null;
            AssertInactiveOrMissing("MemberModal");
            Assert.That((float)pendingRefresh.GetValue(app), Is.LessThan(0f),
                "关闭档案后仍须完成背景列表的适配，不能丢弃重排请求。");
            RequireActiveObject("Member-xingli");
        }

        [UnityTest]
        public IEnumerator TrainingPracticeBecomesUnavailableBeforeOverspending()
        {
            Click("Nav-members");
            yield return null;
            Click("Member-xingli");
            yield return null;
            PlayerPrefs.SetInt(MemberTrainingPractice.ReduceMotionPreferenceKey, 1);
            int attempts = 0;
            while (RequireActiveObject("Train").GetComponent<Button>().interactable && attempts++ < 30)
            {
                Click("Train");
                yield return null;
                MemberPracticePanel practice = Object.FindAnyObjectByType<MemberPracticePanel>();
                Assert.That(practice, Is.Not.Null);
                Assert.That(RequireActiveObject("StartPractice").GetComponent<Button>().interactable, Is.True,
                    "档案报价与练习面板扣费边界必须一致：档案显示可练时练习按钮必须可用。");
                Click("StartPractice");
                yield return new WaitForSeconds(MemberTrainingPractice.ReducedDurationSeconds + .35f);
                Click("PracticeDone");
                yield return null;
            }
            Assert.That(attempts, Is.LessThan(30));
            RequireActiveObject("MemberModal");
            Button button = RequireActiveObject("Train").GetComponent<Button>();
            Assert.That(button.interactable, Is.False);
            Assert.That(button.transform.Find("Label").GetComponent<Text>().text, Is.EqualTo("星光币不足"));
            GameSave saved = JsonUtility.FromJson<GameSave>(PlayerPrefs.GetString(SaveKey));
            Assert.That(saved.Gold, Is.GreaterThanOrEqualTo(0));
            Assert.That(saved.Gold, Is.LessThan(BattleSimulator.TrainingCostAtLevel(saved.MemberLevels[0])));
            Click("CloseTop");
            yield return null;
            PlayerPrefs.DeleteKey(MemberTrainingPractice.ReduceMotionPreferenceKey);
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
        public IEnumerator StartPerformanceLevelSelectionBattleAndChapterRoute()
        {
            Click("LiveOnStage");
            yield return null;

            LevelMapPanel map = Object.FindAnyObjectByType<LevelMapPanel>();
            Assert.That(map, Is.Not.Null, "开始演出必须直接进入原冒险剧本的章节地图。");
            AssertInactiveOrMissing("PerformanceStagePanel");
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
            tactics = Object.FindAnyObjectByType<TacticsBattlePanel>();
            float battleTimeout = Time.realtimeSinceStartup + tactics.Battle.TimeLimitMilliseconds / 1000f + 30f;
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
        public IEnumerator StartPerformanceOnlyOpensAdventureWithoutSpendingStamina()
        {
            var app = Object.FindAnyObjectByType<ChoSirenApp>();
            var modelField = typeof(ChoSirenApp).GetField("model",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(modelField, Is.Not.Null);
            var model = (GameModel)modelField.GetValue(app);

            foreach (int stamina in new[] { 0, 120 })
            {
                model.Save.Stamina = stamina;
                int gold = model.Save.Gold;
                int diamonds = model.Save.Diamonds;
                int progress = model.Save.StoryProgress;
                AssertInactiveOrMissing("冒险剧本");
                float entranceTimeout = Time.realtimeSinceStartup + 15f;
                while (!IsInteractable("LiveOnStage") && Time.realtimeSinceStartup < entranceTimeout)
                    yield return null;
                Click("LiveOnStage");
                yield return null;
                RequireActiveObject("LevelMapPanel");
                AssertInactiveOrMissing("PerformanceStagePanel");
                AssertInactiveOrMissing("TacticsBattlePanel");
                Assert.That(model.Save.Stamina, Is.EqualTo(stamina), "浏览冒险剧本不能扣体力。");
                Assert.That(model.Save.Gold, Is.EqualTo(gold));
                Assert.That(model.Save.Diamonds, Is.EqualTo(diamonds));
                Assert.That(model.Save.StoryProgress, Is.EqualTo(progress));
                Click("Back");
                yield return null;
                RequireActiveObject("LiveOnStage");
                AssertInactiveOrMissing("冒险剧本");
                AssertInactiveOrMissing("LevelMapPanel");
            }
        }

        [UnityTest]
        public IEnumerator LegacyPerformancePanelSixTapButtonsStillShowResult()
        {
            // The legacy component is retained, but it is no longer the lobby CTA destination.
            PerformanceStagePanel.Open(RequireActiveObject("SafeArea").transform, new GameModel());
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
