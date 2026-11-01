using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ChoSiren.Systems.Tactics;
using NUnit.Framework;
using UnityEngine;

namespace ChoSiren.Tests
{
    /// <summary>Measurements of unchanged production combat, never a pacing adjustment.</summary>
    public sealed class BattlePaceAuditTests
    {
        private const string Output = "/Users/bojuejun/Documents/Codex/2026-10-09/kan/work/battle-cleanup-20261016/battle-pace-audit.json";
        private static readonly DateTime Now = new DateTime(2026, 10, 16, 12, 0, 0);
        private static readonly int[] PartyOrder = { 0, 3, 1, 2 };
        private static readonly ulong[] Seeds = { 1, 2, 3, 4, 5, 847 };

        [Serializable] public sealed class Report
        {
            public string scope = "Unchanged production source; in-memory saves only";
            public int logicalStepMilliseconds = BattleSimulator.RealtimeStepMilliseconds;
            public string controls = "Manual: no ultimate taps. Auto: automatic ultimates. Both: no dice rerolls, no tactical guard, no target changes.";
            public string screenshotStage = "stage-1-9 (2026-11-01 roster: 狂响幻蚀歌姬 + 浊浪溺者, then 幻魅歌姬 + 赤血刺客)";
            public string enemyHpScopes = "openingEnemyHp matches the HUD: spawned enemies in CurrentWave. modelEnemyHpAllWaves is CurrentEnemyHp, which includes unspawned later waves. stage-1-9 starts with 28819 visible and 43669 across both waves.";
            public string screenshotEquipmentAssumption = "Feiyin wears owned unupgraded index11 + index38 in distinct slots. Their HP150/ATK200/DEF190 permille reproduces screenshot HP9811/ATK721. Real save equipment/defense is unknown.";
            public int maxCatalogRecommendedLevel;
            public bool hasRecommendedStageAtScreenshotAverageLevel;
            public bool complete;
            public List<Run> runs = new List<Run>();
        }
        [Serializable] public sealed class UnitStart
        {
            public string id;
            public int level, hp, attack, defense, displayedAttack, energy, ultimateCooldownMilliseconds;
            public int equipmentHpPermille, equipmentAttackPermille, equipmentDefensePermille;
        }
        [Serializable] public sealed class Wave
        {
            public int wave, enemyCount, totalHp, spawnedAtMilliseconds = -1, defeatedAtMilliseconds = -1;
        }
        [Serializable] public sealed class Defeat
        {
            public string id;
            public int wave, atMilliseconds;
        }
        [Serializable] public sealed class Ultimate
        {
            public string actor, skill;
            public int atMilliseconds;
        }
        [Serializable] public sealed class FirstBasic
        {
            public string actor;
            public int atMilliseconds, actual, expectedWithOneDiceMultiplier, diceMultiplierPermille;
            public bool critical;
        }
        [Serializable] public sealed class Run
        {
            public string scenario, stageId, mode, seed, openingHand, outcome;
            public int recommendedLevel, elapsedMilliseconds, victoryMilliseconds = -1;
            public int initialEnemyHpAllWaves, modelEnemyHpAllWaves, openingEnemyHp, firstEnemyDefeatMilliseconds = -1;
            public int openingDiceBonusPermille, endingDiceBonusPermille, openingDiceEnergy, openingEarnedRerolls;
            public int usedRerolls, firstUltimateMilliseconds = -1, maxDamageEventDiceMultiplierPermille;
            public long playerDamage, poisonDamage, enemyHpDamage, damageBeforeFirstSecond;
            public bool freshBattleAfterwardsStartsWithZeroEnergy;
            public List<UnitStart> party = new List<UnitStart>();
            public List<Wave> waves = new List<Wave>();
            public List<Defeat> defeats = new List<Defeat>();
            public List<Ultimate> ultimates = new List<Ultimate>();
            public List<FirstBasic> firstBasics = new List<FirstBasic>();
        }

