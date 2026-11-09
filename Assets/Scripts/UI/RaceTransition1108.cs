using System;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.UI
{
    /// <summary>
    /// 2026-11-08 (赵金 精灵 / 人鱼 / 恶魔 / 魅族转场.psd + 转场参考视频): tapping a member on the 团队 page cuts to her
    /// race's cyan-silhouette card before her detail page, after the reference: a cyan panel sweeps in from the right
    /// with a diamond flash where the finger was, the silhouette card wipes in and slowly pushes, cyan floods back,
    /// and the detail page (opened under full cover) is revealed as the cyan panel slides off to the right.
    /// Reduce Motion skips straight to the page.
    /// </summary>
    public sealed class RaceTransition1108 : MonoBehaviour
    {
        public static readonly Color32 Cyan = new Color32(21, 235, 247, 255);
        const float SweepEnd = .14f, RevealEnd = .32f, HoldEnd = .92f, FloodEnd = 1.04f, SliceEnd = 1.42f;
        const int Slices = 8;

        RectTransform root;
        Image sweep, flood, diamond;
        RawImage card;
        RectTransform[] slices;
        Action openPage;
        bool opened;
        float time;
        Vector2 tap;

        public static bool Playing { get; private set; }
        /// <summary>Batch-mode test runs skip page transitions (so navigation tests stay instant) unless a test opts in.</summary>
        public static bool AllowInBatch;
        public float Clock => time;
        public bool PageOpened => opened;

        /// <summary>Art key for a member race string ("海灵族 · 人鱼" → mermaid …).</summary>
        public static string RaceKey(string race)
        {
            race = race ?? string.Empty;
            if (race.Contains("人鱼") || race.Contains("海")) return "mermaid";
            if (race.Contains("恶魔") || race.Contains("魔")) return "demon";
            if (race.Contains("精灵")) return "elf";
            return "charm";
        }

        public static Texture2D Art(string race) => Resources.Load<Texture2D>("Art/Transition1108/race-" + RaceKey(race));

        /// <summary>Plays over <paramref name="host"/> and calls <paramref name="open"/> once the screen is fully cyan.</summary>
        public static RaceTransition1108 Play(Transform host, string race, Vector2 tapLocal, Action open)
        {
            Texture2D art = Art(race);
            if (host == null || art == null || Playing || BattlePresentationOptions.ReduceMotion || !Application.isPlaying
                || Application.isBatchMode && !AllowInBatch)
            {
                open?.Invoke();
                return null;
            }
            var go = new GameObject("RaceTransition1108", typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(host, false);
            go.transform.SetAsLastSibling();
            var fx = go.AddComponent<RaceTransition1108>();
            fx.Build(art, tapLocal, open);
            return fx;
        }

        void Build(Texture2D art, Vector2 tapLocal, Action open)
        {
            Playing = true;
            openPage = open; tap = tapLocal;
            root = (RectTransform)transform; Stretch(root);
            var blocker = gameObject.AddComponent<Image>(); blocker.color = new Color(0, 0, 0, .004f); blocker.raycastTarget = true;
            card = New<RawImage>("RaceCard"); card.texture = art; Center(card.rectTransform);
            sweep = New<Image>("CyanSweep"); sweep.color = Cyan; Stretch(sweep.rectTransform);
            flood = New<Image>("CyanFlood"); flood.color = new Color(Cyan.r / 255f, Cyan.g / 255f, Cyan.b / 255f, 0f); Stretch(flood.rectTransform);
            slices = new RectTransform[Slices];
            for (int i = 0; i < Slices; i++)
            {
                Image bar = New<Image>("Slice-" + i); bar.color = Cyan;
                RectTransform r = bar.rectTransform;
                r.anchorMin = new Vector2(i / (float)Slices, 0f); r.anchorMax = new Vector2((i + 1) / (float)Slices, 1f);
                r.offsetMin = new Vector2(-1f, 0f); r.offsetMax = new Vector2(1f, 0f);
                r.pivot = new Vector2(i % 2 == 0 ? 0f : 1f, .5f);
                bar.enabled = false;
                slices[i] = r;
            }
            diamond = New<Image>("TapDiamond"); diamond.color = Color.white;
            diamond.rectTransform.anchorMin = diamond.rectTransform.anchorMax = new Vector2(.5f, .5f);
            diamond.rectTransform.sizeDelta = new Vector2(46f, 46f);
            diamond.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
            diamond.gameObject.AddComponent<Outline>().effectColor = new Color(1f, 1f, 1f, .6f);
            Pose();
        }

        T New<T>(string name) where T : Graphic
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(T));
            go.transform.SetParent(transform, false);
            var g = go.GetComponent<T>(); g.raycastTarget = false; return g;
        }

        void Update()
        {
            time += Mathf.Min(Time.unscaledDeltaTime, .05f);
            if (!opened && time >= FloodEnd) OpenPage();
            Pose();
            if (time >= SliceEnd) Finish();
        }

        /// <summary>Test hook: jump the clock.</summary>
        public void Advance(float seconds)
        {
            time += Mathf.Max(0f, seconds);
            if (!opened && time >= FloodEnd) OpenPage();
            Pose();
            if (time >= SliceEnd) Finish();
        }

        void OpenPage()
        {
            opened = true;
            Action open = openPage; openPage = null;
            try { open?.Invoke(); } catch (Exception e) { Debug.LogException(e); }
            if (this != null) transform.SetAsLastSibling();
        }

        void Pose()
        {
            if (root == null) return;
            float w = Mathf.Max(1f, root.rect.width), h = Mathf.Max(1f, root.rect.height);
            // 1) cyan sweep from the right edge
            float s = Ease(Mathf.Clamp01(time / SweepEnd));
            float uncover = Ease(Mathf.Clamp01((time - SweepEnd) / (RevealEnd - SweepEnd)));
            // In from the right edge, then it recedes to the left as the card wipes in behind it.
            sweep.rectTransform.offsetMin = new Vector2(w * (1f - s), 0f);
            sweep.rectTransform.offsetMax = new Vector2(w * (1f - s) - w * uncover, 0f);
            sweep.enabled = time < RevealEnd;
            // diamond flash at the tap point
            float d = Mathf.Clamp01(time / .3f);
            diamond.enabled = time < .3f;
            diamond.rectTransform.anchoredPosition = tap;
            diamond.rectTransform.localScale = Vector3.one * Mathf.Lerp(.4f, 1.5f, d);
            diamond.color = new Color(1f, 1f, 1f, 1f - d * d);
            // 2) the silhouette card wipes in under the sweep (revealed right to left) and slowly pushes in
            Texture tex = card.texture;
            float aspect = tex != null ? tex.width / (float)Mathf.Max(1, tex.height) : 1080f / 1920f;
            float cover = Mathf.Max(w / aspect, h);
            float push = Mathf.Lerp(1f, 1.035f, Mathf.Clamp01((time - SweepEnd) / (HoldEnd - SweepEnd)));
            card.rectTransform.sizeDelta = new Vector2(cover * aspect, cover);
            card.rectTransform.localScale = Vector3.one * push;
            float reveal = Ease(Mathf.Clamp01((time - SweepEnd) / (RevealEnd - SweepEnd)));
            card.enabled = time >= SweepEnd && time < FloodEnd + .02f;
            card.uvRect = new Rect(1f - reveal, 0f, reveal, 1f);
            card.rectTransform.pivot = new Vector2(.5f, .5f);
            card.rectTransform.sizeDelta = new Vector2(cover * aspect * reveal, cover);
            card.rectTransform.anchoredPosition = new Vector2(cover * aspect * (1f - reveal) * .5f * push, 0f);
            // 3) cyan floods back over the card
            float f = Mathf.Clamp01((time - HoldEnd) / (FloodEnd - HoldEnd));
            flood.enabled = time >= HoldEnd && time < FloodEnd + .02f;
            var fc = flood.color; fc.a = f; flood.color = fc;
            // 4) 2026-11-08 (转场参考视频 frames 128-148): the cyan panel slides off to the right, uncovering the page from the left
            bool wiping = time >= FloodEnd;
            for (int i = 0; i < Slices; i++)
            {
                Image bar = slices[i].GetComponent<Image>();
                bar.enabled = wiping && i == 0;
            }
            if (wiping)
            {
                RectTransform panel = slices[0];
                panel.anchorMin = Vector2.zero; panel.anchorMax = Vector2.one; panel.pivot = new Vector2(.5f, .5f);
                panel.localScale = Vector3.one;
                float k = Mathf.Clamp01((time - FloodEnd) / (SliceEnd - FloodEnd));
                float e = k * k * (3f - 2f * k);
                panel.offsetMin = new Vector2(w * e, 0f);
                panel.offsetMax = new Vector2(w * e, 0f);
            }
        }

        static float Ease(float t) => 1f - Mathf.Pow(1f - t, 3f);

        void Finish()
        {
            if (!opened) OpenPage();
            Playing = false;
            if (this != null) Destroy(gameObject);
        }

        void OnDestroy() => Playing = false;

        static void Center(RectTransform r) => r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f);
        static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
    }
}
