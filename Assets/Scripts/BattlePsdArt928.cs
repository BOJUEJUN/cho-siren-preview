using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace ChoSiren
{
    /// <summary>Latest returned battle PSD. Native RGBA components stream on first battle,
    /// outside the WebGL data archive. Coordinates always refer to the 2946 x 6144 source.</summary>
    public static class BattlePsdArt928
    {
        [Serializable] public sealed class Entry
        {
            public string key, file;
            public float x, y, width, height;
        }
        [Serializable] private sealed class Manifest { public Entry[] entries; }
        private static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        private static Manifest manifest;
        public static Entry[] Entries => (manifest ??= JsonUtility.FromJson<Manifest>(
            Resources.Load<TextAsset>("BattlePsd928Manifest").text)).entries;
        public static bool Ready => sprites.Count == Entries.Length;
        public static Sprite Sprite(string key) => sprites.TryGetValue(key, out var s) ? s : null;
        public static Entry Find(string key) => Array.Find(Entries, e => e.key == key);

        private static void Store(Entry entry, Texture2D texture)
        {
            texture.name = entry.key;
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            var sprite = UnityEngine.Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                Vector2.one * .5f, 100, 0, SpriteMeshType.FullRect);
            sprite.name = entry.key; sprites[entry.key] = sprite;
        }

#if UNITY_EDITOR || !UNITY_WEBGL
        public static void LoadLocal()
        {
            foreach (var entry in Entries)
            {
                if (sprites.ContainsKey(entry.key)) continue;
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!texture.LoadImage(System.IO.File.ReadAllBytes(System.IO.Path.Combine(
                    Application.streamingAssetsPath, "BattlePsd928", entry.file)), true))
                    throw new InvalidOperationException("Battle PSD texture: " + entry.key);
                Store(entry, texture);
            }
        }
#endif

        private sealed class PendingImage
        {
            public Entry Entry;
            public UnityWebRequest Request;
        }

        public static IEnumerator Load(Action<float> progress, Action<string> complete)
        {
            // Four bounded requests avoid paying a round trip for every small layer.
            // Identical authored plates share one downloaded texture; each keeps its bounds.
            var queue = new Queue<Entry>();
            var hashes = new HashSet<string>();
            foreach (var entry in Entries)
                if (!sprites.ContainsKey(entry.key) && hashes.Add(entry.file.Substring(0, 16))) queue.Enqueue(entry);
            var pending = new List<PendingImage>();
            try
            {
                while (queue.Count > 0 || pending.Count > 0)
                {
                    while (queue.Count > 0 && pending.Count < 4)
                    {
                        var entry = queue.Dequeue();
                        var parameters = DownloadedTextureParams.Default;
                        parameters.readable = false; parameters.mipmapChain = false;
                        var request = UnityWebRequestTexture.GetTexture(
                            Application.streamingAssetsPath + "/BattlePsd928/" + entry.file, parameters);
                        request.timeout = 35;
                        request.SendWebRequest();
                        pending.Add(new PendingImage { Entry = entry, Request = request });
                    }
                    for (int i = pending.Count - 1; i >= 0; i--)
                    {
                        var item = pending[i];
                        if (!item.Request.isDone) continue;
                        if (item.Request.result != UnityWebRequest.Result.Success)
                        { complete?.Invoke("战斗素材加载失败，请重试"); yield break; }
                        var texture = DownloadHandlerTexture.GetContent(item.Request);
                        var hash = item.Entry.file.Substring(0, 16);
                        foreach (var entry in Entries)
                            if (entry.file.StartsWith(hash, StringComparison.Ordinal) && !sprites.ContainsKey(entry.key)) Store(entry, texture);
                        item.Request.Dispose(); pending.RemoveAt(i);
                        progress?.Invoke(sprites.Count / (float)Entries.Length);
                    }
                    if (pending.Count > 0) yield return null;
                }
                complete?.Invoke(null);
            }
            finally
            {
                foreach (var item in pending) item.Request.Dispose();
            }
        }
    }
}
