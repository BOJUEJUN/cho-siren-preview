using System;
using System.Collections.Generic;

namespace ChoSiren.Systems.Tactics
{
    /// <summary>
    /// 2026-11-12 第三章 七色功能骰 (同事设计): carried in from outside the battle, dropped inside it, stackable, and at risk of
    /// shattering on a loss. They are skill dice only — the six battle dice, their hands and multipliers are untouched; each
    /// carried die is one team skill (like 应急守护), usable once a battle, at most two a round. Support only, no direct hits.
    /// </summary>
    public static class RelicDice1111
    {
        public const string Venom = "venom", Aegis = "aegis", Bloom = "bloom", Chaos = "chaos",
            Blade = "blade", Bulwark = "bulwark", Mend = "mend";
        public static readonly string[] All = { Blade, Bulwark, Bloom, Aegis, Venom, Chaos, Mend };
        public const int CarrySlots = 3;
        public const int UsesPerRound = 2;
        /// <summary>Chance a won 第三章 battle drops one die.</summary>
        public const int DropPermille = 400;
        /// <summary>Chance each carried die shatters when the battle is lost.</summary>
        public const int ShatterPermille = 500;

        public static bool IsKnown(string id) => Array.IndexOf(All, id) >= 0;

        public static string ColorName(string id)
        {
            switch (id)
            {
                case Venom: return "绿骰"; case Aegis: return "金骰"; case Bloom: return "粉骰"; case Chaos: return "紫骰";
                case Blade: return "红骰"; case Bulwark: return "银骰"; case Mend: return "黑骰"; default: return "骰子";
            }
        }

        public static string Name(string id)
        {
            switch (id)
            {
                case Venom: return "蚀毒"; case Aegis: return "护佑"; case Bloom: return "回春"; case Chaos: return "惑乱";
                case Blade: return "锋刃"; case Bulwark: return "坚壁"; case Mend: return "守愈"; default: return "普通";
            }
        }

        public static string Effect(string id)
        {
            switch (id)
            {
                case Venom: return "敌方全体挂4层毒，并且每回合按当前生命掉3%，持续4回合；毒能穿盾";
                case Aegis: return "血量最低的队友获得两层血盾，各挡一次伤害";
                case Bloom: return "回复血量最低的队友30%生命，可以和治疗、护盾叠加";
                case Chaos: return "敌方全体混乱（下次出手打自己人，首领一半概率），并致盲4回合（伤害-30%）；能穿盾";
                case Blade: return "我方攻击+10%，敌方全队攻击-20%，持续2回合";
                case Bulwark: return "全队叠2层防御，受到伤害-25%，持续2回合";
                case Mend: return "全队减伤20%并每回合回复10%生命，持续4回合";
                default: return string.Empty;
            }
        }

        /// <summary>R / SR / SSR / UR.</summary>
        public static string Rarity(string id)
        {
            switch (id)
            {
                case Blade: case Bulwark: return "R";
                case Bloom: case Aegis: return "SR";
                case Venom: case Chaos: return "SSR";
                case Mend: return "UR";
                default: return string.Empty;
            }
        }

        public static int DropWeight(string id)
        {
            switch (Rarity(id))
            {
                case "R": return 30; case "SR": return 14; case "SSR": return 5; case "UR": return 2; default: return 0;
            }
        }

        /// <summary>Weighted pick from a 0..999 roll and a second 0..999 roll.</summary>
        public static string Pick(int roll)
        {
            int total = 0;
            foreach (string id in All) total += DropWeight(id);
            int at = Math.Max(0, Math.Min(999, roll)) * total / 1000;
            foreach (string id in All)
            {
                at -= DropWeight(id);
                if (at < 0) return id;
            }
            return All[0];
        }

        /// <summary>Resources path of the colleague's gem die (7 colours × faces 1-6, cut from 「7 色骰子贴图.png」).</summary>
        public static string SpritePath(string id, int face) => $"Art/RelicDice1111/relic-{id}-{Math.Max(1, Math.Min(6, face))}";

