using System;
using System.Collections.Generic;

namespace ChoSiren.Systems.Story
{
    /// <summary>
    /// What the UI must do for one player-visible step: apply every directive (in order), then
    /// present the blocking line. <see cref="Blocking"/> is null only when the script ended.
    /// </summary>
    public sealed class StoryFrame
    {
        public readonly List<StoryLine> Directives = new List<StoryLine>();
        public StoryLine Blocking;
        public int LineIndex = -1;
        public bool IsEnd => Blocking == null || Blocking.Command == StoryCommand.End;
        public bool IsChoice => Blocking != null && Blocking.Command == StoryCommand.Choice;
    }

    /// <summary>
    /// Pure visual-novel interpreter. Flags persist across scripts (pass the same set), so a
    /// choice in chapter 1 can change a line in chapter 3.
    /// </summary>
    public sealed class StoryRunner
    {
        public const int MaxDirectivesPerFrame = 64;

        private readonly StoryScript script;
        private readonly HashSet<string> flags;
        private int cursor;

        public StoryRunner(StoryScript script, HashSet<string> flags = null)
        {
            this.script = script ?? throw new ArgumentNullException(nameof(script));
            if (!script.TryValidate(out string error)) throw new ArgumentException(error, nameof(script));
            this.flags = flags ?? new HashSet<string>(StringComparer.Ordinal);
        }

        public StoryScript Script => script;
        public IReadOnlyCollection<string> Flags => flags;
        public StoryFrame Current { get; private set; }
        public bool Finished { get; private set; }
        public int LinesSeen { get; private set; }

        public bool HasFlag(string flag) => !string.IsNullOrEmpty(flag) && flags.Contains(flag);

        /// <summary>Runs from the first line (or a label) to the first blocking line.</summary>
        public StoryFrame Start(string label = null)
        {
            cursor = string.IsNullOrEmpty(label) ? 0 : script.IndexOfLabel(label);
            if (cursor < 0) throw new ArgumentException($"标签不存在：{label}", nameof(label));
            Finished = false;
            return Continue();
        }

        /// <summary>Advances past a say line. Throws when a choice is pending or the script ended.</summary>
        public StoryFrame Advance()
        {
            if (Current == null) throw new InvalidOperationException("剧情尚未开始");
            if (Finished) throw new InvalidOperationException("剧情已经结束");
            if (Current.IsChoice) throw new InvalidOperationException("必须先做出选择");
            cursor = Current.LineIndex + 1;
            return Continue();
        }

        public StoryFrame Choose(int optionIndex)
        {
            if (Current == null || !Current.IsChoice) throw new InvalidOperationException("当前没有可选项");
            List<StoryChoice> options = Current.Blocking.Choices;
            if (optionIndex < 0 || optionIndex >= options.Count) throw new ArgumentOutOfRangeException(nameof(optionIndex));

            StoryChoice choice = options[optionIndex];
            if (!string.IsNullOrEmpty(choice.SetFlag)) flags.Add(choice.SetFlag);
            cursor = string.IsNullOrEmpty(choice.Jump) ? Current.LineIndex + 1 : script.IndexOfLabel(choice.Jump);
            return Continue();
        }

        private StoryFrame Continue()
        {
            var frame = new StoryFrame();
            int guard = 0;
            while (cursor >= 0 && cursor < script.Lines.Count)
            {
                if (++guard > MaxDirectivesPerFrame)
                    throw new InvalidOperationException("剧情脚本存在无限跳转");

                StoryLine line = script.Lines[cursor];
                switch (line.Command)
                {
                    case StoryCommand.Say:
                    case StoryCommand.Choice:
                    case StoryCommand.End:
                        frame.Blocking = line;
                        frame.LineIndex = cursor;
                        LinesSeen++;
                        if (line.Command == StoryCommand.End) Finished = true;
                        Current = frame;
                        return frame;
                    case StoryCommand.SetFlag:
                        if (line.Value) flags.Add(line.Subject);
                        else flags.Remove(line.Subject);
                        cursor++;
                        break;
                    case StoryCommand.Jump:
                        cursor = script.IndexOfLabel(line.Jump);
                        break;
                    case StoryCommand.JumpIf:
                        cursor = flags.Contains(line.Subject) == line.Value ? script.IndexOfLabel(line.Jump) : cursor + 1;
                        break;
                    default:
                        frame.Directives.Add(line);
                        cursor++;
                        break;
                }
            }

            // Running off the end without an explicit "end" still terminates cleanly.
            frame.Blocking = null;
            frame.LineIndex = script.Lines.Count;
            Finished = true;
            Current = frame;
            return frame;
        }
    }
}
