using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ChoSiren.Tests
{
    /// <summary>
    /// 0.3.8 acceptance coverage for reachable secondary UI states that the existing
    /// click-through suite does not exercise: insufficient-balance signing, the stamina
    /// refill exchange, the deep-talk result modal, member-picker search/pagination,
    /// and the chapter-map stamina icon beside the live value. Every test drives real
    /// Button/onEndEdit dispatches instead of calling application logic directly.
    /// </summary>
    public sealed class Qa038SecondaryRoutesPlayModeTests
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

            new GameObject("CHO-SIREN Secondary Route App").AddComponent<ChoSirenApp>();
            yield return null;

            Assert.That(Object.FindAnyObjectByType<ChoSirenApp>(), Is.Not.Null,
                "The app did not bootstrap for the secondary-route acceptance test.");
            RequireActiveObject("LobbyCards");
            Assert.That(EventSystem.current, Is.Not.Null,
                "Pointer-click acceptance tests require an active EventSystem.");
            float timeout = Time.realtimeSinceStartup + 15f;
            while ((!IsInteractable("LiveOnStage") || GameObject.Find("StartupLoading") != null) &&
                   Time.realtimeSinceStartup < timeout)
                yield return null;
            Assert.That(GameObject.Find("StartupLoading"), Is.Null,
                "Startup loading must finish before click-through acceptance begins.");
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

        /// <summary>签约流程验收：余额不足状态必须有自己的设计反馈，确认键禁用且不扣费。</summary>
        [UnityTest]
        public IEnumerator SignConfirmShowsInsufficientBalanceStateWithoutCharging()
        {
            GameModel appModel = AppModel();
            int candidateCost = -1;
            Click("Nav-audition");
            yield return null;
            RequireActiveObject("GachaPanel");

            // Pick the currently displayed candidate's actual quote, then drop the
            // wallet below it so the insufficient-balance branch is the one under test.
            int quoted = int.Parse(new string(RequireActiveObject("SigningPrice")
                .GetComponent<Text>().text.Where(char.IsDigit).ToArray()));
            candidateCost = quoted;
            appModel.Save.Diamonds = quoted - 1;
            int diamondsBefore = appModel.Save.Diamonds;

            Click("SignCandidate");
            yield return null;

            RequireActiveObject("SignConfirmCard");
            // 2026-11-01 visual-b: wallet row = diamond + held amount (red) + lock chip 「差 N」; button reads 星钻不足.
            Transform balance = RequireActiveObject("SignConfirmBalance").transform;
            Assert.That(balance.Find("From").GetComponent<Text>().text, Is.EqualTo(diamondsBefore.ToString("N0")),
                "余额提示必须展示真实持有数。");
            Assert.That(balance.Find("SignShortfall/Label").GetComponent<Text>().text,
                Is.EqualTo("差 " + (candidateCost - diamondsBefore).ToString("N0")),
                "余额不足时确认卡必须明确提示缺口，不能只悄悄禁用按钮。");
            Assert.That(RequireActiveObject("SignConfirm").GetComponentInChildren<Text>().text, Is.EqualTo("星钻不足"));
            Button confirm = RequireActiveObject("SignConfirm").GetComponent<Button>();
            Assert.That(confirm.IsInteractable(), Is.False,
                "余额不足时确认签约必须不可点击，杜绝欠费签约。");
            // Even a forced pointer dispatch on the disabled button must be a no-op.
            ExecuteEvents.Execute(confirm.gameObject,
                new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left },
                ExecuteEvents.pointerClickHandler);
            yield return null;
            Assert.That(appModel.Save.Diamonds, Is.EqualTo(diamondsBefore));
            Assert.That(GameObject.Find("SignResultCard"), Is.Null);

            Click("SignCancel");
            yield return null;
            Assert.That(GameObject.Find("SignConfirmCard"), Is.Null,
                "取消签约必须关闭确认卡回到候选页。");
            Assert.That(appModel.Save.Diamonds, Is.EqualTo(diamondsBefore),
                $"浏览并取消 {candidateCost} 报价不能产生扣费。");
        }

        /// <summary>资源弹窗验收：体力面板执行真实兑换动作，一次确认只扣一次星钻。</summary>
        [UnityTest]
        public IEnumerator StaminaRefillExchangeChargesDiamondsOnceAndRestoresStamina()
        {
            GameModel appModel = AppModel();
            appModel.Save.Stamina = 40;
            int diamondsBefore = appModel.Save.Diamonds;
            int cap = appModel.StaminaCap;

            Click("CurrencyPlus-stamina");
            yield return null;
            GameObject modal = RequireActiveObject("CurrencyModal");
            Assert.That(modal.GetComponentsInChildren<Text>(true)
                    .Any(text => text.name == "Title" && text.text == "体力"), Is.True,
                "体力加号必须打开体力资源面板。");
            Assert.That(RequireActiveObject("CurrencyBalance").GetComponent<Text>().text,
                Does.Contain("40"));

            Click("CurrencyExchange");
            yield return null;
            RequireActiveObject("CurrencyConfirmation");
            Assert.That(appModel.Save.Diamonds, Is.EqualTo(diamondsBefore),
                "展示报价确认不能提前扣费。");
            Assert.That(appModel.Save.Stamina, Is.EqualTo(40));

            Button submit = RequireActiveObject("ConfirmExchange").GetComponent<Button>();
            Click("ConfirmExchange");
            submit.onClick.Invoke(); // a queued duplicate must not charge twice
            yield return null;

            int expectedAmount = System.Math.Min(GameModel.StaminaRefillMaximum,
                System.Math.Max(0, cap - 40));
            Assert.That(appModel.Save.Stamina, Is.EqualTo(40 + expectedAmount),
                "补充体力必须按报价一次性到账。");
            Assert.That(appModel.Save.Diamonds, Is.EqualTo(diamondsBefore - expectedAmount),
                "体力兑换只能按报价扣一次星钻。");
            Assert.That(RequireActiveObject("CurrencyBalance").GetComponent<Text>().text,
                Does.Contain(appModel.Save.Stamina.ToString()),
                "兑换后资源面板必须展示新余额。");
            Click("CloseProgression");
            yield return null;
            AssertInactiveOrMissing("CurrencyModal");
        }

        /// <summary>全部遗漏 UI 验收：深入交流结果弹窗是可达二级状态，扣费一次且能回到档案。</summary>
        [UnityTest]
        public IEnumerator DeepTalkResultModalCompletesClickChainAndRestoresDossier()
        {
            GameModel appModel = AppModel();
            int memberIndex = appModel.Save.Team[0]; // signed member: DeepTalk requires unlock
            appModel.GainAffection(memberIndex, 80); // pushes past DeepTalkAffectionUnlock(80)
            int affectionBefore = appModel.AffectionOf(memberIndex);
            int goldBefore = appModel.Save.Gold;

            Click("Nav-members");
            yield return null;
            Click("Member-" + GameModel.Members[memberIndex].Id);
            yield return null;
            RequireActiveObject("MemberModal");

            Click("MemberDeepTalkButton");
            yield return null;

            GameObject modal = RequireActiveObject("DeepTalkModal");
            Assert.That(RequireActiveObject("DeepTalkDialogue").GetComponent<Text>().text,
                Is.Not.Empty, "深入交流必须展示真实羁绊文案而不是空白弹窗。");
            Assert.That(RequireActiveObject("DeepTalkAffectionResult").GetComponent<Text>().text,
                Does.Contain($"{affectionBefore} → {affectionBefore + GameModel.DeepTalkAffectionGain}"),
                "好感度变化必须显示真实前后数值。");
            Assert.That(appModel.Save.Gold, Is.EqualTo(goldBefore - GameModel.DeepTalkGoldCost),
                "深入交流只扣一次星光币。");
            Assert.That(appModel.AffectionOf(memberIndex),
                Is.EqualTo(affectionBefore + GameModel.DeepTalkAffectionGain));

            Click("BackToProfile");
            yield return null;
            Assert.That(GameObject.Find("DeepTalkModal"), Is.Null,
                "返回档案必须关闭结果弹窗。");
            RequireActiveObject("MemberModal");
            Assert.That(modal == null || !modal.activeInHierarchy, Is.True);
        }

        /// <summary>档案行动态验收：状态文案必须是真实可见渲染元素（非隐藏节点），
        /// 覆盖余额不足、好感未解锁、可交流、今日已用四种状态。</summary>
        [UnityTest]
        public IEnumerator ProfileActionStatesRenderVisibleTextNotHiddenStrings()
        {
            GameModel appModel = AppModel();
            int memberIndex = appModel.Save.Team[0];
            appModel.Save.Gold = 0; // 星光币不足：练习与深入交流都被真实资源条件卡住

            Click("Nav-members");
            yield return null;
            Click("Member-" + GameModel.Members[memberIndex].Id);
            yield return null;
            RequireActiveObject("MemberModal");

            // 星光币不足：训练按钮禁用，且原因必须画在按钮内而不是隐藏字段。
            GameObject train = RequireActiveObject("Train");
            Assert.That(train.GetComponent<Button>().IsInteractable(), Is.False);
            AssertVisibleStateLabel(train, "星光币不足", expectBlocked: true);

            // 好感未解锁：深入交流入口必须显示真实解锁条件，点击只能提示原因。
            GameObject deep = RequireActiveObject("MemberDeepTalkButton");
            AssertVisibleStateLabel(deep, $"好感{GameModel.DeepTalkAffectionUnlock}解锁",
                expectBlocked: true);
            Click("MemberDeepTalkButton");
            yield return null;
            Assert.That(GameObject.Find("DeepTalkModal"), Is.Null,
                "未解锁状态点击只能提示原因，不能打开结果弹窗。");

            // 解锁后（好感足够但星光币仍不足）：入口文案是可行动作，点击给真实提示且不扣费。
            appModel.GainAffection(memberIndex, GameModel.DeepTalkAffectionUnlock);
            InvokeApp("OpenMember", memberIndex);
            yield return null;
            deep = RequireActiveObject("MemberDeepTalkButton");
            AssertVisibleStateLabel(deep, "深入交流", expectBlocked: false);
            int goldBefore = appModel.Save.Gold;
            Click("MemberDeepTalkButton");
            yield return null;
            Assert.That(GameObject.Find("DeepTalkModal"), Is.Null,
                "星光币不足时只能提示 200 星光币需求，不能欠费开启。");
            Assert.That(appModel.Save.Gold, Is.EqualTo(goldBefore));

            // 补齐星光币走完整真实链路：成功 → 结果弹窗 → 返回档案 → 今日已交流。
            appModel.Save.Gold = GameModel.DeepTalkGoldCost;
            InvokeApp("OpenMember", memberIndex);
            yield return null;
            deep = RequireActiveObject("MemberDeepTalkButton");
            AssertVisibleStateLabel(deep, "深入交流", expectBlocked: false);
            Click("MemberDeepTalkButton");
            yield return null;
            RequireActiveObject("DeepTalkModal");
            Click("BackToProfile");
            yield return null;
            RequireActiveObject("MemberModal");
            AssertVisibleStateLabel(RequireActiveObject("MemberDeepTalkButton"),
                "今日已交流", expectBlocked: true);
        }

        /// <summary>角色选择验收：搜索/筛选在真实 64 人目录上可用。2026-11-01 起改为可滚动的两列大卡片网格，
        /// 不再分页：名单一次全部出现在滚动区内。</summary>
        [UnityTest]
        public IEnumerator MemberPickerPaginatesSearchesAndFiltersRealCatalog()
        {
            InvokeApp("OpenTeamSlotPicker", 0, 0);
            yield return null;
            RequireActiveObject("TeamMemberPicker");

            GameModel model = AppModel();
            int owned = model.Save.UnlockedMembers.Count;
            Assert.That(PickerRowCount(), Is.EqualTo(owned),
                "仅拥有筛选必须一次列出全部真实签约成员。");
            Assert.That(GameObject.Find("PickerNext"), Is.Null, "滚动网格不再有分页按钮。");
            Require("PickerScroll").GetComponent<ScrollRect>();

            Click("PickerOwnership");
            yield return null;
            Assert.That(PickerRowCount(), Is.EqualTo(GameModel.Members.Length),
                "全部名单必须覆盖真实目录的每一名成员。");
            ScrollRect scroll = Require("PickerScroll").GetComponent<ScrollRect>();
            Canvas.ForceUpdateCanvases();
            Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height),
                "全部名单超出一屏时必须可滚动。");

            Require("PickerSearch").GetComponent<InputField>().onEndEdit
                .Invoke(GameModel.Members[model.Save.Team[0]].Name);
            yield return null;
            Assert.That(GameObject.Find("PickMember-" + model.Save.Team[0]), Is.Not.Null,
                "搜索已拥有角色名必须能找到该成员。");
            Assert.That(Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude)
                    .Count(item => item.name.StartsWith("PickMember-")), Is.EqualTo(1),
                "精确姓名搜索应收敛到单个候选行。");

            Require("PickerSearch").GetComponent<InputField>().onEndEdit.Invoke("不存在zzz");
            yield return null;
            Require("PickerEmpty");
            Assert.That(Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude)
                    .Count(item => item.name.StartsWith("PickMember-")), Is.EqualTo(0),
                "无结果搜索必须展示空态而不是残留上一页列表。");
        }

        /// <summary>章节地图验收：体力数值旁必须有对应图标，且读数仍是实时 x/上限。</summary>
        [UnityTest]
        public IEnumerator ChapterMapKeepsStaminaIconBesideLiveValue()
        {
            var model = AppModel();
            model.Save.Stamina = 87;
            Click("LiveOnStage");
            yield return null;
            GameObject map = RequireActiveObject("LevelMapPanel");

            // The header pill has a same-named anchor; the map's icon is the one inside
            // LevelMapPanel and it must carry the actual stamina sprite.
            Image icon = map.GetComponentsInChildren<Image>(true)
                .FirstOrDefault(image => image.name == "StaminaIcon");
            Assert.That(icon, Is.Not.Null, "章节地图缺少 StaminaIcon 图标。");
            Assert.That(icon.sprite, Is.Not.Null, "体力数值旁必须有可识别图标。");
            Assert.That(icon.raycastTarget, Is.False, "体力图标不能吞掉地图点击。");

            Text value = icon.transform.parent.GetComponentsInChildren<Text>(true)
                .FirstOrDefault(text => text.text == $"{model.Save.Stamina}/{model.StaminaCap}");
            Assert.That(value, Is.Not.Null, "章节地图必须显示实时体力读数。");

            // Compare in the shared board space so authored-map scaling cannot skew the check.
            Rect iconRect = InParentSpace(icon.rectTransform);
            Rect valueRect = InParentSpace(value.rectTransform);
            Assert.That(valueRect.xMin - iconRect.xMax, Is.LessThan(80f),
                "体力图标必须紧贴数值左侧，而不是漂到别处。");
            Assert.That(valueRect.xMin - iconRect.xMax, Is.GreaterThan(-iconRect.width),
                "体力图标不能被数值完全盖住。");
            Assert.That(iconRect.yMax, Is.GreaterThan(valueRect.yMin),
                "体力图标与数值必须处于同一行。");
            Assert.That(iconRect.yMin, Is.LessThan(valueRect.yMax),
                "体力图标与数值必须处于同一行。");

            Click("Back");
            yield return null;
            AssertInactiveOrMissing("LevelMapPanel");
            RequireActiveObject("LobbyCards");
        }

        private static int PickerRowCount()
        {
            return Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude)
                .Count(item => item.name.StartsWith("PickMember-"));
        }

        /// <summary>档案行动态必须是真实渲染的元素：StateChip 背板 + StateLabel 文字
        /// 都在按钮内、激活、不透明；禁止用隐藏节点冒充可见状态。</summary>
        private static void AssertVisibleStateLabel(GameObject action, string expected, bool expectBlocked)
        {
            var button = (RectTransform)action.transform;

            Transform chipNode = action.transform.Find("StateChip");
            Assert.That(chipNode, Is.Not.Null,
                $"{action.name} 缺少 StateChip 背板——状态必须画出来，不能只有语义字段。");
            Assert.That(chipNode.gameObject.activeInHierarchy, Is.True);
            var chipGraphic = chipNode.GetComponent<Graphic>();
            Assert.That(chipGraphic, Is.Not.Null);
            Assert.That(chipGraphic.color.a, Is.GreaterThan(0.5f),
                $"{action.name} StateChip 不可见（alpha 过低）。");

            Transform labelNode = action.transform.Find("StateLabel");
            Assert.That(labelNode, Is.Not.Null,
                $"{action.name} 缺少 StateLabel——状态原因必须是真实可见文字。");
            Assert.That(labelNode.gameObject.activeInHierarchy, Is.True,
                $"{action.name} StateLabel 被禁用——玩家看不到状态。");
            var label = labelNode.GetComponent<Text>();
            Assert.That(label, Is.Not.Null, $"{action.name} StateLabel 不是 Text。");
            Assert.That(label.text, Is.EqualTo(expected),
                $"{action.name} 状态文案应为 '{expected}'，实际 '{label.text}'。");
            Assert.That(label.color.a, Is.GreaterThan(0.2f),
                $"{action.name} StateLabel 文字透明——等于没有状态提示。");
            if (expectBlocked)
            {
                Assert.That(label.color.r, Is.GreaterThan(label.color.b),
                    $"{action.name} 受阻原因应使用 Pink 提示色，实际 {label.color}。");
            }
            else
            {
                Assert.That(label.color.b, Is.GreaterThan(label.color.r),
                    $"{action.name} 可行动作应使用 Cyan 提示色，实际 {label.color}。");
            }

            Rect labelRect = InParentSpace((RectTransform)labelNode);
            Rect actionRect = button.rect;
            const float tolerance = 1.5f;
            Assert.That(labelRect.xMin, Is.GreaterThanOrEqualTo(actionRect.xMin - tolerance),
                $"{action.name} StateLabel 越出按钮左缘。");
            Assert.That(labelRect.xMax, Is.LessThanOrEqualTo(actionRect.xMax + tolerance),
                $"{action.name} StateLabel 越出按钮右缘。");
            Assert.That(labelRect.yMin, Is.GreaterThanOrEqualTo(actionRect.yMin - tolerance),
                $"{action.name} StateLabel 越出按钮底缘。");
            Assert.That(labelRect.yMax, Is.LessThanOrEqualTo(actionRect.yMax + tolerance),
                $"{action.name} StateLabel 越出按钮顶缘。");
            Assert.That(labelRect.width, Is.GreaterThan(40f),
                $"{action.name} StateLabel 宽度不足以显示状态原因。");
            Assert.That(labelRect.height, Is.GreaterThan(12f),
                $"{action.name} StateLabel 高度不足以显示状态原因。");
        }

        private static Rect InParentSpace(RectTransform child)
        {
            var corners = new Vector3[4];
            child.GetWorldCorners(corners);
            Transform parent = child.parent;
            Vector3 min = parent.InverseTransformPoint(corners[0]);
            Vector3 max = parent.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static GameModel AppModel()
        {
            var app = Object.FindAnyObjectByType<ChoSirenApp>();
            var field = typeof(ChoSirenApp).GetField("model",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (GameModel)field.GetValue(app);
        }

        private void InvokeApp(string name, params object[] args)
        {
            var app = Object.FindAnyObjectByType<ChoSirenApp>();
            var method = typeof(ChoSirenApp).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Route method '{name}' no longer exists on ChoSirenApp.");
            method.Invoke(app, args);
        }

        private static void Click(string objectName)
        {
            GameObject target = RequireActiveObject(objectName);
            Button button = target.GetComponent<Button>();
            Assert.That(button, Is.Not.Null, $"'{objectName}' exists but has no Button component.");
            Assert.That(button.enabled, Is.True, $"'{objectName}' has a disabled Button component.");
            Assert.That(button.IsInteractable(), Is.True, $"'{objectName}' is not interactable.");
            Assert.That(button.targetGraphic, Is.Not.Null);
            Assert.That(button.targetGraphic.raycastTarget, Is.True);
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

        private static GameObject Require(string objectName) => RequireActiveObject(objectName);

        private static void AssertInactiveOrMissing(string objectName)
        {
            Assert.That(GameObject.Find(objectName), Is.Null,
                $"UI object '{objectName}' should have closed.");
        }

        private static bool IsInteractable(string objectName)
        {
            GameObject target = GameObject.Find(objectName);
            Button button = target != null ? target.GetComponent<Button>() : null;
            return button != null && button.isActiveAndEnabled && button.IsInteractable();
        }

        private static void DestroyAll<T>() where T : Component
        {
            foreach (T obj in Object.FindObjectsByType<T>(FindObjectsInactive.Include))
                Object.Destroy(obj.gameObject);
        }
    }
}
