using System;
using ChoSiren.Systems.Dice;

namespace ChoSiren.Systems.Tactics
{
    /// <summary>
    /// 骰子节奏 (owner 2026-10-21: "骰子前面一小半就完了"). Turn battles keep the dice alive all fight:
    /// rerolls from the charge bar (at most three a battle since 2026-10-31, see <see cref="DiceTurn.EnableRhythm"/>),
    /// a new wave pays out the next one at once, and a per-wave hand target — reaching it triggers a
    /// 队长演出: the captain strikes once more and gains ultimate energy.
    /// </summary>
    public sealed partial class BattleSimulator
    {
        public const string DiceTargetSkillId = "rt-dice-target";
        public const string DiceEncoreSkillId = "rt-dice-encore";
        /// <summary>
        /// 2026-11-01b (骰子加成约 +10%): 150% → 90% hit and 30 → 15 ultimate energy. 2026-11-01c 无限骰子: every wave's target
        /// is now reached (rerolls never run out), so the 队长演出 is a flourish — 40% hit and 5 energy.
        /// </summary>
        public const int DiceEncorePower = 400;
        public const int DiceEncoreEnergy = 5;
        internal static int DiceEncorePower1101 = DiceEncorePower;
        internal static int DiceEncoreEnergy1101 = DiceEncoreEnergy;

        private static readonly (int Score, string Label)[] DiceTargets =
        {
            (1250, "两对以上"), (1400, "三条以上"), (1450, "顺子以上"), (1600, "葫芦以上"),
        };

        public int DiceTargetWave { get; private set; }
        public int DiceTargetScore { get; private set; }
        public string DiceTargetLabel { get; private set; } = string.Empty;
        public bool DiceTargetDone { get; private set; }
        public bool HasDiceTarget => IsTurnBased && BattleDice != null && BattleDice.RhythmMode && DiceTargetScore > 0;

        private void EnableDiceRhythm()
        {
            if (BattleDice == null) return;
            BattleDice.EnableRhythm();
            StartDiceTarget(Math.Max(1, CurrentWave));
        }

        /// <summary>Called when a new wave of enemies arrives in a turn battle.</summary>
        private void OnDiceWave(int wave)
        {
            if (!IsTurnBased || BattleDice == null || !BattleDice.RhythmMode || wave <= DiceTargetWave) return;
            BattleDice.GrantRhythmReroll();
            StartDiceTarget(wave);
        }

        private void StartDiceTarget(int wave)
        {
            DiceTargetWave = wave;
            // Later waves ask for stronger hands: wave 1 两对/三条, wave 2 三条/顺子, wave 3+ 顺子/葫芦.
            int low = Math.Min(DiceTargets.Length - 2, Math.Max(0, wave - 1));
            var pick = DiceTargets[low + random.Next(2)];
            DiceTargetScore = pick.Score;
            DiceTargetLabel = pick.Label;
            DiceTargetDone = false;
        }

        private void CheckDiceTarget()
        {
            if (!HasDiceTarget || DiceTargetDone || Outcome != BattleOutcome.Ongoing) return;
            if (BattleDice.Hand == null || BattleDice.Hand.MultiplierPermille < DiceTargetScore) return;
            DiceTargetDone = true;
            BattleUnit leader = FindUnit(leaderId);
            if (leader == null || !leader.Alive) return;
            Emit(BattleEventKind.Buff, leader.Id, leader.Id, DiceTargetSkillId, DiceTargetWave, false);
            BattleUnit target = SelectEnemy(leader);
            if (target != null) Deal(leader, target, DiceEncoreSkillId, DiceEncorePower1101);
            // Paid at the start of the next turn, so an ultimate cast in this same action still
            // leaves the bar empty (TimedUltimateInAutoModeSpendsTheBar).
            pendingEncoreEnergyUnit = leader.Id;
        }

        private int pendingEncoreEnergyUnit = -1;

        private void PayPendingEncoreEnergy()
        {
            if (pendingEncoreEnergyUnit < 0) return;
            BattleUnit unit = FindUnit(pendingEncoreEnergyUnit);
            pendingEncoreEnergyUnit = -1;
            GainUltimateEnergy(unit, DiceEncoreEnergy1101);
        }
    }
}
