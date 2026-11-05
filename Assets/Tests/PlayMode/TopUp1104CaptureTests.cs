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
    /// <summary>
    /// 2026-11-04: captures the 星钻 panel and the top-up shop into Builds/qa-logs/topup-1104/.
    /// </summary>
    public sealed class TopUp1104CaptureTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const string AgreementKey = "cho-agreement-v1";
        private static string Phase => Environment.GetEnvironmentVariable("HOMECENTER_PHASE") is string p && p.Length > 0 ? p : "after";
        private static string Out => Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/qa-logs/home-center-1104", Phase));

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

        [UnityTest]
        public IEnumerator CaptureDiamondPanelAndTopUpShop()
        {
            var saveData = new GameSave { HomeCharacterIndex = 1, StoryProgress = 100, PlayerName = "经纪人" };
            saveData.CompletedStories.Add(GameModel.PrologueStoryId);
            PlayerPrefs.SetString(GameModel.SaveKey, JsonUtility.ToJson(saveData));
            PlayerPrefs.SetInt(AgreementKey, 1);
            foreach (var old in UnityEngine.Object.FindObjectsByType<ChoSirenApp>(FindObjectsSortMode.None)) UnityEngine.Object.Destroy(old.gameObject);
            yield return null;
            var app = new GameObject("TopUp1104").AddComponent<ChoSirenApp>();
            var camera = new GameObject("TopUp1104Camera", typeof(Camera)).GetComponent<Camera>();
            camera.transform.SetParent(app.transform, false);
            const int width = 1080, height = 2288;
            var rt = new RenderTexture(width, height, 24);
            try
            {
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
                Invoke(app, "OpenCurrency1101", ChoSiren.Systems.Economy.CurrencyIds.Diamond);
                yield return new WaitForSecondsRealtime(.8f);
                Shot(camera, rt, "diamond-panel.png");
                Invoke(app, "OpenTopUp1104");
                yield return new WaitForSecondsRealtime(.6f);
                Shot(camera, rt, "topup-shop-pre.png");
                Invoke(app, "CloseModal");
                yield return null;
                // 2026-11-04: the former 专辑制作 card opens 创意工坊.
                Invoke(app, "ActivatePsdHomeAction", "AlbumProduction");
                yield return new WaitForSecondsRealtime(.8f);
                Assert.That(canvas.GetComponentsInChildren<RectTransform>(true).Any(r => r.name == "WorkshopPage1104"), Is.True, "创意工坊 opens");
                Assert.That(canvas.GetComponentsInChildren<RectTransform>(true).Any(r => r.name == "WorkshopTool-filter"), Is.True);
                yield return new WaitForSecondsRealtime(2.5f);
                Assert.That(canvas.GetComponentsInChildren<RawImage>(true).Any(r => r.name == "WorkshopFigureVideo" && r.enabled),
                    Is.True, "初始角色's idle loop plays");
                Shot(camera, rt, "workshop.png");
                var stage = UnityEngine.Object.FindAnyObjectByType<WorkshopStage1104>();
                Assert.That(stage, Is.Not.Null);
                Button Tool(string id) => canvas.GetComponentsInChildren<Button>(true).First(b => b.name == "WorkshopTool-" + id);
                Tool("bright").onClick.Invoke();
                yield return new WaitForSecondsRealtime(.5f);
                stage.SetDialValue(80);
                yield return new WaitForSecondsRealtime(.7f);
                Assert.That(stage.Raise, Is.GreaterThan(.9f), "the dial slides out while a value is set");
                Shot(camera, rt, "workshop-dial.png");
                yield return new WaitForSecondsRealtime(2.6f);
                Assert.That(stage.Raise, Is.LessThan(.1f), "the dial slides back to a sliver");
                Shot(camera, rt, "workshop-tucked.png");
                Tool("dress").onClick.Invoke();
                yield return new WaitForSecondsRealtime(.6f);
                Assert.That(canvas.GetComponentsInChildren<RectTransform>(true).Any(r => r.name == "AiDressPanel"), Is.True, "AI 换装 stays in game");
                Shot(camera, rt, "workshop-ai.png");
                // 2026-11-05: one real generation through the game's service (costs one model call; opt-in).
                if (Environment.GetEnvironmentVariable("WORKSHOP_AI_LIVE") == "1")
                {
                    Invoke(app, "StartAiDress1104");
                    FieldInfo busy = typeof(ChoSirenApp).GetField("aiDressBusy1104", Private);
                    float started = Time.realtimeSinceStartup;
                    while ((bool)busy.GetValue(app) && Time.realtimeSinceStartup - started < 200f) yield return new WaitForSecondsRealtime(1f);
                    Debug.Log("[AI1105] " + typeof(ChoSirenApp).GetField("aiDressStatus1104", Private).GetValue(app)
                        + $" after {Time.realtimeSinceStartup - started:0}s");
                    Assert.That(typeof(ChoSirenApp).GetField("aiDressResult1104", Private).GetValue(app), Is.Not.Null, "AI 换装 returns an outfit");
                    yield return new WaitForSecondsRealtime(.8f);
                    Shot(camera, rt, "workshop-ai-result.png");
                    Invoke(app, "CloseModal");
                    yield return new WaitForSecondsRealtime(1.5f);
                    Shot(camera, rt, "workshop-ai-result-full.png");
                    stage.SetOverride(null);
                }
                Tool("accent").onClick.Invoke();
                yield return new WaitForSecondsRealtime(.4f);
                stage.SetDialValue(1);
                yield return new WaitForSecondsRealtime(.8f);
                Shot(camera, rt, "workshop-accent.png");
                Tool("bg").onClick.Invoke();
                yield return new WaitForSecondsRealtime(.4f);
                stage.SetDialValue(2);
                yield return new WaitForSecondsRealtime(1f);
                Shot(camera, rt, "workshop-bg.png");
                // 星璃 has the angle sheets; the starter does not.
                Invoke(app, "StepWorkshopMember1104", 1);
                yield return new WaitForSecondsRealtime(.8f);
                stage = UnityEngine.Object.FindAnyObjectByType<WorkshopStage1104>();
                Tool("turn").onClick.Invoke();
                stage.TurnTo(3);
                yield return new WaitForSecondsRealtime(1.2f);
                Assert.That(stage.View, Is.EqualTo(3), "转身 reaches the back view");
                Shot(camera, rt, "workshop-back.png");
                stage.TurnTo(2);
                yield return new WaitForSecondsRealtime(.6f);
                Shot(camera, rt, "workshop-side.png");
                Invoke(app, "OpenVoiceRecorder1104");
                yield return new WaitForSecondsRealtime(.5f);
                Shot(camera, rt, "workshop-recorder.png");
                Invoke(app, "CloseModal");
                Invoke(app, "CloseWorkshop1104");
                yield return null;
                Invoke(app, "OpenTopUp1104");
                yield return new WaitForSecondsRealtime(.8f);
                Shot(camera, rt, "topup-shop.png");
                Button buy = canvas.GetComponentsInChildren<Button>(true).First(b => b.name == "TopUpBuy-diamond-60");
                int diamonds = ((GameModel)typeof(ChoSirenApp).GetField("model", Private).GetValue(app)).Save.Diamonds;
                buy.onClick.Invoke();
                yield return new WaitForSecondsRealtime(.4f);
                Shot(camera, rt, "topup-tap.png");
                Assert.That(((GameModel)typeof(ChoSirenApp).GetField("model", Private).GetValue(app)).Save.Diamonds, Is.EqualTo(diamonds));
            }
            finally
            {
                camera.targetTexture = null;
                rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
            }
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
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/qa-logs/topup-1104"));
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, name), shot.EncodeToPNG());
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
