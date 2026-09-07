using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ChoSiren.Tests
{
    public sealed class EnemyUnitPresentationPlayModeTests
    {
        [UnityTest]
        public IEnumerator IdleAttackHitAndDefeatAnimateOnlyChildRig()
        {
            GameObject owner = new GameObject("EnemySlot", typeof(RectTransform));
            try
            {
                RectTransform parent = owner.GetComponent<RectTransform>();
                parent.anchoredPosition = new Vector2(40f, 80f);
                EnemyUnitPresentation animation = CreateRig(parent, out Image portrait);
                animation.Configure(portrait, () => false, () => 1, "echo-drone");
                Vector2 baseline = animation.GetComponent<RectTransform>().anchoredPosition;
                animation.AdvancePresentation(.2f);
                Assert.That(animation.GetComponent<RectTransform>().anchoredPosition, Is.Not.EqualTo(baseline));
                Assert.That(parent.anchoredPosition, Is.EqualTo(new Vector2(40f, 80f)));
                animation.PlayAttack(true);
                animation.AdvancePresentation(.15f);
                Assert.That(animation.IsAttacking, Is.True);
                Assert.That(animation.ActionCount, Is.EqualTo(1));
                Assert.That(animation.transform.Find("EnemyChargeLine-0").GetComponent<Image>().color.a, Is.GreaterThan(0f));
                animation.PlayHit(true);
                animation.AdvancePresentation(.04f);
                Assert.That(animation.HitReactionCount, Is.EqualTo(1));
                Assert.That(portrait.color, Is.Not.EqualTo(Color.white));
                animation.PlayDefeat();
                animation.AdvancePresentation(.2f);
                Assert.That(animation.IsDefeating, Is.True);
                Assert.That(portrait.color.a, Is.InRange(.1f, .9f));
                animation.AdvancePresentation(.26f);
                Assert.That(animation.IsDefeated, Is.True);
                Assert.That(animation.IsDefeating, Is.False);
                Assert.That(portrait.color.a, Is.EqualTo(0f));
                animation.PlayAttack(false);
                Assert.That(animation.ActionCount, Is.EqualTo(1), "Defeated units cannot start attacks.");
                foreach (Image effect in animation.GetComponentsInChildren<Image>())
                    if (effect != portrait) Assert.That(effect.raycastTarget, Is.False);
                yield return null;
            }
            finally { Object.Destroy(owner); }
        }

        [UnityTest]
        public IEnumerator PauseSpeedAndDisableHaveNoStaleVisualState()
        {
            GameObject owner = new GameObject("PauseSlot", typeof(RectTransform));
            bool paused = false;
            int speed = 1;
            try
            {
                EnemyUnitPresentation animation = CreateRig(owner.transform, out Image portrait);
                animation.Configure(portrait, () => paused, () => speed, "noise-wraith");
                RectTransform rig = animation.GetComponent<RectTransform>();
                Vector2 initialPosition = rig.anchoredPosition;
                Vector3 initialScale = rig.localScale;
                animation.PlayAttack(false);
                animation.AdvancePresentation(.1f);
                paused = true;
                Vector2 frozenPosition = rig.anchoredPosition;
                Color frozenColor = portrait.color;
                float frozenTime = animation.VisualElapsedSeconds;
                animation.AdvancePresentation(10f);
                Assert.That(rig.anchoredPosition, Is.EqualTo(frozenPosition));
                Assert.That(portrait.color, Is.EqualTo(frozenColor));
                Assert.That(animation.VisualElapsedSeconds, Is.EqualTo(frozenTime));
                paused = false;
                speed = 2;
                animation.AdvancePresentation(.1f);
                Assert.That(animation.VisualElapsedSeconds, Is.EqualTo(frozenTime + .2f).Within(.0001f));
                animation.PlayDefeat();
                paused = true;
                animation.AdvancePresentation(1f);
                Assert.That(animation.DefeatProgress, Is.Zero);
                paused = false;
                animation.AdvancePresentation(.23f);
                Assert.That(animation.IsDefeated, Is.True);
                animation.gameObject.SetActive(false);
                Assert.That(rig.anchoredPosition, Is.EqualTo(initialPosition));
                Assert.That(rig.localScale, Is.EqualTo(initialScale));
                Assert.That(portrait.color, Is.EqualTo(Color.white));
                Assert.That(animation.IsDefeated, Is.False);
                Assert.That(animation.ActionCount, Is.Zero);
                animation.gameObject.SetActive(true);
                animation.PlayAttack(false);
                Assert.That(animation.IsAttacking, Is.True);
                animation.Configure(portrait, () => false, () => 1, "static-golem");
                Assert.That(animation.ActionCount, Is.Zero);
                Assert.That(animation.GetComponentsInChildren<Image>().Length, Is.EqualTo(10), "Reconfiguration reuses bounded effects.");
                yield return null;
            }
            finally { Object.Destroy(owner); }
        }

        private static EnemyUnitPresentation CreateRig(Transform parent, out Image portrait)
        {
            GameObject root = new GameObject("EnemyMotionRoot", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            root.GetComponent<RectTransform>().anchoredPosition = new Vector2(2f, 3f);
            GameObject figure = new GameObject("EnemyPortrait", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            figure.transform.SetParent(root.transform, false);
            portrait = figure.GetComponent<Image>();
            return root.AddComponent<EnemyUnitPresentation>();
        }
    }
}
