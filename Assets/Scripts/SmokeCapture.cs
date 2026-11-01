using System;
using System.Collections;
using System.Collections.Generic;
using ChoSiren.UI;
using UnityEngine.EventSystems;
using System.IO;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
using System.Text;
#endif

namespace ChoSiren
{
    public static class SmokeCaptureBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartCaptureWhenRequested()
        {
            string output = ReadArgument("-smokeCapture");
            if (string.IsNullOrWhiteSpace(output)) return;
            string screen = ReadArgument("-smokeScreen");
            string delayText = ReadArgument("-smokeDelay");
            float delay = 2.5f;
            if (!string.IsNullOrWhiteSpace(delayText) &&
                float.TryParse(delayText, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedDelay) &&
                !float.IsNaN(parsedDelay) && !float.IsInfinity(parsedDelay)) delay = parsedDelay;

            GameObject runner = new GameObject("Smoke Capture Runner");
            UnityEngine.Object.DontDestroyOnLoad(runner);
            runner.AddComponent<SmokeCaptureRunner>().Begin(output, screen, Mathf.Max(0.05f, delay), ReadArgument("-smokeLevel"));
        }

        internal static bool Requested => !string.IsNullOrWhiteSpace(ReadArgument("-smokeCapture"));
        internal static string Argument(string name) => ReadArgument(name);

        private static string ReadArgument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int index = 0; index < args.Length - 1; index++)
            {
                if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
                    return args[index + 1];
            }

#if UNITY_IOS && !UNITY_EDITOR
            // IL2CPP on iOS may omit the arguments passed by simctl from Environment.
            return ReadIosArgument(name);
#else
            return null;
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal", EntryPoint = "ChoSirenQaCopyArgument", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr CopyIosArgument([MarshalAs(UnmanagedType.LPStr)] string name, out int byteCount);

        [DllImport("__Internal", EntryPoint = "ChoSirenQaFreeArgument", CallingConvention = CallingConvention.Cdecl)]
        private static extern void FreeIosArgument(IntPtr value);

        private static string ReadIosArgument(string name)
        {
            IntPtr value = CopyIosArgument(name, out int byteCount);
            if (value == IntPtr.Zero) return null;
            try
            {
                var utf8 = new byte[byteCount];
                if (byteCount > 0) Marshal.Copy(value, utf8, 0, byteCount);
                return Encoding.UTF8.GetString(utf8);
            }
            finally
            {
                // Native memory belongs to this call, never to an autoreleased NSString.
                FreeIosArgument(value);
            }
        }
