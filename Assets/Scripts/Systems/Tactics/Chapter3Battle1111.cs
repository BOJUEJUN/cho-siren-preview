using System;
using System.Collections.Generic;
using ChoSiren.Systems.Dice;

namespace ChoSiren.Systems.Tactics
{
    /// <summary>The three members 赵金 designed on 2026-11-11; each replaces her race's two active skills.</summary>
    public enum MemberKit1111 { None, Huansha, Huanhuo, Linyin }

    /// <summary>
    /// 2026-11-11 (赵金 11 月内容, Docs/CONTENT-1111.md): 第三章 BOSS 护盾 (破盾才能扣血), 弹反 (enemy specials can be
    /// parried with a timed tap and reflected) and the kits of 幻纱 / 幻惑伶 / 凛音. 2026-11-12 (同事第三章设计 "敌方顶盾"): the
    /// barrier is a row of shield layers — 精英 2, 小BOSS 3, 大BOSS 5 (3-20: 6) — each taking two hits; 功能骰 live in RelicDice1111.
    /// Turn battles only; the design's seconds are mapped to whole rounds (3 s ≈ 2 rounds, 5 s ≈ 2, 10 s ≈ 4).
    /// </summary>
    public sealed partial class BattleSimulator
    {
        // ---------------------------------------------------------------- tuning
        public const int ShieldLayersElite = 2;
        public const int ShieldLayersMiniBoss = 3;
        public const int ShieldLayersMajorBoss = 5;
        public const int ShieldLayersFinalBoss = 6;
        public const int HitsPerShieldLayer = 2;
        /// <summary>穿刺 (血精灵: 绯鸢 / 凛音) hits pass this share to the body while the shield stands.</summary>
        public const int PiercePassPermille = 400;
        /// <summary>毒爆 grinds a layer and lets this share through.</summary>
        public const int PoisonPassPermille = 500;
        public const int BarrierBrokenBonusPermille = 200;
        public const int BarrierBrokenRounds = 2;
        public const int ParryReflectPermille = 1000;
        public const int FlawPermillePerStack = 150;
        public const int FlawMaxStacks = 2;
        public const int FlawRounds = 2;
        public const int FlawCapRounds = 4;
        public const int FiveKindVulnerablePermille = 50;
        public const int DisguiseRounds = 2;
        public const int DisguiseDecoyPermille = 600;
        public const int DisguiseDecoyBossPermille = 300;
        public const int PhantomDodgePermille = 250;
        public const int PhantomHpPermille = 300;
        public const int ConfuseBossPermille = 500;
        public const int LinyinTeamPiercePermille = 150;
        public const int LinyinBreakEnergy = 5;
        public const int LinyinDefenseDownPermille = 200;
        public const int LinyinShieldedBonusPermille = 300;

        public const string BarrierSkillId = "rt-barrier";
        public const string ShieldHitSkillId = "rt-shield-hit";
        public const string LayerBreakSkillId = "rt-layer-break";
        public const string BarrierBreakSkillId = "rt-barrier-break";
        public const string ParrySkillId = "rt-parry";
        public const string ParryReflectSkillId = "rt-parry-reflect";
        public const string PhantomSkillId = "rt-phantom";
        public const string PhantomPopSkillId = "rt-phantom-pop";
        public const string PhantomEchoSkillId = "rt-phantom-echo";
        public const string DisguiseSkillId = "rt-disguise";
        public const string DecoySkillId = "rt-decoy";
        public const string DodgeSkillId = "rt-dodge";
        public const string FlawSkillId = "rt-flaw";
        public const string ConfuseSkillId = "rt-confuse";
        public const string ConfuseHitSkillId = "rt-confuse-hit";
        public const string ShieldStripSkillId = "rt-shield-strip";

        private sealed class KitState1111
        {
            public int PhantomHp, DisguiseUntil;
            public int FlawStacks, FlawUntil;
            public int ConfusedUntil;
            public int ShieldLayers, ShieldLayersMax, LayerHits;
            public int BarrierBrokenUntil, DefenseDownUntil;
            public int BlindUntil, VenomUntil;
        }

        private readonly Dictionary<int, KitState1111> kits1111 = new Dictionary<int, KitState1111>();
        private int parryActor1111 = -1, parryPermille1111, parryReflect1111;

        /// <summary>第三章 rules (boss barrier, parry). Set from the stage at start; tests may switch it on.</summary>
        public bool Chapter3Rules1111 { get; set; }
        /// <summary>The last parry's result (for the panel): blocked damage, 0 = none.</summary>
        public int LastParryBlocked1111 { get; private set; }

