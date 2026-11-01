using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.Video;

namespace ChoSiren.Tests
{
    /// <summary>
    /// 2026-11-01 video-hq (用户: "首页这些角色的动画是不是都是最高清晰度的，不要压缩画质"): every lobby idol's idle
    /// clip through the real playback path (VideoPlayer → RenderTexture → UIStackedAlpha on the lobby canvas), the
    /// same clip frame in three builds — 0.9.19 online (720-wide colour, bilinear), 0.9.21 (VideoToolbox export,
    /// Catmull-Rom) and the x264 CRF 12 re-export — at the phone (1080×2288) and Mac-window (1080×2160) sizes.
    /// Also records what the playback chain renders into (texture size/format/filter, on-screen magnification).
    /// Captures go to Builds/qa-logs/zj-1101b/video-hq/; compose_faces.py builds the face comparison sheets.
    /// </summary>
    public sealed class LobbyVideoQuality1101Tests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly string[] Names = { "catalena", "lilith", "amethyst", "coralie" };
        private static readonly string[] Files = { "catalena-a.mp4", "lilith-a.mp4", "elf-a.mp4", "mermaid-a.mp4" };
        private ChoSirenApp app;
        private string save, legacy;
        private bool had, hadLegacy;

        private static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        private static string Out => Path.Combine(Root, "Builds/qa-logs/zj-1101b/video-hq");
        private GameObject Home => GameObject.Find("PsdHome20260921");
        private PsdHomeCharacterCarousel Carousel => Home.GetComponent<PsdHomeCharacterCarousel>();

