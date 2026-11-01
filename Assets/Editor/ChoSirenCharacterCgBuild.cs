using System;
using UnityEditor;
using UnityEngine;

namespace ChoSiren.Editor
{
    /// <summary>R64 import/build entrypoints. All importer metadata is written by Unity APIs.</summary>
    public static class ChoSirenCharacterCgBuild
    {
        private static readonly string[] Ids = { "xingli", "feiyin", "wubai", "yeying" };

        public static void ConfigureReview()
        {
            ChoSirenBgmBuild.ConfigureReview();
            ConfigureCharacters();
            PlayerSettings.companyName = "CHO-SIREN Character CG Review 20261015";
            PlayerSettings.bundleVersion = "0.3.69";
            AssetDatabase.SaveAssets();
        }

        public static void BuildWebGLRelease()
        {
            ChoSirenBgmBuild.PrepareRelease();
            ConfigureCharacters();
            PlayerSettings.bundleVersion = "0.3.69";
            AssetDatabase.SaveAssets();
            ChoSirenBuild.BuildWebGL();
        }

        private static void ConfigureCharacters()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (string id in Ids)
            {
                string body = $"Assets/Resources/Art/Members/member-{id}.png";
                ImportSprite(body, 2048);
                ImportSprite($"Assets/Resources/Art/Members/Avatars/avatar-{id}.png", 1024);
                Sprite portrait = AssetDatabase.LoadAssetAtPath<Sprite>(body);
                if (portrait == null || portrait.rect.height < 2000)
                    throw new InvalidOperationException("Revised member portrait import failed: " + id);
                Debug.Log($"CHO_CHARACTER_IMPORT_VERIFIED {id} {portrait.rect.size} RGBA32 / FullRect");
            }
        }

        private static void ImportSprite(string path, int maximum)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing PSD export: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = maximum;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            var web = importer.GetPlatformTextureSettings("WebGL");
            web.overridden = true; web.maxTextureSize = maximum;
            web.format = TextureImporterFormat.RGBA32;
            importer.SetPlatformTextureSettings(web);
            importer.SaveAndReimport();
        }
    }
}
