using System;
using System.Collections.Generic;
using System.Linq;
using ChoSiren.Systems.Dice;
using ChoSiren.Systems.Tactics;
using NUnit.Framework;

namespace ChoSiren.Tests
{
    /// <summary>
    /// Player report regression: repeated races are legal as a pair, but cannot fill a formation
    /// and repeatedly restore the same battle's rescue reroll. In the current data model the
    /// player's “position” is <see cref="MemberDefinition.Career"/>.
    /// </summary>
    public sealed class BattleFormationRerollPlayModeTests
    {
        private static readonly DateTime Now = new DateTime(2026, 9, 15, 11, 33, 0);
        private const string Mermaid = "海灵族 · 人鱼";
        private const string FirstStage = "stage-1-1";

        [Test]
        public void TwoSameRaceMembersCanShareAFullFormationButAThirdIsRejectedAtomically()
        {
            var model = CreateModel();
            int[] original = model.Save.Team.ToArray();
            int existingMermaid = original.Single(index => GameModel.Members[index].Race == Mermaid);
            int secondMermaid = FindCandidate(model, Mermaid,
                career: GameModel.Members[original[0]].Career);
            Unlock(model, secondMermaid);

            int firstSlot = Array.IndexOf(original, original[0]);
            Assert.That(model.ReplaceTeamSlot(firstSlot, secondMermaid, out string accepted), Is.True, accepted);
            Assert.That(model.Save.Team.Count(index => GameModel.Members[index].Race == Mermaid), Is.EqualTo(2),
                "同种族仍可共同上阵一次，不能把正常的双人配队误判为非法。");
            Assert.That(model.Save.Team, Does.Contain(existingMermaid));

            int thirdMermaid = FindCandidate(model, Mermaid,
                career: GameModel.Members[model.Save.Team[1]].Career);
            Unlock(model, thirdMermaid);
            int[] acceptedTeam = model.Save.Team.ToArray();

            Assert.That(model.ReplaceTeamSlot(1, thirdMermaid, out string rejected), Is.False);
            Assert.That(rejected, Does.Contain("同一种族最多同时上阵 2 人"));
            Assert.That(model.Save.Team, Is.EqualTo(acceptedTeam),
                "非法第三名同族必须原子拒绝，不能先换人再留下半成品编队。");
        }

        [Test]
        public void FullFormationKeepsAtLeastThreeDistinctCareerPositions()
        {
            var model = CreateModel();
            string repeatedCareer = GameModel.Members[model.Save.Team[1]].Career;
            int firstDuplicate = FindCandidate(model, race: "魅族", career: repeatedCareer);
            Unlock(model, firstDuplicate);

            Assert.That(model.ReplaceTeamSlot(3, firstDuplicate, out string accepted), Is.True, accepted);
            Assert.That(DistinctCareerCount(model.Save.Team), Is.EqualTo(3),
                "满编允许一组重复定位，给同族或同定位搭档留出组合空间。");

            int[] acceptedTeam = model.Save.Team.ToArray();
            int secondDuplicate = FindCandidate(model, race: Mermaid,
                career: GameModel.Members[model.Save.Team[0]].Career);
            Unlock(model, secondDuplicate);

            Assert.That(model.ReplaceTeamSlot(2, secondDuplicate, out string rejected), Is.False);
            Assert.That(rejected, Does.Contain("至少需要 3 种不同定位"));
            Assert.That(model.Save.Team, Is.EqualTo(acceptedTeam),
                "剩余两席不能继续挤占定位，失败后应保留上一次有效阵容。");
        }

        [Test]
        public void LegacyFourMermaidFormationCannotEnterBattleOrSpendStamina()
        {
            var model = CreateModel();
            int[] mermaids = Enumerable.Range(0, GameModel.Members.Length)
                .Where(index => GameModel.Members[index].Race == Mermaid &&
                                model.Tactics.FindUnit(GameModel.MemberIdAt(index)) != null)
                .Take(GameModel.TeamCapacity)
                .ToArray();
            Assert.That(mermaids.Length, Is.EqualTo(GameModel.TeamCapacity),
                "测试清单必须能复现玩家反馈的四人鱼旧阵容。");
            Unlock(model, mermaids);
            model.Save.Team = mermaids.ToList();
            int stamina = model.Save.Stamina;

            BattleSimulator battle = model.StartStageBattle(FirstStage, 91501UL, out string message);

            Assert.That(battle, Is.Null, "四人鱼旧存档不能绕过编队入口进入可反复重投的战斗。");
            Assert.That(message, Does.Contain("同一种族最多同时上阵 2 人"));
            Assert.That(model.Save.Stamina, Is.EqualTo(stamina), "拒绝非法阵容时不能扣体力。");
            Assert.That(model.Save.Team, Is.EqualTo(mermaids), "旧存档只提示调整，不能擅自删除已拥有角色。");
        }

        [Test]
        public void FormationChangeDoesNotRestoreSpentRescueRerollButANewBattleGetsItsOwnAllowance()
        {
            var model = CreateModel();
            BattleSimulator firstBattle = model.StartStageBattle(FirstStage, 91502UL, out string firstMessage);
            Assert.That(firstBattle, Is.Not.Null, firstMessage);
            DiceTurn firstDice = firstBattle.BattleDice;

            firstDice.GrantFreeReroll();
            Assert.That(firstDice.FreeRerolls, Is.EqualTo(1));
            Assert.That(firstDice.RerollAll(out string rerollMessage), Is.True, rerollMessage);
            Assert.That(firstDice.FreeRerolls, Is.Zero);

            int nextLeader = model.Save.Team[1];
            Assert.That(model.ReplaceTeamSlot(0, nextLeader, out string formationMessage), Is.True,
                formationMessage);
            firstDice.GrantFreeReroll();
            Assert.That(firstDice.FreeRerolls, Is.Zero,
                "战斗中换阵/换队长不能刷新这场战斗已经消费的同族救场重投。");

            BattleSimulator nextBattle = model.StartStageBattle(FirstStage, 91503UL, out string nextMessage);
            Assert.That(nextBattle, Is.Not.Null, nextMessage);
            Assert.That(nextBattle.BattleDice, Is.Not.SameAs(firstDice));
            nextBattle.BattleDice.GrantFreeReroll();
            Assert.That(nextBattle.BattleDice.FreeRerolls, Is.EqualTo(1),
                "新战斗创建新的 DiceTurn，才重新拥有一次救场重投资格。");
        }

        private static GameModel CreateModel() =>
            new GameModel(() => Now, new InMemorySaveStore());

        private static void Unlock(GameModel model, params int[] members)
        {
            foreach (int member in members)
                if (!model.Save.UnlockedMembers.Contains(member)) model.Save.UnlockedMembers.Add(member);
        }

        private static int FindCandidate(GameModel model, string race, string career) =>
            Enumerable.Range(0, GameModel.Members.Length).First(index =>
                !model.Save.Team.Contains(index) &&
                GameModel.Members[index].Race == race &&
                GameModel.Members[index].Career == career &&
                model.Tactics.FindUnit(GameModel.MemberIdAt(index)) != null);

        private static int DistinctCareerCount(IEnumerable<int> team) =>
            team.Select(index => GameModel.Members[index].Career).Distinct().Count();
    }
}
