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
    public sealed class RosterReadableLayoutPlayModeTests
    {
        private ChoSirenApp app;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            ClearSave();
            DestroyAll<ChoSirenApp>(); DestroyAll<EventSystem>();
            yield return null;
            app = new GameObject("Readable roster layout test").AddComponent<ChoSirenApp>();
            yield return null;
            Require("Content").GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 1034);
            Canvas.ForceUpdateCanvases();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            DestroyAll<ChoSirenApp>(); DestroyAll<EventSystem>();
            yield return null;
            ClearSave();
        }

        [UnityTest]
        public IEnumerator TeamInformationContainersKeepEveryTextRowApart()
        {
            Require("Nav-team").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Canvas.ForceUpdateCanvases();
            foreach (string container in new[] { "TeamTitlePlaque", "TeamPower", "TeamSynergy" })
            {
                GameObject panel = Require(container);
                Assert.That(panel.GetComponent<Outline>(), Is.Not.Null);
                Assert.That(panel.GetComponent<Image>().sprite?.texture.name, Does.Not.Contain("-ai-"));
                AssertTextRectsContainedAndSeparated(panel);
            }
            Assert.That(Require("TeamAttributes").GetComponent<Text>().text, Is.EqualTo("职业自由搭配"));
            Assert.That(Require("TeamStellarBackground").GetComponent<Image>().sprite, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator TeamPickerHasSuppliedPortraitAndDedicatedTextForEveryOwnedMember()
        {
            Invoke("OpenTeamSlotPicker", 0, 0);
            yield return null;
            Canvas.ForceUpdateCanvases();
            var model = new GameModel();
            foreach (int member in model.Save.UnlockedMembers.Take(8))
            {
                GameObject row = Require("PickMember-" + member);
                Image portrait = Require("PickerPortrait-" + member).GetComponent<Image>();
                Assert.That(portrait.sprite, Is.EqualTo(Resources.Load<Sprite>(GameModel.Members[member].ResourcePath)));
                Assert.That(portrait.preserveAspect, Is.True);
                Assert.That(portrait.raycastTarget, Is.False);
                Assert.That(row.GetComponent<Button>().IsInteractable(), Is.True);
                Assert.That(Require("PickerName-" + member).GetComponent<Text>().text, Is.EqualTo(GameModel.Members[member].Name));
                AssertTextRectsContainedAndSeparated(row);
            }
        }

        [UnityTest]
        public IEnumerator MemberProfileUsesSeparateSkillCardsWithActualSemanticIcons()
        {
            Invoke("OpenMember", 0);
            yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.That(GameObject.Find("MemberProfilePanelArt"), Is.Null, "Baked decorative profile frame must not cover live text");
            Assert.That(Require("Portrait").GetComponent<Image>().sprite,
                Is.EqualTo(Resources.Load<Sprite>(GameModel.Members[0].ResourcePath)));
            foreach (string name in new[] { "MemberSkillPrimary", "MemberSkillSecondary" })
            {
                var icon = Require(name + "Icon").GetComponent<SkillIconGraphic>();
                Assert.That(icon, Is.Not.Null);
                Assert.That(icon.raycastTarget, Is.False);
                Assert.That(icon.Kind, Is.EqualTo(SkillIconVisuals.Resolve(Require(name).GetComponent<Text>().text,
                    Require(name + "Description").GetComponent<Text>().text)));
                AssertTextRectsContainedAndSeparated(Require(name + "Card"));
            }
            Assert.That(Require("MemberSkillPrimaryIcon").GetComponent<SkillIconGraphic>().Kind,
                Is.Not.EqualTo(Require("MemberSkillSecondaryIcon").GetComponent<SkillIconGraphic>().Kind));
            foreach (string name in new[] { "MemberStatPanel", "MemberAcquireGuide" })
                AssertTextRectsContainedAndSeparated(Require(name));
        }

        [TestCase("狐影瞬击", "两段共120%伤害", SkillIconKind.Slash)]
        [TestCase("魅语迷心", "前排伤害，封技能2秒", SkillIconKind.Charm)]
        [TestCase("潮汐低吟", "恢复全队8%最大生命", SkillIconKind.Heal)]
        [TestCase("深海佑域", "全队18%生命护盾", SkillIconKind.Shield)]
        [TestCase("穿甲击", "固定无视15%防御", SkillIconKind.Pierce)]
        [TestCase("毒刺", "附加2层毒素", SkillIconKind.Poison)]
        [TestCase("终结", "敌人生命低于35%时增伤", SkillIconKind.Burst)]
        public void EverySkillIconHasRecognizableNonEmptyBoundedMesh(string name, string description, SkillIconKind expected)
        {
            SkillIconGraphic icon = SkillIconVisuals.Create(app.transform, "test-icon", name, description, Color.cyan);
            icon.rectTransform.sizeDelta = new Vector2(48,48);
            Assert.That(icon.Kind, Is.EqualTo(expected));
            using (var mesh = new VertexHelper())
            {
                typeof(SkillIconGraphic).GetMethod("OnPopulateMesh", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Invoke(icon, new object[] { mesh });
                Assert.That(mesh.currentVertCount, Is.GreaterThan(3));
                var vertex = new UIVertex();
                for (int i=0; i<mesh.currentVertCount; i++)
                {
                    mesh.PopulateUIVertex(ref vertex,i);
                    Assert.That(icon.rectTransform.rect.Contains(vertex.position), Is.True);
                }
            }
            Object.DestroyImmediate(icon.gameObject);
        }

        [UnityTest]
        public IEnumerator InitialFourAndQinggeProfilesRenderTheirActualSkillPictograms()
        {
            int qingge = System.Array.FindIndex(GameModel.Members, member => member.Name == "晴歌");
            Assert.That(qingge, Is.GreaterThanOrEqualTo(0), "晴歌应存在于真实成员目录");
            var renderedKinds = new System.Collections.Generic.HashSet<SkillIconKind>();
            foreach (int member in new[] { 0, 1, 2, 3, qingge })
            {
                Invoke("OpenMember", member);
                yield return null;
                foreach (string name in new[] { "MemberSkillPrimary", "MemberSkillSecondary" })
                {
                    var icon = Require(name + "Icon").GetComponent<SkillIconGraphic>();
                    string skill = Require(name).GetComponent<Text>().text;
                    string effect = Require(name + "Description").GetComponent<Text>().text;
                    Assert.That(icon.Kind, Is.EqualTo(SkillIconVisuals.Resolve(skill, effect)),
                        GameModel.Members[member].Name + " " + skill);
                    renderedKinds.Add(icon.Kind);
                    using (var mesh = new VertexHelper())
                    {
                        typeof(SkillIconGraphic).GetMethod("OnPopulateMesh", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                            .Invoke(icon, new object[] { mesh });
                        Assert.That(mesh.currentVertCount, Is.GreaterThan(3), "不能只是空圆框：" + skill);
                    }
                }
            }
            Assert.That(renderedKinds.Count, Is.GreaterThanOrEqualTo(5),
                "初始四名不同定位的技能必须具有多种可辨识语义轮廓，不能统一图案换色。");
        }

        private static void AssertTextRectsContainedAndSeparated(GameObject panel)
        {
            RectTransform root = panel.GetComponent<RectTransform>();
            Text[] text = panel.GetComponentsInChildren<Text>().Where(t => !string.IsNullOrEmpty(t.text)).ToArray();
            Rect[] rects = text.Select(t => InParent(t.rectTransform, root)).ToArray();
            for (int i=0; i<text.Length; i++)
            {
                Assert.That(rects[i].xMin, Is.GreaterThanOrEqualTo(root.rect.xMin + 2), text[i].name);
                Assert.That(rects[i].xMax, Is.LessThanOrEqualTo(root.rect.xMax - 2), text[i].name);
                Assert.That(rects[i].yMin, Is.GreaterThanOrEqualTo(root.rect.yMin + 2), text[i].name);
                Assert.That(rects[i].yMax, Is.LessThanOrEqualTo(root.rect.yMax - 2), text[i].name);
                for (int j=i+1; j<text.Length; j++)
                    Assert.That(rects[i].Overlaps(rects[j]), Is.False, text[i].name + " overlaps " + text[j].name);
            }
        }

        private static Rect InParent(RectTransform child, RectTransform parent)
        {
            var corners = new Vector3[4]; child.GetWorldCorners(corners);
            Vector3 min = parent.InverseTransformPoint(corners[0]), max = parent.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
        }
        private void Invoke(string name, params object[] args) => typeof(ChoSirenApp)
            .GetMethod(name,BindingFlags.Instance | BindingFlags.NonPublic).Invoke(app,args);
        private static GameObject Require(string name)
        { var obj=GameObject.Find(name); Assert.That(obj, Is.Not.Null, name); return obj; }
        private static void DestroyAll<T>() where T:Component
        { foreach (T obj in Object.FindObjectsByType<T>(FindObjectsInactive.Include)) Object.Destroy(obj.gameObject); }
        private static void ClearSave()
        { PlayerPrefs.DeleteKey(GameModel.SaveKey); PlayerPrefs.DeleteKey(GameModel.LegacySaveKey); PlayerPrefs.Save(); }
    }
}
