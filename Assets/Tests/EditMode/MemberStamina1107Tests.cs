using System;
using NUnit.Framework;
using UnityEngine;

namespace ChoSiren.Tests
{
    /// <summary>2026-11-07 角色体力: battles tire the team, tired members fight weaker, rest refills over time or for gold.</summary>
    public sealed class MemberStamina1107Tests
    {
        private DateTime now = new DateTime(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc);

        [SetUp, TearDown]
        public void Clean()
        {
            PlayerPrefs.DeleteKey(GameModel.SaveKey);
            PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
        }

        private GameModel NewModel()
        {
            var model = new GameModel(() => now, new InMemorySaveStore());
            model.Save.StoryProgress = 100;
            model.Save.Stamina = 120;
            model.Save.Gold = 100000;
            return model;
        }

        [Test]
        public void MembersStartFullAndABattleTiresTheWholeTeam()
        {
            GameModel model = NewModel();
            int member = model.Save.Team[0];
            Assert.That(model.MemberStamina1107(member), Is.EqualTo(GameModel.MemberStaminaMax1107));
            var battle = model.StartStageBattle("stage-1-1", 7, out string message);
            Assert.That(battle, Is.Not.Null, message);
            foreach (int m in model.Save.Team)
                Assert.That(model.MemberStamina1107(m), Is.EqualTo(GameModel.MemberStaminaMax1107 - GameModel.BattleMemberStamina1107));
            int bench = model.Save.UnlockedMembers.Find(i => !model.Save.Team.Contains(i));
            if (bench >= 0 && !model.Save.Team.Contains(bench))
                Assert.That(model.MemberStamina1107(bench), Is.EqualTo(GameModel.MemberStaminaMax1107), "the bench rests");
        }

        [Test]
        public void TiredMembersFightWeakerAndExhaustedOnesMustRest()
        {
            GameModel model = NewModel();
            int member = model.Save.Team[0];
            var field = typeof(GameModel).GetMethod("SetMemberStamina1107", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            field.Invoke(model, new object[] { member, GameModel.TiredBelow1107 - 1 });
            Assert.That(model.IsTired1107(member), Is.True);
            Assert.That(model.FatiguePermille1107(member), Is.EqualTo(GameModel.TiredPermille1107));
            field.Invoke(model, new object[] { member, GameModel.BattleMemberStamina1107 - 1 });
            int stamina = model.Save.Stamina;
            Assert.That(model.StartStageBattle("stage-1-1", 7, out string message), Is.Null);
            Assert.That(message, Does.Contain("体力不足"));
            Assert.That(model.Save.Stamina, Is.EqualTo(stamina), "a refused battle costs nothing");
        }

        [Test]
        public void RestRefillsOverTimeAndGoldRefillsAtOnce()
        {
            GameModel model = NewModel();
            int member = model.Save.Team[0];
            var set = typeof(GameModel).GetMethod("SetMemberStamina1107", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            set.Invoke(model, new object[] { member, 20 });
            now = now.AddSeconds(GameModel.MemberStaminaRegenSeconds1107 * 10);
            Assert.That(model.MemberStamina1107(member), Is.EqualTo(30));
            int gold = model.Save.Gold;
            Assert.That(model.RestCost1107(member), Is.EqualTo(90 * GameModel.RestGoldPerPoint1107));
            Assert.That(model.TryRestNow1107(member, out string message), Is.True, message);
            Assert.That(model.MemberStamina1107(member), Is.EqualTo(GameModel.MemberStaminaMax1107));
            Assert.That(model.Save.Gold, Is.EqualTo(gold - 900));
            now = now.AddHours(30);
            Assert.That(model.MemberStamina1107(member), Is.EqualTo(GameModel.MemberStaminaMax1107), "never above full");
        }

        [Test]
        public void FifteenBattlesEmptyAFreshBar()
        {
            Assert.That(GameModel.MemberStaminaMax1107 / GameModel.BattleMemberStamina1107, Is.EqualTo(15));
            Assert.That(GameModel.MemberStaminaMax1107 * GameModel.MemberStaminaRegenSeconds1107, Is.EqualTo(6 * 3600), "a full rest takes six hours");
        }
    }
}
