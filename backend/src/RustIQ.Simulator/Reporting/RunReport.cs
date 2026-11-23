using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using RustIQ.Domain.Servers;
using RustIQ.Domain.Study;
using RustIQ.Simulator.Data;
using RustIQ.Simulator.Engine;
using RustIQ.Simulator.Evaluation;

namespace RustIQ.Simulator.Reporting;

public sealed record LaneRate(string Lane, bool Exogenous, double PollsPerMinute, double UnansweredShare);

public static class RunReport
{
    public const long ShiftedGridOffsetMs = FreshnessEvaluator.DefaultGridStepMs / 2;

    public static IReadOnlyList<FreshnessSummary> EvaluateSimulated(
        SimulatorData data, SimulationResult result, long fromMs, long toMs)
    {
        List<EvaluationInput> inputs = new List<EvaluationInput>();
        foreach (SampleServer sample in data.SampleServers)
        {
            inputs.Add(new EvaluationInput(sample.Stratum, data.Probes[sample.ServerIdx - 1], result.Shown(sample.ServerIdx)));
        }

        return FreshnessEvaluator.Evaluate(inputs, fromMs, toMs);
    }

    public static IReadOnlyList<FreshnessSummary> EvaluateReal(SimulatorData data, long fromMs, long toMs)
    {
        List<EvaluationInput> inputs = new List<EvaluationInput>();
        foreach (SampleServer sample in data.SampleServers)
        {
            inputs.Add(new EvaluationInput(
                sample.Stratum,
                data.Probes[sample.ServerIdx - 1],
                ShownSeries.FromSnapshots(data.Snapshots[sample.ServerIndex])));
        }

        return FreshnessEvaluator.Evaluate(inputs, fromMs, toMs);
    }

    public static IReadOnlyList<LaneRate> LaneRates(SimulationResult result, long fromMs, long toMs)
    {
        List<int> minutes = new List<int>();
        foreach (int minute in result.MinuteRange(fromMs, toMs))
        {
            minutes.Add(minute);
        }

        List<LaneRate> rates = new List<LaneRate>();
        foreach (LaneLog log in result.Lanes.Values)
        {
            long polls = 0;
            long unanswered = 0;
            foreach (int minute in minutes)
            {
                polls += log.Polls[minute];
                unanswered += log.Unanswered[minute];
            }

            double pollsPerMinute = 0;
            if (minutes.Count != 0)
            {
                pollsPerMinute = (double)polls / minutes.Count;
            }

            double unansweredShare = 0;
            if (polls != 0)
            {
                unansweredShare = (double)unanswered / polls;
            }

            LaneRate rate = new LaneRate(log.Lane, log.Exogenous, pollsPerMinute, unansweredShare);

            int position = rates.Count;
            while (position > 0 && CompareLaneRates(rates[position - 1], rate) > 0)
            {
                position--;
            }

            rates.Insert(position, rate);
        }

        int ownCount = 0;
        double total = 0;
        double weightedUnanswered = 0;
        foreach (LaneRate rate in rates)
        {
            if (!rate.Exogenous)
            {
                ownCount++;
                total += rate.PollsPerMinute;
                weightedUnanswered += rate.PollsPerMinute * rate.UnansweredShare;
            }
        }

        double totalUnansweredShare = 0;
        if (total != 0)
        {
            totalUnansweredShare = weightedUnanswered / total;
        }

        rates.Insert(ownCount, new LaneRate("StrategyTotal", false, total, totalUnansweredShare));
        return rates;
    }

    public static Dictionary<string, double> RealLaneRates(SimulatorData data, long fromMs, long toMs)
    {
        Dictionary<string, long> counts = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (SnapshotSeries series in data.Snapshots)
        {
            int start = FreshnessEvaluator.LastAtOrBefore(series.TimesMs, fromMs - 1) + 1;
            for (int i = start; i < series.Count && series.TimesMs[i] < toMs; i++)
            {
                string lane = series.Sources[i].ToString();
                long count;
                if (counts.TryGetValue(lane, out count))
                {
                    counts[lane] = count + 1;
                }
                else
                {
                    counts[lane] = 1;
                }
            }
        }

        double minutes = (toMs - fromMs) / 60_000.0;
        Dictionary<string, double> rates = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, long> pair in counts)
        {
            rates.Add(pair.Key, pair.Value / minutes);
        }

