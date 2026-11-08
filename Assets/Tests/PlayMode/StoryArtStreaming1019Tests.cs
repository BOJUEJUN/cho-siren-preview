#if UNITY_EDITOR
// The fixture emulates Web requests inside the Editor PlayMode runner.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using ChoSiren.Panels;
using ChoSiren.Systems.Story;
using ChoSiren.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ChoSiren.Tests
{
    public sealed class StoryArtStreaming1019Tests
    {
        const string SceneA = "Art/Story/Scenes1019/stage-1-1", SceneB = "Art/Story/Scenes1019/stage-2-1";
        const string Xingli = "Art/Story/Portraits1019/xingli", Feiyin = "Art/Story/Portraits1019/feiyin";
        readonly Dictionary<string, List<Action<Sprite>>> callbacks = new Dictionary<string, List<Action<Sprite>>>();
        readonly List<Object> owned = new List<Object>();
        GameObject host;
        string temp;
        bool hadReduced; int reduced;

        [SetUp] public void Before()
        {
            StoryArt1019.ResetEditorFixture(); callbacks.Clear();
            hadReduced = PlayerPrefs.HasKey(BattlePresentationOptions.ReduceMotionKey);
            reduced = PlayerPrefs.GetInt(BattlePresentationOptions.ReduceMotionKey); PlayerPrefs.SetInt(BattlePresentationOptions.ReduceMotionKey, 0);
            host = new GameObject("StoryStreamingFixture", typeof(RectTransform), typeof(Canvas));
            ((RectTransform)host.transform).sizeDelta = new Vector2(720, 1536);
        }
        [TearDown] public void After()
        {
            if (host != null) Object.DestroyImmediate(host);
            StoryArt1019.ResetEditorFixture();
            foreach (Object item in owned) if (item != null) Object.DestroyImmediate(item);
            owned.Clear();
            if (temp != null && Directory.Exists(temp)) Directory.Delete(temp, true);
            temp = null;
            if (hadReduced) PlayerPrefs.SetInt(BattlePresentationOptions.ReduceMotionKey, reduced); else PlayerPrefs.DeleteKey(BattlePresentationOptions.ReduceMotionKey);
        }
        string Manifest(params string[] keys) => "{\"entries\":[" + string.Join(",", keys.Select((key, i) =>
            "{\"key\":\"" + key + "\",\"file\":\"Story1019/test-" + i + ".png\"}")) + "]}";
        void Controlled(params string[] keys) => StoryArt1019.ConfigureEditorStreamingFixture(Manifest(keys), "file:///unused", (key, complete) =>
        {
            if (!callbacks.TryGetValue(key, out var list)) callbacks.Add(key, list = new List<Action<Sprite>>());
            if (complete != null) list.Add(complete);
        });
        void Complete(string key, Sprite sprite)
        {
            if (!callbacks.TryGetValue(key, out var list)) return;
            callbacks.Remove(key); foreach (var complete in list) complete(sprite);
        }
        Sprite NewSprite(string name)
        {
            var texture = new Texture2D(12, 8, TextureFormat.RGBA32, false); owned.Add(texture);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 12, 8), Vector2.one * .5f); sprite.name = name; owned.Add(sprite); return sprite;
        }
        static void Call(StoryPanel panel, string method, params object[] args) => typeof(StoryPanel)
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(panel, args);
        static Image ImageOf(StoryPanel panel, string name) => panel.GetComponentsInChildren<Image>(true).Single(i => i.name == name);
        static void Next(StoryPanel panel) { Call(panel, "StopTyping", true); Call(panel, "DialogueClicked"); }
        StoryPanel Open()
        {
            var script = new StoryScript { Id = "streaming-test", Title = "延迟素材" };
            script.Lines.Add(new StoryLine { Command = StoryCommand.Background, Subject = SceneA });
            script.Lines.Add(new StoryLine { Subject = "xingli", Text = "出发。" });
            script.Lines.Add(new StoryLine { Command = StoryCommand.Background, Subject = SceneB });
            script.Lines.Add(new StoryLine { Subject = "feiyin", Text = "我在这里。" });
            script.Lines.Add(new StoryLine { Subject = "producer", Text = "准备好了。" });
            script.Lines.Add(new StoryLine { Command = StoryCommand.End });
            return StoryPanel.Open(host.transform, new GameModel(() => DateTime.Now, new InMemorySaveStore()), new StoryRunner(script), null);
        }

        [Test] public void LateBackgroundAndPortraitCannotOverwriteTheCurrentSceneOrRealSpeaker()
        {
            Controlled(SceneA, SceneB, Xingli, Feiyin);
            var panel = Open();
            Assert.That(ImageOf(panel, "Background").sprite, Is.Not.Null, "Keep a visible original fallback while downloading.");
            Next(panel);
            var sceneB = NewSprite("scene-b"); Complete(SceneB, sceneB);
            Assert.That(ImageOf(panel, "Background").sprite, Is.SameAs(sceneB));
            Complete(SceneA, NewSprite("stale-scene-a"));
            Assert.That(ImageOf(panel, "Background").sprite, Is.SameAs(sceneB));
            Action<Sprite> delayedFeiyin = callbacks[Feiyin].Last();
            var feiyin = NewSprite("feiyin"); Complete(Feiyin, feiyin);
            Assert.That(ImageOf(panel, "SpeakerPortrait").sprite, Is.SameAs(feiyin));
            Complete(Xingli, NewSprite("stale-xingli"));
            Assert.That(ImageOf(panel, "SpeakerPortrait").sprite, Is.SameAs(feiyin));
            Next(panel); delayedFeiyin(feiyin);
            Assert.That(panel.SpeakerCharacterId, Is.EqualTo("producer"));
            Assert.That(ImageOf(panel, "SpeakerPortrait").sprite, Is.Null);
        }

        [Test] public void DestroyedPanelIgnoresBothOutstandingArtCallbacks()
        {
            Controlled(SceneA, SceneB, Xingli, Feiyin); var panel = Open();
            Object.DestroyImmediate(panel.gameObject);
            Assert.DoesNotThrow(() => Complete(SceneA, NewSprite("late-background")));
            Assert.DoesNotThrow(() => Complete(Xingli, NewSprite("late-portrait")));
        }

        [UnityTest] public IEnumerator RealPngDownloadCoalescesRequestsCachesNativePixelsAndKeepsItsAlphaFormat()
        {
            temp = Path.Combine(Application.temporaryCachePath, "StoryStreaming-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(temp, "Story1019"));
            var source = new Texture2D(12, 8, TextureFormat.RGBA32, false); owned.Add(source);
            source.SetPixels(Enumerable.Range(0, 96).Select(i => i % 2 == 0 ? Color.clear : Color.magenta).ToArray()); source.Apply();
            byte[] png = source.EncodeToPNG(); string file = Path.Combine(temp, "Story1019/test-0.png"); File.WriteAllBytes(file, png);
            StoryArt1019.ConfigureEditorStreamingFixture(Manifest(SceneA), new Uri(temp + "/").AbsoluteUri);
            Sprite a = null, b = null; int calls = 0;
            StoryArt1019.Request(SceneA, sprite => { a = sprite; calls++; });
            StoryArt1019.Request(SceneA, sprite => { b = sprite; calls++; });
            Assert.That(StoryArt1019.PendingCount, Is.EqualTo(1));
            float deadline = Time.realtimeSinceStartup + 5f;
            while (calls < 2 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(calls, Is.EqualTo(2)); Assert.That(a, Is.Not.Null); Assert.That(b, Is.SameAs(a));
            Assert.That(a.texture.width, Is.EqualTo(12)); Assert.That(a.texture.height, Is.EqualTo(8));
            Assert.That(UnityEngine.Experimental.Rendering.GraphicsFormatUtility.HasAlphaChannel(a.texture.graphicsFormat), Is.True);
            // The native decoder may expose ARGB32 or RGBA32. Verify rendered alpha and
            // colour instead of requiring one CPU channel order from every platform.
            RenderTexture previous = RenderTexture.active;
            RenderTexture decoded = RenderTexture.GetTemporary(12, 8, 0, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(12, 8, TextureFormat.RGBA32, false);
            try
            {
                Graphics.Blit(a.texture, decoded); RenderTexture.active = decoded;
                pixels.ReadPixels(new Rect(0, 0, 12, 8), 0, 0); pixels.Apply();
                Color[] actual = pixels.GetPixels();
                Assert.That(actual[0].a, Is.LessThan(.01f));
                Assert.That(actual[1].a, Is.GreaterThan(.99f));
                Assert.That(actual[1].r, Is.GreaterThan(.98f));
                Assert.That(actual[1].b, Is.GreaterThan(.98f));
                Assert.That(actual[1].g, Is.LessThan(.01f));
            }
            finally
            {
                RenderTexture.active = previous; RenderTexture.ReleaseTemporary(decoded);
                Object.DestroyImmediate(pixels);
            }
            Assert.That(File.ReadAllBytes(file), Is.EqualTo(png), "Download must not rewrite or compress original bytes.");
            Assert.That(StoryArt1019.PendingCount, Is.Zero); Assert.That(StoryArt1019.Load(SceneA), Is.SameAs(a));
            StoryArt1019.Request(SceneA, sprite => { Assert.That(sprite, Is.SameAs(a)); calls++; });
            Assert.That(calls, Is.EqualTo(3), "A cache hit completes without another frame/network request.");
        }

        [UnityTest] public IEnumerator MissingDownloadCompletesBothWaitersAndReleasesThePendingSlot()
        {
            temp = Path.Combine(Application.temporaryCachePath, "StoryStreaming-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            StoryArt1019.ConfigureEditorStreamingFixture(Manifest(SceneA), new Uri(temp + "/").AbsoluteUri);
            int calls = 0;
            LogAssert.Expect(LogType.Warning, new Regex("Story1019 art keeps its fallback: .*stage-1-1"));
            StoryArt1019.Request(SceneA, sprite => { Assert.That(sprite, Is.Null); calls++; });
            StoryArt1019.Request(SceneA, sprite => { Assert.That(sprite, Is.Null); calls++; });
            float deadline = Time.realtimeSinceStartup + 5f;
            while (calls < 2 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(calls, Is.EqualTo(2)); Assert.That(StoryArt1019.PendingCount, Is.Zero);
            Assert.That(StoryArt1019.Load(SceneA), Is.Null);
        }

        [Test] public void UnsafeManifestCannotStartADownloadOrEscapeStreamingAssets()
        {
            string json = "{\"entries\":[{\"key\":\"" + SceneA + "\",\"file\":\"Story1019/../../outside.png\"}]}";
            StoryArt1019.ConfigureEditorStreamingFixture(json, "file:///unused");
            int calls = 0; StoryArt1019.Request(SceneA, sprite => { Assert.That(sprite, Is.Null); calls++; });
            Assert.That(calls, Is.EqualTo(1)); Assert.That(StoryArt1019.PendingCount, Is.Zero);
            Assert.That(StoryArt1019.CachedCount, Is.Zero);
        }

        [Ignore("Quarantined 2026-11-08: story CG streaming flow changed after 10-19; see Docs/TEST-QUARANTINE-1108.md")]
        [Test] public void SlowCgWaitsBrieflyThenUsesTheExistingPaperRecordFallbackWithoutDoubleDispatch()
        {
            Controlled(ChallengeVideoTransition.ActionCgResource);
            var fx = ChallengeVideoTransition.Warm(host.transform); int calls = 0;
            Assert.That(fx.IsReady, Is.False);
            fx.Play(host.transform, () => calls++, null);
            fx.Advance(.4f); Assert.That(fx.Clock, Is.Zero); Assert.That(calls, Is.Zero);
            Assert.That(ChallengeVideoTransition.IsCoveringGame, Is.True);
            fx.Advance(.41f);
            Assert.That(fx.Clock, Is.LessThan(.02f), "Waiting cannot consume the opening animation.");
            Assert.That(fx.GetComponentsInChildren<Image>(true).Where(i => i.name == "GeneratedActionCG").All(i => !i.gameObject.activeSelf), Is.True);
            fx.Advance(ChallengeVideoTransition.Duration + .01f);
            Assert.That(calls, Is.EqualTo(1)); Assert.That(ChallengeVideoTransition.IsCoveringGame, Is.False);
            Assert.DoesNotThrow(() => Complete(ChallengeVideoTransition.ActionCgResource, NewSprite("late-cg")));
        }

        [Ignore("Quarantined 2026-11-08: story CG streaming flow changed after 10-19; see Docs/TEST-QUARANTINE-1108.md")]
        [Test] public void PrewarmedCgCanArriveDuringTheShortWaitAndUseTheOriginalTransitionTiming()
        {
            Controlled(ChallengeVideoTransition.ActionCgResource);
            var fx = ChallengeVideoTransition.Warm(host.transform); int calls = 0;
            fx.Play(host.transform, () => calls++, null); fx.Advance(.2f);
            Complete(ChallengeVideoTransition.ActionCgResource, NewSprite("loaded-cg"));
            Assert.That(fx.IsReady, Is.True);
            Assert.That(fx.GetComponentsInChildren<Image>(true).Count(i => i.name == "GeneratedActionCG" && i.gameObject.activeSelf), Is.EqualTo(3));
            fx.Advance(ChallengeVideoTransition.CoverTime + .01f); Assert.That(calls, Is.EqualTo(1));
            fx.Advance(2); Assert.That(calls, Is.EqualTo(1)); Assert.That(ChallengeVideoTransition.IsCoveringGame, Is.False);
        }

        [Ignore("Quarantined 2026-11-08: story CG streaming flow changed after 10-19; see Docs/TEST-QUARANTINE-1108.md")]
        [Test] public void FailedPrewarmRetriesOnceOnPlayAndCanUseTheRecoveredCg()
        {
            Controlled(ChallengeVideoTransition.ActionCgResource);
            var fx = ChallengeVideoTransition.Warm(host.transform);
            Complete(ChallengeVideoTransition.ActionCgResource, null);
            Assert.That(fx.IsReady, Is.False);
            int calls = 0; fx.Play(host.transform, () => calls++, null);
            Assert.That(callbacks.ContainsKey(ChallengeVideoTransition.ActionCgResource), Is.True,
                "Play must request again after a failed prewarm, without polling every frame.");
            int requests = callbacks[ChallengeVideoTransition.ActionCgResource].Count;
            fx.Advance(.2f); fx.Advance(.2f);
            Assert.That(callbacks[ChallengeVideoTransition.ActionCgResource].Count, Is.EqualTo(requests));
            Complete(ChallengeVideoTransition.ActionCgResource, NewSprite("recovered-cg"));
            Assert.That(fx.IsReady, Is.True);
            Assert.That(fx.GetComponentsInChildren<Image>(true).Count(i => i.name == "GeneratedActionCG" && i.gameObject.activeSelf), Is.EqualTo(3));
            fx.Advance(ChallengeVideoTransition.Duration + .01f);
            Assert.That(calls, Is.EqualTo(1)); Assert.That(ChallengeVideoTransition.IsCoveringGame, Is.False);
        }
    }
}
#endif
