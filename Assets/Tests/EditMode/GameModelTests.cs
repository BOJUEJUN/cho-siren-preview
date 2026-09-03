using System;
using System.Collections.Generic;
using System.Linq;
using ChoSiren.Systems.Economy;
using ChoSiren.Systems.Gacha;
using ChoSiren.Systems.Story;
using ChoSiren.Systems.Tactics;
using NUnit.Framework;
using UnityEngine;

namespace ChoSiren.Tests
{
    public sealed class GameModelTests
    {
        private const string SaveKey = GameModel.SaveKey;
        private const string LegacySaveKey = GameModel.LegacySaveKey;
        private const string CharacterBanner = "test-character";
        private const string CostumeBanner = "test-costume";
        private const string EasyStage = "test-stage";
        private const string HardStage = "test-stage-hard";
        private const string StoryId = "test-story";
        private const int StageStamina = 8;
        private const int StageGold = 300;
        private const int StageDropGold = 50;
        private const int StageFirstClearDiamonds = 20;

        private DateTime now;
        private EconomyConfig economy;
        private GachaManifest gacha;
        private TacticsManifest tactics;
        private Dictionary<string, StoryScript> stories;

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.DeleteKey(LegacySaveKey);
            now = new DateTime(2026, 9, 2, 12, 0, 0, DateTimeKind.Local);
            economy = BuildEconomy();
            gacha = BuildGacha();
            tactics = BuildTactics();
            stories = new Dictionary<string, StoryScript> { { StoryId, BuildStory() } };
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.DeleteKey(LegacySaveKey);
        }

        // ------------------------------------------------------------------ existing behaviour

        [Test]
        public void NewGameCreatesAndPersistsNormalizedDefaults()
        {
            GameModel model = CreateModel();

            Assert.That(model.Save.Diamonds, Is.EqualTo(10695));
            Assert.That(model.Save.Gold, Is.EqualTo(17267));
            Assert.That(model.Save.Stamina, Is.EqualTo(GameModel.MaxStamina));
            Assert.That(model.Save.DailyActivityDate, Is.EqualTo("2026-09-02"));
            Assert.That(model.Save.StoryProgress, Is.EqualTo(79));
            Assert.That(model.Save.Team, Is.EqualTo(new[] { 0, 1, 2, 3 }));
            Assert.That(model.Save.SchemaVersion, Is.EqualTo(GameModel.SaveSchemaVersion));
            Assert.That(PlayerPrefs.HasKey(SaveKey), Is.True);
            Assert.That(PlayerPrefs.HasKey(LegacySaveKey), Is.False, "A new game must not write the v1 key.");

            GameModel reloaded = CreateModel();
            Assert.That(reloaded.Save.DailyActivityDate, Is.EqualTo("2026-09-02"));
            Assert.That(reloaded.Save.StoryProgress, Is.EqualTo(79));
            Assert.That(reloaded.Save.Team, Is.EqualTo(new[] { 0, 1, 2, 3 }));
        }

        [Test]
        public void LegacyChapterStageClearsMigrateToChapterOneIdsAndKeepBestStars()
        {
            SaveRaw(new GameSave
            {
                ClearedStages = new List<StageClear>
                {
                    new StageClear { Id = "stage-7-3", Stars = 2 },
                    new StageClear { Id = "stage-1-1", Stars = 3 },
                    new StageClear { Id = "stage-7-6", Stars = 1 },
                },
            });

            GameModel model = CreateModel();

            Assert.That(model.StarsOf("stage-1-1"), Is.EqualTo(3));
            Assert.That(model.StarsOf("stage-1-4"), Is.EqualTo(1));
            Assert.That(model.Save.ClearedStages.Any(clear => clear.Id.StartsWith("stage-7-")), Is.False);
        }

        [Test]
        public void PerformConsumesStaminaRewardsGoldAndAwardsDailyGoalOnlyOnce()
        {
            GameModel model = CreateModel();
            int initialDiamonds = model.Save.Diamonds;
            int initialGold = model.Save.Gold;
            int initialStamina = model.Save.Stamina;

            for (int count = 0; count < GameModel.DailyPerformanceGoal; count++)
                Assert.That(model.Perform(out _), Is.True);

            Assert.That(model.Save.Gold,
                Is.EqualTo(initialGold + GameModel.PerformanceGoldReward * GameModel.DailyPerformanceGoal));
            Assert.That(model.Save.Stamina,
                Is.EqualTo(initialStamina - GameModel.PerformanceStaminaCost * GameModel.DailyPerformanceGoal));
            Assert.That(model.Save.DailyPerformances, Is.EqualTo(GameModel.DailyPerformanceGoal));
            Assert.That(model.DailyTaskComplete, Is.True);
            Assert.That(model.Save.Diamonds, Is.EqualTo(initialDiamonds + GameModel.DailyPerformanceDiamondReward));

            Assert.That(model.Perform(out _), Is.True);
            Assert.That(model.Save.Diamonds, Is.EqualTo(initialDiamonds + GameModel.DailyPerformanceDiamondReward));

            GameModel reloaded = CreateModel();
            Assert.That(reloaded.Save.DailyPerformances, Is.EqualTo(GameModel.DailyPerformanceGoal + 1));
            Assert.That(reloaded.Save.Diamonds, Is.EqualTo(model.Save.Diamonds));
        }

        [Test]
        public void PerformFailsWithoutEnoughStaminaAndDoesNotReward()
        {
            GameSave save = new GameSave
            {
                Stamina = GameModel.PerformanceStaminaCost - 1,
                DailyActivityDate = "2026-09-02",
            };
            SaveRaw(save);
            GameModel model = CreateModel();
            int initialGold = model.Save.Gold;

            Assert.That(model.Perform(out _), Is.False);
            Assert.That(model.Save.Stamina, Is.EqualTo(GameModel.PerformanceStaminaCost - 1));
            Assert.That(model.Save.Gold, Is.EqualTo(initialGold));
            Assert.That(model.Save.DailyPerformances, Is.Zero);
        }

        [Test]
        public void DailyPerformanceProgressResetsAndPersistsOnANewDay()
        {
            GameModel model = CreateModel();
            Assert.That(model.Perform(out _), Is.True);
            Assert.That(model.Perform(out _), Is.True);
            Assert.That(model.Save.DailyPerformances, Is.EqualTo(2));

            now = now.AddDays(1);
            model.RefreshDailyState();

            Assert.That(model.Save.DailyActivityDate, Is.EqualTo("2026-09-03"));
            Assert.That(model.Save.DailyPerformances, Is.Zero);
            Assert.That(model.DailyTaskComplete, Is.False);
            Assert.That(CreateModel().Save.DailyPerformances, Is.Zero);
        }

        [Test]
        public void AdvanceStoryChangesRewardsProgressAndPersists()
        {
            GameModel model = CreateModel();
            int initialGold = model.Save.Gold;
            int initialDiamonds = model.Save.Diamonds;
            int initialStamina = model.Save.Stamina;

            Assert.That(model.AdvanceStory(out _), Is.True);
            Assert.That(model.Save.StoryProgress, Is.EqualTo(84));
            Assert.That(model.Save.Gold, Is.EqualTo(initialGold + GameModel.StoryGoldReward));
            Assert.That(model.Save.Diamonds, Is.EqualTo(initialDiamonds + GameModel.StoryDiamondReward));
            Assert.That(model.Save.Stamina, Is.EqualTo(initialStamina - GameModel.StoryStaminaCost));
            Assert.That(CreateModel().Save.StoryProgress, Is.EqualTo(84));
        }

