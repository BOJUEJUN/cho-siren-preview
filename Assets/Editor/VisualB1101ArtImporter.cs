using UnityEditor;

/// <summary>
/// 2026-11-01 (visual-b, 赵金 1101c): import rules for Resources/Art/VisualB1101 — the extra neon line icons
/// (same family as VisualA1101: white glyph + soft glow the UI tints). Sprites, no mipmaps, no crunch.
/// </summary>
public sealed class VisualB1101ArtImporter : AssetPostprocessor
{
    private const string Folder = "Assets/Resources/Art/VisualB1101/";

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
