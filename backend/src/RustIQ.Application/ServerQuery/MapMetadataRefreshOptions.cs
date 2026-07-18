namespace RustIQ.Application.ServerQuery;

public sealed class MapMetadataRefreshOptions
{
    public const string SectionName = "MapMetadataRefresh";

    public bool Enabled { get; init; }

    public bool RunOnStartup { get; init; } = true;

    public int StartupStaleAfterHours { get; init; } = 8;

    public string TimeZoneId { get; init; } = "Europe/Tallinn";

    public int[] LocalHours { get; init; } = [];

    public int BatchSize { get; init; } = 25;

    public int DelayBetweenServersMilliseconds { get; init; } = 500;

    public int DelayBetweenBatchesSeconds { get; init; } = 10;
}
