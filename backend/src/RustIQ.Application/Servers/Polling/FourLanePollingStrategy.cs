using RustIQ.Domain.Servers;

namespace RustIQ.Application.Servers.Polling;

/// <summary>
/// Production's lane parameters (<c>BattleMetricsSummaryRefresh__*</c> in docker-compose.vps.yml).
/// </summary>
public sealed class FourLaneOptions
{
    public int FastLaneIntervalSeconds { get; init; } = 45;
    public int NormalOnlineIntervalMinutes { get; init; } = 3;
    public int SlowOfflineIntervalMinutes { get; init; } = 20;
    public int VeryStaleOfflineIntervalMinutes { get; init; } = 15;

    public int FastLaneBatchSize { get; init; } = 100;
    public int NormalOnlineLaneBatchSize { get; init; } = 400;
    public int SlowOfflineLaneBatchSize { get; init; } = 500;
    public int VeryStaleOfflineLaneBatchSize { get; init; } = 500;

    public int PopularRankThreshold { get; init; } = 250;
    public int PopularPlayerThreshold { get; init; } = 100;
    public int PopularPeakPlayerThreshold { get; init; } = 100;
    public int RecentPeakWindowMinutes { get; init; } = 60;
    public int PopularOfflineGraceMinutes { get; init; } = 60;
    public int SlowOfflineAfterMinutes { get; init; } = 60;
    public int VeryStaleOfflineAfterHours { get; init; } = 24;

    /// <summary>Multiplies every batch size; used to run the strategy at other query budgets.</summary>
    public double BatchScale { get; init; } = 1;

    public int ScaledBatch(int batchSize) => (int)Math.Round(batchSize * BatchScale, MidpointRounding.AwayFromZero);
}

/// <summary>
/// The current production strategy, ported from <c>BattleMetricsPlayerCountRefreshWorker</c> and
/// <c>ServerRepository.GetServersForRefreshLaneAsync</c>: four lanes with their own interval and batch,
/// run in a fixed order each tick.
/// </summary>
public sealed class FourLanePollingStrategy : IPollingStrategy
{
    private static readonly ServerRefreshLane[] LaneOrder =
    [
        ServerRefreshLane.Fast,
        ServerRefreshLane.NormalOnline,
        ServerRefreshLane.SlowOffline,
        ServerRefreshLane.VeryStaleOffline
    ];

    private readonly FourLaneOptions _options;
    private readonly long?[] _lastRunMs = new long?[LaneOrder.Length];
    private readonly HashSet<int> _claimed = [];
    private readonly TopK<PolledServer> _topK = new(LaneOrderComparer.Instance);

    public FourLanePollingStrategy(FourLaneOptions? options = null)
    {
        _options = options ?? new FourLaneOptions();
    }

    public string Name => "fourlane";

    public void Tick(long nowMs, IReadOnlyList<PolledServer> servers, IPollExecutor poll)
    {
        _claimed.Clear();
        for (var i = 0; i < LaneOrder.Length; i++)
        {
            var lane = LaneOrder[i];
            var intervalMs = IntervalMs(lane);
            if (_lastRunMs[i] is { } lastRun && nowMs - lastRun < intervalMs)
            {
                continue;
            }

            var selected = SelectForLane(lane, nowMs - intervalMs, nowMs, BatchSize(lane), servers);

            // A server an earlier lane took this tick is skipped, but it still used up a batch slot.
            var due = selected.Where(server => _claimed.Add(server.Index)).ToArray();
            if (due.Length > 0)
            {
                poll.Poll(due, lane.ToString());
            }

            _lastRunMs[i] = nowMs;
        }
    }

