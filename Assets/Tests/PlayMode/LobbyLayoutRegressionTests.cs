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

            RectTransform backdrop = RequireRect("TeamStellarBackground");
            Image backdropImage = backdrop.GetComponent<Image>();
            AspectRatioFitter fitter = backdrop.GetComponent<AspectRatioFitter>();
            Assert.That(backdropImage, Is.Not.Null);
            Assert.That(backdropImage.sprite, Is.Not.Null,
                "星环编队必须使用本地 AI 舞台底图，不能回退为空背景。");
            Assert.That(fitter, Is.Not.Null,
                "星环舞台背景必须由 AspectRatioFitter 保持原始比例。");
            Assert.That(fitter.aspectMode, Is.EqualTo(AspectRatioFitter.AspectMode.EnvelopeParent),
                "星环舞台应等比覆盖内容区，不能被强行拉伸。");

            for (int slot = 0; slot < GameModel.TeamCapacity; slot++)
            {
                RectTransform orbit = RequireButtonRect($"TeamOrbit-{slot}");
                Assert.That(orbit.GetComponent<Image>().color.a, Is.LessThanOrEqualTo(0.08f),
                    $"TeamOrbit-{slot} 点击层应接近透明，不能恢复成大块纯色卡片。");
                RequireRect($"TeamCharacter-{slot}");
            }

            RequireRect("TeamLeader");
            RectTransform titlePlaque = RequireRect("TeamTitlePlaque");
            RectTransform teamIndex = RequireRect("TeamIndexLabel");
            RectTransform power = RequireRect("TeamPower");
            AssertContained(titlePlaque, teamIndex, "编队编号");
            Rect titlePlaqueRect = RectInParent(titlePlaque);
            Rect teamIndexRect = RectRelativeTo(titlePlaque.parent as RectTransform, teamIndex);
            Rect powerRect = RectInParent(power);
            Assert.That(teamIndexRect.xMin - titlePlaqueRect.xMin,
                Is.GreaterThanOrEqualTo(20f - PositionTolerance),
                "编队编号必须保留清晰的20px内边距。");
            Assert.That(teamIndexRect.Overlaps(powerRect), Is.False,
                "编队编号不能侵入右侧总战力卡。");
            Text powerValue = RequireRect("TeamPowerValue").GetComponent<Text>();
            Assert.That(powerValue, Is.Not.Null);
            Assert.That(powerValue.text, Is.Not.Empty, "编队总战力数字不能为空。");
            Assert.That(powerValue.resizeTextForBestFit, Is.True,
                "总战力数字必须在数值增长后仍受控缩放，不能溢出美术框。");
            Assert.That(powerValue.verticalOverflow, Is.EqualTo(VerticalWrapMode.Truncate),
                "总战力必须受内容框约束，不能侵入下一行。");
            Assert.That(powerValue.preferredHeight, Is.LessThanOrEqualTo(powerValue.rectTransform.rect.height + 1f),
                "总战力大号数字仍需有足够行高，不得靠截断隐藏数字。");
            Text resonanceValue = RequireRect("TeamResonanceValue").GetComponent<Text>();
            Text memberCount = RequireRect("TeamMemberCount").GetComponent<Text>();
            Assert.That(resonanceValue.resizeTextForBestFit, Is.True,
                "共鸣评分动态文案必须允许受控缩字号。");
            Assert.That(memberCount.resizeTextForBestFit, Is.True,
                "成员数量动态文案必须允许受控缩字号。");
            RectTransform synergy = RequireRect("TeamSynergy");
            Assert.That(resonanceValue.text, Does.StartWith("职业种类"));
            for (int index = 0; index < MemberCareers.All.Count; index++)
            {
                RectTransform careerStatus = RequireRect("CareerStatus-" + index);
                AssertContained(synergy, careerStatus, "职业就位提示");
                Assert.That(careerStatus.GetComponent<Text>().text, Does.StartWith(MemberCareers.All[index]));
            }
            Assert.That(power.GetComponent<Outline>(), Is.Not.Null, "总战力保持精细边界。");
            Assert.That(synergy.GetComponent<Outline>(), Is.Not.Null, "职业配置保持精细边界。");
            Assert.That(power.GetComponent<Image>().color.a, Is.GreaterThan(.8f));
            Assert.That(synergy.GetComponent<Image>().color.a, Is.GreaterThan(.8f));
            foreach (RectTransform container in new[] { titlePlaque, power, synergy })
            {
                Text[] information = container.GetComponentsInChildren<Text>().Where(t => !string.IsNullOrWhiteSpace(t.text)).ToArray();
                foreach (Text label in information) AssertContained(container, label.rectTransform, label.name);
                for (int a = 0; a < information.Length; a++)
                for (int b = a + 1; b < information.Length; b++)
                    Assert.That(RectRelativeTo(container, information[a].rectTransform)
                        .Overlaps(RectRelativeTo(container, information[b].rectTransform)), Is.False,
                        $"团队信息 {information[a].name} 与 {information[b].name} 不得重叠。");
            }
            RequireButtonRect("ChangeLeader");
            RequireButtonRect("AutoTeam");
            AssertContained(RequireRect("Content"), synergy, "协同效果");
        }

        [UnityTest]
        public IEnumerator MemberAndAccessoryPagesUseCalmAspectSafeBackgrounds()
        {
            RequireButtonRect("Nav-members").GetComponent<Button>().onClick.Invoke();
            yield return null;
            RectTransform memberBackdrop = RequireRect("MemberGalleryBackground");
            Assert.That(memberBackdrop.GetComponent<Image>().sprite, Is.Not.Null,
                "成员页必须加载指定的低干扰舞台背景。");
            Assert.That(memberBackdrop.GetComponent<AspectRatioFitter>()?.aspectMode,
                Is.EqualTo(AspectRatioFitter.AspectMode.EnvelopeParent),
                "成员页背景必须等比覆盖，不能拉伸。");
            Assert.That(RequireRect("Member-" + GameModel.Members[0].Id).GetComponent<Image>().color.a,
                Is.LessThan(0.4f), "成员图鉴卡应使用低透明深蓝玻璃。");

            RequireButtonRect("Nav-accessory").GetComponent<Button>().onClick.Invoke();
            yield return null;
            RectTransform accessoryBackdrop = RequireRect("AccessoryDressingRoomStage");
            Assert.That(accessoryBackdrop.GetComponent<Image>().sprite, Is.Not.Null,
                "饰品页必须加载指定的低干扰舞台背景。");
            Assert.That(accessoryBackdrop.GetComponent<AspectRatioFitter>()?.aspectMode,
                Is.EqualTo(AspectRatioFitter.AspectMode.EnvelopeParent),
                "饰品页背景必须等比覆盖，不能拉伸。");
            string[] accessoryLabels = RequireRect("Content").GetComponentsInChildren<Text>(true)
                .Select(text => text.text)
                .ToArray();
            Assert.That(accessoryLabels, Does.Contain("舞台饰品"),
                "饰品页眉应准确描述当前功能。");
            Assert.That(accessoryLabels, Does.Not.Contain("饰品与设置"),
                "设置已经统一到顶部齿轮，饰品页不能继续使用旧的混合页标题。");
            Assert.That(RequireRect("EquipmentSelectedArt").GetComponent<Image>().preserveAspect,
                Is.True, "角色装备选中饰品图必须保持素材比例。");
            Assert.That(GameObject.Find("AccessoryPreviewArt"), Is.Null,
                "新装备页不可叠回自带栏位与字样的旧预览框体。");
            Assert.That(RequireRect("EquipmentScroll").GetComponent<RectMask2D>(), Is.Not.Null);
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
                    AssertContained(profile, RequireRect("MemberAcquireGuide"),
                        $"成员第 {visitedPages + 1} 页培养或获取说明");
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
                RectTransform viewport = RequireRect("EquipmentScroll");
                RectTransform equipmentBody = RequireRect("EquipmentBody");
                ScrollRect scroll = viewport.GetComponent<ScrollRect>();
                Assert.That(scroll, Is.Not.Null);
                Assert.That(scroll.viewport, Is.EqualTo(viewport));
                Assert.That(scroll.content, Is.EqualTo(equipmentBody));
                Assert.That(scroll.vertical, Is.True);
                Assert.That(scroll.horizontal, Is.False);
                Assert.That(scroll.movementType, Is.EqualTo(ScrollRect.MovementType.Clamped));
                Assert.That(viewport.GetComponent<RectMask2D>(), Is.Not.Null,
                    "短屏必须裁剪滚动区，不能让饰品内容压住底部导航。");
                AssertContained(content, viewport, $"{targetHeight} 高度下的装备滚动视口");
                AssertContained(equipmentBody, RequireRect("EquipmentMemberSelector"), "装备角色切换");
                AssertContained(equipmentBody, RequireRect("AccessoryDetail"), "角色装备详情");
                for (int index = 0; index < 6; index++)
                    AssertContained(equipmentBody, RequireButtonRect($"Accessory-{index}"),
                        $"{targetHeight} 高度下的饰品图鉴卡 {index + 1}");
                AssertEquipmentTextFitsWithoutOverlap(equipmentBody);
                scroll.verticalNormalizedPosition = 0f;
                Canvas.ForceUpdateCanvases();
                yield return null;
                AssertContained(viewport, RequireRect("EquipmentPageCount"),
                    "滚动到底部后必须能完整看到分页信息");
                AssertContained(viewport, RequireButtonRect("Accessory-5"),
                    "滚动到底部后必须能完整点击最后一件饰品");

                RequireButtonRect("Nav-audition").GetComponent<Button>().onClick.Invoke();
                yield return null;
                content = RequireRect("Content");
                RectTransform onlinePool = RequireButtonRect("InterviewPool-0");
                RectTransform offlinePool = RequireButtonRect("InterviewPool-1");
                RectTransform candidate = RequireRect("CandidateCard");
                RectTransform actions = RequireRect("InterviewActions");
                AssertContained(content, onlinePool, $"{targetHeight} 高度下的线上面试入口");
                AssertContained(content, offlinePool, $"{targetHeight} 高度下的线下面试入口");
                AssertContained(content, RequireRect("InterviewRefresh"),
                    $"{targetHeight} 高度下的候选刷新信息");
                AssertContained(content, candidate, $"{targetHeight} 高度下的候选主卡");
                AssertContained(content, actions, $"{targetHeight} 高度下的签约操作区");
                AssertVerticallyContained(content, RequireButtonRect("PreviousCandidate"),
                    $"{targetHeight} 高度下的上一位候选");
                AssertVerticallyContained(content, RequireButtonRect("NextCandidate"),
                    $"{targetHeight} 高度下的下一位候选");
                Assert.That(RectInParent(onlinePool).Overlaps(RectInParent(offlinePool)), Is.False,
                    "线上与线下面试入口不能重叠。");
                Assert.That(RectInParent(candidate).Overlaps(RectInParent(actions)), Is.False,
                    $"{targetHeight} 高度下候选主卡不能侵入签约操作区。");
                AssertContained(candidate, RequireRect("CandidateStats"),
                    $"{targetHeight} 高度下的候选能力值");
                AssertContained(candidate, RequireRect("CandidateCharm"),
                    $"{targetHeight} 高度下的魅力与收益信息");
                AssertContained(actions, RequireButtonRect("ViewInterview"),
                    $"{targetHeight} 高度下的查看面试操作");
                AssertContained(actions, RequireButtonRect("SignCandidate"),
                    $"{targetHeight} 高度下的签约操作");
                foreach (RectTransform dot in actions.GetComponentsInChildren<RectTransform>(true)
                             .Where(rect => rect.name.StartsWith("CandidateDot-")))
                    AssertContained(actions, dot, $"{targetHeight} 高度下的候选分页点");

                offlinePool.GetComponent<Button>().onClick.Invoke();
                yield return null;
                content = RequireRect("Content");
                AssertContained(content, RequireRect("CandidateCard"),
                    $"{targetHeight} 高度下的线下候选主卡");
                AssertContained(content, RequireRect("InterviewActions"),
                    $"{targetHeight} 高度下的线下签约操作区");
                Assert.That(RectInParent(RequireRect("CandidateCard"))
                        .Overlaps(RectInParent(RequireRect("InterviewActions"))), Is.False,
                    $"{targetHeight} 高度下线下面试主卡不能侵入签约操作区。");
            }

            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, originalHeight);
            yield return null;
        }

        [UnityTest]
        public IEnumerator LobbyContentEntriesAreUniqueAiRenderedHotspots()
        {
            RectTransform[] hotspots =
            {
                RequireButtonRect("闪耀舞台"),
                RequireButtonRect("任务"),
            };

            for (int index = 0; index < hotspots.Length; index++)
            {
                Rect rect = RectInParent(hotspots[index]);
                Assert.That(rect.width, Is.InRange(240f - PositionTolerance, 280f + PositionTolerance),
                    $"{hotspots[index].name} 应保留完整 AI 全息装置的视觉尺寸，同时不能扩张成页面卡片。");
                Assert.That(rect.height, Is.InRange(220f - PositionTolerance, 250f + PositionTolerance),
                    $"{hotspots[index].name} 应保留完整 AI 全息装置的视觉尺寸，同时不能扩张成页面卡片。");
                Assert.That(hotspots[index].GetComponent<Image>().color.a, Is.LessThanOrEqualTo(0.01f),
                    $"{hotspots[index].name} 的点击层必须透明，让视频舞台保持完整。");
                Assert.That(hotspots[index].GetComponent<Mask>(), Is.Null,
                    $"{hotspots[index].name} 不应使用会形成实体卡片的遮罩。");
                Transform emblem = hotspots[index].Find("Emblem");
                Assert.That(emblem, Is.Not.Null, $"{hotspots[index].name} 必须保留 AI 生成的透明入口素材。");
                Assert.That(emblem.GetComponent<Image>()?.sprite, Is.Not.Null,
                    $"{hotspots[index].name} 的 AI 入口素材未成功载入。");
                FindText(hotspots[index], hotspots[index].name);
            }

            for (int first = 0; first < hotspots.Length; first++)
            for (int second = first + 1; second < hotspots.Length; second++)
                Assert.That(RectInParent(hotspots[first]).Overlaps(RectInParent(hotspots[second])), Is.False,
                    $"{hotspots[first].name} 与 {hotspots[second].name} 不应重叠。");

            Assert.That(Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude)
                    .Count(button => button.name == "LiveOnStage"), Is.EqualTo(1),
                "首页只能有一个开始演出主入口。");
            Transform stageFrame = RequireRect("LiveOnStage").Find("StageFrame");
            Assert.That(stageFrame, Is.Not.Null, "开始演出必须保留 AI 生成的主视觉素材。");
            Assert.That(stageFrame.GetComponent<Image>()?.sprite, Is.Not.Null,
                "开始演出的 AI 主视觉素材未成功载入。");
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
        public IEnumerator StageCallToActionCopyStaysInsideItsHitRect()
        {
            RectTransform stage = RequireRect("LiveOnStage");
            Text title = FindText(stage, "开始演出");
            Text subtitle = FindText(stage, "舞台已就绪");

            Rect stageRect = RectInParent(stage);
            Assert.That(stageRect.center.x, Is.EqualTo(0f).Within(PositionTolerance),
                "开始演出应居中成为首页唯一主操作。");

            AssertContained(stage, title.rectTransform, "开始演出");
            AssertContained(stage, subtitle.rectTransform, "舞台已就绪");
            Assert.That(title.horizontalOverflow, Is.EqualTo(HorizontalWrapMode.Wrap));
            Assert.That(title.verticalOverflow, Is.EqualTo(VerticalWrapMode.Truncate));
            Assert.That(subtitle.horizontalOverflow, Is.EqualTo(HorizontalWrapMode.Wrap));
            Assert.That(subtitle.verticalOverflow, Is.EqualTo(VerticalWrapMode.Truncate));
            yield return null;
        }

        [UnityTest]
        public IEnumerator TopBarOnlyKeepsCompactMailAndSettingsWithMusicInsideSettings()
        {
            RectTransform mail = RequireButtonRect("Mail");
            RectTransform settings = RequireButtonRect("Settings");
            RectTransform[] controls = { mail, settings };
            RectTransform topBar = RequireRect("TopBar");

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

            Rect mailRect = RectInParent(mail);
            Rect settingsRect = RectInParent(settings);
            Assert.That(settingsRect.xMin - mailRect.xMax,
                Is.InRange(-PositionTolerance, 9f),
                "邮件与设置应紧凑排列，不应为已移除的音乐按钮保留空槽。");

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
            Assert.That(diamondValueRect.xMin - diamondIconRect.xMax,
                Is.InRange(-PositionTolerance, 4f + PositionTolerance));
            Assert.That(goldValueRect.xMin - goldIconRect.xMax,
                Is.InRange(-PositionTolerance, 4f + PositionTolerance));
            Assert.That(staminaValueRect.xMin - staminaIconRect.xMax,
                Is.InRange(-PositionTolerance, 4f + PositionTolerance));
            Rect previousResource = default;
            foreach (string currency in new[] { "diamond", "gold", "stamina" })
            {
                RectTransform group = RequireButtonRect("Currency-" + currency);
                Assert.That(GameObject.Find("CurrencyPlus-" + currency), Is.Null);
                Assert.That(group.GetComponent<Button>().IsInteractable(), Is.True);
                Rect groupRect = RectInParent(group);
                Assert.That(groupRect.height, Is.GreaterThanOrEqualTo(44f));
                if (currency != "diamond")
                    Assert.That(groupRect.Overlaps(previousResource), Is.False,
                        "资源信息块不得侵入下一组。");
                previousResource = groupRect;
                string valueName = currency == "diamond" ? "Diamonds" : currency == "gold" ? "Gold" : "Stamina";
                AssertContained(group, RequireRect(valueName), "数字必须完整落在所属资源信息块内");
                Assert.That(RequireRect(valueName).GetComponent<Text>().raycastTarget, Is.False);
            }
            Assert.That(mailRect.xMin - previousResource.xMax, Is.InRange(-PositionTolerance, 24f),
                "邮件应紧跟体力信息块，不应保留旧加号空槽。");

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
        public IEnumerator BottomNavigationKeepsFiveEqualNonOverlappingDestinations()
        {
            string[] ids = { "team", "members", "lobby", "accessory", "audition" };
            string[] labels = { "团队", "成员", "大厅", "饰品", "选秀" };
            RectTransform navigation = RequireRect("BottomNavigation");
            var buttons = new RectTransform[ids.Length];
            int selectedCount = 0;

            for (int index = 0; index < ids.Length; index++)
            {
                buttons[index] = RequireButtonRect("Nav-" + ids[index]);
                AssertContained(navigation, buttons[index], labels[index]);
                FindText(buttons[index], labels[index]);
                Rect hitRect = RectInParent(buttons[index]);
                Assert.That(hitRect.width, Is.GreaterThanOrEqualTo(128f - PositionTolerance),
                    $"{labels[index]} 的点击热区不能因页面切换被压窄。");
                Assert.That(hitRect.height, Is.GreaterThanOrEqualTo(120f - PositionTolerance),
                    $"{labels[index]} 的点击热区必须覆盖图标与文字。");
                Image highlight = buttons[index].Find("Highlight")?.GetComponent<Image>();
                Assert.That(highlight, Is.Not.Null, $"{labels[index]} 缺少选中态指示。");
                if (highlight.color.a > 0.5f) selectedCount++;
            }

            float expectedWidth = RectInParent(buttons[0]).width;
            for (int first = 0; first < buttons.Length; first++)
            {
                Assert.That(RectInParent(buttons[first]).width,
                    Is.EqualTo(expectedWidth).Within(PositionTolerance),
                    "底部导航的五个入口必须保持等宽。");
                for (int second = first + 1; second < buttons.Length; second++)
                    Assert.That(RectInParent(buttons[first]).Overlaps(RectInParent(buttons[second])), Is.False,
                        $"{labels[first]} 与 {labels[second]} 的点击热区不能重叠。");
            }

            Assert.That(selectedCount, Is.EqualTo(1),
                "底部导航任何时刻只能显示一个选中入口。");
            yield return null;
        }

        [UnityTest]
        public IEnumerator ButtonsReceiveHoverPressAndExitScaleFeedback()
        {
            RectTransform mail = RequireButtonRect("Mail");
            ButtonInteractionFeedback feedback = mail.GetComponent<ButtonInteractionFeedback>();
            Assert.That(feedback, Is.Not.Null,
                "Canvas installer should attach feedback to dynamically built buttons.");

            float restingScale = mail.localScale.x;
            PointerEventData pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
            };

            ExecuteEvents.Execute<IPointerEnterHandler>(mail.gameObject, pointer,
                ExecuteEvents.pointerEnterHandler);
            yield return new WaitForSecondsRealtime(0.12f);
            float hoverScale = mail.localScale.x;
            Assert.That(hoverScale, Is.GreaterThan(restingScale + 0.01f),
                "Hover should visibly increase the button scale.");

            ExecuteEvents.Execute<IPointerDownHandler>(mail.gameObject, pointer,
                ExecuteEvents.pointerDownHandler);
            yield return new WaitForSecondsRealtime(0.16f);
            Assert.That(mail.localScale.x, Is.LessThan(restingScale),
                "Pointer down should rebound below the resting scale.");

            ExecuteEvents.Execute<IPointerExitHandler>(mail.gameObject, pointer,
                ExecuteEvents.pointerExitHandler);
            yield return new WaitForSecondsRealtime(0.22f);
            Assert.That(mail.localScale.x, Is.EqualTo(restingScale).Within(0.01f),
                "Pointer exit should restore the scale even when it follows pointer down.");

            yield return null;
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
