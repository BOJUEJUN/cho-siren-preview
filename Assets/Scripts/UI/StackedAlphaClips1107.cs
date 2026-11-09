using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace ChoSiren.UI
{
    /// <summary>
    /// 2026-11-07: plays cut-out "stacked alpha" clips (left half colour, right half alpha as grey; made by
    /// Tools/lobbyvideo/matte*.swift) from StreamingAssets in RawImages. With more than one clip it picks the next
    /// one at random each time the playing clip loops (赵金 1107: "两段练习来回连续随机播放") and crossfades.
    /// The VideoPlayers live on this always-active host, because a player on a hidden object cannot prepare.
    /// </summary>
    public sealed class StackedAlphaClips1107 : MonoBehaviour
    {
        public sealed class Clip
        {
            public string File;
            public RawImage Image;
            public VideoPlayer Player;
            public RenderTexture Texture;
            public bool Prepared, Failed;
            public float Alpha;
        }

        private const float FadeSeconds = .35f;
        private readonly List<Clip> clips = new List<Clip>();
        private Material material;
        private int current = -1;
        private bool paused;
        // 2026-11-09 (少爷: "怎么没有角色？"): a clip started while paused (减弱动画) never decoded a frame, so the
        // stage stayed empty. It now plays until its first frame lands, then freezes there.
        private bool freezePending;
        private float tint = 1f;
        private System.Random random = new System.Random();

        public IReadOnlyList<Clip> Clips => clips;
        public int Current => current;
        /// <summary>True once a clip has a frame on screen.</summary>
        public bool Showing => current >= 0 && clips[current].Prepared && !clips[current].Failed && clips[current].Alpha > 0f;
        /// <summary>True when every clip failed (the caller then shows its still art instead).</summary>
        public bool AllFailed => clips.Count > 0 && clips.TrueForAll(c => c.Failed);

        public static StackedAlphaClips1107 Create(GameObject host)
        {
            var clips = host.AddComponent<StackedAlphaClips1107>();
            Shader shader = Resources.Load<Shader>("Shaders/UIStackedAlpha");
            if (shader != null) clips.material = new Material(shader) { name = "StackedAlphaClips1107" };
            return clips;
        }

        /// <summary>Seeds the random clip order (tests use a fixed seed).</summary>
        public void Seed(int seed) => random = new System.Random(seed);

        /// <summary>Adds a clip (StreamingAssets-relative path) drawn into <paramref name="image"/>.</summary>
        public Clip Add(string streamingPath, RawImage image)
        {
            var clip = new Clip { File = streamingPath, Image = image };
            image.raycastTarget = false;
            image.enabled = false;
            if (material != null) image.material = material;
            VideoPlayer player = gameObject.AddComponent<VideoPlayer>();
            player.playOnAwake = false; player.isLooping = true; player.skipOnDrop = true; player.waitForFirstFrame = true;
            player.source = VideoSource.Url; player.renderMode = VideoRenderMode.RenderTexture;
            player.audioOutputMode = VideoAudioOutputMode.None;
            player.url = Url(streamingPath);
            player.prepareCompleted += source =>
            {
                if (clip.Player != source) return;
                clip.Texture = new RenderTexture(Mathf.Max(2, (int)source.width), Mathf.Max(2, (int)source.height), 0,
                    RenderTextureFormat.ARGB32) { name = "Clip1107-" + streamingPath };
                clip.Texture.Create();
                source.targetTexture = clip.Texture;
                clip.Image.texture = clip.Texture;
                if (clip.Image.material != null && clip.Image.material.HasProperty("_AlphaFromSrgb"))
                    clip.Image.material.SetFloat("_AlphaFromSrgb",
                        QualitySettings.activeColorSpace == ColorSpace.Linear && clip.Texture.sRGB ? 1f : 0f);
                clip.Prepared = true;
                if (current < 0 || clips[current].Failed) PlayClip(clips.IndexOf(clip));
            };
            player.errorReceived += (source, message) =>
            {
                if (clip.Player != source) return;
                clip.Failed = true;
                Debug.LogWarning("CHO_SIREN_CLIP1107 failed: " + source.url + " " + message);
            };
            player.loopPointReached += source =>
            {
                if (current < 0 || clips[current].Player != source || clips.Count < 2) return;
                int next = random.Next(clips.Count);
                if (next != current && clips[next].Prepared && !clips[next].Failed) PlayClip(next);
            };
            clip.Player = player;
            clips.Add(clip);
            player.Prepare();
            return clip;
        }

        /// <summary>Pauses or resumes the clips (reduce-motion freezes them on the current frame).</summary>
        public void SetPaused(bool value)
        {
            paused = value;
            if (current < 0) return;
            Clip live = clips[current];
            if (paused)
            {
                bool hasFrame = live.Player.frame > 0 || live.Player.time > .03;
                if (hasFrame) { live.Player.Pause(); freezePending = false; }
                else { freezePending = true; if (live.Prepared) live.Player.Play(); }
            }
            else { freezePending = false; if (live.Prepared) live.Player.Play(); }
        }

        /// <summary>
        /// 2026-11-09 (少爷: "开始练习之后角色才是那个唱歌跳舞的动画平时是那个待机的动画"): back to the clip's opening
        /// pose and hold it (the stage's idle); <see cref="SetPaused"/>(false) starts the performance again.
        /// </summary>
        public void Rest()
        {
            paused = true;
            if (current < 0) return;
            Clip live = clips[current];
            live.Player.time = 0;
            if (live.Prepared) { live.Player.Play(); freezePending = true; }
        }

        /// <summary>Overall opacity (a panel fading in or out).</summary>
        public void SetTint(float alpha) => tint = Mathf.Clamp01(alpha);

        private void PlayClip(int index)
        {
            if (index < 0 || index >= clips.Count) return;
            Clip next = clips[index];
            next.Player.time = 0;
            next.Player.Play();
            freezePending = paused;
            next.Alpha = current < 0 ? 0f : next.Alpha;
            next.Image.transform.SetAsLastSibling();
            current = index;
        }

        private void LateUpdate()
        {
            if (current < 0) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, .1f);
            Clip live = clips[current];
            bool hasFrame = live.Player.frame > 0 || live.Player.time > .03;
            if (paused && freezePending && hasFrame) { live.Player.Pause(); freezePending = false; }
            bool ready = live.Prepared && !live.Failed && hasFrame;
            if (live.Prepared && !live.Failed && (!paused || freezePending) && !live.Player.isPlaying) live.Player.Play();
            if (ready) live.Alpha = Mathf.MoveTowards(live.Alpha, 1f, dt / FadeSeconds);
            for (int i = 0; i < clips.Count; i++)
            {
                Clip clip = clips[i];
                if (i != current && live.Alpha >= 1f && clip.Alpha > 0f)
                {
                    // The outgoing clip leaves only once the incoming one fully covers it.
                    clip.Alpha = 0f;
                    if (clip.Player.isPlaying) clip.Player.Pause();
                }
                clip.Image.enabled = clip.Texture != null && clip.Alpha * tint > 0f;
                clip.Image.color = new Color(1f, 1f, 1f, clip.Alpha * tint);
            }
        }

        private void OnDestroy()
        {
            foreach (Clip clip in clips)
            {
                if (clip.Player != null) clip.Player.Stop();
                if (clip.Texture != null) { clip.Texture.Release(); Destroy(clip.Texture); }
            }
            if (material != null) Destroy(material);
        }

        public static string Url(string streamingPath)
        {
            string url = Application.streamingAssetsPath.TrimEnd('/') + "/" + streamingPath;
#if !UNITY_WEBGL || UNITY_EDITOR
            // Native players need a file:// URI for local media.
            if (!url.Contains("://")) url = new System.Uri(System.IO.Path.GetFullPath(url)).AbsoluteUri;
#endif
            return url;
        }
    }
}
