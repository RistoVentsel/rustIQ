using RustIQ.Domain.Servers;

namespace RustIQ.Application.Servers.Polling;

public enum PriorityScore
{
    ExpectedAge,
    ChangeProbability
}

public sealed class PriorityOptions
{
    public PriorityScore Score { get; init; } = PriorityScore.ExpectedAge;

    public double MinIntervalSeconds { get; init; } = 165;

    public double MaxBackoffSeconds { get; init; } = 6 * 3600;

    public bool BackoffScalesWithWeight { get; init; } = true;

    public double RecentlySeenSeconds { get; init; } = 24 * 3600;

    public double MaxAgeBudgetShare { get; init; } = DefaultMaxAgeBudgetShare;

    public const double DefaultMaxAgeBudgetShare = 0.619;

    public double MaxAgeSeconds { get; init; } = 0;

    public bool UsePredictedWipes { get; init; } = true;

    public double WipeBoost { get; init; } = 1;

    public double WipeWindowSeconds { get; init; } = 3600;

    public double PlayersEwmaAlpha { get; init; } = 0.2;

    public double PriorChanges { get; init; } = 1;

    public double PriorSeconds { get; init; } = 3600;

    public double MaxObservedGapSeconds { get; init; } = 3600;
}

public sealed class PriorityPollingStrategy : IPollingStrategy
{
    public const string Lane = "Priority";

    public const string MaxAgeLane = "PriorityMaxAge";

    public const double OverdueScore = 1e15;

    private readonly PriorityOptions _options;
    private readonly PollBudget _budget;
    private readonly double _queriesPerSecond;
    private double _maxAgeSeconds;
    private double _maxAgeSum;
    private double _onlineSum;
    private long _ticks;
    private readonly TopK<(double Score, int Index)> _topK = new TopK<(double Score, int Index)>(HighestScoreFirst.Instance);
    private Estimate[] _estimates = new Estimate[0];

    public PriorityPollingStrategy(double queriesPerMinute, PriorityOptions? options = null, double tickSeconds = 15)
    {
        if (options == null)
        {
            _options = new PriorityOptions();
        }
        else
        {
            _options = options;
        }

        _budget = new PollBudget(queriesPerMinute, tickSeconds);
        _queriesPerSecond = queriesPerMinute / 60.0;

        if (_options.MaxAgeBudgetShare > 0)
        {
            _maxAgeSeconds = 0;
        }
        else
        {
            _maxAgeSeconds = _options.MaxAgeSeconds;
        }
    }

    public long StatisticsFromMs { get; set; } = long.MinValue;

    public double AverageMaxAgeSeconds
    {
        get
        {
            if (_ticks == 0)
            {
                return _maxAgeSeconds;
            }

            return _maxAgeSum / _ticks;
        }
    }

    public double AverageOnlineServers
    {
        get
        {
            if (_ticks == 0)
            {
                return 0;
            }

            return _onlineSum / _ticks;
        }
    }

    public string Name
    {
        get { return "priority"; }
    }

    public void Tick(long nowMs, IReadOnlyList<PolledServer> servers, IPollExecutor poll)
    {
        EnsureCapacity(servers.Count);
        _topK.Reset(_budget.Next(nowMs));
        UpdateMaxAge(servers, nowMs);

        foreach (PolledServer server in servers)
        {
            if (!server.Exists)
            {
                continue;
            }

            double score = Score(server, nowMs);
            if (score > 0)
            {
                _topK.Offer((score, server.Index));
            }
        }

        List<(double Score, int Index)> selected = _topK.ToSortedList();
        List<PolledServer> overdue = new List<PolledServer>();
        List<PolledServer> scored = new List<PolledServer>();
        for (int i = 0; i < selected.Count; i++)
        {
            (double Score, int Index) item = selected[i];
            if (item.Score >= OverdueScore)
            {
                overdue.Add(servers[item.Index]);
            }
            else
            {
                scored.Add(servers[item.Index]);
            }
        }

        if (overdue.Count > 0)
        {
            poll.Poll(overdue, MaxAgeLane);
        }

        if (scored.Count > 0)
        {
            poll.Poll(scored, Lane);
        }
    }

    public void UpdateMaxAge(IReadOnlyList<PolledServer> servers, long nowMs)
    {
        int online = 0;
        foreach (PolledServer server in servers)
        {
            if (server.Exists && server.Status == ServerStatus.Online)
            {
                online++;
            }
        }

        if (_options.MaxAgeBudgetShare > 0 && _queriesPerSecond > 0)
        {
            _maxAgeSeconds = online / (_options.MaxAgeBudgetShare * _queriesPerSecond);
        }

        if (nowMs >= StatisticsFromMs)
        {
            _maxAgeSum += _maxAgeSeconds;
            _onlineSum += online;
            _ticks++;
        }
    }

