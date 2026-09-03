using System;
using System.Collections.Generic;

namespace ChoSiren.Systems.Economy
{
    /// <summary>Currency ids shared by tasks, gacha costs, drops and shops.</summary>
    public static class CurrencyIds
    {
        public const string Diamond = "diamond";
        public const string Gold = "gold";
        public const string Stamina = "stamina";
        public const string RecruitTicket = "recruit-ticket";
        public const string CostumeTicket = "costume-ticket";
        public const string Shard = "shard";

        public static readonly string[] All =
        {
            Diamond, Gold, Stamina, RecruitTicket, CostumeTicket, Shard
        };

        public static bool IsKnown(string id) => Array.IndexOf(All, id) >= 0;
    }

    [Serializable]
    public sealed class CurrencyAmount
    {
        public string Currency = CurrencyIds.Gold;
        public int Amount;

        public CurrencyAmount()
        {
        }

        public CurrencyAmount(string currency, int amount)
        {
            Currency = currency;
            Amount = amount;
        }
    }

    /// <summary>
    /// Tunables for the retention loop. Loaded from Resources/Data/economy.json; every
    /// value here is something a designer should be able to change without a rebuild.
    /// </summary>
    [Serializable]
    public sealed class EconomyConfig
    {
        public int SchemaVersion = 1;

        public int StaminaMax = 120;
        /// <summary>Seconds per stamina point. 360 s = 1 point / 6 min = 240 per day.</summary>
        public int StaminaRegenSeconds = 360;
        public int StaminaPerTick = 1;

        /// <summary>Outpost-style passive income while the player is away.</summary>
        public int IdleGoldPerHour = 900;
        public int IdleDiamondPerHour = 6;
        public int IdleCapHours = 12;

        public List<TaskDefinition> Tasks = new List<TaskDefinition>();

        public bool TryValidate(out string error)
        {
            if (SchemaVersion != 1)
            {
                error = $"economy.json 版本不支持：{SchemaVersion}";
                return false;
            }

            if (StaminaMax <= 0 || StaminaRegenSeconds <= 0 || StaminaPerTick <= 0)
            {
                error = "体力参数必须为正数";
                return false;
            }

            if (IdleCapHours <= 0 || IdleGoldPerHour < 0 || IdleDiamondPerHour < 0)
            {
                error = "挂机产出参数无效";
                return false;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < Tasks.Count; index++)
            {
                TaskDefinition task = Tasks[index];
                if (task == null)
                {
                    error = $"第 {index + 1} 条任务为空";
                    return false;
                }

                if (!task.TryValidate(out error)) return false;
                if (!ids.Add(task.Id))
                {
                    error = $"任务 ID 重复：{task.Id}";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }
    }
}
