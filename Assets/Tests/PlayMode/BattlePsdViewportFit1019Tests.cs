using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ChoSiren.Tests
{
    public sealed class BattlePsdViewportFit1019Tests
    {
        static readonly Vector2 Source = new Vector2(720, 6144 * 720f / 2946f);
        [TestCase(720, 1536)]
        [TestCase(720, 1390)]
        [TestCase(560, 930)]
        [TestCase(1280, 720)]
        public void AuthoredPsdStaysUniformAndCompletelyWithinTheUsableViewport(float width, float height)
        {
            var owner = Host(width, height);
            try
            {
                var viewport = (RectTransform)owner.transform;
                var content = Content(viewport);
                var fit = content.gameObject.AddComponent<BattlePsdViewportFit1019>();
                fit.Configure(content, viewport, Source);
                AssertFit(content, viewport);
            }
            finally { Object.DestroyImmediate(owner); }
        }
        [UnityTest]
        public IEnumerator RectCallbacksSettleAndRepeatedRefreshDoesNotCauseSelfLayoutChurn()
        {
            var owner = Host(720, 1390);
            try
            {
                var viewport = (RectTransform)owner.transform;
                var content = Content(viewport);
                var counter = content.gameObject.AddComponent<ViewportDimensionCounter1019>();
                var fit = content.gameObject.AddComponent<BattlePsdViewportFit1019>();
                fit.Configure(content, viewport, Source);
                yield return null; Canvas.ForceUpdateCanvases(); yield return null;
                int settled = counter.Count;
                Assert.That(settled, Is.LessThan(20), "Dimension callbacks should be bounded during configuration.");
                for (int i = 0; i < 50; i++) fit.RefreshFit();
                yield return null; Canvas.ForceUpdateCanvases(); yield return null;
                Assert.That(counter.Count, Is.EqualTo(settled), "An unchanged viewport must not rewrite dimensions every frame.");
                AssertFit(content, viewport);
            }
            finally { Object.DestroyImmediate(owner); }
        }
        [UnityTest]
        public IEnumerator ShortToTallToNarrowResizeRecomputesFromTheReferenceInsteadOfCompoundingScale()
        {
            var owner = Host(720, 1100);
            try
            {
                var viewport = (RectTransform)owner.transform; var content = Content(viewport);
                var fit = content.gameObject.AddComponent<BattlePsdViewportFit1019>(); fit.Configure(content, viewport, Source);
                foreach (var size in new[] { new Vector2(720, 1800), new Vector2(390, 844), new Vector2(720, 1100), new Vector2(720, 1536) })
                {
                    viewport.sizeDelta = size;
                    Canvas.ForceUpdateCanvases(); yield return null; yield return null;
                    AssertFit(content, viewport);
                    Assert.That(content.sizeDelta, Is.EqualTo(Source), "Resize must not compound the authored dimensions.");
                }
                // Normal fitting should not fight a temporary presentation offset at the same size.
                content.anchoredPosition += new Vector2(3, -2);
                Vector2 offset = content.anchoredPosition; fit.RefreshFit();
                Assert.That(content.anchoredPosition, Is.EqualTo(offset));
            }
            finally { Object.DestroyImmediate(owner); }
        }
        [Test]
        public void TemporarilyCollapsedViewportKeepsTheLastValidPoseInsteadOfProducingInvalidScale()
        {
            var owner = Host(720, 1390);
            try
            {
                var viewport = (RectTransform)owner.transform; var content = Content(viewport);
                var fit = content.gameObject.AddComponent<BattlePsdViewportFit1019>(); fit.Configure(content, viewport, Source);
                Vector3 before = content.localScale; Vector2 position = content.anchoredPosition;
                viewport.sizeDelta = Vector2.zero; fit.RefreshFit();
                Assert.That(content.localScale, Is.EqualTo(before)); Assert.That(content.anchoredPosition, Is.EqualTo(position));
                viewport.sizeDelta = new Vector2(720, 1800); fit.RefreshFit(); AssertFit(content, viewport);
            }
            finally { Object.DestroyImmediate(owner); }
        }
        static GameObject Host(float width, float height)
        {
            var go = new GameObject("PSD usable viewport", typeof(RectTransform), typeof(Canvas));
            go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var rect = (RectTransform)go.transform; rect.pivot = Vector2.one * .5f; rect.sizeDelta = new Vector2(width, height);
            return go;
        }
        static RectTransform Content(RectTransform viewport)
        {
            var go = new GameObject("AuthoredPsd", typeof(RectTransform)); go.transform.SetParent(viewport, false);
            return (RectTransform)go.transform;
        }
        static void AssertFit(RectTransform content, RectTransform viewport)
        {
            float scale = Mathf.Min(viewport.rect.width / Source.x, viewport.rect.height / Source.y);
            Assert.That(content.localScale.x, Is.EqualTo(scale).Within(.0001f));
            Assert.That(content.localScale.y, Is.EqualTo(scale).Within(.0001f));
            var corners = new Vector3[4]; content.GetWorldCorners(corners);
            foreach (var world in corners)
            {
                Vector3 p = viewport.InverseTransformPoint(world);
                Assert.That(p.x, Is.InRange(viewport.rect.xMin - .01f, viewport.rect.xMax + .01f));
                Assert.That(p.y, Is.InRange(viewport.rect.yMin - .01f, viewport.rect.yMax + .01f));
            }
            Vector3 center = viewport.InverseTransformPoint(content.TransformPoint(content.rect.center));
            Assert.That(center.x, Is.EqualTo(viewport.rect.center.x).Within(.01f));
            Assert.That(center.y, Is.EqualTo(viewport.rect.center.y).Within(.01f));
        }
    }
    public sealed class ViewportDimensionCounter1019 : MonoBehaviour
    {
        public int Count { get; private set; }
        void OnRectTransformDimensionsChange() => Count++;
    }
}
