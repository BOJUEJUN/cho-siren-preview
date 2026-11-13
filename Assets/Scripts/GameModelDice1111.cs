using System;
using System.Collections.Generic;
using System.Linq;
using ChoSiren.Systems.Tactics;

namespace ChoSiren
{
    public sealed partial class GameSave
    {
        /// <summary>2026-11-12 七色功能骰: owned dice, one entry per die (same colour stacks).</summary>
        public List<string> RelicInventory1111 = new List<string>();
        /// <summary>Dice carried into 第三章 battles (at most <see cref="RelicDice1111.CarrySlots"/>).</summary>
        public List<string> RelicCarry1111 = new List<string>();
        public bool RelicStarterGiven1111;
    }

    /// <summary>
    /// 2026-11-12 七色功能骰 (同事第三章设计): unlocked with 第三章 (2-20 cleared), carried from the homepage entry, used in
    /// 第三章 battles only. A won 第三章 battle drops one die 40% of the time; a lost one shatters each carried die at 50%.
    /// </summary>
    public sealed partial class GameModel
    {
        /// <summary>0..999 rolls for drops / shattering (tests replace it).</summary>
        public Func<int> RelicRoll1111 = () => UnityEngine.Random.Range(0, 1000);

        public bool RelicDiceUnlocked => IsStageCleared("stage-2-20");

        public int RelicCount(string id) => Save.RelicInventory1111?.Count(x => x == id) ?? 0;

        /// <summary>Two starter dice (锋刃 + 坚壁) the first time the entry opens, so the system can be tried at once.</summary>
        public void EnsureRelicStarter()
        {
            if (!RelicDiceUnlocked || Save.RelicStarterGiven1111) return;
            Save.RelicStarterGiven1111 = true;
            Save.RelicInventory1111.Add(RelicDice1111.Blade);
            Save.RelicInventory1111.Add(RelicDice1111.Bulwark);
            SaveState();
        }

        /// <summary>The carried dice that are still owned (a colour can be carried as many times as it is owned).</summary>
        public IReadOnlyList<string> CarriedRelics
        {
            get
            {
                var result = new List<string>();
                var used = new Dictionary<string, int>();
                foreach (string id in Save.RelicCarry1111 ?? new List<string>())
                {
                    if (!RelicDice1111.IsKnown(id) || result.Count >= RelicDice1111.CarrySlots) continue;
                    used.TryGetValue(id, out int n);
                    if (n >= RelicCount(id)) continue;
                    used[id] = n + 1;
                    result.Add(id);
                }
                return result;
            }
        }

        public bool CarryRelic(string id, out string message)
        {
            message = null;
            if (!RelicDiceUnlocked) { message = "通关 2-20 后开放功能骰"; return false; }
            if (!RelicDice1111.IsKnown(id)) { message = "没有这种骰子"; return false; }
            var carried = CarriedRelics.ToList();
            if (carried.Count >= RelicDice1111.CarrySlots) { message = $"最多带 {RelicDice1111.CarrySlots} 颗"; return false; }
            if (carried.Count(x => x == id) >= RelicCount(id)) { message = RelicDice1111.ColorName(id) + "不够了"; return false; }
            carried.Add(id);
            Save.RelicCarry1111 = carried;
            SaveState();
            return true;
        }

        public bool UncarryRelic(int slot)
        {
            var carried = CarriedRelics.ToList();
            if (slot < 0 || slot >= carried.Count) return false;
            carried.RemoveAt(slot);
            Save.RelicCarry1111 = carried;
            SaveState();
            return true;
        }

        /// <summary>第三章 battles take the carried dice; earlier chapters have none (前两章没有多骰玩法).</summary>
        public void ApplyRelicLoadout(BattleSimulator battle)
        {
            if (battle == null) return;
            battle.SetRelicDice(BattleSimulator.IsChapter3Stage(battle.Stage) && RelicDiceUnlocked ? CarriedRelics : null);
        }

        /// <summary>After a 第三章 battle: a win may drop a die, a loss may shatter the carried ones. Returns a line or null.</summary>
        private string SettleRelics1111(BattleSimulator battle)
        {
            if (battle == null || !BattleSimulator.IsChapter3Stage(battle.Stage) || !RelicDiceUnlocked) return null;
            if (battle.Outcome == BattleOutcome.Victory)
            {
                if (RelicRoll1111() >= RelicDice1111.DropPermille) return null;
                string id = RelicDice1111.Pick(RelicRoll1111());
                Save.RelicInventory1111.Add(id);
                SaveState();
                return $"掉落功能骰：{RelicDice1111.ColorName(id)}·{RelicDice1111.Name(id)}";
            }
            var broken = new List<string>();
            foreach (string id in battle.RelicDice)
            {
                if (RelicRoll1111() >= RelicDice1111.ShatterPermille) continue;
                Save.RelicInventory1111.Remove(id);
                broken.Add(RelicDice1111.ColorName(id) + "·" + RelicDice1111.Name(id));
            }
            if (broken.Count == 0) return null;
            Save.RelicCarry1111 = CarriedRelics.ToList();
            SaveState();
            return "功能骰碎裂：" + string.Join("、", broken);
        }
    }
}
