namespace RustIQ.Application.ServerQuery;

public sealed class BattleMetricsSummaryRefreshOptions
{
    public const string SectionName = "BattleMetricsSummaryRefresh";

    public bool Enabled { get; init; }

    public int BatchSize { get; init; } = 50;

    public int RefreshIntervalSeconds { get; init; } = 30;

    public int StaleAfterSeconds { get; init; } = 120;

    public int SnapshotIntervalMinutes { get; init; } = 30;

    public bool RefreshCurrentPlayers { get; init; }

    public bool RefreshMapMetadata { get; init; }
}
