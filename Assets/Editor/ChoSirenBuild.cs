using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
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
            // Portrait window that fits a 1080p screen (resizable); not the desktop's native landscape size.
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultIsNativeResolution = false;
            PlayerSettings.defaultScreenWidth = 470;
            PlayerSettings.defaultScreenHeight = 1010;
            PlayerSettings.resizableWindow = true;
            ApplyDefaultIcon();
            Build(output, BuildTarget.StandaloneWindows64, BuildOptions.CleanBuildCache);
            WriteWindowsSupportFiles(output);
        }

        /// <summary>macOS app (2026-10-22): Apple silicon, same portrait window as Windows.</summary>
        public static void BuildMacOS()
        {
            string output = ResolveOutput("Builds/macOS/CHO-SIREN.app");
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultIsNativeResolution = false;
            PlayerSettings.defaultScreenWidth = 470;
            PlayerSettings.defaultScreenHeight = 1010;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "com.bojuejun.chosiren");
            ApplyDefaultIcon();
            // Apple silicon only: the universal build needs Burst's llvm-lipo, which is not allowed to run here.
            UnityEditor.OSXStandalone.UserBuildSettings.architecture = UnityEditor.Build.OSArchitecture.ARM64;
            Build(output, BuildTarget.StandaloneOSX, BuildOptions.CleanBuildCache);
        }

        public static void BuildWebGL()
        {
            string output = ResolveOutput("Builds/WebGL");
            VerifyExternalMusicAsset("Assets/StreamingAssets/Music/home-f19d89ea8124a31b.mp3",
                "f19d89ea8124a31b0bae5a7a8811166cd0efdb45363994f60e8c98122a527a95");
            VerifyExternalMusicAsset("Assets/StreamingAssets/Music/battle-b0859b88305ca4f4.mp3",
                "b0859b88305ca4f40dda33876e0e92a88ce03217d20521c112a34cf872b70bd1");
            ConfigurePortraitWebGL();
            // The old 238-frame PNG lobby loop was superseded by the small MP4 player
            // and is not referenced by the current scene. Resources would nevertheless
            // force every frame into the Web archive, creating a large decompression
            // spike on iPhone. Move it under Editor only for this build, then restore it
            // immediately so desktop/source assets remain untouched.
            const string heroFrames = "Assets/Resources/Art/HeroFrames";
            const string excludedHeroFrames = "Assets/Editor/HeroFrames-WebGL-Excluded";
            bool movedHeroFrames = false;
            bool movedReferenceArt = false;
            bool movedNativeLoading = false;
            bool movedHomeMusic = false;
            bool movedBattleMusic = false;
            const string homeMusic = "Assets/Resources/Audio/bgm.wav";
            const string battleMusic = "Assets/Resources/Audio/bgm-battle.wav";
            const string excludedHomeMusic = "Assets/Editor/bgm-home-WebGL-Excluded.wav";
            const string excludedBattleMusic = "Assets/Editor/bgm-battle-WebGL-Excluded.wav";
            const string nativeLoading = "Assets/StreamingAssets/NativeLoading";
            const string excludedNativeLoading = "Assets/Editor/NativeLoading-WebGL-Excluded";
            const string referenceArt = "Assets/Resources/Art/Reference038";
            const string excludedReferenceArt = "Assets/Editor/Reference038-WebGL-Excluded";
            using var streamedArt = new ReferenceArtWebBuild();
            using var streamedStory = new StoryArtWebBuild1019();
            try
            {
                streamedArt.Prepare();
                streamedStory.Prepare();
                movedHeroFrames = TemporarilyMoveAsset(heroFrames, excludedHeroFrames);
                movedReferenceArt = TemporarilyMoveAsset(referenceArt, excludedReferenceArt);
                // The installed app's title CG lives in StreamingAssets; the web page plays
                // its own copy from media/, so keep it out of the WebGL output.
                movedNativeLoading = TemporarilyMoveAsset(nativeLoading, excludedNativeLoading);
                // Web downloads immutable MP3s only when their music context is entered.
                // Exclude only the two long native clips; recorded voice and UI audio stay available.
                movedHomeMusic = TemporarilyMoveAudioAsset(homeMusic, excludedHomeMusic);
                movedBattleMusic = TemporarilyMoveAudioAsset(battleMusic, excludedBattleMusic);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                Debug.Log("CHO_SIREN_WEBGL_MUSIC_EXTERNAL home=true battle=true");
                // WebGL's Bee/IL2CPP graph is sensitive to assets changing while a build is
                // running. A clean cache keeps reproducible release builds from inheriting
                // a stale dependency graph after parallel art imports.
                Build(output, BuildTarget.WebGL, BuildOptions.CleanBuildCache);
            }
            finally
            {
                var restoreErrors = new List<Exception>();
                void Restore(bool moved, string source, string destination)
                {
                    if (!moved) return;
                    try { RestoreMovedAsset(source, destination); }
                    catch (Exception exception) { restoreErrors.Add(exception); }
                }
                // Attempt every restoration even if an earlier asset has a restoration error.
                Restore(movedBattleMusic, excludedBattleMusic, battleMusic);
                Restore(movedHomeMusic, excludedHomeMusic, homeMusic);
                Restore(movedReferenceArt, excludedReferenceArt, referenceArt);
                Restore(movedNativeLoading, excludedNativeLoading, nativeLoading);
                Restore(movedHeroFrames, excludedHeroFrames, heroFrames);
                if (restoreErrors.Count > 0)
                    throw new AggregateException("Temporary WebGL assets could not all be restored.", restoreErrors);
            }
            HashWebGLBuildAssets(output);
            WriteGitHubPagesMarker(output);
        }

        private static bool TemporarilyMoveAsset(string source, string destination)
        {
            if (!AssetDatabase.IsValidFolder(source)) return false;
            if (AssetDatabase.IsValidFolder(destination))
                throw new BuildFailedException($"Temporary WebGL exclusion path already exists: {destination}");

            string error = AssetDatabase.MoveAsset(source, destination);
            if (!string.IsNullOrEmpty(error))
                throw new BuildFailedException($"Could not exclude legacy WebGL frames: {error}");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("CHO_SIREN_WEBGL_HERO_FRAMES_EXCLUDED count=238");
            return true;
        }

        private static bool TemporarilyMoveAudioAsset(string source, string destination)
        {
            if (File.Exists(destination) || Directory.Exists(destination) || File.Exists(destination + ".meta"))
                throw new BuildFailedException($"Temporary WebGL music exclusion path already exists: {destination}");
            if (!File.Exists(source)) return false;
            string error = AssetDatabase.MoveAsset(source, destination);
            if (!string.IsNullOrEmpty(error))
                throw new BuildFailedException($"Could not exclude native music from WebGL: {source}: {error}");
            return true;
        }

        private static void VerifyExternalMusicAsset(string path, string expectedSha256)
        {
            if (!File.Exists(path)) throw new BuildFailedException($"External Web music is missing: {path}");
            string actual;
            using (var algorithm = SHA256.Create())
            using (var stream = File.OpenRead(path))
                actual = BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
            if (!string.Equals(actual, expectedSha256, StringComparison.Ordinal))
                throw new BuildFailedException($"External Web music does not match its immutable filename: {path}");
        }

        private static void RestoreMovedAsset(string source, string destination)
        {
            string error = AssetDatabase.MoveAsset(source, destination);
            if (!string.IsNullOrEmpty(error))
                throw new BuildFailedException($"Could not restore temporary WebGL asset: {source} -> {destination}: {error}");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static void ConfigurePortraitWebGL()
        {
            PlayerSettings.defaultWebScreenWidth = 720;
            PlayerSettings.defaultWebScreenHeight = 1536;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.WebGL.template = "PROJECT:ChoSirenPortrait";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            // Phones are the main audience: mobile browsers have no DXT, so Unity decompressed every
            // texture to RGBA32 and the tab ran out of memory mid-battle and reloaded (2026-10-21).
            // ETC2 is native on mobile WebGL2 and still crunch-compressed for download.
            EditorUserBuildSettings.webGLBuildSubtarget = WebGLTextureSubtarget.ETC2;
            // The public preview is hosted on GitHub Pages and must use Unity's
            // JavaScript decompression fallback. Avoid making iOS Safari also copy the
            // large .data response into IndexedDB during startup; hashed HTTP assets
            // already give this preview reliable browser caching.
            PlayerSettings.WebGL.dataCaching = false;
            // Unity's built-in hashed-name pass can repeatedly invalidate its own Bee
            // graph ("Backend has requested a buildprogram run 6 times"). Build stable
            // names first and hash them deterministically after a successful build.
            PlayerSettings.WebGL.nameFilesAsHashes = false;
            PlayerSettings.runInBackground = true;
            AssetDatabase.SaveAssets();
        }

        private static void HashWebGLBuildAssets(string output)
        {
            string absoluteOutput = Path.GetFullPath(output);
            string indexPath = Path.Combine(absoluteOutput, "index.html");
            string buildDirectory = Path.Combine(absoluteOutput, "Build");
            if (!File.Exists(indexPath) || !Directory.Exists(buildDirectory))
                throw new BuildFailedException("WebGL output is missing index.html or Build directory.");

            string html = File.ReadAllText(indexPath);
            string[] suffixes =
            {
                ".data.unityweb",
                ".framework.js.unityweb",
                ".wasm.unityweb",
                ".loader.js"
            };
            var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string suffix in suffixes)
            {
                string marker = "buildAssetUrl(\"";
                int markerIndex = html.IndexOf(marker, StringComparison.Ordinal);
                string sourceName = null;
                while (markerIndex >= 0)
                {
                    int nameStart = markerIndex + marker.Length;
                    int nameEnd = html.IndexOf("\"", nameStart, StringComparison.Ordinal);
                    if (nameEnd < 0)
                        break;

                    string candidate = html.Substring(nameStart, nameEnd - nameStart);
                    if (candidate.EndsWith(suffix, StringComparison.Ordinal))
                    {
                        sourceName = candidate;
                        break;
                    }
                    markerIndex = html.IndexOf(marker, nameEnd, StringComparison.Ordinal);
                }

                if (string.IsNullOrEmpty(sourceName))
                    throw new BuildFailedException($"WebGL index does not reference a *{suffix} asset.");

                string sourcePath = Path.Combine(buildDirectory, sourceName);
                if (!File.Exists(sourcePath))
                    throw new BuildFailedException($"WebGL asset referenced by index is missing: {sourceName}");

                string hash;
                using (var algorithm = SHA256.Create())
                using (var stream = File.OpenRead(sourcePath))
                    hash = BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();

                string hashedName = hash.Substring(0, 32) + suffix;
                string hashedPath = Path.Combine(buildDirectory, hashedName);
                if (!sourceName.Equals(hashedName, StringComparison.OrdinalIgnoreCase))
                {
                    if (File.Exists(hashedPath))
                        File.Delete(hashedPath);
                    File.Move(sourcePath, hashedPath);
                    html = html.Replace($"buildAssetUrl(\"{sourceName}\")", $"buildAssetUrl(\"{hashedName}\")");
                }
                keep.Add(hashedName);
            }

            foreach (string filePath in Directory.GetFiles(buildDirectory))
            {
                string fileName = Path.GetFileName(filePath);
                bool isUnityBuildAsset = false;
                foreach (string suffix in suffixes)
                {
                    if (fileName.EndsWith(suffix, StringComparison.Ordinal))
                    {
                        isUnityBuildAsset = true;
                        break;
                    }
                }
                if (isUnityBuildAsset && !keep.Contains(fileName))
                    File.Delete(filePath);
            }

            File.WriteAllText(indexPath, html, new System.Text.UTF8Encoding(false));
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

        /// <summary>
        /// Exports the Xcode project to Builds/iOS. Pass -iosSimulator for a simulator build;
        /// otherwise it targets real devices with automatic signing (the Apple ID/team is
        /// chosen in Xcode by the owner, never stored here).
        /// </summary>
        public static void BuildIOS()
        {
            string output = ResolveOutput("Builds/iOS");
            bool simulator = Array.IndexOf(Environment.GetCommandLineArgs(), "-iosSimulator") >= 0;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.bojuejun.chosiren");
            PlayerSettings.iOS.sdkVersion = simulator ? iOSSdkVersion.SimulatorSDK : iOSSdkVersion.DeviceSDK;
            // Apple-silicon Macs run arm64 simulators; new iOS simulators no longer accept x86_64.
            if (simulator) PlayerSettings.iOS.simulatorSdkArchitecture = AppleMobileArchitectureSimulator.ARM64;
            PlayerSettings.iOS.appleEnableAutomaticSigning = true;
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.statusBarHidden = true;
            ApplyIosBranding();
            Build(output, BuildTarget.iOS, BuildOptions.None);
        }

        /// <summary>Game icon (雾白 portrait, C3) and the title art as the launch image.</summary>
        private static void ApplyIosBranding()
        {
            Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/AppIcon/app-icon.png");
            if (icon != null)
            {
                int count = PlayerSettings.GetIconSizes(NamedBuildTarget.iOS, IconKind.Application).Length;
                var icons = new Texture2D[count];
                for (int i = 0; i < count; i++) icons[i] = icon;
                PlayerSettings.SetIcons(NamedBuildTarget.iOS, icons, IconKind.Application);
            }

            Texture2D launch = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/AppIcon/launch-portrait.png");
            if (launch != null)
            {
                PlayerSettings.iOS.SetiPhoneLaunchScreenType(iOSLaunchScreenType.ImageAndBackgroundRelative);
                PlayerSettings.iOS.SetLaunchScreenImage(launch, iOSLaunchScreenImageType.iPhonePortraitImage);
                PlayerSettings.SplashScreen.backgroundColor = new Color32(9, 6, 22, 255);
            }
        }

        /// <summary>
        /// Default app icon for Android / Windows / Mac (2026-10-22: 雾白 portrait, option C3), rounded corners
        /// with a transparent outside (owner asked for a rounded logo). iOS keeps the square art: it masks itself.
        /// </summary>
        private static void ApplyDefaultIcon()
        {
            Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/AppIcon/app-icon-rounded.png");
            if (icon != null) PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
        }

        [Serializable] private sealed class AndroidSigning { public string keystore, storePass, alias, keyPass; }

        /// <summary>
        /// Release signing (2026-10-22): every APK must carry the same key or Android refuses to update it.
        /// The keystore and its passwords stay outside the project in ~/.cho-siren/android-signing.json
        /// (backup in ~/cho-siren-backups/android-signing); a build without them fails instead of silently
        /// falling back to the per-machine debug key.
        /// </summary>
        private static void UseReleaseKeystore()
        {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cho-siren", "android-signing.json");
            if (!File.Exists(path)) throw new BuildFailedException("Android release keystore config missing: " + path);
            AndroidSigning signing = JsonUtility.FromJson<AndroidSigning>(File.ReadAllText(path));
            if (signing == null || !File.Exists(signing.keystore))
                throw new BuildFailedException("Android release keystore missing (see " + path + ")");
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = signing.keystore;
            PlayerSettings.Android.keystorePass = signing.storePass;
            PlayerSettings.Android.keyaliasName = signing.alias;
            PlayerSettings.Android.keyaliasPass = signing.keyPass;
        }

        public static void BuildAndroid()
        {
            string output = ResolveOutput("Builds/Android/CHO-SIREN.apk");
            EditorUserBuildSettings.buildAppBundle = false;
            ApplyDefaultIcon();
            // Side-loaded test APK (2026-10-22): one 64-bit ARM binary, ASTC (ETC2 left odd-sized art uncompressed: 413 MB APK),
            // portrait, version code from x.y.z so each release installs over the last one (saves kept).
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.bojuejun.chosiren");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            string[] parts = PlayerSettings.bundleVersion.Split('.');
            int code = 0;
            for (int i = 0; i < 3; i++) code = code * 100 + (i < parts.Length && int.TryParse(parts[i], out int n) ? n : 0);
            PlayerSettings.Android.bundleVersionCode = Math.Max(1, code);
            UseReleaseKeystore();
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
