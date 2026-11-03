using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using ChoSiren.Systems.Economy;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ChoSiren.UI
{
    /// <summary>
    /// The one reward card (2026-11-01 reward-ui): a torn-paper card in the item's quality colour, the ink face with a
    /// halftone corner, the item's own icon (currencies = the top-HUD badges, accessories = their collection art), the
    /// amount as a big sticker number and the name on an ink band. Used by the 「获得奖励」 popup and the victory page,
    /// so a coin or an earring looks the same wherever it is received.
    /// </summary>
    public static class RewardCard1101
    {
        /// <summary>Height / width of the full card (the compact grid variant uses <see cref="CompactAspect"/>).</summary>
        public const float Aspect = 1.22f, CompactAspect = 1.12f;

        static Sprite rim, face;

        public static Sprite RimSprite => rim != null ? rim : rim = Resources.Load<Sprite>("Art/Reward1101/reward-card-rim-1101");
        public static Sprite FaceSprite => face != null ? face : face = Resources.Load<Sprite>("Art/Reward1101/reward-card-face-1101");

        public static bool IsCurrency(string itemId) =>
            itemId == CurrencyIds.Gold || itemId == CurrencyIds.Diamond || itemId == CurrencyIds.Stamina;

        /// <summary>Accessories: their rarity colour. Currencies: their HUD colour. Anything else: brand teal.</summary>
        public static Color QualityColor(string itemId)
        {
            int accessory = GameModel.AccessoryIndexForItem(itemId);
            if (accessory >= 0) return RewardItemVisuals.RarityColor(GameModel.AccessoryRarityOf(accessory));
            switch (itemId)
            {
                case CurrencyIds.Gold: return new Color32(255, 196, 64, 255);
                case CurrencyIds.Diamond: return new Color32(72, 192, 255, 255);
                case CurrencyIds.Stamina: return new Color32(255, 92, 190, 255);
                case CurrencyIds.RecruitTicket: return new Color32(255, 150, 120, 255);
                case CurrencyIds.CostumeTicket: return new Color32(110, 214, 255, 255);
                default: return Brand1025.Teal;
            }
        }

        public static string DisplayName(string itemId)
        {
            int accessory = GameModel.AccessoryIndexForItem(itemId);
            if (accessory >= 0 || IsCurrency(itemId) || CurrencyIds.IsKnown(itemId)) return GameModel.RewardItemName(itemId);
            if (!string.IsNullOrEmpty(itemId) && itemId.StartsWith("costume-", StringComparison.Ordinal)) return "服装";
            return GameModel.RewardItemName(itemId);
        }

        public static RectTransform Create(Transform parent, Font font, string itemId, int amount, Vector2 size, string name = null)
        {
            var root = NewRect(name ?? "RewardCard-" + itemId, parent);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(.5f, .5f);
            root.sizeDelta = size;
            float w = size.x, h = size.y;
            Color q = QualityColor(itemId);
            int accessory = GameModel.AccessoryIndexForItem(itemId);

            var shadow = Img("Shadow", root, RimSprite, new Color(.03f, .01f, .06f, .92f));
            Stretch(shadow.rectTransform); shadow.rectTransform.anchoredPosition = new Vector2(w * .04f, -w * .04f);
            var rimImage = Img("Rim", root, RimSprite, q); Stretch(rimImage.rectTransform);
            var faceImage = Img("Face", root, FaceSprite, FaceSprite != null ? Color.white : (Color)new Color32(24, 14, 40, 255));
            Stretch(faceImage.rectTransform);
            if (FaceSprite == null) { faceImage.rectTransform.offsetMin = Vector2.one * w * .05f; faceImage.rectTransform.offsetMax = -Vector2.one * w * .05f; }

            float iconY = h * .5f - h * .40f;   // icon centre, as an offset from the card centre
            var glow = Img("Glow", root, BattleFxArt1021.Dot, new Color(q.r, q.g, q.b, .55f));
            if (BattleFxArt1021.Additive != null) glow.material = BattleFxArt1021.Additive;
            Centre(glow.rectTransform, new Vector2(0f, iconY), Vector2.one * w * .98f);

            Sprite sprite = RewardItemVisuals.SpriteFor(itemId);
            float iconSize = w * (IsCurrency(itemId) ? .66f : .6f);
            var icon = Img("Icon", root, sprite != null ? sprite : CelShapes.Star4, sprite != null ? Color.white : q);
            icon.preserveAspect = true;
            if (RewardItemVisuals.IsCodeSymbol(sprite)) icon.color = RewardItemVisuals.AccentFor(itemId);
            Centre(icon.rectTransform, new Vector2(0f, iconY), Vector2.one * iconSize);

            var spark = Img("Spark", root, CelShapes.Star4, new Color(1f, 1f, 1f, .92f));
            if (BattleFxArt1021.Additive != null) spark.material = BattleFxArt1021.Additive;
            Centre(spark.rectTransform, new Vector2(w * .28f, iconY + h * .2f), Vector2.one * w * .2f);

            if (accessory >= 0)
            {
                var tag = NewRect("Quality", root);
                Centre(tag, new Vector2(-w * .2f, h * .5f - h * .1f), new Vector2(w * .5f, h * .12f));
                tag.localEulerAngles = new Vector3(0f, 0f, 4f);
                var tagShadow = New<SkewedPlate1021>("Shadow", tag); tagShadow.color = Brand1025.Ink; tagShadow.SkewDegrees = -12f; tagShadow.raycastTarget = false;
                Stretch(tagShadow.rectTransform); tagShadow.rectTransform.anchoredPosition = new Vector2(2.5f, -2.5f);
                var tagPlate = New<SkewedPlate1021>("Plate", tag); tagPlate.color = q; tagPlate.SkewDegrees = -12f; tagPlate.raycastTarget = false;
                Stretch(tagPlate.rectTransform);
                var tagText = Label("Text", tag, font, GameModel.AccessoryRarityNameOf(accessory), Mathf.RoundToInt(h * .075f), Brand1025.Ink);
                Stretch(tagText.rectTransform);
            }

            if (accessory < 0 || amount > 1)
            {
                var amountText = Label("Amount", root, font, "×" + amount.ToString("N0", CultureInfo.InvariantCulture),
                    Mathf.RoundToInt(Mathf.Clamp(w * .21f, 18f, 46f)), Color.white);
                amountText.fontStyle = FontStyle.BoldAndItalic;
                amountText.alignment = TextAnchor.MiddleRight;
                Centre(amountText.rectTransform, new Vector2(w * .02f, iconY - h * .25f), new Vector2(w * .84f, h * .2f));
                amountText.resizeTextForBestFit = true;
                amountText.resizeTextMinSize = Mathf.Max(10, amountText.fontSize / 2);
                amountText.resizeTextMaxSize = amountText.fontSize;
                amountText.horizontalOverflow = HorizontalWrapMode.Wrap;
                var stroke = amountText.gameObject.AddComponent<Outline>();
                stroke.effectColor = Brand1025.Ink; stroke.effectDistance = new Vector2(2.4f, -2.4f);
                var drop = amountText.gameObject.AddComponent<Shadow>();
                drop.effectColor = new Color(q.r * .55f, q.g * .55f, q.b * .55f, 1f); drop.effectDistance = new Vector2(3.5f, -3.5f);
            }

            var band = NewRect("NameBand", root);
            Centre(band, new Vector2(0f, -h * .5f + h * .145f), new Vector2(w * .86f, h * .17f));
            var bandPlate = New<SkewedPlate1021>("Plate", band); bandPlate.color = new Color(.04f, .02f, .07f, .94f);
            bandPlate.SkewDegrees = -8f; bandPlate.raycastTarget = false; Stretch(bandPlate.rectTransform);
            var bandLine = New<SkewedPlate1021>("Accent", band); bandLine.color = q; bandLine.SkewDegrees = -8f; bandLine.raycastTarget = false;
            bandLine.rectTransform.anchorMin = new Vector2(0f, 1f); bandLine.rectTransform.anchorMax = Vector2.one;
            bandLine.rectTransform.pivot = new Vector2(.5f, 1f); bandLine.rectTransform.sizeDelta = new Vector2(0f, Mathf.Max(2f, h * .014f));
            bandLine.rectTransform.anchoredPosition = Vector2.zero;
            var nameText = Label("Name", band, font, DisplayName(itemId), Mathf.RoundToInt(Mathf.Clamp(h * .092f, 13f, 26f)),
                accessory >= 0 ? q : Color.white);
            Stretch(nameText.rectTransform);
            nameText.rectTransform.offsetMin = new Vector2(w * .05f, 0f); nameText.rectTransform.offsetMax = new Vector2(-w * .05f, 0f);
            nameText.resizeTextForBestFit = true; nameText.resizeTextMinSize = 10; nameText.resizeTextMaxSize = nameText.fontSize;
            nameText.horizontalOverflow = HorizontalWrapMode.Wrap;
            return root;
        }

        /// <summary>
        /// Card size for <paramref name="count"/> rewards in a box <paramref name="availableWidth"/> wide: up to four in one
        /// row, five to eight in two balanced rows (5 = 3+2, 6 = 3+3, 7 = 4+3, 8 = 4+4).
        /// </summary>
        public static void GridLayout(int count, float availableWidth, float gap, float maxWidth, out int rows, out int perRow, out Vector2 card)
        {
            count = Mathf.Max(1, count);
            rows = count <= 4 ? 1 : Mathf.CeilToInt(count / 4f);
            perRow = Mathf.CeilToInt(count / (float)rows);
            int columnsForWidth = Mathf.Max(perRow, rows > 1 ? 4 : perRow);
            float w = Mathf.Min(maxWidth, (availableWidth - gap * (columnsForWidth - 1)) / columnsForWidth);
            card = new Vector2(w, w * (rows > 1 ? CompactAspect : Aspect));
        }

        /// <summary>Card centres (offsets from the grid centre) for the layout above.</summary>
        public static List<Vector2> GridPositions(int count, int rows, int perRow, Vector2 card, float gap, float rowGap)
        {
            var list = new List<Vector2>(count);
            float totalH = rows * card.y + (rows - 1) * rowGap;
            int index = 0;
            for (int r = 0; r < rows; r++)
            {
                int inRow = Mathf.Min(perRow, count - index);
                float rowW = inRow * card.x + (inRow - 1) * gap;
                float y = totalH * .5f - card.y * .5f - r * (card.y + rowGap);
                for (int c = 0; c < inRow; c++, index++)
                    list.Add(new Vector2(-rowW * .5f + card.x * .5f + c * (card.x + gap), y));
            }
            return list;
        }

        // ------------------------------------------------------------------ helpers (shared with RewardGain1101)

        internal static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent != null ? parent.gameObject.layer : 5;
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        internal static T New<T>(string name, Transform parent) where T : Component
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(T));
            go.layer = parent != null ? parent.gameObject.layer : 5;
            go.transform.SetParent(parent, false);
            return go.GetComponent<T>();
        }

        internal static Image Img(string name, Transform parent, Sprite sprite, Color color)
        {
            var image = New<Image>(name, parent);
            image.sprite = sprite; image.color = color; image.raycastTarget = false;
            return image;
        }

        internal static Text Label(string name, Transform parent, Font font, string text, int size, Color color)
        {
            var t = New<Text>(name, parent);
            t.font = font != null ? font : DefaultFont; t.text = text; t.fontSize = Mathf.Max(8, size); t.color = color;
            t.fontStyle = FontStyle.Bold; t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        static Font defaultFont;
        internal static Font DefaultFont => defaultFont != null ? defaultFont
            : defaultFont = Resources.Load<Font>("Fonts/NotoSansSC-Subset") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        internal static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        internal static void Centre(RectTransform rect, Vector2 offset, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = offset; rect.sizeDelta = size;
        }
    }

    /// <summary>
    /// 2026-11-01 reward-ui — the single "获得奖励" presentation for every grant (用户: "领取到金币、钻石这些，设计要统一下，
    /// 不要一个地方一个样"). <see cref="Bind"/> listens to <see cref="GameModel.RewardsGranted1101"/>, so 每日签到、任务、
    /// 邮件（含新手赠送）、成就、章节奖励/章节任务、扫荡、商店兑换、舞台收益、演出 all end up here:
    /// · Full — a torn-paper/neon popup: dimmed halftone ground, slowly turning light rays, the teal 「获得奖励」 band,
    ///   the reward cards popping in one by one; tap anywhere to collect, and the currency icons then fly into the HUD.
    /// · Lite — small routine currency income (one task, 舞台收益, 演出, 兑换): no popup, the HUD badges burst from where the
    ///   player tapped and fly into the matching HUD pill, whose number rolls up with a flash.
    /// Stage settlement is skipped (the victory page shows the same cards). The text toast the caller sends for the same
    /// action is absorbed into the popup caption (<see cref="Absorb"/>), so there is never a second, different notice.
    /// </summary>
    public sealed class RewardGain1101 : MonoBehaviour
    {
        public enum Mode { Full, Lite }

        public sealed class Presentation
        {
            public Mode Mode;
            public List<(string ItemId, int Amount)> Items = new List<(string ItemId, int Amount)>();
            public string Source;
            public string Caption;
            public int Batches;
        }

        /// <summary>The latest presentation (tests read it to prove each entry used this component).</summary>
        public static Presentation LastPresentation { get; private set; }
        public static int PresentedCount { get; private set; }

        public const string PopupName = "RewardGainPopup1101", LiteName = "RewardGainLite1101";
        const int SortingOrder = 520;

        static readonly Dictionary<GameModel, RewardGain1101> hosts = new Dictionary<GameModel, RewardGain1101>();
        static RewardGain1101 pendingHost;

        GameModel model;
        Font font;
        Presentation pending;
        int pendingFrame;
        Vector3? pendingOrigin;
        readonly Dictionary<string, string> heldText = new Dictionary<string, string>();
        readonly Queue<Presentation> queued = new Queue<Presentation>();
        RewardPopup1101 openPopup;
        RectTransform liteLayer;
        readonly List<HudSlot> slots = new List<HudSlot>();

        // ------------------------------------------------------------------ binding

        /// <summary>Idempotent: one presenter per model, living on the root canvas of <paramref name="anyChild"/>.</summary>
        public static RewardGain1101 Bind(GameModel model, Transform anyChild, Font font)
        {
            if (model == null || anyChild == null) return null;
            if (hosts.TryGetValue(model, out RewardGain1101 existing) && existing != null)
            {
                if (existing.font == null && font != null) existing.font = font;
                return existing;
            }
            Canvas canvas = anyChild.GetComponentInParent<Canvas>();
            if (canvas == null) return null;   // not on screen (headless fixtures): nothing to present on
            Transform root = canvas.rootCanvas.transform;
            var go = new GameObject("RewardGain1101", typeof(RectTransform));
            go.layer = root.gameObject.layer;
            go.transform.SetParent(root, false);
            RewardCard1101.Stretch((RectTransform)go.transform);
            var host = go.AddComponent<RewardGain1101>();
            host.model = model;
            host.font = font != null ? font : RewardCard1101.DefaultFont;
            model.DiscardPendingRewards1101();
            model.RewardsGranted1101 += host.OnGranted;
            hosts[model] = host;
            return host;
        }

        /// <summary>
        /// 2026-11-03: pages that show their own reward cards (the victory page) still send the currency
        /// icons flying to the header like every other grant, instead of being the one place without it.
        /// </summary>
        public static void FlyFrom(GameModel model, string itemId, Vector3 fromWorld, float delay)
        {
            RewardGain1101 host = HostFor(model);
            if (host == null || !Application.isPlaying || !RewardCard1101.IsCurrency(itemId)) return;
            host.FlyToHud(itemId, fromWorld, 3, delay);
        }

        public static RewardGain1101 HostFor(GameModel model) =>
            model != null && hosts.TryGetValue(model, out RewardGain1101 host) && host != null ? host : null;

        public bool IsPopupOpen => openPopup != null;
        public bool HasPending => pending != null;

        void OnDestroy()
        {
            if (model != null)
            {
                model.RewardsGranted1101 -= OnGranted;
                if (hosts.TryGetValue(model, out RewardGain1101 host) && host == this) hosts.Remove(model);
            }
            if (pendingHost == this) pendingHost = null;
            foreach (HudSlot slot in slots) slot.Restore();
        }

        void OnGranted(RewardGrant1101 grant)
        {
            if (grant == null || grant.Items == null || grant.Items.Count == 0) return;
            if (grant.Source == RewardGrant1101.SourceBattle) return;
            if (pending == null)
            {
                pending = new Presentation { Source = grant.Source };
                pendingFrame = Time.frameCount;
                pendingOrigin = CaptureOrigin();
            }
            else if (pending.Source != grant.Source) pending.Source = RewardGrant1101.SourceGeneral;
            pending.Batches++;
            foreach ((string itemId, int amount) in grant.Items)
            {
                int at = pending.Items.FindIndex(entry => entry.ItemId == itemId);
                if (at >= 0) pending.Items[at] = (itemId, pending.Items[at].Amount + amount);
                else pending.Items.Add((itemId, amount));
                // The HUD still shows the old value (the model publishes before Changed): remember it so the number can
                // roll up when the icons land instead of jumping the moment the claim is tapped.
                if (RewardCard1101.IsCurrency(itemId) && !heldText.ContainsKey(itemId))
                {
                    Text value = FindHudValue(itemId);
                    if (value != null) heldText[itemId] = value.text;
                }
            }
            pendingHost = this;
            if (!Application.isPlaying) Present();
        }

        /// <summary>
        /// The caller's toast for the action that just granted rewards: becomes the popup caption instead of a second
        /// notice. Returns true when absorbed (the caller then skips its own text toast).
        /// </summary>
        public static bool Absorb(string message)
        {
            if (string.IsNullOrEmpty(message)) return false;
            RewardGain1101 host = pendingHost;
            if (host == null || host.pending == null || host.pendingFrame != Time.frameCount) return false;
            string caption = CaptionFrom(message);
            if (!string.IsNullOrEmpty(caption)) host.pending.Caption = caption;
            return true;
        }

        static readonly Regex RewardFragment = new Regex(@"(星钻|星光币|金币|体力|签约券|服装券|碎片|饰品|服装|道具)\s*[+×]\s*[\d,，]+");

        /// <summary>"签到成功：连续第 1 天，星钻 +20" → "签到成功 · 连续第 1 天" (the cards already show the amounts).</summary>
        public static string CaptionFrom(string message)
        {
            if (string.IsNullOrEmpty(message)) return string.Empty;
            string line = message.Split('\n')[0];
            line = RewardFragment.Replace(line, string.Empty);
            line = Regex.Replace(line, @"[（(]\s*[、，,\s]*\s*[）)]", string.Empty);
            line = Regex.Replace(line, @"[、，,]\s*(?=[、，,：:）)]|$)", string.Empty);
            line = Regex.Replace(line, @"([：:])\s*[、，,]+", "$1");
            line = line.Trim().TrimEnd('：', ':', '，', ',', '、', '。', ' ').TrimStart('，', ',', '、', ' ');
            line = Regex.Replace(line, @"\s*[：:]\s*", " · ");
            return line.Trim();
        }

        /// <summary>Lite only for one routine currency income; milestones (签到、邮件、成就、章节…) and items get the popup.</summary>
        public static Mode Classify(IReadOnlyList<(string ItemId, int Amount)> items, string source, int batches)
        {
            bool routine = source == RewardGrant1101.SourceTask || source == RewardGrant1101.SourceIdle ||
                           source == RewardGrant1101.SourcePerform || source == RewardGrant1101.SourceExchange ||
                           source == RewardGrant1101.SourceStory;
            if (!routine || batches > 1 || items == null || items.Count == 0 || items.Count > 2) return Mode.Full;
            foreach (var item in items)
                if (!RewardCard1101.IsCurrency(item.ItemId)) return Mode.Full;
            return Mode.Lite;
        }

        static Vector3? CaptureOrigin()
        {
            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected == null || !selected.activeInHierarchy) return null;
            var rect = selected.transform as RectTransform;
            if (rect == null) return null;
            return rect.TransformPoint(rect.rect.center);
        }

        // ------------------------------------------------------------------ presenting

        void LateUpdate()
        {
            if (pending != null) Present();
            for (int i = slots.Count - 1; i >= 0; i--)
                if (slots[i].Tick()) slots.RemoveAt(i);
            if (liteLayer != null && liteLayer.childCount == 0 && slots.Count == 0)
            {
                Destroy(liteLayer.gameObject);
                liteLayer = null;
            }
        }

        void Present()
        {
            Presentation p = pending;
            Vector3? origin = pendingOrigin;
            pending = null;
            pendingOrigin = null;
            if (pendingHost == this) pendingHost = null;
            p.Mode = Classify(p.Items, p.Source, p.Batches);
            if (p.Mode == Mode.Lite && !AllOnHud(p.Items)) p.Mode = Mode.Full;
            LastPresentation = p;
            PresentedCount++;
            if (!Application.isPlaying) { heldText.Clear(); return; }

            HoldHud(p.Items);
            if (p.Mode == Mode.Lite) PlayLite(p, origin);
            else if (openPopup != null) queued.Enqueue(p);
            else OpenPopup(p);
        }

        void OpenPopup(Presentation p)
        {
            openPopup = RewardPopup1101.Open(this, transform, font, p);
        }

        internal void PopupClosed(RewardPopup1101 popup, List<(string ItemId, Vector3 World)> cardCurrencies)
        {
            if (popup == openPopup) openPopup = null;
            foreach ((string itemId, Vector3 world) in cardCurrencies) FlyToHud(itemId, world, 3, 0f);
            foreach (HudSlot slot in slots) if (!slot.Flying && !slot.Retiring) slot.Land();   // nothing flew there: just settle
            if (queued.Count > 0) OpenPopup(queued.Dequeue());
        }

        bool AllOnHud(List<(string ItemId, int Amount)> items)
        {
            foreach (var item in items)
                if (FindHudPill(item.ItemId) == null) return false;
            return true;
        }

        void HoldHud(List<(string ItemId, int Amount)> items)
        {
            foreach ((string itemId, int amount) in items)
            {
                if (!RewardCard1101.IsCurrency(itemId) || !FindHud(itemId, out RectTransform pill, out Text value)) continue;
                HudSlot slot = slots.Find(s => s.ItemId == itemId && !s.Retiring);
                if (slot == null)
                {
                    slot = new HudSlot(itemId, pill, value, RewardCard1101.QualityColor(itemId), this, IsCovered(pill));
                    slots.Add(slot);
                }
                heldText.TryGetValue(itemId, out string old);
                slot.Hold(old, value.text);
            }
            heldText.Clear();
        }

        /// <summary>
        /// True when a later-drawn, mostly opaque full-page graphic (a task board, a modal dim…) sits over the HUD pill.
        /// The icons then fly into a copy of the pill raised above everything for the moment instead of into a hidden bar.
        /// </summary>
        bool IsCovered(RectTransform pill)
        {
            Canvas canvas = pill.GetComponentInParent<Canvas>();
            if (canvas == null) return false;
            var rootRect = (RectTransform)canvas.rootCanvas.transform;
            float rootArea = WorldArea(rootRect);
            Vector3 centre = pill.TransformPoint(pill.rect.center);
            Transform bar = pill.parent != null ? pill.parent : pill;
            bool after = false;
            foreach (Graphic g in rootRect.GetComponentsInChildren<Graphic>(false))
            {
                if (!after) { after = g.transform.IsChildOf(pill); continue; }
                if (g.transform.IsChildOf(bar) || g.transform.IsChildOf(transform) || !g.enabled) continue;
                if (g.color.a * g.canvasRenderer.GetInheritedAlpha() < .55f) continue;
                RectTransform r = g.rectTransform;
                if (!r.rect.Contains((Vector2)r.InverseTransformPoint(centre))) continue;
                if (WorldArea(r) >= rootArea * .2f) return true;
            }
            return false;
        }

        static float WorldArea(RectTransform rect)
        {
            var c = new Vector3[4];
            rect.GetWorldCorners(c);
            return Vector3.Distance(c[0], c[3]) * Vector3.Distance(c[0], c[1]);
        }

        RectTransform LiteLayer()
        {
            if (liteLayer != null) return liteLayer;
            liteLayer = OverlayCanvas(LiteName, false);
            return liteLayer;
        }

        internal RectTransform OverlayCanvas(string name, bool blocking)
        {
            var rect = RewardCard1101.NewRect(name, transform);
            RewardCard1101.Stretch(rect);
            var canvas = rect.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = SortingOrder + (blocking ? 2 : 1);
            if (blocking) rect.gameObject.AddComponent<GraphicRaycaster>();
            return rect;
        }

        void PlayLite(Presentation p, Vector3? origin)
        {
            RectTransform layer = LiteLayer();
            Vector3 start = origin ?? layer.TransformPoint(layer.rect.center + new Vector2(0f, layer.rect.height * .08f));
            float delay = 0f;
            var chips = new List<(string ItemId, int Amount)>();
            foreach ((string itemId, int amount) in p.Items)
            {
                int count = Mathf.Clamp(2 + Mathf.FloorToInt(Mathf.Log10(Mathf.Max(1, amount))), 3, 6);
                FlyToHud(itemId, start, count, delay);
                delay += .12f;
                chips.Add((itemId, amount));
            }
            LiteChip1101.Spawn(layer, font, start, chips, p.Caption);
        }

        void FlyToHud(string itemId, Vector3 fromWorld, int count, float delay)
        {
            HudSlot slot = slots.Find(s => s.ItemId == itemId && !s.Retiring);
            if (slot == null || slot.Pill == null)
            {
                // 2026-11-03 (用户: 金币领取有些地方没有动画): pages without that HUD pill (关卡地图, 练习室…) still
                // get the same burst, flying to where the pill sits on the lobby header and fading there.
                if (BattlePresentationOptions.ReduceMotion || !RewardCard1101.IsCurrency(itemId)) return;
                RectTransform fallbackLayer = LiteLayer();
                var corners = new Vector3[4];
                fallbackLayer.GetWorldCorners(corners);
                float across = itemId == CurrencyIds.Diamond ? .47f : itemId == CurrencyIds.Gold ? .67f : .87f;
                Vector3 end = Vector3.Lerp(corners[1], corners[2], across) + (corners[0] - corners[1]) * .03f;
                for (int i = 0; i < count; i++)
                    RewardFlyer1101.Spawn(fallbackLayer, itemId, fromWorld, null, delay + i * .055f, i, i == count - 1, end);
                return;
            }
            if (BattlePresentationOptions.ReduceMotion) { slot.Land(); return; }
            RectTransform layer = LiteLayer();
            slot.Flying = true;
            for (int i = 0; i < count; i++)
                RewardFlyer1101.Spawn(layer, itemId, fromWorld, slot, delay + i * .055f, i, i == count - 1);
        }

        // ------------------------------------------------------------------ HUD lookup

        static readonly string[] HudValueNames = { "Diamonds", "Gold", "Stamina" };

        RectTransform FindHudPill(string itemId) => FindHud(itemId, out RectTransform pill, out _) ? pill : null;

        Text FindHudValue(string itemId) => FindHud(itemId, out _, out Text value) ? value : null;

        /// <summary>The visible top-bar pill "Currency-&lt;id&gt;" that carries the live number text.</summary>
        bool FindHud(string itemId, out RectTransform pill, out Text value)
        {
            pill = null; value = null;
            if (!RewardCard1101.IsCurrency(itemId)) return false;
            Transform root = transform.parent;
            if (root == null) return false;
            string wanted = itemId == CurrencyIds.Diamond ? HudValueNames[0] : itemId == CurrencyIds.Gold ? HudValueNames[1] : HudValueNames[2];
            foreach (RectTransform rect in root.GetComponentsInChildren<RectTransform>(false))
            {
                if (rect.name != "Currency-" + itemId || !rect.gameObject.activeInHierarchy || rect.IsChildOf(transform)) continue;
                foreach (Text text in rect.GetComponentsInChildren<Text>(false))
                {
                    if (text.name != wanted) continue;
                    pill = rect; value = text;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// One HUD currency pill: holds its old number until the icons land, then rolls up with a pulse and a flash. When the
        /// real pill is covered by a full page, a copy of it drops in above everything, takes the icons and leaves again.
        /// </summary>
        internal sealed class HudSlot
        {
            public readonly string ItemId;
            public readonly RectTransform Pill;
            readonly Text value;
            readonly Color color;
            readonly RewardGain1101 owner;
            readonly Vector3 baseScale;
            readonly CanvasGroup ghost;
            readonly Vector2 ghostHome;
            readonly float bornAt;
            string from, to, written;
            float rollStart = -1f, pulseStart = -1f, retireAt = -1f;
            bool holding;
            Image flash;
            public bool Flying;
            public bool Retiring => retireAt >= 0f;
            public bool IsGhost => ghost != null;

            public HudSlot(string itemId, RectTransform pill, Text value, Color color, RewardGain1101 owner, bool covered)
            {
                ItemId = itemId; this.color = color; this.owner = owner;
                bornAt = Time.unscaledTime;
                if (covered)
                {
                    RectTransform layer = owner.LiteLayer();
                    GameObject copy = UnityEngine.Object.Instantiate(pill.gameObject, layer, true);
                    copy.name = "HudGhost-" + itemId;
                    foreach (MonoBehaviour behaviour in copy.GetComponentsInChildren<MonoBehaviour>(true))
                        if (!(behaviour is Graphic) && !(behaviour is BaseMeshEffect) && !(behaviour is Mask) && !(behaviour is RectMask2D))
                            UnityEngine.Object.Destroy(behaviour);
                    foreach (Graphic g in copy.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
                    ghost = UnityNull.Real(copy.GetComponent<CanvasGroup>()) ?? copy.AddComponent<CanvasGroup>();
                    ghost.blocksRaycasts = false; ghost.alpha = 0f;
                    Pill = (RectTransform)copy.transform;
                    ghostHome = Pill.anchoredPosition;
                    this.value = null;
                    foreach (Text text in copy.GetComponentsInChildren<Text>(true))
                        if (text.name == value.name) this.value = text;
                }
                else { Pill = pill; this.value = value; }
                baseScale = Pill.localScale;
            }

            public void Hold(string oldText, string finalText)
            {
                to = finalText;
                if (!holding) from = string.IsNullOrEmpty(oldText) ? finalText : oldText;
                holding = true;
                rollStart = -1f;
                Write(from);
            }

            void Write(string text) { if (value != null) { value.text = text; written = text; } }

            /// <summary>The first icon arrived (or nothing flies): pulse, flash and roll the number.</summary>
            public void Land()
            {
                pulseStart = Time.unscaledTime;
                if (rollStart < 0f && holding) rollStart = Time.unscaledTime;
                if (Pill == null) return;
                if (flash == null)
                {
                    Transform icon = Pill.Find("DiamondIcon") ?? Pill.Find("GoldIcon") ?? Pill.Find("StaminaIcon");
                    var layer = owner.LiteLayer();
                    flash = RewardCard1101.Img("HudFlash-" + ItemId, layer, BattleFxArt1021.Dot, Color.clear);
                    if (BattleFxArt1021.Additive != null) flash.material = BattleFxArt1021.Additive;
                    var rect = flash.rectTransform;
                    rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
                    rect.sizeDelta = new Vector2(150f, 150f);
                    rect.position = icon != null ? icon.position : Pill.TransformPoint(Pill.rect.center);
                }
            }

            public void Pulse() => pulseStart = Time.unscaledTime;

            /// <summary>Returns true when finished (the slot can be dropped).</summary>
            public bool Tick()
            {
                if (value == null || Pill == null) { Restore(); return true; }
                float now = Time.unscaledTime;
                if (ghost != null)
                {
                    float shown = Mathf.Clamp01((now - bornAt) / .18f);
                    float gone = retireAt >= 0f ? Mathf.Clamp01((now - retireAt) / .3f) : 0f;
                    ghost.alpha = shown * (1f - gone);
                    Pill.anchoredPosition = ghostHome + new Vector2(0f, 26f * (1f - shown) * (1f - shown) + 18f * gone);
                    if (gone >= 1f) { Restore(); return true; }
                    if (retireAt >= 0f) return false;
                }
                else if (holding && value.text != written) holding = false;   // the app refreshed it: its value wins
                if (pulseStart >= 0f)
                {
                    float k = (now - pulseStart) / .32f;
                    float s = k < 1f ? 1f + .14f * Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI) : 1f;
                    Pill.localScale = baseScale * s;
                    if (flash != null)
                    {
                        float a = Mathf.Clamp01(1f - (now - pulseStart) / .45f);
                        flash.color = new Color(color.r, color.g, color.b, .95f * a);
                        flash.rectTransform.localScale = Vector3.one * (.7f + .6f * (1f - a));
                    }
                    if (k >= 1.5f) pulseStart = -1f;
                }
                if (holding && rollStart >= 0f)
                {
                    float k = Mathf.Clamp01((now - rollStart) / .6f);
                    if (k >= 1f) { Write(to); holding = false; }
                    else Write(RollText(from, to, 1f - (1f - k) * (1f - k)));
                }
                bool done = !holding && pulseStart < 0f && !Flying;
                if (!done) return false;
                Pill.localScale = baseScale;
                DestroyFlash();
                if (ghost == null) return true;
                retireAt = now + .35f;    // let the new number sit a beat, then the copy leaves
                return false;
            }

            void DestroyFlash()
            {
                if (flash != null) UnityEngine.Object.Destroy(flash.gameObject);
                flash = null;
            }

            public void Restore()
            {
                if (ghost != null) { if (Pill != null) UnityEngine.Object.Destroy(Pill.gameObject); }
                else
                {
                    if (holding && value != null && value.text == written) value.text = to;
                    if (Pill != null) Pill.localScale = baseScale;
                }
                DestroyFlash();
            }
        }

        /// <summary>Interpolates two HUD texts ("1,200", "45/120"); texts it cannot parse (万/亿) jump at the end.</summary>
        public static string RollText(string from, string to, float t)
        {
            if (!TryParseHud(from, out long a, out _) || !TryParseHud(to, out long b, out string suffix)) return t >= 1f ? to : from;
            long v = (long)Math.Round(a + (b - a) * (double)Mathf.Clamp01(t));
            return v.ToString("N0", CultureInfo.InvariantCulture) + suffix;
        }

        static bool TryParseHud(string text, out long number, out string suffix)
        {
            number = 0; suffix = string.Empty;
            if (string.IsNullOrEmpty(text) || text.Contains("万") || text.Contains("亿")) return false;
            int slash = text.IndexOf('/');
            string head = slash >= 0 ? text.Substring(0, slash) : text;
            suffix = slash >= 0 ? text.Substring(slash) : string.Empty;
            if (slash >= 0) { number = 0; return long.TryParse(head.Replace(",", string.Empty).Trim(), out number); }
            return long.TryParse(head.Replace(",", string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number);
        }
    }

    /// <summary>The full 「获得奖励」 popup.</summary>
    public sealed class RewardPopup1101 : MonoBehaviour
    {
        RewardGain1101 owner;
        CanvasGroup group;
        RectTransform rays, raysTeal, band, caption, hint;
        readonly List<RectTransform> cards = new List<RectTransform>();
        readonly List<float> tilts = new List<float>();
        readonly List<(string ItemId, RectTransform Card)> currencyCards = new List<(string ItemId, RectTransform Card)>();
        Image dim;
        float clock, closeClock = -1f;
        bool reduce;
        Vector2 bandHome;

        public RewardGain1101.Presentation Shown { get; private set; }
        public IReadOnlyList<RectTransform> Cards => cards;
        public float Clock => clock;

        internal static RewardPopup1101 Open(RewardGain1101 owner, Transform hostTransform, Font font, RewardGain1101.Presentation p)
        {
            RectTransform root = owner.OverlayCanvas(RewardGain1101.PopupName, true);
            var popup = root.gameObject.AddComponent<RewardPopup1101>();
            popup.owner = owner;
            popup.Shown = p;
            popup.Build(root, font, p);
            return popup;
        }

        void Build(RectTransform root, Font font, RewardGain1101.Presentation p)
        {
            reduce = BattlePresentationOptions.ReduceMotion;
            group = root.gameObject.AddComponent<CanvasGroup>();
            Rect area = root.rect;
            float W = area.width > 1f ? area.width : 720f, H = area.height > 1f ? area.height : 1536f;

            dim = RewardCard1101.Img("Dim", root, null, new Color(.03f, .015f, .06f, .94f));
            RewardCard1101.Stretch(dim.rectTransform);
            dim.raycastTarget = true;
            var dots = RewardCard1101.Img("Halftone", root, CelShapes.Halftone, new Color(.62f, .4f, 1f, .07f));
            dots.type = Image.Type.Tiled; dots.pixelsPerUnitMultiplier = 2.4f;
            RewardCard1101.Stretch(dots.rectTransform);
            var tap = root.gameObject.AddComponent<Button>();
            tap.targetGraphic = dim; tap.transition = Selectable.Transition.None;
            tap.onClick.AddListener(Tapped);

            int n = p.Items.Count;
            float margin = Mathf.Max(40f, W * .065f), gap = W * .028f, rowGap = W * .045f;
            RewardCard1101.GridLayout(n, W - margin * 2f, gap, n <= 2 ? 216f : 168f, out int rows, out int perRow, out Vector2 card);
            float gridH = rows * card.y + (rows - 1) * rowGap;
            float centreY = H * .02f;                       // grid centre, slightly above the screen centre
            float bandW = Mathf.Min(W * .84f, 600f), bandH = bandW * 168f / 640f;
            // band, then the caption strip, then a clear gap above the cards (they pop in with an overshoot)
            float bandY = centreY + gridH * .5f + bandH * .5f + 92f;

            Sprite raySprite = Resources.Load<Sprite>("Art/Reward1101/reward-rays-1101");
            rays = RewardCard1101.Img("Rays", root, raySprite, new Color(.66f, .38f, 1f, .42f)).rectTransform;
            raysTeal = RewardCard1101.Img("RaysTeal", root, raySprite, new Color(.11f, .95f, .93f, .28f)).rectTransform;
            foreach (RectTransform r in new[] { rays, raysTeal })
            {
                var image = r.GetComponent<Image>();
                if (BattleFxArt1021.Additive != null) image.material = BattleFxArt1021.Additive;
                float size = Mathf.Max(W, gridH + 320f) * (r == rays ? 1.5f : 1.05f);
                RewardCard1101.Centre(r, new Vector2(0f, centreY + gridH * .1f), Vector2.one * size);
            }

            // ---- the teal torn band with 「获得奖励」, an ink "REWARD" tag and the caption under it
            band = RewardCard1101.NewRect("TitleBand", root);
            RewardCard1101.Centre(band, new Vector2(0f, bandY), new Vector2(bandW, bandH));
            band.localEulerAngles = new Vector3(0f, 0f, -3f);
            bandHome = band.anchoredPosition;
            Sprite bandSprite = Resources.Load<Sprite>("Art/Reward1101/reward-title-1101");
            var bandImage = RewardCard1101.Img("Band", band, bandSprite, bandSprite != null ? Color.white : (Color)Brand1025.Teal);
            RewardCard1101.Stretch(bandImage.rectTransform);
            var title = RewardCard1101.Label("Title", band, font, "获得奖励", Mathf.RoundToInt(bandH * .42f), Brand1025.Ink);
            title.fontStyle = FontStyle.BoldAndItalic;
            RewardCard1101.Stretch(title.rectTransform);
            title.rectTransform.anchoredPosition = new Vector2(0f, bandH * .02f);
            var titleEcho = title.gameObject.AddComponent<Shadow>();
            titleEcho.effectColor = new Color(1f, 1f, 1f, .85f); titleEcho.effectDistance = new Vector2(3f, -3f);
            var tag = RewardCard1101.NewRect("Eyebrow", band);
            tag.anchorMin = tag.anchorMax = new Vector2(0f, 1f); tag.pivot = new Vector2(0f, .5f);
            tag.sizeDelta = new Vector2(bandW * .26f, bandH * .26f); tag.anchoredPosition = new Vector2(bandW * .06f, -bandH * .08f);
            tag.localEulerAngles = new Vector3(0f, 0f, 6f);
            var tagPlate = RewardCard1101.New<SkewedPlate1021>("Plate", tag); tagPlate.color = Brand1025.Ink; tagPlate.SkewDegrees = -14f;
            tagPlate.raycastTarget = false; RewardCard1101.Stretch(tagPlate.rectTransform);
            var tagText = RewardCard1101.Label("Text", tag, font, "REWARD", Mathf.RoundToInt(bandH * .16f), Color.white);
            tagText.fontStyle = FontStyle.BoldAndItalic; RewardCard1101.Stretch(tagText.rectTransform);

            if (!string.IsNullOrEmpty(p.Caption))
            {
                caption = RewardCard1101.NewRect("Caption", root);
                var text = RewardCard1101.Label("Text", caption, font, p.Caption, 24, Color.white);
                float cw = Mathf.Min(W - margin * 2f, text.preferredWidth + 56f);
                RewardCard1101.Centre(caption, new Vector2(0f, bandY - bandH * .5f - 26f), new Vector2(cw, 44f));
                var plate = RewardCard1101.New<SkewedPlate1021>("Plate", caption); plate.color = new Color(.05f, .02f, .09f, .96f);
                plate.SkewDegrees = -10f; plate.raycastTarget = false; RewardCard1101.Stretch(plate.rectTransform);
                plate.transform.SetAsFirstSibling();
                var edge = RewardCard1101.New<SkewedPlate1021>("Edge", caption); edge.color = Brand1025.Teal; edge.SkewDegrees = -10f;
                edge.raycastTarget = false; edge.rectTransform.anchorMin = Vector2.zero; edge.rectTransform.anchorMax = new Vector2(0f, 1f);
                edge.rectTransform.pivot = new Vector2(0f, .5f); edge.rectTransform.sizeDelta = new Vector2(8f, 0f);
                edge.rectTransform.anchoredPosition = new Vector2(6f, 0f);
                RewardCard1101.Stretch(text.rectTransform);
                text.rectTransform.offsetMin = new Vector2(24f, 0f); text.rectTransform.offsetMax = new Vector2(-18f, 0f);
                text.resizeTextForBestFit = true; text.resizeTextMinSize = 14; text.resizeTextMaxSize = 24;
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.transform.SetAsLastSibling();
            }

            // ---- the cards
            List<Vector2> at = RewardCard1101.GridPositions(n, rows, perRow, card, gap, rowGap);
            for (int i = 0; i < n; i++)
            {
                (string itemId, int amount) = p.Items[i];
                RectTransform c = RewardCard1101.Create(root, font, itemId, amount, card, "RewardCard-" + i + "-" + itemId);
                c.anchoredPosition = at[i] + new Vector2(0f, centreY);
                float tilt = (i % 2 == 0 ? -1f : 1f) * (1.5f + (i * 37 % 5) * .3f);
                tilts.Add(tilt);
                c.localEulerAngles = new Vector3(0f, 0f, tilt);
                cards.Add(c);
                if (RewardCard1101.IsCurrency(itemId)) currencyCards.Add((itemId, c));
            }

            hint = RewardCard1101.NewRect("Hint", root);
            var hintText = RewardCard1101.Label("Text", hint, font, "点击任意处收起", 22, new Color(.86f, .8f, .98f, .85f));
            RewardCard1101.Stretch(hintText.rectTransform);
            hintText.gameObject.AddComponent<Outline>().effectColor = Brand1025.Ink;
            RewardCard1101.Centre(hint, new Vector2(0f, centreY - gridH * .5f - 70f), new Vector2(W * .8f, 40f));

            clock = reduce ? 5f : 0f;
            Pose();
        }

        /// <summary>First tap during the entrance finishes it; the next tap (or any tap after it) collects.</summary>
        public void Tapped()
        {
            if (closeClock >= 0f) return;
            if (clock < IntroLength) { clock = IntroLength; Pose(); return; }
            closeClock = 0f;
        }

        float IntroLength => .3f + cards.Count * .09f + .32f;

        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
            clock += dt;
            if (closeClock >= 0f)
            {
                closeClock += dt;
                float k = Mathf.Clamp01(closeClock / .2f);
                group.alpha = 1f - k;
                transform.localScale = Vector3.one * (1f - .05f * k);
                if (k >= 1f) Finish();
                return;
            }
            Pose();
        }

        void Finish()
        {
            var flights = new List<(string ItemId, Vector3 World)>();
            foreach ((string itemId, RectTransform card) in currencyCards)
                if (card != null) flights.Add((itemId, card.TransformPoint(card.rect.center)));
            if (owner != null) owner.PopupClosed(this, flights);
            Destroy(gameObject);
        }

        static float OutExpo(float t) { t = Mathf.Clamp01(t); return t >= 1f ? 1f : 1f - Mathf.Pow(2f, -10f * t); }
        static float Back(float t) { t = Mathf.Clamp01(t); const float c = 1.7f; float u = t - 1f; return 1f + (c + 1f) * u * u * u + c * u * u; }

        void Pose()
        {
            float t = clock;
            dim.color = new Color(.03f, .015f, .06f, .94f * Mathf.Clamp01(t / .15f));
            float spin = reduce ? 0f : t * 7f;
            rays.localEulerAngles = new Vector3(0f, 0f, spin);
            raysTeal.localEulerAngles = new Vector3(0f, 0f, -spin * 1.4f + 9f);
            float rayIn = OutExpo(t / .35f);
            rays.localScale = raysTeal.localScale = Vector3.one * Mathf.Lerp(.4f, 1f, rayIn);
            float breathe = reduce ? 1f : 1f + .04f * Mathf.Sin(t * 2.2f);
            rays.GetComponent<Image>().color = new Color(.66f, .38f, 1f, .42f * rayIn * breathe);

            float b = OutExpo((t - .04f) / .2f);
            band.anchoredPosition = bandHome + new Vector2(-900f * (1f - b), 0f);
            band.localScale = Vector3.one * Mathf.Lerp(1.25f, 1f, b);
            if (caption != null)
            {
                float c = Mathf.Clamp01((t - .2f) / .18f);
                caption.localScale = new Vector3(1f, c, 1f);
            }
            for (int i = 0; i < cards.Count; i++)
            {
                float start = .3f + i * .09f;
                float k = (t - start) / .26f;
                RectTransform card = cards[i];
                card.localScale = Vector3.one * (k <= 0f ? 0f : Back(k));
                card.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(-16f, tilts[i], Mathf.Clamp01(k)));
                Transform spark = card.Find("Spark");
                if (spark != null)
                {
                    float s = Mathf.Clamp01((t - start - .12f) / .35f);
                    spark.localScale = Vector3.one * (s <= 0f ? 0f : s < .4f ? s / .4f * 1.6f : Mathf.Lerp(1.6f, .8f, (s - .4f) / .6f));
                    spark.localEulerAngles = new Vector3(0f, 0f, t * 40f);
                }
            }
            float h = Mathf.Clamp01((t - IntroLength) / .25f);
            var hintText = hint.GetComponentInChildren<Text>();
            if (hintText != null) hintText.color = new Color(.86f, .8f, .98f, .85f * h * (reduce ? 1f : .7f + .3f * Mathf.Sin(t * 3.4f)));
        }
    }

    /// <summary>One HUD badge flying from where the reward was claimed into its HUD pill.</summary>
    internal sealed class RewardFlyer1101 : MonoBehaviour
    {
        RectTransform rect, trail;
        RewardGain1101.HudSlot slot;
        Vector3 start, burst;
        Vector3? fixedEnd;
        float clock, delay;
        bool last, landed;
        const float BurstTime = .2f, FlyTime = .48f;

        public static void Spawn(RectTransform layer, string itemId, Vector3 fromWorld, RewardGain1101.HudSlot slot, float delay, int index, bool last,
            Vector3? fixedEnd = null)
        {
            var image = RewardCard1101.Img("Flyer-" + itemId + "-" + index, layer, RewardItemVisuals.SpriteFor(itemId), Color.white);
            image.preserveAspect = true;
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(58f, 58f);
            rect.position = fromWorld;
            rect.localScale = Vector3.zero;
            var flyer = image.gameObject.AddComponent<RewardFlyer1101>();
            flyer.rect = rect; flyer.slot = slot; flyer.delay = delay; flyer.last = last; flyer.fixedEnd = fixedEnd;
            flyer.start = fromWorld;
            float angle = (index * 137.5f + 20f) * Mathf.Deg2Rad;
            float radius = 46f + (index % 3) * 18f;
            Vector3 lossy = layer.lossyScale;
            flyer.burst = fromWorld + new Vector3(Mathf.Cos(angle) * radius * lossy.x, Mathf.Sin(angle) * radius * lossy.y, 0f);
            var glow = RewardCard1101.Img("Trail", rect, BattleFxArt1021.Dot, RewardCard1101.QualityColor(itemId) * new Color(1f, 1f, 1f, .7f));
            if (BattleFxArt1021.Additive != null) glow.material = BattleFxArt1021.Additive;
            RewardCard1101.Stretch(glow.rectTransform);
            glow.rectTransform.offsetMin = new Vector2(-20f, -20f); glow.rectTransform.offsetMax = new Vector2(20f, 20f);
            glow.transform.SetAsFirstSibling();
            flyer.trail = glow.rectTransform;
        }

        void Update()
        {
            clock += Mathf.Min(Time.unscaledDeltaTime, .05f);
            float t = clock - delay;
            if (t < 0f) return;
            if (!fixedEnd.HasValue && (slot == null || slot.Pill == null)) { Destroy(gameObject); return; }
            if (t < BurstTime)
            {
                float k = t / BurstTime, e = 1f - (1f - k) * (1f - k) * (1f - k);
                rect.position = Vector3.LerpUnclamped(start, burst, e);
                rect.localScale = Vector3.one * Mathf.Lerp(.3f, 1.1f, e);
                return;
            }
            Vector3 end;
            if (fixedEnd.HasValue) end = fixedEnd.Value;
            else
            {
                Transform icon = slot.Pill.Find("DiamondIcon") ?? slot.Pill.Find("GoldIcon") ?? slot.Pill.Find("StaminaIcon");
                end = icon != null ? icon.position : slot.Pill.TransformPoint(slot.Pill.rect.center);
            }
            float f = Mathf.Clamp01((t - BurstTime) / FlyTime), ease = f * f * (3f - 2f * f) * .4f + f * f * .6f;
            Vector3 control = (burst + end) * .5f + new Vector3((burst.x - end.x) * .25f, Mathf.Abs(end.y - burst.y) * .15f, 0f);
            Vector3 a = Vector3.LerpUnclamped(burst, control, ease), b = Vector3.LerpUnclamped(control, end, ease);
            rect.position = Vector3.LerpUnclamped(a, b, ease);
            rect.localScale = Vector3.one * Mathf.Lerp(1.1f, .55f, f);
            if (trail != null) trail.localScale = Vector3.one * (1f + .4f * f);
            if (fixedEnd.HasValue)
            {
                // No pill to land in: the icon shrinks and fades out at the header instead.
                var image = GetComponent<Image>();
                if (image != null) image.color = new Color(1f, 1f, 1f, 1f - Mathf.Clamp01((f - .7f) / .3f));
                if (f >= 1f) Destroy(gameObject);
                return;
            }
            if (f >= 1f && !landed)
            {
                landed = true;
                slot.Land();
                slot.Pulse();
                if (last) slot.Flying = false;
                Destroy(gameObject);
            }
        }
    }

    /// <summary>The small "+20 [icon]" sticker that rises from where a routine income was claimed.</summary>
    internal sealed class LiteChip1101 : MonoBehaviour
    {
        RectTransform rect;
        CanvasGroup group;
        Vector2 home;
        float clock;

        public static void Spawn(RectTransform layer, Font font, Vector3 atWorld, List<(string ItemId, int Amount)> items, string caption)
        {
            var root = RewardCard1101.NewRect("LiteChip", layer);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(.5f, .5f);
            root.position = atWorld;
            var chip = root.gameObject.AddComponent<LiteChip1101>();
            chip.rect = root;
            chip.group = root.gameObject.AddComponent<CanvasGroup>();
            chip.group.blocksRaycasts = false;
            const float rowH = 50f, gap = 16f, padX = 26f, padY = 10f;
            var parts = new List<(RectTransform Part, float Width)>();
            float itemsW = 0f;
            foreach ((string itemId, int amount) in items)
            {
                var part = RewardCard1101.NewRect("Item-" + itemId, root);
                part.anchorMin = part.anchorMax = part.pivot = new Vector2(.5f, .5f);
                var icon = RewardCard1101.Img("Icon", part, RewardItemVisuals.SpriteFor(itemId), Color.white);
                icon.preserveAspect = true;
                var text = RewardCard1101.Label("Amount", part, font, "+" + amount.ToString("N0", CultureInfo.InvariantCulture), 30, Color.white);
                text.fontStyle = FontStyle.BoldAndItalic; text.alignment = TextAnchor.MiddleLeft;
                float tw = text.preferredWidth + 6f, pw = 50f + tw;
                RewardCard1101.Centre(icon.rectTransform, new Vector2(-pw * .5f + 24f, 0f), new Vector2(48f, 48f));
                text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(.5f, .5f);
                text.rectTransform.pivot = new Vector2(0f, .5f);
                text.rectTransform.sizeDelta = new Vector2(tw, rowH);
                text.rectTransform.anchoredPosition = new Vector2(-pw * .5f + 50f, 0f);
                var stroke = text.gameObject.AddComponent<Outline>(); stroke.effectColor = Brand1025.Ink; stroke.effectDistance = new Vector2(2f, -2f);
                Color q = RewardCard1101.QualityColor(itemId);
                var drop = text.gameObject.AddComponent<Shadow>(); drop.effectColor = new Color(q.r * .6f, q.g * .6f, q.b * .6f, 1f); drop.effectDistance = new Vector2(3f, -3f);
                parts.Add((part, pw));
                itemsW += pw + (parts.Count > 1 ? gap : 0f);
            }
            Text captionText = null;
            float captionH = 0f;
            if (!string.IsNullOrEmpty(caption))
            {
                captionText = RewardCard1101.Label("Caption", root, font, caption, 18, new Color(.88f, .84f, 1f, 1f));
                captionH = 26f;
            }
            float contentW = Mathf.Max(itemsW, captionText != null ? captionText.preferredWidth : 0f);
            float plateW = contentW + padX * 2f, plateH = rowH + captionH + padY * 2f;
            var shadow = RewardCard1101.New<SkewedPlate1021>("Shadow", root); shadow.color = new Color(0f, 0f, 0f, .55f); shadow.SkewDegrees = -10f;
            shadow.raycastTarget = false; RewardCard1101.Centre(shadow.rectTransform, new Vector2(5f, -5f), new Vector2(plateW, plateH));
            var plate = RewardCard1101.New<SkewedPlate1021>("Plate", root); plate.color = new Color(.05f, .02f, .09f, .94f); plate.SkewDegrees = -10f;
            plate.raycastTarget = false; RewardCard1101.Centre(plate.rectTransform, Vector2.zero, new Vector2(plateW, plateH));
            var edge = RewardCard1101.New<SkewedPlate1021>("Edge", root); edge.color = Brand1025.Teal; edge.SkewDegrees = -10f; edge.raycastTarget = false;
            RewardCard1101.Centre(edge.rectTransform, new Vector2(-plateW * .5f + 6f, 0f), new Vector2(7f, plateH));
            shadow.transform.SetAsFirstSibling(); plate.transform.SetSiblingIndex(1); edge.transform.SetSiblingIndex(2);
            float top = plateH * .5f - padY;
            if (captionText != null)
            {
                RewardCard1101.Centre(captionText.rectTransform, new Vector2(0f, top - captionH * .5f), new Vector2(contentW + 4f, captionH));
                top -= captionH;
            }
            float x = -itemsW * .5f;
            foreach ((RectTransform part, float width) in parts)
            {
                RewardCard1101.Centre(part, new Vector2(x + width * .5f, top - rowH * .5f), new Vector2(width, rowH));
                x += width + gap;
            }
            // sits just above where the claim was tapped, then rises
            root.anchoredPosition += new Vector2(0f, plateH * .5f + 20f);
            Rect bounds = layer.rect;
            float halfW = plateW * .5f + 14f;
            Vector2 at = root.anchoredPosition;
            at.x = Mathf.Clamp(at.x, bounds.xMin + halfW, bounds.xMax - halfW);
            root.anchoredPosition = at;
            chip.home = root.anchoredPosition;
            root.localEulerAngles = new Vector3(0f, 0f, 2f);
        }

        void Update()
        {
            clock += Mathf.Min(Time.unscaledDeltaTime, .05f);
            float rise = 1f - Mathf.Pow(1f - Mathf.Clamp01(clock / .5f), 3f);
            rect.anchoredPosition = home + new Vector2(0f, 70f * rise + 18f * Mathf.Clamp01((clock - .5f) / .8f));
            rect.localScale = Vector3.one * (clock < .14f ? Mathf.Lerp(.5f, 1.12f, clock / .14f) : Mathf.Lerp(1.12f, 1f, Mathf.Clamp01((clock - .14f) / .12f)));
            group.alpha = clock < 1f ? 1f : 1f - Mathf.Clamp01((clock - 1f) / .35f);
            if (clock > 1.4f) Destroy(gameObject);
        }
    }
}