        [Test]
        public void AdvanceStoryRejectsCompletedChapterOrInsufficientStamina()
        {
            GameSave completed = new GameSave { StoryProgress = GameModel.MaxStoryProgress };
            SaveRaw(completed);
            GameModel model = CreateModel();
            int completedGold = model.Save.Gold;
            Assert.That(model.AdvanceStory(out _), Is.False);
            Assert.That(model.Save.Gold, Is.EqualTo(completedGold));

            GameSave exhausted = new GameSave
            {
                StoryProgress = 79,
                Stamina = GameModel.StoryStaminaCost - 1,
            };
            SaveRaw(exhausted);
            model = CreateModel();
            int exhaustedGold = model.Save.Gold;
            Assert.That(model.AdvanceStory(out _), Is.False);
            Assert.That(model.Save.StoryProgress, Is.EqualTo(79));
            Assert.That(model.Save.Gold, Is.EqualTo(exhaustedGold));
        }

        [Test]
        public void StoryChapterCompletesAcrossAllFourMapEncounters()
        {
            GameModel model = CreateModel();

            Assert.That(model.AdvanceStory(out _), Is.True);
            Assert.That(model.Save.StoryProgress, Is.EqualTo(84));
            Assert.That(model.AdvanceStory(out _), Is.True);
            Assert.That(model.Save.StoryProgress, Is.EqualTo(89));
            Assert.That(model.AdvanceStory(out _), Is.True);
            Assert.That(model.Save.StoryProgress, Is.EqualTo(94));
            Assert.That(model.AdvanceStory(out _), Is.True);
            Assert.That(model.Save.StoryProgress, Is.EqualTo(GameModel.MaxStoryProgress));
            Assert.That(model.AdvanceStory(out _), Is.False);
        }

        [Test]
        public void CheckInCanOnlyRewardOncePerDayAndTracksConsecutiveDays()
        {
            GameModel model = CreateModel();
            int initialDiamonds = model.Save.Diamonds;

            Assert.That(model.CheckIn(out _), Is.True);
            Assert.That(model.HasCheckedInToday, Is.True);
            Assert.That(model.Save.CheckInDay, Is.EqualTo(1));
            Assert.That(model.Save.Diamonds, Is.EqualTo(initialDiamonds + 100));

            Assert.That(model.CheckIn(out _), Is.False);
            Assert.That(model.Save.Diamonds, Is.EqualTo(initialDiamonds + 100));

            now = now.AddDays(1);
            Assert.That(model.CheckIn(out _), Is.True);
            Assert.That(model.Save.CheckInDay, Is.EqualTo(2));
            Assert.That(CreateModel().Save.CheckInDay, Is.EqualTo(2));

            now = now.AddDays(2);
            Assert.That(model.CheckIn(out _), Is.True);
            Assert.That(model.Save.CheckInDay, Is.EqualTo(1));
        }

        [Test]
        public void RecruitValidatesCandidateCostAndPersistsUnlock()
        {
            GameModel model = CreateModel();
            int initialDiamonds = model.Save.Diamonds;

            Assert.That(model.Recruit(-1, out _), Is.False);
            Assert.That(model.Recruit(0, out _), Is.False);
            Assert.That(model.Recruit(4, out _), Is.True);
            Assert.That(model.IsUnlocked(4), Is.True);
            Assert.That(model.Save.Diamonds, Is.EqualTo(initialDiamonds - GameModel.RecruitCost));
            Assert.That(CreateModel().IsUnlocked(4), Is.True);

            GameSave poor = new GameSave { Diamonds = GameModel.RecruitCost - 1 };
            SaveRaw(poor);
            model = CreateModel();
            Assert.That(model.Recruit(4, out _), Is.False);
            Assert.That(model.IsUnlocked(4), Is.False);
        }

        [Test]
        public void TrainValidatesOwnershipFundsAndLevelCapAndPersistsSuccess()
        {
            GameModel model = CreateModel();
            int initialLevel = model.LevelOf(0);
            int expectedCost = 180 + initialLevel * 12;
            int initialGold = model.Save.Gold;

            Assert.That(model.Train(0, out _), Is.True);
            Assert.That(model.LevelOf(0), Is.EqualTo(initialLevel + 1));
            Assert.That(model.Save.Gold, Is.EqualTo(initialGold - expectedCost));
            Assert.That(CreateModel().LevelOf(0), Is.EqualTo(initialLevel + 1));
            Assert.That(model.Train(4, out _), Is.False);
            Assert.That(model.Train(-1, out _), Is.False);

            GameSave capped = new GameSave();
            capped.MemberLevels[0] = GameModel.MaxMemberLevel;
            SaveRaw(capped);
            model = CreateModel();
            Assert.That(model.Train(0, out _), Is.False);
            Assert.That(model.LevelOf(0), Is.EqualTo(GameModel.MaxMemberLevel));

            GameSave poor = new GameSave { Gold = 0 };
            SaveRaw(poor);
            model = CreateModel();
            Assert.That(model.Train(0, out _), Is.False);
            Assert.That(model.LevelOf(0), Is.EqualTo(68));
        }

        [Test]
        public void TeamEditingEnforcesCapacityMinimumOwnershipAndPersists()
        {
            GameModel model = CreateModel();
            Assert.That(model.Recruit(4, out _), Is.True);

            model.ToggleTeamMember(4, out _);
            Assert.That(model.IsInTeam(4), Is.False, "A full team must reject an additional member.");

            model.ToggleTeamMember(3, out _);
            model.ToggleTeamMember(4, out _);
            Assert.That(model.Save.Team.Count, Is.EqualTo(4));
            Assert.That(model.IsInTeam(4), Is.True);
            Assert.That(CreateModel().IsInTeam(4), Is.True);

            while (model.Save.Team.Count > 1)
                model.ToggleTeamMember(model.Save.Team[0], out _);

            int finalMember = model.Save.Team[0];
            model.ToggleTeamMember(finalMember, out _);
            Assert.That(model.Save.Team, Is.EqualTo(new[] { finalMember }));

            model.ToggleTeamMember(8, out _);
            Assert.That(model.Save.Team, Is.EqualTo(new[] { finalMember }));
        }

        [Test]
        public void AutoTeamSelectsFourHighestPowerUnlockedMembersAndPersists()
        {
            GameSave save = new GameSave
            {
                UnlockedMembers = new List<int> { 0, 1, 2, 3, 4 },
                Team = new List<int> { 0 },
            };
            save.MemberLevels[4] = GameModel.MaxMemberLevel;
            SaveRaw(save);
            GameModel model = CreateModel();

            model.AutoTeam();

            Assert.That(model.Save.Team.Count, Is.EqualTo(4));
            Assert.That(model.Save.Team, Does.Contain(4));
            Assert.That(model.Save.Team, Is.All.Matches<int>(index => model.IsUnlocked(index)));
            Assert.That(CreateModel().Save.Team, Is.EqualTo(model.Save.Team));
        }

