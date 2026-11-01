using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ChoSiren.Tests
{
    /// <summary>
    /// 2026-11-01 (赵金 roster-ui): member detail switching (edge tabs, swipe, roster order, kept tab)
    /// and the near-full-screen two-column member picker, plus review captures in
    /// Builds/qa-logs/zj-1101b/roster-ui/ at phone (1080×2288) and Mac-window (1080×2160) ratios.
    /// </summary>
    public sealed class RosterUi1101Tests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private ChoSirenApp app;
        private GameModel model;
        private string save, legacy;
        private bool had, hadLegacy;

        private static string Out => Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/qa-logs/zj-1101b/roster-ui"));
        private static Type SwipeType => typeof(ChoSirenApp).Assembly.GetType("ChoSiren.MemberDetailSwipe1101");

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            had = PlayerPrefs.HasKey(GameModel.SaveKey); save = PlayerPrefs.GetString(GameModel.SaveKey);
            hadLegacy = PlayerPrefs.HasKey(GameModel.LegacySaveKey); legacy = PlayerPrefs.GetString(GameModel.LegacySaveKey);
            PlayerPrefs.SetString(GameModel.SaveKey, JsonUtility.ToJson(new GameSave
                { HomeCharacterIndex = 0, StoryProgress = 100, MusicEnabled = false, SfxEnabled = false }));
            foreach (var old in Object.FindObjectsByType<ChoSirenApp>(FindObjectsSortMode.None)) Object.Destroy(old.gameObject);
            yield return null;
            app = new GameObject("RosterUi1101").AddComponent<ChoSirenApp>();
            yield return null;
            float deadline = Time.realtimeSinceStartup + 15f;
            while (Find("StartupLoading") != null && Time.realtimeSinceStartup < deadline) yield return null;
            model = (GameModel)typeof(ChoSirenApp).GetField("model", Private).GetValue(app);
            Canvas.ForceUpdateCanvases();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (app != null) Object.Destroy(app.gameObject);
            yield return null;
            if (had) PlayerPrefs.SetString(GameModel.SaveKey, save); else PlayerPrefs.DeleteKey(GameModel.SaveKey);
            if (hadLegacy) PlayerPrefs.SetString(GameModel.LegacySaveKey, legacy); else PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
            PlayerPrefs.Save();
        }

        [UnityTest]
        public IEnumerator EdgeTabsCycleRosterOrderKeepTabAndRefreshData()
        {
            int[] order = Order();
            Assert.That(order.Length, Is.GreaterThan(1), "需要至少两名已签约成员。");
            Invoke("OpenMember", order[0]);
            yield return null;
            Assert.That(Label("DetailPagerLabel"), Is.EqualTo($"1/{order.Length}"));

            // The tabs must be visible, sized for a thumb and drawn above the info column.
            GameObject next = Require("DetailNext");
            RectTransform hit = (RectTransform)next.transform;
            Assert.That(hit.rect.width, Is.GreaterThanOrEqualTo(64f));
            Assert.That(hit.rect.height, Is.GreaterThanOrEqualTo(140f));
            Graphic plate = next.transform.Find("TabPlate").GetComponent<Graphic>();
            Assert.That(plate.color.a, Is.GreaterThan(.9f), "切换箭头底板必须清楚可见。");
            Assert.That(next.transform.Find("Chevron").GetComponent<Graphic>().color.a, Is.EqualTo(1f));
            Assert.That(next.transform.GetSiblingIndex(), Is.GreaterThan(Require("MemberStatPanel").transform.GetSiblingIndex()),
                "右侧箭头不能被属性面板盖住。");

            Require("DetailTab-1").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Require("DetailSkillScroll");

            Click("DetailNext");
            yield return null;
            Assert.That(Label("MemberName"), Is.EqualTo(GameModel.Members[order[1]].Name));
            Assert.That(Label("MemberPower"), Is.EqualTo(model.PowerOf(order[1]).ToString("N0")), "数据要随成员刷新。");
            Assert.That(Label("DetailPagerLabel"), Is.EqualTo($"2/{order.Length}"));
            Require("DetailSkillScroll");
            Assert.That(Find("DetailProfileScroll"), Is.Null, "切换后仍停在技能页签。");
            Assert.That(Find("MemberFullBodyOutgoing"), Is.Not.Null, "旧立绘应滑出而不是瞬间消失。");
            RectTransform figure = Require("MemberFullBody").GetComponent<RectTransform>();
            float settledX = 240f - figure.rect.width * .5f;
            Assert.That(figure.anchoredPosition.x, Is.Not.EqualTo(settledX).Within(1f), "新立绘应从侧面滑入。");
            yield return new WaitForSecondsRealtime(.5f);
            Assert.That(Find("MemberFullBodyOutgoing"), Is.Null);
            Assert.That(figure.anchoredPosition.x, Is.EqualTo(settledX).Within(.5f));
            Assert.That(figure.GetComponent<Graphic>().color.a, Is.EqualTo(1f).Within(.01f));

            Click("DetailPrevious");
            yield return null;
            Click("DetailPrevious");
            yield return null;
            Assert.That(Label("MemberName"), Is.EqualTo(GameModel.Members[order[order.Length - 1]].Name), "首位向前应循环到末位。");
            Assert.That(Label("DetailPagerLabel"), Is.EqualTo($"{order.Length}/{order.Length}"));
            Require("DetailSkillScroll");
        }

        [UnityTest]
        public IEnumerator ShortSwipeSpringsBackLongSwipeSwitchesAndVerticalDragIsIgnored()
        {
            int[] order = Order();
            Invoke("OpenMember", order[0]);
            yield return null;
            RectTransform figure = Require("MemberFullBody").GetComponent<RectTransform>();
            float baseX = figure.anchoredPosition.x;

            Drag(new Vector2(-70f, 0f), release: true);
            Assert.That(Label("MemberName"), Is.EqualTo(GameModel.Members[order[0]].Name), "距离不够不能切换。");
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(figure.anchoredPosition.x, Is.EqualTo(baseX).Within(.5f), "距离不够应回弹。");

            Drag(new Vector2(30f, -260f), release: true);
            yield return null;
            Assert.That(Label("MemberName"), Is.EqualTo(GameModel.Members[order[0]].Name), "竖向拖动不触发切换。");
            Assert.That(figure.anchoredPosition.x, Is.EqualTo(baseX).Within(.5f));

            Drag(new Vector2(-260f, 10f), release: true);
            yield return null;
            Assert.That(Label("MemberName"), Is.EqualTo(GameModel.Members[order[1]].Name), "左滑切到下一位。");
            yield return new WaitForSecondsRealtime(.5f);

            Drag(new Vector2(260f, 0f), release: true);
            yield return null;
            Assert.That(Label("MemberName"), Is.EqualTo(GameModel.Members[order[0]].Name), "右滑回到上一位。");
        }

        [UnityTest]
        public IEnumerator SwitchOrderFollowsMemberListFilter()
        {
            int first = model.Save.UnlockedMembers.OrderBy(i => i).First();
            MethodInfo cycle = typeof(ChoSirenApp).GetMethod("MemberDetailCycle1101", Private);
            int[] all = (int[])cycle.Invoke(app, new object[] { first });
            Assert.That(all, Is.EqualTo(Enumerable.Range(0, GameModel.Members.Length).Where(model.IsUnlocked).ToArray()));

            // Search narrows the list: switching walks only the members the list shows…
            string query = GameModel.Members[first].Name;
            typeof(ChoSirenApp).GetField("memberSearchQuery", Private).SetValue(app, query);
            int[] filtered = (int[])cycle.Invoke(app, new object[] { first });
            Assert.That(filtered, Does.Contain(first));
            Assert.That(filtered.All(i => GameModel.Members[i].Name.Contains(query)), Is.True);
            // …and a member outside the filtered list falls back to every signed member.
            int other = all.First(i => !filtered.Contains(i));
            Assert.That((int[])cycle.Invoke(app, new object[] { other }), Is.EqualTo(all));
            Invoke("OpenMember", first);
            yield return null;
            Assert.That(Find("DetailNext") != null, Is.EqualTo(filtered.Length > 1));
            typeof(ChoSirenApp).GetField("memberSearchQuery", Private).SetValue(app, string.Empty);
        }

        [UnityTest]
        public IEnumerator TeamPickerIsNearFullScreenTwoColumnGridOfLargeCards()
        {
            int[] before = model.Save.Team.ToArray();
            Invoke("OpenTeamSlotPicker", 0, 0);
            yield return null;
            Canvas.ForceUpdateCanvases();
            RectTransform safe = Require("SafeArea").GetComponent<RectTransform>();
            RectTransform panel = Require("TeamMemberPicker").transform.Find("Panel").GetComponent<RectTransform>();
            Assert.That(panel.rect.height, Is.GreaterThanOrEqualTo(Mathf.Min(760f, safe.rect.height * .9f)));
            Assert.That(panel.rect.width, Is.GreaterThanOrEqualTo(safe.rect.width * .95f - 1f));

            ScrollRect scroll = Require("PickerScroll").GetComponent<ScrollRect>();
            RectTransform[] cards = Cards();
            Assert.That(cards.Length, Is.EqualTo(model.Save.UnlockedMembers.Count));
            float bodyWidth = scroll.viewport.rect.width;
            foreach (RectTransform card in cards)
            {
                Assert.That(card.parent, Is.SameAs(scroll.content));
                Assert.That(card.rect.width, Is.GreaterThanOrEqualTo(bodyWidth * .4f), card.name + " 卡片宽度不小于红框");
                Assert.That(card.rect.height, Is.GreaterThanOrEqualTo(card.rect.width * 1.3f), card.name + " 卡片高度约 1.3 倍宽");
            }
            Assert.That(cards.Select(c => Mathf.Round(c.anchoredPosition.x)).Distinct().Count(), Is.EqualTo(2), "两列网格");
            Assert.That(Find("PickerNext"), Is.Null);
            Require("PickerOwnership"); Require("PickerSearch");

            Assert.That(Label("PickerBadge-" + before[0]), Is.EqualTo("当前成员"));
            Assert.That(Label("PickerBadge-" + before[1]), Is.EqualTo("已出战 · 第 2 位"));
            Assert.That(Label("PickerState-" + before[1]), Is.EqualTo("交换位置"));
            Assert.That(Require("PickerRaceIcon"), Is.Not.Null);
            Image art = Require("PickerPortrait-" + before[1]).GetComponent<Image>();
            Assert.That(art.rectTransform.rect.width, Is.GreaterThan(cards[0].rect.width * .9f), "立绘是卡片主体");

            Require("PickMember-" + before[1]).GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.That(model.Save.Team[0], Is.EqualTo(before[1]), "点击仍走原有的交换逻辑");
            Assert.That(model.Save.Team[1], Is.EqualTo(before[0]));
        }

        [UnityTest]
        public IEnumerator CaptainReplacementAndAccessoryEntriesShareTheCardGrid()
        {
            Invoke("OpenCaptainPicker");
            yield return null;
            Require("CaptainMemberPicker");
            RectTransform[] cards = Cards();
            Assert.That(cards.Length, Is.GreaterThan(0));
            Assert.That(cards[0].rect.height, Is.EqualTo(ChoSirenApp.PickerCaptainCardHeight1101).Within(.1f));
            Assert.That(Label("PickerBadge-" + model.Save.Team[0]), Is.EqualTo("当前队长"));
            Require("PickerCaptainTrait-" + model.Save.Team[0]);

            Invoke("OpenTeamReplacement", model.Save.Team[0]);
            yield return null;
            Require("TeamReplacementPicker");
            for (int slot = 0; slot < model.Save.Team.Count; slot++)
            {
                RectTransform card = Require("ReplaceSlot-" + slot).GetComponent<RectTransform>();
                Assert.That(card.rect.height, Is.EqualTo(ChoSirenApp.PickerCardHeight1101).Within(.1f));
            }

            Invoke("OpenOwnedMemberPicker", 0, 0, true);
            yield return null;
            Require("EquipmentMemberPicker");
            int member = model.Save.Team[1];
            Assert.That(Label("PickerState-" + member), Is.EqualTo("管理饰品"));
            Require("PickMember-" + member).GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.That((int)typeof(ChoSirenApp).GetField("equipmentMember", Private).GetValue(app), Is.EqualTo(member));
        }

        [UnityTest]
        public IEnumerator CaptureDetailSwitchAndPickerAtPhoneAndMacRatios()
        {
            foreach (var size in new[] { new Vector2Int(1080, 2288), new Vector2Int(1080, 2160) })
            {
                string tag = size.y == 2288 ? "phone" : "mac";
                var camera = new GameObject("RosterUi1101Camera", typeof(Camera)).GetComponent<Camera>();
                var rt = new RenderTexture(size.x, size.y, 24);
                Canvas canvas = app.GetComponentInChildren<Canvas>();
                try
                {
                    camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                    camera.orthographic = true; camera.targetTexture = rt;
                    canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
                    foreach (var fitter in canvas.GetComponentsInChildren<SafeAreaFitter>(true))
                    {
                        fitter.enabled = false;
                        var safe = (RectTransform)fitter.transform;
                        safe.anchorMin = Vector2.zero; safe.anchorMax = Vector2.one;
                        safe.offsetMin = safe.offsetMax = Vector2.zero;
                    }
                    canvas.GetComponent<CanvasScaler>().matchWidthOrHeight = 0f;
                    yield return null; Canvas.ForceUpdateCanvases();

                    int[] order = Order();
                    Invoke("OpenMember", order[0]);
                    yield return new WaitForSecondsRealtime(.4f);
                    Capture(camera, rt, $"detail-{tag}-1-before.png");
                    Drag(new Vector2(-150f, 0f), release: false, camera);
                    yield return null;
                    Capture(camera, rt, $"detail-{tag}-2-dragging.png");
                    EndDrag(camera);
                    yield return new WaitForSecondsRealtime(.12f);
                    Capture(camera, rt, $"detail-{tag}-3-sliding.png");
                    yield return new WaitForSecondsRealtime(.5f);
                    Capture(camera, rt, $"detail-{tag}-4-after.png");
                    Require("DetailTab-2").GetComponent<Button>().onClick.Invoke();
                    yield return null;
                    Click("DetailNext");
                    yield return new WaitForSecondsRealtime(.5f);
                    Capture(camera, rt, $"detail-{tag}-5-next-profile-tab.png");
                    Require("DetailTab-0").GetComponent<Button>().onClick.Invoke();
                    yield return null;
                    Invoke("CloseModal");

                    Invoke("OpenTeamSlotPicker", 0, 0);
                    yield return new WaitForSecondsRealtime(.3f);
                    Capture(camera, rt, $"picker-{tag}-team.png");
                    Require("PickerOwnership").GetComponent<Button>().onClick.Invoke();
                    yield return new WaitForSecondsRealtime(.2f);
                    ScrollRect scroll = Require("PickerScroll").GetComponent<ScrollRect>();
                    scroll.verticalNormalizedPosition = 0f;
                    yield return null;
                    Capture(camera, rt, $"picker-{tag}-all-scrolled.png");
                    Invoke("OpenCaptainPicker");
                    yield return new WaitForSecondsRealtime(.3f);
                    Capture(camera, rt, $"picker-{tag}-captain.png");
                    Invoke("OpenTeamReplacement", order[0]);
                    yield return new WaitForSecondsRealtime(.3f);
                    Capture(camera, rt, $"picker-{tag}-replacement.png");
                    Invoke("CloseModal");
                    yield return null;
                }
                finally
                {
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null;
                    Object.DestroyImmediate(camera.gameObject); rt.Release(); Object.DestroyImmediate(rt);
                }
            }
        }

        // ------------------------------------------------------------------ helpers

        private int[] Order() => (int[])typeof(ChoSirenApp).GetMethod("MemberDetailCycle1101", Private)
            .Invoke(app, new object[] { model.Save.UnlockedMembers.OrderBy(i => i).First() });

        private PointerEventData pending;

        /// <summary>Drags across the detail page through the real swipe handler, in page units.</summary>
        private void Drag(Vector2 pageDelta, bool release, Camera camera = null)
        {
            GameObject modal = Require("MemberModal");
            Component swipe = modal.GetComponent(SwipeType);
            Assert.That(swipe, Is.Not.Null);
            RectTransform page = (RectTransform)modal.transform.Find("Page");
            Vector2 from = new Vector2(-150f, -760f);
            Vector2 press = RectTransformUtility.WorldToScreenPoint(camera, page.TransformPoint(from));
            Vector2 to = RectTransformUtility.WorldToScreenPoint(camera, page.TransformPoint(from + pageDelta));
            var e = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                pressPosition = press,
                position = press + (to - press) * .2f,
                pointerPressRaycast = new RaycastResult { module = modal.GetComponentInParent<Canvas>().rootCanvas.GetComponent<GraphicRaycaster>() },
            };
            ((IInitializePotentialDragHandler)swipe).OnInitializePotentialDrag(e);
            ((IBeginDragHandler)swipe).OnBeginDrag(e);
            for (int step = 2; step <= 5; step++)
            {
                e.position = press + (to - press) * (step / 5f);
                ((IDragHandler)swipe).OnDrag(e);
            }
            pending = e;
            if (release) EndDrag(camera);
        }

        private void EndDrag(Camera camera)
        {
            GameObject modal = Require("MemberModal");
            ((IEndDragHandler)modal.GetComponent(SwipeType)).OnEndDrag(pending);
        }

        private void Click(string name) => Require(name).GetComponent<Button>().onClick.Invoke();

        private RectTransform[] Cards() => app.GetComponentsInChildren<RectTransform>()
            .Where(t => t.name.StartsWith("PickMember-") || t.name.StartsWith("ReplaceSlot-")).ToArray();

        private string Label(string name) => Require(name).GetComponent<Text>().text;

        private void Invoke(string name, params object[] args)
        {
            MethodInfo method = typeof(ChoSirenApp).GetMethods(Private)
                .First(m => m.Name == name && m.GetParameters().Length >= args.Length
                    && m.GetParameters().Skip(args.Length).All(p => p.HasDefaultValue));
            object[] full = args.Concat(method.GetParameters().Skip(args.Length).Select(p => p.DefaultValue)).ToArray();
            method.Invoke(app, full);
        }

        private GameObject Find(string name) => app == null ? null : app.GetComponentsInChildren<Transform>(true)
            .Where(t => t.name == name && t.gameObject.activeInHierarchy).Select(t => t.gameObject).FirstOrDefault();

        private GameObject Require(string name)
        {
            GameObject value = Find(name);
            Assert.That(value, Is.Not.Null, name);
            return value;
        }

        private static void Capture(Camera camera, RenderTexture rt, string name)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            Canvas.ForceUpdateCanvases(); camera.Render();
            var old = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
            Directory.CreateDirectory(Out);
            File.WriteAllBytes(Path.Combine(Out, name), tex.EncodeToPNG());
            RenderTexture.active = old; Object.DestroyImmediate(tex);
        }
    }
}
