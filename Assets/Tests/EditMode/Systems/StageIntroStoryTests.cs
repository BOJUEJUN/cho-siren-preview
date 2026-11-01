using ChoSiren.Systems.Data;
using ChoSiren.Systems.Story;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace ChoSiren.Tests.Systems
{
    public sealed class StageIntroStoryTests
    {
        [Test]
        public void EveryStageHasItsOwnImportedSceneAndChapterCommandsStillChangeLocations()
        {
            var repository = new GameDataRepository(new ResourcesGameDataSource(), new UnityJsonReader());
            Assert.That(repository.LoadAll(), Is.True, string.Join("\n", repository.Errors));
            for (int chapter = 1; chapter <= 2; chapter++)
            for (int number = 1; number <= 10; number++)
            {
                string id = $"stage-{chapter}-{number}";
                Assert.That(repository.TryGetStory(id, out StoryScript script, out string error), Is.True, error);
                string expected = "Art/Story/Scenes1019/" + id;
                Assert.That(script.Lines.First(line => line.Command == StoryCommand.Background).Subject,
                    Is.EqualTo(expected), id + " must follow its actual story setting");
                Assert.That(Resources.Load<Sprite>(expected), Is.Not.Null, id + " scene must be imported as a Sprite");
            }
            Assert.That(repository.TryGetStory("chapter-01", out StoryScript first, out _), Is.True);
            Assert.That(first.Lines.Where(line => line.Command == StoryCommand.Background).Select(line => line.Subject),
                Is.EqualTo(new[]
                {
                    "Art/Story/Scenes1019/chapter-01-rehearsal-solo",
                    "Art/Story/Scenes1019/chapter-01-rehearsal-arrival",
                    "Art/Story/Scenes1019/chapter-01-alley-outro",
                }));
            Assert.That(repository.TryGetStory("chapter-02", out StoryScript second, out _), Is.True);
            Assert.That(second.Lines.First(line => line.Command == StoryCommand.Background).Subject,
                Is.EqualTo("Art/Story/Scenes1019/stage-2-1"));
        }

        [TestCase(0)]
        [TestCase(1)]
        public void ChapterOneBothBranchesCutToArrivalBeforeDoorSoundAndToTheirOwnOutro(int option)
        {
            var repository = new GameDataRepository(new ResourcesGameDataSource(), new UnityJsonReader());
            Assert.That(repository.LoadAll(), Is.True, string.Join("\n", repository.Errors));
            Assert.That(repository.TryGetStory("chapter-01", out StoryScript script, out string error), Is.True, error);
            var runner = new StoryRunner(script);
            StoryFrame frame = runner.Start();
            var backgrounds = new System.Collections.Generic.List<string>();
            bool sawArrival = false;
            int guard = 0;
            while (!frame.IsEnd && guard++ < 100)
            {
                backgrounds.AddRange(frame.Directives.Where(line => line.Command == StoryCommand.Background).Select(line => line.Subject));
                if (frame.Blocking.Subject == "feiyin" && !sawArrival)
                {
                    sawArrival = true;
                    Assert.That(frame.Directives.Select(line => line.Command),
                        Is.EqualTo(new[] { StoryCommand.Background, StoryCommand.Sound, StoryCommand.Show }));
                    Assert.That(frame.Directives[0].Subject,
                        Is.EqualTo("Art/Story/Scenes1019/chapter-01-rehearsal-arrival"));
                    Assert.That(frame.Directives[2].Subject, Is.EqualTo("feiyin"));
                }
                frame = frame.IsChoice ? runner.Choose(option) : runner.Advance();
            }
            Assert.That(frame.IsEnd, Is.True);
            Assert.That(sawArrival, Is.True);
            Assert.That(backgrounds, Is.EqualTo(new[]
            {
                "Art/Story/Scenes1019/chapter-01-rehearsal-solo",
                "Art/Story/Scenes1019/chapter-01-rehearsal-arrival",
                "Art/Story/Scenes1019/chapter-01-alley-outro",
            }));
            Assert.That(runner.HasFlag("ch1-complete"), Is.True);
        }

        [Test]
        public void EveryChapterStageHasAValidFirstAttemptScene()
        {
            var repository = new GameDataRepository(new ResourcesGameDataSource(), new UnityJsonReader());
            Assert.That(repository.LoadAll(), Is.True, string.Join("\n", repository.Errors));
            for (int chapter = 1; chapter <= 2; chapter++)
            for (int number = 1; number <= 10; number++)
            {
                string id = $"stage-{chapter}-{number}";
                Assert.That(repository.TryGetStory(id, out StoryScript script, out string error), Is.True, $"{id}: {error}");
                Assert.That(script.Lines.Count, Is.GreaterThanOrEqualTo(4), id);
                var runner = new StoryRunner(script, new System.Collections.Generic.HashSet<string>());
                runner.Start();
            }
        }
    }
}
