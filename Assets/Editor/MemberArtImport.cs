using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ChoSiren.Editor
{
    public sealed class MemberArtTexturePostprocessor : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!MemberArtImport.IsRuntimeMemberArt(assetPath)) return;
            MemberArtImport.Configure((TextureImporter)assetImporter, assetPath);
        }
    }

    public static class MemberArtImport
    {
        private const string Root = "Assets/Resources/Art/Members";

        [MenuItem("CHO-SIREN/资源/重新导入成员立绘")]
        public static void ConfigureAll()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { Root });
            int configured = 0;
            for (int index = 0; index < guids.Length; index++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[index]);
                if (!IsRuntimeMemberArt(path)) continue;
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                Configure(importer, path);
                AssetDatabase.WriteImportSettingsIfDirty(path);
                AssetDatabase.ImportAsset(path,
                    ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                configured++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"CHO-SIREN 成员立绘导入完成：{configured} 张 Sprite");
        }

        internal static bool IsRuntimeMemberArt(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string normalized = path.Replace('\\', '/');
            return normalized.StartsWith(Root + "/hero-", StringComparison.Ordinal) &&
                   normalized.EndsWith(".png", StringComparison.OrdinalIgnoreCase);
        }

        internal static void Configure(TextureImporter importer, string path)
        {
            bool thumbnail = string.Equals(Path.GetFileName(path), "thumb.png",
                StringComparison.OrdinalIgnoreCase);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = thumbnail ? 256 : 512;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
        }
    }
}
