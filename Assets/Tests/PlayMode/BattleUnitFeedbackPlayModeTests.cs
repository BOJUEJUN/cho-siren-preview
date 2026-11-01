using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ChoSiren.Panels;
using ChoSiren.Systems;
using ChoSiren.Systems.Data;
using ChoSiren.Systems.Tactics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;

namespace ChoSiren.Tests
{
    public sealed class BattleUnitFeedbackPlayModeTests
    {
        private bool hadSave, hadLegacy;
        private bool hadReduced;
        private int reduced;
        private string saved, legacy;

        [SetUp]
        public void PreserveSave()
        {
            hadSave = PlayerPrefs.HasKey(GameModel.SaveKey);
            hadLegacy = PlayerPrefs.HasKey(GameModel.LegacySaveKey);
            saved = PlayerPrefs.GetString(GameModel.SaveKey);
            legacy = PlayerPrefs.GetString(GameModel.LegacySaveKey);
            hadReduced = PlayerPrefs.HasKey(BattlePresentationOptions.ReduceMotionKey);
            reduced = PlayerPrefs.GetInt(BattlePresentationOptions.ReduceMotionKey);
            PlayerPrefs.SetInt(BattlePresentationOptions.ReduceMotionKey, 0);
        }

        [TearDown]
        public void RestoreSave()
        {
            if (hadSave) PlayerPrefs.SetString(GameModel.SaveKey, saved); else PlayerPrefs.DeleteKey(GameModel.SaveKey);
            if (hadLegacy) PlayerPrefs.SetString(GameModel.LegacySaveKey, legacy); else PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
            if (hadReduced) PlayerPrefs.SetInt(BattlePresentationOptions.ReduceMotionKey, reduced);
            else PlayerPrefs.DeleteKey(BattlePresentationOptions.ReduceMotionKey);
        }

        [Test]
        public void GuardSupportActionUsesShieldGlyphAndTintInsteadOfFallbackSlash()
        {
            Assert.That(CombatStatusIconVisuals.ForSkill("rt-enemy-guard-shield"), Is.EqualTo(SkillIconKind.Shield));
            Assert.That(CombatFeedbackPalette.Skill("rt-enemy-guard-shield", BattleSide.Enemy),
                Is.EqualTo(CombatFeedbackPalette.Shield));
        }

        [Test]
        public void SkillEffectsUseActualRecipientsAndReplaceCrossScreenTrails()
        {
            using (var fixture = new Fixture(false))
            {
                var players = fixture.Battle.Units.Where(u => u.Side == BattleSide.Player).ToArray();
                var caster = players[0]; var healTarget = players[1]; var shieldTarget = players[2];
                var enemy = fixture.Battle.Units.First(u => u.Side == BattleSide.Enemy && u.Alive);
                var hp = fixture.Battle.Units.ToDictionary(u => u.Id, u => u.Hp);
                var shields = fixture.Battle.Units.ToDictionary(u => u.Id, u => u.Shield);
                Present(fixture.Panel, new BattleEvent { Kind = BattleEventKind.Damage, ActorId = caster.Id,
                    TargetId = enemy.Id, SkillId = fixture.Battle.ActiveSkillId(caster, true), Amount = 20 });
                var fx = fixture.Panel.GetComponent<SkillEffectPresentation>();
                Assert.That(fx.ActiveCount, Is.EqualTo(1));
                var trajectory = fixture.Panel.GetComponent<AttackTrajectoryPresentation>();
                Assert.That(trajectory.ActiveCount, Is.EqualTo(1), "A real damage recipient retains its causal attack cue.");
                AssertBigNumber(fixture.Panel, enemy, 20); // 2026-11-01: our hits on enemies are big glyph numbers

                Present(fixture.Panel, new BattleEvent { Kind = BattleEventKind.Heal, ActorId = caster.Id,
                    TargetId = healTarget.Id, SkillId = "rt-mermaid-small", Amount = 10 });
                Text healing = PopupFor(fixture.Panel, healTarget, "+10");
                AssertPopupIcon(healing, SkillIconKind.Heal);
                Assert.That(fixture.Panel.GetComponentsInChildren<SkillEffectGraphic>(true)
                    .Any(g => g.gameObject.activeInHierarchy && g.Kind == SkillVisualKind.Heal), Is.False,
                    "The PSD uses a compact heal glyph and value instead of bringing back the large healing ring.");

                Present(fixture.Panel, new BattleEvent { Kind = BattleEventKind.Shield, ActorId = caster.Id,
                    TargetId = shieldTarget.Id, SkillId = "rt-mermaid-big", Amount = 10 });
                Text shielding = PopupFor(fixture.Panel, shieldTarget, "+10");
                AssertPopupIcon(shielding, SkillIconKind.Shield);
                Assert.That(healing.gameObject.activeInHierarchy, Is.True, "Different recipients keep independent feedback.");
                var shieldFx = fixture.Panel.GetComponentsInChildren<SkillEffectGraphic>(true)
                    .Single(g => g.gameObject.activeInHierarchy && g.Kind == SkillVisualKind.Shield);
                var targetVisual = (RectTransform)typeof(TacticsBattlePanel).GetMethod("UnitVisual",
                    BindingFlags.Instance | BindingFlags.NonPublic).Invoke(fixture.Panel, new object[] { shieldTarget });
                Assert.That(Vector3.Distance(shieldFx.rectTransform.TransformPoint(shieldFx.rectTransform.rect.center),
                    targetVisual.TransformPoint(targetVisual.rect.center)), Is.LessThan(.5f),
                    "Shield geometry must be on the protected recipient, not on the caster or a random card.");
                Assert.That(shieldFx.raycastTarget, Is.False);
                Assert.That(fixture.Panel.GetComponentsInChildren<Transform>().Any(t => t.name.StartsWith("ActionTrail-")), Is.False);
                fx.AdvancePresentation(2f); trajectory.AdvancePresentation(2f);
                Assert.That(fx.ActiveCount, Is.Zero, "Local effect geometry must retire after its own presentation lifetime.");
                Assert.That(trajectory.ActiveCount, Is.Zero);
                foreach (BattleUnit unit in fixture.Battle.Units)
                {
                    Assert.That(unit.Hp, Is.EqualTo(hp[unit.Id]), "Presentation events cannot reapply HP changes.");
                    Assert.That(unit.Shield, Is.EqualTo(shields[unit.Id]), "Presentation events cannot grant a second model shield.");
                }
            }
        }

