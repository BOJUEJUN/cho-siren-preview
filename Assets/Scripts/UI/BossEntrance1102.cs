using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace ChoSiren.UI
{
    /// <summary>The supplied Boss movie plays to its real end, without advancing battle time.</summary>
    public sealed class BossEntrance1102 : MonoBehaviour
    {
        VideoPlayer player;
        RenderTexture texture;
        RawImage screen;
        CanvasGroup group;
        bool ended, failed, skipped;

        public IEnumerator Play(bool major, Font font, Func<bool> paused)
        {
            var root = (RectTransform)transform;
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            group = gameObject.AddComponent<CanvasGroup>(); group.alpha = 0;
            var blocker = gameObject.AddComponent<Image>(); blocker.color = new Color32(8, 5, 14, 255);
            var image = new GameObject("Movie", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            image.transform.SetParent(transform, false);
            screen = image.GetComponent<RawImage>(); screen.raycastTarget = false;
            screen.rectTransform.anchorMin = Vector2.zero; screen.rectTransform.anchorMax = Vector2.one;
            screen.rectTransform.offsetMin = screen.rectTransform.offsetMax = Vector2.zero;
            var skip = new GameObject("Skip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Button));
            skip.transform.SetParent(transform, false);
            var label = skip.GetComponent<Text>(); label.font = font; label.text = "跳过 ›";
            label.fontSize = 23; label.color = Color.white; label.alignment = TextAnchor.MiddleCenter;
            var rect = (RectTransform)skip.transform; rect.anchorMin = rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one; rect.anchoredPosition = new Vector2(-20, -24); rect.sizeDelta = new Vector2(116, 60);
            skip.GetComponent<Button>().onClick.AddListener(() => skipped = true);
            player = gameObject.AddComponent<VideoPlayer>(); player.playOnAwake = false;
            player.isLooping = false; player.waitForFirstFrame = true; player.skipOnDrop = true;
            player.source = VideoSource.Url; player.renderMode = VideoRenderMode.RenderTexture;
            player.audioOutputMode = VideoAudioOutputMode.None;
            string url = Application.streamingAssetsPath.TrimEnd('/') + "/BossEntrance1102/" + (major ? "major" : "mini") + ".mp4";
#if !UNITY_WEBGL || UNITY_EDITOR
            if (!url.Contains("://")) url = new Uri(System.IO.Path.GetFullPath(url)).AbsoluteUri;
#endif
            player.url = url;
            player.loopPointReached += _ => ended = true;
            player.errorReceived += (_, message) => { failed = true; Debug.LogWarning("Boss intro unavailable: " + message); };
            player.Prepare();
            float wait = 0;
            while (!player.isPrepared && !failed && !skipped && wait < 8)
            { wait += Time.unscaledDeltaTime; yield return null; }
            if (!player.isPrepared || failed || skipped) yield break;
            int width = (int)Mathf.Max(2, player.width), height = (int)Mathf.Max(2, player.height);
            texture = new RenderTexture(width, height, 0); texture.Create();
            player.targetTexture = texture; screen.texture = texture;
            group.alpha = 1;
            float elapsed = 0, limit = (float)Math.Max(major ? 6.04 : 4.04, player.length) + 5f;
            player.Play();
            while (!ended && !failed && !skipped && elapsed < limit)
            {
                bool hold = paused != null && paused();
                if (hold && player.isPlaying) player.Pause();
                else if (!hold && !player.isPlaying) player.Play();
                if (!hold) elapsed += Time.unscaledDeltaTime;
                float viewAspect = Mathf.Max(.1f, root.rect.width / Mathf.Max(1, root.rect.height));
                float clipAspect = width / (float)height;
                float u = Mathf.Min(1, viewAspect / clipAspect), v = Mathf.Min(1, clipAspect / viewAspect);
                screen.uvRect = new Rect((1-u)*.5f, (1-v)*.5f, u, v);
                yield return null;
            }
            group.alpha = 0; player.Stop();
        }

        void OnDestroy()
        {
            if (player != null) { player.Stop(); player.targetTexture = null; }
            if (texture != null) { texture.Release(); Destroy(texture); }
        }
    }
}
