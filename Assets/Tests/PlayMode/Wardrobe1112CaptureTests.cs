using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ChoSiren.Tests
{
    /// <summary>
    /// 2026-11-12 创意工坊 · 衣橱: the workshop opens on her wardrobe, a finished outfit lands in it with its label and the
    /// reveal, 穿上 wears it across the game (PanelKit portrait), another card previews, 删除 removes, and the design
    /// drawer offers the parts. No AI call: the finished pictures are the saved demo looks. Captures go to
    /// Builds/qa-logs/wardrobe-1112/. Only files this test creates are removed again.
    /// </summary>
    public sealed class Wardrobe1112CaptureTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const string AgreementKey = "cho-agreement-v1";
        private static string Out => Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/qa-logs/wardrobe-1112"));
        private static string Demo(string name) => Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/qa-logs/workshop-ui-demos", name));

        private string save; private bool had, hadAgreement; private int agreement;

        [UnitySetUp] public IEnumerator SetUp()
        {
            had = PlayerPrefs.HasKey(GameModel.SaveKey); save = PlayerPrefs.GetString(GameModel.SaveKey);
            hadAgreement = PlayerPrefs.HasKey(AgreementKey); agreement = PlayerPrefs.GetInt(AgreementKey, 0);
            foreach (var old in Object.FindObjectsByType<ChoSirenApp>(FindObjectsSortMode.None)) Object.Destroy(old.gameObject);
            yield return null;
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            foreach (var old in Object.FindObjectsByType<ChoSirenApp>(FindObjectsSortMode.None)) Object.Destroy(old.gameObject);
            yield return null;
            if (had) PlayerPrefs.SetString(GameModel.SaveKey, save); else PlayerPrefs.DeleteKey(GameModel.SaveKey);
            if (hadAgreement) PlayerPrefs.SetInt(AgreementKey, agreement); else PlayerPrefs.DeleteKey(AgreementKey);
            PlayerPrefs.Save();
        }

        [UnityTest]
        public IEnumerator WardrobeMakesWearsPreviewsAndDeletesOutfits()
        {
            var data = new GameSave { HomeCharacterIndex = 1, StoryProgress = 100, PlayerName = "经纪人", Diamonds = 200 };
            data.CompletedStories.Add(GameModel.PrologueStoryId);
            PlayerPrefs.SetString(GameModel.SaveKey, JsonUtility.ToJson(data));
            PlayerPrefs.SetInt(AgreementKey, 1);
            var app = new GameObject("Wardrobe1112").AddComponent<ChoSirenApp>();
            var camera = new GameObject("Wardrobe1112Camera", typeof(Camera)).GetComponent<Camera>();
            camera.transform.SetParent(app.transform, false);
            const int width = 1080, height = 2288;
            var rt = new RenderTexture(width, height, 24);
            var made = new System.Collections.Generic.List<string>();
            string memberId = null;
            GameModel model = null;
            try
            {
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.orthographic = true; camera.targetTexture = rt;
                Canvas canvas = app.GetComponentInChildren<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10;
                SafeAreaFitter.Layout(new Rect(0, 0, width, height), new Vector2(width, height), out Vector2 min, out Vector2 max, out bool wide);
                foreach (var fitter in canvas.GetComponentsInChildren<SafeAreaFitter>(true))
                {
                    fitter.enabled = false;
                    var safe = (RectTransform)fitter.transform;
                    safe.anchorMin = min; safe.anchorMax = max; safe.offsetMin = safe.offsetMax = Vector2.zero;
                }
                canvas.GetComponent<CanvasScaler>().matchWidthOrHeight = wide ? 1f : 0f;
                yield return null;
                Invoke(app, "ShowScreenSafely", "lobby");
                yield return null;
                Invoke(app, "CloseModal");
                yield return new WaitForSecondsRealtime(.8f);
                model = (GameModel)typeof(ChoSirenApp).GetField("model", Private).GetValue(app);
                Invoke(app, "ActivatePsdHomeAction", "AlbumProduction");
                yield return new WaitForSecondsRealtime(1.5f);
                memberId = (string)typeof(ChoSirenApp).GetProperty("WorkshopSubjectId1104", Private).GetValue(app);
                int before = model.Wardrobe1112(memberId).Count;
                Assert.That(canvas.GetComponentsInChildren<RectTransform>(true).Any(r => r.name == "WardrobeBand1112"), Is.True, "the bottom is her wardrobe");
                Assert.That(canvas.GetComponentsInChildren<RectTransform>(true).Any(r => r.name.StartsWith("WorkshopTool-")), Is.False, "no 换装/炫彩 bar any more");
                Assert.That(canvas.GetComponentsInChildren<RectTransform>(true).Any(r => r.name == "WorkshopSave"), Is.False, "no separate 保存");
                Shot(camera, rt, "1-wardrobe.png");

                // The design drawer: 换哪里 (6 parts) and the styles.
                canvas.GetComponentsInChildren<Button>(true).First(b => b.name == "WardrobeDesign").onClick.Invoke();
                yield return new WaitForSecondsRealtime(.6f);
                Assert.That(canvas.GetComponentsInChildren<Button>(false).Count(b => b.name.StartsWith("Part-")), Is.EqualTo(6));
                canvas.GetComponentsInChildren<Button>(false).First(b => b.name == "Part-1").onClick.Invoke();
                yield return new WaitForSecondsRealtime(.4f);
                Assert.That(canvas.GetComponentsInChildren<Text>(false).Any(t => t.text.StartsWith("只改上衣")), Is.True, "the button names the part");
                Assert.That((string)typeof(ChoSirenApp).GetMethod("DressPrompt1112", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { "上衣", "学院制服", "" }), Does.Contain("只改她的上衣"));
                Shot(camera, rt, "2-design-part.png");

                // Two finished outfits arrive (as a paid generation would hand them over).
                foreach ((string file, string style, string part) in new[] { ("look-knit.png", "冬日针织", "整套"), ("look-school.png", "学院制服", "上衣") })
                {
                    var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    texture.LoadImage(File.ReadAllBytes(Demo(file)));
                    typeof(ChoSirenApp).GetMethod("FinishAiDress1112", Private).Invoke(app, new object[] { memberId, style, part, texture });
                    yield return new WaitForSecondsRealtime(.7f);
                    made.Add((string)typeof(ChoSirenApp).GetField("wardrobePreview1112", Private).GetValue(app));
                }
                Assert.That(model.Wardrobe1112(memberId).Count, Is.EqualTo(before + 2), "every result goes straight into her wardrobe");
                Assert.That(model.Wardrobe1112(memberId).First().Name, Is.EqualTo("学院制服"));
                Assert.That(canvas.GetComponentsInChildren<RectTransform>(true).Any(r => r.name == "AiReveal1105"), Is.True);
                Assert.That(canvas.GetComponentsInChildren<Text>(true).Any(t => t.text == "已收进衣橱"), Is.True);
                Shot(camera, rt, "3-reveal.png");

                // 穿上 from the reveal: worn across the game.
                canvas.GetComponentsInChildren<Button>(true).First(b => b.name == "AiRevealWear").onClick.Invoke();
                yield return new WaitForSecondsRealtime(.6f);
                Assert.That(model.WornOutfitFile1112(memberId), Is.EqualTo(made[1]));
                Sprite worn = Portrait(memberId, false);
                Assert.That(worn, Is.Not.Null);
                Assert.That(worn.name, Is.EqualTo("Outfit-" + memberId), "her full portrait is the worn outfit everywhere");
                Assert.That(Portrait(memberId, true)?.name, Is.Not.EqualTo("Outfit-" + memberId), "small avatars stay drawn");
                Shot(camera, rt, "4-worn.png");

                // Try another card on, then 删除 it.
                canvas.GetComponentsInChildren<Button>(true).First(b => b.name == "WardrobeCard-2").onClick.Invoke();
                yield return new WaitForSecondsRealtime(.5f);
                Assert.That((string)typeof(ChoSirenApp).GetField("wardrobePreview1112", Private).GetValue(app), Is.EqualTo(made[0]));
                Assert.That(canvas.GetComponentsInChildren<Text>(true).Any(t => t.text == "穿上"), Is.True, "a card that is not worn offers 穿上");
                Shot(camera, rt, "5-preview.png");
                canvas.GetComponentsInChildren<Button>(true).First(b => b.name == "WardrobeDelete").onClick.Invoke();
                yield return new WaitForSecondsRealtime(.4f);
                Shot(camera, rt, "6-delete-confirm.png");
                canvas.GetComponentsInChildren<Button>(true).First(b => b.name == "DeleteOutfit").onClick.Invoke();
                yield return new WaitForSecondsRealtime(.5f);
                Assert.That(model.Wardrobe1112(memberId).Count, Is.EqualTo(before + 1));
                Assert.That(model.WornOutfitFile1112(memberId), Is.EqualTo(made[1]), "deleting another outfit keeps the worn one");

                // 原装 → 穿上 returns her original look.
                canvas.GetComponentsInChildren<Button>(true).First(b => b.name == "WardrobeCard-0").onClick.Invoke();
                yield return new WaitForSecondsRealtime(.3f);
                canvas.GetComponentsInChildren<Button>(true).First(b => b.name == "WardrobeWear").onClick.Invoke();
                yield return new WaitForSecondsRealtime(.3f);
                Assert.That(model.WornOutfitFile1112(memberId), Is.Null);
                Assert.That(Portrait(memberId, false)?.name, Is.Not.EqualTo("Outfit-" + memberId));
            }
            finally
            {
                if (model != null && memberId != null)
                    foreach (string file in made) if (!string.IsNullOrEmpty(file)) model.DeleteOutfit1112(memberId, file);
                camera.targetTexture = null;
                rt.Release(); Object.DestroyImmediate(rt);
            }
        }

        private static Sprite Portrait(string id, bool thumbnail) => (Sprite)typeof(ChoSirenApp).Assembly.GetType("ChoSiren.Panels.PanelKit")
            .GetMethod("MemberSpriteOrNull", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Invoke(null, new object[] { id, thumbnail });

        private static void Invoke(object target, string method, params object[] args)
        {
            MethodInfo info = target.GetType().GetMethods(Private).FirstOrDefault(m => m.Name == method && m.GetParameters().Length == args.Length);
            if (info == null) throw new System.MissingMethodException(target.GetType().Name, method);
            info.Invoke(target, args);
        }

        private static void Shot(Camera camera, RenderTexture rt, string name)
        {
            foreach (Canvas stray in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!stray.isRootCanvas || stray.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                stray.renderMode = RenderMode.ScreenSpaceCamera; stray.worldCamera = camera; stray.planeDistance = 5;
            }
            Canvas.ForceUpdateCanvases();
            camera.Render();
            var previous = RenderTexture.active; RenderTexture.active = rt;
            var shot = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            shot.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); shot.Apply();
            RenderTexture.active = previous;
            Directory.CreateDirectory(Out);
            File.WriteAllBytes(Path.Combine(Out, name), shot.EncodeToPNG());
            Object.DestroyImmediate(shot);
        }
    }
}