        [UnityTest]
        public IEnumerator BasicAttackAndHealingUseVisualIconsWhilePopupsKeepOnlyReadableValues()
        {
            using (var fixture = new Fixture(false))
            {
                BattleUnit caster = fixture.Battle.Units.First(unit => unit.Side == BattleSide.Player);
                BattleUnit enemy = fixture.Battle.Units.First(unit => unit.Side == BattleSide.Enemy && unit.Alive);
                var before = fixture.Battle.Units.ToDictionary(unit => unit.Id, unit => unit.Hp);
                Present(fixture.Panel, new BattleEvent
                {
                    Kind = BattleEventKind.Damage, ActorId = caster.Id, TargetId = enemy.Id,
                    SkillId = "rt-basic", Amount = 18, HpLostAmount = 18,
                });
                Present(fixture.Panel, new BattleEvent
                {
                    Kind = BattleEventKind.Heal, ActorId = caster.Id, TargetId = caster.Id,
                    SkillId = "rt-mermaid-small", Amount = 11,
                });
                var impactIcons = fixture.Panel.GetComponentsInChildren<SkillEffectGraphic>(true)
                    .Where(graphic => graphic.gameObject.activeInHierarchy).ToArray();
                Assert.That(impactIcons.Any(icon => icon.Kind == SkillVisualKind.Impact), Is.True,
                    "An ordinary attack needs visible impact feedback in addition to its value.");
                foreach (SkillEffectGraphic icon in impactIcons)
                    Assert.That(icon.raycastTarget, Is.False, "Combat effects must not block unit/command clicks.");
                AssertBigNumber(fixture.Panel, enemy, 18); // 2026-11-01: our hits on enemies are big glyph numbers
                Text healing = PopupFor(fixture.Panel, caster, "+11");
                AssertPopupIcon(healing, SkillIconKind.Heal);
                Assert.That(healing.fontSize, Is.GreaterThanOrEqualTo(18));
                Assert.That(healing.color.g, Is.GreaterThan(healing.color.r), "Healing has a distinct green value.");
                Assert.That(new[] { healing }.Any(text => text.text.Contains("攻击") || text.text.Contains("治疗")), Is.False,
                    "Compact glyphs carry meaning; popup text keeps the authoritative signed values.");

                float deadline = Time.realtimeSinceStartup + 20f;
                var bigNumbers = fixture.Panel.GetComponentInChildren<BigDamageNumbers1101>(true);
                while ((bigNumbers.ActiveCount + bigNumbers.PendingCount > 0 || healing.gameObject.activeSelf) && Time.realtimeSinceStartup < deadline)
                    yield return null;
                Assert.That(bigNumbers.ActiveCount + bigNumbers.PendingCount, Is.Zero, "Damage feedback must retire naturally.");
                Assert.That(healing.gameObject.activeSelf, Is.False, "Healing feedback must retire naturally.");
                Assert.That(healing.GetComponentInChildren<SkillIconGraphic>(true).gameObject.activeSelf, Is.False,
                    "The semantic icon cannot linger after its number disappears.");
                foreach (BattleUnit unit in fixture.Battle.Units)
                    Assert.That(unit.Hp, Is.EqualTo(before[unit.Id]), "A visual feedback lifetime cannot mutate combat HP.");
            }
        }

        [Test]
        public void PerformerNamesAndDiceSummaryHaveRoomToRenderAboveTheirBars()
        {
            using (var fixture = new Fixture(false))
            {
                foreach (BattleUnit actor in fixture.Battle.Units.Where(unit => unit.Side == BattleSide.Player))
                {
                    Text name = Find(fixture.Panel.transform, CellName(actor)).Find("UnitName").GetComponent<Text>();
                    Assert.That(name.text, Is.EqualTo(actor.Definition.Name));
                    Assert.That(name.rectTransform.rect.height, Is.GreaterThanOrEqualTo(name.fontSize * 1.6f));
                }
                Text hand = Find(fixture.Panel.transform, "DiceHandSummary").GetComponent<Text>();
                Assert.That(hand.text, Does.Not.Contain("\n"), "充能条上方只放单行骰型，剩余次数已在按钮显示。");
                var fill = Find(fixture.Panel.transform, "DiceEnergy");
                var fillRect = fill as RectTransform;
                Assert.That(hand.rectTransform.parent, Is.EqualTo(fillRect.parent));
                Assert.That(-hand.rectTransform.anchoredPosition.y + hand.rectTransform.rect.height,
                    Is.LessThan(-fillRect.anchoredPosition.y), "新版 PSD 的骰型文案必须在充能条上方");
            }
        }

