using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace ChoSiren
{
    /// <summary>
    /// Lobby idols (2026-10-25, 赵金: "让她们一直动 不点时候也动 点的时候可以替换 都是微小动作"): each lobby idol
    /// loops a short micro-motion clip and changes to her second clip when tapped. The clips are stacked-alpha
    /// MP4s (left colour, right alpha; Tools/lobbyvideo/matte.swift) in StreamingAssets/LobbyIdols, played by
    /// Unity's VideoPlayer on every platform and drawn as children of the portrait, so they follow its bob,
    /// parallax and the swipe between idols. The authored portrait (and its rim, which the clips carry) stays
    /// until a clip has a frame, and comes back if a clip cannot play.
    /// </summary>
    public sealed class PsdHomeIdolVideo1025 : MonoBehaviour
    {
        private static readonly string[][] Files =
        {
            new[] { "catalena-a.mp4", "catalena-b.mp4" },
            new[] { "lilith-a.mp4", "lilith-b.mp4" },
            new[] { "elf-a.mp4", "elf-b.mp4" },
            new[] { "mermaid-a.mp4", "mermaid-b.mp4" },
        };
        // Where each clip's frame sits on its portrait (fractions of the portrait rect: min x, min y, max x, max y),
        // registered on the clip's first frame against the portrait's alpha.
        private static readonly Vector4[][] Anchors =
        {
            new[] { new Vector4(-.0551f, -.0145f, 1.0976f, 1.0322f), new Vector4(-.0367f, .0072f, 1.0877f, 1.0282f) },
            new[] { new Vector4(.1473f, -.0291f, 1.0054f, 1.0209f), new Vector4(.1430f, -.0016f, .9933f, 1.0388f) },
            new[] { new Vector4(-.0541f, .0102f, 1.0663f, 1.0362f), new Vector4(-.0541f, .0102f, 1.0663f, 1.0362f) },
            new[] { new Vector4(-.1022f, -.0608f, .9591f, .9696f), new Vector4(-.1022f, -.0608f, .9591f, .9696f) },
        };
        private const float FadeSeconds = .35f;
        /// <summary>2026-11-04 (用户: "切到这个角色的时候有第一帧错误的感觉"): how long a newly shown idol keeps her still portrait
        /// hidden while her clip prepares. The still and the clip never line up exactly (pose, framing), so showing the still
        /// first made the figure jump when the clip took over; past this wait (or if the clip fails) the still shows.</summary>
        private const float ClipWaitSeconds = 1.5f;
        private float awaitingSince;

        private sealed class Clip
        {
            public VideoPlayer Player;
            public RenderTexture Texture;
            public RawImage Image;
            public bool Prepared, Failed;
            public float Alpha;
        }

        private readonly Clip[] clips = new Clip[2];
        private PsdHomeMotion motion;
        private RectTransform portrait;
        private Material material;
        private int idol = -1, current;
        private float shown, swipeAlpha = 1f, appliedHidden;

        /// <summary>True once the current idol's clip is on screen (taps then switch clips).</summary>
        public bool Showing => idol >= 0 && clips[current] != null && !clips[current].Failed && shown > 0f;

        public void Initialize(PsdHomeMotion owner)
        {
            motion = owner;
            Shader shader = Resources.Load<Shader>("Shaders/UIStackedAlpha");
            if (shader != null) material = new Material(shader) { name = "LobbyIdolStackedAlpha" };
        }

        /// <summary>Called whenever the carousel shows an idol; builds that idol's two clips under her portrait.</summary>
        public void ShowIdol(int index, RectTransform portraitRect)
        {
            if (material == null || index < 0 || index >= Files.Length) return;
            if (idol == index && portrait == portraitRect) return;
            Release();
            idol = index; portrait = portraitRect; current = 0; shown = 0f; awaitingSince = Time.unscaledTime;
            for (int i = 0; i < 2; i++) clips[i] = Build(i);
            clips[0].Player.Prepare();
            clips[1].Player.Prepare();
        }

        /// <summary>The carousel's swipe fades the clips with the portrait.</summary>
        public void SetSwipeAlpha(float alpha) => swipeAlpha = Mathf.Clamp01(alpha);

        /// <summary>Tap: the other clip starts from its first frame and fades in over this one.</summary>
        public bool Swap()
        {
            if (!Showing) return false;
            Clip next = clips[1 - current];
            if (next == null || next.Failed || !next.Prepared) return false;
            next.Player.time = 0; next.Player.Play();
            next.Alpha = 0f; next.Image.transform.SetAsLastSibling();
            current = 1 - current;
            return true;
        }

        private Clip Build(int which)
        {
            var clip = new Clip();
            var go = new GameObject("IdolClip-" + Files[idol][which], typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            go.transform.SetParent(portrait, false);
            clip.Image = go.GetComponent<RawImage>();
            clip.Image.raycastTarget = false; clip.Image.material = material; clip.Image.enabled = false;
            Vector4 a = Anchors[idol][which];
            RectTransform rect = clip.Image.rectTransform;
            rect.anchorMin = new Vector2(a.x, a.y); rect.anchorMax = new Vector2(a.z, a.w);
            rect.offsetMin = rect.offsetMax = Vector2.zero; rect.pivot = new Vector2(.5f, .5f);
            // On this always-active host: a VideoPlayer on a hidden object cannot prepare.
            VideoPlayer player = gameObject.AddComponent<VideoPlayer>();
            player.playOnAwake = false; player.isLooping = true; player.skipOnDrop = true; player.waitForFirstFrame = true;
            player.source = VideoSource.Url; player.renderMode = VideoRenderMode.RenderTexture;
            player.audioOutputMode = VideoAudioOutputMode.None;
            player.url = ClipUrl(Files[idol][which]);
            player.prepareCompleted += source =>
            {
                if (clip.Player != source) return;
                clip.Texture = new RenderTexture(Mathf.Max(2, (int)source.width), Mathf.Max(2, (int)source.height), 0,
                    RenderTextureFormat.ARGB32) { name = "LobbyIdol-" + source.url };
                clip.Texture.Create();
                source.targetTexture = clip.Texture;
                clip.Image.texture = clip.Texture;
                // In a linear project an sRGB target stores the grey alpha half linearised; the shader undoes it.
                clip.Image.material.SetFloat("_AlphaFromSrgb",
                    QualitySettings.activeColorSpace == ColorSpace.Linear && clip.Texture.sRGB ? 1f : 0f);
                clip.Prepared = true;
                if (clips[current] == clip) source.Play();
            };
            player.errorReceived += (source, message) =>
            {
                if (clip.Player != source) return;
                clip.Failed = true;
                Debug.LogWarning("CHO_SIREN_LOBBY_IDOL clip failed: " + source.url + " " + message);
            };
            clip.Player = player;
            return clip;
        }

        private static string ClipUrl(string file)
        {
            string url = Application.streamingAssetsPath.TrimEnd('/') + "/LobbyIdols/" + file;
#if !UNITY_WEBGL || UNITY_EDITOR
            // Native players need a file:// URI for local media.
            if (!url.Contains("://")) url = new System.Uri(System.IO.Path.GetFullPath(url)).AbsoluteUri;
#endif
            return url;
        }

        private void LateUpdate()
        {
            if (idol < 0 || motion == null) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, .1f);
            Clip live = clips[current];
            bool ready = live != null && !live.Failed && live.Prepared && live.Player.isPlaying &&
                (live.Player.frame > 0 || live.Player.time > .05);
            if (live != null && live.Prepared && !live.Failed && !live.Player.isPlaying) live.Player.Play();
            shown = Mathf.MoveTowards(shown, ready || (shown > 0f && live != null && !live.Failed) ? 1f : 0f, dt / FadeSeconds);
            // The incoming clip fades in over the outgoing one; the outgoing leaves only once it is covered,
            // so the figure never turns see-through halfway.
            Clip other = clips[1 - current];
            if (live != null) live.Alpha = Mathf.MoveTowards(live.Alpha, ready ? 1f : live.Alpha, dt / FadeSeconds);
            if (other != null && live != null && live.Alpha >= 1f && other.Alpha > 0f)
            {
                other.Alpha = 0f;
                if (other.Player.isPlaying) { other.Player.Pause(); other.Player.time = 0; }
            }
            for (int i = 0; i < 2; i++)
            {
                Clip clip = clips[i];
                if (clip == null) continue;
                float alpha = Smooth(shown) * clip.Alpha * swipeAlpha;
                clip.Image.enabled = alpha > 0f && clip.Texture != null;
                clip.Image.color = new Color(1f, 1f, 1f, alpha);
            }
            // The portrait and its rim hide under the clip (the clips carry the rim). Asserted every frame while
            // hidden, because the carousel restores the portrait after a swipe; never while the portrait is fully
            // shown, so the carousel's own fade of a newly shown portrait is left alone.
            bool waiting = live != null && !live.Failed && !ready && shown <= 0f &&
                Time.unscaledTime - awaitingSince < ClipWaitSeconds;
            float hidden = waiting ? 1f : Smooth((shown - .4f) / .6f);
            if (hidden > 0f || appliedHidden > 0f)
            {
                appliedHidden = hidden;
                motion.SetCharacterPlayback(hidden, hidden);
            }
        }

        private static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

        private void OnEnable()
        {
            if (idol >= 0 && clips[current] != null && clips[current].Prepared && !clips[current].Failed) clips[current].Player.Play();
        }

        private void Release()
        {
            for (int i = 0; i < clips.Length; i++)
            {
                Clip clip = clips[i];
                if (clip == null) continue;
                if (clip.Player != null) { clip.Player.Stop(); Destroy(clip.Player); }
                if (clip.Image != null) Destroy(clip.Image.gameObject);
                if (clip.Texture != null) { clip.Texture.Release(); Destroy(clip.Texture); }
                clips[i] = null;
            }
            if (motion != null && appliedHidden > 0f) motion.SetCharacterPlayback(0f, 0f);
            appliedHidden = 0f;
            idol = -1;
        }

        private void OnDestroy()
        {
            Release();
            if (material != null) Destroy(material);
        }
    }
}
