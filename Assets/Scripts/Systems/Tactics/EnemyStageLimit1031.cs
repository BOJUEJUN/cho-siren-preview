using System.Collections.Generic;

namespace ChoSiren.Systems.Tactics
{
    /// <summary>
    /// 2026-10-31 (赵金: "对面的怪一定要大"): at most two minions stand on the stage at once, so each can be drawn
    /// at half the stage width. Extra enemies of a wave (or of a boss phase) queue offstage and step in, in authored
    /// order, as soon as a minion falls. A queued enemy is not spawned: it cannot act, be targeted or take damage.
    /// The boss itself never counts against, nor waits for, this limit.
    /// </summary>
    public sealed partial class BattleSimulator
    {
        public const int MaxActiveMinions = 2;

        private readonly List<int> queuedEnemies1031 = new List<int>();

        /// <summary>Enemies whose wave has begun but who wait offstage for a free minion slot, in entry order.</summary>
        public IReadOnlyList<int> QueuedEnemyIds => queuedEnemies1031;

        /// <summary>True for the boss of a boss stage (exempt from the minion limit).</summary>
        public bool IsStageBoss(BattleUnit unit) =>
            unit != null && unit.Side == BattleSide.Enemy && Stage.HasBossPhases && unit.Id == encounterLeadId;

        public int ActiveMinionCount
        {
            get
            {
                int count = 0;
                foreach (BattleUnit unit in units)
                    if (unit.Side == BattleSide.Enemy && unit.Alive && !IsStageBoss(unit)) count++;
                return count;
            }
        }

        /// <summary>Holds <paramref name="unit"/> back when the minion slots are full. Returns true if queued.</summary>
        private bool QueueIfStageFull1031(BattleUnit unit)
        {
            if (IsStageBoss(unit) || ActiveMinionCount < MaxActiveMinions) return false;
            if (!queuedEnemies1031.Contains(unit.Id)) queuedEnemies1031.Add(unit.Id);
            return true;
        }

        /// <summary>The opening wave is spawned directly; anything past the minion slots waits offstage.</summary>
        private void HoldOpeningOverflow1031()
        {
            int onStage = 0;
            foreach (BattleUnit unit in units)
            {
                if (unit.Side != BattleSide.Enemy || !unit.Spawned || IsStageBoss(unit)) continue;
                if (++onStage <= MaxActiveMinions) continue;
                unit.Spawned = false;
                queuedEnemies1031.Add(unit.Id);
            }
        }

        /// <summary>Brings queued enemies on while there is room. Returns true when any entered.</summary>
        private bool FillMinionSlots1031()
        {
            bool entered = false;
            while (queuedEnemies1031.Count > 0 && ActiveMinionCount < MaxActiveMinions)
            {
                BattleUnit next = FindUnit(queuedEnemies1031[0]);
                queuedEnemies1031.RemoveAt(0);
                if (next == null || next.Spawned) continue;
                ActivateEnemy(next);
                entered = true;
            }
            return entered;
        }
    }
}
