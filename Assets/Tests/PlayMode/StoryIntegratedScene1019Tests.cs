#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ChoSiren.Panels;
using ChoSiren.Systems.Story;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ChoSiren.Tests
{
    /// <summary>Complete scene paintings and the dialogue portrait are separate presentation layers.</summary>
    public sealed class StoryIntegratedScene1019Tests
    {
        const string Prefix = "Art/Story/Scenes1019/";
        const string Solo = Prefix + "chapter-01-rehearsal-solo";
        const string Arrival = Prefix + "chapter-01-rehearsal-arrival";
        const string Outro = Prefix + "chapter-01-alley-outro";
        const string Alley = Prefix + "stage-1-1";
        const string Xingli = "Art/Story/Portraits1019/xingli";
        const string Feiyin = "Art/Story/Portraits1019/feiyin";
        readonly Dictionary<string, List<Action<Sprite>>> callbacks = new Dictionary<string, List<Action<Sprite>>>();
        readonly List<Object> owned = new List<Object>();
        GameObject host;
        bool hadReduced;
        int reduced;

        [SetUp] public void Before()
        {
            StoryArt1019.ResetEditorFixture(); callbacks.Clear();
            hadReduced = PlayerPrefs.HasKey(BattlePresentationOptions.ReduceMotionKey);
            reduced = PlayerPrefs.GetInt(BattlePresentationOptions.ReduceMotionKey);
            PlayerPrefs.SetInt(BattlePresentationOptions.ReduceMotionKey, 1);
            host = new GameObject("StoryIntegratedSceneFixture", typeof(RectTransform), typeof(Canvas));
            ((RectTransform)host.transform).sizeDelta = new Vector2(720, 1536);
        }

        [TearDown] public void After()
        {
            if (host != null) Object.DestroyImmediate(host);
            StoryArt1019.ResetEditorFixture();
            foreach (Object item in owned) if (item != null) Object.DestroyImmediate(item);
            owned.Clear(); callbacks.Clear();
            if (hadReduced) PlayerPrefs.SetInt(BattlePresentationOptions.ReduceMotionKey, reduced);
            else PlayerPrefs.DeleteKey(BattlePresentationOptions.ReduceMotionKey);
        }

        void Controlled(params string[] keys)
        {
            string manifest = "{\"entries\":[" + string.Join(",", keys.Select((key, i) =>
                "{\"key\":\"" + key + "\",\"file\":\"Story1019/scene-" + i + ".png\"}")) + "]}";
            StoryArt1019.ConfigureEditorStreamingFixture(manifest, "file:///unused", (key, complete) =>
            {
                if (!callbacks.TryGetValue(key, out var list)) callbacks.Add(key, list = new List<Action<Sprite>>());
                if (complete != null) list.Add(complete);
            });
        }

        void Complete(string key, Sprite sprite)
        {
            if (!callbacks.TryGetValue(key, out var list)) return;
            callbacks.Remove(key);
            foreach (var complete in list) complete(sprite);
        }

        Sprite Painting(string name)
        {
            var texture = new Texture2D(12, 18, TextureFormat.RGBA32, false); owned.Add(texture);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 12, 18), Vector2.one * .5f);
            sprite.name = name; owned.Add(sprite); return sprite;
        }

        static Transform Find(StoryPanel panel, string name) => panel.GetComponentsInChildren<Transform>(true).Single(t => t.name == name);
        static UnityEngine.UI.Image ImageOf(StoryPanel panel, string name) => Find(panel, name).GetComponent<UnityEngine.UI.Image>();
        static object Call(StoryPanel panel, string method, params object[] args) => typeof(StoryPanel)
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(panel, args);
        static void Next(StoryPanel panel) { Call(panel, "StopTyping", true); Call(panel, "DialogueClicked"); }
        static void NoStageActors(StoryPanel panel)
        {
            foreach (string slot in new[] { "left", "center", "right" })
                Assert.That(Find(panel, "Slot-" + slot).gameObject.activeInHierarchy, Is.False,
                    "CG/loading/error mode must not place a second full-body actor over the scene.");
        }

        StoryPanel Open(StoryScript script) => StoryPanel.Open(host.transform,
            new GameModel(() => new DateTime(2026, 10, 19), new InMemorySaveStore()), new StoryRunner(script), null);

        static StoryScript Sequence()
        {
            var script = new StoryScript { Id = "complete-scene", Title = "自然场景" };
            script.Lines.Add(new StoryLine { Command = StoryCommand.Background, Subject = Solo });
            script.Lines.Add(new StoryLine { Command = StoryCommand.Show, Subject = "xingli", Expression = "neutral", Position = "center" });
            script.Lines.Add(new StoryLine { Subject = "xingli", Expression = "surprised", Text = "你也没回去吗？" });
            script.Lines.Add(new StoryLine { Command = StoryCommand.Background, Subject = Arrival });
            script.Lines.Add(new StoryLine { Command = StoryCommand.Show, Subject = "feiyin", Expression = "grin", Position = "right" });
            script.Lines.Add(new StoryLine { Subject = "feiyin", Expression = "serious", Text = "楼下的信号又断了。" });
            script.Lines.Add(new StoryLine { Command = StoryCommand.Hide, Subject = "feiyin" });
            script.Lines.Add(new StoryLine { Command = StoryCommand.Background, Subject = Outro });
            script.Lines.Add(new StoryLine { Subject = "producer", Text = "后巷就是这里。" });
            script.Lines.Add(new StoryLine { Command = StoryCommand.Background, Subject = "Art/Story/rehearsal-night" });
            script.Lines.Add(new StoryLine { Subject = "xingli", Text = "继续排练。" });
            script.Lines.Add(new StoryLine { Command = StoryCommand.End });
            return script;
        }

        [Test] public void CompleteSceneKeepsLogicalShowHideAndForegroundSpeakerWithoutDuplicateActors()
        {
            Controlled(Solo, Arrival, Outro, Alley, Xingli, Feiyin);
            var panel = Open(Sequence());
            NoStageActors(panel);
            Assert.That(ImageOf(panel, "Background").sprite, Is.Not.Null, "Pending CG keeps a real empty setting.");
            var starPortrait = Painting("xingli-ui"); Complete(Xingli, starPortrait);
            Assert.That(panel.SpeakerCharacterId, Is.EqualTo("xingli"));
            Assert.That(ImageOf(panel, "SpeakerPortrait").sprite, Is.SameAs(starPortrait));
            var solo = Painting("solo-complete"); Complete(Solo, solo);
            Assert.That(ImageOf(panel, "Background").sprite, Is.SameAs(solo)); NoStageActors(panel);

            Next(panel);
            var demonPortrait = Painting("feiyin-ui"); Complete(Feiyin, demonPortrait);
            Assert.That(panel.SpeakerCharacterId, Is.EqualTo("feiyin"));
            Assert.That(ImageOf(panel, "SpeakerPortrait").sprite, Is.SameAs(demonPortrait));
            NoStageActors(panel);
            var arrival = Painting("arrival-complete"); Complete(Arrival, arrival); NoStageActors(panel);
            Assert.That(ImageOf(panel, "Background").sprite, Is.SameAs(arrival));

            Next(panel);
            Assert.That(panel.SpeakerCharacterId, Is.EqualTo("producer"));
            Assert.That(ImageOf(panel, "SpeakerPortrait").sprite, Is.Null);
            Assert.That(Find(panel, "ProducerMarker").gameObject.activeInHierarchy, Is.True);
            NoStageActors(panel);
            Complete(Outro, Painting("alley-complete"));
            Next(panel);
            Assert.That(Find(panel, "Slot-center").gameObject.activeInHierarchy, Is.True,
                "The previously shown star must return when switching to a classic empty scene.");
            Assert.That(Find(panel, "Slot-right").gameObject.activeInHierarchy, Is.False,
                "A logical hide in a CG scene must persist after classic presentation returns.");
            Assert.That(Find(panel, "Slot-center").Find("Portrait").GetComponent<UnityEngine.UI.Image>().sprite.texture,
                Is.SameAs((Resources.Load<Sprite>(GameModel.Members.First(member => member.Id == "xingli").ResourcePath)
                    ?? Resources.Load<Sprite>("Art/Members/member-xingli")).texture));
            object actor = Call(panel, "FindSlotByCharacter", "xingli");
            Assert.That(actor.GetType().GetField("Expression").GetValue(actor), Is.EqualTo("surprised"),
                "The say expression was retained while the full-body image was hidden.");
        }

        [Test] public void FailedCgKeepsEmptySettingAndShowOrSayCannotReopenTheFullBody()
        {
            Controlled(Solo);
            var panel = Open(Sequence());
            Sprite empty = ImageOf(panel, "Background").sprite;
            Assert.That(empty, Is.Not.Null);
            Complete(Solo, null);
            Call(panel, "ShowCharacter", "xingli", "smile", "center");
            NoStageActors(panel);
            Assert.That(ImageOf(panel, "Background").sprite, Is.SameAs(empty));
            Assert.That(ImageOf(panel, "SpeakerPortrait").sprite, Is.Not.Null,
                "Even when streamed art is missing, the real speaker has its own native fallback.");
            Next(panel); // The arrival key is absent from the manifest, so Request returns null immediately.
            Assert.That(panel.SpeakerCharacterId, Is.EqualTo("feiyin")); NoStageActors(panel);
            Assert.That(ImageOf(panel, "Background").sprite, Is.Not.Null);
        }

        [Test] public void StaleCgAndLateEmptyFallbackCannotCoverANewerSceneOrPainting()
        {
            Controlled(Solo, Arrival, Outro, Alley);
            var panel = Open(Sequence());
            Next(panel);
            Complete(Solo, Painting("late-solo"));
            var arrival = Painting("current-arrival"); Complete(Arrival, arrival);
            Assert.That(ImageOf(panel, "Background").sprite, Is.SameAs(arrival));
            Next(panel);
            Action<Sprite> lateOutro = callbacks[Outro].Last();
            var outro = Painting("current-outro"); Complete(Outro, outro);
            Complete(Alley, Painting("late-empty-alley"));
            Assert.That(ImageOf(panel, "Background").sprite, Is.SameAs(outro),
                "The slow empty fallback must not cover the completed painting.");
            Next(panel);
            Sprite classic = ImageOf(panel, "Background").sprite;
            lateOutro(Painting("stale-outro-again"));
            Assert.That(ImageOf(panel, "Background").sprite, Is.SameAs(classic));
            Assert.That(Find(panel, "Slot-center").gameObject.activeInHierarchy, Is.True);
            Assert.That(ImageOf(panel, "StoryReadabilityVeil").enabled, Is.True);
        }

        [Test] public void IllustratedModeHasNeutralLowerReadingShadeAndClassicEffectsReturnOnlyOnClassicScenes()
        {
            Controlled(Solo);
            var panel = Open(Sequence());
            Complete(Solo, Painting("unfiltered-painting"));
            Assert.That(ImageOf(panel, "Background").color, Is.EqualTo(Color.white));
            Assert.That(ImageOf(panel, "StoryReadabilityVeil").enabled, Is.False);
            Assert.That(ImageOf(panel, "Vignette").enabled, Is.False);
            var shade = ImageOf(panel, "IllustratedReadingShade");
            Assert.That(shade.gameObject.activeInHierarchy, Is.True);
            Assert.That(shade.raycastTarget, Is.False);
            Assert.That(shade.rectTransform.anchorMin.y, Is.Zero);
            Assert.That(shade.rectTransform.anchorMax.y, Is.Zero);
            Assert.That(shade.rectTransform.anchoredPosition.y, Is.Zero);
            Assert.That(shade.rectTransform.rect.height,
                Is.LessThanOrEqualTo(((RectTransform)panel.transform).rect.height * .35f));
            Call(panel, "SetBackground", "Art/Story/rehearsal-night");
            Assert.That(shade.gameObject.activeSelf, Is.False);
            Assert.That(ImageOf(panel, "StoryReadabilityVeil").enabled, Is.True);
            Assert.That(ImageOf(panel, "Vignette").enabled, Is.True);
            Assert.That(Find(panel, "Slot-center").gameObject.activeSelf, Is.True);
        }

        [Test] public void EnvironmentOnlyScenesAreNotMisclassifiedAsPaintingsContainingActors()
        {
            Controlled(Alley);
            var script = new StoryScript { Id = "classic-alley", Title = "空景" };
            script.Lines.Add(new StoryLine { Command = StoryCommand.Background, Subject = Alley });
            script.Lines.Add(new StoryLine { Command = StoryCommand.Show, Subject = "xingli", Position = "left" });
            script.Lines.Add(new StoryLine { Subject = "xingli", Text = "后巷见。" });
            var panel = Open(script);
            Complete(Alley, Painting("environment-only"));
            Assert.That(Find(panel, "Slot-left").gameObject.activeSelf, Is.True);
            Assert.That(ImageOf(panel, "IllustratedReadingShade").gameObject.activeSelf, Is.False);
            Assert.That(ImageOf(panel, "StoryReadabilityVeil").enabled, Is.True);
        }

        [UnityTest] public IEnumerator RenderActualChapterOneSoloArrivalAndOutro()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("A real graphics device is required for scene-painting acceptance.");
            const string output = "/Users/bojuejun/Documents/Codex/2026-10-09/kan/work/story-natural-cg-20261019/runtime";
            var cameraObject = new GameObject("NaturalSceneCamera", typeof(Camera)); owned.Add(cameraObject);
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            camera.orthographic = true;
            var target = new RenderTexture(720, 1536, 24); owned.Add(target); camera.targetTexture = target;
            var canvas = host.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera;
            canvas.planeDistance = 1f; canvas.sortingOrder = 30000;
            var scaler = host.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720, 1536); scaler.matchWidthOrHeight = 0f;
            Canvas.ForceUpdateCanvases();
            var script = JsonUtility.FromJson<StoryScript>(Resources.Load<TextAsset>("Data/Story/chapter-01").text);
            var runner = new StoryRunner(script);
            var panel = StoryPanel.Open(host.transform,
                new GameModel(() => new DateTime(2026, 10, 19), new InMemorySaveStore()), runner, null);
            try
            {
                foreach (var scene in new[] { (key: Solo, name: "solo", speaker: "xingli"),
                                             (key: Arrival, name: "arrival", speaker: "feiyin"),
                                             (key: Outro, name: "outro", speaker: "producer") })
                {
                    Sprite original = Resources.Load<Sprite>(scene.key);
                    Assert.That(original, Is.Not.Null, "The root-generated original must be imported before this test.");
                    int guard = 0;
                    while (ImageOf(panel, "Background").sprite != original && !runner.Finished && guard++ < 40)
                    {
                        if (runner.Current.IsChoice) Find(panel, "Choice-0").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                        else Next(panel);
                    }
                    Assert.That(ImageOf(panel, "Background").sprite, Is.SameAs(original));
                    Assert.That(panel.SpeakerCharacterId, Is.EqualTo(scene.speaker));
                    NoStageActors(panel);
                    Call(panel, "StopTyping", true);
                    yield return null;
                    Canvas.ForceUpdateCanvases(); camera.Render();
                    RenderTexture previous = RenderTexture.active; RenderTexture.active = target;
                    var frame = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
                    try
                    {
                        frame.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); frame.Apply();
                        Assert.That(frame.GetPixels32().Count(pixel => pixel.r > 15 || pixel.g > 15 || pixel.b > 15),
                            Is.GreaterThan(target.width * target.height / 4), "Actual complete scene must render visibly.");
                        Directory.CreateDirectory(output);
                        File.WriteAllBytes(Path.Combine(output, "chapter-01-" + scene.name + "-720x1536.png"), frame.EncodeToPNG());
                    }
                    finally { RenderTexture.active = previous; Object.DestroyImmediate(frame); }
                }
            }
            finally { camera.targetTexture = null; target.Release(); }
        }
    }
}
#endif
