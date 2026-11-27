using System.Globalization;
using System.Text;
using RustIQ.Application.Servers.Polling;
using RustIQ.Simulator.Data;
using RustIQ.Simulator.Engine;
using RustIQ.Simulator.Evaluation;
using RustIQ.Simulator.Reporting;

namespace RustIQ.Simulator;

public static class SimulationCommands
{
    public static readonly DateTimeOffset DefaultFrom = DateTimeOffset.Parse("2026-11-04T00:00:00Z", CultureInfo.InvariantCulture);
    public static readonly DateTimeOffset DefaultTo = DateTimeOffset.Parse("2026-11-18T00:00:00Z", CultureInfo.InvariantCulture);
    public static readonly DateTimeOffset DefaultSplit = DateTimeOffset.Parse("2026-11-11T00:00:00Z", CultureInfo.InvariantCulture);

    private static readonly string[] SharedOptions = new string[]
    {
        "data", "from", "to", "eval-from", "split", "seed", "out", "no-checksums", "ranking",
        "no-timing", "answered-poll-seconds", "unanswered-poll-seconds", "jitter-ms", "concurrency"
    };

    private static readonly string[] PriorityOptionNames = new string[]
    {
        "min-interval", "max-backoff", "wipe-boost", "ewma-alpha", "prior-changes", "prior-seconds", "score", "max-age", "max-age-share", "no-predicted-wipes", "flat-backoff"
    };

    public static void Run(CommandLine commandLine, Action<string> log)
    {
        List<string> allowedOptions = new List<string> { "strategy", "budget", "batch-scale", "validate", "normal-batch", "offline-scale" };
        allowedOptions.AddRange(PriorityOptionNames);
        CheckOptions(commandLine, allowedOptions);

        Period period = Period.From(commandLine);
        SimulatorData data = SimulatorCommands.LoadData(commandLine, log);
        period = period.ClampedTo(data);
        string outDir = commandLine.Required("out");

        string strategyName = commandLine.Required("strategy");
        double? budget = null;
        string? budgetText = commandLine.Optional("budget");
        if (budgetText != null)
        {
            budget = Parse(budgetText);
        }
        double batchScale = commandLine.Double("batch-scale", 1);

        IPollingStrategy strategy = CreateStrategy(commandLine, strategyName, budget, batchScale);
        SimulationResult result = Simulate(data, strategy, period, commandLine, log);

        RunReport.WriteRunFiles(outDir, data, result, period.EvalFromMs, period.ToMs, period.SplitMs, Arguments(commandLine));
        PrintRun(result, data, period, log);

        if (commandLine.Has("validate"))
        {
            bool passed = ValidationReport.Write(outDir, data, result, period.EvalFromMs, period.ToMs, Limitations(data, period), period.SplitMs);
            string passedText;
            if (passed)
            {
                passedText = "PASS";
            }
            else
            {
                passedText = "FAIL";
            }
            log($"validation: {passedText}, see {Path.Combine(outDir, "validation.md")}");
        }

        log($"wrote {outDir}");
    }

