using ChoSiren.Systems.Tactics;
using UnityEngine;

namespace ChoSiren
{
    public static class CombatFeedbackPalette
    {
        public static readonly Color Attack = new Color32(92, 226, 255, 255);
        public static readonly Color Hurt = new Color32(255, 103, 113, 255);
        public static readonly Color Critical = new Color32(255, 180, 74, 255);
        public static readonly Color Heal = new Color32(94, 235, 148, 255);
        public static readonly Color Shield = new Color32(103, 171, 255, 255);
        public static readonly Color Control = new Color32(255, 219, 99, 255);
        public static readonly Color Debuff = new Color32(199, 135, 255, 255);

        public static Color Skill(string id, BattleSide side = BattleSide.Player)
        {
            if (id == "rt-mermaid-small" || id == "rt-cleanse") return Heal;
            if (id == "rt-mermaid-big" || id == "rt-control-ward" || id == "rt-tide-hand" || id == "rt-elite-harden" || id == "rt-boss-barrier") return Shield;
            if (id == "rt-stun" || id == "rt-slow" || id == "rt-interrupt" || id == "rt-resist" || id == "rt-cast" || id == "rt-charm-big") return Control;
            if (id == "rt-armor-break" || id == "rt-poison" || id == "rt-demon-small" || id == "rt-demon-big") return Debuff;
            return side == BattleSide.Player ? Attack : Hurt;
        }

        public static Color Status(string value)
        {
            if (value.Contains("眩晕") || value.Contains("蓄力") || value.Contains("封技") || value.Contains("迟缓")) return Control;
            if (value.Contains("毒") || value.Contains("破甲") || value.Contains("禁疗")) return Debuff;
            if (value.Contains("护盾") || value.Contains("控免") || value.Contains("减伤") || value.Contains("硬化")) return Shield;
            return Attack;
        }
    }
}
