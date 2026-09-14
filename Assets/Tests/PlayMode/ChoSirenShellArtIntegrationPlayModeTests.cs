using System.Collections;
using System.Linq;
using ChoSiren.Systems.Presentation;
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
        public IEnumerator HeaderKeepsDynamicResourceActionsNonOverlappingAndOmitsMail()
        {
            string[] groups = { "Currency-diamond", "Currency-gold", "Currency-stamina", "Settings" };
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
            Assert.That(GameObject.Find("Mail"), Is.Null,
                "最新首页参考没有邮件入口，邮件功能不得继续占用顶部 HUD 空间。");
            yield return null;
        }

        [UnityTest]
        public IEnumerator LobbyV2ArtworkCoversHeaderTitleAndEveryNavigationDestination()
        {
            Canvas canvas = Object.FindAnyObjectByType<Canvas>();
            Assert.That(canvas, Is.Not.Null, "首页 V2 美术回归需要 Canvas。");
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            Assert.That(scaler, Is.Not.Null, "首页 V2 美术回归需要 CanvasScaler。");

            // Fix this test's own viewport instead of inheriting the physical Editor window or
            // a viewport left behind by another test. Lobby responsive layout uses the logical
            // Canvas height to choose between reference portrait and compact fallback modes.
            canvas.renderMode = RenderMode.WorldSpace;
            scaler.enabled = false;
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            canvasRect.anchorMin = canvasRect.anchorMax = new Vector2(.5f, .5f);
            canvasRect.pivot = new Vector2(.5f, .5f);
            canvasRect.sizeDelta = new Vector2(720f, 1536f);
            Canvas.ForceUpdateCanvases();
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return null;

            string[] roots =
            {
                "Profile",
                "Currency-diamond", "Currency-gold", "Currency-stamina",
                "Settings",
                "LobbyLogo",
                "Nav-team", "Nav-members", "Nav-lobby", "Nav-accessory", "Nav-audition",
            };
            string[] resources =
            {
                "Art/LobbyPunk/lobby-header-profile-punk-v2",
                "Art/LobbyPunk/lobby-resource-diamond-punk-v2",
                "Art/LobbyPunk/lobby-resource-gold-punk-v2",
                "Art/LobbyPunk/lobby-resource-stamina-punk-v2",
                "Art/LobbyPunk/lobby-settings-punk-v2",
                "Art/LobbyPunk/lobby-logo-punk-v2",
                "Art/LobbyPunk/lobby-nav-team-punk-v2",
                "Art/LobbyPunk/lobby-nav-member-punk-v2",
                "Art/LobbyPunk/lobby-nav-lobby-punk-v2",
                "Art/LobbyPunk/lobby-nav-accessory-punk-v2",
                "Art/LobbyPunk/lobby-nav-audition-punk-v2",
            };
            float[] minimumWidths = { 175, 129, 131, 142, 60, 340, 128, 128, 128, 128, 128 };
            float[] minimumHeights = { 78, 45, 44, 49, 63, 296, 120, 120, 120, 120, 120 };

            for (int index = 0; index < roots.Length; index++)
                AssertV2Artwork(roots[index], resources[index], minimumWidths[index], minimumHeights[index]);

            // Numbers are live game state and must remain updateable even though their frames are authored art.
            Assert.That(Require("Diamonds").GetComponent<Text>(), Is.Not.Null);
            Assert.That(Require("Gold").GetComponent<Text>(), Is.Not.Null);
            Assert.That(Require("Stamina").GetComponent<Text>(), Is.Not.Null);
            yield return null;
        }

        [UnityTest]
        public IEnumerator LobbyReferenceArtStaysLayeredAndLeavesOnlyActionRootsRaycastable()
        {
            RawImage video = Require("LobbyVideoBackground").GetComponent<RawImage>();
            Assert.That(video, Is.Not.Null, "首页必须保留动态角色舞台层。");
            Assert.That(video.raycastTarget, Is.False, "动态角色舞台不得挡住首页操作。");

            Button[] actions =
            {
                Require("PracticeRoom").GetComponent<Button>(),
                Require("AlbumProduction").GetComponent<Button>(),
                Require("Tasks").GetComponent<Button>(),
                Require("LiveOnStage").GetComponent<Button>(),
            };
            string[] resourcePaths =
            {
                "Art/LobbyPunk/lobby-practice-punk-v2",
                "Art/LobbyPunk/lobby-album-punk-v2",
                "Art/LobbyPunk/lobby-task-punk-v2",
                "Art/LobbyPunk/lobby-perform-cta-punk-v2",
            };
            for (int actionIndex = 0; actionIndex < actions.Length; actionIndex++)
            {
                Button action = actions[actionIndex];
                Assert.That(action, Is.Not.Null);
                Image[] artwork = action.GetComponentsInChildren<Image>(true)
                    .Where(image => image != action.targetGraphic && image.sprite != null &&
                                    image.name.EndsWith("VisualV2"))
                    .ToArray();
                Assert.That(artwork, Is.Not.Empty,
                    $"{action.name} 必须使用以 VisualV2 结尾的独立整图层，不能回退到旧版拼装卡。");
                Texture expected = Resources.Load<Sprite>(resourcePaths[actionIndex])?.texture ??
                                   Resources.Load<Texture2D>(resourcePaths[actionIndex]);
                Assert.That(expected, Is.Not.Null, "未导入首页正式素材：" + resourcePaths[actionIndex]);
                Assert.That(artwork.Any(layer => layer.sprite.texture == expected), Is.True,
                    $"{action.name} 未装配正式素材 {resourcePaths[actionIndex]}。");
                foreach (Image layer in artwork)
                {
                    Assert.That(layer.raycastTarget, Is.False,
                        $"{action.name}/{layer.name} 的美术层不得抢占点击射线。");
                }
            }

            GameObject faceSafeZone = Require("HeroFaceSafeZone");
            Graphic faceGraphic = faceSafeZone.GetComponent<Graphic>();
            Assert.That(faceGraphic == null || !faceGraphic.raycastTarget, Is.True,
                "角色脸部安全区只能记录构图，不应成为隐藏点击层。");
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
            Assert.That(Require("EquipmentMemberName").GetComponent<Text>().text,
                Does.StartWith(GameModel.Members[selectedMember].Name));
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
            Assert.That(Require("EquipmentMemberName").GetComponent<Text>().text,
                Does.StartWith(GameModel.Members[next].Name));
            Click("Accessory-2");
            yield return null;
            AssertSpriteTexture("EquipmentSelectedArt", itemPaths[2]);
        }

        [UnityTest]
        public IEnumerator OwnedProfilesRevealEverythingWhileLockedProfilesOnlyShowSilhouetteAndProgress()
        {
            Click("Nav-members");
            yield return null;

            int lockedIndex = Enumerable.Range(0, GameModel.Members.Length)
                .First(index => !new GameModel().IsUnlocked(index));
            Click("Member-" + GameModel.Members[lockedIndex].Id);
            yield return null;
            Assert.That(Require("MemberOwnershipStatus").GetComponent<Text>()?.text, Is.EqualTo("尚未签约"));
            Text lockedProgress = Require("MemberLockedProgress").GetComponent<Text>();
            Assert.That(lockedProgress.text, Does.Contain("未获得"), "未获得角色必须给出剩余数量/进度。");
            Require("LockedSilhouetteMark");
            // 未获得角色不得暴露真实姓名、属性、技能或队长特性。
            Assert.That(GameObject.Find("MemberStatVocal"), Is.Null, "未获得角色不得显示舞台四维。");
            Assert.That(GameObject.Find("MemberStatRhythm"), Is.Null);
            Assert.That(GameObject.Find("MemberStatPresence"), Is.Null);
            Assert.That(GameObject.Find("MemberStatResonance"), Is.Null);
            Assert.That(GameObject.Find("MemberSkillPrimary"), Is.Null, "未获得角色不得显示技能名与效果。");
            Assert.That(GameObject.Find("MemberSkillSecondary"), Is.Null);
            Assert.That(GameObject.Find("MemberNormalAttack"), Is.Null);
            Assert.That(Require("CaptainEffectDescription").GetComponent<Text>().text,
                Is.EqualTo("签约后解锁该成员的队长特性。"), "未获得角色不得提前公开队长特性。");
            Require("MemberAcquireGuide");
            Transform lockedPortrait = Require("MemberModal").transform.Find("Panel/LockedPortraitFrame/Portrait");
            Assert.That(lockedPortrait, Is.Not.Null);
            Image lockedImage = lockedPortrait.GetComponent<Image>();
            Assert.That(lockedImage.sprite, Is.Not.Null);
            Assert.That(lockedImage.sprite.name, Is.EqualTo(MemberRosterVisibility.SilhouetteSpriteName),
                "未获得角色必须使用程序生成的通用剪影，而不是任何成员立绘。");
            Assert.That(lockedImage.sprite, Is.Not.SameAs(
                    Resources.Load<Sprite>(GameModel.Members[lockedIndex].ResourcePath)),
                "未获得角色不得渲染该成员的真实立绘。");
            Transform cardSilhouette = GameObject.Find("Member-" + GameModel.Members[lockedIndex].Id)
                .transform.Find("LockedSilhouette");
            Assert.That(cardSilhouette, Is.Not.Null);
            Assert.That(cardSilhouette.GetComponent<Image>().sprite, Is.SameAs(lockedImage.sprite),
                "图鉴卡与档案必须共用同一张剪影。");
            Require("AcquireMember");
            Assert.That(GameObject.Find("Train"), Is.Null, "未签约成员不得显示训练操作。");
            Assert.That(GameObject.Find("Team"), Is.Null, "未签约成员不得显示编队操作。");
            Assert.That(GameObject.Find("MemberEquipment"), Is.Null);
            Click("CloseTop");
            yield return null;

            Click("Member-" + GameModel.Members[0].Id);
            yield return null;
            Assert.That(Require("MemberOwnershipStatus").GetComponent<Text>()?.text, Is.EqualTo("已签约成员"));
            Transform ownedPortrait = Require("MemberModal").transform.Find("Panel/PortraitFrame/Portrait");
            Assert.That(ownedPortrait, Is.Not.Null);
            AssertSpriteTexture(ownedPortrait.gameObject, GameModel.Members[0].ResourcePath);
            Require("MemberSectionBaseStats");
            Require("MemberSectionNormalAttack");
            Require("MemberNormalAttack");
            Require("MemberSectionActiveSkills");
            Require("CaptainEffectTitle");
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

        private static void AssertV2Artwork(string rootName, string resourcePath,
            float minimumWidth, float minimumHeight)
        {
            GameObject root = Require(rootName);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            Assert.That(rootRect, Is.Not.Null, rootName + " 缺少 RectTransform。");
            Assert.That(rootRect.rect.width, Is.GreaterThanOrEqualTo(minimumWidth - .5f),
                $"{rootName} 宽度不足，无法达到参考图的视觉占比。");
            Assert.That(rootRect.rect.height, Is.GreaterThanOrEqualTo(minimumHeight - .5f),
                $"{rootName} 高度不足，无法覆盖完整视觉和点击区域。");

            Texture expected = Resources.Load<Sprite>(resourcePath)?.texture ?? Resources.Load<Texture2D>(resourcePath);
            Assert.That(expected, Is.Not.Null, "未导入首页 V2 素材：" + resourcePath);
            Image[] visualLayers = root.GetComponentsInChildren<Image>(true)
                .Where(image => image.name.EndsWith("VisualV2") && image.sprite != null)
                .ToArray();
            Assert.That(visualLayers.Any(image => image.sprite.texture == expected), Is.True,
                $"{rootName} 未装配 V2 素材 {resourcePath}，或整图节点名未以 VisualV2 结尾。");
            foreach (Image visual in visualLayers)
                Assert.That(visual.raycastTarget, Is.False,
                    $"{rootName}/{visual.name} 是视觉层，不得抢占 {rootName} 的点击射线。");
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
