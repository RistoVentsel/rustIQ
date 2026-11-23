using System.Text;
using RustIQ.Domain.Servers;
using RustIQ.Simulator.Data;
using RustIQ.Simulator.Engine;
using RustIQ.Simulator.Evaluation;

namespace RustIQ.Simulator.Reporting;

public static class ValidationReport
{
    public const double AoiTolerance = 0.20;
    public const double LaneTolerance = 0.10;

    private static readonly Dictionary<string, double> Measured1112 = CreateMeasured1112();

    private static readonly DateTimeOffset MeasuredWindowStart = new DateTimeOffset(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);

    private static readonly string[] SimLanes =
    {
        "Fast",
        "NormalOnline",
        "SlowOffline",
        "VeryStaleOffline"
    };

    private static readonly string[] RealLanes =
    {
        nameof(ServerPollSource.FastLane),
        nameof(ServerPollSource.NormalOnlineLane),
        nameof(ServerPollSource.SlowOfflineLane),
        nameof(ServerPollSource.VeryStaleOfflineLane)
    };

    private sealed class StratumLaneTotal
    {
        public string Stratum = "";
        public string Lane = "";
        public long Sim;
        public long Real;
    }

    private static Dictionary<string, double> CreateMeasured1112()
    {
        Dictionary<string, double> measured = new Dictionary<string, double>();
        measured["Fast"] = 105;
        measured["NormalOnline"] = 125;
        measured["SlowOffline"] = 14;
        measured["VeryStaleOffline"] = 30;
        return measured;
    }

