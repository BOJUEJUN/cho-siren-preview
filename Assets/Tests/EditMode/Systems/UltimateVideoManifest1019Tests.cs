using NUnit.Framework;

namespace ChoSiren.Tests
{
    public sealed class UltimateVideoManifest1019Tests
    {
        [TestCase("charm/finale.mp4", true)]
        [TestCase("mermaid.mp4", true)]
        [TestCase("../other.mp4", false)]
        [TestCase("https://example.com/clip.mp4", false)]
        [TestCase("file:///tmp/clip.mp4", false)]
        [TestCase("/absolute/clip.mp4", false)]
        [TestCase("clip.mp4?download=1", false)]
        [TestCase("poster.png", false)]
        public void OnlyRelativeLocalVideoFilesAreEligible(string path, bool expected)
            => Assert.That(BattleUltimateVideo1019.IsLocalClipPath(path), Is.EqualTo(expected));
    }
}
