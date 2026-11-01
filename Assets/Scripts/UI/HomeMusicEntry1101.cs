using UnityEngine;

namespace ChoSiren.UI
{
    /// <summary>
    /// 2026-11-01: the lobby record bar's live state — a thin teal progress line along the song window's
    /// bottom edge, and the torn-paper notice sticker on the record while a new song has not been played.
    /// A new song counts as heard once it actually plays. Per frame it only moves one anchor.
    /// </summary>
    public sealed class HomeMusicEntry1101 : MonoBehaviour
    {
        private GameAudio audio;
        private GameModel model;
        private RectTransform progress;
        private GameObject sticker;
        private float shown = -1f;
        private int pendingHeard = -1;

        public RectTransform Progress => progress;
        public GameObject Sticker => sticker;

        public void Bind(GameAudio transport, GameModel gameModel, RectTransform progressLine, GameObject newSticker)
        {
            Unsubscribe();
            audio = transport;
            model = gameModel;
            progress = progressLine;
            sticker = newSticker;
            Subscribe();
            Refresh();
        }

        private void OnEnable() { Subscribe(); Refresh(); }
        private void OnDisable() => Unsubscribe();
        private void OnDestroy() => Unsubscribe();

        private void Subscribe()
        {
            if (!isActiveAndEnabled) return;
            Unsubscribe();
            if (audio != null) audio.HomeMusicTrackChanged += TrackChanged;
            if (model != null) model.Changed += Refresh;
        }

        private void Unsubscribe()
        {
            if (audio != null) audio.HomeMusicTrackChanged -= TrackChanged;
            if (model != null) model.Changed -= Refresh;
        }

        private void TrackChanged(int index) => Refresh();

        /// <summary>Sticker visibility and which song still waits to be heard (on events only).</summary>
        public void Refresh()
        {
            bool anyNew = false;
            pendingHeard = -1;
            for (int i = 0; i < HomeMusicPlaylist.Count; i++)
            {
                HomeMusicPlaylist.Track track = HomeMusicPlaylist.Get(i);
                if (!track.IsNew || !HomeMusicPlaylist.IsUnlocked(i, model) || (model != null && model.IsMusicHeard(track.Id))) continue;
                anyNew = true;
                if (audio != null && i == audio.HomeMusicTrackIndex) pendingHeard = i;
            }
            if (sticker != null && sticker.activeSelf != anyNew) sticker.SetActive(anyNew);
        }

        private void Update()
        {
            if (audio == null) return;
            if (pendingHeard >= 0 && pendingHeard == audio.HomeMusicTrackIndex && audio.HomeMusicPlaying && model != null)
            {
                int index = pendingHeard;
                pendingHeard = -1;
                model.MarkMusicHeard(HomeMusicPlaylist.Get(index).Id);
            }
            if (progress == null) return;
            audio.TryGetHomeMusicProgress(out float time, out float length);
            float p = length > 0f ? Mathf.Clamp01(time / length) : 0f;
            if (Mathf.Abs(p - shown) < .0005f) return;
            shown = p;
            progress.anchorMax = new Vector2(p, progress.anchorMax.y);
        }
    }
}