    public static void Sweep(CommandLine commandLine, Action<string> log)
    {
        List<string> allowedOptions = new List<string> { "budgets", "strategies", "normal-batch", "offline-scale" };
        allowedOptions.AddRange(PriorityOptionNames);
        CheckOptions(commandLine, allowedOptions);

        Period period = Period.From(commandLine);
        SimulatorData data = SimulatorCommands.LoadData(commandLine, log);
        period = period.ClampedTo(data);
        string outDir = commandLine.Required("out");

        List<double> budgets = new List<double>();
        if (commandLine.Has("budgets"))
        {
            foreach (string text in commandLine.Values("budgets"))
            {
                budgets.Add(Parse(text));
            }
        }
        else
        {
            budgets.Add(150);
            budgets.Add(200);
            budgets.Add(350);
            budgets.Add(450);
        }

        List<string> strategies = new List<string>();
        if (commandLine.Has("strategies"))
        {
            foreach (string text in commandLine.Values("strategies"))
            {
                strategies.Add(text);
            }
        }
        else
        {
            strategies.Add("fourlane");
            strategies.Add("uniform");
            strategies.Add("priority");
        }

        List<string> rows = new List<string>();
        SimulationResult baseline = Simulate(data, CreateStrategy(commandLine, "fourlane", null, 1), period, commandLine, log);
        double baselineBudget = Math.Round(StrategyBudget(baseline, period), 1);
        log($"fourlane at production settings: {baselineBudget:0.0} queries per minute");
        RunReport.WriteRunFiles(Path.Combine(outDir, "fourlane-production"), data, baseline, period.EvalFromMs, period.ToMs, period.SplitMs, Arguments(commandLine));
        rows.AddRange(SweepRows("fourlane", "production", 1, baseline, data, period));

        List<double?> targets = new List<double?>();
        targets.Add(null);
        foreach (double b in budgets)
        {
            targets.Add(b);
        }

        foreach (double? target in targets)
        {
            string label;
            if (target.HasValue)
            {
                label = target.Value.ToString("0", CultureInfo.InvariantCulture);
            }
            else
            {
                label = "fourlane-budget";
            }

            double achieved = baselineBudget;
            if (target.HasValue)
            {
                if (strategies.Contains("fourlane"))
                {
                    double scale = target.Value / baselineBudget;
                    SimulationResult fourlane = Simulate(data, CreateStrategy(commandLine, "fourlane", null, scale), period, commandLine, log);
                    achieved = Math.Round(StrategyBudget(fourlane, period), 1);
                    WritePoint(outDir, data, period, commandLine, rows, log, "fourlane", label, scale, fourlane);
                }
                else
                {
                    achieved = target.Value;
                }
            }

            foreach (string name in strategies)
            {
                if (name == "fourlane")
                {
                    continue;
                }

                SimulationResult result = Simulate(data, CreateStrategy(commandLine, name, achieved, 1), period, commandLine, log);
                WritePoint(outDir, data, period, commandLine, rows, log, name, label, 1, result);
            }
        }

        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, "sweep.csv"),
            "strategy,budget_label,batch_scale,window,queries_per_min,unanswered_share," + SummaryCsv.Header + "\n" +
            string.Join("\n", rows) + "\n");
        log($"wrote {Path.Combine(outDir, "sweep.csv")}");
    }

    private static void WritePoint(string outDir, SimulatorData data, Period period, CommandLine commandLine, List<string> rows, Action<string> log,
        string name, string label, double scale, SimulationResult result)
    {
        RunReport.WriteRunFiles(Path.Combine(outDir, $"{name}-{label}"), data, result, period.EvalFromMs, period.ToMs, period.SplitMs, Arguments(commandLine));
        rows.AddRange(SweepRows(name, label, scale, result, data, period));
        double budget = StrategyBudget(result, period);
        FreshnessSummary all = FindAllGroup(RunReport.EvaluateSimulated(data, result, period.EvalFromMs, period.ToMs));
        log($"{name} at {label}: {budget:0.0} queries per minute, " +
            $"overall mean AoI {all.AoiMeanSeconds:0} s");
    }

    public static void Tune(CommandLine commandLine, Action<string> log)
    {
        List<string> allowedOptions = new List<string> { "budget", "min-intervals", "prior-seconds-values", "wipe-boosts", "ewma-alphas", "objective", "max-age", "max-age-share", "flat-backoff" };
        CheckOptions(commandLine, allowedOptions);

        Period period = Period.From(commandLine);
        SimulatorData data = SimulatorCommands.LoadData(commandLine, log);
        period = period.ClampedTo(data);

        long tuningEnd;
        if (period.SplitMs.HasValue)
        {
            tuningEnd = period.SplitMs.Value;
        }
        else
        {
            tuningEnd = period.ToMs;
        }
        Period tuning = new Period(period.FromMs, tuningEnd, period.EvalFromMs, null);

        double budget = Parse(commandLine.Required("budget"));
        string? objectiveText = commandLine.Optional("objective");
        string objective;
        if (objectiveText != null)
        {
            objective = objectiveText;
        }
        else
        {
            objective = "pw-aoi";
        }

        double[] minIntervals = Values(commandLine, "min-intervals", new double[] { 45, 90 });
        double[] priorSecondsValues = Values(commandLine, "prior-seconds-values", new double[] { 600, 1800, 3600 });
        double[] wipeBoosts = Values(commandLine, "wipe-boosts", new double[] { 1 });
        double[] alphas = Values(commandLine, "ewma-alphas", new double[] { 0.2 });

        List<string> lines = new List<string>();
        double bestScore = 0;
        PriorityOptions? bestOptions = null;

        foreach (double minInterval in minIntervals)
        {
            foreach (double priorSeconds in priorSecondsValues)
            {
                foreach (double wipeBoost in wipeBoosts)
                {
                    foreach (double alpha in alphas)
                    {
                        PriorityOptions options = new PriorityOptions
                        {
                            MinIntervalSeconds = minInterval,
                            PriorSeconds = priorSeconds,
                            WipeBoost = wipeBoost,
                            PlayersEwmaAlpha = alpha,
                            MaxAgeSeconds = commandLine.Double("max-age", 0),
                            MaxAgeBudgetShare = MaxAgeShare(commandLine),
                            BackoffScalesWithWeight = !commandLine.Has("flat-backoff")
                        };

                        SimulationResult result = Simulate(data, new PriorityPollingStrategy(budget, options), tuning, commandLine, log);
                        FreshnessSummary all = FindAllGroup(RunReport.EvaluateSimulated(data, result, tuning.EvalFromMs, tuning.ToMs));
                        FreshnessSummary shifted = FindAllGroup(RunReport.EvaluateSimulated(data, result, tuning.EvalFromMs + RunReport.ShiftedGridOffsetMs, tuning.ToMs));
                        double allScore = ObjectiveValue(objective, all);
                        double shiftedScore = ObjectiveValue(objective, shifted);
                        double score = (allScore + shiftedScore) / 2;

                        string line = RunReport.Invariant(
                            $"{options.MinIntervalSeconds},{options.PriorSeconds},{options.WipeBoost},{options.PlayersEwmaAlpha},{options.MaxAgeSeconds},{all.AoiMeanSeconds:0.#},{all.AoiMedianSeconds:0.#},{all.PlayerWeightedAoiSeconds:0.#},{all.PlayerMae:0.###},{shifted.AoiMeanSeconds:0.#},{shifted.AoiMedianSeconds:0.#},{shifted.PlayerWeightedAoiSeconds:0.#},{shifted.PlayerMae:0.###}");
                        lines.Add(line);
                        log(line);

                        if (bestOptions == null || score < bestScore)
                        {
                            bestScore = score;
                            bestOptions = options;
                        }
                    }
                }
            }
        }

        string outDir = commandLine.Required("out");
        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, "tuning.csv"),
            "min_interval_s,prior_seconds,wipe_boost,ewma_alpha,aoi_mean_s,aoi_median_s,player_weighted_aoi_s,player_mae\n" +
            string.Join("\n", lines) + "\n");
        PriorityOptions chosen = bestOptions!;
        log(RunReport.Invariant(
            $"best by {objective}, mean of both grids: --min-interval {chosen.MinIntervalSeconds} --prior-seconds {chosen.PriorSeconds} --wipe-boost {chosen.WipeBoost} --ewma-alpha {chosen.PlayersEwmaAlpha}"));
    }

    private static double ObjectiveValue(string objective, FreshnessSummary row)
    {
        double? value;
        if (objective == "pw-aoi")
        {
            value = row.PlayerWeightedAoiSeconds;
        }
        else if (objective == "mean-aoi")
        {
            value = row.AoiMeanSeconds;
        }
        else if (objective == "mae")
        {
            value = row.PlayerMae;
        }
        else
        {
            throw new ArgumentException("--objective is pw-aoi, mean-aoi or mae.");
        }

        if (value.HasValue)
        {
            return value.Value;
        }
        return double.MaxValue;
    }

    public static void Diagnose(CommandLine commandLine, Action<string> log)
    {
        List<string> allowedOptions = new List<string> { "budget", "window-from", "window-to" };
        allowedOptions.AddRange(PriorityOptionNames);
        CheckOptions(commandLine, allowedOptions);

        Period period = Period.From(commandLine);
        SimulatorData data = SimulatorCommands.LoadData(commandLine, log);
        period = period.ClampedTo(data);
        string outDir = commandLine.Required("out");
        Directory.CreateDirectory(outDir);
        long windowFrom = commandLine.Time("window-from", DateTimeOffset.FromUnixTimeMilliseconds(period.EvalFromMs)).ToUnixTimeMilliseconds();
        long windowTo = commandLine.Time("window-to", DateTimeOffset.FromUnixTimeMilliseconds(period.ToMs)).ToUnixTimeMilliseconds();
        double budget = Parse(commandLine.Required("budget"));

        StringBuilder daily = new StringBuilder("strategy,day,group,points_with_value,aoi_mean_s,aoi_median_s,player_weighted_aoi_s,player_mae\n");
        StringBuilder window = new StringBuilder("strategy," + SummaryCsv.Header + "\n");
        StringBuilder states = new StringBuilder(
            "strategy,stratum,avg_believed_offline,max_believed_offline,avg_in_backoff,max_in_backoff," +
            "avg_believed_offline_but_answering,servers_ever_believed_offline\n");

        string[] names = new string[] { "fourlane", "priority" };
        foreach (string name in names)
        {
            double? strategyBudget = null;
            if (name != "fourlane")
            {
                strategyBudget = budget;
            }

            IPollingStrategy strategy = CreateStrategy(commandLine, name, strategyBudget, 1);
            PriorityPollingStrategy? priority = strategy as PriorityPollingStrategy;
            Dictionary<Domain.Study.StudyStratum, StateStats> stats = new Dictionary<Domain.Study.StudyStratum, StateStats>();
            foreach (Domain.Study.StudyStratum stratum in Enum.GetValues<Domain.Study.StudyStratum>())
            {
                stats.Add(stratum, new StateStats());
            }

            void OnTick(long nowMs, IReadOnlyList<PolledServer> servers)
            {
                if (nowMs < windowFrom || nowMs >= windowTo)
                {
                    return;
                }

                foreach (StateStats group in stats.Values)
                {
                    group.BeginTick();
                }

                foreach (SampleServer sample in data.SampleServers)
                {
                    PolledServer server = servers[sample.ServerIndex];
                    bool offline = server.Status != Domain.Servers.ServerStatus.Online;
                    int unansweredInRow = 0;
                    if (priority != null)
                    {
                        unansweredInRow = priority.UnansweredInRow(sample.ServerIndex);
                    }
                    bool backoff = unansweredInRow > 0;
                    bool answering = FreshnessEvaluator.TruthAt(data.Probes[sample.ServerIdx - 1], nowMs) != null;
                    stats[sample.Stratum].Add(sample.ServerIdx, offline, backoff, offline && answering);
                }

                foreach (StateStats group in stats.Values)
                {
                    group.EndTick();
                }
            }

            SimulationResult result = Simulate(data, strategy, period, commandLine, log, OnTick);

            for (long day = period.EvalFromMs; day < period.ToMs; day += 86_400_000L)
            {
                string label = DateTimeOffset.FromUnixTimeMilliseconds(day).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                long dayEnd = Math.Min(day + 86_400_000L, period.ToMs);
                foreach (FreshnessSummary row in RunReport.EvaluateSimulated(data, result, day, dayEnd))
                {
                    daily.AppendLine(RunReport.Invariant(
                        $"{name},{label},{row.Group},{row.PointsWithValue},{row.AoiMeanSeconds:0.#},{row.AoiMedianSeconds:0.#},{row.PlayerWeightedAoiSeconds:0.#},{row.PlayerMae:0.###}"));
                }
            }

            string windowCsv = SummaryCsv.Format(RunReport.EvaluateSimulated(data, result, windowFrom, windowTo));
            string[] windowLines = windowCsv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 1; i < windowLines.Length; i++)
            {
                window.AppendLine(name + "," + windowLines[i]);
            }

            foreach (KeyValuePair<Domain.Study.StudyStratum, StateStats> pair in stats)
            {
                Domain.Study.StudyStratum stratum = pair.Key;
                StateStats s = pair.Value;
                states.AppendLine(RunReport.Invariant(
                    $"{name},{stratum},{s.AverageOffline:0.##},{s.MaxOffline},{s.AverageBackoff:0.##},{s.MaxBackoff},{s.AverageOfflineButAnswering:0.##},{s.EverOffline}"));
            }

            RunReport.WriteRunFiles(Path.Combine(outDir, name), data, result, period.EvalFromMs, period.ToMs, period.SplitMs, Arguments(commandLine));
        }

        File.WriteAllText(Path.Combine(outDir, "daily.csv"), daily.ToString());
        File.WriteAllText(Path.Combine(outDir, "window.csv"), window.ToString());
        File.WriteAllText(Path.Combine(outDir, "window_states.csv"), states.ToString());
        log($"wrote {outDir}");
    }

    public static void Trace(CommandLine commandLine, Action<string> log)
    {
        List<string> allowedOptions = new List<string> { "strategy", "budget", "servers" };
        allowedOptions.AddRange(PriorityOptionNames);
        CheckOptions(commandLine, allowedOptions);

        Period period = Period.From(commandLine);
        SimulatorData data = SimulatorCommands.LoadData(commandLine, log);
        period = period.ClampedTo(data);
        string outDir = commandLine.Required("out");
        Directory.CreateDirectory(outDir);

        HashSet<int> wanted = new HashSet<int>();
        foreach (string text in commandLine.Values("servers"))
        {
            wanted.Add(int.Parse(text));
        }

        List<SampleServer> tracked = new List<SampleServer>();
        foreach (SampleServer sample in data.SampleServers)
        {
            if (wanted.Contains(sample.ServerIdx))
            {
                tracked.Add(sample);
            }
        }

        Dictionary<int, TracedState> last = new Dictionary<int, TracedState>();
        foreach (SampleServer sample in tracked)
        {
            last.Add(sample.ServerIdx, new TracedState());
        }

        StringBuilder trace = new StringBuilder("server_idx,tick_utc,last_checked_utc,status,players,rank,last_seen_utc\n");
        StringBuilder population = new StringBuilder("hour_utc,existing,online,offline,very_stale_offline\n");
        long nextHourMs = period.FromMs;

        void OnTick(long nowMs, IReadOnlyList<PolledServer> servers)
        {
            if (nowMs >= nextHourMs)
            {
                nextHourMs += 3_600_000L;
                int existing = 0;
                int online = 0;
                int veryStale = 0;
                foreach (PolledServer server in servers)
                {
                    if (!server.Exists)
                    {
                        continue;
                    }

                    existing++;
                    if (server.Status == Domain.Servers.ServerStatus.Online)
                    {
                        online++;
                    }
                    else if (!server.LastSeenAtMs.HasValue || server.LastSeenAtMs.Value < nowMs - 86_400_000L)
                    {
                        veryStale++;
                    }
                }

                population.AppendLine(RunReport.Invariant($"{FormatUtc(nowMs)},{existing},{online},{existing - online},{veryStale}"));
            }

            foreach (SampleServer sample in tracked)
            {
                PolledServer server = servers[sample.ServerIndex];
                TracedState previous = last[sample.ServerIdx];
                if (previous.Checked == server.LastCheckedAtMs && previous.Status == server.Status && previous.Rank == server.Rank)
                {
                    continue;
                }

                previous.Checked = server.LastCheckedAtMs;
                previous.Status = server.Status;
                previous.Rank = server.Rank;
                trace.AppendLine(RunReport.Invariant(
                    $"{sample.ServerIdx},{FormatUtc(nowMs)},{FormatUtc(server.LastCheckedAtMs)},{server.Status},{server.Players},{server.Rank},{FormatUtc(server.LastSeenAtMs)}"));
            }
        }

        string strategyName = commandLine.Required("strategy");
        double? budget = null;
        string? budgetText = commandLine.Optional("budget");
        if (budgetText != null)
        {
            budget = Parse(budgetText);
        }

        IPollingStrategy strategy = CreateStrategy(commandLine, strategyName, budget, 1);
        Simulate(data, strategy, period, commandLine, log, OnTick);
        File.WriteAllText(Path.Combine(outDir, "trace.csv"), trace.ToString());
        File.WriteAllText(Path.Combine(outDir, "population.csv"), population.ToString());
        log($"wrote {outDir}");
    }

    private static string FormatUtc(long? ms)
    {
        if (ms.HasValue)
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(ms.Value).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }
        return "";
    }

    private sealed class TracedState
    {
        public long? Checked = null;
        public Domain.Servers.ServerStatus Status = Domain.Servers.ServerStatus.Unknown;
        public int? Rank = null;
    }

    private sealed class StateStats
    {
        private readonly HashSet<int> _everOffline = new HashSet<int>();
        private long _ticks;
        private long _offline;
        private long _backoff;
        private long _offlineAnswering;
        private int _tickOffline;
        private int _tickBackoff;

        public int MaxOffline { get; private set; }
        public int MaxBackoff { get; private set; }

        public int EverOffline
        {
            get { return _everOffline.Count; }
        }

        public double AverageOffline
        {
            get
            {
                if (_ticks == 0)
                {
                    return 0;
                }
                return (double)_offline / _ticks;
            }
        }

        public double AverageBackoff
        {
            get
            {
                if (_ticks == 0)
                {
                    return 0;
                }
                return (double)_backoff / _ticks;
            }
        }

        public double AverageOfflineButAnswering
        {
            get
            {
                if (_ticks == 0)
                {
                    return 0;
                }
                return (double)_offlineAnswering / _ticks;
            }
        }

        public void BeginTick()
        {
            _tickOffline = 0;
            _tickBackoff = 0;
        }

        public void Add(int serverIdx, bool offline, bool backoff, bool offlineButAnswering)
        {
            if (offline)
            {
                _offline++;
                _tickOffline++;
                _everOffline.Add(serverIdx);
            }

            if (backoff)
            {
                _backoff++;
                _tickBackoff++;
            }

            if (offlineButAnswering)
            {
                _offlineAnswering++;
            }
        }

        public void EndTick()
        {
            _ticks++;
            MaxOffline = Math.Max(MaxOffline, _tickOffline);
            MaxBackoff = Math.Max(MaxBackoff, _tickBackoff);
        }
    }

    public static SimulationResult Simulate(SimulatorData data, IPollingStrategy strategy, Period period, CommandLine commandLine, Action<string> log,
        Action<long, IReadOnlyList<PolledServer>>? onTick = null)
    {
        SimulationOptions options = new SimulationOptions
        {
            FromMs = period.FromMs,
            ToMs = period.ToMs,
            Seed = commandLine.Int("seed", 1),
            OnTick = onTick,
            Ranking = ParseRanking(commandLine),
            Timing = CreateTiming(commandLine)
        };

        PriorityPollingStrategy? priority = strategy as PriorityPollingStrategy;
        if (priority != null)
        {
            priority.StatisticsFromMs = period.EvalFromMs;
        }

        DateTime started = DateTime.UtcNow;
        SimulationResult result = new SimulationEngine(data, strategy, options).Run();
        log($"simulated {strategy.Name} in {(DateTime.UtcNow - started).TotalSeconds:0} s");
        if (priority != null)
        {
            log($"priority: average maximum age {priority.AverageMaxAgeSeconds / 60:0.00} min, " +
                $"average servers believed online {priority.AverageOnlineServers:0}");
        }
        return result;
    }

    private static RankingMode ParseRanking(CommandLine commandLine)
    {
        string? ranking = commandLine.Optional("ranking");
        if (ranking == null)
        {
            ranking = "frozen";
        }

        if (ranking == "frozen")
        {
            return RankingMode.Frozen;
        }
        if (ranking == "hourly")
        {
            return RankingMode.Hourly;
        }
        throw new ArgumentException("--ranking is frozen or hourly.");
    }

    private static WorkerTiming? CreateTiming(CommandLine commandLine)
    {
        if (commandLine.Has("no-timing"))
        {
            return null;
        }

        return new WorkerTiming
        {
            Concurrency = commandLine.Int("concurrency", 32),
            AnsweredPollSeconds = commandLine.Double("answered-poll-seconds", 0.5),
            UnansweredPollSeconds = commandLine.Double("unanswered-poll-seconds", 6),
            JitterMs = commandLine.Int("jitter-ms", 50)
        };
    }

    public static IPollingStrategy CreateStrategy(CommandLine commandLine, string name, double? budget, double batchScale)
    {
        if (name == "fourlane")
        {
            return new FourLanePollingStrategy(new FourLaneOptions { BatchScale = batchScale });
        }

        if (name == "fourlane-shift")
        {
            // The current lanes with the two offline lanes scaled by offline-scale (0 turns them off);
            // the freed queries go to the normal lane, whose batch is set with normal-batch.
            double offlineScale = commandLine.Double("offline-scale", 0);
            FourLaneOptions defaults = new FourLaneOptions();
            return new FourLanePollingStrategy(new FourLaneOptions
            {
                NormalOnlineLaneBatchSize = commandLine.Int("normal-batch", defaults.NormalOnlineLaneBatchSize),
                SlowOfflineLaneBatchSize = (int)Math.Round(defaults.SlowOfflineLaneBatchSize * offlineScale, MidpointRounding.AwayFromZero),
                VeryStaleOfflineLaneBatchSize = (int)Math.Round(defaults.VeryStaleOfflineLaneBatchSize * offlineScale, MidpointRounding.AwayFromZero),
                BatchScale = batchScale
            });
        }

        if (name == "uniform")
        {
            if (!budget.HasValue)
            {
                throw new ArgumentException("uniform needs --budget.");
            }
            return new UniformPollingStrategy(budget.Value);
        }

        if (name == "priority")
        {
            if (!budget.HasValue)
            {
                throw new ArgumentException("priority needs --budget.");
            }

            PriorityOptions options = new PriorityOptions
            {
                MinIntervalSeconds = commandLine.Double("min-interval", 165),
                MaxBackoffSeconds = commandLine.Double("max-backoff", 6 * 3600),
                MaxAgeSeconds = commandLine.Double("max-age", 0),
                MaxAgeBudgetShare = MaxAgeShare(commandLine),
                BackoffScalesWithWeight = !commandLine.Has("flat-backoff"),
                UsePredictedWipes = !commandLine.Has("no-predicted-wipes"),
                WipeBoost = commandLine.Double("wipe-boost", 1),
                PlayersEwmaAlpha = commandLine.Double("ewma-alpha", 0.2),
                PriorChanges = commandLine.Double("prior-changes", 1),
                PriorSeconds = commandLine.Double("prior-seconds", 3600),
                Score = ParseScore(commandLine)
            };
            return new PriorityPollingStrategy(budget.Value, options);
        }

        throw new ArgumentException($"Unknown strategy '{name}': fourlane, fourlane-shift, uniform or priority.");
    }

    private static double MaxAgeShare(CommandLine commandLine)
    {
        if (commandLine.Has("max-age"))
        {
            return 0;
        }
        return commandLine.Double("max-age-share", PriorityOptions.DefaultMaxAgeBudgetShare);
    }

    private static PriorityScore ParseScore(CommandLine commandLine)
    {
        string? score = commandLine.Optional("score");
        if (score == null)
        {
            score = "expected-age";
        }

        if (score == "expected-age")
        {
            return PriorityScore.ExpectedAge;
        }
        if (score == "change-probability")
        {
            return PriorityScore.ChangeProbability;
        }
        throw new ArgumentException("--score is expected-age or change-probability.");
    }

    public static double StrategyBudget(SimulationResult result, Period period)
    {
        LaneRate total = FindStrategyTotal(RunReport.LaneRates(result, period.EvalFromMs, period.ToMs));
        return total.PollsPerMinute;
    }

    public static IReadOnlyList<string> Limitations(SimulatorData data, Period period)
    {
        List<string> notes = new List<string>();

        long firstSnapshot = long.MaxValue;
        foreach (SnapshotSeries series in data.Snapshots)
        {
            if (series.Count > 0 && series.TimesMs[0] < firstSnapshot)
            {
                firstSnapshot = series.TimesMs[0];
            }
        }

        string firstNote = $"The starting state of every server (players, status, last seen, last checked) comes from its last " +
                           $"snapshot before {RunReport.Iso(period.FromMs)}; only server_type and created_at come from the servers file. " +
                           $"The export's snapshots start at {RunReport.Iso(firstSnapshot)}";
        if (firstSnapshot >= period.FromMs - 60_000)
        {
            firstNote = firstNote + ", so every server starts as never polled and the first hours are a warm-up; " +
                        $"the comparison starts at {RunReport.Iso(period.EvalFromMs)}. The 7-day ranking window also has no history before the period.";
        }
        else
        {
            firstNote = firstNote + ".";
        }
        notes.Add(firstNote);

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
        notes.Add("Why Priority's player-weighted AoI is worse in the tuning window 2026-11-04..11 (699 against 688 s, final run on both " +
                  "exports): not the forced wipe window itself. From 2026-11-05 18:00 to 11-06 06:00 Priority is better (212 against 355 s; " +
                  "Popular 410 against 670 s mean AoI) and holds as many sample servers offline as fourlane (about 22 per stratum). The cost " +
                  "lands on 11-07 to 11-10, when servers that went down at the forced wipe came back after 41 to 98 hours (sample servers " +
                  "172, 188 Popular; 37, 93 Medium; 313 Small, 77 h from 11-06). Unseen for over 24 h they are under the 6 h back-off cap, " +
                  "so Priority can notice the return up to 6 h late, and the player weighting magnifies it (daily player-weighted AoI " +
                  "fourlane/Priority: 11-07 382/836, 11-08 988/1137, 11-10 645/705 s). Fourlane keeps polling ranked servers in its Fast " +
                  "lane while they are down, because the ranking is frozen, and sees them return within minutes. On every other day " +
                  "Priority is better. Mechanism: the 6 h back-off cap for servers unseen over 24 h (as agreed), not the change-rate estimate.");
        notes.Add("Popular median AoI (Priority 188 s against fourlane's 134 s) does not come from the 165 s minimum interval: with " +
                  "--min-interval 60 at the production volume it is 203 s (mean 658 s, Medium mean 1311 s against 1248 s, player-weighted AoI " +
                  "466 against 474 s). Fourlane polls a Popular sample server 13.0 times an hour, Priority 9.3 (10.1 with 60 s), because " +
                  "57.5 % of Priority's queries go to the maximum-age queue. The median gap is the price of that guarantee; Priority's " +
                  "Popular mean AoI is still lower (621 against 1092 s).");
        notes.Add("Phantom servers fill the gap between study_population_samples.TotalServers and the known servers. Of them, " +
                  "OnlineServers minus the known servers the oracle answers for (counted hourly) answer, with no players and 100 max " +
                  "players, so the online/offline split follows production; the rest never answer. Answering phantoms present at the start " +
                  "begin online, last polled at a random moment of the previous 75 min; later ones join never polled.");
        notes.Add("Worker timing is modelled, not replayed: 32 queries in flight, 0.5 s per answered and 6 s per unanswered query " +
                  "(two 3 s attempts), lanes one after another, PeriodicTimer's one kept tick, and a few ms of jitter in reading the clock. " +
                  "Without it the Fast lane always runs every 45 s (133 polls/min against production's 105).");
        return notes;
    }

    private static List<string> SweepRows(string name, string label, double scale, SimulationResult result, SimulatorData data, Period period)
    {
        List<string> rows = new List<string>();
        AddSweepRows(rows, name, label, scale, result, data, "full", period.EvalFromMs, period.ToMs);
        if (period.SplitMs.HasValue)
        {
            long split = period.SplitMs.Value;
            if (split > period.EvalFromMs && split < period.ToMs)
            {
                AddSweepRows(rows, name, label, scale, result, data, "before_split", period.EvalFromMs, split);
                AddSweepRows(rows, name, label, scale, result, data, "after_split", split, period.ToMs);
            }
        }
        return rows;
    }

    private static void AddSweepRows(List<string> rows, string name, string label, double scale, SimulationResult result, SimulatorData data,
        string window, long from, long to)
    {
        LaneRate total = FindStrategyTotal(RunReport.LaneRates(result, from, to));
        string summaryCsv = SummaryCsv.Format(RunReport.EvaluateSimulated(data, result, from, to));
        string[] summaryLines = summaryCsv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 1; i < summaryLines.Length; i++)
        {
            rows.Add(RunReport.Invariant($"{name},{label},{scale:0.####},{window},{total.PollsPerMinute:0.##},{total.UnansweredShare:0.####},") + summaryLines[i]);
        }
    }

    private static LaneRate FindStrategyTotal(IReadOnlyList<LaneRate> rates)
    {
        LaneRate? found = null;
        int matches = 0;
        foreach (LaneRate rate in rates)
        {
            if (rate.Lane == "StrategyTotal")
            {
                found = rate;
                matches++;
            }
        }

        if (matches == 0)
        {
            throw new InvalidOperationException("Sequence contains no matching element");
        }
        if (matches > 1)
        {
            throw new InvalidOperationException("Sequence contains more than one matching element");
        }
        return found!;
    }

    private static FreshnessSummary FindAllGroup(IReadOnlyList<FreshnessSummary> rows)
    {
        FreshnessSummary? found = null;
        int matches = 0;
        foreach (FreshnessSummary row in rows)
        {
            if (row.Group == FreshnessEvaluator.AllGroup)
            {
                found = row;
                matches++;
            }
        }

        if (matches == 0)
        {
            throw new InvalidOperationException("Sequence contains no matching element");
        }
        if (matches > 1)
        {
            throw new InvalidOperationException("Sequence contains more than one matching element");
        }
        return found!;
    }

    private static void PrintRun(SimulationResult result, SimulatorData data, Period period, Action<string> log)
    {
        foreach (LaneRate rate in RunReport.LaneRates(result, period.EvalFromMs, period.ToMs))
        {
            log(RunReport.Invariant($"  {rate.Lane,-18} {rate.PollsPerMinute,7:0.0} per min, {rate.UnansweredShare,6:P1} unanswered"));
        }

        Console.WriteLine(SummaryCsv.FormatTable(RunReport.EvaluateSimulated(data, result, period.EvalFromMs, period.ToMs)));
    }

    private static Dictionary<string, object?> Arguments(CommandLine commandLine)
    {
        Dictionary<string, object?> arguments = new Dictionary<string, object?>();
        foreach (KeyValuePair<string, List<string>> pair in commandLine.Options)
        {
            arguments.Add(pair.Key, pair.Value.ToArray());
        }
        return arguments;
    }

    private static void CheckOptions(CommandLine commandLine, IEnumerable<string> extra)
    {
        HashSet<string> allowed = new HashSet<string>(StringComparer.Ordinal);
        foreach (string name in SharedOptions)
        {
            allowed.Add(name);
        }
        foreach (string name in extra)
        {
            allowed.Add(name);
        }

        foreach (string name in commandLine.Options.Keys)
        {
            if (!allowed.Contains(name))
            {
                throw new ArgumentException($"Unknown option --{name} for {commandLine.Command}.");
            }
        }
    }

    private static double[] Values(CommandLine commandLine, string name, double[] fallback)
    {
        if (!commandLine.Has(name))
        {
            return fallback;
        }

        IReadOnlyList<string> texts = commandLine.Values(name);
        double[] values = new double[texts.Count];
        for (int i = 0; i < texts.Count; i++)
        {
            values[i] = Parse(texts[i]);
        }
        return values;
    }

    private static double Parse(string text)
    {
        return double.Parse(text, CultureInfo.InvariantCulture);
    }
}

