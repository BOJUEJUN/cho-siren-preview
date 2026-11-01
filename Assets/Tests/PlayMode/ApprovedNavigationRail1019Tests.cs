using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ChoSiren.Panels;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ChoSiren.Tests
{
    /// <summary>Acceptance checks for the approved independent rail and round record.</summary>
    public sealed class ApprovedNavigationRail1019Tests
    {
        private static readonly string[] Ids = { "team", "members", "lobby", "accessory", "audition" };
        private static readonly string[] Labels = { "团队", "成员", "大厅", "饰品", "选秀" };
        private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;
        private ChoSirenApp app;
        private bool hadSave, hadLegacy, hadMotion;
        private string save, legacy;
        private int motion;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            hadSave = PlayerPrefs.HasKey(GameModel.SaveKey);
            save = PlayerPrefs.GetString(GameModel.SaveKey);
            hadLegacy = PlayerPrefs.HasKey(GameModel.LegacySaveKey);
            legacy = PlayerPrefs.GetString(GameModel.LegacySaveKey);
            hadMotion = PlayerPrefs.HasKey(BattlePresentationOptions.ReduceMotionKey);
            motion = PlayerPrefs.GetInt(BattlePresentationOptions.ReduceMotionKey, 0);
            foreach (var old in Object.FindObjectsByType<ChoSirenApp>(FindObjectsSortMode.None))
                Object.Destroy(old.gameObject);
            foreach (var old in Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
                Object.Destroy(old.gameObject);
            yield return null;
            PlayerPrefs.DeleteKey(GameModel.SaveKey);
            PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
            BattlePresentationOptions.ReduceMotion = false;
            app = new GameObject("Approved Navigation Test").AddComponent<ChoSirenApp>();
            yield return null;
            yield return new WaitForSecondsRealtime(.5f);
            Canvas.ForceUpdateCanvases();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (app != null) Object.Destroy(app.gameObject);
            foreach (var old in Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
                Object.Destroy(old.gameObject);
            yield return null;
            if (hadSave) PlayerPrefs.SetString(GameModel.SaveKey, save); else PlayerPrefs.DeleteKey(GameModel.SaveKey);
            if (hadLegacy) PlayerPrefs.SetString(GameModel.LegacySaveKey, legacy); else PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
            if (hadMotion) PlayerPrefs.SetInt(BattlePresentationOptions.ReduceMotionKey, motion);
            else PlayerPrefs.DeleteKey(BattlePresentationOptions.ReduceMotionKey);
            PlayerPrefs.Save();
        }

        [UnityTest]
        public IEnumerator RailAndLowRecordKeepTheirSourceProportionsAndNeverBecomeAMenuScreenshot()
        {
            RectTransform nav = Navigation;
            Image rail = nav.Find("NavOpticalRail").GetComponent<Image>();
            Image record = nav.Find("NavDiscArc").GetComponent<Image>();
            Assert.That(rail.sprite, Is.EqualTo(Resources.Load<Sprite>("Art/LobbyPunk/nav-optical-rail-1019-v2")));
            Assert.That(record.sprite, Is.EqualTo(Resources.Load<Sprite>("Art/LobbyPunk/nav-optical-vinyl-1019-v2")));
            Assert.That(rail.preserveAspect, Is.True);
            Assert.That(record.preserveAspect, Is.True);
            Assert.That(rail.rectTransform.rect.width / rail.rectTransform.rect.height,
                Is.EqualTo(2172f / 724f).Within(.001f), "The optical rail must keep its authored 3:1 shape");
            Assert.That(record.rectTransform.rect.width, Is.EqualTo(record.rectTransform.rect.height).Within(.001f),
                "The record must remain a square before rotation, rather than a flattened ellipse");
            Assert.That(record.rectTransform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(record.sprite.rect.width / record.sprite.rect.height, Is.EqualTo(1).Within(.002f));
            Assert.That(rail.sprite.texture.width, Is.LessThanOrEqualTo(1024));
            Assert.That(record.sprite.texture.width, Is.LessThanOrEqualTo(1024));
            Assert.That(rail.sprite.texture.mipmapCount, Is.EqualTo(1));
            Assert.That(record.sprite.texture.mipmapCount, Is.EqualTo(1));
            Assert.That(rail.raycastTarget, Is.False);
            Assert.That(rail.GetComponent<LobbyNavigationDisc1019>(), Is.Null, "The rail is stationary");
            Assert.That(record.GetComponent<LobbyNavigationDisc1019>(), Is.Not.Null);
            Assert.That(nav.Find("NavDiscFill"), Is.Null, "No opaque black hill may fill the lower composition");
            Assert.That(nav.GetComponentsInChildren<Button>().Length, Is.EqualTo(5),
                "The five destinations must remain live controls rather than baked into a composite image");

            // Use the approved source's opaque rim, not its 53px transparent top margin.
            RectTransform rect = record.rectTransform;
            float fullRectTop = -rect.anchoredPosition.y - rect.rect.height * .5f;
            float opaqueRimTop = fullRectTop + rect.rect.height * (53f / 1254f);
            float visibleRim = nav.offsetMax.y - opaqueRimTop;
            Assert.That(visibleRim, Is.InRange(24f, 65f), "Only a quiet upper edge is visible above the screen bottom");
            Text hallLabel = nav.Find("Nav-lobby/Label").GetComponent<Text>();
            RectTransform hall = (RectTransform)hallLabel.transform.parent;
            float labelBottom = -hall.anchoredPosition.y - hallLabel.rectTransform.anchoredPosition.y + hallLabel.rectTransform.rect.height;
            Assert.That(opaqueRimTop, Is.GreaterThan(labelBottom + 15f), "The record stays below the selected caption");
            yield return null;
        }

        [UnityTest]
        public IEnumerator EveryMenuHitStaysReachableAndOneSelectionTurnsOnlyTheRecord()
        {
            RectTransform nav = Navigation;
            var hitsBefore = Ids.ToDictionary(id => id, id => Snapshot((RectTransform)nav.Find("Nav-" + id)));
            yield return MapAndStoryRoundTrip();
            nav = Navigation;
            Assert.That(nav.gameObject.activeInHierarchy, Is.True);
            foreach (string id in Ids)
            {
                var button = nav.Find("Nav-" + id).GetComponent<UnityEngine.UI.Button>();
                Assert.That(button.isActiveAndEnabled && button.IsInteractable(), Is.True, id);
                Assert.That(Snapshot((RectTransform)button.transform), Is.EqualTo(hitsBefore[id]), id + " after return");
            }
            Canvas canvas = nav.GetComponentInParent<Canvas>();
            var camera = new GameObject("Navigation Hit Camera").AddComponent<Camera>();
            var target = new RenderTexture(720, 1536, 24);
            camera.targetTexture = target;
            RenderMode oldMode = canvas.renderMode;
            Camera oldCamera = canvas.worldCamera;
            float oldPlane = canvas.planeDistance;
            try
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 10;
                Canvas.ForceUpdateCanvases();
                yield return null;
                Canvas.ForceUpdateCanvases();
                for (int i = 0; i < Ids.Length; i++)
                {
                    Button button = nav.Find("Nav-" + Ids[i]).GetComponent<Button>();
                    Assert.That(button.transform.Find("Label").GetComponent<Text>().text, Is.EqualTo(Labels[i]));
                    RectTransform hit = (RectTransform)button.transform;
                    var data = new PointerEventData(EventSystem.current)
                    {
                        position = RectTransformUtility.WorldToScreenPoint(camera, hit.TransformPoint(hit.rect.center)),
                        button = PointerEventData.InputButton.Left,
                    };
                    var results = new List<RaycastResult>();
                    EventSystem.current.RaycastAll(data, results);
                    Assert.That(results.Count, Is.GreaterThan(0), button.name);
                    Assert.That(results[0].gameObject.GetComponentInParent<Button>(), Is.EqualTo(button),
                        "Neither the rail, round record nor an adjacent hit area may intercept " + button.name);
                }

                Quaternion railPose = nav.Find("NavOpticalRail").localRotation;
                float oldAngle = nav.Find("NavDiscArc").GetComponent<LobbyNavigationDisc1019>().Angle;
                var click = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
                ExecuteEvents.Execute(nav.Find("Nav-team").gameObject, click, ExecuteEvents.pointerClickHandler);
                // Freeze the new live component before yielding: a slow editor/import frame
                // must not finish the one-shot before this explicit-time assertion begins.
                var turn = nav.GetComponentsInChildren<LobbyNavigationDisc1019>()
                    .Single(x => x.gameObject.activeInHierarchy);
                turn.enabled = false;
                yield return null;
                Assert.That(typeof(ChoSirenApp).GetField("currentScreen", PrivateInstance).GetValue(app), Is.EqualTo("team"));
                nav = Navigation;
                Assert.That(turn.Spinning, Is.True);
                turn.Advance(.25f);
                Assert.That(turn.Angle, Is.GreaterThan(oldAngle).And.LessThan(oldAngle + 60f));
                Assert.That(nav.Find("NavOpticalRail").localRotation, Is.EqualTo(railPose));
                foreach (string id in Ids)
                    Assert.That(Snapshot((RectTransform)nav.Find("Nav-" + id)), Is.EqualTo(hitsBefore[id]), id);
                turn.Advance(1);
                Assert.That(turn.Angle, Is.EqualTo(oldAngle + 60f).Within(.001f));
                Assert.That(turn.Spinning, Is.False);
                float stopped = turn.Angle;
                turn.Advance(10);
                Assert.That(turn.Angle, Is.EqualTo(stopped), "The navigation record must not rotate while idle");
                Assert.That(nav.Find("NavOpticalRail").localRotation, Is.EqualTo(railPose));
            }
            finally
            {
                canvas.renderMode = oldMode;
                canvas.worldCamera = oldCamera;
                canvas.planeDistance = oldPlane;
                camera.targetTexture = null;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(camera.gameObject);
            }
        }

        private IEnumerator MapAndStoryRoundTrip()
        {
            var live = app.GetComponentsInChildren<UnityEngine.UI.Button>(false).Single(button => button.name == "LiveOnStage");
            Click(live);
            LevelMapPanel map = null;
            yield return WaitFor(() =>
            {
                map = app.GetComponentInChildren<LevelMapPanel>();
                return map != null && !ChoSiren.UI.ChallengeVideoTransition.IsCoveringGame;
            }, "The real homepage entry must finish before testing destination input.");
            AssertLobbyDockHidden("map");
            Click(map.GetComponentsInChildren<UnityEngine.UI.Button>(false).Single(button => button.name == "StoryChapter-01"));
            StoryPanel story = null;
            yield return WaitFor(() => (story = app.GetComponentInChildren<StoryPanel>()) != null, "Chapter story must open from its real map button.");
            AssertLobbyDockHidden("story");
            Click(story.GetComponentsInChildren<UnityEngine.UI.Button>(false).Single(button => button.name == "Back"));
            yield return null;
            Assert.That(story == null, Is.True);
            Assert.That(map != null && map.gameObject.activeInHierarchy, Is.True);
            AssertLobbyDockHidden("story back to map");
            Click(map.GetComponentsInChildren<UnityEngine.UI.Button>(false).Single(button => button.name == "Back"));
            yield return WaitFor(() => map == null && Navigation.gameObject.activeInHierarchy &&
                !ChoSiren.UI.ChallengeVideoTransition.IsCoveringGame, "Returning to the lobby must restore the dock after the actual return reveal.");
            Assert.That(typeof(ChoSirenApp).GetField("currentScreen", PrivateInstance).GetValue(app), Is.EqualTo("lobby"));
        }

        private void AssertLobbyDockHidden(string route)
        {
            RectTransform nav = Navigation;
            Assert.That(nav.gameObject.activeSelf, Is.False, route + " must hide the whole dock, including its out-of-safe-area record.");
            foreach (string name in new[] { "NavOpticalRail", "NavDiscArc", "Nav-team", "Nav-members", "Nav-lobby", "Nav-accessory", "Nav-audition" })
                Assert.That(nav.Find(name).gameObject.activeInHierarchy, Is.False, route + ":" + name);
        }

        private static void Click(UnityEngine.UI.Button button)
        {
            Assert.That(button != null && button.isActiveAndEnabled && button.IsInteractable(), Is.True);
            var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler);
        }

        private static IEnumerator WaitFor(System.Func<bool> condition, string reason)
        {
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, reason);
        }

        private RectTransform Navigation => (RectTransform)app.GetComponentsInChildren<RectTransform>(true)
            .First(x => x.name == "BottomNavigation");

        private static Rect Snapshot(RectTransform rect) => new Rect(rect.anchoredPosition, rect.sizeDelta);
    }
}
