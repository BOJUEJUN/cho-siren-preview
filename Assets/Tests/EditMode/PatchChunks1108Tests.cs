using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace ChoSiren.Tests
{
    /// <summary>2026-11-08: the in-game chunker must cut exactly where Tools/patch/cdc.swift cuts (small patches).</summary>
    public sealed class PatchChunks1108Tests
    {
        [Test]
        public void ChunkBoundariesMatchThePublisher()
        {
            // Reference from: cdc Assets/StreamingAssets/Practice1107/sing.mp4
            long[,] expected =
            {
                { 0, 2045864 }, { 2045864, 880593 }, { 2926457, 603804 }, { 3530261, 973430 },
                { 4503691, 426090 }, { 4929781, 1198008 }, { 6127789, 395031 }, { 6522820, 1051084 },
            };
            string path = Path.Combine(Application.dataPath, "StreamingAssets/Practice1107/sing.mp4");
            using (var stream = File.OpenRead(path))
            {
                var chunks = ChoSiren.AppUpdate1022.Cdc.Split(stream);
                Assert.That(chunks.Count, Is.EqualTo(expected.GetLength(0)));
                for (int i = 0; i < chunks.Count; i++)
                {
                    Assert.That(chunks[i].Offset, Is.EqualTo(expected[i, 0]), "chunk " + i + " offset");
                    Assert.That(chunks[i].Size, Is.EqualTo(expected[i, 1]), "chunk " + i + " size");
                }
            }
        }

        [Test]
        public void ChunksStayWithinBoundsAndCoverTheWholeStream()
        {
            var data = new byte[9 * 1024 * 1024 + 123];
            var random = new System.Random(1108);
            random.NextBytes(data);
            var chunks = ChoSiren.AppUpdate1022.Cdc.Split(new MemoryStream(data));
            long next = 0;
            for (int i = 0; i < chunks.Count; i++)
            {
                Assert.That(chunks[i].Offset, Is.EqualTo(next));
                Assert.That(chunks[i].Size, Is.LessThanOrEqualTo(ChoSiren.AppUpdate1022.Cdc.MaxSize));
                if (i < chunks.Count - 1) Assert.That(chunks[i].Size, Is.GreaterThanOrEqualTo(ChoSiren.AppUpdate1022.Cdc.MinSize));
                next += chunks[i].Size;
            }
            Assert.That(next, Is.EqualTo(data.Length));
        }
    }
}
