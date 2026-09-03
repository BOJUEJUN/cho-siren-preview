using UnityEditor;
using UnityEngine;

namespace ChoSiren.Editor
{
    /// <summary>Keeps the 720 px lobby animation sharp without enabling costly mipmaps.</summary>
    public sealed class HeroFrameImportProcessor : AssetPostprocessor
    {
        private const string HeroFramePath = "Assets/Resources/Art/HeroFrames/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(HeroFramePath, System.StringComparison.OrdinalIgnoreCase)) return;

            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            // Crunch keeps the same DXT5/ETC2 GPU footprint but shrinks the on-disk / WebGL
            // download payload of the 238-frame sequence to roughly a third. Quality 70 was
            // chosen to stay visually identical at 720 px; frame count and size are untouched.
            importer.compressionQuality = 70;
            importer.crunchedCompression = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;

            ApplyPlatform(importer, "Standalone");
            ApplyPlatform(importer, "WebGL");
        }

        private static void ApplyPlatform(TextureImporter importer, string platform)
        {
            TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(platform);
            settings.overridden = true;
            settings.maxTextureSize = 1024;
            settings.format = TextureImporterFormat.Automatic;
            settings.compressionQuality = 70;
            settings.crunchedCompression = true;
            importer.SetPlatformTextureSettings(settings);
        }
    }
}