        [Test]
        public void EveryPerformerAnimatesItsOwnBasicAndBothSkillsWithoutChangingHp()
        {
            using (var fixture = new Fixture(false))
            {
                var originalHp = fixture.Battle.Units.ToDictionary(unit => unit.Id, unit => unit.Hp);
                BattleUnit enemy = fixture.Battle.Units.First(unit => unit.Side == BattleSide.Enemy && unit.Alive);
                var ultimate = fixture.Panel.GetComponent<BattleAlbumUltimate929>();
                Assert.That(ultimate, Is.Not.Null, "当前PSD战斗使用专属的大招头像演出。");
                int timestamp = 1000;
                foreach (BattleUnit actor in fixture.Battle.Units.Where(unit => unit.Side == BattleSide.Player))
                {
                    Transform cell = Find(fixture.Panel.transform, CellName(actor));
                    var motion = Find(cell, "PerformerMotion").GetComponent<EnemyUnitPresentation>();
                    Assert.That(motion, Is.Not.Null, actor.Definition.Name + " 必须有自己的施法动作组件。");
                    var album = cell.GetComponent<BattleMemberAlbum929>();
                    Assert.That(album, Is.Not.Null, "当前PSD的小技能提示应属于该成员自己的专辑卡。");
                    Image actorPortrait = motion.transform.Find("Portrait").GetComponent<Image>();
                    Assert.That(actorPortrait.sprite, Is.Not.Null, "验收必须使用该成员实际可见的画像。");
                    ultimate.Cancel();
                    var skills = new[] { "rt-basic", fixture.Battle.ActiveSkillId(actor, false),
                        fixture.Battle.ActiveSkillId(actor, true) };
                    foreach (string skill in skills)
                    {
                        bool mermaid = fixture.Battle.RaceOf(actor) == CombatRace.Mermaid;
                        Present(fixture.Panel, new BattleEvent
                        {
                            Kind = BattleEventKind.ActionStarted, ActorId = actor.Id,
                            TargetId = mermaid && skill != "rt-basic" ? actor.Id : enemy.Id,
                            SkillId = skill, TimeMilliseconds = timestamp++,
                        });
                        Text action = cell.Find("PerformerAction").GetComponent<Text>();
                        if (skill == "rt-basic")
                        {
                            Assert.That(action.gameObject.activeSelf, Is.False,
                                "普攻已有角色动作与轨迹，不应再常规喊话。");
                            // 2026-10-31 (赵金): the bust inside her own card acts out; the old head nudge stays off.
                            Assert.That(cell.GetComponentInChildren<HeroCardLunge1031>(true)?.PlayCount ?? 0, Is.EqualTo(1),
                                "普攻必须驱动该成员自己卡内的立绘动作。");
                            Assert.That(motion.ActionCount, Is.Zero, "旧的头像微动不能与卡内立绘表演叠加。");
                        }
                        else
                        {
                            Assert.That(action.text, Does.EndWith(fixture.Battle.LookupSkill(skill).Name),
                                "减少重复文字不能删去该成员自己的技能提示。");
                            Assert.That(motion.ActionCount, Is.Zero, "PSD技能提示不应再次猛推整张半身卡。");
                            Assert.That(cell.GetComponentInChildren<HeroCardLunge1031>(true).CurrentTier,
                                Is.EqualTo(skill == skills[1] ? HeroCardLunge1031.Tier.Skill : HeroCardLunge1031.Tier.Ultimate),
                                "技能与大招的卡内立绘表演逐级加强。");
                            if (skill == skills[1])
                            {
                                Assert.That(album.PassiveVisible, Is.False,
                                    "The caster's skill name and semantic icon must not add a duplicate portrait.");
                                Assert.That(action.gameObject.activeSelf, Is.True);
                            }
                            else
                            {
                                Assert.That(ultimate.IsShowing, Is.True);
                                Assert.That(ultimate.PendingCount, Is.Zero, "每次施法只应生成一个大招演出。");
                                Assert.That(Find(fixture.Panel.transform, "UltimatePortrait").GetComponent<Image>().sprite,
                                    Is.SameAs(actorPortrait.sprite), "大招演出必须使用该成员自己的画像。");
                            }
                        }
                    }
                    Text label = cell.Find("PerformerAction").GetComponent<Text>();
                    Assert.That(label.gameObject.activeInHierarchy, Is.True);
                    // 大招带「大招 · 」前缀（0.3.5 上线形态），回归点是必须指向该成员自己的大招名。
                    Assert.That(label.text, Does.EndWith(fixture.Battle.LookupSkill(skills[2]).Name),
                        actor.Definition.Name + " 的大招标签没有指向自己的大招");
                    Assert.That(fixture.Panel.GetComponentsInChildren<Text>(true).Any(text =>
                        text.name == "Popup" && text.gameObject.activeSelf && text.text.StartsWith("大招 · ")),
                        Is.False, "专辑演出与卡片已显示大招名，不应再复制为头像浮字。");
                }
                foreach (BattleUnit unit in fixture.Battle.Units)
                    Assert.That(unit.Hp, Is.EqualTo(originalHp[unit.Id]),
                        "ActionStarted 是表现事件，不能重复扣血或为了播放治疗而修改满血角色。");
            }
        }

