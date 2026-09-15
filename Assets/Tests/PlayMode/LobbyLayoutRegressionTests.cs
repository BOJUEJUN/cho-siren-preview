using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ChoSiren.Tests
{
    /// <summary>
    /// Coordinate-level regression coverage for the portrait lobby composition.
    /// These checks intentionally avoid screenshots and text preferred-size metrics so the
    /// results do not depend on GPU output, DPI, or the font installed on the test machine.
    /// </summary>
    public sealed class LobbyLayoutRegressionTests
    {
        private const string SaveKey = "ChoSiren.Save.v1";
        private const float PositionTolerance = 0.5f;

        // 0.3.8 bottom destinations measured from the visible icon/frame/Chinese-label
        // groups in lobby-home-base-038.png, converted from 821x1739 to 720x1536.
        // Keep these non-uniform x positions: the approved artwork is not an equal-column grid.
        private static readonly Rect[] LobbyNavHitBounds038 =
        {
            new Rect(18f, 1328f, 132f, 143f),
            new Rect(161f, 1343f, 118f, 128f),
            new Rect(280f, 1337f, 142f, 149f),
            new Rect(424f, 1343f, 125f, 128f),
            new Rect(554f, 1326f, 147f, 148f),
        };

        // Visible sprite placement is calibrated separately from input. These offsets
        // reverse the drift observed in the deployed 720x1536 capture while preserving
        // each source asset's measured size and the larger touch-friendly hit rectangle.
        private static readonly Rect[] LobbyNavVisualBounds038 =
        {
            new Rect(18.375f, 1325.225f, 132f, 143f),
            new Rect(153.92f, 1337.81f, 118f, 128f),
            new Rect(276.55f, 1319.87f, 142f, 149f),
            new Rect(417.55f, 1337.375f, 125f, 128f),
            new Rect(528.815f, 1328.415f, 147f, 148f),
        };

        private static readonly float[] LobbyNavLabelCenters038 =
        {
            84.63f, 212.67f, 347.28f, 479.27f, 602.05f,
        };

        private const float LobbyNavUnderlineBaseline038 = 1473.09f;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
            DestroyAll<ChoSirenApp>();
            DestroyAll<EventSystem>();
            yield return null;

            new GameObject("CHO-SIREN Lobby Layout App").AddComponent<ChoSirenApp>();
            yield return null;

            Assert.That(Object.FindAnyObjectByType<ChoSirenApp>(), Is.Not.Null,
                "The app did not bootstrap for the lobby layout regression test.");
            RequireRect("LobbyCards");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            DestroyAll<ChoSirenApp>();
            DestroyAll<EventSystem>();
            yield return null;
            PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
        }

        [UnityTest]
        public IEnumerator CanvasUsesApprovedPortraitReferenceResolution()
        {
            CanvasScaler scaler = Object.FindAnyObjectByType<CanvasScaler>();
            Assert.That(scaler, Is.Not.Null, "The runtime UI requires a CanvasScaler.");
            Assert.That(scaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
            Assert.That(scaler.referenceResolution.x, Is.EqualTo(720f).Within(PositionTolerance),
                "The approved portrait design width is 720 pixels.");
            Assert.That(scaler.referenceResolution.y, Is.EqualTo(1536f).Within(PositionTolerance),
                "The approved portrait design height is 1536 pixels.");
            Assert.That(scaler.referenceResolution.y, Is.GreaterThan(scaler.referenceResolution.x),
                "The lobby must remain a portrait composition.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator TeamUsesStellarFormationStageInsteadOfSolidCards()
        {
            RequireButtonRect("Nav-team").GetComponent<Button>().onClick.Invoke();
            yield return null;
            RectTransform board = RequireRect("TeamReferenceBoard038");
            Image image = board.GetComponent<Image>();
            Assert.That(image.sprite, Is.EqualTo(Resources.Load<Sprite>("Art/Reference038/team-board-038")));
            Assert.That(image.preserveAspect, Is.True);
            Assert.That(board.localScale.x, Is.EqualTo(board.localScale.y).Within(.001f));
            var state = new GameModel();
            for (int slot = 0; slot < state.Save.Team.Count; slot++)
            {
                RectTransform orbit = RequireButtonRect($"TeamOrbit-{slot}");
                Assert.That(orbit.GetComponent<Image>().color.a, Is.LessThanOrEqualTo(.08f));
                Image portrait = RequireRect($"TeamCharacter-{slot}").GetComponent<Image>();
                Assert.That(portrait.sprite, Is.EqualTo(Resources.Load<Sprite>(GameModel.Members[state.Save.Team[slot]].ResourcePath)));
                Assert.That(portrait.preserveAspect, Is.True);
                Assert.That(portrait.raycastTarget, Is.False);
            }
            Assert.That(RequireRect("TeamPowerValue").GetComponent<Text>().text, Is.EqualTo(state.TeamPower.ToString("N0")));
            RectTransform synergy = RequireRect("TeamSynergy");
            for (int i = 0; i < MemberCareers.All.Count; i++)
                AssertContained(synergy, RequireRect("CareerStatus-" + i), "职业状态");
            RequireButtonRect("ChangeLeader");
            RequireButtonRect("AutoTeam");
        }

        [UnityTest]
        public IEnumerator MemberAndAccessoryPagesUseCalmAspectSafeBackgrounds()
        {
            foreach (string screen in new[] { "members", "accessory" })
            {
                RequireButtonRect("Nav-" + screen).GetComponent<Button>().onClick.Invoke();
                yield return null;
                RectTransform board = RequireRect(screen == "members" ? "MembersReferenceBoard038" : "EquipmentReferenceBoard038");
                Image image = board.GetComponent<Image>();
                Assert.That(image.sprite, Is.Not.Null);
                Assert.That(image.preserveAspect, Is.True, "完整参考背板不可拉伸或裁掉内容。");
                Assert.That(board.localScale.x, Is.EqualTo(board.localScale.y).Within(.001f));
            }
            Assert.That(RequireRect("EquipmentSelectedArt").GetComponent<Image>().preserveAspect, Is.True);
            Assert.That(RequireRect("EquipmentBody").GetComponentsInChildren<Button>().Count(b => b.name.StartsWith("Accessory-")), Is.EqualTo(12));
            Assert.That(GameObject.Find("AccessoryPreviewArt"), Is.Null);
            Assert.That(RequireRect("Content").GetComponentsInChildren<Text>().Select(t => t.text),
                Does.Not.Contain("饰品与设置"));
        }

        [UnityTest]
        public IEnumerator FaceCareerFilterUsesLatestDocumentAndShowsOnlyMatchingMembers()
        {
            RequireButtonRect("Nav-members").GetComponent<Button>().onClick.Invoke();
            yield return null;
            for (int index = 0; index < 4; index++)
            {
                RequireButtonRect("MemberRoleFilter").GetComponent<Button>().onClick.Invoke();
                yield return null;
            }
            string filterLabel = RequireRect("MemberRoleFilter").GetComponentInChildren<Text>().text;
            Assert.That(filterLabel, Is.EqualTo("职业：门面"));
            var cards = RequireRect("Content").GetComponentsInChildren<Button>(true)
                .Where(button => button.name.StartsWith("Member-")).ToArray();
            Assert.That(cards, Is.Not.Empty);
            foreach (Button card in cards)
            {
                Assert.That(card.transform.Find("LockedSilhouette"), Is.Null,
                    "职业筛选不得再筛出未获得剪影卡。");
                string id = card.name.Substring("Member-".Length);
                Assert.That(GameModel.Members.Single(member => member.Id == id).Career, Is.EqualTo("门面"));
                string[] labels = card.GetComponentsInChildren<Text>(true).Select(label => label.text).ToArray();
                Assert.That(labels.Any(value => value.Contains("门面")), Is.True);
                Assert.That(labels.Any(value => value.Contains("DJ")), Is.False);
            }
            RequireButtonRect("Nav-team").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Text fourthCareer = RequireRect("CareerStatus-3").GetComponent<Text>();
            Assert.That(fourthCareer.text, Does.StartWith("门面"));
        }

        [UnityTest]
        public IEnumerator RosterSearchAndCareerFilterNeverRevealLockedIdentity()
        {
            RequireButtonRect("Nav-members").GetComponent<Button>().onClick.Invoke();
            yield return null;

            var model = new GameModel();
            int lockedIndex = Enumerable.Range(0, GameModel.Members.Length)
                .First(index => !model.IsUnlocked(index));
            string lockedId = GameModel.Members[lockedIndex].Id;
            string lockedName = GameModel.Members[lockedIndex].Name;
            string lockedCareer = GameModel.Members[lockedIndex].Career;
            string lockedRace = GameModel.Members[lockedIndex].Race;

            Assert.That(GameObject.Find("Member-" + lockedId), Is.Not.Null,
                "默认列表必须保留匿名剪影占位。");
            Assert.That(GameObject.Find("Member-" + lockedId).transform.Find("LockedSilhouette"), Is.Not.Null);

            // 真实姓名搜索不得返回未获得成员，也不得再列出匿名剪影卡暗示命中。
            RequireRect("MemberSearch").GetComponent<InputField>().onEndEdit.Invoke(lockedName);
            yield return null;
            Assert.That(GameObject.Find("Member-" + lockedId), Is.Null,
                "搜索未获得成员真实姓名不得返回该卡。");
            Assert.That(VisibleMemberCards().Any(card => card.transform.Find("LockedSilhouette") != null), Is.False,
                "身份搜索时不得继续列出匿名剪影占位。");
            Assert.That(RequireRect("Content").GetComponentsInChildren<Text>(true)
                    .Any(text => text.text == lockedCareer || text.text == lockedRace), Is.False,
                "身份搜索不得暴露未获得成员的职业或种族。");

            // 清空搜索后剪影恢复。
            RequireRect("MemberSearch").GetComponent<InputField>().onEndEdit.Invoke(string.Empty);
            yield return null;
            Assert.That(GameObject.Find("Member-" + lockedId), Is.Not.Null);
            Assert.That(GameObject.Find("Member-" + lockedId).transform.Find("LockedSilhouette"), Is.Not.Null,
                "清空搜索后匿名剪影必须恢复。");

            // 逐一切换职业筛选：只允许出现已获得成员，且身份与筛选一致。
            for (int step = 0; step < 4; step++)
            {
                RequireButtonRect("MemberRoleFilter").GetComponent<Button>().onClick.Invoke();
                yield return null;
                string career = RequireRect("MemberRoleFilter").GetComponentInChildren<Text>().text
                    .Substring("职业：".Length);
                foreach (RectTransform card in VisibleMemberCards())
                {
                    Assert.That(card.transform.Find("LockedSilhouette"), Is.Null,
                        "职业筛选不得筛出未获得剪影卡：" + career);
                    string id = card.name.Substring("Member-".Length);
                    Assert.That(GameModel.Members.Single(member => member.Id == id).Career, Is.EqualTo(career));
                }
            }
        }

        private static RectTransform[] VisibleMemberCards()
        {
            return RequireRect("Content").GetComponentsInChildren<Button>(true)
                .Where(button => button.name.StartsWith("Member-"))
                .Select(button => button.GetComponent<RectTransform>())
                .ToArray();
        }

        [UnityTest]
        public IEnumerator MemberPageUsesRaceAndCareerInsteadOfVisibleRarity()
        {
            RequireButtonRect("Nav-members").GetComponent<Button>().onClick.Invoke();
            yield return null;

            RectTransform content = RequireRect("Content");
            string[] labels = content.GetComponentsInChildren<Text>(true)
                .Select(text => text.text)
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .ToArray();
            Assert.That(labels, Does.Contain("职业：全部"));
            Assert.That(labels, Does.Contain("种族：全部"));
            Assert.That(labels.Any(text => text == "SSR" || text == "SR" || text == "R"), Is.False,
                "成员页不应再把内部稀有度作为玩家可见身份标签。");
            Assert.That(labels.Any(text => text.Contains("魅族") && text.Contains("主唱")), Is.True,
                "成员卡应同时展示种族与职业。");
        }

        [UnityTest]
        public IEnumerator EveryMemberPageAndProfileKeepsTaxonomyInsideItsLayout()
        {
            RectTransform initialContent = RequireRect("Content");
            float originalHeight = initialContent.rect.height;
            initialContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 1536f - 246f);
            Canvas.ForceUpdateCanvases();
            RequireButtonRect("Nav-members").GetComponent<Button>().onClick.Invoke();
            yield return null;

            const int maximumExpectedPages = 12;
            int visitedPages = 0;
            while (true)
            {
                RectTransform content = RequireRect("Content");
                RectTransform[] cards = content.GetComponentsInChildren<Button>(true)
                    .Where(button => button.name.StartsWith("Member-"))
                    .Select(button => button.GetComponent<RectTransform>())
                    .ToArray();
                Assert.That(cards.Length, Is.GreaterThan(0),
                    $"成员第 {visitedPages + 1} 页至少应显示一张角色卡。");

                for (int first = 0; first < cards.Length; first++)
                {
                    AssertContained(content, cards[first],
                        $"成员第 {visitedPages + 1} 页角色卡 {first + 1}");
                    string[] cardLabels = cards[first].GetComponentsInChildren<Text>(true)
                        .Select(text => text.text)
                        .Where(text => !string.IsNullOrWhiteSpace(text))
                        .ToArray();
                    // 未获得角色只显示剪影与剩余数量，不得泄露种族/职业/姓名。
                    bool lockedCard = cards[first].transform.Find("LockedSilhouette") != null;
                    if (lockedCard)
                    {
                        Assert.That(cardLabels.Any(IsVisibleMemberTaxonomy), Is.False,
                            $"未获得角色卡不得泄露种族与职业：第 {visitedPages + 1} 页卡 {first + 1}");
                        Assert.That(cardLabels.Any(text => text == "未签约成员"), Is.True,
                            "未获得角色卡必须标注未签约");
                        Assert.That(cardLabels.Any(text => text.Contains("待揭晓")), Is.True,
                            "未获得角色卡必须给出剩余数量/进度");
                        Assert.That(cards[first].transform.Find("LockedSilhouette"), Is.Not.Null);
                    }
                    else
                    {
                        Assert.That(cardLabels.Any(IsVisibleMemberTaxonomy), Is.True,
                            $"成员第 {visitedPages + 1} 页角色卡 {first + 1} 缺少种族与职业。");
                    }

                    Assert.That(cardLabels.Any(IsLegacyVisibleRarity), Is.False,
                        $"成员第 {visitedPages + 1} 页角色卡 {first + 1} 不应显示内部稀有度。");

                    for (int second = first + 1; second < cards.Length; second++)
                        Assert.That(RectInParent(cards[first]).Overlaps(RectInParent(cards[second])), Is.False,
                            $"成员第 {visitedPages + 1} 页角色卡 {first + 1} 与 {second + 1} 不能重叠。");
                }

                cards[0].GetComponent<Button>().onClick.Invoke();
                yield return null;
                RectTransform overlay = RequireRect("MemberModal");
                overlay.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 720f);
                overlay.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 1536f);
                Canvas.ForceUpdateCanvases();
                RectTransform profile = overlay.Find("Panel") as RectTransform;
                Assert.That(profile, Is.Not.Null, "成员详情必须包含完整资料面板。");
                AssertContained(overlay, profile, $"成员第 {visitedPages + 1} 页资料面板");
                string[] profileLabels = profile.GetComponentsInChildren<Text>(true)
                    .Select(text => text.text)
                    .Where(text => !string.IsNullOrWhiteSpace(text))
                    .ToArray();
                bool lockedProfile = profile.GetComponentsInChildren<Transform>(true)
                    .Any(item => item.name == "LockedSilhouetteMark");
                if (lockedProfile)
                {
                    Assert.That(profileLabels.Any(IsVisibleMemberTaxonomy), Is.False,
                        $"未获得角色档案不得泄露种族与职业：第 {visitedPages + 1} 页");
                    RequireRect("MemberLockedProgress");
                    Assert.That(GameObject.Find("MemberStatPanel"), Is.Null,
                        "未获得角色不得显示基础属性面板。");
                    Assert.That(GameObject.Find("MemberSkillPanel"), Is.Null,
                        "未获得角色不得显示技能面板。");
                    AssertContained(profile, RequireRect("MemberAcquireGuide"),
                        $"成员第 {visitedPages + 1} 页获取说明");
                }
                else
                {
                    Assert.That(profileLabels.Any(IsVisibleMemberTaxonomy), Is.True,
                        $"成员第 {visitedPages + 1} 页资料面板缺少种族与职业。");
                    AssertContained(profile, RequireRect("MemberStatPanel"),
                        $"成员第 {visitedPages + 1} 页基础属性");
                    AssertContained(profile, RequireRect("MemberSkillPanel"),
                        $"成员第 {visitedPages + 1} 页成员技能");
                    AssertContained(profile, RequireRect("MemberTrainingPreview"),
                        $"成员第 {visitedPages + 1} 页培养预览");
                    AssertContained(profile, RequireRect("MemberTrainingCost"),
                        $"成员第 {visitedPages + 1} 页真实培养费用");
                }

                Assert.That(profileLabels.Any(IsLegacyVisibleRarity), Is.False,
                    $"成员第 {visitedPages + 1} 页资料面板不应显示内部稀有度。");
                RequireButtonRect("CloseTop").GetComponent<Button>().onClick.Invoke();
                yield return null;

                visitedPages++;
                Assert.That(visitedPages, Is.LessThanOrEqualTo(maximumExpectedPages),
                    "成员分页数量异常，可能出现翻页状态没有收敛。");
                Button next = RequireButtonRect("MemberNextPage").GetComponent<Button>();
                if (!next.interactable) break;
                next.onClick.Invoke();
                yield return null;
            }

            Assert.That(visitedPages, Is.GreaterThan(1),
                "当前成员目录应覆盖多个分页，避免回归只验证首屏角色。");
            RequireRect("Content").SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, originalHeight);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PrimaryPagesStayInsideShortAndStandardPortraitContent()
        {
            RectTransform content = RequireRect("Content");
            float originalHeight = content.rect.height;
            float[] designContentHeights = { 1280f - 246f, 1536f - 246f };

            foreach (float targetHeight in designContentHeights)
            {
                content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetHeight);
                Canvas.ForceUpdateCanvases();

                RequireButtonRect("Nav-team").GetComponent<Button>().onClick.Invoke();
                yield return null;
                content = RequireRect("Content");
                AssertContained(content, RequireRect("TeamSynergy"), $"{targetHeight} 高度下的组合效果");
                AssertContained(content, RequireButtonRect("ChangeLeader"), $"{targetHeight} 高度下的更换队长");
                AssertContained(content, RequireButtonRect("AutoTeam"), $"{targetHeight} 高度下的一键编队");
                for (int slot = 0; slot < GameModel.TeamCapacity; slot++)
                    AssertContained(content, RequireButtonRect($"TeamOrbit-{slot}"),
                        $"{targetHeight} 高度下的编队槽 {slot}");

                RequireButtonRect("Nav-members").GetComponent<Button>().onClick.Invoke();
                yield return null;
                content = RequireRect("Content");
                AssertContained(content, RequireButtonRect("MemberPreviousPage"),
                    $"{targetHeight} 高度下的成员上一页");
                AssertContained(content, RequireButtonRect("MemberNextPage"),
                    $"{targetHeight} 高度下的成员下一页");

                RequireButtonRect("Nav-accessory").GetComponent<Button>().onClick.Invoke();
                yield return null;
                content = RequireRect("Content");
                RectTransform equipmentBoard = RequireRect("EquipmentReferenceBoard038");
                RectTransform equipmentBody = RequireRect("EquipmentBody");
                Assert.That(equipmentBoard.GetComponent<Image>().preserveAspect, Is.True);
                Assert.That(equipmentBoard.localScale.x, Is.EqualTo(equipmentBoard.localScale.y).Within(.001f));
                AssertContained(content, RequireButtonRect("EquipmentCycleMember"), "装备角色切换");
                AssertContained(content, RequireButtonRect("EquipmentChooseMember"), "装备角色选择");
                AssertContained(content, RequireRect("EquipmentSelectedArt"), "角色装备详情图片");
                for (int index = 0; index < 12; index++)
                    AssertContained(content, RequireButtonRect($"Accessory-{index}"),
                        $"{targetHeight} 高度下的饰品图鉴卡 {index + 1}");
                AssertEquipmentTextFitsWithoutOverlap(equipmentBody);
                AssertContained(content, RequireRect("EquipmentPageCount"),
                    "整图分页模式必须能完整看到分页信息");
                foreach (string name in new[] { "EquipmentPreviousPage", "EquipmentNextPage" })
                {
                    RectTransform pagination = RequireButtonRect(name);
                    AssertContained(content, pagination, $"{targetHeight} 高度下的饰品分页操作");
                    Assert.That(RectRelativeTo(RequireRect("SafeArea"), pagination)
                        .Overlaps(RectRelativeTo(RequireRect("SafeArea"), RequireRect("BottomNavigation"))), Is.False,
                        "饰品分页操作不可侵入公共底栏。");
                }

                RequireButtonRect("Nav-audition").GetComponent<Button>().onClick.Invoke();
                yield return null;
                content = RequireRect("Content");
                RectTransform offlinePool = RequireButtonRect("InterviewPool-1");
                AssertAuthoredAuditionFitsContent(content, $"{targetHeight} 高度线上面试");

                offlinePool.GetComponent<Button>().onClick.Invoke();
                yield return null;
                content = RequireRect("Content");
                AssertAuthoredAuditionFitsContent(content, $"{targetHeight} 高度线下面试");
            }

            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, originalHeight);
            yield return null;
        }

        [UnityTest]
        public IEnumerator LobbyKeepsThreeSecondaryRoutesOnePrimaryActionAndAProtectedFaceArea()
        {
            RectTransform lobby = RequireRect("LobbyCards");
            RectTransform[] hotspots =
            {
                RequireButtonRect("PracticeRoom"),
                RequireButtonRect("AlbumProduction"),
                RequireButtonRect("Tasks"),
            };

            for (int index = 0; index < hotspots.Length; index++)
            {
                Rect rect = RectInParent(hotspots[index]);
                Assert.That(rect.width, Is.GreaterThanOrEqualTo(160f),
                    $"{hotspots[index].name} 的完整卡面必须可点击，不能只让文字响应。");
                Assert.That(rect.height, Is.GreaterThanOrEqualTo(96f),
                    $"{hotspots[index].name} 的点击热区必须覆盖卡面。");
                Assert.That(hotspots[index].GetComponent<Button>(), Is.Not.Null);
                Assert.That(hotspots[index].GetComponent<Button>().targetGraphic?.raycastTarget, Is.True);
                AssertDecorationsDoNotStealRaycasts(hotspots[index]);
            }

            for (int first = 0; first < hotspots.Length; first++)
            for (int second = first + 1; second < hotspots.Length; second++)
                Assert.That(RectInParent(hotspots[first]).Overlaps(RectInParent(hotspots[second])), Is.False,
                    $"{hotspots[first].name} 与 {hotspots[second].name} 不应重叠。");

            Assert.That(Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude)
                    .Count(button => button.name == "LiveOnStage"), Is.EqualTo(1),
                "首页只能有一个开始演出主入口。");
            RectTransform stage = RequireButtonRect("LiveOnStage");
            AssertDecorationsDoNotStealRaycasts(stage);

            Button album = hotspots[1].GetComponent<Button>();
            Assert.That(HasLockedPresentation(hotspots[1]), Is.True,
                "专辑制作必须明确显示锁定/即将开放，不能让玩家误以为功能已经可用。");
            Assert.That(album, Is.Not.Null,
                "专辑制作锁定入口仍需保留 Button，以便禁用或点击后解释开放状态。");
            Assert.That(album.IsInteractable(), Is.True,
                "专辑制作锁定入口应可点击并解释开放状态，不能像失效按钮一样没有反馈。");

            RectTransform faceSafeZone = RequireRect("HeroFaceSafeZone");
            Graphic faceGraphic = faceSafeZone.GetComponent<Graphic>();
            Assert.That(faceGraphic == null || !faceGraphic.raycastTarget, Is.True,
                "角色脸部安全区只能用于构图回归，不得拦截点击。");
            foreach (RectTransform hotspot in hotspots)
                Assert.That(RectRelativeTo(lobby, hotspot).Overlaps(RectRelativeTo(lobby, faceSafeZone)), Is.False,
                    $"{hotspot.name} 不得遮住角色脸部安全区。");

            Assert.That(GameObject.Find("闪耀舞台计划"), Is.Null, "首页入口应使用短标签“闪耀舞台”。");
            Assert.That(GameObject.Find("每日签到"), Is.Null, "签到应合并进任务面板，不应重复占据首页入口。");
            Assert.That(GameObject.Find("直播间"), Is.Null, "演出只能保留一个主入口。");
            Assert.That(Object.FindObjectsByType<Transform>(FindObjectsInactive.Include)
                    .Any(item => item.name == "冒险剧本"), Is.False,
                "冒险剧本已合并进开始演出，不得留下重复按钮或隐藏点击热区。");
            Assert.That(GameObject.Find("商城抽卡"), Is.Null, "底部导航功能不应在首页重复出现。");
            Assert.That(GameObject.Find("ClaimableDot"), Is.Null,
                "首页不应出现悬空的纯色方块通知，任务状态统一在任务面板中呈现。");
            yield return null;
        }

        [UnityTest]
        public IEnumerator StageCallToActionHotspotDoesNotCoverTheHeroFaceAndGoldenDoesNotStealInput()
        {
            RectTransform stage = RequireRect("LiveOnStage");
            Image golden = RequireApprovedLobbyGolden();

            Rect stageRect = RectInParent(stage);
            RectTransform lobby = RequireRect("LobbyCards");
            RectTransform faceSafeZone = RequireRect("HeroFaceSafeZone");
            Assert.That(stageRect.Overlaps(RectRelativeTo(lobby, faceSafeZone)), Is.False,
                "开始演出不得盖住角色脸部。");
            Assert.That(golden.raycastTarget, Is.False,
                "0.3.8 首页 golden 不得抢占主 CTA 的按钮射线。");
            yield return null;
        }

        [UnityTest]
        public IEnumerator TopBarMatchesLatestReferenceWithoutMailAndKeepsMusicInsideSettings()
        {
            RectTransform profile = RequireButtonRect("Profile");
            RectTransform settings = RequireButtonRect("Settings");
            RectTransform[] controls = { profile, settings };
            RectTransform topBar = RequireRect("TopBar");

            Assert.That(GameObject.Find("Mail"), Is.Null,
                "最新首页参考不显示邮件，顶部不得保留可见按钮或隐藏热区。");

            Assert.That(topBar.GetComponentsInChildren<Transform>(true)
                    .Any(item => item.name == "Music"), Is.False,
                "顶部栏不应再创建音乐按钮或遗留点击热区。");
            Assert.That(topBar.GetComponentsInChildren<Transform>(true)
                    .Any(item => item.name == "Notice"), Is.False,
                "设置按钮不应显示没有真实状态来源的粉色通知点。");
            Assert.That(topBar.GetComponentsInChildren<Transform>(true)
                    .Any(item => item.name == "Badge"), Is.False,
                "顶部栏不应遗留无意义的 Badge 节点。");
            Assert.That(topBar.GetComponentsInChildren<Transform>(true)
                    .Any(item => item.name == "Accent"), Is.False,
                "顶部栏不应遗留无意义的 Accent 小方块。");

            foreach (RectTransform control in controls)
            {
                Rect hitRect = RectInParent(control);
                Assert.That(hitRect.width, Is.GreaterThanOrEqualTo(40f - PositionTolerance),
                    $"{control.name} 的点击热区宽度至少应为 40 设计像素。");
                Assert.That(hitRect.height, Is.GreaterThanOrEqualTo(44f - PositionTolerance),
                    $"{control.name} 的点击热区高度至少应为 44 设计像素。");
            }

            for (int first = 0; first < controls.Length; first++)
            {
                for (int second = first + 1; second < controls.Length; second++)
                {
                    Rect a = RectInParent(controls[first]);
                    Rect b = RectInParent(controls[second]);
                    Assert.That(a.Overlaps(b), Is.False,
                        $"{controls[first].name} 与 {controls[second].name} 的点击热区不应重叠。");
                }
            }

            Rect settingsRect = RectInParent(settings);

            Rect diamondIconRect = RectInParent(RequireRect("DiamondIcon"));
            Rect diamondValueRect = RectInParent(RequireRect("Diamonds"));
            Rect goldIconRect = RectInParent(RequireRect("GoldIcon"));
            Rect goldValueRect = RectInParent(RequireRect("Gold"));
            Rect staminaIconRect = RectInParent(RequireRect("StaminaIcon"));
            RectTransform staminaValue = RequireRect("Stamina");
            Rect staminaValueRect = RectInParent(staminaValue);
            Assert.That(staminaValue.GetComponent<Text>().text, Does.Match(@"^\d+/\d+$"),
                "体力栏只应显示当前值/上限，不应再包含倒计时。");
            Assert.That(staminaValue.GetComponent<Text>().text, Does.Not.Contain(":"));
            Assert.That(staminaValueRect.width, Is.LessThanOrEqualTo(85f + PositionTolerance),
                "体力文本不应为已删除的倒计时保留空白宽度。");
            // The latest header uses a smaller diamond and more breathing room.
            // These exact live-value slots are measured against that authored strip.
            Assert.That(diamondValueRect.xMin - diamondIconRect.xMax, Is.EqualTo(18f).Within(PositionTolerance));
            Assert.That(goldValueRect.xMin - goldIconRect.xMax, Is.EqualTo(4f).Within(PositionTolerance));
            Assert.That(staminaValueRect.xMin - staminaIconRect.xMax, Is.EqualTo(0f).Within(PositionTolerance));
            Assert.That(diamondValueRect.width, Is.EqualTo(41f).Within(PositionTolerance));
            Assert.That(goldValueRect.width, Is.EqualTo(59f).Within(PositionTolerance));
            Assert.That(staminaValueRect.width, Is.EqualTo(65f).Within(PositionTolerance));
            Assert.That(diamondValueRect.Overlaps(diamondIconRect), Is.False);
            Assert.That(goldValueRect.Overlaps(goldIconRect), Is.False);
            Assert.That(staminaValueRect.Overlaps(staminaIconRect), Is.False);
            Rect previousResource = default;
            Rect previousPlus = default;
            foreach (string currency in new[] { "diamond", "gold", "stamina" })
            {
                RectTransform group = RequireRect("Currency-" + currency);
                RectTransform plus = RequireButtonRect("CurrencyPlus-" + currency);
                Assert.That(plus.GetComponent<Button>().IsInteractable(), Is.True);
                Assert.That(plus.GetComponent<Button>().targetGraphic.raycastTarget, Is.True,
                    $"{currency} 的加号必须能独立接收玩家点击。");
                Rect groupRect = RectInParent(group);
                Rect plusRect = RectRelativeTo(group, plus);
                Assert.That(groupRect.height, Is.GreaterThanOrEqualTo(44f));
                Assert.That(plusRect.width, Is.GreaterThanOrEqualTo(30f),
                    $"{currency} 的加号点击区不能窄到难以点击。");
                Assert.That(plusRect.height, Is.GreaterThanOrEqualTo(36f),
                    $"{currency} 的加号点击区必须覆盖可见加号。");
                if (currency != "diamond")
                {
                    Assert.That(groupRect.Overlaps(previousResource), Is.False,
                        "资源信息块不得侵入下一组。");
                    Assert.That(RectInParent(plus).Overlaps(previousPlus), Is.False,
                        "三个资源加号必须拥有互不重叠的独立点击区。");
                }
                previousResource = groupRect;
                previousPlus = RectInParent(plus);
                string valueName = currency == "diamond" ? "Diamonds" : currency == "gold" ? "Gold" : "Stamina";
                AssertContained(group, RequireRect(valueName), "数字必须完整落在所属资源信息块内");
                Assert.That(RequireRect(valueName).GetComponent<Text>().raycastTarget, Is.False);
            }
            Assert.That(settingsRect.xMin - previousResource.xMax, Is.InRange(-PositionTolerance, 24f),
                "设置应紧跟体力信息块，不应为已移除的邮件按钮保留空槽。");

            settings.GetComponent<Button>().onClick.Invoke();
            yield return null;
            RectTransform settingsModal = RequireRect("SettingsModal");
            RectTransform musicRow = RequireRect("Setting-音乐");
            Assert.That(musicRow.IsChildOf(settingsModal), Is.True,
                "音乐开关应继续保留在设置弹窗内。");
            FindText(musicRow, "音乐");
            Assert.That(musicRow.GetComponentsInChildren<Text>(true)
                    .Any(text => text.text == "已开启" || text.text == "已关闭"), Is.True,
                "设置中的音乐行应显示当前开关状态。");

            yield return null;
        }

        [UnityTest]
        public IEnumerator BottomNavigationMatchesLatestFiveNonOverlappingDestinations()
        {
            string[] ids = { "team", "members", "lobby", "accessory", "audition" };
            string[] labels = { "团队", "成员", "大厅", "饰品", "选秀" };
            Canvas canvas = Object.FindAnyObjectByType<Canvas>();
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            canvas.renderMode = RenderMode.WorldSpace;
            scaler.enabled = false;
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            canvasRect.anchorMin = canvasRect.anchorMax = new Vector2(.5f, .5f);
            canvasRect.pivot = new Vector2(.5f, .5f);
            canvasRect.sizeDelta = new Vector2(720f, 1536f);
            Canvas.ForceUpdateCanvases();
            yield return null;

            RectTransform safe = RequireRect("SafeArea");
            RectTransform navigation = RequireRect("BottomNavigation");
            var buttons = new RectTransform[ids.Length];
            int selectedCount = 0;

            for (int index = 0; index < ids.Length; index++)
            {
                buttons[index] = RequireButtonRect("Nav-" + ids[index]);
                AssertContained(navigation, buttons[index], labels[index]);
                Text[] buttonLabels = buttons[index].GetComponentsInChildren<Text>(true)
                    .Where(text => !string.IsNullOrWhiteSpace(text.text))
                    .ToArray();
                Assert.That(buttonLabels.Length, Is.EqualTo(1),
                    $"底部{labels[index]}入口只能保留一个中文标签，不能再叠加英文副标题。");
                Assert.That(buttonLabels[0].text.Trim(), Is.EqualTo(labels[index]));
                Assert.That(buttonLabels[0].text.Any(character =>
                        character >= 'A' && character <= 'Z' || character >= 'a' && character <= 'z'),
                    Is.False, $"底部{labels[index]}入口不能显示英文。");
                Assert.That(buttons[index].GetComponent<Button>().IsInteractable(), Is.True,
                    $"底部{labels[index]}入口必须可点击。");
                Assert.That(buttons[index].GetComponent<Button>().targetGraphic.raycastTarget, Is.True,
                    $"底部{labels[index]}入口必须能独立接收点击。");
                Rect hitRect = RectRelativeTo(safe, buttons[index]);
                Rect expected = LobbyNavHitBounds038[index];
                Rect expectedLocal = Rect.MinMaxRect(
                    safe.rect.xMin + expected.x,
                    safe.rect.yMax - expected.y - expected.height,
                    safe.rect.xMin + expected.x + expected.width,
                    safe.rect.yMax - expected.y);
                AssertTopLeftBounds(safe, buttons[index],
                    expected.x, expected.y, expected.width, expected.height, 10f,
                    labels[index] + " 0.3.8 点击热区");
                Assert.That(Vector2.Distance(hitRect.center, expectedLocal.center), Is.LessThanOrEqualTo(6f),
                    $"{labels[index]} 的点击中心必须命中 0.3.8 图中对应图标，不能沿用等分底栏的偏移中心。");
                Assert.That(hitRect.Contains(expectedLocal.center), Is.True,
                    $"{labels[index]} 的真实视觉中心必须落在自身点击热区内。");
                for (int other = 0; other < index; other++)
                    Assert.That(RectRelativeTo(safe, buttons[other]).Contains(expectedLocal.center), Is.False,
                        $"{labels[index]} 的视觉中心不能被 {labels[other]} 的热区截获。");
                Image highlight = buttons[index].Find("Highlight")?.GetComponent<Image>();
                Assert.That(highlight, Is.Not.Null, $"{labels[index]} 缺少选中态指示。");
                if (highlight.color.a > 0.5f) selectedCount++;
            }

            for (int first = 0; first < buttons.Length; first++)
            {
                for (int second = first + 1; second < buttons.Length; second++)
                    Assert.That(RectRelativeTo(safe, buttons[first]).Overlaps(RectRelativeTo(safe, buttons[second])), Is.False,
                        $"{labels[first]} 与 {labels[second]} 的点击热区不能重叠。");
            }

            Assert.That(selectedCount, Is.EqualTo(1),
                "底部导航任何时刻只能显示一个选中入口。");
            yield return null;
        }

        [UnityTest]
        public IEnumerator BottomNavigationKeepsMeasuredLayoutAndFeedbackAnchorsAcrossPages()
        {
            string[] ids = { "team", "members", "lobby", "accessory", "audition" };
            Canvas canvas = Object.FindAnyObjectByType<Canvas>();
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            canvas.renderMode = RenderMode.WorldSpace;
            scaler.enabled = false;
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            canvasRect.anchorMin = canvasRect.anchorMax = new Vector2(.5f, .5f);
            canvasRect.pivot = new Vector2(.5f, .5f);
            canvasRect.sizeDelta = new Vector2(720f, 1536f);
            Canvas.ForceUpdateCanvases();
            yield return null;

            RectTransform safe = RequireRect("SafeArea");
            string[] destinations = { "members", "team", "accessory", "lobby", "audition" };
            foreach (string destination in destinations)
            {
                RequireButtonRect("Nav-" + destination).GetComponent<Button>().onClick.Invoke();
                Canvas.ForceUpdateCanvases();
                yield return null;

                for (int index = 0; index < ids.Length; index++)
                {
                    RectTransform button = RequireButtonRect("Nav-" + ids[index]);
                    Rect expected = LobbyNavHitBounds038[index];
                    AssertTopLeftBounds(safe, button, expected.x, expected.y,
                        expected.width, expected.height, 1f,
                        destination + " 页面底栏 " + ids[index]);
                    Assert.That(button.GetComponent<ButtonInteractionFeedback>(), Is.Null,
                        ids[index] + " 不得缩放输入热区并拖动反馈线。 ");

                    RectTransform underline = button.Find("InteractionFx/HoverUnderline") as RectTransform;
                    Assert.That(underline, Is.Not.Null, ids[index] + " 缺少独立导航反馈锚点。 ");
                    Rect underlineRect = RectRelativeTo(safe, underline);
                    float screenCenterX = underlineRect.center.x - safe.rect.xMin;
                    float screenCenterY = safe.rect.yMax - underlineRect.center.y;
                    Assert.That(screenCenterX, Is.EqualTo(LobbyNavLabelCenters038[index]).Within(1f),
                        ids[index] + " 的反馈线必须对齐底图中文标签中心。 ");
                    Assert.That(screenCenterY, Is.EqualTo(LobbyNavUnderlineBaseline038).Within(1f),
                        ids[index] + " 的反馈线必须和大厅选中光条共用基线。 ");

                    if (destination != "lobby")
                    {
                        Image visual = button.GetComponentsInChildren<Image>(true)
                            .FirstOrDefault(image => image.name.EndsWith("VisualV2"));
                        Assert.That(visual, Is.Not.Null, ids[index] + " 非大厅导航缺少视觉素材。 ");
                        Assert.That(visual.gameObject.activeInHierarchy && visual.enabled, Is.True,
                            ids[index] + " 非大厅导航素材必须实际可见。 ");
                        Assert.That(visual.sprite, Is.Not.Null,
                            ids[index] + " 非大厅导航素材不能回退为空 Image。 ");
                        Assert.That(visual.preserveAspect, Is.True,
                            ids[index] + " 导航素材必须保持原比例，不能随热区拉伸。 ");
                        Assert.That(visual.rectTransform.localScale.x,
                            Is.EqualTo(visual.rectTransform.localScale.y).Within(0.001f),
                            ids[index] + " 导航素材不能通过非等比 Transform 缩放变形。 ");

                        Rect expectedVisual = LobbyNavVisualBounds038[index];
                        expectedVisual.height -= 20f;
                        Rect visualRect = RectRelativeTo(safe, visual.rectTransform);
                        Rect expectedLocal = Rect.MinMaxRect(
                            safe.rect.xMin + expectedVisual.x,
                            safe.rect.yMax - expectedVisual.y - expectedVisual.height,
                            safe.rect.xMin + expectedVisual.x + expectedVisual.width,
                            safe.rect.yMax - expectedVisual.y);
                        AssertTopLeftBounds(safe, visual.rectTransform, expectedVisual.x, expectedVisual.y,
                            expectedVisual.width, expectedVisual.height, 1f,
                            destination + " 页面底栏 " + ids[index] + " 可见素材");
                        Assert.That(Vector2.Distance(visualRect.center, expectedLocal.center),
                            Is.LessThanOrEqualTo(1f),
                            ids[index] + " 非大厅可见素材中心必须与 0.3.8 首页实测中心一致。 ");

                        Text chineseLabel = button.Find("Label").GetComponent<Text>();
                        Assert.That(chineseLabel.fontSize, Is.GreaterThanOrEqualTo(18), "底栏中文不可缩成微小烘焙字。");
                        Assert.That(chineseLabel.color.a, Is.GreaterThan(.9f));
                        Assert.That(chineseLabel.preferredHeight, Is.LessThanOrEqualTo(chineseLabel.rectTransform.rect.height));
                        AssertTopLeftBounds(safe, chineseLabel.rectTransform, LobbyNavLabelCenters038[index] - 38f,
                            1432f, 76f, 28f, 1f, "各页中文标签必须共享清晰字号和坐标");

                        Text[] englishLiveText = button.GetComponentsInChildren<Text>(true)
                            .Where(text => !string.IsNullOrWhiteSpace(text.text) && text.text.Any(character =>
                                character >= 'A' && character <= 'Z' ||
                                character >= 'a' && character <= 'z'))
                            .ToArray();
                        Assert.That(englishLiveText, Is.Empty,
                            ids[index] + " 非大厅导航不得在图片素材上额外叠加英文 live Text 节点。 ");
                    }
                }
            }
        }

        [UnityTest]
        public IEnumerator LobbyCompositionFitsReferenceTallAndCompactPortraitViewports()
        {
            Canvas canvas = Object.FindAnyObjectByType<Canvas>();
            Assert.That(canvas, Is.Not.Null);
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            Assert.That(scaler, Is.Not.Null);

            // CanvasScaler matches width, so every physical viewport maps to a 720-unit-wide
            // design surface. Driving that logical surface directly is deterministic in the
            // Editor, where Screen.SetResolution is not reliable in batch PlayMode tests.
            canvas.renderMode = RenderMode.WorldSpace;
            scaler.enabled = false;
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            canvasRect.anchorMin = canvasRect.anchorMax = new Vector2(.5f, .5f);
            canvasRect.pivot = new Vector2(.5f, .5f);

            (int width, int height)[] viewports =
            {
                (720, 1536),
                (390, 844),
                (320, 568),
            };
            foreach ((int width, int height) viewport in viewports)
            {
                float logicalHeight = viewport.height * 720f / viewport.width;
                canvasRect.sizeDelta = new Vector2(720f, logicalHeight);
                Canvas.ForceUpdateCanvases();
                yield return null;

                RectTransform safe = RequireRect("SafeArea");
                RectTransform content = RequireRect("Content");
                RectTransform navigation = RequireRect("BottomNavigation");
                RectTransform lobby = RequireRect("LobbyCards");
                RectTransform faceSafeZone = RequireRect("HeroFaceSafeZone");
                RectTransform[] actions =
                {
                    RequireButtonRect("PracticeRoom"),
                    RequireButtonRect("AlbumProduction"),
                    RequireButtonRect("Tasks"),
                    RequireButtonRect("LiveOnStage"),
                };

                AssertContained(safe, content, $"{viewport.width}x{viewport.height} 内容区");
                AssertContained(safe, navigation, $"{viewport.width}x{viewport.height} 底部导航");
                AssertContained(content, faceSafeZone, $"{viewport.width}x{viewport.height} 角色脸部安全区");
                foreach (RectTransform action in actions)
                {
                    AssertContained(content, action,
                        $"{viewport.width}x{viewport.height} 的 {action.name}");
                    Assert.That(RectRelativeTo(safe, action).Overlaps(RectRelativeTo(safe, navigation)), Is.False,
                        $"{viewport.width}x{viewport.height} 下 {action.name} 不得侵入底栏。");
                    Assert.That(RectRelativeTo(content, action)
                            .Overlaps(RectRelativeTo(content, faceSafeZone)), Is.False,
                        $"{viewport.width}x{viewport.height} 下 {action.name} 不得遮住角色脸部。");
                }

                for (int first = 0; first < actions.Length; first++)
                for (int second = first + 1; second < actions.Length; second++)
                    Assert.That(RectRelativeTo(content, actions[first])
                            .Overlaps(RectRelativeTo(content, actions[second])), Is.False,
                        $"{viewport.width}x{viewport.height} 下 {actions[first].name} 与 {actions[second].name} 点击区不得重叠。");

                if (viewport.width == 720 && viewport.height == 1536)
                    AssertLatestReferenceBounds(safe, navigation, faceSafeZone, actions);
            }
        }

        [UnityTest]
        public IEnumerator LobbyHotspotsExposeVisibleHoverPressExitFeedbackWithoutBreakingClicks()
        {
            PointerEventData pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
            };
            string[] hotspotNames =
            {
                "PracticeRoom",
                "CurrencyPlus-diamond",
                "Nav-team",
                "Nav-members",
                "Nav-lobby",
                "Nav-accessory",
                "Nav-audition",
                "Settings",
            };

            foreach (string hotspotName in hotspotNames)
                yield return AssertVisiblePointerFeedback(hotspotName, pointer);

            // Pointer feedback must decorate the existing Buttons rather than replace or
            // consume their click chain. Exercise the original route behind each class.
            RequireButtonRect("Settings").GetComponent<Button>().onClick.Invoke();
            yield return null;
            RequireRect("SettingsModal");
            RequireButtonRect("Done").GetComponent<Button>().onClick.Invoke();
            yield return null;

            RequireButtonRect("CurrencyPlus-diamond").GetComponent<Button>().onClick.Invoke();
            yield return null;
            RequireRect("CurrencyModal");
            RequireButtonRect("CloseProgression").GetComponent<Button>().onClick.Invoke();
            yield return null;

            RequireButtonRect("Nav-team").GetComponent<Button>().onClick.Invoke();
            yield return null;
            RequireRect("TeamPowerValue");
            RequireButtonRect("Nav-lobby").GetComponent<Button>().onClick.Invoke();
            yield return null;

            RequireButtonRect("PracticeRoom").GetComponent<Button>().onClick.Invoke();
            yield return null;
            RequireRect("MemberPracticePanel");
        }

        private static IEnumerator AssertVisiblePointerFeedback(string hotspotName, PointerEventData pointer)
        {
            RectTransform hotspot = RequireButtonRect(hotspotName);
            Button button = hotspot.GetComponent<Button>();
            Vector2 originalPosition = hotspot.anchoredPosition;
            Vector2 originalSize = hotspot.sizeDelta;
            Vector3 originalScale = hotspot.localScale;
            Graphic[] decorations = hotspot.GetComponentsInChildren<Graphic>(true)
                .Where(graphic => graphic != button.targetGraphic)
                .ToArray();
            Assert.That(decorations, Is.Not.Empty,
                $"{hotspotName} 必须提供独立的可见反馈层，不能只缩放透明点击层。");
            foreach (Graphic decoration in decorations)
                Assert.That(decoration.raycastTarget, Is.False,
                    $"{hotspotName}/{decoration.name} 是反馈装饰，不得拦截 Button 射线。");

            Assert.That(MaxEffectiveAlpha(hotspot, button.targetGraphic), Is.LessThanOrEqualTo(0.01f),
                $"{hotspotName} 静止时反馈必须完全透明或 inactive，不能改变 0.3.8 golden。");

            ExecuteEvents.Execute<IPointerEnterHandler>(hotspot.gameObject, pointer,
                ExecuteEvents.pointerEnterHandler);
            yield return new WaitForSecondsRealtime(0.16f);
            Graphic visible = MostVisibleFeedback(hotspot, button.targetGraphic);
            Assert.That(visible, Is.Not.Null,
                $"{hotspotName} 悬停后必须出现玩家看得到的 Graphic/CanvasGroup 反馈。");
            Assert.That(EffectiveAlpha(visible, hotspot), Is.GreaterThan(0.05f),
                $"{hotspotName} 悬停反馈不能只改变透明 Button 自身的缩放。");
            FeedbackVisualState hover = CaptureFeedbackState(visible, hotspot);

            ExecuteEvents.Execute<IPointerDownHandler>(hotspot.gameObject, pointer,
                ExecuteEvents.pointerDownHandler);
            yield return new WaitForSecondsRealtime(0.08f);
            FeedbackVisualState pressed = CaptureFeedbackState(visible, hotspot);
            Assert.That(FeedbackChanged(hover, pressed), Is.True,
                $"{hotspotName} pointer down 必须产生可观察的压下或闪光状态。");

            ExecuteEvents.Execute<IPointerUpHandler>(hotspot.gameObject, pointer,
                ExecuteEvents.pointerUpHandler);
            yield return new WaitForSecondsRealtime(0.12f);
            FeedbackVisualState released = CaptureFeedbackState(visible, hotspot);
            Assert.That(FeedbackChanged(pressed, released), Is.True,
                $"{hotspotName} pointer up 必须从按压态恢复为悬停态。");

            ExecuteEvents.Execute<IPointerExitHandler>(hotspot.gameObject, pointer,
                ExecuteEvents.pointerExitHandler);
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(MaxEffectiveAlpha(hotspot, button.targetGraphic), Is.LessThanOrEqualTo(0.01f),
                $"{hotspotName} pointer exit 后必须恢复完全透明，保持静止画面 1:1。");
            Assert.That(button.IsInteractable(), Is.True,
                $"{hotspotName} 动画结束后必须保留原 Button 点击链。");
            Assert.That(hotspot.anchoredPosition, Is.EqualTo(originalPosition),
                $"{hotspotName} 的输入区域不能被反馈动画移动。");
            Assert.That(hotspot.sizeDelta, Is.EqualTo(originalSize),
                $"{hotspotName} 的输入区域不能被反馈动画改尺寸。");
            Assert.That(hotspot.localScale, Is.EqualTo(originalScale),
                $"{hotspotName} 的输入区域不能被反馈动画缩放。");
        }

        private struct FeedbackVisualState
        {
            public float Alpha;
            public Color Color;
            public Vector3 Scale;
            public Vector3 Position;
        }

        private static FeedbackVisualState CaptureFeedbackState(Graphic graphic, RectTransform hotspot)
        {
            return new FeedbackVisualState
            {
                Alpha = EffectiveAlpha(graphic, hotspot),
                Color = graphic.color,
                Scale = graphic.rectTransform.localScale,
                Position = graphic.rectTransform.localPosition,
            };
        }

        private static bool FeedbackChanged(FeedbackVisualState before, FeedbackVisualState after)
        {
            Color colorDelta = before.Color - after.Color;
            float colorDifference = colorDelta.r * colorDelta.r + colorDelta.g * colorDelta.g +
                                    colorDelta.b * colorDelta.b + colorDelta.a * colorDelta.a;
            return Mathf.Abs(before.Alpha - after.Alpha) > 0.005f ||
                   colorDifference > 0.000025f ||
                   (before.Scale - after.Scale).sqrMagnitude > 0.000025f ||
                   (before.Position - after.Position).sqrMagnitude > 0.000025f;
        }

        private static Graphic MostVisibleFeedback(RectTransform hotspot, Graphic targetGraphic)
        {
            return hotspot.GetComponentsInChildren<Graphic>(true)
                .Where(graphic => graphic != targetGraphic)
                .OrderByDescending(graphic => EffectiveAlpha(graphic, hotspot))
                .FirstOrDefault(graphic => EffectiveAlpha(graphic, hotspot) > 0.01f);
        }

        private static float MaxEffectiveAlpha(RectTransform hotspot, Graphic targetGraphic)
        {
            return hotspot.GetComponentsInChildren<Graphic>(true)
                .Where(graphic => graphic != targetGraphic)
                .Select(graphic => EffectiveAlpha(graphic, hotspot))
                .DefaultIfEmpty(0f)
                .Max();
        }

        private static float EffectiveAlpha(Graphic graphic, RectTransform hotspot)
        {
            if (graphic == null || !graphic.enabled || !graphic.gameObject.activeInHierarchy)
                return 0f;

            float alpha = graphic.color.a;
            Transform current = graphic.transform;
            while (current != null)
            {
                CanvasGroup group = current.GetComponent<CanvasGroup>();
                if (group != null) alpha *= group.alpha;
                if (current == hotspot) break;
                current = current.parent;
            }
            return alpha;
        }

        private static void AssertLatestReferenceBounds(RectTransform safe, RectTransform navigation,
            RectTransform faceSafeZone, RectTransform[] actions)
        {
            const float tolerance = 3f;
            RectTransform[] headerVisuals =
            {
                RequireRect("Profile"),
                RequireRect("Currency-diamond"),
                RequireRect("Currency-gold"),
                RequireRect("Currency-stamina"),
                RequireRect("Settings"),
            };
            AssertTopLeftBounds(safe, CombinedRectRelativeTo(safe, headerVisuals),
                7, 7, 700, 93, tolerance, "顶部 HUD 整组");
            AssertTopLeftBounds(safe, headerVisuals[0], 7, 10, 194, 90, tolerance, "玩家信息");
            AssertTopLeftBounds(safe, headerVisuals[1], 209, 20, 137, 53, tolerance, "钻石");
            AssertTopLeftBounds(safe, headerVisuals[2], 356, 21, 141, 52, tolerance, "金币");
            AssertTopLeftBounds(safe, headerVisuals[3], 498, 20, 140, 55, tolerance, "体力");
            AssertTopLeftBounds(safe, headerVisuals[4], 640, 7, 67, 80, tolerance, "设置");
            Assert.That(GameObject.Find("Mail"), Is.Null, "720x1536 最新参考不得显示 Mail。");

            Image golden = RequireApprovedLobbyGolden();
            AssertTopLeftBounds(safe, golden.rectTransform,
                0, 0, 720, 1536, .5f, "0.3.8 首页 golden");
            Assert.That(golden.preserveAspect, Is.True,
                "0.3.8 整图必须原比例显示，不能纵向压扁来填满画布。");
            AssertTopLeftBounds(safe, faceSafeZone,
                318, 184, 217, 267, tolerance, "角色脸部安全区");

            int[,] hitBounds =
            {
                { 12, 506, 283, 251 },
                { 16, 758, 274, 245 },
                { 19, 1003, 265, 196 },
                { 354, 834, 365, 464 },
            };
            for (int index = 0; index < actions.Length; index++)
            {
                AssertTopLeftBounds(safe, actions[index],
                    hitBounds[index, 0], hitBounds[index, 1], hitBounds[index, 2], hitBounds[index, 3],
                    tolerance, actions[index].name + " 点击区");
            }

            AssertTopLeftBounds(safe, navigation,
                12, 1318, 696, 184, tolerance, "底部导航点击根区");

            string[] currencies = { "diamond", "gold", "stamina" };
            float[,] plusBounds =
            {
                { 306.9f, 36.6f, 36.8f, 42.1f },
                { 456.0f, 37.5f, 33.3f, 42.1f },
                { 593.7f, 37.5f, 36.8f, 43.0f },
            };
            for (int index = 0; index < currencies.Length; index++)
                AssertTopLeftBounds(safe, RequireButtonRect("CurrencyPlus-" + currencies[index]),
                    plusBounds[index, 0], plusBounds[index, 1], plusBounds[index, 2], plusBounds[index, 3],
                    1.5f, currencies[index] + " 独立加号点击区");
        }

        private static Image RequireApprovedLobbyGolden()
        {
            const string path = "Art/Reference038/lobby-board-038";
            Texture expected = Resources.Load<Sprite>(path)?.texture ?? Resources.Load<Texture2D>(path);
            Assert.That(expected, Is.Not.Null, "未导入 0.3.8 首页 golden：" + path);
            Image[] matches = Object.FindObjectsByType<Image>(FindObjectsInactive.Exclude)
                .Where(image => image.sprite != null && image.sprite.texture == expected)
                .ToArray();
            Assert.That(matches.Length, Is.EqualTo(1), "首页必须且只能显示一张 0.3.8 golden 整图。");
            Assert.That(matches[0].raycastTarget, Is.False, "0.3.8 golden 不得拦截任何点击热区。");
            return matches[0];
        }

        private static Rect CombinedRectRelativeTo(RectTransform coordinateSpace, RectTransform[] rects)
        {
            Assert.That(rects, Is.Not.Empty);
            Rect combined = RectRelativeTo(coordinateSpace, rects[0]);
            for (int index = 1; index < rects.Length; index++)
            {
                Rect next = RectRelativeTo(coordinateSpace, rects[index]);
                combined = Rect.MinMaxRect(Mathf.Min(combined.xMin, next.xMin), Mathf.Min(combined.yMin, next.yMin),
                    Mathf.Max(combined.xMax, next.xMax), Mathf.Max(combined.yMax, next.yMax));
            }
            return combined;
        }

        private static void AssertTopLeftBounds(RectTransform coordinateSpace, RectTransform rect,
            float x, float y, float width, float height, float tolerance, string label)
        {
            AssertTopLeftBounds(coordinateSpace, RectRelativeTo(coordinateSpace, rect),
                x, y, width, height, tolerance, label);
        }

        private static void AssertTopLeftBounds(RectTransform coordinateSpace, Rect actual,
            float x, float y, float width, float height, float tolerance, string label)
        {
            float actualX = actual.xMin - coordinateSpace.rect.xMin;
            float actualY = coordinateSpace.rect.yMax - actual.yMax;
            Assert.That(actualX, Is.EqualTo(x).Within(tolerance), label + " x 与最新参考不符。");
            Assert.That(actualY, Is.EqualTo(y).Within(tolerance), label + " y 与最新参考不符。");
            Assert.That(actual.width, Is.EqualTo(width).Within(tolerance), label + " 宽度与最新参考不符。");
            Assert.That(actual.height, Is.EqualTo(height).Within(tolerance), label + " 高度与最新参考不符。");
        }

        private static void AssertAuthoredAuditionFitsContent(RectTransform content, string context)
        {
            RectTransform page = RequireRect("AuthoredInterviewPage");
            Image board = RequireRect("AuditionReferenceBoard").GetComponent<Image>();
            Assert.That(board.sprite, Is.Not.Null);
            Assert.That(board.preserveAspect, Is.True);
            Assert.That(page.localScale.x, Is.EqualTo(page.localScale.y).Within(.001f));
            // Background artwork deliberately reaches into the header/footer; actual controls and
            // live candidate values must stay inside the safe content region at both aspect ratios.
            string[] controls = { "InterviewPool-0", "InterviewPool-1", "PreviousCandidate", "NextCandidate",
                "ViewInterview", "SignCandidate" };
            for (int i = 0; i < controls.Length; i++)
            {
                RectTransform button = RequireButtonRect(controls[i]);
                AssertContained(content, button, context + " " + controls[i]);
                Assert.That(button.GetComponent<Button>().targetGraphic.raycastTarget, Is.True);
                for (int j = i + 1; j < controls.Length; j++)
                    Assert.That(RectRelativeTo(content, button)
                        .Overlaps(RectRelativeTo(content, RequireButtonRect(controls[j]))), Is.False,
                        context + $" 的 {controls[i]} 与 {controls[j]} 点击区域不能重叠。");
            }
            foreach (string name in new[] { "CandidatePortrait", "CandidateName", "CandidateCounter", "InterviewRefresh", "SigningPrice" })
                AssertContained(content, RequireRect(name), context + " " + name);
            foreach (string key in new[] { "Vocal", "Rhythm", "Presence", "Resonance", "Charm" })
            {
                RectTransform label = RequireRect("StatLabel-" + key);
                RectTransform value = RequireRect("StatValue-" + key);
                AssertContained(content, label, context + " 五维属性标题");
                AssertContained(content, value, context + " 五维属性数值");
                Assert.That(RectRelativeTo(content, label).Overlaps(RectRelativeTo(content, value)), Is.False,
                    context + " 的属性名称与数值不可相互覆盖。");
                Assert.That(value.GetComponent<Text>().text, Is.Not.Empty);
            }
        }

        private static void AssertEquipmentTextFitsWithoutOverlap(RectTransform body)
        {
            Text[] labels = body.GetComponentsInChildren<Text>()
                .Where(text => !string.IsNullOrWhiteSpace(text.text)).ToArray();
            foreach (Text label in labels)
            {
                AssertContained(label.rectTransform.parent as RectTransform, label.rectTransform, label.name);
                Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + 1f),
                    $"装备文字 {label.name} 被行高裁切：{label.text}");
            }
            for (int first = 0; first < labels.Length; first++)
            for (int second = first + 1; second < labels.Length; second++)
                Assert.That(RectRelativeTo(body, labels[first].rectTransform)
                    .Overlaps(RectRelativeTo(body, labels[second].rectTransform)), Is.False,
                    $"装备页文字 {labels[first].name} 与 {labels[second].name} 不应重叠。");
        }

        private static void AssertContained(RectTransform parent, RectTransform child, string label)
        {
            Rect parentRect = parent.rect;
            Rect childRect = RectRelativeTo(parent, child);
            Assert.That(childRect.xMin, Is.GreaterThanOrEqualTo(parentRect.xMin - PositionTolerance),
                $"{label} 的左侧溢出父容器。");
            Assert.That(childRect.xMax, Is.LessThanOrEqualTo(parentRect.xMax + PositionTolerance),
                $"{label} 的右侧溢出父容器。");
            Assert.That(childRect.yMin, Is.GreaterThanOrEqualTo(parentRect.yMin - PositionTolerance),
                $"{label} 的底部溢出父容器。");
            Assert.That(childRect.yMax, Is.LessThanOrEqualTo(parentRect.yMax + PositionTolerance),
                $"{label} 的顶部溢出父容器。");
        }

        private static void AssertVerticallyContained(RectTransform parent, RectTransform child, string label)
        {
            Rect parentRect = parent.rect;
            Rect childRect = RectRelativeTo(parent, child);
            Assert.That(childRect.yMin, Is.GreaterThanOrEqualTo(parentRect.yMin - PositionTolerance),
                $"{label} 的底部溢出内容区。");
            Assert.That(childRect.yMax, Is.LessThanOrEqualTo(parentRect.yMax + PositionTolerance),
                $"{label} 的顶部溢出内容区。");
        }

        private static Rect RectInParent(RectTransform rect)
        {
            RectTransform parent = rect.parent as RectTransform;
            Assert.That(parent, Is.Not.Null, $"{rect.name} must have a RectTransform parent.");
            return RectRelativeTo(parent, rect);
        }

        private static Rect RectRelativeTo(RectTransform coordinateSpace, RectTransform rect)
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector3 first = coordinateSpace.InverseTransformPoint(corners[0]);
            float minX = first.x;
            float maxX = first.x;
            float minY = first.y;
            float maxY = first.y;
            for (int index = 1; index < corners.Length; index++)
            {
                Vector3 point = coordinateSpace.InverseTransformPoint(corners[index]);
                minX = Mathf.Min(minX, point.x);
                maxX = Mathf.Max(maxX, point.x);
                minY = Mathf.Min(minY, point.y);
                maxY = Mathf.Max(maxY, point.y);
            }

            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        private static Text FindText(RectTransform parent, string value)
        {
            Text result = parent.GetComponentsInChildren<Text>(true)
                .SingleOrDefault(candidate => candidate.text == value);
            Assert.That(result, Is.Not.Null, $"Expected text '{value}' under {parent.name} was not found.");
            return result;
        }

        private static bool HasLockedPresentation(RectTransform entry)
        {
            bool namedLock = entry.GetComponentsInChildren<Transform>(true)
                .Any(item => item.name.ToLowerInvariant().Contains("lock"));
            bool copyLock = entry.GetComponentsInChildren<Text>(true)
                .Any(text => text.text.Contains("即将") || text.text.Contains("未开放") || text.text.Contains("锁定"));
            return namedLock || copyLock || !entry.GetComponent<Button>().IsInteractable();
        }

        private static void AssertDecorationsDoNotStealRaycasts(RectTransform action)
        {
            Button button = action.GetComponent<Button>();
            Assert.That(button, Is.Not.Null);
            foreach (Graphic graphic in action.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic == button.targetGraphic) continue;
                Assert.That(graphic.raycastTarget, Is.False,
                    $"{action.name}/{graphic.name} 是装饰层，不应抢占 {action.name} 的射线。");
            }
        }

        private static bool IsVisibleMemberTaxonomy(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            bool hasRace = value.Contains("魅族") || value.Contains("魔族") ||
                           value.Contains("海灵族") || value.Contains("血精灵");
            bool hasCareer = MemberCareers.All.Any(value.Contains);
            return hasRace && hasCareer;
        }

        private static bool IsLegacyVisibleRarity(string value)
        {
            return value == "SSR" || value == "SR" || value == "R";
        }

        private static RectTransform RequireButtonRect(string objectName)
        {
            RectTransform result = RequireRect(objectName);
            Button button = result.GetComponent<Button>();
            Assert.That(button, Is.Not.Null, $"{objectName} must expose a Button component.");
            Assert.That(button.isActiveAndEnabled, Is.True, $"{objectName} must be active and enabled.");
            Assert.That(button.targetGraphic, Is.Not.Null, $"{objectName} must have a target graphic.");
            Assert.That(button.targetGraphic.raycastTarget, Is.True,
                $"{objectName} must receive UI raycasts across its hit area.");
            return result;
        }

        private static RectTransform RequireRect(string objectName)
        {
            GameObject result = GameObject.Find(objectName);
            Assert.That(result, Is.Not.Null, $"Expected active UI object '{objectName}' was not found.");
            RectTransform rect = result.GetComponent<RectTransform>();
            Assert.That(rect, Is.Not.Null, $"{objectName} must have a RectTransform.");
            return rect;
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
