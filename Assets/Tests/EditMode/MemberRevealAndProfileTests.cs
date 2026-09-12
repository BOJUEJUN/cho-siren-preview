using System.Collections.Generic;
using System.Linq;
using ChoSiren.Systems.Presentation;
using ChoSiren.Systems.Tactics;
using NUnit.Framework;
using UnityEngine;

namespace ChoSiren.Tests
{
    /// <summary>
    /// Reveal rules and grouped profile copy. These are independent of the runtime UI: they pin
    /// the contract that a locked member never exposes identity/combat data, and that every
    /// unlocked profile line comes from the shared battle definitions instead of invented copy.
    /// </summary>
    public sealed class MemberRevealAndProfileTests
    {
        private static readonly string[] CareerOrder = { "主唱", "主舞", "Rapper", "门面" };

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey(GameModel.SaveKey);
            PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(GameModel.SaveKey);
            PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
        }

        [Test]
        public void NewlySignedMemberFlipsFromSilhouetteToFullCardAndProgressAdvances()
        {
            var model = new GameModel();
            int ownedBefore = model.Save.UnlockedMembers.Count;
            int candidate = Enumerable.Range(0, GameModel.Members.Length).Last(index => !model.IsUnlocked(index));
            Assert.That(MemberRosterVisibility.ShowsRealName(model.IsUnlocked(candidate)), Is.False);
            Assert.That(MemberRosterVisibility.LockedCardTitle(
                    MemberRosterVisibility.RemainingCount(ownedBefore, GameModel.Members.Length)),
                Does.Contain((GameModel.Members.Length - ownedBefore).ToString()));

            Assert.That(model.SignCandidate(candidate, 1, out string message), Is.True, message);
            var reloaded = new GameModel();
            Assert.That(reloaded.IsUnlocked(candidate), Is.True, "新获得角色必须无损保存");
            Assert.That(MemberRosterVisibility.ShowsRealName(reloaded.IsUnlocked(candidate)), Is.True);
            Assert.That(MemberRosterVisibility.ShowsCombatStats(reloaded.IsUnlocked(candidate)), Is.True);
            Assert.That(MemberRosterVisibility.RemainingCount(reloaded.Save.UnlockedMembers.Count,
                GameModel.Members.Length), Is.EqualTo(GameModel.Members.Length - ownedBefore - 1));

            // 分页优先已拥有：新获得角色必须出现在第一页，未获得角色仍保持隐藏。
            MemberRosterPage page = MemberRosterPagination.Build(GameModel.Members.Length, 0,
                priority: index => reloaded.IsUnlocked(index) ? 0 : 1);
            Assert.That(page.SourceIndices, Does.Contain(candidate));
            int stillLocked = Enumerable.Range(0, GameModel.Members.Length).First(index => !reloaded.IsUnlocked(index));
            Assert.That(MemberRosterVisibility.ShowsCombatStats(reloaded.IsUnlocked(stillLocked)), Is.False);
            Assert.That(MemberRosterVisibility.ShowsSkills(reloaded.IsUnlocked(stillLocked)), Is.False);
        }

        [Test]
        public void EmptyFilterAndOwnedOnlyFilterKeepPaginationRulesIntact()
        {
            MemberRosterPage empty = MemberRosterPagination.Build(GameModel.Members.Length, 3, _ => false);
            Assert.That(empty.IsEmpty, Is.True);
            Assert.That(empty.PageCount, Is.Zero);
            Assert.That(empty.VisibleCount, Is.Zero);
            Assert.That(empty.PageIndex, Is.Zero);

            MemberRosterPage ownedOnly = MemberRosterPagination.Build(GameModel.Members.Length, 0, index => index < 4);
            Assert.That(ownedOnly.TotalMatches, Is.EqualTo(4));
            Assert.That(ownedOnly.SourceIndices, Is.EqualTo(new[] { 0, 1, 2, 3 }));
        }

