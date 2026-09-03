using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ChoSiren.Panels;
using ChoSiren.Systems;
using ChoSiren.Systems.Data;
using ChoSiren.Systems.Economy;
using ChoSiren.Systems.Gacha;
using ChoSiren.Systems.Story;
using ChoSiren.Systems.Tactics;
using UnityEngine;

namespace ChoSiren
{
    [Serializable]
    public sealed class MemberDefinition
    {
        public string Id;
        public string Name;
        public string Role;
        public string Rarity;
        public string ResourcePath;
        public string ThumbnailResourcePath;
        public int BasePower;

        public MemberDefinition(string id, string name, string role, string rarity, string resourcePath, int basePower,
            string thumbnailResourcePath = null)
        {
            Id = id;
            Name = name;
            Role = role;
            Rarity = rarity;
            ResourcePath = resourcePath;
            ThumbnailResourcePath = string.IsNullOrWhiteSpace(thumbnailResourcePath)
                ? resourcePath
                : thumbnailResourcePath;
            BasePower = basePower;
        }
    }

    /// <summary>Best result on a tactics stage. Stars are 0–3; 0 is never stored.</summary>
    [Serializable]
    public sealed class StageClear
    {
        public string Id = string.Empty;
        public int Stars;
    }

    /// <summary>
    /// Persisted player state (save schema v2, key <see cref="GameModel.SaveKey"/>).
    /// Member progress is authoritative in <see cref="Roster"/> (stable ids). The index lists
    /// (<see cref="UnlockedMembers"/>, <see cref="MemberLevels"/>, <see cref="Team"/>) are kept as
    /// a projection over the current <see cref="GameModel.Members"/> order so existing panels keep
    /// working; when a save carries no roster the index lists are used to build one.
    /// </summary>
    [Serializable]
    public sealed class GameSave
    {
        public int SchemaVersion = GameModel.SaveSchemaVersion;
        public int Diamonds = 10695;
        public int Gold = 17267;
        public int Stamina = 120;
        public int DailyPerformances;
        public string DailyActivityDate = string.Empty;
        public int CheckInDay = 1;
        public string LastCheckInDate = string.Empty;
        public int StoryProgress = 79;
        public List<int> UnlockedMembers = new List<int> { 0, 1, 2, 3 };
        public List<int> MemberLevels = new List<int> { 68, 64, 59, 57, 52, 49, 46, 43, 40 };
        public List<int> Team = new List<int> { 0, 1, 2, 3 };
        public int EquippedAccessory = -1;
        public bool MusicEnabled = true;
        public bool SfxEnabled = true;
        public int QualityLevel = 1;

        // ---- v2 ----
        public long StaminaRegenAnchorUnix;
        public long IdleLastClaimUnix;
        public int RecruitTickets;
        public int CostumeTickets;
        public int Shards;
        public TaskBoardState Tasks = new TaskBoardState();
        public List<GachaBannerState> Gacha = new List<GachaBannerState>();
        public List<string> StoryFlags = new List<string>();
        public List<string> CompletedStories = new List<string>();
        public List<StageClear> ClearedStages = new List<StageClear>();
        public List<string> OwnedCostumes = new List<string>();
        public MemberRosterSaveV2 Roster = new MemberRosterSaveV2();
    }

    public sealed class GameModel : IGachaService, ITaskBoardService
    {
        public const int SaveSchemaVersion = 2;
        public const string SaveKey = "ChoSiren.Save.v2";
        /// <summary>v1 index-based save. Read for migration, never deleted (kept for at least two releases).</summary>
        public const string LegacySaveKey = "ChoSiren.Save.v1";
        private const int DefaultStoryProgress = 79;
        private const string EnemyRole = "敌方";
        private static readonly GachaBannerDefinition[] EmptyBanners = Array.Empty<GachaBannerDefinition>();

        public const int RecruitCost = 800;
        public const int MaxStamina = 120;
        public const int MaxMemberLevel = 100;
        public const int DailyPerformanceGoal = 3;
        public const int PerformanceStaminaCost = 5;
        public const int PerformanceGoldReward = 520;
        public const int DailyPerformanceDiamondReward = 100;
        public const int StoryStaminaCost = 8;
        public const int StoryGoldReward = 300;
        public const int StoryDiamondReward = 20;
        public const int StoryProgressPerRun = 5;
        public const int MaxStoryProgress = 100;
        public const int TeamCapacity = 4;
        public static readonly int[] StoryStageThresholds = { 84, 89, 94, 100 };

        private static readonly MemberDefinition[] LegacyMembers =
        {
            new MemberDefinition("xingli", "星璃", "主唱", "SSR", "Art/Members/member-xingli", 9200),
            new MemberDefinition("feiyin", "绯音", "舞者", "SSR", "Art/Members/member-feiyin", 8750),
            new MemberDefinition("wubai", "雾白", "支援", "SSR", "Art/Members/member-wubai", 8340),
            new MemberDefinition("yeying", "夜莺", "主唱", "SR", "Art/Members/member-yeying", 7920),
            new MemberDefinition("yaoguang", "瑶光", "舞者", "SR", "Art/Members/member-yaoguang", 7560),
            new MemberDefinition("hupo", "琥珀", "支援", "SR", "Art/Members/member-hupo", 7210),
            new MemberDefinition("xianyue", "弦月", "主唱", "R", "Art/Members/member-xianyue", 6860),
            new MemberDefinition("chuxue", "初雪", "舞者", "R", "Art/Members/member-chuxue", 6530),
            new MemberDefinition("chengxia", "澄夏", "支援", "R", "Art/Members/member-chengxia", 6240),
        };

        private static readonly int[] LegacyDefaultLevels = { 68, 64, 59, 57, 52, 49, 46, 43, 40 };

        // Assigned by LoadMemberDefinitions; declared first so the static initializer order is explicit.
        private static MemberCatalog memberCatalog;
        public static readonly MemberDefinition[] Members = LoadMemberDefinitions();
        private static readonly Dictionary<string, int> MemberIndexById = BuildMemberIndex(Members);

        public static readonly string[] AccessoryNames = { "星轨耳返", "霓虹心链", "月桂舞鞋" };
        public static readonly int[] AccessoryPower = { 1200, 1800, 2400 };

        public GameSave Save { get; private set; }
        public event Action Changed;

        private readonly Func<DateTime> nowProvider;
        private readonly EconomyConfig economy;
        private readonly GachaManifest gacha;
        private readonly TacticsManifest tactics;
        private readonly Func<string, StoryScript> storyLoader;
        private readonly Dictionary<BattleSimulator, ulong> pendingBattles = new Dictionary<BattleSimulator, ulong>();
        private readonly Dictionary<string, StoryRunner> activeStories = new Dictionary<string, StoryRunner>(StringComparer.Ordinal);

