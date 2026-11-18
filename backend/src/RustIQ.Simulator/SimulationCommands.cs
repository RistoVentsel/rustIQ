using System.Globalization;
using System.Text;
using RustIQ.Application.Servers.Polling;
using RustIQ.Simulator.Data;
using RustIQ.Simulator.Engine;
using RustIQ.Simulator.Evaluation;
using RustIQ.Simulator.Reporting;

namespace RustIQ.Simulator;

/// <summary>The <c>run</c>, <c>sweep</c> and <c>tune</c> commands (plan sections 6 and 9).</summary>
public static class SimulationCommands
{
    public static readonly DateTimeOffset DefaultFrom = DateTimeOffset.Parse("2026-11-04T00:00:00Z", CultureInfo.InvariantCulture);
    public static readonly DateTimeOffset DefaultTo = DateTimeOffset.Parse("2026-11-18T00:00:00Z", CultureInfo.InvariantCulture);
    public static readonly DateTimeOffset DefaultSplit = DateTimeOffset.Parse("2026-11-11T00:00:00Z", CultureInfo.InvariantCulture);

    private static readonly string[] SharedOptions =
        ["data", "from", "to", "eval-from", "split", "seed", "out", "no-checksums", "ranking",
         "no-timing", "answered-poll-seconds", "unanswered-poll-seconds", "jitter-ms", "concurrency"];

    private static readonly string[] PriorityOptionNames =
        ["min-interval", "max-backoff", "wipe-boost", "ewma-alpha", "prior-changes", "prior-seconds", "score", "max-age", "max-age-share", "no-predicted-wipes", "flat-backoff"];

    /// <summary>One run of one strategy; with <c>--validate</c> also the plan 8 validation.</summary>
    public static void Run(CommandLine commandLine, Action<string> log)
    {
        CheckOptions(commandLine, ["strategy", "budget", "batch-scale", "validate", .. PriorityOptionNames]);
        var period = Period.From(commandLine);
        var data = SimulatorCommands.LoadData(commandLine, log);
        period = period.ClampedTo(data);
        var outDir = commandLine.Required("out");

        var strategy = CreateStrategy(commandLine, commandLine.Required("strategy"), commandLine.Optional("budget") is { } b ? Parse(b) : null, commandLine.Double("batch-scale", 1));
        var result = Simulate(data, strategy, period, commandLine, log);

        RunReport.WriteRunFiles(outDir, data, result, period.EvalFromMs, period.ToMs, period.SplitMs, Arguments(commandLine));
        PrintRun(result, data, period, log);

        if (commandLine.Has("validate"))
        {
            var passed = ValidationReport.Write(outDir, data, result, period.EvalFromMs, period.ToMs, Limitations(data, period));
            log($"validation: {(passed ? "PASS" : "FAIL")}, see {Path.Combine(outDir, "validation.md")}");
        }

        log($"wrote {outDir}");
    }