        [Test]
        public void HurtKeepsItsRealNumberAndMotionWithoutASecondHurtWord()
        {
            using (var fixture = new Fixture(false))
            {
                BattleUnit target = fixture.Battle.Units.First(unit => unit.Side == BattleSide.Player);
                BattleUnit enemy = fixture.Battle.Units.First(unit => unit.Side == BattleSide.Enemy && unit.Alive);
                Transform cell = Find(fixture.Panel.transform, CellName(target));
                var motion = Find(cell, "PerformerMotion").GetComponent<EnemyUnitPresentation>();
                int before = target.Hp;
                Present(fixture.Panel, new BattleEvent
                {
                    Kind = BattleEventKind.Damage, ActorId = enemy.Id, TargetId = target.Id,
                    SkillId = "rt-basic", Amount = 18, HpLostAmount = 18,
                });
                Assert.That(cell.Find("PerformerHurt").gameObject.activeSelf, Is.False);
                Assert.That(motion.HitReactionCount, Is.EqualTo(1));
                Assert.That(fixture.Panel.GetComponentsInChildren<Text>(true).Any(text =>
                    text.name == "Popup" && text.gameObject.activeSelf && text.text == "-18"), Is.True);
                Assert.That(target.Hp, Is.EqualTo(before), "表现事件不能再次结算伤害。");
            }
        }

        [Test]
        public void RepeatedAoeActionEventDoesNotRestartCasterForEachTarget()
        {
            using (var fixture = new Fixture(false))
            {
                BattleUnit actor = fixture.Battle.Units.First(unit => unit.Side == BattleSide.Player);
                var motion = Find(Find(fixture.Panel.transform, CellName(actor)), "PerformerMotion")
                    .GetComponent<EnemyUnitPresentation>();
                var ultimate = fixture.Panel.GetComponent<BattleAlbumUltimate929>();
                Assert.That(ultimate, Is.Not.Null);
                ultimate.Cancel();
                foreach (BattleUnit enemy in fixture.Battle.Units.Where(unit => unit.Side == BattleSide.Enemy && unit.Alive))
                    Present(fixture.Panel, new BattleEvent
                    {
                        Kind = BattleEventKind.ActionStarted, ActorId = actor.Id, TargetId = enemy.Id,
                        SkillId = fixture.Battle.ActiveSkillId(actor, true), TimeMilliseconds = 4000,
                    });
                Assert.That(ultimate.IsShowing, Is.True);
                Assert.That(ultimate.PendingCount, Is.Zero, "同一次群攻不能按命中目标数重复排队大招演出。");
                Assert.That(motion.ActionCount, Is.Zero, "PSD大招用独立头像演出，不应同时猛推整张卡。");
                Present(fixture.Panel, new BattleEvent
                {
                    Kind = BattleEventKind.ActionStarted, ActorId = actor.Id,
                    SkillId = "rt-basic", TimeMilliseconds = 4000,
                });
                // 2026-10-31: the PSD card's bust lunge (HeroCardLunge1031) replaces the old head nudge; the
                // basic is still presented (not de-duplicated away) but merges into the stronger ultimate swell.
                var lunge = Find(fixture.Panel.transform, CellName(actor)).GetComponentInChildren<HeroCardLunge1031>(true);
                Assert.That(lunge, Is.Not.Null);
                Assert.That(lunge.CurrentTier, Is.EqualTo(HeroCardLunge1031.Tier.Ultimate), "同一tick的普攻不能削弱正在进行的大招表演。");
                Assert.That(motion.ActionCount, Is.Zero, "旧的头像微动不能与卡内立绘表演叠加。");
                Assert.That(ultimate.IsShowing, Is.True);
                Assert.That(ultimate.PendingCount, Is.Zero, "普攻不能重新复制已播放的大招。");
            }
        }

