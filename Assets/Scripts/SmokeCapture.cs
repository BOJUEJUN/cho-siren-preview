using System;
using System.Collections;
using System.IO;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

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
            if (!string.IsNullOrWhiteSpace(delayText))
                float.TryParse(delayText, NumberStyles.Float, CultureInfo.InvariantCulture, out delay);

            GameObject runner = new GameObject("Smoke Capture Runner");
            UnityEngine.Object.DontDestroyOnLoad(runner);
            runner.AddComponent<SmokeCaptureRunner>().Begin(output, screen, Mathf.Max(0.05f, delay));
        }

        private static string ReadArgument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int index = 0; index < args.Length - 1; index++)
            {
                if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
                    return args[index + 1];
            }

            return null;
        }
    }

    public sealed class SmokeCaptureRunner : MonoBehaviour
    {
        private string outputPath;
        private string requestedScreen;
        private float initialDelay;

        public void Begin(string path, string screen, float delay)
        {
            outputPath = Path.GetFullPath(path);
            requestedScreen = screen ?? string.Empty;
            initialDelay = delay;
            StartCoroutine(Capture());
        }

        private IEnumerator Capture()
        {
            Application.targetFrameRate = 60;
            yield return new WaitForSecondsRealtime(initialDelay);

            if (string.Equals(requestedScreen, "lobby", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(requestedScreen))
            {
                // Default: stay on lobby after startup delay.
            }
            else if (string.Equals(requestedScreen, "gacha", StringComparison.OrdinalIgnoreCase))
            {
                yield return ClickNamed("Nav-audition", "gacha");
                yield return new WaitForSecondsRealtime(0.6f);
            }
            else if (string.Equals(requestedScreen, "taskboard", StringComparison.OrdinalIgnoreCase))
            {
                yield return ClickNamed("每日任务", "taskboard");
                yield return new WaitForSecondsRealtime(0.6f);
            }
            else if (string.Equals(requestedScreen, "story", StringComparison.OrdinalIgnoreCase))
            {
                yield return ClickNamed("冒险剧本", "story-map");
                yield return new WaitForSecondsRealtime(0.6f);
                yield return ClickNamed("StoryChapter-01", "story");
                yield return new WaitForSecondsRealtime(0.8f);
            }
            else if (string.Equals(requestedScreen, "tactics", StringComparison.OrdinalIgnoreCase))
            {
                yield return ClickNamed("冒险剧本", "tactics-map");
                yield return new WaitForSecondsRealtime(0.6f);
                LevelMapPanel map = UnityEngine.Object.FindAnyObjectByType<LevelMapPanel>();
                string levelName = map != null ? $"Level-1-{map.SelectedStage}" : "Level-1-1";
                yield return ClickNamed(levelName, "tactics-level");
                yield return new WaitForSecondsRealtime(0.35f);
                yield return ClickNamed("StartChallenge", "tactics");
                yield return new WaitForSecondsRealtime(1.2f);
            }
            else if (string.Equals(requestedScreen, "levelmap", StringComparison.OrdinalIgnoreCase))
            {
                yield return ClickNamed("冒险剧本", "levelmap");
                yield return new WaitForSecondsRealtime(0.75f);
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
                }
            }

            yield return new WaitForEndOfFrame();

            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            ScreenCapture.CaptureScreenshot(outputPath, 1);

            yield return WaitForFile(outputPath);
            string motionPath = Path.Combine(
                directory ?? string.Empty,
                Path.GetFileNameWithoutExtension(outputPath) + "-motion" + Path.GetExtension(outputPath));
            yield return new WaitForSecondsRealtime(0.45f);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(motionPath, 1);
            yield return WaitForFile(motionPath);

            bool succeeded = File.Exists(outputPath) && File.Exists(motionPath);
            Debug.Log(succeeded
                ? $"CHO_SIREN_SMOKE_CAPTURE_OK {outputPath} motion={motionPath}"
                : $"CHO_SIREN_SMOKE_CAPTURE_TIMEOUT {outputPath}");
            yield return new WaitForSecondsRealtime(0.5f);
            Application.Quit(succeeded ? 0 : 2);
        }

        private static IEnumerator ClickNamed(string objectName, string screenTag)
        {
            GameObject target = GameObject.Find(objectName);
            Button button = target != null ? target.GetComponent<Button>() : null;
            if (button == null)
            {
                Debug.LogError($"CHO_SIREN_SMOKE_SCREEN_NOT_FOUND {screenTag} missing={objectName}");
                yield break;
            }

            button.onClick.Invoke();
        }

        private static IEnumerator WaitForFile(string path)
        {
            float timeout = Time.realtimeSinceStartup + 8f;
            while (!File.Exists(path) && Time.realtimeSinceStartup < timeout)
                yield return null;
        }
    }
}
