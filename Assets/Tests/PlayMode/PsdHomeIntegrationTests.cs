using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ChoSiren.Systems.Economy;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ChoSiren.Tests
{
    public sealed class PsdHomeIntegrationTests
    {
        private ChoSirenApp app;
        private string originalSave;
        private bool hadSave;
        private const string SaveKey = GameModel.SaveKey;
        private bool hadLegacy;private string originalLegacy;
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnitySetUp] public IEnumerator SetUp()
        {
            hadSave = PlayerPrefs.HasKey(SaveKey);
            originalSave = PlayerPrefs.GetString(SaveKey);
            hadLegacy=PlayerPrefs.HasKey(GameModel.LegacySaveKey);originalLegacy=PlayerPrefs.GetString(GameModel.LegacySaveKey);
            foreach (var old in Object.FindObjectsByType<ChoSirenApp>(FindObjectsSortMode.None)) Object.Destroy(old.gameObject);
            yield return null;
            PlayerPrefs.DeleteKey(SaveKey);PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
            app = new GameObject("PSD Home Test").AddComponent<ChoSirenApp>();
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return new WaitForSecondsRealtime(.5f);
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            if (app != null) Object.Destroy(app.gameObject);
            yield return null;
            if (hadSave) PlayerPrefs.SetString(SaveKey, originalSave); else PlayerPrefs.DeleteKey(SaveKey);
            if(hadLegacy)PlayerPrefs.SetString(GameModel.LegacySaveKey,originalLegacy);else PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
            PlayerPrefs.Save();
        }

        private GameObject Home => GameObject.Find("PsdHome20260921");
        private void Show(string page) => typeof(ChoSirenApp).GetMethod("ShowScreen", PrivateInstance).Invoke(app, new object[]{page});

        [UnityTest] public IEnumerator AuthoredBackdropMovesBehindCharacterWithoutChangingButtons()
        {
            var motion=Home.GetComponent<PsdHomeBackdropMotion929>();Assert.That(motion,Is.Not.Null);
            var disc=(RectTransform)Home.transform.Find("layer-52VisualV2");
            var title=(RectTransform)motion.TitleWindow.Find("layer-53VisualV2");
            var actor=Home.transform.Find("layer-04VisualV2");
            var button=(RectTransform)Home.transform.Find("LiveOnStage");var hit=button.anchoredPosition;
            var x=title.anchoredPosition.x;var rotation=disc.localRotation;
            motion.Advance(1);
            Assert.That(title.anchoredPosition.x,Is.LessThan(x));
            Assert.That(title.anchoredPosition.y,Is.GreaterThan(0),"Lettering must follow its authored diagonal, not a horizontal banner");
            Assert.That(Quaternion.Angle(rotation,disc.localRotation),Is.GreaterThan(6));
            Assert.That(button.anchoredPosition,Is.EqualTo(hit));
            Assert.That(disc.GetSiblingIndex(),Is.LessThan(actor.GetSiblingIndex()));
            Assert.That(motion.TitleWindow.GetSiblingIndex(),Is.LessThan(actor.GetSiblingIndex()));
            yield return null;
        }
        [UnityTest] public IEnumerator TitleRepeatsStayInsideOriginalLeftHandPsdRegion()
        {
            var motion=Home.GetComponent<PsdHomeBackdropMotion929>();motion.enabled=false;
            var window=motion.TitleWindow;var origin=window.anchoredPosition;
            Assert.That(window.GetComponent<RectMask2D>(),Is.Not.Null);
            Assert.That(window.rect.width,Is.EqualTo(1796f*720/4034).Within(.01f));
            Assert.That(window.rect.height,Is.EqualTo(1694f*720/4034).Within(.01f));
            Assert.That(window.anchoredPosition.x+window.rect.width,Is.LessThan(360f),"Repeat window must stay in the authored left half");
            Assert.That(Home.transform.Find("BackdropTitleRepeat1"),Is.Null,"Repeats must never be siblings covering the whole homepage");
            foreach(var image in window.GetComponentsInChildren<Image>())
            {
                Assert.That(image.raycastTarget,Is.False);
                Assert.That(image.rectTransform.rect.size,Is.EqualTo(window.rect.size));
            }
            for(int phase=0;phase<5;phase++)
            {
                motion.Advance(window.rect.width/22f*.25f);
                Assert.That(Vector2.Distance(window.anchoredPosition,origin),Is.LessThan(.01f));
                Assert.That(window.GetComponentsInChildren<Image>().All(x=>x.GetComponentInParent<RectMask2D>()==window.GetComponent<RectMask2D>()),Is.True);
                yield return null;
                CaptureTitlePhase("home-title-phase-"+phase+".png");
            }
        }
        void CaptureTitlePhase(string name)
        {
            string output=System.Environment.GetEnvironmentVariable("CHO_TITLE_CAPTURE");if(string.IsNullOrEmpty(output))return;
            var loading=GameObject.Find("StartupLoading");if(loading!=null)loading.SetActive(false);
            var canvas=Home.GetComponentInParent<Canvas>();
            var scaler=canvas.GetComponent<CanvasScaler>();bool scalerEnabled=scaler.enabled;scaler.enabled=false;float oldScale=canvas.scaleFactor;
            var homeRect=(RectTransform)Home.transform;var homeScale=homeRect.localScale;
            var oldMode=canvas.renderMode;var oldCamera=canvas.worldCamera;float oldPlane=canvas.planeDistance;
            var camera=new GameObject("TitleCaptureCamera").AddComponent<Camera>();
            var target=new RenderTexture(720,1536,24);camera.targetTexture=target;
            canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=10;canvas.scaleFactor=1;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
            Canvas.ForceUpdateCanvases();homeRect.localScale=Vector3.one;camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;
            var texture=new Texture2D(720,1536,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,720,1536),0,0);texture.Apply();
            System.IO.Directory.CreateDirectory(output);System.IO.File.WriteAllBytes(System.IO.Path.Combine(output,name),texture.EncodeToPNG());
            RenderTexture.active=previous;canvas.renderMode=oldMode;canvas.worldCamera=oldCamera;canvas.planeDistance=oldPlane;canvas.scaleFactor=oldScale;scaler.enabled=scalerEnabled;homeRect.localScale=homeScale;
            Object.DestroyImmediate(texture);camera.targetTexture=null;target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(camera.gameObject);
        }
        [UnityTest] public IEnumerator FastRecordTransitionSwitchesOnceAndNeverLeavesAnInputBlocker()
        {
            int switched=0;var fx=AlbumSceneTransition929.Play(Home.transform,()=>switched++);
            fx.Advance(.26f);Assert.That(switched,Is.Zero);
            fx.Advance(.05f);Assert.That(switched,Is.EqualTo(1));Assert.That(fx.raycastTarget,Is.True);
            fx.Advance(.5f);Assert.That(switched,Is.EqualTo(1));Assert.That(fx.gameObject.activeSelf,Is.False);
            yield return null;Assert.That(Home.GetComponentInChildren<AlbumSceneTransition929>(),Is.Null);
        }
        [UnityTest] public IEnumerator RecordSleeveHasACompleteCoverFrameAndReleasesInput()
        {
            var fx=AlbumSceneTransition929.Play(Home.transform,()=>{});fx.ManualPlayback=true;
            fx.Advance(.14f);yield return null;CaptureTitlePhase("transition-record-in.png");
            fx.Advance(.16f);yield return null;CaptureTitlePhase("transition-record-covered.png");
            Assert.That(fx.FullyCovered,Is.True);
            Assert.That(fx.HasSwitched,Is.True);
            Assert.That(fx.GetComponent<Mask>(),Is.Not.Null);
            Assert.That(fx.GetComponentsInChildren<Image>().All(i=>!i.raycastTarget),Is.True);
            fx.Advance(.24f);yield return null;CaptureTitlePhase("transition-record-out.png");
            fx.Advance(.3f);Assert.That(fx.raycastTarget,Is.False);
            yield return null;Assert.That(Home.GetComponentInChildren<AlbumSceneTransition929>(),Is.Null);
        }

        [UnityTest] public IEnumerator EveryHotspotIsReachableWithoutNeighbourInterception()
        {
            Assert.That(Home, Is.Not.Null);
            // Batch Editor windows are landscape. Exercise the actual portrait canvas
            // (including its shared dock), rather than an unrelated Editor window size.
            var canvas=app.GetComponentInChildren<Canvas>();
            var camera=new GameObject("PortraitHitTestCamera").AddComponent<Camera>();
            var target=new RenderTexture(720,1536,24); camera.targetTexture=target;
            var previousMode=canvas.renderMode; var previousCamera=canvas.worldCamera;
            canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=camera; canvas.planeDistance=10f;
            Canvas.ForceUpdateCanvases(); yield return null; Canvas.ForceUpdateCanvases();
            try
            {
            var buttons = Home.GetComponentsInChildren<Button>()
                .Concat(GameObject.Find("BottomNavigation").GetComponentsInChildren<Button>())
                .Concat(GameObject.Find("TopBar").GetComponentsInChildren<Button>()).ToArray();
            foreach (string name in new[] { "Tasks", "AlbumProduction", "PracticeRoom", "LiveOnStage", "PreviousCharacter", "NextCharacter", "Nav-team", "Nav-members", "Nav-lobby", "Nav-accessory", "Nav-audition" })
                Assert.That(buttons.Any(b => b.name == name), Is.True, name);
            foreach (var button in buttons)
            {
                var rect = (RectTransform)button.transform;
                var point = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
                var data = new PointerEventData(EventSystem.current) { position = point };
                var hits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(data, hits);
                Assert.That(hits.Count, Is.GreaterThan(0), button.name);
                Assert.That(hits[0].gameObject.GetComponentInParent<Button>(), Is.EqualTo(button), button.name);
            }
            Assert.That(Home.GetComponentsInChildren<Image>().Where(x => x.name.EndsWith("VisualV2")).All(x => !x.raycastTarget), Is.True);
            }
            finally
            {
                canvas.renderMode=previousMode; canvas.worldCamera=previousCamera;
                camera.targetTexture=null; Object.DestroyImmediate(camera.gameObject);
                target.Release(); Object.DestroyImmediate(target);
            }
        }

        [UnityTest] public IEnumerator PointerFeedbackMovesArtworkAndRestoresItWithoutMovingHitArea()
        {
            foreach (var button in Home.GetComponentsInChildren<Button>().Where(b => b.GetComponent<LobbyHotspotFeedback>() != null))
            {
                var hit = (RectTransform)button.transform;
                var image = button.GetComponentsInChildren<Image>().FirstOrDefault(x => x.sprite != null && x.isActiveAndEnabled && x.name.EndsWith("VisualV2") && x.name != "layer-12VisualV2");
                var art = image != null ? image.rectTransform : null;
                var effect = button.transform.Find("InteractionFx").GetComponent<CanvasGroup>();
                Vector2 position = hit.anchoredPosition, artPosition = art != null ? art.anchoredPosition : Vector2.zero;
                Vector3 artScale = art != null ? art.localScale : Vector3.one;
                var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
                ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerEnterHandler);
                yield return new WaitForSecondsRealtime(.2f);
                Assert.That(effect.alpha, Is.GreaterThan(.2f), button.name);
                if (art != null) Assert.That(Vector3.Distance(artScale, art.localScale), Is.GreaterThan(.001f));
                Assert.That(hit.anchoredPosition, Is.EqualTo(position));
                ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerDownHandler);
                yield return new WaitForSecondsRealtime(.2f);
                if (art != null) Assert.That(art.localScale.x, Is.LessThan(artScale.x));
                ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerExitHandler);
                yield return new WaitForSecondsRealtime(.4f);
                Assert.That(effect.alpha, Is.LessThan(.05f), button.name);
                if (art != null)
                {
                    Assert.That(Vector3.Distance(artScale, art.localScale), Is.LessThan(.001f));
                    Assert.That(Vector2.Distance(artPosition, art.anchoredPosition), Is.LessThan(.01f));
                }
                Assert.That(hit.anchoredPosition, Is.EqualTo(position));
            }
        }

        [UnityTest] public IEnumerator StageHeartbeatMovesFrontArtworkWithHaloButKeepsHitAreaFixed()
        {
            Transform button = Home.transform.Find("LiveOnStage");
            RectTransform hit = (RectTransform)button;
            RectTransform face = (RectTransform)button.Find("StageFaceBeat");
            Image artwork = face.GetComponentsInChildren<Image>()
                .First(image => image.name == "layer-22VisualV2");
            Image halo = face.GetComponentsInChildren<Image>()
                .First(image => image.name == "StageBeatEcho");
            Vector2 hitPosition = hit.anchoredPosition;
            Vector3 hitScale = hit.localScale;
            float minScale = float.MaxValue, maxScale = float.MinValue;
            for (int i = 0; i < 14; i++)
            {
                float scale = artwork.rectTransform.lossyScale.x;
                minScale = Mathf.Min(minScale, scale);
                maxScale = Mathf.Max(maxScale, scale);
                Assert.That(hit.anchoredPosition, Is.EqualTo(hitPosition));
                Assert.That(hit.localScale, Is.EqualTo(hitScale));
                Assert.That(halo.transform.parent, Is.EqualTo(artwork.transform.parent));
                yield return new WaitForSecondsRealtime(.08f);
            }
            Assert.That(maxScale / minScale, Is.GreaterThan(1.035f),
                "The authored CTA face must visibly beat, not only the image behind it.");
        }

        [UnityTest] public IEnumerator AlbumInkStartsHiddenAndAppearsOnlyAfterSelection()
        {
            var album = Home.transform.Find("AlbumProduction");
            Assert.That(album, Is.Not.Null);
            var ink = album.GetComponent<PsdHomeAlbumInkMotion>();
            Assert.That(ink, Is.Not.Null);
            Assert.That(ink.Reveal, Is.EqualTo(0f));
            album.GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(ink.Reveal, Is.GreaterThan(.75f));
            album.GetComponent<PsdHomeAlbumInkMotion>().SetSelected(false);
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(ink.Reveal, Is.LessThan(.05f));
        }

        [UnityTest] public IEnumerator AlbumInkPreviewsOnHoverAndClearsOnExit()
        {
            var album = Home.transform.Find("AlbumProduction");
            var ink = album.GetComponent<PsdHomeAlbumInkMotion>();
            var eventData = new PointerEventData(EventSystem.current);
            ExecuteEvents.Execute(album.gameObject, eventData, ExecuteEvents.pointerEnterHandler);
            yield return new WaitForSecondsRealtime(.18f);
            Assert.That(ink.Reveal, Is.GreaterThan(.9f));
            ExecuteEvents.Execute(album.gameObject, eventData, ExecuteEvents.pointerExitHandler);
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(ink.Reveal, Is.LessThan(.05f));
        }

        [UnityTest] public IEnumerator EveryEntryPlaysTheApprovedInkOnHover()
        {
            foreach (string entry in new[] { "Tasks", "PracticeRoom", "LiveOnStage" })
            {
                var card = Home.transform.Find(entry);
                var ink = card.GetComponent<PsdHomeAlbumInkMotion>();
                Assert.That(ink, Is.Not.Null, entry);
                Assert.That(card.Find("AlbumSelectedInk").GetComponent<PsdHomeInkAtlas>().raycastTarget, Is.False, entry);
                var eventData = new PointerEventData(EventSystem.current);
                ExecuteEvents.Execute(card.gameObject, eventData, ExecuteEvents.pointerEnterHandler);
                yield return new WaitForSecondsRealtime(.18f);
                Assert.That(ink.Reveal, Is.GreaterThan(.9f), entry);
                ExecuteEvents.Execute(card.gameObject, eventData, ExecuteEvents.pointerExitHandler);
                yield return new WaitForSecondsRealtime(.2f);
                Assert.That(ink.Reveal, Is.LessThan(.05f), entry);
            }
            // The CTA's ink wraps the whole diamond: its footprint covers the card in both directions.
            Rect cta = WorldBounds((RectTransform)Home.transform.Find("LiveOnStage"));
            Rect ctaInk = WorldBounds((RectTransform)Home.transform.Find("LiveOnStage/AlbumSelectedInk"));
            Assert.That(ctaInk.Contains(cta.center), Is.True, "CTA ink must sit behind the button");
            Assert.That(ctaInk.width, Is.GreaterThan(cta.width * .95f));
            Assert.That(ctaInk.height, Is.GreaterThan(cta.height * .9f));
            Color albumGreen = Home.transform.Find("AlbumProduction/AlbumSelectedInk").GetComponent<PsdHomeInkAtlas>().color;
            foreach (string entry in new[] { "Tasks", "AlbumProduction", "PracticeRoom", "LiveOnStage" })
                Assert.That(Home.transform.Find(entry + "/AlbumSelectedInk").GetComponent<PsdHomeInkAtlas>().color,
                    Is.EqualTo(albumGreen), "Every entry uses the approved album green: " + entry);
        }

        private static Rect WorldBounds(RectTransform rt)
        {
            var c = new Vector3[4]; rt.GetWorldCorners(c);
            float x0 = Mathf.Min(c[0].x, c[1].x, c[2].x, c[3].x), x1 = Mathf.Max(c[0].x, c[1].x, c[2].x, c[3].x);
            float y0 = Mathf.Min(c[0].y, c[1].y, c[2].y, c[3].y), y1 = Mathf.Max(c[0].y, c[1].y, c[2].y, c[3].y);
            return Rect.MinMaxRect(x0, y0, x1, y1);
        }

        [UnityTest] public IEnumerator AlbumClickProducesTransientInkWave()
        {
            Home.transform.Find("AlbumProduction").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.That(Home.GetComponentsInChildren<PsdHomePulse>().Length, Is.EqualTo(0), "No generic burst over the approved ink");
            Assert.That(Home.GetComponentsInChildren<PsdHomeInkAtlas>().Length, Is.EqualTo(4));
            yield return new WaitForSecondsRealtime(.75f);
            Assert.That(Home.GetComponentsInChildren<PsdHomePulse>().Length, Is.EqualTo(0));
        }

        [UnityTest] public IEnumerator ApprovedInkLoadsBehindAlbumAndRepeatedClickClearsSelection()
        {
            var album = Home.transform.Find("AlbumProduction");
            var ink = album.GetComponent<PsdHomeAlbumInkMotion>();
            float deadline = Time.realtimeSinceStartup + 8f;
            while (!ink.Ready && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(ink.Ready, Is.True);
            var field = album.Find("AlbumSelectedInk").GetComponent<PsdHomeInkAtlas>();
            Assert.That(field.transform.GetSiblingIndex(), Is.LessThan(album.Find("layer-13VisualV2").GetSiblingIndex()));
            Assert.That(field.raycastTarget, Is.False);
            album.GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(ink.Selected, Is.True);
            Assert.That(field.mainTexture.width, Is.EqualTo(2048));
            album.GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(ink.Selected, Is.False);
            Assert.That(ink.Reveal, Is.LessThan(.01f));
        }

        [UnityTest] public IEnumerator HiddenUiDoesNotRetainAnOldAlbumHover()
        {
            var album = Home.transform.Find("AlbumProduction");
            var ink = album.GetComponent<PsdHomeAlbumInkMotion>();
            ink.OnPointerEnter(new PointerEventData(EventSystem.current));
            yield return new WaitForSecondsRealtime(.2f);
            Home.GetComponent<PsdHomeMotion>().SceneClicked(2, false);
            yield return new WaitForSecondsRealtime(.3f);
            Home.GetComponent<PsdHomeMotion>().SceneClicked(4, false);
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(ink.Reveal, Is.LessThan(.01f));
        }

        [UnityTest] public IEnumerator DoubleClickSceneHidesUiWithoutLosingRestoreTarget()
        {
            GameObject sceneTouch = Home.transform.Find("PsdSceneTouch").gameObject;
            RectTransform tasks = (RectTransform)Home.transform.Find("Tasks");
            RectTransform live = (RectTransform)Home.transform.Find("LiveOnStage");
            Vector2 tasksRest = tasks.anchoredPosition, liveRest = live.anchoredPosition;
            var data = new PointerEventData(EventSystem.current) { clickCount = 2 };
            ExecuteEvents.Execute(sceneTouch, data, ExecuteEvents.pointerClickHandler);
            yield return new WaitForSecondsRealtime(.23f);
            Assert.That(Home.GetComponent<PsdHomeMotion>().UiHidden, Is.True);
            var tasksGroup = Home.transform.Find("Tasks").GetComponent<CanvasGroup>();
            Assert.That(tasksGroup, Is.Not.Null,
                string.Join(", ", Home.GetComponentsInChildren<CanvasGroup>().Select(group => group.name)));
            Assert.That(tasksGroup.alpha, Is.EqualTo(0f).Within(.02f));
            Assert.That(tasks.anchoredPosition.x, Is.LessThan(tasksRest.x - 65f));
            Assert.That(live.anchoredPosition.x, Is.GreaterThan(liveRest.x + 65f));
            Assert.That(PsdHomeStageBridge.UiHidden, Is.True);
            Assert.That(sceneTouch.GetComponent<Image>().raycastTarget, Is.True);
            // A fast second double-click may continue the same pointer sequence
            // with clickCount 4; it must still restore the UI.
            yield return new WaitForSecondsRealtime(.18f);
            data.clickCount = 4;
            ExecuteEvents.Execute(sceneTouch, data, ExecuteEvents.pointerClickHandler);
            yield return new WaitForSecondsRealtime(.23f);
            Assert.That(Home.GetComponent<PsdHomeMotion>().UiHidden, Is.False);
            Assert.That(PsdHomeStageBridge.UiHidden, Is.False);
            Assert.That(Home.transform.Find("Tasks").GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f).Within(.02f));
            Assert.That(Vector2.Distance(tasks.anchoredPosition, tasksRest), Is.LessThan(.01f));
            Assert.That(Vector2.Distance(live.anchoredPosition, liveRest), Is.LessThan(.01f));
        }

        [UnityTest] public IEnumerator BrowserAndUnityDoubleClickShareOneToggleGate()
        {
            var motion = Home.GetComponent<PsdHomeMotion>();
            motion.SceneClicked(2, false);
            motion.ToggleUiFromBrowser();
            Assert.That(motion.UiHidden, Is.True);
            yield return new WaitForSecondsRealtime(.25f);
            motion.ToggleUiFromBrowser();
            motion.SceneClicked(4, false);
            Assert.That(motion.UiHidden, Is.False);
        }

        [UnityTest] public IEnumerator CharacterSceneRaycastOpensVisibleDialogue()
        {
            RectTransform sceneTouch = (RectTransform)Home.transform.Find("PsdSceneTouch");
            Vector3 world = sceneTouch.TransformPoint(new Vector3(350, -310));
            Vector2 point = RectTransformUtility.WorldToScreenPoint(null, world);
            var data = new PointerEventData(EventSystem.current) { position = point, clickCount = 1 };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(data, hits);
            Assert.That(hits.Count, Is.GreaterThan(0));
            Assert.That(hits[0].gameObject, Is.EqualTo(sceneTouch.gameObject),
                string.Join(", ", hits.Select(hit => hit.gameObject.name)));
            ExecuteEvents.Execute(hits[0].gameObject, data, ExecuteEvents.pointerClickHandler);
            yield return new WaitForSecondsRealtime(.5f);
            Assert.That(Home.transform.Find("PsdCharacterDialogue").GetComponent<CanvasGroup>().alpha,
                Is.GreaterThan(.8f));
        }

        [UnityTest] public IEnumerator InlinePerformanceReplacesAndRestoresAuthoredPortrait()
        {
            var motion = Home.GetComponent<PsdHomeMotion>();
            var portrait = Home.transform.Find("layer-04VisualV2").GetComponent<Image>();
            var rim = Home.transform.Find("layer-03VisualV2").GetComponent<Image>();
            Assert.That(portrait.enabled && rim.enabled, Is.True);
            motion.SetCharacterPlayback(1);
            yield return null;
            Assert.That(portrait.enabled || rim.enabled, Is.False);
            motion.SetCharacterPlayback(0);
            yield return null;
            Assert.That(portrait.enabled && rim.enabled, Is.True);
        }

        [UnityTest] public IEnumerator PerformanceIsInsideRealUiStackAndPreservesVideoAspect()
        {
            var live = Home.transform.Find("CharacterPerformance");
            Assert.That(live, Is.Not.Null);
            Assert.That(live.GetSiblingIndex(), Is.GreaterThan(Home.transform.Find("layer-04VisualV2").GetSiblingIndex()));
            foreach (string name in new[]{"Tasks", "AlbumProduction", "PracticeRoom", "LiveOnStage"})
                Assert.That(live.GetSiblingIndex(), Is.LessThan(Home.transform.Find(name).GetSiblingIndex()), name);
            var image = live.GetComponent<RawImage>();
            Assert.That(image.raycastTarget, Is.False);
            for (int clip = 0; clip < 3; clip++)
            {
                live.GetComponent<PsdHomeCharacterSurface>().PositionClip(clip);
                Assert.That(image.rectTransform.rect.width / image.rectTransform.rect.height, Is.EqualTo(9f / 16f).Within(.0001f));
            }
            yield return null;
        }

        [UnityTest] public IEnumerator EmptySceneClickDoesNotTriggerCharacterDialogue()
        {
            RectTransform sceneTouch = (RectTransform)Home.transform.Find("PsdSceneTouch");
            Vector3 world = sceneTouch.TransformPoint(new Vector3(35, -700));
            Vector2 point = RectTransformUtility.WorldToScreenPoint(null, world);
            var data = new PointerEventData(EventSystem.current) { position = point, clickCount = 1 };
            ExecuteEvents.Execute(sceneTouch.gameObject, data, ExecuteEvents.pointerClickHandler);
            yield return new WaitForSecondsRealtime(.5f);
            Assert.That(Home.transform.Find("PsdCharacterDialogue").GetComponent<CanvasGroup>().alpha,
                Is.LessThan(.05f));
        }

        [UnityTest] public IEnumerator NavigationRestoresOtherPageChromeAndReturnsToSingleAuthoredHome()
        {
            GameObject.Find("BottomNavigation").transform.Find("Nav-members").GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(Home, Is.Null);
            Assert.That(GameObject.Find("TopBar"), Is.Not.Null);
            Assert.That(GameObject.Find("BottomNavigation"), Is.Not.Null);
            Show("lobby");
            yield return null;
            Assert.That(Home, Is.Not.Null);
            Assert.That(GameObject.Find("TopBar"), Is.Not.Null);
            Assert.That(GameObject.Find("BottomNavigation"), Is.Not.Null);
            Assert.That(Object.FindObjectsByType<PsdHomeFit>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
        }

        [UnityTest] public IEnumerator RhythmStageStopsOnFullScreenMapAndRestartsOnLobbyReturn()
        {
            Assert.That(PsdHomeStageBridge.Active, Is.True);
            Home.transform.Find("LiveOnStage").GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSecondsRealtime(.9f);
            Assert.That(PsdHomeStageBridge.Active, Is.False);
            Show("lobby");
            yield return null;
            Assert.That(PsdHomeStageBridge.Active, Is.True);
        }

        [UnityTest] public IEnumerator ExchangeUpdatesLiveBalancesAndNeverUsesPsdDemoNumbers()
        {
            var model = (GameModel)typeof(ChoSirenApp).GetField("model", PrivateInstance).GetValue(app);
            model.Save.Diamonds = 1000;
            model.Save.Gold = 20;
            Assert.That(model.TryExchangeDiamondsForGold(out _), Is.True);
            // reward-ui 1101: the gained gold flies into its HUD pill and the number rolls up when it lands.
            yield return new WaitForSecondsRealtime(1.8f);
            var bar=GameObject.Find("TopBar").transform;
            Assert.That(bar.Find("Currency-" + CurrencyIds.Diamond + "/Diamonds").GetComponent<Text>().text, Is.EqualTo("900"));
            Assert.That(bar.Find("Currency-" + CurrencyIds.Gold + "/Gold").GetComponent<Text>().text.Replace(",", ""), Is.EqualTo("2020"));
        }
    }
}
