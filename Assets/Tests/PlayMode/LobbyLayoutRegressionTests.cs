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
            Text powerValue = RequireRect("TeamPowerValue").GetComponent<Text>();
            Assert.That(powerValue, Is.Not.Null);
            Assert.That(powerValue.text, Is.Not.Empty, "编队总战力数字不能为空。");
            Assert.That(powerValue.verticalOverflow, Is.EqualTo(VerticalWrapMode.Overflow),
                "总战力大号数字不能被字体行高裁掉。");
            RectTransform power = RequireRect("TeamPower");
            RectTransform synergy = RequireRect("TeamSynergy");
            Assert.That(power.GetComponent<Image>().sprite, Is.Not.Null,
                "总战力必须装配完整美术框体，不能退回纯色卡片。");
            Assert.That(synergy.GetComponent<Image>().sprite, Is.Not.Null,
                "协同效果必须装配完整美术框体，不能退回纯色卡片。");
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
            Assert.That(RequireRect("AccessoryPreview").GetComponent<Image>().color.a,
                Is.LessThan(0.2f), "饰品角色预览不应恢复为大块实色底卡。");
        }

        [UnityTest]
        public IEnumerator TeamAndMemberLayoutsStayInsideShortAndStandardPortraitContent()
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
                RequireButtonRect("冒险剧本"),
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
            Assert.That(settingsRect.xMin - mailRect.xMax, Is.InRange(0f, 8f),
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
            Assert.That(staminaValueRect.width, Is.LessThanOrEqualTo(80f + PositionTolerance),
                "体力文本不应为已删除的倒计时保留空白宽度。");
            Assert.That(diamondValueRect.xMin - diamondIconRect.xMax, Is.InRange(0f, 4f));
            Assert.That(goldIconRect.xMin - diamondValueRect.xMax, Is.InRange(0f, 8f));
            Assert.That(goldValueRect.xMin - goldIconRect.xMax, Is.InRange(0f, 4f));
            Assert.That(staminaIconRect.xMin - goldValueRect.xMax, Is.InRange(0f, 8f));
            Assert.That(staminaValueRect.xMin - staminaIconRect.xMax, Is.InRange(0f, 4f));

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

        private static void AssertContained(RectTransform parent, RectTransform child, string label)
        {
            Rect parentRect = parent.rect;
            Rect childRect = RectRelativeTo(parent, child);
            Assert.That(childRect.xMin, Is.GreaterThanOrEqualTo(parentRect.xMin - PositionTolerance),
                $"{label} 的左侧溢出 LiveOnStage 热区。");
            Assert.That(childRect.xMax, Is.LessThanOrEqualTo(parentRect.xMax + PositionTolerance),
                $"{label} 的右侧溢出 LiveOnStage 热区。");
            Assert.That(childRect.yMin, Is.GreaterThanOrEqualTo(parentRect.yMin - PositionTolerance),
                $"{label} 的底部溢出 LiveOnStage 热区。");
            Assert.That(childRect.yMax, Is.LessThanOrEqualTo(parentRect.yMax + PositionTolerance),
                $"{label} 的顶部溢出 LiveOnStage 热区。");
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
