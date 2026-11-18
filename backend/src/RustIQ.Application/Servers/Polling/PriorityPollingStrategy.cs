using RustIQ.Domain.Servers;

namespace RustIQ.Application.Servers.Polling;

public enum PriorityScore
{
    /// <summary>
    /// <c>w * E[age]</c>, the expected time the shown value has been out of date under a Poisson change
    /// rate: <c>age - (1 - exp(-lambda * age)) / lambda</c> (Cho and Garcia-Molina's "age"). Bounded in
    /// lambda (at most <c>age</c>), but it keeps growing with age, so no server starves.
    /// </summary>
    ExpectedAge,

    /// <summary>
    /// The plan's first proposal, <c>w * (1 - exp(-lambda * age))</c>: the probability that the value has
    /// changed. Bounded by <c>w</c>, so with a short minimum interval the busiest servers take the whole
    /// budget and quiet servers are never polled.
    /// </summary>
    ChangeProbability
}

public sealed class PriorityOptions
{
    public PriorityScore Score { get; init; } = PriorityScore.ExpectedAge;

    /// <summary>
    /// A server is not polled again sooner than this. 165 s was tuned on 2026-11-04..11 at 272 queries
    /// per minute; 180 s scored slightly better on the 15 min evaluation grid but not on a shifted one.
    /// </summary>
    public double MinIntervalSeconds { get; init; } = 165;

    /// <summary>
    /// Cap of the back-off <c>MinInterval * 2^k</c> after k unanswered polls in a row, for servers not seen
    /// answering within <see cref="RecentlySeenSeconds"/>. Recently seen servers are capped at the maximum age.
    /// </summary>
    public double MaxBackoffSeconds { get; init; } = 6 * 3600;

    /// <summary>
    /// For recently seen servers, also divide the back-off cap by the server's weight <c>w</c> (never below
    /// the minimum interval). With a flat 6 h cap a 300-player server that came back after an outage could
    /// wait hours for its next poll, and how long depended on the back-off's phase, which made the result
    /// swing with tiny budget changes.
    /// </summary>
    public bool BackoffScalesWithWeight { get; init; } = true;

    /// <summary>
    /// A server that answered within this window is never left unpolled longer than the maximum age, even
    /// while this strategy thinks it is offline.
    /// </summary>
    public double RecentlySeenSeconds { get; init; } = 24 * 3600;

    /// <summary>
    /// The maximum age tied to the budget: <c>online servers / (share * queries per second)</c>, recomputed
    /// every tick from the servers this strategy believes online. 0 uses <see cref="MaxAgeSeconds"/> instead.
    /// </summary>
    public double MaxAgeBudgetShare { get; init; } = DefaultMaxAgeBudgetShare;

    /// <summary>
    /// Calibrated so the maximum age averages 60 min at 272.4 queries per minute on export 1, 2026-11-05 to
    /// 2026-11-15 (60.02 min with about 10,120 servers believed online).
    /// </summary>
    public const double DefaultMaxAgeBudgetShare = 0.619;

    /// <summary>A fixed maximum age, used when <see cref="MaxAgeBudgetShare"/> is 0. 0 turns the guarantee off.</summary>
    public double MaxAgeSeconds { get; init; } = 0;

    /// <summary>Whether RustIQ's stored next wipe (<see cref="PolledServer.NextWipeAtMs"/>) triggers the wipe boost.</summary>
    public bool UsePredictedWipes { get; init; } = true;

    /// <summary>
    /// Score multiplier within <see cref="WipeWindowSeconds"/> of RustIQ's predicted next wipe. 1 (the
    /// default) turns it off. A boost on the calendar's forced wipe was dropped: it hit every server at
    /// once, so it never changed the order.
    /// </summary>
    public double WipeBoost { get; init; } = 1;

    public double WipeWindowSeconds { get; init; } = 3600;

    /// <summary>Weight of the newest answer in the player count EWMA.</summary>
    public double PlayersEwmaAlpha { get; init; } = 0.2;

    /// <summary>
    /// Prior for the change rate: <c>lambda = (changes + PriorChanges) / (observed + PriorSeconds)</c>,
    /// i.e. one change per <c>PriorSeconds / PriorChanges</c> before anything is observed.
    /// </summary>
    public double PriorChanges { get; init; } = 1;

    public double PriorSeconds { get; init; } = 3600;

    /// <summary>A gap between two answers longer than this counts only this long as observed time.</summary>
    public double MaxObservedGapSeconds { get; init; } = 3600;
}

/// <summary>
/// Polls the servers whose stale data costs most: <c>score = w * f(lambda, age)</c>, with
/// <c>w = 1 + EWMA(players)</c>, <c>lambda</c> the change rate this strategy has observed itself and
/// <c>f</c> from <see cref="PriorityScore"/>. <c>f</c> stays bounded in lambda (Cho and Garcia-Molina show
/// that pure proportional-to-change refreshing is worse than uniform). Unanswered servers back off
/// exponentially, recently seen servers are never older than a maximum age tied to the budget, and a
/// predicted wipe can boost the score.
/// </summary>
public sealed class PriorityPollingStrategy : IPollingStrategy
{
    public const string Lane = "Priority";

    /// <summary>Label of the polls the maximum age forced.</summary>
    public const string MaxAgeLane = "PriorityMaxAge";

    private readonly PriorityOptions _options;
    private readonly PollBudget _budget;
    private readonly double _queriesPerSecond;
    private double _maxAgeSeconds;
    private double _maxAgeSum;
    private double _onlineSum;
    private long _ticks;
    private readonly TopK<(double Score, int Index)> _topK = new(HighestScoreFirst.Instance);
    private Estimate[] _estimates = [];

