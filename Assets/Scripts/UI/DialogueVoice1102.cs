using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChoSiren.UI
{
    /// <summary>Exact subtitle-to-recording mapping; later script edits cannot play an unrelated old line.</summary>
    public static class DialogueVoice1102
    {
        [Serializable] sealed class Entry { public string script, subject, zh, ja, clip; public int line; }
        [Serializable] sealed class Manifest { public Entry[] entries; }
        static Dictionary<string, Entry> entries;

        public static string StoryClip(string script, int index, string subject, string text)
        {
            if (entries == null)
            {
                entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
                TextAsset source = Resources.Load<TextAsset>("Audio/Voice/dialogue-1102");
                Manifest manifest = source != null ? JsonUtility.FromJson<Manifest>(source.text) : null;
                if (manifest?.entries != null)
                    foreach (Entry e in manifest.entries) entries[e.script + "|" + e.line] = e;
            }
            string who = string.IsNullOrEmpty(subject) ? "narrator" : subject;
            return entries.TryGetValue(script + "|" + index, out Entry entry) && entry.subject == who &&
                string.Equals(entry.zh, text, StringComparison.Ordinal) ? entry.clip : null;
        }

        public static string HomeClip(int character, int line)
        {
            if (line < 0) return null;
            switch (character)
            {
                case 0: return "catalena-" + (line % 3 + 1);
                case 1: return "lilith-" + (line % 4 + 1);
                case 2: return "Home/yeying-" + (line % 3 + 1);
                case 3: return "Home/wubai-" + (line % 3 + 1);
                default: return null;
            }
        }
    }
}
