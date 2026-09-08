using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace ChoSiren.Tests
{
    public sealed class RosterOwnershipLoopTests
    {
        private static readonly DateTime Now = new DateTime(2026, 9, 7, 12, 0, 0);

        [SetUp]
        public void SetUp() => ClearSave();

        [TearDown]
        public void TearDown() => ClearSave();

        private static void ClearSave()
        {
            PlayerPrefs.DeleteKey(GameModel.SaveKey);
            PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
        }

        private static int Candidate(GameModel model) => Enumerable.Range(0, GameModel.Members.Length)
            .Last(i => !model.IsUnlocked(i) && model.Tactics.FindUnit(GameModel.MemberIdAt(i)) != null);

        [Test]
        public void CaptainAppointmentPreservesPartyEquipmentAndRejectsUnowned()
        {
            var model = new GameModel(() => Now);
            int candidate = Candidate(model);
            int[] original = model.Save.Team.ToArray();
            Assert.That(model.AppointCaptain(candidate, out _), Is.False);
            Assert.That(model.Save.Team, Is.EqualTo(original));
            Assert.That(model.EquipAccessoryForMember(original[0], 0, out _), Is.True);
            Assert.That(model.AppointCaptain(original[2], out _), Is.True);
            Assert.That(model.Save.Team[0], Is.EqualTo(original[2]));
            Assert.That(model.Save.Team, Is.EquivalentTo(original));
            Assert.That(model.EquippedAccessoryFor(original[0]), Is.EqualTo(0));
            int[] otherMembers = model.Save.Team.Skip(1).ToArray();
            Assert.That(model.SignCandidate(candidate, 1, out _), Is.True);
            Assert.That(model.AppointCaptain(candidate, out _), Is.True);
            Assert.That(model.Save.Team.Skip(1), Is.EqualTo(otherMembers));
            Assert.That(model.IsUnlocked(original[2]), Is.True);
            var loaded = new GameModel(() => Now);
            Assert.That(loaded.Save.Team[0], Is.EqualTo(candidate));
            Assert.That(loaded.EquippedAccessoryFor(original[0]), Is.EqualTo(0));
            Assert.That(loaded.AppointCaptain(candidate, out _), Is.False);
        }

        [Test]
        public void SigningPersistsAndOwnedFirstPaginationShowsLateCatalogCandidate()
        {
            var model = new GameModel(() => Now);
            int candidate = Candidate(model);
            int diamonds = model.Save.Diamonds;  // v0.3.3: 签约消耗星钻而非星光币
            Assert.That(model.SignCandidate(candidate, 1, out string message), Is.True, message);

            var loaded = new GameModel(() => Now);
            Assert.That(loaded.IsUnlocked(candidate), Is.True);
            Assert.That(loaded.Save.Diamonds, Is.EqualTo(diamonds - 1));
            var page = MemberRosterPagination.Build(GameModel.Members.Length, 0,
                priority: i => loaded.IsUnlocked(i) ? 0 : 1);
            Assert.That(page.SourceIndices, Does.Contain(candidate));
            Assert.That(page.SourceIndices.Take(loaded.Save.UnlockedMembers.Count).All(loaded.IsUnlocked), Is.True);
            Assert.That(loaded.SignCandidate(candidate, 1, out _), Is.False);
            Assert.That(loaded.Save.Diamonds, Is.EqualTo(diamonds - 1), "Repeated clicks cannot charge twice");
        }

        [Test]
        public void SignedMemberReplacesFullTeamAndSurvivesReload()
        {
            var model = new GameModel(() => Now);
            int candidate = Candidate(model);
            Assert.That(model.SignCandidate(candidate, 1, out _), Is.True);
            int[] previous = model.Save.Team.ToArray();
            Assert.That(previous.Length, Is.EqualTo(4));
            Assert.That(model.ReplaceTeamSlot(2, candidate, out string message), Is.True, message);
            Assert.That(model.Save.Team, Is.EqualTo(new[] { previous[0], previous[1], candidate, previous[3] }));
            Assert.That(new GameModel(() => Now).Save.Team, Is.EqualTo(model.Save.Team));
        }

        [Test]
        public void ExistingTeammateSwapsIntoLeaderSlotWithoutDuplication()
        {
            var model = new GameModel(() => Now);
            int[] original = model.Save.Team.ToArray();
            Assert.That(model.ReplaceTeamSlot(0, original[3], out _), Is.True);
            Assert.That(model.Save.Team, Is.EqualTo(new[] { original[3], original[1], original[2], original[0] }));
            Assert.That(new GameModel(() => Now).Save.Team, Is.EqualTo(model.Save.Team));
            Assert.That(model.ReplaceTeamSlot(0, original[3], out _), Is.False);
        }

        [Test]
        public void ReplacingWithDuplicateCareerStillProducesPlayableBattle()
        {
            var model = new GameModel(() => Now);
            string retainedCareer = GameModel.Members[model.Save.Team[0]].Career;
            int candidate = Enumerable.Range(0, GameModel.Members.Length).First(i => !model.IsUnlocked(i)
                && GameModel.Members[i].Career == retainedCareer
                && model.Tactics.FindUnit(GameModel.MemberIdAt(i)) != null);
            Assert.That(model.SignCandidate(candidate, 1, out _), Is.True);
            Assert.That(model.ReplaceTeamSlot(3, candidate, out _), Is.True);
            Assert.That(model.Save.Team.Select(i => GameModel.Members[i].Career).Distinct().Count(), Is.LessThan(4));
            Assert.That(model.StartStageBattle("stage-1-1", 337, out string message), Is.Not.Null, message);
        }

        [Test]
        public void EmptySlotCanAppendOwnedMemberButNeverDuplicateAnExistingOne()
        {
            var model = new GameModel(() => Now);
            int[] original = model.Save.Team.ToArray();
            model.Save.Team = new List<int> { original[0], original[1] };
            Assert.That(model.ReplaceTeamSlot(2, original[2], out _), Is.True);
            Assert.That(model.ReplaceTeamSlot(3, original[0], out _), Is.True);
            Assert.That(model.Save.Team, Is.EqualTo(new[] { original[1], original[2], original[0] }));
            Assert.That(new GameModel(() => Now).Save.Team.Distinct().Count(), Is.EqualTo(3));
        }

        [Test]
        public void InvalidSlotOrUnownedMemberDoesNotMutateTeamOrCurrency()
        {
            var model = new GameModel(() => Now);
            int[] original = model.Save.Team.ToArray();
            int gold = model.Save.Gold;
            Assert.That(model.ReplaceTeamSlot(-1, original[0], out _), Is.False);
            Assert.That(model.ReplaceTeamSlot(4, original[0], out _), Is.False);
            Assert.That(model.ReplaceTeamSlot(0, Candidate(model), out _), Is.False);
            Assert.That(model.ReplaceTeamSlot(0, -1, out _), Is.False);
            Assert.That(model.Save.Team, Is.EqualTo(original));
            Assert.That(model.Save.Gold, Is.EqualTo(gold));
        }

        [Test]
        public void PrioritySortIsStableAndFilteringHappensBeforePagination()
        {
            var first = MemberRosterPagination.Build(8, 0, i => i % 2 == 0,
                pageSize: 2, priority: i => i >= 4 ? 0 : 1);
            var second = MemberRosterPagination.Build(8, 1, i => i % 2 == 0,
                pageSize: 2, priority: i => i >= 4 ? 0 : 1);
            Assert.That(first.SourceIndices, Is.EqualTo(new[] { 4, 6 }));
            Assert.That(second.SourceIndices, Is.EqualTo(new[] { 0, 2 }));
            Assert.That(first.TotalMatches, Is.EqualTo(4));
        }
    }
}
