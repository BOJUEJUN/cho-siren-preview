using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ChoSiren.Tests
{
    /// <summary>
    /// 2026-11-01 (赵金 "资源不清楚"): every homepage layer — including the PSD v3 idols character3/4 and their
    /// outlines — imports at source size without Crunch on every platform: BC7 (Standalone), ASTC 4×4 (Android),
    /// and RGBA32 for WebGL, mirroring the original PNG bytes the web player streams from StreamingAssets.
    /// </summary>
    public sealed class LobbyArtImport1101Tests
    {
        private const string Root = "Assets/Resources/Art/Reference038/";

        [Test] public void HomeLayersKeepSourceSizeWithoutCrunchOnEveryPlatform()
        {
            string[] files = Directory.GetFiles(Root, "home-layer-*.png");
            Assert.That(files.Length, Is.GreaterThanOrEqualTo(40));
            foreach (string file in files)
            {
                string path = file.Replace('\\', '/');
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.That(importer, Is.Not.Null, path);
                importer.GetSourceTextureWidthAndHeight(out int width, out int height);
                int longest = Mathf.Max(width, height);
                Check(importer, "WebGL", TextureImporterFormat.RGBA32, longest, path);
                Check(importer, "Standalone", TextureImporterFormat.BC7, longest, path);
                Check(importer, "Android", TextureImporterFormat.ASTC_4x4, longest, path);
                Assert.That(importer.mipmapEnabled, Is.False, path);
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Assert.That(texture, Is.Not.Null, path);
                Assert.That(new Vector2Int(texture.width, texture.height), Is.EqualTo(new Vector2Int(width, height)),
                    path + " imported below its source size");
                Assert.That(texture.format.ToString(), Does.Not.Contain("Crunched"), path);
            }
        }

        [Test] public void LobbyIdolPortraitsAndOutlinesAreCoveredOnIos()
        {
            foreach (string id in new[] { "03", "04", "character2-portrait", "character2-outline", "character3-portrait",
                         "character3-outline", "character4-portrait", "character4-outline" })
            {
                string path = Root + "home-layer-" + id + ".png";
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.GetSourceTextureWidthAndHeight(out int width, out int height);
                Check(importer, "iOS", TextureImporterFormat.ASTC_4x4, Mathf.Max(width, height), path);
            }
        }

        private static void Check(TextureImporter importer, string platform, TextureImporterFormat format, int longest, string path)
        {
            TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(platform);
            Assert.That(settings.overridden, Is.True, platform + " " + path);
            Assert.That(settings.format, Is.EqualTo(format), platform + " " + path);
            Assert.That(settings.crunchedCompression, Is.False, platform + " " + path);
            Assert.That(settings.maxTextureSize, Is.GreaterThanOrEqualTo(longest), platform + " " + path);
        }
    }
}
