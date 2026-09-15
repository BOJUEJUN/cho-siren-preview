using System;
using System.Collections;
using System.Linq;
using ChoSiren.Panels;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ChoSiren.Tests
{
    public sealed class AuthoredAuditionMapPlayModeTests
    {
        private static GameModel Model() => new GameModel(() => new DateTime(2026, 9, 15, 12, 0, 0), new InMemorySaveStore());
        private static GameObject Host(float height)
        {
            var root = new GameObject("Authored UI Test", typeof(RectTransform));
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(720, height);
            return root;
        }
        private static Transform Find(Transform root, string name) =>
            root.GetComponentsInChildren<Transform>(true).First(t => t.name == name && t.gameObject.activeInHierarchy);

        [Test]
        public void AuditionBoardKeepsFiveLiveStatsAndSigningTransaction()
        {
            var host = Host(1290);
            try
            {
                var model = Model(); model.Save.Diamonds = 10000;
                var panel = GachaPanel.OpenEmbedded(host.transform, model, model);
                Assert.That(Find(panel.transform, "AuditionReferenceBoard").GetComponent<Image>().preserveAspect, Is.True);
                int member = model.InterviewCandidates(0)[0];
                GameModel.StageStats(GameModel.Members[member], member, 0, model.PreviewSigningLevel(member, 0),
                    out int vocal, out int rhythm, out int dance, out int fame, out int beauty);
                string[] keys = { "Vocal", "Rhythm", "Presence", "Resonance", "Charm" };
                int[] values = { vocal, rhythm, dance, fame, beauty };
                for (int i = 0; i < keys.Length; i++)
                    Assert.That(Find(panel.transform, "StatValue-" + keys[i]).GetComponent<Text>().text, Is.EqualTo(values[i].ToString()));
                int diamonds = model.Save.Diamonds;
                Find(panel.transform, "SignCandidate").GetComponent<Button>().onClick.Invoke();
                Assert.That(model.IsUnlocked(member), Is.False, "展示签约确认时不可先扣款或解锁");
                Find(panel.transform, "SignConfirm").GetComponent<Button>().onClick.Invoke();
                Assert.That(model.IsUnlocked(member), Is.True);
                Assert.That(model.Save.Diamonds, Is.LessThan(diamonds));
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        [TestCase(1290)]
        [TestCase(1030)]
        public void CandidateNavigationIsVisibleCyclesWholePoolAndKeepsChannelPositions(float height)
        {
            var host = Host(height);
            try
            {
                var model = Model();
                int diamonds = model.Save.Diamonds, stamina = model.Save.Stamina;
                var panel = GachaPanel.OpenEmbedded(host.transform, model, model);
                int[] online = model.InterviewCandidates(0).ToArray();
                int[] offline = model.InterviewCandidates(1).ToArray();
                Assert.That(online.Length, Is.EqualTo(10));
                Assert.That(offline.Length, Is.EqualTo(10));
                void AssertCandidate(int channel, int index)
                {
                    int[] pool = channel == 0 ? online : offline;
                    Assert.That(panel.InterviewPoolIndex, Is.EqualTo(channel));
                    Assert.That(panel.InterviewCandidateIndex, Is.EqualTo(index));
                    Assert.That(Find(panel.transform, "CandidateName").GetComponent<Text>().text,
                        Is.EqualTo(GameModel.Members[pool[index]].Name));
                    Assert.That(Find(panel.transform, "CandidateCounter").GetComponent<Text>().text,
                        Is.EqualTo($"{index + 1} / {pool.Length}"));
                }
                foreach (string buttonName in new[] { "PreviousCandidate", "NextCandidate" })
                {
                    var hit = Find(panel.transform, buttonName).GetComponent<RectTransform>();
                    float scale = Find(panel.transform, "AuthoredInterviewPage").localScale.x;
                    Assert.That(hit.rect.width * scale, Is.GreaterThanOrEqualTo(44));
                    Assert.That(hit.rect.height * scale, Is.GreaterThanOrEqualTo(44));
                    Assert.That(hit.GetComponent<Button>().IsInteractable(), Is.True);
                    Assert.That(hit.GetComponent<Image>().raycastTarget, Is.True);
                    Assert.That(hit.GetComponent<EventTrigger>(), Is.Null, "不得用整片矩形填色遮住美术");
                    Assert.That(hit.GetComponent<LobbyHotspotFeedback>(), Is.Not.Null);
                    Image visual = hit.Find("CandidateArrowVisualV2").GetComponent<Image>();
                    Assert.That(visual.color.a, Is.GreaterThan(.9f), "切换箭头必须常驻可见");
                    Assert.That(visual.GetComponentsInChildren<Graphic>().All(g => !g.raycastTarget), Is.True,
                        "按钮图文不能截断真实Button指针事件");
                }
                AssertCandidate(0, 0);
                for (int i = 1; i <= online.Length; i++)
                {
                    Find(panel.transform, "NextCandidate").GetComponent<Button>().onClick.Invoke();
                    AssertCandidate(0, i % online.Length);
                }
                Find(panel.transform, "PreviousCandidate").GetComponent<Button>().onClick.Invoke();
                AssertCandidate(0, online.Length - 1);
                Find(panel.transform, "InterviewPool-1").GetComponent<Button>().onClick.Invoke();
                AssertCandidate(1, 0);
                Find(panel.transform, "NextCandidate").GetComponent<Button>().onClick.Invoke();
                AssertCandidate(1, 1);
                Find(panel.transform, "InterviewPool-0").GetComponent<Button>().onClick.Invoke();
                AssertCandidate(0, online.Length - 1);
                Find(panel.transform, "InterviewPool-1").GetComponent<Button>().onClick.Invoke();
                AssertCandidate(1, 1);
                Assert.That(model.Save.Diamonds, Is.EqualTo(diamonds));
                Assert.That(model.Save.Stamina, Is.EqualTo(stamina));
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        [TestCase(0, 1)]
        [TestCase(0, 20)]
        [TestCase(1, 1)]
        [TestCase(1, 20)]
        public void SigningPreservesDisplayedFiveStatsAtActualChannelLevelAndAfterReload(int channel, int baselineLevel)
        {
            var host = Host(1290);
            try
            {
                var store = new InMemorySaveStore();
                Func<DateTime> clock = () => new DateTime(2026, 9, 15, 12, 0, 0);
                var model = new GameModel(clock, store);
                model.Save.Diamonds = 10000;
                int member = model.InterviewCandidates(channel)[0];
                model.Save.MemberLevels[member] = baselineLevel;
                int signedLevel = model.PreviewSigningLevel(member, channel);
                Assert.That(signedLevel, Is.EqualTo(Mathf.Clamp(baselineLevel +
                    GameModel.SigningStartingLevelOffset(channel), 1, GameModel.MaxMemberLevel)));
                var panel = GachaPanel.OpenEmbedded(host.transform, model, model);
                if (channel == 1) Find(panel.transform, "InterviewPool-1").GetComponent<Button>().onClick.Invoke();
                string[] keys = { "Vocal", "Rhythm", "Presence", "Resonance", "Charm" };
                int[] shown = keys.Select(key => int.Parse(Find(panel.transform,
                    "StatValue-" + key).GetComponent<Text>().text)).ToArray();
                Assert.That(Find(panel.transform, "StrongestStat"), Is.Not.Null);
                int diamonds = model.Save.Diamonds;
                Find(panel.transform, "SignCandidate").GetComponent<Button>().onClick.Invoke();
                Assert.That(model.IsUnlocked(member), Is.False);
                Assert.That(model.Save.Diamonds, Is.EqualTo(diamonds), "确认前不能扣款");
                Find(panel.transform, "SignConfirm").GetComponent<Button>().onClick.Invoke();
                Assert.That(model.IsUnlocked(member), Is.True);
                Assert.That(model.LevelOf(member), Is.EqualTo(signedLevel));
                Assert.That(model.SigningChannelOf(member), Is.EqualTo(channel));
                Assert.That(model.Save.Diamonds, Is.LessThan(diamonds));
                foreach (GameModel state in new[] { model, new GameModel(clock, store) })
                {
                    Assert.That(state.IsUnlocked(member), Is.True);
                    Assert.That(state.LevelOf(member), Is.EqualTo(signedLevel));
                    Assert.That(state.SigningChannelOf(member), Is.EqualTo(channel));
                    GameModel.StageStats(GameModel.Members[member], member, state.SigningChannelOf(member),
                        state.LevelOf(member), out int vocal, out int rap, out int dance, out int fame, out int beauty);
                    Assert.That(new[] { vocal, rap, dance, fame, beauty }, Is.EqualTo(shown),
                        "签约与重载后，五维必须与确认前的面试卡逐项相同");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        [UnityTest]
        public IEnumerator CandidateArrowsKeepHitGeometryWhileFeedbackAnimates()
        {
            var host = Host(1290);
            try
            {
                var panel = GachaPanel.OpenEmbedded(host.transform, Model(), Model());
                var pointer = new PointerEventData(EventSystem.current)
                { button = PointerEventData.InputButton.Left };
                foreach (string buttonName in new[] { "PreviousCandidate", "NextCandidate" })
                {
                    var hit = Find(panel.transform, buttonName).GetComponent<RectTransform>();
                    Vector3 position = hit.localPosition, scale = hit.localScale;
                    Vector2 size = hit.sizeDelta;
                    CanvasGroup fx = hit.Find("InteractionFx")?.GetComponent<CanvasGroup>();
                    Assert.That(fx, Is.Not.Null, "缺少独立的悬停可见反馈层");
                    Assert.That(fx.alpha, Is.LessThanOrEqualTo(.01f), "静止时反馈必须完全透明");
                    RectTransform visual = hit.Find("CandidateArrowVisualV2")?.GetComponent<RectTransform>();
                    Assert.That(visual, Is.Not.Null);
                    Vector3 visualScale = visual.localScale;
                    ExecuteEvents.Execute<IPointerEnterHandler>(hit.gameObject, pointer,
                        ExecuteEvents.pointerEnterHandler);
                    yield return new WaitForSecondsRealtime(.25f);
                    Assert.That(fx.alpha, Is.GreaterThan(.05f), "悬停必须产生玩家可见的反馈");
                    Assert.That(visual.localScale.x, Is.Not.EqualTo(visualScale.x),
                        "箭头美术层必须承担可见动效");
                    ExecuteEvents.Execute<IPointerDownHandler>(hit.gameObject, pointer,
                        ExecuteEvents.pointerDownHandler);
                    yield return new WaitForSecondsRealtime(.12f);
                    ExecuteEvents.Execute<IPointerUpHandler>(hit.gameObject, pointer,
                        ExecuteEvents.pointerUpHandler);
                    ExecuteEvents.Execute<IPointerExitHandler>(hit.gameObject, pointer,
                        ExecuteEvents.pointerExitHandler);
                    yield return new WaitForSecondsRealtime(.4f);
                    Assert.That(fx.alpha, Is.LessThanOrEqualTo(.01f), "移出后反馈必须恢复透明");
                    Assert.That(visual.localScale, Is.EqualTo(visualScale),
                        "动效结束后美术层必须复位");
                    Assert.That(hit.localPosition, Is.EqualTo(position), "命中区不可移动");
                    Assert.That(hit.localScale, Is.EqualTo(scale), "命中区不可缩放");
                    Assert.That(hit.sizeDelta, Is.EqualTo(size), "命中区尺寸不可改变");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        [Test]
        public void MapBoardHasTenUniqueLiveStagesAndLockedClickCannotSpendStamina()
        {
            var host = Host(1536);
            try
            {
                var model = Model(); string notice = null;
                var panel = LevelMapPanel.Open(host.transform, model, message: text => notice = text);
                Assert.That(Find(panel.transform, "MapReferenceBoard").GetComponent<Image>().preserveAspect, Is.True);
                for (int i = 1; i <= 10; i++)
                {
                    Transform node = Find(panel.transform, "Level-1-" + i);
                    Assert.That(node.Find("StageLabel").GetComponent<Text>().text, Is.EqualTo("1-" + i));
                    Assert.That(node.Find("StatusLabel").GetComponent<Text>().text, Does.Not.Contain("锁定"));
                }
                int stamina = model.Save.Stamina;
                Find(panel.transform, "Level-1-10").GetComponent<Button>().onClick.Invoke();
                Assert.That(model.Save.Stamina, Is.EqualTo(stamina));
                Assert.That(notice, Is.Not.Null.And.Not.Empty);
                Assert.That(Find(panel.transform, "StartChallenge").GetComponent<Button>().interactable, Is.True);
                Assert.That(Find(panel.transform, "StoryChapter-01").GetComponent<Button>().interactable, Is.True);
                Find(panel.transform, "ChapterRewards").GetComponent<Button>().onClick.Invoke();
                Assert.That(Find(panel.transform, "ChapterRewardsModal"), Is.Not.Null);
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        [UnityTest]
        public IEnumerator MapHotspotFeedbackDoesNotMoveItsMeasuredInputRegion()
        {
            var host = Host(1536);
            try
            {
                var panel = LevelMapPanel.Open(host.transform, Model());
                var hit = Find(panel.transform, "StartChallenge").GetComponent<RectTransform>();
                Vector3 position = hit.localPosition, scale = hit.localScale;
                Assert.That(hit.GetComponent<EventTrigger>(), Is.Null,
                    "地图热点与大厅共用 LobbyHotspotFeedback，不再挂整片 EventTrigger 填色");
                Assert.That(hit.GetComponent<LobbyHotspotFeedback>(), Is.Not.Null);
                CanvasGroup fx = hit.Find("InteractionFx")?.GetComponent<CanvasGroup>();
                Assert.That(fx, Is.Not.Null, "缺少独立的悬停可见反馈层");
                Assert.That(fx.alpha, Is.LessThanOrEqualTo(.01f), "静止时反馈必须完全透明");
                var pointer = new PointerEventData(EventSystem.current)
                { button = PointerEventData.InputButton.Left };
                ExecuteEvents.Execute<IPointerEnterHandler>(hit.gameObject, pointer,
                    ExecuteEvents.pointerEnterHandler);
                yield return new WaitForSecondsRealtime(.25f);
                Assert.That(fx.alpha, Is.GreaterThan(.05f), "悬停必须产生玩家可见的反馈");
                Assert.That(hit.localPosition, Is.EqualTo(position));
                Assert.That(hit.localScale, Is.EqualTo(scale));
                ExecuteEvents.Execute<IPointerExitHandler>(hit.gameObject, pointer,
                    ExecuteEvents.pointerExitHandler);
                yield return new WaitForSecondsRealtime(.4f);
                Assert.That(fx.alpha, Is.LessThanOrEqualTo(.01f), "移出后反馈必须恢复透明");
                Assert.That(hit.localPosition, Is.EqualTo(position));
                Assert.That(hit.localScale, Is.EqualTo(scale));
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }
    }
}
