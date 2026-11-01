using System;
using NUnit.Framework;
using UnityEngine;

namespace ChoSiren.Tests
{
    public sealed class VolumeSliderSaveTests
    {
        [Test]
        public void SavesFromBeforeTheSlidersStartAtFullVolume()
        {
            var store = new InMemorySaveStore();
            var model = new GameModel(() => DateTime.Now, store);
            model.Save.MusicVolume = 0; model.Save.SfxVolume = 0;
            string json = JsonUtility.ToJson(model.Save)
                .Replace("\"MusicVolume\":0,", string.Empty).Replace("\"SfxVolume\":0,", string.Empty);
            store.SetString(GameModel.SaveKey, json);
            var loaded = new GameModel(() => DateTime.Now, store);
            Assert.That(loaded.Save.MusicEnabled, Is.True);
            Assert.That(loaded.Save.MusicVolume, Is.EqualTo(100), "老存档不能把音乐读成 0 音量（会让战斗静音）。");
            Assert.That(loaded.Save.SfxVolume, Is.EqualTo(100));
        }
    }
}
