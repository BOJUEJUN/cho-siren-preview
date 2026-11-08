using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using ChoSiren.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ChoSiren.Tests
{
    /// <summary>
    /// 2026-11-08 少爷 review: 团队 power banner (inpainted art, slanted live number, pig behind the banner), more team chips
    /// scroll in their own strip, baked stickers press with the body, 饰品 sort opens a drop-down, 团队 / 饰品 / 成员 share a
    /// full-screen background, the 面试 badge has a blue photo backdrop and its back cards switch on tap.
    /// Captures at a phone aspect into Builds/qa-logs/page-polish-1108/.
    /// </summary>
    public sealed class PagePolish1108Tests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        string saved;
        bool had, previousIgnore;
        ChoSirenApp app;
        static string Out => Path.GetFullPath("Builds/qa-logs/page-polish-1108");

        [UnitySetUp] public IEnumerator SetUp()
        {
            had = PlayerPrefs.HasKey(GameModel.SaveKey); saved = PlayerPrefs.GetString(GameModel.SaveKey);
            previousIgnore = LogAssert.ignoreFailingMessages; LogAssert.ignoreFailingMessages = true;
            foreach (var old in Object.FindObjectsByType<ChoSirenApp>(FindObjectsSortMode.None)) Object.Destroy(old.gameObject);
            foreach (var old in Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None)) Object.Destroy(old.gameObject);
            yield return null;
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            if (app != null) Object.Destroy(app.gameObject);
            foreach (var old in Object.FindObjectsByType<NativeTitleScreen>(FindObjectsSortMode.None)) Object.Destroy(old.gameObject);
            foreach (var old in Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None)) Object.Destroy(old.gameObject);
            yield return null;
            if (had) PlayerPrefs.SetString(GameModel.SaveKey, saved); else PlayerPrefs.DeleteKey(GameModel.SaveKey);
            PlayerPrefs.Save(); LogAssert.ignoreFailingMessages = previousIgnore;
        }

        [UnityTest] public IEnumerator PagesPolishedForPhone()
        {
            app = new GameObject("PagePolish1108").AddComponent<ChoSirenApp>();
            yield return null;
            Camera camera = new GameObject("PagePolishCamera", typeof(Camera)).GetComponent<Camera>();
            var target = new RenderTexture(603, 1311, 24);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.magenta;
            camera.orthographic = true; camera.targetTexture = target;
            try
            {
                Canvas canvas = app.GetComponentInChildren<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
                foreach (var fitter in canvas.GetComponentsInChildren<SafeAreaFitter>(true))
                {
                    fitter.enabled = false; var rect = (RectTransform)fitter.transform;
                    rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                    rect.offsetMin = rect.offsetMax = Vector2.zero;
                }
                var model = new GameModel(() => DateTime.Now, new InMemorySaveStore());
                model.Save.StoryProgress = 100;
                for (int i = 0; i < 3; i++) model.AddTeamPreset(out _);
                typeof(ChoSirenApp).GetField("model", Private).SetValue(app, model);
                yield return null;

                // 团队
                yield return Show("team");
                Transform team = Find("TeamReferenceBoard038");
                Transform pig = team.Find("TeamPig"), board = team.Find("TeamBoard1107");
                Assert.That(pig.GetSiblingIndex(), Is.LessThan(board.GetSiblingIndex()), "the pig peeks from behind the banner");
                Text power = team.Find("TeamPowerValue").GetComponent<Text>();
                Assert.That(power.rectTransform.localEulerAngles.z, Is.EqualTo(5.5f).Within(.01f), "the number rides the banner's slant");
                Assert.That(team.Find("TeamPreset-0"), Is.Null, "no row of chips under the pig / banner");
                Assert.That(team.Find("TeamPresetPicker/Label").GetComponent<Text>().text, Is.EqualTo(model.ActiveTeamName));
                foreach (string sticker in new[] { "ChangeLeader", "AutoTeam" })
                {
                    // 更换队长 / 一键编队 are their own RGBA layers; the lobby feedback squashes on press and pops past full size on release.
                    Transform hit = team.Find(sticker);
                    RectTransform art = (RectTransform)hit.Find("StickerVisualV2");
                    Assert.That(art.GetComponent<Image>().sprite, Is.Not.Null, sticker);
                    Assert.That(hit.GetComponent<LobbyHotspotFeedback>(), Is.Not.Null, sticker);
                    var press = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, pointerId = 0 };
                    ExecuteEvents.Execute(hit.gameObject, press, ExecuteEvents.pointerEnterHandler);
                    ExecuteEvents.Execute(hit.gameObject, press, ExecuteEvents.pointerDownHandler);
                    yield return new WaitForSecondsRealtime(.15f);
                    Assert.That(art.localScale.x, Is.LessThan(1f), sticker + " squashes under the finger");
                    ExecuteEvents.Execute(hit.gameObject, press, ExecuteEvents.pointerUpHandler);
                    ExecuteEvents.Execute(hit.gameObject, press, ExecuteEvents.pointerExitHandler);
                    float peak = 0f;
                    for (float t = 0f; t < .3f; t += Time.unscaledDeltaTime) { peak = Mathf.Max(peak, art.localScale.x); yield return null; }
                    Assert.That(peak, Is.GreaterThan(1.02f), sticker + " pops out on release");
                    yield return new WaitForSecondsRealtime(.3f);
                    Assert.That(art.localScale.x, Is.EqualTo(1f).Within(.002f), sticker + " settles");
                }
                AssertCovered(team);
                Capture(camera, target, "team.png");
                // 2026-11-08 (少爷: "队伍搞下拉选择吧"): the picker drops down every team plus 新建队伍.
                team.Find("TeamPresetPicker").GetComponent<Button>().onClick.Invoke();
                yield return new WaitForSecondsRealtime(.3f);
                Transform teamMenu = team.Find("TeamPresetMenu");
                Assert.That(teamMenu, Is.Not.Null);
                Assert.That(teamMenu.Find("TeamPresetAdd"), Is.Not.Null, "room for a fifth team");
                Assert.That(Enumerable.Range(0, model.VisibleTeamPresetCount).All(i => teamMenu.Find("TeamPreset-" + i) != null), Is.True);
                Capture(camera, target, "team-picker.png");
                int other = model.Save.ActiveTeamPreset == 0 ? 1 : 0;
                teamMenu.Find("TeamPreset-" + other).GetComponent<Button>().onClick.Invoke();
                yield return new WaitForSecondsRealtime(.3f);
                Assert.That(model.Save.ActiveTeamPreset, Is.EqualTo(other));
                Assert.That(Find("TeamReferenceBoard038").Find("TeamPresetMenu"), Is.Null);

                // 饰品: sort drop-down
                yield return Show("accessory");
                Transform inventory = Find("InventoryBoard");
                AssertCovered(inventory);
                Capture(camera, target, "accessory.png");
                inventory.Find("InventorySort").GetComponent<Button>().onClick.Invoke();
                yield return new WaitForSecondsRealtime(.3f);
                Transform menu = inventory.Find("InventorySortMenu");
                Assert.That(menu, Is.Not.Null, "稀有度 opens a panel to choose from");
                Assert.That(menu.GetComponentsInChildren<Button>().Count(b => b.name.StartsWith("InventorySortOption-")), Is.EqualTo(3));
                Capture(camera, target, "accessory-sort-menu.png");
                menu.Find("InventorySortOption-1").GetComponent<Button>().onClick.Invoke();
                yield return new WaitForSecondsRealtime(.2f);
                inventory = Find("InventoryBoard");
                Assert.That(inventory.Find("InventorySort/Label").GetComponent<Text>().text, Does.StartWith("强化等级"));
                Assert.That(inventory.Find("InventorySortMenu"), Is.Null);

                // 成员 shares the background
                yield return Show("members");
                AssertCovered(Find("MembersReferenceBoard038"));
                Capture(camera, target, "members.png");

                // 选秀: photo backdrop, tap the back card to switch
                yield return Show("audition");
                Transform photoBg = app.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "CandidatePhotoBg");
                Assert.That(photoBg, Is.Not.Null, "cut-out busts stand on the badge-blue backdrop");
                Assert.That(app.GetComponentsInChildren<Image>().Count(i => i.name == "BackCardPortrait" && i.sprite != null), Is.EqualTo(2),
                    "both back cards show the next candidates");
                Capture(camera, target, "audition.png");
                Text counter = app.GetComponentsInChildren<Text>().First(t => t.name == "CandidateCounter");
                string before = counter.text;
                var swipe = app.GetComponentsInChildren<InterviewBadgeSwipe1108>().First();
                RectTransform card1 = app.GetComponentsInChildren<RectTransform>().First(t => t.name == "InterviewCard1");
                Vector3[] corners = new Vector3[4]; card1.GetWorldCorners(corners);
                // A point on the visible lower strip of the first back card (below the front badge).
                Vector3 world = Vector3.Lerp(corners[0], corners[3], .45f) + (corners[1] - corners[0]) * .1f;
                var click = new PointerEventData(EventSystem.current)
                {
                    position = RectTransformUtility.WorldToScreenPoint(camera, world),
                    pointerPressRaycast = new RaycastResult { module = canvas.GetComponent<GraphicRaycaster>() },
                    eligibleForClick = true, button = PointerEventData.InputButton.Left,
                };
                swipe.OnPointerClick(click);
                yield return new WaitForSecondsRealtime(.2f);
                Capture(camera, target, "audition-promoting.png");
                yield return new WaitForSecondsRealtime(.5f);
                counter = app.GetComponentsInChildren<Text>().First(t => t.name == "CandidateCounter");
                Assert.That(counter.text, Is.Not.EqualTo(before), "tapping a back card brings it forward");
                yield return new WaitForSecondsRealtime(.7f);
                Capture(camera, target, "audition-after-tap.png");
            }
            finally { Object.DestroyImmediate(camera.gameObject); target.Release(); Object.DestroyImmediate(target); }
        }

        IEnumerator Show(string screen)
        {
            typeof(ChoSirenApp).GetMethod("ShowScreenSafely", Private).Invoke(app, new object[] { screen });
            yield return null; yield return new WaitForSecondsRealtime(.5f); Canvas.ForceUpdateCanvases();
        }

        Transform Find(string name) => app.GetComponentsInChildren<Transform>().First(t => t.name == name);

        static void AssertCovered(Transform board)
        {
            Assert.That(board.GetComponent<Image>().color.a, Is.EqualTo(0f), board.name + " leaves the background to the full-screen cover");
            Assert.That(board.GetComponent<ScreenEdgeExtend>() == null, Is.True, board.name + " no longer stretches its edge pixels");
            RawImage ambient = Object.FindObjectsByType<PageAmbient>(FindObjectsSortMode.None).First().GetComponent<RawImage>();
            Assert.That(ambient.enabled, Is.True);
            Assert.That(ambient.color, Is.EqualTo(Color.white), "the cover is the art itself, not a dimmed blur");
        }

        static void Capture(Camera camera, RenderTexture target, string name)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            Canvas.ForceUpdateCanvases(); camera.Render(); var previous = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
            Directory.CreateDirectory(Out); File.WriteAllBytes(Path.Combine(Out, name), image.EncodeToPNG());
            RenderTexture.active = previous; Object.DestroyImmediate(image);
        }
    }
}