        /// <summary>Gem colour of each die (text accents).</summary>
        public static (byte r, byte g, byte b) Tint(string id)
        {
            switch (id)
            {
                case Venom: return (86, 220, 120); case Aegis: return (255, 205, 70); case Bloom: return (255, 128, 196);
                case Chaos: return (170, 96, 255); case Blade: return (255, 72, 82); case Bulwark: return (205, 215, 230);
                case Mend: return (70, 64, 92); default: return (90, 230, 230);
            }
        }
    }

    public sealed partial class BattleSimulator
    {
        public const int RelicBladeAttackPermille = 100;
        public const int RelicBladeWeakenPermille = 200;
        public const int RelicBlindPermille = 300;
        public const int RelicBulwarkPermille = 250;
        public const int RelicMendReductionPermille = 200;
        public const string RelicVenomSkillId = "rt-relic-venom";

        private readonly List<string> relics1111 = new List<string>();
        private readonly List<bool> relicUsed1111 = new List<bool>();
        private int relicRound1111 = -1, relicUsesThisRound1111;
        private int bladeUntil1111, bulwarkUntil1111, mendUntil1111;

        public IReadOnlyList<string> RelicDice => relics1111;
        public bool RelicUsed(int slot) => slot >= 0 && slot < relicUsed1111.Count && relicUsed1111[slot];
        public int RelicUsesLeftThisRound => RelicDice1111.UsesPerRound - (relicRound1111 == TurnRound ? relicUsesThisRound1111 : 0);

        public void SetRelicDice(IReadOnlyList<string> ids)
        {
            relics1111.Clear(); relicUsed1111.Clear();
            if (ids == null) return;
            foreach (string id in ids)
            {
                if (!RelicDice1111.IsKnown(id) || relics1111.Count >= RelicDice1111.CarrySlots) continue;
                relics1111.Add(id); relicUsed1111.Add(false);
            }
        }

        public bool CanUseRelic(int slot) => IsRealtime && Outcome == BattleOutcome.Ongoing && slot >= 0 &&
            slot < relics1111.Count && !relicUsed1111[slot] && RelicUsesLeftThisRound > 0 && RelicCaster1111() != null;

        private BattleUnit RelicCaster1111()
        {
            BattleUnit leader = FindUnit(leaderId);
            if (leader != null && leader.Alive) return leader;
            foreach (BattleUnit unit in units) if (unit.Side == BattleSide.Player && unit.Alive) return unit;
            return null;
        }

        public bool TryUseRelic(int slot)
        {
            if (!CanUseRelic(slot)) return false;
            if (relicRound1111 != TurnRound) { relicRound1111 = TurnRound; relicUsesThisRound1111 = 0; }
            relicUsesThisRound1111++;
            relicUsed1111[slot] = true;
            string id = relics1111[slot];
            BattleUnit caster = RelicCaster1111();
            string skill = "rt-relic-" + id;
            Emit(BattleEventKind.ActionStarted, caster.Id, caster.Id, skill, 0, false);
            int Rounds(int n) => IsTurnBased ? TurnRoundsMilliseconds1101(n) : n * 2500;
            int now = ElapsedMilliseconds;
            switch (id)
            {
                case RelicDice1111.Venom:
                    foreach (BattleUnit enemy in LivingOpponents(caster))
                    {
                        AddPoison(enemy, caster, 4);
                        Kit1111(enemy).VenomUntil = now + Rounds(4);
                        Emit(BattleEventKind.Buff, caster.Id, enemy.Id, skill, 4, false);
                    }
                    break;
                case RelicDice1111.Aegis:
                {
                    BattleUnit ally = LowestHpRatioPlayer1111();
                    if (ally == null) break;
                    PerformerClock clock = performerClocks[ally.Id];
                    if (IsTurnBased) clock.GuardBlocks += 2;
                    else clock.TacticalGuardUntil = Math.Max(clock.TacticalGuardUntil, now + 5000);
                    Emit(BattleEventKind.Buff, caster.Id, ally.Id, "rt-tactical-guard", 2, false);
                    break;
                }
                case RelicDice1111.Bloom:
                {
                    BattleUnit ally = LowestHpRatioPlayer1111();
                    if (ally != null) HealPercent(caster, ally, skill, IsTurnBased ? 75 : 300); // turn heals ×4 → 30%
                    break;
                }
                case RelicDice1111.Chaos:
                    foreach (BattleUnit enemy in LivingOpponents(caster))
                    {
                        Confuse1111(caster, enemy);
                        Kit1111(enemy).BlindUntil = now + Rounds(4);
                        Emit(BattleEventKind.Buff, caster.Id, enemy.Id, skill, 4, false);
                    }
                    break;
                case RelicDice1111.Blade:
                    bladeUntil1111 = now + Rounds(2);
                    foreach (BattleUnit ally in units)
                        if (ally.Alive && ally.Side == BattleSide.Player) Emit(BattleEventKind.Buff, caster.Id, ally.Id, "rt-attack-up", 2, false);
                    break;
                case RelicDice1111.Bulwark:
                    bulwarkUntil1111 = now + Rounds(2);
                    foreach (BattleUnit ally in units)
                        if (ally.Alive && ally.Side == BattleSide.Player) Emit(BattleEventKind.Buff, caster.Id, ally.Id, skill, 2, false);
                    break;
                case RelicDice1111.Mend:
                    mendUntil1111 = now + Rounds(4);
                    foreach (BattleUnit ally in units)
                        if (ally.Alive && ally.Side == BattleSide.Player) Emit(BattleEventKind.Buff, caster.Id, ally.Id, skill, 4, false);
                    break;
            }
            return true;
        }

