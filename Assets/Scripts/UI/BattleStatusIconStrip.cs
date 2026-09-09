using System;
using System.Collections.Generic;
using ChoSiren.Panels;
using ChoSiren.Systems.Tactics;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>
    /// Small semantic status chips for a player card: shield/guard, poison, pierce, charm pursuit
    /// and control. Shapes come from <see cref="SkillIconVisuals"/> so they read without colour.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleStatusIconStrip : MonoBehaviour
    {
        private const int SlotCount = 4;
        private static readonly Color SlotBackground = new Color32(12, 16, 40, 214);

        private sealed class Slot
        {
            public GameObject Root;
            public SkillIconGraphic Icon;
        }

        private readonly List<Slot> slots = new List<Slot>();
        private readonly List<SkillIconKind> visible = new List<SkillIconKind>();
        private BattleSimulator battle;
        private Func<BattleUnit> unitProbe;

        /// <summary>Kinds currently drawn, in draw order. Used by presentation tests.</summary>
        public IReadOnlyList<SkillIconKind> VisibleKinds => visible;

        internal void Configure(RectTransform host, BattleSimulator battle, Func<BattleUnit> unitProbe, PanelKit kit)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            if (kit == null) throw new ArgumentNullException(nameof(kit));
            this.battle = battle;
            this.unitProbe = unitProbe;
            for (int index = 0; index < SlotCount; index++)
            {
                GameObject root = kit.NewPanel("StatusIcon-" + index, host, SlotBackground, 6);
                PanelKit.PlaceTop(root.GetComponent<RectTransform>(), 6 + index * 20, 6, 18, 18);
                SkillIconGraphic icon = SkillIconVisuals.Create(root.transform, "StatusGlyph-" + index,
                    string.Empty, string.Empty, PanelKit.White);
                PanelKit.PlaceTop(icon.rectTransform, 3, 3, 12, 12);
                slots.Add(new Slot { Root = root, Icon = icon });
                root.SetActive(false);
            }
            Refresh();
        }

        public void Refresh()
        {
            visible.Clear();
            BattleUnit unit = unitProbe?.Invoke();
            if (battle == null || unit == null || !unit.Alive || !battle.IsRealtime)
            {
                Apply();
                return;
            }

            if (battle.ConditionRemaining(unit, CombatCondition.Stun) > 0)
                visible.Add(SkillIconKind.Burst);
            else if (battle.ConditionRemaining(unit, CombatCondition.ArmorBreak) > 0)
                visible.Add(SkillIconKind.Pierce);
            if (unit.Shield > 0) visible.Add(SkillIconKind.Shield);
            if (unit.Id == battle.CurrentLeaderId)
            {
                if (battle.CaptainPoisonLayers > 0) visible.Add(SkillIconKind.Poison);
                if (battle.CaptainPiercePermille > 0) visible.Add(SkillIconKind.Pierce);
                if (battle.CaptainComboCount > 0) visible.Add(SkillIconKind.Charm);
                if (battle.CaptainShieldPermille > 0) visible.Add(SkillIconKind.Shield);
            }
            Apply();
        }

        private void Apply()
        {
            for (int index = 0; index < slots.Count; index++)
            {
                bool used = index < visible.Count;
                slots[index].Root.SetActive(used);
                if (!used) continue;
                SkillIconKind kind = visible[index];
                slots[index].Icon.Kind = kind;
                slots[index].Icon.color = ColorFor(kind);
            }
        }

        private static Color ColorFor(SkillIconKind kind)
        {
            switch (kind)
            {
                case SkillIconKind.Shield: return CombatFeedbackPalette.Shield;
                case SkillIconKind.Poison: return CombatFeedbackPalette.Debuff;
                case SkillIconKind.Pierce: return PanelKit.Gold;
                case SkillIconKind.Charm: return PanelKit.Pink;
                default: return CombatFeedbackPalette.Control;
            }
        }
    }
}