        public static bool IsChapter3Stage(StageDefinition stage) => stage != null &&
            (stage.Id.StartsWith("stage-3-", StringComparison.Ordinal) || stage.Chapter.Contains("03"));

        public static MemberKit1111 KitOf(string unitId)
        {
            switch (unitId)
            {
                case "huansha": return MemberKit1111.Huansha;
                case "huanhuoling": return MemberKit1111.Huanhuo;
                case "linyin": return MemberKit1111.Linyin;
                default: return MemberKit1111.None;
            }
        }

        public MemberKit1111 KitOf(BattleUnit unit) =>
            unit != null && unit.Side == BattleSide.Player ? KitOf(unit.Definition.Id) : MemberKit1111.None;

        private KitState1111 Kit1111(BattleUnit unit)
        {
            if (!kits1111.TryGetValue(unit.Id, out KitState1111 state)) kits1111[unit.Id] = state = new KitState1111();
            return state;
        }

        // ---------------------------------------------------------------- readouts for the panel / tests
        /// <summary>Shield layers still standing (第三章 顶盾).</summary>
        public int Barrier(BattleUnit unit) => unit != null && kits1111.TryGetValue(unit.Id, out var s) ? s.ShieldLayers : 0;
        public int BarrierMax(BattleUnit unit) => unit != null && kits1111.TryGetValue(unit.Id, out var s) ? s.ShieldLayersMax : 0;
        /// <summary>Hits the current layer has taken (0 or 1 of <see cref="HitsPerShieldLayer"/>).</summary>
        public int BarrierLayerHits(BattleUnit unit) => unit != null && kits1111.TryGetValue(unit.Id, out var s) ? s.LayerHits : 0;
        public bool IsBlinded(BattleUnit unit) => unit != null && kits1111.TryGetValue(unit.Id, out var s) &&
            s.BlindUntil > ElapsedMilliseconds;
        public bool BarrierBroken(BattleUnit unit) => unit != null && kits1111.TryGetValue(unit.Id, out var s) &&
            s.BarrierBrokenUntil > ElapsedMilliseconds;
        public int FlawStacks(BattleUnit unit) => unit != null && kits1111.TryGetValue(unit.Id, out var s) &&
            s.FlawUntil > ElapsedMilliseconds ? s.FlawStacks : 0;
        public bool IsConfused(BattleUnit unit) => unit != null && kits1111.TryGetValue(unit.Id, out var s) &&
            s.ConfusedUntil > ElapsedMilliseconds;
        public bool HasPhantom(BattleUnit unit) => unit != null && kits1111.TryGetValue(unit.Id, out var s) && s.PhantomHp > 0;
        public bool IsDisguised(BattleUnit unit) => unit != null && kits1111.TryGetValue(unit.Id, out var s) &&
            s.DisguiseUntil > ElapsedMilliseconds;
        public bool IsDefenseDown(BattleUnit unit) => unit != null && kits1111.TryGetValue(unit.Id, out var s) &&
            s.DefenseDownUntil > ElapsedMilliseconds;

        /// <summary>Badges over a unit for the 第三章 / new-member effects (existing badge kinds and icons).</summary>
        private void AppendStatuses1111(BattleUnit unit, List<UnitStatus> list)
        {
            if (!kits1111.TryGetValue(unit.Id, out KitState1111 s)) return;
            int now = ElapsedMilliseconds;
            if (s.ShieldLayers > 0) list.Insert(0, new UnitStatus(UnitStatusKind.Shield, "盾×" + s.ShieldLayers, 0));
            if (s.BlindUntil > now) list.Add(new UnitStatus(UnitStatusKind.Stunned, "致盲", RoundsRemaining(s.BlindUntil)));
            if (s.VenomUntil > now) list.Add(new UnitStatus(UnitStatusKind.Poison, "蚀毒", RoundsRemaining(s.VenomUntil)));
            if (s.BarrierBrokenUntil > now) list.Insert(0, new UnitStatus(UnitStatusKind.ArmorBreak, "破盾", RoundsRemaining(s.BarrierBrokenUntil)));
            if (s.ConfusedUntil > now) list.Add(new UnitStatus(UnitStatusKind.Stunned, "扰乱", RoundsRemaining(s.ConfusedUntil)));
            if (s.FlawUntil > now && s.FlawStacks > 0)
                list.Add(new UnitStatus(UnitStatusKind.ArmorBreak, "破绽×" + s.FlawStacks, RoundsRemaining(s.FlawUntil)));
            if (s.DefenseDownUntil > now) list.Add(new UnitStatus(UnitStatusKind.ArmorBreak, "防-20%", RoundsRemaining(s.DefenseDownUntil)));
            if (s.DisguiseUntil > now) list.Add(new UnitStatus(UnitStatusKind.Reduction, "迷惑", RoundsRemaining(s.DisguiseUntil)));
            if (s.PhantomHp > 0) list.Add(new UnitStatus(UnitStatusKind.Shield, "幻影", 0));
        }

