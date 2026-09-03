using System;
using System.Collections.Generic;
using ChoSiren.Systems.Story;
using NUnit.Framework;

namespace ChoSiren.Tests.Systems
{
    public sealed class StoryRunnerTests
    {
        private static StoryScript Script()
        {
            var script = new StoryScript { Id = "t", Title = "测试" };
            script.Lines.Add(new StoryLine { Label = "start", Command = StoryCommand.Background, Subject = "bg-a" });
            script.Lines.Add(new StoryLine { Command = StoryCommand.Music, Subject = "bgm-a" });
            script.Lines.Add(new StoryLine { Command = StoryCommand.Show, Subject = "xingli", Expression = "neutral", Position = "center" });
            script.Lines.Add(new StoryLine { Command = StoryCommand.Say, Subject = "xingli", Text = "第一句" });
            script.Lines.Add(new StoryLine
            {
                Command = StoryCommand.Choice,
                Choices = new List<StoryChoice>
                {
                    new StoryChoice { Text = "信任", Jump = "trust", SetFlag = "trusted" },
                    new StoryChoice { Text = "随便", Jump = "casual" },
                }
            });
            script.Lines.Add(new StoryLine { Label = "trust", Command = StoryCommand.Say, Subject = "xingli", Text = "信任分支" });
            script.Lines.Add(new StoryLine { Command = StoryCommand.Jump, Jump = "merge" });
            script.Lines.Add(new StoryLine { Label = "casual", Command = StoryCommand.Say, Subject = "xingli", Text = "随便分支" });
            script.Lines.Add(new StoryLine { Label = "merge", Command = StoryCommand.JumpIf, Subject = "trusted", Value = true, Jump = "bonus" });
            script.Lines.Add(new StoryLine { Command = StoryCommand.Say, Subject = "producer", Text = "普通结尾" });
            script.Lines.Add(new StoryLine { Command = StoryCommand.Jump, Jump = "finish" });
            script.Lines.Add(new StoryLine { Label = "bonus", Command = StoryCommand.Say, Subject = "producer", Text = "羁绢结尾" });
            script.Lines.Add(new StoryLine { Label = "finish", Command = StoryCommand.SetFlag, Subject = "t-complete", Value = true });
            script.Lines.Add(new StoryLine { Command = StoryCommand.End });
            return script;
        }

        [Test]
        public void FirstFrameBatchesDirectivesBeforeTheFirstLine()
        {
            var runner = new StoryRunner(Script());
            StoryFrame frame = runner.Start();

            Assert.That(frame.Directives.Count, Is.EqualTo(3));
            Assert.That(frame.Directives[0].Command, Is.EqualTo(StoryCommand.Background));
            Assert.That(frame.Directives[2].Subject, Is.EqualTo("xingli"));
            Assert.That(frame.Blocking.Text, Is.EqualTo("第一句"));
            Assert.That(frame.IsChoice, Is.False);
            Assert.That(runner.Finished, Is.False);
        }

        [Test]
        public void TrustBranchSetsFlagAndUnlocksBonusLine()
        {
            var runner = new StoryRunner(Script());
            runner.Start();
            StoryFrame choice = runner.Advance();
            Assert.That(choice.IsChoice, Is.True);
            Assert.That(choice.Blocking.Choices.Count, Is.EqualTo(2));

            StoryFrame branch = runner.Choose(0);
            Assert.That(branch.Blocking.Text, Is.EqualTo("信任分支"));
            Assert.That(runner.HasFlag("trusted"), Is.True);

            StoryFrame bonus = runner.Advance();
            Assert.That(bonus.Blocking.Text, Is.EqualTo("羁绢结尾"));

            StoryFrame end = runner.Advance();
            Assert.That(end.IsEnd, Is.True);
            Assert.That(runner.Finished, Is.True);
            Assert.That(runner.HasFlag("t-complete"), Is.True);
        }

        [Test]
        public void CasualBranchSkipsBonusLine()
        {
            var runner = new StoryRunner(Script());
            runner.Start();
            runner.Advance();
            StoryFrame branch = runner.Choose(1);
            Assert.That(branch.Blocking.Text, Is.EqualTo("随便分支"));

            StoryFrame normal = runner.Advance();
            Assert.That(normal.Blocking.Text, Is.EqualTo("普通结尾"));
            Assert.That(runner.HasFlag("trusted"), Is.False);
            Assert.That(runner.Advance().IsEnd, Is.True);
        }

        [Test]
        public void FlagsCarryAcrossScripts()
        {
            var flags = new HashSet<string>(StringComparer.Ordinal) { "trusted" };
            var runner = new StoryRunner(Script(), flags);
            runner.Start("merge");

            Assert.That(runner.Current.Blocking.Text, Is.EqualTo("羁绢结尾"), "外部传入的标记必须影响条件跳转");
        }

        [Test]
        public void MisuseIsRejected()
        {
            var runner = new StoryRunner(Script());
            Assert.Throws<InvalidOperationException>(() => runner.Advance());
            runner.Start();
            runner.Advance();
            Assert.Throws<InvalidOperationException>(() => runner.Advance(), "选择支未决时不能推进");
            Assert.Throws<ArgumentOutOfRangeException>(() => runner.Choose(5));
            Assert.Throws<ArgumentException>(() => runner.Start("nowhere"));
        }

        [Test]
        public void ValidationCatchesDanglingJumpsAndInfiniteLoops()
        {
            StoryScript broken = Script();
            broken.Lines[6].Jump = "missing";
            Assert.That(broken.TryValidate(out string error), Is.False);
            Assert.That(error, Does.Contain("不存在的标签"));

            var loop = new StoryScript { Id = "loop", Title = "循环" };
            loop.Lines.Add(new StoryLine { Label = "a", Command = StoryCommand.Jump, Jump = "b" });
            loop.Lines.Add(new StoryLine { Label = "b", Command = StoryCommand.Jump, Jump = "a" });
            var runner = new StoryRunner(loop);
            Assert.Throws<InvalidOperationException>(() => runner.Start());
        }

        [Test]
        public void ScriptWithoutExplicitEndStillFinishes()
        {
            var script = new StoryScript { Id = "short", Title = "短" };
            script.Lines.Add(new StoryLine { Command = StoryCommand.Say, Subject = "a", Text = "唯一一句" });
            var runner = new StoryRunner(script);
            runner.Start();
            StoryFrame end = runner.Advance();

            Assert.That(end.IsEnd, Is.True);
            Assert.That(end.Blocking, Is.Null);
            Assert.That(runner.Finished, Is.True);
        }
    }
}