        [Test]
        public void LockedMembersHideIdentityStatsAndSkillsWhileOwnedMembersReveal()
        {
            foreach (bool unlocked in new[] { true, false })
            {
                Assert.That(MemberRosterVisibility.ShowsRealPortrait(unlocked), Is.EqualTo(unlocked));
                Assert.That(MemberRosterVisibility.ShowsRealName(unlocked), Is.EqualTo(unlocked));
                Assert.That(MemberRosterVisibility.ShowsCareer(unlocked), Is.EqualTo(unlocked));
                Assert.That(MemberRosterVisibility.ShowsRace(unlocked), Is.EqualTo(unlocked));
                Assert.That(MemberRosterVisibility.ShowsLevel(unlocked), Is.EqualTo(unlocked));
                Assert.That(MemberRosterVisibility.ShowsCombatStats(unlocked), Is.EqualTo(unlocked));
                Assert.That(MemberRosterVisibility.ShowsSkills(unlocked), Is.EqualTo(unlocked));
                Assert.That(MemberRosterVisibility.ShowsCaptainTrait(unlocked), Is.EqualTo(unlocked));
            }

            Assert.That(MemberRosterVisibility.ProgressLabel(4, 54), Is.EqualTo("已拥有 4/54 · 未获得 50"));
            Assert.That(MemberRosterVisibility.RemainingCount(54, 54), Is.Zero);
            Assert.That(MemberRosterVisibility.RemainingCount(99, 54), Is.Zero, "进度不得出现负数");
            Assert.That(MemberRosterVisibility.LockedCardTitle(1), Does.Contain("1"));
            Assert.That(MemberRosterVisibility.LockedCardTitle(50), Does.Contain("50"));
            Assert.That(MemberRosterVisibility.LockedProfileProgress(4, 54), Does.Contain("未获得 50"));
            Assert.That(MemberRosterVisibility.LockedProfileNotice(4, 54), Does.Contain("选秀"));
        }

        [Test]
        public void LockedProfileViewCarriesNoSectionsAtAll()
        {
            MemberProfileView locked = MemberProfileView.Locked("未获得 50 位 · 已拥有 4/54");
            Assert.That(locked.Revealed, Is.False);
            Assert.That(locked.Sections, Is.Empty);
            Assert.That(locked.LockedNotice, Does.Contain("未获得"));
            Assert.That(locked.TryGet(MemberProfileSectionKind.BaseStats, out _), Is.False);
        }

        [Test]
        public void ProfileGroupsBaseStatsNormalAttackActiveSkillsAndCaptainInOrder()
        {
            var input = new MemberProfileInput
            {
                MemberId = "xingli",
                Name = "星璃",
                Career = "主唱",
                Race = "魅族",
                Level = 68,
                Power = 9200,
                Vocal = 88,
                Rhythm = 76,
                Presence = 81,
                Resonance = 90,
                NormalAttackName = "普攻",
                NormalAttackDescription = "单体100%攻击伤害，无冷却。",
                ActiveSkills = new[]
                {
                    new MemberSkillCopy("狐影瞬击", "两段共120%伤害，自身攻速叠加5%。"),
                    new MemberSkillCopy("魅语迷心", "前排175%伤害，眩晕1.2秒并打断蓄力，封技2秒。"),
                },
                CaptainTraitTitle = "魅族指挥",
                CaptainTraitDescription = MemberCaptainTraits.Describe("魅族"),
            };

            MemberProfileView view = MemberProfileSections.Build(input);
            Assert.That(view.Revealed, Is.True);
            Assert.That(view.Sections.Select(section => section.Kind), Is.EqualTo(new[]
            {
                MemberProfileSectionKind.BaseStats,
                MemberProfileSectionKind.NormalAttack,
                MemberProfileSectionKind.ActiveSkills,
                MemberProfileSectionKind.PassiveCaptain,
            }));
            Assert.That(view.Sections.Select(section => section.Title), Is.EqualTo(new[]
            {
                MemberProfileSections.BaseStatsTitle,
                MemberProfileSections.NormalAttackTitle,
                MemberProfileSections.ActiveSkillsTitle,
                MemberProfileSections.PassiveCaptainTitle,
            }));

            Assert.That(view.TryGet(MemberProfileSectionKind.NormalAttack, out MemberProfileSection normal), Is.True);
            Assert.That(normal.Lines[0].Label, Is.EqualTo("普攻"));
            Assert.That(view.TryGet(MemberProfileSectionKind.ActiveSkills, out MemberProfileSection actives), Is.True);
            Assert.That(actives.Lines.Count, Is.EqualTo(2));
            Assert.That(actives.Lines[0].Label, Is.EqualTo("狐影瞬击"));
            Assert.That(view.TryGet(MemberProfileSectionKind.PassiveCaptain, out MemberProfileSection captain), Is.True);
            Assert.That(captain.Lines[0].Value, Is.EqualTo(MemberCaptainTraits.Describe("魅族")));
            foreach (MemberProfileSection section in view.Sections)
            {
                Assert.That(section.Lines, Is.Not.Empty, section.Title);
                foreach (MemberProfileLine line in section.Lines)
                {
                    Assert.That(line.Label, Is.Not.Empty);
                    Assert.That(line.Value, Is.Not.Empty);
                    Assert.That(line.Label, Is.Not.EqualTo("暂无技能"));
                }
            }
        }

