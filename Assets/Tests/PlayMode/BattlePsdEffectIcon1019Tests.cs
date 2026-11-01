using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ChoSiren.Panels;
using ChoSiren.Systems;
using ChoSiren.Systems.Tactics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ChoSiren.Tests
{
    public sealed class BattlePsdEffectIcon1019Tests
    {
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void PooledHealPopupLosesItsSpriteWhenReusedForPlainDamageOrShield()
        {
            BattlePsdArt928.LoadLocal();
            var root = Stage();
            try
            {
                var panel = root.AddComponent<TacticsBattlePanel>();
                Type kitType = typeof(TacticsBattlePanel).Assembly.GetType("ChoSiren.Panels.PanelKit");
                typeof(TacticsBattlePanel).GetField("kit", Hidden).SetValue(panel,
                    Activator.CreateInstance(kitType, new object[] { "EffectIconPoolTest" }));
                typeof(TacticsBattlePanel).GetMethod("BuildPopups", Hidden).Invoke(panel, null);
                var anchor = Point("Anchor", root.transform);
                var spawn = typeof(TacticsBattlePanel).GetMethod("SpawnPopupAt", Hidden);
                var pool = (List<Text>)typeof(TacticsBattlePanel).GetField("popupPool", Hidden).GetValue(panel);
                var icons = (SkillIconGraphic[])typeof(TacticsBattlePanel).GetField("popupIcons", Hidden).GetValue(panel);
                spawn.Invoke(panel, new object[] { anchor, 42, "+25", Color.green, 22, 22f, (SkillIconKind?)SkillIconKind.Heal });
                int slot = pool.FindIndex(p => p.gameObject.activeSelf);
                Assert.That(slot, Is.GreaterThanOrEqualTo(0));
                Text reused = pool[slot];
                var image = icons[slot].transform.Find("AuthoredEffectIcon").GetComponent<Image>();
                Assert.That(image.sprite, Is.SameAs(BattlePsdArt928.Sprite("effect-heal-plus")));
                Assert.That(image.gameObject.activeInHierarchy, Is.True);
                Assert.That(image.preserveAspect, Is.True);
                Assert.That(image.raycastTarget, Is.False);
                Assert.That(image.color.r, Is.EqualTo(1));
                Assert.That(image.color.g, Is.EqualTo(1));
                Assert.That(image.color.b, Is.EqualTo(1));
                Assert.That(icons[slot].enabled, Is.False, "The original glyph must not render underneath the PSD plus.");

                spawn.Invoke(panel, new object[] { anchor, 42, "18", Color.white, 22, 22f, null });
                Assert.That(pool[slot], Is.SameAs(reused));
                Assert.That(reused.text, Is.EqualTo("18"));
                Assert.That(image.sprite, Is.Null);
                Assert.That(image.gameObject.activeSelf, Is.False, "A real reused damage popup must not retain a healing plus.");
                Assert.That(icons[slot].gameObject.activeSelf, Is.False);

                spawn.Invoke(panel, new object[] { anchor, 42, "+10", Color.cyan, 22, 22f, (SkillIconKind?)SkillIconKind.Shield });
                Assert.That(pool[slot], Is.SameAs(reused));
                Assert.That(icons[slot].enabled, Is.True);
                Assert.That(icons[slot].Kind, Is.EqualTo(SkillIconKind.Shield));
                Assert.That(image.sprite, Is.Null);
                Assert.That(image.gameObject.activeSelf, Is.False);

                // A missing optional authored asset must still show the original semantic glyph.
                var optional = icons[slot].GetComponent<BattlePsdEffectIcon1019>();
                optional.Configure(icons[slot], SkillIconKind.Heal, "effect-not-installed");
                Assert.That(optional.Apply(SkillIconKind.Heal, Color.green), Is.SameAs(icons[slot]));
                Assert.That(icons[slot].enabled, Is.True);
                Assert.That(icons[slot].Kind, Is.EqualTo(SkillIconKind.Heal));
                Assert.That(image.sprite, Is.Null);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void PoisonSpriteFollowsRealStacksAndDoesNotLeakIntoTheNextShieldState()
        {
            BattlePsdArt928.LoadLocal();
            var root = Stage();
            try
            {
                var model = Battle();
                var player = model.Units.First(u => u.Side == BattleSide.Player);
                var enemy = model.Units.First(u => u.Side == BattleSide.Enemy);
                typeof(BattleSimulator).GetMethod("AddPoison", Hidden).Invoke(model, new object[] { enemy, player, 3 });
                var visual = Point("EnemyVisual", root.transform);
                var owner = new GameObject("PoisonOwner"); owner.transform.SetParent(root.transform, false);
                var marker = owner.AddComponent<BattleHeadEffects1019>();
                marker.Configure((RectTransform)root.transform, model, () => enemy, () => visual);
                var strip = root.transform.Find("AffectedUnitHeadEffects");
                var glyph = strip.Find("Effect-0/EffectSymbol").GetComponent<SkillIconGraphic>();
                var image = glyph.transform.Find("AuthoredEffectIcon").GetComponent<Image>();
                Assert.That(marker.DisplayedPoisonStacks, Is.EqualTo(3));
                Assert.That(image.sprite, Is.SameAs(BattlePsdArt928.Sprite("effect-poison-drop")));
                Assert.That(image.gameObject.activeInHierarchy, Is.True);
                Assert.That(image.preserveAspect, Is.True);
                Assert.That(image.color, Is.EqualTo(Color.white));
                Assert.That(glyph.enabled, Is.False);

                // Tick the actual model: poison 3 -> 1 -> 0, rather than hiding the UI by hand.
                var tick = typeof(BattleSimulator).GetMethod("ResolvePoison", Hidden);
                tick.Invoke(model, null); tick.Invoke(model, null); marker.Refresh();
                Assert.That(enemy.Alive, Is.True);
                Assert.That(model.PoisonStacks(enemy), Is.Zero);
                Assert.That(marker.DisplayedCount, Is.Zero);
                Assert.That(strip.gameObject.activeSelf, Is.False);
                Assert.That(image.sprite, Is.Null);

                enemy.Shield = 5; marker.Refresh();
                Assert.That(strip.gameObject.activeSelf, Is.True);
                Assert.That(marker.DisplayedPoisonStacks, Is.Zero);
                Assert.That(glyph.Kind, Is.EqualTo(SkillIconKind.Shield));
                Assert.That(glyph.enabled, Is.True);
                Assert.That(image.sprite, Is.Null);
                Assert.That(image.gameObject.activeSelf, Is.False, "The status slot now represents a shield, not the old poison.");
                enemy.Hp = 0; marker.Refresh();
                Assert.That(strip.gameObject.activeSelf, Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }

        static GameObject Stage()
        {
            var go = new GameObject("EffectIcon1019Test", typeof(RectTransform));
            var rect = (RectTransform)go.transform; rect.pivot = new Vector2(0, 1); rect.sizeDelta = new Vector2(720, 1536);
            return go;
        }
        static RectTransform Point(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)); var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false); rect.sizeDelta = new Vector2(100, 150); return rect;
        }
        static BattleSimulator Battle()
        {
            var data = new TacticsManifest();
            data.Skills.Add(new SkillDefinition { Id = "strike", Name = "attack", Effect = SkillEffect.Damage, Pattern = SkillPattern.Single, PowerPermille = 1000 });
            data.Units.Add(new UnitDefinition { Id = "demo", Name = "player", MaxHp = 10000, Attack = 10, Defense = 5, Speed = 100, SkillIds = new List<string> { "strike" } });
            data.Units.Add(new UnitDefinition { Id = "foe", Name = "enemy", MaxHp = 10000, Attack = 10, Defense = 5, Speed = 50, SkillIds = new List<string> { "strike" } });
            var stage = new StageDefinition { Id = "effects", Name = "effects", TimeLimitSeconds = 120, TurnLimit = 20,
                Enemies = new List<EnemySpawn> { new EnemySpawn { UnitId = "foe", Row = 0, Col = 0 } } };
            data.Stages.Add(stage);
            var model = new BattleSimulator(data, stage, new[] { new PlayerUnitSetup { UnitId = "demo", Row = 0, Col = 0 } }, new SeededRandom(1019));
            model.EnableRealtime(new Dictionary<string, string> { { "demo", "恶魔" } }, "demo"); return model;
        }
    }
}
