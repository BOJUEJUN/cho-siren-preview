using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ChoSiren.Systems.Dice;
using ChoSiren.Systems.Tactics;
using NUnit.Framework;
using UnityEngine;

namespace ChoSiren.Tests
{
    /// <summary>
    /// 2026-11-01 赵金数值批次 (Docs/BALANCE-1101.md, Builds/qa-logs/zj-1101/balance/report.md).
    /// 通过率 = the shipped turn battle, default formation (星璃 队长 / 绯音 / 雾白 / 夜莺), 自动 mode (auto ultimates + the
    /// panel's 自动 reroll policy), fixed seeds 1 + 7919·k. Gear = starting accessories + first-clear rewards of the
    /// chapter-one stages already passed, put on with 快捷穿戴 in team order.
    /// </summary>
    public sealed class Balance1101Tests
    {
        private static readonly DateTime Now = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
        private const int TargetSeeds = 200;
        private const int ReportSeeds = 100;
        private static readonly string[] ArtEnemies = { "neon-scout", "noise-wraith", "pulse-guard", "velvet-hexer", "echo-drone", "static-golem" };
        private static readonly string[] DeckEnemies = { "purify-dancer", "cheer-shield", "afterimage-diva" };

        /// <summary>Spam = 手动狂投 (2026-11-01c): before every own action spend every 骰子豆 on the best two dice.</summary>
        public enum Mode { Auto, NoReroll, NoDice, Idle, Spam }

        [SetUp, TearDown]
        public void Clean()
        {
            PlayerPrefs.DeleteKey(GameModel.SaveKey);
            PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
            GameModel.FirstStageNewcomerGuard = true; // the shipped default (2026-11-01b)
        }

        // ------------------------------------------------------------------ 通过率 targets

        [Test]
        public void StagesOneToFourAreAboutSixtyPercentAtLevelOneWithoutGear()
        {
            for (int number = 1; number <= 4; number++)
            {
                double win = WinRate($"stage-1-{number}", 1, -1, Mode.Auto, TargetSeeds);
                TestContext.WriteLine($"1-{number} L1 无装备 自动 {win:0.0}%");
                Assert.That(win, Is.InRange(50.0, 70.0), $"1-{number}: 1 级无装备的通过率应约 60%");
            }
        }

        [Test]
        public void MiniBossOneFiveIsLostAtLevelOneAndAboutSixtyFivePercentAtLevelTenWithGear()
        {
            double bare = WinRate("stage-1-5", 1, -1, Mode.Auto, TargetSeeds);
            double ready = WinRate("stage-1-5", 10, 4, Mode.Auto, TargetSeeds);
            TestContext.WriteLine($"1-5 L1 无装备 {bare:0.0}% · L10 + 1-1~1-4 装备 {ready:0.0}%");
            Assert.That(bare, Is.LessThanOrEqualTo(5.0), "1-5 小BOSS：不升级不穿装备基本必败");
            Assert.That(ready, Is.InRange(55.0, 75.0), "1-5 小BOSS：全员 10 级 + 合理装备约 65%");
        }

        [Test]
        public void EverySingleAccessoryRaisesPowerByAtMostFivePercent()
        {
            var model = NewModel(1);
            model.Save.OwnedAccessories = Enumerable.Range(0, GameModel.AccessoryNames.Length).ToList();
            int worst = 0;
            foreach (int level in new[] { 1, 10, 30, 60 })
            {
                for (int m = 0; m < 4; m++) model.Save.MemberLevels[m] = level;
                for (int item = 0; item < GameModel.AccessoryNames.Length; item++)
                for (int upgrade = 0; upgrade <= 3; upgrade++)
                {
                    model.Save.AccessoryUpgradeLevels[item] = upgrade;
                    CombatStatBonuses bonus = model.EffectiveAccessoryBonuses(item);
                    Assert.That(Math.Max(bonus.Hp, Math.Max(bonus.Attack, bonus.Defense)), Is.LessThanOrEqualTo(GameModel.MaxItemStatPermille), $"#{item} +{upgrade}");
                    for (int m = 0; m < 4; m++)
                    {
                        int gain = model.AccessoryPowerGainPermille(m, item);
                        worst = Math.Max(worst, gain);
                        Assert.That(gain, Is.LessThanOrEqualTo(50), $"{GameModel.AccessoryNames[item]} +{upgrade} on member {m} L{level}");
                    }
                }
            }
            TestContext.WriteLine($"largest single-item power gain {worst / 10f:0.0}%");
        }

