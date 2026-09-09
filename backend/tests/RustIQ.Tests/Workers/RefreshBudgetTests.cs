using RustIQ.Application.ServerQuery;

namespace RustIQ.Tests.Workers;

/// <summary>
/// The refresh lanes and the discovery sweep are driven by intervals that only mean anything if a
/// batch can actually be polled inside them. These pin that relationship, because when it breaks
/// nothing fails loudly - the timer simply skips ticks and servers quietly go stale, which is enough
/// to push a healthy server past its offline threshold and mark it offline.
/// </summary>
public sealed class RefreshBudgetTests
{
    private static TimeSpan WorstCaseQuery(RustServerQueryOptions queryOptions)
    {
        return TimeSpan.FromMilliseconds(
            (double)queryOptions.MaxQueryAttempts * queryOptions.TimeoutMilliseconds);
    }

    private static TimeSpan WorstCaseLane(
        int batchSize,
        BattleMetricsSummaryRefreshOptions refreshOptions,
        RustServerQueryOptions queryOptions)
    {
        var waves = (int)Math.Ceiling(batchSize / (double)refreshOptions.RefreshConcurrency);
        return waves * WorstCaseQuery(queryOptions);
    }

    public static TheoryData<string, Func<BattleMetricsSummaryRefreshOptions, int>, Func<BattleMetricsSummaryRefreshOptions, TimeSpan>> Lanes =>
        new()
        {
            {
                "Fast",
                options => options.FastLaneBatchSize,
                options => TimeSpan.FromSeconds(options.FastLaneRefreshIntervalSeconds)
            },
            {
                "NormalOnline",
                options => options.NormalOnlineLaneBatchSize,
                options => TimeSpan.FromMinutes(options.NormalOnlineRefreshIntervalMinutes)
            },
            {
                "SlowOffline",
                options => options.SlowOfflineLaneBatchSize,
                options => TimeSpan.FromMinutes(options.SlowOfflineRefreshIntervalMinutes)
            },
            {
                "VeryStaleOffline",
                options => options.VeryStaleOfflineLaneBatchSize,
                options => TimeSpan.FromMinutes(options.VeryStaleOfflineRefreshIntervalMinutes)
            }
        };

    [Theory]
    [MemberData(nameof(Lanes))]
    public void EachLaneCanPollItsBatchInsideItsOwnInterval(
        string laneName,
        Func<BattleMetricsSummaryRefreshOptions, int> batchSize,
        Func<BattleMetricsSummaryRefreshOptions, TimeSpan> interval)
    {
        var refreshOptions = new BattleMetricsSummaryRefreshOptions();
        var queryOptions = new RustServerQueryOptions();

        var worstCase = WorstCaseLane(batchSize(refreshOptions), refreshOptions, queryOptions);

        Assert.True(
            worstCase <= interval(refreshOptions),
            $"The {laneName} lane needs up to {worstCase.TotalSeconds}s to poll "
            + $"{batchSize(refreshOptions)} servers at concurrency {refreshOptions.RefreshConcurrency}, "
            + $"but its interval is {interval(refreshOptions).TotalSeconds}s.");
    }

    [Fact]
    public void AFullTickCannotOutlastTheOfflineThreshold()
    {
        var refreshOptions = new BattleMetricsSummaryRefreshOptions();
        var queryOptions = new RustServerQueryOptions();

        // All four lanes run one after another inside a single tick, so the time to get back around to
        // any given online server is the whole tick - and the offline decision is measured against the
        // last successful query. If a tick can outlast the threshold, a server that simply was not
        // polled in time gets marked offline.
        var worstCaseTick =
            WorstCaseLane(refreshOptions.FastLaneBatchSize, refreshOptions, queryOptions)
            + WorstCaseLane(refreshOptions.NormalOnlineLaneBatchSize, refreshOptions, queryOptions)
            + WorstCaseLane(refreshOptions.SlowOfflineLaneBatchSize, refreshOptions, queryOptions)
            + WorstCaseLane(refreshOptions.VeryStaleOfflineLaneBatchSize, refreshOptions, queryOptions);

        Assert.True(
            worstCaseTick < TimeSpan.FromSeconds(queryOptions.OfflineThresholdSeconds),
            $"A full refresh tick can take {worstCaseTick.TotalSeconds}s, which is not comfortably "
            + $"inside the {queryOptions.OfflineThresholdSeconds}s offline threshold.");
    }

    [Fact]
    public void DiscoveryCanPollItsNewServersInsideItsRefreshInterval()
    {
        var discoveryOptions = new SteamDiscoveryOptions();
        var queryOptions = new RustServerQueryOptions();

        // These first polls are sequential, unlike the lanes.
        var worstCase = discoveryOptions.MaxNewServerPollsPerRefresh * WorstCaseQuery(queryOptions);

        Assert.True(
            worstCase <= TimeSpan.FromMinutes(discoveryOptions.RefreshIntervalMinutes),
            $"Discovery needs up to {worstCase.TotalSeconds}s to poll "
            + $"{discoveryOptions.MaxNewServerPollsPerRefresh} new servers, but it runs every "
            + $"{TimeSpan.FromMinutes(discoveryOptions.RefreshIntervalMinutes).TotalSeconds}s.");
    }
}
