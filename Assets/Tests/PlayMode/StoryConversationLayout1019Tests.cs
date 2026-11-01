using System;
using System.Collections;
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
    public sealed class StoryConversationLayout1019Tests
    {
        private const string Out = "/Users/bojuejun/Documents/Codex/2026-10-09/kan/work/story-layout-20261019/runtime";
        private GameObject host;
        private Camera camera;
        private RenderTexture target;
        private GameModel model;
        private Canvas canvas;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("StoryConversationCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            camera = new GameObject("StoryConversationCamera", typeof(Camera)).GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.orthographic = true;
            canvas = host.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 30000;
            CanvasScaler scaler = host.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720, 1536);
            scaler.matchWidthOrHeight = 0f;
            ResizeTarget(720, 1536);
            model = new GameModel(() => new DateTime(2026, 10, 19, 12, 0, 0), new InMemorySaveStore());
        }

        [TearDown]
        public void TearDown()
        {
            if (host != null) Object.DestroyImmediate(host);
            if (camera != null) Object.DestroyImmediate(camera.gameObject);
            if (target != null) { target.Release(); Object.DestroyImmediate(target); }
        }

        private void ResizeTarget(int width, int height)
        {
            if (target != null) { camera.targetTexture = null; target.Release(); Object.DestroyImmediate(target); }
            target = new RenderTexture(width, height, 24);
            camera.targetTexture = target;
            Canvas.ForceUpdateCanvases();
        }

        private StoryPanel Open(StoryScript script, Action finished = null, Action back = null)
        {
            StoryPanel panel = StoryPanel.Open(host.transform, model, new StoryRunner(script), finished, back: back);
            Canvas.ForceUpdateCanvases();
            return panel;
        }

        private static Transform Find(StoryPanel panel, string name) => panel.GetComponentsInChildren<Transform>(true)
            .Single(item => item.name == name);
        private static void Call(StoryPanel panel, string method, params object[] args) => typeof(StoryPanel)
            .GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(panel, args);
        private static void Click(StoryPanel panel, string name) => Find(panel, name).GetComponent<Button>().onClick.Invoke();
        private static void Next(StoryPanel panel) { Call(panel, "StopTyping", true); Click(panel, "DialogueBox"); }

        private static StoryScript Speakers()
        {
            var script = new StoryScript { Id = "speaker-switch", Title = "讲者对应" };
            script.Lines.Add(new StoryLine { Command = StoryCommand.Background, Subject = "Art/Story/Scenes1019/stage-1-1" });
            script.Lines.Add(new StoryLine { Command = StoryCommand.Show, Subject = "xingli", Position = "center" });
            foreach (string id in new[] { "xingli", "producer", "feiyin", "wubai", "yeying" })
                script.Lines.Add(new StoryLine { Subject = id, Text = "这一次，我们一起登上舞台。", Expression = "neutral" });
            script.Lines.Add(new StoryLine { Text = "霓虹映在安静的街道上。" });
            script.Lines.Add(new StoryLine { Command = StoryCommand.End });
            return script;
        }

        [TestCase(0)]
        [TestCase(7)]
        [TestCase(12)]
        public void ProducerDialogueUsesTheSavedNameAndAvatarAfterProfileChanges(int avatar)
        {
            Assert.That(model.TrySetPlayerProfile("林夏", avatar, null, out _), Is.True);
            var script = new StoryScript { Id = "producer-profile", Title = "档案同步" };
            script.Lines.Add(new StoryLine { Subject = "producer", Text = "第一句。" });
            script.Lines.Add(new StoryLine { Subject = "producer", Text = "第二句。" });
            StoryPanel panel = Open(script);
            var portrait = Find(panel, "ProducerAvatarFrame").Find("Mask/Avatar").GetComponent<Image>();
            Assert.That(portrait.sprite, Is.Not.Null);
            Assert.That(portrait.sprite.texture, Is.SameAs(Resources.Load<Texture2D>(model.PlayerAvatarResource)));
            Assert.That(Find(panel, "SpeakerName").GetComponent<Text>().text, Is.EqualTo("林夏"));
            Assert.That(model.TrySetPlayerProfile("江野", 3, null, out _), Is.True);
            Next(panel);
            Assert.That(portrait.sprite.texture, Is.SameAs(Resources.Load<Sprite>(model.PlayerAvatarResource).texture));
            Assert.That(Find(panel, "SpeakerName").GetComponent<Text>().text, Is.EqualTo("江野"));
        }

        [UnityTest]
        public IEnumerator PrologueAlarmUsesAnIntegratedSceneWithoutFloatingPortraits()
        {
            var script = JsonUtility.FromJson<StoryScript>(Resources.Load<TextAsset>("Data/Story/prologue").text);
            // Enter the actual alarm branch with the actor still present, as after either choice.
            script.Lines[0] = new StoryLine { Command = StoryCommand.Show, Subject = "xingli", Position = "center" };
            script.Lines[1] = new StoryLine { Command = StoryCommand.Jump, Jump = "alarm" };
            StoryPanel panel = Open(script);
            yield return null;
            Call(panel, "StopTyping", true);
            Sprite expected = StoryArt1019.Load("Art/Story/Scenes1019/cg-prologue-l24");
            Assert.That(expected, Is.Not.Null);
            var background = (Image)typeof(StoryPanel).GetField("background", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel);
            var dialogue = script.Lines.Skip(script.IndexOfLabel("alarm"))
                .Where(value => value.Command == StoryCommand.Say).ToArray();
            for (int index = 0; index < dialogue.Length; index++)
            {
                Assert.That(background.sprite.texture, Is.SameAs(expected.texture));
                Assert.That(Find(panel, "Slot-center").gameObject.activeInHierarchy, Is.False);
                Assert.That(Find(panel, "SpeakerPortrait").gameObject.activeInHierarchy, Is.False);
                if (index == 0)
                {
                    yield return new WaitForSecondsRealtime(.3f);
                    Canvas.ForceUpdateCanvases();
                    if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                        Capture("prologue-alarm-integrated-1028.png");
                }
                if (index + 1 < dialogue.Length) Next(panel);
            }
        }

        [UnityTest]
        public IEnumerator SkippingPrologueKeepsCompleteSceneAtTheChoice()
        {
            var script = JsonUtility.FromJson<StoryScript>(Resources.Load<TextAsset>("Data/Story/prologue").text);
            StoryPanel panel = Open(script);
            Click(panel, "Skip"); yield return null;
            Assert.That(Find(panel, "Choice-0").gameObject.activeInHierarchy, Is.True);
            var background = (Image)typeof(StoryPanel).GetField("background", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel);
            Assert.That(background.sprite.texture, Is.SameAs(StoryArt1019.Load("Art/Story/Scenes1019/cg-prologue-l4").texture));
            foreach (string slot in new[] { "Slot-left", "Slot-center", "Slot-right", "SpeakerPortrait" })
                Assert.That(Find(panel, slot).gameObject.activeInHierarchy, Is.False, slot);
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null) Capture("prologue-skip-integrated-099.png");
        }

        [Test]
        public void ForegroundPortraitFollowsRealSpeakerAndProducerNeverBorrowsAnIdol()
        {
            StoryPanel panel = Open(Speakers());
            Image stageActor = Find(panel, "Slot-center").Find("Portrait").GetComponent<Image>();
            Sprite originalStageActor = stageActor.sprite;
            Image foreground = Find(panel, "SpeakerPortrait").GetComponent<Image>();
            foreach (string id in new[] { "xingli", "producer", "feiyin", "wubai", "yeying" })
            {
                Assert.That(panel.SpeakerCharacterId, Is.EqualTo(id));
                if (id == "producer")
                {
                    Assert.That(foreground.sprite, Is.Null);
                    Assert.That(foreground.enabled, Is.False);
                    Assert.That(Find(panel, "ProducerMarker").gameObject.activeInHierarchy, Is.True);
                    Assert.That(Find(panel, "SpeakerName").GetComponent<Text>().text, Is.EqualTo("经纪人"));
                }
                else
                {
                    Assert.That(foreground.sprite, Is.Not.Null);
                    Sprite expected = Resources.Load<Sprite>("Art/Story/Portraits1019/" + id + "-neutral")
                        ?? Resources.Load<Sprite>("Art/Story/Portraits1019/" + id)
                        ?? Resources.Load<Sprite>(GameModel.Members.First(member => member.Id == id).ResourcePath);
                    Assert.That(foreground.sprite.texture, Is.SameAs(expected.texture), "Only the exact speaker's artwork is valid");
                    Assert.That(foreground.preserveAspect, Is.True);
                    Assert.That(foreground.raycastTarget, Is.False);
                    Assert.That(foreground.GetComponentsInParent<RectMask2D>(true), Is.Empty,
                        "The speaking silhouette must not be clipped by a rectangular character frame");
                    Assert.That(Find(panel, "ProducerMarker").gameObject.activeSelf, Is.False);
                }
                Assert.That(stageActor.sprite, Is.SameAs(originalStageActor),
                    "Foreground speaker changes must not silently replace a scene actor");
                Next(panel);
            }
            Assert.That(panel.SpeakerCharacterId, Is.Null);
            Assert.That(Find(panel, "SpeakerPortraitRoot").gameObject.activeSelf, Is.False, "Narration has no false speaker portrait");
        }

        [TestCase(720, 1536)]
        [TestCase(390, 844)]
        [TestCase(320, 568)]
        [TestCase(720, 1000)]
        public void LowerReadingAreaFitsPhoneAndDoesNotCoverTheSpeaker(int width, int height)
        {
            ResizeTarget(width, height);
            StoryPanel panel = Open(Speakers());
            Call(panel, "LayoutConversation");
            Canvas.ForceUpdateCanvases();
            var reading = (RectTransform)Find(panel, "DialogueBox");
            var viewport = (RectTransform)Find(panel, "DialogueReadingArea");
            var portrait = (RectTransform)Find(panel, "SpeakerPortraitRoot");
            Rect body = LocalBounds(viewport, (RectTransform)panel.transform);
            Rect bust = LocalBounds(portrait, (RectTransform)panel.transform);
            Rect root = ((RectTransform)panel.transform).rect;
            Rect box = LocalBounds(reading, (RectTransform)panel.transform);
            Assert.That(reading.anchorMin.y, Is.Zero);
            Assert.That(reading.anchorMax.y, Is.Zero);
            Assert.That(reading.anchoredPosition.x, Is.GreaterThan(250f));
            Assert.That(body.xMin, Is.GreaterThanOrEqualTo(bust.xMax + 10f), "Text must never sit over the large speaking bust");
            Assert.That(body.yMin, Is.GreaterThanOrEqualTo(root.yMin));
            Assert.That(body.xMax, Is.LessThanOrEqualTo(root.xMax));
            Assert.That(box.xMax, Is.LessThanOrEqualTo(root.xMax), "The reading frame itself must also stay on the actual canvas");
            Assert.That(reading.rect.width, Is.LessThanOrEqualTo(424f));
            if (root.width >= 720f)
                Assert.That(reading.rect.width, Is.EqualTo(424f).Within(.01f), "A full design-width canvas keeps the authored reading measure");
            Text text = Find(panel, "DialogueText").GetComponent<Text>();
            Assert.That(text.fontSize, Is.GreaterThanOrEqualTo(26));
            Assert.That(text.resizeTextForBestFit, Is.False, "Long dialogue must scroll rather than become tiny");
            Image top = Find(panel, "TopBar").GetComponent<Image>();
            Assert.That(top.rectTransform.rect.height, Is.LessThanOrEqualTo(72f));
            Assert.That(top.color.a, Is.LessThan(.5f), "Top controls should leave the scene visible");
            Assert.That(LocalBounds((RectTransform)Find(panel, "History"), (RectTransform)panel.transform).xMax,
                Is.LessThanOrEqualTo(root.xMax), "History must remain reachable on a narrower actual canvas");
        }

        [Test]
        public void SkipStopsAtChoiceAndCompletionAndBackRemainExactlyOnce()
        {
            var script = new StoryScript { Id = "choices", Title = "选择与返回" };
            script.Lines.Add(new StoryLine { Subject = "xingli", Text = "第一句" });
            script.Lines.Add(new StoryLine { Subject = "feiyin", Text = "第二句" });
            var choice = new StoryLine { Command = StoryCommand.Choice, Text = "要去哪里？" };
            choice.Choices.Add(new StoryChoice { Text = "去码头", Jump = "dock", SetFlag = "story-ui-dock" });
            choice.Choices.Add(new StoryChoice { Text = "留在这里", Jump = "stay" });
            script.Lines.Add(choice);
            script.Lines.Add(new StoryLine { Label = "dock", Subject = "wubai", Text = "港口见。" });
            script.Lines.Add(new StoryLine { Command = StoryCommand.Jump, Jump = "finish" });
            script.Lines.Add(new StoryLine { Label = "stay", Subject = "yeying", Text = "那就继续排练。" });
            script.Lines.Add(new StoryLine { Label = "finish", Command = StoryCommand.End });
            int finished = 0, back = 0;
            StoryPanel panel = Open(script, () => finished++, () => back++);
            Click(panel, "Skip");
            Assert.That(panel.Ended, Is.False);
            Assert.That(Find(panel, "Choice-0").gameObject.activeInHierarchy, Is.True);
            Assert.That(panel.History.Any(line => line.Contains("第二句")), Is.True);
            Assert.That(finished, Is.Zero, "Skip must not choose an answer for the player");
            Click(panel, "Choice-0");
            Assert.That(panel.SpeakerCharacterId, Is.EqualTo("wubai"));
            Click(panel, "Skip");
            Assert.That(panel.Ended, Is.True);
            Assert.That(finished, Is.EqualTo(1));
            Click(panel, "Skip");
            Assert.That(finished, Is.EqualTo(1));
            Click(panel, "Back");
            Assert.That(back, Is.Zero, "Completed scenes close themselves; Back must not fire a second callback.");
            Assert.That(panel.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator HistoryPausesAutoAndClosingHistoryResumesWithoutSkippingAChoice()
        {
            var script = new StoryScript { Id = "auto-history", Title = "自动与记录" };
            script.Lines.Add(new StoryLine { Subject = "xingli", Text = "看这里。" });
            script.Lines.Add(new StoryLine { Subject = "feiyin", Text = "到我了。" });
            var choice = new StoryLine { Command = StoryCommand.Choice };
            choice.Choices.Add(new StoryChoice { Text = "继续" });
            choice.Choices.Add(new StoryChoice { Text = "稍后决定" });
            script.Lines.Add(choice);
            script.Lines.Add(new StoryLine { Command = StoryCommand.End });
            StoryPanel panel = Open(script);
            Call(panel, "StopTyping", true);
            Click(panel, "AutoPlay");
            Click(panel, "History");
            int count = panel.History.Count;
            yield return new WaitForSecondsRealtime(1.8f);
            Assert.That(panel.SpeakerCharacterId, Is.EqualTo("xingli"));
            Assert.That(panel.History.Count, Is.EqualTo(count), "Reading history must not advance the scene behind it");
            Click(panel, "HistoryClose");
            yield return new WaitForSecondsRealtime(1.8f);
            Assert.That(panel.SpeakerCharacterId, Is.EqualTo("feiyin"));
            yield return new WaitForSecondsRealtime(1.8f);
            Assert.That(Find(panel, "Choice-0").gameObject.activeInHierarchy, Is.True);
            Assert.That(Find(panel, "Choice-1").gameObject.activeInHierarchy, Is.True);
            yield return new WaitForSecondsRealtime(1.8f);
            Assert.That(panel.Ended, Is.False, "Auto mode must wait for the player at a choice");
        }

        [UnityTest]
        public IEnumerator LongDialogueRemainsLargeAndScrollsToItsEnd()
        {
            var script = new StoryScript { Id = "long-reading", Title = "长台词" };
            string longLine = string.Concat(Enumerable.Repeat("霓虹与歌声都留在我们的舞台上。", 12));
            script.Lines.Add(new StoryLine { Subject = "xingli", Text = longLine });
            StoryPanel panel = Open(script);
            Call(panel, "StopTyping", true);
            yield return null;
            Canvas.ForceUpdateCanvases();
            ScrollRect scroll = Find(panel, "DialogueReadingArea").GetComponent<ScrollRect>();
            Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height));
            scroll.verticalNormalizedPosition = 1f;
            Vector2 top = scroll.content.anchoredPosition;
            scroll.verticalNormalizedPosition = 0f;
            Canvas.ForceUpdateCanvases();
            Assert.That(Vector2.Distance(top, scroll.content.anchoredPosition), Is.GreaterThan(50f));
            Assert.That(Find(panel, "DialogueText").GetComponent<Text>().fontSize, Is.EqualTo(28));
        }

        [UnityTest]
        public IEnumerator RenderActualSpeakerAndProducerCompositions()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("A graphics device is required to produce actual scene screenshots");
            StoryPanel panel = Open(Speakers());
            foreach (string id in new[] { "xingli", "producer", "feiyin", "wubai", "yeying" })
            {
                yield return new WaitForSecondsRealtime(.3f);
                Call(panel, "StopTyping", true);
                Canvas.ForceUpdateCanvases();
                Capture("story-" + id + "-720x1536.png");
                Next(panel);
            }
        }

        private static Rect LocalBounds(RectTransform rect, RectTransform root)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            Vector2 lower = root.InverseTransformPoint(corners[0]);
            Vector2 upper = root.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(lower.x, lower.y, upper.x, upper.y);
        }

        private void Capture(string file)
        {
            camera.Render();
            RenderTexture old = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            try
            {
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
                Assert.That(image.GetPixels32().Count(pixel => pixel.r > 15 || pixel.g > 15 || pixel.b > 15),
                    Is.GreaterThan(target.width * target.height / 4), "Rendered story must contain actual visible art");
                Directory.CreateDirectory(Out); File.WriteAllBytes(Path.Combine(Out, file), image.EncodeToPNG());
            }
            finally { RenderTexture.active = old; Object.DestroyImmediate(image); }
        }
    }
}
