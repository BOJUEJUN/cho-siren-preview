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
    /// 2026-11-04: captures the 星钻 panel and the top-up shop into Builds/qa-logs/topup-1104/.
    /// </summary>
    public sealed class TopUp1104CaptureTests
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
        public IEnumerator CaptureDiamondPanelAndTopUpShop()
        {
            var saveData = new GameSave { HomeCharacterIndex = 1, StoryProgress = 100, PlayerName = "经纪人" };
            saveData.CompletedStories.Add(GameModel.PrologueStoryId);
            PlayerPrefs.SetString(GameModel.SaveKey, JsonUtility.ToJson(saveData));
            PlayerPrefs.SetInt(AgreementKey, 1);
            foreach (var old in UnityEngine.Object.FindObjectsByType<ChoSirenApp>(FindObjectsSortMode.None)) UnityEngine.Object.Destroy(old.gameObject);
            yield return null;
            var app = new GameObject("TopUp1104").AddComponent<ChoSirenApp>();
            var camera = new GameObject("TopUp1104Camera", typeof(Camera)).GetComponent<Camera>();
            camera.transform.SetParent(app.transform, false);
            const int width = 1080, height = 2288;
            var rt = new RenderTexture(width, height, 24);
            try
            {
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.orthographic = true; camera.targetTexture = rt;
                Canvas canvas = app.GetComponentInChildren<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10;
                SafeAreaFitter.Layout(new Rect(0, 0, width, height), new Vector2(width, height), out Vector2 min, out Vector2 max, out bool wide);
                foreach (var fitter in canvas.GetComponentsInChildren<SafeAreaFitter>(true))
                {
                    fitter.enabled = false;
                    var safe = (RectTransform)fitter.transform;
                    safe.anchorMin = min; safe.anchorMax = max; safe.offsetMin = safe.offsetMax = Vector2.zero;
                }
                canvas.GetComponent<CanvasScaler>().matchWidthOrHeight = wide ? 1f : 0f;
                yield return null;
                Invoke(app, "ShowScreenSafely", "lobby");
                yield return null;
                Invoke(app, "CloseModal");
                yield return new WaitForSecondsRealtime(1f);
                Invoke(app, "OpenCurrency1101", ChoSiren.Systems.Economy.CurrencyIds.Diamond);
                yield return new WaitForSecondsRealtime(.8f);
                Shot(camera, rt, "diamond-panel.png");
                Invoke(app, "OpenTopUp1104");
                yield return new WaitForSecondsRealtime(.6f);
                Shot(camera, rt, "topup-shop-pre.png");
                Invoke(app, "CloseModal");
                yield return null;
                // 2026-11-04: the former 专辑制作 card opens 创意工坊.
                Invoke(app, "ActivatePsdHomeAction", "AlbumProduction");
                yield return new WaitForSecondsRealtime(.8f);
                Assert.That(canvas.GetComponentsInChildren<RectTransform>(true).Any(r => r.name == "WorkshopPage1104"), Is.True, "创意工坊 opens");
                Assert.That(canvas.GetComponentsInChildren<RectTransform>(true).Any(r => r.name == "WorkshopTool-dress"), Is.True);
                yield return new WaitForSecondsRealtime(2.5f);
                Assert.That(canvas.GetComponentsInChildren<RawImage>(true).Any(r => r.name == "WorkshopFigureVideo" && r.enabled),
                    Is.True, "初始角色's idle loop plays");
                Shot(camera, rt, "workshop.png");
                var stage = UnityEngine.Object.FindAnyObjectByType<WorkshopStage1104>();
                Assert.That(stage, Is.Not.Null);
                Button Tool(string id) => canvas.GetComponentsInChildren<Button>(true).First(b => b.name == "WorkshopTool-" + id);
                // 2026-11-08: hidden tools (滤镜 / 亮度 / 应援色 …) are still opened directly to keep their panels covered.
                void OpenTool(string id) => typeof(ChoSirenApp).GetMethod("OpenWorkshopTool1104", Private, null, new[] { typeof(string) }, null).Invoke(app, new object[] { id });
                // 2026-11-07: 作曲 is shown only in the private test build (public builds have nine tools).
                Assert.That(canvas.GetComponentsInChildren<Button>(true).Count(b => b.name.StartsWith("WorkshopTool-")), Is.EqualTo(WorkshopMusic1105.TestBuild ? 4 : 3), "the simplified bar shows 换装 / 背景 / 转身");
                Assert.That(canvas.GetComponentsInChildren<Button>(true).Any(b => b.name == "WorkshopTool-music"), Is.EqualTo(WorkshopMusic1105.TestBuild), "作曲 only appears with the private test code");
                Assert.That(stage.Shot, Is.EqualTo(1), "the editor initially frames the upper body");
                var workshopPage = canvas.GetComponentsInChildren<RectTransform>(true).First(r => r.name == "WorkshopPage1104");
                Assert.That(workshopPage.Find("WorkshopDial").gameObject.activeSelf, Is.False, "parameters never cover her with a giant disc");
                OpenTool("bright");
                yield return new WaitForSecondsRealtime(.5f);
                Slider parameterSlider = canvas.GetComponentsInChildren<Slider>(true).First(s => s.name == "ParameterSlider");
                parameterSlider.value = 4; // 60 + 4 * 5 = 80%
                yield return new WaitForSecondsRealtime(.7f);
                Assert.That(canvas.GetComponentsInChildren<Text>(true).First(t => t.name == "ParameterValue").text, Is.EqualTo("80%"));
                var edited = (WorkshopLook1104)typeof(ChoSirenApp).GetField("workshopLook1104", Private).GetValue(app);
                Assert.That(edited.Brightness, Is.EqualTo(80), "the actual slider updates the preview's saved parameter");
                Shot(camera, rt, "workshop-dial.png");
                canvas.GetComponentsInChildren<Button>(true).First(b => b.name == "ParameterReset").onClick.Invoke();
                Assert.That(edited.Brightness, Is.EqualTo(100), "one reset restores neutral brightness");
                canvas.GetComponentsInChildren<Button>(true).First(b => b.name == "ParameterCollapse").onClick.Invoke();
                yield return new WaitForSecondsRealtime(.4f);
                Assert.That(canvas.GetComponentsInChildren<RectTransform>(false).Any(r => r.name == "WorkshopParameterPanel"), Is.False, "explicit 收起 returns to the preview and tools");
                Tool("bg").onClick.Invoke();
                yield return new WaitForSecondsRealtime(.3f);
                Tool("bg").onClick.Invoke();
                yield return new WaitForSecondsRealtime(.4f);
                Assert.That(canvas.GetComponentsInChildren<RectTransform>(false).Any(r => r.name == "WorkshopParameterPanel"), Is.False, "tapping the selected tool also collapses its drawer");
                Assert.That(stage.BottomBand, Is.EqualTo(118f), "closing returns the preview space");
                Shot(camera, rt, "workshop-tucked.png");
                // Offline music-panel fixture: exercise real editor controls without Config/Generate requests.
                typeof(ChoSirenApp).GetField("workshopMusicConfigured1105", Private).SetValue(app, true);
                typeof(ChoSirenApp).GetField("workshopMusicEnabled1105", Private).SetValue(app, true);
                var musicModel = (GameModel)typeof(ChoSirenApp).GetField("model", Private).GetValue(app);
                int musicDiamondsBefore = musicModel.Save.Diamonds;
                // The panel itself is still exercised offline, opened directly because public builds hide its button.
                typeof(ChoSirenApp).GetMethod("OpenWorkshopTool1104", Private, null, new[] { typeof(string) }, null).Invoke(app, new object[] { "music" });
                yield return new WaitForSecondsRealtime(.4f);
                RectTransform MusicPanel() => canvas.GetComponentsInChildren<RectTransform>(false).First(r => r.name == "WorkshopMusicPanel");
                var musicPanel = MusicPanel();
                Assert.That(musicPanel.GetComponentsInChildren<Button>().Any(b => b.name == "GenerateSong"), Is.True, "作曲 exposes its action without submitting it");
                Assert.That(musicPanel.GetComponentsInChildren<Text>().Any(t => t.text == "Suno6"), Is.True);
                musicPanel.GetComponentsInChildren<InputField>().First(f => f.name == "SongTitle").text = "星光试音";
                Shot(camera, rt, "workshop-music.png");
                musicPanel.GetComponentsInChildren<Button>().First(b => b.name == "JapaneseVoice").onClick.Invoke();
                yield return new WaitForSecondsRealtime(.25f);
                musicPanel = MusicPanel();
                Assert.That(musicPanel.GetComponentsInChildren<InputField>().Any(f => f.name == "Lyrics"), Is.True, "the voice mode reveals editable lyrics");
                musicPanel.GetComponentsInChildren<InputField>().First(f => f.name == "Lyrics").text = "星の向こうへ、一緒に歌おう。";
                musicPanel.GetComponentsInChildren<Button>().First(b => b.name == "Duration80").onClick.Invoke();
                yield return new WaitForSecondsRealtime(.25f);
                musicPanel = MusicPanel();
                var musicOptions = (WorkshopMusic1105.Options)typeof(ChoSirenApp).GetField("workshopMusicOptions1105", Private).GetValue(app);
                Assert.That(musicOptions.title, Is.EqualTo("星光试音"));
                Assert.That(musicOptions.duration, Is.EqualTo(80));
                Assert.That(musicOptions.instrumental, Is.False);
                Assert.That(musicOptions.lyrics, Does.Contain("一緒に歌おう"));
                ScrollRect musicScroll = musicPanel.GetComponentInChildren<ScrollRect>();
                Canvas.ForceUpdateCanvases();
                Assert.That(musicScroll.vertical && musicScroll.content.rect.height > musicScroll.viewport.rect.height, Is.True, "long song parameters scroll inside the drawer");
                musicScroll.verticalNormalizedPosition = 0f;
                yield return null;
                Shot(camera, rt, "workshop-music-lyrics.png");
                musicPanel.GetComponentsInChildren<Button>().First(b => b.name == "Collapse").onClick.Invoke();
                yield return new WaitForSecondsRealtime(.3f);
                Assert.That(canvas.GetComponentsInChildren<RectTransform>(false).Any(r => r.name == "WorkshopMusicPanel"), Is.False, "music 收起 restores the same preview/tools workspace");
                Assert.That(stage.BottomBand, Is.EqualTo(118f));
                Assert.That((bool)typeof(ChoSirenApp).GetField("workshopMusicBusy1105", Private).GetValue(app), Is.False);
                Assert.That(musicModel.Save.Diamonds, Is.EqualTo(musicDiamondsBefore), "editing/collapsing never pays or generates a song");
                Tool("dress").onClick.Invoke();
                yield return new WaitForSecondsRealtime(.6f);
                Assert.That(canvas.GetComponentsInChildren<RectTransform>(true).Any(r => r.name == "AiDressPanel"), Is.True, "AI 换装 stays in game");
                Shot(camera, rt, "workshop-ai.png");
                // 2026-11-05 poster reveal, from a saved outfit (no model call).
                if (Environment.GetEnvironmentVariable("WORKSHOP_REVEAL_SAMPLE") is string sample && File.Exists(sample))
                {
                    var look = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    look.LoadImage(File.ReadAllBytes(sample));
                    typeof(ChoSirenApp).GetField("aiDressResult1104", Private).SetValue(app, look);
                    stage.SetOverride(look);
                    Invoke(app, "OpenAiDress1104");
                    Invoke(app, "ShowAiReveal1105", look);
                    yield return new WaitForSecondsRealtime(.8f);
                    Assert.That(canvas.GetComponentsInChildren<RectTransform>(true).Any(r => r.name == "AiReveal1105"), Is.True, "the new look gets its reveal");
                    Shot(camera, rt, "workshop-reveal.png");
                    Invoke(app, "CloseAiReveal1105");
                    yield return new WaitForSecondsRealtime(.6f);
                    Shot(camera, rt, "workshop-ai-look.png");
                    Invoke(app, "UndoAiDress1105");
                    yield return null;
                }
                // 2026-11-05: one real generation through the game's service (costs one model call; opt-in).
                if (Environment.GetEnvironmentVariable("WORKSHOP_AI_LIVE") == "1")
                {
                    var gameModel = (GameModel)typeof(ChoSirenApp).GetField("model", Private).GetValue(app);
                    int diamondsBefore = gameModel.Save.Diamonds;
                    Invoke(app, "StartAiDress1104");
                    FieldInfo busy = typeof(ChoSirenApp).GetField("aiDressBusy1104", Private);
                    float started = Time.realtimeSinceStartup;
                    while ((bool)busy.GetValue(app) && Time.realtimeSinceStartup - started < 200f) yield return new WaitForSecondsRealtime(1f);
                    Debug.Log("[AI1105] " + typeof(ChoSirenApp).GetField("aiDressStatus1104", Private).GetValue(app)
                        + $" after {Time.realtimeSinceStartup - started:0}s");
                    Assert.That(typeof(ChoSirenApp).GetField("aiDressResult1104", Private).GetValue(app), Is.Not.Null, "AI 换装 returns an outfit");
                    Assert.That(gameModel.Save.Diamonds, Is.EqualTo(diamondsBefore - GameModel.AiDressDiamondCost1105), "the outfit costs 星钻");
                    yield return new WaitForSecondsRealtime(.8f);
                    Shot(camera, rt, "workshop-ai-result.png");
                    Invoke(app, "CloseModal");
                    yield return new WaitForSecondsRealtime(1.5f);
                    Shot(camera, rt, "workshop-ai-result-full.png");
                    stage.SetOverride(null);
                }
                OpenTool("accent");
                yield return new WaitForSecondsRealtime(.4f);
                stage.SetDialValue(1);
                yield return new WaitForSecondsRealtime(.8f);
                Shot(camera, rt, "workshop-accent.png");
                Tool("bg").onClick.Invoke();
                yield return new WaitForSecondsRealtime(.4f);
                stage.SetDialValue(2);
                yield return new WaitForSecondsRealtime(1f);
                Shot(camera, rt, "workshop-bg.png");
                // 星璃 has the angle sheets; the starter does not.
                Invoke(app, "StepWorkshopMember1104", 1);
                yield return new WaitForSecondsRealtime(.8f);
                stage = UnityEngine.Object.FindAnyObjectByType<WorkshopStage1104>();
                Tool("turn").onClick.Invoke();
                stage.TurnTo(3);
                yield return new WaitForSecondsRealtime(1.2f);
                Assert.That(stage.View, Is.EqualTo(3), "转身 reaches the back view");
                Shot(camera, rt, "workshop-back.png");
                stage.TurnTo(2);
                yield return new WaitForSecondsRealtime(.6f);
                Shot(camera, rt, "workshop-side.png");
                Invoke(app, "OpenVoiceRecorder1104");
                yield return new WaitForSecondsRealtime(.5f);
                Shot(camera, rt, "workshop-recorder.png");
                Invoke(app, "CloseModal");
                Invoke(app, "CloseWorkshop1104");
                yield return null;
                Invoke(app, "OpenTopUp1104");
                yield return new WaitForSecondsRealtime(.8f);
                Shot(camera, rt, "topup-shop.png");
                Button buy = canvas.GetComponentsInChildren<Button>(true).First(b => b.name == "TopUpBuy-diamond-60");
                int diamonds = ((GameModel)typeof(ChoSirenApp).GetField("model", Private).GetValue(app)).Save.Diamonds;
                buy.onClick.Invoke();
                yield return new WaitForSecondsRealtime(.4f);
                Shot(camera, rt, "topup-tap.png");
                Assert.That(((GameModel)typeof(ChoSirenApp).GetField("model", Private).GetValue(app)).Save.Diamonds, Is.EqualTo(diamonds));
            }
            finally
            {
                camera.targetTexture = null;
                rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
            }
        }

        private static void Shot(Camera camera, RenderTexture rt, string name)
        {
            foreach (Canvas stray in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!stray.isRootCanvas || stray.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                stray.renderMode = RenderMode.ScreenSpaceCamera; stray.worldCamera = camera; stray.planeDistance = 5;
            }
            Canvas.ForceUpdateCanvases();
            camera.Render();
            var previous = RenderTexture.active; RenderTexture.active = rt;
            var shot = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            shot.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); shot.Apply();
            RenderTexture.active = previous;
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/qa-logs/topup-1104"));
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, name), shot.EncodeToPNG());
            UnityEngine.Object.Destroy(shot);
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
