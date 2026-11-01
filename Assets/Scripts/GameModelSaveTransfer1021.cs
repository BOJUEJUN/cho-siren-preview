using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;

namespace ChoSiren
{
    /// <summary>
    /// 存档导出 / 导入 (2026-10-21): the web save lives only in the browser, so players can move it
    /// as a text code — "CHOSIREN1:" + base64(gzip(save json)). Import validates before replacing.
    /// </summary>
    public sealed partial class GameModel
    {
        public const string SaveCodePrefix = "CHOSIREN1:";

        public string ExportSaveCode()
        {
            SaveState();
            byte[] raw = Encoding.UTF8.GetBytes(JsonUtility.ToJson(Save));
            using (var output = new MemoryStream())
            {
                using (var gzip = new GZipStream(output, System.IO.Compression.CompressionLevel.Optimal, true))
                    gzip.Write(raw, 0, raw.Length);
                return SaveCodePrefix + Convert.ToBase64String(output.ToArray());
            }
        }

        public static bool TryDecodeSaveCode(string code, out GameSave save, out string json, out string message)
        {
            save = null; json = null;
            string trimmed = (code ?? string.Empty).Trim();
            int start = trimmed.IndexOf(SaveCodePrefix, StringComparison.Ordinal);
            if (start < 0) { message = "这不是 CHO-SIREN 的存档码"; return false; }
            var builder = new StringBuilder();
            foreach (char c in trimmed.Substring(start + SaveCodePrefix.Length))
                if (!char.IsWhiteSpace(c)) builder.Append(c);
            try
            {
                byte[] packed = Convert.FromBase64String(builder.ToString());
                using (var input = new GZipStream(new MemoryStream(packed), CompressionMode.Decompress))
                using (var reader = new StreamReader(input, Encoding.UTF8))
                    json = reader.ReadToEnd();
                save = JsonUtility.FromJson<GameSave>(json);
            }
            catch (Exception)
            {
                message = "存档码不完整或已损坏，请重新复制";
                return false;
            }
            if (save == null || save.SchemaVersion <= 0 || save.Team == null || save.UnlockedMembers == null)
            {
                message = "存档内容无效";
                return false;
            }
            message = string.Empty;
            return true;
        }

        public bool TryImportSaveCode(string code, out string message)
        {
            if (!TryDecodeSaveCode(code, out _, out string json, out message)) return false;
            pendingBattles.Clear();
            activeStories.Clear();
            store.SetString(SaveKey, json);
            Load();
            SaveState();
            message = "存档已导入";
            return true;
        }
    }
}