        [Test]
        public void AccessoryEquipToggleChangesTeamPowerValidatesIndexAndPersists()
        {
            GameModel model = CreateModel();
            int basePower = model.TeamPower;

            Assert.That(model.EquipAccessory(-1, out _), Is.False);
            Assert.That(model.TeamPower, Is.EqualTo(basePower));
            Assert.That(model.EquipAccessory(1, out _), Is.True);
            Assert.That(model.Save.EquippedAccessory, Is.EqualTo(1));
            Assert.That(model.TeamPower, Is.EqualTo(basePower + GameModel.AccessoryPower[1]));
            Assert.That(CreateModel().Save.EquippedAccessory, Is.EqualTo(1));

            Assert.That(model.EquipAccessory(1, out _), Is.True);
            Assert.That(model.Save.EquippedAccessory, Is.EqualTo(-1));
            Assert.That(model.TeamPower, Is.EqualTo(basePower));
        }

        [Test]
        public void SettingsToggleAndResetArePersisted()
        {
            GameModel model = CreateModel();
            bool initialMusic = model.Save.MusicEnabled;
            bool initialSfx = model.Save.SfxEnabled;
            int initialQuality = model.Save.QualityLevel;

            model.ToggleMusic();
            model.ToggleSfx();
            model.ToggleQuality();

            GameModel reloaded = CreateModel();
            Assert.That(reloaded.Save.MusicEnabled, Is.EqualTo(!initialMusic));
            Assert.That(reloaded.Save.SfxEnabled, Is.EqualTo(!initialSfx));
            if (QualitySettings.names.Length > 1)
                Assert.That(reloaded.Save.QualityLevel, Is.Not.EqualTo(initialQuality));

            Assert.That(reloaded.Recruit(4, out _), Is.True);
            reloaded.Reset();
            GameModel resetReloaded = CreateModel();
            Assert.That(resetReloaded.Save.MusicEnabled, Is.True);
            Assert.That(resetReloaded.Save.SfxEnabled, Is.True);
            Assert.That(resetReloaded.Save.UnlockedMembers, Is.EqualTo(new[] { 0, 1, 2, 3 }));
            Assert.That(resetReloaded.Save.Team, Is.EqualTo(new[] { 0, 1, 2, 3 }));
            Assert.That(resetReloaded.Save.EquippedAccessory, Is.EqualTo(-1));
            Assert.That(resetReloaded.Save.StoryProgress, Is.EqualTo(79));
        }

        [Test]
        public void LoadRepairsCorruptCollectionsRangesAndAccessory()
        {
            GameSave corrupt = new GameSave
            {
                Diamonds = -10,
                Gold = -20,
                Stamina = 999,
                DailyPerformances = -3,
                CheckInDay = 0,
                StoryProgress = 500,
                UnlockedMembers = new List<int> { -1, 0, 0, 99 },
                MemberLevels = new List<int> { -5, 500 },
                Team = new List<int> { 99, 0, 0 },
                EquippedAccessory = 99,
                QualityLevel = 99,
                RecruitTickets = -4,
                Shards = -1,
                StoryFlags = null,
                Tasks = null,
                ClearedStages = new List<StageClear> { null, new StageClear { Id = "x", Stars = 0 }, new StageClear { Id = "y", Stars = 9 } },
            };
            SaveRaw(corrupt);

            GameModel model = CreateModel();

            Assert.That(model.Save.Diamonds, Is.Zero);
            Assert.That(model.Save.Gold, Is.Zero);
            Assert.That(model.Save.Stamina, Is.EqualTo(GameModel.MaxStamina));
            Assert.That(model.Save.DailyPerformances, Is.Zero);
            Assert.That(model.Save.CheckInDay, Is.EqualTo(1));
            Assert.That(model.Save.StoryProgress, Is.EqualTo(GameModel.MaxStoryProgress));
            Assert.That(model.Save.UnlockedMembers, Is.EqualTo(new[] { 0 }));
            Assert.That(model.Save.Team, Is.EqualTo(new[] { 0 }));
            Assert.That(model.Save.MemberLevels.Count, Is.EqualTo(GameModel.Members.Length));
            Assert.That(model.Save.MemberLevels[0], Is.EqualTo(1));
            Assert.That(model.Save.MemberLevels[1], Is.EqualTo(GameModel.MaxMemberLevel));
            Assert.That(model.Save.EquippedAccessory, Is.EqualTo(-1));
            Assert.That(model.Save.RecruitTickets, Is.Zero);
            Assert.That(model.Save.Shards, Is.Zero);
            Assert.That(model.Save.StoryFlags, Is.Empty);
            Assert.That(model.Save.Tasks, Is.Not.Null);
            Assert.That(model.StarsOf("x"), Is.Zero);
            Assert.That(model.StarsOf("y"), Is.EqualTo(3));
            Assert.That(model.TeamPower, Is.GreaterThan(0));

            GameModel reloaded = CreateModel();
            Assert.That(reloaded.Save.UnlockedMembers, Is.EqualTo(new[] { 0 }));
            Assert.That(reloaded.Save.EquippedAccessory, Is.EqualTo(-1));
        }

        [Test]
        public void LegacySaveWithoutNewFieldsMigratesStoryAndDailyState()
        {
            PlayerPrefs.SetString(LegacySaveKey,
                "{\"Diamonds\":50,\"Gold\":60,\"Stamina\":70,\"DailyPerformances\":2," +
                "\"CheckInDay\":1,\"UnlockedMembers\":[0],\"MemberLevels\":[10],\"Team\":[0]}");
            PlayerPrefs.Save();

            GameModel model = CreateModel();

            Assert.That(model.Save.StoryProgress, Is.EqualTo(79));
            Assert.That(model.Save.DailyActivityDate, Is.EqualTo("2026-09-02"));
            Assert.That(model.Save.DailyPerformances, Is.EqualTo(2));
            Assert.That(model.Save.MemberLevels.Count, Is.EqualTo(GameModel.Members.Length));
            Assert.That(model.Save.Stamina, Is.EqualTo(70));
            Assert.That(CreateModel().Save.StoryProgress, Is.EqualTo(79));
        }

        [Test]
        public void SuccessfulMutationRaisesChangedButRejectedMutationDoesNot()
        {
            GameModel model = CreateModel();
            int changedCount = 0;
            model.Changed += () => changedCount++;

            Assert.That(model.Recruit(-1, out _), Is.False);
            Assert.That(changedCount, Is.Zero);
            Assert.That(model.Perform(out _), Is.True);
            Assert.That(changedCount, Is.EqualTo(1));
        }

        // ------------------------------------------------------------------ v1 → v2 migration

