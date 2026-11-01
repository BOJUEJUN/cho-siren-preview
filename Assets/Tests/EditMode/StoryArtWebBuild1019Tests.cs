using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ChoSiren.Tests
{
    public sealed class StoryArtWebBuild1019Tests
    {
        string root;
        Type ScopeType => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("StoryArtWebBuild1019"))
            .First(t => t != null);
        [SetUp] public void Before()
        {
            root = "Assets/__StoryBuildScope-" + Guid.NewGuid().ToString("N");
            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            byte[] png = texture.EncodeToPNG(); UnityEngine.Object.DestroyImmediate(texture);
            foreach (string folder in new[] { "Scenes1019", "Portraits1019", "Transitions1019" })
            { Directory.CreateDirectory(root + "/Resources/Art/Story/" + folder); File.WriteAllBytes(root + "/Resources/Art/Story/" + folder + "/fixture.png", png); }
            Directory.CreateDirectory(root + "/Editor"); Directory.CreateDirectory(root + "/StreamingAssets");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }
        [TearDown] public void After() { if (AssetDatabase.IsValidFolder(root)) AssetDatabase.DeleteAsset(root); }
        object NewScope(string manifest = null) => Activator.CreateInstance(ScopeType, root + "/Resources/Art/Story", root + "/StreamingAssets/Story1019",
            manifest ?? root + "/Resources/StoryArt1019Manifest.json", root + "/Editor/Excluded");
        void Prepare(object scope) => ScopeType.GetMethod("Prepare").Invoke(scope, null);

        [Test] public void ScopeStreamsExactOriginalBytesAndRestoresAllSourceFoldersAndGuids()
        {
            string source = root + "/Resources/Art/Story/Scenes1019/fixture.png";
            byte[] original = File.ReadAllBytes(source); string guid = AssetDatabase.AssetPathToGUID(source);
            using (var scope = (IDisposable)NewScope())
            {
                Prepare(scope);
                Assert.That(Directory.Exists(root + "/Resources/Art/Story/Scenes1019"), Is.False);
                Assert.That(Directory.Exists(root + "/Editor/Excluded/Scenes1019"), Is.True);
                string[] images = Directory.GetFiles(root + "/StreamingAssets/Story1019", "*.png");
                Assert.That(images, Has.Length.EqualTo(3));
                foreach (string image in images) Assert.That(File.ReadAllBytes(image), Is.EqualTo(original));
                Assert.That(File.ReadAllText(root + "/Resources/StoryArt1019Manifest.json"),
                    Is.EqualTo(File.ReadAllText(root + "/StreamingAssets/Story1019/manifest.json")));
            }
            Assert.That(File.ReadAllBytes(source), Is.EqualTo(original)); Assert.That(AssetDatabase.AssetPathToGUID(source), Is.EqualTo(guid));
            foreach (string folder in new[] { "Scenes1019", "Portraits1019", "Transitions1019" })
                Assert.That(Directory.Exists(root + "/Resources/Art/Story/" + folder), Is.True);
            Assert.That(Directory.Exists(root + "/Editor/Excluded"), Is.False);
            Assert.That(Directory.Exists(root + "/StreamingAssets/Story1019"), Is.False);
            Assert.That(File.Exists(root + "/Resources/StoryArt1019Manifest.json"), Is.False);
        }

        [Test] public void PreexistingStagingIsRejectedWithoutDeletingOrMovingAnything()
        {
            string manifest = root + "/Resources/StoryArt1019Manifest.json"; File.WriteAllText(manifest, "keep-existing");
            using (var scope = (IDisposable)NewScope())
                Assert.That(Assert.Throws<TargetInvocationException>(() => Prepare(scope)).InnerException.Message, Does.Contain("already exists"));
            Assert.That(File.ReadAllText(manifest), Is.EqualTo("keep-existing"));
            Assert.That(Directory.Exists(root + "/Resources/Art/Story/Scenes1019"), Is.True);
        }

        [Test] public void FailureBeforeAssetImportStillCleansOnlyItsOwnedStreamingCopies()
        {
            string source = root + "/Resources/Art/Story/Scenes1019/fixture.png";
            string guid = AssetDatabase.AssetPathToGUID(source); byte[] original = File.ReadAllBytes(source);
            using (var scope = (IDisposable)NewScope(root + "/missing-parent/manifest.json"))
                Assert.That(Assert.Throws<TargetInvocationException>(() => Prepare(scope)).InnerException,
                    Is.TypeOf<DirectoryNotFoundException>());
            Assert.That(Directory.Exists(root + "/StreamingAssets/Story1019"), Is.False);
            Assert.That(Directory.Exists(root + "/Editor/Excluded"), Is.False);
            Assert.That(AssetDatabase.AssetPathToGUID(source), Is.EqualTo(guid));
            Assert.That(File.ReadAllBytes(source), Is.EqualTo(original));
        }
    }
}
