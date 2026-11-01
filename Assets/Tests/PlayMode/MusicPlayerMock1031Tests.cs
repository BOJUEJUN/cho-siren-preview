using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ChoSiren.Tests
{
    /// <summary>2026-10-31: clean lobby captures (no dialogue / no music bar) used as the backdrop of the music-player design mocks.</summary>
    public sealed class MusicPlayerMock1031Tests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private ChoSirenApp app;
        private string save, legacy;
        private bool had, hadLegacy;
        private static string Out => Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/qa-logs/music-1031"));

        [UnitySetUp] public IEnumerator SetUp()
        {
            had = PlayerPrefs.HasKey(GameModel.SaveKey); save = PlayerPrefs.GetString(GameModel.SaveKey);
            hadLegacy = PlayerPrefs.HasKey(GameModel.LegacySaveKey); legacy = PlayerPrefs.GetString(GameModel.LegacySaveKey);
            PlayerPrefs.SetString(GameModel.SaveKey, JsonUtility.ToJson(new GameSave { HomeCharacterIndex = 0, StoryProgress = 100 }));
            foreach (var old in Object.FindObjectsByType<ChoSirenApp>(FindObjectsSortMode.None)) Object.Destroy(old.gameObject);
            yield return null;
            app = new GameObject("MusicPlayerMock1031").AddComponent<ChoSirenApp>();
            yield return null; Canvas.ForceUpdateCanvases(); yield return null;
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            if (app != null) Object.Destroy(app.gameObject);
            yield return null;
            if (had) PlayerPrefs.SetString(GameModel.SaveKey, save); else PlayerPrefs.DeleteKey(GameModel.SaveKey);
            if (hadLegacy) PlayerPrefs.SetString(GameModel.LegacySaveKey, legacy); else PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
            PlayerPrefs.Save();
        }

        [UnityTest] public IEnumerator CaptureCleanLobbyBackdrops()
        {
            var camera = new GameObject("MusicMockCamera", typeof(Camera)).GetComponent<Camera>();
            var rt = new RenderTexture(1080, 2288, 24);
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
                typeof(ChoSirenApp).GetMethod("ShowScreenSafely", Private)?.Invoke(app, new object[] { "lobby" });
                yield return null;
                typeof(ChoSirenApp).GetMethod("CloseModal", Private)?.Invoke(app, null);
                yield return new WaitForSecondsRealtime(1.4f);
                GameObject home = GameObject.Find("PsdHome20260921");
                Assert.That(home, Is.Not.Null);
                var motion = home.GetComponent<PsdHomeMotion>();
                motion.CancelCharacterInteraction();
                Hide(home.transform);
                yield return new WaitForSecondsRealtime(.2f);
                Capture(camera, rt, "clean-lobby-0.png");
                Transform music = home.transform.Find("MusicNext");
                if (music != null) music.gameObject.SetActive(true);
                foreach (var t in home.GetComponentsInChildren<Transform>(true))
                    if (t.name == "PsdCharacterDialogue") t.gameObject.SetActive(false);
                yield return new WaitForSecondsRealtime(.2f);
                Capture(camera, rt, "current-lobby.png");
            }
            finally
            {
                Object.DestroyImmediate(camera.gameObject); rt.Release(); Object.DestroyImmediate(rt);
            }
        }

        private static void Hide(Transform home)
        {
            Transform music = home.Find("MusicNext");
            if (music != null) music.gameObject.SetActive(false);
            foreach (var t in home.GetComponentsInChildren<Transform>(true))
                if (t.name == "PsdCharacterDialogue") t.gameObject.SetActive(false);
        }

        private static void Capture(Camera camera, RenderTexture rt, string name)
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
