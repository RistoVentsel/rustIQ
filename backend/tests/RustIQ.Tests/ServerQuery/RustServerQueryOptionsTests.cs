using RustIQ.Application.ServerQuery;

namespace RustIQ.Tests.ServerQuery;

public sealed class RustServerQueryOptionsTests
{
    [Fact]
    public void OfflineThresholdOutlastsTheIntervalAtWhichOnlineServersArePolled()
    {
        var queryOptions = new RustServerQueryOptions();
        var refreshOptions = new BattleMetricsSummaryRefreshOptions();
        var normalOnlineInterval = TimeSpan.FromMinutes(refreshOptions.NormalOnlineRefreshIntervalMinutes);

        // The offline decision is "unreachable for longer than this threshold", measured from the last
        // successful query. If the threshold is not comfortably longer than the gap between polls,
        // every poll is already past it and one dropped UDP packet marks a healthy server offline.
        // Two polls of headroom keeps a single failure - and a server restart - from counting as down.
        Assert.True(
            TimeSpan.FromSeconds(queryOptions.OfflineThresholdSeconds) >= normalOnlineInterval * 2,
            $"OfflineThresholdSeconds ({queryOptions.OfflineThresholdSeconds}s) must stay well above the "
            + $"{normalOnlineInterval.TotalSeconds}s online poll interval.");
    }

    [Fact]
    public void WatchedOfflineThresholdOutlastsSeveralDetailPagePolls()
    {
        var queryOptions = new RustServerQueryOptions();

        // WatchedServerRefreshWorker polls every 5 seconds, so this only needs to outlast a handful of
        // dropped packets - keeping it short is what stops a live page insisting a dead server is up.
        Assert.InRange(queryOptions.WatchedOfflineThresholdSeconds, 20, 120);
    }
}
