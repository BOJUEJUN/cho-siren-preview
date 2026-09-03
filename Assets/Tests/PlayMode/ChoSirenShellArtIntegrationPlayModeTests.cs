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
        public IEnumerator HeaderUsesUserAvatarAndEvenResourceRhythm()
        {
            AssertSpriteTexture("Avatar", "Art/ProfileAvatarUser");

            float[] centers =
            {
                LeftInParent(Require("DiamondIcon")) + 12.5f,
                LeftInParent(Require("GoldIcon")) + 12.5f,
                LeftInParent(Require("StaminaIcon")) + 13f,
                LeftInParent(Require("Mail")) + 20f,
            };
            for (int index = 1; index < centers.Length; index++)
                Assert.That(centers[index] - centers[index - 1], Is.InRange(112f, 132f),
                    "顶部资源与邮件应保持一致的视觉节拍，不能在体力后留出一个空按钮位。");

            Assert.That(Require("Mail").GetComponent<RectTransform>().anchoredPosition.x,
                Is.EqualTo(-120f).Within(0.1f));
            Assert.That(Require("Settings").GetComponent<RectTransform>().anchoredPosition.x,
                Is.EqualTo(-80f).Within(0.1f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator TeamChromeUsesTransparentAiArtWithoutReplacingLiveText()
        {
            Click("Nav-team");
            yield return null;

            AssertSpriteTexture("TeamTitlePlaque", "Art/TeamAI/UI/team-title-plaque-ai-v2");
            AssertSpriteTexture("TeamPower", "Art/TeamAI/UI/team-power-panel-ai-v2");
            AssertSpriteTexture("TeamSynergy", "Art/TeamAI/UI/team-synergy-panel-ai-v2");
            AssertSpriteTexture("ChangeLeader", "Art/TeamAI/UI/team-action-cyan-ai-v2");
            AssertSpriteTexture("AutoTeam", "Art/TeamAI/UI/team-action-pink-ai-v2");
            AssertSpriteTexture("TeamSwapIcon", "Art/TeamAI/UI/team-swap-ai-v2");

            Assert.That(Require("TeamPowerValue").GetComponent<Text>()?.text, Is.Not.Empty,
                "AI 面板只负责美术框体，实时战力仍必须由可读文字呈现。");
            Assert.That(Require("ChangeLeader").transform.Find("Label")?.GetComponent<Text>()?.text,
                Is.EqualTo("更换队长"));
        }

        [UnityTest]
        public IEnumerator AccessoryPreviewUsesOneAlignedRealAssetPerSlot()
        {
            Click("Nav-accessory");
            yield return null;

            AssertSpriteTexture("AccessoryPreviewCharacter", "Art/Members/member-feiyin");
            AssertSpriteTexture("AccessoryPreviewArt", "Art/AccessoryAI/UI/accessory-preview-panel-ai-v1");
            AssertSpriteTexture("AccessoryDetail", "Art/AccessoryAI/UI/accessory-detail-panel-ai-v1");
            AssertSpriteTexture("AccessoryCollection", "Art/AccessoryAI/UI/accessory-collection-panel-ai-v1");
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
                string slotName = index < GameModel.AccessoryNames.Length
                    ? "Accessory-" + index
                    : "AccessorySlot-" + index;
                GameObject slot = Require(slotName);
                AssertSpriteTexture(slot, "Art/AccessoryAI/UI/accessory-slot-ring-ai-v1");
                Transform art = slot.transform.Find("Art");
                Assert.That(art, Is.Not.Null, slotName + " 缺少饰品图标节点。");
                AssertSpriteTexture(art.gameObject, itemPaths[index]);

                RectTransform slotRect = slot.GetComponent<RectTransform>();
                RectTransform artRect = art.GetComponent<RectTransform>();
                Assert.That(artRect.anchoredPosition.x + artRect.rect.width * 0.5f,
                    Is.EqualTo(slotRect.rect.width * 0.5f).Within(0.5f),
                    slotName + " 的图标必须和 AI 槽环水平同心。");
            }

            AssertSpriteTexture("AccessoryDetailArt", itemPaths[0]);
            Transform collectionArt = Require("AccessoryCollection-0").transform.Find("Art");
            Assert.That(collectionArt, Is.Not.Null);
            AssertSpriteTexture(collectionArt.gameObject, itemPaths[0]);
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
            AssertSpriteTexture("MemberProfilePanelArt", "Art/MemberAI/UI/member-profile-panel-ai-v1");
            Require("AcquireMember");
            Assert.That(GameObject.Find("Train"), Is.Null, "未签约成员不得显示训练操作。");
            Assert.That(GameObject.Find("Team"), Is.Null, "未签约成员不得显示编队操作。");
            Click("Close");
            yield return null;

            Click("Member-" + GameModel.Members[0].Id);
            yield return null;
            Assert.That(Require("MemberOwnershipStatus").GetComponent<Text>()?.text, Is.EqualTo("已签约成员"));
            Require("Train");
            Require("Team");
            Assert.That(GameObject.Find("AcquireMember"), Is.Null);
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
