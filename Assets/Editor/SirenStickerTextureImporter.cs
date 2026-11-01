using UnityEditor;
using UnityEngine;

namespace ChoSiren.Editor
{
    public sealed class SirenStickerTextureImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.Contains("/Art/SirenSticker20261013/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = assetPath.Contains("chibi-") ? 512 : 1024;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            if (assetPath.Contains("action-plate"))
                importer.spriteBorder = new Vector4(390f, 202f, 390f, 202f);
        }
    }
}
