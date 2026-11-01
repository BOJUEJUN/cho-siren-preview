using ChoSiren.Systems.Dice;
using ChoSiren.Systems.Tactics;
using ChoSiren.UI;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.Panels
{
    /// <summary>
    /// 2026-11-01c 无限骰子 (DiceEndless1101). The reroll bar shows its 骰子豆 as small dice and the next one as a ring;
    /// the long bar under the dice title is the 骰子加成 meter — it fills towards the cap and reads MAX at the top.
    /// Replaces the old "重投充能 N% · 本场剩N次 / 本场重投用完" words.
    /// </summary>
    public sealed partial class TacticsBattlePanel
    {
        private DiceEndlessHud1101 diceEndlessHud1101;

        /// <summary>For tests / captures.</summary>
        public DiceEndlessHud1101 DiceEndlessHud => diceEndlessHud1101;

        private void RefreshDiceEndless1101(Text rerollCount)
        {
            if (diceTurn == null || !diceTurn.IsBattleSession) return;
            bool ongoing = battle != null && battle.Outcome == BattleOutcome.Ongoing && !closing;
            bool inputAllowed = awaitingInput && !paused && !awaitingDiceLanding;
            if (psdRerollSlide != null) psdRerollSlide.SetEndless1101(ongoing, diceTurn.AvailableRerolls > 0,
                diceTurn.CanReroll && inputAllowed);
            if (rerollCount != null) rerollCount.text = $"重投×{diceTurn.AvailableRerolls}";
            // 骰子加成 meter: the share of the cap reached (the PSD's own reroll-fill art); full = capped + "MAX".
            if (diceEnergyFill != null)
            {
                float share = diceTurn.BonusCapPermille <= 0 ? 0f
                    : Mathf.Clamp01(diceTurn.AccumulatedBonusPermille / (float)diceTurn.BonusCapPermille);
                diceEnergyFill.fillAmount = share;
            }
            if (psdChargeLabel != null) psdChargeLabel.text = diceTurn.BonusCapped ? "MAX" : string.Empty;
            // Sound instead of words: a soft chime when a 骰子豆 arrives, a brighter one when the bonus first tops out.
            int pips = diceTurn.AvailableRerolls;
            if (ongoing && lastPips1101 >= 0 && pips > lastPips1101) Sfx("orb-chime", .35f);
            if (ongoing && diceTurn.BonusCapped && !lastCapped1101) Sfx("orb-chime", .7f);
            lastPips1101 = pips;
            lastCapped1101 = diceTurn.BonusCapped;
        }

        private int lastPips1101 = -1;
        private bool lastCapped1101;

        private void BuildDiceEndless1101(Text rerollCount)
        {
            if (diceEndlessHud1101 != null || psdReroll == null || rerollCount == null) return;
            var bar = rerollCount.transform.parent as RectTransform;
            if (bar == null) return;
            // Pips sit in the bar's dark band (the old ×N slot), slanted with it.
            var pipArea = kit.NewRect("DiceEndlessPips1101", bar);
            pipArea.anchorMin = rerollCount.rectTransform.anchorMin; pipArea.anchorMax = rerollCount.rectTransform.anchorMax;
            pipArea.pivot = rerollCount.rectTransform.pivot;
            pipArea.anchoredPosition = rerollCount.rectTransform.anchoredPosition;
            pipArea.sizeDelta = rerollCount.rectTransform.sizeDelta;
            pipArea.localEulerAngles = rerollCount.rectTransform.localEulerAngles;
            pipArea.SetAsLastSibling();
            // The ring hugs the bar's crystal die (its PSD part), falling back to the right end of the bar.
            RectTransform ringArea = null;
            Transform dieArt = bar.Find("PsdReroll-reroll-dice");
            if (dieArt is RectTransform die)
            {
                ringArea = kit.NewRect("DiceEndlessRing1101", bar);
                ringArea.anchorMin = die.anchorMin; ringArea.anchorMax = die.anchorMax;
                ringArea.pivot = new Vector2(.5f, .5f);
                ringArea.anchoredPosition = die.anchoredPosition + Vector2.Scale(new Vector2(.5f, .5f) - die.pivot, die.sizeDelta);
                float side = Mathf.Max(die.sizeDelta.x, die.sizeDelta.y) * 1.9f; // outside the art's own cyan arrows
                ringArea.sizeDelta = new Vector2(side, side);
                ringArea.SetSiblingIndex(die.GetSiblingIndex());
            }
            Sprite face = userDiceFaceSprites.Length >= 6 ? userDiceFaceSprites[5] : null;
            diceEndlessHud1101 = DiceEndlessHud1101.Build(pipArea, ringArea, face, kit.RadialSprite(),
                diceTurn.StoredRerollCapacity, () => paused || closing);
        }
    }
}
