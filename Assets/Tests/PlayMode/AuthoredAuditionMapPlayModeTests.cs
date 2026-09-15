using System;
using System.Linq;
using ChoSiren.Panels;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
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

        [Test]
        public void MapHotspotFeedbackDoesNotMoveItsMeasuredInputRegion()
        {
            var host = Host(1536);
            try
            {
                var panel = LevelMapPanel.Open(host.transform, Model());
                var hit = Find(panel.transform, "StartChallenge").GetComponent<RectTransform>();
                Vector3 position = hit.localPosition, scale = hit.localScale;
                var trigger = hit.GetComponent<EventTrigger>();
                trigger.triggers.First(e => e.eventID == EventTriggerType.PointerEnter).callback.Invoke(null);
                Assert.That(hit.GetComponent<Image>().color.a, Is.GreaterThan(0));
                Assert.That(hit.localPosition, Is.EqualTo(position)); Assert.That(hit.localScale, Is.EqualTo(scale));
                trigger.triggers.First(e => e.eventID == EventTriggerType.PointerExit).callback.Invoke(null);
                Assert.That(hit.GetComponent<Image>().color.a, Is.Zero);
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }
    }
}
