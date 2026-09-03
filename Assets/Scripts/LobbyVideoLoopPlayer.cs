using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace ChoSiren
{
    /// <summary>
    /// Streams the combined lobby character/background movie and crops it to the
    /// current portrait viewport without stretching. The source stays outside the
    /// asset database so Windows and WebGL can both play the same small MP4 URL.
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public sealed class LobbyVideoLoopPlayer : MonoBehaviour
    {
        public event Action MusicAvailabilityChanged;

        private RawImage surface;
        private VideoPlayer player;
        private RenderTexture target;
        private Action readyCallback;
        private Action<string> errorCallback;
        private bool initialized;
        private bool waitingForPrepare;
        private bool loopRequested;
        private bool musicEnabled = true;
        private bool audioUnlocked;
        private float prepareDeadline;
        private Vector2 lastSurfaceSize;
        private bool hasReportedMusicAvailability;
        private bool lastReportedMusicAvailability;

        public bool IsReady => player != null && player.isPrepared;
        public bool HasAudioTrack => IsReady && player.audioTrackCount > 0;
        public bool HasEnabledAudioTrack => HasAudioTrack && player.IsAudioTrackEnabled(0);
        public bool IsVideoAudioMuted => !HasAudioTrack || player.GetDirectAudioMute(0);
        public bool CanProvideMusic => ShouldVideoOwnMusic(
            musicEnabled,
            audioUnlocked,
            loopRequested,
            IsReady,
            player != null && player.isPlaying,
            HasEnabledAudioTrack,
            IsVideoAudioMuted);

        public void StartLoop(Action onReady, Action<string> onError)
        {
            readyCallback = onReady;
            errorCallback = onError;
            loopRequested = true;
            gameObject.SetActive(true);

            if (!initialized) InitializePlayer();
            if (player.isPrepared)
            {
                surface.enabled = true;
                UpdateCrop();
                ApplyAudioState();
                if (loopRequested && !player.isPlaying) player.Play();
                NotifyMusicAvailabilityIfChanged();
                readyCallback?.Invoke();
                return;
            }

            waitingForPrepare = true;
            prepareDeadline = Time.realtimeSinceStartup + 6f;
            player.Prepare();
        }

        public void PauseLoop()
        {
            loopRequested = false;
            ApplyAudioState();
            if (player != null && player.isPlaying) player.Pause();
            NotifyMusicAvailabilityIfChanged();
        }

        public void SetMusicEnabled(bool enabled)
        {
            musicEnabled = enabled;
            ApplyAudioState();
            NotifyMusicAvailabilityIfChanged();
        }

        public void ResumeAudioAfterUserGesture()
        {
            audioUnlocked = true;
            ApplyAudioState();
            if (loopRequested && player != null && player.isPrepared && !player.isPlaying) player.Play();
            NotifyMusicAvailabilityIfChanged();
        }

        private void InitializePlayer()
        {
            initialized = true;
            surface = GetComponent<RawImage>();
            surface.raycastTarget = false;
            surface.enabled = false;
            audioUnlocked = Application.platform != RuntimePlatform.WebGLPlayer;

            player = gameObject.AddComponent<VideoPlayer>();
            player.playOnAwake = false;
            player.waitForFirstFrame = true;
            player.isLooping = true;
            player.skipOnDrop = true;
            player.renderMode = VideoRenderMode.RenderTexture;
            player.audioOutputMode = VideoAudioOutputMode.Direct;
            player.controlledAudioTrackCount = 1;
            player.EnableAudioTrack(0, true);
            player.SetDirectAudioVolume(0, GameAudio.MusicOutputVolume);
            player.SetDirectAudioMute(0, ShouldMuteVideoAudio(musicEnabled, audioUnlocked, loopRequested));
            player.source = VideoSource.Url;
            string root = Application.streamingAssetsPath.TrimEnd('/', '\\');
            player.url = $"{root}/Lobby/lobby-loop.mp4";
            player.prepareCompleted += HandlePrepared;
            player.started += HandleStarted;
            player.errorReceived += HandleError;
        }

        private void HandlePrepared(VideoPlayer source)
        {
            waitingForPrepare = false;
            Vector2Int renderSize = CalculateRenderTextureSize((int)source.width, (int)source.height);
            int width = renderSize.x;
            int height = renderSize.y;
            if (target == null || target.width != width || target.height != height)
            {
                ReleaseTarget();
                target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
                {
                    name = "CHO-SIREN Lobby Loop",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                };
                target.Create();
                source.targetTexture = target;
                surface.texture = target;
            }

            ApplyAudioState();
            if (!loopRequested)
            {
                NotifyMusicAvailabilityIfChanged();
                return;
            }
            surface.enabled = true;
            UpdateCrop();
            if (loopRequested) source.Play();
            NotifyMusicAvailabilityIfChanged();
            readyCallback?.Invoke();
        }

        private void HandleStarted(VideoPlayer source)
        {
            ApplyAudioState();
            NotifyMusicAvailabilityIfChanged();
        }

        private void HandleError(VideoPlayer source, string message)
        {
            waitingForPrepare = false;
            surface.enabled = false;
            NotifyMusicAvailabilityIfChanged();
            errorCallback?.Invoke(string.IsNullOrEmpty(message) ? "首页动画载入失败" : message);
        }

        private void Update()
        {
            if (waitingForPrepare && Time.realtimeSinceStartup >= prepareDeadline)
            {
                waitingForPrepare = false;
                player.Stop();
                surface.enabled = false;
                NotifyMusicAvailabilityIfChanged();
                errorCallback?.Invoke("首页动画准备超时，已使用静态背景");
                return;
            }

            NotifyMusicAvailabilityIfChanged();
            if (!IsReady || surface == null) return;
            Vector2 size = surface.rectTransform.rect.size;
            if ((size - lastSurfaceSize).sqrMagnitude < 0.25f) return;
            UpdateCrop();
        }

        private void UpdateCrop()
        {
            if (surface == null || player == null || player.width == 0 || player.height == 0) return;
            Vector2 size = surface.rectTransform.rect.size;
            if (size.x <= 1f || size.y <= 1f) return;
            lastSurfaceSize = size;
            surface.uvRect = CalculateCoverUvRect(size.x, size.y, player.width, player.height);
        }

        private void ApplyAudioState()
        {
            if (player == null) return;
            player.SetDirectAudioMute(0, ShouldMuteVideoAudio(musicEnabled, audioUnlocked, loopRequested));
        }

        public static bool ShouldMuteVideoAudio(bool enabled, bool unlocked, bool playingOnLobby) =>
            !enabled || !unlocked || !playingOnLobby;

        public static bool ShouldVideoOwnMusic(bool enabled, bool unlocked, bool playingOnLobby,
            bool prepared, bool playing, bool hasEnabledAudioTrack, bool muted) =>
            enabled && unlocked && playingOnLobby && prepared && playing && hasEnabledAudioTrack && !muted;

        private void NotifyMusicAvailabilityIfChanged()
        {
            bool available = CanProvideMusic;
            if (hasReportedMusicAvailability && available == lastReportedMusicAvailability) return;
            hasReportedMusicAvailability = true;
            lastReportedMusicAvailability = available;
            MusicAvailabilityChanged?.Invoke();
        }

        public static Vector2Int CalculateRenderTextureSize(int sourceWidth, int sourceHeight, int maxDimension = 2048)
        {
            int width = sourceWidth > 0 ? sourceWidth : 720;
            int height = sourceHeight > 0 ? sourceHeight : 1536;
            int limit = Mathf.Max(2, maxDimension);
            float scale = Mathf.Min(1f, limit / (float)Mathf.Max(width, height));
            return new Vector2Int(
                Mathf.Max(2, Mathf.RoundToInt(width * scale)),
                Mathf.Max(2, Mathf.RoundToInt(height * scale)));
        }

        public static Rect CalculateCoverUvRect(float surfaceWidth, float surfaceHeight, float videoWidth, float videoHeight)
        {
            if (surfaceWidth <= 0f || surfaceHeight <= 0f || videoWidth <= 0f || videoHeight <= 0f)
                return new Rect(0f, 0f, 1f, 1f);

            float surfaceAspect = surfaceWidth / surfaceHeight;
            float videoAspect = videoWidth / videoHeight;
            if (videoAspect > surfaceAspect)
            {
                float width = surfaceAspect / videoAspect;
                return new Rect((1f - width) * 0.5f, 0f, width, 1f);
            }

            float height = videoAspect / surfaceAspect;
            return new Rect(0f, (1f - height) * 0.5f, 1f, height);
        }

        private void OnDestroy()
        {
            if (player != null)
            {
                player.prepareCompleted -= HandlePrepared;
                player.started -= HandleStarted;
                player.errorReceived -= HandleError;
                player.targetTexture = null;
            }
            ReleaseTarget();
        }

        private void ReleaseTarget()
        {
            if (target == null) return;
            target.Release();
            Destroy(target);
            target = null;
        }
    }
}
