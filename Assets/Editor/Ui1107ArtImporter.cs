using UnityEditor;

/// <summary>
/// 2026-11-07 (赵金 six-screen pass): import rules for Resources/Art/Ui1107 — layers cut from the practice, inventory,
/// task, team, interview and battle PSDs. Sprites without mipmaps, true alpha, clamped, high-quality compression.
/// </summary>
public sealed class Ui1107ArtImporter : AssetPostprocessor
{
    private const string Folder = "Assets/Resources/Art/Ui1107/";

    public override uint GetVersion() => 2;
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
        importer.maxTextureSize = 2048;
        importer.crunchedCompression = false;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.compressionQuality = 100;
        // The inventory frame stretches vertically: keep its header, corners and bottom edge (texels L, B, R, T).
        if (assetPath.EndsWith("/inv-frame.png")) importer.spriteBorder = new UnityEngine.Vector4(40, 120, 40, 330);
    }
}