        public GameModel() : this(() => DateTime.Now)
        {
        }

        /// <summary>Production constructor: tables come from <see cref="GameData.Repository"/> (Resources).</summary>
        public GameModel(Func<DateTime> nowProvider) : this(nowProvider, null, null, null, null)
        {
        }

        /// <summary>
        /// Injectable constructor for tests and tools. Any null table falls back to
        /// <see cref="GameData.Repository"/>; a table that is still missing there degrades to an
        /// empty manifest so the lobby keeps working when a designer commits a broken file.
        /// </summary>
        public GameModel(Func<DateTime> nowProvider, EconomyConfig economy, GachaManifest gacha,
            TacticsManifest tactics, Func<string, StoryScript> storyLoader)
        {
            this.nowProvider = nowProvider ?? throw new ArgumentNullException(nameof(nowProvider));

            GameDataRepository repository = null;
            if (economy == null || gacha == null || tactics == null || storyLoader == null)
                repository = GameData.Repository;

            this.economy = economy ?? repository?.Economy ?? new EconomyConfig();
            this.gacha = gacha ?? repository?.Gacha ?? new GachaManifest();
            this.tactics = tactics ?? repository?.Tactics ?? new TacticsManifest();
            this.storyLoader = storyLoader ?? (id =>
                repository != null && repository.TryGetStory(id, out StoryScript script, out _) ? script : null);

            Load();
        }

        // ------------------------------------------------------------------ tables

        public EconomyConfig Economy => economy;
        public GachaManifest GachaTables => gacha;
        public TacticsManifest Tactics => tactics;

        // ------------------------------------------------------------------ members (index API)

        public bool IsUnlocked(int index) => IsValidMemberIndex(index) && Save.UnlockedMembers.Contains(index);
        public bool IsInTeam(int index) => IsValidMemberIndex(index) && Save.Team.Contains(index);
        public int LevelOf(int index) => IsValidMemberIndex(index) ? Save.MemberLevels[index] : 0;
        public int PowerOf(int index) => IsValidMemberIndex(index) ? Members[index].BasePower + LevelOf(index) * 135 : 0;
        public bool DailyTaskComplete => Save.DailyPerformances >= DailyPerformanceGoal;
        public bool HasCheckedInToday => Save.LastCheckInDate == DateKey(Today);

        public int TeamPower => Save.Team
            .Where(index => index >= 0 && index < Members.Length && IsUnlocked(index))
            .Sum(PowerOf) + AccessoryBonus;

        // ------------------------------------------------------------------ members (stable-id API)

        public static string MemberIdAt(int index) => IsValidMemberIndex(index) ? Members[index].Id : null;

        public static int IndexOfMember(string memberId) =>
            !string.IsNullOrEmpty(memberId) && MemberIndexById.TryGetValue(memberId, out int index) ? index : -1;

        public bool IsUnlocked(string memberId) => IsUnlocked(IndexOfMember(memberId));
        public int LevelOf(string memberId) => LevelOf(IndexOfMember(memberId));

        public HashSet<string> UnlockedMemberIds()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (int index in Save.UnlockedMembers)
                if (IsValidMemberIndex(index)) ids.Add(Members[index].Id);
            return ids;
        }

        // ------------------------------------------------------------------ currencies

        public static string CurrencyName(string currencyId)
        {
            switch (currencyId)
            {
                case CurrencyIds.Diamond: return "星钻";
                case CurrencyIds.Gold: return "金币";
                case CurrencyIds.Stamina: return "体力";
                case CurrencyIds.RecruitTicket: return "签约券";
                case CurrencyIds.CostumeTicket: return "服装券";
                case CurrencyIds.Shard: return "碎片";
                default: return currencyId ?? string.Empty;
            }
        }

        public int Balance(string currencyId)
        {
            switch (currencyId)
            {
                case CurrencyIds.Diamond: return Save.Diamonds;
                case CurrencyIds.Gold: return Save.Gold;
                case CurrencyIds.Stamina: return Save.Stamina;
                case CurrencyIds.RecruitTicket: return Save.RecruitTickets;
                case CurrencyIds.CostumeTicket: return Save.CostumeTickets;
                case CurrencyIds.Shard: return Save.Shards;
                default: return 0;
            }
        }

        /// <summary>Deducts and persists. Stamina goes through <see cref="StaminaRegen.TrySpend"/>.</summary>
        public bool TrySpend(string currencyId, int amount)
        {
            if (amount < 0 || !CurrencyIds.IsKnown(currencyId)) return false;
            if (currencyId == CurrencyIds.Stamina)
            {
                if (!SpendStaminaInternal(amount)) return false;
            }
            else
            {
                if (!SpendInternal(currencyId, amount)) return false;
            }

            SaveState();
            return true;
        }

        /// <summary>Adds and persists. Unknown ids are treated as cosmetic items (see <see cref="GameSave.OwnedCostumes"/>).</summary>
        public void Grant(CurrencyAmount amount)
        {
            if (amount == null || amount.Amount <= 0) return;
            GrantItemInternal(amount.Currency, amount.Amount);
            SaveState();
        }

        public bool OwnsCostume(string itemId) => !string.IsNullOrEmpty(itemId) && Save.OwnedCostumes.Contains(itemId);

        // ------------------------------------------------------------------ stamina

        public int StaminaCap => economy.StaminaMax;

        public long SecondsUntilNextStamina
        {
            get
            {
                var snapshot = new StaminaSnapshot(Save.Stamina, Save.StaminaRegenAnchorUnix, 0);
                return snapshot.SecondsUntilNextPoint(NowUnix, economy.StaminaRegenSeconds, economy.StaminaMax);
            }
        }

        // ------------------------------------------------------------------ legacy recruit (过渡)

        /// <summary>
        /// 过渡接口：旧选秀页的定额签约。新的抽卡入口是 <see cref="TryPull"/>；此方法仅为让旧界面在接线完成前继续可用。
        /// </summary>
        public bool Recruit(int memberIndex, out string message)
        {
            if (!IsValidMemberIndex(memberIndex))
            {
                message = "候选人不存在";
                return false;
            }

            if (IsUnlocked(memberIndex))
            {
                message = "该成员已经签约";
                return false;
            }

            if (Save.Diamonds < RecruitCost)
            {
                message = "星钻不足，完成演出可继续获得";
                return false;
            }

            Save.Diamonds -= RecruitCost;
            UnlockMemberInternal(memberIndex);
            Report(TaskTriggers.GachaPull);
            SaveState();
            message = $"签约成功：{Members[memberIndex].Name} 已加入成员列表";
            return true;
        }

