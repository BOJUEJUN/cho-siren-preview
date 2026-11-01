using System;
using System.Collections.Generic;
using System.Linq;
using ChoSiren.Systems;
using ChoSiren.Systems.Tactics;
using NUnit.Framework;
using UnityEngine;

namespace ChoSiren.Tests
{
    /// <summary>2026-10-26 (赵金): seals and wards last whole rounds, the mermaid captain weakens enemies, statuses read out.</summary>
    public sealed class TurnSkillReadability1026Tests
    {
        private static readonly DateTime Now = new DateTime(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc);

        [SetUp, TearDown]
        public void Clean() { PlayerPrefs.DeleteKey(GameModel.SaveKey); PlayerPrefs.DeleteKey(GameModel.LegacySaveKey); }

        private static BattleSimulator TurnBattle(string stageId)
        {
            var model = new GameModel(() => Now);
            BattleSimulator battle = model.StartStageBattle(stageId, 7, out string error);
            Assert.That(battle, Is.Not.Null, error);
            var races = new Dictionary<string, string>();
            foreach (MemberDefinition member in GameModel.Members) races[member.Id] = member.Race;
            battle.EnableRealtime(races, null, 3, false);
            battle.EnableTurnBased();
            return battle;
        }

        /// <summary>Four 魅族 members against one front-row enemy too sturdy to die to the ultimate (and too weak to hurt).</summary>
        private static BattleSimulator SealFixture()
        {
            var manifest = new TacticsManifest();
            manifest.Skills.Add(new SkillDefinition { Id = "basic", Name = "basic", Cooldown = 0 });
            UnitDefinition Unit(string id, int hp, int atk, int speed) => new UnitDefinition
            {
                Id = id, Name = id, MaxHp = hp, Attack = atk, Defense = 0,
                Speed = speed, CritPermille = 0, SkillIds = new List<string> { "basic" }
            };
            for (int i = 0; i < 4; i++) manifest.Units.Add(Unit("player" + i, 10000, 10, 5));
            manifest.Units.Add(Unit("dummy", 1000000, 1, 1));
            var stage = new StageDefinition { Id = "stage-seal", RecommendedLevel = 1, EncounterType = "normal", TimeLimitSeconds = 120 };
            stage.Enemies.Add(new EnemySpawn { UnitId = "dummy", Row = 0, Col = 0 });
            var party = new List<PlayerUnitSetup>();
            for (int i = 0; i < 4; i++) party.Add(new PlayerUnitSetup { UnitId = "player" + i, Row = i % 3, Col = i / 3 });
            var battle = new BattleSimulator(manifest, stage, party, new ScriptedRandom(new[] { 999 }, new[] { 0, 1, 2, 3, 4, 5 }));
            battle.EnableRealtime(Enumerable.Range(0, 4).ToDictionary(i => "player" + i, i => "魅族"), "player0", stage.RerollLimit, false);
            battle.EnableTurnBased();
            return battle;
        }

        [Test]
        public void CharmSealOutlastsTheSealedEnemysNextTurn()
        {
            BattleSimulator battle = SealFixture();
            BattleUnit charm = battle.Units.FirstOrDefault(u => u.Side == BattleSide.Player && battle.RaceOf(u) == CombatRace.Charm);
            Assume.That(charm, Is.Not.Null, "the starter team has a 魅族 member");
            for (int guard = 0; guard < 600 && battle.Outcome == BattleOutcome.Ongoing && !battle.IsUltimateReady(charm); guard++)
            {
                if (battle.NextTurnActor() == null) break;
                battle.AutoTurn(false);
            }
            Assume.That(battle.IsUltimateReady(charm), Is.True, "her bar fills");
            Assert.That(battle.TryCastUltimateNow(charm, UltimateTimingGrade.Miss), Is.True);
            var sealedEnemies = battle.Units.Where(u => u.Side == BattleSide.Enemy && battle.IsSealed(u)).ToList();
            Assume.That(sealedEnemies.Count, Is.GreaterThan(0), "the front row survived the hit");
            BattleUnit sealedEnemy = sealedEnemies[0];
            Assert.That(battle.StatusesOf(sealedEnemy).Any(s => s.Kind == UnitStatusKind.Sealed && s.Rounds >= 1), Is.True,
                "the badge shows the seal with rounds left");
            // Its own next turn comes and goes while the seal still holds (2 slices used to expire before it).
            bool actedSealed = false;
            for (int guard = 0; guard < 40 && battle.Outcome == BattleOutcome.Ongoing && sealedEnemy.Alive; guard++)
            {
                BattleUnit actor = battle.NextTurnActor();
                if (actor == null) break;
                if (actor == sealedEnemy) { actedSealed = battle.IsSealed(sealedEnemy); battle.EnemyTurn(); break; }
                battle.AutoTurn(false);
            }
            Assume.That(sealedEnemy.Alive, Is.True);
            Assert.That(actedSealed, Is.True, "still sealed when its turn arrives");
        }