    public static bool Write(
        string outDir,
        SimulatorData data,
        SimulationResult result,
        long evalFromMs,
        long evalToMs,
        IReadOnlyList<string> notes,
        long? splitMs = null)
    {
        StringBuilder report = new StringBuilder();
        bool passed = true;
        report.AppendLine($"# Validation: {result.Strategy}, ranking replay");
        report.AppendLine();
        report.AppendLine($"Simulated {RunReport.Iso(result.FromMs)} .. {RunReport.Iso(result.ToMs)}, " +
                          $"compared over {RunReport.Iso(evalFromMs)} .. {RunReport.Iso(evalToMs)}.");
        report.AppendLine($"Thresholds (plan 8, proposed): AoI mean and median within {AoiTolerance:P0}, " +
                          $"polls per minute per lane within {LaneTolerance:P0}.");
        report.AppendLine();

        IReadOnlyList<FreshnessSummary> simulated = RunReport.EvaluateSimulated(data, result, evalFromMs, evalToMs);
        IReadOnlyList<FreshnessSummary> real = RunReport.EvaluateReal(data, evalFromMs, evalToMs);
        report.AppendLine("## 1. Freshness per stratum");
        report.AppendLine();
        report.AppendLine("| Stratum | Metric | Simulated | Real | Difference | Pass |");
        report.AppendLine("| --- | --- | --- | --- | --- | --- |");
        foreach (FreshnessSummary sim in simulated)
        {
            FreshnessSummary actual = FindGroup(real, sim.Group);
            bool counts = sim.Group != FreshnessEvaluator.AllGroup;
            if (!AppendComparison(report, sim.Group, "AoI mean (s)", sim.AoiMeanSeconds, actual.AoiMeanSeconds, AoiTolerance, counts))
            {
                passed = false;
            }

            if (!AppendComparison(report, sim.Group, "AoI median (s)", sim.AoiMedianSeconds, actual.AoiMedianSeconds, AoiTolerance, counts))
            {
                passed = false;
            }

            AppendComparison(report, sim.Group, "AoI p95 (s)", sim.AoiP95Seconds, actual.AoiP95Seconds, null, false);
            AppendComparison(report, sim.Group, "Player MAE", sim.PlayerMae, actual.PlayerMae, null, false);
            AppendComparison(report, sim.Group, "Share > 5 off", sim.ShareOffByMoreThan5, actual.ShareOffByMoreThan5, null, false);
            AppendComparison(report, sim.Group, "Player-weighted AoI (s)", sim.PlayerWeightedAoiSeconds, actual.PlayerWeightedAoiSeconds, null, false);
        }

        File.WriteAllText(Path.Combine(outDir, "summary_real.csv"), SummaryCsv.Format(real));

        if (splitMs.HasValue && splitMs.Value > evalFromMs && splitMs.Value < evalToMs)
        {
            long split = splitMs.Value;
            report.AppendLine();
            report.AppendLine("Player-weighted AoI per half of the period (no threshold):");
            report.AppendLine();
            report.AppendLine("| Window | Stratum | Simulated | Real | Difference |");
            report.AppendLine("| --- | --- | --- | --- | --- |");
            AppendHalf(report, data, result, "before split", evalFromMs, split);
            AppendHalf(report, data, result, "after split", split, evalToMs);
        }

        Dictionary<string, LaneRate> simRates = new Dictionary<string, LaneRate>();
        foreach (LaneRate rate in RunReport.LaneRates(result, evalFromMs, evalToMs))
        {
            simRates.Add(rate.Lane, rate);
        }

        Dictionary<string, double> realRates = RunReport.RealLaneRates(data, evalFromMs, evalToMs);
        double? inserts = RunReport.SnapshotInsertsPerMinute(data, evalFromMs, evalToMs);

        double exportedTotal = 0;
        foreach (double value in realRates.Values)
        {
            exportedTotal += value;
        }

        double? missing = null;
        if (inserts.HasValue)
        {
            missing = Math.Max(0, inserts.Value - exportedTotal);
        }

        report.AppendLine();
        report.AppendLine("## 2. Polls per minute per lane");
        report.AppendLine();
        report.AppendLine("Fast and NormalOnline are compared with the exported snapshots (retention removed almost " +
                          "nothing from them). The offline lanes lost the snapshots of pruned servers, so they are compared " +
                          "together with the 44 polls per minute measured on 2026-11-12 over the previous 24 h (from about noon UTC), against the simulation over 2026-11-11 12:00 to 11-12 12:00. " +
                          "The full period is compared with the exported rate plus the whole gap between the write counter and the " +
                          "export; it is an upper bound, because the gap also holds the lost Watched and Discovery polls of pruned servers.");
        report.AppendLine();
        report.AppendLine("| Lane | Simulated | Exported | Measured 12.11 | Reference | Difference | Pass |");
        report.AppendLine("| --- | --- | --- | --- | --- | --- | --- |");
        double simOffline = 0;
        double exportedOffline = 0;
        for (int k = 0; k < SimLanes.Length; k++)
        {
            string simLane = SimLanes[k];
            string realLane = RealLanes[k];

            double sim = 0;
            LaneRate? rate;
            if (simRates.TryGetValue(simLane, out rate))
            {
                sim = rate.PollsPerMinute;
            }

            double exported = 0;
            double exportedValue;
            if (realRates.TryGetValue(realLane, out exportedValue))
            {
                exported = exportedValue;
            }

            bool online = simLane == "Fast" || simLane == "NormalOnline";
            if (!online)
            {
                simOffline += sim;
                exportedOffline += exported;
            }

            string line = $"| {simLane} | {sim:0.0} | {exported:0.0} | {Measured1112[simLane]:0} | ";
            if (online)
            {
                bool ok = Within(sim, exported, LaneTolerance);
                if (!ok)
                {
                    passed = false;
                }

                report.AppendLine(line + $"{exported:0.0} (exported) | {Diff(sim, exported)} | {Yes(ok)} |");
            }
            else
            {
                report.AppendLine(line + $"see combined | {Diff(sim, Measured1112[simLane])} vs 12.11 | - |");
            }
        }

        const double offlineMeasured = 44;
        long dayFrom = MeasuredWindowStart.ToUnixTimeMilliseconds();
        long dayTo = dayFrom + 86_400_000L;
        if (dayFrom >= evalFromMs && dayTo <= evalToMs)
        {
            Dictionary<string, LaneRate> dayRates = new Dictionary<string, LaneRate>();
            foreach (LaneRate rate in RunReport.LaneRates(result, dayFrom, dayTo))
            {
                dayRates.Add(rate.Lane, rate);
            }

            double simOfflineDay = 0;
            string[] offlineLanes = { "SlowOffline", "VeryStaleOffline" };
            foreach (string lane in offlineLanes)
            {
                double lanePolls = 0;
                LaneRate? dayRate;
                if (dayRates.TryGetValue(lane, out dayRate))
                {
                    lanePolls = dayRate.PollsPerMinute;
                }

                simOfflineDay += lanePolls;
            }

            bool offlineOk = Within(simOfflineDay, offlineMeasured, LaneTolerance);
            if (!offlineOk)
            {
                passed = false;
            }

            report.AppendLine($"| Offline lanes combined, 12.11 | {simOfflineDay:0.0} | | {offlineMeasured:0} | " +
                              $"{offlineMeasured:0} (12.11) | {Diff(simOfflineDay, offlineMeasured)} | {Yes(offlineOk)} |");
        }

        report.AppendLine($"| Offline lanes combined, period | {simOffline:0.0} | {exportedOffline:0.0} | | | | - |");
        if (missing.HasValue)
        {
            double gap = missing.Value;
            double upper = exportedOffline + gap;
            report.AppendLine($"| Offline lanes, upper bound | {simOffline:0.0} | {exportedOffline:0.0} | | " +
                              $"{upper:0.0} (exported + {gap:0.0} lost) | {Diff(simOffline, upper)} | - |");
        }

        double simExogenous = 0;
        foreach (LaneRate rate in simRates.Values)
        {
            if (rate.Exogenous)
            {
                simExogenous += rate.PollsPerMinute;
            }
        }

        double simAll = simRates["StrategyTotal"].PollsPerMinute + simExogenous;

        string counterText = "n/a";
        string counterDiff = "";
        if (inserts.HasValue)
        {
            double x = inserts.Value;
            counterText = $"{x:0.0} (write counter)";
            counterDiff = Diff(simAll, x);
        }

        report.AppendLine($"| All polls incl. Watched/Discovery | {simAll:0.0} | {exportedTotal:0.0} | | " +
                          $"{counterText} | " +
                          $"{counterDiff} | - |");
        report.AppendLine();
        report.AppendLine($"Unanswered share of the strategy's polls: {simRates["StrategyTotal"].UnansweredShare:P1}.");
        (double Known, double Phantoms) population = result.AveragePopulation(evalFromMs, evalToMs);
        report.AppendLine($"Population: {population.Known:0} known servers and {population.Phantoms:0} phantoms on average.");

        StringBuilder perServer = new StringBuilder("server_idx,stratum,lane,simulated_polls,real_polls\n");
        List<StratumLaneTotal> stratumTotals = new List<StratumLaneTotal>();
        foreach (SampleServer sample in data.SampleServers)
        {
            SnapshotSeries snapshots = data.Snapshots[sample.ServerIndex];
            for (int k = 0; k < SimLanes.Length; k++)
            {
                string simLane = SimLanes[k];
                string realLane = RealLanes[k];

                int sim = 0;
                LaneLog? log;
                if (result.Lanes.TryGetValue(simLane, out log))
                {
                    foreach (long t in log.SamplePolls[sample.ServerIdx - 1])
                    {
                        if (t >= evalFromMs && t < evalToMs)
                        {
                            sim++;
                        }
                    }
                }

                int realCount = 0;
                for (int s = 0; s < snapshots.Count; s++)
                {
                    if (snapshots.TimesMs[s] >= evalFromMs && snapshots.TimesMs[s] < evalToMs
                        && snapshots.Sources[s].ToString() == realLane)
                    {
                        realCount++;
                    }
                }

                perServer.AppendLine($"{sample.ServerIdx},{sample.Stratum},{simLane},{sim},{realCount}");

                string stratumName = sample.Stratum.ToString();
                StratumLaneTotal? total = null;
                foreach (StratumLaneTotal existing in stratumTotals)
                {
                    if (existing.Stratum == stratumName && existing.Lane == simLane)
                    {
                        total = existing;
                        break;
                    }
                }

                if (total == null)
                {
                    total = new StratumLaneTotal();
                    total.Stratum = stratumName;
                    total.Lane = simLane;
                    stratumTotals.Add(total);
                }

                total.Sim = total.Sim + sim;
                total.Real = total.Real + realCount;
            }
        }

        File.WriteAllText(Path.Combine(outDir, "sample_server_polls.csv"), perServer.ToString());
        report.AppendLine();
        report.AppendLine("## 3. Polls of sample servers per lane (totals; per server in sample_server_polls.csv)");
        report.AppendLine();
        report.AppendLine("| Stratum | Lane | Simulated | Real |");
        report.AppendLine("| --- | --- | --- | --- |");

        List<StratumLaneTotal> sortedTotals = new List<StratumLaneTotal>();
        foreach (StratumLaneTotal total in stratumTotals)
        {
            int position = sortedTotals.Count;
            while (position > 0 && CompareTotals(sortedTotals[position - 1], total) > 0)
            {
                position--;
            }

            sortedTotals.Insert(position, total);
        }

        foreach (StratumLaneTotal total in sortedTotals)
        {
            string stratum = total.Stratum;
            string lane = total.Lane;
            long sim = total.Sim;
            long realCount = total.Real;
            report.AppendLine($"| {stratum} | {lane} | {sim} | {realCount} |");
        }

        report.AppendLine();
        report.AppendLine("## 4. Ranking agreement on the sample servers");
        report.AppendLine();
        AppendRankingAgreement(report, outDir, data, result);

        report.AppendLine();
        report.AppendLine("## Limitations");
        report.AppendLine();
        foreach (string note in notes)
        {
            report.AppendLine($"- {note}");
        }

        report.AppendLine();
        report.AppendLine($"**Overall: {(passed ? "PASS" : "FAIL")}** (only rows marked Pass/Fail count).");
        File.WriteAllText(Path.Combine(outDir, "validation.md"), report.ToString());
        return passed;
    }