        public bool Train(int memberIndex, out string message)
        {
            if (!IsValidMemberIndex(memberIndex))
            {
                message = "成员不存在";
                return false;
            }

            if (!IsUnlocked(memberIndex))
            {
                message = "尚未签约该成员";
                return false;
            }

            int level = LevelOf(memberIndex);
            if (level >= MaxMemberLevel)
            {
                message = $"{Members[memberIndex].Name} 已达到最高等级";
                return false;
            }

            int cost = 180 + level * 12;
            if (Save.Gold < cost)
            {
                message = $"金币不足，本次训练需要 {cost:N0}";
                return false;
            }

            Save.Gold -= cost;
            Save.MemberLevels[memberIndex] = level + 1;
            Report(TaskTriggers.Train);
            SaveState();
            message = $"{Members[memberIndex].Name} 提升至等级 {level + 1}";
            return true;
        }

        public void ToggleTeamMember(int memberIndex, out string message)
        {
            if (!IsValidMemberIndex(memberIndex))
            {
                message = "成员不存在";
                return;
            }

            if (!IsUnlocked(memberIndex))
            {
                message = "尚未签约该成员";
                return;
            }

            if (Save.Team.Contains(memberIndex))
            {
                if (Save.Team.Count <= 1)
                {
                    message = "团队至少保留一名成员";
                    return;
                }

                Save.Team.Remove(memberIndex);
                SaveState();
                message = $"{Members[memberIndex].Name} 已退出当前编队";
                return;
            }

            if (Save.Team.Count >= TeamCapacity)
            {
                message = "当前编队已满，请先移除一名成员";
                return;
            }

            Save.Team.Add(memberIndex);
            SaveState();
            message = $"{Members[memberIndex].Name} 已加入当前编队";
        }

        public void AutoTeam()
        {
            Save.Team = Save.UnlockedMembers
                .OrderByDescending(PowerOf)
                .Take(TeamCapacity)
                .ToList();
            SaveState();
        }

        // ------------------------------------------------------------------ performance / legacy story / check-in

        public bool Perform(out string message)
        {
            bool stateChanged = Tick();
            if (!SpendStaminaInternal(PerformanceStaminaCost))
            {
                if (stateChanged) SaveState();
                message = "体力不足，稍后再来演出";
                return false;
            }

            Save.Gold += PerformanceGoldReward;
            Save.DailyPerformances++;
            Report(TaskTriggers.Perform);
            if (Save.DailyPerformances == DailyPerformanceGoal)
            {
                Save.Diamonds += DailyPerformanceDiamondReward;
                message = $"演出完成！金币 +{PerformanceGoldReward}，每日目标达成，星钻 +{DailyPerformanceDiamondReward}";
            }
            else
            {
                message = $"演出完成！金币 +{PerformanceGoldReward}（今日 {Save.DailyPerformances}/{DailyPerformanceGoal}）";
            }

            SaveState();
            return true;
        }

        /// <summary>
        /// Legacy chapter progression used by the old story-battle panel. The tactics flow
        /// (<see cref="StartStageBattle"/> + <see cref="SettleStageBattle"/>) advances the same
        /// <see cref="GameSave.StoryProgress"/> so both paths stay consistent.
        /// </summary>
        public bool AdvanceStory(out string message)
        {
            bool stateChanged = Tick();
            if (Save.StoryProgress >= MaxStoryProgress)
            {
                if (stateChanged) SaveState();
                message = "当前章节探索已完成";
                return false;
            }

            if (!SpendStaminaInternal(StoryStaminaCost))
            {
                if (stateChanged) SaveState();
                message = "体力不足，无法继续冒险";
                return false;
            }

            Save.Gold += StoryGoldReward;
            Save.Diamonds += StoryDiamondReward;
            AdvanceStoryProgressInternal();
            Report(TaskTriggers.BattleWin);
            SaveState();
            message = $"章节推进至 {Save.StoryProgress}%：金币 +{StoryGoldReward}，星钻 +{StoryDiamondReward}";
            return true;
        }

        public bool CheckIn(out string message)
        {
            DateTime todayDate = Today;
            string today = DateKey(todayDate);
            if (Save.LastCheckInDate == today)
            {
                message = "今天已经签到过了";
                return false;
            }

            Tick();
            if (DateTime.TryParseExact(Save.LastCheckInDate, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out DateTime previousDate) && previousDate.Date == todayDate.AddDays(-1))
            {
                Save.CheckInDay = Mathf.Max(1, Save.CheckInDay) + 1;
            }
            else
            {
                Save.CheckInDay = 1;
            }

            Save.LastCheckInDate = today;
            Save.Diamonds += 100;
            Report(TaskTriggers.CheckIn);
            SaveState();
            message = $"签到成功：连续第 {Save.CheckInDay} 天，星钻 +100";
            return true;
        }

        /// <summary>Applies day/week rollovers and stamina regeneration; call from the lobby on focus/resume.</summary>
        public void RefreshDailyState()
        {
            if (Tick()) SaveState();
        }

        // ------------------------------------------------------------------ idle income

        public IdleIncomeReport PreviewIdleIncome() => IdleIncome.Compute(Save.IdleLastClaimUnix, NowUnix, economy);

        public bool CanClaimIdleIncome => IdleIncome.CanClaim(Save.IdleLastClaimUnix, NowUnix);

        public bool ClaimIdleIncome(out string message)
        {
            long now = NowUnix;
            if (!IdleIncome.CanClaim(Save.IdleLastClaimUnix, now))
            {
                message = "舞台收益还在累积，稍后再来领取";
                return false;
            }

            Tick();
            IdleIncomeReport report = IdleIncome.Compute(Save.IdleLastClaimUnix, now, economy);
            foreach (CurrencyAmount reward in report.Rewards) GrantItemInternal(reward.Currency, reward.Amount);
            Save.IdleLastClaimUnix = now;
            Report(TaskTriggers.ClaimIdle);
            SaveState();
            message = report.Rewards.Count == 0
                ? "舞台收益已刷新，暂无可领取的资源"
                : "领取舞台收益：" + FormatRewards(report.Rewards.Select(reward => (reward.Currency, reward.Amount)));
            return true;
        }

        // ------------------------------------------------------------------ task board

        public int ClaimableTaskCount => TaskBoard.ClaimableCount(Save.Tasks, economy.Tasks);

        public List<TaskView> TaskViews(string cadence = null) => TaskBoard.Views(Save.Tasks, economy.Tasks, cadence);

