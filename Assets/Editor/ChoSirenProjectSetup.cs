using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChoSiren.Editor
{
    public static class ChoSirenProjectSetup
    {
        private const string MainScene = "Assets/Scenes/Main.unity";

        [MenuItem("CHO-SIREN/Configure Project")]
        public static void Configure()
        {
            ConfigurePlayer();
            ConfigureArt();
            CreateMainScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("CHO_SIREN_SETUP_OK: project, art, scene and build settings are configured.");
        }

        [MenuItem("CHO-SIREN/Configure Player Settings")]
        public static void ConfigurePlayerSettings()
        {
            ConfigurePlayer();
            AssetDatabase.SaveAssets();
            Debug.Log("CHO_SIREN_PLAYER_SETTINGS_OK: portrait player settings are configured.");
        }

        private static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "CHO-SIREN Studio";
            PlayerSettings.productName = "CHO-SIREN 幻域魅声";
            PlayerSettings.bundleVersion = "0.3.0";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.runInBackground = true;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.defaultScreenWidth = 720;
            PlayerSettings.defaultScreenHeight = 1552;
            PlayerSettings.defaultWebScreenWidth = 720;
            PlayerSettings.defaultWebScreenHeight = 1552;
            PlayerSettings.WebGL.template = "PROJECT:ChoSirenPortrait";
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "com.chosiren.preview");
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.chosiren.game");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel28;
            QualitySettings.vSyncCount = 0;
        }

        private static void ConfigureArt()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Resources/Art" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer) continue;

                bool isBackground = path.EndsWith("LobbyBackground.jpg");
                bool isHeroFrame = path.Contains("/HeroFrames/");
                bool isMember = path.Contains("/Members/");

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = !isBackground;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                // Hero frames are owned by HeroFrameImportProcessor (1024, crunched); the value here
                // only has to agree with it so a manual "Configure Project" does not fight the
                // postprocessor and trigger a second reimport of all 238 frames.
                importer.maxTextureSize = isBackground ? 2048 : isMember ? 1024 : isHeroFrame ? 1024 : 1024;

                TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings("Android");
                android.overridden = true;
                // Android keeps the 512 px hero frames already baked into the .meta files.
                android.maxTextureSize = isHeroFrame ? 512 : importer.maxTextureSize;
                android.format = isBackground ? TextureImporterFormat.ASTC_8x8 : TextureImporterFormat.ASTC_6x6;
                android.compressionQuality = 75;
                importer.SetPlatformTextureSettings(android);

                importer.SaveAndReimport();
            }
        }

        private static void CreateMainScene()
        {
            Directory.CreateDirectory("Assets/Scenes");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(6, 5, 28, 255);
            camera.orthographic = true;
            EditorSceneManager.SaveScene(scene, MainScene);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MainScene, true) };
        }
    }
}
