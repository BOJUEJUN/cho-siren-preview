using UnityEditor;

/// <summary>In-scene story CGs (Scenes1019/cg-*) import as full-quality sprites like the scenes.</summary>
public sealed class StoryCgImporter1020 : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Resources/Art/Story/Scenes1019/cg-")) return;
        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
    }
}