        public bool TryClaimTask(string taskId, out string message)
        {
            Tick();
            if (!TaskBoard.TryClaim(Save.Tasks, economy.Tasks, taskId, out CurrencyAmount reward, out message))
                return false;

            GrantItemInternal(reward.Currency, reward.Amount);
            SaveState();
            message += $"，{CurrencyName(reward.Currency)} +{reward.Amount}";
            return true;
        }

        // ------------------------------------------------------------------ gacha (IGachaService)

        public IReadOnlyList<GachaBannerDefinition> Banners =>
            gacha?.Banners != null ? gacha.Banners : EmptyBanners;

        public GachaBannerState BannerState(string bannerId) => GachaStateOf(bannerId);

        public string ItemDisplayName(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;
            int memberIndex = IndexOfMember(itemId);
            if (memberIndex >= 0) return Members[memberIndex].Name;
            if (CurrencyIds.IsKnown(itemId)) return CurrencyName(itemId);
            if (itemId.StartsWith("costume-", StringComparison.Ordinal)) return "服装";
            if (itemId.StartsWith("accessory-", StringComparison.Ordinal)) return "饰品";
            return null;
        }

        public GachaBannerState GachaStateOf(string bannerId)
        {
            for (int index = 0; index < Save.Gacha.Count; index++)
                if (Save.Gacha[index].BannerId == bannerId) return Save.Gacha[index];
            return null;
        }

        /// <summary>
        /// Pays for and resolves <paramref name="count"/> pulls (1–10). Tickets are consumed one per
        /// pull first, the remainder is charged in the banner currency (ten-pull price when all
        /// ten are paid with currency). Character pulls unlock new members and convert duplicates
        /// to shards; costume pulls fill <see cref="GameSave.OwnedCostumes"/>.
        /// </summary>
        public bool TryPull(string bannerId, int count, ulong seed, out List<GachaPullResult> results, out string message)
        {
            results = null;
            GachaBannerDefinition banner = gacha.Find(bannerId);
            if (banner == null)
            {
                message = "卡池不存在";
                return false;
            }

            if (count <= 0 || count > GachaEngine.TenPullCount)
            {
                message = "签约次数无效";
                return false;
            }

            Tick();
            int tickets = string.IsNullOrEmpty(banner.TicketCurrency) ? 0 : Balance(banner.TicketCurrency);
            int ticketsUsed = Math.Min(tickets, count);
            int paidPulls = count - ticketsUsed;
            int currencyCost = paidPulls == 0 ? 0
                : paidPulls == GachaEngine.TenPullCount ? banner.CostTenPull
                : banner.CostPerPull * paidPulls;

            if (Balance(banner.CostCurrency) < currencyCost)
            {
                message = $"{CurrencyName(banner.CostCurrency)}不足，本次需要 {currencyCost:N0}";
                return false;
            }

            if (ticketsUsed > 0) SpendInternal(banner.TicketCurrency, ticketsUsed);
            if (currencyCost > 0) SpendInternal(banner.CostCurrency, currencyCost);

            GachaBannerState state = GachaStateOf(bannerId);
            if (state == null)
            {
                state = new GachaBannerState { BannerId = bannerId };
                Save.Gacha.Add(state);
            }

            results = GachaEngine.Pull(banner, state, new SeededRandom(seed), count);

            int shards;
            int newCount = 0;
            if (banner.Kind == GachaBannerKind.Character)
            {
                HashSet<string> owned = UnlockedMemberIds();
                shards = DuplicateConverter.Apply(gacha, owned, results);
                foreach (GachaPullResult result in results)
                {
                    if (!result.IsNew) continue;
                    newCount++;
                    int memberIndex = IndexOfMember(result.ItemId);
                    if (memberIndex >= 0) UnlockMemberInternal(memberIndex);
                }
            }
            else
            {
                var owned = new HashSet<string>(Save.OwnedCostumes, StringComparer.Ordinal);
                shards = DuplicateConverter.Apply(gacha, owned, results);
                foreach (GachaPullResult result in results)
                {
                    if (!result.IsNew) continue;
                    newCount++;
                    if (!Save.OwnedCostumes.Contains(result.ItemId)) Save.OwnedCostumes.Add(result.ItemId);
                }
            }

            Save.Shards += shards;
            Report(TaskTriggers.GachaPull, count);
            SaveState();

            string subject = banner.Kind == GachaBannerKind.Character ? "新成员" : "新服装";
            message = $"签约 {count} 次：{subject} {newCount} 项";
            if (shards > 0) message += $"，碎片 +{shards}";
            if (ticketsUsed > 0) message += $"（使用 {CurrencyName(banner.TicketCurrency)} ×{ticketsUsed}）";
            return true;
        }

        // ------------------------------------------------------------------ tactics battle

        public int StarsOf(string stageId)
        {
            for (int index = 0; index < Save.ClearedStages.Count; index++)
                if (Save.ClearedStages[index].Id == stageId) return Save.ClearedStages[index].Stars;
            return 0;
        }

        public bool IsStageCleared(string stageId) => StarsOf(stageId) > 0;

        /// <summary>
        /// Builds the party from <see cref="GameSave.Team"/> (members with a tactics unit only),
        /// front column top-down then the next column, spends the stage stamina and returns a
        /// battle ready for the UI or <see cref="BattleSimulator.AutoPlay"/>. Null on failure.
        /// </summary>
        public BattleSimulator StartStageBattle(string stageId, ulong seed, out string message)
        {
            StageDefinition stage = tactics.FindStage(stageId);
            if (stage == null)
            {
                message = "关卡不存在";
                return null;
            }

            bool stateChanged = Tick();
            if (Save.Stamina < stage.StaminaCost)
            {
                if (stateChanged) SaveState();
                message = $"体力不足，本关需要 {stage.StaminaCost} 点";
                return null;
            }

            List<PlayerUnitSetup> party = BuildParty();
            if (party.Count == 0)
            {
                if (stateChanged) SaveState();
                message = "当前编队没有可出战的成员";
                return null;
            }

            BattleSimulator battle;
            try
            {
                battle = new BattleSimulator(tactics, stage, party, new SeededRandom(seed));
            }
            catch (ArgumentException exception)
            {
                if (stateChanged) SaveState();
                message = "关卡数据有误：" + exception.Message;
                return null;
            }

            SpendStaminaInternal(stage.StaminaCost);
            pendingBattles[battle] = seed;
            SaveState();
            message = $"{stage.Name} 开始，体力 -{stage.StaminaCost}";
            return battle;
        }