    /// <summary>
    /// The main comparison and the budget sweep: fourlane at production settings fixes the reference
    /// budget; then fourlane scaled towards each <c>--budgets</c> value, and the other strategies at the
    /// volume fourlane reached there.
    /// </summary>
    public static void Sweep(CommandLine commandLine, Action<string> log)
    {
        CheckOptions(commandLine, ["budgets", "strategies", .. PriorityOptionNames]);
        var period = Period.From(commandLine);
        var data = SimulatorCommands.LoadData(commandLine, log);
        period = period.ClampedTo(data);
        var outDir = commandLine.Required("out");
        var budgets = commandLine.Has("budgets")
            ? commandLine.Values("budgets").Select(Parse).ToArray()
            : [150, 200, 350, 450];
        var strategies = commandLine.Has("strategies") ? commandLine.Values("strategies").ToArray() : ["fourlane", "uniform", "priority"];

        var rows = new List<string>();
        var baseline = Simulate(data, CreateStrategy(commandLine, "fourlane", null, 1), period, commandLine, log);
        // Rounded to 0.1, so a run given the printed budget by hand reproduces the sweep exactly.
        var baselineBudget = Math.Round(StrategyBudget(baseline, period), 1);
        log($"fourlane at production settings: {baselineBudget:0.0} queries per minute");
        RunReport.WriteRunFiles(Path.Combine(outDir, "fourlane-production"), data, baseline, period.EvalFromMs, period.ToMs, period.SplitMs, Arguments(commandLine));
        rows.AddRange(SweepRows("fourlane", "production", 1, baseline, data, period));

        // Each sweep point: fourlane with its batches scaled towards the target, then the budget strategies
        // at the volume fourlane actually reached, so every comparison is at the same query volume.
        foreach (var target in new double?[] { null }.Concat(budgets.Select(b => (double?)b)))
        {
            var label = target is { } t ? t.ToString("0", CultureInfo.InvariantCulture) : "fourlane-budget";
            var achieved = baselineBudget;
            if (target is { } targetBudget && strategies.Contains("fourlane"))
            {
                var scale = targetBudget / baselineBudget;
                var fourlane = Simulate(data, CreateStrategy(commandLine, "fourlane", null, scale), period, commandLine, log);
                achieved = Math.Round(StrategyBudget(fourlane, period), 1);
                WritePoint("fourlane", label, scale, fourlane);
            }
            else if (target is { } plain)
            {
                achieved = plain;
            }

            foreach (var name in strategies.Where(name => name != "fourlane"))
            {
                WritePoint(name, label, 1, Simulate(data, CreateStrategy(commandLine, name, achieved, 1), period, commandLine, log));
            }
        }

        void WritePoint(string name, string label, double scale, SimulationResult result)
        {
            RunReport.WriteRunFiles(Path.Combine(outDir, $"{name}-{label}"), data, result, period.EvalFromMs, period.ToMs, period.SplitMs, Arguments(commandLine));
            rows.AddRange(SweepRows(name, label, scale, result, data, period));
            log($"{name} at {label}: {StrategyBudget(result, period):0.0} queries per minute, " +
                $"overall mean AoI {RunReport.EvaluateSimulated(data, result, period.EvalFromMs, period.ToMs).Single(r => r.Group == FreshnessEvaluator.AllGroup).AoiMeanSeconds:0} s");
        }

        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, "sweep.csv"),
            "strategy,budget_label,batch_scale,window,queries_per_min,unanswered_share," + SummaryCsv.Header + "\n" +
            string.Join("\n", rows) + "\n");
        log($"wrote {Path.Combine(outDir, "sweep.csv")}");
    }

    /// <summary>
    /// Tunes Priority on the tuning window only (plan 6.3: 2026-11-04 to 2026-11-11 by default) at a fixed
    /// budget, by a small grid. The objective is the overall player-weighted AoI, averaged over the usual
    /// evaluation grid and one shifted by 7 min 30 s.
    /// </summary>
    public static void Tune(CommandLine commandLine, Action<string> log)
    {
        CheckOptions(commandLine, ["budget", "min-intervals", "prior-seconds-values", "wipe-boosts", "ewma-alphas", "objective", "max-age", "max-age-share", "flat-backoff"]);
        var period = Period.From(commandLine);
        var data = SimulatorCommands.LoadData(commandLine, log);
        period = period.ClampedTo(data);

        // Only the tuning window is simulated, so nothing after the split can influence the choice.
        var tuningEnd = period.SplitMs ?? period.ToMs;
        var tuning = period with { ToMs = tuningEnd, SplitMs = null };
        var budget = Parse(commandLine.Required("budget"));
        var objective = commandLine.Optional("objective") ?? "pw-aoi";

        var grid =
            from minInterval in Values(commandLine, "min-intervals", [45, 90])
            from priorSeconds in Values(commandLine, "prior-seconds-values", [600, 1800, 3600])
            from wipeBoost in Values(commandLine, "wipe-boosts", [1])
            from alpha in Values(commandLine, "ewma-alphas", [0.2])
            select new PriorityOptions
            {
                MinIntervalSeconds = minInterval,
                PriorSeconds = priorSeconds,
                WipeBoost = wipeBoost,
                PlayersEwmaAlpha = alpha,
                MaxAgeSeconds = commandLine.Double("max-age", 0),
                MaxAgeBudgetShare = commandLine.Has("max-age")
                    ? 0
                    : commandLine.Double("max-age-share", PriorityOptions.DefaultMaxAgeBudgetShare),
                BackoffScalesWithWeight = !commandLine.Has("flat-backoff")
            };

        var lines = new List<string>();
        (double Score, PriorityOptions Options)? best = null;
        foreach (var options in grid)
        {
            var result = Simulate(data, new PriorityPollingStrategy(budget, options), tuning, commandLine, log);
            // Scored on the usual 15 min grid and on one shifted by half a step, so a minimum interval that
            // happens to divide 900 s cannot win by lining its polls up with the grid.
            var all = RunReport.EvaluateSimulated(data, result, tuning.EvalFromMs, tuning.ToMs).Single(r => r.Group == FreshnessEvaluator.AllGroup);
            var shifted = RunReport.EvaluateSimulated(data, result, tuning.EvalFromMs + RunReport.ShiftedGridOffsetMs, tuning.ToMs)
                .Single(r => r.Group == FreshnessEvaluator.AllGroup);
            double Objective(FreshnessSummary row) => objective switch
            {
                "pw-aoi" => row.PlayerWeightedAoiSeconds ?? double.MaxValue,
                "mean-aoi" => row.AoiMeanSeconds ?? double.MaxValue,
                "mae" => row.PlayerMae ?? double.MaxValue,
                _ => throw new ArgumentException("--objective is pw-aoi, mean-aoi or mae.")
            };
            var score = (Objective(all) + Objective(shifted)) / 2;
            lines.Add(RunReport.Invariant(
                $"{options.MinIntervalSeconds},{options.PriorSeconds},{options.WipeBoost},{options.PlayersEwmaAlpha},{options.MaxAgeSeconds},{all.AoiMeanSeconds:0.#},{all.AoiMedianSeconds:0.#},{all.PlayerWeightedAoiSeconds:0.#},{all.PlayerMae:0.###},{shifted.AoiMeanSeconds:0.#},{shifted.AoiMedianSeconds:0.#},{shifted.PlayerWeightedAoiSeconds:0.#},{shifted.PlayerMae:0.###}"));
            log(lines[^1]);
            if (best is null || score < best.Value.Score)
            {
                best = (score, options);
            }
        }

        var outDir = commandLine.Required("out");
        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, "tuning.csv"),
            "min_interval_s,prior_seconds,wipe_boost,ewma_alpha,aoi_mean_s,aoi_median_s,player_weighted_aoi_s,player_mae\n" +
            string.Join("\n", lines) + "\n");
        var chosen = best!.Value.Options;
        log(RunReport.Invariant(
            $"best by {objective}, mean of both grids: --min-interval {chosen.MinIntervalSeconds} --prior-seconds {chosen.PriorSeconds} --wipe-boost {chosen.WipeBoost} --ewma-alpha {chosen.PlayersEwmaAlpha}"));
    }

    public static SimulationResult Simulate(SimulatorData data, IPollingStrategy strategy, Period period, CommandLine commandLine, Action<string> log)
    {
        var options = new SimulationOptions
        {
            FromMs = period.FromMs,
            ToMs = period.ToMs,
            Seed = commandLine.Int("seed", 1),
            Ranking = (commandLine.Optional("ranking") ?? "frozen") switch
            {
                "frozen" => RankingMode.Frozen,
                "hourly" => RankingMode.Hourly,
                _ => throw new ArgumentException("--ranking is frozen or hourly.")
            },
            Timing = commandLine.Has("no-timing")
                ? null
                : new WorkerTiming
                {
                    Concurrency = commandLine.Int("concurrency", 32),
                    AnsweredPollSeconds = commandLine.Double("answered-poll-seconds", 0.5),
                    UnansweredPollSeconds = commandLine.Double("unanswered-poll-seconds", 6),
                    JitterMs = commandLine.Int("jitter-ms", 50)
                }
        };
        if (strategy is PriorityPollingStrategy statistics)
        {
            statistics.StatisticsFromMs = period.EvalFromMs;
        }

        var started = DateTime.UtcNow;
        var result = new SimulationEngine(data, strategy, options).Run();
        log($"simulated {strategy.Name} in {(DateTime.UtcNow - started).TotalSeconds:0} s");
        if (strategy is PriorityPollingStrategy priority)
        {
            log($"priority: average maximum age {priority.AverageMaxAgeSeconds / 60:0.00} min, " +
                $"average servers believed online {priority.AverageOnlineServers:0}");
        }
        return result;
    }

    public static IPollingStrategy CreateStrategy(CommandLine commandLine, string name, double? budget, double batchScale) => name switch
    {
        "fourlane" => new FourLanePollingStrategy(new FourLaneOptions { BatchScale = batchScale }),
        "uniform" => new UniformPollingStrategy(budget ?? throw new ArgumentException("uniform needs --budget.")),
        "priority" => new PriorityPollingStrategy(
            budget ?? throw new ArgumentException("priority needs --budget."),
            new PriorityOptions
            {
                MinIntervalSeconds = commandLine.Double("min-interval", 165),
                MaxBackoffSeconds = commandLine.Double("max-backoff", 6 * 3600),
                // --max-age <s> fixes the maximum age; otherwise it follows the budget via --max-age-share.
                MaxAgeSeconds = commandLine.Double("max-age", 0),
                MaxAgeBudgetShare = commandLine.Has("max-age")
                    ? 0
                    : commandLine.Double("max-age-share", PriorityOptions.DefaultMaxAgeBudgetShare),
                BackoffScalesWithWeight = !commandLine.Has("flat-backoff"),
                UsePredictedWipes = !commandLine.Has("no-predicted-wipes"),
                WipeBoost = commandLine.Double("wipe-boost", 1),
                PlayersEwmaAlpha = commandLine.Double("ewma-alpha", 0.2),
                PriorChanges = commandLine.Double("prior-changes", 1),
                PriorSeconds = commandLine.Double("prior-seconds", 3600),
                Score = (commandLine.Optional("score") ?? "expected-age") switch
                {
                    "expected-age" => PriorityScore.ExpectedAge,
                    "change-probability" => PriorityScore.ChangeProbability,
                    _ => throw new ArgumentException("--score is expected-age or change-probability.")
                }
            }),
        _ => throw new ArgumentException($"Unknown strategy '{name}': fourlane, uniform or priority.")
    };

    public static double StrategyBudget(SimulationResult result, Period period) =>
        RunReport.LaneRates(result, period.EvalFromMs, period.ToMs).Single(rate => rate.Lane == "StrategyTotal").PollsPerMinute;

    public static IReadOnlyList<string> Limitations(SimulatorData data, Period period)
    {
        var notes = new List<string>();
        var firstSnapshot = data.Snapshots.Where(s => s.Count > 0).Select(s => s.TimesMs[0]).DefaultIfEmpty(long.MaxValue).Min();
        notes.Add($"The starting state of every server (players, status, last seen, last checked) comes from its last " +
                  $"snapshot before {RunReport.Iso(period.FromMs)}; only server_type and created_at come from the servers file. " +
                  $"The export's snapshots start at {RunReport.Iso(firstSnapshot)}" +
                  (firstSnapshot >= period.FromMs - 60_000
                      ? ", so every server starts as never polled and the first hours are a warm-up; " +
                        $"the comparison starts at {RunReport.Iso(period.EvalFromMs)}. The 7-day ranking window also has no history before the period."
                      : "."));
        notes.Add("Non-sample servers answer with the production snapshot nearest in time, so they are only known at production's poll times. " +
                  "This affects how the budget and lanes are spent, not the measured metrics.");
        notes.Add("Queue: server_snapshots has no queue column, so a non-sample server never shows a queue in the simulation. " +
                  "Production's popular rule 'online with a queue' (Fast lane) therefore only works for the 450 sample servers, " +
                  "whose probes carry the queue; a non-sample server with a queue but under 100 players and unranked lands in " +
                  "NormalOnline instead of Fast. Uniform and Priority do not use the queue.");
        notes.Add("The ranking ignores the name requirement and the host:port dedup, which are not in the export.");
        notes.Add("Frozen ranking moments are the hours in which a sample rank changed, at the minute past the hour of server_rankings.generated_at.");
        notes.Add("Priority was tuned on 2026-11-04 to 2026-11-11 only, at 272.4 queries per minute (fourlane's volume at " +
                  "production settings), on the overall player-weighted AoI, and not tuned again for the other volumes of the sweep.");
        notes.Add("Priority's back-off cap is divided by the server's weight (1 + EWMA of its players), never below the minimum interval. " +
                  "With the flat 6 h cap first proposed, a busy server that came back after an outage could stay unpolled for hours, " +
                  "and which servers got caught depended on the back-off's phase: a budget change of 0.01 % moved the player-weighted AoI " +
                  "by 15 %, more budget could make it worse (414 s at 272.4/min, 446 s at 339.1/min) and the minimum interval 150 s scored " +
                  "1235 against 583 for 165 s. Three busy servers that had multi-day outages (272, 37, 160) caused that jump. With the weighted " +
                  "cap the result is stable (190 vs 193 s for 272.4 vs 272.43/min) and falls with the budget (190, 168, 147 s at 272.4, 339.1, 417.6/min).");
        notes.Add("Priority's minimum interval is 165 s, from the middle of the stable range. With the weighted back-off the player-weighted AoI " +
                  "on the tuning window is flat from 90 s to 240 s (187 to 221 s), on the usual 15 min grid and on one shifted by 7 min 30 s alike " +
                  "(150 s: 187/191, 165 s: 193/198, 180 s: 215/219). Before that fix 180 s looked best, but only on the usual grid (557 against 604 " +
                  "on the shifted one): its 900 s = 5 x 180 s alignment with the grid flattered it.");
        notes.Add("Priority's maximum age follows the budget: max-age = servers it believes online / (0.619 x queries per second), " +
                  "recomputed every tick. The share 0.619 makes it 60 min on average at 272.4 queries per minute (2026-11-05 to 11-15, " +
                  "about 10,120 servers online), the value under which no stratum was worse than fourlane among 60/80/100 min. " +
                  "A server that answered within the last 24 h never waits longer than the maximum age, even while Priority thinks it is " +
                  "offline; the 6 h back-off cap only applies to servers not seen for over 24 h. The 'no stratum worse than fourlane' " +
                  "condition is checked at the production volume only; the other volumes are reported as they come.");
        notes.Add("Residual sensitivity (export 1, 2026-11-05 to 11-15): Priority's player-weighted AoI is 519, 421 and 455 s at 272.4, " +
                  "339.1 and 417.6 queries per minute, so the last step is not monotonic. Two servers cause it, 37 (Medium) and 175 (Popular), " +
                  "which were down for 41.7 h and 76.4 h. After more than 24 h unseen they fall under the 6 h back-off cap, so their return can " +
                  "go unnoticed for up to 6 h depending on the back-off's phase, and the player weighting magnifies that. In 2026-11-11 to 11-15 " +
                  "the result is monotonic (206, 156, 131 s). This follows from the agreed rule and was left as it is.");
        notes.Add("Phantom servers fill the gap between study_population_samples.TotalServers and the known servers; they never answer.");
        notes.Add("Worker timing is modelled, not replayed: 32 queries in flight, 0.5 s per answered and 6 s per unanswered query " +
                  "(two 3 s attempts), lanes one after another, PeriodicTimer's one kept tick, and a few ms of jitter in reading the clock. " +
                  "Without it the Fast lane always runs every 45 s (133 polls/min against production's 105).");
        return notes;
    }

    private static IEnumerable<string> SweepRows(string name, string label, double scale, SimulationResult result, SimulatorData data, Period period)
    {
        var windows = new List<(string Name, long From, long To)> { ("full", period.EvalFromMs, period.ToMs) };
        if (period.SplitMs is { } split && split > period.EvalFromMs && split < period.ToMs)
        {
            windows.Add(("before_split", period.EvalFromMs, split));
            windows.Add(("after_split", split, period.ToMs));
        }

        foreach (var (window, from, to) in windows)
        {
            var total = RunReport.LaneRates(result, from, to).Single(rate => rate.Lane == "StrategyTotal");
            var summary = SummaryCsv.Format(RunReport.EvaluateSimulated(data, result, from, to))
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Skip(1);
            foreach (var row in summary)
            {
                yield return RunReport.Invariant($"{name},{label},{scale:0.####},{window},{total.PollsPerMinute:0.##},{total.UnansweredShare:0.####},") + row;
            }
        }
    }

    private static void PrintRun(SimulationResult result, SimulatorData data, Period period, Action<string> log)
    {
        foreach (var rate in RunReport.LaneRates(result, period.EvalFromMs, period.ToMs))
        {
            log(RunReport.Invariant($"  {rate.Lane,-18} {rate.PollsPerMinute,7:0.0} per min, {rate.UnansweredShare,6:P1} unanswered"));
        }

        Console.WriteLine(SummaryCsv.FormatTable(RunReport.EvaluateSimulated(data, result, period.EvalFromMs, period.ToMs)));
    }

    private static Dictionary<string, object?> Arguments(CommandLine commandLine) =>
        commandLine.Options.ToDictionary(pair => pair.Key, pair => (object?)pair.Value.ToArray());

    private static void CheckOptions(CommandLine commandLine, IEnumerable<string> extra)
    {
        var allowed = SharedOptions.Concat(extra).ToHashSet(StringComparer.Ordinal);
        foreach (var name in commandLine.Options.Keys)
        {
            if (!allowed.Contains(name))
            {
                throw new ArgumentException($"Unknown option --{name} for {commandLine.Command}.");
            }
        }
    }

    private static double[] Values(CommandLine commandLine, string name, double[] fallback) =>
        commandLine.Has(name) ? commandLine.Values(name).Select(Parse).ToArray() : fallback;

    private static double Parse(string text) => double.Parse(text, CultureInfo.InvariantCulture);
}

