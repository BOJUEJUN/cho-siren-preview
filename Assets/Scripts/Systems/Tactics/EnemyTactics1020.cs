using System;
using System.Collections.Generic;

namespace ChoSiren.Systems.Tactics
{
    /// <summary>
    /// Turn-battle enemy roles (owner 2026-10-20, design deck "四阶敌人 / 集火 / 亡命冲锋"):
    /// · 霓虹巡游 neon-scout — glass cannon: hunts the weakest member, +50% damage below half HP.
    /// · 回声无人机 echo-drone — its special leaves an echo mark on the target.
    /// · 噪声幽影 noise-wraith — combo: double damage on an echo-marked target (consumes the mark).
    /// · 脉冲女卫 pulse-guard — support: shields the most hurt enemy (the chapter-2 role, now everywhere).
    /// · 绯幕咒歌 velvet-hexer — healer; poison on her target halves the heal (the demon counter).
    /// · 集火 — with three or more enemies alive, basic attacks focus the most hurt member.
    /// </summary>
    public sealed partial class BattleSimulator
    {
        public const string EchoMarkSkillId = "rt-echo-mark";
        private readonly Dictionary<int, int> echoMarkedUntilRound = new Dictionary<int, int>();

        public bool IsEchoMarked(BattleUnit unit) => unit != null &&
            echoMarkedUntilRound.TryGetValue(unit.Id, out int until) && TurnRound <= until;

        /// <summary>Enemy target choice in turn mode; null keeps the default front-row rule.</summary>
        private BattleUnit TurnEnemyTarget(BattleUnit actor)
        {
            if (!IsTurnBased || actor == null || actor.Side != BattleSide.Enemy) return null;
            var players = LivingOpponents(actor);
            if (players.Count == 0) return null;
            if (actor.Definition.Id == "noise-wraith")
                foreach (BattleUnit p in players) if (IsEchoMarked(p)) return p;
            int livingEnemies = 0;
            foreach (BattleUnit u in units) if (u.Side == BattleSide.Enemy && u.Alive) livingEnemies++;
            if (actor.Definition.Id != "neon-scout" && livingEnemies < 3) return ThreatTarget(players);
            BattleUnit weakest = players[0];
            foreach (BattleUnit p in players)
                if ((long)p.Hp * weakest.MaxHp < (long)weakest.Hp * p.MaxHp) weakest = p;
            return weakest;
        }

        // 2026-10-24 (owner: "攻击是固定先攻击队长吗…感觉很奇怪"): enemies used to hit the lowest-HP front member,
        // which was always the captain. Now they go after whoever is stealing the show: 仇恨 (threat) builds from
        // damage dealt and healing/shields given, fades by a third each round, and the target is drawn by weight.
        // The captain stands centre stage (+30%); a guarded member draws more (she is taunting).
        private readonly Dictionary<int, long> threat = new Dictionary<int, long>();
        private int threatRound;

        /// <summary>Current 仇恨 of a member (for the panel's "被盯上" hint).</summary>
        public long ThreatOf(BattleUnit unit) => unit != null && threat.TryGetValue(unit.Id, out long t) ? t : 0;

        private void TrackThreat(BattleEventKind kind, int actorId, int targetId, int amount)
        {
            if (!IsTurnBased || amount <= 0) return;
            if (threatRound != TurnRound)
            {
                threatRound = TurnRound;
                var keys = new List<int>(threat.Keys);
                foreach (int key in keys) threat[key] = threat[key] * 2 / 3;
            }
            BattleUnit actor = FindUnit(actorId);
            if (actor == null || actor.Side != BattleSide.Player) return;
            long add = kind == BattleEventKind.Damage ? amount
                : kind == BattleEventKind.Heal || kind == BattleEventKind.Shield ? amount * 3L / 2 : 0;
            if (add <= 0) return;
            threat[actorId] = ThreatOf(actor) + add;
        }

        /// <summary>The most 仇恨 is likeliest, never certain: everyone keeps a base share.</summary>
        private BattleUnit ThreatTarget(List<BattleUnit> players)
        {
            long total = 0;
            var weights = new long[players.Count];
            long average = 0;
            foreach (BattleUnit p in players) average += ThreatOf(p);
            average = Math.Max(100, average / players.Count);
            for (int i = 0; i < players.Count; i++)
            {
                long w = average + ThreatOf(players[i]) * 2;
                if (players[i].Id == CurrentLeaderId) w = w * 13 / 10;
                if (GuardRemaining(players[i]) > 0) w = w * 15 / 10;
                weights[i] = Math.Max(1, w);
                total += weights[i];
            }
            long pick = random.NextPermille() * total / 1000;
            for (int i = 0; i < players.Count; i++)
            {
                if (pick < weights[i]) return players[i];
                pick -= weights[i];
            }
            return players[players.Count - 1];
        }

        /// <summary>Extra damage scale for enemy roles (permille).</summary>
        private int TurnEnemyRolePermille(BattleUnit actor, BattleUnit target)
        {
            if (!IsTurnBased || actor == null || actor.Side != BattleSide.Enemy) return 1000;
            int scale = 1000;
            if (actor.Definition.Id == "neon-scout" && (long)actor.Hp * 2 < actor.MaxHp) scale = scale * 1500 / 1000;
            if (actor.Definition.Id == "noise-wraith" && IsEchoMarked(target))
            {
                echoMarkedUntilRound.Remove(target.Id);
                scale = scale * 2000 / 1000;
            }
            return scale;
        }

        private void MarkEcho(BattleUnit actor, BattleUnit target)
        {
            if (!IsTurnBased || target == null || !target.Alive) return;
            echoMarkedUntilRound[target.Id] = TurnRound + 2;
            Emit(BattleEventKind.Buff, actor.Id, target.Id, EchoMarkSkillId, 0, false);
        }

        /// <summary>Hexer heals are halved on a poisoned target (demon captains counter healers).</summary>
        private int TurnHealCounterPermille(BattleUnit actor, BattleUnit target) =>
            IsTurnBased && actor != null && actor.Side == BattleSide.Enemy && PoisonStacks(target) > 0 ? 500 : 1000;
    }
}
