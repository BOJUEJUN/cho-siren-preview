using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ChoSiren.Panels;
using ChoSiren.Systems.Economy;
using ChoSiren.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ChoSiren.Tests
{
    /// <summary>
    /// 2026-11-01 (赵金 1101b · edge-fit): screen-edge audit. Every main page is opened on simulated
    /// devices — Mac window 1:2, phone 1080×2288, notched 1080×2340, 9:16, 3:4 tablet and a landscape
    /// web window — and rendered twice with the "gap fillers" (the blurred page ambient, its edge
    /// feathers, the shell backdrop and the camera clear) keyed magenta and then green. Wherever the
    /// two renders differ, the filler shows through: a ring of such pixels along a screen edge is the
    /// "UI does not reach the edge" bug (refs/edge-gap-levelmap.jpg). Also flags buttons pushed off
    /// screen or outside the simulated safe area. Writes half-size captures and a report to
    /// Builds/qa-logs/zj-1101b/edge-fit/&lt;phase&gt;/ (phase from EDGEFIT_PHASE, default "after").
    /// </summary>
    public sealed class EdgeFit1101Tests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        public sealed class Device
        {
            public string Name; public int Width, Height;
            // Simulated system insets in pixels (notch / home indicator).
            public int Top, Bottom;
            public bool Keep; // always save the capture (otherwise only on failure)
            public override string ToString() => Name;
        }

        private static readonly Device[] Devices =
        {
            new Device { Name = "mac-1080x2160", Width = 1080, Height = 2160, Keep = true },
            new Device { Name = "phone-1080x2288", Width = 1080, Height = 2288, Keep = true },
            new Device { Name = "notch-1080x2340", Width = 1080, Height = 2340, Top = 130, Bottom = 94, Keep = true },
            new Device { Name = "wide-1080x1920", Width = 1080, Height = 1920, Keep = true },
            new Device { Name = "tablet-1440x1920", Width = 1440, Height = 1920, Keep = true },
            new Device { Name = "web-1920x1080", Width = 1920, Height = 1080, Keep = true },
        };

        public static IEnumerable<string> DeviceNames => Devices.Select(d => d.Name);

        /// <summary>Page key → how to open it on a freshly started app.</summary>
        private static readonly string[] Pages =
        {
            "title", "lobby", "levelmap-ch1", "levelmap-ch2", "battle", "battle-result", "story",
            "members", "member-detail", "team", "accessory", "audition", "audition-offline",
            "music-collection", "practice", "tasks", "mail", "shop", "settings", "signin", "profile",
        };

        /// <summary>
        /// Pages whose remaining edge issue is owned by another 1101b sub-task (they are rebuilding
        /// the page). Still captured and reported, but they do not fail the regression guard.
        /// </summary>
        private static readonly Dictionary<string, string> OwnedElsewhere = new Dictionary<string, string>
        {
        };

        private static string Phase => Environment.GetEnvironmentVariable("EDGEFIT_PHASE") is string p && p.Length > 0 ? p : "after";
        private static string Out => Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/qa-logs/zj-1101b/edge-fit", Phase));

        private string save, legacy;
        private bool had, hadLegacy;

        [UnitySetUp] public IEnumerator SetUp()
        {
            had = PlayerPrefs.HasKey(GameModel.SaveKey); save = PlayerPrefs.GetString(GameModel.SaveKey);
            hadLegacy = PlayerPrefs.HasKey(GameModel.LegacySaveKey); legacy = PlayerPrefs.GetString(GameModel.LegacySaveKey);
            yield return null;
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            foreach (var old in UnityEngine.Object.FindObjectsByType<ChoSirenApp>(FindObjectsSortMode.None)) UnityEngine.Object.Destroy(old.gameObject);
            yield return null;
            if (had) PlayerPrefs.SetString(GameModel.SaveKey, save); else PlayerPrefs.DeleteKey(GameModel.SaveKey);
            if (hadLegacy) PlayerPrefs.SetString(GameModel.LegacySaveKey, legacy); else PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
            PlayerPrefs.Save();
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator EveryPageReachesTheScreenEdges([ValueSource(nameof(DeviceNames))] string deviceName)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("Needs a graphics device to render the captures.");
            Device device = Devices.First(d => d.Name == deviceName);
            // An audit: page-side error logs are reported by their own tests, not here.
            LogAssert.ignoreFailingMessages = true;
            Directory.CreateDirectory(Out);
            var report = new StringBuilder();
            var failures = new List<string>();
            report.AppendLine($"# edge audit · {device.Name} · phase {Phase}");
            report.AppendLine();
            report.AppendLine("| page | left | top | right | bottom | clipped / outside safe area | verdict |");
            report.AppendLine("|---|---|---|---|---|---|---|");
            foreach (string page in Pages)
            {
                PageResult result = null;
                string error = null;
                IEnumerator run = Flatten(AuditPage(device, page, r => result = r));
                while (true)
                {
                    bool more;
                    try { more = run.MoveNext(); }
                    catch (Exception exception)
                    {
                        error = exception.GetType().Name + ": " + (exception.InnerException ?? exception).Message;
                        (run as IDisposable)?.Dispose();
                        break;
                    }
                    if (!more) break;
                    yield return run.Current;
                }
                foreach (var old in UnityEngine.Object.FindObjectsByType<ChoSirenApp>(FindObjectsSortMode.None)) UnityEngine.Object.Destroy(old.gameObject);
                yield return null;
                if (error != null || result == null)
                {
                    report.AppendLine($"| {page} | – | – | – | – | – | ERROR {error} |");
                    failures.Add($"{page}: could not open ({error})");
                    continue;
                }
                bool owned = OwnedElsewhere.TryGetValue(page, out string owner);
                string verdict = result.Problems.Count == 0 ? "ok" : owned ? "known (" + owner + ")" : "FAIL";
                report.AppendLine($"| {page} | {result.Side(0)} | {result.Side(1)} | {result.Side(2)} | {result.Side(3)} | " +
                    $"{(result.Clipped.Count == 0 ? "–" : string.Join("<br>", result.Clipped))} | {verdict} {string.Join("; ", result.Problems)} |");
                if (result.Problems.Count > 0 && !owned) failures.Add($"{page}: {string.Join("; ", result.Problems)}");
            }
            File.WriteAllText(Path.Combine(Out, $"report-{device.Name}.md"), report.ToString());
            Debug.Log(report.ToString());
            Assert.That(failures, Is.Empty, $"Edge gaps on {device.Name}:\n" + string.Join("\n", failures));
        }

        /// <summary>Steps nested coroutines here, so a page that throws is reported, not fatal.</summary>
        private static IEnumerator Flatten(IEnumerator root)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(root);
            try
            {
                while (stack.Count > 0)
                {
                    IEnumerator top = stack.Peek();
                    if (!top.MoveNext()) { stack.Pop(); continue; }
                    if (top.Current is IEnumerator nested) { stack.Push(nested); continue; }
                    yield return top.Current;
                }
            }
            finally
            {
                while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
            }
        }

        private sealed class PageResult
        {
            // Per side (left, top, right, bottom): share of the outer ring showing the filler, and
            // the median depth (screen px) of the filler band measured inward.
            public readonly float[] Ring = new float[4];
            public readonly int[] Depth = new int[4];
            public readonly bool[] Exempt = new bool[4];
            public readonly List<string> Problems = new List<string>();
            public readonly List<string> Clipped = new List<string>();
            public string Side(int side) => (Exempt[side] ? "column " : "") + (Ring[side] < .005f ? "✓" : $"{Ring[side] * 100f:0}% · {Depth[side]}px");
        }

        // ------------------------------------------------------------------ one page

        private IEnumerator AuditPage(Device device, string page, Action<PageResult> done)
        {
            var saveData = new GameSave { HomeCharacterIndex = 0, StoryProgress = 100, PlayerName = "经纪人" };
            saveData.CompletedStories.Add(GameModel.PrologueStoryId);
            PlayerPrefs.SetString(GameModel.SaveKey, JsonUtility.ToJson(saveData));
            foreach (var old in UnityEngine.Object.FindObjectsByType<ChoSirenApp>(FindObjectsSortMode.None)) UnityEngine.Object.Destroy(old.gameObject);
            yield return null;

            var app = new GameObject("EdgeFit1101").AddComponent<ChoSirenApp>();
            var camera = new GameObject("EdgeFit1101Camera", typeof(Camera)).GetComponent<Camera>();
            camera.transform.SetParent(app.transform, false);
            var rt = new RenderTexture(device.Width, device.Height, 24);
            try
            {
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.orthographic = true; camera.targetTexture = rt;
                Canvas canvas = app.GetComponentInChildren<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10;
                // Batchmode reports a 640×480 screen; lay the safe root out for the simulated device.
                var safeRect = new Rect(0, device.Bottom, device.Width, device.Height - device.Top - device.Bottom);
                SafeAreaFitter.Layout(safeRect, new Vector2(device.Width, device.Height), out Vector2 min, out Vector2 max, out bool wide);
                foreach (var fitter in canvas.GetComponentsInChildren<SafeAreaFitter>(true))
                {
                    fitter.enabled = false;
                    var safe = (RectTransform)fitter.transform;
                    safe.anchorMin = min; safe.anchorMax = max;
                    safe.offsetMin = safe.offsetMax = Vector2.zero;
                }
                canvas.GetComponent<CanvasScaler>().matchWidthOrHeight = wide ? 1f : 0f;
                yield return null; Canvas.ForceUpdateCanvases();
                Invoke(app, "ShowScreenSafely", "lobby");
                yield return null;
                Invoke(app, "CloseModal");
                yield return null; Canvas.ForceUpdateCanvases();

                yield return Open(app, canvas, page);
                float until = Time.realtimeSinceStartup + 1.4f;
                while (Time.realtimeSinceStartup < until) yield return null;
                ConvertStrayCanvases(camera);
                Canvas.ForceUpdateCanvases();
                yield return null;

                var result = new PageResult();
                Color32[] normal = Render(camera, rt, null);
                Color32[] keyA = Render(camera, rt, new Color32(255, 0, 255, 255));
                Color32[] keyB = Render(camera, rt, new Color32(0, 255, 0, 255));
                Measure(device, wide, min, max, keyA, keyB, result);
                CheckButtons(app, canvas, camera, device, min, max, result);
                bool failed = result.Problems.Count > 0;
                if (device.Keep || failed)
                    SaveHalf(normal, device.Width, device.Height, Path.Combine(Out, $"{device.Name}-{page}.jpg"));
                if (failed)
                    SaveMask(keyA, keyB, device.Width, device.Height, Path.Combine(Out, $"{device.Name}-{page}-gaps.jpg"));
                done(result);
            }
            finally
            {
                camera.targetTexture = null;
                rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
            }
        }

        private static IEnumerator Open(ChoSirenApp app, Canvas canvas, string page)
        {
            var safeRoot = (Transform)typeof(ChoSirenApp).GetField("safeRoot", Private).GetValue(app);
            var model = (GameModel)typeof(ChoSirenApp).GetField("model", Private).GetValue(app);
            switch (page)
            {
                case "title":
                    NativeTitleScreen.Create(safeRoot.parent, (RectTransform)safeRoot, false);
                    break;
                case "lobby": break;
                case "levelmap-ch1":
                case "levelmap-ch2":
                    typeof(LevelMapPanel).GetField("lastChapter", BindingFlags.Static | BindingFlags.NonPublic)
                        ?.SetValue(null, page.EndsWith("2") ? 2 : 1);
                    Invoke(app, "OpenLevelMap");
                    break;
                case "battle":
                case "battle-result":
                {
                    // In-game the battle opens over the chapter map (lobby dock hidden).
                    Invoke(app, "OpenLevelMap");
                    yield return null;
                    var battle = model.StartStageBattle("stage-1-1", 1101UL, out string message);
                    Assert.That(battle, Is.Not.Null, message);
                    TacticsBattlePanel panel = TacticsBattlePanel.Open(safeRoot, model, battle, null);
                    if (page == "battle-result")
                    {
                        yield return new WaitForSecondsRealtime(.4f);
                        panel.StopAllCoroutines();
                        typeof(TacticsBattlePanel).GetMethod("ShowResult", Private).Invoke(panel, null);
                    }
                    break;
                }
                case "story":
                {
                    Invoke(app, "OpenLevelMap");
                    yield return null;
                    Assert.That(model.TryStartStory("stage-1-1", out var runner, out string why), Is.True, why);
                    StoryPanel.Open(safeRoot, model, runner, () => { });
                    break;
                }
                case "members": Invoke(app, "ShowScreenSafely", "members"); break;
                case "member-detail":
                    Invoke(app, "ShowScreenSafely", "members");
                    yield return null;
                    Invoke(app, "OpenMember", 0);
                    break;
                case "team": Invoke(app, "ShowScreenSafely", "team"); break;
                case "accessory": Invoke(app, "ShowScreenSafely", "accessory"); break;
                case "audition": Invoke(app, "ShowScreenSafely", "audition"); break;
                case "audition-offline":
                    Invoke(app, "ShowScreenSafely", "audition");
                    yield return null;
                    var gacha = UnityEngine.Object.FindAnyObjectByType<GachaPanel>();
                    typeof(GachaPanel).GetMethod("SelectInterviewPool", Private).Invoke(gacha, new object[] { 1 });
                    break;
                case "music-collection": Invoke(app, "OpenMusicCollection1101"); break;
                case "practice": Invoke(app, "OpenLobbyPractice"); break;
                case "tasks": Invoke(app, "OpenDailyTasks"); break;
                case "mail": Invoke(app, "OpenInbox"); break;
                case "shop": Invoke(app, "OpenCurrency", CurrencyIds.Diamond); break;
                case "settings": Invoke(app, "OpenSettings"); break;
                case "signin": Invoke(app, "OpenSignIn"); break;
                case "profile": Invoke(app, "OpenProfile"); break;
                default: throw new ArgumentException(page);
            }
            yield return null;
        }

        private static void Invoke(object target, string method, params object[] args)
        {
            MethodInfo info = target.GetType().GetMethods(Private)
                .FirstOrDefault(m => m.Name == method && m.GetParameters().Length == args.Length);
            if (info == null) throw new MissingMethodException(target.GetType().Name, method);
            info.Invoke(target, args);
        }

        /// <summary>Overlay canvases created by pages (transitions, popups) render into the capture too.</summary>
        private static void ConvertStrayCanvases(Camera camera)
        {
            foreach (Canvas canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera;
                canvas.planeDistance = 5;
            }
        }

        // ------------------------------------------------------------------ rendering

        /// <summary>Renders a frame; with <paramref name="key"/> every gap filler is painted that flat colour.</summary>
        private static Color32[] Render(Camera camera, RenderTexture rt, Color32? key)
        {
            var restore = new List<Action>();
            if (key.HasValue)
            {
                Color k = key.Value;
                Color oldClear = camera.backgroundColor;
                camera.backgroundColor = k;
                restore.Add(() => camera.backgroundColor = oldClear);
                foreach (Graphic graphic in UnityEngine.Object.FindObjectsByType<Graphic>(FindObjectsSortMode.None))
                {
                    if (!IsFiller(graphic, out bool hide)) continue;
                    Graphic g = graphic;
                    bool oldEnabled = g.enabled; Color oldColor = g.color;
                    if (hide) { g.enabled = false; restore.Add(() => g.enabled = oldEnabled); continue; }
                    g.color = new Color(k.r, k.g, k.b, 1f);
                    restore.Add(() => g.color = oldColor);
                    if (g is RawImage raw)
                    {
                        Texture oldTexture = raw.texture; raw.texture = Texture2D.whiteTexture;
                        restore.Add(() => raw.texture = oldTexture);
                    }
                    else if (g is Image image)
                    {
                        Sprite oldSprite = image.sprite; image.sprite = null;
                        restore.Add(() => image.sprite = oldSprite);
                    }
                    else
                    {
                        FieldInfo field = g.GetType().GetField("texture", Private);
                        if (field != null)
                        {
                            object oldTexture = field.GetValue(g); field.SetValue(g, Texture2D.whiteTexture);
                            restore.Add(() => field.SetValue(g, oldTexture));
                        }
                    }
                    g.SetAllDirty();
                }
            }
            try
            {
                Canvas.ForceUpdateCanvases();
                camera.Render();
                var previous = RenderTexture.active; RenderTexture.active = rt;
                var texture = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
                texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); texture.Apply();
                RenderTexture.active = previous;
                Color32[] pixels = texture.GetPixels32();
                UnityEngine.Object.DestroyImmediate(texture);
                return pixels;
            }
            finally
            {
                for (int i = restore.Count - 1; i >= 0; i--) restore[i]();
                Canvas.ForceUpdateCanvases();
            }
        }

        /// <summary>
        /// The layers that only exist to fill whatever a page leaves uncovered: the page ambient
        /// (blurred art) and its edge feathers, and the app shell's stretched backdrop + tint.
        /// </summary>
        private static bool IsFiller(Graphic graphic, out bool hide)
        {
            hide = false;
            if (graphic == null || !graphic.isActiveAndEnabled) return false;
            string name = graphic.gameObject.name;
            if (graphic.GetComponent<PageAmbient>() != null) return true;
            if (graphic.GetType().Name == "FeatherQuad") return true;
            // Page-owned copies of the blurred ambient (e.g. the PSD battle backdrop).
            if (graphic is RawImage raw && raw.texture != null && raw.texture.name.StartsWith("Ambient-", StringComparison.Ordinal)) return true;
            Transform parent = graphic.transform.parent;
            bool shellChild = parent != null && parent.GetComponent<Canvas>() != null && parent.GetComponentInParent<ChoSirenApp>() != null;
            if (shellChild && name == "Background") return true;
            if (shellChild && name == "Atmosphere") { hide = true; return true; }
            return false;
        }

        // ------------------------------------------------------------------ measuring

        private static void Measure(Device device, bool wide, Vector2 safeMin, Vector2 safeMax,
            Color32[] a, Color32[] b, PageResult result)
        {
            int w = device.Width, h = device.Height;
            bool Gap(int x, int y)
            {
                Color32 p = a[y * w + x], q = b[y * w + x];
                int d = Mathf.Abs(p.r - q.r) + Mathf.Abs(p.g - q.g) + Mathf.Abs(p.b - q.b);
                return d > 150; // > ~20% of the filler shows through
            }
            // Landscape web / tablets keep the portrait column by design: the screen sides outside
            // it are ambient; the page must still reach the column's own edges and the screen's
            // top and bottom inside the column.
            int colLeft = wide ? Mathf.RoundToInt(safeMin.x * w) : 0;
            int colRight = wide ? Mathf.RoundToInt(safeMax.x * w) - 1 : w - 1;
            result.Exempt[0] = result.Exempt[2] = wide;
            // The outer band checked per side: a thin opaque line right at the edge must not hide
            // a gap behind it, so the whole band counts.
            int band = Mathf.Max(6, Mathf.RoundToInt(Mathf.Min(w, h) * .03f));
            for (int side = 0; side < 4; side++)
            {
                int hits = 0, total = 0;
                var depths = new List<int>();
                bool vertical = side == 0 || side == 2;
                // In the column the side feathers run the full height; their corners are not a
                // top/bottom gap, so the top and bottom bands skip one feather width at each end.
                int feather = wide ? Mathf.RoundToInt(56f * h / 1536f) : 0;
                int from = vertical ? 0 : colLeft + feather, to = vertical ? h : colRight + 1 - feather;
                int edgeX0 = side == 0 ? colLeft : colRight;
                for (int i = from; i < to; i += 2)
                {
                    for (int r = 0; r < band; r += 2)
                    {
                        int x = side == 0 ? edgeX0 + r : side == 2 ? edgeX0 - r : i;
                        int y = side == 1 ? h - 1 - r : side == 3 ? r : i;
                        total++; if (Gap(x, y)) hits++;
                    }
                    if (i % 8 != 0) continue;
                    int depth = 0, limit = vertical ? (colRight - colLeft) / 2 : h / 2;
                    for (int d = 0; d < limit; d++)
                    {
                        int x = side == 0 ? edgeX0 + d : side == 2 ? edgeX0 - d : i;
                        int y = side == 1 ? h - 1 - d : side == 3 ? d : i;
                        if (Gap(x, y)) depth++;
                    }
                    depths.Add(depth);
                }
                result.Ring[side] = total > 0 ? hits / (float)total : 0f;
                depths.Sort();
                result.Depth[side] = depths.Count > 0 ? depths[depths.Count * 3 / 4] : 0;
                // Column sides are reported, not judged: there the page feathers into the ambient by design.
                if (result.Ring[side] > .03f && !(wide && vertical))
                    result.Problems.Add($"{SideName(side)} edge {result.Ring[side] * 100f:0}% filler (≈{result.Depth[side]}px)");
            }
        }

        private static string SideName(int side) => side == 0 ? "left" : side == 1 ? "top" : side == 2 ? "right" : "bottom";

        private static readonly Vector3[] Corners = new Vector3[4];

        /// <summary>Buttons pushed off screen, or (on the notched device) out of the safe area.</summary>
        private static void CheckButtons(ChoSirenApp app, Canvas canvas, Camera camera, Device device,
            Vector2 safeMin, Vector2 safeMax, PageResult result)
        {
            var screen = new Rect(0, 0, device.Width, device.Height);
            var safe = Rect.MinMaxRect(safeMin.x * device.Width, safeMin.y * device.Height, safeMax.x * device.Width, safeMax.y * device.Height);
            foreach (Button button in canvas.GetComponentsInChildren<Button>(false))
            {
                if (!button.isActiveAndEnabled || !button.interactable) continue;
                Graphic target = button.targetGraphic;
                if (target != null && !target.raycastTarget) continue;
                CanvasGroup group = button.GetComponentInParent<CanvasGroup>();
                if (group != null && (group.alpha < .05f || !group.blocksRaycasts)) continue;
                ((RectTransform)button.transform).GetWorldCorners(Corners);
                Vector2 lo = RectTransformUtility.WorldToScreenPoint(camera, Corners[0]);
                Vector2 hi = RectTransformUtility.WorldToScreenPoint(camera, Corners[2]);
                var rect = Rect.MinMaxRect(Mathf.Min(lo.x, hi.x), Mathf.Min(lo.y, hi.y), Mathf.Max(lo.x, hi.x), Mathf.Max(lo.y, hi.y));
                float area = rect.width * rect.height;
                if (area < 4f || area > screen.width * screen.height * .6f) continue;
                float onScreen = Overlap(rect, screen) / area;
                if (onScreen < .6f)
                {
                    result.Clipped.Add($"{button.name} {onScreen * 100f:0}% on screen");
                    result.Problems.Add($"{button.name} clipped");
                }
                else if ((device.Top > 0 || device.Bottom > 0) && !safe.Contains(rect.center))
                    result.Clipped.Add($"{button.name} outside safe area");
            }
        }

        private static float Overlap(Rect a, Rect b)
        {
            float w = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
            float h = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
            return w > 0 && h > 0 ? w * h : 0f;
        }

        // ------------------------------------------------------------------ output

        private static void SaveHalf(Color32[] pixels, int w, int h, string path)
        {
            int hw = w / 2, hh = h / 2;
            var half = new Color32[hw * hh];
            for (int y = 0; y < hh; y++)
            for (int x = 0; x < hw; x++)
            {
                Color32 p0 = pixels[(y * 2) * w + x * 2], p1 = pixels[(y * 2) * w + x * 2 + 1];
                Color32 p2 = pixels[(y * 2 + 1) * w + x * 2], p3 = pixels[(y * 2 + 1) * w + x * 2 + 1];
                half[y * hw + x] = new Color32((byte)((p0.r + p1.r + p2.r + p3.r) / 4), (byte)((p0.g + p1.g + p2.g + p3.g) / 4),
                    (byte)((p0.b + p1.b + p2.b + p3.b) / 4), 255);
            }
            var texture = new Texture2D(hw, hh, TextureFormat.RGBA32, false);
            texture.SetPixels32(half); texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToJPG(88));
            UnityEngine.Object.DestroyImmediate(texture);
        }

        /// <summary>Where the filler shows through, in red over a dim copy of the keyed frame.</summary>
        private static void SaveMask(Color32[] a, Color32[] b, int w, int h, string path)
        {
            var mask = new Color32[a.Length];
            for (int i = 0; i < a.Length; i++)
            {
                int d = Mathf.Abs(a[i].r - b[i].r) + Mathf.Abs(a[i].g - b[i].g) + Mathf.Abs(a[i].b - b[i].b);
                byte grey = (byte)((a[i].r + a[i].g + a[i].b) / 9);
                mask[i] = d > 150 ? new Color32(255, 40, 40, 255) : new Color32(grey, grey, grey, 255);
            }
            SaveHalf(mask, w, h, path);
        }
    }
}