        /// <summary>
        /// Settles a battle created by <see cref="StartStageBattle"/> exactly once. Victory pays
        /// gold, first-clear diamonds, drops, records the best star rating and advances the
        /// chapter like <see cref="AdvanceStory"/>. Defeat only clears the pending entry.
        /// </summary>
        public void SettleStageBattle(BattleSimulator battle, out string message)
        {
            if (battle == null || !pendingBattles.TryGetValue(battle, out ulong seed))
            {
                message = "该战斗不是由本局开始的，或已经结算";
                return;
            }

            if (battle.Outcome == BattleOutcome.Ongoing)
            {
                message = "战斗尚未结束";
                return;
            }

            pendingBattles.Remove(battle);
            if (battle.Outcome != BattleOutcome.Victory)
            {
                message = "演出失败，调整编队后再来挑战";
                return;
            }

            StageDefinition stage = battle.Stage;
            var rewards = new List<(string ItemId, int Amount)>();
            if (stage.GoldReward > 0) rewards.Add((CurrencyIds.Gold, stage.GoldReward));

            bool firstClear = !IsStageCleared(stage.Id);
            if (firstClear && stage.DiamondFirstClear > 0) rewards.Add((CurrencyIds.Diamond, stage.DiamondFirstClear));

            // Drop rolls are seeded from the battle seed so (setup, seed) still determines everything.
            List<(string ItemId, int Amount)> drops = DropResolver.Roll(stage.Drops, new SeededRandom(seed ^ 0xD1CE5EEDUL));
            rewards.AddRange(drops);

            foreach ((string itemId, int amount) in rewards) GrantItemInternal(itemId, amount);

            int stars = battle.StarRating();
            RecordStageClear(stage.Id, stars);
            AdvanceStoryProgressInternal();
            Report(TaskTriggers.BattleWin);
            SaveState();

            message = $"战斗胜利 {new string('★', stars)}：{FormatRewards(rewards)}";
            if (firstClear) message += "（首次通关）";
        }

        // ------------------------------------------------------------------ story

        public bool HasStoryFlag(string flag) => !string.IsNullOrEmpty(flag) && Save.StoryFlags.Contains(flag);
        public bool IsStoryCompleted(string storyId) => !string.IsNullOrEmpty(storyId) && Save.CompletedStories.Contains(storyId);

        /// <summary>
        /// Loads and starts a script. The runner works on a copy of <see cref="GameSave.StoryFlags"/>;
        /// flags are written back by <see cref="CompleteStory(string)"/> so abandoning a scene
        /// changes nothing.
        /// </summary>
        public bool TryStartStory(string storyId, out StoryRunner runner, out string message)
        {
            runner = null;
            StoryScript script = string.IsNullOrEmpty(storyId) ? null : storyLoader(storyId);
            if (script == null)
            {
                message = "剧情不存在";
                return false;
            }

            try
            {
                runner = new StoryRunner(script, new HashSet<string>(Save.StoryFlags, StringComparer.Ordinal));
                runner.Start();
            }
            catch (ArgumentException exception)
            {
                runner = null;
                message = "剧情脚本有误：" + exception.Message;
                return false;
            }

            activeStories[storyId] = runner;
            message = script.Title;
            return true;
        }

        public void CompleteStory(string storyId)
        {
            if (string.IsNullOrEmpty(storyId)) return;
            if (activeStories.TryGetValue(storyId, out StoryRunner runner))
            {
                activeStories.Remove(storyId);
                Save.StoryFlags = runner.Flags.Distinct(StringComparer.Ordinal).OrderBy(flag => flag, StringComparer.Ordinal).ToList();
            }

            if (!Save.CompletedStories.Contains(storyId)) Save.CompletedStories.Add(storyId);
            Report(TaskTriggers.ReadStory);
            SaveState();
        }

        public void CompleteStory(StoryRunner runner)
        {
            if (runner == null) return;
            string id = runner.Script.Id;
            if (!activeStories.ContainsKey(id)) activeStories[id] = runner;
            CompleteStory(id);
        }

        // ------------------------------------------------------------------ accessories / settings

        public void EquipAccessory(int index)
        {
            EquipAccessory(index, out _);
        }

        public bool EquipAccessory(int index, out string message)
        {
            if (index < 0 || index >= AccessoryNames.Length)
            {
                message = "饰品不存在";
                return false;
            }

            Save.EquippedAccessory = Save.EquippedAccessory == index ? -1 : index;
            SaveState();
            message = Save.EquippedAccessory == index ? "饰品已装备" : "饰品已卸下";
            return true;
        }

        public void ToggleMusic()
        {
            Save.MusicEnabled = !Save.MusicEnabled;
            SaveState();
        }

        public void ToggleSfx()
        {
            Save.SfxEnabled = !Save.SfxEnabled;
            SaveState();
        }

        public void ToggleQuality()
        {
            Save.QualityLevel = Save.QualityLevel == 1 ? 0 : 1;
            ApplyQualitySetting();
            SaveState();
        }

        /// <summary>Starts a fresh v2 save. The v1 key is left untouched; a fresh v2 shadows it.</summary>
        public void Reset()
        {
            PlayerPrefs.DeleteKey(SaveKey);
            pendingBattles.Clear();
            activeStories.Clear();
            Save = CreateDefault();
            InitializeSession();
            SaveState();
        }

        // ------------------------------------------------------------------ load / migrate

        private void Load()
        {
            string v2 = PlayerPrefs.GetString(SaveKey, string.Empty);
            if (!string.IsNullOrEmpty(v2))
            {
                try
                {
                    Save = JsonUtility.FromJson<GameSave>(v2) ?? CreateDefault();
                }
                catch
                {
                    Save = CreateDefault();
                }
            }
            else
            {
                Save = LoadLegacyOrDefault();
            }

            // A roster (stable ids) wins over index lists; saves written without one (tests, hand
            // edits) fall back to the index lists, which Normalize turns into a roster.
            if (Save.Roster != null && Save.Roster.Members != null && Save.Roster.Members.Count > 0)
                ApplyRosterToIndices();

            InitializeSession();
            Report(TaskTriggers.Login);
            Normalize();
            Persist();
        }

        private static GameSave LoadLegacyOrDefault()
        {
            string v1 = PlayerPrefs.GetString(LegacySaveKey, string.Empty);
            if (string.IsNullOrEmpty(v1)) return CreateDefault();

            try
            {
                GameSave legacy = JsonUtility.FromJson<GameSave>(v1) ?? CreateDefault();
                if (!v1.Contains("\"StoryProgress\"")) legacy.StoryProgress = DefaultStoryProgress;
                legacy.SchemaVersion = SaveSchemaVersion;
                legacy.Roster = MigrateLegacyRoster(legacy);
                return legacy;
            }
            catch
            {
                return CreateDefault();
            }
        }