        // ------------------------------------------------------------------ bosses

        [Test]
        public void BossStagesCarryTheirTier()
        {
            var tactics = NewModel(1).Tactics;
            foreach (StageDefinition stage in tactics.Stages)
            {
                string expected = stage.Id.EndsWith("-5") ? BattleSimulator.BossTierMini
                    : stage.Id.EndsWith("-10") ? BattleSimulator.BossTierMajor : string.Empty;
                Assert.That(stage.BossTier ?? string.Empty, Is.EqualTo(expected), stage.Id);
                if (expected.Length > 0) Assert.That(stage.EncounterType, Is.EqualTo("boss"), stage.Id);
            }
        }

        [Test]
        public void BossActsTwiceARoundAndItsSweepHitsTheWholeTeam()
        {
            BattleSimulator battle = Start("stage-1-5", 10, 4, 3);
            BattleUnit boss = battle.Units.Single(u => battle.IsStageBoss(u));
            var perRound = new Dictionary<int, int>();
            int cursor = battle.Log.Count;
            bool sweepHitAll = false;
            while (battle.Outcome == BattleOutcome.Ongoing && battle.TurnRound <= 3)
            {
                if (Step(battle, Mode.Auto) == null) break;
                for (; cursor < battle.Log.Count; cursor++)
                {
                    BattleEvent e = battle.Log[cursor];
                    if (e.ActorId != boss.Id || e.Kind != BattleEventKind.ActionStarted && !(e.Kind == BattleEventKind.Buff && e.SkillId == "rt-cast")) continue;
                    // BattleEvent.Round counts realtime measures; the turn round is the simulator's TurnRound.
                    perRound[battle.TurnRound] = (perRound.TryGetValue(battle.TurnRound, out int n) ? n : 0) + 1;
                    if (e.SkillId == BattleSimulator.BossSweepSkillId)
                    {
                        int hit = battle.Log.Skip(cursor).TakeWhile(x => x == e || x.Kind != BattleEventKind.ActionStarted)
                            .Where(x => x.Kind == BattleEventKind.Damage && x.ActorId == boss.Id).Select(x => x.TargetId).Distinct().Count();
                        sweepHitAll |= hit == battle.Units.Count(u => u.Side == BattleSide.Player && u.Alive) || hit == 4;
                    }
                }
            }
            Assert.That(perRound.TryGetValue(1, out int first) ? first : 0, Is.EqualTo(BattleSimulator.BossActionsPerRound), "第 1 回合 BOSS 出手两次");
            Assert.That(perRound.TryGetValue(2, out int second) ? second : 0, Is.EqualTo(BattleSimulator.BossActionsPerRound), "第 2 回合 BOSS 出手两次");
            Assert.That(sweepHitAll, Is.True, "碎音横扫打全队");
        }

