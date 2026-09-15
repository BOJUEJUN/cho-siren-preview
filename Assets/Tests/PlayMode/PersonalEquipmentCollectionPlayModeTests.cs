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
            Assert.That(Require("EquipmentReferenceBoard038").GetComponent<Image>().preserveAspect, Is.True);
            for (int page = 0; page < 6; page++)
            {
                int[] expected = Enumerable.Range(page * 12, Mathf.Min(12, 66 - page * 12)).ToArray();
                Assert.That(VisibleIds(), Is.EqualTo(expected), "饰品分页不能重复或漏掉条目");
                Assert.That(Label("EquipmentPageCount"), Is.EqualTo($"{page + 1}/6"));
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
                Assert.That(Label("EquipmentPageCount"), Is.EqualTo($"1/{Mathf.Max(1, Mathf.CeilToInt(matches.Length / 12f))}"));
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
            Assert.That(Label("EquipmentPageCount"), Is.EqualTo("1/1"));
            string emptyCategory = GameModel.AccessoryCategories.First(c => c != "全部" &&
                !model.Save.OwnedAccessories.Any(i => GameModel.AccessoryCategory(i) == c));
            for (int guard=0; guard<GameModel.AccessoryCategories.Length; guard++)
            {
                if (Require("EquipmentCategoryFilter").GetComponentInChildren<Text>().text == "类别：" + emptyCategory) break;
                Click("EquipmentCategoryFilter"); yield return null;
            }
            Assert.That(VisibleIds(), Is.Empty);
            Assert.That(Label("EquipmentEmpty"), Is.EqualTo("暂无符合筛选条件的饰品"));
            Assert.That(Label("EquipmentPageCount"), Is.EqualTo("1/1"));
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
            Assert.That(Label("EquipmentCompareHeading"), Is.EqualTo("队伍战力 · 未装备→已装备"));
            Assert.That(Label("AccessoryDelta-3"), Is.EqualTo($"{baselinePower:N0}→{expectedPower:N0}"));
            Assert.That(Label("AccessoryPowerChange"), Is.EqualTo($"已生效 +{expectedPower - baselinePower:N0}"),
                "装备状态应解释已生效收益，不可伪装成卸下后的负变化");
            Assert.That(Label("PlayerLevel"), Is.EqualTo($"战力 {model.TeamPower:N0}"));
            Assert.That(Require("QuickEquip").GetComponentInChildren<Text>().text, Is.EqualTo("卸下"));
            Assert.That(new GameModel().EquippedAccessoryFor(member), Is.EqualTo(1));
            Click("QuickEquip");
            yield return null;
            Assert.That(model.EquippedAccessoryFor(member), Is.EqualTo(-1));
            Assert.That(Label("PlayerLevel"), Is.EqualTo($"战力 {baselinePower:N0}"));
            Assert.That(Label("EquipmentCompareHeading"), Is.EqualTo("队伍战力 · 当前→装备后"));
        }

        [UnityTest]
        public IEnumerator MemberSwitchRevealsSelectedCardPageSoActionsStayReachable()
        {
            // 二号成员装备第 6 页的饰品；再找一个无装备成员验证选中回退。
            int equipped = model.Save.UnlockedMembers.First(m => m != model.Save.Team[0]);
            model.Save.OwnedAccessories.Add(60);
            Assert.That(model.EquipAccessoryForMember(equipped, 60, out string equipMessage), Is.True, equipMessage);
            int bare = model.Save.UnlockedMembers.First(m => model.EquippedAccessoryFor(m) < 0);
            Assert.That(bare, Is.Not.EqualTo(equipped));

            for (int page = 0; page < 5; page++) { Click("EquipmentNextPage"); yield return null; }
            Assert.That(VisibleIds(), Is.EqualTo(Enumerable.Range(60, 6).ToArray()));

            // 切到无装备成员：选中回退到第 0 件，网格必须跟到该卡所在页，
            // 否则详情条显示的饰品没有 QuickEquip/QuickUpgrade 任何操作入口。
            Click("EquipmentChooseMember"); yield return null;
            Click("PickMember-" + bare); yield return null;
            Assert.That(Label("EquipmentMemberName"), Does.StartWith(GameModel.Members[bare].Name));
            Assert.That(VisibleIds(), Does.Contain(0), "换人后选中卡必须留在可见页，否则没有穿戴/卸下入口。");
            Assert.That(GameObject.Find("QuickEquip"), Is.Not.Null, "选中卡可见时必须提供快捷操作。");

            // 切到穿戴着第 6 页饰品的成员：网格必须跟到第 6 页并给出「卸下」。
            Click("EquipmentChooseMember"); yield return null;
            Click("PickMember-" + equipped); yield return null;
            Assert.That(VisibleIds(), Does.Contain(60));
            Assert.That(Label("EquipmentItemName"), Is.EqualTo(GameModel.AccessoryNames[60]));
            Assert.That(Require("QuickEquip").GetComponentInChildren<Text>().text, Is.EqualTo("卸下"));

            // 离开饰品页再返回：成员、选中项与所在页保持一致。
            Click("Nav-lobby"); yield return null;
            Click("Nav-accessory"); yield return null;
            Assert.That(Label("EquipmentMemberName"), Does.StartWith(GameModel.Members[equipped].Name));
            Assert.That(VisibleIds(), Does.Contain(60));
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
