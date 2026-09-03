using UnityEditor;

namespace ChoSiren.Editor
{
    public sealed class ResourceIconImportProcessor : AssetPostprocessor
    {
        private const string ResourceIconPath = "Assets/Resources/Art/UI/Resource";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ResourceIconPath) || !assetPath.EndsWith("-C.png")) return;

            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 256;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.compressionQuality = 90;
            ConfigurePlatform(importer, "Standalone");
            ConfigurePlatform(importer, "WebGL");
        }

        private static void ConfigurePlatform(TextureImporter importer, string platform)
        {
            TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(platform);
            settings.overridden = true;
            settings.maxTextureSize = 256;
            settings.format = TextureImporterFormat.Automatic;
            settings.textureCompression = TextureImporterCompression.CompressedHQ;
            settings.compressionQuality = 90;
            importer.SetPlatformTextureSettings(settings);
        }
    }
}
