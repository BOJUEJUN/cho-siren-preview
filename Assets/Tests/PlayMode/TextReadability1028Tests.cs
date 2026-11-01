using System.Collections;
using System.Linq;
using System.Reflection;
using ChoSiren.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ChoSiren.Tests
{
    public sealed class TextReadability1028Tests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private GameObject canvas;
        private NativeTitleScreen title;

        [SetUp]
        public void SetUp()
        {
            canvas = new GameObject("Notice test", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720, 1536);
            var node = new GameObject("Title", typeof(RectTransform));
            node.transform.SetParent(canvas.transform, false);
            var rect = (RectTransform)node.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            title = node.AddComponent<NativeTitleScreen>();
        }

        [UnityTearDown]
        public IEnumerator TearDown() { Object.Destroy(canvas); yield return null; }

        [TestCase("0.9.22", "0.9.19", "当前版本 v0.9.22\n历史公告 v0.9.19")]
        [TestCase("0.9.22", "0.9.22", "v0.9.22")]
        [TestCase("0.9.22", "0.9.23", "v0.9.22 → v0.9.23")]
        [TestCase("0.9.22", "", "当前版本 v0.9.22")]
        public void NoticeNeverAdvertisesADowngrade(string current, string published, string expected)
        {
            string heading = (string)typeof(NativeTitleScreen).GetMethod("NoticeVersionHeading",
                BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { current, published, "" });
            Assert.That(heading, Is.EqualTo(expected));
        }

        [UnityTest]
        public IEnumerator NoticeKeepsLargeTypeAndScrollsTheEntireFetchedText()
        {
            typeof(NativeTitleScreen).GetMethod("ShowTextDialog", Private).Invoke(title,
                new object[] { "更新公告", "公告", "正在获取最新公告", "知道了", null });
            yield return null;
            Text body = canvas.GetComponentsInChildren<Text>().Single(t => t.name == "Notice");
            Assert.That(body.font.dynamic, Is.True, "font is rasterized for the actual Canvas density");
            Assert.That(body.fontSize, Is.GreaterThanOrEqualTo(24));
            Assert.That(body.transform.rotation, Is.EqualTo(Quaternion.identity));
            body.text = string.Join("\n\n", Enumerable.Repeat("更新内容：优化文字、保留存档与战斗进度。", 28));
            yield return null;
            Canvas.ForceUpdateCanvases();
            ScrollRect scroll = body.GetComponentInParent<ScrollRect>();
            Assert.That(scroll, Is.Not.Null);
            Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height));
            scroll.verticalNormalizedPosition = 0;
            Canvas.ForceUpdateCanvases();
            Vector3[] textCorners = new Vector3[4], viewCorners = new Vector3[4];
            body.rectTransform.GetWorldCorners(textCorners);
            scroll.viewport.GetWorldCorners(viewCorners);
            Assert.That(textCorners[0].y, Is.EqualTo(viewCorners[0].y).Within(1f), "last line remains reachable after async content replacement");
        }

        [UnityTest]
        public IEnumerator UpdateOfferSlantsOnlyTheBackdropAndDoesNotTruncateLongNotes()
        {
            var progress = new GameObject("Progress"); progress.transform.SetParent(title.transform, false);
            var start = new GameObject("Start", typeof(CanvasGroup)); start.transform.SetParent(title.transform, false);
            typeof(NativeTitleScreen).GetField("progressGroup", Private).SetValue(title, progress);
            typeof(NativeTitleScreen).GetField("startGroup", Private).SetValue(title, start.GetComponent<CanvasGroup>());
            typeof(NativeTitleScreen).GetField("safeFrame", Private).SetValue(title, title.GetComponent<RectTransform>());
            title.ShowUpdateOffer("0.9.10", string.Join("\n", Enumerable.Repeat("优化界面文字和新玩家引导，保留已有存档。", 24)),
                "立即更新", null, null);
            yield return null;
            Canvas.ForceUpdateCanvases();
            Text body = canvas.GetComponentsInChildren<Text>().Single(t => t.name == "Changes");
            Assert.That(body.fontSize, Is.GreaterThanOrEqualTo(24));
            Assert.That(body.transform.rotation, Is.EqualTo(Quaternion.identity));
            var scroll = body.GetComponentInParent<ScrollRect>();
            Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height));
            Assert.That(canvas.GetComponentsInChildren<Button>().All(b => !b.transform.IsChildOf(scroll.transform)), Is.True,
                "buttons stay outside the reading viewport");
        }
    }
}
