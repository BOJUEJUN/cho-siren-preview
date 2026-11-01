using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ChoSiren.Tests
{
    /// <summary>
    /// 2026-11-01 (赵金 lobby batch 2, refs/02 "这个对话框有点太挡了"): every lobby idol with her speech box,
    /// plus a clean 2 px/unit (Retina web) capture of each idol for the clarity comparison, into
    /// Builds/qa-logs/zj-1101/lobby-fix/. The layout test asserts the box clears each idol's face, the
    /// top-left record bar and the right-hand buttons.
    /// </summary>
    public sealed class LobbyDialogue1101Tests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly string[] Names = { "catalena", "lilith", "amethyst", "coralie" };
        private ChoSirenApp app;
        private string save, legacy;
        private bool had, hadLegacy;

        private static string Out => Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/qa-logs/zj-1101/lobby-fix"));
        private static string Prefix => System.Environment.GetEnvironmentVariable("LOBBY1101_SHOT") ?? "shot";
        private GameObject Home => GameObject.Find("PsdHome20260921");
        private PsdHomeCharacterCarousel Carousel => Home.GetComponent<PsdHomeCharacterCarousel>();

        [UnitySetUp] public IEnumerator SetUp()
        {
            had = PlayerPrefs.HasKey(GameModel.SaveKey); save = PlayerPrefs.GetString(GameModel.SaveKey);
            hadLegacy = PlayerPrefs.HasKey(GameModel.LegacySaveKey); legacy = PlayerPrefs.GetString(GameModel.LegacySaveKey);
            PlayerPrefs.SetString(GameModel.SaveKey, JsonUtility.ToJson(new GameSave { HomeCharacterIndex = 0, StoryProgress = 100 }));
            foreach (var old in Object.FindObjectsByType<ChoSirenApp>(FindObjectsSortMode.None)) Object.Destroy(old.gameObject);
            yield return null;
            app = new GameObject("LobbyDialogue1101").AddComponent<ChoSirenApp>();
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

        [UnityTest] public IEnumerator CaptureEachIdolWithDialogueAndClean()
        {
            var camera = new GameObject("Lobby1101Camera", typeof(Camera)).GetComponent<Camera>();
            var phone = new RenderTexture(1080, 2288, 24);
            var retina = new RenderTexture(1440, 3050, 24);
            var report = new StringBuilder();
            try
            {
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.orthographic = true; camera.targetTexture = phone;
                var canvas = app.GetComponentInChildren<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
                PinPortrait(canvas);
                yield return null; Canvas.ForceUpdateCanvases();
                typeof(ChoSirenApp).GetMethod("ShowScreenSafely", Private)?.Invoke(app, new object[] { "lobby" });
                yield return null;
                typeof(ChoSirenApp).GetMethod("CloseModal", Private)?.Invoke(app, null);
                yield return new WaitForSecondsRealtime(1.2f);
                Assert.That(Home, Is.Not.Null);
                var motion = Home.GetComponent<PsdHomeMotion>();
                var idols = Home.GetComponent<PsdHomeIdolVideo1025>();
                for (int i = 0; i < Names.Length; i++)
                {
                    if (i > 0) { Carousel.Switch(1); yield return new WaitForSecondsRealtime(.6f); }
                    Assert.That(Carousel.Index, Is.EqualTo(i));
                    float until = Time.realtimeSinceStartup + 8f;
                    while (idols != null && !idols.Showing && Time.realtimeSinceStartup < until) yield return null;
                    yield return new WaitForSecondsRealtime(.6f);
                    bool video = idols != null && idols.Showing;
                    motion.CancelCharacterInteraction();
                    yield return null;
                    camera.targetTexture = retina; canvas.worldCamera = camera;
                    yield return null;
                    Capture(camera, retina, $"{Prefix}-{i}-{Names[i]}-clean.png");
                    camera.targetTexture = phone;
                    yield return null;
                    // The real tap path: the idol swaps to her second clip and says her line.
                    typeof(ChoSirenApp).GetMethod("ShowPsdHomeDialogue", Private).Invoke(app, new object[] { 0 });
                    yield return new WaitForSecondsRealtime(.9f);
                    Capture(camera, phone, $"{Prefix}-{i}-{Names[i]}-dialogue.png");
                    yield return new WaitForSecondsRealtime(1.1f);
                    Capture(camera, phone, $"{Prefix}-{i}-{Names[i]}-dialogue-b.png");
                    var box = (RectTransform)Home.transform.Find("PsdCharacterDialogue");
                    report.AppendLine($"{Names[i]} video={video} dialogue={StageRect(box)}");
                }
            }
            finally
            {
                Object.DestroyImmediate(camera.gameObject);
                phone.Release(); Object.DestroyImmediate(phone);
                retina.Release(); Object.DestroyImmediate(retina);
                Directory.CreateDirectory(Out);
                File.WriteAllText(Path.Combine(Out, Prefix + "-report.txt"), report.ToString());
            }
        }

        /// <summary>
        /// Same idol, same clip frame, three builds of the idle clip: 0.9.19 online (720-wide colour, bilinear),
        /// the 0.9.20 native-width export (bilinear) and this batch (native width, 12 Mbps, Catmull-Rom).
        /// Retina web scale (2 px per stage unit). Faces are cropped and compared afterwards.
        /// </summary>
        [UnityTest] public IEnumerator CaptureIdleClipClarityBeforeAfter()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var camera = new GameObject("Lobby1101ClarityCamera", typeof(Camera)).GetComponent<Camera>();
            var retina = new RenderTexture(1440, 3050, 24);
            try
            {
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.orthographic = true; camera.targetTexture = retina;
                var canvas = app.GetComponentInChildren<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
                PinPortrait(canvas);
                yield return new WaitForSecondsRealtime(1f);
                var idols = Home.GetComponent<PsdHomeIdolVideo1025>();
                foreach (int idol in new[] { 2, 3 })
                {
                    while (Carousel.Index != idol) { Carousel.Switch(1); yield return new WaitForSecondsRealtime(.6f); }
                    float until = Time.realtimeSinceStartup + 8f;
                    while (!idols.Showing && Time.realtimeSinceStartup < until) yield return null;
                    Assert.That(idols.Showing, Is.True, "idle clip did not start");
                    var clips = (System.Array)typeof(PsdHomeIdolVideo1025).GetField("clips", Private).GetValue(idols);
                    object clip = clips.GetValue(0);
                    var player = (UnityEngine.Video.VideoPlayer)clip.GetType().GetField("Player").GetValue(clip);
                    var image = (RawImage)clip.GetType().GetField("Image").GetValue(clip);
                    string file = (idol == 2 ? "elf" : "mermaid") + "-a.mp4";
                    var variants = new (string tag, string path, float bicubic)[]
                    {
                        ("0919", "Builds/qa-logs/clarity-20261101/before/" + file, 0f),
                        ("0920", "Builds/qa-logs/zj-1101/lobby-fix/backup-0.9.20/" + file, 0f),
                        ("final", "Assets/StreamingAssets/LobbyIdols/" + file, 1f),
                    };
                    foreach (var variant in variants)
                    {
                        string path = Path.Combine(root, variant.path);
                        if (!File.Exists(path)) continue;
                        image.material.SetFloat("_Bicubic", variant.bicubic);
                        player.Stop();
                        player.url = new System.Uri(path).AbsoluteUri;
                        player.Prepare();
                        until = Time.realtimeSinceStartup + 8f;
                        while (!player.isPrepared && Time.realtimeSinceStartup < until) yield return null;
                        player.Play();
                        yield return new WaitForSecondsRealtime(.4f);
                        player.Pause();
                        bool sought = false;
                        UnityEngine.Video.VideoPlayer.EventHandler done = _ => sought = true;
                        player.seekCompleted += done;
                        player.frame = 30;
                        until = Time.realtimeSinceStartup + 4f;
                        while (!sought && Time.realtimeSinceStartup < until) yield return null;
                        player.seekCompleted -= done;
                        yield return new WaitForSecondsRealtime(.5f);
                        Capture(camera, retina, $"clarity-{(idol == 2 ? "amethyst" : "coralie")}-{variant.tag}.png");
                    }
                    image.material.SetFloat("_Bicubic", 1f);
                }
            }
            finally
            {
                Object.DestroyImmediate(camera.gameObject);
                retina.Release(); Object.DestroyImmediate(retina);
            }
        }

        [UnityTest] public IEnumerator DialogueClearsEachFaceTheRecordBarAndButtons()
        {
            Assert.That(Home, Is.Not.Null);
            var motion = Home.GetComponent<PsdHomeMotion>();
            var box = (RectTransform)Home.transform.Find("PsdCharacterDialogue");
            Assert.That(box.rect.width, Is.LessThanOrEqualTo(540f * .7f), "about a third smaller than the 10-31 box");
            Assert.That(box.rect.height, Is.LessThanOrEqualTo(108f * .75f));
            // No line leaves a lone glyph or punctuation mark on its second row.
            var parts = (PsdHomeDialogue1031.Parts)typeof(PsdHomeMotion).GetField("dialogue", Private).GetValue(motion);
            const BindingFlags Statics = BindingFlags.Static | BindingFlags.NonPublic;
            var all = new System.Collections.Generic.List<string>((string[])typeof(PsdHomeMotion).GetField("Lines", Statics).GetValue(null));
            all.AddRange((string[])typeof(PsdHomeMotion).GetField("LilithLines", Statics).GetValue(null));
            foreach (string[] guest in (string[][])typeof(PsdHomeMotion).GetField("GuestLines", Statics).GetValue(null)) all.AddRange(guest);
            foreach (string raw in all)
            {
                string text = raw.Replace("{player}", "经纪人小王");
                parts.Line.text = text;
                PsdHomeDialogue1031.FitLine(parts);
                Assert.That(parts.Line.fontSize, Is.GreaterThanOrEqualTo(PsdHomeDialogue1031.LineMinSize), text);
                var settings = parts.Line.GetGenerationSettings(parts.Line.rectTransform.rect.size);
                var generator = new TextGenerator();
                generator.Populate(text, settings);
                if (generator.lineCount > 1)
                    Assert.That(text.Substring(generator.lines[generator.lineCount - 1].startCharIdx).Trim().Length,
                        Is.GreaterThanOrEqualTo(3), text + " " + Diagnose(parts.Line, text));
                Assert.That(generator.lineCount, Is.LessThanOrEqualTo(2), text);
            }
            motion.CancelCharacterInteraction();
            for (int i = 0; i < Names.Length; i++)
            {
                if (i > 0) { Carousel.Switch(1); yield return new WaitForSecondsRealtime(.6f); }
                Assert.That(Carousel.Index, Is.EqualTo(i));
                Speak(motion, i);
                yield return new WaitForSecondsRealtime(.4f);
                Canvas.ForceUpdateCanvases();
                Rect body = StageRect(box);
                // The slanted name plate (and its drop shadow) rises above the box's top-left edge.
                float plateLeft = body.xMin + PsdHomeDialogueFrame1031.TagX - 3f;
                Rect plate = Rect.MinMaxRect(plateLeft, body.yMin + PsdHomeDialogueFrame1031.TagY - 3f,
                    plateLeft + parts.Frame.TagWidth + PsdHomeDialogueFrame1031.TagSlant + 10f, body.yMin + 6f);
                Rect area = Rect.MinMaxRect(body.xMin, plate.yMin, body.xMax, body.yMax);
                Rect face = PsdHomeDialogue1031.FaceRects[i];
                Assert.That(body.Overlaps(face), Is.False, $"{Names[i]}: box {body} covers face {face}");
                Assert.That(plate.Overlaps(face), Is.False, $"{Names[i]}: name plate {plate} covers face {face}");
                Assert.That(area.xMin, Is.GreaterThanOrEqualTo(0f)); Assert.That(area.xMax, Is.LessThanOrEqualTo(720f));
                foreach (string name in new[] { "MusicNext", "Settings", "Mail", "HideUi", "PreviousCharacter",
                             "NextCharacter", "Tasks", "LiveOnStage" })
                {
                    var other = Home.transform.Find(name) as RectTransform;
                    if (other == null) continue;
                    foreach (var part in other.GetComponentsInChildren<RectTransform>())
                    {
                        if (part.rect.width <= 1f || part.rect.height <= 1f) continue;
                        // The control's own hit rect, anything tappable inside it, and every visible piece of
                        // the top-left record bar; decorative ink splashes around the cards are not controls.
                        if (part != other && !(part.TryGetComponent(out Graphic graphic) &&
                                               (graphic.raycastTarget || name == "MusicNext"))) continue;
                        Rect r = StageRect(part);
                        Assert.That(area.Overlaps(r), Is.False, $"{Names[i]}: box {area} overlaps {part.name} {r}");
                    }
                }
            }
        }

        [UnityTest] public IEnumerator TapClosesTheShownLineEarlyAndHiddenBoxNeverBlocks()
        {
            var motion = Home.GetComponent<PsdHomeMotion>();
            var box = Home.transform.Find("PsdCharacterDialogue");
            var group = box.GetComponent<CanvasGroup>();
            yield return null;
            Assert.That(group.blocksRaycasts, Is.False);
            motion.DisplayDialogue(0);
            yield return new WaitForSecondsRealtime(.5f);
            Assert.That(group.alpha, Is.GreaterThan(.8f));
            Assert.That(group.blocksRaycasts, Is.True);
            var data = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current);
            UnityEngine.EventSystems.ExecuteEvents.Execute(box.gameObject, data,
                UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
            yield return new WaitForSecondsRealtime(.35f);
            Assert.That(group.alpha, Is.LessThan(.05f));
            Assert.That(group.blocksRaycasts, Is.False);
            // Shorter hold than the old 4.5 s, still long enough to read a line.
            Assert.That(PsdHomeDialogue1031.HoldSeconds("月光下的旋律，只唱给你听。"), Is.InRange(2.6f, 3.6f));
            motion.DisplayDialogue(1);
            yield return new WaitForSecondsRealtime(3.9f);
            Assert.That(group.alpha, Is.LessThan(.05f), "the line closes on its own before the old 4.5 s");
        }

        private static string Diagnose(Text line, string text)
        {
            var sb = new StringBuilder($"rect={line.rectTransform.rect.size} ppu={line.pixelsPerUnit} best={line.resizeTextForBestFit} size={line.fontSize};");
            bool best = line.resizeTextForBestFit; int keep = line.fontSize;
            line.resizeTextForBestFit = false;
            for (int points = PsdHomeDialogue1031.LineSize; points >= PsdHomeDialogue1031.LineMinSize; points--)
            {
                line.fontSize = points;
                var settings = line.GetGenerationSettings(line.rectTransform.rect.size);
                var generator = new TextGenerator();
                float h = generator.GetPreferredHeight(text, settings) / line.pixelsPerUnit;
                sb.Append($" {points}:h={h:0.0},rows={generator.lineCount},tail={(generator.lineCount > 0 ? generator.lines[generator.lineCount - 1].startCharIdx : -1)}");
            }
            line.resizeTextForBestFit = best; line.fontSize = keep;
            return sb.ToString();
        }

        private static void Speak(PsdHomeMotion motion, int index)
        {
            if (index == 0) motion.DisplayDialogue(0);
            else if (index == 1) motion.DisplayLilithLine();
            else motion.DisplayGuestLine(index);
        }

        private static void PinPortrait(Canvas canvas)
        {
            // Batchmode reports a 640×480 landscape screen; pin the portrait phone layout the
            // capture represents (full-width safe area, width-matched scaler).
            foreach (var fitter in canvas.GetComponentsInChildren<SafeAreaFitter>(true))
            {
                fitter.enabled = false;
                var safe = (RectTransform)fitter.transform;
                safe.anchorMin = Vector2.zero; safe.anchorMax = Vector2.one;
                safe.offsetMin = safe.offsetMax = Vector2.zero;
            }
            canvas.GetComponent<CanvasScaler>().matchWidthOrHeight = 0f;
        }

        private Rect StageRect(RectTransform rect)
        {
            var stage = (RectTransform)Home.transform;
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
            foreach (Vector3 corner in corners)
            {
                Vector2 local = stage.InverseTransformPoint(corner);
                min = Vector2.Min(min, local); max = Vector2.Max(max, local);
            }
            // Stage space: origin at its top-left, y down (matches PSD/720-unit layout numbers).
            Rect s = stage.rect;
            return Rect.MinMaxRect(min.x - s.xMin, s.yMax - max.y, max.x - s.xMin, s.yMax - min.y);
        }

        private static void Capture(Camera camera, RenderTexture rt, string name)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            Canvas.ForceUpdateCanvases(); camera.Render();
            var old = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
            Directory.CreateDirectory(Out);
            File.WriteAllBytes(Path.Combine(Out, name), tex.EncodeToPNG());
            RenderTexture.active = old; Object.DestroyImmediate(tex);
        }
    }
}
