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
        private bool navigationFailed;

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
                RequireNamed("GachaPanel", "gacha");
            }
            else if (string.Equals(requestedScreen, "taskboard", StringComparison.OrdinalIgnoreCase))
            {
                yield return ClickNamed("任务", "taskboard");
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
                RequireNamed("MemberStatAttack", "member-owned");
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
                RequireNamed("AccessoryPreviewCharacter", "accessory");
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
                RequireNamed("TacticsBattlePanel", "tactics");
                RequireNamed("DiceConsole", "tactics");
            }
            else if (string.Equals(requestedScreen, "levelmap", StringComparison.OrdinalIgnoreCase))
            {
                yield return ClickNamed("冒险剧本", "levelmap");
                yield return new WaitForSecondsRealtime(0.75f);
            }
            else if (string.Equals(requestedScreen, "levelmap-difficulty", StringComparison.OrdinalIgnoreCase))
            {
                yield return ClickNamed("冒险剧本", "levelmap-difficulty");
                yield return new WaitForSecondsRealtime(0.75f);
                yield return ClickNamed("ChapterDifficulty", "levelmap-difficulty");
                yield return new WaitForSecondsRealtime(0.45f);
                RequireNamed("ChapterDifficultyModal", "levelmap-difficulty");
            }
            else if (string.Equals(requestedScreen, "levelmap-rewards", StringComparison.OrdinalIgnoreCase))
            {
                yield return ClickNamed("冒险剧本", "levelmap-rewards");
                yield return new WaitForSecondsRealtime(0.75f);
                yield return ClickNamed("ChapterRewards", "levelmap-rewards");
                yield return new WaitForSecondsRealtime(0.45f);
                RequireNamed("ChapterRewardsModal", "levelmap-rewards");
            }
            else if (string.Equals(requestedScreen, "levelmap-tasks", StringComparison.OrdinalIgnoreCase))
            {
                yield return ClickNamed("冒险剧本", "levelmap-tasks");
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

            bool succeeded = !navigationFailed && File.Exists(outputPath) && File.Exists(motionPath);
            Debug.Log(succeeded
                ? $"CHO_SIREN_SMOKE_CAPTURE_OK {outputPath} motion={motionPath}"
                : $"CHO_SIREN_SMOKE_CAPTURE_TIMEOUT {outputPath}");
            yield return new WaitForSecondsRealtime(0.5f);
            Application.Quit(succeeded ? 0 : 2);
        }

        private IEnumerator ClickNamed(string objectName, string screenTag)
        {
            GameObject target = GameObject.Find(objectName);
            Button button = target != null ? target.GetComponent<Button>() : null;
            if (button == null)
            {
                Debug.LogError($"CHO_SIREN_SMOKE_SCREEN_NOT_FOUND {screenTag} missing={objectName}");
                navigationFailed = true;
                yield break;
            }

            button.onClick.Invoke();
        }

        private void RequireNamed(string objectName, string screenTag)
        {
            if (GameObject.Find(objectName) != null) return;
            navigationFailed = true;
            Debug.LogError($"CHO_SIREN_SMOKE_SCREEN_NOT_FOUND {screenTag} destination={objectName}");
        }

        private static IEnumerator WaitForFile(string path)
        {
            float timeout = Time.realtimeSinceStartup + 8f;
            while (!File.Exists(path) && Time.realtimeSinceStartup < timeout)
                yield return null;
        }
    }
}
