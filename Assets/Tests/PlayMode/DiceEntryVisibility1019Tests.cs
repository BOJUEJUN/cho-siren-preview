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
    public sealed class DiceEntryVisibility1019Tests
    {
        private GameObject host;
        private bool hadReduceMotion;
        private int previousReduceMotion;
        private static Sprite[] Faces => Enumerable.Range(1, 6)
            .Select(value => Resources.Load<Sprite>("Art/Reference038/vfx-die-face-" + value)).ToArray();

        [SetUp]
        public void SetUp()
        {
            hadReduceMotion = PlayerPrefs.HasKey(BattlePresentationOptions.ReduceMotionKey);
            previousReduceMotion = PlayerPrefs.GetInt(BattlePresentationOptions.ReduceMotionKey, 0);
            PlayerPrefs.SetInt(BattlePresentationOptions.ReduceMotionKey, 0);
            host = new GameObject("DiceEntryVisibilityCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            host.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = host.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720, 1536);
            scaler.matchWidthOrHeight = 0f;
        }

        [TearDown]
        public void TearDown()
        {
            if (host != null) Object.DestroyImmediate(host);
            if (hadReduceMotion) PlayerPrefs.SetInt(BattlePresentationOptions.ReduceMotionKey, previousReduceMotion);
            else PlayerPrefs.DeleteKey(BattlePresentationOptions.ReduceMotionKey);
        }

        private static void Call(TacticsBattlePanel panel, string method) => typeof(TacticsBattlePanel)
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(panel, null);

        private DiceRollPresentation NewDie(out GameObject die, out RectTransform rig, out Image face)
        {
            die = new GameObject("EntryDie", typeof(RectTransform), typeof(Button));
            die.transform.SetParent(host.transform, false);
            die.GetComponent<RectTransform>().sizeDelta = new Vector2(102, 102);
            var motion = new GameObject("RollRig", typeof(RectTransform));
            motion.transform.SetParent(die.transform, false);
            rig = motion.GetComponent<RectTransform>();
            rig.sizeDelta = new Vector2(102, 102);
            var image = new GameObject("AuthoredFace", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            image.transform.SetParent(rig, false);
            face = image.GetComponent<Image>();
            face.sprite = Faces[0]; face.raycastTarget = false;
            return motion.AddComponent<DiceRollPresentation>();
        }

        [Test]
        public void LongOpaqueCoverDoesNotConsumeRollTimeOrReplayTheModel()
        {
            DiceRollPresentation roll = NewDie(out GameObject die, out RectTransform rig, out Image face);
            bool covered = true;
            roll.Configure(face, rig, () => covered, () => 1, renderedRoll: false);
            roll.PlayRoll(Faces[5], 6, Faces, 3, true);
            for (int frame = 0; frame < 240; frame++) roll.AdvancePresentation(1f / 30f);
            Assert.That(roll.Elapsed, Is.Zero, "Eight seconds under a cover must not consume any throw time");
            Assert.That(roll.RollCount, Is.EqualTo(1));
            Assert.That(roll.Busy, Is.True);
            covered = false;
            for (int frame = 0; frame < 70 && roll.Busy; frame++) roll.AdvancePresentation(1f / 30f);
            Assert.That(roll.Busy, Is.False);
            Assert.That(roll.RollCount, Is.EqualTo(1));
            Assert.That(roll.FinalValue, Is.EqualTo(6));
            Assert.That(face.sprite, Is.SameAs(Faces[5]));
        }

        [UnityTest]
        public IEnumerator InactiveOpeningThrowDoesNotExpireAfterTwoSeconds()
        {
            DiceRollPresentation roll = NewDie(out GameObject die, out RectTransform rig, out Image face);
            roll.Configure(face, rig, () => false, () => 1, renderedRoll: false);
            die.SetActive(false);
            roll.PlayRoll(Faces[4], 5, Faces, 5, true);
            yield return new WaitForSecondsRealtime(2.3f);
            Assert.That(roll.Busy, Is.True, "A still-hidden throw cannot be reported as landed after a timeout");
            Assert.That(roll.RollCount, Is.Zero);
            die.SetActive(true);
            Assert.That(roll.IsRolling, Is.True);
            Assert.That(roll.RollCount, Is.EqualTo(1));
            for (int frame = 0; frame < 70 && roll.Busy; frame++) roll.AdvancePresentation(1f / 30f);
            Assert.That(roll.Busy, Is.False);
            Assert.That(face.sprite, Is.SameAs(Faces[4]));
        }

        [UnityTest]
        public IEnumerator RealEntryTransitionPreservesSixOpeningThrowsAndKeepsModelStoppedUntilLanding()
        {
            var model = new GameModel(() => new DateTime(2026, 10, 19, 12, 0, 0), new InMemorySaveStore());
            var map = new GameObject("TransitionOwner", typeof(RectTransform));
            map.transform.SetParent(host.transform, false);
            ChallengeVideoTransition transition = ChallengeVideoTransition.Warm(map.transform);
            Assert.That(ChallengeVideoTransition.IsCoveringGame, Is.False, "Prewarming a hidden transition is not a cover");
            TacticsBattlePanel panel = null;
            int calls = 0;
            transition.Play(host.transform, () =>
            {
                calls++;
                var battle = model.StartStageBattle("stage-1-1", 1019UL, out string error);
                Assert.That(battle, Is.Not.Null, error);
                panel = TacticsBattlePanel.Open(host.transform, model, battle, null);
            }, null);
            float deadline = Time.realtimeSinceStartup + 8f;
            while (panel == null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(panel, Is.Not.Null);
            DiceRollPresentation[] rolls = panel.GetComponentsInChildren<DiceRollPresentation>(true);
            Assert.That(rolls, Has.Length.EqualTo(6));
            int revision = panel.Battle.BattleDice.Revision;
            int[] values = panel.Battle.BattleDice.Values.ToArray();
            bool observedCoveredBattle = false;
            while (ChallengeVideoTransition.IsCoveringGame && Time.realtimeSinceStartup < deadline)
            {
                observedCoveredBattle = true;
                Assert.That(panel.Battle.ElapsedMilliseconds, Is.Zero);
                Assert.That(rolls.All(roll => roll.Elapsed == 0f && roll.Busy), Is.True,
                    "The opening throw must wait behind the transition instead of playing invisibly");
                Call(panel, "RefreshDiceUi");
                yield return null;
            }
            Assert.That(observedCoveredBattle, Is.True, "The real switch callback must run while the transition covers its new scene");
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(rolls.All(roll => roll.RollCount == 1), Is.True);
            Assert.That(rolls.Any(roll => roll.Busy), Is.True, "Uncovering must reveal a throw, not six static results");
            deadline = Time.realtimeSinceStartup + 8f;
            while (rolls.Any(roll => roll.Busy) && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
            Assert.That(rolls.Any(roll => roll.Busy), Is.False);
            Assert.That(rolls.All(roll => roll.RollCount == 1), Is.True);
            Assert.That(panel.Battle.ElapsedMilliseconds, Is.Zero, "No attack may happen before the first real landing");
            Assert.That(panel.Battle.BattleDice.Values, Is.EqualTo(values));
            Assert.That(panel.Battle.BattleDice.Revision, Is.EqualTo(revision));
            Assert.That(panel.Battle.BattleDice.UsedRerolls, Is.Zero, "Presentation must not consume reroll budget");
            var diceButtons = panel.GetComponentsInChildren<Button>(true).Where(button => button.name.StartsWith("Dice-")).ToArray();
            Assert.That(diceButtons, Has.Length.EqualTo(6));
            Assert.That(diceButtons.All(button => button.interactable), Is.True);
        }

        [UnityTest]
        public IEnumerator ScoringFlamesDrawAboveTheFaceAndPointsStayAboveFlamesWithoutBlockingInput()
        {
            NewDie(out GameObject die, out RectTransform rig, out Image face);
            Vector2 hitSize = die.GetComponent<RectTransform>().sizeDelta;
            Font font = Resources.Load<Font>("Fonts/NotoSansSC-Subset") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            DiceScoreAura aura = DiceScoreAura.Attach(die.GetComponent<RectTransform>(), font, () => false);
            aura.Show(true, true, 6, 0f, new DiceFxStyle(DiceFxStyle.Violet, 1f));
            yield return new WaitForSecondsRealtime(.3f);
            Transform overlay = die.transform.Find("DiceScoreOverlay");
            Assert.That(overlay, Is.Not.Null);
            Assert.That(overlay.GetSiblingIndex(), Is.GreaterThan(rig.GetSiblingIndex()),
                "Flames must not be inserted below the opaque authored die face");
            Image[] flames = overlay.GetComponentsInChildren<Image>();
            Assert.That(flames.All(image => !image.raycastTarget), Is.True);
            foreach (Image flame in flames.Where(image => image.name.StartsWith("DiceFlame")))
            {
                Assert.That(flame.color.a, Is.GreaterThan(0f));
                Assert.That(flame.color.a, Is.LessThanOrEqualTo(.45f), "The front glow must not hide the dice pips");
                Assert.That(Mathf.Abs(flame.rectTransform.anchoredPosition.x), Is.GreaterThan(hitSize.x * .25f));
            }
            Text points = overlay.GetComponentInChildren<Text>();
            Assert.That(points.text, Is.EqualTo("+6"));
            Assert.That(points.transform.GetSiblingIndex(), Is.EqualTo(overlay.childCount - 1));
            Assert.That(overlay.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
            Assert.That(die.GetComponent<Button>().IsInteractable(), Is.True);
            Assert.That(die.GetComponent<RectTransform>().sizeDelta, Is.EqualTo(hitSize));
            Assert.That(face.sprite, Is.SameAs(Faces[0]));
        }
    }
}
