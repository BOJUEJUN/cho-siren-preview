using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ChoSiren.Systems.Story;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ChoSiren.Tests
{
    public sealed class StorySceneDirector1028Tests
    {
        private const string Prefix = "Art/Story/Scenes1019/cg-";
        private HashSet<string> keys;

        [SetUp]
        public void ReadProductionCgIndex()
        {
            var index = Resources.Load<TextAsset>("Data/StoryCgIndex");
            Assert.That(index, Is.Not.Null);
            keys = new HashSet<string>(index.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
        }

        private static StoryScript Script(string id)
        {
            var asset = Resources.Load<TextAsset>("Data/Story/" + id);
            Assert.That(asset, Is.Not.Null, id);
            return JsonUtility.FromJson<StoryScript>(asset.text);
        }

        [Test]
        public void AllProductionDialogueScenesUseExistingIntegratedArt()
        {
            var scripts = Resources.LoadAll<TextAsset>("Data/Story");
            Assert.That(scripts.Length, Is.GreaterThanOrEqualTo(43));
            var used = new HashSet<string>();
            int characterLines = 0;
            foreach (var asset in scripts)
            {
                var script = JsonUtility.FromJson<StoryScript>(asset.text);
                string current = null;
                for (int i = 0; i < script.Lines.Count; i++)
                {
                    var line = script.Lines[i];
                    if (line.Command == StoryCommand.Background) current = null;
                    if (line.Command != StoryCommand.Say) continue;
                    current = StorySceneDirector1028.Resolve(script, i, current, keys.Contains);
                    if (string.IsNullOrEmpty(line.Subject)) continue;
                    characterLines++;
                    Assert.That(current, Is.Not.Null.And.Not.Empty, script.Id + " line " + i);
                    Assert.That(current, Does.Match(@"-l\d+$|-scene-v2$"), "Must not fall back to a pasted speaker portrait");
                    Assert.That(current, Is.Not.EqualTo(Prefix + "chapter-01-l19"));
                    used.Add(current);
                }
            }
            Assert.That(characterLines, Is.GreaterThan(140));
            foreach (string key in used)
            {
                string path = Path.Combine(Application.dataPath, "Resources", key);
                Assert.That(File.Exists(path + ".png") || File.Exists(path + ".jpg"), Is.True, key);
            }
        }

        [Test]
        public void SpeakerChangesKeepTheSameRehearsalScene()
        {
            var script = Script("prologue");
            string scene = StorySceneDirector1028.Resolve(script, 4, null, keys.Contains);
            Assert.That(scene, Is.EqualTo(Prefix + "prologue-l4"));
            int alarm = script.IndexOfLabel("alarm");
            foreach (int i in Enumerable.Range(4, alarm - 4))
            {
                scene = StorySceneDirector1028.Resolve(script, i, scene, keys.Contains);
                Assert.That(scene, Is.EqualTo(Prefix + "prologue-l4"), "Speaker " + script.Lines[i].Subject);
            }
        }

        [Test]
        public void TheShortOpeningChangesToTheAlarmSceneAtItsActualLabel()
        {
            var script = Script("prologue");
            int alarm = script.IndexOfLabel("alarm");
            for (int i = alarm; i < script.Lines.Count; i++)
                Assert.That(StorySceneDirector1028.Resolve(script, i, Prefix + "prologue-l4", keys.Contains),
                    Is.EqualTo(Prefix + "prologue-l24"));
        }

        [Test]
        public void AlleyAndItsPostBattleDialogueUseTheReplacementCompleteScene()
        {
            Assert.That(StorySceneDirector1028.Resolve(Script("stage-1-1"), 3, null, keys.Contains),
                Is.EqualTo(Prefix + "stage-1-1-scene-v2"));
            Assert.That(StorySceneDirector1028.Resolve(Script("stage-1-1-end"), 2, null, keys.Contains),
                Is.EqualTo(Prefix + "stage-1-1-scene-v2"));
        }

        [Test]
        public void WrongWhiteFxImageIsReplacedAndLookaheadStopsAtLocationChange()
        {
            Assert.That(StorySceneDirector1028.Resolve(Script("chapter-01"), 19, null, keys.Contains),
                Is.EqualTo(Prefix + "chapter-01-l21"));
            var opening = Script("prologue");
            Assert.That(StorySceneDirector1028.Resolve(opening, 1, null, keys.Contains), Is.Null,
                "City narration must not show the later rehearsal room before its background command");
        }
    }

    public sealed class NativePortraitQuality1028Tests
    {
        [TestCase("Assets/Resources/Art/Reference038/home-layer-04.png")]
        [TestCase("Assets/Resources/Art/Members/member-xingli.png")]
        [TestCase("Assets/Resources/Art/Members/member-feiyin.png")]
        [TestCase("Assets/Resources/Art/Members/member-wubai.png")]
        [TestCase("Assets/Resources/Art/Members/member-yeying.png")]
        public void LargePortraitsRetainSourceResolutionWithFineNativeCompression(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Assert.That(importer, Is.Not.Null, path);
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            if (path.EndsWith("home-layer-04.png", StringComparison.Ordinal))
            {
                Assert.That(width, Is.EqualTo(1524));
                Assert.That(height, Is.EqualTo(2984));
            }
            Assert.That(importer.npotScale, Is.EqualTo(TextureImporterNPOTScale.None));
            Assert.That(importer.mipmapEnabled, Is.False);
            foreach (string platform in new[] { "Standalone", "Android" })
            {
                var settings = importer.GetPlatformTextureSettings(platform);
                Assert.That(settings.overridden, Is.True, platform);
                Assert.That(settings.maxTextureSize, Is.EqualTo(4096), platform);
                Assert.That(settings.maxTextureSize, Is.GreaterThanOrEqualTo(Math.Max(width, height)), path);
                Assert.That(settings.format, Is.EqualTo(platform == "Standalone" ? TextureImporterFormat.BC7 : TextureImporterFormat.ASTC_4x4));
                Assert.That(settings.crunchedCompression, Is.False, platform);
            }
        }
    }
}
