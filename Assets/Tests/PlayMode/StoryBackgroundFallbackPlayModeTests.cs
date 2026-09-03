using System;
using System.Collections;
using System.Linq;
using ChoSiren.Panels;
using ChoSiren.Systems.Story;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ChoSiren.Tests
{
    public sealed class StoryBackgroundFallbackPlayModeTests
    {
        [UnityTest]
        public IEnumerator UnknownRuntimeBackgroundKeepsVisibleAspectSafeArt()
        {
            PlayerPrefs.DeleteKey(GameModel.SaveKey);
            PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
            GameObject host = new GameObject("剧情运行时背景测试根节点", typeof(RectTransform));
            var script = new StoryScript { Id = "runtime-fallback", Title = "运行时兜底" };
            script.Lines.Add(new StoryLine
            {
                Command = StoryCommand.Background,
                Subject = "runtime-missing-background",
            });
            script.Lines.Add(new StoryLine { Command = StoryCommand.Say, Text = "舞台仍然可见" });

            StoryPanel panel = StoryPanel.Open(host.transform,
                new GameModel(() => new DateTime(2026, 9, 3, 12, 0, 0, DateTimeKind.Local)),
                new StoryRunner(script), null);
            yield return null;

            Image background = panel.GetComponentsInChildren<Image>(true)
                .Single(image => image.name == "Background");
            Assert.That(background.sprite, Is.Not.Null);
            Assert.That(background.color.a, Is.EqualTo(1f).Within(0.001f));
            Assert.That(background.GetComponent<AspectRatioFitter>().aspectRatio,
                Is.EqualTo(background.sprite.rect.width / background.sprite.rect.height).Within(0.0001f));
            Assert.That(panel.GetComponentsInChildren<Text>(true)
                    .Any(text => (text.text ?? string.Empty).Contains("背景资源缺失")),
                Is.False);

            UnityEngine.Object.Destroy(host);
            yield return null;
            PlayerPrefs.DeleteKey(GameModel.SaveKey);
            PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
        }
    }
}
