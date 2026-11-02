using System.IO;
using ChoSiren.Systems.Story;
using ChoSiren.UI;
using NUnit.Framework;
using UnityEngine;

namespace ChoSiren.Tests
{
    public sealed class DialogueVoice1102Tests
    {
        [Test]
        public void EveryCurrentStoryLineHasItsOwnExactRecordingAndEditedTextCannotReuseIt()
        {
            int count = 0;
            foreach (TextAsset asset in Resources.LoadAll<TextAsset>("Data/Story"))
            {
                StoryScript script = JsonUtility.FromJson<StoryScript>(asset.text);
                for (int i = 0; i < script.Lines.Count; i++)
                {
                    StoryLine line = script.Lines[i];
                    if (line.Command != StoryCommand.Say) continue;
                    string clip = DialogueVoice1102.StoryClip(script.Id, i, line.Subject, line.Text);
                    Assert.That(clip, Is.Not.Null.And.Not.Empty, script.Id + " line " + i);
                    string path = Path.Combine(Application.streamingAssetsPath, "Voice", clip + ".mp3");
                    Assert.That(File.Exists(path) && new FileInfo(path).Length > 2000, Is.True, path);
                    Assert.That(DialogueVoice1102.StoryClip(script.Id, i, line.Subject, line.Text + "changed"), Is.Null,
                        "改稿后不能凭旧索引播放不对应的台词。");
                    count++;
                }
            }
            Assert.That(count, Is.GreaterThan(0));
        }

        [Test]
        public void AllFourLobbyIdolsHaveEveryRecordedLine()
        {
            int[] counts = { 3, 4, 3, 3 };
            for (int character = 0; character < counts.Length; character++)
                for (int line = 0; line < counts[character]; line++)
                {
                    string clip = DialogueVoice1102.HomeClip(character, line);
                    AudioClip audio = Resources.Load<AudioClip>("Audio/Voice/" + clip);
                    Assert.That(audio, Is.Not.Null, clip);
                    Assert.That(audio.length, Is.GreaterThan(.3f), clip);
                }
        }
    }
}
