using System.Collections.Generic;

namespace ChoSiren
{
    /// <summary>
    /// 2026-11-01 唱片收藏: optional fields (no schema bump). Saves written before this simply lack them and
    /// read as "no favourites, default 首页曲, list loop, nothing heard yet".
    /// </summary>
    public sealed partial class GameSave
    {
        /// <summary>Favourite song ids (Data/music-playlist.json).</summary>
        public List<string> MusicFavorites = new List<string>();
        /// <summary>The song the lobby starts with; empty = the first song of the list.</summary>
        public string HomeMusicId = string.Empty;
        /// <summary><see cref="HomeMusicPlaylist.PlayMode"/>: 0 list loop, 1 single loop, 2 shuffle.</summary>
        public int MusicPlayMode;
        /// <summary>New songs (isNew) already played once; their notice sticker is gone.</summary>
        public List<string> MusicHeard = new List<string>();
    }

    public sealed partial class GameModel
    {
        public bool IsMusicFavorite(string id) => !string.IsNullOrEmpty(id) && Save.MusicFavorites != null && Save.MusicFavorites.Contains(id);

        public bool ToggleMusicFavorite(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            Save.MusicFavorites ??= new List<string>();
            bool now = !Save.MusicFavorites.Remove(id);
            if (now) Save.MusicFavorites.Add(id);
            SaveState();
            return now;
        }

        public void SetHomeMusicId(string id)
        {
            id ??= string.Empty;
            if (Save.HomeMusicId == id) return;
            Save.HomeMusicId = id;
            SaveState();
        }

        public HomeMusicPlaylist.PlayMode MusicPlayMode =>
            (HomeMusicPlaylist.PlayMode)UnityEngine.Mathf.Clamp(Save.MusicPlayMode, 0, 2);

        public void SetMusicPlayMode(HomeMusicPlaylist.PlayMode mode)
        {
            int value = UnityEngine.Mathf.Clamp((int)mode, 0, 2);
            if (Save.MusicPlayMode == value) return;
            Save.MusicPlayMode = value;
            SaveState();
        }

        public bool IsMusicHeard(string id) => !string.IsNullOrEmpty(id) && Save.MusicHeard != null && Save.MusicHeard.Contains(id);

        /// <summary>Records that a new song has been played; returns true when this changed the save.</summary>
        public bool MarkMusicHeard(string id)
        {
            if (string.IsNullOrEmpty(id) || IsMusicHeard(id)) return false;
            Save.MusicHeard ??= new List<string>();
            Save.MusicHeard.Add(id);
            SaveState();
            return true;
        }
    }
}
