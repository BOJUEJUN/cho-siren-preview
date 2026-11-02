using System.Collections.Generic;
using ChoSiren.Systems.Tactics;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.Panels
{
    public sealed partial class TacticsBattlePanel
    {
        // 2026-10-31 (赵金): the acting member's bust swells out of her card instead of the whole card stepping up 1.06x.
        private readonly Dictionary<CellView, HeroCardLunge1031> heroLunges1031 = new Dictionary<CellView, HeroCardLunge1031>();
        /// <summary>How far above the sleeve's top edge the swelling bust may break out (canvas px).</summary>
        private const float HeroLungeHeadroom1031 = 140f;
        /// <summary>Side inset (fraction of the sleeve width) so the swelling bust stays inside the sleeve's rim art.</summary>
        private const float HeroLungeRim1031 = .045f;

        /// <summary>
        /// Plays the in-card act-out for a member's action. Returns true when the PSD card owns the motion
        /// (the old <see cref="EnemyUnitPresentation.PlayAttack"/> nudge must then stay off so the two never fight).
        /// </summary>
        private bool PlayHeroLunge1031(CellView cell, BattleUnit actor, string skillId)
        {
            HeroCardLunge1031 lunge = HeroLunge1031(cell);
            if (lunge == null || actor == null) return false;
            HeroCardLunge1031.Tier tier = skillId == battle.ActiveSkillId(actor, true) ? HeroCardLunge1031.Tier.Ultimate
                : skillId == battle.ActiveSkillId(actor, false) ? HeroCardLunge1031.Tier.Skill
                : HeroCardLunge1031.Tier.Basic;
            FitHeroClip1031(cell, lunge);
            if (lunge.Play(tier)) RaiseHeroCard1031(cell);
            return true;
        }

        /// <summary>The member's strike connected: the bust takes its contact jolt.</summary>
        private void HeroLungeImpact1031(BattleUnit actor)
        {
            if (actor == null || actor.Side != BattleSide.Player) return;
            CellView cell = FindCell(actor);
            if (cell != null && heroLunges1031.TryGetValue(cell, out HeroCardLunge1031 lunge) && lunge != null) lunge.Impact();
        }

        /// <summary>
        /// Lazily slips a clip layer and a dedicated rig between the PSD portrait area and the bust's existing hit/idle
        /// rig (PerformerMotion), so the lunge composes with hurt reactions instead of overwriting them.
        /// </summary>
        private HeroCardLunge1031 HeroLunge1031(CellView cell)
        {
            if (psdRoot == null || cell == null || cell.Side != BattleSide.Player || cell.Portrait == null) return null;
            if (heroLunges1031.TryGetValue(cell, out HeroCardLunge1031 existing) && existing != null) return existing;
            Transform motion = cell.Portrait.transform.parent;
            Transform area = motion != null ? motion.parent : null;
            if (area == null || area.name != "PsdPortraitArea") return null;
            // The bust art is cropped at its sides; while it swells, this clip (sleeve width, open above the
            // sleeve) keeps those crop edges and the neighbouring cards clean. Idle, it is off: nothing changes.
            RectTransform clipRect = kit.NewRect("HeroLungeClip1031", area);
            PanelKit.Stretch(clipRect);
            clipRect.SetSiblingIndex(motion.GetSiblingIndex());
            var clip = clipRect.gameObject.AddComponent<RectMask2D>();
            // Soft sides: hair or horns crossing the clip above the sleeve fade out instead of being cut by a
            // hard vertical line (live portraits use the default UI material, which supports the softness).
            clip.softness = new Vector2Int(10, 0);
            clip.enabled = false;
            RectTransform rig = kit.NewRect("HeroLunge1031", clipRect);
            PanelKit.Stretch(rig);
            // Grow from the chest line: the bust swells up and outwards, its cut-off base stays behind the bars.
            rig.pivot = new Vector2(.5f, .12f);
            rig.anchoredPosition = Vector2.zero;
            motion.SetParent(rig, false);
            var lunge = rig.gameObject.AddComponent<HeroCardLunge1031>();
            lunge.Configure(() => paused, () => closing, () => speed,
                () => cell.Unit != null && cell.Unit.Alive,
                () => ManualUltimateTimingActive || (psdUltimate != null && psdUltimate.IsShowing), clip);
            heroLunges1031[cell] = lunge;
            return lunge;
        }

        /// <summary>Clip = the sleeve's window (inside its rim art), from the portrait base up to well above the frame top.</summary>
        private void FitHeroClip1031(CellView cell, HeroCardLunge1031 lunge)
        {
            var rig = (RectTransform)lunge.transform;
            var clip = (RectTransform)rig.parent;
            var area = (RectTransform)clip.parent;
            Vector2 min = Vector2.zero, max = Vector2.zero;
            if (psdFrames.TryGetValue(cell, out Image frame) && frame != null)
            {
                var corners = new Vector3[4];
                frame.rectTransform.GetWorldCorners(corners);
                Vector2 low = area.InverseTransformPoint(corners[0]), high = area.InverseTransformPoint(corners[2]);
                Rect r = area.rect;
                float rim = -(high.x - low.x) * .24f;
                min = new Vector2(low.x + rim - r.xMin, 0f);
                max = new Vector2(high.x - rim - r.xMax, high.y + HeroLungeHeadroom1031 - r.yMax);
            }
            else max = new Vector2(0f, HeroLungeHeadroom1031);
            clip.offsetMin = min; clip.offsetMax = max;
            // The rig keeps the portrait area's own rectangle, so the resting bust is pixel-identical.
            rig.offsetMin = -min; rig.offsetMax = -max;
        }

        /// <summary>The acting card draws over its neighbours so the bust can break out past the sleeve edge.</summary>
        private void RaiseHeroCard1031(CellView cell)
        {
            Transform root = cell.Root.transform, parent = root.parent;
            if (parent == null) return;
            int top = root.GetSiblingIndex();
            float centre = 0f; int count = 0;
            foreach (CellView other in cells)
            {
                if (other.Side != BattleSide.Player || other.Root == null || other.Root.transform.parent != parent) continue;
                top = Mathf.Max(top, other.Root.transform.GetSiblingIndex());
                centre += other.Rect.anchoredPosition.x; count++;
            }
            root.SetSiblingIndex(top);
            // Lean towards the middle of the team (and the stage above it).
            if (count > 0 && heroLunges1031.TryGetValue(cell, out HeroCardLunge1031 lunge))
                lunge.LeanSign = cell.Rect.anchoredPosition.x <= centre / count ? 1f : -1f;
        }
    }
}
