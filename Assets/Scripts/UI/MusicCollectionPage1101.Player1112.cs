using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.UI
{
    /// <summary>
    /// 2026-11-12 新播放器 on the 唱片机 page: the artist's player (<see cref="MusicPlayer1112"/>) at the board's full width,
    /// with the 收藏 / 设为首页曲 chips under it. Every control shows hover (teal halo), press and release feedback.
    /// </summary>
    public sealed partial class MusicCollectionPage1101
    {
        private const float PlayerY = 100f, PlayerK = BoardWidth / MusicPlayer1112.Width;

        private MusicPlayer1112 player;
        private RectTransform favChip, homeChip;
        private MusicQuad1101 favQuad, homeQuad;
        private Text favLabel, homeLabel;

        public RectTransform Player => player != null ? player.Rect : null;
        public string ShownTitle => player != null ? player.ShownTitle : null;
        public string ShownElapsed => player != null ? player.ShownElapsed : null;
        public string ShownDuration => player != null ? player.ShownDuration : null;
        public float ShownProgress => player != null ? player.ShownProgress : 0f;

        private void BuildPlayer1112()
        {
            player = MusicPlayer1112.Create(board, "Player1112", audio, model, font, toast, userGesture, false);
            player.Place(0f, PlayerY, PlayerK);

            // Favourite and 首页曲 chips under the player (right aligned, clear of the round button).
            float chipY = PlayerY + MusicPlayer1112.Height * PlayerK + 6f;
            homeChip = NewChip("HomeSongChip", board, "设为首页曲", out homeQuad, out homeLabel, SetHomeSong);
            favChip = NewChip("FavoriteChip", board, "♥ 收藏", out favQuad, out favLabel, ToggleFavorite);
            homeChip.anchoredPosition = new Vector2(818f - homeChip.sizeDelta.x, -chipY);
            favChip.anchoredPosition = new Vector2(818f - homeChip.sizeDelta.x - 12f - favChip.sizeDelta.x, -chipY);
        }

        public void Next() => player.Next();
        public void Previous() => player.Previous();
        public void ToggleSingleLoop() => player.ToggleSingleLoop();
        public void ToggleShuffle() => player.ToggleShuffle();

        /// <summary>The chips follow the current song (the player refreshes itself).</summary>
        private void RefreshNowPlaying()
        {
            if (favQuad == null) return;
            HomeMusicPlaylist.Track track = HomeMusicPlaylist.Get(CurrentIndex);
            bool fav = model != null && model.IsMusicFavorite(track.Id);
            // The lobby opens on this song (saved choice, or the first song when none was chosen).
            bool home = HomeMusicPlaylist.StartIndex(model) == CurrentIndex;
            StyleChip(favQuad, favLabel, fav ? Pink : ChipOff, fav ? Color.white : (Color)ChipOffText);
            StyleChip(homeQuad, homeLabel, home ? Teal : ChipOff, home ? (Color)Ink : ChipOffText);
        }
    }
}
