using System;
using System.Collections.Generic;

namespace ChoSiren.Systems.Tactics
{
    /// <summary>
    /// 2026-11-01 (赵金: "怪的技能修改优先级高 / BOSS 都是全体攻击 / 小BOSS 攻击力高一点、攻击频率高一点 / 群体禁魔 /
    /// 大BOSS 就更不要说了"). Turn battles (the shipped mode) only; realtime replays keep their old rules.
    /// <list type="bullet">
    /// <item>Bosses (stage BossTier mini / major) act twice a round: once at their speed, once more at the round's end.</item>
    /// <item>Boss attacks hit the whole team: 碎音横扫 (plain action) and, every third boss action, a special announced one
    /// action ahead — 小BOSS 魅声爆鸣 → 群体禁魔 → 爆鸣 → 爆鸣; 大BOSS 爆鸣 → 群体禁魔 → 蚀心侵染 → 爆鸣, and below 30%
    /// health 终演震荡 → 群体禁魔 → 蚀心侵染 (see <see cref="BossRotation"/>).</item>
    /// <item>群体禁魔 seals every member's skill and ultimate for <see cref="MassSilenceRounds"/> rounds (the existing 封印
    /// badge, heart-lock chains and 「封印」 popup show it); 魅语迷心 sealing the boss while it charges cancels it.</item>
    /// <item>Minion roles (PPT 幻浊体): 毒蚀蛛娘 poisons (or patches a badly hurt ally), 浊浪溺者 regenerates 5% a turn,
    /// 赤血刺客 cuts the most hurt member, 幻魅歌姬 / 狂响幻蚀歌姬 seal one member's skills.</item>
    /// </list>
    /// Every special is announced one turn ahead with the existing 蓄力预警 (rt-cast) badge and intent label.
    /// </summary>
    public sealed partial class BattleSimulator
    {
        public const string BossTierMini = "mini";
        public const string BossTierMajor = "major";
        public const string BossSweepSkillId = "rt-boss-sweep";
        public const string BossNovaSkillId = "rt-boss-nova";
        public const string BossMassSilenceSkillId = "rt-boss-mass-silence";
        public const string BossCorrodeSkillId = "rt-boss-corrode";
        public const string EnemyVenomSkillId = "rt-enemy-venom";
        public const string EnemyRegenSkillId = "rt-enemy-regen";
        public const string EnemyAmbushSkillId = "rt-enemy-ambush";
        public const string EnemyCharmSealSkillId = "rt-enemy-charm-seal";

        /// <summary>Boss turns per round (both tiers; the major boss is stronger through its rotation and stats).</summary>
        public const int BossActionsPerRound = 2;
        /// <summary>Every third boss action starts charging the next special (charge → release → sweep).</summary>
        public const int BossSpecialEveryActions = 3;
        public const int BossSweepPower = 520;
        public const int BossNovaPower = 1000;
        public const int BossFinalePower = 1300;
        public const int BossMassSilencePower = 300;
        public const int BossCorrodePower = 650;
        public const int MassSilenceRounds = 2;
        public const int BossCorrodePoison = 2;
        public const int EnemyVenomPower = 1100;
        public const int EnemyVenomPoison = 3;
        public const int EnemyAmbushPower = 2000;
        public const int EnemyRegenPermille = 30;
        public const int EnemyCharmSealRounds = 1;

        private int bossActions1101;
        private int bossRotation1101;
        private readonly Dictionary<int, EnemySpecialCapability> plannedSpecial1101 = new Dictionary<int, EnemySpecialCapability>();
        private readonly Dictionary<int, int> golemSpecials1101 = new Dictionary<int, int>();

        public bool IsMiniBossStage => Stage.HasBossPhases && Stage.BossTier == BossTierMini;
        public bool IsMajorBossStage => Stage.HasBossPhases && Stage.BossTier == BossTierMajor;

        /// <summary>True for the boss of a mini/major boss stage while the turn battle runs.</summary>
        public bool UsesBossMechanics(BattleUnit unit) =>
            IsTurnBased && (IsMiniBossStage || IsMajorBossStage) && IsStageBoss(unit);

