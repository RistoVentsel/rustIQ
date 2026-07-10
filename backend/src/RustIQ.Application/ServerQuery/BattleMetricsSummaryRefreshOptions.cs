namespace RustIQ.Application.ServerQuery;

public sealed class BattleMetricsSummaryRefreshOptions
{
    public const string SectionName = "BattleMetricsSummaryRefresh";

    public bool Enabled { get; init; }

    public int BatchSize { get; init; } = 50;

    public int RefreshIntervalSeconds { get; init; } = 30;

    public int StaleAfterSeconds { get; init; } = 120;

    public int WorkerTickSeconds { get; init; } = 15;

    public int DirectoryRefreshIntervalMinutes { get; init; } = 3;

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
