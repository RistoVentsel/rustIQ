using RustIQ.Domain.Servers;
using RustIQ.Simulator.Data;
using RustIQ.Simulator.Evaluation;

namespace RustIQ.Simulator;

public static class SimulatorCommands
{
    public static SimulatorData LoadData(CommandLine commandLine, Action<string> log) =>
        SimulatorDataLoader.Load(commandLine.Values("data"), new SimulatorDataLoadOptions
        {
            VerifyChecksums = !commandLine.Has("no-checksums"),
            Log = log
        });

    /// <summary>Step 1: loads the exports and prints the figures the plan's "known facts" are checked against.</summary>
    public static void CheckData(CommandLine commandLine, Action<string> log)
    {
        var data = LoadData(commandLine, log);
        log($"servers known: {data.ServerIndex.Count}, with a servers row: {data.Servers.Count(s => s is not null)}, " +
            $"still in the latest export: {data.Servers.Count(s => s?.InLatestExport == true)}");

        foreach (var manifest in data.Manifests)
        {
            var from = PostgresTimestamp.Parse(manifest.From).ToUnixTimeMilliseconds();
            var to = PostgresTimestamp.Parse(manifest.To).ToUnixTimeMilliseconds();
            var minutes = (to - from) / 60_000.0;
            log($"export {Path.GetFileName(manifest.Folder)}: {manifest.From} .. {manifest.To} ({minutes:0} min)");

            var probes = data.Probes.Sum(series => (long)series.TimesMs.Count(t => t >= from && t < to));
            log($"  probes per minute: {probes / minutes:0.0}");

            var perSource = new Dictionary<ServerPollSource, long>();
            foreach (var series in data.Snapshots)
            {
                for (var i = 0; i < series.Count; i++)
                {
                    if (series.TimesMs[i] >= from && series.TimesMs[i] < to)
                    {
                        perSource[series.Sources[i]] = perSource.GetValueOrDefault(series.Sources[i]) + 1;
                    }
                }
            }

            foreach (var (source, count) in perSource.OrderByDescending(pair => pair.Value))
            {
                log($"  snapshots per minute, {source}: {count / minutes:0.0}");
            }

            log($"  snapshots per minute, all: {perSource.Values.Sum() / minutes:0.0}");
        }

        log($"sample ranking samples: {data.SampleRankings.Select(row => row.SampledAtUtc).Distinct().Count()}, " +
            $"ranking generated at: {data.Rankings.Select(row => row.GeneratedAtUtc).DefaultIfEmpty().Max():u}");
    }

    /// <summary>Step 2: the evaluator on the real production snapshots, to compare with Table 5.</summary>
    public static void EvaluateReal(CommandLine commandLine, Action<string> log)
    {
        var from = commandLine.Time("from", DateTimeOffset.Parse("2026-11-05T00:00:00Z"));
        var to = commandLine.Time("to", DateTimeOffset.Parse("2026-11-12T00:00:00Z"));
        var data = LoadData(commandLine, log);

        var inputs = data.SampleServers
            .Select(server => new EvaluationInput(
                server.Stratum,
                data.Probes[server.ServerIdx - 1],
                ShownSeries.FromSnapshots(data.Snapshots[server.ServerIndex])))
            .ToArray();
        var summary = FreshnessEvaluator.Evaluate(inputs, from.ToUnixTimeMilliseconds(), to.ToUnixTimeMilliseconds());

        log($"real snapshots, {from:u} .. {to:u}");
        Console.WriteLine(SummaryCsv.FormatTable(summary));

        if (commandLine.Optional("out") is { } outDir)
        {
            Directory.CreateDirectory(outDir);
            File.WriteAllText(Path.Combine(outDir, "summary.csv"), SummaryCsv.Format(summary));
            log($"wrote {Path.Combine(outDir, "summary.csv")}");
        }
    }
}
