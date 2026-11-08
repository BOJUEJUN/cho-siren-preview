using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ChoSiren.Systems;
using ChoSiren.Systems.Tactics;
using NUnit.Framework;
using UnityEngine;

namespace ChoSiren.Tests
{
    public sealed class BattlePresentation1019Tests
    {
        bool hadReduce;
        int reduce;
        [SetUp] public void Before()
        {
            hadReduce = PlayerPrefs.HasKey(BattlePresentationOptions.ReduceMotionKey);
            reduce = PlayerPrefs.GetInt(BattlePresentationOptions.ReduceMotionKey);
            PlayerPrefs.SetInt(BattlePresentationOptions.ReduceMotionKey, 0);
        }
        [TearDown] public void After()
        {
            if (hadReduce) PlayerPrefs.SetInt(BattlePresentationOptions.ReduceMotionKey, reduce);
            else PlayerPrefs.DeleteKey(BattlePresentationOptions.ReduceMotionKey);
        }
        [Test]
        public void BasicsUseBothFormsWithoutMovingTheRealCardAndPauseFreezesThem()
        {
            var root = Stage(); var texture = new Texture2D(2, 4);
            var art = Sprite.Create(texture, new Rect(0, 0, 2, 4), Vector2.one * .5f);
            bool paused = false; int speed = 1;
            try
            {
                var source = Point("RealMember", root.transform, 180, 1030);
                var target = Point("RealEnemy", root.transform, 480, 520);
                var before = source.anchoredPosition;
                var cues = root.AddComponent<BattleBasicAttack1019>(); cues.Configure((RectTransform)root.transform, () => paused, () => speed);
                Assert.That(cues.Play(source, target, art, Color.red, BattleBasicForm1019.Fireball), Is.True);
                Assert.That(cues.Play(source, target, art, Color.magenta, BattleBasicForm1019.PortraitBurst), Is.True);
                Assert.That(cues.LastForm, Is.EqualTo(BattleBasicForm1019.PortraitBurst));
                cues.AdvancePresentation(.2f);
                var burst = root.transform.Find("AuthoredBasicLayer/BasicCue-1/FullPortraitBurst").GetComponent<UnityEngine.UI.Image>();
                Assert.That(burst.sprite, Is.SameAs(art));
                Assert.That(burst.gameObject.activeSelf, Is.True);
                float clock = cues.VisualClock; Vector2 pose = burst.rectTransform.anchoredPosition;
                paused = true; cues.AdvancePresentation(10);
                Assert.That(cues.VisualClock, Is.EqualTo(clock)); Assert.That(burst.rectTransform.anchoredPosition, Is.EqualTo(pose));
                Assert.That(source.anchoredPosition, Is.EqualTo(before));
                Assert.That(root.GetComponentsInChildren<UnityEngine.UI.Graphic>(true).All(g => !g.raycastTarget), Is.True);
                paused = false; speed = 2; cues.AdvancePresentation(.6f);
                Assert.That(cues.ActiveCount, Is.Zero);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(art); Object.DestroyImmediate(texture); }
        }
        [Ignore("Quarantined 2026-11-08: battle presentation redesigned (PSD battle 10-28 to 11-07); see Docs/TEST-QUARANTINE-1108.md")]
        [Test]
        public void FullPortraitBurstUsesPsdL012HeightAndTheStableCardInsteadOfItsMovingBust()
        {
            const float scale = 720f / 2946f;
            var root = Stage();
            try
            {
                var card = Point("ActualMemberCard", root.transform, 0, 0);
                card.anchorMin = card.anchorMax = card.pivot = Vector2.up;
                card.anchoredPosition = new Vector2(1488 * scale, -3670 * scale);
                card.sizeDelta = new Vector2(708 * scale, 1381 * scale);
                var bust = Point("MovingBust", card, 80, 135);
                var target = Point("Enemy", root.transform, 300, 400);
                var cue = root.AddComponent<BattleBasicAttack1019>();
                cue.Configure((RectTransform)root.transform, () => false, () => 1);
                Sprite art = Resources.Load<Sprite>("Art/Members/member-feiyin");
                Assert.That(art, Is.Not.Null);
                Vector2 cardPosition = card.anchoredPosition;
                Assert.That(cue.Play(bust, target, art, Color.magenta, BattleBasicForm1019.PortraitBurst, card), Is.True);
                cue.AdvancePresentation(.2f / BattlePresentationOptions.Pace);
                var portrait = (RectTransform)root.transform.Find("AuthoredBasicLayer/BasicCue-0/FullPortraitBurst");
                var corners = new Vector3[4]; portrait.GetWorldCorners(corners);
                var host = (RectTransform)root.transform;
                float top = corners.Max(p => host.InverseTransformPoint(p).y);
                Assert.That(host.rect.yMax - top, Is.EqualTo(2104 * scale).Within(.01f));
                Assert.That(portrait.rect.height, Is.EqualTo(3727 * scale).Within(.01f));
                Assert.That(portrait.rect.width / portrait.rect.height,
                    Is.EqualTo(art.rect.width / art.rect.height).Within(.0001f));
                Assert.That(portrait.anchoredPosition.x, Is.EqualTo((1488 + 354) * scale).Within(.01f));
                Assert.That(card.anchoredPosition, Is.EqualTo(cardPosition));
                Vector2 anchored = portrait.anchoredPosition;
                bust.anchoredPosition += new Vector2(75, 90);
                cue.AdvancePresentation(.02f);
                Assert.That(portrait.anchoredPosition, Is.EqualTo(anchored), "real card attack motion must not drag the enlarged clone into the stage");
                cue.AdvancePresentation(1f / BattlePresentationOptions.Pace);
                Assert.That(cue.ActiveCount, Is.Zero, "the PSD figure is an attack cue, not a persistent stage character");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void SuppliedL012RgbaKeepsItsNativePaddingAndExactThirdCardPlacement()
        {
            const float scale = 720f / 2946f;
            var root = Stage();
            try
            {
                BattlePsdArt928.LoadLocal();
                var native = BattlePsdArt928.Find("basic-portrait-feiyin");
                var art = BattlePsdArt928.Sprite("basic-portrait-feiyin");
                Assert.That(art, Is.Not.Null);
                var card = Point("AuthoredThirdCard", root.transform, 0, 0);
                card.anchorMin = card.anchorMax = card.pivot = Vector2.up;
                card.anchoredPosition = new Vector2(1488 * scale, -3670 * scale);
                card.sizeDelta = new Vector2(708 * scale, 1381 * scale);
                var target = Point("Enemy", root.transform, 400, 400);
                var cue = root.AddComponent<BattleBasicAttack1019>();
                cue.Configure((RectTransform)root.transform, () => false, () => 1);
                Assert.That(cue.Play(card, target, art, Color.magenta, BattleBasicForm1019.PortraitBurst, card, native), Is.True);
                cue.AdvancePresentation(.2f / BattlePresentationOptions.Pace);
                var portrait = (RectTransform)root.transform.Find("AuthoredBasicLayer/BasicCue-0/FullPortraitBurst");
                Assert.That(portrait.anchoredPosition.x - portrait.rect.width * .5f,
                    Is.EqualTo(1021 * scale).Within(.01f));
                Assert.That(-portrait.anchoredPosition.y - portrait.rect.height * .5f,
                    Is.EqualTo(2104 * scale).Within(.01f));
                Assert.That(portrait.rect.size, Is.EqualTo(new Vector2(2186 * scale, 3727 * scale)));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Ignore("Quarantined 2026-11-08: battle presentation redesigned (PSD battle 10-28 to 11-07); see Docs/TEST-QUARANTINE-1108.md")]
        [Test]
        public void BasicCapacityDropsDecorationAndReducedMotionCancelsEveryCue()
        {
            var root = Stage();
            try
            {
                var source = Point("Source", root.transform, 150, 1100); var target = Point("Target", root.transform, 400, 500);
                var cues = root.AddComponent<BattleBasicAttack1019>(); cues.Configure((RectTransform)root.transform, () => false, () => 1);
                for (int i = 0; i < BattleBasicAttack1019.Capacity; i++) Assert.That(cues.Play(source, target, null, Color.cyan, BattleBasicForm1019.Fireball), Is.True);
                Assert.That(cues.Play(source, target, null, Color.cyan, BattleBasicForm1019.Fireball), Is.False);
                PlayerPrefs.SetInt(BattlePresentationOptions.ReduceMotionKey, 1); cues.AdvancePresentation(.01f);
                Assert.That(cues.ActiveCount, Is.Zero);
                Assert.That(cues.Play(source, target, null, Color.cyan, BattleBasicForm1019.Fireball), Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }
        [Test]
        public void SmallSkillCloseupIsMaskedShortAndNeverQueuesOldCasts()
        {
            var root = Stage(); var texture = new Texture2D(2, 4); var art = Sprite.Create(texture, new Rect(0, 0, 2, 4), Vector2.one * .5f);
            bool paused = false;
            try
            {
                var cue = root.AddComponent<BattleActiveCloseup1019>(); cue.Configure((RectTransform)root.transform, () => paused, () => 2);
                Assert.That(cue.Play(art, "雾白", "净化之歌", Color.cyan), Is.True);
                Assert.That(cue.Play(art, "夜莺", "迟到的技能", Color.magenta), Is.False);
                Assert.That(cue.VisibleRect.height, Is.GreaterThan(0f), "the fallback art is cropped to its paper (UVs, no clipper)");
                paused = true; cue.AdvancePresentation(5); Assert.That(cue.IsShowing, Is.True);
                paused = false; cue.AdvancePresentation(1.5f); Assert.That(cue.IsShowing, Is.False);
                cue.AdvancePresentation(5); Assert.That(cue.IsShowing, Is.False, "A capacity drop must not replay after the battle has moved on.");
                Assert.That(root.GetComponentsInChildren<UnityEngine.UI.Text>(true).Any(t => t.text == "净化之歌"), Is.True);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(art); Object.DestroyImmediate(texture); }
        }
        [Ignore("Quarantined 2026-11-08: battle presentation redesigned (PSD battle 10-28 to 11-07); see Docs/TEST-QUARANTINE-1108.md")]
        [Test]
        public void WaveBreakIsOncePerWaveAndCancelRestoresTheEnemyVisualWithoutChangingItsLayout()
        {
            var root = Stage(); bool paused = false;
            try
            {
                var enemyLayer = Point("Enemies", root.transform, 100, 300);
                var cue = root.AddComponent<BattleWaveFracture1019>(); cue.Configure((RectTransform)root.transform, enemyLayer, null, () => paused, () => 1);
                var group = enemyLayer.GetComponent<CanvasGroup>(); group.alpha = .75f;
                Vector2 layout = enemyLayer.anchoredPosition;
                Assert.That(cue.Play(1), Is.False); Assert.That(cue.Play(2), Is.True);
                Assert.That(group.alpha, Is.Zero);
                Assert.That(cue.Play(2), Is.False, "Two spawn events in one wave must not restart its transition.");
                paused = true; cue.AdvancePresentation(3); Assert.That(group.alpha, Is.Zero);
                paused = false; cue.AdvancePresentation(1.5f); Assert.That(group.alpha, Is.GreaterThan(0));
                cue.Cancel(); Assert.That(group.alpha, Is.EqualTo(.75f).Within(.001f));
                Assert.That(enemyLayer.anchoredPosition, Is.EqualTo(layout));
                PlayerPrefs.SetInt(BattlePresentationOptions.ReduceMotionKey, 1);
                Assert.That(cue.Play(3), Is.True); Assert.That(cue.IsShowing, Is.False);
                Assert.That(group.alpha, Is.EqualTo(.75f).Within(.001f));
            }
            finally { Object.DestroyImmediate(root); }
        }
        [Test]
        public void HeadPoisonUsesTheAffectedEnemiesRealStacksAndVanishesAfterDefeat()
        {
            var root = Stage();
            try
            {
                var model = Battle(); var player = model.Units.First(u => u.Side == BattleSide.Player); var enemy = model.Units.First(u => u.Side == BattleSide.Enemy);
                typeof(BattleSimulator).GetMethod("AddPoison", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(model, new object[] { enemy, player, 3 });
                var visual = Point("AffectedEnemy", root.transform, 400, 500);
                var owner = new GameObject("MarkerOwner"); owner.transform.SetParent(root.transform, false);
                var cue = owner.AddComponent<BattleHeadEffects1019>(); cue.Configure((RectTransform)root.transform, model, () => enemy, () => visual);
                Assert.That(cue.DisplayedPoisonStacks, Is.EqualTo(model.PoisonStacks(enemy)).And.EqualTo(3));
                Assert.That(cue.DisplayedCount, Is.EqualTo(1));
                int elapsed = model.ElapsedMilliseconds; cue.Refresh(); Assert.That(model.ElapsedMilliseconds, Is.EqualTo(elapsed));
                enemy.Hp = 0; cue.Refresh(); Assert.That(cue.DisplayedPoisonStacks, Is.Zero); Assert.That(cue.DisplayedCount, Is.Zero);
                Assert.That(root.transform.Find("AffectedUnitHeadEffects").gameObject.activeSelf, Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }
        static GameObject Stage()
        {
            var go = new GameObject("Presentation1019", typeof(RectTransform)); var rect = (RectTransform)go.transform;
            rect.pivot = new Vector2(0, 1); rect.sizeDelta = new Vector2(720, 1501.63f); return go;
        }
        static RectTransform Point(string name, Transform parent, float x, float y)
        {
            var go = new GameObject(name, typeof(RectTransform)); var rect = (RectTransform)go.transform; rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = Vector2.one * .5f; rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(100, 150); return rect;
        }
        static BattleSimulator Battle()
        {
            var data = new TacticsManifest();
            data.Skills.Add(new SkillDefinition { Id = "strike", Name = "普攻", Effect = SkillEffect.Damage, Pattern = SkillPattern.Single, PowerPermille = 1000 });
            data.Units.Add(new UnitDefinition { Id = "demo", Name = "测试队员", MaxHp = 10000, Attack = 10, Defense = 5, Speed = 100, SkillIds = new List<string> { "strike" } });
            data.Units.Add(new UnitDefinition { Id = "foe", Name = "测试敌人", MaxHp = 10000, Attack = 10, Defense = 5, Speed = 50, SkillIds = new List<string> { "strike" } });
            var stage = new StageDefinition { Id = "presentation", Name = "表现测试", TimeLimitSeconds = 120, TurnLimit = 20,
                Enemies = new List<EnemySpawn> { new EnemySpawn { UnitId = "foe", Row = 0, Col = 0 } } };
            data.Stages.Add(stage); var simulator = new BattleSimulator(data, stage, new[] { new PlayerUnitSetup { UnitId = "demo", Row = 0, Col = 0 } }, new SeededRandom(1019));
            simulator.EnableRealtime(new Dictionary<string, string> { { "demo", "恶魔" } }, "demo"); return simulator;
        }
    }
}
