using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ChoSiren.Panels;
using ChoSiren.Systems.Gacha;
using ChoSiren.Systems.Story;
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
        public void ChapterMapUsesAiControlsAndConnectsDifficultyRewardsAndTasksToSaveData()
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

            string[] aiResources =
            {
                "Art/LevelMapAI/stage-node-frame-ai-v1",
                "Art/LevelMapAI/chapter-progress-ring-ai-v1",
                "Art/LevelMapAI/chapter-reward-chest-ai-v1",
                "Art/LevelMapAI/chapter-action-frame-ai-v1",
            };
            for (int index = 0; index < aiResources.Length; index++)
                Assert.That(Resources.Load<Sprite>(aiResources[index]), Is.Not.Null,
                    $"章节地图 AI 素材未导入：{aiResources[index]}");

            Assert.That(FindNamed<Image>(map.transform, "Level-1-1").sprite,
                Is.SameAs(Resources.Load<Sprite>(aiResources[0])));
            Assert.That(FindNamed<Image>(map.transform, "ChapterProgressAIFrame").sprite,
                Is.SameAs(Resources.Load<Sprite>(aiResources[1])));
            Assert.That(FindNamed<Image>(map.transform, "ChapterRewards").sprite,
                Is.SameAs(Resources.Load<Sprite>(aiResources[2])));
            Assert.That(FindNamed<Image>(map.transform, "ChapterDifficulty").sprite,
                Is.SameAs(Resources.Load<Sprite>(aiResources[3])));

            Assert.That(FindNamed<Transform>(map.transform, "ChapterControlDock"), Is.Not.Null);
            Button difficultyEntry = FindButton(map.transform, "ChapterDifficulty");
            Assert.That(difficultyEntry.GetComponent<UnityEngine.EventSystems.EventTrigger>(), Is.Not.Null,
                "章节入口应有鼠标悬停与按压缩放反馈。");
            Assert.That(difficultyEntry.colors.highlightedColor,
                Is.Not.EqualTo(difficultyEntry.colors.normalColor), "悬停颜色不能与常态完全相同。");
            Assert.That(difficultyEntry.colors.pressedColor,
                Is.Not.EqualTo(difficultyEntry.colors.normalColor), "按压颜色不能与常态完全相同。");
            Assert.That(map.GetComponentsInChildren<Text>(true).Any(text => text.text.Contains("20/30 星")), Is.True,
                "章节进度必须显示真实累计星数。");

            difficultyEntry.onClick.Invoke();
            Assert.That(FindNamed<Transform>(map.transform, "ChapterDifficultyModal"), Is.Not.Null);
            FindButton(map.transform, "Difficulty-Easy").onClick.Invoke();
            Assert.That(model.ChapterOneDifficulty, Is.EqualTo(BattleDifficulty.Easy));

            FindButton(map.transform, "ChapterRewards").onClick.Invoke();
            Assert.That(FindNamed<Transform>(map.transform, "ChapterRewardsModal"), Is.Not.Null);
            FindButton(map.transform, "ChapterStarReward-10").onClick.Invoke();
            Assert.That(model.Save.Diamonds, Is.EqualTo(diamondsBefore + 100),
                "领取 10 星章节奖励必须真实写入星钻余额。");
            Assert.That(model.Save.ClaimedChapterOneStarRewards, Does.Contain(10));

            FindButton(map.transform, "ChapterTasks").onClick.Invoke();
            Assert.That(FindNamed<Transform>(map.transform, "ChapterTasksModal"), Is.Not.Null);
            FindButton(map.transform, "ChapterTaskClaim-1").onClick.Invoke();
            Assert.That(model.Save.ClaimedChapterOneTasks, Does.Contain(GameModel.ChapterOneTaskClearStageThree));
        }

        [Test]
        public void NonLobbyGameplayPagesKeepArtLegibleBehindFunctionalGlass()
        {
            GameModel model = CreateModel();

            LevelMapPanel map = LevelMapPanel.Open(host.transform, model);
            AssertReadabilityVeil(map.transform, "LevelMapReadabilityVeil");
            Image mapBackground = FindNamed<Image>(map.transform, "NightGradient");
            Assert.That(mapBackground.raycastTarget, Is.False, "关卡背景不能吞掉功能区点击。");
            Image userCity = FindNamed<Image>(map.transform, "Chapter01UserCityBackground");
            Assert.That(userCity.sprite, Is.Not.Null, "应加载用户确认的第一章夜城底图。");
            Assert.That(userCity.preserveAspect, Is.True, "第一章夜城底图不能被竖屏拉伸。");
            Assert.That(userCity.raycastTarget, Is.False, "第一章夜城底图不能吞掉关卡节点点击。");
            AspectRatioFitter cityCover = userCity.GetComponent<AspectRatioFitter>();
            Assert.That(cityCover, Is.Not.Null);
            Assert.That(cityCover.aspectMode, Is.EqualTo(AspectRatioFitter.AspectMode.EnvelopeParent));
            Assert.That(cityCover.aspectRatio,
                Is.EqualTo(userCity.sprite.rect.width / userCity.sprite.rect.height).Within(0.001f));
            AssertGlass(FindNamed<Image>(map.transform, "SelectedLevel"), 0.72f, "关卡详情");
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
            AssertGlass(FindNamed<Image>(story.transform, "DialogueBox"), 0.78f, "剧情对话框");
            AssertGlass(FindNamed<Image>(story.transform, "Choice-0"), 0.78f, "剧情选项");
            AssertBright(FindNamed<Text>(FindNamed<Transform>(story.transform, "Choice-0"), "Label"), "剧情选项");
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
            Assert.That(viewport.offsetMax.y, Is.EqualTo(-238f).Within(0.01f));
            Assert.That(viewport.offsetMin.y, Is.EqualTo(52f).Within(0.01f));

            RectTransform checkIn = FindNamed<RectTransform>(panel.transform, "CheckInTaskChip");
            float checkInBottom = -checkIn.anchoredPosition.y + checkIn.rect.height;
            float listTop = -viewport.offsetMax.y;
            Assert.That(listTop - checkInBottom, Is.GreaterThanOrEqualTo(10f),
                "签到任务条与第一张任务卡之间必须留出稳定间距。");
            Assert.That(checkIn.GetComponentInChildren<Text>(true).text, Does.StartWith("签到任务"));

            Image[] taskCards = panel.GetComponentsInChildren<Image>(true)
                .Where(image => image.name.StartsWith("Task-daily-", StringComparison.Ordinal))
                .ToArray();
            Assert.That(taskCards.Length, Is.GreaterThanOrEqualTo(5));
            for (int index = 0; index < taskCards.Length; index++)
            {
                Assert.That(taskCards[index].color.a, Is.LessThanOrEqualTo(0.55f),
                    "任务卡应保持轻量玻璃透明度，不能变成大块纯色卡片。");
                RectTransform cardRect = taskCards[index].rectTransform;
                Assert.That(cardRect.rect.width, Is.EqualTo(680f).Within(0.01f));
                Assert.That(cardRect.anchoredPosition.x, Is.Zero.Within(0.01f));
            }

            Assert.That(TaskBoardPanel.ListContentHeight(0), Is.Zero);
            Assert.That(TaskBoardPanel.ListContentHeight(3), Is.EqualTo(420f).Within(0.01f));
        }

        [Test]
        public void EmbeddedGachaKeepsShellVisibleAndUsesThreeDistinctPortraits()
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
                Is.False, "金币余额应由主界面顶栏统一显示。");
            Assert.That(FindNamed<Transform>(panel.transform, "BalanceTicket"), Is.Not.Null,
                "招募券仍需留在签约操作区。");

            Image calmStage = FindNamed<Image>(panel.transform, "ImmersiveStage");
            Assert.That(calmStage.sprite, Is.Not.Null, "嵌入选秀页必须加载低干扰 AI 舞台底图。");
            Assert.That(calmStage.sprite.texture.name, Does.Contain("gacha-calm-stage-bg-ai-v2-20260903"),
                "嵌入选秀页应优先使用低干扰背景，旧底图只能作为回退。");
            Assert.That(calmStage.preserveAspect, Is.True, "选秀舞台底图不能被拉伸。");
            AspectRatioFitter stageCover = calmStage.GetComponent<AspectRatioFitter>();
            Assert.That(stageCover, Is.Not.Null);
            Assert.That(stageCover.aspectMode, Is.EqualTo(AspectRatioFitter.AspectMode.EnvelopeParent),
                "选秀舞台底图必须等比覆盖内容区。");

            string[] glassNames =
            {
                "HeroCaption", "RateBoard", "PityBoard", "BalanceTicket",
                "GachaDetails", "PullOne", "PullTen",
            };
            Transform guaranteeChip = FindNamed<Transform>(panel.transform, "GuaranteeChip");
            for (int index = 0; index < glassNames.Length; index++)
            {
                Transform glass = FindNamed<Transform>(panel.transform, glassNames[index]);
                Image glassImage = glass.GetComponent<Image>();
                Assert.That(glassImage.color.a, Is.LessThanOrEqualTo(0.60f),
                    $"{glassNames[index]} 应使用低透明深蓝玻璃，不能成为高饱和纯色块。");
                Text[] labels = glass.GetComponentsInChildren<Text>(true);
                for (int labelIndex = 0; labelIndex < labels.Length; labelIndex++)
                {
                    // The gold guarantee badge intentionally uses dark lettering on a bright chip.
                    if (labels[labelIndex].transform.IsChildOf(guaranteeChip)) continue;
                    Color textColor = labels[labelIndex].color;
                    Assert.That(textColor.a, Is.GreaterThanOrEqualTo(0.75f),
                        $"{glassNames[index]} 内文字透明度过低。\n");
                    Assert.That(Mathf.Max(textColor.r, textColor.g, textColor.b), Is.GreaterThanOrEqualTo(0.70f),
                        $"{glassNames[index]} 内文字亮度不足。\n");
                }
            }

            string[] bannerIds = { "debut-xingli", "standard-signing", "costume-neon-night" };
            Texture[] portraitTextures = new Texture[bannerIds.Length];
            for (int index = 0; index < bannerIds.Length; index++)
            {
                FindButton(panel.transform, "BannerTab-" + bannerIds[index]).onClick.Invoke();
                Image portrait = FindNamed<Image>(panel.transform, "FeaturedPortrait");
                Assert.That(portrait.preserveAspect, Is.True, "角色立绘不能拉伸。");
                Assert.That(portrait.sprite, Is.Not.Null, $"卡池 {bannerIds[index]} 缺少本地角色素材。");
                portraitTextures[index] = portrait.sprite.texture;
            }

            Assert.That(portraitTextures.Distinct().Count(), Is.EqualTo(3),
                "初登场、常驻和霓虹服装必须展示三个不同的本地角色素材。");

            RectTransform pullTen = FindNamed<RectTransform>(panel.transform, "PullTen");
            Text pullTenTitle = FindNamed<Text>(pullTen, "Label");
            RectTransform pullTenCost = FindNamed<RectTransform>(pullTen, "PullTenCost");
            float titleBottom = -pullTenTitle.rectTransform.anchoredPosition.y + pullTenTitle.rectTransform.rect.height;
            float costTop = -pullTenCost.anchoredPosition.y;
            Assert.That(costTop - titleBottom, Is.GreaterThanOrEqualTo(4f), "十连标题与价格文字不能重叠。");
            Assert.That(-pullTen.anchoredPosition.y + pullTen.rect.height, Is.LessThanOrEqualTo(1290f),
                "签约按钮必须保持在竖屏内容区内，不能遮挡底部导航。");
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

            for (int index = 0; index < results.Count; index++)
            {
                Transform result = FindNamed<Transform>(panel.transform, "Result-" + index);
                Image portrait = FindNamed<Image>(result, "Portrait");
                Assert.That(portrait.sprite, Is.Not.Null,
                    $"{results[index].ItemId} 的抽卡结果不能留下空立绘。");
                Assert.That(portrait.enabled, Is.True,
                    $"{results[index].ItemId} 的抽卡结果立绘不能被禁用。");
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
