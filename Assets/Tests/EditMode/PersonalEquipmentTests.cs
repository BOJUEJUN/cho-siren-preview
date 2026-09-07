using System;
using System.Linq;
using ChoSiren.Systems.Tactics;
using NUnit.Framework;
using UnityEngine;

namespace ChoSiren.Tests
{
    public sealed class PersonalEquipmentTests
    {
        private readonly DateTime now = new DateTime(2026, 9, 7, 12, 0, 0);
        [SetUp] public void Setup() { PlayerPrefs.DeleteKey(GameModel.SaveKey); PlayerPrefs.DeleteKey(GameModel.LegacySaveKey); }
        [TearDown] public void Teardown() { Setup(); }
        [Test] public void EquippingOnlyAffectsWearerAndTransferRemovesPreviousBonus()
        {
            var model = new GameModel(() => now);
            int first = model.PowerOf(0), second = model.PowerOf(1);
            Assert.True(model.EquipAccessoryForMember(0, 1, out _));
            Assert.Greater(model.PowerOf(0), first);
            Assert.AreEqual(second, model.PowerOf(1));
            int preview = model.TeamPowerWithMemberAccessory(1, 1);
            Assert.True(model.EquipAccessoryForMember(1, 1, out _));
            Assert.AreEqual(first, model.PowerOf(0));
            Assert.Greater(model.PowerOf(1), second);
            Assert.AreEqual(preview, model.TeamPower);
            Assert.AreEqual(1, model.Save.MemberAccessories.Count);
            var reloaded = new GameModel(() => now);
            Assert.AreEqual(1, reloaded.EquippedAccessoryFor(1));
            Assert.AreEqual(-1, reloaded.EquippedAccessoryFor(0));
            Assert.True(reloaded.EquipAccessoryForMember(1, 1, out _));
            Assert.AreEqual(second, reloaded.PowerOf(1));
        }
        [Test] public void CaptainChangeDoesNotMovePersonalEquipmentAndBattleUsesSameStats()
        {
            var model = new GameModel(() => now);
            model.EquipAccessoryForMember(0, 0, out _);
            model.EquipAccessoryForMember(1, 1, out _);
            model.SetTeamLeader(1, out _);
            Assert.AreEqual(0, model.EquippedAccessoryFor(0));
            Assert.AreEqual(1, model.Save.EquippedAccessory);
            BattleSimulator battle = model.StartStageBattle("stage-1-1", 91, out string reason);
            Assert.NotNull(battle, reason);
            foreach (int member in model.Save.Team)
            {
                var unit = battle.Units.Single(u => u.Definition.Id == GameModel.MemberIdAt(member));
                var stats = model.StatsOf(member);
                Assert.AreEqual(stats.Hp, unit.MaxHp);
                Assert.AreEqual(stats.Attack, unit.BaseAttack);
                Assert.AreEqual(stats.Defense, unit.BaseDefense);
            }
        }
        [Test] public void LegacyPartyItemMigratesOnceWithoutClearingProgressOrDuplicating()
        {
            var save = new GameSave { EquippedAccessory = 2, PersonalEquipmentMigrated = false };
            PlayerPrefs.SetString(GameModel.SaveKey, JsonUtility.ToJson(save));
            var model = new GameModel(() => now);
            Assert.AreEqual(2, model.EquippedAccessoryFor(0));
            Assert.AreEqual(-1, model.EquippedAccessoryFor(1));
            model.SetTeamLeader(1, out _);
            var reloaded = new GameModel(() => now);
            Assert.AreEqual(2, reloaded.EquippedAccessoryFor(0));
            Assert.AreEqual(-1, reloaded.EquippedAccessoryFor(1));
            Assert.AreEqual(1, reloaded.Save.MemberAccessories.Count);
        }
        [Test] public void LockedItemsAndMembersCannotEquipOrSpend()
        {
            var model = new GameModel(() => now);
            string before = JsonUtility.ToJson(model.Save);
            Assert.False(model.EquipAccessoryForMember(0, 5, out _));
            Assert.False(model.EquipAccessoryForMember(10, 0, out _));
            Assert.False(model.EquipAccessoryForMember(0, -1, out _));
            Assert.AreEqual(before, JsonUtility.ToJson(model.Save));
        }
    }
}