        // ---------------------------------------------------------------- setup
        private void InitChapter3_1111()
        {
            Chapter3Rules1111 = IsChapter3Stage(Stage);
            if (!Chapter3Rules1111) return;
            GiveOpeningBarrier1111();
        }

        /// <summary>
        /// 第三章 顶盾: the elite of an elite stage and the boss of a boss stage start behind shield layers (普通小怪没有盾).
        /// Tests may call it after switching the rules on.
        /// </summary>
        public void GiveOpeningBarrier1111()
        {
            if (!Chapter3Rules1111) return;
            int layers = Stage.EncounterType == "elite" ? ShieldLayersElite
                : Stage.HasBossPhases ? (Stage.BossTier == BossTierMajor
                    ? (Stage.Id == "stage-3-20" ? ShieldLayersFinalBoss : ShieldLayersMajorBoss) : ShieldLayersMiniBoss)
                : 0;
            BattleUnit lead = FindUnit(encounterLeadId);
            if (layers <= 0 || lead == null || lead.Side != BattleSide.Enemy) return;
            KitState1111 s = Kit1111(lead);
            s.ShieldLayers = s.ShieldLayersMax = layers;
            s.LayerHits = 0;
            Emit(BattleEventKind.Buff, lead.Id, lead.Id, BarrierSkillId, layers, false);
        }

        /// <summary>Most dice showing one face (2 = 对子, 3 = 三骰, 5 = 五骰).</summary>
        public int MaxOfAKind
        {
            get
            {
                if (BattleDice == null) return 0;
                var counts = new int[7];
                int best = 0;
                foreach (int v in BattleDice.Values) if (v >= 1 && v <= 6) best = Math.Max(best, ++counts[v]);
                return best;
            }
        }

        /// <summary>葫芦及以上: a full house, double triple, or four of a kind and up.</summary>
        public bool FullHouseOrBetter
        {
            get
            {
                DicePattern? p = BattleDice?.Hand?.Pattern;
                return p == DicePattern.FullHouse || p == DicePattern.DoubleThreeKind || MaxOfAKind >= 4;
            }
        }

        private bool TeamHas1111(MemberKit1111 kit)
        {
            foreach (BattleUnit unit in units)
                if (unit.Side == BattleSide.Player && unit.Alive && KitOf(unit) == kit) return true;
            return false;
        }

        // ---------------------------------------------------------------- hooks called from the core files
        /// <summary>Round start (turn mode): 功能骰 timers (守愈 heal, 蚀毒).</summary>
        private void OnTurnRoundStart1111() => RelicRoundStart1111();

        /// <summary>A new hand: 幻纱's 幻骰生影 (odd total → phantom; even → it fades into a one-hit guard).</summary>
        private void OnNewHand1111()
        {
            if (BattleDice?.Hand == null || Outcome != BattleOutcome.Ongoing) return;
            int total = 0;
            foreach (int v in BattleDice.Values) total += v;
            foreach (BattleUnit unit in units)
            {
                if (!unit.Alive || KitOf(unit) != MemberKit1111.Huansha) continue;
                KitState1111 s = Kit1111(unit);
                if (total % 2 == 1) { if (s.PhantomHp <= 0) SpawnPhantom1111(unit); }
                else if (s.PhantomHp > 0)
                {
                    s.PhantomHp = 0;
                    Emit(BattleEventKind.Buff, unit.Id, unit.Id, PhantomPopSkillId, 0, false);
                    GuardOne1111(unit);
                }
            }
        }

        /// <summary>A flat bar grant (turn mode's bars otherwise follow the formation clock only).</summary>
        private void AddEnergy1111(BattleUnit unit, int amount)
        {
            if (amount <= 0 || unit == null || !unit.Alive || unit.Side != BattleSide.Player ||
                !performerClocks.TryGetValue(unit.Id, out PerformerClock clock) || clock.ReleasingUltimate) return;
            clock.Energy = Math.Min(MaxUnitEnergy, clock.Energy + amount);
        }

