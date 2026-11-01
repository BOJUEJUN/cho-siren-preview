using System;
using System.IO;
using System.Linq;
using ChoSiren.Panels;
using ChoSiren.Systems;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ChoSiren.Tests
{
    public sealed class BattleAuthoredCloseup1019Tests
    {
        [Serializable] private sealed class Manifest { public Entry[] entries; }
        [Serializable] private sealed class Entry { public string key, file; }
        private bool hadReduce;
        private int reduce;

        [SetUp] public void Before()
        {
            hadReduce = PlayerPrefs.HasKey(BattlePresentationOptions.ReduceMotionKey);
            reduce = PlayerPrefs.GetInt(BattlePresentationOptions.ReduceMotionKey, 0);
            PlayerPrefs.SetInt(BattlePresentationOptions.ReduceMotionKey, 0);
        }
        [TearDown] public void After()
        {
            if (hadReduce) PlayerPrefs.SetInt(BattlePresentationOptions.ReduceMotionKey, reduce);
            else PlayerPrefs.DeleteKey(BattlePresentationOptions.ReduceMotionKey);
        }

        [Test]
        public void EveryAuthoredCutinIsItsOwnPsdCompositionEnlargedOverTheStageWithoutAnInventedTitleBoard()
        {
            var root = Stage();
            try
            {
                var cue = root.AddComponent<BattleActiveCloseup1019>();
                cue.Configure((RectTransform)root.transform, () => false, () => 1);
                foreach (string member in new[] { "xingli", "feiyin", "wubai", "yeying" })
                {
                    Texture2D texture; Sprite art = LoadActual(BattleAuthoredCutin1019.KeyForMember(member), out texture);
                    try
                    {
                        Assert.That(cue.Play(art, member, "原生主动技能", Color.cyan, authoredCutin: true), Is.True);
                        cue.AdvancePresentation(.2f / BattlePresentationOptions.Pace);
                        var banner = (RectTransform)root.transform.Find("ActiveSkillCloseup");
                        var window = (RectTransform)banner.GetComponentsInChildren<RectTransform>(true).First(t => t.name == "CloseupWindow");
                        var image = window.Find("OriginalCharacterCloseup").GetComponent<RawImage>();
                        Assert.That(cue.UsesAuthoredCutin, Is.True);
                        Assert.That(banner.GetComponentsInChildren<RectMask2D>(true), Is.Empty, "no clipper the close-up depends on; finished paper art gets no face crop");
                        Assert.That(image.texture, Is.SameAs(art.texture)); Assert.That(cue.Art, Is.SameAs(art));
                        var native = BattlePsdArt928.Find(art.name);
                        const float scale = 720f / 2946f;
                        Assert.That(native, Is.Not.Null);
                        // 2026-11-01 (赵金 "这个贴脸图大一点"): the same PSD group, uniformly enlarged — never stretched.
                        Rect placed = cue.ArtRect;
                        Assert.That(placed, Is.EqualTo(BattleActiveCloseup1019.AuthoredLayout(native.key, native.x, native.y, native.width, native.height)));
                        Assert.That(placed.width / placed.height, Is.EqualTo(native.width / native.height).Within(.001f), member + " keeps its PSD proportions");
                        Assert.That(placed.width, Is.GreaterThan(native.width * scale * 1.2f), member + " is clearly larger than its PSD size");
                        float onScreen = Mathf.Min(720f, placed.xMax) - Mathf.Max(0f, placed.xMin);
                        Assert.That(onScreen, Is.InRange(720f * .85f, 720f), member + " spans 85–100% of the screen width");
                        BattleActiveCloseup1019.AuthoredContent(native.key, out float contentTop, out float contentBottom);
                        Assert.That(placed.y + placed.height * contentBottom, Is.LessThanOrEqualTo(BattleActiveCloseup1019.RegionBottom + .01f), "stands on the member cards, never over them");
                        Assert.That(placed.y + placed.height * contentTop, Is.GreaterThanOrEqualTo(BattleActiveCloseup1019.RegionTop - .01f), "stays under the top HUD");
                        bool rightEdge = native.x + native.width >= 2946f - 24f;
                        if (rightEdge) Assert.That(placed.x + placed.width * ((2946f - native.x) / native.width), Is.EqualTo(720f).Within(.01f), "keeps its right-edge cut");
                        else Assert.That(placed.x + placed.width * (-native.x / native.width), Is.EqualTo(0f).Within(.01f), "keeps its left-edge cut");
                        Assert.That(window.sizeDelta.x, Is.EqualTo(placed.width).Within(.001f));
                        Assert.That(window.sizeDelta.y, Is.EqualTo(placed.height).Within(.001f));
                        // The shown part is cropped only at the stage edges / card line, with UVs that match it 1:1 (no stretch).
                        Rect vis = cue.VisibleRect;
                        Assert.That(vis.xMin, Is.GreaterThanOrEqualTo(-.01f)); Assert.That(vis.xMax, Is.LessThanOrEqualTo(720.01f));
                        Assert.That(vis.yMax, Is.LessThanOrEqualTo(BattleActiveCloseup1019.RegionBottom + 2.01f));
                        Assert.That(image.uvRect.width * art.texture.width / vis.width,
                            Is.EqualTo(image.uvRect.height * art.texture.height / vis.height).Within(.01f), "texels per unit equal on both axes");
                        Assert.That(image.uvRect.width * art.texture.width / vis.width, Is.EqualTo(art.texture.width / placed.width).Within(.001f));
                        var corners = new Vector3[4]; image.rectTransform.GetWorldCorners(corners);
                        foreach (Vector3 point in corners)
                        {
                            Vector3 local = window.InverseTransformPoint(point);
                            Assert.That(local.x, Is.InRange(window.rect.xMin - .001f, window.rect.xMax + .001f));
                            Assert.That(local.y, Is.InRange(window.rect.yMin - .001f, window.rect.yMax + .001f));
                        }
                        Assert.That(banner.anchoredPosition, Is.EqualTo(Vector2.zero));
                        Assert.That(banner.sizeDelta, Is.EqualTo(new Vector2(720, 6144f * scale)));
                        Assert.That(banner.GetComponentsInChildren<Transform>(true).First(t => t.name == "SlantedBackdrop").gameObject.activeInHierarchy, Is.False);
                        Assert.That(banner.GetComponentsInChildren<Transform>(true).First(t => t.name == "FallbackPaper").gameObject.activeSelf, Is.False);
                        Assert.That(banner.GetComponentsInChildren<Text>(true).All(t => !t.gameObject.activeInHierarchy), Is.True,
                            "native cut-ins must not add a large duplicate title board; the real member action label remains live");
                        Assert.That(banner.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
                        cue.Cancel();
                    }
                    finally { Object.DestroyImmediate(art); Object.DestroyImmediate(texture); }
                }
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void EnlargedAttackLayerStaysBehindEveryReadout()
        {
            var root = Stage();
            try
            {
                var model = new GameModel(() => new DateTime(2026, 10, 19, 12, 0, 0), new InMemorySaveStore());
                var battle = model.StartStageBattle("stage-1-1", 1019UL, out var message);
                Assert.That(battle, Is.Not.Null, message);
                var panel = TacticsBattlePanel.Open(root.transform, model, battle, null);
                panel.StopAllCoroutines();
                Transform psd = panel.GetComponentsInChildren<Transform>(true).First(t => t.name == "BattlePSD928");
                Transform basic = psd.Find("AuthoredBasicLayer");
                Assert.That(basic.GetSiblingIndex(), Is.GreaterThan(psd.Find("PsdEnemyStage").GetSiblingIndex()));
                foreach (string node in new[] { "PSD-dice-bar", "Dice-0", "Dice-5" })
                    Assert.That(basic.GetSiblingIndex(), Is.LessThan(psd.Find(node).GetSiblingIndex()), "enlarged lower body must not paint over " + node);
                foreach (Transform card in psd.Cast<Transform>().Where(t => t.name.StartsWith("Cell-P-")))
                    Assert.That(basic.GetSiblingIndex(), Is.LessThan(card.GetSiblingIndex()));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void UnlistedMembersNeverBorrowAnotherCharacterAndLegacyFallbackResetsAfterAuthoredMode()
        {
            Assert.That(BattleAuthoredCutin1019.KeyForMember("luoyu"), Is.Null);
            Assert.That(BattleAuthoredCutin1019.KeyForMember("charm"), Is.Null, "a race name is not an approved member identity");
            Assert.That(BattleAuthoredCutin1019.KeyForMember("another-mermaid"), Is.Null);
            var root = Stage(); Texture2D texture; Sprite art = LoadActual("cutin-charm", out texture);
            try
            {
                var cue = root.AddComponent<BattleActiveCloseup1019>();
                cue.Configure((RectTransform)root.transform, () => false, () => 1);
                Assert.That(cue.Play(art, "星璃", "技能", Color.cyan, authoredCutin: true), Is.True); cue.Cancel();
                Sprite ownPortrait = Resources.Load<Sprite>("Art/Members/member-xingli");
                Assert.That(ownPortrait, Is.Not.Null, "fallback uses the actual current member portrait resource");
                Assert.That(cue.Play(ownPortrait, "星璃", "原角色技能", Color.magenta, false, "xingli"), Is.True);
                var banner = (RectTransform)root.transform.Find("ActiveSkillCloseup");
                Transform Node(string n) => banner.GetComponentsInChildren<Transform>(true).First(t => t.name == n);
                var window = (RectTransform)Node("CloseupWindow");
                Assert.That(cue.UsesAuthoredCutin, Is.False);
                Assert.That(cue.VisibleRect.yMax, Is.LessThan(cue.ArtRect.yMax), "the illustration is cut by the paper's lower edge");
                // 2026-11-01: the fallback is a torn-paper card across the stage in the cut-ins' style, not a small strip.
                Assert.That(banner.sizeDelta.x, Is.EqualTo(720f).Within(.01f));
                Assert.That(Node("FallbackPaper").gameObject.activeSelf, Is.True);
                Assert.That(((RectTransform)Node("FallbackPaper")).rect.width, Is.GreaterThan(720f * .85f));
                Assert.That(cue.VisibleRect.yMin, Is.GreaterThanOrEqualTo(BattleActiveCloseup1019.RegionTop - .01f));
                Assert.That(cue.VisibleRect.yMax, Is.LessThanOrEqualTo(BattleActiveCloseup1019.RegionBottom + .01f));
                Assert.That(banner.GetComponentsInChildren<Text>(true).Any(t => t.text == "原角色技能" && t.gameObject.activeInHierarchy), Is.True);
                Assert.That(window.Find("OriginalCharacterCloseup").GetComponent<RawImage>().texture, Is.SameAs(ownPortrait.texture));
                Assert.That(cue.Art, Is.SameAs(ownPortrait));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(art); Object.DestroyImmediate(texture); }
        }

        private static Sprite LoadActual(string key, out Texture2D texture)
        {
            string folder = Path.Combine(Application.streamingAssetsPath, "BattlePsd928");
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Path.Combine(folder, "manifest.json")));
            Entry entry = Array.Find(manifest.entries, item => item.key == key);
            Assert.That(entry, Is.Not.Null, "the real79-entry manifest must include " + key);
            byte[] png = File.ReadAllBytes(Path.Combine(folder, entry.file));
            Assert.That(png[25], Is.EqualTo(6), "actual exported PNG must be RGBA");
            texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Assert.That(texture.LoadImage(png, false), Is.True);
            int clear = 0, opaque = 0;
            foreach (Color32 pixel in texture.GetPixels32())
            { if (pixel.a == 0) clear++; if (pixel.a == 255) opaque++; }
            Assert.That(clear, Is.GreaterThan(10)); Assert.That(opaque, Is.GreaterThan(10));
            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * .5f);
            sprite.name = key;
            return sprite;
        }

        private static GameObject Stage()
        {
            var root = new GameObject("AuthoredCloseupStage", typeof(RectTransform));
            var rect = (RectTransform)root.transform; rect.pivot = new Vector2(0, 1);
            rect.sizeDelta = new Vector2(720, 1501.63f); return root;
        }
    }
}