    /// <summary>The servers <c>GetServersForRefreshLaneAsync</c> returns for one lane, in its order.</summary>
    public List<PolledServer> SelectForLane(
        ServerRefreshLane lane,
        long dueBeforeMs,
        long nowMs,
        int take,
        IReadOnlyList<PolledServer> servers)
    {
        if (take <= 0)
        {
            return [];
        }

        var recentPeakSinceMs = nowMs - _options.RecentPeakWindowMinutes * 60_000L;
        var graceSinceMs = nowMs - _options.PopularOfflineGraceMinutes * 60_000L;
        var slowBeforeMs = nowMs - _options.SlowOfflineAfterMinutes * 60_000L;
        var veryStaleBeforeMs = nowMs - _options.VeryStaleOfflineAfterHours * 3_600_000L;

        if (lane == ServerRefreshLane.Fast)
        {
            _topK.Reset(take);
            foreach (var server in servers)
            {
                if (IsDue(server, dueBeforeMs) && IsPopular(server, recentPeakSinceMs, graceSinceMs))
                {
                    _topK.Offer(server);
                }
            }

            var popular = _topK.ToSortedList();
            var remaining = take - popular.Count;
            if (remaining <= 0)
            {
                return popular;
            }

            // Official servers only fill the capacity popular servers left over.
            var popularIndexes = popular.Select(server => server.Index).ToHashSet();
            _topK.Reset(remaining);
            foreach (var server in servers)
            {
                if (IsDue(server, dueBeforeMs)
                    && server.ServerType == RustServerType.Official
                    && !popularIndexes.Contains(server.Index))
                {
                    _topK.Offer(server);
                }
            }

            popular.AddRange(_topK.ToSortedList());
            return popular;
        }

        _topK.Reset(take);
        foreach (var server in servers)
        {
            if (!IsDue(server, dueBeforeMs))
            {
                continue;
            }

            var member = lane switch
            {
                ServerRefreshLane.NormalOnline =>
                    server.Status == ServerStatus.Online
                    && server.Players < _options.PopularPlayerThreshold
                    && server.QueuePlayers <= 0
                    && !IsRanked(server)
                    && !server.HasSnapshotWithPlayersSince(recentPeakSinceMs, _options.PopularPeakPlayerThreshold),
                ServerRefreshLane.SlowOffline =>
                    server.Status != ServerStatus.Online
                    && server.LastSeenAtMs is { } lastSeen
                    && lastSeen < slowBeforeMs
                    && lastSeen >= veryStaleBeforeMs
                    && !IsRanked(server)
                    && !server.HasSnapshotWithPlayersSince(recentPeakSinceMs, _options.PopularPeakPlayerThreshold),
                ServerRefreshLane.VeryStaleOffline =>
                    server.Status != ServerStatus.Online
                    && (server.LastSeenAtMs is null || server.LastSeenAtMs < veryStaleBeforeMs)
                    && !IsRanked(server)
                    && !server.HasSnapshotWithPlayersSince(recentPeakSinceMs, _options.PopularPeakPlayerThreshold),
                _ => false
            };

            if (member)
            {
                _topK.Offer(server);
            }
        }

        return _topK.ToSortedList();
    }

    public bool IsPopular(PolledServer server, long recentPeakSinceMs, long graceSinceMs) =>
        server.Players >= _options.PopularPlayerThreshold
        || (server.Status == ServerStatus.Online && server.QueuePlayers > 0)
        || IsRanked(server)
        || server.HasSnapshotWithPlayersSince(recentPeakSinceMs, _options.PopularPeakPlayerThreshold)
        || (server.Status != ServerStatus.Online
            && server.LastSeenAtMs is { } lastSeen
            && lastSeen >= graceSinceMs
            && (IsRanked(server)
                || server.HasSnapshotWithPlayersSince(graceSinceMs, _options.PopularPeakPlayerThreshold)));

    private bool IsRanked(PolledServer server) => server.Rank is { } rank && rank <= _options.PopularRankThreshold;

    // Every lane also needs Host != '' and QueryPort > 0; every exported server has both.
    private static bool IsDue(PolledServer server, long dueBeforeMs) => server.Exists && server.DueKeyMs <= dueBeforeMs;

    private long IntervalMs(ServerRefreshLane lane) => lane switch
    {
        ServerRefreshLane.Fast => _options.FastLaneIntervalSeconds * 1000L,
        ServerRefreshLane.NormalOnline => _options.NormalOnlineIntervalMinutes * 60_000L,
        ServerRefreshLane.SlowOffline => _options.SlowOfflineIntervalMinutes * 60_000L,
        ServerRefreshLane.VeryStaleOffline => _options.VeryStaleOfflineIntervalMinutes * 60_000L,
        _ => throw new ArgumentOutOfRangeException(nameof(lane))
    };

    private int BatchSize(ServerRefreshLane lane) => _options.ScaledBatch(lane switch
    {
        ServerRefreshLane.Fast => _options.FastLaneBatchSize,
        ServerRefreshLane.NormalOnline => _options.NormalOnlineLaneBatchSize,
        ServerRefreshLane.SlowOffline => _options.SlowOfflineLaneBatchSize,
        ServerRefreshLane.VeryStaleOffline => _options.VeryStaleOfflineLaneBatchSize,
        _ => throw new ArgumentOutOfRangeException(nameof(lane))
    });

    /// <summary>
    /// <c>ORDER BY LastCheckedAtUtc ?? CreatedAtUtc, Players DESC</c>. SQL leaves further ties
    /// unordered; the index makes the simulation deterministic.
    /// </summary>
    private sealed class LaneOrderComparer : IComparer<PolledServer>
    {
        public static readonly LaneOrderComparer Instance = new();

        public int Compare(PolledServer? x, PolledServer? y)
        {
            var byDue = x!.DueKeyMs.CompareTo(y!.DueKeyMs);
            if (byDue != 0) return byDue;
            var byPlayers = y.Players.CompareTo(x.Players);
            return byPlayers != 0 ? byPlayers : x.Index.CompareTo(y.Index);
        }
    }
}