        [UnitySetUp] public IEnumerator SetUp()
        {
            had = PlayerPrefs.HasKey(GameModel.SaveKey); save = PlayerPrefs.GetString(GameModel.SaveKey);
            hadLegacy = PlayerPrefs.HasKey(GameModel.LegacySaveKey); legacy = PlayerPrefs.GetString(GameModel.LegacySaveKey);
            PlayerPrefs.SetString(GameModel.SaveKey, JsonUtility.ToJson(new GameSave { HomeCharacterIndex = 0, StoryProgress = 100 }));
            foreach (var old in Object.FindObjectsByType<ChoSirenApp>(FindObjectsSortMode.None)) Object.Destroy(old.gameObject);
            yield return null;
            app = new GameObject("LobbyVideoQuality1101").AddComponent<ChoSirenApp>();
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

        [UnityTest] public IEnumerator CaptureEachIdolClip0919_0921_New()
        {
            var camera = new GameObject("LobbyVideoQualityCamera", typeof(Camera)).GetComponent<Camera>();
            var sizes = new[] { new RenderTexture(1080, 2288, 24), new RenderTexture(1080, 2160, 24) };
            var report = new StringBuilder();
            try
            {
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.orthographic = true; camera.targetTexture = sizes[0];
                var canvas = app.GetComponentInChildren<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
                PinPortrait(canvas);
                yield return null; Canvas.ForceUpdateCanvases();
                typeof(ChoSirenApp).GetMethod("ShowScreenSafely", Private)?.Invoke(app, new object[] { "lobby" });
                yield return null;
                typeof(ChoSirenApp).GetMethod("CloseModal", Private)?.Invoke(app, null);
                yield return new WaitForSecondsRealtime(1.2f);
                Assert.That(Home, Is.Not.Null);
                var idols = Home.GetComponent<PsdHomeIdolVideo1025>();
                Assert.That(idols, Is.Not.Null);
                for (int idol = 0; idol < Names.Length; idol++)
                {
                    while (Carousel.Index != idol) { Carousel.Switch(1); yield return new WaitForSecondsRealtime(.6f); }
                    float until = Time.realtimeSinceStartup + 10f;
                    while (!idols.Showing && Time.realtimeSinceStartup < until) yield return null;
                    Assert.That(idols.Showing, Is.True, Names[idol] + ": idle clip did not start");
                    Home.GetComponent<PsdHomeMotion>().CancelCharacterInteraction();
                    var clips = (System.Array)typeof(PsdHomeIdolVideo1025).GetField("clips", Private).GetValue(idols);
                    object clip = clips.GetValue(0);
                    var player = (VideoPlayer)clip.GetType().GetField("Player").GetValue(clip);
                    var image = (RawImage)clip.GetType().GetField("Image").GetValue(clip);
                    var variants = new (string tag, string path, float bicubic)[]
                    {
                        ("0919", "Builds/qa-logs/clarity-20261101/before/" + Files[idol], 0f),
                        ("0921", "Builds/qa-logs/zj-1101b/video-hq/backup-0.9.21/" + Files[idol], 1f),
                        ("new", "Assets/StreamingAssets/LobbyIdols/" + Files[idol], 1f),
                    };
                    foreach (var variant in variants)
                    {
                        string path = Path.Combine(Root, variant.path);
                        Assert.That(File.Exists(path), Is.True, path);
                        image.material.SetFloat("_Bicubic", variant.bicubic);
                        player.Stop();
                        player.url = new System.Uri(path).AbsoluteUri;
                        player.Prepare();
                        until = Time.realtimeSinceStartup + 10f;
                        while (!player.isPrepared && Time.realtimeSinceStartup < until) yield return null;
                        Assert.That(player.isPrepared, Is.True, path + " did not prepare");
                        player.Play();
                        yield return new WaitForSecondsRealtime(.4f);
                        player.Pause();
                        bool sought = false;
                        VideoPlayer.EventHandler done = _ => sought = true;
                        player.seekCompleted += done;
                        player.frame = 30;
                        until = Time.realtimeSinceStartup + 4f;
                        while (!sought && Time.realtimeSinceStartup < until) yield return null;
                        player.seekCompleted -= done;
                        yield return new WaitForSecondsRealtime(.5f);
                        for (int s = 0; s < sizes.Length; s++)
                        {
                            camera.targetTexture = sizes[s]; canvas.worldCamera = camera;
                            yield return null; yield return null;
                            Capture(camera, sizes[s], $"hq-{sizes[s].width}x{sizes[s].height}-{idol}-{Names[idol]}-{variant.tag}.png");
                            if (variant.tag == "new") report.AppendLine(Describe(Names[idol], sizes[s], player, image, camera));
                        }
                        camera.targetTexture = sizes[0];
                    }
                    image.material.SetFloat("_Bicubic", 1f);
                }
            }
            finally
            {
                Object.DestroyImmediate(camera.gameObject);
                foreach (var rt in sizes) { rt.Release(); Object.DestroyImmediate(rt); }
                Directory.CreateDirectory(Out);
                File.WriteAllText(Path.Combine(Out, "playback-chain.txt"), report.ToString());
            }
        }

        /// <summary>The idle clip's render target and how many screen pixels each video pixel covers.</summary>
        private static string Describe(string name, RenderTexture screen, VideoPlayer player, RawImage image, Camera camera)
        {
            var rt = image.texture as RenderTexture;
            var corners = new Vector3[4];
            image.rectTransform.GetWorldCorners(corners);
            Vector2 a = camera.WorldToScreenPoint(corners[0]), b = camera.WorldToScreenPoint(corners[2]);
            float shownW = Mathf.Abs(b.x - a.x), shownH = Mathf.Abs(b.y - a.y);
            float colourW = player.width / 2f;
            return $"{name} screen={screen.width}x{screen.height} video={player.width}x{player.height} " +
                   $"rt={(rt != null ? $"{rt.width}x{rt.height} {rt.format} graphics={rt.graphicsFormat} sRGB={rt.sRGB} mips={rt.useMipMap} filter={rt.filterMode} aniso={rt.anisoLevel} aa={rt.antiAliasing}" : "none")} " +
                   $"shown={shownW:0}x{shownH:0}px colour-half={colourW:0}x{player.height} magnification={shownW / colourW:0.00}x " +
                   $"canvasScale={image.canvas.scaleFactor:0.###} bicubic={image.material.GetFloat("_Bicubic")} alphaFromSrgb={image.material.GetFloat("_AlphaFromSrgb")}";
        }

        private static void PinPortrait(Canvas canvas)
        {
            // Batchmode reports a 640×480 landscape screen; pin the portrait layout the capture represents.
            foreach (var fitter in canvas.GetComponentsInChildren<SafeAreaFitter>(true))
            {
                fitter.enabled = false;
                var safe = (RectTransform)fitter.transform;
                safe.anchorMin = Vector2.zero; safe.anchorMax = Vector2.one;
                safe.offsetMin = safe.offsetMax = Vector2.zero;
            }
            canvas.GetComponent<CanvasScaler>().matchWidthOrHeight = 0f;
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
