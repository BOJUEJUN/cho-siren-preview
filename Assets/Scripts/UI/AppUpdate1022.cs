using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using ChoSiren.UI;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

namespace ChoSiren
{
    /// <summary>
    /// Installed builds check for a newer version on start (2026-10-22).
    /// The manifest lives on Tencent COS: app-version.json
    /// {version, android, windows, mac, windowsUpdate, macUpdate, notes, changelog:[{version, notes}]}.
    /// The offer appears on the title screen (changes since the player's own version, 立即更新 / 稍后) and the
    /// download reuses the title's loading bar, so the side buttons stay usable; if the player is already in
    /// the lobby a plate shows instead. Windows / Mac update in place: the zip is downloaded, a small script
    /// waits for the game to quit, swaps the files and starts the new version (saves live in the registry /
    /// ~/Library, not next to the game). Android opens the APK link (the system install prompt cannot be
    /// skipped). Silent on any network error.
    /// Desktop builds start the update right away (closable notice); Android keeps a 「去下载」 button.
    /// Test hook: -updateManifest &lt;url&gt; points at another manifest.
    /// </summary>
    public sealed class AppUpdate1022 : MonoBehaviour
    {
        // Tencent COS (Guangzhou): fast inside China, unlike GitHub. Uploaded by Tools/cos_upload.py.
        public const string ManifestUrl = "https://cho-siren-1318245308.cos.ap-guangzhou.myqcloud.com/app-version.json";

        [Serializable] private sealed class Entry { public string version, notes; }

        [Serializable]
        private sealed class Manifest
        {
            public string version, android, windows, mac, windowsUpdate, macUpdate, windowsFiles, macFiles, notes;
            public Entry[] changelog;
        }

        // Delta updates (2026-10-22): {version, base, files:[{path, size, sha256}]}; each file lives at base + sha256.
        [Serializable] private sealed class FileEntry { public string path, sha256; public long size; }
        [Serializable] private sealed class FileList { public string version, @base; public FileEntry[] files; }

        public static void CheckLater(MonoBehaviour host, Transform canvasChild, Font font, Func<NativeTitleScreen> title)
        {
#if (UNITY_ANDROID || UNITY_STANDALONE) && !UNITY_EDITOR
            if (host != null) host.StartCoroutine(Check(host, canvasChild, font, title));
#endif
        }

        /// <summary>True when remote (x.y.z) is newer than local.</summary>
        public static bool IsNewer(string remote, string local)
        {
            int[] a = Parse(remote), b = Parse(local);
            for (int i = 0; i < 3; i++) if (a[i] != b[i]) return a[i] > b[i];
            return false;
        }

        static int[] Parse(string version)
        {
            var parts = new int[3];
            string[] bits = (version ?? string.Empty).Trim().TrimStart('v', 'V').Split('.');
            for (int i = 0; i < 3 && i < bits.Length; i++) int.TryParse(bits[i], out parts[i]);
            return parts;
        }

        static string Arg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                    return i + 1 < args.Length ? args[i + 1] : string.Empty;
            return null;
        }

        /// <summary>Every version newer than the player's, newest first: "v0.7.0  · …".</summary>
        static string Changes(Manifest manifest, string local)
        {
            var lines = new List<string>();
            if (manifest.changelog != null)
                foreach (Entry entry in manifest.changelog)
                    if (entry != null && IsNewer(entry.version, local) && !string.IsNullOrEmpty(entry.notes))
                        lines.Add($"v{entry.version}　{entry.notes}");
            if (lines.Count == 0 && !string.IsNullOrEmpty(manifest.notes)) lines.Add($"v{manifest.version}　{manifest.notes}");
            if (lines.Count == 0) lines.Add("修复与优化。");
            return string.Join("\n", lines);
        }

