using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ChoSiren.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ChoSiren.Tests
{
    public sealed class AvatarPickerScroll1102Tests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        string saved, legacy;
        bool had, hadLegacy, previousIgnore;
        ChoSirenApp app;
        Camera camera;
        RenderTexture target;
        static string Out => Path.GetFullPath("Builds/qa-logs/avatar-scroll-1102");

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
            if (camera != null) Object.Destroy(camera.gameObject);
            if (target != null) { target.Release(); Object.Destroy(target); }
            foreach (var old in Object.FindObjectsByType<NativeTitleScreen>(FindObjectsSortMode.None)) Object.Destroy(old.gameObject);
            foreach (var old in Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None)) Object.Destroy(old.gameObject);
            yield return null;
            if (had) PlayerPrefs.SetString(GameModel.SaveKey, saved); else PlayerPrefs.DeleteKey(GameModel.SaveKey);
            if (hadLegacy) PlayerPrefs.SetString(GameModel.LegacySaveKey, legacy); else PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
            PlayerPrefs.Save(); LogAssert.ignoreFailingMessages = previousIgnore;
        }

        [UnityTest] public IEnumerator OverflowingFacesScrollInsideFrameAndBottomFaceCanBePicked()
        {
            app = new GameObject("AvatarScrollReview").AddComponent<ChoSirenApp>();
            yield return null;
            var model = new GameModel(() => DateTime.Now, new InMemorySaveStore());
            model.Save.StoryProgress = 100;
            model.Save.UnlockedMembers = Enumerable.Range(0, Mathf.Min(24, GameModel.Members.Length)).ToList();
            typeof(ChoSirenApp).GetField("model", Private).SetValue(app, model);

            camera = new GameObject("AvatarScrollCamera", typeof(Camera)).GetComponent<Camera>();
            target = new RenderTexture(858, 1834, 24);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            camera.orthographic = true; camera.targetTexture = target;
            Canvas canvas = app.GetComponentInChildren<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720, 1536); scaler.matchWidthOrHeight = 0;
            foreach (var fitter in canvas.GetComponentsInChildren<SafeAreaFitter>(true))
            {
                fitter.enabled = false;
                var safe = (RectTransform)fitter.transform;
                safe.anchorMin = Vector2.zero; safe.anchorMax = Vector2.one;
                safe.offsetMin = safe.offsetMax = Vector2.zero;
            }
            yield return null;
            Invoke("OpenAvatarPicker1101"); yield return null;
            Canvas.ForceUpdateCanvases();
            Transform sheet = app.GetComponentsInChildren<Transform>().Single(t => t.name == "AvatarPicker1101");
            ScrollRect scroll = sheet.GetComponentInChildren<ScrollRect>();
            Assert.That(scroll, Is.Not.Null);
            Assert.That(scroll.horizontal, Is.False); Assert.That(scroll.vertical, Is.True);
            Assert.That(scroll.movementType, Is.EqualTo(ScrollRect.MovementType.Clamped));
            Assert.That(scroll.inertia, Is.True);
            Assert.That(scroll.viewport.GetComponent<RectMask2D>(), Is.Not.Null);
            Assert.That(scroll.viewport.GetComponent<Image>().raycastTarget, Is.True);
            Assert.That(scroll.content.parent, Is.SameAs(scroll.viewport));
            Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height + 50f));
            var tiles = sheet.GetComponentsInChildren<Button>(true).Where(b => b.name.StartsWith("Pick-"))
                .OrderBy(b => int.Parse(b.name.Substring(5))).ToList();
            Button last = tiles.Last();
            RectTransform lastRect = last.GetComponent<RectTransform>();
            Assert.That(BoundsIn(lastRect, scroll.viewport).yMax, Is.LessThan(scroll.viewport.rect.yMin),
                "fixture must place the last avatar below the clipped viewport");
            Button close = sheet.GetComponentsInChildren<Button>().Single(b => b.name == "Close");
            Vector3 closeBefore = close.transform.position;
            Vector3 titleBefore = sheet.GetComponentsInChildren<Text>().First(t => t.name == "Title").transform.position;
            Capture("top.png");

            GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
            int beforePreset = model.PlayerAvatar; string beforeMember = model.PlayerAvatarMemberId;
            for (int gesture = 0; gesture < 8 && scroll.verticalNormalizedPosition > .002f; gesture++)
            {
                Vector2 start = RectTransformUtility.WorldToScreenPoint(camera,
                    scroll.viewport.TransformPoint(scroll.viewport.rect.center));
                var pointer = new PointerEventData(EventSystem.current)
                {
                    position = start, pressPosition = start, button = PointerEventData.InputButton.Left,
                    pointerPressRaycast = new RaycastResult { module = raycaster },
                    pointerDrag = scroll.gameObject,
                };
                ExecuteEvents.Execute(scroll.gameObject, pointer, ExecuteEvents.initializePotentialDrag);
                ExecuteEvents.Execute(scroll.gameObject, pointer, ExecuteEvents.beginDragHandler);
                pointer.position = start + Vector2.up * scroll.viewport.rect.height * canvas.scaleFactor * .8f;
                ExecuteEvents.Execute(scroll.gameObject, pointer, ExecuteEvents.dragHandler);
                tiles[1].onClick.Invoke();
                Assert.That(model.PlayerAvatar, Is.EqualTo(beforePreset), "dragging must not pick the face under the finger");
                Assert.That(model.PlayerAvatarMemberId, Is.EqualTo(beforeMember));
                ExecuteEvents.Execute(scroll.gameObject, pointer, ExecuteEvents.endDragHandler);
                tiles[1].onClick.Invoke();
                Assert.That(model.PlayerAvatar, Is.EqualTo(beforePreset), "the drag release must not become a tap");
                scroll.StopMovement(); yield return null;
            }
            Assert.That(scroll.verticalNormalizedPosition, Is.LessThan(.002f), "real drag handlers must reach the bottom");
            Canvas.ForceUpdateCanvases();
            Rect visibleLast = BoundsIn(lastRect, scroll.viewport);
            Assert.That(visibleLast.yMin, Is.GreaterThanOrEqualTo(scroll.viewport.rect.yMin - 1f));
            Assert.That(visibleLast.yMax, Is.LessThanOrEqualTo(scroll.viewport.rect.yMax + 1f));
            Assert.That(close.transform.position, Is.EqualTo(closeBefore), "close stays fixed while faces scroll");
            Assert.That(sheet.GetComponentsInChildren<Text>().First(t => t.name == "Title").transform.position,
                Is.EqualTo(titleBefore), "title stays fixed while faces scroll");
            Capture("bottom.png");
            yield return new WaitForSecondsRealtime(.14f);
            var tap = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(camera, lastRect.TransformPoint(lastRect.rect.center)),
                button = PointerEventData.InputButton.Left,
            };
            var hits = new List<RaycastResult>(); raycaster.Raycast(tap, hits);
            Assert.That(hits, Is.Not.Empty);
            Assert.That(hits[0].gameObject.GetComponentInParent<Button>(), Is.EqualTo(last), "bottom avatar is reachable by the actual pointer raycast");
            Sprite selected = last.GetComponentsInChildren<Image>().First(i => i.name == "Face").sprite;
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, tap, ExecuteEvents.pointerClickHandler);
            yield return null;
            Assert.That(app.GetComponentsInChildren<Transform>().Any(t => t.name == "AvatarPicker1101"), Is.False);
            Assert.That((Sprite)Invoke("PlayerAvatarSpriteNow"), Is.SameAs(selected));

            Invoke("OpenProfile"); yield return null;
            Transform profile = app.GetComponentsInChildren<Transform>().Single(t => t.name == "ProfileModal");
            Image profileFace = profile.GetComponentsInChildren<Image>().Single(i => i.name == "Avatar");
            Assert.That(profileFace.sprite, Is.SameAs(selected));
            Invoke("OpenAvatarPicker1101"); yield return null;
            sheet = app.GetComponentsInChildren<Transform>().Single(t => t.name == "AvatarPicker1101");
            sheet.GetComponentsInChildren<Button>().Single(b => b.name == "Pick-1").onClick.Invoke();
            Assert.That(profileFace.sprite, Is.SameAs((Sprite)Invoke("PlayerAvatarSpriteNow")),
                "the open profile updates in the same click as the lobby header");
            yield return null;
            Invoke("CloseModal"); yield return null;

            Transform safeRoot = (Transform)typeof(ChoSirenApp).GetField("safeRoot", Private).GetValue(app);
            int picked = -1;
            Invoke("OpenAvatarSheet1028", safeRoot, 0, (Action<int>)(choice => picked = choice));
            yield return null;
            Transform contract = app.GetComponentsInChildren<Transform>().Single(t => t.name == "AvatarSheet1028");
            Assert.That(contract.GetComponentInChildren<ScrollRect>(), Is.Not.Null, "contract avatar sheet uses the same clipping");
            Assert.That(contract.GetComponentsInChildren<Button>().Any(b => b.name == "Close"), Is.True);
            var contractLast = contract.GetComponentsInChildren<Button>().Where(b => b.name.StartsWith("Avatar-"))
                .OrderBy(b => int.Parse(b.name.Substring(7))).Last();
            contractLast.onClick.Invoke(); yield return null;
            Assert.That(picked, Is.EqualTo(GameModel.PlayerAvatarResources.Length - 1));
        }

        object Invoke(string name, params object[] args) => typeof(ChoSirenApp).GetMethods(Private)
            .First(m => m.Name == name && m.GetParameters().Length == args.Length).Invoke(app, args);

        static Rect BoundsIn(RectTransform rect, Transform parent)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            var points = corners.Select(parent.InverseTransformPoint).ToArray();
            return Rect.MinMaxRect(points.Min(p => p.x), points.Min(p => p.y), points.Max(p => p.x), points.Max(p => p.y));
        }
        void Capture(string name)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            Canvas.ForceUpdateCanvases(); camera.Render();
            var before = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply(); RenderTexture.active = before;
            Directory.CreateDirectory(Out); File.WriteAllBytes(Path.Combine(Out, name), image.EncodeToPNG());
            Object.DestroyImmediate(image);
        }
    }
}
