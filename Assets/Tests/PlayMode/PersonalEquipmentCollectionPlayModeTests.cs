using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ChoSiren.Tests
{
    public sealed class PersonalEquipmentCollectionPlayModeTests
    {
        private ChoSirenApp app;
        private GameModel model;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            ClearSave();
            DestroyAll<ChoSirenApp>(); DestroyAll<EventSystem>();
            yield return null;
            app = new GameObject("Personal equipment collection test").AddComponent<ChoSirenApp>();
            yield return null;
            model = (GameModel)typeof(ChoSirenApp).GetField("model", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(app);
            Assert.That(model, Is.Not.Null);
            Click("Nav-accessory");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            DestroyAll<ChoSirenApp>(); DestroyAll<EventSystem>();
            yield return null;
            ClearSave();
        }

        [UnityTest]
        public IEnumerator AllSixtySixItemsHaveImagesAndAreReachableAcrossSixTwelveItemPages()
        {
            Assert.That(GameModel.AccessoryNames.Length, Is.EqualTo(66));
            Assert.That(Require("EquipmentScroll").GetComponent<ScrollRect>().scrollSensitivity, Is.GreaterThanOrEqualTo(32));
            for (int page = 0; page < 6; page++)
            {
                int[] expected = Enumerable.Range(page * 12, Mathf.Min(12, 66 - page * 12)).ToArray();
                Assert.That(VisibleIds(), Is.EqualTo(expected), "饰品分页不能重复或漏掉条目");
                Assert.That(Label("EquipmentPageCount"), Is.EqualTo($"{page + 1} / 6 · 共 66 件"));
                Assert.That(Require("EquipmentPreviousPage").GetComponent<Button>().interactable, Is.EqualTo(page > 0));
                Assert.That(Require("EquipmentNextPage").GetComponent<Button>().interactable, Is.EqualTo(page < 5));
                foreach (int item in expected)
                {
                    Image art = Require("Accessory-" + item).transform.Find("ItemArt").GetComponent<Image>();
                    Assert.That(art.sprite, Is.Not.Null, "饰品图缺失：" + GameModel.AccessoryNames[item]);
                    Assert.That(art.sprite.texture, Is.Not.Null);
                    Assert.That(art.preserveAspect, Is.True);
                    if (item >= 12)
                    {
                        Sprite authored = Resources.Load<Sprite>(GameModel.AccessoryCollectionResourcePath(item));
                        Assert.That(authored, Is.Not.Null, "54件新素材必须真正落盘，不以占位图通过：" + item);
                        Assert.That(art.sprite.texture, Is.EqualTo(authored.texture));
                    }
                }
                if (page < 5) { Click("EquipmentNextPage"); yield return null; }
            }
            Click("Accessory-65");
            yield return null;
            Assert.That(Label("EquipmentItemName"), Is.EqualTo(GameModel.AccessoryNames[65]));
            Assert.That(Require("EquipmentSelectedArt").GetComponent<Image>().sprite.texture,
                Is.EqualTo(Resources.Load<Sprite>(GameModel.AccessoryCollectionResourcePath(65)).texture));
            Click("EquipmentPreviousPage");
            yield return null;
            Assert.That(VisibleIds(), Is.EqualTo(Enumerable.Range(48,12).ToArray()));
        }

        [UnityTest]
        public IEnumerator CategoryCycleFiltersActualCatalogAndResetsOldPageIndex()
        {
            for (int page=0; page<5; page++) { Click("EquipmentNextPage"); yield return null; }
            string[] categories = GameModel.AccessoryCategories.Where(c => c != "全部").Concat(new[] { "全部" }).ToArray();
            foreach (string category in categories)
            {
                Click("EquipmentCategoryFilter");
                yield return null;
                Assert.That(Require("EquipmentCategoryFilter").GetComponentInChildren<Text>().text, Is.EqualTo("类别：" + category));
                int[] matches = Enumerable.Range(0,66).Where(i => category == "全部" || GameModel.AccessoryCategory(i) == category).ToArray();
                Assert.That(VisibleIds(), Is.EqualTo(matches.Take(12).ToArray()));
                Assert.That(Label("EquipmentPageCount"), Is.EqualTo($"1 / {Mathf.Max(1, Mathf.CeilToInt(matches.Length / 12f))} · 共 {matches.Length} 件"));
                Assert.That(Require("EquipmentPreviousPage").GetComponent<Button>().interactable, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator OwnedOnlyAndEmptyCategoryRemainSafeWhenSwitchingCharacters()
        {
            Click("EquipmentOwnedFilter");
            yield return null;
            Assert.That(VisibleIds(), Is.EqualTo(model.Save.OwnedAccessories.OrderBy(i=>i).ToArray()));
            Assert.That(VisibleIds().All(model.OwnsAccessory), Is.True);
            Assert.That(Label("EquipmentPageCount"), Is.EqualTo($"1 / 1 · 共 {model.Save.OwnedAccessories.Count} 件"));
            string emptyCategory = GameModel.AccessoryCategories.First(c => c != "全部" &&
                !model.Save.OwnedAccessories.Any(i => GameModel.AccessoryCategory(i) == c));
            for (int guard=0; guard<GameModel.AccessoryCategories.Length; guard++)
            {
                if (Require("EquipmentCategoryFilter").GetComponentInChildren<Text>().text == "类别：" + emptyCategory) break;
                Click("EquipmentCategoryFilter"); yield return null;
            }
            Assert.That(VisibleIds(), Is.Empty);
            Assert.That(Label("EquipmentEmpty"), Is.EqualTo("暂无符合筛选条件的饰品"));
            Assert.That(Label("EquipmentPageCount"), Is.EqualTo("1 / 1 · 共 0 件"));
            Assert.That(Require("EquipmentPreviousPage").GetComponent<Button>().interactable, Is.False);
            Assert.That(Require("EquipmentNextPage").GetComponent<Button>().interactable, Is.False);
            int original = model.Save.Team[0];
            int next = model.Save.UnlockedMembers[(model.Save.UnlockedMembers.IndexOf(original)+1) % model.Save.UnlockedMembers.Count];
            Click("EquipmentChooseMember");
            yield return null;
            Click("PickMember-" + next);
            yield return null;
            Assert.That(VisibleIds(), Is.Empty);
            Assert.That(Label("EquipmentMemberName"), Does.StartWith(GameModel.Members[next].Name));
            Assert.That(Require("EquipmentSelectedArt").GetComponent<Image>().sprite, Is.Not.Null);
            Click("EquipmentChooseMember");
            yield return null;
            Click("PickMember-" + original);
            yield return null;
            Assert.That(Label("EquipmentMemberName"), Does.StartWith(GameModel.Members[original].Name));
            Assert.That(model.Save.MemberAccessories, Is.Empty, "浏览筛选和切换角色不得自动装备或转移饰品");
        }

        [UnityTest]
        public IEnumerator WornEquipmentShowsItsPositiveAppliedBonusAndHeaderTracksRealTeamPower()
        {
            int member = model.Save.Team[0];
            int baselinePower = model.TeamPower;
            int expectedPower = model.TeamPowerWithMemberAccessory(member, 1);
            Assert.That(expectedPower, Is.GreaterThan(baselinePower));
            Assert.That(Label("PlayerLevel"), Is.EqualTo($"战力 {baselinePower:N0}"));
            Click("Accessory-1");
            yield return null;
            Click("QuickEquip");
            yield return null;
            Assert.That(model.EquippedAccessoryFor(member), Is.EqualTo(1));
            Assert.That(Label("EquipmentCompareHeading"), Is.EqualTo("未装备时   →   当前已装备"));
            Assert.That(Label("AccessoryDelta-3"), Is.EqualTo($"{baselinePower:N0}→{expectedPower:N0}"));
            Assert.That(Label("AccessoryPowerChange"), Is.EqualTo($"已生效 · 战力 +{expectedPower - baselinePower:N0}"),
                "装备状态应解释已生效收益，不可伪装成卸下后的负变化");
            Assert.That(Label("PlayerLevel"), Is.EqualTo($"战力 {model.TeamPower:N0}"));
            Assert.That(Require("QuickEquip").GetComponentInChildren<Text>().text, Is.EqualTo("卸下饰品"));
            Assert.That(new GameModel().EquippedAccessoryFor(member), Is.EqualTo(1));
            Click("QuickEquip");
            yield return null;
            Assert.That(model.EquippedAccessoryFor(member), Is.EqualTo(-1));
            Assert.That(Label("PlayerLevel"), Is.EqualTo($"战力 {baselinePower:N0}"));
            Assert.That(Label("EquipmentCompareHeading"), Is.EqualTo("当前     →     装备后预览"));
        }

        private static int[] VisibleIds() => Require("EquipmentBody").GetComponentsInChildren<Button>()
            .Where(b => b.name.StartsWith("Accessory-") && int.TryParse(b.name.Substring(10), out _))
            .Select(b => int.Parse(b.name.Substring(10))).OrderBy(i=>i).ToArray();
        private static string Label(string name) => Require(name).GetComponent<Text>().text;
        private static void Click(string name)
        { Button button=Require(name).GetComponent<Button>(); Assert.That(button.IsInteractable(), Is.True,name); button.onClick.Invoke(); }
        private static GameObject Require(string name)
        { var obj=GameObject.Find(name); Assert.That(obj, Is.Not.Null,name); return obj; }
        private static void DestroyAll<T>() where T:Component
        { foreach(T obj in Object.FindObjectsByType<T>(FindObjectsInactive.Include)) Object.Destroy(obj.gameObject); }
        private static void ClearSave()
        { PlayerPrefs.DeleteKey(GameModel.SaveKey); PlayerPrefs.DeleteKey(GameModel.LegacySaveKey); PlayerPrefs.Save(); }
    }
}
