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
    public int RefreshConcurrency { get; init; } = 16;

    public int FastLaneRefreshIntervalSeconds { get; init; } = 45;

    public int NormalOnlineRefreshIntervalMinutes { get; init; } = 3;

    public int SlowOfflineRefreshIntervalMinutes { get; init; } = 20;

    public int VeryStaleOfflineRefreshIntervalMinutes { get; init; } = 60;

    public int PopularOfflineGraceMinutes { get; init; } = 60;

    public int SlowOfflineAfterMinutes { get; init; } = 60;

    public int VeryStaleOfflineAfterHours { get; init; } = 24;

    public int PopularRankThreshold { get; init; } = 250;

    public int PopularPlayerThreshold { get; init; } = 100;

    public int PopularPeakPlayerThreshold { get; init; } = 100;

    public int RecentPeakWindowMinutes { get; init; } = 60;

    public int FastLaneBatchSize { get; init; } = 50;

    public int NormalOnlineLaneBatchSize { get; init; } = 100;

    public int SlowOfflineLaneBatchSize { get; init; } = 100;

    public int VeryStaleOfflineLaneBatchSize { get; init; } = 100;

    public int SnapshotIntervalMinutes { get; init; } = 30;

    public bool RefreshCurrentPlayers { get; init; }

    public bool RefreshMapMetadata { get; init; }
}