        [Test]
        public void MassSilenceIsAnnouncedThenSealsEveryMember()
        {
            bool seen = false;
            for (int seed = 0; seed < 8 && !seen; seed++)
            {
                BattleSimulator battle = Start("stage-1-5", 11, 4, 1UL + (ulong)seed * 7919UL);
                BattleUnit boss = battle.Units.Single(u => battle.IsStageBoss(u));
                bool announced = false;
                int cursor = battle.Log.Count;
                while (battle.Outcome == BattleOutcome.Ongoing && !seen)
                {
                    if (Step(battle, Mode.Auto) == null) break;
                    if (battle.CastRemaining(boss) > 0 && battle.EnemyCapabilityOf(boss) == EnemySpecialCapability.MassSilence)
                    {
                        announced = true;
                        Assert.That(battle.EnemyCastIntent(boss), Is.EqualTo("禁魔"));
                        Assert.That(battle.EnemyCastTargetsAll(boss), Is.True);
                        Assert.That(battle.EnemyCastTargetDescription(boss), Is.EqualTo("全队"));
                        Assert.That(battle.EnemyThreatName(boss), Does.Contain("群体禁魔"));
                    }
                    for (; cursor < battle.Log.Count; cursor++)
                    {
                        BattleEvent e = battle.Log[cursor];
                        if (e.Kind != BattleEventKind.ActionStarted || e.SkillId != BattleSimulator.BossMassSilenceSkillId) continue;
                        Assert.That(announced, Is.True, "群体禁魔前一回合必须有蓄力预警");
                        Assert.That(battle.LookupSkill(e.SkillId).Name, Is.EqualTo("群体禁魔"));
                        foreach (BattleUnit member in battle.Units.Where(u => u.Side == BattleSide.Player && u.Alive))
                        {
                            Assert.That(battle.IsSealed(member), Is.True, member.Definition.Name);
                            Assert.That(battle.StatusesOf(member).Any(s => s.Kind == UnitStatusKind.Sealed && s.Rounds > 0), Is.True, "封印图标挂在成员身上");
                            Assert.That(battle.IsUltimateReady(member), Is.False, "封印期间不能放大招");
                            Assert.That(battle.Log.Skip(cursor).Any(x => x.Kind == BattleEventKind.Buff && x.SkillId == "rt-seal" && x.TargetId == member.Id), Is.True);
                        }
                        seen = true;
                        break;
                    }
                }
            }
            Assert.That(seen, Is.True, "1-5 小BOSS 应在战斗中放出群体禁魔");
        }

        [Test]
        public void MajorBossRotatesNovaSilenceAndCorrosion()
        {
            var used = new HashSet<string>();
            bool corrodeMarks = false;
            for (int seed = 0; seed < 6; seed++)
            {
                BattleSimulator battle = Start("stage-1-10", 17, 9, 1UL + (ulong)seed * 7919UL);
                int cursor = battle.Log.Count;
                while (battle.Outcome == BattleOutcome.Ongoing)
                {
                    if (Step(battle, Mode.Auto) == null) break;
                    for (; cursor < battle.Log.Count; cursor++)
                    {
                        BattleEvent e = battle.Log[cursor];
                        BattleUnit actor = battle.FindUnit(e.ActorId);
                        if (e.Kind == BattleEventKind.ActionStarted && actor != null && battle.IsStageBoss(actor)) used.Add(e.SkillId);
                        if (e.Kind == BattleEventKind.Buff && e.SkillId == BattleSimulator.BossCorrodeSkillId)
                        {
                            BattleUnit target = battle.FindUnit(e.TargetId);
                            corrodeMarks |= battle.StatusesOf(target).Any(s => s.Kind == UnitStatusKind.AntiHeal) &&
                                            battle.StatusesOf(target).Any(s => s.Kind == UnitStatusKind.Poison);
                        }
                    }
                }
            }
            CollectionAssert.IsSupersetOf(used, new[] { BattleSimulator.BossSweepSkillId, BattleSimulator.BossNovaSkillId,
                BattleSimulator.BossMassSilenceSkillId, BattleSimulator.BossCorrodeSkillId });
            Assert.That(corrodeMarks, Is.True, "蚀心侵染在成员身上挂禁疗 + 毒");
        }

        // ------------------------------------------------------------------ 充能顺序 (企划书 顺位充能)

        [Test]
        public void UltimateChargeFollowsPositionWithoutMovingTheNewCaptain()
        {
            // 2026-11-03 planning deck: the captain charges first (17s), the others follow formation order.
            BattleSimulator battle = Start("stage-1-1", 1, -1, 5);
            BattleUnit[] team = battle.Units.Where(u => u.Side == BattleSide.Player).ToArray();
            int[] order = battle.FormationOrder1102.ToArray();
            CollectionAssert.AreEqual(new[] { 1000, 850, 680, 567 }, team.Select(battle.ChargeOrderPermille).ToArray());
            Assert.That(battle.CurrentLeaderId, Is.EqualTo(team[0].Id));
            Assert.That(battle.TrySwapCaptain(team[2].Id, out string message), Is.True, message);
            CollectionAssert.AreEqual(order, battle.FormationOrder1102.ToArray(), "Changing captain never moves a card");
            CollectionAssert.AreEqual(new[] { 850, 680, 1000, 567 }, team.Select(battle.ChargeOrderPermille).ToArray(),
                "The new captain releases first; the rest keep their formation order");
        }

        // ------------------------------------------------------------------ 怪物分配