        static IEnumerator Check(MonoBehaviour host, Transform canvasChild, Font font, Func<NativeTitleScreen> title)
        {
            yield return new WaitForSecondsRealtime(.5f);
            string manifestUrl = Arg("-updateManifest") ?? ManifestUrl;
            Manifest manifest = null;
            using (var request = UnityWebRequest.Get(manifestUrl + "?t=" + DateTime.UtcNow.Ticks))
            {
                request.timeout = 10;
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success) yield break;
                try { manifest = JsonUtility.FromJson<Manifest>(request.downloadHandler.text); }
                catch (Exception) { yield break; }
            }
            if (manifest == null || !IsNewer(manifest.version, Application.version)) yield break;
#if UNITY_ANDROID
            string page = manifest.android, package = null, files = null;
#elif UNITY_STANDALONE_OSX
            string page = manifest.mac, package = manifest.macUpdate, files = manifest.macFiles;
#else
            string page = manifest.windows, package = manifest.windowsUpdate, files = manifest.windowsFiles;
#endif
            if (string.IsNullOrEmpty(page) && string.IsNullOrEmpty(package)) yield break;
            bool inPlace = (!string.IsNullOrEmpty(package) || !string.IsNullOrEmpty(files)) && CanUpdateInPlace();
            string changes = Changes(manifest, Application.version);