        private static MemberRosterSaveV2 MigrateLegacyRoster(GameSave legacy)
        {
            if (memberCatalog != null)
            {
                try
                {
                    return MemberSaveMigration.FromLegacy(legacy, Members, memberCatalog);
                }
                catch (ArgumentException)
                {
                    // Fall through to the index-based conversion below.
                }
            }

            var roster = new MemberRosterSaveV2();
            var unlocked = new HashSet<int>(legacy.UnlockedMembers ?? new List<int>());
            for (int index = 0; index < Members.Length; index++)
            {
                int level = legacy.MemberLevels != null && index < legacy.MemberLevels.Count
                    ? Mathf.Clamp(legacy.MemberLevels[index], 1, MaxMemberLevel)
                    : DefaultLevel(index);
                roster.Members.Add(new MemberProgressV2
                {
                    MemberId = Members[index].Id,
                    Level = level,
                    Unlocked = unlocked.Contains(index)
                });
            }

            foreach (int index in legacy.Team ?? new List<int>())
            {
                if (!IsValidMemberIndex(index) || !unlocked.Contains(index)) continue;
                if (!roster.TeamMemberIds.Contains(Members[index].Id)) roster.TeamMemberIds.Add(Members[index].Id);
                if (roster.TeamMemberIds.Count == TeamCapacity) break;
            }

            return roster;
        }

        /// <summary>Roster (stable ids) → index lists over the current <see cref="Members"/> order.</summary>
        private void ApplyRosterToIndices()
        {
            var byId = new Dictionary<string, MemberProgressV2>(StringComparer.Ordinal);
            foreach (MemberProgressV2 progress in Save.Roster.Members)
            {
                if (progress == null || string.IsNullOrEmpty(progress.MemberId) || byId.ContainsKey(progress.MemberId)) continue;
                byId.Add(progress.MemberId, progress);
            }

            var levels = new List<int>(Members.Length);
            var unlocked = new List<int>();
            for (int index = 0; index < Members.Length; index++)
            {
                if (byId.TryGetValue(Members[index].Id, out MemberProgressV2 progress))
                {
                    levels.Add(Mathf.Clamp(progress.Level, 1, MaxMemberLevel));
                    if (progress.Unlocked) unlocked.Add(index);
                }
                else
                {
                    levels.Add(DefaultLevel(index));
                    if (DefaultUnlocked(index)) unlocked.Add(index);
                }
            }

            var team = new List<int>();
            foreach (string id in Save.Roster.TeamMemberIds ?? new List<string>())
            {
                int index = IndexOfMember(id);
                if (index < 0 || !unlocked.Contains(index) || team.Contains(index)) continue;
                team.Add(index);
                if (team.Count == TeamCapacity) break;
            }

            Save.MemberLevels = levels;
            Save.UnlockedMembers = unlocked;
            Save.Team = team;
        }

        /// <summary>Index lists → roster. Entries for members no longer in <see cref="Members"/> are preserved.</summary>
        private void SyncRosterFromIndices()
        {
            Save.Roster ??= new MemberRosterSaveV2();
            var known = new HashSet<string>(Members.Select(member => member.Id), StringComparer.Ordinal);
            var kept = (Save.Roster.Members ?? new List<MemberProgressV2>())
                .Where(progress => progress != null && !string.IsNullOrEmpty(progress.MemberId) && !known.Contains(progress.MemberId))
                .ToList();

            var roster = new List<MemberProgressV2>(Members.Length + kept.Count);
            for (int index = 0; index < Members.Length; index++)
            {
                roster.Add(new MemberProgressV2
                {
                    MemberId = Members[index].Id,
                    Level = Save.MemberLevels[index],
                    Unlocked = Save.UnlockedMembers.Contains(index)
                });
            }

            roster.AddRange(kept);
            Save.Roster.SchemaVersion = 2;
            Save.Roster.Members = roster;
            Save.Roster.TeamMemberIds = Save.Team.Select(index => Members[index].Id).ToList();
        }

        /// <summary>Everything that must happen once per constructed model or after <see cref="Reset"/>.</summary>
        private void InitializeSession()
        {
            long now = NowUnix;
            Normalize();
            if (Save.StaminaRegenAnchorUnix <= 0) Save.StaminaRegenAnchorUnix = now;
            if (Save.IdleLastClaimUnix <= 0) Save.IdleLastClaimUnix = now;
            Tick();
            ApplyQualitySetting();
        }

        /// <summary>Lazy time-driven updates. Returns true when anything changed so callers can persist.</summary>
        private bool Tick()
        {
            bool changed = EnsureDailyState();
            changed |= ApplyStaminaRegen();
            changed |= TaskBoard.Refresh(Save.Tasks, economy.Tasks, nowProvider());
            return changed;
        }

        private bool ApplyStaminaRegen()
        {
            long now = NowUnix;
            StaminaSnapshot snapshot = StaminaRegen.Apply(Save.Stamina, Save.StaminaRegenAnchorUnix, now, economy);
            bool changed = snapshot.Stamina != Save.Stamina || snapshot.LastRegenUnixSeconds != Save.StaminaRegenAnchorUnix;
            Save.Stamina = snapshot.Stamina;
            Save.StaminaRegenAnchorUnix = snapshot.LastRegenUnixSeconds;
            return changed;
        }

        private bool SpendStaminaInternal(int cost)
        {
            if (!StaminaRegen.TrySpend(Save.Stamina, cost, Save.StaminaRegenAnchorUnix, NowUnix, economy.StaminaMax,
                    out StaminaSnapshot snapshot))
                return false;

            Save.Stamina = snapshot.Stamina;
            Save.StaminaRegenAnchorUnix = snapshot.LastRegenUnixSeconds;
            if (cost > 0) Report(TaskTriggers.SpendStamina, cost);
            return true;
        }

        private bool SpendInternal(string currencyId, int amount)
        {
            if (amount < 0 || Balance(currencyId) < amount) return false;
            switch (currencyId)
            {
                case CurrencyIds.Diamond: Save.Diamonds -= amount; return true;
                case CurrencyIds.Gold: Save.Gold -= amount; return true;
                case CurrencyIds.RecruitTicket: Save.RecruitTickets -= amount; return true;
                case CurrencyIds.CostumeTicket: Save.CostumeTickets -= amount; return true;
                case CurrencyIds.Shard: Save.Shards -= amount; return true;
                case CurrencyIds.Stamina: Save.Stamina -= amount; return true;
                default: return false;
            }
        }

