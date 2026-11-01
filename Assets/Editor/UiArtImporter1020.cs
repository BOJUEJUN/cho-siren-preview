using UnityEditor;

/// <summary>
/// Keeps the WebGL data file under GitHub's 100 MiB cap.
/// Resources/UI (GPT-made UI/VFX art): capped at 1024 px, no mipmaps, crunch-compressed (alpha kept).
/// Resources/Art (2026-10-21): textures were imported uncompressed (182 MB of textures in R96);
/// they are now crunch-compressed at high quality, keeping each texture's own size and settings.
/// </summary>
public sealed class UiArtImporter1020 : AssetPostprocessor
{
    public override uint GetVersion() => 3;

    void OnPreprocessTexture()
    {
        var importer = (TextureImporter)assetImporter;
        if (assetPath.StartsWith("Assets/Resources/UI/"))
        {
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.crunchedCompression = true;
            importer.compressionQuality = 60;
            UseAstcOnAndroid(importer);
            return;
        }
        if (assetPath.StartsWith("Assets/Resources/Art/"))
        {
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.crunchedCompression = true;
            importer.compressionQuality = 85;
            UseAstcOnAndroid(importer);
        }
    }

    /// <summary>
    /// Crunch forces ETC2 on Android, and ETC2 cannot compress sizes that are not multiples of 4, so that
    /// art shipped as raw RGBA (413 MB APK, 2026-10-22). ASTC compresses any size; explicit overrides stay.
    /// </summary>
    static void UseAstcOnAndroid(TextureImporter importer)
    {
        TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings("Android");
        if (android.overridden && android.format != TextureImporterFormat.ASTC_6x6) return;
        android.overridden = true;
        android.format = TextureImporterFormat.ASTC_6x6;
        android.maxTextureSize = importer.maxTextureSize;
        android.compressionQuality = 50;
        importer.SetPlatformTextureSettings(android);
    }
}
