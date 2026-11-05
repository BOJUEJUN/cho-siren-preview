using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace ChoSiren
{
    /// <summary>
    /// 2026-11-04 创意工坊 AI 换装, in game (用户: "不要跳转到网页，我要在游戏里面就能实现 AI 生图"). The game never holds a
    /// model key: it posts the member's still and the outfit request to the game's own generation service, which keeps
    /// the RunningHub key, calls an image-edit model that keeps the face and changes only the outfit, limits each device
    /// per day, and answers with an image URL. Contract (JSON over HTTPS):
    ///   POST {Endpoint}/dress        {device, member, prompt, image (PNG base64)} → {task, remaining} or {error}
    ///   GET  {Endpoint}/dress/{task} → {status: "running"|"done"|"failed", url?, error?, remaining?}
    /// Until <see cref="Endpoint"/> is set the client answers 「AI 服务开通中」 and nothing is sent anywhere.
    /// Results are kept on this device under persistentDataPath/workshop-looks/&lt;member&gt;/.
    /// </summary>
    public static class WorkshopAi1104
    {
        /// <summary>The game's generation service (empty = not connected yet).</summary>
        public static string Endpoint = string.Empty;
        public const string PendingNotice = "AI 服务开通中，开通后就能在这里直接生成";
        public static bool Connected => !string.IsNullOrEmpty(Endpoint);

        public static readonly string[] StyleTags =
        {
            "偶像演出服", "甜酷街头", "学院制服", "晚礼服", "赛博朋克", "国风", "运动风", "冬日针织",
        };

        [Serializable] private sealed class Request { public string device, member, prompt, image; }
        [Serializable] private sealed class Reply { public string task, status, url, error; public int remaining = -1; }

        /// <summary>Generates an outfit. progress gets status text; done gets the texture (or null and an error).</summary>
        public static IEnumerator Generate(string memberId, Sprite still, string prompt, Action<string> progress,
            Action<Texture2D, string> done)
        {
            if (!Connected) { done(null, PendingNotice); yield break; }
            if (still == null) { done(null, "没有可用的立绘"); yield break; }
            progress?.Invoke("正在上传立绘…");
            var body = new Request
            {
                device = SystemInfo.deviceUniqueIdentifier, member = memberId, prompt = prompt,
                image = Convert.ToBase64String(EncodeStill(still, 1024)),
            };
            Reply reply = null; string error = null;
            yield return Send(UnityWebRequest.kHttpVerbPOST, Endpoint.TrimEnd('/') + "/dress", JsonUtility.ToJson(body),
                r => reply = r, e => error = e);
            if (error != null || reply == null || string.IsNullOrEmpty(reply.task)) { done(null, error ?? reply?.error ?? "生成请求失败"); yield break; }

            float started = Time.unscaledTime;
            while (Time.unscaledTime - started < 180f)
            {
                progress?.Invoke($"AI 正在换装… {Mathf.FloorToInt(Time.unscaledTime - started)} 秒");
                yield return new WaitForSecondsRealtime(2.5f);
                Reply poll = null; string pollError = null;
                yield return Send(UnityWebRequest.kHttpVerbGET, Endpoint.TrimEnd('/') + "/dress/" + UnityWebRequest.EscapeURL(reply.task), null,
                    r => poll = r, e => pollError = e);
                if (pollError != null) continue;
                if (poll.status == "failed") { done(null, poll.error ?? "生成失败，换个描述再试试"); yield break; }
                if (poll.status != "done" || string.IsNullOrEmpty(poll.url)) continue;
                progress?.Invoke("正在下载结果…");
                using (UnityWebRequest image = UnityWebRequestTexture.GetTexture(poll.url))
                {
                    image.timeout = 60;
                    yield return image.SendWebRequest();
                    if (image.result != UnityWebRequest.Result.Success) { done(null, "结果下载失败"); yield break; }
                    Texture2D texture = DownloadHandlerTexture.GetContent(image);
                    texture.name = "workshop-ai-" + memberId;
                    done(texture, null);
                    yield break;
                }
            }
            done(null, "生成超时，请稍后再试");
        }

        private static IEnumerator Send(string verb, string url, string json, Action<Reply> ok, Action<string> fail)
        {
            using (var request = new UnityWebRequest(url, verb))
            {
                if (json != null)
                {
                    request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
                    request.SetRequestHeader("Content-Type", "application/json");
                }
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = 30;
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success) { fail("网络连接失败"); yield break; }
                Reply reply;
                try { reply = JsonUtility.FromJson<Reply>(request.downloadHandler.text); }
                catch { fail("服务返回异常"); yield break; }
                if (reply == null) { fail("服务返回异常"); yield break; }
                if (!string.IsNullOrEmpty(reply.error) && string.IsNullOrEmpty(reply.task) && string.IsNullOrEmpty(reply.status))
                { fail(reply.error); yield break; }
                ok(reply);
            }
        }

        /// <summary>PNG of a (possibly non-readable) sprite, scaled to at most maxHeight, via a GPU blit.</summary>
        public static byte[] EncodeStill(Sprite sprite, int maxHeight)
        {
            Rect r = sprite.textureRect;
            float scale = Mathf.Min(1f, maxHeight / r.height);
            int w = Mathf.Max(8, Mathf.RoundToInt(r.width * scale)), h = Mathf.Max(8, Mathf.RoundToInt(r.height * scale));
            RenderTexture rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            Vector2 tex = new Vector2(sprite.texture.width, sprite.texture.height);
            Graphics.Blit(sprite.texture, rt, new Vector2(r.width / tex.x, r.height / tex.y), new Vector2(r.x / tex.x, r.y / tex.y));
            RenderTexture previous = RenderTexture.active; RenderTexture.active = rt;
            var copy = new Texture2D(w, h, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, w, h), 0, 0); copy.Apply();
            RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt);
            byte[] png = copy.EncodeToPNG();
            UnityEngine.Object.Destroy(copy);
            return png;
        }

        // ------------------------------------------------------------------ wardrobe (this device)

        private static string Folder(string memberId) => Path.Combine(Application.persistentDataPath, "workshop-looks", memberId ?? "member");

        public static string SaveToWardrobe(string memberId, Texture2D texture)
        {
            string folder = Folder(memberId);
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".png");
            File.WriteAllBytes(path, texture.EncodeToPNG());
            return path;
        }

        public static string[] Wardrobe(string memberId)
        {
            string folder = Folder(memberId);
            if (!Directory.Exists(folder)) return Array.Empty<string>();
            string[] files = Directory.GetFiles(folder, "*.png");
            Array.Sort(files, StringComparer.Ordinal);
            Array.Reverse(files);
            return files;
        }

        public static Texture2D Load(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            return texture.LoadImage(File.ReadAllBytes(path)) ? texture : null;
        }
    }
}
