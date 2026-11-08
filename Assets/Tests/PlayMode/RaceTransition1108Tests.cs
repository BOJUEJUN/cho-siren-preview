using System.Collections;
using System.IO;
using ChoSiren.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ChoSiren.Tests
{
    /// <summary>2026-11-08: the 团队 member tap plays the race silhouette transition and opens the page once, under cover.</summary>
    public sealed class RaceTransition1108Tests
    {
        [UnityTest]
        public IEnumerator EachRaceCardPlaysAndOpensThePageOnceUnderCover()
        {
            var host = new GameObject("RaceFxCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var camera = new GameObject("RaceFxCamera", typeof(Camera)).GetComponent<Camera>();
            var target = new RenderTexture(540, 1144, 24);
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/qa-logs/race-transition-1108"));
            Directory.CreateDirectory(output);
            try
            {
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.1f, .05f, .2f);
                camera.orthographic = true; camera.targetTexture = target;
                var canvas = host.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera; canvas.planeDistance = 1;
                var scaler = host.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(720, 1536); scaler.matchWidthOrHeight = 0;
                var page = new GameObject("FakeDetailPage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                page.transform.SetParent(host.transform, false);
                var pageRect = (RectTransform)page.transform; pageRect.anchorMin = Vector2.zero; pageRect.anchorMax = Vector2.one;
                pageRect.offsetMin = pageRect.offsetMax = Vector2.zero;
                page.GetComponent<Image>().color = new Color(.85f, .3f, .55f);
                yield return null;
                foreach (string race in new[] { "血精灵", "海灵族 · 人鱼", "魔族 · 恶魔", "魅族" })
                {
                    Assert.That(RaceTransition1108.Art(race), Is.Not.Null, race + " art");
                    page.SetActive(false);
                    int opened = 0;
                    var fx = RaceTransition1108.Play(host.transform, race, new Vector2(-180f, 120f), () => { opened++; page.SetActive(true); });
                    Assert.That(fx, Is.Not.Null);
                    fx.enabled = false; // drive the clock by hand
                    string key = RaceTransition1108.RaceKey(race);
                    foreach (float t in new[] { .07f, .2f, .6f, 1f, 1.15f, 1.3f })
                    {
                        fx.Advance(t - fx.Clock);
                        Assert.That(opened, Is.EqualTo(t >= 1.04f ? 1 : 0), $"{race} @ {t}: the page opens once, only under full cyan");
                        Capture(camera, target, Path.Combine(output, $"{key}-{t:0.00}.png"));
                    }
                    fx.Advance(1f);
                    yield return null;
                    Assert.That(fx == null, Is.True, "the overlay removes itself");
                    Assert.That(opened, Is.EqualTo(1));
                    Assert.That(RaceTransition1108.Playing, Is.False);
                }
                Assert.That(ChallengeVideoTransition.Duration, Is.EqualTo(2.2f).Within(.01f), "the level cut plays the sped-up 关卡转折 clip");
                Assert.That(File.Exists(Path.Combine(Application.streamingAssetsPath, ChallengeVideoTransition.ClipFile)), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(host); Object.DestroyImmediate(camera.gameObject);
                target.Release(); Object.DestroyImmediate(target);
            }
        }

        private static void Capture(Camera camera, RenderTexture target, string path)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            var previous = RenderTexture.active; RenderTexture.active = target;
            var shot = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            shot.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); shot.Apply(); RenderTexture.active = previous;
            File.WriteAllBytes(path, shot.EncodeToPNG());
            Object.Destroy(shot);
        }
    }
}
