using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using ChoSiren.Panels;
using ChoSiren.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ChoSiren.Tests
{
    public sealed class ChallengeEntryCover1019Tests
    {
        GameObject host;
        bool hadReduce; int reduce;
        [SetUp] public void Before()
        {
            hadReduce = PlayerPrefs.HasKey(BattlePresentationOptions.ReduceMotionKey);
            reduce = PlayerPrefs.GetInt(BattlePresentationOptions.ReduceMotionKey);
            PlayerPrefs.SetInt(BattlePresentationOptions.ReduceMotionKey, 0);
            host = new GameObject("ChallengeCoverFixture", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            host.GetComponent<RectTransform>().sizeDelta = new Vector2(720,1536);
        }
        [TearDown] public void After()
        {
            if (host != null) Object.DestroyImmediate(host);
            if (hadReduce) PlayerPrefs.SetInt(BattlePresentationOptions.ReduceMotionKey, reduce);
            else PlayerPrefs.DeleteKey(BattlePresentationOptions.ReduceMotionKey);
        }

        [Ignore("Quarantined 2026-11-08: record transition replaced by the vinyl stage map (11-02); see Docs/TEST-QUARANTINE-1108.md")]
        [UnityTest] public IEnumerator CoveredMapCannotSpendStaminaAndTheNextRevealedChallengeOpensExactlyOnce()
        {
            var model = new GameModel(() => new DateTime(2026,10,19,12,0,0), new InMemorySaveStore());
            model.CompleteStory("stage-1-1");
            int stamina = model.Save.Stamina, cost = model.Tactics.FindStage("stage-1-1").StaminaCost;
            LevelMapPanel map = null;
            int mapOpened = 0;
            var homeEntry = ChallengeVideoTransition.Warm(host.transform);
            homeEntry.Play(host.transform, () => { mapOpened++; map = LevelMapPanel.Open(host.transform, model); }, null);
            homeEntry.Advance(ChallengeVideoTransition.CoverTime + .01f);
            Assert.That(map, Is.Not.Null);
            Button start = map.GetComponentsInChildren<Button>(true).Single(b => b.name == "StartChallenge");
            start.onClick.Invoke(); start.onClick.Invoke();
            Assert.That(model.Save.Stamina, Is.EqualTo(stamina), "A still-covered click cannot pay for a Play that will be rejected.");
            Assert.That(host.GetComponentInChildren<TacticsBattlePanel>(true), Is.Null);
            Assert.That(mapOpened, Is.EqualTo(1));
            homeEntry.Advance(ChallengeVideoTransition.Duration - homeEntry.Clock + .01f);
            Assert.That(ChallengeVideoTransition.IsCoveringGame, Is.False);
            start.onClick.Invoke(); start.onClick.Invoke();
            Assert.That(model.Save.Stamina, Is.EqualTo(stamina - cost), "Only the accepted challenge may spend once.");
            var battleEntry = host.GetComponentsInChildren<ChallengeVideoTransition>(true).Single(fx => fx.IsPlaying);
            battleEntry.Advance(ChallengeVideoTransition.CoverTime + .01f);
            var panels = host.GetComponentsInChildren<TacticsBattlePanel>(true);
            Assert.That(panels, Has.Length.EqualTo(1));
            Assert.That(panels[0].Battle.ElapsedMilliseconds, Is.Zero);
            Assert.That(panels[0].GetComponentsInChildren<DiceRollPresentation>(true), Has.Length.EqualTo(6));
            battleEntry.Advance(ChallengeVideoTransition.Duration - battleEntry.Clock + .01f);
            yield return null;
            Assert.That(ChallengeVideoTransition.IsCoveringGame, Is.False);
            Assert.That(model.Save.Stamina, Is.EqualTo(stamina - cost));
            Assert.That(host.GetComponentsInChildren<TacticsBattlePanel>(true), Has.Length.EqualTo(1));
        }

        [Ignore("Quarantined 2026-11-08: record transition replaced by the vinyl stage map (11-02); see Docs/TEST-QUARANTINE-1108.md")]
        [UnityTest] public IEnumerator SmokeCallbacksWaitForBothRealRevealsRatherThanBypassingTheirRaycastCover()
        {
            var buttonObject = new GameObject("CoverWaitFixtureButton", typeof(RectTransform), typeof(Button));
            buttonObject.transform.SetParent(host.transform, false);
            int clicks = 0;
            buttonObject.GetComponent<Button>().onClick.AddListener(() => clicks++);
            var runner = host.AddComponent<SmokeCaptureRunner>();
            IEnumerator Click() => (IEnumerator)typeof(SmokeCaptureRunner).GetMethod("ClickNamed", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(runner, new object[] { buttonObject.name, "cover-regression" });
            for (int index = 0; index < 2; index++)
            {
                var owner = new GameObject("CoverOwner-" + index, typeof(RectTransform)); owner.transform.SetParent(host.transform, false);
                var transition = ChallengeVideoTransition.Warm(owner.transform);
                int opened = 0; transition.Play(host.transform, () => opened++, null);
                runner.StartCoroutine(Click());
                transition.Advance(ChallengeVideoTransition.CoverTime + .01f);
                yield return null;
                Assert.That(opened, Is.EqualTo(1));
                Assert.That(clicks, Is.EqualTo(index), "The destination exists but still cannot receive player input under this cover.");
                transition.Advance(ChallengeVideoTransition.Duration - transition.Clock + .01f);
                yield return null;
                Assert.That(clicks, Is.EqualTo(index + 1));
                Assert.That(ChallengeVideoTransition.IsCoveringGame, Is.False);
            }
        }
    }
}
