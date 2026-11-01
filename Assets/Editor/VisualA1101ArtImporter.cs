using UnityEditor;

/// <summary>
/// 2026-11-01 (visual-a, 赵金 1101c): import rules for Resources/Art/VisualA1101 — the neon line icons,
/// the progress ring and the torn-paper badge plate used by the low-text secondary pages. They are white
/// glyphs with a soft glow that the UI tints, so they stay sprites without mipmaps, and skip crunch (crunch
/// blotches the glow); runs after <see cref="UiArtImporter1020"/>.
/// </summary>
public sealed class VisualA1101ArtImporter : AssetPostprocessor
{
    private const string Folder = "Assets/Resources/Art/VisualA1101/";

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
        importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
        importer.filterMode = UnityEngine.FilterMode.Bilinear;
        importer.alphaIsTransparency = true;
        importer.maxTextureSize = 256;
        importer.crunchedCompression = false;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.compressionQuality = 100;
    }
}
