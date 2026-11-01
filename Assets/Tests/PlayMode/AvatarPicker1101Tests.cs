using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ChoSiren.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ChoSiren.Tests
{
    /// <summary>
    /// 2026-11-01 (赵金: "这个头像要支持点击头像换头像"): the top-bar portrait opens the avatar sheet on its own
    /// (the rest of the plate still opens the profile), picking a face applies at once, and the sheet is
    /// captured at the Mac window and phone ratios into Builds/qa-logs/zj-1101c/avatar-face/.
    /// </summary>
    public sealed class AvatarPicker1101Tests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        private static readonly (string Name, int Width, int Height)[] Devices =
        {
            ("mac-1080x2160", 1080, 2160),
            ("phone-1080x2288", 1080, 2288),
        };

        public static IEnumerable<string> DeviceNames => Devices.Select(d => d.Name);

        private static string Out => Path.GetFullPath(Path.Combine(Application.dataPath,
            "../Builds/qa-logs/zj-1101c/avatar-face"));

        private string save;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            save = PlayerPrefs.GetString(GameModel.SaveKey, string.Empty);
            LogAssert.ignoreFailingMessages = true;
            foreach (ChoSirenApp old in Object.FindObjectsByType<ChoSirenApp>(FindObjectsSortMode.None))
                Object.Destroy(old.gameObject);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (ChoSirenApp old in Object.FindObjectsByType<ChoSirenApp>(FindObjectsSortMode.None))
                Object.Destroy(old.gameObject);
            yield return null;
            if (save.Length > 0) PlayerPrefs.SetString(GameModel.SaveKey, save); else PlayerPrefs.DeleteKey(GameModel.SaveKey);
            PlayerPrefs.Save();
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator TopBarPortraitOpensThePickerAndWearsThePick([ValueSource(nameof(DeviceNames))] string deviceName)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("Needs a graphics device to render the captures.");
            var device = Devices.First(d => d.Name == deviceName);
            Directory.CreateDirectory(Out);

            var saveData = new GameSave { SchemaVersion = GameModel.SaveSchemaVersion, PlayerName = "经纪人" };
            saveData.UnlockedMembers.Add(4); // 瑶光: an owned member with no preset portrait, offered as her own face.
            PlayerPrefs.SetString(GameModel.SaveKey, JsonUtility.ToJson(saveData));
            PlayerPrefs.SetInt("cho-agreement-v1", 1);
            yield return null;

            var app = new GameObject("AvatarPicker1101").AddComponent<ChoSirenApp>();
            var camera = new GameObject("AvatarPicker1101Camera", typeof(Camera)).GetComponent<Camera>();
            camera.transform.SetParent(app.transform, false);
            var rt = new RenderTexture(device.Width, device.Height, 24);
            try
            {
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.orthographic = true; camera.targetTexture = rt;
                Canvas canvas = app.GetComponentInChildren<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10;
                SafeAreaFitter.Layout(new Rect(0, 0, device.Width, device.Height), new Vector2(device.Width, device.Height),
                    out Vector2 min, out Vector2 max, out bool wide);
                foreach (var fitter in canvas.GetComponentsInChildren<SafeAreaFitter>(true))
                {
                    fitter.enabled = false;
                    var safe = (RectTransform)fitter.transform;
                    safe.anchorMin = min; safe.anchorMax = max;
                    safe.offsetMin = safe.offsetMax = Vector2.zero;
                }
                canvas.GetComponent<CanvasScaler>().matchWidthOrHeight = wide ? 1f : 0f;
                yield return null;
                Canvas.ForceUpdateCanvases();
                Invoke(app, "ShowScreenSafely", "lobby");
                yield return null;
                Invoke(app, "CloseModal");
                yield return null;
                Canvas.ForceUpdateCanvases();

                Transform safeRoot = (Transform)typeof(ChoSirenApp).GetField("safeRoot", Private).GetValue(app);
                var hit = safeRoot.Find("TopBar/AvatarHit");
                Assert.That(hit, Is.Not.Null, "The top bar needs its own portrait hit area.");
                hit.GetComponent<Button>().onClick.Invoke();
                yield return null;
                Canvas.ForceUpdateCanvases();

                GameObject sheet = safeRoot.Find("AvatarPicker1101")?.gameObject;
                Assert.That(sheet, Is.Not.Null, "Tapping the portrait opens the avatar sheet.");
                var tiles = sheet.GetComponentsInChildren<Button>(true).Where(b => b.name.StartsWith("Pick-")).ToList();
                Assert.That(tiles.Count, Is.GreaterThanOrEqualTo(GameModel.PlayerAvatarResources.Length),
                    "Every preset is offered as a face.");

                float until = Time.realtimeSinceStartup + 1.2f;
                while (Time.realtimeSinceStartup < until) yield return null;
                Canvas.ForceUpdateCanvases();
                CameraCapture(camera, rt, Path.Combine(Out, device.Name + "-avatar-picker.png"));

                // The last tile is an owned member's face: wearing it must stick and repaint the top bar.
                var model = (GameModel)typeof(ChoSirenApp).GetField("model", Private).GetValue(app);
                tiles[tiles.Count - 1].onClick.Invoke();
                yield return null;
                string worn = model.Save.PlayerAvatarMember;
                Assert.That(string.IsNullOrEmpty(worn), Is.False, "Picking a member face wears it.");
                Image face = safeRoot.Find("TopBar/Profile/AvatarMask/Avatar")?.GetComponent<Image>();
                Assert.That(face, Is.Not.Null);
                Assert.That(face.sprite, Is.Not.Null);
                Assert.That(face.sprite.name, Does.Contain("@Head"), "The top bar shows the face-cropped portrait.");
                var reloaded = new GameModel(() => DateTime.Now, new PlayerPrefsSaveStore());
                Assert.That(reloaded.Save.PlayerAvatarMember, Is.EqualTo(worn), "The worn face survives a reload.");

                // The profile page portrait opens the same sheet; 改名 stays on the pencil.
                Invoke(app, "OpenProfile");
                yield return null;
                Canvas.ForceUpdateCanvases();
                Transform profile = safeRoot.Find("ProfileModal");
                Assert.That(profile, Is.Not.Null);
                Button profileAvatar = profile.GetComponentsInChildren<Button>(true).First(b => b.name == "ProfileAvatarFrame");
                profileAvatar.onClick.Invoke();
                yield return null;
                Assert.That(safeRoot.Find("AvatarPicker1101"), Is.Not.Null, "The profile portrait opens the picker too.");
                Assert.That(safeRoot.Find("PlayerContract1028"), Is.Null, "Changing the face no longer opens 改名.");
                CameraCapture(camera, rt, Path.Combine(Out, device.Name + "-avatar-picker-from-profile.png"));
            }
            finally
            {
                camera.targetTexture = null;
                rt.Release(); Object.DestroyImmediate(rt);
            }
        }

        private static void Invoke(object target, string method, params object[] args)
        {
            MethodInfo info = target.GetType().GetMethods(Private)
                .FirstOrDefault(m => m.Name == method && m.GetParameters().Length == args.Length);
            if (info == null) throw new MissingMethodException(target.GetType().Name, method);
            info.Invoke(target, args);
        }

        private static void CameraCapture(Camera camera, RenderTexture rt, string path)
        {
            camera.Render();
            RenderTexture previous = RenderTexture.active; RenderTexture.active = rt;
            var texture = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }
    }
}