public sealed record Period(long FromMs, long ToMs, long EvalFromMs, long? SplitMs)
{
    public static Period From(CommandLine commandLine)
    {
        long from = commandLine.Time("from", SimulationCommands.DefaultFrom).ToUnixTimeMilliseconds();
        long to = commandLine.Time("to", SimulationCommands.DefaultTo).ToUnixTimeMilliseconds();
        long evalFrom = commandLine.Time("eval-from", DateTimeOffset.FromUnixTimeMilliseconds(from)).ToUnixTimeMilliseconds();
        long split = commandLine.Time("split", SimulationCommands.DefaultSplit).ToUnixTimeMilliseconds();
        if (to <= from || evalFrom < from || evalFrom >= to)
        {
            throw new ArgumentException("Need --from <= --eval-from < --to.");
        }

        return new Period(from, to, evalFrom, split);
    }

    public Period ClampedTo(SimulatorData data)
    {
        long lastProbe = ToMs;
        bool foundProbe = false;
        foreach (ProbeSeries probes in data.Probes)
        {
            if (probes.Count == 0)
            {
                continue;
            }

            long probeEnd = probes.TimesMs[probes.TimesMs.Length - 1];
            if (!foundProbe || probeEnd > lastProbe)
            {
                lastProbe = probeEnd;
                foundProbe = true;
            }
        }

        long end = Math.Min(ToMs, (lastProbe / 60_000 + 1) * 60_000);
        if (end <= EvalFromMs)
        {
            throw new ArgumentException("The data ends before the compared window starts.");
        }

        return new Period(FromMs, end, EvalFromMs, SplitMs);
    }
}
