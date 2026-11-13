using System.Text;
using ChoSiren.Systems.Tactics;
using ChoSiren.Systems.Presentation;

namespace ChoSiren.Panels
{
    public sealed partial class TacticsBattlePanel
    {
        private string TeamSkillRules1113()
        {
            var text = new StringBuilder("<color=#1CF2EC>【本队角色技能】</color>\n");
            foreach (BattleUnit member in battle.Units)
            {
                if (member.Side != BattleSide.Player) continue;
                int index = GameModel.IndexOfMember(member.Definition.Id);
                if (index < 0) continue;
                var definition = GameModel.Members[index];
                CombatRace race = BattleSimulator.ParseCombatRace(definition.Race);
                text.Append("\n<color=#1CF2EC>").Append(definition.Name).Append("</color>\n");
                text.Append("小技能（自动）：").Append(BattleSimulator.MemberSkillDescription(definition.Id, race, false)).Append("\n");
                text.Append("大招（充满后点卡）：").Append(BattleSimulator.MemberSkillDescription(definition.Id, race, true)).Append("\n");
                string passive = BattleSimulator.KitPassiveDescription(BattleSimulator.KitOf(definition.Id));
                if (!string.IsNullOrEmpty(passive)) text.Append("被动：").Append(passive).Append("\n");
                text.Append("担任队长时：").Append(MemberCaptainTraits.Describe(definition.Race)).Append("\n");
            }
            return text.ToString();
        }
    }
}
