using System;
using System.Collections.Generic;

namespace ChoSiren.Systems.Tactics
{
    /// <summary>The same capability drives resolution, threat name and the cast target preview.</summary>
    public enum EnemySpecialCapability
    {
        HeavyStrike, SlowStrike, ArmorBreakStrike, StunStrike, AllyHeal, SilenceStrike,
        FinaleAll, AllyShield, TeamSweep,
        // 2026-11-01 (BossMechanics1101): boss rotation and reworked minion specials, turn battles only.
        BossNova, MassSilence, BossCorrode, Venom, Ambush, CharmSeal
    }

    public sealed partial class BattleSimulator
    {
        public const string EnemyGuardShieldSkillId = "rt-enemy-guard-shield";
        public const string EnemyTeamSweepSkillId = "rt-enemy-team-sweep";

        // Explicit chapter prefix prevents this small introduction from changing future content.
        private bool UsesChapterTwoRoles => Stage != null && Stage.UsesRealtime &&
            !Stage.HasBossPhases && Stage.RecommendedLevel >= 11 &&
            Stage.Id != null && Stage.Id.StartsWith("stage-2-", StringComparison.Ordinal);
        /// <summary>Turn battles give the guard its support role in every chapter (2026-10-20).</summary>
        private bool GuardSupportsAllies => UsesChapterTwoRoles || IsTurnBased && Stage != null && Stage.UsesRealtime;

        public EnemySpecialCapability EnemyCapabilityOf(BattleUnit unit)
        {
            if (unit == null || unit.Side != BattleSide.Enemy) return EnemySpecialCapability.HeavyStrike;
            if (TryPlannedCapability1101(unit, out EnemySpecialCapability planned)) return planned;
            if (Stage.HasBossPhases && unit.Id == encounterLeadId && EnemyPhase >= 3)
                return EnemySpecialCapability.FinaleAll;
            if (UsesChapterTwoRoles)
            {
                if (unit.Definition.Id == "pulse-guard") return EnemySpecialCapability.AllyShield;
                if (unit.Definition.Id == "noise-wraith") return EnemySpecialCapability.TeamSweep;
            }
            if (GuardSupportsAllies && unit.Definition.Id == "pulse-guard") return EnemySpecialCapability.AllyShield;
            switch (unit.Definition.Id)
            {
                case "echo-drone": return EnemySpecialCapability.SlowStrike;
                case "noise-wraith": return EnemySpecialCapability.ArmorBreakStrike;
                case "pulse-guard": case "static-golem": return EnemySpecialCapability.StunStrike;
                case "velvet-hexer": return EnemySpecialCapability.AllyHeal;
                case "siren-queen": return EnemySpecialCapability.SilenceStrike;
                default: return EnemySpecialCapability.HeavyStrike;
            }
        }

        public string EnemyCastIntent(BattleUnit unit)
        {
            switch (EnemyCapabilityOf(unit))
            {
                case EnemySpecialCapability.SlowStrike: return "迟缓";
                case EnemySpecialCapability.ArmorBreakStrike: return "破甲";
                case EnemySpecialCapability.StunStrike: return "眩晕";
                case EnemySpecialCapability.AllyHeal: return "治疗";
                case EnemySpecialCapability.SilenceStrike: return "封技";
                case EnemySpecialCapability.FinaleAll: case EnemySpecialCapability.TeamSweep: return "全攻";
                case EnemySpecialCapability.AllyShield: return "护盾";
                case EnemySpecialCapability.BossNova: return "全攻";
                case EnemySpecialCapability.MassSilence: return "禁魔";
                case EnemySpecialCapability.BossCorrode: return "侵蚀";
                case EnemySpecialCapability.Venom: return "剧毒";
                case EnemySpecialCapability.Ambush: return "突切";
                case EnemySpecialCapability.CharmSeal: return "封技";
                default: return "重击";
            }
        }

        public bool EnemyCastTargetsAll(BattleUnit unit)
        {
            var capability = EnemyCapabilityOf(unit);
            return capability == EnemySpecialCapability.FinaleAll || capability == EnemySpecialCapability.TeamSweep ||
                   capability == EnemySpecialCapability.BossNova || capability == EnemySpecialCapability.MassSilence ||
                   capability == EnemySpecialCapability.BossCorrode;
        }

        public string EnemyCastTargetDescription(BattleUnit unit) => EnemyCastTargetsAll(unit)
            ? "全队" : CastTarget(unit)?.Definition.Name ?? "目标";