/// <summary>The simulated period, the start of the compared window and the tuning split.</summary>
public sealed record Period(long FromMs, long ToMs, long EvalFromMs, long? SplitMs)
{
    public static Period From(CommandLine commandLine)
    {
        var from = commandLine.Time("from", SimulationCommands.DefaultFrom).ToUnixTimeMilliseconds();
        var to = commandLine.Time("to", SimulationCommands.DefaultTo).ToUnixTimeMilliseconds();
        var evalFrom = commandLine.Time("eval-from", DateTimeOffset.FromUnixTimeMilliseconds(from)).ToUnixTimeMilliseconds();
        var split = commandLine.Time("split", SimulationCommands.DefaultSplit).ToUnixTimeMilliseconds();
        if (to <= from || evalFrom < from || evalFrom >= to)
        {
            throw new ArgumentException("Need --from <= --eval-from < --to.");
        }

        return new Period(from, to, evalFrom, split);
    }

    /// <summary>Ends the period where the probes end, so no step runs without ground truth.</summary>
    public Period ClampedTo(SimulatorData data)
    {
        var lastProbe = data.Probes.Where(p => p.Count > 0).Select(p => p.TimesMs[^1]).DefaultIfEmpty(ToMs).Max();
        var end = Math.Min(ToMs, (lastProbe / 60_000 + 1) * 60_000);
        if (end <= EvalFromMs)
        {
            throw new ArgumentException("The data ends before the compared window starts.");
        }

        return this with { ToMs = end };
    }
}
