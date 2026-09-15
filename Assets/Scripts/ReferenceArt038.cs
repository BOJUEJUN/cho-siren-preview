using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace ChoSiren
{
    // Original, lossless PNGs are downloaded separately from the WebGL data archive.
    // This cache owns their textures and sprites; page instances must not destroy them.
    public static class ReferenceArt038
    {
        private const string Prefix = "Art/Reference038/";
        private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
        private static readonly string[] Required = {
            "lobby-board-038", "team-board-038", "members-board-038", "profile-board-038",
            "accessory-board-038", "audition-board-038", "map-board-038", "battle-stage-038", "unknown-member-038"
        };
        [Serializable] private sealed class Manifest { public Entry[] entries; }
        [Serializable] private sealed class Entry { public string key; public string file; }
        private sealed class Pending { public string key; public UnityWebRequest request; }

        public static Sprite Load(string resource)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string key = resource.StartsWith(Prefix, StringComparison.Ordinal) ? resource.Substring(Prefix.Length) : resource;
            return Sprites.TryGetValue(key, out Sprite sprite) ? sprite : null;
#else
            return Resources.Load<Sprite>(resource);
#endif
        }

        public static IEnumerator Preload(Action<float> progress, Action<string> finished)
        {
            Manifest manifest = null;
            try
            {
                TextAsset json = Resources.Load<TextAsset>("ReferenceArt038Manifest");
                if (json != null) manifest = JsonUtility.FromJson<Manifest>(json.text);
            }
            catch (Exception exception) { Debug.LogError("Reference art manifest: " + exception.Message); }
            if (manifest == null || manifest.entries == null)
            { finished?.Invoke("界面素材清单未载入，请重试"); yield break; }
            var files = new Dictionary<string, string>();
            foreach (Entry entry in manifest.entries)
                if (entry != null && !string.IsNullOrEmpty(entry.key) && !string.IsNullOrEmpty(entry.file)
                    && entry.file.StartsWith("Reference038/", StringComparison.Ordinal) && !entry.file.Contains(".."))
                    files[entry.key] = entry.file;
            foreach (string key in Required)
                if (!files.ContainsKey(key))
                { Debug.LogError("Reference art manifest missing " + key); finished?.Invoke("界面素材清单不完整，请重试"); yield break; }

            var queue = new Queue<string>();
            int complete = 0;
            foreach (string key in Required)
                if (Sprites.TryGetValue(key, out Sprite sprite) && sprite != null) complete++;
                else queue.Enqueue(key);
            var pending = new List<Pending>();
            try
            {
                while (queue.Count > 0 || pending.Count > 0)
                {
                    while (pending.Count < 3 && queue.Count > 0)
                    {
                        string key = queue.Dequeue();
                        string url = Application.streamingAssetsPath.TrimEnd('/') + "/" + files[key];
                        UnityWebRequest request = UnityWebRequestTexture.GetTexture(url, true);
                        request.timeout = 45;
                        request.SendWebRequest();
                        pending.Add(new Pending { key = key, request = request });
                    }
                    for (int i = pending.Count - 1; i >= 0; i--)
                    {
                        Pending item = pending[i];
                        if (!item.request.isDone) continue;
                        if (item.request.result != UnityWebRequest.Result.Success)
                        {
                            Debug.LogError("Reference art download failed: " + item.key + " " + item.request.url + " " + item.request.error);
                            finished?.Invoke("界面素材下载失败" + (item.request.responseCode > 0 ? "（HTTP " + item.request.responseCode + "）" : "（网络连接中断）") + "，请重试");
                            yield break;
                        }
                        Texture2D texture = DownloadHandlerTexture.GetContent(item.request);
                        if (texture == null || texture.width < 1 || texture.height < 1)
                        { finished?.Invoke("界面素材读取失败，请重试"); yield break; }
                        // UnityWebRequestTexture decodes PNGs as color/sRGB textures.
                        texture.filterMode = FilterMode.Bilinear;
                        texture.wrapMode = TextureWrapMode.Clamp;
                        texture.name = item.key;
                        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                            new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
                        sprite.name = item.key;
                        Sprites[item.key] = sprite;
                        item.request.Dispose();
                        pending.RemoveAt(i);
                        complete++;
                    }
                    float inFlight = 0;
                    foreach (Pending item in pending) inFlight += Mathf.Clamp01(item.request.downloadProgress);
                    progress?.Invoke((complete + inFlight) / Required.Length);
                    yield return null;
                }
            }
            finally
            {
                foreach (Pending item in pending) { item.request.Abort(); item.request.Dispose(); }
            }
            progress?.Invoke(1f);
            finished?.Invoke(null);
        }
    }
}
