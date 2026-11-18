using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using RustIQ.Domain.Servers;
using RustIQ.Simulator.Data;
using RustIQ.Simulator.Engine;
using RustIQ.Simulator.Evaluation;

namespace RustIQ.Simulator.Reporting;

public sealed record LaneRate(string Lane, bool Exogenous, double PollsPerMinute, double UnansweredShare);

/// <summary>Turns a run into the plain files of plan section 9.</summary>
public static class RunReport
{
    /// <summary>Half the 15 min grid step: the second grid used to check for alignment with polling intervals.</summary>
    public const long ShiftedGridOffsetMs = FreshnessEvaluator.DefaultGridStepMs / 2;

    public static IReadOnlyList<FreshnessSummary> EvaluateSimulated(
        SimulatorData data, SimulationResult result, long fromMs, long toMs) =>
        FreshnessEvaluator.Evaluate(
            data.SampleServers
                .Select(sample => new EvaluationInput(sample.Stratum, data.Probes[sample.ServerIdx - 1], result.Shown(sample.ServerIdx)))
                .ToArray(),
            fromMs,
            toMs);

    public static IReadOnlyList<FreshnessSummary> EvaluateReal(SimulatorData data, long fromMs, long toMs) =>
        FreshnessEvaluator.Evaluate(
            data.SampleServers
                .Select(sample => new EvaluationInput(
                    sample.Stratum,
                    data.Probes[sample.ServerIdx - 1],
                    ShownSeries.FromSnapshots(data.Snapshots[sample.ServerIndex])))
                .ToArray(),
            fromMs,
            toMs);

    /// <summary>Polls per minute and unanswered share per lane over [fromMs, toMs), strategy lanes first.</summary>
    public static IReadOnlyList<LaneRate> LaneRates(SimulationResult result, long fromMs, long toMs)
    {
        var minutes = result.MinuteRange(fromMs, toMs).ToArray();
        var rates = result.Lanes.Values
            .Select(log =>
            {
                long polls = 0, unanswered = 0;
                foreach (var minute in minutes)
                {
                    polls += log.Polls[minute];
                    unanswered += log.Unanswered[minute];
                }

                return new LaneRate(
                    log.Lane,
                    log.Exogenous,
                    minutes.Length == 0 ? 0 : (double)polls / minutes.Length,
                    polls == 0 ? 0 : (double)unanswered / polls);
            })
            .OrderBy(rate => rate.Exogenous)
            .ThenBy(rate => LaneOrder(rate.Lane))
            .ToList();

        var own = rates.Where(rate => !rate.Exogenous).ToArray();
        var total = own.Sum(rate => rate.PollsPerMinute);
        rates.Insert(own.Length, new LaneRate(
            "StrategyTotal",
            false,
            total,
            total == 0 ? 0 : own.Sum(rate => rate.PollsPerMinute * rate.UnansweredShare) / total));
        return rates;
    }

    /// <summary>Production's polls per minute per <c>PollSource</c> over [fromMs, toMs), from the exported snapshots.</summary>
    public static Dictionary<string, double> RealLaneRates(SimulatorData data, long fromMs, long toMs)
    {
        var counts = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var series in data.Snapshots)
        {
            var start = FreshnessEvaluator.LastAtOrBefore(series.TimesMs, fromMs - 1) + 1;
            for (var i = start; i < series.Count && series.TimesMs[i] < toMs; i++)
            {
                var lane = series.Sources[i].ToString();
                counts[lane] = counts.GetValueOrDefault(lane) + 1;
            }
        }

