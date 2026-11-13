using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>
    /// 2026-11-01 「A · 唱片收藏」 lobby entry. 2026-11-12: the lobby bar is now the artist's player (BuildHomeMusicDisc):
    /// tapping the record plays/pauses, tapping its black panel opens the record collection.
    /// </summary>
    public sealed partial class ChoSirenApp
    {
        private UI.MusicCollectionPage1101 musicPage1101;

        /// <summary>Opens the full-screen record collection (no-op while one is open).</summary>
        private void OpenMusicCollection1101()
        {
            if (safeRoot == null || (musicPage1101 != null && !musicPage1101.IsClosing)) return;
            musicPage1101 = UI.MusicCollectionPage1101.Open((RectTransform)safeRoot, gameAudio, model, font, Toast,
                ResumeMediaAfterUserGesture, psdHome);
        }

        private void ToggleHomeMusicFromDisc1101()
        {
            if (psdOpening || psdMotion == null || psdMotion.UiHidden) return;
            gameAudio?.PlayClick();
            ResumeMediaAfterUserGesture();
            if (gameAudio != null && gameAudio.ToggleHomeMusicPaused() && !gameAudio.MusicEnabled)
                Toast("音乐已在设置中关闭");
        }

    }
}
