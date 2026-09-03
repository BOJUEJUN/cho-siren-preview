using System;
using System.Collections.Generic;
using ChoSiren.Systems.Data;
using ChoSiren.Systems.Economy;
using ChoSiren.Systems.Gacha;
using ChoSiren.Systems.Story;
using ChoSiren.Systems.Tactics;
using NUnit.Framework;

namespace ChoSiren.Tests.Systems
{
    /// <summary>
    /// Exercises the loader's error aggregation with in-memory tables. Parsing of the real JSON
    /// files is covered separately by <see cref="GameDataTablesTests"/>, which needs JsonUtility.
    /// </summary>
    public sealed class GameDataRepositoryTests
    {
        private sealed class FakeSource : IGameDataSource
        {
            public readonly Dictionary<string, string> Files = new Dictionary<string, string>(StringComparer.Ordinal);

            public bool TryReadText(string resourcePath, out string text) => Files.TryGetValue(resourcePath, out text);
        }

        private sealed class FakeReader : IJsonReader
        {
            public readonly Dictionary<string, object> Objects = new Dictionary<string, object>(StringComparer.Ordinal);

            public T FromJson<T>(string json) where T : class
            {
                if (json == "throw") throw new FormatException("bad json");
                return Objects.TryGetValue(json, out object value) ? value as T : null;
            }
        }

        private static TacticsManifest ValidTactics()
        {
            var manifest = new TacticsManifest();
            manifest.Skills.Add(new SkillDefinition { Id = "strike", Name = "普攻" });
            manifest.Units.Add(new UnitDefinition { Id = "xingli", Name = "星璃", SkillIds = new List<string> { "strike" } });
            manifest.Units.Add(new UnitDefinition { Id = "yeying", Name = "夜莺", SkillIds = new List<string> { "strike" } });
            manifest.Units.Add(new UnitDefinition { Id = "chuxue", Name = "初雪", SkillIds = new List<string> { "strike" } });
            manifest.Units.Add(new UnitDefinition { Id = "drone", Name = "无人机", SkillIds = new List<string> { "strike" } });
            manifest.Stages.Add(new StageDefinition
            {
                Id = "s1", Name = "关卡", Enemies = new List<EnemySpawn> { new EnemySpawn { UnitId = "drone" } }
            });
            return manifest;
        }

        private static GachaManifest ValidGacha(string ssrId = "xingli")
        {
            var manifest = new GachaManifest();
            manifest.Banners.Add(new GachaBannerDefinition
            {
                Id = "b1",
                Name = "池",
                FeaturedItemIds = new List<string> { ssrId },
                SrItemIds = new List<string> { "yeying" },
                RItemIds = new List<string> { "chuxue" }
            });
            return manifest;
        }

        private static (GameDataRepository repo, FakeSource source, FakeReader reader) Build()
        {
            var source = new FakeSource();
            var reader = new FakeReader();
            source.Files[GameDataPaths.Economy] = "economy";
            source.Files[GameDataPaths.Gacha] = "gacha";
            source.Files[GameDataPaths.Tactics] = "tactics";
            reader.Objects["economy"] = new EconomyConfig();
            reader.Objects["gacha"] = ValidGacha();
            reader.Objects["tactics"] = ValidTactics();
            return (new GameDataRepository(source, reader), source, reader);
        }

        [Test]
        public void ValidTablesLoadCleanly()
        {
            (GameDataRepository repo, _, _) = Build();

            Assert.That(repo.LoadAll(), Is.True, string.Join("; ", repo.Errors));
            Assert.That(repo.Economy, Is.Not.Null);
            Assert.That(repo.Gacha.Banners.Count, Is.EqualTo(1));
            Assert.That(repo.Tactics.Units.Count, Is.EqualTo(4));
        }

        [Test]
        public void EveryProblemIsReportedNotJustTheFirst()
        {
            (GameDataRepository repo, FakeSource source, FakeReader reader) = Build();
            source.Files.Remove(GameDataPaths.Economy);
            source.Files[GameDataPaths.Gacha] = "throw";
            reader.Objects["tactics"] = new TacticsManifest { SchemaVersion = 99 };

            Assert.That(repo.LoadAll(), Is.False);
            Assert.That(repo.Errors.Count, Is.EqualTo(3));
            Assert.That(repo.Errors[0], Does.Contain("未找到数据表"));
            Assert.That(repo.Errors[1], Does.Contain("无法解析"));
            Assert.That(repo.Errors[2], Does.Contain("版本不支持"));
        }

        [Test]
        public void CharacterBannersMustReferenceBattleUnits()
        {
            (GameDataRepository repo, _, FakeReader reader) = Build();
            reader.Objects["gacha"] = ValidGacha("ghost");

            Assert.That(repo.LoadAll(), Is.False);
            Assert.That(repo.Errors.Count, Is.EqualTo(1));
            Assert.That(repo.Errors[0], Does.Contain("ghost"));
        }

        [Test]
        public void StoriesLoadLazilyAndCheckTheirId()
        {
            (GameDataRepository repo, FakeSource source, FakeReader reader) = Build();
            var script = new StoryScript { Id = "chapter-01", Title = "一" };
            script.Lines.Add(new StoryLine { Command = StoryCommand.Say, Subject = "a", Text = "你好" });
            source.Files[GameDataPaths.Story("chapter-01")] = "story";
            source.Files[GameDataPaths.Story("chapter-02")] = "story";
            reader.Objects["story"] = script;

            Assert.That(repo.TryGetStory("chapter-01", out StoryScript loaded, out string error), Is.True, error);
            Assert.That(loaded, Is.SameAs(script));
            Assert.That(repo.TryGetStory("chapter-01", out StoryScript cached, out _), Is.True);
            Assert.That(cached, Is.SameAs(loaded));

            Assert.That(repo.TryGetStory("chapter-02", out _, out error), Is.False);
            Assert.That(error, Does.Contain("不一致"));
            Assert.That(repo.TryGetStory("chapter-09", out _, out error), Is.False);
            Assert.That(error, Does.Contain("未找到"));
        }
    }
}
