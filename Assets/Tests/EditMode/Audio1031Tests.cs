using System.Collections.Generic;
using System.IO;
using ChoSiren.Panels;
using ChoSiren.Systems.Tactics;
using ChoSiren.UI;
using NUnit.Framework;
using UnityEngine;

namespace ChoSiren.Tests
{
    /// <summary>2026-10-31 audio batch: enemy barks data, defeat lines, grade tiers and shipped SFX takes.</summary>
    public sealed class Audio1031Tests
    {
        private static readonly string[] Categories = { "enter", "taunt", "lowhp", "kill", "death" };

        [Test]
        public void BarksJsonCoversEveryEnemyAndEveryClipLoads()
        {
            TextAsset asset = Resources.Load<TextAsset>("Audio/Voice/Enemy/barks");
            Assert.That(asset, Is.Not.Null);
            var root = MiniJson1031.Parse(asset.text) as Dictionary<string, object>;
            Assert.That(root, Is.Not.Null);
            var enemies = new List<string> { "neon-scout", "noise-wraith", "pulse-guard", "velvet-hexer", "echo-drone",
                "static-golem", "siren-queen", "boss-default", "minion-default" };
            foreach (string id in enemies)
            {
                Assert.That(root.ContainsKey(id), id);
                var cats = (Dictionary<string, object>)root[id];
                foreach (string cat in Categories)
                {
                    var list = (List<object>)cats[cat];
                    Assert.That(list.Count, Is.InRange(1, 2), id + "/" + cat);
                    if (id == "siren-queen" || id == "boss-default") Assert.That(list.Count, Is.EqualTo(2), "boss: two per category");
                    foreach (Dictionary<string, object> line in list)
                    {
                        string clip = (string)line["clip"], text = (string)line["text"];
                        Assert.That(text, Is.Not.Empty);
                        Assert.That(clip, Does.StartWith("Voice/Enemy/"));
                        Assert.That(Path.HasExtension(clip), Is.False);
                        Assert.That(Resources.Load<AudioClip>("Audio/" + clip), Is.Not.Null, clip);
                    }
                }
            }
        }

        [Test]
        public void EveryVoicedMemberHasTwoDefeatLinesWithSubtitles()
        {
            foreach (string member in new[] { "xingli", "feiyin", "wubai", "yeying" })
            {
                Assert.That(TacticsBattlePanel.DefeatSubtitles1031[member].Length, Is.EqualTo(2));
                for (int v = 1; v <= 2; v++)
                {
                    AudioClip clip = Resources.Load<AudioClip>($"Audio/Voice/Battle/{member}-defeat-0{v}");
                    Assert.That(clip, Is.Not.Null);
                    Assert.That(clip.length, Is.InRange(.8f, 2.5f), "阵亡台词 0.8–2.5 s");
                }
            }
        }

        [TestCase(UltimateTimingGrade.Perfect, 0f, UltimateGradeTier1031.Perfect)]
        [TestCase(UltimateTimingGrade.Good, .12f, UltimateGradeTier1031.Great)]
        [TestCase(UltimateTimingGrade.Good, -.15f, UltimateGradeTier1031.Great)]
        [TestCase(UltimateTimingGrade.Good, .17f, UltimateGradeTier1031.Good)] // the auto-assist tap stays GOOD
        [TestCase(UltimateTimingGrade.Miss, .5f, UltimateGradeTier1031.Miss)]
        public void GradeTiersSplitGoodWithoutChangingTheGrade(UltimateTimingGrade grade, float offset, UltimateGradeTier1031 tier)
        {
            Assert.That(UltimateGradeBurst1031.TierFor(grade, offset), Is.EqualTo(tier));
            float assist = (UltimateBeatChallenge1025.PerfectWindow + UltimateBeatChallenge1025.GoodWindow) * .5f;
            Assert.That(UltimateGradeBurst1031.TierFor(UltimateTimingGrade.Good, assist), Is.EqualTo(UltimateGradeTier1031.Good));
        }

        [Test]
        public void EveryTierHasItsStingerAndEveryPositiveTierItsAnnouncer()
        {
            foreach (UltimateGradeTier1031 tier in System.Enum.GetValues(typeof(UltimateGradeTier1031)))
            {
                Assert.That(Resources.Load<AudioClip>("Audio/Sfx/" + UltimateGradeBurst1031.SfxName(tier)), Is.Not.Null, tier.ToString());
                string voice = UltimateGradeBurst1031.AnnouncerClip(tier);
                if (tier == UltimateGradeTier1031.Miss) Assert.That(voice, Is.Null);
                else Assert.That(Resources.Load<AudioClip>("Audio/Voice/" + voice), Is.Not.Null, voice);
            }
        }

        [Test]
        public void ReplacedBattleSfxKeepTheirNamesAndThreeTakes()
        {
            foreach (string name in new[] { "hit", "crit", "hurt", "block", "shield", "heal", "ko", "ultimate", "boss-hit", "down",
                "strike-charm", "strike-demon", "strike-mermaid", "strike-bloodelf" })
                foreach (string suffix in new[] { "", "-2", "-3" })
                {
                    AudioClip clip = Resources.Load<AudioClip>("Audio/Sfx/" + name + suffix);
                    Assert.That(clip, Is.Not.Null, name + suffix);
                    Assert.That(clip.frequency, Is.EqualTo(44100), name + suffix);
                    Assert.That(clip.length, Is.InRange(.1f, 2.4f), name + suffix);
                }
        }
    }

    /// <summary>Minimal JSON reader for the barks schema (objects, arrays, strings, numbers, literals).</summary>
    internal static class MiniJson1031
    {
        public static object Parse(string json) { int i = 0; return Value(json, ref i); }

        private static void Ws(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

        private static object Value(string s, ref int i)
        {
            Ws(s, ref i);
            char c = s[i];
            if (c == '{')
            {
                var d = new Dictionary<string, object>(); i++;
                for (Ws(s, ref i); s[i] != '}'; Ws(s, ref i))
                {
                    string k = Str(s, ref i); Ws(s, ref i); i++; // ':'
                    d[k] = Value(s, ref i); Ws(s, ref i);
                    if (s[i] == ',') i++;
                }
                i++; return d;
            }
            if (c == '[')
            {
                var l = new List<object>(); i++;
                for (Ws(s, ref i); s[i] != ']'; Ws(s, ref i))
                {
                    l.Add(Value(s, ref i)); Ws(s, ref i);
                    if (s[i] == ',') i++;
                }
                i++; return l;
            }
            if (c == '"') return Str(s, ref i);
            int start = i;
            while (i < s.Length && ",}] \n\r\t".IndexOf(s[i]) < 0) i++;
            return s.Substring(start, i - start);
        }

        private static string Str(string s, ref int i)
        {
            var b = new System.Text.StringBuilder(); i++;
            while (s[i] != '"')
            {
                if (s[i] == '\\')
                {
                    i++;
                    char e = s[i];
                    if (e == 'u') { b.Append((char)System.Convert.ToInt32(s.Substring(i + 1, 4), 16)); i += 4; }
                    else b.Append(e == 'n' ? '\n' : e == 't' ? '\t' : e);
                }
                else b.Append(s[i]);
                i++;
            }
            i++; return b.ToString();
        }
    }
}
