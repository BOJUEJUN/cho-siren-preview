using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ChoSiren.Tests.PlayMode
{
    /// <summary>
    /// Component-level contract for <see cref="BakedRegionMotion038"/>: the whole baked
    /// control body (board slice + live children) scales as one unit, the hit rect and
    /// sibling order never move, overlays stay non-interactive, and the resting frame
    /// is pixel-exact (scale 1, sheen 0) so no static/double frame can show.
    /// </summary>
    public class Qa038BakedRegionMotionPlayModeTests
    {
        private GameObject canvasGo;
        private Image boardImage;
        private GameObject buttonGo;
        private RectTransform buttonRect;
        private Texture2D boardTexture;
        private Sprite boardSprite;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            canvasGo = new GameObject("QaCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;

            GameObject boardGo = new GameObject("Board", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform boardRect = (RectTransform)boardGo.transform;
            boardRect.SetParent(canvasGo.transform, false);
            boardRect.anchorMin = boardRect.anchorMax = new Vector2(.5f, .5f);
            boardRect.pivot = new Vector2(.5f, .5f);
            boardRect.sizeDelta = new Vector2(400f, 800f);
            boardTexture = new Texture2D(400, 800, TextureFormat.RGBA32, false);
            Color32[] px = new Color32[400 * 800];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(30, 40, 60, 255);
            boardTexture.SetPixels32(px); boardTexture.Apply();
            boardSprite = Sprite.Create(boardTexture, new Rect(0, 0, 400, 800), new Vector2(.5f, .5f), 100f);
            boardImage = boardGo.GetComponent<Image>();
            boardImage.sprite = boardSprite;

            buttonGo = new GameObject("Baked", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonRect = (RectTransform)buttonGo.transform;
            buttonRect.SetParent(boardGo.transform, false);
            buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(.5f, .5f);
            buttonRect.pivot = new Vector2(.5f, .5f);
            buttonRect.anchoredPosition = Vector2.zero;
            buttonRect.sizeDelta = new Vector2(160f, 80f);
            buttonGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);

            GameObject label = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform labelRect = (RectTransform)label.transform;
            labelRect.SetParent(buttonGo.transform, false);
            labelRect.anchorMin = labelRect.anchorMax = new Vector2(.5f, .5f);
            labelRect.sizeDelta = new Vector2(60f, 20f);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (canvasGo != null) Object.Destroy(canvasGo);
            if (boardTexture != null) Object.Destroy(boardTexture);
            if (boardSprite != null) Object.Destroy(boardSprite);
            yield return null;
        }

        [UnityTest]
        public IEnumerator AttachBuildsBodyCoveringRegionAndKeepsHitFixed()
        {
            Vector2 hitSizeBefore = buttonRect.rect.size;
            // Region in board source px (top-left origin): centred 200x100 on the 400x800 board.
            BakedRegionMotion038.Attach(buttonGo, boardImage, new Rect(100f, 350f, 200f, 100f));
            yield return null;

            Assert.AreEqual(hitSizeBefore, buttonRect.rect.size, "hit rect must not change");

            Transform body = buttonGo.transform.Find(BakedRegionMotion038.BodyName);
            Assert.IsNotNull(body, "BakedRegionBody missing");
            Transform art = body.Find("RegionArt");
            Transform sheen = body.Find("RegionSheen");
            Assert.IsNotNull(art); Assert.IsNotNull(sheen);
            var artGraphic = art.GetComponent<Graphic>();
            var sheenGraphic = sheen.GetComponent<Graphic>();
            Assert.IsNotNull(artGraphic); Assert.IsNotNull(sheenGraphic);
            Assert.IsFalse(artGraphic.raycastTarget, "region overlay must not intercept input");
            Assert.IsFalse(sheenGraphic.raycastTarget, "sheen overlay must not intercept input");
            Assert.AreSame(boardTexture, artGraphic.mainTexture, "overlay must sample the board texture");
            Assert.AreSame(artGraphic.mainTexture, sheenGraphic.mainTexture);

            // Children are NEVER reparented: Transform.Find paths and sibling order
            // must keep working exactly as authored (dynamic labels rely on this).
            Transform label = buttonGo.transform.Find("Label");
            Assert.IsNotNull(label, "existing child must remain a direct child (Find path intact)");
            Assert.AreEqual(0, body.GetSiblingIndex(), "overlay must render under live children (first sibling)");
            Assert.AreEqual(1, label.GetSiblingIndex(), "sibling order must be preserved");
            Assert.Less(((RectTransform)label).anchoredPosition3D.magnitude, .01f,
                "child must keep its exact position at rest");

            // Region centre in board px (200,400) is the board centre -> overlays the
            // centre-anchored button exactly; art centre must sit at button centre.
            Vector3 artWorld = ((RectTransform)art).position;
            Vector3 buttonWorld = buttonRect.position;
            Assert.Less(Vector3.Distance(artWorld, buttonWorld), 1.5f,
                "region art must land on the baked control, not drift");
        }

        [UnityTest]
        public IEnumerator PointerSequenceScalesWholeBodyWithFixedFloorAndExactReset()
        {
            BakedRegionMotion038.Attach(buttonGo, boardImage, new Rect(100f, 350f, 200f, 100f));
            yield return null;
            RectTransform body = (RectTransform)buttonGo.transform.Find(BakedRegionMotion038.BodyName);
            RectTransform label = (RectTransform)buttonGo.transform.Find("Label");
            CanvasGroup sheen = body.Find("RegionSheen").GetComponent<CanvasGroup>();
            Assert.AreEqual(1f, body.localScale.x, .0001f);
            Assert.AreEqual(0f, sheen.alpha, .0001f);

            PointerEventData data = new PointerEventData(EventSystem.current);
            ExecuteEvents.Execute(buttonGo, data, ExecuteEvents.pointerEnterHandler);
            for (int i = 0; i < 20; i++) yield return null;
            Assert.Greater(body.localScale.x, 1.02f, "hover must grow the whole body");
            Assert.Greater(sheen.alpha, .1f, "hover must brighten the body");
            // Live children move with the body — the "whole visual body" requirement —
            // while remaining direct children (hierarchy untouched).
            Assert.Greater(label.localScale.x, 1.02f, "child art must scale with the body");
            Assert.AreEqual(buttonGo.transform, label.parent, "child stays a direct child");

            ExecuteEvents.Execute(buttonGo, data, ExecuteEvents.pointerDownHandler);
            for (int i = 0; i < 12; i++) yield return null;
            float pressed = body.localScale.x;
            Assert.GreaterOrEqual(pressed, 1f, "press must never expose the static frame");
            Assert.Less(pressed, 1.02f, "press should settle below the hover scale");

            ExecuteEvents.Execute(buttonGo, data, ExecuteEvents.pointerUpHandler);
            for (int i = 0; i < 20; i++) yield return null;
            Assert.Greater(body.localScale.x, 1.02f, "release over a still-hovered control returns to hover");

            ExecuteEvents.Execute(buttonGo, data, ExecuteEvents.pointerExitHandler);
            for (int i = 0; i < 20; i++) yield return null;
            Assert.AreEqual(1f, body.localScale.x, .0001f, "exit must restore the exact idle frame");
            Assert.AreEqual(0f, sheen.alpha, .0001f);
            Assert.Less(label.localScale.x - 1f, .0001f, "children must reset to baseline exactly");
            Assert.AreEqual(new Vector2(160f, 80f), buttonRect.rect.size, "hit rect stayed fixed");
        }
    }
}
