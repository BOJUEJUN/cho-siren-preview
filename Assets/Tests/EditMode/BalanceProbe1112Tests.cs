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
    /// 2026-11-12 (少爷: "新开的账号好像都无伤通关到第一个小 Boss"): a new account starts with enough gold (1,200 + the
    /// welcome mail's 3,000) to train the team to about Lv9 before 1-2, while 1-1…1-4 were tuned for Lv1. This probe finds
    /// the enemy factor that gives ~70% (自动, no gear) at the levels a new player really has: 1-2 Lv4, 1-3 Lv6, 1-4 Lv8.
    /// Writes Builds/qa-logs/balance-1112/probe.txt; applied to tactics.json by hand.
    /// </summary>
    [Explicit("balance probe; run by name")]
    public sealed class BalanceProbe1112Tests
    {
        public static readonly (string Stage, int Level)[] Targets = { ("stage-1-2", 4), ("stage-1-3", 6), ("stage-1-4", 8) };

        [Test]
        public void ProbeEarlyStagesForNewAccountLevels()
        {
            var tactics = GameData.Repository.Tactics;
            var lines = new List<string>();
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "qa-logs", "balance-1112"));
            Directory.CreateDirectory(dir);
            foreach (var (id, level) in Targets)
            {
                StageDefinition stage = tactics.Stages.First(s => s.Id == id);
                var original = stage.Enemies.Select(e => (e.ScalePermille, e.HpScalePermille)).ToList();
                double Rate(double f)
                {
                    for (int i = 0; i < stage.Enemies.Count; i++)
                    {
                        stage.Enemies[i].ScalePermille = Math.Max(1, (int)Math.Round(original[i].ScalePermille * f));
                        stage.Enemies[i].HpScalePermille = Math.Max(1, (int)Math.Round(original[i].HpScalePermille * f));
                    }
                    return Balance1101Tests.WinRate(stage.Id, level, -1, Balance1101Tests.Mode.Auto, 80);
                }
                double before = Rate(1.0);
                double lo = 1.0, hi = 12.0, best = 1.0, bestRate = before;
                for (int step = 0; step < 10; step++)
                {
                    double mid = Math.Sqrt(lo * hi);
                    double rate = Rate(mid);
                    if (Math.Abs(rate - 70) < Math.Abs(bestRate - 70)) { best = mid; bestRate = rate; }
                    if (rate > 70) lo = mid; else hi = mid;
                }
                for (int i = 0; i < stage.Enemies.Count; i++)
                {
                    stage.Enemies[i].ScalePermille = original[i].ScalePermille;
                    stage.Enemies[i].HpScalePermille = original[i].HpScalePermille;
                }
                string line = $"{id} L{level} before={before:0.0}% factor={best:0.000} after={bestRate:0.0}% L1={Balance1101Tests.WinRate(id, 1, -1, Balance1101Tests.Mode.Auto, 40):0}%";
                lines.Add(line);
                TestContext.WriteLine(line);
                File.WriteAllText(Path.Combine(dir, "probe.txt"), string.Join("\n", lines) + "\n");
            }
        }
    }
}
