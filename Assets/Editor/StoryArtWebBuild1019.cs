using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

/// <summary>Streams only the new Story1019 originals; every temporary source move is owned and restored.</summary>
public sealed class StoryArtWebBuild1019 : IDisposable
{
    public const string SourceRoot = "Assets/Resources/Art/Story";
    public const string StreamingRoot = "Assets/StreamingAssets/Story1019";
    public const string ManifestPath = "Assets/Resources/StoryArt1019Manifest.json";
    public const string ExclusionRoot = "Assets/Editor/Story1019-WebGL-Excluded";
    static readonly string[] Folders = { "Scenes1019", "Portraits1019", "Transitions1019" };
    readonly string sourceRoot, streamingRoot, manifestPath, exclusionRoot;
    readonly List<string> moved = new List<string>();
    bool ownsStreaming, ownsManifest, ownsExclusion;

    [Serializable] sealed class Entry { public string key, file, sha256; public long bytes; }
    [Serializable] sealed class Manifest { public List<Entry> entries = new List<Entry>(); }

    public StoryArtWebBuild1019() : this(SourceRoot, StreamingRoot, ManifestPath, ExclusionRoot) { }
    // Explicit paths let isolated build-scope tests exercise restoration without touching production art.
    public StoryArtWebBuild1019(string source, string streaming, string manifest, string exclusion)
    { sourceRoot = source; streamingRoot = streaming; manifestPath = manifest; exclusionRoot = exclusion; }

    public void Prepare()
    {
        if (ownsStreaming || Exists(streamingRoot) || Exists(manifestPath) || Exists(exclusionRoot))
            throw new BuildFailedException("Story1019 build staging already exists; preserve it and inspect before retrying.");
        var manifest = new Manifest();
        // Validate the complete input set before owning any temporary state.
        foreach (string folder in Folders)
        {
            string source = sourceRoot + "/" + folder;
            if (!Directory.Exists(source)) throw new BuildFailedException("Missing story source folder: " + source);
            foreach (string path in Directory.EnumerateFiles(source).OrderBy(p => p, StringComparer.Ordinal))
            {
                string extension = Path.GetExtension(path).ToLowerInvariant();
                if (extension != ".png" && extension != ".jpg" && extension != ".jpeg") continue;
                byte[] bytes = File.ReadAllBytes(path);
                if (bytes.Length == 0) throw new BuildFailedException("Empty story source: " + path);
                using var sha = SHA256.Create();
                string hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
                string key = "Art/Story/" + folder + "/" + Path.GetFileNameWithoutExtension(path);
                string file = hash.Substring(0, 16) + "-" + folder.ToLowerInvariant() + "-" + Path.GetFileName(path);
                manifest.entries.Add(new Entry { key = key, file = "Story1019/" + file, sha256 = hash, bytes = bytes.Length });
            }
        }
        if (manifest.entries.Count == 0) throw new BuildFailedException("Story1019 has no original image bytes to stream.");
        Directory.CreateDirectory(streamingRoot); ownsStreaming = true;
        foreach (Entry entry in manifest.entries)
        {
            string relative = entry.key.Substring("Art/Story/".Length);
            string source = Directory.EnumerateFiles(Path.GetDirectoryName(sourceRoot + "/" + relative))
                .Single(p => Path.GetFileNameWithoutExtension(p) == Path.GetFileName(relative) && !p.EndsWith(".meta", StringComparison.Ordinal));
            File.Copy(source, Path.Combine(streamingRoot, Path.GetFileName(entry.file)), false);
        }
        string json = JsonUtility.ToJson(manifest);
        ownsManifest = true; File.WriteAllText(manifestPath, json);
        File.WriteAllText(Path.Combine(streamingRoot, "manifest.json"), json);
        Directory.CreateDirectory(exclusionRoot); ownsExclusion = true;
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach (string folder in Folders)
        {
            string error = AssetDatabase.MoveAsset(sourceRoot + "/" + folder, exclusionRoot + "/" + folder);
            if (!string.IsNullOrEmpty(error)) throw new BuildFailedException("Story1019 exclusion failed: " + error);
            moved.Add(folder);
        }
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Debug.Log("CHO_STORY1019_ORIGINAL_STREAMING count=" + manifest.entries.Count);
    }

    public void Dispose()
    {
        var errors = new List<Exception>();
        for (int i = moved.Count - 1; i >= 0; i--)
        {
            string folder = moved[i];
            try
            {
                string error = AssetDatabase.MoveAsset(exclusionRoot + "/" + folder, sourceRoot + "/" + folder);
                if (!string.IsNullOrEmpty(error)) throw new BuildFailedException("Story1019 restoration failed: " + error);
                moved.RemoveAt(i);
            }
            catch (Exception exception) { errors.Add(exception); }
        }
        // Never delete an exclusion folder while it still contains a source that failed restoration.
        void Clean(bool owned, string path, Action release)
        {
            if (!owned) return;
            try { DeleteOwnedPath(path); release(); }
            catch (Exception exception) { errors.Add(exception); }
        }
        if (moved.Count == 0) Clean(ownsExclusion, exclusionRoot, () => ownsExclusion = false);
        Clean(ownsManifest, manifestPath, () => ownsManifest = false);
        Clean(ownsStreaming, streamingRoot, () => ownsStreaming = false);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        if (errors.Count > 0) throw new AggregateException("Story1019 source restoration needs attention; originals were preserved.", errors);
    }

    static bool Exists(string path) => File.Exists(path) || Directory.Exists(path) || File.Exists(path + ".meta");
    static void DeleteOwnedPath(string path)
    {
        if (AssetDatabase.IsValidFolder(path) || !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path)))
            AssetDatabase.DeleteAsset(path);
        // Preparation can fail before Refresh imports the owned staging path.
        // Remove only this scope's files even when AssetDatabase cannot see them yet.
        if (File.Exists(path)) File.Delete(path);
        if (Directory.Exists(path)) Directory.Delete(path, true);
        if (File.Exists(path + ".meta")) File.Delete(path + ".meta");
    }
}
