using System;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.UI
{
    /// <summary>
    /// 2026-11-07 (赵金: "等待时候放 LOADING … 视频循环播放 … 深色背景 … 下面有个进度条在这些角色下面跟进系统
    /// LOADING 速度来跑动"): the four leads and the pig run in a loop (cut-out clip StreamingAssets/Loading1107/run.mp4)
    /// on a dark band, with the pink bow progress bar under their feet. The bar follows whatever progress the host
    /// already tracks (read each frame), so existing loaders keep their own logic.
    /// </summary>
    public sealed class LoadingRun1107 : MonoBehaviour
    {
        private const string ArtRoot = "Art/Ui1107/";
        // Bar art (1117 x 50): the inner capsule runs from x 32 to 1088; the bow's stick is at x 31.5 of 63.
        private const float BarW = 1117f, BarH = 50f, InnerX0 = 32f, InnerX1 = 1088f, BowW = 63f, BowH = 84f;
        // Clip frame 1280 x 720: the runners' feet are at 0.878 of its height.
        private const float ClipAspect = 1280f / 720f, FeetFraction = .878f;

        private Func<float> source;
        private Image fill;
        private RectTransform bow;
        private float width, shown;
        private StackedAlphaClips1107 clips;

        public float Progress => shown;
        public StackedAlphaClips1107 Clips => clips;
        /// <summary>Height of the runners plus the bar, for hosts that stack text below.</summary>
        public float Height { get; private set; }

        /// <summary>
        /// Builds the widget at the bottom-centre of <paramref name="parent"/>'s rect, <paramref name="barWidth"/> wide.
        /// <paramref name="progress"/> returns 0..1; <paramref name="darkBand"/> adds the dark backdrop behind the runners.
        /// </summary>
        public static LoadingRun1107 Create(Transform parent, float barWidth, Func<float> progress, bool darkBand)
        {
            var node = new GameObject("LoadingRun1107", typeof(RectTransform));
            node.transform.SetParent(parent, false);
            var run = node.AddComponent<LoadingRun1107>();
            run.source = progress;
            run.Build(barWidth, darkBand);
            return run;
        }

        private void Build(float barWidth, bool darkBand)
        {
            width = barWidth;
            float s = barWidth / BarW;
            float barH = BarH * s;
            float clipW = barWidth, clipH = clipW / ClipAspect;
            float feetY = clipH * FeetFraction; // from the clip's top
            Height = feetY + barH;
            var rect = (RectTransform)transform;
            rect.sizeDelta = new Vector2(barWidth, Height);

            if (darkBand)
            {
                // A soft dark oval behind the runners and the bar (the "深色背景" while waiting).
                Image band = NewImage("DarkBand", transform, VisualA1101.Glow, new Color(.03f, .02f, .09f, .9f));
                Place(band.rectTransform, -barWidth * .08f, Height * .18f, barWidth * 1.16f, Height * .9f);
            }

            var clipNode = new GameObject("Runners", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            clipNode.transform.SetParent(transform, false);
            Place((RectTransform)clipNode.transform, 0f, Height - barH - feetY, clipW, clipH);
            clips = StackedAlphaClips1107.Create(gameObject);
            clips.Add("Loading1107/run.mp4", clipNode.GetComponent<RawImage>());

            Image track = NewImage("BarTrack", transform, Resources.Load<Sprite>(ArtRoot + "loading-track"), Color.white);
            Place(track.rectTransform, 0f, Height - barH, barWidth, barH);
            fill = NewImage("BarFill", transform, Resources.Load<Sprite>(ArtRoot + "loading-fill"), Color.white);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            Place(fill.rectTransform, 0f, Height - barH, barWidth, barH);
            Image bowImage = NewImage("BarBow", transform, Resources.Load<Sprite>(ArtRoot + "loading-bow"), Color.white);
            bow = bowImage.rectTransform;
            Place(bow, 0f, Height - BowH * s, BowW * s, BowH * s);
            Apply(0f);
        }

        private void LateUpdate()
        {
            float target = Mathf.Clamp01(source != null ? source() : 0f);
            // Eases toward the real value so a jumpy loader still reads as a run, never backwards by itself.
            shown = target < shown - .2f ? target : Mathf.MoveTowards(shown, target, Mathf.Max(.02f, Mathf.Abs(target - shown)) * Time.unscaledDeltaTime * 6f);
            Apply(shown);
        }

        private void Apply(float value)
        {
            float s = width / BarW;
            float inner = (InnerX0 + (InnerX1 - InnerX0) * value) / BarW;
            fill.fillAmount = value <= 0f ? 0f : inner;
            bow.anchoredPosition = new Vector2((InnerX0 + (InnerX1 - InnerX0) * value) * s - BowW * s * .5f, bow.anchoredPosition.y);
        }

        private static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite; image.color = color; image.raycastTarget = false;
            return image;
        }

        /// <summary>Top-left placement inside this widget's rect.</summary>
        private static void Place(RectTransform rect, float x, float y, float w, float h)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(w, h);
        }
    }
}
