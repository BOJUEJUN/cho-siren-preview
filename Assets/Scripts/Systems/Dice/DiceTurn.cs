using System;
using System.Collections.Generic;
using ChoSiren.Systems;

namespace ChoSiren.Systems.Dice
{
    /// <summary>Owns one six-die combat turn, including holds, rerolls and the energy reroll.</summary>
    public sealed partial class DiceTurn
    {
        public const int MaxEnergy = 100;
        public const int InitialRerolls = 2;
        /// <summary>
        /// 2026-10-31 (赵金): one battle allows at most three rerolls. Since 2026-11-01c only the legacy realtime battle keeps
        /// this budget; turn battles (rhythm mode) use the endless 骰子豆 (DiceEndless1101: refill per action, store ≤ 3).
        /// </summary>
        public const int MaxRerollsPerBattle = 3;
        /// <summary>2026-10-31 (赵金): one reroll changes at most two dice.</summary>
        public const int MaxDicePerReroll = 2;

        private readonly IRandomSource random;
        private readonly int[] values = new int[DiceRules.DiceCount];
        private readonly bool[] held = new bool[DiceRules.DiceCount];
        private readonly bool[] rolled = new bool[DiceRules.DiceCount];
        private readonly IReadOnlyList<bool> readOnlyRolled;
        private readonly IReadOnlyList<int> readOnlyValues;
        private readonly IReadOnlyList<bool> readOnlyHeld;
        private bool begun;
        private bool freeRerollGranted;
        private int legacyRerolls = InitialRerolls;
        private long energyMicros;
        private const long EnergyScale = 1000000;

        public bool IsBattleSession { get; private set; }
        public bool SelectiveReroll { get; private set; }
        public int BattleRerollLimit { get; private set; }
        public int UsedRerolls { get; private set; }
        /// <summary>充能条每充满一次自动发放 1 次重投，最多发满 BattleRerollLimit 次。</summary>
        public int EarnedRerolls { get; private set; }
        public int FreeRerolls { get; private set; }
        private int freeRerollsUsed;
        /// <summary>Every reroll operation of this battle, paid from the charge or from a free grant.</summary>
        public int RerollsUsedInBattle => UsedRerolls + freeRerollsUsed;
        /// <summary>Rerolls the player can spend right now, never past <see cref="MaxRerollsPerBattle"/>.</summary>
        public int AvailableRerolls => !IsBattleSession ? RerollsRemaining
            : RhythmMode ? StoredRerolls + FreeRerolls
            : Math.Max(0, Math.Min(RerollsRemaining + FreeRerolls, MaxRerollsPerBattle - RerollsUsedInBattle));
        public int Revision { get; private set; }
        public const int MaxBattleBonusPermille = 1000;
        public int AccumulatedBonusPermille { get; private set; }
        public int LastBonusGainPermille { get; private set; }
        public int DamageMultiplierPermille => IsBattleSession ? 1000 + AccumulatedBonusPermille : Hand.MultiplierPermille;

        // ---------------------------------------------------------------- 骰子节奏 (2026-10-21)
        // Turn battles: rerolls are earned whenever the charge bar fills, the attack bonus follows the CURRENT hand
        // instead of stacking forever, and "heat" grows while each reroll keeps or improves the hand.
        // 2026-10-31 (赵金 "重投太多次 / 我方过强"): the battle total is back — at most BattleRerollLimit (≤3) — the
        // hand no longer re-deals itself each round, and the bonus cap/heat step are lower (+40% / +2.5% a step).
        /// <summary>2026-11-01c: the whole dice attack bonus of a turn battle (hand + heat) never passes this — 骰子加成上限.</summary>
        public const int RhythmMaxBonusPermille = EndlessBonusCapPermille;
        public const int RhythmHeatMax = 3;
        /// <summary>2026-11-01b (赵金: 骰子加成约 +10% 通过率): 2.5% → 1% per kept-or-better reroll.</summary>
        public const int RhythmHeatStepPermille = 10;
        /// <summary>
        /// 2026-11-01b: 一对 / 两对 (≈ half of all six-dice hands) add no attack; the bonus starts above 两对 (1.2×) and is a
        /// third of the rest: 三对 +1.7%, 三条 +3.3%, 顺子 +5%, 葫芦 +8.3%, 四条 +13.3%, 五条 +18.3%, 六同 +20%.
        /// </summary>
        public const int RhythmBonusFloorPermille = 1200;
        public bool RhythmMode { get; private set; }
        public int Heat { get; private set; }
        public int LastHeatChange { get; private set; }
        private int lastHandScore;
        public int BonusCapPermille => RhythmMode ? EndlessBonusCap1101 : MaxBattleBonusPermille;
        /// <summary>Hand bonus in rhythm mode: a third of the hand's edge above <see cref="RhythmBonusFloorPermille"/>
        /// (2026-11-01b; 10-31 counted from 1.0×: 一对 +3.3%, 两对 +6.7%, 顺子 +11.7%, 葫芦 +15%).</summary>
        public static int RhythmHandBonusPermille(int handScorePermille) => Math.Max(0, (handScorePermille - RhythmBonusFloorPermille) / 3);

