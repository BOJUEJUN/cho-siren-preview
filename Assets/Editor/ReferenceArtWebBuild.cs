using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

// Keep the exact PNG bytes outside Unity's expanded texture archive. Hash filenames
// make browser caches safe across releases. All temporary source changes are restored.
public sealed class ReferenceArtWebBuild : IDisposable
{
    const string Source = "Assets/Resources/Art/Reference038";
    const string Streaming = "Assets/StreamingAssets/Reference038";
    const string Manifest = "Assets/Resources/ReferenceArt038Manifest.json";
    bool ownsStreaming;
    bool ownsManifest;

    [Serializable] sealed class Entry { public string key; public string file; }
    [Serializable] sealed class ManifestData { public List<Entry> entries = new List<Entry>(); }

    public void Prepare()
    {
        if (Directory.Exists(Streaming) || File.Exists(Manifest))
            throw new BuildFailedException("Reference038 build staging already exists; inspect before replacing it.");
        Directory.CreateDirectory(Streaming);
        ownsStreaming = true;
        var manifest = new ManifestData();
        string[] keys = { "lobby-board-038", "team-board-038", "members-board-038",
            "profile-board-038", "accessory-board-038", "audition-board-038",
            "map-board-038", "battle-stage-038", "unknown-member-038" };
        foreach (string key in keys)
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(Source, key + ".png"));
            using var sha = SHA256.Create();
            string hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
            string filename = hash.Substring(0, 16) + "-" + key + ".png";
            File.WriteAllBytes(Path.Combine(Streaming, filename), bytes);
            manifest.entries.Add(new Entry { key = key, file = "Reference038/" + filename });
        }
        ownsManifest = true;
        File.WriteAllText(Manifest, JsonUtility.ToJson(manifest));
        File.WriteAllText(Path.Combine(Streaming, "manifest.json"), JsonUtility.ToJson(manifest));
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Debug.Log("CHO_REFERENCE_PNG_STREAMING count=" + manifest.entries.Count);
    }

    public void Dispose()
    {
        if (ownsManifest) AssetDatabase.DeleteAsset(Manifest);
        if (ownsStreaming) AssetDatabase.DeleteAsset(Streaming);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
    }
}
