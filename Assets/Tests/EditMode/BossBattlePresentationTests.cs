using System;
using System.Reflection;
using ChoSiren.Panels;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.Tests
{
    public sealed class BossBattlePresentationTests
    {
        [Test]
        public void PresentationExposesAllRequiredCombatStatesAndHooks()
        {
            string[] hooks = { "SetHealthRatio", "PlayHit", "PlayCharge", "PlayPhaseSurge", "PlayOutcome" };
            for (int index = 0; index < hooks.Length; index++)
                Assert.That(typeof(BossBattlePresentation).GetMethod(hooks[index],
                    BindingFlags.Instance | BindingFlags.Public), Is.Not.Null, hooks[index]);

            Assert.That(Enum.GetNames(typeof(BossBattlePresentation.BossVisualState)),
                Is.EquivalentTo(new[] { "Idle", "Charging", "Hit", "LowHealth", "Defeated", "VictoryPose" }));
        }

        [Test]
        public void HealthStateSwitchesToVisibleLowHealthModeWithoutBattleLogicDependency()
        {
            GameObject root = new GameObject("BossPresentationTest", typeof(RectTransform));
            try
            {
                RectTransform rig = new GameObject("Rig", typeof(RectTransform)).GetComponent<RectTransform>();
                rig.SetParent(root.transform, false);
                Image portrait = new GameObject("Portrait", typeof(RectTransform), typeof(CanvasRenderer),
                    typeof(Image)).GetComponent<Image>();
                portrait.transform.SetParent(rig, false);
                Text label = new GameObject("State", typeof(RectTransform), typeof(CanvasRenderer),
                    typeof(Text)).GetComponent<Text>();
                label.transform.SetParent(root.transform, false);
                Image warning = new GameObject("Warning", typeof(RectTransform), typeof(CanvasRenderer),
                    typeof(Image)).GetComponent<Image>();
                warning.transform.SetParent(root.transform, false);

                BossBattlePresentation presentation = root.AddComponent<BossBattlePresentation>();
                presentation.Configure(rig, portrait, null, null, null, null, null, warning,
                    null, null, null, label, Array.Empty<Image>(), Array.Empty<Image>(), Array.Empty<Text>());
                presentation.SetHealthRatio(0.29f);

                Assert.That(presentation.LowHealth, Is.True);
                Assert.That(presentation.State, Is.EqualTo(BossBattlePresentation.BossVisualState.LowHealth));
                Assert.That(label.gameObject.activeSelf, Is.True);
                Assert.That(label.text, Does.Contain("危险"));
                Assert.That(presentation.MotionRoot, Is.SameAs(rig));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void GeneratedBattleEffectsRemainLoadableFromResources()
        {
            string[] paths =
            {
                "Art/BattleAI/battle-hit-slash-ai-v1",
                "Art/BattleAI/battle-heart-impact-ai-v1",
                "Art/BattleAI/battle-charge-aura-ai-v1",
                "Art/BattleAI/battle-low-health-frame-ai-v1",
            };

            for (int index = 0; index < paths.Length; index++)
            {
                UnityEngine.Object asset = Resources.Load<Sprite>(paths[index]) ??
                                           (UnityEngine.Object)Resources.Load<Texture2D>(paths[index]);
                Assert.That(asset, Is.Not.Null, paths[index]);
            }
        }
    }
}
