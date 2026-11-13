using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace ChoSiren.Tests
{
    public sealed class HomeAppearance1113Tests
    {
        string saved, legacy;
        bool had, hadLegacy;
        [SetUp] public void Before()
        {
            had = PlayerPrefs.HasKey(GameModel.SaveKey); saved = PlayerPrefs.GetString(GameModel.SaveKey);
            hadLegacy = PlayerPrefs.HasKey(GameModel.LegacySaveKey); legacy = PlayerPrefs.GetString(GameModel.LegacySaveKey);
            PlayerPrefs.DeleteKey(GameModel.SaveKey); PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
        }
        [TearDown] public void After()
        {
            if (had) PlayerPrefs.SetString(GameModel.SaveKey, saved); else PlayerPrefs.DeleteKey(GameModel.SaveKey);
            if (hadLegacy) PlayerPrefs.SetString(GameModel.LegacySaveKey, legacy); else PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
        }
        [Test] public void AnyOwnedMemberCanPersistWithoutChangingTeamAndUnownedCannot()
        {
            var model = new GameModel();
            int[] team = model.Save.Team.ToArray();
            int extra = Enumerable.Range(0, GameModel.Members.Length).First(i => !model.IsUnlocked(i));
            int home = GameModel.HomeIndexForMember1113(extra);
            Assert.That(home, Is.GreaterThanOrEqualTo(6));
            model.SetHomeCharacter(home);
            Assert.That(model.Save.HomeCharacterIndex, Is.Zero);
            model.Save.UnlockedMembers.Add(extra);
            model.SetHomeCharacter(home);
            Assert.That(model.Save.HomeCharacterIndex, Is.EqualTo(home));
            Assert.That(new GameModel().Save.HomeCharacterIndex, Is.EqualTo(home));
            CollectionAssert.AreEqual(team, model.Save.Team);
            Assert.That(PsdHomeMotion.SpeakerName(home), Is.EqualTo(GameModel.Members[extra].Name));
        }
        [Test] public void PublishedHomeIndicesRemainStable()
        {
            CollectionAssert.AreEqual(new[] { "xingli", "feiyin", "yeying", "wubai", "huanhuoling", "linyin" },
                GameModel.HomeMemberIds1113.Take(6));
            Assert.That(GameModel.HomeMemberIds1113.Length, Is.EqualTo(GameModel.Members.Length));
        }
    }
}