        private string EnemyCapabilityThreatName(BattleUnit unit)
        {
            switch (EnemyCapabilityOf(unit))
            {
                case EnemySpecialCapability.SlowStrike: return "电磁干扰·迟缓";
                case EnemySpecialCapability.ArmorBreakStrike: return "侵蚀·破甲";
                case EnemySpecialCapability.StunStrike:
                    return unit?.Definition.Id == "static-golem" ? "重锤·眩晕" : "震击·眩晕";
                case EnemySpecialCapability.AllyHeal: return "回响治愈";
                case EnemySpecialCapability.SilenceStrike: return "魅惑·封技";
                case EnemySpecialCapability.FinaleAll: return "终演·全体震荡";
                case EnemySpecialCapability.AllyShield: return "援护·临时盾";
                case EnemySpecialCapability.TeamSweep: return "杂音·全体攻击";
                case EnemySpecialCapability.BossNova: return "魅声爆鸣·全体重击";
                case EnemySpecialCapability.MassSilence: return "群体禁魔·全队封技";
                case EnemySpecialCapability.BossCorrode: return "蚀心侵染·全体禁疗";
                case EnemySpecialCapability.Venom: return "毒牙·剧毒";
                case EnemySpecialCapability.Ambush: return "血镰·突切";
                case EnemySpecialCapability.CharmSeal: return "魅惑·封技";
                default: return "突袭·重击";
            }
        }

        // Same ratio/tie policy as the existing healer: self is a valid ally, ties prefer self.
        private BattleUnit LowestHpRatioAlly(BattleUnit actor)
        {
            BattleUnit lowest = actor != null && actor.Alive ? actor : null;
            if (lowest == null) return null;
            foreach (BattleUnit ally in units)
                if (ally.Side == actor.Side && ally.Alive &&
                    (long)ally.Hp * lowest.MaxHp < (long)lowest.Hp * ally.MaxHp) lowest = ally;
            return lowest;
        }

        private BattleUnit SelectEnemyCastTarget(BattleUnit actor)
        {
            var capability = EnemyCapabilityOf(actor);
            if (capability == EnemySpecialCapability.Ambush) return MostHurtMember1101(actor);
            return capability == EnemySpecialCapability.AllyShield || capability == EnemySpecialCapability.AllyHeal
                ? LowestHpRatioAlly(actor) : SelectEnemy(actor);
        }

        private BattleUnit PreviewEnemyCastTarget(BattleUnit actor)
        {
            var capability = EnemyCapabilityOf(actor);
            if (capability == EnemySpecialCapability.AllyShield || capability == EnemySpecialCapability.AllyHeal)
                return LowestHpRatioAlly(actor);
            BattleUnit target = FindUnit(performerClocks[actor.Id].CastTargetId);
            return target != null && target.Alive ? target : SelectEnemy(actor);
        }

        // Called after the common alive/silence check, before the original damaging special.
        private bool TryResolveChapterTwoSpecial(BattleUnit actor)
        {
            switch (EnemyCapabilityOf(actor))
            {
                case EnemySpecialCapability.AllyShield:
                    BattleUnit ally = LowestHpRatioAlly(actor);
                    if (ally != null)
                    {
                        Emit(BattleEventKind.ActionStarted, actor.Id, ally.Id, EnemyGuardShieldSkillId, 0, false);
                        // Existing timed-shield code refreshes the layer and expires it after 4000ms.
                        AddShield(actor, ally, EnemyGuardShieldSkillId, 20, true);
                    }
                    return true;
                case EnemySpecialCapability.TeamSweep:
                    var targets = LivingOpponents(actor);
                    if (targets.Count > 0)
                        Emit(BattleEventKind.ActionStarted, actor.Id, targets[0].Id, EnemyTeamSweepSkillId, 0, false);
                    // 4 x 40% replaces the old 160% single strike, before each target's mitigation.
                    foreach (BattleUnit target in targets) Deal(actor, target, EnemyTeamSweepSkillId, 400);
                    return true; // No old strike or armor-break can fall through.
                default: return false;
            }
        }

        private static void RegisterChapterTwoRealtimeSkills(Dictionary<string, SkillDefinition> result)
        {
            result.Add(EnemyGuardShieldSkillId, new SkillDefinition
            {
                Id = EnemyGuardShieldSkillId, Name = "脉冲援护", Effect = SkillEffect.Shield,
                Pattern = SkillPattern.Single, PowerPermille = 20, Duration = 4, CanCrit = false
            });
            result.Add(EnemyTeamSweepSkillId, new SkillDefinition
            {
                Id = EnemyTeamSweepSkillId, Name = "杂音扩散", Effect = SkillEffect.Damage,
                Pattern = SkillPattern.All, PowerPermille = 400
            });
        }
    }
}
