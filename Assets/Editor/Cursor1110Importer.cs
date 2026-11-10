using UnityEditor;

/// <summary>2026-11-10: the lipstick mouse cursor (from the web page's SVG) imports as a readable cursor texture.</summary>
public sealed class Cursor1110Importer : AssetPostprocessor
{
    public override uint GetVersion() => 1;

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Resources/Art/Cursor1110/")) return;
        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Cursor;
        importer.isReadable = true;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
    }
}