        [Test]
        public void EveryEnemyWithArtFightsInBothChaptersWithItsOwnSkill()
        {
            var tactics = NewModel(1).Tactics;
            // 2026-11-03: chapter one follows the planning deck's 1-1..1-10 table (all eight enemies + the elite appear);
            // chapter two keeps every original enemy in at least two fights.
            var chapterOne = tactics.Stages.Where(s => s.Id.StartsWith("stage-1-")).ToList();
            foreach (string id in ArtEnemies.Concat(DeckEnemies))
                Assert.That(chapterOne.Count(s => s.Enemies.Any(e => e.UnitId == id)), Is.GreaterThanOrEqualTo(1), "stage-1- " + id);
            var chapterTwo = tactics.Stages.Where(s => s.Id.StartsWith("stage-2-")).ToList();
            foreach (string id in ArtEnemies)
                Assert.That(chapterTwo.Count(s => s.Enemies.Any(e => e.UnitId == id)), Is.GreaterThanOrEqualTo(2), "stage-2- " + id);
            // Each reworked role shows up in real chapter-one fights (11-01c: 12 seeds — the retuned 1-3 elite fight is
            // short enough that its 蛛娘 sometimes falls before her venom in the first few).
            var skills = new HashSet<string>();
            foreach (string stage in new[] { "stage-1-2", "stage-1-3", "stage-1-4", "stage-1-7" })
            for (int seed = 0; seed < 12; seed++)
            {
                BattleSimulator battle = Start(stage, 1, -1, 1UL + (ulong)seed * 7919UL);
                while (battle.Outcome == BattleOutcome.Ongoing)
                {
                    if (Step(battle, Mode.Auto) == null) break;
                    Assert.That(battle.ActiveMinionCount, Is.LessThanOrEqualTo(BattleSimulator.MaxActiveMinions), "同屏最多 2 只小怪");
                }
                foreach (BattleEvent e in battle.Log) if (e.SkillId != null) skills.Add(e.SkillId);
            }
            CollectionAssert.IsSupersetOf(skills, new[] { BattleSimulator.EnemyVenomSkillId, BattleSimulator.EnemyRegenSkillId,
                BattleSimulator.EnemyAmbushSkillId, BattleSimulator.EnemyCharmSealSkillId });
        }

        // ------------------------------------------------------------------ economy

        [Test]
        public void FirstClearGoldDoesNotPayForTheRecommendedLevels()
        {
            var model = NewModel(1);
            int start = new GameSave().Gold;
            int TeamCost(int level) => 4 * Enumerable.Range(1, level - 1).Sum(BattleSimulator.TrainingCostAtLevel);
            int Earned(int lastStage) => Enumerable.Range(1, lastStage).Sum(n => model.Tactics.FindStage($"stage-1-{n}").GoldReward);
            int forMiniBoss = TeamCost(10) - (start + Earned(4));
            int forFinale = TeamCost(model.Tactics.FindStage("stage-1-10").RecommendedLevel) - (start + Earned(9));
            TestContext.WriteLine($"1-5 需 {TeamCost(10)}，开局+首通 {start + Earned(4)}，缺 {forMiniBoss}；1-10 缺 {forFinale}");
            Assert.That(forMiniBoss, Is.GreaterThan(0), "不重复刷图时金币不够把全队练到 10 级");
            Assert.That(forFinale, Is.GreaterThan(forMiniBoss));
        }