        private void GrantItemInternal(string itemId, int amount)
        {
            if (string.IsNullOrEmpty(itemId) || amount <= 0) return;
            switch (itemId)
            {
                case CurrencyIds.Diamond: Save.Diamonds += amount; break;
                case CurrencyIds.Gold: Save.Gold += amount; break;
                case CurrencyIds.Stamina: Save.Stamina += amount; break;
                case CurrencyIds.RecruitTicket: Save.RecruitTickets += amount; break;
                case CurrencyIds.CostumeTicket: Save.CostumeTickets += amount; break;
                case CurrencyIds.Shard: Save.Shards += amount; break;
                default:
                    // Non-currency drops (accessories, costumes) are cosmetics owned once.
                    if (!Save.OwnedCostumes.Contains(itemId)) Save.OwnedCostumes.Add(itemId);
                    break;
            }
        }

        private void UnlockMemberInternal(int memberIndex)
        {
            if (Save.UnlockedMembers.Contains(memberIndex)) return;
            Save.UnlockedMembers.Add(memberIndex);
            Save.UnlockedMembers.Sort();
        }

        private void RecordStageClear(string stageId, int stars)
        {
            stars = Mathf.Clamp(stars, 1, 3);
            for (int index = 0; index < Save.ClearedStages.Count; index++)
            {
                if (Save.ClearedStages[index].Id != stageId) continue;
                Save.ClearedStages[index].Stars = Mathf.Max(Save.ClearedStages[index].Stars, stars);
                return;
            }

            Save.ClearedStages.Add(new StageClear { Id = stageId, Stars = stars });
        }

        private void AdvanceStoryProgressInternal()
        {
            if (Save.StoryProgress >= MaxStoryProgress) return;
            // The final encounter completes the chapter in one run instead of leaving a misleading
            // 99% state that has no additional map node.
            Save.StoryProgress = Save.StoryProgress >= StoryStageThresholds[2]
                ? MaxStoryProgress
                : Mathf.Min(MaxStoryProgress, Save.StoryProgress + StoryProgressPerRun);
        }

        private List<PlayerUnitSetup> BuildParty()
        {
            var party = new List<PlayerUnitSetup>();
            int slot = 0;
            foreach (int index in Save.Team)
            {
                if (!IsValidMemberIndex(index) || !IsUnlocked(index)) continue;
                if (tactics.FindUnit(Members[index].Id) == null) continue;
                if (slot >= BattleGrid.Rows * BattleGrid.Columns) break;
                party.Add(new PlayerUnitSetup
                {
                    UnitId = Members[index].Id,
                    Row = slot % BattleGrid.Rows,
                    Col = slot / BattleGrid.Rows,
                    Level = LevelOf(index)
                });
                slot++;
            }

            return party;
        }

        private void Report(string trigger, int amount = 1) => TaskBoard.Report(Save.Tasks, economy.Tasks, trigger, amount);

        private static string FormatRewards(IEnumerable<(string ItemId, int Amount)> rewards)
        {
            var parts = new List<string>();
            foreach ((string itemId, int amount) in rewards)
            {
                if (CurrencyIds.IsKnown(itemId))
                {
                    parts.Add($"{CurrencyName(itemId)} +{amount}");
                    continue;
                }

                if (!string.IsNullOrEmpty(itemId) && itemId.StartsWith("costume-", StringComparison.Ordinal))
                    parts.Add($"服装 ×{amount}");
                else if (!string.IsNullOrEmpty(itemId) && itemId.StartsWith("accessory-", StringComparison.Ordinal))
                    parts.Add($"饰品 ×{amount}");
                else
                    parts.Add($"道具 ×{amount}");
            }

            return parts.Count == 0 ? "无" : string.Join("，", parts);
        }

        private static GameSave CreateDefault() => new GameSave();

        // ------------------------------------------------------------------ member definitions

        /// <summary>
        /// Member source priority: the 50+ member catalog (already wired), then tactics units that
        /// are not enemies merged with the legacy nine's presentation data, then the legacy nine.
        /// The first nine ids and their order never change so v1 index saves stay meaningful.
        /// </summary>
        private static MemberDefinition[] LoadMemberDefinitions()
        {
            memberCatalog = null;
            if (MemberCatalog.TryLoad(MemberCatalog.DefaultManifestResourcePath,
                    out MemberCatalog catalog, out _) && catalog.Count >= 50 && KeepsLegacyOrder(catalog))
            {
                memberCatalog = catalog;
                return catalog.ToLegacyDefinitions();
            }

            TacticsManifest tactics = TryLoadTacticsManifestQuietly();
            if (tactics != null && tactics.Units.Count > 0) return MembersFromUnits(tactics.Units);

            return LegacyMembers;
        }

        private static bool KeepsLegacyOrder(MemberCatalog catalog)
        {
            if (catalog.Count < LegacyMembers.Length) return false;
            for (int index = 0; index < LegacyMembers.Length; index++)
                if (catalog[index].Id != LegacyMembers[index].Id) return false;
            return true;
        }

        /// <summary>Reads tactics.json without going through <see cref="GameData"/> so a missing table logs nothing here.</summary>
        private static TacticsManifest TryLoadTacticsManifestQuietly()
        {
            try
            {
                TextAsset asset = Resources.Load<TextAsset>(GameDataPaths.Tactics);
                if (asset == null || string.IsNullOrWhiteSpace(asset.text)) return null;
                TacticsManifest manifest = JsonUtility.FromJson<TacticsManifest>(asset.text);
                return manifest != null && manifest.TryValidate(out _) ? manifest : null;
            }
            catch
            {
                return null;
            }
        }

        public static MemberDefinition[] MembersFromUnits(IReadOnlyList<UnitDefinition> units)
        {
            var result = new List<MemberDefinition>(LegacyMembers);
            var seen = new HashSet<string>(LegacyMembers.Select(member => member.Id), StringComparer.Ordinal);
            if (units == null) return result.ToArray();

            for (int index = 0; index < units.Count; index++)
            {
                UnitDefinition unit = units[index];
                if (unit == null || string.IsNullOrEmpty(unit.Id) || unit.Role == EnemyRole || !seen.Add(unit.Id)) continue;
                const string rarity = "R";
                result.Add(new MemberDefinition(unit.Id, unit.Name, unit.Role, rarity, "Art/Members/member-" + unit.Id,
                    MemberCatalogRules.DeterministicBasePower(unit.Id, rarity)));
            }

            return result.ToArray();
        }

        private static Dictionary<string, int> BuildMemberIndex(MemberDefinition[] members)
        {
            var index = new Dictionary<string, int>(members.Length, StringComparer.Ordinal);
            for (int position = 0; position < members.Length; position++)
                if (!index.ContainsKey(members[position].Id)) index.Add(members[position].Id, position);
            return index;
        }

        private static int DefaultLevel(int index)
        {
            if (memberCatalog != null && index < memberCatalog.Count) return memberCatalog[index].StartingLevel;
            if (index < LegacyDefaultLevels.Length) return LegacyDefaultLevels[index];
            return Mathf.Max(1, 40 - index * 2);
        }