        [Test]
        public void RealCatalogProfileCopyMatchesBattleConfigForEveryMember()
        {
            var model = new GameModel();
            Assert.That(GameModel.Members.Length, Is.GreaterThanOrEqualTo(50));
            int checkedMembers = 0;
            for (int index = 0; index < GameModel.Members.Length; index++)
            {
                MemberDefinition member = GameModel.Members[index];
                UnitDefinition unit = model.Tactics.FindUnit(member.Id);
                if (unit == null) continue;

                MemberProfileInput input = BuildRealInput(model, index, unit);
                MemberProfileView view = MemberProfileSections.Build(input);
                Assert.That(view.Revealed, Is.True, member.Id);
                Assert.That(view.Sections.Count, Is.EqualTo(4), member.Id);

                view.TryGet(MemberProfileSectionKind.NormalAttack, out MemberProfileSection normal);
                string expectedNormal = unit.GrowthModel == "idol-v1"
                    ? BattleSimulator.BasicAttackName
                    : model.Tactics.FindSkill("strike")?.Name ?? "普通攻击";
                Assert.That(normal.Lines[0].Label, Is.EqualTo(expectedNormal), member.Id + " 普攻名称必须来自真实配置");

                view.TryGet(MemberProfileSectionKind.ActiveSkills, out MemberProfileSection actives);
                Assert.That(actives.Lines.Count, Is.GreaterThanOrEqualTo(1), member.Id);
                foreach (MemberProfileLine line in actives.Lines)
                {
                    Assert.That(line.Label, Is.Not.Empty, member.Id);
                    Assert.That(line.Value, Is.Not.Empty, member.Id);
                    Assert.That(line.Label, Is.Not.EqualTo("暂无技能"), member.Id + " 有真实技能时不得显示占位");
                }

                view.TryGet(MemberProfileSectionKind.PassiveCaptain, out MemberProfileSection captain);
                Assert.That(captain.Lines[0].Value, Is.EqualTo(MemberCaptainTraits.Describe(member.Race)), member.Id);
                Assert.That(captain.Lines[0].Value.Length, Is.GreaterThan(10), member.Id);

                view.TryGet(MemberProfileSectionKind.BaseStats, out MemberProfileSection stats);
                Assert.That(stats.Lines.Count, Is.EqualTo(6), member.Id);
                checkedMembers++;
            }

            Assert.That(checkedMembers, Is.GreaterThanOrEqualTo(50), "必须覆盖完整真实成员目录");
        }

