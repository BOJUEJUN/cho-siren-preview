using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ChoSiren.Editor
{
    public static class ChoSirenBuild
    {
        private const string MainScene = "Assets/Scenes/Main.unity";

        public static void BuildWindows()
        {
            string output = ResolveOutput("Builds/Windows/CHO-SIREN.exe");
            Build(output, BuildTarget.StandaloneWindows64, BuildOptions.CleanBuildCache);
            WriteWindowsSupportFiles(output);
        }

        public static void BuildWebGL()
        {
            string output = ResolveOutput("Builds/WebGL");
            ConfigurePortraitWebGL();
            // WebGL's Bee/IL2CPP graph is sensitive to assets changing while a build is
            // running. A clean cache keeps reproducible release builds from inheriting
            // a stale dependency graph after parallel art imports.
            Build(output, BuildTarget.WebGL, BuildOptions.CleanBuildCache);
            WriteGitHubPagesMarker(output);
        }

        private static void ConfigurePortraitWebGL()
        {
            PlayerSettings.defaultWebScreenWidth = 720;
            PlayerSettings.defaultWebScreenHeight = 1536;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.WebGL.template = "PROJECT:ChoSirenPortrait";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.nameFilesAsHashes = true;
            PlayerSettings.runInBackground = true;
            AssetDatabase.SaveAssets();
        }

        private static void WriteGitHubPagesMarker(string output)
        {
            string absoluteOutput = Path.GetFullPath(output);
            File.WriteAllText(Path.Combine(absoluteOutput, ".nojekyll"), string.Empty);
        }

        private static void WriteWindowsSupportFiles(string playerOutput)
        {
            string playerDirectory = Path.GetDirectoryName(Path.GetFullPath(playerOutput));
            if (string.IsNullOrWhiteSpace(playerDirectory))
                throw new InvalidOperationException($"Invalid Windows player output: {playerOutput}");

            const string gameLauncher =
                "@echo off\r\n" +
                "setlocal\r\n" +
                "cd /d \"%~dp0\"\r\n" +
                "if not exist \"CHO-SIREN.exe\" (\r\n" +
                "  echo CHO-SIREN.exe is missing. Please rebuild or extract the complete package.\r\n" +
                "  pause\r\n" +
                "  exit /b 1\r\n" +
                ")\r\n" +
                "start \"\" \"CHO-SIREN.exe\"\r\n" +
                "exit /b 0\r\n";
            File.WriteAllText(
                Path.Combine(playerDirectory, "开始游戏.cmd"),
                gameLauncher,
                new System.Text.UTF8Encoding(false));

            // This developer-only shortcut deliberately searches parent folders instead
            // of embedding an absolute path, so a Git clone remains portable to another PC.
            const string launcher =
                "@echo off\r\n" +
                "setlocal EnableExtensions EnableDelayedExpansion\r\n" +
                "set \"SEARCH_DIR=%~dp0\"\r\n" +
                "for /L %%I in (1,1,6) do (\r\n" +
                "  if exist \"!SEARCH_DIR!ProjectSettings\\ProjectVersion.txt\" if exist \"!SEARCH_DIR!Tools\\Open-UnityEditorSafe.ps1\" (\r\n" +
                "    start \"CHO-SIREN Unity Editor\" powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"!SEARCH_DIR!Tools\\Open-UnityEditorSafe.ps1\"\r\n" +
                "    exit /b 0\r\n" +
                "  )\r\n" +
                "  for %%P in (\"!SEARCH_DIR!..\") do set \"SEARCH_DIR=%%~fP\\\"\r\n" +
                ")\r\n" +
                "echo Unity project source was not found above this build folder.\r\n" +
                "echo Open the repository root and run Open-CHO-SIREN-Editor there.\r\n" +
                "pause\r\n" +
                "exit /b 1\r\n";

            File.WriteAllText(
                Path.Combine(playerDirectory, "打开Unity编辑器.cmd"),
                launcher,
                new System.Text.UTF8Encoding(false));

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string readmeSource = Path.Combine(projectRoot, "Tools", "WindowsPreview-README.txt");
            if (File.Exists(readmeSource))
                File.Copy(readmeSource, Path.Combine(playerDirectory, "预览说明.txt"), true);
        }

        public static void BuildAndroid()
        {
            string output = ResolveOutput("Builds/Android/CHO-SIREN.apk");
            EditorUserBuildSettings.buildAppBundle = false;
            Build(output, BuildTarget.Android, BuildOptions.CleanBuildCache);
        }

        private static void Build(string output, BuildTarget target, BuildOptions options)
        {
            string absoluteOutput = Path.GetFullPath(output);
            string directory = target == BuildTarget.WebGL
                ? absoluteOutput
                : Path.GetDirectoryName(absoluteOutput);
            if (string.IsNullOrWhiteSpace(directory))
                throw new InvalidOperationException($"Invalid build output: {absoluteOutput}");

            Directory.CreateDirectory(directory);
            BuildPlayerOptions buildOptions = new BuildPlayerOptions
            {
                scenes = new[] { MainScene },
                locationPathName = absoluteOutput,
                target = target,
                options = options
            };

            BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException(
                    $"CHO-SIREN {target} build failed: {report.summary.result} " +
                    $"({report.summary.totalErrors} error(s)).");

            Debug.Log(
                $"CHO_SIREN_BUILD_OK target={target} output={absoluteOutput} " +
                $"size={report.summary.totalSize} duration={report.summary.totalTime}");
        }

        private static string ResolveOutput(string fallback)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int index = 0; index < args.Length - 1; index++)
            {
                if (string.Equals(args[index], "-buildOutput", StringComparison.OrdinalIgnoreCase))
                    return args[index + 1];
            }

            return fallback;
        }
    }
}
