using System.Text;
using RustIQ.Domain.Servers;
using RustIQ.Simulator.Data;
using RustIQ.Simulator.Engine;
using RustIQ.Simulator.Evaluation;

namespace RustIQ.Simulator.Reporting;

/// <summary>
/// Plan section 8: the simulated current strategy against what production measured over the same period.
/// </summary>
public static class ValidationReport
{
    public const double AoiTolerance = 0.20;
    public const double LaneTolerance = 0.10;

    /// <summary>Polls per minute measured per lane on 2026-11-12 (plan section 3).</summary>
    private static readonly Dictionary<string, double> Measured1112 = new()
    {
        ["Fast"] = 105,
        ["NormalOnline"] = 125,
        ["SlowOffline"] = 14,
        ["VeryStaleOffline"] = 30
    };

    private static readonly (string SimLane, string RealLane)[] LaneMap =
    [
        ("Fast", nameof(ServerPollSource.FastLane)),
        ("NormalOnline", nameof(ServerPollSource.NormalOnlineLane)),
        ("SlowOffline", nameof(ServerPollSource.SlowOfflineLane)),
        ("VeryStaleOffline", nameof(ServerPollSource.VeryStaleOfflineLane))
    ];

    public static bool Write(
        string outDir,
        SimulatorData data,
        SimulationResult result,
        long evalFromMs,
        long evalToMs,
        IReadOnlyList<string> notes)
    {
        var report = new StringBuilder();
        var passed = true;
        report.AppendLine($"# Validation: {result.Strategy}, ranking replay");
        report.AppendLine();
        report.AppendLine($"Simulated {RunReport.Iso(result.FromMs)} .. {RunReport.Iso(result.ToMs)}, " +
                          $"compared over {RunReport.Iso(evalFromMs)} .. {RunReport.Iso(evalToMs)}.");
        report.AppendLine($"Thresholds (plan 8, proposed): AoI mean and median within {AoiTolerance:P0}, " +
                          $"polls per minute per lane within {LaneTolerance:P0}.");
        report.AppendLine();

        // 1. AoI per stratum.
        var simulated = RunReport.EvaluateSimulated(data, result, evalFromMs, evalToMs);
        var real = RunReport.EvaluateReal(data, evalFromMs, evalToMs);
        report.AppendLine("## 1. Freshness per stratum");
        report.AppendLine();
        report.AppendLine("| Stratum | Metric | Simulated | Real | Difference | Pass |");
        report.AppendLine("| --- | --- | --- | --- | --- | --- |");
        foreach (var sim in simulated)
        {
            var actual = real.Single(row => row.Group == sim.Group);
            passed &= AppendComparison(report, sim.Group, "AoI mean (s)", sim.AoiMeanSeconds, actual.AoiMeanSeconds, AoiTolerance, sim.Group != FreshnessEvaluator.AllGroup);
            passed &= AppendComparison(report, sim.Group, "AoI median (s)", sim.AoiMedianSeconds, actual.AoiMedianSeconds, AoiTolerance, sim.Group != FreshnessEvaluator.AllGroup);
            AppendComparison(report, sim.Group, "AoI p95 (s)", sim.AoiP95Seconds, actual.AoiP95Seconds, null, false);
            AppendComparison(report, sim.Group, "Player MAE", sim.PlayerMae, actual.PlayerMae, null, false);
            AppendComparison(report, sim.Group, "Share > 5 off", sim.ShareOffByMoreThan5, actual.ShareOffByMoreThan5, null, false);
            AppendComparison(report, sim.Group, "Player-weighted AoI (s)", sim.PlayerWeightedAoiSeconds, actual.PlayerWeightedAoiSeconds, null, false);
        }

        File.WriteAllText(Path.Combine(outDir, "summary_real.csv"), SummaryCsv.Format(real));

        // 2. Polls per minute per lane.
        var simRates = RunReport.LaneRates(result, evalFromMs, evalToMs).ToDictionary(rate => rate.Lane);
        var realRates = RunReport.RealLaneRates(data, evalFromMs, evalToMs);
        var inserts = RunReport.SnapshotInsertsPerMinute(data, evalFromMs, evalToMs);
        var exportedTotal = realRates.Values.Sum();
        var missing = inserts is { } i ? Math.Max(0, i - exportedTotal) : (double?)null;

        report.AppendLine();
        report.AppendLine("## 2. Polls per minute per lane");
        report.AppendLine();
        report.AppendLine("Fast and NormalOnline are compared with the exported snapshots (retention removed almost " +
                          "nothing from them). The offline lanes lost the snapshots of pruned servers, so they are compared " +
                          "together, with the 44 polls per minute measured on 2026-11-12 as the reference (decided 2026-11-16). " +
                          "The exported rate plus the whole gap between the write counter and the export is shown as well; it is " +
                          "an upper bound, because the gap also holds the lost Watched and Discovery polls of pruned servers.");
        report.AppendLine();
        report.AppendLine("| Lane | Simulated | Exported | Measured 12.11 | Reference | Difference | Pass |");
        report.AppendLine("| --- | --- | --- | --- | --- | --- | --- |");
        double simOffline = 0, exportedOffline = 0;
        foreach (var (simLane, realLane) in LaneMap)
        {
            var sim = simRates.TryGetValue(simLane, out var rate) ? rate.PollsPerMinute : 0;
            var exported = realRates.GetValueOrDefault(realLane);
            var online = simLane is "Fast" or "NormalOnline";
            if (!online)
            {
                simOffline += sim;
                exportedOffline += exported;
            }

            var line = $"| {simLane} | {sim:0.0} | {exported:0.0} | {Measured1112[simLane]:0} | ";
            if (online)
            {
                var ok = Within(sim, exported, LaneTolerance);
                passed &= ok;
                report.AppendLine(line + $"{exported:0.0} (exported) | {Diff(sim, exported)} | {Yes(ok)} |");
            }
            else
            {
                report.AppendLine(line + $"see combined | {Diff(sim, Measured1112[simLane])} vs 12.11 | - |");
            }
        }

        const double offlineMeasured = 44;
        var offlineOk = Within(simOffline, offlineMeasured, LaneTolerance);
        passed &= offlineOk;
        report.AppendLine($"| Offline lanes combined | {simOffline:0.0} | {exportedOffline:0.0} | {offlineMeasured:0} | " +
                          $"{offlineMeasured:0} (12.11) | {Diff(simOffline, offlineMeasured)} | {Yes(offlineOk)} |");
        if (missing is { } gap)
        {
            var upper = exportedOffline + gap;
            report.AppendLine($"| Offline lanes, upper bound | {simOffline:0.0} | {exportedOffline:0.0} | | " +
                              $"{upper:0.0} (exported + {gap:0.0} lost) | {Diff(simOffline, upper)} | - |");
        }

        var simExogenous = simRates.Values.Where(r => r.Exogenous).Sum(r => r.PollsPerMinute);
        var simAll = simRates["StrategyTotal"].PollsPerMinute + simExogenous;
        report.AppendLine($"| All polls incl. Watched/Discovery | {simAll:0.0} | {exportedTotal:0.0} | | " +
                          $"{(inserts is { } x ? $"{x:0.0} (write counter)" : "n/a")} | " +
                          $"{(inserts is { } y ? Diff(simAll, y) : "")} | - |");
        report.AppendLine();
        report.AppendLine($"Unanswered share of the strategy's polls: {simRates["StrategyTotal"].UnansweredShare:P1}.");
        var population = result.AveragePopulation(evalFromMs, evalToMs);
        report.AppendLine($"Population: {population.Known:0} known servers and {population.Phantoms:0} phantoms on average.");

        // 3. Polls per sample server per lane.
        var perServer = new StringBuilder("server_idx,stratum,lane,simulated_polls,real_polls\n");
        var stratumTotals = new Dictionary<(string Stratum, string Lane), (long Sim, long Real)>();
        foreach (var sample in data.SampleServers)
        {
            var snapshots = data.Snapshots[sample.ServerIndex];
            foreach (var (simLane, realLane) in LaneMap)
            {
                var sim = result.Lanes.TryGetValue(simLane, out var log)
                    ? log.SamplePolls[sample.ServerIdx - 1].Count(t => t >= evalFromMs && t < evalToMs)
                    : 0;
                var realCount = 0;
                for (var s = 0; s < snapshots.Count; s++)
                {
                    if (snapshots.TimesMs[s] >= evalFromMs && snapshots.TimesMs[s] < evalToMs
                        && snapshots.Sources[s].ToString() == realLane)
                    {
                        realCount++;
                    }
                }

                perServer.AppendLine($"{sample.ServerIdx},{sample.Stratum},{simLane},{sim},{realCount}");
                var key = (sample.Stratum.ToString(), simLane);
                var current = stratumTotals.GetValueOrDefault(key);
                stratumTotals[key] = (current.Sim + sim, current.Real + realCount);
            }
        }

        File.WriteAllText(Path.Combine(outDir, "sample_server_polls.csv"), perServer.ToString());
        report.AppendLine();
        report.AppendLine("## 3. Polls of sample servers per lane (totals; per server in sample_server_polls.csv)");
        report.AppendLine();
        report.AppendLine("| Stratum | Lane | Simulated | Real |");
        report.AppendLine("| --- | --- | --- | --- |");
        foreach (var ((stratum, lane), (sim, realCount)) in stratumTotals.OrderBy(pair => pair.Key.Stratum).ThenBy(pair => RunReport.LaneOrder(pair.Key.Lane)))
        {
            report.AppendLine($"| {stratum} | {lane} | {sim} | {realCount} |");
        }

        // 4. Ranking agreement.
        report.AppendLine();
        report.AppendLine("## 4. Ranking agreement on the sample servers");
        report.AppendLine();
        AppendRankingAgreement(report, outDir, data, result);

        report.AppendLine();
        report.AppendLine("## Limitations");
        report.AppendLine();
        foreach (var note in notes)
        {
            report.AppendLine($"- {note}");
        }

        report.AppendLine();
        report.AppendLine($"**Overall: {(passed ? "PASS" : "FAIL")}** (only rows marked Pass/Fail count).");
        File.WriteAllText(Path.Combine(outDir, "validation.md"), report.ToString());
        return passed;
    }