        private void GuardOne1111(BattleUnit unit)
        {
            PerformerClock clock = performerClocks[unit.Id];
            if (IsTurnBased) clock.GuardBlocks = Math.Max(1, clock.GuardBlocks);
            else clock.TacticalGuardUntil = Math.Max(clock.TacticalGuardUntil, ElapsedMilliseconds + 3000);
            Emit(BattleEventKind.Buff, unit.Id, unit.Id, "rt-tactical-guard", 0, false);
        }

        private void SpawnPhantom1111(BattleUnit unit)
        {
            Kit1111(unit).PhantomHp = Math.Max(1, (int)((long)unit.MaxHp * PhantomHpPermille / 1000));
            Emit(BattleEventKind.Buff, unit.Id, unit.Id, PhantomSkillId, Kit1111(unit).PhantomHp, false);
        }

        /// <summary>Kit skills. False = the race skill runs.</summary>
        private bool TryCastKit1111(BattleUnit actor, bool big)
        {
            MemberKit1111 kit = KitOf(actor);
            if (kit == MemberKit1111.None) return false;
            PerformerClock clock = performerClocks[actor.Id];
            string skill = SkillId(clock.Race, big);
            BattleUnit target = SelectEnemy(actor);
            switch (kit)
            {
                case MemberKit1111.Huansha:
                    if (!big)
                    {
                        if (target == null) return true;
                        Emit(BattleEventKind.ActionStarted, actor.Id, target.Id, skill, 0, false);
                        if (!HasPhantom(actor)) SpawnPhantom1111(actor);
                        Deal(actor, target, skill, 600);
                        target = target.Alive ? target : SelectEnemy(actor);
                        if (target != null && HasPhantom(actor)) Deal(actor, target, PhantomEchoSkillId, 600);
                    }
                    else
                    {
                        Emit(BattleEventKind.ActionStarted, actor.Id, actor.Id, skill, 0, false);
                        int until = ElapsedMilliseconds + (IsTurnBased ? TurnRoundsMilliseconds1101(DisguiseRounds) : 5000);
                        foreach (BattleUnit ally in units)
                        {
                            if (!ally.Alive || ally.Side != BattleSide.Player) continue;
                            Kit1111(ally).DisguiseUntil = until;
                            Emit(BattleEventKind.Buff, actor.Id, ally.Id, DisguiseSkillId, DisguiseRounds, false);
                        }
                    }
                    return true;
                case MemberKit1111.Huanhuo:
                    if (!big)
                    {
                        if (target == null) return true;
                        Emit(BattleEventKind.ActionStarted, actor.Id, target.Id, skill, 0, false);
                        Deal(actor, target, skill, 1200);
                        ApplyFlaw1111(actor, target);
                        if (MaxOfAKind >= 3)
                        {
                            BattleUnit next = Neighbour1111(target);
                            if (next != null) ApplyFlaw1111(actor, next);
                        }
                    }
                    else
                    {
                        List<BattleUnit> enemies = LivingOpponents(actor);
                        if (enemies.Count == 0) return true;
                        Emit(BattleEventKind.ActionStarted, actor.Id, enemies[0].Id, skill, 0, false);
                        foreach (BattleUnit enemy in enemies) Deal(actor, enemy, skill, 1200);
                        foreach (BattleUnit enemy in enemies) Confuse1111(actor, enemy);
                    }
                    return true;
                case MemberKit1111.Linyin:
                    if (target == null) return true;
                    Emit(BattleEventKind.ActionStarted, actor.Id, target.Id, skill, 0, false);
                    if (!big)
                    {
                        Deal(actor, target, skill, 1150, 150);
                        StripShield1111(actor, target, false);
                    }
                    else
                    {
                        Deal(actor, target, skill, 1800, 500);
                        StripShield1111(actor, target, true);
                        if (target.Alive)
                        {
                            Kit1111(target).DefenseDownUntil = ElapsedMilliseconds +
                                (IsTurnBased ? TurnRoundsMilliseconds1101(2) : 4000);
                            Emit(BattleEventKind.Buff, actor.Id, target.Id, "rt-armor-break", 2, false);
                        }
                    }
                    return true;
            }
            return false;
        }

        private BattleUnit Neighbour1111(BattleUnit target)
        {
            BattleUnit best = null;
            foreach (BattleUnit unit in units)
            {
                if (unit == target || !unit.Alive || unit.Side != target.Side) continue;
                int d = Math.Abs(unit.Row - target.Row) + Math.Abs(unit.Col - target.Col);
                if (best == null || d < Math.Abs(best.Row - target.Row) + Math.Abs(best.Col - target.Col)) best = unit;
            }
            return best;
        }

