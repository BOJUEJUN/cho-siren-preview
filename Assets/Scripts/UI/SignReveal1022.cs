using System;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.UI
{
    /// <summary>
    /// Signing a member is the gacha moment (2026-10-21): a full-screen P5 reveal instead of a
    /// small modal — white flash, black halftone wedge and red band slash in, the member's full
    /// portrait slides up from the right on a red impact star, her name and 「签约成功」 stamp in.
    /// Tap (or wait) to continue; the regular signed-card modal follows.
    /// </summary>
    public sealed class SignReveal1022 : MonoBehaviour
    {
        RectTransform root, wedge, band, figure, star, nameRect, stampRect, tapRect;
        Image flash, figureImage;
        Text tapText;
        Action done, sfxStamp;
        float clock;
        bool stamped, finished;
        const float AutoFinish = 4f;

        public static SignReveal1022 Play(Transform host, Font font, Sprite portrait, string memberName, string subtitle,
            Action playStamp, Action onDone)
        {
            if (host == null) { onDone?.Invoke(); return null; }
            var go = new GameObject("SignReveal1022", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            // Full screen above the HUD and nav: host on the root canvas, not the page content.
            var canvas = host.GetComponentInParent<Canvas>();
            go.transform.SetParent(canvas != null ? canvas.rootCanvas.transform : host, false);
            go.transform.SetAsLastSibling();
            var reveal = go.AddComponent<SignReveal1022>();
            reveal.done = onDone; reveal.sfxStamp = playStamp;
            reveal.Build(font, portrait, memberName, subtitle);
            if (BattlePresentationOptions.ReduceMotion) reveal.clock = 1.2f;
            reveal.Pose();
            return reveal;
        }

        static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
        static void Centre(RectTransform r, Vector2 at, Vector2 size) { r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f); r.anchoredPosition = at; r.sizeDelta = size; }

        Image Img(string name, Transform parent, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>(); image.sprite = sprite; image.color = color; image.raycastTarget = false;
            return image;
        }

        Text Label(string name, Transform parent, Font font, string value, int size, Color color, float edge)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = font; t.text = value; t.fontSize = size; t.color = color; t.fontStyle = FontStyle.BoldAndItalic;
            t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            var o = go.AddComponent<Outline>(); o.effectColor = Color.black; o.effectDistance = new Vector2(edge, -edge);
            return t;
        }

        void Build(Font font, Sprite portrait, string memberName, string subtitle)
        {
            root = (RectTransform)transform; Stretch(root);
            var bg = GetComponent<Image>(); bg.color = new Color(.02f, .01f, .05f, 1f); bg.raycastTarget = true; // linear space: <1 shows through
            GetComponent<Button>().transition = Selectable.Transition.None;
            GetComponent<Button>().onClick.AddListener(Tap);
            var tilt = new GameObject("Tilt", typeof(RectTransform)).GetComponent<RectTransform>();
            tilt.SetParent(transform, false); Centre(tilt, new Vector2(0, -60), new Vector2(1400, 1400));
            tilt.localEulerAngles = new Vector3(0, 0, 10f);
            var w = Img("Wedge", tilt, null, Color.black); wedge = w.rectTransform; Centre(wedge, new Vector2(0, 120), new Vector2(1600, 560));
            var half = Img("Halftone", wedge, CelShapes.Halftone, Brand1025.TealA(85));
            half.type = Image.Type.Tiled; half.pixelsPerUnitMultiplier = 2f; Stretch(half.rectTransform);
            var b = Img("Band", tilt, null, Brand1025.Teal); band = b.rectTransform; Centre(band, new Vector2(0, -230), new Vector2(1600, 210));
            var edge = Img("Edge", band, null, Color.white);
            var er = edge.rectTransform; er.anchorMin = new Vector2(0, 1); er.anchorMax = Vector2.one; er.pivot = new Vector2(.5f, 1); er.sizeDelta = new Vector2(0, 12);
            // A star holder: the black rim draws first, the teal star over it (children draw after their parent).
            star = new GameObject("StarHolder", typeof(RectTransform)).GetComponent<RectTransform>();
            star.SetParent(transform, false); Centre(star, new Vector2(120, 140), new Vector2(760, 760));
            var sr = Img("StarRim", star, CelShapes.Jagged, Color.black); Centre(sr.rectTransform, Vector2.zero, new Vector2(840, 840));
            var s = Img("Star", star, CelShapes.Jagged, Brand1025.Teal); Centre(s.rectTransform, Vector2.zero, new Vector2(760, 760));
            figureImage = Img("Member", transform, portrait, Color.white); figureImage.preserveAspect = true; figure = figureImage.rectTransform;
            Centre(figure, new Vector2(110, 120), new Vector2(640, 1100));
            var nameText = Label("Name", transform, font, memberName, 104, Color.white, 6f); nameRect = nameText.rectTransform;
            Centre(nameRect, new Vector2(-120, -330), new Vector2(700, 140)); nameRect.localEulerAngles = new Vector3(0, 0, 10f);
            var stampPlate = Img("Stamp", transform, null, Color.white); stampRect = stampPlate.rectTransform;
            Centre(stampRect, new Vector2(-170, -460), new Vector2(330, 84)); stampRect.localEulerAngles = new Vector3(0, 0, 10f);
            var stampText = Label("StampText", stampRect, font, "签约成功", 50, new Color32(20, 10, 24, 255), 0f);
            Stretch(stampText.rectTransform);
            var sub = Label("Sub", transform, font, subtitle ?? string.Empty, 28, new Color32(255, 214, 92, 255), 3f);
            Centre(sub.rectTransform, new Vector2(-110, -540), new Vector2(700, 50)); sub.rectTransform.localEulerAngles = new Vector3(0, 0, 10f);
            tapText = Label("Tap", transform, font, "点击继续 ▼", 26, Color.white, 2f); tapRect = tapText.rectTransform;
            tapRect.anchorMin = tapRect.anchorMax = tapRect.pivot = new Vector2(.5f, 0f); tapRect.anchoredPosition = new Vector2(0, 90); tapRect.sizeDelta = new Vector2(400, 50);
            flash = Img("Flash", transform, null, Color.white); Stretch(flash.rectTransform);
            if (BattleFxArt1021.Additive != null) flash.material = BattleFxArt1021.Additive;
        }

        static float OutExpo(float t) { t = Mathf.Clamp01(t); return t >= 1f ? 1f : 1f - Mathf.Pow(2f, -10f * t); }
        static float Back(float t) { t = Mathf.Clamp01(t); const float c = 1.8f; float u = t - 1f; return 1f + (c + 1f) * u * u * u + c * u * u; }

        void Update()
        {
            if (finished) return;
            clock += Mathf.Min(Time.unscaledDeltaTime, .05f);
            Pose();
            if (clock >= AutoFinish) Tap();
        }

        void Pose()
        {
            float t = clock;
            flash.color = new Color(1, 1, 1, .9f * (1f - Mathf.Clamp01(t / .25f)));
            wedge.anchoredPosition = new Vector2(Mathf.Lerp(-1600f, 0f, OutExpo((t - .05f) / .2f)), 120f);
            band.anchoredPosition = new Vector2(Mathf.Lerp(1600f, 0f, OutExpo((t - .12f) / .2f)), -230f);
            star.localScale = Vector3.one * Back((t - .25f) / .3f);
            star.localEulerAngles = new Vector3(0, 0, t * 12f);
            figure.anchoredPosition = new Vector2(Mathf.Lerp(560f, 110f, OutExpo((t - .3f) / .4f)), Mathf.Lerp(40f, 120f, OutExpo((t - .3f) / .4f)));
            figureImage.color = new Color(1, 1, 1, Mathf.Clamp01((t - .3f) / .1f));
            float n = Mathf.Clamp01((t - .62f) / .08f);
            nameRect.localScale = Vector3.one * (t < .62f ? 0f : Mathf.Lerp(1.8f, 1f, n));
            float st = Mathf.Clamp01((t - .82f) / .08f);
            stampRect.localScale = Vector3.one * (t < .82f ? 0f : Mathf.Lerp(1.9f, 1f, st));
            if (!stamped && t >= .82f) { stamped = true; sfxStamp?.Invoke(); }
            tapText.color = new Color(1, 1, 1, t < 1.3f ? 0f : .55f + .45f * Mathf.Sin(t * 5f));
        }

        void Tap()
        {
            if (finished) return;
            if (clock < 1f) { clock = 1.2f; Pose(); return; } // first tap skips to the end pose
            finished = true;
            var callback = done; done = null;
            Destroy(gameObject);
            try { callback?.Invoke(); } catch (Exception e) { Debug.LogException(e); }
        }
    }
}
