namespace RustIQ.Application.ServerQuery;

public sealed class BattleMetricsSummaryRefreshOptions
{
    public const string SectionName = "BattleMetricsSummaryRefresh";

    public bool Enabled { get; init; }

    public int WorkerTickSeconds { get; init; } = 15;

    public int DirectoryRefreshIntervalMinutes { get; init; } = 3;

    // Servers in a lane batch are polled concurrently. Without this the batches were polled one at a
    // time, so a lane of 100 servers took up to 100 * MaxQueryAttempts * TimeoutMilliseconds to get
    // through - far longer than the lane's own interval, which made the intervals below meaningless and
    // could push a server past its offline threshold purely because the batch was slow. Each poll takes
    // its own DI scope, so this also bounds how many database connections a lane holds at once.
    //
    // Concurrency does not by itself set throughput - batch size over interval does - it decides
    // whether a batch fits inside its window at all. Raising it is what buys room for the batch sizes
    // below. The ceiling is the connection pool: at 32 a full lane holds 32 of Npgsql's default 100,
    // which leaves the API's own requests and the other workers comfortable room.
    public int RefreshConcurrency { get; init; } = 32;

    public int FastLaneRefreshIntervalSeconds { get; init; } = 45;

    public int NormalOnlineRefreshIntervalMinutes { get; init; } = 3;

    public int SlowOfflineRefreshIntervalMinutes { get; init; } = 20;

    // Sized against what the lanes can actually do now that polling is concurrent and every query is
    // bounded: 500 servers finish in about 96 seconds of the 15 minute window. At the old 100 per hour
    // a stale server waited days for its turn, which is why servers kept showing a status from weeks
    // earlier. RefreshBudgetTests pins the batch, concurrency and interval against each other.
    public int VeryStaleOfflineRefreshIntervalMinutes { get; init; } = 15;

    public int PopularOfflineGraceMinutes { get; init; } = 60;

    public int SlowOfflineAfterMinutes { get; init; } = 60;

    public int VeryStaleOfflineAfterHours { get; init; } = 24;

    public int PopularRankThreshold { get; init; } = 250;

    public int PopularPlayerThreshold { get; init; } = 100;

    public int PopularPeakPlayerThreshold { get; init; } = 100;

    public int RecentPeakWindowMinutes { get; init; } = 60;

    public int FastLaneBatchSize { get; init; } = 100;

    // The lane that decides how fresh the list feels: it covers every online server that is not
    // popular and not official, which is most of them. At 100 per 3 minutes a server in that group
    // waited about five hours between polls once discovery had registered ten thousand of them, so
    // the page was showing player counts from hours earlier. 400 per 3 minutes brings that round trip
    // down to roughly an hour and a quarter.
    public int NormalOnlineLaneBatchSize { get; init; } = 400;

    public int SlowOfflineLaneBatchSize { get; init; } = 500;

    public int VeryStaleOfflineLaneBatchSize { get; init; } = 500;

    public int SnapshotIntervalMinutes { get; init; } = 30;

    public bool RefreshCurrentPlayers { get; init; }

    public bool RefreshMapMetadata { get; init; }
}
