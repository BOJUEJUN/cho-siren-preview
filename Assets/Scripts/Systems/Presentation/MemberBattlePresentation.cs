using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ChoSiren.Systems.Tactics;

namespace ChoSiren.Systems.Presentation
{
    /// <summary>Player-facing descriptions from the same skill definitions used by battle.</summary>
    public static class MemberBattlePresentation
    {
        public static List<SkillDefinition> FeaturedSkills(TacticsManifest manifest, string memberId)
        {
            UnitDefinition unit = manifest?.FindUnit(memberId);
            if (unit == null) return new List<SkillDefinition>();
            return unit.SkillIds.Select(manifest.FindSkill).Where(skill => skill != null)
                .OrderBy(skill => skill.Id == "strike" ? 1 : 0).Take(2).ToList();
        }

        public static string DescribeSkill(SkillDefinition skill)
        {
            if (skill == null) return "暂无技能资料";
            string area = skill.Pattern switch
            {
                SkillPattern.Row => "一行目标",
                SkillPattern.Column => "一列目标",
                SkillPattern.Plus => "十字范围目标",
                SkillPattern.All => "全体目标",
                _ => "单个目标"
            };
            string percent = (skill.PowerPermille / 10m).ToString("0.#", CultureInfo.InvariantCulture) + "%";
            string effect = skill.Effect switch
            {
                SkillEffect.Heal => $"为{area}回复攻击力 {percent} 的生命。",
                SkillEffect.Shield => $"为{area}提供其生命上限 {percent} 的护盾。",
                SkillEffect.BuffAttack => $"使{area}攻击提高 {percent}。",
                SkillEffect.DebuffDefense => $"使{area}防御降低 {percent}。",
                _ => $"对{area}造成攻击力 {percent} 的伤害。"
            };
            if (skill.Effect != SkillEffect.Damage && skill.Effect != SkillEffect.Heal && skill.Duration > 0)
                effect += $"持续 {skill.Duration} 回合。";
            effect += skill.Cooldown > 0 ? $"冷却 {skill.Cooldown} 回合。" : "无冷却。";
            return effect;
        }

        public static string Position(TacticsManifest manifest, string memberId)
        {
            var skills = FeaturedSkills(manifest, memberId);
            var effects = new List<string>();
            foreach (SkillDefinition skill in skills)
            {
                string label = skill.Effect switch
                {
                    SkillEffect.Heal => "治疗",
                    SkillEffect.Shield => "护盾",
                    SkillEffect.BuffAttack => "增益",
                    SkillEffect.DebuffDefense => "破甲",
                    _ => "输出"
                };
                if (!effects.Contains(label)) effects.Add(label);
            }
            return effects.Count == 0 ? "暂无定位" : string.Join(" / ", effects);
        }
    }
}
