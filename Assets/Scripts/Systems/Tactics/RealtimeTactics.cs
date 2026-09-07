using System;
using System.Collections.Generic;

namespace ChoSiren.Systems.Tactics
{
    public enum CombatCondition { Stun, Slow, ArmorBreak, ControlWard }

    public sealed partial class BattleSimulator
    {
        private int nextTacticalInterrupt;
        public int InterruptCooldownRemaining => Math.Max(0, nextTacticalInterrupt - ElapsedMilliseconds);

        public int ConditionRemaining(BattleUnit unit, CombatCondition condition)
        {
            if (!IsRealtime || unit == null || !unit.Alive || !performerClocks.TryGetValue(unit.Id, out var c)) return 0;
            int until = condition == CombatCondition.Stun ? c.StunnedUntil : condition == CombatCondition.Slow
                ? c.SlowUntil : condition == CombatCondition.ArmorBreak ? c.ArmorBrokenUntil : c.ControlWardUntil;
            return Math.Max(0, until - ElapsedMilliseconds);
        }

        public int EffectiveRealtimeDefense(BattleUnit unit) => unit == null ? 0 :
            (int)((long)unit.Defense * (ConditionRemaining(unit, CombatCondition.ArmorBreak) > 0 ? 70 : 100) / 100);

        public int CastRemaining(BattleUnit unit) => unit != null && unit.Alive &&
            performerClocks.TryGetValue(unit.Id, out var c) ? Math.Max(0, c.CastUntil - ElapsedMilliseconds) : 0;

        public BattleUnit InterruptTarget
        {
            get
            {
                BattleUnit focused = FindUnit(FocusTargetId);
                if (focused != null && focused.Side == BattleSide.Enemy && CastRemaining(focused) > 0) return focused;
                BattleUnit earliest = null;
                foreach (BattleUnit unit in units)
                    if (unit.Side == BattleSide.Enemy && CastRemaining(unit) > 0 &&
                        (earliest == null || CastRemaining(unit) < CastRemaining(earliest))) earliest = unit;
                return earliest;
            }
        }

        public bool TryTacticalInterrupt()
        {
            if (!IsRealtime || Outcome != BattleOutcome.Ongoing || InterruptCooldownRemaining > 0) return false;
            BattleUnit target = InterruptTarget, actor = null;
            if (target == null) return false;
            foreach (BattleUnit unit in units)
                if (unit.Side == BattleSide.Player && unit.Alive &&
                    ConditionRemaining(unit, CombatCondition.Stun) == 0) { actor = unit; break; }
            if (actor == null || !ApplyCondition(actor, target, CombatCondition.Stun, 1200)) return false;
            nextTacticalInterrupt = ElapsedMilliseconds + 12000;
            return true;
        }

        private bool ApplyCondition(BattleUnit actor, BattleUnit target, CombatCondition condition, int duration)
        {
            if (target == null || !target.Alive || Outcome != BattleOutcome.Ongoing) return false;
            PerformerClock c = performerClocks[target.Id];
            string id;
            switch (condition)
            {
                case CombatCondition.Stun:
                    if (c.ControlWardUntil > ElapsedMilliseconds || c.ControlRecoveryUntil > ElapsedMilliseconds)
                    { Emit(BattleEventKind.Buff, actor.Id, target.Id, "rt-resist", 0, false); return false; }
                    if (Stage.HasBossPhases && target.Id == encounterLeadId) duration = Math.Min(600, duration);
                    c.StunnedUntil = ElapsedMilliseconds + duration;
                    c.ControlRecoveryUntil = c.StunnedUntil + 3000;
                    c.NextBasic = Math.Max(c.NextBasic, c.StunnedUntil + 200);
                    if (c.CastUntil > 0)
                    {
                        c.CastUntil = 0;
                        c.NextSmall = ElapsedMilliseconds + 6000;
                        Emit(BattleEventKind.Buff, actor.Id, target.Id, "rt-interrupt", 0, false);
                    }
                    id = "rt-stun"; break;
                case CombatCondition.Slow: c.SlowUntil = ElapsedMilliseconds + duration; id = "rt-slow"; break;
                case CombatCondition.ArmorBreak: c.ArmorBrokenUntil = ElapsedMilliseconds + duration; id = "rt-armor-break"; break;
                default: c.ControlWardUntil = ElapsedMilliseconds + duration; id = "rt-control-ward"; break;
            }
            Emit(BattleEventKind.Buff, actor.Id, target.Id, id, duration, false);
            return true;
        }