        [Test]
        public void CaptainTraitCopyStaysTheAuthoredRealtimeEffects()
        {
            Assert.That(MemberCaptainTraits.Describe("魅族"), Is.EqualTo(
                "魅族指挥：骰型赋予全队追击次数，随普攻触发；好骰型获得更多追击。"));
            Assert.That(MemberCaptainTraits.Describe("海灵族 · 人鱼"), Is.EqualTo(
                "人鱼指挥：骰型为全队提供护盾；五同时全队额外减伤。"));
            Assert.That(MemberCaptainTraits.Describe("海灵族 · 人鱼"), Does.Not.Contain("重投"),
                "自选重投已向所有队长开放，不能继续写成需要人鱼队长的专属能力。");
            Assert.That(MemberCaptainTraits.Describe("海灵族 · 人鱼"), Does.Not.Contain("保留"),
                "人鱼队长特性只保留仍实际生效的骰型护盾与五同减伤。");
            Assert.That(MemberCaptainTraits.Describe("魔族 · 恶魔"), Is.EqualTo(
                "魔族指挥：全队普攻附加中毒，持续消耗敌人；骰型决定叠毒层数。"));
            Assert.That(MemberCaptainTraits.Describe("血精灵"), Is.EqualTo(
                "血精灵指挥：骰型提供穿甲，收割低血量目标；首次散点可免费重投一次。"));
            Assert.That(MemberCaptainTraits.Describe(string.Empty), Does.Contain("骰子累计伤害"));
            Assert.That(MemberCaptainTraits.Title("魅族"), Is.EqualTo("魅族指挥"));
            Assert.That(MemberCaptainTraits.Title("海灵族 · 人鱼"), Is.EqualTo("人鱼指挥"));
        }

        [Test]
        public void LockedMembersNeverMatchIdentitySearchOrFilterAndRevealAfterSigning()
        {
            const string name = "夜莺";
            const string career = "Rapper";
            const string race = "血精灵";

            // 无身份筛选：未获得成员只以匿名占位出现。
            Assert.That(MemberRosterVisibility.MatchesRosterFilter(false, name, career, race,
                false, string.Empty, string.Empty, string.Empty), Is.True);

            // 姓名/职业/种族搜索或筛选都不得命中未获得成员。
            Assert.That(MemberRosterVisibility.MatchesRosterFilter(false, name, career, race,
                false, string.Empty, string.Empty, name), Is.False, "真实姓名搜索不得命中未获得成员");
            Assert.That(MemberRosterVisibility.MatchesRosterFilter(false, name, career, race,
                false, string.Empty, string.Empty, "夜"), Is.False, "姓名片段搜索不得命中未获得成员");
            Assert.That(MemberRosterVisibility.MatchesRosterFilter(false, name, career, race,
                false, career, string.Empty, string.Empty), Is.False, "职业筛选不得命中未获得成员");
            Assert.That(MemberRosterVisibility.MatchesRosterFilter(false, name, career, race,
                false, string.Empty, race, string.Empty), Is.False, "种族筛选不得命中未获得成员");
            Assert.That(MemberRosterVisibility.MatchesRosterFilter(false, name, career, race,
                true, string.Empty, string.Empty, string.Empty), Is.False, "只看已拥有必须排除未获得成员");

            // 搜索清空后匿名占位恢复；已获得成员的真实身份搜索/筛选保持正常。
            Assert.That(MemberRosterVisibility.MatchesRosterFilter(false, name, career, race,
                false, string.Empty, string.Empty, string.Empty), Is.True, "清空搜索后占位必须恢复");
            Assert.That(MemberRosterVisibility.MatchesRosterFilter(true, name, career, race,
                false, string.Empty, string.Empty, name), Is.True);
            Assert.That(MemberRosterVisibility.MatchesRosterFilter(true, name, career, race,
                false, career, string.Empty, string.Empty), Is.True);
            Assert.That(MemberRosterVisibility.MatchesRosterFilter(true, name, career, race,
                false, string.Empty, race, string.Empty), Is.True);

            // 首次获得后同一搜索/筛选必须命中该成员，且未获得数量递减。
            Assert.That(MemberRosterVisibility.MatchesRosterFilter(false, name, career, race,
                false, string.Empty, string.Empty, name), Is.False, "签约前搜索不得命中");
            Assert.That(MemberRosterVisibility.MatchesRosterFilter(true, name, career, race,
                false, string.Empty, string.Empty, name), Is.True, "首次获得后搜索必须命中");
            Assert.That(MemberRosterVisibility.RemainingCount(5, 64), Is.EqualTo(59));
            Assert.That(MemberRosterVisibility.RemainingCount(6, 64), Is.EqualTo(58));

            // 网格级：搜索未获得真实姓名得到空列表，签约后命中。
            string[] names = { "星璃", "绯音", "雾白", name, "瑶光" };
            bool[] owned = { true, true, true, false, false };
            MemberRosterPage before = MemberRosterPagination.Build(names.Length, 0, index =>
                MemberRosterVisibility.MatchesRosterFilter(owned[index], names[index], string.Empty,
                    string.Empty, false, string.Empty, string.Empty, name));
            Assert.That(before.TotalMatches, Is.Zero, "搜索未获得真实姓名必须得到空列表");
            owned[3] = true;
            MemberRosterPage after = MemberRosterPagination.Build(names.Length, 0, index =>
                MemberRosterVisibility.MatchesRosterFilter(owned[index], names[index], string.Empty,
                    string.Empty, false, string.Empty, string.Empty, name));
            Assert.That(after.TotalMatches, Is.EqualTo(1));
            Assert.That(after.SourceIndexAt(0), Is.EqualTo(3));
        }

