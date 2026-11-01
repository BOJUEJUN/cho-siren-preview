using System.Collections;
using System.IO;
using System.Reflection;
using ChoSiren.Panels;
using ChoSiren.Systems.Presentation;
using ChoSiren.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ChoSiren.Tests
{
    /// <summary>
    /// 2026-11-01 practice room (练习室) rebuild: a real pointer click on a stage figure switches the
    /// trainee (hit-tested through the EventSystem, nothing may cover the figure), the practice then
    /// trains only that member, and screenshots of before / switched / result are written for two
    /// portrait ratios into Builds/qa-logs/zj-1101b/practice-ui/.
    /// </summary>
    public sealed class MemberPracticeUi1101Tests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private ChoSirenApp app;
        private string save, legacy;
        private bool had, hadLegacy, hadMotion;
        private int motion;

        private static string Out => Path.GetFullPath(Path.Combine(Application.dataPath,
            "../Builds/qa-logs/zj-1101b/practice-ui"));

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            had = PlayerPrefs.HasKey(GameModel.SaveKey); save = PlayerPrefs.GetString(GameModel.SaveKey);
            hadLegacy = PlayerPrefs.HasKey(GameModel.LegacySaveKey); legacy = PlayerPrefs.GetString(GameModel.LegacySaveKey);
            hadMotion = PlayerPrefs.HasKey(MemberTrainingPractice.ReduceMotionPreferenceKey);
            motion = PlayerPrefs.GetInt(MemberTrainingPractice.ReduceMotionPreferenceKey, 0);
            PlayerPrefs.SetInt(MemberTrainingPractice.ReduceMotionPreferenceKey, 0);
            var fresh = new GameSave { HomeCharacterIndex = 0, StoryProgress = 100, Gold = 9000000 };
            fresh.UnlockedMembers = new System.Collections.Generic.List<int> { 0, 1, 2, 3, 4, 5, 6 };
            fresh.Team = new System.Collections.Generic.List<int> { 0, 1, 2, 3 };
            PlayerPrefs.SetString(GameModel.SaveKey, JsonUtility.ToJson(fresh));
            foreach (var old in Object.FindObjectsByType<ChoSirenApp>(FindObjectsSortMode.None)) Object.Destroy(old.gameObject);
            yield return null;
            app = new GameObject("MemberPracticeUi1101").AddComponent<ChoSirenApp>();
            yield return null; Canvas.ForceUpdateCanvases(); yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (app != null) Object.Destroy(app.gameObject);
            yield return null;
            if (had) PlayerPrefs.SetString(GameModel.SaveKey, save); else PlayerPrefs.DeleteKey(GameModel.SaveKey);
            if (hadLegacy) PlayerPrefs.SetString(GameModel.LegacySaveKey, legacy); else PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
            if (hadMotion) PlayerPrefs.SetInt(MemberTrainingPractice.ReduceMotionPreferenceKey, motion);
            else PlayerPrefs.DeleteKey(MemberTrainingPractice.ReduceMotionPreferenceKey);
            PlayerPrefs.Save();
        }

        [UnityTest]
        public IEnumerator PointerClickOnStageFigureSwitchesTraineeAndTrainsOnlyThatMember()
        {
            GameModel model = Model();
            int first = model.Save.Team[0];
            yield return OpenPractice(first);
            MemberPracticePanel panel = Object.FindAnyObjectByType<MemberPracticePanel>();
            Assert.That(panel, Is.Not.Null);
            SirenPracticeScene stage = panel.Stage;
            Assert.That(stage.Count, Is.EqualTo(model.Save.UnlockedMembers.Count));
            Assert.That(stage.PageCount, Is.GreaterThan(1), "超过 4 名成员要能翻页");

            int target = model.Save.Team[1];
            int targetLevel = model.LevelOf(target), firstLevel = model.LevelOf(first);
            RectTransform actor = stage.ActorRect(target);
            Assert.That(ClickThroughEventSystem(actor), Is.True, "小人必须能被真实指针点到（不能被其它控件挡住）");
            yield return new WaitForSecondsRealtime(.6f);

            Assert.That(panel.MemberIndex, Is.EqualTo(target));
            Assert.That(stage.SelectedMember, Is.EqualTo(target));
            Assert.That(stage.SelectionAmount(target), Is.GreaterThan(.95f));
            Assert.That(stage.SelectionAmount(first), Is.LessThan(.05f));
            Assert.That(Find(panel.transform, "PracticeMemberName").GetComponent<Text>().text,
                Is.EqualTo(GameModel.Members[target].Name));
            Assert.That(Find(panel.transform, "PracticePortrait").GetComponent<Image>().sprite,
                Is.EqualTo(Resources.Load<Sprite>(GameModel.Members[target].ResourcePath)));

            // Page two shows the members beyond the first four; paging back keeps the selection.
            stage.ShowPage(1);
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(stage.Page, Is.EqualTo(1));
            stage.ShowPage(0);
            yield return new WaitForSecondsRealtime(.4f);

            Find(panel.transform, "StartPractice").GetComponent<Button>().onClick.Invoke();
            Assert.That(panel.IsAnimating, Is.True);
            Assert.That(ClickThroughEventSystem(stage.ActorRect(first)), Is.True);
            yield return null;
            Assert.That(panel.MemberIndex, Is.EqualTo(target), "练习进行中不得切换成员");
            float deadline = Time.realtimeSinceStartup + 6f;
            while (panel.IsAnimating && Time.realtimeSinceStartup < deadline) yield return null;
            yield return new WaitForSecondsRealtime(2f);
            Assert.That(panel.ResultVisible, Is.True);
            Assert.That(model.LevelOf(target), Is.EqualTo(targetLevel + 1));
            Assert.That(model.LevelOf(first), Is.EqualTo(firstLevel));
            Assert.That(Find(panel.transform, "PracticeDelta-等级").GetComponent<Text>().text,
                Does.Contain((targetLevel + 1).ToString()));

            Find(panel.transform, "PracticeResultOk").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.That(panel.ResultVisible, Is.False);
            Assert.That(Find(panel.transform, "StartPracticeLabel").GetComponent<Text>().text, Is.EqualTo("再练一次"));
        }

        [UnityTest]
        public IEnumerator ReduceMotionFreezesTheStageAndStillShowsTheFinalNumbers()
        {
            PlayerPrefs.SetInt(MemberTrainingPractice.ReduceMotionPreferenceKey, 1);
            GameModel model = Model();
            int first = model.Save.Team[0], target = model.Save.Team[1];
            int level = model.LevelOf(target);
            yield return OpenPractice(first);
            MemberPracticePanel panel = Object.FindAnyObjectByType<MemberPracticePanel>();
            Assert.That(panel.ReduceMotion, Is.True);
            Assert.That(ClickThroughEventSystem(panel.Stage.ActorRect(target)), Is.True);
            yield return null;
            Assert.That(panel.Stage.SelectionAmount(target), Is.EqualTo(1f), "减弱动画下选中立即到位");
            Transform body = panel.Stage.ActorRect(target).Find("Body");
            Vector2 pose = ((RectTransform)body).anchoredPosition;
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(((RectTransform)body).anchoredPosition, Is.EqualTo(pose), "减弱动画下小人不得待机跳动");

            Find(panel.transform, "StartPractice").GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSecondsRealtime(MemberTrainingPractice.ReducedDurationSeconds + .4f);
            Assert.That(panel.IsAnimating, Is.False);
            Assert.That(panel.ResultVisible, Is.True);
            Assert.That(Find(panel.transform, "PracticeDelta-等级").GetComponent<Text>().text,
                Does.Contain((level + 1).ToString()));
        }

        [UnityTest]
        public IEnumerator CapturePhoneRatio1080x2288() => Capture(1080, 2288, "phone");

        [UnityTest]
        public IEnumerator CaptureMacWindowRatio1080x2160() => Capture(1080, 2160, "mac");

        private IEnumerator Capture(int width, int height, string prefix)
        {
            var camera = new GameObject("Practice1101Camera", typeof(Camera)).GetComponent<Camera>();
            var rt = new RenderTexture(width, height, 24);
            try
            {
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.orthographic = true; camera.targetTexture = rt;
                var canvas = app.GetComponentInChildren<Canvas>();
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

                GameModel model = Model();
                yield return OpenPractice(model.Save.Team[0]);
                MemberPracticePanel panel = Object.FindAnyObjectByType<MemberPracticePanel>();
                Assert.That(panel, Is.Not.Null);
                var host = (RectTransform)panel.transform;
                Assert.That(host.rect.height, Is.GreaterThan(1300f), "截图应使用竖屏比例");
                yield return new WaitForSecondsRealtime(.8f);
                AssertInsideScreen(panel);
                Shot(camera, rt, prefix + "-01-before.png");

                int target = model.Save.Team[1];
                Find(panel.transform, "PracticeActor-" + GameModel.Members[target].Id).GetComponent<Button>().onClick.Invoke();
                yield return new WaitForSecondsRealtime(.8f);
                Assert.That(panel.MemberIndex, Is.EqualTo(target));
                Shot(camera, rt, prefix + "-02-switched.png");

                panel.Stage.ShowPage(1);
                yield return new WaitForSecondsRealtime(.6f);
                Shot(camera, rt, prefix + "-03-page2.png");
                panel.Stage.ShowPage(0);
                yield return new WaitForSecondsRealtime(.4f);

                Find(panel.transform, "StartPractice").GetComponent<Button>().onClick.Invoke();
                yield return new WaitForSecondsRealtime(.7f);
                Shot(camera, rt, prefix + "-04-practicing.png");
                float deadline = Time.realtimeSinceStartup + 6f;
                while (panel.IsAnimating && Time.realtimeSinceStartup < deadline) yield return null;
                yield return new WaitForSecondsRealtime(.55f);
                Shot(camera, rt, prefix + "-05-result-levelflash.png");
                yield return new WaitForSecondsRealtime(1.6f);
                Assert.That(panel.ResultVisible, Is.True);
                Shot(camera, rt, prefix + "-06-result.png");
            }
            finally
            {
                Object.DestroyImmediate(camera.gameObject); rt.Release(); Object.DestroyImmediate(rt);
            }
        }

        private GameModel Model() => (GameModel)typeof(ChoSirenApp).GetField("model", Private).GetValue(app);

        private IEnumerator OpenPractice(int member)
        {
            typeof(ChoSirenApp).GetMethod("ShowScreenSafely", Private)?.Invoke(app, new object[] { "lobby" });
            yield return null;
            typeof(ChoSirenApp).GetMethod("CloseModal", Private)?.Invoke(app, null);
            typeof(ChoSirenApp).GetMethod("OpenTrainingPractice", Private).Invoke(app, new object[] { member, -1 });
            yield return null; Canvas.ForceUpdateCanvases(); yield return null;
        }

        private static bool ClickThroughEventSystem(RectTransform target)
        {
            EventSystem events = EventSystem.current;
            Assert.That(events, Is.Not.Null);
            Canvas canvas = target.GetComponentInParent<Canvas>().rootCanvas;
            Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector3 world = target.TransformPoint(target.rect.center + new Vector2(0f, target.rect.height * .15f));
            var data = new PointerEventData(events) { position = RectTransformUtility.WorldToScreenPoint(cam, world) };
            var hits = new System.Collections.Generic.List<RaycastResult>();
            events.RaycastAll(data, hits);
            if (hits.Count == 0 || !hits[0].gameObject.transform.IsChildOf(target)) return false;
            data.pointerPressRaycast = data.pointerCurrentRaycast = hits[0];
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerClickHandler);
            return true;
        }

        private static void AssertInsideScreen(MemberPracticePanel panel)
        {
            var host = (RectTransform)panel.transform;
            Rect bounds = host.rect;
            foreach (string name in new[] { "PracticeStage", "PracticeMemberCard", "PracticeProjectCard", "StartPractice", "PracticeDone" })
            {
                var rect = (RectTransform)Find(panel.transform, name);
                Vector3[] corners = new Vector3[4];
                rect.GetWorldCorners(corners);
                foreach (Vector3 corner in corners)
                {
                    Vector3 local = host.InverseTransformPoint(corner);
                    Assert.That(local.y, Is.InRange(bounds.yMin - 1f, bounds.yMax + 1f), name + " 超出屏幕");
                }
            }
        }

        private static Transform Find(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child;
            Assert.Fail("未找到界面节点：" + name);
            return null;
        }

        private static void Shot(Camera camera, RenderTexture rt, string name)
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
