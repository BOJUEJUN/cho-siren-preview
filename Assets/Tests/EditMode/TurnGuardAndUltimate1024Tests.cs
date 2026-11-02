using System;
using System.Collections.Generic;
using System.Linq;
using ChoSiren.Systems.Tactics;
using NUnit.Framework;
using UnityEngine;

namespace ChoSiren.Tests
{
    /// <summary>Guard blocks one whole hit; the 11-02 charge rules preserve other members' energy.</summary>
    public sealed class TurnGuardAndUltimate1024Tests
    {
        private static readonly DateTime Now = new DateTime(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc);

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

        [Test]
        public void GuardBlocksOneWholeHitPerMemberAndStaysUntilHit()
        {
            BattleSimulator battle = TurnBattle("stage-1-1");
            Assert.That(battle.TryTacticalGuard(), Is.True);
            var players = battle.Units.Where(u => u.Side == BattleSide.Player && u.Alive).ToList();
            Assert.That(battle.GuardedCount, Is.EqualTo(players.Count));
            var hp = players.ToDictionary(p => p.Id, p => p.Hp);
            int guardBreaks = 0, enemyTurns = 0;
            while (battle.Outcome == BattleOutcome.Ongoing && enemyTurns < 3)
            {
                BattleUnit actor = battle.NextTurnActor();
                if (actor == null) break;
                int logStart = battle.Log.Count;
                if (actor.Side == BattleSide.Enemy) { battle.EnemyTurn(); enemyTurns++; }
                else battle.AutoTurn(false);
                for (int i = logStart; i < battle.Log.Count; i++)
                {
                    BattleEvent e = battle.Log[i];
                    if (e.Kind == BattleEventKind.Buff && e.SkillId == BattleSimulator.GuardBreakSkillId)
                    {
                        guardBreaks++;
                        BattleUnit target = battle.FindUnit(e.TargetId);
                        Assert.That(battle.GuardRemaining(target), Is.Zero, "a broken shield is gone");
                    }
                }
            }
            Assert.That(guardBreaks, Is.GreaterThan(0), "enemy hits land on shields");
            Assert.That(battle.GuardedCount, Is.EqualTo(players.Count - guardBreaks), "untouched members keep their shield");
            // Time passing alone never removes a shield in turn mode.
            Assert.That(battle.TryTacticalGuard(), Is.False, "one 应急守护 per battle");
        }

        [Test]
        public void TappedUltimatePreservesTheOtherBars()
        {
            BattleSimulator battle = TurnBattle("stage-1-1");
            battle.AdvanceFormationCharge1102(12000);
            BattleUnit caster = battle.FindUnit(battle.FormationOrder1102[0]);
            var before = battle.Units.Where(u => u.Side == BattleSide.Player && u != caster).ToDictionary(u => u.Id, battle.EnergyOf);
            Assert.That(battle.TryCastUltimateNow(caster, UltimateTimingGrade.Miss), Is.True);
            foreach (BattleUnit other in battle.Units.Where(u => u.Side == BattleSide.Player && u != caster))
                Assert.That(battle.EnergyOf(other), Is.EqualTo(before[other.Id]), "Only the caster spends energy");
            Assert.That(battle.EnergyOf(caster), Is.Zero);
        }
    }
}
