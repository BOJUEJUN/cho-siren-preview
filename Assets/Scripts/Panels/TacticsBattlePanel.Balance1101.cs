using ChoSiren.Systems.Tactics;

namespace ChoSiren.Panels
{
    public sealed partial class TacticsBattlePanel
    {
        /// <summary>
        /// 2026-11-01 (BossMechanics1101): a charging enemy says what is coming. The 蓄力预警 popup reads 「蓄力·禁魔」 /
        /// 「蓄力·剧毒」…; a boss also lights its own state label (蓄力 · 群体禁魔·全队封技, BossBattlePresentation.PlayCharge)
        /// and the milestone strip names the skill and its target, one turn before it lands.
        /// </summary>
        private string PresentTelegraph1101(BattleEvent entry, BattleUnit target, string label)
        {
            if (entry == null || entry.SkillId != "rt-cast" || target == null || target.Side != BattleSide.Enemy ||
                battle == null || !battle.IsTurnBased) return label;
            string intent = battle.EnemyCastIntent(target);
            if (target.Id == bossUnitId || battle.IsStageBoss(target))
            {
                string threat = battle.EnemyThreatName(target);
                bossPresentation?.PlayCharge(threat);
                Notify($"{target.Definition.Name} 蓄力「{threat}」→ {battle.EnemyCastTargetDescription(target)}");
            }
            return "蓄力·" + intent;
        }
    }
}
