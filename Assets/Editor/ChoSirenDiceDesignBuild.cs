using UnityEditor;
using UnityEngine;

namespace ChoSiren.Editor
{
    public static class ChoSirenDiceDesignBuild
    {
        public static void Prepare()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            VerifyPlaylistAudio();
            for (int value = 1; value <= 6; value++)
            {
                string path = $"Assets/Resources/Art/Reference038/vfx-die-face-{value}.png";
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
                importer.sRGBTexture = true;
                importer.mipmapEnabled = false;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = 256;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null || sprite.rect.width != 148 || sprite.rect.height != 152)
                    throw new System.InvalidOperationException("Dice import differs from the supplied PSD: " + path);
            }
            PlayerSettings.bundleVersion = "0.3.78";
            AssetDatabase.SaveAssets();
            Debug.Log("CHO_DICE_DESIGN_IMPORT_OK faces=6 size=148x152 nativeAlpha=true");
        }

        private static void VerifyPlaylistAudio()
        {
            for (int index = 0; index < HomeMusicPlaylist.Count; index++)
            {
                string file = HomeMusicPlaylist.Get(index).FileName;
                string path = "Assets/StreamingAssets/Music/" + file;
                if (!System.IO.File.Exists(path))
                    throw new System.InvalidOperationException("Selected Home song is missing: " + path);
                using var hash = System.Security.Cryptography.SHA256.Create();
                using var stream = System.IO.File.OpenRead(path);
                string sha = System.BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                if (!file.Contains(sha.Substring(0, 16)))
                    throw new System.InvalidOperationException("Home song does not match its immutable name: " + path);
            }
        }

        public static void BuildWebGLRelease()
        {
            Prepare();
            ChoSirenBuild.BuildWebGL();
        }
    }
}