        [Test]
        public void MeasureFreshRecommendedAndScreenshotPartiesWithoutChangingProductionRules()
        {
            var report = new Report();
            try
            {
                var catalog = new GameModel(() => Now, new InMemorySaveStore());
                report.maxCatalogRecommendedLevel = catalog.Tactics.Stages.Max(s => s.RecommendedLevel);
                int screenshotAverage = (68 + 57 + 64 + 59) / 4;
                report.hasRecommendedStageAtScreenshotAverageLevel = catalog.Tactics.Stages
                    .Any(s => s.RecommendedLevel >= screenshotAverage);
                foreach (string scenario in new[] { "new-player-l1", "recommended-l9", "screenshot-l68-57-64-59" })
                foreach (bool automatic in new[] { false, true })
                foreach (ulong seed in Seeds)
                    report.runs.Add(Measure(scenario, automatic, seed));
                report.complete = true;
            }
            finally
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Output));
                File.WriteAllText(Output, JsonUtility.ToJson(report, true));
                TestContext.WriteLine("BATTLE_PACE_AUDIT " + Output + " complete=" + report.complete);
            }
        }

        private static GameModel Fixture(string scenario)
        {
            var model = new GameModel(() => Now, new InMemorySaveStore());
            model.Save.Team = PartyOrder.ToList();
            model.Save.StoryProgress = 100; // Unlock the measured stage in the isolated fixture only.
            model.Save.SelectedDifficulty = BattleDifficulty.Normal;
            model.Save.MemberAccessories.Clear();
            model.Save.PersonalEquipmentMigrated = true;
            model.Save.EquippedAccessory = -1;
            int[] levels = scenario == "new-player-l1" ? new[] { 1, 1, 1, 1 }
                : scenario == "recommended-l9" ? new[] { 9, 9, 9, 9 } : new[] { 68, 57, 64, 59 };
            for (int i = 0; i < PartyOrder.Length; i++) model.Save.MemberLevels[PartyOrder[i]] = levels[i];
            typeof(GameModel).GetMethod("SaveState", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(model, null);
            if (scenario.StartsWith("screenshot", StringComparison.Ordinal))
            {
                foreach (int item in new[] { 11, 38 })
                {
                    if (!model.Save.OwnedAccessories.Contains(item)) model.Save.OwnedAccessories.Add(item);
                    Assert.That(model.EquipAccessoryForMember(1, item, out string message), Is.True, message);
                }
                CombatStatBonuses gear = model.MemberEquipmentBonuses(1);
                // 2026-11-01: items are capped at +5% a stat (GameModelBalance1101); the 10-16 screenshot's 150/200 permille
                // came from the old uncapped table, so the expectation is read from the live table.
                Assert.That(gear.Hp, Is.EqualTo(GameModel.AccessoryBonuses(11).Hp + GameModel.AccessoryBonuses(38).Hp));
                Assert.That(gear.Attack, Is.EqualTo(GameModel.AccessoryBonuses(11).Attack + GameModel.AccessoryBonuses(38).Attack));
                Assert.That(GameModel.AccessorySlot(11), Is.Not.EqualTo(GameModel.AccessorySlot(38)));
            }
            return model;
        }

        private static Run Measure(string scenario, bool automatic, ulong seed)
        {
            GameModel model = Fixture(scenario);
            string stage = scenario == "new-player-l1" ? "stage-1-1" : "stage-1-9";
            BattleSimulator battle = model.StartStageBattle(stage, seed, out string problem);
            Assert.That(battle, Is.Not.Null, problem);
            battle.AutoCastUltimates = automatic;
            Assert.That(battle.ElapsedMilliseconds, Is.Zero);
            Assert.That(battle.Units.Where(u => u.Side == BattleSide.Player).All(u => battle.EnergyOf(u) == 0), Is.True);
            Assert.That(battle.ReadyUltimateCount, Is.Zero);
            Assert.That(battle.Log.Any(e => e.Kind == BattleEventKind.Damage), Is.False,
                "Opening the UI or creating a battle cannot apply damage before the first logical tick.");
            Assert.That(battle.BattleDice.Energy, Is.Zero);
            Assert.That(battle.BattleDice.EarnedRerolls, Is.Zero);
            var run = new Run
            {
                scenario = scenario, stageId = stage, mode = automatic ? "auto-ultimates/no-rerolls" : "manual/no-taps",
                seed = seed.ToString(), openingHand = battle.BattleDice.Hand.DisplayName,
                recommendedLevel = battle.Stage.RecommendedLevel,
                // CurrentEnemyHp intentionally counts the whole roster, including later waves.
                // The player's screenshot uses RefreshBattleHud's current-wave-only readout.
                openingEnemyHp = battle.Units.Where(u => u.Side == BattleSide.Enemy && u.Spawned &&
                    u.Wave == battle.CurrentWave - 1).Sum(u => Math.Max(0, u.Hp)),
                modelEnemyHpAllWaves = battle.CurrentEnemyHp,
                initialEnemyHpAllWaves = battle.Units.Where(u => u.Side == BattleSide.Enemy).Sum(u => u.MaxHp),
                openingDiceBonusPermille = battle.BattleDice.AccumulatedBonusPermille,
                openingDiceEnergy = battle.BattleDice.Energy, openingEarnedRerolls = battle.BattleDice.EarnedRerolls
            };
            foreach (BattleUnit unit in battle.Units.Where(u => u.Side == BattleSide.Player))
            {
                CombatStatBonuses gear = model.MemberEquipmentBonuses(GameModel.IndexOfMember(unit.Definition.Id));
                run.party.Add(new UnitStart
                {
                    id = unit.Definition.Id, level = unit.Level, hp = unit.MaxHp, attack = unit.BaseAttack,
                    defense = unit.BaseDefense, displayedAttack = unit.Attack * battle.DiceMultiplierFor(unit) / 1000,
                    energy = battle.EnergyOf(unit), ultimateCooldownMilliseconds = battle.UltimateCooldownRemaining(unit),
                    equipmentHpPermille = gear.Hp, equipmentAttackPermille = gear.Attack, equipmentDefensePermille = gear.Defense
                });
            }
            if (scenario.StartsWith("screenshot", StringComparison.Ordinal))
            {
                // 2026-11-01: stage-1-9's roster and the item table changed (Docs/BALANCE-1101.md), so the screenshot's
                // 28819 HUD health and HP9811/ATK721 no longer apply; the party must still match the live stat formula.
                Assert.That(run.openingEnemyHp, Is.GreaterThan(0));
                foreach (BattleUnit unit in battle.Units.Where(u => u.Side == BattleSide.Player))
                {
                    int index = GameModel.IndexOfMember(unit.Definition.Id);
                    CombatStats expected = BattleSimulator.PlayerStats(model.Tactics.FindUnit(unit.Definition.Id), model.LevelOf(index),
                        model.MemberEquipmentBonuses(index));
                    Assert.That(unit.MaxHp, Is.EqualTo(expected.Hp), unit.Definition.Id);
                    Assert.That(unit.BaseAttack, Is.EqualTo(expected.Attack), unit.Definition.Id);
                }
            }
            var enemies = battle.Units.Where(u => u.Side == BattleSide.Enemy).ToArray();
            foreach (var group in enemies.GroupBy(u => u.Wave).OrderBy(g => g.Key))
                run.waves.Add(new Wave { wave = group.Key + 1, enemyCount = group.Count(), totalHp = group.Sum(u => u.MaxHp),
                    spawnedAtMilliseconds = group.Key == 0 ? 0 : -1 });
            var remainingHp = enemies.ToDictionary(u => u.Id, u => u.Hp);
            var firstBasicSeen = new HashSet<int>();
            int cursor = battle.Log.Count;
            while (battle.Outcome == BattleOutcome.Ongoing && battle.ElapsedMilliseconds < battle.TimeLimitMilliseconds)
            {
                battle.AdvanceRealtime(BattleSimulator.RealtimeStepMilliseconds, false);
                while (cursor < battle.Log.Count)
                {
                    BattleEvent entry = battle.Log[cursor++];
                    BattleUnit actor = battle.FindUnit(entry.ActorId), target = battle.FindUnit(entry.TargetId);
                    if (entry.Kind == BattleEventKind.Spawned && target != null)
                        run.waves.Single(w => w.wave == target.Wave + 1).spawnedAtMilliseconds = entry.TimeMilliseconds;
                    if (entry.Kind == BattleEventKind.ActionStarted && actor != null && actor.Side == BattleSide.Player &&
                        entry.SkillId != null && entry.SkillId.EndsWith("-big", StringComparison.Ordinal))
                    {
                        run.ultimates.Add(new Ultimate { actor = actor.Definition.Id, skill = entry.SkillId, atMilliseconds = entry.TimeMilliseconds });
                        Assert.That(entry.TimeMilliseconds, Is.GreaterThanOrEqualTo(20000), "No opening ultimate or old-battle energy can bypass the initial cooldown.");
                    }
                    if (entry.Kind == BattleEventKind.Damage && target != null && target.Side == BattleSide.Enemy)
                    {
                        if (actor != null && actor.Side == BattleSide.Player)
                        {
                            run.maxDamageEventDiceMultiplierPermille = Math.Max(run.maxDamageEventDiceMultiplierPermille, entry.DiceMultiplierPermille);
                            Assert.That(entry.DiceMultiplierPermille, Is.InRange(1000, 2000));
                            if (entry.TimeMilliseconds < 1000) run.damageBeforeFirstSecond += entry.HpLostAmount;
                            if (entry.SkillId == "rt-basic" && firstBasicSeen.Add(actor.Id))
                            {
                                // Every actor's first basic happens before the first small skill,
                                // armor break or elite hardening. Compare against the real initial
                                // defense and remaining HP, including any earlier opening follow-up.
                                long raw = (long)actor.BaseAttack * entry.DiceMultiplierPermille / 1000;
                                raw = raw * 1000 / (1000 + target.BaseDefense * BattleSimulator.DefenseWeight);
                                if (entry.Critical) raw = raw * BattleSimulator.CritMultiplierPermille / 1000;
                                int expected = Math.Min(remainingHp[target.Id], (int)Math.Max(1, raw));
                                run.firstBasics.Add(new FirstBasic { actor = actor.Definition.Id, atMilliseconds = entry.TimeMilliseconds,
                                    actual = entry.Amount, expectedWithOneDiceMultiplier = expected,
                                    diceMultiplierPermille = entry.DiceMultiplierPermille, critical = entry.Critical });
                                Assert.That(entry.Amount, Is.EqualTo(expected), "The displayed dice bonus must multiply the base attack exactly once.");
                            }
                        }
                        remainingHp[target.Id] -= entry.HpLostAmount;
                        run.enemyHpDamage += entry.HpLostAmount;
                    }
                    if (entry.Kind == BattleEventKind.Heal && target != null && target.Side == BattleSide.Enemy)
                        remainingHp[target.Id] = Math.Min(target.MaxHp, remainingHp[target.Id] + entry.Amount);
                    if (entry.Kind == BattleEventKind.Defeated && target != null && target.Side == BattleSide.Enemy)
                        run.defeats.Add(new Defeat { id = target.Definition.Id, wave = target.Wave + 1, atMilliseconds = entry.TimeMilliseconds });
                }
                foreach (Wave wave in run.waves)
                    if (wave.defeatedAtMilliseconds < 0 && enemies.Where(u => u.Wave + 1 == wave.wave).All(u => u.Hp <= 0))
                        wave.defeatedAtMilliseconds = battle.ElapsedMilliseconds;
            }
            run.outcome = battle.Outcome.ToString();
            run.elapsedMilliseconds = battle.ElapsedMilliseconds;
            if (battle.Outcome == BattleOutcome.Victory) run.victoryMilliseconds = battle.ElapsedMilliseconds;
            run.endingDiceBonusPermille = battle.BattleDice.AccumulatedBonusPermille;
            run.usedRerolls = battle.BattleDice.UsedRerolls;
            run.playerDamage = battle.CharacterDamageDealt;
            run.poisonDamage = battle.PoisonDamageDealt;
            if (run.defeats.Count > 0) run.firstEnemyDefeatMilliseconds = run.defeats[0].atMilliseconds;
            if (run.ultimates.Count > 0) run.firstUltimateMilliseconds = run.ultimates[0].atMilliseconds;
            Assert.That(battle.Outcome, Is.Not.EqualTo(BattleOutcome.Ongoing));
            Assert.That(run.usedRerolls, Is.Zero);
            Assert.That(run.endingDiceBonusPermille, Is.EqualTo(run.openingDiceBonusPermille));
            if (!automatic) Assert.That(run.ultimates, Is.Empty, "Manual mode cannot cast without a tap.");
            BattleSimulator next = model.StartStageBattle(stage, seed + 10000, out string nextProblem);
            Assert.That(next, Is.Not.Null, nextProblem);
            run.freshBattleAfterwardsStartsWithZeroEnergy = next.Units.Where(u => u.Side == BattleSide.Player)
                .All(u => next.EnergyOf(u) == 0) && next.BattleDice.Energy == 0 && next.BattleDice.EarnedRerolls == 0;
            Assert.That(run.freshBattleAfterwardsStartsWithZeroEnergy, Is.True);
            TestContext.WriteLine($"PACE {scenario} {run.mode} seed={seed} {run.outcome} {run.elapsedMilliseconds}ms firstDefeat={run.firstEnemyDefeatMilliseconds}ms ultimates={run.ultimates.Count}");
            return run;
        }
    }
}
