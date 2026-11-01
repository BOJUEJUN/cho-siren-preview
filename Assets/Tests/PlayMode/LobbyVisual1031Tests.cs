using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ChoSiren.Tests
{
    /// <summary>
    /// 2026-10-31 (赵金 lobby batch): screenshot-only capture of the authored lobby — the character
    /// dialogue, the selected nav flame edge, a press flare on the top-right buttons and the
    /// torn-paper notice stickers — into Builds/qa-logs/zj-1031/lobby/ for visual review.
    /// </summary>
    public sealed class LobbyVisual1031Tests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private ChoSirenApp app;
        private string save, legacy;
        private bool had, hadLegacy;

        private static string Out => Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/qa-logs/zj-1031/lobby"));
        private static string Prefix => System.Environment.GetEnvironmentVariable("LOBBY1031_SHOT") ?? "shot";
        private GameObject Home => GameObject.Find("PsdHome20260921");

        [UnitySetUp] public IEnumerator SetUp()
        {
            had = PlayerPrefs.HasKey(GameModel.SaveKey); save = PlayerPrefs.GetString(GameModel.SaveKey);
            hadLegacy = PlayerPrefs.HasKey(GameModel.LegacySaveKey); legacy = PlayerPrefs.GetString(GameModel.LegacySaveKey);
            PlayerPrefs.SetString(GameModel.SaveKey, JsonUtility.ToJson(new GameSave { HomeCharacterIndex = 0, StoryProgress = 100 }));
            foreach (var old in Object.FindObjectsByType<ChoSirenApp>(FindObjectsSortMode.None)) Object.Destroy(old.gameObject);
            yield return null;
            app = new GameObject("LobbyVisual1031").AddComponent<ChoSirenApp>();
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

        [UnityTest] public IEnumerator CaptureLobbyDialogueFlameAndNoticeStickers()
        {
            var camera = new GameObject("Lobby1031Camera", typeof(Camera)).GetComponent<Camera>();
            var rt = new RenderTexture(1080, 2288, 24);
            try
            {
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.orthographic = true; camera.targetTexture = rt;
                var canvas = app.GetComponentInChildren<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
                // Batchmode reports a 640×480 landscape screen; pin the portrait phone layout the
                // capture represents (full-width safe area, width-matched scaler) and rebuild the lobby.
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
                yield return new WaitForSecondsRealtime(1.2f);
                Assert.That(Home, Is.Not.Null);
                // Every notice the lobby can raise, shown together for review.
                foreach (string field in new[] { "psdTaskBadge", "psdMailBadge", "psdProfileBadge", "psdAlbumTag" })
                    (typeof(ChoSirenApp).GetField(field, Private)?.GetValue(app) as GameObject)?.SetActive(true);
                var motion = Home.GetComponent<PsdHomeMotion>();
                motion.DisplayDialogue(0);
                yield return new WaitForSecondsRealtime(1.6f);
                Capture(camera, rt, Prefix + "-home.png");
                yield return new WaitForSecondsRealtime(.37f);
                Capture(camera, rt, Prefix + "-home-b.png");
                // A press flare on the top-right column (settings / mail / eye).
                foreach (string name in new[] { "Settings", "Mail", "HideUi" })
                {
                    Transform button = Home.transform.Find(name);
                    if (button == null) continue;
                    foreach (var behaviour in button.GetComponentsInChildren<MonoBehaviour>(true))
                        behaviour.GetType().GetMethod("Flare", BindingFlags.Instance | BindingFlags.Public)?.Invoke(behaviour, null);
                }
                yield return new WaitForSecondsRealtime(.18f);
                Capture(camera, rt, Prefix + "-press.png");
                motion.DisplayLilithLine();
                yield return new WaitForSecondsRealtime(.9f);
                Capture(camera, rt, Prefix + "-dialogue-long.png");
            }
            finally
            {
                Object.DestroyImmediate(camera.gameObject); rt.Release(); Object.DestroyImmediate(rt);
            }
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