        [Test]
        public void EnemyHealthCardsAndArtShareTheCorrectUnitAnchorAndReserveRemainsHidden()
        {
            using (var fixture = new Fixture(false))
            {
                var psd = Find(fixture.Panel.transform, "BattlePSD928") as RectTransform;
                var active = fixture.Battle.Units.Where(unit => unit.Side == BattleSide.Enemy && unit.Alive).ToArray();
                Assert.That(active.Length, Is.EqualTo(2));
                foreach (BattleUnit enemy in fixture.Battle.Units.Where(unit => unit.Side == BattleSide.Enemy))
                {
                    Transform anchor = Find(fixture.Panel.transform, "EnemyAnchor-" + enemy.Id);
                    Transform cell = Find(fixture.Panel.transform, CellName(enemy));
                    Transform motion = Find(fixture.Panel.transform, "EnemyMotion-" + enemy.Id);
                    Transform figure = Find(fixture.Panel.transform, "EnemyFigure-" + enemy.Id);
                    Assert.That(motion.parent, Is.SameAs(anchor));
                    Assert.That(figure.parent, Is.SameAs(motion));
                    Assert.That(cell.IsChildOf(motion), Is.False, "Hit motion must not shake the name/HP readout.");
                    Assert.That(anchor.gameObject.activeInHierarchy, Is.EqualTo(enemy.Spawned));
                    Assert.That(cell.gameObject.activeInHierarchy, Is.EqualTo(enemy.Spawned));
                    if (!enemy.Spawned) continue;
                    Assert.That(cell.parent, Is.SameAs(psd), "The current PSD puts two enemy nameplates in the stage corners.");
                    Assert.That(cell.Find("UnitName").GetComponent<Text>().text, Is.EqualTo(enemy.Definition.Name));
                    Assert.That(cell.Find("UnitHpText").GetComponent<Text>().text, Is.EqualTo($"{enemy.Hp}/{enemy.MaxHp}"));
                    Assert.That(cell.position.y, Is.GreaterThan(motion.position.y), "Corner HP readouts stay above the figures.");
                    // The PSD ornamental frame intentionally bleeds past the edge; live labels cannot.
                    AssertContained(psd, cell.Find("UnitName").GetComponent<RectTransform>(), enemy.Definition.Name + " name");
                    AssertContained(psd, cell.Find("UnitHpText").GetComponent<RectTransform>(), enemy.Definition.Name + " HP");
                }
                Transform left = Find(fixture.Panel.transform, CellName(active.Single(u => u.Definition.Id == "noise-wraith")));
                Transform right = Find(fixture.Panel.transform, CellName(active.Single(u => u.Definition.Id == "echo-drone")));
                Assert.That(psd.InverseTransformPoint(left.position).x, Is.LessThan(psd.rect.center.x));
                Assert.That(psd.InverseTransformPoint(right.position).x, Is.GreaterThan(psd.rect.center.x));

                active[0].Hp -= 37;
                Invoke(fixture.Panel, "RefreshAllCells");
                foreach (BattleUnit enemy in active)
                    Assert.That(Find(fixture.Panel.transform, CellName(enemy)).Find("UnitHpText").GetComponent<Text>().text,
                        Is.EqualTo($"{enemy.Hp}/{enemy.MaxHp}"), "Updating one enemy cannot copy its HP to the other corner.");

                BattleUnit reserve = fixture.Battle.Units.First(unit => unit.Side == BattleSide.Enemy && !unit.Spawned);
                reserve.Spawned = true;
                Invoke(fixture.Panel, "RefreshAllCells");
                foreach (BattleUnit enemy in fixture.Battle.Units.Where(unit => unit.Side == BattleSide.Enemy && unit.Alive))
                {
                    Transform cell = Find(fixture.Panel.transform, CellName(enemy));
                    Assert.That(cell.gameObject.activeInHierarchy, Is.True);
                    Assert.That(cell.parent, Is.SameAs(Find(fixture.Panel.transform, "EnemyAnchor-" + enemy.Id)),
                        "When three enemies share the stage, every live HP card falls back to its own unit anchor.");
                    Assert.That(cell.Find("UnitName").GetComponent<Text>().text, Is.EqualTo(enemy.Definition.Name));
                    Assert.That(cell.Find("UnitHpText").GetComponent<Text>().text, Is.EqualTo($"{enemy.Hp}/{enemy.MaxHp}"));
                }
            }
        }

        [Test]
        public void EnemyDefeatImmediatelyRemovesHpCardThenHidesFinishedFadeAnchor()
        {
            using (var fixture = new Fixture(false))
            {
                BattleUnit enemy = fixture.Battle.Units.First(unit => unit.Side == BattleSide.Enemy && unit.Alive);
                BattleUnit player = fixture.Battle.Units.First(unit => unit.Side == BattleSide.Player);
                Transform anchor = Find(fixture.Panel.transform, "EnemyAnchor-" + enemy.Id);
                Transform cell = Find(fixture.Panel.transform, CellName(enemy));
                var motion = Find(fixture.Panel.transform, "EnemyMotion-" + enemy.Id)
                    .GetComponent<EnemyUnitPresentation>();
                enemy.Hp = 0;
                Present(fixture.Panel, new BattleEvent
                {
                    Kind = BattleEventKind.Defeated, ActorId = player.Id, TargetId = enemy.Id,
                    SkillId = "rt-basic", TimeMilliseconds = 1000,
                });
                Assert.That(cell.gameObject.activeInHierarchy, Is.False, "死亡血条必须立即消失。");
                Assert.That(anchor.gameObject.activeInHierarchy, Is.True, "画像保留短暂退场，不应直接消失。");
                Assert.That(motion.IsDefeating, Is.True);
                Assert.That(fixture.Battle.FocusEnemy(enemy.Id), Is.False, "死敌不能继续被集火选择。");
                motion.AdvancePresentation(0.5f);
                Invoke(fixture.Panel, "RefreshAllCells");
                Assert.That(anchor.gameObject.activeInHierarchy, Is.False, "退场完成不能残留空画像/旧血条。");
            }
        }

        [Test]
        public void DamagingBossAddAnimatesThatAddNotTheBoss()
        {
            using (var fixture = new Fixture(true))
            {
                BattleUnit add = fixture.Battle.Units.First(unit => unit.Side == BattleSide.Enemy && unit.Definition.Id == "echo-drone");
                BattleUnit queen = fixture.Battle.Units.First(unit => unit.Definition.Id == "siren-queen");
                BattleUnit player = fixture.Battle.Units.First(unit => unit.Side == BattleSide.Player);
                var boss = fixture.Panel.GetComponentInChildren<BossBattlePresentation>(true);
                var addMotion = Find(fixture.Panel.transform, "EnemyMotion-" + add.Id)
                    .GetComponent<EnemyUnitPresentation>();
                Assert.That(boss, Is.Not.Null);
                int before = boss.HitReactionCount;
                Present(fixture.Panel, new BattleEvent
                {
                    Kind = BattleEventKind.Damage, ActorId = player.Id, TargetId = add.Id,
                    SkillId = "rt-basic", Amount = 5,
                });
                Assert.That(addMotion.HitReactionCount, Is.EqualTo(1));
                Assert.That(boss.HitReactionCount, Is.EqualTo(before), "打小怪不能在Boss身上摇晃并重复飘字。");
                Present(fixture.Panel, new BattleEvent
                {
                    Kind = BattleEventKind.Damage, ActorId = player.Id, TargetId = queen.Id,
                    SkillId = "rt-basic", Amount = 6,
                });
                Assert.That(boss.HitReactionCount, Is.EqualTo(before + 1));
            }
        }