        /// <summary>The boss rotation, in order (for tests, the report and the stage tips).</summary>
        public EnemySpecialCapability[] BossRotation()
        {
            // 群体禁魔 comes every fourth special (about every six rounds) — often enough to plan around, rare enough
            // that skills and ultimates still carry the fight. Below 30% the major boss closes with 终演震荡 and seals
            // every third special instead.
            const EnemySpecialCapability Nova = EnemySpecialCapability.BossNova, Silence = EnemySpecialCapability.MassSilence,
                Corrode = EnemySpecialCapability.BossCorrode, Finale = EnemySpecialCapability.FinaleAll;
            if (IsMajorBossStage) return EnemyPhase >= 3 ? new[] { Finale, Silence, Corrode } : new[] { Nova, Silence, Corrode, Nova };
            return EnemyPhase >= 3 ? new[] { Finale, Silence, Nova, Finale } : new[] { Nova, Silence, Nova, Nova };
        }

        private EnemySpecialCapability NextBossSpecial1101()
        {
            EnemySpecialCapability[] rotation = BossRotation();
            return rotation[bossRotation1101 % rotation.Length];
        }

        /// <summary>Turn order: the boss gets its extra action(s) at the end of each round.</summary>
        private void AddBossExtraTurns1101()
        {
            if (!IsMiniBossStage && !IsMajorBossStage) return;
            BattleUnit boss = FindUnit(encounterLeadId);
            if (boss == null || !boss.Alive || !IsStageBoss(boss)) return;
            for (int i = 1; i < BossActionsPerRound; i++) turnOrderQueue.Add(boss.Id);
        }

        /// <summary>Capability the panel and the resolver share; a charging unit keeps what it announced.</summary>
        private bool TryPlannedCapability1101(BattleUnit unit, out EnemySpecialCapability capability)
        {
            capability = EnemySpecialCapability.HeavyStrike;
            if (!IsTurnBased || unit == null || unit.Side != BattleSide.Enemy || !performerClocks.TryGetValue(unit.Id, out PerformerClock clock))
                return false;
            if (clock.CastUntil > 0 && plannedSpecial1101.TryGetValue(unit.Id, out capability)) return true;
            if (UsesBossMechanics(unit)) { capability = NextBossSpecial1101(); return true; }
            switch (unit.Definition.Id)
            {
                case "velvet-hexer":
                    BattleUnit hurt = LowestHpRatioAlly(unit);
                    capability = hurt != null && (long)hurt.Hp * 2 < hurt.MaxHp ? EnemySpecialCapability.AllyHeal : EnemySpecialCapability.Venom;
                    return true;
                case "neon-scout": case "afterimage-diva": capability = EnemySpecialCapability.Ambush; return true;
                case "purify-dancer":
                    BattleUnit wounded = LowestHpRatioAlly(unit);
                    if (wounded == null || (long)wounded.Hp * 2 >= wounded.MaxHp) return false;
                    capability = EnemySpecialCapability.AllyHeal;
                    return true;
                case "echo-drone": capability = EnemySpecialCapability.CharmSeal; return true;
                case "static-golem":
                    golemSpecials1101.TryGetValue(unit.Id, out int casts);
                    capability = casts % 2 == 0 ? EnemySpecialCapability.StunStrike : EnemySpecialCapability.CharmSeal;
                    return true;
                default: return false;
            }
        }

        /// <summary>Called as a special starts charging (turn mode): freezes what the enemy announced.</summary>
        private void RecordPlannedSpecial1101(BattleUnit actor)
        {
            if (!IsTurnBased || actor == null) return;
            if (UsesBossMechanics(actor)) { plannedSpecial1101[actor.Id] = NextBossSpecial1101(); bossRotation1101++; return; }
            plannedSpecial1101[actor.Id] = EnemyCapabilityOf(actor);
            if (actor.Definition.Id == "static-golem")
                golemSpecials1101[actor.Id] = (golemSpecials1101.TryGetValue(actor.Id, out int n) ? n : 0) + 1;
        }

        /// <summary>One boss action. Returns false when the regular enemy turn should run instead.</summary>
        private bool BossTurn1101(BattleUnit actor)
        {
            if (!UsesBossMechanics(actor)) return false;
            PerformerClock clock = performerClocks[actor.Id];
            bossActions1101++;
            if (clock.CastUntil > 0)
            {
                clock.CastUntil = 0;
                // 魅语迷心 sealed the boss while it charged: the special fizzles into a plain sweep.
                if (clock.SilencedUntil > ElapsedMilliseconds) BossSweep1101(actor);
                else ResolveTelegraphedSpecial(actor);
            }
            else if (bossActions1101 % BossSpecialEveryActions == 0 && clock.SilencedUntil <= ElapsedMilliseconds)
                BeginTelegraphedSpecial(actor);
            else BossSweep1101(actor);
            return true;
        }

