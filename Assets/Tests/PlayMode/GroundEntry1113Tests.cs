using System.Collections;
using ChoSiren.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ChoSiren.Tests
{
    public sealed class GroundEntry1113Tests
    {
        [UnityTest] public IEnumerator FracturePrecedesVisibleEnemyAndBlocksEarlyInput()
        {
            bool reduced = BattlePresentationOptions.ReduceMotion;
            BattlePresentationOptions.ReduceMotion = false;
            var root = new GameObject("GroundEntryTest", typeof(RectTransform));
            var enemy = new GameObject("Enemy", typeof(RectTransform));
            enemy.transform.SetParent(root.transform, false);
            try
            {
                BattleEnter1112.Show((RectTransform)enemy.transform, true, () => true);
                var entry = enemy.GetComponent<BattleEnter1112>();
                var group = enemy.GetComponent<CanvasGroup>();
                Assert.That(entry != null && group != null, Is.True, "Native presentation components must actually be attached.");
                Assert.That(group.alpha, Is.Zero);
                Assert.That(group.blocksRaycasts, Is.False);
                Assert.That(root.GetComponentInChildren<BattleGroundCrack1113>().raycastTarget, Is.False);
                entry.Advance(.4f);
                Assert.That(group.alpha, Is.Zero, "Ground must open before the figure appears.");
                entry.Advance(.25f);
                Assert.That(group.alpha, Is.InRange(.01f, .99f));
                yield return null; // paused entry does not advance with real time
                entry.Advance(1f);
                Assert.That(entry.IsShowing, Is.False);
                Assert.That(group.alpha, Is.EqualTo(1f));
                Assert.That(group.blocksRaycasts, Is.True);
                BattleEnter1112.Show((RectTransform)enemy.transform, true);
                Assert.That(entry.IsShowing, Is.False, "Refreshing an already shown enemy must not replay entry.");
            }
            finally { BattlePresentationOptions.ReduceMotion = reduced; Object.Destroy(root); }
        }
    }
}
