using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChoSiren
{
    /// <summary>
    /// 存档字符串的读写抽象。生产环境走 <see cref="PlayerPrefsSaveStore"/>；测试注入
    /// <see cref="InMemorySaveStore"/>，从而在不触碰真实玩家 PlayerPrefs 的前提下隔离验证。
    /// </summary>
    public interface ISaveStore
    {
        string GetString(string key, string fallback);
        void SetString(string key, string value);
        void DeleteKey(string key);
        void Flush();
    }

    /// <summary>生产实现：直接读写 Unity PlayerPrefs。</summary>
    public sealed class PlayerPrefsSaveStore : ISaveStore
    {
        public string GetString(string key, string fallback) => PlayerPrefs.GetString(key, fallback);
        public void SetString(string key, string value) => PlayerPrefs.SetString(key, value);
        public void DeleteKey(string key) => PlayerPrefs.DeleteKey(key);
        public void Flush() => PlayerPrefs.Save();
    }

    /// <summary>测试实现：内存字典，隔离真实玩家存档。</summary>
    public sealed class InMemorySaveStore : ISaveStore
    {
        private readonly Dictionary<string, string> data = new Dictionary<string, string>(StringComparer.Ordinal);

        public string GetString(string key, string fallback) =>
            data.TryGetValue(key, out string value) ? value : fallback;

        public void SetString(string key, string value) => data[key] = value;
        public void DeleteKey(string key) => data.Remove(key);
        public void Flush() { }
    }
}
