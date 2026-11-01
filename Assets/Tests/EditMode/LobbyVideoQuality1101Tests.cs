using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ChoSiren.Tests
{
    /// <summary>
    /// 2026-11-01 video-hq (用户: "首页这些角色的动画是不是都是最高清晰度的，不要压缩画质"): the lobby idle clips stay at
    /// each source MOV's native resolution as hardware-decodable H.264 (High, level ≤ 5.1) with the index first
    /// (browsers can start before the file has arrived), and every homepage layer imports at full resolution
    /// without Crunch on every platform (Tools/lobbyvideo/export_hq.sh, NativePortraitQuality1028).
    /// </summary>
    public sealed class LobbyVideoQuality1101Tests
    {
        private const string Clips = "Assets/StreamingAssets/LobbyIdols/";
        private const string Layers = "Assets/Resources/Art/Reference038/";

        // Stacked width = 2 × the source MOV's colour width; never scaled down, never up.
        [TestCase("elf-a.mp4", 2496, 1664)]
        [TestCase("elf-b.mp4", 2496, 1664)]
        [TestCase("mermaid-a.mp4", 2360, 1760)]
        [TestCase("mermaid-b.mp4", 2360, 1760)]
        [TestCase("catalena-a.mp4", 2160, 1920)]
        [TestCase("catalena-b.mp4", 2160, 1920)]
        [TestCase("lilith-a.mp4", 2160, 1920)]
        [TestCase("lilith-b.mp4", 2160, 1920)]
        public void IdleClipIsNativeResolutionHighProfileFaststart(string file, int width, int height)
        {
            byte[] mp4 = File.ReadAllBytes(Clips + file);
            long moov = -1, mdat = -1;
            for (long at = 0; at + 8 <= mp4.Length;)
            {
                long size = (uint)(mp4[at] << 24 | mp4[at + 1] << 16 | mp4[at + 2] << 8 | mp4[at + 3]);
                string type = System.Text.Encoding.ASCII.GetString(mp4, (int)at + 4, 4);
                if (size == 1) size = (long)BitConverter.ToUInt64(mp4.Skip((int)at + 8).Take(8).Reverse().ToArray(), 0);
                if (type == "moov") moov = at;
                if (type == "mdat") mdat = at;
                if (size < 8) break;
                at += size;
            }
            Assert.That(moov, Is.GreaterThanOrEqualTo(0), file);
            Assert.That(mdat, Is.GreaterThan(moov), file + ": moov must precede mdat (faststart)");
            int avc1 = Find(mp4, "avc1", (int)moov);
            Assert.That(avc1, Is.GreaterThan(0), file + ": H.264 sample entry");
            int w = mp4[avc1 + 4 + 24] << 8 | mp4[avc1 + 4 + 25], h = mp4[avc1 + 4 + 26] << 8 | mp4[avc1 + 4 + 27];
            Assert.That(w, Is.EqualTo(width), file);
            Assert.That(h, Is.EqualTo(height), file);
            int avcC = Find(mp4, "avcC", avc1);
            Assert.That(avcC, Is.GreaterThan(0), file);
            Assert.That(mp4[avcC + 5], Is.EqualTo(100), file + ": High profile");
            Assert.That(mp4[avcC + 7], Is.InRange(40, 51), file + ": level 4.0–5.1");
        }

        [Test]
        public void EveryHomepageLayerImportsAtFullResolutionWithoutCrunch()
        {
            string[] paths = Directory.GetFiles(Layers, "home-layer-*.png").Select(p => p.Replace('\\', '/')).ToArray();
            Assert.That(paths.Length, Is.GreaterThanOrEqualTo(40));
            foreach (string path in paths)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.That(importer, Is.Not.Null, path);
                importer.GetSourceTextureWidthAndHeight(out int width, out int height);
                Assert.That(importer.mipmapEnabled, Is.False, path);
                Assert.That(importer.npotScale, Is.EqualTo(TextureImporterNPOTScale.None), path);
                foreach (var (platform, format) in new[]
                         {
                             ("Standalone", TextureImporterFormat.BC7), ("Android", TextureImporterFormat.ASTC_4x4),
                             ("iOS", TextureImporterFormat.ASTC_4x4), ("WebGL", TextureImporterFormat.RGBA32),
                         })
                {
                    var settings = importer.GetPlatformTextureSettings(platform);
                    string at = path + " " + platform;
                    Assert.That(settings.overridden, Is.True, at);
                    Assert.That(settings.format, Is.EqualTo(format), at);
                    Assert.That(settings.maxTextureSize, Is.GreaterThanOrEqualTo(Math.Max(width, height)), at);
                    Assert.That(settings.crunchedCompression, Is.False, at);
                }
            }
        }

        /// <summary>The texture the importer really produced for the active target (other targets: settings above).</summary>
        [Test]
        public void EveryHomepageLayerIsImportedAtSourceSizeForTheActiveTarget()
        {
            var log = new System.Text.StringBuilder("active target " + EditorUserBuildSettings.activeBuildTarget + "\n");
            foreach (string path in Directory.GetFiles(Layers, "home-layer-*.png").Select(p => p.Replace('\\', '/')))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.GetSourceTextureWidthAndHeight(out int width, out int height);
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Assert.That(texture, Is.Not.Null, path);
                log.AppendLine($"{Path.GetFileName(path)} source {width}x{height} imported {texture.width}x{texture.height} {texture.format} mips={texture.mipmapCount}");
                Assert.That(texture.width, Is.EqualTo(width), path);
                Assert.That(texture.height, Is.EqualTo(height), path);
                Assert.That(texture.mipmapCount, Is.EqualTo(1), path);
            }
            Directory.CreateDirectory("Builds/qa-logs/zj-1101b/video-hq");
            File.WriteAllText("Builds/qa-logs/zj-1101b/video-hq/imported-textures.txt", log.ToString());
        }

        private static int Find(byte[] data, string tag, int from)
        {
            byte a = (byte)tag[0], b = (byte)tag[1], c = (byte)tag[2], d = (byte)tag[3];
            for (int i = Math.Max(0, from); i + 4 <= data.Length; i++)
                if (data[i] == a && data[i + 1] == b && data[i + 2] == c && data[i + 3] == d) return i;
            return -1;
        }
    }
}