        [Test]
        public void NewcomerGuardIsOnByDefaultAndOnlySoftensOneOneUntilItsFirstClear()
        {
            // 2026-11-01b (用户: "第一关可以简单点"): on by default; 简单 enemies until 1-1 is cleared once, 普通 rewards.
            Assert.That(GameModel.FirstStageNewcomerGuard, Is.True);
            var fresh = new GameModel(() => Now, new InMemorySaveStore());
            BattleSimulator first = fresh.StartStageBattle("stage-1-1", 1, out string message);
            Assert.That(first.EnemyHpMultiplierPermille, Is.EqualTo(850));
            Assert.That(first.EnemyAttackMultiplierPermille, Is.EqualTo(850));
            Assert.That(message, Does.Contain("普通"), "保底不改难度显示");
            Assert.That(fresh.PreviewStageGoldReward("stage-1-1"), Is.EqualTo(fresh.Tactics.FindStage("stage-1-1").GoldReward));
            fresh.Save.ClearedStages.Add(new StageClear { Id = "stage-1-1", Stars = 1 });
            Assert.That(fresh.StartStageBattle("stage-1-1", 2, out _).EnemyHpMultiplierPermille, Is.EqualTo(1000), "通关后重打恢复正常");
            fresh.Save.StoryProgress = 100;
            Assert.That(fresh.StartStageBattle("stage-1-2", 3, out _).EnemyHpMultiplierPermille, Is.EqualTo(1000), "只影响 1-1");
            GameModel.FirstStageNewcomerGuard = false;
            Assert.That(new GameModel(() => Now, new InMemorySaveStore()).StartStageBattle("stage-1-1", 4, out _).EnemyHpMultiplierPermille,
                Is.EqualTo(1000));
            GameModel.FirstStageNewcomerGuard = true;

            // A brand-new team (level 1, no gear) wins its first 1-1 nine times in ten or more; replays stay ≈60%.
            double guarded = 0;
            for (int k = 0; k < TargetSeeds; k++)
            {
                GameModel model = NewModel(1, -1, 0, firstClearOneOne: false);
                BattleSimulator battle = model.StartStageBattle("stage-1-1", 1UL + (ulong)k * 7919UL, out string error);
                Assert.That(battle, Is.Not.Null, error);
                battle.EnableTurnBased();
                for (int guard = 0; guard < 4000 && battle.Outcome == BattleOutcome.Ongoing; guard++)
                    if (Step(battle, Mode.Auto) == null) break;
                if (battle.Outcome == BattleOutcome.Victory) guarded++;
            }
            guarded = 100.0 * guarded / TargetSeeds;
            double replay = WinRate("stage-1-1", 1, -1, Mode.Auto, TargetSeeds);
            TestContext.WriteLine($"1-1 首战（保底）{guarded:0.0}% · 重打 {replay:0.0}%");
            Assert.That(guarded, Is.GreaterThanOrEqualTo(90.0));
            Assert.That(replay, Is.InRange(50.0, 70.0));
        }

        // ------------------------------------------------------------------ 骰子加成 / 四队长 (2026-11-01b)

        [Test]
        public void DiceAddAboutTenPercentOnStagesOneToFour()
        {
            // 骰子加成 = 正常玩法（骰子 + 队长被动，自动）减去「骰子完全无效」(nodice) 的通过率，1 级无装备。
            double sum = 0;
            for (int number = 1; number <= 4; number++)
            {
                double auto = WinRate($"stage-1-{number}", 1, -1, Mode.Auto, TargetSeeds);
                double none = WinRate($"stage-1-{number}", 1, -1, Mode.NoDice, TargetSeeds);
                TestContext.WriteLine($"1-{number} 自动 {auto:0.0}% · 无骰子 {none:0.0}% · 骰子加成 {auto - none:+0.0;-0.0}");
                Assert.That(auto - none, Is.InRange(-2.0, 25.0), $"1-{number}");
                sum += auto - none;
            }
            Assert.That(sum / 4, Is.InRange(5.0, 15.0), "1-1~1-4 骰子加成约 +10%（±5）");
        }

        [Test]
        public void LowHandsGiveNoCaptainPassive()
        {
            foreach (DicePattern low in new[] { DicePattern.HighPoint, DicePattern.Pair, DicePattern.TwoPair, DicePattern.ThreePair })
            {
                Assert.That(BattleSimulator.Combo(low), Is.Zero, low.ToString());
                Assert.That(BattleSimulator.PoisonLayers(low), Is.Zero, low.ToString());
                Assert.That(BattleSimulator.Pierce(low), Is.Zero, low.ToString());
                Assert.That(BattleSimulator.ShieldPercent(low), Is.Zero, low.ToString());
            }
            foreach (DicePattern low in new[] { DicePattern.Pair, DicePattern.TwoPair })
            {
                DiceHand hand = DiceRules.Evaluate(Hand(low));
                Assert.That(hand.Pattern, Is.EqualTo(low));
                Assert.That(DiceTurn.RhythmHandBonusPermille(hand.MultiplierPermille), Is.Zero, "一对/两对不加攻击 " + low);
            }
            Assert.That(BattleSimulator.Combo(DicePattern.ThreeKind), Is.GreaterThan(0));
            Assert.That(BattleSimulator.PoisonLayers(DicePattern.ThreeKind), Is.GreaterThan(0));
            Assert.That(DiceTurn.RhythmHandBonusPermille(DiceRules.Evaluate(Hand(DicePattern.ThreeKind)).MultiplierPermille), Is.GreaterThan(0));
        }