    private static void AppendHalf(StringBuilder report, SimulatorData data, SimulationResult result, string label, long from, long to)
    {
        IReadOnlyList<FreshnessSummary> realHalf = RunReport.EvaluateReal(data, from, to);
        foreach (FreshnessSummary sim in RunReport.EvaluateSimulated(data, result, from, to))
        {
            FreshnessSummary actual = FindGroup(realHalf, sim.Group);
            if (sim.PlayerWeightedAoiSeconds.HasValue && actual.PlayerWeightedAoiSeconds.HasValue)
            {
                double s = sim.PlayerWeightedAoiSeconds.Value;
                double r = actual.PlayerWeightedAoiSeconds.Value;
                report.AppendLine($"| {label} | {sim.Group} | {s:0.0} | {r:0.0} | {Diff(s, r)} |");
            }
        }
    }

    private static void AppendRankingAgreement(StringBuilder report, string outDir, SimulatorData data, SimulationResult result)
    {
        List<RankingRegeneration> regenerations = new List<RankingRegeneration>();
        foreach (RankingRegeneration regeneration in result.Rankings)
        {
            int position = regenerations.Count;
            while (position > 0 && regenerations[position - 1].AtMs > regeneration.AtMs)
            {
                position--;
            }

            regenerations.Insert(position, regeneration);
        }

        List<DateTimeOffset> sampleTimes = new List<DateTimeOffset>();
        Dictionary<DateTimeOffset, List<SampleRankingRow>> samplesByTime = new Dictionary<DateTimeOffset, List<SampleRankingRow>>();
        foreach (SampleRankingRow row in data.SampleRankings)
        {
            List<SampleRankingRow>? group;
            if (!samplesByTime.TryGetValue(row.SampledAtUtc, out group))
            {
                group = new List<SampleRankingRow>();
                samplesByTime.Add(row.SampledAtUtc, group);
                sampleTimes.Add(row.SampledAtUtc);
            }

            group.Add(row);
        }

        sampleTimes.Sort();

        StringBuilder rows = new StringBuilder("sampled_at,sim_ranking_at,servers,ranked_status_agree,exact_rank_agree,both_ranked,mean_abs_rank_diff\n");
        long servers = 0;
        long statusAgree = 0;
        long exactAgree = 0;
        long bothRanked = 0;
        double absDiff = 0;

        foreach (DateTimeOffset sampledAt in sampleTimes)
        {
            long atMs = sampledAt.ToUnixTimeMilliseconds();
            if (atMs < result.FromMs || atMs >= result.ToMs)
            {
                continue;
            }

            RankingRegeneration? active = null;
            for (int i = regenerations.Count - 1; i >= 0; i--)
            {
                if (regenerations[i].AtMs <= atMs)
                {
                    active = regenerations[i];
                    break;
                }
            }

            Dictionary<Guid, int?> production = new Dictionary<Guid, int?>();
            foreach (SampleRankingRow row in samplesByTime[sampledAt])
            {
                production.Add(row.ServerId, row.Rank);
            }

            long n = 0;
            long status = 0;
            long exact = 0;
            long both = 0;
            double diff = 0;
            foreach (SampleServer server in data.SampleServers)
            {
                int? simRank = null;
                if (active != null)
                {
                    simRank = active.SampleRanks[server.ServerIdx - 1];
                }

                int? realRank = null;
                int? productionRank;
                if (production.TryGetValue(server.ServerId, out productionRank))
                {
                    realRank = productionRank;
                }

                n++;
                if (simRank.HasValue == realRank.HasValue)
                {
                    status++;
                }

                if (simRank == realRank)
                {
                    exact++;
                }

                if (simRank.HasValue && realRank.HasValue)
                {
                    both++;
                    diff += Math.Abs(simRank.Value - realRank.Value);
                }
            }

            string activeAt = "";
            if (active != null)
            {
                activeAt = RunReport.Iso(active.AtMs);
            }

            double meanDiff = 0;
            if (both != 0)
            {
                meanDiff = diff / both;
            }

            rows.AppendLine(RunReport.Invariant(
                $"{RunReport.Iso(atMs)},{activeAt},{n},{status},{exact},{both},{meanDiff:0.##}"));
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

        double meanAbsDiff = 0;
        if (bothRanked != 0)
        {
            meanAbsDiff = absDiff / bothRanked;
        }

        List<string> regenerationTexts = new List<string>();
        foreach (RankingRegeneration r in result.Rankings)
        {
            regenerationTexts.Add($"{RunReport.Iso(r.AtMs)}{(r.Initial ? " (initial)" : "")} [{r.RankedCount}]");
        }

        report.AppendLine($"Over every hourly production sample in the period ({servers} server-hours):");
        report.AppendLine();
        report.AppendLine($"- ranked / not ranked agrees: {(double)statusAgree / servers:P1}");
        report.AppendLine($"- exact rank (or both unranked) agrees: {(double)exactAgree / servers:P1}");
        report.AppendLine($"- mean |rank difference| where both ranked: {meanAbsDiff:0.0} over {bothRanked} server-hours");
        report.AppendLine($"- regenerations: {string.Join(", ", regenerationTexts)}");
    }

    private static bool AppendComparison(StringBuilder report, string group, string metric, double? sim, double? real, double? tolerance, bool counts)
    {
        string pass = "-";
        bool ok = true;
        if (tolerance.HasValue && counts)
        {
            ok = sim.HasValue && real.HasValue && Within(sim.Value, real.Value, tolerance.Value);
            pass = Yes(ok);
        }

        string difference = "";
        if (sim.HasValue && real.HasValue)
        {
            difference = Diff(sim.Value, real.Value);
        }

        report.AppendLine(RunReport.Invariant(
            $"| {group} | {metric} | {sim:0.###} | {real:0.###} | {difference} | {pass} |"));
        return ok;
    }

    private static int CompareTotals(StratumLaneTotal a, StratumLaneTotal b)
    {
        int byStratum = string.Compare(a.Stratum, b.Stratum);
        if (byStratum != 0)
        {
            return byStratum;
        }

        return RunReport.LaneOrder(a.Lane).CompareTo(RunReport.LaneOrder(b.Lane));
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

    private static bool Within(double sim, double real, double tolerance)
    {
        if (real == 0)
        {
            return sim == 0;
        }

        return Math.Abs(sim - real) / Math.Abs(real) <= tolerance;
    }

    private static string Diff(double sim, double real)
    {
        if (real == 0)
        {
            return "n/a";
        }

        return RunReport.Invariant($"{(sim - real) / real:+0.0%;-0.0%;0.0%}");
    }

    private static string Yes(bool ok)
    {
        if (ok)
        {
            return "PASS";
        }

        return "FAIL";
    }
}
