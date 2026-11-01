using System;
using ChoSiren.Systems.Dice;

namespace ChoSiren.Systems.Tactics
{
    /// <summary>
    /// 2026-11-01b (用户: "骰子加成你看着来吧" — 赵金: 骰子加成大约提高 10% 通过率; Docs/BALANCE-1101.md).
    /// <list type="bullet">
    /// <item>Captain passive tables per hand (index = <see cref="DicePattern"/>). Six dice always hold at least a pair
    /// (一对/两对/三对 ≈ 54% of opening hands), so those low hands give nothing any more; a passive starts at 三条 (顺子 for
    /// the mermaid wall) and every big hand gives less. Read by <see cref="BattleSimulator.PoisonLayers"/>,
    /// <see cref="BattleSimulator.Combo"/>, <see cref="BattleSimulator.Pierce"/>, <see cref="BattleSimulator.ShieldPercent"/>
    /// (the panel shows the same numbers).</item>
    /// <item>魅族 狐舞连斩 follow-up (turn mode): 85% → 50% (11-01b) → 25% of attack (11-01c 无限骰子: 三条+ is now held
    /// almost all fight).</item>
    /// <item>11-01c 无限骰子: passives stop growing at 四条, the hand no longer speeds up ultimate charge in turn battles,
    /// and the 顺子 team heal (4% in turn mode; 6-dice spam rolls straights far more often) fires once a round.</item>
    /// <item>人鱼 深海壁垒 (turn mode): every round, not only when the hand changes — each member's shield is topped up to
    /// 1% of max HP plus the hand's wall (11-01c: 2% → 1%, wall 4/3/5/7% → 3/2/4/5%; with endless rerolls the wall is held
    /// all fight and a mermaid captain led the 1-5 boss by ~15 points). Without it a mermaid captain lost almost every boss (1-5: 0–35% vs 魅族 65%).</item>
    /// </list>
    /// </summary>
    public sealed partial class BattleSimulator
    {
        //                                                       高点 一对 两对 三条 顺子 葫芦 四条 五条 三对 双三条 六连顺 六同
        internal static readonly int[] PoisonLayersByHand1101 = { 0,   0,   0,   2,   0,   2,   3,   4,   0,   2,    0,    4 };
        internal static readonly int[] ComboByHand1101 =        { 0,   0,   0,   1,   0,   1,   2,   2,   0,   1,    0,    2 };
        internal static readonly int[] PierceByHand1101 =       { 0,   0,   0, 150,   0, 200, 250, 400,   0, 200,    0,  400 };
        internal static readonly int[] ShieldByHand1101 =       { 0,   0,   0,  30,  20,  40,  50,  60,   0,  40,   20,   60 };

        /// <summary>人鱼队长每回合的基础护盾（千分比，叠加骰型护盾）。11-01c: 20 → 10 (rerolls are endless, the wall is up all fight).</summary>
        public const int MermaidRoundShieldBasePermille = 10;
        internal static int MermaidRoundShieldBase1101 = MermaidRoundShieldBasePermille;
        /// <summary>魅族队长 魅影追击 (turn mode), permille of attack (realtime keeps 850).</summary>
        public const int CharmFollowPowerTurn = 250;
        internal static int CharmFollowPowerTurn1101 = CharmFollowPowerTurn;

        /// <summary>
        /// Turn mode, start of every round: a living mermaid captain's 深海壁垒 tops each member's shield up to
        /// base + the hand's wall (it used to fire only when the hand changed — once or twice a battle).
        /// </summary>
        private void OnTurnRoundStart1101()
        {
            if (!IsTurnBased || leaderRace != CombatRace.Mermaid || Outcome != BattleOutcome.Ongoing) return;
            BattleUnit leader = FindUnit(leaderId);
            if (leader == null || !leader.Alive) return;
            int permille = MermaidRoundShieldBase1101 + (BattleDice?.Hand != null ? ShieldPercent(BattleDice.Hand.Pattern) : 0);
            if (permille <= 0) return;
            foreach (BattleUnit ally in units)
            {
                if (!ally.Alive || ally.Side != BattleSide.Player) continue;
                int want = (int)((long)ally.MaxHp * permille / 1000);
                if (ally.Shield >= want) continue;
                int amount = want - ally.Shield;
                ally.Shield += amount;
                Emit(BattleEventKind.Shield, leader.Id, ally.Id, "rt-tide-hand", amount, false);
            }
        }

        /// <summary>
        /// 2026-11-01c 无限骰子: captain passives stop growing at this hand (rerolls are endless now, so the top hands are
        /// reachable every fight; the bigger hands still look and sound bigger, they just give no more power).
        /// </summary>
        public const DicePattern PassiveTopPattern = DicePattern.FourKind;
        internal static DicePattern PassiveTopPattern1101 = PassiveTopPattern;
        /// <summary>
        /// Upper bound of the hand's boost to ultimate charge in turn battles (permille; was 1300). 11-01c: 1000 = none —
        /// with endless rerolls a hand-driven charge only shifted when ultimates landed (the 2-10 auto rate swung 58% ↔ 85%
        /// on it alone), so the hand now speaks through attack, passives and the 队长演出.
        /// </summary>
        public const int HandEnergyCapPermille = 1000;
        internal static int HandEnergyCap1101 = HandEnergyCapPermille;
        /// <summary>顺子 / 六连顺 team heal in a turn battle (permille of max HP before the turn support scale), once a round.</summary>
        public const int StraightHealTurnPermille = 10;
        internal static int StraightHealTurnPermille1101 = StraightHealTurnPermille;
        private int straightHealRound1101 = -1;

        private bool TakeStraightHeal1101()
        {
            if (!IsTurnBased) return true;
            if (straightHealRound1101 == TurnRound) return false;
            straightHealRound1101 = TurnRound;
            return true;
        }

        private static int ByHand1101(int[] table, DicePattern p)
        {
            int i = (int)p, top = (int)PassiveTopPattern1101;
            int value = i >= 0 && i < table.Length ? table[i] : 0;
            return top >= 0 && top < table.Length ? Math.Min(value, table[top]) : value;
        }
    }
}
