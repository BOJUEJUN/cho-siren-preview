using System.Collections.Generic;
using ChoSiren.Systems.Tactics;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.Panels
{
    public sealed partial class TacticsBattlePanel
    {
        private readonly Dictionary<int, float[]> skillCueUntil1019 = new Dictionary<int, float[]>();
        private readonly Dictionary<int, int> passiveCueRevision1019 = new Dictionary<int, int>();
        // 2026-10-26: one cue at a time over the record's top-right; long enough to read, with a pop on arrival.
        private const float SkillCueSeconds1019 = 1.6f;

        // These PSD symbols describe real actions; they are not a permanent card decoration.
        private void ObservePsdSkillCue1019(BattleEvent entry, BattleUnit actor)
        {
            if (psdRoot == null || actor == null || actor.Side != BattleSide.Player) return;
            if (entry.Kind == BattleEventKind.ActionStarted)
            {
                if (entry.SkillId == battle.ActiveSkillId(actor, true)) PulsePsdSkill1019(actor, 0);
                else if (entry.SkillId == battle.ActiveSkillId(actor, false)) PulsePsdSkill1019(actor, 1);
                return;
            }
            if (entry.Amount <= 0 || battle.BattleDice == null) return;
            var leader = battle.FindUnit(battle.CurrentLeaderId);
            if (leader == null || !leader.Alive) return;
            bool damage = entry.Kind == BattleEventKind.Damage;
            bool actualPassive = battle.CurrentLeaderRace == CombatRace.Charm && damage && entry.SkillId == "rt-charm-follow"
                || battle.CurrentLeaderRace == CombatRace.Demon && damage && entry.SkillId == "rt-poison"
                || battle.CurrentLeaderRace == CombatRace.Mermaid && actor.Id == leader.Id && entry.SkillId == "rt-tide-hand" &&
                    (entry.Kind == BattleEventKind.Shield || entry.Kind == BattleEventKind.Heal)
                || battle.CurrentLeaderRace == CombatRace.BloodElf && actor.Id == leader.Id && damage && entry.SkillId != "rt-poison" && battle.CaptainPiercePermille > 0;
            if (!actualPassive) return;
            int revision = battle.BattleDice.Revision;
            if (passiveCueRevision1019.TryGetValue(leader.Id, out int shown) && shown == revision) return;
            passiveCueRevision1019[leader.Id] = revision;
            PulsePsdSkill1019(leader, 2);
        }

        private void PulsePsdSkill1019(BattleUnit actor, int row)
        {
            if (!skillCueUntil1019.TryGetValue(actor.Id, out float[] until))
                skillCueUntil1019[actor.Id] = until = new float[3];
            until[row] = presentationClock + SkillCueSeconds1019;
            var cell = FindCell(actor);
            if (cell != null) RefreshPsdCell(cell);
        }

        /// <summary>The row whose cue started last (and is still showing), or -1.</summary>
        private int LatestPsdSkillCue1026(BattleUnit unit)
        {
            if (unit == null || !unit.Alive || !skillCueUntil1019.TryGetValue(unit.Id, out float[] until)) return -1;
            int latest = -1;
            for (int row = 0; row < until.Length; row++)
                if (until[row] > presentationClock && (latest < 0 || until[row] > until[latest])) latest = row;
            return latest;
        }

        private void ApplyPsdSkillCue1026(CellView cell, Image[] column)
        {
            int latest = LatestPsdSkillCue1026(cell.Unit);
            float alpha = 0f, scale = 1f;
            if (latest >= 0)
            {
                float left = skillCueUntil1019[cell.Unit.Id][latest] - presentationClock;
                float age = SkillCueSeconds1019 - left;
                alpha = Mathf.Clamp01(left / .3f) * Mathf.Clamp01(age / .08f);
                // Pops in a size larger, settles, and the ultimate keeps a gentle pulse.
                float pop = 1f - Mathf.Clamp01(age / .22f);
                scale = BattlePresentationOptions.ReduceMotion ? 1f
                    : 1f + .35f * pop * pop + (latest == 0 ? .05f * Mathf.Sin(Time.unscaledTime * 6f) : 0f);
            }
            for (int row = 0; row < column.Length; row++)
            {
                bool visible = row == latest && alpha > 0f;
                if (column[row].gameObject.activeSelf != visible) column[row].gameObject.SetActive(visible);
                if (!visible) continue;
                column[row].color = new Color(1, 1, 1, alpha);
                column[row].transform.localScale = Vector3.one * scale;
            }
            if (psdSkillCueBackings.TryGetValue(cell, out Image backing) && backing != null)
                backing.color = new Color(0f, 0f, 0f, .62f * alpha);
        }

        private void RefreshPsdSkillCues1019()
        {
            foreach (var pair in psdSkillColumns)
                if (pair.Key.Unit != null) ApplyPsdSkillCue1026(pair.Key, pair.Value);
        }
    }
}
