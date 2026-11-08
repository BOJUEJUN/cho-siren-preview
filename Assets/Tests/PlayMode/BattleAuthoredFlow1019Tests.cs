using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.Tests
{
    public sealed class BattleAuthoredFlow1019Tests
    {
        [Ignore("Quarantined 2026-11-08: battle presentation redesigned (PSD battle 10-28 to 11-07); see Docs/TEST-QUARANTINE-1108.md")]
        [TestCase(false)]
        [TestCase(true)]
        public void AuthoredAlphaFlowsWhileReadyAndFreezesWithPauseAndReducedMotion(bool pillar)
        {
            bool had = PlayerPrefs.HasKey(BattlePresentationOptions.ReduceMotionKey);
            int old = PlayerPrefs.GetInt(BattlePresentationOptions.ReduceMotionKey);
            PlayerPrefs.SetInt(BattlePresentationOptions.ReduceMotionKey, 0);
            var root = new GameObject("AuthoredFlow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bool pause = false;
            try
            {
                var rect = (RectTransform)root.transform;
                rect.sizeDelta = new Vector2(300, 160);
                var flow = root.AddComponent<BattleAuthoredFlow1019>();
                flow.Configure(pillar, () => pause);
                flow.SetShowing(true);
                var position = rect.anchoredPosition;
                var first = Quad(rect.rect); flow.ModifyMesh(first);
                Assert.That(first.currentVertCount, Is.GreaterThan(4));
                flow.Advance(.2f);
                var next = Quad(rect.rect); flow.ModifyMesh(next);
                UIVertex a = default, b = default;
                first.PopulateUIVertex(ref a, 50); next.PopulateUIVertex(ref b, 50);
                Assert.That(Vector3.Distance(a.position, b.position) > .01f || a.color.a != b.color.a, Is.True,
                    "A ready beam/smoke must show actual flowing pixels, not just a static image");
                Assert.That(root.GetComponent<Image>().raycastTarget, Is.False);
                Assert.That(rect.anchoredPosition, Is.EqualTo(position));
                float clock = flow.VisualClock;
                pause = true; flow.Advance(10);
                Assert.That(flow.VisualClock, Is.EqualTo(clock));
                pause = false; PlayerPrefs.SetInt(BattlePresentationOptions.ReduceMotionKey, 1);
                flow.Advance(10);
                Assert.That(flow.VisualClock, Is.EqualTo(clock));
                var reduced = Quad(rect.rect); flow.ModifyMesh(reduced);
                Assert.That(reduced.currentVertCount, Is.EqualTo(4), "Reduced motion keeps the native still artwork");
                flow.SetShowing(false); flow.Advance(10);
                Assert.That(flow.VisualClock, Is.Zero, "Unavailable artwork cannot run an idle effect");
                first.Dispose(); next.Dispose(); reduced.Dispose();
            }
            finally
            {
                Object.DestroyImmediate(root);
                if (had) PlayerPrefs.SetInt(BattlePresentationOptions.ReduceMotionKey, old);
                else PlayerPrefs.DeleteKey(BattlePresentationOptions.ReduceMotionKey);
            }
        }
        static VertexHelper Quad(Rect r)
        {
            var mesh = new VertexHelper();
            mesh.AddVert(new Vector3(r.xMin, r.yMin), Color.white, Vector2.zero);
            mesh.AddVert(new Vector3(r.xMin, r.yMax), Color.white, Vector2.up);
            mesh.AddVert(new Vector3(r.xMax, r.yMax), Color.white, Vector2.one);
            mesh.AddVert(new Vector3(r.xMax, r.yMin), Color.white, Vector2.right);
            mesh.AddTriangle(0, 1, 2); mesh.AddTriangle(2, 3, 0);
            return mesh;
        }
    }
}