        [Test]
        public void LegacyIndexSaveMigratesToStableIdsWithoutMixingMembersAndKeepsV1Key()
        {
            GameSave legacy = new GameSave
            {
                Diamonds = 1234,
                UnlockedMembers = new List<int> { 0, 2, 5 },
                MemberLevels = new List<int> { 70, 64, 33, 57, 52, 44, 46, 43, 40 },
                Team = new List<int> { 5, 2 },
            };
            PlayerPrefs.SetString(LegacySaveKey, JsonUtility.ToJson(legacy));
            PlayerPrefs.Save();

            GameModel model = CreateModel();

            Assert.That(model.Save.Diamonds, Is.EqualTo(1234));
            Assert.That(model.IsUnlocked(0), Is.True);
            Assert.That(model.IsUnlocked(1), Is.False);
            Assert.That(model.IsUnlocked(2), Is.True);
            Assert.That(model.IsUnlocked(5), Is.True);
            Assert.That(model.LevelOf(0), Is.EqualTo(70));
            Assert.That(model.LevelOf(2), Is.EqualTo(33));
            Assert.That(model.LevelOf(5), Is.EqualTo(44));
            Assert.That(model.Save.Team, Is.EqualTo(new[] { 5, 2 }));
            Assert.That(model.IsUnlocked("hupo"), Is.True);
            Assert.That(model.LevelOf("hupo"), Is.EqualTo(44));

            Assert.That(PlayerPrefs.HasKey(LegacySaveKey), Is.True, "The v1 save must be kept.");
            Assert.That(PlayerPrefs.HasKey(SaveKey), Is.True);

            GameSave written = JsonUtility.FromJson<GameSave>(PlayerPrefs.GetString(SaveKey));
            MemberProgressV2 hupo = written.Roster.Members.Single(member => member.MemberId == "hupo");
            Assert.That(hupo.Level, Is.EqualTo(44));
            Assert.That(hupo.Unlocked, Is.True);
            Assert.That(written.Roster.Members.Single(member => member.MemberId == "feiyin").Unlocked, Is.False);
            Assert.That(written.Roster.TeamMemberIds, Is.EqualTo(new[] { "hupo", "wubai" }));

            GameModel reloaded = CreateModel();
            Assert.That(reloaded.LevelOf(5), Is.EqualTo(44));
            Assert.That(reloaded.Save.Team, Is.EqualTo(new[] { 5, 2 }));
            Assert.That(reloaded.IsUnlocked(1), Is.False);
        }

        [Test]
        public void RosterWinsOverStaleIndexListsWhenBothArePresent()
        {
            GameSave save = new GameSave();
            save.Roster.Members.Add(new MemberProgressV2 { MemberId = "xingli", Level = 77, Unlocked = true });
            save.Roster.Members.Add(new MemberProgressV2 { MemberId = "feiyin", Level = 12, Unlocked = true });
            save.Roster.Members.Add(new MemberProgressV2 { MemberId = "wubai", Level = 5, Unlocked = false });
            save.Roster.TeamMemberIds.Add("feiyin");
            SaveRaw(save);

            GameModel model = CreateModel();

            Assert.That(model.LevelOf(0), Is.EqualTo(77));
            Assert.That(model.LevelOf(1), Is.EqualTo(12));
            Assert.That(model.IsUnlocked(2), Is.False, "Roster says wubai is locked; the default index list must not override it.");
            Assert.That(model.Save.Team, Is.EqualTo(new[] { 1 }));
        }

        [Test]
        public void LegacyMigrationOnlyHappensWhenNoV2SaveExists()
        {
            GameSave v2 = new GameSave { Diamonds = 1 };
            SaveRaw(v2);
            GameSave v1 = new GameSave { Diamonds = 999 };
            PlayerPrefs.SetString(LegacySaveKey, JsonUtility.ToJson(v1));
            PlayerPrefs.Save();

            Assert.That(CreateModel().Save.Diamonds, Is.EqualTo(1));
        }

        // ------------------------------------------------------------------ stamina

        [Test]
        public void StaminaRegeneratesOverTimeUpToTheCapAndExposesCountdown()
        {
            SaveRaw(new GameSave { Stamina = 100 });
            GameModel model = CreateModel();
            Assert.That(model.Save.Stamina, Is.EqualTo(100), "A save without an anchor must not regenerate retroactively.");
            Assert.That(model.SecondsUntilNextStamina, Is.EqualTo(economy.StaminaRegenSeconds));

            now = now.AddSeconds(economy.StaminaRegenSeconds * 5 + 30);
            model.RefreshDailyState();
            Assert.That(model.Save.Stamina, Is.EqualTo(105));
            Assert.That(model.SecondsUntilNextStamina, Is.EqualTo(economy.StaminaRegenSeconds - 30));
            Assert.That(CreateModel().Save.Stamina, Is.EqualTo(105));

            now = now.AddDays(3);
            model.RefreshDailyState();
            Assert.That(model.Save.Stamina, Is.EqualTo(economy.StaminaMax));
            Assert.That(model.SecondsUntilNextStamina, Is.Zero);

            now = now.AddSeconds(-3600);
            model.RefreshDailyState();
            Assert.That(model.Save.Stamina, Is.EqualTo(economy.StaminaMax), "A clock that went backwards must not change stamina.");
        }

        [Test]
        public void SpendingFromFullStaminaRestartsTheRegenTimerAndReportsTheTask()
        {
            GameModel model = CreateModel();
            Assert.That(model.Save.Stamina, Is.EqualTo(economy.StaminaMax));

            Assert.That(model.Perform(out _), Is.True);
            Assert.That(model.SecondsUntilNextStamina, Is.EqualTo(economy.StaminaRegenSeconds));
            Assert.That(ProgressOf(model, "daily-stamina-40"), Is.EqualTo(GameModel.PerformanceStaminaCost));

            now = now.AddSeconds(economy.StaminaRegenSeconds);
            model.RefreshDailyState();
            Assert.That(model.Save.Stamina, Is.EqualTo(economy.StaminaMax - GameModel.PerformanceStaminaCost + 1));

            Assert.That(model.TrySpend(CurrencyIds.Stamina, 10), Is.True);
            Assert.That(ProgressOf(model, "daily-stamina-40"), Is.EqualTo(GameModel.PerformanceStaminaCost + 10));
            Assert.That(model.TrySpend(CurrencyIds.Stamina, 10_000), Is.False);
        }

        // ------------------------------------------------------------------ idle income

        [Test]
        public void IdleIncomeAccruesWhileAwayAndClaimingCreditsCurrencies()
        {
            GameModel model = CreateModel();
            int gold = model.Save.Gold;
            int diamonds = model.Save.Diamonds;

            Assert.That(model.ClaimIdleIncome(out _), Is.False, "Nothing to claim right after the session starts.");
            Assert.That(model.PreviewIdleIncome().AmountOf(CurrencyIds.Gold), Is.Zero);

            now = now.AddHours(2);
            IdleIncomeReport preview = model.PreviewIdleIncome();
            Assert.That(preview.AmountOf(CurrencyIds.Gold), Is.EqualTo(economy.IdleGoldPerHour * 2));
            Assert.That(preview.AmountOf(CurrencyIds.Diamond), Is.EqualTo(economy.IdleDiamondPerHour * 2));

            Assert.That(model.ClaimIdleIncome(out string message), Is.True);
            Assert.That(message, Does.Contain("金币"));
            Assert.That(model.Save.Gold, Is.EqualTo(gold + economy.IdleGoldPerHour * 2));
            Assert.That(model.Save.Diamonds, Is.EqualTo(diamonds + economy.IdleDiamondPerHour * 2));
            Assert.That(model.ClaimIdleIncome(out _), Is.False);
            Assert.That(ProgressOf(model, "daily-idle-claim"), Is.EqualTo(1));

            now = now.AddHours(economy.IdleCapHours + 10);
            Assert.That(model.PreviewIdleIncome().Capped, Is.True);
            Assert.That(model.PreviewIdleIncome().AmountOf(CurrencyIds.Gold), Is.EqualTo(economy.IdleGoldPerHour * economy.IdleCapHours));
            Assert.That(CreateModel().PreviewIdleIncome().AmountOf(CurrencyIds.Gold), Is.EqualTo(economy.IdleGoldPerHour * economy.IdleCapHours));
        }

