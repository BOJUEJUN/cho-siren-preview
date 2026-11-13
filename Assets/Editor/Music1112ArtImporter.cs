using UnityEditor;
using UnityEngine;

/// <summary>
/// 2026-11-12 新播放器: import rules for Resources/Art/Music1112 — the parts split from the artist's 播放器.psd by
/// Tools/music-player-1112/split_player.py. Every part is a plain sprite kept uncompressed so the painted panel stays crisp.
/// </summary>
public sealed class Music1112ArtImporter : AssetPostprocessor
{
    private const string Folder = "Assets/Resources/Art/Music1112/";

    public override uint GetVersion() => 1;
    public override int GetPostprocessOrder() => 50;

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Folder)) return;
        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.isReadable = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.wrapMode = System.IO.Path.GetFileNameWithoutExtension(assetPath) == "eq-seg"
            ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.alphaIsTransparency = true;
        importer.crunchedCompression = false;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        foreach (string platform in new[] { "Standalone", "WebGL", "Android", "iPhone" })
        {
            TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(platform);
            settings.overridden = true;
            settings.maxTextureSize = 2048;
            settings.format = TextureImporterFormat.RGBA32;
            settings.textureCompression = TextureImporterCompression.Uncompressed;
            settings.crunchedCompression = false;
            importer.SetPlatformTextureSettings(settings);
        }
    }
}
