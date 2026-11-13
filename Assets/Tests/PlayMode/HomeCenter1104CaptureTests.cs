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
    /// 2026-11-04 (赵金: "这个角色中线偏了，要正一点"): captures the lobby with each of the four homepage idols on a
    /// phone canvas, with the authored still (no idol clip) so the stance can be compared between phases.
    /// Writes Builds/qa-logs/home-center-1104/&lt;phase&gt;/idol-N.png (phase from HOMECENTER_PHASE, default "after").
    /// </summary>
    public sealed class HomeCenter1104CaptureTests
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
            foreach (var old in UnityEngine.Object.FindObjectsByType<ChoSirenApp>(FindObjectsSortMode.None))
                UnityEngine.Object.Destroy(old.gameObject);
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
        public IEnumerator CaptureEachHomeIdol([Values(0, 1, 2, 3, 4, 5)] int index)
        {
            var saveData = new GameSave { HomeCharacterIndex = index, StoryProgress = 100, PlayerName = "经纪人",
                AccountLevel1104 = 7, AccountXp1104 = 300 };
            saveData.CompletedStories.Add(GameModel.PrologueStoryId);
            PlayerPrefs.SetString(GameModel.SaveKey, JsonUtility.ToJson(saveData));
            PlayerPrefs.SetInt(AgreementKey, 1);
            yield return null;

            // HOMECENTER_ORIGINAL=1 captures the authored placement (no centring / tilt) for comparison.
            bool original = Environment.GetEnvironmentVariable("HOMECENTER_ORIGINAL") == "1";
            float keptShift = PsdHomeCharacterCarousel.CenterShift1104[index], keptTilt = PsdHomeCharacterCarousel.Tilt1104[index];
            if (original) { PsdHomeCharacterCarousel.CenterShift1104[index] = 0f; PsdHomeCharacterCarousel.Tilt1104[index] = 0f; }
            var app = new GameObject("HomeCenter1104").AddComponent<ChoSirenApp>();
            var camera = new GameObject("HomeCenter1104Camera", typeof(Camera)).GetComponent<Camera>();
            camera.transform.SetParent(app.transform, false);
            int width = int.TryParse(Environment.GetEnvironmentVariable("HOMECENTER_W"), out int w) ? w : 1080;
            int height = int.TryParse(Environment.GetEnvironmentVariable("HOMECENTER_H"), out int h) ? h : 2288;
            var rt = new RenderTexture(width, height, 24);
            try
            {
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.orthographic = true; camera.targetTexture = rt;
                Canvas canvas = app.GetComponentInChildren<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10;
                SafeAreaFitter.Layout(new Rect(0, 0, width, height), new Vector2(width, height),
                    out Vector2 min, out Vector2 max, out bool wide);
                foreach (var fitter in canvas.GetComponentsInChildren<SafeAreaFitter>(true))
                {
                    fitter.enabled = false;
                    var safe = (RectTransform)fitter.transform;
                    safe.anchorMin = min; safe.anchorMax = max;
                    safe.offsetMin = safe.offsetMax = Vector2.zero;
                }
                canvas.GetComponent<CanvasScaler>().matchWidthOrHeight = wide ? 1f : 0f;
                yield return null; Canvas.ForceUpdateCanvases();
                Invoke(app, "ShowScreenSafely", "lobby");
                yield return null;
                Invoke(app, "CloseModal");
                float until = Time.realtimeSinceStartup + 1.6f;
                while (Time.realtimeSinceStartup < until) yield return null;

                // Keep the authored still on screen: the idol clips carry the same stance.
                // HOMECENTER_LIVE=1 (2026-11-12) leaves the idle clip playing so the live stance can be checked too.
                bool live = Environment.GetEnvironmentVariable("HOMECENTER_LIVE") == "1";
                if (live) { until = Time.realtimeSinceStartup + 2.5f; while (Time.realtimeSinceStartup < until) yield return null; }
                if (!live) foreach (var idols in UnityEngine.Object.FindObjectsByType<PsdHomeIdolVideo1025>(FindObjectsSortMode.None)) idols.enabled = false;
                if (!live) foreach (var surface in UnityEngine.Object.FindObjectsByType<PsdHomeCharacterSurface>(FindObjectsSortMode.None)) surface.gameObject.SetActive(false);
                if (!live) foreach (var clip in canvas.GetComponentsInChildren<RawImage>(true).Where(r => r.name.StartsWith("IdolClip-", StringComparison.Ordinal)))
                    clip.gameObject.SetActive(false);
                yield return null;
                var carousel = UnityEngine.Object.FindAnyObjectByType<PsdHomeCharacterCarousel>();
                Assert.That(carousel, Is.Not.Null);
                Assert.That(carousel.Index, Is.EqualTo(index));
                Image portrait = carousel.transform.Find("layer-04VisualV2").GetComponent<Image>();
                Image rim = carousel.transform.Find("layer-03VisualV2").GetComponent<Image>();
                if (!live) { portrait.enabled = true; portrait.color = Color.white; rim.enabled = true; rim.color = Color.white; }
                Canvas.ForceUpdateCanvases();
                Directory.CreateDirectory(Out);
                camera.Render();
                var previous = RenderTexture.active; RenderTexture.active = rt;
                var shot = new Texture2D(width, height, TextureFormat.RGB24, false);
                shot.ReadPixels(new Rect(0, 0, width, height), 0, 0); shot.Apply();
                RenderTexture.active = previous;
                File.WriteAllBytes(Path.Combine(Out, $"idol-{index}.png"), shot.EncodeToPNG());
                UnityEngine.Object.Destroy(shot);
                RectTransform r = portrait.rectTransform;
                var profileNode = canvas.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(t => t.name == "AccountXpFill1104");
                if (profileNode != null)
                    File.WriteAllText(Path.Combine(Out, $"xp-{index}.txt"), string.Join("\n", profileNode.parent.Cast<Transform>()
                        .Select(t => $"{t.GetSiblingIndex()} {t.name} active={t.gameObject.activeInHierarchy} pos={((RectTransform)t).anchoredPosition} size={((RectTransform)t).sizeDelta}")));
                else File.WriteAllText(Path.Combine(Out, $"xp-{index}.txt"), "no ring");
                File.WriteAllText(Path.Combine(Out, $"idol-{index}.txt"),
                    $"pos={r.anchoredPosition} size={r.sizeDelta} rot={r.localEulerAngles.z} pivot={r.pivot}");
            }
            finally
            {
                PsdHomeCharacterCarousel.CenterShift1104[index] = keptShift; PsdHomeCharacterCarousel.Tilt1104[index] = keptTilt;
                camera.targetTexture = null;
                rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
            }
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
