using RustIQ.Domain.Servers;

namespace RustIQ.Application.Servers.Polling;

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

    public double BatchScale { get; init; } = 1;

    public int ScaledBatch(int batchSize)
    {
        return (int)Math.Round(batchSize * BatchScale, MidpointRounding.AwayFromZero);
    }
}

public sealed class FourLanePollingStrategy : IPollingStrategy
{
    private static readonly ServerRefreshLane[] LaneOrder = new ServerRefreshLane[]
    {
        ServerRefreshLane.Fast,
        ServerRefreshLane.NormalOnline,
        ServerRefreshLane.SlowOffline,
        ServerRefreshLane.VeryStaleOffline
    };

    private readonly FourLaneOptions _options;
    private readonly long?[] _lastRunMs = new long?[LaneOrder.Length];
    private readonly HashSet<int> _claimed = new HashSet<int>();
    private readonly TopK<PolledServer> _topK = new TopK<PolledServer>(LaneOrderComparer.Instance);

    public FourLanePollingStrategy(FourLaneOptions? options = null)
    {
        if (options == null)
        {
            _options = new FourLaneOptions();
        }
        else
        {
            _options = options;
        }
    }

    public string Name
    {
        get { return "fourlane"; }
    }

    public void Tick(long nowMs, IReadOnlyList<PolledServer> servers, IPollExecutor poll)
    {
        _claimed.Clear();
        for (int i = 0; i < LaneOrder.Length; i++)
        {
            ServerRefreshLane lane = LaneOrder[i];
            long intervalMs = IntervalMs(lane);
            long? lastRunMs = _lastRunMs[i];
            if (lastRunMs.HasValue && nowMs - lastRunMs.Value < intervalMs)
            {
                continue;
            }

            List<PolledServer> selected = SelectForLane(lane, nowMs - intervalMs, nowMs, BatchSize(lane), servers);

            List<PolledServer> due = new List<PolledServer>();
            foreach (PolledServer server in selected)
            {
                if (_claimed.Add(server.Index))
                {
                    due.Add(server);
                }
            }

            if (due.Count > 0)
            {
                poll.Poll(due, lane.ToString());
            }

            _lastRunMs[i] = nowMs;
        }
    }

