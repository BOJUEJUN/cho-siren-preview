using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using ChoSiren.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ChoSiren.Tests
{
    /// <summary>2026-11-07: captures the six-screen pass (title loading run, practice camera view, …) into Builds/qa-logs/ui-1107/.</summary>
    public sealed class Ui1107CaptureTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const string AgreementKey = "cho-agreement-v1";
        private static string Out => Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/qa-logs/ui-1107"));
        private string save;
        private bool had, hadAgreement;
        private int agreement;

        [UnitySetUp] public IEnumerator SetUp()
        {
            had = PlayerPrefs.HasKey(GameModel.SaveKey); save = PlayerPrefs.GetString(GameModel.SaveKey);
            hadAgreement = PlayerPrefs.HasKey(AgreementKey); agreement = PlayerPrefs.GetInt(AgreementKey, 0);
            yield return null;
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            foreach (var old in UnityEngine.Object.FindObjectsByType<ChoSirenApp>(FindObjectsSortMode.None)) UnityEngine.Object.Destroy(old.gameObject);
            yield return null;
            if (had) PlayerPrefs.SetString(GameModel.SaveKey, save); else PlayerPrefs.DeleteKey(GameModel.SaveKey);
            if (hadAgreement) PlayerPrefs.SetInt(AgreementKey, agreement); else PlayerPrefs.DeleteKey(AgreementKey);
            PlayerPrefs.Save();
        }

        private ChoSirenApp app;
        private Camera camera;
        private RenderTexture rt;

        private IEnumerator Boot(GameSave saveData)
        {
            saveData.CompletedStories.Add(GameModel.PrologueStoryId);
            PlayerPrefs.SetString(GameModel.SaveKey, JsonUtility.ToJson(saveData));
            PlayerPrefs.SetInt(AgreementKey, 1);
            foreach (var old in UnityEngine.Object.FindObjectsByType<ChoSirenApp>(FindObjectsSortMode.None)) UnityEngine.Object.Destroy(old.gameObject);
            yield return null;
            app = new GameObject("Ui1107Capture").AddComponent<ChoSirenApp>();
            camera = new GameObject("Ui1107Camera", typeof(Camera)).GetComponent<Camera>();
            camera.transform.SetParent(app.transform, false);
            const int width = 1080, height = 2288;
            rt = new RenderTexture(width, height, 24);
            Directory.CreateDirectory(Out);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            camera.orthographic = true; camera.targetTexture = rt;
            Canvas canvas = app.GetComponentInChildren<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10;
            SafeAreaFitter.Layout(new Rect(0, 0, width, height), new Vector2(width, height), out Vector2 min, out Vector2 max, out bool wide);
            foreach (var fitter in canvas.GetComponentsInChildren<SafeAreaFitter>(true))
            {
                fitter.enabled = false;
                var safe = (RectTransform)fitter.transform;
                safe.anchorMin = min; safe.anchorMax = max; safe.offsetMin = safe.offsetMax = Vector2.zero;
            }
            canvas.GetComponent<CanvasScaler>().matchWidthOrHeight = wide ? 1f : 0f;
            yield return null;
            Invoke(app, "ShowScreenSafely", "lobby");
            yield return null;
            Invoke(app, "CloseModal");
            yield return new WaitForSecondsRealtime(1f);
        }

        private GameModel Model => (GameModel)typeof(ChoSirenApp).GetField("model", Private).GetValue(app);
        private Transform SafeRoot => (Transform)typeof(ChoSirenApp).GetField("safeRoot", Private).GetValue(app);

        private void Release()
        {
            if (camera != null) camera.targetTexture = null;
            if (rt != null) rt.Release();
        }

        [UnityTest]
        public IEnumerator CaptureLobbyAndPracticeLevels()
        {
            var saveData = new GameSave { HomeCharacterIndex = 1, StoryProgress = 100, PlayerName = "经纪人", Gold = 50000, VoiceCrystals = 7 };
            foreach (string id in new[] { "stage-1-5", "stage-1-10", "stage-2-5", "stage-2-10" })
                saveData.ClearedStages.Add(new StageClear { Id = id, Stars = 3 });
            yield return Boot(saveData);
            try
            {
                yield return new WaitForSecondsRealtime(.5f);
                Shot(camera, rt, "lobby.png");
                GameModel model = Model;
                int member = model.Save.Team[0];
                model.Save.MemberLevels[member] = 30;
                Invoke(app, "OpenTrainingPractice", member, 0);
                yield return new WaitForSecondsRealtime(2.5f);
                Assert.That(model.NeedsBreakthrough1104(member), Is.True);
                var stage = UnityEngine.Object.FindAnyObjectByType<PracticeVideoStage1107>();
                Assert.That(stage, Is.Not.Null);
                Shot(camera, rt, "practice-lv30-break.png");
                Assert.That(stage.Clips.Showing, Is.True, "a practice clip is on screen");
                Invoke(app, "CloseModal");
                model.Save.MemberLevels[member] = 35;
                Invoke(app, "ShowScreenSafely", "lobby");
                yield return null;
                Invoke(app, "OpenTrainingPractice", member, 0);
                yield return new WaitForSecondsRealtime(1.5f);
                Shot(camera, rt, "practice-lv35-remnants.png");
            }
            finally { Release(); }
        }

        [UnityTest]
        public IEnumerator CaptureTitleLoadingRun()
        {
            yield return Boot(new GameSave { StoryProgress = 100, PlayerName = "经纪人" });
            try
            {
                NativeTitleScreen title = NativeTitleScreen.Create(SafeRoot.parent, (RectTransform)SafeRoot, false);
                title.SetProgress(.45f);
                yield return new WaitForSecondsRealtime(2.5f);
                var run = title.GetComponentInChildren<LoadingRun1107>();
                Assert.That(run, Is.Not.Null);
                Assert.That(run.Progress, Is.EqualTo(.45f).Within(.02f), "the bow follows the real progress");
                Shot(camera, rt, "title-loading-45.png");
                Assert.That(run.Clips.Showing, Is.True, "the runners clip is on screen");
                title.SetProgress(.9f);
                yield return new WaitForSecondsRealtime(1f);
                Shot(camera, rt, "title-loading-90.png");
                UnityEngine.Object.Destroy(title.gameObject);
            }
            finally { Release(); }
        }

        [UnityTest]
        public IEnumerator CaptureSecondaryPages()
        {
            var saveData = new GameSave { HomeCharacterIndex = 1, StoryProgress = 100, PlayerName = "经纪人", Gold = 50000 };
            yield return Boot(saveData);
            try
            {
                GameModel model = Model;
                model.Save.OwnedAccessories.Clear();
                model.Save.OwnedAccessories.AddRange(new[] { 0, 1, 2, 3, 6, 7, 12, 13, 21, 22, 30, 31, 40, 41, 50, 60 });
                Invoke(app, "ShowScreenSafely", "accessory");
                yield return new WaitForSecondsRealtime(1f);
                Shot(camera, rt, "inventory.png");
                // 2026-11-12 (原画同事: "这个角色缩小，3 分之一"): the pig is 2/3 of its PSD size and clears the sort plate.
                var pig = (RectTransform)GameObject.Find("InventoryPig").transform;
                var sortPlate = (RectTransform)GameObject.Find("InventorySort").transform;
                Assert.That(pig.rect.width, Is.EqualTo(422f * .65f * 2f / 3f).Within(.5f));
                Vector3[] pc = new Vector3[4], sc = new Vector3[4];
                pig.GetWorldCorners(pc); sortPlate.GetWorldCorners(sc);
                Assert.That(pc[0].y, Is.GreaterThanOrEqualTo(sc[1].y - .001f), "the pig no longer sits on the 强化等级 plate");
                Invoke(app, "OpenDailyTasks");
                yield return new WaitForSecondsRealtime(1f);
                Shot(camera, rt, "tasks.png");
                foreach (var panel in UnityEngine.Object.FindObjectsByType<ChoSiren.Panels.TaskBoardPanel>(FindObjectsSortMode.None))
                    UnityEngine.Object.Destroy(panel.gameObject);
                yield return null;
                Invoke(app, "ShowScreenSafely", "team");
                yield return new WaitForSecondsRealtime(1f);
                Shot(camera, rt, "team.png");
                Invoke(app, "ShowScreenSafely", "audition");
                yield return new WaitForSecondsRealtime(1.5f);
                Shot(camera, rt, "audition.png");
            }
            finally { Release(); }
        }

        [UnityTest]
        public IEnumerator CaptureTeamStaminaAndRestDialog()
        {
            yield return Boot(new GameSave { HomeCharacterIndex = 1, StoryProgress = 100, PlayerName = "经纪人", Gold = 50000 });
            try
            {
                GameModel model = Model;
                var set = typeof(GameModel).GetMethod("SetMemberStamina1107", Private);
                set.Invoke(model, new object[] { model.Save.Team[1], 30 });   // 疲劳
                set.Invoke(model, new object[] { model.Save.Team[2], 4 });    // 需休息
                set.Invoke(model, new object[] { model.Save.Team[3], 90 });
                Invoke(app, "ShowScreenSafely", "team");
                yield return new WaitForSecondsRealtime(1f);
                Shot(camera, rt, "team-stamina.png");
                Assert.That(GameObject.Find("TeamStamina-" + model.Save.Team[2]), Is.Not.Null);
                Invoke(app, "OpenRestDialog1107", model.Save.Team[2]);
                yield return new WaitForSecondsRealtime(.6f);
                Shot(camera, rt, "team-rest-dialog.png");
                var rest = GameObject.Find("RestNow").GetComponent<Button>();
                int gold = model.Save.Gold;
                rest.onClick.Invoke();
                yield return new WaitForSecondsRealtime(.6f);
                Assert.That(model.MemberStamina1107(model.Save.Team[2]), Is.EqualTo(GameModel.MemberStaminaMax1107));
                Assert.That(model.Save.Gold, Is.EqualTo(gold - 116 * GameModel.RestGoldPerPoint1107));
                Shot(camera, rt, "team-after-rest.png");
            }
            finally { Release(); }
        }

        private static void Shot(Camera camera, RenderTexture rt, string name)
        {
            foreach (Canvas stray in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!stray.isRootCanvas || stray.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                stray.renderMode = RenderMode.ScreenSpaceCamera; stray.worldCamera = camera; stray.planeDistance = 5;
            }
            Canvas.ForceUpdateCanvases();
            camera.Render();
            var previous = RenderTexture.active; RenderTexture.active = rt;
            var shot = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            shot.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); shot.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(Path.Combine(Out, name), shot.EncodeToPNG());
            UnityEngine.Object.Destroy(shot);
        }


        private static void Invoke(object target, string method, params object[] args)
        {
            MethodInfo info = target.GetType().GetMethods(Private)
                .FirstOrDefault(m => m.Name == method && m.GetParameters().Length == args.Length);
            if (info == null) throw new MissingMethodException(target.GetType().Name, method);
            info.Invoke(target, args);
        }
    }
}