        return rates;
    }

    public static double? SnapshotInsertsPerMinute(SimulatorData data, long fromMs, long toMs)
    {
        List<long> times = new List<long>();
        List<long> counters = new List<long>();
        foreach (TableWriteSample row in data.TableWriteSamples)
        {
            if (row.TableName != "server_snapshots")
            {
                continue;
            }

            long atMs = row.SampledAtUtc.ToUnixTimeMilliseconds();
            if (atMs < fromMs || atMs > toMs)
            {
                continue;
            }

            int position = times.Count;
            while (position > 0 && times[position - 1] > atMs)
            {
                position--;
            }

            times.Insert(position, atMs);
            counters.Insert(position, row.Inserts);
        }

        if (times.Count < 2)
        {
            return null;
        }

        long inserts = 0;
        for (int i = 1; i < times.Count; i++)
        {
            inserts += Math.Max(0, counters[i] - counters[i - 1]);
        }

        return inserts / ((times[times.Count - 1] - times[0]) / 60_000.0);
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
        if (splitMs.HasValue && splitMs.Value > evalFromMs && splitMs.Value < evalToMs)
        {
            long split = splitMs.Value;
            File.WriteAllText(Path.Combine(outDir, "summary_before_split.csv"),
                SummaryCsv.Format(EvaluateSimulated(data, result, evalFromMs, split)));
            File.WriteAllText(Path.Combine(outDir, "summary_after_split.csv"),
                SummaryCsv.Format(EvaluateSimulated(data, result, split, evalToMs)));
        }

        StringBuilder lanes = new StringBuilder("lane,exogenous,polls_per_min,unanswered_share\n");
        foreach (LaneRate rate in LaneRates(result, evalFromMs, evalToMs))
        {
            lanes.AppendLine(Invariant($"{rate.Lane},{(rate.Exogenous ? 1 : 0)},{rate.PollsPerMinute:0.###},{rate.UnansweredShare:0.####}"));
        }

        File.WriteAllText(Path.Combine(outDir, "polls_by_lane.csv"), lanes.ToString());

        List<LaneLog> orderedLogs = LogsInLaneOrder(result);
        StringBuilder perMinute = new StringBuilder("minute_utc,lane,polls,unanswered\n");
        for (int minute = 0; minute < result.Minutes; minute++)
        {
            string at = DateTimeOffset.FromUnixTimeMilliseconds(result.FromMs + minute * 60_000L).ToString("yyyy-MM-ddTHH:mmZ", CultureInfo.InvariantCulture);
            foreach (LaneLog log in orderedLogs)
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

        (double Known, double Phantoms) population = result.AveragePopulation(evalFromMs, evalToMs);

        string? splitText = null;
        if (splitMs.HasValue)
        {
            splitText = Iso(splitMs.Value);
        }

        List<object> rankingRegenerations = new List<object>();
        foreach (RankingRegeneration r in result.Rankings)
        {
            rankingRegenerations.Add(new { at = Iso(r.AtMs), initial = r.Initial, ranked = r.RankedCount });
        }

        List<object> dataFolders = new List<object>();
        foreach (ExportManifest m in data.Manifests)
        {
            dataFolders.Add(new
            {
                folder = Path.GetFileName(m.Folder),
                from = m.From,
                to = m.To,
                exported_at = m.ExportedAtUtc,
                sha256 = m.Sha256
            });
        }

        Dictionary<string, object?> run = new Dictionary<string, object?>();
        run["strategy"] = result.Strategy;
        run["arguments"] = arguments;
        run["simulated_from"] = Iso(result.FromMs);
        run["simulated_to"] = Iso(result.ToMs);
        run["evaluated_from"] = Iso(evalFromMs);
        run["evaluated_to"] = Iso(evalToMs);
        run["split"] = splitText;
        run["average_known_servers"] = Math.Round(population.Known, 1);
        run["average_phantom_servers"] = Math.Round(population.Phantoms, 1);
        run["ticks"] = result.Ticks;
        run["late_ticks"] = result.LateTicks;
        run["ranking_regenerations"] = rankingRegenerations;
        run["git_commit"] = GitCommit();
        run["data"] = dataFolders;

        JsonSerializerOptions options = new JsonSerializerOptions();
        options.WriteIndented = true;
        File.WriteAllText(Path.Combine(outDir, "run.json"), JsonSerializer.Serialize(run, options));
    }

    public static string PollsByStratumCsv(SimulatorData data, SimulationResult result, long fromMs, long toMs)
    {
        double minutes = (toMs - fromMs) / 60_000.0;
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("stratum,lane,polls_per_min,polls_per_server_per_hour");

        List<StudyStratum> strata = new List<StudyStratum>();
        foreach (SampleServer sample in data.SampleServers)
        {
            if (!strata.Contains(sample.Stratum))
            {
                strata.Add(sample.Stratum);
            }
        }

        strata.Sort();

        List<LaneLog> orderedLogs = LogsInLaneOrder(result);
        foreach (StudyStratum stratum in strata)
        {
            List<SampleServer> samples = new List<SampleServer>();
            foreach (SampleServer sample in data.SampleServers)
            {
                if (sample.Stratum == stratum)
                {
                    samples.Add(sample);
                }
            }

            foreach (LaneLog log in orderedLogs)
            {
                int polls = 0;
                foreach (SampleServer sample in samples)
                {
                    polls += CountInRange(log.SamplePolls[sample.ServerIdx - 1], fromMs, toMs);
                }

                builder.AppendLine(Invariant($"{stratum},{log.Lane},{polls / minutes:0.###},{polls / minutes * 60 / samples.Count:0.###}"));
            }
        }

        return builder.ToString();
    }

    public static string PerServerCsv(SimulatorData data, SimulationResult result, long fromMs, long toMs)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine(
            "server_idx,stratum,points,points_with_value,aoi_mean_s,aoi_p95_s,player_mae,player_weighted_aoi_s,polls");
        foreach (SampleServer sample in data.SampleServers)
        {
            ShownSeries shown = result.Shown(sample.ServerIdx);
            List<EvaluationInput> input = new List<EvaluationInput>();
            input.Add(new EvaluationInput(sample.Stratum, data.Probes[sample.ServerIdx - 1], shown));
            IReadOnlyList<FreshnessSummary> summaries = FreshnessEvaluator.Evaluate(input, fromMs, toMs);
            FreshnessSummary row = FindGroup(summaries, FreshnessEvaluator.AllGroup);

            int polls = 0;
            foreach (LaneLog log in result.Lanes.Values)
            {
                if (!log.Exogenous)
                {
                    polls += CountInRange(log.SamplePolls[sample.ServerIdx - 1], fromMs, toMs);
                }
            }

            builder.AppendLine(Invariant(
                $"{sample.ServerIdx},{sample.Stratum},{row.Points},{row.PointsWithValue},{row.AoiMeanSeconds:0.#},{row.AoiP95Seconds:0.#},{row.PlayerMae:0.###},{row.PlayerWeightedAoiSeconds:0.#},{polls}"));
        }

        return builder.ToString();
    }

    public static string Iso(long ms)
    {
        return DateTimeOffset.FromUnixTimeMilliseconds(ms).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
    }

    public static string Invariant(FormattableString text)
    {
        return text.ToString(CultureInfo.InvariantCulture);
    }

    public static int LaneOrder(string lane)
    {
        if (lane == nameof(ServerPollSource.FastLane) || lane == "Fast")
        {
            return 0;
        }

        if (lane == nameof(ServerPollSource.NormalOnlineLane) || lane == "NormalOnline")
        {
            return 1;
        }

        if (lane == nameof(ServerPollSource.SlowOfflineLane) || lane == "SlowOffline")
        {
            return 2;
        }

        if (lane == nameof(ServerPollSource.VeryStaleOfflineLane) || lane == "VeryStaleOffline")
        {
            return 3;
        }

        if (lane == "Priority" || lane == "PriorityMaxAge")
        {
            return 4;
        }

        if (lane == "StrategyTotal")
        {
            return 5;
        }

        if (lane == nameof(ServerPollSource.Watched))
        {
            return 6;
        }

        if (lane == nameof(ServerPollSource.Discovery))
        {
            return 7;
        }

        return 4;
    }

    private static int CompareLaneRates(LaneRate a, LaneRate b)
    {
        if (a.Exogenous != b.Exogenous)
        {
            if (a.Exogenous)
            {
                return 1;
            }

            return -1;
        }

        return LaneOrder(a.Lane).CompareTo(LaneOrder(b.Lane));
    }

    private static List<LaneLog> LogsInLaneOrder(SimulationResult result)
    {
        List<LaneLog> ordered = new List<LaneLog>();
        foreach (LaneLog log in result.Lanes.Values)
        {
            int position = ordered.Count;
            while (position > 0 && LaneOrder(ordered[position - 1].Lane) > LaneOrder(log.Lane))
            {
                position--;
            }

            ordered.Insert(position, log);
        }

        return ordered;
    }

    private static int CountInRange(List<long> times, long fromMs, long toMs)
    {
        int count = 0;
        foreach (long t in times)
        {
            if (t >= fromMs && t < toMs)
            {
                count++;
            }
        }

        return count;
    }

    private static FreshnessSummary FindGroup(IReadOnlyList<FreshnessSummary> rows, string group)
    {
        FreshnessSummary? found = null;
        int matches = 0;
        foreach (FreshnessSummary row in rows)
        {
            if (row.Group == group)
            {
                found = row;
                matches++;
            }
        }

        if (matches != 1 || found == null)
        {
            throw new InvalidOperationException("Expected exactly one row for group " + group + ".");
        }

        return found;
    }

    private static string? GitCommit()
    {
        try
        {
            ProcessStartInfo headInfo = new ProcessStartInfo("git", "rev-parse HEAD");
            headInfo.RedirectStandardOutput = true;
            headInfo.RedirectStandardError = true;
            headInfo.UseShellExecute = false;

            using (Process? process = Process.Start(headInfo))
            {
                if (process == null)
                {
                    return null;
                }

                string output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    return null;
                }

                ProcessStartInfo statusInfo = new ProcessStartInfo("git", "status --porcelain --untracked-files=no");
                statusInfo.RedirectStandardOutput = true;
                statusInfo.UseShellExecute = false;

                using (Process? status = Process.Start(statusInfo))
                {
                    bool dirty = false;
                    if (status != null)
                    {
                        dirty = status.StandardOutput.ReadToEnd().Trim().Length > 0;
                        status.WaitForExit();
                    }

                    if (dirty)
                    {
                        return output + "-dirty";
                    }

                    return output;
                }
            }
        }
        catch (Exception)
        {
            return null;
        }
    }
}
