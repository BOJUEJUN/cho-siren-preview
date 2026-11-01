using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ChoSiren.Tests
{
    public sealed class FormationLaunchRegressionTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly DateTime Now = new DateTime(2026, 10, 16, 12, 0, 0);
        private static readonly int[] ScreenshotTeam = { 19, 59, 3, 0 };

        private static void Call(object target, string method) => target.GetType()
            .GetMethod(method, Private).Invoke(target, null);

        private static GameModel ScreenshotModel(out InMemorySaveStore store, bool onlyScreenshotMembers = false)
        {
            store = new InMemorySaveStore();
            var model = new GameModel(() => Now, store);
            Assert.That(GameModel.Members[19].Name, Is.EqualTo("洛羽"));
            Assert.That(GameModel.Members[59].Name, Is.EqualTo("梦璃"));
            model.Save.UnlockedMembers = (onlyScreenshotMembers ? ScreenshotTeam :
                model.Save.UnlockedMembers.Concat(ScreenshotTeam)).Distinct().OrderBy(i => i).ToList();
            model.Save.Team = ScreenshotTeam.ToList();
            int[] levels = { 35, 9, 2, 4 };
            for (int i = 0; i < ScreenshotTeam.Length; i++)
                model.Save.MemberLevels[ScreenshotTeam[i]] = levels[i];
            Call(model, "SaveState");
            // The regression is a persisted older formation, not merely an invalid list
            // injected into the battle UI after loading.
            return new GameModel(() => Now, store);
        }

        [Test]
        public void LoadingAndRejectingLegacyThreeBloodElvesNeverRewritesTheSaveOrSpendsStamina()
        {
            var model = ScreenshotModel(out var store);
            Assert.That(model.Save.Team, Is.EqualTo(ScreenshotTeam));
            string saved = store.GetString(GameModel.SaveKey, string.Empty);
            string state = JsonUtility.ToJson(model.Save);
            int stamina = model.Save.Stamina;
            Assert.That(model.IsTeamReady(out string problem), Is.False);
            Assert.That(problem, Does.Contain("血精灵当前 3 人"));
            Assert.That(model.StartStageBattle("stage-1-1", 101601UL, out string error), Is.Null);
            Assert.That(error, Does.Contain("同一种族最多同时上阵 2 人"));
            Assert.That(model.Save.Stamina, Is.EqualTo(stamina));
            Assert.That(model.Save.Team, Is.EqualTo(ScreenshotTeam));
            Assert.That(JsonUtility.ToJson(model.Save), Is.EqualTo(state));
            Assert.That(store.GetString(GameModel.SaveKey, string.Empty), Is.EqualTo(saved));
        }

        [Test]
        public void ExplicitAutoFormationKeepsCaptainAndTrainingAndCanThenStartBattle()
        {
            var model = ScreenshotModel(out var store);
            int[] owned = model.Save.UnlockedMembers.ToArray();
            int[] levels = model.Save.MemberLevels.ToArray();
            int[] affection = model.Save.MemberAffection.ToArray();
            int[] risk = model.Save.MemberRisk.ToArray();
            int stamina = model.Save.Stamina;
            int gold = model.Save.Gold, diamonds = model.Save.Diamonds;

            Assert.That(model.TryAutoTeam(out string message), Is.True, message);
            Assert.That(model.Save.Team.Count, Is.EqualTo(4));
            Assert.That(model.Save.Team[0], Is.EqualTo(19), "Keep the valid, trained current captain.");
            Assert.That(model.Save.Team, Does.Contain(59), "Do not replace a trained member just to display four careers.");
            Assert.That(model.Save.Team.GroupBy(i => GameModel.Members[i].Race).All(g => g.Count() <= 2), Is.True);
            Assert.That(model.Save.Team.Select(i => GameModel.Members[i].Career).Distinct().Count(), Is.GreaterThanOrEqualTo(3));
            Assert.That(model.IsTeamReady(out string problem), Is.True, problem);
            Assert.That(model.Save.UnlockedMembers, Is.EqualTo(owned));
            Assert.That(model.Save.MemberLevels, Is.EqualTo(levels));
            Assert.That(model.Save.MemberAffection, Is.EqualTo(affection));
            Assert.That(model.Save.MemberRisk, Is.EqualTo(risk));
            Assert.That(model.Save.Stamina, Is.EqualTo(stamina));
            Assert.That(model.Save.Gold, Is.EqualTo(gold));
            Assert.That(model.Save.Diamonds, Is.EqualTo(diamonds));

            var reloaded = new GameModel(() => Now, store);
            Assert.That(reloaded.Save.Team, Is.EqualTo(model.Save.Team));
            Assert.That(reloaded.Save.MemberLevels, Is.EqualTo(levels));
            Assert.That(reloaded.StartStageBattle("stage-1-1", 101602UL, out string error), Is.Not.Null, error);
            Assert.That(reloaded.Save.Stamina, Is.EqualTo(stamina - reloaded.Tactics.FindStage("stage-1-1").StaminaCost));
        }

        [Test]
        public void LimitedCandidatesProduceAnExplainedLegalSmallerPartyWithoutRecruiting()
        {
            var model = ScreenshotModel(out _, onlyScreenshotMembers: true);
            int[] owned = model.Save.UnlockedMembers.ToArray();
            int[] levels = model.Save.MemberLevels.ToArray();
            Assert.That(model.TryAutoTeam(out string message), Is.True, message);
            Assert.That(model.Save.Team.Count, Is.EqualTo(3));
            Assert.That(message, Does.Contain("3/4"));
            Assert.That(message, Does.Contain("无法补满"));
            Assert.That(model.IsTeamReady(out string problem), Is.True, problem);
            Assert.That(model.Save.Team[0], Is.EqualTo(19));
            Assert.That(model.Save.UnlockedMembers, Is.EqualTo(owned));
            Assert.That(model.Save.MemberLevels, Is.EqualTo(levels));
        }

        [Test]
        public void LegalTwoBloodElvesStillEnterBattleAndCaptainSurvivesAutoFormation()
        {
            var model = ScreenshotModel(out _);
            model.Save.Team = new List<int> { 19, 59, 0, 1 };
            Assert.That(model.IsTeamReady(out string problem), Is.True, problem);
            int stamina = model.Save.Stamina;
            Assert.That(model.StartStageBattle("stage-1-1", 101603UL, out string error), Is.Not.Null, error);
            Assert.That(model.Save.Stamina, Is.LessThan(stamina));
            model.AutoTeam();
            Assert.That(model.Save.Team[0], Is.EqualTo(19));
            Assert.That(model.IsTeamReady(out problem), Is.True, problem);
        }

        [Test]
        public void NoAvailableCandidatesLeaveTheCurrentFormationAndPersistedSaveUntouched()
        {
            var model = ScreenshotModel(out var store);
            model.Save.UnlockedMembers.Clear();
            string saved = store.GetString(GameModel.SaveKey, string.Empty);
            string state = JsonUtility.ToJson(model.Save);
            Assert.That(model.TryAutoTeam(out string message), Is.False);
            Assert.That(message, Does.Contain("当前编队已保留"));
            Assert.That(JsonUtility.ToJson(model.Save), Is.EqualTo(state));
            Assert.That(store.GetString(GameModel.SaveKey, string.Empty), Is.EqualTo(saved));
        }

        [UnityTest]
        public IEnumerator InvalidChallengeKeepsTheMapAndReasonUntilExplicitFormationAdjustment()
        {
            var model = ScreenshotModel(out var store);
            var host = new GameObject("FormationMapFixture", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            host.GetComponent<RectTransform>().sizeDelta = new Vector2(720, 1536);
            string destination = null;
            int navigationCount = 0;
            try
            {
                var map = LevelMapPanel.Open(host.transform, model, growth: value =>
                { destination = value; navigationCount++; });
                int stamina = model.Save.Stamina;
                string state = JsonUtility.ToJson(model.Save);
                string saved = store.GetString(GameModel.SaveKey, string.Empty);
                Call(map, "StartChallenge");
                yield return null;
                Assert.That(map != null && map.gameObject.activeInHierarchy, Is.True);
                Assert.That(destination, Is.Null, "An invalid challenge cannot navigate on its own.");
                var reason = map.GetComponentsInChildren<Text>(true).Single(t => t.name == "FormationProblemReason");
                Assert.That(reason.text, Does.Contain("血精灵当前 3 人"));
                Assert.That(reason.gameObject.activeInHierarchy, Is.True);
                var buttons = map.GetComponentsInChildren<Button>(true);
                buttons.Single(b => b.name == "FormationProblemCancel").onClick.Invoke();
                yield return null;
                Assert.That(map.gameObject.activeInHierarchy, Is.True);
                Assert.That(destination, Is.Null);
                Assert.That(map.GetComponentsInChildren<Transform>(true).Any(t => t.name == "FormationProblemModal"), Is.False);
                Call(map, "StartChallenge");
                map.GetComponentsInChildren<Button>(true).Single(b => b.name == "FormationProblemAdjust").onClick.Invoke();
                Assert.That(destination, Is.EqualTo("team-fix"));
                Assert.That(navigationCount, Is.EqualTo(1));
                yield return null;
                Assert.That(map == null, Is.True);
                Assert.That(model.Save.Stamina, Is.EqualTo(stamina));
                Assert.That(JsonUtility.ToJson(model.Save), Is.EqualTo(state));
                Assert.That(store.GetString(GameModel.SaveKey, string.Empty), Is.EqualTo(saved));
            }
            finally { Object.DestroyImmediate(host); }
        }
    }
}
