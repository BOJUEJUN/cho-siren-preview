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
    /// 2026-11-01 (赵金 1101c · visual-a): captures the secondary pages this batch rebuilt around
    /// "最少文字" — profile, settings, mail, tasks, achievements, shop / exchange, sign-in, title + its
    /// dialogs and the shared confirm cards — on the Mac window (1080×2160) and a phone (1080×2288),
    /// and counts every visible character on each page. Writes captures and a report to
    /// Builds/qa-logs/zj-1101c/visual-a/&lt;phase&gt;/ (phase from VISUALA_PHASE, default "after").
    /// </summary>
    public sealed class VisualA1101CaptureTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const string AgreementKey = "cho-agreement-v1";

        private static readonly (string Name, int Width, int Height)[] Devices =
        {
            ("mac-1080x2160", 1080, 2160),
            ("phone-1080x2288", 1080, 2288),
        };

        public static IEnumerable<string> DeviceNames => Devices.Select(d => d.Name);

        private static readonly string[] Pages =
        {
            "profile", "achievements", "settings", "confirm-reset", "mail", "mail-detail", "tasks", "chapter-tasks",
            "tasks-weekly", "signin-done", "mail-claimed", "captain-confirm",
            "shop-gold", "shop-stamina", "shop-diamond", "exchange-confirm", "signin", "signin-help", "stage-income",
            "title", "title-agreement", "title-notice", "title-repair",
        };

        private static string Phase => Environment.GetEnvironmentVariable("VISUALA_PHASE") is string p && p.Length > 0 ? p : "after";
        private static string Out => Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/qa-logs/zj-1101c/visual-a", Phase));

        private string save, legacy;
        private bool had, hadLegacy, hadAgreement;
        private int agreement;

        [UnitySetUp] public IEnumerator SetUp()
        {
            had = PlayerPrefs.HasKey(GameModel.SaveKey); save = PlayerPrefs.GetString(GameModel.SaveKey);
            hadLegacy = PlayerPrefs.HasKey(GameModel.LegacySaveKey); legacy = PlayerPrefs.GetString(GameModel.LegacySaveKey);
            hadAgreement = PlayerPrefs.HasKey(AgreementKey); agreement = PlayerPrefs.GetInt(AgreementKey, 0);
            yield return null;
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            foreach (var old in UnityEngine.Object.FindObjectsByType<ChoSirenApp>(FindObjectsSortMode.None)) UnityEngine.Object.Destroy(old.gameObject);
            yield return null;
            if (had) PlayerPrefs.SetString(GameModel.SaveKey, save); else PlayerPrefs.DeleteKey(GameModel.SaveKey);
            if (hadLegacy) PlayerPrefs.SetString(GameModel.LegacySaveKey, legacy); else PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
            if (hadAgreement) PlayerPrefs.SetInt(AgreementKey, agreement); else PlayerPrefs.DeleteKey(AgreementKey);
            PlayerPrefs.Save();
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator CaptureSecondaryPages([ValueSource(nameof(DeviceNames))] string deviceName)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("Needs a graphics device to render the captures.");
            LogAssert.ignoreFailingMessages = true;
            var device = Devices.First(d => d.Name == deviceName);
            Directory.CreateDirectory(Out);
            var report = new StringBuilder();
            report.AppendLine($"# visual-a · {device.Name} · phase {Phase}");
            report.AppendLine();
            report.AppendLine("| page | visible characters | text blocks |");
            report.AppendLine("|---|---|---|");
            var errors = new List<string>();
            foreach (string page in Pages)
            {
                (int chars, int blocks, string dump) result = default;
                string error = null;
                IEnumerator run = Flatten(CapturePage(device, page, r => result = r));
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
                foreach (var old in UnityEngine.Object.FindObjectsByType<NativeTitleScreen>(FindObjectsSortMode.None)) UnityEngine.Object.Destroy(old.gameObject);
                yield return null;
                if (error != null) { report.AppendLine($"| {page} | ERROR {error} | |"); errors.Add(page + ": " + error); continue; }
                report.AppendLine($"| {page} | {result.chars} | {result.blocks} |");
                File.WriteAllText(Path.Combine(Out, $"{device.Name}-{page}-text.txt"), result.dump);
            }
            File.WriteAllText(Path.Combine(Out, $"report-{device.Name}.md"), report.ToString());
            Debug.Log(report.ToString());
            Assert.That(errors, Is.Empty, string.Join("\n", errors));
        }

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

        private IEnumerator CapturePage((string Name, int Width, int Height) device, string page, Action<(int, int, string)> done)
        {
            var saveData = new GameSave { HomeCharacterIndex = 0, StoryProgress = 100, PlayerName = "经纪人" };
            saveData.CompletedStories.Add(GameModel.PrologueStoryId);
            saveData.ClearedStages.Add(new StageClear { Id = "stage-1-1", Stars = 3 });
            saveData.ClearedStages.Add(new StageClear { Id = "stage-1-2", Stars = 2 });
            PlayerPrefs.SetString(GameModel.SaveKey, JsonUtility.ToJson(saveData));
            if (page == "title-agreement") PlayerPrefs.DeleteKey(AgreementKey); else PlayerPrefs.SetInt(AgreementKey, 1);
            foreach (var old in UnityEngine.Object.FindObjectsByType<ChoSirenApp>(FindObjectsSortMode.None)) UnityEngine.Object.Destroy(old.gameObject);
            yield return null;

            var app = new GameObject("VisualA1101").AddComponent<ChoSirenApp>();
            var camera = new GameObject("VisualA1101Camera", typeof(Camera)).GetComponent<Camera>();
            camera.transform.SetParent(app.transform, false);
            var rt = new RenderTexture(device.Width, device.Height, 24);
            try
            {
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.orthographic = true; camera.targetTexture = rt;
                Canvas canvas = app.GetComponentInChildren<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10;
                SafeAreaFitter.Layout(new Rect(0, 0, device.Width, device.Height), new Vector2(device.Width, device.Height),
                    out Vector2 min, out Vector2 max, out bool wide);
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

                Transform root = null;
                yield return Open(app, page, r => root = r);
                float until = Time.realtimeSinceStartup + 1.3f;
                while (Time.realtimeSinceStartup < until) yield return null;
                foreach (Canvas stray in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                {
                    if (!stray.isRootCanvas || stray.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                    stray.renderMode = RenderMode.ScreenSpaceCamera; stray.worldCamera = camera; stray.planeDistance = 5;
                }
                Canvas.ForceUpdateCanvases();
                yield return null;
                Save(camera, rt, Path.Combine(Out, $"{device.Name}-{page}.jpg"));
                done(Count(root));
            }
            finally
            {
                camera.targetTexture = null;
                rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
            }
        }

        private static IEnumerator Open(ChoSirenApp app, string page, Action<Transform> root)
        {
            var safeRoot = (Transform)typeof(ChoSirenApp).GetField("safeRoot", Private).GetValue(app);
            var model = (GameModel)typeof(ChoSirenApp).GetField("model", Private).GetValue(app);
            Transform Modal() => (typeof(ChoSirenApp).GetField("modalObject", Private).GetValue(app) as GameObject)?.transform;
            switch (page)
            {
                case "profile": Invoke(app, "OpenProfile"); break;
                case "achievements": Invoke(app, "OpenAchievements"); break;
                case "settings": Invoke(app, "OpenSettings"); break;
                case "confirm-reset":
                {
                    Invoke(app, "OpenSettings");
                    yield return null;
                    Button reset = Modal().GetComponentsInChildren<Button>(true).First(b => b.name == "Reset");
                    reset.onClick.Invoke();
                    break;
                }
                case "mail": Invoke(app, "OpenInbox"); break;
                case "mail-detail": Invoke(app, "OpenMail1101", "welcome-2026"); break;
                case "mail-claimed":
                    model.ClaimMail("welcome-2026", out _);
                    yield return new WaitForSecondsRealtime(.1f);
                    Invoke(app, "OpenInbox");
                    break;
                case "signin-done":
                    model.SignIn(out _);
                    yield return new WaitForSecondsRealtime(.1f);
                    Invoke(app, "OpenSignIn");
                    break;
                case "captain-confirm":
                {
                    int bench = Enumerable.Range(0, GameModel.Members.Length).First(i => !model.Save.Team.Contains(i));
                    if (!model.Save.UnlockedMembers.Contains(bench)) model.Save.UnlockedMembers.Add(bench);
                    Invoke(app, "ConfirmCaptain", bench);
                    break;
                }
                case "tasks-weekly":
                {
                    Invoke(app, "OpenDailyTasks");
                    yield return null;
                    TaskBoardPanel board = UnityEngine.Object.FindAnyObjectByType<TaskBoardPanel>();
                    Invoke(board, "SelectCadence", TaskCadence.Weekly);
                    root(board.transform);
                    yield break;
                }
                case "signin-help":
                {
                    Invoke(app, "OpenSignIn");
                    yield return null;
                    Modal().GetComponentsInChildren<Button>(true).First(b => b.name == "SignInHelp").onClick.Invoke();
                    break;
                }
                case "tasks":
                    Invoke(app, "OpenDailyTasks");
                    yield return null;
                    root(UnityEngine.Object.FindAnyObjectByType<TaskBoardPanel>().transform);
                    yield break;
                case "chapter-tasks":
                {
                    Invoke(app, "OpenLevelMap");
                    yield return new WaitForSecondsRealtime(.3f);
                    var map = UnityEngine.Object.FindAnyObjectByType<LevelMapPanel>();
                    Invoke(map, "OpenChapterTasks");
                    yield return null;
                    root(map.transform.Find("ChapterTasksModal") ?? map.transform);
                    yield break;
                }
                case "shop-gold": Invoke(app, "OpenCurrency", CurrencyIds.Gold); break;
                case "shop-stamina": Invoke(app, "OpenCurrency", CurrencyIds.Stamina); break;
                case "shop-diamond": Invoke(app, "OpenCurrency", CurrencyIds.Diamond); break;
                case "exchange-confirm": Invoke(app, "OpenCurrencyConfirmation", CurrencyIds.Gold, model.PreviewGoldExchange()); break;
                case "signin": Invoke(app, "OpenSignIn"); break;
                case "stage-income": Invoke(app, "OpenActivity"); break;
                case "title":
                case "title-agreement":
                case "title-notice":
                case "title-repair":
                {
                    NativeTitleScreen title = NativeTitleScreen.Create(safeRoot.parent, (RectTransform)safeRoot, false);
                    if (page == "title") title.MarkReady();
                    if (page == "title-notice") Invoke(title, "ShowNotice");
                    if (page == "title-repair") Invoke(title, "ShowRepair");
                    if (page == "title") yield return new WaitForSecondsRealtime(2.4f);
                    root(title.transform);
                    yield break;
                }
                default: throw new ArgumentException(page);
            }
            yield return null;
            root(Modal());
        }

        private static void Invoke(object target, string method, params object[] args)
        {
            MethodInfo info = target.GetType().GetMethods(Private)
                .FirstOrDefault(m => m.Name == method && m.GetParameters().Length == args.Length);
            if (info == null) throw new MissingMethodException(target.GetType().Name, method);
            info.Invoke(target, args);
        }

        /// <summary>Visible characters (whitespace excluded) of every shown Text under the page root.</summary>
        public static (int chars, int blocks, string dump) Count(Transform root)
        {
            if (root == null) return (0, 0, "(no root)");
            int chars = 0, blocks = 0;
            var dump = new StringBuilder();
            foreach (Text text in root.GetComponentsInChildren<Text>(false))
            {
                if (!text.isActiveAndEnabled || text.color.a < .08f || string.IsNullOrWhiteSpace(text.text)) continue;
                float alpha = 1f;
                foreach (CanvasGroup group in text.GetComponentsInParent<CanvasGroup>()) alpha *= group.alpha;
                if (alpha < .08f) continue;
                string value = text.supportRichText ? System.Text.RegularExpressions.Regex.Replace(text.text, "<[^>]+>", "") : text.text;
                int n = value.Count(c => !char.IsWhiteSpace(c));
                if (n == 0) continue;
                chars += n; blocks++;
                dump.AppendLine($"{text.name}\t{n}\t{value.Replace("\n", " / ")}");
            }
            return (chars, blocks, dump.ToString());
        }

        private static void Save(Camera camera, RenderTexture rt, string path)
        {
            camera.Render();
            var previous = RenderTexture.active; RenderTexture.active = rt;
            var full = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            full.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); full.Apply();
            RenderTexture.active = previous;
            Color32[] pixels = full.GetPixels32();
            int w = rt.width, h = rt.height, hw = w / 2, hh = h / 2;
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
            File.WriteAllBytes(path, texture.EncodeToJPG(90));
            UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(full);
        }
    }
}