        public void EnableRhythm()
        {
            if (!IsBattleSession) throw new InvalidOperationException("需要局内骰子状态");
            if (RhythmMode) return;
            RhythmMode = true;
            Heat = 0;
            if (begun && Hand != null)
            {
                lastHandScore = Hand.MultiplierPermille;
                AccumulatedBonusPermille = Math.Min(BonusCapPermille, RhythmHandBonusPermille(lastHandScore));
                LastBonusGainPermille = AccumulatedBonusPermille;
            }
            StartEndless1101();
        }

        /// <summary>
        /// A new wave pays out the next reroll of this battle's budget at once (it never adds one beyond it).
        /// 2026-10-31: the hand itself is never re-dealt — 赵金: dice change only when somebody rerolls them.
        /// </summary>
        public void GrantRhythmReroll()
        {
            if (RhythmMode) { GrantStoredReroll1101(); return; }
            if (EarnedRerolls >= BattleRerollLimit) return;
            EarnedRerolls++;
            AddEnergyMicros(0);
        }

        public static DiceTurn ForBattle(IRandomSource random, int rerollLimit, bool selectiveReroll)
        {
            if (rerollLimit < 1 || rerollLimit > 5) throw new ArgumentOutOfRangeException(nameof(rerollLimit));
            return new DiceTurn(random)
            {
                IsBattleSession = true, BattleRerollLimit = Math.Min(rerollLimit, MaxRerollsPerBattle),
                SelectiveReroll = selectiveReroll
            };
        }

        public DiceTurn(IRandomSource random, int startingEnergy = 0)
        {
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            Energy = Math.Max(0, Math.Min(MaxEnergy, startingEnergy));
            readOnlyValues = Array.AsReadOnly(values);
            readOnlyHeld = Array.AsReadOnly(held);
            readOnlyRolled = Array.AsReadOnly(rolled);
        }

        public IReadOnlyList<int> Values => readOnlyValues;
        public IReadOnlyList<bool> Held => readOnlyHeld;
        /// <summary>Dice included in the latest revision, independent of post-roll hold reset.</summary>
        public IReadOnlyList<bool> Rolled => readOnlyRolled;
        public int RerollsRemaining => IsBattleSession ? RhythmMode ? StoredRerolls : EarnedRerolls - UsedRerolls : legacyRerolls;
        public int Energy { get; private set; }
        /// <summary>Dice explicitly ticked for the next selective reroll (unheld dice).</summary>
        public int SelectedForRerollCount
        {
            get
            {
                int count = 0;
                for (int index = 0; index < held.Length; index++) if (!held[index]) count++;
                return count;
            }
        }
        /// <summary>
        /// Battle sessions spend one earned reroll charge per operation; charges are granted
        /// progressively as the 重投充能 meter fills (first chapter grants up to three). The
        /// legacy turn session still requires a full energy charge.
        /// </summary>
        public bool CanReroll => IsBattleSession
            ? begun && AvailableRerolls > 0
            : Energy >= MaxEnergy;
        /// <summary>Backward-compatible alias for callers/tests written before the quota rename.</summary>
        public bool CanEnergyReroll => CanReroll;
        public DiceHand Hand { get; private set; }

        public void Begin()
        {
            // A battle owns one persistent hand and budget. A new actor must not refill it.
            if (IsBattleSession && begun) return;
            // Battle dice start fully kept: the player ticks the dice that should be rerolled, so a
            // stray tap can never reroll the whole hand. Legacy turns keep the old all-unheld rule.
            for (int index = 0; index < held.Length; index++) held[index] = IsBattleSession;
            legacyRerolls = InitialRerolls;
            RollAll();
            begun = true;
            RefreshHand();
        }

        /// <summary>
        /// Toggles a die and returns its new held state. In a battle a third die cannot be ticked for the same
        /// reroll (<see cref="MaxDicePerReroll"/>): the tap is refused and the die stays kept.
        /// </summary>
        public bool ToggleHold(int index)
        {
            if (!begun) throw new InvalidOperationException("请先开始骰子回合");
            if (index < 0 || index >= held.Length) throw new ArgumentOutOfRangeException(nameof(index));
            if (!CanToggleHold(index)) return held[index];
            held[index] = !held[index];
            return held[index];
        }

        /// <summary>False only when ticking this kept die would select more than two dice.</summary>
        public bool CanToggleHold(int index) =>
            index >= 0 && index < held.Length &&
            (!IsBattleSession || !held[index] || SelectedForRerollCount < MaxDicePerReroll);

