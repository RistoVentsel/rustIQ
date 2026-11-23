using RustIQ.Domain.Servers;
using RustIQ.Simulator.Data;
using RustIQ.Simulator.Evaluation;

namespace RustIQ.Simulator;

public static class SimulatorCommands
{
    public static SimulatorData LoadData(CommandLine commandLine, Action<string> log)
    {
        SimulatorDataLoadOptions options = new SimulatorDataLoadOptions
        {
            VerifyChecksums = !commandLine.Has("no-checksums"),
            Log = log
        };
        return SimulatorDataLoader.Load(commandLine.Values("data"), options);
    }

    public static void CheckData(CommandLine commandLine, Action<string> log)
    {
        SimulatorData data = LoadData(commandLine, log);

        int withRow = 0;
        int inLatest = 0;
        foreach (ServerRecord? server in data.Servers)
        {
            if (server != null)
            {
                withRow++;
                if (server.InLatestExport)
                {
                    inLatest++;
                }
            }
        }

        log($"servers known: {data.ServerIndex.Count}, with a servers row: {withRow}, " +
            $"still in the latest export: {inLatest}");

        foreach (ExportManifest manifest in data.Manifests)
        {
            long from = PostgresTimestamp.Parse(manifest.From).ToUnixTimeMilliseconds();
            long to = PostgresTimestamp.Parse(manifest.To).ToUnixTimeMilliseconds();
            double minutes = (to - from) / 60_000.0;
            log($"export {Path.GetFileName(manifest.Folder)}: {manifest.From} .. {manifest.To} ({minutes:0} min)");

            long probes = 0;
            foreach (ProbeSeries series in data.Probes)
            {
                foreach (long t in series.TimesMs)
                {
                    if (t >= from && t < to)
                    {
                        probes++;
                    }
                }
            }

            log($"  probes per minute: {probes / minutes:0.0}");

            Dictionary<ServerPollSource, long> perSource = new Dictionary<ServerPollSource, long>();
            foreach (SnapshotSeries series in data.Snapshots)
            {
                for (int i = 0; i < series.Count; i++)
                {
                    if (series.TimesMs[i] >= from && series.TimesMs[i] < to)
                    {
                        ServerPollSource source = series.Sources[i];
                        long countSoFar;
                        perSource.TryGetValue(source, out countSoFar);
                        perSource[source] = countSoFar + 1;
                    }
                }
            }

            List<KeyValuePair<ServerPollSource, long>> sorted = new List<KeyValuePair<ServerPollSource, long>>();
            foreach (KeyValuePair<ServerPollSource, long> pair in perSource)
            {
                sorted.Add(pair);
            }

            for (int i = 1; i < sorted.Count; i++)
            {
                KeyValuePair<ServerPollSource, long> current = sorted[i];
                int j = i - 1;
                while (j >= 0 && sorted[j].Value < current.Value)
                {
                    sorted[j + 1] = sorted[j];
                    j--;
                }

                sorted[j + 1] = current;
            }

            foreach (KeyValuePair<ServerPollSource, long> pair in sorted)
            {
                log($"  snapshots per minute, {pair.Key}: {pair.Value / minutes:0.0}");
            }

            long allSnapshots = 0;
            foreach (long count in perSource.Values)
            {
                allSnapshots += count;
            }

            log($"  snapshots per minute, all: {allSnapshots / minutes:0.0}");
        }

        HashSet<DateTimeOffset> sampleTimes = new HashSet<DateTimeOffset>();
        foreach (SampleRankingRow row in data.SampleRankings)
        {
            sampleTimes.Add(row.SampledAtUtc);
        }

        DateTimeOffset latestRanking = default(DateTimeOffset);
        bool foundRanking = false;
        foreach (ServerRankingRow row in data.Rankings)
        {
            if (!foundRanking || row.GeneratedAtUtc > latestRanking)
            {
                latestRanking = row.GeneratedAtUtc;
                foundRanking = true;
            }
        }

        log($"sample ranking samples: {sampleTimes.Count}, " +
            $"ranking generated at: {latestRanking:u}");
    }

    public static void EvaluateReal(CommandLine commandLine, Action<string> log)
    {
        DateTimeOffset from = commandLine.Time("from", DateTimeOffset.Parse("2026-11-05T00:00:00Z"));
        DateTimeOffset to = commandLine.Time("to", DateTimeOffset.Parse("2026-11-12T00:00:00Z"));
        SimulatorData data = LoadData(commandLine, log);

        EvaluationInput[] inputs = new EvaluationInput[data.SampleServers.Count];
        for (int i = 0; i < data.SampleServers.Count; i++)
        {
            SampleServer server = data.SampleServers[i];
            inputs[i] = new EvaluationInput(
                server.Stratum,
                data.Probes[server.ServerIdx - 1],
                ShownSeries.FromSnapshots(data.Snapshots[server.ServerIndex]));
        }

        IReadOnlyList<FreshnessSummary> summary = FreshnessEvaluator.Evaluate(inputs, from.ToUnixTimeMilliseconds(), to.ToUnixTimeMilliseconds());

        log($"real snapshots, {from:u} .. {to:u}");
        Console.WriteLine(SummaryCsv.FormatTable(summary));

        string? outDir = commandLine.Optional("out");
        if (outDir != null)
        {
            Directory.CreateDirectory(outDir);
            File.WriteAllText(Path.Combine(outDir, "summary.csv"), SummaryCsv.Format(summary));
            log($"wrote {Path.Combine(outDir, "summary.csv")}");
        }
    }
}
