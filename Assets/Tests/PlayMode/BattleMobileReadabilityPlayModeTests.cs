using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ChoSiren.Panels;
using ChoSiren.Systems;
using ChoSiren.Systems.Tactics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.Tests
{
    public sealed class BattleMobileReadabilityPlayModeTests
    {
        private static readonly string[] Members = { "xingli", "feiyin", "wubai", "yeying" };

        [Ignore("Quarantined 2026-11-08: battle presentation redesigned (PSD battle 10-28 to 11-07); see Docs/TEST-QUARANTINE-1108.md")]
        [Test]
        public void ActualPsdCardsUseCachedBustCropsAndLeaveSharedFullBodiesIntact()
        {
            GameObject root = NewRoot();
            try
            {
                var panel = TacticsBattlePanel.Open(root.transform, new GameModel(() => new System.DateTime(2026, 10, 16, 12, 0, 0), new InMemorySaveStore()), CreateBattle(), null);
                var cards = PlayerCards(panel);
                Assert.That(cards, Has.Length.EqualTo(4));
                foreach (Transform card in cards)
                {
                    var portrait = Find(card, "PerformerMotion").Find("Portrait").GetComponent<Image>();
                    Assert.That(portrait.sprite, Is.Not.Null);
                    string memberId = portrait.sprite.name.Replace("-BattleCloseup", "");
                    Assert.That(Members, Does.Contain(memberId));
                    Sprite original = Resources.Load<Sprite>(GameModel.Members.First(m => m.Id == memberId).ResourcePath);
                    Assert.That(portrait.sprite.texture, Is.SameAs(original.texture),
                        "The bust must reuse native RGBA, never a flattened replacement.");
                    Assert.That(portrait.sprite.rect.height, Is.LessThan(original.rect.height * .45f),
                        "The live PSD card must not silently fall back to the full-body art.");
                    Assert.That(portrait.sprite.rect.yMin, Is.GreaterThan(original.rect.yMin + original.rect.height * .5f));
                    Assert.That(portrait.sprite.rect.xMin, Is.GreaterThanOrEqualTo(original.rect.xMin));
                    Assert.That(portrait.sprite.rect.xMax, Is.LessThanOrEqualTo(original.rect.xMax));
                    Assert.That(portrait.sprite.rect.yMax, Is.EqualTo(original.rect.yMax).Within(.01f),
                        "Native hair and ear tips at the top source edge must remain in the crop.");
                    Assert.That(portrait.sprite.rect.width, Is.GreaterThanOrEqualTo(original.rect.width * .6f),
                        "A face-only horizontal crop must not slice through hair or ears.");
                    if (memberId == "wubai")
                        Assert.That(portrait.sprite.rect.xMax, Is.EqualTo(original.rect.xMax).Within(.01f),
                            "The updated right ponytail must retain the full source edge.");
                    Assert.That(portrait.GetComponentsInParent<RectMask2D>(true).All(m => m.name == "BattlePSD928"), Is.True,
                        "The portrait/motion rig must not introduce a second head-clipping mask.");
                    Assert.That(portrait.preserveAspect, Is.True);
                    Assert.That(portrait.raycastTarget, Is.False);
                    Assert.That(Invoke(panel, "BattlePortrait", memberId), Is.SameAs(portrait.sprite),
                        "Repeated refreshes must reuse the crop instead of allocating another sprite.");
                    Assert.That(portrait.rectTransform.rect.width / portrait.rectTransform.rect.height,
                        Is.EqualTo(portrait.sprite.rect.width / portrait.sprite.rect.height).Within(.01f));
                }
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Ignore("Quarantined 2026-11-08: battle presentation redesigned (PSD battle 10-28 to 11-07); see Docs/TEST-QUARANTINE-1108.md")]
        [Test]
        public void IdleReadyAndAttackKeepFullPortraitsInsideTheirAreaAndReadoutsBelow()
        {
            GameObject root = NewRoot();
            try
            {
                var panel = TacticsBattlePanel.Open(root.transform, new GameModel(() => new System.DateTime(2026, 10, 16, 12, 0, 0), new InMemorySaveStore()), CreateBattle(), null);
                panel.StopAllCoroutines();
                foreach (Transform card in PlayerCards(panel))
                {
                    var area = (RectTransform)Find(card, "PsdPortraitArea");
                    var motion = Find(card, "PerformerMotion").GetComponent<EnemyUnitPresentation>();
                    var portrait = Find(card, "PerformerMotion").Find("Portrait").GetComponent<Image>();
                    var album = card.GetComponent<BattleMemberAlbum929>();
                    RectTransform hp = (RectTransform)Find(card, "UnitHpText");
                    RectTransform attack = (RectTransform)Find(card, "AttackBadge");
                    RectTransform skill = (RectTransform)Find(card, "BattleSkillDock");
                    RectTransform energy = (RectTransform)Find(card, "PsdEnergyReadout");
                    foreach (bool ready in new[] { false, true, false })
                    {
                        bool value = ready;
                        typeof(BattleMemberAlbum929).GetField("ready", BindingFlags.Instance | BindingFlags.NonPublic)
                            .SetValue(album, (System.Func<bool>)(() => value));
                        album.Advance(.5f);
                        Assert.That(album.Expansion, Is.EqualTo(ready ? 1 : 0));
                        AssertInside(portrait.rectTransform, area);
                        foreach (RectTransform reading in new[] { hp, attack, skill, energy })
                            AssertAbove(portrait.rectTransform, reading);
                        foreach (bool heavy in new[] { false, true })
                        {
                            motion.PlayAttack(heavy);
                            for (int step = 0; step < 14; step++)
                            {
                                motion.AdvancePresentation(.05f);
                                AssertInside(portrait.rectTransform, area);
                                foreach (RectTransform reading in new[] { hp, attack, skill, energy })
                                    AssertAbove(portrait.rectTransform, reading);
                            }
                        }
                        motion.PlayHit(true);
                        for (int step = 0; step < 6; step++)
                        {
                            motion.AdvancePresentation(.05f);
                            AssertInside(portrait.rectTransform, area);
                            AssertAbove(portrait.rectTransform, hp);
                        }
                    }
                    Transform frame = Find(card, "PsdUnitFrame");
                    Assert.That(frame.GetSiblingIndex(), Is.LessThan(area.GetSiblingIndex()),
                        "The illustrated frame must render behind complete hair, not across it.");
                    AssertInside((RectTransform)frame, (RectTransform)card);
                    AssertInside((RectTransform)Find(card, "PsdUnitRear"), (RectTransform)card);
                    AssertInside((RectTransform)Find(card, "MemberLiveReadout"), (RectTransform)frame);
                    Assert.That(Find(card, "AttackBadge").Find("Gain").gameObject.activeSelf, Is.False,
                        "The compact numeric strip must not add a second floating attack number.");
                    AssertAbove((RectTransform)Find(card, "PsdGuardRing"), hp);
                    AssertAbove((RectTransform)Find(card, "PsdGuardRing").Find("ShieldFrame"), hp);
                    AssertAbove((RectTransform)card, (RectTransform)Find(panel.transform, "PSD-battle-49"));
                }
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Ignore("Quarantined 2026-11-08: battle presentation redesigned (PSD battle 10-28 to 11-07); see Docs/TEST-QUARANTINE-1108.md")]
        [Test]
        public void PhoneReadoutsHaveRealLineBoxesAndDistinctSkillAndAttackRows()
        {
            GameObject root = NewRoot();
            try
            {
                var panel = TacticsBattlePanel.Open(root.transform, new GameModel(() => new System.DateTime(2026, 10, 16, 12, 0, 0), new InMemorySaveStore()), CreateBattle(), null);
                Canvas.ForceUpdateCanvases();
                foreach (Transform card in PlayerCards(panel))
                {
                    Text hp = Find(card, "UnitHpText").GetComponent<Text>();
                    Assert.That(hp.text, Is.EqualTo("9999/9999"));
                    Assert.That(hp.fontSize, Is.EqualTo(18));
                    Assert.That(hp.resizeTextMinSize, Is.GreaterThanOrEqualTo(16));
                    Assert.That(hp.preferredWidth, Is.LessThanOrEqualTo(hp.rectTransform.rect.width),
                        "Four-digit health must not require a tiny best-fit font.");
                    Assert.That(hp.preferredHeight, Is.LessThanOrEqualTo(hp.rectTransform.rect.height));
                    Transform dock = Find(card, "BattleSkillDock");
                    var glyphs = dock.GetComponentsInChildren<SkillIconGraphic>();
                    Assert.That(glyphs, Has.Length.EqualTo(4));
                    foreach (SkillIconGraphic glyph in glyphs)
                    {
                        Assert.That(glyph.rectTransform.rect.width * 390f / 720f, Is.GreaterThanOrEqualTo(17f),
                            "At 390px width each skill needs an identifiable silhouette.");
                        Assert.That(glyph.raycastTarget, Is.False);
                        AssertInside(glyph.rectTransform, (RectTransform)dock);
                    }
                    Text attack = Find(card, "AttackBadge").Find("Punch/Value").GetComponent<Text>();
                    Assert.That(attack.fontSize, Is.EqualTo(18));
                    Assert.That(attack.resizeTextMinSize, Is.GreaterThanOrEqualTo(16));
                    RectTransform badge = (RectTransform)Find(card, "AttackBadge");
                    AssertNoVerticalOverlap(badge, (RectTransform)Find(card, "PerformerAction"));
                    AssertNoVerticalOverlap(badge, (RectTransform)dock);
                    AssertNoVerticalOverlap(hp.rectTransform, (RectTransform)Find(card, "CaptainBadge"));
                }
                var guide = Find(panel.transform, "BattleQuickGuide").GetComponent<Button>();
                Assert.That(guide.interactable, Is.True);
                Assert.That(Find(panel.transform, "RulesOverlay").gameObject.activeSelf, Is.False,
                    "Starting a fight must not force a tutorial modal on the player.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Ignore("Quarantined 2026-11-08: battle presentation redesigned (PSD battle 10-28 to 11-07); see Docs/TEST-QUARANTINE-1108.md")]
        [Test]
        public void CompactCardsPutHealthEnergyAndAttackInOneShortReadableBlock()
        {
            const float scale = 720f / 2946f;
            GameObject root = NewRoot();
            try
            {
                var panel = TacticsBattlePanel.Open(root.transform,
                    new GameModel(() => new System.DateTime(2026, 10, 16, 12, 0, 0), new InMemorySaveStore()),
                    CreateBattle(), null);
                Invoke(panel, "RefreshRealtimeCommands");
                Canvas.ForceUpdateCanvases();
                foreach (Transform card in PlayerCards(panel))
                {
                    Assert.That(((RectTransform)card).rect.height, Is.LessThanOrEqualTo(1370f * scale + .01f));
                    var health = Find(card, "UnitHpText").GetComponent<Text>();
                    health.text = "99999/99999";
                    Assert.That(health.fontSize, Is.EqualTo(18));
                    Assert.That(health.preferredWidth, Is.LessThanOrEqualTo(health.rectTransform.rect.width),
                        "Even five-digit HP pairs must fit at the actual 18px font, not shrink to 9px.");
                    Assert.That(health.preferredHeight, Is.LessThanOrEqualTo(health.rectTransform.rect.height));
                    var attack = (RectTransform)Find(card, "AttackBadge");
                    var energy = (RectTransform)Find(card, "PsdEnergyReadout");
                    float top = -health.rectTransform.anchoredPosition.y;
                    float bottom = Mathf.Max(-attack.anchoredPosition.y + attack.rect.height,
                        -energy.anchoredPosition.y + energy.rect.height);
                    Assert.That(bottom - top, Is.LessThanOrEqualTo(260f * scale + .01f),
                        "HP, attack and energy must share a compact block rather than three distant rows.");
                    var meter = card.GetComponent<BattleEnergyMeter>();
                    var energyTrack = (RectTransform)meter.Fill.transform.parent;
                    AssertInside(energyTrack, attack);
                    Assert.That(attack.GetSiblingIndex(), Is.GreaterThan(energyTrack.GetSiblingIndex()),
                        "The shared blue track must not paint over its real attack value.");
                    Assert.That(energyTrack.rect.height, Is.LessThanOrEqualTo(54f * scale + .01f));
                    Assert.That(Find(card, "AttackBadge").Find("Punch/Plate").GetComponent<Image>().color.a, Is.Zero,
                        "The attack number belongs on the blue strip without a separate black board.");
                    var guard = card.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == "TacticalGuard");
                    if (guard != null) AssertInside(guard.GetComponent<RectTransform>(), (RectTransform)card);
                }
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void QuickGuideCanBeClosedAndReplayedWithoutChangingDiceOrLosingPauseState()
        {
            GameObject root = NewRoot();
            try
            {
                var panel = TacticsBattlePanel.Open(root.transform, new GameModel(() => new System.DateTime(2026, 10, 16, 12, 0, 0), new InMemorySaveStore()), CreateBattle(), null);
                var dice = panel.Battle.BattleDice;
                int[] faces = dice.Values.ToArray();
                int bonus = dice.AccumulatedBonusPermille, revision = dice.Revision;
                int rerolls = dice.RerollsRemaining, clock = panel.Battle.ElapsedMilliseconds;
                int captain = panel.Battle.CurrentLeaderId;
                int[] hp = panel.Battle.Units.Select(u => u.Hp).ToArray();
                Button guide = Find(panel.transform, "BattleQuickGuide").GetComponent<Button>();
                Button back = Find(panel.transform, "RulesBack").GetComponent<Button>();

                guide.onClick.Invoke();
                Assert.That(panel.IsPaused, Is.True);
                Assert.That(Find(panel.transform, "RulesOverlay").gameObject.activeSelf, Is.True);
                string rules = Find(panel.transform, "RulesBody").GetComponent<Text>().text;
                // 2026-11-01 onboarding: the rules page is one short line per rule (dice: 2 per pick, none = all six).
                Assert.That(rules, Does.Contain("【骰子】").And.Contain("【大招】").And.Contain("【队长】"));
                Assert.That(rules, Does.Contain("每次最多2颗").And.Contain("不选=6颗全部重投"));
                Invoke(panel, "Update");
                back.onClick.Invoke();
                Assert.That(panel.IsPaused, Is.False, "A guide opened during play resumes when closed.");

                Invoke(panel, "TogglePause");
                guide.onClick.Invoke();
                back.onClick.Invoke();
                Assert.That(panel.IsPaused, Is.True, "A guide opened from pause must preserve the pause.");
                Invoke(panel, "TogglePause");
                guide.onClick.Invoke();
                Find(panel.transform, "PauseToggle").GetComponent<Button>().onClick.Invoke();
                Assert.That(panel.IsPaused, Is.False, "The resume control must not re-pause while closing rules.");
                Assert.That(Find(panel.transform, "RulesOverlay").gameObject.activeSelf, Is.False);

                Assert.That(dice.Values, Is.EqualTo(faces));
                Assert.That(dice.AccumulatedBonusPermille, Is.EqualTo(bonus));
                Assert.That(dice.Revision, Is.EqualTo(revision));
                Assert.That(dice.RerollsRemaining, Is.EqualTo(rerolls));
                Assert.That(panel.Battle.ElapsedMilliseconds, Is.EqualTo(clock));
                Assert.That(panel.Battle.CurrentLeaderId, Is.EqualTo(captain));
                Assert.That(panel.Battle.Units.Select(u => u.Hp), Is.EqualTo(hp));
                Assert.That(panel.Battle.TacticalGuardUsed, Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static GameObject NewRoot()
        {
            var root = new GameObject("Mobile Battle Readability", typeof(RectTransform));
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(720, 1536);
            return root;
        }

        private static Transform[] PlayerCards(TacticsBattlePanel panel) => panel.GetComponentsInChildren<Transform>()
            .Where(t => t.name.StartsWith("Cell-P-")).OrderBy(t => t.GetComponent<RectTransform>().anchoredPosition.x).ToArray();

        private static Transform Find(Transform root, string name)
        {
            Transform result = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
            Assert.That(result, Is.Not.Null, "Missing battle node: " + name);
            return result;
        }

        private static object Invoke(TacticsBattlePanel panel, string name, params object[] args) =>
            typeof(TacticsBattlePanel).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(panel, args);

        private static void AssertInside(RectTransform child, RectTransform parent)
        {
            var corners = new Vector3[4]; child.GetWorldCorners(corners);
            foreach (Vector3 corner in corners)
            {
                Vector3 local = parent.InverseTransformPoint(corner);
                Assert.That(local.x, Is.InRange(parent.rect.xMin - .01f, parent.rect.xMax + .01f));
                Assert.That(local.y, Is.InRange(parent.rect.yMin - .01f, parent.rect.yMax + .01f));
            }
        }

        private static void AssertNoVerticalOverlap(RectTransform first, RectTransform second)
        {
            var a = new Vector3[4]; var b = new Vector3[4];
            first.GetWorldCorners(a); second.GetWorldCorners(b);
            Assert.That(a.Max(p => p.y) <= b.Min(p => p.y) + .01f || b.Max(p => p.y) <= a.Min(p => p.y) + .01f,
                Is.True, first.name + " must not cover " + second.name);
        }

        private static void AssertAbove(RectTransform first, RectTransform second)
        {
            var a = new Vector3[4]; var b = new Vector3[4];
            first.GetWorldCorners(a); second.GetWorldCorners(b);
            Assert.That(a.Min(p => p.y), Is.GreaterThanOrEqualTo(b.Max(p => p.y) - .01f),
                first.name + " must stay entirely above " + second.name);
        }

        private static BattleSimulator CreateBattle()
        {
            var manifest = new TacticsManifest();
            manifest.Skills.Add(new SkillDefinition { Id = "strike", Name = "攻击", Effect = SkillEffect.Damage,
                Pattern = SkillPattern.Single, PowerPermille = 1000 });
            foreach (string id in Members)
                manifest.Units.Add(new UnitDefinition { Id = id, Name = id, MaxHp = 9999, Attack = 1234,
                    Defense = 20, Speed = 100, SkillIds = new List<string> { "strike" } });
            manifest.Units.Add(new UnitDefinition { Id = "enemy", Name = "敌方", MaxHp = 999999, Attack = 1,
                Defense = 10, Speed = 50, SkillIds = new List<string> { "strike" } });
            var stage = new StageDefinition { Id = "mobile-readability-test", Name = "手机战斗布局测试",
                EncounterType = "normal", TurnLimit = 20, TimeLimitSeconds = 120,
                Enemies = new List<EnemySpawn> { new EnemySpawn { UnitId = "enemy", Row = 0, Col = 0 } } };
            manifest.Stages.Add(stage);
            var party = new List<PlayerUnitSetup>();
            for (int i = 0; i < Members.Length; i++)
                party.Add(new PlayerUnitSetup { UnitId = Members[i], Row = i % 3, Col = i / 3 });
            return new BattleSimulator(manifest, stage, party, new ScriptedRandom(new[] { 999 }, new[] { 0, 1, 2, 3, 5 }));
        }
    }
}