        [Test]
        public void LockedSilhouetteIsOneGenericFlatShapeWithNoArtwork()
        {
            Assert.That(MemberRosterVisibility.SilhouetteSpriteName, Is.EqualTo("LockedMemberSilhouette"));
            Assert.That(MemberRosterVisibility.SilhouetteAlpha, Is.EqualTo(1f), "剪影必须是单一纯色");
            Assert.That(MemberRosterVisibility.SilhouetteCoverage(80f, 180f), Is.GreaterThan(0.99f), "头部必须实心");
            Assert.That(MemberRosterVisibility.SilhouetteCoverage(80f, 4f), Is.GreaterThan(0.99f), "肩部必须实心");
            Assert.That(MemberRosterVisibility.SilhouetteCoverage(4f, 236f), Is.LessThan(0.001f), "边角必须透明");
            Assert.That(MemberRosterVisibility.SilhouetteCoverage(40f, 60f),
                Is.EqualTo(MemberRosterVisibility.SilhouetteCoverage(120f, 60f)).Within(0.001f), "必须左右对称");

            int filled = 0;
            for (int y = 0; y < 240; y++)
            {
                for (int x = 0; x < 160; x++)
                {
                    if (MemberRosterVisibility.SilhouetteCoverage(x + 0.5f, y + 0.5f) > 0.5f) filled++;
                }
            }

            Assert.That(filled, Is.InRange(15000, 28000), "必须是完整人形轮廓，而不是空白或整块色板");
        }

        [Test]
        public void StageQualitiesNeverReuseCombatSkillNames()
        {
            var model = new GameModel();
            var combatNames = new HashSet<string>(model.Tactics.Skills.Select(skill => skill.Name),
                System.StringComparer.Ordinal);
            for (int big = 0; big < 2; big++)
            {
                foreach (CombatRace race in new[]
                         {
                             CombatRace.Charm, CombatRace.Mermaid, CombatRace.Demon, CombatRace.BloodElf,
                             CombatRace.None,
                         })
                    combatNames.Add(BattleSimulator.ActiveSkillName(race, big == 1));
            }

            Assert.That(MemberStageQualities.All.Count, Is.EqualTo(4));
            foreach (string quality in MemberStageQualities.All)
                Assert.That(combatNames.Contains(quality), Is.False,
                    "面试素质不能与战斗技能/属性同名：" + quality);

            Assert.That(MemberStageQualities.DominantTrait(90, 70, 60, 80), Is.EqualTo(MemberStageQualities.Vocal));
            Assert.That(MemberStageQualities.DominantTrait(70, 91, 60, 80), Is.EqualTo(MemberStageQualities.Rhythm));
            Assert.That(MemberStageQualities.DominantTrait(70, 60, 92, 80), Is.EqualTo(MemberStageQualities.Presence));
            Assert.That(MemberStageQualities.DominantTrait(70, 60, 80, 93), Is.EqualTo(MemberStageQualities.Resonance));
            Assert.That(MemberStageQualities.Describe(90, 70, 60, 80), Does.StartWith("舞台特质 · "));
            Assert.That(MemberStageQualities.DominantTrait(80, 80, 80, 80), Is.EqualTo(MemberStageQualities.Vocal),
                "并列时按固定顺序取第一项，结果必须稳定。");
        }

