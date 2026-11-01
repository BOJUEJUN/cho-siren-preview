using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ChoSiren.Panels;
using ChoSiren.Systems.Gacha;
using ChoSiren.Systems.Story;
using NUnit.Framework;
using ChoSiren.UI;
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

        [TestCase("开始挑战")]
        [TestCase("再次挑战")]
        public void ChallengeLabelFitsCenteredAuthoredFrame(string caption)
        {
            LevelMapPanel map = LevelMapPanel.Open(host.transform, CreateModel());
            RectTransform button = FindNamed<RectTransform>(map.transform, "StartChallenge");
            Text label = button.Find("Label").GetComponent<Text>();
            label.text = caption;
            Assert.That(button.rect.width, Is.GreaterThan(200f));
            Assert.That(button.rect.height, Is.GreaterThan(200f));
            // 授权整页把开始挑战爆发图烘进底板：命中矩形保持透明，自身不再绘制框图。
            Image hitImage = button.GetComponent<Image>();
            Assert.That(hitImage.color.a, Is.LessThan(0.05f),
                "开始挑战框图已烘进底板，命中区不应再叠原生框。");
            RectTransform labelRect = label.rectTransform;
            Assert.That(labelRect.anchoredPosition.x, Is.GreaterThanOrEqualTo(0f));
            Assert.That(-labelRect.anchoredPosition.y, Is.GreaterThanOrEqualTo(0f));
            Assert.That(labelRect.anchoredPosition.x + labelRect.rect.width,
                Is.LessThanOrEqualTo(button.rect.width));
            Assert.That(-labelRect.anchoredPosition.y + labelRect.rect.height,
                Is.LessThanOrEqualTo(button.rect.height));
            Assert.That(label.preferredWidth, Is.LessThan(labelRect.rect.width),
                "实际字形必须留在授权爆发图内侧，不能仅检查按钮矩形。");
            Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(labelRect.rect.height + 1f));
            Assert.That(button.GetComponent<BakedRegionMotion038>(), Is.Not.Null,
                "整幅爆发美术必须随按压整体缩放。");
            Assert.That(button.Find("BakedRegionBody/RegionArt"), Is.Not.Null);
        }

        [TestCase(0, 1)]
        [TestCase(80, 1)]
        [TestCase(81, 2)]
        [TestCase(83, 3)]
        [TestCase(85, 4)]
        [TestCase(87, 5)]
        [TestCase(89, 6)]
        [TestCase(91, 7)]
        [TestCase(93, 8)]
        [TestCase(95, 9)]
        [TestCase(97, 10)]
        [TestCase(100, 10)]
        public void StoryProgressMapsToDisplayStageAndSettlementStopsAtCompletion(int progress, int expectedStage)
        {
            Assert.That(LevelMapPanel.StoryStageForProgress(progress), Is.EqualTo(expectedStage));
            Assert.That(StoryBattlePanel.IsCurrentStoryStage(expectedStage, progress),
                Is.EqualTo(progress < GameModel.MaxStoryProgress));
        }

        [TestCase(0, 100, false)]
        [TestCase(1, 80, false)]
        [TestCase(1, 81, true)]
        [TestCase(4, 86, false)]
        [TestCase(4, 87, true)]
        [TestCase(10, 99, false)]
        [TestCase(10, 100, true)]
        [TestCase(11, 100, false)]
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
            Assert.That(levelNodeCount, Is.EqualTo(10), "第一章地图应显示 1-1 到 1-10。");

            FindButton(map.transform, "Level-1-2").onClick.Invoke();
            Assert.That(map.SelectedStage, Is.EqualTo(1));
            Transform toast = FindNamed<Transform>(map.transform, "LevelToast");
            Assert.That(toast.gameObject.activeSelf, Is.True);
            Assert.That(toast.GetComponentInChildren<Text>(true).text, Does.Contain("尚未解锁"));

            // The first-attempt stage scene is covered by its own story tests; skip it here.
            model.CompleteStory("stage-1-1");
            Button start = FindButton(map.transform, "StartChallenge");
            start.onClick.Invoke();
            start.onClick.Invoke();

            TacticsBattlePanel[] battlePanels = host.GetComponentsInChildren<TacticsBattlePanel>(true);
            Assert.That(battlePanels, Has.Length.EqualTo(1));
            Sprite approvedBoss = Resources.Load<Sprite>("Art/BattleUser/boss-throne-user-v1");
            Assert.That(approvedBoss, Is.Not.Null, "用户提供的副本 BOSS 应可由 Resources 加载。");
            Assert.That(FindNamed<Image>(battlePanels[0].transform, "BossPortrait").sprite,
                Is.SameAs(approvedBoss), "战斗中央舞台应使用用户提供的王座 BOSS。");
            for (int face = 1; face <= 6; face++)
                Assert.That(Resources.Load<Sprite>($"Art/BattleUser/dice-face-{face}-user-v1"), Is.Not.Null,
                    $"用户提供的骰子面 {face} 应可由 Resources 加载。");
            Assert.That(start.interactable, Is.False);
        }

        [Test]
        public void ChapterMapHidesProgressAndDifficultyAndConnectsRewardsAndTasksToSaveData()
        {
            GameModel model = CreateModel();
            for (int stage = 1; stage <= 10; stage++)
            {
                model.Save.ClearedStages.Add(new StageClear
                {
                    Id = $"stage-1-{stage}",
                    Stars = stage <= 5 ? 3 : 1,
                });
            }

            int diamondsBefore = model.Save.Diamonds;
            LevelMapPanel map = LevelMapPanel.Open(host.transform, model);

            string[] retainedAiResources =
            {
                "Art/LevelMapAI/stage-node-frame-ai-v1",
                "Art/LevelMapAI/chapter-reward-chest-ai-v1",
                "Art/LevelMapAI/chapter-action-frame-ai-v1",
            };
            for (int index = 0; index < retainedAiResources.Length; index++)
                Assert.That(Resources.Load<Sprite>(retainedAiResources[index]), Is.Not.Null,
                    $"章节地图保留素材未导入：{retainedAiResources[index]}");

            // 授权整页把台座/按钮美术烘进底板：命中区自身透明，整区切片负责展示与整体缩放。
            Transform firstNode = FindNamed<Transform>(map.transform, "Level-1-1");
            Assert.That(firstNode.GetComponent<Image>().color.a, Is.LessThan(0.05f),
                "关卡台座已烘进底板，命中区不应再叠原生框图。");
            Assert.That(firstNode.GetComponent<BakedRegionMotion038>(), Is.Not.Null,
                "台座美术必须随按压整体缩放。");
            Assert.That(firstNode.Find("BakedRegionBody/RegionArt"), Is.Not.Null);
            Assert.That(FindNamed<Image>(map.transform, "StartChallenge").color.a, Is.LessThan(0.05f));
            Assert.That(FindNamed<Transform>(map.transform, "StartChallenge")
                .GetComponent<BakedRegionMotion038>(), Is.Not.Null);
            Transform rewardsEntry = FindNamed<Transform>(map.transform, "ChapterRewards");
            Assert.That(rewardsEntry.GetComponent<BakedRegionMotion038>(), Is.Not.Null);
            Assert.That(map.GetComponentsInChildren<Text>(true).Any(text => text.text == "章节奖励"), Is.True,
                "章节奖励入口的实时文案必须保留。");
            Assert.That(map.GetComponentsInChildren<Transform>(true)
                .Any(item => item.name == "ChapterTasksIcon"), Is.False,
                "章节任务入口左侧不应再显示装饰按钮素材。 ");
            Assert.That(FindNamed<Text>(FindNamed<Transform>(map.transform, "Level-1-2"), "StageLabel").text,
                Is.EqualTo("1-2"));
            Assert.That(FindNamed<Text>(FindNamed<Transform>(map.transform, "Level-1-2"), "StatusLabel").text,
                Is.EqualTo("★★★"), "通关节点应显示该关最佳星数。 ");

            // 章节奖励/任务烘成底板左右双牌，替代旧的玻璃底栏。
            RectTransform rewardsPlaque = rewardsEntry.GetComponent<RectTransform>();
            RectTransform tasksPlaque = FindNamed<RectTransform>(map.transform, "ChapterTasks");
            Assert.That(-rewardsPlaque.anchoredPosition.y, Is.GreaterThan(1600f),
                "章节双入口必须落在底板底带内。");
            Assert.That(rewardsPlaque.anchoredPosition.x + rewardsPlaque.rect.width,
                Is.LessThanOrEqualTo(tasksPlaque.anchoredPosition.x),
                "章节奖励必须位于章节任务左侧。");
            Assert.That(map.GetComponentsInChildren<Text>(true).Any(text => text.text == "章节任务"), Is.True);
            RectTransform detail = FindNamed<RectTransform>(map.transform, "SelectedLevel");
            RectTransform drops = FindNamed<RectTransform>(map.transform, "StageDropPreview");
            RectTransform start = FindNamed<RectTransform>(map.transform, "StartChallenge");
            Assert.That(-drops.anchoredPosition.y + drops.rect.height,
                Is.LessThanOrEqualTo(detail.rect.height),
                "掉落预览必须留在关卡详情卡片内侧。");
            Assert.That(start.anchoredPosition.x,
                Is.GreaterThanOrEqualTo(detail.anchoredPosition.x + detail.rect.width),
                "开始挑战不得与关卡详情卡片横向重叠。");
            Image[] previewIcons = drops.GetComponentsInChildren<Image>().Where(image => image.name.StartsWith("DropIcon-")).ToArray();
            Assert.That(previewIcons.Length, Is.EqualTo(5));
            Assert.That(previewIcons.All(image => image.rectTransform.rect.width >= 64), Is.True,
                "奖励图标需至少64设计像素，不能退回32像素缩略图。");
            RectTransform backButton = FindNamed<RectTransform>(map.transform, "Back");
            Assert.That(backButton.rect.width, Is.GreaterThanOrEqualTo(80f),
                "返回命中区必须保留足够大的可点范围。");
            Assert.That(backButton.GetComponent<BakedRegionMotion038>(), Is.Not.Null);
            string[] hiddenControls = { "ChapterProgressAIFrame", "ChapterDifficulty", "StarMilestones" };
            Transform[] descendants = map.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < hiddenControls.Length; index++)
                Assert.That(descendants.Any(item => item.name == hiddenControls[index]), Is.False,
                    $"章节地图不应再创建界面节点：{hiddenControls[index]}");
            Assert.That(map.GetComponentsInChildren<Text>(true).Any(text => text.text == "章节进度"), Is.False);
            Assert.That(map.GetComponentsInChildren<Text>(true).Any(text => text.text == "难度"), Is.False);

            FindButton(map.transform, "ChapterRewards").onClick.Invoke();
            Assert.That(FindNamed<Transform>(map.transform, "ChapterRewardsModal"), Is.Not.Null);
            FindButton(map.transform, "ChapterStarReward-10").onClick.Invoke();
            Assert.That(model.Save.Diamonds, Is.EqualTo(diamondsBefore + 30),
                "领取 10 星章节奖励必须真实写入星钻余额。");
            Assert.That(model.Save.ClaimedChapterOneStarRewards, Does.Contain(10));

            FindButton(map.transform, "ChapterTasks").onClick.Invoke();
            Assert.That(FindNamed<Transform>(map.transform, "ChapterTasksModal"), Is.Not.Null);
            FindButton(map.transform, "ChapterTaskClaim-1").onClick.Invoke();
            Assert.That(model.Save.ClaimedChapterOneTasks, Does.Contain(GameModel.ChapterOneTaskClearStageThree));
        }

        [Test]
        public void LockedChapterNodesKeepTheirStageNumberAndFullFrameVisible()
        {
            GameModel model = CreateModel();
            LevelMapPanel map = LevelMapPanel.Open(host.transform, model);
            Transform lockedNode = FindNamed<Transform>(map.transform, "Level-1-2");

            Text stageNumber = FindNamed<Text>(lockedNode, "StageLabel");
            Assert.That(stageNumber.text, Is.EqualTo("1-2"));
            Assert.That(stageNumber.color.a, Is.GreaterThan(0.9f),
                "锁定节点的关卡号必须保持可辨认。 ");
            // 授权底板里锁定态由锁形图标表达，不再叠加文字条。
            Assert.That(FindNamed<Text>(lockedNode, "StatusLabel").text, Is.Empty);
            Assert.That(FindNamed<Image>(lockedNode, "LockIcon").enabled, Is.True,
                "锁定节点必须点亮锁形图标。");
            Assert.That(lockedNode.Find("BakedRegionBody/RegionArt"), Is.Not.Null,
                "锁定台座的完整烘焙帧必须通过区域切片保持可见。");
        }

        [Test]
        public void NonLobbyGameplayPagesKeepArtLegibleBehindFunctionalGlass()
        {
            GameModel model = CreateModel();

            LevelMapPanel map = LevelMapPanel.Open(host.transform, model);
            // 授权整页底板本身就是已压暗构图：它替代旧的渐变+夜城+轻纱三层叠加。
            Image mapBackground = FindNamed<Image>(map.transform, "MapReferenceBoard");
            Assert.That(mapBackground.sprite, Is.Not.Null, "应加载授权的章节地图底板。");
            Assert.That(mapBackground.preserveAspect, Is.True, "授权底板不能被竖屏拉伸。");
            Assert.That(mapBackground.raycastTarget, Is.False, "底板不能吞掉关卡节点点击。");
            Assert.That(FindNamed<Transform>(map.transform, "LevelMapInputBlocker"), Is.Not.Null,
                "底板之外需要独立的输入拦截层。");
            Assert.That(FindNamed<Transform>(map.transform, "SelectedLevel"), Is.Not.Null);
            AssertBright(FindNamed<Text>(FindNamed<Transform>(map.transform, "StartChallenge"), "Label"), "开始挑战");
            UnityEngine.Object.DestroyImmediate(map.gameObject);

            var script = new StoryScript { Id = "readability", Title = "可读性检查" };
            var choice = new StoryLine { Command = StoryCommand.Choice, Text = "选择下一步" };
            choice.Choices.Add(new StoryChoice { Text = "继续调查" });
            choice.Choices.Add(new StoryChoice { Text = "返回舞台" });
            script.Lines.Add(choice);
            StoryPanel story = StoryPanel.Open(host.transform, model, new StoryRunner(script), null);
            AssertReadabilityVeil(story.transform, "StoryReadabilityVeil");
            Image storyBackground = FindNamed<Image>(story.transform, "Background");
            Assert.That(storyBackground.raycastTarget, Is.False, "剧情背景不能吞掉对话和选项点击。");
            Assert.That(storyBackground.preserveAspect, Is.True, "剧情场景图不能被竖屏拉伸。");
            Assert.That(storyBackground.GetComponent<AspectRatioFitter>().aspectMode,
                Is.EqualTo(AspectRatioFitter.AspectMode.EnvelopeParent));
            Transform dialogueRoot = FindNamed<Transform>(story.transform, "DialogueBox");
            // 2026-10-20 owner pick C2: the reading surface is the torn-paper strip art (dark ink text).
            Assert.That(FindNamed<Image>(dialogueRoot, "DialoguePaperStrip").sprite, Is.Not.Null, "剧情对话应使用撕纸对话条。");
            Transform choiceRoot = FindNamed<Transform>(story.transform, "Choice-0");
            Assert.That(choiceRoot.GetComponent<Button>().interactable, Is.True);
            Assert.That(choiceRoot.Find("Body/SirenButtonFace").GetComponent<Image>().raycastTarget, Is.False,
                "选项纸边装饰不应拦截按钮输入。");
            AssertReadableAction(FindNamed<Transform>(story.transform, "Choice-0"), "剧情选项");
            UnityEngine.Object.DestroyImmediate(story.gameObject);

            PerformanceStagePanel performance = PerformanceStagePanel.Open(host.transform, model);
            AssertReadabilityVeil(performance.transform, "PerformanceReadabilityVeil");
            Image performanceBackground = FindNamed<Image>(performance.transform, "StageBackground");
            Assert.That(performanceBackground.raycastTarget, Is.False, "演出背景不能吞掉节拍点击。");
            Assert.That(performanceBackground.preserveAspect, Is.True, "演出背景不能被竖屏拉伸。");
            Assert.That(performanceBackground.GetComponent<AspectRatioFitter>().aspectMode,
                Is.EqualTo(AspectRatioFitter.AspectMode.EnvelopeParent));
            AssertGlass(FindNamed<Image>(performance.transform, "RoundBanner"), 0.72f, "演出回合提示");
            AssertGlass(FindNamed<Image>(performance.transform, "TimingPanel"), 0.75f, "演出节拍区");
            AssertGlass(FindNamed<Image>(performance.transform, "StageStats"), 0.72f, "演出统计区");
            AssertBright(FindNamed<Text>(FindNamed<Transform>(performance.transform, "PerformanceTap"), "Label"),
                "演出操作按钮");
        }

        [Test]
        public void SceneBackgroundFittersUseLoadedSpriteAspectRatios()
        {
            GameModel model = CreateModel();
            var script = new StoryScript { Id = "aspect-ratio", Title = "背景比例检查" };
            script.Lines.Add(new StoryLine { Command = StoryCommand.Say, Text = "测试台词" });
            StoryPanel story = StoryPanel.Open(host.transform, model, new StoryRunner(script), null);
            Image storyBackground = FindNamed<Image>(story.transform, "Background");
            AspectRatioFitter storyFitter = storyBackground.GetComponent<AspectRatioFitter>();
            Assert.That(storyFitter, Is.Not.Null);
            MethodInfo applyBackground = typeof(StoryPanel).GetMethod("ApplyBackgroundSprite",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(applyBackground, Is.Not.Null, "测试需要调用剧情背景的实际赋图路径。");
            var texture = new Texture2D(320, 180);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 320f, 180f), Vector2.one * 0.5f);

            try
            {
                applyBackground.Invoke(story, new object[] { sprite });

                Assert.That(storyBackground.sprite, Is.SameAs(sprite));
                Assert.That(storyFitter.aspectRatio,
                    Is.EqualTo(sprite.rect.width / sprite.rect.height).Within(0.0001f),
                    "剧情背景覆盖比例必须跟随已加载的场景图。");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(sprite);
                UnityEngine.Object.DestroyImmediate(texture);
            }

            UnityEngine.Object.DestroyImmediate(story.gameObject);
            PerformanceStagePanel performance = PerformanceStagePanel.Open(host.transform, model);
            Image performanceBackground = FindNamed<Image>(performance.transform, "StageBackground");
            AspectRatioFitter performanceFitter = performanceBackground.GetComponent<AspectRatioFitter>();
            Assert.That(performanceBackground.sprite, Is.Not.Null, "演出舞台必须加载背景图。");
            Assert.That(performanceFitter, Is.Not.Null);
            Assert.That(performanceFitter.aspectRatio,
                Is.EqualTo(performanceBackground.sprite.rect.width / performanceBackground.sprite.rect.height)
                    .Within(0.0001f),
                "演出背景覆盖比例必须跟随已加载的舞台图。");
        }

        [Test]
        public void TaskBoardUsesOneResponsiveGlassTaskSurfaceWithoutOverlap()
        {
            host.GetComponent<RectTransform>().sizeDelta = new Vector2(720f, 1536f);
            GameModel model = CreateModel();
            TaskBoardPanel panel = TaskBoardPanel.Open(host.transform, model, model);

            Assert.That(host.GetComponentsInChildren<TaskBoardPanel>(true), Has.Length.EqualTo(1));
            Image stageBackground = FindNamed<Image>(panel.transform, "TaskBoardStageBackground");
            Assert.That(stageBackground.sprite, Is.Not.Null,
                "任务面板应使用 AI 舞台背景，不应退回纯色渐变。");
            AspectRatioFitter backgroundCover = stageBackground.GetComponent<AspectRatioFitter>();
            Assert.That(backgroundCover, Is.Not.Null);
            Assert.That(backgroundCover.aspectMode, Is.EqualTo(AspectRatioFitter.AspectMode.EnvelopeParent),
                "AI 舞台背景必须等比覆盖竖屏，不能被拉伸。");
            RectTransform viewport = FindNamed<RectTransform>(panel.transform, "TaskList");
            Assert.That(viewport.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(viewport.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(viewport.rect.height, Is.GreaterThan(300f), "页头扩大后任务仍应有可用阅读空间。");
            Assert.That(viewport.offsetMin.y, Is.EqualTo(52f).Within(0.01f));

            RectTransform checkIn = FindNamed<RectTransform>(panel.transform, "CheckInTaskChip");
            float checkInBottom = -checkIn.anchoredPosition.y + checkIn.rect.height;
            float listTop = -viewport.offsetMax.y;
            Assert.That(listTop - checkInBottom, Is.GreaterThanOrEqualTo(10f),
                "签到任务条与第一张任务卡之间必须留出稳定间距。");
            Assert.That(checkIn.GetComponentsInChildren<Text>(true).Any(text => text.text.StartsWith("签到", StringComparison.Ordinal)), Is.True);

            Image[] taskCards = panel.GetComponentsInChildren<Image>(true)
                .Where(image => image.name.StartsWith("Task-daily-", StringComparison.Ordinal))
                .ToArray();
            Assert.That(taskCards.Length, Is.GreaterThanOrEqualTo(5));
            for (int index = 0; index < taskCards.Length; index++)
            {
                Assert.That(taskCards[index].sprite, Is.Not.Null, "静态任务卡应有纸边阅读底板。");
                Assert.That(taskCards[index].raycastTarget, Is.False, "卡片装饰不应吞掉领取和前往按钮。");
                RectTransform cardRect = taskCards[index].rectTransform;
                Assert.That(cardRect.rect.width, Is.EqualTo(680f).Within(0.01f));
                Assert.That(cardRect.anchoredPosition.x, Is.Zero.Within(0.01f));
            }

            Assert.That(TaskBoardPanel.ListContentHeight(0), Is.Zero);
            Assert.That(TaskBoardPanel.ListContentHeight(3), Is.GreaterThan(TaskBoardPanel.ListContentHeight(2)),
                "阅读字号增大后，内容高度仍须随任务数增长以便滚到最后一项。");
        }

        [Test]
        public void EmbeddedAuditionKeepsShellAndBuildsTwoPoolCandidateBrowser()
        {
            RectTransform hostRect = host.GetComponent<RectTransform>();
            hostRect.sizeDelta = new Vector2(720f, 1536f);
            GameObject topBar = new GameObject("TopBar", typeof(RectTransform), typeof(Image));
            topBar.transform.SetParent(host.transform, false);
            GameObject content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(host.transform, false);
            content.GetComponent<RectTransform>().sizeDelta = new Vector2(720f, 1290f);
            GameObject navigation = new GameObject("BottomNavigation", typeof(RectTransform), typeof(Image));
            navigation.transform.SetParent(host.transform, false);

            GameModel model = CreateModel();
            GachaPanel panel = GachaPanel.OpenEmbedded(content.transform, model, model);

            Assert.That(panel.transform.parent, Is.EqualTo(content.transform));
            Assert.That(topBar.activeSelf, Is.True);
            Assert.That(navigation.activeSelf, Is.True);
            Assert.That(panel.GetComponentsInChildren<Transform>(true).Any(item => item.name == "Back"), Is.False,
                "嵌入选秀页不应重复创建返回按钮。");
            Assert.That(panel.GetComponentsInChildren<Transform>(true).Any(item => item.name == "BalanceDiamond"),
                Is.False, "钻石余额应由主界面顶栏统一显示。");
            Assert.That(panel.GetComponentsInChildren<Transform>(true).Any(item => item.name == "BalanceGold"),
                Is.False, "星光币余额应由主界面顶栏统一显示。");
            Assert.That(panel.GetComponentsInChildren<Text>(true).Any(label =>
                    label.text == "SSR" || label.text == "SR" || label.text == "R"),
                Is.False, "选秀候选页不应再显示抽卡稀有度。");

            Assert.That(FindNamed<Transform>(panel.transform, "InterviewPool-0"), Is.Not.Null);
            Assert.That(FindNamed<Transform>(panel.transform, "InterviewPool-1"), Is.Not.Null);
            // 2026-11-01 visual-b: the refresh line is a refresh icon + 「每日 18:00」.
            Assert.That(FindNamed<Text>(panel.transform, "InterviewRefresh").text,
                Does.Contain("18:00"));
            Assert.That(panel.InterviewPoolIndex, Is.Zero);

            RectTransform card = FindNamed<RectTransform>(panel.transform, "CandidateCard");
            RectTransform actions = FindNamed<RectTransform>(panel.transform, "InterviewActions");
            // 斜卡使用中心 pivot：真实底边 = -anchored.y + 高·(1-pivot.y)，不能用旧枢轴公式。
            Assert.That(-card.anchoredPosition.y + card.rect.height * (1f - card.pivot.y),
                Is.LessThanOrEqualTo(1290f));
            // 授权整页把签约区烘进 y1400–1668 的契约构图；透明命中区对齐该烘区，
            // 不得再套用旧内容面板 1290 上限。
            Assert.That(-actions.anchoredPosition.y, Is.GreaterThanOrEqualTo(1380f),
                "签约操作区必须落在授权契约带内。");
            Assert.That(-actions.anchoredPosition.y + actions.rect.height, Is.LessThanOrEqualTo(1670f),
                "签约操作区不得越出授权契约带。");
            Assert.That(FindNamed<Transform>(panel.transform, "PreviousCandidate"), Is.Not.Null);
            Assert.That(FindNamed<Transform>(panel.transform, "NextCandidate"), Is.Not.Null);

            Image portrait = FindNamed<Image>(panel.transform, "CandidatePortrait");
            Assert.That(portrait.sprite, Is.Not.Null, "候选卡必须使用现有本地角色立绘。");
            Assert.That(portrait.preserveAspect, Is.True, "候选立绘不能被拉伸。");
            Assert.That(FindNamed<Text>(panel.transform, "CandidateName").text, Is.Not.Empty);
            // 2026-11-01 visual-b: identity = race badge + role chip + battle-position chip (no sentence).
            Assert.That(FindNamed<Image>(panel.transform, "CandidateRace").sprite, Is.Not.Null, "种族用徽章表达。");
            Assert.That(FindNamed<Text>(panel.transform, "CandidateCareer").text,
                Does.Match("^(主唱|主舞|Rapper|门面)$"), "候选资料必须使用新版四种偶像职业。");
            Assert.That(FindNamed<Text>(panel.transform, "CandidatePosition").text,
                Does.Match("治疗|增益|输出|共鸣|破甲|护盾|毒伤|斩杀"), "候选资料必须在职能之后给出阵容定位。");
            Assert.That(FindNamed<Transform>(panel.transform, "CandidatePositionChip").Find("Icon"), Is.Not.Null,
                "阵容定位带图标。");

            string[] statKeys = { "Vocal", "Rhythm", "Presence", "Resonance", "Charm" };
            string[] statNames = { "声波", "说唱", "舞蹈", "名气", "颜值" };
            for (int index = 0; index < statKeys.Length; index++)
            {
                Assert.That(FindNamed<Text>(panel.transform, "StatLabel-" + statKeys[index]).text,
                    Is.EqualTo(statNames[index]), "五维标签必须使用真实舞台属性名。");
                int value = int.Parse(FindNamed<Text>(panel.transform, "StatValue-" + statKeys[index]).text);
                Assert.That(value, Is.InRange(30, 68), "线上池五维读数必须落在渠道区间内。");
            }
            Assert.That(panel.GetComponentsInChildren<Transform>(true)
                    .Count(item => item.name == "StrongestStat"), Is.EqualTo(1),
                "五维只突出一个最强项，避免所有属性同时抢视觉焦点。");
            Assert.That(panel.GetComponentsInChildren<Transform>(true).Any(item => item.name == "CandidateTrait"), Is.False,
                "舞台特质整句已去掉：最强项由 ★ 标出。");

            Text signingPrice = FindNamed<Text>(panel.transform, "SigningPrice");
            int onlinePrice = int.Parse(signingPrice.text.Replace(",", string.Empty));
            // v0.3.3: 签约改用星钻(稀有货币),报价从金币 700-1000 压到星钻 60-200
            Assert.That(onlinePrice, Is.InRange(60, 200));
            Transform viewInterview = FindNamed<Transform>(panel.transform, "ViewInterview");
            Assert.That(FindNamed<Text>(viewInterview, "Label").text, Is.EqualTo("查看视频面试"));
            Assert.That(FindButton(panel.transform, "SignCandidate"), Is.Not.Null);

            FindButton(panel.transform, "InterviewPool-1").onClick.Invoke();
            Assert.That(panel.InterviewPoolIndex, Is.EqualTo(1), "线上与线下必须是两个独立候选池。");
        }

        [Test]
        public void CostumeBannerShowsItsOwnTicketBalance()
        {
            GameModel model = CreateModel();
            model.Save.RecruitTickets = 7;
            model.Save.CostumeTickets = 23;
            GachaPanel panel = GachaPanel.Open(host.transform, model, model);

            FindButton(panel.transform, "BannerTab-costume-neon-night").onClick.Invoke();

            Text balance = FindNamed<Text>(FindNamed<Transform>(panel.transform, "BalanceTicket"), "Value");
            Assert.That(balance.text, Is.EqualTo("23"),
                "服装卡池必须显示服装券余额，不能继续显示招募券余额。");
        }

        [Test]
        public void StandaloneSigningUiNeverExposesInternalRarityCodes()
        {
            GameModel model = CreateModel();
            GachaPanel panel = GachaPanel.Open(host.transform, model, model);

            string[] labels = panel.GetComponentsInChildren<Text>(true)
                .Select(label => label.text ?? string.Empty)
                .ToArray();
            Assert.That(labels.Any(label => System.Text.RegularExpressions.Regex.IsMatch(label,
                    @"(?<![A-Za-z])(SSR|SR|R)(?![A-Za-z])")), Is.False,
                "签约页应使用候选与邀约语言，不应显示内部稀有度代码。");
            Assert.That(labels, Has.Some.Contains("特别邀约"));
            Assert.That(labels, Has.Some.Contains("重点邀约"));
        }

        [Test]
        public void CostumeAndAccessoryResultsAlwaysHavePortraits()
        {
            GameModel model = CreateModel();
            GachaPanel panel = GachaPanel.Open(host.transform, model, model);
            GachaBannerDefinition banner = model.Banners.Single(item => item.Id == "costume-neon-night");
            var results = new List<GachaPullResult>
            {
                new GachaPullResult
                {
                    ItemId = "costume-xingli-neon-night",
                    Rarity = GachaRarity.Ssr,
                    IsNew = true,
                },
                new GachaPullResult
                {
                    ItemId = "accessory-neon-earring",
                    Rarity = GachaRarity.R,
                    IsNew = true,
                },
            };
            MethodInfo showResults = typeof(GachaPanel).GetMethod("ShowResults",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(showResults, Is.Not.Null, "测试需要调用实际结果填充路径。");

            showResults.Invoke(panel, new object[] { banner, results });

            Assert.That(FindNamed<Text>(panel.transform, "ResultSummary").text,
                Does.Not.Match(@"(?<![A-Za-z])(SSR|SR|R)(?![A-Za-z])"));

            for (int index = 0; index < results.Count; index++)
            {
                Transform result = FindNamed<Transform>(panel.transform, "Result-" + index);
                Image portrait = FindNamed<Image>(result, "Portrait");
                Assert.That(portrait.sprite, Is.Not.Null,
                    $"{results[index].ItemId} 的抽卡结果不能留下空立绘。");
                Assert.That(portrait.enabled, Is.True,
                    $"{results[index].ItemId} 的抽卡结果立绘不能被禁用。");
                Assert.That(result.GetComponentsInChildren<Text>(true).Any(text =>
                        text.text.Contains("主唱") || text.text.Contains("主舞") || text.text.Contains("Rapper") || text.text.Contains("门面") ||
                        text.text.Contains("舞台饰品")),
                    Is.True, "结果卡应优先显示候选职能或物品定位，而不是稀有度。");
            }
        }

        [Test]
        public void BattleLocksEverySkillDuringTurnFeedback()
        {
            StoryBattlePanel panel = StoryBattlePanel.Open(host.transform, CreateModel(), 3);
            AssertContainsNoLatinText(panel.transform);
            Assert.That(panel.GetComponentsInChildren<Text>(true).Any(text => text.text.Contains("7-")), Is.False,
                "第一章任何遗留战斗界面都不能再显示旧的 7-x 编号。");
            Image legacyBackground = FindNamed<Image>(panel.transform, "BattleBackground");
            Assert.That(legacyBackground.preserveAspect, Is.True);
            Assert.That(legacyBackground.GetComponent<AspectRatioFitter>().aspectMode,
                Is.EqualTo(AspectRatioFitter.AspectMode.EnvelopeParent));
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
                // These are authored idol-career names, not untranslated debug text.
                value = value.Replace("Rapper", string.Empty);
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

        private static void AssertReadabilityVeil(Transform root, string name)
        {
            Image veil = FindNamed<Image>(root, name);
            Assert.That(veil.raycastTarget, Is.False, $"{name} 只能压暗背景，不能接收点击。");
            Assert.That(veil.color.a, Is.InRange(0.15f, 0.35f), $"{name} 应保持轻量，不能盖住角色和场景。");
        }

        private static void AssertGlass(Image image, float maxAlpha, string description)
        {
            Assert.That(image.color.a, Is.InRange(0.55f, maxAlpha),
                $"{description} 应使用高对比半透明玻璃，不能变成不透明纯色卡。");
        }

        private static void AssertBright(Text text, string description)
        {
            Assert.That(text.color.a, Is.GreaterThanOrEqualTo(0.80f), $"{description} 文字透明度不足。");
            Assert.That(Mathf.Max(text.color.r, text.color.g, text.color.b), Is.GreaterThanOrEqualTo(0.80f),
                $"{description} 文字亮度不足。");
        }

        /// <summary>Unified design: ink on the green primary sticker, bright text on every dark one.</summary>
        private static void AssertReadableAction(Transform action, string description)
        {
            Text label = FindNamed<Text>(action, "Label");
            Image face = action.GetComponentsInChildren<Image>(true).FirstOrDefault(i => i.name == "SirenButtonFace");
            if (face != null && face.sprite == SirenStickerUi.PrimarySprite)
            {
                Assert.That(label.color.a, Is.GreaterThanOrEqualTo(0.80f), $"{description} 文字透明度不足。");
                Assert.That(Mathf.Max(label.color.r, label.color.g, label.color.b), Is.LessThanOrEqualTo(0.20f),
                    $"{description} 在绿色主按钮上必须是深色字。");
            }
            else AssertBright(label, description);
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
