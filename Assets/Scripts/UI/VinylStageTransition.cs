using System;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.UI
{
    /// <summary>
    /// Stage transition after the user's reference clip (2026-10-19): a black streak slashes
    /// across, a spinning vinyl record fills the screen (the scene switches underneath), then a
    /// diagonal cut reveals the idol's close-up with a slanted P5-style banner —
    /// CHO-SIREN / NAME / CLAN · ROLE — before the overlay clears.
    /// </summary>
    public sealed class VinylStageTransition : MonoBehaviour
    {
        private const float Cover = .34f, RevealStart = .78f, Hold = 1.95f, End = 2.25f;
        private static readonly Color Yellow = new Color32(255, 226, 0, 255);

        private Action switchScene;
        private bool switched;
        private float t;
        private RectTransform root, streak, vinyl, reveal, revealContent, banner, portrait, backVinyl;
        private CanvasGroup group;
        private Text bannerTitle, bannerName, bannerTag;

        public bool HasSwitched => switched;

        public static VinylStageTransition Play(Transform parent, Action switchScene, Sprite portrait,
            string name, string tag)
        {
            var existing = parent.GetComponentInChildren<VinylStageTransition>();
            if (existing != null) return existing;
            var go = new GameObject("VinylStageTransition", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            go.transform.SetParent(parent, false);
            var fx = go.AddComponent<VinylStageTransition>();
            fx.Build(portrait, name, tag);
            fx.switchScene = switchScene;
            fx.Advance(0f);
            return fx;
        }

        private void Build(Sprite art, string name, string tag)
        {
            root = (RectTransform)transform;
            Stretch(root);
            group = GetComponent<CanvasGroup>();
            Image blocker = GetComponent<Image>();
            blocker.color = new Color(0, 0, 0, 0.004f);
            blocker.raycastTarget = true;
            Rect r = root.rect;
            float big = Mathf.Max(r.width, r.height, 800f);
            Sprite vinylSprite = ReferenceArt038.Load("Art/Reference038/vfx-vinyl");
            Font font = Resources.Load<Font>("Fonts/NotoSansSC-Subset") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Diagonal reveal: a rotated mask that widens, holding an upright close-up scene.
            reveal = NewRect("Reveal", root);
            reveal.anchorMin = reveal.anchorMax = reveal.pivot = new Vector2(.5f, .5f);
            reveal.localEulerAngles = new Vector3(0, 0, -28f);
            reveal.sizeDelta = new Vector2(0f, big * 2.4f);
            var maskImage = reveal.gameObject.AddComponent<Image>();
            maskImage.raycastTarget = false;
            reveal.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            revealContent = NewRect("Content", reveal);
            revealContent.anchorMin = revealContent.anchorMax = revealContent.pivot = new Vector2(.5f, .5f);
            revealContent.localEulerAngles = new Vector3(0, 0, 28f);
            revealContent.sizeDelta = new Vector2(r.width, r.height);
            Image backdrop = NewImage("Backdrop", revealContent, null, new Color32(20, 14, 44, 255));
            Stretch(backdrop.rectTransform);
            Image cyan = NewImage("CyanEdge", revealContent, null, new Color32(0, 210, 240, 255));
            cyan.rectTransform.anchorMin = cyan.rectTransform.anchorMax = new Vector2(.5f, .5f);
            cyan.rectTransform.sizeDelta = new Vector2(r.width * .18f, big * 2f);
            cyan.rectTransform.anchoredPosition = new Vector2(r.width * .52f, 0);
            cyan.rectTransform.localEulerAngles = new Vector3(0, 0, -28f);
            backVinyl = NewImage("BackVinyl", revealContent, vinylSprite, new Color(.85f, .85f, .9f, 1f)).rectTransform;
            backVinyl.anchorMin = backVinyl.anchorMax = backVinyl.pivot = new Vector2(.5f, .5f);
            backVinyl.sizeDelta = Vector2.one * r.width * 1.35f;
            backVinyl.anchoredPosition = new Vector2(r.width * .18f, r.height * .26f);
            Image figure = NewImage("Portrait", revealContent, art, Color.white);
            figure.preserveAspect = true;
            portrait = figure.rectTransform;
            portrait.anchorMin = portrait.anchorMax = new Vector2(.5f, .5f);
            portrait.pivot = new Vector2(.5f, .78f);
            // Close-up: the figure is blown up so the frame holds face to hips.
            float h = r.height * 1.75f;
            float w = art != null ? h * art.rect.width / Mathf.Max(1f, art.rect.height) : h * .5f;
            portrait.sizeDelta = new Vector2(w, h);
            // Face sits about a fifth down the screen.
            portrait.anchoredPosition = new Vector2(r.width * .1f, r.height * .09f);

            // Slanted banner: black bar running from top-left down to the right.
            banner = NewRect("Banner", revealContent);
            banner.anchorMin = banner.anchorMax = banner.pivot = new Vector2(.5f, .5f);
            banner.localEulerAngles = new Vector3(0, 0, -62f);
            banner.sizeDelta = new Vector2(big * 2.2f, r.width * .36f);
            banner.anchoredPosition = BannerHome(r);
            Image bar = NewImage("Bar", banner, null, Color.black);
            Stretch(bar.rectTransform);
            float bh = banner.sizeDelta.y;
            bannerTitle = NewText("Title", banner, "CHO-SIREN", font, Mathf.RoundToInt(bh * .42f), Color.white);
            Place(bannerTitle.rectTransform, new Vector2(0, bh * .2f), new Vector2(big * 1.6f, bh * .5f));
            bannerName = NewText("Name", banner, name, font, Mathf.RoundToInt(bh * .26f), Yellow);
            Place(bannerName.rectTransform, new Vector2(-bh * .55f, -bh * .14f), new Vector2(big * 1.6f, bh * .3f));
            bannerTag = NewText("Tag", banner, tag, font, Mathf.RoundToInt(bh * .13f), Color.white);
            Place(bannerTag.rectTransform, new Vector2(-bh * .3f, -bh * .36f), new Vector2(big * 1.6f, bh * .18f));

            // The opening slash.
            Image slash = NewImage("Streak", root, null, Color.black);
            streak = slash.rectTransform;
            streak.anchorMin = streak.anchorMax = streak.pivot = new Vector2(.5f, .5f);
            streak.localEulerAngles = new Vector3(0, 0, -62f);
            streak.sizeDelta = new Vector2(big * 2.4f, 0f);
            // Spinning record that covers the screen while the scene switches.
            vinyl = NewImage("Vinyl", root, vinylSprite, Color.white).rectTransform;
            vinyl.anchorMin = vinyl.anchorMax = vinyl.pivot = new Vector2(.5f, .5f);
            vinyl.sizeDelta = Vector2.one * Mathf.Max(r.width * 1.9f, r.height * .9f);

        }

        private void Update() => Advance(Time.unscaledDeltaTime);

        public void Advance(float seconds)
        {
            // Long first frames (scene build) must not skip the show.
            t += Mathf.Min(Mathf.Max(0f, seconds), 1f / 30f);
            if (BattlePresentationOptions.ReduceMotion && !switched) t = Mathf.Max(t, Cover);
            if (!switched && t >= Cover)
            {
                switched = true;
                var callback = switchScene; switchScene = null;
                try { callback?.Invoke(); }
                catch (Exception e) { Debug.LogException(e); Destroy(gameObject); return; }
                transform.SetAsLastSibling();
            }
            Rect r = root.rect;
            float big = Mathf.Max(r.width, r.height, 800f);

            // 0 – .22: a thin black streak thickens across the screen.
            float s = Mathf.Clamp01(t / .22f);
            streak.sizeDelta = new Vector2(big * 2.4f, Mathf.Lerp(6f, big * 2.2f, s * s));
            streak.gameObject.SetActive(t < RevealStart + .3f);

            // .12 – reveal: the record punches in and spins fast, slowing as the cut arrives.
            float vin = Mathf.Clamp01((t - .12f) / .2f);
            vinyl.gameObject.SetActive(t >= .12f && t < RevealStart + .3f);
            vinyl.localScale = Vector3.one * Mathf.Lerp(.35f, 1f, Ease(vin));
            vinyl.localEulerAngles = new Vector3(0, 0, -t * 900f);

            // Reveal cut widens over the record.
            float cut = Mathf.Clamp01((t - RevealStart) / .22f);
            reveal.sizeDelta = new Vector2(Ease(cut) * big * 2.6f, big * 2.4f);
            reveal.gameObject.SetActive(t >= RevealStart);
            backVinyl.localEulerAngles = new Vector3(0, 0, -t * 120f);
            // Slow push on the idol while the banner holds.
            float hold = Mathf.Clamp01((t - RevealStart) / (Hold - RevealStart));
            portrait.localScale = Vector3.one * Mathf.Lerp(1.08f, 1f, Ease(hold));
            // Banner slides down its own axis; the name stamps in a beat later.
            float bar = Ease(Mathf.Clamp01((t - RevealStart - .08f) / .22f));
            banner.anchoredPosition = BannerHome(r) +
                (Vector2)(banner.localRotation * Vector3.right) * Mathf.Lerp(big * 1.2f, 0f, bar);
            float stamp = Mathf.Clamp01((t - RevealStart - .22f) / .14f);
            bannerName.rectTransform.localScale = Vector3.one * (stamp <= 0 ? 0 : Mathf.Lerp(1.6f, 1f, Ease(stamp)));
            bannerTag.color = new Color(1, 1, 1, Mathf.Clamp01((t - RevealStart - .3f) / .15f));

            // Clear out.
            float exit = Mathf.Clamp01((t - Hold) / (End - Hold));
            group.alpha = 1f - exit;
            if (t >= End) Destroy(gameObject);
        }

        /// <summary>Tap after the switch to skip the hold.</summary>
        private void LateUpdate()
        {
            if (switched && t > RevealStart + .3f && t < Hold && Input.GetMouseButtonDown(0)) t = Hold;
        }

        private static Vector2 BannerHome(Rect r) => new Vector2(-r.width * .1f, r.height * .2f);

        private static float Ease(float x) => 1f - Mathf.Pow(1f - Mathf.Clamp01(x), 3f);

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite; image.color = color; image.raycastTarget = false;
            return image;
        }

        private static Text NewText(string name, Transform parent, string value, Font font, int size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = font; text.text = value; text.fontSize = size; text.color = color;
            text.fontStyle = FontStyle.BoldAndItalic;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
