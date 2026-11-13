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
        public static string Endpoint = "https://1318245308-d7n1q0c0wj.ap-guangzhou.tencentscf.com";
        public const string PendingNotice = "AI 服务开通中，开通后就能在这里直接生成";
        public static bool Connected => !string.IsNullOrEmpty(ActiveEndpoint);

        /// <summary>
        /// 2026-11-05 (用户在美国出差，自己测试游戏里的 GPT 换装): GPT Image 2.6 personal test service in Tencent Cloud
        /// Singapore. Used only when the local, never-committed file Resources/AiGptTestCode.txt exists (it holds the
        /// test code the service requires); public builds don't have it and keep <see cref="Endpoint"/>.
        /// </summary>
        public const string GptTestEndpoint = "https://1318245308-m2ju450cfv.ap-singapore.tencentscf.com";
        private static string testCode;
        private static bool testCodeLoaded;
        public static string TestCode
        {
            get
            {
                if (!testCodeLoaded)
                {
                    testCodeLoaded = true;
                    TextAsset file = Resources.Load<TextAsset>("AiGptTestCode");
                    testCode = file != null ? file.text.Trim() : null;
                }
                return testCode;
            }
        }
        public static string ActiveEndpoint => string.IsNullOrEmpty(TestCode) ? Endpoint : GptTestEndpoint;
        /// <summary>Generations this device has left today, from the service (-1 = not known yet).</summary>
        public static int RemainingToday = -1;
        public const int DailyLimit = 5;

        public static readonly string[] StyleTags =
        {
            "偶像演出服", "甜酷街头", "学院制服", "晚礼服", "赛博朋克", "国风", "运动风", "冬日针织",
        };

        /// <summary>What each tag asks the model for: a clearly different outfit, not a touch-up of the current one.</summary>
        public static readonly string[] StylePrompts =
        {
            "全新的亮片舞台演出服，白金配色，蓬蓬短裙、蝴蝶结和闪亮长靴",
            "甜酷街头风：宽松短款卫衣、百褶短裙、厚底运动鞋，粉黑配色",
            "日系学院制服：白衬衫、领结、格纹百褶裙、及膝袜和乐福鞋",
            "缎面长款晚礼服，星空渐变色，裙摆及地，配长手套",
            "赛博朋克机能风：发光线条短外套、紧身裤、霓虹配色的厚底靴",
            "改良汉服：交领上衣、飘逸长裙和披帛，淡雅刺绣",
            "运动风：运动背心、短款运动外套、运动短裤和球鞋",
            "冬日针织：米白高领毛衣、格纹围巾、针织短裙和长靴",
        };

        /// <summary>The outfit request for a tag plus the player's own words.</summary>
        public static string PromptFor(string tag, string extra)
        {
            int i = Array.IndexOf(StyleTags, tag);
            string wish = i >= 0 ? StylePrompts[i] : tag;
            return string.IsNullOrWhiteSpace(extra) ? wish : wish + "，" + extra.Trim();
        }

        [Serializable] private sealed class Request { public string device, member, prompt, image; }
        [Serializable] private sealed class Reply { public string task, status, url, error; public int remaining = -1; }
        [Serializable] private sealed class Pending
        {
            public string member, task, endpoint;
            public bool submitting;
            public int x, y, width, height, imageWidth, imageHeight;
        }

        private static string PendingPath(string member)
        {
            if (string.IsNullOrEmpty(member) || member.Length > 100) return null;
            foreach (char c in member) if (!char.IsLetterOrDigit(c) && c != '-' && c != '_') return null;
            return Path.Combine(Application.persistentDataPath, "workshop-jobs", member + ".json");
        }

        private static Pending LoadPending(string member, out string error) =>
            LoadPendingAtPath(member, ActiveEndpoint.TrimEnd('/'), PendingPath(member), out error);

        private static Pending LoadPendingAtPath(string member, string endpoint, string path, out string error)
        {
            error = null;
            try
            {
                if (path == null) { error = "成员标识异常，未提交生成"; return null; }
                if (!File.Exists(path))
                {
                    if (File.Exists(path + ".tmp")) error = "上次任务记录未保存完整，未重复生成";
                    return null;
                }
                Pending job = JsonUtility.FromJson<Pending>(File.ReadAllText(path));
                if (job == null || job.member != member || job.imageWidth <= 0 || job.imageHeight <= 0
                    || job.width <= 0 || job.height <= 0 || job.x < 0 || job.y < 0
                    || job.x > job.imageWidth - job.width || job.y > job.imageHeight - job.height
                    || (!job.submitting && string.IsNullOrEmpty(job.task)))
                { error = "上次任务记录异常，未重复生成"; return null; }
                if (job.endpoint != endpoint)
                { error = "上次任务属于另一生成服务，未重复提交"; return null; }
                return job;
            }
            catch { error = "上次任务记录无法读取，未重复生成"; return null; }
        }

        public static bool HasPending(string member)
        {
            Pending job = LoadPending(member, out string error);
            return job != null || error != null;
        }

        private static bool SavePending(Pending job) => SavePendingAtPath(job, PendingPath(job.member));

        private static bool SavePendingAtPath(Pending job, string path)
        {
            try
            {
                if (path == null) return false;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string temporary = path + ".tmp";
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(job));
                using (var file = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    file.Write(bytes, 0, bytes.Length);
                    file.Flush(true);
                }
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
                return true;
            }
            catch { Debug.LogWarning("[WorkshopAi] could not save generation progress"); return false; }
        }

        private static void ClearPending(string member)
        {
            try { string path = PendingPath(member); if (path != null && File.Exists(path)) File.Delete(path); }
            catch { Debug.LogWarning("[WorkshopAi] could not clear generation progress"); }
        }

        /// <summary>Generates an outfit. progress gets status text; done gets the texture (or null and an error).</summary>
        public static IEnumerator Generate(string memberId, Sprite still, string prompt, Action<string> progress,
            Action<Texture2D, string> done)
        {
            if (!Connected) { done(null, PendingNotice); yield break; }
            if (still == null) { done(null, "没有可用的立绘"); yield break; }
            string endpoint = ActiveEndpoint.TrimEnd('/');
            Pending job = LoadPending(memberId, out string loadError);
            if (loadError != null) { done(null, loadError); yield break; }
            if (job != null && string.IsNullOrEmpty(job.task))
            { done(null, "上次提交结果尚未确认，请勿重复生成"); yield break; }
            bool saved = job != null;
            if (job == null)
            {
                progress?.Invoke("正在上传立绘…");
                byte[] png = EncodeStill(still, 1024, out RectInt figure, out Vector2Int stillSize);
                var body = new Request
                {
                    device = SystemInfo.deviceUniqueIdentifier, member = memberId, prompt = prompt,
                    image = Convert.ToBase64String(png),
                };
                job = new Pending { member = memberId, endpoint = endpoint, submitting = true,
                    x = figure.x, y = figure.y, width = figure.width, height = figure.height,
                    imageWidth = stillSize.x, imageHeight = stillSize.y };
                if (!SavePending(job))
                { done(null, "无法保存生成进度，本次未提交，请检查存储空间"); yield break; }
                Reply reply = null; string error = null; long submitStatus = 0;
                yield return Send(UnityWebRequest.kHttpVerbPOST, endpoint + "/dress", JsonUtility.ToJson(body),
                    r => reply = r, e => error = e, responseStatus: status => submitStatus = status);
                if (error != null && error.Contains("次数用完")) RemainingToday = 0;
                if (error != null || reply == null || string.IsNullOrEmpty(reply.task))
                {
                    if (submitStatus == 400 || submitStatus == 403 || submitStatus == 413 || submitStatus == 429)
                        ClearPending(memberId);
                    done(null, error ?? "服务未确认任务，可能已提交，请勿重复生成"); yield break;
                }
                if (reply.remaining >= 0) RemainingToday = reply.remaining;
                job.task = reply.task; job.submitting = false;
                saved = SavePending(job);
                if (!saved) progress?.Invoke("任务已接收但暂存失败，请保持此页面");
            }
            else progress?.Invoke("继续上次生成…");

            float started = Time.unscaledTime;
            int transientFailures = 0;
            while (Time.unscaledTime - started < 600f)
            {
                progress?.Invoke($"AI 正在换装… {Mathf.FloorToInt(Time.unscaledTime - started)} 秒");
                yield return new WaitForSecondsRealtime(2.5f);
                Reply poll = null; string pollError = null; bool retryable = false;
                yield return Send(UnityWebRequest.kHttpVerbGET, endpoint + "/dress/" + UnityWebRequest.EscapeURL(job.task), null,
                    r => poll = r, e => pollError = e, r => retryable = r);
                if (pollError != null)
                {
                    if (!retryable || ++transientFailures >= 3)
                    { done(null, pollError + (saved ? "；任务已保留，可继续查询" : "；任务暂存失败，请保持页面")); yield break; }
                    progress?.Invoke("连接暂时中断，正在恢复查询…");
                    continue;
                }
                transientFailures = 0;
                if (poll == null) { done(null, saved ? "服务回复异常，任务已保留" : "服务回复异常，任务暂存失败"); yield break; }
                if (poll.status == "failed")
                { done(null, "本次未能完成生成" + (saved ? "，任务记录已保留" : "，任务暂存失败")); yield break; }
                if (poll.status != "done" || string.IsNullOrEmpty(poll.url)) continue;
                progress?.Invoke("正在下载结果…");
                Texture2D raw = null; string downloadError = null;
                yield return DownloadResult(poll.url, progress, image => raw = image, message => downloadError = message);
                if (raw == null)
                { done(null, (downloadError ?? "结果下载未完成") + (saved ? "；任务已保留，可继续查询" : "；任务暂存失败")); yield break; }
                progress?.Invoke("正在抠图…");
                yield return null;
                Texture2D texture = FitToStill(KeyGreen(raw), new RectInt(job.x, job.y, job.width, job.height),
                    new Vector2Int(job.imageWidth, job.imageHeight));
                UnityEngine.Object.Destroy(raw);
                texture.name = "workshop-ai-" + memberId;
                ClearPending(memberId);
                done(texture, null);
                yield break;
            }
            done(null, saved ? "还在生成中，任务已保留，可稍后继续查询" : "还在生成中，暂存失败，请保持页面");
        }

        private static IEnumerator DownloadResult(string url, Action<string> progress, Action<Texture2D> done,
            Action<string> fail)
        {
            // GET is safe to repeat. Never re-submit the paid generation after a slow/truncated download.
            for (int attempt = 0; attempt < 2; attempt++)
            {
                using (UnityWebRequest image = UnityWebRequest.Get(url))
                {
                    image.timeout = 120;
                    UnityWebRequestAsyncOperation operation = image.SendWebRequest();
                    ulong previousKb = ulong.MaxValue;
                    while (!operation.isDone)
                    {
                        ulong kb = image.downloadedBytes / 1024;
                        if (kb != previousKb)
                        { previousKb = kb; progress?.Invoke($"正在下载结果… {kb} KB"); }
                        yield return null;
                    }
                    // Some gateways finish the complete payload but stall closing the response. Accept only a
                    // structurally complete image, never a partial PNG that happened to reach the phone first.
                    Texture2D decoded = image.responseCode == 200 || image.result == UnityWebRequest.Result.Success
                        ? DecodeImage(image.downloadHandler.data) : null;
                    if (decoded != null) { done(decoded); yield break; }
                    bool retryable = image.responseCode == 0 || image.responseCode == 200 || image.responseCode == 408
                        || image.responseCode == 429 || image.responseCode >= 500;
                    Debug.LogWarning($"[WorkshopAi] phase=download result={image.result} http={image.responseCode} bytes={image.downloadedBytes}");
                    if (!retryable || attempt == 1)
                    { fail(retryable ? "结果下载暂时中断" : "结果暂时无法下载"); yield break; }
                }
                progress?.Invoke("下载连接中断，正在重试已生成的结果…");
                yield return new WaitForSecondsRealtime(1f);
            }
        }

        /// <summary>PNG/JPEG bytes, or the same image as base64 text (the Singapore function URL sends it that way).</summary>
        private static Texture2D DecodeImage(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 8) return null;
            if (bytes[0] == (byte)'i' && bytes[1] == (byte)'V' && bytes[2] == (byte)'B')
            {
                try { bytes = Convert.FromBase64String(System.Text.Encoding.ASCII.GetString(bytes).Trim()); }
                catch { return null; }
            }
            bool png = bytes.Length >= 20 && bytes[0] == 137 && bytes[1] == 80 && bytes[2] == 78 && bytes[3] == 71;
            bool jpeg = bytes.Length >= 4 && bytes[0] == 255 && bytes[1] == 216;
            if (png)
            {
                byte[] ending = { 0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130 };
                for (int i = 0; i < ending.Length; i++)
                    if (bytes[bytes.Length - ending.Length + i] != ending[i]) return null;
            }
            else if (!jpeg || bytes[bytes.Length - 2] != 255 || bytes[bytes.Length - 1] != 217) return null;
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (texture.LoadImage(bytes)) return texture;
            UnityEngine.Object.Destroy(texture);
            return null;
        }

        private static IEnumerator Send(string verb, string url, string json, Action<Reply> ok, Action<string> fail,
            Action<bool> retryable = null, Action<long> responseStatus = null)
        {
            using (var request = new UnityWebRequest(url, verb))
            {
                if (!string.IsNullOrEmpty(TestCode)) request.SetRequestHeader("X-Test-Code", TestCode);
                if (json != null)
                {
                    request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
                    request.SetRequestHeader("Content-Type", "application/json");
                    request.useHttpContinue = false;
                }
                request.downloadHandler = new DownloadHandlerBuffer();
                bool submitting = verb == UnityWebRequest.kHttpVerbPOST;
                request.timeout = submitting ? 90 : 30;
                yield return request.SendWebRequest();
                responseStatus?.Invoke(request.responseCode);
                if (request.result != UnityWebRequest.Result.Success)
                {
                    // The service answers refusals (429 daily limit, 400, 502) with {error}; show that, not "network".
                    string message = WorkshopRequestError1105.Message(request.responseCode,
                        request.downloadHandler?.text, request.error, submitting, out bool canRetry);
                    if (message.Contains("次数用完")) RemainingToday = 0;
                    retryable?.Invoke(canRetry);
                    // No body, endpoint, task identifier, test code or provider key is written to logs.
                    Debug.LogWarning($"[WorkshopAi] phase={(submitting ? "submit" : "query")} result={request.result} http={request.responseCode} message={message}");
                    fail(message);
                    yield break;
                }
                Reply reply = null;
                try { reply = JsonUtility.FromJson<Reply>(request.downloadHandler.text); }
                catch { /* Classify malformed replies below; never expose their raw text. */ }
                if (reply == null || (string.IsNullOrEmpty(reply.task) && string.IsNullOrEmpty(reply.status)))
                {
                    string message = WorkshopRequestError1105.Message(request.responseCode,
                        request.downloadHandler.text, null, submitting, out bool canRetry);
                    retryable?.Invoke(canRetry); fail(message); yield break;
                }
                ok(reply);
            }
        }

        /// <summary>PNG of a (possibly non-readable) sprite, scaled to at most maxHeight, via a GPU blit; also the
        /// figure's opaque bounds and the image size, so the result can be framed like her still.</summary>
        public static byte[] EncodeStill(Sprite sprite, int maxHeight, out RectInt figure, out Vector2Int size)
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
            size = new Vector2Int(w, h);
            figure = OpaqueBounds(copy.GetPixels32(), w, h);
            byte[] png = copy.EncodeToPNG();
            UnityEngine.Object.Destroy(copy);
            return png;
        }

        // ------------------------------------------------------------------ green key

        /// <summary>
        /// The model paints her on a plain green backdrop (often a soft, not pure, green with a floor shadow). The
        /// backdrop colour is read from the border; pixels are scored by how much greener than red/blue they are
        /// relative to it; everything that scores high and is connected to the border is the backdrop (so a green
        /// dress inside her outline survives), enclosed holes are removed only when they match the backdrop closely,
        /// and a soft edge plus despill removes the green fringe. Returns a new readable RGBA texture.
        /// </summary>
        public static Texture2D KeyGreen(Texture2D source)
        {
            int w = source.width, h = source.height, n = w * h;
            Color32[] px = source.GetPixels32();
            // Backdrop colour: median of a 6px border strip.
            var rs = new System.Collections.Generic.List<int>(); var gs = new System.Collections.Generic.List<int>(); var bs = new System.Collections.Generic.List<int>();
            for (int y = 0; y < h; y += 3)
                for (int x = 0; x < w; x += 3)
                {
                    if (x >= 6 && x < w - 6 && y >= 6 && y < h - 6) continue;
                    Color32 c = px[y * w + x]; rs.Add(c.r); gs.Add(c.g); bs.Add(c.b);
                }
            rs.Sort(); gs.Sort(); bs.Sort();
            int br = rs[rs.Count / 2], bg = gs[gs.Count / 2], bb = bs[bs.Count / 2];
            float bgGreen = Mathf.Max(24f, bg - Mathf.Max(br, bb));

            var score = new float[n];
            for (int i = 0; i < n; i++)
            {
                Color32 c = px[i];
                score[i] = Mathf.Clamp01((c.g - Mathf.Max(c.r, c.b)) / bgGreen);
            }

            // Backdrop = high-scoring pixels connected to the border.
            var back = new bool[n];
            var stack = new int[n]; int top = 0;
            void Seed(int i) { if (!back[i] && score[i] > .35f) { back[i] = true; stack[top++] = i; } }
            for (int x = 0; x < w; x++) { Seed(x); Seed((h - 1) * w + x); }
            for (int y = 0; y < h; y++) { Seed(y * w); Seed(y * w + w - 1); }
            while (top > 0)
            {
                int i = stack[--top], x = i % w;
                if (x > 0) Seed(i - 1);
                if (x < w - 1) Seed(i + 1);
                if (i >= w) Seed(i - w);
                if (i < n - w) Seed(i + w);
            }
            // Enclosed gaps (between an arm and the body) that are the backdrop colour itself.
            for (int i = 0; i < n; i++)
            {
                if (back[i] || score[i] < .65f) continue;
                Color32 c = px[i];
                if (Mathf.Abs(c.r - br) + Mathf.Abs(c.g - bg) + Mathf.Abs(c.b - bb) < 90) back[i] = true;
            }
            // The edge band: two pixels around the backdrop get a soft alpha and despill.
            var edge = new bool[n];
            for (int i = 0; i < n; i++)
            {
                if (!back[i]) continue;
                int x = i % w, y = i / w;
                for (int dy = -2; dy <= 2; dy++)
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        int xx = x + dx, yy = y + dy;
                        if (xx >= 0 && xx < w && yy >= 0 && yy < h) edge[yy * w + xx] = true;
                    }
            }
            for (int i = 0; i < n; i++)
            {
                if (!edge[i]) continue;
                Color32 c = px[i];
                float a = back[i] ? Mathf.Clamp01((.55f - score[i]) / .4f) : Mathf.Clamp01((.75f - score[i]) / .5f);
                if (a <= 0f) { px[i] = new Color32(0, 0, 0, 0); continue; }
                byte limit = (byte)Mathf.Max(c.r, c.b);
                if (c.g > limit) c.g = limit;
                c.a = (byte)Mathf.RoundToInt(a * 255f);
                px[i] = c;
            }
            var result = new Texture2D(w, h, TextureFormat.RGBA32, false);
            result.SetPixels32(px); result.Apply();
            return result;
        }

        /// <summary>Opaque bounds (alpha above ~10%) of a pixel block; the whole image when it has none.</summary>
        public static RectInt OpaqueBounds(Color32[] px, int w, int h)
        {
            int x0 = w, y0 = h, x1 = -1, y1 = -1;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (px[y * w + x].a < 26) continue;
                    if (x < x0) x0 = x; if (x > x1) x1 = x;
                    if (y < y0) y0 = y; if (y > y1) y1 = y;
                }
            return x1 < 0 ? new RectInt(0, 0, w, h) : new RectInt(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
        }

        /// <summary>
        /// Places the keyed figure on a canvas framed like her still: same image aspect, the figure as tall as hers and
        /// standing in the same place (feet on her feet, centred on her), so the workshop's face close-ups still land.
        /// </summary>
        public static Texture2D FitToStill(Texture2D keyed, RectInt stillFigure, Vector2Int stillSize)
        {
            int w = keyed.width, h = keyed.height;
            Color32[] src = keyed.GetPixels32();
            RectInt fig = OpaqueBounds(src, w, h);
            if (stillSize.x <= 0 || stillSize.y <= 0 || stillFigure.height <= 0) return keyed;
            // Canvas scale keeps the AI figure at native resolution.
            float k = fig.height / (float)stillFigure.height;
            int cw = Mathf.Clamp(Mathf.RoundToInt(stillSize.x * k), 8, 4096), ch = Mathf.Clamp(Mathf.RoundToInt(stillSize.y * k), 8, 4096);
            int ox = Mathf.RoundToInt((stillFigure.x + stillFigure.width * .5f) * k - (fig.x + fig.width * .5f));
            int oy = Mathf.RoundToInt(stillFigure.y * k - fig.y);
            var dst = new Color32[cw * ch];
            for (int y = fig.y; y < fig.y + fig.height; y++)
            {
                int ty = y + oy;
                if (ty < 0 || ty >= ch) continue;
                for (int x = fig.x; x < fig.x + fig.width; x++)
                {
                    int tx = x + ox;
                    if (tx < 0 || tx >= cw) continue;
                    dst[ty * cw + tx] = src[y * w + x];
                }
            }
            var result = new Texture2D(cw, ch, TextureFormat.RGBA32, false);
            result.SetPixels32(dst); result.Apply();
            if (Application.isPlaying) UnityEngine.Object.Destroy(keyed); else UnityEngine.Object.DestroyImmediate(keyed);
            return result;
        }

        // ------------------------------------------------------------------ wardrobe (this device)

        private static string Folder(string memberId) => Path.Combine(Application.persistentDataPath, "workshop-looks", memberId ?? "member");

        public static string SaveToWardrobe(string memberId, Texture2D texture)
        {
            string folder = Folder(memberId);
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".png");
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

        public static string OutfitPath1112(string memberId, string file) => Path.Combine(Folder(memberId), file ?? string.Empty);

        public static bool DeleteOutfit1112(string memberId, string file)
        {
            string path = OutfitPath1112(memberId, file);
            ForgetWornSprite1112(memberId);
            try { if (File.Exists(path)) { File.Delete(path); return true; } }
            catch (Exception error) { Debug.LogWarning("[Wardrobe] could not delete an outfit: " + error.Message); }
            return false;
        }

        private static readonly System.Collections.Generic.Dictionary<string, (string File, Sprite Sprite)> wornSprites1112 =
            new System.Collections.Generic.Dictionary<string, (string, Sprite)>();

        /// <summary>Her worn outfit as a full-portrait sprite (loaded once and kept until she changes).</summary>
        public static Sprite WornSprite1112(string memberId, string file)
        {
            if (string.IsNullOrEmpty(memberId) || string.IsNullOrEmpty(file)) return null;
            if (wornSprites1112.TryGetValue(memberId, out var cached) && cached.File == file && cached.Sprite != null) return cached.Sprite;
            Texture2D texture = Load(OutfitPath1112(memberId, file));
            if (texture == null) return null;
            texture.name = "Outfit-" + memberId;
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 100f);
            sprite.name = "Outfit-" + memberId;
            wornSprites1112[memberId] = (file, sprite);
            return sprite;
        }

        public static void ForgetWornSprite1112(string memberId)
        {
            if (memberId != null) wornSprites1112.Remove(memberId);
        }

        public static Texture2D Load(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            return texture.LoadImage(File.ReadAllBytes(path)) ? texture : null;
        }
    }
}