        private static int[] Hand(DicePattern p)
        {
            switch (p)
            {
                case DicePattern.Pair: return new[] { 1, 1, 2, 3, 4, 6 };
                case DicePattern.TwoPair: return new[] { 1, 1, 2, 2, 4, 6 };
                case DicePattern.ThreePair: return new[] { 1, 1, 2, 2, 4, 4 };
                case DicePattern.ThreeKind: return new[] { 1, 1, 1, 2, 4, 6 };
                default: throw new ArgumentOutOfRangeException(nameof(p));
            }
        }

        [Test]
        public void FourCaptainsAreWithinFifteenPointsOnTheFirstMiniBoss()
        {
            string[] names = { "魅族 星璃", "恶魔 绯音", "人鱼 雾白", "血精灵 夜莺" };
            var rates = new double[4];
            for (int captain = 0; captain < 4; captain++)
            {
                rates[captain] = WinRate("stage-1-5", 10, 4, Mode.Auto, TargetSeeds, captain);
                TestContext.WriteLine($"1-5 L10+装备 队长 {names[captain]} {rates[captain]:0.0}%");
            }
            foreach (double rate in rates) Assert.That(rate, Is.InRange(50.0, 80.0), "同等级装备下四个种族队长都在 65% ±15 内");
            Assert.That(rates.Max() - rates.Min(), Is.LessThanOrEqualTo(25.0));
        }

        // ------------------------------------------------------------------ 无限骰子 (2026-11-01c)

        [Test]
        public void ManualSpamRerollingIsNotMuchBetterThanAuto()
        {
            // 用户: "一直能投，但数值控制好". Spending every 骰子豆 before every action (手动狂投) vs 自动, 1 级无装备 1-1~1-4.
            double sum = 0;
            for (int number = 1; number <= 4; number++)
            {
                double auto = WinRate($"stage-1-{number}", 1, -1, Mode.Auto, TargetSeeds);
                double spam = WinRate($"stage-1-{number}", 1, -1, Mode.Spam, TargetSeeds);
                TestContext.WriteLine($"1-{number} 自动 {auto:0.0}% · 手动狂投 {spam:0.0}%");
                Assert.That(spam - auto, Is.LessThanOrEqualTo(12.0), $"1-{number}");
                sum += spam - auto;
            }
            Assert.That(sum / 4, Is.LessThanOrEqualTo(5.0), "手动狂投平均不比自动高出 5 个百分点以上");
        }

        [Test]
        public void EndlessRerollsNeverPushTheDiceBonusPastItsCap()
        {
            int maxUsed = 0, longBattles = 0, longPastThree = 0;
            for (int k = 0; k < 40; k++)
            {
                BattleSimulator battle = Start("stage-1-4", 1, -1, 1UL + (ulong)k * 7919UL, Mode.Spam);
                for (int guard = 0; guard < 4000 && battle.Outcome == BattleOutcome.Ongoing; guard++)
                {
                    if (Step(battle, Mode.Spam) == null) break;
                    Assert.That(battle.BattleDice.AccumulatedBonusPermille, Is.LessThanOrEqualTo(DiceTurn.EndlessBonusCapPermille));
                    Assert.That(battle.BattleDice.StoredRerolls, Is.InRange(0, DiceTurn.MaxStoredRerolls));
                }
                foreach (BattleEvent e in battle.Log)
                    if (e.Kind == BattleEventKind.Damage && e.DiceMultiplierPermille > 0)
                        Assert.That(e.DiceMultiplierPermille, Is.LessThanOrEqualTo(1000 + DiceTurn.EndlessBonusCapPermille));
                maxUsed = Math.Max(maxUsed, battle.BattleDice.UsedRerolls);
                if (battle.TurnRound >= 6)
                {
                    longBattles++;
                    if (battle.BattleDice.UsedRerolls > DiceTurn.MaxRerollsPerBattle) longPastThree++;
                }
            }
            // 狂投 stops once no two dice can improve the hand, so a few strong-hand battles stay at ≤3 — most do not.
            TestContext.WriteLine($"1-4 狂投：单场最多重投 {maxUsed} 次；≥6 回合的 {longBattles} 场里 {longPastThree} 场超过 3 次");
            Assert.That(maxUsed, Is.GreaterThan(DiceTurn.MaxRerollsPerBattle), "回合制不再有每场 3 次的上限");
            Assert.That(longPastThree * 2, Is.GreaterThan(longBattles));
            Assert.That(BattleSimulator.Combo(DicePattern.FiveKind), Is.EqualTo(BattleSimulator.Combo(DicePattern.FourKind)), "被动四条封顶");
            Assert.That(BattleSimulator.PoisonLayers(DicePattern.SixKind), Is.EqualTo(BattleSimulator.PoisonLayers(DicePattern.FourKind)));
            Assert.That(BattleSimulator.Pierce(DicePattern.FiveKind), Is.EqualTo(BattleSimulator.Pierce(DicePattern.FourKind)));
        }

