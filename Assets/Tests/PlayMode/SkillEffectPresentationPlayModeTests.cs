using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.Tests
{
    public sealed class SkillEffectPresentationPlayModeTests
    {
        private GameObject root;
        private RectTransform layer, target;
        private SkillEffectPresentation effects;
        private bool paused;
        private int speed;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("FX fixture", typeof(RectTransform));
            layer = root.GetComponent<RectTransform>();
            layer.sizeDelta = new Vector2(720, 1536);
            target = new GameObject("Target", typeof(RectTransform)).GetComponent<RectTransform>();
            target.SetParent(layer, false);
            target.sizeDelta = new Vector2(100, 100);
            target.anchoredPosition = new Vector2(120, 240);
            effects = root.AddComponent<SkillEffectPresentation>();
            paused = false; speed = 1;
            effects.Configure(layer, () => paused, () => speed);
        }

        [TearDown] public void TearDown() => Object.DestroyImmediate(root);

        [Test]
        public void EffectsStayOnTargetAndNeverInterceptInput()
        {
            effects.Play(target, SkillVisualKind.Shield, Color.cyan, true);
            effects.AdvancePresentation(.2f);
            var graphic = root.GetComponentInChildren<SkillEffectGraphic>();
            Assert.That(graphic.rectTransform.anchoredPosition, Is.EqualTo(target.anchoredPosition));
            target.anchoredPosition += Vector2.up * 30;
            effects.AdvancePresentation(.1f);
            Assert.That(graphic.rectTransform.anchoredPosition, Is.EqualTo(target.anchoredPosition));
            Assert.That(root.GetComponentsInChildren<Graphic>().All(g => !g.raycastTarget), Is.True);
        }

        [Test]
        public void PauseFreezesEffectsAndSpeedControlsTheirLifetime()
        {
            effects.Play(target, SkillVisualKind.Heal, Color.green, true);
            effects.AdvancePresentation(.1f);
            paused = true;
            effects.AdvancePresentation(10);
            Assert.That(effects.VisualClock, Is.EqualTo(.1f).Within(.001f));
            Assert.That(effects.ActiveCount, Is.EqualTo(1));
            paused = false; speed = 2;
            effects.AdvancePresentation(.36f);
            Assert.That(effects.ActiveCount, Is.Zero);
        }

        [Test]
        public void BurstPoolIsBoundedAndReusedAndClearRemovesResultResidue()
        {
            for (int i = 0; i < 60; i++) effects.Play(target, SkillVisualKind.Slash, Color.magenta);
            Assert.That(effects.PoolCount, Is.EqualTo(SkillEffectPresentation.Capacity));
            effects.AdvancePresentation(1);
            Assert.That(effects.ActiveCount, Is.Zero);
            effects.Play(target, SkillVisualKind.Hex, Color.magenta);
            Assert.That(effects.PoolCount, Is.EqualTo(SkillEffectPresentation.Capacity));
            effects.Clear();
            Assert.That(effects.ActiveCount, Is.Zero);
        }

        [TestCase(SkillVisualKind.Cast)]
        [TestCase(SkillVisualKind.Impact)]
        [TestCase(SkillVisualKind.Slash)]
        [TestCase(SkillVisualKind.Hex)]
        [TestCase(SkillVisualKind.Pierce)]
        [TestCase(SkillVisualKind.Heal)]
        [TestCase(SkillVisualKind.Shield)]
        [TestCase(SkillVisualKind.Tide)]
        [TestCase(SkillVisualKind.Rift)]
        [TestCase(SkillVisualKind.Thorn)]
        [TestCase(SkillVisualKind.Prism)]
        public void EveryEffectProducesFiniteBoundedGeometry(SkillVisualKind kind)
        {
            effects.Play(target, kind, Color.cyan, true);
            effects.AdvancePresentation(.14f);
            var graphic = root.GetComponentInChildren<SkillEffectGraphic>();
            using (var helper = new VertexHelper())
            {
                typeof(SkillEffectGraphic).GetMethod("OnPopulateMesh", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly)
                    .Invoke(graphic, new object[] { helper });
                Assert.That(helper.currentVertCount, Is.GreaterThan(0).And.LessThan(1600));
                var vertex = new UIVertex();
                for (int i = 0; i < helper.currentVertCount; i++)
                {
                    helper.PopulateUIVertex(ref vertex, i);
                    Assert.That(float.IsNaN(vertex.position.x) || float.IsInfinity(vertex.position.y), Is.False);
                    Assert.That(vertex.position.magnitude, Is.LessThan(100));
                }
            }
        }
    }
}
