using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using ChoSiren.Panels;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ChoSiren.Tests
{
    public sealed class PsdCharacterSwitchTests
    {
        const string Out = "/Users/bojuejun/Documents/Codex/2026-09-21/zhe/work/character-switch-20260928";
        ChoSirenApp app;
        string save, legacy; bool had, hadLegacy;
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        GameObject Home => GameObject.Find("PsdHome20260921");
        PsdHomeCharacterCarousel Carousel => Home.GetComponent<PsdHomeCharacterCarousel>();
        GameModel Model => (GameModel)typeof(ChoSirenApp).GetField("model", Private).GetValue(app);

        [UnitySetUp] public IEnumerator SetUp()
        {
            had = PlayerPrefs.HasKey(GameModel.SaveKey); save = PlayerPrefs.GetString(GameModel.SaveKey);
            hadLegacy = PlayerPrefs.HasKey(GameModel.LegacySaveKey); legacy = PlayerPrefs.GetString(GameModel.LegacySaveKey);
            PlayerPrefs.SetString(GameModel.SaveKey, JsonUtility.ToJson(new GameSave { HomeCharacterIndex = 0, StoryProgress = 100 }));
            foreach (var old in Object.FindObjectsByType<ChoSirenApp>(FindObjectsSortMode.None)) Object.Destroy(old.gameObject);
            yield return null;
            app = new GameObject("CharacterSwitchTest").AddComponent<ChoSirenApp>();
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
        [UnityTest] public IEnumerator ArrowsSwapOnlyPortraitAndRememberSelectionAcrossLobbyRebuild()
        {
            var portrait = Home.transform.Find("layer-04VisualV2").GetComponent<Image>();
            int index = portrait.transform.GetSiblingIndex();
            var tasks = Home.transform.Find("Tasks");
            Vector3 taskPosition = tasks.position;
            Assert.That(Carousel.Index, Is.EqualTo(0));
            Home.transform.Find("NextCharacter").GetComponent<Button>().onClick.Invoke();
            Carousel.Switch(1); // A second event during the same transition cannot double-advance.
            yield return new WaitForSecondsRealtime(.45f);
            Assert.That(Carousel.Index, Is.EqualTo(1));
            Assert.That(portrait.sprite.name, Does.Contain("character2-portrait"));
            Assert.That(portrait.transform.GetSiblingIndex(), Is.EqualTo(index));
            Assert.That(tasks.position, Is.EqualTo(taskPosition));
            Assert.That(portrait.transform.GetSiblingIndex(), Is.LessThan(tasks.GetSiblingIndex()));
            Assert.That(Model.Save.HomeCharacterIndex, Is.EqualTo(1));
            Assert.That(new GameModel().Save.HomeCharacterIndex, Is.EqualTo(1));
            typeof(ChoSirenApp).GetMethod("ShowScreen", Private).Invoke(app,new object[]{"members"});
            yield return null;
            typeof(ChoSirenApp).GetMethod("ShowScreen", Private).Invoke(app,new object[]{"lobby"});
            yield return null;
            Assert.That(Carousel.Index, Is.EqualTo(1));
            Home.transform.Find("PreviousCharacter").GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSecondsRealtime(.45f);
            Assert.That(Carousel.Index, Is.EqualTo(0));
            Assert.That(Carousel.HasPerformance, Is.True);
        }
        void Drag(Vector2 start, Vector2 end)
        {
            var surface=(RectTransform)Home.transform.Find("PsdSceneTouch");
            Vector2 Screen(Vector2 p)=>RectTransformUtility.WorldToScreenPoint(null,surface.TransformPoint(p));
            var data=new PointerEventData(EventSystem.current){position=Screen(start),button=PointerEventData.InputButton.Left,clickCount=1};
            ExecuteEvents.Execute(surface.gameObject,data,ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(surface.gameObject,data,ExecuteEvents.beginDragHandler);
            data.position=Screen(end);
            ExecuteEvents.Execute(surface.gameObject,data,ExecuteEvents.dragHandler);
            ExecuteEvents.Execute(surface.gameObject,data,ExecuteEvents.endDragHandler);
            ExecuteEvents.Execute(surface.gameObject,data,ExecuteEvents.pointerClickHandler);
        }
        [UnityTest] public IEnumerator SwipeCancelsPendingDialogueAndVerticalDragDoesNotSwitch()
        {
            Home.GetComponent<PsdHomeMotion>().SceneClicked(1,true);
            Drag(new Vector2(510,-370),new Vector2(220,-365));
            yield return new WaitForSecondsRealtime(.55f);
            Assert.That(Carousel.Index,Is.EqualTo(1));
            Assert.That(Home.transform.Find("PsdCharacterDialogue").GetComponent<CanvasGroup>().alpha,Is.EqualTo(0));
            Drag(new Vector2(410,-370),new Vector2(430,-670));
            yield return new WaitForSecondsRealtime(.45f);
            Assert.That(Carousel.Index,Is.EqualTo(1));
            Drag(new Vector2(260,-370),new Vector2(450,-365));
            yield return new WaitForSecondsRealtime(.45f);
            Assert.That(Carousel.Index,Is.EqualTo(0));
        }
        [UnityTest] public IEnumerator SecondCharacterSpeaksHerOwnLinesAndHiddenArrowsDoNotIntercept()
        {
            Carousel.Switch(1);yield return new WaitForSecondsRealtime(.45f);
            Home.GetComponent<PsdHomeMotion>().SceneClicked(1,true);
            yield return new WaitForSecondsRealtime(.6f);
            const BindingFlags Statics = BindingFlags.Static|BindingFlags.NonPublic;
            var bubble=Home.transform.Find("PsdCharacterDialogue");
            string shown=bubble.transform.Find("DialogueLine").GetComponent<Text>().text;
            Assert.That(bubble.GetComponent<CanvasGroup>().alpha,Is.GreaterThan(.8f));
            Assert.That(bubble.transform.Find("Speaker").GetComponent<Text>().text,Is.EqualTo("绯音"));
            // 2026-10-25: the second idol answers with her own lines, never Catalena's.
            string[] catalena=(string[])typeof(PsdHomeMotion).GetField("Lines",Statics).GetValue(null);
            string[] lilith=(string[])typeof(PsdHomeMotion).GetField("LilithLines",Statics).GetValue(null);
            Assert.That(catalena,Does.Not.Contain(shown),shown);
            Assert.That(lilith.Any(line=>Model.Personalize(line)==shown),Is.True,shown);
            var motion=Home.GetComponent<PsdHomeMotion>();motion.SceneClicked(2,false);
            yield return new WaitForSecondsRealtime(.3f);
            foreach(string name in new[]{"PreviousCharacter","NextCharacter"})
            {
                var arrow=Home.transform.Find(name);
                Assert.That(arrow.GetComponent<CanvasGroup>().blocksRaycasts,Is.False);
                Assert.That(arrow.GetComponent<Button>().interactable,Is.False);
            }
            Drag(new Vector2(260,-370),new Vector2(450,-365));
            yield return new WaitForSecondsRealtime(.45f);
            Assert.That(Carousel.Index,Is.EqualTo(0),"Hidden-UI mode still permits intentional character swipes.");
        }
        [UnityTest] public IEnumerator V3IdolsJoinTheCarouselWrapAroundAndSpeakTheirOwnLines()
        {
            var portrait = Home.transform.Find("layer-04VisualV2").GetComponent<Image>();
            string[] expectedNames = { "星璃", "绯音", "夜莺", "雾白" };
            Assert.That(Home.transform.Find("PsdCharacterDialogue/Speaker").GetComponent<Text>().text,
                Is.EqualTo(expectedNames[0]));
            for (int step = 0; step < 3; step++)
            {
                Carousel.Switch(1); yield return new WaitForSecondsRealtime(.45f);
                Assert.That(Home.transform.Find("PsdCharacterDialogue/Speaker").GetComponent<Text>().text,
                    Is.EqualTo(expectedNames[step + 1]));
            }
            Assert.That(Carousel.Index, Is.EqualTo(3));
            Assert.That(portrait.sprite.name, Does.Contain("character4-portrait"));
            Assert.That(Model.Save.HomeCharacterIndex, Is.EqualTo(3));
            Carousel.Switch(1); yield return new WaitForSecondsRealtime(.45f);
            Assert.That(Carousel.Index, Is.EqualTo(0), "The carousel wraps back to the first idol.");
            Carousel.Switch(-1); yield return new WaitForSecondsRealtime(.45f);
            Assert.That(Carousel.Index, Is.EqualTo(3));
            Home.GetComponent<PsdHomeMotion>().SceneClicked(1, true);
            yield return new WaitForSecondsRealtime(.6f);
            const BindingFlags Statics = BindingFlags.Static|BindingFlags.NonPublic;
            var bubble = Home.transform.Find("PsdCharacterDialogue");
            string shown = bubble.transform.Find("DialogueLine").GetComponent<Text>().text;
            Assert.That(bubble.GetComponent<CanvasGroup>().alpha, Is.GreaterThan(.8f));
            Assert.That(bubble.transform.Find("Speaker").GetComponent<Text>().text, Is.EqualTo("雾白"));
            var guests = (string[][])typeof(PsdHomeMotion).GetField("GuestLines", Statics).GetValue(null);
            Assert.That(guests[1].Any(line => Model.Personalize(line) == shown), Is.True, shown);
        }
        [UnityTest] public IEnumerator CaptureBothAuthoredCharactersAndTransparentBossInRealBattle()
        {
            var camera=new GameObject("CharacterCaptureCamera",typeof(Camera)).GetComponent<Camera>();
            var rt=new RenderTexture(720,1536,24);
            try
            {
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.orthographic=true;camera.targetTexture=rt;
                var canvas=app.GetComponentInChildren<Canvas>();
                canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
                yield return new WaitForSecondsRealtime(1f);Capture(camera,rt,"home-catalena.png");
                Carousel.Switch(1);yield return new WaitForSecondsRealtime(.5f);Capture(camera,rt,"home-character2.png");
                foreach (var arrow in Home.GetComponentsInChildren<PsdHomeChevron>())
                {
                    var mesh = arrow.canvasRenderer.GetMesh();
                    Assert.That(mesh.vertexCount, Is.GreaterThanOrEqualTo(8));
                }
                var battle=Model.StartStageBattle("stage-1-5",928UL,out var message);
                Assert.That(battle,Is.Not.Null,message);
                Home.SetActive(false);
                var panel=TacticsBattlePanel.Open(canvas.transform,Model,battle,null);panel.StopAllCoroutines();
                yield return new WaitForSecondsRealtime(.3f);
                var boss=panel.GetComponentsInChildren<Image>().First(i=>i.name=="BossPortrait");
                Assert.That(boss.sprite.name,Does.Contain("boss-throne-20260928"));
                Assert.That(boss.sprite.rect.height,Is.GreaterThan(boss.sprite.rect.width));
                Capture(camera,rt,"battle-small-boss.png");
            }
            finally{Object.DestroyImmediate(camera.gameObject);rt.Release();Object.DestroyImmediate(rt);}
        }
        static void Capture(Camera camera,RenderTexture rt,string name)
        {
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)return;
            Canvas.ForceUpdateCanvases();camera.Render();var old=RenderTexture.active;RenderTexture.active=rt;
            var tex=new Texture2D(rt.width,rt.height,TextureFormat.RGBA32,false);
            tex.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);tex.Apply();Directory.CreateDirectory(Out);File.WriteAllBytes(Path.Combine(Out,name),tex.EncodeToPNG());RenderTexture.active=old;Object.DestroyImmediate(tex);
        }
    }
}