        [Test]
        public void BossRemainsStageDominantWithoutBreakingFocusOrPauseControls()
        {
            using (var fixture = new Fixture(true))
            {
                BattleUnit queen = fixture.Battle.Units.First(unit => unit.Definition.Id == "siren-queen");
                BattleUnit add = fixture.Battle.Units.First(unit => unit.Side == BattleSide.Enemy &&
                    unit.Definition.Id == "echo-drone");
                RectTransform stage = Find(fixture.Panel.transform, "EnemyStage") as RectTransform;
                RectTransform bossRig = Find(fixture.Panel.transform, "BossMotionRig") as RectTransform;
                RectTransform addRig = Find(fixture.Panel.transform, "EnemyMotion-" + add.Id) as RectTransform;
                Assert.That(stage, Is.Not.Null);
                Assert.That(bossRig, Is.Not.Null);
                Assert.That(addRig, Is.Not.Null);
                Assert.That(bossRig.rect.width, Is.GreaterThanOrEqualTo(addRig.rect.width * 2f),
                    "Boss 主视觉宽度至少应为伴生敌人的两倍，保持首领层级。");
                Assert.That(bossRig.rect.height, Is.GreaterThanOrEqualTo(addRig.rect.height * 2f),
                    "Boss 主视觉高度至少应为伴生敌人的两倍，不能退化成普通单位卡。");
                // 2026-10-31 (赵金, refs/07): the boss now runs from under the top HUD down to the member cards,
                // past the legacy EnemyStage rect, so it is bounded by the PSD stage instead and must be large.
                AssertContained(Find(fixture.Panel.transform, "BattlePSD928") as RectTransform, bossRig, "Boss 主视觉");
                Assert.That(bossRig.rect.height, Is.GreaterThanOrEqualTo(600f), "Boss 应从顶部 HUD 下方延伸到卡片上方。");

                Button focus = Find(fixture.Panel.transform, "BossPortrait").GetComponent<Button>();
                Assert.That(focus, Is.Not.Null);
                Assert.That(focus.IsInteractable(), Is.True);
                Assert.That(focus.targetGraphic?.raycastTarget, Is.True);
                focus.onClick.Invoke();
                Assert.That(fixture.Battle.FocusTargetId, Is.EqualTo(queen.Id),
                    "放大 Boss 后，其画像仍必须能被点击集火。");

                Button pause = Find(fixture.Panel.transform, "PauseToggle").GetComponent<Button>();
                Assert.That(pause.IsInteractable(), Is.True);
                pause.onClick.Invoke();
                Assert.That(fixture.Panel.IsPaused, Is.True, "视觉尺寸调整不能切断暂停操作。");
                pause.onClick.Invoke();
                Assert.That(fixture.Panel.IsPaused, Is.False, "暂停后必须能继续战斗。");
            }
        }