        private void ApplyFlaw1111(BattleUnit actor, BattleUnit target)
        {
            if (target == null || !target.Alive || Outcome != BattleOutcome.Ongoing) return;
            KitState1111 s = Kit1111(target);
            int now = ElapsedMilliseconds;
            if (s.FlawUntil <= now) s.FlawStacks = 0;
            s.FlawStacks = Math.Min(FlawMaxStacks, s.FlawStacks + 1);
            int rounds = FlawRounds + (MaxOfAKind >= 2 ? 1 : 0);
            int Ms(int r) => IsTurnBased ? TurnRoundsMilliseconds1101(r) : r * 1500;
            s.FlawUntil = Math.Min(now + Ms(FlawCapRounds), Math.Max(s.FlawUntil, now) + Ms(rounds));
            Emit(BattleEventKind.Buff, actor.Id, target.Id, FlawSkillId, s.FlawStacks, false);
        }

        private void Confuse1111(BattleUnit actor, BattleUnit enemy)
        {
            if (!enemy.Alive || Outcome != BattleOutcome.Ongoing) return;
            Kit1111(enemy).ConfusedUntil = ElapsedMilliseconds + (IsTurnBased ? TurnRoundsMilliseconds1101(1) : 3000);
            PerformerClock clock = performerClocks[enemy.Id];
            // 打断盾娘套盾、歌姬封技节奏: minions lose what they were charging; a boss keeps its special.
            if (!IsStageBoss(enemy) && clock.CastUntil > 0)
            {
                clock.CastUntil = 0;
                plannedSpecial1101.Remove(enemy.Id);
                Emit(BattleEventKind.Buff, actor.Id, enemy.Id, "rt-interrupt", 0, false);
            }
            Emit(BattleEventKind.Buff, actor.Id, enemy.Id, ConfuseSkillId, 1, false);
        }

        private void StripShield1111(BattleUnit actor, BattleUnit target, bool half)
        {
            if (!target.Alive || Outcome != BattleOutcome.Ongoing) return;
            KitState1111 s = Kit1111(target);
            PerformerClock clock = performerClocks[target.Id];
            int removed = 0;
            bool broke = false;
            if (s.ShieldLayers > 0)
            {
                int layers = half ? (s.ShieldLayers + 1) / 2 : 1;
                s.ShieldLayers -= layers;
                s.LayerHits = 0;
                removed += layers;
                broke = s.ShieldLayers == 0;
                Emit(BattleEventKind.Buff, actor.Id, target.Id, LayerBreakSkillId, s.ShieldLayers, false);
            }
            if (target.Shield > 0)
            {
                int cut = half ? (target.Shield + 1) / 2 : target.Shield;
                target.Shield -= cut;
                clock.TimedShield = Math.Max(0, clock.TimedShield - cut);
                removed += cut;
                broke |= target.Shield == 0;
            }
            if (clock.HardenedUntil > ElapsedMilliseconds) { clock.HardenedUntil = 0; removed = Math.Max(1, removed); }
            if (removed <= 0) return;
            Emit(BattleEventKind.Buff, actor.Id, target.Id, ShieldStripSkillId, removed, false);
            if (s.ShieldLayersMax > 0 && s.ShieldLayers == 0 && broke) OnBarrierBroken1111(actor, target);
            else if (broke) OnShieldBroken1111();
        }

        private void OnShieldBroken1111()
        {
            if (!FullHouseOrBetter || !TeamHas1111(MemberKit1111.Linyin)) return;
            foreach (BattleUnit ally in units)
                if (ally.Alive && ally.Side == BattleSide.Player) AddEnergy1111(ally, LinyinBreakEnergy);
        }

        private void OnBarrierBroken1111(BattleUnit actor, BattleUnit target)
        {
            KitState1111 s = Kit1111(target);
            s.BarrierBrokenUntil = ElapsedMilliseconds + (IsTurnBased ? TurnRoundsMilliseconds1101(BarrierBrokenRounds) : 6000);
            PerformerClock clock = performerClocks[target.Id];
            if (clock.CastUntil > 0)
            {
                clock.CastUntil = 0;
                plannedSpecial1101.Remove(target.Id);
                Emit(BattleEventKind.Buff, actor.Id, target.Id, "rt-interrupt", 0, false);
            }
            Emit(BattleEventKind.Buff, actor.Id, target.Id, BarrierBreakSkillId, BarrierBrokenRounds, false);
            OnShieldBroken1111();
        }

