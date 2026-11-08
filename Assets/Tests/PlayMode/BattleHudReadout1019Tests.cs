using System;
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
    public sealed class BattleHudReadout1019Tests
    {
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        GameObject host;
        TacticsBattlePanel panel;
        BattleSimulator battle;

        [SetUp] public void Before()
        {
            host = new GameObject("BattleHudReadout1019", typeof(RectTransform));
            ((RectTransform)host.transform).sizeDelta = new Vector2(720, 1536);
            var model = new GameModel(() => new DateTime(2026, 10, 19, 12, 0, 0), new InMemorySaveStore());
            battle = model.StartStageBattle("stage-1-1", 1019UL, out var message);
            Assert.That(battle, Is.Not.Null, message);
            panel = TacticsBattlePanel.Open(host.transform, model, battle, null);
            panel.StopAllCoroutines();
            Canvas.ForceUpdateCanvases();
        }
        [TearDown] public void After() => Object.DestroyImmediate(host);

        [Test]
        public void ChargeReadoutLivesCenteredInsideTheBlueTrackWithoutDuplicatingTheOldCornerText()
        {
            var fill = (Image)typeof(TacticsBattlePanel).GetField("diceEnergyFill", Hidden).GetValue(panel);
            var track = (RectTransform)fill.transform.parent;
            var label = Find("RerollChargeLabel").GetComponent<Text>();
            var old = (Text)typeof(TacticsBattlePanel).GetField("diceEnergyText", Hidden).GetValue(panel);
            int elapsed = battle.ElapsedMilliseconds;
            foreach (int gain in new[] { 0, 8, 30, 70 })
            {
                battle.BattleDice.GainEnergy(gain);
                Call("RefreshPsdDice");
                Assert.That(label.transform.parent, Is.SameAs(track));
                Assert.That(label.rectTransform.rect.size, Is.EqualTo(track.rect.size));
                Assert.That(label.rectTransform.anchoredPosition, Is.EqualTo(Vector2.zero));
                Assert.That(label.alignment, Is.EqualTo(TextAnchor.MiddleCenter));
                Assert.That(label.color, Is.EqualTo(Color.white), "White plus a dark outline works on both light and dark blue");
                Assert.That(label.GetComponent<Outline>().effectColor.a, Is.GreaterThan(.9f));
                Assert.That(label.transform.GetSiblingIndex(), Is.GreaterThan(fill.transform.GetSiblingIndex()));
                Assert.That(label.preferredWidth, Is.LessThan(track.rect.width));
                Assert.That(old.gameObject.activeSelf, Is.False);
                Assert.That(old.text, Is.Empty);
            }
            const float scale = 720f / 2946f;
            Assert.That(track.rect.size, Is.EqualTo(new Vector2(1760 * scale, 62 * scale)), "Do not enlarge the authored frame to fit its label");
            Assert.That(battle.ElapsedMilliseconds, Is.EqualTo(elapsed));
        }

        [Test]
        public void CaptainBenefitFitsInsideItsOwnRibbonAndNoDiceOrSquareCardCoversIt()
        {
            var slot = (RectTransform)Find("PsdDiceSubtitle");
            var label = Find("DiceInstruction").GetComponent<Text>();
            var cover = Find("DiceSubCover").GetComponent<Image>();
            var psd = (RectTransform)Find("BattlePSD928");
            var frame = (RectTransform)Find("PSD-dice-bar");
            Assert.That(label.transform.parent, Is.SameAs(slot));
            Assert.That(cover.transform.parent, Is.SameAs(slot));
            Assert.That(cover.sprite, Is.Not.Null, "The sample-text cover follows the angled ribbon, not a new square black card");
            Assert.That(label.transform.GetSiblingIndex(), Is.GreaterThan(cover.transform.GetSiblingIndex()));
            Assert.That(label.rectTransform.rect.height, Is.GreaterThanOrEqualTo(label.preferredHeight));
            Assert.That(label.verticalOverflow, Is.EqualTo(VerticalWrapMode.Overflow));
            AssertInside(slot, frame, psd); AssertInside(cover.rectTransform, frame, psd);
            Rect subtitle = Bounds(slot, psd);
            for (int i = 0; i < 6; i++)
                Assert.That(subtitle.yMin, Is.GreaterThan(Bounds((RectTransform)Find("Dice-" + i), psd).yMax), "Dice hit/roll rectangles remain below the complete benefit reading");
            Assert.That(label.GetComponentsInParent<RectMask2D>(true).All(m => m.name == "BattlePSD928"), Is.True);
            Assert.That(label.raycastTarget, Is.False);
        }

        [Ignore("Quarantined 2026-11-08: battle presentation redesigned (PSD battle 10-28 to 11-07); see Docs/TEST-QUARANTINE-1108.md")]
        [Test]
        public void SmallSkillFlashesOnlyAboveItsOwnMemberAndThenRetiresFromTheHorizontalRow()
        {
            var caster = battle.Units.First(u => u.Side == BattleSide.Player && u.Definition.Id == "yeying");
            typeof(TacticsBattlePanel).GetMethod("PulsePsdSkill1019", Hidden).Invoke(panel, new object[] { caster, 1 });
            var visible = panel.GetComponentsInChildren<Image>(true).Where(i => i.name.StartsWith("PsdSkillRow-") && i.gameObject.activeInHierarchy).ToArray();
            Assert.That(visible, Has.Length.EqualTo(1));
            Assert.That(visible[0].name, Is.EqualTo("PsdSkillRow-1"));
            Assert.That(visible[0].sprite, Is.SameAs(BattlePsdArt928.Sprite("icon-gold-shield")));
            var card = visible[0].transform.parent;
            Assert.That(card.GetComponentsInChildren<Image>(true).Any(i => i.sprite != null && i.sprite.name.StartsWith("yeying-Battle")), Is.True);
            var rows = card.GetComponentsInChildren<Image>(true).Where(i => i.name.StartsWith("PsdSkillRow-")).OrderBy(i => i.name).ToArray();
            Assert.That(rows, Has.Length.EqualTo(3));
            for (int i = 1; i < rows.Length; i++)
                Assert.That(rows[i].rectTransform.anchoredPosition.y, Is.EqualTo(rows[0].rectTransform.anchoredPosition.y).Within(.001f));
            typeof(TacticsBattlePanel).GetField("presentationClock", Hidden).SetValue(panel, 2f);
            Call("RefreshPsdSkillCues1019");
            Assert.That(rows.All(i => !i.gameObject.activeInHierarchy), Is.True);
        }

        [Test]
        public void PoisonControlAndShieldAreOneHeadRowOnTheActualAffectedActorOnly()
        {
            var player = battle.Units.First(u => u.Side == BattleSide.Player);
            var enemy = battle.Units.First(u => u.Side == BattleSide.Enemy);
            typeof(BattleSimulator).GetMethod("AddPoison", Hidden).Invoke(battle, new object[] { enemy, player, 3 });
            typeof(BattleSimulator).GetMethod("ApplyCondition", Hidden).Invoke(battle, new object[] { player, enemy, CombatCondition.Stun, 3000 });
            enemy.Shield = 50;
            var visual = new GameObject("AffectedEnemyVisual", typeof(RectTransform)).GetComponent<RectTransform>();
            visual.SetParent(host.transform, false);
            visual.anchorMin = visual.anchorMax = visual.pivot = Vector2.up;
            visual.anchoredPosition = new Vector2(450, -450); visual.sizeDelta = new Vector2(120, 200);
            var owner = new GameObject("AffectedEnemyMarker"); owner.transform.SetParent(host.transform, false);
            var cue = owner.AddComponent<BattleHeadEffects1019>();
            cue.Configure((RectTransform)host.transform, battle, () => enemy, () => visual);
            var strip = (RectTransform)host.transform.Find("AffectedUnitHeadEffects");
            Assert.That(cue.DisplayedCount, Is.EqualTo(3));
            var symbols = strip.GetComponentsInChildren<SkillIconGraphic>(true);
            Assert.That(symbols.Select(g => g.Kind), Is.EqualTo(new[] { SkillIconKind.Poison, SkillIconKind.Burst, SkillIconKind.Shield }));
            var slots = strip.Cast<Transform>().Where(t => t.name.StartsWith("Effect-")).Select(t => (RectTransform)t).ToArray();
            Assert.That(slots, Has.Length.EqualTo(3));
            Assert.That(slots.Select(s => s.anchoredPosition.y).Distinct().Count(), Is.EqualTo(1));
            for (int i = 1; i < slots.Length; i++) Assert.That(slots[i].anchoredPosition.x, Is.GreaterThan(slots[i - 1].anchoredPosition.x + slots[i - 1].rect.width));
            Assert.That(symbols.All(g => g.rectTransform.rect.width >= 24), Is.True, "Icons must remain identifiable on a phone");
            enemy.Hp = 0; cue.Refresh(); Assert.That(strip.gameObject.activeSelf, Is.False);
        }

        Transform Find(string name) => panel.GetComponentsInChildren<Transform>(true).First(t => t.name == name);
        void Call(string name) => typeof(TacticsBattlePanel).GetMethod(name, Hidden).Invoke(panel, null);
        static Rect Bounds(RectTransform item, RectTransform root)
        {
            var corners = new Vector3[4]; item.GetWorldCorners(corners);
            var local = corners.Select(p => root.InverseTransformPoint(p)).ToArray();
            return Rect.MinMaxRect(local.Min(p => p.x), local.Min(p => p.y), local.Max(p => p.x), local.Max(p => p.y));
        }
        static void AssertInside(RectTransform item, RectTransform frame, RectTransform root)
        {
            Rect a = Bounds(item, root), b = Bounds(frame, root);
            Assert.That(a.xMin, Is.GreaterThanOrEqualTo(b.xMin - .01f)); Assert.That(a.xMax, Is.LessThanOrEqualTo(b.xMax + .01f));
            Assert.That(a.yMin, Is.GreaterThanOrEqualTo(b.yMin - .01f)); Assert.That(a.yMax, Is.LessThanOrEqualTo(b.yMax + .01f));
        }
    }
}