        // ------------------------------------------------------------------ task board

        [Test]
        public void TaskBoardTracksProgressAndPaysRewardsOnce()
        {
            GameModel model = CreateModel();
            int diamonds = model.Save.Diamonds;

            Assert.That(ProgressOf(model, "daily-login"), Is.EqualTo(1), "Each launch reports a login.");
            Assert.That(model.ClaimableTaskCount, Is.EqualTo(1));
            Assert.That(model.TaskViews(TaskCadence.Daily).Count, Is.EqualTo(economy.Tasks.Count(task => task.Cadence == TaskCadence.Daily)));
            Assert.That(model.TaskViews(TaskCadence.Weekly).Count, Is.EqualTo(economy.Tasks.Count(task => task.Cadence == TaskCadence.Weekly)));

            Assert.That(model.TryClaimTask("daily-login", out string message), Is.True);
            Assert.That(message, Does.Contain("星钻 +50"));
            Assert.That(model.Save.Diamonds, Is.EqualTo(diamonds + 50));
            Assert.That(model.TryClaimTask("daily-login", out _), Is.False);
            Assert.That(model.TryClaimTask("missing", out _), Is.False);
            Assert.That(model.TryClaimTask("daily-perform-3", out _), Is.False, "Incomplete tasks cannot be claimed.");

            for (int count = 0; count < 3; count++) model.Perform(out _);
            Assert.That(ProgressOf(model, "daily-perform-3"), Is.EqualTo(3));
            Assert.That(model.Train(0, out _), Is.True);
            Assert.That(ProgressOf(model, "daily-train-1"), Is.EqualTo(1));
            Assert.That(model.CheckIn(out _), Is.True);
            Assert.That(ProgressOf(model, "weekly-checkin-5"), Is.EqualTo(1));
            Assert.That(model.ClaimableTaskCount, Is.EqualTo(2));
            Assert.That(CreateModel().ClaimableTaskCount, Is.EqualTo(2));

            now = now.AddDays(1);
            model.RefreshDailyState();
            Assert.That(ProgressOf(model, "daily-perform-3"), Is.Zero, "Daily tasks reset on a new day.");
            Assert.That(ProgressOf(model, "weekly-checkin-5"), Is.EqualTo(1), "Weekly tasks survive the daily reset.");
            Assert.That(model.TryClaimTask("daily-login", out _), Is.False, "Login progress for the new day comes from the next launch.");
        }

        // ------------------------------------------------------------------ gacha

        [Test]
        public void TenPullChargesCurrencyUnlocksNewMembersAndConvertsDuplicatesToShards()
        {
            GameModel model = CreateModel();
            int diamonds = model.Save.Diamonds;
            Assert.That(model.IsUnlocked(4), Is.False);

            Assert.That(model.TryPull(CharacterBanner, 10, 20260902UL, out List<GachaPullResult> results, out string message), Is.True, message);

            GachaBannerDefinition banner = gacha.Find(CharacterBanner);
            Assert.That(results.Count, Is.EqualTo(10));
            Assert.That(model.Save.Diamonds, Is.EqualTo(diamonds - banner.CostTenPull));
            Assert.That(results.Select(result => result.ItemId), Is.All.Matches<string>(id => id == "xingli" || id == "yaoguang"));
            Assert.That(results.Count(result => result.IsNew), Is.EqualTo(1), "Only 瑶光 can be new; 星璃 is already owned.");
            Assert.That(model.IsUnlocked(4), Is.True);
            Assert.That(model.IsInTeam(4), Is.False);
            int expectedShards = results.Where(result => !result.IsNew)
                .Sum(result => result.Rarity == GachaRarity.Ssr ? gacha.DuplicateShardsSsr : gacha.DuplicateShardsR);
            Assert.That(expectedShards, Is.GreaterThan(0));
            Assert.That(model.Save.Shards, Is.EqualTo(expectedShards));
            Assert.That(model.GachaStateOf(CharacterBanner).TotalPulls, Is.EqualTo(10));
            Assert.That(ProgressOf(model, "weekly-gacha-10"), Is.EqualTo(10));

            GameModel reloaded = CreateModel();
            Assert.That(reloaded.IsUnlocked(4), Is.True);
            Assert.That(reloaded.Save.Shards, Is.EqualTo(expectedShards));
            Assert.That(reloaded.GachaStateOf(CharacterBanner).TotalPulls, Is.EqualTo(10));
        }

        [Test]
        public void PullsUseTicketsBeforeCurrencyAndRejectWhenBothAreShort()
        {
            SaveRaw(new GameSave { RecruitTickets = 3, Diamonds = 1000 });
            GameModel model = CreateModel();
            GachaBannerDefinition banner = gacha.Find(CharacterBanner);

            Assert.That(model.TryPull(CharacterBanner, 1, 7UL, out _, out _), Is.True);
            Assert.That(model.Save.RecruitTickets, Is.EqualTo(2));
            Assert.That(model.Save.Diamonds, Is.EqualTo(1000));

            Assert.That(model.TryPull(CharacterBanner, 10, 8UL, out _, out _), Is.False, "2 tickets + 1000 diamonds cannot pay 8 single pulls.");
            Assert.That(model.Save.RecruitTickets, Is.EqualTo(2));
            Assert.That(model.Save.Diamonds, Is.EqualTo(1000));

            Assert.That(model.TryPull(CharacterBanner, 5, 9UL, out List<GachaPullResult> results, out _), Is.True);
            Assert.That(results.Count, Is.EqualTo(5));
            Assert.That(model.Save.RecruitTickets, Is.Zero);
            Assert.That(model.Save.Diamonds, Is.EqualTo(1000 - banner.CostPerPull * 3));

            Assert.That(model.TryPull("missing-banner", 1, 1UL, out _, out _), Is.False);
            Assert.That(model.TryPull(CharacterBanner, 0, 1UL, out _, out _), Is.False);
            Assert.That(model.TryPull(CharacterBanner, 11, 1UL, out _, out _), Is.False);
        }

        [Test]
        public void CostumePullsStoreOwnedCostumesAndConsumeCostumeTickets()
        {
            SaveRaw(new GameSave { CostumeTickets = 1 });
            GameModel model = CreateModel();
            int diamonds = model.Save.Diamonds;

            Assert.That(model.TryPull(CostumeBanner, 1, 3UL, out List<GachaPullResult> first, out _), Is.True);
            Assert.That(model.Save.CostumeTickets, Is.Zero);
            Assert.That(model.Save.Diamonds, Is.EqualTo(diamonds));
            Assert.That(first[0].IsNew, Is.True);
            Assert.That(model.OwnsCostume(first[0].ItemId), Is.True);
            Assert.That(model.Save.UnlockedMembers, Is.EqualTo(new[] { 0, 1, 2, 3 }), "Costume pulls never unlock members.");

            Assert.That(model.TryPull(CostumeBanner, 10, 4UL, out List<GachaPullResult> second, out _), Is.True);
            Assert.That(model.Save.Diamonds, Is.EqualTo(diamonds - gacha.Find(CostumeBanner).CostTenPull));
            Assert.That(model.Save.OwnedCostumes.Count, Is.EqualTo(model.Save.OwnedCostumes.Distinct().Count()));
            Assert.That(second.Where(result => !result.IsNew).Sum(result => result.ShardReward), Is.EqualTo(model.Save.Shards));
            Assert.That(CreateModel().OwnsCostume(first[0].ItemId), Is.True);
        }

