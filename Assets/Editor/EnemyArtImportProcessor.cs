using System;
using UnityEditor;
using UnityEngine;

namespace ChoSiren.Editor
{
    /// <summary>Bounds the download and GPU cost of transparent battle enemy portraits.</summary>
    public sealed class EnemyArtImportProcessor : AssetPostprocessor
    {
        private const string EnemyArtPath = "Assets/Resources/Art/Enemies/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(EnemyArtPath, StringComparison.OrdinalIgnoreCase) ||
                !assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) return;

            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.compressionQuality = 80;
            importer.crunchedCompression = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;

            ConfigurePlatform(importer, "Standalone");
            ConfigurePlatform(importer, "WebGL");
        }

        private static void ConfigurePlatform(TextureImporter importer, string platform)
        {
            TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(platform);
            settings.overridden = true;
            settings.maxTextureSize = 1024;
            settings.format = TextureImporterFormat.Automatic;
            settings.textureCompression = TextureImporterCompression.CompressedHQ;
            settings.compressionQuality = 80;
            settings.crunchedCompression = true;
            importer.SetPlatformTextureSettings(settings);
        }
    }
}
