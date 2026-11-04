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
                Assert.That(canvas.GetComponentsInChildren<RectTransform>(true).Any(r => r.name == "WorkshopDress"), Is.True);
                Shot(camera, rt, "workshop.png");
                var pageRect = canvas.GetComponentsInChildren<RectTransform>(true).First(r => r.name == "WorkshopPage1104");
                File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/qa-logs/topup-1104/order.txt")),
                    string.Join("\n", pageRect.parent.Cast<Transform>().Select(t => $"{t.GetSiblingIndex()} {t.name} {t.gameObject.activeSelf} canvas={(t.GetComponent<Canvas>() != null)}")));
                for (int tab = 1; tab < 4; tab++)
                {
                    canvas.GetComponentsInChildren<Button>(true).First(b => b.name == "WorkshopTab-" + tab).onClick.Invoke();
                    yield return new WaitForSecondsRealtime(.3f);
                    Shot(camera, rt, $"workshop-tab{tab}.png");
                }
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
