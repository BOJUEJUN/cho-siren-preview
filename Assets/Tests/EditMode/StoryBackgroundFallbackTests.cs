using System;
using System.Linq;
using ChoSiren.Panels;
using ChoSiren.Systems.Story;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.Tests
{
    public sealed class StoryBackgroundFallbackTests
    {
        private GameObject host;

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey(GameModel.SaveKey);
            PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
            host = new GameObject("剧情背景测试根节点", typeof(RectTransform));
        }

        [TearDown]
        public void TearDown()
        {
            if (host != null) UnityEngine.Object.DestroyImmediate(host);
            PlayerPrefs.DeleteKey(GameModel.SaveKey);
            PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
        }

        [Test]
        public void ChapterBackgroundDirectivesReferenceImportedSpriteArt()
        {
            TextAsset json = Resources.Load<TextAsset>("Data/Story/chapter-01");
            Assert.That(json, Is.Not.Null);
            StoryScript script = JsonUtility.FromJson<StoryScript>(json.text);
            StoryLine[] backgrounds = script.Lines
                .Where(line => line.Command == StoryCommand.Background)
                .ToArray();

            Assert.That(backgrounds, Has.Length.EqualTo(2));
            for (int index = 0; index < backgrounds.Length; index++)
            {
                Assert.That(backgrounds[index].Subject, Does.StartWith("Art/"));
                Assert.That(Resources.Load<Sprite>(backgrounds[index].Subject), Is.Not.Null,
                    $"剧情背景未导入为 Sprite：{backgrounds[index].Subject}");
            }
        }

        [Test]
        public void UnknownBackgroundUsesRealStageArtWithoutPlayerFacingError()
        {
            var script = new StoryScript { Id = "fallback", Title = "兜底检查" };
            script.Lines.Add(new StoryLine
            {
                Command = StoryCommand.Background,
                Subject = "missing-scene-that-does-not-exist",
            });
            script.Lines.Add(new StoryLine { Command = StoryCommand.Say, Text = "继续演出" });

            StoryPanel panel = StoryPanel.Open(host.transform,
                new GameModel(() => new DateTime(2026, 9, 3, 12, 0, 0, DateTimeKind.Local)),
                new StoryRunner(script), null);
            Image background = panel.GetComponentsInChildren<Image>(true)
                .Single(image => image.name == "Background");

            Assert.That(background.sprite, Is.Not.Null, "未知背景 ID 必须回退到真实本地舞台图。");
            Assert.That(background.preserveAspect, Is.True);
            Assert.That(background.GetComponent<AspectRatioFitter>().aspectMode,
                Is.EqualTo(AspectRatioFitter.AspectMode.EnvelopeParent));
            Assert.That(panel.GetComponentsInChildren<Text>(true)
                    .Any(text => (text.text ?? string.Empty).Contains("背景资源缺失")),
                Is.False);
        }
    }
}
