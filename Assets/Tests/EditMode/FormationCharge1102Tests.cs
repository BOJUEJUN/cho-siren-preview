using System;
using System.Collections.Generic;
using System.Linq;
using ChoSiren.Systems.Dice;
using ChoSiren.Systems.Tactics;
using NUnit.Framework;
using UnityEngine;

namespace ChoSiren.Tests
{
    public sealed class FormationCharge1102Tests
    {
        bool had; string save;
        [SetUp] public void Before() { had = PlayerPrefs.HasKey(GameModel.SaveKey); save = PlayerPrefs.GetString(GameModel.SaveKey); PlayerPrefs.DeleteKey(GameModel.SaveKey); }
        [TearDown] public void After() { if (had) PlayerPrefs.SetString(GameModel.SaveKey, save); else PlayerPrefs.DeleteKey(GameModel.SaveKey); }
        static BattleSimulator NewBattle()
        {
            var model = new GameModel(() => new DateTime(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc));
            var battle = model.StartStageBattle("stage-1-1", 7, out string error);
            Assert.That(battle, Is.Not.Null, error);
            battle.EnableRealtime(GameModel.Members.ToDictionary(m => m.Id, m => m.Race), null, 3, false);
            battle.EnableTurnBased(); battle.BattleDice.NeutralizeForBalanceAudit1101();
            return battle;
        }
        [TestCase(0, 12000)] [TestCase(1, 15000)] [TestCase(2, 20000)] [TestCase(3, 30000)]
        public void EveryPositionChargesAtItsAuthoredTime(int slot, int milliseconds)
        {
            var b = NewBattle(); var member = b.FindUnit(b.FormationOrder1102[slot]);
            b.AdvanceFormationCharge1102(milliseconds - 1);
            Assert.That(b.EnergyOf(member), Is.EqualTo(99));
            b.AdvanceFormationCharge1102(1); Assert.That(b.EnergyOf(member), Is.EqualTo(100));
            Assert.That(b.ChargeSeconds1102(member), Is.EqualTo(milliseconds / 1000d));
        }
        [Test]
        public void BigStraightOnlyAcceleratesTheCaptainAboutHalfASecond()
        {
            var b = NewBattle(); var captain = b.FindUnit(b.CurrentLeaderId);
            typeof(DiceTurn).GetProperty("Hand").GetSetMethod(true).Invoke(b.BattleDice,
                new object[] { DiceRules.Evaluate(new[] { 1, 2, 3, 4, 5, 6 }) });
            Assert.That(b.ChargeSeconds1102(captain), Is.EqualTo(12d/1.04d).Within(.001));
            b.AdvanceFormationCharge1102(11538); Assert.That(b.EnergyOf(captain), Is.EqualTo(99));
            b.AdvanceFormationCharge1102(1); Assert.That(b.EnergyOf(captain), Is.EqualTo(100));
            foreach (var member in b.Units.Where(u => u.Side == BattleSide.Player && u.Id != captain.Id))
                Assert.That(b.FormationChargeBonus1102(member), Is.EqualTo(1000));
        }
        [Test]
        public void ChangingCaptainDoesNotMoveOrRefillMembersAndDraggingKeepsEarnedEnergy()
        {
            var b = NewBattle(); int[] order = b.FormationOrder1102.ToArray();
            b.AdvanceFormationCharge1102(6000);
            var before = order.Select(id => b.EnergyOf(b.FindUnit(id))).ToArray();
            Assert.That(b.TrySwapCaptain(order[2], out string message), Is.True, message);
            CollectionAssert.AreEqual(order, b.FormationOrder1102);
            CollectionAssert.AreEqual(before, order.Select(id => b.EnergyOf(b.FindUnit(id))).ToArray());
            Assert.That(b.SwapFormationPositions1102(order[0], order[3]), Is.True);
            Assert.That(b.CurrentLeaderId, Is.EqualTo(order[2]));
            Assert.That(b.ChargeRank(b.FindUnit(order[3])), Is.Zero);
            CollectionAssert.AreEqual(before, order.Select(id => b.EnergyOf(b.FindUnit(id))).ToArray());
            b.AdvanceFormationCharge1102(1200);
            Assert.That(b.EnergyOf(b.FindUnit(order[3])), Is.EqualTo(before[3] + 10));
            Assert.That(b.EnergyOf(b.FindUnit(order[0])), Is.EqualTo(before[0] + 4));
        }
        [Test]
        public void FrequentSmallTicksDoNotLoseFractions()
        {
            var b = NewBattle(); var first = b.FindUnit(b.FormationOrder1102[0]);
            for (int i = 0; i < 12000; i++) b.AdvanceFormationCharge1102(1);
            Assert.That(b.EnergyOf(first), Is.EqualTo(100));
            Assert.Throws<ArgumentOutOfRangeException>(() => b.AdvanceFormationCharge1102(-1));
        }
    }
}
