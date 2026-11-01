using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace ChoSiren
{
    /// <summary>Optional, explicitly approved original-character local clips. Missing media uses the existing accent.</summary>
    public sealed class BattleUltimateVideo1019 : MonoBehaviour
    {
        [Serializable] public sealed class ClipEntry
        {
            public string characterId, path;
            public bool approvedOriginalArt;
            public float durationSeconds = 1.3f;
        }
        [Serializable] public sealed class ClipManifest { public ClipEntry[] entries; }
        private ClipManifest manifest;
        private RectTransform screen;
        private RawImage image;
        private VideoPlayer player;
        private RenderTexture target;
        private Func<bool> paused;
        private Action fallback;
        private string activeUrl;
        private float wait, elapsed, duration;
        private bool pending, showing, resume;

        public void Configure(Transform host, Func<bool> isPaused)
        {
            paused = isPaused;
            TextAsset asset = Resources.Load<TextAsset>("UltimateVideos/manifest");
            if (asset != null)
            {
                try { manifest = JsonUtility.FromJson<ClipManifest>(asset.text); }
                catch (ArgumentException) { manifest = null; }
            }
            var go = new GameObject("OptionalOriginalUltimateVideo", typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(host, false); screen = (RectTransform)go.transform;
            screen.anchorMin = screen.anchorMax = new Vector2(.5f, .65f); screen.pivot = Vector2.one * .5f;
            screen.sizeDelta = new Vector2(540, 304);
            image = go.GetComponent<RawImage>(); image.raycastTarget = false;
            screen.gameObject.SetActive(false);
            player = gameObject.AddComponent<VideoPlayer>(); player.playOnAwake = false; player.isLooping = false;
            player.source = VideoSource.Url; player.waitForFirstFrame = true; player.skipOnDrop = true;
            // This optional accent does not introduce unknown audio or override the selected game BGM.
            player.audioOutputMode = VideoAudioOutputMode.None;
            player.renderMode = VideoRenderMode.RenderTexture;
            player.prepareCompleted += Prepared; player.errorReceived += Failed;
        }

        public static bool IsLocalClipPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || path.Contains("..") || path.Contains(":") || path.Contains("\\") ||
                path.StartsWith("/", StringComparison.Ordinal) || path.Contains("?") || path.Contains("#")) return false;
            return path.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase);
        }

        public bool TryPlay(string characterId, Action onUnavailable)
        {
            Cancel();
            if (player == null || manifest?.entries == null || string.IsNullOrEmpty(characterId)) return false;
            ClipEntry selected = null;
            foreach (ClipEntry entry in manifest.entries)
                if (entry != null && entry.characterId == characterId && entry.approvedOriginalArt && IsLocalClipPath(entry.path))
                { selected = entry; break; }
            if (selected == null) return false;
            string path = Application.streamingAssetsPath.TrimEnd('/') + "/UltimateVideos/" + selected.path;
#if !UNITY_WEBGL
            if (!File.Exists(path)) return false;
            path = new Uri(Path.GetFullPath(path)).AbsoluteUri;
#endif
            activeUrl = path; fallback = onUnavailable; pending = true; wait = elapsed = 0;
            duration = Mathf.Clamp(selected.durationSeconds, .25f, 1.4f);
            if (target == null)
            {
                target = new RenderTexture(960, 540, 0, RenderTextureFormat.ARGB32);
                target.Create(); image.texture = target; player.targetTexture = target;
            }
            player.url = path; player.Prepare(); return true;
        }

        private void Prepared(VideoPlayer source)
        {
            if (!pending || source.url != activeUrl) return;
            pending = false; showing = true;
            float ratio = source.width > 0 && source.height > 0 ? source.width / (float)source.height : 16f / 9;
            screen.sizeDelta = ratio >= 1.4f ? new Vector2(540, 540 / ratio) : new Vector2(304 * ratio, 304);
            source.aspectRatio = VideoAspectRatio.FitInside;
            screen.gameObject.SetActive(true); screen.SetAsLastSibling(); source.Play();
        }

        private void Failed(VideoPlayer source, string error)
        {
            if ((!pending && !showing) || source.url != activeUrl) return;
            Unavailable();
        }

        private void Update()
        {
            if (!pending && !showing) return;
            if (paused?.Invoke() ?? false)
            {
                if (showing && player.isPlaying) { player.Pause(); resume = true; }
                return;
            }
            if (resume) { resume = false; player.Play(); }
            float dt = Mathf.Min(Time.unscaledDeltaTime, .1f);
            if (pending) { wait += dt; if (wait >= .7f) Unavailable(); return; }
            elapsed += dt;
            if (elapsed >= duration || (!player.isPlaying && !resume)) Cancel();
        }

        private void Unavailable() { var callback = fallback; Cancel(); callback?.Invoke(); }
        public void Cancel()
        {
            pending = showing = resume = false; fallback = null; activeUrl = null;
            if (player != null) player.Stop();
            if (screen != null) screen.gameObject.SetActive(false);
        }
        private void OnDisable() => Cancel();
        private void OnDestroy()
        {
            Cancel();
            if (player != null) { player.prepareCompleted -= Prepared; player.errorReceived -= Failed; Destroy(player); }
            if (screen != null) Destroy(screen.gameObject);
            if (target != null) { target.Release(); Destroy(target); }
        }
    }
}
