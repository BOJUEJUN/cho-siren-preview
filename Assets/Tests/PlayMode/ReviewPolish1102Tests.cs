using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using ChoSiren.Systems.Tactics;
using ChoSiren.Systems.Data;
using ChoSiren.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ChoSiren.Tests
{
    public sealed class ReviewPolish1102Tests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        string saved, legacy;
        bool had, hadLegacy, previousIgnore;
        ChoSirenApp app;
        static string Out => Path.GetFullPath("Builds/qa-logs/review-polish-1102");

        [UnitySetUp] public IEnumerator SetUp()
        {
            had = PlayerPrefs.HasKey(GameModel.SaveKey); saved = PlayerPrefs.GetString(GameModel.SaveKey);
            hadLegacy = PlayerPrefs.HasKey(GameModel.LegacySaveKey); legacy = PlayerPrefs.GetString(GameModel.LegacySaveKey);
            previousIgnore = LogAssert.ignoreFailingMessages; LogAssert.ignoreFailingMessages = true;
            foreach (var old in Object.FindObjectsByType<ChoSirenApp>(FindObjectsSortMode.None)) Object.Destroy(old.gameObject);
            foreach (var old in Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None)) Object.Destroy(old.gameObject);
            yield return null;
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            if (app != null) Object.Destroy(app.gameObject);
            foreach (var old in Object.FindObjectsByType<NativeTitleScreen>(FindObjectsSortMode.None)) Object.Destroy(old.gameObject);
            foreach (var old in Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None)) Object.Destroy(old.gameObject);
            yield return null;
            if (had) PlayerPrefs.SetString(GameModel.SaveKey, saved); else PlayerPrefs.DeleteKey(GameModel.SaveKey);
            if (hadLegacy) PlayerPrefs.SetString(GameModel.LegacySaveKey, legacy); else PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
            PlayerPrefs.Save(); LogAssert.ignoreFailingMessages = previousIgnore;
        }

        [UnityTest] public IEnumerator TeamNameBoxAcceptsClicksAfterLosingFocus()
        {
            app = new GameObject("ReviewTeamNameInput").AddComponent<ChoSirenApp>();
            yield return null;
            var model = new GameModel(() => DateTime.Now, new InMemorySaveStore());
            typeof(ChoSirenApp).GetField("model", Private).SetValue(app, model);
            typeof(ChoSirenApp).GetMethod("OpenTeamNameDialog1028", Private).Invoke(app, new object[] { 0 });
            yield return null;
            var input = app.GetComponentsInChildren<InputField>().First(f => f.name == "TeamNameInput");
            input.DeactivateInputField(); EventSystem.current.SetSelectedGameObject(null);
            yield return null;
            Assert.That(input.GetComponent<Image>().raycastTarget, Is.True);
            var rect = (RectTransform)input.transform;
            var point = RectTransformUtility.WorldToScreenPoint(null,
                rect.TransformPoint(new Vector2(rect.rect.xMax - 24, rect.rect.center.y)));
            var pointer = new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(input.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(input.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            yield return null;
            Assert.That(input.isFocused, Is.True);
            input.text = "夜场小队";
            app.GetComponentsInChildren<Button>().First(b => b.name == "Confirm").onClick.Invoke();
            Assert.That(model.ActiveTeamName, Is.EqualTo("夜场小队"));
        }

        [UnityTest] public IEnumerator TeamPencilAndPowerStayCloseToTheirContent()
        {
            app = new GameObject("ReviewPolishTeam").AddComponent<ChoSirenApp>();
            yield return null;
            var camera = NewCamera(out RenderTexture target);
            try
            {
                Canvas canvas = app.GetComponentInChildren<Canvas>(); ConfigureCanvas(canvas, camera);
                foreach (var fitter in canvas.GetComponentsInChildren<SafeAreaFitter>(true))
                {
                    fitter.enabled = false; var rect = (RectTransform)fitter.transform;
                    rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                    rect.offsetMin = rect.offsetMax = Vector2.zero;
                }
                yield return null;
                for (int digits = 4, multiplier = 1; digits <= 7; digits++, multiplier *= 10)
                {
                    var tables = JsonUtility.FromJson<TacticsManifest>(JsonUtility.ToJson(GameData.Repository.Tactics));
                    foreach (var unit in tables.Units)
                    {
                        unit.MaxHp *= multiplier; unit.Attack *= multiplier; unit.Defense *= multiplier;
                    }
                    var model = new GameModel(() => DateTime.Now, new InMemorySaveStore(), null, null, tables, null);
                    model.Save.TeamPresets[model.Save.ActiveTeamPreset].Name = "超神队";
                    foreach (int member in model.Save.Team) model.Save.MemberLevels[member] = 1;
                    typeof(ChoSirenApp).GetField("model", Private).SetValue(app, model);
                    typeof(ChoSirenApp).GetMethod("ShowScreenSafely", Private).Invoke(app, new object[] { "team" });
                    yield return null; yield return new WaitForSecondsRealtime(.4f); Canvas.ForceUpdateCanvases();
                    Transform root = app.GetComponentsInChildren<Transform>().First(t => t.name == "TeamReferenceBoard038");
                    Text title = root.Find("TeamTitle").GetComponent<Text>();
                    Assert.That(title.text, Is.EqualTo("超神队"));
                    RectTransform plate = root.Find("SirenTitlePlate-TeamTitle") as RectTransform;
                    RectTransform pencil = root.Find("RenameTeam/Pencil") as RectTransform;
                    float pencilGap = BoundsIn(pencil, root).xMin - BoundsIn(plate, root).xMax;
                    Assert.That(pencilGap, Is.InRange(0f, 22f), "pencil should sit beside the name plate");
                    Transform power = root.Find("TeamPowerReadout");
                    Text value = power.Find("TeamPowerValue").GetComponent<Text>();
                    RectTransform bolt = power.Find("PowerIcon") as RectTransform;
                    Assert.That(value.text.Replace(",", "").Length, Is.EqualTo(digits));
                    float powerGap = RenderedTextLeft(value, power) - BoundsIn(bolt, power).xMax;
                    Assert.That(powerGap, Is.InRange(-4f, 24f), "bolt should follow visible numbers, including long values");
                    Capture(camera, target, "team-" + digits + "digits.png");
                }
            }
            finally { Object.DestroyImmediate(camera.gameObject); target.Release(); Object.DestroyImmediate(target); }
        }

        [UnityTest] public IEnumerator MapShowsFourActualMembersAndAnimatedTeamSwitch()
        {
            var host = new GameObject("ReviewPolishMap", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var camera = NewCamera(out RenderTexture target);
            try
            {
                var model = new GameModel(() => DateTime.Now, new InMemorySaveStore()); model.Save.StoryProgress = 100;
                for (int i = 1; i <= 7; i++) model.Save.ClearedStages.Add(new StageClear { Id = "stage-1-" + i, Stars = 3 });
                ConfigureCanvas(host.GetComponent<Canvas>(), camera);
                var map = LevelMapPanel.Open(host.transform, model); yield return null;
                var scene = map.GetComponentInChildren<StageSceneArt1102>().GetComponent<RawImage>();
                float deadline = Time.realtimeSinceStartup + 3f;
                while (scene.texture == null && Time.realtimeSinceStartup < deadline) yield return null;
                yield return new WaitForSecondsRealtime(.4f);
                Transform strip = map.GetComponentsInChildren<Transform>().First(t => t.name == "StageTeamPreset");
                Assert.That(strip.Find("ActiveTeamName").GetComponent<Text>().enabled, Is.False);
                for (int slot = 0; slot < GameModel.TeamCapacity; slot++)
                {
                    Transform member = strip.Find("StageTeamMember-" + slot);
                    Assert.That(member.Find("MemberName").GetComponent<Text>().text,
                        Is.EqualTo(GameModel.Members[model.Save.Team[slot]].Name));
                    Assert.That(member.Find("AvatarWindow/Avatar").GetComponent<Image>().sprite, Is.Not.Null);
                }
                Capture(camera, target, "map-members.png");
                RectTransform glyph = strip.Find("StageTeamSwap/SwapGlyph/Glyph") as RectTransform;
                strip.Find("StageTeamSwap").GetComponent<Button>().onClick.Invoke();
                yield return new WaitForSecondsRealtime(.1f);
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(0, glyph.localEulerAngles.z)), Is.GreaterThan(10f));
                Capture(camera, target, "map-switch-motion.png");
                yield return new WaitForSecondsRealtime(.28f);
                Assert.That(map.GetComponentsInChildren<Transform>().Any(t => t.name == "StageTeamPresets"), Is.True);
                Capture(camera, target, "map-switch-picker.png");
            }
            finally { Object.DestroyImmediate(host); Object.DestroyImmediate(camera.gameObject); target.Release(); Object.DestroyImmediate(target); }
        }

        static Camera NewCamera(out RenderTexture target)
        {
            Camera camera = new GameObject("ReviewPolishCamera", typeof(Camera)).GetComponent<Camera>();
            target = new RenderTexture(858, 1834, 24); camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black; camera.orthographic = true; camera.targetTexture = target;
            return camera;
        }
        static void ConfigureCanvas(Canvas canvas, Camera camera)
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720, 1536); scaler.matchWidthOrHeight = 0;
        }
        static Rect BoundsIn(RectTransform rect, Transform parent)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            var local = corners.Select(parent.InverseTransformPoint).ToArray();
            return Rect.MinMaxRect(local.Min(p => p.x), local.Min(p => p.y), local.Max(p => p.x), local.Max(p => p.y));
        }
        static float RenderedTextLeft(Text text, Transform parent)
        {
            var vertices = text.cachedTextGenerator.verts; int count = Mathf.Max(0, vertices.Count - 4);
            Assert.That(count, Is.GreaterThan(0)); float x = float.MaxValue;
            for (int i = 0; i < count; i++) x = Mathf.Min(x, vertices[i].position.x / text.pixelsPerUnit);
            return parent.InverseTransformPoint(text.transform.TransformPoint(new Vector3(x, 0, 0))).x;
        }
        static void Capture(Camera camera, RenderTexture target, string name)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            Canvas.ForceUpdateCanvases(); camera.Render(); var previous = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
            Directory.CreateDirectory(Out); File.WriteAllBytes(Path.Combine(Out, name), image.EncodeToPNG());
            RenderTexture.active = previous; Object.DestroyImmediate(image);
        }
    }
}
