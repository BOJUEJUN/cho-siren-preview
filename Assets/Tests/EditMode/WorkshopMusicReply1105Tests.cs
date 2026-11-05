using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace ChoSiren.Tests
{
    public sealed class WorkshopMusicReply1105Tests
    {
        private static string Validate(string json, bool config = false)
        {
            var reply = JsonUtility.FromJson<WorkshopMusic1105.Reply>(json);
            return (string)typeof(WorkshopMusic1105).GetMethod("ValidateReply", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { reply, config });
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EmptyObjectIsNotAnAcceptedTaskOrQuote(bool config)
        {
            Assert.That(Validate("{}", config), Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void QuoteRequiresRealPriceAndCurrency()
        {
            Assert.That(Validate("{\"enabled\":true,\"model\":\"Suno6\"}", true), Is.Not.Null);
            Assert.That(Validate("{\"enabled\":true,\"model\":\"Suno6\",\"price\":0.08,\"currency\":\"USD\"}", true), Is.Null);
        }

        [TestCase("{\"status\":\"done\"}")]
        [TestCase("{\"status\":\"done\",\"tracks\":[null]}")]
        [TestCase("{\"status\":\"done\",\"tracks\":[{\"url\":\"http://example.test/a.mp3\"}]}")]
        public void CompletedTaskNeedsUsableHttpsAudio(string json)
        {
            Assert.That(Validate(json), Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void RunningAndCompletedTasksRemainAccepted()
        {
            Assert.That(Validate("{\"status\":\"running\"}"), Is.Null);
            Assert.That(Validate("{\"status\":\"done\",\"tracks\":[{\"url\":\"https://example.test/a.mp3\"}]}"), Is.Null);
        }

        [Test]
        public void CacheIoFailureReturnsAnActionableError()
        {
            string blocker = Path.Combine(Path.GetTempPath(), "cho-music-" + Guid.NewGuid().ToString("N"));
            File.WriteAllText(blocker, "This file prevents creating a child folder.");
            try
            {
                object[] args = { Path.Combine(blocker, "song.mp3"), new byte[] { 1, 2, 3 }, null };
                bool ok = (bool)typeof(WorkshopMusic1105).GetMethod("TryCache", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, args);
                Assert.That(ok, Is.False);
                Assert.That((string)args[2], Does.Contain("存储"));
            }
            finally { File.Delete(blocker); }
        }
    }
}
