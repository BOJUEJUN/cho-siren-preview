using System;
using UnityEngine;

namespace ChoSiren
{
    /// <summary>
    /// 2026-11-01: data-driven song list (Resources/Data/music-playlist.json). Adding a song is one line in
    /// that file: id, title, artist, cover (Resources path), file (StreamingAssets/Music name), optional native
    /// Resources clip, categories, isNew, unlock. Files are immutable and loaded only when selected.
    /// </summary>
    public static class HomeMusicPlaylist
    {
        public const string DataPath = "Data/music-playlist";
        public const string NativeHomeClipPath = "Audio/bgm";

        public enum PlayMode { ListLoop = 0, SingleLoop = 1, Shuffle = 2 }
        public static readonly string[] PlayModeNames = { "列表循环", "单曲循环", "随机播放" };

        public readonly struct Track
        {
            public readonly string Id, Title, Artist, Cover, FileName, Clip, Unlock, UnlockHint, BookmarkKey;
            public readonly string[] Categories;
            public readonly bool IsNew;
            public readonly float Duration;

            public Track(string id, string title, string fileName) : this(id, title, string.Empty, string.Empty,
                fileName, string.Empty, null, false, string.Empty, string.Empty, 0f) { }

            public Track(string id, string title, string artist, string cover, string fileName, string clip,
                string[] categories, bool isNew, string unlock, string unlockHint, float duration)
            {
                Id = id; Title = title; Artist = artist ?? string.Empty; Cover = cover ?? string.Empty;
                FileName = fileName; Clip = clip ?? string.Empty; Categories = categories ?? Array.Empty<string>();
                IsNew = isNew; Unlock = unlock ?? string.Empty; UnlockHint = unlockHint ?? string.Empty;
                Duration = Mathf.Max(0f, duration); BookmarkKey = "home:" + id;
            }

            public bool InCategory(string category)
            {
                foreach (string c in Categories) if (string.Equals(c, category, StringComparison.Ordinal)) return true;
                return false;
            }
        }

        [Serializable] private sealed class Entry
        {
            public string id, title, artist, cover, file, clip, unlock, unlockHint;
            public string[] categories;
            public bool isNew;
            public float duration;
        }
        [Serializable] private sealed class Document { public Entry[] tracks; }

        private static Track[] tracks;

        private static Track[] Tracks
        {
            get
            {
                if (tracks != null) return tracks;
                TextAsset json = Resources.Load<TextAsset>(DataPath);
                Document doc = json != null ? JsonUtility.FromJson<Document>(json.text) : null;
                var list = new System.Collections.Generic.List<Track>();
                if (doc?.tracks != null)
                    foreach (Entry e in doc.tracks)
                    {
                        if (e == null || string.IsNullOrEmpty(e.id) || string.IsNullOrEmpty(e.file)) continue;
                        if (list.Exists(t => t.Id == e.id)) { Debug.LogError("Duplicate music id: " + e.id); continue; }
                        list.Add(new Track(e.id, string.IsNullOrEmpty(e.title) ? e.id : e.title, e.artist, e.cover,
                            e.file, e.clip, e.categories, e.isNew, e.unlock, e.unlockHint, e.duration));
                    }
                if (list.Count == 0)
                {
                    // Never leave the lobby without its approved song if the list is missing or broken.
                    Debug.LogError("Music playlist missing or empty; using the approved home song.");
                    list.Add(new Track("my-light-my-rules", "光的规则", string.Empty, string.Empty,
                        "home-f19d89ea8124a31b.mp3", NativeHomeClipPath, new[] { "home" }, false, string.Empty, string.Empty, 172.38f));
                }
                return tracks = list.ToArray();
            }
        }

        public static int Count => Tracks.Length;

        /// <summary>Tests only: replace the list (null reloads the shipped file).</summary>
        public static void SetTracksForTesting(Track[] list) => tracks = list;

        /// <summary>The first song backed by the imported native soundtrack (Resources/Audio/bgm).</summary>
        public static int NativeResourceTrackIndex
        {
            get
            {
                Track[] all = Tracks;
                for (int i = 0; i < all.Length; i++) if (all[i].Clip == NativeHomeClipPath) return i;
                return 0;
            }
        }

