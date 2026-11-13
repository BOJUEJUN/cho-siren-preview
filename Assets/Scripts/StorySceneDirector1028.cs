using System;
using ChoSiren.Systems.Story;

namespace ChoSiren
{
    public static class StorySceneDirector1028
    {
        private const string Prefix = "Art/Story/Scenes1019/cg-";

        private static string LineKey(string id, int index)
        {
            if (id == "stage-1-1") return Prefix + "stage-1-1-scene-v2";
            // l19 accidentally points to an unrelated white-background VFX image. Both branches
            // say "we should go and look"; the authored determined pose is appropriate for both.
            if (id == "chapter-01" && index == 19) index = 21;
            return Prefix + id + "-l" + index;
        }

        public static string Resolve(StoryScript script, int line, string current, Func<string, bool> exists)
        {
            if (script == null || line < 0 || script.Lines == null) return current;
            if (script.Id == "prologue")
            {
                // The shorter opening keeps the painted scenes authored for the original script.
                // Scene boundaries, rather than dialogue offsets, decide when the alarm CG appears.
                int alarm = script.IndexOfLabel("alarm");
                int rehearsal = script.Lines.FindIndex(value => value.Command == StoryCommand.Background &&
                    value.Subject == "Art/Story/rehearsal-night");
                string scene = alarm >= 0 && line >= alarm ? Prefix + "prologue-l24"
                    : rehearsal >= 0 && line >= rehearsal ? Prefix + "prologue-l4" : null;
                return scene != null && exists(scene) ? scene : null;
            }
            string exact = LineKey(script.Id, line);
            if (exists(exact)) return exact;
            if (!string.IsNullOrEmpty(current)) return current;
            // A choice reached directly by Skip still belongs to the last painted moment
            // in this location, not a future scene after the player's answer.
            for (int i = line - 1; i >= 0; i--)
            {
                if (script.Lines[i].Command == StoryCommand.Background) break;
                string candidate = LineKey(script.Id, i);
                if (exists(candidate)) return candidate;
            }
            // Pick the establishing CG in this scene, without leaking a later location backwards.
            for (int i = line + 1; i < script.Lines.Count; i++)
            {
                if (script.Lines[i].Command == StoryCommand.Background) break;
                string candidate = LineKey(script.Id, i);
                if (exists(candidate)) return candidate;
            }
            if (script.Id.EndsWith("-end", StringComparison.Ordinal))
            {
                string stage = script.Id.Substring(0, script.Id.Length - 4);
                for (int i = 0; i < 64; i++)
                {
                    string candidate = LineKey(stage, i);
                    if (exists(candidate)) return candidate;
                }
            }
            // 2026-11-11 (每章 20 关): x-11…x-20 have no CG of their own; they borrow the painted moments of the stage
            // whose scene they are set in (their bg), so a new stage never shows a bare speaker portrait.
            const string scenes = "Art/Story/Scenes1019/";
            StoryLine bg = script.Lines.Find(value => value.Command == StoryCommand.Background);
            if (bg != null && bg.Subject != null && bg.Subject.StartsWith(scenes + "stage-", StringComparison.Ordinal))
            {
                string borrowed = bg.Subject.Substring(scenes.Length);
                if (borrowed != script.Id)
                    for (int i = 0; i < 64; i++)
                    {
                        string candidate = LineKey(borrowed, i);
                        if (exists(candidate)) return candidate;
                    }
            }
            return null;
        }
    }
}
