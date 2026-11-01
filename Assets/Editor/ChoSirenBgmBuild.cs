using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace ChoSiren.Editor
{
    /// <summary>Reproducible selected-song imports through Unity APIs.</summary>
    public static class ChoSirenBgmBuild
    {
        private const int SampleRate = 44100;
        private const int FrameTolerance = 2048;

        public static void ConfigureReview()
        {
            Configure();
            PlayerSettings.companyName = "CHO-SIREN Context Music Review 20261015";
            AssetDatabase.SaveAssets();
        }

        public static void PrepareRelease()
        {
            string version = Environment.GetEnvironmentVariable("CHO_BGM_RELEASE_VERSION");
            if (!string.IsNullOrEmpty(version) && !Regex.IsMatch(version, @"\A0\.3\.[0-9]+\z"))
                throw new InvalidOperationException("CHO_BGM_RELEASE_VERSION must use numeric 0.3.x.");
            Configure();
            PlayerSettings.companyName = "CHO-SIREN Studio";
            if (!string.IsNullOrEmpty(version)) PlayerSettings.bundleVersion = version;
            AssetDatabase.SaveAssets();
        }

        public static void BuildWebGLRelease()
        {
            PrepareRelease();
            ChoSirenBuild.BuildWebGL();
        }

        private static void Configure()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var evidence = new ImportEvidence
            {
                tracks = new[]
                {
                    ConfigureTrack("Assets/Resources/Audio/bgm.wav",
                        "45f57d479b8f07092b6c9d9e1af85060d51f0232cb1660369f8acea8103c6ce8", 7600932),
                    ConfigureTrack("Assets/Resources/Audio/bgm-battle.wav",
                        "38420c0391d6623780517f0a40790c475fc197013374a9cc653f92e4a2ed96bb", 6387300)
                },
                activeBuildTarget = EditorUserBuildSettings.activeBuildTarget.ToString(),
                outputSampleRate = AudioSettings.outputSampleRate
            };
            string destination = Environment.GetEnvironmentVariable("CHO_BGM_EVIDENCE_PATH");
            if (!string.IsNullOrEmpty(destination)) File.WriteAllText(destination, JsonUtility.ToJson(evidence, true));
            Debug.Log("CHO_BGM_IMPORT_VERIFIED " + JsonUtility.ToJson(evidence));
        }

        private static TrackEvidence ConfigureTrack(string path, string expectedSha, int expectedFrames)
        {
            string sourceSha;
            using (var algorithm = SHA256.Create())
            using (var stream = File.OpenRead(path))
                sourceSha = BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
            if (!string.Equals(sourceSha, expectedSha, StringComparison.Ordinal))
                throw new InvalidOperationException("Unapproved music source: " + path);
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null) throw new InvalidOperationException("Missing music importer: " + path);
            importer.forceToMono = false;
            importer.ambisonic = false;
            importer.loadInBackground = true;
            var native = importer.defaultSampleSettings;
            native.loadType = AudioClipLoadType.CompressedInMemory;
            native.compressionFormat = AudioCompressionFormat.Vorbis;
            native.quality = .75f;
            native.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            native.preloadAudioData = true;
            importer.defaultSampleSettings = native;
            var web = importer.GetOverrideSampleSettings(BuildTargetGroup.WebGL);
            web.loadType = AudioClipLoadType.CompressedInMemory;
            web.compressionFormat = AudioCompressionFormat.AAC;
            web.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
            web.sampleRateOverride = SampleRate;
            web.quality = .75f;
            web.preloadAudioData = true;
            if (!importer.SetOverrideSampleSettings(BuildTargetGroup.WebGL, web))
                throw new InvalidOperationException("Rejected Web audio profile: " + path);
            importer.SaveAndReimport();
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null || clip.channels != 2 || clip.frequency != SampleRate ||
                Math.Abs((long)clip.samples - expectedFrames) > FrameTolerance)
                throw new InvalidOperationException("Imported music identity/format mismatch: " + path);
            return new TrackEvidence
            {
                asset = path, sourceSha256 = sourceSha, seconds = clip.length,
                samples = clip.samples, expectedFrames = expectedFrames,
                importedFrameDelta = clip.samples - expectedFrames,
                channels = clip.channels, sampleRate = clip.frequency,
                nativeCompression = native.compressionFormat.ToString(),
                webCompression = web.compressionFormat.ToString(),
                webQuality = web.quality, estimatedWebDecodedPcmBytes = (long)clip.samples * clip.channels * sizeof(float)
            };
        }

        [Serializable]
        private sealed class ImportEvidence
        {
            public TrackEvidence[] tracks;
            public string activeBuildTarget;
            public int outputSampleRate;
        }

        [Serializable]
        private sealed class TrackEvidence
        {
            public string asset, sourceSha256, nativeCompression, webCompression;
            public float seconds, webQuality;
            public int samples, expectedFrames, importedFrameDelta, channels, sampleRate;
            public long estimatedWebDecodedPcmBytes;
        }
    }
}
