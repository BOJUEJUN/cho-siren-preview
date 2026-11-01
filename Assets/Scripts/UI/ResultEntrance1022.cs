using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.UI
{
    /// <summary>
    /// R2 result screen entrance (research 2026-10-21 §5.9): the red band wipes in, the MVP slides in
    /// from the side, VICTORY stamps, the MVP line slides in, team avatars pop one by one while their
    /// damage numbers roll up, then rewards drop in and the 继续 button rises. Every element ends in
    /// exactly its laid-out state; 减弱动画 skips straight to it.
    /// </summary>
    public sealed class ResultEntrance1022 : MonoBehaviour
    {
        struct Rolling { public Text Text; public long Target; public float Start; }
        struct Slide { public RectTransform Rect; public Vector2 Home; public Vector2 From; public float Start, Length; public CanvasGroup Group; }
        struct Pop { public RectTransform Rect; public float Start; }

        readonly List<Rolling> numbers = new List<Rolling>();
        readonly List<Slide> slides = new List<Slide>();
        readonly List<Pop> pops = new List<Pop>();
        RectTransform band, title;
        Vector3 bandScale, titleScale;
        Action<string> sfx;
        float clock;
        bool stamped, running;
        const float End = 1.6f;

        public static ResultEntrance1022 Play(RectTransform card, Text titleText, RectTransform reward, RectTransform button, Action<string> sound)
        {
            if (card == null) return null;
            var entrance = UnityNull.Real(card.GetComponent<ResultEntrance1022>()) ?? card.gameObject.AddComponent<ResultEntrance1022>();
            entrance.Begin(card, titleText, reward, button, sound);
            return entrance;
        }

        void Begin(RectTransform card, Text titleText, RectTransform reward, RectTransform button, Action<string> sound)
        {
            Finish();
            numbers.Clear(); slides.Clear(); pops.Clear();
            sfx = sound; clock = 0f; stamped = false;
            Transform screens = card.Find("ResultConcertScreens");
            RectTransform hero = null;
            if (screens != null)
            {
                foreach (Transform child in screens)
                {
                    if (!child.gameObject.activeSelf) continue;
                    if (child.name.StartsWith("ResultMember-", StringComparison.Ordinal))
                    {
                        if (hero == null && child.GetComponent<RectMask2D>() != null) { hero = (RectTransform)child; continue; }
                        pops.Add(new Pop { Rect = (RectTransform)child, Start = .55f + pops.Count * .09f });
                        AddNumber(child.Find("Output") as RectTransform, .6f + pops.Count * .09f);
                    }
                    else if (child.name == "MvpBand") band = (RectTransform)child;
                    else if (child.name == "MvpLine")
                    {
                        AddSlide((RectTransform)child, new Vector2(-520f, 0f), .35f, .22f);
                        AddNumber(child.Find("Output") as RectTransform, .45f);
                    }
                }
            }
            if (hero != null)
            {
                var portrait = hero.Find("ResultPortrait") as RectTransform;
                if (portrait != null) AddSlide(portrait, new Vector2(260f, 0f), .1f, .3f);
            }
            if (reward != null) AddSlide(reward, new Vector2(0f, 26f), 1.05f, .18f);
            if (button != null) AddSlide(button, new Vector2(0f, -40f), 1.2f, .2f);
            title = titleText != null ? titleText.rectTransform : null;
            if (band != null) bandScale = band.localScale;
            if (title != null) titleScale = title.localScale;
            running = !BattlePresentationOptions.ReduceMotion;
            if (running) Pose(); else Finish();
        }

        void AddSlide(RectTransform rect, Vector2 offset, float start, float length)
        {
            if (rect == null) return;
            var group = UnityNull.Real(rect.GetComponent<CanvasGroup>()) ?? rect.gameObject.AddComponent<CanvasGroup>();
            slides.Add(new Slide { Rect = rect, Home = rect.anchoredPosition, From = rect.anchoredPosition + offset, Start = start, Length = length, Group = group });
        }

        void AddNumber(RectTransform rect, float start)
        {
            var text = rect != null ? rect.GetComponent<Text>() : null;
            if (text == null) return;
            string digits = text.text.Replace(",", string.Empty);
            if (!long.TryParse(digits, out long value)) return;
            numbers.Add(new Rolling { Text = text, Target = value, Start = start });
        }

        static float Expo(float t) { t = Mathf.Clamp01(t); return t >= 1f ? 1f : 1f - Mathf.Pow(2f, -10f * t); }
        static float Back(float t) { t = Mathf.Clamp01(t); const float c = 1.7f; float u = t - 1f; return 1f + (c + 1f) * u * u * u + c * u * u; }

        void Update()
        {
            if (!running) return;
            clock += Mathf.Min(Time.unscaledDeltaTime, .05f);
            if (clock >= End) { Finish(); return; }
            Pose();
        }

        void Pose()
        {
            float t = clock;
            if (band != null)
            {
                float w = Expo(t / .17f);
                band.localScale = new Vector3(bandScale.x * w, bandScale.y, bandScale.z);
            }
            if (title != null)
            {
                float s = Mathf.Clamp01((t - .22f) / .07f);
                title.localScale = titleScale * (t < .22f ? 0f : Mathf.Lerp(1.7f, 1f, s));
                if (!stamped && t >= .22f) { stamped = true; sfx?.Invoke("stamp"); }
            }
            foreach (Slide slide in slides)
            {
                float k = Expo((t - slide.Start) / slide.Length);
                slide.Rect.anchoredPosition = Vector2.LerpUnclamped(slide.From, slide.Home, k);
                slide.Group.alpha = Mathf.Clamp01((t - slide.Start) / (slide.Length * .5f));
            }
            foreach (Pop pop in pops)
                pop.Rect.localScale = Vector3.one * (t < pop.Start ? 0f : Back((t - pop.Start) / .22f));
            foreach (Rolling number in numbers)
            {
                float k = Mathf.Clamp01((t - number.Start) / .55f);
                k = 1f - (1f - k) * (1f - k);
                number.Text.text = ((long)Math.Round(number.Target * k)).ToString("N0");
            }
        }

        /// <summary>Snaps everything to its final laid-out state.</summary>
        void Finish()
        {
            running = false;
            if (band != null) band.localScale = bandScale;
            if (title != null) title.localScale = titleScale;
            foreach (Slide slide in slides)
            {
                if (slide.Rect == null) continue;
                slide.Rect.anchoredPosition = slide.Home;
                if (slide.Group != null) slide.Group.alpha = 1f;
            }
            foreach (Pop pop in pops) if (pop.Rect != null) pop.Rect.localScale = Vector3.one;
            foreach (Rolling number in numbers) if (number.Text != null) number.Text.text = number.Target.ToString("N0");
        }

        void OnDisable() => Finish();
    }
}