        [Test]
        public void InspectingAnotherPerformerDoesNotChangeBattleCommander()
        {
            using (var fixture = new Fixture(false))
            {
                int captain = fixture.Battle.CurrentLeaderId;
                BattleUnit selected = fixture.Battle.Units.First(unit => unit.Side == BattleSide.Player && unit.Id != captain);
                Find(fixture.Panel.transform, CellName(selected)).GetComponent<Button>().onClick.Invoke();
                var inspected = (BattleUnit)typeof(TacticsBattlePanel).GetField("inputActor",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(fixture.Panel);
                Assert.That(inspected.Id, Is.EqualTo(selected.Id), "点击队员应允许查看其双技能。");
                Assert.That(fixture.Battle.CurrentLeaderId, Is.EqualTo(captain), "查看角色不能悄悄替换队长和种族指挥效果。");
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void HealthSummariesUseCurrentWaveOrBossAndKeepRemovedTeamFooterInactive(bool bossEncounter)
        {
            using (var fixture = new Fixture(bossEncounter))
            {
                BattleUnit player = fixture.Battle.Units.First(unit => unit.Side == BattleSide.Player);
                player.Hp /= 2;
                BattleUnit boss = fixture.Battle.Units.FirstOrDefault(unit => unit.Definition.Id == "siren-queen");
                if (boss != null) boss.Hp /= 4;
                Invoke(fixture.Panel, "RefreshBattleHud");
                Text enemyText = (Text)typeof(TacticsBattlePanel).GetField("enemyHpText",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(fixture.Panel);
                var displayedEnemies = fixture.Battle.Units.Where(unit => unit.Side == BattleSide.Enemy &&
                    (boss != null ? unit.Id == boss.Id : unit.Spawned && unit.Wave == fixture.Battle.CurrentWave - 1));
                long enemyHp = displayedEnemies.Sum(unit => (long)unit.Hp);
                long enemyMax = displayedEnemies.Sum(unit => (long)unit.MaxHp);
                Assert.That(enemyText.text, Does.Contain($"{enemyHp:N0}/{enemyMax:N0}"),
                    "总条不能把未出场增援算进当前波，也不能用小怪生命冒充Boss生命。");
                var players = fixture.Battle.Units.Where(unit => unit.Side == BattleSide.Player);
                long teamHp = players.Sum(unit => (long)unit.Hp);
                long teamMax = players.Sum(unit => (long)unit.MaxHp);
                Text summary = Find(fixture.Panel.transform, "TeamHealthSummary").GetComponent<Text>();
                Assert.That(summary.gameObject.activeInHierarchy, Is.False,
                    "实时战斗已按设计移除队伍总生命页尾，不能仅移动到画布外。");
                Assert.That(summary.text, Does.Contain($"{teamHp:N0}/{teamMax:N0}"));
                Assert.That(summary.text, Does.Contain("存活 4/4"));
            }
        }

        [TestCase("training")]
        [TestCase("accessory")]
        [TestCase("team")]
        [TestCase("farm")]
        public void DefeatRecoveryRoutesCloseBattleWithoutSettlingOrChargingAgain(string route)
        {
            using (var fixture = new RecoveryFixture(false))
            {
                Transform actions = Find(fixture.Panel.transform, "RecoveryActions");
                Assert.That(actions.gameObject.activeInHierarchy, Is.True);
                Button button = Find(actions, "Recovery-" + route).GetComponent<Button>();
                Assert.That(button.interactable, Is.True);
                Assert.That(fixture.SettlementCalls, Is.EqualTo(1));
                int gold = fixture.Model.Save.Gold, diamonds = fixture.Model.Save.Diamonds;
                int stamina = fixture.Model.Save.Stamina;
                button.onClick.Invoke();
                // A second queued click must also be idempotent after the panel closes.
                button.onClick.Invoke();
                Assert.That(fixture.GrowthRoute, Is.EqualTo(route));
                Assert.That(fixture.GrowthCalls, Is.EqualTo(1));
                Assert.That(fixture.Panel.gameObject.activeSelf, Is.False);
                Assert.That(fixture.SettlementCalls, Is.EqualTo(1));
                Assert.That(fixture.Model.Save.Gold, Is.EqualTo(gold));
                Assert.That(fixture.Model.Save.Diamonds, Is.EqualTo(diamonds));
                Assert.That(fixture.Model.Save.Stamina, Is.EqualTo(stamina),
                    "导航去训练/装备/编队/刷关只是打开界面，不能再次收费或自动开始关卡。");
                Assert.That(fixture.BackCalls, Is.Zero, "恢复导航不能同时触发回地图并抢走目标页面。");
            }
        }

        [Test]
        public void VictoryHidesRecoveryActionsAndContinueStillClosesOnce()
        {
            using (var fixture = new RecoveryFixture(true))
            {
                Assert.That(fixture.Battle.Outcome, Is.EqualTo(BattleOutcome.Victory));
                Assert.That(Find(fixture.Panel.transform, "RecoveryActions").gameObject.activeInHierarchy, Is.False);
                Button button = Find(fixture.Panel.transform, "ResultContinue").GetComponent<Button>();
                Assert.That(button.gameObject.activeInHierarchy && button.interactable, Is.True);
                int gold = fixture.Model.Save.Gold, diamonds = fixture.Model.Save.Diamonds;
                button.onClick.Invoke();
                Assert.That(fixture.Panel.gameObject.activeSelf, Is.False);
                Assert.That(fixture.SettlementCalls, Is.EqualTo(1));
                Assert.That(fixture.BackCalls, Is.EqualTo(1));
                Assert.That(fixture.GrowthCalls, Is.Zero);
                Assert.That(fixture.Model.Save.Gold, Is.EqualTo(gold));
                Assert.That(fixture.Model.Save.Diamonds, Is.EqualTo(diamonds), "继续不能重复发放胜利奖励。");
            }
        }

        [Test]
        public void DefeatContinueRemainsAvailableWithoutForcingGrowthNavigation()
        {
            using (var fixture = new RecoveryFixture(false))
            {
                Button button = Find(fixture.Panel.transform, "ResultContinue").GetComponent<Button>();
                Assert.That(button.gameObject.activeInHierarchy && button.interactable, Is.True);
                button.onClick.Invoke();
                Assert.That(fixture.Panel.gameObject.activeSelf, Is.False);
                Assert.That(fixture.BackCalls, Is.EqualTo(1));
                Assert.That(fixture.GrowthCalls, Is.Zero);
                Assert.That(fixture.SettlementCalls, Is.EqualTo(1));
            }
        }

        private sealed class RecoveryFixture : IDisposable
        {
            private readonly GameObject root;
            public readonly GameModel Model;
            public readonly BattleSimulator Battle;
            public readonly TacticsBattlePanel Panel;
            public int SettlementCalls, GrowthCalls, BackCalls;
            public string GrowthRoute;

            public RecoveryFixture(bool victory)
            {
                // The enclosing fixture restores both save keys after each test.
                PlayerPrefs.DeleteKey(GameModel.SaveKey);
                PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
                Model = new GameModel();
                Battle = Model.StartStageBattle("stage-1-1", 713UL, out string message);
                Assert.That(Battle, Is.Not.Null, message);
                root = new GameObject("Battle recovery route fixture", typeof(RectTransform));
                Panel = TacticsBattlePanel.Open(root.transform, Model, Battle,
                    finished: battle => { SettlementCalls++; Model.SettleStageBattle(battle, out _); },
                    back: () => BackCalls++, growth: route => { GrowthCalls++; GrowthRoute = route; });
                Panel.StopAllCoroutines();
                if (victory)
                {
                    foreach (BattleUnit unit in Battle.Units)
                        if (unit.Side == BattleSide.Enemy) unit.Hp = 1;
                        else unit.BaseAttack = 10000;
                    Battle.AdvanceRealtime(Battle.TimeLimitMilliseconds, true);
                    Assert.That(Battle.Outcome, Is.EqualTo(BattleOutcome.Victory));
                }
                else Battle.AutoPlay(0);
                Invoke(Panel, "ShowResult");
            }

            public void Dispose() { UnityEngine.Object.DestroyImmediate(root); }
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject root;
            public readonly BattleSimulator Battle;
            public readonly TacticsBattlePanel Panel;

            public Fixture(bool boss)
            {
                root = new GameObject("Battle unit feedback fixture", typeof(RectTransform));
                var repository = new GameDataRepository(new ResourcesGameDataSource(), new UnityJsonReader());
                Assert.That(repository.LoadAll(), Is.True, string.Join("\n", repository.Errors));
                var stage = new StageDefinition
                {
                    Id = "unit-feedback-test", Name = "表现回归", EncounterType = boss ? "boss" : "normal",
                    Enemies = new List<EnemySpawn>
                    {
                        new EnemySpawn { UnitId = boss ? "siren-queen" : "noise-wraith", Row = 0, Col = 0 },
                        new EnemySpawn { UnitId = "echo-drone", Row = 1, Col = 0 },
                        new EnemySpawn { UnitId = "static-golem", Row = 0, Col = 1, Wave = 1 },
                    },
                };
                Battle = new BattleSimulator(repository.Tactics, stage, new List<PlayerUnitSetup>
                {
                    new PlayerUnitSetup { UnitId = "xingli", Row = 0, Col = 0, Level = 1 },
                    new PlayerUnitSetup { UnitId = "feiyin", Row = 1, Col = 0, Level = 1 },
                    new PlayerUnitSetup { UnitId = "wubai", Row = 2, Col = 0, Level = 1 },
                    new PlayerUnitSetup { UnitId = "yeying", Row = 0, Col = 1, Level = 1 },
                }, new SeededRandom(412));
                Panel = TacticsBattlePanel.Open(root.transform, new GameModel(), Battle, _ => { });
                Panel.StopAllCoroutines();
            }

            public void Dispose() { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static string CellName(BattleUnit unit) => $"Cell-{(unit.Side == BattleSide.Player ? "P" : "E")}-{unit.Row}-{unit.Col}";

        private static Transform Find(Transform root, string name)
        {
            Transform found = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(item => item.name == name);
            Assert.That(found, Is.Not.Null, "缺少验收节点：" + name);
            return found;
        }

        private static Text PopupFor(TacticsBattlePanel panel, BattleUnit target, string value)
        {
            var owners = (int[])typeof(TacticsBattlePanel).GetField("popupOwners",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel);
            var popups = (List<Text>)typeof(TacticsBattlePanel).GetField("popupPool",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel);
            var matches = Enumerable.Range(0, popups.Count).Where(i => owners[i] == target.Id &&
                popups[i].gameObject.activeInHierarchy && popups[i].text == value).ToArray();
            Assert.That(matches.Length, Is.EqualTo(1), "Exactly one signed value must belong to " + target.Definition.Name);
            return popups[matches[0]];
        }

        private static void AssertBigNumber(TacticsBattlePanel panel, BattleUnit target, int amount)
        {
            var big = panel.GetComponentInChildren<BigDamageNumbers1101>(true);
            Assert.That(big, Is.Not.Null);
            Assert.That(big.ActiveCountFor(target.Id) + big.PendingCount, Is.GreaterThanOrEqualTo(1), "our hit on " + target.Definition.Name + " shows a big number");
            if (big.TryNewest(out int shown, out _, out float width, out _, out _))
            {
                Assert.That(shown, Is.EqualTo(amount));
                Assert.That(width, Is.GreaterThanOrEqualTo(BigDamageNumbers1101.MinWidth - .5f), "about a third of the screen wide or more");
            }
        }

        private static void AssertPopupIcon(Text popup, SkillIconKind kind)
        {
            var icon = popup.GetComponentInChildren<SkillIconGraphic>(true);
            Assert.That(icon, Is.Not.Null);
            Assert.That(icon.gameObject.activeInHierarchy, Is.True);
            Assert.That(icon.Kind, Is.EqualTo(kind));
            Assert.That(icon.raycastTarget, Is.False);
            Assert.That(icon.rectTransform.rect.width, Is.InRange(18f, 28f), "Semantic glyphs remain legible and compact.");
            Assert.That(icon.rectTransform.rect.height, Is.InRange(18f, 28f));
        }

        private static void AssertContained(RectTransform outer, RectTransform inner, string label)
        {
            Vector3[] outerCorners = new Vector3[4];
            Vector3[] innerCorners = new Vector3[4];
            outer.GetWorldCorners(outerCorners);
            inner.GetWorldCorners(innerCorners);
            Assert.That(innerCorners[0].x, Is.GreaterThanOrEqualTo(outerCorners[0].x - .5f), label + " 左侧超出舞台。");
            Assert.That(innerCorners[2].x, Is.LessThanOrEqualTo(outerCorners[2].x + .5f), label + " 右侧超出舞台。");
            Assert.That(innerCorners[0].y, Is.GreaterThanOrEqualTo(outerCorners[0].y - .5f), label + " 底部超出舞台。");
            Assert.That(innerCorners[2].y, Is.LessThanOrEqualTo(outerCorners[2].y + .5f), label + " 顶部超出舞台。");
        }

        private static void Present(TacticsBattlePanel panel, BattleEvent entry) =>
            typeof(TacticsBattlePanel).GetMethod("PresentEvent", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(panel, new object[] { entry, -1 });

        private static void Invoke(TacticsBattlePanel panel, string method) =>
            typeof(TacticsBattlePanel).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(panel, null);
    }
}