        private void BossSweep1101(BattleUnit actor)
        {
            List<BattleUnit> targets = LivingOpponents(actor);
            if (targets.Count == 0) return;
            Emit(BattleEventKind.ActionStarted, actor.Id, targets[0].Id, BossSweepSkillId, 0, false);
            foreach (BattleUnit target in targets) Deal(actor, target, BossSweepSkillId, BossSweepPower);
        }

        /// <summary>Turn-mode specials of bosses and of the reworked minions. False = use the original resolver.</summary>
        private bool TryResolveSpecial1101(BattleUnit actor)
        {
            if (!IsTurnBased || !plannedSpecial1101.TryGetValue(actor.Id, out EnemySpecialCapability planned)) return false;
            plannedSpecial1101.Remove(actor.Id);
            switch (planned)
            {
                case EnemySpecialCapability.BossNova: TeamStrike1101(actor, BossNovaSkillId, BossNovaPower); return true;
                case EnemySpecialCapability.FinaleAll:
                    if (!UsesBossMechanics(actor)) return false;
                    TeamStrike1101(actor, "rt-enemy-finale", BossFinalePower);
                    return true;
                case EnemySpecialCapability.MassSilence:
                {
                    List<BattleUnit> targets = TeamStrike1101(actor, BossMassSilenceSkillId, BossMassSilencePower);
                    foreach (BattleUnit target in targets) SealMember1101(actor, target, MassSilenceRounds);
                    return true;
                }
                case EnemySpecialCapability.BossCorrode:
                {
                    List<BattleUnit> targets = TeamStrike1101(actor, BossCorrodeSkillId, BossCorrodePower);
                    foreach (BattleUnit target in targets)
                    {
                        if (!target.Alive || Outcome != BattleOutcome.Ongoing) continue;
                        performerClocks[target.Id].AntiHealUntil = ElapsedMilliseconds + TurnRoundsMilliseconds1101(2);
                        AddPoison(target, actor, BossCorrodePoison);
                        Emit(BattleEventKind.Buff, actor.Id, target.Id, BossCorrodeSkillId, BossCorrodePoison, false);
                    }
                    return true;
                }
                case EnemySpecialCapability.Venom:
                {
                    BattleUnit target = CastTargetOrEnemy1101(actor);
                    if (target == null) return true;
                    Emit(BattleEventKind.ActionStarted, actor.Id, target.Id, EnemyVenomSkillId, 0, false);
                    Deal(actor, target, EnemyVenomSkillId, EnemyVenomPower);
                    if (target.Alive && Outcome == BattleOutcome.Ongoing)
                    {
                        AddPoison(target, actor, EnemyVenomPoison);
                        Emit(BattleEventKind.Buff, actor.Id, target.Id, EnemyVenomSkillId, EnemyVenomPoison, false);
                    }
                    return true;
                }
                case EnemySpecialCapability.AllyHeal:
                {
                    BattleUnit lowest = LowestHpRatioAlly(actor);
                    if (lowest == null) return true;
                    Emit(BattleEventKind.ActionStarted, actor.Id, lowest.Id, "rt-mermaid-small", 0, false);
                    HealPercent(actor, lowest, "rt-mermaid-small", 40);
                    return true;
                }
                case EnemySpecialCapability.Ambush:
                {
                    BattleUnit target = MostHurtMember1101(actor);
                    if (target == null) return true;
                    Emit(BattleEventKind.ActionStarted, actor.Id, target.Id, EnemyAmbushSkillId, 0, false);
                    Deal(actor, target, EnemyAmbushSkillId, EnemyAmbushPower);
                    return true;
                }
                case EnemySpecialCapability.CharmSeal:
                {
                    BattleUnit target = CastTargetOrEnemy1101(actor);
                    if (target == null) return true;
                    Emit(BattleEventKind.ActionStarted, actor.Id, target.Id, EnemyCharmSealSkillId, 0, false);
                    Deal(actor, target, EnemyCharmSealSkillId, 1100);
                    SealMember1101(actor, target, EnemyCharmSealRounds);
                    if (actor.Definition.Id == "echo-drone") MarkEcho(actor, target);
                    return true;
                }
                default: return false;
            }
        }

        private List<BattleUnit> TeamStrike1101(BattleUnit actor, string skill, int power)
        {
            List<BattleUnit> targets = LivingOpponents(actor);
            if (targets.Count == 0) return targets;
            Emit(BattleEventKind.ActionStarted, actor.Id, targets[0].Id, skill, 0, false);
            foreach (BattleUnit target in targets) Deal(actor, target, skill, power);
            return targets;
        }

