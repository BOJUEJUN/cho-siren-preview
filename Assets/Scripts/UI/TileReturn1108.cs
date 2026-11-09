using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.UI
{
    /// <summary>
    /// 2026-11-08 (少爷: "详细角色信息返回的转场动画…要完美复刻"): the 转场参考视频 return from the member detail page.
    /// A diamond flashes on the 返回 button while the page dims; dark tilted tiles cascade in a diagonal staircase from
    /// the bottom-right corner to the top-left until the screen is black (the previous page is opened under full cover);
    /// after a short hold the tiles leave in the same direction over a white wash that fades into the page.
    /// Reduce Motion goes straight back.
    /// </summary>
    public sealed class TileReturn1108 : MonoBehaviour
    {
        const float FlashEnd = .24f, CoverStart = .16f, CoverEnd = .50f, HoldEnd = .80f, RevealEnd = 1.12f, WashEnd = 1.38f;
        const float TileSize = 118f, TileAngle = -28f, Wave = .12f;
        static readonly Color TileColor = new Color32(16, 15, 19, 255);

        sealed class Tile { public RectTransform Rect; public Image Face, Edge; public float Order, RevealOrder, Shade, Glint; public Vector2 Home; }

        readonly List<Tile> tiles = new List<Tile>();
        RectTransform root;
        Image dim, wash, diamond, diamondCore;
        Text logo;
        Action openPage;
        bool opened;
        float time;
        Vector2 tap;

        public static bool Playing { get; private set; }
        /// <summary>Batch-mode test runs skip page transitions (so navigation tests stay instant) unless a test opts in.</summary>
        public static bool AllowInBatch;
        public float Clock => time;
        public bool PageOpened => opened;

        /// <summary>Plays over <paramref name="host"/>; <paramref name="back"/> runs once the screen is fully covered.</summary>
        public static TileReturn1108 Play(Transform host, Vector2 tapLocal, Action back)
        {
            if (host == null || Playing || BattlePresentationOptions.ReduceMotion || !Application.isPlaying
                || Application.isBatchMode && !AllowInBatch)
            {
                back?.Invoke();
                return null;
            }
            var go = new GameObject("TileReturn1108", typeof(RectTransform));
            go.transform.SetParent(host, false);
            go.transform.SetAsLastSibling();
            var fx = go.AddComponent<TileReturn1108>();
            fx.Build(tapLocal, back);
            return fx;
        }

        void Build(Vector2 tapLocal, Action back)
        {
            Playing = true;
            openPage = back; tap = tapLocal;
            root = (RectTransform)transform;
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = root.offsetMax = Vector2.zero;
            var blocker = gameObject.AddComponent<Image>(); blocker.color = new Color(0, 0, 0, .004f);
            dim = New<Image>("Dim", transform); Stretch(dim.rectTransform); dim.color = new Color(0, 0, 0, 0);
            wash = New<Image>("Wash", transform); Stretch(wash.rectTransform); wash.color = new Color(1, 1, 1, 0);

            // The tile field: a rotated grid big enough to cover the screen diagonal.
            Canvas.ForceUpdateCanvases();
            float w = Mathf.Max(720f, root.rect.width), h = Mathf.Max(1290f, root.rect.height);
            var field = new GameObject("Tiles", typeof(RectTransform)).GetComponent<RectTransform>();
            field.SetParent(transform, false);
            field.anchorMin = field.anchorMax = field.pivot = new Vector2(.5f, .5f);
            field.localEulerAngles = new Vector3(0f, 0f, TileAngle);
            float span = Mathf.Sqrt(w * w + h * h) + TileSize * 2f;
            int n = Mathf.CeilToInt(span / TileSize);
            Quaternion turn = Quaternion.Euler(0f, 0f, TileAngle);
            // 2026-11-08 (少爷: "是右边到左边的一个转场"): the black blocks come in from the right edge and sweep left along a
            // steep front (its foot slightly ahead), as in 转场参考视频 frames 230-242; they leave from the bottom-right
            // corner towards the top-left (frames 272-284).
            Vector2 dir = new Vector2(-.954f, .3f).normalized;
            Vector2 revealDir = new Vector2(-w, h).normalized;
            float reach = Mathf.Abs(dir.x) * w * .5f + Mathf.Abs(dir.y) * h * .5f + TileSize;
            float revealReach = Mathf.Abs(Vector2.Dot(new Vector2(w, h) * .5f, revealDir)) + TileSize;
            var rng = new System.Random(1108);
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                Vector2 local = new Vector2((x - (n - 1) * .5f) * TileSize, (y - (n - 1) * .5f) * TileSize);
                Vector2 screen = turn * local;
                if (Mathf.Abs(screen.x) > w * .5f + TileSize || Mathf.Abs(screen.y) > h * .5f + TileSize) continue;
                var tile = new Tile();
                tile.Face = New<Image>("Tile", field);
                tile.Rect = tile.Face.rectTransform;
                tile.Rect.anchorMin = tile.Rect.anchorMax = new Vector2(.5f, .5f);
                tile.Rect.sizeDelta = new Vector2(TileSize + 3f, TileSize + 3f);
                tile.Rect.anchoredPosition = local;
                tile.Home = local;
                tile.Face.color = TileColor;
                // Mostly near-black blocks with a few lighter, glassier ones, like the reference's mosaic front.
                tile.Shade = (float)rng.NextDouble() < .18f ? .18f + (float)rng.NextDouble() * .16f : (float)rng.NextDouble() * .07f;
                tile.Glint = (float)rng.NextDouble() < .35f ? .4f + (float)rng.NextDouble() * .4f : 0f;
                // A thin light edge along the tile's top, like the glinting seams in the reference.
                tile.Edge = New<Image>("Edge", tile.Rect);
                RectTransform edge = tile.Edge.rectTransform;
                edge.anchorMin = new Vector2(0f, 1f); edge.anchorMax = Vector2.one; edge.pivot = new Vector2(.5f, 1f);
                edge.offsetMin = new Vector2(0f, -2.5f); edge.offsetMax = Vector2.zero;
                tile.Edge.color = new Color(1f, 1f, 1f, 0f);
                // 0 = first … 1 = last along each sweep, with a little jitter so the front is ragged.
                float jitter = ((float)rng.NextDouble() - .5f) * .06f;
                tile.Order = Mathf.Clamp01((Vector2.Dot(screen, dir) + reach) / (2f * reach) + jitter);
                tile.RevealOrder = Mathf.Clamp01((Vector2.Dot(screen, revealDir) + revealReach) / (2f * revealReach) + jitter);
                tiles.Add(tile);
            }
            // The reference holds the black screen on a faint studio mark; ours shows the game's name.
            logo = New<Text>("HoldLogo", transform);
            logo.font = Resources.Load<Font>("Fonts/NotoSansSC-Subset");
            logo.text = "CHO-SIREN"; logo.fontSize = 46; logo.fontStyle = FontStyle.Bold; logo.alignment = TextAnchor.MiddleCenter;
            logo.horizontalOverflow = HorizontalWrapMode.Overflow;
            logo.rectTransform.anchorMin = logo.rectTransform.anchorMax = new Vector2(.5f, .5f);
            logo.rectTransform.sizeDelta = new Vector2(600f, 80f);
            logo.color = new Color(1f, 1f, 1f, 0f);
            diamond = New<Image>("TapDiamond", transform);
            diamond.rectTransform.anchorMin = diamond.rectTransform.anchorMax = new Vector2(.5f, .5f);
            diamond.rectTransform.sizeDelta = new Vector2(64f, 64f);
            diamond.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
            diamond.color = new Color(.45f, .85f, 1f, 0f);
            diamond.gameObject.AddComponent<Outline>().effectColor = new Color(1f, 1f, 1f, .9f);
            diamondCore = New<Image>("Core", diamond.rectTransform);
            Stretch(diamondCore.rectTransform, 10f);
            diamondCore.color = new Color(.2f, .7f, 1f, 0f);
            Pose();
        }

        T New<T>(string name, Transform parent) where T : Graphic
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(T));
            go.transform.SetParent(parent, false);
            var g = go.GetComponent<T>(); g.raycastTarget = false; return g;
        }

        void Update() => Advance(Mathf.Min(Time.unscaledDeltaTime, .05f));

        /// <summary>Test hook and per-frame step.</summary>
        public void Advance(float seconds)
        {
            time += Mathf.Max(0f, seconds);
            if (!opened && time >= CoverEnd) OpenPage();
            Pose();
            if (time >= WashEnd) Finish();
        }

        void OpenPage()
        {
            opened = true;
            Action back = openPage; openPage = null;
            try { back?.Invoke(); } catch (Exception e) { Debug.LogException(e); }
            if (this != null) transform.SetAsLastSibling();
        }

        static float Ease(float t) => 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);

        void Pose()
        {
            // 1) diamond flash on the button and the page dimming before the tiles arrive
            float f = Mathf.Clamp01(time / FlashEnd);
            diamond.rectTransform.anchoredPosition = tap;
            diamond.rectTransform.localScale = Vector3.one * Mathf.Lerp(.5f, 1.5f, Ease(f));
            float flashAlpha = time < FlashEnd ? Mathf.Sin(f * Mathf.PI) : 0f;
            diamond.color = new Color(.45f, .85f, 1f, .35f * flashAlpha);
            diamondCore.color = new Color(.25f, .75f, 1f, .8f * flashAlpha);
            diamond.enabled = diamondCore.enabled = time < FlashEnd;
            dim.color = new Color(0f, 0f, 0f, opened ? 0f : .55f * Ease(time / CoverEnd));

            // 2) the tiles: in along the staircase, then out the same way
            float coverSpan = CoverEnd - CoverStart, revealSpan = RevealEnd - HoldEnd;
            Vector2 slide = (Vector2)(Quaternion.Euler(0f, 0f, -TileAngle) * new Vector3(1f, -.3f, 0f)) * 46f; // they arrive from the right
            foreach (Tile tile in tiles)
            {
                float start = CoverStart + tile.Order * (coverSpan - Wave);
                float s = Ease((time - start) / Wave);
                float leave = HoldEnd + tile.RevealOrder * (revealSpan - Wave);
                float r = Mathf.Clamp01((time - leave) / Wave);
                // Once settled every block is solid; the mosaic only shows along the moving front.
                float settled = Mathf.Clamp01((time - start - Wave) / .12f) * (1f - Mathf.Clamp01(r * 3f));
                float shade = Mathf.Lerp(tile.Shade, 0f, settled);
                Color c = Color.Lerp(TileColor, new Color(.75f, .75f, .8f), shade);
                float alpha = Mathf.Clamp01(s * 1.25f) * (1f - r);
                tile.Face.color = new Color(c.r, c.g, c.b, alpha * Mathf.Lerp(shade > .1f ? .8f : 1f, 1f, settled));
                tile.Rect.anchoredPosition = tile.Home + slide * (1f - s) - slide * (r * r) * .6f;
                tile.Rect.localScale = Vector3.one * Mathf.Lerp(1.08f, 1f, s);
                float glint = tile.Glint * (Mathf.Sin(Mathf.Clamp01(s) * Mathf.PI) + Mathf.Sin(r * Mathf.PI));
                tile.Edge.color = new Color(1f, 1f, 1f, Mathf.Clamp01(glint) * alpha);
            }

            float hold = Mathf.Clamp01((time - CoverEnd) / .1f) * (1f - Mathf.Clamp01((time - HoldEnd + .06f) / .12f));
            logo.color = new Color(.62f, .6f, .66f, .55f * hold);

            // 3) a white wash under the leaving tiles that fades into the page
            float washIn = Mathf.Clamp01((time - HoldEnd) / .08f);
            float washOut = Mathf.Clamp01((time - (RevealEnd - .1f)) / (WashEnd - RevealEnd + .1f));
            wash.color = new Color(1f, 1f, 1f, opened ? .85f * washIn * (1f - washOut) : 0f);
        }

        void Finish()
        {
            if (!opened) OpenPage();
            Playing = false;
            if (this != null) Destroy(gameObject);
        }

        void OnDestroy() => Playing = false;

        static void Stretch(RectTransform r, float inset = 0f)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = new Vector2(inset, inset); r.offsetMax = new Vector2(-inset, -inset);
        }
    }
}
