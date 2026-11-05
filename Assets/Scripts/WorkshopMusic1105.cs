using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace ChoSiren
{
    /// <summary>Server-side Suno 6 songs. Persist the request before POST; resume it rather than pay twice.</summary>
    public static class WorkshopMusic1105
    {
        public static string Endpoint => WorkshopAi1104.Endpoint.TrimEnd('/');
        public static readonly string[] Genres = { "K-pop", "电子", "流行", "摇滚" };
        public static readonly string[] Moods = { "明亮", "热血", "温柔" };
        public static readonly int[] Tempos = { 90, 120, 145 };

        [Serializable] public sealed class Options
        {
            public string title = "我的新歌", genre = "K-pop", mood = "明亮", lyrics = "";
            public bool instrumental = true;
            public int tempo = 120, duration = 40;
        }
        [Serializable] public sealed class Track { public string title, url; }
        [Serializable] public sealed class Reply
        {
            public string task, status, error, model, currency;
            public bool enabled;
            public float price;
            public int remaining = -1;
            public Track[] tracks;
        }
        [Serializable] private sealed class Pending
        {
            public string device, requestId, member, endpoint, created;
            public string title, genre, mood, lyrics;
            public bool instrumental;
            public int tempo, duration;
        }
        [Serializable] private sealed class Saved { public string title, path, savedAt; }
        [Serializable] private sealed class Results { public Track[] tracks; }
        private static string Root => Path.Combine(Application.persistentDataPath, "workshop-music");
        private static string PendingPath => Path.Combine(Root, "pending.json");
        public static bool HasPending => File.Exists(PendingPath);
        public static bool TestBuild => !string.IsNullOrEmpty(WorkshopAi1104.TestCode);

        public static IEnumerator Config(Action<Reply, string> done)
        {
            if (!TestBuild) { done(null, "作曲服务准备中"); yield break; }
            yield return Send("GET", Endpoint + "/music/config", null, done);
        }

        public static IEnumerator Generate(string member, Options options, Action<string> progress, Action<Track[], string> done)
        {
            if (HasPending) { done(null, "上次作曲任务尚未结束，请点继续查询"); yield break; }
            if (!TestBuild) { done(null, "当前版本未开通作曲测试"); yield break; }
            var pending = new Pending { device = SystemInfo.deviceUniqueIdentifier, requestId = Guid.NewGuid().ToString("N"),
                member = member, endpoint = Endpoint, created = DateTime.UtcNow.ToString("o"), title = options.title,
                genre = options.genre, mood = options.mood, lyrics = options.lyrics, instrumental = options.instrumental,
                tempo = options.tempo, duration = options.duration };
            string persistenceError;
            if (!TryCreatePending(pending, out persistenceError))
            { done(null, persistenceError); yield break; }
            progress?.Invoke("正在提交作曲…");
            Reply reply = null; string error = null;
            yield return Send("POST", pending.endpoint + "/music", JsonUtility.ToJson(pending), (r, e) => { reply = r; error = e; });
            if (error != null)
            {
                // Clear only a definite server refusal. Transport failures may already have created a paid task.
                if (reply != null && (reply.status == "refused" || reply.status == "failed")) ClearPending();
                done(null, error + (HasPending ? "；可继续查询原任务" : "")); yield break;
            }
            if (reply == null || reply.status == "refused" || reply.status == "failed")
            { ClearPending(); done(null, reply?.error ?? "作曲请求未被接受"); yield break; }
            yield return Poll(pending, progress, done);
        }

        public static IEnumerator Resume(Action<string> progress, Action<Track[], string> done)
        {
            Pending pending = null;
            try { if (HasPending) pending = JsonUtility.FromJson<Pending>(File.ReadAllText(PendingPath)); } catch { }
            if (pending == null || pending.endpoint != Endpoint || string.IsNullOrEmpty(pending.requestId))
            { done(null, "没有可以恢复的作曲任务"); yield break; }
            yield return Poll(pending, progress, done);
        }

        private static IEnumerator Poll(Pending pending, Action<string> progress, Action<Track[], string> done)
        {
            float start = Time.realtimeSinceStartup;
            int consecutiveErrors = 0;
            while (Time.realtimeSinceStartup - start < 300f)
            {
                progress?.Invoke("正在作曲… " + Mathf.FloorToInt(Time.realtimeSinceStartup - start) + "秒");
                Reply reply = null; string error = null; long httpStatus = 0;
                yield return Send("GET", pending.endpoint + "/music/" + UnityWebRequest.EscapeURL(pending.requestId) +
                    "?device=" + UnityWebRequest.EscapeURL(pending.device), null, (r, e) => { reply = r; error = e; }, s => httpStatus = s);
                if (error != null)
                {
                    bool transient = httpStatus == 0 || httpStatus == 408 || httpStatus == 429 || httpStatus >= 500;
                    if (!transient || ++consecutiveErrors >= 3)
                    { done(null, error + "；任务已保留，可继续查询"); yield break; }
                    progress?.Invoke(error + "，稍后继续查询"); yield return new WaitForSecondsRealtime(5f); continue;
                }
                consecutiveErrors = 0;
                if (reply.status == "done" && reply.tracks != null && reply.tracks.Length > 0)
                {
                    if (!TrySaveResults(reply.tracks))
                    { done(reply.tracks, "歌曲已完成，本机记录保存失败；任务已保留"); yield break; }
                    ClearPending(); done(reply.tracks, null); yield break;
                }
                if (reply.status == "failed" || reply.status == "refused")
                { ClearPending(); done(null, reply.error ?? "歌曲生成失败"); yield break; }
                if (reply.status == "unknown")
                { done(null, reply.error ?? "任务状态待确认，请继续查询原任务"); yield break; }
                yield return new WaitForSecondsRealtime(5f);
            }
            done(null, "作曲还在进行，任务已保留，可继续查询");
        }

        private static bool TryCreatePending(Pending pending, out string error)
        {
            string temporary = PendingPath + "." + pending.requestId + ".tmp";
            error = null;
            try
            {
                Directory.CreateDirectory(Root);
                File.WriteAllText(temporary, JsonUtility.ToJson(pending));
                File.Move(temporary, PendingPath);
                return true;
            }
            catch (IOException) { error = "无法保存作曲任务，请检查本机存储后再试"; }
            catch (UnauthorizedAccessException) { error = "无法写入本机存储，未提交作曲"; }
            finally { TryDelete(temporary); }
            return false;
        }

        private static bool TrySaveResults(Track[] tracks)
        {
            try
            {
                Directory.CreateDirectory(Root);
                File.WriteAllText(Path.Combine(Root, "results.json"), JsonUtility.ToJson(new Results { tracks = tracks }));
                return true;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private static void ClearPending() { TryDelete(PendingPath); }

        public static Track[] LastTracks()
        {
            try
            {
                string file = Path.Combine(Root, "results.json");
                return File.Exists(file) ? JsonUtility.FromJson<Results>(File.ReadAllText(file))?.tracks : null;
            }
            catch { return null; }
        }

        private static IEnumerator Send(string method, string url, string json, Action<Reply, string> done, Action<long> responseStatus = null)
        {
            using (var request = new UnityWebRequest(url, method))
            {
                request.SetRequestHeader("X-Test-Code", WorkshopAi1104.TestCode ?? "");
                request.downloadHandler = new DownloadHandlerBuffer();
                if (json != null)
                {
                    request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
                    request.SetRequestHeader("Content-Type", "application/json");
                }
                request.timeout = 45;
                yield return request.SendWebRequest();
                responseStatus?.Invoke(request.responseCode);
                Reply reply = null;
                try { reply = JsonUtility.FromJson<Reply>(request.downloadHandler.text); } catch { }
                if (request.result != UnityWebRequest.Result.Success)
                {
                    // Only explicit pre-submit HTTP refusals may release the local pending slot.
                    if (method == "POST" && (request.responseCode == 400 || request.responseCode == 403 || request.responseCode == 413 || request.responseCode == 429 || request.responseCode == 503))
                    { if (reply != null) reply.status = "refused"; }
                    done(reply, reply?.error ?? (request.responseCode > 0 ? "作曲服务返回HTTP " + request.responseCode : "作曲连接中断"));
                    yield break;
                }
                string validation = ValidateReply(reply, url.EndsWith("/music/config", StringComparison.Ordinal));
                done(reply, validation);
            }
        }

        private static string ValidateReply(Reply reply, bool config)
        {
            if (reply == null) return "作曲服务返回异常";
            if (config)
            {
                if (!reply.enabled) return reply.error ?? "作曲服务暂未开通";
                return reply.price > 0 && reply.currency == "USD" && !string.IsNullOrEmpty(reply.model) ? null : "作曲报价不完整，请重新连接";
            }
            if (reply.status != "running" && reply.status != "submitting" && reply.status != "done" &&
                reply.status != "failed" && reply.status != "refused" && reply.status != "unknown")
                return reply.error ?? "作曲服务返回异常，任务已保留";
            if (reply.status == "done")
            {
                if (reply.tracks == null || reply.tracks.Length == 0) return "作曲服务未返回歌曲文件";
                foreach (Track track in reply.tracks)
                {
                    if (!Uri.TryCreate(track?.url, UriKind.Absolute, out Uri uri) || uri.Scheme != "https") return "作曲服务返回的歌曲链接不可用";
                    if (string.IsNullOrEmpty(track.title)) track.title = "我的新歌";
                }
            }
            return null;
        }

        public static IEnumerator Load(Track track, Action<AudioClip, string> done)
        {
            if (!Uri.TryCreate(track?.url, UriKind.Absolute, out Uri uri) || uri.Scheme != "https")
            { done(null, "歌曲链接不可用"); yield break; }
            string path = CachePath(track);
            if (!File.Exists(path))
            {
                using (var request = UnityWebRequest.Get(track.url))
                {
                    request.timeout = 60;
                    yield return request.SendWebRequest();
                    if (request.result != UnityWebRequest.Result.Success) { done(null, "歌曲下载未完成，请再点试听"); yield break; }
                    string cacheError;
                    if (!TryCache(path, request.downloadHandler.data, out cacheError)) { done(null, cacheError); yield break; }
                }
            }
            using (var request = UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri, AudioType.MPEG))
            {
                request.timeout = 30;
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success) { TryDelete(path); done(null, "歌曲解码失败，请重新下载试听"); yield break; }
                AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
                if (clip == null) { TryDelete(path); done(null, "歌曲解码失败，请重新下载试听"); yield break; }
                clip.name = track.title; done(clip, null);
            }
        }

        private static bool TryCache(string path, byte[] data, out string error)
        {
            error = null;
            if (data == null || data.Length == 0) { error = "歌曲文件为空，请重新试听"; return false; }
            string temporary = path + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(temporary, data);
                if (!File.Exists(path)) File.Move(temporary, path);
                return true;
            }
            catch (IOException) { error = "歌曲保存失败，请检查本机存储"; }
            catch (UnauthorizedAccessException) { error = "无法写入本机存储"; }
            finally { TryDelete(temporary); }
            return false;
        }

        public static bool Save(Track track, out string error)
        {
            error = null;
            if (track == null || string.IsNullOrEmpty(track.url)) { error = "歌曲链接不可用"; return false; }
            try
            {
                string source = CachePath(track);
                if (!File.Exists(source)) { error = "请先点试听，下载歌曲后再保存"; return false; }
                string folder = Path.Combine(Root, "library"); Directory.CreateDirectory(folder);
                string target = Path.Combine(folder, Path.GetFileName(source));
                if (!File.Exists(target)) File.Copy(source, target);
                File.WriteAllText(target + ".json", JsonUtility.ToJson(new Saved { title = track.title, path = target, savedAt = DateTime.UtcNow.ToString("o") }));
                return true;
            }
            catch (IOException) { error = "歌曲保存失败，请检查本机存储"; }
            catch (UnauthorizedAccessException) { error = "无法写入本机存储"; }
            return false;
        }

        private static string CachePath(Track track)
        {
            string hash;
            using (var sha = System.Security.Cryptography.SHA256.Create())
                hash = BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(track.url))).Replace("-", "").ToLowerInvariant().Substring(0, 24);
            return Path.Combine(Root, "cache", hash + ".mp3");
        }
    }
}