        /// <summary>Extra damage multiplier and pierce for a hit (破绽, 五骰易伤, 破盾易伤, 烈音骰, 凛音).</summary>
        private long AdjustDeal1111(BattleUnit actor, BattleUnit target, long raw, ref int ignoreDefense)
        {
            if (!IsRealtime) return raw;
            if (actor.Side == BattleSide.Enemy && target.Side == BattleSide.Player) return raw * EnemyHitPermille1111(actor) / 1000;
            if (actor.Side != BattleSide.Player || target.Side != BattleSide.Enemy) return raw;
            int permille = 1000 + (bladeUntil1111 > ElapsedMilliseconds ? RelicBladeAttackPermille : 0);
            if (kits1111.TryGetValue(target.Id, out KitState1111 s))
            {
                if (s.FlawUntil > ElapsedMilliseconds) permille += s.FlawStacks * FlawPermillePerStack;
                if (s.BarrierBrokenUntil > ElapsedMilliseconds) permille += BarrierBrokenBonusPermille;
                if (s.DefenseDownUntil > ElapsedMilliseconds) ignoreDefense += LinyinDefenseDownPermille;
                if (KitOf(actor) == MemberKit1111.Linyin && performerClocks[actor.Id].ReleasingUltimate &&
                    (s.ShieldLayers > 0 || target.Shield > 0)) permille += LinyinShieldedBonusPermille;
            }
            else if (KitOf(actor) == MemberKit1111.Linyin && performerClocks[actor.Id].ReleasingUltimate && target.Shield > 0)
                permille += LinyinShieldedBonusPermille;
            int kind = MaxOfAKind;
            if (kind >= 5 && TeamHas1111(MemberKit1111.Huanhuo)) permille += FiveKindVulnerablePermille;
            if (kind >= 3 && TeamHas1111(MemberKit1111.Linyin)) ignoreDefense += LinyinTeamPiercePermille;
            return raw * permille / 1000;
        }

        private bool IsAoe1111(string skill)
        {
            SkillDefinition definition = LookupSkill(skill);
            return definition != null && definition.Pattern == SkillPattern.All;
        }

        /// <summary>
        /// Runs before a hit lands. Enemy → member: 弹反, 迷惑伪装 decoys, 幻影 dodge. Anything → barrier: the barrier takes
        /// the whole hit and the health bar does not move (第三章 BOSS 护盾). True = the hit was fully handled here.
        /// </summary>
        private bool InterceptDamage1111(BattleUnit actor, BattleUnit target, string skill, ref int damage, bool critical,
            int diceMultiplierPermille)
        {
            if (!IsRealtime) return false;
            if (actor.Side == BattleSide.Enemy && target.Side == BattleSide.Player)
            {
                if (parryActor1111 == actor.Id && parryPermille1111 > 0)
                {
                    int blocked = (int)((long)damage * parryPermille1111 / 1000);
                    damage -= blocked;
                    parryReflect1111 += blocked;
                    LastParryBlocked1111 += blocked;
                    Emit(BattleEventKind.Buff, actor.Id, target.Id, ParrySkillId, blocked, false);
                    if (damage <= 0)
                    {
                        Emit(BattleEventKind.Damage, actor.Id, target.Id, skill, 0, false, 1000, 0, 0);
                        return true;
                    }
                }
                if (kits1111.TryGetValue(target.Id, out KitState1111 t))
                {
                    bool aoe = IsAoe1111(skill);
                    if (t.DisguiseUntil > ElapsedMilliseconds)
                    {
                        if (aoe)
                        {
                            t.DisguiseUntil = 0;
                            damage = Math.Max(1, damage / 2);
                            Emit(BattleEventKind.Buff, actor.Id, target.Id, PhantomPopSkillId, 0, false);
                        }
                        else if (random.NextPermille() < (IsStageBoss(actor) ? DisguiseDecoyBossPermille : DisguiseDecoyPermille))
                        {
                            t.DisguiseUntil = 0;
                            Emit(BattleEventKind.Damage, actor.Id, target.Id, skill, 0, false, 1000, 0, 0);
                            Emit(BattleEventKind.Buff, actor.Id, target.Id, DecoySkillId, 0, false);
                            PerformerClock attacker = performerClocks[actor.Id];
                            attacker.SilencedUntil = Math.Max(attacker.SilencedUntil,
                                ElapsedMilliseconds + (IsTurnBased ? TurnRoundsMilliseconds1101(1) : 1500));
                            Emit(BattleEventKind.Buff, target.Id, actor.Id, "rt-seal", 1, false);
                            return true;
                        }
                    }
                    if (t.PhantomHp > 0)
                    {
                        if (aoe)
                        {
                            t.PhantomHp = 0;
                            Emit(BattleEventKind.Buff, actor.Id, target.Id, PhantomPopSkillId, 0, false);
                        }
                        else if (random.NextPermille() < PhantomDodgePermille)
                        {
                            Emit(BattleEventKind.Damage, actor.Id, target.Id, skill, 0, false, 1000, 0, 0);
                            Emit(BattleEventKind.Buff, actor.Id, target.Id, DodgeSkillId, 0, false);
                            return true;
                        }
                    }
                }
                return false;
            }
            if (target.Side != BattleSide.Enemy || !kits1111.TryGetValue(target.Id, out KitState1111 b) || b.ShieldLayers <= 0 ||
                skill == RelicVenomSkillId) return false;
            // 顶盾: every hit grinds the layer it lands on (two hits a layer). 穿刺 (血精灵) and 毒爆 let part through; the rest stops.
            bool pierce = actor.Side == BattleSide.Player && RaceOf(actor) == CombatRace.BloodElf;
            int through = skill == "rt-poison" ? (int)((long)damage * PoisonPassPermille / 1000)
                : pierce ? (int)((long)damage * PiercePassPermille / 1000) : 0;
            HitShield1111(actor, target, b);
            if (through > 0 && target.Alive) { damage = through; return false; }
            Emit(BattleEventKind.Buff, actor.Id, target.Id, ShieldHitSkillId, b.ShieldLayers, critical);
            return true;
        }

