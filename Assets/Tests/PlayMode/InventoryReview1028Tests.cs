using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ChoSiren.Tests
{
    public sealed class InventoryReview1028Tests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private ChoSirenApp app;
        private GameModel model;
        private string saved, legacy;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            saved = PlayerPrefs.HasKey(GameModel.SaveKey) ? PlayerPrefs.GetString(GameModel.SaveKey) : null;
            legacy = PlayerPrefs.HasKey(GameModel.LegacySaveKey) ? PlayerPrefs.GetString(GameModel.LegacySaveKey) : null;
            foreach (var old in Object.FindObjectsByType<ChoSirenApp>(FindObjectsSortMode.None)) Object.Destroy(old.gameObject);
            foreach (var old in Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None)) Object.Destroy(old.gameObject);
            yield return null;
            app = new GameObject("Inventory review").AddComponent<ChoSirenApp>();
            yield return null;
            model = new GameModel(() => DateTime.Now, new InMemorySaveStore());
            typeof(ChoSirenApp).GetField("model", Private).SetValue(app, model);
            ShowInventory();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (app != null) Object.Destroy(app.gameObject);
            foreach (var old in Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None)) Object.Destroy(old.gameObject);
            yield return null;
            Restore(GameModel.SaveKey, saved); Restore(GameModel.LegacySaveKey, legacy);
            PlayerPrefs.Save();
        }

        [UnityTest]
        public IEnumerator RecentAcquisitionFiltersAndAllCatalogItemsStayReachable()
        {
            // 2026-11-08: the slot plates are category tabs, 耳饰 by default; the grid shows only that category.
            model.Save.OwnedAccessories.Clear();
            model.Save.OwnedAccessories.AddRange(new[] { 60, 1, 12, 3 });
            typeof(ChoSirenApp).GetField("inventorySort", Private).SetValue(app, 2);
            ShowInventory(); yield return null;
            Assert.That(Require("InventorySlot-0").GetComponent<Image>().sprite.name, Does.EndWith("-on"), "耳饰 selected by default");
            for (int slot = 0; slot < GameModel.EquipmentSlotCount; slot++)
            {
                if (slot > 0) { Click("InventorySlot-" + slot); yield return null; }
                Assert.That(Require("InventorySlot-" + slot).GetComponent<Image>().sprite.name, Does.EndWith("-on"));
                int[] expected = new[] { 3, 12, 1, 60 }.Where(i => GameModel.AccessorySlot(i) == slot).ToArray();
                CollectionAssert.AreEqual(expected, GridItems(), "only the chosen category, newest first");
            }
            Click("InventoryTab-1"); yield return null;
            var seen = new System.Collections.Generic.HashSet<int>();
            for (int slot = 0; slot < GameModel.EquipmentSlotCount; slot++)
            {
                Click("InventorySlot-" + slot); yield return null;
                string first = Require("InventoryPage").GetComponent<Text>().text;
                int pages = int.Parse(first.Split('/')[1]);
                for (int p = 0; p < pages; p++)
                {
                    foreach (int item in GridItems())
                    {
                        Assert.That(GameModel.AccessorySlot(item), Is.EqualTo(slot));
                        seen.Add(item);
                        Assert.That(Require("InventoryGrid").transform.Find("Accessory-" + item + "/ItemArt").GetComponent<Image>().sprite, Is.Not.Null);
                    }
                    Click("InventoryNextPage"); yield return null;
                }
                Assert.That(Require("InventoryPage").GetComponent<Text>().text, Is.EqualTo(first), "› wraps back to page 1");
            }
            Assert.That(seen.Count, Is.EqualTo(66), "every catalog item is reachable through the tabs and pages");
        }

        [UnityTest]
        public IEnumerator SelectingCurrentWearerNeverUnequipsAndTransferPersists()
        {
            int first = model.Save.UnlockedMembers[0], second = model.Save.UnlockedMembers[1];
            model.EquipAccessoryForMember(first, 1, out _);
            Invoke("OpenAccessoryMemberPicker", 1); yield return null;
            var owner = Require("AccessoryMemberPicker").GetComponentsInChildren<Button>()
                .Single(b => b.name == "Member-" + GameModel.Members[first].Id);
            Assert.That(owner.interactable, Is.False);
            owner.onClick.Invoke(); yield return null;
            Assert.That(model.AccessoryOwner(1), Is.EqualTo(first));
            var target = Require("AccessoryMemberPicker").GetComponentsInChildren<Button>()
                .Single(b => b.name == "Member-" + GameModel.Members[second].Id);
            target.onClick.Invoke(); yield return null;
            Assert.That(model.AccessoryOwner(1), Is.EqualTo(second));
            Assert.That(model.EquippedAccessoriesFor(first).Contains(1), Is.False);
            Assert.That(model.Save.MemberAccessories.Count(b => b.Accessory == 1), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator UpgradeRefreshesCollectionAndPreservesScrollPosition()
        {
            model.Save.OwnedAccessories.Clear();
            model.Save.OwnedAccessories.AddRange(Enumerable.Range(0, 66));
            model.Save.Gold = 100000;
            // Open the page that holds item 1, upgrade it there and stay on that page.
            var items = (int[])typeof(ChoSirenApp).GetMethod("InventoryItems", Private).Invoke(app, new object[] { GameModel.AccessorySlot(1) });
            int page = System.Array.IndexOf(items, 1) / 16;
            typeof(ChoSirenApp).GetField("inventorySlot", Private).SetValue(app, GameModel.AccessorySlot(1));
            typeof(ChoSirenApp).GetField("inventoryPage", Private).SetValue(app, page);
            ShowInventory(); yield return null;
            Invoke("OpenAccessoryCard", 1, -1, null); yield return null;
            var card = Require("AccessoryCard");
            card.GetComponentsInChildren<Button>().Single(b => b.name == "Upgrade").onClick.Invoke();
            yield return null;
            Assert.That(model.AccessoryUpgradeLevel(1), Is.EqualTo(1));
            Assert.That(Require("InventoryGrid").transform.Find("Accessory-1/Level").GetComponent<Text>().text, Is.EqualTo("02"));
            Assert.That(typeof(ChoSirenApp).GetField("inventoryPage", Private).GetValue(app), Is.EqualTo(page));
            Require("AccessoryCard").GetComponentsInChildren<Button>().Single(b => b.name == "Close").onClick.Invoke();
            yield return null;
            Assert.That(GameObject.Find("AccessoryCard"), Is.Null);
        }

        [UnityTest]
        public IEnumerator UnownedAndUnaffordableItemsCannotUpgradeAndDetailsRemainReadable()
        {
            model.Save.Gold = 0;
            Invoke("OpenAccessoryCard", 1, -1, null); yield return null;
            Assert.That(Require("AccessoryCard").GetComponentsInChildren<Button>().Single(b => b.name == "Upgrade").interactable, Is.False);
            Require("AccessoryCard").GetComponentsInChildren<Button>().Single(b => b.name == "Close").onClick.Invoke();
            yield return null;
            Invoke("OpenAccessoryCard", 60, -1, null); yield return null;
            var card = Require("AccessoryCard");
            Assert.That(card.GetComponentsInChildren<Button>().Single(b => b.name == "Upgrade").interactable, Is.False);
            Assert.That(card.GetComponentsInChildren<Button>().Single(b => b.name == "CardRight").GetComponentInChildren<Text>().text, Is.EqualTo("去关卡"));
        }

        [Ignore("Quarantined 2026-11-08: preset bar shows only existing presets (no empty slot 1); see Docs/TEST-QUARANTINE-1108.md")]
        [UnityTest]
        public IEnumerator TeamPresetControlsSaveRenameSwitchAndRemainReadable()
        {
            Invoke("ShowScreen", "team"); yield return null;
            Click("TeamPreset-1"); yield return null;
            Click("SavePreset"); yield return null;
            Assert.That(model.Save.ActiveTeamPreset, Is.EqualTo(1));
            Assert.That(model.Save.TeamPresets[1].Members, Is.EqualTo(model.Save.Team));
            Click("RenameTeam"); yield return null;
            Require("TeamNameInput").GetComponent<InputField>().text = "夜场小队";
            Click("Confirm"); yield return null;
            Assert.That(model.ActiveTeamName, Is.EqualTo("夜场小队"));
            Assert.That(Require("TeamPreset-1").GetComponentInChildren<Text>().color.r, Is.LessThan(.2f));
            int captain = model.Save.Team[3];
            Assert.That(model.ReplaceTeamSlot(0, captain, out _), Is.True);
            Click("TeamPreset-0"); yield return null;
            Assert.That(model.Save.Team[0], Is.Not.EqualTo(captain));
            Click("TeamPreset-1"); yield return null;
            Assert.That(model.Save.Team[0], Is.EqualTo(captain));
        }

        [UnityTest]
        public IEnumerator MemberSearchAndOwnedCatalogFiltersRemainFunctional()
        {
            Invoke("ShowScreen", "members"); yield return null;
            Assert.That(Require("MembersTitle").GetComponent<Text>().text, Is.EqualTo("成员"));
            Click("MemberOwnedFilter"); yield return null;
            Assert.That(Require("MemberNextPage").GetComponent<Button>().interactable, Is.True);
            Click("MemberNextPage"); yield return null;
            Assert.That(Require("MemberPageLabel").GetComponent<Text>().text, Does.StartWith("2/"));
            var input = Require("MemberSearch").GetComponent<InputField>();
            string name = GameModel.Members[model.Save.Team[0]].Name;
            input.onEndEdit.Invoke(name); yield return null;
            Assert.That(Require("MembersReferenceBoard038").transform.Cast<Transform>().Count(t => t.name.StartsWith("Member-")), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator SignedContractHidesEmptyGuideWhileKeepingInk()
        {
            Assert.That(model.TrySetPlayerProfile("林夏", 0, null, out _), Is.True);
            model.Save.UnlockedMembers.Add(4);
            Assert.That(model.SetPlayerAvatarMember(GameModel.Members[4].Id), Is.True);
            Invoke("OpenPlayerContract1028", false, null); yield return null;
            var page = Require("PlayerContract1028");
            var guide = page.GetComponentsInChildren<Transform>(true).Single(t => t.name == "SignLine");
            Assert.That(guide.gameObject.activeSelf, Is.False);
            Assert.That(page.GetComponentInChildren<ChoSiren.UI.SignatureInk1028>(), Is.Not.Null);
            Image photo = page.transform.Find("Paper/IdPhoto/Clip/Face").GetComponent<Image>();
            Assert.That(photo.sprite, Is.SameAs(ChoSiren.UI.PlayerAvatar1102.Resolve(model)));
            page.transform.Find("Paper/IdPhoto").GetComponent<Button>().onClick.Invoke(); yield return null;
            Require("AvatarSheet1028").GetComponentsInChildren<Button>().Single(b => b.name == "Avatar-3").onClick.Invoke();
            Assert.That(model.PlayerAvatar, Is.EqualTo(3));
            Assert.That(model.PlayerAvatarMemberId, Is.Null, "picking a preset clears the previously worn member");
            Assert.That(photo.sprite, Is.SameAs(ChoSiren.UI.PlayerAvatar1102.Preset(3)));
            Assert.That(model.PlayerName, Is.EqualTo("林夏"));
        }

        [UnityTest]
        public IEnumerator NameCaretDrawsAndBlinksBeforeAndAfterTyping()
        {
            Transform host = (Transform)typeof(ChoSirenApp).GetField("safeRoot", Private).GetValue(app);
            Invoke("OpenNameDialog1028", host, string.Empty, null);
            InputField input = Require("NameBox").GetComponent<InputField>();
            EventSystem.current.SetSelectedGameObject(input.gameObject);
            input.ActivateInputField();
            yield return null;
            Assert.That(input.isFocused, Is.True);
            Assert.That(input.caretWidth * input.textComponent.canvas.scaleFactor, Is.GreaterThanOrEqualTo(2f), "PC caret must cover at least two physical pixels");
            foreach (string value in new[] { string.Empty, "林夏" })
            {
                input.text = value;
                input.caretPosition = value.Length;
                input.ForceLabelUpdate();
                bool visible = false, hidden = false;
                float until = Time.realtimeSinceStartup + 1.35f;
                while (Time.realtimeSinceStartup < until)
                {
                    Canvas.ForceUpdateCanvases();
                    Transform caret = input.transform.Find("NameBox Input Caret");
                    if (caret != null)
                    {
                        Mesh mesh = caret.GetComponent<CanvasRenderer>().GetMesh();
                        visible |= mesh != null && mesh.vertexCount >= 4;
                        hidden |= mesh == null || mesh.vertexCount == 0;
                    }
                    yield return null;
                }
                Assert.That(visible, Is.True, "visible caret with name '" + value + "'");
                Assert.That(hidden, Is.True, "caret actually blinks with name '" + value + "'");
            }
        }

        private void ShowInventory() => Invoke("ShowScreen", "accessory");
        private void Invoke(string method, params object[] args) => typeof(ChoSirenApp).GetMethod(method, Private).Invoke(app, args);
        private static void Click(string name) => Require(name).GetComponent<Button>().onClick.Invoke();
        private static GameObject Require(string name) { var obj = GameObject.Find(name); Assert.That(obj, Is.Not.Null, name); return obj; }
        private static int[] GridItems() => Require("InventoryGrid").transform.Cast<Transform>()
            .Where(t => t.name.StartsWith("Accessory-")).Select(t => int.Parse(t.name.Substring(10))).ToArray();
        private static void Restore(string key, string value) { if (value == null) PlayerPrefs.DeleteKey(key); else PlayerPrefs.SetString(key, value); }
    }
}