        // ------------------------------------------------------------------ report (writes Builds/qa-logs/zj-1101/balance)

        [Test]
        public void ReportPassRatesForEveryStage()
        {
            var lines = new List<string> { $"BALANCE-1101 seeds={ReportSeeds} (auto=自动大招+自动重投 / noreroll=自动大招不重投 / nodice=骰子无效 / idle=手动不操作)" };
            var tactics = NewModel(1).Tactics;
            foreach (StageDefinition stage in tactics.Stages)
            {
                int number = int.Parse(stage.Id.Split('-')[2]);
                int cleared = stage.Id.StartsWith("stage-1-") ? number - 1 : 10;
                int rec = stage.RecommendedLevel;
                string All(int level, int gear) => string.Join(" ", Enum.GetValues(typeof(Mode)).Cast<Mode>()
                    .Select(mode => $"{mode.ToString().ToLowerInvariant()}={WinRate(stage.Id, level, gear, mode, ReportSeeds),5:0.0}%"));
                lines.Add($"{stage.Id,-10} {stage.EncounterType,-6} {stage.BossTier,-5} 推荐L{rec} 金币{stage.GoldReward}");
                lines.Add($"    L1 无装备    {All(1, -1)}");
                lines.Add($"    推荐+装备    {All(rec, cleared)}");
                lines.Add($"    推荐 无装备  auto={WinRate(stage.Id, rec, -1, Mode.Auto, ReportSeeds),5:0.0}%" +
                          $"  推荐-1+装备 auto={WinRate(stage.Id, Math.Max(1, rec - 1), cleared, Mode.Auto, ReportSeeds),5:0.0}%" +
                          $"  推荐+1+装备 auto={WinRate(stage.Id, rec + 1, cleared, Mode.Auto, ReportSeeds),5:0.0}%");
            }
            string text = string.Join("\n", lines);
            TestContext.WriteLine(text);
            // 11-01c: written to the dice-endless batch (the 11-01 balance batch's evidence in zj-1101/balance stays as it was).
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "qa-logs", "zj-1101c", "dice-endless"));
            if (Directory.Exists(dir)) File.WriteAllText(Path.Combine(dir, "balance-editmode.txt"), text + "\n");
        }

        // ------------------------------------------------------------------ helpers

        private static GameModel NewModel(int level, int clearedChapterOne = -1, int captain = 0, bool firstClearOneOne = true)
        {
            var model = new GameModel(() => Now, new InMemorySaveStore());
            for (int i = 0; i < model.Save.MemberLevels.Count; i++) model.Save.MemberLevels[i] = level;
            // StoryProgress 100 opens chapter one; only chapter-two clears are recorded (a chapter-one clear would make the
            // next save grant that stage's first-clear accessory retroactively and inflate the "gear so far" loadout).
            foreach (StageDefinition s in model.Tactics.Stages)
                if (s.Id.StartsWith("stage-2-")) model.Save.ClearedStages.Add(new StageClear { Id = s.Id, Stars = 3 });
            // 1-1 already cleared once: the 通过率 targets are about replays, not the guarded first 1-1 (2026-11-01b).
            if (firstClearOneOne) model.Save.ClearedStages.Add(new StageClear { Id = "stage-1-1", Stars = 1 });
            model.Save.StoryProgress = 100;
            if (clearedChapterOne >= 0)
            {
                for (int n = 1; n <= Math.Min(10, clearedChapterOne); n++)
                {
                    int item = GameModel.FirstClearAccessory($"stage-1-{n}");
                    if (!model.Save.OwnedAccessories.Contains(item)) model.Save.OwnedAccessories.Add(item);
                }
                foreach (int member in model.Save.Team.ToArray()) model.QuickEquip(member, out _);
            }
            if (captain > 0) Assert.That(model.SetTeamLeader(model.Save.Team[captain], out string message), Is.True, message);
            return model;
        }

        private static BattleSimulator Start(string stageId, int level, int clearedChapterOne, ulong seed, Mode mode = Mode.Auto,
            int captain = 0)
        {
            GameModel model = NewModel(level, clearedChapterOne, captain);
            BattleSimulator battle = model.StartStageBattle(stageId, seed, out string error);
            Assert.That(battle, Is.Not.Null, stageId + " " + error);
            battle.EnableTurnBased();
            if (mode == Mode.NoDice) battle.BattleDice.NeutralizeForBalanceAudit1101();
            return battle;
        }

        private sealed class AutoState { public int Actions, LastReroll = -100; }
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<BattleSimulator, AutoState> States =
            new System.Runtime.CompilerServices.ConditionalWeakTable<BattleSimulator, AutoState>();

        /// <summary>One action of the 通过率 policy. Returns the actor, or null once the battle has ended.</summary>
        private static BattleUnit Step(BattleSimulator battle, Mode mode)
        {
            AutoState state = States.GetOrCreateValue(battle);
            BattleUnit actor = battle.NextTurnActor();
            if (actor == null) return null;
            if (actor.Side == BattleSide.Player)
            {
                if (mode == Mode.Auto) AutoReroll(battle.BattleDice, state);
                else if (mode == Mode.Spam) SpamReroll(battle.BattleDice);
                battle.AutoTurn(mode != Mode.Idle);
            }
            else battle.AutoTurn();
            state.Actions++;
            return actor;
        }

        /// <summary>手动狂投: every pip, every action, on the two dice that help most (stops when nothing can improve).</summary>
        private static void SpamReroll(DiceTurn dice)
        {
            for (int guard = 0; guard < 16 && dice.CanReroll; guard++)
            {
                bool[] pick = DiceAutoPlanner1031.ChooseRerollMask(dice.Values, DiceTurn.MaxDicePerReroll, false);
                if (DiceAutoPlanner1031.Count(pick) == 0) break;
                for (int i = 0; i < pick.Length; i++) if (pick[i] && dice.Held[i]) dice.ToggleHold(i);
                if (!dice.RerollUnheld(out _)) break;
            }
        }

        /// <summary>The shipped 自动 reroll policy (same as DiceBalance1031Tests / TacticsBattlePanel.AutoReroll1031).</summary>
        private static void AutoReroll(DiceTurn dice, AutoState state)
        {
            if (state.Actions - state.LastReroll < 3 || !DiceAutoPlanner1031.ShouldAutoReroll(dice)) return;
            bool[] pick = DiceAutoPlanner1031.ChooseRerollMask(dice.Values, DiceTurn.MaxDicePerReroll, false);
            for (int i = 0; i < pick.Length; i++) if (pick[i] && dice.Held[i]) dice.ToggleHold(i);
            if (dice.RerollUnheld(out _)) state.LastReroll = state.Actions;
        }

        public static double WinRate(string stageId, int level, int clearedChapterOne, Mode mode, int seeds, int captain = 0)
        {
            int wins = 0;
            for (int k = 0; k < seeds; k++)
            {
                BattleSimulator battle = Start(stageId, level, clearedChapterOne, 1UL + (ulong)k * 7919UL, mode, captain);
                for (int guard = 0; guard < 4000 && battle.Outcome == BattleOutcome.Ongoing; guard++)
                    if (Step(battle, mode) == null) break;
                if (battle.Outcome == BattleOutcome.Victory) wins++;
            }
            return 100.0 * wins / seeds;
        }
    }
}
