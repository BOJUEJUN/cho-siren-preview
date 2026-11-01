using System.Collections.Generic;
using ChoSiren.Systems.Tactics;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.Panels
{
    public sealed partial class TacticsBattlePanel
    {
        // 2026-10-26 (赵金): effects must read as what the skill says — status badges with value and rounds on every unit,
        // chains on enemies sealed by 魅语迷心, a water bubble on members under 深海佑域, a big green plus for heals.
        private readonly HashSet<CellView> skillVisualCells1026 = new HashSet<CellView>();

        private RectTransform bossBadgeAnchor1026;

        private void EnsureSkillVisuals1026(CellView cell)
        {
            if (psdRoot == null || cell == null || cell.Unit == null) return;
            // The boss has no name plate of its own: its badges hang under the top health bar.
            // 2026-10-31 (赵金): no longer a strip under the top HUD — the boss's statuses hang on its own head plate.
            if (bossBadgeAnchor1026 == null && bossUnitId >= 0) bossBadgeAnchor1026 = BuildBossPlate1031();
            if (skillVisualCells1026.Add(cell))
            {
                CellView bound = cell;
                if (cell.Side == BattleSide.Player)
                {
                    // A column down the card's left edge; the record's top-right holds the skill cue.
                    // 2026-11-02 visual-battle: compact — the symbol is the name, only the rounds are written.
                    StatusBadges1026.Attach(cell.Rect, kit.Font, () => battle.StatusesOf(bound.Unit), false,
                        new Vector2(6f, -420f * PsdScale), 24f, true);
                    WaterWard1026.Ensure(cell.Rect, new Rect(40f * PsdScale, 300f * PsdScale, 630f * PsdScale, 760f * PsdScale),
                        () => battle.HasMermaidWard(bound.Unit));
                }
                else
                {
                    // The name-plate art can sit outside the cell rect: hang the row on the plate itself.
                    RectTransform plate = psdFrames.TryGetValue(cell, out Image frame) && frame != null ? frame.rectTransform : cell.Rect;
                    // 2026-10-31: compact icon + rounds chips tucked under the (smaller) head plate.
                    StatusBadges1026.Attach(plate, kit.Font, () => battle.StatusesOf(bound.Unit), true,
                        EnemyPlateBadgeOffset1031, EnemyPlateBadgeHeight1031, true);
                }
            }
            if (cell.Side == BattleSide.Enemy)
            {
                BattleUnit unit = cell.Unit;
                SealChains1026.Ensure(UnitVisual(unit), BattlePsdArt928.Sprite("icon-heart-lock"), () => battle.IsSealed(unit));
            }
        }

        /// <summary>The seal just landed: the chains tighten in again.</summary>
        private void SnapSealChains1026(BattleUnit target)
        {
            RectTransform figure = UnitVisual(target);
            if (figure == null) return;
            var chains = figure.GetComponentInChildren<SealChains1026>(true);
            if (chains == null) chains = SealChains1026.Ensure(figure, BattlePsdArt928.Sprite("icon-heart-lock"), () => battle.IsSealed(target));
            chains?.Snap();
        }

        private void PlayHealBurst1026(BattleUnit target)
        {
            if (psdRoot == null || target == null || target.Side != BattleSide.Player) return;
            CellView cell = FindCell(target);
            if (cell != null) HealBurst1026.Play(cell.Rect, BattlePsdArt928.Sprite("effect-heal-plus"), kit.RadialSprite());
        }
    }
}