        private void HitShield1111(BattleUnit actor, BattleUnit target, KitState1111 b)
        {
            if (b.ShieldLayers <= 0) return;
            if (++b.LayerHits < HitsPerShieldLayer) return;
            b.LayerHits = 0;
            b.ShieldLayers--;
            Emit(BattleEventKind.Buff, actor.Id, target.Id, LayerBreakSkillId, b.ShieldLayers, false);
            if (b.ShieldLayers == 0) OnBarrierBroken1111(actor, target);
        }

        /// <summary>Before an enemy acts: the boss raises its 阶段二 barrier; a confused enemy hits its own side.</summary>
        private bool EnemyTurnStart1111(BattleUnit actor)
        {
            if (!kits1111.TryGetValue(actor.Id, out KitState1111 c) || c.ConfusedUntil <= ElapsedMilliseconds) return false;
            c.ConfusedUntil = 0;
            if (IsStageBoss(actor) && random.NextPermille() >= ConfuseBossPermille) return false;
            BattleUnit victim = null;
            foreach (BattleUnit unit in units)
                if (unit != actor && unit.Alive && unit.Side == actor.Side) { victim = unit; break; }
            victim = victim ?? actor;
            Emit(BattleEventKind.ActionStarted, actor.Id, victim.Id, ConfuseHitSkillId, 0, false);
            Deal(actor, victim, ConfuseHitSkillId, victim == actor ? 500 : 1000);
            return true;
        }

        // ---------------------------------------------------------------- 弹反
        /// <summary>第三章: the enemy about to act releases a charged special that can be parried.</summary>
        public bool CanParry(BattleUnit actor)
        {
            if (!Chapter3Rules1111 || !IsTurnBased || actor == null || actor.Side != BattleSide.Enemy || !actor.Alive ||
                Outcome != BattleOutcome.Ongoing || !performerClocks.TryGetValue(actor.Id, out PerformerClock clock)) return false;
            return clock.CastUntil > 0 && clock.SilencedUntil <= ElapsedMilliseconds && !IsConfused(actor);
        }

        /// <summary>The player's timing for the coming special: Perfect blocks and reflects all, Good half, Miss nothing.</summary>
        public void ArmParry(BattleUnit actor, UltimateTimingGrade grade)
        {
            LastParryBlocked1111 = 0;
            if (actor == null) { parryActor1111 = -1; return; }
            parryActor1111 = actor.Id;
            parryPermille1111 = grade == UltimateTimingGrade.Perfect ? 1000 : grade == UltimateTimingGrade.Good ? 500 : 0;
            parryReflect1111 = 0;
        }

