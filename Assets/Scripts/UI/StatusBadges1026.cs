using System;
using System.Collections.Generic;
using ChoSiren.Panels;
using ChoSiren.Systems.Tactics;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>
    /// Status badges hung on a unit (2026-10-26, 赵金: "这些图标必须挂在角色身上，不挂别人看不懂减了多少、多少秒"):
    /// up to three dark plates, each with the effect's PSD symbol and a short readout — "封印 2回合", "攻-20%", "毒×3".
    /// </summary>
    public sealed class StatusBadges1026 : MonoBehaviour
    {
        const int MaxBadges = 3;
        const float RefreshSeconds = .2f;

        sealed class Badge { public RectTransform Root; public Image Icon; public Text Label; }

        readonly List<Badge> badges = new List<Badge>();
        Func<List<UnitStatus>> probe;
        RectTransform row;
        float height, nextRefresh;
        /// <summary>2026-10-31 enemy head plates: icon + rounds only ("2回合", "×3"), the name is the icon.</summary>
        bool compact;

        /// <summary>
        /// Hangs badges on <paramref name="host"/>. Enemies: a row below the name plate (<paramref name="hangBelow"/>).
        /// Members: a column down the card's left edge from the top-left (the record's top-right holds the skill cue).
        /// </summary>
        public static StatusBadges1026 Attach(RectTransform host, Font font, Func<List<UnitStatus>> probe, bool hangBelow,
            Vector2 offset, float height = 26f, bool compact = false)
        {
            var go = new GameObject("StatusBadges1026", typeof(RectTransform));
            go.transform.SetParent(host, false);
            var badges = go.AddComponent<StatusBadges1026>();
            badges.compact = compact;
            badges.Build(font, probe, hangBelow, offset, height);
            return badges;
        }

        void Build(Font font, Func<List<UnitStatus>> source, bool hangBelow, Vector2 offset, float rowHeight)
        {
            probe = source; height = rowHeight;
            row = (RectTransform)transform;
            row.anchorMin = row.anchorMax = new Vector2(0f, hangBelow ? 0f : 1f);
            row.pivot = new Vector2(0f, 1f);
            row.anchoredPosition = offset;
            row.sizeDelta = hangBelow ? new Vector2(400f, height) : new Vector2(200f, MaxBadges * (height + 4f));
            HorizontalOrVerticalLayoutGroup layout = hangBelow
                ? (HorizontalOrVerticalLayoutGroup)row.gameObject.AddComponent<HorizontalLayoutGroup>()
                : row.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 4f; layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = false; layout.childControlHeight = false;
            layout.childForceExpandWidth = false; layout.childForceExpandHeight = false;
            for (int i = 0; i < MaxBadges; i++)
            {
                var plate = new GameObject("Badge-" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                plate.transform.SetParent(row, false);
                var back = plate.GetComponent<Image>();
                back.sprite = PunkUi.SlantFill; back.color = new Color32(8, 5, 18, 232); back.raycastTarget = false;
                var rim = plate.AddComponent<Outline>();
                rim.effectColor = new Color(Brand1025.Teal.r / 255f, Brand1025.Teal.g / 255f, Brand1025.Teal.b / 255f, .55f);
                rim.effectDistance = new Vector2(1f, -1f);
                var icon = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
                icon.transform.SetParent(plate.transform, false);
                icon.preserveAspect = true; icon.raycastTarget = false;
                var ir = icon.rectTransform; ir.anchorMin = ir.anchorMax = new Vector2(0f, .5f); ir.pivot = new Vector2(0f, .5f);
                ir.sizeDelta = new Vector2(height - 2f, height - 2f); ir.anchoredPosition = new Vector2(3f, 0f);
                var label = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)).GetComponent<Text>();
                label.transform.SetParent(plate.transform, false);
                label.font = font; label.fontSize = Mathf.RoundToInt(height * (compact ? .6f : .54f)); label.fontStyle = FontStyle.Bold;
                label.color = Color.white; label.alignment = TextAnchor.MiddleLeft; label.raycastTarget = false;
                label.horizontalOverflow = HorizontalWrapMode.Overflow; label.verticalOverflow = VerticalWrapMode.Overflow;
                var lr = label.rectTransform; lr.anchorMin = new Vector2(0f, 0f); lr.anchorMax = new Vector2(1f, 1f);
                lr.offsetMin = new Vector2(height + 2f, 0f); lr.offsetMax = new Vector2(-6f, 0f);
                plate.SetActive(false);
                badges.Add(new Badge { Root = (RectTransform)plate.transform, Icon = icon, Label = label });
            }
        }

        /// <summary>
        /// 2026-10-31: a plate on the stage's right edge grows its row leftwards from its right corner, so the chips never
        /// run off screen. Only for rows hung below a plate.
        /// </summary>
        public void AlignRow(bool fromRight, Vector2 offset)
        {
            if (row == null) return;
            float x = fromRight ? 1f : 0f;
            row.anchorMin = row.anchorMax = new Vector2(x, row.anchorMin.y);
            row.pivot = new Vector2(x, 1f);
            row.anchoredPosition = new Vector2(fromRight ? -offset.x : offset.x, offset.y);
            var layout = row.GetComponent<HorizontalOrVerticalLayoutGroup>();
            if (layout != null) layout.childAlignment = fromRight ? TextAnchor.UpperRight : TextAnchor.UpperLeft;
        }

        void LateUpdate()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + RefreshSeconds;
            Refresh();
        }

        public void Refresh()
        {
            List<UnitStatus> statuses = probe != null ? probe() : null;
            int count = statuses == null ? 0 : Mathf.Min(MaxBadges, statuses.Count);
            for (int i = 0; i < badges.Count; i++)
            {
                Badge badge = badges[i];
                bool used = i < count;
                if (badge.Root.gameObject.activeSelf != used) badge.Root.gameObject.SetActive(used);
                if (!used) continue;
                UnitStatus status = statuses[i];
                badge.Icon.sprite = BattlePsdArt928.Sprite(IconKey(status.Kind));
                badge.Icon.enabled = badge.Icon.sprite != null;
                badge.Label.text = compact ? CompactLabel(status)
                    : status.Rounds > 0 ? status.Value + " " + status.Rounds + "回合" : status.Value;
                badge.Label.color = IsBuff(status.Kind) ? (Color)Brand1025.Teal : Color.white;
                badge.Root.sizeDelta = new Vector2(height + 10f + badge.Label.preferredWidth, height);
            }
            transform.SetAsLastSibling();
        }

        /// <summary>Icon-plate readout: rounds left, else the number part of the value ("毒×3" → "×3").</summary>
        public static string CompactLabel(UnitStatus status)
        {
            if (status.Rounds > 0) return status.Rounds + "回合";
            string value = status.Value ?? string.Empty;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (char.IsDigit(c) || c == '×' || c == '-' || c == '+') return value.Substring(i);
            }
            return value;
        }

        static bool IsBuff(UnitStatusKind kind) =>
            kind == UnitStatusKind.Shield || kind == UnitStatusKind.Reduction || kind == UnitStatusKind.AttackBuff;

        /// <summary>The PSD symbol each status wears (the same art as the skill that causes it).</summary>
        public static string IconKey(UnitStatusKind kind)
        {
            switch (kind)
            {
                case UnitStatusKind.Sealed: return "icon-heart-lock";
                case UnitStatusKind.Stunned: return "icon-demon-mask";
                case UnitStatusKind.TideSuppressed: return "icon-wave-dice";
                case UnitStatusKind.ArmorBreak: return "icon-gold-shield";
                case UnitStatusKind.Poison: return "icon-purple-drop";
                case UnitStatusKind.AntiHeal: return "icon-virus-dice";
                case UnitStatusKind.Shield:
                case UnitStatusKind.Reduction: return "icon-cyan-shield";
                case UnitStatusKind.AttackBuff: return "icon-dagger";
                default: return "icon-dice";
            }
        }
    }
}
