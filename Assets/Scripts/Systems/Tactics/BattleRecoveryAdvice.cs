using System;
using System.Linq;
using System.Text;

namespace ChoSiren.Systems.Tactics
{
    /// <summary>Read-only post-battle guidance, using actual quotes rather than simulated upgrades.</summary>
    public static class BattleRecoveryAdvice
    {
        public const int MaximumLength = 140;

        public static string Describe(GameModel model, BattleSimulator battle)
        {
            if (model == null || battle == null) return "返回关卡查看队伍，再到成员页训练或调整配装。";
            if (battle.Outcome == BattleOutcome.Victory) return "已通关。可继续下一关，或重刷已通关关卡积累训练星光币。";
            var party = battle.Units.Where(unit => unit.Side == BattleSide.Player).ToArray();
            var text = new StringBuilder();
            if (party.Length > 0 && party.All(unit => !unit.Alive))
                text.Append("全员倒下：优先补生存、调整队长。");
            else if (battle.IsRealtime && battle.ElapsedMilliseconds >= battle.TimeLimitMilliseconds)
                text.Append("存活但超时：优先补输出。");
            else text.Append("本次未通关：先检查培养与配装。");

            if (party.Length > 0)
            {
                double meanLevel = party.Average(unit => unit.Level);
                if (meanLevel < battle.Stage.RecommendedLevel)
                    text.Append($"队均{meanLevel:0.#}级，建议{battle.Stage.RecommendedLevel}级。");
            }

            int member = party.Select(unit => GameModel.IndexOfMember(unit.Definition.Id))
                .Where(index => index >= 0 && model.IsUnlocked(index))
                .OrderBy(model.LevelOf).DefaultIfEmpty(-1).First();
            if (member >= 0)
            {
                bool affordable = model.CanTrain(member, out int cost, out _);
                if (affordable) text.Append($"训练{GameModel.Members[member].Name}需{cost:N0}星光币。");
                else if (cost > 0)
                {
                    text.Append($"训练需{cost:N0}星光币；星光币不足，");
                    text.Append(model.Save.ClearedStages.Count > 0
                        ? "重刷已通关关卡或领任务星光币。" : "先领任务星光币或挂机收益。");
                }
            }

            int bestAccessory = -1, improvement = 0;
            // Only owned items are actionable. Preview includes removing the item from
            // its previous wearer if the player chooses to transfer it to the captain.
            for (int index = 0; index < GameModel.AccessoryNames.Length; index++)
            {
                if (!model.OwnsAccessory(index)) continue;
                int delta = model.AccessoryPowerChange(index);
                if (delta <= improvement) continue;
                improvement = delta;
                bestAccessory = index;
            }
            if (bestAccessory >= 0)
                AppendIfFits(text, $"可换{GameModel.AccessoryNames[bestAccessory]}，战力+{improvement:N0}。");
            AppendIfFits(text, "招募用于补职业/队长风格，非必然更强。");
            return text.Length <= MaximumLength ? text.ToString() : text.ToString(0, MaximumLength - 1) + "…";
        }

        private static void AppendIfFits(StringBuilder text, string sentence)
        {
            if (text.Length + sentence.Length <= MaximumLength) text.Append(sentence);
        }
    }
}
