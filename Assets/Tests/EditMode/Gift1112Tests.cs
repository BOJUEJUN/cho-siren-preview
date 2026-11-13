using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace ChoSiren.Tests
{
    /// <summary>2026-11-12 (少爷: "新角色直接送所有人"): 幻纱 / 幻惑伶 / 凛音 are owned by every save, given once.</summary>
    public sealed class Gift1112Tests
    {
        private static readonly DateTime Now = new DateTime(2026, 11, 12, 12, 0, 0, DateTimeKind.Utc);

        private static int[] Gifted => GameModel.GiftedMembers1112.Select(GameModel.IndexOfMember).ToArray();

        [Test]
        public void ANewSaveStartsWithTheThreeNewMembers()
        {
            var model = new GameModel(() => Now, new InMemorySaveStore());
            foreach (int index in Gifted) Assert.That(model.IsUnlocked(index), Is.True, GameModel.Members[index].Name);
            Assert.That(model.Save.NewMembersGifted1112, Is.True);
        }

        [Test]
        public void AnOldSaveGetsThemOnLoadOnceWithoutDuplicatesAndKeepsWhatItHad()
        {
            var store = new InMemorySaveStore();
            var old = new GameModel(() => Now, store);
            // Pretend this save predates the gift: it signed 凛音 itself (at a higher level) but not the other two.
            int linyin = GameModel.IndexOfMember("linyin");
            old.Save.NewMembersGifted1112 = false;
            old.Save.UnlockedMembers = old.Save.UnlockedMembers.Where(i => !Gifted.Contains(i)).ToList();
            old.Save.UnlockedMembers.Add(linyin);
            old.Save.MemberLevels[linyin] = 33;
            int huansha = GameModel.IndexOfMember("huansha");
            old.Save.MemberLevels[huansha] = 1; // what a 0.9.51 load left behind
            foreach (int i in old.Save.UnlockedMembers.Where(i => !Gifted.Contains(i)).ToList()) old.Save.MemberLevels[i] = 20 + i;
            old.Save.Roster = null; // the index lists rebuild the roster on load
            store.SetString(GameModel.SaveKey, JsonUtility.ToJson(old.Save));

            var loaded = new GameModel(() => Now, store);
            foreach (int index in Gifted) Assert.That(loaded.IsUnlocked(index), Is.True, GameModel.Members[index].Name);
            Assert.That(loaded.Save.UnlockedMembers.Count(i => i == linyin), Is.EqualTo(1), "no duplicate entry");
            Assert.That(loaded.Save.MemberLevels[linyin], Is.EqualTo(33), "a member she already had keeps her level");
            int weakest = old.Save.UnlockedMembers.Where(i => !Gifted.Contains(i)).Min(i => old.Save.MemberLevels[i]);
            Assert.That(loaded.Save.MemberLevels[huansha], Is.EqualTo(System.Math.Max(1, weakest)), "she joins at the weakest owned member's level");
            Assert.That(loaded.Save.Roster.Members.Where(m => GameModel.GiftedMembers1112.Contains(m.MemberId)).All(m => m.Unlocked), Is.True,
                "the stable roster records the gift too");

            // Loading again changes nothing.
            int owned = loaded.Save.UnlockedMembers.Count;
            var again = new GameModel(() => Now, store);
            Assert.That(again.Save.UnlockedMembers.Count, Is.EqualTo(owned));
        }

        [Test]
        public void GiftedMembersLeaveTheAuditionPool()
        {
            var model = new GameModel(() => Now, new InMemorySaveStore());
            Assert.That(model.Save.OnlineCandidateIds.Concat(model.Save.OfflineCandidateIds)
                .Any(id => GameModel.GiftedMembers1112.Contains(id)), Is.False, "选秀 never offers a member you already own");
        }
    }
}