        private void CleanseOne(BattleUnit source, BattleUnit ally)
        {
            var c = performerClocks[ally.Id];
            if (c.StunnedUntil > ElapsedMilliseconds) c.StunnedUntil = 0;
            else if (c.SilencedUntil > ElapsedMilliseconds) c.SilencedUntil = 0;
            else if (c.ArmorBrokenUntil > ElapsedMilliseconds) c.ArmorBrokenUntil = 0;
            else if (c.SlowUntil > ElapsedMilliseconds) c.SlowUntil = 0;
            else if (c.AntiHealUntil > ElapsedMilliseconds) c.AntiHealUntil = 0;
            else return;
            Emit(BattleEventKind.Buff, source.Id, ally.Id, "rt-cleanse", 0, false);
        }

        private string TacticalStatus(BattleUnit unit)
        {
            var c = performerClocks[unit.Id];
            var labels = new List<string>();
            string Seconds(int ms) => (Math.Max(0, ms - ElapsedMilliseconds) / 1000f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "s";
            if (c.CastUntil > ElapsedMilliseconds) labels.Add("蓄力 " + Seconds(c.CastUntil));
            if (c.StunnedUntil > ElapsedMilliseconds) labels.Add("眩晕 " + Seconds(c.StunnedUntil));
            if (c.ArmorBrokenUntil > ElapsedMilliseconds) labels.Add("破甲30%");
            if (c.SlowUntil > ElapsedMilliseconds) labels.Add("迟缓35%");
            if (c.ControlWardUntil > ElapsedMilliseconds) labels.Add("控免 " + Seconds(c.ControlWardUntil));
            return string.Join(" · ", labels.GetRange(0, Math.Min(2, labels.Count)));
        }

        public string EnemyThreatName(BattleUnit unit)
        {
            switch (unit?.Definition.Id)
            {
                case "echo-drone": return "电磁干扰·迟缓";
                case "noise-wraith": return "侵蚀·破甲";
                case "pulse-guard": return "震击·眩晕";
                case "velvet-hexer": return "回响治愈";
                case "static-golem": return "重锤·眩晕";
                case "siren-queen": return EnemyPhase >= 3 ? "终演·全体震荡" : "魅惑·封技";
                default: return "突袭·重击";
            }
        }

        private void BeginTelegraphedSpecial(BattleUnit actor)
        {
            var target = SelectEnemy(actor);
            if (target == null) return;
            var c = performerClocks[actor.Id];
            c.CastUntil = ElapsedMilliseconds + 2000;
            c.CastTargetId = target.Id;
            Emit(BattleEventKind.Buff, actor.Id, actor.Id, "rt-cast", 2000, false);
        }

        private void ResolveTelegraphedSpecial(BattleUnit actor)
        {
            var c = performerClocks[actor.Id];
            if (!actor.Alive || c.SilencedUntil > ElapsedMilliseconds) return;
            BattleUnit target = FindUnit(c.CastTargetId);
            if (target == null || !target.Alive) target = SelectEnemy(actor);
            if (target == null) return;
            if (actor.Definition.Id == "velvet-hexer")
            {
                BattleUnit lowest = actor;
                foreach (BattleUnit ally in units)
                    if (ally.Side == actor.Side && ally.Alive && (long)ally.Hp * lowest.MaxHp < (long)lowest.Hp * ally.MaxHp) lowest = ally;
                Emit(BattleEventKind.ActionStarted, actor.Id, lowest.Id, "rt-mermaid-small", 0, false);
                HealPercent(actor, lowest, "rt-mermaid-small", 40);
                return;
            }
            if (Stage.HasBossPhases && actor.Id == encounterLeadId && EnemyPhase >= 3) { EnemySpecial(actor); return; }
            Emit(BattleEventKind.ActionStarted, actor.Id, target.Id, "rt-enemy-heavy", 0, false);
            Deal(actor, target, "rt-enemy-heavy", 1600);
            switch (actor.Definition.Id)
            {
                case "echo-drone": ApplyCondition(actor, target, CombatCondition.Slow, 3000); break;
                case "noise-wraith": ApplyCondition(actor, target, CombatCondition.ArmorBreak, 4000); break;
                case "pulse-guard": case "static-golem": ApplyCondition(actor, target, CombatCondition.Stun, 1200); break;
                case "siren-queen":
                    if (target.Alive) performerClocks[target.Id].SilencedUntil = ElapsedMilliseconds + 2000;
                    break;
            }
        }
    }
}