        var minutes = (toMs - fromMs) / 60_000.0;
        return counts.ToDictionary(pair => pair.Key, pair => pair.Value / minutes, StringComparer.Ordinal);
    }

    /// <summary>server_snapshots inserts per minute from the cumulative write counters, or null.</summary>
    public static double? SnapshotInsertsPerMinute(SimulatorData data, long fromMs, long toMs)
    {
        var rows = data.TableWriteSamples
            .Where(row => row.TableName == "server_snapshots")
            .Select(row => (AtMs: row.SampledAtUtc.ToUnixTimeMilliseconds(), row.Inserts))
            .Where(row => row.AtMs >= fromMs && row.AtMs <= toMs)
            .OrderBy(row => row.AtMs)
            .ToArray();
        if (rows.Length < 2)
        {
            return null;
        }

        // Counters only reset on a restart; sum the non-negative differences.
        long inserts = 0;
        for (var i = 1; i < rows.Length; i++)
        {
            inserts += Math.Max(0, rows[i].Inserts - rows[i - 1].Inserts);
        }

        return inserts / ((rows[^1].AtMs - rows[0].AtMs) / 60_000.0);
    }

    public static void WriteRunFiles(
        string outDir,
        SimulatorData data,
        SimulationResult result,
        long evalFromMs,
        long evalToMs,
        long? splitMs,
        IReadOnlyDictionary<string, object?> arguments)
    {
        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, "summary.csv"),
            SummaryCsv.Format(EvaluateSimulated(data, result, evalFromMs, evalToMs)));
        File.WriteAllText(Path.Combine(outDir, "summary_shifted_grid.csv"),
            SummaryCsv.Format(EvaluateSimulated(data, result, evalFromMs + ShiftedGridOffsetMs, evalToMs)));
        if (splitMs is { } split && split > evalFromMs && split < evalToMs)
        {
            File.WriteAllText(Path.Combine(outDir, "summary_before_split.csv"),
                SummaryCsv.Format(EvaluateSimulated(data, result, evalFromMs, split)));
            File.WriteAllText(Path.Combine(outDir, "summary_after_split.csv"),
                SummaryCsv.Format(EvaluateSimulated(data, result, split, evalToMs)));
        }

        var lanes = new StringBuilder("lane,exogenous,polls_per_min,unanswered_share\n");
        foreach (var rate in LaneRates(result, evalFromMs, evalToMs))
        {
            lanes.AppendLine(Invariant($"{rate.Lane},{(rate.Exogenous ? 1 : 0)},{rate.PollsPerMinute:0.###},{rate.UnansweredShare:0.####}"));
        }

        File.WriteAllText(Path.Combine(outDir, "polls_by_lane.csv"), lanes.ToString());

        var perMinute = new StringBuilder("minute_utc,lane,polls,unanswered\n");
        foreach (var minute in Enumerable.Range(0, result.Minutes))
        {
            var at = DateTimeOffset.FromUnixTimeMilliseconds(result.FromMs + minute * 60_000L).ToString("yyyy-MM-ddTHH:mmZ", CultureInfo.InvariantCulture);
            foreach (var log in result.Lanes.Values.OrderBy(log => LaneOrder(log.Lane)))
            {
                if (log.Polls[minute] > 0)
                {
                    perMinute.Append(at).Append(',').Append(log.Lane).Append(',')
                        .Append(log.Polls[minute]).Append(',').Append(log.Unanswered[minute]).Append('\n');
                }
            }
        }

        File.WriteAllText(Path.Combine(outDir, "polls_per_minute.csv"), perMinute.ToString());

        File.WriteAllText(Path.Combine(outDir, "polls_by_stratum.csv"), PollsByStratumCsv(data, result, evalFromMs, evalToMs));
        File.WriteAllText(Path.Combine(outDir, "per_server.csv"), PerServerCsv(data, result, evalFromMs, evalToMs));

        var population = result.AveragePopulation(evalFromMs, evalToMs);
        var run = new Dictionary<string, object?>
        {
            ["strategy"] = result.Strategy,
            ["arguments"] = arguments,
            ["simulated_from"] = Iso(result.FromMs),
            ["simulated_to"] = Iso(result.ToMs),
            ["evaluated_from"] = Iso(evalFromMs),
            ["evaluated_to"] = Iso(evalToMs),
            ["split"] = splitMs is { } s ? Iso(s) : null,
            ["average_known_servers"] = Math.Round(population.Known, 1),
            ["average_phantom_servers"] = Math.Round(population.Phantoms, 1),
            ["ticks"] = result.Ticks,
            ["late_ticks"] = result.LateTicks,
            ["ranking_regenerations"] = result.Rankings.Select(r => new { at = Iso(r.AtMs), initial = r.Initial, ranked = r.RankedCount }),
            ["git_commit"] = GitCommit(),
            ["data"] = data.Manifests.Select(m => new
            {
                folder = Path.GetFileName(m.Folder),
                from = m.From,
                to = m.To,
                exported_at = m.ExportedAtUtc,
                sha256 = m.Sha256
            })
        };
        File.WriteAllText(Path.Combine(outDir, "run.json"),
            JsonSerializer.Serialize(run, new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>Polls per minute of the sample servers per stratum and lane over [fromMs, toMs).</summary>
    public static string PollsByStratumCsv(SimulatorData data, SimulationResult result, long fromMs, long toMs)
    {
        var minutes = (toMs - fromMs) / 60_000.0;
        var builder = new StringBuilder().AppendLine("stratum,lane,polls_per_min,polls_per_server_per_hour");
        foreach (var stratum in data.SampleServers.Select(s => s.Stratum).Distinct().OrderBy(s => s))
        {
            var samples = data.SampleServers.Where(s => s.Stratum == stratum).ToArray();
            foreach (var log in result.Lanes.Values.OrderBy(l => LaneOrder(l.Lane)))
            {
                var polls = samples.Sum(s => log.SamplePolls[s.ServerIdx - 1].Count(t => t >= fromMs && t < toMs));
                builder.AppendLine(Invariant($"{stratum},{log.Lane},{polls / minutes:0.###},{polls / minutes * 60 / samples.Length:0.###}"));
            }
        }

        return builder.ToString();
    }

    /// <summary>Freshness per sample server, to see which servers move the player-weighted AoI.</summary>
    public static string PerServerCsv(SimulatorData data, SimulationResult result, long fromMs, long toMs)
    {
        var builder = new StringBuilder().AppendLine(
            "server_idx,stratum,points,points_with_value,aoi_mean_s,aoi_p95_s,player_mae,player_weighted_aoi_s,polls");
        foreach (var sample in data.SampleServers)
        {
            var shown = result.Shown(sample.ServerIdx);
            var row = FreshnessEvaluator.Evaluate(
                    [new EvaluationInput(sample.Stratum, data.Probes[sample.ServerIdx - 1], shown)], fromMs, toMs)
                .Single(r => r.Group == FreshnessEvaluator.AllGroup);
            var polls = result.Lanes.Values.Where(l => !l.Exogenous)
                .Sum(l => l.SamplePolls[sample.ServerIdx - 1].Count(t => t >= fromMs && t < toMs));
            builder.AppendLine(Invariant(
                $"{sample.ServerIdx},{sample.Stratum},{row.Points},{row.PointsWithValue},{row.AoiMeanSeconds:0.#},{row.AoiP95Seconds:0.#},{row.PlayerMae:0.###},{row.PlayerWeightedAoiSeconds:0.#},{polls}"));
        }

        return builder.ToString();
    }

    public static string Iso(long ms) =>
        DateTimeOffset.FromUnixTimeMilliseconds(ms).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

    public static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    public static int LaneOrder(string lane) => lane switch
    {
        nameof(ServerPollSource.FastLane) or "Fast" => 0,
        nameof(ServerPollSource.NormalOnlineLane) or "NormalOnline" => 1,
        nameof(ServerPollSource.SlowOfflineLane) or "SlowOffline" => 2,
        nameof(ServerPollSource.VeryStaleOfflineLane) or "VeryStaleOffline" => 3,
        "Priority" => 4,
        "PriorityMaxAge" => 4,
        "StrategyTotal" => 5,
        nameof(ServerPollSource.Watched) => 6,
        nameof(ServerPollSource.Discovery) => 7,
        _ => 4
    };

    private static string? GitCommit()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("git", "rev-parse HEAD")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            });
            if (process is null)
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                return null;
            }

            // Uncommitted changes make the commit alone not reproduce the run.
            using var status = Process.Start(new ProcessStartInfo("git", "status --porcelain --untracked-files=no")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false
            });
            var dirty = status?.StandardOutput.ReadToEnd().Trim().Length > 0;
            status?.WaitForExit();
            return dirty ? output + "-dirty" : output;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