        // ------------------------------------------------------------------ tactics battle

        [Test]
        public void StageBattleSpendsStaminaPaysRewardsRecordsStarsAndAdvancesTheChapter()
        {
            GameModel model = CreateModel();
            int gold = model.Save.Gold;
            int diamonds = model.Save.Diamonds;
            int stamina = model.Save.Stamina;

            BattleSimulator battle = model.StartStageBattle(EasyStage, 42UL, out string startMessage);
            Assert.That(battle, Is.Not.Null, startMessage);
            Assert.That(model.Save.Stamina, Is.EqualTo(stamina - StageStamina));
            Assert.That(battle.Units.Count(unit => unit.Side == BattleSide.Player), Is.EqualTo(4));
            Assert.That(battle.Units.Where(unit => unit.Side == BattleSide.Player).Select(unit => unit.Row * 10 + unit.Col),
                Is.EquivalentTo(new[] { 0, 10, 20, 1 }), "Front column top-down, then the next column.");
            Assert.That(battle.Units.First(unit => unit.Definition.Id == "xingli").Level, Is.EqualTo(model.LevelOf(0)));

            model.SettleStageBattle(battle, out string early);
            Assert.That(early, Does.Contain("尚未结束"));
            Assert.That(model.Save.Gold, Is.EqualTo(gold));

            Assert.That(battle.AutoPlay(), Is.EqualTo(BattleOutcome.Victory));
            model.SettleStageBattle(battle, out string message);

            Assert.That(message, Does.Contain("胜利"));
            Assert.That(model.Save.Gold, Is.EqualTo(gold + StageGold + StageDropGold));
            Assert.That(model.Save.Diamonds, Is.EqualTo(diamonds + StageFirstClearDiamonds));
            Assert.That(model.StarsOf(EasyStage), Is.EqualTo(3));
            Assert.That(model.IsStageCleared(EasyStage), Is.True);
            Assert.That(model.Save.StoryProgress, Is.EqualTo(84));
            Assert.That(ProgressOf(model, "daily-battle-2"), Is.EqualTo(1));
            Assert.That(ProgressOf(model, "daily-stamina-40"), Is.EqualTo(StageStamina));

            model.SettleStageBattle(battle, out string again);
            Assert.That(again, Does.Contain("已经结算"));
            Assert.That(model.Save.Gold, Is.EqualTo(gold + StageGold + StageDropGold), "A battle settles exactly once.");

            BattleSimulator second = model.StartStageBattle(EasyStage, 43UL, out _);
            second.AutoPlay();
            model.SettleStageBattle(second, out _);
            Assert.That(model.Save.Diamonds, Is.EqualTo(diamonds + StageFirstClearDiamonds), "First-clear diamonds are paid once.");
            Assert.That(model.Save.Gold, Is.EqualTo(gold + (StageGold + StageDropGold) * 2));

            GameModel reloaded = CreateModel();
            Assert.That(reloaded.StarsOf(EasyStage), Is.EqualTo(3));
            Assert.That(reloaded.Save.StoryProgress, Is.EqualTo(89));
        }

        [Test]
        public void StageBattleRejectsUnknownStageLowStaminaAndDefeatPaysNothing()
        {
            Assert.That(CreateModel().StartStageBattle("missing", 1UL, out string missing), Is.Null);
            Assert.That(missing, Does.Contain("不存在"));

            SaveRaw(new GameSave { Stamina = StageStamina - 1 });
            GameModel poor = CreateModel();
            Assert.That(poor.StartStageBattle(EasyStage, 1UL, out string tired), Is.Null);
            Assert.That(tired, Does.Contain("体力不足"));
            Assert.That(poor.Save.Stamina, Is.EqualTo(StageStamina - 1));

            SaveRaw(new GameSave());
            GameModel model = CreateModel();
            int gold = model.Save.Gold;
            BattleSimulator battle = model.StartStageBattle(HardStage, 5UL, out _);
            Assert.That(battle, Is.Not.Null);
            Assert.That(battle.AutoPlay(), Is.EqualTo(BattleOutcome.Defeat));

            model.SettleStageBattle(battle, out string message);
            Assert.That(message, Does.Contain("失败"));
            Assert.That(model.Save.Gold, Is.EqualTo(gold));
            Assert.That(model.IsStageCleared(HardStage), Is.False);
            Assert.That(model.Save.StoryProgress, Is.EqualTo(79));
        }

        // ------------------------------------------------------------------ story

        [Test]
        public void StoryFlagsPersistOnlyAfterCompletionAndCountTowardsTasks()
        {
            GameModel model = CreateModel();
            Assert.That(model.TryStartStory("missing", out _, out string missing), Is.False);
            Assert.That(missing, Does.Contain("不存在"));

            Assert.That(model.TryStartStory(StoryId, out StoryRunner runner, out _), Is.True);
            Assert.That(runner.Current.Blocking.Command, Is.EqualTo(StoryCommand.Say));
            runner.Advance();
            Assert.That(runner.Current.IsChoice, Is.True);
            runner.Choose(0);
            Assert.That(runner.HasFlag("test-chose-trust"), Is.True);
            Assert.That(runner.HasFlag("test-met-xingli"), Is.True);
            Assert.That(model.HasStoryFlag("test-met-xingli"), Is.False, "Flags stay in the runner until the story completes.");
            Assert.That(CreateModel().HasStoryFlag("test-met-xingli"), Is.False);

            runner.Advance();
            Assert.That(runner.Finished, Is.True);
            model.CompleteStory(StoryId);

            Assert.That(model.HasStoryFlag("test-met-xingli"), Is.True);
            Assert.That(model.HasStoryFlag("test-chose-trust"), Is.True);
            Assert.That(model.IsStoryCompleted(StoryId), Is.True);
            Assert.That(ProgressOf(model, "daily-story-1"), Is.EqualTo(1));

            GameModel reloaded = CreateModel();
            Assert.That(reloaded.HasStoryFlag("test-chose-trust"), Is.True);
            Assert.That(reloaded.Save.CompletedStories, Is.EqualTo(new[] { StoryId }));

            Assert.That(reloaded.TryStartStory(StoryId, out StoryRunner second, out _), Is.True);
            Assert.That(second.HasFlag("test-chose-trust"), Is.True, "Earlier choices are visible to later playthroughs.");
            reloaded.CompleteStory(second);
            Assert.That(reloaded.Save.CompletedStories, Is.EqualTo(new[] { StoryId }));
        }

        // ------------------------------------------------------------------ currencies