        public bool RerollUnheld(out string error)
        {
            if (IsBattleSession) return RerollBattle(true, out error);
            if (!begun)
            {
                error = "请先开始骰子回合";
                return false;
            }

            if (RerollsRemaining <= 0)
            {
                error = "本回合的重投次数已经用完";
                return false;
            }

            bool hasUnheld = false;
            for (int index = 0; index < held.Length; index++) hasUnheld |= !held[index];
            if (!hasUnheld)
            {
                error = "至少取消保留一颗骰子才能重投";
                return false;
            }

            for (int index = 0; index < values.Length; index++)
                { rolled[index] = !held[index]; if (rolled[index]) values[index] = RollDie(); }
            legacyRerolls--;
            RefreshHand();
            error = string.Empty;
            return true;
        }

        public bool EnergyRerollAll(out string error)
        {
            if (IsBattleSession)
            {
                return RerollBattle(false, out error, true);
            }
            if (!begun)
            {
                error = "请先开始骰子回合";
                return false;
            }

            if (Energy < MaxEnergy)
            {
                error = $"能量达到 {MaxEnergy} 才能全部重投";
                return false;
            }

            Energy = 0;
            for (int index = 0; index < held.Length; index++) held[index] = false;
            RollAll();
            RefreshHand();
            error = string.Empty;
            return true;
        }

        /// <summary>
        /// 全部重投 (2026-10-31 用户): with no die picked, one reroll operation rolls all six dice. Picking dice
        /// instead rerolls just those (at most <see cref="MaxDicePerReroll"/>, see <see cref="Reroll"/>).
        /// </summary>
        public bool RerollAll(out string error) =>
            IsBattleSession ? RerollBattle(false, out error, true) : EnergyRerollAll(out error);

        /// <summary>
        /// Auto-play reroll: one operation on at most two loose dice — those whose reroll helps the hand most
        /// (<see cref="DiceAutoPlanner1031"/>); the dice of the current pattern are kept.
        /// </summary>
        public bool RerollSuggested(out string error) =>
            IsBattleSession ? RerollBattle(false, out error, false) : EnergyRerollAll(out error);

        /// <summary>Legacy hook kept for old auto-planning callers; charges now grant automatically.</summary>
        public void SpendEnergyCharge()
        {
            if (!IsBattleSession) return;
            energyMicros = 0;
            Energy = 0;
        }

        public void GainEnergy(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (IsBattleSession)
            {
                AddEnergyMicros((long)amount * EnergyScale);
                return;
            }
            Energy = (int)Math.Min(MaxEnergy, (long)Energy + amount);
        }

        /// <summary>Only actual HP/shield loss counts; callers must remove overkill first.</summary>
        public void RecordDamage(int appliedDamage, bool killed, int gainPermille = 1000)
        {
            if (!IsBattleSession) throw new InvalidOperationException("需要局内骰子状态");
            if (appliedDamage < 0 || gainPermille < 0 || gainPermille > 2000)
                throw new ArgumentOutOfRangeException(nameof(appliedDamage));
            if (RhythmMode) return; // 2026-11-01c: 骰子豆 refill per own action only (DiceEndless1101), not per damage
            // Keep fractional energy across small hits: 125 one-point hits also earn one energy.
            long earned = (long)appliedDamage * 8000 + (killed ? 25 * EnergyScale : 0);
            AddEnergyMicros(earned * gainPermille / 1000);
        }

        public void GrantFreeReroll()
        {
            // A rescue reroll may arrive after the normal quota is spent, but it is one allowance
            // for this battle object. Consuming it must not let a later same-race successor grant
            // it again and turn a repeated-race formation into an unlimited reroll chain.
            if (!IsBattleSession || freeRerollGranted) return;
            freeRerollGranted = true;
            FreeRerolls = 1;
        }

        /// <summary>
        /// 2026-11-01 balance audit only (Balance1101Tests "无骰子加成" control group); the game never calls it. The hand
        /// reads 高点 ×1.0, the battle bonus is zero and no reroll (earned, wave or rescue) is ever granted, so every
        /// captain passive and dice multiplier stays off. Call right after the battle is created, before time passes.
        /// </summary>
        public void NeutralizeForBalanceAudit1101()
        {
            if (!IsBattleSession) throw new InvalidOperationException("需要局内骰子状态");
            BattleRerollLimit = 0;
            neutralized1101 = true;
            StoredRerolls = 0;
            freeRerollGranted = true;
            FreeRerolls = 0;
            energyMicros = 0;
            Energy = 0;
            Heat = 0;
            AccumulatedBonusPermille = 0;
            LastBonusGainPermille = 0;
            Hand = new DiceHand(values, DicePattern.HighPoint, "高点", 1000);
        }

