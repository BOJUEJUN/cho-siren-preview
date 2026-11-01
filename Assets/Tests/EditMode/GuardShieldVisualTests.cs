using System;
using System.Linq;
using ChoSiren.Systems.Tactics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.Tests
{
    public sealed class GuardShieldVisualTests
    {
        [TearDown] public void Cleanup() { PlayerPrefs.DeleteKey(GameModel.SaveKey); PlayerPrefs.DeleteKey(GameModel.LegacySaveKey); }

        [Test] public void GuardShowsTheRenderedBubbleWhileActive()
        {
            Assert.IsNotNull(ReferenceArt038.Load("Art/Reference038/vfx-shield-crest"), "crest art");
            Assert.IsNotNull(ReferenceArt038.Load("Art/Reference038/vfx-dice-flame"), "flame art");
            var model = new GameModel(() => new DateTime(2026, 10, 12, 12, 0, 0));
            var battle = model.StartStageBattle("stage-1-1", 1, out _);
            var member = battle.Units.First(u => u.Side == BattleSide.Player);
            var canvas = new GameObject("Canvas", typeof(Canvas));
            try
            {
                var go = new GameObject("Guard", typeof(RectTransform));
                go.transform.SetParent(canvas.transform, false);
                ((RectTransform)go.transform).sizeDelta = new Vector2(160, 220);
                var ring = go.AddComponent<BattleGuardRing929>();
                ring.Configure(battle, () => member, () => false, () => 1);
                Image bubble = go.transform.Find("ShieldCrest").GetComponent<Image>();
                Assert.IsTrue(bubble.enabled && bubble.sprite != null, "bubble sprite");
                ring.Advance(.1f);
                Assert.AreEqual(0f, bubble.color.a, .001f, "hidden before the guard");
                for (int i = 0; i < 300 && !battle.TryTacticalGuard(); i++) battle.AdvanceRealtime(100, true);
                Assert.Greater(battle.GuardRemaining(member), 0, "guard active");
                ring.Advance(.3f);
                Assert.Greater(bubble.color.a, .5f, "bubble visible while guarding");
            }
            finally { UnityEngine.Object.DestroyImmediate(canvas); }
        }
    }
}