#endif
    }

    public sealed class SmokeCaptureRunner : MonoBehaviour
    {
        private string outputPath;
        private string requestedScreen;
        private float initialDelay;
        private bool navigationFailed;
        private bool titleEntered;
        private string requestedLevel;
        private readonly List<string> failures = new List<string>();
        private bool IsTitleCapture => string.Equals(requestedScreen, "title", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(requestedScreen, "notice", StringComparison.OrdinalIgnoreCase);

        public void Begin(string path, string screen, float delay, string level = null)
        {
            // This runner never supplies a product input path without explicit QA launch arguments.
            if (!SmokeCaptureBootstrap.Requested) { Destroy(gameObject); return; }
            outputPath = Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(Application.persistentDataPath, path));
            outputPath = Path.ChangeExtension(outputPath, ".png");
            requestedScreen = string.IsNullOrWhiteSpace(screen) ? "lobby" : screen.Trim();
            requestedLevel = level;
            initialDelay = delay;
            StartCoroutine(Capture());
        }

        private IEnumerator Capture()
        {
            Application.targetFrameRate = 60;
            yield return new WaitForSecondsRealtime(initialDelay);

            Debug.Log($"CHO_SIREN_SMOKE_BEGIN version={Application.version} platform={Application.platform} screen={requestedScreen} pixels={Screen.width}x{Screen.height} input=synthetic-unity-callbacks-not-physical-touch output={outputPath}");
            yield return EnterNativeTitleForQa();
            if (navigationFailed)
            {
                // Capture two diagnostic frames, but never turn a blocked agreement into a pass.
            }
            else if (IsTitleCapture)
            {
                if (UnityEngine.Object.FindAnyObjectByType<NativeTitleScreen>() == null) Fail("native-title-unavailable");
                if (string.Equals(requestedScreen, "notice", StringComparison.OrdinalIgnoreCase))
                {
                    yield return ClickNamed("NoticeButton", "notice");
                    yield return new WaitForSecondsRealtime(1f);
                    RequireNamed("TitleDialog", "notice");
                }
            }
            else if (string.Equals(requestedScreen, "name", StringComparison.OrdinalIgnoreCase))
            {
                var app = UnityEngine.Object.FindAnyObjectByType<ChoSirenApp>();
                const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                Transform host = (Transform)typeof(ChoSirenApp).GetField("safeRoot", flags).GetValue(app);
                typeof(ChoSirenApp).GetMethod("OpenNameDialog1028", flags).Invoke(app, new object[] { host, string.Empty, null });
                yield return new WaitForSecondsRealtime(.3f);
                RequireNamed("NameBox", "name");
            }
            else if (string.Equals(requestedScreen, "lobby", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(requestedScreen))
            {
                // Default: stay on lobby after startup delay.
            }
            else if (string.Equals(requestedScreen, "fxdemo", StringComparison.OrdinalIgnoreCase))
            {
                // Shader VFX stage on top of the lobby; the burst frames capture the whole sequence.
                StartCoroutine(FxDemo1023.Run());
                yield return new WaitForSecondsRealtime(0.2f);
            }
            else if (string.Equals(requestedScreen, "gacha", StringComparison.OrdinalIgnoreCase))
            {
                yield return ClickNamed("Nav-audition", "gacha");
                yield return new WaitForSecondsRealtime(0.6f);
                RequireNamed("GachaPanel", "gacha");
            }
            else if (string.Equals(requestedScreen, "taskboard", StringComparison.OrdinalIgnoreCase))
            {
                yield return ClickNamed("Tasks", "taskboard");
                yield return new WaitForSecondsRealtime(0.6f);
                RequireNamed("TaskBoardPanel", "taskboard");
            }
            else if (string.Equals(requestedScreen, "members", StringComparison.OrdinalIgnoreCase))
            {
                yield return ClickNamed("Nav-members", "members");
                yield return new WaitForSecondsRealtime(0.75f);
                RequireNamed("MemberSearch", "members");
            }
            else if (string.Equals(requestedScreen, "member-owned", StringComparison.OrdinalIgnoreCase))
            {
                yield return ClickNamed("Nav-members", "member-owned");
                yield return new WaitForSecondsRealtime(0.75f);
                yield return ClickNamed("Member-xingli", "member-owned");
                yield return new WaitForSecondsRealtime(0.45f);
                RequireNamed("MemberModal", "member-owned");
                RequireNamed("MemberStatVocal", "member-owned");
                RequireNamed("MemberSkillPrimary", "member-owned");
            }
            else if (string.Equals(requestedScreen, "member-locked", StringComparison.OrdinalIgnoreCase))
            {
                yield return ClickNamed("Nav-members", "member-locked");
                yield return new WaitForSecondsRealtime(0.75f);
                yield return ClickNamed("Member-hero-0002", "member-locked");
                yield return new WaitForSecondsRealtime(0.45f);
                RequireNamed("MemberModal", "member-locked");
                RequireNamed("AcquireMember", "member-locked");
            }
            else if (string.Equals(requestedScreen, "team", StringComparison.OrdinalIgnoreCase))
            {
                yield return ClickNamed("Nav-team", "team");
                yield return new WaitForSecondsRealtime(0.75f);
                RequireNamed("TeamStellarBackground", "team");
                RequireNamed("TeamPowerValue", "team");
            }
            else if (string.Equals(requestedScreen, "accessory", StringComparison.OrdinalIgnoreCase))
            {
                yield return ClickNamed("Nav-accessory", "accessory");
                yield return new WaitForSecondsRealtime(0.75f);
                RequireNamed("InventoryBoard", "accessory");
            }
            else if (string.Equals(requestedScreen, "story", StringComparison.OrdinalIgnoreCase))
            {
                yield return ClickNamed("LiveOnStage", "story-map");
                yield return new WaitForSecondsRealtime(0.6f);
                yield return ClickNamed("StoryChapter-01", "story");
                yield return new WaitForSecondsRealtime(0.8f);
            }
            else if (string.Equals(requestedScreen, "tactics", StringComparison.OrdinalIgnoreCase))
            {
                yield return ClickNamed("LiveOnStage", "tactics-map");
                yield return WaitForNamed("LevelMapPanel", "tactics-map", 15f);
                if (!navigationFailed)
                {
                    LevelMapPanel map = UnityEngine.Object.FindAnyObjectByType<LevelMapPanel>();
                    int selected = map != null ? map.SelectedStage : 1;
                    string levelName = requestedLevel;
                    if (string.IsNullOrWhiteSpace(levelName))
                        levelName = GameObject.Find($"Level-1-{selected}") != null ? $"Level-1-{selected}" : $"Level-2-{selected}";
                    if (!levelName.StartsWith("Level-", StringComparison.Ordinal)) Fail("invalid-smoke-level-name");
                    else yield return ClickNamed(levelName, "tactics-level");
                    yield return new WaitForSecondsRealtime(0.35f);
                    yield return ClickNamed("StartChallenge", "tactics");
                    yield return WaitForTactics();
                }
            }
            else if (string.Equals(requestedScreen, "levelmap", StringComparison.OrdinalIgnoreCase))
            {
                yield return ClickNamed("LiveOnStage", "levelmap");
                yield return new WaitForSecondsRealtime(0.75f);
            }
            else if (string.Equals(requestedScreen, "levelmap-rewards", StringComparison.OrdinalIgnoreCase))
            {
                yield return ClickNamed("LiveOnStage", "levelmap-rewards");
                yield return new WaitForSecondsRealtime(0.75f);
                yield return ClickNamed("ChapterRewards", "levelmap-rewards");
                yield return new WaitForSecondsRealtime(0.45f);
                RequireNamed("ChapterRewardsModal", "levelmap-rewards");
            }
            else if (string.Equals(requestedScreen, "levelmap-tasks", StringComparison.OrdinalIgnoreCase))
            {
                yield return ClickNamed("LiveOnStage", "levelmap-tasks");
                yield return new WaitForSecondsRealtime(0.75f);
                yield return ClickNamed("ChapterTasks", "levelmap-tasks");
                yield return new WaitForSecondsRealtime(0.45f);
                RequireNamed("ChapterTasksModal", "levelmap-tasks");
            }
            else if (string.Equals(requestedScreen, "performance", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(requestedScreen, "performance-result", StringComparison.OrdinalIgnoreCase))
            {
                GameObject lobbyNav = GameObject.Find("Nav-lobby");
                Button lobbyButton = lobbyNav != null ? lobbyNav.GetComponent<Button>() : null;
                if (lobbyButton != null)
                {
                    lobbyButton.onClick.Invoke();
                    yield return new WaitForSecondsRealtime(0.2f);
                }

                GameObject liveCard = GameObject.Find("LiveOnStage");
                Button liveButton = liveCard != null ? liveCard.GetComponent<Button>() : null;
                if (liveButton != null)
                {
                    liveButton.onClick.Invoke();
                    yield return new WaitForSecondsRealtime(0.8f);

                    if (string.Equals(requestedScreen, "performance-result", StringComparison.OrdinalIgnoreCase))
                    {
                        for (int note = 0; note < 6; note++)
                        {
                            GameObject tap = GameObject.Find("PerformanceTap");
                            Button tapButton = tap != null ? tap.GetComponent<Button>() : null;
                            if (tapButton == null)
                            {
                                Debug.LogError($"CHO_SIREN_SMOKE_TAP_NOT_FOUND note={note + 1}");
                                break;
                            }

                            tapButton.onClick.Invoke();
                            yield return new WaitForSecondsRealtime(0.62f);
                        }

                        yield return new WaitForSecondsRealtime(0.8f);
                    }
                }
                else
                {
                    Debug.LogError("CHO_SIREN_SMOKE_SCREEN_NOT_FOUND performance");
                    navigationFailed = true;
                }
            }
            else
            {
                Debug.LogError($"CHO_SIREN_SMOKE_SCREEN_UNKNOWN {requestedScreen}");
                navigationFailed = true;
            }

            ValidateRequestedStructure();
            yield return new WaitForEndOfFrame();

            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            DateTime firstRequestedAt = DateTime.UtcNow;
#if UNITY_IOS && !UNITY_EDITOR
            // iOS prefixes persistentDataPath itself, even for an absolute filename.
            ScreenCapture.CaptureScreenshot(Path.GetRelativePath(Application.persistentDataPath, outputPath), 1);
#else
            ScreenCapture.CaptureScreenshot(outputPath, 1);
#endif

            yield return WaitForFile(outputPath, firstRequestedAt);
            if (!CompletePng(outputPath, firstRequestedAt)) Fail("first-png-write-timeout-or-incomplete");
            string motionPath = Path.Combine(
                directory ?? string.Empty,
                Path.GetFileNameWithoutExtension(outputPath) + "-motion" + Path.GetExtension(outputPath));
            yield return new WaitForSecondsRealtime(0.45f);
            ValidateRequestedStructure();
            yield return new WaitForEndOfFrame();
            DateTime secondRequestedAt = DateTime.UtcNow;
#if UNITY_IOS && !UNITY_EDITOR
            // iOS prefixes persistentDataPath itself, even for an absolute filename.
            ScreenCapture.CaptureScreenshot(Path.GetRelativePath(Application.persistentDataPath, motionPath), 1);
#else
            ScreenCapture.CaptureScreenshot(motionPath, 1);
#endif
            yield return WaitForFile(motionPath, secondRequestedAt);
            if (!CompletePng(motionPath, secondRequestedAt)) Fail("second-png-write-timeout-or-incomplete");

            bool succeeded = !navigationFailed && CompletePng(outputPath, firstRequestedAt) && CompletePng(motionPath, secondRequestedAt);
            WriteStructureReport(directory, motionPath, succeeded);
            // QA: "-smokeBurst N [-smokeBurstStep seconds]" saves N more frames (name-burst-00.png …) to check effects frame by frame.
            if (int.TryParse(SmokeCaptureBootstrap.Argument("-smokeBurst"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int burst) && burst > 0)
            {
                float step = float.TryParse(SmokeCaptureBootstrap.Argument("-smokeBurstStep"), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedStep) ? Mathf.Clamp(parsedStep, .02f, 2f) : .1f;
                for (int frame = 0; frame < Mathf.Min(burst, 200); frame++)
                {
                    yield return new WaitForSecondsRealtime(step);
                    yield return new WaitForEndOfFrame();
                    string framePath = Path.Combine(directory ?? string.Empty, $"{Path.GetFileNameWithoutExtension(outputPath)}-burst-{frame:00}.png");
#if UNITY_IOS && !UNITY_EDITOR
                    ScreenCapture.CaptureScreenshot(Path.GetRelativePath(Application.persistentDataPath, framePath), 1);
#else
                    ScreenCapture.CaptureScreenshot(framePath, 1);
#endif
                }
                yield return new WaitForSecondsRealtime(1f);
            }
            Debug.Log(succeeded
                ? $"CHO_SIREN_SMOKE_CAPTURE_OK version={Application.version} screen={requestedScreen} input=synthetic-unity-callbacks-not-physical-touch {outputPath} motion={motionPath}"
                : $"CHO_SIREN_SMOKE_CAPTURE_FAILED version={Application.version} screen={requestedScreen} reason={string.Join(";", failures)} output={outputPath}");
            yield return new WaitForSecondsRealtime(0.5f);
            Application.Quit(succeeded ? 0 : 2);
        }

        private IEnumerator ClickNamed(string objectName, string screenTag)
        {
            if (navigationFailed) yield break;
            if (string.Equals(objectName, "Agree", StringComparison.OrdinalIgnoreCase)) { Fail("agreement-must-be-accepted-by-human"); yield break; }
            if (AgreementVisible()) { Fail("agreement-required-no-automatic-consent"); yield break; }
            // Direct callbacks do not pass through the overlay raycast blocker. Wait for
            // the real reveal just as a player must before looking up/clicking its UI.
            float coverDeadline = Time.realtimeSinceStartup + 15f;
            while (ChallengeVideoTransition.IsCoveringGame && Time.realtimeSinceStartup < coverDeadline)
            {
                if (AgreementVisible()) { Fail("agreement-required-no-automatic-consent"); yield break; }
                yield return null;
            }
            if (ChallengeVideoTransition.IsCoveringGame) { Fail("transition-cover-did-not-release-before:" + objectName); yield break; }
            GameObject target = GameObject.Find(objectName);
            Button button = target != null ? target.GetComponent<Button>() : null;
            if (button == null || !button.isActiveAndEnabled || !button.IsInteractable())
            {
                Fail($"{screenTag}:button-unavailable:{objectName}"); yield break;
            }

            Debug.Log($"CHO_SIREN_SMOKE_INPUT version={Application.version} target={objectName} mode=Button.onClick-not-physical-touch");
            button.onClick.Invoke();
        }

        private void RequireNamed(string objectName, string screenTag)
        {
            if (GameObject.Find(objectName) != null) return;
            navigationFailed = true;
            Debug.LogError($"CHO_SIREN_SMOKE_SCREEN_NOT_FOUND {screenTag} destination={objectName}");
        }

        private static IEnumerator WaitForFile(string path, DateTime requestedAt)
        {
            float timeout = Time.realtimeSinceStartup + 8f;
            while (!CompletePng(path, requestedAt) && Time.realtimeSinceStartup < timeout) yield return null;
        }

        private static bool CompletePng(string path, DateTime requestedAt)
        {
            try
            {
                if (!File.Exists(path) || File.GetLastWriteTimeUtc(path) < requestedAt.AddSeconds(-2)) return false;
                using (FileStream file = File.OpenRead(path))
                {
                    if (file.Length < 32) return false;
                    var signature = new byte[8]; if (file.Read(signature, 0, 8) != 8) return false;
                    byte[] expected = { 137, 80, 78, 71, 13, 10, 26, 10 };
                    for (int i = 0; i < 8; i++) if (signature[i] != expected[i]) return false;
                    file.Seek(-12, SeekOrigin.End); var last = new byte[12]; if (file.Read(last, 0, 12) != 12) return false;
                    return last[4] == 73 && last[5] == 69 && last[6] == 78 && last[7] == 68;
                }
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        private IEnumerator EnterNativeTitleForQa()
        {
            float timeout = Time.realtimeSinceStartup + 45f; bool asked = false;
            while (Time.realtimeSinceStartup < timeout)
            {
                if (AgreementVisible()) { Fail("agreement-required-no-automatic-consent"); yield break; }
                NativeTitleScreen title = UnityEngine.Object.FindAnyObjectByType<NativeTitleScreen>();
                if (IsTitleCapture)
                {
                    if (title != null) yield break; // Observe only. Never enter for a title capture.
                }
                else if (title == null && StartupLoadingBridge.IsReady && GameObject.Find("Nav-lobby") != null) yield break;
                else if (title != null && !asked)
                {
                    GameObject prompt = GameObject.Find("TapToStart");
                    CanvasGroup tap = prompt != null ? prompt.GetComponent<CanvasGroup>() : null;
                    // The product Update only reveals this after MarkReady AND MinimumShowSeconds.
                    if (StartupLoadingBridge.IsReady && tap != null && tap.alpha > .05f)
                    {
                        if (EventSystem.current == null) { Fail("native-title-eventsystem-missing"); yield break; }
                        var click = new PointerEventData(EventSystem.current)
                        { position = new Vector2(Screen.width * .5f, Screen.height * .5f), button = PointerEventData.InputButton.Left };
                        Debug.Log($"CHO_SIREN_SMOKE_INPUT version={Application.version} target=NativeTitleScreen.OnPointerClick mode=synthetic-not-physical-touch");
                        title.OnPointerClick(click); asked = titleEntered = true;
                    }
                }
                if (!string.IsNullOrEmpty(StartupLoadingBridge.LastError)) { Fail("startup-error:" + StartupLoadingBridge.LastError); yield break; }
                yield return null;
            }
            Fail(asked ? "native-title-did-not-dismiss" : "native-title-not-ready-or-missing");
        }

        private IEnumerator WaitForNamed(string name, string tag, float seconds)
        {
            float timeout = Time.realtimeSinceStartup + seconds;
            while (!navigationFailed && GameObject.Find(name) == null && Time.realtimeSinceStartup < timeout)
            {
                if (AgreementVisible()) { Fail("agreement-required-no-automatic-consent"); yield break; }
                yield return null;
            }
            if (!navigationFailed) RequireNamed(name, tag);
        }

        private IEnumerator WaitForTactics()
        {
            float timeout = Time.realtimeSinceStartup + 45f;
            while (!navigationFailed && Time.realtimeSinceStartup < timeout)
            {
                if (AgreementVisible()) { Fail("agreement-required-no-automatic-consent"); yield break; }
                if (GameObject.Find("FormationProblemModal") != null) { Fail("formation-dialog-blocked-use-prepared-qa-snapshot"); yield break; }
                if (GameObject.Find("StoryPanel") != null) { Fail("story-intro-blocked-no-automatic-story-skip"); yield break; }
                if (TacticsReady(out _)) yield break;
                yield return null;
            }
            if (!navigationFailed) { TacticsReady(out string reason); Fail("tactics-not-ready:" + reason); }
        }

        private static bool AgreementVisible() => GameObject.Find("AgreementDialog") != null;

        private static bool TacticsReady(out string reason)
        {
            GameObject stage = GameObject.Find("BattlePSD928");
            if (stage == null || GameObject.Find("TacticsBattlePanel") == null) { reason = "BattlePSD928-missing"; return false; }
            if (GameObject.Find("BattleResult") != null) { reason = "battle-already-finished"; return false; }
            for (int i = 0; i < 6; i++)
            {
                Transform die = stage.transform.Find("Dice-" + i);
                Button button = die != null ? die.GetComponent<Button>() : null;
                RectTransform rect = die as RectTransform;
                Image face = die != null ? die.GetComponentInChildren<Image>(false) : null;
                // The transparent Button's own Image is not the face; inspect the authored child.
                if (die != null)
                    foreach (Image image in die.GetComponentsInChildren<Image>(false))
                        if (image.name == "DiceFace-" + i) { face = image; break; }
                if (button == null || !button.isActiveAndEnabled || !button.IsInteractable() || rect == null ||
                    rect.rect.width <= 1 || rect.rect.height <= 1 || face == null || !face.isActiveAndEnabled || face.sprite == null || face.color.a <= .01f)
                { reason = "Dice-" + i + "-not-active-visible-interactable"; return false; }
                Canvas canvas = die.GetComponentInParent<Canvas>();
                Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
                var corners = new Vector3[4]; rect.GetWorldCorners(corners);
                foreach (Vector3 corner in corners)
                {
                    Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, corner);
                    if (point.x < -1 || point.x > Screen.width + 1 || point.y < -1 || point.y > Screen.height + 1)
                    { reason = "Dice-" + i + "-outside-rendered-screen"; return false; }
                }
            }
            reason = "six-live-authored-dice"; return true;
        }

        private void ValidateRequestedStructure()
        {
            if (AgreementVisible()) { Fail("agreement-required-no-automatic-consent"); return; }
            if (navigationFailed) return;
            if (IsTitleCapture)
            {
                if (UnityEngine.Object.FindAnyObjectByType<NativeTitleScreen>() == null) Fail("native-title-unavailable");
            }
            else if (string.Equals(requestedScreen, "lobby", StringComparison.OrdinalIgnoreCase))
            {
                RequireNamed("PsdHome20260921", "lobby"); RequireNamed("NavDiscArc", "lobby");
                foreach (string name in new[] { "Nav-team", "Nav-members", "Nav-lobby", "Nav-accessory", "Nav-audition" }) RequireNamed(name, "lobby");
            }
            else if (string.Equals(requestedScreen, "tactics", StringComparison.OrdinalIgnoreCase) && !TacticsReady(out string reason)) Fail("tactics-structure:" + reason);
        }

        private void Fail(string reason)
        {
            navigationFailed = true;
            if (!failures.Contains(reason)) failures.Add(reason);
            Debug.LogError($"CHO_SIREN_SMOKE_QA_FAILED version={Application.version} screen={requestedScreen} reason={reason}");
        }

        [Serializable] private sealed class StructureReport
        {
            public string version, platform, device, screen, inputMode, firstPng, secondPng, utc;
            public int width, height;
            public bool success, titleEntered, startupReady;
            public string[] failures, structure;
        }

        private void WriteStructureReport(string directory, string motionPath, bool succeeded)
        {
            var lines = new List<string>();
            foreach (string name in new[] { "NativeTitleScreen", "AgreementDialog", "PsdHome20260921", "NavDiscArc", "Nav-team", "Nav-members", "Nav-lobby", "Nav-accessory", "Nav-audition", "TacticsBattlePanel", "BattlePSD928", "BattleResult" })
            {
                GameObject node = GameObject.Find(name); Image image = node != null ? node.GetComponent<Image>() : null;
                lines.Add(name + ":active=" + (node != null) + ":sprite=" + (image != null && image.sprite != null ? image.sprite.name : "none"));
            }
            for (int i = 0; i < 6; i++)
            {
                GameObject die = GameObject.Find("Dice-" + i); Button button = die != null ? die.GetComponent<Button>() : null;
                lines.Add("Dice-" + i + ":active=" + (die != null) + ":interactable=" + (button != null && button.IsInteractable()));
            }
            var report = new StructureReport
            {
                version = Application.version, platform = Application.platform.ToString(), device = SystemInfo.deviceModel,
                screen = requestedScreen, width = Screen.width, height = Screen.height, success = succeeded,
                inputMode = "synthetic Unity UI callbacks; not physical touch", titleEntered = titleEntered,
                startupReady = StartupLoadingBridge.IsReady, firstPng = outputPath, secondPng = motionPath,
                utc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture), failures = failures.ToArray(), structure = lines.ToArray()
            };
            string path = Path.Combine(directory ?? string.Empty, Path.GetFileNameWithoutExtension(outputPath) + "-structure.json");
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
            Debug.Log($"CHO_SIREN_SMOKE_STRUCTURE version={Application.version} screen={requestedScreen} success={succeeded} report={path} " + string.Join(" | ", lines));
        }
    }
}
