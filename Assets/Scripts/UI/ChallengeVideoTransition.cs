using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace ChoSiren.UI
{
    /// <summary>
    /// Every stage transition plays the owner's clip (2026-10-20: "统一弄成我之前给你那个视频"):
    /// StreamingAssets/Transitions/challenge-cut.mp4 — black slashes, then the idol with the
    /// CHO-SIREN title. It fades in over the page, the scene switches under full cover, the clip
    /// plays out and then fades away onto the new page. If the clip cannot start in time the scene
    /// still switches (the game never waits on a video).
    /// </summary>
    public sealed class ChallengeVideoTransition : MonoBehaviour
    {
        public const string ClipFile = "Transitions/challenge-cut.mp4";
        /// <summary>Kept for older callers.</summary>
        public const string ActionCgResource = "Art/Story/Portraits1019/xingli";
        public const float CoverTime = .18f, SplitStart = 1.28f, Duration = 1.6f;
        public const float ArtWaitTimeout = .8f;
        static int coveringCount;
        bool registeredCover, playing, switched, returning, retired, waitingForClip;
        float elapsed, waited;
        int cue;
        RectTransform root, coverRecord;
        RawImage screen;
        Image backing;
        VideoPlayer player;
        RenderTexture texture;
        Action switchScene;
        Action<string> sfx;
        public static bool IsCoveringGame => coveringCount > 0;
        public bool IsReady => root != null;
        public bool IsPlaying => playing;
        public bool HasSwitched => switched;
        public float Clock => elapsed;
        public RectTransform Record => coverRecord;

        public static ChallengeVideoTransition Warm(Transform owner)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            foreach (var existing in owner.GetComponentsInChildren<ChallengeVideoTransition>(true))
                if (!existing.retired) return existing;
            var go = new GameObject("ChallengeVideoTransition", typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(owner, false);
            var fx = go.AddComponent<ChallengeVideoTransition>(); fx.Build(); return fx;
        }

        static string ClipUrl()
        {
            string url = Application.streamingAssetsPath.TrimEnd('/') + "/" + ClipFile;
#if !UNITY_WEBGL || UNITY_EDITOR
            if (!url.Contains("://")) url = new Uri(System.IO.Path.GetFullPath(url)).AbsoluteUri;
#endif
            return url;
        }

        void Build()
        {
            root = (RectTransform)transform; Stretch(root);
            var group = GetComponent<CanvasGroup>(); group.alpha = 0; group.blocksRaycasts = false; group.interactable = false;
            var blocker = gameObject.AddComponent<Image>(); blocker.color = new Color(0, 0, 0, .004f); blocker.raycastTarget = true;
            backing = NewChild<Image>("Backing"); backing.color = new Color32(8, 5, 14, 255); Stretch(backing.rectTransform);
            screen = NewChild<RawImage>("Clip"); screen.color = Color.white;
            Center(screen.rectTransform);
            coverRecord = screen.rectTransform;
            if (Application.isPlaying)
            {
                player = gameObject.AddComponent<VideoPlayer>();
                player.playOnAwake = false; player.isLooping = false; player.skipOnDrop = true; player.waitForFirstFrame = true;
                player.source = VideoSource.Url; player.url = ClipUrl();
                player.renderMode = VideoRenderMode.RenderTexture;
                player.audioOutputMode = VideoAudioOutputMode.None;
                player.prepareCompleted += source =>
                {
                    if (texture == null)
                    {
                        texture = new RenderTexture((int)Mathf.Max(2, source.width), (int)Mathf.Max(2, source.height), 0);
                        texture.Create();
                    }
                    source.targetTexture = texture;
                    screen.texture = texture;
                };
                player.Prepare();
            }
            Pose();
        }

        T NewChild<T>(string name) where T : Graphic
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(T));
            go.transform.SetParent(transform, false);
            var g = go.GetComponent<T>(); g.raycastTarget = false; return g;
        }

        public void Play(Transform host, Action openScene, Action<string> playSfx, bool returnToLobby = false)
        {
            if (this == null || retired || playing || IsCoveringGame || host == null) return;
            transform.SetParent(host, false); transform.SetAsLastSibling(); Stretch(root);
            gameObject.SetActive(true); enabled = true;
            switchScene = openScene; sfx = playSfx; returning = returnToLobby;
            playing = true; switched = false; elapsed = 0; cue = 0; waited = 0;
            waitingForClip = player != null && !player.isPrepared;
            if (!isActiveAndEnabled) { Finish(); return; }
            RegisterCover();
            var group = GetComponent<CanvasGroup>(); group.alpha = 0; group.blocksRaycasts = true;
            if (!Application.isPlaying || BattlePresentationOptions.ReduceMotion || player == null) { Finish(); return; }
            if (!waitingForClip) StartClip();
            Pose();
        }

        void StartClip()
        {
            player.time = 0; player.Play();
        }

        void Update() => Advance(Mathf.Min(Time.unscaledDeltaTime, .05f));
        public void Advance(float seconds)
        {
            if (!playing || seconds < 0 || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            if (waitingForClip)
            {
                waited += seconds;
                if (player != null && player.isPrepared) { waitingForClip = false; StartClip(); }
                else if (waited >= ArtWaitTimeout) { Finish(); return; }
                else return;
            }
            elapsed += seconds;
            Pose();
            if (!switched && elapsed >= CoverTime && !SwitchScene()) { Finish(); return; }
            if (this == null || !playing) return;
            float[] at = { 0, CoverTime };
            string[] names = { "cut", "stamp" };
            while (cue < at.Length && elapsed >= at[cue])
            {
                string sound = names[cue++];
                try { sfx?.Invoke(sound); } catch (Exception e) { Debug.LogWarning("Transition sound skipped: " + e.Message); }
                if (this == null || !playing) return;
            }
            if (elapsed >= Duration) Finish();
        }

        bool SwitchScene()
        {
            if (switched) return true;
            switched = true; var callback = switchScene; switchScene = null;
            try { callback?.Invoke(); }
            catch (Exception e) { Debug.LogException(e); return false; }
            if (this != null) transform.SetAsLastSibling();
            return true;
        }

        void Finish()
        {
            if (retired) return;
            retired = true;
            if (!switched) SwitchScene();
            if (this == null) return;
            playing = false; ReleaseCover();
            if (player != null) player.Stop();
            var group = GetComponent<CanvasGroup>();
            if (group != null) { group.alpha = 0; group.blocksRaycasts = false; }
            if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
        }

        void RegisterCover() { if (registeredCover) return; registeredCover = true; coveringCount++; }
        void ReleaseCover() { if (!registeredCover) return; registeredCover = false; coveringCount = Mathf.Max(0, coveringCount - 1); }
        void OnDisable()
        {
            ReleaseCover(); playing = false; switchScene = null; sfx = null;
            var group = GetComponent<CanvasGroup>(); if (group != null) { group.alpha = 0; group.blocksRaycasts = false; }
        }
        void OnDestroy()
        {
            ReleaseCover();
            if (texture != null) { texture.Release(); Destroy(texture); }
        }

        void Pose()
        {
            if (root == null || screen == null) return;
            var group = GetComponent<CanvasGroup>();
            float fadeIn = Mathf.Clamp01(elapsed / CoverTime);
            float fadeOut = 1f - Mathf.Clamp01((elapsed - SplitStart) / (Duration - SplitStart));
            if (group != null && playing) group.alpha = Mathf.Min(fadeIn, fadeOut);
            // Cover the screen with the clip, keeping its aspect (crop the overflow).
            float w = Mathf.Max(1, root.rect.width), h = Mathf.Max(1, root.rect.height);
            float vw = player != null && player.width > 0 ? player.width : 704f, vh = player != null && player.height > 0 ? player.height : 1440f;
            float scale = Mathf.Max(w / vw, h / vh);
            screen.rectTransform.sizeDelta = new Vector2(vw * scale, vh * scale);
            screen.rectTransform.localScale = Vector3.one * (returning ? 1f : Mathf.Lerp(1.06f, 1f, fadeIn));
            screen.enabled = screen.texture != null;
        }

        static void Center(RectTransform rect) => rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    }
}
