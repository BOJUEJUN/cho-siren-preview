using System.Collections;
using ChoSiren.Systems.Dice;
using ChoSiren.Systems.Tactics;
using UnityEngine;

namespace ChoSiren.Panels
{
    /// <summary>
    /// 2026-10-31 赵金 dice feedback: 手动 never rerolls on its own; 自动 rerolls like a person would — only a weak
    /// hand, at most two loose dice (shown lifted first), with a readable gap between two auto rerolls.
    /// </summary>
    public sealed partial class TacticsBattlePanel
    {
        /// <summary>Real seconds between the landing of one auto reroll and the start of the next.</summary>
        public const float AutoRerollGapSeconds1031 = 2.75f;
        /// <summary>Real seconds the chosen dice stay lifted before 自动 rolls them.</summary>
        public const float AutoRerollPreviewSeconds1031 = .7f;

        private float lastAutoRerollLandedAt1031 = -100f;
        private int autoRerollCount1031;

        /// <summary>Auto rerolls this panel has played (tests and QA).</summary>
        public int AutoRerollCount1031 => autoRerollCount1031;

        /// <summary>Player taps a die: selecting a third one is refused with a clear note.</summary>
        private bool TrySelectDie1031(int index)
        {
            if (diceTurn == null || !diceTurn.IsBattleSession || diceTurn.CanToggleHold(index)) return true;
            Notify($"最多选 {DiceTurn.MaxDicePerReroll} 颗，先点掉一颗");
            return false;
        }

        // 2026-10-31 (用户): the heart with nothing selected rerolls all six dice (EnergyRerollDice → RerollAll).

        private IEnumerator AutoReroll1031()
        {
            if (!autoMode || closing || diceTurn == null || battle.Outcome != BattleOutcome.Ongoing) yield break;
            // The player is choosing dice themselves: 自动 does not override a selection in progress.
            if (diceTurn.SelectedForRerollCount > 0) yield break;
            if (Time.unscaledTime - lastAutoRerollLandedAt1031 < AutoRerollGapSeconds1031) yield break;
            if (paused || awaitingDiceLanding || dicePresentations.Exists(item => item.Busy)) yield break;
            if (!DiceAutoPlanner1031.ShouldAutoReroll(diceTurn)) yield break;
            bool[] pick = DiceAutoPlanner1031.ChooseRerollMask(diceTurn.Values, DiceTurn.MaxDicePerReroll, false);
            for (int i = 0; i < pick.Length; i++)
                if (pick[i] && diceTurn.Held[i]) diceTurn.ToggleHold(i);
            RefreshDiceUi();
            Notify($"自动重投 {DiceAutoPlanner1031.Count(pick)} 颗");
            // Let the lifted dice read before they roll; pausing holds the preview.
            float shown = 0f;
            while (!closing && shown < AutoRerollPreviewSeconds1031)
            {
                if (!paused) shown += Time.unscaledDeltaTime;
                yield return null;
            }
            // Switched to 手动 (or the fight ended) during the preview: put the dice back, roll nothing.
            if (closing || !autoMode || battle.Outcome != BattleOutcome.Ongoing)
            {
                RestoreHolds1031();
                yield break;
            }
            while (!closing && paused) yield return null;
            int revision = diceTurn.Revision;
            bool wasAwaiting = awaitingInput;
            awaitingInput = true;
            RerollDice();
            awaitingInput = wasAwaiting;
            if (diceTurn.Revision == revision) { RestoreHolds1031(); yield break; }
            autoRerollCount1031++;
            while (!closing && (awaitingDiceLanding || dicePresentations.Exists(item => item.Busy))) yield return null;
            lastAutoRerollLandedAt1031 = Time.unscaledTime;
            yield return WaitBattleDelay(.3f);
        }

        private void RestoreHolds1031()
        {
            if (diceTurn == null) return;
            for (int i = 0; i < diceTurn.Held.Count; i++) if (!diceTurn.Held[i]) diceTurn.ToggleHold(i);
            RefreshDiceUi();
        }
    }
}
