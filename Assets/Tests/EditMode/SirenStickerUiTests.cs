using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ChoSiren.UI;

namespace ChoSiren.Tests
{
    public sealed class SirenStickerUiTests
    {
        [Test]
        public void PrimaryIsTheGreenStickerAndExitTheDarkOneWithoutChangingClicks()
        {
            var resume = new GameObject("ResumeBattle", typeof(RectTransform), typeof(Image), typeof(Button));
            var exit = new GameObject("BattleExit", typeof(RectTransform), typeof(Image), typeof(Button));
            try
            {
                int clicks = 0;
                resume.GetComponent<Button>().onClick.AddListener(() => clicks++);
                SirenStickerUi.SkinButton(resume, true);
                SirenStickerUi.SkinButton(exit);
                var primary = resume.transform.Find("SirenButtonFace").GetComponent<Image>();
                var secondary = exit.transform.Find("SirenButtonFace").GetComponent<Image>();
                // Unified design (2026-10-25): one green primary sticker everywhere, the dark sticker for everything else.
                Assert.That(primary.sprite, Is.SameAs(SirenStickerUi.PrimarySprite));
                Assert.That(secondary.sprite, Is.SameAs(SirenStickerUi.ButtonSprite));
                Assert.That(primary.type, Is.EqualTo(Image.Type.Sliced));
                Assert.That(SirenStickerUi.ActionPlate, Is.SameAs(secondary.sprite), "All shared action-plate callers must retire the AI shard artwork.");
                resume.GetComponent<Button>().onClick.Invoke();
                Assert.That(clicks, Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(resume); Object.DestroyImmediate(exit); }
        }

        [Test]
        public void ReapplyingSkinPreservesHitRectCallbacksAndDecorationRaycasts()
        {
            var root = new GameObject("ReviewAction", typeof(RectTransform), typeof(Image), typeof(Button));
            try
            {
                var rect = root.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(220f, 64f); rect.anchoredPosition = new Vector2(38f, -52f);
                var button = root.GetComponent<Button>(); int clicks = 0;
                button.onClick.AddListener(() => clicks++);
                SirenStickerUi.SkinButton(root, true);
                int children = root.transform.childCount;
                SirenStickerUi.SkinButton(root, false);
                Assert.That(root.transform.childCount, Is.EqualTo(children), "Skin updates cannot accumulate duplicate visual layers");
                Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(220f, 64f)));
                Assert.That(rect.anchoredPosition, Is.EqualTo(new Vector2(38f, -52f)));
                button.onClick.Invoke(); Assert.That(clicks, Is.EqualTo(1));
                foreach (var image in root.GetComponentsInChildren<Image>())
                    if (image.gameObject != root) Assert.That(image.raycastTarget, Is.False);
                button.interactable = false; SirenStickerUi.SkinButton(root, true);
                Assert.That(button.interactable, Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void ReapplyingTitleSkinKeepsTheReadingLayerInFront()
        {
            var parent = new GameObject("TitleHost", typeof(RectTransform));
            var node = new GameObject("Title", typeof(RectTransform), typeof(Text));
            node.transform.SetParent(parent.transform, false);
            try
            {
                var title = node.GetComponent<Text>(); title.text = "任务";
                title.rectTransform.sizeDelta = new Vector2(360, 60);
                SirenStickerUi.SkinTitle(title, true); SirenStickerUi.SkinTitle(title, true);
                Assert.That(parent.transform.childCount, Is.EqualTo(2), "复用标题装饰不能重复堆层。");
                Assert.That(node.transform.GetSiblingIndex(), Is.EqualTo(1), "纸边必须在文字后面绘制，不能盖住标题。");
            }
            finally { Object.DestroyImmediate(parent); }
        }

        [Test]
        public void SameNamedTitlesKeepIndependentDecorationOwners()
        {
            var parent = new GameObject("ResultTitleHost", typeof(RectTransform));
            try
            {
                var first = new GameObject("Text", typeof(RectTransform), typeof(Text));
                var second = new GameObject("Text", typeof(RectTransform), typeof(Text));
                first.transform.SetParent(parent.transform, false);
                second.transform.SetParent(parent.transform, false);
                SirenStickerUi.SkinTitle(first.GetComponent<Text>(), true);
                SirenStickerUi.SkinTitle(second.GetComponent<Text>(), true);
                SirenStickerUi.SkinTitle(first.GetComponent<Text>(), true);
                var a = first.GetComponent<SirenStickerTitleBinding>().Plate;
                var b = second.GetComponent<SirenStickerTitleBinding>().Plate;
                Assert.That(a, Is.Not.SameAs(b));
                Assert.That(parent.transform.childCount, Is.EqualTo(4));
                Assert.That(a.transform.GetSiblingIndex(), Is.LessThan(first.transform.GetSiblingIndex()));
                Assert.That(b.transform.GetSiblingIndex(), Is.LessThan(second.transform.GetSiblingIndex()));
            }
            finally { Object.DestroyImmediate(parent); }
        }

        [Test]
        public void ModalBodyAndLiveCaptionSurviveRestyling()
        {
            var root = new GameObject("ModalAction", typeof(RectTransform), typeof(Image), typeof(Button));
            try
            {
                var body = new GameObject("Body", typeof(RectTransform), typeof(Image)); body.transform.SetParent(root.transform, false);
                var label = new GameObject("Label", typeof(RectTransform), typeof(Text)); label.transform.SetParent(body.transform, false);
                var text = label.GetComponent<Text>(); text.text = "领取 120 星光币";
                SirenStickerUi.SkinButton(root, true);
                Assert.That(label.activeInHierarchy, Is.True);
                Assert.That(text.text, Is.EqualTo("领取 120 星光币"));
                Assert.That(body.GetComponent<Image>().enabled, Is.False);
                Assert.That(body.transform.Find("SirenButtonFace"), Is.Not.Null);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
