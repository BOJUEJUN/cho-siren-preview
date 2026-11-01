using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace ChoSiren
{
    // Original PNG bytes stay outside the data archive. This cache owns their sprites/textures.
    public static class ReferenceArt038
    {
        private const string Prefix = "Art/Reference038/";
        private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
        private static readonly HashSet<string> RetryKeys = new HashSet<string>(StringComparer.Ordinal);
        private static readonly string[] Required = {
            "lobby-board-038", "team-board-038", "members-board-038", "profile-board-038",
            "accessory-board-038", "audition-board-038", "map-board-038", "battle-stage-038", "unknown-member-038"
        };
        private static readonly string[] Optional = {
            "lobby-board-038-clean", "team-board-038-clean", "members-board-038-clean",
            "profile-board-038-clean", "accessory-board-038-clean", "audition-board-038-clean",
            "map-board-038-clean", "map-board-038-ch2"
        };
        [Serializable] private sealed class Manifest { public Entry[] entries; }
        [Serializable] private sealed class Entry { public string key; public string file; }
        [Serializable] private sealed class HomeLayout { public HomeElement[] elements; }
        [Serializable] private sealed class HomeElement { public string id; public string action; }
        private sealed class Pending { public string key; public UnityWebRequest request; }
        private static bool loading;
        public static bool HomeResourcesReady { get; private set; }
        public static bool AllResourcesReady { get; private set; }

        public static Sprite Load(string resource)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string key = resource.StartsWith(Prefix, StringComparison.Ordinal) ? resource.Substring(Prefix.Length) : resource;
            return Sprites.TryGetValue(key, out Sprite sprite) ? sprite : null;
#else
            return Resources.Load<Sprite>(resource);
#endif
        }

        // Existing callers now prepare only the authored Home composition at initial entry.
        public static IEnumerator Preload(Action<float> progress, Action<string> finished) =>
            PreloadHome(progress, finished);
        public static IEnumerator PreloadHome(Action<float> progress, Action<string> finished) =>
            LoadSelection(true, progress, finished);
        // Route gates call this before creating non-Home screens and their modal children.
        public static IEnumerator EnsureAll(Action<float> progress, Action<string> finished) =>
            LoadSelection(false, progress, finished);

        private static IEnumerator LoadSelection(bool homeOnly, Action<float> progress, Action<string> finished)
        {
            while (loading) yield return null;
            loading = true;
            var pending = new List<Pending>();
            try
            {
                if (!TryReadSelection(homeOnly, out Dictionary<string, string> files,
                        out List<string> keys, out string failure))
                { finished?.Invoke(failure); yield break; }
                var queue = new Queue<string>();
                int total = keys.Count, complete = 0;
                foreach (string key in keys)
                {
                    if (Sprites.TryGetValue(key, out Sprite sprite) && sprite != null) complete++;
                    else queue.Enqueue(key);
                }
                // Cached optional entries count too: retries report the actual retained progress.
                progress?.Invoke(total == 0 ? 1f : (float)complete / total);
                while (queue.Count > 0 || pending.Count > 0)
                {
                    while (pending.Count < 3 && queue.Count > 0)
                    {
                        string key = queue.Dequeue();
                        string url = Application.streamingAssetsPath.TrimEnd('/') + "/" + files[key];
                        if (RetryKeys.Contains(key)) url += "?retry=" + DateTime.UtcNow.Ticks;
                        if (!TryStartDownload(key, url, out Pending item, out failure))
                        { RetryKeys.Add(key); finished?.Invoke(failure); yield break; }
                        pending.Add(item);
                    }
                    for (int i = pending.Count - 1; i >= 0; i--)
                    {
                        Pending item = pending[i];
                        if (!item.request.isDone) continue;
                        if (item.request.result != UnityWebRequest.Result.Success)
                        {
                            RetryKeys.Add(item.key);
                            Debug.LogError("Reference art download failed: " + item.key + " " + item.request.url + " " + item.request.error);
                            finished?.Invoke(item.request.result == UnityWebRequest.Result.DataProcessingError
                                ? "界面素材读取失败，请重试"
                                : "界面素材下载失败" + (item.request.responseCode > 0
                                    ? "（HTTP " + item.request.responseCode + "）" : "（网络连接中断或超时）") + "，请重试");
                            yield break;
                        }
                        if (!TryCacheSprite(item, out failure))
                        { RetryKeys.Add(item.key); finished?.Invoke(failure); yield break; }
                        RetryKeys.Remove(item.key);
                        item.request.Dispose();
                        pending.RemoveAt(i);
                        complete++;
                        // Avoid a burst of large cached PNG uploads in a single frame.
                        break;
                    }
                    float inFlight = 0f;
                    foreach (Pending item in pending) inFlight += Mathf.Clamp01(item.request.downloadProgress);
                    progress?.Invoke(total == 0 ? 1f : (complete + inFlight) / total);
                    yield return null;
                }
                HomeResourcesReady = true;
                if (!homeOnly) AllResourcesReady = true;
                progress?.Invoke(1f);
                finished?.Invoke(null);
            }
            finally
            {
                foreach (Pending item in pending) { item.request.Abort(); item.request.Dispose(); }
                loading = false;
            }
        }

        private static bool TryReadSelection(bool homeOnly, out Dictionary<string, string> files,
            out List<string> keys, out string failure)
        {
            files = new Dictionary<string, string>(StringComparer.Ordinal);
            keys = new List<string>();
            failure = null;
            try
            {
                TextAsset json = Resources.Load<TextAsset>("ReferenceArt038Manifest");
                Manifest manifest = json != null ? JsonUtility.FromJson<Manifest>(json.text) : null;
                if (manifest?.entries == null) throw new InvalidOperationException("Missing ReferenceArt038Manifest");
                foreach (Entry entry in manifest.entries)
                    if (entry != null && !string.IsNullOrEmpty(entry.key) && !string.IsNullOrEmpty(entry.file)
                        && entry.file.StartsWith("Reference038/", StringComparison.Ordinal) && !entry.file.Contains(".."))
                        files[entry.key] = entry.file;
                foreach (string key in Required)
                    if (!files.ContainsKey(key)) throw new InvalidOperationException("Manifest missing " + key);
                var unique = new HashSet<string>(StringComparer.Ordinal);
                if (homeOnly)
                {
                    TextAsset layoutJson = Resources.Load<TextAsset>("Art/PsdHome20260921/layout");
                    HomeLayout layout = layoutJson != null ? JsonUtility.FromJson<HomeLayout>(layoutJson.text) : null;
                    if (layout?.elements == null) throw new InvalidOperationException("Missing authored Home layout");
                    foreach (HomeElement element in layout.elements)
                    {
                        if (element == null || string.IsNullOrEmpty(element.id)) throw new InvalidOperationException("Invalid Home layer");
                        string action = element.action ?? string.Empty;
                        // Match BuildPsdHome's existing exclusions without changing any artwork or positions.
                        if (element.id == "layer-32" || element.id == "layer-63" || action.StartsWith("Nav-", StringComparison.Ordinal)
                            || action == "Profile" || action.StartsWith("Currency-", StringComparison.Ordinal)) continue;
                        unique.Add("home-" + element.id);
                    }
                    unique.Add("home-layer-character2-portrait");
                    unique.Add("home-layer-character2-outline");
                    // 2026-10-29 PSD v3: the carousel reads these on home entry; without them
                    // Load returns null on WebGL and the idol is skipped until a full reload.
                    unique.Add("home-layer-character3-portrait");
                    unique.Add("home-layer-character3-outline");
                    unique.Add("home-layer-character4-portrait");
                    unique.Add("home-layer-character4-outline");
                    foreach (string key in unique)
                        if (!files.ContainsKey(key)) throw new InvalidOperationException("Home manifest missing " + key);
                }
                else
                {
                    foreach (string key in Required) unique.Add(key);
                    foreach (string key in Optional) if (files.ContainsKey(key)) unique.Add(key);
                    foreach (string key in files.Keys)
                        if (key.StartsWith("home-layer-", StringComparison.Ordinal) || key.StartsWith("race-", StringComparison.Ordinal)
                            || key.StartsWith("idol-", StringComparison.Ordinal) || key.StartsWith("member-detail-", StringComparison.Ordinal)
                            || key.StartsWith("vfx-", StringComparison.Ordinal)) unique.Add(key);
                }
                keys.AddRange(unique);
                keys.Sort(StringComparer.Ordinal);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError("Reference art manifest: " + exception);
                failure = "界面素材清单未载入或不完整，请重试";
                return false;
            }
        }

        private static bool TryStartDownload(string key, string url, out Pending item, out string failure)
        {
            item = null;
            failure = null;
            UnityWebRequest request = null;
            try
            {
                var parameters = DownloadedTextureParams.Default;
                parameters.readable = false;
                parameters.mipmapChain = false;
                request = UnityWebRequestTexture.GetTexture(url, parameters);
                request.timeout = 45;
                request.SendWebRequest();
                item = new Pending { key = key, request = request };
                return true;
            }
            catch (Exception exception)
            {
                request?.Dispose();
                Debug.LogError("Reference art request: " + key + " " + exception);
                failure = "界面素材请求失败，请重试";
                return false;
            }
        }

        private static bool TryCacheSprite(Pending item, out string failure)
        {
            failure = null;
            Texture2D texture = null;
            try
            {
                texture = DownloadHandlerTexture.GetContent(item.request);
                if (texture == null || texture.width < 1 || texture.height < 1)
                    throw new InvalidOperationException("Empty PNG texture");
                texture.filterMode = FilterMode.Bilinear;
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.name = item.key;
                Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                    new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
                if (sprite == null) throw new InvalidOperationException("Sprite creation failed");
                sprite.name = item.key;
                Sprites[item.key] = sprite;
                return true;
            }
            catch (Exception exception)
            {
                if (texture != null) UnityEngine.Object.Destroy(texture);
                Debug.LogError("Reference art decode: " + item.key + " " + exception);
                failure = "界面素材读取失败，请重新载入";
                return false;
            }
        }
    }
}