        [Test]
        public void CurrencyEntryPointsReadSpendAndGrantEveryWallet()
        {
            GameModel model = CreateModel();
            int changed = 0;
            model.Changed += () => changed++;

            Assert.That(model.Balance(CurrencyIds.Diamond), Is.EqualTo(model.Save.Diamonds));
            Assert.That(model.Balance(CurrencyIds.Gold), Is.EqualTo(model.Save.Gold));
            Assert.That(model.Balance(CurrencyIds.Stamina), Is.EqualTo(model.Save.Stamina));
            Assert.That(model.Balance(CurrencyIds.RecruitTicket), Is.Zero);
            Assert.That(model.Balance("unknown"), Is.Zero);

            model.Grant(new CurrencyAmount(CurrencyIds.RecruitTicket, 2));
            model.Grant(new CurrencyAmount(CurrencyIds.CostumeTicket, 1));
            model.Grant(new CurrencyAmount(CurrencyIds.Shard, 7));
            Assert.That(model.Save.RecruitTickets, Is.EqualTo(2));
            Assert.That(model.Save.CostumeTickets, Is.EqualTo(1));
            Assert.That(model.Save.Shards, Is.EqualTo(7));
            Assert.That(changed, Is.EqualTo(3));

            Assert.That(model.TrySpend(CurrencyIds.Shard, 8), Is.False);
            Assert.That(model.TrySpend(CurrencyIds.Shard, 7), Is.True);
            Assert.That(model.Save.Shards, Is.Zero);
            Assert.That(model.TrySpend("unknown", 1), Is.False);
            Assert.That(model.TrySpend(CurrencyIds.Gold, -1), Is.False);

            model.Grant(new CurrencyAmount("accessory-test-clip", 1));
            Assert.That(model.OwnsCostume("accessory-test-clip"), Is.True);

            GameModel reloaded = CreateModel();
            Assert.That(reloaded.Save.RecruitTickets, Is.EqualTo(2));
            Assert.That(reloaded.OwnsCostume("accessory-test-clip"), Is.True);
        }

        [Test]
        public void MembersFromUnitsKeepsTheLegacyNineFirstAndSkipsEnemies()
        {
            MemberDefinition[] members = GameModel.MembersFromUnits(tactics.Units);

            string[] legacyIds = { "xingli", "feiyin", "wubai", "yeying", "yaoguang", "hupo", "xianyue", "chuxue", "chengxia" };
            Assert.That(members.Take(9).Select(member => member.Id), Is.EqualTo(legacyIds));
            Assert.That(members.Select(member => member.Id), Does.Not.Contain("dummy"));
            Assert.That(members.Select(member => member.Id), Does.Not.Contain("wall"));
            Assert.That(members.Select(member => member.Id), Does.Contain("guest-unit"));
            Assert.That(members.Select(member => member.Id).Distinct().Count(), Is.EqualTo(members.Length));
        }

        // ------------------------------------------------------------------ legacy panel logic

        [Test]
        public void PerformanceTimingJudgementUsesClearSymmetricWindows()
        {
            Assert.That(PerformanceStagePanel.JudgeDistance(0f), Is.EqualTo(PerformanceJudgement.Perfect));
            Assert.That(PerformanceStagePanel.JudgeDistance(0.085f), Is.EqualTo(PerformanceJudgement.Perfect));
            Assert.That(PerformanceStagePanel.JudgeDistance(-0.086f), Is.EqualTo(PerformanceJudgement.Great));
            Assert.That(PerformanceStagePanel.JudgeDistance(0.225f), Is.EqualTo(PerformanceJudgement.Great));
            Assert.That(PerformanceStagePanel.JudgeDistance(-0.226f), Is.EqualTo(PerformanceJudgement.Miss));
        }

        [Test]
        public void StoryBattleRejectsUltimateUntilResonanceIsReady()
        {
            StoryBattleState battle = new StoryBattleState(3, 67690);
            int hp = battle.PlayerHp;
            StoryBattleTurnResult result = battle.TakeAction(StoryBattleAction.Ultimate);

            Assert.That(result.Accepted, Is.False);
            Assert.That(battle.Turn, Is.Zero);
            Assert.That(battle.PlayerHp, Is.EqualTo(hp));
            Assert.That(battle.Resonance, Is.Zero);
        }

        [Test]
        public void StoryBattleShieldAbsorbsTheTelegraphedHeavyCounterattack()
        {
            StoryBattleState battle = new StoryBattleState(6, 67690);
            battle.TakeAction(StoryBattleAction.Dance);
            battle.TakeAction(StoryBattleAction.Vocal);
            int hpBeforeSupport = battle.PlayerHp;

            StoryBattleTurnResult support = battle.TakeAction(StoryBattleAction.Support);

            Assert.That(support.Accepted, Is.True);
            Assert.That(support.EnemyDamage, Is.EqualTo(4));
            Assert.That(battle.PlayerHp, Is.EqualTo(hpBeforeSupport - 4));
        }

        [Test]
        public void BalancedSkillRotationCanClearTheHardestStoryStage()
        {
            StoryBattleState battle = new StoryBattleState(6, 67690);
            StoryBattleAction[] rotation =
            {
                StoryBattleAction.Dance,
                StoryBattleAction.Vocal,
                StoryBattleAction.Support,
                StoryBattleAction.Ultimate,
                StoryBattleAction.Support,
                StoryBattleAction.Dance,
                StoryBattleAction.Vocal,
                StoryBattleAction.Ultimate,
            };

            foreach (StoryBattleAction action in rotation)
            {
                battle.TakeAction(action);
                if (battle.Finished) break;
            }

            Assert.That(battle.Victory, Is.True);
            Assert.That(battle.Defeat, Is.False);
            Assert.That(battle.PlayerHp, Is.GreaterThan(0));
        }

        [Test]
        public void IgnoringSupportAndComboCanLoseTheHardestStoryStage()
        {
            StoryBattleState battle = new StoryBattleState(6, 67690);
            while (!battle.Finished) battle.TakeAction(StoryBattleAction.Vocal);

            Assert.That(battle.Defeat, Is.True);
            Assert.That(battle.Victory, Is.False);
        }

        // ------------------------------------------------------------------ helpers

        private GameModel CreateModel() =>
            new GameModel(() => now, economy, gacha, tactics, id => stories.TryGetValue(id, out StoryScript script) ? script : null);

