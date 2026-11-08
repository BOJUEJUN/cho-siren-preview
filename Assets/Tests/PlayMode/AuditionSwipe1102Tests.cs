using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ChoSiren.Panels;
using ChoSiren.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ChoSiren.Tests
{
    public sealed class AuditionSwipe1102Tests
    {
        [UnityTest] public IEnumerator CandidateFollowsSwipeSpringsBackAndKeepsButtonsReachable()
        {
            var host = new GameObject("AuditionSwipeCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            var camera = new GameObject("AuditionSwipeCamera", typeof(Camera)).GetComponent<Camera>();
            var target = new RenderTexture(858, 1834, 24);
            GameObject events = null;
            bool previousIgnore = LogAssert.ignoreFailingMessages; LogAssert.ignoreFailingMessages = true;
            try
            {
                if (EventSystem.current == null) events = new GameObject("AuditionSwipeEvents", typeof(EventSystem));
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.orthographic = true; camera.targetTexture = target;
                Canvas canvas = host.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera; canvas.planeDistance = 1;
                CanvasScaler scaler = host.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(720, 1536); scaler.matchWidthOrHeight = 0;
                var content = new GameObject("EmbeddedContent", typeof(RectTransform)); content.transform.SetParent(host.transform, false);
                RectTransform contentRect = content.GetComponent<RectTransform>();
                contentRect.anchorMin = contentRect.anchorMax = contentRect.pivot = new Vector2(0, 1);
                contentRect.anchoredPosition = new Vector2(0, -104); contentRect.sizeDelta = new Vector2(720, 1290);
                var model = new GameModel(() => new DateTime(2026, 11, 2, 12, 0, 0), new InMemorySaveStore());
                model.Save.Diamonds = 10000;
                var panel = GachaPanel.OpenEmbedded(content.transform, model, model);
                yield return null; Canvas.ForceUpdateCanvases();
                GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
                InterviewBadgeSwipe1108 swipe = panel.GetComponentInChildren<InterviewBadgeSwipe1108>();
                Assert.That(swipe, Is.Not.Null);
                var surface = (RectTransform)swipe.transform;
                RectTransform portrait = Find(panel, "CandidatePortrait") as RectTransform;
                RectTransform card = Find(panel, "CandidateCard") as RectTransform;
                Vector2 portraitHome = portrait.anchoredPosition, cardHome = card.anchoredPosition;
                int initial = panel.InterviewCandidateIndex;
                int candidateCount = model.InterviewCandidates(0).Count;
                Assert.That(candidateCount, Is.GreaterThan(1));
                int owned = model.Save.UnlockedMembers.Count;

                PointerEventData Begin(Vector2 delta)
                {
                    Vector2 start = RectTransformUtility.WorldToScreenPoint(camera, surface.TransformPoint(surface.rect.center));
                    var pointer = new PointerEventData(EventSystem.current)
                    {
                        pressPosition = start, position = start,
                        button = PointerEventData.InputButton.Left,
                        pointerPressRaycast = new RaycastResult { module = raycaster },
                    };
                    var hits = new List<RaycastResult>(); raycaster.Raycast(pointer, hits);
                    Assert.That(hits, Is.Not.Empty);
                    Assert.That(hits[0].gameObject.GetComponentInParent<InterviewBadgeSwipe1108>(), Is.SameAs(swipe),
                        "the actual candidate region must receive the swipe");
                    ExecuteEvents.Execute(swipe.gameObject, pointer, ExecuteEvents.initializePotentialDrag);
                    pointer.position = RectTransformUtility.WorldToScreenPoint(camera,
                        surface.TransformPoint(surface.rect.center + delta));
                    ExecuteEvents.Execute(swipe.gameObject, pointer, ExecuteEvents.beginDragHandler);
                    ExecuteEvents.Execute(swipe.gameObject, pointer, ExecuteEvents.dragHandler);
                    return pointer;
                }
                void End(PointerEventData pointer) => ExecuteEvents.Execute(swipe.gameObject, pointer, ExecuteEvents.endDragHandler);

                var shortDrag = Begin(new Vector2(36f, 0));
                Assert.That(swipe.IsDragging, Is.True);
                Assert.That(portrait.anchoredPosition.x - portraitHome.x, Is.EqualTo(36f).Within(1f));
                // 2026-11-08: the badge swings on its clip; the cards behind drift a sixth as far.
                Assert.That(card.anchoredPosition.x - cardHome.x, Is.EqualTo(36f * .16f).Within(1f));
                Assert.That(Mathf.DeltaAngle(0f, portrait.localEulerAngles.z), Is.LessThan(-.5f), "dragging right tilts the badge");
                Assert.That(panel.InterviewCandidateIndex, Is.EqualTo(initial), "candidate must follow the pointer before release, not jump immediately");
                Capture(camera, target, "following.png");
                yield return new WaitForSecondsRealtime(.08f); End(shortDrag);
                yield return new WaitForSecondsRealtime(.4f);
                Assert.That(swipe.Offset, Is.EqualTo(0f).Within(.01f));
                Assert.That(panel.InterviewCandidateIndex, Is.EqualTo(initial), "a short drag springs back");

                var vertical = Begin(new Vector2(10f, 150f)); End(vertical);
                yield return null;
                Assert.That(swipe.IsDragging, Is.False);
                Assert.That(swipe.Offset, Is.EqualTo(0f).Within(.01f));
                Assert.That(panel.InterviewCandidateIndex, Is.EqualTo(initial), "vertical gestures must not browse candidates");

                var left = Begin(new Vector2(-140f, 0)); End(left);
                yield return new WaitForSecondsRealtime(.3f);
                Assert.That(panel.InterviewCandidateIndex, Is.EqualTo((initial + 1) % candidateCount), "left swipe uses the same next-candidate action");
                Assert.That(model.Save.UnlockedMembers.Count, Is.EqualTo(owned), "a swipe never signs a candidate");
                Assert.That(panel.GetComponentsInChildren<Transform>().Any(t => t.name == "SignConfirmOverlay"), Is.False);
                yield return new WaitForSecondsRealtime(.15f);
                Canvas.ForceUpdateCanvases(); Capture(camera, target, "badge-entering.png");
                yield return new WaitForSecondsRealtime(.6f);
                Canvas.ForceUpdateCanvases(); Capture(camera, target, "next-candidate.png");
                foreach (string name in new[] { "PreviousCandidate", "NextCandidate", "InterviewPool-0", "InterviewPool-1", "SignCandidate" })
                    AssertReachable(Find(panel, name).GetComponent<Button>(), raycaster, camera);
                Button next = Find(panel, "NextCandidate").GetComponent<Button>();
                Click(next, raycaster, camera); yield return null;
                Assert.That(panel.InterviewCandidateIndex, Is.EqualTo((initial + 2) % candidateCount), "existing navigation button remains functional");
                Click(Find(panel, "InterviewPool-1").GetComponent<Button>(), raycaster, camera); yield return null;
                Assert.That(panel.InterviewPoolIndex, Is.EqualTo(1), "channel button remains independent of the swipe surface");
            }
            finally
            {
                Object.DestroyImmediate(host); Object.DestroyImmediate(camera.gameObject);
                target.Release(); Object.DestroyImmediate(target);
                if (events != null) Object.DestroyImmediate(events);
                LogAssert.ignoreFailingMessages = previousIgnore;
            }
        }
        /// <summary>2026-11-08: every candidate's photo fills the badge window (no empty corner) and is captured for review.</summary>
        [UnityTest] public IEnumerator EveryCandidatePhotoFillsTheBadgeWindow()
        {
            var host = new GameObject("BadgeCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var camera = new GameObject("BadgeCamera", typeof(Camera)).GetComponent<Camera>();
            var target = new RenderTexture(1080, 2288, 24);
            bool previousIgnore = LogAssert.ignoreFailingMessages; LogAssert.ignoreFailingMessages = true;
            try
            {
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.orthographic = true; camera.targetTexture = target;
                Canvas canvas = host.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera; canvas.planeDistance = 1;
                CanvasScaler scaler = host.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(720, 1536); scaler.matchWidthOrHeight = 0;
                var content = new GameObject("EmbeddedContent", typeof(RectTransform)); content.transform.SetParent(host.transform, false);
                RectTransform contentRect = content.GetComponent<RectTransform>();
                contentRect.anchorMin = contentRect.anchorMax = contentRect.pivot = new Vector2(0, 1);
                contentRect.anchoredPosition = new Vector2(0, -104); contentRect.sizeDelta = new Vector2(720, 1290);
                var model = new GameModel(() => new DateTime(2026, 11, 8, 12, 0, 0), new InMemorySaveStore());
                var panel = GachaPanel.OpenEmbedded(content.transform, model, model);
                yield return null;
                for (int pool = 0; pool < 2; pool++)
                {
                    if (pool == 1) { Find(panel, "InterviewPool-1").GetComponent<Button>().onClick.Invoke(); yield return null; }
                    int count = model.InterviewCandidates(pool).Count;
                    for (int i = 0; i < count; i++)
                    {
                        yield return new WaitForSecondsRealtime(.75f);
                        Canvas.ForceUpdateCanvases();
                        var photo = (RectTransform)Find(panel, "CandidatePhoto");
                        var art = (RectTransform)Find(panel, "CandidatePortraitArt");
                        Vector3[] p = new Vector3[4], a = new Vector3[4];
                        photo.GetWorldCorners(p); art.GetWorldCorners(a);
                        // Compare in the photo's own (rotated) space.
                        Vector2 min = photo.InverseTransformPoint(a[0]), max = photo.InverseTransformPoint(a[2]);
                        Rect r = photo.rect;
                        string who = Find(panel, "CandidateName").GetComponent<Text>().text;
                        Assert.That(min.x <= r.xMin + 1f && min.y <= r.yMin + 1f && max.x >= r.xMax - 1f && max.y >= r.yMax - 1f, Is.True,
                            $"{who}: the portrait must cover the whole badge window");
                        Capture(camera, target, $"badge-p{pool}-{i}.png");
                        Find(panel, "NextCandidate").GetComponent<Button>().onClick.Invoke();
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(host); Object.DestroyImmediate(camera.gameObject);
                target.Release(); Object.DestroyImmediate(target);
                LogAssert.ignoreFailingMessages = previousIgnore;
            }
        }

        static Transform Find(GachaPanel panel, string name) => panel.GetComponentsInChildren<Transform>()
            .First(t => t.name == name && t.gameObject.activeInHierarchy);
        static PointerEventData AssertReachable(Button button, GraphicRaycaster raycaster, Camera camera)
        {
            var rect = (RectTransform)button.transform;
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center)),
                button = PointerEventData.InputButton.Left,
            };
            var hits = new List<RaycastResult>(); raycaster.Raycast(pointer, hits);
            Assert.That(hits, Is.Not.Empty);
            Assert.That(hits[0].gameObject.GetComponentInParent<Button>(), Is.EqualTo(button), button.name + " must remain reachable");
            return pointer;
        }
        static void Click(Button button, GraphicRaycaster raycaster, Camera camera)
        {
            var pointer = AssertReachable(button, raycaster, camera);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }
        static void Capture(Camera camera, RenderTexture target, string name)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            Canvas.ForceUpdateCanvases(); camera.Render();
            var before = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply(); RenderTexture.active = before;
            string dir = Path.GetFullPath("Builds/qa-logs/audition-swipe-1102"); Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, name), image.EncodeToPNG()); Object.DestroyImmediate(image);
        }
    }
}
