using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    // Native UI sibling: the live character shares the same clipping, scaling and
    // draw order as the PSD portrait. No fixed foreground mask is involved.
    public sealed class PsdHomeCharacterSurface : MonoBehaviour
    {
        private RawImage image;
        private PsdHomeMotion motion;
        private RectTransform stage;
        private Texture2D texture, incoming;
        private int displayedClip = -1;
        private int nativeTextureId, incomingTextureId;
        // Cross-fade between the authored portrait and the clip, both ways. The clip
        // comes in over the still portrait first and the portrait leaves after it, so
        // the character never turns see-through halfway.
        private const float FadeSeconds = .35f;
        private float fade, appliedFade = -1f;
        private bool displayedKeyed;
        // Keyed clips follow the portrait's bob and parallax from this resting place, so
        // the rim behind them stays in register. The rim fades out while a performance
        // moves the figure away from the portrait's pose and back in for the idle loop.
        private Vector2 keyedRest;
        private float rimFade, appliedRim = -1f;
        private const int IdleClip = 3;
        private const float RimFadeSeconds = .25f;
        // Green-screen clips are made from a 1080x1920 key frame in which the portrait
        // stands 1860 px tall, 40 px below the top and centred, so they line up with it.
        private const float KeyedFigureHeight = 1860f / 1920f, KeyedFigureTop = 40f / 1920f;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern int ChoSirenCharacterCurrentClip();
        [DllImport("__Internal")] private static extern int ChoSirenCharacterUploadFrame(int textureId);
        [DllImport("__Internal")] private static extern int ChoSirenCharacterClipSize();
        [DllImport("__Internal")] private static extern int ChoSirenCharacterClipKeyed();
#endif

        public void Initialize(RectTransform home, PsdHomeMotion owner)
        {
            stage = home;
            motion = owner;
            image = GetComponent<RawImage>();
            image.raycastTarget = false;
            image.enabled = false;
        }

        private void LateUpdate()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            int clip = ChoSirenCharacterCurrentClip() - 1;
            float target = 0f;
            int size = clip >= 0 ? ChoSirenCharacterClipSize() : 0;
            if (size > 0)
            {
                int width = size >> 16, height = size & 0xFFFF;
                bool uploaded;
                if (texture != null && texture.width == width && texture.height == height)
                {
                    uploaded = ChoSirenCharacterUploadFrame(nativeTextureId) != 0;
                }
                else
                {
                    // A clip of another size fills a texture of its own first; the old
                    // one keeps showing the previous clip's last frame until then.
                    if (incoming == null || incoming.width != width || incoming.height != height)
                    {
                        if (incoming != null) Destroy(incoming);
                        incoming = new Texture2D(width, height, TextureFormat.RGBA32, false);
                        incoming.wrapMode = TextureWrapMode.Clamp;
                        incoming.filterMode = FilterMode.Bilinear;
                        incoming.Apply(false, true);
                        incomingTextureId = incoming.GetNativeTexturePtr().ToInt32();
                    }
                    uploaded = ChoSirenCharacterUploadFrame(incomingTextureId) != 0;
                    if (uploaded)
                    {
                        if (texture != null) Destroy(texture);
                        texture = incoming; nativeTextureId = incomingTextureId;
                        incoming = null;
                        image.texture = texture;
                    }
                }
                if (uploaded)
                {
                    bool keyed = ChoSirenCharacterClipKeyed() != 0;
                    if (displayedClip != clip || displayedKeyed != keyed)
                    {
                        if (!keyed || !PositionOnPortrait(width, height)) PositionClip(clip);
                    }
                    displayedClip = clip;
                    displayedKeyed = keyed;
                }
            }
            // While one clip hands over to the next, the held frame stays fully shown.
            if (clip >= 0 && displayedClip >= 0) target = 1f;
            // After the clip ends its last frame stays in the texture while it fades out.
            fade = Mathf.MoveTowards(fade, target, Time.unscaledDeltaTime / FadeSeconds);
            bool posing = displayedKeyed && displayedClip >= 0 && displayedClip != IdleClip && fade > 0f;
            float rimTarget = !displayedKeyed ? 1f : posing ? 1f : 0f;
            rimFade = Mathf.MoveTowards(rimFade, rimTarget, Time.unscaledDeltaTime / RimFadeSeconds);
            if (fade <= 0f) displayedClip = -1;
            if (displayedKeyed && fade > 0f) image.rectTransform.anchoredPosition = keyedRest + motion.CharacterOffset;
            ApplyFade();
#endif
        }

        private void ApplyFade()
        {
            if (Mathf.Approximately(fade, appliedFade) && Mathf.Approximately(rimFade, appliedRim)) return;
            appliedFade = fade;
            appliedRim = rimFade;
            float clipAlpha = Smooth(fade / .6f);
            image.enabled = clipAlpha > 0f;
            image.color = new Color(1f, 1f, 1f, clipAlpha);
            float portraitHidden = Smooth((fade - .4f) / .6f);
            motion.SetCharacterPlayback(portraitHidden, Mathf.Min(portraitHidden, Smooth(rimFade)));
        }

        private static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        // Places a green-screen clip so its figure covers the authored portrait exactly.
        public bool PositionOnPortrait(int width, int height)
        {
            if (width <= 0 || height <= 0 || !motion.TryGetCharacterRest(out Vector2 topLeft, out Vector2 size))
                return false;
            float videoH = size.y / KeyedFigureHeight, videoW = videoH * width / height;
            RectTransform rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.sizeDelta = new Vector2(videoW, videoH);
            rect.anchoredPosition = new Vector2(topLeft.x + (size.x - videoW) / 2, topLeft.y + KeyedFigureTop * videoH);
            keyedRest = rect.anchoredPosition;
            return true;
        }

        public void PositionClip(int clip)
        {
            // The idle loop (index 3) is framed like the first performance.
            if (clip > 2) clip = 0;
            // Keep the native video's aspect and the R10 accepted portrait anchor.
            float w = stage.rect.width, h = stage.rect.height;
            float boxW = w * (clip == 2 ? 1.26f : 1.19f);
            float boxH = h * (clip == 2 ? 1.05f : .99f);
            float videoW = Mathf.Min(boxW, boxH * 9f / 16f);
            float videoH = videoW * 16f / 9f;
            RectTransform rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.sizeDelta = new Vector2(videoW, videoH);
            rect.anchoredPosition = new Vector2(w * (clip == 2 ? -.06f : -.04f) + (boxW - videoW) / 2,
                -(h * (clip == 2 ? 0 : .06f) + (boxH - videoH) / 2));
        }

        private void RestorePortrait()
        {
            fade = 0f;
            rimFade = 0f;
            displayedClip = -1;
            ApplyFade();
        }

        private void OnDisable() { RestorePortrait(); }
        public void ResetPlayback()
        {
            appliedFade = -1f;
            RestorePortrait();
        }
        private void OnDestroy()
        {
            if (texture != null) Destroy(texture);
            if (incoming != null) Destroy(incoming);
        }
    }
}