        /// <summary>自动 battles spend one carried die a round, the first that is still unused.</summary>
        public bool AutoUseRelic()
        {
            for (int slot = 0; slot < relics1111.Count; slot++)
                if (relicRound1111 != TurnRound && CanUseRelic(slot)) return TryUseRelic(slot);
            return false;
        }

        private BattleUnit LowestHpRatioPlayer1111()
        {
            BattleUnit best = null;
            foreach (BattleUnit u in units)
                if (u.Alive && u.Side == BattleSide.Player && (best == null || (long)u.Hp * best.MaxHp < (long)best.Hp * u.MaxHp)) best = u;
            return best;
        }

        /// <summary>Enemy hit on a member: 致盲, 锋刃 weakening, 坚壁 and 守愈 reduction.</summary>
        private int EnemyHitPermille1111(BattleUnit enemy)
        {
            int now = ElapsedMilliseconds, permille = 1000;
            if (IsBlinded(enemy)) permille = permille * (1000 - RelicBlindPermille) / 1000;
            if (bladeUntil1111 > now) permille = permille * (1000 - RelicBladeWeakenPermille) / 1000;
            if (bulwarkUntil1111 > now) permille = permille * (1000 - RelicBulwarkPermille) / 1000;
            if (mendUntil1111 > now) permille = permille * (1000 - RelicMendReductionPermille) / 1000;
            return permille;
        }

        private void RelicRoundStart1111()
        {
            if (Outcome != BattleOutcome.Ongoing) return;
            int now = ElapsedMilliseconds;
            BattleUnit caster = RelicCaster1111();
            if (mendUntil1111 > now && caster != null)
                foreach (BattleUnit ally in units)
                    if (ally.Alive && ally.Side == BattleSide.Player) HealPercent(caster, ally, "rt-relic-mend", IsTurnBased ? 25 : 100);
            if (caster == null) return;
            foreach (BattleUnit enemy in units)
            {
                if (!enemy.Alive || enemy.Side != BattleSide.Enemy || !kits1111.TryGetValue(enemy.Id, out KitState1111 s) ||
                    s.VenomUntil <= now || Outcome != BattleOutcome.Ongoing) continue;
                int amount = Math.Max(1, (int)((long)enemy.Hp * 30 / 1000));
                ApplyActualDamage(caster, enemy, RelicVenomSkillId, amount, false);
            }
        }

        private static void RegisterRelicSkills1111(Action<string, string, string> add)
        {
            foreach (string id in RelicDice1111.All)
                add("rt-relic-" + id, RelicDice1111.ColorName(id) + "·" + RelicDice1111.Name(id),
                    id == RelicDice1111.Bloom || id == RelicDice1111.Mend ? SkillEffect.Heal : SkillEffect.BuffAttack);
        }
    }
}
