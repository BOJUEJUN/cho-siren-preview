using System;
using ChoSiren.Systems.Story;
using NUnit.Framework;
using UnityEngine;

namespace ChoSiren.Tests
{
    /// <summary>2026-10-28: the first-launch contract (name + avatar) and the prologue it leads into.</summary>
    public sealed class PlayerProfile1028Tests
    {
        private static readonly DateTime Now = new DateTime(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc);

        [SetUp, TearDown]
        public void Clean() { PlayerPrefs.DeleteKey(GameModel.SaveKey); PlayerPrefs.DeleteKey(GameModel.LegacySaveKey); }

        [Test]
        public void ANewSaveAsksForANameAndShowsTheDefaultTitle()
        {
            var model = new GameModel(() => Now);
            Assert.That(model.NeedsPlayerName, Is.True);
            Assert.That(model.PlayerName, Is.EqualTo(GameModel.DefaultPlayerName));
            Assert.That(model.PlayerAvatar, Is.Zero);
        }

        [Test]
        public void ASignedNameAndAvatarSurviveAReload()
        {
            var model = new GameModel(() => Now);
            Assert.That(model.TrySetPlayerProfile("  林  夏 ", 3, null, out string error), Is.True, error);
            var reloaded = new GameModel(() => Now);
            Assert.That(reloaded.NeedsPlayerName, Is.False);
            Assert.That(reloaded.PlayerName, Is.EqualTo("林 夏"), "ends trimmed, inner whitespace folded");
            Assert.That(reloaded.PlayerAvatar, Is.EqualTo(3));
        }

        [Test]
        public void SharedAvatarResolverUsesTheOwnedMemberThenFallsBackToTheSavedPreset()
        {
            var model = new GameModel(() => Now, new InMemorySaveStore());
            Assert.That(model.TrySetPlayerProfile("林夏", 3, null, out _), Is.True);
            model.Save.UnlockedMembers.Add(4);
            string member = GameModel.Members[4].Id;
            Assert.That(model.SetPlayerAvatarMember(member), Is.True);
            Sprite face = ChoSiren.UI.PlayerAvatar1102.Resolve(model);
            Assert.That(face, Is.SameAs(ChoSiren.UI.PlayerAvatar1102.Member(member)));
            Assert.That(face.name, Does.Contain("@Head"));
            model.SetPlayerAvatarPreset(7);
            Assert.That(model.PlayerAvatarMemberId, Is.Null);
            Assert.That(ChoSiren.UI.PlayerAvatar1102.Resolve(model), Is.SameAs(ChoSiren.UI.PlayerAvatar1102.Preset(7)));
            model.Save.PlayerAvatarMember = "missing-member";
            Assert.That(ChoSiren.UI.PlayerAvatar1102.Resolve(model), Is.SameAs(ChoSiren.UI.PlayerAvatar1102.Preset(7)));
            Assert.That(model.PlayerName, Is.EqualTo("林夏"), "changing the worn face keeps the signed name");
        }

        [TestCase("", "先写下你的名字")]
        [TestCase("   ", "先写下你的名字")]
        [TestCase("一二三四五六七八九", "名字最多 8 个字")]
        [TestCase("<b>星</b>", "名字里不能有 < > { } \\ 这些符号")]
        [TestCase("{player}", "名字里不能有 < > { } \\ 这些符号")]
        public void BadNamesAreRefusedWithAReason(string name, string reason)
        {
            var model = new GameModel(() => Now);
            Assert.That(model.TrySetPlayerProfile(name, 0, null, out string error), Is.False);
            Assert.That(error, Is.EqualTo(reason));
            Assert.That(model.NeedsPlayerName, Is.True, "nothing was saved");
        }

        [Test]
        public void ACharacterTheFontCannotDrawIsNamed()
        {
            var model = new GameModel(() => Now);
            Assert.That(model.TrySetPlayerProfile("林龘", 0, c => c != '龘', out string error), Is.False);
            Assert.That(error, Does.Contain("龘"));
        }

        [Test]
        public void TheAvatarIndexIsClampedToTheChoices()
        {
            var model = new GameModel(() => Now);
            Assert.That(model.TrySetPlayerProfile("星野", 99, null, out _), Is.True);
            Assert.That(model.PlayerAvatar, Is.EqualTo(GameModel.PlayerAvatarResources.Length - 1));
        }

        [Test]
        public void EverySuggestedNameIsValid()
        {
            foreach (string name in GameModel.SuggestedPlayerNames)
                Assert.That(GameModel.PlayerNameProblem(name), Is.Null, name);
        }

        [Test]
        public void ThePrologueLoadsRunsToTheEndAndNamesTheManager()
        {
            var model = new GameModel(() => Now);
            Assert.That(model.TrySetPlayerProfile("林夏", 0, null, out _), Is.True);
            Assert.That(model.TryStartStory(GameModel.PrologueStoryId, out StoryRunner runner, out string message), Is.True, message);
            bool named = false;
            int guard = 0;
            for (StoryFrame frame = runner.Current; frame != null && !frame.IsEnd && guard < 200; guard++)
            {
                string text = frame.Blocking.Text;
                if (!string.IsNullOrEmpty(text) && text.Contains("{player}"))
                    named |= model.Personalize(text).Contains("林夏");
                frame = frame.IsChoice ? runner.Choose(0) : runner.Advance();
            }
            Assert.That(guard, Is.LessThan(200), "the prologue ends");
            Assert.That(named, Is.True, "{player} becomes the signed name");
            model.CompleteStory(GameModel.PrologueStoryId);
            Assert.That(model.IsStoryCompleted(GameModel.PrologueStoryId), Is.True);
        }

        [TestCase(0, "prologue-accept")]
        [TestCase(1, "prologue-bold")]
        public void BothOpeningChoicesFinishAfterSixShortLines(int option, string flag)
        {
            var asset = Resources.Load<TextAsset>("Data/Story/prologue");
            var runner = new StoryRunner(JsonUtility.FromJson<StoryScript>(asset.text));
            int dialogue = 0, choices = 0, guard = 0;
            for (StoryFrame frame = runner.Start(); !frame.IsEnd && guard++ < 30;)
            {
                if (frame.IsChoice)
                {
                    choices++;
                    frame = runner.Choose(option);
                }
                else
                {
                    dialogue++;
                    Assert.That(frame.Blocking.Text.Length, Is.LessThanOrEqualTo(48));
                    frame = runner.Advance();
                }
            }
            Assert.That(runner.Finished, Is.True);
            Assert.That(dialogue, Is.EqualTo(6));
            Assert.That(choices, Is.EqualTo(1));
            Assert.That(runner.HasFlag(flag), Is.True);
        }
    }
}