    public PriorityPollingStrategy(double queriesPerMinute, PriorityOptions? options = null, double tickSeconds = 15)
    {
        _options = options ?? new PriorityOptions();
        _budget = new PollBudget(queriesPerMinute, tickSeconds);
        _queriesPerSecond = queriesPerMinute / 60.0;
        _maxAgeSeconds = _options.MaxAgeBudgetShare > 0 ? 0 : _options.MaxAgeSeconds;
    }

    /// <summary>Ticks before this time are left out of the averages below (a simulation's warm-up).</summary>
    public long StatisticsFromMs { get; set; } = long.MinValue;

    /// <summary>The maximum age in force, averaged over the ticks so far (0 when off).</summary>
    public double AverageMaxAgeSeconds => _ticks == 0 ? _maxAgeSeconds : _maxAgeSum / _ticks;

    /// <summary>Servers this strategy believed online, averaged over the ticks so far.</summary>
    public double AverageOnlineServers => _ticks == 0 ? 0 : _onlineSum / _ticks;

    /// <summary>The score of a recently seen server past the maximum age, before its age is added.</summary>
    public const double OverdueScore = 1e15;

    public string Name => "priority";

    public void Tick(long nowMs, IReadOnlyList<PolledServer> servers, IPollExecutor poll)
    {
        EnsureCapacity(servers.Count);
        _topK.Reset(_budget.Next(nowMs));
        UpdateMaxAge(servers, nowMs);

        foreach (var server in servers)
        {
            if (!server.Exists)
            {
                continue;
            }

            var score = Score(server, nowMs);
            if (score > 0)
            {
                _topK.Offer((score, server.Index));
            }
        }

        // Overdue servers sort first; they are polled under their own label so their share can be measured.
        var selected = _topK.ToSortedList();
        var overdue = selected.Where(item => item.Score >= OverdueScore).Select(item => servers[item.Index]).ToArray();
        var scored = selected.Where(item => item.Score < OverdueScore).Select(item => servers[item.Index]).ToArray();
        if (overdue.Length > 0)
        {
            poll.Poll(overdue, MaxAgeLane);
        }

        if (scored.Length > 0)
        {
            poll.Poll(scored, Lane);
        }
    }

    /// <summary>Sets the maximum age for this tick from the servers believed online.</summary>
    public void UpdateMaxAge(IReadOnlyList<PolledServer> servers, long nowMs)
    {
        var online = 0;
        foreach (var server in servers)
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

    /// <summary>The score, or 0 when the server may not be polled yet.</summary>
    public double Score(PolledServer server, long nowMs)
    {
        EnsureCapacity(server.Index + 1);
        ref var estimate = ref _estimates[server.Index];
        var ageSeconds = (nowMs - server.DueKeyMs) / 1000.0;
        var weight = 1 + estimate.PlayersEwma;
        var recentlySeen = server.LastSeenAtMs is { } lastSeen && nowMs - lastSeen <= _options.RecentlySeenSeconds * 1000;
        var guaranteed = recentlySeen && _maxAgeSeconds > 0;

        // The 6 h cap only for servers not seen for a day; a recently seen one never waits past the maximum age.
        var backoffCap = _options.MaxBackoffSeconds;
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

        var minInterval = Math.Min(
            backoffCap,
            _options.MinIntervalSeconds * Math.Pow(2, estimate.UnansweredInRow));
        if (server.LastCheckedAtMs is not null && ageSeconds < minInterval)
        {
            return 0;
        }

        var lambda = (estimate.Changes + _options.PriorChanges) / (estimate.ObservedSeconds + _options.PriorSeconds);
        var age = Math.Max(0, ageSeconds);
        var score = weight * (_options.Score == PriorityScore.ExpectedAge
            ? ExpectedAge(lambda, age)
            : -double.ExpM1(-lambda * age));

        if (guaranteed && age >= _maxAgeSeconds)
        {
            // Overdue: above any score a weight and an age can reach, the oldest first.
            return OverdueScore + age;
        }

        var nearWipe = _options.UsePredictedWipes
            && server.NextWipeAtMs is { } nextWipe
            && Math.Abs(nowMs - nextWipe) <= _options.WipeWindowSeconds * 1000;
        return nearWipe ? score * _options.WipeBoost : score;
    }

    /// <summary><c>age - (1 - exp(-lambda * age)) / lambda</c>, computed without cancellation for small lambda * age.</summary>
    public static double ExpectedAge(double lambda, double age)
    {
        var x = lambda * age;
        if (x < 1e-4)
        {
            // Series: age * (x/2 - x^2/6 + ...).
            return age * (x / 2 - x * x / 6);
        }

        return age - (-double.ExpM1(-x)) / lambda;
    }

    public void OnPolled(PolledServer server, PollOutcome outcome)
    {
        EnsureCapacity(server.Index + 1);
        ref var estimate = ref _estimates[server.Index];
        if (!outcome.Answered)
        {
            estimate.UnansweredInRow++;
            return;
        }

        estimate.UnansweredInRow = 0;
        if (estimate.LastAnswerAtMs is { } lastAt)
        {
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

    public void OnServerReset(PolledServer server)
    {
        EnsureCapacity(server.Index + 1);
        _estimates[server.Index] = default;
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
        public static readonly HighestScoreFirst Instance = new();

        public int Compare((double Score, int Index) x, (double Score, int Index) y)
        {
            var byScore = y.Score.CompareTo(x.Score);
            return byScore != 0 ? byScore : x.Index.CompareTo(y.Index);
        }
    }
}
