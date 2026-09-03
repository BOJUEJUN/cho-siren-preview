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
        }

        public static void BuildWebGL()
        {
            string output = ResolveOutput("Builds/WebGL");
            ConfigurePortraitWebGL();
            Build(output, BuildTarget.WebGL, BuildOptions.None);
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
