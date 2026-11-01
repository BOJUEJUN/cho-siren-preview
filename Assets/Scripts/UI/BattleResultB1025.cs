using System;
using System.Collections.Generic;
using System.Linq;
using ChoSiren.Panels;
using ChoSiren.Systems.Tactics;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.UI
{
    /// <summary>
    /// Battle result, option B2 "暗夜撕纸" (少爷 picked it 2026-10-25 from the option page, after living with B):
    /// an ink ground with white halftone, a big torn sheet of brand-teal paper behind the MVP, a stamped VICTORY with
    /// the stage on a paper tag (never green on green), the whole team as P5 menu bars with the MVP's bar in green
    /// sticking out, and the loot on paper stubs. A defeat uses the same composition in lilac. Pure layout plus its
    /// own entrance; the panel keeps the 继续 button and the recovery actions on top of it.
    /// </summary>
    public sealed class BattleResultB1025 : MonoBehaviour
    {
        public struct Loot { public string ItemId; public int Amount; }

        /// <summary>The grid holds up to two rows of four.</summary>
        public const int MaxLoot = 8;

        static readonly Color32 Paper = new Color32(246, 240, 248, 255), Lilac = new Color32(176, 160, 214, 255);
        static Material silhouette;

        sealed class Move
        {
            public RectTransform Rect;
            public Vector2 Home, From;
            public float Start, Length, FromScale = 1f;
            public CanvasGroup Group;
            public string Sound;
            public bool Sounded;
        }

        readonly List<Move> moves = new List<Move>();
        /// <summary>Where the panel puts 继续 (top-left x/y, size), measured from the card's top-left.</summary>
        public Rect ContinueSlot { get; private set; }
        /// <summary>Top of the band the panel's recovery actions use on a defeat.</summary>
        public float RecoveryTop { get; private set; }
        RectTransform slash;
        Action<string> sfx;
        float clock;
        bool running;
        float W, H;

        public static BattleResultB1025 Show(RectTransform card, Font font, BattleSimulator battle, bool victory, int stars,
            IList<Loot> loot, Func<BattleUnit, Sprite> fullArt, Action onGearTapped, Action<string> sound)
        {
            var go = new GameObject("ResultB1025", typeof(RectTransform));
            go.transform.SetParent(card, false);
            var root = (RectTransform)go.transform;
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = root.offsetMax = Vector2.zero;
            root.SetAsFirstSibling();
            var view = go.AddComponent<BattleResultB1025>();
            view.sfx = sound;
            view.Build(root, font, battle, victory, stars, loot ?? new List<Loot>(), fullArt, onGearTapped);
            return view;
        }

        void Build(RectTransform root, Font font, BattleSimulator battle, bool victory, int stars, IList<Loot> loot,
            Func<BattleUnit, Sprite> fullArt, Action onGearTapped)
        {
            W = root.rect.width > 1f ? root.rect.width : 720f;
            H = root.rect.height > 1f ? root.rect.height : 1536f;
            // Bottom-up, so nothing collides on short or tall screens: 继续, then the reward grid (or the recovery
            // actions on a defeat), then the team bars and the MVP sheet. 2026-11-01 reward-ui (用户: 奖励条"很紧凑，
            // 而且裁剪了排版不对"): the loot is the shared RewardCard1101 grid — up to four in a row, five to eight in two
            // balanced rows — inside a fixed side margin, so nothing can run off either edge.
            float side = W * .06f;
            float contH = Mathf.Max(64f, H * .058f), contTop = H - Mathf.Max(40f, H * .03f) - contH;
            ContinueSlot = new Rect(W - side - W * .4f, contTop, W * .4f, contH);
            RecoveryTop = contTop - W * .03f - 140f;
            int lootCount = victory ? Mathf.Min(MaxLoot, loot.Count) : 0;
            float cardGap = W * .025f, cardRowGap = W * .022f, headerH = W * .062f;
            RewardCard1101.GridLayout(Mathf.Max(1, lootCount), W - side * 2f, cardGap, lootCount <= 2 ? W * .21f : W * .205f,
                out int lootRows, out int lootPerRow, out Vector2 cardSize);
            float gridH = lootRows * cardSize.y + (lootRows - 1) * cardRowGap;
            float gridTop = contTop - W * .045f - gridH;
            float headerTop = gridTop - W * .022f - headerH;
            float rowsBottom = !victory ? RecoveryTop - W * .04f : lootCount > 0 ? headerTop - W * .04f : contTop - W * .06f;
            Color paperColor = victory ? (Color)Brand1025.Teal : new Color32(126, 108, 170, 255);
            Color accent = victory ? (Color)Brand1025.Teal : Lilac;

            var bg = Img("Ground", root, null, new Color32(11, 7, 20, 255)); Stretch(bg.rectTransform);
            var dots = Img("Halftone", root, CelShapes.Halftone, new Color(1f, 1f, 1f, .07f));
            dots.type = Image.Type.Tiled; dots.pixelsPerUnitMultiplier = 2.2f; Stretch(dots.rectTransform);

            var contributions = BattleStickerResultView.ReadContributions(battle);
            var ranked = contributions.OrderByDescending(c => c.Damage).Take(4).ToList();
            var best = ranked.FirstOrDefault();
            BattleUnit mvp = best != null && best.Damage > 0 ? best.Unit : null;

            // ---- the team as P5 menu bars, bottom-up above the loot (a little tighter when the loot takes two rows)
            float rowH = W * (lootRows > 1 ? .088f : .1f), rowGap = W * (lootRows > 1 ? .016f : .02f);
            float rowsTop = rowsBottom - ranked.Count * rowH - Mathf.Max(0, ranked.Count - 1) * rowGap;

            // ---- the torn green sheet behind the MVP (ink halftone clipped to it)
            var paper = New<TornPaper1025>("TornPaper", root);
            paper.color = paperColor;
            float paperTop = H * .085f, paperBottom = rowsTop + H * .025f;
            Place(paper.rectTransform, W * .37f, (paperTop + paperBottom) * .5f, W * .92f, paperBottom - paperTop);
            paper.rectTransform.localEulerAngles = new Vector3(0, 0, -7f);
            paper.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var paperDots = Img("InkDots", paper.rectTransform, CelShapes.Halftone, new Color(0.055f, .03f, .08f, .3f));
            paperDots.type = Image.Type.Tiled; paperDots.pixelsPerUnitMultiplier = 2f; Stretch(paperDots.rectTransform);
            slash = paper.rectTransform; slash.pivot = new Vector2(0f, .5f);
            slash.anchoredPosition += new Vector2(-slash.sizeDelta.x * .5f, 0f);

            // ---- MVP cut out with an ink drop, standing on the sheet; her feet tuck behind the bars
            // 2026-11-02 visual-battle: a defeat with no damage dealt still shows the team's lead (greyed), not a blank sheet.
            BattleUnit figureUnit = mvp ?? (!victory ? ranked.FirstOrDefault()?.Unit : null);
            Sprite art = figureUnit != null ? fullArt?.Invoke(figureUnit) : null;
            if (art != null)
            {
                float top = H * .12f, h = Mathf.Clamp(rowsTop + H * .05f - top, H * .3f, H * .58f);
                float w = h * art.rect.width / art.rect.height;
                if (w > W * .66f) { w = W * .66f; h = w * art.rect.height / art.rect.width; }
                var holder = Rect("Mvp", root);
                Place(holder, W * .04f + w * .5f, top + h * .5f, w, h);
                var ink = Img("InkDrop", holder, art, Brand1025.Ink); Stretch(ink.rectTransform);
                ink.rectTransform.anchoredPosition = new Vector2(W * .02f, -W * .014f);
                var figure = Img("Figure", holder, art, victory ? Color.white : new Color(.72f, .7f, .8f, 1f)); Stretch(figure.rectTransform);
                ink.preserveAspect = figure.preserveAspect = true;
                Add(holder, new Vector2(-W * .6f, 0f), .1f, .3f);
            }

            // ---- stamped title (white on green slab shadow) and the stage on a paper tag
            var title = Label("Title", root, font, victory ? "VICTORY" : "DEFEAT", Mathf.RoundToInt(W * .128f), Paper);
            title.fontStyle = FontStyle.BoldAndItalic; title.alignment = TextAnchor.MiddleRight;
            var slab = title.gameObject.AddComponent<Shadow>();
            slab.effectColor = accent; slab.effectDistance = new Vector2(W * .014f, -W * .014f);
            title.gameObject.AddComponent<Outline>().effectColor = Brand1025.Ink;
            // right edge kept inside the screen even with the tilt and the slab shadow
            Place(title.rectTransform, W * .46f, W * .1f, W * .84f, W * .17f);
            title.rectTransform.localEulerAngles = new Vector3(0, 0, -7f);
            Add(title.rectTransform, Vector2.zero, .25f, .14f, 1.8f, "stamp");
            string stage = battle?.Stage != null ? battle.Stage.Name : string.Empty;
            var sub = Plate("Stage", root, (victory ? "胜利 · " : "失败 · ") + stage, font, Mathf.RoundToInt(W * .042f), Paper, Brand1025.Ink, -7f);
            Place(sub, W * .93f - sub.sizeDelta.x * .5f, W * .25f, sub.sizeDelta.x, sub.sizeDelta.y); // clear of the tilted title (it read "DEFFAT", 2026-10-28)
            Add(sub, new Vector2(W * .4f, 0f), .45f, .2f);

            // ---- stars on the dark side, right of the sheet
            var starText = Label("Stars", root, font, StarText(stars), Mathf.RoundToInt(W * .085f), accent);
            starText.gameObject.AddComponent<Shadow>().effectColor = Color.black;
            starText.GetComponent<Shadow>().effectDistance = new Vector2(W * .008f, -W * .008f);
            starText.alignment = TextAnchor.MiddleRight;
            Place(starText.rectTransform, W * .74f, Mathf.Lerp(H * .12f, rowsTop, .45f), W * .4f, W * .12f);
            starText.rectTransform.localEulerAngles = new Vector3(0, 0, -8f);
            Add(starText.rectTransform, Vector2.zero, .6f, .16f, 1.8f, "stamp");

            for (int i = 0; i < ranked.Count; i++)
            {
                bool isMvp = ranked[i].Unit == mvp;
                var bar = MenuBar(root, font, ranked[i], isMvp, rowH, victory, accent, fullArt);
                float left = isMvp ? W * .12f : W * .2f, right = W * .94f;   // rim + skew stay inside the screen
                Place(bar, (left + right) * .5f, rowsTop + rowH * .5f + i * (rowH + rowGap), right - left, rowH);
                bar.localEulerAngles = new Vector3(0, 0, -3f);
                Add(bar, new Vector2(W * .8f, 0f), .75f + i * .08f, .22f);
            }

            // ---- loot: a 「获得奖励」 tag and the shared reward cards (victory only; a defeat shows the recovery actions)
            if (lootCount > 0)
            {
                var header = Plate("LootHeader", root, "获得奖励", font, Mathf.RoundToInt(headerH * .56f), Paper, Brand1025.Ink, -3f);
                Place(header, side + header.sizeDelta.x * .5f, headerTop + headerH * .5f, header.sizeDelta.x, header.sizeDelta.y);
                Add(header, new Vector2(-W * .4f, 0f), .98f, .18f);
                var rule = New<SkewedPlate1021>("LootRule", root); rule.color = accent; rule.SkewDegrees = -20f; rule.raycastTarget = false;
                float ruleLeft = side + header.sizeDelta.x + W * .03f, ruleRight = W - side;
                Place(rule.rectTransform, (ruleLeft + ruleRight) * .5f, headerTop + headerH * .5f + 2f, ruleRight - ruleLeft, Mathf.Max(3f, W * .006f));
                rule.rectTransform.localEulerAngles = new Vector3(0, 0, -3f);
                Add(rule.rectTransform, new Vector2(W * .6f, 0f), 1.0f, .2f);

                List<Vector2> centres = RewardCard1101.GridPositions(lootCount, lootRows, lootPerRow, cardSize, cardGap, cardRowGap);
                float gridCentre = gridTop + gridH * .5f;
                for (int i = 0; i < lootCount; i++)
                {
                    Loot item = loot[i];
                    RectTransform card = RewardCard1101.Create(root, font, item.ItemId, item.Amount, cardSize, "Loot-" + item.ItemId);
                    Place(card, W * .5f + centres[i].x, gridCentre - centres[i].y, cardSize.x, cardSize.y);
                    card.localEulerAngles = new Vector3(0, 0, (i % 2 == 0 ? -1.5f : 1.5f));
                    bool gear = GameModel.AccessoryIndexForItem(item.ItemId) >= 0;
                    if (gear && onGearTapped != null)
                    {
                        var hitArea = card.gameObject.AddComponent<Image>();
                        hitArea.color = Color.clear; hitArea.raycastTarget = true;
                        var hit = card.gameObject.AddComponent<Button>();
                        hit.targetGraphic = hitArea; hit.transition = Selectable.Transition.None;
                        hit.onClick.AddListener(() => onGearTapped());
                    }
                    Add(card, Vector2.zero, 1.05f + i * .07f, .2f, .35f);
                }
            }

            running = !BattlePresentationOptions.ReduceMotion;
            clock = 0f;
            Pose(running ? 0f : 99f);
        }

        /// <summary>One P5 menu bar: a skewed ink plate with a paper rim (green for the MVP), face, name and output.</summary>
        RectTransform MenuBar(RectTransform root, Font font, BattleStickerResultView.Contribution item, bool isMvp, float h, bool victory,
            Color accent, Func<BattleUnit, Sprite> fullArt)
        {
            var bar = Rect("Bar-" + item.Unit.Definition.Id, root);
            var rim = New<SkewedPlate1021>("Rim", bar); rim.SkewDegrees = -14f; rim.raycastTarget = false;
            rim.color = isMvp ? (Color)Brand1025.Ink : Paper; Stretch(rim.rectTransform);
            rim.rectTransform.offsetMin = new Vector2(-4f, -4f); rim.rectTransform.offsetMax = new Vector2(4f, 4f);
            var plate = New<SkewedPlate1021>("Plate", bar); plate.SkewDegrees = -14f; plate.raycastTarget = false;
            plate.color = isMvp ? accent : (Color)Brand1025.Ink; Stretch(plate.rectTransform);
            Color ink = isMvp ? (Color)Brand1025.Ink : Paper;

            float face = h * .82f;
            var window = Rect("Face", bar);
            window.anchorMin = window.anchorMax = new Vector2(0f, .5f); window.pivot = new Vector2(0f, .5f);
            window.sizeDelta = new Vector2(face, face); window.anchoredPosition = new Vector2(h * .3f, 0f);
            window.gameObject.AddComponent<RectMask2D>();
            string id = item.Unit.Definition.Id;
            Sprite head = Resources.Load<Sprite>("Art/Members/Avatars/" + PanelKit.HeadAvatarPrefix + id) ?? PanelKit.MemberSpriteOrNull(id, true);
            Sprite art = head ?? fullArt?.Invoke(item.Unit);
            if (art != null)
            {
                var img = Img("Art", window, art, item.Unit.Alive || victory ? Color.white : new Color(.7f, .68f, .78f, 1f));
                // 2026-11-01 avatar-face: 按每张立绘的人脸锚点裁头像。
                PortraitFace1101.Apply(img, window, id, PortraitFace1101.Framing.Head);
            }
            var name = Label("Name", bar, font, item.Unit.Definition.Name, Mathf.RoundToInt(h * .4f), ink);
            name.alignment = TextAnchor.MiddleLeft;
            name.rectTransform.anchorMin = name.rectTransform.anchorMax = new Vector2(0f, .5f); name.rectTransform.pivot = new Vector2(0f, .5f);
            name.rectTransform.sizeDelta = new Vector2(h * 2.6f, h); name.rectTransform.anchoredPosition = new Vector2(h * .3f + face + h * .2f, 0f);
            var value = Label("Output", bar, font, (isMvp ? "MVP  " : string.Empty) + item.Damage.ToString("N0"), Mathf.RoundToInt(h * .44f), ink);
            value.alignment = TextAnchor.MiddleRight; value.fontStyle = FontStyle.BoldAndItalic;
            value.rectTransform.anchorMin = value.rectTransform.anchorMax = new Vector2(1f, .5f); value.rectTransform.pivot = new Vector2(1f, .5f);
            value.rectTransform.sizeDelta = new Vector2(h * 3f, h); value.rectTransform.anchoredPosition = new Vector2(-h * .35f, 0f);
            return bar;
        }

        // ------------------------------------------------------------------ entrance

        void Add(RectTransform rect, Vector2 from, float start, float length, float fromScale = 1f, string sound = null, bool fade = false)
        {
            if (rect == null) return;
            var move = new Move { Rect = rect, Home = rect.anchoredPosition, From = from, Start = start, Length = length, FromScale = fromScale, Sound = sound };
            if (fade) move.Group = UnityNull.Real(rect.gameObject.GetComponent<CanvasGroup>()) ?? rect.gameObject.AddComponent<CanvasGroup>();
            if (move.Group != null) { move.Group.blocksRaycasts = true; move.Group.interactable = true; }
            moves.Add(move);
        }

        void Update()
        {
            if (!running) return;
            clock += Mathf.Min(Time.unscaledDeltaTime, .05f);
            Pose(clock);
            if (clock > 2f) running = false;
        }

        void Pose(float t)
        {
            if (slash != null) slash.localScale = new Vector3(OutCubic(t / .2f), 1f, 1f);
            foreach (Move m in moves)
            {
                if (m.Rect == null) continue;
                float k = Mathf.Clamp01((t - m.Start) / Mathf.Max(.01f, m.Length));
                float e = OutCubic(k);
                m.Rect.anchoredPosition = m.Home + m.From * (1f - e);
                bool hidden = t < m.Start;
                m.Rect.localScale = Vector3.one * (hidden ? 0f : Mathf.Lerp(m.FromScale, 1f, e));
                if (m.Group != null) m.Group.alpha = e;
                if (!m.Sounded && !hidden && m.Sound != null) { m.Sounded = true; sfx?.Invoke(m.Sound); }
            }
        }

        static float OutCubic(float k) { k = Mathf.Clamp01(k); return 1f - (1f - k) * (1f - k) * (1f - k); }

        // ------------------------------------------------------------------ helpers

        static string StarText(int stars)
        {
            stars = Mathf.Clamp(stars, 0, 3);
            return new string('★', stars) + new string('☆', 3 - stars);
        }

        static Material Silhouette()
        {
            if (silhouette != null) return silhouette;
            Shader shader = Resources.Load<Shader>("Shaders/UISilhouette");
            return silhouette = shader != null ? new Material(shader) { name = "ResultSilhouette" } : null;
        }

        RectTransform Plate(string name, Transform parent, string text, Font font, int fontSize, Color fill, Color ink, float tiltDegrees)
        {
            var holder = Rect(name, parent);
            var label = Label("Text", holder, font, text, fontSize, ink);
            float w = label.preferredWidth + fontSize * 1.1f, h = fontSize * 1.55f;
            holder.sizeDelta = new Vector2(w, h);
            var shadow = Img("Shadow", holder, null, Brand1025.Ink); Stretch(shadow.rectTransform);
            shadow.rectTransform.anchoredPosition = new Vector2(fontSize * .16f, -fontSize * .16f);
            var plate = Img("Plate", holder, null, fill); Stretch(plate.rectTransform);
            label.transform.SetAsLastSibling(); Stretch(label.rectTransform);
            holder.localEulerAngles = new Vector3(0, 0, tiltDegrees);
            return holder;
        }

        Text Label(string name, Transform parent, Font font, string text, int size, Color color)
        {
            var t = New<Text>(name, parent);
            t.font = font; t.text = text; t.fontSize = Mathf.Max(8, size); t.color = color; t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        static Image Img(string name, Transform parent, Sprite sprite, Color color)
        {
            var image = New<Image>(name, parent);
            image.sprite = sprite; image.color = color; image.raycastTarget = false;
            return image;
        }

        static T New<T>(string name, Transform parent) where T : Component
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(T));
            go.transform.SetParent(parent, false);
            return go.GetComponent<T>();
        }

        static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        /// <summary>Centre at (x, y) measured from the top-left of the result card.</summary>
        static void Place(RectTransform rect, float x, float y, float w, float h)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f); rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(w, h); rect.anchoredPosition = new Vector2(x, -y);
        }

        /// <summary>Centre at an offset from the parent's centre.</summary>
        static void Anchor(RectTransform rect, Vector2 offset)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = offset;
        }
    }

    /// <summary>The torn sheet behind the MVP: a ragged star-shaped polygon drawn as a fan from its centre.</summary>
    public sealed class TornPaper1025 : MaskableGraphic
    {
        // fractions of the rect, y measured from the top
        static readonly Vector2[] Edge =
        {
            new Vector2(.06f, .04f), new Vector2(.18f, 0f), new Vector2(.26f, .06f), new Vector2(.40f, .01f), new Vector2(.52f, .07f),
            new Vector2(.66f, 0f), new Vector2(.80f, .05f), new Vector2(.94f, .02f), new Vector2(1f, .18f), new Vector2(.96f, .34f),
            new Vector2(1f, .50f), new Vector2(.95f, .66f), new Vector2(.99f, .84f), new Vector2(.88f, 1f), new Vector2(.72f, .95f),
            new Vector2(.58f, 1f), new Vector2(.44f, .94f), new Vector2(.30f, 1f), new Vector2(.16f, .95f), new Vector2(.02f, 1f),
            new Vector2(.04f, .82f), new Vector2(0f, .64f), new Vector2(.05f, .46f), new Vector2(0f, .28f),
        };

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            Color32 c = color;
            vh.AddVert(r.center, c, Vector2.zero);
            foreach (Vector2 e in Edge) vh.AddVert(new Vector2(r.xMin + e.x * r.width, r.yMax - e.y * r.height), c, Vector2.zero);
            for (int i = 0; i < Edge.Length; i++) vh.AddTriangle(0, 1 + i, 1 + (i + 1) % Edge.Length);
        }
    }
}
