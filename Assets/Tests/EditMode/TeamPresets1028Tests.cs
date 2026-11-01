using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace ChoSiren.Tests
{
    public sealed class TeamPresets1028Tests
    {
        private static readonly DateTime Now = new DateTime(2026, 10, 28, 12, 0, 0);

        [Test]
        public void SaveWithoutPresetFieldsKeepsItsFormationEquipmentAndCurrency()
        {
            var store = new InMemorySaveStore();
            var before = new GameModel(() => Now, store);
            int[] formation = before.Save.Team.ToArray();
            Assert.That(before.EquipAccessoryForMember(formation[0], 1, out _), Is.True);
            before.Save.Gold = 12345;
            before.Save.Diamonds = 4321;
            before.Save.TeamPresets.Clear();
            string legacy = JsonUtility.ToJson(before.Save)
                .Replace("\"TeamPresets\":[],", string.Empty)
                .Replace("\"ActiveTeamPreset\":0,", string.Empty);
            Assert.That(legacy, Does.Not.Contain("TeamPresets"));
            store.SetString(GameModel.SaveKey, legacy);

            var after = new GameModel(() => Now, store);
            Assert.That(after.Save.Team, Is.EqualTo(formation));
            // 2026-10-29: a legacy save keeps one visible team, not five padding slots.
            Assert.That(after.Save.TeamPresets.Count, Is.EqualTo(1));
            Assert.That(after.Save.ActiveTeamPreset, Is.Zero);
            Assert.That(after.Save.TeamPresets[0].Members, Is.EqualTo(formation));
            Assert.That(after.Save.TeamPresets[0].Name, Is.Not.Empty);
            Assert.That(after.Save.Gold, Is.EqualTo(12345));
            Assert.That(after.Save.Diamonds, Is.EqualTo(4321));
            Assert.That(after.EquippedAccessoriesFor(formation[0]), Does.Contain(1));
        }

        [Test]
        public void ChangingCaptainAndNameOnlyChangesTheActivePresetAndSurvivesReload()
        {
            var store = new InMemorySaveStore();
            var model = new GameModel(() => Now, store);
            int[] original = model.Save.Team.ToArray();
            // Only the first slot exists until the player adds another team.
            Assert.That(model.AddTeamPreset(out string message), Is.True, message);
            Assert.That(model.SaveTeamPreset(1, out message), Is.True, message);
            Assert.That(model.ReplaceTeamSlot(0, original[3], out message), Is.True, message);
            int[] second = model.Save.Team.ToArray();
            Assert.That(second[0], Is.EqualTo(original[3]));
            Assert.That(model.RenameTeamPreset(1, "月光演出", out message), Is.True, message);
            Assert.That(model.SelectTeamPreset(0, out message), Is.True, message);
            Assert.That(model.Save.Team, Is.EqualTo(original));
            Assert.That(model.Save.TeamPresets[1].Members, Is.EqualTo(second));
            Assert.That(model.Save.TeamPresets[0].Members, Is.Not.SameAs(model.Save.TeamPresets[1].Members));

            var reloaded = new GameModel(() => Now, store);
            Assert.That(reloaded.Save.ActiveTeamPreset, Is.Zero);
            Assert.That(reloaded.Save.Team, Is.EqualTo(original));
            Assert.That(reloaded.SelectTeamPreset(1, out message), Is.True, message);
            Assert.That(reloaded.Save.Team, Is.EqualTo(second));
            Assert.That(reloaded.ActiveTeamName, Is.EqualTo("月光演出"));
        }

        [TestCase("empty")]
        [TestCase("duplicate")]
        [TestCase("unowned")]
        [TestCase("invalid")]
        [TestCase("tooMany")]
        public void UnusablePresetCannotChangeActiveTeamOrSavedData(string reason)
        {
            var store = new InMemorySaveStore();
            var model = new GameModel(() => Now, store);
            int[] original = model.Save.Team.ToArray();
            Assert.That(model.AddTeamPreset(out string added), Is.True, added);
            Assert.That(model.SelectTeamPreset(0, out string back), Is.True, back);
            var target = new List<int>(original);
            if (reason == "empty") target.Clear();
            if (reason == "duplicate") target[1] = target[0];
            if (reason == "unowned") target[1] = Enumerable.Range(0, GameModel.Members.Length).First(i => !model.IsUnlocked(i));
            if (reason == "invalid") target[1] = -1;
            if (reason == "tooMany") target.Add(original[0]);
            model.Save.TeamPresets[1].Members = target;
            string persisted = store.GetString(GameModel.SaveKey, "");
            Assert.That(model.SelectTeamPreset(1, out string message), Is.False);
            Assert.That(message, Is.Not.Empty);
            Assert.That(model.Save.Team, Is.EqualTo(original));
            Assert.That(model.Save.ActiveTeamPreset, Is.Zero);
            Assert.That(store.GetString(GameModel.SaveKey, ""), Is.EqualTo(persisted));
        }

        [Test]
        public void NamesAreLimitedToEightTextElementsAndBlankNamesCannotOverwrite()
        {
            var store = new InMemorySaveStore();
            var model = new GameModel(() => Now, store);
            Assert.That(model.RenameTeamPreset(0, "🌙星光演出第一小队额外", out _), Is.True);
            string name = model.ActiveTeamName;
            Assert.That(new StringInfo(name).LengthInTextElements, Is.EqualTo(8));
            Assert.That(name.StartsWith("🌙", StringComparison.Ordinal), Is.True);
            Assert.That(model.RenameTeamPreset(0, " \n<>\t ", out _), Is.False);
            Assert.That(model.ActiveTeamName, Is.EqualTo(name));
            Assert.That(new GameModel(() => Now, store).ActiveTeamName, Is.EqualTo(name));
        }
    }
}