    public double Score(PolledServer server, long nowMs)
    {
        EnsureCapacity(server.Index + 1);
        ref Estimate estimate = ref _estimates[server.Index];
        double ageSeconds = (nowMs - server.DueKeyMs) / 1000.0;
        double weight = 1 + estimate.PlayersEwma;

        bool recentlySeen = false;
        long? lastSeenAtMs = server.LastSeenAtMs;
        if (lastSeenAtMs.HasValue && nowMs - lastSeenAtMs.Value <= _options.RecentlySeenSeconds * 1000)
        {
            recentlySeen = true;
        }

        bool guaranteed = recentlySeen && _maxAgeSeconds > 0;

        double backoffCap = _options.MaxBackoffSeconds;
        if (recentlySeen)
        {
            if (_options.BackoffScalesWithWeight)
            {
                backoffCap = Math.Max(_options.MinIntervalSeconds, backoffCap / weight);
            }

            if (guaranteed)
            {
                backoffCap = Math.Min(backoffCap, _maxAgeSeconds);
            }
        }

        double minInterval = Math.Min(
            backoffCap,
            _options.MinIntervalSeconds * Math.Pow(2, estimate.UnansweredInRow));
        if (server.LastCheckedAtMs.HasValue && ageSeconds < minInterval)
        {
            return 0;
        }

        double lambda = (estimate.Changes + _options.PriorChanges) / (estimate.ObservedSeconds + _options.PriorSeconds);
        double age = Math.Max(0, ageSeconds);

        double staleness;
        if (_options.Score == PriorityScore.ExpectedAge)
        {
            staleness = ExpectedAge(lambda, age);
        }
        else
        {
            staleness = -double.ExpM1(-lambda * age);
        }

        double score = weight * staleness;

        if (guaranteed && age >= _maxAgeSeconds)
        {
            return OverdueScore + age;
        }

        bool nearWipe = false;
        if (_options.UsePredictedWipes)
        {
            long? nextWipeAtMs = server.NextWipeAtMs;
            if (nextWipeAtMs.HasValue && Math.Abs(nowMs - nextWipeAtMs.Value) <= _options.WipeWindowSeconds * 1000)
            {
                nearWipe = true;
            }
        }

        if (nearWipe)
        {
            return score * _options.WipeBoost;
        }

        return score;
    }

    public static double ExpectedAge(double lambda, double age)
    {
        double x = lambda * age;
        if (x < 1e-4)
        {
            return age * (x / 2 - x * x / 6);
        }

        return age - (-double.ExpM1(-x)) / lambda;
    }

    public void OnPolled(PolledServer server, PollOutcome outcome)
    {
        EnsureCapacity(server.Index + 1);
        ref Estimate estimate = ref _estimates[server.Index];
        if (!outcome.Answered)
        {
            estimate.UnansweredInRow++;
            return;
        }

        estimate.UnansweredInRow = 0;
        if (estimate.LastAnswerAtMs.HasValue)
        {
            long lastAt = estimate.LastAnswerAtMs.Value;
            estimate.ObservedSeconds += Math.Min(_options.MaxObservedGapSeconds, (outcome.AtMs - lastAt) / 1000.0);
            if (outcome.Players != estimate.LastPlayers)
            {
                estimate.Changes++;
            }

            estimate.PlayersEwma += _options.PlayersEwmaAlpha * (outcome.Players - estimate.PlayersEwma);
        }
        else
        {
            estimate.PlayersEwma = outcome.Players;
        }

        estimate.LastAnswerAtMs = outcome.AtMs;
        estimate.LastPlayers = outcome.Players;
    }

    public int UnansweredInRow(int index)
    {
        if (index < _estimates.Length)
        {
            return _estimates[index].UnansweredInRow;
        }

        return 0;
    }

    public void OnServerReset(PolledServer server)
    {
        EnsureCapacity(server.Index + 1);
        _estimates[server.Index] = new Estimate();
    }

    private void EnsureCapacity(int count)
    {
        if (_estimates.Length < count)
        {
            Array.Resize(ref _estimates, Math.Max(count, _estimates.Length * 2));
        }
    }

    private struct Estimate
    {
        public double PlayersEwma;
        public double ObservedSeconds;
        public int Changes;
        public int UnansweredInRow;
        public long? LastAnswerAtMs;
        public int LastPlayers;
    }

    private sealed class HighestScoreFirst : IComparer<(double Score, int Index)>
    {
        public static readonly HighestScoreFirst Instance = new HighestScoreFirst();

        public int Compare((double Score, int Index) x, (double Score, int Index) y)
        {
            int byScore = y.Score.CompareTo(x.Score);
            if (byScore != 0)
            {
                return byScore;
            }

            return x.Index.CompareTo(y.Index);
        }
    }
}
