using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ChoSiren.Tests
{
    public sealed class MemberRosterIntegrationPlayModeTests
    {
        private const string SaveKey = "ChoSiren.Save.v1";

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
            DestroyAll<ChoSirenApp>();
            DestroyAll<EventSystem>();
            yield return null;
            new GameObject("CHO-SIREN Member Roster Test").AddComponent<ChoSirenApp>();
            yield return null;
            Click("Nav-members");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            DestroyAll<ChoSirenApp>();
            DestroyAll<EventSystem>();
            yield return null;
            PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
        }

        [UnityTest]
        public IEnumerator MemberRosterPagesThroughEveryRealCatalogEntry()
        {
            Assert.That(GameModel.Members.Length, Is.GreaterThanOrEqualTo(50));
            int firstPageCount = VisibleMemberCards().Length;
            Assert.That(firstPageCount, Is.InRange(
                MemberRosterPagination.MinimumRows * MemberRosterPagination.DefaultColumns,
                MemberRosterPagination.MaximumRows * MemberRosterPagination.DefaultColumns));
            Assert.That(firstPageCount % MemberRosterPagination.DefaultColumns, Is.Zero);
            Assert.That(GameObject.Find("Member-" + GameModel.Members[0].Id), Is.Not.Null);

            Click("MemberNextPage");
            yield return null;
            Assert.That(GameObject.Find("Member-" + GameModel.Members[0].Id), Is.Null);
            Assert.That(GameObject.Find("Member-" + GameModel.Members[firstPageCount].Id), Is.Not.Null);

            int pageCount = (GameModel.Members.Length + firstPageCount - 1) / firstPageCount;
            for (int page = 2; page < pageCount; page++)
            {
                Click("MemberNextPage");
                yield return null;
            }

            Assert.That(GameObject.Find("Member-" + GameModel.Members[GameModel.Members.Length - 1].Id), Is.Not.Null,
                "成员页无法翻到目录最后一名成员。");
            Button next = Require("MemberNextPage").GetComponent<Button>();
            Assert.That(next.interactable, Is.False, "最后一页的下一页按钮应禁用。");
        }

        private static GameObject[] VisibleMemberCards()
        {
            return Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Exclude)
                .Select(rect => rect.gameObject)
                .Where(item => item.name.StartsWith("Member-"))
                .ToArray();
        }

        private static void Click(string objectName)
        {
            GameObject target = Require(objectName);
            Button button = target.GetComponent<Button>();
            Assert.That(button, Is.Not.Null);
            Assert.That(button.interactable, Is.True);
            button.onClick.Invoke();
        }

        private static GameObject Require(string objectName)
        {
            GameObject result = GameObject.Find(objectName);
            Assert.That(result, Is.Not.Null, $"未找到界面节点：{objectName}");
            return result;
        }

        private static void DestroyAll<T>() where T : Component
        {
            T[] objects = Object.FindObjectsByType<T>(FindObjectsInactive.Include);
            for (int index = 0; index < objects.Length; index++)
                if (objects[index] != null) Object.Destroy(objects[index].gameObject);
        }
    }
}