    private static void AppendRankingAgreement(StringBuilder report, string outDir, SimulatorData data, SimulationResult result)
    {
        var regenerations = result.Rankings.OrderBy(r => r.AtMs).ToArray();
        var rows = new StringBuilder("sampled_at,sim_ranking_at,servers,ranked_status_agree,exact_rank_agree,both_ranked,mean_abs_rank_diff\n");
        long servers = 0, statusAgree = 0, exactAgree = 0, bothRanked = 0;
        double absDiff = 0;

        foreach (var sample in data.SampleRankings.GroupBy(row => row.SampledAtUtc).OrderBy(group => group.Key))
        {
            var atMs = sample.Key.ToUnixTimeMilliseconds();
            if (atMs < result.FromMs || atMs >= result.ToMs)
            {
                continue;
            }

            var active = regenerations.LastOrDefault(r => r.AtMs <= atMs);
            var production = sample.ToDictionary(row => row.ServerId, row => row.Rank);
            long n = 0, status = 0, exact = 0, both = 0;
            double diff = 0;
            foreach (var server in data.SampleServers)
            {
                var simRank = active?.SampleRanks[server.ServerIdx - 1];
                var realRank = production.GetValueOrDefault(server.ServerId);
                n++;
                if (simRank.HasValue == realRank.HasValue) status++;
                if (simRank == realRank) exact++;
                if (simRank.HasValue && realRank.HasValue)
                {
                    both++;
                    diff += Math.Abs(simRank.Value - realRank.Value);
                }
            }

            rows.AppendLine(RunReport.Invariant(
                $"{RunReport.Iso(atMs)},{(active is null ? "" : RunReport.Iso(active.AtMs))},{n},{status},{exact},{both},{(both == 0 ? 0 : diff / both):0.##}"));
            servers += n;
            statusAgree += status;
            exactAgree += exact;
            bothRanked += both;
            absDiff += diff;
        }

        File.WriteAllText(Path.Combine(outDir, "ranking_agreement.csv"), rows.ToString());
        if (servers == 0)
        {
            report.AppendLine("No production rank samples in the period.");
            return;
        }

        report.AppendLine($"Over every hourly production sample in the period ({servers} server-hours):");
        report.AppendLine();
        report.AppendLine($"- ranked / not ranked agrees: {(double)statusAgree / servers:P1}");
        report.AppendLine($"- exact rank (or both unranked) agrees: {(double)exactAgree / servers:P1}");
        report.AppendLine($"- mean |rank difference| where both ranked: {(bothRanked == 0 ? 0 : absDiff / bothRanked):0.0} over {bothRanked} server-hours");
        report.AppendLine($"- regenerations: {string.Join(", ", result.Rankings.Select(r => $"{RunReport.Iso(r.AtMs)}{(r.Initial ? " (initial)" : "")} [{r.RankedCount}]"))}");
    }

    private static bool AppendComparison(StringBuilder report, string group, string metric, double? sim, double? real, double? tolerance, bool counts)
    {
        string pass = "-";
        var ok = true;
        if (tolerance is { } t && counts)
        {
            ok = sim is { } s && real is { } r && Within(s, r, t);
            pass = Yes(ok);
        }

        report.AppendLine(RunReport.Invariant(
            $"| {group} | {metric} | {sim:0.###} | {real:0.###} | {(sim is { } a && real is { } b ? Diff(a, b) : "")} | {pass} |"));
        return ok;
    }

    private static bool Within(double sim, double real, double tolerance) =>
        real == 0 ? sim == 0 : Math.Abs(sim - real) / Math.Abs(real) <= tolerance;

    private static string Diff(double sim, double real) =>
        real == 0 ? "n/a" : RunReport.Invariant($"{(sim - real) / real:+0.0%;-0.0%;0.0%}");

    private static string Yes(bool ok) => ok ? "PASS" : "FAIL";
}
