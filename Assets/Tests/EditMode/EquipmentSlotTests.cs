using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace ChoSiren.Tests
{
    public sealed class EquipmentSlotTests
    {
        private readonly DateTime now = new DateTime(2026, 10, 12, 12, 0, 0);
        [SetUp] public void Setup() { PlayerPrefs.DeleteKey(GameModel.SaveKey); PlayerPrefs.DeleteKey(GameModel.LegacySaveKey); }
        [TearDown] public void Teardown() { Setup(); }

        [Test] public void EveryAccessoryFitsExactlyOneOfFourSlots()
        {
            Assert.AreEqual(4, GameModel.EquipmentSlotCount);
            for (int i = 0; i < GameModel.AccessoryNames.Length; i++)
                Assert.That(GameModel.AccessorySlot(i), Is.InRange(0, 3), GameModel.AccessoryNames[i]);
            for (int slot = 0; slot < 4; slot++)
                Assert.Greater(Enumerable.Range(0, GameModel.AccessoryNames.Length).Count(i => GameModel.AccessorySlot(i) == slot), 0);
        }

        [Test] public void SameSlotReplacesAndMemberWearsAtMostFour()
        {
            var model = new GameModel(() => now);
            for (int i = 0; i < GameModel.AccessoryNames.Length; i++) if (!model.Save.OwnedAccessories.Contains(i)) model.Save.OwnedAccessories.Add(i);
            for (int i = 0; i < GameModel.AccessoryNames.Length; i++) model.EquipAccessoryForMember(0, i, out _);
            int[] worn = model.EquippedAccessoriesFor(0);
            Assert.LessOrEqual(worn.Length, 4);
            CollectionAssert.AllItemsAreUnique(worn.Select(GameModel.AccessorySlot));
            int neck = Enumerable.Range(0, GameModel.AccessoryNames.Length).First(i => GameModel.AccessoryCategory(i) == "项链");
            int charm = Enumerable.Range(0, GameModel.AccessoryNames.Length).First(i => GameModel.AccessoryCategory(i) == "挂饰");
            model.EquipAccessoryForMember(1, neck, out _);
            model.EquipAccessoryForMember(1, charm, out _);
            Assert.AreEqual(charm, model.EquippedInSlot(1, 1));
            CollectionAssert.DoesNotContain(model.EquippedAccessoriesFor(1), neck);
        }

        [Test] public void OldSavesWithTwoItemsInOneSlotKeepTheStronger()
        {
            var model = new GameModel(() => now);
            int ring = Enumerable.Range(0, GameModel.AccessoryNames.Length).First(i => GameModel.AccessoryCategory(i) == "戒指");
            int bracelet = Enumerable.Range(0, GameModel.AccessoryNames.Length).Last(i => GameModel.AccessoryCategory(i) == "手环");
            string id = GameModel.Members[0].Id;
            model.Save.OwnedAccessories.Add(ring); model.Save.OwnedAccessories.Add(bracelet);
            model.Save.MemberAccessories.Clear();
            model.Save.MemberAccessories.Add(new MemberAccessoryBinding { MemberId = id, Accessory = ring });
            model.Save.MemberAccessories.Add(new MemberAccessoryBinding { MemberId = id, Accessory = bracelet });
            PlayerPrefs.SetString(GameModel.SaveKey, JsonUtility.ToJson(model.Save));
            var reloaded = new GameModel(() => now);
            Assert.AreEqual(1, reloaded.EquippedAccessoriesFor(0).Count(i => GameModel.AccessorySlot(i) == 2));
        }

        [Test] public void QuickEquipFillsFreeSlotsWithoutStealingAndUnequipAllClears()
        {
            var model = new GameModel(() => now);
            for (int i = 0; i < GameModel.AccessoryNames.Length; i++) if (!model.Save.OwnedAccessories.Contains(i)) model.Save.OwnedAccessories.Add(i);
            int ear = Enumerable.Range(0, GameModel.AccessoryNames.Length).First(i => GameModel.AccessorySlot(i) == 0);
            model.EquipAccessoryForMember(1, ear, out _);
            Assert.Greater(model.QuickEquip(0, out _), 0);
            Assert.AreEqual(4, model.EquippedAccessoriesFor(0).Length);
            Assert.AreEqual(1, model.AccessoryOwner(ear));
            Assert.AreEqual(0, model.QuickEquip(0, out _));
            Assert.AreEqual(4, model.UnequipAll(0, out _));
            Assert.IsEmpty(model.EquippedAccessoriesFor(0));
            Assert.AreEqual(1, model.AccessoryOwner(ear));
        }
    }
}
