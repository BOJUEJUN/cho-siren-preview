using UnityEditor;
using UnityEngine;

/// <summary>
/// 2026-11-01 唱片收藏 (music-a): import rules for Resources/Art/Music1101. Runs after
/// <see cref="UiArtImporter1020"/> so the user's board art is never crunched soft:
/// the 856×1832 board and covers use plain high-quality block compression (sizes are multiples of 4),
/// the small hand-cut buttons stay uncompressed, and the window masks are single-channel Alpha8 at ≤256 px.
/// </summary>
public sealed class Music1101ArtImporter : AssetPostprocessor
{
    private const string Folder = "Assets/Resources/Art/Music1101/";

    public override uint GetVersion() => 1;
    public override int GetPostprocessOrder() => 50;

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Folder)) return;
        var importer = (TextureImporter)assetImporter;
        string name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.isReadable = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.crunchedCompression = false;
        importer.alphaIsTransparency = true;

        if (name.StartsWith("mask-"))
        {
            importer.textureType = TextureImporterType.SingleChannel;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
            importer.maxTextureSize = 256;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            Override(importer, TextureImporterFormat.Alpha8, 256);
            return;
        }
        if (name.StartsWith("b-") || name == "x")
        {
            importer.maxTextureSize = 512;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            Override(importer, TextureImporterFormat.RGBA32, 512);
            return;
        }
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.compressionQuality = 100;
        Override(importer, TextureImporterFormat.Automatic, 2048);
    }

    private static void Override(TextureImporter importer, TextureImporterFormat format, int max)
    {
        foreach (string platform in new[] { "Standalone", "WebGL", "Android", "iPhone" })
        {
            TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(platform);
            settings.overridden = format != TextureImporterFormat.Automatic || platform == "Android";
            settings.maxTextureSize = max;
            settings.format = format == TextureImporterFormat.Automatic && platform == "Android"
                ? TextureImporterFormat.ASTC_5x5 : format;
            settings.textureCompression = format == TextureImporterFormat.Automatic
                ? TextureImporterCompression.CompressedHQ : TextureImporterCompression.Uncompressed;
            settings.compressionQuality = 100;
            settings.crunchedCompression = false;
            importer.SetPlatformTextureSettings(settings);
        }
    }
}