        [Test]
        public void PracticePlansCoverEveryCareerAndStayInsideOneToTwoSeconds()
        {
            foreach (string career in CareerOrder)
            {
                MemberPracticePlan plan = MemberTrainingPractice.BuildPlan(career, 1234, false);
                Assert.That(plan.ActionName, Is.Not.EqualTo(MemberTrainingPractice.GeneralPractice), career);
                Assert.That(plan.FocusLabel, Is.Not.Empty, career);
                Assert.That(plan.DurationSeconds, Is.InRange(MemberTrainingPractice.MinimumDurationSeconds,
                    MemberTrainingPractice.MaximumDurationSeconds), career);
                Assert.That(plan.GoldCost, Is.EqualTo(1234));
                Assert.That(plan.DiamondCost, Is.Zero, "练习不得新增钻石消耗");
            }

            Assert.That(MemberTrainingPractice.BuildPlan("未知", 1, false).ActionName,
                Is.EqualTo(MemberTrainingPractice.GeneralPractice));
            Assert.That(MemberTrainingPractice.Duration(true),
                Is.LessThan(MemberTrainingPractice.Duration(false)), "减弱动画必须更短");
        }

        private static int FindMemberIndex(string memberId)
        {
            for (int index = 0; index < GameModel.Members.Length; index++)
                if (GameModel.Members[index].Id == memberId) return index;
            return -1;
        }

        /// <summary>Mirrors the runtime profile reader against the shared battle definitions.</summary>
        private static MemberProfileInput BuildRealInput(GameModel model, int index, UnitDefinition unit)
        {
            MemberDefinition member = GameModel.Members[index];
            GameModel.StageStats(member, index, 1, model.LevelOf(index), out int vocal,
                out int rhythm, out int presence, out int resonance, out _);
            string normalName = unit.GrowthModel == "idol-v1"
                ? BattleSimulator.BasicAttackName
                : model.Tactics.FindSkill("strike")?.Name ?? "普通攻击";
            string normalEffect = unit.GrowthModel == "idol-v1"
                ? "单体100%攻击伤害，无冷却。"
                : MemberBattlePresentation.DescribeSkill(model.Tactics.FindSkill("strike"));

            var actives = new List<MemberSkillCopy>();
            if (unit.GrowthModel == "idol-v1")
            {
                CombatRace race = BattleSimulator.ParseCombatRace(member.Race);
                actives.Add(new MemberSkillCopy(BattleSimulator.ActiveSkillName(race, false),
                    BattleSimulator.ActiveSkillDescription(race, false)));
                actives.Add(new MemberSkillCopy(BattleSimulator.ActiveSkillName(race, true),
                    BattleSimulator.ActiveSkillDescription(race, true)));
            }
            else
            {
                foreach (SkillDefinition skill in MemberBattlePresentation.FeaturedSkills(model.Tactics, member.Id))
                    actives.Add(new MemberSkillCopy(skill.Name, MemberBattlePresentation.DescribeSkill(skill)));
            }

            return new MemberProfileInput
            {
                MemberId = member.Id,
                Name = member.Name,
                Career = member.Career,
                Race = member.Race,
                Level = model.LevelOf(index),
                Power = model.PowerOf(index),
                Vocal = vocal,
                Rhythm = rhythm,
                Presence = presence,
                Resonance = resonance,
                NormalAttackName = normalName,
                NormalAttackDescription = normalEffect,
                ActiveSkills = actives,
                CaptainTraitTitle = MemberCaptainTraits.Title(member.Race),
                CaptainTraitDescription = MemberCaptainTraits.Describe(member.Race),
            };
        }
    }
}
