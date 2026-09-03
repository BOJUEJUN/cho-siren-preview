using System;
using System.Collections.Generic;

namespace ChoSiren.Systems.Story
{
    /// <summary>
    /// Line commands. Presentation commands (bg/bgm/sfx/show/hide/set) are applied instantly and
    /// batched by the runner; blocking commands (say/choice/end) wait for the player.
    /// </summary>
    public static class StoryCommand
    {
        public const string Say = "say";
        public const string Choice = "choice";
        public const string Background = "bg";
        public const string Music = "bgm";
        public const string Sound = "sfx";
        public const string Show = "show";
        public const string Hide = "hide";
        public const string SetFlag = "set";
        public const string Jump = "jump";
        public const string JumpIf = "jump-if";
        public const string End = "end";

        public static readonly string[] All =
        {
            Say, Choice, Background, Music, Sound, Show, Hide, SetFlag, Jump, JumpIf, End
        };

        public static bool IsKnown(string id) => Array.IndexOf(All, id) >= 0;
        public static bool IsBlocking(string id) => id == Say || id == Choice || id == End;
    }

    [Serializable]
    public sealed class StoryChoice
    {
        public string Text = string.Empty;
        /// <summary>Label to continue from. Empty continues with the next line.</summary>
        public string Jump = string.Empty;
        /// <summary>Flag set to true when this option is picked (e.g. "ch1-trusted-lin").</summary>
        public string SetFlag = string.Empty;
    }

    [Serializable]
    public sealed class StoryLine
    {
        public string Label = string.Empty;
        public string Command = StoryCommand.Say;
        /// <summary>Character id for say/show/hide; asset id for bg/bgm/sfx; flag name for set/jump-if.</summary>
        public string Subject = string.Empty;
        public string Text = string.Empty;
        /// <summary>Portrait expression for say/show, e.g. "neutral", "smile".</summary>
        public string Expression = string.Empty;
        /// <summary>Stage slot for show/say: "left", "center", "right".</summary>
        public string Position = string.Empty;
        /// <summary>Target label for jump/jump-if.</summary>
        public string Jump = string.Empty;
        public bool Value = true;
        public List<StoryChoice> Choices = new List<StoryChoice>();
    }

    [Serializable]
    public sealed class StoryScript
    {
        public int SchemaVersion = 1;
        public string Id = string.Empty;
        public string Title = string.Empty;
        public string Chapter = string.Empty;
        public List<StoryLine> Lines = new List<StoryLine>();

        public int IndexOfLabel(string label)
        {
            if (string.IsNullOrEmpty(label)) return -1;
            for (int index = 0; index < Lines.Count; index++)
                if (Lines[index].Label == label) return index;
            return -1;
        }

        public bool TryValidate(out string error)
        {
            if (SchemaVersion != 1)
            {
                error = $"剧情脚本版本不支持：{SchemaVersion}";
                return false;
            }

            if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(Title))
            {
                error = "剧情脚本缺少 ID 或标题";
                return false;
            }

            if (Lines.Count == 0)
            {
                error = $"剧情 {Id} 没有任何台词";
                return false;
            }

            var labels = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < Lines.Count; index++)
            {
                StoryLine line = Lines[index];
                if (line == null)
                {
                    error = $"剧情 {Id} 第 {index + 1} 行为空";
                    return false;
                }

                if (!string.IsNullOrEmpty(line.Label) && !labels.Add(line.Label))
                {
                    error = $"剧情 {Id} 的标签重复：{line.Label}";
                    return false;
                }

                if (!StoryCommand.IsKnown(line.Command))
                {
                    error = $"剧情 {Id} 第 {index + 1} 行的指令未知：{line.Command}";
                    return false;
                }
            }

            for (int index = 0; index < Lines.Count; index++)
            {
                StoryLine line = Lines[index];
                switch (line.Command)
                {
                    case StoryCommand.Say:
                        if (string.IsNullOrWhiteSpace(line.Text))
                        {
                            error = $"剧情 {Id} 第 {index + 1} 行没有台词";
                            return false;
                        }

                        break;
                    case StoryCommand.Choice:
                        if (line.Choices.Count < 2)
                        {
                            error = $"剧情 {Id} 第 {index + 1} 行的选择支少于两个";
                            return false;
                        }

                        for (int choiceIndex = 0; choiceIndex < line.Choices.Count; choiceIndex++)
                        {
                            StoryChoice choice = line.Choices[choiceIndex];
                            if (choice == null || string.IsNullOrWhiteSpace(choice.Text))
                            {
                                error = $"剧情 {Id} 第 {index + 1} 行的选项 {choiceIndex + 1} 没有文字";
                                return false;
                            }

                            if (!string.IsNullOrEmpty(choice.Jump) && !labels.Contains(choice.Jump))
                            {
                                error = $"剧情 {Id} 的选项跳转到不存在的标签：{choice.Jump}";
                                return false;
                            }
                        }

                        break;
                    case StoryCommand.Jump:
                    case StoryCommand.JumpIf:
                        if (!labels.Contains(line.Jump))
                        {
                            error = $"剧情 {Id} 第 {index + 1} 行跳转到不存在的标签：{line.Jump}";
                            return false;
                        }

                        if (line.Command == StoryCommand.JumpIf && string.IsNullOrWhiteSpace(line.Subject))
                        {
                            error = $"剧情 {Id} 第 {index + 1} 行的条件跳转缺少标记名";
                            return false;
                        }

                        break;
                    case StoryCommand.SetFlag:
                        if (string.IsNullOrWhiteSpace(line.Subject))
                        {
                            error = $"剧情 {Id} 第 {index + 1} 行的 set 缺少标记名";
                            return false;
                        }

                        break;
                    case StoryCommand.Background:
                    case StoryCommand.Music:
                    case StoryCommand.Sound:
                    case StoryCommand.Show:
                    case StoryCommand.Hide:
                        if (string.IsNullOrWhiteSpace(line.Subject))
                        {
                            error = $"剧情 {Id} 第 {index + 1} 行的 {line.Command} 缺少资源或角色 ID";
                            return false;
                        }

                        break;
                }
            }

            error = string.Empty;
            return true;
        }
    }
}