        private static void SaveRaw(GameSave save)
        {
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(save));
            PlayerPrefs.Save();
        }

        private static int ProgressOf(GameModel model, string taskId) =>
            model.TaskViews().Single(view => view.Definition.Id == taskId).Progress;

        internal static EconomyConfig BuildEconomy()
        {
            var config = new EconomyConfig
            {
                StaminaMax = 120,
                StaminaRegenSeconds = 360,
                StaminaPerTick = 1,
                IdleGoldPerHour = 900,
                IdleDiamondPerHour = 6,
                IdleCapHours = 12,
            };
            config.Tasks.Add(Task("daily-login", "登录游戏", TaskCadence.Daily, TaskTriggers.Login, 1, CurrencyIds.Diamond, 50));
            config.Tasks.Add(Task("daily-perform-3", "完成 3 次舞台演出", TaskCadence.Daily, TaskTriggers.Perform, 3, CurrencyIds.Diamond, 100));
            config.Tasks.Add(Task("daily-battle-2", "赢得 2 场剧情战斗", TaskCadence.Daily, TaskTriggers.BattleWin, 2, CurrencyIds.Diamond, 60));
            config.Tasks.Add(Task("daily-train-1", "训练任意成员 1 次", TaskCadence.Daily, TaskTriggers.Train, 1, CurrencyIds.Gold, 600));
            config.Tasks.Add(Task("daily-idle-claim", "领取一次舞台收益", TaskCadence.Daily, TaskTriggers.ClaimIdle, 1, CurrencyIds.Gold, 400));
            config.Tasks.Add(Task("daily-stamina-40", "消耗 40 点体力", TaskCadence.Daily, TaskTriggers.SpendStamina, 40, CurrencyIds.Diamond, 40));
            config.Tasks.Add(Task("daily-story-1", "阅读一段剧情", TaskCadence.Daily, TaskTriggers.ReadStory, 1, CurrencyIds.Diamond, 30));
            config.Tasks.Add(Task("weekly-gacha-10", "本周进行 10 次签约", TaskCadence.Weekly, TaskTriggers.GachaPull, 10, CurrencyIds.CostumeTicket, 1));
            config.Tasks.Add(Task("weekly-checkin-5", "本周签到 5 天", TaskCadence.Weekly, TaskTriggers.CheckIn, 5, CurrencyIds.RecruitTicket, 1));
            Assert.That(config.TryValidate(out string error), Is.True, error);
            return config;
        }

        private static TaskDefinition Task(string id, string title, string cadence, string trigger, int target,
            string currency, int amount) => new TaskDefinition
        {
            Id = id,
            Title = title,
            Cadence = cadence,
            Trigger = trigger,
            Target = target,
            Reward = new CurrencyAmount(currency, amount),
        };

        internal static GachaManifest BuildGacha()
        {
            var manifest = new GachaManifest
            {
                DuplicateShardsSsr = 50,
                DuplicateShardsSr = 15,
                DuplicateShardsR = 5,
            };
            // 1‰ SSR (星璃, already owned) and otherwise R (瑶光) so any seed yields a predictable mix.
            manifest.Banners.Add(new GachaBannerDefinition
            {
                Id = CharacterBanner,
                Name = "测试签约",
                Kind = GachaBannerKind.Character,
                CostCurrency = CurrencyIds.Diamond,
                CostPerPull = 150,
                CostTenPull = 1500,
                TicketCurrency = CurrencyIds.RecruitTicket,
                SsrRatePermille = 1,
                SrRatePermille = 0,
                SoftPityStart = 80,
                SoftPityStepPermille = 0,
                HardPity = 80,
                RateUpSharePermille = 0,
                GuaranteeFeaturedAfterLoss = false,
                TenPullGuaranteesSr = false,
                StandardSsrItemIds = new List<string> { "xingli" },
                RItemIds = new List<string> { "yaoguang" },
            });
            manifest.Banners.Add(new GachaBannerDefinition
            {
                Id = CostumeBanner,
                Name = "测试服装",
                Kind = GachaBannerKind.Costume,
                CostCurrency = CurrencyIds.Diamond,
                CostPerPull = 150,
                CostTenPull = 1500,
                TicketCurrency = CurrencyIds.CostumeTicket,
                SsrRatePermille = 40,
                SrRatePermille = 200,
                SoftPityStart = 50,
                SoftPityStepPermille = 80,
                HardPity = 70,
                RateUpSharePermille = 1000,
                GuaranteeFeaturedAfterLoss = false,
                TenPullGuaranteesSr = true,
                FeaturedItemIds = new List<string> { "costume-xingli-test" },
                SrItemIds = new List<string> { "costume-yeying-test", "costume-hupo-test" },
                RItemIds = new List<string> { "accessory-test-earring", "accessory-test-choker" },
            });
            Assert.That(manifest.TryValidate(out string error), Is.True, error);
            return manifest;
        }

        internal static TacticsManifest BuildTactics()
        {
            var manifest = new TacticsManifest();
            manifest.Skills.Add(new SkillDefinition
            {
                Id = "strike", Name = "普通攻击", Effect = SkillEffect.Damage, Pattern = SkillPattern.Single,
                PowerPermille = 1000, Duration = 0, Cooldown = 0, CanCrit = true,
            });

            string[] ids = { "xingli", "feiyin", "wubai", "yeying", "yaoguang", "hupo", "xianyue", "chuxue", "chengxia" };
            string[] names = { "星璃", "绯音", "雾白", "夜莺", "瑶光", "琥珀", "弦月", "初雪", "澄夏" };
            for (int index = 0; index < ids.Length; index++)
            {
                manifest.Units.Add(new UnitDefinition
                {
                    Id = ids[index], Name = names[index], Role = "主唱", MaxHp = 1400, Attack = 150, Defense = 60,
                    Speed = 110, CritPermille = 100, SkillIds = new List<string> { "strike" },
                });
            }

            manifest.Units.Add(new UnitDefinition
            {
                Id = "guest-unit", Name = "客串", Role = "支援", MaxHp = 1000, Attack = 100, Defense = 50, Speed = 100,
                CritPermille = 50, SkillIds = new List<string> { "strike" },
            });
            manifest.Units.Add(new UnitDefinition
            {
                Id = "dummy", Name = "训练人偶", Role = "敌方", MaxHp = 10, Attack = 1, Defense = 0, Speed = 1,
                CritPermille = 0, SkillIds = new List<string> { "strike" },
            });
            manifest.Units.Add(new UnitDefinition
            {
                Id = "wall", Name = "静电壁垒", Role = "敌方", MaxHp = 9_000_000, Attack = 1, Defense = 100_000, Speed = 1,
                CritPermille = 0, SkillIds = new List<string> { "strike" },
            });

            manifest.Stages.Add(new StageDefinition
            {
                Id = EasyStage, Chapter = "测试章", Name = "测试关卡", StaminaCost = StageStamina, TurnLimit = 20,
                ThreeStarRounds = 8, GoldReward = StageGold, DiamondFirstClear = StageFirstClearDiamonds,
                Enemies = new List<EnemySpawn> { new EnemySpawn { UnitId = "dummy", Row = 1, Col = 0, ScalePermille = 1000 } },
                Drops = new DropTable
                {
                    Rolls = 1,
                    Entries = new List<DropEntry> { new DropEntry { ItemId = CurrencyIds.Gold, Weight = 1, Min = StageDropGold, Max = StageDropGold } },
                },
            });
            manifest.Stages.Add(new StageDefinition
            {
                Id = HardStage, Chapter = "测试章", Name = "不可能的关卡", StaminaCost = StageStamina, TurnLimit = 3,
                ThreeStarRounds = 1, GoldReward = 999, DiamondFirstClear = 999,
                Enemies = new List<EnemySpawn> { new EnemySpawn { UnitId = "wall", Row = 1, Col = 1, ScalePermille = 1000 } },
                Drops = new DropTable(),
            });
            Assert.That(manifest.TryValidate(out string error), Is.True, error);
            return manifest;
        }

        internal static StoryScript BuildStory()
        {
            var script = new StoryScript { Id = StoryId, Title = "测试剧情", Chapter = "测试章" };
            script.Lines.Add(new StoryLine { Command = StoryCommand.Say, Subject = "xingli", Text = "今晚的舞台，交给我们吧。" });
            script.Lines.Add(new StoryLine { Command = StoryCommand.SetFlag, Subject = "test-met-xingli", Value = true });
            script.Lines.Add(new StoryLine
            {
                Command = StoryCommand.Choice,
                Choices = new List<StoryChoice>
                {
                    new StoryChoice { Text = "相信她", SetFlag = "test-chose-trust" },
                    new StoryChoice { Text = "再观察一下" },
                },
            });
            script.Lines.Add(new StoryLine { Command = StoryCommand.Say, Subject = "xingli", Text = "谢谢你。" });
            script.Lines.Add(new StoryLine { Command = StoryCommand.End });
            Assert.That(script.TryValidate(out string error), Is.True, error);
            return script;
        }
    }
}