        /// <summary>Changes command capability without rerolling or refilling any battle budget.</summary>
        public void SetBattleSelectiveReroll(bool enabled)
        {
            if (!IsBattleSession) throw new InvalidOperationException("需要局内骰子状态");
            SelectiveReroll = enabled;
            if (!enabled) for (int i = 0; i < held.Length; i++) held[i] = true;
        }

        private void AddEnergyMicros(long amount)
        {
            if (RhythmMode) { AddEndlessCharge1101(amount); return; }
            // 充满一条发一次重投；本场额度发满后充能条归零不再积累（一共只给 RerollLimit 次，最多 3 次）。
            // Turn battles (rhythm) follow the same budget since 2026-10-31.
            if (EarnedRerolls >= BattleRerollLimit)
            {
                energyMicros = 0;
                Energy = 0;
                return;
            }
            energyMicros += amount;
            while (EarnedRerolls < BattleRerollLimit && energyMicros >= MaxEnergy * EnergyScale)
            {
                energyMicros -= MaxEnergy * EnergyScale;
                EarnedRerolls++;
            }
            if (EarnedRerolls >= BattleRerollLimit) energyMicros = 0;
            Energy = (int)(energyMicros / EnergyScale);
        }

        private bool RerollBattle(bool selective, out string error, bool all = false)
        {
            if (!CanReroll)
            {
                error = !begun ? "请先开始骰子回合"
                    : RhythmMode ? "骰子豆充能中"
                    : RerollsUsedInBattle >= MaxRerollsPerBattle || EarnedRerolls >= BattleRerollLimit && FreeRerolls <= 0
                        ? $"本场重投次数已用完（每场最多 {BattleRerollLimit} 次）"
                        : "暂无可重投次数，充能满后会自动发放";
                return false;
            }
            bool[] pick;
            if (selective)
            {
                int unheld = SelectedForRerollCount;
                if (!SelectiveReroll)
                {
                    error = "本场不支持自选重投";
                    return false;
                }
                if (unheld < 1)
                {
                    error = "请先点选要重投的骰子";
                    return false;
                }
                if (unheld > MaxDicePerReroll)
                {
                    error = $"每次最多重投 {MaxDicePerReroll} 颗骰子";
                    return false;
                }
                pick = new bool[held.Length];
                for (int i = 0; i < held.Length; i++) pick[i] = !held[i];
            }
            else if (all) { pick = new bool[values.Length]; for (int i = 0; i < pick.Length; i++) pick[i] = true; }
            else pick = DiceAutoPlanner1031.ChooseRerollMask(values, MaxDicePerReroll, true);
            // One operation = one cost. A rescue free reroll is spent before the normal quota, and
            // the same operation never consumes both.
            if (FreeRerolls > 0) { FreeRerolls--; freeRerollsUsed++; }
            else { UsedRerolls++; if (RhythmMode) StoredRerolls--; }
            heatCounts = true;
            for (int i = 0; i < values.Length; i++)
            {
                rolled[i] = pick[i];
                if (rolled[i]) values[i] = RollDie();
            }
            // After a reroll the new faces are kept by default; the player ticks the next selection.
            for (int i = 0; i < held.Length; i++) held[i] = true;
            RefreshHand();
            error = string.Empty;
            return true;
        }

        private int RollDie() => random.Next(6) + 1;

        private void RollAll()
        {
            for (int index = 0; index < values.Length; index++) { rolled[index] = true; values[index] = RollDie(); }
        }

        private bool heatCounts;

        private void RefreshHand()
        {
            Hand = DiceRules.Evaluate(values);
            if (IsBattleSession && RhythmMode)
            {
                int score = Hand.MultiplierPermille;
                int heatBefore = Heat;
                if (heatCounts) Heat = score >= lastHandScore ? Math.Min(RhythmHeatMax, Heat + 1) : 0;
                heatCounts = false;
                LastHeatChange = Heat - heatBefore;
                lastHandScore = score;
                int before = AccumulatedBonusPermille;
                AccumulatedBonusPermille = Math.Min(BonusCapPermille,
                    RhythmHandBonusPermille(score) + Heat * RhythmHeatStepPermille);
                LastBonusGainPermille = AccumulatedBonusPermille - before;
            }
            else if (IsBattleSession)
            {
                // A hand contributes a bounded additive encore bonus, never a compound multiplier.
                // A reroll always progresses by at least 5%; opening high point is the neutral baseline.
                int gain = (Hand.MultiplierPermille - 1000) / 4;
                if (Revision > 0) gain = Math.Max(50, gain);
                LastBonusGainPermille = Math.Min(gain, MaxBattleBonusPermille - AccumulatedBonusPermille);
                AccumulatedBonusPermille += LastBonusGainPermille;
            }
            Revision++;
        }
    }
}