        private void FinishParry1111(BattleUnit actor)
        {
            if (actor == null || parryActor1111 != actor.Id) return;
            int pool = (int)Math.Min(int.MaxValue, (long)parryReflect1111 * ParryReflectPermille / 1000);
            parryActor1111 = -1; parryPermille1111 = 0; parryReflect1111 = 0;
            if (pool <= 0 || !actor.Alive || Outcome != BattleOutcome.Ongoing) return;
            BattleUnit reflector = FindUnit(leaderId);
            if (reflector == null || !reflector.Alive)
                foreach (BattleUnit unit in units) if (unit.Side == BattleSide.Player && unit.Alive) { reflector = unit; break; }
            if (reflector == null) return;
            Emit(BattleEventKind.ActionStarted, reflector.Id, actor.Id, ParryReflectSkillId, 0, false);
            ApplyActualDamage(reflector, actor, ParryReflectSkillId, pool, false);
        }

        private static void RegisterChapter3Skills1111(Dictionary<string, SkillDefinition> result)
        {
            void Add(string id, string name, string effect = SkillEffect.Damage) =>
                result.Add(id, new SkillDefinition { Id = id, Name = name, Effect = effect, Pattern = SkillPattern.Single });
            Add(PhantomEchoSkillId, "幻影模仿");
            Add(ConfuseHitSkillId, "扰乱误击");
            Add(ParryReflectSkillId, "弹反");
            Add(BarrierSkillId, "顶盾", SkillEffect.BuffAttack);
            Add(ShieldHitSkillId, "打在盾上", SkillEffect.BuffAttack);
            Add(LayerBreakSkillId, "碎盾", SkillEffect.BuffAttack);
            RegisterRelicSkills1111(Add);
            Add(BarrierBreakSkillId, "破盾！", SkillEffect.BuffAttack);
            Add(ParrySkillId, "弹反！", SkillEffect.BuffAttack);
            Add(PhantomSkillId, "幻影", SkillEffect.BuffAttack);
            Add(PhantomPopSkillId, "幻影消散", SkillEffect.BuffAttack);
            Add(DisguiseSkillId, "迷惑伪装", SkillEffect.BuffAttack);
            Add(DecoySkillId, "打中幻影", SkillEffect.BuffAttack);
            Add(DodgeSkillId, "闪避", SkillEffect.BuffAttack);
            Add(FlawSkillId, "声魂破绽", SkillEffect.BuffAttack);
            Add(ConfuseSkillId, "扰乱", SkillEffect.BuffAttack);
            Add(ShieldStripSkillId, "拆盾", SkillEffect.BuffAttack);
        }

        // ---------------------------------------------------------------- member-facing text
        public static string KitSkillDescription(MemberKit1111 kit, bool big)
        {
            switch (kit)
            {
                case MemberKit1111.Huansha: return big
                    ? "全队迷惑伪装2回合：敌人单体攻击60%打中幻影（首领30%），打中后攻击者沉默1回合；群攻会清掉幻影"
                    : "召出幻影（30%生命）并单体60%伤害，幻影再模仿一次60%；有幻影时自身25%闪避";
                case MemberKit1111.Huanhuo: return big
                    ? "全体120%伤害，扰乱1回合：敌人攻击自己人、打断蓄力；首领只有一半概率被扰乱"
                    : "单体120%伤害，挂声魂破绽：受伤+15%，最多2层，持续2回合，最长叠到4回合";
                case MemberKit1111.Linyin: return big
                    ? "单体180%穿透伤害；清掉一半护盾，防御-20%持续2回合；对有护盾目标再+30%"
                    : "单体115%伤害，无视15%防御，拆掉1层护盾（顶盾直接碎1层）";
                default: return null;
            }
        }

        public static string KitPassiveDescription(MemberKit1111 kit)
        {
            switch (kit)
            {
                case MemberKit1111.Huansha: return "幻骰生影：骰子点数合计为奇数时生成幻影；偶数时幻影消失，换一次挡伤护盾";
                case MemberKit1111.Huanhuo: return "惑骰·破绽烙印：对子及以上破绽+1回合；三骰及以上破绽扩散到旁边1个敌人；五骰全队攻击附带5%易伤";
                case MemberKit1111.Linyin: return "破骰·碎甲印记：三骰及以上全队无视防御+15%；葫芦及以上击破护盾时全队回少量能量";
                default: return null;
            }
        }

        public static string MemberSkillDescription(string memberId, CombatRace race, bool big) =>
            KitSkillDescription(KitOf(memberId), big) ?? ActiveSkillDescription(race, big);
    }
}