        private static bool DefaultUnlocked(int index)
        {
            if (memberCatalog != null && index < memberCatalog.Count) return memberCatalog[index].InitiallyUnlocked;
            return index < 4;
        }

        // ------------------------------------------------------------------ normalize / persist

        private void Normalize()
        {
            Save.SchemaVersion = SaveSchemaVersion;
            Save.Diamonds = Mathf.Max(0, Save.Diamonds);
            Save.Gold = Mathf.Max(0, Save.Gold);
            Save.Stamina = Mathf.Clamp(Save.Stamina, 0, economy.StaminaMax);
            Save.RecruitTickets = Mathf.Max(0, Save.RecruitTickets);
            Save.CostumeTickets = Mathf.Max(0, Save.CostumeTickets);
            Save.Shards = Mathf.Max(0, Save.Shards);
            Save.StaminaRegenAnchorUnix = Math.Max(0, Save.StaminaRegenAnchorUnix);
            Save.IdleLastClaimUnix = Math.Max(0, Save.IdleLastClaimUnix);
            Save.DailyPerformances = Mathf.Max(0, Save.DailyPerformances);
            Save.DailyActivityDate ??= string.Empty;
            Save.CheckInDay = Mathf.Max(1, Save.CheckInDay);
            Save.LastCheckInDate ??= string.Empty;
            Save.StoryProgress = Mathf.Clamp(Save.StoryProgress, 0, MaxStoryProgress);
            Save.QualityLevel = Mathf.Clamp(Save.QualityLevel, 0, 1);
            Save.UnlockedMembers ??= new List<int> { 0, 1, 2, 3 };
            Save.MemberLevels ??= new List<int>();
            Save.Team ??= new List<int>();
            Save.Tasks ??= new TaskBoardState();
            Save.Tasks.Entries ??= new List<TaskProgress>();
            Save.Tasks.DailyKey ??= string.Empty;
            Save.Tasks.WeeklyKey ??= string.Empty;
            Save.Gacha ??= new List<GachaBannerState>();
            Save.Gacha.RemoveAll(state => state == null || string.IsNullOrEmpty(state.BannerId));
            Save.StoryFlags = CleanStrings(Save.StoryFlags);
            Save.CompletedStories = CleanStrings(Save.CompletedStories);
            Save.OwnedCostumes = CleanStrings(Save.OwnedCostumes);
            Save.ClearedStages ??= new List<StageClear>();
            Save.ClearedStages.RemoveAll(clear => clear == null || string.IsNullOrEmpty(clear.Id) || clear.Stars <= 0);
            foreach (StageClear clear in Save.ClearedStages) clear.Stars = Mathf.Clamp(clear.Stars, 1, 3);
            MigrateChapterOneStageIds();
            Save.Roster ??= new MemberRosterSaveV2();

            while (Save.MemberLevels.Count < Members.Length)
                Save.MemberLevels.Add(DefaultLevel(Save.MemberLevels.Count));

            if (Save.MemberLevels.Count > Members.Length)
                Save.MemberLevels.RemoveRange(Members.Length, Save.MemberLevels.Count - Members.Length);

            for (int index = 0; index < Save.MemberLevels.Count; index++)
                Save.MemberLevels[index] = Mathf.Clamp(Save.MemberLevels[index], 1, MaxMemberLevel);

            Save.UnlockedMembers = Save.UnlockedMembers
                .Where(index => index >= 0 && index < Members.Length)
                .Distinct()
                .OrderBy(index => index)
                .ToList();
            if (Save.UnlockedMembers.Count == 0) Save.UnlockedMembers.Add(0);

            Save.Team = Save.Team
                .Where(index => Save.UnlockedMembers.Contains(index))
                .Distinct()
                .Take(TeamCapacity)
                .ToList();
            if (Save.Team.Count == 0) Save.Team.Add(Save.UnlockedMembers[0]);

            if (Save.EquippedAccessory < -1 || Save.EquippedAccessory >= AccessoryNames.Length)
                Save.EquippedAccessory = -1;

            SyncRosterFromIndices();
        }

        private void MigrateChapterOneStageIds()
        {
            for (int index = Save.ClearedStages.Count - 1; index >= 0; index--)
            {
                StageClear legacy = Save.ClearedStages[index];
                string currentId = legacy.Id switch
                {
                    "stage-7-3" => "stage-1-1",
                    "stage-7-4" => "stage-1-2",
                    "stage-7-5" => "stage-1-3",
                    "stage-7-6" => "stage-1-4",
                    _ => legacy.Id,
                };
                if (currentId == legacy.Id) continue;

                StageClear current = Save.ClearedStages.Find(clear => clear.Id == currentId);
                if (current == null)
                {
                    legacy.Id = currentId;
                    continue;
                }

                current.Stars = Mathf.Max(current.Stars, legacy.Stars);
                Save.ClearedStages.RemoveAt(index);
            }
        }

        private static List<string> CleanStrings(List<string> values)
        {
            if (values == null) return new List<string>();
            return values.Where(value => !string.IsNullOrEmpty(value)).Distinct(StringComparer.Ordinal).ToList();
        }

        private void SaveState()
        {
            Normalize();
            Persist();
            Changed?.Invoke();
        }

        private void Persist()
        {
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(Save));
            PlayerPrefs.Save();
        }

        private DateTime Today => nowProvider().Date;

        private long NowUnix => new DateTimeOffset(nowProvider()).ToUnixTimeSeconds();

        private int AccessoryBonus => Save.EquippedAccessory >= 0 && Save.EquippedAccessory < AccessoryPower.Length
            ? AccessoryPower[Save.EquippedAccessory]
            : 0;

        private static bool IsValidMemberIndex(int index) => index >= 0 && index < Members.Length;

        private static string DateKey(DateTime date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        private bool EnsureDailyState()
        {
            string today = DateKey(Today);
            if (string.IsNullOrEmpty(Save.DailyActivityDate))
            {
                Save.DailyActivityDate = today;
                return true;
            }

            if (Save.DailyActivityDate == today) return false;

            Save.DailyActivityDate = today;
            Save.DailyPerformances = 0;
            return true;
        }

        private void ApplyQualitySetting()
        {
            int qualityCount = QualitySettings.names.Length;
            if (qualityCount <= 0)
            {
                Save.QualityLevel = 0;
                return;
            }

            Save.QualityLevel = Mathf.Clamp(Save.QualityLevel, 0, Mathf.Min(1, qualityCount - 1));
            QualitySettings.SetQualityLevel(Save.QualityLevel, true);
        }
    }
}