    public List<PolledServer> SelectForLane(
        ServerRefreshLane lane,
        long dueBeforeMs,
        long nowMs,
        int take,
        IReadOnlyList<PolledServer> servers)
    {
        if (take <= 0)
        {
            return new List<PolledServer>();
        }

        long recentPeakSinceMs = nowMs - _options.RecentPeakWindowMinutes * 60_000L;
        long graceSinceMs = nowMs - _options.PopularOfflineGraceMinutes * 60_000L;
        long slowBeforeMs = nowMs - _options.SlowOfflineAfterMinutes * 60_000L;
        long veryStaleBeforeMs = nowMs - _options.VeryStaleOfflineAfterHours * 3_600_000L;

        if (lane == ServerRefreshLane.Fast)
        {
            _topK.Reset(take);
            foreach (PolledServer server in servers)
            {
                if (IsDue(server, dueBeforeMs) && IsPopular(server, recentPeakSinceMs, graceSinceMs))
                {
                    _topK.Offer(server);
                }
            }

            List<PolledServer> popular = _topK.ToSortedList();
            int remaining = take - popular.Count;
            if (remaining <= 0)
            {
                return popular;
            }

            HashSet<int> popularIndexes = new HashSet<int>();
            foreach (PolledServer server in popular)
            {
                popularIndexes.Add(server.Index);
            }

            _topK.Reset(remaining);
            foreach (PolledServer server in servers)
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
        foreach (PolledServer server in servers)
        {
            if (!IsDue(server, dueBeforeMs))
            {
                continue;
            }

            if (IsInLane(lane, server, recentPeakSinceMs, slowBeforeMs, veryStaleBeforeMs))
            {
                _topK.Offer(server);
            }
        }

        return _topK.ToSortedList();
    }

    public bool IsPopular(PolledServer server, long recentPeakSinceMs, long graceSinceMs)
    {
        return server.Players >= _options.PopularPlayerThreshold
            || (server.Status == ServerStatus.Online && server.QueuePlayers > 0)
            || IsRanked(server)
            || server.HasSnapshotWithPlayersSince(recentPeakSinceMs, _options.PopularPeakPlayerThreshold)
            || (server.Status != ServerStatus.Online
                && server.LastSeenAtMs.HasValue
                && server.LastSeenAtMs.Value >= graceSinceMs
                && (IsRanked(server)
                    || server.HasSnapshotWithPlayersSince(graceSinceMs, _options.PopularPeakPlayerThreshold)));
    }

    private bool IsInLane(
        ServerRefreshLane lane,
        PolledServer server,
        long recentPeakSinceMs,
        long slowBeforeMs,
        long veryStaleBeforeMs)
    {
        if (lane == ServerRefreshLane.NormalOnline)
        {
            return server.Status == ServerStatus.Online
                && server.Players < _options.PopularPlayerThreshold
                && server.QueuePlayers <= 0
                && !IsRanked(server)
                && !server.HasSnapshotWithPlayersSince(recentPeakSinceMs, _options.PopularPeakPlayerThreshold);
        }

        if (lane == ServerRefreshLane.SlowOffline)
        {
            return server.Status != ServerStatus.Online
                && server.LastSeenAtMs.HasValue
                && server.LastSeenAtMs.Value < slowBeforeMs
                && server.LastSeenAtMs.Value >= veryStaleBeforeMs
                && !IsRanked(server)
                && !server.HasSnapshotWithPlayersSince(recentPeakSinceMs, _options.PopularPeakPlayerThreshold);
        }

        if (lane == ServerRefreshLane.VeryStaleOffline)
        {
            return server.Status != ServerStatus.Online
                && (!server.LastSeenAtMs.HasValue || server.LastSeenAtMs.Value < veryStaleBeforeMs)
                && !IsRanked(server)
                && !server.HasSnapshotWithPlayersSince(recentPeakSinceMs, _options.PopularPeakPlayerThreshold);
        }

        return false;
    }

    private bool IsRanked(PolledServer server)
    {
        return server.Rank.HasValue && server.Rank.Value <= _options.PopularRankThreshold;
    }

    private static bool IsDue(PolledServer server, long dueBeforeMs)
    {
        return server.Exists && server.DueKeyMs <= dueBeforeMs;
    }

    private long IntervalMs(ServerRefreshLane lane)
    {
        if (lane == ServerRefreshLane.Fast)
        {
            return _options.FastLaneIntervalSeconds * 1000L;
        }

        if (lane == ServerRefreshLane.NormalOnline)
        {
            return _options.NormalOnlineIntervalMinutes * 60_000L;
        }

        if (lane == ServerRefreshLane.SlowOffline)
        {
            return _options.SlowOfflineIntervalMinutes * 60_000L;
        }

        if (lane == ServerRefreshLane.VeryStaleOffline)
        {
            return _options.VeryStaleOfflineIntervalMinutes * 60_000L;
        }

        throw new ArgumentOutOfRangeException(nameof(lane));
    }

    private int BatchSize(ServerRefreshLane lane)
    {
        int batchSize;
        if (lane == ServerRefreshLane.Fast)
        {
            batchSize = _options.FastLaneBatchSize;
        }
        else if (lane == ServerRefreshLane.NormalOnline)
        {
            batchSize = _options.NormalOnlineLaneBatchSize;
        }
        else if (lane == ServerRefreshLane.SlowOffline)
        {
            batchSize = _options.SlowOfflineLaneBatchSize;
        }
        else if (lane == ServerRefreshLane.VeryStaleOffline)
        {
            batchSize = _options.VeryStaleOfflineLaneBatchSize;
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(lane));
        }

        return _options.ScaledBatch(batchSize);
    }

    private sealed class LaneOrderComparer : IComparer<PolledServer>
    {
        public static readonly LaneOrderComparer Instance = new LaneOrderComparer();

        public int Compare(PolledServer? x, PolledServer? y)
        {
            int byDue = x!.DueKeyMs.CompareTo(y!.DueKeyMs);
            if (byDue != 0)
            {
                return byDue;
            }

            int byPlayers = y.Players.CompareTo(x.Players);
            if (byPlayers != 0)
            {
                return byPlayers;
            }

            return x.Index.CompareTo(y.Index);
        }
    }
}
