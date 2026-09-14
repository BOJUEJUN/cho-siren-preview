using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>
    /// Semantic icon vocabulary for combat feedback: heal/shield/buff/debuff reads come from
    /// <see cref="SkillIconVisuals"/> glyphs instead of stacked sentences. All helpers are
    /// allocation-free at draw time and every produced Graphic keeps raycastTarget off.
    /// </summary>
    public static class CombatStatusIconVisuals
    {
        /// <summary>Realtime/turn-based skill id → pictogram family.</summary>
        public static SkillIconKind ForSkill(string skillId)
        {
            switch (skillId)
            {
                case "rt-mermaid-small":
                case "rt-cleanse":
                    return SkillIconKind.Heal;
                case "rt-mermaid-big":
                case "rt-tide-hand":
                case "rt-tactical-guard":
                case "rt-control-ward":
                case "rt-elite-harden":
                case "rt-boss-barrier":
                case "rt-resist":
                    return SkillIconKind.Shield;
                case "rt-poison":
                case "rt-demon-small":
                case "rt-demon-big":
                    return SkillIconKind.Poison;
                case "rt-armor-break":
                    return SkillIconKind.Pierce;
                case "rt-stun":
                case "rt-slow":
                case "rt-interrupt":
                case "rt-charm-big":
                case "rt-cast":
                    return SkillIconKind.Charm;
                case "rt-enemy-finale":
                    return SkillIconKind.Burst;
                default:
                    return SkillIconKind.Slash;
            }
        }

        /// <summary>Short status label (眩晕/破甲/护盾…) → pictogram family.</summary>
        public static SkillIconKind ForStatusText(string value)
        {
            if (string.IsNullOrEmpty(value)) return SkillIconKind.Burst;
            if (value.Contains("护盾") || value.Contains("守护") || value.Contains("减伤") ||
                value.Contains("硬化") || value.Contains("控免")) return SkillIconKind.Shield;
            if (value.Contains("疗") || value.Contains("净化") || value.Contains("恢复"))
                return SkillIconKind.Heal;
            if (value.Contains("毒") || value.Contains("禁疗")) return SkillIconKind.Poison;
            if (value.Contains("破甲") || value.Contains("穿甲") || value.Contains("防↓"))
                return SkillIconKind.Pierce;
            if (value.Contains("攻") || value.Contains("暴") || value.Contains("伤"))
                return SkillIconKind.Slash;
            return SkillIconKind.Burst;
        }

        /// <summary>Default feedback tint per icon family, aligned with the combat palette.</summary>
        public static Color ColorFor(SkillIconKind kind)
        {
            switch (kind)
            {
                case SkillIconKind.Heal: return CombatFeedbackPalette.Heal;
                case SkillIconKind.Shield: return CombatFeedbackPalette.Shield;
                case SkillIconKind.Poison:
                case SkillIconKind.Pierce: return CombatFeedbackPalette.Debuff;
                case SkillIconKind.Charm: return CombatFeedbackPalette.Control;
                case SkillIconKind.Burst: return CombatFeedbackPalette.Critical;
                default: return CombatFeedbackPalette.Attack;
            }
        }

        /// <summary>Creates one glyph instance; the caller owns placement and lifecycle.</summary>
        public static SkillIconGraphic CreateIcon(Transform parent, string name, float size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(SkillIconGraphic));
            go.transform.SetParent(parent, false);
            SkillIconGraphic icon = go.GetComponent<SkillIconGraphic>();
            icon.raycastTarget = false;
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(.5f, .5f);
            icon.rectTransform.pivot = new Vector2(.5f, .5f);
            icon.rectTransform.sizeDelta = Vector2.one * size;
            return icon;
        }
    }

    /// <summary>
    /// Flat annulus used by attack hit feedback. The mesh is authored once per layout change;
    /// callers animate size/alpha through the RectTransform and color, never new textures.
    /// </summary>
    public sealed class CombatRingGraphic : MaskableGraphic
    {
        [Range(.05f, .95f)]
        public float thickness = .18f;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            const int segments = 48;
            Rect rect = rectTransform.rect;
            Vector2 center = rect.center;
            Vector2 outer = new Vector2(rect.width, rect.height) * .5f;
            float inner = 1f - Mathf.Clamp01(thickness);
            Color tint = color;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                float b = (i + 1) * Mathf.PI * 2f / segments;
                Vector2 outerA = center + new Vector2(Mathf.Cos(a) * outer.x, Mathf.Sin(a) * outer.y);
                Vector2 outerB = center + new Vector2(Mathf.Cos(b) * outer.x, Mathf.Sin(b) * outer.y);
                Vector2 innerA = center + new Vector2(Mathf.Cos(a) * outer.x * inner,
                    Mathf.Sin(a) * outer.y * inner);
                Vector2 innerB = center + new Vector2(Mathf.Cos(b) * outer.x * inner,
                    Mathf.Sin(b) * outer.y * inner);
                int first = vh.currentVertCount;
                vh.AddVert(outerA, tint, Vector2.zero);
                vh.AddVert(innerA, tint, Vector2.zero);
                vh.AddVert(innerB, tint, Vector2.zero);
                vh.AddVert(outerB, tint, Vector2.zero);
                vh.AddTriangle(first, first + 1, first + 2);
                vh.AddTriangle(first, first + 2, first + 3);
            }
        }
    }
}
