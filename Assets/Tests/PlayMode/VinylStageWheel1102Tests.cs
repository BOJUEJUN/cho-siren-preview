using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
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
    public sealed class VinylStageWheel1102Tests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        sealed class Fixture : IDisposable
        {
            public readonly GameObject Host, Events;
            public readonly Camera Camera;
            public readonly RenderTexture Target;
            public readonly GameModel Model;
            public readonly LevelMapPanel Map;
            readonly int previousChapter;
            public Fixture(int width, int height)
            {
                Host = new GameObject("VinylWheelCapture", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                Events = new GameObject("VinylWheelEvents", typeof(EventSystem));
                Camera = new GameObject("VinylWheelCamera", typeof(Camera)).GetComponent<Camera>();
                Target = new RenderTexture(width, height, 24);
                Camera.clearFlags = CameraClearFlags.SolidColor; Camera.backgroundColor = Color.black;
                Camera.orthographic = true; Camera.targetTexture = Target;
                Canvas canvas = Host.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = Camera; canvas.planeDistance = 1;
                CanvasScaler scaler = Host.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(720, 1536); scaler.matchWidthOrHeight = 0;
                // The map derives its initial stage from story progress, not clear-record count.
                // InMemorySaveStore isolates both current and legacy saves from PlayerPrefs.
                Model = new GameModel(() => DateTime.Now, new InMemorySaveStore());
                Model.Save.StoryProgress = GameModel.StoryStageThresholds[6];
                Model.Save.ClearedStages.Clear();
                for (int i = 1; i <= 7; i++) Model.Save.ClearedStages.Add(new StageClear { Id = "stage-1-" + i, Stars = 3 });
                FieldInfo chapter = typeof(LevelMapPanel).GetField("lastChapter", BindingFlags.Static | BindingFlags.NonPublic);
                previousChapter = (int)chapter.GetValue(null); chapter.SetValue(null, 1);
                Canvas.ForceUpdateCanvases(); Map = LevelMapPanel.Open(Host.transform, Model);
            }
            public T Find<T>(string name) where T : Component => Map.GetComponentsInChildren<T>(true).First(c => c.name == name);
            public void Click(string name)
            {
                Button button = Find<Button>(name);
                Assert.That(button.gameObject.activeInHierarchy && button.IsInteractable(), Is.True, name + " must be usable");
                ExecuteEvents.Execute(button.gameObject, new PointerEventData(Events.GetComponent<EventSystem>())
                    { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
            }
            public void Dispose()
            {
                Object.DestroyImmediate(Host); Object.DestroyImmediate(Events); Object.DestroyImmediate(Camera.gameObject);
                Target.Release(); Object.DestroyImmediate(Target);
                typeof(LevelMapPanel).GetField("lastChapter", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, previousChapter);
            }
        }

        [UnityTest]
        public IEnumerator ActualClicksAndWheelDragSnapToTheSelectedStage()
        {
            using (var f = new Fixture(858, 1834))
            {
                yield return new WaitForSecondsRealtime(.4f);
                var wheel = f.Find<VinylStageWheel1102>("VinylWheelInput1102");
                Assert.That(wheel.SelectedStage, Is.EqualTo(8));
                AssertVisibleNodes(f);
                f.Click("Level-1-7");
                yield return new WaitForSecondsRealtime(.5f);
                Assert.That(wheel.SelectedStage, Is.EqualTo(7));
                Assert.That(Selected(f.Map), Is.EqualTo(7));
                float before = wheel.Angle;
                RectTransform disc = f.Find<Image>("HalfVinyl1102").rectTransform;
                Vector2 centre = RectTransformUtility.WorldToScreenPoint(f.Camera, disc.TransformPoint(disc.rect.center));
                Vector2 from = centre + Vector2.left * 180f;
                var pointer = new PointerEventData(f.Events.GetComponent<EventSystem>())
                {
                    pressPosition = from, position = from, button = PointerEventData.InputButton.Left,
                    pointerPressRaycast = new RaycastResult { module = f.Host.GetComponent<GraphicRaycaster>() }
                };
                ExecuteEvents.Execute(wheel.gameObject, pointer, ExecuteEvents.pointerDownHandler);
                pointer.position = from + Vector2.up * 16f;
                ExecuteEvents.Execute(wheel.gameObject, pointer, ExecuteEvents.beginDragHandler);
                pointer.position = from + Vector2.up * 200f; pointer.delta = Vector2.up * 184f;
                ExecuteEvents.Execute(wheel.gameObject, pointer, ExecuteEvents.dragHandler);
                Assert.That(wheel.IsDragging, Is.True);
                Assert.That(Mathf.Abs(wheel.Angle - before), Is.GreaterThan(3f), "拨动时光碟本身应转动。");
                Capture(f, "wheel-drag-mid.png");
                ExecuteEvents.Execute(wheel.gameObject, pointer, ExecuteEvents.endDragHandler);
                yield return new WaitForSecondsRealtime(.65f);
                Assert.That(wheel.IsDragging, Is.False);
                Assert.That(wheel.SelectedStage, Is.Not.EqualTo(7), "松手吸附应选中另一已解锁关卡。");
                Assert.That(wheel.SelectedStage, Is.InRange(1, 8));
                Assert.That(Selected(f.Map), Is.EqualTo(wheel.SelectedStage));
                Assert.That(f.Find<Text>("ChapterSwitchLabel").text, Does.Contain("01"));
                AssertVisibleNodes(f); Capture(f, "wheel-snapped.png");
            }
        }

        [UnityTest]
        public IEnumerator PhoneAndPcKeepTheDiscFlushAndLargeTeamCardsClearOfActions()
        {
            foreach (var size in new[] { new Vector2Int(858, 1834), new Vector2Int(1080, 2160) })
            using (var f = new Fixture(size.x, size.y))
            {
                yield return new WaitForSecondsRealtime(.4f); Canvas.ForceUpdateCanvases();
                RectTransform map = (RectTransform)f.Map.transform;
                RectTransform disc = f.Find<Image>("HalfVinyl1102").rectTransform;
                Assert.That(disc.rect.width, Is.EqualTo(disc.rect.height).Within(.01f));
                Assert.That(Mathf.Abs(disc.lossyScale.x), Is.EqualTo(Mathf.Abs(disc.lossyScale.y)).Within(.001f));
                float centreX = map.InverseTransformPoint(disc.TransformPoint(disc.rect.center)).x;
                Assert.That(centreX, Is.EqualTo(map.rect.xMax).Within(1.5f), "圆心应贴真实视口右缘，不能在居中page右缘提前裁切。");
                Assert.That(map.GetComponent<RectMask2D>(), Is.Not.Null);
                Rect selected = Bounds(f.Find<RectTransform>("SelectedLevel"), map);
                Transform strip = f.Find<RectTransform>("StageTeamPreset");
                Assert.That(((RectTransform)strip).rect.width, Is.EqualTo(490f).Within(.01f));
                Assert.That(Bounds((RectTransform)strip, map).width, Is.EqualTo(selected.width).Within(1f),
                    "头像框宽度与关卡详情卡一致；换队独立在右侧空白。 ");
                for (int i = 0; i < GameModel.TeamCapacity; i++)
                {
                    Transform member = strip.Find("StageTeamMember-" + i);
                    var avatar = member.Find("AvatarWindow") as RectTransform;
                    var label = member.Find("MemberName").GetComponent<Text>();
                    Assert.That(avatar.rect.width, Is.GreaterThanOrEqualTo(80f));
                    Assert.That(avatar.rect.height, Is.EqualTo(avatar.rect.width).Within(.01f));
                    Assert.That(label.text, Is.EqualTo(GameModel.Members[f.Model.Save.Team[i]].Name));
                    Assert.That(Bounds(avatar, map).yMin, Is.GreaterThan(selected.yMax));
                    Assert.That(Bounds(label.rectTransform, map).yMin, Is.GreaterThanOrEqualTo(selected.yMax));
                }
                AssertVisibleNodes(f); Capture(f, "wheel-layout-" + size.x + "x" + size.y + ".png");
                Assert.That(f.Model.StarsOf("stage-1-7"), Is.EqualTo(3));
                Assert.That(f.Model.CanSweep("stage-1-7"), Is.True);
                f.Click("Level-1-7"); yield return new WaitForSecondsRealtime(.4f);
                Assert.That(f.Map.SelectedStage, Is.EqualTo(7), "应先实际选中已三星通关的1-7，再操作扫荡。");
                int stamina = f.Model.Save.Stamina;
                f.Click("SweepOnce");
                Assert.That(f.Model.Save.Stamina, Is.LessThan(stamina), "三星关卡仍能从可见扫荡按钮实际扣体力并结算。");
                f.Click("ChapterRewards"); yield return null;
                Assert.That(f.Map.GetComponentsInChildren<Transform>().Any(t => t.name == "ChapterRewardsModal"), Is.True);
                f.Click("CloseChapterModal"); yield return null;
                f.Click("ChapterTasks"); yield return null;
                Assert.That(f.Map.GetComponentsInChildren<Transform>().Any(t => t.name == "ChapterTasksModal"), Is.True);
                Capture(f, "wheel-tasks-" + size.x + ".png");
                f.Click("CloseChapterModal"); yield return null;
                f.Click("ChapterSwitch"); yield return null;
                Assert.That(f.Find<RectTransform>("ChapterPicker1102"), Is.Not.Null);
                Button current = f.Find<Button>("ChooseChapter-1"), locked = f.Find<Button>("ChooseChapter-2");
                Assert.That(current.GetComponentsInChildren<Text>().Any(t => t.text.Contains("当前")), Is.True);
                Assert.That(locked.interactable, Is.False);
                int stageBefore = f.Map.SelectedStage;
                ExecuteEvents.Execute(locked.gameObject, new PointerEventData(f.Events.GetComponent<EventSystem>())
                    { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
                Assert.That(f.Map.SelectedStage, Is.EqualTo(stageBefore));
                Assert.That(f.Find<RectTransform>("ChapterPicker1102").gameObject.activeInHierarchy, Is.True,
                    "锁定章节点击不能关闭列表或穿透到关卡。 ");
            }
        }

        static int Selected(LevelMapPanel map) => (int)typeof(LevelMapPanel).GetField("selectedStage", Private).GetValue(map);
        static void AssertVisibleNodes(Fixture f)
        {
            int visible = f.Map.GetComponentsInChildren<Button>().Count(b => b.name.StartsWith("Level-", StringComparison.Ordinal)
                && b.GetComponent<CanvasGroup>() != null && b.GetComponent<CanvasGroup>().alpha > .5f);
            Assert.That(visible, Is.InRange(3, 5), "只保留选中关卡附近3–5节点。");
        }
        static Rect Bounds(RectTransform rect, Transform root)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            var points = corners.Select(root.InverseTransformPoint).ToArray();
            return Rect.MinMaxRect(points.Min(p => p.x), points.Min(p => p.y), points.Max(p => p.x), points.Max(p => p.y));
        }
        static void Capture(Fixture f, string name)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            Canvas.ForceUpdateCanvases(); f.Camera.Render(); var previous = RenderTexture.active; RenderTexture.active = f.Target;
            var pixels = new Texture2D(f.Target.width, f.Target.height, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, f.Target.width, f.Target.height), 0, 0); pixels.Apply();
            string path = Path.GetFullPath("Builds/qa-logs/review-polish-1102/" + name);
            Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllBytes(path, pixels.EncodeToPNG());
            RenderTexture.active = previous; Object.DestroyImmediate(pixels);
        }
    }
}