        /// <summary>Seals a member's skill and ultimate (basic attacks go on) with the existing 封印 feedback.</summary>
        private void SealMember1101(BattleUnit actor, BattleUnit target, int rounds)
        {
            if (target == null || !target.Alive || Outcome != BattleOutcome.Ongoing) return;
            PerformerClock clock = performerClocks[target.Id];
            clock.SilencedUntil = Math.Max(clock.SilencedUntil, ElapsedMilliseconds + TurnRoundsMilliseconds1101(rounds));
            if (QueuedUltimateId == target.Id) QueuedUltimateId = -1;
            Emit(BattleEventKind.Buff, actor.Id, target.Id, "rt-seal", rounds, false);
        }

        /// <summary>Whole rounds of logical time, counting the boss's extra actions (each action spends one slice).</summary>
        private int TurnRoundsMilliseconds1101(int rounds)
        {
            int extra = IsMiniBossStage || IsMajorBossStage ? BossActionsPerRound - 1 : 0;
            return RoundsToMilliseconds(rounds) + Math.Max(1, rounds) * extra * TurnSliceMilliseconds;
        }

        private BattleUnit CastTargetOrEnemy1101(BattleUnit actor)
        {
            BattleUnit target = FindUnit(performerClocks[actor.Id].CastTargetId);
            return target != null && target.Alive && target.Side != actor.Side ? target : SelectEnemy(actor);
        }

        private BattleUnit MostHurtMember1101(BattleUnit actor)
        {
            BattleUnit weakest = null;
            foreach (BattleUnit p in LivingOpponents(actor))
                if (weakest == null || (long)p.Hp * weakest.MaxHp < (long)weakest.Hp * p.MaxHp) weakest = p;
            return weakest;
        }

        /// <summary>浊浪溺者: regains 5% of its health at the start of each of its turns (green heal number + 「浊浪再生」).</summary>
        private void EnemyTurnStart1101(BattleUnit actor)
        {
            if (!IsTurnBased || actor == null || actor.Definition.Id != "pulse-guard" || !actor.Alive || actor.Hp >= actor.MaxHp) return;
            int amount = Math.Max(1, (int)((long)actor.MaxHp * EnemyRegenPermille / 1000));
            if (performerClocks[actor.Id].AntiHealUntil > ElapsedMilliseconds) amount = amount * 700 / 1000;
            if (PoisonStacks(actor) > 0) amount /= 2;
            int applied = Math.Min(actor.MaxHp - actor.Hp, amount);
            if (applied <= 0) return;
            actor.Hp += applied;
            Emit(BattleEventKind.Heal, actor.Id, actor.Id, EnemyRegenSkillId, applied, false);
        }

        /// <summary>
        /// Turn mode: every enemy hit rolls 85%–115% (per target). Without it a fight was a fixed race — one point of
        /// enemy attack or health flipped almost every seed at once (win rate jumping 89% → 35%), so 通过率 could not be
        /// tuned and every retry of a lost stage played out the same way.
        /// </summary>
        public const int EnemyDamageSpreadPermille = 150;

        private int EnemyDamageRollPermille1101(BattleUnit actor) =>
            IsTurnBased && actor != null && actor.Side == BattleSide.Enemy
                ? 1000 - EnemyDamageSpreadPermille + random.Next(2 * EnemyDamageSpreadPermille + 1) : 1000;

        private static void RegisterBossSkills1101(Dictionary<string, SkillDefinition> result)
        {
            void Add(string id, string name, string effect = SkillEffect.Damage, string pattern = SkillPattern.Single) =>
                result.Add(id, new SkillDefinition { Id = id, Name = name, Effect = effect, Pattern = pattern });
            Add(BossSweepSkillId, "碎音横扫", SkillEffect.Damage, SkillPattern.All);
            Add(BossNovaSkillId, "魅声爆鸣", SkillEffect.Damage, SkillPattern.All);
            Add(BossMassSilenceSkillId, "群体禁魔", SkillEffect.Damage, SkillPattern.All);
            Add(BossCorrodeSkillId, "蚀心侵染", SkillEffect.Damage, SkillPattern.All);
            Add(EnemyVenomSkillId, "毒牙·剧毒");
            Add(EnemyRegenSkillId, "浊浪再生", SkillEffect.Heal);
            Add(EnemyAmbushSkillId, "血镰突切");
            Add(EnemyCharmSealSkillId, "魅惑封技");
        }
    }
}
