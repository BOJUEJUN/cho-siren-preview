using System;
using System.Collections.Generic;

namespace ChoSiren.Systems.Tactics
{
    /// <summary>What a status badge over a unit shows (2026-10-26, 赵金: "这些图标必须挂在角色身上…减了多少、多少秒").</summary>
    public enum UnitStatusKind { Sealed, Stunned, Poison, ArmorBreak, AntiHeal, Reduction, AttackBuff, Shield, TideSuppressed }

    public readonly struct UnitStatus
    {
        public readonly UnitStatusKind Kind;
        /// <summary>Short value, e.g. "-20%", "×3", "1200".</summary>
        public readonly string Value;
        /// <summary>Rounds left in turn mode (0 = until used up / permanent while the source lasts).</summary>
        public readonly int Rounds;
        public UnitStatus(UnitStatusKind kind, string value, int rounds) { Kind = kind; Value = value; Rounds = rounds; }
    }

    public sealed partial class BattleSimulator
    {
        /// <summary>魅语迷心 seals the front row's skills for this many rounds in turn mode.</summary>
        public const int CharmSealRounds = 2;
        /// <summary>深海佑域: shield and damage reduction last this many rounds in turn mode.</summary>
        public const int MermaidWardRounds = 2;
        /// <summary>潮汐压制 (mermaid captain passive): every enemy deals this much less damage.</summary>
        public const int TideSuppressionPermille = 200;

        /// <summary>Removed: the mermaid captain now has a single dice passive (深海壁垒).</summary>
        public bool TideSuppressionActive => false; // 2026-10-29: 人鱼队长只保留深海壁垒，潮汐压制已移除

        /// <summary>
        /// Turn mode: logical time for <paramref name="rounds"/> whole rounds (every living unit's action spends one slice),
        /// so a "2 round" effect really outlives the target's next turns instead of fading after two actions.
        /// </summary>
        public int RoundsToMilliseconds(int rounds)
        {
            int living = 0;
            foreach (BattleUnit unit in units) if (unit.Alive) living++;
            return Math.Max(1, rounds) * Math.Max(1, living) * TurnSliceMilliseconds;
        }

        /// <summary>Rounds (turn mode) or whole seconds (realtime) until <paramref name="untilMilliseconds"/>.</summary>
        public int RoundsRemaining(int untilMilliseconds)
        {
            int left = untilMilliseconds - ElapsedMilliseconds;
            if (left <= 0) return 0;
            int round = IsTurnBased ? RoundsToMilliseconds(1) : 1000;
            return (left + round - 1) / round;
        }

        /// <summary>魅语迷心 seal (skills locked) is on this unit.</summary>
        public bool IsSealed(BattleUnit unit) =>
            IsRealtime && unit != null && unit.Alive && performerClocks.TryGetValue(unit.Id, out PerformerClock c) &&
            c.SilencedUntil > ElapsedMilliseconds;

        /// <summary>深海佑域 ward (timed shield or damage reduction) still covers this member.</summary>
        public bool HasMermaidWard(BattleUnit unit)
        {
            if (!IsRealtime || unit == null || !unit.Alive || !performerClocks.TryGetValue(unit.Id, out PerformerClock c)) return false;
            int now = ElapsedMilliseconds;
            return c.TimedShield > 0 && c.TimedShieldUntil > now || c.ReductionUntil > now;
        }

        /// <summary>Statuses to hang over a unit, most important first.</summary>
        public List<UnitStatus> StatusesOf(BattleUnit unit)
        {
            var list = new List<UnitStatus>();
            if (!IsRealtime || unit == null || !unit.Alive || !performerClocks.TryGetValue(unit.Id, out PerformerClock c)) return list;
            int now = ElapsedMilliseconds;
            if (c.SilencedUntil > now) list.Add(new UnitStatus(UnitStatusKind.Sealed, "封印", RoundsRemaining(c.SilencedUntil)));
            if (c.StunnedUntil > now) list.Add(new UnitStatus(UnitStatusKind.Stunned, "眩晕", RoundsRemaining(c.StunnedUntil)));
            if (unit.Side == BattleSide.Enemy && TideSuppressionActive)
                list.Add(new UnitStatus(UnitStatusKind.TideSuppressed, "攻-" + TideSuppressionPermille / 10 + "%", 0));
            if (c.ArmorBrokenUntil > now) list.Add(new UnitStatus(UnitStatusKind.ArmorBreak, "破甲", RoundsRemaining(c.ArmorBrokenUntil)));
            if (c.PoisonStacks > 0) list.Add(new UnitStatus(UnitStatusKind.Poison, "毒×" + c.PoisonStacks, 0));
            if (c.AntiHealUntil > now) list.Add(new UnitStatus(UnitStatusKind.AntiHeal, "禁疗", RoundsRemaining(c.AntiHealUntil)));
            if (unit.Shield > 0)
                list.Add(new UnitStatus(UnitStatusKind.Shield, "盾" + unit.Shield,
                    c.TimedShield > 0 && c.TimedShieldUntil > now ? RoundsRemaining(c.TimedShieldUntil) : 0));
            if (c.ReductionUntil > now) list.Add(new UnitStatus(UnitStatusKind.Reduction, "减伤15%", RoundsRemaining(c.ReductionUntil)));
            if (c.AttackBuffUntil > now) list.Add(new UnitStatus(UnitStatusKind.AttackBuff, "攻+10%", RoundsRemaining(c.AttackBuffUntil)));
            AppendStatuses1111(unit, list); // 2026-11-11 护盾 / 破绽 / 扰乱 / 迷惑 / 幻影 (Chapter3Battle1111)
            return list;
        }
    }
}
