using NUnit.Framework;
using UnityEngine;

namespace ChoSiren.Tests
{
    public sealed class LobbyMediaTests
    {
        [TestCase(720, 1536, 2048, 720, 1536)]
        [TestCase(3840, 2160, 2048, 2048, 1152)]
        [TestCase(2160, 3840, 2048, 1152, 2048)]
        [TestCase(0, 0, 2048, 720, 1536)]
        public void RenderTextureSizePreservesSourceAspectWithinLimit(
            int sourceWidth, int sourceHeight, int limit, int expectedWidth, int expectedHeight)
        {
            Vector2Int result = LobbyVideoLoopPlayer.CalculateRenderTextureSize(sourceWidth, sourceHeight, limit);

            Assert.That(result, Is.EqualTo(new Vector2Int(expectedWidth, expectedHeight)));
        }

        [Test]
        public void CoverCropCentersWideVideoWithoutStretching()
        {
            Rect crop = LobbyVideoLoopPlayer.CalculateCoverUvRect(1000f, 1000f, 1920f, 1080f);

            Assert.That(crop.x, Is.EqualTo(0.21875f).Within(0.0001f));
            Assert.That(crop.y, Is.Zero.Within(0.0001f));
            Assert.That(crop.width, Is.EqualTo(0.5625f).Within(0.0001f));
            Assert.That(crop.height, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void CoverCropCentersTallVideoWithoutStretching()
        {
            Rect crop = LobbyVideoLoopPlayer.CalculateCoverUvRect(1920f, 1080f, 720f, 1536f);

            Assert.That(crop.x, Is.Zero.Within(0.0001f));
            Assert.That(crop.y, Is.EqualTo(0.3681641f).Within(0.0001f));
            Assert.That(crop.width, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(crop.height, Is.EqualTo(0.2636719f).Within(0.0001f));
        }

        [TestCase(true, false, true)]
        [TestCase(true, true, false)]
        [TestCase(false, false, false)]
        [TestCase(false, true, false)]
        public void FallbackMusicOnlyPlaysWhenEnabledAndVideoDoesNotOwnLobbyMusic(
            bool musicEnabled, bool videoOwnsMusic, bool expected)
        {
            Assert.That(GameAudio.ShouldPlayFallbackMusic(musicEnabled, videoOwnsMusic), Is.EqualTo(expected));
        }

        [TestCase(true, true, true, false)]
        [TestCase(true, false, true, true)]
        [TestCase(true, true, false, true)]
        [TestCase(false, true, true, true)]
        public void VideoAudioRequiresMusicPermissionBrowserUnlockAndActiveLobby(
            bool musicEnabled, bool audioUnlocked, bool lobbyActive, bool expectedMuted)
        {
            Assert.That(LobbyVideoLoopPlayer.ShouldMuteVideoAudio(musicEnabled, audioUnlocked, lobbyActive),
                Is.EqualTo(expectedMuted));
        }
    }
}