            NativeTitleScreen screen = title?.Invoke();
            while (screen != null && screen.CanHostUpdate && screen.AgreementOpen) yield return null;
            if (screen != null && screen.CanHostUpdate)
            {
                Action start = null;
                start = () =>
                {
                    if (!inPlace) { Application.OpenURL(page); screen.HideUpdateOffer(); return; }
                    host.StartCoroutine(Install(files, package,
                        (p, label, detail) => screen.SetUpdateProgress(p, label, detail),
                        error => screen.ShowUpdateOffer(manifest.version, error + "\n\n" + changes, "重试", start, null)));
                };
                if (inPlace)
                {
                    // Desktop builds update on their own (2026-10-22, owner): a closable notice + the bar.
                    screen.ShowUpdateOffer(manifest.version, changes, null, null, null);
                    start();
                }
                else screen.ShowUpdateOffer(manifest.version, changes, "去下载", start, null);
                yield break;
            }
            ShowPlate(host, canvasChild, font, manifest.version, changes, page, package, files, inPlace);
        }

        static bool CanUpdateInPlace()
        {
#if (UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX) && !UNITY_EDITOR
            return true;
#else
            return false;
#endif
        }

        /// <summary>Lobby fallback: a P5 plate (slanted) with a level progress bar.</summary>
        static void ShowPlate(MonoBehaviour host, Transform canvasChild, Font font, string version, string changes,
            string page, string package, string files, bool inPlace)
        {
            if (canvasChild == null) return;
            var canvas = canvasChild.GetComponentInParent<Canvas>();
            Transform layer = canvas != null ? canvas.rootCanvas.transform : canvasChild;
            var root = new GameObject("AppUpdate1022", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            root.transform.SetParent(layer, false); root.transform.SetAsLastSibling();
            var rect = (RectTransform)root.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            root.GetComponent<Image>().color = new Color(.02f, .01f, .05f, .85f);

            // Slant the card art only: the version list is reading text and must stay level.
            Transform plate = Box("Plate", root.transform, new Vector2(0, 40), new Vector2(640, 520), Color.clear, 0f);
            Box("Back", plate, Vector2.zero, new Vector2(640, 520), new Color32(14, 8, 20, 255), -4f);
            Box("Band", plate, new Vector2(0, 190), new Vector2(680, 96), Brand1025.Teal, 0f);
            Label(plate, font, "发现新版本  v" + version, 40, Brand1025.Ink, new Vector2(0, 190), new Vector2(620, 80));
            Text body = Label(plate, font, changes + "\n\n更新后存档会保留。", 26, new Color32(230, 225, 255, 255), new Vector2(0, 30), new Vector2(560, 220));
            body.alignment = TextAnchor.UpperLeft;
            var buttons = new GameObject("Buttons", typeof(RectTransform));
            buttons.transform.SetParent(plate, false);
            Button(buttons.transform, font, inPlace ? "立即更新" : "去下载", new Vector2(140, -190), Brand1025.Teal, () =>
            {
                if (!inPlace) { Application.OpenURL(page); return; }
                buttons.SetActive(false);
                // The plate is slanted; the bar counter-rotates so it reads level.
                Transform track = Box("Track", root.transform, new Vector2(0, -150), new Vector2(520, 26), new Color32(40, 32, 56, 255), 0f);
                var fill = (RectTransform)Box("Fill", track, Vector2.zero, new Vector2(0, 26), Brand1025.Teal, 0f);
                fill.anchorMin = fill.anchorMax = fill.pivot = new Vector2(0, .5f); fill.anchoredPosition = Vector2.zero;
                host.StartCoroutine(Install(files, package,
                    (p, label, detail) => { fill.sizeDelta = new Vector2(520f * p, 26f); body.text = label + "\n" + detail; },
                    error => { track.gameObject.SetActive(false); body.text = error + "\n可以稍后重试，或去官网下载新版本。"; buttons.SetActive(true); }));
            });
            Button(buttons.transform, font, "稍后", new Vector2(-160, -190), new Color32(70, 60, 96, 255), () => Destroy(root));
        }

        /// <summary>Delta update when the release has a file list, otherwise the whole zip.</summary>
        static IEnumerator Install(string files, string package, Action<float, string, string> progress, Action<string> fail)
        {
            if (!string.IsNullOrEmpty(files))
            {
                bool fallback = false;
                yield return InstallDelta(files, progress, fail, () => fallback = true);
                if (!fallback) yield break;
            }
            if (string.IsNullOrEmpty(package)) { fail("更新文件列表读取失败"); yield break; }
            yield return InstallZip(package, progress, fail);
        }

        static string GameRoot()
        {
#if UNITY_STANDALONE_OSX
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..")); // …/幻域魅声.app
#else
            return Path.GetDirectoryName(Application.dataPath);                 // folder holding the exe
#endif
        }

        /// <summary>
        /// Hashes the installed files (on a worker thread), downloads only the ones whose SHA-256 differs into a
        /// staging folder, then hands over to the swap script. fallback() asks for the full zip instead.
        /// </summary>
        static IEnumerator InstallDelta(string filesUrl, Action<float, string, string> progress, Action<string> fail, Action fallback)
        {
            progress(0f, "正在检查更新内容", "对比本机文件…");
            FileList list = null;
            using (var request = UnityWebRequest.Get(filesUrl + "?t=" + DateTime.UtcNow.Ticks))
            {
                request.timeout = 20;
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success)
                    try { list = JsonUtility.FromJson<FileList>(request.downloadHandler.text); } catch (Exception) { list = null; }
            }
            if (list == null || list.files == null || list.files.Length == 0 || string.IsNullOrEmpty(list.@base)) { fallback(); yield break; }

            string root = GameRoot();
            var changed = new List<FileEntry>();
            var task = System.Threading.Tasks.Task.Run(() =>
            {
                using (var sha = System.Security.Cryptography.SHA256.Create())
                    foreach (FileEntry entry in list.files)
                    {
                        string local = Path.Combine(root, entry.path.Replace('/', Path.DirectorySeparatorChar));
                        bool same = false;
                        try
                        {
                            if (File.Exists(local) && new FileInfo(local).Length == entry.size)
                                using (var stream = File.OpenRead(local))
                                    same = string.Equals(BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", ""), entry.sha256, StringComparison.OrdinalIgnoreCase);
                        }
                        catch (Exception) { same = false; }
                        if (!same) lock (changed) changed.Add(entry);
                    }
            });
            while (!task.IsCompleted) yield return null;
            if (task.IsFaulted) { fallback(); yield break; }

            long total = 0;
            foreach (FileEntry entry in changed) total += entry.size;
            string folder = Path.Combine(Application.temporaryCachePath, "update");
            string staging = Path.Combine(folder, "files");
            try { if (Directory.Exists(folder)) Directory.Delete(folder, true); Directory.CreateDirectory(staging); }
            catch (Exception e) { fail("无法准备更新目录：" + e.Message); yield break; }

            long done = 0;
            float started = Time.realtimeSinceStartup;
            for (int i = 0; i < changed.Count; i++)
            {
                FileEntry entry = changed[i];
                string target = Path.Combine(staging, entry.path.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                using (var request = new UnityWebRequest(list.@base + entry.sha256, UnityWebRequest.kHttpVerbGET))
                {
                    request.downloadHandler = new DownloadHandlerFile(target) { removeFileOnAbort = true };
                    request.SendWebRequest();
                    while (!request.isDone)
                    {
                        long now = done + (long)request.downloadedBytes;
                        float speed = now / Mathf.Max(.5f, Time.realtimeSinceStartup - started) / 1048576f;
                        progress(total > 0 ? now / (float)total : 1f, "正在下载更新",
                            $"{now / 1048576f:0.0}/{total / 1048576f:0.0} MB · {speed:0.0} MB/s（只下载改动的 {changed.Count} 个文件）");
                        yield return null;
                    }
                    if (request.result != UnityWebRequest.Result.Success) { fail("下载失败：" + request.error); yield break; }
                }
                done += entry.size;
            }
            progress(1f, "下载完成，正在安装", "游戏会自动关闭并重新打开");
            yield return null;
            yield return null;
            string error;
            try { error = SwapAndRestart(staging, folder, root); }
            catch (Exception e) { error = e.Message; Debug.LogException(e); }
            if (error != null) fail("安装失败：" + error);
        }

        /// <summary>Downloads the whole update zip, reporting (progress, label, detail), then swaps and restarts.</summary>
        static IEnumerator InstallZip(string package, Action<float, string, string> progress, Action<string> fail)
        {
            string folder = Path.Combine(Application.temporaryCachePath, "update");
            try { if (Directory.Exists(folder)) Directory.Delete(folder, true); Directory.CreateDirectory(folder); }
            catch (Exception e) { Debug.LogWarning("Update folder: " + e.Message); }
            string zip = Path.Combine(folder, "update.zip");

            using (var request = new UnityWebRequest(package, UnityWebRequest.kHttpVerbGET))
            {
                request.downloadHandler = new DownloadHandlerFile(zip) { removeFileOnAbort = true };
                request.SendWebRequest();
                float started = Time.realtimeSinceStartup;
                while (!request.isDone)
                {
                    ulong got = request.downloadedBytes;
                    float p = request.downloadProgress < 0 ? 0 : request.downloadProgress;
                    float speed = got / Mathf.Max(.5f, Time.realtimeSinceStartup - started) / 1048576f;
                    float total = p > .001f ? got / p / 1048576f : 0f;
                    progress(p, "正在下载更新",
                        total > 0 ? $"{got / 1048576f:0}/{total:0} MB · {speed:0.0} MB/s" : $"{got / 1048576f:0} MB · {speed:0.0} MB/s");
                    yield return null;
                }
                if (request.result != UnityWebRequest.Result.Success)
                {
                    fail("下载失败：" + request.error);
                    yield break;
                }
            }
            progress(1f, "下载完成，正在安装", "游戏会自动关闭并重新打开");
            yield return null;
            yield return null;
            string error;
            try { error = ApplyAndRestart(zip, folder); }
            catch (Exception e) { error = e.Message; Debug.LogException(e); }
            if (error != null) fail("安装失败：" + error);
        }

        /// <summary>Unpacks the whole-game zip, then swaps it in like a delta update.</summary>
        static string ApplyAndRestart(string zip, string folder)
        {
            string extracted = Path.Combine(folder, "files");
#if UNITY_STANDALONE_WIN
            System.IO.Compression.ZipFile.ExtractToDirectory(zip, extracted);
            File.Delete(zip);
            string exe = Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName);
            if (!File.Exists(Path.Combine(extracted, exe))) return "更新包内容不完整";
            return SwapAndRestart(extracted, folder, GameRoot());
#elif UNITY_STANDALONE_OSX
            Directory.CreateDirectory(extracted);
            using (var ditto = Process.Start(new ProcessStartInfo("/usr/bin/ditto", $"-x -k \"{zip}\" \"{extracted}\"") { UseShellExecute = false }))
                ditto.WaitForExit();
            File.Delete(zip);
            string[] apps = Directory.GetDirectories(extracted, "*.app");
            if (apps.Length == 0) return "更新包内容不完整";
            return SwapAndRestart(apps[0], folder, GameRoot());
#else
            return "当前平台不支持自动更新";
#endif
        }

        /// <summary>
        /// Copies <paramref name="source"/> over the game root after we quit and starts the game again.
        /// Windows: cmd + robocopy. Mac: ditto over the .app, re-sign ad hoc, open.
        /// </summary>
        static string SwapAndRestart(string source, string folder, string root)
        {
            int pid = Process.GetCurrentProcess().Id;
#if UNITY_STANDALONE_WIN
            string extracted = source, installDir = root;
            string exe = Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName);
            string script = Path.Combine(folder, "apply-update.cmd");
            var cmd = new StringBuilder();
            cmd.AppendLine("@echo off");
            cmd.AppendLine("chcp 65001 >nul");
            cmd.AppendLine($"set \"SRC={extracted}\"");
            cmd.AppendLine($"set \"DST={installDir}\"");
            cmd.AppendLine(":wait");
            cmd.AppendLine($"tasklist /FI \"PID eq {pid}\" 2>nul | find \"{pid}\" >nul");
            cmd.AppendLine("if not errorlevel 1 (ping -n 2 127.0.0.1 >nul & goto wait)");
            cmd.AppendLine("robocopy \"%SRC%\" \"%DST%\" /E /R:5 /W:1 /NFL /NDL /NJH /NJS /NP >nul");
            // Windows caches shortcut icons; ask Explorer to refresh so a new icon shows without a reboot.
            cmd.AppendLine("ie4uinit.exe -show >nul 2>&1");
            cmd.AppendLine($"start \"\" \"%DST%\\{exe}\"");
            cmd.AppendLine("rmdir /S /Q \"%SRC%\"");
            File.WriteAllText(script, cmd.ToString(), new UTF8Encoding(false));
            Process.Start(new ProcessStartInfo("cmd.exe", "/c \"\"" + script + "\"\"")
            {
                CreateNoWindow = true, UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden
            });
            Application.Quit();
            return null;
#elif UNITY_STANDALONE_OSX
            string script = Path.Combine(folder, "apply-update.sh");
            var sh = new StringBuilder();
            sh.Append("#!/bin/sh\n");
            sh.Append($"while kill -0 {pid} 2>/dev/null; do sleep 0.5; done\n");
            sh.Append($"APP='{root.Replace("'", "'\\''")}'\n");
            sh.Append($"NEW='{source.Replace("'", "'\\''")}'\n");
            // Merge the new files into the bundle, refresh the ad-hoc signature, relaunch.
            // Downloaded files arrive without the executable bit; the launcher binary needs it back.
            sh.Append("/usr/bin/ditto \"$NEW\" \"$APP\" && chmod +x \"$APP\"/Contents/MacOS/* && xattr -cr \"$APP\"; /usr/bin/codesign --force --deep -s - \"$APP\" 2>/dev/null\n");
            sh.Append("open \"$APP\"\n");
            sh.Append("rm -rf \"$NEW\"\n");
            File.WriteAllText(script, sh.ToString(), new UTF8Encoding(false));
            Process.Start(new ProcessStartInfo("/bin/sh", $"\"{script}\"") { UseShellExecute = false });
            Application.Quit();
            return null;
#else
            return "当前平台不支持自动更新";
#endif
        }

        static Transform Box(string name, Transform parent, Vector2 at, Vector2 size, Color color, float tilt)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            var r = (RectTransform)go.transform;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f); r.anchoredPosition = at; r.sizeDelta = size;
            r.localEulerAngles = new Vector3(0, 0, tilt);
            return go.transform;
        }

        static Text Label(Transform parent, Font font, string value, int size, Color color, Vector2 at, Vector2 box)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = font; t.text = value; t.fontSize = size; t.color = color; t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            var r = t.rectTransform; r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f); r.anchoredPosition = at; r.sizeDelta = box;
            return t;
        }

        static void Button(Transform parent, Font font, string label, Vector2 at, Color color, Action onClick)
        {
            Transform box = Box("Button-" + label, parent, at, new Vector2(240, 84), color, 0f);
            var button = box.gameObject.AddComponent<Button>();
            button.onClick.AddListener(() => onClick());
            Label(box, font, label, 32, Color.white, Vector2.zero, new Vector2(240, 84));
        }
    }
}