        public static Track Get(int index)
        {
            Track[] all = Tracks;
            if (index < 0 || index >= all.Length) throw new ArgumentOutOfRangeException(nameof(index));
            return all[index];
        }

        public static int IndexOf(string id)
        {
            if (string.IsNullOrEmpty(id)) return -1;
            Track[] all = Tracks;
            for (int i = 0; i < all.Length; i++) if (all[i].Id == id) return i;
            return -1;
        }

        /// <summary>Unlock rule: empty = always; "stage:&lt;id&gt;", "story:&lt;id&gt;" or "flag:&lt;flag&gt;".</summary>
        public static bool IsUnlocked(int index, GameModel model)
        {
            string rule = Get(index).Unlock;
            if (string.IsNullOrEmpty(rule)) return true;
            if (model == null) return false;
            int colon = rule.IndexOf(':');
            if (colon <= 0) return false;
            string kind = rule.Substring(0, colon), value = rule.Substring(colon + 1);
            switch (kind)
            {
                case "stage": return model.IsStageCleared(value);
                case "story": return model.IsStoryCompleted(value);
                case "flag": return model.HasStoryFlag(value);
                default: return false;
            }
        }

        public static int NextIndex(int current) => NextIndex(current, null);

        /// <summary>The next unlocked song after <paramref name="current"/> (wrapping).</summary>
        public static int NextIndex(int current, GameModel model) => Step(current, 1, model);

        public static int PreviousIndex(int current, GameModel model) => Step(current, -1, model);

        private static int Step(int current, int direction, GameModel model)
        {
            Get(current); // Invalid selections must not silently become a different song.
            int count = Count;
            for (int k = 1; k <= count; k++)
            {
                int i = ((current + direction * k) % count + count) % count;
                if (IsUnlocked(i, model)) return i;
            }
            return current;
        }

        /// <summary>A random unlocked song other than the current one (the current one if it is alone).</summary>
        public static int ShuffleIndex(int current, GameModel model)
        {
            Get(current);
            int count = Count, available = 0;
            for (int i = 0; i < count; i++) if (i != current && IsUnlocked(i, model)) available++;
            if (available == 0) return current;
            int pick = UnityEngine.Random.Range(0, available);
            for (int i = 0; i < count; i++)
                if (i != current && IsUnlocked(i, model) && pick-- == 0) return i;
            return current;
        }

        /// <summary>
        /// 2026-11-08 (少爷: "进去还是光的规则…要随机的"): the song the game opens on. In shuffle mode (the default) it is a
        /// random unlocked song unless the player picked a 首页曲; otherwise <see cref="StartIndex"/>.
        /// </summary>
        public static int OpeningIndex(GameModel model)
        {
            if (model == null) return StartIndex(model);
            PlayMode mode = model.MusicPlayMode; // runs the one-time save migrations first
            bool chosen = IndexOf(model.Save.HomeMusicId) >= 0;
            if (chosen || mode != PlayMode.Shuffle) return StartIndex(model);
            int available = 0;
            for (int i = 0; i < Count; i++) if (IsUnlocked(i, model)) available++;
            if (available == 0) return StartIndex(model);
            int pick = UnityEngine.Random.Range(0, available);
            for (int i = 0; i < Count; i++)
                if (IsUnlocked(i, model) && pick-- == 0) return i;
            return StartIndex(model);
        }

        /// <summary>The saved 首页曲 when it exists and is unlocked, otherwise the first unlocked song.</summary>
        public static int StartIndex(GameModel model)
        {
            int saved = IndexOf(model?.Save.HomeMusicId);
            if (saved >= 0 && IsUnlocked(saved, model)) return saved;
            for (int i = 0; i < Count; i++) if (IsUnlocked(i, model)) return i;
            return 0;
        }

        public static string FormatTime(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0f) return "0:00";
            int s = Mathf.FloorToInt(seconds);
            return (s / 60) + ":" + (s % 60).ToString("00");
        }
    }
}
