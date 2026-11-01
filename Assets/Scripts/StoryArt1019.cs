using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace ChoSiren
{
    /// <summary>Native Resources; on Web, original PNG/JPEG bytes loaded once on demand.</summary>
    public static class StoryArt1019
    {
        [Serializable] sealed class Manifest { public Entry[] entries; }
        [Serializable] sealed class Entry { public string key, file; }
        sealed class Pending { public readonly List<Action<Sprite>> callbacks = new List<Action<Sprite>>(); }
        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        static readonly Dictionary<string, Pending> pending = new Dictionary<string, Pending>(StringComparer.Ordinal);
        static readonly List<Texture2D> ownedTextures = new List<Texture2D>();
        static readonly List<Sprite> ownedSprites = new List<Sprite>();
        static Dictionary<string, string> files;
        static StoryArt1019Loader loader;
        static int generation;
        public static int PendingCount => pending.Count;
        public static int CachedCount => sprites.Count;

        public static bool Streaming
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return true;
#elif UNITY_EDITOR
                return EditorUseStreaming;
#else
                return false;
#endif
            }
        }

        public static bool IsManaged(string key) => !string.IsNullOrEmpty(key) &&
            (key.StartsWith("Art/Story/Scenes1019/", StringComparison.Ordinal) ||
             key.StartsWith("Art/Story/Portraits1019/", StringComparison.Ordinal) ||
             key.StartsWith("Art/Story/Transitions1019/", StringComparison.Ordinal));

        public static Sprite Load(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (sprites.TryGetValue(key, out Sprite cached) && cached != null) return cached;
            if (Streaming && IsManaged(key)) return null;
            Sprite native = Resources.Load<Sprite>(key);
            // The profile's original user portrait is imported as a texture, just like transition CGs.
            if (native != null || (!key.StartsWith("Art/Story/Transitions1019/", StringComparison.Ordinal)
                && key != "Art/ProfileAvatarUser")) return native;
            Texture2D texture = Resources.Load<Texture2D>(key);
            if (texture == null) return null;
            native = CreateSprite(key, texture);
            sprites[key] = native; ownedSprites.Add(native);
            return native;
        }

        public static string FirstAvailable(params string[] keys)
        {
            foreach (string key in keys)
                if (!string.IsNullOrEmpty(key) && (Streaming && IsManaged(key) ? Files.ContainsKey(key) : Load(key) != null)) return key;
            return null;
        }

        public static void Prewarm(params string[] keys)
        { foreach (string key in keys) if (IsManaged(key)) Request(key, null); }

        public static void Request(string key, Action<Sprite> complete)
        {
            Sprite cached = Load(key);
            if (cached != null || !Streaming || !IsManaged(key)) { complete?.Invoke(cached); return; }
            if (!Files.TryGetValue(key, out string file)) { complete?.Invoke(null); return; }
#if UNITY_EDITOR
            if (EditorRequestOverride != null) { EditorRequestOverride(key, complete); return; }
#endif
            if (pending.TryGetValue(key, out Pending waiting))
            { if (complete != null) waiting.callbacks.Add(complete); return; }
            var item = new Pending();
            if (complete != null) item.callbacks.Add(complete);
            pending.Add(key, item);
            if (loader == null)
            {
                var go = new GameObject("StoryArt1019Downloads");
                UnityEngine.Object.DontDestroyOnLoad(go);
                loader = go.AddComponent<StoryArt1019Loader>();
            }
            loader.StartCoroutine(Download(key, file, item, generation));
        }

        static Dictionary<string, string> Files
        {
            get
            {
                if (files != null) return files;
                TextAsset json = Resources.Load<TextAsset>("StoryArt1019Manifest");
                files = ReadManifest(json != null ? json.text : null);
                return files;
            }
        }

        static Dictionary<string, string> ReadManifest(string json)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(json)) return result;
            try
            {
                var manifest = JsonUtility.FromJson<Manifest>(json);
                if (manifest?.entries == null) return result;
                foreach (Entry entry in manifest.entries)
                    if (entry != null && IsManaged(entry.key) && !string.IsNullOrEmpty(entry.file) &&
                        entry.file.StartsWith("Story1019/", StringComparison.Ordinal) && !entry.file.Contains("..") &&
                        !entry.file.Contains(":") && !entry.file.Contains("\\")) result[entry.key] = entry.file;
            }
            catch (Exception exception) { Debug.LogWarning("Story1019 manifest unavailable: " + exception.Message); }
            return result;
        }

        static IEnumerator Download(string key, string file, Pending item, int ownerGeneration)
        {
            UnityWebRequest request = null;
            Sprite sprite = null;
            try
            {
                UnityWebRequestAsyncOperation operation = null;
                try
                {
                    var parameters = DownloadedTextureParams.Default; parameters.readable = false; parameters.mipmapChain = false;
                    string root = Application.streamingAssetsPath;
#if UNITY_EDITOR
                    if (!string.IsNullOrEmpty(EditorStreamingRoot)) root = EditorStreamingRoot;
#endif
                    request = UnityWebRequestTexture.GetTexture(root.TrimEnd('/') + "/" + file, parameters);
                    request.timeout = 15;
                    operation = request.SendWebRequest();
                }
                catch (Exception exception) { Debug.LogWarning("Story1019 art keeps its fallback: " + key + " (" + exception.Message + ")"); }
                if (operation != null) yield return operation;
                if (ownerGeneration != generation) yield break;
                if (request != null && request.result == UnityWebRequest.Result.Success)
                {
                    Texture2D texture = null;
                    try
                    {
                        texture = DownloadHandlerTexture.GetContent(request);
                        if (texture != null && texture.width > 0 && texture.height > 0)
                        {
                            texture.filterMode = FilterMode.Bilinear; texture.wrapMode = TextureWrapMode.Clamp; texture.name = key;
                            sprite = CreateSprite(key, texture);
                            sprites[key] = sprite; ownedTextures.Add(texture); ownedSprites.Add(sprite);
                            texture = null; // The finite shared cache now owns the decoded pixels.
                        }
                    }
                    catch (Exception exception) { Debug.LogWarning("Story1019 art keeps its fallback: " + key + " (" + exception.Message + ")"); }
                    finally { if (texture != null) UnityEngine.Object.Destroy(texture); }
                }
                else if (request != null && operation != null)
                    Debug.LogWarning("Story1019 art keeps its fallback: " + key + " (" + request.responseCode + ")");
            }
            finally
            {
                request?.Dispose();
                if (ownerGeneration == generation) pending.Remove(key);
            }
            if (ownerGeneration == generation)
                foreach (Action<Sprite> callback in item.callbacks)
                    try { callback?.Invoke(sprite); } catch (Exception exception) { Debug.LogException(exception); }
        }

        static Sprite CreateSprite(string key, Texture2D texture)
        {
            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * .5f, 100f, 0, SpriteMeshType.FullRect);
            sprite.name = key.Substring(key.LastIndexOf('/') + 1); return sprite;
        }

#if UNITY_EDITOR
        public static bool EditorUseStreaming;
        public static string EditorStreamingRoot;
        public static Action<string, Action<Sprite>> EditorRequestOverride;
        public static void ConfigureEditorStreamingFixture(string manifest, string root, Action<string, Action<Sprite>> requests = null)
        {
            ResetEditorFixture(); EditorUseStreaming = true; EditorStreamingRoot = root;
            EditorRequestOverride = requests; files = ReadManifest(manifest);
        }
        public static void ResetEditorFixture()
        {
            generation++;
            if (loader != null) { loader.StopAllCoroutines(); UnityEngine.Object.DestroyImmediate(loader.gameObject); loader = null; }
            pending.Clear();
            foreach (Sprite sprite in ownedSprites) if (sprite != null) UnityEngine.Object.DestroyImmediate(sprite);
            foreach (Texture2D texture in ownedTextures) if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
            sprites.Clear(); ownedSprites.Clear(); ownedTextures.Clear(); files = null;
            EditorUseStreaming = false; EditorStreamingRoot = null; EditorRequestOverride = null;
        }
#endif
    }

    public sealed class StoryArt1019Loader : MonoBehaviour { }
}