        /// <summary>Four 人鱼 members and one weak, slow enemy that keeps attacking (2026-10-28 ward-duration check).</summary>
        private static BattleSimulator WardFixture()
        {
            var manifest = new TacticsManifest();
            manifest.Skills.Add(new SkillDefinition { Id = "basic", Name = "basic", Cooldown = 0 });
            UnitDefinition Unit(string id, int hp, int atk, int speed) => new UnitDefinition
            {
                Id = id, Name = id, MaxHp = hp, Attack = atk, Defense = 0,
                Speed = speed, CritPermille = 0, SkillIds = new List<string> { "basic" }
            };
            for (int i = 0; i < 4; i++) manifest.Units.Add(Unit("player" + i, 10000, 10, 5));
            manifest.Units.Add(Unit("dummy", 1000000, 5, 1));
            var stage = new StageDefinition { Id = "stage-ward", RecommendedLevel = 1, EncounterType = "normal", TimeLimitSeconds = 120 };
            stage.Enemies.Add(new EnemySpawn { UnitId = "dummy", Row = 0, Col = 0 });
            var party = new List<PlayerUnitSetup>();
            for (int i = 0; i < 4; i++) party.Add(new PlayerUnitSetup { UnitId = "player" + i, Row = i % 3, Col = i / 3 });
            var battle = new BattleSimulator(manifest, stage, party, new ScriptedRandom(new[] { 999 }, new[] { 0, 1, 2, 3, 4, 5 }));
            battle.EnableRealtime(Enumerable.Range(0, 4).ToDictionary(i => "player" + i, i => "海灵族"), "player0", stage.RerollLimit, false);
            battle.EnableTurnBased();
            return battle;
        }

        [Test]
        public void MermaidWardCoversTheEnemysNextTwoTurns()
        {
            BattleSimulator battle = WardFixture();
            BattleUnit mermaid = battle.Units.First(u => u.Side == BattleSide.Player);
            BattleUnit enemy = battle.Units.First(u => u.Side == BattleSide.Enemy);
            for (int guard = 0; guard < 600 && battle.Outcome == BattleOutcome.Ongoing && !battle.IsUltimateReady(mermaid); guard++)
            {
                if (battle.NextTurnActor() == null) break;
                if (battle.TurnActor.Side == BattleSide.Enemy) battle.EnemyTurn(); else battle.AutoTurn(false);
            }
            Assume.That(battle.IsUltimateReady(mermaid), Is.True, "her bar fills");
            // Worst case: cast at the very start of a round, so the enemy (slowest) acts last in it.
            Assert.That(battle.TryCastUltimateNow(mermaid, UltimateTimingGrade.Miss), Is.True);
            UnitStatus ward = battle.StatusesOf(mermaid).FirstOrDefault(s => s.Kind == UnitStatusKind.Reduction);
            Assert.That(ward.Rounds, Is.EqualTo(BattleSimulator.MermaidWardRounds), "the badge reads 2回合 right after the cast");
            int wardedEnemyTurns = 0;
            for (int guard = 0; guard < 40 && battle.Outcome == BattleOutcome.Ongoing; guard++)
            {
                BattleUnit actor = battle.NextTurnActor();
                if (actor == null) break;
                if (actor == enemy)
                {
                    if (!battle.StatusesOf(mermaid).Any(s => s.Kind == UnitStatusKind.Reduction)) break;
                    wardedEnemyTurns++;
                    battle.EnemyTurn();
                }
                else battle.AutoTurn(false);
            }
            Assert.That(wardedEnemyTurns, Is.GreaterThanOrEqualTo(BattleSimulator.MermaidWardRounds),
                "2回合 = the enemy's next two turns hit the ward");
        }

        [Test]
        public void MermaidCaptainNoLongerWeakensEnemies()
        {
            BattleSimulator battle = TurnBattle("stage-1-1");
            BattleUnit mermaid = battle.Units.FirstOrDefault(u => u.Side == BattleSide.Player && u.Alive && battle.RaceOf(u) == CombatRace.Mermaid);
            Assume.That(mermaid, Is.Not.Null, "the starter team has a 人鱼 member");
            if (battle.CurrentLeaderId != mermaid.Id) Assert.That(battle.TrySwapCaptain(mermaid.Id, out _), Is.True);
            Assert.That(battle.TideSuppressionActive, Is.False);
            BattleUnit enemy = battle.Units.First(u => u.Side == BattleSide.Enemy && u.Alive);
            Assert.That(battle.StatusesOf(enemy).Any(s => s.Kind == UnitStatusKind.TideSuppressed), Is.False);
        }

        [Test]
        public void RoundsRemainingCountsWholeRoundsInTurnMode()
        {
            BattleSimulator battle = TurnBattle("stage-1-1");
            int round = battle.RoundsToMilliseconds(1);
            Assert.That(round, Is.GreaterThan(BattleSimulator.TurnSliceMilliseconds), "a round spends one slice per living unit");
            Assert.That(battle.RoundsToMilliseconds(2), Is.EqualTo(round * 2));
        }
    }
}
