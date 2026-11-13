using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ChoSiren.Systems.Data;
using ChoSiren.Systems.Tactics;
using NUnit.Framework;
using UnityEngine;

namespace ChoSiren.Tests
{
    /// <summary>
    /// 2026-11-11 (每章 20 关): searches the enemy stat factor per stage that hits the 1104 pass-rate targets at the
    /// recommended level with the chapter-one gear (自动: normal ~70%, elite / boss ~60%). Explicit: run by name only;
    /// writes Builds/qa-logs/chapter20-1111/probe.txt (stage factor before after), applied to tactics.json by hand.
    /// Set CHO_PROBE_STAGES to a comma list to probe only those stages.
    /// </summary>
    [Explicit("balance probe; run by name")]
    public sealed class BalanceProbe1111Tests
    {
        private const int Seeds = 60;

        [Test]
        public void ProbeEnemyScaleForTargetPassRates()
        {
            string only = Environment.GetEnvironmentVariable("CHO_PROBE_STAGES");
            var wanted = string.IsNullOrEmpty(only) ? null : new HashSet<string>(only.Split(','));
            var tactics = GameData.Repository.Tactics;
            var lines = new List<string>();
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "qa-logs", "chapter20-1111"));
            Directory.CreateDirectory(dir);
            foreach (StageDefinition stage in tactics.Stages)
            {
                if (wanted != null ? !wanted.Contains(stage.Id) : !NeedsProbe(stage.Id)) continue;
                int target = stage.EncounterType == "normal" ? 70 : 60;
                var original = stage.Enemies.Select(e => (e.ScalePermille, e.HpScalePermille)).ToList();
                double Rate(double f)
                {
                    for (int i = 0; i < stage.Enemies.Count; i++)
                    {
                        stage.Enemies[i].ScalePermille = Math.Max(1, (int)Math.Round(original[i].ScalePermille * f));
                        stage.Enemies[i].HpScalePermille = Math.Max(1, (int)Math.Round(original[i].HpScalePermille * f));
                    }
                    return Balance1101Tests.WinRate(stage.Id, stage.RecommendedLevel, 10, Balance1101Tests.Mode.Auto, Seeds);
                }
                double before = Rate(1.0);
                double lo = .25, hi = 4.0, best = 1.0, bestRate = before;
                for (int step = 0; step < 9; step++)
                {
                    double mid = Math.Sqrt(lo * hi);
                    double rate = Rate(mid);
                    if (Math.Abs(rate - target) < Math.Abs(bestRate - target)) { best = mid; bestRate = rate; }
                    if (rate > target) lo = mid; else hi = mid;
                }
                for (int i = 0; i < stage.Enemies.Count; i++)
                {
                    stage.Enemies[i].ScalePermille = original[i].ScalePermille;
                    stage.Enemies[i].HpScalePermille = original[i].HpScalePermille;
                }
                string line = $"{stage.Id} {stage.EncounterType} rec{stage.RecommendedLevel} before={before:0.0}% factor={best:0.000} after={bestRate:0.0}%";
                lines.Add(line);
                TestContext.WriteLine(line);
                File.WriteAllText(Path.Combine(dir, "probe.txt"), string.Join("\n", lines) + "\n");
            }
        }

        /// <summary>Stages this change touched: the wide ones, chapter two (moved up) and every new stage.</summary>
        private static bool NeedsProbe(string id)
        {
            string[] parts = id.Split('-');
            int n = int.Parse(parts[2]);
            return id == "stage-1-2" || id == "stage-1-8" || parts[1] == "2" || n > 10;
        }
    }
}
