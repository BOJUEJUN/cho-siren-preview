using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ChoSiren.Tests
{
    public sealed class ChoSirenShellArtIntegrationPlayModeTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            PlayerPrefs.DeleteKey(GameModel.SaveKey);
            PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
            PlayerPrefs.Save();
            DestroyAll<ChoSirenApp>();
            DestroyAll<EventSystem>();
            yield return null;

            new GameObject("CHO-SIREN Shell Art Test").AddComponent<ChoSirenApp>();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            DestroyAll<ChoSirenApp>();
            DestroyAll<EventSystem>();
            yield return null;
            PlayerPrefs.DeleteKey(GameModel.SaveKey);
            PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
            PlayerPrefs.Save();
        }

        [UnityTest]
        public IEnumerator HeaderUsesUserAvatarAndNonOverlappingResourceActions()
        {
            AssertSpriteTexture("Avatar", "Art/ProfileAvatarUser");

            string[] groups = { "Currency-diamond", "Currency-gold", "Currency-stamina", "Mail", "Settings" };
            float previousRight = 0;
            foreach (string name in groups)
            {
                GameObject group = Require(name);
                RectTransform rect = group.GetComponent<RectTransform>();
                float left = LeftInParent(group);
                Assert.That(left, Is.GreaterThanOrEqualTo(previousRight - 0.5f),
                    "顶部完整资源信息块、邮件与设置不得重叠。");
                previousRight = left + rect.rect.width;
                Assert.That(group.GetComponent<Button>()?.IsInteractable(), Is.True);
                if (!name.StartsWith("Currency-")) continue;
                Assert.That(GameObject.Find(name.Replace("Currency-", "CurrencyPlus-")), Is.Null,
                    "资源信息块直接可点，不再显示额外加号。");
                Assert.That(group.GetComponent<Image>().color.a, Is.InRange(.3f, .7f));
                Assert.That(group.GetComponentsInChildren<Text>().Count(t => !string.IsNullOrEmpty(t.text)), Is.EqualTo(1));
            }
            Assert.That(previousRight, Is.LessThan(Require("TopBar").GetComponent<RectTransform>().rect.width));
            yield return null;
        }

        [UnityTest]
        public IEnumerator TeamInformationUsesQuietFramesAndPreservesSuppliedStageAndActions()
        {
            Click("Nav-team");
            yield return null;

            AssertQuietInformationPanel("TeamTitlePlaque");
            AssertQuietInformationPanel("TeamPower");
            AssertQuietInformationPanel("TeamSynergy");
            Assert.That(Require("TeamStellarBackground").GetComponent<Image>().sprite, Is.Not.Null);
            AssertSpriteTexture("ChangeLeader", "Art/TeamAI/UI/team-action-cyan-ai-v2");
            AssertSpriteTexture("AutoTeam", "Art/TeamAI/UI/team-action-pink-ai-v2");
            AssertSpriteTexture("TeamSwapIcon", "Art/TeamAI/UI/team-swap-ai-v2");

            Assert.That(Require("TeamPowerValue").GetComponent<Text>()?.text, Is.Not.Empty,
                "简洁信息框仍必须保留可读的实时战力。");
            Assert.That(Require("ChangeLeader").transform.Find("Label")?.GetComponent<Text>()?.text,
                Is.EqualTo("更换队长"));
        }

        [UnityTest]
        public IEnumerator PersonalEquipmentUsesSelectedCharacterAndAlignedSuppliedItemArt()
        {
            Click("Nav-accessory");
            yield return null;

            var model = new GameModel();
            int selectedMember = model.Save.Team[0];
            AssertSpriteTexture("EquipmentPortrait", GameModel.Members[selectedMember].ResourcePath);
            Assert.That(Require("EquipmentPortrait").GetComponent<Image>().preserveAspect, Is.True);
            Assert.That(GameObject.Find("AccessoryPreviewArt"), Is.Null,
                "新角色装备页不再叠加自带槽位/文字的旧美术面板。");
            Assert.That(GameObject.Find("AccessoryCollection"), Is.Null);
            Assert.That(GameObject.Find("WornAccessoryGlow"), Is.Null,
                "旧悬浮佩戴层会把一个饰品重复显示两次，必须彻底移除。");
            Assert.That(GameObject.Find("WornAccessory"), Is.Null);

            string[] itemPaths =
            {
                "Art/AccessoryAI/Items/accessory-ear-monitor-ai-v1",
                "Art/AccessoryAI/Items/accessory-heart-necklace-ai-v1",
                "Art/AccessoryAI/Items/accessory-dance-boots-ai-v1",
                "Art/AccessoryAI/Items/accessory-microphone-charm-ai-v1",
                "Art/AccessoryAI/Items/accessory-star-bracelet-ai-v1",
                "Art/AccessoryAI/Items/accessory-stage-crown-ai-v1",
            };
            for (int index = 0; index < itemPaths.Length; index++)
            {
                string slotName = "Accessory-" + index;
                GameObject slot = Require(slotName);
                Transform art = slot.transform.Find("ItemArt");
                Assert.That(art, Is.Not.Null, slotName + " 缺少饰品图标节点。");
                AssertSpriteTexture(art.gameObject, itemPaths[index]);

                RectTransform slotRect = slot.GetComponent<RectTransform>();
                RectTransform artRect = art.GetComponent<RectTransform>();
                Assert.That(artRect.anchoredPosition.x + artRect.rect.width * 0.5f,
                    Is.EqualTo(slotRect.rect.width * 0.5f).Within(0.5f),
                    slotName + " 的图标必须和收藏卡水平同心。");
                Assert.That(art.GetComponent<Image>().preserveAspect, Is.True);
            }

            AssertSpriteTexture("EquipmentSelectedArt", itemPaths[0]);
            Click("EquipmentChooseMember");
            yield return null;
            Click("PickMember-1");
            yield return null;
            int next = model.Save.UnlockedMembers[(model.Save.UnlockedMembers.IndexOf(selectedMember) + 1)
                % model.Save.UnlockedMembers.Count];
            AssertSpriteTexture("EquipmentPortrait", GameModel.Members[next].ResourcePath);
            Assert.That(Require("EquipmentMemberName").GetComponent<Text>().text,
                Does.StartWith(GameModel.Members[next].Name));
            Click("Accessory-2");
            yield return null;
            AssertSpriteTexture("EquipmentSelectedArt", itemPaths[2]);
            AssertSpriteTexture("EquipmentPortrait", GameModel.Members[next].ResourcePath);
        }

        [UnityTest]
        public IEnumerator OwnedAndLockedMembersBothOpenCompleteProfiles()
        {
            Click("Nav-members");
            yield return null;

            int lockedIndex = Enumerable.Range(0, GameModel.Members.Length).First(index => index >= 4);
            Click("Member-" + GameModel.Members[lockedIndex].Id);
            yield return null;
            Assert.That(Require("MemberOwnershipStatus").GetComponent<Text>()?.text, Is.EqualTo("尚未签约"));
            Require("MemberPower");
            Require("MemberStatAttack");
            Require("MemberStatHp");
            Require("MemberStatCrit");
            Require("MemberStatSpeed");
            Require("MemberSkillPrimary");
            Require("MemberSkillSecondary");
            Require("MemberAcquireGuide");
            Assert.That(GameObject.Find("MemberProfilePanelArt"), Is.Null,
                "带空圆圈和装饰文字的旧资料底图不可重新压在动态信息后方。");
            AssertQuietInformationPanel("MemberStatPanel");
            AssertQuietInformationPanel("MemberSkillPanel");
            AssertQuietInformationPanel("MemberAcquireGuide");
            Assert.That(Require("MemberSkillPrimaryIcon").GetComponent<SkillIconGraphic>(), Is.Not.Null);
            Assert.That(Require("MemberSkillSecondaryIcon").GetComponent<SkillIconGraphic>(), Is.Not.Null);
            Transform lockedPortrait = Require("MemberModal").transform.Find("Panel/Portrait");
            Assert.That(lockedPortrait, Is.Not.Null);
            AssertSpriteTexture(lockedPortrait.gameObject, GameModel.Members[lockedIndex].ResourcePath);
            Require("AcquireMember");
            Assert.That(GameObject.Find("Train"), Is.Null, "未签约成员不得显示训练操作。");
            Assert.That(GameObject.Find("Team"), Is.Null, "未签约成员不得显示编队操作。");
            Click("Close");
            yield return null;

            Click("Member-" + GameModel.Members[0].Id);
            yield return null;
            Assert.That(Require("MemberOwnershipStatus").GetComponent<Text>()?.text, Is.EqualTo("已签约成员"));
            Transform ownedPortrait = Require("MemberModal").transform.Find("Panel/Portrait");
            Assert.That(ownedPortrait, Is.Not.Null);
            AssertSpriteTexture(ownedPortrait.gameObject, GameModel.Members[0].ResourcePath);
            Require("Train");
            Require("Team");
            Assert.That(GameObject.Find("AcquireMember"), Is.Null);
        }

        private static void AssertQuietInformationPanel(string name)
        {
            var panel = Require(name);
            Image image = panel.GetComponent<Image>();
            Assert.That(image, Is.Not.Null);
            Assert.That(image.color.a, Is.GreaterThan(.8f), "信息区需要足够背景对比度：" + name);
            Assert.That(image.sprite?.texture.name ?? string.Empty, Does.Not.Contain("-ai-"),
                "信息框不得再使用会干扰动态文字的旧AI装饰框：" + name);
            Outline edge = panel.GetComponent<Outline>();
            Assert.That(edge, Is.Not.Null);
            Assert.That(edge.effectDistance.magnitude, Is.LessThanOrEqualTo(2f));
            Assert.That(panel.GetComponentsInChildren<Text>().Any(t => !string.IsNullOrWhiteSpace(t.text)), Is.True);
        }

        private static float LeftInParent(GameObject target)
        {
            RectTransform rect = target.GetComponent<RectTransform>();
            RectTransform parent = rect.parent as RectTransform;
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return parent != null ? parent.InverseTransformPoint(corners[0]).x + parent.rect.width * 0.5f : 0f;
        }

        private static void AssertSpriteTexture(string objectName, string resourcePath)
        {
            AssertSpriteTexture(Require(objectName), resourcePath);
        }

        private static void AssertSpriteTexture(GameObject target, string resourcePath)
        {
            Texture expected = Resources.Load<Sprite>(resourcePath)?.texture ?? Resources.Load<Texture2D>(resourcePath);
            Assert.That(expected, Is.Not.Null, "未导入指定 AI 透明素材：" + resourcePath);
            Image image = target.GetComponent<Image>();
            Assert.That(image, Is.Not.Null, target.name + " 缺少 Image。");
            Assert.That(image.sprite, Is.Not.Null, target.name + " 未装配 AI 素材。");
            Assert.That(image.sprite.texture, Is.SameAs(expected),
                target.name + " 使用了错误或回退素材，应为 " + resourcePath);
        }

        private static void Click(string objectName)
        {
            Button button = Require(objectName).GetComponent<Button>();
            Assert.That(button, Is.Not.Null, objectName + " 缺少 Button。");
            Assert.That(button.interactable, Is.True, objectName + " 不可点击。");
            button.onClick.Invoke();
        }

        private static GameObject Require(string objectName)
        {
            GameObject result = GameObject.Find(objectName);
            Assert.That(result, Is.Not.Null, "未找到界面节点：" + objectName);
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
